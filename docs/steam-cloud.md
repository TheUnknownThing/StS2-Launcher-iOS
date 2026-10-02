# Steam Cloud

The experimental Cloud panel is available from **iOS > Steam Cloud**. It provides
QR login, a file inventory, downloads to a separate archive, and an experimental
vanilla profile import. Uploading iPad progress to Steam is not implemented.

## Connect

1. Tap **Connect Steam**.
2. Scan the QR code with the Steam mobile app on your phone.
3. Review the **StS2 iOS** login request and approve it in Steam.
4. Choose **Vanilla** or **Modded**, then **Saves**, **Run history (.run)**, or
   **Other files**. The menu opens on **Vanilla > Saves**, with progress and unlocks
   first. Each tab shows its file count; history is sorted newest first.
5. Select a file to view its path, size, and modification time in the details pane.
   Import controls are shown only for vanilla profile saves. A preview checks
   compatibility before the import can be confirmed.
6. Tap **Download a separate copy** to archive that file on the iPad. Open
   **Files > On My iPad > StS2 iOS > cloud-downloads** to find it.

Common files in each `profileN/saves/` directory:

| File | Contents |
| --- | --- |
| `progress.save` | Profile progress and unlocks |
| `current_run.save` | An ongoing run |
| `prefs.save` | Profile preferences |
| `history/*.run` | Records of past runs |

The file count includes history and files from multiple profiles, including
modded profiles. Downloading a separate copy does not make it available under
**Continue** in the game; use profile import to create a playable local copy.

The launcher never asks for your Steam password or reads the desktop client's
credentials. The refresh token is stored in an app-specific iOS Keychain item,
accessible only while the device is unlocked and marked for this device only.
**Disconnect** removes it from Keychain. You can also revoke the login through
Steam's authorized-device settings.

Closing the panel cancels its current request. An expired login or network error
leaves active saves unchanged. Gameplay continues to use local saves offline.

## Downloads and modded saves

**Download a separate copy** writes beneath `Documents/cloud-downloads/`, visible
through Files/file sharing. Each download gets its own directory. The code checks
the remote timestamp and content hash and enforces a 16 MiB save size limit.
ZIP-compressed cloud payloads are decoded with a bounded output size.
Download addresses come from Steam's authenticated connection and can point to
third-party storage providers. Downloads require HTTPS and do not follow
redirects.

Paths containing mod metadata are labeled **Modded - archive only**. A
**Vanilla candidate** label describes the directory naming convention; it is not
a guarantee of compatibility. Downloaded JSON can expose a schema/game version,
but compatibility with the installed game still needs validation.

Downloads do not replace `Documents/default`, activate a profile, modify a run,
or write anything to Steam Cloud. This separation is especially important when
the desktop profile has mods and the iPad runs vanilla. Downloaded saves, tokens,
and account details must not be included in public bug reports or commits.

## Import a vanilla profile

1. Close the desktop game and let Steam finish syncing. Keep it closed during import.
2. Return to the iPad game's main menu and open **iOS > Steam Cloud**.
3. Under **Vanilla > Saves**, select **Progress & unlocks**, **Preferences**, or
   **Current run** for the desired profile. The details pane shows the original
   file path and available iPad destination slots.
4. Choose whether to **Include current run when importing**. Turn this off if
   you only want progress and unlocks. If there is no current run, this option is
   automatically turned off and disabled.
5. Tap **Preview Profile N import**. Review the source, destination slot, playtime,
   and current-run details, then tap **Import into Profile N**.
6. Close the panel, tap the profile name at the top left of the main menu, and
   select the imported profile. Use **Continue** if you imported a current run.

Import requires an inactive slot with no saved files. Empty profile directories
created by the game, including empty `saves/history` directories, are eligible.
An active profile, any saved file (including backups), or a symbolic link makes
a slot unavailable. The destination is checked again before import; existing
save files are never replaced. The launcher checks save schemas against
the installed game and uses the game's progress validator. Unknown historical
stats and discovered-item references that the game preserves are allowed, with
a compatibility note in the preview. The original imported bytes remain intact;
unavailable content will not appear in the iPad game. Other repair warnings and
fatal errors stop the import and display the specific reason. Current runs must be regular
single-player runs, pass deserialization, and reference available game models.
These checks reduce compatibility problems; matching schemas alone cannot
guarantee compatibility across different game builds.

The preview downloads progress, optional preferences, and the optional current
run as a group. Cloud metadata is checked again before import; files without
inventory hashes are downloaded again for comparison. Steam does not provide a
transactional snapshot across these requests, so the desktop game must stay
closed. History, desktop settings, multiplayer runs, and `modded/` profiles are
excluded.

A dated snapshot under **Files > StS2 iOS > save-backups** preserves the local
account directory, incoming files, and a checksum manifest before a complete
profile directory is published. The manifest includes private account metadata;
keep it out of public reports. A failed backup prevents import. The new profile
is selected through the game's normal profile picker, and future progress stays
local to the iPad.

## Implementation and validation

QR authentication uses Steam's HTTPS service interface and `System.Text.Json`.
Cloud requests use a logged-in TLS WebSocket connection to a Steam connection
manager on port 443. A small bounded protobuf wire reader handles the required
login/Cloud fields without runtime-generated serializers. Connections are opened
for individual cloud requests and closed afterward. A small
Objective-C++ bridge supplies QR images and Keychain access. Steam's service
interface is unofficial for this launcher and can change independently.

Source tests cover path validation, bounded archive decoding, timestamp
conflicts, checksum validation, and separate snapshot destinations. The HTTPS QR
request and a client-protocol request also run in a macOS NativeAOT test executable.
QR approval, restoring a saved login, authenticated Cloud listing, and downloading
a separate copy have been exercised on the iPad. This validates the browser and
archive flow; it does not establish save compatibility or synchronization.
Profile import has source tests for empty and occupied slots, backup failures,
and changed cloud files. Browser tests cover modded/vanilla separation, history
sorting, and import eligibility. A private Godot probe using the installed game
assembly verified that unknown historical references in a vanilla progress save
survive a load/serialize cycle; source tests distinguish those references from
destructive repairs and fatal errors. Vanilla profile import has been confirmed
on iPad; continued gameplay across every imported save variant remains unverified.
Import is blocked during a LAN session and while resource mods are active. Disable
resource mods and restart before importing vanilla progress.

```sh
dotnet run --project tests/cloud/CloudTests.csproj -c Release
```

The optional `-- --login-probe` argument requests an anonymous QR challenge from
Steam; it does not sign in or print the QR URL or tokens.
`-- --cm-probe` checks the TLS WebSocket client protocol without account credentials.

## Remaining sync work

- Validate profile import and continued gameplay on the iPad with matching game
  builds; broaden compatibility only after testing.
- Add explicit upload with a conflict preview and snapshots of both sides.
- Recheck remote content immediately before writing; never choose a winner from
  progress or timestamp alone, and never overwrite a modded profile from vanilla.
- Exercise account switching, concurrent desktop writes, interrupted transfers,
  backgrounding, and backup restoration before enabling automatic sync.
