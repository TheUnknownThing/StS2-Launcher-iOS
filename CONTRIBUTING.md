# Contributing

Useful contributions include validating another device, improving touch/layout
behavior, reproducing scene-loading stalls and testing a new Steam beta version.
Start with the [architecture](docs/architecture.md) and [build guide](docs/building.md).
Describe larger design changes before implementing them, especially save sync.

## Scope changes clearly

- Keep each commit focused on one behavior or documentation change.
- Use descriptive subjects such as `feat(ios): ...`, `fix(ios): ...`,
  `test(ios): ...` or `docs: ...`.
- Explain the problem, final behavior, verification and remaining limits in a PR.
- Update the support table when a feature becomes tested; do not equate a
  successful AOT compilation with complete gameplay compatibility.
- Preserve upstream copyright notices and dependency provenance.

This repository contains the iOS implementation, tools and documentation.
Keep commits focused; avoid mixing dependency updates, unrelated cleanup and
behavior changes in one patch. Upstream attribution is preserved in the license
notices and Git history.

## Tests without game files

Python report/content tests use synthetic data:

```sh
python3 -m unittest discover -s scripts/ios -p 'test_*.py'
```

Frame-accounting tests require .NET 9 but no Godot, game files or iOS device:

```sh
dotnet run --project tests/ios/FrameWindowTests.csproj -c Release
```

If you used bootstrap, substitute `.tools/dotnet/dotnet` for `dotnet`. The static
weaver can also be built independently with `dotnet build src/STS2Weaver -c Release`.
CI runs these checks without proprietary files or signing credentials.

For runtime changes, build and test on a physical device. Exercise the affected
path and a fresh run, card input, save/relaunch and background/resume as relevant.
Use isolated iOS saves. Describe any device validation you could not perform.

## Repository contents

Keep only source, configuration templates, tests and public documentation in Git.
Never add SDK downloads, native/managed game binaries, content packs, saves,
credentials, certificates, provisioning profiles, raw benchmark data, device logs
or private screenshots. Use `.cache/` for local diagnostics and experiments.

Public benchmark references may include a device model, OS version, methodology,
aggregate metrics and a chart. Remove device names/IDs, signing details and
personal paths. Keep raw JSONL and console output local.

Before committing:

```sh
git diff --check
git diff --cached --stat
python3 scripts/check_repository.py
```

The repository check inspects the Git index, so stage the intended source files
first. It checks local Markdown links and rejects common private/generated file
types and machine-specific identifiers. It is a focused guardrail, not a general
secret-scanner guarantee. Review staged content yourself too.

See [troubleshooting](docs/troubleshooting.md#report-an-issue) for the information
to include in a bug report. Source publication does not grant redistribution
rights for the game or proprietary middleware; see the
[third-party notices](THIRD_PARTY_LICENSES.md).
