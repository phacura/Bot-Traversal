using System;

namespace BotTraversal.Core
{
    // Pure command precision, never physical entry or completion evidence.
    internal static class FollowerWalkThroughArrivalPolicy
    {
        internal enum Action { PreserveDefault, TightenReach, Reject }
        internal readonly struct Plan
        {
            internal readonly Action Kind;
            internal readonly float ReachDistance;
            internal Plan(Action kind, float reach = 0f)
            { Kind = kind; ReachDistance = reach; }
        }
        internal static Plan Select(float distance, float approachRadius,
            bool currentEpisodeAuthority, bool corridorWitness, bool insideLane,
            float laneTolerance)
        {
            if (!Finite(distance) || !Finite(approachRadius) || !Finite(laneTolerance) ||
                distance < 0f || approachRadius <= 0f || laneTolerance <= 0f)
                return new Plan(Action.Reject);
            if (!currentEpisodeAuthority || !corridorWitness || insideLane || distance > approachRadius)
                return new Plan(Action.PreserveDefault);
            return new Plan(Action.TightenReach, Math.Min(laneTolerance, approachRadius) * 0.5f);
        }
        private static bool Finite(float value)
        { return !float.IsNaN(value) && !float.IsInfinity(value); }
    }
}
