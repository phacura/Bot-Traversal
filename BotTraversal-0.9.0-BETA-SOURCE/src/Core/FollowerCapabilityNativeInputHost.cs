// R3U_NATIVE_INPUT_EXTENSION_REVIEWED
using EFT;
using UnityEngine;
using Input = BotTraversal.Core.FollowerCapabilityNativeInputPolicy;
namespace BotTraversal.Core
{
    internal static partial class FollowerTraversalTestRuntime
    {
        // The single existing native Player.Move producer is shared. The issued
        // writer gateway retains authorization and records input, never body progress.
        private static bool CommitCapabilityNativePlayerInput(BotOwner bot,Player player,Vector2 input,bool refreshReceipt=true)
        {
            bool accepted=FollowerMovementWriterAuthority.TryCommitNativePlayerMove(bot,player,input);
            if(accepted&&refreshReceipt&&TryGetActiveFollowerRouteMission(bot,out var mission)&&mission.CurrentStepIndex>=0&&mission.CurrentStepIndex<mission.Steps.Count)
            {
                var step=mission.Steps[mission.CurrentStepIndex];
                step.CapabilityInputOrigin=LifecycleIdentity(mission,step,mission.CurrentStepIndex,FollowerMovementWriterAuthority.PeekExactGeneration(bot,out _));
                step.CapabilityInputAt=Time.time;step.CapabilityInputVector=input;
            }
            return accepted;
        }
        private sealed partial class CapabilityLocomotionPort : ICapabilityNativeInputPort
        {
            private BaseMovementState observedIdle,observedLocomotion;
            internal bool? NativeInertObserved,NativeInertAfterInput;
            internal bool CanWalkObserved,InputObserved;
            internal Vector2 ObservedInput;
            private static bool? ReadNativeInert(IAnimator animator)
            {
                try { return animator.GetBool(PlayerAnimator.INERT_PARAM_HASH); }
                catch { return null; }
            }
            public Input.Snapshot CaptureInput()
            {
                var managed=player.CurrentManagedState;
                bool locomotion=context.CurrentState!=null &&
                    (context.CurrentState.Name==EPlayerState.Run||context.CurrentState.Name==EPlayerState.Sprint) &&
                    context.States.TryGetValue(context.CurrentState.Name,out observedLocomotion) &&
                    ReferenceEquals(context.CurrentState,observedLocomotion)&&ReferenceEquals(managed,observedLocomotion);
                bool idle=context.CurrentState is IdleStateClass&&context.States.TryGetValue(EPlayerState.Idle,out observedIdle)&&
                    ReferenceEquals(context.CurrentState,observedIdle)&&ReferenceEquals(managed,observedIdle);
                InputObserved=true;
                CanWalkObserved=context.CanWalk;
                ObservedInput=player.InputDirection;
                NativeInertObserved=ReadNativeInert(animator);
                return new Input.Snapshot(IsCurrent(capturedOrigin),idle,CanWalkObserved,ObservedInput.x,ObservedInput.y,locomotion);
            }
            public bool RequestInput(Input.Snapshot observed)
            {
                var managed=player.CurrentManagedState;
                var expectedConsumer=observed.LocomotionConsumer?observedLocomotion:observedIdle;
                var follower=BossPlayers.GetFollowerByProfileId(bot.ProfileId);
                if(follower==null||!IsLiveBot(bot)||
                    (follower.TryPeekActiveCommand(out var command,out _,out _)&&command!=FollowerCommandType.None)||
                    context.OverridenState!=null||context.OverridenControlsState!=null||context.IgnoreDeltaMovement||
                    FollowerMedical.IsUsingMedical(bot)||FollowerMedical.IsHandsInteractionActiveForAuthority(player)||IsTeleportTransactionActive(bot)||
                    FollowerEnvironment.TryGetImmediateSurvivalThreat(bot,out _)||FollowerStateAuthorityCoordinator.HasCombatMovementClaim(bot,out _)||
                    !IsCurrent(capturedOrigin)||!ReferenceEquals(context.CurrentState,expectedConsumer)||!ReferenceEquals(managed,expectedConsumer)||
                    (observed.IdleConsumer&&!(context.CurrentState is IdleStateClass))||
                    (observed.LocomotionConsumer&&(expectedConsumer==null||
                        (expectedConsumer.Name!=EPlayerState.Run&&expectedConsumer.Name!=EPlayerState.Sprint)))||!context.CanWalk||
                    player.InputDirection.x!=observed.X||player.InputDirection.y!=observed.Y||
                    Input.Evaluate(observed)!=Input.Action.Publish)return false;
                if(!CommitCapabilityNativePlayerInput(bot,player,new Vector2(observed.X,observed.Y),false))return false;
                NativeInertAfterInput=ReadNativeInert(animator);
                return true;
            }
        }
        private static void PrepareCapabilityDropIngressLocomotion(BotOwner bot,Player player,CapabilityMotionPurpose purpose)
        {
            if(purpose!=CapabilityMotionPurpose.FollowIngress||player?.MovementContext==null||player.CurrentStateName!=EPlayerState.Idle||
                !player.MovementContext.IsGrounded||!player.MovementContext.CanWalk||HasActiveTraversalProbe(bot)||!IsCapabilityRouteRetrySafe(bot)||
                !TryGetActiveFollowerRouteMission(bot,out var mission)||mission.CurrentStepIndex<0||mission.CurrentStepIndex>=mission.Steps.Count||
                mission.Steps[mission.CurrentStepIndex]?.Kind!="DropDown"||!player.MovementContext.States.TryGetValue(EPlayerState.Run,out var run))return;
            RequestCapabilityLocomotionResume(bot,player,run);
        }
        private static void HoldForwardInput(ProbeRuntime probe)
        {
            if (probe?.Player?.MovementContext == null)
            {
                return;
            }

            probe.Player.SetPlayerInputDirection("R1-959ED2C135FB",@probe?.Bot,Vector2.up);
            probe.Player.MovementContext.SetMovementContextMovementDirection("R1-364AB781B058",@probe?.Bot,Vector2.up);
            CommitCapabilityNativePlayerInput(probe.Bot,probe.Player,Vector2.up);
        }

        private static bool TryMaintainCapabilityNativeInput(BotOwner bot,Player player,bool atNativeConsumer=false)
        {
            if(IsCapabilityPortalWaiting(bot))
            {
                if(bot!=null&&ReferenceEquals(bot.GetPlayer,player)&&IsCapabilityRouteRetrySafe(bot)&&!HasActiveTraversalProbe(bot))
                {
                    long gen=FollowerMovementWriterAuthority.PeekExactGeneration(bot,out var queueOwner);
                    if(gen>0&&(queueOwner==FollowerMovementWriterAuthority.WriterDomain.Follow||queueOwner==FollowerMovementWriterAuthority.WriterDomain.Traversal)&&
                        FollowerMovementWriterAuthority.TryEnterBoundPhysicalMotion(bot,queueOwner,gen,out var holdScope,out _))
                        using(holdScope)CommitCapabilityPortalHold(bot,FollowerRouteMissions[bot.ProfileId]);
                }
                return true;
            }
            if(TryMaintainCapabilityRouteRetryHold(bot,player))return true;
            if(bot==null||player?.MovementContext==null||
                (player.CurrentStateName!=EPlayerState.Run&&player.CurrentStateName!=EPlayerState.Sprint&&
                 player.CurrentStateName!=EPlayerState.Idle)||
                !TryGetActiveFollowerRouteMission(bot,out var mission)||mission?.Steps==null||
                mission.CurrentStepIndex<0||mission.CurrentStepIndex>=mission.Steps.Count||
                !player.MovementContext.States.TryGetValue(EPlayerState.Run,out var run)||run==null)return false;
            var step=mission.Steps[mission.CurrentStepIndex];
            long generation=FollowerMovementWriterAuthority.PeekExactGeneration(bot,out var owner);
            if(generation<=0||(owner!=FollowerMovementWriterAuthority.WriterDomain.Traversal&&
                owner!=FollowerMovementWriterAuthority.WriterDomain.Follow))return false;
            if(!TryValidateV2CurrentTypedStep(bot,mission,step,false,out _)||Time.time>mission.ExpiresAt)return false;
            if(TryGetActiveProbe(bot,out var probe))
            {
                if(owner!=FollowerMovementWriterAuthority.WriterDomain.Traversal||!IsActiveProbe(probe)||
                    !IsProbeLifecycleCurrent(probe)||probe.MovementWriterGeneration!=generation||
                    !ReferenceEquals(probe.Player,player)||!ReferenceEquals(probe.LifecycleMission,mission)||
                    !ReferenceEquals(probe.LifecycleStep,step))return false;
            }
            else if(!TryValidateV2CurrentTypedStep(bot,mission,step,true,out _))return false;
            // Maintenance consumes a producer receipt; it must never renew its own deadline.
            if(!FollowerCapabilityPassagePolicy.FreshInput(step.CapabilityInputOrigin.Equals(LifecycleIdentity(mission,step,mission.CurrentStepIndex,generation)),
                bot.Mover?.HasPathAndNoComplete==true,HasCurrentCapabilityNativeWay(bot,out _),IsCapabilityReturnInProgress(bot),
                mission.FailureSequence?.Pending==true,Time.time-step.CapabilityInputAt,(player.InputDirection-step.CapabilityInputVector).sqrMagnitude))return false;
            bool entered=FollowerMovementWriterAuthority.TryEnterBoundPhysicalMotion(bot,owner,generation,out var scope,out _);
            if(!entered)return false;
            using(scope)
            {
                var port=new CapabilityLocomotionPort(bot,player,mission,step,run);
                var snapshot=port.Capture();
                if(FollowerCapabilityLocomotionPolicy.Evaluate(snapshot)==FollowerCapabilityLocomotionPolicy.Action.Reject)return false;
                bool accepted=FollowerCapabilityNativeInputDriver.Commit(port)==FollowerCapabilityNativeInputDriver.Receipt.InputRequested;
                if(accepted&&ShouldEmitDiagnostic("capabilityNativeInputMaintained:"+step.EpisodeId,bot,.50f))
                    WriteEvent("capabilityNativeInputMaintained",bot,new{missionId=mission.MissionId,episodeId=step.EpisodeId,
                        edgeId=step.EdgeId,state=player.CurrentStateName.ToString(),nativeInput=Vec2(port.ObservedInput),
                        nativeInert=port.NativeInertObserved,nativeInertAfterInput=port.NativeInertAfterInput,
                        nativeConsumerInvoked=true,atNativeConsumer,nativeEvaluationFrame=Time.frameCount,
                        nativeDirection=player.CurrentManagedState is MovementState nativeState?Vec2(nativeState.Direction):null,
                        nativeSuccessRecorded=false});
                return accepted;
            }
        }
    }
}
