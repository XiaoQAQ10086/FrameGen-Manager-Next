# Flutter shell

Phase 0 desktop shell for FrameGen Manager-Next. It renders the top-level
navigation and the first frame-generation workspace, and loads the Rust core
through the `fgm_ffi.dll` C ABI.

## Layout

```
lib/
  app/        shell, navigation, root widget
  bridge/     FFI loading and DTO boundary
  design/     theme and visual tokens
  features/   one directory per feature slice
  l10n/       ARB sources; generated bindings are ignored
test/         widget smoke tests
tool/         cross-language smoke checks
```

Feature pages never call `dart:ffi` directly; only `bridge/` does.

## Toolchain

The SDK is pinned in `.flutter-version` (master channel). The gate rejects a
different channel or framework revision.

## Commands

```powershell
flutter pub get
flutter gen-l10n
flutter analyze
flutter test
flutter build windows --release
```

Build the native core first so the CMake post-build step can copy it:

```powershell
cd ../rust
cargo build -p fgm-ffi --release
```

For the FFI smoke check:

```powershell
$env:FGM_FFI_DLL = (Resolve-Path ../rust/target/release/fgm_ffi.dll)
dart run tool/ffi_smoke.dart
```

The full gate lives in `../build/verify-rust-flutter.ps1`.
