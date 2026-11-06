# Infinite Rotator v0.1.1 candidate

This compatibility update is an unreleased candidate. It has been compiled and checked against **Steam build 26451593 / 26w45a (Unity 6)**. In-game acceptance of this candidate is still pending. The existing v0.1.0 release and rollback files are preserved.

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

- Candidate v0.1.1: contract and mutation checks against the installed Unity 6 game and the preserved Unity 2019 assembly, plus standalone Mono reflection probes and rotation/save policy tests. These checks do not launch or play the game. See [compatibility details](COMPATIBILITY.md).
- Basic placement, continuous rotation and base painting in a disposable single-player installation. The white-top revision loaded in the same test copy.
- An earlier build ran for about **7½ hours** in a world with four Infinite Rotators. At comparable workloads, frame times stayed near **11.3 ms** **(88,5 FPS)** and memory showed no sustained growth.
- Testing was limited to a few setups. Save and blueprint round trips, multiplayer and high rotor counts still need testing. Native Event Gates linked to the mod can log a lookup error, including when loading a world.

---

## Installation requirements

- Latest candidate target: **Starship EVO 26w45a**, Steam build **26451593**, Unity **6000.6.6f1**, Steam's **unity6** branch. The default build targets .NET Standard 2.1. Runtime acceptance is pending.
- An older-target candidate can be built with `-Framework netstandard2.0` against the preserved Steam build **26122752**, Unity **2019.4.30f1**. Its older year-week label was not independently reverified in this maintenance pass. Earlier gameplay observations below belong to that older game and mod, not to a new Unity 6 acceptance test.
- Future builds are checked against the required native movement/save/resource contracts. A different build ID, MVID or assembly hash alone does not disable the mod. A changed required contract disables registration and leaves vanilla behavior in place. A missing optional white-top surface falls back to the stock appearance.
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

5. For this unreleased candidate, build from source as described below. The build script produces `InfiniteRotator-v0.1.1-candidate-netstandard2.1.zip`. The existing v0.1.0 release predates Unity 6 support; it is not this candidate. The auto-generated source ZIP does not contain a compiled plugin. [Releases](../../releases)
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
- Keep the Unity 6 and older-target packages separate. Use only one plugin DLL in the installation. No collision optimization experiments are included in this compatibility candidate, and no performance gain or leak-free guarantee is claimed.

---

## License

Copyright © 2026 MadTheDog. Released under the [MIT License](LICENSE). The mod loads the original rotator mesh from your game installation.
