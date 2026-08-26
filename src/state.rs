use std::{
    collections::{BTreeMap, BTreeSet},
    fs, io,
    path::Path,
};

use serde::{Deserialize, Serialize};
use thiserror::Error;

use crate::domain::{ManagedRule, ProxyRule, RuleKey};

pub const STATE_SCHEMA_VERSION: u16 = 1;

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct UserState {
    pub schema_version: u16,
    #[serde(default)]
    pub rules: Vec<ManagedRule>,
    #[serde(default = "default_sort_column")]
    pub sort_column: String,
    #[serde(default = "default_sort_ascending")]
    pub sort_ascending: bool,
    #[serde(default)]
    pub draft_dirty: bool,
    #[serde(default)]
    pub last_backup_id: String,
    #[serde(default)]
    pub baseline_rules: Vec<ProxyRule>,
}

impl Default for UserState {
    fn default() -> Self {
        Self {
            schema_version: STATE_SCHEMA_VERSION,
            rules: Vec::new(),
            sort_column: default_sort_column(),
            sort_ascending: default_sort_ascending(),
            draft_dirty: false,
            last_backup_id: String::new(),
            baseline_rules: Vec::new(),
        }
    }
}

fn default_sort_column() -> String {
    "Listen".to_owned()
}

const fn default_sort_ascending() -> bool {
    true
}

impl UserState {
    pub fn load(path: &Path) -> Result<Self, StateError> {
        if !path.exists() {
            return Ok(Self::default());
        }
        let state: Self = serde_json::from_slice(&fs::read(path)?)?;
        if state.schema_version != STATE_SCHEMA_VERSION {
            return Err(StateError::UnsupportedSchema(state.schema_version));
        }
        Ok(state)
    }

    pub fn save(&self, path: &Path) -> Result<(), StateError> {
        if let Some(parent) = path.parent() {
            fs::create_dir_all(parent)?;
        }
        let mut bytes = serde_json::to_vec_pretty(self)?;
        bytes.push(b'\n');
        fs::write(path, bytes)?;
        Ok(())
    }

    #[must_use]
    pub fn merge_effective_against(
        &self,
        baseline: &[ProxyRule],
        effective: &[ProxyRule],
        preserve_drafts: bool,
    ) -> Vec<ManagedRule> {
        if !preserve_drafts {
            return self.merge_effective(effective);
        }
        let baseline: BTreeMap<RuleKey, &ProxyRule> =
            baseline.iter().map(|rule| (rule.key(), rule)).collect();
        let desired: BTreeMap<RuleKey, &ManagedRule> = self
            .rules
            .iter()
            .map(|managed| (managed.rule.key(), managed))
            .collect();
        let mut all_keys: BTreeSet<RuleKey> = baseline.keys().copied().collect();
        all_keys.extend(desired.keys().copied());
        let touched: BTreeSet<RuleKey> = all_keys
            .into_iter()
            .filter(|key| match (baseline.get(key), desired.get(key)) {
                (Some(before), Some(after)) => !after.enabled || after.rule != **before,
                (None, Some(_)) | (Some(_), None) => true,
                (None, None) => false,
            })
            .collect();

        let mut saved: BTreeMap<RuleKey, ManagedRule> = self
            .rules
            .iter()
            .cloned()
            .map(|managed| (managed.rule.key(), managed))
            .collect();
        let mut merged = Vec::new();
        for rule in effective {
            if touched.contains(&rule.key()) {
                if let Some(managed) = saved.remove(&rule.key()) {
                    merged.push(managed);
                }
            } else if let Some(mut managed) = saved.remove(&rule.key()) {
                managed.rule = rule.clone();
                managed.enabled = true;
                merged.push(managed);
            } else {
                merged.push(ManagedRule::new(rule.clone()));
            }
        }
        for (key, mut managed) in saved {
            if !touched.contains(&key) {
                managed.enabled = false;
            }
            merged.push(managed);
        }
        merged.sort_by_key(|managed| managed.rule.key());
        merged
    }

    #[must_use]
    pub fn merge_effective(&self, effective: &[ProxyRule]) -> Vec<ManagedRule> {
        let mut saved: BTreeMap<RuleKey, ManagedRule> = self
            .rules
            .iter()
            .cloned()
            .map(|rule| (rule.rule.key(), rule))
            .collect();
        let mut merged = Vec::new();
        for rule in effective {
            if let Some(mut managed) = saved.remove(&rule.key()) {
                managed.rule = rule.clone();
                managed.enabled = true;
                merged.push(managed);
            } else {
                merged.push(ManagedRule::new(rule.clone()));
            }
        }
        merged.extend(saved.into_values().map(|mut managed| {
            managed.enabled = false;
            managed
        }));
        merged.sort_by_key(|managed| managed.rule.key());
        merged
    }
}

#[derive(Debug, Error)]
pub enum StateError {
    #[error("unsupported state schema version: {0}")]
    UnsupportedSchema(u16),
    #[error("I/O error: {0}")]
    Io(#[from] io::Error),
    #[error("JSON error: {0}")]
    Json(#[from] serde_json::Error),
}

#[cfg(test)]
mod tests {
    use std::net::{IpAddr, Ipv4Addr};

    use crate::domain::{Endpoint, Port, ProxyKind};

    use super::*;

    fn rule(port: u16) -> ProxyRule {
        ProxyRule::new(
            ProxyKind::V4ToV4,
            Endpoint::new(IpAddr::V4(Ipv4Addr::UNSPECIFIED), Port::new(port).unwrap()),
            Endpoint::new("127.0.0.1".parse().unwrap(), Port::new(80).unwrap()),
        )
        .unwrap()
    }

    #[test]
    fn dirty_three_way_refresh_preserves_draft_and_adopts_unrelated_external_rule() {
        let baseline = vec![rule(8080)];
        let mut edited = ManagedRule::new(rule(8080));
        edited.rule.connect.port = Port::new(81).unwrap();
        let state = UserState {
            rules: vec![edited.clone()],
            draft_dirty: true,
            baseline_rules: baseline.clone(),
            ..UserState::default()
        };
        let merged = state.merge_effective_against(&baseline, &[rule(8080), rule(9090)], true);
        assert_eq!(merged.len(), 2);
        assert_eq!(
            merged
                .iter()
                .find(|item| item.rule.listen.port.get() == 8080)
                .unwrap()
                .rule
                .connect
                .port
                .get(),
            81
        );
        assert!(merged
            .iter()
            .any(|item| item.rule.listen.port.get() == 9090 && item.enabled));
    }

    #[test]
    fn effective_state_refresh_preserves_metadata_and_disabled_rules() {
        let mut existing = ManagedRule::new(rule(8080));
        existing.comment = "Web".to_owned();
        let mut disabled = ManagedRule::new(rule(9090));
        disabled.enabled = false;
        let stale_enabled = ManagedRule::new(rule(7070));
        let state = UserState {
            rules: vec![existing, disabled, stale_enabled],
            ..UserState::default()
        };
        let merged = state.merge_effective(&[rule(8080), rule(3000)]);
        assert_eq!(merged.len(), 4);
        assert_eq!(
            merged
                .iter()
                .find(|item| item.rule.listen.port.get() == 8080)
                .unwrap()
                .comment,
            "Web"
        );
        for port in [7070, 9090] {
            assert!(
                !merged
                    .iter()
                    .find(|item| item.rule.listen.port.get() == port)
                    .unwrap()
                    .enabled
            );
        }
    }
}
