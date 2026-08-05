#[cfg(not(windows))]
fn main() {
    eprintln!("ys-netsh-portproxy-helper is available only on Windows");
    std::process::exit(1);
}

#[cfg(windows)]
fn main() {
    if let Err(error) = run() {
        eprintln!("Privileged helper failed: {error}");
        std::process::exit(1);
    }
}

#[cfg(windows)]
fn run() -> Result<(), Box<dyn std::error::Error>> {
    let mut arguments = std::env::args().skip(1);
    let mut pipe = None;
    let mut parent_pid = None;
    let mut nonce = None;
    while let Some(flag) = arguments.next() {
        let value = arguments
            .next()
            .ok_or_else(|| format!("missing value for {flag}"))?;
        match flag.as_str() {
            "--pipe" if pipe.is_none() => pipe = Some(value),
            "--parent-pid" if parent_pid.is_none() => {
                parent_pid = Some(value.parse::<u32>()?);
            }
            "--nonce" if nonce.is_none() => nonce = Some(value),
            _ => return Err(format!("unsupported or duplicate argument: {flag}").into()),
        }
    }
    let pipe = pipe.ok_or("missing --pipe")?;
    let parent_pid = parent_pid.ok_or("missing --parent-pid")?;
    let nonce = nonce.ok_or("missing --nonce")?;
    ys_netsh_portproxy::windows::serve_helper(
        &pipe,
        parent_pid,
        &nonce,
        ys_netsh_portproxy::windows::execute_privileged,
    )?;
    Ok(())
}
