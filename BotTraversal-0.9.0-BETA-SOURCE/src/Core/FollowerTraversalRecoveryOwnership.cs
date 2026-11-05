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
    // RC61: mechanical extraction of RC60 methods into the SAME partial runtime.
    // No new ownership, clock, writer, recovery loop, execution success or persisted state.
    internal static partial class FollowerTraversalTestRuntime
    {
        private static bool TryCF54HandoffTerminalRecoveryWriter(
            BotOwner bot, FollowerMovementRecoveryState state,
            FollowerRouteMission mission, string source)
        {
            if (bot == null || state == null || mission == null ||
                !state.LearningV350TerminalRecoveryEligible ||
                mission.Steps == null || mission.CurrentStepIndex < 0 ||
                mission.CurrentStepIndex >= mission.Steps.Count ||
                HasActiveTraversalProbe(bot) || IsTeleportTransactionActive(bot))
            {
                return false;
            }

            if (!MatchLifecycleRecovery(state,mission)) return false;
            FollowerRouteMissionStep stalled = mission.Steps[mission.CurrentStepIndex];
            if (stalled == null || stalled.EpisodeId <= 0 || stalled.EdgeId <= 0 ||
                stalled.EpisodeId != state.LearningV385TerminalRecoveryExecutionEpisodeId ||
                stalled.EdgeId != state.LearningV385TerminalRecoveryExecutionEdgeId ||
                stalled.StartBreadcrumbSequence != state.LearningV385TerminalRecoveryExecutionStartBreadcrumbSequence ||
                stalled.EndBreadcrumbSequence != state.LearningV385TerminalRecoveryExecutionEndBreadcrumbSequence)
            {
                return false;
            }

            long generation = FollowerMovementWriterAuthority.GetCurrentGeneration(
                bot, out FollowerMovementWriterAuthority.WriterDomain owner);
            // V2-RC37: one classified lifecycle decision, not three independent
            // boolean branches that can drift when new Recovery producers arrive.
            // Live transaction fields are observed (never minted) before disposition.
            FollowerTraversalRouteCursors.TryGetValue(
                bot.ProfileId ?? string.Empty, out FollowerTraversalRouteCursor recoveryCursor);
            FollowerMovementV2IntentGateway.TerminalRecoveryWriterDisposition disposition =
                FollowerMovementV2IntentGateway.ResolveTerminalRecoveryWriterDisposition(
                    state.V2TerminalRecoveryWriterYieldReceipt,
                    mission.MissionId, mission.CurrentStepIndex,
                    stalled.EpisodeId, stalled.EdgeId,
                    state.LearningV385TerminalRecoveryExecutionEpisodeId,
                    state.LearningV385TerminalRecoveryExecutionEdgeId,
                    stalled.StartBreadcrumbSequence, stalled.EndBreadcrumbSequence,
                    state.LearningV385TerminalRecoveryExecutionStartBreadcrumbSequence,
                    state.LearningV385TerminalRecoveryExecutionEndBreadcrumbSequence,
                    recoveryCursor != null && recoveryCursor.LearningV375TransactionActive,
                    recoveryCursor?.LearningV375RouteMissionId ?? 0,
                    recoveryCursor?.LearningV375MovementGeneration ?? 0,
                    owner, generation);
            if (disposition == FollowerMovementV2IntentGateway.TerminalRecoveryWriterDisposition.AlreadyYielded)
            {
                return true;
            }
            if (disposition == FollowerMovementV2IntentGateway.TerminalRecoveryWriterDisposition.FollowAlreadyOwns)
            {
                if (ShouldEmitDiagnostic("rc36FollowOwnedTerminalRecoveryNoHandoff", bot, 3.0f))
                {
                    WriteEvent("followerTraversalRecoveryFollowAlreadyOwnerNoHandoff", bot, new
                    {
                        missionId = mission.MissionId,
                        stepIndex = mission.CurrentStepIndex,
                        episodeId = stalled.EpisodeId,
                        edgeId = stalled.EdgeId,
                        currentOwner = owner.ToString(),
                        currentGeneration = generation,
                        routeTransactionActive = false,
                        writerTransitionPerformed = false,
                        yieldReceiptMinted = false,
                        routeStepAdvanced = false,
                        nativeSuccessRecorded = false,
                        recoveryStateCleared = false,
                        action = "followOwnsRecoveryNoTraversalWriterToYieldPreserveUnconsumedDebt"
                    });
                }
                return true;
            }
            bool recoveryWriterAdmitted =
                disposition == FollowerMovementV2IntentGateway.TerminalRecoveryWriterDisposition.TransferTraversal;
            if (!recoveryWriterAdmitted)
            {
                if (ShouldEmitDiagnostic("rc31RecoveryWriterEpochMismatch", bot, 0.50f))
                {
                    WriteEvent("followerTraversalRecoveryWriterHandoffRejected", bot, new
                    {
                        missionId = mission.MissionId,
                        stepIndex = mission.CurrentStepIndex,
                        episodeId = stalled.EpisodeId,
                        edgeId = stalled.EdgeId,
                        recoveryEpisodeId = state.LearningV385TerminalRecoveryExecutionEpisodeId,
                        recoveryEdgeId = state.LearningV385TerminalRecoveryExecutionEdgeId,
                        routeTransactionActive = recoveryCursor != null && recoveryCursor.LearningV375TransactionActive,
                        transactionMissionId = recoveryCursor?.LearningV375RouteMissionId ?? 0,
                        transactionGeneration = recoveryCursor?.LearningV375MovementGeneration ?? 0,
                        currentGeneration = generation,
                        currentOwner = owner.ToString(),
                        routeStepAdvanced = false,
                        nativeSuccessRecorded = false,
                        action = "staleRecoveryCannotRetireAnotherRouteWriterGeneration"
                    });
                }
                return false;
            }

            // The old continuous producer must unpause its own mover before the exact
            // generation changes; no Follow/Teleport/Combat epoch may be overridden.
            ClearLearningV365ContinuousPhysicalRoute(bot, "cf54ExactTerminalRecoveryWriterYield");
            FollowerMovementWriterAuthority.TransitionTraversalToFollowIfCurrent(
                bot, generation, "cf54ExactTerminalRecoveryWriterYield");
            long newGeneration = FollowerMovementWriterAuthority.GetCurrentGeneration(
                bot, out FollowerMovementWriterAuthority.WriterDomain newOwner);
            bool transferred = newGeneration > generation &&
                newOwner == FollowerMovementWriterAuthority.WriterDomain.Follow;
            // Store provenance only AFTER an observed successful handoff. Do not
            // manufacture a receipt when a producer fails to release its writer.
            if (transferred)
            {
                state.V2TerminalRecoveryWriterYieldReceipt =
                    new FollowerMovementV2IntentGateway.TerminalRecoveryWriterYieldReceipt(
                        mission.MissionId, mission.CurrentStepIndex,
                        stalled.EpisodeId, stalled.EdgeId, generation, newGeneration);
            }
            WriteEvent("cf54ExactTerminalRecoveryWriterHandoff", bot, new
            {
                missionId = mission.MissionId,
                stepIndex = mission.CurrentStepIndex,
                episodeId = stalled.EpisodeId,
                edgeId = stalled.EdgeId,
                kind = stalled.Kind,
                oldGeneration = generation,
                newGeneration,
                transferred,
                source = source ?? string.Empty,
                traversalSuccessRecorded = false,
                persistentLearningChanged = false,
                action = "expiredActuatorExecutionDebtYieldsExclusiveTraversalWriterToFollowRecoveryWithoutClearingDebt"
            });
            return transferred;
        }

        private static bool ReconcileLearningV354RouteMissionAfterRecoveryTeleport(BotOwner bot, FollowerRouteMission mission, out string disposition)
        {
            disposition = "notApplicable";
            if ((bot == null) || mission == null || mission.Steps == null || mission.Steps.Count == 0)
            {
                return false;
            }
            int previousStep = mission.CurrentStepIndex;
            var r2DebtObservation1 = CaptureRouteDebtObservation(bot, mission, null, true);

            string profileId = bot.ProfileId ?? string.Empty;
            int previousExecutionFloor = GetFollowerRouteExecutionFloor(profileId);
            int chronologyFloor = Math.Max(mission.PhysicalApproachBreadcrumbSequence, mission.ResumeFloorBreadcrumbSequence);
            PlayerBreadcrumbNode nearest = mission.RouteBreadcrumbs
                .Where(node => node != null && node.Sequence >= chronologyFloor && IsOperationalBreadcrumbNode(node))
                .OrderBy(node => GetPlanarDistance(bot.Position, GetOperationalBreadcrumbTarget(node)))
                .ThenByDescending(node => node.Sequence)
                .FirstOrDefault();
            if (nearest == null)
            {
                disposition = "noRecoveryBreadcrumb";
                return false;
            }
            float nearestDistance = GetPlanarDistance(bot.Position, GetOperationalBreadcrumbTarget(nearest));
            if (nearestDistance > RouteMissionRebaseMaxDistance * 1.5f)
            {
                disposition = "recoveryOutsideRouteEnvelope";
                return false;
            }
            int newStep = previousStep;
            while (newStep < mission.Steps.Count)
            {
                FollowerRouteMissionStep step = mission.Steps[newStep];
                if (step == null) { newStep++; continue; }
                if (step.StartBreadcrumbSequence > nearest.Sequence) break;
                if (step.EndBreadcrumbSequence > nearest.Sequence)
                {
                    float localEntryDistance = GetPlanarDistance(bot.Position, step.Entry);
                    if (localEntryDistance <= RouteMissionTraversalAdjacentBridgeMaxDistance) break;
                    // Recovery physically relocated downstream of this stale entry. Retire execution debt
                    // only; do not grant traversal success or mutate persistent skill evidence.
                }
                newStep++;
            }
            mission.PhysicalApproachBreadcrumbSequence = Math.Max(mission.PhysicalApproachBreadcrumbSequence, nearest.Sequence);
            mission.ApproachBreadcrumbSequence = Math.Max(mission.ApproachBreadcrumbSequence, nearest.Sequence);
            mission.ObservedDownstreamBreadcrumbSequence = Math.Max(mission.ObservedDownstreamBreadcrumbSequence, nearest.Sequence);
            CommitCapabilityMoveRetry(mission,FollowerCapabilityRetryPolicy.Change.Reset,0f);
            mission.TraversalRetryMovementHoldUntil = 0f;
            mission.LastProgressAt = Time.time;
            mission.LastProgressPosition = bot.Position;
            if (newStep != previousStep)
            {
                int recoveryExecutionFloor = previousExecutionFloor;
                for (int i = previousStep; i < Math.Min(newStep, mission.Steps.Count); i++)
                {
                    FollowerRouteMissionStep retired = mission.Steps[i];
                    if (retired != null)
                    {
                        RecordLearningV354ActionOutcome(retired.Kind, false, true, "recoveryFrontierRetiredExecutionDebt");
                        {
                            recoveryExecutionFloor = Math.Max(
                                recoveryExecutionFloor,
                                retired.EndBreadcrumbSequence);
                        }
                    }
                }
                if ((!string.IsNullOrEmpty(profileId)) &&
                    recoveryExecutionFloor > previousExecutionFloor)
                {
                    FollowerRouteExecutionFloors[profileId] = recoveryExecutionFloor;
                    mission.ResumeFloorBreadcrumbSequence = Math.Max(
                        mission.ResumeFloorBreadcrumbSequence,
                        recoveryExecutionFloor);
                    WriteEvent("learningV356RecoveryExecutionFloorAdvanced", bot, new
                    {
                        missionId = mission.MissionId,
                        previousStepIndex = previousStep,
                        newStepIndex = newStep,
                        previousExecutionFloor,
                        newExecutionFloor = recoveryExecutionFloor,
                        routeSemanticCursorAdvanced = false,
                        traversalSuccessRecorded = false,
                        action = "persistRetiredExecutionDebtAcrossMissionRecreation"
                    });
                }
                if (!TryAdvanceRouteMission(bot, mission, newStep, RouteDebtEvidence.TeleportRecovery, "ReconcileLearningV354RouteMissionAfterRecoveryTeleport:1", r2DebtObservation1)) return false;
                mission.LearningV355ProgressStepIndex = -1;
                mission.LearningV355BestEntryDistance = float.MaxValue;
                mission.LearningV355BestRouteProjection = float.MinValue;
                mission.LearningV355WrongWaySince = -1f;
                if (newStep < mission.Steps.Count)
                {
                    FollowerRouteMissionStep next = mission.Steps[newStep];
                    disposition = "reanchoredToStep:" + newStep.ToString(CultureInfo.InvariantCulture);
                    WriteEvent("learningV354RecoveryFrontierReanchored", bot, new
                    {
                        missionId = mission.MissionId, previousStepIndex = previousStep, newStepIndex = newStep,
                        matchedBreadcrumbSequence = nearest.Sequence, matchedDistance = nearestDistance,
                        nextEdgeId = next?.EdgeId ?? 0, nextKind = next?.Kind ?? string.Empty,
                        traversalSuccessRecorded = false, persistentLearningChanged = false
                    });
                }
                else
                {
                    disposition = "allOldExecutionDebtBehindRecoveryFrontier";
                    WriteEvent("learningV354RecoveryFrontierReanchored", bot, new
                    {
                        missionId = mission.MissionId, previousStepIndex = previousStep, newStepIndex = newStep,
                        matchedBreadcrumbSequence = nearest.Sequence, matchedDistance = nearestDistance,
                        traversalSuccessRecorded = false, persistentLearningChanged = false
                    });
                }
                return true;
            }
            disposition = "localRetryRearmed";
            WriteEvent("learningV354RecoveryFrontierLocalRetryRearmed", bot, new
            {
                missionId = mission.MissionId, stepIndex = mission.CurrentStepIndex, matchedBreadcrumbSequence = nearest.Sequence, matchedDistance = nearestDistance
            });
            return true;
        }

        private static bool IsLearningV361RecoveryEdgeCompatibleWithActiveRouteMission(
            BotOwner bot,
            LearnedTraversalEdge edge,
            out int missionId,
            out int stepIndex,
            out int routeEdgeId,
            out string routeKind)
        {
            missionId = 0;
            stepIndex = -1;
            routeEdgeId = 0;
            routeKind = string.Empty;
            if ((bot == null) || edge == null ||
                string.IsNullOrEmpty(bot.ProfileId))
            {
                return true;
            }

            if (!FollowerRouteMissions.TryGetValue(bot.ProfileId, out FollowerRouteMission mission) ||
                mission == null || mission.CurrentStepIndex < 0 ||
                mission.CurrentStepIndex >= mission.Steps.Count)
            {
                return true;
            }

            FollowerRouteMissionStep step = mission.Steps[mission.CurrentStepIndex];
            if (step == null)
            {
                return true;
            }

            missionId = mission.MissionId;
            stepIndex = mission.CurrentStepIndex;
            routeEdgeId = step.EdgeId;
            routeKind = step.Kind ?? string.Empty;

            if (edge.Id == step.EdgeId)
            {
                return true;
            }

            // Canonical/native-proven aliases are allowed only when they represent the SAME
            // semantic action and the same local geometry. Recovery is not allowed to replace an
            // unresolved WT with a nearby Vault/Climb/Jump merely because that portal is proven.
            if (!string.Equals(edge.Kind ?? string.Empty, routeKind, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            Vector3 stepEntry = IsFinite(step.RawEntry) ? step.RawEntry : step.Entry;
            Vector3 stepExit = IsFinite(step.RawExit) ? step.RawExit : step.Exit;
            Vector3 edgeEntry = IsFinite(edge.RawStart) ? edge.RawStart : edge.A;
            Vector3 edgeExit = IsFinite(edge.RawEnd) ? edge.RawEnd : edge.B;
            if (!IsFinite(stepEntry) || !IsFinite(stepExit) ||
                !IsFinite(edgeEntry) || !IsFinite(edgeExit))
            {
                return false;
            }

            bool forwardEquivalent =
                GetPlanarDistance(stepEntry, edgeEntry) <= LearningV2RevisionEndpointRadius &&
                GetPlanarDistance(stepExit, edgeExit) <= LearningV2RevisionEndpointRadius;
            bool reverseEquivalent =
                GetPlanarDistance(stepEntry, edgeExit) <= LearningV2RevisionEndpointRadius &&
                GetPlanarDistance(stepExit, edgeEntry) <= LearningV2RevisionEndpointRadius;
            return forwardEquivalent || reverseEquivalent;
        }

        private static bool TryGetLearningV373RouteLocalPhysicalRecoveryExit(
            LearnedTraversalCommitment commitment,
            out Vector3 requestedExit,
            out string geometrySource)
        {
            requestedExit = default;
            geometrySource = string.Empty;
            if ((commitment == null) ||
                !IsLearningV373PhysicalCompletionBarrierKind(commitment.Kind))
            {
                return false;
            }

            TraversalEpisodeMetadata episode = commitment.EpisodeId > 0
                ? TraversalEpisodeStream.LastOrDefault(item =>
                    item != null && item.EpisodeId == commitment.EpisodeId)
                : null;
            if (episode != null)
            {
                if ((string.Equals(commitment.Kind, "WalkThrough", StringComparison.OrdinalIgnoreCase)) &&
                    episode.LearningV372HasCurrentExecutionGeometry &&
                    IsFinite(episode.LearningV372CurrentExecutionEnd))
                {
                    requestedExit = episode.LearningV372CurrentExecutionEnd;
                    geometrySource = "currentEpisodeWalkThroughExecutionEnd";
                    return true;
                }
                if (IsFinite(episode.RawEnd))
                {
                    requestedExit = episode.RawEnd;
                    geometrySource = "currentEpisodeRawEnd";
                    return true;
                }
                if (IsFinite(episode.Exit))
                {
                    requestedExit = episode.Exit;
                    geometrySource = "currentEpisodeExit";
                    return true;
                }
            }

            if (IsFinite(commitment.RawExit))
            {
                requestedExit = commitment.RawExit;
                geometrySource = "commitmentRawExit";
                return true;
            }
            if (commitment.HasSettledExit && IsFinite(commitment.SettledExit))
            {
                requestedExit = commitment.SettledExit;
                geometrySource = "commitmentSettledExit";
                return true;
            }
            if (IsFinite(commitment.Exit))
            {
                requestedExit = commitment.Exit;
                geometrySource = "commitmentExit";
                return true;
            }
            return false;
        }

        private static bool TryValidateLearningV350PlayerRoutePhysicalRecoveryAnchor(
            BotOwner bot,
            Vector3 requested,
            out Vector3 result)
        {
            result = default;
            Player player = bot?.GetPlayer ?? bot?.AIData?.Player;
            if (bot == null || player == null || !IsFinite(requested))
            {
                return false;
            }

            // V350 deliberately leaves FollowerSafeTeleport byte-identical to the proven baseline.
            // For terminal player-route recovery, validate the authored PHYSICAL standing point
            // here with the same minimum world facts: a nearby floor and a clear follower capsule.
            // Player/bot-owned colliders are ignored through the existing traversal collider filter.
            Vector3 candidate = requested;
            if (!TryGetFirstNonSelfRaycastHit(
                    player,
                    requested + Vector3.up * 0.80f,
                    Vector3.down,
                    1.70f,
                    Physics.AllLayers,
                    out RaycastHit floorHit) ||
                Mathf.Abs(floorHit.point.y - requested.y) > 0.85f)
            {
                return false;
            }

            candidate.y = floorHit.point.y;
            Vector3 bottom = candidate + Vector3.up * 0.38f;
            Vector3 top = candidate + Vector3.up * 1.55f;
            Collider[] overlaps = Physics.OverlapCapsule(
                bottom, top, WalkThroughCapsuleRadius, Physics.AllLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < overlaps.Length; i++)
            {
                Collider collider = overlaps[i];
                if (collider == null || IsIgnoredTraversalCollider(player, collider))
                {
                    continue;
                }
                return false;
            }

            result = candidate;
            return true;
        }

        private static bool TryRetireLearningV373ExhaustedPhysicalDebtByDownstreamBodyEpoch(
            BotOwner bot,
            FollowerRouteMission mission,
            string source,
            out int retiredEdgeId,
            out int retiredEpisodeId)
        {
            retiredEdgeId = 0;
            retiredEpisodeId = 0;
            if (bot == null || mission == null || mission.CurrentStepIndex < 0 ||
                mission.CurrentStepIndex >= mission.Steps.Count)
            {
                return false;
            }
            FollowerRouteMissionStep step = mission.Steps[mission.CurrentStepIndex];
            var r2DebtObservation2 = CaptureRouteDebtObservation(bot, mission, step, false);

            if (!TryProveLearningV373ExhaustedPhysicalDebtDownstreamBodyEpoch(
                    bot, mission, step,
                    out PlayerBreadcrumbNode downstreamNode,
                    out float downstreamDistance,
                    out float downstreamPathLength,
                    out float actionEntryDistance,
                    out float actionExitDistance))
            {
                return false;
            }

            string profileId = bot.ProfileId ?? string.Empty;
            int previousStepIndex = mission.CurrentStepIndex;
            int previousExecutionFloor = GetFollowerRouteExecutionFloor(profileId);
            int newExecutionFloor = Math.Max(previousExecutionFloor, step.EndBreadcrumbSequence);
            FollowerRouteExecutionFloors[profileId] = newExecutionFloor;
            mission.ResumeFloorBreadcrumbSequence = Math.Max(mission.ResumeFloorBreadcrumbSequence, newExecutionFloor);
            mission.PhysicalApproachBreadcrumbSequence = Math.Max(
                mission.PhysicalApproachBreadcrumbSequence, downstreamNode.Sequence);
            mission.ApproachBreadcrumbSequence = Math.Max(mission.ApproachBreadcrumbSequence, downstreamNode.Sequence);
            mission.ObservedDownstreamBreadcrumbSequence = Math.Max(
                mission.ObservedDownstreamBreadcrumbSequence, downstreamNode.Sequence);
            if (!TryAdvanceRouteMission(bot, mission, mission.CurrentStepIndex + 1, RouteDebtEvidence.PhysicalPass, "TryRetireLearningV373ExhaustedPhysicalDebtByDownstreamBodyEpoch:2", r2DebtObservation2)) return false;
            CommitCapabilityMoveRetry(mission,FollowerCapabilityRetryPolicy.Change.Reset,0f);
            mission.TraversalRetryMovementHoldUntil = 0f;
            mission.LocalRecoveryAttempts = 0;
            mission.PathRejectsSinceProgress = 0;
            mission.RejectedBreadcrumbSequences.Clear();
            mission.FailureSignatureCounts.Clear();
            mission.LastProgressAt = Time.time;
            mission.LastProgressPosition = bot.Position;
            mission.LearningV355ProgressStepIndex = -1;
            mission.LearningV336SelfRescueStepIndex = -1;
            mission.LearningV336SelfRescueEdgeId = 0;
            mission.LearningV336SelfRescueAttempts = 0;
            mission.LearningV336SelfRescueForceNativeNextAttempt = false;
            mission.LearningV336SelfRescueForceNativeSource = null;
            mission.LearningV357ExhaustedBarrierStepIndex = -1;
            mission.LearningV357ExhaustedBarrierEdgeId = 0;
            mission.LearningV357ExhaustedBarrierEndBreadcrumbSequence = 0;
            mission.LearningV357ExhaustedBarrierAt = -1f;
            ClearLearnedTraversalCommitment(bot, step.EdgeId,
                "learningV373ExhaustedPhysicalDebtRetiredByDownstreamBodyEpoch");
            NavigationRecoveryTraversalAnchors.Remove(profileId);
            retiredEdgeId = step.EdgeId;
            retiredEpisodeId = step.EpisodeId;

            WriteEvent("learningV373ExhaustedPhysicalDebtRetiredByDownstreamBodyEpoch", bot, new
            {
                missionId = mission.MissionId,
                previousStepIndex,
                currentStepIndex = mission.CurrentStepIndex,
                episodeId = step.EpisodeId,
                edgeId = step.EdgeId,
                kind = step.Kind,
                matchedBreadcrumbSequence = downstreamNode.Sequence,
                downstreamDistance,
                downstreamPathLength,
                actionEntryDistance,
                actionExitDistance,
                previousExecutionFloor,
                newExecutionFloor,
                source = source ?? "unknown",
                traversalSuccessRecorded = false,
                persistentLearningChanged = false,
                semanticAuthorityRetained = true,
                action = "boundedSelfRescueExhaustedAndBodyLocalizedToNewerRouteEpochRetireExecutionDebtOnly"
            });
            return true;
        }

        private static bool TryProveLearningV373ExhaustedPhysicalDebtDownstreamBodyEpoch(
            BotOwner bot,
            FollowerRouteMission mission,
            FollowerRouteMissionStep step,
            out PlayerBreadcrumbNode downstreamNode,
            out float downstreamDistance,
            out float downstreamPathLength,
            out float actionEntryDistance,
            out float actionExitDistance)
        {
            downstreamNode = null;
            downstreamDistance = float.MaxValue;
            downstreamPathLength = 0f;
            actionEntryDistance = float.MaxValue;
            actionExitDistance = float.MaxValue;
            if ((bot == null) || mission == null ||
                step == null || HasActiveTraversalProbe(bot) ||
                !IsLearningV373PhysicalCompletionBarrierKind(step.Kind) ||
                mission.CurrentStepIndex < 0 || mission.CurrentStepIndex >= mission.Steps.Count ||
                mission.Steps[mission.CurrentStepIndex] != step ||
                mission.LearningV357ExhaustedBarrierStepIndex != mission.CurrentStepIndex ||
                mission.LearningV357ExhaustedBarrierEdgeId != step.EdgeId ||
                mission.LearningV357ExhaustedBarrierEndBreadcrumbSequence <= 0 ||
                step.EndBreadcrumbSequence <= 0)
            {
                return false;
            }

            string profileId = bot.ProfileId ?? string.Empty;
            if (LearnedTraversalCommitments.TryGetValue(profileId, out LearnedTraversalCommitment liveCommitment) &&
                liveCommitment != null && liveCommitment.EpisodeId == step.EpisodeId)
            {
                return false;
            }

            Vector3 entry = IsFinite(step.RawEntry) ? step.RawEntry : step.Entry;
            Vector3 exit = IsFinite(step.RawExit) ? step.RawExit : step.Exit;
            if (!IsFinite(entry) || !IsFinite(exit))
            {
                return false;
            }
            actionEntryDistance = Vector3.Distance(bot.Position, entry);
            actionExitDistance = Vector3.Distance(bot.Position, exit);
            float entryVerticalDistance = Mathf.Abs(bot.Position.y - entry.y);
            float exitVerticalDistance = Mathf.Abs(bot.Position.y - exit.y);
            bool entryOperationallyLocal = entryVerticalDistance <= RouteMissionRebaseMaxVertical;
            bool exitOperationallyLocal = exitVerticalDistance <= RouteMissionRebaseMaxVertical;
            float actionLocalDistance = float.MaxValue;
            if (entryOperationallyLocal)
            {
                actionLocalDistance = Mathf.Min(actionLocalDistance, actionEntryDistance);
            }
            if (exitOperationallyLocal)
            {
                actionLocalDistance = Mathf.Min(actionLocalDistance, actionExitDistance);
            }

            RefreshRouteMissionBreadcrumbSnapshot(mission);
            long minimumSequence = Math.Max(
                step.EndBreadcrumbSequence,
                mission.LearningV357ExhaustedBarrierEndBreadcrumbSequence);
            PlayerBreadcrumbNode best = null;
            float bestDirect = float.MaxValue;
            float bestPath = 0f;
            if (mission.RouteBreadcrumbs == null)
            {
                return false;
            }
            foreach (PlayerBreadcrumbNode candidate in mission.RouteBreadcrumbs)
            {
                if (candidate == null || candidate.Sequence <= minimumSequence ||
                    !IsOperationalBreadcrumbNode(candidate))
                {
                    continue;
                }
                Vector3 target = GetOperationalBreadcrumbTarget(candidate);
                if (!IsFinite(target))
                {
                    continue;
                }
                float direct = GetPlanarDistance(bot.Position, target);
                float vertical = Mathf.Abs(bot.Position.y - target.y);
                if (direct <= 0.001f || direct > RouteMissionBreadcrumbLookAheadDistance ||
                    vertical > RouteMissionRebaseMaxVertical)
                {
                    continue;
                }

                // V373: compare operational locality, not raw Euclidean proximity. A follower that
                // has fallen below/above the demonstrated actuator entry by more than the existing
                // route rebase vertical envelope must not be dragged back merely because that stale
                // 3D point is numerically closer than the newer player route. The newer node still
                // has to be after the exhausted action and body-local inside the existing look-ahead.
                bool clearlyMoreLocalThanOldAction =
                    actionLocalDistance < float.MaxValue &&
                    direct + RouteMissionBreadcrumbReachDistance < actionLocalDistance;
                bool entryLostVerticalLocalityAtDownstreamExit =
                    !entryOperationallyLocal && exitOperationallyLocal &&
                    GetPlanarDistance(target, exit) <= RouteMissionBreadcrumbReachDistance;
                if (!clearlyMoreLocalThanOldAction && !entryLostVerticalLocalityAtDownstreamExit)
                {
                    continue;
                }

                StrictPathResult strict = CalculateStrictPath(bot.Position, target);
                float allowed = GetAllowedFollowPathLength(Mathf.Max(0.25f, direct), true);
                bool strictRouteLocal = strict != null && strict.StrictComplete &&
                    strict.PathLength > 0f && strict.PathLength <= allowed;
                LearningV354TraversalCapabilityProfile actionProfile =
                    GetLearningV354ActionProfile(GetLearningV354SemanticKind(step.EdgeId, step.Kind));
                bool physicalRouteLocal = actionProfile != null && actionProfile.CanFinishOffNavMesh &&
                    entryLostVerticalLocalityAtDownstreamExit;
                if (!strictRouteLocal && !physicalRouteLocal)
                {
                    continue;
                }
                if (best == null || direct < bestDirect - 0.001f ||
                    (Mathf.Abs(direct - bestDirect) <= 0.001f && candidate.Sequence > best.Sequence))
                {
                    best = candidate;
                    bestDirect = direct;
                    bestPath = strictRouteLocal && strict != null ? strict.PathLength : 0f;
                }
            }
            if (best == null)
            {
                return false;
            }
            downstreamNode = best;
            downstreamDistance = bestDirect;
            downstreamPathLength = bestPath;
            return true;
        }

        private static bool TryHandoffLearningV419TraversalToFreshFollowConsumer(
            BotOwner bot, long expectedTraversalGeneration, string reason, string pathSource,
            out string detail)
        {
            detail = "unavailable";
            if ((bot?.GetPlayer == null) ||
                bot.Mover == null || expectedTraversalGeneration <= 0)
            {
                return false;
            }

            long beforeGeneration = FollowerMovementWriterAuthority.GetCurrentGeneration(
                bot, out FollowerMovementWriterAuthority.WriterDomain beforeOwner);
            if (beforeOwner != FollowerMovementWriterAuthority.WriterDomain.Traversal ||
                beforeGeneration != expectedTraversalGeneration)
            {
                detail = "notExpectedTraversalEpoch:owner=" + beforeOwner + ":generation=" +
                         beforeGeneration.ToString(CultureInfo.InvariantCulture);
                return false;
            }

            Player leader = FollowerEnvironment.LocalPlayer;
            if (!IsLivePlayer(leader) || !IsFinite(leader.Position) || !IsFinite(bot.GetPlayer.Position))
            {
                detail = "leaderUnavailable";
                return false;
            }

            string source = string.IsNullOrEmpty(pathSource) ? "authority-reacquire" : pathSource;
            if (!IsLeaderFollowPathSource(source))
            {
                source = "authority-reacquire";
            }

            // CF24: V425's native-epoch proof needs the physical body inside the *same*
            // tight NavMesh sampling envelope checked by V364 below.  V423 can receive a
            // broad PathComplete answer while this tight physical admission is still false.
            // Preflight BEFORE retiring topology, arming a barrier or advancing the writer
            // generation.  Do not let an unready Follow repeatedly steal/rollback Traversal
            // authority while the already-live physical continuation remains in control.
            {
                Vector3 cf24Body = bot.GetPlayer.Position;
                NavMeshHit cf24Hit;
                bool cf24Sampled = NavMesh.SamplePosition(
                    cf24Body, out cf24Hit, ExitAuthorityTopologyRejoinSampleRadius, NavMesh.AllAreas);
                float cf24Planar = cf24Sampled
                    ? GetPlanarDistance(cf24Body, cf24Hit.position) : float.MaxValue;
                float cf24Vertical = cf24Sampled
                    ? Mathf.Abs(cf24Body.y - cf24Hit.position.y) : float.MaxValue;
                if (!cf24Sampled ||
                    cf24Planar > ExitAuthorityTopologyRejoinSampleRadius ||
                    cf24Vertical > ExitAuthorityTopologyRejoinMaxVertical)
                {
                    detail = "cf24PhysicalEpochPreflightDeferred:sampled=" + cf24Sampled +
                             ":planar=" + cf24Planar.ToString("0.000", CultureInfo.InvariantCulture) +
                             ":vertical=" + cf24Vertical.ToString("0.000", CultureInfo.InvariantCulture);
                    if (ShouldEmitDiagnostic(
                            "cf24FreshFollowPhysicalEpochPreflightDeferred", bot, 0.08f))
                    {
                        WriteEvent("cf24FreshFollowPhysicalEpochPreflightDeferred", bot, new
                        {
                            reason = reason ?? string.Empty,
                            source,
                            traversalGeneration = expectedTraversalGeneration,
                            body = Vec(cf24Body),
                            tightSampled = cf24Sampled,
                            tightPlanar = cf24Planar,
                            tightVertical = cf24Vertical,
                            allowedPlanar = ExitAuthorityTopologyRejoinSampleRadius,
                            allowedVertical = ExitAuthorityTopologyRejoinMaxVertical,
                            movementOwner = beforeOwner.ToString(),
                            movementGeneration = beforeGeneration,
                            traversalSuccessRecorded = false,
                            persistentLearningChanged = false,
                            bodyRelocated = false,
                            action = "deferFreshFollowUntilV364TightBodyNavMeshAdmissionWithoutOwnershipChurn"
                        });
                    }
                    return false;
                }
            }

            // Phase 1: validate the consumer while Traversal still owns movement. A rejected path
            // cannot cause an owner transition, brake or body relocation.
            if (!CanAcceptFollowPath(bot, leader.Position, source, out float pathLength, out float detourRatio))
            {
                RecordFollowPathSanityRejected(bot, leader.Position, source, pathLength, detourRatio);
                detail = "pathRejected:length=" + pathLength.ToString("0.00", CultureInfo.InvariantCulture) +
                         ":detour=" + detourRatio.ToString("0.00", CultureInfo.InvariantCulture);
                return false;
            }

            bool learningV425EpochArmed = false;
            string learningV425RetireDetail = "notRequested";
            {
                // A consumer epoch is not complete until BotMover's native path/link origin agrees
                // with the authoritative physical body. V424 runtime proved that a PathComplete
                // object inherited from Traversal can still carry PrevSuccessLinkedFrom back to a
                // consumed Jump entry and method_10 will relocate the body there on the very first
                // Follow tick. Retire only stale topology (preserve locomotion), arm V364's existing
                // body-writer barrier at CURRENT body, and let the fresh GoToPoint write below prove
                // that the new Follow path and native bookkeeping share this same epoch.
                TryRetireNativeMoverPathPreserveLocomotion(
                    bot, "learningV425FreshFollowConsumerEpoch:" + (reason ?? "traversalHandoff"),
                    out learningV425RetireDetail);

                Vector3 learningV425Anchor = bot.GetPlayer.Position;
                int learningV425EdgeId = 0;
                int learningV425EpisodeId = 0;
                int learningV425ChainId = 0;
                Vector3 learningV425Entry = new Vector3(float.NaN, float.NaN, float.NaN);
                Vector3 learningV425Exit = new Vector3(float.NaN, float.NaN, float.NaN);
                if (!string.IsNullOrEmpty(bot.ProfileId) &&
                    PostTraversalIntegrityWatches.TryGetValue(
                        bot.ProfileId, out PostTraversalIntegrityWatch learningV425Watch) &&
                    learningV425Watch != null)
                {
                    learningV425EdgeId = learningV425Watch.EdgeId;
                    learningV425EpisodeId = learningV425Watch.EpisodeId;
                    learningV425ChainId = learningV425Watch.CompletedChainId;
                    learningV425Entry = learningV425Watch.Entry;
                    learningV425Exit = learningV425Watch.Exit;
                }
                ArmLearningV364PhysicalNavigationEpochHandoff(
                    bot,
                    learningV425EdgeId,
                    learningV425EpisodeId,
                    learningV425ChainId,
                    learningV425Entry,
                    learningV425Exit,
                    learningV425Anchor,
                    "learningV425FreshFollowConsumerEpoch:" + (reason ?? "traversalHandoff"));
                learningV425EpochArmed =
                    HasLearningV364UnsynchronizedPhysicalNavigationEpoch(
                        bot, out PhysicalNavigationEpochHandoff _);
            }

            // Phase 2: only an already-validated consumer is allowed to take ownership. If the
            // actual native GoToPoint write still fails unexpectedly, roll authority back to a new
            // Traversal epoch at the current physical body and retire the failed native path.
            FollowerMovementWriterAuthority.TransitionTraversalToFollowIfCurrent(
                bot, expectedTraversalGeneration,
                "learningV419FreshConsumer:" + (reason ?? "traversalHandoff"));
            long followGeneration = FollowerMovementWriterAuthority.GetCurrentGeneration(
                bot, out FollowerMovementWriterAuthority.WriterDomain followOwner);
            if (followOwner != FollowerMovementWriterAuthority.WriterDomain.Follow ||
                followGeneration <= 0 || followGeneration == expectedTraversalGeneration)
            {
                detail = "followTransitionRejected:owner=" + followOwner + ":generation=" +
                         followGeneration.ToString(CultureInfo.InvariantCulture);
                return false;
            }

            // Do not retire/scrub a Follow consumer with an obsolete handoff epoch.
            if (!FollowerMovementWriterAuthority.TryEnterBoundPhysicalMotion(
                bot, FollowerMovementWriterAuthority.WriterDomain.Follow, followGeneration,
                out var v2HandoffScope, out string v2HandoffTicketReason))
            {
                detail = "v2FollowHandoffOriginRejected:" + v2HandoffTicketReason;
                return false;
            }
            try
            {
                NavMeshPathStatus status;
                using (v2HandoffScope)
                {
                    bot.Mover.LocalAvoidance?.GClass492DropOffset("R1-A9FDB108D6D1",@bot);
                    bot.Mover.SetBotMoverPause("R1-F2B94484AD5F",@bot,false);
                    // RC24: destination publication belongs to the existing exact
                    // Follow handoff ticket, not a new writer generation.
                    bool v2PathCommitted = FollowerMovementWriterAuthority.TryCommitScopedFollowPath(
                        bot, leader.Position, out status);
                    if (v2PathCommitted)
                        RecordFollowPathResult(bot, leader.Position, source, status);
                    bool learningV425EpochSynchronized =
                        !learningV425EpochArmed ||
                        !HasLearningV364UnsynchronizedPhysicalNavigationEpoch(
                            bot, out PhysicalNavigationEpochHandoff _);
                    if (status == NavMeshPathStatus.PathComplete &&
                        learningV425EpochSynchronized)
                    {
                        bot.BotOwnerSetTargetMoveSpeed("R1-CA62E5CEB518",@bot,ProbeMomentumFullMoveSpeed);
                        bot.Mover.BotMoverSetTargetMoveSpeed("R1-D7A8FA48E973",@bot,ProbeMomentumFullMoveSpeed);
                        detail = "primed:generation=" + followGeneration.ToString(CultureInfo.InvariantCulture) +
                                 ":length=" + pathLength.ToString("0.00", CultureInfo.InvariantCulture) +
                                 ":detour=" + detourRatio.ToString("0.00", CultureInfo.InvariantCulture) +
                                 ":twoPhase=true" +
                                 ":learningV425EpochSynchronized=" + learningV425EpochSynchronized +
                                 ":retire=" + learningV425RetireDetail;
                        {
                            WriteEvent("learningV425FreshFollowConsumerEpochCommitted", bot, new
                            {
                                reason = reason ?? string.Empty,
                                source,
                                followGeneration,
                                body = Vec(bot.GetPlayer.Position),
                                pathLength,
                                detourRatio,
                                staleTopologyRetireDetail = learningV425RetireDetail,
                                physicalNavigationEpochSynchronized = learningV425EpochSynchronized,
                                traversalSuccessRecorded = false,
                                persistentLearningChanged = false,
                                bodyRelocated = false,
                                action = "freshFollowPathAndNativeMoverLinkOriginShareCurrentPhysicalBodyBeforeFirstFollowTick"
                            });
                        }
                        ArmCF75FreshFollowPhysicalWatch(bot, followGeneration, source);
                        return true;
                    }
                    if ((status == NavMeshPathStatus.PathComplete &&
                        !learningV425EpochSynchronized))
                    {
                        WriteEvent("learningV425FreshFollowConsumerEpochRejected", bot, new
                        {
                            reason = reason ?? string.Empty,
                            source,
                            followGeneration,
                            body = Vec(bot.GetPlayer.Position),
                            pathLength,
                            detourRatio,
                            staleTopologyRetireDetail = learningV425RetireDetail,
                            physicalNavigationEpochSynchronized = false,
                            traversalSuccessRecorded = false,
                            persistentLearningChanged = false,
                            bodyRelocated = false,
                            action = "pathCompleteCannotOwnBodyUntilNativeMoverBookkeepingProvesSamePhysicalEpoch"
                        });
                    }
                }

                TryRetireNativeMoverPathPreserveLocomotion(
                    bot, "learningV419FreshConsumerRollback:" + (reason ?? "traversalHandoff"),
                    out string retireDetail);
                long rollbackGeneration = FollowerMovementWriterAuthority.BeginTraversal(
                    bot, "learningV419FreshConsumerRollback:" + (reason ?? "traversalHandoff"));
                bool scrubbed = TryScrubLearningV364NativeMoverEpochAnchor(
                    bot, bot.GetPlayer.Position,
                    "learningV419FreshConsumerRollback:" + (reason ?? "traversalHandoff"),
                    out string scrubDetail);
                detail = "pathPrime=" + status + ":rolledBackTraversalGeneration=" +
                         rollbackGeneration.ToString(CultureInfo.InvariantCulture) +
                         ":prePrimeRetire=" + learningV425RetireDetail +
                         ":retire=" + retireDetail + ":scrubbed=" + scrubbed + ":scrub=" + scrubDetail;
                return false;
            }
            catch (Exception ex)
            {
                RecordFollowPathWriteException(bot, leader.Position, source, ex);
                TryRetireNativeMoverPathPreserveLocomotion(
                    bot, "learningV419FreshConsumerExceptionRollback:" + (reason ?? "traversalHandoff"),
                    out string retireDetail);
                long rollbackGeneration = FollowerMovementWriterAuthority.BeginTraversal(
                    bot, "learningV419FreshConsumerExceptionRollback:" + (reason ?? "traversalHandoff"));
                bool scrubbed = TryScrubLearningV364NativeMoverEpochAnchor(
                    bot, bot.GetPlayer.Position,
                    "learningV419FreshConsumerExceptionRollback:" + (reason ?? "traversalHandoff"),
                    out string scrubDetail);
                detail = "primeFailed:" + ex.GetType().Name + ":rolledBackTraversalGeneration=" +
                         rollbackGeneration.ToString(CultureInfo.InvariantCulture) +
                         ":retire=" + retireDetail + ":scrubbed=" + scrubbed + ":scrub=" + scrubDetail;
                return false;
            }
        }

        private static bool TryInstallLearningV423PhysicalContinuationConsumer(
            BotOwner bot,
            long expectedTraversalGeneration,
            string reason,
            out string detail)
        {
            detail = "unavailable";
            if (((bot?.GetPlayer == null) ||
                bot.Mover == null || expectedTraversalGeneration <= 0 ||
                HasActiveTraversalProbe(bot) || BreadcrumbRoute.Count == 0))
            {
                return false;
            }

            string profileId = bot.ProfileId ?? string.Empty;
            if (string.IsNullOrEmpty(profileId))
            {
                detail = "profileUnavailable";
                return false;
            }

            long currentGeneration = FollowerMovementWriterAuthority.GetCurrentGeneration(
                bot, out FollowerMovementWriterAuthority.WriterDomain currentOwner);
            if (currentOwner != FollowerMovementWriterAuthority.WriterDomain.Traversal ||
                currentGeneration != expectedTraversalGeneration)
            {
                detail = "notExpectedTraversalEpoch:owner=" + currentOwner + ":generation=" +
                         currentGeneration.ToString(CultureInfo.InvariantCulture);
                return false;
            }

            if (FollowerRouteMissions.TryGetValue(
                    profileId, out FollowerRouteMission liveMission) &&
                liveMission != null && liveMission.Steps != null &&
                liveMission.CurrentStepIndex >= 0 &&
                liveMission.CurrentStepIndex < liveMission.Steps.Count)
            {
                detail = "typedRouteMissionStillOwnsContinuation:mission=" +
                         liveMission.MissionId.ToString(CultureInfo.InvariantCulture);
                return false;
            }

            if (!FollowerMovementRecoveryStates.TryGetValue(
                    profileId, out FollowerMovementRecoveryState state) || state == null)
            {
                state = new FollowerMovementRecoveryState
                {
                    LastPosition = bot.Position,
                    LastMovedAt = Time.time,
                    LearningV349LastPhysicalPosition = bot.Position,
                    LearningV349LastPhysicalProgressAt = Time.time
                };
                FollowerMovementRecoveryStates[profileId] = state;
            }

            int cursorFloor = Math.Max(
                Math.Max(
                    GetFollowerRouteExecutionFloor(profileId),
                    GetFollowerRouteResumeFloor(profileId)),
                Math.Max(0, state.LearningV349GenericPassageCursorSequence));
            if (FollowerTraversalRouteCursors.TryGetValue(
                    profileId, out FollowerTraversalRouteCursor routeCursor) &&
                routeCursor != null)
            {
                cursorFloor = Math.Max(cursorFloor, routeCursor.ConsumedBreadcrumbSequence);
            }

            // CF55: a completed WT can leave its real body ahead of the first future
            // breadcrumb in the tiny exit corridor. Choosing that breadcrumb drives the
            // continuation backwards across the already crossed physical frontier.
            // This is only a local WT far-side guard, not a global straight-line path
            // constraint: outside the exit vicinity genuine 90-degree turns remain legal.
            bool cf55TerminalWalkThroughFarSide = false;
            Vector3 cf55TerminalExit = default(Vector3);
            Vector3 cf55TerminalOutward = default(Vector3);
            int cf55TerminalEdgeId = 0;
            if (liveMission != null && liveMission.Steps != null &&
                liveMission.Steps.Count > 0 &&
                liveMission.CurrentStepIndex >= liveMission.Steps.Count)
            {
                FollowerRouteMissionStep cf55LastStep = liveMission.Steps[liveMission.Steps.Count - 1];
                if (cf55LastStep != null &&
                    string.Equals(cf55LastStep.Kind, "WalkThrough", StringComparison.OrdinalIgnoreCase) &&
                    IsFinite(cf55LastStep.RawEntry) && IsFinite(cf55LastStep.RawExit))
                {
                    cf55TerminalExit = cf55LastStep.RawExit;
                    cf55TerminalOutward = GetFlatDirection(cf55LastStep.RawExit - cf55LastStep.RawEntry);
                    Vector3 cf55BodyPastExit = bot.Position - cf55TerminalExit;
                    cf55BodyPastExit.y = 0f;
                    float cf55Ahead = Vector3.Dot(cf55BodyPastExit, cf55TerminalOutward);
                    float cf55Lateral = (cf55BodyPastExit - cf55TerminalOutward * cf55Ahead).magnitude;
                    cf55TerminalWalkThroughFarSide = cf55TerminalOutward.sqrMagnitude > 0.5f &&
                        cf55Ahead >= 0.25f && cf55Lateral <= 1.50f;
                    cf55TerminalEdgeId = cf55LastStep.EdgeId;
                }
            }

            PlayerBreadcrumbNode next = null;
            int advancedFloor = cursorFloor;
            int cf55BackwardExitCandidatesSkipped = 0;
            List<PlayerBreadcrumbNode> candidates = BreadcrumbRoute
                .Where(node => node != null && node.Sequence > cursorFloor &&
                               IsOperationalBreadcrumbNode(node) && IsFinite(node.Position))
                .OrderBy(node => node.Sequence)
                .ToList();
            for (int index = 0; index < candidates.Count; index++)
            {
                PlayerBreadcrumbNode candidate = candidates[index];
                float distance = (Vector3.Distance(bot.Position, candidate.Position));
                float reachDistance = (BreadcrumbMinDistance);
                if (distance <= reachDistance)
                {
                    advancedFloor = Math.Max(advancedFloor, candidate.Sequence);
                    continue;
                }
                if (cf55TerminalWalkThroughFarSide &&
                    GetPlanarDistance(candidate.Position, cf55TerminalExit) <= 1.80f)
                {
                    Vector3 cf55ToCandidate = candidate.Position - bot.Position;
                    cf55ToCandidate.y = 0f;
                    if (Vector3.Dot(cf55ToCandidate, cf55TerminalOutward) < -0.25f)
                    {
                        // Do not skip an unsafe/unknown distant route: only skip the
                        // locally backwards WT exit crumb; keep the physical floor
                        // strictly monotonic. The next genuine turn can be selected.
                        advancedFloor = Math.Max(advancedFloor, candidate.Sequence);
                        cf55BackwardExitCandidatesSkipped++;
                        continue;
                    }
                }
                if (distance <= RouteMissionBreadcrumbLookAheadDistance)
                {
                    next = candidate;
                    break;
                }
            }
            if (cf55BackwardExitCandidatesSkipped > 0)
            {
                WriteEvent("cf55TerminalWalkThroughBackwardExitBreadcrumbSuppressed", bot, new
                {
                    missionId = liveMission != null ? liveMission.MissionId : 0,
                    edgeId = cf55TerminalEdgeId,
                    skipped = cf55BackwardExitCandidatesSkipped,
                    fromFloor = cursorFloor,
                    advancedFloor,
                    selectedSequence = next != null ? next.Sequence : 0,
                    body = Vec(bot.Position),
                    exit = Vec(cf55TerminalExit),
                    traversalSuccessRecorded = false,
                    persistentLearningChanged = false,
                    bodyRelocated = false,
                    action = "skipOnlyBackwardBreadcrumbInsideMeasuredTerminalWalkThroughFarSideExitCorridor"
                });
            }

            state.LearningV349GenericPassageCursorSequence =
                Math.Max(state.LearningV349GenericPassageCursorSequence, advancedFloor);
            if (next == null)
            {
                detail = "noForwardBreadcrumbWithinExistingLookAhead:floor=" +
                         advancedFloor.ToString(CultureInfo.InvariantCulture);
                return false;
            }

            ClearLearningV351TerminalRecoveryState(
                bot, "learningV423PhysicalContinuationConsumer");
            state.LearningV350GenericPassageArmedAt = Time.time;
            state.LearningV350GenericPassageArmCursorSequence =
                state.LearningV349GenericPassageCursorSequence;
            state.LearningV349GenericPassageLeaseUntil =
                Time.time + RouteMissionHardRecoverySeconds;
            state.LearningV349LastPhysicalPosition = bot.Position;
            state.LearningV349LastPhysicalProgressAt = Time.time;
            state.LearningV349PhysicalStallSince = -1f;
            state.LearningV350TerminalRecoveryEligible = false;
            state.LearningV350TerminalRecoverySince = -1f;
            state.LearningV350TerminalRecoveryReason = string.Empty;

            bool cf19NativePathRetired = TryRetireNativeMoverPathPreserveLocomotion(
                bot, "learningV423PhysicalContinuationConsumer:" + (reason ?? "terminal"),
                out string retireDetail);
            // CF19-B: V423 previously retired the native path and set PointOnWay,
            // but unlike V419 never synchronized PrevSuccessLinkedFrom_1. CF18 WT
            // Edge150 physically finished at z=-47.58 then reappeared around z=-41.80
            // on the next native tick. Only native bookkeeping changes here: no body
            // relocation, StopMove, path replanning, or movement generation mutation.
            bool cf19NativeEpochScrubbed = cf19NativePathRetired &&
                TryScrubLearningV364NativeMoverEpochAnchor(
                    bot, bot.Position, "cf19V423AtomicPhysicalContinuation",
                    out string cf19ScrubDetail);
            if (!cf19NativeEpochScrubbed)
            {
                detail = "cf19NativeEpochNotSynchronized:retire=" + retireDetail;
                // Fail closed rather than publish a physical producer backed by a
                // stale native mover epoch. The existing V419/orphan retry can recover.
                return false;
            }
            WriteEvent("cf19V423NativeMoverEpochSynchronized", bot, new
            {
                reason = reason ?? string.Empty,
                generation = expectedTraversalGeneration,
                body = Vec(bot.Position),
                nativeLinkAnchor = Vec(bot.Mover.PositionOnWayInner),
                previousNativeLinkAnchor = Vec(bot.Mover.PrevSuccessLinkedFrom_1),
                retireDetail,
                bodyRelocated = false,
                traversalSuccessRecorded = false,
                persistentLearningChanged = false,
                action = "synchronizeNativeMoverLinkBookkeepingBeforeV423PhysicalContinuationProducer"
            });
            ArmLearningV365ContinuousGenericPhysicalRoute(
                bot, next, expectedTraversalGeneration);

            bool drove = TryContinueLearningV365ContinuousGenericPhysicalRoute(
                bot, out int targetSequence);
            long afterGeneration = FollowerMovementWriterAuthority.GetCurrentGeneration(
                bot, out FollowerMovementWriterAuthority.WriterDomain afterOwner);
            if (!drove)
            {
                ClearLearningV365ContinuousPhysicalRoute(
                    bot, "learningV423PhysicalContinuationConsumerDriveRejected");
                if (afterOwner != FollowerMovementWriterAuthority.WriterDomain.Traversal)
                {
                    long rollbackGeneration = FollowerMovementWriterAuthority.BeginTraversal(
                        bot, "learningV423PhysicalContinuationConsumerRollback");
                    TryScrubLearningV364NativeMoverEpochAnchor(
                        bot, bot.Position,
                        "learningV423PhysicalContinuationConsumerRollback",
                        out string rollbackScrubDetail);
                    detail = "driveRejected:rolledBackTraversalGeneration=" +
                             rollbackGeneration.ToString(CultureInfo.InvariantCulture) +
                             ":retire=" + retireDetail +
                             ":scrub=" + rollbackScrubDetail;
                }
                else
                {
                    detail = "driveRejected:ownerStayedTraversal:retire=" + retireDetail;
                }
                return false;
            }

            detail = "installed:targetSequence=" +
                     targetSequence.ToString(CultureInfo.InvariantCulture) +
                     ":owner=" + afterOwner +
                     ":generation=" + afterGeneration.ToString(CultureInfo.InvariantCulture) +
                     ":floor=" + advancedFloor.ToString(CultureInfo.InvariantCulture);
            WriteEvent("learningV423PhysicalContinuationConsumerInstalled", bot, new
            {
                reason = reason ?? string.Empty,
                previousTraversalGeneration = expectedTraversalGeneration,
                consumerOwner = afterOwner.ToString(),
                consumerGeneration = afterGeneration,
                routeFloor = advancedFloor,
                targetSequence,
                target = Vec(next.Position),
                targetNavMeshSampled = next.NavMeshSampled,
                staleTopologyRetireDetail = retireDetail,
                traversalSuccessRecorded = false,
                persistentLearningChanged = false,
                bodyRelocated = false,
                action = "completionAndForwardPhysicalConsumerCommitAtomicallyWithoutRequiringNavMesh"
            });
            return true;
        }
    }
}
