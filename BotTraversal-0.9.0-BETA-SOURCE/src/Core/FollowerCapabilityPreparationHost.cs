using System;
using System.Linq;
using EFT;
using UnityEngine;
namespace BotTraversal.Core
{
    internal static partial class FollowerTraversalTestRuntime
    {
        // Dropdown boundary capture is shared by execution, verification and exit commit.
        private static bool TryCaptureCapabilityJumpPreparation(BotOwner bot,FollowerRouteMission mission,FollowerRouteMissionStep step,
            out Vector3 axis,out float forward,out float speed)
        {
            axis=default;forward=0f;speed=0f;
            var player=bot?.GetPlayer;var follower=bot==null?null:BossPlayers.GetFollowerByProfileId(bot.ProfileId);
            if(bot==null||player?.MovementContext==null||follower==null||!IsLiveBot(bot)||mission==null||mission.Lifecycle.Retired||
                Time.time>mission.ExpiresAt||mission.CurrentStepIndex<0||mission.CurrentStepIndex>=mission.Steps.Count||
                !ReferenceEquals(mission.Steps[mission.CurrentStepIndex],step)||step==null||HasActiveTraversalProbe(bot)||
                IsLearningV350TerminalPhysicalRecoveryEligible(bot)||IsTeleportTransactionActive(bot)||
                player.MovementContext.OverridenState!=null||player.MovementContext.OverridenControlsState!=null||
                HasCapabilityFailureStrategy(bot)||IsCapabilityReturnInProgress(bot)||
                !string.Equals(step.Kind,"Jump",StringComparison.OrdinalIgnoreCase)||IsRetiredCapabilityJumpPreparation(mission,step)||
                !TryValidateV2CurrentTypedStep(bot,mission,step,true,out _))return false;
            if(follower.TryPeekActiveCommand(out var command,out _,out _)&&command!=FollowerCommandType.None)return false;
            if(FollowerEnvironment.TryGetImmediateSurvivalThreat(bot,out _)||FollowerStateAuthorityCoordinator.HasCombatMovementClaim(bot,out _))return false;
            if(!LearnedTraversalCommitments.TryGetValue(bot.ProfileId,out var commitment)||commitment==null||
                commitment.RouteMissionId!=mission.MissionId||commitment.RouteMissionStepIndex!=mission.CurrentStepIndex||commitment.EdgeId!=step.EdgeId)return false;
            if(!TryGetLearningV460JumpAdmissionGeometry(bot,step,out _,out axis,out forward,out speed))return false;
            return true;
        }
        private static bool TryDriveCapabilityJumpPreparationConsumer(BotOwner bot,FollowerRouteMission mission,FollowerRouteMissionStep step)
        {
            if(!TryCaptureCapabilityJumpPreparation(bot,mission,step,out var axis,out float forward,out _))return false;
            var player=bot.GetPlayer;
            var delta=player.Position-GetLearningV442JumpAdmissionEntry(step);delta.y=0f;
            float lateral=(delta-axis*Vector3.Dot(delta,axis)).magnitude;
            bool centered=lateral<=LearningV352JumpTakeoffCorridorMaxLateral;
            bool facing=Vector3.Dot(GetFlatDirection(player.MovementContext.PlayerRealForward),axis)>=LearningV352JumpTakeoffCorridorMinFacingDot;
            bool local=forward>=-RouteMissionTraversalAdjacentBridgeMaxDistance&&forward<=LearningV352JumpTakeoffCorridorMaxForwardOvershoot;
            var approach=GetCapabilityJumpApproachPlan(player,local,lateral,forward);
            if(approach.Active)
            {
                long generation=FollowerMovementWriterAuthority.PeekExactGeneration(bot,out var owner);
                if((owner==FollowerMovementWriterAuthority.WriterDomain.Follow||owner==FollowerMovementWriterAuthority.WriterDomain.Traversal)&&
                    FollowerMovementWriterAuthority.TryEnterBoundPhysicalMotion(bot,owner,generation,out var scope,out _))
                {
                    using(scope)
                    {
                        if(!FollowerRouteMissions.TryGetValue(bot.ProfileId,out var live)||!ReferenceEquals(live,mission)||
                            mission.CurrentStepIndex<0||mission.CurrentStepIndex>=mission.Steps.Count||
                            !ReferenceEquals(mission.Steps[mission.CurrentStepIndex],step)||
                            !TryValidateV2CurrentTypedStep(bot,mission,step,true,out _))return false;
                        // One scoped consumer supplies centering and then the real run-up tangent.
                        var lateralVector=delta-axis*Vector3.Dot(delta,axis);
                        var inputDirection=GetFlatDirection(axis*approach.Forward-
                            (lateralVector.sqrMagnitude>.000001f?lateralVector.normalized:Vector3.zero)*(-approach.Lateral));
                        var receipt=ApplyCollisionActivePhysicalRouteMotion(bot,player,inputDirection,
                            inputDirection*(RollingTraversalMinSpeed*Mathf.Max(.001f,Time.deltaTime)),Mathf.Max(.001f,Time.deltaTime),1f,
                            sprint:!approach.Centering,targetMoveSpeed:approach.Speed,preserveSprintMode:!approach.Centering,
                            exactWorldDirectionInput:true,deliveryTarget:GetLearningV442JumpAdmissionEntry(step),deliveryPurpose:CapabilityMotionPurpose.PreActuator);
                        if(ShouldEmitDiagnostic("capabilityPreparationConsumer:"+step.EpisodeId,bot,.25f))
                            WriteEvent("capabilityPreparationConsumerFrame",bot,new{missionId=mission.MissionId,episodeId=step.EpisodeId,
                                edgeId=step.EdgeId,owner=owner.ToString(),generation,receipt=receipt.Kind.ToString(),forward,lateral,centering=approach.Centering,inputDirection=Vec(inputDirection),
                                state=player.CurrentStateName.ToString(),physicalSuccessRecorded=false});
                        return receipt.Accepted;
                    }
                }
            }
            return false;
        }
        private static bool TryDispatchCapabilityPreparedJump(BotOwner bot,FollowerRouteMission mission,FollowerRouteMissionStep step,string cadence)
        {
            if(HasCapabilityFailureStrategy(bot)||IsCapabilityReturnInProgress(bot)||
                !TryCaptureCapabilityJumpPreparation(bot,mission,step,out var axis,out float forward,out float speed))return false;
            if(forward < -GetLearningV452JumpPreparationLeadDistance(speed)||ShouldDeferCapabilityJumpProbe(bot,step.EdgeId,out _)||
                forward>LearningV352JumpTakeoffCorridorMaxForwardOvershoot)return false;
            bool started=TryStartProbeForBot(bot,axis,"learningV378PersistentActuatorFrontier",step.EdgeId,"Jump","capabilityPreparationFrame",out var result);
            if(started||ShouldEmitDiagnostic("capabilityPreparationFrame:"+step.EpisodeId,bot,.12f))
                WriteEvent("capabilityJumpPreparationFrameDispatch",bot,new{missionId=mission.MissionId,episodeId=step.EpisodeId,
                    edgeId=step.EdgeId,started,result,cadence,nativeSuccessRecorded=false});
            return started;
        }
        internal static void TryDispatchCapabilityJumpAtNativeUpdate(BotOwner bot)
        {
            if(!raidActive||bot==null||!BossPlayers.IsFollower(bot)||HasActiveTraversalProbe(bot)||
                !TryGetActiveFollowerRouteMission(bot,out var mission)||mission.CurrentStepIndex<0||mission.CurrentStepIndex>=mission.Steps.Count)return;
            TryDispatchCapabilityPreparedJump(bot,mission,mission.Steps[mission.CurrentStepIndex],"nativeMovement");
        }
        private static void TickCapabilityPreparationConsumerFrames()
        {
            foreach(var pair in FollowerRouteMissions.ToArray())
            {
                var mission=pair.Value;
                if(mission==null||mission.Lifecycle.Retired||Time.time>mission.ExpiresAt||
                    mission.CurrentStepIndex<0||mission.CurrentStepIndex>=mission.Steps.Count)continue;
                var bot=BossPlayers.GetFollowerByProfileId(pair.Key)?.GetBot();var step=mission.Steps[mission.CurrentStepIndex];
                if(TryRecoverCapabilityStalledJumpPreparation(bot,mission,step))continue;
                TryDriveCapabilityCompletedPhaseConsumer(bot,mission,step);TryDriveCapabilityJumpPreparationConsumer(bot,mission,step);
                TryDispatchCapabilityPreparedJump(bot,mission,step,"renderFrame");
            }
        }
        private readonly struct CapabilityPlanarBodySample
        {
            internal readonly Vector3 Position,PreviousPosition;
            internal readonly float At,PreviousAt;
            internal readonly long Generation;
            internal readonly int MissionId;
            internal CapabilityPlanarBodySample(Vector3 position,float at,Vector3 previous,float previousAt,long generation,int missionId)
            {Position=position;At=at;PreviousPosition=previous;PreviousAt=previousAt;Generation=generation;MissionId=missionId;}
        }
        private static void ObserveCapabilityPlanarBodyVelocity(FollowerTraversalRouteCursor cursor,Vector3 position,float now,long generation)
        {
            var sample=cursor.CapabilityBodyVelocity;
            if(sample.Generation!=generation||sample.MissionId!=cursor.LearningV375RouteMissionId||now<sample.At)
                cursor.CapabilityBodyVelocity=new CapabilityPlanarBodySample(position,now,position,now,generation,cursor.LearningV375RouteMissionId);
            else if(FollowerCapabilityApproachPolicy.RotateVelocityAnchor(now-sample.At))
                cursor.CapabilityBodyVelocity=new CapabilityPlanarBodySample(position,now,sample.Position,sample.At,generation,cursor.LearningV375RouteMissionId);
        }
        private static bool TryObserveCapabilityJumpBodyVelocity(Player player,out Vector3 measured,out float duration,out float age)
        {
            measured=Vector3.zero;duration=0f;age=0f;
            if(player?.MovementContext==null)return false;
            var bot=BossPlayers.GetFollowerByProfileId(player.ProfileId)?.GetBot();
            if(bot==null||!ReferenceEquals(bot.GetPlayer,player)||!IsCapabilityRouteRetrySafe(bot)||!TryGetActiveFollowerRouteMission(bot,out var mission)||
                mission.CurrentStepIndex<0||mission.CurrentStepIndex>=mission.Steps.Count||
                !FollowerTraversalRouteCursors.TryGetValue(bot.ProfileId,out var cursor)||cursor==null||
                !cursor.LearningV375TransactionActive||cursor.LearningV375RouteMissionId!=mission.MissionId)return false;
            var current=mission.Steps[mission.CurrentStepIndex];var jump=current;
            if(!TryValidateV2CurrentTypedStep(bot,mission,current,false,out _))return false;
            if(!string.Equals(current?.Kind,"Jump",StringComparison.OrdinalIgnoreCase))
            {
                if(!(IsLearningV369PhysicalCorridorStep(current)||string.Equals(current?.Kind,"WalkThrough",StringComparison.OrdinalIgnoreCase))||
                    mission.CurrentStepIndex+1>=mission.Steps.Count)return false;
                jump=mission.Steps[mission.CurrentStepIndex+1];
                if(!string.Equals(jump?.Kind,"Jump",StringComparison.OrdinalIgnoreCase)||
                    !TryValidateV2AdjacentPhysicalContinuationBoundary(bot,mission,current,jump,true,out _))return false;
            }
            if(TryGetActiveProbe(bot,out var probe)&&probe.JumpAttempted)return false;
            long generation=FollowerMovementWriterAuthority.PeekExactGeneration(bot,out var owner);
            var sample=cursor.CapabilityBodyVelocity;
            bool currentSample=owner==FollowerMovementWriterAuthority.WriterDomain.Traversal&&generation>0&&
                cursor.LearningV375MovementGeneration==generation&&sample.Generation==generation&&sample.MissionId==mission.MissionId;
            // Retain two passive anchors in the existing observer. A short native movement
            // tick must not turn an accumulated displacement into an alleged stable takeoff speed.
            duration=Time.time-sample.At;
            bool previous=FollowerCapabilityApproachPolicy.PreviousVelocityAnchor(duration);
            Vector3 origin=previous?sample.PreviousPosition:sample.Position;
            float originAt=previous?sample.PreviousAt:sample.At;
            duration=Time.time-originAt;
            age=Time.time-cursor.LearningV375LastBodySampleAt;
            measured=duration>0f?GetFlatDelta(origin,player.Position)/duration:Vector3.zero;
            return IsFinite(measured)&&FollowerCapabilityApproachPolicy.BodyVelocityObservation(currentSample,measured.magnitude,duration,age);
        }
        private static Vector3 GetCapabilityJumpPlanarVelocity(Player player)
        {
            if(player==null)return Vector3.zero;
            Vector3 native=player.Velocity;native.y=0f;
            if(!IsFinite(native)||!TryObserveCapabilityJumpBodyVelocity(player,out var measured,out float duration,out float age))return native;
            var bot=BossPlayers.GetFollowerByProfileId(player.ProfileId)?.GetBot();
            TryGetActiveFollowerRouteMission(bot,out var observedMission);
            int observedIndex=observedMission?.CurrentStepIndex??-1;
            var observedStep=observedIndex>=0&&observedIndex<observedMission.Steps.Count?observedMission.Steps[observedIndex]:null;
            if(observedStep!=null&&!string.Equals(observedStep.Kind,"Jump",StringComparison.OrdinalIgnoreCase)&&observedIndex+1<observedMission.Steps.Count)
                observedStep=observedMission.Steps[observedIndex+1];
            long observedGeneration=FollowerMovementWriterAuthority.PeekExactGeneration(bot,out _);
            if(Mathf.Abs(native.magnitude-measured.magnitude)>1f&&ShouldEmitDiagnostic("rc119BodyVelocity",bot,.15f))
                WriteEvent("capabilityJumpVelocityReconciled",bot,new{missionId=observedMission?.MissionId??0,edgeId=observedStep?.EdgeId??0,generation=observedGeneration,
                    nativeVelocity=Vec(native),measuredVelocity=Vec(measured),
                    nativeSpeed=native.magnitude,measuredSpeed=measured.magnitude,duration,age,controller=player.CharacterController?.GetType().Name,
                    state=player.CurrentStateName.ToString(),grounded=player.MovementContext.IsGrounded,nativeAdmissionGranted=false,nativeSuccessRecorded=false});
            return measured;
        }
        private static void TryCarryCapabilityJumpBodyObservation(ProbeRuntime probe,FollowerRouteMission mission,FollowerTraversalRouteCursor cursor,
            long oldGeneration,long generation,FollowerMovementWriterAuthority.WriterDomain oldOwner,string source)
        {
            if(probe?.Player==null||mission==null||cursor==null||source!="probeStart"||
                !string.Equals(probe.RequestedTraversalKind,"Jump",StringComparison.OrdinalIgnoreCase)||!IsCapabilityRouteRetrySafe(probe.Bot)||
                mission.CurrentStepIndex<0||mission.CurrentStepIndex>=mission.Steps.Count)return;
            var step=mission.Steps[mission.CurrentStepIndex];var sample=cursor.CapabilityBodyVelocity;
            bool exact=step.EdgeId==probe.LearnedEdgeId&&step.EpisodeId==probe.TraversalEpisodeId&&
                TryValidateV2CurrentTypedStep(probe.Bot,mission,step,false,out _)&&cursor.LearningV375TransactionActive&&
                cursor.LearningV375MovementGeneration==oldGeneration&&sample.Generation==oldGeneration&&sample.MissionId==mission.MissionId;
            if(!FollowerCapabilityApproachPolicy.CarryBodyObservation(cursor.LearningV375RouteMissionId==mission.MissionId,exact,
                oldOwner==FollowerMovementWriterAuthority.WriterDomain.Traversal,oldGeneration,generation,
                Time.time-cursor.LearningV375LastBodySampleAt,GetPlanarDistance(cursor.LearningV375LastBodyPosition,probe.Player.Position),
                Mathf.Abs(cursor.LearningV375LastBodyPosition.y-probe.Player.Position.y)))return;
            cursor.CapabilityBodyVelocity=new CapabilityPlanarBodySample(sample.Position,sample.At,sample.PreviousPosition,sample.PreviousAt,generation,mission.MissionId);
            WriteEvent("capabilityJumpBodyObservationTransferred",probe.Bot,new{missionId=mission.MissionId,stepIndex=mission.CurrentStepIndex,
                edgeId=step.EdgeId,oldGeneration,generation,position=Vec(probe.Player.Position),oldWriterReused=false,nativeSuccessRecorded=false});
        }
        private static bool IsCapabilityWalkPrefixForJump(BotOwner bot,FollowerRouteMission mission,FollowerRouteMissionStep step)
        {
            if(step==null||!string.Equals(step.Kind,"WalkThrough",StringComparison.OrdinalIgnoreCase)||
                !TryValidateV2CurrentTypedStep(bot,mission,step,true,out _)||mission.CurrentStepIndex+1>=mission.Steps.Count)return false;
            var jump=mission.Steps[mission.CurrentStepIndex+1];
            if(!string.Equals(jump?.Kind,"Jump",StringComparison.OrdinalIgnoreCase)||
                !TryValidateV2AdjacentPhysicalContinuationBoundary(bot,mission,step,jump,true,out _)||
                !TryGetLearningV460JumpAdmissionGeometry(bot,jump,out var entry,out var axis,out _,out _))return false;
            var delta=GetFlatDelta(step.RawEntry,step.RawExit);float span=delta.magnitude;var wtAxis=GetFlatDirection(delta);
            var toEntry=GetFlatDelta(step.RawEntry,entry);float along=Vector3.Dot(toEntry,wtAxis);
            var body=GetFlatDelta(step.RawEntry,bot.Position);float bodyAlong=Vector3.Dot(body,wtAxis);
            bool eligible=FollowerCapabilityApproachPolicy.WalkPrefixJump(step.CurrentRaidRouteAuthority&&jump.CurrentRaidRouteAuthority,
                step.ChainId>0&&step.ChainId==jump.ChainId,Vector3.Dot(wtAxis,axis),along,span,Vector3.Dot(GetFlatDelta(entry,step.RawExit),axis),
                bodyAlong,(body-wtAxis*bodyAlong).magnitude,(toEntry-wtAxis*along).magnitude,Mathf.Abs(bot.Position.y-entry.y));
            if(eligible&&ShouldEmitDiagnostic("rc119WalkPrefix:"+jump.EpisodeId,bot,.15f))
                WriteEvent("capabilityWalkPrefixJumpBoundaryObserved",bot,new{missionId=mission.MissionId,edgeId=step.EdgeId,jumpEdge=jump.EdgeId,
                    bodyAlong,along,span,body=Vec(bot.Position),nativeWalkThroughSuccessRecorded=false,rawGeometryChanged=false});
            return eligible;
        }
        private static bool HasCapabilityMeasuredMissedJumpWindow(BotOwner bot,FollowerRouteMission mission,FollowerRouteMissionStep step,float forward)
        {
            if(forward<=LearningV352JumpTakeoffCorridorMaxForwardOvershoot||HasActiveTraversalProbe(bot)||
                !TryObserveCapabilityJumpBodyVelocity(bot.GetPlayer,out _,out _,out _)||
                !FollowerTraversalRouteCursors.TryGetValue(bot.ProfileId,out var cursor))return false;
            var sample=cursor.CapabilityBodyVelocity;var entry=GetLearningV442JumpAdmissionEntry(step);var axis=GetFlatDirection(step.RawExit-step.RawEntry);
            var before=GetFlatDelta(entry,sample.PreviousPosition);var now=GetFlatDelta(entry,bot.Position);
            return FollowerCapabilityApproachPolicy.CrossedJumpWindow(step.CurrentRaidRouteAuthority,Vector3.Dot(before,axis),forward,
                Time.time-sample.PreviousAt,(now-axis*forward).magnitude)||
                FollowerCapabilityApproachPolicy.MissedJumpWindow(step.CurrentRaidRouteAuthority,forward,
                    (now-axis*forward).magnitude,Time.time-sample.PreviousAt);
        }
        private static bool IsCapabilityColdRunJumpTrialReady(BotOwner bot,FollowerRouteMissionStep jump)
        {
            if(bot?.GetPlayer?.MovementContext==null||jump==null||!jump.CurrentRaidRouteAuthority||
                jump.CapabilityColdRunTrialConsumed||
                !string.Equals(jump.Kind,"Jump",StringComparison.OrdinalIgnoreCase)||!IsCapabilityRouteRetrySafe(bot)||
                !TryGetActiveFollowerRouteMission(bot,out var mission)||mission.CurrentStepIndex<0||mission.CurrentStepIndex>=mission.Steps.Count)return false;
            var episode=TraversalEpisodeStream.LastOrDefault(x=>x!=null&&x.EpisodeId==jump.EpisodeId);
            if(episode==null||episode.EdgeId!=jump.EdgeId||!string.Equals(episode.Kind,"Jump",StringComparison.OrdinalIgnoreCase)||
                !IsFinite(episode.RawStart)||!IsFinite(episode.RawEnd)||
                GetPlanarDistance(episode.RawStart,GetLearningV442JumpAdmissionEntry(jump))>.55f||
                Vector3.Dot(GetFlatDirection(episode.RawEnd-episode.RawStart),GetFlatDirection(jump.RawExit-jump.RawEntry))<.94f)return false;
            var current=mission.Steps[mission.CurrentStepIndex];
            bool identity=ReferenceEquals(current,jump)?TryValidateV2CurrentTypedStep(bot,mission,jump,false,out _):
                (IsLearningV369PhysicalCorridorStep(current)||string.Equals(current?.Kind,"WalkThrough",StringComparison.OrdinalIgnoreCase))&&
                TryValidateV2CurrentTypedStep(bot,mission,current,false,out _)&&
                TryValidateV2AdjacentPhysicalContinuationBoundary(bot,mission,current,jump,true,out _);
            if(!identity||!TryGetLearningV460JumpAdmissionGeometry(bot,jump,out var entry,out var axis,out float forward,out float speed)||
                IsLearningV414FollowerRunJumpCapabilityReadyForEdge(jump.EdgeId,speed,out _,out int priorRunSamples)||
                IsLearningV418PlayerProvenRunJumpTrialReadyForEdge(jump.EdgeId,speed,out _,out int playerRunSamples)||
                priorRunSamples>0||playerRunSamples>0)return false;
            var player=bot.GetPlayer;var context=player.MovementContext;var delta=GetFlatDelta(entry,player.Position);
            var velocity=GetCapabilityJumpPlanarVelocity(player);velocity.y=0f;
            float lateral=(delta-axis*forward).magnitude;
            float facing=Vector3.Dot(GetFlatDirection(context.PlayerRealForward),axis),heading=Vector3.Dot(GetFlatDirection(velocity),axis);
            bool admitted=FollowerCapabilityApproachPolicy.ColdRunTrial(true,jump.CapabilityColdRunTrialConsumed,
                player.CurrentStateName==EPlayerState.Run,context.IsGrounded,context.CanJump,
                forward,GetLearningV452JumpPreparationLeadDistance(speed),lateral,facing,heading,speed);
            if(forward>=-GetLearningV452JumpPreparationLeadDistance(speed)&&forward<=.16f&&
                ShouldEmitDiagnostic("rc117ColdRunDecision:"+jump.EpisodeId,bot,.25f))
                WriteEvent("capabilityColdRunJumpTrialDecision",bot,new{missionId=mission.MissionId,edgeId=jump.EdgeId,
                    admitted,state=player.CurrentStateName.ToString(),grounded=context.IsGrounded,canJump=context.CanJump,
                    forward,lateral,facing,heading,speed,trialConsumed=jump.CapabilityColdRunTrialConsumed,nativeSuccessRecorded=false});
            return admitted;
        }
        private static void TryConsumeCapabilityColdRunJumpTrial(ProbeRuntime probe)
        {
            if(!IsCapabilityColdRunJumpTrialReady(probe?.Bot,probe?.LifecycleStep))return;
            probe.LifecycleStep.CapabilityColdRunTrialConsumed=true;
            WriteEvent("capabilityColdRunJumpTrialConsumed",probe.Bot,new{missionId=probe.LifecycleMission.MissionId,
                edgeId=probe.LifecycleStep.EdgeId,speed=GetLearningV329PlanarSpeed(probe.Player),
                playerRunEvidenceFabricated=false,nativeSuccessRecorded=false});
        }
        private static bool TryObserveCapabilityJumpContactWindow(BotOwner bot,float expectedForward,float speed)
        {
            var player=bot?.GetPlayer;var context=player?.MovementContext;
            if(context==null||!context.IsGrounded||!context.CanJump||!IsCapabilityRouteRetrySafe(bot)||
                (player.CurrentStateName!=EPlayerState.Run&&player.CurrentStateName!=EPlayerState.Sprint))return false;
            Vector3 entry,axis;
            if(TryGetActiveProbe(bot,out var probe)&&IsActiveProbe(probe)&&ReferenceEquals(probe.Player,player)&&
                string.Equals(probe.RequestedTraversalKind,"Jump",StringComparison.OrdinalIgnoreCase)&&
                TryGetLearnedCommitmentForProbe(probe,out var commitment)&&commitment!=null)
            {entry=GetLearningV442JumpAdmissionEntry(commitment);axis=GetFlatDirection(commitment.RawExit-commitment.RawEntry);}
            else
            {
                if(!TryGetActiveFollowerRouteMission(bot,out var mission)||mission.CurrentStepIndex<0||mission.CurrentStepIndex>=mission.Steps.Count)return false;
                var current=mission.Steps[mission.CurrentStepIndex];var jump=current;
                if(!string.Equals(current?.Kind,"Jump",StringComparison.OrdinalIgnoreCase))
                {
                    if(mission.CurrentStepIndex+1>=mission.Steps.Count)return false;
                    jump=mission.Steps[mission.CurrentStepIndex+1];
                    if(!TryValidateV2AdjacentPhysicalContinuationBoundary(bot,mission,current,jump,true,out _))return false;
                }
                if(!TryValidateV2CurrentTypedStep(bot,mission,current,false,out _)||
                    !TryGetLearningV460JumpAdmissionGeometry(bot,jump,out entry,out axis,out _,out _))return false;
            }
            if(!IsFinite(entry)||axis.sqrMagnitude<.5f)return false;
            var delta=GetFlatDelta(entry,player.Position);float forward=Vector3.Dot(delta,axis);
            if(Mathf.Abs(forward-expectedForward)>.04f||forward<-.55f||forward>.16f||speed<RollingTraversalMinSpeed)return false;
            if(!TryGetFirstNonSelfCapsuleCastHit(player,player.Position+Vector3.up*WalkThroughCapsuleBottom,
                player.Position+Vector3.up*WalkThroughCapsuleTop,WalkThroughCapsuleRadius,axis,.15f,
                LayerMaskClass.HighPolyWithTerrainMask,out var hit))return false;
            var velocity=GetCapabilityJumpPlanarVelocity(player);velocity.y=0f;
            bool ready=FollowerCapabilityApproachPolicy.JumpContact(forward,(delta-axis*forward).magnitude,
                Vector3.Dot(GetFlatDirection(context.PlayerRealForward),axis),Vector3.Dot(GetFlatDirection(velocity),axis),speed,
                hit.distance,hit.normal.y,Vector3.Dot(hit.normal,-axis));
            if(ready&&ShouldEmitDiagnostic("rc117JumpContact",bot,.15f))
                WriteEvent("capabilityJumpContactWindowObserved",bot,new{forward,speed,distance=hit.distance,
                    blocker=hit.collider?.name,normal=Vec(hit.normal),nativeSuccessRecorded=false});
            return ready;
        }
        private static bool TryGetCapabilityVaultIngressFacing(BotOwner bot,Vector3? target,CapabilityMotionPurpose purpose,out Vector3 axis,out bool centering)
        {
            axis=default;centering=false;
            if(!target.HasValue||(purpose!=CapabilityMotionPurpose.PreActuator&&purpose!=CapabilityMotionPurpose.FollowIngress)||HasActiveTraversalProbe(bot)||
                !TryGetActiveFollowerRouteMission(bot,out var mission)||mission.CurrentStepIndex<0||mission.CurrentStepIndex>=mission.Steps.Count)return false;
            var step=mission.Steps[mission.CurrentStepIndex];
            if(!string.Equals(step?.Kind,"Vault",StringComparison.OrdinalIgnoreCase)||
                !TryValidateV2CurrentTypedStep(bot,mission,step,true,out _)||!IsCapabilityRouteRetrySafe(bot))return false;
            axis=GetFlatDirection(step.RawExit-step.RawEntry);if(axis.sqrMagnitude<.5f)return false;
            var delta=GetFlatDelta(step.RawEntry,bot.Position);float forward=Vector3.Dot(delta,axis);float lateral=(delta-axis*forward).magnitude;
            if(forward < -RouteMissionExecutionCommitDistance||forward>InjectedNativeVaultEntryMaxForwardOvershoot||lateral>1.5f)return false;
            var edge=LearnedTraversalEdges.FirstOrDefault(x=>x!=null&&x.Id==step.EdgeId);
            float maximum=GetLearningV357ActuatorEntryLateralTolerance(edge,axis,InjectedNativeVaultRollingMaxLateralOffset);
            centering=FollowerCapabilityApproachPolicy.VaultPortalExcess(lateral,maximum,InjectedNativeVaultEntryTriggerDistance)>.001f;
            if(ShouldEmitDiagnostic("rc117VaultFacing:"+step.EpisodeId,bot,.25f))
                WriteEvent("capabilityVaultIngressAxisHeld",bot,new{missionId=mission.MissionId,edgeId=step.EdgeId,forward,lateral,centering,
                    axis=Vec(axis),target=Vec(target.Value),nativeAdmissionChanged=false});
            return true;
        }
        private static bool TryRecoverCapabilityStalledJumpPreparation(BotOwner bot,FollowerRouteMission mission,FollowerRouteMissionStep step)
        {
            if(!TryCaptureCapabilityJumpPreparation(bot,mission,step,out var axis,out float forward,out float speed))return false;
            if(HasCapabilityMeasuredMissedJumpWindow(bot,mission,step,forward))
                return TryRequestCapabilityFullRouteRetry(bot,mission,"jumpPreparationMeasuredMissedWindow");
            var player=bot.GetPlayer;var delta=GetFlatDelta(GetLearningV442JumpAdmissionEntry(step),player.Position);
            var measured=GetFlatDelta(mission.LastProgressPosition,player.Position);
            if(FollowerCapabilityApproachPolicy.HorizontalProgress(measured.magnitude,Vector3.Dot(measured,axis),.05f,.04f))
            {mission.LastProgressAt=Time.time;mission.LastProgressPosition=player.Position;return false;}
            bool attempted=player.InputDirection.y>.1f||player.MovementContext.MovementDirection.y>.1f||
                Vector3.Dot(GetFlatDirection(player.MovementContext.PlayerRealForward),axis)<LearningV352JumpTakeoffCorridorMinFacingDot;
            if(!FollowerCapabilityApproachPolicy.StalledJump(attempted,Time.time-mission.LastProgressAt,forward,
                (delta-axis*forward).magnitude,speed))return false;
            WriteEvent("capabilityJumpPreparationStalled",bot,new{missionId=mission.MissionId,edgeId=step.EdgeId,forward,speed,
                noProgressAge=Time.time-mission.LastProgressAt,state=player.CurrentStateName.ToString(),executionFailed=true,nativeSuccessRecorded=false});
            return TryRequestCapabilityFullRouteRetry(bot,mission,"jumpPreparationMeasuredStall");
        }
        private static Vector3 GetFlatDelta(Vector3 from,Vector3 to){var delta=to-from;delta.y=0f;return delta;}
        private static bool IsCapabilityJumpTriggerReady(ProbeRuntime probe,out string reason,bool atomicAdmission=false)
        {
            reason="nativeJumpOriginUnavailable";
            if(probe?.LifecycleMission==null||!string.Equals(probe.RequestedTraversalKind,"Jump",StringComparison.OrdinalIgnoreCase))return true;
            var player=probe.Player;var context=player?.MovementContext;
            bool current=IsProbeLifecycleCurrent(probe);
            if(context==null||!current||!TryGetLearnedCommitmentForProbe(probe,out var commitment)||commitment==null)return false;
            float speed=GetLearningV329PlanarSpeed(player);
            int template=commitment.SkillTransferTransient?commitment.SkillTemplateEdgeId:commitment.EdgeId;
            var edge=LearnedTraversalEdges.FirstOrDefault(x=>x!=null&&x.Id==template);
            if(FollowerCapabilityRouteRetryPolicy.UnsafeJumpSpeed(speed,edge?.ApproachSpeed??0f))
            {reason=TryObserveCapabilityJumpBodyVelocity(player,out _,out _,out _)?"unsafeSpeed":"velocityObservationPending";return false;}
            var identity=IsFinite(commitment.RawEntry)?commitment.RawEntry:commitment.Entry;
            var entry=GetLearningV442JumpAdmissionEntry(commitment);
            var axis=GetFlatDirection(commitment.RawExit-identity);
            var offset=GetFlatDelta(entry,player.Position);float forward=Vector3.Dot(offset,axis);
            float side=(offset-axis*forward).magnitude;
            var velocity=GetCapabilityJumpPlanarVelocity(player);var facing=GetFlatDirection(context.PlayerRealForward);
            bool reserved=TryGetLearningV459ReservedJumpExactSuccessorContinuity(probe,forward,out _,out float reservedMaximum);
            // A stack-only V429 call may consume its already validated one-frame forecast.
            // Ordinary trigger callers cannot obtain forecast permission from a reservation alone.
            long generation=FollowerMovementWriterAuthority.PeekExactGeneration(probe.Bot,out var owner);
            bool atomic=FollowerCapabilityActuationPolicy.AtomicJumpReceipt(atomicAdmission,current,
                probe.NativeExecutorConsumed,probe.LearningV354JumpActuatorLeadTriggered,
                probe.LifecycleStep?.LearningV429JumpAdmissionReserved==true,
                generation>0&&generation==probe.MovementWriterGeneration&&owner==FollowerMovementWriterAuthority.WriterDomain.Traversal);
            bool forecast=atomic&&IsCf60LegalOneFrameJumpTriggerForecast(probe.Bot,forward,speed,out _,out _);
            bool window=FollowerCapabilityActuationPolicy.JumpTriggerWindow(
                IsLearningV449JumpInsidePhysicalTriggerHorizon(forward,speed,probe.Bot),reserved,atomic,forecast);
            bool ready=axis.sqrMagnitude>.5f&&IsLearningV450LiveEftJumpAttemptLegal(window,probe.LearningV32NativeEntryAcquired,
                probe.JumpEntryCaptured,context.IsGrounded,context.CanJump,player.CurrentStateName,player.InputDirection,context.MovementDirection,
                forward,side,Vector3.Dot(facing,axis),Vector3.Dot(GetFlatDirection(velocity),axis),speed,
                reserved?reservedMaximum:LearningV352JumpTakeoffCorridorMaxForwardOvershoot);
            if(!ready)WriteEvent("capabilityJumpFinalLiveEvidence",probe.Bot,new{window,reserved,atomic,forecast,forward,side,speed,
                entryAcquired=probe.LearningV32NativeEntryAcquired,captured=probe.JumpEntryCaptured,grounded=context.IsGrounded,
                canJump=context.CanJump,state=player.CurrentStateName.ToString(),nativeJumpTriggered=false});
            reason=ready?string.Empty:"nativeLiveWindowOrGeometry";
            return ready; // The same live EFT contract as admission. Historical gait/success floors rank preparation, never veto this consumer.
        }
        private static bool TryPrepareCapabilityAdjacentJumpRunup(BotOwner bot,Player player,FollowerRouteMission mission,FollowerRouteMissionStep current,Vector3 direction)
        {
            bool activeWT=TryGetActiveProbe(bot,out var wt)&&IsProbeLifecycleCurrent(wt)&&
                ReferenceEquals(wt.LifecycleMission,mission)&&ReferenceEquals(wt.LifecycleStep,current)&&
                string.Equals(wt.RequestedTraversalKind,"WalkThrough",StringComparison.OrdinalIgnoreCase)&&
                !wt.WalkThroughSharpTurnGateActive&&!wt.WalkThroughStepAssistActive;
            if(mission==null||mission.Lifecycle.Retired||Time.time>mission.ExpiresAt||
                !TryValidateV2CurrentTypedStep(bot,mission,current,!activeWT,out _)||
                player?.MovementContext==null||!player.MovementContext.IsGrounded||!player.MovementContext.CanSprint||
                player.MovementContext.OverridenState!=null||player.MovementContext.OverridenControlsState!=null||
                (HasActiveTraversalProbe(bot)&&!activeWT)||!IsCapabilityRouteRetrySafe(bot)||
                !IsLearningV369PhysicalCorridorStep(current)&&!string.Equals(current?.Kind,"WalkThrough",StringComparison.OrdinalIgnoreCase)||
                mission.CurrentStepIndex+1>=mission.Steps.Count)return false;
            var jump=mission.Steps[mission.CurrentStepIndex+1];
            if(jump==null||!string.Equals(jump.Kind,"Jump",StringComparison.OrdinalIgnoreCase)||
                !TryValidateV2AdjacentPhysicalContinuationBoundary(bot,mission,current,jump,true,out _)||
                !TryGetLearningV460JumpAdmissionGeometry(bot,jump,out var entry,out var axis,out float forward,out float speed))return false;
            var edge=LearnedTraversalEdges.FirstOrDefault(x=>x!=null&&x.Id==jump.EdgeId);
            var offset=GetFlatDelta(entry,player.Position);float lateral=(offset-axis*forward).magnitude;
            // Prepare while the predecessor still owns the body, never trigger/advance its cursor.
            if(!TryGetCapabilityRouteSprint(bot,direction,out bool routeSprint)||!routeSprint||
                forward>-.15f||forward < -RouteMissionTraversalAdjacentBridgeMaxDistance||
                lateral>LearningV352JumpTakeoffCorridorMaxLateral||Vector3.Dot(GetFlatDirection(direction),axis)<.98f||
                Vector3.Dot(GetFlatDirection(player.MovementContext.PlayerRealForward),axis)<LearningV352JumpTakeoffCorridorMinFacingDot||
                player.MovementContext.PoseLevel<.9f)return false;
            long generation=FollowerMovementWriterAuthority.PeekExactGeneration(bot,out var owner);
            if(generation<=0||(owner!=FollowerMovementWriterAuthority.WriterDomain.Traversal&&owner!=FollowerMovementWriterAuthority.WriterDomain.Follow)||
                (activeWT&&(owner!=FollowerMovementWriterAuthority.WriterDomain.Traversal||wt.MovementWriterGeneration!=generation))||
                !FollowerMovementWriterAuthority.TryEnterBoundPhysicalMotion(bot,owner,generation,out var scope,out _))return false;
            using(scope)
            {
            if(!TryValidateV2CurrentTypedStep(bot,mission,current,!activeWT,out _)||!IsCapabilityRouteRetrySafe(bot)||
                (activeWT&&!IsProbeLifecycleCurrent(wt)))return false;
            bot.Mover.BotMoverSprint("R116-116A00000004",bot,true,false);
            if(player.CurrentStateName==EPlayerState.Run&&!player.MovementContext.IsSprintEnabled&&player.MovementContext.MovementDirection.y>.1f)
                player.PlayerEnableSprint("R116-116A00000005",bot,true);
            if(ShouldEmitDiagnostic("rc116AdjacentJumpRunup:"+jump.EpisodeId,bot,.25f))
                WriteEvent("capabilityAdjacentJumpRunupPrepared",bot,new{missionId=mission.MissionId,edgeId=jump.EdgeId,
                    predecessor=current.EdgeId,forward,lateral,speed,nativeJumpTriggered=false});
            return true;
            }
        }
        private static bool TryDriveCapabilityWalkThroughSeam(ProbeRuntime probe)
        {
            if(probe?.Player?.MovementContext==null||!IsProbeLifecycleCurrent(probe))return false;
            var player=probe.Player;var context=player.MovementContext;
            var velocity=player.Velocity;velocity.y=0;
            if(!FollowerCapabilityPassagePolicy.WalkSeam(true,IsCapabilityRouteRetrySafe(probe.Bot),context.IsGrounded,
                probe.WalkThroughStepAssistActive,IsProbeTraversalState(player.CurrentStateName)||
                    context.OverridenState!=null||context.OverridenControlsState!=null,
                Time.time-probe.WalkThroughLastProgressAt,velocity.magnitude))return false;
            if(!probe.WalkThroughStepAssistActive)
            {
                var target=SampleWalkThroughCorridorAtDistance(probe,Mathf.Min(probe.WalkThroughTotalLength,
                    probe.WalkThroughProgressS+WalkThroughStepAssistForwardProbeDistance));
                var direction=GetFlatDirection(target-player.Position);
                if(direction.sqrMagnitude<.5f||!TryGetFirstNonSelfRaycastHit(player,
                    player.Position+direction*.42f+Vector3.up*.55f,Vector3.down,.75f,out var support)||
                    !FollowerCapabilityPassagePolicy.WalkSeamSupport(support.point.y-player.Position.y))return false;
            }
            // Reuse the bounded, collision-active WT contract. It never triggers Vault/Climb.
            return TryUpdateWalkThroughStepAssist(probe);
        }
        private static bool TryObserveCapabilityLowStep(Player player,Vector3 direction,out RaycastHit bodyHit,out RaycastHit raisedHit,out bool raisedClear)
        {
            bodyHit=default;raisedHit=default;raisedClear=false;
            var bottom=player.Position+Vector3.up*(WalkThroughCapsuleRadius+.02f);var top=player.Position+Vector3.up*WalkThroughCapsuleTop;
            if(!TryGetFirstNonSelfCapsuleCastHit(player,bottom,top,WalkThroughCapsuleRadius,direction,
                WalkThroughStepAssistForwardProbeDistance,LayerMaskClass.HighPolyWithTerrainMask,out bodyHit))return false;
            raisedClear=!TryGetFirstNonSelfCapsuleCastHit(player,bottom+Vector3.up*WalkThroughStepAssistLift,
                top+Vector3.up*WalkThroughStepAssistLift,WalkThroughCapsuleRadius,direction,
                WalkThroughStepAssistForwardProbeDistance,LayerMaskClass.HighPolyWithTerrainMask,out raisedHit);
            return true;
        }
        private static bool IsCapabilityLowStepProducerCurrent(BotOwner bot,LearningV365ContinuousRouteProducerState producer)
        {
            if(producer==null)return false;
            if(producer.Generic)return true;
            if(!TryGetActiveFollowerRouteMission(bot,out var m)||m.Lifecycle.Retired||m.MissionId!=producer.MissionId||
                m.CurrentStepIndex!=producer.StepIndex||m.CurrentStepIndex<0||m.CurrentStepIndex>=m.Steps.Count||
                HasCapabilityFailureStrategy(bot)||IsCapabilityReturnInProgress(bot))return false;
            var step=m.Steps[m.CurrentStepIndex];
            return step!=null&&step.EdgeId==producer.EdgeId&&IsLearningV369PhysicalCorridorStep(step)&&step.Lifecycle.DebtOutstanding;
        }
        private static bool TryDriveCapabilityGenericLowStep(BotOwner bot,Player player,FollowerMovementRecoveryState recovery,
            LearningV365ContinuousRouteProducerState producer,Vector3 direction,long generation,float dt)
        {
            var context=player.MovementContext;
            if(!FollowerCapabilityPassagePolicy.TargetAfterFloor(producer.Generic,producer.TargetSequence,CapabilityGenericResumeFloor(bot))||
                !IsCapabilityLowStepProducerCurrent(bot,producer)||HasActiveTraversalProbe(bot)||!IsCapabilityRouteRetrySafe(bot)||generation<=0||
                !FollowerCapabilityApproachPolicy.StepSupport(context.IsGrounded,recovery.LowStepActive,
                    recovery.LowStepActive?Time.time-recovery.LowStepStartedAt:0f,
                    recovery.LowStepActive?player.Position.y-recovery.LowStepStart.y:0f)||
                context.OverridenState!=null||context.OverridenControlsState!=null||
                (player.CurrentStateName!=EPlayerState.Run&&player.CurrentStateName!=EPlayerState.Idle)||
                context.IsSprintEnabled||player.CurrentStateName==EPlayerState.Sprint||direction.sqrMagnitude<.5f)
            {recovery.LowStepActive=false;return false;}
            long liveGeneration=FollowerMovementWriterAuthority.PeekExactGeneration(bot,out var owner);
            if(liveGeneration!=generation||(owner!=FollowerMovementWriterAuthority.WriterDomain.Follow&&owner!=FollowerMovementWriterAuthority.WriterDomain.Traversal)||
                !FollowerMovementWriterAuthority.TryEnterBoundPhysicalMotion(bot,owner,generation,out var scope,out _))return false;
            using(scope)
            {
            if(!IsCapabilityRouteRetrySafe(bot)||HasActiveTraversalProbe(bot)||
                !LearningV365ContinuousRouteProducerStates.TryGetValue(bot.ProfileId,out var liveProducer)||!ReferenceEquals(liveProducer,producer))return false;
            if(recovery.LowStepGeneration!=generation)
            {recovery.LowStepGeneration=generation;recovery.LowStepActive=false;}
            float progress=recovery.LowStepAttempted?Vector3.Dot(GetFlatDelta(recovery.LowStepStart,player.Position),recovery.LowStepAxis):0f;
            if(recovery.LowStepAttempted&&progress>=WalkThroughStepAssistSuccessDistance)
            {recovery.LowStepAttempted=false;recovery.LowStepActive=false;return false;}
            if(recovery.LowStepAttempted&&!recovery.LowStepActive)return false;
            if(recovery.LowStepActive&&(Time.time-recovery.LowStepStartedAt>=WalkThroughStepAssistMaxSeconds||
                player.Position.y-recovery.LowStepStart.y>=WalkThroughStepAssistLift||
                Vector3.Dot(direction,recovery.LowStepAxis)<.95f))
            {recovery.LowStepActive=false;return false;}
            bool blocked=TryObserveCapabilityLowStep(player,direction,out var bodyHit,out _,out bool raisedClear);
            bool supported=TryGetFirstNonSelfRaycastHit(player,player.Position+direction*.42f+Vector3.up*.55f,
                Vector3.down,.75f,out var support);
            float height=supported?support.point.y-player.Position.y:float.NaN;
            if(!FollowerCapabilityApproachPolicy.LowStep(true,context.IsGrounded||recovery.LowStepActive,!context.IsSprintEnabled,blocked,raisedClear,supported,
                recovery.LowStepActive?.28f:Time.time-Mathf.Max(recovery.LearningV349LastPhysicalProgressAt,recovery.LearningV350GenericPassageArmedAt),
                Vector3.Dot(GetFlatDirection(context.PlayerRealForward),direction),height,progress))
            {
                recovery.LowStepActive=false;
                if(ShouldEmitDiagnostic("rc116GenericLowStepDeferred",bot,.75f))
                    WriteEvent("capabilityGenericLowStepDeferred",bot,new{generation,targetSequence=producer.TargetSequence,
                        blocked,raisedClear,supported,height,progress,grounded=context.IsGrounded,
                        facing=Vector3.Dot(GetFlatDirection(context.PlayerRealForward),direction),
                        stallAge=Time.time-Mathf.Max(recovery.LearningV349LastPhysicalProgressAt,recovery.LearningV350GenericPassageArmedAt),
                        nativeSuccessRecorded=false});
                return false;
            }
            if(!recovery.LowStepActive)
            {
                recovery.LowStepActive=true;recovery.LowStepAttempted=true;recovery.LowStepStartedAt=Time.time;
                recovery.LowStepStart=player.Position;recovery.LowStepAxis=direction;
                WriteEvent("capabilityGenericLowStepEntered",bot,new{generation,targetSequence=producer.TargetSequence,
                    blocker=bodyHit.collider?.name,height,collisionActive=true,nativeSuccessRecorded=false});
            }
            float elapsed=Time.time-recovery.LowStepStartedAt;
            var motion=direction*Mathf.Min(WalkThroughSharpTurnMaxStep,WalkThroughStepAssistForwardSpeed*dt)+
                Vector3.up*(elapsed<WalkThroughStepAssistLiftPhaseSeconds?Mathf.Min(.035f,WalkThroughStepAssistLiftSpeed*dt):0f);
            // Same collision-active native backend as the WT step contract, never transform/teleport.
            return context.TryMovementContextApplyApproachMotion("R116-116A00000006",bot,motion,dt);
            }
        }
        private static void ObserveCapabilityLandingMotionFrontier(BotOwner bot,Player player,MovementContext context,Vector3 motion,bool current)
        {
            if(!TryGetActiveProbe(bot,out var probe)||!IsCapabilityDropDownConsumerPhase(probe)||
                !ShouldEmitDiagnostic("capabilityLandingMotionFrontier:"+probe.TraversalEpisodeId,bot,.15f))return;
            bool captured=TryCaptureCapabilityAdjacentLanding(probe,probe.FrozenTraversalCommitment,player.Position,out _,out var geometry);
            WriteEvent("capabilityLandingMotionFrontierObserved",bot,new{current,geometryCaptured=captured,phase=probe.Stage.ToString(),
                missionId=probe.LifecycleMission?.MissionId,edgeId=probe.FrozenTraversalCommitment?.EdgeId,episodeId=probe.TraversalEpisodeId,
                probeCurrent=IsProbeLifecycleCurrent(probe),nativeActor=ReferenceEquals(probe.Player,player),
                nativeContext=ReferenceEquals(player.MovementContext,context),
                actualGrounded=context.IsGrounded,overridden=context.OverridenState!=null||context.OverridenControlsState!=null,
                probe.NativeDropDownSawAirborne,probe.NativeDropDownSawDescent,probe.NativeDropDownSawFarSide,
                candidateSince=probe.NativeDropDownLandingStableSince,ahead=geometry.Ahead,lateral=geometry.Lateral,
                verticalError=geometry.VerticalError,requested=Vec(motion),body=Vec(player.Position),constrained=false,nativeSuccessRecorded=false});
        }
    }
}
