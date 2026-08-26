use std::collections::BTreeMap;

use serde::{Deserialize, Serialize};
use thiserror::Error;

use crate::{
    domain::{
        reconcile_rules, FirewallPolicy, ManagedRule, ProxyRule, ReconcileMode, RuleChange, RuleKey,
    },
    integrations::{DockerStatus, WslStatus},
    protocol::{CommandResult, FirewallChange, HelperFailure, PrivilegedCommand},
};

#[derive(Debug, Clone, Copy, Default, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum ServiceState {
    Running,
    Stopped,
    StartPending,
    StopPending,
    #[default]
    Unknown,
}

#[derive(Debug, Clone, Default, PartialEq, Eq, Serialize, Deserialize)]
pub struct SystemSnapshot {
    pub effective_rules: Vec<ProxyRule>,
    pub ip_helper: ServiceState,
    pub wsl: WslStatus,
    pub docker: DockerStatus,
    pub diagnostics: Vec<Diagnostic>,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct Diagnostic {
    pub source: String,
    pub message: String,
}

pub trait RuleReader {
    fn load_rules(&self) -> Result<Vec<ProxyRule>, AppError>;
}

pub trait PrivilegedExecutor {
    fn execute(&self, command: PrivilegedCommand) -> Result<CommandResult, AppError>;
}

pub trait IntegrationProbe {
    fn service_state(&self) -> Result<ServiceState, AppError>;
    fn managed_firewall_exists(&self, rule_id: &str) -> Result<bool, AppError>;
    fn managed_firewall_groups(
        &self,
        rule_ids: &[String],
    ) -> Result<BTreeMap<String, String>, AppError>;
    fn wsl_status(&self) -> WslStatus;
    fn docker_status(&self) -> DockerStatus;
}

pub struct PortProxyManager<R, E, P> {
    reader: R,
    executor: E,
    probes: P,
}

impl<R, E, P> PortProxyManager<R, E, P>
where
    R: RuleReader,
    E: PrivilegedExecutor,
    P: IntegrationProbe,
{
    #[must_use]
    pub const fn new(reader: R, executor: E, probes: P) -> Self {
        Self {
            reader,
            executor,
            probes,
        }
    }

    pub fn refresh(&self) -> Result<SystemSnapshot, AppError> {
        Ok(SystemSnapshot {
            effective_rules: self.reader.load_rules()?,
            ip_helper: self.probes.service_state()?,
            wsl: self.probes.wsl_status(),
            docker: self.probes.docker_status(),
            diagnostics: Vec::new(),
        })
    }

    pub fn apply_desired(
        &self,
        desired: &[ManagedRule],
        mode: ReconcileMode,
    ) -> Result<ApplyOutcome, AppError> {
        let before = self.reader.load_rules()?;
        let changes = reconcile_rules(desired, &before, mode)?;
        let firewall_changes = plan_firewall_changes(desired, &changes, &self.probes)?;
        if changes.is_empty() && firewall_changes.is_empty() {
            return Ok(ApplyOutcome {
                changes,
                after: before,
                backup_id: None,
            });
        }

        let result = self.executor.execute(PrivilegedCommand::ApplyRules {
            changes: changes.clone(),
            firewall_changes,
        })?;
        let CommandResult::Applied { backup_id, .. } = result else {
            return Err(AppError::UnexpectedHelperResponse);
        };

        let after = self.reader.load_rules()?;
        let remaining = reconcile_rules(desired, &after, mode)?;
        if !remaining.is_empty() {
            return Err(AppError::VerificationFailed(remaining));
        }
        Ok(ApplyOutcome {
            changes,
            after,
            backup_id,
        })
    }
}

fn plan_firewall_changes<P: IntegrationProbe>(
    desired: &[ManagedRule],
    rule_changes: &[RuleChange],
    probes: &P,
) -> Result<Vec<FirewallChange>, AppError> {
    let mut changes = Vec::new();
    for managed in desired {
        let rule_id = firewall_rule_id(managed.rule.key());
        if managed.enabled && managed.firewall != FirewallPolicy::None {
            changes.push(FirewallChange::Ensure {
                rule_id,
                display_name: if managed.comment.trim().is_empty() {
                    format!("Port proxy {}", managed.rule.listen)
                } else {
                    managed.comment.clone()
                },
                local_port: managed.rule.listen.port.get(),
                policy: managed.firewall,
            });
        } else if probes.managed_firewall_exists(&rule_id)? {
            changes.push(FirewallChange::Remove { rule_id });
        }
    }
    for change in rule_changes {
        if let RuleChange::Delete { rule } = change {
            let removal = FirewallChange::Remove {
                rule_id: firewall_rule_id(rule.key()),
            };
            if !changes.contains(&removal) {
                changes.push(removal);
            }
        }
    }
    Ok(changes)
}

#[must_use]
pub fn firewall_rule_id(key: RuleKey) -> String {
    let address = key.listen.address.to_string().replace([':', '.'], "_");
    format!("{}_{}_{}", key.kind, address, key.listen.port)
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ApplyOutcome {
    pub changes: Vec<RuleChange>,
    pub after: Vec<ProxyRule>,
    pub backup_id: Option<String>,
}

#[derive(Debug, Error)]
pub enum AppError {
    #[error(transparent)]
    Domain(#[from] crate::domain::DomainError),
    #[error("adapter error: {0}")]
    Adapter(String),
    #[error("privileged helper rejected the operation: {0:?}")]
    Helper(HelperFailure),
    #[error("privileged helper returned an unexpected result")]
    UnexpectedHelperResponse,
    #[error("effective Windows state did not match the requested state: {0:?}")]
    VerificationFailed(Vec<RuleChange>),
}

#[cfg(test)]
mod tests {
    use std::{
        cell::RefCell,
        net::{IpAddr, Ipv4Addr},
        rc::Rc,
    };

    use crate::domain::{Endpoint, Port, ProxyKind};

    use super::*;

    fn rule(listen: u16, connect: u16) -> ProxyRule {
        ProxyRule::new(
            ProxyKind::V4ToV4,
            Endpoint::new(
                IpAddr::V4(Ipv4Addr::UNSPECIFIED),
                Port::new(listen).unwrap(),
            ),
            Endpoint::new(
                "172.29.108.222".parse().unwrap(),
                Port::new(connect).unwrap(),
            ),
        )
        .unwrap()
    }

    #[derive(Clone)]
    struct MemoryAdapter {
        rules: Rc<RefCell<Vec<ProxyRule>>>,
        commands: Rc<RefCell<Vec<PrivilegedCommand>>>,
        firewall_exists: bool,
    }

    impl RuleReader for MemoryAdapter {
        fn load_rules(&self) -> Result<Vec<ProxyRule>, AppError> {
            Ok(self.rules.borrow().clone())
        }
    }

    impl PrivilegedExecutor for MemoryAdapter {
        fn execute(&self, command: PrivilegedCommand) -> Result<CommandResult, AppError> {
            if let PrivilegedCommand::ApplyRules { changes, .. } = &command {
                for change in changes {
                    match change {
                        RuleChange::Add { rule } => self.rules.borrow_mut().push(rule.clone()),
                        RuleChange::Update { before, after } => {
                            let mut rules = self.rules.borrow_mut();
                            let item = rules.iter_mut().find(|item| *item == before).unwrap();
                            *item = after.clone();
                        }
                        RuleChange::Delete { rule } => {
                            self.rules.borrow_mut().retain(|item| item != rule);
                        }
                    }
                }
            }
            self.commands.borrow_mut().push(command);
            Ok(CommandResult::Applied {
                change_count: 1,
                backup_id: Some("backup_1".to_owned()),
            })
        }
    }

    impl IntegrationProbe for MemoryAdapter {
        fn service_state(&self) -> Result<ServiceState, AppError> {
            Ok(ServiceState::Running)
        }

        fn managed_firewall_exists(&self, _rule_id: &str) -> Result<bool, AppError> {
            Ok(self.firewall_exists)
        }

        fn managed_firewall_groups(
            &self,
            rule_ids: &[String],
        ) -> Result<BTreeMap<String, String>, AppError> {
            Ok(if self.firewall_exists {
                rule_ids
                    .iter()
                    .map(|rule_id| (rule_id.clone(), "Young Security PortProxy".to_owned()))
                    .collect()
            } else {
                BTreeMap::new()
            })
        }

        fn wsl_status(&self) -> WslStatus {
            WslStatus::default()
        }

        fn docker_status(&self) -> DockerStatus {
            DockerStatus::default()
        }
    }

    #[test]
    fn apply_plans_executes_and_verifies_through_public_seams() {
        let adapter = MemoryAdapter {
            rules: Rc::new(RefCell::new(vec![rule(2222, 2222)])),
            commands: Rc::new(RefCell::new(Vec::new())),
            firewall_exists: false,
        };
        let manager = PortProxyManager::new(adapter.clone(), adapter.clone(), adapter.clone());
        let desired = vec![
            ManagedRule::new(rule(2222, 22)),
            ManagedRule::new(rule(8080, 80)),
        ];

        let outcome = manager
            .apply_desired(&desired, ReconcileMode::Merge)
            .unwrap();

        assert_eq!(outcome.changes.len(), 2);
        assert_eq!(outcome.backup_id.as_deref(), Some("backup_1"));
        assert_eq!(adapter.commands.borrow().len(), 1);
        assert_eq!(outcome.after, vec![rule(2222, 22), rule(8080, 80)]);
    }

    #[test]
    fn disabled_firewall_policy_removes_an_existing_owned_rule() {
        let adapter = MemoryAdapter {
            rules: Rc::new(RefCell::new(vec![rule(2222, 22)])),
            commands: Rc::new(RefCell::new(Vec::new())),
            firewall_exists: true,
        };
        let manager = PortProxyManager::new(adapter.clone(), adapter.clone(), adapter.clone());
        manager
            .apply_desired(&[ManagedRule::new(rule(2222, 22))], ReconcileMode::Merge)
            .unwrap();
        let commands = adapter.commands.borrow();
        let PrivilegedCommand::ApplyRules {
            firewall_changes, ..
        } = &commands[0]
        else {
            panic!("expected an apply command");
        };
        assert!(matches!(
            firewall_changes.as_slice(),
            [FirewallChange::Remove { .. }]
        ));
    }

    #[test]
    fn no_changes_skips_elevation() {
        let adapter = MemoryAdapter {
            rules: Rc::new(RefCell::new(vec![rule(2222, 22)])),
            commands: Rc::new(RefCell::new(Vec::new())),
            firewall_exists: false,
        };
        let manager = PortProxyManager::new(adapter.clone(), adapter.clone(), adapter.clone());
        let outcome = manager
            .apply_desired(&[ManagedRule::new(rule(2222, 22))], ReconcileMode::Merge)
            .unwrap();
        assert!(outcome.changes.is_empty());
        assert!(adapter.commands.borrow().is_empty());
    }
}
