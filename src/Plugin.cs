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
        // The runtime loader identifies stock materials by load order, not by texture path.
        internal static readonly object[] RotorMaterialArgs = { "mechanism/rotator_large" };
        internal static readonly object[] TopMaterialArgs = { "Hull/HullA" };
        private static InfiniteRotorBrick registered;
        private static InfiniteRotorTop registeredTop;
        internal static ushort TopId => (ushort)registeredTop.brickID;
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
                if (!ReferenceEquals(Brick.BRICK_INDEX[blockId], registered)
                    || !ReferenceEquals(Brick.BRICK_INDEX[TopId], registeredTop))
                    throw new InvalidOperationException("Infinite Rotator's reserved ID was replaced by another definition.");
                registeredTop.LoadAfterStock();
                registered.LoadAfterStock();
                return;
            }
            if (!SpinPolicy.CanRegister(blockId, blockId >= 0 && blockId < Brick.BRICK_INDEX.Length && Brick.BRICK_INDEX[blockId] != null))
            {
                registrationFailed = true;
                Log.LogError("Infinite Rotator registration disabled: invalid or occupied brick ID " + blockId + ". No definition overwritten.");
                return;
            }
            int topId = blockId == 1001 ? 1023 : 1001;
            if (Brick.BRICK_INDEX[topId] != null)
            {
                registrationFailed = true;
                Log.LogError("Infinite Rotator registration disabled: top brick ID " + topId + " is occupied.");
                return;
            }
            // Native tabs have finished registering by this point. Constructor itself would overwrite an occupied ID.
            registeredTop = new InfiniteRotorTop(topId, TabMechanism.ROTATOR_TOP);
            registeredTop.LoadAfterStock();
            Brick stock = TabMechanism.ROTATOR;
            registered = new InfiniteRotorBrick(blockId, stock)
            {
                scaleUV = stock.scaleUV,
                canPaint = stock.canPaint,
                defaultPaint = stock.defaultPaint,
                PreviewPaint = new Color32(255, 175, 0, 255),
                functionType = BrickFunctionType.mechanism,
                massForUnitShape = 300f,
                childrenBrickID = TopId,
                childrenGridPosition = new Vector3i(0, 32, 0),
                isDynamicGridSize = true,
                minGridSize = 1,
                IsFacingForward = true
            };
            registered.SetCommandArgs(new RotatorSystem.RotatorData
            {
                brickTopID = TopId, range = 360f, axis = Vector3.up, upAxis = -Vector3.right,
                maxWeight = 50000f, speed = .5f, gridPosition = 16, verticalOffset = .5f
            }).Desc("Continuously spins attached construction. Configure run, speed and direction. Experimental mod block.")
              .Tab(CodexTab.BLOCK_MECANISMS);
            registered.LoadAfterStock();
            Log.LogInfo("Registered Infinite Rotator as brick " + blockId + "; stock rotator and hinge definitions retained.");
        }
    }

    internal sealed class InfiniteRotorTop : BrickPipe
    {
        internal static readonly Color32 White = new Color32(255, 255, 255, 255);

        internal InfiniteRotorTop(int id, Brick stock)
            : base(id, "Infinite Rotator top", stock.nameMesh, ResizeMode.NoneBrick,
                ItemStorageType.mechanism, Plugin.TopMaterialArgs)
        {
            canBeDeleted = stock.canBeDeleted;
            canPaint = false;
            functionType = stock.functionType;
            IsCollisionOff = stock.IsCollisionOff;
            scaleUV = stock.scaleUV;
            isDynamicGridSize = stock.isDynamicGridSize;
            minGridSize = stock.minGridSize;
            isMechanismTop = stock.isMechanismTop;
            massForUnitShape = stock.massForUnitShape;
            defaultPaint = PreviewPaint = White;
        }

        protected override void LoadResourcesRuntime() { }

        internal void LoadAfterStock() => base.LoadResourcesRuntime();
    }

    internal sealed class InfiniteRotorBrick : CommandBrick<InfiniteRotatorSystem>
    {
        internal InfiniteRotorBrick(int id, Brick stock)
            : base(id, "Infinite Rotator", stock.nameMesh, ResizeMode.NoneBrick, Plugin.RotorMaterialArgs) { }

        // Even on a subsequent resource load, wait until the stock material exists.
        protected override void LoadResourcesRuntime() { }

        internal void LoadAfterStock() => base.LoadResourcesRuntime();
    }

    [HarmonyPatch(typeof(VoxelMaterial), nameof(VoxelMaterial.CreateFaceIDArrayRuntime))]
    internal static class RotorMaterialPatch
    {
        private static bool Prefix(object[] __0, ref VoxelMaterial[] __result)
        {
            if (ReferenceEquals(__0, Plugin.TopMaterialArgs))
            {
                // White paint alone leaves the stock top black. Use the game's
                // paintable hull surface on the original top mesh.
                __result = new[] { TabHull.HULL_A.VoxelMaterials[0] };
                return false;
            }
            if (!ReferenceEquals(__0, Plugin.RotorMaterialArgs)) return true;

            // Reuse the loaded stock materials without consuming a stock material slot.
            __result = TabMechanism.ROTATOR.VoxelMaterials;
            return false;
        }
    }

    [HarmonyPatch(typeof(Brick), nameof(Brick.LoadAllResourcesRuntime))]
    internal static class RegisterBlockPatch
    {
        private static void Postfix() => Plugin.Register();
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
