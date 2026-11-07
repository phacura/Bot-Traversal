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
        private static bool TryGetActiveFollowerRouteMission(BotOwner bot, out FollowerRouteMission mission)
        {
            mission = null;
            string profileId = bot?.ProfileId ?? string.Empty;
            if (string.IsNullOrEmpty(profileId) ||
                !FollowerRouteMissions.TryGetValue(profileId, out mission) ||
                mission == null || mission.Lifecycle.Retired || mission.LifecycleRaid!=raidGeneration ||
                mission.LifecycleBotId!=profileId || (Time.time > mission.ExpiresAt&&!IsCapabilityRouteRetryReturning(mission)) ||
                mission.CurrentStepIndex >= mission.Steps.Count)
            {
                return false;
            }
            return true;
        }

        private static bool TryArmOrRefreshFollowerRouteMission(BotOwner bot, Vector3 leaderPosition, string source, bool force)
        {
            string profileId = bot?.ProfileId ?? string.Empty;
            if (string.IsNullOrEmpty(profileId) || !IsLiveBot(bot) || !IsFinite(leaderPosition))
            {
                return false;
            }

            bool newTraversalBypass = string.Equals(source, "newTraversalAfterReject", StringComparison.Ordinal);
            if (!newTraversalBypass &&
                FollowerRouteMissionRearmBlockedUntil.TryGetValue(profileId, out float rearmBlockedUntil))
            {
                if (Time.time < rearmBlockedUntil)
                {
                    return false;
                }
                FollowerRouteMissionRearmBlockedUntil.Remove(profileId);
            }

            if (FollowerRouteMissions.TryGetValue(profileId, out FollowerRouteMission existing) && existing != null)
            {
                RefreshFollowerRouteMission(bot, existing);
                return existing.CurrentStepIndex < existing.Steps.Count;
            }

            float leaderDistance = GetPlanarDistance(bot.Position, leaderPosition);
            int routeFloor = Math.Max(
                GetFollowerRouteResumeFloor(profileId),
                GetFollowerRouteExecutionFloor(profileId));
            int startSequence = Math.Max(routeFloor, FindFollowerRouteStartBreadcrumbSequence(bot));
            if (FollowerTraversalRouteCursors.TryGetValue(profileId, out FollowerTraversalRouteCursor cursor) && cursor != null)
            {
                startSequence = Math.Max(startSequence, cursor.ConsumedBreadcrumbSequence);
            }

            bool learningV340FreshTraversalOpportunity = (TraversalEpisodeStream.Any(item => item != null && item.EndBreadcrumbSequence > startSequence));
            if (!force && leaderDistance < RouteMissionProactiveLeaderDistance && !learningV340FreshTraversalOpportunity)
            {
                return false;
            }

            // CF34: time the actual cold mission build, not frame time or the diagnostic writer.
            // No caching, negative plan reuse, authority change or traversal admission suppression.
            long cf34ArmStartedTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            int cf34EpisodesInspected = 0;
            // CF51: split the expensive CF34 episode scan by unchanged call boundaries.
            // One sample per cold arm; no cached decisions and no changes to admission.
            long cf51StepBuildTotalTicks = 0;
            long cf51SelectorTotalTicks = 0;
            long cf51MaxStepBuildTicks = 0;
            int cf51MaxStepBuildEpisodeId = 0;
            long cf51MaxSelectorTicks = 0;
            int cf51MaxSelectorEpisodeId = 0;
            // CF52: identify the expensive CF51 "other scan" without guessing.
            // Additive, disjoint inner intervals; every original admission and
            // physical proof executes unchanged. No negative-result caching.
            long cf52EdgeLookupTotalTicks = 0;
            long cf52PhysicalPassTotalTicks = 0;
            long cf52MaxPhysicalPassTicks = 0;
            int cf52MaxPhysicalPassEpisodeId = 0;
            // CF53: materialize exactly the previous ordered LINQ query once in this
            // cold arm to isolate enumeration/ordering cost. Never persist or reuse it
            // across frames, missions, or authoritative breadcrumb generations.
            long cf53EpisodeOrderingStartTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            List<TraversalEpisodeMetadata> cf53OrderedEpisodes = TraversalEpisodeStream
                .Where(item => item != null &&
                               item.EndBreadcrumbSequence > startSequence)
                .OrderBy(item => item.StartBreadcrumbSequence)
                .ThenBy(item => item.EpisodeId)
                .ToList();
            long cf53EpisodeOrderingTicks = System.Diagnostics.Stopwatch.GetTimestamp() -
                cf53EpisodeOrderingStartTicks;
            long cf53EpisodeRejectionTelemetryTicks = 0;
            long cf53PhysicalPassRetirementTicks = 0;
            long cf53CanonicalStepTicks = 0;
            List<FollowerRouteMissionStep> steps = new List<FollowerRouteMissionStep>();
            foreach (TraversalEpisodeMetadata episode in cf53OrderedEpisodes)
            {
                cf34EpisodesInspected++;
                long cf51BuildBeginTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                FollowerRouteMissionStep step = CreateFollowerRouteMissionStep(episode);
                long cf51StepBuildTicks = System.Diagnostics.Stopwatch.GetTimestamp() - cf51BuildBeginTicks;
                cf51StepBuildTotalTicks += cf51StepBuildTicks;
                if (cf51StepBuildTicks > cf51MaxStepBuildTicks)
                {
                    cf51MaxStepBuildTicks = cf51StepBuildTicks;
                    cf51MaxStepBuildEpisodeId = episode.EpisodeId;
                }
                if (step == null)
                {
                    long cf53RejectedStartTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                    EmitLearningV347TraversalEpisodeRejectedBeforeMission(
                        bot, episode, "routeMissionArm", "stepCreationRejected");
                    cf53EpisodeRejectionTelemetryTicks += System.Diagnostics.Stopwatch.GetTimestamp() -
                        cf53RejectedStartTicks;
                    continue;
                }
                long cf52LookupStartTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                LearnedTraversalEdge learnedStepEdge = LearnedTraversalEdges.FirstOrDefault(candidate =>
                    candidate != null && candidate.Id == step.EdgeId);
                cf52EdgeLookupTotalTicks += System.Diagnostics.Stopwatch.GetTimestamp() - cf52LookupStartTicks;
                if (step.EndBreadcrumbSequence <= routeFloor &&
                    (!GetLearningV354ActionProfile(step.Kind).RequiresActuator ||
                     IsFollowerRouteMissionStepPhysicallyPassed(bot,step,out _,out _)))
                {
                    EmitLearningV347TraversalOpportunityNoStart(
                        bot, null, step, "routeMissionArm", "belowRouteResumeFloor");
                    continue;
                }
                if ((string.Equals(step.Kind, "WalkThrough", StringComparison.OrdinalIgnoreCase)) &&
                    LearningV344ReleasedWalkThroughEpisodeByFollower.TryGetValue(profileId, out int releasedEpisodeId))
                {
                    if (step.EpisodeId > releasedEpisodeId)
                    {
                        LearningV344ReleasedWalkThroughEpisodeByFollower.Remove(profileId);
                    }
                    else if (step.EpisodeId == releasedEpisodeId)
                    {
                        float releasedEntryDistance = GetPlanarDistance(bot.Position, step.Entry);
                        if (!step.CurrentRaidRouteAuthority &&
                            releasedEntryDistance > RouteMissionTraversalAdjacentBridgeMaxDistance)
                        {
                            EmitLearningV347TraversalOpportunityNoStart(
                                bot, null, step, "routeMissionArm", "releasedEpisodeRemoteRearmSuppressed");
                            if (ShouldEmitDiagnostic("learningV344ReleasedWalkThroughRemoteRearmSuppressed:" + step.EdgeId, bot, 0.75f))
                            {
                                WriteEvent("learningV344ReleasedWalkThroughRemoteRearmSuppressed", bot, new
                                {
                                    episodeId = step.EpisodeId,
                                    edgeId = step.EdgeId,
                                    entryDistance = releasedEntryDistance,
                                    localEnvelope = RouteMissionTraversalAdjacentBridgeMaxDistance,
                                    action = "nonCurrentRaidReleasedEpisodeCannotReacquireRemoteOwnership"
                                });
                            }
                            continue;
                        }
                        LearningV344ReleasedWalkThroughEpisodeByFollower.Remove(profileId);
                        if ((step.CurrentRaidRouteAuthority) &&
                            releasedEntryDistance > RouteMissionTraversalAdjacentBridgeMaxDistance)
                        {
                            WriteEvent("learningV348CurrentRaidReleasedEpisodeRearmedByRouteEpoch", bot, new
                            {
                                episodeId = step.EpisodeId,
                                edgeId = step.EdgeId,
                                kind = step.Kind,
                                entryDistance = releasedEntryDistance,
                                persistentLearningChanged = false,
                                action = "releaseWasMovementOwnershipOnly"
                            });
                        }
                    }
                }
                long cf51SelectorBeginTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                bool cf51Selectable = IsTraversalEdgeTopologicallySelectableForRouteStep(
                    bot, learnedStepEdge, step, out string routePolicyReason);
                long cf51SelectorTicks = System.Diagnostics.Stopwatch.GetTimestamp() - cf51SelectorBeginTicks;
                cf51SelectorTotalTicks += cf51SelectorTicks;
                if (cf51SelectorTicks > cf51MaxSelectorTicks)
                {
                    cf51MaxSelectorTicks = cf51SelectorTicks;
                    cf51MaxSelectorEpisodeId = episode.EpisodeId;
                }
                if (!cf51Selectable)
                {
                    EmitLearningV347TraversalOpportunityNoStart(
                        bot, null, step, "routeMissionArmSelector", routePolicyReason);
                    if (ShouldEmitDiagnostic("traversalExperienceRouteSuppressed:" + step.EdgeId, bot, 1.25f))
                    {
                        WriteEvent("traversalExperienceRouteSuppressed", bot, new
                        {
                            edgeId = step.EdgeId,
                            kind = step.Kind,
                            episodeId = step.EpisodeId,
                            reason = routePolicyReason
                        });
                    }
                    continue;
                }

                // V3.25: proximity to an exit is not proof that an asymmetric barrier was crossed.
                // Consume only through the topology-aware physical-pass contract.
                long cf52PhysicalPassStartTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                bool cf52AlreadyPhysicallyPassed = IsFollowerRouteMissionStepPhysicallyPassed(
                    bot, step, out float learningV388ArmProjection,
                    out float learningV388ArmRequiredProjection);
                long cf52PhysicalPassTicks = System.Diagnostics.Stopwatch.GetTimestamp() -
                    cf52PhysicalPassStartTicks;
                cf52PhysicalPassTotalTicks += cf52PhysicalPassTicks;
                if (cf52PhysicalPassTicks > cf52MaxPhysicalPassTicks)
                {
                    cf52MaxPhysicalPassTicks = cf52PhysicalPassTicks;
                    cf52MaxPhysicalPassEpisodeId = episode.EpisodeId;
                }
                if (cf52AlreadyPhysicallyPassed)
                {
                    long cf53PassedStartTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                    LearningV354TraversalCapabilityProfile learningV388ArmProfile =
                        GetLearningV354ActionProfile(step.Kind);
                    if ((learningV388ArmProfile != null) &&
                        learningV388ArmProfile.PhysicalPassageCanProveCompletion)
                    {
                        ConsumeFollowerRouteMissionPhysicalPass(
                            bot, null, step, "learningV388RouteArmPhysicalPassConsumed",
                            learningV388ArmProjection, learningV388ArmRequiredProjection);
                        MarkLearningV388PhysicalPassageExecutionDebtConsumed(
                            bot, step, "learningV388RouteArmPhysicalPassConsumed");
                    }
                    EmitLearningV347TraversalOpportunityNoStart(
                        bot, null, step, "routeMissionArm", "physicalPassAlreadyProvenNoExecutionRequired");
                    startSequence = Math.Max(startSequence, step.EndBreadcrumbSequence);
                    cf53PhysicalPassRetirementTicks += System.Diagnostics.Stopwatch.GetTimestamp() -
                        cf53PassedStartTicks;
                    continue;
                }
                long cf53CanonicalStepStartTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                if (!TryAddCanonicalRouteMissionStep(steps, step))
                {
                    EmitLearningV347TraversalOpportunityNoStart(
                        bot, null, step, "routeMissionArmCanonicalization",
                        "redundantWithEquivalentRouteStep");
                }
                cf53CanonicalStepTicks += System.Diagnostics.Stopwatch.GetTimestamp() -
                    cf53CanonicalStepStartTicks;
            }

            long cf34AfterEpisodeEnumerationTicks = System.Diagnostics.Stopwatch.GetTimestamp();

            // V3.47: a known WT crossing can be synthesized from the current player's ordered
            // breadcrumbs even when no heuristic traversal episode was created. Build the temporary
            // mission first, then let directional-crossing synthesis contribute first-class steps.
            // This breaks the old circular dependency: "need a step to create a mission, need a
            // mission to recognize the known WT crossing".
            FollowerRouteMission mission = new FollowerRouteMission
            {
                MissionId = ++followerRouteMissionSequence,
                LifecycleBotId = profileId,
                LifecycleRaid = raidGeneration,
                Source = source ?? "playerRoute",
                CreatedAt = Time.time,
                ExpiresAt = Time.time + RouteMissionLifetimeSeconds,
                LastProgressAt = Time.time,
                LastProgressPosition = bot.Position,
                StartBreadcrumbSequence = startSequence,
                TargetBreadcrumbSequence = Math.Max(
                    breadcrumbSequence,
                    steps.Count > 0 ? steps.Max(step => step.EndBreadcrumbSequence) : startSequence),
                ResumeFloorBreadcrumbSequence = routeFloor,

                LastPlayerEpisodeId = GetLatestTraversalEpisodeId(),
                ApproachBreadcrumbSequence = startSequence,
                PhysicalApproachBreadcrumbSequence = startSequence,
                ObservedDownstreamBreadcrumbSequence = startSequence,
                Steps = steps
            };
            RefreshRouteMissionBreadcrumbSnapshot(mission);
            // CF35: split CF34's measured synthesis window into directly comparable,
            // single-call stages. Never skip a breadcrumb, filter an edge, or cache a
            // negative decision; native action and movement authority stay unchanged.
            long cf35AfterSnapshotTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            AppendDirectionalWalkThroughRouteStepsFromBreadcrumbs(
                bot,
                mission,
                startSequence,
                "arm");
            long cf35AfterDirectionalSynthesisTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            ApplyLearningV366DirectedPhysicalStepOrder(bot, mission, "routeMissionArm");
            long cf35AfterStepOrderTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            ApplyLearningV369NestedUnprovenDropDownCanonicalization(bot, mission, "routeMissionArm");
            long cf35AfterNestedNormalizationTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            mission.TargetBreadcrumbSequence = Math.Max(
                mission.TargetBreadcrumbSequence,
                mission.Steps.Count > 0 ? mission.Steps.Max(step => step.EndBreadcrumbSequence) : startSequence);
            long cf34AfterSynthesisTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            // Timestamp overhead is bounded to three calls per cold arm, including empty arms.
            // Data are single-invocation samples; they cannot be read as total per-raid CPU cost.
            double cf34EpisodeScanMilliseconds = (cf34AfterEpisodeEnumerationTicks - cf34ArmStartedTicks) *
                1000.0 / System.Diagnostics.Stopwatch.Frequency;
            double cf51StepBuildMilliseconds = cf51StepBuildTotalTicks *
                1000.0 / System.Diagnostics.Stopwatch.Frequency;
            double cf51SelectorMilliseconds = cf51SelectorTotalTicks *
                1000.0 / System.Diagnostics.Stopwatch.Frequency;
            double cf51OtherEpisodeScanMilliseconds = Math.Max(0.0,
                cf34EpisodeScanMilliseconds - cf51StepBuildMilliseconds - cf51SelectorMilliseconds);
            double cf51MaxStepBuildMilliseconds = cf51MaxStepBuildTicks *
                1000.0 / System.Diagnostics.Stopwatch.Frequency;
            double cf51MaxSelectorMilliseconds = cf51MaxSelectorTicks *
                1000.0 / System.Diagnostics.Stopwatch.Frequency;
            double cf52EdgeLookupMilliseconds = cf52EdgeLookupTotalTicks *
                1000.0 / System.Diagnostics.Stopwatch.Frequency;
            double cf52PhysicalPassMilliseconds = cf52PhysicalPassTotalTicks *
                1000.0 / System.Diagnostics.Stopwatch.Frequency;
            double cf52MaxPhysicalPassMilliseconds = cf52MaxPhysicalPassTicks *
                1000.0 / System.Diagnostics.Stopwatch.Frequency;
            double cf52OtherEpisodeScanAfterInnerProbesMilliseconds = Math.Max(0.0,
                cf51OtherEpisodeScanMilliseconds - cf52EdgeLookupMilliseconds - cf52PhysicalPassMilliseconds);
            // CF53: partition only the CF52 residual. The remaining interval includes
            // unreconciled branch/iteration cost and Stopwatch overhead, never FPS.
            double cf53EpisodeOrderingMilliseconds = cf53EpisodeOrderingTicks *
                1000.0 / System.Diagnostics.Stopwatch.Frequency;
            double cf53EpisodeRejectionTelemetryMilliseconds = cf53EpisodeRejectionTelemetryTicks *
                1000.0 / System.Diagnostics.Stopwatch.Frequency;
            double cf53PhysicalPassRetirementMilliseconds = cf53PhysicalPassRetirementTicks *
                1000.0 / System.Diagnostics.Stopwatch.Frequency;
            double cf53CanonicalStepMilliseconds = cf53CanonicalStepTicks *
                1000.0 / System.Diagnostics.Stopwatch.Frequency;
            double cf53OtherEpisodeScanResidualMilliseconds = Math.Max(0.0,
                cf52OtherEpisodeScanAfterInnerProbesMilliseconds -
                cf53EpisodeOrderingMilliseconds - cf53EpisodeRejectionTelemetryMilliseconds -
                cf53PhysicalPassRetirementMilliseconds - cf53CanonicalStepMilliseconds);
            double cf34SynthesisMilliseconds = (cf34AfterSynthesisTicks - cf34AfterEpisodeEnumerationTicks) *
                1000.0 / System.Diagnostics.Stopwatch.Frequency;
            double cf34ColdArmMilliseconds = (cf34AfterSynthesisTicks - cf34ArmStartedTicks) *
                1000.0 / System.Diagnostics.Stopwatch.Frequency;
            // Adjacent, disjoint Stopwatch intervals should sum to CF34 synthesis cost
            // (subject to rounding). They are diagnostic observations, not frame FPS.
            double cf35BreadcrumbSnapshotMilliseconds = (cf35AfterSnapshotTicks - cf34AfterEpisodeEnumerationTicks) *
                1000.0 / System.Diagnostics.Stopwatch.Frequency;
            double cf35DirectionalSynthesisMilliseconds = (cf35AfterDirectionalSynthesisTicks - cf35AfterSnapshotTicks) *
                1000.0 / System.Diagnostics.Stopwatch.Frequency;
            double cf35StepOrderMilliseconds = (cf35AfterStepOrderTicks - cf35AfterDirectionalSynthesisTicks) *
                1000.0 / System.Diagnostics.Stopwatch.Frequency;
            double cf35NestedNormalizationMilliseconds = (cf35AfterNestedNormalizationTicks - cf35AfterStepOrderTicks) *
                1000.0 / System.Diagnostics.Stopwatch.Frequency;
            double cf35FinalTargetMilliseconds = (cf34AfterSynthesisTicks - cf35AfterNestedNormalizationTicks) *
                1000.0 / System.Diagnostics.Stopwatch.Frequency;
            if (mission.Steps.Count == 0)
            {
                // CF33: a freshly observed episode is not necessarily an executable traversal.
                // Re-evaluate each new breadcrumb opportunity exactly as before, but do not turn
                // ordinary running Follow with no action debt into a repeated diagnostic failure.
                // Use one bounded per-bot key rather than one key per breadcrumb (unbounded keys).
                // This changes only diagnostic publication; never cache a negative mission result.
                if ((learningV340FreshTraversalOpportunity) &&
                    ShouldEmitDiagnostic("learningV347RouteMissionNoExecutableTraversal", bot, 3.0f))
                {
                    WriteEvent("learningV347RouteMissionNoExecutableTraversal", bot, new
                    {
                        source = mission.Source,
                        startBreadcrumbSequence = startSequence,
                        targetBreadcrumbSequence = mission.TargetBreadcrumbSequence,
                        leaderDistance,
                        lastValidStage = "routeMissionArm",
                        rejectionReason = "freshTraversalOpportunityProducedNoExecutableStep",
                        missingEvent = "TraversalAttemptStarted",
                        cf34EpisodesInspected,
                        cf34EpisodeScanMilliseconds,
                        cf51StepBuildMilliseconds,
                        cf51SelectorMilliseconds,
                        cf51OtherEpisodeScanMilliseconds,
                        cf51MaxStepBuildEpisodeId,
                        cf51MaxStepBuildMilliseconds,
                        cf51MaxSelectorEpisodeId,
                        cf51MaxSelectorMilliseconds,
                        cf52EdgeLookupMilliseconds,
                        cf52PhysicalPassMilliseconds,
                        cf52MaxPhysicalPassEpisodeId,
                        cf52MaxPhysicalPassMilliseconds,
                        cf52OtherEpisodeScanAfterInnerProbesMilliseconds,
                        cf53EpisodeOrderingMilliseconds,
                        cf53EpisodeRejectionTelemetryMilliseconds,
                        cf53PhysicalPassRetirementMilliseconds,
                        cf53CanonicalStepMilliseconds,
                        cf53OtherEpisodeScanResidualMilliseconds,
                        cf53MeasurementScope = "singleColdArmExactOrderingAndDisjointBranchSubstages",
                        cf34SynthesisMilliseconds,
                        cf34ColdArmMilliseconds,
                        cf34MeasurementScope = "singleColdArmNotAggregated",
                        cf35BreadcrumbSnapshotMilliseconds,
                        cf35DirectionalSynthesisMilliseconds,
                        cf35StepOrderMilliseconds,
                        cf35NestedNormalizationMilliseconds,
                        cf35FinalTargetMilliseconds,
                        cf35MeasurementScope = "singleColdArmDisjointSynthesisSubstages",
                        observationOnly = true,
                        traversalSuccessRecorded = false,
                        persistentLearningChanged = false,
                        action = "noExecutableStepKeepFollowAndRescanFreshBreadcrumbsWithoutNegativeCache"
                    });
                }
                return false;
            }
            TryRetireLearningV382SupersededPhysicalNavigationEpochForMission(
                bot, mission, "routeMissionArm");
            if (!RegisterLifecycleMission(bot,mission)) return false;

            WriteEvent("followerRouteMissionArmed", bot, new
            {
                missionId = mission.MissionId,
                source = mission.Source,
                startBreadcrumbSequence = mission.StartBreadcrumbSequence,
                targetBreadcrumbSequence = mission.TargetBreadcrumbSequence,
                traversalCount = mission.Steps.Count,
                firstEpisodeId = mission.Steps[0].EpisodeId,
                firstEdgeId = mission.Steps[0].EdgeId,
                firstKind = mission.Steps[0].Kind,
                leaderDistance,
                learningV340FreshTraversalOpportunity,
                cf34EpisodesInspected,
                cf34EpisodeScanMilliseconds,
                cf51StepBuildMilliseconds,
                cf51SelectorMilliseconds,
                cf51OtherEpisodeScanMilliseconds,
                cf51MaxStepBuildEpisodeId,
                cf51MaxStepBuildMilliseconds,
                cf51MaxSelectorEpisodeId,
                cf51MaxSelectorMilliseconds,
                cf52EdgeLookupMilliseconds,
                cf52PhysicalPassMilliseconds,
                cf52MaxPhysicalPassEpisodeId,
                cf52MaxPhysicalPassMilliseconds,
                cf52OtherEpisodeScanAfterInnerProbesMilliseconds,
                cf53EpisodeOrderingMilliseconds,
                cf53EpisodeRejectionTelemetryMilliseconds,
                cf53PhysicalPassRetirementMilliseconds,
                cf53CanonicalStepMilliseconds,
                cf53OtherEpisodeScanResidualMilliseconds,
                cf53MeasurementScope = "singleColdArmExactOrderingAndDisjointBranchSubstages",
                cf34SynthesisMilliseconds,
                cf34ColdArmMilliseconds,
                cf34MeasurementScope = "singleColdArmNotAggregated",
                cf35BreadcrumbSnapshotMilliseconds,
                cf35DirectionalSynthesisMilliseconds,
                cf35StepOrderMilliseconds,
                cf35NestedNormalizationMilliseconds,
                cf35FinalTargetMilliseconds,
                cf35MeasurementScope = "singleColdArmDisjointSynthesisSubstages",
                admission = learningV340FreshTraversalOpportunity && leaderDistance < RouteMissionProactiveLeaderDistance
                    ? "freshTraversalOpportunityPreemptsGenericFollowDetour"
                    : "legacyDistanceOrForced"
            });
            return true;
        }

        private static bool EnsureFollowerRouteMissionCommitment(BotOwner bot, FollowerRouteMission mission)
        {
            if(IsCapabilityRouteRetryReturning(mission))return true;
            string profileId = bot?.ProfileId ?? string.Empty;
            if (string.IsNullOrEmpty(profileId) || mission == null)
            {
                return false;
            }

            int learningV374RetiredSemanticEdgeId;
            int learningV374RetiredSemanticEpisodeId;
            while (!IsCapabilityFullRouteReplay(mission) && mission.CurrentStepIndex >= 0 &&
                   mission.CurrentStepIndex < mission.Steps.Count &&
                   TryRetireLearningV374SemanticDisprovedUnownedWalkThroughDebt(
                       bot, mission, "ensureRouteMissionCommitment",
                       out learningV374RetiredSemanticEdgeId,
                       out learningV374RetiredSemanticEpisodeId))
            {
                // Multiple overlapping WT windows may all have been disproved by the same ordinary
                // current-raid route. Retire only their runtime execution debt, monotonically.
            }

            if ((IsLearningV350TerminalPhysicalRecoveryEligible(bot)))
            {
                TrySupersedeLearningV391TerminalRecoveryByExactActuatorFrontier(
                    bot, mission, "ensureRouteMissionCommitment");
            }

            if ((IsLearningV350TerminalPhysicalRecoveryEligible(bot)))
            {
                if (!TrySupersedeLearningV362TerminalRecoveryByFreshRouteEpoch(
                        bot, mission, "ensureRouteMissionCommitment"))
                {
                    LearnedTraversalCommitments.Remove(profileId);
                    {
                        ReleaseLearningV353TerminalRecoveryRetryLease(bot, "ensureRouteMissionCommitment");
                    }
                    if (ShouldEmitDiagnostic("learningV351TerminalRecoveryBlocksRouteRecommit", bot, 0.50f))
                    {
                        WriteEvent("learningV351TerminalRecoveryBlocksRouteRecommit", bot, new
                        {
                            missionId = mission.MissionId,
                            stepIndex = mission.CurrentStepIndex,
                            retryLeaseReleased = true,
                            action = ("keepMissionSemanticIntentYieldMovementToNavigationRecovery")
                        });
                    }
                    return true;
                }
            }

            while (mission.CurrentStepIndex < mission.Steps.Count)
            {
                if(TryRetireCapabilityCoveredDropOverlap(bot,mission))continue;
                TryCanonicalizeLearningV334DirectionalWalkThroughSuccessor(bot, mission);
                FollowerRouteMissionStep step = mission.Steps[mission.CurrentStepIndex];
            var r2DebtObservation3 = CaptureRouteDebtObservation(bot, mission, step, false);

                if (step == null)
                {
                    if (!TryAdvanceRouteMission(bot, mission, mission.CurrentStepIndex + 1, RouteDebtEvidence.PolicySuppression, "EnsureFollowerRouteMissionCommitment:1", r2DebtObservation3)) return true;
                    continue;
                }

                RefreshCapabilityCurrentPhysicalCorridor(bot,mission,step);
                if(!IsCapabilityFullRouteReplay(mission))RefreshLearningV424CurrentEpisodeWalkThroughAdmissionGeometry(
                    bot, mission, step, "ensureRouteMissionCommitment");
                r2DebtObservation3 = CaptureRouteDebtObservation(bot, mission, step);


                if (TryRetireLearningV435WalkThroughDebtOnDownstreamNavMeshRejoin(
                        bot, mission, step, "ensureRouteMissionCommitment"))
                {
                    continue;
                }

                {
                    if (mission.LearningV431EquivalentBarrierRescuePending &&
                        TryBeginLearningV431EquivalentLocalBarrierSelfRescue(
                            bot, mission, step, out string learningV431TransferSource))
                    {
                        WriteEvent("learningV431RouteMissionYieldedToEquivalentBarrierTransfer", bot, new
                        {
                            missionId = mission.MissionId,
                            stepIndex = mission.CurrentStepIndex,
                            edgeId = step.EdgeId,
                            episodeId = step.EpisodeId,
                            kind = step.Kind,
                            transferSource = learningV431TransferSource,
                            action = "keepOriginalRouteDebtFrozenWhileTransientAlternatePhysicalProducerOwnsBody"
                        });
                        return true;
                    }

                    if (mission.LearningV431EquivalentBarrierRescueActive)
                    {
                        bool activeTransfer = LearnedTraversalCommitments.TryGetValue(
                                profileId, out LearnedTraversalCommitment learningV431Commitment) &&
                            IsLearningV431EquivalentBarrierTransfer(learningV431Commitment);
                        if (activeTransfer || HasActiveTraversalProbe(bot))
                        {
                            // Do not let the original authored entry recommit while the alternate
                            // frontier transaction is waiting for/owns its physical probe.
                            return true;
                        }

                        // The transient producer disappeared without success (for example selection
                        // was rejected before a probe started). The original route debt remains and
                        // normal bounded recovery may resume; never relaunch this alternate forever.
                        mission.LearningV431EquivalentBarrierRescueActive = false;
                        WriteEvent("learningV431EquivalentBarrierProducerMissingFallback", bot, new
                        {
                            missionId = mission.MissionId,
                            stepIndex = mission.CurrentStepIndex,
                            edgeId = step.EdgeId,
                            episodeId = step.EpisodeId,
                            kind = step.Kind,
                            originalRouteDebtPreserved = true,
                            action = "resumeExistingRouteRecoveryWithoutRearmingAlternateFrontier"
                        });
                    }
                }

                int routeFloor = Math.Max(
                    Math.Max(GetFollowerRouteResumeFloor(profileId), GetFollowerRouteExecutionFloor(profileId)),
                    mission.ResumeFloorBreadcrumbSequence);
                if (step.EndBreadcrumbSequence <= routeFloor && CanConsumeCapabilityCursorFloor(bot,step))
                {
                    if (!TryAdvanceRouteMission(bot, mission, mission.CurrentStepIndex + 1, RouteDebtEvidence.CursorFloor, "EnsureFollowerRouteMissionCommitment:2", r2DebtObservation3)) return true;
                    mission.ApproachBreadcrumbSequence = Math.Max(mission.ApproachBreadcrumbSequence, step.EndBreadcrumbSequence);
                    ClearLearnedTraversalCommitment(bot, step.EdgeId, "routeResumeFloorSkip");
                    WriteEvent("followerRouteMissionStepSkipped", bot, new
                    {
                        missionId = mission.MissionId,
                        stepIndex = mission.CurrentStepIndex - 1,
                        episodeId = step.EpisodeId,
                        edgeId = step.EdgeId,
                        kind = step.Kind,
                        routeFloor,
                        quarantined = false,
                        reason = "routeResumeFloor"
                    });
                    continue;
                }

                LearnedTraversalEdge stepEdge = LearnedTraversalEdges.FirstOrDefault(candidate =>
                    candidate != null && candidate.Id == step.EdgeId);
                if (!IsTraversalEdgeTopologicallySelectableForRouteStep(bot, stepEdge, step, out string stepPolicyReason))
                {
                    bool transientRetryCooldown =
                        (!string.IsNullOrWhiteSpace(stepPolicyReason)) &&
                        stepPolicyReason.StartsWith("retryCooldown=", StringComparison.OrdinalIgnoreCase);
                    if (transientRetryCooldown)
                    {
                        // V3.29: retry suppression is not traversal completion. Keep the current
                        // RouteMission barrier in place so a failed Jump/Vault cannot silently advance
                        // into its successor and manufacture a cascade of Assist/Teleport recoveries.
                        ClearLearnedTraversalCommitment(bot, step.EdgeId, "learningV329RouteMissionRetryBarrier");
                        if ((TryParseTraversalRetryCooldownSeconds(stepPolicyReason, out float retryCooldownSeconds)))
                        {
                            float retryHoldUntil = Time.time + retryCooldownSeconds;
                            float previousMoveAttemptAt = mission.NextMoveAttemptAt;
                            float previousTraversalRetryHoldUntil = mission.TraversalRetryMovementHoldUntil;
                            CommitCapabilityMoveRetry(mission,FollowerCapabilityRetryPolicy.Change.AttemptOrExplicitHold,Mathf.Max(mission.NextMoveAttemptAt, retryHoldUntil));
                            {
                                // V3.31: keep the traversal retry transaction independent from the generic
                                // route-move retry clock. RouteMission rebase legitimately clears
                                // NextMoveAttemptAt, but it must not dissolve a failed traversal edge's
                                // movement hold and revive leader/navigation-recovery routes mid-cooldown.
                                mission.TraversalRetryMovementHoldUntil = Mathf.Max(
                                    mission.TraversalRetryMovementHoldUntil, retryHoldUntil);
                            }
                            if (mission.NextMoveAttemptAt > previousMoveAttemptAt + 0.001f ||
                                mission.TraversalRetryMovementHoldUntil > previousTraversalRetryHoldUntil + 0.001f)
                            {
                                WriteEvent("learningV330RouteMissionRetryMovementHoldArmed", bot, new
                                {
                                    missionId = mission.MissionId,
                                    stepIndex = mission.CurrentStepIndex,
                                    edgeId = step.EdgeId,
                                    kind = step.Kind,
                                    retryCooldownSeconds,
                                    holdUntil = mission.NextMoveAttemptAt,
                                    traversalRetryMovementHoldUntil = mission.TraversalRetryMovementHoldUntil,
                                    action = "holdMovementWithCurrentTraversalCursor"
                                });
                            }
                        }
                        if (ShouldEmitDiagnostic("learningV329RouteMissionRetryBarrier:" + step.EdgeId, bot, 0.75f))
                        {
                            WriteEvent("learningV329RouteMissionTraversalBarrierHeld", bot, new
                            {
                                missionId = mission.MissionId,
                                stepIndex = mission.CurrentStepIndex,
                                edgeId = step.EdgeId,
                                kind = step.Kind,
                                episodeId = step.EpisodeId,
                                reason = stepPolicyReason,
                                action = "holdCurrentStepUntilRetryEligible"
                            });
                        }
                        return true;
                    }

                    EmitLearningV347TraversalOpportunityNoStart(
                        bot, mission, step, "routeMissionCommitmentSelector", stepPolicyReason);
                    if (!TryAdvanceRouteMission(bot, mission, mission.CurrentStepIndex + 1, RouteDebtEvidence.PolicySuppression, "EnsureFollowerRouteMissionCommitment:3", r2DebtObservation3)) return true;
                    mission.ApproachBreadcrumbSequence = Math.Max(mission.ApproachBreadcrumbSequence, step.EndBreadcrumbSequence);
                    ClearLearnedTraversalCommitment(bot, step.EdgeId, "routeMissionCentralPolicySuppressed");
                    if (ShouldEmitDiagnostic("routeMissionCentralPolicy:" + step.EdgeId, bot, 1.25f))
                    {
                        WriteEvent("followerRouteMissionStepPolicySuppressed", bot, new
                        {
                            missionId = mission.MissionId,
                            edgeId = step.EdgeId,
                            kind = step.Kind,
                            episodeId = step.EpisodeId,
                            reason = stepPolicyReason
                        });
                    }
                    continue;
                }

                if (FollowerTraversalRouteCursors.TryGetValue(profileId, out FollowerTraversalRouteCursor cursor) &&
                    cursor != null && step.EndBreadcrumbSequence > 0 &&
                    step.EndBreadcrumbSequence <= cursor.ConsumedBreadcrumbSequence)
                {
                    if (!TryAdvanceRouteMission(bot, mission, mission.CurrentStepIndex + 1, RouteDebtEvidence.CursorFloor, "EnsureFollowerRouteMissionCommitment:4", r2DebtObservation3)) return true;
                    mission.ApproachBreadcrumbSequence = Math.Max(mission.ApproachBreadcrumbSequence, step.EndBreadcrumbSequence);
                    continue;
                }

                TryRefreshLearningV439JumpStepCurrentEpisodeLandingFrontier(
                    bot, mission, step);
                r2DebtObservation3 = CaptureRouteDebtObservation(bot, mission, step);

                float entryDistance = GetPlanarDistance(bot.Position, step.Entry);
                float exitDistance = GetPlanarDistance(bot.Position, step.Exit);
                bool alreadyBelowDropDown = string.Equals(step.Kind, "DropDown", StringComparison.OrdinalIgnoreCase) &&
                                            IsFollowerAlreadyBelowDropDownLanding(bot.Position, step.RawEntry, step.RawExit, step.Exit);
                if (TryConsumeLearningV382PhysicallySubsumedSuccessorDebt(
                        bot, mission, step,
                        out float learningV382SubsumedProjection,
                        out float learningV382SubsumedRequiredProjection))
                {
                    if (!TryAdvanceRouteMission(bot, mission, mission.CurrentStepIndex + 1, RouteDebtEvidence.PhysicalPass, "EnsureFollowerRouteMissionCommitment:5", r2DebtObservation3)) return true;
                    mission.ApproachBreadcrumbSequence = Math.Max(
                        mission.ApproachBreadcrumbSequence, step.EndBreadcrumbSequence);
                    mission.LastProgressAt = Time.time;
                    mission.LastProgressPosition = bot.Position;
                    continue;
                }
                bool physicallyPastTraversal = IsFollowerRouteMissionStepPhysicallyPassed(
                    bot, step, out float stepProjection, out float stepRequiredProjection);
                if (physicallyPastTraversal)
                {
                    ConsumeFollowerRouteMissionPhysicalPass(
                        bot, mission, step,
                        alreadyBelowDropDown ? "alreadyBelowDropDownWithCompletionProof" : "physicallyPastTraversal",
                        stepProjection, stepRequiredProjection);
                    if (!TryAdvanceRouteMission(bot, mission, mission.CurrentStepIndex + 1, RouteDebtEvidence.PhysicalPass, "EnsureFollowerRouteMissionCommitment:6", r2DebtObservation3)) return true;
                    mission.ApproachBreadcrumbSequence = Math.Max(mission.ApproachBreadcrumbSequence, step.EndBreadcrumbSequence);
                    mission.LastProgressAt = Time.time;
                    mission.LastProgressPosition = bot.Position;
                    WriteEvent("followerRouteMissionStepSkippedAlreadyExitSide", bot, new
                    {
                        missionId = mission.MissionId,
                        stepIndex = mission.CurrentStepIndex - 1,
                        episodeId = step.EpisodeId,
                        edgeId = step.EdgeId,
                        kind = step.Kind,
                        entryDistance,
                        exitDistance,
                        alreadyBelowDropDown,
                        physicallyPastTraversal,
                        completionProofRequired =
                            (GetLearningV354ActionProfile(step.Kind).RequiresActuator),
                        projection = stepProjection,
                        requiredProjection = stepRequiredProjection
                    });
                    continue;
                }
                if ((alreadyBelowDropDown) &&
                    GetLearningV354ActionProfile(step.Kind).RequiresActuator &&
                    ShouldEmitDiagnostic("learningV371StrictActuatorPassRejectedNoCompletionProof:" + step.EdgeId, bot, 0.75f))
                {
                    WriteEvent("learningV371StrictActuatorPassRejectedNoCompletionProof", bot, new
                    {
                        missionId = mission.MissionId,
                        stepIndex = mission.CurrentStepIndex,
                        edgeId = step.EdgeId,
                        kind = step.Kind,
                        entryDistance,
                        exitDistance,
                        alreadyBelowDropDown = true,
                        physicallyPastTraversal = false,
                        action = "retainCurrentExecutionDebtNoSuccessCreditNoMissionAdvance"
                    });
                }

                bool learningV384PhysicalLocomotionOwnsFirstAttempt =
                    (IsLearningV369PhysicalCorridorStep(step)) &&
                    !step.LearningV384ActuatorFallbackArmed;
                if (!learningV384PhysicalLocomotionOwnsFirstAttempt &&
                    TryAdoptLearningV381ProvenPortalActuatorStrategy(bot, mission, step))
                {
                    continue;
                }

                if (IsLearningV369PhysicalCorridorStep(step))
                {
                    bool learningV387PassedPhysicalCorridorRouteFrontier = false;
                    float learningV387PhysicalCorridorBodyArc = 0f;
                    float learningV387PhysicalCorridorExitArc = 0f;
                    if ((mission.PhysicalApproachBreadcrumbSequence >= step.StartBreadcrumbSequence) &&
                        TryProjectLearningV375RouteArc(
                            mission, bot.Position,
                            out learningV387PhysicalCorridorBodyArc, out _, out _, out _) &&
                        TryGetLearningV375SequenceArc(
                            mission, step.EndBreadcrumbSequence,
                            out learningV387PhysicalCorridorExitArc, out _) &&
                        learningV387PhysicalCorridorBodyArc >= learningV387PhysicalCorridorExitArc)
                    {
                        learningV387PassedPhysicalCorridorRouteFrontier = true;
                    }

                    if (learningV387PassedPhysicalCorridorRouteFrontier)
                    {
                        int previousIndex = mission.CurrentStepIndex;
                        int previousExecutionFloor = GetFollowerRouteExecutionFloor(profileId);
                        int newExecutionFloor = Math.Max(previousExecutionFloor, step.EndBreadcrumbSequence);
                        FollowerRouteExecutionFloors[profileId] = newExecutionFloor;
                        mission.ResumeFloorBreadcrumbSequence = Math.Max(
                            mission.ResumeFloorBreadcrumbSequence, newExecutionFloor);
                        ConsumeFollowerRouteMissionPhysicalPass(
                            bot, mission, step,
                            "learningV387PhysicalCorridorPassedFiniteRouteFrontier",
                            learningV387PhysicalCorridorBodyArc,
                            learningV387PhysicalCorridorExitArc);
                        MarkFollowerTraversalEpisodeCompleted(bot, step.EpisodeId, step.EdgeId);
                        mission.LastProgressAt = Time.time;
                        mission.LastProgressPosition = bot.Position;
                        ClearLearnedTraversalCommitment(
                            bot, step.EdgeId,
                            "learningV387PhysicalCorridorPassedFiniteRouteFrontier");
                        WriteEvent("learningV387PhysicalCorridorPassedFiniteRouteFrontier", bot, new
                        {
                            missionId = mission.MissionId,
                            stepIndex = previousIndex,
                            episodeId = step.EpisodeId,
                            edgeId = step.EdgeId,
                            bodyArc = learningV387PhysicalCorridorBodyArc,
                            exitArc = learningV387PhysicalCorridorExitArc,
                            startBreadcrumbSequence = step.StartBreadcrumbSequence,
                            endBreadcrumbSequence = step.EndBreadcrumbSequence,
                            executionFloor = newExecutionFloor,
                            traversalSuccessRecorded = false,
                            persistentLearningChanged = false,
                            action = "finiteRouteFrontierConsumesRuntimePhysicalCorridorWithoutRequiringExactFinalBreadcrumbTouch"
                        });
                        continue;
                    }

                    if (mission.PhysicalApproachBreadcrumbSequence >= step.EndBreadcrumbSequence)
                    {
                        int previousIndex = mission.CurrentStepIndex;
                        int previousExecutionFloor = GetFollowerRouteExecutionFloor(profileId);
                        int newExecutionFloor = Math.Max(previousExecutionFloor, step.EndBreadcrumbSequence);
                        FollowerRouteExecutionFloors[profileId] = newExecutionFloor;
                        mission.ResumeFloorBreadcrumbSequence = Math.Max(mission.ResumeFloorBreadcrumbSequence, newExecutionFloor);
                        MarkFollowerTraversalEpisodeCompleted(bot, step.EpisodeId, step.EdgeId);
                        mission.LastProgressAt = Time.time;
                        mission.LastProgressPosition = bot.Position;
                        ClearLearnedTraversalCommitment(bot, step.EdgeId, "learningV369PhysicalCorridorCompleted");
                        WriteEvent("learningV369PhysicalCorridorRouteSegmentCompleted", bot, new
                        {
                            missionId = mission.MissionId,
                            stepIndex = previousIndex,
                            episodeId = step.EpisodeId,
                            edgeId = step.EdgeId,
                            startBreadcrumbSequence = step.StartBreadcrumbSequence,
                            endBreadcrumbSequence = step.EndBreadcrumbSequence,
                            executionFloor = newExecutionFloor,
                            traversalSuccessRecorded = false,
                            persistentLearningChanged = false,
                            action = "continuousPhysicalRouteSegmentAdvancesMissionWithoutWalkThroughExecutor"
                        });
                        continue;
                    }

                    LearnedTraversalCommitments.Remove(profileId);
                    return true;
                }

                bool traversalAdjacentSuccessor = IsRouteMissionSameChainImmediateSuccessor(mission, mission.CurrentStepIndex);
                // RC64: share the exact existing adjacent physical corridor envelope with
                // Native-Proven Jump preparation. Non-Jump admission distances are unchanged.
                bool rc64JumpRunupCommitment = string.Equals(
                    step.Kind, "Jump", StringComparison.OrdinalIgnoreCase);
                float executionCommitDistance = traversalAdjacentSuccessor || rc64JumpRunupCommitment
                    ? RouteMissionTraversalAdjacentBridgeMaxDistance
                    : RouteMissionExecutionCommitDistance;
                if (entryDistance > executionCommitDistance)
                {
                    if (LearnedTraversalCommitments.TryGetValue(profileId, out LearnedTraversalCommitment farCommitment) &&
                        farCommitment != null && farCommitment.RouteMissionId == mission.MissionId)
                    {
                        LearnedTraversalCommitments.Remove(profileId);
                    }
                    return true;
                }

                if (LearnedTraversalCommitments.TryGetValue(profileId, out LearnedTraversalCommitment current) &&
                    current != null && current.RouteMissionId == mission.MissionId &&
                    current.RouteMissionStepIndex == mission.CurrentStepIndex && current.EdgeId == step.EdgeId)
                {
                    // CompileFix18: never refresh one exact traversal latch to the 45 s Route Mission
                    // lifetime. Existing Fix17 commitments loaded into this frame are capped immediately.
                    if (current.ExpiresAt <= 0f)
                    {
                        current.ExpiresAt = Time.time + RouteMissionTraversalCommitmentSeconds;
                    }
                    else
                    {
                        current.ExpiresAt = Mathf.Min(
                            current.ExpiresAt,
                            Time.time + RouteMissionTraversalCommitmentSeconds);
                    }
                    return true;
                }

                LearnedTraversalEdge edge = LearnedTraversalEdges.FirstOrDefault(candidate =>
                    candidate != null && candidate.Id == step.EdgeId);
                if (edge == null || (edge.Disabled && !step.ChainLocalExecutable))
                {
                    if (!TryAdvanceRouteMission(bot, mission, mission.CurrentStepIndex + 1, RouteDebtEvidence.PolicySuppression, "EnsureFollowerRouteMissionCommitment:7", r2DebtObservation3)) return true;
                    continue;
                }

                LearnedTraversalCommitment commitment = CreateLearnedTraversalCommitment(
                    edge, step.Entry, step.Exit, step.RawEntry, step.RawExit, step.EpisodeId);
                if (commitment == null)
                {
                    if (!TryAdvanceRouteMission(bot, mission, mission.CurrentStepIndex + 1, RouteDebtEvidence.PolicySuppression, "EnsureFollowerRouteMissionCommitment:8", r2DebtObservation3)) return true;
                    continue;
                }
                commitment.EpisodeId = step.EpisodeId;
                TraversalEpisodeMetadata episode = TraversalEpisodeStream.LastOrDefault(item => item != null && item.EpisodeId == step.EpisodeId);
                commitment.ChainId = step.ChainId > 0 ? step.ChainId : (episode?.ChainId ?? 0);
                commitment.RouteMissionId = mission.MissionId;
                commitment.RouteMissionStepIndex = mission.CurrentStepIndex;
                commitment.ChainLocalExecutable = step.ChainLocalExecutable;
                commitment.TraversalAdjacentSuccessor = traversalAdjacentSuccessor;
                if ((step.LearningV382TransientNativeSkillInstance) &&
                    step.LearningV382SkillTemplateEdgeId > 0)
                {
                    commitment.SkillTransferTransient = true;
                    commitment.SkillTemplateEdgeId = step.LearningV382SkillTemplateEdgeId;
                    commitment.SkillTransferParentAttemptId = 0;
                    commitment.SkillTransferSource = step.LearningV382SkillTransferSource ??
                        "learningV382ReversePortalNativeSkillInstance";
                    commitment.LearningV382RouteBoundTransientNativeSkill = true;
                    commitment.SkillTransferNearFrontier = step.RawEntry;
                    commitment.SkillTransferFarFrontier = step.RawExit;
                    WriteEvent("learningV382ReversePortalNativeSkillInstance", bot, new
                    {
                        missionId = mission.MissionId,
                        stepIndex = mission.CurrentStepIndex,
                        episodeId = step.EpisodeId,
                        templateEdgeId = commitment.SkillTemplateEdgeId,
                        kind = commitment.Kind,
                        rawEntry = Vec(commitment.RawEntry),
                        rawExit = Vec(commitment.RawExit),
                        persistentEdgeCreated = false,
                        learningAuthority = "transientUntilPhysicalNativeSuccess",
                        action = "reuseExistingV319NativeSkillTransformInsteadOfReverseDirectionAssistFallback"
                    });
                }
                commitment.ExpiresAt = Time.time + RouteMissionTraversalCommitmentSeconds;
                LearnedTraversalCommitments[profileId] = commitment;
                WriteEvent("followerRouteMissionStepCommitted", bot, new
                {
                    missionId = mission.MissionId,
                    stepIndex = mission.CurrentStepIndex,
                    episodeId = step.EpisodeId,
                    edgeId = step.EdgeId,
                    kind = step.Kind,
                    entry = Vec(step.Entry),
                    exit = Vec(step.Exit),
                    entryDistance
                });
                return true;
            }

            RemoveLifecycleMission(profileId, "EnsureFollowerRouteMissionCommitment");
            FollowRejectLeases.Remove(profileId);
            FollowerRouteMissionRearmBlockedUntil[profileId] = Time.time + RouteMissionCompletedRearmBlockSeconds;
            FinalizeLearningV382TerminalRouteFreshFollowFrontier(
                bot, mission, "ensureRouteMissionCommitment");
            WriteEvent(mission.Lifecycle.HasNonPhysicalRetirement ? "followerRouteMissionEndedWithoutPhysicalCompletion" : "followerRouteMissionCompleted", bot, new
            {
                missionId = mission.MissionId,
                source = mission.Source,
                completedSteps = mission.Steps.Count
            });
            return false;
        }

        private static bool TryAddCanonicalRouteMissionStep(
            List<FollowerRouteMissionStep> steps,
            FollowerRouteMissionStep candidate)
        {
            if (steps == null || candidate == null)
            {
                return false;
            }

            // CompileFix49: canonical clustering is best/freshest-wins, never first-wins. A fresh
            // player episode is stronger evidence than an older nearby sibling. Collapse every
            // redundant same-direction member in the local cluster, not only the first one found;
            // reverse-direction siblings are protected by AreRouteMissionWalkThroughStepsRedundant().
            // CF56: the first canonical step and all nonredundant steps need no cluster index
            // allocation. Preserve the exact winner/ordering rules for real collisions.
            if (steps.Count == 0)
            {
                steps.Add(candidate);
                return true;
            }
            List<int> redundantIndices = null;
            FollowerRouteMissionStep winner = candidate;
            bool candidateWins = true;
            int insertionIndex = steps.Count;
            for (int index = 0; index < steps.Count; index++)
            {
                FollowerRouteMissionStep existing = steps[index];
                if (existing == null)
                {
                    continue;
                }
                if (existing.EpisodeId == candidate.EpisodeId)
                {
                    return false;
                }
                if (!AreRouteMissionPassageStepsRedundant(existing, candidate))
                {
                    continue;
                }

                if (redundantIndices == null)
                {
                    redundantIndices = new List<int>();
                }
                redundantIndices.Add(index);
                insertionIndex = Math.Min(insertionIndex, index);
                FollowerRouteMissionStep selected = SelectRouteMissionPassageWinner(existing, winner);
                if (!ReferenceEquals(selected, winner))
                {
                    winner = selected;
                    candidateWins = ReferenceEquals(winner, candidate);
                }
            }

            if (redundantIndices == null)
            {
                steps.Add(candidate);
                return true;
            }

            candidateWins = ReferenceEquals(winner, candidate);
            for (int index = redundantIndices.Count - 1; index >= 0; index--)
            {
                steps.RemoveAt(redundantIndices[index]);
            }
            steps.Insert(Mathf.Clamp(insertionIndex, 0, steps.Count), winner);
            return candidateWins;
        }

        private static bool RebaseFollowerRouteMissionToCurrentPosition(BotOwner bot, FollowerRouteMission mission)
        {
            if(IsCapabilityFullRouteReplay(mission)||IsCapabilityRouteRetryReturning(mission))return false;
            if (bot == null || mission == null || mission.RouteBreadcrumbs == null || mission.RouteBreadcrumbs.Count == 0)
            {
                return false;
            }

            string profileId = bot.ProfileId ?? string.Empty;
            int semanticFloor = Math.Max(GetFollowerRouteResumeFloor(profileId), mission.ResumeFloorBreadcrumbSequence);
            int executionFloor = (GetFollowerRouteExecutionFloor(profileId));
            int physicalFloor = Math.Max(semanticFloor, Math.Max(executionFloor, mission.PhysicalApproachBreadcrumbSequence));

            PlayerBreadcrumbNode best = null;
            float bestDistance = float.MaxValue;
            foreach (PlayerBreadcrumbNode node in mission.RouteBreadcrumbs)
            {
                if (node == null || node.Sequence <= physicalFloor || !IsOperationalBreadcrumbNode(node))
                {
                    continue;
                }
                Vector3 target = GetOperationalBreadcrumbTarget(node);
                float planar = GetPlanarDistance(bot.Position, target);
                float vertical = Mathf.Abs(bot.Position.y - target.y);
                if (planar > RouteMissionRebaseMaxDistance || vertical > RouteMissionRebaseMaxVertical)
                {
                    continue;
                }
                if (best == null || node.Sequence > best.Sequence ||
                    (node.Sequence == best.Sequence && planar < bestDistance))
                {
                    best = node;
                    bestDistance = planar;
                }
            }

            if (best == null || best.Sequence < physicalFloor + RouteMissionRebaseMinSequenceAdvance)
            {
                if ((mission.CurrentStepIndex >= 0) && mission.CurrentStepIndex < mission.Steps.Count)
                {
                    FollowerRouteMissionStep currentBarrier = mission.Steps[mission.CurrentStepIndex];
                    if (currentBarrier != null &&
                        string.Equals(currentBarrier.Kind, "WalkThrough", StringComparison.OrdinalIgnoreCase) &&
                        IsFollowerRouteMissionStepPhysicallyPassed(
                            bot, currentBarrier, out float v366Projection, out float v366RequiredProjection) &&
                        TryProveLearningV366FiniteWalkThroughDownstreamRouteRejoin(
                            bot, mission, currentBarrier, v366Projection, v366RequiredProjection,
                            out PlayerBreadcrumbNode v366Node, out float v366Distance, out _))
                    {
                        best = v366Node;
                        bestDistance = v366Distance;
                    }
                }
                if (best == null || best.Sequence < physicalFloor + RouteMissionRebaseMinSequenceAdvance)
                {
                    return false;
                }
            }

            int previousApproach = mission.ApproachBreadcrumbSequence;
            int previousPhysicalApproach = mission.PhysicalApproachBreadcrumbSequence;
            int previousStep = mission.CurrentStepIndex;
            mission.ObservedDownstreamBreadcrumbSequence = Math.Max(
                mission.ObservedDownstreamBreadcrumbSequence, best.Sequence);

            // V3.48: a player breadcrumb chronology barrier is not follower body truth. If the body
            // is observed physically downstream of an unresolved traversal, retire only the old
            // EXECUTION obligation. This is not traversal success and does not mutate the semantic
            // route cursor/persistent edge. It prevents stale step-0 debt from making new player
            // traversal chains inaccessible for tens of seconds.
            if (mission.CurrentStepIndex >= 0 && mission.CurrentStepIndex < mission.Steps.Count)
            {
                FollowerRouteMissionStep barrierStep = mission.Steps[mission.CurrentStepIndex];
            var r2DebtObservation4 = CaptureRouteDebtObservation(bot, mission, barrierStep, false);

                if (barrierStep != null && barrierStep.EndBreadcrumbSequence > 0 &&
                    best.Sequence >= barrierStep.EndBreadcrumbSequence &&
                    !IsFollowerTraversalEdgeQuarantined(profileId, barrierStep.EdgeId) &&
                    !IsFollowerRouteMissionStepPhysicallyPassed(
                        bot, barrierStep, out float barrierProjection, out float barrierRequiredProjection))
                {
                    bool activeProbeOwnsBarrier = TryGetActiveProbe(bot, out ProbeRuntime barrierProbe) &&
                        barrierProbe != null && barrierProbe.LearnedEdgeId == barrierStep.EdgeId;
                    bool activeAttemptOwnsBarrier = activeProbeOwnsBarrier ||
                        (LearnedTraversalCommitments.TryGetValue(profileId, out LearnedTraversalCommitment liveCommitment) &&
                         liveCommitment != null && liveCommitment.EpisodeId == barrierStep.EpisodeId);
                    bool strictActuatorBarrier = IsLearningV353StrictActuatorBarrierKind(barrierStep.Kind);
                    Vector3 barrierRawExit = IsFinite(barrierStep.RawExit) ? barrierStep.RawExit : barrierStep.Exit;
                    bool learningV365LocalDownstreamBreadcrumb =
                        (bestDistance <= RouteMissionBreadcrumbReachDistance);
                    bool learningV366FiniteDownstreamRouteRejoin =
                        TryProveLearningV366FiniteWalkThroughDownstreamRouteRejoin(
                            bot, mission, barrierStep, barrierProjection, barrierRequiredProjection,
                            out PlayerBreadcrumbNode learningV366DownstreamNode,
                            out float learningV366DownstreamDistance,
                            out float learningV366DownstreamPathLength);
                    bool walkThroughDownstreamRouteDebtProof = (string.Equals(barrierStep.Kind, "WalkThrough", StringComparison.OrdinalIgnoreCase)) &&
                        !activeProbeOwnsBarrier &&
                        barrierRequiredProjection > 0f && barrierProjection >= barrierRequiredProjection &&
                        Mathf.Abs(bot.Position.y - barrierRawExit.y) <= 1.35f &&
                        (learningV365LocalDownstreamBreadcrumb || learningV366FiniteDownstreamRouteRejoin);
                    bool learningV357ExhaustedBarrierSupersession =
                        (strictActuatorBarrier) &&
                        !activeAttemptOwnsBarrier &&
                        mission.LearningV357ExhaustedBarrierStepIndex == mission.CurrentStepIndex &&
                        mission.LearningV357ExhaustedBarrierEdgeId == barrierStep.EdgeId &&
                        mission.LearningV357ExhaustedBarrierEndBreadcrumbSequence > 0 &&
                        best.Sequence >= Math.Max(
                            barrierStep.EndBreadcrumbSequence,
                            mission.LearningV357ExhaustedBarrierEndBreadcrumbSequence);
                    // V367-CF1: keep diagnostic outputs definitely assigned even when the
                    // strict-actuator/ownership guards short-circuit before TryProve runs.
                    // This does not change the bypass predicate or runtime semantics.
                    float learningV367DownstreamPathLength = 0f;
                    float learningV367ActionEntryDistance = float.MaxValue;
                    float learningV367ActionExitDistance = float.MaxValue;
                    bool learningV367DownstreamExecutionDebtBypass =
                        strictActuatorBarrier && !activeAttemptOwnsBarrier &&
                        TryProveLearningV367DownstreamExecutionDebtBypass(
                            bot, mission, barrierStep, best, bestDistance,
                            out learningV367DownstreamPathLength,
                            out learningV367ActionEntryDistance,
                            out learningV367ActionExitDistance);

                    if ((strictActuatorBarrier) &&
                        !learningV357ExhaustedBarrierSupersession &&
                        !learningV367DownstreamExecutionDebtBypass)
                    {
                        WriteEvent("learningV352RouteRebaseChronologyCannotSkipPhysicalBarrier", bot, new
                        {
                            missionId = mission.MissionId,
                            stepIndex = mission.CurrentStepIndex,
                            episodeId = barrierStep.EpisodeId,
                            edgeId = barrierStep.EdgeId,
                            kind = barrierStep.Kind,
                            matchedBreadcrumbSequence = best.Sequence,
                            matchedDistance = bestDistance,
                            projection = barrierProjection,
                            requiredProjection = barrierRequiredProjection,
                            activeAttemptOwnsBarrier,
                            semanticFloor,
                            executionFloor,
                            action = "holdUnresolvedActuatorTraversalUntilBodyProofOrExplicitRecoveryBoundary"
                        });
                        return false;
                    }

                    if ((string.Equals(barrierStep.Kind, "WalkThrough", StringComparison.OrdinalIgnoreCase)) &&
                        !activeProbeOwnsBarrier &&
                        barrierRequiredProjection > 0f && barrierProjection >= barrierRequiredProjection &&
                        !learningV365LocalDownstreamBreadcrumb && !learningV366FiniteDownstreamRouteRejoin &&
                        ShouldEmitDiagnostic(
                            "learningV365NonLocalWalkThroughDebtRetirementRejected:" +
                                barrierStep.EdgeId.ToString(CultureInfo.InvariantCulture),
                            bot, 0.35f))
                    {
                        WriteEvent("learningV365NonLocalWalkThroughDebtRetirementRejected", bot, new
                        {
                            missionId = mission.MissionId,
                            stepIndex = mission.CurrentStepIndex,
                            episodeId = barrierStep.EpisodeId,
                            edgeId = barrierStep.EdgeId,
                            matchedBreadcrumbSequence = best.Sequence,
                            matchedDistance = bestDistance,
                            maxLocalDistance = RouteMissionBreadcrumbReachDistance,
                            projection = barrierProjection,
                            requiredProjection = barrierRequiredProjection,
                            action = "chronologyCannotReplaceFinitePhysicalPassageTruth"
                        });
                    }

                    if (learningV366FiniteDownstreamRouteRejoin)
                    {
                        WriteEvent("learningV366FiniteWalkThroughDownstreamRouteRejoinProven", bot, new
                        {
                            missionId = mission.MissionId,
                            stepIndex = mission.CurrentStepIndex,
                            episodeId = barrierStep.EpisodeId,
                            edgeId = barrierStep.EdgeId,
                            matchedBreadcrumbSequence = learningV366DownstreamNode?.Sequence ?? 0,
                            matchedDistance = learningV366DownstreamDistance,
                            matchedPathLength = learningV366DownstreamPathLength,
                            projection = barrierProjection,
                            requiredProjection = barrierRequiredProjection,
                            traversalSuccessRecorded = false,
                            persistentLearningChanged = false,
                            action = "retireExecutionDebtOnlyBecauseFinitePassageAndLocalDownstreamRouteBothProveBodyEpoch"
                        });
                    }
                    if (learningV367DownstreamExecutionDebtBypass)
                    {
                        WriteEvent("learningV367DownstreamExecutionDebtBypassProven", bot, new
                        {
                            missionId = mission.MissionId,
                            stepIndex = mission.CurrentStepIndex,
                            episodeId = barrierStep.EpisodeId,
                            edgeId = barrierStep.EdgeId,
                            kind = barrierStep.Kind,
                            matchedBreadcrumbSequence = best.Sequence,
                            matchedDistance = bestDistance,
                            matchedPathLength = learningV367DownstreamPathLength,
                            actionEntryDistance = learningV367ActionEntryDistance,
                            actionExitDistance = learningV367ActionExitDistance,
                            traversalSuccessRecorded = false,
                            persistentLearningChanged = false,
                            action = "retireExecutionDebtOnlyBodyAlreadyLocalizedToNewerPlayerRouteEpoch"
                        });
                    }

                    string learningV369BarrierSemanticKind = GetLearningV354SemanticKind(
                        barrierStep.EdgeId, barrierStep.Kind);
                    bool learningV369PhysicalPassageDebt =
                        (GetLearningV354ActionProfile(learningV369BarrierSemanticKind).PhysicalPassageCanProveCompletion);
                    bool learningV369ChronologyOnlyRetirementAllowed =
                        !strictActuatorBarrier && !activeAttemptOwnsBarrier && !learningV369PhysicalPassageDebt;

                    if (((walkThroughDownstreamRouteDebtProof ||
                         learningV357ExhaustedBarrierSupersession ||
                         learningV367DownstreamExecutionDebtBypass ||
                         learningV369ChronologyOnlyRetirementAllowed)))
                    {
                        int previousExecutionFloor = GetFollowerRouteExecutionFloor(profileId);
                        int newExecutionFloor = Math.Max(previousExecutionFloor, barrierStep.EndBreadcrumbSequence);
                        FollowerRouteExecutionFloors[profileId] = newExecutionFloor;
                        mission.ResumeFloorBreadcrumbSequence = Math.Max(mission.ResumeFloorBreadcrumbSequence, newExecutionFloor);
                        mission.PhysicalApproachBreadcrumbSequence = Math.Max(
                            mission.PhysicalApproachBreadcrumbSequence, best.Sequence);
                        mission.ApproachBreadcrumbSequence = Math.Max(
                            mission.ApproachBreadcrumbSequence, best.Sequence);
                        if (!TryAdvanceRouteMission(bot, mission, mission.CurrentStepIndex + 1, RouteDebtEvidence.PhysicalPass, "RebaseFollowerRouteMissionToCurrentPosition:9", r2DebtObservation4)) return false;
                        string retiredDebtReason = walkThroughDownstreamRouteDebtProof
                            ? "learningV353WalkThroughDownstreamRouteDebtRetired"
                            : (learningV357ExhaustedBarrierSupersession
                                ? "learningV357ExhaustedActuatorDebtRetiredByDownstreamBodyEpoch"
                                : (learningV367DownstreamExecutionDebtBypass
                                    ? "learningV367DownstreamExecutionDebtRetired"
                                    : "learningV348DownstreamPhysicalEpochSupersededOldExecutionDebt"));
                        ClearLearnedTraversalCommitment(bot, barrierStep.EdgeId, retiredDebtReason);
                        if (learningV357ExhaustedBarrierSupersession)
                        {
                            mission.LearningV357ExhaustedBarrierStepIndex = -1;
                            mission.LearningV357ExhaustedBarrierEdgeId = 0;
                            mission.LearningV357ExhaustedBarrierEndBreadcrumbSequence = 0;
                            mission.LearningV357ExhaustedBarrierAt = -1f;
                        }
                        WriteEvent(walkThroughDownstreamRouteDebtProof
                            ? "learningV353WalkThroughDownstreamRouteDebtRetired"
                            : (learningV357ExhaustedBarrierSupersession
                                ? "learningV357ExhaustedActuatorDebtRetiredByDownstreamBodyEpoch"
                                : (learningV367DownstreamExecutionDebtBypass
                                    ? "learningV367DownstreamExecutionDebtRetired"
                                    : "learningV348RouteObligationSupersededByDownstreamPhysicalEpoch")), bot, new
                        {
                            missionId = mission.MissionId,
                            previousStepIndex = previousStep,
                            episodeId = barrierStep.EpisodeId,
                            edgeId = barrierStep.EdgeId,
                            kind = barrierStep.Kind,
                            matchedBreadcrumbSequence = best.Sequence,
                            matchedDistance = bestDistance,
                            previousExecutionFloor,
                            newExecutionFloor,
                            projection = barrierProjection,
                            requiredProjection = barrierRequiredProjection,
                            traversalSuccessRecorded = false,
                            persistentLearningChanged = false,
                            routeDebtOnly = walkThroughDownstreamRouteDebtProof ||
                                learningV357ExhaustedBarrierSupersession || learningV367DownstreamExecutionDebtBypass,
                            exhaustedActuatorDebt = learningV357ExhaustedBarrierSupersession,
                            downstreamExecutionDebtBypass = learningV367DownstreamExecutionDebtBypass,
                            reason = walkThroughDownstreamRouteDebtProof
                                ? "monotonicWalkThroughBodyAlreadyDownstreamOfFinitePassage"
                                : (learningV357ExhaustedBarrierSupersession
                                    ? "boundedActuatorRecoveryExhaustedAndFollowerBodyObservedInNewerRouteEpoch"
                                    : (learningV367DownstreamExecutionDebtBypass
                                        ? "unattemptedActuatorDebtBehindLocalizedNewerPlayerRouteEpoch"
                                        : "followerBodyAlreadyInLaterPlayerRouteEpoch"))
                        });
                    }
                    else
                    {
                        WriteEvent("learningV348RouteRebaseBarrierHeldWithoutChronologyFabrication", bot, new
                        {
                            missionId = mission.MissionId,
                            stepIndex = mission.CurrentStepIndex,
                            episodeId = barrierStep.EpisodeId,
                            edgeId = barrierStep.EdgeId,
                            kind = barrierStep.Kind,
                            matchedBreadcrumbSequence = best.Sequence,
                            matchedDistance = bestDistance,
                            physicalApproachBreadcrumbSequence = mission.PhysicalApproachBreadcrumbSequence,
                            chronologyApproachBreadcrumbSequence = mission.ApproachBreadcrumbSequence,
                            activeAttemptOwnsBarrier,
                            activeProbeOwnsBarrier,
                            strictActuatorBarrier,
                            walkThroughDownstreamRouteDebtProof,
                            learningV357ExhaustedBarrierSupersession,
                            learningV367DownstreamExecutionDebtBypass
                        });
                        return false;
                    }
                }
                else
                {
                    mission.PhysicalApproachBreadcrumbSequence = Math.Max(
                        mission.PhysicalApproachBreadcrumbSequence, best.Sequence);
                    mission.ApproachBreadcrumbSequence = Math.Max(
                        mission.ApproachBreadcrumbSequence, best.Sequence);
                }
            }
            else
            {
                mission.PhysicalApproachBreadcrumbSequence = Math.Max(
                    mission.PhysicalApproachBreadcrumbSequence, best.Sequence);
                mission.ApproachBreadcrumbSequence = Math.Max(
                    mission.ApproachBreadcrumbSequence, best.Sequence);
            }

            while (mission.CurrentStepIndex < mission.Steps.Count)
            {
                TryCanonicalizeLearningV334DirectionalWalkThroughSuccessor(bot, mission);
                FollowerRouteMissionStep step = mission.Steps[mission.CurrentStepIndex];
            var r2DebtObservation5 = CaptureRouteDebtObservation(bot, mission, step, false);

                if (step == null || IsFollowerTraversalEdgeQuarantined(profileId, step.EdgeId))
                {
                    if (!TryAdvanceRouteMission(bot, mission, mission.CurrentStepIndex + 1, RouteDebtEvidence.PolicySuppression, "RebaseFollowerRouteMissionToCurrentPosition:10", r2DebtObservation5)) return false;
                    continue;
                }
                if (step.EndBreadcrumbSequence <= mission.PhysicalApproachBreadcrumbSequence &&
                    IsFollowerRouteMissionStepPhysicallyPassed(
                        bot, step, out float rebasePassProjection, out float rebasePassRequiredProjection))
                {
                    ConsumeFollowerRouteMissionPhysicalPass(
                        bot, mission, step, "routeMissionRebase", rebasePassProjection, rebasePassRequiredProjection);
                    if (!TryAdvanceRouteMission(bot, mission, mission.CurrentStepIndex + 1, RouteDebtEvidence.PhysicalPass, "RebaseFollowerRouteMissionToCurrentPosition:11", r2DebtObservation5)) return false;
                    continue;
                }
                break;
            }

            if (LearnedTraversalCommitments.TryGetValue(profileId, out LearnedTraversalCommitment current) &&
                current != null && current.RouteMissionId == mission.MissionId &&
                current.RouteMissionStepIndex < mission.CurrentStepIndex)
            {
                LearnedTraversalCommitments.Remove(profileId);
            }

            bool physicalRebaseProgress =
                mission.PhysicalApproachBreadcrumbSequence > previousPhysicalApproach ||
                mission.CurrentStepIndex > previousStep;
            if ((!physicalRebaseProgress))
            {
                WriteEvent("learningV340RouteMissionBarrierNoOpRebaseSuppressed", bot, new
                {
                    missionId = mission.MissionId,
                    stepIndex = mission.CurrentStepIndex,
                    previousApproachBreadcrumbSequence = previousApproach,
                    previousPhysicalApproachBreadcrumbSequence = previousPhysicalApproach,
                    proposedApproachBreadcrumbSequence = mission.ApproachBreadcrumbSequence,
                    physicalApproachBreadcrumbSequence = mission.PhysicalApproachBreadcrumbSequence,
                    matchedBreadcrumbSequence = best.Sequence,
                    matchedDistance = bestDistance,
                    action = "preserveCurrentStepProducerLifetimeDoNotFabricatePhysicalProgress"
                });
                return false;
            }

            mission.LastProgressAt = Time.time;
            mission.LastProgressPosition = bot.Position;
            CommitCapabilityMoveRetry(mission,FollowerCapabilityRetryPolicy.Change.Reset,0f);
            mission.LocalRecoveryAttempts = 0;
            WriteEvent("followerRouteMissionRebased", bot, new
            {
                missionId = mission.MissionId,
                previousApproachBreadcrumbSequence = previousApproach,
                newApproachBreadcrumbSequence = mission.ApproachBreadcrumbSequence,
                previousPhysicalApproachBreadcrumbSequence = previousPhysicalApproach,
                newPhysicalApproachBreadcrumbSequence = mission.PhysicalApproachBreadcrumbSequence,
                previousStepIndex = previousStep,
                newStepIndex = mission.CurrentStepIndex,
                matchedBreadcrumbSequence = best.Sequence,
                matchedDistance = bestDistance
            });
            return true;
        }

        private static bool TryGetRouteMissionBreadcrumbTarget(
            BotOwner bot,
            FollowerRouteMission mission,
            FollowerRouteMissionStep step,
            out Vector3 target,
            out int targetSequence)
        {
            target = default;
            targetSequence = 0;
            if (bot == null || mission == null || step == null)
            {
                return false;
            }

            int learningV369RouteTargetBoundary = IsLearningV369PhysicalCorridorStep(step)
                ? step.EndBreadcrumbSequence
                : step.StartBreadcrumbSequence;
            if (mission.PhysicalApproachBreadcrumbSequence >= learningV369RouteTargetBoundary)
            {
                return false;
            }

            RefreshRouteMissionBreadcrumbSnapshot(mission);
            // CF51: the route step geometry cannot change during this synchronous scan.
            // Calculate the same V326 entrance axis once, not for every rejected breadcrumb.
            bool cf51WalkThroughApproachSideGate = (string.Equals(step.Kind, "WalkThrough", StringComparison.OrdinalIgnoreCase));
            Vector3 cf51RawEntry = Vector3.zero;
            Vector3 cf51RawExit = Vector3.zero;
            Vector3 cf51Direction = Vector3.zero;
            bool cf51HasWalkThroughEntranceAxis = false;
            if (cf51WalkThroughApproachSideGate)
            {
                cf51RawEntry = IsFinite(step.RawEntry) ? step.RawEntry : step.Entry;
                cf51RawExit = IsFinite(step.RawExit) ? step.RawExit : step.Exit;
                Vector3 cf51Axis = cf51RawExit - cf51RawEntry;
                cf51Axis.y = 0f;
                float cf51Span = cf51Axis.magnitude;
                cf51HasWalkThroughEntranceAxis = cf51Span >= WalkThroughCandidateMinDistance;
                if (cf51HasWalkThroughEntranceAxis)
                {
                    cf51Direction = cf51Axis / cf51Span;
                }
            }
            int cf51RejectedBreadcrumbs = 0;
            int cf51NewestRejectedSequence = 0;
            int cf51OldestRejectedSequence = 0;
            float cf51LastRejectedProjection = 0f;
            float cf51LastRejectedLateral = 0f;
            Vector3 cf51LastRejectedTarget = Vector3.zero;
            PlayerBreadcrumbNode best = null;
            for (int index = mission.RouteBreadcrumbs.Count - 1; index >= 0; index--)
            {
                PlayerBreadcrumbNode node = mission.RouteBreadcrumbs[index];
                if (node == null || node.Sequence <= mission.PhysicalApproachBreadcrumbSequence ||
                    node.Sequence > learningV369RouteTargetBoundary || !IsOperationalBreadcrumbNode(node))
                {
                    continue;
                }
                if (mission.RejectedBreadcrumbSequences.Contains(node.Sequence) ||
                    (node.Sequence == mission.LastFailedBreadcrumbSequence && Time.time < mission.NextMoveAttemptAt + 1.0f))
                {
                    continue;
                }

                Vector3 nodeTarget = GetOperationalBreadcrumbTarget(node);

                // V3.26: StartBreadcrumbSequence is evidence chronology, not navigation authority.
                // Reverse-known WT capture may intentionally choose a late in-portal sample to prove
                // observed locomotion. RouteMission must not then navigate toward that downstream
                // sample with ordinary NavMesh before the WT executor owns movement. Reuse the
                // existing 0.18 m entry-overshoot contract rather than introducing a new tolerance.
                if (cf51HasWalkThroughEntranceAxis)
                {
                    Vector3 offset = nodeTarget - cf51RawEntry;
                    offset.y = 0f;
                    float projection = Vector3.Dot(offset, cf51Direction);
                    Vector3 lateralOffset = offset - cf51Direction * projection;
                    float lateral = lateralOffset.magnitude;
                    if (projection > CompatibilityTraversalEntryMaxForwardOvershoot ||
                        lateral > RouteMissionDirectionalWalkThroughMaxLateral)
                    {
                        // Never change navigation rejection. Publish one bounded diagnosis
                        // for this scan rather than one dictionary key/JSON allocation per node.
                        if (cf51RejectedBreadcrumbs == 0)
                        {
                            cf51NewestRejectedSequence = node.Sequence;
                        }
                        cf51RejectedBreadcrumbs++;
                        cf51OldestRejectedSequence = node.Sequence;
                        cf51LastRejectedProjection = projection;
                        cf51LastRejectedLateral = lateral;
                        cf51LastRejectedTarget = nodeTarget;
                        continue;
                    }
                }

                float direct = GetPlanarDistance(bot.Position, nodeTarget);
                if (direct > RouteMissionBreadcrumbLookAheadDistance)
                {
                    continue;
                }

                bool learningV355RequiresReachableNearSide = ((step.CurrentRaidRouteAuthority || step.ChainLocalExecutable ||
                     IsLearningV339NativeProvenWalkThroughRouteStep(step) ||
                     string.Equals(GetLearningV354SemanticKind(step.EdgeId, step.Kind), "PhysicalCorridor", StringComparison.OrdinalIgnoreCase)));
                if (((IsLearningV339NativeProvenWalkThroughRouteStep(step))) || learningV355RequiresReachableNearSide)
                {
                    // V3.55: chronology never makes an off-NavMesh entry reachable. Select the
                    // newest PRE-ENTRY breadcrumb that ordinary navigation can actually reach, then
                    // transfer authority to the physical corridor/actuator transaction. This is the
                    // generic form of V3.42's proven-WT repair and also covers new action combinations
                    // and long no-NavMesh spans without accepting PathPartial as a fake complete path.
                    string preflightSource = "route-mission-breadcrumb-" +
                        node.Sequence.ToString(CultureInfo.InvariantCulture);
                    if (!CanAcceptFollowPath(
                            bot, nodeTarget, preflightSource,
                            out float preflightPathLength, out float preflightDetourRatio))
                    {
                        if (ShouldEmitDiagnostic(
                            "learningV355UnreachablePreEntryBreadcrumbSkipped:" +
                                step.EdgeId.ToString(CultureInfo.InvariantCulture) + ":" +
                                node.Sequence.ToString(CultureInfo.InvariantCulture),
                            bot,
                            0.35f))
                        {
                            WriteEvent("learningV355UnreachablePreEntryBreadcrumbSkipped", bot, new
                            {
                                missionId = mission.MissionId,
                                stepIndex = mission.CurrentStepIndex,
                                edgeId = step.EdgeId,
                                breadcrumbSequence = node.Sequence,
                                target = Vec(nodeTarget),
                                directDistance = direct,
                                pathLength = preflightPathLength,
                                detourRatio = preflightDetourRatio,
                                action = "continueBackwardToNewestReachablePreEntryBreadcrumb"
                            });
                        }
                        continue;
                    }
                }

                best = node;
                break;
            }

            if (cf51RejectedBreadcrumbs > 0 && ShouldEmitDiagnostic(
                "learningV326RouteMissionWalkThroughDownstreamBreadcrumbSuppressed:" +
                    mission.MissionId.ToString(CultureInfo.InvariantCulture) + ":" +
                    mission.CurrentStepIndex.ToString(CultureInfo.InvariantCulture) + ":" +
                    step.EpisodeId.ToString(CultureInfo.InvariantCulture), bot, 0.35f))
            {
                WriteEvent("learningV326RouteMissionWalkThroughDownstreamBreadcrumbSuppressed", bot, new
                {
                    missionId = mission.MissionId,
                    stepIndex = mission.CurrentStepIndex,
                    edgeId = step.EdgeId,
                    breadcrumbSequence = cf51OldestRejectedSequence,
                    cf51NewestRejectedSequence,
                    cf51OldestRejectedSequence,
                    cf51RejectedBreadcrumbs,
                    projection = cf51LastRejectedProjection,
                    maxForwardProjection = CompatibilityTraversalEntryMaxForwardOvershoot,
                    lateral = cf51LastRejectedLateral,
                    maxLateral = RouteMissionDirectionalWalkThroughMaxLateral,
                    rawEntry = Vec(cf51RawEntry),
                    rawExit = Vec(cf51RawExit),
                    target = Vec(cf51LastRejectedTarget),
                    reason = "episodeEvidenceSequenceIsNotRouteApproachAuthority",
                    observationOnly = true,
                    action = "groupExactStepRejectionsWithoutChangingTargetSearch"
                });
            }
            if (best == null)
            {
                return false;
            }

            target = GetOperationalBreadcrumbTarget(best);
            targetSequence = best.Sequence;
            return IsFinite(target);
        }

        private static bool TryRetireLearningV382SupersededPhysicalNavigationEpochForMission(
            BotOwner bot,
            FollowerRouteMission mission,
            string source)
        {
            if ((bot == null) || mission?.Steps == null ||
                mission.Steps.Count == 0 ||
                !HasLearningV364UnsynchronizedPhysicalNavigationEpoch(
                    bot, out PhysicalNavigationEpochHandoff handoff) ||
                handoff == null || !handoff.FreshConsumerPathObserved)
            {
                return false;
            }

            FollowerRouteMissionStep first = mission.Steps
                .Skip(Math.Max(0, mission.CurrentStepIndex))
                .FirstOrDefault(step => step != null && step.CurrentRaidRouteAuthority);
            if (first == null || first.ChainId <= 0 || handoff.ChainId <= 0 ||
                first.ChainId == handoff.ChainId)
            {
                return false;
            }

            Vector3 body = bot.GetPlayer != null ? bot.GetPlayer.Position : bot.Position;
            if (!IsFinite(body))
            {
                return false;
            }

            bool scrubbed = TryScrubLearningV364NativeMoverEpochAnchor(
                bot,
                body,
                "learningV382CrossChainBodyFrontier:" + (source ?? "routeMission"),
                out string scrubDetail);

            // The old physical-navigation epoch is semantic barrier state, not a mover-path lease.
            // Once a different current-raid chain owns the new route and the old epoch already
            // observed a fresh consumer path, inability to scrub a now-absent native mover must not
            // keep the obsolete cross-chain projection barrier alive.
            PhysicalNavigationEpochHandoffs.Remove(bot.ProfileId);
            WriteEvent("learningV382SupersededPhysicalNavigationEpochRetired", bot, new
            {
                missionId = mission.MissionId,
                oldEdgeId = handoff.SourceEdgeId,
                oldEpisodeId = handoff.EpisodeId,
                oldChainId = handoff.ChainId,
                newEpisodeId = first.EpisodeId,
                newEdgeId = first.EdgeId,
                newChainId = first.ChainId,
                newKind = first.Kind,
                body = Vec(body),
                freshConsumerPathSource = handoff.FreshConsumerPathSource ?? string.Empty,
                staleMoverScrubbed = scrubbed,
                scrubDetail = scrubDetail ?? string.Empty,
                traversalSuccessRecorded = false,
                persistentLearningChanged = false,
                action = "newCurrentRaidChainStartsFromCurrentBodyDoNotCarryOldPhysicalEpochProjectionBarrierAcrossChains"
            });
            return true;
        }

        private static bool ConsumeFollowerRouteMissionPhysicalPass(
            BotOwner bot,
            FollowerRouteMission mission,
            FollowerRouteMissionStep step,
            string source,
            float projection,
            float requiredProjection)
        {
            string profileId = bot?.ProfileId ?? string.Empty;
            if (string.IsNullOrEmpty(profileId) || step == null || step.EpisodeId <= 0 || step.EndBreadcrumbSequence <= 0)
            {
                return false;
            }

            if (!FollowerTraversalRouteCursors.TryGetValue(profileId, out FollowerTraversalRouteCursor cursor) || cursor == null)
            {
                cursor = new FollowerTraversalRouteCursor();
                FollowerTraversalRouteCursors[profileId] = cursor;
            }

            int previousCompletedEpisodeId = cursor.LastCompletedEpisodeId;
            int previousCompletedChainId = cursor.LastCompletedChainId;
            int previousConsumedBreadcrumbSequence = cursor.ConsumedBreadcrumbSequence;
            int previousPreloadedEpisodeId = cursor.PreloadedEpisodeId;

            // Physical passage consumes route identity but does NOT grant Native Success.
            CommitFollowerTraversalRouteCursor(
                cursor, step.EpisodeId, step.ChainId, step.EndBreadcrumbSequence,
                FollowerMovementV2IntentGateway.RouteCursorMutationKind.MeasuredPhysicalPass);

            // Make the same consumption floor visible to this live mission immediately. New missions
            // already consult FollowerTraversalRouteCursor; this closes the current-mission producer /
            // consumer ordering gap without globally cancelling valid downstream intent.
            if (mission != null)
            {
                mission.ResumeFloorBreadcrumbSequence = Math.Max(
                    mission.ResumeFloorBreadcrumbSequence,
                    cursor.ConsumedBreadcrumbSequence);
            }

            bool breadcrumbCommitmentCleared = false;
            if (BreadcrumbFollowCommitments.TryGetValue(profileId, out BreadcrumbFollowCommitment breadcrumbCommitment) &&
                breadcrumbCommitment != null && breadcrumbCommitment.TargetSequence <= cursor.ConsumedBreadcrumbSequence)
            {
                BreadcrumbFollowCommitments.Remove(profileId);
                breadcrumbCommitmentCleared = true;
            }

            bool reservationCleared = false;
            if (ProvisionalWalkThroughReservations.TryGetValue(profileId, out ProvisionalWalkThroughReservation reservation) &&
                reservation != null)
            {
                TraversalEpisodeMetadata reservationEpisode = TraversalEpisodeStream.LastOrDefault(item =>
                    item != null && item.EpisodeId == reservation.EpisodeId);
                bool reservationConsumed = reservation.EpisodeId == step.EpisodeId ||
                    (reservationEpisode != null && reservationEpisode.EndBreadcrumbSequence > 0 &&
                     reservationEpisode.EndBreadcrumbSequence <= cursor.ConsumedBreadcrumbSequence);
                if (reservationConsumed)
                {
                    ProvisionalWalkThroughReservations.Remove(profileId);
                    reservationCleared = true;
                }
            }

            // A commitment for exactly the consumed episode is stale by definition. Do not clear a
            // newer downstream commitment that may already have been preloaded by the same mission.
            bool traversalCommitmentCleared = false;
            if (LearnedTraversalCommitments.TryGetValue(profileId, out LearnedTraversalCommitment commitment) &&
                commitment != null &&
                (commitment.EpisodeId == step.EpisodeId ||
                 (commitment.EdgeId == step.EdgeId && commitment.RouteMissionId == (mission?.MissionId ?? 0) &&
                  commitment.RouteMissionStepIndex <= (mission?.CurrentStepIndex ?? int.MaxValue))))
            {
                LearnedTraversalCommitments.Remove(profileId);
                traversalCommitmentCleared = true;
            }

            bool learningV421EpisodeDebtIdentityMarked = false;
            {
                TraversalEpisodeMetadata consumedEpisode = TraversalEpisodeStream.LastOrDefault(item =>
                    item != null && item.EpisodeId == step.EpisodeId);
                if (consumedEpisode != null && !consumedEpisode.LearningV386ExecutionDebtConsumedByPhysicalPassage)
                {
                    consumedEpisode.LearningV386ExecutionDebtConsumedByPhysicalPassage = true;
                    consumedEpisode.LearningV386ExecutionDebtPhysicalPassageSource =
                        "learningV421CanonicalRoutePhysicalPassConsumed";
                    learningV421EpisodeDebtIdentityMarked = true;
                }
            }

            bool changed = cursor.LastCompletedEpisodeId != previousCompletedEpisodeId ||
                           cursor.LastCompletedChainId != previousCompletedChainId ||
                           cursor.ConsumedBreadcrumbSequence != previousConsumedBreadcrumbSequence ||
                           cursor.PreloadedEpisodeId != previousPreloadedEpisodeId ||
                           learningV421EpisodeDebtIdentityMarked;

            WriteEvent("followerRouteMissionPhysicalPassConsumed", bot, new
            {
                missionId = mission?.MissionId ?? 0,
                episodeId = step.EpisodeId,
                edgeId = step.EdgeId,
                kind = step.Kind,
                chainId = step.ChainId,
                source = source ?? "physicalPass",
                previousCompletedEpisodeId,
                completedEpisodeId = cursor.LastCompletedEpisodeId,
                previousConsumedBreadcrumbSequence,
                consumedBreadcrumbSequence = cursor.ConsumedBreadcrumbSequence,
                projection,
                requiredProjection,
                breadcrumbCommitmentCleared,
                reservationCleared,
                traversalCommitmentCleared,
                learningV421EpisodeDebtIdentityMarked,
                learningAuthorityGranted = false,
                changed
            });
            return changed;
        }

        private static bool MarkLearningV388PhysicalPassageExecutionDebtConsumed(
            BotOwner bot, FollowerRouteMissionStep step, string source)
        {
            if ((step == null) ||
                step.EpisodeId <= 0)
            {
                return false;
            }
            LearningV354TraversalCapabilityProfile profile = GetLearningV354ActionProfile(step.Kind);
            if (profile == null || !profile.PhysicalPassageCanProveCompletion)
            {
                return false;
            }
            TraversalEpisodeMetadata episode = TraversalEpisodeStream.LastOrDefault(item =>
                item != null && item.EpisodeId == step.EpisodeId);
            if (episode == null)
            {
                return false;
            }
            bool changed = !episode.LearningV386ExecutionDebtConsumedByPhysicalPassage;
            episode.LearningV386ExecutionDebtConsumedByPhysicalPassage = true;
            episode.LearningV386ExecutionDebtPhysicalPassageSource =
                string.IsNullOrWhiteSpace(source)
                    ? "learningV388PhysicalPassageExecutionDebtConsumed"
                    : source;
            if (changed)
            {
                WriteEvent("learningV388PhysicalPassageExecutionDebtConsumed", bot, new
                {
                    episodeId = step.EpisodeId,
                    edgeId = step.EdgeId,
                    kind = step.Kind,
                    startBreadcrumbSequence = step.StartBreadcrumbSequence,
                    endBreadcrumbSequence = step.EndBreadcrumbSequence,
                    source = episode.LearningV386ExecutionDebtPhysicalPassageSource,
                    traversalSuccessRecorded = false,
                    persistentLearningChanged = false,
                    action = "physicalCompletionRetiresOnlyRaidLocalExecutionIdentitySemanticKnowledgeRemainsReadable"
                });
            }
            return changed;
        }

        private static bool TryConsumeLearningV408ImmediatePhysicalPassageCompletion(
            BotOwner bot,
            FollowerRouteMission mission,
            FollowerTraversalRouteCursor cursor)
        {
            if ((bot == null) || mission == null ||
                cursor == null || !cursor.LearningV375TransactionActive ||
                cursor.LearningV375RouteMissionId != mission.MissionId || HasActiveTraversalProbe(bot) ||
                mission.CurrentStepIndex < 0 || mission.CurrentStepIndex >= mission.Steps.Count)
            {
                return false;
            }

            FollowerRouteMissionStep step = mission.Steps[mission.CurrentStepIndex];
            LearningV354TraversalCapabilityProfile profile = step != null
                ? GetLearningV354ActionProfile(
                    GetLearningV354SemanticKind(step.EdgeId, step.Kind))
                : null;
            if (step == null || step.EpisodeId <= 0 || step.EndBreadcrumbSequence <= 0 ||
                profile == null || profile.RequiresActuator || !profile.PhysicalPassageCanProveCompletion ||
                !IsFollowerRouteMissionStepPhysicallyPassed(
                    bot, step, out float projection, out float requiredProjection))
            {
                return false;
            }

            // V409 source-grounded correction: the broad V388 watchdog predicate also accepts a
            // near-exit proxy. That evidence is useful for bounded recovery, but it is not strong
            // enough to transfer LIVE movement authority. A live consumer advances only after the
            // current physical body actually crosses the directed passage projection.
            if ((projection + 0.001f < requiredProjection))
            {
                if (ShouldEmitDiagnostic(
                        "learningV409NearExitPhysicalCompletionDeferred:" +
                            mission.MissionId.ToString(CultureInfo.InvariantCulture) + ":" +
                            mission.CurrentStepIndex.ToString(CultureInfo.InvariantCulture), bot, 0.35f))
                {
                    WriteEvent("learningV409NearExitPhysicalCompletionDeferred", bot, new
                    {
                        missionId = mission.MissionId,
                        stepIndex = mission.CurrentStepIndex,
                        episodeId = step.EpisodeId,
                        edgeId = step.EdgeId,
                        kind = step.Kind,
                        projection,
                        requiredProjection,
                        body = Vec(bot.Position),
                        traversalSuccessRecorded = false,
                        persistentLearningChanged = false,
                        action = "nearExitIsRecoveryEvidenceNotLiveStepCompletionAuthority"
                    });
                }
                return false;
            }

            // V4.56: V427 can already prove that the body has reached the predecessor boundary
            // while the immediate rolling actuator still needs the SAME physical producer to finish
            // centering/alignment. V408 used to ignore that live lease and consume the corridor anyway,
            // which is exactly the fence regression: Jump inherited a body already off its takeoff lane.
            // Prefer an atomic V388 handoff when ready; otherwise keep the current step/producer alive
            // with the existing V427 staging target. No new admission geometry is introduced here.
            if ((IsLearningV369PhysicalCorridorStep(step)) &&
                mission.CurrentStepIndex + 1 < mission.Steps.Count)
            {
                if (TryHandoffLearningV388PhysicalCorridorToReadySuccessor(bot, mission, step))
                {
                    WriteEvent("learningV456PhysicalCorridorRetiredIntoReadyConsumer", bot, new
                    {
                        missionId = mission.MissionId,
                        predecessorStepIndex = mission.CurrentStepIndex,
                        predecessorEdgeId = step.EdgeId,
                        predecessorKind = step.Kind,
                        projection,
                        requiredProjection,
                        traversalSuccessRecorded = false,
                        persistentLearningChanged = false,
                        bodyRelocated = false,
                        action = "physicalPassageCompletionTransfersDirectlyToAlreadyReadyRollingConsumer"
                    });
                    return true;
                }

                string learningV456ProfileId = bot.ProfileId ?? string.Empty;
                if (!string.IsNullOrEmpty(learningV456ProfileId) &&
                    LearningV365ContinuousRouteProducerStates.TryGetValue(
                        learningV456ProfileId, out LearningV365ContinuousRouteProducerState learningV456Producer) &&
                    learningV456Producer != null && !learningV456Producer.Generic &&
                    learningV456Producer.MissionId == mission.MissionId &&
                    learningV456Producer.StepIndex == mission.CurrentStepIndex &&
                    TryLeaseLearningV427PendingRollingSuccessorBoundary(
                        bot, mission, step, learningV456Producer))
                {
                    FollowerRouteMissionStep learningV456Successor =
                        mission.Steps[mission.CurrentStepIndex + 1];
                    WriteEvent("learningV456PhysicalCorridorCompletionDeferredForConsumerAdmission", bot, new
                    {
                        missionId = mission.MissionId,
                        predecessorStepIndex = mission.CurrentStepIndex,
                        predecessorEdgeId = step.EdgeId,
                        predecessorKind = step.Kind,
                        successorStepIndex = mission.CurrentStepIndex + 1,
                        successorEdgeId = learningV456Successor?.EdgeId ?? 0,
                        successorKind = learningV456Successor?.Kind ?? string.Empty,
                        projection,
                        requiredProjection,
                        body = Vec(bot.Position),
                        stagingTarget = Vec(learningV456Producer.Target),
                        traversalSuccessRecorded = false,
                        persistentLearningChanged = false,
                        bodyRelocated = false,
                        action = "doNotRetirePredecessorWhileExistingBoundaryLeaseIsStillPreparingImmediateRollingConsumer"
                    });
                    // false here means "V408 did not consume the step". The caller must continue
                    // into the existing persistent-producer update in this same frame so the V427
                    // staging target actually moves the body. Returning true would suppress that
                    // producer and create the very dead-owner gap this contract is eliminating.
                    return false;
                }
            }

            string profileId = bot.ProfileId ?? string.Empty;
            int previousStepIndex = mission.CurrentStepIndex;
            int previousExecutionFloor = GetFollowerRouteExecutionFloor(profileId);
            int newExecutionFloor = Math.Max(previousExecutionFloor, step.EndBreadcrumbSequence);
            if (!string.IsNullOrEmpty(profileId))
            {
                FollowerRouteExecutionFloors[profileId] = newExecutionFloor;
            }
            mission.ResumeFloorBreadcrumbSequence = Math.Max(
                mission.ResumeFloorBreadcrumbSequence, newExecutionFloor);

            bool routeIdentityChanged = ConsumeFollowerRouteMissionPhysicalPass(
                bot, mission, step,
                "learningV408ImmediatePhysicalPassageCompletion",
                projection, requiredProjection);
            bool executionDebtChanged = MarkLearningV388PhysicalPassageExecutionDebtConsumed(
                bot, step, "learningV408ImmediatePhysicalPassageCompletion");

            // MarkFollowerTraversalEpisodeCompleted remains the sole mission-step advancement writer
            // (V390). This path only moves that already-proven physical completion earlier in the
            // same body epoch; it does not grant traversal/native/persistent success.
            MarkFollowerTraversalEpisodeCompleted(bot, step.EpisodeId, step.EdgeId);
            ClearLearningV365ContinuousPhysicalRoute(
                bot, "learningV408ImmediatePhysicalPassageCompletion");

            WriteEvent("learningV408ImmediatePhysicalPassageCompletion", bot, new
            {
                missionId = mission.MissionId,
                previousStepIndex,
                currentStepIndex = mission.CurrentStepIndex,
                episodeId = step.EpisodeId,
                edgeId = step.EdgeId,
                kind = step.Kind,
                projection,
                requiredProjection,
                previousExecutionFloor,
                newExecutionFloor,
                routeIdentityChanged,
                executionDebtChanged,
                traversalSuccessRecorded = false,
                nativeSuccessRecorded = false,
                persistentLearningChanged = false,
                recoveryTeleportUsed = false,
                bodyRelocated = false,
                action = ("strictDirectedBodyCrossingRetiresRuntimeDebtAndHandsRouteToSuccessor")
            });
            {
                WriteEvent("learningV409StrictPhysicalPassageCompletion", bot, new
                {
                    missionId = mission.MissionId,
                    previousStepIndex,
                    currentStepIndex = mission.CurrentStepIndex,
                    episodeId = step.EpisodeId,
                    edgeId = step.EdgeId,
                    kind = step.Kind,
                    projection,
                    requiredProjection,
                    traversalSuccessRecorded = false,
                    persistentLearningChanged = false,
                    action = "onlyMeasuredDirectedBodyCrossingTransfersLiveStepAuthority"
                });
            }
            return true;
        }

        private static bool IsFollowerRouteMissionStepPhysicallyPassed(
            BotOwner bot,
            FollowerRouteMissionStep step,
            out float projection,
            out float requiredProjection)
        {
            projection = 0f;
            requiredProjection = 0f;
            if (bot == null || step == null || !IsFinite(bot.Position))
            {
                return false;
            }

            float entryDistance = GetPlanarDistance(bot.Position, step.Entry);
            float exitDistance = GetPlanarDistance(bot.Position, step.Exit);
            bool isDropDown = string.Equals(step.Kind, "DropDown", StringComparison.OrdinalIgnoreCase);
            bool asymmetricBarrier =
                string.Equals(step.Kind, "Vault", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(step.Kind, "Jump", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(step.Kind, "Climb", StringComparison.OrdinalIgnoreCase);
            bool typedTraversalRequiresCompletion = asymmetricBarrier ||
                (true && isDropDown);
            bool recentMatchingCompletion = false;
            if (true && typedTraversalRequiresCompletion)
            {
                string profileId = bot.ProfileId ?? string.Empty;
                string completionKey = MakeRecentTraversalCompletionKey(profileId, step.EdgeId);
                recentMatchingCompletion = !string.IsNullOrEmpty(profileId) &&
                    RecentTraversalCompletions.TryGetValue(completionKey, out RecentTraversalCompletion recentCompletion) &&
                    recentCompletion != null && IsLifecycleCompletionForStep(bot,recentCompletion,step) && Time.time <= recentCompletion.ExpiresAt &&
                    IsFinite(recentCompletion.Exit) &&
                    GetPlanarDistance(bot.Position, recentCompletion.Exit) <= 1.75f;
                if (!recentMatchingCompletion)
                {
                    return false;
                }
            }
            if (isDropDown &&
                IsFollowerAlreadyBelowDropDownLanding(bot.Position, step.RawEntry, step.RawExit, step.Exit))
            {
                return !true || recentMatchingCompletion;
            }

            Vector3 rawEntry = IsFinite(step.RawEntry) ? step.RawEntry : step.Entry;
            Vector3 rawExit = IsFinite(step.RawExit) ? step.RawExit : step.Exit;
            Vector3 axis = rawExit - rawEntry;
            axis.y = 0f;
            float length = axis.magnitude;
            if(IsLearningV369PhysicalCorridorStep(step))
            {
                var finiteAxis=GetFlatDirection(rawExit-rawEntry);
                projection=Vector3.Dot(GetFlatDelta(rawEntry,bot.Position),finiteAxis);
                requiredProjection=length;
                return FollowerCapabilityPassagePolicy.CorridorExit(bot.GetPlayer?.MovementContext?.IsGrounded==true,
                    length,projection,GetPlanarDistanceToSegment(bot.Position,rawEntry,rawExit),
                    RouteMissionDirectionalWalkThroughMaxLateral,bot.Position.y-rawExit.y);
            }
            if (length < 0.25f)
            {
                return exitDistance <= 1.20f && exitDistance + 0.25f < entryDistance;
            }

            Vector3 direction = axis / length;
            Vector3 fromEntry = bot.Position - rawEntry;
            fromEntry.y = 0f;
            projection = Vector3.Dot(fromEntry, direction);
            requiredProjection = Mathf.Max(0.12f, length - 0.25f);
            bool verticalCredible = Mathf.Abs(bot.Position.y - rawExit.y) <= 1.35f;
            bool nearExit = exitDistance <= 1.35f && exitDistance + 0.25f < entryDistance;
            if (true &&
                string.Equals(step.Kind, "WalkThrough", StringComparison.OrdinalIgnoreCase))
            {
                // V3.41 could consume a WT from arbitrarily far away when the follower happened to
                // lie on the infinite extension of RawEntry->RawExit. A physical-pass proof must be
                // local to the finite authored corridor. Reuse the established directional WT
                // lateral contract; do not widen distance/projection thresholds.
                float localCorridorDistance = GetPlanarDistanceToSegment(bot.Position, rawEntry, rawExit);
                bool localCorridorCredible =
                    localCorridorDistance <= RouteMissionDirectionalWalkThroughMaxLateral;
                bool oldInfiniteAxisWouldPass =
                    verticalCredible && (projection >= requiredProjection || nearExit);
                if (oldInfiniteAxisWouldPass && !localCorridorCredible &&
                    ShouldEmitDiagnostic(
                        "learningV342WalkThroughPhysicalPassRejectedNonLocal:" +
                            step.EdgeId.ToString(CultureInfo.InvariantCulture),
                        bot,
                        0.50f))
                {
                    WriteEvent("learningV342WalkThroughPhysicalPassRejectedNonLocal", bot, new
                    {
                        edgeId = step.EdgeId,
                        episodeId = step.EpisodeId,
                        entryDistance,
                        exitDistance,
                        projection,
                        requiredProjection,
                        localCorridorDistance,
                        maxLocalCorridorDistance = RouteMissionDirectionalWalkThroughMaxLateral,
                        action = "retainRouteStepDoNotConsumeFromInfiniteAxisProjection"
                    });
                }
                return oldInfiniteAxisWouldPass && localCorridorCredible;
            }
            return verticalCredible && (projection >= requiredProjection || nearExit);
        }

        private static int GetLatestTraversalEpisodeId()
        {
            TraversalEpisodeMetadata latest = TraversalEpisodeStream.LastOrDefault(item => item != null);
            return latest?.EpisodeId ?? 0;
        }

        private static bool IsRouteMissionPathSource(string source)
        {
            return !string.IsNullOrEmpty(source) &&
                   (source.StartsWith("route-mission-", StringComparison.OrdinalIgnoreCase) ||
                    source.StartsWith("rejected-route-", StringComparison.OrdinalIgnoreCase));
        }

        private static bool AreRouteMissionWalkThroughStepsRedundant(
            FollowerRouteMissionStep existing,
            FollowerRouteMissionStep candidate)
        {
            if (existing == null || candidate == null ||
                !string.Equals(existing.Kind, "WalkThrough", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(candidate.Kind, "WalkThrough", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            Vector3 existingDirection = GetFlatDirection(existing.RawExit - existing.RawEntry);
            Vector3 candidateDirection = GetFlatDirection(candidate.RawExit - candidate.RawEntry);
            if (existingDirection.sqrMagnitude < 0.5f || candidateDirection.sqrMagnitude < 0.5f)
            {
                return false;
            }

            int sequenceGap = Math.Min(
                Math.Abs(existing.EndBreadcrumbSequence - candidate.StartBreadcrumbSequence),
                Math.Abs(candidate.EndBreadcrumbSequence - existing.StartBreadcrumbSequence));
            if (sequenceGap > 24)
            {
                return false;
            }

            if ((existing.EdgeId > 0) && candidate.EdgeId > 0 && sequenceGap <= 3 &&
                IsLearningV376RouteStepEquivalentToEdgeId(candidate, existing.EdgeId))
            {
                // One canonical directional WT edge observed by adjacent recorder episodes is one
                // physical passage obligation. Do not recreate the same execution debt after the
                // predecessor has already physically crossed it. Reverse traversal has a distinct
                // directional edge identity and therefore does not enter this branch.
                return true;
            }

            float existingSpan = GetPlanarDistance(existing.RawEntry, existing.RawExit);
            float candidateSpan = GetPlanarDistance(candidate.RawEntry, candidate.RawExit);
            FollowerRouteMissionStep micro = existingSpan < WalkThroughStandalonePersistMinDistance ? existing :
                candidateSpan < WalkThroughStandalonePersistMinDistance ? candidate : null;
            FollowerRouteMissionStep passage = ReferenceEquals(micro, existing) ? candidate :
                ReferenceEquals(micro, candidate) ? existing : null;
            if (micro != null && passage != null &&
                GetPlanarDistance(passage.RawEntry, passage.RawExit) >= WalkThroughStandalonePersistMinDistance)
            {
                // CompileFix50 Passage Bundle: a sub-0.85m rough-ground seam or terminal sidestep
                // is a sample of the enclosing player passage, not another mandatory traversal.
                // This deliberately does not require identical heading; the junk-pile regression
                // produced tiny perpendicular edges at the end of a valid 3-4m WalkThrough.
                bool microContained =
                    GetPlanarDistanceToSegment(micro.RawEntry, passage.RawEntry, passage.RawExit) <= 0.85f &&
                    GetPlanarDistanceToSegment(micro.RawExit, passage.RawEntry, passage.RawExit) <= 0.85f;
                bool terminalContinuation =
                    GetPlanarDistance(micro.RawEntry, passage.RawExit) <= 0.75f &&
                    GetPlanarDistance(micro.RawExit, passage.RawExit) <= 0.95f;
                if (microContained || terminalContinuation)
                {
                    return true;
                }
            }

            // Same-direction passage variants are redundant when their authored anchors are near.
            // Reverse siblings remain protected because their direction dot is negative.
            if (Vector3.Dot(existingDirection, candidateDirection) < 0.94f)
            {
                return false;
            }
            bool geometryNear = GetPlanarDistance(existing.RawEntry, candidate.RawEntry) <= 0.80f &&
                                GetPlanarDistance(existing.RawExit, candidate.RawExit) <= 1.00f &&
                                Mathf.Abs(existing.RawEntry.y - candidate.RawEntry.y) <= 0.55f &&
                                Mathf.Abs(existing.RawExit.y - candidate.RawExit.y) <= 0.65f;
            if (geometryNear)
            {
                return true;
            }

            {
                // Endpoint-near clustering misses partial/contained observations of the same doorway
                // or bridge passage. Bundle only same-direction finite-corridor overlap; sequential
                // non-overlapping passages and reverse siblings remain distinct.
                bool candidateInsideExisting =
                    GetPlanarDistanceToSegment(candidate.RawEntry, existing.RawEntry, existing.RawExit) <=
                        RouteMissionDirectionalWalkThroughMaxLateral &&
                    GetPlanarDistanceToSegment(candidate.RawExit, existing.RawEntry, existing.RawExit) <=
                        RouteMissionDirectionalWalkThroughMaxLateral;
                bool existingInsideCandidate =
                    GetPlanarDistanceToSegment(existing.RawEntry, candidate.RawEntry, candidate.RawExit) <=
                        RouteMissionDirectionalWalkThroughMaxLateral &&
                    GetPlanarDistanceToSegment(existing.RawExit, candidate.RawEntry, candidate.RawExit) <=
                        RouteMissionDirectionalWalkThroughMaxLateral;
                if (candidateInsideExisting || existingInsideCandidate)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool AreRouteMissionPassageStepsRedundant(
            FollowerRouteMissionStep existing,
            FollowerRouteMissionStep candidate)
        {
            if (existing == null || candidate == null)
            {
                return false;
            }
            if (AreRouteMissionWalkThroughStepsRedundant(existing, candidate))
            {
                return true;
            }

            bool existingWalk = string.Equals(existing.Kind, "WalkThrough", StringComparison.OrdinalIgnoreCase);
            bool candidateWalk = string.Equals(candidate.Kind, "WalkThrough", StringComparison.OrdinalIgnoreCase);
            if (existingWalk == candidateWalk)
            {
                // Do not collapse two semantic traversals (Vault -> DropDown, Jump -> DropDown, ...)
                // merely because they are close. Passage bundling only removes a WalkThrough fragment
                // that is describing the same obstacle action already represented by a stronger kind.
                return false;
            }

            FollowerRouteMissionStep walk = existingWalk ? existing : candidate;
            FollowerRouteMissionStep semantic = existingWalk ? candidate : existing;
            Vector3 walkDirection = GetFlatDirection(walk.RawExit - walk.RawEntry);
            Vector3 semanticDirection = GetFlatDirection(semantic.RawExit - semantic.RawEntry);
            if (walkDirection.sqrMagnitude < 0.5f || semanticDirection.sqrMagnitude < 0.5f ||
                Vector3.Dot(walkDirection, semanticDirection) < 0.88f)
            {
                return false;
            }

            int overlapStart = Math.Max(walk.StartBreadcrumbSequence, semantic.StartBreadcrumbSequence);
            int overlapEnd = Math.Min(walk.EndBreadcrumbSequence, semantic.EndBreadcrumbSequence);
            int sequenceGap = Math.Max(0, overlapStart - overlapEnd);
            if (sequenceGap > 3)
            {
                return false;
            }

            Vector3 walkCenter = (walk.RawEntry + walk.RawExit) * 0.5f;
            Vector3 semanticCenter = (semantic.RawEntry + semantic.RawExit) * 0.5f;
            float centerDistance = GetPlanarDistance(walkCenter, semanticCenter);
            float walkStartToSemantic = GetPlanarDistanceToSegment(walk.RawEntry, semantic.RawEntry, semantic.RawExit);
            float walkEndToSemantic = GetPlanarDistanceToSegment(walk.RawExit, semantic.RawEntry, semantic.RawExit);
            float walkSpan = GetPlanarDistance(walk.RawEntry, walk.RawExit);
            float semanticSpan = GetPlanarDistance(semantic.RawEntry, semantic.RawExit);
            bool contained = walkStartToSemantic <= 0.80f && walkEndToSemantic <= 0.80f &&
                             walkSpan <= semanticSpan + 1.25f;
            return centerDistance <= 1.35f && contained;
        }

        private static bool IsRouteMissionStepFresher(
            FollowerRouteMissionStep candidate,
            FollowerRouteMissionStep existing)
        {
            if (candidate == null) return false;
            if (existing == null) return true;
            if (candidate.EndBreadcrumbSequence != existing.EndBreadcrumbSequence)
            {
                return candidate.EndBreadcrumbSequence > existing.EndBreadcrumbSequence;
            }
            if (candidate.StartBreadcrumbSequence != existing.StartBreadcrumbSequence)
            {
                return candidate.StartBreadcrumbSequence > existing.StartBreadcrumbSequence;
            }
            if (Mathf.Abs(candidate.CreatedAt - existing.CreatedAt) > 0.001f)
            {
                return candidate.CreatedAt > existing.CreatedAt;
            }
            return candidate.EpisodeId > existing.EpisodeId;
        }
    }
}
