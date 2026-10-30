<p align="center"><img src="docs/images/icon.png" width="96" height="96" alt="FrameGen Manager-Next icon"></p>

# FrameGen Manager-Next

[中文](README.md) | **English**

[![CI](https://github.com/XiaoQAQ10086/FrameGen-Manager-Next/actions/workflows/ci.yml/badge.svg)](https://github.com/XiaoQAQ10086/FrameGen-Manager-Next/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11%20x64-0078D4)](#requirements)
[![.NET](https://img.shields.io/badge/.NET-10%20LTS-512BD4)](#tech-stack)

## What is this

One-click deployment of frame generation and DLSS5 for NVIDIA RTX 20 / 30 series GPUs.

FrameGen Manager-Next is a rewrite of the [original FrameGen-Manager](https://github.com/XiaoQAQ10086/FrameGen-Manager), built from scratch with **C# / .NET 10 LTS**. It scans your game library, deploys frame generation and DLSS5 components into game folders, and supports reversible restore and cleanup.

It is **not** a frame generation renderer: frame generation itself is performed by the game, the NVIDIA runtime and ReShade. This tool only detects, deploys, configures and cleans up.

![Frame generation page](docs/images/main.png)

## Features

- **Game library scan**: Steam / Epic / loose game folders, with manual game adding and manual selection of the executable a game actually launches.
- **Frame generation deployment**: deploys SM75 / SM86 runtimes for RTX 20 / 30 series GPUs, with proxy slot, optimization level, 2X/3X/4X/6X, DLSS / XeSS engine, super-resolution mode and XeSS optical-flow settings. Deployments can be restored; files modified by the user are kept and reported as conflicts.
- **Smooth Motion**: writes the Smooth Motion switch into the game's driver profile and deploys the unlock component. The previous value is recorded before writing, the component is removed when disabled, and the original value is restored on restore.
- **DLSS5 / ReShade**: offline installation of the ReShade module, GITC Uplift components and standard shaders, with uninstall and legacy RenoDX cleanup.
- **GPU model spoofing**: changes the GPU name recorded by the driver with administrator rights; supports applying a preset, restoring the driver name, and restoring the original value. A reboot is required.
- **Diagnostics and localization**: structured logs, diagnostic bundle export, and runtime Chinese/English switching.

| Smooth Motion | DLSS5 / ReShade |
|---|---|
| ![Smooth Motion page](docs/images/smooth-motion.png) | ![DLSS5 page](docs/images/dlss5.png) |

## Why the original project needed a rewrite

As FrameGen-Manager grew and went through long-term iteration, structural problems became visible: blurred module boundaries, tight coupling between UI and core logic, and drifting public contracts. These made long-term maintenance increasingly difficult.

## Supported components

| Component | GPUs | Notes |
|---|---|---|
| Frame generation (SM86 / SM75) | RTX 30 / 20 series | Proxy DLL + `dlssg_sm86` runtime, supports DLSS and XeSS engines |
| Smooth Motion | RTX 30 / 20 series, driver ≥ 616.64 | Driver profile switch + unlock component; games with kernel anti-cheat are rejected |
| DLSS5 / ReShade | Depends on the game's graphics API | Offline ReShade + GITC Uplift + standard shaders |
| GPU model spoofing | NVIDIA discrete GPUs | Requires administrator rights; takes effect after reboot |

Frame generation and Smooth Motion are mutually exclusive for the same game: when one is enabled, the other shows a red conflict badge and locks its action, preventing two injectors from entering the same game process.

## Tech stack

| Layer | Choice |
|---|---|
| Runtime | .NET 10 LTS (`net10.0-windows`) |
| Host | WPF + Wpf.Ui (window shell only) |
| UI | WebView2 + local HTML/CSS/JS |
| Composition | Microsoft.Extensions.Hosting / DI |

Package versions are centralized in [`Directory.Packages.props`](Directory.Packages.props). Core extensions and test assertions track the latest preview/RC builds; Serilog and Velopack stay on stable releases; the runtime stays on .NET 10 LTS.

## Requirements

- Windows 10 / 11 x64
- .NET 10 SDK 10.0.403 or newer

## Project structure

```text
src/
  FrameGenManagerNext.Domain/          Models, rules, error codes, asset pack protocol
  FrameGenManagerNext.Core/            Use-case services and port interfaces
  FrameGenManagerNext.Infrastructure/  Windows, file, NVAPI, ReShade and pack implementations
  FrameGenManagerNext.App/             WPF host + WebView2 UI
  FrameGenManagerNext.Cli/             Headless acceptance entry point
tests/                                 Domain / Core / Infrastructure / Integration tests
docs/behavior/                          Behavior specifications for each module
build/                                 Pack generation, validation and publishing scripts
```

Dependency direction: `Domain ← Core ← Infrastructure`; `App` / `Cli` reference Infrastructure only at the composition root. Core and Domain do not reference WPF/WinForms, and all P/Invoke stays in Infrastructure.

## Acknowledgements and disclaimer

- Frame generation runtimes, Smooth Motion components and GITC Uplift assets come from their respective upstream projects; copyright belongs to the original authors. This repository does not contain those binary assets.
- Frame generation files: <https://github.com/sdli1995/dlssg_for_sm86>
- Smooth Motion files: <https://github.com/pipotoufikxyz-lgtm/dlssg_for_sm86-MFG-version>
- DLSS5 plugin: <https://github.com/ghostinthecamera/GITC-Uplift>
- ReShade: <https://github.com/crosire/reshade>
- DLSS5 model/render files for RTX 20 / 30 series were collected from the internet: <https://www.bilibili.com/video/BV1hXaA6jEDJ/?spm_id_from=333.1387.favlist.content.click&vd_source=ae80914c86306af292e3a6fe3e9444dd>
- This is a community tool, not affiliated with NVIDIA, Intel or ReShade. No official support or warranty is provided.
- Check the game's terms of service and anti-cheat policy before use; do not deploy injected components into games with kernel anti-cheat.

## Feedback

If you run into problems or have suggestions, join our Tencent QQ groups: **1125445047** (group 2: **181592939**).

## License

Project code is licensed under the [MIT License](LICENSE).
