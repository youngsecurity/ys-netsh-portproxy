#![allow(unsafe_code)]

use std::{
    fmt::Write as _,
    io::{self, Read, Write},
    num::NonZeroU8,
    os::windows::io::AsRawHandle,
    path::Path,
    sync::atomic::{AtomicU64, Ordering},
    thread,
    time::{Duration, Instant},
};

use interprocess::{
    os::windows::{
        named_pipe::{pipe_mode, DuplexPipeStream, PipeListenerOptions},
        security_descriptor::SecurityDescriptor,
    },
    ConnectWaitMode,
};
use widestring::U16CString;
use windows::{
    core::{w, PCWSTR, PWSTR},
    Win32::{
        Foundation::{CloseHandle, LocalFree, HANDLE, HLOCAL},
        Security::{
            Authorization::ConvertSidToStringSidW, GetTokenInformation, TokenUser, TOKEN_QUERY,
            TOKEN_USER,
        },
        System::{
            Com::{CoInitializeEx, CoUninitialize, COINIT_APARTMENTTHREADED},
            Pipes::{GetNamedPipeClientProcessId, GetNamedPipeServerProcessId},
            Threading::{
                GetProcessId, OpenProcess, OpenProcessToken, PROCESS_QUERY_LIMITED_INFORMATION,
            },
        },
        UI::{
            Shell::{
                ShellExecuteExW, SEE_MASK_NOASYNC, SEE_MASK_NOCLOSEPROCESS, SHELLEXECUTEINFOW,
            },
            WindowsAndMessaging::SW_HIDE,
        },
    },
};

use crate::{
    app::{AppError, PrivilegedExecutor},
    protocol::{
        read_frame, write_frame, CommandResult, HelperFailure, HelperFailureCode,
        PrivilegedCommand, RequestEnvelope, ResponseEnvelope, PROTOCOL_VERSION,
    },
};

static NEXT_REQUEST_ID: AtomicU64 = AtomicU64::new(1);
const CONNECT_TIMEOUT: Duration = Duration::from_secs(30);
const SESSION_TIMEOUT: Duration = Duration::from_secs(300);

#[derive(Debug, Clone, Copy, Default)]
pub struct ElevatedHelperClient;

impl ElevatedHelperClient {
    #[must_use]
    pub const fn new() -> Self {
        Self
    }
}

impl PrivilegedExecutor for ElevatedHelperClient {
    fn execute(&self, command: PrivilegedCommand) -> Result<CommandResult, AppError> {
        command
            .validate()
            .map_err(|error| AppError::Adapter(error.to_string()))?;
        let nonce = generate_nonce()?;
        let pipe_path = format!(r"\\.\pipe\ys-netsh-portproxy-{nonce}");
        let helper = helper_path()?;
        let process = launch_elevated(&helper, &pipe_path, &nonce)?;
        let helper_pid = unsafe { GetProcessId(process.0) };
        if helper_pid == 0 {
            return Err(AppError::Adapter(
                "could not determine helper process ID".to_owned(),
            ));
        }

        let deadline = Instant::now() + CONNECT_TIMEOUT;
        let mut stream = loop {
            match DuplexPipeStream::<pipe_mode::Bytes>::connect_by_path_with_wait_mode(
                pipe_path.as_str(),
                ConnectWaitMode::Timeout(Duration::from_millis(250)),
            ) {
                Ok(stream) => break stream,
                Err(error) if error.kind() == io::ErrorKind::PermissionDenied => {
                    return Err(AppError::Adapter(format!(
                        "access denied connecting to the elevated helper's named pipe; \
                         the pipe security descriptor does not admit this process token: {error}"
                    )));
                }
                Err(error) if Instant::now() < deadline => {
                    if !matches!(
                        error.kind(),
                        io::ErrorKind::NotFound
                            | io::ErrorKind::WouldBlock
                            | io::ErrorKind::TimedOut
                    ) {
                        return Err(adapter_error(error));
                    }
                    thread::sleep(Duration::from_millis(25));
                }
                Err(_) => {
                    return Err(AppError::Adapter(
                        "timed out connecting to elevated helper".to_owned(),
                    ));
                }
            }
        };

        let mut server_pid = 0_u32;
        unsafe { GetNamedPipeServerProcessId(HANDLE(stream.as_raw_handle()), &raw mut server_pid) }
            .map_err(adapter_error)?;
        if server_pid != helper_pid {
            return Err(AppError::Adapter(
                "named-pipe server did not match launched helper".to_owned(),
            ));
        }

        stream.set_nonblocking(true).map_err(adapter_error)?;
        let request_id = NEXT_REQUEST_ID.fetch_add(1, Ordering::Relaxed);
        let request = RequestEnvelope::new(request_id, nonce, command);
        let mut session = DeadlineIo::new(&mut stream, SESSION_TIMEOUT);
        write_frame(&mut session, &request).map_err(adapter_error)?;
        let response: ResponseEnvelope = read_frame(&mut session).map_err(adapter_error)?;
        if response.version != PROTOCOL_VERSION || response.request_id != request_id {
            return Err(AppError::Adapter(
                "helper response version or request ID did not match".to_owned(),
            ));
        }
        response.result.map_err(AppError::Helper)
    }
}

pub fn serve_helper(
    pipe_path: &str,
    parent_pid: u32,
    expected_nonce: &str,
    execute: impl FnOnce(PrivilegedCommand) -> Result<CommandResult, HelperFailure>,
) -> Result<(), AppError> {
    if !pipe_path.starts_with(r"\\.\pipe\ys-netsh-portproxy-")
        || expected_nonce.len() != 64
        || !expected_nonce.bytes().all(|byte| byte.is_ascii_hexdigit())
    {
        return Err(AppError::Adapter(
            "invalid helper channel arguments".to_owned(),
        ));
    }

    let security_descriptor = pipe_security_descriptor(parent_pid)?;
    let listener = PipeListenerOptions::new()
        .path(pipe_path)
        .security_descriptor(Some(security_descriptor))
        .nonblocking(true)
        .instance_limit(NonZeroU8::new(2))
        .accept_remote(false)
        .input_buffer_size_hint(64 * 1024)
        .output_buffer_size_hint(64 * 1024)
        .create_duplex::<pipe_mode::Bytes>()
        .map_err(adapter_error)?;
    let deadline = Instant::now() + CONNECT_TIMEOUT;
    let mut stream = loop {
        match listener.accept() {
            Ok(stream) => break stream,
            Err(error) if error.kind() == io::ErrorKind::WouldBlock => {
                if Instant::now() >= deadline {
                    return Err(AppError::Adapter(
                        "timed out waiting for the GUI client".to_owned(),
                    ));
                }
                thread::sleep(Duration::from_millis(25));
            }
            Err(error) => return Err(adapter_error(error)),
        }
    };
    stream.set_nonblocking(true).map_err(adapter_error)?;

    let mut client_pid = 0_u32;
    unsafe { GetNamedPipeClientProcessId(HANDLE(stream.as_raw_handle()), &raw mut client_pid) }
        .map_err(adapter_error)?;
    if client_pid != parent_pid {
        return Err(AppError::Adapter(
            "named-pipe client did not match the launching UI process".to_owned(),
        ));
    }

    let mut session = DeadlineIo::new(&mut stream, SESSION_TIMEOUT);
    let request: RequestEnvelope = read_frame(&mut session).map_err(adapter_error)?;
    let result = if request.session_nonce != expected_nonce {
        Err(HelperFailure {
            code: HelperFailureCode::AccessDenied,
            message: "session nonce mismatch".to_owned(),
        })
    } else if let Err(error) = request.validate().and_then(|()| request.command.validate()) {
        Err(HelperFailure {
            code: HelperFailureCode::InvalidRequest,
            message: error.to_string(),
        })
    } else {
        execute(request.command)
    };
    let response = ResponseEnvelope {
        version: PROTOCOL_VERSION,
        request_id: request.request_id,
        result,
    };
    write_frame(&mut session, &response).map_err(adapter_error)
}

struct DeadlineIo<T> {
    inner: T,
    deadline: Instant,
}

impl<T> DeadlineIo<T> {
    fn new(inner: T, timeout: Duration) -> Self {
        Self {
            inner,
            deadline: Instant::now() + timeout,
        }
    }

    fn wait_or_timeout(&self) -> io::Result<()> {
        if Instant::now() >= self.deadline {
            Err(io::Error::new(
                io::ErrorKind::TimedOut,
                "named-pipe session timed out",
            ))
        } else {
            thread::sleep(Duration::from_millis(10));
            Ok(())
        }
    }
}

impl<T: Read> Read for DeadlineIo<T> {
    fn read(&mut self, buffer: &mut [u8]) -> io::Result<usize> {
        loop {
            match self.inner.read(buffer) {
                Ok(0) if !buffer.is_empty() => self.wait_or_timeout()?,
                Err(error) if error.kind() == io::ErrorKind::WouldBlock => {
                    self.wait_or_timeout()?;
                }
                result => return result,
            }
        }
    }
}

impl<T: Write> Write for DeadlineIo<T> {
    fn write(&mut self, buffer: &[u8]) -> io::Result<usize> {
        loop {
            match self.inner.write(buffer) {
                Ok(0) if !buffer.is_empty() => self.wait_or_timeout()?,
                Err(error) if error.kind() == io::ErrorKind::WouldBlock => {
                    self.wait_or_timeout()?;
                }
                result => return result,
            }
        }
    }

    fn flush(&mut self) -> io::Result<()> {
        loop {
            match self.inner.flush() {
                Err(error) if error.kind() == io::ErrorKind::WouldBlock => {
                    self.wait_or_timeout()?;
                }
                result => return result,
            }
        }
    }
}

struct OwnedProcess(HANDLE);

impl Drop for OwnedProcess {
    fn drop(&mut self) {
        let _ = unsafe { CloseHandle(self.0) };
    }
}

fn launch_elevated(
    helper_path: &Path,
    pipe_path: &str,
    nonce: &str,
) -> Result<OwnedProcess, AppError> {
    let _apartment = ComApartment::initialize()?;
    let file = wide_null(&helper_path.to_string_lossy());
    let parameters = wide_null(&format!(
        "--pipe {pipe_path} --parent-pid {} --nonce {nonce}",
        std::process::id()
    ));
    let mut info = SHELLEXECUTEINFOW {
        cbSize: u32::try_from(std::mem::size_of::<SHELLEXECUTEINFOW>())
            .expect("SHELLEXECUTEINFOW size fits u32"),
        fMask: SEE_MASK_NOCLOSEPROCESS | SEE_MASK_NOASYNC,
        lpVerb: w!("runas"),
        lpFile: PCWSTR(file.as_ptr()),
        lpParameters: PCWSTR(parameters.as_ptr()),
        nShow: SW_HIDE.0,
        ..Default::default()
    };
    unsafe { ShellExecuteExW(&raw mut info) }.map_err(adapter_error)?;
    if info.hProcess.is_invalid() {
        return Err(AppError::Adapter(
            "elevated helper did not return a process handle".to_owned(),
        ));
    }
    Ok(OwnedProcess(info.hProcess))
}

/// Builds an explicit security descriptor for the helper's named pipe.
///
/// The helper runs elevated, so a default-DACL pipe would only admit `SYSTEM` and
/// `Administrators` — the launching GUI holds the limited (filtered) token, in which the
/// Administrators group is deny-only, and would be rejected with `ACCESS_DENIED`. Grant the
/// launching process's exact user SID instead; request authenticity is still enforced by the
/// client-PID and session-nonce checks in `serve_helper`.
fn pipe_security_descriptor(parent_pid: u32) -> Result<SecurityDescriptor, AppError> {
    let process = unsafe { OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, parent_pid) }
        .map_err(adapter_error)?;
    let process = OwnedProcess(process);
    let mut token = HANDLE::default();
    unsafe { OpenProcessToken(process.0, TOKEN_QUERY, &raw mut token) }.map_err(adapter_error)?;
    let token = OwnedProcess(token);
    let mut needed = 0_u32;
    let _ = unsafe { GetTokenInformation(token.0, TokenUser, None, 0, &raw mut needed) };
    if needed == 0 {
        return Err(AppError::Adapter(
            "could not size the launching process's token user information".to_owned(),
        ));
    }
    let words = usize::try_from(needed.div_ceil(8)).map_err(adapter_error)?;
    let mut buffer = vec![0_u64; words];
    unsafe {
        GetTokenInformation(
            token.0,
            TokenUser,
            Some(buffer.as_mut_ptr().cast()),
            needed,
            &raw mut needed,
        )
    }
    .map_err(adapter_error)?;
    // SAFETY: GetTokenInformation(TokenUser) filled the 8-byte-aligned buffer with a
    // TOKEN_USER structure followed by the SID it points into.
    let token_user = unsafe { &*buffer.as_ptr().cast::<TOKEN_USER>() };
    let mut sid_text = PWSTR::null();
    unsafe { ConvertSidToStringSidW(token_user.User.Sid, &raw mut sid_text) }
        .map_err(adapter_error)?;
    // SAFETY: ConvertSidToStringSidW returned a valid NUL-terminated wide string.
    let sid = unsafe { sid_text.to_string() }.map_err(adapter_error);
    unsafe { LocalFree(Some(HLOCAL(sid_text.as_ptr().cast()))) };
    let sid = sid?;
    let sddl = format!("D:P(A;;GA;;;SY)(A;;GA;;;BA)(A;;GRGW;;;{sid})");
    let sddl = U16CString::from_str(&sddl)
        .map_err(|error| AppError::Adapter(format!("invalid SDDL string: {error}")))?;
    SecurityDescriptor::deserialize(&sddl).map_err(adapter_error)
}

struct ComApartment;

impl ComApartment {
    fn initialize() -> Result<Self, AppError> {
        unsafe { CoInitializeEx(None, COINIT_APARTMENTTHREADED) }
            .ok()
            .map_err(adapter_error)?;
        Ok(Self)
    }
}

impl Drop for ComApartment {
    fn drop(&mut self) {
        unsafe { CoUninitialize() };
    }
}

pub fn helper_path() -> Result<std::path::PathBuf, AppError> {
    let executable = std::env::current_exe().map_err(adapter_error)?;
    let directory = executable
        .parent()
        .ok_or_else(|| AppError::Adapter("GUI executable has no parent directory".to_owned()))?;
    let helper = directory.join("ys-netsh-portproxy-helper.exe");
    if !helper.is_file() {
        return Err(AppError::Adapter(format!(
            "privileged helper not found beside GUI: {}",
            helper.display()
        )));
    }
    Ok(helper)
}

fn generate_nonce() -> Result<String, AppError> {
    let mut bytes = [0_u8; 32];
    getrandom::fill(&mut bytes).map_err(adapter_error)?;
    let mut nonce = String::with_capacity(bytes.len() * 2);
    for byte in bytes {
        write!(&mut nonce, "{byte:02x}").expect("writing to String cannot fail");
    }
    Ok(nonce)
}

fn wide_null(value: &str) -> Vec<u16> {
    value.encode_utf16().chain(std::iter::once(0)).collect()
}

fn adapter_error(error: impl std::fmt::Display) -> AppError {
    AppError::Adapter(error.to_string())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn pipe_security_descriptor_grants_the_launching_user() {
        pipe_security_descriptor(std::process::id()).unwrap();
    }

    #[test]
    fn pipe_security_descriptor_rejects_a_nonexistent_parent() {
        assert!(pipe_security_descriptor(u32::MAX - 1).is_err());
    }

    #[test]
    fn local_pipe_round_trip_checks_peer_and_protocol() {
        let nonce = generate_nonce().unwrap();
        let pipe_path = format!(r"\\.\pipe\ys-netsh-portproxy-test-{nonce}");
        let server_path = pipe_path.clone();
        let server_nonce = nonce.clone();
        let server =
            thread::spawn(move || {
                serve_helper(&server_path, std::process::id(), &server_nonce, |command| {
                    match command {
                        PrivilegedCommand::Probe => Ok(CommandResult::Probe {
                            helper_version: "test".to_owned(),
                            elevated: false,
                        }),
                        _ => unreachable!(),
                    }
                })
            });

        let deadline = Instant::now() + Duration::from_secs(5);
        let mut stream = loop {
            match DuplexPipeStream::<pipe_mode::Bytes>::connect_by_path_with_wait_mode(
                pipe_path.as_str(),
                ConnectWaitMode::Timeout(Duration::from_millis(100)),
            ) {
                Ok(stream) => break stream,
                Err(_) if Instant::now() < deadline => thread::sleep(Duration::from_millis(10)),
                Err(error) => panic!("could not connect to test helper: {error}"),
            }
        };
        stream.set_nonblocking(true).unwrap();
        let request = RequestEnvelope::new(7, nonce, PrivilegedCommand::Probe);
        let mut session = DeadlineIo::new(&mut stream, Duration::from_secs(5));
        write_frame(&mut session, &request).unwrap();
        let response: ResponseEnvelope = read_frame(&mut session).unwrap();
        assert_eq!(response.request_id, 7);
        assert!(matches!(response.result, Ok(CommandResult::Probe { .. })));
        server.join().unwrap().unwrap();
    }
}
