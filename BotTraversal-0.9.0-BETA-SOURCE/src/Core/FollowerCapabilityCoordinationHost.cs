using System;
using System.Linq;
using EFT;
using UnityEngine;
using UnityEngine.AI;
using S=BotTraversal.Core.FollowerCapabilityFailureSequence.Stage;

namespace BotTraversal.Core
{
    internal static partial class FollowerTraversalTestRuntime
    {
        private static readonly FollowerPortalAdmission CapabilityPortalQueue=new FollowerPortalAdmission();
        private static readonly FollowerCapabilityResolvedDebt CapabilityResolvedDebt=new FollowerCapabilityResolvedDebt();
        private static bool IsCapabilityDebtResolved(BotOwner bot,int episode,int edge)
            => bot!=null&&CapabilityResolvedDebt.Contains(raidGeneration,bot.ProfileId,episode,edge);
        private static void PruneCapabilityResolvedCommitment(BotOwner bot)
        {
            if(bot!=null&&LearnedTraversalCommitments.TryGetValue(bot.ProfileId,out var c)&&c!=null&&IsCapabilityDebtResolved(bot,c.EpisodeId,c.EdgeId))
            {ClearLearnedTraversalCommitment(bot,c.EdgeId,"resolvedEpisodeCannotRepublishEntry");global::BotTraversal.LegacyOriginalPITFollowBridge.CancelDelivery(bot.ProfileId);}
        }
        private static bool IsCapabilityPrioritySafe(BotOwner bot)
        {
            var f=bot==null?null:BossPlayers.GetFollowerByProfileId(bot.ProfileId);
            return IsLiveBot(bot)&&f!=null&&!(f.TryPeekActiveCommand(out var command,out _,out _)&&command!=FollowerCommandType.None)&&
                !global::BotTraversal.LegacyPITCompatibility.IsOriginalManualRegroupInProgress&&
                !FollowerMedical.IsUsingMedical(bot)&&!FollowerMedical.IsHandsInteractionActiveForAuthority(bot.GetPlayer)&&
                !FollowerEnvironment.TryGetImmediateSurvivalThreat(bot,out _)&&!FollowerStateAuthorityCoordinator.HasCombatMovementClaim(bot,out _);
        }
        internal static bool OwnsCapabilityLocalMotion(BotOwner bot)
        {
            if(!IsCapabilityPrioritySafe(bot))return false;
            if(HasCapabilityLivePassageProducer(bot))return true;
            if(!FollowerRouteMissions.TryGetValue(bot.ProfileId,out var m)||m.Lifecycle.Retired||
                m.CurrentStepIndex<0||m.CurrentStepIndex>=m.Steps.Count)return false;
            var step=m.Steps[m.CurrentStepIndex];
            if(step==null||!step.Lifecycle.DebtOutstanding||IsCapabilityDebtResolved(bot,step.EpisodeId,step.EdgeId))return false;
            if(IsCapabilityPortalWaiting(bot)||HasCapabilityFailureStrategy(bot)||IsCapabilityRouteRetryReturning(m))return true;
            if(TryGetActiveProbe(bot,out var probe)&&IsProbeLifecycleCurrent(probe))return true;
            return GetPlanarDistance(bot.Position,step.RawEntry)<=RouteMissionTraversalAdjacentBridgeMaxDistance;
        }
        internal static bool IsCapabilityPortalWaiting(BotOwner bot)
            => BotTraversalPlugin.PassThroughEnabled?.Value==false&&bot!=null&&FollowerRouteMissions.TryGetValue(bot.ProfileId,out var m)&&!m.Lifecycle.Retired&&m.PortalQueueWaiting;
        internal static bool TryGetCapabilityPortalApproachTarget(BotOwner bot,out Vector3 target,out int edge)
        {
            target=default;edge=0;
            if(bot==null||!IsLiveBot(bot)||!FollowerRouteMissions.TryGetValue(bot.ProfileId,out var m)||m.Lifecycle.Retired||
                m.CurrentStepIndex<0||m.CurrentStepIndex>=m.Steps.Count)return false;
            var step=m.Steps[m.CurrentStepIndex];if(step==null||IsCapabilityDebtResolved(bot,step.EpisodeId,step.EdgeId))return false;
            if(IsFollowerRouteMissionStepPhysicallyPassed(bot,step,out _,out _))return false; // Existing proof resolver owns this handoff; never re-approach a passed entry.
            if(IsCapabilityPassageInFlight(bot,m,step))return false;
            var selected=m.FailureSequence?.Selected;
            bool strategy=m.FailureSequence?.Pending==true&&HasCapabilityFailureStrategy(bot)&&selected!=null&&
                selected.RouteMissionId==m.MissionId&&selected.RouteMissionStepIndex==m.CurrentStepIndex;
            var entry=strategy?selected.RawEntry:step.Entry;
            string kind=strategy?selected.Kind:step.Kind;
            bool off=BotTraversalPlugin.PassThroughEnabled?.Value==false;
            double window=strategy?.65:kind=="Jump"?RouteMissionTraversalAdjacentBridgeMaxDistance:RouteMissionExecutionCommitDistance;
            double distance=strategy?Vector3.Distance(bot.Position,entry):GetPlanarDistance(bot.Position,entry);
            if(!FollowerCapabilityPortalPolicy.Approach(FollowerCapabilityPortalPolicy.OwnsApproach(off,CapabilityPortalQueue.Owns(bot.ProfileId,m.MissionId,m.CurrentStepIndex),strategy),
                HasActiveTraversalProbe(bot),m.FailureSequence!=null&&!strategy,IsCapabilityPrioritySafe(bot),distance,window))return false;
            target=entry;edge=step.EdgeId;return IsFinite(target);
        }
        private static bool IsCapabilityPassageInFlight(BotOwner bot,FollowerRouteMission m,FollowerRouteMissionStep step)
        {
            if(step==null||step.Kind!="PhysicalCorridor"||m.FailureSequence!=null||IsCapabilityReturnInProgress(bot)||
                !FollowerTraversalRouteCursors.TryGetValue(bot.ProfileId,out var cursor))return false;
            long generation=FollowerMovementWriterAuthority.PeekExactGeneration(bot,out var owner);
            bool exact=cursor.LearningV375TransactionActive&&cursor.LearningV375RouteMissionId==m.MissionId&&
                cursor.LearningV375MovementGeneration==generation&&owner==FollowerMovementWriterAuthority.WriterDomain.Traversal;
            var axis=GetFlatDirection(step.RawExit-step.RawEntry);float span=GetPlanarDistance(step.RawEntry,step.RawExit);
            var offset=GetFlatDelta(step.RawEntry,bot.Position);float forward=Vector3.Dot(offset,axis);
            float height=bot.Position.y-Mathf.Lerp(step.RawEntry.y,step.RawExit.y,span>.001f?Mathf.Clamp01(forward/span):0);
            return FollowerCapabilityPassagePolicy.PassageInFlight(true,exact,forward,span,(offset-axis*forward).magnitude,height);
        }
        private static bool IsCapabilityRelatedPrefix(TraversalEpisodeMetadata episode,FollowerRouteMissionStep step)
            => FollowerCapabilityPassagePolicy.RelatedPrefix(episode.ChainId==step.ChainId,step.StartBreadcrumbSequence-episode.EndBreadcrumbSequence,
                Vector3.Dot(GetFlatDirection(episode.RawEnd-episode.RawStart),GetFlatDirection(step.RawExit-step.RawEntry)));
        private static bool TryGetCapabilityCompletedPrefixBoundary(BotOwner bot,FollowerRouteMission mission,FollowerRouteMissionStep step,out Vector3 boundary,out Vector3 axis)
        {
            boundary=default;axis=default;
            var previous=mission.Steps.Take(mission.CurrentStepIndex).LastOrDefault(x=>x!=null&&x.Lifecycle.PhysicallyResolved);
            if(previous!=null){boundary=previous.RawExit;axis=GetFlatDirection(previous.RawExit-previous.RawEntry);return axis.sqrMagnitude>.5f;}
            var episode=TraversalEpisodeStream.Where(x=>x!=null&&x.EndBreadcrumbSequence<=step.StartBreadcrumbSequence&&
                IsCapabilityDebtResolved(bot,x.EpisodeId,x.EdgeId)&&GetPlanarDistance(x.RawEnd,step.RawEntry)<=RouteMissionTraversalAdjacentBridgeMaxDistance&&
                IsCapabilityRelatedPrefix(x,step))
                .OrderByDescending(x=>x.EndBreadcrumbSequence).FirstOrDefault();
            if(episode==null)return false;
            boundary=episode.RawEnd;axis=GetFlatDirection(episode.RawEnd-episode.RawStart);return axis.sqrMagnitude>.5f;
        }
        internal static bool TryValidateCapabilityPortalApproachPath(BotOwner bot,Vector3 target)
        {
            if(!TryGetCapabilityPortalApproachTarget(bot,out var current,out _)||GetPlanarDistance(current,target)>.05f)return false;
            var path=CalculateStrictPath(bot.Position,target);
            var mission=FollowerRouteMissions[bot.ProfileId];var step=mission.Steps[mission.CurrentStepIndex];
            bool prefix=TryGetCapabilityCompletedPrefixBoundary(bot,mission,step,out var boundary,out var axis);
            double cornerForward=path?.Corners!=null&&path.Corners.Length>0?path.Corners.Min(point=>Vector3.Dot(GetFlatDelta(boundary,point),axis)):double.NegativeInfinity;
            float bodyForward=Vector3.Dot(GetFlatDelta(boundary,bot.Position),axis),entryForward=Vector3.Dot(GetFlatDelta(boundary,step.RawEntry),axis),targetForward=Vector3.Dot(GetFlatDelta(boundary,target),axis);
            bool valid=FollowerCapabilityPortalPolicy.LocalApproachPath(path?.StrictComplete==true,path?.PathLength??float.PositiveInfinity,Vector3.Distance(bot.Position,target))&&
                FollowerCapabilityIngressMotionPolicy.PrefixPath(prefix,bodyForward,entryForward,targetForward,cornerForward);
            if(!valid&&ShouldEmitDiagnostic("capabilityPortalApproachPathRejected",bot,1f))
                WriteEvent("capabilityPortalApproachPathRejected",bot,new{target=Vec(target),body=Vec(bot.Position),pathLength=path?.PathLength??-1,strictComplete=path?.StrictComplete??false,hasCompletedPrefix=prefix,bodyForward,entryForward,targetForward,cornerForward,reason="currentEntryRequiresLocalPathPreservingCompletedPrefix",nativeSuccessRecorded=false});
            return valid;
        }
        private static bool HasCapabilityUnresolvedRouteWork(BotOwner bot)
            => HasPendingTraversalMission(bot)||(bot!=null&&FollowerRouteMissions.TryGetValue(bot.ProfileId,out var mission)&&!mission.Lifecycle.Retired&&
                mission.CurrentStepIndex>=0&&mission.CurrentStepIndex<mission.Steps.Count&&mission.Steps[mission.CurrentStepIndex]?.Lifecycle.DebtOutstanding==true);
        internal static bool TryReleaseCapabilityResolvedAuthority(BotOwner bot)
        {
            if(bot==null||!IsLiveBot(bot))return false;
            long generation=FollowerMovementWriterAuthority.PeekExactGeneration(bot,out var owner);
            if(owner!=FollowerMovementWriterAuthority.WriterDomain.Traversal&&owner!=FollowerMovementWriterAuthority.WriterDomain.PostTraversal)return false;
            if(!FollowerCapabilityPortalPolicy.MayReleaseResolvedAuthority(CapabilityResolvedDebt.HasWriter(raidGeneration,bot.ProfileId,generation),
                HasActiveTraversalProbe(bot),HasCapabilityUnresolvedRouteWork(bot),HasCurrentCapabilityNativeWay(bot,out _),IsCapabilityRouteRetrySafe(bot)))return false;
            if(!TryRetireNativeMoverPathPreserveLocomotion(bot,"resolvedWriterNoPendingWork",out _)||
                !FollowerMovementWriterAuthority.TryEnterBoundPhysicalMotion(bot,owner,generation,out var scope,out _))return false;
            using(scope)
            {
                ClearCapabilityNativeDestinationData(bot);
                CommitCapabilityRouteRetryZeroInput(bot,bot.GetPlayer);
            }
            EndLearningV375PersistentRouteTransaction(bot,"resolvedWriterNoPendingWork");
            bool released=owner==FollowerMovementWriterAuthority.WriterDomain.Traversal
                ?FollowerMovementWriterAuthority.TransitionTraversalToFollowIfIdleExact(bot,generation,"resolvedWriterNoPendingWork")
                :FollowerMovementWriterAuthority.TransitionPostTraversalToFollowIfCurrent(bot,generation,"resolvedWriterNoPendingWork");
            if(released)WriteEvent("capabilityResolvedAuthorityReleased",bot,new{generation,owner=owner.ToString(),nativeSuccessRecorded=false});
            return released;
        }
        private static void QuiesceCapabilityCompletedFollowInput(BotOwner bot)
        {
            if(bot?.GetPlayer?.MovementContext==null||bot.Mover==null||
                !PostTraversalIntegrityWatches.TryGetValue(bot.ProfileId,out var watch)||watch==null||watch.ExpiresAt<Time.time)return;
            var leader=FollowerEnvironment.LocalPlayer;if(!IsLivePlayer(leader))return;
            long generation=FollowerMovementWriterAuthority.PeekExactGeneration(bot,out var owner);
            if(!FollowerCapabilityIngressMotionPolicy.StopCompletedFollowInput(owner==FollowerMovementWriterAuthority.WriterDomain.Follow&&
                generation>0&&generation==watch.MovementGeneration,IsCapabilityRouteRetrySafe(bot),bot.GetPlayer.MovementContext.IsGrounded,
                HasActiveTraversalProbe(bot),HasCapabilityUnresolvedRouteWork(bot),bot.Mover.HasPathAndNoComplete||HasCurrentCapabilityNativeWay(bot,out _),
                bot.Mover.IsMoving,bot.GetPlayer.InputDirection.sqrMagnitude>.001f,GetPlanarDistance(bot.Position,leader.Position),Time.time-watch.ArmedAt))return;
            if(!FollowerMovementWriterAuthority.TryEnterBoundPhysicalMotion(bot,owner,generation,out var scope,out _))return;
            using(scope)
            {
                CommitCapabilityRouteRetryZeroInput(bot,bot.GetPlayer);
                if(ShouldEmitDiagnostic("capabilityCompletedFollowInputQuiesced",bot,.25f))
                    WriteEvent("capabilityCompletedFollowInputQuiesced",bot,new{watch.EdgeId,watch.EpisodeId,generation,
                        distance=GetPlanarDistance(bot.Position,leader.Position),nativeSuccessRecorded=false,bodyRelocated=false});
            }
        }
        private static bool HasCapabilityNewRecordedWork(BotOwner bot)
        {
            int floor=CapabilityGenericResumeFloor(bot);
            return TraversalEpisodeStream.Any(e=>e!=null&&e.EpisodeId>0&&e.EndBreadcrumbSequence>floor&&
                !IsCapabilityEpisodeRetired(e)&&!IsCapabilityDebtResolved(bot,e.EpisodeId,e.EdgeId));
        }
        private static bool TryGetCapabilityCleanCompletedFollow(BotOwner bot,out PostTraversalIntegrityWatch watch,out Player leader)
        {
            watch=null;leader=FollowerEnvironment.LocalPlayer;
            if(bot?.GetPlayer?.MovementContext==null||!IsLivePlayer(leader)||!IsCapabilityRouteRetrySafe(bot)||
                HasActiveTraversalProbe(bot)||HasCapabilityUnresolvedRouteWork(bot)||HasCapabilityNewRecordedWork(bot)||HasCapabilityLivePassageProducer(bot)||HasCurrentCapabilityNativeWay(bot,out _)||
                !bot.GetPlayer.MovementContext.IsGrounded||!PostTraversalIntegrityWatches.TryGetValue(bot.ProfileId,out watch)||watch==null||
                watch.CompletionMode!=TraversalCompletionMode.Native||!watch.FreshConsumerPathProven||watch.ExpiresAt<Time.time||Time.time-watch.ArmedAt>6)return false;
            long generation=FollowerMovementWriterAuthority.PeekExactGeneration(bot,out var owner);
            return generation>0&&owner==FollowerMovementWriterAuthority.WriterDomain.Follow&&generation==watch.MovementGeneration;
        }
        private static bool RefreshCapabilityCurrentPhysicalCorridor(BotOwner bot,FollowerRouteMission mission,FollowerRouteMissionStep step)
        {
            if(bot==null||mission==null||step==null||mission.Lifecycle.Retired||mission.FailureSequence!=null||
                IsCapabilityFullRouteReplay(mission)||HasActiveTraversalProbe(bot)||!IsLearningV369PhysicalCorridorStep(step)||
                !step.Lifecycle.DebtOutstanding||step.EpisodeId<=0)return false;
            var episode=TraversalEpisodeStream.LastOrDefault(e=>e!=null&&e.EpisodeId==step.EpisodeId&&e.EdgeId==step.EdgeId&&
                e.ChainId==step.ChainId&&e.Kind=="PhysicalCorridor"&&e.StartBreadcrumbSequence==step.StartBreadcrumbSequence);
            if(episode==null||!IsFinite(episode.RawStart)||!IsFinite(episode.RawEnd)||!IsFinite(step.RawEntry)||!IsFinite(step.RawExit))return false;
            var axis=GetFlatDirection(step.RawExit-step.RawEntry);var delta=GetFlatDelta(step.RawEntry,episode.RawEnd);
            float forward=Vector3.Dot(delta,axis);
            if(!FollowerCapabilityPassagePolicy.CorridorExtension(step.EndBreadcrumbSequence,episode.EndBreadcrumbSequence,
                Vector3.Distance(episode.RawStart,step.RawEntry),GetPlanarDistance(step.RawEntry,step.RawExit),forward,
                (delta-axis*forward).magnitude,episode.RawEnd.y-step.RawExit.y))return false;
            int previousEnd=step.EndBreadcrumbSequence;var previousExit=step.RawExit;
            step.RawExit=episode.RawEnd;step.Exit=episode.RawEnd;step.EndBreadcrumbSequence=episode.EndBreadcrumbSequence;
            mission.TargetBreadcrumbSequence=Math.Max(mission.TargetBreadcrumbSequence,step.EndBreadcrumbSequence);
            if(LearnedTraversalCommitments.TryGetValue(bot.ProfileId,out var commitment)&&commitment!=null&&
                commitment.RouteMissionId==mission.MissionId&&commitment.RouteMissionStepIndex==mission.CurrentStepIndex&&commitment.EpisodeId==step.EpisodeId)
            {commitment.RawExit=step.RawExit;commitment.Exit=step.Exit;}
            WriteEvent("capabilityRecordedCorridorExtended",bot,new{missionId=mission.MissionId,step.EpisodeId,step.EdgeId,
                previousEnd,currentEnd=step.EndBreadcrumbSequence,previousExit=Vec(previousExit),currentExit=Vec(step.RawExit),
                progressClockReset=false,completedPrefixChanged=false,nativeSuccessRecorded=false});
            return true;
        }
        private static bool HasCapabilityLivePassageProducer(BotOwner bot)
        {
            if(bot==null||!LearningV365ContinuousRouteProducerStates.TryGetValue(bot.ProfileId,out var p)||p==null)return false;
            bool recovery=FollowerMovementRecoveryStates.TryGetValue(bot.ProfileId,out var state)&&state!=null;
            return FollowerCapabilityPassagePolicy.PassageLease(p.Generic,recovery,state?.LearningV350TerminalRecoveryEligible??false,
                p.TargetSequence,CapabilityGenericResumeFloor(bot),state?.LearningV349GenericPassageLeaseUntil??-1,Time.time);
        }
        private static bool CanConsumeCapabilityCursorFloor(BotOwner bot,FollowerRouteMissionStep step)
        {
            bool movement=step!=null&&(step.Kind=="WalkThrough"||IsLearningV369PhysicalCorridorStep(step)||
                GetLearningV354ActionProfile(step.Kind)?.RequiresActuator==true);
            return FollowerCapabilityPassagePolicy.CursorFloor(true,movement,
                movement&&IsFollowerRouteMissionStepPhysicallyPassed(bot,step,out _,out _));
        }
        internal static long PrepareCapabilityOriginalFollowPath(BotOwner bot,Vector3? target)
        {
            if(!target.HasValue||!IsFinite(target.Value)||
                FollowerMovementWriterAuthority.CanCommitScopedPhysicalMotion(bot,out _)||
                !global::BotTraversal.LegacyPITCompatibility.SupportsOriginalFollowBridge||
                !global::BotTraversal.LegacyOriginalPITFollowBridge.IsOriginalPITFollowCall()||
                !TryGetCapabilityLiveResolvedFollow(bot,out _,out var generation)||
                !PhysicalNavigationEpochHandoffs.TryGetValue(bot.ProfileId,out var handoff)||handoff==null||
                handoff.Generation!=raidGeneration||handoff.NativeBookkeepingSynchronized||handoff.SourceMovementGeneration>generation)return 0;
            if(!TryRetireNativeMoverPathPreserveLocomotion(bot,"originalFollowPendingEpoch",out _)||
                !TryScrubLearningV364NativeMoverEpochAnchor(bot,bot.GetPlayer.Position,"originalFollowPendingEpoch",out _))return 0;
            return generation;
        }
        internal static void CompleteCapabilityOriginalFollowPath(BotOwner bot,Vector3? target,NavMeshPathStatus status,long issued)
        {
            if(issued<=0||!target.HasValue||bot?.Mover==null)return;
            bool clean=TryGetCapabilityLiveResolvedFollow(bot,out _,out var live);
            var endpoint=bot.Mover.ActualPathController?.CurPath?.TargetPoint?.Position;
            if(!FollowerCapabilityPassagePolicy.NativeFollowReceipt(issued,live,clean,status==NavMeshPathStatus.PathComplete,
                bot.Mover.ActualPathController?.HavePath==true,endpoint.HasValue?Vector3.Distance(endpoint.Value,target.Value):float.PositiveInfinity)||
                !FollowerMovementWriterAuthority.TryEnterBoundPhysicalMotion(bot,FollowerMovementWriterAuthority.WriterDomain.Follow,issued,out var scope,out _))return;
            using(scope)RecordFollowPathResult(bot,target.Value,"authority-reacquire",status);
            WriteEvent("capabilityOriginalFollowEpochReceipt",bot,new{generation=issued,target=Vec(target.Value),
                nativePathAccepted=true,bookkeepingSynchronized=PhysicalNavigationEpochHandoffs.TryGetValue(bot.ProfileId,out var h)&&h.NativeBookkeepingSynchronized,
                nativeSuccessRecorded=false,bodyRelocated=false});
        }
        internal static void QuiesceCapabilityOriginalFollowIdle(BotOwner bot)
        {
            if(bot?.GetPlayer?.MovementContext==null||bot.Mover==null)return;
            var player=bot.GetPlayer;var context=player.MovementContext;
            bool clean=TryGetCapabilityLiveResolvedFollow(bot,out _,out var generation);
            bool nativeActor=IsProbeTraversalState(player.CurrentStateName)||context.OverridenState!=null||context.OverridenControlsState!=null;
            if(!FollowerCapabilityPassagePolicy.NativeFollowIdle(clean,context.IsGrounded,bot.Mover.HasPathAndNoComplete,
                bot.Mover.IsMoving,nativeActor,player.InputDirection.sqrMagnitude>.001f)||
                !FollowerMovementWriterAuthority.TryEnterBoundPhysicalMotion(bot,FollowerMovementWriterAuthority.WriterDomain.Follow,generation,out var scope,out _))return;
            using(scope)CommitCapabilityRouteRetryZeroInput(bot,player);
            if(ShouldEmitDiagnostic("capabilityOriginalFollowIdleInputClosed",bot,.5f))
                WriteEvent("capabilityOriginalFollowIdleInputClosed",bot,new{generation,nativeFollowRequestedStop=true,
                    nativeSuccessRecorded=false,bodyRelocated=false});
        }
        internal static bool TryYieldCapabilityCompletedFollow(BotOwner bot)
        {
            bool recent=TryGetCapabilityCleanCompletedFollow(bot,out _,out _);
            if(!recent&&!TryGetCapabilityLiveResolvedFollow(bot,out _,out _))return false;
            // Ordinary PIT Follow owns formation distance, stopping and stationary-player
            // separation. Completion is not another route to the leader's exact position.
            global::BotTraversal.LegacyOriginalPITFollowBridge.CancelDelivery(bot.ProfileId,preserveCadence:true);
            if(ShouldEmitDiagnostic("capabilityCompletedFollowYielded",bot,2f))
                WriteEvent("capabilityCompletedFollowYielded",bot,new{nativeFormationOwnsTarget=true,
                    newPathIssued=false,nativeSuccessRecorded=false});
            return true;
        }
        internal static bool TryPrepareCapabilityPortalApproachDelivery(BotOwner bot)
        {
            if(!TryGetCapabilityPortalApproachTarget(bot,out var target,out _)||
                !FollowerCapabilityPortalPolicy.MayYieldApproach(true,HasActiveTraversalProbe(bot),HasCurrentCapabilityNativeWay(bot,out _),IsCapabilityRouteRetrySafe(bot))||
                !TryValidateCapabilityPortalApproachPath(bot,target))return false;
            long generation=FollowerMovementWriterAuthority.PeekExactGeneration(bot,out var owner);
            if(owner==FollowerMovementWriterAuthority.WriterDomain.Follow)return true;
            if(owner!=FollowerMovementWriterAuthority.WriterDomain.Traversal&&owner!=FollowerMovementWriterAuthority.WriterDomain.PostTraversal)return false;
            if(!TryRetireNativeMoverPathPreserveLocomotion(bot,"portalApproachOwnerHandoff",out _)||
                !FollowerMovementWriterAuthority.TryEnterBoundPhysicalMotion(bot,owner,generation,out var scope,out _))return false;
            using(scope)
            {
                ClearCapabilityNativeDestinationData(bot);
                if(!TryScrubLearningV364NativeMoverEpochAnchor(bot,bot.GetPlayer.Position,"portalApproachOwnerHandoff",out _))return false;
            }
            EndLearningV375PersistentRouteTransaction(bot,"portalApproachOwnerHandoff");
            bool yielded=owner==FollowerMovementWriterAuthority.WriterDomain.Traversal
                ?FollowerMovementWriterAuthority.TransitionTraversalToFollowIfIdleExact(bot,generation,"portalApproachOwnerHandoff")
                :FollowerMovementWriterAuthority.TransitionPostTraversalToFollowIfCurrent(bot,generation,"portalApproachOwnerHandoff");
            if(yielded)WriteEvent("capabilityPortalApproachOwnerYielded",bot,new{generation,owner=owner.ToString(),target=Vec(target),nativeSuccessRecorded=false});
            return yielded;
        }
        private static void ReconcileCapabilityRetiredProbeConsumer(ProbeRuntime probe)
        {
            var bot=probe?.Bot;var step=probe?.LifecycleStep;
            if(bot?.Mover==null||step==null||!IsCapabilityPrioritySafe(bot))return;
            var goal=bot.Mover.RealDestPoint;var axis=GetFlatDirection(step.RawExit-step.RawEntry);
            bool behind=IsFinite(goal)&&axis.sqrMagnitude>.5f&&GetPlanarDistance(goal,bot.Position)>.5f&&
                Vector3.Dot(GetFlatDelta(step.RawExit,goal),axis)<-.25f;
            bool terminal=probe.LifecycleMission!=null&&probe.LifecycleMission.CurrentStepIndex>=probe.LifecycleMission.Steps.Count&&
                bot.Mover.HasPathAndNoComplete==false&&IsCapabilityRouteRetrySafe(bot)&&bot.GetPlayer.MovementContext.IsGrounded;
            if(!FollowerCapabilityPortalPolicy.MayRetireConsumer(IsCapabilityDebtResolved(bot,step.EpisodeId,step.EdgeId),
                HasActiveTraversalProbe(bot),HasCurrentCapabilityNativeWay(bot,out _),behind||terminal))return;
            long generation=FollowerMovementWriterAuthority.PeekExactGeneration(bot,out var owner);
            if(generation!=probe.MovementWriterGeneration||
                (owner!=FollowerMovementWriterAuthority.WriterDomain.Traversal&&owner!=FollowerMovementWriterAuthority.WriterDomain.PostTraversal))return;
            if(!TryRetireNativeMoverPathPreserveLocomotion(bot,"completedProbeBehindGoalRetired",out _))return;
            if(FollowerMovementWriterAuthority.TryEnterBoundPhysicalMotion(bot,owner,generation,out var scope,out _))
                using(scope)
                {
                    ClearCapabilityNativeDestinationData(bot);
                    if(terminal)CommitCapabilityRouteRetryZeroInput(bot,bot.GetPlayer);
                }
            global::BotTraversal.LegacyOriginalPITFollowBridge.CancelDelivery(bot.ProfileId);
            WriteEvent("capabilityCompletedConsumerRetired",bot,new{step.EpisodeId,step.EdgeId,oldGoal=Vec(goal),body=Vec(bot.Position),generation,nativeSuccessRecorded=false});
        }
        private static Vector3 CapabilitySelfRescueFrontier(BotOwner bot,CapabilityFailureTransaction state)
        {
            var body=bot.GetPlayer.Position;var entry=state.Original.RawEntry;
            return FollowerCapabilityPassagePolicy.RescueAtBody(IsCapabilityRouteRetrySafe(bot),bot.GetPlayer.MovementContext.IsGrounded,IsFinite(body))?body:entry;
        }
        private static int CapabilityGenericResumeFloor(BotOwner bot)
            => Math.Max(GetFollowerRouteResumeFloor(bot.ProfileId),GetFollowerRouteExecutionFloor(bot.ProfileId));
        private static void CommitCapabilityResolvedNavigation(BotOwner bot,FollowerRouteMission m,FollowerRouteMissionStep step)
        {
            var next=m.CurrentStepIndex<m.Steps.Count?m.Steps[m.CurrentStepIndex]:null;
            int floor=FollowerCapabilityPortalPolicy.ResolvedNavigationFloor(GetFollowerRouteResumeFloor(bot.ProfileId),step.EndBreadcrumbSequence,next?.StartBreadcrumbSequence??-1);
            FollowerRouteResumeFloors[bot.ProfileId]=floor;
            m.ResumeFloorBreadcrumbSequence=Math.Max(m.ResumeFloorBreadcrumbSequence,floor);
            m.ApproachBreadcrumbSequence=Math.Max(m.ApproachBreadcrumbSequence,floor);
            m.PhysicalApproachBreadcrumbSequence=Math.Max(m.PhysicalApproachBreadcrumbSequence,floor);
            if(FollowerMovementRecoveryStates.TryGetValue(bot.ProfileId,out var genericRecovery))
            {
                genericRecovery.LearningV349GenericPassageCursorSequence=Math.Max(genericRecovery.LearningV349GenericPassageCursorSequence,floor);
                genericRecovery.LowStepActive=false;genericRecovery.LowStepAttempted=false;
            }
            if(LearningV365ContinuousRouteProducerStates.TryGetValue(bot.ProfileId,out var genericProducer)&&
                !FollowerCapabilityPassagePolicy.TargetAfterFloor(genericProducer.Generic,genericProducer.TargetSequence,floor))
                ClearLearningV365ContinuousPhysicalRoute(bot,"completedRouteFloorRetiresGenericTarget");
            if(BreadcrumbFollowCommitments.TryGetValue(bot.ProfileId,out var breadcrumb)&&breadcrumb.TargetSequence<=floor)BreadcrumbFollowCommitments.Remove(bot.ProfileId);
            global::BotTraversal.LegacyOriginalPITFollowBridge.CancelDelivery(bot.ProfileId);
            WriteEvent("capabilityResolvedNavigationCommitted",bot,new{missionId=m.MissionId,step.EpisodeId,step.EdgeId,floor,nextUnresolvedEpisode=next?.EpisodeId??0,nativeSuccessRecorded=false});
        }
        private static bool TryRetireCapabilityCoveredDropOverlap(BotOwner bot,FollowerRouteMission mission)
        {
            if(bot?.GetPlayer?.MovementContext==null||mission==null||mission.Lifecycle.Retired||mission.FailureSequence!=null||
                IsCapabilityReturnInProgress(bot)||!IsCapabilityRouteRetrySafe(bot)||mission.CurrentStepIndex<=0||mission.CurrentStepIndex>=mission.Steps.Count)return false;
            var step=mission.Steps[mission.CurrentStepIndex];var previous=mission.Steps[mission.CurrentStepIndex-1];
            if(step?.Kind=="DropDown")return TryRetireCapabilityCoveredLowerDrop(bot,mission,step);
            if(step==null||previous==null||step.Kind!="WalkThrough"||previous.Kind!="DropDown"||!step.Lifecycle.DebtOutstanding||
                !previous.Lifecycle.PhysicallyResolved||!RecentTraversalCompletions.TryGetValue(MakeRecentTraversalCompletionKey(bot.ProfileId,previous.EdgeId),out var proof)||
                proof==null||Time.time>proof.ExpiresAt)return false;
            if(TryGetActiveProbe(bot,out var actor)&&(actor.Lifecycle?.CompletionPublished!=true||!ReferenceEquals(actor.LifecycleStep,previous)))return false;
            var axis=GetFlatDirection(previous.RawExit-previous.RawEntry);var wtAxis=GetFlatDirection(step.RawExit-step.RawEntry);
            if(axis.sqrMagnitude<.5f||wtAxis.sqrMagnitude<.5f||!IsFinite(proof.Rc58ObservedNativeStart)||!IsFinite(proof.Rc58ObservedNativeEnd))return false;
            var witness=GetFlatDelta(step.RawExit,proof.Rc58ObservedNativeEnd);float forward=Vector3.Dot(witness,wtAxis);
            bool nativeExact=proof.Rc58VerifiedNativePhysical&&proof.Rc58EpisodeId==previous.EpisodeId&&proof.Rc58ChainId==previous.ChainId&&
                IsLifecycleCompletionForStep(bot,proof,previous);
            bool overlap=step.StartBreadcrumbSequence<=previous.EndBreadcrumbSequence&&step.EndBreadcrumbSequence>=previous.StartBreadcrumbSequence&&
                step.EndBreadcrumbSequence<=previous.EndBreadcrumbSequence+4;
            if(!FollowerCapabilityPassagePolicy.CoveredDropOverlap(nativeExact,overlap,bot.GetPlayer.MovementContext.IsGrounded,
                Mathf.Max(Vector3.Distance(step.RawEntry,previous.RawEntry),Vector3.Distance(step.RawEntry,proof.Rc58ObservedNativeStart)),
                Vector3.Dot(axis,wtAxis),Vector3.Dot(GetFlatDelta(previous.RawExit,step.RawExit),axis),step.RawExit.y-previous.RawExit.y,
                forward,(witness-wtAxis*forward).magnitude,Mathf.Min(step.RawExit.y-proof.Rc58ObservedNativeEnd.y,step.RawExit.y-bot.Position.y),Time.time-proof.CompletedAt))return false;
            var observation=CaptureRouteDebtObservation(bot,mission,step,false);
            if(!TryAdvanceRouteMission(bot,mission,mission.CurrentStepIndex+1,RouteDebtEvidence.PhysicalPass,"nativeDropCoveredRecordingOverlap",observation))return false;
            WriteEvent("capabilityNativeDropOverlapRetired",bot,new{missionId=mission.MissionId,dropEpisode=previous.EpisodeId,dropEdge=previous.EdgeId,
                coveredEpisode=step.EpisodeId,coveredEdge=step.EdgeId,witness=Vec(proof.Rc58ObservedNativeEnd),body=Vec(bot.Position),
                newWalkThroughSuccessRecorded=false,persistentLearningChanged=false,bodyRelocated=false});
            return true;
        }
        private static bool TryRetireCapabilityCoveredLowerDrop(BotOwner bot,FollowerRouteMission mission,FollowerRouteMissionStep step)
        {
            int previousIndex=mission.Steps.FindLastIndex(mission.CurrentStepIndex-1,s=>s!=null&&s.Kind=="DropDown"&&s.Lifecycle.PhysicallyResolved);
            if(previousIndex<0||!step.Lifecycle.DebtOutstanding)return false;
            var previous=mission.Steps[previousIndex];
            if(!RecentTraversalCompletions.TryGetValue(MakeRecentTraversalCompletionKey(bot.ProfileId,previous.EdgeId),out var proof)||proof==null||Time.time>proof.ExpiresAt)return false;
            if(TryGetActiveProbe(bot,out var actor)&&(actor.Lifecycle?.CompletionPublished!=true||!ReferenceEquals(actor.LifecycleStep,previous)))return false;
            bool between=mission.Steps.Skip(previousIndex+1).Take(mission.CurrentStepIndex-previousIndex-1).All(s=>s!=null&&s.Lifecycle.PhysicallyResolved);
            bool exact=proof.Rc58VerifiedNativePhysical&&proof.Rc58EpisodeId==previous.EpisodeId&&proof.Rc58ChainId==previous.ChainId&&IsLifecycleCompletionForStep(bot,proof,previous)&&
                IsFinite(proof.Rc58ObservedNativeStart)&&IsFinite(proof.Rc58ObservedNativeEnd);
            var axis=GetFlatDirection(step.RawExit-step.RawEntry);var previousAxis=GetFlatDirection(previous.RawExit-previous.RawEntry);
            var end=GetFlatDelta(step.RawExit,proof.Rc58ObservedNativeEnd);float forward=Vector3.Dot(end,axis);
            if(!FollowerCapabilityPassagePolicy.CoveredLowerDrop(exact,between,bot.GetPlayer.MovementContext.IsGrounded,previous.ChainId==step.ChainId,
                step.StartBreadcrumbSequence-previous.EndBreadcrumbSequence,step.EndBreadcrumbSequence-previous.EndBreadcrumbSequence,Vector3.Dot(axis,previousAxis),
                proof.Rc58ObservedNativeStart.y-step.RawEntry.y,proof.Rc58ObservedNativeEnd.y-step.RawExit.y,forward,(end-axis*forward).magnitude,
                Vector3.Distance(bot.Position,proof.Rc58ObservedNativeEnd),Time.time-proof.CompletedAt))return false;
            var observation=CaptureRouteDebtObservation(bot,mission,step,false);
            if(!TryAdvanceRouteMission(bot,mission,mission.CurrentStepIndex+1,RouteDebtEvidence.PhysicalPass,"nativeDropAlreadyCoversLowerSuccessor",observation))return false;
            WriteEvent("capabilityNativeDropSuccessorCovered",bot,new{missionId=mission.MissionId,previous.EpisodeId,previous.EdgeId,
                coveredEpisode=step.EpisodeId,coveredEdge=step.EdgeId,witness=Vec(proof.Rc58ObservedNativeEnd),body=Vec(bot.Position),
                newDropDownSuccessRecorded=false,persistentLearningChanged=false,bodyRelocated=false});return true;
        }
        private static bool RejectCapabilityForeignCommitment(BotOwner bot,LearnedTraversalCommitment c)
        {
            if(bot==null||c==null||HasActiveTraversalProbe(bot)||!FollowerRouteMissions.TryGetValue(bot.ProfileId,out var m)||
                m.Lifecycle.Retired||m.CurrentStepIndex<0||m.CurrentStepIndex>=m.Steps.Count)return false;
            var step=m.Steps[m.CurrentStepIndex];if(step==null)return false;
            bool exact=OwnsCapabilityUnfinishedCommitment(bot,m,c);
            // An ordinary candidate may not yet carry its mission link; reconcile only
            // the same recorded episode/edge through the existing binder, never another debt.
            if(!exact&&c.EpisodeId==step.EpisodeId&&c.EdgeId==step.EdgeId&&!HasCapabilityFailureStrategy(bot))
            {
                TryPrepareCapabilityAutomaticProbeMission(bot,"currentCommitmentReconcile",c.EdgeId,out _);
                exact=OwnsCapabilityUnfinishedCommitment(bot,m,c);
            }
            if(!FollowerCapabilityPassagePolicy.ForeignCommitment(true,step.Lifecycle.DebtOutstanding&&!IsCapabilityDebtResolved(bot,step.EpisodeId,step.EdgeId),
                HasCapabilityFailureStrategy(bot),exact))return false;
            ClearLearnedTraversalCommitment(bot,c.EdgeId,"currentTypedDebtOutranksForeignCommitment");
            global::BotTraversal.LegacyOriginalPITFollowBridge.CancelDelivery(bot.ProfileId);
            if(ShouldEmitDiagnostic("capabilityForeignCommitmentRejected",bot,.5f))WriteEvent("capabilityForeignCommitmentRejected",bot,new{
                missionId=m.MissionId,stepIndex=m.CurrentStepIndex,step.EdgeId,step.EpisodeId,rejectedEdge=c.EdgeId,rejectedEpisode=c.EpisodeId,
                rejectedMission=c.RouteMissionId,routeFloorAdvanced=false,nativeSuccessRecorded=false});
            return true;
        }
        private static bool HandleCapabilityRecordedTeleportFailure(BotOwner bot,LearnedTraversalCommitment c,string source)
        {
            if(bot==null||c==null)return false;
            bool recorded=FollowerCapabilityPassagePolicy.RecordedFallback(c.EpisodeId);
            if(!FollowerCapabilityPassagePolicy.RecordedRecoveryScope(recorded,HasCapabilityUnresolvedRouteWork(bot)))return false;
            string reason="currentTypedDebtRetainsRecovery";
            bool prepared=recorded&&TryPrepareCapabilityAutomaticProbeMission(bot,"legacyMissedTraversalFailure",c.EdgeId,out reason);
            bool advanced=false;
            if(FollowerRouteMissions.TryGetValue(bot.ProfileId,out var m)&&OwnsCapabilityUnfinishedCommitment(bot,m,c))
                advanced=TryRequestCapabilityFullRouteRetry(bot,m,"recordedEntryFailure:"+source);
            else RejectCapabilityForeignCommitment(bot,c);
            if(!advanced)
            {
                ClearLearnedTraversalCommitment(bot,c.EdgeId,"recordedTeleportCannotBypassCurrentDebt");
                global::BotTraversal.LegacyOriginalPITFollowBridge.CancelDelivery(bot.ProfileId);
            }
            WriteEvent("capabilityRecordedTeleportRedirected",bot,new{edgeId=c.EdgeId,episodeId=c.EpisodeId,prepared,reason,advanced,
                directTeleportAttempted=false,nativeSuccessRecorded=false});
            return true; // Handled/suppressed, never a successful teleport receipt.
        }
        private static bool TryCloseCapabilityClimbAtSuccessor(ProbeRuntime probe,LearnedTraversalCommitment c)
        {
            if(probe?.Bot==null||c?.Kind!="Climb"||!IsProbeLifecycleCurrent(probe)||!IsCapabilityRouteRetrySafe(probe.Bot)||
                (!TryGetLearningV375FutureSameChainSuccessor(probe,out var mission,out var next)&&
                 !TryGetLearningV341ImmediateSemanticSuccessor(probe,c,out next)))return false;
            if(mission==null)mission=probe.LifecycleMission;
            if(!FollowerCapabilityPassagePolicy.ClimbSuccessor(true,probe.NativeExecutorConsumed,probe.NativeStateObserved,
                probe.LearningV349PhysicalPassageCrossed,IsEffectivelySupportedForProbe(probe.Bot,probe.Player),next.Kind=="DropDown",
                Vector3.Distance(probe.Player.Position,c.RawExit),Vector3.Distance(probe.Player.Position,next.RawEntry)))return false;
            bool closed=TryFinalizeTraversalChainIntermediate(probe,"currentNativeClimbUpperLandingDropSuccessor");
            WriteEvent("capabilityClimbUpperSuccessorClosure",probe.Bot,new{missionId=mission.MissionId,edgeId=c.EdgeId,nextEdge=next.EdgeId,closed,
                existingChainValidatorUsed=true,nativeSuccessRecordedByThisAdapter=false,bodyRelocated=false});
            return closed;
        }
        private static bool OwnsCapabilityUnfinishedCommitment(BotOwner bot,FollowerRouteMission mission,LearnedTraversalCommitment c)
        {
            if(bot==null||mission==null||c==null||mission.Lifecycle.Retired||mission.MissionId!=c.RouteMissionId||
                mission.CurrentStepIndex!=c.RouteMissionStepIndex||mission.CurrentStepIndex<0||mission.CurrentStepIndex>=mission.Steps.Count)return false;
            var step=mission.Steps[mission.CurrentStepIndex];
            return step!=null&&step.EpisodeId==c.EpisodeId&&IsLearningV376RouteStepEquivalentToEdgeId(step,c.EdgeId)&&
                step.Lifecycle.DebtOutstanding&&!IsCapabilityDebtResolved(bot,step.EpisodeId,step.EdgeId);
        }
        private static bool IsCapabilityEpisodeRetired(TraversalEpisodeMetadata episode)
            => episode!=null&&FollowerCapabilityPassagePolicy.RetiredEpisode(episode.Kind=="WalkThrough",
                episode.LearningV386ExecutionDebtRetiredByOrdinaryNavMesh,episode.LearningV386ExecutionDebtConsumedByPhysicalPassage);
        private static bool RegisterCapabilityLocalMission(BotOwner bot,FollowerRouteMissionStep step,string source)
        {
            if(bot==null||step==null||step.EpisodeId<=0||!IsFinite(step.RawEntry)||!IsFinite(step.RawExit)||
                FollowerRouteMissions.ContainsKey(bot.ProfileId)||IsCapabilityDebtResolved(bot,step.EpisodeId,step.EdgeId)||
                IsCapabilityEpisodeRetired(TraversalEpisodeStream.FirstOrDefault(e=>e!=null&&e.EpisodeId==step.EpisodeId&&e.EdgeId==step.EdgeId))||
                GetFollowerRouteResumeFloor(bot.ProfileId)>=step.EndBreadcrumbSequence)return false;
            var m=new FollowerRouteMission {MissionId=++followerRouteMissionSequence,LifecycleBotId=bot.ProfileId,LifecycleRaid=raidGeneration,
                Source=source,CreatedAt=Time.time,ExpiresAt=Time.time+RouteMissionLifetimeSeconds,LastProgressAt=Time.time,
                LastProgressPosition=bot.Position,StartBreadcrumbSequence=step.StartBreadcrumbSequence,
                TargetBreadcrumbSequence=Math.Max(breadcrumbSequence,step.EndBreadcrumbSequence),LastPlayerEpisodeId=step.EpisodeId,
                ResumeFloorBreadcrumbSequence=GetFollowerRouteResumeFloor(bot.ProfileId),
                ApproachBreadcrumbSequence=step.StartBreadcrumbSequence,PhysicalApproachBreadcrumbSequence=step.StartBreadcrumbSequence,
                Steps=new System.Collections.Generic.List<FollowerRouteMissionStep>{step},
                RouteBreadcrumbs=BreadcrumbRoute.Where(n=>n!=null&&n.Sequence>=Math.Max(0,step.StartBreadcrumbSequence-32)).Select(CloneRouteMissionBreadcrumb).ToList()};
            if(!RegisterLifecycleMission(bot,m))return false;
            LearnedTraversalCommitments[bot.ProfileId]=CapabilitySemanticCommitment(m,step);
            WriteEvent("capabilityLocalMissionRegistered",bot,new{missionId=m.MissionId,source,step.EdgeId,step.EpisodeId,
                nativeSuccessRecorded=false,routeBindingRequired=true});
            return true;
        }
        private static bool TryPrepareCapabilityAutomaticProbeMission(BotOwner bot,string source,int edge,out string reason)
        {
            reason=string.Empty;
            if(edge<=0)return true; // Explicit edge-less diagnostic probes are not recorded route actions.
            if(bot==null){reason="automaticProbeBotUnavailable";return false;}
            if(FollowerRouteMissions.TryGetValue(bot.ProfileId,out var m))
            {
                if(m.Lifecycle.Retired||m.CurrentStepIndex<0||m.CurrentStepIndex>=m.Steps.Count||m.Steps[m.CurrentStepIndex]?.EdgeId!=edge)
                {reason="automaticProbeForeignOrRetiredMission";return false;}
                // Resolve the old terminal epoch BEFORE allocating the new probe/writer.
                // The existing supersession gate alone decides whether this route is fresh.
                if(IsLearningV350TerminalPhysicalRecoveryEligible(bot)&&
                    !TrySupersedeLearningV362TerminalRecoveryByFreshRouteEpoch(bot,m,"capabilityPreProbeAdmission"))
                {reason="automaticProbeTerminalEpochStillOwnsRecovery";return false;}
                if(m.CurrentStepIndex<0||m.CurrentStepIndex>=m.Steps.Count||m.Steps[m.CurrentStepIndex]?.EdgeId!=edge)
                {reason="automaticProbeSupersessionChangedCurrentStep";return false;}
                if(!LearnedTraversalCommitments.TryGetValue(bot.ProfileId,out var bound)||bound==null||
                    bound.EpisodeId!=m.Steps[m.CurrentStepIndex].EpisodeId){reason="automaticProbeMissingCurrentCommitment";return false;}
                if(IsCapabilityDebtResolved(bot,bound.EpisodeId,bound.EdgeId)){reason="automaticProbeEpisodeAlreadyResolved";PruneCapabilityResolvedCommitment(bot);return false;}
                bound.RouteMissionId=m.MissionId;bound.RouteMissionStepIndex=m.CurrentStepIndex;
                return true;
            }
            // Unrecorded, transient navigation recovery is not a fabricated player episode.
            if(LearnedTraversalCommitments.TryGetValue(bot.ProfileId,out var navigation)&&navigation!=null&&
                navigation.EdgeId==edge&&navigation.SkillTransferTransient&&navigation.EpisodeId<=0)return true;
            if(!LearnedTraversalCommitments.TryGetValue(bot.ProfileId,out var c)||c==null||c.EdgeId!=edge||c.ExpiresAt<Time.time||
                !TraversalEpisodeStream.Any(e=>e!=null&&e.EpisodeId==c.EpisodeId&&e.EdgeId==edge))
            {reason="automaticProbeRequiresRecordedCommitment";return false;}
            if(IsCapabilityDebtResolved(bot,c.EpisodeId,c.EdgeId)){reason="automaticProbeEpisodeAlreadyResolved";PruneCapabilityResolvedCommitment(bot);return false;}
            var e=TraversalEpisodeStream.Last(x=>x!=null&&x.EpisodeId==c.EpisodeId&&x.EdgeId==edge);
            var step=CreateFollowerRouteMissionStep(e);
            bool admissionRejected=step==null;
            if(admissionRejected&&IsCapabilityEpisodeRetired(e))
            {
                ClearLearnedTraversalCommitment(bot,edge,"recordedEpisodeExecutionRetired");
                global::BotTraversal.LegacyOriginalPITFollowBridge.CancelDelivery(bot.ProfileId);
                reason="automaticProbeEpisodeExecutionRetired";
                if(ShouldEmitDiagnostic("retiredEpisodeRequest:"+e.EpisodeId,bot,.5f))
                WriteEvent("capabilityRetiredEpisodeRequestSuppressed",bot,new{edge,episode=e.EpisodeId,
                    newMissionRegistered=false,nativeSuccessRecorded=false});
                return false;
            }
            if(admissionRejected)step=new FollowerRouteMissionStep {EpisodeId=e.EpisodeId,EdgeId=edge,ChainId=e.ChainId,
                StartBreadcrumbSequence=e.StartBreadcrumbSequence,EndBreadcrumbSequence=e.EndBreadcrumbSequence,
                ChainLocalExecutable=c.ChainLocalExecutable,CurrentRaidRouteAuthority=false,CreatedAt=Time.time,
                Entry=e.Entry,Exit=e.Exit,RawEntry=e.RawStart,RawExit=e.RawEnd,Kind=e.Kind};
            // Keep the recorded WHAT; transient HOW geometry remains only on the frozen commitment.
            if(!c.SkillTransferTransient){step.Entry=c.Entry;step.Exit=c.Exit;step.RawEntry=c.RawEntry;step.RawExit=c.RawExit;}
            if(!RegisterCapabilityLocalMission(bot,step,"automaticLearnedProbe")){reason="automaticProbeMissionRegistrationFailed";return false;}
            // Preserve the physical strategy and geometry selected by this automatic request.
            c.RouteMissionId=FollowerRouteMissions[bot.ProfileId].MissionId;c.RouteMissionStepIndex=0;
            LearnedTraversalCommitments[bot.ProfileId]=c;
            if(admissionRejected)
            {
                WriteEvent("capabilityAutomaticNativeAdmissionRejected",bot,new{missionId=c.RouteMissionId,edge,
                    episode=c.EpisodeId,reason="typedStepCreationRejected",nativeActuatorTriggered=false,nativeSuccessRecorded=false});
                TryRequestCapabilityFullRouteRetry(bot,FollowerRouteMissions[bot.ProfileId],"automaticNativeAdmissionStepRejected");
                reason="automaticNativeAdmissionRejectedFailureSequenceOwnsRetry";
                return false;
            }
            return true;
        }
        private static void ReleaseCapabilityPortalWait(FollowerRouteMission m)
        {
            if(m.PortalQueueWaiting&&m.PortalQueueWaitStartedAt>=0)
            {
                float wait=Mathf.Max(0,Time.time-m.PortalQueueWaitStartedAt);
                m.ExpiresAt+=wait;m.LastProgressAt+=wait;
                if(m.FailureSequence?.Pending==true){m.FailureSequence.PendingAt+=wait;if(m.FailureSequence.ApproachObserved)m.FailureSequence.ApproachProgressAt+=wait;}
            }
            m.PortalQueueWaiting=false;m.PortalQueueWaitStartedAt=-1;m.PortalQueueHoldGeneration=0;m.PortalQueueHoldPath=null;
        }

        private sealed class CapabilityFailureTransaction
        {
            internal S Stage;internal int Episode,Edge,Step,LastFailureFrame=-1;
            internal LearnedTraversalCommitment Original,Selected;
            internal bool Pending;internal float PendingAt;
            internal ProbeRuntime DeferredProbe;internal string DeferredReason;
            internal bool ApproachObserved;internal float ApproachBestDistance,ApproachProgressAt;
        }
        internal static bool HasCapabilityFailureStrategy(BotOwner bot)
            => bot!=null&&FollowerRouteMissions.TryGetValue(bot.ProfileId,out var m)&&m.FailureSequence!=null&&
                FollowerCapabilityFailureSequence.OwnsStrategy(m.FailureSequence.Stage,
                    !m.Lifecycle.Retired&&m.CurrentStepIndex>=0&&m.CurrentStepIndex<m.Steps.Count&&
                    m.FailureSequence.Step==m.CurrentStepIndex&&m.FailureSequence.Edge==m.Steps[m.CurrentStepIndex].EdgeId&&
                    m.FailureSequence.Episode==m.Steps[m.CurrentStepIndex].EpisodeId);
        private static bool IsCapabilityStrategyPending(BotOwner bot)
            => HasCapabilityFailureStrategy(bot)&&FollowerRouteMissions[bot.ProfileId].FailureSequence.Pending&&!HasActiveTraversalProbe(bot);
        private static bool TryAdmitCapabilitySequenceProbe(BotOwner bot,string source,int edge,string kind,out string reason)
        {
            reason=string.Empty;
            if(!CanAllocateCapabilityWalkingKind(bot,kind))
            {reason="provenWalkingContractRejectsActuatorRecovery";return false;}
            if(!HasCapabilityFailureStrategy(bot))
            {
                if(!FollowerCapabilityPassagePolicy.UnownedSequenceProbe(string.Equals(source,"capabilitySequenceOwnedDispatch",StringComparison.Ordinal)))
                {reason="sequenceOwnedDispatchRequiresLiveSelectedTransaction";return false;}
                return true;
            }
            var m=FollowerRouteMissions[bot.ProfileId];var state=m.FailureSequence;var selected=state.Selected;
            if(!FollowerCapabilityFailureSequence.CanAllocateStrategy(true,state.Pending,
                string.Equals(source,"capabilitySequenceOwnedDispatch",StringComparison.Ordinal),
                selected!=null&&selected.RouteMissionId==m.MissionId&&selected.RouteMissionStepIndex==m.CurrentStepIndex&&
                selected.EdgeId==edge&&string.Equals(selected.Kind,kind,StringComparison.OrdinalIgnoreCase)))
            {
                reason="sequenceSelectedAttemptOwnsProbeAllocation";
                if(ShouldEmitDiagnostic("capabilitySequenceForeignProbeBlocked",bot,.25f))
                    WriteEvent("capabilitySequenceForeignProbeBlocked",bot,new{missionId=m.MissionId,source,
                        requestedEdge=edge,requestedKind=kind,selectedKind=selected?.Kind,
                        selectedTemplate=selected?.SkillTemplateEdgeId??0,pending=state.Pending,nativeSuccessRecorded=false});
                return false;
            }
            // Republish the selected HOW at the allocation boundary, never a generic semantic refresh.
            LearnedTraversalCommitments[bot.ProfileId]=selected;
            return true;
        }
        private static bool IsCapabilityRecoveryPending(BotOwner bot)
            => bot!=null && FollowerRouteMissions.TryGetValue(bot.ProfileId,out var m) &&
                !m.Lifecycle.Retired && m.FailureSequence?.Stage==S.RecoveryTeleport;
        internal static bool IsCapabilityReturnInProgress(BotOwner bot)
            => bot!=null&&FollowerRouteMissions.TryGetValue(bot.ProfileId,out var mission)&&!mission.Lifecycle.Retired&&
                (IsCapabilityRouteRetryReturning(mission)||mission.FailureSequence?.Stage==S.ReturnToEntry);
        private static float CapabilityIngressDistance(BotOwner bot)
        {
            if(bot==null||!FollowerRouteMissions.TryGetValue(bot.ProfileId,out var m)||m.CurrentStepIndex<0||m.CurrentStepIndex>=m.Steps.Count)return 0;
            Vector3 target=IsCapabilityRouteRetryReturning(m)?m.RouteRetry.Stage:
                m.FailureSequence?.Selected?.RawEntry??m.Steps[m.CurrentStepIndex].Entry;
            return Vector3.Distance(bot.Position,target);
        }
        private static bool NeedsCapabilityRunTakeoff(BotOwner bot)
        {
            if(bot==null||IsCapabilityReturnInProgress(bot)||!FollowerRouteMissions.TryGetValue(bot.ProfileId,out var m)||
                m.CurrentStepIndex<0||m.CurrentStepIndex>=m.Steps.Count)return false;
            return (m.FailureSequence?.Selected?.Kind??m.Steps[m.CurrentStepIndex]?.Kind)=="Jump";
        }
        private static float CapabilityIngressSpeed(BotOwner bot,float requested,float distance)
        {
            if(!FollowerRouteMissions.TryGetValue(bot.ProfileId,out var m)||m.CurrentStepIndex<0||m.CurrentStepIndex>=m.Steps.Count)return requested;
            var step=m.Steps[m.CurrentStepIndex];var selected=m.FailureSequence?.Selected;
            var entry=selected?.RawEntry??step.RawEntry;var axis=GetFlatDirection((selected?.RawExit??step.RawExit)-entry);
            var offset=GetFlatDelta(entry,bot.Position);float forward=Vector3.Dot(offset,axis);float lateral=(offset-axis*forward).magnitude;
            bool aligned=axis.sqrMagnitude>.5f&&lateral<=.30f&&
                Vector3.Dot(GetFlatDirection(bot.GetPlayer.MovementContext.PlayerRealForward),axis)>=.95f;
            bool precise=IsCapabilityReturnInProgress(bot)||m.FailureSequence?.Stage==S.RetryAlternate;
            return FollowerCapabilityIngressMotionPolicy.ApproachSpeed(requested,distance,precise,aligned,(selected?.Kind??step.Kind)=="Jump");
        }
        internal static float LimitCapabilityIngressSpeed(BotOwner bot,float requested)
            => IsCapabilityRetryWalking(bot)?CapabilityIngressSpeed(bot,requested,CapabilityIngressDistance(bot)):requested;
        private static bool TryGetCapabilityRouteSprint(BotOwner bot,Vector3 requestedDirection,out bool sprint)
        {
            sprint=false;
            if(bot?.GetPlayer?.MovementContext==null||!IsCapabilityRouteRetrySafe(bot)||
                !FollowerRouteMissions.TryGetValue(bot.ProfileId,out var m)||m.Lifecycle.Retired||
                m.CurrentStepIndex<0||m.CurrentStepIndex>=m.Steps.Count)return false;
            var step=m.Steps[m.CurrentStepIndex];if(step==null)return false;
            bool hasProbe=TryGetActiveProbe(bot,out var probe);
            if(hasProbe&&(!IsProbeLifecycleCurrent(probe)||!ReferenceEquals(probe.LifecycleMission,m)||!ReferenceEquals(probe.LifecycleStep,step)))return false;
            if(hasProbe&&!FollowerCapabilityIngressMotionPolicy.ExecutionGaitPhase(probe.Stage==ProbeStage.Egress,
                probe.Stage==ProbeStage.Verify,probe.Stage==ProbeStage.ExitCommit))return false;
            if(!bot.GetPlayer.MovementContext.IsGrounded)return false;
            if(hasProbe&&probe.NativeExecutorConsumed&&(probe.RequestedTraversalKind=="Jump"||probe.RequestedTraversalKind=="Vault"||probe.RequestedTraversalKind=="Climb"))return false;
            if(!bot.GetPlayer.MovementContext.CanSprint||bot.GetPlayer.MovementContext.PoseLevel<.9f)return true;
            bool correction=IsCapabilityRetryWalking(bot);
            if(hasProbe&&FollowerCapabilityIngressMotionPolicy.WalkingGaitCorrection(probe.Stage==ProbeStage.Aligning,
                NeedsCapabilityWalkThroughPrecision(probe,requestedDirection)))correction=true;
            if(IsCapabilityPortalWaiting(bot)||correction)return true;
            var gait=step;Vector3 tangent=GetFlatDirection(step.RawExit-step.RawEntry);
            if(hasProbe&&probe.RequestedTraversalKind=="WalkThrough")
                tangent=GetFlatDirection(GetCurrentWalkThroughCorridorTarget(probe,bot.Position)-bot.Position);
            if((step.Kind=="WalkThrough"||step.Kind=="PhysicalCorridor")&&m.CurrentStepIndex+1<m.Steps.Count)
            {
                var next=m.Steps[m.CurrentStepIndex+1];
                if(next!=null&&(next.Kind=="Jump"||next.Kind=="Vault"||next.Kind=="Climb")&&
                    IsRouteMissionSameChainImmediateSuccessor(m,m.CurrentStepIndex+1)&&GetPlanarDistance(bot.Position,next.RawEntry)<=RouteMissionTraversalAdjacentBridgeMaxDistance)gait=next;
            }
            bool jump=gait.Kind=="Jump";
            Vector3 entry=jump?GetLearningV442JumpAdmissionEntry(gait):(gait.Kind=="WalkThrough"||gait.Kind=="PhysicalCorridor"?gait.RawExit:gait.RawEntry);
            var axis=GetFlatDirection(gait.RawExit-gait.RawEntry);
            if(gait==step&&(step.Kind=="WalkThrough"||step.Kind=="PhysicalCorridor")&&tangent.sqrMagnitude>.5f)axis=tangent;
            if(axis.sqrMagnitude<.5f)return true;
            if(tangent.sqrMagnitude<.5f)tangent=axis;
            if(requestedDirection.sqrMagnitude>.5f)tangent=GetFlatDirection(requestedDirection);
            var offset=GetFlatDelta(entry,bot.Position);float forward=Vector3.Dot(offset,axis),lateral=(offset-axis*forward).magnitude;
            if(gait==step&&(step.Kind=="WalkThrough"||step.Kind=="PhysicalCorridor"))
            {
                if(hasProbe&&probe.RequestedTraversalKind=="WalkThrough"&&TryProjectOntoWalkThroughCorridor(
                    probe,bot.Position,out _,out _,out _,out float corridorLateral))lateral=corridorLateral;
            }
            var sample=m.RouteBreadcrumbs.Where(n=>n!=null&&n.Grounded&&(n.Sprinting.HasValue||n.State=="Sprint"||n.State=="Run")&&
                n.Sequence>=gait.StartBreadcrumbSequence-4&&n.Sequence<=gait.EndBreadcrumbSequence)
                .OrderBy(n=>GetPlanarDistance(n.Position,bot.Position)).FirstOrDefault();
            var edge=LearnedTraversalEdges.FirstOrDefault(x=>x!=null&&x.Id==gait.EdgeId);
            bool demonstrated=FollowerCapabilityPassagePolicy.SprintIntent(sample?.Sprinting,sample?.State,edge?.ApproachMode);
            sprint=FollowerCapabilityIngressMotionPolicy.ExecutionSprint(demonstrated,
                bot.Mover.Sprinting||bot.GetPlayer.MovementContext.IsSprintEnabled||bot.GetPlayer.CurrentStateName==EPlayerState.Sprint,
                bot.GetPlayer.MovementContext.CanSprint,correction,forward,lateral,
                Vector3.Dot(GetFlatDirection(bot.GetPlayer.MovementContext.PlayerRealForward),axis),Vector3.Dot(tangent,axis),GetLearningV329PlanarSpeed(bot.GetPlayer));
            return true;
        }
        internal static bool IsCapabilityRetryWalking(BotOwner bot)
        {
            // Queue waiting has its own zero-input owner. Run is an entry correction, not an executor-wide mode.
            if(IsCapabilityPortalWaiting(bot))return false;
            return NeedsCapabilityEntryRun(bot);
        }

        private static bool IsCapabilityForwardFailurePhase(BotOwner bot)
            => bot!=null&&FollowerRouteMissions.TryGetValue(bot.ProfileId,out var m)&&m.FailureSequence!=null&&
                (m.FailureSequence.Stage==S.Alternate||m.FailureSequence.Stage==S.SelfRescue);

        private static LearnedTraversalCommitment CapabilitySemanticCommitment(FollowerRouteMission mission,FollowerRouteMissionStep step)
            => new LearnedTraversalCommitment { EdgeId=step.EdgeId,EpisodeId=step.EpisodeId,ChainId=step.ChainId,
                Entry=step.Entry,Exit=step.Exit,RawEntry=step.RawEntry,RawExit=step.RawExit,Kind=step.Kind,
                RouteMissionId=mission.MissionId,RouteMissionStepIndex=mission.CurrentStepIndex,
                ChainLocalExecutable=step.ChainLocalExecutable,HasSettledEntry=true,SettledEntry=step.Entry,HasSettledExit=true,SettledExit=step.Exit,ExpiresAt=Time.time+30 };

        private static bool DeferCapabilitySequenceFailure(BotOwner bot,FollowerRouteMission m,ProbeRuntime probe,string reason)
        {
            if(m==null||m.Lifecycle.Retired||m.CurrentStepIndex<0||m.CurrentStepIndex>=m.Steps.Count)return false;
            var step=m.Steps[m.CurrentStepIndex];if(step==null)return false;
            if(m.FailureSequence==null)m.FailureSequence=new CapabilityFailureTransaction {
                Stage=S.Native,Episode=step.EpisodeId,Edge=step.EdgeId,Step=m.CurrentStepIndex,Original=CapabilitySemanticCommitment(m,step)};
            var state=m.FailureSequence;
            if(state.DeferredReason==null){state.DeferredProbe=probe;state.DeferredReason=reason;}
            if(ShouldEmitDiagnostic("capabilitySequenceFailureDeferred",bot,.5f))
                WriteEvent("capabilitySequenceFailureDeferred",bot,new{missionId=m.MissionId,stepIndex=m.CurrentStepIndex,reason,
                    nativeSuccessRecorded=false,oldFallbackAllowed=false});
            return true; // Remember failure without entering a competing writer or old Assist branch.
        }

        private static bool TryAdvanceCapabilityFailureSequence(BotOwner bot,FollowerRouteMission mission,string reason)
        {
            var step=mission.Steps[mission.CurrentStepIndex];if(step==null)return false;
            var state=mission.FailureSequence;
            if(state==null||state.Episode!=step.EpisodeId||state.Edge!=step.EdgeId)
                mission.FailureSequence=state=new CapabilityFailureTransaction {Stage=S.Native,Episode=step.EpisodeId,
                    Edge=step.EdgeId,Step=mission.CurrentStepIndex,Original=CapabilitySemanticCommitment(mission,step)};
            bool hasProbe=TryGetActiveProbe(bot,out var active);
            if(!hasProbe&&state.Pending&&!FollowerCapabilityIngressMotionPolicy.PendingDeadline(reason))return true;
            if(!hasProbe&&state.LastFailureFrame==Time.frameCount)return true;
            if(hasProbe&&(!IsProbeLifecycleCurrent(active)||!ReferenceEquals(active.LifecycleMission,mission)))return true;
            state.LastFailureFrame=Time.frameCount;
            // Publish the next stage before retiring the old probe. ForceRelease may
            // synchronously run old route-frontier callbacks; no candidate is admitted
            // while this existing transaction has no selected/pending attempt.
            state.Stage=FollowerCapabilityFailureSequence.AfterFailure(state.Stage);
            state.Pending=false;state.Selected=null;state.ApproachObserved=false;
            if(hasProbe)
            {
                TryCommitLearningV433FailedPhysicalActuatorEpoch(active,reason);
                ForceReleaseTraversalProbe(active,"sequenceAttemptFailed:"+reason);
            }
            ClearCapabilityStrategyTargets(bot);
            // A missing local alternate is one recorded failed selection, not a return to an old entry.
            for(int i=0;i<2&&(state.Stage==S.Alternate||state.Stage==S.SelfRescue);i++)
            {
                if(TrySelectCapabilitySequenceStrategy(bot,mission,state,state.Stage==S.SelfRescue))
                {state.Pending=true;state.PendingAt=Time.time;break;}
                WriteEvent("capabilitySequenceSelectionFailed",bot,new{missionId=mission.MissionId,stage=state.Stage.ToString(),reason="noExecutableTemplate",nativeSuccessRecorded=false});
                state.Stage=FollowerCapabilityFailureSequence.AfterFailure(state.Stage);
            }
            WriteEvent("capabilityFailureSequenceAdvanced",bot,new{missionId=mission.MissionId,stepIndex=mission.CurrentStepIndex,
                edgeId=step.EdgeId,episodeId=step.EpisodeId,stage=state.Stage.ToString(),reason,
                selectedTemplate=state.Selected?.SkillTemplateEdgeId??0,completedPrefixRetained=true,nativeSuccessRecorded=false});
            if(state.Stage==S.ReturnToEntry)
            {
                // Rebuild from the current unresolved step. Completed WT/Climb is never replay debt.
                mission.RouteRetry=null;
                return false; // Existing, scoped return driver takes over after this admission.
            }
            return true;
        }

        private static bool TrySelectCapabilitySequenceStrategy(BotOwner bot,FollowerRouteMission mission,CapabilityFailureTransaction state,bool generalized)
        {
            var body=bot.GetPlayer.Position;var original=state.Original;
            var axis=GetFlatDirection(original.RawExit-original.RawEntry);
            LearnedTraversalEdge best=null;Vector3 entry=default,exit=default;double rank=double.NegativeInfinity;
            var originalEdge=LearnedTraversalEdges.FirstOrDefault(x=>x!=null&&x.Id==state.Edge);
            bool walkingOnly=IsCapabilityWalkingRecoveryOnly(original);
            if(walkingOnly&&generalized)return TrySelectCapabilityWalkingRescue(bot,mission,state);
            if(generalized&&!TryObserveCapabilityRescueObstacle(bot,axis,out string obstacleFailure))
            {
                WriteEvent("capabilitySequenceObstacleUnavailable",bot,new{missionId=mission.MissionId,
                    stepIndex=mission.CurrentStepIndex,semanticEdge=state.Edge,reason=obstacleFailure,
                    attemptedStage="SelfRescue",nativeSuccessRecorded=false});
                return false; // Explicit physical refusal advances the existing ladder, never a history veto.
            }
            float requestedSpan=Mathf.Clamp(GetPlanarDistance(body,original.RawExit),.4f,2.5f);
            float requestedHeight=originalEdge?.NativeVaultParameters?.VaultingHeight??Mathf.Max(.4f,original.RawExit.y-body.y);
            foreach(var e in LearnedTraversalEdges)
            {
                if(e==null||(!generalized&&e.Id==state.Edge))continue;
                if(e.Kind!="WalkThrough"&&e.Kind!="Jump"&&e.Kind!="DropDown"&&e.Kind!="Vault"&&e.Kind!="Climb")continue;
                if(!FollowerCapabilityPassagePolicy.WalkingRecoveryKind(walkingOnly,e.Kind))continue;
                // Learning quality ranks, never suppresses, the requested self-rescue attempt.
                if(generalized&&!IsLearningV433ObstacleClearanceActuatorKind(e.Kind))continue;
                if(!generalized&&e.Disabled)continue;
                if((e.Kind=="Vault"||e.Kind=="Climb")&&e.NativeVaultParameters==null)continue;
                var a=e.RawStart;var b=e.RawEnd;if(!IsFinite(a)||!IsFinite(b))continue;
                var direction=GetFlatDirection(b-a);float span=GetPlanarDistance(a,b);
                if(span<.20f)continue;
                if(generalized)
                {
                    // Rebase learned relative native parameters on the current physical frontier.
                    a=CapabilitySelfRescueFrontier(bot,state);b=a+axis*Mathf.Clamp(span,.40f,2.5f);
                    b.y=a.y+Mathf.Clamp(e.RawEnd.y-e.RawStart.y,-1.2f,1.2f);
                }
                else
                {
                    if(Vector3.Dot(direction,axis)<.85f||GetPlanarDistance(body,a)>2.5f||
                        GetPlanarDistance(b,original.RawExit)>1.5f||Mathf.Abs(a.y-body.y)>.65f)continue;
                    if(FollowerCapabilityFailureSequence.PastObstacleEntry(e.Kind=="Vault"||e.Kind=="Climb",
                        Vector3.Dot(GetFlatDelta(a,body),direction),InjectedNativeVaultEntryMaxForwardOvershoot))continue;
                    float forward=Vector3.Dot(GetFlatDelta(body,a),axis),end=Vector3.Dot(GetFlatDelta(body,b),axis);
                    if(!FollowerCapabilityFailureSequence.ForwardCandidate(forward,end,0,forward))continue;
                    var path=CalculateStrictPath(body,a);
                    if(path?.StrictComplete!=true||path.Corners==null||path.Corners.Length==0||
                        path.PathLength>Mathf.Max(1f,GetPlanarDistance(body,a)*1.8f+.5f))continue;
                    // Every returned corner must stay ahead of the current completed physical prefix.
                    if(path.Corners.Any(c=>Vector3.Dot(GetFlatDelta(body,c),axis)<-.14f))continue;
                }
                double score=RankCapabilityLearnedRecovery(e,GetPlanarDistance(body,a));
                if(generalized)
                {
                    float height=e.NativeVaultParameters?.VaultingHeight??Mathf.Max(.4f,e.RawEnd.y-e.RawStart.y);
                    score-=1.5*Math.Abs(height-requestedHeight)+.2*Math.Abs(span-requestedSpan);
                    if(e.ObstacleFingerprint!=null&&originalEdge?.ObstacleFingerprint!=null)
                        score-=.25*Math.Abs(e.ObstacleFingerprint.BoundsSize.y-originalEdge.ObstacleFingerprint.BoundsSize.y);
                }
                if(score>rank||(score==rank&&best!=null&&e.Id<best.Id)){best=e;entry=a;exit=b;rank=score;}
            }
            if(best==null)
            {
                if(!generalized)return false;
                var scan=CapabilitySemanticCommitment(mission,mission.Steps[mission.CurrentStepIndex]);
                var frontier=CapabilitySelfRescueFrontier(bot,state);
                scan.Kind="Vault";scan.Entry=scan.RawEntry=scan.SettledEntry=frontier;
                scan.Exit=scan.RawExit=scan.SettledExit=frontier+axis;
                scan.SkillTransferTransient=true;scan.LearningV382RouteBoundTransientNativeSkill=true;
                scan.SkillTransferNearFrontier=scan.RawEntry;scan.SkillTransferFarFrontier=scan.RawExit;
                scan.SkillTransferSource="learningV433EquivalentPhysicalActuatorFrontier:RC121:LiveScanner";state.Selected=scan;
                WriteEvent("capabilitySequenceLiveScannerSelected",bot,new{missionId=mission.MissionId,nativeSuccessRecorded=false});
                return true;
            }
            // Same semantic edge/episode/chain for binding; only the physical HOW/template changes.
            var selected=CapabilitySemanticCommitment(mission,mission.Steps[mission.CurrentStepIndex]);
            selected.Kind=best.Kind;selected.Entry=selected.RawEntry=selected.SettledEntry=entry;
            selected.Exit=selected.RawExit=selected.SettledExit=exit;
            selected.LearningV382RouteBoundTransientNativeSkill=true;selected.SkillTransferTransient=true;selected.SkillTemplateEdgeId=best.Id;
            selected.SkillTransferSource="learningV433EquivalentPhysicalActuatorFrontier:RC121:"+state.Stage;
            selected.SkillTransferNearFrontier=entry;selected.SkillTransferFarFrontier=exit;
            selected.ExpiresAt=Time.time+30;selected.LastEntryDistance=GetPlanarDistance(body,entry);selected.LastEntryProgressAt=Time.time;
            state.Selected=selected;
            WriteEvent("capabilitySequenceStrategySelected",bot,new{missionId=mission.MissionId,stage=state.Stage.ToString(),
                semanticEdge=state.Edge,templateEdge=best.Id,kind=best.Kind,rank,generalized,
                entry=Vec(entry),exit=Vec(exit),body=Vec(body),semanticEntry=Vec(original.RawEntry),frontierAtBody=generalized&&GetPlanarDistance(entry,body)<.01f,nativeSuccessRecorded=false});
            return true;
        }

        private static bool TryObserveCapabilityRescueObstacle(BotOwner bot,Vector3 axis,out string reason)
        {
            reason="selfRescueNoCurrentBlockingObstacle";
            var player=bot?.GetPlayer;
            if(player?.MovementContext==null||axis.sqrMagnitude<.5f)return false;
            bool hit=TryGetFirstNonSelfCapsuleCastHit(player,player.Position+Vector3.up*.40f,
                player.Position+Vector3.up*1.35f,.18f,axis,.85f,LayerMaskClass.HighPolyWithTerrainMask,out var obstacle);
            bool accepted=FollowerCapabilityPassagePolicy.RescueObstacle(player.MovementContext.IsGrounded,hit,
                hit?obstacle.normal.y:0,hit?-Vector3.Dot(obstacle.normal,axis):0,hit?obstacle.distance:0);
            if(accepted)reason=string.Empty;
            if(ShouldEmitDiagnostic("capabilityRescueObstacleEvidence",bot,.25f))WriteEvent("capabilityRescueObstacleEvidence",bot,new{
                accepted,grounded=player.MovementContext.IsGrounded,hit,normal=Vec(obstacle.normal),distance=obstacle.distance,
                body=Vec(player.Position),direction=Vec(axis),nativeSuccessRecorded=false});
            return accepted;
        }
        private static bool TryRetainCapabilityObservedJumpBeforePassageBridge(ProbeRuntime probe)
        {
            if(!IsProbeLifecycleCurrent(probe)||probe.LifecycleMission==null||probe.LifecycleStep==null||
                probe.Player==null||probe.LifecycleStep.Kind!="WalkThrough")return false;
            var m=probe.LifecycleMission;var step=probe.LifecycleStep;
            if(m.CurrentStepIndex+1!=m.Steps.Count)return false;
            bool passageProof=TryLatchLearningV372WalkThroughPhysicalPassageProof(probe,"recordedActuatorBoundary")&&
                probe.LearningV372WalkThroughPhysicalPassageProven;
            bool physical=IsFollowerRouteMissionStepPhysicallyPassed(probe.Bot,step,out _,out _);
            bool supported=IsEffectivelySupportedForProbe(probe.Bot,probe.Player);
            bool released=!IsProbeTraversalState(probe.Player.CurrentStateName);
            bool clean=!probe.LearningV341ForcedEgressPhysicalWriterUsed&&!probe.TraversalTeleportAttempted&&!probe.TeleportFallbackUsed;
            foreach(var e in TraversalEpisodeStream.Where(e=>e!=null&&e.Kind=="Jump"&&e.EpisodeId>step.EpisodeId&&
                e.StartBreadcrumbSequence>=step.EndBreadcrumbSequence-TraversalChainImmediateMaxBreadcrumbGap&&
                e.StartBreadcrumbSequence<=step.EndBreadcrumbSequence+TraversalChainImmediateMaxBreadcrumbGap)
                .OrderBy(e=>e.StartBreadcrumbSequence).ThenBy(e=>e.EpisodeId))
            {
                if(m.Steps.Any(x=>x!=null&&x.EpisodeId==e.EpisodeId)||
                    !(HasLearningV437JumpEpisodePostLandingRetention(e)||IsLearningV453RetainedNativeProvenJumpSubtrajectory(e)))continue;
                var next=CreateFollowerRouteMissionStep(e);
                bool adjacent=next!=null&&next.CurrentRaidRouteAuthority&&IsFinite(next.RawEntry)&&IsFinite(next.RawExit)&&
                    GetPlanarDistance(probe.Player.Position,next.RawEntry)<=RouteMissionTraversalAdjacentBridgeMaxDistance&&
                    GetPlanarDistance(step.RawExit,next.RawEntry)<=TraversalChainMaxSpatialGap;
                if(!FollowerCapabilityFailureSequence.CanClosePassageBoundary(true,
                    passageProof,physical,supported,released,adjacent,clean))continue;
                if(!InsertLifecycleMissionStep(m,m.CurrentStepIndex+1,next))return false;
                m.TargetBreadcrumbSequence=Math.Max(m.TargetBreadcrumbSequence,next.EndBreadcrumbSequence);
                WriteEvent("capabilityObservedJumpRetainedBeforePassageBridge",probe.Bot,new{missionId=m.MissionId,
                    completedCandidateEdge=step.EdgeId,nextEdge=next.EdgeId,nextEpisode=next.EpisodeId,
                    currentBody=Vec(probe.Player.Position),nativeSuccessRecorded=false,persistentLearningChanged=false});
                return TryFinalizeTraversalChainIntermediate(probe,"recordedJumpOwnsNextPhysicalCrossing");
            }
            return false;
        }

        private static void ClearCapabilityStrategyTargets(BotOwner bot)
        {
            if(!HasActiveTraversalProbe(bot)&&!HasCurrentCapabilityNativeWay(bot,out _)&&IsCapabilityRouteRetrySafe(bot))
            {
                long generation=FollowerMovementWriterAuthority.PeekExactGeneration(bot,out var owner);
                if((owner==FollowerMovementWriterAuthority.WriterDomain.Follow||owner==FollowerMovementWriterAuthority.WriterDomain.Traversal||owner==FollowerMovementWriterAuthority.WriterDomain.PostTraversal)&&
                    TryRetireNativeMoverPathPreserveLocomotion(bot,"failureStrategyHandoff",out _)&&
                    FollowerMovementWriterAuthority.TryEnterBoundPhysicalMotion(bot,owner,generation,out var scope,out _))
                    using(scope)
                    {
                        ClearCapabilityNativeDestinationData(bot);
                        CommitCapabilityRouteRetryZeroInput(bot,bot.GetPlayer);
                    }
            }
            var id=bot.ProfileId;
            if(FollowerRouteMissions.TryGetValue(id,out var inputMission)&&inputMission.CurrentStepIndex>=0&&inputMission.CurrentStepIndex<inputMission.Steps.Count)
                inputMission.Steps[inputMission.CurrentStepIndex].CapabilityInputAt=float.NegativeInfinity;
            global::BotTraversal.LegacyOriginalPITFollowBridge.CancelDelivery(id);
            LearnedTraversalCommitments.Remove(id);NavigationRecoveryTraversalAnchors.Remove(id);
            BreadcrumbFollowCommitments.Remove(id);FollowRejectLeases.Remove(id);
            PostTraversalChaseLeases.Remove(id);LearningV430PendingPostTraversalFollowRollovers.Remove(id);
            TraversalChainLocomotionIntents.Remove(id);LearningV444TransitionMomentumEpochs.Remove(id);
            FollowReacquireWatches.Remove(id);CF63DormantFollowWatches.Remove(id);CF64FollowPhysicalWatches.Remove(id);
            LearningV365ContinuousRouteProducerStates.Remove(id);ChainHandoffAuthorityGuards.Remove(id);
            ProvisionalWalkThroughReservations.Remove(id);PhysicalNavigationEpochHandoffs.Remove(id);
            if(FollowerTraversalRouteCursors.TryGetValue(id,out var cursor))
            {cursor.LearningV375TransactionActive=false;cursor.LearningV375RouteMissionId=0;}
        }

        private static void PrepareCapabilityAlternateReplay(FollowerRouteMission fresh)
        {
            if(fresh.FailureSequence==null)return;
            var state=fresh.FailureSequence;state.Stage=S.RetryAlternate;state.Step=0;state.Pending=true;state.PendingAt=Time.time;
            var step=fresh.Steps[0];state.Episode=step.EpisodeId;state.Edge=step.EdgeId;
            state.Original=CapabilitySemanticCommitment(fresh,step);
            state.Selected=null;state.ApproachObserved=false;state.DeferredReason=null;state.DeferredProbe=null;state.LastFailureFrame=-1;
        }

        private static bool TickCapabilityFailureSequence(BotOwner bot)
        {
            if(IsCapabilityPortalWaiting(bot)&&TickCapabilityPortalWait(bot))return true;
            if(bot==null||!FollowerRouteMissions.TryGetValue(bot.ProfileId,out var m)||m.Lifecycle.Retired||
                m.CurrentStepIndex<0||m.CurrentStepIndex>=m.Steps.Count)return false;
            var state=m.FailureSequence;if(state==null)return false;
            if(state.DeferredReason!=null)
            {
                if(state.DeferredProbe!=null&&!IsProbeLifecycleCurrent(state.DeferredProbe))
                {state.DeferredProbe=null;state.DeferredReason=null;return false;}
                if(!IsCapabilityRouteRetrySafe(bot))return true;
                string deferred=state.DeferredReason;state.DeferredProbe=null;state.DeferredReason=null;
                TryRequestCapabilityFullRouteRetry(bot,m,deferred);return true;
            }
            if(state.Stage==S.RecoveryTeleport)
            {
                if(!IsCapabilityRouteRetrySafe(bot))return true;
                var leader=FollowerEnvironment.LocalPlayer;
                if(Time.time<m.NextMoveAttemptAt)return true;m.NextMoveAttemptAt=Time.time+1;
                if(leader!=null&&FollowerSafeTeleport.TryFindSafeSpotNearDestination(bot,leader.Position,out var spot))
                {
                    long origin=PrepareForAutomaticRecoveryTeleport(bot,"capabilityFailureSequenceExhausted");
                    if(origin>0&&FollowerSafeTeleport.TryTeleport(bot,spot,"capabilityFailureSequenceExhausted",out _,issuedTeleportGeneration:origin))
                    {
                        // Continue only the exact origin issued above; never borrow a later owner.
                        if(FollowerMovementWriterAuthority.TryEnterBoundPhysicalMotion(bot,
                            FollowerMovementWriterAuthority.WriterDomain.Teleport,origin,out var postTeleportScope,out var scopeFailure))
                            using(postTeleportScope)NotifyAutomaticRecoveryTeleportCompleted(bot,"capabilityFailureSequenceExhausted");
                        else WriteEvent("capabilitySequencePostTeleportScopeRejected",bot,new{origin,scopeFailure,nativeSuccessRecorded=false});
                    }
                }
                else if(ShouldEmitDiagnostic("sequenceTeleportNoSafeSpot",bot,1))
                    WriteEvent("capabilitySequenceRecoveryTeleportBlocked",bot,new{missionId=m.MissionId,nativeSuccessRecorded=false});
                return true;
            }
            if(state.Stage==S.ReturnToEntry && IsCapabilityRouteRetryReturning(m) &&
                !HasActiveTraversalProbe(bot) && IsCapabilityRouteRetrySafe(bot) && Time.time-m.LastProgressAt>4f)
            {
                m.Lifecycle.CancelFullRetry();state.Stage=S.RecoveryTeleport;
                m.NextMoveAttemptAt=0;
                ClearCapabilityStrategyTargets(bot);
                WriteEvent("capabilitySequenceReturnFailed",bot,new{missionId=m.MissionId,reason="returnNoProgress",nativeSuccessRecorded=false});
                return TickCapabilityFailureSequence(bot);
            }
            if(!state.Pending)return false;
            if(HasActiveTraversalProbe(bot)){state.Pending=false;return false;}
            if(!IsCapabilityRouteRetrySafe(bot))return true;
            if(state.Selected==null)
            {
                if(!TrySelectCapabilitySequenceStrategy(bot,m,state,false))state.Selected=CloneTraversalCommitment(state.Original);
                state.Selected.RouteMissionId=m.MissionId;state.Selected.RouteMissionStepIndex=m.CurrentStepIndex;
            }
            float entryDistance=Vector3.Distance(bot.GetPlayer.Position,state.Selected.RawEntry);
            if(!state.ApproachObserved){state.ApproachObserved=true;state.ApproachBestDistance=entryDistance;state.ApproachProgressAt=Time.time;}
            else if(entryDistance<state.ApproachBestDistance-.04f){state.ApproachBestDistance=entryDistance;state.ApproachProgressAt=Time.time;}
            if(FollowerCapabilityIngressMotionPolicy.StrategyExpired(Time.time-state.PendingAt,Time.time-state.ApproachProgressAt))
            {TryRequestCapabilityFullRouteRetry(bot,m,"sequenceStrategyApproachNoMeasuredProgress");return true;}
            if(state.Stage==S.SelfRescue&&!PrepareCapabilityStationaryRescue(bot,m,state))return true;
            entryDistance=Vector3.Distance(bot.GetPlayer.Position,state.Selected.RawEntry);
            LearnedTraversalCommitments[bot.ProfileId]=state.Selected;
            if(IsCapabilityRetryWalking(bot))ApplyCapabilitySequenceWalking(bot,entryDistance);
            if(!FollowerCapabilityPortalPolicy.StrategyEntryReady(entryDistance))
            {DriveCapabilityStrategyApproach(bot,m,state);return true;}
            var selected=state.Selected;
            if(!FollowerCapabilityPortalPolicy.StrategyEntryReady(Vector3.Distance(bot.GetPlayer.Position,selected.RawEntry)))
            {
                if(ShouldEmitDiagnostic("capabilityStrategyEntryNotReached",bot,1f))
                    WriteEvent("capabilityStrategyEntryNotReached",bot,new{missionId=m.MissionId,stage=state.Stage.ToString(),body=Vec(bot.GetPlayer.Position),entry=Vec(selected.RawEntry),nativeProbeAllocated=false});
                return true; // The existing four-second pending deadline above still advances failure.
            }
            if(GetPlanarDistance(bot.GetPlayer.Position,selected.RawEntry)<=RouteMissionTraversalAdjacentBridgeMaxDistance)
            {
                bool started=TryStartProbeForBot(bot,GetFlatDirection(selected.RawExit-selected.RawEntry),
                    "capabilitySequenceOwnedDispatch",selected.EdgeId,selected.Kind,"sequenceStrategy",out string dispatch);
                WriteEvent("capabilitySequenceOwnedDispatch",bot,new{missionId=m.MissionId,stage=state.Stage.ToString(),
                    template=selected.SkillTemplateEdgeId,kind=selected.Kind,started,dispatch,nativeSuccessRecorded=false});
                // Do not expose an allocated strategy to a normal producer that can republish the old entry.
                return true;
            }
            return true; // Pending selection remains exclusive; a stale semantic producer cannot approach its old entry.
        }

        private static bool PrepareCapabilityStationaryRescue(BotOwner bot,FollowerRouteMission m,CapabilityFailureTransaction state)
        {
            var c=state.Selected;var player=bot.GetPlayer;
            if(c==null||!c.SkillTransferTransient||!IsCapabilityRouteRetrySafe(bot)||!player.MovementContext.IsGrounded)return false;
            if(c.Kind=="WalkThrough")return true; // WT retains its selected current route; actuator braking/rebasing does not apply.
            var axis=GetFlatDirection(c.RawExit-c.RawEntry);if(axis.sqrMagnitude<.5f)return false;
            long generation=FollowerMovementWriterAuthority.PeekExactGeneration(bot,out var owner);
            if(generation<=0||(owner!=FollowerMovementWriterAuthority.WriterDomain.Traversal&&owner!=FollowerMovementWriterAuthority.WriterDomain.Follow)||
                !FollowerMovementWriterAuthority.TryEnterBoundPhysicalMotion(bot,owner,generation,out var scope,out _))return false;
            using(scope)
            {
                if(!ReferenceEquals(m.FailureSequence,state)||state.Stage!=S.SelfRescue||!state.Pending||HasActiveTraversalProbe(bot)||IsCapabilityPortalWaiting(bot)||!IsCapabilityRouteRetrySafe(bot)||
                    BotTraversalPlugin.PassThroughEnabled?.Value==false&&!CapabilityPortalQueue.Owns(bot.ProfileId,m.MissionId,m.CurrentStepIndex))return false;
                float speed=GetLearningV329PlanarSpeed(player),heading=Vector3.Dot(GetFlatDirection(player.MovementContext.PlayerRealForward),axis);
                ApplyCollisionActivePhysicalRouteMotion(bot,player,axis,Vector3.zero,Mathf.Max(.001f,Time.deltaTime),1f,false,0f,false,true);
                bool ready=FollowerCapabilityPassagePolicy.RescueSettled(speed,heading);
                if(ready)
                {
                    // Rebase only the selected HOW after physical braking; original semantic debt never moves.
                    var delta=player.Position-c.RawEntry;
                    c.Entry=c.RawEntry=c.SettledEntry=player.Position;c.Exit+=delta;c.RawExit+=delta;c.SettledExit+=delta;
                    c.SkillTransferNearFrontier=c.RawEntry;c.SkillTransferFarFrontier=c.RawExit;
                    state.ApproachBestDistance=0;state.ApproachProgressAt=Time.time;
                }
                if(ShouldEmitDiagnostic("capabilityRescueEntrySettling",bot,.25f))WriteEvent("capabilityRescueEntrySettling",bot,new{
                    missionId=m.MissionId,ready,speed,heading,body=Vec(player.Position),selectedEntry=Vec(c.RawEntry),semanticEntry=Vec(state.Original.RawEntry),nativeSuccessRecorded=false});
                return ready;
            }
        }
        private static void DriveCapabilityStrategyApproach(BotOwner bot,FollowerRouteMission m,CapabilityFailureTransaction state)
        {
            if(!IsCapabilityRouteRetrySafe(bot)||HasActiveTraversalProbe(bot)||IsCapabilityPortalWaiting(bot)||state.Selected==null||
                BotTraversalPlugin.PassThroughEnabled?.Value==false&&!CapabilityPortalQueue.Owns(bot.ProfileId,m.MissionId,m.CurrentStepIndex))return;
            if(state.Stage==S.RetryAlternate&&state.Selected.Kind=="WalkThrough"&&TryDriveCapabilityGroundEntryLead(bot,m,state.Selected.RawEntry,true))return;
            var player=bot.GetPlayer;var target=state.Selected.RawEntry;var path=CalculateStrictPath(player.Position,target);
            bool valid=FollowerCapabilityPortalPolicy.LocalApproachPath(path?.StrictComplete==true,path?.PathLength??float.PositiveInfinity,Vector3.Distance(player.Position,target));
            var step=m.Steps[m.CurrentStepIndex];
            if(valid&&TryGetCapabilityCompletedPrefixBoundary(bot,m,step,out var boundary,out var axis))
                valid=path.Corners!=null&&path.Corners.Length>0&&FollowerCapabilityIngressMotionPolicy.PrefixPath(true,
                    Vector3.Dot(GetFlatDelta(boundary,player.Position),axis),Vector3.Dot(GetFlatDelta(boundary,step.RawEntry),axis),
                    Vector3.Dot(GetFlatDelta(boundary,target),axis),path.Corners.Min(p=>Vector3.Dot(GetFlatDelta(boundary,p),axis)));
            long generation=FollowerMovementWriterAuthority.PeekExactGeneration(bot,out var owner);
            if(generation<=0||(owner!=FollowerMovementWriterAuthority.WriterDomain.Follow&&owner!=FollowerMovementWriterAuthority.WriterDomain.Traversal)||
                !FollowerMovementWriterAuthority.TryEnterBoundPhysicalMotion(bot,owner,generation,out var scope,out _))return;
            using(scope)
            {
                if(!FollowerRouteMissions.TryGetValue(bot.ProfileId,out var live)||!ReferenceEquals(live,m)||!ReferenceEquals(m.FailureSequence,state)||
                    !state.Pending||!IsCapabilityRouteRetrySafe(bot)||HasActiveTraversalProbe(bot))return;
                Vector3 next=target;
                if(valid&&path.Corners!=null)
                    foreach(var corner in path.Corners)if(GetPlanarDistance(player.Position,corner)>.15f){next=corner;break;}
                bool physicalAimReady=valid&&TryGetCapabilitySupportedLocalAim(player,next,out next);
                if(physicalAimReady&&TryRetireNativeMoverPathPreserveLocomotion(bot,"strategyEntryManualConsumer",out _))
                {
                    ClearCapabilityNativeDestinationData(bot);
                    var direction=GetFlatDirection(next-player.Position);float dt=Mathf.Max(.001f,Time.deltaTime);
                    ApplyCollisionActivePhysicalRouteMotion(bot,player,direction,direction*dt,dt,1f,false,
                        CapabilityIngressSpeed(bot,.8f,Vector3.Distance(player.Position,target)),false,true);
                }
                else
                {
                    TryRetireNativeMoverPathPreserveLocomotion(bot,"strategyEntryLocalPathUnavailable",out _);
                    CommitCapabilityRouteRetryZeroInput(bot,player);
                }
                if(ShouldEmitDiagnostic("capabilityStrategyApproachDrive",bot,.5f))WriteEvent("capabilityStrategyApproachDrive",bot,new{
                    missionId=m.MissionId,stage=state.Stage.ToString(),valid,physicalAimReady,body=Vec(player.Position),target=Vec(target),localTarget=Vec(next),
                    pathLength=path?.PathLength??-1,oldInputRenewed=false,nativeSuccessRecorded=false});
            }
        }
        private static bool TryDispatchCapabilitySequenceNative(ProbeRuntime probe,out string failure)
        {
            failure=string.Empty;
            if(!IsProbeLifecycleCurrent(probe))return false;
            var c=probe.FrozenTraversalCommitment;if(c==null)return false;
            if(!FollowerMovementWriterAuthority.TryEnterBoundPhysicalMotion(probe.Bot,
                FollowerMovementWriterAuthority.WriterDomain.Traversal,probe.MovementWriterGeneration,out var nativeScope,out failure))
                return DeferCapabilitySequenceFailure(probe.Bot,probe.LifecycleMission,probe,"nativeDispatchOriginUnavailable");
            using(nativeScope)
            {
            probe.NativeStrategySelected=true;probe.AssistedStrategySelected=false;
            var edge=LearnedTraversalEdges.FirstOrDefault(x=>x!=null&&x.Id==(c.SkillTransferTransient?c.SkillTemplateEdgeId:c.EdgeId));
            bool began=false;
            if(c.Kind=="Jump")
            {
                // Preserve RC119's same-frame physical opportunity at the original Jump boundary.
                if(!c.SkillTransferTransient&&TryCommitCapabilityJumpAtDispatch(probe,c))began=true;
                else if(IsActiveProbe(probe))began=TryBeginLearnedNativeJump(probe,c,"sequenceNative",out failure);
                else return true; // A final-consumer refusal already advanced this exact attempt.
            }
            else if(c.Kind=="Vault"||c.Kind=="Climb")
            {
                if(edge?.NativeVaultParameters!=null)
                    began=TryBeginInjectedNativeVault(probe,c,edge.NativeVaultParameters,"sequenceNative",out failure);
                else if(EnsureVaultingComponent(probe.Player,out failure) &&
                    FollowerMovementWriterAuthority.TryEnterBoundPhysicalMotion(probe.Bot,
                        FollowerMovementWriterAuthority.WriterDomain.Traversal,probe.MovementWriterGeneration,out var scanScope,out failure))
                {
                    using(scanScope)TriggerNativeVaultScan(probe,0,true);
                    began=IsActiveProbe(probe); // Scanner refusal already went through the shared failure sequence.
                }
            }
            else if(c.Kind=="DropDown")began=TryBeginNativeDropDown(probe,c,"sequenceNative",out failure);
            else return false;
            if(began&&c.Kind=="Jump"&&IsProbeLifecycleCurrent(probe)&&probe.Stage==ProbeStage.NativeEntryAcquisition&&
                !probe.LearningV32NativeEntryAcquired&&!probe.NativeExecutorConsumed)
                UpdateSharedNativeEntryAcquisition(probe);
            if(!began)TryRequestCapabilityFullRouteRetry(probe,"sequenceNativeAdmission:"+failure,true);
            WriteEvent("capabilitySequenceNativeDispatched",probe.Bot,new{missionId=probe.LifecycleMission?.MissionId??0,
                edgeId=probe.LearnedEdgeId,kind=c.Kind,began,failure,
                templateEdgeId=c.SkillTransferTransient?c.SkillTemplateEdgeId:c.EdgeId,
                admissionOrExecutionStarted=began,entryAcquired=probe.LearningV32NativeEntryAcquired,
                nativeExecutorConsumed=probe.NativeExecutorConsumed,nativeStateObserved=probe.NativeStateObserved,
                injectedVaultAttempted=probe.InjectedNativeVaultAttempted,jumpAttempted=probe.JumpAttempted,
                probeStage=probe.Stage.ToString(),nativeSuccessRecorded=false});
            return true;
            }
        }

        private static void ApplyCapabilitySequenceWalking(BotOwner bot,float distance=0f)
        {
            if(NeedsCapabilityRunTakeoff(bot))distance=Mathf.Max(distance,1.4f); // Run Jump keeps walking momentum; precise return staging does not borrow this floor.
            long gen=FollowerMovementWriterAuthority.PeekExactGeneration(bot,out var owner);
            if(gen>0&&(owner==FollowerMovementWriterAuthority.WriterDomain.Follow||owner==FollowerMovementWriterAuthority.WriterDomain.Traversal)&&
                FollowerMovementWriterAuthority.TryEnterBoundPhysicalMotion(bot,owner,gen,out var scope,out _))
                using(scope)ApplyCapabilityReturnWalking(bot,bot.GetPlayer,distance);
        }

        private static bool TickCapabilityUnstartedNative(BotOwner bot,FollowerRouteMission m)
        {
            if(m==null||m.Lifecycle.Retired||m.CurrentStepIndex<0||m.CurrentStepIndex>=m.Steps.Count)return false;
            if(m.FailureSequence!=null||IsCapabilityPortalWaiting(bot)||HasActiveTraversalProbe(bot)||
                !IsCapabilityRouteRetrySafe(bot)||!bot.GetPlayer.MovementContext.IsGrounded)
            {m.CapabilityNativeObservedStep=-1;return false;}
            var step=m.Steps[m.CurrentStepIndex];if(step==null||!step.Lifecycle.DebtOutstanding||
                IsFollowerRouteMissionStepPhysicallyPassed(bot,step,out _,out _))return false;
            if(m.CapabilityNativeObservedStep!=m.CurrentStepIndex&&GetPlanarDistance(bot.Position,step.RawEntry)>7)return false;
            float distance=GetPlanarDistance(bot.Position,step.RawExit);
            if(m.CapabilityNativeObservedStep!=m.CurrentStepIndex)
            {m.CapabilityNativeObservedStep=m.CurrentStepIndex;m.CapabilityNativeObservedAt=m.CapabilityNativeProgressAt=Time.time;m.CapabilityNativeBestDistance=distance;}
            else if(distance<m.CapabilityNativeBestDistance-.04f)
            {m.CapabilityNativeBestDistance=distance;m.CapabilityNativeProgressAt=Time.time;}
            if(!FollowerCapabilityIngressMotionPolicy.NativeExpired(Time.time-m.CapabilityNativeObservedAt,Time.time-m.CapabilityNativeProgressAt,Vector3.Distance(step.RawEntry,step.RawExit)))return false;
            WriteEvent("capabilityNativeFrontierStalled",bot,new{missionId=m.MissionId,stepIndex=m.CurrentStepIndex,step.EdgeId,
                body=Vec(bot.Position),entry=Vec(step.RawEntry),exit=Vec(step.RawExit),distance,
                elapsed=Time.time-m.CapabilityNativeObservedAt,noProgress=Time.time-m.CapabilityNativeProgressAt,nativeSuccessRecorded=false});
            return TryRequestCapabilityFullRouteRetry(bot,m,"nativeFrontierNoMeasuredProgress");
        }
        private static bool RejectCapabilityPostCompletionProjection(BotOwner bot,Vector3 candidate)
        {
            if(bot?.GetPlayer?.MovementContext==null||!IsFinite(candidate)||HasActiveTraversalProbe(bot)||
                !PostTraversalIntegrityWatches.TryGetValue(bot.ProfileId,out var watch)||watch==null||watch.ExpiresAt<Time.time)return false;
            long generation=FollowerMovementWriterAuthority.PeekExactGeneration(bot,out var owner);
            bool exact=watch.MovementGeneration==generation&&generation>0&&watch.CompletionMode==TraversalCompletionMode.Native&&
                (owner==FollowerMovementWriterAuthority.WriterDomain.Follow||owner==FollowerMovementWriterAuthority.WriterDomain.PostTraversal);
            var body=bot.GetPlayer.Position;
            bool reject=FollowerCapabilityIngressMotionPolicy.RejectPostCompletionProjection(IsCapabilityRouteRetrySafe(bot),exact,
                bot.GetPlayer.MovementContext.IsGrounded,Time.time-watch.ArmedAt,GetPlanarDistance(body,candidate),candidate.y-body.y);
            if(reject&&ShouldEmitDiagnostic("capabilityPostCompletionProjectionRejected",bot,.25f))
                WriteEvent("capabilityPostCompletionProjectionRejected",bot,new{watch.EdgeId,watch.EpisodeId,generation,
                    body=Vec(body),candidate=Vec(candidate),age=Time.time-watch.ArmedAt,bodyRelocated=false,nativeSuccessRecorded=false});
            return reject;
        }
        private static bool OwnsCapabilityAdmittedIngress(BotOwner bot)
        {
            if(bot==null||!FollowerRouteMissions.TryGetValue(bot.ProfileId,out var m)||m==null)return false;
            bool current=m.CurrentStepIndex>=0&&m.CurrentStepIndex<m.Steps.Count;
            return FollowerCapabilityIngressMotionPolicy.OwnedIngress(!m.Lifecycle.Retired&&m.LifecycleRaid==raidGeneration&&
                m.LifecycleBotId==bot.ProfileId&&Time.time<=m.ExpiresAt,current,
                current&&m.PortalQueueStep==m.CurrentStepIndex&&m.PortalQueueAdmittedAt>=0&&m.PortalQueueClaimSerial>0,
                current&&CapabilityPortalQueue.Owns(bot.ProfileId,m.MissionId,m.CurrentStepIndex),
                current&&m.Steps[m.CurrentStepIndex]?.Lifecycle.DebtOutstanding==true);
        }
        private static bool ProtectCapabilityLocalBodyProjection(BotOwner bot)
            => bot!=null&&FollowerCapabilityIngressMotionPolicy.ProtectLocalBody(IsCapabilityRouteRetrySafe(bot),
                IsCapabilityPortalWaiting(bot),IsCapabilityReturnInProgress(bot),HasCapabilityFailureStrategy(bot)||OwnsCapabilityAdmittedIngress(bot));

        private static bool TryGetCapabilityLiveResolvedFollow(BotOwner bot,out Player leader,out long generation)
        {
            leader=FollowerEnvironment.LocalPlayer;generation=0;
            if(bot?.GetPlayer?.MovementContext==null||!IsLivePlayer(leader))return false;
            generation=FollowerMovementWriterAuthority.PeekExactGeneration(bot,out var owner);
            return FollowerCapabilityPassagePolicy.LiveFollow(IsCapabilityRouteRetrySafe(bot),bot.GetPlayer.MovementContext.IsGrounded,
                HasActiveTraversalProbe(bot),HasCapabilityUnresolvedRouteWork(bot),HasCapabilityNewRecordedWork(bot),HasCurrentCapabilityNativeWay(bot,out _)||HasCapabilityLivePassageProducer(bot),
                CapabilityGenericResumeFloor(bot),generation,owner==FollowerMovementWriterAuthority.WriterDomain.Follow);
        }
        private static bool TryDispatchCapabilityCurrentAction(BotOwner bot,FollowerRouteMission m)
        {
            if(bot==null||m==null||m.Lifecycle.Retired||m.CurrentStepIndex<0||m.CurrentStepIndex>=m.Steps.Count||
                !IsCapabilityRouteRetrySafe(bot)||IsCapabilityPortalWaiting(bot)||IsCapabilityReturnInProgress(bot))return false;
            var step=m.Steps[m.CurrentStepIndex];if(step==null)return false;
            long observedGeneration=FollowerMovementWriterAuthority.PeekExactGeneration(bot,out var observedOwner);
            bool observedLower=FollowerCapabilityPassagePolicy.ObservedLowerDrop(step.Kind=="DropDown",step.LearningV468ObservedEntryWitness,
                observedGeneration>0&&observedOwner==FollowerMovementWriterAuthority.WriterDomain.Traversal&&
                    step.LearningV468EntryWitnessGeneration==observedGeneration,
                IsFollowerAlreadyBelowDropDownLanding(bot.Position,step.RawEntry,step.RawExit,step.Exit),GetPlanarDistance(bot.Position,step.RawExit));
            double entryDistance=FollowerCapabilityPassagePolicy.ImmediateEntryDistance(observedLower,
                Vector3.Distance(bot.Position,step.RawEntry),GetPlanarDistance(bot.Position,step.RawEntry));
            if(!FollowerCapabilityPassagePolicy.ImmediateDispatchKind(step.Kind)||!FollowerCapabilityPassagePolicy.JumpDispatch(true,step.Lifecycle.DebtOutstanding&&!IsCapabilityDebtResolved(bot,step.EpisodeId,step.EdgeId),
                HasActiveTraversalProbe(bot),m.FailureSequence!=null,entryDistance))return false;
            if(BotTraversalPlugin.PassThroughEnabled?.Value==false&&!CapabilityPortalQueue.Owns(bot.ProfileId,m.MissionId,m.CurrentStepIndex))
            {
                if(TickCapabilityPortalWait(bot)||!CapabilityPortalQueue.Owns(bot.ProfileId,m.MissionId,m.CurrentStepIndex))return false;
            }
            // Run the same bound selector at the native Bot update / predecessor release, not the slower bridge scan.
            var leader=FollowerEnvironment.LocalPlayer;if(!IsLivePlayer(leader))return false;
            bool handled=false;
            // Immediate cadence never expands Vault/Climb admission. Until ready,
            // the original path/physical lane selector continues to own the approach.
            bool ready=step.Kind!="Vault"&&step.Kind!="Climb" ||
                TryGetLearningV424ActuatorExecutorAdmission(bot,step,out bool obstacleReady,out _,out _,out _,out _,out _,
                    out _,out _,out _,out _,out _)&&obstacleReady;
            if(ready&&EnsureFollowerRouteMissionCommitment(bot,m)&&LearnedTraversalCommitments.TryGetValue(bot.ProfileId,out var commitment)&&commitment!=null&&
                commitment.Kind==step.Kind&&commitment.RouteMissionId==m.MissionId&&commitment.RouteMissionStepIndex==m.CurrentStepIndex&&
                IsLearningV376RouteStepEquivalentToEdgeId(step,commitment.EdgeId))
                handled=TryStartEmpiricalLocalEntryBridge(bot,commitment,Vector3.Distance(bot.Position,step.RawEntry),out _,freshTraversalFrontierReady:true);
            if(!HasActiveTraversalProbe(bot)&&m.FailureSequence==null)
                TryGetPlayerRouteMissionFollowTarget(bot,leader.Position,out _,out handled,out _,out _);
            bool started=HasActiveTraversalProbe(bot);
            if(ShouldEmitDiagnostic("capabilityActuatorFrontierImmediateDispatch",bot,.25f))WriteEvent("capabilityActuatorFrontierImmediateDispatch",bot,new{
                missionId=m.MissionId,stepIndex=m.CurrentStepIndex,step.EdgeId,handled,started,body=Vec(bot.Position),entry=Vec(step.RawEntry),nativeSuccessRecorded=false});
            return started||m.FailureSequence!=null;
        }
        private static bool CanApproachCapabilityUnfinishedWalkEntry(BotOwner bot,FollowerRouteMissionStep step)
        {
            if(step?.Kind!="WalkThrough")return true;
            var axis=GetFlatDirection(step.RawExit-step.RawEntry);
            var offset=GetFlatDelta(step.RawEntry,bot.Position);float forward=Vector3.Dot(offset,axis);
            var closest=Vector3.Lerp(step.RawEntry,step.RawExit,Mathf.Clamp01(forward/Mathf.Max(.01f,GetPlanarDistance(step.RawEntry,step.RawExit))));
            return FollowerCapabilityPassagePolicy.EntryStillAhead(forward,(offset-axis*forward).magnitude,bot.Position.y-closest.y);
        }
        private static bool TryDriveCapabilityGroundEntryLead(BotOwner bot,FollowerRouteMission m,Vector3 entry,bool retry)
        {
            if(bot?.GetPlayer?.MovementContext==null||m==null||m.Lifecycle.Retired||m.CurrentStepIndex<0||m.CurrentStepIndex>=m.Steps.Count||
                BotTraversalPlugin.PassThroughEnabled?.Value!=false||!CapabilityPortalQueue.Owns(bot.ProfileId,m.MissionId,m.CurrentStepIndex)||
                !IsCapabilityRouteRetrySafe(bot)||HasActiveTraversalProbe(bot)||IsCapabilityReturnInProgress(bot))return false;
            var step=m.Steps[m.CurrentStepIndex];
            if(!CanApproachCapabilityUnfinishedWalkEntry(bot,step))return false;
            if(step==null||step.Kind!="WalkThrough"||!step.Lifecycle.DebtOutstanding||IsCapabilityDebtResolved(bot,step.EpisodeId,step.EdgeId)||
                !IsFinite(entry)||Vector3.Distance(entry,step.RawEntry)>.35f)return false;
            bool selectedRetry=m.FailureSequence?.Stage==S.RetryAlternate&&m.FailureSequence.Pending&&m.FailureSequence.Selected?.Kind=="WalkThrough";
            if(retry?!selectedRetry:m.FailureSequence!=null)return false;
            var player=bot.GetPlayer;float distance=GetPlanarDistance(player.Position,entry),height=entry.y-player.Position.y;
            if(distance<=.65f||distance>5||Mathf.Abs(height)>.65f||!player.MovementContext.IsGrounded)return false;
            var target=Vector3.Lerp(player.Position,entry,Mathf.Min(1,1f/distance));var direction=GetFlatDirection(target-player.Position);
            if(TryGetCapabilityCompletedPrefixBoundary(bot,m,step,out var boundary,out var axis)&&
                !FollowerCapabilityIngressMotionPolicy.PrefixPath(true,Vector3.Dot(GetFlatDelta(boundary,player.Position),axis),
                    Vector3.Dot(GetFlatDelta(boundary,entry),axis),Vector3.Dot(GetFlatDelta(boundary,target),axis),Vector3.Dot(GetFlatDelta(boundary,target),axis)))return false;
            bool support=true;
            foreach(var point in new[]{Vector3.Lerp(player.Position,target,.5f),target})
                if(!TryGetFirstNonSelfRaycastHit(player,point+Vector3.up*.6f,Vector3.down,1f,LayerMaskClass.HighPolyWithTerrainMask,out var hit)||
                    Mathf.Abs(hit.point.y-point.y)>.35f||hit.normal.y<.65f){support=false;break;}
            var delta=target-player.Position;
            bool blocked=TryGetFirstNonSelfCapsuleCastHit(player,player.Position+Vector3.up*.4f,player.Position+Vector3.up*1.35f,
                .18f,delta.normalized,delta.magnitude,LayerMaskClass.HighPolyWithTerrainMask,out _);
            if(!FollowerCapabilityPassagePolicy.GroundLead(true,true,true,false,distance,height,support,blocked))return false;
            long generation=FollowerMovementWriterAuthority.PeekExactGeneration(bot,out var owner);
            if(generation<=0||(owner!=FollowerMovementWriterAuthority.WriterDomain.Follow&&owner!=FollowerMovementWriterAuthority.WriterDomain.Traversal)||
                !FollowerMovementWriterAuthority.TryEnterBoundPhysicalMotion(bot,owner,generation,out var scope,out _))return false;
            using(scope)
            {
                if(!FollowerRouteMissions.TryGetValue(bot.ProfileId,out var live)||!ReferenceEquals(live,m)||!ReferenceEquals(m.Steps[m.CurrentStepIndex],step)||
                    !CapabilityPortalQueue.Owns(bot.ProfileId,m.MissionId,m.CurrentStepIndex)||!IsCapabilityRouteRetrySafe(bot)||HasActiveTraversalProbe(bot))return false;
                if(!TryRetireNativeMoverPathPreserveLocomotion(bot,"queueGroundEntryManualConsumer",out _))return false;
                ClearCapabilityNativeDestinationData(bot);
                float heading=Vector3.Dot(GetFlatDirection(player.MovementContext.PlayerRealForward),direction);
                bool turnHold=FollowerCapabilityPassagePolicy.EntryTurnHold(heading);
                var receipt=ApplyCollisionActivePhysicalRouteMotion(bot,player,direction,turnHold?Vector3.zero:direction*Mathf.Max(.001f,Time.deltaTime),
                    Mathf.Max(.001f,Time.deltaTime),1f,false,turnHold?0f:(retry?.55f:.85f),false,true,deliveryPurpose:CapabilityMotionPurpose.FollowIngress);
                if(ShouldEmitDiagnostic("capabilityGroundEntryLead",bot,.25f))WriteEvent("capabilityGroundEntryLead",bot,new{
                    missionId=m.MissionId,stepIndex=m.CurrentStepIndex,step.EdgeId,retry,distance,height,heading,turnHold,body=Vec(player.Position),target=Vec(target),
                    receipt=receipt.Kind.ToString(),nativeSuccessRecorded=false,physicalEntryGranted=false});
                return receipt.Accepted;
            }
        }
        private static bool TryValidateCapabilitySemanticWalkingRamp(ProbeRuntime probe,Vector3 target)
        {
            if(probe?.Player==null||!IsProbeLifecycleCurrent(probe)||!IsFinite(target)||!IsCapabilityRouteRetrySafe(probe.Bot))return false;
            var body=probe.Player.Position;float distance=GetPlanarDistance(body,target),height=target.y-body.y;
            if(!FollowerCapabilityPassagePolicy.RampEnvelope(height,distance))return false;
            int count=Math.Max(2,(int)Math.Ceiling(Vector3.Distance(body,target)));if(count>16)return false;
            var previous=body;bool supported=true;
            for(int i=1;i<=count;i++)
            {
                var point=Vector3.Lerp(body,target,(float)i/count);
                bool hit=TryGetFirstNonSelfRaycastHit(probe.Player,point+Vector3.up*.6f,Vector3.down,1f,
                    LayerMaskClass.HighPolyWithTerrainMask,out var floor);
                var delta=point-previous;
                bool blocked=TryGetFirstNonSelfCapsuleCastHit(probe.Player,previous+Vector3.up*.4f,previous+Vector3.up*1.35f,
                    .18f,delta.normalized,delta.magnitude,LayerMaskClass.HighPolyWithTerrainMask,out _);
                if(!FollowerCapabilityPassagePolicy.RampSample(hit,hit?floor.point.y-point.y:float.PositiveInfinity,hit?floor.normal.y:0,blocked))
                {supported=false;break;}
                previous=point;
            }
            if(ShouldEmitDiagnostic("capabilitySemanticRampContinuation",probe.Bot,.25f))WriteEvent("capabilitySemanticRampContinuation",probe.Bot,new{
                missionId=probe.LifecycleMission?.MissionId??0,edgeId=probe.LearnedEdgeId,body=Vec(body),target=Vec(target),height,distance,
                supported,source="finiteDirectGroundAndClearanceSamples",nativeSuccessRecorded=false});
            return supported;
        }
        private static bool TryAdmitCapabilityHeadWalkThroughEntry(BotOwner bot,FollowerRouteMission m)
        {
            if(bot?.GetPlayer?.MovementContext==null||m==null||m.Lifecycle.Retired||m.CurrentStepIndex<0||m.CurrentStepIndex>=m.Steps.Count||
                BotTraversalPlugin.PassThroughEnabled?.Value!=false||!IsCapabilityRouteRetrySafe(bot)||HasActiveTraversalProbe(bot)||
                m.FailureSequence!=null||IsCapabilityReturnInProgress(bot))return false;
            var step=m.Steps[m.CurrentStepIndex];
            if(!CanApproachCapabilityUnfinishedWalkEntry(bot,step))return false;
            if(step==null||step.Kind!="WalkThrough"||!step.Lifecycle.DebtOutstanding||IsCapabilityDebtResolved(bot,step.EpisodeId,step.EdgeId)||
                !LearnedTraversalCommitments.TryGetValue(bot.ProfileId,out var commitment)||commitment==null||commitment.Kind!="WalkThrough"||
                commitment.RouteMissionId!=m.MissionId||commitment.RouteMissionStepIndex!=m.CurrentStepIndex||
                !IsLearningV376RouteStepEquivalentToEdgeId(step,commitment.EdgeId))return false;
            float distance=GetPlanarDistance(bot.Position,step.RawEntry);
            if(!FollowerCapabilityPassagePolicy.HeadEntry(true,CapabilityPortalQueue.Owns(bot.ProfileId,m.MissionId,m.CurrentStepIndex),
                bot.GetPlayer.MovementContext.IsGrounded,distance,step.RawEntry.y-bot.Position.y))return false;
            // Allocate only the existing Aligning probe. Physical lane/crossing proof remains mandatory.
            bool started=TryStartEmpiricalLocalEntryBridge(bot,commitment,distance,out var message,nativeWalkThroughFrontierReady:true);
            if(ShouldEmitDiagnostic("capabilityQueueHeadEntryHandoff",bot,.25f))WriteEvent("capabilityQueueHeadEntryHandoff",bot,new{
                missionId=m.MissionId,stepIndex=m.CurrentStepIndex,step.EdgeId,distance,started,message,
                physicalEntryGranted=false,nativeSuccessRecorded=false});
            return started;
        }
        private static bool TryDriveCapabilityRecordedEntryConnector(BotOwner bot,FollowerRouteMission m)
        {
            if(bot?.GetPlayer?.MovementContext==null||m==null||m.Lifecycle.Retired||m.CurrentStepIndex<0||m.CurrentStepIndex>=m.Steps.Count||
                BotTraversalPlugin.PassThroughEnabled?.Value!=false||!CapabilityPortalQueue.Owns(bot.ProfileId,m.MissionId,m.CurrentStepIndex)||
                !IsCapabilityRouteRetrySafe(bot)||HasActiveTraversalProbe(bot)||m.FailureSequence!=null||IsCapabilityReturnInProgress(bot))return false;
            var step=m.Steps[m.CurrentStepIndex];
            if(!CanApproachCapabilityUnfinishedWalkEntry(bot,step))return false;
            if(step==null||step.Kind!="WalkThrough"||!step.Lifecycle.DebtOutstanding||IsCapabilityDebtResolved(bot,step.EpisodeId,step.EdgeId))return false;
            var player=bot.GetPlayer;float distance=GetPlanarDistance(player.Position,step.RawEntry);
            if(distance<=.65f||distance>5||!player.MovementContext.IsGrounded)return false;
            if(TryDriveCapabilityGroundEntryLead(bot,m,step.RawEntry,false))return true;
            var strict=CalculateStrictPath(player.Position,step.Entry);
            if(FollowerCapabilityPortalPolicy.LocalApproachPath(strict?.StrictComplete==true,strict?.PathLength??float.PositiveInfinity,Vector3.Distance(player.Position,step.Entry)))return false;
            // Only the current raid's ordered, grounded upstream demonstration may fill a NavMesh hole.
            var trail=m.RouteBreadcrumbs.Where(n=>n!=null&&n.Sequence>=step.StartBreadcrumbSequence-32&&n.Sequence<=step.StartBreadcrumbSequence).OrderBy(n=>n.Sequence).ToList();
            int nearest=-1;float join=float.PositiveInfinity;
            for(int i=0;i<trail.Count;i++)if(trail[i].Grounded&&IsFinite(trail[i].Position))
            {float d=Vector3.Distance(player.Position,trail[i].Position);if(d<join){join=d;nearest=i;}}
            if(nearest<0)return false;
            bool localJoin=false;
            Vector3 target=default;bool found=false;
            if(join>.85f)
            {
                var node=trail[nearest];var joinPath=CalculateStrictPath(player.Position,node.Position);
                localJoin=FollowerCapabilityPassagePolicy.LocalJoin(joinPath?.StrictComplete==true,join,joinPath?.PathLength??float.PositiveInfinity,node.Position.y-player.Position.y);
                if(!localJoin||joinPath.Corners==null)return false;
                if(TryGetCapabilityCompletedPrefixBoundary(bot,m,step,out var joinBoundary,out var joinAxis)&&
                    !FollowerCapabilityIngressMotionPolicy.PrefixPath(true,Vector3.Dot(GetFlatDelta(joinBoundary,player.Position),joinAxis),
                        Vector3.Dot(GetFlatDelta(joinBoundary,step.RawEntry),joinAxis),Vector3.Dot(GetFlatDelta(joinBoundary,node.Position),joinAxis),
                        joinPath.Corners.Min(p=>Vector3.Dot(GetFlatDelta(joinBoundary,p),joinAxis))))return false;
                foreach(var corner in joinPath.Corners)if(GetPlanarDistance(player.Position,corner)>.15f){target=corner;found=true;break;}
                if(found&&GetPlanarDistance(player.Position,target)>1.5f)target=Vector3.Lerp(player.Position,target,1.5f/GetPlanarDistance(player.Position,target));
            }
            else for(int i=nearest+1;i<trail.Count;i++)
            {
                if(!trail[i].Grounded||!IsFinite(trail[i].Position))break;
                if(GetPlanarDistance(player.Position,trail[i].Position)>.15f){target=trail[i].Position;found=true;break;}
            }
            if(!found)return false;
            var axis=GetFlatDirection(step.RawExit-step.RawEntry);
            if(!localJoin&&distance<=1.25f&&Vector3.Dot(GetFlatDelta(step.RawEntry,target),axis)>0)target=step.RawEntry;
            var direction=GetFlatDirection(target-player.Position);float segment=GetPlanarDistance(player.Position,target);
            if(!localJoin&&Vector3.Dot(direction,axis)<.30f)return false;
            if(TryGetCapabilityCompletedPrefixBoundary(bot,m,step,out var boundary,out var prefixAxis)&&
                !FollowerCapabilityIngressMotionPolicy.PrefixPath(true,Vector3.Dot(GetFlatDelta(boundary,player.Position),prefixAxis),
                    Vector3.Dot(GetFlatDelta(boundary,step.RawEntry),prefixAxis),Vector3.Dot(GetFlatDelta(boundary,target),prefixAxis),
                    Vector3.Dot(GetFlatDelta(boundary,target),prefixAxis)))return false;
            bool support=true;
            foreach(var sample in new[]{Vector3.Lerp(player.Position,target,.5f),target})
                if(!TryGetFirstNonSelfRaycastHit(player,sample+Vector3.up*.6f,Vector3.down,1f,LayerMaskClass.HighPolyWithTerrainMask,out var hit)||
                    Mathf.Abs(hit.point.y-sample.y)>.35f||hit.normal.y<.65f){support=false;break;}
            bool blocked=TryGetFirstNonSelfCapsuleCastHit(player,player.Position+Vector3.up*.4f,player.Position+Vector3.up*1.35f,
                .18f,direction,segment,LayerMaskClass.HighPolyWithTerrainMask,out _);
            if(!FollowerCapabilityIngressMotionPolicy.Connector(true,true,false,false,true,distance,localJoin?0:join,segment,target.y-player.Position.y,support,blocked,true))return false;
            long generation=FollowerMovementWriterAuthority.PeekExactGeneration(bot,out var owner);
            if(generation<=0||(owner!=FollowerMovementWriterAuthority.WriterDomain.Follow&&owner!=FollowerMovementWriterAuthority.WriterDomain.Traversal)||
                !FollowerMovementWriterAuthority.TryEnterBoundPhysicalMotion(bot,owner,generation,out var scope,out _))return false;
            using(scope)
            {
                if(!FollowerRouteMissions.TryGetValue(bot.ProfileId,out var live)||!ReferenceEquals(live,m)||m.CurrentStepIndex<0||m.CurrentStepIndex>=m.Steps.Count||
                    !ReferenceEquals(m.Steps[m.CurrentStepIndex],step)||!CapabilityPortalQueue.Owns(bot.ProfileId,m.MissionId,m.CurrentStepIndex)||
                    !IsCapabilityRouteRetrySafe(bot)||HasActiveTraversalProbe(bot))return false;
                var receipt=ApplyCollisionActivePhysicalRouteMotion(bot,player,direction,direction*Mathf.Max(.001f,Time.deltaTime),
                    Mathf.Max(.001f,Time.deltaTime),1f,sprint:false,targetMoveSpeed:.8f,preserveSprintMode:false,exactWorldDirectionInput:true);
                if(ShouldEmitDiagnostic("capabilityRecordedEntryConnector",bot,.25f))
                    WriteEvent("capabilityRecordedEntryConnector",bot,new{missionId=m.MissionId,stepIndex=m.CurrentStepIndex,step.EdgeId,
                        body=Vec(player.Position),target=Vec(target),entry=Vec(step.RawEntry),distance,join,localJoin,segment,support,blocked,
                        receipt=receipt.Kind.ToString(),nativeActuatorStarted=false,nativeSuccessRecorded=false});
                return receipt.Accepted;
            }
        }
        private static bool IsCapabilityCurrentPassageInFlight(BotOwner bot,FollowerRouteMission m)
        {
            if(bot==null||m==null||m.Lifecycle.Retired||m.LifecycleRaid!=raidGeneration||m.LifecycleBotId!=bot.ProfileId||
                m.CurrentStepIndex<0||m.CurrentStepIndex>=m.Steps.Count||m.PortalQueueStep!=m.CurrentStepIndex||m.PortalQueueAdmittedAt<0||m.PortalQueueClaimSerial<=0)return false;
            var step=m.Steps[m.CurrentStepIndex];if(step==null||!step.Lifecycle.DebtOutstanding||!IsFinite(step.RawEntry)||!IsFinite(step.RawExit))return false;
            var axis=GetFlatDirection(step.RawExit-step.RawEntry);float span=GetPlanarDistance(step.RawEntry,step.RawExit);
            var offset=GetFlatDelta(step.RawEntry,bot.Position);float forward=Vector3.Dot(offset,axis);
            var closest=Vector3.Lerp(step.RawEntry,step.RawExit,Mathf.Clamp01(forward/Mathf.Max(.01f,span)));
            if(FollowerCapabilityPassagePolicy.DropInFlight(step.Kind=="DropDown",true,forward,span,
                (offset-axis*forward).magnitude,bot.Position.y,step.RawEntry.y,step.RawExit.y))return true;
            return FollowerCapabilityPassagePolicy.PassageInFlight(step.Kind=="WalkThrough"||step.Kind=="PhysicalCorridor",true,
                forward,span,(offset-axis*forward).magnitude,bot.Position.y-closest.y);
        }
        private static Vector3 GetCapabilityQueueAnchor(FollowerRouteMissionStep step,out Vector3 axis)
        {
            var anchor=step.RawEntry;axis=GetFlatDirection(step.RawExit-step.RawEntry);
            var edge=LearnedTraversalEdges.FirstOrDefault(e=>e!=null&&e.Id==step.EdgeId&&e.Kind=="WalkThrough"&&e.PlayerValidated);
            if(edge==null||!IsFinite(edge.RawStart)||!IsFinite(edge.RawEnd))return anchor;
            var canonicalAxis=GetFlatDirection(edge.RawEnd-edge.RawStart);
            if(FollowerCapabilityPassagePolicy.CanonicalQueue(IsLearningV3NativeProvenEdge(edge),step.Kind=="WalkThrough",
                GetPlanarDistance(anchor,edge.RawStart),GetPlanarDistance(step.RawExit,edge.RawEnd),anchor.y-edge.RawStart.y,
                step.RawExit.y-edge.RawEnd.y,Vector3.Dot(axis,canonicalAxis),RouteMissionTraversalAdjacentBridgeMaxDistance))
            {anchor=edge.RawStart;axis=canonicalAxis;}
            return anchor;
        }
        private static bool TickCapabilityPortalWait(BotOwner bot)
        {
            if(bot==null||!IsLiveBot(bot)||!FollowerRouteMissions.TryGetValue(bot.ProfileId,out var m))return false;
            if(BotTraversalPlugin.PassThroughEnabled?.Value!=false)
            {CapabilityPortalQueue.Clear();ReleaseCapabilityPortalWait(m);return false;}
            bool activeCurrent=TryGetActiveProbe(bot,out var queueProbe)&&IsProbeLifecycleCurrent(queueProbe)&&IsCapabilityPrioritySafe(bot);
            if(activeCurrent&&CapabilityPortalQueue.Owns(bot.ProfileId,m.MissionId,m.CurrentStepIndex))
            {ReleaseCapabilityPortalWait(m);return false;}
            if(m.Lifecycle.Retired||m.CurrentStepIndex<0||m.CurrentStepIndex>=m.Steps.Count||
                IsCapabilityRouteRetryReturning(m)||m.FailureSequence?.Stage==S.RecoveryTeleport||(!activeCurrent&&!IsCapabilityRouteRetrySafe(bot)))
            {ReleaseCapabilityPortalWait(m);CapabilityPortalQueue.Cancel(bot.ProfileId,Time.time);return false;}
            var step=m.Steps[m.CurrentStepIndex];if(step==null)return false;
            bool active=HasActiveTraversalProbe(bot);
            if(!active&&m.FailureSequence==null&&IsCapabilityPrioritySafe(bot)&&
                FollowerTraversalRouteCursors.TryGetValue(bot.ProfileId,out var observedCursor)&&
                TryAdvanceLearningV468ObservedDropDownLandingDebt(bot,m,observedCursor))
            {ReleaseCapabilityPortalWait(m);return true;}
            if(IsFollowerRouteMissionStepPhysicallyPassed(bot,step,out _,out _))
            {ReleaseCapabilityPortalWait(m);return false;}
            var entry=step.RawEntry;var axis=GetFlatDirection(step.RawExit-entry);
            float distance=GetPlanarDistance(bot.Position,entry);
            // A refreshed semantic step may replace its geometry at the same index.
            // Its old portal ticket cannot prove admission to the newly selected entrance.
            var queueAnchor=GetCapabilityQueueAnchor(step,out var queueAxis);
            var boundTicket=CapabilityPortalQueue.Inspect(queueAnchor.x,queueAnchor.y,queueAnchor.z,queueAxis.x,queueAxis.z);
            if(m.PortalQueueStep==m.CurrentStepIndex&&m.PortalQueueAdmittedAt>=0&&m.PortalQueueClaimSerial>0&&
                !FollowerCapabilityPassagePolicy.QueueClaimCurrent(m.PortalQueueClaimSerial,boundTicket?.ClaimSerial??0,
                    boundTicket!=null&&boundTicket.Bot==bot.ProfileId&&boundTicket.Mission==m.MissionId&&boundTicket.Step==m.CurrentStepIndex))
            {
                CapabilityPortalQueue.Cancel(bot.ProfileId,Time.time);ReleaseCapabilityPortalWait(m);
                m.PortalQueueAdmittedAt=-1;m.PortalQueueStep=-1;m.PortalQueueClaimSerial=0;
                WriteEvent("capabilityPortalQueueStaleGeometryReleased",bot,new{missionId=m.MissionId,stepIndex=m.CurrentStepIndex,
                    step.EdgeId,entry=Vec(entry),completedPrefixRetained=true,nativeSuccessRecorded=false,bodyRelocated=false});
            }
            bool passageInFlight=IsCapabilityCurrentPassageInFlight(bot,m);
            if(FollowerCapabilityPassagePolicy.LeftAdmittedIngress(m.PortalQueueStep==m.CurrentStepIndex&&m.PortalQueueAdmittedAt>=0&&m.PortalQueueClaimSerial>0,
                active,m.FailureSequence!=null,distance,passageInFlight)&&axis.sqrMagnitude>=.5f)
            {
                WriteEvent("capabilityPortalQueueAdmittedIngressLost",bot,new{missionId=m.MissionId,stepIndex=m.CurrentStepIndex,
                    step.EdgeId,distance,body=Vec(bot.Position),entry=Vec(entry),nativeSuccessRecorded=false});
                TryRequestCapabilityFullRouteRetry(bot,m,"portalQueueHeadLeftUnfinishedIngressWindow");
                return true; // A released head's failed approach is not an unregistered distant observer.
            }
            if((!FollowerPortalAdmission.InWindow(distance,m.PortalQueueWaiting||m.PortalQueueStep==m.CurrentStepIndex&&m.PortalQueueAdmittedAt>=0)&&!passageInFlight)||axis.sqrMagnitude<.5f)
            {ReleaseCapabilityPortalWait(m);CapabilityPortalQueue.Cancel(bot.ProfileId,Time.time);m.PortalQueueAdmittedAt=-1;m.PortalQueueStep=-1;return false;}
            // Enrol eligible near-side followers BEFORE choosing nearest. An unregistered observer
            // may not veto the queue indefinitely, nor start an unbound legacy probe alongside it.
            foreach(var otherBot in FollowerEnvironment.RegisteredFollowers)
            {
                if(otherBot==null||otherBot.ProfileId==bot.ProfileId||!IsLiveBot(otherBot)||
                    FollowerRouteMissions.ContainsKey(otherBot.ProfileId)||HasActiveTraversalProbe(otherBot)||!IsCapabilityRouteRetrySafe(otherBot))continue;
                var offset=GetFlatDelta(entry,otherBot.Position);
                if(offset.magnitude>5||Vector3.Dot(offset,axis)>.14f||Mathf.Abs(otherBot.Position.y-entry.y)>.65f)continue;
                RegisterCapabilityLocalMission(otherBot,CloneCapabilityReplayStep(step),"portalQueueEnrollment");
            }
            bool nearest=true;
            foreach(var pair in FollowerRouteMissions)
            {
                if(pair.Key==bot.ProfileId)continue;var om=pair.Value;
                if(om.Lifecycle.Retired||om.CurrentStepIndex<0||om.CurrentStepIndex>=om.Steps.Count||
                    IsCapabilityRouteRetryReturning(om)||om.FailureSequence?.Stage==S.RecoveryTeleport)continue;
                var os=om.Steps[om.CurrentStepIndex];var ob=BossPlayers.GetFollowerByProfileId(pair.Key)?.GetBot();
                if(os==null||!IsLiveBot(ob)||!IsCapabilityRouteRetrySafe(ob)||IsFollowerRouteMissionStepPhysicallyPassed(ob,os,out _,out _))continue;
                var otherAnchor=GetCapabilityQueueAnchor(os,out var otherAxis);
                if(GetPlanarDistance(otherAnchor,queueAnchor)>1||Mathf.Abs(otherAnchor.y-queueAnchor.y)>.65f||Vector3.Dot(otherAxis,queueAxis)<.85f)continue;
                float od=GetPlanarDistance(ob.Position,queueAnchor),own=GetPlanarDistance(bot.Position,queueAnchor);
                if(FollowerPortalAdmission.InWindow(GetPlanarDistance(ob.Position,os.RawEntry),om.PortalQueueWaiting||om.PortalQueueStep==om.CurrentStepIndex&&om.PortalQueueAdmittedAt>=0)&&FollowerPortalAdmission.Closer(od,pair.Key,own,bot.ProfileId))nearest=false;
            }
            bool Live(string id,int mission,int index)=>FollowerRouteMissions.TryGetValue(id,out var live)&&!live.Lifecycle.Retired&&
                live.MissionId==mission&&live.CurrentStepIndex==index&&IsLiveBot(BossPlayers.GetFollowerByProfileId(id)?.GetBot())&&
                !IsCapabilityRouteRetryReturning(live)&&live.FailureSequence?.Stage!=S.RecoveryTeleport&&
                (HasActiveTraversalProbe(BossPlayers.GetFollowerByProfileId(id)?.GetBot())||
                 IsCapabilityCurrentPassageInFlight(BossPlayers.GetFollowerByProfileId(id)?.GetBot(),live)||
                 FollowerPortalAdmission.InWindow(GetPlanarDistance(BossPlayers.GetFollowerByProfileId(id).GetBot().Position,entry),true));
            bool allowed=CapabilityPortalQueue.Admit(bot.ProfileId,m.MissionId,m.CurrentStepIndex,queueAnchor.x,queueAnchor.y,queueAnchor.z,
                queueAxis.x,queueAxis.z,Time.time,nearest||activeCurrent,Live);
            var ticket=CapabilityPortalQueue.Inspect(queueAnchor.x,queueAnchor.y,queueAnchor.z,queueAxis.x,queueAxis.z);
            if(allowed)
            {
                bool acquired=m.PortalQueueStep!=m.CurrentStepIndex||m.PortalQueueAdmittedAt<0||m.PortalQueueClaimSerial!=ticket.ClaimSerial;
                bool wasWaiting=m.PortalQueueWaiting;
                ReleaseCapabilityPortalWait(m);
                if(wasWaiting)
                {
                    global::BotTraversal.LegacyOriginalPITFollowBridge.CancelDelivery(bot.ProfileId);
                    m.NextMoveAttemptAt=0;
                    if(LearnedTraversalCommitments.TryGetValue(bot.ProfileId,out var queued)&&queued!=null)queued.ExpiresAt=Math.Max(queued.ExpiresAt,Time.time+6);
                    WriteEvent("capabilityPortalQueueApproachReleased",bot,new{missionId=m.MissionId,stepIndex=m.CurrentStepIndex,step.EdgeId,claim=ticket.ClaimSerial,nativeSuccessRecorded=false});
                }
                bool corridor=step.Kind=="PhysicalCorridor"||step.Kind=="WalkThrough";
                // Once inside a passage, distance from the entry increases during SUCCESSFUL motion.
                // Use remaining distance to its exit, with the same existing claim/progress clock.
                float progressDistance=(float)FollowerCapabilityPassagePolicy.QueueDistance(corridor||step.Kind=="DropDown",distance,
                    step.Kind=="DropDown"?Vector3.Distance(bot.GetPlayer.Position,step.RawExit):GetPlanarDistance(bot.Position,step.RawExit));
                if(acquired)
                {
                    m.PortalQueueStep=m.CurrentStepIndex;m.PortalQueueClaimSerial=ticket.ClaimSerial;m.PortalQueueAdmittedAt=m.PortalQueueHeadProgressAt=Time.time;
                    m.PortalQueueHeadBestDistance=progressDistance;
                    WriteEvent("capabilityPortalQueueAdmitted",bot,new{missionId=m.MissionId,stepIndex=m.CurrentStepIndex,edgeId=step.EdgeId,distance,nearest,sameBotContinuation=ticket?.LastAdmissionContinued??false});
                }
                if(progressDistance<m.PortalQueueHeadBestDistance-.04f){m.PortalQueueHeadBestDistance=progressDistance;m.PortalQueueHeadProgressAt=Time.time;}
                var velocity=bot.GetPlayer.Velocity;velocity.y=0;
                if(FollowerCapabilityIngressMotionPolicy.QueueMovingAway(active,m.FailureSequence!=null,passageInFlight,
                    m.PortalQueueHeadBestDistance,progressDistance,velocity.magnitude,
                    Vector3.Dot(GetFlatDirection(velocity),GetFlatDirection(entry-bot.GetPlayer.Position))))
                {
                    WriteEvent("capabilityQueueApproachMovingAway",bot,new{missionId=m.MissionId,stepIndex=m.CurrentStepIndex,
                        body=Vec(bot.GetPlayer.Position),entry=Vec(entry),best=m.PortalQueueHeadBestDistance,progressDistance,
                        velocity=Vec(velocity),nativeSuccessRecorded=false});
                    TryRequestCapabilityFullRouteRetry(bot,m,"portalQueueHeadMeasuredMovingAway");return true;
                }
                if(FollowerPortalAdmission.HeadStalled(Time.time,m.PortalQueueAdmittedAt,m.PortalQueueHeadProgressAt,active,m.FailureSequence!=null,corridor))
                {TryRequestCapabilityFullRouteRetry(bot,m,"portalQueueHeadNoMeasuredProgress");return true;}
                return false;
            }
            // Never stop an already-running native body. This is diagnosed rather than assigning a second owner.
            if(active)
            {
                ReleaseCapabilityPortalWait(m);
                if(ShouldEmitDiagnostic("capabilityPortalQueueActiveConflict",bot,.5f))
                    WriteEvent("capabilityPortalQueueActiveConflict",bot,new{missionId=m.MissionId,head=ticket?.Bot});
                return false;
            }
            m.CapabilityNativeObservedStep=-1;
            if(!m.PortalQueueWaiting){m.PortalQueueWaiting=true;m.PortalQueueWaitStartedAt=Time.time;}
            long gen=FollowerMovementWriterAuthority.PeekExactGeneration(bot,out var owner);
            if(gen<=0)
            {
                if(FollowerMovementWriterAuthority.TryEnterFollowPhysicalMotion(bot,"portalQueueHold",out var freshScope,out _))
                    using(freshScope)CommitCapabilityPortalHold(bot,m);
            }
            else if((owner==FollowerMovementWriterAuthority.WriterDomain.Follow||owner==FollowerMovementWriterAuthority.WriterDomain.Traversal)&&
                FollowerMovementWriterAuthority.TryEnterBoundPhysicalMotion(bot,owner,gen,out var scope,out _))
                using(scope)CommitCapabilityPortalHold(bot,m);
            if(ShouldEmitDiagnostic("capabilityPortalQueueWaiting",bot,.5f))
                WriteEvent("capabilityPortalQueueWaiting",bot,new{missionId=m.MissionId,stepIndex=m.CurrentStepIndex,edgeId=step.EdgeId,
                    distance,nearest,entry=Vec(entry),head=ticket?.Bot,cooldownRemaining=Mathf.Max(0,(float)(ticket?.ReadyAt??0)-Time.time),
                    waitSeconds=Time.time-m.PortalQueueWaitStartedAt,waitingMode="holdCurrentBodyNoAlternatePath",
                    waitReason=ticket?.Bot!=null?"portalOwnedByOtherBot":ticket!=null&&Time.time<ticket.ReadyAt?"oneSecondCompletionCooldown":!nearest?"nearerEligibleBot":"admissionNotGranted",
                    canonicalAnchor=Vec(queueAnchor),claimSerial=ticket?.ClaimSerial??0,nativeSuccessRecorded=false});
            return true;
        }
        private static void CommitCapabilityPortalHold(BotOwner bot,FollowerRouteMission m)
        {
                long gen=FollowerMovementWriterAuthority.PeekExactGeneration(bot,out _);
                bot.Mover.BotMoverSprint("R121-121A00000001",bot,false,false);
                var path=bot.Mover.ActualPathController?.CurPath;
                if(m.PortalQueueHoldGeneration!=gen||!ReferenceEquals(m.PortalQueueHoldPath,path))
                {
                    bot.Mover.BotMoverStop("R121-121A00000002",bot);
                    bot.BotOwnerStopMove("R121-121A00000003",bot);
                    ClearCapabilityNativeDestinationData(bot);
                    global::BotTraversal.LegacyOriginalPITFollowBridge.CancelDelivery(bot.ProfileId);
                    // Queue idling suspends movement, not an already captured upper-side body epoch.
                    var step=m.CurrentStepIndex>=0&&m.CurrentStepIndex<m.Steps.Count?m.Steps[m.CurrentStepIndex]:null;
                    bool retain=FollowerTraversalRouteCursors.TryGetValue(bot.ProfileId,out var cursor)&&
                        FollowerCapabilityPassagePolicy.QueuedDropWitness(step?.Kind=="DropDown",step?.LearningV468ObservedEntryWitness==true,
                            cursor.LearningV375TransactionActive,cursor.LearningV375RouteMissionId==m.MissionId,
                            cursor.LearningV375MovementGeneration==gen&&step?.LearningV468EntryWitnessGeneration==gen);
                    if(!retain)EndLearningV375PersistentRouteTransaction(bot,"portalQueueWaitOwnsIdle");
                    m.PortalQueueHoldGeneration=gen;m.PortalQueueHoldPath=bot.Mover.ActualPathController?.CurPath;
                }
                CommitCapabilityRouteRetryZeroInput(bot,bot.GetPlayer);
        }
    }
}

