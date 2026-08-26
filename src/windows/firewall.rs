#![allow(unsafe_code)]

use std::collections::BTreeMap;

use windows::{
    core::BSTR,
    Win32::{
        Foundation::VARIANT_TRUE,
        NetworkManagement::WindowsFirewall::{
            INetFwPolicy2, INetFwRule, NetFwPolicy2, NetFwRule, NET_FW_ACTION_ALLOW,
            NET_FW_PROFILE2_ALL, NET_FW_PROFILE2_DOMAIN, NET_FW_PROFILE2_PRIVATE,
            NET_FW_RULE_DIR_IN,
        },
        System::Com::{
            CoCreateInstance, CoInitializeEx, CoUninitialize, CLSCTX_INPROC_SERVER,
            COINIT_APARTMENTTHREADED,
        },
    },
};

use crate::{app::AppError, domain::FirewallPolicy};

const RULE_PREFIX: &str = "Young Security PortProxy - ";
const GROUP_NAME: &str = "Young Security PortProxy";
const TCP_PROTOCOL: i32 = 6;

#[derive(Debug, Clone)]
pub struct FirewallSnapshot {
    pub rule_id: String,
    pub display_name: String,
    pub local_port: u16,
    pub policy: FirewallPolicy,
}

pub fn snapshot_rule(rule_id: &str) -> Result<Option<FirewallSnapshot>, AppError> {
    let _apartment = ComApartment::initialize()?;
    let policy2: INetFwPolicy2 =
        unsafe { CoCreateInstance(&NetFwPolicy2, None, CLSCTX_INPROC_SERVER) }
            .map_err(adapter_error)?;
    let rules = unsafe { policy2.Rules() }.map_err(adapter_error)?;
    let name = BSTR::from(format!("{RULE_PREFIX}{rule_id}"));
    let Ok(rule) = (unsafe { rules.Item(&name) }) else {
        return Ok(None);
    };
    if unsafe { rule.Grouping() }.map_err(adapter_error)? != GROUP_NAME {
        return Err(AppError::Adapter(
            "a foreign firewall rule uses an application-owned name".to_owned(),
        ));
    }
    let local_port = unsafe { rule.LocalPorts() }
        .map_err(adapter_error)?
        .to_string()
        .parse::<u16>()
        .map_err(adapter_error)?;
    let profiles = unsafe { rule.Profiles() }.map_err(adapter_error)?;
    let policy = if profiles == NET_FW_PROFILE2_ALL.0 {
        FirewallPolicy::AllProfiles
    } else if profiles == (NET_FW_PROFILE2_DOMAIN.0 | NET_FW_PROFILE2_PRIVATE.0) {
        FirewallPolicy::DomainAndPrivate
    } else {
        return Err(AppError::Adapter(
            "owned firewall rule has an unsupported profile mask".to_owned(),
        ));
    };
    Ok(Some(FirewallSnapshot {
        rule_id: rule_id.to_owned(),
        display_name: unsafe { rule.Description() }
            .map_err(adapter_error)?
            .to_string(),
        local_port,
        policy,
    }))
}

pub fn restore_snapshot(
    snapshot: Option<&FirewallSnapshot>,
    rule_id: &str,
) -> Result<(), AppError> {
    if let Some(snapshot) = snapshot {
        ensure_rule(
            &snapshot.rule_id,
            &snapshot.display_name,
            snapshot.local_port,
            snapshot.policy,
        )
    } else {
        remove_rule(rule_id)
    }
}

pub fn ensure_rule(
    rule_id: &str,
    display_name: &str,
    local_port: u16,
    policy: FirewallPolicy,
) -> Result<(), AppError> {
    let _apartment = ComApartment::initialize()?;
    let policy2: INetFwPolicy2 =
        unsafe { CoCreateInstance(&NetFwPolicy2, None, CLSCTX_INPROC_SERVER) }
            .map_err(adapter_error)?;
    let rules = unsafe { policy2.Rules() }.map_err(adapter_error)?;
    let name = BSTR::from(format!("{RULE_PREFIX}{rule_id}"));
    if let Ok(existing) = unsafe { rules.Item(&name) } {
        if unsafe { existing.Grouping() }.map_err(adapter_error)? != GROUP_NAME {
            return Err(AppError::Adapter(
                "refusing to replace a foreign firewall rule with a colliding name".to_owned(),
            ));
        }
        unsafe { rules.Remove(&name) }.map_err(adapter_error)?;
    }

    let rule: INetFwRule = unsafe { CoCreateInstance(&NetFwRule, None, CLSCTX_INPROC_SERVER) }
        .map_err(adapter_error)?;
    let description = BSTR::from(display_name);
    let ports = BSTR::from(local_port.to_string());
    let all_addresses = BSTR::from("*");
    let group = BSTR::from(GROUP_NAME);
    let profiles = policy_profiles(policy)?;

    unsafe {
        (|| -> windows::core::Result<()> {
            rule.SetName(&name)?;
            rule.SetDescription(&description)?;
            rule.SetProtocol(TCP_PROTOCOL)?;
            rule.SetLocalPorts(&ports)?;
            rule.SetLocalAddresses(&all_addresses)?;
            rule.SetRemoteAddresses(&all_addresses)?;
            rule.SetDirection(NET_FW_RULE_DIR_IN)?;
            rule.SetProfiles(profiles)?;
            rule.SetGrouping(&group)?;
            rule.SetAction(NET_FW_ACTION_ALLOW)?;
            rule.SetEnabled(VARIANT_TRUE)?;
            rules.Add(&rule)?;
            Ok(())
        })()
    }
    .map_err(adapter_error)
}

pub fn rule_groups(rule_ids: &[String]) -> Result<BTreeMap<String, String>, AppError> {
    let _apartment = ComApartment::initialize()?;
    let policy2: INetFwPolicy2 =
        unsafe { CoCreateInstance(&NetFwPolicy2, None, CLSCTX_INPROC_SERVER) }
            .map_err(adapter_error)?;
    let rules = unsafe { policy2.Rules() }.map_err(adapter_error)?;
    let mut groups = BTreeMap::new();
    for rule_id in rule_ids {
        let name = BSTR::from(format!("{RULE_PREFIX}{rule_id}"));
        let Ok(rule) = (unsafe { rules.Item(&name) }) else {
            continue;
        };
        let group = unsafe { rule.Grouping() }.map_err(adapter_error)?.to_string();
        if !group.is_empty() {
            groups.insert(rule_id.clone(), group);
        }
    }
    Ok(groups)
}

pub fn rule_exists(rule_id: &str) -> Result<bool, AppError> {
    let _apartment = ComApartment::initialize()?;
    let policy2: INetFwPolicy2 =
        unsafe { CoCreateInstance(&NetFwPolicy2, None, CLSCTX_INPROC_SERVER) }
            .map_err(adapter_error)?;
    let rules = unsafe { policy2.Rules() }.map_err(adapter_error)?;
    let name = BSTR::from(format!("{RULE_PREFIX}{rule_id}"));
    Ok(unsafe { rules.Item(&name) }.is_ok())
}

pub fn rule_matches(
    rule_id: &str,
    local_port: u16,
    policy: FirewallPolicy,
) -> Result<bool, AppError> {
    let _apartment = ComApartment::initialize()?;
    let policy2: INetFwPolicy2 =
        unsafe { CoCreateInstance(&NetFwPolicy2, None, CLSCTX_INPROC_SERVER) }
            .map_err(adapter_error)?;
    let rules = unsafe { policy2.Rules() }.map_err(adapter_error)?;
    let name = BSTR::from(format!("{RULE_PREFIX}{rule_id}"));
    let Ok(rule) = (unsafe { rules.Item(&name) }) else {
        return Ok(false);
    };
    let expected_profiles = policy_profiles(policy)?;
    let expected_port = BSTR::from(local_port.to_string());
    let expected_group = BSTR::from(GROUP_NAME);
    unsafe {
        (|| -> windows::core::Result<bool> {
            Ok(rule.LocalPorts()? == expected_port
                && rule.Profiles()? == expected_profiles
                && rule.Direction()? == NET_FW_RULE_DIR_IN
                && rule.Action()? == NET_FW_ACTION_ALLOW
                && rule.Enabled()? == VARIANT_TRUE
                && rule.Grouping()? == expected_group)
        })()
    }
    .map_err(adapter_error)
}

pub fn remove_rule(rule_id: &str) -> Result<(), AppError> {
    let _apartment = ComApartment::initialize()?;
    let policy2: INetFwPolicy2 =
        unsafe { CoCreateInstance(&NetFwPolicy2, None, CLSCTX_INPROC_SERVER) }
            .map_err(adapter_error)?;
    let rules = unsafe { policy2.Rules() }.map_err(adapter_error)?;
    let name = BSTR::from(format!("{RULE_PREFIX}{rule_id}"));
    if let Ok(existing) = unsafe { rules.Item(&name) } {
        if unsafe { existing.Grouping() }.map_err(adapter_error)? != GROUP_NAME {
            return Err(AppError::Adapter(
                "refusing to remove a foreign firewall rule with a colliding name".to_owned(),
            ));
        }
        unsafe { rules.Remove(&name) }.map_err(adapter_error)?;
    }
    Ok(())
}

fn policy_profiles(policy: FirewallPolicy) -> Result<i32, AppError> {
    match policy {
        FirewallPolicy::None => Err(AppError::Adapter("firewall policy is disabled".to_owned())),
        FirewallPolicy::DomainAndPrivate => {
            Ok(NET_FW_PROFILE2_DOMAIN.0 | NET_FW_PROFILE2_PRIVATE.0)
        }
        FirewallPolicy::AllProfiles => Ok(NET_FW_PROFILE2_ALL.0),
    }
}

struct ComApartment;

impl ComApartment {
    fn initialize() -> Result<Self, AppError> {
        unsafe { CoInitializeEx(None, COINIT_APARTMENTTHREADED) }
            .ok()
            .map_err(adapter_error)?;
        Ok(Self)
    }
}

impl Drop for ComApartment {
    fn drop(&mut self) {
        unsafe { CoUninitialize() };
    }
}

fn adapter_error(error: impl std::fmt::Display) -> AppError {
    AppError::Adapter(error.to_string())
}
