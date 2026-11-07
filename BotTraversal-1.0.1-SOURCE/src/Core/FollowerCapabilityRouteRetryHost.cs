using System;
using System.Collections.Generic;
using System.Linq;
using EFT;
using UnityEngine;
using UnityEngine.AI;
using Life=BotTraversal.Core.FollowerTraversalLifecycle;
using Retry=BotTraversal.Core.FollowerCapabilityRouteRetryPolicy;
namespace BotTraversal.Core
{
    internal static partial class FollowerTraversalTestRuntime
    {
        // Data attached to the existing Mission, never another controller or actor registry.
        private sealed class CapabilityRouteRetryPlan
        {
            internal readonly List<FollowerRouteMissionStep> Steps;
            internal readonly List<PlayerBreadcrumbNode> Breadcrumbs;
            internal readonly Vector3 Entry,Axis,Stage;
            internal int Restarts,ProgressFloor;
            internal object ReturnPath,HoldPath;
            internal long ReturnGeneration,HoldGeneration;
            internal float ReturnBudget;
            internal List<Vector3> ReturnCorridor;
            internal int ReturnCorridorIndex;
            internal bool ReturnCorridorMoving, PrecisionWalking, PrecisionLocalAttempted;
            internal CapabilityRouteRetryPlan(List<FollowerRouteMissionStep> steps,List<PlayerBreadcrumbNode> breadcrumbs)
            {
                Steps=steps;Breadcrumbs=breadcrumbs;Entry=steps[0].RawEntry;Axis=GetFlatDirection(steps[0].RawExit-Entry);
                if(steps[0].Kind=="WalkThrough")
                {
                    var first=breadcrumbs.Where(n=>n!=null&&n.Sequence>=steps[0].StartBreadcrumbSequence&&n.Sequence<=steps[0].EndBreadcrumbSequence&&
                        GetPlanarDistance(n.Position,Entry)>=.30f).OrderBy(n=>n.Sequence).FirstOrDefault();
                    if(first!=null&&Vector3.Dot(GetFlatDirection(first.Position-Entry),Axis)>.30f)Axis=GetFlatDirection(first.Position-Entry);
                }
                Stage=Entry-Axis*1.20f;
                // Only recorded upstream support may change the expected staging height.
                var upstream=breadcrumbs.Where(n=>n!=null&&n.Grounded&&IsFinite(n.Position)&&n.Sequence<=steps[0].StartBreadcrumbSequence&&GetPlanarDistance(n.Position,Stage)<=.40f)
                    .OrderBy(n=>GetPlanarDistance(n.Position,Stage)).FirstOrDefault();
                if(upstream!=null)Stage=new Vector3(Stage.x,upstream.Position.y,Stage.z);
                ProgressFloor=Math.Max(0,steps[0].StartBreadcrumbSequence-1);
            }
        }
        private static bool IsCapabilityRouteRetryReturning(FollowerRouteMission m)=>m?.RouteRetry!=null&&
            (m.Lifecycle.Status==Life.Phase.Recovering||m.Lifecycle.Status==Life.Phase.Blocked);
        private static bool IsCapabilityFullRouteReplay(FollowerRouteMission m)=>m?.RouteRetry!=null&&
            m.RouteRetry.Restarts>0&&!m.Lifecycle.Retired&&!IsCapabilityRouteRetryReturning(m);
        private static bool IsCapabilityRouteRetrySafe(BotOwner bot)
        {
            var player=bot?.GetPlayer;var context=player?.MovementContext;var f=bot==null?null:BossPlayers.GetFollowerByProfileId(bot.ProfileId);
            return IsLiveBot(bot)&&context!=null&&f!=null&&
                !(f.TryPeekActiveCommand(out var command,out _,out _)&&command!=FollowerCommandType.None)&&
                context.OverridenState==null&&context.OverridenControlsState==null&&!context.IgnoreDeltaMovement&&
                !IsTeleportTransactionActive(bot)&&!FollowerMedical.IsUsingMedical(bot)&&
                !FollowerMedical.IsHandsInteractionActiveForAuthority(player)&&
                !FollowerEnvironment.TryGetImmediateSurvivalThreat(bot,out _)&&!FollowerStateAuthorityCoordinator.HasCombatMovementClaim(bot,out _);
        }
        private static FollowerRouteMissionStep CloneCapabilityReplayStep(FollowerRouteMissionStep x)=>new FollowerRouteMissionStep {
            EpisodeId=x.EpisodeId,EdgeId=x.EdgeId,Kind=x.Kind,StartBreadcrumbSequence=x.StartBreadcrumbSequence,
            EndBreadcrumbSequence=x.EndBreadcrumbSequence,Entry=x.Entry,Exit=x.Exit,RawEntry=x.RawEntry,RawExit=x.RawExit,
            ChainId=x.ChainId,ChainLocalExecutable=x.ChainLocalExecutable,CurrentRaidRouteAuthority=x.CurrentRaidRouteAuthority,
            LearningV382TransientNativeSkillInstance=x.LearningV382TransientNativeSkillInstance,
            LearningV382SkillTemplateEdgeId=x.LearningV382SkillTemplateEdgeId,LearningV382SkillTransferSource=x.LearningV382SkillTransferSource,
            LearningV383PortalPhysicalSourceEdgeId=x.LearningV383PortalPhysicalSourceEdgeId,
            LearningV383PortalPhysicalEntry=x.LearningV383PortalPhysicalEntry,LearningV383PortalPhysicalExit=x.LearningV383PortalPhysicalExit,
            LearningV383PortalPhysicalRawEntry=x.LearningV383PortalPhysicalRawEntry,LearningV383PortalPhysicalRawExit=x.LearningV383PortalPhysicalRawExit,
            LearningV383PhysicalContinuationOnly=x.LearningV383PhysicalContinuationOnly,
            LearningV386SemanticSourceEdgeId=x.LearningV386SemanticSourceEdgeId,LearningV386SemanticSourceKind=x.LearningV386SemanticSourceKind,
            LearningV386SiblingActuatorStrategy=x.LearningV386SiblingActuatorStrategy,
            LearningV442HasCurrentEpisodeJumpTakeoffFrontier=x.LearningV442HasCurrentEpisodeJumpTakeoffFrontier,
            LearningV442CurrentEpisodeJumpTakeoffFrontier=x.LearningV442CurrentEpisodeJumpTakeoffFrontier,CreatedAt=Time.time };
        private static void CaptureCapabilityRouteRetryPlan(FollowerRouteMission mission)
        {
            if(mission?.Steps==null||mission.Lifecycle.Retired||IsCapabilityFullRouteReplay(mission)||IsCapabilityRouteRetryReturning(mission))return;
            var actual=mission.Steps.Skip(mission.CurrentStepIndex).Where(x=>x!=null&&!string.Equals(x.Kind,"PhysicalCorridor",StringComparison.OrdinalIgnoreCase)).ToList();
            if(actual.Count==0)return;
            if(mission.RouteRetry==null)
            {
                var first=actual[0];if(!IsFinite(first.RawEntry)||!IsFinite(first.RawExit)||GetPlanarDistance(first.RawEntry,first.RawExit)<.20f)return;
                mission.RouteRetry=new CapabilityRouteRetryPlan(mission.Steps.SkipWhile(x=>!ReferenceEquals(x,first)).Where(x=>x!=null).Select(CloneCapabilityReplayStep).ToList(),
                    mission.RouteBreadcrumbs.Concat(BreadcrumbRoute.Where(n=>n!=null&&n.Grounded&&
                        n.Sequence>=first.StartBreadcrumbSequence-32&&n.Sequence<=first.StartBreadcrumbSequence))
                        .GroupBy(n=>n.Sequence).Select(g=>CloneRouteMissionBreadcrumb(g.First())).OrderBy(n=>n.Sequence).ToList());
            }
            else
            {
                // Preserve the first genuinely selected entry, append late recorder successors only.
                foreach(var step in mission.Steps.Where(x=>x!=null))
                    if(step.StartBreadcrumbSequence>=mission.RouteRetry.Steps[0].StartBreadcrumbSequence&&
                       !mission.RouteRetry.Steps.Any(x=>x.EpisodeId==step.EpisodeId&&x.EdgeId==step.EdgeId))
                        mission.RouteRetry.Steps.Add(CloneCapabilityReplayStep(step));
                foreach(var node in mission.RouteBreadcrumbs)
                    if(node!=null&&!mission.RouteRetry.Breadcrumbs.Any(x=>x.Sequence==node.Sequence))mission.RouteRetry.Breadcrumbs.Add(CloneRouteMissionBreadcrumb(node));
                mission.RouteRetry.Breadcrumbs.Sort((a,b)=>a.Sequence.CompareTo(b.Sequence));
            }
        }
        private static bool TryRequestCapabilityFullRouteRetry(ProbeRuntime probe,string reason,bool executionFailed)
        {
            if(probe?.Bot==null||probe.LifecycleMission==null||string.IsNullOrEmpty(reason)||
                !executionFailed||!IsProbeLifecycleCurrent(probe)||probe.Lifecycle?.CompletionPublished==true)return false;
            string r=reason.ToLowerInvariant();
            if(FollowerCapabilityFailureSequence.IsCancellation(r))return false;
            if(!Retry.FailureAdmission(executionFailed,true,false,IsCapabilityRouteRetrySafe(probe.Bot)))
                return DeferCapabilitySequenceFailure(probe.Bot,probe.LifecycleMission,probe,reason);
            return TryRequestCapabilityFullRouteRetry(probe.Bot,probe.LifecycleMission,reason);
        }
        private static bool TryRequestCapabilityFullRouteRetry(BotOwner bot,FollowerRouteMission mission,string reason)
        {
            if(bot==null||!IsLiveBot(bot)||!BossPlayers.IsFollower(bot)||mission==null||mission.Lifecycle.Retired||mission.LifecycleRaid!=raidGeneration||
                mission.LifecycleBotId!=bot.ProfileId||!FollowerRouteMissions.TryGetValue(bot.ProfileId,out var live)||!ReferenceEquals(live,mission)||
                mission.CurrentStepIndex<0||mission.CurrentStepIndex>=mission.Steps.Count)return false;
            if(!IsCapabilityRouteRetrySafe(bot))return DeferCapabilitySequenceFailure(bot,mission,null,reason);
            if(IsCapabilityRouteRetryReturning(mission))return true;
            if (TryAdvanceCapabilityFailureSequence(bot,mission,reason)) return true;
            CaptureCapabilityRouteRetryPlan(mission);if(mission.RouteRetry==null)return false;
            mission.Lifecycle.RequestFullRetry();
            RetireCapabilityRescueCommitmentForRetry(bot,mission,reason);
            mission.LastProgressAt=Time.time;mission.ApproachDeferredLastEntryDistance=float.MaxValue;
            mission.RouteRetry.ReturnBudget=Retry.ReturnBudget(GetPlanarDistance(bot.Position,mission.RouteRetry.Stage));
            mission.RouteRetry.ReturnPath=null;mission.RouteRetry.ReturnGeneration=0;mission.RouteRetry.HoldGeneration=0;
            mission.RouteRetry.ReturnCorridor=null;mission.RouteRetry.ReturnCorridorIndex=0;mission.RouteRetry.ReturnCorridorMoving=false;mission.RouteRetry.PrecisionWalking=false;mission.RouteRetry.PrecisionLocalAttempted=false;
            CommitCapabilityMoveRetry(mission,FollowerCapabilityRetryPolicy.Change.Reset,0f);
            ResetLearningV431EquivalentBarrierSelfRescueState(mission);
            WriteEvent("capabilityFullRouteRetryRequested",bot,new {missionId=mission.MissionId,failedStep=mission.CurrentStepIndex,
                failedEdge=mission.Steps[mission.CurrentStepIndex].EdgeId,rootEdge=mission.RouteRetry.Steps[0].EdgeId,
                entry=Vec(mission.RouteRetry.Entry),stage=Vec(mission.RouteRetry.Stage),reason,restarts=mission.RouteRetry.Restarts,
                oldSuccessHistoryPreserved=true,nativeSuccessRecorded=false});return true;
        }

        private static void HoldCapabilityRouteRetry(BotOwner bot)
        {
            // Called only in an issued current Follow scope. Hard-stop once per held epoch/resource.
            if(bot==null||!FollowerRouteMissions.TryGetValue(bot.ProfileId,out var mission)||
                !IsCapabilityRouteRetryReturning(mission)||mission.Lifecycle.Retired)return;
            var plan=mission.RouteRetry;
            long generation=FollowerMovementWriterAuthority.PeekExactGeneration(bot,out _);
            if(Retry.HoldNeedsStop(plan.HoldGeneration==generation,ReferenceEquals(plan.HoldPath,bot.Mover.ActualPathController?.CurPath)))
            {
                bot.Mover.BotMoverSprint("R112-112A00000001",bot,false,false);
                bot.Mover.BotMoverStop("R112-112A00000002",bot);
                bot.BotOwnerStopMove("R112-112A00000003",bot);
            }
            plan.ReturnPath=null;plan.ReturnGeneration=0;plan.HoldGeneration=generation;plan.HoldPath=bot.Mover.ActualPathController?.CurPath;
            CommitCapabilityRouteRetryZeroInput(bot,bot.GetPlayer);
        }
        private static bool CommitCapabilityRouteRetryZeroInput(BotOwner bot,Player player)
        {
            player.SetPlayerInputDirection("R113-113A00000001",bot,Vector2.zero);
            player.MovementContext.SetMovementContextMovementDirection("R113-113A00000002",bot,Vector2.zero);
            return CommitCapabilityNativePlayerInput(bot,player,Vector2.zero);
        }
        private static bool TryMaintainCapabilityRouteRetryHold(BotOwner bot,Player player)
        {
            if(bot==null||player?.MovementContext==null||!ReferenceEquals(bot.GetPlayer,player)||
                !FollowerRouteMissions.TryGetValue(bot.ProfileId,out var mission)||!IsCapabilityRouteRetryReturning(mission)||
                !IsCapabilityRouteRetrySafe(bot)||HasActiveTraversalProbe(bot)||!player.MovementContext.IsGrounded||
                (player.CurrentStateName!=EPlayerState.Run&&player.CurrentStateName!=EPlayerState.Sprint&&player.CurrentStateName!=EPlayerState.Idle))return false;
            long generation=FollowerMovementWriterAuthority.PeekExactGeneration(bot,out var owner);
            if(generation<=0||owner!=FollowerMovementWriterAuthority.WriterDomain.Follow||
                (mission.RouteRetry.HoldGeneration!=generation&&!mission.RouteRetry.PrecisionWalking)||
                !FollowerMovementWriterAuthority.TryEnterBoundPhysicalMotion(bot,owner,generation,out var scope,out _))return false;
            using(scope)
            {
                if(!FollowerRouteMissions.TryGetValue(bot.ProfileId,out var live)||!ReferenceEquals(live,mission)||
                    !IsCapabilityRouteRetryReturning(mission)||!IsCapabilityRouteRetrySafe(bot))return false;
                if(mission.RouteRetry.PrecisionWalking)ApplyCapabilityReturnWalking(bot,player,GetPlanarDistance(player.Position,mission.RouteRetry.Stage));
                return mission.RouteRetry.HoldGeneration==generation&&CommitCapabilityRouteRetryZeroInput(bot,player);
            }
        }
        private static void ApplyCapabilityReturnWalking(BotOwner bot,Player player,float distance)
        {
            bool correction=IsCapabilityRetryWalking(bot);
            bool sprint=!correction&&TryGetCapabilityRouteSprint(bot,Vector3.zero,out bool desired)&&desired;
            bot.Mover.BotMoverSprint("R116-116A00000001",bot,sprint,false);
            if(player.MovementContext.IsSprintEnabled&&!sprint)player.PlayerEnableSprint("R116-116A00000002",bot,false);
            bot.BotOwnerSetTargetMoveSpeed("R116-116A00000003",bot,correction?CapabilityIngressSpeed(bot,1f,distance):1f);
        }
        private static bool TryObserveCapabilityReturnRemaining(BotOwner bot,CapabilityRouteRetryPlan plan,out float remaining)
        {
            remaining=float.PositiveInfinity;
            try
            {
            var controller=bot?.Mover?.ActualPathController;var path=controller?.CurPath;
            long generation=FollowerMovementWriterAuthority.PeekExactGeneration(bot,out var owner);
            if(plan?.ReturnPath==null||!ReferenceEquals(path,plan.ReturnPath)||generation!=plan.ReturnGeneration||
                owner!=FollowerMovementWriterAuthority.WriterDomain.Follow||!bot.Mover.HasPathAndNoComplete)return false;
            Vector3? target=path.TargetPoint?.Position;
            if(!target.HasValue||!IsFinite(target.Value)||GetPlanarDistance(target.Value,plan.Stage)>.20f||Mathf.Abs(target.Value.y-plan.Stage.y)>.30f)return false;
            if(path.CurIndex<0||path.CurIndex>=path.Length||path.Length-path.CurIndex>512)return false;
            var points=controller.GetWayPoints(512);
            if(points==null||points.Length==0||!IsFinite(points[points.Length-1])||GetPlanarDistance(points[points.Length-1],target.Value)>.20f)return false;
            float distance=0f;Vector3 previous=bot.GetPlayer.Position;
            foreach(var point in points){if(!IsFinite(point))return false;distance+=Vector3.Distance(previous,point);previous=point;}
            if(!IsFinite(distance)||distance<0f)return false;
            if(!ReferenceEquals(controller.CurPath,path)||FollowerMovementWriterAuthority.PeekExactGeneration(bot,out var liveOwner)!=generation||
                liveOwner!=FollowerMovementWriterAuthority.WriterDomain.Follow)return false;
            remaining=distance;return true;
            }
            catch(Exception) { return false; } // Native resource replaced or unavailable: no stale receipt.
        }

        private static bool TickCapabilityFullRouteRetry(BotOwner bot)
        {
            if(bot==null||!FollowerRouteMissions.TryGetValue(bot.ProfileId,out var mission)||mission==null||mission.Lifecycle.Retired)return false;
            if(!IsLiveBot(bot)||!BossPlayers.IsFollower(bot))
            {mission.Lifecycle.Retire();RemoveLifecycleMission(bot.ProfileId,"lifecycleUnavailable",mission);return false;}
            TryRetireCapabilityCoveredDropOverlap(bot,mission);
            if(mission.CurrentStepIndex>=mission.Steps.Count)return false;
            // One update entry arbitrates queue, staged failure and the existing return driver.
            if(TickCapabilityPortalWait(bot))return true;
            if(TryDispatchCapabilityCurrentAction(bot,mission))return true;
            if(TickCapabilityUnstartedNative(bot,mission)||TickCapabilityFailureSequence(bot))return true;
            if(TryAdmitCapabilityHeadWalkThroughEntry(bot,mission))return true;
            if(TryDriveCapabilityRecordedEntryConnector(bot,mission))return true;
            if(!IsCapabilityRouteRetryReturning(mission))
            {
                if(IsCapabilityFullRouteReplay(mission)&&Time.time>mission.ExpiresAt)
                    TryRequestCapabilityFullRouteRetry(bot,mission,"fullReplayExpired");

            }
            if(!IsCapabilityRouteRetryReturning(mission))return false;
            if(!IsCapabilityRouteRetrySafe(bot))return false; // Existing high-priority/manual producer retains movement.
            if(TryGetActiveProbe(bot,out var active))
            {
                if(!active.Player.MovementContext.IsGrounded)return true; // A terminal failed grounded native state must also be released.
                ForceReleaseTraversalProbe(active,"fullRouteRetryRetiresFailedAttempt");return true;
            }
            var plan=mission.RouteRetry;var player=bot.GetPlayer;
            if(!player.MovementContext.IsGrounded)return true;
            EndLearningV375PersistentRouteTransaction(bot,"fullRouteRetryReturn");
            long generation=FollowerMovementWriterAuthority.PeekExactGeneration(bot,out var owner);
            if(owner==FollowerMovementWriterAuthority.WriterDomain.Traversal)
                FollowerMovementWriterAuthority.TransitionTraversalToFollowIfIdleExact(bot,generation,"fullRouteRetryReturn");
            else if(owner==FollowerMovementWriterAuthority.WriterDomain.PostTraversal)
                FollowerMovementWriterAuthority.TransitionPostTraversalToFollowExact(bot,generation,"fullRouteRetryReturn");
            if(!FollowerMovementWriterAuthority.TryEnterFollowPhysicalMotion(bot,"fullRouteRetryReturn",out var scope,out _))return true;
            using(scope)
            {
                if(!IsCapabilityRouteRetryReturning(mission)||!IsCapabilityRouteRetrySafe(bot)||
                    !FollowerRouteMissions.TryGetValue(bot.ProfileId,out var current)||!ReferenceEquals(current,mission))return true;
                var delta=player.Position-plan.Entry;delta.y=0;float forward=Vector3.Dot(delta,plan.Axis),side=(delta-plan.Axis*forward).magnitude;
                float vertical=Mathf.Abs(player.Position.y-plan.Stage.y);var v=player.Velocity;v.y=0;
                float facing=Vector3.Dot(GetFlatDirection(player.MovementContext.PlayerRealForward),plan.Axis);
                if(Retry.StagePosition(forward,side,vertical,true))
                {
                    HoldCapabilityRouteRetry(bot);bot.Steering.BotSteeringLookToDirection("R112-112A00000004",bot,plan.Axis);
                    if(Retry.StageReached(forward,side,vertical,v.magnitude,facing,true))
                    {
                        if(plan.Restarts>=1)
                        {
                            if(ShouldEmitDiagnostic("fullRouteRetryExhausted:"+mission.MissionId,bot,2f))
                                WriteEvent("capabilityFullRouteRetryExhausted",bot,new{missionId=mission.MissionId,restarts=plan.Restarts,
                                    body=Vec(player.Position),nativeSuccessRecorded=false,manualCancelAvailable=true});
                            return true;
                        }
                        var fresh=new FollowerRouteMission { MissionId=++followerRouteMissionSequence,LifecycleBotId=bot.ProfileId,LifecycleRaid=raidGeneration,
                            Source="fullRouteReplay", FailureSequence=mission.FailureSequence, CreatedAt=Time.time,ExpiresAt=Time.time+RouteMissionLifetimeSeconds,LastProgressAt=Time.time,
                            LastProgressPosition=player.Position,StartBreadcrumbSequence=plan.Steps[0].StartBreadcrumbSequence,
                            TargetBreadcrumbSequence=mission.TargetBreadcrumbSequence,ResumeFloorBreadcrumbSequence=plan.Steps[0].StartBreadcrumbSequence-1,
                            LastPlayerEpisodeId=mission.LastPlayerEpisodeId,ApproachBreadcrumbSequence=plan.Steps[0].StartBreadcrumbSequence,
                            PhysicalApproachBreadcrumbSequence=plan.Steps[0].StartBreadcrumbSequence,RouteRetry=plan,
                            Steps=plan.Steps.Select(CloneCapabilityReplayStep).ToList(),RouteBreadcrumbs=plan.Breadcrumbs.Select(CloneRouteMissionBreadcrumb).ToList()};
                        PrepareCapabilityAlternateReplay(fresh);
                        plan.Restarts++;plan.ProgressFloor=Math.Max(0,plan.Steps[0].StartBreadcrumbSequence-1);
                        mission.Lifecycle.Retire();FollowerRouteMissions[bot.ProfileId]=fresh;
                        FollowerTraversalRouteCursors[bot.ProfileId]=new FollowerTraversalRouteCursor {ConsumedBreadcrumbSequence=plan.ProgressFloor,
                            LearningV375RouteMissionId=fresh.MissionId};
                        LearnedTraversalCommitments.Remove(bot.ProfileId);FollowRejectLeases.Remove(bot.ProfileId);
                        WriteEvent("capabilityFullRouteReplayStarted",bot,new {previousMissionId=mission.MissionId,missionId=fresh.MissionId,
                            rootEdge=plan.Steps[0].EdgeId,stepCount=fresh.Steps.Count,restarts=plan.Restarts,body=Vec(player.Position),
                            behindEntryMeters=-forward,side,vertical,facing,nativeSuccessRecorded=false});
                    }
                }
                else
                {
                    // This is the existing failure-owned Follow scope, never a normal entry writer.
                    // Near the root staging point use measured walking input so Sprint cannot orbit it.
                    float stageDistance=GetPlanarDistance(player.Position,plan.Stage);
                    plan.PrecisionWalking=true;
                    if(plan.PrecisionWalking)ApplyCapabilityReturnWalking(bot,player,stageDistance);
                    if(!plan.PrecisionLocalAttempted&&plan.ReturnCorridor==null&&stageDistance<=2.2f&&vertical<=.30f&&
                        !NavMesh.Raycast(player.Position,plan.Stage,out _,NavMesh.AllAreas)&&
                        CalculateStrictPath(player.Position,plan.Stage)?.StrictComplete==true)
                    {
                        HoldCapabilityRouteRetry(bot);plan.HoldGeneration=0;plan.PrecisionLocalAttempted=true;
                        plan.ReturnCorridor=new List<Vector3>{plan.Stage};plan.ReturnCorridorIndex=0;plan.ReturnCorridorMoving=false;
                        mission.LastProgressAt=Time.time;mission.LastProgressPosition=player.Position;
                        mission.ApproachDeferredLastEntryDistance=stageDistance;
                    }
                    if(TryDriveCapabilityReturnCorridor(bot,mission))return true;
                    float remaining;bool currentPath=TryObserveCapabilityReturnRemaining(bot,plan,out remaining);
                    if(Retry.ReturnDiverging(currentPath,mission.ApproachDeferredLastEntryDistance,remaining,
                        GetPlanarDistance(player.Position,mission.LastProgressPosition)))
                    {
                        HoldCapabilityRouteRetry(bot);
                        WriteEvent("capabilityReturnPathDiverged",bot,new{missionId=mission.MissionId,
                            body=Vec(player.Position),target=Vec(plan.Stage),bestRemaining=mission.ApproachDeferredLastEntryDistance,
                            remaining,noProgressAge=Time.time-mission.LastProgressAt,nativeSuccessRecorded=false});
                        return true; // Rebuild under the existing retry cadence; allocation is not measured progress.
                    }
                    if(currentPath&&Retry.PathProgress(mission.ApproachDeferredLastEntryDistance,remaining,
                        GetPlanarDistance(player.Position,mission.LastProgressPosition)))
                    {mission.ApproachDeferredLastEntryDistance=remaining;mission.LastProgressAt=Time.time;mission.LastProgressPosition=player.Position;}
                    var decision=Retry.ReturnDecision(currentPath,Time.time-mission.LastProgressAt,Time.time>=mission.NextMoveAttemptAt);
                    if(decision==Retry.ReturnAction.KeepPath)
                    {
                        if(ShouldEmitDiagnostic("fullRouteReturnProgress:"+mission.MissionId,bot,.50f))
                            WriteEvent("capabilityFullRouteReturnProgress",bot,new{missionId=mission.MissionId,body=Vec(player.Position),
                                target=Vec(plan.Stage),remaining,directDistance=GetPlanarDistance(player.Position,plan.Stage),
                                pathIndex=bot.Mover.ActualPathController.CurPath.CurIndex,writer=plan.ReturnGeneration,
                                noProgressAge=Time.time-mission.LastProgressAt,state=player.CurrentStateName.ToString(),nativeSuccessRecorded=false});
                        return true;
                    }
                    if(decision==Retry.ReturnAction.Wait)return true;
                    HoldCapabilityRouteRetry(bot);
                    CommitCapabilityMoveRetry(mission,FollowerCapabilityRetryPolicy.Change.AttemptOrExplicitHold,Time.time+RouteMissionMoveRetrySeconds);
                    bool sampled=NavMesh.SamplePosition(plan.Stage,out var hit,.30f,NavMesh.AllAreas);
                    var sampledOffset=sampled?hit.position-plan.Entry:Vector3.zero;sampledOffset.y=0;
                    float behind=sampled?-Vector3.Dot(sampledOffset,plan.Axis):0;
                    var path=sampled?CalculateStrictPath(player.Position,hit.position):null;
                    if(sampled&&behind>=1f&&Mathf.Abs(hit.position.y-plan.Stage.y)<=.30f&&GetPlanarDistance(hit.position,plan.Stage)<=.18f&&
                        path?.StrictComplete==true&&Retry.PathWithinBudget(path.PathLength,plan.ReturnBudget))
                    {
                        if(TryArmCapabilityStrictReturnCorridor(bot,mission,path.Corners,path.PathLength))return true;
                        TryScrubLearningV364NativeMoverEpochAnchor(bot,player.Position,"fullRouteRetry",out _);
                        ArmLearningV364PhysicalNavigationEpochHandoff(bot,0,0,0,Vector3.zero,Vector3.zero,player.Position,"fullRouteRetry");
                        bot.Mover.SetBotMoverPause("R112-112A00000005",bot,false);
                        bot.BotOwnerSetTargetMoveSpeed("R112-112A00000006",bot,IsCapabilityRetryWalking(bot)?Retry.ReturnSpeed(GetPlanarDistance(player.Position,plan.Stage)):1f);
                        var status=bot.BotOwnerGoToPoint("R112-112A00000007",bot,hit.position,true,.05f,false,false);
                        bool epochReady=status==NavMeshPathStatus.PathComplete&&TryProveLearningV364PhysicalNavigationEpochFromAcceptedPath(bot,"fullRouteRetry",out _);
                        if(epochReady)
                        {
                            plan.ReturnPath=bot.Mover.ActualPathController?.CurPath;
                            plan.ReturnGeneration=FollowerMovementWriterAuthority.PeekExactGeneration(bot,out _);plan.HoldGeneration=0;
                            if(TryObserveCapabilityReturnRemaining(bot,plan,out remaining))
                            {mission.ApproachDeferredLastEntryDistance=remaining;mission.LastProgressPosition=player.Position;}
                            else {epochReady=false;HoldCapabilityRouteRetry(bot);}
                        }
                        else HoldCapabilityRouteRetry(bot);
                        WriteEvent("capabilityFullRouteReturnPath",bot,new {missionId=mission.MissionId,rootEdge=plan.Steps[0].EdgeId,
                            body=Vec(player.Position),target=Vec(hit.position),behindEntryMeters=behind,status=status.ToString(),epochReady,
                            pathLength=path.PathLength,fixedBudget=plan.ReturnBudget,writer=plan.ReturnGeneration,restarts=plan.Restarts,nativeSuccessRecorded=false});
                    }
                    else if(ShouldEmitDiagnostic("fullRouteRetryBlocked:"+mission.MissionId,bot,1f))
                    {
                        if(TryBuildCapabilityReturnCorridor(bot,mission))return true;
                        WriteEvent("capabilityFullRouteReturnBlocked",bot,new {missionId=mission.MissionId,rootEdge=plan.Steps[0].EdgeId,
                            body=Vec(player.Position),target=Vec(plan.Stage),sampled,fixedBudget=plan.ReturnBudget,pathLength=path?.PathLength,
                            speed=player.Velocity.magnitude,input=Vec2(player.InputDirection),reason="firstEntryStagingNotStrictReachableWithinFrozenBudget",
                            leaderDetourAllowed=false,nativeSuccessRecorded=false});
                    }
                }
                return true;
            }
        }

        private static bool TryArmCapabilityStrictReturnCorridor(BotOwner bot,FollowerRouteMission mission,Vector3[] corners,float length)
        {
            var plan=mission.RouteRetry;var player=bot.GetPlayer;
            if(plan==null||corners==null||corners.Length<2||!Retry.PathWithinBudget(length,plan.ReturnBudget)||
                Vector3.Distance(corners[0],player.Position)>.45f||Vector3.Distance(corners[corners.Length-1],plan.Stage)>.35f)return false;
            var points=new List<Vector3>();var previous=player.Position;
            foreach(var corner in corners.Skip(1))
            {
                if(!IsFinite(corner))return false;
                int count=Math.Max(1,(int)Math.Ceiling(Vector3.Distance(previous,corner)/1.25f));
                if(count>64||points.Count+count>128)return false;
                var start=previous;
                for(int i=1;i<=count;i++)
                {
                    var point=Vector3.Lerp(start,corner,(float)i/count);
                    if(!Retry.ReturnSegment(GetPlanarDistance(previous,point),Mathf.Abs(previous.y-point.y))||
                        !TryGetFirstNonSelfRaycastHit(player,point+Vector3.up*.6f,Vector3.down,1f,LayersMaskController.HighPolyWithTerrainMask,out var hit)||
                        Mathf.Abs(hit.point.y-point.y)>.35f||hit.normal.y<.65f||
                        TryGetFirstNonSelfCapsuleCastHit(player,previous+Vector3.up*.4f,previous+Vector3.up*1.35f,.18f,
                            GetFlatDirection(point-previous),GetPlanarDistance(previous,point),LayersMaskController.HighPolyWithTerrainMask,out _))return false;
                    points.Add(point);previous=point;
                }
            }
            var step=mission.Steps[mission.CurrentStepIndex];
            if(TryGetCapabilityCompletedPrefixBoundary(bot,mission,step,out var boundary,out var axis)&&
                !FollowerCapabilityIngressMotionPolicy.PrefixPath(true,Vector3.Dot(GetFlatDelta(boundary,player.Position),axis),
                    Vector3.Dot(GetFlatDelta(boundary,plan.Entry),axis),Vector3.Dot(GetFlatDelta(boundary,plan.Stage),axis),
                    points.Min(p=>Vector3.Dot(GetFlatDelta(boundary,p),axis))))return false;
            HoldCapabilityRouteRetry(bot);plan.HoldGeneration=0;plan.ReturnPath=null;plan.ReturnGeneration=0;
            plan.ReturnCorridor=points;plan.ReturnCorridorIndex=0;plan.ReturnCorridorMoving=false;
            mission.LastProgressAt=Time.time;mission.LastProgressPosition=player.Position;mission.ApproachDeferredLastEntryDistance=length;
            WriteEvent("capabilityFullRouteReturnCorridorArmed",bot,new{missionId=mission.MissionId,rootEdge=plan.Steps[0].EdgeId,
                points=points.Count,arc=length,body=Vec(player.Position),target=Vec(plan.Stage),source="currentStrictSupportedGroundPath",nativeSuccessRecorded=false});
            return true;
        }
        private static bool TryBuildCapabilityReturnCorridor(BotOwner bot,FollowerRouteMission mission)
        {
            var plan=mission.RouteRetry;
            // Reverse only a demonstrated grounded WT prefix, never Vault/Jump/DropDown itself.
            if(plan.ReturnCorridor!=null||plan.ReturnCorridorIndex<0||
                !string.Equals(plan.Steps[0].Kind,"WalkThrough",StringComparison.OrdinalIgnoreCase))return false;
            var failed=mission.Steps[mission.CurrentStepIndex];
            if(plan.Steps.TakeWhile(x=>x.EpisodeId!=failed.EpisodeId).Any(x=>
                !string.Equals(x.Kind,"WalkThrough",StringComparison.OrdinalIgnoreCase)&&
                !string.Equals(x.Kind,"PhysicalCorridor",StringComparison.OrdinalIgnoreCase)))return false;
            int prefixEnd=string.Equals(failed.Kind,"WalkThrough",StringComparison.OrdinalIgnoreCase)
                ?failed.EndBreadcrumbSequence:failed.StartBreadcrumbSequence;
            var points=plan.Breadcrumbs.Where(n=>n!=null&&n.Grounded&&IsFinite(n.Position)&&
                n.Sequence>=plan.Steps[0].StartBreadcrumbSequence&&n.Sequence<=prefixEnd)
                .OrderByDescending(n=>n.Sequence).Select(n=>n.Position).ToList();
            if(points.Count<2)return false;
            int nearest=-1;float best=float.MaxValue;
            for(int index=0;index<points.Count;index++)
            {
                float distance=Vector3.Distance(bot.Position,points[index]);
                if(distance<best){best=distance;nearest=index;}
            }
            if(nearest<0||best>.85f)return false;
            var corridor=new List<Vector3>();Vector3 previous=bot.Position;float arc=0f;
            foreach(var point in points.Skip(nearest).Concat(new[]{plan.Entry,plan.Stage}))
            {
                float distance=GetPlanarDistance(previous,point);
                if(distance<.04f&&Mathf.Abs(previous.y-point.y)<.04f)continue;
                if(!Retry.ReturnSegment(distance,Mathf.Abs(previous.y-point.y)))return false;
                arc+=Vector3.Distance(previous,point);corridor.Add(point);previous=point;
            }
            if(corridor.Count==0||!Retry.PathWithinBudget(arc,plan.ReturnBudget))return false;
            HoldCapabilityRouteRetry(bot);plan.HoldGeneration=0;
            plan.ReturnCorridor=corridor;plan.ReturnCorridorIndex=0;plan.ReturnCorridorMoving=false;
            mission.LastProgressAt=Time.time;mission.LastProgressPosition=bot.Position;
            mission.ApproachDeferredLastEntryDistance=arc;
            WriteEvent("capabilityFullRouteReturnCorridorArmed",bot,new{missionId=mission.MissionId,
                rootEdge=plan.Steps[0].EdgeId,points=corridor.Count,arc,body=Vec(bot.Position),
                target=Vec(plan.Stage),source="reverseGroundedWalkThroughPrefix",nativeSuccessRecorded=false});
            return true;
        }
        private static bool TryDriveCapabilityReturnCorridor(BotOwner bot,FollowerRouteMission mission)
        {
            var plan=mission.RouteRetry;var player=bot.GetPlayer;var points=plan.ReturnCorridor;
            if(points==null||plan.ReturnCorridorIndex<0||plan.ReturnCorridorIndex>=points.Count)return false;
            if(!plan.ReturnCorridorMoving)
            {
                var velocity=player.Velocity;velocity.y=0f;
                if((player.CurrentStateName!=EPlayerState.Run&&player.CurrentStateName!=EPlayerState.Idle)||velocity.magnitude>.35f)
                {
                    HoldCapabilityRouteRetry(bot);
                    if(Time.time-mission.LastProgressAt>=2f){plan.ReturnCorridor=null;plan.ReturnCorridorIndex=-1;return false;}
                    return true;
                }
                plan.ReturnCorridorMoving=true;plan.HoldGeneration=0;
            }
            int index=plan.ReturnCorridorIndex;
            while(index<points.Count-1&&GetPlanarDistance(player.Position,points[index])<=.15f&&
                Mathf.Abs(player.Position.y-points[index].y)<=.30f)index++;
            plan.ReturnCorridorIndex=index;Vector3 target=points[index];
            float distance=GetPlanarDistance(player.Position,target);
            float remaining=Vector3.Distance(player.Position,target);
            for(int n=index+1;n<points.Count;n++)remaining+=Vector3.Distance(points[n-1],points[n]);
            if(Retry.PathProgress(mission.ApproachDeferredLastEntryDistance,remaining,
                GetPlanarDistance(player.Position,mission.LastProgressPosition)))
            {mission.ApproachDeferredLastEntryDistance=remaining;mission.LastProgressAt=Time.time;mission.LastProgressPosition=player.Position;}
            Vector3 segment=index>0?points[index-1]:mission.LastProgressPosition;
            var span=target-segment;span.y=0f;var offset=player.Position-segment;offset.y=0f;
            float fraction=span.sqrMagnitude>.0001f?Mathf.Clamp01(Vector3.Dot(offset,span)/span.sqrMagnitude):0f;
            float deviation=GetPlanarDistance(player.Position,segment+span*fraction);
            float expectedY=Mathf.Lerp(segment.y,target.y,fraction);
            if(!Retry.ReturnLocal(Time.time-mission.LastProgressAt,deviation,Mathf.Abs(player.Position.y-expectedY)))
            {
                plan.ReturnCorridor=null;plan.ReturnCorridorIndex=-1;HoldCapabilityRouteRetry(bot);
                WriteEvent("capabilityFullRouteReturnCorridorRejected",bot,new{missionId=mission.MissionId,
                    deviation,body=Vec(player.Position),target=Vec(target),nativeSuccessRecorded=false});
                return false;
            }
            var direction=GetFlatDirection(target-player.Position);if(direction.sqrMagnitude<.5f){HoldCapabilityRouteRetry(bot);return true;}
            plan.HoldGeneration=0;
            var receipt=ApplyCollisionActivePhysicalRouteMotion(bot,player,direction,direction*Mathf.Max(.001f,Time.deltaTime),
                Mathf.Max(.001f,Time.deltaTime),1f,sprint:false,targetMoveSpeed:Retry.ReturnSpeed(remaining),
                preserveSprintMode:false,exactWorldDirectionInput:true,deliveryPurpose:CapabilityMotionPurpose.Recovery);
            if(!receipt.Accepted)HoldCapabilityRouteRetry(bot);
            if(ShouldEmitDiagnostic("fullRouteReturnCorridor:"+mission.MissionId,bot,.3f))
                WriteEvent("capabilityFullRouteReturnCorridorProgress",bot,new{missionId=mission.MissionId,index,remaining,
                    body=Vec(player.Position),target=Vec(target),receipt=receipt.Kind.ToString(),nativeSuccessRecorded=false});
            return true;
        }



    }
}
