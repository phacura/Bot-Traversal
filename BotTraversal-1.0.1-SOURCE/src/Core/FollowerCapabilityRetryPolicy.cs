using System;

namespace BotTraversal.Core
{
    // Pure update of the existing Mission clock; proposals never create an attempt.
    internal static class FollowerCapabilityRetryPolicy
    {
        internal enum Change { Reset, ProposalDeferred, AttemptOrExplicitHold }
        internal static float Apply(float existing,Change change,float requestedAt)
        {
            if (change==Change.Reset) return 0f;
            if (change==Change.ProposalDeferred) return existing;
            return !float.IsNaN(requestedAt) && !float.IsInfinity(requestedAt) && requestedAt>=0f
                ? requestedAt : existing;
        }
    }
}
