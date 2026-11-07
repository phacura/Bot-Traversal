// R3X_ROUTE_CONTINUATION_REVIEWED
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
using BotTraversal.Core;

namespace BotTraversal.Core
{
    internal static partial class FollowerTraversalTestRuntime
    {
        private static bool TryGetLearningV375SequenceArc(
            FollowerRouteMission mission, int sequence, out float routeArc, out Vector3 point)
        {
            routeArc = 0f;
            point = Vector3.zero;
            if (mission?.RouteBreadcrumbs == null || mission.RouteBreadcrumbs.Count == 0 || sequence <= 0)
            {
                return false;
            }
            List<PlayerBreadcrumbNode> nodes = mission.RouteBreadcrumbs
                .Where(node => node != null && IsFinite(node.Position))
                .OrderBy(node => node.Sequence)
                .ToList();
            if (nodes.Count == 0)
            {
                return false;
            }
            float cumulative = 0f;
            for (int index = 0; index < nodes.Count; index++)
            {
                if (nodes[index].Sequence >= sequence)
                {
                    point = nodes[index].Position;
                    return true;
                }
                if (index + 1 < nodes.Count)
                {
                    Vector3 delta = nodes[index + 1].Position - nodes[index].Position;
                    delta.y = 0f;
                    cumulative += delta.magnitude;
                    routeArc = cumulative;
                }
            }
            point = nodes[nodes.Count - 1].Position;
            return true;
        }

        private static bool TryGetLearningV375RouteLookAheadTarget(
            FollowerRouteMission mission,
            float bodyArc,
            out Vector3 target,
            out Vector3 tangent,
            out float targetArc)
        {
            target = Vector3.zero;
            tangent = Vector3.zero;
            targetArc = bodyArc;
            if (mission?.RouteBreadcrumbs == null || mission.RouteBreadcrumbs.Count < 2)
            {
                return false;
            }
            List<PlayerBreadcrumbNode> nodes = mission.RouteBreadcrumbs
                .Where(node => node != null && IsFinite(node.Position))
                .OrderBy(node => node.Sequence)
                .ToList();
            if (nodes.Count < 2)
            {
                return false;
            }
            float cumulative = 0f;
            float desiredArc = bodyArc + RouteMissionBreadcrumbReachDistance;
            for (int index = 0; index < nodes.Count - 1; index++)
            {
                Vector3 a = nodes[index].Position;
                Vector3 b = nodes[index + 1].Position;
                Vector3 flatSegment = b - a;
                flatSegment.y = 0f;
                float length = flatSegment.magnitude;
                if (length <= 0.001f)
                {
                    continue;
                }
                if (cumulative + length >= desiredArc)
                {
                    float local = Mathf.Clamp(desiredArc - cumulative, 0f, length);
                    float t = local / length;
                    target = Vector3.Lerp(a, b, t);
                    tangent = flatSegment / length;
                    targetArc = cumulative + local;
                    return true;
                }
                cumulative += length;
            }
            Vector3 lastDelta = nodes[nodes.Count - 1].Position - nodes[nodes.Count - 2].Position;
            lastDelta.y = 0f;
            target = nodes[nodes.Count - 1].Position;
            tangent = lastDelta.sqrMagnitude > 0.001f ? lastDelta.normalized : Vector3.zero;
            targetArc = cumulative;
            return tangent.sqrMagnitude > 0.25f;
        }

        private static bool TryGetLearningV405RoutePointAtArc(
            FollowerRouteMission mission,
            float desiredArc,
            out Vector3 point,
            out Vector3 tangent,
            out float resolvedArc)
        {
            point = Vector3.zero;
            tangent = Vector3.zero;
            resolvedArc = 0f;
            if ((mission?.RouteBreadcrumbs == null) || mission.RouteBreadcrumbs.Count < 2 ||
                float.IsNaN(desiredArc) || float.IsInfinity(desiredArc))
            {
                return false;
            }

            List<PlayerBreadcrumbNode> nodes = mission.RouteBreadcrumbs
                .Where(node => node != null && IsFinite(node.Position))
                .OrderBy(node => node.Sequence)
                .ToList();
            if (nodes.Count < 2)
            {
                return false;
            }

            float clampedArc = Mathf.Max(0f, desiredArc);
            float cumulative = 0f;
            for (int index = 0; index < nodes.Count - 1; index++)
            {
                Vector3 a = nodes[index].Position;
                Vector3 b = nodes[index + 1].Position;
                Vector3 flat = b - a;
                flat.y = 0f;
                float length = flat.magnitude;
                if (length <= 0.001f)
                {
                    continue;
                }
                if (cumulative + length >= clampedArc)
                {
                    float local = Mathf.Clamp(clampedArc - cumulative, 0f, length);
                    float t = local / length;
                    point = Vector3.Lerp(a, b, t);
                    tangent = flat / length;
                    resolvedArc = cumulative + local;
                    return true;
                }
                cumulative += length;
            }

            Vector3 lastDelta = nodes[nodes.Count - 1].Position - nodes[nodes.Count - 2].Position;
            lastDelta.y = 0f;
            if (lastDelta.sqrMagnitude <= 0.001f)
            {
                return false;
            }
            point = nodes[nodes.Count - 1].Position;
            tangent = lastDelta.normalized;
            resolvedArc = cumulative;
            return true;
        }

        private static bool TryCapLearningV405NonActuatorTargetAtCurrentStepExit(
            FollowerRouteMission mission,
            FollowerRouteMissionStep step,
            LearningV354TraversalCapabilityProfile profile,
            float bodyArc,
            ref Vector3 target,
            ref Vector3 tangent,
            ref float targetArc,
            out float originalTargetArc,
            out float stepExitArc,
            out float cappedTargetArc)
        {
            originalTargetArc = targetArc;
            stepExitArc = -1f;
            cappedTargetArc = targetArc;
            if ((mission == null) ||
                step == null || profile == null || profile.RequiresActuator ||
                !profile.PhysicalPassageCanProveCompletion ||
                mission.CurrentStepIndex < 0 || mission.CurrentStepIndex >= mission.Steps.Count ||
                mission.Steps[mission.CurrentStepIndex] != step)
            {
                return false;
            }

            Vector3 exit = IsFinite(step.Exit) ? step.Exit : step.RawExit;
            bool haveExitArc = IsFinite(exit) &&
                TryProjectLearningV375RouteArc(
                    mission, exit, out stepExitArc, out _, out _, out _);
            if (!haveExitArc && step.EndBreadcrumbSequence > 0)
            {
                haveExitArc = TryGetLearningV375SequenceArc(
                    mission, step.EndBreadcrumbSequence, out stepExitArc, out _);
            }
            if (!haveExitArc || stepExitArc < 0f)
            {
                return false;
            }

            float exitProofArc = stepExitArc + PostTraversalRollbackSideMargin;
            // Never send the body backward when it has already reached/passed the proof boundary.
            // In that case hold at the current projected body epoch while completion truth catches up.
            float desiredArc = Mathf.Min(targetArc, Mathf.Max(bodyArc, exitProofArc));
            if (targetArc <= desiredArc + 0.001f)
            {
                cappedTargetArc = targetArc;
                return false;
            }

            if (!TryGetLearningV405RoutePointAtArc(
                    mission, desiredArc, out Vector3 cappedPoint,
                    out Vector3 cappedTangent, out float resolvedArc))
            {
                return false;
            }

            target = cappedPoint;
            if (cappedTangent.sqrMagnitude > 0.25f)
            {
                tangent = cappedTangent;
            }
            targetArc = resolvedArc;
            cappedTargetArc = resolvedArc;
            return true;
        }
    }
}
