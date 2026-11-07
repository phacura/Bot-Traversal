using Comfort.Common;
using EFT;
using EFT.Vaulting;
using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.AI;

namespace BotTraversal.Core
{
    // RC61: mechanical extraction of RC60 methods into the SAME partial runtime.
    // No new ownership, clock, writer, recovery loop, execution success or persisted state.
    internal static partial class FollowerTraversalTestRuntime
    {
        private static bool IsLearningV386ThreeDimensionalCurrentEpisodeWalkThroughStep(
            FollowerRouteMissionStep step)
        {
            return (step != null) && step.CurrentRaidRouteAuthority &&
                   string.Equals(step.Kind, "WalkThrough", StringComparison.OrdinalIgnoreCase);
        }

        private static bool UsesLearningV386ThreeDimensionalPhysicalRoute(FollowerRouteMissionStep step)
        {
            return ((IsLearningV369PhysicalCorridorStep(step) ||
                    IsLearningV386ThreeDimensionalCurrentEpisodeWalkThroughStep(step)));
        }

        private static float GetLearningV380PhysicalBreadcrumbDistance(
            FollowerRouteMissionStep step,
            Vector3 from,
            Vector3 target)
        {
            if (UsesLearningV386ThreeDimensionalPhysicalRoute(step))
            {
                return Vector3.Distance(from, target);
            }
            return GetPlanarDistance(from, target);
        }

        private static float GetLearningV380PhysicalBreadcrumbReachDistance(
            FollowerRouteMissionStep step)
        {
            // PhysicalCorridor is reconstructed from the player's recorder samples. Consuming a
            // point with the broader RouteMission 1.15m radius can skip the only raised/airborne
            // sample at a doorstep or rough ledge. Reuse the recorder's existing 0.5m spatial
            // resolution so execution cannot discard information that capture deliberately kept.
            return UsesLearningV386ThreeDimensionalPhysicalRoute(step)
                ? BreadcrumbMinDistance
                : RouteMissionBreadcrumbReachDistance;
        }

        private static Vector3 GetLearningV381PortalIdentityEndpoint(
            LearnedTraversalEdge edge,
            bool end)
        {
            if (edge == null)
            {
                return default(Vector3);
            }

            LearnedTraversalEdge identity = edge;
            if ((edge.SyntheticPhysicalCorridor) && edge.RuntimeAlternativeParentEdgeId > 0)
            {
                LearnedTraversalEdge parent = LearnedTraversalEdges.FirstOrDefault(candidate =>
                    candidate != null && candidate.Id == edge.RuntimeAlternativeParentEdgeId);
                if (parent != null)
                {
                    identity = parent;
                }
            }

            if (end)
            {
                return IsFinite(identity.RawEnd) ? identity.RawEnd : identity.B;
            }
            return IsFinite(identity.RawStart) ? identity.RawStart : identity.A;
        }

        private static float GetLearningV381PhysicalCorridorApproachSpeed(
            IList<PlayerBreadcrumbNode> corridor)
        {
            if ((corridor == null) || corridor.Count < 2)
            {
                return 0f;
            }

            PlayerBreadcrumbNode first = corridor[0];
            PlayerBreadcrumbNode last = corridor[corridor.Count - 1];
            if (first == null || last == null || last.CreatedAt <= first.CreatedAt)
            {
                return 0f;
            }

            float distance = 0f;
            PlayerBreadcrumbNode previous = first;
            for (int index = 1; index < corridor.Count; index++)
            {
                PlayerBreadcrumbNode current = corridor[index];
                if (current == null || previous == null || !IsFinite(current.Position) || !IsFinite(previous.Position))
                {
                    previous = current;
                    continue;
                }
                distance += Vector3.Distance(previous.Position, current.Position);
                previous = current;
            }
            return distance > 0f ? distance / Mathf.Max(0.001f, last.CreatedAt - first.CreatedAt) : 0f;
        }

        private static string GetLearningV381PhysicalCorridorApproachMode(
            IList<PlayerBreadcrumbNode> corridor)
        {
            if ((corridor == null) || corridor.Count == 0)
            {
                return string.Empty;
            }

            if (corridor.Any(node => node != null &&
                                     string.Equals(node.State, "Sprint", StringComparison.OrdinalIgnoreCase)))
            {
                return "Sprint";
            }
            PlayerBreadcrumbNode last = corridor.LastOrDefault(node =>
                node != null && !string.IsNullOrWhiteSpace(node.State));
            return last?.State ?? string.Empty;
        }

        private static float GetLearningV381PhysicalCorridorDriveSpeed(
            FollowerRouteMissionStep step)
        {
            if ((!IsLearningV369PhysicalCorridorStep(step)))
            {
                return ChainLocalSuccessorBridgeSpeed;
            }

            LearnedTraversalEdge edge = LearnedTraversalEdges.FirstOrDefault(candidate =>
                candidate != null && candidate.Id == step.EdgeId);
            return edge != null && IsFinite(edge.ApproachSpeed) && edge.ApproachSpeed > 0f
                ? Mathf.Max(ChainLocalSuccessorBridgeSpeed, edge.ApproachSpeed)
                : ChainLocalSuccessorBridgeSpeed;
        }

        private static bool GetLearningV381PhysicalCorridorSprintIntent(
            FollowerRouteMissionStep step)
        {
            if ((!IsLearningV369PhysicalCorridorStep(step)))
            {
                return false;
            }
            LearnedTraversalEdge edge = LearnedTraversalEdges.FirstOrDefault(candidate =>
                candidate != null && candidate.Id == step.EdgeId);
            return edge != null &&
                   string.Equals(edge.ApproachMode, "Sprint", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsLearningV369PhysicalCorridorStep(FollowerRouteMissionStep step)
        {
            return (step != null) &&
                   string.Equals(
                       GetLearningV354SemanticKind(step.EdgeId, step.Kind),
                       "PhysicalCorridor",
                       StringComparison.OrdinalIgnoreCase);
        }
        // RC62: native DropDown landing may overlap the NEXT typed DropDown entry.
        // Preserve downward physics, but bound horizontal native locomotion BEFORE
        // reaching that portal. This is only an intent speed policy for the current
        // Traversal owner, never successor admission/completion/recovery authority.
        private static float GetV2RC62AdjacentDropDownNativeSpeed(
            ProbeRuntime probe, LearnedTraversalCommitment commitment, Vector3 body,
            out int nextEdgeId, out float successorEntryAhead, out float successorLateral)
        {
            return GetCapabilityAdjacentDropDownNativeSpeed(probe,commitment,body,
                out nextEdgeId,out successorEntryAhead,out successorLateral);
        }

    }
}
