using System;
using System.IO;
using System.Security.Cryptography;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UI.Codex;
using UnityEngine;
using VoxelEngine;

namespace InfiniteRotator
{
    [BepInPlugin(Id, "Infinite Rotator (draft)", "0.1.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Id = "local.sevo.infiniterotator";
        private const string SupportedHash = "2F5C2A843EF1698DC814FC096E2F6C9DF44C126F24601269CB294D0C0B0867AF";
        internal static ManualLogSource Log;
        private static int blockId;
        private static Brick registered;
        private static bool registrationFailed;
        private Harmony harmony;

        private void Awake()
        {
            Log = Logger;
            blockId = Config.Bind("Block", "BrickId", 1000,
                "Stable custom ID, 1-1023. Never change for an existing world. Occupied IDs disable the block.").Value;
            try
            {
                string hash;
                using (var stream = File.OpenRead(typeof(Brick).Assembly.Location))
                using (var sha = SHA256.Create())
                    hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
                if (hash != SupportedHash)
                    throw new NotSupportedException("This draft requires SEVO build 26122752 (inspected Assembly-CSharp hash).");
                NativeMovement.Validate();
                harmony = new Harmony(Id);
                harmony.PatchAll(typeof(Plugin).Assembly);
                Logger.LogInfo("Infinite Rotator draft loaded. Block registration waits for native resource loading. No hot unload support.");
            }
            catch (Exception error)
            {
                harmony?.UnpatchSelf();
                Logger.LogError("Infinite Rotator disabled: " + error);
            }
        }

        internal static void Register()
        {
            if (registrationFailed) return;
            if (registered != null)
            {
                if (!ReferenceEquals(Brick.BRICK_INDEX[blockId], registered))
                    throw new InvalidOperationException("Infinite Rotator's reserved ID was replaced by another definition.");
                return;
            }
            if (!SpinPolicy.CanRegister(blockId, blockId >= 0 && blockId < Brick.BRICK_INDEX.Length && Brick.BRICK_INDEX[blockId] != null))
            {
                registrationFailed = true;
                Log.LogError("Infinite Rotator registration disabled: invalid or occupied brick ID " + blockId + ". No definition overwritten.");
                return;
            }
            // Native tabs have finished registering by this point. Constructor itself would overwrite an occupied ID.
            registered = new CommandBrick<InfiniteRotatorSystem>(blockId, "Infinite Rotator", "mechanism/rotator",
                ResizeMode.NoneBrick, "mechanism/rotator_large")
            {
                scaleUV = 2f,
                PreviewPaint = new Color32(255, 175, 0, 255),
                functionType = BrickFunctionType.mechanism,
                massForUnitShape = 300f,
                childrenBrickID = 26,
                childrenGridPosition = new Vector3i(0, 32, 0),
                isDynamicGridSize = true,
                minGridSize = 1,
                IsFacingForward = true
            }.SetCommandArgs(new RotatorSystem.RotatorData
            {
                brickTopID = 26, range = 360f, axis = Vector3.up, upAxis = -Vector3.right,
                maxWeight = 50000f, speed = .5f, gridPosition = 16, verticalOffset = .5f
            }).Desc("Continuously spins attached construction. Configure run, speed and direction. Experimental mod block.")
              .Tab(CodexTab.BLOCK_MECANISMS);
            Log.LogInfo("Registered Infinite Rotator as brick " + blockId + "; stock rotator and hinge definitions retained.");
        }
    }

    [HarmonyPatch(typeof(Brick), nameof(Brick.LoadAllResourcesRuntime))]
    internal static class RegisterBlockPatch
    {
        private static void Prefix() => Plugin.Register();
    }

    [HarmonyPatch(typeof(ChildrenEntitySystem), nameof(ChildrenEntitySystem.UpdateTransform))]
    internal static class ContinuousMovementPatch
    {
        private static bool Prefix(ChildrenEntitySystem __instance)
            => !(__instance is InfiniteRotatorSystem rotor) || rotor.BeforeNativeUpdate();

        private static void Postfix(ChildrenEntitySystem __instance)
        {
            if (__instance is InfiniteRotatorSystem rotor) rotor.AfterNativeUpdate();
        }
    }
}
