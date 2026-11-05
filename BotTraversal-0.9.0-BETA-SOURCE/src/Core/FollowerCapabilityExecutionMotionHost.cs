using System;
using System.Linq;
using EFT;
using UnityEngine;
namespace BotTraversal.Core
{
    internal static partial class FollowerTraversalTestRuntime
    {
        private static bool TryGetCapabilityObstacleCenterFacing(BotOwner bot,out Vector3 facing)
        {
            facing=default;
            if(!TryGetActiveProbe(bot,out var p)||!IsProbeLifecycleCurrent(p)||p.Stage!=ProbeStage.NativeEntryAcquisition||
                p.NativeExecutorConsumed||p.LearningV32NativeEntryReacquireActive||
                !TryGetLearnedCommitmentForProbe(p,out var c)||(c.Kind!="Vault"&&c.Kind!="Climb"))return false;
            var axis=GetFlatDirection(c.RawExit-c.RawEntry);var offset=GetFlatDelta(c.RawEntry,bot.Position);
            float side=(offset-axis*Vector3.Dot(offset,axis)).magnitude;
            var edge=LearnedTraversalEdges.FirstOrDefault(e=>e!=null&&e.Id==(c.SkillTransferTransient?c.SkillTemplateEdgeId:c.EdgeId));
            float lateral=GetLearningV357ActuatorEntryLateralTolerance(edge,axis,c.Kind=="Vault"?InjectedNativeVaultRollingMaxLateralOffset:InjectedNativeVaultEntryMaxLateralOffset);
            double maximum=FollowerCapabilityPassagePolicy.ObstacleCenterTolerance(lateral,InjectedNativeVaultEntryTriggerDistance);
            if(axis.sqrMagnitude<.5f||!FollowerCapabilityPassagePolicy.CenterSideOnly(true,offset.magnitude)||
                !FollowerCapabilityPassagePolicy.ObstacleNeedsCenter(side,maximum))return false;
            facing=axis;return true;
        }
        private static bool TrySelectCapabilityClearForwardCorner(ProbeRuntime p,out Vector3 target)
        {
            target=default;
            if(!IsProbeLifecycleCurrent(p)||p.Stage!=ProbeStage.WalkThrough||
                !TryProjectOntoWalkThroughCorridor(p,p.Player.Position,out float bodyS,out _,out _,out _))return false;
            float from=Mathf.Max(bodyS,p.WalkThroughProgressS);
            for(int i=1;i<=5;i++)
            {
                var next=SampleWalkThroughCorridorAtDistance(p,Mathf.Min(p.WalkThroughTotalLength,from+i*.12f));
                if(!TryGetCapabilitySupportedLocalAim(p.Player,next,out var aim)||
                    !TryProjectOntoWalkThroughCorridor(p,aim,out float s,out var projected,out _,out float lateral)||
                    !FollowerCapabilityPassagePolicy.ForwardCorner(from,s,lateral,aim.y-projected.y))continue;
                target=aim;return true;
            }
            return false;
        }
        private static Vector3 GetCapabilityWalkThroughEntryTarget(ProbeRuntime probe,Vector3 fallback)
        {
            // The physical lane witness and all entry producers must refer to the same finite episode.
            if(probe!=null&&probe.LearningV372CurrentEpisodeWalkThroughCorridorAuthority&&
                probe.WalkThroughCorridorPoints!=null&&probe.WalkThroughCorridorPoints.Count>=2&&
                IsFinite(probe.WalkThroughCorridorPoints[0]))return probe.WalkThroughCorridorPoints[0];
            return fallback;
        }
        // Read-only preview of the finite recorded lane. Gait changes still use the one existing producer.
        private static bool NeedsCapabilityWalkThroughPrecision(ProbeRuntime p,Vector3 requestedDirection)
        {
            if(!IsProbeLifecycleCurrent(p)||p.RequestedTraversalKind!="WalkThrough"||
                p.WalkThroughStepAssistNativeOwned||p.WalkThroughCorridorPoints==null||p.WalkThroughCorridorPoints.Count<2)return false;
            var points=p.WalkThroughCorridorPoints;var body=p.Player.Position;
            float arc=0,bodyS=0,best=float.MaxValue,height=0;
            float minimum=p.Stage==ProbeStage.WalkThrough?Mathf.Max(0,p.WalkThroughProgressS-WalkThroughArcProjectionBackSlack):0;
            for(int i=1;i<points.Count;i++)
            {
                if(!IsFinite(points[i-1])||!IsFinite(points[i]))return true;
                var delta=GetFlatDelta(points[i-1],points[i]);float length=delta.magnitude;
                if(length<.001f)continue;
                float fraction=Mathf.Clamp01(Vector3.Dot(GetFlatDelta(points[i-1],body),delta)/(length*length));
                var projected=Vector3.Lerp(points[i-1],points[i],fraction);float s=arc+fraction*length;
                float side=GetPlanarDistance(body,projected);
                if(s>=minimum&&(side<best-.001f||Mathf.Abs(side-best)<=.001f&&s>bodyS))
                {best=side;bodyS=s;height=body.y-projected.y;}
                arc+=length;
            }
            float heading=Vector3.Dot(GetFlatDirection(p.Player.MovementContext.PlayerRealForward),GetFlatDirection(requestedDirection));
            bool correction=FollowerCapabilityIngressMotionPolicy.WalkingLanePrecision(best,height,heading);
            float speed=GetLearningV329PlanarSpeed(p.Player),apex=0,turnDistance=float.MaxValue,turnDot=1;
            for(int i=1;i<points.Count-1;i++)
            {
                apex+=GetPlanarDistance(points[i-1],points[i]);
                var incoming=GetFlatDirection(points[i]-points[i-1]);var outgoing=GetFlatDirection(points[i+1]-points[i]);
                if(incoming.sqrMagnitude<.5f||outgoing.sqrMagnitude<.5f)continue;
                float dot=Vector3.Dot(incoming,outgoing),distance=apex-bodyS;
                if(FollowerCapabilityIngressMotionPolicy.WalkingTurnPrecision(distance,dot,speed)&&distance<turnDistance)
                {correction=true;turnDistance=distance;turnDot=dot;}
            }
            // Short WT episodes may put the bend exactly at the semantic handoff.
            var m=p.LifecycleMission;
            if(m.CurrentStepIndex+1<m.Steps.Count&&IsRouteMissionSameChainImmediateSuccessor(m,m.CurrentStepIndex+1))
            {
                var next=m.Steps[m.CurrentStepIndex+1];var incoming=GetFlatDirection(points[points.Count-1]-points[points.Count-2]);
                var outgoing=next==null?Vector3.zero:GetFlatDirection(next.RawExit-next.RawEntry);
                if(next?.Kind=="WalkThrough"&&GetPlanarDistance(points[points.Count-1],next.RawEntry)<=.65f&&
                    incoming.sqrMagnitude>.5f&&outgoing.sqrMagnitude>.5f)
                {
                    float distance=arc-bodyS,dot=Vector3.Dot(incoming,outgoing);
                    if(FollowerCapabilityIngressMotionPolicy.WalkingTurnPrecision(distance,dot,speed)&&distance<turnDistance)
                    {correction=true;turnDistance=distance;turnDot=dot;}
                }
            }
            if(correction&&ShouldEmitDiagnostic("capabilityWalkingTurnPrecision",p.Bot,.25f))WriteEvent("capabilityWalkingTurnPrecision",p.Bot,new{
                edgeId=p.LearnedEdgeId,episodeId=p.TraversalEpisodeId,body=Vec(body),bodyS,lateral=best,height,heading,speed,
                turnDistance=turnDistance<float.MaxValue?(float?)turnDistance:null,turnDot,brakingDistance=FollowerCapabilityIngressMotionPolicy.BrakingDistance(speed),
                nativeSuccessRecorded=false,bodyRelocated=false,action="entryUsesRunPassageRetainsSprintWithFiniteSpeedEnvelope"});
            return correction;
        }
        private static bool NeedsCapabilityEntryRun(BotOwner bot)
        {
            if(bot?.GetPlayer?.MovementContext==null||!IsCapabilityRouteRetrySafe(bot)||
                !FollowerRouteMissions.TryGetValue(bot.ProfileId,out var m)||m.Lifecycle.Retired||
                m.CurrentStepIndex<0||m.CurrentStepIndex>=m.Steps.Count)return false;
            var player=bot.GetPlayer;
            if(IsCapabilityReturnInProgress(bot))
                return m.RouteRetry!=null&&GetPlanarDistance(player.Position,m.RouteRetry.Stage)<=
                    Mathf.Max(2.2f,(float)FollowerCapabilityIngressMotionPolicy.BrakingDistance(GetLearningV329PlanarSpeed(player)));
            bool hasProbe=TryGetActiveProbe(bot,out var probe);
            bool entry=!hasProbe||IsProbeLifecycleCurrent(probe)&&!probe.NativeExecutorConsumed&&
                (probe.Stage==ProbeStage.Aligning||probe.Stage==ProbeStage.NativeEntryAcquisition||probe.Stage==ProbeStage.RollingApproach);
            if(!entry)return false;
            var step=m.Steps[m.CurrentStepIndex];var selected=m.FailureSequence?.Selected;
            var kind=selected?.Kind??step.Kind;var origin=selected?.RawEntry??step.RawEntry;
            var axis=GetFlatDirection((selected?.RawExit??step.RawExit)-origin);
            if(hasProbe&&kind=="WalkThrough") {
                origin=GetCapabilityWalkThroughEntryTarget(probe,origin);
                if(probe.WalkThroughCorridorPoints!=null&&probe.WalkThroughCorridorPoints.Count>=2)
                    axis=GetFlatDirection(probe.WalkThroughCorridorPoints[1]-origin);
            }
            if(kind=="Jump")origin=selected==null?GetLearningV442JumpAdmissionEntry(step):GetLearningV442JumpAdmissionEntry(selected);
            if(!IsFinite(origin)||axis.sqrMagnitude<.5f)return false;
            var offset=GetFlatDelta(origin,player.Position);float forward=Vector3.Dot(offset,axis),side=(offset-axis*forward).magnitude;
            if((kind=="WalkThrough"||kind=="PhysicalCorridor")&&forward>LearningV32EntryProgressEpsilon)return false;
            float maximum=kind=="Jump"?LearningV352JumpTakeoffCorridorMaxLateral:WalkThroughPassageStraightLateralTolerance;
            float facing=kind=="Jump"?LearningV352JumpTakeoffCorridorMinFacingDot:Mathf.Cos(InjectedNativeVaultEntryMaxAlignmentAngle*Mathf.Deg2Rad);
            return FollowerCapabilityIngressMotionPolicy.EntryCorrection(entry,offset.magnitude<=RouteMissionTraversalAdjacentBridgeMaxDistance,
                side,maximum,Vector3.Dot(GetFlatDirection(player.MovementContext.PlayerRealForward),axis),facing);
        }
        private static Vector3 SelectCapabilityWalkThroughCornerTarget(ProbeRuntime probe,Vector3 position,float desiredS)
        {
            float from=probe.WalkThroughProgressS,safe=from;
            var first=SampleWalkThroughCorridorAtDistance(probe,from);
            double initial=GetPlanarDistance(position,first);
            const float increment=.12f; // bounded geometry inspection; no new cursor or progress clock.
            for(int i=1;i<=16;i++)
            {
                float candidateS=Mathf.Min(desiredS,from+i*increment);
                var candidate=SampleWalkThroughCorridorAtDistance(probe,candidateS);bool inside=true;
                for(int j=0;j<i;j++)
                {
                    var point=SampleWalkThroughCorridorAtDistance(probe,Mathf.Min(candidateS,from+j*increment));
                    double deviation=FollowerCapabilityIngressMotionPolicy.ChordDeviation(position.x,position.z,candidate.x,candidate.z,point.x,point.z);
                    if(!FollowerCapabilityIngressMotionPolicy.RejoinChordInside(deviation,WalkThroughPassageStraightLateralTolerance,initial,
                        (Mathf.Min(candidateS,from+j*increment)-from)/Mathf.Max(.001f,candidateS-from)))
                    {inside=false;break;}
                }
                if(!inside)break;
                safe=candidateS;if(candidateS>=desiredS-.001f)break;
            }
            // The recovery envelope decays toward a forward join, avoiding a perpendicular Sprint correction.
            // It still rejects a new inside-corner shortcut; no cursor or body position is advanced here.
            return SampleWalkThroughCorridorAtDistance(probe,safe);
        }
        private static bool TryConstrainCapabilityWalkThroughTarget(ProbeRuntime probe,ref Vector3 target)
        {
            if(!IsProbeLifecycleCurrent(probe)||probe.RequestedTraversalKind!="WalkThrough")return false;
            var player=probe.Player;var delta=GetFlatDelta(player.Position,target);float distance=delta.magnitude;
            if(distance<=.015f)return false;
            var direction=delta/distance;
            bool hit=TryGetFirstNonSelfCapsuleCastHit(player,player.Position+Vector3.up*WalkThroughCapsuleBottom,
                player.Position+Vector3.up*WalkThroughCapsuleTop,WalkThroughCapsuleRadius,direction,distance,
                LayerMaskClass.HighPolyWithTerrainMask,out var obstacle);
            bool blocked=FollowerCapabilityIngressMotionPolicy.WallBlocks(hit,hit?obstacle.normal.y:0,
                hit?-Vector3.Dot(direction,obstacle.normal):0,hit?obstacle.distance:0);
            if(!blocked)return true;
            if(TrySelectCapabilityClearForwardCorner(probe,out var forwardCorner)){target=forwardCorner;return true;}
            float available=Mathf.Max(0,obstacle.distance-.03f);
            target=Vector3.Lerp(player.Position,target,Mathf.Clamp01(available/distance));
            if(ShouldEmitDiagnostic("capabilityWTWallTarget",probe.Bot,.25f))WriteEvent("capabilityWalkThroughWallTargetConstrained",probe.Bot,new{
                edgeId=probe.LearnedEdgeId,body=Vec(player.Position),target=Vec(target),blocker=obstacle.collider?.name,
                distance=obstacle.distance,normal=Vec(obstacle.normal),available,nativeSuccessRecorded=false,bodyRelocated=false});
            return available>.015f;
        }
    }
}
