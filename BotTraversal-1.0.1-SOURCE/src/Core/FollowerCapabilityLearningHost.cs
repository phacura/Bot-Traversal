using System;
using UnityEngine;
namespace BotTraversal.Core
{
    internal static class FollowerCapabilityLearningPolicy
    {
        private static bool Finite(double v)=>!double.IsNaN(v)&&!double.IsInfinity(v);
        internal static double RecoveryRank(int successes,int failures,double legacyScore,bool hasEvidence,int attempts,
            int persistedSuccesses,int persistedFailures,double recentScore,double distance)
            => FollowerCapabilityFailureSequence.Rank(hasEvidence?Math.Max(successes,Math.Max(0,persistedSuccesses)):successes,
                hasEvidence?Math.Max(failures,Math.Max(0,persistedFailures)):failures,
                hasEvidence&&attempts>0?recentScore:legacyScore,distance);
        internal static bool FreshDemonstration(bool validated,bool proven,double age,double lifetime)
            => validated&&!proven&&Finite(age)&&age>=0&&Finite(lifetime)&&lifetime>0&&age<=lifetime;
        internal static bool CanDiscard(bool historical,int physicalSuccesses,bool referenced,bool fresh)
            => !historical&&physicalSuccesses==0&&!referenced&&!fresh;
    }
    internal static partial class FollowerTraversalTestRuntime
    {
        private static double RankCapabilityLearnedRecovery(LearnedTraversalEdge edge,float distance)
        {
            TraversalExperienceRecord legacy=null;LearningV322PersistentSkillEvidenceRecord evidence=null;
            if(!string.IsNullOrWhiteSpace(edge.ExperienceKey))
            {TraversalExperiences.TryGetValue(edge.ExperienceKey,out legacy);LearningV322SkillEvidence.TryGetValue(edge.ExperienceKey,out evidence);}
            bool current=evidence!=null&&string.Equals(evidence.Kind,edge.Kind,StringComparison.OrdinalIgnoreCase);
            return FollowerCapabilityLearningPolicy.RecoveryRank(Math.Max(edge.NativeSuccessCount,GetTraversalPhysicalSuccessCount(edge)),
                edge.FailedExecutions,GetTraversalExperienceEffectiveScore(legacy),current,evidence?.NativeAttempts??0,
                evidence?.NativeSuccesses??0,evidence?.NativeFailures??0,evidence?.RecentNativeSuccessScore??.5f,distance);
        }
        // Lifetime only. A newly observed route is not cache garbage while the recorder is publishing it.
        private static bool HasCapabilityFreshTraversalDefinition(LearnedTraversalEdge edge)
            => edge!=null&&FollowerCapabilityLearningPolicy.FreshDemonstration(edge.PlayerValidated,
                IsLearningV3NativeProvenEdge(edge),Time.time-edge.CreatedAt,LearningV3UnprovenCandidateTtlSeconds);
        private static bool CanDiscardCapabilityTraversalEdge(LearnedTraversalEdge edge)
            => edge!=null&&FollowerCapabilityLearningPolicy.CanDiscard(
                IsLearningV345ProtectedHistoricalWalkThroughSemantic(edge),GetTraversalPhysicalSuccessCount(edge),
                false,HasCapabilityFreshTraversalDefinition(edge))&&IsTraversalEdgeActivelyReferenced(edge.Id)==false;
        private static bool HasCapabilityRecordedEdgeReference(int edgeId)
        {
            // A canonical mission can execute a proven parent while still consuming its authored child episode.
            foreach(var probe in ActiveProbes.Values)
                if(probe!=null&&probe.FrozenTraversalCommitment?.SkillTemplateEdgeId==edgeId)return true;
            foreach(var commitment in LearnedTraversalCommitments.Values)
                if(commitment!=null&&commitment.SkillTemplateEdgeId==edgeId)return true;
            foreach(var m in FollowerRouteMissions.Values)
            {
                if(m==null||m.Lifecycle.Retired||m.LifecycleRaid!=raidGeneration||m.Steps==null)continue;
                if(m.FailureSequence?.Selected?.SkillTemplateEdgeId==edgeId)return true;
                for(int i=Math.Max(0,m.CurrentStepIndex);i<m.Steps.Count;i++)
                {
                    var step=m.Steps[i];
                    if(step==null||!step.Lifecycle.DebtOutstanding)continue;
                    foreach(var episode in TraversalEpisodeStream)
                        if(episode!=null&&episode.EpisodeId==step.EpisodeId&&episode.ChainId==step.ChainId&&episode.EdgeId==edgeId)return true;
                }
            }
            return false;
        }
    }
}
