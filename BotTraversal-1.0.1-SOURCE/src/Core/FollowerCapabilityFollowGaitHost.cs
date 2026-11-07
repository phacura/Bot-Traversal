using System;
using System.Reflection;
using EFT;
using HarmonyLib;
using UnityEngine;
using DrakiaXYZ.BigBrain.Brains;

namespace BotTraversal.Core
{
    internal static partial class FollowerTraversalTestRuntime
    {
        internal static bool SelectCapabilityOrdinaryFollowGait(BotOwner bot,bool requested,float followDistance)
        {
            var player=bot?.GetPlayer;var context=player?.MovementContext;
            var leader=bot?.BotFollower?.BossToFollow;
            if(bot==null||context==null||leader==null||bot.Mover==null||!IsLiveBot(bot))return requested;
            var follower=BossPlayers.GetFollowerByProfileId(bot.ProfileId);
            long generation=FollowerMovementWriterAuthority.PeekExactGeneration(bot,out var owner);
            bool eligible=generation>0&&owner==FollowerMovementWriterAuthority.WriterDomain.Follow&&follower!=null&&
                !(follower.TryPeekActiveCommand(out var command,out _,out _)&&command!=FollowerCommandType.None)&&
                !HasActiveTraversalProbe(bot)&&!TryGetActiveFollowerRouteMission(bot,out _)&&
                !IsLearningV350TerminalPhysicalRecoveryEligible(bot)&&!IsTeleportTransactionActive(bot)&&
                context.OverridenState==null&&context.OverridenControlsState==null&&!context.IgnoreDeltaMovement&&
                !FollowerMedical.IsUsingMedical(bot)&&!FollowerMedical.IsHandsInteractionActiveForAuthority(player)&&
                !FollowerEnvironment.TryGetImmediateSurvivalThreat(bot,out _)&&
                !FollowerStateAuthorityCoordinator.HasCombatMovementClaim(bot,out _);
            float distance=Vector3.Distance(player.Position,leader.Position);
            bool selected=FollowerCapabilityFollowGaitPolicy.Select(requested,bot.Mover.Sprinting,eligible,
                context.CanWalk&&context.CanSprint,distance,followDistance);
            if(eligible&&selected!=requested&&ShouldEmitDiagnostic("capabilityFollowGaitHysteresis",bot,.5f))
                WriteEvent("capabilityFollowGaitHysteresis",bot,new{generation,distance,followDistance,requested,
                    selected,currentSprint=bot.Mover.Sprinting,source="originalPITFollowAction",newOwner=false});
            return selected;
        }
    }
    internal static class FollowerCapabilityFollowGaitPatch
    {
        private static MethodInfo FollowDistance;
        internal static void Apply(Harmony harmony)
        {
            var type=AccessTools.TypeByName("pitTeam.BigBrain.Actions.FollowAction");
            if(type==null)return; // Optional compatibility with the installed PIT assembly.
            if(!string.Equals(type.Assembly.GetName().Name,"pitFireTeam",StringComparison.OrdinalIgnoreCase)||
                !typeof(CustomLogic).IsAssignableFrom(type))return;
            if(!PITCompatibilityPolicy.ReviewedFollowBuild(type.Module.ModuleVersionId))
            {
                Logger.LogRuntimeInfo("[RC105 Follow gait] PIT build differs from the reviewed getter contract; original gait retained.");
                return;
            }
            var method=AccessTools.DeclaredMethod(type,"ApplyFollowChaseMovementState",new[]{typeof(bool)});
            FollowDistance=AccessTools.DeclaredMethod(type,"GetEffectiveFollowDistance",Type.EmptyTypes);
            if(method==null||FollowDistance==null||FollowDistance.ReturnType!=typeof(float))
                throw new MissingMethodException("PIT FollowAction gait/config contract changed");
            harmony.Patch(method,prefix:new HarmonyMethod(AccessTools.Method(typeof(FollowerCapabilityFollowGaitPatch),nameof(Prefix))){priority=Priority.Last});
            var follow=AccessTools.DeclaredMethod(type,"Follow",new[]{typeof(bool),typeof(float)});
            if(follow==null)throw new MissingMethodException("PIT ordinary Follow boundary changed");
            harmony.Patch(follow,prefix:new HarmonyMethod(AccessTools.Method(typeof(FollowerCapabilityFollowGaitPatch),nameof(FollowPrefix))){priority=Priority.Last},
                postfix:new HarmonyMethod(AccessTools.Method(typeof(FollowerCapabilityFollowGaitPatch),nameof(FollowPostfix))){priority=Priority.Last},
                finalizer:new HarmonyMethod(AccessTools.Method(typeof(FollowerCapabilityFollowGaitPatch),nameof(FollowFinalizer))){priority=Priority.Last});
        }
        private static void FollowPrefix(CustomLogic __instance,ref bool __0,ref float __1,out object? __state)
        {
            __state=null;if(__instance==null||FollowDistance==null)return;
            float distance;try{distance=(float)FollowDistance.Invoke(__instance,null);}catch{return;}
            __state=FollowerTraversalTestRuntime.PrepareCapabilityGenericFollowHandoff(__instance.BotOwner,distance,ref __0,ref __1);
        }
        private static void FollowPostfix(CustomLogic __instance,object? __state,bool __runOriginal)
        {
            if(__instance==null)return;
            FollowerTraversalTestRuntime.CompleteCapabilityGenericFollowHandoff(__instance.BotOwner,__state,__runOriginal);
            FollowerTraversalTestRuntime.QuiesceCapabilityOriginalFollowIdle(__instance.BotOwner);
        }
        private static Exception? FollowFinalizer(CustomLogic __instance,object? __state,Exception? __exception)
        {
            if(__instance!=null&&__exception!=null)FollowerTraversalTestRuntime.CompleteCapabilityGenericFollowHandoff(__instance.BotOwner,__state,false);
            return __exception;
        }
        private static void Prefix(CustomLogic __instance,ref bool __0)
        {
            // The verified getter only reads/clamps the existing ConfigEntry<int>.
            if(__instance==null||FollowDistance==null||!FollowDistance.DeclaringType.IsInstanceOfType(__instance))return;
            float distance;
            try { distance=(float)FollowDistance.Invoke(__instance,null); }
            catch { return; }
            __0=FollowerTraversalTestRuntime.SelectCapabilityOrdinaryFollowGait(__instance.BotOwner,__0,distance);
        }
    }
}
