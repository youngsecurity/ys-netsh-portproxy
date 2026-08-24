#![allow(unsafe_code)]

use std::{
    thread,
    time::{Duration, Instant},
};

use windows::{
    core::{w, PCWSTR},
    Win32::System::Services::{
        CloseServiceHandle, ControlService, OpenSCManagerW, OpenServiceW, QueryServiceStatus,
        StartServiceW, SC_HANDLE, SC_MANAGER_CONNECT, SERVICE_CONTROL_PARAMCHANGE,
        SERVICE_PAUSE_CONTINUE, SERVICE_QUERY_STATUS, SERVICE_RUNNING, SERVICE_START,
        SERVICE_START_PENDING, SERVICE_STATUS, SERVICE_STOPPED, SERVICE_STOP_PENDING,
    },
};

use crate::app::{AppError, ServiceState};

#[derive(Debug, Clone, Copy, Default)]
pub struct IpHelperService;

impl IpHelperService {
    #[must_use]
    pub const fn new() -> Self {
        Self
    }

    pub fn state(&self) -> Result<ServiceState, AppError> {
        let service = open_service(SERVICE_QUERY_STATUS)?;
        let mut status = SERVICE_STATUS::default();
        unsafe { QueryServiceStatus(service.0, &raw mut status) }.map_err(adapter_error)?;
        Ok(match status.dwCurrentState {
            SERVICE_RUNNING => ServiceState::Running,
            SERVICE_STOPPED => ServiceState::Stopped,
            SERVICE_START_PENDING => ServiceState::StartPending,
            SERVICE_STOP_PENDING => ServiceState::StopPending,
            _ => ServiceState::Unknown,
        })
    }

    pub fn start(&self) -> Result<(), AppError> {
        if self.state()? == ServiceState::Running {
            return Ok(());
        }
        let service = open_service(SERVICE_START | SERVICE_QUERY_STATUS)?;
        unsafe { StartServiceW(service.0, None) }.map_err(adapter_error)?;
        let deadline = Instant::now() + Duration::from_secs(15);
        while Instant::now() < deadline {
            if self.state()? == ServiceState::Running {
                return Ok(());
            }
            thread::sleep(Duration::from_millis(100));
        }
        Err(AppError::Adapter(
            "IP Helper did not reach the running state before timeout".to_owned(),
        ))
    }

    pub fn reload(&self) -> Result<(), AppError> {
        if self.state()? != ServiceState::Running {
            self.start()?;
        }
        let service = open_service(SERVICE_PAUSE_CONTINUE | SERVICE_QUERY_STATUS)?;
        let mut status = SERVICE_STATUS::default();
        unsafe { ControlService(service.0, SERVICE_CONTROL_PARAMCHANGE, &raw mut status) }
            .map_err(adapter_error)
    }
}

struct OwnedServiceHandle(SC_HANDLE);

impl Drop for OwnedServiceHandle {
    fn drop(&mut self) {
        let _ = unsafe { CloseServiceHandle(self.0) };
    }
}

fn open_service(access: u32) -> Result<OwnedServiceHandle, AppError> {
    let manager = unsafe { OpenSCManagerW(PCWSTR::null(), PCWSTR::null(), SC_MANAGER_CONNECT) }
        .map_err(adapter_error)?;
    let manager = OwnedServiceHandle(manager);
    let service =
        unsafe { OpenServiceW(manager.0, w!("iphlpsvc"), access) }.map_err(adapter_error)?;
    Ok(OwnedServiceHandle(service))
}

fn adapter_error(error: impl std::fmt::Display) -> AppError {
    AppError::Adapter(error.to_string())
}
