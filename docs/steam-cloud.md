# Steam Cloud implementation notes

Status: proposed design, not implemented. The current iOS build keeps its saves local.

[SteamKit2](https://github.com/SteamRE/SteamKit) is a candidate for authentication
and CCloud download/upload support. Its authentication and serialization paths
have not been validated in this NativeAOT build. Credentials would need iOS
Keychain storage.

A sync coordinator must distinguish vanilla and modded profile directories.
Automatically choosing an upload or download based on save progress is unsuitable
when the desktop save uses mods and the iPad is running a fresh vanilla profile.

## Proposed first release

1. Validate SteamKit2 authentication and protobuf serialization in an isolated
   NativeAOT device build. Use explicit interactive login; store refresh tokens in
   iOS Keychain. Do not read the desktop Steam client's credentials.
2. Implement account connection and a cloud inventory screen that lists profiles,
   mod status, versions, timestamps and save sizes. Listing must not mutate saves.
3. Offer a preview for downloading a selected compatible vanilla profile to a new
   iOS profile slot. Back up local data first; reject unknown/modded save formats
   and version mismatches rather than trying to repair them automatically.
4. Add explicit upload with a conflict preview. Preserve snapshots of both sides
   and recheck remote hashes immediately before upload. Remote changes since the
   preview must abort the write. Never overwrite a modded profile from vanilla iOS.
5. Test network interruption, expired authentication, concurrent desktop writes,
   account switching, backgrounding, offline saves, and backup restoration before
   considering automatic synchronization.

Start with cloud browsing/download preview because it can establish format and
runtime compatibility without writing to the user's Steam Cloud. Authentication
will require the user's interaction. There is no delivery date for this feature.
