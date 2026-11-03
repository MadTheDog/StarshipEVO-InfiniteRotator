# Infinite Rotator v0.1.0

Adds an upright, continuously spinning rotator with a paintable base and a white spinning top. Player testing covered collision stops, including contact between different grids.

**Supported and tested on Starship EVO Early Access 26w43a**, Steam build **26122752**, Unity **2019.4.30f1**, with BepInEx **5.4.23.5 for Windows x64**.

Limited single-player testing. An earlier build showed no sustained memory growth or slowdown during a roughly 7½-hour soak at comparable workloads. Cleanup of removed rotor references, broader save and blueprint testing, and multiplayer still need verification. Linked native Event Gates can log an error.

Back up your worlds. Extract the mod ZIP into the game folder so the DLL lands at `BepInEx/plugins/InfiniteRotator/InfiniteRotator.dll`. Set `HideManagerGameObject = true` under `[Chainloader]` in `BepInEx/config/BepInEx.cfg`. See the [README](https://github.com/MadTheDog/StarshipEVO-InfiniteRotator#how-to-install) for the full install steps and save compatibility notes.
