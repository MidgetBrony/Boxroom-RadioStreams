# BOXROOM Radio Streams 1.2.3

- Rejects malformed stream addresses before they reach FMOD.
- Canonicalizes saved and directory-provided HTTP(S) stream addresses.
- Adds a 15-second timeout to Radio Browser directory requests.
- Defers releasing a non-blocking FMOD sound until it reports `READY` or `ERROR`, preventing a native crash when a connection times out while still opening.

The release build completed with zero warnings and zero errors. This update addresses the native FMOD/Unity crash reported after a malformed or unavailable stream timed out.
