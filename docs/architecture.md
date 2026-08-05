# Rust application architecture

## Trust model

`ys-netsh-portproxy.exe` is the interactive, unelevated eframe/egui process. It reads effective port-proxy state and performs WSL/Docker discovery in the signed-in user's context. It cannot mutate HKLM, IP Helper, or Windows Firewall directly.

`ys-netsh-portproxy-helper.exe` is a one-request elevated process. The UI creates a local-only, nonce-named Windows named pipe and launches the helper from the canonical directory beside the GUI using `ShellExecuteExW` with the `runas` verb. Both processes verify the peer PID. Every request carries a 256-bit nonce, protocol version, and request ID. Frames are length-prefixed and limited to 64 KiB.

The helper accepts only the typed operations in `PrivilegedCommand`. There is no command for arbitrary executables, shell text, registry paths, service names, firewall names, or file paths. Direct non-elevated helper execution cannot perform an operation.

## Deep modules and seams

- `domain` — typed proxy kinds, IP endpoints, non-zero ports, registry serialization, bounded range expansion, duplicate detection, and deterministic reconciliation.
- `app` — the `RuleReader`, `PrivilegedExecutor`, and `IntegrationProbe` seams. `PortProxyManager` plans, executes, re-reads, and verifies mutations.
- `protocol` — bounded, versioned helper messages and the privileged operation allowlist.
- `backup` / `state` — versioned JSON exchange and UI metadata. Effective rules remain Windows state, not JSON state.
- `windows` — registry, Service Control Manager, Windows Firewall COM, WSL/Docker discovery, elevation, and named-pipe adapters.
- `ui` — an immediate-mode presentation layer. Slow and privileged operations run on worker threads.

Tests exercise the public domain, application, protocol, and adapter seams. The GUI intentionally contains little domain behavior.

## Mutation safety

Before registry mutation, the helper reads all parseable effective rules and writes a versioned backup under:

```text
%ProgramData%\Young Security\ys-netsh-portproxy\backups
```

Registry changes use a Windows registry transaction. After commit, the helper sends `SERVICE_CONTROL_PARAMCHANGE` to IP Helper. The UI then re-reads effective state and fails the operation if it differs from the requested state. Unknown or malformed registry values are reported and block mutation rather than being silently deleted.

Firewall rules are application-owned and named from validated rule identifiers. The helper never edits unrelated firewall rules.

## Known platform constraint

Microsoft documents `netsh interface portproxy` but does not expose the registry schema as a supported public programming contract. The registry implementation is therefore a compatibility adapter. Keep Windows integration tests, preserve unknown values, and verify behavior on every supported Windows release.
