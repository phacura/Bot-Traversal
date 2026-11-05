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
        private static bool TryGetLearningV424ActuatorExecutorAdmission(
            BotOwner bot,
            FollowerRouteMissionStep step,
            out bool executorAdmissible,
            out Vector3 admissionTarget,
            out float entryDistance,
            out float forwardEntryProjection,
            out float lateralEntryOffset,
            out float maxLateral,
            out float alignmentAngle,
            out float maxAlignmentAngle,
            out float maxForwardOvershoot,
            out float triggerDistance,
            out string detail)
        {
            executorAdmissible = false;
            admissionTarget = default;
            entryDistance = float.MaxValue;
            forwardEntryProjection = 0f;
            lateralEntryOffset = float.MaxValue;
            maxLateral = 0f;
            alignmentAngle = 180f;
            maxAlignmentAngle = 0f;
            maxForwardOvershoot = 0f;
            triggerDistance = 0f;
            detail = "unsupported";
            if ((bot?.GetPlayer?.MovementContext == null) ||
                step == null || !IsFinite(bot.Position))
            {
                return false;
            }

            LearningV354TraversalCapabilityProfile profile = GetLearningV354ActionProfile(step.Kind);
            if (profile == null || !profile.RequiresActuator ||
                string.Equals(step.Kind, "Jump", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            bool vault = string.Equals(step.Kind, "Vault", StringComparison.OrdinalIgnoreCase);
            bool climb = string.Equals(step.Kind, "Climb", StringComparison.OrdinalIgnoreCase);
            bool dropDown = string.Equals(step.Kind, "DropDown", StringComparison.OrdinalIgnoreCase);
            if (!vault && !climb && !dropDown)
            {
                return false;
            }

            Vector3 rawEntry = IsFinite(step.RawEntry) ? step.RawEntry : step.Entry;
            Vector3 rawExit = IsFinite(step.RawExit) ? step.RawExit : step.Exit;
            if (!IsFinite(rawEntry) || !IsFinite(rawExit))
            {
                detail = "geometryUnavailableUseExistingAdmissionPath";
                return false;
            }
            // Preserve the active executor's own approach direction before ownership transfer:
            // Vault/Climb converge on RawEntry, while DropDown acquisition intentionally drives
            // through toward RawExit. This prevents the pre-admission consumer from introducing a
            // backward target after the body has already reached the DropDown entry plane.
            admissionTarget = dropDown ? rawExit : rawEntry;
            Vector3 direction = GetFlatDirection(rawExit - rawEntry);
            if (direction.sqrMagnitude < 0.5f)
            {
                detail = "axisUnavailableUseExistingAdmissionPath";
                return false;
            }

            Player player = bot.GetPlayer;
            entryDistance = GetPlanarDistance(bot.Position, rawEntry);
            Vector3 bodyOffset = bot.Position - rawEntry;
            bodyOffset.y = 0f;
            forwardEntryProjection = Vector3.Dot(bodyOffset, direction);
            lateralEntryOffset = (bodyOffset - direction * forwardEntryProjection).magnitude;
            Vector3 actualForward = GetFlatDirection(
                player.MovementContext.PlayerRealForward);
            alignmentAngle = actualForward.sqrMagnitude > 0.5f
                ? Vector3.Angle(actualForward, direction)
                : 180f;

            bool grounded = player.MovementContext.IsGrounded;
            EPlayerState currentState = player.CurrentStateName;
            bool transitionClear = currentState != EPlayerState.Transition;
            bool runState = FollowerCapabilityHelperRules.TryGetLearningV424ActuatorExecutorAdmission_runState_69E4928A_1((int)(currentState),(int)(EPlayerState.Run),(int)(currentState),(int)(EPlayerState.Sprint));
            // The active entry executor treats measured forward body motion as evidence. Before the
            // probe exists there is no probe-relative start projection yet, so preserve the same
            // semantic with EFT's actual local-forward input plus the executor's existing near-entry
            // projection allowance. This is not a new threshold.
            bool forwardMotionEvidence = player.IsForwardInputDirection ||
                forwardEntryProjection >= -0.30f;

            LearnedTraversalEdge edge = LearnedTraversalEdges.FirstOrDefault(candidate =>
                candidate != null && candidate.Id == step.EdgeId);
            if (dropDown)
            {
                triggerDistance = NativeDropDownEntryTriggerDistance;
                maxAlignmentAngle = NativeDropDownEntryMaxAlignmentAngle;
                maxLateral = NativeDropDownEntryMaxLateralOffset;
                maxForwardOvershoot = NativeDropDownEntryMaxForwardOvershoot;
            }
            else
            {
                triggerDistance = InjectedNativeVaultEntryTriggerDistance;
                maxAlignmentAngle = (vault)
                    ? InjectedNativeVaultRollingMaxAlignmentAngle
                    : InjectedNativeVaultEntryMaxAlignmentAngle;
                float baseLateral = (vault)
                    ? InjectedNativeVaultRollingMaxLateralOffset
                    : InjectedNativeVaultEntryMaxLateralOffset;
                maxLateral = GetLearningV357ActuatorEntryLateralTolerance(edge, direction, baseLateral);
                maxForwardOvershoot = InjectedNativeVaultEntryMaxForwardOvershoot;
            }

            // CF13: native Climb requires mover, EFT sprint intent, and physical state to
            // agree BEFORE handing off the route producer. CF12's pre-probe admission
            // omitted IsSprintEnabled, although RollingApproach required it. That mismatch
            // could accept a seemingly settled body while EFT still owned a sprint intent.
            bool learnedTriggerFrontierReady = true;
            if ((vault || climb) &&
                TryGetLearningV374LearnedActuatorTriggerFrontier(
                    edge, out float minimumForwardProjection, out _))
            {
                learnedTriggerFrontierReady = forwardEntryProjection >= minimumForwardProjection;
            }

            if (climb && grounded && transitionClear &&
                entryDistance <= triggerDistance &&
                lateralEntryOffset <= maxLateral &&
                forwardEntryProjection <= maxForwardOvershoot &&
                alignmentAngle <= maxAlignmentAngle &&
                learnedTriggerFrontierReady &&
                (forwardMotionEvidence || entryDistance <= 0.30f))
            {
                // The existing route movement producer still owns translation. Only it
                // may request this single mode change; never stop, teleport, or replace
                // its target. Subsequent frames observe the EFT state becoming Run/Idle.
                TryRequestCF13ClimbSprintRelease(bot, step.EdgeId,
                    "routeProducerExactNativeClimbEntry");
            }
            bool climbSprintReleased = !climb ||
                (bot.Mover?.Sprinting != true &&
                 !player.MovementContext.IsSprintEnabled &&
                 currentState != EPlayerState.Sprint &&
                 currentState != EPlayerState.Transition);

            if (dropDown)
            {
                // Mirror UpdateSharedNativeEntryAcquisition's DropDown admission shape. The
                // route-side pre-probe producer may stage farther away, but ownership does not
                // change until the body is grounded, running forward and inside the same finite
                // near-side/lateral/alignment envelope used by the executor.
                executorAdmissible =
                    grounded && runState && transitionClear &&
                    forwardEntryProjection >= -LearningV32DropDownAcquireNearSideDistance &&
                    forwardEntryProjection <= maxForwardOvershoot &&
                    lateralEntryOffset <= maxLateral &&
                    alignmentAngle <= maxAlignmentAngle &&
                    forwardMotionEvidence;
            }
            else if (vault)
            {
                executorAdmissible =
                    grounded && runState && transitionClear &&
                    entryDistance <= triggerDistance &&
                    lateralEntryOffset <= maxLateral &&
                    forwardEntryProjection <= maxForwardOvershoot &&
                    alignmentAngle <= maxAlignmentAngle &&
                    learnedTriggerFrontierReady &&
                    forwardMotionEvidence;
            }
            else // Climb
            {
                executorAdmissible =
                    grounded && transitionClear && climbSprintReleased &&
                    entryDistance <= triggerDistance &&
                    lateralEntryOffset <= maxLateral &&
                    forwardEntryProjection <= maxForwardOvershoot &&
                    alignmentAngle <= maxAlignmentAngle &&
                    learnedTriggerFrontierReady &&
                    (forwardMotionEvidence || entryDistance <= 0.30f);
            }
            detail = executorAdmissible
                ? "existingExecutorAcquisitionEnvelope"
                : "approachProducerRetainsAuthorityUntilExistingExecutorEnvelope";
            return true;
        }

        private static bool TryGetLearningV425OrientationOnlyActuatorAdmission(
            BotOwner bot,
            FollowerRouteMissionStep step,
            float entryDistance,
            float forwardEntryProjection,
            float lateralEntryOffset,
            float maxLateral,
            float alignmentAngle,
            float maxAlignmentAngle,
            float maxForwardOvershoot,
            float triggerDistance,
            out Vector3 desiredDirection,
            out string detail)
        {
            desiredDirection = Vector3.zero;
            detail = "notEligible";
            if ((bot?.GetPlayer?.MovementContext == null) ||
                step == null || !IsFinite(bot.Position) ||
                alignmentAngle <= maxAlignmentAngle)
            {
                return false;
            }

            bool vault = string.Equals(step.Kind, "Vault", StringComparison.OrdinalIgnoreCase);
            bool climb = string.Equals(step.Kind, "Climb", StringComparison.OrdinalIgnoreCase);
            bool dropDown = string.Equals(step.Kind, "DropDown", StringComparison.OrdinalIgnoreCase);
            if (!vault && !climb && !dropDown)
            {
                return false;
            }

            Vector3 rawEntry = IsFinite(step.RawEntry) ? step.RawEntry : step.Entry;
            Vector3 rawExit = IsFinite(step.RawExit) ? step.RawExit : step.Exit;
            if (!IsFinite(rawEntry) || !IsFinite(rawExit))
            {
                return false;
            }
            desiredDirection = GetFlatDirection(rawExit - rawEntry);
            if (desiredDirection.sqrMagnitude < 0.5f)
            {
                return false;
            }

            Player player = bot.GetPlayer;
            EPlayerState currentState = player.CurrentStateName;
            bool grounded = player.MovementContext.IsGrounded;
            bool transitionClear = currentState != EPlayerState.Transition;
            bool runState = FollowerCapabilityHelperRules.TryGetLearningV425OrientationOnlyActuatorAdmission_runState_7B86D56D_1((int)(currentState),(int)(EPlayerState.Run),(int)(currentState),(int)(EPlayerState.Sprint));
            bool forwardMotionEvidence = player.IsForwardInputDirection ||
                forwardEntryProjection >= -0.30f;

            bool climbSprintReleased = !climb ||
                (bot.Mover?.Sprinting != true &&
                 !player.MovementContext.IsSprintEnabled &&
                 currentState != EPlayerState.Sprint &&
                 currentState != EPlayerState.Transition);

            bool learnedTriggerFrontierReady = true;
            if (vault || climb)
            {
                LearnedTraversalEdge edge = LearnedTraversalEdges.FirstOrDefault(candidate =>
                    candidate != null && candidate.Id == step.EdgeId);
                if (TryGetLearningV374LearnedActuatorTriggerFrontier(
                        edge, out float minimumForwardProjection, out _))
                {
                    learnedTriggerFrontierReady =
                        forwardEntryProjection >= minimumForwardProjection;
                }
            }

            bool allExceptHeading;
            if (dropDown)
            {
                allExceptHeading =
                    grounded && runState && transitionClear &&
                    forwardEntryProjection >= -LearningV32DropDownAcquireNearSideDistance &&
                    forwardEntryProjection <= maxForwardOvershoot &&
                    lateralEntryOffset <= maxLateral &&
                    forwardMotionEvidence;
            }
            else if (vault)
            {
                allExceptHeading =
                    grounded && runState && transitionClear &&
                    entryDistance <= triggerDistance &&
                    lateralEntryOffset <= maxLateral &&
                    forwardEntryProjection <= maxForwardOvershoot &&
                    learnedTriggerFrontierReady &&
                    forwardMotionEvidence;
            }
            else
            {
                allExceptHeading =
                    grounded && transitionClear && climbSprintReleased &&
                    entryDistance <= triggerDistance &&
                    lateralEntryOffset <= maxLateral &&
                    forwardEntryProjection <= maxForwardOvershoot &&
                    learnedTriggerFrontierReady &&
                    (forwardMotionEvidence || entryDistance <= 0.30f);
            }

            if (!allExceptHeading)
            {
                detail = "translationOrStateRequirementStillOpen";
                return false;
            }

            // V4.53: all physical admission requirements except heading already match the
            // existing executor. Admission is passive: it may steer toward the typed actuator axis,
            // but it must not stop the mover, zero speed, rewrite local input or replace the live
            // translation producer. V4.18 already proves that rolling actuators can transfer the
            // position frontier to the probe and let that real physical consumer own final heading.
            try
            {
                bot.Steering?.BotSteeringLookToDirection("R1-662BF30376B6",@bot,desiredDirection);
                detail = ("onlyHeadingBlocksExistingExecutorEnvelopePassiveSteering");
                return true;
            }
            catch (Exception ex)
            {
                detail = "orientationSteeringFailed:" + ex.GetType().Name;
                return false;
            }
        }

        private static bool TryBuildLearningV463DeferredDropDownProducerTarget(
            BotOwner bot, ProbeRuntime probe, FollowerRouteMission mission,
            FollowerRouteMissionStep step, Vector3 rawEntry, Vector3 rawExit,
            out Vector3 target, out float forwardEntryProjection,
            out float measuredForwardProgress)
        {
            target = default;
            forwardEntryProjection = 0f;
            measuredForwardProgress = 0f;
            if ((bot == null) || probe?.Player == null ||
                probe.FrozenTraversalCommitment == null ||
                probe.LearningV32NativeEntryAcquired ||
                mission?.Steps == null || step == null ||
                !string.Equals(step.Kind, "DropDown", StringComparison.OrdinalIgnoreCase) ||
                !IsRouteMissionSameChainImmediateSuccessor(mission, mission.CurrentStepIndex) ||
                mission.CurrentStepIndex < 0 ||
                mission.CurrentStepIndex >= mission.Steps.Count ||
                mission.Steps[mission.CurrentStepIndex] != step ||
                step.EdgeId <= 0 || probe.LearnedEdgeId != step.EdgeId ||
                probe.FrozenTraversalCommitment.RouteMissionId != mission.MissionId ||
                probe.FrozenTraversalCommitment.RouteMissionStepIndex != mission.CurrentStepIndex ||
                !IsFinite(rawEntry) || !IsFinite(rawExit) || !IsFinite(bot.Position))
            {
                return false;
            }
            LearningV354TraversalCapabilityProfile profile = GetLearningV354ActionProfile(step.Kind);
            if (profile == null || !profile.CanAcquireFromOffNavMesh ||
                !profile.SupportsRollingEntry ||
                !IsEffectivelySupportedForProbe(bot, probe.Player))
            {
                return false;
            }
            Vector3 axis = GetFlatDirection(rawExit - rawEntry);
            if (axis.sqrMagnitude < 0.5f)
            {
                return false;
            }
            Vector3 bodyOffset = bot.Position - rawEntry;
            bodyOffset.y = 0f;
            forwardEntryProjection = Vector3.Dot(bodyOffset, axis);
            float lateral = (bodyOffset - axis * forwardEntryProjection).magnitude;
            measuredForwardProgress = forwardEntryProjection -
                probe.LearningV32NativeEntryStartForwardProjection;
            // V3.2's 0.90m envelope controls ACTUATOR admission, not how early the
            // already-proven same-chain physical producer can bridge an off-NavMesh gap.
            // Reuse the existing adjacent-route locality bound for producer-only continuity.
            if (forwardEntryProjection < -RouteMissionTraversalAdjacentBridgeMaxDistance ||
                forwardEntryProjection >= 0f ||
                lateral > NativeDropDownEntryMaxLateralOffset ||
                rawEntry.y - bot.Position.y < LearningV33DropDownCrossingMinVerticalDrop ||
                IsFollowerAlreadyBelowDropDownLanding(
                    bot.Position, rawEntry, rawExit, step.Exit))
            {
                return false;
            }
            // The target is strictly forward and bounded by the EXISTING near-side envelope;
            // set current-body height so the historical elevated RawEntry cannot brake the body.
            float forwardHorizon = Mathf.Min(
                ProbeJumpRunupDistance,
                Mathf.Max(NativeDropDownEntryTriggerDistance,
                    -forwardEntryProjection + NativeDropDownEntryTriggerDistance));
            target = bot.Position + axis * forwardHorizon;
            target.y = bot.Position.y;
            return IsFinite(target);
        }

        private static bool TryGetLearningV453SameChainSuccessorPhysicalProducer(
            BotOwner bot,
            FollowerRouteMission mission,
            FollowerRouteMissionStep step,
            out Vector3 target,
            out bool actuatorReady,
            out bool producerTargetAvailable,
            out string source)
        {
            target = default;
            actuatorReady = false;
            producerTargetAvailable = false;
            source = string.Empty;
            if ((bot == null) || mission == null ||
                step == null || string.Equals(step.Kind, "Jump", StringComparison.OrdinalIgnoreCase) ||
                !IsRouteMissionSameChainImmediateSuccessor(mission, mission.CurrentStepIndex))
            {
                return false;
            }

            LearningV354TraversalCapabilityProfile successorProfile = GetLearningV354ActionProfile(step.Kind);
            if (successorProfile == null || !successorProfile.RequiresActuator)
            {
                return false;
            }

            if (!TryGetLearningV424ActuatorExecutorAdmission(
                    bot, step,
                    out bool executorAdmissible,
                    out Vector3 admissionTarget,
                    out float entryDistance,
                    out float forwardEntryProjection,
                    out float lateralEntryOffset,
                    out float maxLateral,
                    out float alignmentAngle,
                    out float maxAlignmentAngle,
                    out float maxForwardOvershoot,
                    out float triggerDistance,
                    out string admissionDetail))
            {
                return false;
            }

            actuatorReady = executorAdmissible;
            Vector3 rawEntry = IsFinite(step.RawEntry) ? step.RawEntry : step.Entry;
            Vector3 rawExit = IsFinite(step.RawExit) ? step.RawExit : step.Exit;
            Vector3 direction = GetFlatDirection(rawExit - rawEntry);

            // CF71: V453's forward current-body target is only a valid actuator *approach*
            // if the body remains inside V424's already-authoritative lateral portal lane.
            // The CF70 field raid proved an off-axis Climb body (~1.07m lateral vs 0.75m
            // admitted) being pushed toward RawExit for >6s after crossing the forward
            // entry plane. That target cannot reduce the lateral error, and it keeps the
            // live traversal writer in a collision dead-zone until Terminal Recovery.
            // Do not increase admission tolerances or infer crossing from body chronology.
            if (string.Equals(step.Kind, "Climb", StringComparison.OrdinalIgnoreCase) &&
                !executorAdmissible && forwardEntryProjection > 0f &&
                lateralEntryOffset > maxLateral)
            {
                Vector3 cf71LaneTarget = default;
                string cf71LaneSource = string.Empty;
                bool cf71StrictCurrentRaidLane = false;
                float cf71StrictPathLength = 0f;
                if (TryGetLearningV363ActuatorRouteEntryLaneTarget(
                        bot, mission, step,
                        out Vector3 cf71Candidate, out bool cf71LaneReady,
                        out _, out _, out _, out _, out _, out _,
                        out string cf71CandidateSource) && !cf71LaneReady &&
                    !string.IsNullOrEmpty(cf71CandidateSource) &&
                    cf71CandidateSource.StartsWith("currentRaidBreadcrumb", StringComparison.Ordinal) &&
                    cf71CandidateSource.IndexOf(":navMeshLane", StringComparison.Ordinal) >= 0 &&
                    IsFinite(cf71Candidate) &&
                    GetPlanarDistance(bot.Position, cf71Candidate) > 0.05f &&
                    GetPlanarDistance(bot.Position, cf71Candidate) <=
                        RouteMissionTraversalAdjacentBridgeMaxDistance)
                {
                    StrictPathResult cf71Strict = CalculateStrictPath(bot.Position, cf71Candidate);
                    if (cf71Strict != null && cf71Strict.StrictComplete &&
                        cf71Strict.PathLength >= 0.05f &&
                        cf71Strict.PathLength <= RouteMissionTraversalAdjacentBridgeMaxDistance)
                    {
                        cf71LaneTarget = cf71Candidate;
                        cf71LaneSource = cf71CandidateSource;
                        cf71StrictPathLength = cf71Strict.PathLength;
                        cf71StrictCurrentRaidLane = true;
                    }
                }
                target = cf71StrictCurrentRaidLane ? cf71LaneTarget : bot.Position;
                producerTargetAvailable = cf71StrictCurrentRaidLane;
                actuatorReady = false;
                source = cf71StrictCurrentRaidLane
                    ? "cf71OffAxisClimbStrictCurrentRaidIngress"
                    : "cf71OffAxisClimbProducerSuspendedNoStrictIngress";
                if (ShouldEmitDiagnostic(
                        "cf71OffAxisClimbPhysicalProducer:" +
                            mission.MissionId.ToString(CultureInfo.InvariantCulture) + ":" +
                            step.EdgeId.ToString(CultureInfo.InvariantCulture), bot, 0.35f))
                {
                    WriteEvent("cf71OffAxisClimbPhysicalProducerReconciled", bot, new
                    {
                        missionId = mission.MissionId,
                        stepIndex = mission.CurrentStepIndex,
                        edgeId = step.EdgeId,
                        episodeId = step.EpisodeId,
                        position = Vec(bot.Position),
                        rawEntry = Vec(rawEntry),
                        forwardEntryProjection,
                        lateralEntryOffset,
                        maxLateral,
                        priorExecutorAdmissible = executorAdmissible,
                        currentRaidStrictLane = cf71StrictCurrentRaidLane,
                        laneSource = cf71LaneSource,
                        strictPathLength = cf71StrictPathLength,
                        target = cf71StrictCurrentRaidLane ? (object)Vec(target) : null,
                        traversalSuccessRecorded = false,
                        persistentLearningChanged = false,
                        bodyRelocated = false,
                        action = cf71StrictCurrentRaidLane
                            ? "onlyCurrentRaidStrictReachableIngressMayCorrectOffAxisClimb"
                            : "suspendNonProgressingForwardProducerInsteadOfDrivingThroughObstacle"
                    });
                }
                return true;
            }

            // CF77 / CF73 authority convergence: a Native DropDown predecessor can land the
            // SAME physical body beyond the next DropDown entry before the successor can
            // acquire its *separate* Native actuator. The latter remains an uncompleted typed
            // debt. Do not revive an entry behind/below the body, extend a pathless exit target
            // into a wall, or reclassify the predecessor landing as successor success. A strict
            // SAME-BODY NavMesh path may supply a local forward rejoin corner to the already
            // authorized V453/V375 producer; it never creates a second writer or completion.
            // This path is generic: no map coordinates, edge IDs, or synthetic actuator proof.
            if (string.Equals(step.Kind, "DropDown", StringComparison.OrdinalIgnoreCase) &&
                !executorAdmissible &&
                forwardEntryProjection > NativeDropDownEntryTriggerDistance &&
                rawEntry.y - bot.Position.y >= LearningV33DropDownCrossingMinVerticalDrop &&
                rawEntry.y - rawExit.y >= LearningV33DropDownCrossingMinVerticalDrop &&
                !HasActiveTraversalProbe(bot) && !IsTeleportTransactionActive(bot) &&
                IsEffectivelySupportedForProbe(bot, bot.GetPlayer) &&
                mission.Steps != null && mission.CurrentStepIndex > 0 &&
                mission.CurrentStepIndex < mission.Steps.Count &&
                IsRouteMissionSameChainImmediateSuccessor(mission, mission.CurrentStepIndex) &&
                GetPlanarDistance(bot.Position, rawExit) <= TraversalChainImmediateMaxSpatialGap)
            {
                FollowerRouteMissionStep cf77Predecessor = mission.Steps[mission.CurrentStepIndex - 1];
                string cf77Profile = bot.ProfileId ?? string.Empty;
                RecentTraversalCompletion cf77PhysicalCompletion = null;
                long cf77OwnerGeneration =
                    FollowerMovementWriterAuthority.PeekOwnerGenerationForCF75(
                        bot, out FollowerMovementWriterAuthority.WriterDomain cf77Owner);
                bool cf77ExactProducerOwner =
                    cf77Owner == FollowerMovementWriterAuthority.WriterDomain.Traversal &&
                    cf77OwnerGeneration > 0 && !string.IsNullOrEmpty(cf77Profile) &&
                    FollowerTraversalRouteCursors.TryGetValue(
                        cf77Profile, out FollowerTraversalRouteCursor cf77Cursor) &&
                    cf77Cursor != null && cf77Cursor.LearningV375TransactionActive &&
                    cf77Cursor.LearningV375RouteMissionId == mission.MissionId &&
                    cf77OwnerGeneration == cf77Cursor.LearningV375MovementGeneration;
                bool cf77ExactPredecessorCompleted =
                    cf77ExactProducerOwner &&
                    cf77Predecessor != null && cf77Predecessor.EdgeId > 0 &&
                    cf77Predecessor.CurrentRaidRouteAuthority && step.CurrentRaidRouteAuthority &&
                    cf77Predecessor.ChainId > 0 && cf77Predecessor.ChainId == step.ChainId &&
                    cf77Predecessor.EpisodeId > 0 &&
                    cf77Predecessor.EpisodeId < step.EpisodeId &&
                    !string.IsNullOrEmpty(cf77Profile) &&
                    RecentTraversalCompletions.TryGetValue(
                        MakeRecentTraversalCompletionKey(cf77Profile, cf77Predecessor.EdgeId),
                        out cf77PhysicalCompletion) &&
                    cf77PhysicalCompletion != null &&
                    Time.time <= cf77PhysicalCompletion.ExpiresAt &&
                    Time.time - cf77PhysicalCompletion.CompletedAt <= 2f;
                if (cf77ExactPredecessorCompleted)
                {
                    bool cf77StrictGroundRoute = false;
                    Vector3 cf77RejoinCorner = default;
                    float cf77PathLength = 0f;
                    string cf77Rejection = "navMeshUnavailable";
                    if (NavMesh.SamplePosition(bot.Position, out NavMeshHit cf77BodyHit,
                            0.45f, NavMesh.AllAreas) &&
                        GetPlanarDistance(bot.Position, cf77BodyHit.position) <= 0.35f &&
                        Mathf.Abs(bot.Position.y - cf77BodyHit.position.y) <= 0.35f &&
                        NavMesh.SamplePosition(rawExit, out NavMeshHit cf77ExitHit,
                            0.45f, NavMesh.AllAreas) &&
                        GetPlanarDistance(rawExit, cf77ExitHit.position) <= 0.35f &&
                        Mathf.Abs(rawExit.y - cf77ExitHit.position.y) <= 0.35f)
                    {
                        StrictPathResult cf77Path = CalculateStrictPath(bot.Position, cf77ExitHit.position);
                        if (cf77Path != null && cf77Path.StrictComplete &&
                            cf77Path.Corners != null && cf77Path.Corners.Length > 1 &&
                            cf77Path.PathLength >= 0.10f &&
                            cf77Path.PathLength <= Mathf.Min(
                                RouteMissionTraversalAdjacentBridgeMaxDistance,
                                GetPlanarDistance(bot.Position, cf77ExitHit.position) * 1.6f + 0.20f))
                        {
                            cf77PathLength = cf77Path.PathLength;
                            foreach (Vector3 cf77Corner in cf77Path.Corners)
                            {
                                float cf77CornerDistance = GetPlanarDistance(bot.Position, cf77Corner);
                                if (cf77CornerDistance >= 0.15f &&
                                    cf77CornerDistance <= RouteMissionTraversalAdjacentBridgeMaxDistance &&
                                    Vector3.Dot(GetFlatDirection(cf77Corner - bot.Position),
                                        GetFlatDirection(rawExit - rawEntry)) >= -0.10f &&
                                    IsFinite(cf77Corner))
                                {
                                    cf77RejoinCorner = cf77Corner;
                                    cf77StrictGroundRoute = true;
                                    break;
                                }
                            }
                            cf77Rejection = cf77StrictGroundRoute ? "" : "noUsableShortCorner";
                        }
                        else
                        {
                            cf77Rejection = "strictCompleteShortPathMissing";
                        }
                    }
                    if (ShouldEmitDiagnostic(
                            "cf77OverflownDropDownGroundRejoin:" +
                            mission.MissionId.ToString(CultureInfo.InvariantCulture) + ":" +
                            step.EpisodeId.ToString(CultureInfo.InvariantCulture), bot, 0.45f))
                    {
                        WriteEvent("cf77OverflownDropDownGroundRejoinDecision", bot, new
                        {
                            missionId = mission.MissionId,
                            stepIndex = mission.CurrentStepIndex,
                            edgeId = step.EdgeId,
                            episodeId = step.EpisodeId,
                            chainId = step.ChainId,
                            predecessorEdgeId = cf77Predecessor.EdgeId,
                            predecessorEpisodeId = cf77Predecessor.EpisodeId,
                            predecessorPhysicalCompletionAge = Mathf.Max(0f,
                                Time.time - cf77PhysicalCompletion.CompletedAt),
                            position = Vec(bot.Position),
                            rawEntry = Vec(rawEntry),
                            rawExit = Vec(rawExit),
                            forwardEntryProjection,
                            entryBelowBody = bot.Position.y - rawEntry.y,
                            executorAdmissible = false,
                            strictGroundRoute = cf77StrictGroundRoute,
                            pathLength = cf77PathLength,
                            target = cf77StrictGroundRoute ? (object)Vec(cf77RejoinCorner) : null,
                            rejection = cf77Rejection,
                            traversalSuccessRecorded = false,
                            persistentLearningChanged = false,
                            bodyRelocated = false,
                            action = cf77StrictGroundRoute
                                ? "sameGenerationExistingProducerGetsStrictPhysicalRejoinCornerNoNativeCompletion"
                                : "noNewMovementAuthorityOrCompletionWithoutReachablePhysicalRejoin"
                        });
                    }
                    if (cf77StrictGroundRoute)
                    {
                        target = cf77RejoinCorner;
                        producerTargetAvailable = true;
                        actuatorReady = false;
                        source = "cf77ExactSuccessorStrictPhysicalGroundRejoin";
                        return true;
                    }
                }
            }

            // V4.63: V4.24's predicted readiness must not erase the physical producer while
            // the exact V3.2 native consumer is waiting for its actual DropDown entry plane.
            // Route and probe use the SAME current-body target builder, not rival contracts.
            if ((TryGetActiveProbe(bot, out ProbeRuntime learningV463Probe)) &&
                TryBuildLearningV463DeferredDropDownProducerTarget(
                    bot, learningV463Probe, mission, step, rawEntry, rawExit,
                    out Vector3 learningV463Target,
                    out float learningV463ForwardEntryProjection,
                    out float learningV463MeasuredForwardProgress))
            {
                target = learningV463Target;
                producerTargetAvailable = true;
                actuatorReady = false;
                source = "learningV463DeferredDropDownForwardProducer";
                if (ShouldEmitDiagnostic(
                    "learningV463DeferredDropDownForwardProducer:" +
                        mission.MissionId.ToString(CultureInfo.InvariantCulture) + ":" +
                        step.EdgeId.ToString(CultureInfo.InvariantCulture), bot, 0.18f))
                {
                    WriteEvent("learningV463DeferredDropDownForwardProducer", bot, new
                    {
                        missionId = mission.MissionId,
                        stepIndex = mission.CurrentStepIndex,
                        edgeId = step.EdgeId,
                        position = Vec(bot.Position),
                        target = Vec(target),
                        forwardEntryProjection = learningV463ForwardEntryProjection,
                        measuredEntryForwardProgress = learningV463MeasuredForwardProgress,
                        predictedExecutorReady = executorAdmissible,
                        actualV32EntryAcquired = false,
                        traversalSuccessRecorded = false,
                        persistentLearningChanged = false,
                        bodyRelocated = false,
                        action = "predictedReadyCannotRemoveForwardProducerBeforeActualPortalEntry"
                    });
                }
                return true;
            }
            bool orientationObserver = false;
            string orientationDetail = string.Empty;
            if (!executorAdmissible)
            {
                orientationObserver = TryGetLearningV425OrientationOnlyActuatorAdmission(
                    bot, step,
                    entryDistance, forwardEntryProjection, lateralEntryOffset, maxLateral,
                    alignmentAngle, maxAlignmentAngle, maxForwardOvershoot, triggerDistance,
                    out Vector3 orientationDirection, out orientationDetail);
                if (orientationDirection.sqrMagnitude > 0.5f)
                {
                    direction = orientationDirection;
                }

                // V4.53: the successor inherits the live body epoch from its predecessor. Reuse the
                // existing V4.24 executor envelope to derive the next producer from CURRENT body
                // state. A Vault/Climb body that already crossed its historical entry plane may keep
                // moving forward by the existing trigger distance; DropDown keeps the executor's
                // existing RawExit admission target. No new geometry budget or success proof exists.
                bool dropDown = string.Equals(step.Kind, "DropDown", StringComparison.OrdinalIgnoreCase);
                if (!dropDown && forwardEntryProjection > 0f && direction.sqrMagnitude > 0.5f)
                {
                    target = bot.Position + direction * triggerDistance;
                    target.y = bot.Position.y;
                }
                else
                {
                    target = admissionTarget;
                }
                producerTargetAvailable = IsFinite(target);
            }
            else
            {
                target = bot.Position;
                producerTargetAvailable = false;
            }

            source = executorAdmissible
                ? "learningV453SameChainSuccessorExecutorReady"
                : (orientationObserver
                    ? "learningV453SameChainSuccessorHeadingObserver"
                    : "learningV453SameChainSuccessorCurrentBodyProducer");
            if (ShouldEmitDiagnostic(
                    "learningV453SameChainSuccessorPhysicalProducer:" +
                        mission.MissionId.ToString(CultureInfo.InvariantCulture) + ":" +
                        step.EdgeId.ToString(CultureInfo.InvariantCulture),
                    bot, 0.18f))
            {
                WriteEvent("learningV453SameChainSuccessorPhysicalProducer", bot, new
                {
                    missionId = mission.MissionId,
                    stepIndex = mission.CurrentStepIndex,
                    edgeId = step.EdgeId,
                    kind = step.Kind,
                    position = Vec(bot.Position),
                    target = producerTargetAvailable ? (object)Vec(target) : null,
                    executorAdmissible,
                    orientationObserver,
                    orientationDetail,
                    entryDistance,
                    forwardEntryProjection,
                    lateralEntryOffset,
                    maxLateral,
                    alignmentAngle,
                    maxAlignmentAngle,
                    triggerDistance,
                    admissionDetail,
                    traversalSuccessRecorded = false,
                    persistentLearningChanged = false,
                    bodyRelocated = false,
                    action = executorAdmissible
                        ? "exactSameChainActuatorConsumesCurrentPhysicalEpochBeforeHistoricalApproachLane"
                        : "currentBodyExecutorEnvelopeOwnsSuccessorPreparationHistoricalPreEntryLaneCannotPullBodyBackward"
                });
            }
            return true;
        }

        private static bool TryConsumeRC58FreshMissionMeasuredLandingOverlap(
            BotOwner bot, FollowerRouteMission mission, FollowerRouteMissionStep step)
        {
            var r2DebtObservation16 = CaptureRouteDebtObservation(bot, mission, step, false);

            if (bot?.GetPlayer == null || mission?.Steps == null || step == null ||
                mission.CurrentStepIndex != 0 || mission.Steps.Count != 1 ||
                !step.CurrentRaidRouteAuthority || step.ChainId <= 0 || step.EpisodeId <= 1 ||
                !string.Equals(step.Kind, "DropDown", StringComparison.OrdinalIgnoreCase) ||
                HasActiveTraversalProbe(bot) || IsTeleportTransactionActive(bot) ||
                !IsEffectivelySupportedForProbe(bot, bot.GetPlayer))
                return false;
            string profileId = bot.ProfileId ?? string.Empty;
            if (string.IsNullOrEmpty(profileId) ||
                !FollowerTraversalRouteCursors.TryGetValue(profileId, out FollowerTraversalRouteCursor cursor) ||
                cursor == null || cursor.LastCompletedChainId != step.ChainId ||
                cursor.LastCompletedEpisodeId != step.EpisodeId - 1)
                return false;
            TraversalEpisodeMetadata predecessorEpisode = TraversalEpisodeStream.FirstOrDefault(e =>
                e != null && e.EpisodeId == cursor.LastCompletedEpisodeId &&
                e.ChainId == step.ChainId &&
                string.Equals(e.Kind, "DropDown", StringComparison.OrdinalIgnoreCase));
            if (predecessorEpisode == null || predecessorEpisode.EdgeId <= 0 ||
                !RecentTraversalCompletions.TryGetValue(
                    MakeRecentTraversalCompletionKey(profileId, predecessorEpisode.EdgeId),
                    out RecentTraversalCompletion previous) ||
                previous == null || !previous.Rc58VerifiedNativePhysical ||
                previous.Rc58EpisodeId != predecessorEpisode.EpisodeId ||
                previous.Rc58ChainId != step.ChainId ||
                Time.time > previous.ExpiresAt ||
                Time.time - previous.CompletedAt > 1.50f)
                return false;
            Vector3 start = previous.Rc58ObservedNativeStart;
            Vector3 finish = previous.Rc58ObservedNativeEnd;
            Vector3 entry = IsFinite(step.RawEntry) ? step.RawEntry : step.Entry;
            Vector3 exit = IsFinite(step.RawExit) ? step.RawExit : step.Exit;
            Vector3 axis = exit - entry;
            axis.y = 0f;
            float length = axis.magnitude;
            if (!IsFinite(start) || !IsFinite(finish) || !IsFinite(entry) ||
                !IsFinite(exit) || !IsFinite(bot.Position) || length < 0.35f ||
                entry.y < exit.y + DropDownAlreadyBelowLandingMargin ||
                Vector3.Distance(bot.Position, finish) > 1.50f)
                return false;
            axis /= length;
            Vector3 startOffset = start - entry;
            Vector3 finishOffset = finish - entry;
            startOffset.y = 0f;
            finishOffset.y = 0f;
            float startProjection = Vector3.Dot(startOffset, axis);
            float endProjection = Vector3.Dot(finishOffset, axis);
            float currentProjection = Vector3.Dot(bot.Position - entry, axis);
            float startLateral = (startOffset - axis * startProjection).magnitude;
            float finishLateral = (finishOffset - axis * endProjection).magnitude;
            float needed = Mathf.Max(0.12f, length - 0.25f);
            // Both sides of the exact NEXT action, not a landing height alone.
            // A small witness lateral is essential; an unrelated nearby native drop
            // must never silently consume a distinct actuator.
            bool exactTwoEndedCrossing =
                FollowerCapabilityHelperRules.TryConsumeRC58FreshMissionMeasuredLandingOverlap_exactTwoEndedCrossing_CC0D24AD_1(startProjection,endProjection,needed,currentProjection,needed,startLateral,RouteMissionDirectionalWalkThroughMaxLateral,finishLateral,RouteMissionDirectionalWalkThroughMaxLateral,start.y,entry.y,finish.y,exit.y,DropDownAlreadyBelowLandingMargin) &&
                bot.Position.y <= exit.y + DropDownAlreadyBelowLandingMargin &&
                Mathf.Abs(bot.Position.y - finish.y) <= 0.35f;
            if (!exactTwoEndedCrossing)
                return false;
            int oldIndex = mission.CurrentStepIndex;
            if (!ConsumeFollowerRouteMissionPhysicalPass(bot, mission, step,
                    "rc58FreshMissionPredecessorMeasuredNativeOverlap", currentProjection, needed))
                return false;
            if (!TryAdvanceRouteMission(bot, mission, mission.CurrentStepIndex + 1, RouteDebtEvidence.PhysicalPass, "TryConsumeRC58FreshMissionMeasuredLandingOverlap:14", r2DebtObservation16)) return false;
            mission.ApproachBreadcrumbSequence = Math.Max(
                mission.ApproachBreadcrumbSequence, step.EndBreadcrumbSequence);
            mission.LastProgressAt = Time.time;
            mission.LastProgressPosition = bot.Position;
            CommitCapabilityMoveRetry(mission,FollowerCapabilityRetryPolicy.Change.Reset,0f);
            mission.TraversalRetryMovementHoldUntil = 0f;
            WriteEvent("v2rc58FreshMissionMeasuredPhysicalSubsumption", bot, new
            {
                missionId = mission.MissionId, previousStepIndex = oldIndex,
                edgeId = step.EdgeId, episodeId = step.EpisodeId,
                predecessorEpisodeId = predecessorEpisode.EpisodeId,
                predecessorEdgeId = predecessorEpisode.EdgeId,
                nativeStart = Vec(start), nativeEnd = Vec(finish), body = Vec(bot.Position),
                startProjection, endProjection, currentProjection, needed,
                startLateral, finishLateral,
                nativeSuccessRecorded = false, traversalSuccessRecorded = false,
                persistentLearningChanged = false, bodyRelocated = false,
                action = "measuredTwoEndedNativePredecessorOverlapRetiresOnlyFreshMissionSuccessorExecutionDebt"
            });
            EnsureFollowerRouteMissionCommitment(bot, mission);
            return true;
        }

        private static bool TryAdvanceLearningV424PhysicallySubsumedActuatorDebt(
            BotOwner bot,
            FollowerRouteMission mission,
            string source)
        {
            if ((bot == null) || mission?.Steps == null ||
                mission.CurrentStepIndex < 0 || mission.CurrentStepIndex >= mission.Steps.Count)
            {
                return false;
            }

            FollowerRouteMissionStep step = mission.Steps[mission.CurrentStepIndex];
            var r2DebtObservation17 = CaptureRouteDebtObservation(bot, mission, step, false);

            if (TryConsumeRC58FreshMissionMeasuredLandingOverlap(bot, mission, step))
                return true;
            LearningV354TraversalCapabilityProfile profile = step != null
                ? GetLearningV354ActionProfile(step.Kind)
                : null;
            if (step == null || profile == null || !profile.RequiresActuator)
            {
                return false;
            }

            // LearningV4.30: physical subsumption is valid for a successor whose executor is
            // redundant because a predecessor physically carried the body through it. It is NOT
            // valid for the exact actuator probe that is still alive. V429 runtime showed the
            // mission cursor retiring the active Jump/DropDown while that probe later continued to
            // emit landing/egress failure, resurrecting an older entry/exit consumer.
            if ((TryGetActiveProbe(bot, out ProbeRuntime learningV430ActiveActuatorProbe)) &&
                learningV430ActiveActuatorProbe != null &&
                IsActiveProbe(learningV430ActiveActuatorProbe))
            {
                bool sameEpisode = step.EpisodeId > 0 &&
                    learningV430ActiveActuatorProbe.TraversalEpisodeId > 0 &&
                    step.EpisodeId == learningV430ActiveActuatorProbe.TraversalEpisodeId;
                bool sameEdge = step.EdgeId > 0 &&
                    learningV430ActiveActuatorProbe.LearnedEdgeId > 0 &&
                    step.EdgeId == learningV430ActiveActuatorProbe.LearnedEdgeId;
                // Episode identity is authoritative whenever both producers carry it. EdgeId is
                // only a compatibility fallback for older/incomplete producers; using sameEdge
                // unconditionally can retain a stale probe when a repeated edge is revisited in a
                // later route mission.
                bool sameEdgeFallback = sameEdge &&
                    (step.EpisodeId <= 0 || learningV430ActiveActuatorProbe.TraversalEpisodeId <= 0);
                if (sameEpisode || sameEdgeFallback)
                {
                    if (ShouldEmitDiagnostic(
                            "learningV430ActiveActuatorDebtRetained:" +
                                step.EdgeId.ToString(CultureInfo.InvariantCulture),
                            bot, 0.20f))
                    {
                        WriteEvent("learningV430ActiveActuatorDebtRetained", bot, new
                        {
                            missionId = mission.MissionId,
                            stepIndex = mission.CurrentStepIndex,
                            episodeId = step.EpisodeId,
                            edgeId = step.EdgeId,
                            kind = step.Kind,
                            probeStage = learningV430ActiveActuatorProbe.Stage.ToString(),
                            probeEdgeId = learningV430ActiveActuatorProbe.LearnedEdgeId,
                            probeEpisodeId = learningV430ActiveActuatorProbe.TraversalEpisodeId,
                            source = source ?? string.Empty,
                            traversalSuccessRecorded = false,
                            persistentLearningChanged = false,
                            action = "exactLiveActuatorOwnsItsDebtUntilProbeCompletionSuccessorSubsumptionRemainsAllowed"
                        });
                    }
                    return false;
                }
            }

            int previousStepIndex = mission.CurrentStepIndex;
            if (!TryConsumeLearningV382PhysicallySubsumedSuccessorDebt(
                    bot, mission, step,
                    out float projection, out float requiredProjection))
            {
                return false;
            }

            if (!TryAdvanceRouteMission(bot, mission, mission.CurrentStepIndex + 1, RouteDebtEvidence.PhysicalPass, "TryAdvanceLearningV424PhysicallySubsumedActuatorDebt:15", r2DebtObservation17)) return false;
            mission.ApproachBreadcrumbSequence = Math.Max(
                mission.ApproachBreadcrumbSequence, step.EndBreadcrumbSequence);
            mission.LastProgressAt = Time.time;
            mission.LastProgressPosition = bot.Position;
            CommitCapabilityMoveRetry(mission,FollowerCapabilityRetryPolicy.Change.Reset,0f);
            mission.TraversalRetryMovementHoldUntil = 0f;
            mission.LearningV355ProgressStepIndex = -1;

            WriteEvent("learningV424ActuatorSuccessorPhysicallySubsumed", bot, new
            {
                missionId = mission.MissionId,
                previousStepIndex,
                currentStepIndex = mission.CurrentStepIndex,
                episodeId = step.EpisodeId,
                edgeId = step.EdgeId,
                kind = step.Kind,
                projection,
                requiredProjection,
                source = source ?? string.Empty,
                traversalSuccessRecorded = false,
                nativeSuccessRecorded = false,
                persistentLearningChanged = false,
                bodyRelocated = false,
                action = "bodyChronologyBeyondActuatorRetiresExecutionDebtBeforeAnyEntryProducerCanRewindOrHold"
            });

            EnsureFollowerRouteMissionCommitment(bot, mission);
            return true;
        }

        private static bool TryAdvanceLearningV468ObservedDropDownLandingDebt(
            BotOwner bot,
            FollowerRouteMission mission,
            FollowerTraversalRouteCursor cursor)
        {
            if ((bot?.GetPlayer == null) ||
                mission?.Steps == null || cursor == null ||
                !cursor.LearningV375TransactionActive ||
                cursor.LearningV375RouteMissionId != mission.MissionId ||
                mission.CurrentStepIndex <= 0 || mission.CurrentStepIndex >= mission.Steps.Count)
            {
                return false;
            }

            FollowerRouteMissionStep step = mission.Steps[mission.CurrentStepIndex];
            var r2DebtObservation18 = CaptureRouteDebtObservation(bot, mission, step, false);

            FollowerRouteMissionStep previous = mission.Steps[mission.CurrentStepIndex - 1];
            // A live native probe or teleport has its own body authority. Do not retain a
            // previously sampled upper-side witness across those unrelated physical epochs.
            if (step != null && (HasActiveTraversalProbe(bot) || IsTeleportTransactionActive(bot)))
            {
                step.LearningV468ObservedEntryWitness = false;
                return false;
            }
            if (step == null || previous == null ||
                !string.Equals(step.Kind, "DropDown", StringComparison.OrdinalIgnoreCase) ||
                step.EpisodeId <= 0 || step.EndBreadcrumbSequence <= 0 ||
                !step.CurrentRaidRouteAuthority || !previous.CurrentRaidRouteAuthority ||
                previous.ChainId <= 0 || previous.ChainId != step.ChainId ||
                cursor.LastCompletedEpisodeId != previous.EpisodeId ||
                cursor.LastCompletedChainId != step.ChainId ||
                !IsRouteMissionSameChainImmediateSuccessor(mission, mission.CurrentStepIndex))
            {
                return false;
            }

            long generation = FollowerMovementWriterAuthority.GetCurrentGeneration(
                bot, out FollowerMovementWriterAuthority.WriterDomain owner);
            if (generation <= 0 || generation != cursor.LearningV375MovementGeneration ||
                owner != FollowerMovementWriterAuthority.WriterDomain.Traversal)
            {
                // Once writer authority rolls, the previous body's witness is no longer proof.
                step.LearningV468ObservedEntryWitness = false;
                return false;
            }

            Vector3 entry = IsFinite(step.RawEntry) ? step.RawEntry : step.Entry;
            Vector3 exit = IsFinite(step.RawExit) ? step.RawExit : step.Exit;
            Vector3 axis = exit - entry;
            axis.y = 0f;
            float length = axis.magnitude;
            if (!IsFinite(entry) || !IsFinite(exit) || !IsFinite(bot.Position) ||
                length < 0.25f || entry.y <= exit.y + DropDownAlreadyBelowLandingMargin)
            {
                return false;
            }

            Vector3 direction = axis / length;
            Vector3 offset = bot.Position - entry;
            offset.y = 0f;
            float projection = Vector3.Dot(offset, direction);
            float requiredProjection = Mathf.Max(0.12f, length - 0.25f);
            float lateral = GetPlanarDistanceToSegment(bot.Position, entry, exit);
            bool local = lateral <= RouteMissionDirectionalWalkThroughMaxLateral;
            if (!step.LearningV468ObservedEntryWitness)
            {
                // A fresh witness must be collected while the body is still on the upper side.
                // Never retrospectively infer a drop from a low Y coordinate or route arc alone.
                if (local && projection < requiredProjection && bot.Position.y >= entry.y)
                {
                    // The preceding Jump may release the body into a real airborne DropDown.
                    // Upper-side support is NOT asserted; the final lower-side supported landing
                    // and measured body displacement remain mandatory before retirement.
                    step.LearningV468ObservedEntryWitness = true;
                    step.LearningV468EntryWitnessPosition = bot.Position;
                    step.LearningV468LastObservedBodyPosition = bot.Position;
                    step.LearningV468EntryWitnessProjection = projection;
                    step.LearningV468EntryWitnessGeneration = generation;
                    WriteEvent("learningV468DropDownUpperBodyWitnessCaptured", bot, new
                    {
                        missionId = mission.MissionId,
                        stepIndex = mission.CurrentStepIndex,
                        episodeId = step.EpisodeId,
                        predecessorEpisodeId = previous.EpisodeId,
                        generation,
                        upperBody = Vec(bot.Position),
                        projection,
                        requiredProjection,
                        nativeSuccessRecorded = false,
                        traversalSuccessRecorded = false,
                        persistentLearningChanged = false,
                        action = "observeOnlyCurrentBodyBeforeLandingNoCompletionCredit"
                    });
                }
                return false;
            }

            if (step.LearningV468EntryWitnessGeneration != generation)
            {
                step.LearningV468ObservedEntryWitness = false;
                return false;
            }

            // Same-generation identity is necessary but cannot by itself exclude an external
            // position writer. Reject an unobserved physical jump in this step's sampled body
            // stream using the established bounded physical-egress distance, not a new tolerance.
            float sampleDisplacement = Vector3.Distance(
                bot.Position, step.LearningV468LastObservedBodyPosition);
            if (sampleDisplacement > UnifiedTraversalEgressAuthorityMaxDistance)
            {
                step.LearningV468ObservedEntryWitness = false;
                return false;
            }
            step.LearningV468LastObservedBodyPosition = bot.Position;

            bool forwardCrossed = projection >= requiredProjection &&
                projection > step.LearningV468EntryWitnessProjection &&
                Vector3.Dot(bot.Position - step.LearningV468EntryWitnessPosition, direction) > 0f;
            bool physicallyDescended =
                step.LearningV468EntryWitnessPosition.y - bot.Position.y >=
                entry.y - exit.y - DropDownAlreadyBelowLandingMargin;
            bool genuinelyLanded = IsEffectivelySupportedForProbe(bot, bot.GetPlayer) &&
                IsFollowerAlreadyBelowDropDownLanding(bot.Position, entry, exit, step.Exit) &&
                GetPlanarDistance(bot.Position, exit) <= 1.75f;
            if (!local || !forwardCrossed || !physicallyDescended || !genuinelyLanded)
            {
                return false;
            }

            // No new traversal success, Native credit, skip of an active probe, teleport,
            // learned edge update, or global kind-specific fallback. Only the exact episode's
            // runtime execution debt and monotonic breadcrumb floor are consumed.
            int previousStepIndex = mission.CurrentStepIndex;
            bool consumed = ConsumeFollowerRouteMissionPhysicalPass(
                bot, mission, step, "learningV468ObservedNaturalDropDownLanding",
                projection, requiredProjection);
            if (!consumed)
            {
                return false;
            }
            if (!TryAdvanceRouteMission(bot, mission, mission.CurrentStepIndex + 1, RouteDebtEvidence.PhysicalPass, "TryAdvanceLearningV468ObservedDropDownLandingDebt:16", r2DebtObservation18)) return false;
            mission.ApproachBreadcrumbSequence = Math.Max(
                mission.ApproachBreadcrumbSequence, step.EndBreadcrumbSequence);
            mission.LastProgressAt = Time.time;
            mission.LastProgressPosition = bot.Position;
            CommitCapabilityMoveRetry(mission,FollowerCapabilityRetryPolicy.Change.Reset,0f);
            mission.TraversalRetryMovementHoldUntil = 0f;
            mission.LearningV355ProgressStepIndex = -1;
            ClearLearningV351TerminalRecoveryState(
                bot, "learningV468ObservedNaturalDropDownLanding");
            WriteEvent("learningV468ObservedDropDownLandingExecutionDebtRetired", bot, new
            {
                missionId = mission.MissionId,
                previousStepIndex,
                currentStepIndex = mission.CurrentStepIndex,
                episodeId = step.EpisodeId,
                edgeId = step.EdgeId,
                chainId = step.ChainId,
                predecessorEpisodeId = previous.EpisodeId,
                generation,
                initialBody = Vec(step.LearningV468EntryWitnessPosition),
                landedBody = Vec(bot.Position),
                entry = Vec(entry),
                exit = Vec(exit),
                projection,
                requiredProjection,
                lateral,
                requiresNativeCompletion = false,
                realSupportedDescentObserved = true,
                nativeSuccessRecorded = false,
                traversalSuccessRecorded = false,
                persistentLearningChanged = false,
                bodyRelocated = false,
                action = "twoEndedSameGenerationSupportedDescentRetiresOnlyExactRouteExecutionDebt"
            });
            EnsureFollowerRouteMissionCommitment(bot, mission);
            return true;
        }

        private static bool TryArbitrateLearningV447RegressedJumpPreAdmissionProducer(
            BotOwner bot,
            FollowerRouteMission mission,
            FollowerTraversalRouteCursor cursor,
            bool routeRegressionInvalidatesProducer)
        {
            if ((!routeRegressionInvalidatesProducer) || bot == null || mission == null || cursor == null ||
                !string.Equals(cursor.LearningV375ProducerKind, "ActuatorPhysicalPreEntry", StringComparison.Ordinal) ||
                HasActiveTraversalProbe(bot) || mission.CurrentStepIndex < 0 ||
                mission.CurrentStepIndex >= mission.Steps.Count)
            {
                return false;
            }

            FollowerRouteMissionStep step = mission.Steps[mission.CurrentStepIndex];
            if (step == null || !string.Equals(step.Kind, "Jump", StringComparison.OrdinalIgnoreCase) ||
                IsLearningV446PhysicalStrategyRetiredForCurrentStep(mission, step, step.Kind))
            {
                return false;
            }

            bool learningV447HaveNavMeshFunnel = TryGetLearningV353JumpRouteFunnelTarget(
                bot, step,
                out _, out _,
                out float jumpForwardProjection,
                out float jumpLateralOffset,
                out float jumpFacingDot,
                out float jumpMovementHeadingDot,
                out float jumpPlanarSpeed);
            // V448: V3.53 can return false only because its staging target has no NavMesh sample.
            // Its physical projection/alignment outputs are computed before that backend check.
            // Do not make HOW-switch arbitration disappear merely because NavMesh is absent; the
            // collision-active V448 producer is intentionally pathless. A non-finite/invalid
            // geometry result leaves projection at its conservative default and still returns here.
            if ((!learningV447HaveNavMeshFunnel && false) ||
                jumpForwardProjection <= LearningV352JumpTakeoffCorridorMaxForwardOvershoot)
            {
                return false;
            }

            long currentGeneration = FollowerMovementWriterAuthority.GetCurrentGeneration(
                bot, out FollowerMovementWriterAuthority.WriterDomain currentOwner);
            float reservedMaxForward =
                step.LearningV429JumpAdmissionForwardProjection +
                Mathf.Max(0f, step.LearningV429JumpAdmissionLeadDistance);
            bool liveReservedTakeoffEnvelope =
                (step.LearningV429JumpAdmissionReserved) &&
                step.LearningV429JumpAdmissionMovementGeneration > 0 &&
                step.LearningV429JumpAdmissionMovementGeneration == currentGeneration &&
                currentOwner == FollowerMovementWriterAuthority.WriterDomain.Traversal &&
                FollowerCapabilityHelperRules.TryArbitrateLearningV447RegressedJumpPreAdmissionProducer_liveReservedTakeoffEnvelope_468F90AF_1(jumpForwardProjection,step.LearningV429JumpAdmissionForwardProjection) &&
                jumpForwardProjection <= reservedMaxForward;
            if (liveReservedTakeoffEnvelope)
            {
                return false;
            }

            {
                bool learningV448LiveOpportunity =
                    TryGetLearningV448LiveJumpOpportunity(
                        bot, step,
                        out float learningV448Projection,
                        out float learningV448Lateral,
                        out float learningV448Facing,
                        out float learningV448Heading,
                        out float learningV448Speed,
                        out float learningV448ReservedMax);
                if (learningV448LiveOpportunity)
                {
                    if (ShouldEmitDiagnostic(
                            "learningV448RegressionCannotPreemptLiveJumpOpportunity:" +
                            step.EdgeId.ToString(CultureInfo.InvariantCulture), bot, 0.12f))
                    {
                        WriteEvent("learningV448RegressionCannotPreemptLiveJumpOpportunity", bot, new
                        {
                            missionId = mission.MissionId,
                            stepIndex = mission.CurrentStepIndex,
                            edgeId = step.EdgeId,
                            forwardEntryProjection = learningV448Projection,
                            lateralEntryOffset = learningV448Lateral,
                            facingDot = learningV448Facing,
                            movementHeadingDot = learningV448Heading,
                            planarSpeed = learningV448Speed,
                            reservedMaxForwardProjection = learningV448ReservedMax,
                            traversalSuccessRecorded = false,
                            persistentLearningChanged = false,
                            bodyRelocated = false,
                            action = "singleBackwardSampleCannotRetireCurrentSameGenerationNativeOpportunity"
                        });
                    }
                    return false;
                }

                float learningV448NoProgressSeconds =
                    Mathf.Max(0f, Time.time - mission.LastProgressAt);
                if (learningV448NoProgressSeconds < RouteMissionTraversalNoProgressReleaseSeconds)
                {
                    if (ShouldEmitDiagnostic(
                            "learningV448RegressedProducerRetainedWithinExistingNoProgressBudget:" +
                            step.EdgeId.ToString(CultureInfo.InvariantCulture), bot, 0.12f))
                    {
                        WriteEvent("learningV448RegressedProducerRetainedWithinExistingNoProgressBudget", bot, new
                        {
                            missionId = mission.MissionId,
                            stepIndex = mission.CurrentStepIndex,
                            edgeId = step.EdgeId,
                            jumpForwardProjection,
                            jumpLateralOffset,
                            jumpFacingDot,
                            jumpMovementHeadingDot,
                            jumpPlanarSpeed,
                            noProgressSeconds = learningV448NoProgressSeconds,
                            existingNoProgressBudget = RouteMissionTraversalNoProgressReleaseSeconds,
                            traversalSuccessRecorded = false,
                            persistentLearningChanged = false,
                            bodyRelocated = false,
                            action = "rebaseForwardPhysicalProducerBeforeChangingHOWNoNewTimerAdded"
                        });
                    }
                    return false;
                }
            }

            string source = "learningV447PhysicalRegressionPastJumpTakeoffFrontier";
            if (!TryBeginLearningV433EquivalentPhysicalActuatorFrontierRecovery(
                    bot, source, out string transferSource))
            {
                return false;
            }

            WriteEvent("learningV447RegressedPreAdmissionProducerYieldedToActuator", bot, new
            {
                missionId = mission.MissionId,
                stepIndex = mission.CurrentStepIndex,
                semanticEdgeId = step.EdgeId,
                semanticKind = step.Kind,
                producer = cursor.LearningV375ProducerKind ?? string.Empty,
                jumpForwardProjection,
                jumpLateralOffset,
                jumpFacingDot,
                jumpMovementHeadingDot,
                jumpPlanarSpeed,
                maxForwardOvershoot = LearningV352JumpTakeoffCorridorMaxForwardOvershoot,
                transferSource,
                semanticDebtPreserved = true,
                primaryPhysicalStrategyRetired = true,
                traversalSuccessRecorded = false,
                persistentLearningChanged = false,
                bodyRelocated = false,
                action = "existingPhysicalRegressionProofYieldsPastTakeoffProducerToEquivalentStrategyWithoutRetryLoop"
            });
            return true;
        }

        private static bool TryGetLearningV377ActuatorPhysicalFrontierDecision(
            BotOwner bot,
            FollowerRouteMission mission,
            float bodyArc,
            ref Vector3 target,
            ref Vector3 tangent,
            ref float targetArc,
            out bool actuatorReady,
            out bool producerTargetAvailable,
            out int edgeId,
            out string kind,
            out string source)
        {
            actuatorReady = false;
            producerTargetAvailable = false;
            edgeId = 0;
            kind = string.Empty;
            source = string.Empty;
            if ((bot == null) || mission == null ||
                mission.CurrentStepIndex < 0 || mission.CurrentStepIndex >= mission.Steps.Count)
            {
                return false;
            }

            FollowerRouteMissionStep step = mission.Steps[mission.CurrentStepIndex];
            LearningV354TraversalCapabilityProfile profile = step != null
                ? GetLearningV354ActionProfile(step.Kind)
                : null;
            if (step == null || profile == null || !profile.RequiresActuator ||
                IsFollowerRouteMissionStepPhysicallyPassed(bot, step, out _, out _))
            {
                return false;
            }

            edgeId = step.EdgeId;
            kind = step.Kind ?? string.Empty;

            // V434: once V433 has selected a same-frontier equivalent physical actuator, that
            // transient commitment is not merely advisory. It becomes the physical producer identity
            // for THIS RouteMission step until its real native probe succeeds/fails or expires.
            if ((!string.IsNullOrEmpty(bot.ProfileId)) &&
                LearnedTraversalCommitments.TryGetValue(
                    bot.ProfileId, out LearnedTraversalCommitment learningV434Substitute) &&
                IsLearningV433ExactLocalActuatorSubstitution(learningV434Substitute) &&
                learningV434Substitute.RouteMissionId == mission.MissionId &&
                learningV434Substitute.RouteMissionStepIndex == mission.CurrentStepIndex &&
                learningV434Substitute.ExpiresAt >= Time.time)
            {
                Vector3 substituteEntry = IsFinite(learningV434Substitute.RawEntry)
                    ? learningV434Substitute.RawEntry : learningV434Substitute.Entry;
                Vector3 substituteExit = IsFinite(learningV434Substitute.RawExit)
                    ? learningV434Substitute.RawExit : learningV434Substitute.Exit;
                Vector3 substituteDirection = GetFlatDirection(substituteExit - substituteEntry);
                if (IsFinite(substituteEntry) && IsFinite(substituteExit) &&
                    substituteDirection.sqrMagnitude >= 0.5f)
                {
                    edgeId = learningV434Substitute.EdgeId;
                    kind = learningV434Substitute.Kind ?? string.Empty;
                    target = substituteEntry;
                    tangent = substituteDirection;
                    if (!TryProjectLearningV375RouteArc(
                            mission, substituteEntry, out targetArc, out _, out _, out _))
                    {
                        targetArc = bodyArc;
                    }
                    producerTargetAvailable = true;
                    float substituteEntryDistance = GetPlanarDistance(bot.Position, learningV434Substitute.Entry);
                    actuatorReady = ShouldArmLearnedTraversalProbe(
                        bot, kind, substituteEntryDistance,
                        learningV434Substitute.Entry, substituteExit);
                    source = actuatorReady
                        ? "learningV434EquivalentActuatorSubstitutionReady"
                        : "learningV434EquivalentActuatorSubstitutionApproach";

                    if (ShouldEmitDiagnostic(
                            "learningV434EquivalentActuatorSubstitutionOwnsCurrentStep:" +
                            mission.MissionId.ToString(CultureInfo.InvariantCulture) + ":" +
                            edgeId.ToString(CultureInfo.InvariantCulture), bot, 0.18f))
                    {
                        WriteEvent("learningV434EquivalentActuatorSubstitutionOwnsCurrentStep", bot, new
                        {
                            missionId = mission.MissionId,
                            stepIndex = mission.CurrentStepIndex,
                            originalEdgeId = step.EdgeId,
                            originalKind = step.Kind,
                            substituteEdgeId = learningV434Substitute.EdgeId,
                            substituteKind = learningV434Substitute.Kind,
                            substituteEntry = Vec(substituteEntry),
                            substituteExit = Vec(substituteExit),
                            entryDistance = substituteEntryDistance,
                            actuatorReady,
                            producerTargetAvailable = true,
                            movementOwner = "Traversal",
                            traversalSuccessRecorded = false,
                            persistentLearningChanged = false,
                            bodyRelocated = false,
                            action = "transientSameFrontierSubstituteOwnsPhysicalProducerAndProbeDispatchUntilTerminal"
                        });
                    }
                    return true;
                }
            }

            if ((string.Equals(kind, "Jump", StringComparison.OrdinalIgnoreCase)) &&
                step.LearningV455NativeJumpHowObserved)
            {
                Vector3 learningV455Entry = IsFinite(step.RawEntry) ? step.RawEntry : step.Entry;
                Vector3 learningV455Exit = IsFinite(step.RawExit) ? step.RawExit : step.Exit;
                Vector3 learningV455Direction = GetFlatDirection(learningV455Exit - learningV455Entry);

                // V4.60 corrects the lifetime meaning of the existing no-progress lease. V455
                // measured only how long this continuation had existed, so a body that was visibly
                // advancing could still age out after the fixed recovery window. Renew the SAME
                // budget only from measured forward body progress; do not add time or distance.
                if ((learningV455Direction.sqrMagnitude > 0.5f) &&
                    mission.LastProgressAt > 0f &&
                    IsFinite(mission.LastProgressPosition))
                {
                    Vector3 learningV460ProgressDelta = bot.Position - mission.LastProgressPosition;
                    learningV460ProgressDelta.y = 0f;
                    float learningV460ForwardProgress =
                        Vector3.Dot(learningV460ProgressDelta, learningV455Direction);
                    float learningV460PreviousRemaining =
                        GetPlanarDistance(mission.LastProgressPosition, learningV455Exit);
                    float learningV460CurrentRemaining =
                        GetPlanarDistance(bot.Position, learningV455Exit);
                    float learningV460TargetClosure =
                        learningV460PreviousRemaining - learningV460CurrentRemaining;
                    if (learningV460ForwardProgress > LearningV32EntryProgressEpsilon &&
                        learningV460TargetClosure > LearningV32EntryProgressEpsilon)
                    {
                        mission.LastProgressAt = Time.time;
                        mission.LastProgressPosition = bot.Position;
                        if (ShouldEmitDiagnostic(
                                "learningV460NativeJumpContinuationProgressRenewed:" +
                                step.EdgeId.ToString(CultureInfo.InvariantCulture), bot, 0.18f))
                        {
                            WriteEvent("learningV460NativeJumpContinuationProgressRenewed", bot, new
                            {
                                missionId = mission.MissionId,
                                stepIndex = mission.CurrentStepIndex,
                                edgeId = step.EdgeId,
                                body = Vec(bot.Position),
                                forwardProgress = learningV460ForwardProgress,
                                targetClosure = learningV460TargetClosure,
                                remaining = learningV460CurrentRemaining,
                                existingProgressEpsilon = LearningV32EntryProgressEpsilon,
                                traversalSuccessRecorded = false,
                                persistentLearningChanged = false,
                                bodyRelocated = false,
                                action = "renewExistingNoProgressLeaseOnlyFromMeasuredForwardCurrentBodyContinuation"
                            });
                        }
                    }
                }

                float learningV455NoProgress = Mathf.Max(0f, Time.time - mission.LastProgressAt);
                if (learningV455NoProgress < RouteMissionTraversalNoProgressReleaseSeconds)
                {
                    if (learningV455Direction.sqrMagnitude > 0.5f)
                    {
                        float remaining = GetPlanarDistance(bot.Position, learningV455Exit);
                        float advance = Mathf.Min(ProbeJumpRunupDistance, remaining);
                        if (advance > LearningV32EntryProgressEpsilon)
                        {
                            target = bot.Position + learningV455Direction * advance;
                            target.y = bot.Position.y;
                            tangent = learningV455Direction;
                            producerTargetAvailable = IsFinite(target);
                            actuatorReady = false;
                            if (TryProjectLearningV375RouteArc(
                                    mission, target, out float learningV455TargetArc, out _, out _, out _))
                            {
                                targetArc = learningV455TargetArc;
                            }
                            else
                            {
                                targetArc = bodyArc;
                            }
                            source = "learningV455NativeJumpCurrentBodySemanticContinuation";
                            if (ShouldEmitDiagnostic(
                                    "learningV455NativeJumpCurrentBodySemanticContinuation:" +
                                    step.EdgeId.ToString(CultureInfo.InvariantCulture), bot, 0.18f))
                            {
                                WriteEvent("learningV455NativeJumpCurrentBodySemanticContinuation", bot, new
                                {
                                    missionId = mission.MissionId,
                                    stepIndex = mission.CurrentStepIndex,
                                    edgeId = step.EdgeId,
                                    body = Vec(bot.Position),
                                    target = Vec(target),
                                    originalPhysicalHowBody = Vec(step.LearningV455NativeJumpHowBody),
                                    noProgressSeconds = learningV455NoProgress,
                                    existingNoProgressBudget = RouteMissionTraversalNoProgressReleaseSeconds,
                                    traversalSuccessRecorded = false,
                                    persistentLearningChanged = false,
                                    bodyRelocated = false,
                                    action = "realNativeJumpHOWAlreadyOccurredDriveRemainingWHATForwardFromCurrentBodyNoOldEntryReacquire"
                                });
                            }
                            return true;
                        }
                    }
                }
                else
                {
                    step.LearningV455NativeJumpHowObserved = false;
                    WriteEvent("learningV455NativeJumpCurrentBodyContinuationYieldedToExistingRecovery", bot, new
                    {
                        missionId = mission.MissionId,
                        stepIndex = mission.CurrentStepIndex,
                        edgeId = step.EdgeId,
                        body = Vec(bot.Position),
                        noProgressSeconds = learningV455NoProgress,
                        existingNoProgressBudget = RouteMissionTraversalNoProgressReleaseSeconds,
                        traversalSuccessRecorded = false,
                        persistentLearningChanged = false,
                        bodyRelocated = false,
                        action = "currentBodyContinuationCouldNotAdvanceYieldHOWToExistingSamePortalAssistRecoveryWithoutRewind"
                    });
                }
            }

            // V3.86: Jump keeps the proven V3.52/V3.56 rolling corridor; the fix is ownership, not
            // a new Jump model. Once V375 owns the same-chain route, the Follow-side funnel can no
            // longer be the only consumer that starts the actuator. Reuse the exact existing funnel
            // proof here so persistent route ownership stages the same run-through target and hands
            // the live body directly to Native Jump when that proof becomes ready.
            if ((string.Equals(kind, "Jump", StringComparison.OrdinalIgnoreCase)))
            {
                bool haveJumpFunnel = TryGetLearningV353JumpRouteFunnelTarget(
                    bot, step,
                    out Vector3 jumpTarget,
                    out bool jumpReady,
                    out float jumpForwardProjection,
                    out float jumpLateralOffset,
                    out float jumpFacingDot,
                    out float jumpMovementHeadingDot,
                    out float jumpPlanarSpeed);

                bool learningV448MonotonicOpportunityTarget = false;
                if ((!jumpReady) &&
                    TryBuildLearningV448MonotonicJumpOpportunityTarget(
                        bot, step,
                        out Vector3 learningV448JumpTarget,
                        out float learningV448TargetProjection))
                {
                    jumpTarget = learningV448JumpTarget;
                    jumpForwardProjection = learningV448TargetProjection;
                    haveJumpFunnel = true;
                    learningV448MonotonicOpportunityTarget = true;
                    if (ShouldEmitDiagnostic(
                            "learningV448MonotonicJumpOpportunityTarget:" +
                            step.EdgeId.ToString(CultureInfo.InvariantCulture), bot, 0.18f))
                    {
                        WriteEvent("learningV448MonotonicJumpOpportunityTargetPublished", bot, new
                        {
                            missionId = mission.MissionId,
                            stepIndex = mission.CurrentStepIndex,
                            edgeId = step.EdgeId,
                            position = Vec(bot.Position),
                            target = Vec(jumpTarget),
                            forwardEntryProjection = jumpForwardProjection,
                            horizonDistance = ProbeJumpRunupDistance,
                            navMeshRequired = false,
                            traversalSuccessRecorded = false,
                            persistentLearningChanged = false,
                            bodyRelocated = false,
                            action = ("rebuildForwardCollisionActiveHorizonOnTypedTakeoffAxisCenterWhileAdvancingNoNavMesh")
                        });
                    }
                }

                actuatorReady = haveJumpFunnel && jumpReady;
                producerTargetAvailable = haveJumpFunnel && IsFinite(jumpTarget);

                // V448: producer availability is not body progress. The old fixed funnel could stay
                // "available" forever while the body was physically blocked at the obstacle, which
                // prevented the existing V433 no-progress self-rescue from ever running. Keep the
                // same 0.90s mission no-progress budget, but evaluate it independently of whether a
                // forward target can still be authored. Live same-generation Jump opportunity wins;
                // otherwise HOW may change while WHAT (this Jump semantic debt) remains intact.
                float learningV448OpportunityNoProgressSeconds =
                    Mathf.Max(0f, Time.time - mission.LastProgressAt);
                bool learningV448OpportunityStillLive =
                    TryGetLearningV448LiveJumpOpportunity(
                        bot, step, out _, out _, out _, out _, out _, out _);
                if ((!actuatorReady) &&
                    !learningV448OpportunityStillLive &&
                    learningV448OpportunityNoProgressSeconds >= RouteMissionTraversalNoProgressReleaseSeconds &&
                    TryBeginLearningV433EquivalentPhysicalActuatorFrontierRecovery(
                        bot, "learningV448JumpOpportunityNoProgressSameFrontier",
                        out string learningV448NoProgressTransferSource))
                {
                    actuatorReady = false;
                    producerTargetAvailable = false;
                    source = learningV448NoProgressTransferSource;
                    WriteEvent("learningV448JumpOpportunityNoProgressYieldedToEquivalentActuator", bot, new
                    {
                        missionId = mission.MissionId,
                        stepIndex = mission.CurrentStepIndex,
                        semanticEdgeId = step.EdgeId,
                        semanticKind = step.Kind,
                        jumpForwardProjection,
                        jumpLateralOffset,
                        jumpFacingDot,
                        jumpMovementHeadingDot,
                        jumpPlanarSpeed,
                        noProgressSeconds = learningV448OpportunityNoProgressSeconds,
                        existingNoProgressBudget = RouteMissionTraversalNoProgressReleaseSeconds,
                        transferSource = learningV448NoProgressTransferSource,
                        semanticDebtPreserved = true,
                        traversalSuccessRecorded = false,
                        persistentLearningChanged = false,
                        bodyRelocated = false,
                        action = "availableTargetWithoutBodyProgressCannotHoldHOWForeverExistingSameFrontierFallbackOwnsSelfRescue"
                    });
                    return true;
                }

                Vector3 jumpDirection = GetFlatDirection(
                    (IsFinite(step.RawExit) ? step.RawExit : step.Exit) -
                    (IsFinite(step.RawEntry) ? step.RawEntry : step.Entry));
                if (jumpDirection.sqrMagnitude > 0.5f)
                {
                    tangent = jumpDirection;
                }
                if (actuatorReady)
                {
                    target = bot.Position;
                    targetArc = bodyArc;
                    source = "learningV353JumpRollingFunnelReady";
                }
                else if (producerTargetAvailable)
                {
                    target = jumpTarget;
                    if (TryProjectLearningV375RouteArc(
                            mission, jumpTarget, out float jumpTargetArc, out _, out _, out _))
                    {
                        targetArc = jumpTargetArc;
                    }
                    source = learningV448MonotonicOpportunityTarget
                        ? "learningV448MonotonicJumpOpportunityStaging"
                        : "learningV353JumpRollingFunnelStaging";
                }
                else
                {
                    source = "learningV353JumpRollingFunnelUnavailableHoldActionDebt";
                    bool learningV436EquivalentRecoverySelected = false;
                    long learningV439CurrentGeneration =
                        FollowerMovementWriterAuthority.GetCurrentGeneration(
                            bot, out FollowerMovementWriterAuthority.WriterDomain learningV439CurrentOwner);
                    float learningV439ReservedMaxForward =
                        step.LearningV429JumpAdmissionForwardProjection +
                        Mathf.Max(0f, step.LearningV429JumpAdmissionLeadDistance);
                    ;
                    bool learningV439LiveReservedTakeoffEnvelope =
                        (step.LearningV429JumpAdmissionReserved) &&
                        step.LearningV429JumpAdmissionMovementGeneration > 0 &&
                        step.LearningV429JumpAdmissionMovementGeneration == learningV439CurrentGeneration &&
                        learningV439CurrentOwner == FollowerMovementWriterAuthority.WriterDomain.Traversal &&
                        FollowerCapabilityHelperRules.TryGetLearningV377ActuatorPhysicalFrontierDecision_learningV439LiveReservedTakeoffEnvelope_B3712084_1(jumpForwardProjection,step.LearningV429JumpAdmissionForwardProjection) &&
                        jumpForwardProjection <= learningV439ReservedMaxForward;
                    bool learningV437MissedTakeoffFrontier =
                        (((IsLearningV449JumpTakeoffFrontierMissed(jumpForwardProjection) &&
                              !(FollowerCapabilityHelperRules.TryGetLearningV377ActuatorPhysicalFrontierDecision_learningV437MissedTakeoffFrontier_B3712084_2(learningV439LiveReservedTakeoffEnvelope,learningV448OpportunityStillLive)))));
                    if (learningV439LiveReservedTakeoffEnvelope &&
                        jumpForwardProjection > LearningV352JumpTakeoffCorridorMaxForwardOvershoot &&
                        ShouldEmitDiagnostic(
                            "learningV439JumpReservationProtectedTakeoffFrontier:" +
                                step.EdgeId.ToString(CultureInfo.InvariantCulture),
                            bot, 0.12f))
                    {
                        WriteEvent("learningV439JumpReservationProtectedTakeoffFrontier", bot, new
                        {
                            missionId = mission.MissionId,
                            stepIndex = mission.CurrentStepIndex,
                            edgeId = step.EdgeId,
                            jumpForwardProjection,
                            originalMaxForwardOvershoot = LearningV352JumpTakeoffCorridorMaxForwardOvershoot,
                            reservedAtProjection = step.LearningV429JumpAdmissionForwardProjection,
                            reservedMaxForwardProjection = learningV439ReservedMaxForward,
                            reservedLateralOffset = step.LearningV429JumpAdmissionLateralOffset,
                            movementGeneration = learningV439CurrentGeneration,
                            traversalSuccessRecorded = false,
                            persistentLearningChanged = false,
                            bodyRelocated = false,
                            action = "sameBodyEpochReservedJumpKeepsCenteringAuthorityUntilFiniteLeadEnvelopeExpires"
                        });
                    }
                    bool learningV437NoProgressFallbackReady =
                        Time.time - mission.LastProgressAt >= RouteMissionTraversalNoProgressReleaseSeconds;
                    if (((learningV437MissedTakeoffFrontier || learningV437NoProgressFallbackReady)) &&
                        TryBeginLearningV433EquivalentPhysicalActuatorFrontierRecovery(
                            bot, learningV437MissedTakeoffFrontier
                                ? "learningV437MissedJumpTakeoffFrontierSamePortalSubstitution"
                                : "learningV353JumpRollingFunnelUnavailableAfterRouteNoProgress",
                            out string learningV433TransferSource))
                    {
                        source = learningV433TransferSource;
                        learningV436EquivalentRecoverySelected = true;
                        if (learningV437MissedTakeoffFrontier)
                        {
                            WriteEvent("learningV437MissedJumpTakeoffFrontierYieldedToSamePortalActuator", bot, new
                            {
                                missionId = mission.MissionId,
                                stepIndex = mission.CurrentStepIndex,
                                edgeId = step.EdgeId,
                                kind = step.Kind,
                                jumpForwardProjection,
                                maxForwardOvershoot = LearningV352JumpTakeoffCorridorMaxForwardOvershoot,
                                transferSource = learningV433TransferSource,
                                traversalSuccessRecorded = false,
                                persistentLearningChanged = false,
                                bodyRelocated = false,
                                action = "monotonicMissedTakeoffPlaneCannotKeepDrivingIntoObstacleSamePortalNativeProvenActuatorOwnsRecovery"
                            });
                        }
                    }
                    else if (learningV437MissedTakeoffFrontier)
                    {
                        if ((TryBeginLearningV450MissedJumpCurrentBodySelfRescue(
                                bot, mission, step,
                                "learningV437MissedJumpTakeoffFrontierNoSamePortalCandidate",
                                out string learningV450CurrentBodyTransferSource)))
                        {
                            source = learningV450CurrentBodyTransferSource;
                            actuatorReady = false;
                            producerTargetAvailable = false;
                            return true;
                        }

                        {
                            RetireLearningV446PhysicalStrategy(
                                mission, step, "learningV450MissedJumpTakeoffFrontierNoRewind");
                            source = "learningV450MissedJumpTakeoffFrontierNoRewindHoldSemanticDebt";
                        }
                    }

                    // V4.36: TryGetLearningV353JumpRouteFunnelTarget can return false solely because
                    // the THROUGH-entry run-up target has no NavMesh sample. That is a backend
                    // limitation, not evidence that the physical Jump frontier should stop or that a
                    // probe should start from rest. Reuse the exact existing ProbeJumpRunupDistance
                    // geometry as a collision-active virtual target. Readiness still comes only from
                    // the existing V3.52/V4.29 centerline + momentum + reservation contract.
                    if ((!learningV436EquivalentRecoverySelected && !learningV437MissedTakeoffFrontier) &&
                        jumpDirection.sqrMagnitude > 0.5f)
                    {
                        Vector3 learningV436RawEntry = GetLearningV442JumpAdmissionEntry(step);
                        Vector3 learningV436VirtualTarget =
                            learningV436RawEntry + jumpDirection * ProbeJumpRunupDistance;
                        // The virtual target is the exact existing V3.53 THROUGH-entry target.
                        // Never turn it into a backward producer after the body has already crossed
                        // that bounded horizon. In that case retain actuator debt with no producer;
                        // the existing no-progress V433/V434 fallback remains the next authority.
                        bool learningV436VirtualTargetStillForward =
                            jumpForwardProjection < ProbeJumpRunupDistance &&
                            !IsLearningV449JumpTakeoffFrontierMissed(jumpForwardProjection);
                        if (learningV436VirtualTargetStillForward &&
                            IsFinite(learningV436RawEntry) && IsFinite(learningV436VirtualTarget))
                        {
                            jumpTarget = learningV436VirtualTarget;
                            target = learningV436VirtualTarget;
                            producerTargetAvailable = true;
                            if (!TryProjectLearningV375RouteArc(
                                    mission, learningV436VirtualTarget,
                                    out targetArc, out _, out _, out _))
                            {
                                targetArc = bodyArc;
                            }
                            source = "learningV436JumpPathlessVirtualStaging";
                            if (ShouldEmitDiagnostic(
                                    "learningV436JumpPathlessVirtualStaging:" +
                                        step.EdgeId.ToString(CultureInfo.InvariantCulture),
                                    bot, 0.18f))
                            {
                                WriteEvent("learningV436JumpPathlessVirtualStaging", bot, new
                                {
                                    missionId = mission.MissionId,
                                    stepIndex = mission.CurrentStepIndex,
                                    edgeId = step.EdgeId,
                                    kind = step.Kind,
                                    body = Vec(bot.Position),
                                    rawEntry = Vec(learningV436RawEntry),
                                    rawExit = Vec(IsFinite(step.RawExit) ? step.RawExit : step.Exit),
                                    target = Vec(learningV436VirtualTarget),
                                    jumpForwardProjection,
                                    jumpLateralOffset,
                                    jumpFacingDot,
                                    jumpMovementHeadingDot,
                                    jumpPlanarSpeed,
                                    existingRunupDistance = ProbeJumpRunupDistance,
                                    traversalSuccessRecorded = false,
                                    persistentLearningChanged = false,
                                    bodyRelocated = false,
                                    action = "navMeshSamplingFailureCannotRemoveJumpRunupProducerReservationStillOwnsNativeAdmission"
                                });
                            }
                        }
                    }
                }
                if (ShouldEmitDiagnostic(
                        "learningV386PersistentJumpRollingHandoff:" +
                            mission.MissionId.ToString(CultureInfo.InvariantCulture) + ":" +
                            step.EdgeId.ToString(CultureInfo.InvariantCulture),
                        bot, 0.18f))
                {
                    WriteEvent("learningV386PersistentJumpRollingHandoff", bot, new
                    {
                        missionId = mission.MissionId,
                        stepIndex = mission.CurrentStepIndex,
                        edgeId = step.EdgeId,
                        kind = step.Kind,
                        jumpForwardProjection,
                        jumpLateralOffset,
                        jumpFacingDot,
                        jumpMovementHeadingDot,
                        jumpPlanarSpeed,
                        actuatorReady,
                        producerTargetAvailable,
                        target = producerTargetAvailable ? (object)Vec(jumpTarget) : null,
                        traversalSuccessRecorded = false,
                        persistentLearningChanged = false,
                        action = actuatorReady
                            ? "sameV353RollingProofHandsPersistentRouteBodyDirectlyToNativeJump"
                            : "sameV353RollingProofKeepsPersistentRouteMovingThroughJumpRunup"
                    });
                }
                return true;
            }

            // V4.53: an immediate same-chain actuator successor is part of the same live body
            // transaction. Its existing V4.24 executor envelope therefore has higher authority than
            // historical V3.63 approach breadcrumbs. This prevents a successful predecessor from
            // handing the body backward to an old pre-entry lane before the successor can acquire.
            if (TryGetLearningV453SameChainSuccessorPhysicalProducer(
                    bot, mission, step,
                    out Vector3 learningV453SuccessorTarget,
                    out bool learningV453SuccessorReady,
                    out bool learningV453SuccessorProducerAvailable,
                    out string learningV453SuccessorSource))
            {
                actuatorReady = learningV453SuccessorReady;
                producerTargetAvailable = learningV453SuccessorProducerAvailable;
                source = learningV453SuccessorSource;
                if (producerTargetAvailable)
                {
                    target = learningV453SuccessorTarget;
                    Vector3 learningV453SuccessorDirection = GetFlatDirection(
                        (IsFinite(step.RawExit) ? step.RawExit : step.Exit) -
                        (IsFinite(step.RawEntry) ? step.RawEntry : step.Entry));
                    if (learningV453SuccessorDirection.sqrMagnitude > 0.5f)
                    {
                        tangent = learningV453SuccessorDirection;
                    }
                    if (!TryProjectLearningV375RouteArc(
                            mission, target, out targetArc, out _, out _, out _))
                    {
                        targetArc = bodyArc;
                    }
                }
                else
                {
                    target = bot.Position;
                    targetArc = bodyArc;
                }
                return true;
            }

            // Non-Jump actuators continue to use the V3.63 data-driven physical entry lane.

            if (TryGetLearningV363ActuatorRouteEntryLaneTarget(
                    bot, mission, step,
                    out Vector3 laneTarget,
                    out bool laneReady,
                    out int laneAnchorSequence,
                    out float forwardEntryProjection,
                    out float lateralEntryOffset,
                    out float maxLateral,
                    out float actuatorAlignmentAngle,
                    out bool actuatorHeadingReady,
                    out string laneSource))
            {
                actuatorReady = laneReady;
                if (laneReady)
                {
                    source = "physicalEntryLaneReady:" + (laneSource ?? string.Empty);
                    target = bot.Position;
                    targetArc = bodyArc;
                    Vector3 actuatorDirection = GetFlatDirection(
                        (IsFinite(step.RawExit) ? step.RawExit : step.Exit) -
                        (IsFinite(step.RawEntry) ? step.RawEntry : step.Entry));
                    if (actuatorDirection.sqrMagnitude > 0.5f)
                    {
                        tangent = actuatorDirection;
                    }
                    return true;
                }

                bool learningV425OrientationOnlyAdmission =
                    (!string.IsNullOrEmpty(laneSource)) &&
                    laneSource.IndexOf(
                        "learningV425OrientationOnlyAdmission",
                        StringComparison.Ordinal) >= 0;
                producerTargetAvailable = !learningV425OrientationOnlyAdmission && IsFinite(laneTarget);
                if (producerTargetAvailable)
                {
                    target = laneTarget;
                    Vector3 laneDirection = GetFlatDirection(laneTarget - bot.Position);
                    if (laneDirection.sqrMagnitude > 0.5f)
                    {
                        tangent = laneDirection;
                    }
                    if (TryProjectLearningV375RouteArc(
                            mission, laneTarget,
                            out float laneArc, out _, out _, out _))
                    {
                        targetArc = laneArc;
                    }
                }
                source = learningV425OrientationOnlyAdmission
                    ? "physicalEntryOrientationOnly:" + (laneSource ?? string.Empty)
                    : "physicalEntryLaneStaging:" + (laneSource ?? string.Empty);
                if (ShouldEmitDiagnostic(
                        "learningV377ActuatorPhysicalFrontier:" +
                            mission.MissionId.ToString(CultureInfo.InvariantCulture) + ":" +
                            step.EdgeId.ToString(CultureInfo.InvariantCulture),
                        bot, 0.18f))
                {
                    WriteEvent("learningV377ActuatorPhysicalFrontierOwned", bot, new
                    {
                        missionId = mission.MissionId,
                        stepIndex = mission.CurrentStepIndex,
                        edgeId = step.EdgeId,
                        kind = step.Kind,
                        laneAnchorSequence,
                        laneSource,
                        forwardEntryProjection,
                        lateralEntryOffset,
                        maxLateral,
                        actuatorAlignmentAngle,
                        actuatorHeadingReady,
                        actuatorReady,
                        producerTargetAvailable,
                        learningV425OrientationOnlyAdmission,
                        target = producerTargetAvailable ? (object)Vec(target) :
                            (learningV425OrientationOnlyAdmission ? (object)Vec(bot.Position) : null),
                        traversalSuccessRecorded = false,
                        persistentLearningChanged = false,
                        action = actuatorReady
                            ? "genericProducerYieldsAtPhysicalActuatorFrontier"
                            : (learningV425OrientationOnlyAdmission
                                ? "translationProducerYieldsWhileNativeSteeringRotatesAtImmutableBodyAnchor"
                                : "genericProducerStagesToPhysicalActuatorFrontierNeverDrivesThroughActionNode")
                    });
                }
                return true;
            }

            // V3.87: a predecessor may retire on the exact successor entry before a fresh player
            // approach lane can be reconstructed (observed Climb->DropDown at 0.246 m).  The existing
            // probe-arm geometry is already the bounded local execution contract.  If this mission
            // step is the exact same-chain successor and that contract is true, dispatch the actuator
            // now instead of holding execution debt until the RouteMission lifetime expires.
            if ((IsRouteMissionSameChainImmediateSuccessor(mission, mission.CurrentStepIndex)))
            {
                Vector3 exactEntry = IsFinite(step.Entry) ? step.Entry : step.RawEntry;
                Vector3 exactRawExit = IsFinite(step.RawExit) ? step.RawExit : step.Exit;
                float exactEntryDistance = IsFinite(exactEntry)
                    ? GetPlanarDistance(bot.Position, exactEntry)
                    : float.MaxValue;
                if (IsFinite(exactEntry) && IsFinite(exactRawExit) &&
                    ShouldArmLearnedTraversalProbe(
                        bot, step.Kind, exactEntryDistance, exactEntry, exactRawExit))
                {
                    actuatorReady = true;
                    producerTargetAvailable = false;
                    target = bot.Position;
                    targetArc = bodyArc;
                    Vector3 exactDirection = GetFlatDirection(exactRawExit -
                        (IsFinite(step.RawEntry) ? step.RawEntry : exactEntry));
                    if (exactDirection.sqrMagnitude > 0.5f)
                    {
                        tangent = exactDirection;
                    }
                    source = "learningV387ExactSameChainProbeArmGeometryReady";
                    WriteEvent("learningV387AtomicSameChainSuccessorReady", bot, new
                    {
                        missionId = mission.MissionId,
                        stepIndex = mission.CurrentStepIndex,
                        edgeId = step.EdgeId,
                        kind = step.Kind,
                        entryDistance = exactEntryDistance,
                        traversalSuccessRecorded = false,
                        persistentLearningChanged = false,
                        action = "exactSameChainCommitmentImmediatelyBecomesProbeStartAuthority"
                    });
                    return true;
                }
            }

            // If current-raid / learned lane geometry is unavailable, retain the old sequence-entry
            // cap only when it is still physically ahead. If the polyline says the entry is already
            // behind while the actuator step is NOT physically passed, do not let generic movement
            // continue through the node. Existing executor/recovery remains responsible for the
            // unresolved action debt.
            if (TryCapLearningV375ProducerBeforeActuatorFrontier(
                    mission, bodyArc, ref target, ref tangent, ref targetArc,
                    out _, out _))
            {
                producerTargetAvailable = true;
                source = "sequenceEntryFallback";
                return true;
            }

            source = "physicalFrontierUnavailableHoldActionDebt";
            return true;
        }

        private static bool TryGetLearningV377ActiveMoverForwardCompatibility(
            BotOwner bot,
            FollowerRouteMission mission,
            float physicalFrontierArc,
            Vector3 routeTangent,
            out float cornerArc,
            out float cornerForwardDot,
            out string reason)
        {
            cornerArc = physicalFrontierArc;
            cornerForwardDot = 1f;
            reason = "noExecutableCorner";
            if ((bot?.Mover == null) || mission == null)
            {
                return true;
            }

            try
            {
                var pathController = bot.Mover.ActualPathController;
                if (pathController?.HavePath != true)
                {
                    return true;
                }
                Vector3 corner = pathController.CurrentCornerPoint;
                if (!IsFinite(corner))
                {
                    return true;
                }

                Vector3 toCorner = corner - bot.Position;
                toCorner.y = 0f;
                if (toCorner.magnitude <= 0.05f)
                {
                    return true;
                }

                Vector3 direction = toCorner.normalized;
                bool pointsBackward = false;
                if (routeTangent.sqrMagnitude > 0.25f)
                {
                    cornerForwardDot = Vector3.Dot(direction, routeTangent.normalized);
                    pointsBackward = cornerForwardDot < 0f;
                }

                bool arcBehind = false;
                bool projected = TryProjectLearningV375RouteArc(
                    mission, corner, out cornerArc, out _, out _, out _);
                if (projected)
                {
                    arcBehind = cornerArc + 0.05f < physicalFrontierArc;
                }

                if ((pointsBackward && arcBehind) || (pointsBackward && !projected))
                {
                    reason = projected
                        ? "currentMoverCornerBackwardAndBehindPhysicalFrontier"
                        : "currentMoverCornerPointsBackward";
                    return false;
                }

                reason = "currentMoverCornerForwardCompatible";
                return true;
            }
            catch
            {
                reason = "currentMoverCornerInspectionUnavailable";
                return true;
            }
        }

        private static bool IsLearningV377NavMeshProducerForwardCompatible(
            FollowerRouteMission mission,
            Vector3 position,
            Vector3 target,
            StrictPathResult path,
            float physicalFrontierArc,
            Vector3 routeTangent,
            out float allowedPathLength,
            out float firstMeaningfulCornerArc,
            out float firstMeaningfulCornerForwardDot,
            out string reason)
        {
            float direct = Vector3.Distance(position, target);
            allowedPathLength = GetAllowedFollowPathLength(Mathf.Max(0.25f, direct), true);
            firstMeaningfulCornerArc = physicalFrontierArc;
            firstMeaningfulCornerForwardDot = 1f;
            reason = "notStrictComplete";
            if ((mission == null) || path == null ||
                !path.StrictComplete)
            {
                return false;
            }
            if (path.PathLength > allowedPathLength)
            {
                reason = "pathInflatedBeyondRouteLocalEnvelope";
                return false;
            }
            if (path.Corners == null || path.Corners.Length == 0)
            {
                reason = "missingExecutableCorners";
                return false;
            }

            Vector3 first = Vector3.zero;
            bool found = false;
            foreach (Vector3 corner in path.Corners)
            {
                if (!IsFinite(corner) || GetPlanarDistance(position, corner) <= 0.05f)
                {
                    continue;
                }
                first = corner;
                found = true;
                break;
            }
            if (!found)
            {
                reason = "noMeaningfulForwardCorner";
                return false;
            }

            Vector3 firstDirection = GetFlatDirection(first - position);
            bool firstPointsBackward = false;
            if (routeTangent.sqrMagnitude > 0.25f && firstDirection.sqrMagnitude > 0.25f)
            {
                firstMeaningfulCornerForwardDot = Vector3.Dot(
                    firstDirection, routeTangent.normalized);
                firstPointsBackward = firstMeaningfulCornerForwardDot < 0f;
            }

            bool firstArcBehind = false;
            bool firstProjected = TryProjectLearningV375RouteArc(
                mission, first,
                out firstMeaningfulCornerArc, out _, out _, out _);
            if (firstProjected)
            {
                firstArcBehind = firstMeaningfulCornerArc + 0.05f < physicalFrontierArc;
            }

            if ((firstPointsBackward && firstArcBehind) ||
                (firstPointsBackward && !firstProjected))
            {
                reason = firstProjected
                    ? "firstExecutableCornerBackwardAndBehindPhysicalFrontier"
                    : "firstExecutableCornerPointsBackward";
                return false;
            }

            reason = "routeLocalForwardNavMesh";
            return true;
        }

        private static bool TryConsumeLearningV429AtomicJumpAdmission(
            BotOwner bot,
            FollowerRouteMission mission,
            FollowerRouteMissionStep step,
            LearnedTraversalCommitment commitment,
            float forwardEntryProjection,
            float lateralEntryOffset,
            float facingDot,
            float movementHeadingDot,
            float planarSpeed,
            out string result)
        {
            result = "disabled";
            if ((bot == null) || mission == null ||
                step == null || commitment == null || !step.LearningV429JumpAdmissionReserved ||
                !string.Equals(step.Kind, "Jump", StringComparison.OrdinalIgnoreCase) ||
                commitment.EdgeId != step.EdgeId || commitment.RouteMissionId != mission.MissionId ||
                commitment.RouteMissionStepIndex != mission.CurrentStepIndex ||
                !TryGetActiveProbe(bot, out ProbeRuntime probe) || probe == null ||
                probe.LearnedEdgeId != step.EdgeId ||
                !string.Equals(probe.RequestedTraversalKind, "Jump", StringComparison.OrdinalIgnoreCase) ||
                !probe.NativeStrategySelected || probe.AssistedStrategySelected ||
                probe.JumpAttempted || probe.NativeExecutorConsumed ||
                probe.Player?.MovementContext == null)
            {
                return false;
            }

            long currentGeneration = FollowerMovementWriterAuthority.GetCurrentGeneration(
                bot, out FollowerMovementWriterAuthority.WriterDomain currentOwner);
            EPlayerState state = probe.Player.CurrentStateName;
            bool actuatorStateReady =
                (FollowerCapabilityHelperRules.TryConsumeLearningV429AtomicJumpAdmission_actuatorStateReady_04291716_1((int)(state),(int)(EPlayerState.Run),(int)(state),(int)(EPlayerState.Sprint))) &&
                probe.Player.MovementContext.IsGrounded && probe.Player.MovementContext.CanJump;
            bool learningV429ModeReady = true;
            ;
            bool learningV429MomentumReady = IsLearningV329JumpTakeoffMomentumReady(
                probe, out float learningV429MeasuredPlanarSpeed, out float learningV429RequiredSpeed);
            Vector2 learningV429Input = probe.Player.InputDirection;
            Vector2 learningV429Movement = probe.Player.MovementContext.MovementDirection;
            bool learningV429ForwardProducer =
                FollowerCapabilityHelperRules.TryConsumeLearningV429AtomicJumpAdmission_learningV429ForwardProducer_04291716_2(learningV429Input.y,learningV429Movement.y) || probe.Bot?.Mover?.IsMoving == true;
            float learningV429AlignmentAngle = Mathf.Acos(
                Mathf.Clamp(facingDot, -1f, 1f)) * Mathf.Rad2Deg;
            float maxReservedForwardProjection =
                step.LearningV429JumpAdmissionForwardProjection +
                step.LearningV429JumpAdmissionLeadDistance;
            bool exactReservationGeneration =
                currentOwner == FollowerMovementWriterAuthority.WriterDomain.Traversal &&
                currentGeneration > 0 && currentGeneration == step.LearningV429JumpAdmissionMovementGeneration &&
                probe.MovementWriterGeneration == currentGeneration;
            float learningV441ReservationAge = step.LearningV429JumpAdmissionAt > 0f
                ? Mathf.Max(0f, Time.time - step.LearningV429JumpAdmissionAt)
                : float.MaxValue;
            float learningV441ReservationBodyDrift = IsFinite(step.LearningV429JumpAdmissionPosition)
                ? GetPlanarDistance(probe.Player.Position, step.LearningV429JumpAdmissionPosition)
                : float.PositiveInfinity;
            bool learningV441CanonicalGenerationRollover =
                (!exactReservationGeneration) &&
                currentOwner == FollowerMovementWriterAuthority.WriterDomain.Traversal &&
                currentGeneration > 0 && probe.MovementWriterGeneration == currentGeneration &&
                step.LearningV429JumpAdmissionMovementGeneration > 0 &&
                FollowerCapabilityHelperRules.TryConsumeLearningV429AtomicJumpAdmission_learningV441CanonicalGenerationRollover_04291716_3(currentGeneration,step.LearningV429JumpAdmissionMovementGeneration) &&
                learningV441ReservationAge <= Mathf.Max(0.35f, step.LearningV429JumpAdmissionLeadSeconds + 0.25f) &&
                FollowerCapabilityHelperRules.TryConsumeLearningV429AtomicJumpAdmission_learningV441CanonicalGenerationRollover_04291716_4(learningV441ReservationBodyDrift,step.LearningV429JumpAdmissionLeadDistance);
            bool samePhysicalEpoch = exactReservationGeneration || learningV441CanonicalGenerationRollover;
            if (learningV441CanonicalGenerationRollover &&
                ShouldEmitDiagnostic(
                    "learningV441JumpReservationGenerationRollover:" +
                    mission.MissionId.ToString(CultureInfo.InvariantCulture) + ":" +
                    step.EdgeId.ToString(CultureInfo.InvariantCulture), bot, 0.10f))
            {
                WriteEvent("learningV441JumpReservationGenerationRollover", bot, new
                {
                    missionId = mission.MissionId,
                    stepIndex = mission.CurrentStepIndex,
                    edgeId = step.EdgeId,
                    reservedGeneration = step.LearningV429JumpAdmissionMovementGeneration,
                    currentGeneration,
                    probeGeneration = probe.MovementWriterGeneration,
                    reservationAge = learningV441ReservationAge,
                    reservationBodyDrift = learningV441ReservationBodyDrift,
                    reservationLeadDistance = step.LearningV429JumpAdmissionLeadDistance,
                    forwardEntryProjection,
                    lateralEntryOffset,
                    facingDot,
                    movementHeadingDot,
                    bodyRelocated = false,
                    traversalSuccessRecorded = false,
                    persistentLearningChanged = false,
                    action = "canonicalSuccessorWriterTokenRolledButPhysicalBodyReservationRemainsSameContinuousEpoch"
                });
            }
            // V474: Sprint state is actuator-legal, but the state enum alone cannot prove
            // that a slow, just-restarted body can clear a previously native-proven barrier.
            // Only use the same edge's successful follower-native Sprint observations;
            // never turn the player's absolute sprint velocity into a Bot capability threshold.
            float learningV474NativeSprintFloor = 0f;
            int learningV474NativeSprintSamples = 0;
            if ((state == EPlayerState.Sprint))
            {
                LearnedTraversalEdge learningV474Edge = LearnedTraversalEdges.FirstOrDefault(candidate =>
                    candidate != null && candidate.Id == step.EdgeId);
                if (learningV474Edge != null &&
                    !string.IsNullOrWhiteSpace(learningV474Edge.ExperienceKey) &&
                    LearningV322SkillEvidence.TryGetValue(learningV474Edge.ExperienceKey,
                        out LearningV322PersistentSkillEvidenceRecord learningV474Skill) &&
                    learningV474Skill != null)
                {
                    learningV474NativeSprintSamples = Mathf.Max(0,
                        learningV474Skill.NativeJumpSprintTakeoffSuccesses);
                    learningV474NativeSprintFloor = Mathf.Max(0f,
                        learningV474Skill.NativeJumpSprintMinSuccessfulTakeoffSpeed);
                }
            }
            bool learningV474NativeSprintFloorReady =
                FollowerCapabilityHelperRules.TryConsumeLearningV429AtomicJumpAdmission_learningV474NativeSprintFloorReady_04291716_5(learningV474NativeSprintSamples,learningV474NativeSprintFloor,learningV429MeasuredPlanarSpeed,learningV474NativeSprintFloor);
            bool reservationStillPhysical =
                FollowerCapabilityHelperRules.TryConsumeLearningV429AtomicJumpAdmission_reservationStillPhysical_04291716_6(samePhysicalEpoch,actuatorStateReady,learningV429ModeReady,learningV474NativeSprintFloorReady,lateralEntryOffset,LearningV352JumpTakeoffCorridorMaxLateral,facingDot,LearningV352JumpTakeoffCorridorMinFacingDot,movementHeadingDot,LearningV352JumpTakeoffCorridorMinMovementHeadingDot) &&
                learningV429AlignmentAngle <=
                    Mathf.Acos(LearningV352JumpTakeoffCorridorMinFacingDot) * Mathf.Rad2Deg &&
                learningV429MomentumReady && learningV429ForwardProducer &&
                learningV429MeasuredPlanarSpeed >= RollingTraversalMinSpeed &&
                forwardEntryProjection >= step.LearningV429JumpAdmissionForwardProjection &&
                forwardEntryProjection <= maxReservedForwardProjection;
            float learningV449PhysicalTriggerLeadDistance = (GetLearningV449JumpPhysicalTriggerLeadDistance(learningV429MeasuredPlanarSpeed));
            bool learningV449ActuallyInsidePhysicalTakeoffHorizon =
                IsLearningV449JumpInsidePhysicalTriggerHorizon(
                    forwardEntryProjection, learningV429MeasuredPlanarSpeed, bot);
            bool cf60LegalOneFrameForecast =
                IsCf60LegalOneFrameJumpTriggerForecast(
                    bot, forwardEntryProjection, learningV429MeasuredPlanarSpeed,
                    out float cf60OneFrameDistance, out float cf60PhysicalTriggerLead);
            bool learningV449InsidePhysicalTakeoffHorizon =
                FollowerCapabilityHelperRules.TryConsumeLearningV429AtomicJumpAdmission_learningV449InsidePhysicalTakeoffHorizon_04291716_7(learningV449ActuallyInsidePhysicalTakeoffHorizon,cf60LegalOneFrameForecast,reservationStillPhysical,forwardEntryProjection,maxReservedForwardProjection);
            if (!reservationStillPhysical)
            {
                result = "reservationNoLongerConsumable";
                // CF59: V457 may preserve legitimate Transition momentum while the V429
                // *native* consumer correctly requires a grounded Run/Sprint state.
                // Report this missing legal state independently of speed, direction or
                // stale writer generations. Never call TryJump from Transition.
                if (state == EPlayerState.Transition && !actuatorStateReady &&
                    samePhysicalEpoch && learningV429ModeReady &&
                    probe.Player.MovementContext.IsGrounded &&
                    probe.Player.MovementContext.CanJump &&
                    lateralEntryOffset <= LearningV352JumpTakeoffCorridorMaxLateral &&
                    facingDot >= LearningV352JumpTakeoffCorridorMinFacingDot &&
                    movementHeadingDot >= LearningV352JumpTakeoffCorridorMinMovementHeadingDot &&
                    learningV429ForwardProducer &&
                    forwardEntryProjection <= LearningV352JumpTakeoffCorridorMaxForwardOvershoot &&
                    ShouldEmitDiagnostic("cf59TransitionWindow:" +
                        mission.MissionId.ToString(CultureInfo.InvariantCulture) + ":" +
                        step.EdgeId.ToString(CultureInfo.InvariantCulture), bot, 0.14f))
                {
                    WriteEvent("cf59JumpTransitionTakeoffWindowBlocked", bot, new
                    {
                        missionId = mission.MissionId,
                        stepIndex = mission.CurrentStepIndex,
                        episodeId = step.EpisodeId,
                        edgeId = step.EdgeId,
                        state = state.ToString(),
                        currentGeneration,
                        reservedGeneration = step.LearningV429JumpAdmissionMovementGeneration,
                        forwardEntryProjection,
                        physicalTakeoffHorizonMeters = GetLearningV449JumpPhysicalTriggerLeadDistance(
                            learningV429MeasuredPlanarSpeed),
                        lateralEntryOffset,
                        facingDot,
                        movementHeadingDot,
                        measuredPlanarSpeed = learningV429MeasuredPlanarSpeed,
                        requiredSpeed = learningV429RequiredSpeed,
                        momentumReady = learningV429MomentumReady,
                        actualNativeTryJumpCalled = false,
                        traversalSuccessRecorded = false,
                        nativeSuccessRecorded = false,
                        bodyRelocated = false,
                        action = "transitionCarriesPhysicalMomentumButNativeRunSprintConsumerNotLegallyAdmitted"
                    });
                }
                WriteEvent("learningV429AtomicJumpAdmissionDeferred", bot, new
                {
                    missionId = mission.MissionId,
                    stepIndex = mission.CurrentStepIndex,
                    edgeId = step.EdgeId,
                    state = state.ToString(),
                    actuatorStateReady,
                    modeReady = learningV429ModeReady,
                    momentumReady = learningV429MomentumReady,
                    forwardProducer = learningV429ForwardProducer,
                    samePhysicalEpoch,
                    exactReservationGeneration,
                    learningV441CanonicalGenerationRollover,
                    reservationAge = learningV441ReservationAge,
                    reservationBodyDrift = learningV441ReservationBodyDrift,
                    currentGeneration,
                    reservedGeneration = step.LearningV429JumpAdmissionMovementGeneration,
                    forwardEntryProjection,
                    reservedForwardEntryProjection = step.LearningV429JumpAdmissionForwardProjection,
                    maxReservedForwardProjection,
                    lateralEntryOffset,
                    facingDot,
                    alignmentAngle = learningV429AlignmentAngle,
                    movementHeadingDot,
                    frontierPlanarSpeed = planarSpeed,
                    measuredPlanarSpeed = learningV429MeasuredPlanarSpeed,
                    requiredSpeed = learningV429RequiredSpeed,
                    learningV474NativeSprintFloor,
                    learningV474NativeSprintSamples,
                    learningV474NativeSprintFloorReady,
                    traversalSuccessRecorded = false,
                    persistentLearningChanged = false,
                    action = "doNotConsumeReservationUnlessExactNativeConsumerCanTriggerInsideSamePhysicalEpoch"
                });
                return false;
            }

            if (!learningV449InsidePhysicalTakeoffHorizon)
            {
                result = "reservationRetainedAwaitPhysicalTakeoffHorizon";
                if (ShouldEmitDiagnostic(
                        "learningV449JumpReservationAwaitingPhysicalTakeoffHorizon:" +
                        mission.MissionId.ToString(CultureInfo.InvariantCulture) + ":" +
                        step.EdgeId.ToString(CultureInfo.InvariantCulture), bot, 0.10f))
                {
                    WriteEvent("learningV449JumpReservationAwaitingPhysicalTakeoffHorizon", bot, new
                    {
                        missionId = mission.MissionId,
                        stepIndex = mission.CurrentStepIndex,
                        edgeId = step.EdgeId,
                        forwardEntryProjection,
                        reservedForwardEntryProjection = step.LearningV429JumpAdmissionForwardProjection,
                        reservationLeadDistance = step.LearningV429JumpAdmissionLeadDistance,
                        physicalTriggerLeadDistance = learningV449PhysicalTriggerLeadDistance,
                        maxForwardOvershoot = LearningV352JumpTakeoffCorridorMaxForwardOvershoot,
                        measuredPlanarSpeed = learningV429MeasuredPlanarSpeed,
                        learningV474NativeSprintFloor,
                        learningV474NativeSprintSamples,
                        v474StrictTriggerHorizon = true,
                        traversalSuccessRecorded = false,
                        persistentLearningChanged = false,
                        bodyRelocated = false,
                        action = "reservationPreservesOwnershipButTryJumpWaitsForExistingPhysicalTakeoffHorizon"
                    });
                }
                return false;
            }

            Vector3 rawEntry = IsFinite(commitment.RawEntry) ? commitment.RawEntry : commitment.Entry;
            Vector3 rawExit = IsFinite(commitment.RawExit) ? commitment.RawExit : commitment.Exit;
            Vector3 direction = GetFlatDirection(rawExit - rawEntry);
            if (!IsFinite(rawEntry) || !IsFinite(rawExit) || direction.sqrMagnitude < 0.5f)
            {
                result = "invalidCommittedGeometry";
                return false;
            }

            probe.NativeExecutorConsumed = true;
            probe.NativeStrategySelected = true;
            probe.AssistedStrategySelected = false;
            probe.RequestedDirection = direction;
            probe.LearningV317NativeJumpSemanticTrajectoryAuthority = true;
            probe.LearningV354JumpActuatorLeadTriggered = true;
            probe.LearningV354JumpActuatorLeadSecondsAtTrigger =
                step.LearningV429JumpAdmissionLeadSeconds;
            probe.LearningV354JumpActuatorLeadDistanceAtTrigger =
                step.LearningV429JumpAdmissionLeadDistance;
            // The exact reservation has passed current geometry, state and writer checks.
            // Publish the same entry witness as the shared acquisition path BEFORE the epoch.
            MarkLearningV32NativeEntryAcquired(probe, rawEntry, rawExit,
                Vector3.Distance(probe.Player.Position, rawEntry), learningV429AlignmentAngle,
                lateralEntryOffset, forwardEntryProjection, "atomicRollingAdmission", directHandoff: true);
            BeginLearningV32NativeExecutionEpoch(
                probe, "Jump", "learningV429AtomicRollingAdmission");
            WriteEvent("learningV429AtomicJumpAdmissionConsumed", bot, new
            {
                missionId = mission.MissionId,
                stepIndex = mission.CurrentStepIndex,
                edgeId = step.EdgeId,
                position = Vec(probe.Player.Position),
                rawEntry = Vec(rawEntry),
                rawExit = Vec(rawExit),
                state = state.ToString(),
                movementGeneration = currentGeneration,
                reservationPosition = Vec(step.LearningV429JumpAdmissionPosition),
                reservedForwardEntryProjection = step.LearningV429JumpAdmissionForwardProjection,
                currentForwardEntryProjection = forwardEntryProjection,
                maxReservedForwardProjection,
                lateralEntryOffset,
                facingDot,
                alignmentAngle = learningV429AlignmentAngle,
                movementHeadingDot,
                frontierPlanarSpeed = planarSpeed,
                measuredPlanarSpeed = learningV429MeasuredPlanarSpeed,
                requiredSpeed = learningV429RequiredSpeed,
                momentumReady = learningV429MomentumReady,
                forwardProducer = learningV429ForwardProducer,
                reservationLeadSeconds = step.LearningV429JumpAdmissionLeadSeconds,
                reservationLeadDistance = step.LearningV429JumpAdmissionLeadDistance,
                physicalTriggerLeadDistance = learningV449PhysicalTriggerLeadDistance,
                cf60LegalOneFrameForecast,
                cf60OneFrameDistance,
                cf60PhysicalTriggerLead,
                reservationLeadRetainedForOwnershipOnly = true,
                traversalSuccessRecorded = false,
                persistentLearningChanged = false,
                action = "consumeExactReservedPhysicalFrontierAndTriggerNativeJumpInSameDispatchFrame"
            });
            TriggerNativeJump(probe, atomicAdmission: true);
            // Reservation is a one-shot execution proof. Once consumed by the exact native
            // consumer, never let a later retry inherit stale admission from this body epoch.
            step.LearningV429JumpAdmissionReserved = false;
            result = probe.JumpAttempted ? "triggered" : "triggerNotObserved";
            return probe.JumpAttempted;
        }

        private static bool TryDispatchLearningV378PersistentActuatorFrontierProbe(
            BotOwner bot,
            FollowerRouteMission mission,
            int expectedEdgeId,
            string expectedKind,
            out string result)
        {
            result = "disabled";
            if ((bot == null) || mission == null ||
                mission.CurrentStepIndex < 0 || mission.CurrentStepIndex >= mission.Steps.Count ||
                expectedEdgeId <= 0 || HasActiveTraversalProbe(bot))
            {
                return false;
            }

            if (!EnsureFollowerRouteMissionCommitment(bot, mission) ||
                mission.CurrentStepIndex < 0 || mission.CurrentStepIndex >= mission.Steps.Count)
            {
                result = "commitmentUnavailable";
                return false;
            }

            FollowerRouteMissionStep step = mission.Steps[mission.CurrentStepIndex];
            if (step == null)
            {
                result = "routeCursorChanged";
                return false;
            }

            string profileId = bot.ProfileId ?? string.Empty;
            if (string.IsNullOrEmpty(profileId) ||
                !LearnedTraversalCommitments.TryGetValue(profileId, out LearnedTraversalCommitment commitment) ||
                commitment == null ||
                commitment.RouteMissionId != mission.MissionId ||
                commitment.RouteMissionStepIndex != mission.CurrentStepIndex)
            {
                result = "exactCommitmentMissing";
                return false;
            }

            bool learningV434EquivalentSubstitute =
                (IsLearningV433ExactLocalActuatorSubstitution(commitment));
            if (learningV434EquivalentSubstitute)
            {
                if (commitment.EdgeId != expectedEdgeId ||
                    !string.Equals(commitment.Kind, expectedKind, StringComparison.OrdinalIgnoreCase))
                {
                    result = "substituteIdentityChanged";
                    return false;
                }
            }
            else if (step.EdgeId != expectedEdgeId ||
                     !string.Equals(step.Kind, expectedKind, StringComparison.OrdinalIgnoreCase) ||
                     commitment.EdgeId != step.EdgeId)
            {
                result = "routeCursorChanged";
                return false;
            }

            int dispatchEdgeId = commitment.EdgeId;
            string dispatchKind = commitment.Kind ?? string.Empty;

            if (Time.time < commitment.NextApproachPathAttemptAt)
            {
                result = "approachRetryCooldown";
                return false;
            }

            float entryDistance = GetPlanarDistance(bot.Position, commitment.Entry);
            bool learningV386JumpRollingReady = false;
            float learningV429JumpForwardProjection = 0f;
            float learningV429JumpLateralOffset = float.MaxValue;
            float learningV429JumpFacingDot = -1f;
            float learningV429JumpMovementHeadingDot = -1f;
            float learningV429JumpPlanarSpeed = 0f;
            if ((!learningV434EquivalentSubstitute) &&
                string.Equals(dispatchKind, "Jump", StringComparison.OrdinalIgnoreCase))
            {
                learningV386JumpRollingReady = TryGetLearningV353JumpRouteFunnelTarget(
                    bot, step,
                    out _, out bool learningV386JumpReady,
                    out learningV429JumpForwardProjection,
                    out learningV429JumpLateralOffset,
                    out learningV429JumpFacingDot,
                    out learningV429JumpMovementHeadingDot,
                    out learningV429JumpPlanarSpeed) && learningV386JumpReady;
            }
            if (string.Equals(dispatchKind, "Climb", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryGetLearningV424ActuatorExecutorAdmission(
                        bot, step, out bool cf13ExecutorReady, out _, out _, out _,
                        out _, out _, out _, out _, out _, out _, out _) || !cf13ExecutorReady)
                {
                    result = "cf13ClimbNativeEntryStateNotReady";
                    return false;
                }
            }
            if (!learningV386JumpRollingReady &&
                !ShouldArmLearnedTraversalProbe(
                    bot, commitment.Kind, entryDistance, commitment.Entry, commitment.RawExit))
            {
                result = "probeArmGeometryNotReady";
                return false;
            }

            bool zeroShotCommitment = IsLearningV319TransientSkillTransfer(commitment);
            string retryKey = profileId + ":" + dispatchEdgeId.ToString(CultureInfo.InvariantCulture);
            if (!zeroShotCommitment &&
                LearnedTraversalRetryAfter.TryGetValue(retryKey, out float retryAt) && Time.time < retryAt)
            {
                result = "traversalRetryCooldown";
                return false;
            }

            Vector3 rawEntry = IsFinite(commitment.RawEntry) ? commitment.RawEntry : commitment.Entry;
            Vector3 rawExit = IsFinite(commitment.RawExit) ? commitment.RawExit : commitment.Exit;
            Vector3 direction = GetFlatDirection(rawExit - rawEntry);
            bool started = TryStartProbeForBot(
                bot, direction, learningV434EquivalentSubstitute
                    ? "learningV434EquivalentActuatorSubstitution"
                    : "learningV378PersistentActuatorFrontier",
                dispatchEdgeId, dispatchKind,
                "learningV378PersistentActuatorFrontierDispatch", out string startMessage);
            result = started ? "started" : (startMessage ?? "startRejected");
            if (!started)
            {
                if (ShouldEmitDiagnostic(
                        "learningV378PersistentActuatorDispatchRejected:" +
                            mission.MissionId.ToString(CultureInfo.InvariantCulture) + ":" +
                            dispatchEdgeId.ToString(CultureInfo.InvariantCulture),
                        bot, 0.35f))
                {
                    WriteEvent("learningV378PersistentActuatorFrontierDispatchRejected", bot, new
                    {
                        missionId = mission.MissionId,
                        stepIndex = mission.CurrentStepIndex,
                        edgeId = dispatchEdgeId,
                        kind = dispatchKind,
                        entryDistance,
                        reason = result,
                        traversalSuccessRecorded = false,
                        persistentLearningChanged = false,
                        action = "retainExactActionDebtAndRetryWithoutReturningMovementToGenericFollow"
                    });
                }
                return false;
            }

            if (!zeroShotCommitment)
            {
                LearnedTraversalRetryAfter[retryKey] = Time.time + LearnedTraversalRetryCooldown;
            }
            WriteEvent(learningV434EquivalentSubstitute
                    ? "learningV434EquivalentActuatorSubstitutionDispatched"
                    : "learningV378PersistentActuatorFrontierDispatched", bot, new
            {
                missionId = mission.MissionId,
                stepIndex = mission.CurrentStepIndex,
                edgeId = dispatchEdgeId,
                kind = dispatchKind,
                entryDistance,
                message = startMessage,
                traversalSuccessRecorded = false,
                persistentLearningChanged = false,
                action = "physicalFrontierReadinessDirectlyStartsExactCurrentRouteMissionActuator"
            });

            if (!learningV434EquivalentSubstitute &&
                learningV386JumpRollingReady &&
                string.Equals(dispatchKind, "Jump", StringComparison.OrdinalIgnoreCase))
            {
                TryConsumeLearningV429AtomicJumpAdmission(
                    bot, mission, step, commitment,
                    learningV429JumpForwardProjection,
                    learningV429JumpLateralOffset,
                    learningV429JumpFacingDot,
                    learningV429JumpMovementHeadingDot,
                    learningV429JumpPlanarSpeed,
                    out _);
            }
            return true;
        }

        private static void FinalizeLearningV397SupersededActuatorVerifier(
            ProbeRuntime probe,
            FollowerRouteMission mission,
            FollowerRouteMissionStep retiredStep,
            FollowerRouteMissionStep successorStep,
            float bodyArc,
            float successorStartArc,
            int projectedSequence)
        {
            if (probe == null || !IsActiveProbe(probe))
            {
                return;
            }

            RestoreProbeInput(probe);
            RestoreAssistedObstacleCollisionLease(
                probe, "learningV397ExhaustedActuatorDebtRetiredByCanonicalSuccessorBodyEpoch");
            RestoreAssistedTraversalAnimation(probe);
            if (probe.Bot?.Mover != null)
            {
                probe.Bot.Mover.SetBotMoverPause("R1-D8403EB4D735",@probe?.Bot,false);
            }

            ClearLearnedTraversalCommitment(
                probe.Bot,
                probe.LearnedEdgeId,
                "learningV397ExhaustedActuatorDebtRetiredByCanonicalSuccessorBodyEpoch");

            WriteEvent("learningV397SupersededActuatorVerifierReleased", probe.Bot, new
            {
                missionId = mission?.MissionId ?? 0,
                retiredStepIndex = mission != null ? Math.Max(0, mission.CurrentStepIndex - 1) : -1,
                retiredEpisodeId = retiredStep?.EpisodeId ?? 0,
                retiredEdgeId = retiredStep?.EdgeId ?? 0,
                retiredKind = retiredStep?.Kind ?? string.Empty,
                successorEpisodeId = successorStep?.EpisodeId ?? 0,
                successorEdgeId = successorStep?.EdgeId ?? 0,
                successorKind = successorStep?.Kind ?? string.Empty,
                verifierStage = probe.Stage.ToString(),
                bodyArc,
                successorStartArc,
                projectedSequence,
                traversalSuccessRecorded = false,
                persistentLearningChanged = false,
                semanticOutcomePromoted = false,
                recoveryTeleportUsed = false,
                bodyRelocated = false,
                action = "releaseOldVerifierKeepFailureEvidenceLetCanonicalSuccessorOwnForwardExecution"
            });

            // UnregisterActiveProbe sees mission.CurrentStepIndex already on the canonical successor,
            // so V3.75 preserves the SAME persistent Traversal writer/generation instead of dropping
            // into PostTraversal/Follow. The implicit V3.28 attempt terminalization remains a failure
            // record; V3.97 changes execution debt only, never semantic/native success.
            DetachProbeStateHandler(probe);
            UnregisterActiveProbe(probe);
        }

        private static bool TryRetireLearningV397ExhaustedActuatorDebtByCanonicalSuccessorBodyEpoch(
            BotOwner bot,
            FollowerRouteMission mission,
            FollowerTraversalRouteCursor cursor)
        {
            if ((bot == null) || mission == null || cursor == null ||
                !cursor.LearningV375TransactionActive ||
                cursor.LearningV375RouteMissionId != mission.MissionId ||
                mission.CurrentStepIndex < 0 || mission.CurrentStepIndex >= mission.Steps.Count - 1 ||
                mission.LearningV357ExhaustedBarrierStepIndex != mission.CurrentStepIndex)
            {
                return false;
            }

            FollowerRouteMissionStep step = mission.Steps[mission.CurrentStepIndex];
            var r2DebtObservation19 = CaptureRouteDebtObservation(bot, mission, step, false);

            FollowerRouteMissionStep successor = mission.Steps[mission.CurrentStepIndex + 1];
            if (step == null || successor == null ||
                step.EdgeId <= 0 ||
                mission.LearningV357ExhaustedBarrierEdgeId != step.EdgeId ||
                !IsLearningV353StrictActuatorBarrierKind(step.Kind))
            {
                return false;
            }

            LearningV354TraversalCapabilityProfile profile = GetLearningV354ActionProfile(step.Kind);
            if (profile == null || !profile.RequiresActuator)
            {
                return false;
            }

            if (!TryGetActiveProbe(bot, out ProbeRuntime probe) || probe == null ||
                !IsActiveProbe(probe) ||
                (probe.TraversalEpisodeId > 0 && step.EpisodeId > 0 &&
                 probe.TraversalEpisodeId != step.EpisodeId) ||
                (probe.LearnedEdgeId > 0 && probe.LearnedEdgeId != step.EdgeId))
            {
                return false;
            }

            bool bodyReturnedToPersistentRoute =
                probe.Stage == ProbeStage.Egress ||
                probe.Stage == ProbeStage.Verify ||
                probe.Stage == ProbeStage.ExitCommit;
            if (!bodyReturnedToPersistentRoute || cursor.LearningV375ActuatorBorrowed ||
                probe.TraversalTeleportAttempted || probe.TeleportFallbackUsed)
            {
                return false;
            }

            bool sameCanonicalChain =
                step.ChainId <= 0 || successor.ChainId <= 0 || step.ChainId == successor.ChainId;
            bool chronologyAdjacent =
                step.EndBreadcrumbSequence > 0 &&
                successor.StartBreadcrumbSequence > 0 &&
                FollowerCapabilityHelperRules.TryRetireLearningV397ExhaustedActuatorDebtByCanonicalSuccessorBodyEpoch_chronologyAdjacent_AFFB9473_1(successor.StartBreadcrumbSequence,step.EndBreadcrumbSequence,TraversalChainImmediateMaxBreadcrumbGap);
            if (!sameCanonicalChain || !chronologyAdjacent)
            {
                return false;
            }

            if (!TryProjectLearningV375RouteArc(
                    mission, bot.Position,
                    out float bodyArc, out _, out Vector3 projectedPoint, out int projectedSequence) ||
                !TryGetLearningV375SequenceArc(
                    mission, step.EndBreadcrumbSequence,
                    out float actuatorEndArc, out _) ||
                !TryGetLearningV375SequenceArc(
                    mission,
                    successor.StartBreadcrumbSequence > 0
                        ? successor.StartBreadcrumbSequence
                        : successor.EndBreadcrumbSequence,
                    out float successorStartArc, out _))
            {
                return false;
            }

            float routeLocalDistance = GetPlanarDistance(bot.Position, projectedPoint);
            float successorEntryDistance = IsFinite(successor.Entry)
                ? GetPlanarDistance(bot.Position, successor.Entry)
                : float.MaxValue;
            bool localizedToCanonicalRoute =
                routeLocalDistance <= RouteMissionBreadcrumbReachDistance;
            bool bodyBeyondActuatorFrontier =
                FollowerCapabilityHelperRules.TryRetireLearningV397ExhaustedActuatorDebtByCanonicalSuccessorBodyEpoch_bodyBeyondActuatorFrontier_AFFB9473_2(bodyArc,LearningV354RouteDirectionalProgressEpsilon,actuatorEndArc);
            bool bodyReachedCanonicalSuccessor =
                FollowerCapabilityHelperRules.TryRetireLearningV397ExhaustedActuatorDebtByCanonicalSuccessorBodyEpoch_bodyReachedCanonicalSuccessor_AFFB9473_3(bodyArc,LearningV354RouteDirectionalProgressEpsilon,successorStartArc);
            bool chronologyReachedSuccessor =
                projectedSequence >=
                    Math.Max(
                        step.EndBreadcrumbSequence,
                        successor.StartBreadcrumbSequence -
                            TraversalChainImmediateMaxBreadcrumbGap);
            bool successorPhysicallyLocal =
                FollowerCapabilityHelperRules.TryRetireLearningV397ExhaustedActuatorDebtByCanonicalSuccessorBodyEpoch_successorPhysicallyLocal_AFFB9473_4(successorEntryDistance,RouteMissionTraversalAdjacentBridgeMaxDistance,bodyArc,successorStartArc);
            bool monotonicBodyEpoch =
                FollowerCapabilityHelperRules.TryRetireLearningV397ExhaustedActuatorDebtByCanonicalSuccessorBodyEpoch_monotonicBodyEpoch_AFFB9473_5(bodyArc,LearningV354RouteDirectionalProgressEpsilon,cursor.LearningV375BestRouteArcLength);

            if (!localizedToCanonicalRoute ||
                !bodyBeyondActuatorFrontier ||
                !bodyReachedCanonicalSuccessor ||
                !chronologyReachedSuccessor ||
                !successorPhysicallyLocal ||
                !monotonicBodyEpoch)
            {
                return false;
            }

            string profileId = bot.ProfileId ?? string.Empty;
            int previousStepIndex = mission.CurrentStepIndex;
            float exhaustedAt = mission.LearningV357ExhaustedBarrierAt;
            int previousExecutionFloor = GetFollowerRouteExecutionFloor(profileId);
            int newExecutionFloor = Math.Max(previousExecutionFloor, step.EndBreadcrumbSequence);
            if (!string.IsNullOrEmpty(profileId))
            {
                FollowerRouteExecutionFloors[profileId] = newExecutionFloor;
            }

            mission.ResumeFloorBreadcrumbSequence = Math.Max(
                mission.ResumeFloorBreadcrumbSequence, newExecutionFloor);
            mission.PhysicalApproachBreadcrumbSequence = Math.Max(
                mission.PhysicalApproachBreadcrumbSequence, projectedSequence);
            mission.ApproachBreadcrumbSequence = Math.Max(
                mission.ApproachBreadcrumbSequence, projectedSequence);
            mission.ObservedDownstreamBreadcrumbSequence = Math.Max(
                mission.ObservedDownstreamBreadcrumbSequence, projectedSequence);
            if (!TryAdvanceRouteMission(bot, mission, mission.CurrentStepIndex + 1, RouteDebtEvidence.PhysicalPass, "TryRetireLearningV397ExhaustedActuatorDebtByCanonicalSuccessorBodyEpoch:17", r2DebtObservation19)) return false;
            CommitCapabilityMoveRetry(mission,FollowerCapabilityRetryPolicy.Change.Reset,0f);
            mission.TraversalRetryMovementHoldUntil = 0f;
            mission.LocalRecoveryAttempts = 0;
            mission.PathRejectsSinceProgress = 0;
            mission.RejectedBreadcrumbSequences.Clear();
            mission.FailureSignatureCounts.Clear();
            mission.LastProgressAt = Time.time;
            mission.LastProgressPosition = bot.Position;
            mission.LearningV355ProgressStepIndex = -1;
            mission.LearningV355BestEntryDistance = float.MaxValue;
            mission.LearningV355BestRouteProjection = float.MinValue;
            mission.LearningV355WrongWaySince = -1f;
            mission.LearningV336SelfRescueStepIndex = -1;
            mission.LearningV336SelfRescueEdgeId = 0;
            mission.LearningV336SelfRescueAttempts = 0;
            mission.LearningV336SelfRescueForceNativeNextAttempt = false;
            mission.LearningV336SelfRescueForceNativeSource = null;
            mission.LearningV357ExhaustedBarrierStepIndex = -1;
            mission.LearningV357ExhaustedBarrierEdgeId = 0;
            mission.LearningV357ExhaustedBarrierEndBreadcrumbSequence = 0;
            mission.LearningV357ExhaustedBarrierAt = -1f;

            ClearLearningV351TerminalRecoveryState(
                bot, "learningV397ExhaustedActuatorDebtRetiredByCanonicalSuccessorBodyEpoch");

            WriteEvent("learningV397ExhaustedActuatorDebtRetiredByCanonicalSuccessorBodyEpoch", bot, new
            {
                missionId = mission.MissionId,
                previousStepIndex,
                newStepIndex = mission.CurrentStepIndex,
                retiredEpisodeId = step.EpisodeId,
                retiredEdgeId = step.EdgeId,
                retiredKind = step.Kind,
                successorEpisodeId = successor.EpisodeId,
                successorEdgeId = successor.EdgeId,
                successorKind = successor.Kind,
                chainId = step.ChainId,
                verifierStage = probe.Stage.ToString(),
                exhaustedAt,
                previousExecutionFloor,
                newExecutionFloor,
                bodyArc,
                actuatorEndArc,
                successorStartArc,
                projectedSequence,
                routeLocalDistance,
                successorEntryDistance,
                traversalSuccessRecorded = false,
                persistentLearningChanged = false,
                routeSemanticCursorAdvanced = false,
                executionDebtRetired = true,
                recoveryTeleportUsed = false,
                bodyRelocated = false,
                action = "bodyAlreadyEnteredImmediateCanonicalSuccessorRetireOnlyExhaustedActuatorExecutionDebtNeverRunBackToOldEntry"
            });

            FinalizeLearningV397SupersededActuatorVerifier(
                probe, mission, step, successor, bodyArc, successorStartArc, projectedSequence);
            return true;
        }

        private static bool TryRetireLearningV443StaleActuatorDebtByMonotonicBodyFrontier(
            BotOwner bot,
            FollowerRouteMission mission,
            FollowerTraversalRouteCursor cursor)
        {
            if ((bot == null) || mission == null || cursor == null ||
                !cursor.LearningV375TransactionActive ||
                cursor.LearningV375RouteMissionId != mission.MissionId ||
                mission.CurrentStepIndex < 0 || mission.CurrentStepIndex >= mission.Steps.Count ||
                HasActiveTraversalProbe(bot))
            {
                return false;
            }

            FollowerRouteMissionStep step = mission.Steps[mission.CurrentStepIndex];
            var r2DebtObservation20 = CaptureRouteDebtObservation(bot, mission, step, false);

            if (step == null || step.EpisodeId <= 0 || step.EndBreadcrumbSequence <= 0 ||
                !step.CurrentRaidRouteAuthority)
            {
                return false;
            }

            LearningV354TraversalCapabilityProfile profile =
                GetLearningV354ActionProfile(GetLearningV354SemanticKind(step.EdgeId, step.Kind));
            if (profile == null || !profile.RequiresActuator)
            {
                return false;
            }

            // V448: route chronology is not actuator completion. Jump/Vault/Climb/DropDown may be
            // physically downstream because the body climbed onto a lip, was pushed, or a stale
            // producer overshot the authored breadcrumb. For typed actuators the existing
            // IsFollowerRouteMissionStepPhysicallyPassed contract already requires recent matching
            // traversal completion; without that proof V443 must preserve the semantic debt.
            if ((!IsFollowerRouteMissionStepPhysicallyPassed(
                    bot, step, out float learningV448TypedProjection,
                    out float learningV448TypedRequiredProjection)))
            {
                if (ShouldEmitDiagnostic(
                        "learningV448UnprovenActuatorDebtCannotRetireByBodyChronology:" +
                        mission.MissionId.ToString(CultureInfo.InvariantCulture) + ":" +
                        mission.CurrentStepIndex.ToString(CultureInfo.InvariantCulture),
                        bot, 0.18f))
                {
                    WriteEvent("learningV448UnprovenActuatorDebtCannotRetireByBodyChronology", bot, new
                    {
                        missionId = mission.MissionId,
                        stepIndex = mission.CurrentStepIndex,
                        edgeId = step.EdgeId,
                        kind = step.Kind,
                        body = Vec(bot.Position),
                        physicalProjection = learningV448TypedProjection,
                        requiredProjection = learningV448TypedRequiredProjection,
                        requiresActuator = profile.RequiresActuator,
                        requiresLandingProof = profile.RequiresLandingProof,
                        physicalPassageCanProveCompletion = profile.PhysicalPassageCanProveCompletion,
                        traversalSuccessRecorded = false,
                        persistentLearningChanged = false,
                        bodyRelocated = false,
                        action = "typedActuatorDebtRequiresExistingCompletionProofRouteChronologyAloneCannotEraseWHAT"
                    });
                }
                return false;
            }

            if (!TryProjectLearningV375RouteArc(
                    mission, bot.Position,
                    out float bodyArc, out _, out Vector3 projectedPoint, out int projectedSequence) ||
                !TryGetLearningV375SequenceArc(
                    mission, step.EndBreadcrumbSequence,
                    out float actuatorEndArc, out _))
            {
                return false;
            }

            float routeLocalDistance = GetPlanarDistance(bot.Position, projectedPoint);
            bool localizedToSameCanonicalRoute =
                routeLocalDistance <= RouteMissionBreadcrumbReachDistance;
            bool chronologyBeyondActuator =
                projectedSequence > step.EndBreadcrumbSequence;
            bool arcBeyondActuator =
                FollowerCapabilityHelperRules.TryRetireLearningV443StaleActuatorDebtByMonotonicBodyFrontier_arcBeyondActuator_15AADF94_1(bodyArc,LearningV354RouteDirectionalProgressEpsilon,actuatorEndArc);
            bool monotonicBodyEpoch =
                FollowerCapabilityHelperRules.TryRetireLearningV443StaleActuatorDebtByMonotonicBodyFrontier_monotonicBodyEpoch_15AADF94_2(bodyArc,LearningV354RouteDirectionalProgressEpsilon,cursor.LearningV375BestRouteArcLength);
            if (!localizedToSameCanonicalRoute ||
                !chronologyBeyondActuator ||
                !arcBeyondActuator ||
                !monotonicBodyEpoch)
            {
                return false;
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
            mission.PhysicalApproachBreadcrumbSequence = Math.Max(
                mission.PhysicalApproachBreadcrumbSequence, projectedSequence);
            mission.ApproachBreadcrumbSequence = Math.Max(
                mission.ApproachBreadcrumbSequence, projectedSequence);
            mission.ObservedDownstreamBreadcrumbSequence = Math.Max(
                mission.ObservedDownstreamBreadcrumbSequence, projectedSequence);
            if (!TryAdvanceRouteMission(bot, mission, mission.CurrentStepIndex + 1, RouteDebtEvidence.PhysicalPass, "TryRetireLearningV443StaleActuatorDebtByMonotonicBodyFrontier:18", r2DebtObservation20)) return false;
            CommitCapabilityMoveRetry(mission,FollowerCapabilityRetryPolicy.Change.Reset,0f);
            mission.TraversalRetryMovementHoldUntil = 0f;
            mission.LocalRecoveryAttempts = 0;
            mission.PathRejectsSinceProgress = 0;
            mission.RejectedBreadcrumbSequences.Clear();
            mission.FailureSignatureCounts.Clear();
            mission.LastProgressAt = Time.time;
            mission.LastProgressPosition = bot.Position;
            mission.LearningV355ProgressStepIndex = -1;
            mission.LearningV355BestEntryDistance = float.MaxValue;
            mission.LearningV355BestRouteProjection = float.MinValue;
            mission.LearningV355WrongWaySince = -1f;
            mission.LearningV336SelfRescueStepIndex = -1;
            mission.LearningV336SelfRescueEdgeId = 0;
            mission.LearningV336SelfRescueAttempts = 0;
            mission.LearningV336SelfRescueForceNativeNextAttempt = false;
            mission.LearningV336SelfRescueForceNativeSource = null;
            mission.LearningV431EquivalentBarrierRescuePending = false;
            mission.LearningV431EquivalentBarrierRescueActive = false;
            mission.LearningV431EquivalentBarrierRescueStepIndex = -1;
            mission.LearningV431EquivalentBarrierRescueEdgeId = 0;
            mission.LearningV431EquivalentBarrierRescueEpisodeId = 0;
            mission.LearningV431EquivalentBarrierRescueAttempts = 0;
            mission.LearningV431EquivalentBarrierRescueCapabilityFamily = null;
            mission.LearningV431EquivalentBarrierRescueSource = null;
            mission.LearningV357ExhaustedBarrierStepIndex = -1;
            mission.LearningV357ExhaustedBarrierEdgeId = 0;
            mission.LearningV357ExhaustedBarrierEndBreadcrumbSequence = 0;
            mission.LearningV357ExhaustedBarrierAt = -1f;

            ClearLearnedTraversalCommitment(
                bot, step.EdgeId,
                "learningV443MonotonicBodyFrontierRetiredStaleActuatorDebt");
            NavigationRecoveryTraversalAnchors.Remove(profileId);
            ClearLearningV351TerminalRecoveryState(
                bot, "learningV443MonotonicBodyFrontierRetiredStaleActuatorDebt");

            WriteEvent("learningV443MonotonicBodyFrontierRetiredStaleActuatorDebt", bot, new
            {
                missionId = mission.MissionId,
                previousStepIndex,
                currentStepIndex = mission.CurrentStepIndex,
                episodeId = step.EpisodeId,
                edgeId = step.EdgeId,
                kind = step.Kind,
                endBreadcrumbSequence = step.EndBreadcrumbSequence,
                projectedSequence,
                bodyArc,
                actuatorEndArc,
                routeLocalDistance,
                routeLocalEnvelope = RouteMissionBreadcrumbReachDistance,
                previousExecutionFloor,
                newExecutionFloor,
                currentRaidRouteAuthority = step.CurrentRaidRouteAuthority,
                traversalSuccessRecorded = false,
                nativeSuccessRecorded = false,
                episodeCompleted = false,
                persistentLearningChanged = false,
                routeSemanticCursorAdvanced = false,
                executionDebtRetired = true,
                recoveryTeleportUsed = false,
                bodyRelocated = false,
                evidenceRole = "liveBodyMonotonicFrontier",
                action = "bodyAlreadyAdvancedBeyondCurrentRaidActuatorRetireOnlyRuntimeExecutionDebtNeverPullBodyBackToStoredEntry"
            });

            // Keep the same Traversal writer/generation. Rebind the next canonical step immediately
            // so the body does not pause for one Follow tick between debt retirement and successor.
            EnsureFollowerRouteMissionCommitment(bot, mission);
            return true;
        }

        private static bool TryRetireLearningV394DownstreamPassageExecutionDebt(
            BotOwner bot,
            FollowerRouteMission mission,
            FollowerTraversalRouteCursor cursor)
        {
            if ((bot == null) || mission == null ||
                cursor == null || !cursor.LearningV375TransactionActive ||
                cursor.LearningV375RouteMissionId != mission.MissionId ||
                mission.CurrentStepIndex < 0 || mission.CurrentStepIndex >= mission.Steps.Count ||
                HasActiveTraversalProbe(bot))
            {
                return false;
            }

            string profileId = bot.ProfileId ?? string.Empty;
            bool retiredAny = false;
            while (mission.CurrentStepIndex >= 0 && mission.CurrentStepIndex < mission.Steps.Count)
            {
                FollowerRouteMissionStep step = mission.Steps[mission.CurrentStepIndex];
            var r2DebtObservation21 = CaptureRouteDebtObservation(bot, mission, step, false);

                if (step == null || step.EndBreadcrumbSequence <= 0)
                {
                    break;
                }

                LearningV354TraversalCapabilityProfile profile = GetLearningV354ActionProfile(step.Kind);
                if (profile == null || profile.RequiresActuator || !profile.PhysicalPassageCanProveCompletion)
                {
                    break;
                }

                if (LearnedTraversalCommitments.TryGetValue(profileId, out LearnedTraversalCommitment liveCommitment) &&
                    liveCommitment != null &&
                    (liveCommitment.EpisodeId == step.EpisodeId ||
                     (liveCommitment.RouteMissionId == mission.MissionId &&
                      liveCommitment.RouteMissionStepIndex == mission.CurrentStepIndex)))
                {
                    break;
                }

                // Normal finite-corridor completion remains authoritative. V394 is only for the case
                // where that local proof can no longer succeed because the physical body has already
                // joined a later point of the same empirical route.
                if (IsFollowerRouteMissionStepPhysicallyPassed(bot, step, out _, out _))
                {
                    break;
                }

                if (!TryProjectLearningV375RouteArc(
                        mission, bot.Position,
                        out float bodyArc, out _, out Vector3 projectedPoint, out int projectedSequence) ||
                    !TryGetLearningV375SequenceArc(
                        mission, step.EndBreadcrumbSequence,
                        out float passageEndArc, out _))
                {
                    break;
                }

                float routeLocalDistance = GetPlanarDistance(bot.Position, projectedPoint);
                bool localizedToSameRoute = routeLocalDistance <= RouteMissionBreadcrumbReachDistance;
                bool chronologyBeyondPassage = projectedSequence > step.EndBreadcrumbSequence;
                bool arcBeyondPassage =
                    FollowerCapabilityHelperRules.TryRetireLearningV394DownstreamPassageExecutionDebt_arcBeyondPassage_585C6536_1(bodyArc,passageEndArc,RouteMissionBreadcrumbReachDistance);
                bool monotonicBodyEpoch =
                    FollowerCapabilityHelperRules.TryRetireLearningV394DownstreamPassageExecutionDebt_monotonicBodyEpoch_585C6536_2(bodyArc,LearningV354RouteDirectionalProgressEpsilon,cursor.LearningV375BestRouteArcLength);
                if (!localizedToSameRoute || !chronologyBeyondPassage || !arcBeyondPassage || !monotonicBodyEpoch)
                {
                    break;
                }

                int previousStepIndex = mission.CurrentStepIndex;
                int previousExecutionFloor = GetFollowerRouteExecutionFloor(profileId);
                int newExecutionFloor = Math.Max(previousExecutionFloor, step.EndBreadcrumbSequence);
                if (!string.IsNullOrEmpty(profileId))
                {
                    FollowerRouteExecutionFloors[profileId] = newExecutionFloor;
                }
                mission.ResumeFloorBreadcrumbSequence = Math.Max(
                    mission.ResumeFloorBreadcrumbSequence, newExecutionFloor);
                mission.PhysicalApproachBreadcrumbSequence = Math.Max(
                    mission.PhysicalApproachBreadcrumbSequence, projectedSequence);
                mission.ApproachBreadcrumbSequence = Math.Max(
                    mission.ApproachBreadcrumbSequence, projectedSequence);
                mission.ObservedDownstreamBreadcrumbSequence = Math.Max(
                    mission.ObservedDownstreamBreadcrumbSequence, projectedSequence);
                if (!TryAdvanceRouteMission(bot, mission, mission.CurrentStepIndex + 1, RouteDebtEvidence.PhysicalPass, "TryRetireLearningV394DownstreamPassageExecutionDebt:19", r2DebtObservation21)) return false;
                mission.LastProgressAt = Time.time;
                mission.LastProgressPosition = bot.Position;
                CommitCapabilityMoveRetry(mission,FollowerCapabilityRetryPolicy.Change.Reset,0f);
                mission.TraversalRetryMovementHoldUntil = 0f;
                mission.LearningV355ProgressStepIndex = -1;
                ClearLearnedTraversalCommitment(
                    bot, step.EdgeId, "learningV394DownstreamPassageExecutionDebtRetired");
                ClearLearningV351TerminalRecoveryState(
                    bot, "learningV394DownstreamPassageExecutionDebtRetired");
                retiredAny = true;

                WriteEvent("learningV394DownstreamPassageExecutionDebtRetired", bot, new
                {
                    missionId = mission.MissionId,
                    previousStepIndex,
                    currentStepIndex = mission.CurrentStepIndex,
                    episodeId = step.EpisodeId,
                    edgeId = step.EdgeId,
                    kind = step.Kind,
                    endBreadcrumbSequence = step.EndBreadcrumbSequence,
                    projectedSequence,
                    bodyArc,
                    passageEndArc,
                    routeLocalDistance,
                    routeLocalEnvelope = RouteMissionBreadcrumbReachDistance,
                    previousExecutionFloor,
                    newExecutionFloor,
                    traversalSuccessRecorded = false,
                    nativeSuccessRecorded = false,
                    episodeCompleted = false,
                    persistentLearningChanged = false,
                    actuatorDebtRetired = false,
                    bodyRelocated = false,
                    action = "retireRuntimePassageDebtOnlyAfterBodyLocalizedBeyondFiniteFrontierOnSameCanonicalRoute"
                });
            }

            if (!retiredAny)
            {
                return false;
            }

            // Re-enter the ordinary mission consumer immediately. If all stale passage fragments
            // were behind the body, the loop retires them in one physical epoch before any of them
            // can reacquire a commitment. The next real action is then bound, or the existing V382
            // terminal writer release/fresh-Follow prime runs when the mission is exhausted.
            EnsureFollowerRouteMissionCommitment(bot, mission);
            return true;
        }
    }
}
