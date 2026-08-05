use std::{collections::BTreeSet, fs, io, path::Path};

use serde::{Deserialize, Serialize};
use thiserror::Error;

use crate::domain::{DomainError, ManagedRule, RuleKey};

pub const BACKUP_SCHEMA_VERSION: u16 = 1;

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct BackupDocument {
    pub schema_version: u16,
    pub application_version: String,
    #[serde(default)]
    pub groups: Vec<String>,
    pub rules: Vec<ManagedRule>,
}

impl BackupDocument {
    #[must_use]
    pub fn new(rules: Vec<ManagedRule>, groups: Vec<String>) -> Self {
        Self {
            schema_version: BACKUP_SCHEMA_VERSION,
            application_version: env!("CARGO_PKG_VERSION").to_owned(),
            groups,
            rules,
        }
    }

    pub fn validate(&self) -> Result<(), BackupError> {
        if self.schema_version != BACKUP_SCHEMA_VERSION {
            return Err(BackupError::UnsupportedSchema(self.schema_version));
        }
        let mut keys = BTreeSet::<RuleKey>::new();
        for managed in &self.rules {
            crate::domain::ProxyRule::new(
                managed.rule.kind,
                managed.rule.listen,
                managed.rule.connect,
            )?;
            if managed.group.len() > 128 || managed.comment.len() > 1_024 {
                return Err(BackupError::InvalidGroup);
            }
            if !keys.insert(managed.rule.key()) {
                return Err(BackupError::Domain(DomainError::DuplicateRule(
                    managed.rule.key(),
                )));
            }
        }
        if self.groups.iter().any(|group| group.len() > 128) {
            return Err(BackupError::InvalidGroup);
        }
        Ok(())
    }

    pub fn from_json(bytes: &[u8]) -> Result<Self, BackupError> {
        let document: Self = serde_json::from_slice(bytes)?;
        document.validate()?;
        Ok(document)
    }

    pub fn to_pretty_json(&self) -> Result<Vec<u8>, BackupError> {
        self.validate()?;
        let mut bytes = serde_json::to_vec_pretty(self)?;
        bytes.push(b'\n');
        Ok(bytes)
    }

    pub fn load(path: &Path) -> Result<Self, BackupError> {
        Self::from_json(&fs::read(path)?)
    }

    pub fn save_new(&self, path: &Path) -> Result<(), BackupError> {
        let bytes = self.to_pretty_json()?;
        let mut options = fs::OpenOptions::new();
        options.write(true).create_new(true);
        let mut file = options.open(path)?;
        io::Write::write_all(&mut file, &bytes)?;
        file.sync_all()?;
        Ok(())
    }
}

#[derive(Debug, Error)]
pub enum BackupError {
    #[error("unsupported backup schema version: {0}")]
    UnsupportedSchema(u16),
    #[error("backup contains an invalid group")]
    InvalidGroup,
    #[error(transparent)]
    Domain(#[from] DomainError),
    #[error("I/O error: {0}")]
    Io(#[from] io::Error),
    #[error("JSON error: {0}")]
    Json(#[from] serde_json::Error),
}

#[cfg(test)]
mod tests {
    use std::net::{IpAddr, Ipv4Addr};

    use crate::domain::{Endpoint, Port, ProxyKind, ProxyRule};

    use super::*;

    fn managed_rule() -> ManagedRule {
        let listen = Endpoint::new(IpAddr::V4(Ipv4Addr::UNSPECIFIED), Port::new(2222).unwrap());
        let connect = Endpoint::new("172.29.108.222".parse().unwrap(), Port::new(22).unwrap());
        ManagedRule::new(ProxyRule::new(ProxyKind::V4ToV4, listen, connect).unwrap())
    }

    #[test]
    fn versioned_backup_round_trips_without_losing_metadata() {
        let mut rule = managed_rule();
        rule.group = "WSL".to_owned();
        rule.comment = "SSH".to_owned();
        let expected = BackupDocument::new(vec![rule], vec!["WSL".to_owned()]);
        let actual = BackupDocument::from_json(&expected.to_pretty_json().unwrap()).unwrap();
        assert_eq!(actual, expected);
    }

    #[test]
    fn invalid_deserialized_rule_is_rejected() {
        let mut invalid = managed_rule();
        invalid.rule.kind = ProxyKind::V6ToV4;
        let document = BackupDocument::new(vec![invalid], Vec::new());
        assert!(matches!(document.validate(), Err(BackupError::Domain(_))));
    }

    #[test]
    fn unknown_schema_fails_closed() {
        let mut document = BackupDocument::new(vec![managed_rule()], Vec::new());
        document.schema_version = 999;
        assert!(matches!(
            document.validate(),
            Err(BackupError::UnsupportedSchema(999))
        ));
    }

    #[test]
    fn duplicate_rule_keys_are_rejected() {
        let rule = managed_rule();
        let document = BackupDocument::new(vec![rule.clone(), rule], Vec::new());
        assert!(matches!(
            document.validate(),
            Err(BackupError::Domain(DomainError::DuplicateRule(_)))
        ));
    }
}
