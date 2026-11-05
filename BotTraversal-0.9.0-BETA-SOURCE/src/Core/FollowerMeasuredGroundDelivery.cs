using System;
using System.Linq;
using EFT;
using UnityEngine;
using UnityEngine.AI;
using Policy = BotTraversal.Core.FollowerCapabilityDeliveryPolicy;
using Motion = BotTraversal.Core.FollowerCapabilityMotionPolicy;
namespace BotTraversal.Core
{
    internal static partial class FollowerTraversalTestRuntime
    {
        // One delivery adapter for an explicit existing intent. It never supplies
        // landing proof or invents a new route/owner/probe/clock.
        private static bool TryApplyMeasuredGroundDelivery(BotOwner bot,Player player,out CapabilityMotionCommit commit,
            Vector3? requestedTarget=null,CapabilityMotionPurpose purpose=CapabilityMotionPurpose.NativeProbe,
            Vector3 requestedDirection=default(Vector3),bool intentMoving=true,float pose=1f,float targetMoveSpeed=1f)
        {
            commit=CapabilityMotionCommit.Rejected;
            if (!requestedTarget.HasValue || purpose==CapabilityMotionPurpose.NativeProbe || bot?.Mover==null ||
                player?.MovementContext==null || !TryGetActiveFollowerRouteMission(bot,out var mission) ||
                mission.CurrentStepIndex<0 || mission.CurrentStepIndex>=mission.Steps.Count) return false;
            var step=mission.Steps[mission.CurrentStepIndex];
            ProbeRuntime executionProbe=null;
            bool nativeExecution=purpose==CapabilityMotionPurpose.NativeExecution &&
                TryGetCurrentCapabilityNativeExecution(bot,mission,step,out executionProbe);
            if(purpose==CapabilityMotionPurpose.NativeExecution&&!nativeExecution)return false;
            if (!IsCapabilityDeliveryOriginCurrent(bot,mission,step,executionProbe) ||
                !FollowerMovementWriterAuthority.CanCommitScopedPhysicalMotion(bot,out _)) return false;
            var observation=CaptureRouteDebtObservation(bot,mission,step);
            var kind=Policy.Parse(step.Kind);
            if(kind!=Policy.Capability.WalkThrough&&kind!=Policy.Capability.DropDown)return false;
            Vector3 goal=requestedTarget.Value;
            if(!IsFinite(goal))return false;
            if(nativeExecution&&(executionProbe.FrozenTraversalCommitment==null ||
                !IsFinite(executionProbe.FrozenTraversalCommitment.RawExit) ||
                Vector3.Distance(goal,executionProbe.FrozenTraversalCommitment.RawExit)>0.01f))return false;
            Vector3 body=player.Position;
            Vector3 rawEntry=IsFinite(step.RawEntry)?step.RawEntry:step.Entry;
            Vector3 rawExit=IsFinite(step.RawExit)?step.RawExit:step.Exit;
            Vector3 axis=GetFlatDirection(rawExit-rawEntry);
            Vector3 offset=body-rawEntry;offset.y=0f;
            float forward=axis.sqrMagnitude>0.5f?Vector3.Dot(offset,axis):float.NegativeInfinity;
            float lateral=axis.sqrMagnitude>0.5f?(offset-axis*forward).magnitude:float.PositiveInfinity;
            bool prior=mission.CurrentStepIndex>0 && mission.Steps[mission.CurrentStepIndex-1].Lifecycle.PhysicallyResolved &&
                mission.Steps[mission.CurrentStepIndex-1].ChainId==step.ChainId;
            bool missedEntry=kind==Policy.Capability.DropDown&&prior&&Motion.MissedDropEntry(forward,lateral,body.y-rawEntry.y,
                NativeDropDownEntryMaxForwardOvershoot,NativeDropDownEntryMaxLateralOffset,DropDownTopEntryMaxVerticalSampleOffset);
            ObserveV2CurrentStepPhysicalProgressReceipt(bot,mission,step,goal,"CapabilityMotionDelivery",false,false);
            float noProgress=nativeExecution?Mathf.Max(0f,Time.time-executionProbe.NativeDropDownLastProgressAt):
                (step.V2PhysicalReceiptHasAnchor?Mathf.Max(0f,Time.time-step.V2PhysicalReceiptLastProgressAt):0f);
            Vector3 flat=goal-body;flat.y=0f;float distance=flat.magnitude;
            Vector3 intent=GetFlatDirection(requestedDirection);
            bool aligned=purpose==CapabilityMotionPurpose.Recovery ||
                intent.sqrMagnitude>0.5f && flat.sqrMagnitude>0.000001f && Vector3.Dot(intent,flat.normalized)>=0.5f;
            var path=noProgress>=1f||bot.Mover.HasPathAndNoComplete?CalculateStrictPath(body,goal):null;
            bool strict=path!=null && path.StrictComplete && path.Corners!=null && path.Corners.Length>=2 &&
                path.PathLength<=RouteMissionTraversalAdjacentBridgeMaxDistance;
            Vector3? nativeTarget=bot.Mover.ActualPathController?.CurPath?.TargetPoint?.Position;
            bool ownsWay=HasCurrentCapabilityNativeWay(bot,out _);
            bool matching=ownsWay && bot.Mover.HasPathAndNoComplete && nativeTarget.HasValue &&
                (Vector3.Distance(nativeTarget.Value,goal)<=0.35f || strict&&Vector3.Distance(nativeTarget.Value,path.SampledTarget)<=0.35f);
            bool exact=ReferenceEquals(observation.Step,step)&&observation.Origin.Equals(
                LifecycleIdentity(mission,step,mission.CurrentStepIndex,observation.Origin.Writer));
            var ground=new Policy.Snapshot(kind,exact,HasActiveTraversalProbe(bot),player.MovementContext.IsGrounded,
                player.MovementContext.CanWalk,strict,GetLearningV354ActionProfile(step.Kind)?.RequiresActuator==true,
                !missedEntry,matching,Time.time>=mission.NextMoveAttemptAt,noProgress,
                nativeExecution?NativeDropDownNoProgressTimeout:LearnedTraversalMissedFallbackNoProgressSeconds);
            var plan=Motion.Evaluate(new Motion.Snapshot(ground,true,intentMoving,aligned,prior,missedEntry,
                goal.y<=body.y+DropDownTopEntryMaxVerticalSampleOffset,distance,RouteMissionTraversalAdjacentBridgeMaxDistance,ownsWay,nativeExecution));
            // Validate the captured origin before ANY resource retirement or native write.
            if(!IsCapabilityDeliveryOriginCurrent(bot,mission,step,executionProbe) ||
                !observation.Origin.Equals(LifecycleIdentity(mission,step,mission.CurrentStepIndex,observation.Origin.Writer)) ||
                FollowerMovementWriterAuthority.PeekExactGeneration(bot,out _)!=observation.Origin.Writer ||
                !FollowerMovementWriterAuthority.CanCommitScopedPhysicalMotion(bot,out _))return true;
            var transaction=FollowerCapabilityMotionTransactionPolicy.Evaluate(new FollowerCapabilityMotionTransactionPolicy.Snapshot(
                plan.Kind,plan.KeepExistingConsumer,ownsWay,exact,nativeTarget.HasValue));
            if(transaction.Kind==FollowerCapabilityMotionTransactionPolicy.Action.Reject)return true;
            if(ShouldEmitDiagnostic("capabilityMotionDecision:"+step.EpisodeId,bot,0.5f))
                WriteEvent("capabilityMotionDeliveryDecision",bot,new{missionId=mission.MissionId,stepIndex=mission.CurrentStepIndex,
                    episodeId=step.EpisodeId,edgeId=step.EdgeId,purpose=purpose.ToString(),mode=plan.Kind.ToString(),plan.Reason,
                    grounded=player.MovementContext.IsGrounded,canWalk=player.MovementContext.CanWalk,body=Vec(body),target=Vec(goal),
                    noProgress,forward,lateral,prior,missedEntry,strict,matching,distance,aligned,
                    velocity=Vec(player.Velocity),moverType=bot.Mover.GetType().FullName,
                    managedStateType=player.CurrentManagedState?.GetType().FullName,controllerType=player.CharacterController?.GetType().FullName});
            if(transaction.Kind==FollowerCapabilityMotionTransactionPolicy.Action.ReleaseToManual)
            {
                if(transaction.Retire){step.NativeMotionReceipt=null;TryRetireNativeMoverPathPreserveLocomotion(bot,"capabilityConsumerReleasedForManualFrame",out _);}
                return false;
            }
            if(transaction.Kind==FollowerCapabilityMotionTransactionPolicy.Action.RetireAndBlock)
            {
                if(transaction.Retire){step.NativeMotionReceipt=null;TryRetireNativeMoverPathPreserveLocomotion(bot,"capabilityConsumerInvalidated",out _);}
                commit=new CapabilityMotionCommit(CapabilityMotionCommitKind.Blocked);return true;
            }
            // A managed Run label is not an Animator locomotion consumer. Recover the
            // existing native animation only after measured no-progress and qualified support.
            if(!nativeExecution && prior && player.MovementContext.IsGrounded && noProgress>=1f &&
                player.MovementContext.States.TryGetValue(EPlayerState.Run,out var nativeRunState))
                RequestCapabilityLocomotionResume(bot,player,nativeRunState);
            bool maintain=transaction.Maintain;
            if(!IsCapabilityDeliveryOriginCurrent(bot,mission,step,executionProbe) ||
                !observation.Origin.Equals(LifecycleIdentity(mission,step,mission.CurrentStepIndex,observation.Origin.Writer)) ||
                FollowerMovementWriterAuthority.PeekExactGeneration(bot,out _)!=observation.Origin.Writer ||
                !FollowerMovementWriterAuthority.CanCommitScopedPhysicalMotion(bot,out _))return true;
            bool groundWay=plan.Kind==Motion.Delivery.PublishGroundWay;
            if(maintain&&!nativeTarget.HasValue)return true;
            Vector3 endpoint=maintain?nativeTarget.GetValueOrDefault():(groundWay?path.SampledTarget:goal);
            Vector3[] way=groundWay?(Vector3[])path.Corners.Clone():new[]{goal};
            float reach=groundWay?0.20f:0.05f;
            if(transaction.Install)
            {
                // Replace only an explicitly selected resource, and seed native
                // path bookkeeping from the current body, never from NavMesh.
                step.NativeMotionReceipt=null;
                if(!TryRetireNativeMoverPathPreserveLocomotion(bot,"capabilityNativeConsumerReplace",out _))return true;
                if(!IsCapabilityDeliveryOriginCurrent(bot,mission,step,executionProbe) ||
                    !observation.Origin.Equals(LifecycleIdentity(mission,step,mission.CurrentStepIndex,observation.Origin.Writer)) ||
                    FollowerMovementWriterAuthority.PeekExactGeneration(bot,out _)!=observation.Origin.Writer ||
                    !FollowerMovementWriterAuthority.CanCommitScopedPhysicalMotion(bot,out _))return true;
                bot.Mover.BotMoverGoToByWay("R3-A640E7288391",@bot,way,reach);
            }
            Vector3? installed=bot.Mover.ActualPathController?.CurPath?.TargetPoint?.Position;
            bool accepted=bot.Mover.HasPathAndNoComplete&&installed.HasValue&&Vector3.Distance(installed.Value,endpoint)<=0.05f;
            if(accepted){
                bot.Mover.SetBotMoverPause("R3-69D6D9636117",@bot,false);
                bot.Mover.BotMoverSetTargetMoveSpeed("R3-5A3271EE210C",@bot,targetMoveSpeed);
                bot.BotOwnerSetTargetMoveSpeed("R3-08C28E6A94C5",@bot,targetMoveSpeed);
                bot.BotOwnerSetPose("R3D-642A8378F946",@bot,Mathf.Clamp01(pose));
            }
            if(transaction.Install)
            {
                CommitCapabilityMoveRetry(mission,FollowerCapabilityRetryPolicy.Change.AttemptOrExplicitHold,Time.time+RouteMissionMoveRetrySeconds);
                if(accepted)step.NativeMotionReceipt=new CapabilityNativeWayReceipt(observation.Origin,bot.Mover.ActualPathController.CurPath,endpoint,executionProbe);
            }
            commit=new CapabilityMotionCommit(plan.Kind==Motion.Delivery.Blocked?CapabilityMotionCommitKind.Blocked:
                (accepted?CapabilityMotionCommitKind.NativeWayAccepted:CapabilityMotionCommitKind.Rejected),accepted);
            if(transaction.Install)WriteEvent("capabilityGroundDeliveryCommit",bot,new{missionId=mission.MissionId,stepIndex=mission.CurrentStepIndex,
                episodeId=step.EpisodeId,edgeId=step.EdgeId,purpose=purpose.ToString(),mode=plan.Kind.ToString(),
                writer=observation.Origin.Writer,geometryVersion=observation.Origin.Geometry,accepted,
                status=accepted?"nativeWayInstalled":"nativeWayRejected",groundProof=groundWay,
                grounded=player.MovementContext.IsGrounded,body=Vec(player.Position),target=Vec(endpoint),
                reach,installedTarget=installed.HasValue?Vec(installed.Value):null,
                canWalk=player.MovementContext.CanWalk,nativeExecution,pathCorners=way.Select(Vec).ToArray(),noProgress,retryAt=mission.NextMoveAttemptAt,
                nativeAttemptRecorded=false,physicalCompletionRecorded=false});
            return true;
        }
        private sealed class CapabilityNativeWayReceipt
        {
            internal readonly FollowerTraversalLifecycle.Identity Origin;
            internal readonly object NativePath;
            internal readonly Vector3 Goal;
            internal readonly ProbeRuntime ExecutionProbe;
            internal CapabilityNativeWayReceipt(FollowerTraversalLifecycle.Identity origin,object path,Vector3 goal,ProbeRuntime executionProbe=null)
            {Origin=origin;NativePath=path;Goal=goal;ExecutionProbe=executionProbe;}
        }
        // A receipt for the installed native resource, not a new owner or progress
        // registry. Step/geometry/raid/writer replacement invalidates it naturally.
        private static bool HasCurrentCapabilityNativeWay(BotOwner bot,out CapabilityNativeWayReceipt receipt)
        {
            receipt=null;
            if(bot?.Mover==null||!TryGetActiveFollowerRouteMission(bot,out var mission)||
                mission.CurrentStepIndex<0||mission.CurrentStepIndex>=mission.Steps.Count)return false;
            var step=mission.Steps[mission.CurrentStepIndex];var observed=step.NativeMotionReceipt;
            if(observed==null||!IsCapabilityDeliveryOriginCurrent(bot,mission,step,observed.ExecutionProbe)||
                !bot.Mover.HasPathAndNoComplete||!ReferenceEquals(observed.NativePath,bot.Mover.ActualPathController?.CurPath))return false;
            Vector3? endpoint=bot.Mover.ActualPathController.CurPath.TargetPoint?.Position;
            if(!endpoint.HasValue||Vector3.Distance(endpoint.Value,observed.Goal)>0.05f)return false;
            long writer=FollowerMovementWriterAuthority.PeekExactGeneration(bot,out _);
            if(!Motion.CanRetainConsumer(observed.Origin,LifecycleIdentity(mission,step,mission.CurrentStepIndex,writer),
                ReferenceEquals(observed.NativePath,bot.Mover.ActualPathController.CurPath),bot.Mover.HasPathAndNoComplete,HasActiveTraversalProbe(bot),observed.ExecutionProbe!=null))return false;
            receipt=observed;return true;
        }
        private static bool TryDeliverCurrentCapabilityRecovery(BotOwner bot,FollowerRouteMission mission)
        {
            if(bot?.GetPlayer?.MovementContext==null||mission==null||mission.CurrentStepIndex<0||mission.CurrentStepIndex>=mission.Steps.Count)return false;
            var step=mission.Steps[mission.CurrentStepIndex];
            if(!TryValidateV2CurrentTypedStep(bot,mission,step,true,out _))return false;
            if(!TryEnterV2CurrentIngressScope(bot,mission,step,"capabilityTerminalRecovery",out var scope,out _))return false;
            Vector3 goal=IsFinite(step.RawExit)?step.RawExit:step.Exit;
            using(scope)
            {
                var origin=CaptureRouteDebtObservation(bot,mission,step).Origin;
                bool handled=TryApplyMeasuredGroundDelivery(bot,bot.GetPlayer,out var recoveryCommit,goal,CapabilityMotionPurpose.Recovery,
                    GetFlatDirection(goal-bot.Position),true,1f,ProbeMomentumNormalMoveSpeed);
                bool ownsFrame=FollowerCapabilityMotionTransactionPolicy.RecoveryOwnsFrame(
                    handled,recoveryCommit,Time.time<mission.NextMoveAttemptAt);
                if(ownsFrame)return true;
                // Exhausted/no-progress delivery yields to the EXISTING bounded recovery.
                // Release only the resource belonging to the captured live identity.
                if(TryValidateV2CurrentTypedStep(bot,mission,step,true,out _) &&
                    origin.Equals(LifecycleIdentity(mission,step,mission.CurrentStepIndex,FollowerMovementWriterAuthority.PeekExactGeneration(bot,out _))) &&
                    FollowerMovementWriterAuthority.CanCommitScopedPhysicalMotion(bot,out _) &&
                    HasCurrentCapabilityNativeWay(bot,out var owned) && owned.Origin.Equals(origin))
                {
                    step.NativeMotionReceipt=null;
                    TryRetireNativeMoverPathPreserveLocomotion(bot,"capabilityRecoveryDeliveryExhausted",out _);
                }
                if(handled)WriteEvent("capabilityRecoveryDeliveryYielded",bot,new{missionId=mission.MissionId,
                    stepIndex=mission.CurrentStepIndex,episodeId=step.EpisodeId,receipt=recoveryCommit.Kind.ToString(),
                    nativeResourceOwned=recoveryCommit.NativeMoverOwned,existingRetryAt=mission.NextMoveAttemptAt,
                    traversalSuccessRecorded=false,action="blockedDeliveryDoesNotSwallowExistingBoundedRecovery"});
                return false;
            }
        }
    }
}
