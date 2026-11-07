using System;
using System.IO;
using System.Security.Cryptography;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UI.Codex;
using UnityEngine;
using VoxelEngine;

namespace InfiniteRotator
{
    [BepInPlugin(Id, "Infinite Rotator", "0.1.2")]
    [BepInDependency(KeelId, BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Id = "local.sevo.infiniterotator";
        internal const string KeelId = "local.sevo.modplatform";
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
        internal static bool WhiteTopAvailable;
        internal static VoxelMaterial[] WhiteTopMaterials;

        private void Awake()
        {
            Log = Logger;
            blockId = Config.Bind("Block", "BrickId", 1000,
                "Stable custom ID, 1-1023. Never change for an existing world. Occupied IDs disable the block.").Value;
            try
            {
                LogIdentity();
                var failures = GameContract.Validate(new ReflectionContractView(typeof(Brick).Assembly));
                if (failures.Count != 0)
                    throw new NotSupportedException("Required movement/save/resource contracts changed: " + string.Join("; ", failures));
                NativeMovement.Validate();
                harmony = new Harmony(Id);
                // Experimental collision instrumentation is not part of this candidate.
                harmony.CreateClassProcessor(typeof(RotorMaterialPatch)).Patch();
                harmony.CreateClassProcessor(typeof(RegisterBlockPatch)).Patch();
                harmony.CreateClassProcessor(typeof(ContinuousMovementPatch)).Patch();
                Logger.LogInfo("Infinite Rotator draft loaded. Block registration waits for native resource loading. No hot unload support.");
            }
            catch (Exception error)
            {
                harmony?.UnpatchSelf();
                Logger.LogError("Infinite Rotator disabled: " + error);
            }
        }

        private void LogIdentity()
        {
            // Failure to read diagnostics must never reject compatible game contracts.
            try
            {
                var assembly = typeof(Brick).Assembly;
                string hash;
                using (var stream = File.OpenRead(assembly.Location))
                using (var sha = SHA256.Create())
                    hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
                Log.LogInfo("Game diagnostics: Unity=" + Application.unityVersion + " MVID="
                    + assembly.ManifestModule.ModuleVersionId + " SHA256=" + hash);
            }
            catch (Exception error) { Log.LogWarning("Game identity diagnostics unavailable: " + error.Message); }
        }

        internal static void Register()
        {
            if (registrationFailed) return;
            try { RegisterCore(); }
            catch (Exception error)
            {
                registrationFailed = true;
                if (registered != null && ReferenceEquals(Brick.BRICK_INDEX[blockId], registered)) Brick.BRICK_INDEX[blockId] = null;
                if (registeredTop != null && ReferenceEquals(Brick.BRICK_INDEX[registeredTop.brickID], registeredTop))
                    Brick.BRICK_INDEX[registeredTop.brickID] = null;
                Log.LogError("Infinite Rotator registration disabled; stock definitions retained: " + error);
            }
        }

        private static void RegisterCore()
        {
            if (registrationFailed) return;
            // Resolve actual loaded stock resources, not filenames or material slot numbers.
            var stockRotor = TabMechanism.ROTATOR;
            var stockTop = TabMechanism.ROTATOR_TOP;
            if (stockRotor == null || stockTop == null || string.IsNullOrEmpty(stockRotor.nameMesh)
                || string.IsNullOrEmpty(stockTop.nameMesh) || stockRotor.VoxelMaterials == null
                || stockRotor.VoxelMaterials.Length == 0 || stockTop.VoxelMaterials == null
                || stockTop.VoxelMaterials.Length == 0 || Brick.BRICK_INDEX == null || Brick.BRICK_INDEX.Length != 1024)
                throw new NotSupportedException("Stock rotor mesh/material or 10-bit brick registry contract unavailable.");
            WhiteTopMaterials = null;
            try
            {
                var hullType = typeof(Brick).Assembly.GetType("VoxelEngine.TabHull", false);
                var hull = hullType?.GetField("HULL_A", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as Brick;
                if (hull?.VoxelMaterials != null && hull.VoxelMaterials.Length > 0)
                    WhiteTopMaterials = new[] { hull.VoxelMaterials[0] };
            }
            catch (Exception error) { Log.LogWarning("White top surface discovery failed: " + error.Message); }
            WhiteTopAvailable = WhiteTopMaterials != null;
            if (!WhiteTopAvailable) Log.LogWarning("White top surface unavailable; using stock top material. Rotation remains available.");
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

    [HarmonyPatch(typeof(VoxelMaterial), nameof(VoxelMaterial.CreateFaceIDArrayRuntime), new[] { typeof(object[]) })]
    internal static class RotorMaterialPatch
    {
        private static bool Prefix(object[] __0, ref VoxelMaterial[] __result)
        {
            if (ReferenceEquals(__0, Plugin.TopMaterialArgs))
            {
                // White paint alone leaves the stock top black. Use the game's
                // paintable hull surface on the original top mesh.
                __result = Plugin.WhiteTopAvailable ? Plugin.WhiteTopMaterials : TabMechanism.ROTATOR_TOP.VoxelMaterials;
                return false;
            }
            if (!ReferenceEquals(__0, Plugin.RotorMaterialArgs)) return true;

            // Reuse the loaded stock materials without consuming a stock material slot.
            __result = TabMechanism.ROTATOR.VoxelMaterials;
            return false;
        }
    }

    [HarmonyPatch(typeof(Brick), nameof(Brick.LoadAllResourcesRuntime), new Type[0])]
    [HarmonyAfter(Plugin.KeelId)]
    internal static class RegisterBlockPatch
    {
        private static void Postfix() => Plugin.Register();
    }

    [HarmonyPatch(typeof(ChildrenEntitySystem), nameof(ChildrenEntitySystem.UpdateTransform), new Type[0])]
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
