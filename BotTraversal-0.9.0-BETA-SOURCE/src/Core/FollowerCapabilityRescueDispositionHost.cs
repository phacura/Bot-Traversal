using System;
using EFT;
using UnityEngine;

namespace BotTraversal.Core
{
    internal static partial class FollowerTraversalTestRuntime
    {
        // Semantic probe birth identity stays immutable. A V433 strategy in the global
        // slot is a separate physical candidate, not a replacement lifecycle receipt.
        private static bool TryHandleCapabilityRescueTerminal(ProbeRuntime probe, bool verified, string reason)
        {
            if (verified || !IsProbeLifecycleCurrent(probe) || probe.LifecycleMission == null ||
                probe.LifecycleStep == null || !probe.LifecycleStep.Lifecycle.DebtOutstanding ||
                !LearnedTraversalCommitments.TryGetValue(probe.Bot.ProfileId, out var rescue) ||
                !IsLearningV433ExactLocalActuatorSubstitution(rescue) ||
                rescue.RouteMissionId != probe.Lifecycle.Origin.Mission ||
                rescue.RouteMissionStepIndex != probe.Lifecycle.Origin.Step)
                return false;

            // No completed semantic passage certificate: use the existing explicit-failure
            // transaction, with its root rear staging point, fixed path budget and replay cap.
            // If a higher-priority owner prevents admission, keep this probe attached and retry
            // the terminal decision later. Never fall through to StopMove/GoToPoint/release.
            string source = "rescueTerminalUnproven:" + (reason ?? "unknown");
            if (!TryRequestCapabilityFullRouteRetry(probe, source, true))
            {
                if (ShouldEmitDiagnostic("capabilityRescueTerminalDeferred", probe.Bot, .5f))
                    WriteEvent("capabilityRescueTerminalDeferred", probe.Bot, new {
                        missionId = probe.Lifecycle.Origin.Mission, stepIndex = probe.Lifecycle.Origin.Step,
                        semanticEdge = probe.LearnedEdgeId, substituteEdge = rescue.EdgeId,
                        reason = source, probeRetained = true, nativeSuccessRecorded = false });
                return true;
            }

            TryCommitLearningV433FailedPhysicalActuatorEpoch(probe, source);
            WriteEvent("capabilityRescueTerminalFullRouteRetry", probe.Bot, new {
                missionId = probe.Lifecycle.Origin.Mission, stepIndex = probe.Lifecycle.Origin.Step,
                semanticEdge = probe.LearnedEdgeId, substituteEdge = rescue.EdgeId,
                body = Vec(probe.Player.Position), reason = source,
                nativeSuccessRecorded = false, semanticDebtRetained = true,
                oldEntryReacquisitionAllowed = false });
            return true; // Existing retry tick retires the failed grounded probe.
        }

        private static void RetireCapabilityRescueCommitmentForRetry(
            BotOwner bot, FollowerRouteMission mission, string reason)
        {
            if (bot == null || mission == null || !IsCapabilityRouteRetryReturning(mission) ||
                !LearnedTraversalCommitments.TryGetValue(bot.ProfileId, out var rescue) ||
                !IsLearningV433ExactLocalActuatorSubstitution(rescue) ||
                rescue.RouteMissionId != mission.MissionId ||
                rescue.RouteMissionStepIndex != mission.CurrentStepIndex)
                return;

            // Retry has already accepted this exact live mission. Do not clear by the original
            // Jump edge: that would miss its Vault substitute. No unrelated/newer slot is touched.
            LearnedTraversalCommitments.Remove(bot.ProfileId);
            WriteEvent("capabilityRescueCommitmentRetiredForRetry", bot, new {
                missionId = mission.MissionId, stepIndex = mission.CurrentStepIndex,
                substituteEdge = rescue.EdgeId, reason = reason ?? string.Empty,
                nativeSuccessRecorded = false, persistentLearningChanged = false });
        }
    }
}
