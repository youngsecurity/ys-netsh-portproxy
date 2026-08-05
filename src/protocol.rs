use std::io::{Read, Write};

use serde::{Deserialize, Serialize};
use thiserror::Error;

use crate::domain::{FirewallPolicy, RuleChange};

pub const PROTOCOL_VERSION: u16 = 1;
pub const MAX_FRAME_BYTES: usize = 64 * 1024;

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct RequestEnvelope {
    pub version: u16,
    pub request_id: u64,
    pub session_nonce: String,
    pub command: PrivilegedCommand,
}

impl RequestEnvelope {
    #[must_use]
    pub fn new(request_id: u64, session_nonce: String, command: PrivilegedCommand) -> Self {
        Self {
            version: PROTOCOL_VERSION,
            request_id,
            session_nonce,
            command,
        }
    }

    pub fn validate(&self) -> Result<(), ProtocolError> {
        if self.version != PROTOCOL_VERSION {
            return Err(ProtocolError::UnsupportedVersion(self.version));
        }
        if self.session_nonce.len() != 64
            || !self
                .session_nonce
                .bytes()
                .all(|byte| byte.is_ascii_hexdigit())
        {
            return Err(ProtocolError::InvalidNonce);
        }
        Ok(())
    }
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct ResponseEnvelope {
    pub version: u16,
    pub request_id: u64,
    pub result: Result<CommandResult, HelperFailure>,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(tag = "command", rename_all = "snake_case")]
pub enum PrivilegedCommand {
    Probe,
    ApplyRules {
        changes: Vec<RuleChange>,
        firewall_changes: Vec<FirewallChange>,
    },
    StartIpHelper,
    ReloadIpHelper,
    RemoveFirewallRule {
        rule_id: String,
    },
    RestoreRegistryBackup {
        backup_id: String,
    },
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(tag = "action", rename_all = "snake_case")]
pub enum FirewallChange {
    Ensure {
        rule_id: String,
        display_name: String,
        local_port: u16,
        policy: FirewallPolicy,
    },
    Remove {
        rule_id: String,
    },
}

impl PrivilegedCommand {
    pub fn validate(&self) -> Result<(), ProtocolError> {
        match self {
            Self::ApplyRules {
                changes,
                firewall_changes,
                ..
            } if changes.len() + firewall_changes.len() > 2_048 => Err(
                ProtocolError::TooManyOperations(changes.len() + firewall_changes.len()),
            ),
            Self::ApplyRules {
                changes,
                firewall_changes,
                ..
            } => {
                for change in changes {
                    validate_rule_change(change)?;
                }
                for change in firewall_changes {
                    validate_firewall_change(change)?;
                }
                Ok(())
            }
            Self::RemoveFirewallRule { rule_id } => validate_identifier(rule_id),
            Self::RestoreRegistryBackup { backup_id } => validate_identifier(backup_id),
            _ => Ok(()),
        }
    }
}

fn validate_rule_change(change: &RuleChange) -> Result<(), ProtocolError> {
    if let RuleChange::Update { before, after } = change {
        if before.key() != after.key() {
            return Err(ProtocolError::InvalidProxyRule);
        }
    }
    let rules: &[&crate::domain::ProxyRule] = match change {
        RuleChange::Add { rule } | RuleChange::Delete { rule } => &[rule],
        RuleChange::Update { before, after } => &[before, after],
    };
    for rule in rules {
        crate::domain::ProxyRule::new(rule.kind, rule.listen, rule.connect)
            .map_err(|_| ProtocolError::InvalidProxyRule)?;
    }
    Ok(())
}

fn validate_firewall_change(change: &FirewallChange) -> Result<(), ProtocolError> {
    match change {
        FirewallChange::Ensure {
            rule_id,
            display_name,
            local_port,
            policy,
        } => {
            validate_identifier(rule_id)?;
            if display_name.trim().is_empty() || display_name.len() > 256 {
                return Err(ProtocolError::InvalidDisplayName);
            }
            if *local_port == 0 || *policy == FirewallPolicy::None {
                return Err(ProtocolError::InvalidFirewallRule);
            }
            Ok(())
        }
        FirewallChange::Remove { rule_id } => validate_identifier(rule_id),
    }
}

fn validate_identifier(value: &str) -> Result<(), ProtocolError> {
    if value.is_empty()
        || value.len() > 128
        || !value
            .bytes()
            .all(|byte| byte.is_ascii_alphanumeric() || matches!(byte, b'-' | b'_'))
    {
        return Err(ProtocolError::InvalidIdentifier);
    }
    Ok(())
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(tag = "result", rename_all = "snake_case")]
pub enum CommandResult {
    Probe {
        helper_version: String,
        elevated: bool,
    },
    Applied {
        change_count: usize,
        backup_id: Option<String>,
    },
    ServiceChanged,
    FirewallChanged,
    BackupRestored {
        backup_id: String,
    },
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct HelperFailure {
    pub code: HelperFailureCode,
    pub message: String,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum HelperFailureCode {
    InvalidRequest,
    AccessDenied,
    Conflict,
    WindowsError,
    Timeout,
    Internal,
}

pub fn write_frame<W, T>(writer: &mut W, value: &T) -> Result<(), ProtocolError>
where
    W: Write,
    T: Serialize,
{
    let payload = serde_json::to_vec(value)?;
    if payload.len() > MAX_FRAME_BYTES {
        return Err(ProtocolError::FrameTooLarge(payload.len()));
    }
    let length =
        u32::try_from(payload.len()).map_err(|_| ProtocolError::FrameTooLarge(payload.len()))?;
    writer.write_all(&length.to_le_bytes())?;
    writer.write_all(&payload)?;
    writer.flush()?;
    Ok(())
}

pub fn read_frame<R, T>(reader: &mut R) -> Result<T, ProtocolError>
where
    R: Read,
    T: for<'de> Deserialize<'de>,
{
    let mut length = [0_u8; 4];
    reader.read_exact(&mut length)?;
    let length = u32::from_le_bytes(length) as usize;
    if length > MAX_FRAME_BYTES {
        return Err(ProtocolError::FrameTooLarge(length));
    }
    let mut payload = vec![0_u8; length];
    reader.read_exact(&mut payload)?;
    Ok(serde_json::from_slice(&payload)?)
}

#[derive(Debug, Error)]
pub enum ProtocolError {
    #[error("unsupported protocol version: {0}")]
    UnsupportedVersion(u16),
    #[error("session nonce must be 32 bytes encoded as hexadecimal")]
    InvalidNonce,
    #[error("frame contains {0} bytes and exceeds the limit")]
    FrameTooLarge(usize),
    #[error("request contains too many operations: {0}")]
    TooManyOperations(usize),
    #[error("identifier contains unsupported characters or has an invalid length")]
    InvalidIdentifier,
    #[error("firewall display name is invalid")]
    InvalidDisplayName,
    #[error("port proxy rule is invalid")]
    InvalidProxyRule,
    #[error("firewall rule is invalid")]
    InvalidFirewallRule,
    #[error("I/O error: {0}")]
    Io(#[from] std::io::Error),
    #[error("JSON error: {0}")]
    Json(#[from] serde_json::Error),
}

#[cfg(test)]
mod tests {
    use std::net::{IpAddr, Ipv4Addr};

    use crate::domain::{Endpoint, Port, ProxyKind, ProxyRule};

    use super::*;

    const NONCE: &str = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    #[test]
    fn request_round_trips_through_bounded_frame() {
        let expected = RequestEnvelope::new(42, NONCE.to_owned(), PrivilegedCommand::Probe);
        let mut bytes = Vec::new();
        write_frame(&mut bytes, &expected).unwrap();
        let actual: RequestEnvelope = read_frame(&mut bytes.as_slice()).unwrap();
        assert_eq!(actual, expected);
        assert!(actual.validate().is_ok());
    }

    #[test]
    fn oversized_length_is_rejected_before_payload_allocation() {
        let length = u32::try_from(MAX_FRAME_BYTES + 1).unwrap().to_le_bytes();
        let mut frame = length.as_slice();
        let result = read_frame::<_, RequestEnvelope>(&mut frame);
        assert!(matches!(result, Err(ProtocolError::FrameTooLarge(_))));
    }

    #[test]
    fn protocol_has_no_arbitrary_command_variant() {
        let json = serde_json::to_string(&PrivilegedCommand::Probe).unwrap();
        assert_eq!(json, r#"{"command":"probe"}"#);
        assert!(!json.contains("shell"));
        assert!(!json.contains("executable"));
    }

    #[test]
    fn apply_revalidates_deserialized_domain_invariants() {
        let mut invalid = ProxyRule::new(
            ProxyKind::V4ToV4,
            Endpoint::new(IpAddr::V4(Ipv4Addr::UNSPECIFIED), Port::new(2222).unwrap()),
            Endpoint::new(IpAddr::V4(Ipv4Addr::LOCALHOST), Port::new(22).unwrap()),
        )
        .unwrap();
        invalid.kind = ProxyKind::V6ToV4;
        let command = PrivilegedCommand::ApplyRules {
            changes: vec![RuleChange::Add { rule: invalid }],
            firewall_changes: Vec::new(),
        };
        assert!(matches!(
            command.validate(),
            Err(ProtocolError::InvalidProxyRule)
        ));
    }

    #[test]
    fn identifiers_reject_paths_and_shell_metacharacters() {
        for invalid in ["../rule", "rule name", "rule;remove", "C:\\Windows"] {
            assert!(matches!(
                validate_identifier(invalid),
                Err(ProtocolError::InvalidIdentifier)
            ));
        }
    }
}
