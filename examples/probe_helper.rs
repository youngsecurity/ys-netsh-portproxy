//! Cross-integrity probe of the elevated helper IPC path.
//!
//! Runs the full GUI-side client flow (`ElevatedHelperClient`) with the read-only `Probe`
//! command: launches the privileged helper via UAC, connects to its named pipe, and prints
//! the helper's response. Exit code 0 means the elevated-server / client pipe boundary works.
//!
//! To reproduce the production topology (unelevated GUI, elevated helper), run this binary
//! from an **unelevated** context, e.g. from an elevated shell:
//!
//! ```text
//! runas /trustlevel:0x20000 "C:\path\to\probe_helper.exe"
//! ```
//!
//! The helper executable must sit in the same directory as this binary.

#[cfg(not(windows))]
fn main() {
    eprintln!("probe_helper is available only on Windows");
    std::process::exit(1);
}

#[cfg(windows)]
fn main() {
    use ys_netsh_portproxy::{
        app::PrivilegedExecutor, protocol::PrivilegedCommand, windows::ElevatedHelperClient,
    };

    match ys_netsh_portproxy::windows::is_elevated() {
        Ok(elevated) => println!("probe client elevated: {elevated}"),
        Err(error) => println!("probe client elevation unknown: {error}"),
    }
    match ElevatedHelperClient::new().execute(PrivilegedCommand::Probe) {
        Ok(result) => println!("PROBE OK: {result:?}"),
        Err(error) => {
            eprintln!("PROBE FAILED: {error}");
            std::process::exit(1);
        }
    }
}
