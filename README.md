# BOXROOM Radio Streams

Internet-radio support for BOXROOM's placeable radios. The current integration is a MelonLoader mod, but the implementation is intentionally split into storage, directory, UI, interaction, and playback layers so it can be moved into BOXROOM itself.

## Features

- Icecast and SHOUTcast MP3 playback through BOXROOM's existing FMOD system
- Positional 3D audio emitted by the selected radio
- Full Radio Browser directory search by station name or genre
- Paginated results, popular-station browsing, and saved favorites
- Favorites stored as readable JSON
- Shift-click entry point that preserves the normal BOXROOM radio interaction
- BOXROOM-styled menus supplied by ModsPanel

## Building the mod

1. Copy `Directory.Build.user.props.example` to `Directory.Build.user.props`.
2. Change `GamePath` to the local BOXROOM installation directory. Alternatively, set the `BOXROOM_GAME_PATH` environment variable.
3. Install `ModsPanel.dll` in `<GamePath>/Mods`.
4. Run `dotnet build Boxroom-RadioStreams.csproj -c Release`.
5. Copy `bin/Release/netstandard2.1/Boxroom_RadioStreams.dll` to `<GamePath>/Mods`.

Local paths and build output are ignored by source control.

## Player use

Shift-click a placed radio to open the station menu. Search by station name, genre, or both. Selecting a result opens actions for playback and favorites. A normal radio click stops the internet stream and returns control to BOXROOM's original radio implementation.

Favorites are stored in `<GamePath>/UserData/RadioStreams/stations.json`. Entries require a display `Name` and a direct HTTP or HTTPS MP3 stream `Url`; a station webpage or playlist file is not a direct stream.

The online directory is provided by the community-run Radio Browser service. Station availability, server compatibility, content, and licensing remain the responsibility of each broadcaster.

## BOXROOM source integration

See `UPSTREAM_INTEGRATION.md` for the ownership boundaries, suggested native classes, and the small pieces that exist only because the current build is a mod.
