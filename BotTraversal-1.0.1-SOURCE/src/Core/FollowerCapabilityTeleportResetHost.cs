using System;
using System.Linq;
using EFT;
using UnityEngine;
namespace BotTraversal.Core
{
    internal static partial class FollowerTraversalTestRuntime
    {
        internal static void ClearCapabilityRouteWorkForTeleport(BotOwner bot,string reason)
        {
            if(bot==null||string.IsNullOrEmpty(bot.ProfileId))return;
            string id=bot.ProfileId;
            // Retire asynchronous receipts before removing targets. Teleport owns physical
            // quiescence; no stale Traversal input restoration is allowed in this transaction.
            if(TryGetActiveProbe(bot,out var probe))
            {DetachProbeStateHandler(probe);RetireLifecycleProbe(probe);EnsureLearningV328AttemptTerminalizedBeforeUnregister(probe,"teleportRouteReset");UninstallLifecycleProbe(probe);}
            if(FollowerRouteMissions.TryGetValue(id,out var m))
            {m.Lifecycle.Retire();m.FailureSequence=null;m.RouteRetry=null;FollowerRouteMissions.Remove(id);}
            ClearCapabilityStrategyTargets(bot);
            CapabilityPortalQueue.Cancel(id,Time.time);
            FollowerTraversalRouteCursors.Remove(id);
            FollowerMovementRecoveryStates.Remove(id);
            FollowerWorldSafetyStates.Remove(id);LearningV322PendingParentAttemptByProfile.Remove(id);
            ProvisionalWalkThroughReservations.Remove(id);
            BreadcrumbGapQuarantines.Remove(id);BreadcrumbBarrierDetourUntil.Remove(id);
            PostTraversalEntryQuarantines.Remove(id);RecentTraversalCompletions.Remove(id);
            FollowPathBaselines.Remove(id);FollowPathSanitySamples.Remove(id);
            PostTraversalIntegrityWatches.Remove(id);CF75FollowPhysicalWatches.Remove(id);
            LearningV365ContinuousRouteLastDriveFrame.Remove(id);LearningV374NearSideForwardReapproachUntil.Remove(id);
            LearningV344ReleasedWalkThroughEpisodeByFollower.Remove(id);
            foreach(var key in LearnedTraversalRetryAfter.Keys.Where(k=>k==id||k.StartsWith(id+":",StringComparison.Ordinal)).ToArray())LearnedTraversalRetryAfter.Remove(key);
            foreach(var key in LearningV319ZeroShotTemplateRetryAfter.Keys.Where(k=>k.StartsWith(id+":",StringComparison.Ordinal)).ToArray())LearningV319ZeroShotTemplateRetryAfter.Remove(key);
            // A resume floor is a cancellation boundary, never Native completion evidence.
            FollowerRouteResumeFloors[id]=Math.Max(GetFollowerRouteResumeFloor(id),breadcrumbSequence);
            FollowerRouteExecutionFloors[id]=Math.Max(FollowerRouteExecutionFloors.TryGetValue(id,out int floor)?floor:0,breadcrumbSequence);
            FollowerRouteMissionRearmBlockedUntil[id]=Time.time+.25f;
            WriteEvent("capabilityTeleportRouteWorkCleared",bot,new{reason=reason??string.Empty,
                resumeFloor=breadcrumbSequence,routeDebtRemaining=false,oldTargetsRemaining=false,nativeSuccessRecorded=false});
        }
    }
}
