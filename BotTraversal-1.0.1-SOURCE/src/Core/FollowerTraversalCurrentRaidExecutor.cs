using System;
using UnityEngine;

namespace BotTraversal.Core
{
    internal static partial class FollowerTraversalTestRuntime
    {
        private static FollowerCurrentRaidExecutorPolicy.Admission TryDispatchV2CurrentRaidDropDown(
            ProbeRuntime probe, out string failure)
        {
            failure = string.Empty;
            if (probe?.Bot == null || probe.Player?.MovementContext == null ||
                !string.Equals(probe.RequestedTraversalKind, "DropDown", StringComparison.OrdinalIgnoreCase) ||
                IsPlayerValidatedTraversalProbe(probe))
                return FollowerCurrentRaidExecutorPolicy.Admission.PreserveExisting;
            if (!TryResolveLearningV376MissionForProbe(probe,
                    out FollowerRouteMission mission, out int index) || mission?.Steps == null ||
                index < 0 || index >= mission.Steps.Count)
                return FollowerCurrentRaidExecutorPolicy.Admission.PreserveExisting;
            FollowerRouteMissionStep step = mission.Steps[index];
            LearnedTraversalCommitment origin = probe.FrozenTraversalCommitment;
            bool stepMatches = TryValidateV2CurrentTypedStep(probe.Bot, mission, step, false, out _) &&
                index == mission.CurrentStepIndex && Time.time <= mission.ExpiresAt && IsActiveProbe(probe) &&
                string.Equals(step.Kind, "DropDown", StringComparison.OrdinalIgnoreCase) &&
                origin != null && string.Equals(origin.Kind, "DropDown", StringComparison.OrdinalIgnoreCase) &&
                probe.TraversalEpisodeId == origin.EpisodeId &&
                probe.TraversalChainId == origin.ChainId && probe.LearnedEdgeId == origin.EdgeId;
            long generation = FollowerMovementWriterAuthority.GetCurrentGeneration(
                probe.Bot, out FollowerMovementWriterAuthority.WriterDomain owner);
            var originIdentity = new FollowerCurrentRaidExecutorPolicy.Identity(
                probe.Bot.ProfileId, probe.Generation, origin?.RouteMissionId ?? 0, origin?.RouteMissionStepIndex ?? -1,
                origin?.EpisodeId ?? 0, origin?.EdgeId ?? 0, origin?.ChainId ?? 0,
                probe.MovementWriterGeneration);
            var currentIdentity = new FollowerCurrentRaidExecutorPolicy.Identity(
                probe.Bot.ProfileId, raidGeneration, mission.MissionId, mission.CurrentStepIndex,
                step?.EpisodeId ?? 0, step?.EdgeId ?? 0, step?.ChainId ?? 0, generation);
            bool retired = step != null && IsLearningV446PhysicalStrategyRetiredForCurrentStep(
                mission, step, "DropDown");
            bool finiteGeometry = origin != null && IsFinite(origin.RawEntry) && IsFinite(origin.RawExit) &&
                GetFlatDirection(origin.RawExit - origin.RawEntry).sqrMagnitude > 0.5f;
            FollowerCurrentRaidExecutorPolicy.Admission admission = FollowerCurrentRaidExecutorPolicy.Select(
                true, false, step?.CurrentRaidRouteAuthority == true, retired, stepMatches,
                FollowerCurrentRaidExecutorPolicy.Matches(originIdentity, currentIdentity),
                owner == FollowerMovementWriterAuthority.WriterDomain.Traversal, finiteGeometry);
            if (admission == FollowerCurrentRaidExecutorPolicy.Admission.PreserveExisting)
                return admission;
            WriteEvent("v2rc80CurrentRaidExecutorAdmission", probe.Bot, new
            {
                edgeId = probe.LearnedEdgeId, episodeId = probe.TraversalEpisodeId,
                chainId = probe.TraversalChainId, originMissionId = origin?.RouteMissionId ?? 0,
                currentMissionId = mission.MissionId, originStepIndex = origin?.RouteMissionStepIndex ?? -1,
                currentStepIndex = mission.CurrentStepIndex, originGeneration = probe.MovementWriterGeneration,
                currentGeneration = generation, admission = admission.ToString(), stepMatches,
                primaryRetired = retired, finiteGeometry, persistentValidationChanged = false,
                nativeSuccessRecorded = false, bodyRelocated = false,
                action = "currentRaidExactStepChoosesCompatibleExecutorBeforeGenericVaultScanner"
            });
            if (admission == FollowerCurrentRaidExecutorPolicy.Admission.Reject)
            {
                failure = "current-raid DropDown origin/step/ticket/geometry mismatch";
                return admission;
            }
            if (!FollowerMovementWriterAuthority.TryEnterBoundPhysicalMotion(
                    probe.Bot, FollowerMovementWriterAuthority.WriterDomain.Traversal,
                    probe.MovementWriterGeneration,
                    out FollowerMovementWriterAuthority.WriteScope scope, out failure))
                return FollowerCurrentRaidExecutorPolicy.Admission.Reject;
            using (scope)
            {
                // This changes only the current probe's executor selection.
                // Entry, already-crossed and landing proofs remain the existing gates.
                probe.NativeStrategySelected = true;
                probe.AssistedStrategySelected = false;
                return TryBeginNativeDropDown(probe, origin,
                    "v2rc80CurrentRaidExactDropDownExecutor", out failure)
                    ? FollowerCurrentRaidExecutorPolicy.Admission.NativeDropDown
                    : FollowerCurrentRaidExecutorPolicy.Admission.Reject;
            }
        }
    }
}

