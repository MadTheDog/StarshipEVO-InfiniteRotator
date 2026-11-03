# Infinite Rotator v0.1.0

---

## Short introduction

Adds a separate rotator to Starship EVO that keeps spinning in either direction. It uses the original rotator shape, with a paintable base and a white spinning top to distinguish it from the black vanilla top.

---

## What it does

Find **Infinite Rotator** under **Mechanisms**. Place it, attach a build to the top, and interact with the base to open its settings.

| Setting | What changes |
|---|---|
| Run continuously | Starts or stops rotation. New blocks start stopped. |
| Speed | Sets the requested speed from 0 to 180 degrees per second. Default is 45. |
| Reverse direction | Spins in the opposite direction from the current angle. |

The game reduces speed under load and can stop the rotator on contact or overload. Restart it after clearing the obstruction. Player testing confirmed collision stops, including collisions between different grids.

### What was tested

- Basic placement, continuous rotation and base painting in a disposable single-player installation. The white-top revision loaded in the same test copy.
- An earlier build ran for about **7½ hours** in a world with four Infinite Rotators. At comparable workloads, frame times stayed near **11.3 ms** **(88,5 FPS)** and memory showed no sustained growth. Some removed rotor references remained alive after stock machinery unloaded. Cleanup still needs a controlled test.
- Testing was limited to a few setups. Save and blueprint round trips, multiplayer and high rotor counts still need testing. Native Event Gates linked to the mod can log a lookup error, including when loading a world.

---

## Installation requirements

- **Starship EVO Early Access 26w43a**, Steam build **26122752**, Unity **2019.4.30f1**. This is the tested and supported game build. The plugin checks the game assembly at startup and disables itself on other builds.
- **64-bit Windows** and **BepInEx 5 for Windows x64**. Tested with [BepInEx 5.4.23.5](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.5).

---

## How to install

1. Close Starship EVO and back up any world you plan to use. Try a fresh Creative world first.
2. In Steam, open **Starship EVO → Manage → Browse local files**. This is the folder containing `Starship EVO.exe`.
3. Extract BepInEx 5 for Windows x64 beside the executable. If BepInEx 5 already works, keep your installation. Launch the game once, then close it to generate the configuration.
4. Open `BepInEx/config/BepInEx.cfg` and set the existing `HideManagerGameObject` key under `[Chainloader]` to `true`.

   ```ini
   [Chainloader]
   HideManagerGameObject = true
   ```

5. Download **InfiniteRotator-v0.1.0.zip** from the release's **Assets**. The auto-generated source ZIP does not contain the plugin. [Releases](../../releases)
6. Extract the ZIP's `BepInEx` folder into the game folder. The files should land here.

   ```text
   Starship EVO/
   ├── Starship EVO.exe
   └── BepInEx/
       └── plugins/
           └── InfiniteRotator/
               └── InfiniteRotator.dll
   ```

7. Launch the game. Look for **Infinite Rotator** under **Mechanisms**. If it is missing, check the DLL path, the loader option above and `BepInEx/LogOutput.log` for the registration message.

To update, close the game and replace `BepInEx/plugins/InfiniteRotator/InfiniteRotator.dll`. Keep a single copy of the DLL installed.

## How to uninstall

1. Back up your world and remove the Infinite Rotator blocks from any saves or blueprints you want to keep using without the mod.
2. Close the game and delete `BepInEx/plugins/InfiniteRotator/`.
3. Optionally delete `BepInEx/config/local.sevo.infiniterotator.cfg`.

---

## Notes

- Saves and blueprints containing Infinite Rotators depend on the mod. The base defaults to brick ID **1000** and the white top uses **1001**. If the base is configured as 1001, the top uses **1023**. Keep the configured ID stable across saves. Occupied IDs cause registration to stop.
- Existing Infinite Rotator tops upgrade on load. Saves written with a white top need this revision or newer.
- The initial release adds the upright rotator. Vanilla rotators and hinges retain their original controls.
- Unofficial mod, unaffiliated with Moonfire Entertainment.
- To build from source, install the .NET 8 SDK and run `./Build.ps1 -GameDir "<your Starship EVO folder>"`. The script uses BepInEx references from that installation and packages the DLL. Pass `-LoaderDir` if your loader references live elsewhere.

---

## License

Copyright © 2026 MadTheDog. Released under the [MIT License](LICENSE). The mod loads the original rotator mesh from your game installation.
