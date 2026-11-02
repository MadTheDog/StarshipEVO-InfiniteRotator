# Infinite Rotator v0.1.0 — preview

Adds a separate upright rotator block that continuously spins attached construction, using the original rotator model. Its settings provide run/stop, requested speed and reverse direction.

Basic operation was confirmed in-game on Steam build **26122752** (Unity 2019.4.30f1). This is an early preview; save/load, blueprint, collision/overload and multiplayer acceptance are incomplete.

## Install

1. Close Starship EVO and back up saves. Start with a fresh Creative world.
2. Install **BepInEx 5 x64** in the game folder beside `Starship EVO.exe` if it is not already present. Tested loader: [5.4.23.5](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.5).
3. Run once, then quit. In `BepInEx/config/BepInEx.cfg`, set **`HideManagerGameObject = true`** under the existing `[Chainloader]` section.
4. Download **`InfiniteRotator-v0.1.0.zip`** from this release's Assets and extract it **directly into the game folder**. Final path: `BepInEx/plugins/InfiniteRotator/InfiniteRotator.dll`.
5. Start the game. Look for **Infinite Rotator** in mechanisms, attach a simple arm, and enable **Run continuously** in the new block's settings.

The README contains the full folder layout, troubleshooting and removal steps. Do not use the auto-generated Source code ZIP as the plugin download.

**Important:** default custom brick ID is **1000**. Do not change it for existing modded saves, and do not expect to load worlds/blueprints containing this block after removing the mod. The preview disables itself on an unsupported game binary.

The attached DLL is the exact binary used for the successful local gameplay test. SHA-256:

```text
1268dae53ab89947a7d7bfe192ca7e4c4c6a2c2ca32463717ce75598811dd862
```

Only the original plugin and its documentation/license are included; no game or loader binaries are redistributed.
