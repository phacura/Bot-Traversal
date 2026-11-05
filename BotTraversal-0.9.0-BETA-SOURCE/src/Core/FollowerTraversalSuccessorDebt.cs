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
        private static bool IsRouteMissionSameChainImmediateSuccessor(FollowerRouteMission mission, int stepIndex)
        {
            if (mission?.Steps == null || stepIndex <= 0 || stepIndex >= mission.Steps.Count)
            {
                return false;
            }

            FollowerRouteMissionStep current = mission.Steps[stepIndex];
            FollowerRouteMissionStep previous = mission.Steps[stepIndex - 1];
            return current != null && previous != null && current.ChainId > 0 &&
                   current.ChainId == previous.ChainId;
        }

        private static bool TryProveLearningV428TypedSuccessorPhysicalSubsumption(
            BotOwner bot,
            FollowerRouteMissionStep step,
            Vector3 rawEntry,
            Vector3 rawExit,
            bool directionalChronologyProven,
            out string proofSource)
        {
            proofSource = string.Empty;
            if ((bot == null) || step == null ||
                !directionalChronologyProven ||
                !IsFinite(bot.Position) || !IsFinite(rawEntry) || !IsFinite(rawExit))
            {
                return false;
            }

            LearningV354TraversalCapabilityProfile profile = GetLearningV354ActionProfile(step.Kind);
            if (profile == null || !profile.RequiresActuator || !profile.RequiresLandingProof)
            {
                return false;
            }

            // DropDown already owns a dedicated physical completion semantic used elsewhere by
            // RouteMission: below the landing plane plus local exit proximity or credible directed
            // projection. Reuse that semantic instead of pretending an actuator landing is a
            // WalkThrough finite corridor. Future landing-proof actuator kinds can add their own
            // capability-backed proof here without changing V382's ownership/lifetime contract.
            if (string.Equals(step.Kind, "DropDown", StringComparison.OrdinalIgnoreCase) &&
                IsFollowerAlreadyBelowDropDownLanding(bot.Position, rawEntry, rawExit, step.Exit))
            {
                proofSource = "dropDownLandingSemantic";
                return true;
            }

            return false;
        }

        private static bool TryConsumeLearningV382PhysicallySubsumedSuccessorDebt(
            BotOwner bot,
            FollowerRouteMission mission,
            FollowerRouteMissionStep step,
            out float projection,
            out float requiredProjection)
        {
            projection = 0f;
            requiredProjection = 0f;
            if ((bot == null) || mission?.Steps == null ||
                step == null || mission.CurrentStepIndex <= 0 ||
                mission.CurrentStepIndex >= mission.Steps.Count)
            {
                return false;
            }

            // A completion receipt for a previous edge cannot be borrowed by
            // a stale successor Step after the Mission cursor moved. This is
            // the same current-step identity used by physical writers; it does
            // not grant Native/Assisted success or relax landing proof.
            if (!TryValidateV2CurrentTypedStep(bot, mission, step, false,
                    out string successorIdentityRejectReason))
            {
                RecordV2TypedStepLeaseRejection(bot, mission, step,
                    "learningV382PhysicallySubsumedSuccessorDebt",
                    successorIdentityRejectReason, "v2rc72TypedSuccessorIdentityRejected");
                return false;
            }
            FollowerRouteMissionStep previous = mission.Steps[mission.CurrentStepIndex - 1];
            string profileId = bot.ProfileId ?? string.Empty;
            bool learningV391SameChainImmediateSuccessor =
                IsRouteMissionSameChainImmediateSuccessor(mission, mission.CurrentStepIndex);
            if (previous == null || string.IsNullOrEmpty(profileId) || previous.EdgeId <= 0)
            {
                return false;
            }

            string previousCompletionKey = MakeRecentTraversalCompletionKey(profileId, previous.EdgeId);
            if (!RecentTraversalCompletions.TryGetValue(
                    previousCompletionKey, out RecentTraversalCompletion previousCompletion) ||
                previousCompletion == null || !IsLifecycleCompletionForStep(bot,previousCompletion,previous) || Time.time > previousCompletion.ExpiresAt)
            {
                return false;
            }

            bool learningV391RedundantCanonicalWalkThroughFragment =
                (string.Equals(previous.Kind, "WalkThrough", StringComparison.OrdinalIgnoreCase)) &&
                string.Equals(step.Kind, "WalkThrough", StringComparison.OrdinalIgnoreCase) &&
                AreRouteMissionWalkThroughStepsRedundant(previous, step);
            if (learningV391RedundantCanonicalWalkThroughFragment)
            {
                bool learningV391Consumed = ConsumeFollowerRouteMissionRedundantWalkThroughIdentity(
                    bot, mission, step, previous.EpisodeId, previous.EdgeId);
                if (learningV391Consumed)
                {
                    WriteEvent("learningV391CanonicalPassageFragmentRetired", bot, new
                    {
                        missionId = mission.MissionId,
                        stepIndex = mission.CurrentStepIndex,
                        predecessorEpisodeId = previous.EpisodeId,
                        predecessorEdgeId = previous.EdgeId,
                        predecessorChainId = previous.ChainId,
                        successorEpisodeId = step.EpisodeId,
                        successorEdgeId = step.EdgeId,
                        successorChainId = step.ChainId,
                        sameAuthoredChain = previous.ChainId > 0 && previous.ChainId == step.ChainId,
                        semanticChainMerged = false,
                        traversalSuccessRecorded = false,
                        persistentLearningChanged = false,
                        action = "recentPhysicalWalkThroughCompletionRetiresOnlyRedundantCanonicalRuntimeFragmentAcrossRecorderChainBoundary"
                    });
                    return true;
                }
            }

            // Cross-chain widening above is intentionally limited to redundant WT identity.
            // Distinct actuator/action successors retain V382's original same-chain requirement.
            if (!learningV391SameChainImmediateSuccessor || previous.ChainId <= 0 ||
                previous.ChainId != step.ChainId)
            {
                return false;
            }

            // This path deliberately bypasses only the exact-edge completion requirement for the
            // IMMEDIATE successor.  The predecessor's recent completion is itself physical-only
            // evidence (teleport completions are not registered here), while the current body must
            // independently prove that it already lies on/beyond this successor's finite corridor.
            Vector3 rawEntry = IsFinite(step.RawEntry) ? step.RawEntry : step.Entry;
            Vector3 rawExit = IsFinite(step.RawExit) ? step.RawExit : step.Exit;
            if (!IsFinite(rawEntry) || !IsFinite(rawExit) || !IsFinite(bot.Position))
            {
                return false;
            }

            Vector3 axis = rawExit - rawEntry;
            axis.y = 0f;
            float length = axis.magnitude;
            float entryDistance = GetPlanarDistance(bot.Position, rawEntry);
            float exitDistance = GetPlanarDistance(bot.Position, rawExit);
            bool physicallyBeyond = false;
            bool directionalChronologyProven = false;
            float localCorridorDistance = GetPlanarDistanceToSegment(bot.Position, rawEntry, rawExit);
            if (length < 0.25f)
            {
                directionalChronologyProven =
                    exitDistance <= 1.20f && exitDistance + 0.25f < entryDistance;
                physicallyBeyond = directionalChronologyProven;
            }
            else
            {
                Vector3 direction = axis / length;
                Vector3 fromEntry = bot.Position - rawEntry;
                fromEntry.y = 0f;
                projection = Vector3.Dot(fromEntry, direction);
                requiredProjection = Mathf.Max(0.12f, length - 0.25f);
                bool verticalCredible = Mathf.Abs(bot.Position.y - rawExit.y) <= 1.35f;
                bool nearExit = exitDistance <= 1.35f && exitDistance + 0.25f < entryDistance;
                directionalChronologyProven = verticalCredible &&
                    (projection >= requiredProjection || nearExit);
                physicallyBeyond = directionalChronologyProven &&
                    localCorridorDistance <= RouteMissionDirectionalWalkThroughMaxLateral;
            }

            bool learningV428TypedProof = false;
            string learningV428TypedProofSource = string.Empty;
            if (!physicallyBeyond)
            {
                // LearningV4.31: V428 was meant to replace only the unrelated WalkThrough
                // lateral-envelope requirement for typed landing actuators. It must never replace
                // forward chronology itself. The V430 fence regression proved a DropDown with
                // projection=-0.42/required=+0.75 and exitDistance=1.45 could be deleted solely
                // because the body was below the landing Y plane. Typed semantics may therefore
                // widen locality only after the same directed far-side proof already succeeded.
                learningV428TypedProof = TryProveLearningV428TypedSuccessorPhysicalSubsumption(
                    bot, step, rawEntry, rawExit, directionalChronologyProven,
                    out learningV428TypedProofSource);
                physicallyBeyond = learningV428TypedProof;

                if (!directionalChronologyProven &&
                    string.Equals(step.Kind, "DropDown", StringComparison.OrdinalIgnoreCase) &&
                    IsFollowerAlreadyBelowDropDownLanding(bot.Position, rawEntry, rawExit, step.Exit) &&
                    ShouldEmitDiagnostic(
                        "learningV431TypedSuccessorChronologyRejected:" +
                            step.EdgeId.ToString(CultureInfo.InvariantCulture), bot, 0.35f))
                {
                    WriteEvent("learningV431TypedSuccessorChronologyRejected", bot, new
                    {
                        missionId = mission.MissionId,
                        stepIndex = mission.CurrentStepIndex,
                        edgeId = step.EdgeId,
                        episodeId = step.EpisodeId,
                        kind = step.Kind,
                        body = Vec(bot.Position),
                        rawEntry = Vec(rawEntry),
                        rawExit = Vec(rawExit),
                        entryDistance,
                        exitDistance,
                        projection,
                        requiredProjection,
                        localCorridorDistance,
                        landingSemantic = true,
                        directionalChronologyProven = false,
                        traversalSuccessRecorded = false,
                        persistentLearningChanged = false,
                        action = "typedLandingSemanticCannotRetireSuccessorUntilBodyIsDirectionallyBeyondItsPhysicalFrontier"
                    });
                }
            }

            // V2-RC40: "nearExit" is only a locality shortcut.  A predecessor's
            // landing beside an independent landing-required successor is not proof
            // of directed passage through that successor: RC39 mission 163 consumed
            // Edge 72 at projection 1.571 / required 2.088 in the very frame that
            // Edge 27 landed.  Keep the second actuator live until its own producer
            // or the existing two-ended V4.68 physical landing witness can retire it.
            // This capability rule is shared across landing-required actuator kinds;
            // it creates no new per-scene state, timing budget or Native success.
            LearningV354TraversalCapabilityProfile successorProfile =
                GetLearningV354ActionProfile(step.Kind);
            if (successorProfile != null && successorProfile.RequiresActuator &&
                successorProfile.RequiresLandingProof && length >= 0.25f &&
                projection + LearningV32EntryProgressEpsilon < requiredProjection)
            {
                if (ShouldEmitDiagnostic(
                        "v2rc40LandingSuccessorForwardDebtRetained:" +
                        step.EpisodeId.ToString(CultureInfo.InvariantCulture), bot, 0.30f))
                {
                    WriteEvent("v2rc40LandingSuccessorForwardDebtRetained", bot, new
                    {
                        missionId = mission.MissionId,
                        stepIndex = mission.CurrentStepIndex,
                        episodeId = step.EpisodeId,
                        edgeId = step.EdgeId,
                        predecessorEdgeId = previous.EdgeId,
                        projection,
                        requiredProjection,
                        entryDistance,
                        exitDistance,
                        localCorridorDistance,
                        nativeSuccessRecorded = false,
                        traversalSuccessRecorded = false,
                        routeStepAdvanced = false,
                        action = "nearExitAndPredecessorLandingCannotSubstituteForSuccessorForwardCrossing"
                    });
                }
                return false;
            }

            if (!physicallyBeyond)
            {
                return false;
            }

            if (physicallyBeyond &&
                string.Equals(step.Kind, "DropDown", StringComparison.OrdinalIgnoreCase) &&
                (bot.Position.y > rawExit.y + DropDownAlreadyBelowLandingMargin ||
                 !IsFollowerAlreadyBelowDropDownLanding(bot.Position, rawEntry, rawExit, step.Exit) ||
                 bot.GetPlayer?.MovementContext?.IsGrounded != true))
            {
                // CF20: CF19 Edge66 was retired by horizontal projection at
                // y=+0.569 while its raw landing was y=-0.279. A real DropDown
                // landing was NOT yet proven, so retain the same-chain actuator
                // debt until the existing landing/grounded semantic is true.
                if (ShouldEmitDiagnostic(
                    "cf20DropDownUnlandedDebtRetained:" +
                        step.EdgeId.ToString(CultureInfo.InvariantCulture), bot, 0.2f))
                {
                    WriteEvent("cf20DropDownUnlandedDebtRetained", bot, new
                    {
                        missionId = mission.MissionId,
                        edgeId = step.EdgeId,
                        episodeId = step.EpisodeId,
                        body = Vec(bot.Position),
                        rawExit = Vec(rawExit),
                        projection,
                        requiredProjection,
                        grounded = bot.GetPlayer?.MovementContext?.IsGrounded == true,
                        landingSemantic = false,
                        traversalSuccessRecorded = false,
                        nativeSuccessRecorded = false,
                        persistentLearningChanged = false,
                        bodyRelocated = false,
                        action = "retainDropDownActuatorDebtUntilActualPhysicalLandingBeforeTerminalConsumer"
                    });
                }
                return false;
            }

            if (learningV428TypedProof)
            {
                WriteEvent("learningV428TypedSuccessorPhysicalSubsumptionProven", bot, new
                {
                    missionId = mission.MissionId,
                    stepIndex = mission.CurrentStepIndex,
                    predecessorEpisodeId = previous.EpisodeId,
                    predecessorEdgeId = previous.EdgeId,
                    successorEpisodeId = step.EpisodeId,
                    successorEdgeId = step.EdgeId,
                    successorKind = step.Kind,
                    proofSource = learningV428TypedProofSource,
                    body = Vec(bot.Position),
                    rawEntry = Vec(rawEntry),
                    rawExit = Vec(rawExit),
                    entryDistance,
                    exitDistance,
                    projection,
                    requiredProjection,
                    localCorridorDistance,
                    walkThroughLocalityLimit = RouteMissionDirectionalWalkThroughMaxLateral,
                    traversalSuccessRecorded = false,
                    persistentLearningChanged = false,
                    bodyRelocated = false,
                    action = "typedPhysicalSemanticRetiresAlreadySubsumedSuccessorDebtWithoutWideningWalkThroughLocality"
                });
            }

            // A same-chain, directionally-proven landing cannot retire a
            // successor if the canonical Mission advanced during proof gathering.
            // The same boundary guard is used by pre-native physical handoff.
            if (!TryValidateV2AdjacentPhysicalContinuationBoundary(
                    bot, mission, previous, step, false, out string boundaryRejectReason))
            {
                RecordV2TypedStepLeaseRejection(bot, mission, step,
                    "learningV382PhysicalSubsumptionBeforeConsume",
                    boundaryRejectReason, "v2rc73PhysicalContinuationBoundaryRejected");
                return false;
            }
            RecordV2PhysicalContinuationBoundary(bot, mission, previous, step,
                "physicallySubsumedSuccessorDebt", "directedCrossingAndLanding");
            bool consumed = ConsumeFollowerRouteMissionPhysicalPass(
                bot,
                mission,
                step,
                "learningV382DownstreamActionDebtPhysicallySubsumed",
                projection,
                requiredProjection);
            WriteEvent("learningV382DownstreamActionDebtPhysicallySubsumed", bot, new
            {
                missionId = mission.MissionId,
                stepIndex = mission.CurrentStepIndex,
                predecessorEpisodeId = previous.EpisodeId,
                predecessorEdgeId = previous.EdgeId,
                successorEpisodeId = step.EpisodeId,
                successorEdgeId = step.EdgeId,
                successorKind = step.Kind,
                chainId = step.ChainId,
                body = Vec(bot.Position),
                rawEntry = Vec(rawEntry),
                rawExit = Vec(rawExit),
                projection,
                requiredProjection,
                localCorridorDistance,
                predecessorCompletionAge = Mathf.Max(0f, Time.time - previousCompletion.CompletedAt),
                routeDebtConsumed = consumed,
                traversalSuccessRecorded = false,
                persistentLearningChanged = false,
                action = "physicalPredecessorAlreadyCarriedBodyAcrossImmediateSuccessorRetireExecutionDebtWithoutSuccessCredit"
            });
            return consumed;
        }
    }
}
