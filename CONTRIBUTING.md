# Contributing

## Scope

FrameGen Manager-Next is a Windows-only frame-generation deployment manager built with .NET 10, a WPF host and a WebView2 HTML/CSS UI. It scans game libraries, deploys proxy/runtime assets, and records backups so users can restore their game directories safely.

The old `FrameGen Manager` repository is kept for reference and is not modified.

## Build and test

```powershell
dotnet restore FrameGenManagerNext.slnx
dotnet format FrameGenManagerNext.slnx --verify-no-changes
dotnet build FrameGenManagerNext.slnx -c Debug --no-restore
dotnet test  FrameGenManagerNext.slnx -c Debug --no-build
```

Full gate, including pack schema validation and structural checks:

```powershell
pwsh ./build/verify.ps1 -Configuration Debug
```

If PowerShell 7 is unavailable:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ./build/verify.ps1 -Configuration Debug
```

The .NET SDK may be installed under `%LOCALAPPDATA%\Microsoft\dotnet`. In that case, set `DOTNET_ROOT` to the same directory and add it to `PATH`.

## Architecture

```
Domain  ←  Core  ←  Infrastructure
                 ↗
App / Cli  ─────┘
```

- `FrameGenManagerNext.Domain`: models, rules, error codes and pack contracts. No OS or UI dependencies.
- `FrameGenManagerNext.Core`: use cases and port interfaces.
- `FrameGenManagerNext.Infrastructure`: Windows, file system, HTTP, NVAPI, ReShade and persistence implementations.
- `FrameGenManagerNext.App`: WPF host, WebView2 bridge and web assets.
- `FrameGenManagerNext.Cli`: headless commands used by tests and diagnostics.

Features live in vertical slices under `Core/Features/<Feature>`. Keep each feature's service, models and tests together.

## Coding rules

- Use the latest stable .NET/NuGet versions pinned in `Directory.Packages.props`. No preview packages.
- Keep `Domain` and `Core` free of WPF, WinForms and Win32 dependencies.
- Put all P/Invoke and native interop under `Infrastructure`.
- Do not put user-facing text in `Domain` or `Core`. Return structured errors and progress keys; the UI and CLI own the wording.
- Do not hardcode absolute paths. Use `AppPaths` or an injected abstraction.
- Keep files under 400 lines where practical; CI fails above 500.
- Prefer one public type per file.
- Add tests for every rule that can be tested without hardware.
- Do not commit binary assets or pack payloads. Manifests live in `packs/`; payloads are generated under `assets/packs/` and ignored by git.

## Definition of done

- The relevant behavior spec in `docs/behavior/` is updated.
- Rules have unit tests; file-system or OS behavior has integration tests or a documented reason.
- Failure paths map to an `ErrorCode`.
- `dotnet format --verify-no-changes` and `build/verify.ps1` pass.
- No new hardcoded paths, localized strings in Core, or oversized files.

## Git and release

- Work on a feature branch; keep pull requests focused on one module.
- Commit messages: `<type>(<scope>): <summary>`.
- App and asset-pack versions are independent.
- Portable builds are produced with `build/publish-portable.ps1` and are accepted before an installer is generated.
- Releases use Velopack and GitHub Releases. Verify SHA-256 before publishing.
