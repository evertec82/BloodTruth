# Blood & Truth PC VR prototype 0.1.1

A small update to the 0.1.0 development preview. It is still not a finished or reliably playable port.

## What is new

- **The launcher can unpack a game package.** Put a Blood & Truth `.pkg` in a `games` folder next to `Play Blood and Truth.bat`, or enter its path in the launcher. It asks before unpacking, shows progress, and places the game where the launcher expects it (`games/CUSA11108/files/uroot/`). Extracted games work as before.
- **Clear errors for packages that cannot be used:** a file that is not a PlayStation 4 package, a package from the PlayStation Store (encrypted), too little free disk space, and a package whose game description cannot be read.
- Unpacking uses PkgTool from LibOrbisPkg (LGPL-3.0), included unmodified with its license.

## Unchanged

- Only Blood & Truth CUSA11108 version 1.00 is accepted. Its `eboot.bin` is still checked before the game starts.
- No game files, firmware, saves or shader caches are included.
- The emulator executable is unchanged from 0.1.0.

## Known problems

- Geometry and textures can disappear or pop in and out, including walls and hands.
- Loading stalls and crashes remain possible.
- Higher resolutions are experimental.
- Package unpacking works only for packages made from a dump of the game.
- This release has not had a fresh headset gameplay test.

## Installation

See the README. Extract the ZIP into its own folder with a short path, provide your own copy of the game, connect the headset and open `Play Blood and Truth.bat`.
