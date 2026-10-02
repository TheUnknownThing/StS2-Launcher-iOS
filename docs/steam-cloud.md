# Steam Cloud

The experimental Cloud panel is available from **iOS > Steam Cloud**. It provides
QR login, a file inventory, and downloads to a separate archive. It does not yet
restore a cloud profile into the game or upload iPad progress to Steam.

## Connect

1. Tap **Connect Steam**.
2. Scan the QR code with the Steam mobile app on your phone.
3. Review the **StS2 iOS** login request and approve it in Steam.
4. Select a cloud file to view its path, size, modification time, and path-based
   mod classification.
5. Tap **Download a separate copy** to archive that file on the iPad. Open
   **Files > On My iPad > StS2 iOS > cloud-downloads** to find it.

Common files in each `profileN/saves/` directory:

| File | Contents |
| --- | --- |
| `progress.save` | Profile progress and unlocks |
| `current_run.save` | An ongoing run |
| `prefs.save` | Profile preferences |
| `history/*.run` | Records of past runs |

The file count includes history and files from multiple profiles, including
modded profiles. Downloading a file does not make it available under **Continue**
in the game. Profile import and uploading iPad progress are still pending.

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

```sh
dotnet run --project tests/cloud/CloudTests.csproj -c Release
```

The optional `-- --login-probe` argument requests an anonymous QR challenge from
Steam; it does not sign in or print the QR URL or tokens.
`-- --cm-probe` checks the TLS WebSocket client protocol without account credentials.

## Remaining sync work

- Import a complete, version-compatible vanilla profile into an unused iOS slot,
  with a preview and local backup.
- Add explicit upload with a conflict preview and snapshots of both sides.
- Recheck remote content immediately before writing; never choose a winner from
  progress or timestamp alone, and never overwrite a modded profile from vanilla.
- Exercise account switching, concurrent desktop writes, interrupted transfers,
  backgrounding, and backup restoration before enabling automatic sync.
