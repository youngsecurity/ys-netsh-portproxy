#![allow(unsafe_code)]

mod discovery;
mod firewall;
mod ipc;
mod registry;
mod services;

use std::{
    collections::BTreeMap,
    path::PathBuf,
    time::{Duration, Instant, SystemTime, UNIX_EPOCH},
};

use windows::Win32::{
    Foundation::{CloseHandle, HANDLE},
    Security::{GetTokenInformation, TokenElevation, TOKEN_ELEVATION, TOKEN_QUERY},
    System::Threading::{GetCurrentProcess, OpenProcessToken},
};

use crate::{
    app::{firewall_rule_id, AppError},
    backup::BackupDocument,
    domain::{reconcile_rules, ManagedRule, ProxyRule, ReconcileMode, RuleChange, RuleKey},
    protocol::{
        CommandResult, FirewallChange, HelperFailure, HelperFailureCode, PrivilegedCommand,
    },
};

pub use discovery::{
    discover_docker, discover_wsl, run_docker_action, run_wsl_action, DockerAction, WindowsProbes,
    WslAction,
};
pub use ipc::{helper_path, serve_helper, ElevatedHelperClient};
pub use registry::{RegistryAdapter, RegistryReadReport};
pub use services::IpHelperService;

pub fn execute_privileged(command: PrivilegedCommand) -> Result<CommandResult, HelperFailure> {
    if !is_elevated().map_err(|error| helper_error(&error))? {
        return Err(HelperFailure {
            code: HelperFailureCode::AccessDenied,
            message: "the privileged helper is not elevated".to_owned(),
        });
    }
    execute_privileged_inner(command).map_err(|error| helper_error(&error))
}

fn execute_privileged_inner(command: PrivilegedCommand) -> Result<CommandResult, AppError> {
    command
        .validate()
        .map_err(|error| AppError::Adapter(error.to_string()))?;
    match command {
        PrivilegedCommand::Probe => Ok(CommandResult::Probe {
            helper_version: env!("CARGO_PKG_VERSION").to_owned(),
            elevated: true,
        }),
        PrivilegedCommand::ApplyRules {
            changes,
            firewall_changes,
        } => apply_verified_batch(&changes, &firewall_changes),
        PrivilegedCommand::StartIpHelper => {
            IpHelperService::new().start()?;
            Ok(CommandResult::ServiceChanged)
        }
        PrivilegedCommand::ReloadIpHelper => {
            IpHelperService::new().reload()?;
            Ok(CommandResult::ServiceChanged)
        }
        PrivilegedCommand::RemoveFirewallRule { rule_id } => {
            firewall::remove_rule(&rule_id)?;
            Ok(CommandResult::FirewallChanged)
        }
        PrivilegedCommand::RestoreRegistryBackup { backup_id } => {
            let rollback_id = create_registry_backup()?;
            restore_registry_backup(&backup_id).map_err(|error| {
                AppError::Adapter(format!(
                    "restore failed: {error}; pre-restore backup: {rollback_id}"
                ))
            })?;
            Ok(CommandResult::BackupRestored {
                backup_id: rollback_id,
            })
        }
    }
}

fn apply_verified_batch(
    changes: &[RuleChange],
    firewall_changes: &[FirewallChange],
) -> Result<CommandResult, AppError> {
    let before = RegistryAdapter::new().read_report();
    if !before.read_errors.is_empty() {
        return Err(AppError::Adapter(
            "registry could not be read completely before mutation".to_owned(),
        ));
    }
    validate_registry_preconditions(changes, &before)?;
    let expected = expected_registry_state(&before.rules, changes);
    let backup_id = create_registry_backup()?;
    let mut firewall_snapshots = BTreeMap::new();
    for change in firewall_changes {
        let rule_id = match change {
            FirewallChange::Ensure { rule_id, .. } | FirewallChange::Remove { rule_id } => rule_id,
        };
        if !firewall_snapshots.contains_key(rule_id) {
            firewall_snapshots.insert(rule_id.clone(), firewall::snapshot_rule(rule_id)?);
        }
    }
    let operation_deadline = Instant::now() + Duration::from_secs(90);

    let operation = (|| {
        if Instant::now() >= operation_deadline {
            return Err(AppError::Adapter("privileged batch timed out".to_owned()));
        }
        RegistryAdapter::new().apply_changes(changes)?;
        if !changes.is_empty() {
            IpHelperService::new().reload()?;
        }
        let effective = RegistryAdapter::new().read_report();
        if !effective.read_errors.is_empty() || effective.rules != expected {
            return Err(AppError::Adapter(
                "registry post-mutation verification failed".to_owned(),
            ));
        }
        for change in firewall_changes {
            if Instant::now() >= operation_deadline {
                return Err(AppError::Adapter("privileged batch timed out".to_owned()));
            }
            match change {
                FirewallChange::Ensure {
                    rule_id,
                    display_name,
                    local_port,
                    policy,
                } => {
                    let authorized = effective.rules.iter().any(|rule| {
                        firewall_rule_id(rule.key()) == *rule_id
                            && rule.listen.port.get() == *local_port
                    });
                    if !authorized {
                        return Err(AppError::Adapter(
                            "firewall request does not correspond to an effective port proxy rule"
                                .to_owned(),
                        ));
                    }
                    firewall::ensure_rule(rule_id, display_name, *local_port, *policy)?;
                    if !firewall::rule_matches(rule_id, *local_port, *policy)? {
                        return Err(AppError::Adapter(
                            "firewall post-mutation verification failed".to_owned(),
                        ));
                    }
                }
                FirewallChange::Remove { rule_id } => {
                    firewall::remove_rule(rule_id)?;
                    if firewall::rule_exists(rule_id)? {
                        return Err(AppError::Adapter(
                            "firewall removal verification failed".to_owned(),
                        ));
                    }
                }
            }
        }
        Ok(())
    })();

    if let Err(error) = operation {
        let firewall_rollback_ok =
            firewall_snapshots
                .iter()
                .fold(true, |all_restored, (rule_id, snapshot)| {
                    let restored = firewall::restore_snapshot(snapshot.as_ref(), rule_id).is_ok();
                    all_restored && restored
                });
        let registry_rollback = restore_registry_backup(&backup_id);
        return Err(AppError::Adapter(format!(
            "{error}; rollback registry={}, firewall={}; backup={backup_id}",
            if registry_rollback.is_ok() {
                "complete"
            } else {
                "failed"
            },
            if firewall_rollback_ok {
                "complete"
            } else {
                "partial"
            }
        )));
    }

    Ok(CommandResult::Applied {
        change_count: changes.len(),
        backup_id: Some(backup_id),
    })
}

fn validate_registry_preconditions(
    changes: &[RuleChange],
    before: &RegistryReadReport,
) -> Result<(), AppError> {
    let actual: BTreeMap<RuleKey, &ProxyRule> =
        before.rules.iter().map(|rule| (rule.key(), rule)).collect();
    for change in changes {
        let affected = match change {
            RuleChange::Add { rule } | RuleChange::Delete { rule } => rule,
            RuleChange::Update { before, after } => {
                if before.key() != after.key() {
                    return Err(AppError::Adapter(
                        "registry updates cannot change a rule key".to_owned(),
                    ));
                }
                before
            }
        };
        if before
            .opaque_values
            .contains(&(affected.kind, affected.registry_name()))
        {
            return Err(AppError::Adapter(
                "refusing to overwrite an unrecognized registry value".to_owned(),
            ));
        }
        match change {
            RuleChange::Add { rule } if actual.contains_key(&rule.key()) => {
                return Err(AppError::Adapter("rule was concurrently added".to_owned()));
            }
            RuleChange::Delete { rule } if actual.get(&rule.key()).copied() != Some(rule) => {
                return Err(AppError::Adapter("rule changed before deletion".to_owned()));
            }
            RuleChange::Update { before, .. }
                if actual.get(&before.key()).copied() != Some(before) =>
            {
                return Err(AppError::Adapter("rule changed before update".to_owned()));
            }
            _ => {}
        }
    }
    Ok(())
}

fn expected_registry_state(before: &[ProxyRule], changes: &[RuleChange]) -> Vec<ProxyRule> {
    let mut expected: BTreeMap<RuleKey, ProxyRule> = before
        .iter()
        .cloned()
        .map(|rule| (rule.key(), rule))
        .collect();
    for change in changes {
        match change {
            RuleChange::Add { rule } => {
                expected.insert(rule.key(), rule.clone());
            }
            RuleChange::Delete { rule } => {
                expected.remove(&rule.key());
            }
            RuleChange::Update { before, after } => {
                expected.remove(&before.key());
                expected.insert(after.key(), after.clone());
            }
        }
    }
    expected.into_values().collect()
}

fn restore_registry_backup(backup_id: &str) -> Result<(), AppError> {
    let backup = BackupDocument::load(&backup_path(backup_id))
        .map_err(|error| AppError::Adapter(error.to_string()))?;
    let actual = RegistryAdapter::new().read_report();
    if !actual.read_errors.is_empty() {
        return Err(AppError::Adapter(
            "cannot restore because the registry could not be read completely".to_owned(),
        ));
    }
    if backup.rules.iter().any(|managed| {
        actual
            .opaque_values
            .contains(&(managed.rule.kind, managed.rule.registry_name()))
    }) {
        return Err(AppError::Adapter(
            "restore would overwrite an unrecognized registry value".to_owned(),
        ));
    }
    let changes = reconcile_rules(&backup.rules, &actual.rules, ReconcileMode::Replace)?;
    RegistryAdapter::new().apply_changes(&changes)?;
    if !changes.is_empty() {
        IpHelperService::new().reload()?;
    }
    let after = RegistryAdapter::new().read_report();
    let mut expected: Vec<_> = backup
        .rules
        .iter()
        .filter(|managed| managed.enabled)
        .map(|managed| managed.rule.clone())
        .collect();
    expected.sort_by_key(ProxyRule::key);
    if !after.read_errors.is_empty() || after.rules != expected {
        return Err(AppError::Adapter(
            "registry restore verification failed".to_owned(),
        ));
    }
    Ok(())
}

fn create_registry_backup() -> Result<String, AppError> {
    let report = RegistryAdapter::new().read_report();
    if !report.read_errors.is_empty() {
        return Err(AppError::Adapter(
            "refusing to mutate because the registry could not be read completely".to_owned(),
        ));
    }
    let duration = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map_err(|error| AppError::Adapter(error.to_string()))?;
    let backup_id = format!(
        "registry_{}_{}_{}",
        duration.as_secs(),
        duration.subsec_nanos(),
        std::process::id()
    );
    let path = backup_path(&backup_id);
    if let Some(parent) = path.parent() {
        std::fs::create_dir_all(parent).map_err(|error| AppError::Adapter(error.to_string()))?;
    }
    let rules = report.rules.into_iter().map(ManagedRule::new).collect();
    BackupDocument::new(rules, Vec::new())
        .save_new(&path)
        .map_err(|error| AppError::Adapter(error.to_string()))?;
    Ok(backup_id)
}

fn backup_path(backup_id: &str) -> PathBuf {
    let root = std::env::var_os("ProgramData")
        .map_or_else(|| PathBuf::from(r"C:\ProgramData"), PathBuf::from);
    root.join("Young Security")
        .join("ys-netsh-portproxy")
        .join("backups")
        .join(format!("{backup_id}.json"))
}

pub fn is_elevated() -> Result<bool, AppError> {
    let mut token = HANDLE::default();
    unsafe { OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, &raw mut token) }
        .map_err(|error| AppError::Adapter(error.to_string()))?;
    let token = OwnedHandle(token);
    let mut elevation = TOKEN_ELEVATION::default();
    let mut returned = 0_u32;
    unsafe {
        GetTokenInformation(
            token.0,
            TokenElevation,
            Some(std::ptr::from_mut(&mut elevation).cast()),
            u32::try_from(std::mem::size_of::<TOKEN_ELEVATION>())
                .expect("TOKEN_ELEVATION size fits u32"),
            &raw mut returned,
        )
    }
    .map_err(|error| AppError::Adapter(error.to_string()))?;
    Ok(elevation.TokenIsElevated != 0)
}

struct OwnedHandle(HANDLE);

impl Drop for OwnedHandle {
    fn drop(&mut self) {
        let _ = unsafe { CloseHandle(self.0) };
    }
}

fn helper_error(error: &AppError) -> HelperFailure {
    HelperFailure {
        code: HelperFailureCode::WindowsError,
        message: error.to_string(),
    }
}

#[cfg(test)]
mod tests {
    use std::net::{IpAddr, Ipv4Addr};

    use crate::domain::{Endpoint, Port, ProxyKind};

    use super::*;

    fn rule(connect_port: u16) -> ProxyRule {
        ProxyRule::new(
            ProxyKind::V4ToV4,
            Endpoint::new(IpAddr::V4(Ipv4Addr::UNSPECIFIED), Port::new(2222).unwrap()),
            Endpoint::new(
                IpAddr::V4(Ipv4Addr::LOCALHOST),
                Port::new(connect_port).unwrap(),
            ),
        )
        .unwrap()
    }

    #[test]
    fn stale_update_is_rejected_before_registry_mutation() {
        let report = RegistryReadReport {
            rules: vec![rule(22)],
            ..RegistryReadReport::default()
        };
        let changes = vec![RuleChange::Update {
            before: rule(23),
            after: rule(24),
        }];
        assert!(validate_registry_preconditions(&changes, &report).is_err());
    }

    #[test]
    fn opaque_value_collision_is_rejected_but_unrelated_values_are_preserved() {
        let mut report = RegistryReadReport::default();
        report
            .opaque_values
            .insert((ProxyKind::V4ToV4, rule(22).registry_name()));
        let changes = vec![RuleChange::Add { rule: rule(22) }];
        assert!(validate_registry_preconditions(&changes, &report).is_err());

        report.opaque_values.clear();
        report
            .opaque_values
            .insert((ProxyKind::V4ToV4, "127.0.0.1/9999".to_owned()));
        assert!(validate_registry_preconditions(&changes, &report).is_ok());
    }
}
