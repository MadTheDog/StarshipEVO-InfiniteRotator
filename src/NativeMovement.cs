using System;
using System.Reflection;
using HarmonyLib;
using VoxelEngine;

namespace InfiniteRotator
{
    // Only bind private accessors after the shared lifecycle contract has passed.
    internal static class NativeMovement
    {
        private static AccessTools.FieldRef<ChildrenEntitySystem, bool> running;
        private static AccessTools.FieldRef<ChildrenEntitySystem, float> previous;
        private static MethodInfo stop;
        internal static void Validate()
        {
            running = AccessTools.FieldRefAccess<ChildrenEntitySystem, bool>("isRunning");
            previous = AccessTools.FieldRefAccess<ChildrenEntitySystem, float>("prevState");
            stop = typeof(ChildrenEntitySystem).GetMethod("StopMovement", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null, Type.EmptyTypes, null);
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
