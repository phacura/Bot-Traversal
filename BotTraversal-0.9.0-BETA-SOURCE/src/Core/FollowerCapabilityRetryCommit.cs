namespace BotTraversal.Core
{
    internal static partial class FollowerTraversalTestRuntime
    {
        // The sole writer of Mission move retry time. No separate capability clock.
        private static void CommitCapabilityMoveRetry(FollowerRouteMission mission,
            FollowerCapabilityRetryPolicy.Change change,float requestedAt)
        {
            if (mission==null || mission.Lifecycle.Retired) return;
            mission.NextMoveAttemptAt=FollowerCapabilityRetryPolicy.Apply(mission.NextMoveAttemptAt,change,requestedAt);
        }
    }
}
