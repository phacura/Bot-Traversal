using System;
using System.Globalization;
using EFT;
using UnityEngine;

namespace BotTraversal.Core
{
    // V2 A3.2: shared, stateless landing candidate truth.  Candidate eligibility
    // is NOT an actuator receipt, stable landing proof, successor admission,
    // physical passage receipt, or route completion.  No writer or global state is created here; RC76 adds one narrowly
    // bounded gap witness predicate. Every caller must retain the
    // original independent timed Native completion and downstream step gates.
    internal static partial class FollowerTraversalTestRuntime
    {
        // V2 A3.3.1: a common observed-body receipt, independent of ingress
        // backend. Do NOT confuse an issued command, selected target, or
        // PathComplete with body progress. No movement is issued here.
        private static bool TryClassifyV2BodyProgressReceipt(
            Vector3 priorAnchor, Vector3 measuredBody, float minPlanarProgress,
            out float observedPlanarDisplacement)
        {
            Vector3 delta = measuredBody - priorAnchor;
            delta.y = 0f;
            observedPlanarDisplacement = delta.magnitude;
            return observedPlanarDisplacement >= minPlanarProgress;
        }

        private static void ObserveV2CurrentStepPhysicalProgressReceipt(
            BotOwner bot, FollowerRouteMission mission, FollowerRouteMissionStep step,
            Vector3 selectedTarget, string selectedProducer,
            bool collisionActiveWritePublished, bool navMeshFunnelSelected)
        {
            if (!TryValidateV2CurrentTypedStep(bot, mission, step, true, out _) ||
                !IsFinite(bot.Position) || !IsFinite(selectedTarget))
                return;

            float now = Time.time;
            Vector3 body = bot.Position;
            if (!step.V2PhysicalReceiptHasAnchor)
            {
                step.V2PhysicalReceiptHasAnchor = true;
                step.V2PhysicalReceiptAnchor = body;
                step.V2PhysicalReceiptAnchorAt = now;
                step.V2PhysicalReceiptLastProgressAt = now;
                return;
            }

            bool physicallyMoved = TryClassifyV2BodyProgressReceipt(
                step.V2PhysicalReceiptAnchor, body, 0.10f,
                out float measuredDisplacement);
            if (physicallyMoved)
            {
                step.V2PhysicalReceiptAnchor = body;
                step.V2PhysicalReceiptAnchorAt = now;
                step.V2PhysicalReceiptLastProgressAt = now;
            }
            float noProgressFor = Mathf.Max(0f, now - step.V2PhysicalReceiptLastProgressAt);
            bool bodyStalled = noProgressFor >= 1.00f;
            string eventName = bodyStalled
                ? "v2rc75PreNativePhysicalProgressStalled"
                : "v2rc75PreNativePhysicalProgressReceipt";
            if (!ShouldEmitDiagnostic(
                    eventName + ":" + mission.MissionId.ToString(CultureInfo.InvariantCulture) +
                    ":" + step.EpisodeId.ToString(CultureInfo.InvariantCulture),
                    bot, bodyStalled ? 0.65f : 0.95f))
                return;
            long generation = FollowerMovementWriterAuthority.GetCurrentGeneration(
                bot, out FollowerMovementWriterAuthority.WriterDomain owner);
            Player player = bot.GetPlayer ?? bot.AIData?.Player;
            WriteEvent(eventName, bot, new
            {
                missionId = mission.MissionId,
                stepIndex = mission.CurrentStepIndex,
                episodeId = step.EpisodeId,
                edgeId = step.EdgeId,
                kind = step.Kind,
                body = Vec(body),
                lastObservedAnchor = Vec(step.V2PhysicalReceiptAnchor),
                selectedTarget = Vec(selectedTarget),
                selectedProducer,
                collisionActiveWritePublished,
                navMeshFunnelSelected,
                measuredDisplacement,
                physicallyMoved,
                noProgressFor,
                bodyStalled,
                owner = owner.ToString(), generation,
                state = player == null ? "Unavailable" : player.CurrentStateName.ToString(),
                moverSprinting = bot.Mover?.Sprinting == true,
                actualGrounded = player?.MovementContext?.IsGrounded == true,
                nativeAttemptRecorded = false,
                nativeSuccessRecorded = false,
                bodyRelocated = false,
                action = "nextFrameCurrentStepBodyProgressReceiptCannotGrantOrRetireTraversalDebt"
            });
        }

        // Shared conservative temporal witness. A false IsGrounded sample is
        // NEVER sufficient for Native landing. This only protects the previous
        // candidate from one short isolated sample gap, with unchanged corridor,
        // NavMesh and downstream evidence, after an actual grounded sample.
        private static bool TryClassifyV2StationaryLandingSupportFlicker(
            string blocker, bool sawAirborne, bool sawDescent, bool sawFarSide,
            bool navStable, bool downstreamConnected, bool corridorQualified,
            Vector3 measuredBody, Vector3 previousSupportedBody,
            float absoluteVerticalSpeed, float sinceLastGrounded)
        {
            if (!string.Equals(blocker, "missingPhysicalLandingSupport", StringComparison.Ordinal) ||
                !sawAirborne || !sawDescent || !sawFarSide ||
                !navStable || !downstreamConnected || !corridorQualified ||
                !IsFinite(measuredBody) || !IsFinite(previousSupportedBody) ||
                !IsFinite(absoluteVerticalSpeed) || !IsFinite(sinceLastGrounded) ||
                sinceLastGrounded < 0f || sinceLastGrounded > 0.105f ||
                absoluteVerticalSpeed > 0.06f ||
                Mathf.Abs(measuredBody.y - previousSupportedBody.y) > 0.035f)
                return false;
            Vector3 offset = measuredBody - previousSupportedBody;
            offset.y = 0f;
            return offset.sqrMagnitude <= 0.08f * 0.08f;
        }

        private static bool TryClassifyV2DropDownLandingCandidate(
            bool sawAirborne, bool sawDescent, bool sawFarSide,
            bool physicalSupport, bool navStable, bool downstreamConnected,
            bool landingCorridorQualified, out string firstMissingEvidence)
        {
            if (!sawAirborne) firstMissingEvidence = "missingAirborneWitness";
            else if (!sawDescent) firstMissingEvidence = "missingDescentWitness";
            else if (!sawFarSide) firstMissingEvidence = "missingFarSideWitness";
            else if (!physicalSupport) firstMissingEvidence = "missingPhysicalLandingSupport";
            else if (!navStable) firstMissingEvidence = "missingStableNavMeshContact";
            else if (!downstreamConnected) firstMissingEvidence = "missingDownstreamConnectivity";
            else if (!landingCorridorQualified) firstMissingEvidence = "outsideLandingCorridor";
            else firstMissingEvidence = string.Empty;
            return firstMissingEvidence.Length == 0;
        }
    }
}
