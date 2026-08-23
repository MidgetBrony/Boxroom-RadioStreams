# BOXROOM integration guide

## Code that can move into BOXROOM

- `StationStore.cs`: station/favorite model and JSON persistence. Replace `MelonEnvironment.UserDataDirectory` with BOXROOM's user-data service.
- `StationMenu.cs`: Radio Browser queries, filtering, paging, search state, favorite actions, and station selection. Replace `ModsUi` calls with a native BOXROOM screen or presenter.
- `StreamPlayer.cs`: FMOD non-blocking stream creation, readiness polling, positional channel configuration, cleanup, and error reporting. Replace `MelonCoroutines.Start` and toasts with BOXROOM-owned coroutine/UI services.

## Mod-only adapters to remove

- `Core.cs` is only the MelonLoader entry point.
- `RadioPatch.cs` uses Harmony because the shipped `Interactable_Radio` cannot currently open an external station picker. In native code, route the alternate/modified interaction directly to the station screen and call the stream service from the selected station action.
- The reflective call to private `Interactable_Radio.TurnOff` in `StreamPlayer.Play` should become a normal internal/public radio method or a shared playback-mode transition.

## Suggested native ownership

1. `InternetRadioDirectoryService` performs Radio Browser requests and returns validated station records.
2. `InternetRadioFavorites` reads and writes the favorite list.
3. `InternetRadioPlayer` owns the FMOD `Sound` and `Channel`, and exposes `Play`, `Stop`, status, and errors.
4. `InternetRadioMenu` owns search fields, paging, favorite actions, and focus.
5. `Interactable_Radio` owns the selected playback mode and stops any previous radio before starting a new one.

The native radio save model should eventually store the active station identity/URL alongside `playingAlbumId` if internet streams are expected to resume after loading a room. The mod deliberately does not alter BOXROOM's save schema.

## Important playback rule

FMOD non-blocking streams must reach `OPENSTATE.READY` before `playSound` is called. A nonzero buffer percentage while `OPENSTATE.BUFFERING` is not sufficient and returns `ERR_NOTREADY`.

Some directory entries return `ERR_HTTP` because the broadcaster rejects FMOD, uses unsupported TLS, or has disappeared since its last directory check. Treat this as a station-level failure and keep the browser/player usable.
