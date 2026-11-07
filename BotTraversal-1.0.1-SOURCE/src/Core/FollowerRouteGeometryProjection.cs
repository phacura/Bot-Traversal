using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BotTraversal.Core
{
    // CF14 first mechanical split of the large Traversal runtime.
    // Keep private mission/node types and projection state on the original runtime;
    // source extraction does not introduce a competing movement owner.
    internal static partial class FollowerTraversalTestRuntime
    {
        private static bool TryProjectLearningV375RouteArc(
            FollowerRouteMission mission,
            Vector3 position,
            out float routeArc,
            out Vector3 tangent,
            out Vector3 projectedPoint,
            out int projectedSequence)
        {
            routeArc = 0f;
            tangent = Vector3.zero;
            projectedPoint = position;
            projectedSequence = 0;
            if (mission?.RouteBreadcrumbs == null || mission.RouteBreadcrumbs.Count < 2)
            {
                return false;
            }

            // CF14: the canonical live route is already ordered by Sequence. Rebuilding
            // the same sorted list for each route-arc projection allocates and sorts on
            // the Unity thread, even when the route has not changed. The fast path
            // reads the EXISTING list without mutating it. Any null, non-finite point
            // or out-of-order sequence uses the exact CF13 LINQ fallback (stable sort).
            // Equal sequences are deliberately accepted: OrderBy is stable.
            List<PlayerBreadcrumbNode> routeNodes = mission.RouteBreadcrumbs;
            bool canonicalOrderedFinite = true;
            int previousSequence = int.MinValue;
            for (int nodeIndex = 0; nodeIndex < routeNodes.Count; nodeIndex++)
            {
                PlayerBreadcrumbNode node = routeNodes[nodeIndex];
                if (node == null || !IsFinite(node.Position) ||
                    node.Sequence < previousSequence)
                {
                    canonicalOrderedFinite = false;
                    break;
                }
                previousSequence = node.Sequence;
            }
            List<PlayerBreadcrumbNode> nodes = canonicalOrderedFinite
                ? routeNodes
                : routeNodes.Where(node => node != null && IsFinite(node.Position))
                    .OrderBy(node => node.Sequence).ToList();
            if (nodes.Count < 2)
            {
                return false;
            }

            float cumulative = 0f;
            // CF11: in a multi-level WalkThrough/PhysicalCorridor, two different
            // route segments can overlap in XZ while lying metres apart in Y.
            // The old nearest-PLANAR-segment selector could jump the route cursor
            // to the wrong bridge deck and then drive a physically regressive
            // producer. Keep legacy planar projection for native actuators
            // (Jump/Climb/DropDown/Vault) so valid airborne elevation does not
            // change their proven entry/landing behavior.
            bool spatialNonActuatorProjection = false;
            if (mission.Steps != null && mission.CurrentStepIndex >= 0 &&
                mission.CurrentStepIndex < mission.Steps.Count)
            {
                FollowerRouteMissionStep activeStep = mission.Steps[mission.CurrentStepIndex];
                spatialNonActuatorProjection = activeStep != null &&
                    (string.Equals(activeStep.Kind, "WalkThrough", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(activeStep.Kind, "PhysicalCorridor", StringComparison.OrdinalIgnoreCase));
            }
            float bestDistance = float.MaxValue;
            float bestArc = 0f;
            Vector3 bestTangent = Vector3.zero;
            Vector3 bestPoint = position;
            int bestSequence = nodes[0].Sequence;
            Vector3 flatPosition = position;
            flatPosition.y = 0f;

            for (int index = 0; index < nodes.Count - 1; index++)
            {
                Vector3 a = nodes[index].Position;
                Vector3 b = nodes[index + 1].Position;
                Vector3 flatA = a;
                Vector3 flatB = b;
                flatA.y = 0f;
                flatB.y = 0f;
                Vector3 segment = flatB - flatA;
                float length = segment.magnitude;
                if (length <= 0.001f)
                {
                    continue;
                }
                Vector3 direction = segment / length;
                float local = Mathf.Clamp(Vector3.Dot(flatPosition - flatA, direction), 0f, length);
                Vector3 flatProjected = flatA + direction * local;
                float t = local / length;
                float planarDistance = Vector3.Distance(flatPosition, flatProjected);
                Vector3 physicalRoutePoint = Vector3.Lerp(a, b, t);
                float verticalError = position.y - physicalRoutePoint.y;
                // Score only the nearest ROUTE SEGMENT, not the body's physical
                // ability to reach it. No teleport, NavMesh snap or simulated
                // progress is produced here. Arc length stays horizontal and
                // retains the original contract for all downstream consumers.
                float distance = spatialNonActuatorProjection
                    ? Mathf.Sqrt(planarDistance * planarDistance + verticalError * verticalError)
                    : planarDistance;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestArc = cumulative + local;
                    bestTangent = direction;
                    bestPoint = Vector3.Lerp(a, b, t);
                    bestSequence = t < 0.5f ? nodes[index].Sequence : nodes[index + 1].Sequence;
                }
                cumulative += length;
            }

            if (bestDistance == float.MaxValue || bestTangent.sqrMagnitude < 0.25f)
            {
                return false;
            }
            routeArc = bestArc;
            tangent = bestTangent;
            projectedPoint = bestPoint;
            projectedSequence = bestSequence;
            return true;
        }

    }
}
