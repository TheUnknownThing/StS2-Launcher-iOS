# StS2 iOS Launcher

Run your own Steam copy of **Slay the Spire 2** on an iPad using **Mono JIT**,
Godot's iOS host, and mobile touch/layout adaptations. JIT is required for every
launch: enable it again whenever the app process restarts. The app is named
**StS2 JIT** and loads managed DLL mods through the game's loader and Harmony.

**Playable community prototype.** Tested on an M4 iPad Pro with iPadOS 18.1 and
game v0.111.0. This is an unofficial project, not affiliated with or endorsed by
Mega Crit. You must own and supply the game; this repository contains porting
code and tools, not game files or an IPA.

## Current status

| Capability | iOS status |
| --- | --- |
| Startup and single-player gameplay | Exercised on a physical iPad using Mono 9.0.20 |
| Managed and resource mods | BaseLib, QuickRestart, Watcher and LieRenTVmod smoke-tested; [versions and limits](docs/mods.md#device-results) |
| Touch input and mobile layouts | Implemented; more screen sizes need testing |
| Upgraded card previews | Hold a card in a card list for half a second |
| Audio and background/resume | Playback session and pause/resume hooks implemented |
| Live FPS display | Toggle under the in-game **iOS** menu |
| Local saves and app upgrades | Preserved when upgrading the same bundle ID |
| Steam Cloud | QR login, downloads and vanilla import exercised on iPad; modded import has source tests, device validation pending; [guide](docs/steam-cloud.md) |
| LAN multiplayer | Host/join, resume and Bonjour implemented; offline checks only; [guide](docs/multiplayer.md) |
| On-device game download | Prepare content on a Mac |
| iPhone, simulator and App Store distribution | Not validated / not provided |

## Build and play

You need an **Apple Silicon Mac**, full Xcode, Apple development signing, Python
3.11+, Git, CMake, Ninja, a paired iPad with Developer Mode enabled, and the
supported macOS ARM64 Steam installation. The deployment target is iOS 16.0;
the tested OS is iPadOS 18.1. See the [build guide](docs/building.md) for setup.

From the repository root:

```sh
python3 scripts/ios/bootstrap.py
cp ios/config.example.json ios/config.local.json
```

Fill in your Apple team ID, bundle ID and device identifier. The configured
bundle ID is used exactly by build, installation, launch, backup and profiling.
Keep it stable to preserve the app's data. Then:

```sh
python3 scripts/ios/build.py prepare
python3 scripts/ios/build.py content
python3 scripts/ios/build.py runtime
python3 scripts/ios/build.py build
python3 scripts/ios/build.py install --content
python3 scripts/ios/build.py launch
```

The first installation transfers roughly 2 GB of prepared content. Opening
**StS2 JIT** shows setup instructions until content is present and JIT is enabled.
The Mac launch command enables JIT by attaching and detaching LLDB. A compatible
external JIT tool can also activate the running app; see [JIT activation](docs/jit.md).
A complete bootstrap on another clean Mac still needs independent validation.

## Saves and mods

Install mod folders through **Files > StS2 JIT > mods** while the app is closed.
Keep their manifests, DLLs, PCKs and dependencies together. The game's loader
handles consent, dependency checks and modded save isolation. See [mod support](docs/mods.md).

Steam Cloud downloads are separate archives. Explicit import can create a
compatible vanilla or modded profile in an unused slot after a backup. Automatic
synchronization and uploads are not implemented.

**Uninstalling the app removes local saves and game content.** Upgrade with the
same bundle ID and back up from the main menu:

```sh
python3 scripts/ios/backup.py
```

See [save management](docs/saves.md) for backup coverage and compatibility limits.

## Documentation

- [Build and install](docs/building.md)
- [JIT activation and runtime limits](docs/jit.md)
- [Managed and resource mods](docs/mods.md)
- [Troubleshooting](docs/troubleshooting.md)
- [Save management](docs/saves.md)
- [Architecture and dependency versions](docs/architecture.md)
- [Performance measurement](docs/performance.md)
- [Steam Cloud](docs/steam-cloud.md)
- [LAN multiplayer](docs/multiplayer.md)
- [Contributing](CONTRIBUTING.md)

## Credits and licensing

This fork builds on [Ekyso/StS2-Launcher](https://github.com/Ekyso/StS2-Launcher)
and the MIT-licensed mobile patches and content tools from
[jhaizhou-ops/sts2-ios](https://github.com/jhaizhou-ops/sts2-ios). Godot and .NET
provide the engine and runtime; FMOD and Spine provide audio and animation.

The porting code is covered by the [MIT license](LICENSE), with upstream notices
preserved. The game and assets remain Mega Crit's property. FMOD and Spine have
separate license and distribution terms. See [third-party notices](THIRD_PARTY_LICENSES.md).
