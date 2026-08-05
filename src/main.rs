#[cfg(not(windows))]
fn main() {
    eprintln!("ys-netsh-portproxy is a Windows desktop application");
}

#[cfg(windows)]
#[path = "ui.rs"]
mod ui;

#[cfg(windows)]
fn main() -> eframe::Result<()> {
    match ys_netsh_portproxy::windows::is_elevated() {
        Ok(false) => {}
        Ok(true) => {
            eprintln!(
                "ys-netsh-portproxy must run unelevated; launch it from a standard user session"
            );
            return Ok(());
        }
        Err(error) => {
            eprintln!("could not verify the GUI process token: {error}");
            return Ok(());
        }
    }
    let options = eframe::NativeOptions {
        viewport: eframe::egui::ViewportBuilder::default()
            .with_inner_size([1_180.0, 720.0])
            .with_min_inner_size([840.0, 520.0]),
        ..Default::default()
    };
    eframe::run_native(
        "Young Security Port Proxy",
        options,
        Box::new(|context| Ok(Box::new(ui::PortProxyApp::new(context)))),
    )
}
