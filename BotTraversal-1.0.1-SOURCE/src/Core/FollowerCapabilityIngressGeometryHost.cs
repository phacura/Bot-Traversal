// R3P_GEOMETRY_EXTENSION_REVIEWED
using System;
using System.Linq;
using EFT;
using UnityEngine;
namespace BotTraversal.Core
{
    internal static partial class FollowerTraversalTestRuntime
    {
        private static void ApplyLearningV372CurrentEpisodeWalkThroughExecutionGeometry(
            BotOwner bot,
            TraversalEpisodeMetadata episode,
            LearnedTraversalEdge semanticEdge,
            Vector3 observedStart,
            Vector3 observedEnd,
            string source)
        {
            if ((episode == null) || semanticEdge == null ||
                episode.EdgeId != semanticEdge.Id ||
                !string.Equals(semanticEdge.Kind, "WalkThrough", StringComparison.OrdinalIgnoreCase) ||
                !semanticEdge.PlayerValidated || episode.StartBreadcrumbSequence <= 0 ||
                episode.EndBreadcrumbSequence < episode.StartBreadcrumbSequence ||
                !IsFinite(observedStart) || !IsFinite(observedEnd))
            {
                return;
            }

            bool published=FollowerRouteMissions.Values.Any(m=>m!=null&&!m.Lifecycle.Retired&&
                m.LifecycleRaid==raidGeneration&&m.Steps!=null&&m.Steps.Any(s=>s!=null&&
                s.EpisodeId==episode.EpisodeId&&s.Lifecycle.DebtOutstanding));
            if(FollowerCapabilityIngressGeometryPolicy.KeepPublished(
                episode.LearningV372HasCurrentExecutionGeometry,published,
                IsFinite(episode.LearningV372CurrentExecutionStart)&&IsFinite(episode.LearningV372CurrentExecutionEnd),
                GetPlanarDistance(episode.LearningV372CurrentExecutionStart,episode.LearningV372CurrentExecutionEnd)))
            {
                if(ShouldEmitDiagnostic("publishedWTGeometry:"+episode.EpisodeId,bot,.65f))
                    WriteEvent("capabilityPublishedWalkThroughGeometryRetained",bot,new{
                        episodeId=episode.EpisodeId,edgeId=episode.EdgeId,
                        retainedEntry=Vec(episode.LearningV372CurrentExecutionStart),
                        retainedExit=Vec(episode.LearningV372CurrentExecutionEnd),
                        proposedEntry=Vec(observedStart),proposedExit=Vec(observedEnd),
                        source,nativeSuccessRecorded=false,bodyRelocated=false});
                return;
            }
            // TryFindDirectionalWalkThroughBreadcrumbCrossing already established the historical
            // semantic identity + direction. Preserve that edge identity, but execute THIS raid on
            // the player's actually observed passage lane rather than stale historical anchors.
            episode.LearningV372HasCurrentExecutionGeometry = true;
            episode.LearningV372CurrentExecutionStart = observedStart;
            episode.LearningV372CurrentExecutionEnd = observedEnd;
            episode.LearningV372ExecutionGeometrySource = source ?? "currentRaidDirectionalCrossing";
            if ((TryGetLearningV373ConsolidatedWalkThroughExecutionGeometry(
                    episode,
                    out Vector3 learningV373ConsolidatedStart,
                    out Vector3 learningV373ConsolidatedEnd,
                    out long learningV373ConsolidatedStartSequence,
                    out long learningV373ConsolidatedEndSequence,
                    out int learningV373ContributingEpisodes)) &&
                learningV373ContributingEpisodes > 1)
            {
                episode.LearningV372CurrentExecutionStart = learningV373ConsolidatedStart;
                episode.LearningV372CurrentExecutionEnd = learningV373ConsolidatedEnd;
                episode.LearningV372ExecutionGeometrySource =
                    "learningV373ConsolidatedCurrentRaidPassage:" + (source ?? "currentRaidDirectionalCrossing");
                WriteEvent("learningV373CurrentEpisodeWalkThroughGeometryConsolidated", bot, new
                {
                    episodeId = episode.EpisodeId,
                    edgeId = semanticEdge.Id,
                    chainId = episode.ChainId,
                    contributingEpisodes = learningV373ContributingEpisodes,
                    consolidatedStartBreadcrumbSequence = learningV373ConsolidatedStartSequence,
                    consolidatedEndBreadcrumbSequence = learningV373ConsolidatedEndSequence,
                    currentExecutionStart = Vec(learningV373ConsolidatedStart),
                    currentExecutionEnd = Vec(learningV373ConsolidatedEnd),
                    persistentLearningChanged = false,
                    traversalSuccessRecorded = false,
                    action = "overlappingDirectionalWindowsBecomeOneCurrentRaidPhysicalPassage"
                });
            }
            WriteEvent("learningV372CurrentEpisodeWalkThroughExecutionGeometryBound", bot, new
            {
                episodeId = episode.EpisodeId,
                edgeId = semanticEdge.Id,
                experienceKey = semanticEdge.ExperienceKey,
                startBreadcrumbSequence = episode.StartBreadcrumbSequence,
                endBreadcrumbSequence = episode.EndBreadcrumbSequence,
                historicalRawStart = Vec(semanticEdge.RawStart),
                historicalRawEnd = Vec(semanticEdge.RawEnd),
                observedExecutionStart = Vec(observedStart),
                observedExecutionEnd = Vec(observedEnd),
                currentExecutionStart = Vec(episode.LearningV372CurrentExecutionStart),
                currentExecutionEnd = Vec(episode.LearningV372CurrentExecutionEnd),
                source = episode.LearningV372ExecutionGeometrySource,
                persistentLearningChanged = false,
                traversalSuccessRecorded = false,
                action = "historicalSemanticIdentityCurrentEpisodeExecutionGeometry"
            });
        }

        private static bool RefreshLearningV424CurrentEpisodeWalkThroughAdmissionGeometry(
            BotOwner bot,
            FollowerRouteMission mission,
            FollowerRouteMissionStep step,
            string source)
        {
            if ((bot == null) || mission == null || step == null ||
                !step.CurrentRaidRouteAuthority || step.AttemptStartedObserved || HasActiveTraversalProbe(bot) ||
                !string.Equals(step.Kind, "WalkThrough", StringComparison.OrdinalIgnoreCase) ||
                !TryResolveLearningV424CurrentEpisodeWalkThroughGeometry(
                    step.EdgeId, step.EpisodeId, 0, 0,
                    out TraversalEpisodeMetadata episode,
                    out Vector3 executionEntry,
                    out Vector3 executionExit))
            {
                return false;
            }

            Vector3 previousRawEntry = step.RawEntry;
            Vector3 previousRawExit = step.RawExit;
            bool changed = !IsFinite(previousRawEntry) || !IsFinite(previousRawExit) ||
                Vector3.Distance(previousRawEntry, executionEntry) > LearningV32EntryProgressEpsilon ||
                Vector3.Distance(previousRawExit, executionExit) > LearningV32EntryProgressEpsilon;
            if (!changed)
            {
                return false;
            }

            Vector3 oldAxis=GetFlatDirection(previousRawExit-previousRawEntry);
            Vector3 newAxis=GetFlatDirection(executionExit-executionEntry);
            float span=GetPlanarDistance(previousRawEntry,previousRawExit);
            float start=Vector3.Dot(executionEntry-previousRawEntry,oldAxis);
            float end=Vector3.Dot(executionExit-previousRawEntry,oldAxis);
            float side=GetPlanarDistance(executionEntry,previousRawEntry+oldAxis*start);
            if(!FollowerCapabilityIngressGeometryPolicy.AcceptRefresh(
                IsFinite(previousRawEntry)&&IsFinite(previousRawExit),span,start,end,
                Vector3.Dot(oldAxis,newAxis),side,WalkThroughPassageStraightLateralTolerance,
                executionEntry.y-previousRawEntry.y,WalkThroughMaxVerticalDelta,LearningV32EntryProgressEpsilon))
            {
                if(ShouldEmitDiagnostic("rejectedWTTail:"+step.EpisodeId,bot,.65f))
                    WriteEvent("capabilityWalkThroughTailRefreshRejected",bot,new{
                        missionId=mission.MissionId,episodeId=step.EpisodeId,edgeId=step.EdgeId,
                        retainedEntry=Vec(previousRawEntry),retainedExit=Vec(previousRawExit),
                        proposedEntry=Vec(executionEntry),proposedExit=Vec(executionExit),
                        span,start,end,source,nativeSuccessRecorded=false,bodyRelocated=false});
                return false;
            }

            step.RawEntry = executionEntry;
            step.Entry = executionEntry;
            step.RawExit = executionExit;
            step.Exit = executionExit;

            string profileId = bot.ProfileId ?? string.Empty;
            bool commitmentRefreshed = false;
            if (!string.IsNullOrEmpty(profileId) &&
                LearnedTraversalCommitments.TryGetValue(profileId, out LearnedTraversalCommitment commitment) &&
                commitment != null && commitment.RouteMissionId == mission.MissionId &&
                commitment.RouteMissionStepIndex == mission.CurrentStepIndex &&
                commitment.EdgeId == step.EdgeId)
            {
                commitment.EpisodeId = episode.EpisodeId;
                commitment.ChainId = step.ChainId > 0 ? step.ChainId : episode.ChainId;
                commitment.Entry = executionEntry;
                commitment.RawEntry = executionEntry;
                commitment.Exit = executionExit;
                commitment.RawExit = executionExit;
                commitment.LastEntryDistance = GetPlanarDistance(bot.Position, executionEntry);
                commitment.LastEntryProgressAt = Time.time;
                commitment.NextApproachPathAttemptAt = 0f;
                commitment.ApproachPathFailures = 0;
                commitment.ApproachMoverCommitFailures = 0;
                commitmentRefreshed = true;
            }

            WriteEvent("learningV424WalkThroughExecutionGeometryRefreshedBeforeAdmission", bot, new
            {
                missionId = mission.MissionId,
                stepIndex = mission.CurrentStepIndex,
                episodeId = episode.EpisodeId,
                edgeId = step.EdgeId,
                authoredEpisodeEdgeId = episode.EdgeId,
                semanticAliasApplied = episode.EdgeId != step.EdgeId,
                previousRawEntry = IsFinite(previousRawEntry) ? (object)Vec(previousRawEntry) : null,
                previousRawExit = IsFinite(previousRawExit) ? (object)Vec(previousRawExit) : null,
                executionRawEntry = Vec(executionEntry),
                executionRawExit = Vec(executionExit),
                commitmentRefreshed,
                source = source ?? string.Empty,
                traversalSuccessRecorded = false,
                persistentLearningChanged = false,
                bodyRelocated = false,
                action = "latestCurrentEpisodeExecutionGeometryOutranksEarlierMissionOrCommitmentSnapshot"
            });
            return true;
        }
        private static bool TrySelectCapabilityPhysicalClimbApproach(BotOwner bot,
            FollowerRouteMission mission,FollowerRouteMissionStep step,out Vector3 target)
        {
            target=default;
            if(step==null||!step.CurrentRaidRouteAuthority||!TryCaptureCapabilityClimbIngress(bot,mission,step,out var frame)||
                !TryGetLearningV424ActuatorExecutorAdmission(bot,step,out _,out _,out float distance,
                    out float forward,out float lateral,out float maxLateral,out _,out _,out float maxForward,
                    out float trigger,out _))return false;
            Vector3 side=bot.Position-step.RawEntry-frame.Axis*forward;side.y=0f;
            float stop=Mathf.Min(trigger*.5f,maxLateral*.5f);
            if(!FollowerCapabilityIngressGeometryPolicy.PhysicalTarget(true,distance,trigger+1.95f,
                forward,maxForward,lateral,stop))return false;
            target=step.RawEntry+frame.Axis*Mathf.Max(0f,forward)+side.normalized*Mathf.Min(side.magnitude,stop);
            target.y=bot.Position.y;
            float movement=GetPlanarDistance(bot.Position,target);
            string failure=string.Empty,blocker=string.Empty;
            bool corridor=IsFinite(target)&&movement>=.20f&&
                TryValidateWalkThroughCorridor(frame.Player,bot.Position,target,out failure,out blocker);
            if(ShouldEmitDiagnostic("physicalClimbTarget:"+step.EpisodeId,bot,.35f))
                WriteEvent("capabilityClimbPhysicalCorridorTarget",bot,new{
                    missionId=mission.MissionId,episodeId=step.EpisodeId,edgeId=step.EdgeId,
                    body=Vec(bot.Position),target=Vec(target),distance,forward,lateral,stop,corridor,failure,blocker,
                    nativeSuccessRecorded=false,bodyRelocated=false});
            if(!corridor)
            {
                // A lateral chase at the obstacle plane can hit the rail. First retreat on
                // the current side, then align behind the trigger plane. Never cross it here.
                Vector3 start=bot.Position;
                float lead=Mathf.Max(.65f,trigger+.20f);
                Vector3[] alternatives={start-frame.Axis*lead,step.RawEntry-frame.Axis*lead,step.RawEntry-frame.Axis*(lead+ .45f)};
                foreach(var candidate in alternatives)
                {
                    var staged=candidate;staged.y=start.y;
                    float projection=Vector3.Dot(staged-step.RawEntry,frame.Axis);
                    if(!FollowerCapabilityObstacleIngressPolicy.Staging(true,forward,lateral,maxLateral,projection,
                        GetPlanarDistance(start,staged),frame.Distance,RouteMissionTraversalAdjacentBridgeMaxDistance))continue;
                    if(!TryValidateWalkThroughCorridor(frame.Player,start,staged,out _,out _))continue;
                    target=staged;corridor=true;
                    if(ShouldEmitDiagnostic("capabilityObstacleIngressStage:"+step.EpisodeId,bot,.35f))
                        WriteEvent("capabilityObstacleIngressStage",bot,new{missionId=mission.MissionId,episodeId=step.EpisodeId,
                            edgeId=step.EdgeId,target=Vec(target),projection,lateral,collisionChecked=true,nativeSuccessRecorded=false});
                    break;
                }
            }
            return corridor&&TryCaptureCapabilityClimbIngress(bot,mission,step,out _);
        }
    }
}
