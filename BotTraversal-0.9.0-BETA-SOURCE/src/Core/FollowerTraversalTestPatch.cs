using HarmonyLib;

using SPT.Reflection.Patching;
using System;
using System.Reflection;
using UnityEngine;

namespace BotTraversal.Core
{
    /// <summary>
    /// Prevents the full BotMover fixed update only while PIT owns an exclusive movement
    /// transaction. WalkThrough deliberately keeps locomotion alive and gates only the two
    /// NavMesh relink writers below.
    /// </summary>
    internal sealed class FollowerTraversalTestBotMoverPatch : ModulePatch
    {
        private static float nextFailureLogAt;

        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(BotMover), nameof(BotMover.ManualFixedUpdate));
        }

        [PatchPrefix]
        private static bool PatchPrefix(BotMover __instance, out Vector3 __state)
        {
            __state = new Vector3(float.NaN, float.NaN, float.NaN);
            try
            {
                __state = FollowerTraversalTestRuntime.CaptureLearningV338WriterBodyStart(__instance);
                return !FollowerTraversalTestRuntime.ShouldSkipManualFixedUpdate(__instance);
            }
            catch (Exception ex)
            {
                // Fail open so an instrumentation error can never freeze unrelated AI.
                if (Time.realtimeSinceStartup >= nextFailureLogAt)
                {
                    nextFailureLogAt = Time.realtimeSinceStartup + 10f;
                    Logger.LogError("Traversal test BotMover gate failed; running the original mover update.");
                    Logger.LogError(ex);
                }

                return true;
            }
        }

        [PatchPostfix]
        private static void PatchPostfix(BotMover __instance, Vector3 __state)
        {
            FollowerTraversalTestRuntime.ReportLearningV338WriterBodyEnd(
                __instance, __state, "BotMover.ManualFixedUpdate");
        }
    }

    /// <summary>
    /// v1.3.25 WalkThrough gate #1: BotMover.method_12 is the periodic NavMesh
    /// relink/correction pass. Suppress it only during a direct no-way corridor crossing.
    /// </summary>
    internal sealed class FollowerTraversalTestBotMoverRelinkPatch : ModulePatch
    {
        private static float nextFailureLogAt;

        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(BotMover), "method_12");
        }

        [PatchPrefix]
        private static bool PatchPrefix(BotMover __instance)
        {
            try
            {
                return !FollowerTraversalTestRuntime.ShouldSkipNavMeshRelink(__instance);
            }
            catch (Exception ex)
            {
                if (Time.realtimeSinceStartup >= nextFailureLogAt)
                {
                    nextFailureLogAt = Time.realtimeSinceStartup + 10f;
                    Logger.LogError("Traversal BotMover relink gate failed; running the original relink pass.");
                    Logger.LogError(ex);
                }

                return true;
            }
        }
    }

    /// <summary>
    /// GClass494.method_21 integrates actual motion into BotMover's path state AND ends by
    /// calling SetPlayerToNavMesh. WalkThrough needs the integration/corner advancement, so R5
    /// only skips the whole callback for native root-motion/teleport stages. A separate patch
    /// below suppresses only SetPlayerToNavMesh during WalkThrough.
    /// </summary>
    internal sealed class FollowerTraversalTestMotionRelinkPatch : ModulePatch
    {
        private static float nextFailureLogAt;

        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(GClass494), "method_21");
        }

        [PatchPrefix]
        private static bool PatchPrefix(GClass494 __instance, out Vector3 __state)
        {
            __state = new Vector3(float.NaN, float.NaN, float.NaN);
            try
            {
                __state = FollowerTraversalTestRuntime.CaptureLearningV338WriterBodyStart(__instance);
                return !FollowerTraversalTestRuntime.ShouldSkipMotionAppliedRelink(__instance);
            }
            catch (Exception ex)
            {
                if (Time.realtimeSinceStartup >= nextFailureLogAt)
                {
                    nextFailureLogAt = Time.realtimeSinceStartup + 10f;
                    Logger.LogError("Traversal motion relink gate failed; running the original OnMotionApplied relink pass.");
                    Logger.LogError(ex);
                }

                return true;
            }
        }

        [PatchPostfix]
        private static void PatchPostfix(GClass494 __instance, Vector3 __state)
        {
            FollowerTraversalTestRuntime.ReportLearningV338WriterBodyEnd(
                __instance, __state, "GClass494.method_21");
        }
    }


    /// <summary>
    /// R5 narrow WalkThrough anti-snap gate. Let GClass494.method_21 update PositionOnWayInner
    /// and advance corridor corners, but suppress only its final NavMesh projection while PIT owns
    /// the demonstrated off-mesh passage.
    /// </summary>
    internal sealed class FollowerTraversalTestSetPlayerToNavMeshPatch : ModulePatch
    {
        private static float nextFailureLogAt;

        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(BotMover), nameof(BotMover.SetPlayerToNavMesh));
        }

        [PatchPrefix]
        private static bool PatchPrefix(BotMover __instance, ref EBotLinkResult __result, out Vector3 __state)
        {
            __state = new Vector3(float.NaN, float.NaN, float.NaN);
            try
            {
                __state = FollowerTraversalTestRuntime.CaptureLearningV338WriterBodyStart(__instance);
                if (!FollowerTraversalTestRuntime.ShouldSuppressSetPlayerToNavMesh(__instance))
                {
                    return true;
                }

                __result = EBotLinkResult.complete;
                return false;
            }
            catch (Exception ex)
            {
                if (Time.realtimeSinceStartup >= nextFailureLogAt)
                {
                    nextFailureLogAt = Time.realtimeSinceStartup + 10f;
                    Logger.LogError("Traversal SetPlayerToNavMesh gate failed; running the original NavMesh projection.");
                    Logger.LogError(ex);
                }

                return true;
            }
        }

        [PatchPostfix]
        private static void PatchPostfix(BotMover __instance, Vector3 __state)
        {
            FollowerTraversalTestRuntime.ReportLearningV338WriterBodyEnd(
                __instance, __state, "BotMover.SetPlayerToNavMesh");
        }
    }

    /// <summary>
    /// LearningV3.8 final NavMesh body-writer gate. BotMover.method_10 is the leaf that directly
    /// writes BotOwner.Transform.position when native link correction succeeds. Existing method_12
    /// and SetPlayerToNavMesh patches catch ordinary callers; this patch also catches any direct
    /// method_9 -> method_10 route, while ExitCommit topology validation deliberately remains open.
    /// </summary>
    internal sealed class FollowerTraversalTestBotMoverBodyRelinkPatch : ModulePatch
    {
        private static float nextFailureLogAt;

        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(BotMover), "method_10");
        }

        [PatchPrefix]
        private static bool PatchPrefix(
            BotMover __instance, Vector3 __0, bool __1, ref EBotLinkResult __result, out Vector3 __state)
        {
            __state = new Vector3(float.NaN, float.NaN, float.NaN);
            try
            {
                __state = FollowerTraversalTestRuntime.CaptureLearningV338WriterBodyStart(__instance);
                if (!FollowerTraversalTestRuntime.ShouldSuppressNativeNavMeshBodyRelink(__instance, __0, __1))
                {
                    return true;
                }

                // Returning complete is intentional: callers should continue their bookkeeping as if
                // the local link were stable, but the physical body remains owned by Traversal.
                __result = EBotLinkResult.complete;
                return false;
            }
            catch (Exception ex)
            {
                if (Time.realtimeSinceStartup >= nextFailureLogAt)
                {
                    nextFailureLogAt = Time.realtimeSinceStartup + 10f;
                    Logger.LogError("Traversal BotMover body relink gate failed; running the native body writer.");
                    Logger.LogError(ex);
                }
                return true;
            }
        }

        [PatchPostfix]
        private static void PatchPostfix(BotMover __instance, Vector3 __state)
        {
            FollowerTraversalTestRuntime.ReportLearningV338WriterBodyEnd(
                __instance, __state, "BotMover.method_10");
        }
    }

}
