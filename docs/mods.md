# Mods on iOS

The first mod-loading path supports **cosmetic resource packs**. C# DLL mods
require individual ports because the iOS build uses .NET NativeAOT: dropping a
desktop assembly into the app cannot supply new compiled code, and desktop
Harmony patches commonly generate code at runtime.

The resource loader and menu have static/offline checks. No modded gameplay has
been tested on iPad with this implementation.

## Install a resource mod

Use your own copy of a mod, following its author's terms. Nothing from Workshop
is bundled or downloaded automatically by this repository.

1. Open StS2 once to create its `mods` folder. Fully close the app before copying
   or replacing mod files.
2. In Files (or Finder file sharing), copy the manifest and pack into the app's
   Documents folder using this layout:

   ```text
   mods/
     ExampleCosmetic/
       ExampleCosmetic.json
       ExampleCosmetic.pck
   ```

3. Open **iOS > Resource mods**, refresh, and enable the mod. The menu reports
   unsupported packages and the game's load status for this launch.
4. Fully close and reopen the app. Resource packs cannot be unloaded safely
   during a running game, so enable/disable changes require a restart.

The folder, manifest ID, JSON filename, and PCK filename must agree. The initial
policy requires `has_pck: true`, `has_dll: false`, `affects_gameplay: false`, and
no dependencies. It accepts unencrypted Godot 4 pack formats v2/v3, rejects
embedded C# code/native extensions, and checks directory paths and file bounds.
The game's own minimum-version and load checks still apply. Load order is by
mod ID; packs replacing the same resources can conflict.

Godot scripts and shaders in resource packs are allowed. A cosmetic declaration
is the author's description, not a sandbox or proof of compatibility. Enable
only trusted mods. Desktop texture compression, compiled script versions, and
scene references may still require iOS-specific exports. A pack that passes
inspection can still fail to load or render on a device.

If a mod prevents startup, close the app and remove its folder through file
sharing. Its saved toggle is harmless while the folder is absent. Deleting
`Documents/resource-mods.cfg` disables all resource-mod selections on next launch.

## Saves and Cloud

The loader keeps the game's normal mod tracking. Even cosmetic packs select
the separate `Documents/default/1/modded` save area. On first activation, the
game copies existing vanilla profiles into that area when it is absent. Vanilla
profiles remain in their original area. Disabling all mods and restarting returns
to vanilla profiles; it does not merge progress back from modded profiles.

Take a [save backup](saves.md) before changing mod sets. Loading a resource pack
does not supply missing Watcher cards, characters, or other C# models. Modded
Steam Cloud files remain archive-only. Vanilla Cloud import requires resource
mods to be disabled and the app restarted.

## Examples inspected locally

These installed versions were inspected as compatibility examples, without
executing their code. Their binaries, resources, user configuration, and save
files are not part of this repository.

| Mod | Inspected version | Current assessment |
| --- | --- | --- |
| LieRenTVmod | v0.1.3 | Cosmetic PCK, no declared dependencies; its 485-entry pack passes resource-policy checks. Rendering remains unverified. |
| Quick Restart | v2.0.0 | C# DLL and PCK; requires BaseLib >= 3.4.5. Needs its initialization, input/UI, and runtime patches adapted. |
| BaseLib | v3.4.7 | C# utility library with a broad patch/API surface; needs an AOT compatibility port. |
| Watcher | 0.9.29 | C# gameplay content and many model/UI patches. Requires a full content-code port before its saves are supported. |

Quick Restart is the smallest code-mod candidate, but its BaseLib dependency
makes enabling the original package more than a loader change. A focused mobile
implementation of a restart-room feature would be a separate change, with save
and multiplayer behavior reviewed explicitly.

## Developer checks

The offline inspector reads manifests and PCK directories; it never loads mod
assemblies, starts Godot, or contacts Steam:

```sh
dotnet run --project tests/features/FeatureTests.csproj -c Release
dotnet run --project tests/features/FeatureTests.csproj -c Release -- --inspect /path/to/ExampleCosmetic
dotnet run --project tests/features/FeatureTests.csproj -c Release -- --inspect-pack /path/to/ExampleCosmetic.pck
```

An eventual DLL port needs build-time assembly inclusion, static patch weaving,
model/script registration before game initialization, preserved AOT metadata,
resource compatibility, and version/mod handshake validation. The current
weaver supports a deliberately small prefix/postfix subset; it is not a general
Harmony replacement. Each mod's license and update strategy also need review
before distributing an adapter.
