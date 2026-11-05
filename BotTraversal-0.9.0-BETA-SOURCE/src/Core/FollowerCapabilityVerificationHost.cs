using EFT;
using UnityEngine;
namespace BotTraversal.Core
{
    internal static partial class FollowerTraversalTestRuntime
    {
        private static bool TryCorrectCapabilityDropVerify(ProbeRuntime probe,LearnedTraversalCommitment c,bool exitSide,bool supported,bool nav)
        {
            if(!IsProbeLifecycleCurrent(probe)||probe.Stage!=ProbeStage.Verify||c==null||
                !IsCapabilityRouteRetrySafe(probe.Bot))return false;
            var player=probe.Player;var body=player.Position;var target=probe.TraversalVerifyTarget;
            var axis=GetFlatDirection(c.RawExit-c.RawEntry);var offset=GetFlatDelta(body,target);
            if(!IsFinite(target)||axis.sqrMagnitude<.5f||!FollowerCapabilityIngressMotionPolicy.VerifyExitCorrection(c.Kind=="DropDown",
                probe.TraversalVerifyMoving,probe.TraversalTeleportAttempted,player.MovementContext.IsGrounded,supported,nav,exitSide,
                offset.magnitude,Vector3.Dot(offset,axis),target.y-body.y,c.RawEntry.y-body.y)||
                !TryGetCapabilitySupportedLocalAim(player,target,out var aim))return false;
            var direction=GetFlatDirection(aim-body);if(direction.sqrMagnitude<.5f)return false;
            float dt=Mathf.Max(.001f,Time.deltaTime);
            bool accepted=TryCommitV2ProbePhysicalMotion(probe,direction,direction*dt,dt,1f,false,.45f,
                preserveSprintMode:false,exactWorldDirectionInput:true);
            if(accepted&&ShouldEmitDiagnostic("capabilityDropVerifyExitCorrection",probe.Bot,.20f))
                WriteEvent("capabilityDropVerifyExitCorrection",probe.Bot,new{edgeId=c.EdgeId,body=Vec(body),target=Vec(target),aim=Vec(aim),
                    nativeSuccessRecorded=false,bodyRelocated=false,verifyDeadlineExtended=false});
            return accepted; // Only later real observations can satisfy the original Verify proof and deadline.
        }
    }
}
