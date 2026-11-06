using System;
using System.Collections.Generic;
using System.Linq;

namespace InfiniteRotator
{
    // Shared by the runtime reflection reader and the offline Cecil verifier.
    // Build identity is diagnostic data; it is deliberately not an input here.
    internal sealed class ContractType
    {
        internal string Name, BaseType;
        internal bool Sealed;
        internal readonly Dictionary<string, ContractField> Fields = new Dictionary<string, ContractField>();
        internal readonly List<ContractMethod> Methods = new List<ContractMethod>();
        internal readonly Dictionary<string, string> SaveKeys = new Dictionary<string, string>();
    }
    internal sealed class ContractField
    {
        internal string Type;
        internal bool Static, ReadOnly;
        internal object Constant;
    }
    internal sealed class ContractMethod
    {
        internal string Name, Returns;
        internal string[] Parameters;
        internal bool Static, Virtual, Final, Accessible;
        internal string Flow;
        internal readonly List<string> Operations = new List<string>();
    }
    internal interface IContractView { ContractType Read(string name); }

    internal static class GameContract
    {
        internal const string Children = "VoxelEngine.ChildrenEntitySystem";
        internal const string Rotor = "VoxelEngine.RotatorSystem";
        internal static readonly HashSet<string> RequiredMethods = new HashSet<string>(new[] {
            "UpdateTransform", "StopMovement", "Propagate", "SetStateFromShipyard", "ForceState", "get_saveData", "set_saveData",
            "OnSaveLoaded", "GetCustomData", "GetMoveSpeed", "OnInitialize", "InitChildrenTransform", "OnLoadingSequenceFinished",
            "Activate", "OnGenericParamsUpdated", "get_OverTextIndirect", "get_CanBeLinkSource", "CanBeLinkTargetFromCommand",
            "GetSidePanelInfoText", "LoadAllResourcesRuntime", "LoadResourcesRuntime", "CreateFaceIDArrayRuntime", "CheckForCollision", "RestoreStateIfCollision",
            "StartMoving", "ApplyTransform", "ComputeStateFromAngle", "SetJobData" });
        internal static bool NeedsField(string owner, string name) => (owner == Children &&
            (name == "isRunning" || name == "prevState" || name == "queryId")) || (owner == Rotor && name == "CURRENT_VERSION");
        internal static List<string> Validate(IContractView view)
        {
            var errors = new List<string>();
            if (NativeLifecyclePatterns.Expected.Count != 10) errors.Add("Reviewed movement contract patterns are incomplete");
            Action<string, string, string, bool> field = (owner, name, type, isStatic) =>
            {
                ContractField found;
                var t = view.Read(owner);
                if (t == null || !t.Fields.TryGetValue(name, out found) || found.Type != type || found.Static != isStatic || found.ReadOnly)
                    errors.Add(owner + "." + name + " requires " + (isStatic ? "static " : "instance ") + type);
            };
            field(Children, "isRunning", "System.Boolean", false);
            field(Children, "prevState", "System.Single", false);
            field(Children, "queryId", "System.Int32", false);
            Method(view, errors, Rotor, "get_saveData", Rotor + "/RotatorSaveData", false);
            Method(view, errors, Rotor, "set_saveData", "System.Void", false, false, Rotor + "/RotatorSaveData");
            Method(view, errors, Children, "UpdateTransform", "System.Void", false);
            Method(view, errors, Children, "StopMovement", "System.Void", false);
            Method(view, errors, Children, "Propagate", "System.Void", false, true);
            Method(view, errors, Children, "SetStateFromShipyard", "System.Void", false, false, "System.Single");
            Method(view, errors, Rotor, "ForceState", "System.Void", false, false, "System.Single");
            Method(view, errors, Rotor, "OnSaveLoaded", "System.Void", false, true, "System.Byte[]", "System.Boolean");
            Method(view, errors, Rotor, "GetCustomData", "System.Byte[]", false, true, "System.Boolean");
            Method(view, errors, Rotor, "GetMoveSpeed", "System.Single", false, true);
            Override(view, errors, "OnInitialize", "System.Void", "VoxelEngine.BrickIds", "System.Boolean", "System.Object[]");
            Override(view, errors, "InitChildrenTransform", "System.Void");
            Override(view, errors, "OnLoadingSequenceFinished", "System.Void");
            Override(view, errors, "Activate", "System.Void", "System.Boolean", "Game.PlayerCommandManager", "System.Single");
            Override(view, errors, "OnGenericParamsUpdated", "System.Void", "System.Object", "System.Boolean");
            Override(view, errors, "get_OverTextIndirect", "System.String");
            Override(view, errors, "get_CanBeLinkSource", "System.Boolean");
            Override(view, errors, "CanBeLinkTargetFromCommand", "System.Boolean", "VoxelEngine.ICommand");
            Override(view, errors, "GetSidePanelInfoText", "System.Void", "System.Text.StringBuilder");
            Method(view, errors, "VoxelEngine.Brick", "LoadAllResourcesRuntime", "System.Void", true);
            Method(view, errors, "VoxelEngine.Brick", "LoadResourcesRuntime", "System.Void", false, true);
            OverrideFrom(view, errors, "VoxelEngine.BrickPipe", "LoadResourcesRuntime", "System.Void");
            OverrideFrom(view, errors, "CommandBrick`1", "LoadResourcesRuntime", "System.Void");
            Method(view, errors, "VoxelEngine.VoxelMaterial", "CreateFaceIDArrayRuntime", "VoxelEngine.VoxelMaterial[]", true, false, "System.Object[]");

            Sequence(view, errors, Children, "UpdateTransform", "call:CheckForCollision", "call:RestoreStateIfCollision",
                "field:isRunning", "field:prevState", "call:GetMoveSpeed", "call:StopMovement", "call:MoveTowards",
                "call:ApplyTransform", "call:PushTransformToSkyTransform", "call:get_StateDataPointer", "call:Set");
            Sequence(view, errors, Children, "CheckForCollision", "field:queries", "field:queryId", "field:maxPenetration", "field:isColliding");
            Sequence(view, errors, Children, "RestoreStateIfCollision", "field:isColliding", "field:prevState",
                "call:ApplyTransform", "call:PushTransformToSkyTransform", "call:StopMovement");
            Sequence(view, errors, Children, "StartMoving", "field:isRunning", "call:SetQuery", "field:childrenSystems", "call:Add");
            Sequence(view, errors, Children, "StopMovement", "field:isRunning", "call:stop", "field:childrenSystems",
                "call:Remove", "call:TriggerOnEntityMoveFinished", "call:ReleaseId");
            Sequence(view, errors, Children, "Propagate", "field:isRunning", "call:get_State", "call:StartMoving");
            Sequence(view, errors, Rotor, "ApplyTransform", "call:ApplyRotationLocal", "call:set_localPosition",
                "call:set_localRotation", "call:Set", "call:Set");
            Sequence(view, errors, Rotor, "ForceState", "call:Set", "call:Set");
            Sequence(view, errors, Rotor, "ComputeStateFromAngle", "field:isFullCircle", "call:Mod");
            Sequence(view, errors, Rotor, "SetJobData", "field:RotatorDatas", "field:angleId", "call:set_Item");

            var rotor = view.Read(Rotor);
            ContractField version;
            if (rotor == null || rotor.BaseType != Children || rotor.Sealed) errors.Add("RotatorSystem must inherit ChildrenEntitySystem and remain extensible");
            if (rotor == null || !rotor.Fields.TryGetValue("CURRENT_VERSION", out version) || !Equals(version.Constant, 2))
                errors.Add("Native rotator save writer must use schema version 2");
            var saved = view.Read(Rotor + "/RotatorSaveData");
            string[] members = { "restingPosition:System.Single", "velocity:System.Single", "minimumAngle:System.Single",
                "maximumAngle:System.Single", "version:System.Int32", "axisOffsetLegacy:UnityEngine.Vector3", "limbType:System.Int32",
                "isRightSide:System.Boolean", "customJoinLabel:System.String", "emitSound:System.Boolean", "isFoot:System.Boolean",
                "squatWeight:System.Single", "isReversed:System.Boolean", "customIsReversed:System.Boolean" };
            for (int i = 0; i < members.Length; i++)
            {
                string actual;
                if (saved == null || !saved.SaveKeys.TryGetValue((i + 1).ToString(), out actual) || actual != members[i])
                    errors.Add("Native MessagePack key " + (i + 1) + " requires " + members[i]);
            }
            foreach (var requirement in NativeLifecyclePatterns.Expected)
            {
                int split = requirement.Key.LastIndexOf("::", StringComparison.Ordinal);
                var owner = view.Read(requirement.Key.Substring(0, split));
                var candidates = owner?.Methods.Where(m => m.Name == requirement.Key.Substring(split + 2)).ToArray();
                if (candidates == null || candidates.Length != 1 || !requirement.Value.Contains(candidates[0].Flow))
                    errors.Add(requirement.Key + " reviewed lifecycle control flow or operand contract changed");
            }
            return errors;
        }

        private static void Override(IContractView view, List<string> errors, string name, string returns, params string[] parameters)
            => OverrideFrom(view, errors, Rotor, name, returns, parameters);

        private static void OverrideFrom(IContractView view, List<string> errors, string owner, string name, string returns, params string[] parameters)
        {
            var type = view.Read(owner);
            if (type != null && type.Sealed) errors.Add(owner + " must remain extensible");
            while (type != null)
            {
                var found = type.Methods.Where(m => m.Name == name && m.Parameters.SequenceEqual(parameters)).ToArray();
                if (found.Length > 0)
                {
                    if (found.Length != 1 || found[0].Returns != returns || !found[0].Virtual || found[0].Final
                        || !found[0].Accessible || found[0].Static) errors.Add(name + " must remain an accessible, non-final virtual override target");
                    return;
                }
                type = type.BaseType == null ? null : view.Read(type.BaseType);
            }
            errors.Add(owner + "." + name + " override signature unavailable");
        }

        private static void Method(IContractView view, List<string> errors, string owner, string name, string returns,
            bool isStatic, bool isVirtual = false, params string[] parameters)
        {
            var t = view.Read(owner);
            if (t == null || t.Methods.Count(m => m.Name == name && m.Returns == returns && m.Static == isStatic
                && (m.Accessible || (owner == Children && name == "StopMovement"))
                && (!isVirtual || (m.Virtual && !m.Final)) && m.Parameters.SequenceEqual(parameters)) != 1)
                errors.Add(owner + "." + name + " required signature missing or ambiguous");
        }
        private static void Sequence(IContractView view, List<string> errors, string owner, string name, params string[] operations)
        {
            var t = view.Read(owner);
            var methods = t == null ? new List<ContractMethod>() : t.Methods.Where(m => m.Name == name).ToList();
            if (methods.Count != 1) { errors.Add(owner + "." + name + " missing or ambiguous lifecycle body"); return; }
            int next = 0;
            foreach (string op in methods[0].Operations)
                if (next < operations.Length && op == operations[next]) next++;
            if (next != operations.Length) errors.Add(owner + "." + name + " lifecycle pattern missing " + operations[next]);
        }
    }
}
