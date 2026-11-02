using System;
using System.Reflection;
using HarmonyLib;
using VoxelEngine;

namespace InfiniteRotator
{
    // Only these private members are used; startup checks the inspected assembly before patching.
    internal static class NativeMovement
    {
        private static readonly AccessTools.FieldRef<ChildrenEntitySystem, bool> running =
            AccessTools.FieldRefAccess<ChildrenEntitySystem, bool>("isRunning");
        private static readonly AccessTools.FieldRef<ChildrenEntitySystem, float> previous =
            AccessTools.FieldRefAccess<ChildrenEntitySystem, float>("prevState");
        private static readonly MethodInfo stop = AccessTools.Method(typeof(ChildrenEntitySystem), "StopMovement");
        internal static void Validate()
        {
            if (stop == null || running == null || previous == null)
                throw new MissingMemberException("Native mechanism lifecycle does not match this draft.");
        }
        internal static bool IsRunning(ChildrenEntitySystem rotor) => running(rotor);
        internal static float Previous(ChildrenEntitySystem rotor) => previous(rotor);
        internal static void SetPrevious(ChildrenEntitySystem rotor, float value) => previous(rotor) = value;
        internal static void Stop(ChildrenEntitySystem rotor)
        {
            if (IsRunning(rotor)) stop.Invoke(rotor, null);
        }
    }
}
