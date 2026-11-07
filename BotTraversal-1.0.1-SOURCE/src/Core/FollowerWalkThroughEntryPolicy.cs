using System;

namespace BotTraversal.Core
{
    // Pure orientation admission. The caller owns the existing episode corridor
    // geometry check. This grants neither a physical-entry latch nor completion.
    internal static class FollowerWalkThroughEntryPolicy
    {
        internal static bool CanAcquireCorridorOrientation(
            float entryDistance, float approachRadius,
            bool currentEpisodeCorridorAuthority, bool episodeCorridorEntryLane)
        {
            if (float.IsNaN(entryDistance) || float.IsInfinity(entryDistance) ||
                float.IsNaN(approachRadius) || float.IsInfinity(approachRadius) ||
                entryDistance < 0f || approachRadius <= 0f || entryDistance > approachRadius)
                return false;
            return !currentEpisodeCorridorAuthority || episodeCorridorEntryLane;
        }
    }
}
