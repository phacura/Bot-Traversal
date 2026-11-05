// R3V_OBSTACLE_PACKAGE_REVIEWED
namespace BotTraversal.Core
{
    internal static class FollowerCapabilityObstacleIngressPolicy
    {
        private static bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v);
        internal static bool Staging(bool current,float forward,float lateral,float maxLateral,float candidateForward,
            float move,float entryDistance,float maximumDistance)=>
            current&&Finite(forward)&&Finite(lateral)&&Finite(maxLateral)&&maxLateral>0f&&lateral>maxLateral&&
            Finite(candidateForward)&&candidateForward<=-.20f&&Finite(move)&&move>=.20f&&move<=2.75f&&
            Finite(entryDistance)&&Finite(maximumDistance)&&entryDistance<=maximumDistance&&
            (forward>=-.20f||candidateForward>=forward-.20f);
    }
}
