// R3S_PHASE_HANDOFF_EXTENSION_REVIEWED
namespace BotTraversal.Core
{
    internal static class FollowerCapabilityPhaseHandoffPolicy
    {
        internal static bool CanPrepare(bool current,bool completedPhase,bool nativeProof,bool grounded,bool eligibleState,bool blocked)=>
            current&&completedPhase&&nativeProof&&grounded&&eligibleState&&!blocked;
        internal static bool StateEligible(bool jump,bool drop,bool waiting,bool locomotion)=>
            (jump!=drop)&&(waiting||(drop&&locomotion));
        internal static bool PreserveJumpMomentum(bool jump,float forwardSpeed)=>
            jump&&(float.IsNaN(forwardSpeed)||float.IsInfinity(forwardSpeed)||forwardSpeed>0f);
        // A managed Run/Sprint label with an outbound stop transition is not a live consumer.
        internal static bool Coherent(bool managedLocomotion,bool nativeMatches,bool inTransition,bool nextLocomotion)=>
            managedLocomotion&&nativeMatches&&(!inTransition||nextLocomotion);
    }
}
