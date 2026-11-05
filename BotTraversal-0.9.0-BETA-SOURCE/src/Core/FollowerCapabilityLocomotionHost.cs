using EFT;
using System;
using UnityEngine;
using Policy = BotTraversal.Core.FollowerCapabilityLocomotionPolicy;
using Driver = BotTraversal.Core.FollowerCapabilityLocomotionDriver;
namespace BotTraversal.Core
{
    internal static partial class FollowerTraversalTestRuntime
    {
        private static bool RequestCapabilityLocomotionResume(BotOwner bot,Player player,BaseMovementState run)
        {
            if(bot==null || player?.MovementContext==null || run==null ||
                !TryGetActiveFollowerRouteMission(bot,out var mission) || mission?.Steps==null ||
                mission.CurrentStepIndex<0 || mission.CurrentStepIndex>=mission.Steps.Count)return false;
            var step=mission.Steps[mission.CurrentStepIndex];
            var port=new CapabilityLocomotionPort(bot,player,mission,step,run);
            Driver.Receipt receipt;
            try { receipt=Driver.Commit(port); }
            catch(Exception ex) {
                if(ShouldEmitDiagnostic("capabilityLocomotionRequestFailed:"+step.EpisodeId,bot,0.5f))
                    WriteEvent("capabilityLocomotionRequestFailed",bot,new{error=ex.GetType().Name,managedStateForced=false});
                return false;
            }
            if(receipt==Driver.Receipt.AnimationRequested || receipt==Driver.Receipt.NativeInputRequested || receipt==Driver.Receipt.NativeStopInterrupted || ShouldEmitDiagnostic("capabilityLocomotionHandoff:"+step.EpisodeId,bot,0.5f))
                WriteEvent("capabilityLocomotionHandoff",bot,new{missionId=mission.MissionId,stepIndex=mission.CurrentStepIndex,
                    episodeId=step.EpisodeId,edgeId=step.EdgeId,receipt=receipt.ToString(),managedState=player.CurrentStateName.ToString(),
                    nativeCurrentHash=port.CurrentHash,nativeNextHash=port.NextHash,desiredHash=run.AnimatorStateHash,
                    nativeEvaluationFrame=Time.frameCount,outgoingStop=port.OutgoingStopObserved,nativeInputObserved=port.InputObserved,nativeInert=port.NativeInertObserved,nativeInertAfterInput=port.NativeInertAfterInput,canWalk=port.CanWalkObserved,nativeInput=Vec2(port.ObservedInput),nativeStateTime=port.CurrentStateTime,nativeDelta=Vec(port.NativeDelta),animatorEnabled=port.AnimatorEnabled,
                    animatorSpeed=port.AnimatorSpeed,ignoreDeltaMovement=player.MovementContext.IgnoreDeltaMovement,body=Vec(player.Position),
                    animatorType=player.MovementContext.PlayerAnimator?.Animator?.GetType().FullName,
                    managedStateForced=false,physicalProgressRecorded=false,traversalSuccessRecorded=false});
            return receipt!=Driver.Receipt.Rejected;
        }
        private sealed class CapabilityAnimationEvaluationReceipt
        {
            internal readonly FollowerTraversalLifecycle.Identity Origin;
            internal readonly IAnimator Animator;
            internal readonly int Frame,Hash;
            internal CapabilityAnimationEvaluationReceipt(FollowerTraversalLifecycle.Identity origin,IAnimator animator,int frame,int hash)
            {Origin=origin;Animator=animator;Frame=frame;Hash=hash;}
        }
        private sealed partial class CapabilityLocomotionPort : ICapabilityLocomotionPort
        {
            private readonly BotOwner bot;private readonly Player player;private readonly MovementContext context;
            private readonly FollowerRouteMission mission;private readonly FollowerRouteMissionStep step;
            private readonly BaseMovementState run;
            private IAnimator animator;
            private FollowerTraversalLifecycle.Identity capturedOrigin;
            internal int CurrentHash,NextHash,NextShortHash;
            internal float CurrentStateTime,AnimatorSpeed;
            internal Vector3 NativeDelta;
            internal bool AnimatorEnabled,OutgoingStopObserved,CurrentRunObserved,CoherentLocomotionObserved;
            internal CapabilityLocomotionPort(BotOwner b,Player p,FollowerRouteMission m,FollowerRouteMissionStep s,BaseMovementState r)
            {bot=b;player=p;context=p.MovementContext;mission=m;step=s;run=r;}
            public Policy.Snapshot Capture()
            {
                var origin=CaptureRouteDebtObservation(bot,mission,step).Origin;
                capturedOrigin=origin;
                long generation=FollowerMovementWriterAuthority.PeekExactGeneration(bot,out _);
                var live=LifecycleIdentity(mission,step,mission.CurrentStepIndex,generation);
                animator=context.PlayerAnimator?.Animator;
                bool coherent=false,pending=false;
                var state=player.CurrentStateName;
                bool locomotion=state==EPlayerState.Run||state==EPlayerState.Sprint;
                if(animator!=null && animator.layerCount>0)
                {
                    var current=animator.GetCurrentAnimatorStateInfo(0);
                    CurrentHash=current.fullPathHash;CurrentStateTime=current.normalizedTime;CurrentRunObserved=current.fullPathHash==run.AnimatorStateHash||current.shortNameHash==run.AnimatorStateHash;
                    NativeDelta=animator.deltaPosition;AnimatorEnabled=animator.enabled;AnimatorSpeed=animator.speed;
                    int liveHash=context.CurrentState?.AnimatorStateHash??0;
                    coherent=locomotion && (current.fullPathHash==liveHash||current.shortNameHash==liveHash);
                    // FastAnimator.Play queues a ForcePlayState for its NEXT native update.
                    // Observe that existing native queue; do not create a second request clock.
                    if(animator is FastAnimatorProcessorClass fast && fast.FastControllerInfo!=null)
                    {
                        var queued=fast.FastControllerInfo.GetStateInfo(0)?.ForcePlayState;
                        pending=queued!=null && queued.Hash==run.AnimatorStateHash;
                    }
                    bool transition=animator.IsInTransition(0),nextLocomotion=false;
                    if(transition)
                    {
                        var next=animator.GetNextAnimatorStateInfo(0);NextHash=next.fullPathHash;NextShortHash=next.shortNameHash;
                        pending=pending||next.fullPathHash==run.AnimatorStateHash||next.shortNameHash==run.AnimatorStateHash;
                        nextLocomotion=next.fullPathHash==run.AnimatorStateHash||next.shortNameHash==run.AnimatorStateHash;
                        if(context.States.TryGetValue(EPlayerState.Sprint,out var sprintState)&&sprintState!=null)
                            nextLocomotion=nextLocomotion||next.fullPathHash==sprintState.AnimatorStateHash||next.shortNameHash==sprintState.AnimatorStateHash;
                    }
                    OutgoingStopObserved=transition && context.States.TryGetValue(EPlayerState.Idle,out var idleState) && idleState!=null &&
                        (NextHash==idleState.AnimatorStateHash||NextShortHash==idleState.AnimatorStateHash);
                    coherent=FollowerCapabilityPhaseHandoffPolicy.Coherent(locomotion,coherent,transition,nextLocomotion);CoherentLocomotionObserved=coherent;
                }
                var issued=step.LocomotionRequest;
                if(issued!=null)pending=pending||FollowerCapabilityAnimationContinuityPolicy.SameEvaluation(
                    issued.Origin,origin,issued.Frame,Time.frameCount,issued.Hash,run.AnimatorStateHash,ReferenceEquals(issued.Animator,animator));
                bool survival=FollowerStateAuthorityCoordinator.HasCombatMovementClaim(bot,out _) ||
                    FollowerEnvironment.TryGetImmediateSurvivalThreat(bot,out _);
                bool qualified=IsCurrent(origin) && run.Name==EPlayerState.Run && run.AnimatorStateHash!=0 && run.AnimatorStateHash!=-1;
                return new Policy.Snapshot(origin,live,qualified,context.IsGrounded,
                    locomotion||state==EPlayerState.Idle||state==EPlayerState.JumpLanding||state==EPlayerState.Transition,
                    context.OverridenState!=null||context.OverridenControlsState!=null,IsTeleportTransactionActive(bot),survival,
                    animator!=null && animator.enabled && animator.layerCount>0,coherent,pending);
            }
            public bool IsCurrent(FollowerTraversalLifecycle.Identity origin)
            {
                return origin.Valid && ReferenceEquals(bot.GetPlayer,player) && ReferenceEquals(player.MovementContext,context) &&
                    ReferenceEquals(context.PlayerAnimator?.Animator,animator) &&
                    !mission.Lifecycle.Retired && mission.LifecycleRaid==raidGeneration && mission.LifecycleBotId==bot.ProfileId &&
                    FollowerRouteMissions.TryGetValue(bot.ProfileId,out var liveMission) && ReferenceEquals(liveMission,mission) &&
                    mission.CurrentStepIndex>=0 && mission.CurrentStepIndex<mission.Steps.Count && ReferenceEquals(mission.Steps[mission.CurrentStepIndex],step) &&
                    origin.Equals(LifecycleIdentity(mission,step,mission.CurrentStepIndex,FollowerMovementWriterAuthority.PeekExactGeneration(bot,out _))) &&
                    FollowerMovementWriterAuthority.CanCommitScopedPhysicalMotion(bot,out _);
            }

        }
    }
}
