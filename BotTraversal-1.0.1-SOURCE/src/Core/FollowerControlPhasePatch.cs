using System;
using EFT;
using HarmonyLib;

namespace BotTraversal.Core
{
    // Observe native physics separately from rendered-frame updates. This patch
    // never suppresses a tick or de-duplicates physics by Time.frameCount.
    internal static class FollowerControlPhasePatch
    {
        internal static void Apply(Harmony harmony)
        {
            var method = AccessTools.Method(typeof(BotMover), "ManualFixedUpdate");
            if (method == null)
                throw new MissingMethodException("BotMover.ManualFixedUpdate");
            harmony.Patch(method, prefix: new HarmonyMethod(AccessTools.Method(typeof(FollowerControlPhasePatch), nameof(Prefix))) { priority = Priority.First }, finalizer: new HarmonyMethod(AccessTools.Method(typeof(FollowerControlPhasePatch), nameof(Finalizer))) { priority = Priority.Last });
        }

        private static void Prefix(out FollowerControlBackend.PhaseScope __state)
        {
            __state = FollowerControlBackend.EnterPhase(ControlUpdatePhase.NativeFixedUpdate);
        }

        private static Exception Finalizer(Exception __exception, FollowerControlBackend.PhaseScope __state)
        {
            __state.Dispose();
            return __exception;
        }
    }
}
