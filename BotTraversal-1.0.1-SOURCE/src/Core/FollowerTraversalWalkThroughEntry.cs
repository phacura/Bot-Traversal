using System;
using System.Globalization;
using EFT;
using UnityEngine;
using BotTraversal.Core;

namespace BotTraversal.Core
{
    internal static partial class FollowerTraversalTestRuntime
    {
        private static bool TryDriveCapabilityLocalWalkThroughEntry(ProbeRuntime probe,Vector3 entry,float distance,bool insideLane)
        {
            if(probe?.Bot?.Mover==null||probe.Player?.MovementContext==null||!IsProbeLifecycleCurrent(probe)||
                !IsCapabilityRouteRetrySafe(probe.Bot)||!FollowerMovementWriterAuthority.CanCommitScopedPhysicalMotion(probe.Bot,out _))return false;
            var bot=probe.Bot;var player=probe.Player;var controller=bot.Mover.ActualPathController;
            entry=GetCapabilityWalkThroughEntryTarget(probe,entry);
            distance=GetPlanarDistance(player.Position,entry);
            var step=probe.LifecycleStep;bool physicalEntry=false;
            if(FollowerCapabilityPassagePolicy.RestoreSemanticWalkEntry(HasCapabilityFailureStrategy(bot))&&!probe.LearningV372CurrentEpisodeWalkThroughCorridorAuthority&&step!=null&&step.Kind=="WalkThrough"&&FollowerCapabilityPassagePolicy.PhysicalWalkEntry(insideLane,IsFinite(step.RawEntry),
                Vector3.Distance(entry,step.RawEntry),Vector3.Distance(player.Position,step.RawEntry)))
            {entry=step.RawEntry;distance=GetPlanarDistance(player.Position,entry);physicalEntry=true;}
            Vector3? endpoint=controller?.CurPath?.TargetPoint?.Position;
            var toEntry=GetFlatDirection(entry-player.Position);var corner=controller?.HavePath==true?controller.CurrentCornerPoint:player.Position;
            bool healthy=FollowerCapabilityPassagePolicy.LocalEntryPath(bot.Mover.HasPathAndNoComplete,
                endpoint.HasValue?Vector3.Distance(endpoint.Value,entry):float.PositiveInfinity,GetPlanarDistance(player.Position,corner),distance,
                Vector3.Dot(GetFlatDirection(corner-player.Position),toEntry));
            if(!FollowerCapabilityPassagePolicy.ManualEntry(true,probe.WalkThroughStepAssistNativeOwned,healthy,insideLane,distance))return false;
            if(!TryRetireNativeMoverPathPreserveLocomotion(bot,"currentWTEntryRejectsStaleCorner",out _))return true;
            bool safeHeight=FollowerCapabilityPassagePolicy.SupportedEntry(player.MovementContext.IsGrounded,
                FollowerCapabilityPassagePolicy.EntryHeight(player.MovementContext.IsGrounded,entry.y-player.Position.y),
                player.MovementContext.IsGrounded&&TryValidateCapabilitySemanticWalkingRamp(probe,entry));
            bool published=false;
            if(safeHeight&&distance>.08f)
            {
                // A projection mismatch changes the goal, not the WT locomotion contract.
                // Native collision handles the local step. A distant terrain forecast must
                // not veto this same grounded entry only because RawEntry differs from NavMesh.
                float dt=Mathf.Max(.001f,Time.deltaTime);
                published=ApplyCollisionActivePhysicalRouteMotion(bot,player,toEntry,toEntry*Mathf.Min(distance,dt),dt,1f,false,.70f,false,true).Accepted;
            }
            if(!published)CommitCapabilityRouteRetryZeroInput(bot,player);
            if(ShouldEmitDiagnostic("capabilityWalkThroughEntryResource",bot,.25f))WriteEvent("capabilityWalkThroughEntryResource",bot,new{
                edgeId=probe.LearnedEdgeId,episodeId=probe.TraversalEpisodeId,healthy,safeHeight,published,distance,physicalEntry,
                body=Vec(player.Position),entry=Vec(entry),retiredCorner=Vec(corner),physicalEntryGranted=false,nativeSuccessRecorded=false});
            return true;
        }
        // This adapter is the normal semantic WT entry approach writer. It
        // consumes the probe's original ticket, never a newly acquired owner.
        private static void DispatchV2WalkThroughEntryApproach(
            ProbeRuntime probe, Vector3 entry, float entryDistance,
            float entryApproachAngle, float corridorAlignmentAngle,
            bool corridorWitness, bool insideEpisodeLane)
        {
            if (probe?.Bot?.Mover == null || probe.Player?.MovementContext == null ||
                probe.Generation != raidGeneration || probe.MovementWriterGeneration <= 0 ||
                !IsActiveProbe(probe) || probe.Stage != ProbeStage.Aligning ||
                !string.Equals(probe.RequestedTraversalKind, "WalkThrough", StringComparison.OrdinalIgnoreCase) ||
                !IsFinite(entry))
                return;
            BotOwner bot = probe.Bot;
            FollowerWalkThroughArrivalPolicy.Plan plan = FollowerWalkThroughArrivalPolicy.Select(
                entryDistance, WalkThroughEntryConvergenceDistance,
                true, corridorWitness, insideEpisodeLane, WalkThroughPassageStraightLateralTolerance);
            if (plan.Kind == FollowerWalkThroughArrivalPolicy.Action.Reject)
                return;
            FollowerMovementWriterAuthority.WriterDomain domain = probe.LearningV330WalkThroughPreflightFollowAuthority
                ? FollowerMovementWriterAuthority.WriterDomain.Follow
                : FollowerMovementWriterAuthority.WriterDomain.Traversal;
            bool admitted = FollowerMovementWriterAuthority.TryEnterBoundPhysicalMotion(
                bot, domain, probe.MovementWriterGeneration,
                out FollowerMovementWriterAuthority.WriteScope scope, out string rejection);
            bool reachOverrideRequested = false;
            if (admitted)
            {
                using (scope)
                {
                    if(TryDriveCapabilityLocalWalkThroughEntry(probe,entry,entryDistance,insideEpisodeLane))return;
                    if (Time.time >= probe.WalkThroughNextDirectPointAt)
                    {
                        bot.Mover.BotMoverGoToPointNoWay("R1-CDA8892739A5",@bot,entry);
                        if (plan.Kind == FollowerWalkThroughArrivalPolicy.Action.TightenReach)
                        {
                            bot.Mover.BotMoverSetReachDist("R1-EB8EF1686AD4",@bot,plan.ReachDistance);
                            reachOverrideRequested = true;
                        }
                        probe.WalkThroughNextDirectPointAt = Time.time + WalkThroughDirectPointRefreshSeconds;
                    }
                    ApplyProbeLocomotionIntent(
                        probe,
                        Mathf.Max(WalkThroughEntryGovernorSpeed, 0.88f),
                        allowSprint: entryApproachAngle <= ProbeRollingAlignmentAngle);
                    bool learningV369MoverOwnsEntryTranslation =
                        (probe.Bot.Mover?.HasPathAndNoComplete == true);
                    if (!learningV369MoverOwnsEntryTranslation)
                    {
                        if(!TryPublishLearningV414PathlessNativeInputLease(probe,entry,.55f,"walkThroughEntryCentering",exactWorldDirectionInput:true))
                            CommitCapabilityRouteRetryZeroInput(bot,probe.Player);
                    }
                    else if (ShouldEmitDiagnostic(
                        "learningV369MoverOwnedWalkThroughEntryInputIsolated:" +
                            probe.LearnedEdgeId.ToString(CultureInfo.InvariantCulture), probe.Bot, 0.35f))
                    {
                        WriteEvent("learningV369MoverOwnedWalkThroughEntryInputIsolated", probe.Bot, new
                        {
                            edgeId = probe.LearnedEdgeId,
                            entryDistance,
                            entryApproachAngle,
                            corridorAlignmentAngle,
                            action = "moverOwnsTranslationAndSteeringTraversalDoesNotOverwriteForwardInput"
                        });
                    }
                }
            }
            if (ShouldEmitDiagnostic("v2rc79WTEntryArrivalPlan:" +
                    probe.LearnedEdgeId.ToString(CultureInfo.InvariantCulture), bot, 0.25f))
                WriteEvent("v2rc79WTEntryArrivalPlan", bot, new
                {
                    edgeId = probe.LearnedEdgeId, episodeId = probe.TraversalEpisodeId,
                    movementGeneration = probe.MovementWriterGeneration,
                    entryDistance, entry = Vec(entry), body = Vec(probe.Player.Position),
                    plan = plan.Kind.ToString(), requestedReachDistance = plan.ReachDistance,
                    corridorWitness, insideEpisodeLane, admitted, rejection,
                    reachOverrideRequested,
                    nativeAttemptRecorded = false, physicalEntryGranted = false,
                    nativeSuccessRecorded = false, bodyRelocated = false,
                    action = admitted
                        ? "sameEntryMoverUsesOriginScopedArrivalPrecisionRequireLaterPhysicalLaneWitness"
                        : "staleProbeCannotConfigureOrPublishEntryMovement"
                });
        }
    }
}


