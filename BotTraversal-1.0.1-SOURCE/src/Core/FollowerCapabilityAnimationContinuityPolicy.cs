// R3T_ANIMATION_CONTINUITY_EXTENSION_REVIEWED
namespace BotTraversal.Core
{
    internal static class FollowerCapabilityAnimationContinuityPolicy
    {
        // An accepted request is pending only in the SAME native evaluation frame.
        // It is never a callback, body progress, retry delay, or inherited step credit.
        internal static bool SameEvaluation(FollowerTraversalLifecycle.Identity issued,FollowerTraversalLifecycle.Identity live,
            int issuedFrame,int frame,int issuedHash,int desiredHash,bool sameAnimator)=>
            issued.Valid&&live.Valid&&issued.Writer>0&&issued.Equals(live)&&issuedFrame>=0&&frame==issuedFrame&&
            issuedHash!=0&&issuedHash!=-1&&issuedHash==desiredHash&&sameAnimator;
        internal static bool TryReplayTime(bool sameState,float normalizedTime,out float replayTime)
        {
            replayTime=0f;
            if(!sameState)return true;
            if(float.IsNaN(normalizedTime)||float.IsInfinity(normalizedTime)||normalizedTime<0f)return false;
            replayTime=normalizedTime;return true;
        }
        internal static bool PreserveProbeGait(bool current,bool continuousInput,bool nativeProof,bool grounded,bool completedPhase,bool overridden,bool blocked)=>
            current&&continuousInput&&nativeProof&&grounded&&completedPhase&&!overridden&&!blocked;
        internal static bool LandingProofKeepsGait(bool jump,bool drop,bool attempted,bool proof,
            bool rejected,bool partial,bool forwardHandoff)=>
            (jump!=drop)&&attempted&&proof&&!rejected&&!partial&&(jump||forwardHandoff);
        internal static float ContinuationSpeed(bool eligible,bool sprinting,bool previousRunning,float precision,float normal,float full)=>
            eligible?(sprinting?full:(previousRunning?normal:precision)):precision;
    }
}
