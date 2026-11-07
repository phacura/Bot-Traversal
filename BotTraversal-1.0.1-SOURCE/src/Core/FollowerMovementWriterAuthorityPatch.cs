using EFT;
using HarmonyLib;


using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;
using BotTraversal.Core;

namespace BotTraversal.Core
{
    /// <summary>
    /// CompileFix44 narrowed entry guards for known follower movement writers. The authority coordinator
    /// owns the generation; these hooks only reject a stale writer before it mutates destination,
    /// stop/pause/sprint state, or programmatic teleport position.
    /// </summary>
    internal static class FollowerMovementWriterAuthorityPatch
    {
        private static bool _applied;
        private static readonly FieldInfo BotMoverOwnerField =
            AccessTools.Field(typeof(BotMover), "_owner") ??
            AccessTools.Field(typeof(BotMover), "_botOwner") ??
            AccessTools.Field(typeof(BotMover), "botOwner");
        private static readonly PropertyInfo BotMoverOwnerProperty =
            typeof(BotMover).GetProperty("BotOwner", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ??
            typeof(BotMover).GetProperty("Owner", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        internal static void Apply(Harmony harmony)
        {
            if (_applied || harmony == null)
            {
                return;
            }
            _applied = true;

            FollowerControlPhasePatch.Apply(harmony);
            FollowerCapabilityAdjacentLandingPatch.Apply(harmony);
            FollowerCapabilityFrontierPatch.Apply(harmony);
            FollowerCapabilityNativeConsumerPatch.Apply(harmony);
            FollowerCapabilityFollowGaitPatch.Apply(harmony);
            int patched = 0;
            patched += PatchBotOwnerGoToPointOverloads(harmony);
            patched += PatchAll(harmony, typeof(BotOwner), "StopMove", nameof(PatchBotOwnerStopMove));
            patched += PatchAll(harmony, typeof(BotOwner), "SetTargetMoveSpeed", nameof(PatchBotOwnerIngressSpeed));
            patched += PatchAll(harmony, typeof(BotOwner), "Sprint", nameof(PatchBotOwnerSprint));

            patched += PatchAll(harmony, typeof(BotMover), "GoToPointNoWay", nameof(PatchBotMoverDestination));
            patched += PatchAll(harmony, typeof(BotMover), "SetPointOnWay", nameof(PatchBotMoverDestination));
            patched += PatchAll(harmony, typeof(BotGoToPointData), "SetPoint", nameof(PatchPointDataDestination));
            patched += PatchAll(harmony, typeof(BotGoToPointData), "UpdateToGo", nameof(PatchPointDataDestination));
            patched += PatchAll(harmony, typeof(BotSteering), "LookToDirection", nameof(PatchBotSteeringIntent));
            patched += PatchAll(harmony, typeof(BotSteering), "LookToPoint", nameof(PatchBotSteeringIntent));
            patched += PatchAll(harmony, typeof(BotSteering), "LookToMovingDirection", nameof(PatchBotSteeringIntent));
            patched += PatchAll(harmony, typeof(BotMover), "Stop", nameof(PatchBotMoverStop));
            patched += PatchAll(harmony, typeof(BotMover), "Sprint", nameof(PatchBotMoverSprint));
            patched += PatchAll(harmony, typeof(BotMover), "Teleport", nameof(PatchBotMoverTeleport));
            patched += PatchAll(harmony, typeof(BotMover), "SetPose", nameof(PatchBotMoverSoftMovement));
            patched += PatchAll(harmony, typeof(BotMover), "SetTargetPose", nameof(PatchBotMoverSoftMovement));
            patched += PatchAll(harmony, typeof(BotMover), "SetTargetMoveSpeed", nameof(PatchBotMoverIngressSpeed));

            MethodInfo pauseSetter = typeof(BotMover).GetProperty(
                "Pause", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetSetMethod(true);
            if (pauseSetter != null)
            {
                patched += PatchOne(harmony, pauseSetter, nameof(PatchBotMoverPause));
            }

            patched += PatchAll(harmony, typeof(Player), "Teleport", nameof(PatchPlayerTeleport));
            patched += PatchAll(harmony, typeof(Player), "EnableSprint", nameof(PatchPlayerEnableSprint));

            Logger.LogRuntimeInfo($"[MovementWriterAuthority V2-RC114-FailureOnly] Central control backend, shared probe-frame orchestration and shared current-step Climb ingress and input ownership loaded; guardedTargets={patched}; EFT/SAIN unscoped compatibility remains audited.");
        }

        private static int PatchBotOwnerGoToPointOverloads(Harmony harmony)
        {
            int count = 0;
            try
            {
                foreach (MethodInfo method in typeof(BotOwner).GetMethods(
                             BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                         .Where(method => string.Equals(method.Name, "GoToPoint", StringComparison.Ordinal)))
                {
                    if (method.ReturnType == typeof(void))
                    {
                        count += PatchOne(harmony, method, nameof(PatchBotOwnerGoToPointVoid));
                    }
                    else if (method.ReturnType == typeof(NavMeshPathStatus))
                    {
                        count += PatchOne(harmony, method, nameof(PatchBotOwnerGoToPointStatus));
                    }
                    else
                    {
                        Logger.LogRuntimeInfo(
                            $"[MovementWriterAuthority] Skip unsupported GoToPoint return type {method.ReturnType?.FullName ?? "<null>"}.");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[MovementWriterAuthority] Could not inspect EFT.BotOwner.GoToPoint: {ex.Message}");
            }
            return count;
        }

        private static int PatchAll(Harmony harmony, Type type, string methodName, string prefixName)
        {
            int count = 0;
            try
            {
                IEnumerable<MethodInfo> methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(method => string.Equals(method.Name, methodName, StringComparison.Ordinal));
                foreach (MethodInfo method in methods)
                {
                    count += PatchOne(harmony, method, prefixName);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[MovementWriterAuthority] Could not inspect {type.FullName}.{methodName}: {ex.Message}");
            }
            return count;
        }

        private static int PatchOne(Harmony harmony, MethodInfo target, string prefixName)
        {
            if (target == null)
            {
                return 0;
            }

            try
            {
                MethodInfo prefix = AccessTools.Method(typeof(FollowerMovementWriterAuthorityPatch), prefixName);
                if (prefix == null)
                {
                    return 0;
                }
                var postfix=prefixName==nameof(PatchBotOwnerGoToPointStatus)
                    ?new HarmonyMethod(AccessTools.Method(typeof(FollowerMovementWriterAuthorityPatch),nameof(ObserveNativeFollowPathResult))){priority=Priority.Last}:null;
                harmony.Patch(target, prefix: new HarmonyMethod(prefix) { priority = Priority.First },postfix:postfix);
                return 1;
            }
            catch (Exception ex)
            {
                // A single SPT signature mismatch must not disable the whole authority layer.
                Logger.LogError($"[MovementWriterAuthority] Failed patch {target.DeclaringType?.FullName}.{target.Name}: {ex.Message}");
                return 0;
            }
        }

        private static Vector3? ReadCapabilityDestination(object[] arguments)
        {
            if(arguments==null)return null;
            foreach(var value in arguments)
            {
                if(value is Vector3 vector)return vector;
                if(value is CustomNavigationPoint point)return point.Position;
            }
            return null;
        }
        private static bool PatchBotOwnerGoToPointStatus(BotOwner __instance, ref NavMeshPathStatus __result,object[] __args,out long __state)
        {
            __state=0;
            var target=ReadCapabilityDestination(__args);
            if (Allow(__instance, "BotOwner.GoToPoint",target))
            {
                __state=FollowerTraversalTestRuntime.PrepareCapabilityOriginalFollowPath(__instance,target);
                return true;
            }
            __result = NavMeshPathStatus.PathInvalid;
            return false;
        }
        private static void ObserveNativeFollowPathResult(BotOwner __instance,NavMeshPathStatus __result,object[] __args,long __state,bool __runOriginal)
        {
            if(__runOriginal&&__state>0)
                FollowerTraversalTestRuntime.CompleteCapabilityOriginalFollowPath(__instance,ReadCapabilityDestination(__args),__result,__state);
        }

        private static bool PatchBotOwnerGoToPointVoid(BotOwner __instance,object[] __args)
        {
            return Allow(__instance, "BotOwner.GoToPoint",ReadCapabilityDestination(__args));
        }

        private static bool PatchBotOwnerStopMove(BotOwner __instance)
        {
            return Allow(__instance, "BotOwner.StopMove");
        }

        private static bool PatchBotOwnerIngressSpeed(BotOwner __instance,ref float __0)
        {__0=FollowerTraversalTestRuntime.LimitCapabilityIngressSpeed(__instance,__0);return Allow(__instance,"BotOwner.SoftMovement");}
        private static bool PatchBotMoverIngressSpeed(BotMover __instance,ref float __0)
        {var bot=ResolveBotOwner(__instance);__0=FollowerTraversalTestRuntime.LimitCapabilityIngressSpeed(bot,__0);return Allow(bot,"BotMover.SoftMovement");}
        private static bool PatchBotOwnerSoftMovement(BotOwner __instance)
        {
            return Allow(__instance, "BotOwner.SoftMovement");
        }

        private static bool PatchBotOwnerSprint(BotOwner __instance, bool __0)
        {
            if (__instance == null || !BossPlayers.IsFollower(__instance))
            {
                return true;
            }

            // The same return/retry policy applies to native/PIT callbacks too.
            // Deny re-arm, but a disable still requires the existing authority check.
            if (FollowerTraversalTestRuntime.IsCapabilityRetryWalking(__instance))
                return !__0 && Allow(__instance, "BotOwner.Sprint");

            // Climb-specific deny is scoped to the current, existing Follow-owned
            // pre-Native RouteStep. Combat, medical, expired and other kinds fail open.
            if (__0 && FollowerTraversalTestRuntime.IsV2ClimbEntryWalkPoseOwned(__instance))
            {
                FollowerTraversalTestRuntime.ReportV2RC67ClimbSprintRearmRejected(
                    __instance, "BotOwner.Sprint");
                return false;
            }

            if (!__0 && FollowerMovementWriterAuthority.ShouldHardBlockSprintDisable(__instance, out string leaseReason))
            {
                FollowerTraversalTestRuntime.ReportPostTraversalSprintWriter(
                    __instance,
                    false,
                    __instance.Mover?.Sprinting == true,
                    __instance.GetPlayer?.MovementContext?.IsSprintEnabled == true,
                    "BotOwner.Sprint",
                    "hardGate:" + leaseReason);
                return false;
            }

            return Allow(__instance, "BotOwner.Sprint");
        }

        private static bool PatchBotMoverStop(BotMover __instance)
        {
            return Allow(ResolveBotOwner(__instance), "BotMover.Stop");
        }

        private static bool PatchBotMoverTeleport(BotMover __instance, object[] __args)
        {
            BotOwner owner = ResolveBotOwner(__instance);
            bool accepted = Allow(owner, "BotMover.Teleport");
            // CF11: a rejected native BotMover.Teleport immediately preceded an
            // 8m body-epoch discontinuity in the bridge-side WT trace. Capture
            // the rejected callsite/target ONLY when rejected and rate limited;
            // never guess it caused the relocation and never whitelist it blind.
            if (!accepted && owner != null)
            {
                FollowerTraversalTestRuntime.ReportCF11RejectedNativeTeleport(owner, __args);
            }
            return accepted;
        }

        private static bool PatchBotMoverSoftMovement(BotMover __instance)
        {
            return Allow(ResolveBotOwner(__instance), "BotMover.SoftMovement");
        }

        private static bool PatchBotMoverPause(BotMover __instance, bool __0)
        {
            return Allow(ResolveBotOwner(__instance), "BotMover.Pause=" + __0);
        }

        private static bool PatchBotMoverSprint(BotMover __instance, bool __0)
        {
            BotOwner bot = ResolveBotOwner(__instance);
            if (bot == null || !BossPlayers.IsFollower(bot))
            {
                return true;
            }

            // The same return/retry policy applies to native/PIT callbacks too.
            // Deny re-arm, but a disable still requires the existing authority check.
            if (FollowerTraversalTestRuntime.IsCapabilityRetryWalking(bot))
                return !__0 && Allow(bot, "BotMover.Sprint");

            if (__0 && FollowerTraversalTestRuntime.IsV2ClimbEntryWalkPoseOwned(bot))
            {
                FollowerTraversalTestRuntime.ReportV2RC67ClimbSprintRearmRejected(
                    bot, "BotMover.Sprint");
                return false;
            }

            if (!__0 && FollowerMovementWriterAuthority.ShouldHardBlockSprintDisable(bot, out string leaseReason))
            {
                bool playerSprintEnabled = bot.GetPlayer?.MovementContext?.IsSprintEnabled == true;
                FollowerTraversalTestRuntime.ReportPostTraversalSprintWriter(
                    bot,
                    false,
                    __instance.Sprinting,
                    playerSprintEnabled,
                    "BotMover.Sprint",
                    "hardGate:" + leaseReason);
                using (FollowerMovementWriterAuthority.EnterInternalBypass(bot))
                {
                    __instance.SetBotMoverSprinting("R1-3A6799909F06",@bot,true);
                    if (bot.GetPlayer != null && !playerSprintEnabled)
                    {
                        bot.GetPlayer.PlayerEnableSprint("R1-D7DB705E692B",@bot,true);
                    }
                }
                return false;
            }

            return Allow(bot, "BotMover.Sprint");
        }

        private static bool PatchPlayerTeleport(Player __instance)
        {
            BotOwner bot = __instance?.AIData?.BotOwner;
            return Allow(bot, "Player.Teleport");
        }

        private static bool PatchPlayerEnableSprint(Player __instance, bool __0)
        {
            BotOwner bot = __instance?.AIData?.BotOwner;
            if (bot == null || !BossPlayers.IsFollower(bot))
            {
                return true;
            }

            // The same return/retry policy applies to native/PIT callbacks too.
            // Deny re-arm, but a disable still requires the existing authority check.
            if (FollowerTraversalTestRuntime.IsCapabilityRetryWalking(bot))
                return !__0 && Allow(bot, "Player.EnableSprint");

            if (__0 && FollowerTraversalTestRuntime.IsV2ClimbEntryWalkPoseOwned(bot))
            {
                FollowerTraversalTestRuntime.ReportV2RC67ClimbSprintRearmRejected(
                    bot, "Player.EnableSprint");
                return false;
            }

            if (!__0 && FollowerMovementWriterAuthority.ShouldHardBlockSprintDisable(bot, out string leaseReason))
            {
                FollowerTraversalTestRuntime.ReportPostTraversalSprintWriter(
                    bot,
                    false,
                    bot.Mover?.Sprinting == true,
                    __instance.MovementContext?.IsSprintEnabled == true,
                    "Player.EnableSprint",
                    "hardGate:" + leaseReason);
                return false;
            }

            return Allow(bot, "Player.EnableSprint");
        }

        private static bool PatchBotSteeringIntent(BotSteering __instance,MethodBase __originalMethod)
        {
            foreach(var bot in FollowerEnvironment.RegisteredFollowers)
                if(bot!=null&&ReferenceEquals(bot.Steering,__instance))
                    return !global::BotTraversal.LegacyOriginalPITFollowBridge.TryRejectOriginalPITFollowWrite(bot,"BotSteering."+__originalMethod.Name);
            return true;
        }
        private static bool PatchBotMoverDestination(BotMover __instance, MethodBase __originalMethod,object[] __args)
            => !global::BotTraversal.LegacyOriginalPITFollowBridge.TryRejectOriginalPITFollowWrite(ResolveBotOwner(__instance),"BotMover."+__originalMethod.Name,ReadCapabilityDestination(__args));
        private static bool PatchPointDataDestination(BotGoToPointData __instance,object[] __args)
        {
            foreach(var bot in FollowerEnvironment.RegisteredFollowers)
                if(bot!=null&&ReferenceEquals(bot.GoToSomePointData,__instance))
                    return !global::BotTraversal.LegacyOriginalPITFollowBridge.TryRejectOriginalPITFollowWrite(bot,"BotGoToPointData.Destination",ReadCapabilityDestination(__args));
            return true;
        }

        internal static BotOwner ResolveBotOwner(BotMover mover)
        {
            if (mover == null)
            {
                return null;
            }

            try
            {
                if (BotMoverOwnerField?.GetValue(mover) is BotOwner fieldOwner)
                {
                    return fieldOwner;
                }
                if (BotMoverOwnerProperty?.GetValue(mover, null) is BotOwner propertyOwner)
                {
                    return propertyOwner;
                }
            }
            catch
            {
            }

            return null;
        }

        private static bool Allow(BotOwner bot, string writeKind,Vector3? candidate=null)
        {
            if (bot == null || !BossPlayers.IsFollower(bot))
            {
                return true;
            }

            if (global::BotTraversal.LegacyPITCompatibility.PermitOriginalManualRegroup(bot, writeKind))
                return true;
            // CF12: Before BT reaches the physical probe, the owner's domain may
            // still be Follow. The old fail-open rule otherwise lets beta PIT's
            // FollowAction silently replace an already accepted BT route. Gate
            // only that original action and only for a live, progressing BT lease.
            if (global::BotTraversal.LegacyOriginalPITFollowBridge.TryRejectOriginalPITFollowWrite(bot, writeKind,candidate))
                return false;
            return FollowerMovementWriterAuthority.CanWrite(bot, writeKind, out _);
        }
    }
}
