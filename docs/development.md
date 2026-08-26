# Development

## Prerequisites

- Rust 1.86.0 (installed automatically from `rust-toolchain.toml`)
- Windows 10 or later with the MSVC build tools for GUI/helper builds
- IP Helper enabled for live portproxy integration

The core modules and tests run on Linux. The GUI, helper, and Windows adapters compile only on Windows.

## Commands

```bash
cargo fmt --all --check
cargo clippy --workspace --all-targets --all-features -- -D warnings
cargo test --workspace --all-features
cargo run --bin ys-netsh-portproxy
cargo build --release --bins
```

Linux core gate:

```bash
cargo clippy --workspace --all-targets --all-features -- -D warnings
cargo test --workspace --all-features
```

Release output:

```text
target/release/ys-netsh-portproxy.exe
target/release/ys-netsh-portproxy-helper.exe
```

Keep both executables in the same directory. The GUI locates the helper only beside its own canonical executable path.

## Releasing

Distributed builds go to `C:\temp\ys-netsh-portproxy-dist\v<version>\` and are
immutable: never overwrite the contents of an existing versioned folder. Any
code change that ships — however small — requires a version bump in
`Cargo.toml` (refresh `Cargo.lock` with `cargo check`) and a fresh folder.

Use the release script from WSL; it refuses to overwrite an existing version
and always emits both executables plus `SHA256SUMS.txt`:

```bash
./scripts/release.sh
```

## Testing policy

Durable test seams are:

1. Domain validation, range expansion, registry serialization, and reconciliation.
2. Application planning, helper execution, re-read, and verification.
3. IPC framing, versioning, nonce validation, and typed command allowlisting.
4. Windows adapters using disposable test state; tests must never mutate production HKLM paths unless explicitly marked as administrator integration tests.

Do not put domain behavior in egui event handlers.
