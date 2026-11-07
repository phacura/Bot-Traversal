using UnityEngine;

namespace BotTraversal.Core
{
    // CF20: read-only terminal consumer cursor predicate. No movement authority,
    // player mutation, NavMesh writes, mission success, or durable learning here.
    internal static class FollowerGenericRouteCursorContract
    {
        internal static bool IsSuccessorPhysicallyPassed(
            Vector3 body, Vector3 target, Vector3 successor,
            float laneRadius, float forwardWitness)
        {
            if (float.IsNaN(body.x) || float.IsNaN(body.y) || float.IsNaN(body.z) ||
                float.IsInfinity(body.x) || float.IsInfinity(body.y) || float.IsInfinity(body.z) ||
                float.IsNaN(target.x) || float.IsNaN(target.y) || float.IsNaN(target.z) ||
                float.IsInfinity(target.x) || float.IsInfinity(target.y) || float.IsInfinity(target.z) ||
                float.IsNaN(successor.x) || float.IsNaN(successor.y) || float.IsNaN(successor.z) ||
                float.IsInfinity(successor.x) || float.IsInfinity(successor.y) || float.IsInfinity(successor.z) ||
                laneRadius <= 0f || forwardWitness < 0f)
            {
                return false;
            }
            // A different height is not a valid horizontal crossing witness:
            // keep above/below NavMesh strata separate on bridges and climbs.
            if (Mathf.Abs(body.y - successor.y) > laneRadius)
            {
                return false;
            }
            Vector3 tangent = successor - target;
            tangent.y = 0f;
            float span = tangent.magnitude;
            if (span < 0.08f || span > 9f)
            {
                return false;
            }
            Vector3 displacement = body - target;
            displacement.y = 0f;
            Vector3 direction = tangent / span;
            float along = Vector3.Dot(displacement, direction);
            if (along < span + forwardWitness)
            {
                return false;
            }
            float lateral = (displacement - direction * along).magnitude;
            return lateral <= laneRadius;
        }
    }
}
