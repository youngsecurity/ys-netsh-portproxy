use std::{collections::BTreeSet, net::IpAddr};

use serde::{Deserialize, Serialize};

#[derive(Debug, Clone, Default, PartialEq, Eq, Serialize, Deserialize)]
pub struct WslStatus {
    pub available: bool,
    pub distribution: Option<String>,
    pub addresses: Vec<IpAddr>,
    pub listening_ports: Vec<u16>,
}

#[derive(Debug, Clone, Default, PartialEq, Eq, Serialize, Deserialize)]
pub struct DockerStatus {
    pub available: bool,
    pub running: bool,
    pub context: Option<String>,
    pub containers: Vec<DockerContainer>,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct DockerContainer {
    pub id: String,
    pub name: String,
    pub ports: String,
}

#[must_use]
pub fn parse_wsl_addresses(output: &str) -> Vec<IpAddr> {
    let mut addresses: Vec<_> = output
        .split_whitespace()
        .filter_map(|value| value.parse::<IpAddr>().ok())
        .collect();
    addresses.sort_unstable();
    addresses.dedup();
    addresses
}

#[must_use]
pub fn parse_ss_listening_ports(output: &str) -> Vec<u16> {
    let mut ports = BTreeSet::new();
    for line in output.lines() {
        let fields: Vec<_> = line.split_whitespace().collect();
        let Some(&endpoint) = fields.get(3).or_else(|| fields.last()) else {
            continue;
        };
        let port = endpoint
            .rsplit_once(':')
            .map_or(endpoint, |(_, port)| port)
            .trim_matches(|character| character == '[' || character == ']');
        if let Ok(port) = port.parse::<u16>() {
            if port != 0 {
                ports.insert(port);
            }
        }
    }
    ports.into_iter().collect()
}

#[must_use]
pub fn parse_wsl_distributions(output: &str) -> Vec<String> {
    output
        .lines()
        .map(|line| line.trim().trim_start_matches('*').trim())
        .filter(|line| !line.is_empty())
        .map(ToOwned::to_owned)
        .collect()
}

#[must_use]
pub fn parse_docker_containers(output: &str) -> Vec<DockerContainer> {
    output
        .lines()
        .filter_map(|line| {
            let mut fields = line.splitn(3, '\t');
            let id = fields.next()?.trim();
            let name = fields.next()?.trim();
            let ports = fields.next().unwrap_or_default().trim();
            if id.is_empty() || name.is_empty() {
                return None;
            }
            Some(DockerContainer {
                id: id.to_owned(),
                name: name.to_owned(),
                ports: ports.to_owned(),
            })
        })
        .collect()
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn wsl_address_parser_ignores_noise_and_deduplicates() {
        assert_eq!(
            parse_wsl_addresses("172.29.108.222 172.17.0.1 noise 172.29.108.222\n"),
            vec![
                "172.17.0.1".parse::<IpAddr>().unwrap(),
                "172.29.108.222".parse::<IpAddr>().unwrap(),
            ]
        );
    }

    #[test]
    fn listening_port_parser_handles_ipv4_and_ipv6_ss_output() {
        let output = "LISTEN 0 4096 0.0.0.0:22\nLISTEN 0 4096 [::]:2222\nLISTEN 0 128 *:443";
        assert_eq!(parse_ss_listening_ports(output), vec![22, 443, 2222]);
    }

    #[test]
    fn docker_parser_uses_fixed_tab_delimited_shape() {
        let output = "abc123\tweb\t0.0.0.0:8080->80/tcp\ndef456\tdb\t5432/tcp\n";
        assert_eq!(
            parse_docker_containers(output),
            vec![
                DockerContainer {
                    id: "abc123".to_owned(),
                    name: "web".to_owned(),
                    ports: "0.0.0.0:8080->80/tcp".to_owned(),
                },
                DockerContainer {
                    id: "def456".to_owned(),
                    name: "db".to_owned(),
                    ports: "5432/tcp".to_owned(),
                },
            ]
        );
    }
}
