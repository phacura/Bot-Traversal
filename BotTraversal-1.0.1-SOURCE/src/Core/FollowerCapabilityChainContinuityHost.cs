// R3Q_CONTINUITY_EXTENSION_REVIEWED
using System;
using EFT;
using UnityEngine;
namespace BotTraversal.Core
{
    internal static partial class FollowerTraversalTestRuntime
    {
        private static bool TryCaptureCapabilityLandingProofMotionFrontier(ProbeRuntime probe,
            LearnedTraversalCommitment commitment,Vector3 body,Vector3 requested,
            out FollowerRouteMissionStep next,out float ahead,out float budget)
        {
            ahead=0f;budget=0f;
            if(!TryCaptureCapabilityAdjacentLanding(probe,commitment,body,out next,out var geometry))return false;
            var axis=GetFlatDirection(next.RawExit-next.RawEntry);
            var currentAxis=GetFlatDirection(commitment.RawExit-commitment.RawEntry);
            float length=Mathf.Max(.01f,GetPlanarDistance(commitment.RawEntry,commitment.RawExit));
            float progress=Vector3.Dot(body-commitment.RawEntry,currentAxis);
            float required=FollowerCapabilityDropDownTransitionPolicy.RequiredProjection(length,NativeDropDownFarSideRatio);
            var snapshot=new DropDownBoundarySnapshot(
                GetCapabilityDropDownConsumerPhase(probe),string.Equals(commitment.Kind,"DropDown",StringComparison.OrdinalIgnoreCase),
                probe.NativeDropDownAttempted,!next.AttemptStartedObserved,progress,required,
                geometry.Ahead,geometry.Lateral,geometry.VerticalError,geometry.AboveExit,Vector3.Dot(requested,axis),NativeDropDownEntryTriggerDistance);
            var plan=FollowerCapabilityDropDownTransitionPolicy.Plan(snapshot);
            float candidateAge=probe.NativeDropDownLandingStableSince<0f?0f:Time.time-probe.NativeDropDownLandingStableSince;
            bool stableContact=probe.Player.MovementContext.IsGrounded&&
                Mathf.Abs(probe.Player.Velocity.y)<=NativeDropDownLandingMaxVerticalSpeed;
            float rolling=FollowerCapabilityHandoffPolicy.RollingLandingBudget(snapshot,Time.deltaTime,
                NativeDropDownLandingStableSeconds,Mathf.Abs(probe.Player.Velocity.y),.38f,candidateAge,stableContact);
            ahead=geometry.Ahead;budget=plan.ForwardBudget;
            if(rolling>=0f)
            {
                budget=plan.Active?Mathf.Min(budget,rolling):rolling;
                if(ShouldEmitDiagnostic("chainLandingRunway:"+probe.TraversalEpisodeId,probe.Bot,.12f))
                    WriteEvent("capabilityChainLandingRunwayBudget",probe.Bot,new{
                        edgeId=commitment.EdgeId,successorEdgeId=next.EdgeId,
                        stage=probe.Stage.ToString(),ahead=geometry.Ahead,lateral=geometry.Lateral,
                        verticalError=geometry.VerticalError,progress,required,frameDelta=Time.deltaTime,
                        rollingBudget=rolling,hardBoundaryActive=plan.Active,appliedBudget=budget,
                        landingProofSeconds=NativeDropDownLandingStableSeconds,
                        nativeSuccessRecorded=false,bodyRelocated=false});
            }
            return plan.Active||rolling>=0f;
        }
        private static bool TryGetCapabilityRollingLandingHandoff(ProbeRuntime probe,LearnedTraversalCommitment commitment,
            FollowerRouteMissionStep successor,float ahead,float lateral,float speed)
        {
            if(probe?.Player?.MovementContext==null||successor==null||!IsProbeLifecycleCurrent(probe)||
                !ReferenceEquals(probe.FrozenTraversalCommitment,commitment)||!IsFinite(probe.Player.Velocity)||
                probe.LifecycleMission==null||TimeExpiredCapabilityPhase(probe.LifecycleMission)||
                !FollowerMovementWriterAuthority.CanCommitScopedPhysicalMotion(probe.Bot,out _))return false;
            var context=probe.Player.MovementContext;
            var follower=BossPlayers.GetFollowerByProfileId(probe.Bot.ProfileId);
            if(follower==null||(follower.TryPeekActiveCommand(out var command,out _,out _)&&command!=FollowerCommandType.None)||
                context.OverridenState!=null||context.OverridenControlsState!=null||context.IgnoreDeltaMovement||
                IsLearningV350TerminalPhysicalRecoveryEligible(probe.Bot)||IsTeleportTransactionActive(probe.Bot)||
                FollowerEnvironment.TryGetImmediateSurvivalThreat(probe.Bot,out _)||
                FollowerStateAuthorityCoordinator.HasCombatMovementClaim(probe.Bot,out _))return false;
            var axis=GetFlatDirection(successor.RawExit-successor.RawEntry);
            bool contact=probe.Player.MovementContext.IsGrounded&&Mathf.Abs(probe.Player.Velocity.y)<=NativeDropDownLandingMaxVerticalSpeed;
            if(!contact||!TryCaptureCapabilityLandingProofMotionFrontier(probe,commitment,probe.Player.Position,
                axis*Mathf.Max(.001f,Time.deltaTime*speed*2f),out var exact,out _,out float budget))return false;
            return FollowerCapabilityHandoffPolicy.CanRollLandingProof(IsProbeLifecycleCurrent(probe),
                probe.NativeDropDownSawAirborne&&probe.NativeDropDownSawDescent&&probe.NativeDropDownSawFarSide,
                contact,ReferenceEquals(exact,successor),ahead,lateral,budget);
        }
        private static bool ShouldPrepareCapabilitySuccessorIngressRun(BotOwner bot,Player player,
            FollowerRouteMission mission,FollowerRouteMissionStep step)
        {
            if(bot==null||!IsLiveBot(bot)||step==null||!string.Equals(step.Kind,"DropDown",StringComparison.OrdinalIgnoreCase)||
                !IsRouteMissionSameChainImmediateSuccessor(mission,mission.CurrentStepIndex)||
                !TryValidateV2CurrentTypedStep(bot,mission,step,true,out _)||Time.time>mission.ExpiresAt||
                !ReferenceEquals(bot.GetPlayer,player)||HasActiveTraversalProbe(bot)||
                player.MovementContext.OverridenState!=null||player.MovementContext.OverridenControlsState!=null||
                IsLearningV350TerminalPhysicalRecoveryEligible(bot)||IsTeleportTransactionActive(bot)||
                FollowerEnvironment.TryGetImmediateSurvivalThreat(bot,out _)||
                FollowerStateAuthorityCoordinator.HasCombatMovementClaim(bot,out _)||
                !FollowerMovementWriterAuthority.CanCommitScopedPhysicalMotion(bot,out _)||
                !IsFinite(player.Position)||!IsFinite(step.RawEntry)||!IsFinite(step.RawExit))return false;
            if((FollowerCapabilityActuationPolicy.RetiredPhysicalStrategy(
                mission.CurrentStepIndex,mission.LearningV446StrategyEpochStepIndex,step.EdgeId,
                mission.LearningV446StrategyEpochSemanticEdgeId,mission.LearningV446RetiredPhysicalStrategyEdgeId,
                mission.LearningV446RetiredPhysicalStrategyKind,"DropDown")))return false;
            var previous=mission.Steps[mission.CurrentStepIndex-1];
            var axis=GetFlatDirection(step.RawExit-step.RawEntry);
            if(axis.sqrMagnitude<.5f||step.RawEntry.y-step.RawExit.y<.25f)return false;
            var delta=step.RawEntry-player.Position;delta.y=0f;
            float ahead=Vector3.Dot(delta,axis),side=(delta-axis*ahead).magnitude;
            return FollowerCapabilityChainContinuityPolicy.Resume(true,
                previous.Lifecycle.Result==FollowerTraversalLifecycle.Resolution.Executed,
                !step.AttemptStartedObserved,player.MovementContext.IsGrounded,
                player.CurrentStateName==EPlayerState.Idle,ahead,side,
                Mathf.Abs(player.Position.y-step.RawEntry.y),
                Vector3.Angle(GetFlatDirection(player.MovementContext.PlayerRealForward),axis),
                NativeDropDownEntryMaxAlignmentAngle);
        }
    }
}
