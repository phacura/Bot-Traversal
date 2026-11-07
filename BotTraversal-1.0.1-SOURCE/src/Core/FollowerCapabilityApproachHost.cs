using System;
using EFT;
using UnityEngine;
namespace BotTraversal.Core
{
    internal static partial class FollowerTraversalTestRuntime
    {
        private static CapabilityApproachPlan GetCapabilityJumpApproachPlan(Player player,bool local,float lateral,float forward)=>
            FollowerCapabilityApproachPolicy.Jump(player.MovementContext.IsGrounded,local&&(lateral<=LearningV352JumpTakeoffCorridorMaxLateral||
                forward>=-GetLearningV452JumpPreparationLeadDistance(GetLearningV329PlanarSpeed(player))),
                IsLearningV350TerminalPhysicalRecoveryEligible(player.AIData?.BotOwner),lateral,
                LearningV352JumpTakeoffCorridorMaxLateral,ProbeMomentumFullMoveSpeed,.24f);

        private static bool TrySelectCapabilityLandingApproach(ProbeRuntime probe,ref Vector3 direction,ref Vector3 motion)
        {
            if(probe?.Bot==null||probe.Player?.MovementContext==null||!IsProbeLifecycleCurrent(probe)||
                IsLearningV350TerminalPhysicalRecoveryEligible(probe.Bot)||IsTeleportTransactionActive(probe.Bot)||
                probe.Player.MovementContext.OverridenState!=null||probe.Player.MovementContext.OverridenControlsState!=null||
                FollowerEnvironment.TryGetImmediateSurvivalThreat(probe.Bot,out _)||
                FollowerStateAuthorityCoordinator.HasCombatMovementClaim(probe.Bot,out _))return false;
            var follower=BossPlayers.GetFollowerByProfileId(probe.Bot.ProfileId);
            if(follower!=null&&follower.TryPeekActiveCommand(out var command,out _,out _)&&command!=FollowerCommandType.None)return false;
            var commitment=probe.FrozenTraversalCommitment;var body=probe.Player.Position;
            if(commitment==null||!IsFinite(direction)||!IsFinite(motion)||motion.sqrMagnitude<=.000001f||
                !TryCaptureCapabilityAdjacentLanding(probe,commitment,body,out var next,out var geometry))return false;
            var axis=GetFlatDirection(commitment.RawExit-commitment.RawEntry);
            float progress=Vector3.Dot(body-commitment.RawEntry,axis);
            float required=FollowerCapabilityDropDownTransitionPolicy.RequiredProjection(
                Mathf.Max(.01f,GetPlanarDistance(commitment.RawEntry,commitment.RawExit)),NativeDropDownFarSideRatio);
            if(!FollowerCapabilityApproachPolicy.Landing(IsProbeLifecycleCurrent(probe),IsCapabilityDropDownConsumerPhase(probe),
                probe.NativeDropDownAttempted,string.Equals(commitment.Kind,"DropDown",StringComparison.OrdinalIgnoreCase),
                probe.Bot.Mover?.HasPathAndNoComplete!=true,FollowerCapabilityAdjacentLandingPolicy.LegacyRegion(geometry),
                progress,required,geometry.Ahead,geometry.VerticalError,geometry.AboveExit))return false;
            var successorAxis=GetFlatDirection(next.RawExit-next.RawEntry);
            var target=next.RawEntry-successorAxis*.025f;target.y=body.y;
            var selected=GetFlatDirection(target-body);
            if(selected.sqrMagnitude<.5f||!IsProbeLifecycleCurrent(probe))return false;
            var original=motion;float magnitude=new Vector2(original.x,original.z).magnitude;
            direction=selected;motion=new Vector3(selected.x*magnitude,original.y,selected.z*magnitude);
            if(ShouldEmitDiagnostic("capabilityLandingApproach:"+probe.TraversalEpisodeId,probe.Bot,.15f))
                WriteEvent("capabilityLandingApproachSelected",probe.Bot,new{missionId=probe.LifecycleMission?.MissionId,
                    episodeId=probe.TraversalEpisodeId,edgeId=commitment.EdgeId,successorEdgeId=next.EdgeId,
                    phase=probe.Stage.ToString(),target=Vec(target),direction=Vec(direction),body=Vec(body),
                    geometry.Ahead,geometry.Lateral,nativeSuccessRecorded=false,bodyRelocated=false});
            return true;
        }
    }
}
