#![cfg_attr(windows, windows_subsystem = "windows")]

#[cfg(not(windows))]
fn main() {
    eprintln!("ys-netsh-portproxy is a Windows desktop application");
}

#[cfg(windows)]
#[path = "ui.rs"]
mod ui;

#[cfg(windows)]
#[allow(unsafe_code)]
fn show_startup_error(message: &str) {
    use windows::{
        core::HSTRING,
        Win32::UI::WindowsAndMessaging::{MessageBoxW, MB_ICONERROR, MB_OK},
    };

    let message = HSTRING::from(message);
    let title = HSTRING::from("Young Security Port Proxy");
    // SAFETY: Both strings remain alive and valid for the duration of the modal call.
    unsafe {
        MessageBoxW(None, &message, &title, MB_OK | MB_ICONERROR);
    }
}

#[cfg(windows)]
fn main() {
    match ys_netsh_portproxy::windows::is_elevated() {
        Ok(false) => {}
        Ok(true) => {
            show_startup_error(
                "Young Security Port Proxy must run unelevated. Launch it from a standard user session.",
            );
            return;
        }
        Err(error) => {
            show_startup_error(&format!("Could not verify the GUI process token: {error}"));
            return;
        }
    }
    let options = eframe::NativeOptions {
        viewport: eframe::egui::ViewportBuilder::default()
            .with_inner_size([1_180.0, 720.0])
            .with_min_inner_size([840.0, 520.0]),
        ..Default::default()
    };
    if let Err(error) = eframe::run_native(
        "Young Security Port Proxy",
        options,
        Box::new(|context| Ok(Box::new(ui::PortProxyApp::new(context)))),
    ) {
        show_startup_error(&format!("The application could not start: {error}"));
    }
}
