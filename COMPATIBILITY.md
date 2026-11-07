# Compatibility candidate 0.1.1 — 6 November 2026

## Keel 0.1.2 validation baseline

Infinite Rotator 0.1.2 passed isolated import, installation and startup on **Steam build 26451593 / 26w45a**, Unity **6000.6.6f1**, Windows x64 Mono, with **Uplink 0.1.2** provisioning the published **Keel 0.1.2** runtime. The two installed Keel DLLs matched the checksum-verified SDK and runtime archives. Keel reached resource readiness in audit mode and Infinite Rotator registered at its default brick ID 1000. World placement, rotation, and save reload were outside this startup check. [Keel 0.1.2 experimental prerelease](https://github.com/MadTheDog/Starship-Keel/releases/tag/v0.1.2)

The generic actuator API is separate from the rotator's paired brick definitions and native mechanical lifecycle. Infinite Rotator still uses its existing BepInEx integration, saved IDs and payload format. Keel remains a soft load-order dependency, so the existing mod ZIP can be used with the updated runtime.

## Uplink and Keel integration in 0.1.2

The native Uplink package uses the existing `local.sevo.infiniterotator` identity. The manifest records the repository and compiled asset name. Brick IDs are configurable, so the manifest leaves fixed ID claims empty. The plugin checks both selected slots against the actual game registry before registration.

Keel 0.1.0 supports stock hull registration through its preview API. Infinite Rotator uses its own controller and paired brick definitions. Its resource postfix runs after Keel's hook, and the plugin retains its existing numeric IDs and save format. Keel is an optional BepInEx dependency for load order. The package contains the rotator DLL and Uplink provides the Keel runtime.

Validation on **Steam build 26451593 / 26w45a**, Unity **6000.6.6f1**, used **Uplink 0.1.2** and its bundled **Keel 0.1.0**. The bundled Keel DLLs match the published Keel ZIP. The Unity 6 build compiled cleanly, the rotation/save policy checks passed, and Uplink imported, installed and validated the package in a dedicated profile. In an isolated Uplink launch session, Keel reported resource readiness in audit mode and Infinite Rotator registered at brick ID 1000. World and gameplay acceptance remain pending.

## Original 0.1.1 compatibility work

Target: **Steam build ID 26451593 / 26w45a**, Unity **6000.6.6f1**, `unity6` branch. Confirmed using the installed Steam manifest, Steam's branch cache and the [developer's 26w45a release post](https://github.com/tsunamayo/Starship-EVO/issues/8106). Steam's public and experimental branches still report build 26122752 in the cache checked for this work.

The candidate replaces the original whole-assembly hash rejection with feature-specific admission checks:

- Exact callable signatures, private movement fields, extensible override targets, the native version-2 MessagePack keys, and resource patch signatures.
- Reviewed IL patterns for ten native movement/collision/publication methods. These include real call/callvirt targets and signatures, field accesses, constants, branch targets, local types and the absence of exception regions. A branch bypassing collision work or an unrelated same-named call is rejected. Metadata tokens, assembly names/versions, MVIDs and file hashes are not part of these patterns.
- Runtime discovery of stock rotor meshes/materials and a 1024-entry brick registry before registering custom definitions. Occupied IDs still fail safely. Optional hull-surface discovery is isolated; a missing white surface falls back to the stock top material.

These are conservative guards for the actual native lifecycle the mod uses. A future edit inside a required movement method may require review, even if it ultimately proves compatible. Do not regenerate the reviewed patterns automatically to force an update through. No custom unmanaged/GPU buffer layout is copied or patched by this candidate; native jobs, typed handles and collision scheduling remain owned by the game.

Validation: 72 automated compatibility/mutation checks over the latest installed and preserved older assemblies; four standalone Mono reflection probes (latest, older, changed identity, missing query-release rejection); 100,014 rotation/save policy assertions. Identity tests rewrite actual assembly bytes and re-read them with a changed MVID/name/version/hash. Negative cases include collision bypass, wrong call owner, function-pointer substitution, unexpected exception regions, final overrides, inaccessible methods, changed save keys and missing native allocation/release/publication. The .NET 8 CLR cannot load all Unity game types, so runtime-reader checks use standalone Unity Mono, without initializing the game.

Both the Unity 6 `.NET Standard 2.1` and older `.NET Standard 2.0` source targets compile. The older assembly is Steam build 26122752 / Unity 2019.4.30f1; its year-week release label was not independently reverified here. Compilation and metadata probes are not gameplay acceptance. Placement, both directions, white-top rendering, contact/overload stops, save/reload, blueprint reconstruction, deletion and multiplayer remain acceptance work on 26w45a. Earlier player testing and soak observations do not prove this candidate is leak-free or collision-correct in all setups.

This source update does not publish a candidate binary release or install anything into the Steam game or a running sandbox. Stable brick IDs, custom payload bytes and normal native movement behavior were preserved. The private performance experiments and redundant-phase-write candidate were not added to this public-facing source.

Maintainer checks require existing .NET 8, Mono and Mono.Cecil tools. The project `tests/Compatibility/Compatibility.csproj` accepts `-p:CecilPath=<existing Mono.Cecil.dll>` and two game assembly paths followed by an evidence JSON path. `ReflectionProbe.cs` is a separate Mono executable built with the three contract source files. Keep proprietary game fixtures and resulting reports outside Git.
