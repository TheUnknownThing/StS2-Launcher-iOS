# Build and install

All commands run from the repository root and produce a physical ARM64 iOS app
using Mono JIT. Every new app process needs JIT activation before gameplay.

## Requirements

| Requirement | Configuration |
| --- | --- |
| Build host | Apple Silicon Mac |
| Xcode | Tested with 26.3, iOS SDK and development signing |
| Device | Tested on M4 iPad Pro, iPadOS 18.1; deployment target iOS 16.0 |
| Game | Owned Steam macOS ARM64 installation, v0.111.0 (`41cef1ea`) |
| Engine | Godot .NET 4.5.1 editor and iOS template |
| .NET | SDK 9.0.318; Mono runtime 9.0.20 |
| Tools | Python 3.11+, Git, curl, CMake, Ninja, Xcode command-line tools |

Allow substantial disk space for SDKs, runtime sources, native builds and copies
of the approximately 2 GB content pack. Install full Xcode, finish its first-run
setup and configure your Apple account. Pair and trust the iPad in Xcode's Devices
and Simulators window, then enable Developer Mode on the device.

```sh
xcode-select -p
xcodebuild -version
xcrun devicectl list devices
cmake --version
ninja --version
```

Wireless installation works once the device is paired and reachable. The deployment
target does not establish compatibility with every older iOS release.

## 1. Bootstrap tools

```sh
python3 scripts/ios/bootstrap.py
```

Bootstrap downloads pinned SDKs with checksum verification and builds the iOS
Spine framework. Files stay in `.tools/`, `.cache/` and `vendor/`; the global .NET
installation is unchanged. Install CMake and Ninja separately and make them
available in PATH. Review [dependency notices](../THIRD_PARTY_LICENSES.md).

Existing tools are reused and large downloads support resuming. If a checksum
fails, inspect the named archive before removing it and retrying. Individual
steps have been exercised locally; a fresh bootstrap on another Mac remains
unverified.

## 2. Configure signing and game input

```sh
cp ios/config.example.json ios/config.local.json
```

| Key | Meaning |
| --- | --- |
| `team_id` | Your Apple development team ID from Xcode |
| `bundle_id` | Exact app identifier registered to your team; all tools use this same value |
| `device` | CoreDevice identifier from `xcrun devicectl list devices`; required for install and launch |
| `profile` | Optional frame recording; defaults to false |
| `game` | Optional path to the Steam macOS ARM64 `.app` |
| `sign_identity` | Optional development certificate override; normally inferred from Xcode's signed app |

The default game path is:

```text
~/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app
```

For an existing installation, use its complete bundle ID to retain saves. Earlier
JIT installations used a `.jitprobe` suffix: include that suffix explicitly in
`bundle_id` if upgrading one of those installations. No suffix is added by the
tools. A different ID creates a different app container. Back up before changing
installations and keep personal configuration/signing data out of Git.

## 3. Prepare content and runtime

```sh
python3 scripts/ios/build.py prepare
python3 scripts/ios/build.py content
python3 scripts/ios/build.py runtime
```

`prepare` copies the game's managed assemblies and native extension dependencies
into ignored directories. `content` patches a copy of the PCK, removes the
unavailable Sentry extension, and adds script placeholders for stock Godot. The
Steam installation and desktop saves are unchanged.

`runtime` downloads pinned .NET 9.0.20 sources and the iOS runtime pack, verifies
the revision/checksum, applies `ios/jit/mono-ios-jit.patch`, and builds static ARM64
Mono libraries with JIT and Reflection.Emit enabled. A conflicting runtime
checkout causes failure without overwriting its changes.

Set `game` in the config for a nonstandard Steam library. Alternatively, pass the
same `--game '/path/to/SlayTheSpire2.app'` to `prepare`, `content` and `build`.

## 4. Build, install and enable JIT

```sh
python3 scripts/ios/build.py build
python3 scripts/ios/build.py install --content
python3 scripts/ios/build.py launch
```

`build` checks the Steam assembly against the copied references and content input
metadata, exports a fresh Godot host, compiles the Mono framework, builds the app
with Xcode, packages managed assemblies and dependency adapters, and signs it.
The result is `.cache/xcode-jit/Build/Products/Release-iphoneos/StS2.app`.

`install --content` installs the app and transfers `.cache/StS2.pck` to
`Documents/StS2.pck`. Open **StS2 JIT** to see content/JIT readiness, or use
`launch` to start the app and enable JIT through LLDB. See [activation details](jit.md).
Development signing profiles can expire and require another signed installation.

## Updates

For launcher changes:

```sh
python3 scripts/ios/build.py build
python3 scripts/ios/build.py install
python3 scripts/ios/build.py launch
```

Use the same bundle ID. Upgrading preserves the app's Documents, saves and caches;
uninstalling deletes them. Every build regenerates the host so shell, config,
native bridge and export settings are picked up automatically. Edit source files,
not the generated `.cache/jit-host/` project.

Rerun `runtime` when the Mono patch or alias header changes, resolving conflicting
local runtime-source changes first. For a game update, back up saves and repeat
`prepare`, `content`, `build`, and `install --content` with the same Steam copy.
The build checks assembly identity and source-pack size; these checks do not
prove arbitrary game-version compatibility or content integrity. Dependency
adapters also reject untested input checksums. Do not weaken those checks to
accept a newer beta without validation.

See [troubleshooting](troubleshooting.md), [mods](mods.md), and [save management](saves.md).
