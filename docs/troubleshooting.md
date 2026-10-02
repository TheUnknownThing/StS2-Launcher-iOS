# Troubleshooting

Start with the stage that failed. Keep diagnostic output in `.cache/logs/`, which
is excluded from Git. Share only a small, redacted excerpt when reporting a bug.

| Symptom | What to check |
| --- | --- |
| `xcodebuild` cannot find an iOS SDK | Select a full Xcode installation and finish its first-run setup; Command Line Tools alone are insufficient. |
| Signing or provisioning fails | Set your actual team ID and unique bundle ID; confirm the account and device registration in Xcode. |
| iPad is missing/unavailable | Check pairing, trust, Developer Mode and reachability in Xcode, then `xcrun devicectl list devices`. |
| Game assembly is missing | Run `prepare` against the macOS ARM64 `.app`, or supply `--game` for another Steam library. |
| An SDK checksum fails | Inspect the named cached download; retry/resume an incomplete download or remove that archive before downloading again. |
| Existing native checkout has a different revision | Bootstrap deliberately refuses to move it; use a clean checkout or resolve your local dependency changes explicitly. |
| A weaver target is missing | The Steam game may have updated. Confirm the tested game version and do not weaken the all-patches-must-succeed rule. |
| Framework/editor import fails | Rerun `prepare` after bootstrap and check native dependency paths. Regenerate with `build --export-host`. |
| App opens but game content cannot load | Complete `install --content`; `Documents/StS2.pck` must be the full game pack, not `ios/build/StS2.pck`. |
| Old app fails to open after several days | A development provisioning profile may have expired; rebuild and install the same bundle ID. |
| New game version crashes | Rebuild managed code and content from the same installation. NativeAOT success alone does not prove runtime compatibility. |
| UI or card dragging behaves incorrectly | Report the screen, device size, game version, and exact touch gesture. iPhone layouts are not validated. |
| Brief hitch on a new scene/effect | Shader/resource loading may stall. Capture frame timings before changing rendering settings. |
| No audio | Check in-game volume and the iPad's output route. The native host uses the playback audio session, including in Silent Mode; rebuild/install if upgrading from the initial prototype. |
| Cannot right-click a card | Hold a card in a card list for about half a second to open its upgraded preview. Moving your finger cancels the hold; combat hand dragging is unchanged. |
| Steam cannot connect | Retry with a working connection; disconnect and reconnect for an expired session. Cloud downloads are experimental and kept separately from active saves. |
| No available iPad profile slot | Check the slot status under **Vanilla > Saves**. Import accepts an inactive slot with no files, including empty directories created by the game. Profiles containing saves, history, or backups are preserved. |

## Capture useful output

```sh
mkdir -p .cache/logs
python3 scripts/ios/build.py build > .cache/logs/build.log 2>&1
```

To observe a development launch:

```sh
xcrun devicectl device process launch \
  --device YOUR_IPAD_COREDEVICE_ID \
  --console YOUR_BUNDLE_ID
```

NativeAOT currently emits reflection/trimming warnings from the game and its
dependencies. Record the concrete runtime failure and relevant warning when
investigating; do not treat every warning as either fatal or harmless.

For FPS, CPU and memory measurements, use the [performance guide](performance.md).
The benchmark command requires the documented pymobiledevice3 CLI capabilities;
do not assume every installed version exposes the same command layout.

## Report an issue

Include the game version, device model, iPadOS version, Xcode version, repository
commit, failing stage and steps to reproduce. State whether the shader cache was
already populated and whether the save originated on iOS. Redact personal paths,
team IDs, device IDs, account data and tokens from any excerpt. Do not attach
game assemblies, content packs, saves, signing files or raw device logs.
