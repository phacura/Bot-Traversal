// R3Q_CONTINUITY_EXTENSION_REVIEWED
using System;
namespace BotTraversal.Core
{
    internal static class FollowerCapabilityChainContinuityPolicy
    {
        private static bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v);
        // Restrict only the native forward delta after THIS drop has reached its own
        // far side. Reserve the near-side runway before the late hard border binds.
        // -1 means no additional restriction; never grants admission or completion.
        internal static float LandingBudget(DropDownBoundarySnapshot s,float dt,float stableSeconds,float verticalSpeed)
        {
            // Preserve the original R3Q admission envelope for its existing callers.
            return FollowerCapabilityHandoffPolicy.LandingBudget(s,dt,stableSeconds,verticalSpeed,.20f);
        }
        internal static bool Resume(bool current,bool predecessorExecuted,bool pending,bool grounded,bool idle,
            float ahead,float side,float height,float heading,float maximumHeading)=>
            current&&predecessorExecuted&&pending&&grounded&&idle&&
            Finite(ahead)&&ahead>=-.10f&&ahead<=1.55f&&Finite(side)&&side>=0f&&side<=.38f&&
            Finite(height)&&height>=0f&&height<=1.35f&&Finite(heading)&&heading>=0f&&
            Finite(maximumHeading)&&maximumHeading>0f&&heading<=maximumHeading;
    }
}
