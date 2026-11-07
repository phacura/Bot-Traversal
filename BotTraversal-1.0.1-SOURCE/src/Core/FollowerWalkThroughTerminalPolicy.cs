using System;

namespace BotTraversal.Core
{
    // Stateless decision data. Verification is still performed by the existing
    // full physical passage gate. A forward plan only requests another input.
    internal static class FollowerWalkThroughTerminalPolicy
    {
        internal enum Action { Verify, Advance, HoldForRecovery }
        internal readonly struct Plan
        {
            internal readonly Action Kind;
            internal readonly float ForwardDistance;
            internal Plan(Action kind, float distance = 0f)
            { Kind = kind; ForwardDistance = distance; }
        }

        internal static Plan Select(
            bool currentEpisodeAuthority, bool passageProven, bool downstreamRejoinProven,
            bool measurementAvailable, float farSideProjection, float lateralDistance,
            float verticalDistance, float directionAgreement, float epsilon,
            float lateralLimit, float verticalLimit)
        {
            if (!currentEpisodeAuthority || passageProven || downstreamRejoinProven)
                return new Plan(Action.Verify);
            if (!measurementAvailable || !Finite(farSideProjection) ||
                !Finite(lateralDistance) || !Finite(verticalDistance) ||
                !Finite(directionAgreement) || !Finite(epsilon) ||
                !Finite(lateralLimit) || !Finite(verticalLimit) ||
                lateralDistance < 0f || verticalDistance < 0f ||
                epsilon < 0f || lateralLimit <= 0f || verticalLimit <= 0f ||
                farSideProjection + epsilon >= 0f ||
                lateralDistance > lateralLimit || verticalDistance > verticalLimit ||
                directionAgreement < 0.25f || directionAgreement > 1.001f)
                return new Plan(Action.HoldForRecovery);
            // Request only the missing forward distance plus a small fraction
            // of the existing numerical envelope. No new completion tolerance.
            float distance = -farSideProjection + epsilon * 0.25f;
            if (!Finite(distance) || distance <= 0f || distance > lateralLimit)
                return new Plan(Action.HoldForRecovery);
            return new Plan(Action.Advance, distance);
        }

        private static bool Finite(float value)
        { return !float.IsNaN(value) && !float.IsInfinity(value); }
    }
}
