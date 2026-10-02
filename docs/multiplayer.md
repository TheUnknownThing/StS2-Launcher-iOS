# LAN multiplayer

The launcher exposes the game's existing ENet transport through **iOS > LAN
multiplayer**. Standard lobbies support up to four players, direct IPv4 joining,
and Bonjour discovery of other launcher hosts on the same local network.
This implementation has static build and offline policy checks only; a complete
multiplayer session has **not** been tested on devices.

## Host and join

1. Use matching game versions on the same Wi-Fi network. Finish or leave any
   current run and return to the main menu.
2. On the host, open **iOS > LAN multiplayer > Host new run**. The port defaults
   to UDP `33771`. This opens the game's character-selection lobby.
3. On another device, choose **Find nearby games** and allow Local Network access
   when iOS requests it. Select the host, then **Join**. Alternatively, enter the
   host's IPv4 address and port manually.
4. Select characters and ready up using the game's normal lobby controls.

The main menu's multiplayer Host, Load, and Join buttons also open the LAN panel.
Use the game's Back button to leave a lobby. Closing the LAN panel cancels a
pending join; it does not disconnect an established lobby.

Bonjour discovery runs only after selecting **Find nearby games**. Hosting
advertises a service while waiting in a lobby. Advertising stops when the run
starts, the host disconnects, or the app enters the background. Browsing stops
when its panel closes or the app enters the background. A denied Local Network
permission can be changed under **Settings > Privacy & Security > Local Network**.
Guest Wi-Fi/client isolation and host firewalls can prevent connections even when
both devices show the same network name.

## Saves and identity

The host owns the multiplayer run save. **Resume host save** loads a standard
LAN save for the installed game version into the game's load lobby. Validation
does not rename rejected files. Clients join that lobby before the host resumes.
Each launcher installation creates a random persistent ENet identity in
`Documents/lan.cfg`; keep that file to retain a client's place in the host's save.
The local save account remains `Documents/default/1`.

New hosting is blocked when the selected profile already has a multiplayer save.
Use another profile or resume the existing save. Joining another host is also
blocked in a profile containing a hosted multiplayer save. The launcher does not
automatically abandon or replace that save.

The [backup command](saves.md) includes the `default` save tree, including modded
saves. It does not currently include `lan.cfg`; retain that separately when
moving a client installation. Do not publish identity/config files with reports.

## Desktop peers

Ordinary Steam friend lobbies use Steam networking. Signing into the Cloud
browser does not connect the game's Steam networking API.

For a desktop host, the inspected game has a `--fastmp` command-line option that
selects ENet instead of Steam when hosting. In Steam's launch options, use
`--fastmp`, host a standard game, and join its LAN IPv4 address from the iPad on
port `33771`. This desktop workflow is **unverified** and is intended for a later
manual check. Remove the option afterward to return to Steam hosting. Stock
desktop builds do not advertise this launcher's Bonjour service, so use manual
address entry. Their built-in fast-join shortcut targets loopback and is not a
general remote LAN browser.

The game still checks the version string, model database hash, and gameplay mod
list. For example, a desktop Watcher game cannot join a launcher without Watcher.
Use compatible mod selections on both peers; no handshake checks are bypassed.

## Limits and later validation

- Standard runs only; daily/custom LAN flows are not exposed yet.
- No Steam invites/relay, internet matchmaking, host migration, or joining an
  already running game from this menu.
- IPv4 only. Discovery uses `_sts2lan._udp` in the `local.` domain.
- Keep every app in the foreground. iOS suspension can time out a connection;
  the launcher does not provide background hosting or seamless reconnect.
- LAN peers and advertised names are unauthenticated; use a network you trust.

A later device pass should cover two-device play, four-player limits, incompatible
versions/mods, cancellation, occupied ports, denied permission, lobby exit,
save/resume, and suspension. No discovery, connection, or gameplay tests were
performed while implementing this feature.
