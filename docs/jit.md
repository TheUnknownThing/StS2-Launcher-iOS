# JIT activation and runtime

Mono JIT is the launcher's managed runtime. It runs the original game assembly
and managed mods on iOS ARM64, with Harmony applying mobile hooks at startup.
JIT activation is required whenever the app process restarts. Complete the
[build and install guide](building.md) first; see [mods](mods.md) for installation
and tested compatibility.

## Enable JIT

The tested method is `python3 scripts/ios/build.py launch` on a paired Mac. It starts
the app process suspended, attaches LLDB, then detaches immediately.
The process keeps debugger-enabled executable-memory permission for its lifetime.
The command restarts an existing app process, so save and return to the
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

## Runtime design and limits

`ios/jit/RuntimeHost.mm` exposes Godot's existing managed entry ABI and initializes
Mono using the iOS BCL. `STS2Jit` applies the mobile hook manifest through
Harmony and redirects the stock mod scan to Documents. Godot singleton access
waits until the engine has initialized its managed/native bridge.

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

Native executable detours are deliberately unsupported: the patch backend only
writes registered Mono JIT regions. EventPipe and the managed debugger agent are
disabled. During development, some failure paths involving generic exception
stack formatting triggered a Mono native assertion; resolving the triggering
startup errors avoided it, but the underlying diagnostic-path issue remains.

## Troubleshooting

Startup, runtime errors and mod-loading messages are in `Documents/jit-game.log`,
available through Files. The log is replaced on each game startup. Include the
relevant error and your game/mod versions when reporting a problem, after removing
personal paths or identifiers. Device logs, saves and screenshots stay out of Git.
