# StS2 iOS Launcher

Run your own Steam copy of **Slay the Spire 2** natively on an iPad.
This experimental community port compiles the game's C# assembly to ARM64 with
.NET NativeAOT and runs it in Godot's iOS host, with touch and layout adaptations.
It requires no JIT or jailbreak.

**Playable prototype.** Tested on an M4 iPad Pro with iPadOS 18.1 and game
v0.111.0. Other devices and game versions need testing. This is an unofficial
project, not affiliated with or endorsed by Mega Crit. You must own and supply
the game; this repository contains porting code and tools, not game files or an IPA.

## Current status

| Capability | iOS status |
| --- | --- |
| Native game startup and single-player gameplay | Tested on a physical iPad |
| Touch card input and mobile layouts | Implemented; more screen sizes need testing |
| Upgraded card previews | Hold a card in a card list for half a second; tested on iPad |
| Audio | iOS playback session; music and sound confirmed on iPad |
| Live FPS display | Toggle under the in-game **iOS** menu; no recording required |
| Local saves and app upgrades | Separate iOS saves; preserved when upgrading the same app |
| Background/resume | Audio and scene pause/resume hooks implemented |
| Game download on the device | Not implemented; prepare content on a Mac |
| Steam Cloud | QR login, downloads, and vanilla profile import confirmed on iPad; [status and limits](docs/steam-cloud.md) |
| Resource mods | Experimental cosmetic PCK loader and menu; static checks only; [compatibility](docs/mods.md) |
| C# mods and modded Cloud imports | Not supported; individual code mods need AOT ports |
| LAN multiplayer | Host/join, host-save resume, and Bonjour discovery implemented; static checks only; [guide](docs/multiplayer.md) |
| iPhone, simulator and App Store distribution | Not validated / not provided by this workflow |

## Build and play

You need an **Apple Silicon Mac**, full Xcode, an Apple development signing
account, Python 3.11+, Git, a paired iPad with Developer Mode enabled, and the
supported macOS ARM64 Steam installation. The tested toolchain is Xcode 26.3,
Godot .NET 4.5.1 and .NET SDK 9.0.318.

Clone this repository, open its root directory, then run:

```sh
python3 scripts/ios/bootstrap.py
cp ios/config.example.json ios/config.local.json
```

Fill in your Apple team ID, unique bundle ID and target device identifier in the
local config. Then:

```sh
python3 scripts/ios/build.py prepare
python3 scripts/ios/build.py content
python3 scripts/ios/build.py build
python3 scripts/ios/build.py install --content
```

Open **StS2 iOS** on the iPad. Bootstrap downloads the pinned public tools and
builds the native Spine extension. The first installation transfers roughly 2 GB
of game content; later patch-only updates transfer just the app.

Read the [build guide](docs/building.md) for signing, custom Steam paths,
updates and requirements. The build/publish/install pipeline has been exercised
on the development machine; a completely fresh bootstrap still needs independent
validation. Do not assume a newer Steam beta is compatible.

## Saves

The iOS app uses saves in its own sandbox. Cloud downloads are separate archives;
an explicit import can create a vanilla profile in an unused slot after a backup.
Automatic synchronization and uploads are not implemented. A save created with mods may contain
content unavailable in this build.

Upgrade using the same bundle ID to keep progress. **Uninstalling the app removes
its local saves and game content.** See [save management](docs/saves.md) for backup
instructions and compatibility limits.

Back up the iPad to a new dated directory on the Mac with:

```sh
python3 scripts/ios/backup.py
```

Return to the main menu before copying. The command uses your existing local
device configuration and never replaces an earlier backup.

## Performance reference

A two-minute capture on an **M4 iPad Pro 11-inch, iPadOS 18.1**, using Metal at
2420 x 1668 with a 60 FPS cap, recorded:

| Metric | Gameplay sample |
| --- | ---: |
| Active gameplay measured | 95.13 seconds |
| Average FPS | **59.50** |
| p99 frame interval | 29.04 ms |
| Frames over 50 ms | 7 |
| Worst gameplay frame | 104.07 ms |

A scene transition elsewhere in the capture stalled for 1.27 seconds. These are
warm-cache, user-driven measurements, not a guarantee for other devices or a
battery/thermal benchmark. Read the [full reference and chart](docs/benchmarks/2026-10-02.md)
or [run a benchmark](docs/performance.md). Only the anonymized results and chart
are in Git; raw logs, device identifiers and screenshots stay local.

## Documentation

- [Build and install](docs/building.md)
- [Troubleshooting](docs/troubleshooting.md)
- [Save management](docs/saves.md)
- [Architecture and dependency versions](docs/architecture.md)
- [Performance measurement](docs/performance.md)
- [Contributing and reporting bugs](CONTRIBUTING.md)
- [Steam Cloud and sync roadmap](docs/steam-cloud.md)

## Credits and licensing

This fork builds on [Ekyso/StS2-Launcher](https://github.com/Ekyso/StS2-Launcher)
and the MIT-licensed [jhaizhou-ops/sts2-ios](https://github.com/jhaizhou-ops/sts2-ios)
patches and static weaver. Godot and .NET provide the engine and runtime; FMOD and
Spine provide audio and animation.

The porting code is covered by the [MIT license](LICENSE), with upstream notices
preserved. The game and its assets remain Mega Crit's property. FMOD and Spine
have separate license and distribution terms; the MIT license does not grant
rights to redistribute those components or a built game bundle. See
[third-party notices](THIRD_PARTY_LICENSES.md).
