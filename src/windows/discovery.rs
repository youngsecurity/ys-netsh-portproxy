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
        parse_wsl_distributions, DockerBackend, DockerStatus, WslStatus,
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

    fn managed_firewall_groups(
        &self,
        rule_ids: &[String],
    ) -> Result<std::collections::BTreeMap<String, String>, AppError> {
        super::firewall::rule_groups(rule_ids)
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
    discover_docker_with(&run_text)
}

fn discover_docker_with(run: &impl Fn(&str, &[&str]) -> io::Result<String>) -> DockerStatus {
    let mut available_fallback = probe_docker_backend(run, DockerBackend::Windows);
    if available_fallback
        .as_ref()
        .is_some_and(|status| status.running)
    {
        return available_fallback.expect("running Windows Docker status exists");
    }

    let distributions = run("wsl.exe", &["--list", "--quiet"])
        .map(|output| parse_wsl_distributions(&output))
        .unwrap_or_default();
    for distribution in distributions {
        if let Some(status) = probe_docker_backend(
            run,
            DockerBackend::Wsl {
                distribution: distribution.clone(),
            },
        ) {
            if status.running {
                return status;
            }
            available_fallback.get_or_insert(status);
        }
    }
    available_fallback.unwrap_or_default()
}

fn probe_docker_backend(
    run: &impl Fn(&str, &[&str]) -> io::Result<String>,
    backend: DockerBackend,
) -> Option<DockerStatus> {
    if docker_command(run, &backend, &["--version"]).is_err() {
        return None;
    }
    if docker_command(
        run,
        &backend,
        &["info", "--format", "{{.ServerVersion}}\t{{.Name}}"],
    )
    .is_err()
    {
        return Some(DockerStatus {
            available: true,
            backend: Some(backend),
            ..DockerStatus::default()
        });
    }
    let context = docker_command(run, &backend, &["context", "show"])
        .ok()
        .map(|value| value.trim().to_owned())
        .filter(|value| !value.is_empty());
    let containers = docker_command(
        run,
        &backend,
        &["ps", "--format", "{{.ID}}\t{{.Names}}\t{{.Ports}}"],
    )
    .map(|output| parse_docker_containers(&output))
    .unwrap_or_default();
    Some(DockerStatus {
        available: true,
        running: true,
        backend: Some(backend),
        context,
        containers,
    })
}

fn docker_command(
    run: &impl Fn(&str, &[&str]) -> io::Result<String>,
    backend: &DockerBackend,
    arguments: &[&str],
) -> io::Result<String> {
    match backend {
        DockerBackend::Windows => run("docker.exe", arguments),
        DockerBackend::Wsl { distribution } => {
            let mut wsl_arguments =
                vec!["--distribution", distribution.as_str(), "--exec", "docker"];
            wsl_arguments.extend_from_slice(arguments);
            run("wsl.exe", &wsl_arguments)
        }
    }
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

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn discovers_running_docker_inside_wsl_when_windows_cli_is_missing() {
        let run = |executable: &str, arguments: &[&str]| -> io::Result<String> {
            let command = format!("{executable} {}", arguments.join(" "));
            match command.as_str() {
                "docker.exe --version" => Err(io::Error::new(
                    io::ErrorKind::NotFound,
                    "docker.exe is not installed",
                )),
                "wsl.exe --list --quiet" => Ok("Ubuntu\n".to_owned()),
                "wsl.exe --distribution Ubuntu --exec docker --version" => {
                    Ok("Docker version 29.6.1\n".to_owned())
                }
                "wsl.exe --distribution Ubuntu --exec docker info --format {{.ServerVersion}}\t{{.Name}}" => {
                    Ok("29.6.1\tubuntu-docker\n".to_owned())
                }
                "wsl.exe --distribution Ubuntu --exec docker context show" => {
                    Ok("default\n".to_owned())
                }
                "wsl.exe --distribution Ubuntu --exec docker ps --format {{.ID}}\t{{.Names}}\t{{.Ports}}" => {
                    Ok("abc123\tportainer_agent\t9001/tcp\n".to_owned())
                }
                _ => Err(io::Error::other(format!("unexpected command: {command}"))),
            }
        };

        let status = discover_docker_with(&run);

        assert!(status.available);
        assert!(status.running);
        assert_eq!(
            status.backend,
            Some(DockerBackend::Wsl {
                distribution: "Ubuntu".to_owned(),
            })
        );
        assert_eq!(status.context.as_deref(), Some("default"));
        assert_eq!(status.containers.len(), 1);
        assert_eq!(status.containers[0].name, "portainer_agent");
    }
}
