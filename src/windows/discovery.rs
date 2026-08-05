use std::{
    io::{self, Read},
    os::windows::process::CommandExt,
    process::{Command, Output, Stdio},
    thread,
    time::{Duration, Instant},
};

use crate::{
    app::{AppError, IntegrationProbe, ServiceState},
    integrations::{
        parse_docker_containers, parse_ss_listening_ports, parse_wsl_addresses,
        parse_wsl_distributions, DockerStatus, WslStatus,
    },
};

use super::services::IpHelperService;

const CREATE_NO_WINDOW: u32 = 0x0800_0000;
const COMMAND_TIMEOUT: Duration = Duration::from_secs(5);
const MAX_OUTPUT_BYTES: usize = 1024 * 1024;

#[derive(Debug, Clone, Copy, Default)]
pub struct WindowsProbes;

impl WindowsProbes {
    #[must_use]
    pub const fn new() -> Self {
        Self
    }
}

impl IntegrationProbe for WindowsProbes {
    fn service_state(&self) -> Result<ServiceState, AppError> {
        IpHelperService::new().state()
    }

    fn managed_firewall_exists(&self, rule_id: &str) -> Result<bool, AppError> {
        super::firewall::rule_exists(rule_id)
    }

    fn wsl_status(&self) -> WslStatus {
        discover_wsl()
    }

    fn docker_status(&self) -> DockerStatus {
        discover_docker()
    }
}

#[derive(Debug, Clone, Copy)]
pub enum WslAction {
    Start,
    Shutdown,
    Restart,
}

#[derive(Debug, Clone, Copy)]
pub enum DockerAction {
    Start,
    Stop,
    Restart,
}

pub fn run_wsl_action(action: WslAction) -> Result<String, AppError> {
    match action {
        WslAction::Start => run_text("wsl.exe", &["--exec", "true"]),
        WslAction::Shutdown => run_text("wsl.exe", &["--shutdown"]),
        WslAction::Restart => {
            run_text("wsl.exe", &["--shutdown"])
                .map_err(|error| AppError::Adapter(error.to_string()))?;
            run_text("wsl.exe", &["--exec", "true"])
        }
    }
    .map(|_| format!("WSL {action:?} completed"))
    .map_err(|error| AppError::Adapter(error.to_string()))
}

pub fn run_docker_action(action: DockerAction) -> Result<String, AppError> {
    let verb = match action {
        DockerAction::Start => "start",
        DockerAction::Stop => "stop",
        DockerAction::Restart => "restart",
    };
    run_text("docker.exe", &["desktop", verb])
        .map(|_| format!("Docker Desktop {action:?} completed"))
        .map_err(|error| AppError::Adapter(error.to_string()))
}

#[must_use]
pub fn discover_wsl() -> WslStatus {
    let distributions = run_text("wsl.exe", &["--list", "--quiet"])
        .map(|output| parse_wsl_distributions(&output))
        .unwrap_or_default();
    if distributions.is_empty() {
        return WslStatus::default();
    }

    let addresses = run_text("wsl.exe", &["--exec", "hostname", "-I"])
        .map(|output| parse_wsl_addresses(&output))
        .unwrap_or_default();
    let listening_ports = run_text("wsl.exe", &["--exec", "ss", "-H", "-ltn"])
        .map(|output| parse_ss_listening_ports(&output))
        .unwrap_or_default();
    WslStatus {
        available: true,
        distribution: distributions.into_iter().next(),
        addresses,
        listening_ports,
    }
}

#[must_use]
pub fn discover_docker() -> DockerStatus {
    let info = run_text(
        "docker.exe",
        &["info", "--format", "{{.ServerVersion}}\t{{.Name}}"],
    );
    let Ok(_info) = info else {
        return DockerStatus {
            available: command_exists("docker.exe"),
            ..DockerStatus::default()
        };
    };
    let context = run_text("docker.exe", &["context", "show"])
        .ok()
        .map(|value| value.trim().to_owned())
        .filter(|value| !value.is_empty());
    let containers = run_text(
        "docker.exe",
        &["ps", "--format", "{{.ID}}\t{{.Names}}\t{{.Ports}}"],
    )
    .map(|output| parse_docker_containers(&output))
    .unwrap_or_default();
    DockerStatus {
        available: true,
        running: true,
        context,
        containers,
    }
}

fn command_exists(executable: &str) -> bool {
    run_bounded("where.exe", &[executable], Duration::from_secs(2))
        .is_ok_and(|output| output.status.success())
}

fn run_text(executable: &str, arguments: &[&str]) -> io::Result<String> {
    let output = run_bounded(executable, arguments, COMMAND_TIMEOUT)?;
    if !output.status.success() {
        return Err(io::Error::other(
            String::from_utf8_lossy(&output.stderr).trim().to_owned(),
        ));
    }
    Ok(String::from_utf8_lossy(&output.stdout).replace('\0', ""))
}

fn run_bounded(executable: &str, arguments: &[&str], timeout: Duration) -> io::Result<Output> {
    let mut child = Command::new(executable)
        .args(arguments)
        .creation_flags(CREATE_NO_WINDOW)
        .stdin(Stdio::null())
        .stdout(Stdio::piped())
        .stderr(Stdio::piped())
        .spawn()?;
    let stdout = child
        .stdout
        .take()
        .ok_or_else(|| io::Error::other("stdout pipe was not created"))?;
    let stderr = child
        .stderr
        .take()
        .ok_or_else(|| io::Error::other("stderr pipe was not created"))?;
    let stdout_reader = thread::spawn(move || drain_capped(stdout));
    let stderr_reader = thread::spawn(move || drain_capped(stderr));
    let deadline = Instant::now() + timeout;
    let status = loop {
        if let Some(status) = child.try_wait()? {
            break status;
        }
        if Instant::now() >= deadline {
            child.kill()?;
            let _ = child.wait();
            let _ = stdout_reader.join();
            let _ = stderr_reader.join();
            return Err(io::Error::new(io::ErrorKind::TimedOut, "command timed out"));
        }
        thread::sleep(Duration::from_millis(25));
    };
    let stdout = stdout_reader
        .join()
        .map_err(|_| io::Error::other("stdout reader panicked"))??;
    let stderr = stderr_reader
        .join()
        .map_err(|_| io::Error::other("stderr reader panicked"))??;
    Ok(Output {
        status,
        stdout,
        stderr,
    })
}

fn drain_capped(mut reader: impl Read) -> io::Result<Vec<u8>> {
    let mut output = Vec::new();
    let mut buffer = [0_u8; 8 * 1024];
    loop {
        let read = reader.read(&mut buffer)?;
        if read == 0 {
            return Ok(output);
        }
        let remaining = MAX_OUTPUT_BYTES.saturating_sub(output.len());
        output.extend_from_slice(&buffer[..read.min(remaining)]);
    }
}
