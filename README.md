# ys-netsh-portproxy

A native Windows GUI for managing `netsh interface portproxy` rules.

## Production implementation

The implementation under `src/` uses Rust, eframe/egui, and windows-rs. It provides:

- typed IPv4/IPv6 port-proxy rules and validation;
- list, add, edit, clone, enable/disable, range expansion, and delete workflows;
- comments, groups, application-owned firewall policies, and versioned import/export;
- IP Helper status/start/reload controls;
- WSL address/listener and Docker status discovery;
- registry backups, transactional mutation, state re-read, and verification;
- an unelevated GUI plus a one-request, typed elevated helper over bounded named-pipe IPC.

The GUI never accepts arbitrary privileged commands. Keep these release binaries together:

```text
ys-netsh-portproxy.exe
ys-netsh-portproxy-helper.exe
```

## Development

```bash
cargo fmt --all --check
cargo clippy --all-targets -- -D warnings
cargo test --all-targets
cargo run --bin ys-netsh-portproxy
cargo build --release --bins
```

See [`docs/development.md`](docs/development.md) and [`docs/architecture.md`](docs/architecture.md).

## Repository layout

- `src/` — Rust domain, application, IPC, Windows adapters, helper, and egui GUI.
- `apps/ys-PortProxyGUI-master` — Young Security snapshot of `zmjack/PortProxyGUI`.
- `apps/ys-PortProxyGooey-master` — Young Security snapshot of `jscottelblein/PortProxyGooey`.
- `docs/upstream-sync-and-language-decision.md` — upstream comparison, selective sync record, and stack decision.
