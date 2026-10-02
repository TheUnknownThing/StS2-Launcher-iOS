# Save management

The iOS build creates local saves in its own app sandbox. It does not authenticate
with Steam, import desktop files, or upload to Steam Cloud. A Steam save created
with mods may reference content that is not present in the vanilla iOS build.

## Keep progress when updating

Use `build.py install` with the same bundle ID. This replaces the application
while preserving its data container. Changing the bundle ID creates a separate
app; uninstalling removes that app's local saves and game content.

## Back up iOS saves

Finish the current action and return to the main menu before copying files, so
the game is not writing a save during the backup. The app enables file sharing;
its documents are accessible through Apple device file-sharing tools.

For a development installation, this command copies the local save tree to the
Mac. Replace the two identifiers with values from your local config:

```sh
mkdir -p .cache/save-backups
xcrun devicectl device copy from \
  --device YOUR_IPAD_COREDEVICE_ID \
  --domain-type appDataContainer \
  --domain-identifier YOUR_BUNDLE_ID \
  --source Documents/default \
  --destination .cache/save-backups/default
```

Use a new destination for each backup and retain a copy outside the working
directory. In the tested build, profile saves live beneath
`Documents/default/1/profile1/saves/`; other profiles or future versions may use
different paths. `Documents/StS2.pck` is game content, not a save backup.

The backup command reads from the device. Automatic restore and desktop-save
migration are not provided. Before manually restoring a backup, stop the game,
keep another copy of current data, and use the same game version. Do not replace
Steam saves with a test iOS profile.

## Cloud and mods

Steam Cloud remains a roadmap item. A future implementation needs to distinguish
vanilla and modded profiles explicitly. See [Steam Cloud design notes](steam-cloud.md)
for the proposed account, compatibility and conflict workflow.

Do not include saves or backups in pull requests or public bug reports. A short
description of the profile type, game version and reproduction steps is usually
enough to start investigation.
