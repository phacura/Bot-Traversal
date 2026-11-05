using System;
using System.Linq;
using EFT;
using UnityEngine;
namespace BotTraversal.Core
{
    internal static partial class FollowerTraversalTestRuntime
    {
        private static LearnedTraversalEdge? GetCapabilityWalkingRecoveryProof(LearnedTraversalCommitment original)
        {
            if(original?.Kind!="WalkThrough"||!IsFinite(original.RawEntry)||!IsFinite(original.RawExit))return null;
            var axis=GetFlatDirection(original.RawExit-original.RawEntry);
            return LearnedTraversalEdges.Where(e=>e!=null&&e.Kind=="WalkThrough"&&e.PlayerValidated&&
                IsLearningV3NativeProvenEdge(e)&&e.NativeVaultParameters==null&&IsFinite(e.RawStart)&&IsFinite(e.RawEnd)&&
                FollowerCapabilityPassagePolicy.WalkingIdentity(e.Id==original.EdgeId,FollowerCapabilityPassagePolicy.SameWalkingPortal(GetPlanarDistance(original.RawEntry,e.RawStart),
                    GetPlanarDistance(original.RawExit,e.RawEnd),Vector3.Dot(axis,GetFlatDirection(e.RawEnd-e.RawStart)))))
                .OrderByDescending(e=>e.NativeSuccessCount).ThenBy(e=>e.Id).FirstOrDefault();
        }
        private static bool IsCapabilityWalkingRecoveryOnly(LearnedTraversalCommitment original)
            => GetCapabilityWalkingRecoveryProof(original)!=null;
        private static bool CanAllocateCapabilityWalkingKind(BotOwner bot,string kind)
        {
            if(kind=="WalkThrough"||bot==null||!FollowerRouteMissions.TryGetValue(bot.ProfileId,out var m)||
                m.Lifecycle.Retired||m.LifecycleRaid!=raidGeneration||m.LifecycleBotId!=bot.ProfileId||
                m.CurrentStepIndex<0||m.CurrentStepIndex>=m.Steps.Count)return true;
            var step=m.Steps[m.CurrentStepIndex];
            bool walking=step?.Kind=="WalkThrough"&&step.Lifecycle.DebtOutstanding&&
                IsCapabilityWalkingRecoveryOnly(CapabilitySemanticCommitment(m,step));
            return FollowerCapabilityPassagePolicy.WalkingRecoveryKind(walking,kind);
        }
        private static bool TrySelectCapabilityWalkingRescue(BotOwner bot,FollowerRouteMission m,CapabilityFailureTransaction state)
        {
            var original=state.Original;var proof=GetCapabilityWalkingRecoveryProof(original);var player=bot.GetPlayer;
            if(proof==null||!IsCapabilityRouteRetrySafe(bot)||!player.MovementContext.IsGrounded)
            {
                WriteEvent("capabilityWalkingRescueUnavailable",bot,new{missionId=m.MissionId,edgeId=state.Edge,
                    reason=proof==null?"nativeWalkingProofUnavailable":!player.MovementContext.IsGrounded?"bodyNotGrounded":"higherPriorityMovement",
                    nativeActuatorAllowed=false,nativeSuccessRecorded=false});return false;
            }
            var axis=GetFlatDirection(original.RawExit-original.RawEntry);var offset=GetFlatDelta(original.RawEntry,player.Position);
            float forward=Vector3.Dot(offset,axis),span=GetPlanarDistance(original.RawEntry,original.RawExit);
            if(!FollowerCapabilityPassagePolicy.WalkingRescue(forward,span,(offset-axis*forward).magnitude,
                player.Position.y-original.RawEntry.y))
            {
                WriteEvent("capabilityWalkingRescueUnavailable",bot,new{missionId=m.MissionId,edgeId=state.Edge,
                    reason="outsideUnfinishedWalkingEnvelope",forward,span,lateral=(offset-axis*forward).magnitude,
                    height=player.Position.y-original.RawEntry.y,nativeActuatorAllowed=false,nativeSuccessRecorded=false});return false;
            }
            var selected=CapabilitySemanticCommitment(m,m.Steps[m.CurrentStepIndex]);
            var entry=forward<0?original.RawEntry:player.Position;
            selected.Kind="WalkThrough";selected.Entry=selected.RawEntry=selected.SettledEntry=entry;
            selected.Exit=selected.RawExit=selected.SettledExit=original.RawExit;
            selected.SkillTransferTransient=true;selected.LearningV382RouteBoundTransientNativeSkill=true;
            selected.SkillTemplateEdgeId=proof.Id;selected.SkillTransferNearFrontier=entry;selected.SkillTransferFarFrontier=original.RawExit;
            selected.SkillTransferSource="learningV433EquivalentPhysicalActuatorFrontier:RC146:ProvenWalkingContract";
            state.Selected=selected;
            WriteEvent("capabilityWalkingRescueSelected",bot,new{missionId=m.MissionId,edgeId=state.Edge,template=proof.Id,
                entry=Vec(entry),exit=Vec(original.RawExit),completedPrefixRetained=true,nativeActuatorAllowed=false,nativeSuccessRecorded=false});
            return true;
        }
        private static bool TryResolveCapabilityWalkThroughEgress(ProbeRuntime probe,LearnedTraversalCommitment c,
            out Vector3 target,out string source)
        {
            target=default;source=string.Empty;
            if(!IsProbeLifecycleCurrent(probe)||probe.LifecycleStep?.Kind!="WalkThrough"||c==null||
                c.Kind!="WalkThrough"||probe.RequestedTraversalKind!="WalkThrough"||
                c.RouteMissionId!=probe.LifecycleMission.MissionId||c.RouteMissionStepIndex!=probe.LifecycleMission.CurrentStepIndex||
                !IsFinite(c.RawExit)||Vector3.Distance(c.RawExit,probe.LifecycleStep.RawExit)>.05f)return false;
            target=c.RawExit;source="currentTypedWalkThroughRawExit";
            if(TrySampleTraversalSettledAnchor(target,TraversalSettledCaptureNavRadius,out var sampled)&&
                FollowerCapabilityPassagePolicy.WalkExitSample(GetPlanarDistance(target,sampled),sampled.y-target.y,
                    WalkThroughPassageStraightLateralTolerance,WalkThroughPassageMicroVerticalDeviation))
            {target=sampled;source="currentTypedWalkThroughLocalNavExit";}
            return true; // No broad NavMesh projection may substitute another lane for the recorded exit.
        }
        private static bool TryCloseCapabilityProvenWalkThrough(ProbeRuntime probe)
        {
            if(!IsProbeLifecycleCurrent(probe)||probe.LifecycleStep?.Kind!="WalkThrough"||
                probe.RequestedTraversalKind!="WalkThrough"||
                !TryGetLearnedCommitmentForProbe(probe,out var c)||c==null||c.Kind!="WalkThrough")return false;
            // Reuse all existing finite crossing and passage proofs; this adapter mints none.
            return TryConsumeLearningV414WalkThroughPhysicalPassBeforeRecovery(probe,allowEgressFallback:false);
        }
    }
}


