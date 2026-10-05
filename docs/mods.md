# Managed and resource mods

The launcher uses Mono JIT and the game's stock mod loader for DLL and PCK mods.
Dynamic assembly loading, Reflection.Emit, Harmony prefixes/postfixes, transpilers
and generated accessors are available. Windows libraries, desktop APIs and native
extensions without iOS support can still prevent a mod from working. Compatibility
is experimental and depends on the game and mod versions.

## Install mods

Close the app, then copy your mod folders into **Files > StS2 JIT >
mods**. Keep each mod's manifest, DLL, PCK and dependencies together with their
original filenames. The stock game loader scans this directory recursively,
retains its consent prompt, and handles dependency checks and enabled state.
Workshop download and synchronization are not implemented.

For a directory containing your chosen mod folders:

```sh
python3 scripts/ios/build.py install --mods /path/to/mods
python3 scripts/ios/build.py launch
```

This copies files into `Documents/mods`; it does not delete old mods that are
absent from the source directory. Remove unwanted folders through Files while
the app is closed. If a mod prevents startup, remove its folder and relaunch.
Mods execute with the app's privileges.

The game's normal modded save area is used inside the app sandbox.
Preserve the same mod versions when resuming a run. The [Steam Cloud panel](steam-cloud.md)
can import compatible modded profiles into unused slots in that area while mods
are active. Preview validates against the loaded game and mod serializers/models,
and confirmation backs up local saves before copying the original save bytes.
Separate downloads remain archives; Cloud uploads are not implemented.

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
other devices remain unvalidated. No sustained JIT performance reference has
been published; see [performance measurement](performance.md).

Development-time runtime tests also passed for dynamic methods, external DLL
loading, Harmony patch/unpatch, transpilers, generated field access, protected
fields, init-only setters, deferred initialization, finalizers, and concurrent
compilation with garbage collection. The temporary probe app and device automation
used for these tests are not included in the source tree or game build.
