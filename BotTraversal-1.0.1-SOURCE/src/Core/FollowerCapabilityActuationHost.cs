using System;
using System.Linq;
using EFT;
using UnityEngine;
namespace BotTraversal.Core
{
    internal static partial class FollowerTraversalTestRuntime
    {
        // All route-backed Jump entry points share this pre-allocation gate. A
        // distant bridge cannot acquire the body before the native consumer can run.
        private static bool ShouldDeferCapabilityJumpProbe(BotOwner bot,int edge,out string reason)
        {
            reason=string.Empty;
            if(!TryGetActiveFollowerRouteMission(bot,out var mission)||mission.CurrentStepIndex<0||
                mission.CurrentStepIndex>=mission.Steps.Count)return false;
            var step=mission.Steps[mission.CurrentStepIndex];
            if(step==null||step.EdgeId!=edge||!string.Equals(step.Kind,"Jump",StringComparison.OrdinalIgnoreCase)||
                (FollowerCapabilityActuationPolicy.RetiredPhysicalStrategy(
                    mission.CurrentStepIndex,mission.LearningV446StrategyEpochStepIndex,step.EdgeId,
                    mission.LearningV446StrategyEpochSemanticEdgeId,mission.LearningV446RetiredPhysicalStrategyEdgeId,
                    mission.LearningV446RetiredPhysicalStrategyKind,"Jump")))return false;
            if(!TryValidateV2CurrentTypedStep(bot,mission,step,true,out _)||
                !TryGetLearningV460JumpAdmissionGeometry(bot,step,out var entry,out var axis,out float forward,out float speed))
            {reason="nativeJumpDispatchIdentityOrGeometryUnavailable";return true;}
            var player=bot.GetPlayer;
            var edgeData=LearnedTraversalEdges.FirstOrDefault(x=>x!=null&&x.Id==step.EdgeId);
            float demonstrated=edgeData?.ApproachSpeed??0f;
            bool unsafeSpeed=FollowerCapabilityRouteRetryPolicy.UnsafeJumpSpeed(speed,demonstrated);
            bool missedWindow=HasCapabilityMeasuredMissedJumpWindow(bot,mission,step,forward);
            if(unsafeSpeed&&!TryObserveCapabilityJumpBodyVelocity(player,out _,out float observationDuration,out float observationAge))
            {
                reason="nativeJumpAwaitBodyObservation";
                if(ShouldEmitDiagnostic("rc119ObservationPending:"+step.EpisodeId,bot,.15f))
                    WriteEvent("capabilityJumpBodyObservationPending",bot,new{missionId=mission.MissionId,edgeId=step.EdgeId,
                        rawSpeed=speed,observationDuration,observationAge,forward,state=player.CurrentStateName.ToString(),
                        grounded=player.MovementContext.IsGrounded,canJump=player.MovementContext.CanJump,executionFailed=false,nativeJumpTriggered=false});
                return true;
            }
            if(unsafeSpeed||missedWindow)
            {
                reason=missedWindow?"missedJumpWindowMeasured":"unsafeJumpTakeoffSpeed";
                bool returning=TryRequestCapabilityFullRouteRetry(bot,mission,reason);
                if(ShouldEmitDiagnostic("unsafeJumpTakeoffSpeed:"+step.EpisodeId,bot,.5f))
                    WriteEvent(missedWindow?"capabilityJumpWindowMissedAfterMeasuredMotion":"capabilityJumpUnsafeSpeedRejected",bot,new{missionId=mission.MissionId,edgeId=edge,
                        speed,demonstrated,returning,sprintSpeed=player.MovementContext.SprintSpeed,
                        state=player.CurrentStateName.ToString(),nativeJumpTriggered=false});
                return true;
            }
            Vector3 offset=player.Position-entry;offset.y=0f;
            float lateral=(offset-axis*forward).magnitude;
            Vector3 facing=GetFlatDirection(player.MovementContext.PlayerRealForward);
            Vector3 velocity=GetCapabilityJumpPlanarVelocity(player);velocity.y=0f;
            var corridor=FollowerCapabilityActuationPolicy.JumpWindow(forward,
                GetLearningV452JumpPreparationLeadDistance(speed),LearningV352JumpTakeoffCorridorMaxForwardOvershoot,
                lateral,LearningV352JumpTakeoffCorridorMaxLateral,Vector3.Dot(facing,axis),
                LearningV352JumpTakeoffCorridorMinFacingDot,Vector3.Dot(GetFlatDirection(velocity),axis),
                LearningV352JumpTakeoffCorridorMinMovementHeadingDot);
            bool physicalWindow=IsLearningV449JumpInsidePhysicalTriggerHorizon(forward,speed, bot)||
                IsCf60LegalOneFrameJumpTriggerForecast(bot,forward,speed,out _,out _);
            bool allow=FollowerCapabilityActuationPolicy.CanDispatchNativeJump(physicalWindow,
                player.CurrentStateName==EPlayerState.Run||player.CurrentStateName==EPlayerState.Sprint,
                player.MovementContext.IsGrounded,player.MovementContext.CanJump,corridor.Centered,
                corridor.Facing,corridor.Heading,speed>=RollingTraversalMinSpeed);
            if(allow)return false;
            reason="nativeJumpAwaitExistingPhysicalDispatchWindow";
            if(ShouldEmitDiagnostic("capabilityJumpDispatch:"+step.EpisodeId,bot,0.12f))
                WriteEvent("capabilityJumpProbeDispatchDeferred",bot,new{missionId=mission.MissionId,
                    stepIndex=mission.CurrentStepIndex,episodeId=step.EpisodeId,edgeId=edge,forward,lateral,speed,
                    state=player.CurrentStateName.ToString(),physicalWindow,corridor.Centered,corridor.Facing,corridor.Heading,
                    nativeProbeAllocated=false,nativeAdmissionChanged=false});
            return true;
        }
        private static bool HasCapabilityReadyJumpMotion(Player player,Vector3 axis)
        {
            if(player?.MovementContext==null)return false;
            var velocity=player.Velocity;velocity.y=0f;
            if(TryObserveCapabilityJumpBodyVelocity(player,out var measured,out _,out _))velocity=measured;
            return FollowerCapabilityIngressMotionPolicy.ReadyJumpMotion(velocity.magnitude,Vector3.Dot(GetFlatDirection(velocity),axis),
                RollingTraversalMinSpeed,LearningV352JumpTakeoffCorridorMinFacingDot);
        }
        private static bool TryCommitCapabilityJumpAtDispatch(ProbeRuntime probe,LearnedTraversalCommitment commitment)
        {
            if(probe==null||commitment==null||!IsProbeLifecycleCurrent(probe)||
                probe.AssistedStrategySelected||!probe.NativeStrategySelected||
                !string.Equals(probe.RequestedTraversalKind,"Jump",StringComparison.OrdinalIgnoreCase)||
                probe.JumpAttempted||probe.NativeExecutorConsumed)return false;
            var mission=probe.LifecycleMission;var step=probe.LifecycleStep;
            if(mission==null||step==null||commitment.RouteMissionId!=mission.MissionId||
                commitment.RouteMissionStepIndex!=mission.CurrentStepIndex||commitment.EdgeId!=step.EdgeId)return false;
            // Refresh and consume the EXISTING physical reservation in the same
            // dispatch frame. The native consumer still owns all TryJump guards.
            TryGetLearningV353JumpRouteFunnelTarget(probe.Bot,step,out _,out _,out float forward,
                out float lateral,out float facing,out float heading,out float speed);
            bool committed=TryConsumeLearningV429AtomicJumpAdmission(probe.Bot,mission,step,commitment,
                forward,lateral,facing,heading,speed,out string result);
            WriteEvent("capabilityNativeJumpDispatchCommit",probe.Bot,new{missionId=mission.MissionId,
                stepIndex=mission.CurrentStepIndex,episodeId=step.EpisodeId,edgeId=step.EdgeId,
                committed,result,forward,lateral,facing,heading,speed,nativeSuccessRecorded=false});
            return committed;
        }
        private static bool TryGetCurrentCapabilityNativeExecution(BotOwner bot,FollowerRouteMission mission,
            FollowerRouteMissionStep step,out ProbeRuntime probe)
        {
            probe=null;
            if(!TryGetActiveProbe(bot,out var current)||current==null)return false;
            bool nativeDrop=current.Stage==ProbeStage.NativeDropDown&&
                string.Equals(current.TraversalKind,"DropDown",StringComparison.OrdinalIgnoreCase);
            bool allowed=FollowerCapabilityActuationPolicy.NativeDropConsumer(nativeDrop,
                current.NativeDropDownAttempted,IsProbeLifecycleCurrent(current),
                ReferenceEquals(current.LifecycleMission,mission),ReferenceEquals(current.LifecycleStep,step),
                current.CF67DropDownConsumerEverAccepted||current.LearningV331DropDownLandingForwardHandoffActive||
                    current.NativeDropDownLandingStableSince>=0f,current.NativeDropDownSawFarSide);
            if(!allowed)return false;
            probe=current;return true;
        }
        private static bool IsCapabilityDeliveryOriginCurrent(BotOwner bot,FollowerRouteMission mission,
            FollowerRouteMissionStep step,ProbeRuntime executionProbe)
        {
            if(!TryValidateV2CurrentTypedStep(bot,mission,step,executionProbe==null,out _))return false;
            return executionProbe==null || TryGetCurrentCapabilityNativeExecution(bot,mission,step,out var current)&&
                ReferenceEquals(executionProbe,current);
        }
        private static bool TryGetCapabilityCurrentJumpGait(BotOwner bot,Vector3 axis,out bool facing,out bool sprint)
        {
            facing=true;sprint=false;
            if(HasActiveTraversalProbe(bot)||HasCapabilityFailureStrategy(bot)||IsCapabilityReturnInProgress(bot)||
                !FollowerRouteMissions.TryGetValue(bot.ProfileId,out var m)||m.CurrentStepIndex<0||m.CurrentStepIndex>=m.Steps.Count)return false;
            var step=m.Steps[m.CurrentStepIndex];if(step?.Kind!="Jump")return false;
            facing=Vector3.Dot(GetFlatDirection(bot.GetPlayer.MovementContext.PlayerRealForward),axis)>=LearningV352JumpTakeoffCorridorMinFacingDot;
            TryGetCapabilityRouteSprint(bot,axis,out sprint);
            return true;
        }
        private static bool TryGetCapabilityJumpPreparationFacing(BotOwner bot,Vector3? target,
            CapabilityMotionPurpose purpose,Vector3 movementDirection,out Vector3 axis,out bool forwardOnly)
        {
            axis=Vector3.zero;forwardOnly=false;
            if(HasCapabilityFailureStrategy(bot)||IsCapabilityReturnInProgress(bot))return false;
            if(!target.HasValue||purpose==CapabilityMotionPurpose.NativeProbe||purpose==CapabilityMotionPurpose.NativeExecution||
                !TryGetActiveFollowerRouteMission(bot,out var mission)||mission.CurrentStepIndex<0||
                mission.CurrentStepIndex>=mission.Steps.Count)return false;
            var current=mission.Steps[mission.CurrentStepIndex];
            if(!TryValidateV2CurrentTypedStep(bot,mission,current,true,out _))return false;
            var jump=current;
            if(!string.Equals(current.Kind,"Jump",StringComparison.OrdinalIgnoreCase))
            {
                if(!IsLearningV369PhysicalCorridorStep(current)||mission.CurrentStepIndex+1>=mission.Steps.Count||
                    !LearningV365ContinuousRouteProducerStates.TryGetValue(bot.ProfileId,out var producer)||
                    producer==null||producer.Generic||producer.MissionId!=mission.MissionId||
                    producer.StepIndex!=mission.CurrentStepIndex||!producer.LearningV457SuccessorAdmissionLeaseActive||
                    producer.LearningV457SuccessorStepIndex!=mission.CurrentStepIndex+1)return false;
                jump=mission.Steps[mission.CurrentStepIndex+1];
                if(jump==null||producer.LearningV457SuccessorEdgeId!=jump.EdgeId||
                    !TryValidateV2AdjacentPhysicalContinuationBoundary(bot,mission,current,jump,true,out _))return false;
            }
            if(!string.Equals(jump.Kind,"Jump",StringComparison.OrdinalIgnoreCase))return false;
            Vector3 rawEntry=IsFinite(jump.RawEntry)?jump.RawEntry:jump.Entry;
            Vector3 admission=GetLearningV442JumpAdmissionEntry(jump);
            axis=GetFlatDirection((IsFinite(jump.RawExit)?jump.RawExit:jump.Exit)-rawEntry);
            if(!IsFinite(admission)||axis.sqrMagnitude<0.5f)return false;
            Vector3 offset=bot.Position-admission;offset.y=0f;
            float forward=Vector3.Dot(offset,axis),lateral=(offset-axis*forward).magnitude;
            var plan=FollowerCapabilityActuationPolicy.PrepareJump(true,IsFinite(target.Value),HasActiveTraversalProbe(bot),
                forward,GetLearningV452JumpPreparationLeadDistance(GetLearningV329PlanarSpeed(bot.GetPlayer)),
                LearningV352JumpTakeoffCorridorMaxForwardOvershoot,lateral,LearningV352JumpTakeoffCorridorMaxLateral,
                Vector3.Dot(GetFlatDirection(movementDirection),axis));
            forwardOnly=plan.ForwardMotion;
            if(plan.AxisFacing&&ShouldEmitDiagnostic("capabilityJumpPreparation:"+jump.EpisodeId,bot,0.12f))
                WriteEvent("capabilityJumpPreparationIntent",bot,new{missionId=mission.MissionId,stepIndex=mission.CurrentStepIndex,
                    episodeId=jump.EpisodeId,edgeId=jump.EdgeId,forward,lateral,forwardOnly,axis=Vec(axis),
                    requestedDirection=Vec(movementDirection),target=Vec(target.Value),nativeAdmissionChanged=false});
            return plan.AxisFacing;
        }
    }
}
