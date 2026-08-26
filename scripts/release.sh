#!/usr/bin/env bash
# Build a versioned, immutable Windows release into the dist directory.
#
# Enforced invariants:
#   1. The dist folder for the current Cargo.toml version must not already
#      exist — distributed artifacts are immutable. Bump the version instead.
#   2. Both executables and a SHA256SUMS.txt are always produced together.
#
# Run from WSL. Requires the Windows MSVC toolchain (cargo.exe on PATH).
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DIST_ROOT="${DIST_ROOT:-/mnt/c/temp/ys-netsh-portproxy-dist}"
TARGET_DIR_WIN="${TARGET_DIR_WIN:-C:\\temp\\ys-netsh-portproxy-target}"
TARGET_DIR_WSL="$(wslpath -u "$TARGET_DIR_WIN")"

VERSION="$(grep -m1 '^version' "$REPO_ROOT/Cargo.toml" | sed 's/.*"\(.*\)".*/\1/')"
DIST_DIR="$DIST_ROOT/v$VERSION"

if [[ -z "$VERSION" ]]; then
  echo "error: could not read version from Cargo.toml" >&2
  exit 1
fi

if [[ -e "$DIST_DIR" ]]; then
  echo "error: $DIST_DIR already exists." >&2
  echo "Distributed artifacts are immutable: bump the version in Cargo.toml" >&2
  echo "(and refresh Cargo.lock) instead of overwriting a shipped build." >&2
  exit 1
fi

if [[ -n "$(git -C "$REPO_ROOT" status --porcelain)" ]]; then
  echo "warning: releasing v$VERSION from a dirty working tree" >&2
fi

MANIFEST_WIN="$(wslpath -w "$REPO_ROOT/Cargo.toml")"
echo "Building v$VERSION with the Windows MSVC toolchain..."
(cd "$(wslpath -u 'C:\temp')" \
  && cmd.exe /c "set CARGO_TARGET_DIR=$TARGET_DIR_WIN&& cargo build --release --bins --manifest-path $MANIFEST_WIN")

mkdir -p "$DIST_DIR"
cp "$TARGET_DIR_WSL/release/ys-netsh-portproxy.exe" \
   "$TARGET_DIR_WSL/release/ys-netsh-portproxy-helper.exe" \
   "$DIST_DIR/"
(cd "$DIST_DIR" && sha256sum ys-netsh-portproxy-helper.exe ys-netsh-portproxy.exe > SHA256SUMS.txt)

echo
echo "Released v$VERSION to $DIST_DIR:"
ls -l "$DIST_DIR"
