# Save management

The iOS build creates local saves in its own app sandbox. Steam Cloud downloads
are kept separately and do not replace active saves. A Steam save created
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
Mac using the device and bundle ID in `ios/config.local.json`:

```sh
python3 scripts/ios/backup.py
```

Each backup gets a new dated directory beneath `.cache/save-backups/`, with a
SHA-256 manifest for the copied files. An interrupted or empty copy is not
published as a completed backup. Existing backup directories are never replaced.
The command copies `Documents/default`, including profile settings and saves;
it excludes game content, Steam login credentials, and benchmark recordings.
Keep the game at the main menu throughout the copy; this is not a transactional
snapshot of an actively running game.

To choose a different new directory:

```sh
python3 scripts/ios/backup.py --destination /path/to/new-backup
```

The equivalent manual command is:

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

The experimental [Steam Cloud browser](steam-cloud.md) labels modded paths and
downloads files into `Documents/cloud-downloads/`. Those copies are archives,
not active profiles. **Preview profile import** can prepare compatible vanilla
progress and an optional current run for an unused iPad profile slot. Confirmation
creates a local backup under `Documents/save-backups/` before publishing the new
profile. Select it using the game's profile picker. See the Cloud guide for
compatibility checks and restrictions. Automatic synchronization and uploads are
not enabled. Credentials are stored in the iOS Keychain.

Do not include saves or backups in pull requests or public bug reports. A short
description of the profile type, game version and reproduction steps is usually
enough to start investigation.
