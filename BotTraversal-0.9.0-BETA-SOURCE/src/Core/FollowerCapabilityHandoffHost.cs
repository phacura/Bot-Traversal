// R3R_HANDOFF_EXTENSION_REVIEWED
using System;
using System.Globalization;
using EFT;
using UnityEngine;
using UnityEngine.AI;
namespace BotTraversal.Core
{
    internal static partial class FollowerTraversalTestRuntime
    {
        private static bool TryAdoptLearningV374SeamlessTerminalConsumer(
            ProbeRuntime probe,
            PostTraversalIntegrityWatch watch,
            out string detail)
        {
            detail = "notEligible";
            if ((probe?.Bot == null) ||
                probe.Player == null || watch == null || !watch.EvaluateEndToEndFollow ||
                !probe.ExitAuthorityFollowPathObserved ||
                !string.Equals(
                    probe.ExitAuthorityFollowPathStatus,
                    NavMeshPathStatus.PathComplete.ToString(),
                    StringComparison.OrdinalIgnoreCase) ||
                !IsFinite(probe.ExitAuthorityFollowTarget) ||
                probe.Bot.Mover == null || !probe.Bot.Mover.HasPathAndNoComplete)
            {
                return false;
            }

            if ((HasLearningV364UnsynchronizedPhysicalNavigationEpoch(
                    probe.Bot, out PhysicalNavigationEpochHandoff pendingEpoch)))
            {
                detail = "pendingPhysicalNavigationEpoch:" +
                    (pendingEpoch?.SourceEdgeId ?? 0).ToString(CultureInfo.InvariantCulture);
                return false;
            }

            {
                // V424 runtime falsified V374's "existing PathComplete means fresh producer" premise:
                // after an entire Jump->DropDown chain completed, the inherited Follow path still
                // carried BotMover's PrevSuccessLinkedFrom at the consumed Jump entrance and the
                // first ManualFixedUpdate relocated the body there. Treat the observed path as
                // admission evidence only; V419 must re-prime a fresh path from the CURRENT body
                // while V364 suppresses method_10 until native bookkeeping proves the same epoch.
                bool primed = TryHandoffLearningV419TraversalToFreshFollowConsumer(
                    probe.Bot,
                    probe.MovementWriterGeneration,
                    "learningV425SeamlessTerminalConsumerRebase",
                    "post-traversal-fresh-epoch",
                    out string primeDetail);
                if (!primed)
                {
                    detail = "learningV425FreshEpochNotCommitted:" + primeDetail;
                    return false;
                }

                long followGeneration = FollowerMovementWriterAuthority.GetCurrentGeneration(
                    probe.Bot, out FollowerMovementWriterAuthority.WriterDomain followOwner);
                if (followGeneration <= 0 ||
                    followOwner != FollowerMovementWriterAuthority.WriterDomain.Follow)
                {
                    detail = "learningV425FollowTransitionUnavailable";
                    return false;
                }

                watch.MovementGeneration = followGeneration;
                watch.ReleasePosition = probe.Player.Position;
                EndLearningV375PersistentRouteTransaction(
                    probe.Bot, "learningV425SeamlessTerminalConsumerRebase");
                MarkLearningV352PostTraversalConsumerPathProven(
                    probe.Bot, "learningV425FreshFollowConsumerEpoch", followGeneration);
                detail = "learningV425FreshEpochCommitted:generation=" +
                         followGeneration.ToString(CultureInfo.InvariantCulture) +
                         ":" + primeDetail;
                WriteEvent("learningV425SeamlessTerminalConsumerRebased", probe.Bot, new
                {
                    edgeId = watch.EdgeId,
                    episodeId = watch.EpisodeId,
                    chainId = watch.CompletedChainId,
                    previousObservedTarget = Vec(probe.ExitAuthorityFollowTarget),
                    pathSource = probe.ExitAuthorityFollowPathSource ?? string.Empty,
                    followGeneration,
                    releasePosition = Vec(watch.ReleasePosition),
                    physicalEpochSynchronized =
                        !HasLearningV364UnsynchronizedPhysicalNavigationEpoch(
                            probe.Bot, out PhysicalNavigationEpochHandoff _),
                    bodyRelocated = false,
                    action = "observedExitPathIsEvidenceOnlyFreshFollowPathMustRebaseNativeMoverToCurrentBodyEpoch"
                });
                return true;
            }

            FollowerMovementWriterAuthority.TransitionTraversalToFollowIfCurrent(
                probe.Bot,
                probe.MovementWriterGeneration,
                "learningV374SeamlessTerminalConsumer");
            long legacyFollowGeneration = FollowerMovementWriterAuthority.GetCurrentGeneration(
                probe.Bot, out FollowerMovementWriterAuthority.WriterDomain legacyFollowOwner);
            if (legacyFollowGeneration <= 0 ||
                legacyFollowOwner != FollowerMovementWriterAuthority.WriterDomain.Follow)
            {
                detail = "followTransitionUnavailable";
                return false;
            }

            watch.MovementGeneration = legacyFollowGeneration;
            watch.ReleasePosition = probe.Player.Position;
            EndLearningV375PersistentRouteTransaction(probe.Bot, "learningV374SeamlessTerminalConsumer");
            MarkLearningV352PostTraversalConsumerPathProven(
                probe.Bot, "learningV374ExistingExitConsumerProducer", legacyFollowGeneration);
            detail = "adoptedExistingExitConsumer:generation=" +
                     legacyFollowGeneration.ToString(CultureInfo.InvariantCulture);
            WriteEvent("learningV374SeamlessTerminalConsumerAdopted", probe.Bot, new
            {
                edgeId = watch.EdgeId,
                episodeId = watch.EpisodeId,
                chainId = watch.CompletedChainId,
                target = Vec(probe.ExitAuthorityFollowTarget),
                pathSource = probe.ExitAuthorityFollowPathSource ?? string.Empty,
                followGeneration = legacyFollowGeneration,
                releasePosition = Vec(watch.ReleasePosition),
                physicalEpochSynchronized = true,
                action = "transferFreshExitConsumerProducerToFollowWithoutStopReprimeOrPostTraversalWriterChurn"
            });
            return true;
        }
        private static bool ShouldPreserveCapabilityTerminalSprint(ProbeRuntime probe,Vector3 target)
        {
            if(probe?.Bot==null||probe.Player?.MovementContext==null||!IsLiveBot(probe.Bot)||
                !ReferenceEquals(probe.Bot.GetPlayer,probe.Player)||!IsProbeLifecycleCurrent(probe)||
                probe.Stage!=ProbeStage.ExitCommit||!IsFinite(target)||!IsFinite(probe.Player.Position)||
                !string.Equals(probe.RequestedTraversalKind,"DropDown",StringComparison.OrdinalIgnoreCase)||
                !TryResolveLearningV376MissionForProbe(probe,out var mission,out int index)||
                mission?.Steps==null||mission.CurrentStepIndex!=index||index!=mission.Steps.Count-1||
                !ReferenceEquals(probe.LifecycleMission,mission)||!ReferenceEquals(probe.LifecycleStep,mission.Steps[index])||
                IsTeleportTransactionActive(probe.Bot)||IsLearningV350TerminalPhysicalRecoveryEligible(probe.Bot)||
                FollowerEnvironment.TryGetImmediateSurvivalThreat(probe.Bot,out _)||
                FollowerStateAuthorityCoordinator.HasCombatMovementClaim(probe.Bot,out _))return false;
            var follower=BossPlayers.GetFollowerByProfileId(probe.Bot.ProfileId);
            if(follower==null||(follower.TryPeekActiveCommand(out var command,out _,out _)&&command!=FollowerCommandType.None))return false;
            var context=probe.Player.MovementContext;
            bool keep=FollowerCapabilityHandoffPolicy.PreserveTerminalSprint(true,
                probe.NativeDropDownAttempted&&probe.NativeDropDownPhysicalLandingAuthority,
                context.IsGrounded,probe.ExitAuthorityTopologyRejoinConfirmed,
                probe.Bot.Mover?.Sprinting==true,
                context.OverridenState!=null||context.OverridenControlsState!=null,
                GetPlanarDistance(probe.Player.Position,target),PostTraversalChaseSettleDistance);
            if(keep&&ShouldEmitDiagnostic("terminalSprintCarry:"+probe.TraversalEpisodeId,probe.Bot,.25f))
                WriteEvent("capabilityTerminalSprintCarryPreserved",probe.Bot,new{
                    edgeId=probe.LearnedEdgeId,episodeId=probe.TraversalEpisodeId,missionId=mission.MissionId,
                    writer=probe.MovementWriterGeneration,state=probe.Player.CurrentStateName.ToString(),
                    body=Vec(probe.Player.Position),target=Vec(target),nativeSuccessRecorded=false,
                    bodyRelocated=false,action="validatedFinalDropKeepsExistingSprintAcrossFreshFollowPathCommit"});
            return keep&&IsProbeLifecycleCurrent(probe);
        }
    }
}
