// R3S_PHASE_HANDOFF_EXTENSION_REVIEWED
using System;
using EFT;
using UnityEngine;
namespace BotTraversal.Core
{
    internal static partial class FollowerTraversalTestRuntime
    {
        private static void PrepareCapabilityJumpLocomotion(BotOwner bot,Player player)
        {
            if(bot==null||player?.MovementContext==null||!player.MovementContext.IsGrounded||
                !FollowerRouteMissions.TryGetValue(bot.ProfileId,out var mission)||mission==null||
                mission.CurrentStepIndex<0||mission.CurrentStepIndex>=mission.Steps.Count)return;
            var step=mission.Steps[mission.CurrentStepIndex];
            bool phase=ShouldPrepareCapabilityCompletedPhaseRun(bot,player,mission,step);
            if(!phase&&(HasActiveTraversalProbe(bot)||
                (player.CurrentStateName!=EPlayerState.Transition&&player.CurrentStateName!=EPlayerState.Idle)))return;
            bool climb=!phase&&ShouldPrepareCapabilityClimbIngressRun(bot,player,mission,step);
            bool successor=!phase&&ShouldPrepareCapabilitySuccessorIngressRun(bot,player,mission,step);
            if(!phase&&!climb&&!successor&&(step==null||!string.Equals(step.Kind,"Jump",StringComparison.OrdinalIgnoreCase)||
                IsRetiredCapabilityJumpPreparation(mission,step)||!TryValidateV2CurrentTypedStep(bot,mission,step,true,out _)||
                GetPlanarDistance(player.Position,GetLearningV442JumpAdmissionEntry(step))>RouteMissionTraversalAdjacentBridgeMaxDistance))return;
            var follower=BossPlayers.GetFollowerByProfileId(bot.ProfileId);
            if(follower!=null&&follower.TryPeekActiveCommand(out var command,out _,out _)&&command!=FollowerCommandType.None)return;
            if(player.MovementContext.States.TryGetValue(EPlayerState.Run,out var run))
                RequestCapabilityLocomotionResume(bot,player,run);
        }
        private static bool ShouldPrepareCapabilityCompletedPhaseRun(BotOwner bot,Player player,FollowerRouteMission mission,FollowerRouteMissionStep step)
        {
            if(bot==null||player?.MovementContext==null||!IsLiveBot(bot)||!ReferenceEquals(bot.GetPlayer,player)||
                mission==null||mission.Steps==null||mission.Lifecycle.Retired||TimeExpiredCapabilityPhase(mission)||
                mission.CurrentStepIndex<0||mission.CurrentStepIndex>=mission.Steps.Count||
                !ReferenceEquals(mission.Steps[mission.CurrentStepIndex],step)||
                !TryGetActiveProbe(bot,out var probe)||!IsProbeLifecycleCurrent(probe)||
                !ReferenceEquals(probe.Player,player)||!ReferenceEquals(probe.LifecycleMission,mission)||
                !ReferenceEquals(probe.LifecycleStep,step))return false;
            var context=player.MovementContext;var state=player.CurrentStateName;
            bool jump=string.Equals(probe.RequestedTraversalKind,"Jump",StringComparison.OrdinalIgnoreCase);
            bool drop=string.Equals(probe.RequestedTraversalKind,"DropDown",StringComparison.OrdinalIgnoreCase);
            bool phase=probe.Stage==ProbeStage.Egress||probe.Stage==ProbeStage.Verify||probe.Stage==ProbeStage.ExitCommit||
                jump&&probe.Stage==ProbeStage.Landing;
            bool proof=jump?probe.JumpAttempted&&probe.LearningV330NativeJumpQualifiedLandingWitness:
                drop&&probe.NativeDropDownAttempted&&probe.NativeDropDownPhysicalLandingAuthority;
            bool waiting=state==EPlayerState.Idle||state==EPlayerState.JumpLanding||state==EPlayerState.Transition;
            bool locomotion=state==EPlayerState.Run||state==EPlayerState.Sprint;
            var velocity=player.Velocity;
            bool preserveJumpMomentum=FollowerCapabilityPhaseHandoffPolicy.PreserveJumpMomentum(jump,
                IsFinite(velocity)?Vector3.Dot(velocity,GetFlatDirection(step.RawExit-step.RawEntry)):float.NaN);
            var follower=BossPlayers.GetFollowerByProfileId(bot.ProfileId);
            bool manual=follower==null||(follower.TryPeekActiveCommand(out var command,out _,out _)&&command!=FollowerCommandType.None);
            bool blocked=manual||context.OverridenState!=null||context.OverridenControlsState!=null||
                IsTeleportTransactionActive(bot)||IsLearningV350TerminalPhysicalRecoveryEligible(bot)||
                FollowerEnvironment.TryGetImmediateSurvivalThreat(bot,out _)||FollowerStateAuthorityCoordinator.HasCombatMovementClaim(bot,out _);
            return FollowerCapabilityPhaseHandoffPolicy.CanPrepare(IsProbeLifecycleCurrent(probe),phase,proof,
                context.IsGrounded,FollowerCapabilityPhaseHandoffPolicy.StateEligible(jump,drop,waiting,locomotion)&&!preserveJumpMomentum,blocked);
        }
        private static bool TimeExpiredCapabilityPhase(FollowerRouteMission mission)=>UnityEngine.Time.time>mission.ExpiresAt;
        private static bool TryDriveCapabilityCompletedPhaseConsumer(BotOwner bot,FollowerRouteMission mission,FollowerRouteMissionStep step)
        {
            var player=bot?.GetPlayer;
            if(!ShouldPrepareCapabilityCompletedPhaseRun(bot,player,mission,step))return false;
            long generation=FollowerMovementWriterAuthority.PeekExactGeneration(bot,out var owner);
            if(owner!=FollowerMovementWriterAuthority.WriterDomain.Traversal||generation<=0||
                !FollowerMovementWriterAuthority.TryEnterBoundPhysicalMotion(bot,owner,generation,out var scope,out _))return false;
            using(scope)
            {
                if(!ShouldPrepareCapabilityCompletedPhaseRun(bot,player,mission,step))return false;
                PrepareCapabilityJumpLocomotion(bot,player);
                if(ShouldEmitDiagnostic("capabilityCompletedPhase:"+step.EpisodeId,bot,.25f))
                    WriteEvent("capabilityCompletedPhaseLocomotionPrepared",bot,new{missionId=mission.MissionId,
                        stepIndex=mission.CurrentStepIndex,episodeId=step.EpisodeId,edgeId=step.EdgeId,generation,
                        state=player.CurrentStateName.ToString(),body=Vec(player.Position),
                        animatorRequestIsNotPhysicalProgress=true,traversalSuccessRecorded=false,bodyRelocated=false});
                return true;
            }
        }
    }
}
