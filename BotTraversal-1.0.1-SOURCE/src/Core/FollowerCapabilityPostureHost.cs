using System;
using System.Linq;
using EFT;
using UnityEngine;
namespace BotTraversal.Core
{
    internal static partial class FollowerTraversalTestRuntime
    {
        private static LearnedTraversalEdge? GetCapabilityPostureEdge(BotOwner bot)
        {
            if(bot==null)return null;
            if(TryGetActiveProbe(bot,out var probe)&&IsProbeLifecycleCurrent(probe))
            {
                if(!string.Equals(probe.TraversalKind??probe.RequestedTraversalKind,"WalkThrough",StringComparison.OrdinalIgnoreCase))return null;
                var edge=LearnedTraversalEdges.FirstOrDefault(e=>e!=null&&e.Id==probe.LearnedEdgeId);
                if(edge?.Kind=="WalkThrough")return edge;
            }
            if(!TryGetActiveFollowerRouteMission(bot,out var m)||m.CurrentStepIndex<0||m.CurrentStepIndex>=m.Steps.Count)return null;
            var step=m.Steps[m.CurrentStepIndex];
            if(step?.Kind!="WalkThrough"||!step.Lifecycle.DebtOutstanding)return null;
            return LearnedTraversalEdges.FirstOrDefault(e=>e!=null&&e.Id==step.EdgeId&&e.Kind=="WalkThrough");
        }
        private static float GetCapabilityWalkingPosture(BotOwner bot,float requested)
            =>FollowerCapabilityPosturePolicy.Resolve(requested,GetCapabilityPostureEdge(bot)?.RequiredPose??1f);
        private static float GetCapabilityProbePosture(ProbeRuntime probe)
        {
            if(probe==null)return 1f;
            var edge=LearnedTraversalEdges.FirstOrDefault(e=>e!=null&&e.Id==probe.LearnedEdgeId&&e.Kind=="WalkThrough");
            return FollowerCapabilityPosturePolicy.Normalize(edge?.RequiredPose??1f);
        }
        private static void GetCapabilityCorridorCapsule(Player player,Vector3 origin,out Vector3 bottom,out Vector3 top,out float radius)
        {
            bottom=origin+Vector3.up*WalkThroughCapsuleBottom;top=origin+Vector3.up*WalkThroughCapsuleTop;radius=WalkThroughCapsuleRadius;
            if(player?.MovementContext==null||player.CharacterController==null)return;
            float pose=GetCapabilityWalkingPosture(player.AIData?.BotOwner,FollowerCapabilityPosturePolicy.Normalize(player.MovementContext.PoseLevel));
            var cc=player.CharacterController;
            MovementContext.CalculatePoseLevelValues(pose,cc.center,cc.skinWidth,false,out float height,out var center);
            if(!IsFinite(center))return;
            var capsule=FollowerCapabilityPosturePolicy.QueryCapsule(height,center.y,cc.radius,cc.skinWidth);
            // Same finite native posture envelope for every walking query. Only the read-only
            // query is inset by at most 2 cm of the native contact skin; body collision stays active.
            bottom=origin+new Vector3(center.x,capsule.Bottom,center.z);
            top=origin+new Vector3(center.x,capsule.Top,center.z);radius=capsule.Radius;
        }
        private static bool IsCapabilityWalkThroughSnapshotPublished(WalkThroughEpisodeBuilder snapshot)
            =>snapshot!=null&&TraversalEpisodeStream.Any(e=>e!=null&&e.EpisodeId==snapshot.EpisodeId);
        private static void FinalizeCapabilityWalkThroughSnapshot(WalkThroughEpisodeBuilder snapshot,string reason)
        {
            if(snapshot==null)return;
            if(IsCapabilityWalkThroughSnapshotPublished(snapshot))
            {
                WriteEvent("capabilityWalkThroughSnapshotAlreadyPublished",null,new{snapshotEpisode=snapshot.EpisodeId,reason,nativeSuccessRecorded=false});
                return;
            }
            var current=activeWalkThroughEpisode;
            activeWalkThroughEpisode=CloneWalkThroughEpisode(snapshot);
            FinalizeActiveWalkThroughEpisode(reason);
            // A newer posture episode must not be discarded by an older airborne snapshot.
            if(current!=null&&current.EpisodeId!=snapshot.EpisodeId)activeWalkThroughEpisode=current;
        }
        private static void CompleteCapabilityPostureEpisode(PlayerBreadcrumbNode exit)
        {
            var episode=activeWalkThroughEpisode;
            if(episode==null||!FollowerCapabilityPosturePolicy.Crouched(episode.RequiredPose)||exit==null||
                FollowerCapabilityPosturePolicy.Crouched(exit.Pose)||!exit.Grounded||
                !FollowerCapabilityPosturePolicy.Locomotion(exit.State)||
                Vector3.Distance(episode.RawEnd,exit.Position)>.75f||exit.CreatedAt-episode.LastUpdatedAt>2f)return;
            // The first observed standing point is the finite exit; keep crouching until that point.
            episode.RawEnd=exit.Position;episode.EndSequence=exit.Sequence;episode.LastUpdatedAt=exit.CreatedAt;
            episode.CorridorSamples.Add(exit.Position);
        }
    }
}
