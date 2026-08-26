use std::{
    collections::{BTreeMap, BTreeSet},
    fmt,
    net::{IpAddr, Ipv4Addr, Ipv6Addr},
    str::FromStr,
};

use serde::{de, Deserialize, Deserializer, Serialize};
use thiserror::Error;

pub const MAX_RANGE_RULES: usize = 1_024;

#[derive(Debug, Clone, Copy, PartialEq, Eq, PartialOrd, Ord, Hash, Serialize, Deserialize)]
#[serde(rename_all = "lowercase")]
pub enum ProxyKind {
    V4ToV4,
    V4ToV6,
    V6ToV4,
    V6ToV6,
}

impl ProxyKind {
    #[must_use]
    pub const fn listen_is_ipv4(self) -> bool {
        matches!(self, Self::V4ToV4 | Self::V4ToV6)
    }

    #[must_use]
    pub const fn connect_is_ipv4(self) -> bool {
        matches!(self, Self::V4ToV4 | Self::V6ToV4)
    }
}

impl fmt::Display for ProxyKind {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        formatter.write_str(match self {
            Self::V4ToV4 => "v4tov4",
            Self::V4ToV6 => "v4tov6",
            Self::V6ToV4 => "v6tov4",
            Self::V6ToV6 => "v6tov6",
        })
    }
}

impl FromStr for ProxyKind {
    type Err = DomainError;

    fn from_str(value: &str) -> Result<Self, Self::Err> {
        match value.trim().to_ascii_lowercase().as_str() {
            "v4tov4" => Ok(Self::V4ToV4),
            "v4tov6" => Ok(Self::V4ToV6),
            "v6tov4" => Ok(Self::V6ToV4),
            "v6tov6" => Ok(Self::V6ToV6),
            _ => Err(DomainError::UnsupportedProxyKind(value.to_owned())),
        }
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, PartialOrd, Ord, Hash, Serialize)]
#[serde(transparent)]
pub struct Port(u16);

impl Port {
    pub fn new(value: u16) -> Result<Self, DomainError> {
        if value == 0 {
            Err(DomainError::InvalidPort)
        } else {
            Ok(Self(value))
        }
    }

    #[must_use]
    pub const fn get(self) -> u16 {
        self.0
    }
}

impl fmt::Display for Port {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        self.0.fmt(formatter)
    }
}

impl FromStr for Port {
    type Err = DomainError;

    fn from_str(value: &str) -> Result<Self, Self::Err> {
        let value = value
            .trim()
            .parse::<u16>()
            .map_err(|_| DomainError::InvalidPort)?;
        Self::new(value)
    }
}

impl<'de> Deserialize<'de> for Port {
    fn deserialize<D>(deserializer: D) -> Result<Self, D::Error>
    where
        D: Deserializer<'de>,
    {
        let value = u16::deserialize(deserializer)?;
        Self::new(value).map_err(de::Error::custom)
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, PartialOrd, Ord, Hash, Serialize, Deserialize)]
pub struct Endpoint {
    pub address: IpAddr,
    pub port: Port,
}

impl Endpoint {
    #[must_use]
    pub const fn new(address: IpAddr, port: Port) -> Self {
        Self { address, port }
    }

    pub fn parse(address: &str, port: &str, ipv4: bool) -> Result<Self, DomainError> {
        let address = parse_address(address, ipv4)?;
        let port = port.parse()?;
        Ok(Self::new(address, port))
    }
}

impl fmt::Display for Endpoint {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self.address {
            IpAddr::V4(address) => write!(formatter, "{address}:{}", self.port),
            IpAddr::V6(address) => write!(formatter, "[{address}]:{}", self.port),
        }
    }
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct ProxyRule {
    pub kind: ProxyKind,
    pub listen: Endpoint,
    pub connect: Endpoint,
}

impl ProxyRule {
    pub fn new(kind: ProxyKind, listen: Endpoint, connect: Endpoint) -> Result<Self, DomainError> {
        if listen.address.is_ipv4() != kind.listen_is_ipv4()
            || connect.address.is_ipv4() != kind.connect_is_ipv4()
        {
            return Err(DomainError::AddressFamilyMismatch);
        }
        Ok(Self {
            kind,
            listen,
            connect,
        })
    }

    #[must_use]
    pub const fn key(&self) -> RuleKey {
        RuleKey {
            kind: self.kind,
            listen: self.listen,
        }
    }

    #[must_use]
    pub fn registry_name(&self) -> String {
        format!("{}/{}", self.listen.address, self.listen.port)
    }

    #[must_use]
    pub fn registry_value(&self) -> String {
        format!("{}/{}", self.connect.address, self.connect.port)
    }

    pub fn from_registry(kind: ProxyKind, name: &str, value: &str) -> Result<Self, DomainError> {
        let listen = parse_registry_endpoint(name)?;
        let connect = parse_registry_endpoint(value)?;
        Self::new(kind, listen, connect)
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, PartialOrd, Ord, Hash, Serialize, Deserialize)]
pub struct RuleKey {
    pub kind: ProxyKind,
    pub listen: Endpoint,
}

#[derive(Debug, Clone, Copy, Default, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum FirewallPolicy {
    #[default]
    None,
    DomainAndPrivate,
    AllProfiles,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct ManagedRule {
    pub rule: ProxyRule,
    #[serde(default = "default_true")]
    pub enabled: bool,
    #[serde(default)]
    pub comment: String,
    #[serde(default)]
    pub firewall: FirewallPolicy,
}

impl ManagedRule {
    #[must_use]
    pub fn new(rule: ProxyRule) -> Self {
        Self {
            rule,
            enabled: true,
            comment: String::new(),
            firewall: FirewallPolicy::None,
        }
    }
}

const fn default_true() -> bool {
    true
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(tag = "action", rename_all = "snake_case")]
pub enum RuleChange {
    Add { rule: ProxyRule },
    Update { before: ProxyRule, after: ProxyRule },
    Delete { rule: ProxyRule },
}

impl RuleChange {
    #[must_use]
    pub const fn key(&self) -> RuleKey {
        match self {
            Self::Add { rule } | Self::Delete { rule } => rule.key(),
            Self::Update { after, .. } => after.key(),
        }
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum ReconcileMode {
    Merge,
    Replace,
}

pub fn reconcile_rules(
    desired: &[ManagedRule],
    actual: &[ProxyRule],
    mode: ReconcileMode,
) -> Result<Vec<RuleChange>, DomainError> {
    let mut desired_by_key = BTreeMap::new();
    for managed in desired.iter().filter(|managed| managed.enabled) {
        let key = managed.rule.key();
        if desired_by_key.insert(key, &managed.rule).is_some() {
            return Err(DomainError::DuplicateRule(key));
        }
    }

    let mut actual_by_key = BTreeMap::new();
    for rule in actual {
        let key = rule.key();
        if actual_by_key.insert(key, rule).is_some() {
            return Err(DomainError::DuplicateRule(key));
        }
    }

    let mut changes = Vec::new();
    for (key, desired_rule) in &desired_by_key {
        match actual_by_key.get(key) {
            None => changes.push(RuleChange::Add {
                rule: (*desired_rule).clone(),
            }),
            Some(actual_rule) if *actual_rule != *desired_rule => {
                changes.push(RuleChange::Update {
                    before: (*actual_rule).clone(),
                    after: (*desired_rule).clone(),
                });
            }
            Some(_) => {}
        }
    }

    let disabled_keys: BTreeSet<_> = desired
        .iter()
        .filter(|managed| !managed.enabled)
        .map(|managed| managed.rule.key())
        .collect();

    for (key, actual_rule) in actual_by_key {
        if disabled_keys.contains(&key)
            || (mode == ReconcileMode::Replace && !desired_by_key.contains_key(&key))
        {
            changes.push(RuleChange::Delete {
                rule: actual_rule.clone(),
            });
        }
    }

    changes.sort_by_key(RuleChange::key);
    Ok(changes)
}

pub fn expand_listen_port_range(
    base: &ProxyRule,
    listen_end: Port,
) -> Result<Vec<ProxyRule>, DomainError> {
    let listen_start = base.listen.port.get();
    if listen_end.get() < listen_start {
        return Err(DomainError::ReversedRange);
    }
    let count = usize::from(listen_end.get() - listen_start) + 1;
    if count > MAX_RANGE_RULES {
        return Err(DomainError::RangeTooLarge {
            count,
            max: MAX_RANGE_RULES,
        });
    }
    (0..count)
        .map(|offset| {
            let listen = Endpoint::new(
                base.listen.address,
                Port::new(listen_start + u16::try_from(offset).expect("bounded range offset"))?,
            );
            ProxyRule::new(base.kind, listen, base.connect)
        })
        .collect()
}

pub fn expand_port_range(
    base: &ProxyRule,
    listen_end: Port,
    connect_end: Port,
) -> Result<Vec<ProxyRule>, DomainError> {
    let listen_start = base.listen.port.get();
    let connect_start = base.connect.port.get();
    if listen_end.get() < listen_start || connect_end.get() < connect_start {
        return Err(DomainError::ReversedRange);
    }

    let listen_count = usize::from(listen_end.get() - listen_start) + 1;
    let connect_count = usize::from(connect_end.get() - connect_start) + 1;
    if listen_count != connect_count {
        return Err(DomainError::UnequalRangeLengths);
    }
    if listen_count > MAX_RANGE_RULES {
        return Err(DomainError::RangeTooLarge {
            count: listen_count,
            max: MAX_RANGE_RULES,
        });
    }

    (0..listen_count)
        .map(|offset| {
            let listen = Endpoint::new(
                base.listen.address,
                Port::new(listen_start + u16::try_from(offset).expect("bounded range offset"))?,
            );
            let connect = Endpoint::new(
                base.connect.address,
                Port::new(connect_start + u16::try_from(offset).expect("bounded range offset"))?,
            );
            ProxyRule::new(base.kind, listen, connect)
        })
        .collect()
}

fn parse_address(value: &str, ipv4: bool) -> Result<IpAddr, DomainError> {
    let value = value.trim();
    if value == "*" || value.is_empty() {
        return Ok(if ipv4 {
            IpAddr::V4(Ipv4Addr::UNSPECIFIED)
        } else {
            IpAddr::V6(Ipv6Addr::UNSPECIFIED)
        });
    }
    let address = value
        .parse::<IpAddr>()
        .map_err(|_| DomainError::InvalidAddress(value.to_owned()))?;
    if address.is_ipv4() != ipv4 {
        return Err(DomainError::AddressFamilyMismatch);
    }
    Ok(address)
}

fn parse_registry_endpoint(value: &str) -> Result<Endpoint, DomainError> {
    let (address, port) = value
        .rsplit_once('/')
        .ok_or_else(|| DomainError::MalformedRegistryEndpoint(value.to_owned()))?;
    let address = address
        .trim()
        .parse::<IpAddr>()
        .map_err(|_| DomainError::MalformedRegistryEndpoint(value.to_owned()))?;
    let port = port
        .trim()
        .parse::<Port>()
        .map_err(|_| DomainError::MalformedRegistryEndpoint(value.to_owned()))?;
    Ok(Endpoint::new(address, port))
}

#[derive(Debug, Error, Clone, PartialEq, Eq)]
pub enum DomainError {
    #[error("unsupported proxy kind: {0}")]
    UnsupportedProxyKind(String),
    #[error("port must be in the range 1..=65535")]
    InvalidPort,
    #[error("invalid IP address: {0}")]
    InvalidAddress(String),
    #[error("address family does not match proxy kind")]
    AddressFamilyMismatch,
    #[error("malformed portproxy registry endpoint: {0}")]
    MalformedRegistryEndpoint(String),
    #[error("duplicate rule key: {0:?}")]
    DuplicateRule(RuleKey),
    #[error("port range end must not precede its start")]
    ReversedRange,
    #[error("listen and connect ranges must contain the same number of ports")]
    UnequalRangeLengths,
    #[error("port range contains {count} rules; maximum is {max}")]
    RangeTooLarge { count: usize, max: usize },
}

#[cfg(test)]
mod tests {
    use proptest::prelude::*;

    use super::*;

    fn endpoint(address: &str, port: u16) -> Endpoint {
        Endpoint::new(address.parse().unwrap(), Port::new(port).unwrap())
    }

    fn rule(listen_port: u16, connect_port: u16) -> ProxyRule {
        ProxyRule::new(
            ProxyKind::V4ToV4,
            endpoint("127.0.0.1", listen_port),
            endpoint("172.29.108.222", connect_port),
        )
        .unwrap()
    }

    #[test]
    fn proxy_kinds_round_trip_through_windows_names() {
        let examples = [
            ("v4tov4", ProxyKind::V4ToV4),
            ("v4tov6", ProxyKind::V4ToV6),
            ("v6tov4", ProxyKind::V6ToV4),
            ("v6tov6", ProxyKind::V6ToV6),
        ];
        for (text, kind) in examples {
            assert_eq!(text.parse::<ProxyKind>(), Ok(kind));
            assert_eq!(kind.to_string(), text);
        }
    }

    #[test]
    fn port_rejects_zero_and_accepts_boundaries() {
        assert_eq!(Port::new(0), Err(DomainError::InvalidPort));
        assert_eq!(Port::new(1).map(Port::get), Ok(1));
        assert_eq!(Port::new(u16::MAX).map(Port::get), Ok(u16::MAX));
        assert!(serde_json::from_str::<Port>("0").is_err());
    }

    #[test]
    fn rule_rejects_address_family_mismatch() {
        let result = ProxyRule::new(
            ProxyKind::V4ToV6,
            endpoint("127.0.0.1", 80),
            endpoint("127.0.0.1", 8080),
        );
        assert_eq!(result, Err(DomainError::AddressFamilyMismatch));
    }

    #[test]
    fn registry_endpoints_round_trip_for_ipv4_and_ipv6() {
        let cases = [
            ProxyRule::new(
                ProxyKind::V4ToV4,
                endpoint("0.0.0.0", 2222),
                endpoint("172.29.108.222", 22),
            )
            .unwrap(),
            ProxyRule::new(
                ProxyKind::V6ToV6,
                endpoint("::", 443),
                endpoint("::1", 8443),
            )
            .unwrap(),
        ];
        for expected in cases {
            assert_eq!(
                ProxyRule::from_registry(
                    expected.kind,
                    &expected.registry_name(),
                    &expected.registry_value()
                ),
                Ok(expected)
            );
        }
    }

    #[test]
    fn legacy_listen_range_keeps_connect_port_fixed() {
        let expanded = expand_listen_port_range(&rule(2200, 22), Port::new(2202).unwrap()).unwrap();
        let pairs: Vec<_> = expanded
            .iter()
            .map(|item| (item.listen.port.get(), item.connect.port.get()))
            .collect();
        assert_eq!(pairs, vec![(2200, 22), (2201, 22), (2202, 22)]);
    }

    #[test]
    fn equal_ranges_expand_inclusive_and_in_order() {
        let expanded = expand_port_range(
            &rule(2200, 22),
            Port::new(2202).unwrap(),
            Port::new(24).unwrap(),
        )
        .unwrap();
        let pairs: Vec<_> = expanded
            .iter()
            .map(|item| (item.listen.port.get(), item.connect.port.get()))
            .collect();
        assert_eq!(pairs, vec![(2200, 22), (2201, 23), (2202, 24)]);
    }

    #[test]
    fn range_rejects_unequal_or_excessive_expansion() {
        assert_eq!(
            expand_port_range(
                &rule(2200, 22),
                Port::new(2202).unwrap(),
                Port::new(23).unwrap()
            ),
            Err(DomainError::UnequalRangeLengths)
        );
        assert!(matches!(
            expand_port_range(
                &rule(1, 1),
                Port::new(1 + u16::try_from(MAX_RANGE_RULES).unwrap()).unwrap(),
                Port::new(1 + u16::try_from(MAX_RANGE_RULES).unwrap()).unwrap()
            ),
            Err(DomainError::RangeTooLarge { .. })
        ));
    }

    #[test]
    fn reconciliation_is_deterministic_and_preserves_unlisted_rules_in_merge_mode() {
        let actual = vec![rule(2222, 22), rule(8080, 80)];
        let desired = vec![
            ManagedRule::new(rule(2222, 2222)),
            ManagedRule::new(rule(9000, 90)),
        ];
        let changes = reconcile_rules(&desired, &actual, ReconcileMode::Merge).unwrap();
        assert_eq!(
            changes,
            vec![
                RuleChange::Update {
                    before: rule(2222, 22),
                    after: rule(2222, 2222),
                },
                RuleChange::Add {
                    rule: rule(9000, 90)
                },
            ]
        );
    }

    proptest! {
        #[test]
        fn ipv4_registry_codec_round_trips_all_nonzero_ports(
            listen_port in 1_u16..=u16::MAX,
            connect_port in 1_u16..=u16::MAX,
        ) {
            let expected = rule(listen_port, connect_port);
            let actual = ProxyRule::from_registry(
                expected.kind,
                &expected.registry_name(),
                &expected.registry_value(),
            );
            prop_assert_eq!(actual, Ok(expected));
        }
    }

    #[test]
    fn disabled_rule_is_deleted_but_retains_metadata_in_desired_state() {
        let mut disabled = ManagedRule::new(rule(2222, 22));
        disabled.enabled = false;
        disabled.comment = "WSL SSH".to_owned();
        let changes =
            reconcile_rules(&[disabled], &[rule(2222, 22)], ReconcileMode::Merge).unwrap();
        assert_eq!(
            changes,
            vec![RuleChange::Delete {
                rule: rule(2222, 22)
            }]
        );
    }
}
