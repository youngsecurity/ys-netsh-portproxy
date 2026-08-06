# Upstream sync review and rewrite language decision

Date: 2026-08-05

## Scope and provenance

The downloaded trees were verified byte-for-byte against their upstream Git trees using Git blob hashes:

- `apps/ys-PortProxyGUI-master`: `zmjack/PortProxyGUI@8e36f87205bbcfda5861cfe382a34107aa46003b`
- `apps/ys-PortProxyGooey-master`: `jscottelblein/PortProxyGooey@81e44da17e2f6e605a21bcac977ca549fe31179c`

GitHub's comparison reports that Gooey has diverged from GUI: Gooey is **64 commits ahead and 9 commits behind**, with merge base `3c5c88354b9d5bc3a080ba6ae02c8d0707fdc519`. It is not merely a nine-commit-old copy.

Comparison: <https://github.com/zmjack/PortProxyGUI/compare/jscottelblein:master...master>

## Codebase inspection

### PortProxyGUI

- 25 C# files, approximately 1,994 C# lines, plus a 21-line PowerShell publish script.
- Windows Forms application targeting .NET 8, .NET 6, .NET Framework 4.5.1, and .NET Framework 3.5.
- Reads and writes the effective port-proxy rules directly in `HKLM\SYSTEM\CurrentControlSet\Services\PortProxy`.
- Notifies and controls the IP Helper service through Win32 service-control APIs.
- Uses SQLite for comments, groups, migrations, and UI configuration.
- Requests administrator elevation for the entire process through `app.manifest`.
- Does not configure Windows Firewall.

### PortProxyGooey

- 28 C# files and approximately 8,874 C# lines.
- Windows Forms application with the same registry/SQLite core and administrator manifest.
- Adds WSL and Docker status/actions, firewall COM automation, richer rule editing, ranges, cloning, grouping, persistent sorting/position, status indicators, audio, and external-tool launchers.
- Much of the extra platform logic is concentrated in the 2,629-line `Utils/JSE_Utils.cs`; the main form is another 1,914 lines. This makes isolation and unit testing difficult.
- The checked-in `Native/**` files are excluded from compilation; equivalent service declarations live inside `JSE_Utils.Services`.
- The database moved from the user's Documents directory to a machine-wide ProgramData path.

### Important follow-up findings

- `tmrCheck_Tick` calls `lstIPs.GetRange(1, lstIPs.Count - 1)` before handling an empty IP list. A machine with no reported local address can throw on the UI thread every five-second timer cycle before IP Helper, WSL, and Docker statuses are refreshed.
- IP Helper start failures are effectively silent: the click handler discards the asynchronous result, while `Start_BGW` can dereference a null `e.Result` on worker error/cancellation.
- `Rule.Equals(Rule)` is null-unsafe even though `Equals(object)` can pass null. The new hash implementation fixes the upstream-sync defect but equality still needs a separate cleanup.
- The font sync covers the application default and forms. Gooey's custom dialogs still intentionally hard-code Microsoft Sans Serif, and resource files contain Microsoft YaHei settings; a complete typography/accessibility pass remains separate work.
- Microsoft documents `netsh interface portproxy`, but the registry schema used by both applications is not presented as the supported public management contract. Treat registry access as a compatibility adapter, re-read state after mutation, preserve unknown values, and test against supported Windows versions. See <https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/netsh-interface>.

### Language classification

There is no Smalltalk source in either downloaded tree. GitHub's language API reports 589 bytes of Smalltalk for PortProxyGUI, exactly matching the size of `PortProxyGUI/Native/ScmRights.cs`; this is a GitHub Linguist classification artifact, not a Smalltalk component. The only PowerShell source is GUI's `publish.ps1`.

## Review of the nine upstream commits

| Commit | Upstream change | Gooey disposition |
| --- | --- | --- |
| `5bb57e43e9da` | v1.4.0 README update | Documentation only; not applicable to Gooey's product README. |
| `fe775680217f` | v1.4.1 status footer, IP Helper status/start behavior, expanded SCM declarations | Already implemented and extended in Gooey through `JSE_Utils.Services`, a five-second status refresh, status icon, and click-to-start behavior. |
| `8b9199f202d7` | Correct service access masks | Gooey's service implementation already uses equivalent/superset APIs; no direct patch applies because the code moved. |
| `ce199ef05b52` | Screenshot update | GUI-specific documentation asset. |
| `eb0d60442162` | v1.4.1 README notes | Gooey already documents its richer feature set. |
| `ffeffca95297` | v1.4.2: .NET 8 target, Arial default font, non-throwing `Rule.GetHashCode`, publish script, README | .NET 8 and Arial were ported. `GetHashCode` was fixed with a field-based hash rather than upstream's identity hash, which would violate the equality/hash contract. The GUI-specific publish script was not copied. |
| `cf386cefb7b7` | Expanded runtime README table | GUI-specific because Gooey has a different target matrix. |
| `6bee3ea9858b` | README link/text corrections | Documentation only. |
| `8e36f87205bb` | File-scoped namespaces, formatting, collection expressions, `PortPorxyUtil` typo rename, path compatibility helper, and sort-default cleanup | Mostly style churn that conflicts with Gooey's 64-commit divergence. Gooey already uses the correctly spelled `PortProxyUtil`, targets modern .NET only, and already sets a newly selected sort column to ascending. No bulk port is warranted. |

## Selective sync applied to Gooey

The feasible sync is semantic, not a Git merge or blind cherry-pick. The following changes were applied:

- Target `net8.0-windows` instead of end-of-life `net7.0-windows`.
- Use the upstream v1.4.2 default font, `Arial, 8.25pt`.
- Remove form-level `InterfaceUtil.UiFont` overrides so the application default actually takes effect.
- Replace `Rule.GetHashCode()` throwing `NotImplementedException` with a hash over every field used by equality, including Gooey's `FWHash`.

Not ported:

- GUI-only screenshots, README, version number, assembly name, and publish script.
- Bulk namespace/formatting changes with no behavioral value.
- Duplicate IP Helper UI/service code already superseded by Gooey.
- GUI's `Util` rename because Gooey already has `PortProxyUtil`.

## Validation

- Both downloaded snapshots matched their upstream commit trees before edits.
- PortProxyGUI built successfully for `net8.0-windows` with the Windows .NET SDK. It emitted existing warnings about BinaryFormatter-backed WinForms image-list resources and SQLite runtime identifiers.
- Gooey cannot build with `dotnet build` because its existing `COMReference` to `NetFwTypeLib` triggers `MSB4803`; COM resolution requires full Visual Studio/.NET Framework MSBuild.
- Visual Studio Community 2022 MSBuild 17.14 and .NET Framework 4.8.1 are installed on the test machine. Gooey built successfully for `net8.0-windows` with that MSBuild after copying the source to a Windows-local scratch path. Building directly from the WSL `\\wsl.localhost` UNC path instead failed with `MSB3821` because MSBuild treats the `.resx` files as Internet/Restricted-zone resources.
- The successful build emitted existing warnings about BinaryFormatter-backed WinForms image-list resources and SQLite runtime identifiers.
- Gooey's `System.Management` and `System.ServiceProcess.ServiceController` dependencies remain on 7.x packages after the net8 target change. Align them with net8 in a separate dependency update.
- The edited project and manifest parse as valid XML.

## Language decision

### Recommendation

Build the production rewrite in **Rust**, using:

- `windows`/focused `windows-rs` crates for registry, Service Control Manager, process, and firewall APIs;
- `eframe` + `egui` for a native desktop UI without an embedded browser;
- a small domain core with typed `ProxyRule`, `ProxyKind`, address, port, and reconciliation models;
- an OS adapter layer for registry/service/firewall/WSL/Docker operations;
- JSON or SQLite only for UI metadata and saved profiles—the registry remains the source of effective port-proxy state.

Use **PowerShell + WPF** only for a short behavior/UI prototype. PowerShell can absolutely build a Windows GUI through WPF or WinForms, and it offers excellent access to registry, services, firewall cmdlets, WSL, and Docker. It is less attractive for the production artifact because event-heavy GUI scripts are harder to test and refactor, distribution depends on a PowerShell host/policy, and script signing/packaging is less straightforward than signing a native executable.

Go is a credible second choice, especially with `golang.org/x/sys/windows/registry` and Windows service packages. Avoid Wails for this application's current all-process elevation model: Wails uses WebView2, while Microsoft recommends keeping a WebView2 host unelevated and moving privileged work into a separate process. Fyne avoids that issue but provides a less Windows-native UI. If implementation speed outweighs binary hardening and type safety, **Go + Fyne** is the fallback production stack.

### Decision matrix

| Criterion | Rust + egui | Go + Fyne | Go + Wails | PowerShell + WPF |
| --- | --- | --- | --- | --- |
| Direct Windows API access | Excellent, strongly typed | Good | Good backend | Excellent through .NET/cmdlets |
| Elevated-process attack surface | Native only | Native only | Embedded WebView2 is a poor fit | Native WPF hosted by PowerShell |
| Single distributable | Yes | Yes | Yes, plus WebView2 runtime | Not naturally |
| Domain modeling/testability | Excellent | Good | Good backend; split frontend tests | Fair for scripts, degrades with UI size |
| UI development speed | Moderate | Fast | Fastest polished UI | Fast prototype, slower at scale |
| Runtime/footprint | Native; GUI backend adds size | Native; GUI stack adds size | WebView2 dependency | PowerShell/.NET host |
| Long-term maintainability | Best if Rust skills are available | Good | Good, but two technology stacks | Weakest for a feature-rich GUI |

## Recommended architecture for `src/`

1. `domain`: pure rule validation, normalization, equality, duplicate detection, range expansion, and desired/actual reconciliation.
2. `windows`: registry compatibility repository, IP Helper service controller, firewall adapter, UAC/elevation, and atomic backup/restore. Verify effective state with a re-read and, where useful, `netsh interface portproxy show all`.
3. `integrations`: WSL and Docker discovery behind bounded-time subprocess interfaces using fixed executables and discrete arguments—never shell-built command strings.
4. `app`: use cases returning explicit plans/results; no Windows calls from UI handlers.
5. `ui`: table, editor, status, confirmation, and error presentation only.
6. `tests`: pure domain tests on Linux plus Windows integration tests against disposable keys/adapters.

For least privilege, the eventual production design should keep the UI unelevated and invoke a narrow elevated helper only for registry/service/firewall mutations. Use typed requests over an ACL-protected IPC channel, not arbitrary commands. This is mandatory if a WebView-based UI is ever selected and still beneficial for a native Rust UI. It also keeps WSL and Docker probes in the interactive user's context instead of a potentially different administrator account.

## Primary references

- Rust for Windows: <https://learn.microsoft.com/en-us/windows/dev-environment/rust/rust-for-windows>
- `windows-rs`: <https://github.com/microsoft/windows-rs>
- egui/eframe: <https://github.com/emilk/egui> and <https://docs.rs/eframe/latest/eframe/>
- Go Windows registry: <https://pkg.go.dev/golang.org/x/sys/windows/registry>
- Go Windows services: <https://pkg.go.dev/golang.org/x/sys/windows/svc>
- Fyne packaging: <https://docs.fyne.io/started/packaging/>
- Wails/WebView2: <https://wails.io/docs/introduction/>
- Microsoft WebView2 security guidance: <https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/security>
- Microsoft `netsh interface portproxy`: <https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/netsh-interface>
- Microsoft `ControlService`: <https://learn.microsoft.com/en-us/windows/win32/api/winsvc/nf-winsvc-controlservice>
- PowerShell 7 Windows/WPF support: <https://github.com/MicrosoftDocs/PowerShell-Docs/blob/main/reference/docs-conceptual/whats-new/differences-from-windows-powershell.md>
