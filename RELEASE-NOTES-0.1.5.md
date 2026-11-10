# TruthXR 0.1.5

An update to the development preview of TruthXR, an experimental Windows OpenXR port of Blood & Truth based on shadPS4. It is still not a finished or reliably playable port: the game now reaches VR, but closes after a few seconds there.

## What is new

- **One-file launcher.** `TruthXR-0.1.5.exe` is the whole download. On its first start it installs the emulator into `%LOCALAPPDATA%\TruthXR`; a newer exe updates that install and keeps your settings, saves and logs.
- **Point it at your game.** Choose the unpacked game folder, its `eboot.bin`, or a `.pkg` made from a dump, anywhere on your PC. A `.pkg` is unpacked once, next to itself, with a progress bar and Cancel, and the launcher offers to delete the package afterwards.
- **Headset and controllers.** Pick your headset (Quest 3 / 3S, Quest 2, Quest Pro, Pico 4, PS VR2, Index, Vive, Windows Mixed Reality or another one). It chooses the matching controllers and field of view, which you can change. `Buttons...` shows which button does what.
- **Picture presets.** Low, Medium and High, or set resolution, sharpening, field of view and the desktop mirror yourself.
- **Field of view slider** in degrees, set to your headset's own view. After the first run it uses the headset's measured view (a Quest 3 measures 108 degrees across).
- **Log tab** with the launcher's own log and the emulator's log as it runs, plus Copy and Open log folder.
- **Clear messages** instead of console errors: game not found, wrong version, packages that cannot be unpacked, an emulator that is already running, and the game closing with an error (with a button to the log).

## Also fixed

- Four settings the old launcher passed that this emulator never reads were removed.
- The emulator's console window is hidden without hiding the desktop mirror window.
- Settings are remembered between starts.

## Known problems

- On a Meta Quest 3 through Virtual Desktop, the game loads, switches to VR and shows its first pictures (red, inside a large black frame), then closes with an error after a few seconds.
- A crash during loading has been seen once.
- The first start takes several minutes to load. Wait on the screen with the two arrows at the bottom.
- Geometry and textures can disappear or pop in and out. Higher resolutions are experimental and not yet confirmed to apply.
- Only a Quest 3 has been tried. The other headset choices use advertised sizes until the game has run on them once.

## Installation

See the README. Start `TruthXR-0.1.5.exe`, show it your own copy of Blood & Truth CUSA11108 version 1.00, choose your headset and a preset, then press Play.

TruthXR is licensed GPL-2.0-or-later and is built on shadPS4. It is not affiliated with Sony or London Studio.
