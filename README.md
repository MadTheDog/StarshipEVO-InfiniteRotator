# Infinite Rotator for Starship EVO

A BepInEx 5 mod that adds a rotator block which keeps spinning in either direction in **Starship EVO**. It uses the original rotator model and attachment top.

> **Early preview v0.1.0 — basic operation tested in-game in November 2026 on Steam build 26122752.** Broader save/load, collision, overload and multiplayer testing is still outstanding. Back up your saves before trying it.

## What it does

The normal rotator travels between target angles. Infinite Rotator adds a **separate block** that can turn continuously, passing through 360° without returning to the start in the opposite direction.

Place **Infinite Rotator** from the mechanisms category, attach your construction to its top, and open its settings. Choose a speed, choose a direction, and switch on **Run continuously**. Existing rotators and hinges keep their normal behavior.

## Requirements

- Windows x64 and a Steam copy of Starship EVO (app 711980).
- **Steam build 26122752**, using Unity 2019.4.30f1. This preview checks the game assembly and disables itself on a different binary. It is not compatible with every game version.
- **BepInEx 5 x64**, tested with [5.4.23.5](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.5). Download `BepInEx_win_x64_5.4.23.5.zip`. BepInEx 6 and the x86 package are not supported by this preview.

## Install

**You do not need to compile anything. Use the mod ZIP from Releases, not GitHub's green “Code → Download ZIP” button.** The source-code ZIP does not contain the compiled plugin. While this release is a draft, only the repository owner can access its unpublished release assets.

1. **Close the game and back up your saves.** Try a fresh Creative world first. The mod adds a new block ID, so saves and blueprints containing it depend on the mod remaining installed.
2. **Open the game folder.** In Steam, right-click **Starship EVO → Manage → Browse local files**. You should see `Starship EVO.exe`.
3. **Install BepInEx, if needed.** Extract the contents of the **BepInEx 5 x64** ZIP directly into that folder. The `BepInEx` folder, `winhttp.dll` and `doorstop_config.ini` must be next to the game EXE. If BepInEx 5 x64 already works for your other mods, keep that installation.
4. **Run the game once, reach the main menu, then quit.** This generates `BepInEx/config/BepInEx.cfg`.
5. **Set the required SEVO loader option.** Open `BepInEx/config/BepInEx.cfg` in Notepad. Find its existing `[Chainloader]` section and change the existing setting to:

   ```ini
   [Chainloader]
   HideManagerGameObject = true
   ```

   Save the file. Do not create a second `[Chainloader]` section. This is required for this SEVO build to keep the mod loaded.
6. **Install Infinite Rotator.** Download **`InfiniteRotator-v0.1.0.zip`** from this repository's [Releases](../../releases) page. Extract its contents into the game folder, merging its `BepInEx` folder with the one already there. The DLL must end up at:

   ```text
   Starship EVO/
   ├── Starship EVO.exe
   ├── winhttp.dll
   ├── doorstop_config.ini
   └── BepInEx/
       ├── core/
       ├── config/
       │   └── BepInEx.cfg
       └── plugins/
           └── InfiniteRotator/
               └── InfiniteRotator.dll
   ```

   Avoid an extra wrapper folder such as `Starship EVO/InfiniteRotator-v0.1.0/BepInEx/`. You can also create `BepInEx/plugins/InfiniteRotator/` yourself and copy just the DLL there. Install only one copy of the plugin.
7. **Start the game.** In a fresh Creative world, open the mechanisms category and select **Infinite Rotator**. It starts stopped. Attach an arm or another simple construction, open the block settings, and enable **Run continuously**.

The mod ZIP contains the original plugin, documentation, license and checksum only. It does not include BepInEx or any Starship EVO files.

## Check that it loaded

Open **`BepInEx/LogOutput.log`** in the game folder. Look for both:

```text
Loading [Infinite Rotator (draft) 0.1.0]
Registered Infinite Rotator as brick 1000; stock rotator and hinge definitions retained.
```

The first line alone does not prove the block registered. The second line confirms registration; `1000` is the default ID.

## Controls

Interact with the **new Infinite Rotator block** to open its settings.

| Setting | Action |
|---|---|
| **Run continuously** | Start or stop the rotator. New blocks start stopped. |
| **Speed (deg/s, before load slowdown)** | Requested speed from 0–180 degrees/second. Default: 45. Zero stops it. |
| **Reverse direction** | Change the spinning direction without deliberately changing its current angle. |

Speed is reduced by the game's native attached-mass/contact behavior. Native collision and overload checks remain in the movement path; a stop is latched until restarted. These boundary cases need further testing.

Ordinary logic inputs above `0.5` request running; inputs from `0` to `0.5` request stopping. Turret, mech, sequencer and manual-position controllers cannot link to this block. The initial version includes the **upright block only**, without a side-mounted variant.

## Configuration

After the first launch, the plugin creates:

```text
BepInEx/config/local.sevo.infiniterotator.cfg
```

```ini
[Block]
BrickId = 1000
```

Speed, direction and running state belong to each block's settings. `BrickId` is the global ID for the new block definition.

**Keep the ID unchanged once you have saved a world or blueprint using the mod.** If another mod occupies the ID, Infinite Rotator logs a registration error and does not overwrite that block. Resolve the conflict before placing/saving blocks. There is no automatic ID selection or conversion for existing saves. Restart the game after changing the config.

## Troubleshooting

| Problem | What to check |
|---|---|
| No `BepInEx/LogOutput.log` | Check the game folder and x64 BepInEx installation. Its files must sit beside the game EXE. |
| BepInEx works but the block is missing | Check the DLL path, `HideManagerGameObject = true`, and the registration line above. |
| Log says `This draft requires SEVO build 26122752` | The installed game binary is not the tested one. This preview deliberately stays disabled. Wait for an updated mod; do not replace the game's DLLs with files from another version. |
| Log says `invalid or occupied brick ID` | There is an ID conflict or invalid config. The mod has not overwritten the existing block. See the configuration section. |
| Block does not spin | Use the new Infinite Rotator, enable Run continuously, and set a nonzero speed. Native mass/contact limits can reduce or stop movement. |
| Side panel says the save was rejected | Its custom data was not accepted. Keep a backup and report the issue; do not expect the mod to repair it automatically. |

## Uninstall

**Before removing the mod, keep a backup and remove its custom blocks from any worlds you intend to keep using without it.** Blueprints containing the custom block also require the mod. There is no conversion to an ordinary rotator, and loading a modded save without its block definition is not supported.

After preparing your saves, close the game and remove `BepInEx/plugins/InfiniteRotator`. Its configuration file can also be removed. Leave BepInEx installed if other mods need it.

To remove BepInEx entirely, close the game and remove its folder, `winhttp.dll`, `doorstop_config.ini`, and `.doorstop_version` if present. This disables all BepInEx mods. Steam's **Properties → Installed Files → Verify integrity of game files** can check the original installation afterward.

## Game updates and testing limits

- The preview is tied to the inspected game assembly, not just the displayed game version. A game update can disable it.
- Basic use was confirmed by the tester in a disposable game/profile. Save/reload, blueprint round trips, collision/overload edge cases, long sessions and multiplayer are not yet fully validated.
- Hot unloading is unsupported. Restart after installing, removing or changing the plugin.
- This is an unofficial mod, independent of Moonfire Entertainment.

## Building from source

Game and loader DLLs are **not** included. You need your own game, BepInEx 5 reference files and the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

From this repository's root:

```powershell
dotnet build src/InfiniteRotator.csproj -c Release -p:GameDir="D:\SteamLibrary\steamapps\common\Starship EVO"
```

The default game path is `C:\Program Files (x86)\Steam\steamapps\common\Starship EVO`. Loader references default to that game's `BepInEx/core`. If your loader references are elsewhere, also pass `-p:LoaderDir="C:\path\to\BepInEx\core"`.

The plugin is written to `src/bin/Release/netstandard2.0/InfiniteRotator.dll`. Install it at the plugin path shown above. `./Build.ps1 -GameDir "D:\SteamLibrary\steamapps\common\Starship EVO"` also runs the offline policy tests and creates a ZIP with the installation folder layout. The tests run separately from the plugin and are not run when placing a block.

## Reporting issues

Open an issue with your Steam build, mod version, steps to reproduce, and the relevant part of `BepInEx/LogOutput.log`. Review the log for personal information before attaching it. Include whether the problem affects placement, spinning, a saved world or a blueprint.

## Credits

README organization follows the practical approach used by JamMyTo4st's [OreFix](https://github.com/JamMyTo4st/StarshipEVO-OreFix) and [GravAdditions](https://github.com/JamMyTo4st/StarshipEVO-GravAdditions) projects. This is a separate mod and is not maintained or endorsed by JamMyTo4st.

Built with [BepInEx](https://github.com/BepInEx/BepInEx) and Harmony. The original block model remains part of Starship EVO and is loaded from the player's own installation.

## License

[MIT](LICENSE) © 2026 MadTheDog. This license covers this repository's original code and documentation; it does not license Starship EVO or third-party dependencies.
