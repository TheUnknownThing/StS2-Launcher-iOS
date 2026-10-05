# iOS host

This directory contains the Godot shell, native bridge, Mono JIT runtime host and
startup gate. Start with [build and install](../docs/building.md), then read
[JIT activation](../docs/jit.md) and [architecture](../docs/architecture.md).

`config.example.json` is the public configuration template. Keep signing/device
settings in ignored `config.local.json`. All tools use its exact `bundle_id`.
Native dependencies in `addons/` and `.godot/` caches are generated locally;
exported projects and built apps live under `.cache/` at the repository root.

Use `python3 scripts/ios/build.py build` from the repository root to regenerate
the host and build the JIT app. Do not edit generated Xcode output.
