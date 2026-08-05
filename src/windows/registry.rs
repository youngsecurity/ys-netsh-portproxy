use std::collections::BTreeSet;

use windows::{core::HRESULT, Win32::Foundation::ERROR_FILE_NOT_FOUND};
use windows_registry::{Transaction, Type, Value, LOCAL_MACHINE};

use crate::{
    app::{AppError, Diagnostic, RuleReader},
    domain::{ProxyKind, ProxyRule, RuleChange},
};

const PORTPROXY_ROOT: &str = r"SYSTEM\CurrentControlSet\Services\PortProxy";
const KINDS: [ProxyKind; 4] = [
    ProxyKind::V4ToV4,
    ProxyKind::V4ToV6,
    ProxyKind::V6ToV4,
    ProxyKind::V6ToV6,
];

#[derive(Debug, Default)]
pub struct RegistryReadReport {
    pub rules: Vec<ProxyRule>,
    pub diagnostics: Vec<Diagnostic>,
    pub read_errors: Vec<Diagnostic>,
    pub opaque_values: BTreeSet<(ProxyKind, String)>,
}

#[derive(Debug, Clone, Copy, Default)]
pub struct RegistryAdapter;

impl RegistryAdapter {
    #[must_use]
    pub const fn new() -> Self {
        Self
    }

    pub fn read_report(&self) -> RegistryReadReport {
        let mut report = RegistryReadReport::default();
        for kind in KINDS {
            let path = key_path(kind);
            let key = match LOCAL_MACHINE.open(&path) {
                Ok(key) => key,
                Err(error) if error.code() == HRESULT::from_win32(ERROR_FILE_NOT_FOUND.0) => {
                    continue;
                }
                Err(error) => {
                    let diagnostic = Diagnostic {
                        source: path,
                        message: error.to_string(),
                    };
                    report.read_errors.push(diagnostic.clone());
                    report.diagnostics.push(diagnostic);
                    continue;
                }
            };
            let values = match key.values() {
                Ok(values) => values,
                Err(error) => {
                    let diagnostic = Diagnostic {
                        source: path,
                        message: error.to_string(),
                    };
                    report.read_errors.push(diagnostic.clone());
                    report.diagnostics.push(diagnostic);
                    continue;
                }
            };
            for (name, value) in values {
                match registry_string(value).and_then(|value| {
                    ProxyRule::from_registry(kind, &name, &value).map_err(|error| error.to_string())
                }) {
                    Ok(rule) => report.rules.push(rule),
                    Err(message) => {
                        report.opaque_values.insert((kind, name.clone()));
                        report.diagnostics.push(Diagnostic {
                            source: format!(r"{path}\{name}"),
                            message,
                        });
                    }
                }
            }
        }
        report.rules.sort_by_key(ProxyRule::key);
        report
    }

    pub fn apply_changes(&self, changes: &[RuleChange]) -> Result<(), AppError> {
        let transaction = Transaction::new().map_err(adapter_error)?;
        for change in changes {
            let rule = match change {
                RuleChange::Add { rule } | RuleChange::Delete { rule } => rule,
                RuleChange::Update { after, .. } => after,
            };
            let key = LOCAL_MACHINE
                .options()
                .read()
                .write()
                .create()
                .transaction(&transaction)
                .open(key_path(rule.kind))
                .map_err(adapter_error)?;
            match change {
                RuleChange::Add { rule } => match key.get_value(rule.registry_name()) {
                    Ok(_) => {
                        return Err(AppError::Adapter(
                            "rule was concurrently added before transaction commit".to_owned(),
                        ));
                    }
                    Err(error) if error.code() == HRESULT::from_win32(ERROR_FILE_NOT_FOUND.0) => {}
                    Err(error) => return Err(adapter_error(error)),
                },
                RuleChange::Delete { rule } | RuleChange::Update { before: rule, .. } => {
                    let current = key
                        .get_value(rule.registry_name())
                        .map_err(adapter_error)
                        .and_then(|value| registry_string(value).map_err(AppError::Adapter))
                        .and_then(|value| {
                            ProxyRule::from_registry(rule.kind, &rule.registry_name(), &value)
                                .map_err(AppError::from)
                        })?;
                    if current != *rule {
                        return Err(AppError::Adapter(
                            "rule changed before transaction commit".to_owned(),
                        ));
                    }
                }
            }
            match change {
                RuleChange::Add { rule } | RuleChange::Update { after: rule, .. } => key
                    .set_string(rule.registry_name(), rule.registry_value())
                    .map_err(adapter_error)?,
                RuleChange::Delete { rule } => {
                    key.remove_value(rule.registry_name())
                        .map_err(adapter_error)?;
                }
            }
        }
        transaction.commit().map_err(adapter_error)
    }
}

impl RuleReader for RegistryAdapter {
    fn load_rules(&self) -> Result<Vec<ProxyRule>, AppError> {
        Ok(self.read_report().rules)
    }
}

fn key_path(kind: ProxyKind) -> String {
    format!(r"{PORTPROXY_ROOT}\{kind}\tcp")
}

fn registry_string(value: Value) -> Result<String, String> {
    if value.ty() != Type::String {
        return Err(format!("unsupported registry value type: {:?}", value.ty()));
    }
    String::try_from(value).map_err(|error| error.to_string())
}

fn adapter_error(error: impl std::fmt::Display) -> AppError {
    AppError::Adapter(error.to_string())
}
