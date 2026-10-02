# Build and install

Run all commands from the repository root. This workflow builds for a physical
ARM64 iOS device; it does not produce simulator builds or an App Store submission.

## Requirements

| Requirement | Tested configuration |
| --- | --- |
| Build host | Apple Silicon Mac |
| Xcode | 26.3, with iOS SDK and development signing configured |
| Device | M4 iPad Pro 11-inch, iPadOS 18.1 |
| Game | Owned Steam macOS ARM64 installation, v0.111.0 (`41cef1ea`) |
| Engine | Official Godot .NET 4.5.1 editor and iOS template |
| .NET | SDK 9.0.318, installed in `.tools/dotnet` |
| Other tools | Python 3.11+, Git, curl and Xcode command-line tools |

Allow several GB for downloads and substantially more for extracted SDKs, native
builds and copies of the approximately 2 GB content pack. The game's beta updates
can change patch targets; other versions have not been validated. The project
deployment target is not evidence that every older iOS release works.

Install full Xcode, open it once to finish setup, and configure your Apple account
under Xcode Settings. Verify the active developer directory:

```sh
xcode-select -p
xcodebuild -version
xcrun devicectl list devices
```

Pair and trust the iPad in Xcode's Devices and Simulators window, enable Developer
Mode on the device, and make it reachable. Once pairing is established, the
workflow supports wireless installation. Select the iPad explicitly if multiple
devices are paired.

## 1. Bootstrap tools

```sh
python3 scripts/ios/bootstrap.py
```

This downloads pinned SDK archives with checksum verification, checks out pinned
Spine/godot-cpp sources, and builds the iOS Spine framework. Files stay under
`.tools/`, `.cache/downloads/` and `vendor/`. The global .NET installation is not
modified. Review [dependency notices](../THIRD_PARTY_LICENSES.md) for middleware
license terms.

Existing tools are reused. Large downloads support resuming by rerunning the
command. A checksum failure requires inspecting/removing the named cached
archive before retrying. The individual build steps and cached bootstrap path
were tested; a complete bootstrap on another clean Mac remains unverified.

## 2. Configure signing

```sh
cp ios/config.example.json ios/config.local.json
```

Edit the local file:

```json
{
  "team_id": "YOUR_APPLE_TEAM_ID",
  "bundle_id": "dev.yourname.sts2ios",
  "device": "YOUR_IPAD_COREDEVICE_ID",
  "profile": false
}
```

| Key | Meaning |
| --- | --- |
| `team_id` | Apple team ID shown by Xcode; not necessarily the suffix in a certificate's display name |
| `bundle_id` | Unique app identifier registered to your team; keep it stable to retain the same app container |
| `device` | Identifier from `xcrun devicectl list devices` |
| `profile` | Enable optional benchmark recording; leave false for ordinary play |

The local config is ignored. Never commit certificates, profiles, account tokens,
device identifiers or personal config files.

## 3. Prepare your game copy

```sh
python3 scripts/ios/build.py prepare
python3 scripts/ios/build.py content
```

The default source is:

```text
~/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app
```

For a different Steam library, pass the same app path to both stages:

```sh
python3 scripts/ios/build.py prepare --game '/path/to/SlayTheSpire2.app'
python3 scripts/ios/build.py content --game '/path/to/SlayTheSpire2.app'
```

`prepare` copies managed dependencies and native extension descriptors into
ignored build directories. `content` patches a copy of the PCK, removes the
unavailable Sentry extension, and supplies script placeholders required by stock
Godot. The Steam installation and desktop saves are not modified.

## 4. Build and install

```sh
python3 scripts/ios/build.py build
python3 scripts/ios/build.py install --content
```

The build applies the static patches, publishes NativeAOT, exports a Godot host,
embeds the native library, and invokes Xcode with automatic provisioning. The
signed result is `.cache/xcode/Build/Products/Release-iphoneos/StS2.app`.

The first install transfers the app and `.cache/StS2.pck` into the app's
`Documents/StS2.pck`. Open **StS2 iOS** on the iPad after transfer completes.
Personal development profiles can expire and require another signed installation.

## Updates

For changes only to patches or benchmark instrumentation:

```sh
python3 scripts/ios/build.py build
python3 scripts/ios/build.py install
```

This upgrades the existing app without retransferring the content pack. Saves and
shader caches remain in its container. Do not uninstall to update.

Use `build --export-host` after changing the Godot shell, native extensions, export
preset or bundle ID. Build outputs are generated; edit the source/config rather
than treating edits inside `ios/build/` as permanent.

For an intentional game-version update, back up your iOS saves, then repeat
`prepare`, `content`, `build`, and `install --content` using the same game copy.
These stages do not enforce a version match automatically. Do not mix an older
compiled app with a newer pack. A successful build does not establish gameplay
compatibility with a new beta version.

See [troubleshooting](troubleshooting.md) for common failures and
[save management](saves.md) before changing installations.
