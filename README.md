# TruthXR

**A VR port of Blood & Truth for Windows PC, built on shadPS4 and OpenXR. Early development preview, version 0.1.5.**

> **No game files are included or distributed.** TruthXR plays your own dumped copy of the game.

## Status

**Early work in progress: the game now reaches VR, but closes after a few seconds there.** Expect rough edges.

- **Loading:** the game starts and loads. The first start takes several minutes, on a black screen with two arrows at the bottom of the desktop window.
- **VR:** the game switches to VR and hands its first pictures to the headset, at the headset's own field of view.
- **Crash:** on a Meta Quest 3 through Virtual Desktop, the game closes with an error a few seconds after VR starts. The first VR picture is red inside a large black frame. A crash during loading has also been seen once.
- **Controllers:** two tracked controllers stand in for the PlayStation Move wands, with corrected hand order.
- **Picture:** geometry and textures can disappear or pop in and out, including walls and hands. The High and Ultra resolutions have not yet been confirmed to apply.
- **Headsets:** only a Meta Quest 3 has been tried. The other headset choices are untested.

## What you need

- Windows 10 or 11, 64-bit.
- A Vulkan-capable graphics card.
- A VR headset with two tracked controllers and an active OpenXR runtime (for example [Virtual Desktop](https://www.vrdesktop.net/), Meta Quest Link or SteamVR).
- The [Microsoft Visual C++ 2015-2022 x64 runtime](https://learn.microsoft.com/cpp/windows/latest-supported-vc-redist).
- Your own copy of **Blood & Truth, CUSA11108, version 1.00** (without updates):
  - the unpacked game folder (the one with `eboot.bin` and `sce_sys/param.sfo` in it), or
  - a `.pkg` made from a dump of the game. A `.pkg` from the PlayStation Store is encrypted and cannot be used.

## Installing and playing

1. Download `TruthXR-0.1.5.exe` from the [releases page](https://github.com/RandomName729/TruthXR/releases). It is the whole program.
2. Start it. On the first start it installs the emulator into `%LOCALAPPDATA%\TruthXR`. Windows may warn about an unknown publisher: choose **More info**, then **Run anyway**.
3. When it asks, show it your game: the game folder, its `eboot.bin`, or the `.pkg`. A `.pkg` is unpacked once, next to itself (it needs about twice its size in free space); afterwards the launcher offers to delete it.
4. Pick your headset and a picture preset.
5. Connect the headset, wake both controllers, and press **Play**.
6. Wait. The first start loads for several minutes before the headset shows anything.

A newer TruthXR exe updates the install and keeps your settings and saves. They live in `%LOCALAPPDATA%\TruthXR\pc-vr\user` (saves in `home\1000\savedata`, logs in `log`). Keep that folder when you move to another PC.

## Controls

Each of your controllers is one PlayStation Move wand. `Buttons...` in the launcher shows the same list for your controllers.

| PlayStation Move | Touch-style controllers (Quest, Pico, PS VR2 Sense) | Valve Index controllers |
|---|---|---|
| T (trigger) | Trigger | Trigger |
| Move button | Grip | Grip |
| Cross | A / X (lower button) | A (lower button) |
| Circle | B / Y (upper button) | B (upper button) |
| Square | Thumbstick click | Thumbstick click |
| Triangle | Thumbstick up | Thumbstick up |
| Select | Thumbstick down | Thumbstick down |
| Start | Left menu button | Left trackpad, pressed firmly |
| Re-centre the view | Both thumbstick clicks | Both thumbstick clicks |

## Settings

| Setting | What it does |
|---|---|
| `Your headset` | Picks the controllers and the field of view that match it. You can still change both. |
| `Controllers` | Which button guide `Buttons...` shows. The emulator recognises Touch and Index controllers by itself; others need bindings set in SteamVR. |
| `Original Move hand order` | Swaps the wands back to the order the game first had. Only for comparing; leave it off to play. |
| `Low / Medium / High` | Presets for resolution and sharpening. None is lit when you set them by hand. |
| `Resolution` | `Original` lets the game choose (the safest). `High` is 1440 x 1632 an eye, `Ultra` 8 times the original pixel count. Both are experimental. |
| `Sharpening` | Sharpens the picture on its way to the headset, from `Off` to `Strong`. It costs very little. |
| `Field of view` | Degrees across both eyes. `Match headset` fills your headset's whole view (a Quest 3 measures 108). Narrower is sharper with a dark border; wider is softer. |
| `Desktop mirror` | How often the picture on your monitor is updated: 30, 60 or uncapped. |

The headset and controller choices do not change how the emulator reads your controllers; they set the button guide and the field of view.

The field of view follows your headset's measured view once the game has run on it. Until then it uses the headset's advertised width.

## If something does not work

- **The headset stays black and the desktop window shows two arrows:** the game is still loading. The first start takes several minutes; leave it running.
- **The game closes a few seconds after VR starts:** this is the known problem in this version. Open the **Log** tab, press **Copy**, and include it when you report it.
- **"Blood & Truth wasn't found":** choose your game folder, `eboot.bin` or `.pkg` in that window, or press **Change...** later.
- **"This copy can't be used":** it must be CUSA11108 version 1.00 without updates, with `sce_sys/param.sfo` next to `eboot.bin`.
- **A package cannot be unpacked:** only a `.pkg` made from a dump works. Check the free space on that drive. If it mentions a path that is too long, move the package to a short folder such as `C:\Games`.
- **"Another emulator is running":** close the other shadPS4 (for example another port) or let the launcher close it. Two emulators cannot use the headset at once.
- **The launcher does not open at all:** look for `TruthXR-crash.txt` next to the exe or in your temporary folder and include it in your report.
- **Reporting a problem:** open an issue with the Log tab's text (or the files in `%LOCALAPPDATA%\TruthXR\pc-vr\user\log`), your headset, how you connect it, and what you saw.

## Building from source

The emulator builds like upstream shadPS4, with OpenXR on. The launcher builds with the C# compiler that comes with Windows, so nothing extra has to be installed for it.

```
git clone --recurse-submodules https://github.com/RandomName729/TruthXR.git
cd TruthXR
cmake --preset x64-Clang-Release -DENABLE_OPENXR=ON -DENABLE_DISCORD_RPC=OFF -DENABLE_UPDATER=OFF
cmake --build Build\x64-Clang-Release --target shadps4 --parallel 8
powershell -ExecutionPolicy Bypass -File tools\make-launcher.ps1 -Emulator Build\x64-Clang-Release\shadps4.exe
```

Run the commands in the **x64 Native Tools Command Prompt for VS 2022**, with the Visual Studio C++ workload, the Clang tools, CMake, the Vulkan SDK and Python 3 installed. If CMake cannot find Python, add `-DPython3_EXECUTABLE=<path to python.exe>`. `tools\make-release.ps1 -ExePath <shadps4.exe>` makes the release exe and its `BUILD` notes. The base shadPS4 commit is `e9f2c54c207ad6d789d898b3a691aa4b34a59c96`.

## License

TruthXR is licensed GPL-2.0-or-later, like shadPS4. See `LICENSE` and `LICENSES` for the third-party notices. Package unpacking uses PkgTool from [LibOrbisPkg](https://github.com/maxton/LibOrbisPkg) (LGPL-3.0), included unmodified. Parts of the launcher were written with AI help.

TruthXR is an independent fan project. It is not affiliated with, endorsed by or sponsored by Sony Interactive Entertainment or London Studio.

## Thanks

- [shadPS4](https://github.com/shadps4-emu/shadPS4), the emulator this is built on.
- [AstroQuest](https://github.com/bigmak94/AstroQuest), for the VR integration work it inherits.
- [evertec82](https://github.com/evertec82), for the Blood & Truth prototype this started from and the [Farpoint port](https://github.com/evertec82/Farpoint).
- [LibOrbisPkg](https://github.com/maxton/LibOrbisPkg) for PkgTool, and the [OpenXR SDK](https://github.com/KhronosGroup/OpenXR-SDK).
