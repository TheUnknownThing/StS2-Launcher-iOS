# Experimental JIT runtime

This path runs the original game assembly and managed mod DLLs under Mono JIT on
iOS ARM64. It replaces the NativeAOT managed runtime while retaining the existing
Godot host, native extensions, touch adaptations and layout hooks. It installs as
**StS2 JIT**, with the configured bundle ID suffixed by `.jitprobe`, and has its
own saves, content and mod directory.
The suffix is retained from the initial experiment to preserve installed saves.

JIT makes substantially more desktop mod code usable: dynamic assembly loading,
Reflection.Emit, Harmony prefixes/postfixes, transpilers and generated accessors
can execute. It does not supply Windows libraries, desktop APIs, compatible
native extensions or missing iOS resources. It is an experiment, not universal
mod compatibility or an App Store build.

## Build and install

Complete the [normal build prerequisites](building.md), including bootstrap,
configuration, `prepare`, `content` and `build`. Do not run the normal `install`
stage unless you also want the NativeAOT app. Then run:

```sh
python3 scripts/ios/jit.py runtime
python3 scripts/ios/jit.py game
python3 scripts/ios/jit.py install-game --content
python3 scripts/ios/jit.py launch
```

`runtime` needs CMake and Ninja in PATH. It downloads pinned .NET 9.0.20 sources
and the iOS runtime pack, verifies their revision/checksum, applies the checked-in
Mono patch, and builds a static ARM64 JIT runtime. Existing incompatible source
changes cause a failure instead of being overwritten. Generated files remain
under `vendor/` and `.cache/`.

The build uses the original assemblies in `upstream/game-refs`, not the woven
NativeAOT game. It verifies the Steam game assembly matches the prepared references
and copies its release metadata. For a nonstandard Steam location, add `game`
(the macOS `.app` path) to `ios/config.local.json`. The signing certificate is
inferred from Xcode's signed app; `sign_identity` can override it if needed.

The first install copies about 2 GB of prepared game content. Later upgrades need
only `install-game`. Upgrading the same experimental bundle ID preserves its
sandbox. Uninstalling removes its saves and content. The experimental app also
uses a development signing slot.

## Enable JIT

The tested method is `python3 scripts/ios/jit.py launch` on a paired Mac. It starts
the experimental process suspended, attaches LLDB, then detaches immediately.
The process keeps debugger-enabled executable-memory permission for its lifetime.
The command restarts an existing experimental process, so save and return to the
menu before using it during play.

Opening StS2 JIT creates its shared Files folder before starting Godot. On a
fresh install, the setup screen asks for the prepared `StS2.pck` in **Files >
On My iPad > StS2 JIT**. Copy the contents of your prepared Documents directory
there, without adding another Documents subfolder. The app waits until the pack
matches the size recorded at build time, so an incomplete copy cannot start the
engine. Use the content prepared for that build; this size check does not verify
the file's integrity.

Once content is present, the app presents an enable-JIT screen. A compatible external
JIT tool can attach to that running app; the game starts automatically once the
process is debugged. For an already running process, the Mac helper accepts
`launch --pid PID`. AltStore/SideStore-related JIT activation workflows depend
on the iOS version and external tools; those workflows have not been tested here.
Installing through a sideloading tool alone does not grant JIT permission.

JIT must be enabled again after a force quit, crash, OS termination or device
restart. The runtime additionally executes a small native-code probe before
initialization to verify that executable memory actually works.

## Install mods

Close the experimental app, then copy your mod folders into **Files > StS2 JIT >
mods**. Keep each mod's manifest, DLL, PCK and dependencies together with their
original filenames. The stock game loader scans this directory recursively,
retains its consent prompt, and handles dependency checks and enabled state.
Workshop download and synchronization are not implemented.

For a directory containing your chosen mod folders:

```sh
python3 scripts/ios/jit.py install-game --mods /path/to/mods
python3 scripts/ios/jit.py launch
```

This copies files into `Documents/mods`; it does not delete old mods that are
absent from the source directory. Remove unwanted folders through Files while
the app is closed. If a mod prevents startup, remove its folder and relaunch.
Mods execute with the app's privileges.

The game's normal modded save area is used inside this separate app sandbox.
Preserve the same mod versions when resuming a run. Cloud downloads remain
archives; this experiment does not add modded Cloud import or upload support.

## Device results

Test environment: M4 iPad Pro, iPadOS 18.1, Xcode 26.3, Godot .NET 4.5.1,
game v0.111.0, Mono 9.0.20. All four mods were enabled together using the user's
locally installed Workshop packages; no mod DLL was rewritten.

| Mod | Version | Observed result |
| --- | --- | --- |
| BaseLib | 3.4.7 | 282 patches succeeded, zero failed; custom models and save serialization initialize |
| QuickRestart | 2.0.0 | Initializes and adds Restart Room; reload restores Watcher from turn 2 / 60 HP to turn 1 / 72 HP |
| Watcher | 0.9.29 | 76 patches applied, zero failed/skipped; selection, new run, save/resume, enemy turn, Eruption (9 damage) and Strike in Wrath (12 damage) exercised |
| LieRenTVmod | 0.1.3 | Resource pack loads alongside the three code mods; individual replacements have not all been visually verified |

These are smoke tests, not completed runs or a compatibility guarantee. Sustained
performance, battery/thermal behavior, all cards/relics/events, multiplayer and
other devices remain unvalidated. The NativeAOT performance numbers in the README
do not measure this runtime.

Development-time runtime tests also passed for dynamic methods, external DLL
loading, Harmony patch/unpatch, transpilers, generated field access, protected
fields, init-only setters, deferred initialization, finalizers, and concurrent
compilation with garbage collection. The temporary probe app and device automation
used for these tests are not included in the source tree or game build.

## Runtime design and limits

`ios/jit/RuntimeHost.mm` exposes Godot's existing managed entry ABI and initializes
Mono using the iOS BCL. `STS2Jit` applies the shared mobile hook manifest through
Harmony and redirects the stock mod scan to Documents. It omits the NativeAOT
cosmetic-only loader hooks. Godot singleton access waits until the engine has
initialized its managed/native bridge.

`MonoJitMemory.mm` creates separate executable (RX) and writable (RW) mappings of
the same backing pages. The Mono patch redirects ARM64 instruction emission,
literal data, jump tables and code updates to the writable alias while keeping
executable addresses for branches. A single mapping is never simultaneously
writable and executable. Code deallocation releases the alias; non-code GC
deallocation avoids the alias lock.

The patch also honors `IgnoresAccessChecksTo` for generated Harmony accessors and
defers `beforefieldinit` type initialization until static field access. The stock
game relies on that timing when registering models. These runtime semantics need
broader regression testing; they are not an upstream-supported iOS Mono JIT mode.

`STS2JitPrepare` adapts copies of two pinned shared dependencies: CoreLib's dynamic
code capability report and Harmony's platform selection, patch writes and return
signature modifiers (including init-only setters). Input checksums reject
untested versions. Original game and mod assemblies stay unchanged.

Native/AOT code detours are deliberately unsupported: the patch backend only
writes registered Mono JIT regions. EventPipe and the managed debugger agent are
disabled. During development, some failure paths involving generic exception
stack formatting triggered a Mono native assertion; resolving the triggering
startup errors avoided it, but the underlying diagnostic-path issue remains.

The sibling Android launcher also extends stock mod discovery and works around
Mono model initialization. This experiment handles initialization in the runtime
and adapts Harmony once for iOS, rather than adding a separate port for every mod.

## Troubleshooting

Startup, runtime errors and mod-loading messages are in `Documents/jit-game.log`,
available through Files. The log is replaced on each game startup. Include the
relevant error and your game/mod versions when reporting a problem, after removing
personal paths or identifiers. Device logs, saves and screenshots stay out of Git.
