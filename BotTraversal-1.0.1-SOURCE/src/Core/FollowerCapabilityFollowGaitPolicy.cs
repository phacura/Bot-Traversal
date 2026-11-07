namespace BotTraversal.Core
{
    internal static class FollowerCapabilityFollowGaitPolicy
    {
        internal static bool Select(bool requested,bool current,bool eligible,bool canSprint,float distance,float followDistance)
        {
            if(!eligible)return requested;
            if(!canSprint)return false;
            if(float.IsNaN(distance)||float.IsInfinity(distance)||distance<0||
                float.IsNaN(followDistance)||float.IsInfinity(followDistance))return requested;
            float threshold=System.Math.Min(System.Math.Max(8,System.Math.Min(30,followDistance))+3,16);
            // Existing Sprinting is the memory; no new per-bot timer or mode registry.
            if(distance>=threshold+1)return true;
            if(distance<=threshold-2)return false;
            return current;
        }
    }
}
