# Rust core

Phase 1 workspace for the Rust + Flutter rewrite.

```
crates/
  fgm-domain/  pure models, rules, ErrorCode, pack protocol
  fgm-core/    use cases and port traits
  fgm-infra/   Windows/FS/NVAPI/ReShade/network implementations
  fgm-ffi/     thin C ABI for Flutter
  fgm-cli/     headless acceptance entry point
```

The legacy C# tree is frozen as the behavior oracle until parity. New code only goes here or in `ui/`.

The toolchain is pinned to `nightly-2026-11-17` in `rust-toolchain.toml`; upgrades are separate commits with the full gate.

## Gates

```bash
cargo fmt --all -- --check
cargo clippy --workspace --all-targets --all-features -- -D warnings
cargo test --workspace
```

## Pack validation

```bash
cargo run -p fgm-cli -- pack validate <pack-dir> --app-version 2.3.4
cargo run -p fgm-cli -- pack validate <pack-dir> --schema-only
cargo run -p fgm-cli -- pack validate <pack-dir> --no-hash
```

Exit codes: `0` valid, `1` validation failure, `2` argument or directory error.

## Deploy / restore

```bash
cargo run -p fgm-cli -- deploy <pack-dir> <target-dir> --family framegen.sm86 --data-dir <dir> --game-exe <game.exe> --app-version 2.3.4
cargo run -p fgm-cli -- deploy <pack-dir> <target-dir> --dry-run
cargo run -p fgm-cli -- restore <target-dir> --family framegen.sm86 --data-dir <dir>
```

`deploy` selects one proxy slot, merges the pack INI, writes through the file transaction engine, and records a manifest. `restore` reports conflicts with exit code `1`.

## Library

```bash
cargo run -p fgm-cli -- library scan [--root <dir>]... [--data-dir <dir>]
cargo run -p fgm-cli -- library list [--data-dir <dir>]
```

`library scan` combines Steam, Epic and loose-folder discovery, then writes `library/library.json`.
