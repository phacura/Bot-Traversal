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

        // RC66: Same current-raid WT, short near-side entry, and a demonstrably
        // winding/missing STRICT nav path. Do not chase a long ground detour when
        // the actor is already directly in front of this observed passage.
        // The same Follow writer owns this collision-active Player.Move request.
        // Native/WT crossing proof still belongs to the normal executor.
        private static bool TryDriveRC66SameEpisodeWalkThroughNearSideIngress(
            BotOwner bot, FollowerRouteMission mission, FollowerRouteMissionStep step)
        {
            if (bot == null || mission == null || step == null ||
                !step.CurrentRaidRouteAuthority ||
                !string.Equals(step.Kind, "WalkThrough", StringComparison.OrdinalIgnoreCase) ||
                mission.CurrentStepIndex < 0 ||
                mission.CurrentStepIndex >= mission.Steps.Count ||
                mission.Steps[mission.CurrentStepIndex] != step ||
                HasActiveTraversalProbe(bot) || !IsFinite(bot.Position) ||
                !IsFinite(step.Entry) || !IsFinite(step.RawEntry) ||
                !IsFinite(step.RawExit))
                return false;
            Player player = bot.GetPlayer ?? bot.AIData?.Player;
            if (player?.MovementContext == null || !player.MovementContext.IsGrounded ||
                player.CurrentStateName == EPlayerState.Transition ||
                Mathf.Abs(bot.Position.y - step.Entry.y) > 0.55f)
                return false;
            Vector3 axis = GetFlatDirection(step.RawExit - step.RawEntry);
            if (axis.sqrMagnitude < 0.5f) return false;
            Vector3 fromEntry = bot.Position - step.RawEntry;
            fromEntry.y = 0f;
            float longitudinal = Vector3.Dot(fromEntry, axis);
            float lateral = (fromEntry - axis * longitudinal).magnitude;
            float distance = GetPlanarDistance(bot.Position, step.Entry);
            // RC68: after the existing Follow writer has retired the stale path for
            // this exact observed WT step, keep the SAME near-side correction for
            // a bounded stopping-distance overshoot. One 14ms correction did not
            // retire EFT's 4-5m/s inherited detour in RC67 Mission125.
            bool nearSidePhysicalLaneOwned = step.Rc48MoverPathRetired;
            float maxNearSideApproach = nearSidePhysicalLaneOwned ? 3.60f : 2.50f;
            // The entry itself belongs to the unchanged native WT actuator;
            // do not block it inside 0.75m, or after the entry was crossed.
            if (distance < 0.75f || distance > Mathf.Max(RouteMissionExecutionCommitDistance,
                    maxNearSideApproach + 0.15f) ||
                longitudinal >= -0.70f || longitudinal < -maxNearSideApproach ||
                lateral > 0.55f)
                return false;
            StrictPathResult strict = CalculateStrictPath(bot.Position, step.Entry);
            bool shortStrictPath = strict != null && strict.StrictComplete &&
                strict.PathLength <= distance * 1.50f + 0.20f;
            // A valid short path is not enough when the actual actor velocity
            // points back OUT of the entry corridor. Correct measured wrong-way
            // input before the V355 watchdog merely records a distant detour.
            Vector3 toEntryHeading = step.Entry - bot.Position;
            toEntryHeading.y = 0f;
            Vector3 velocityHeading = player.Velocity;
            velocityHeading.y = 0f;
            float velocityToEntryDot = MeasureRC70IngressDirectionalAgreement(
                velocityHeading, toEntryHeading, 0.64f, 0.01f, 1f);
            bool measuredWrongWay = velocityHeading.sqrMagnitude > 0.64f &&
                velocityToEntryDot < -0.25f;
            // A short NavMesh path observed AFTER the physical writer took this
            // entry is not an ACK of actual body correction. Hold the single
            // writer until the executor frontier, not merely one issued input.
            if (shortStrictPath && !measuredWrongWay && !nearSidePhysicalLaneOwned)
                return false;
            if (!TryEnterV2CurrentIngressScope(
                    bot, mission, step, "v2rc66SameEpisodeWTNearSidePhysicalIngress",
                    out FollowerMovementWriterAuthority.WriteScope scope, out _))
                return false;
            bool committed = false;
            using (scope)
            {
                if (!step.Rc48MoverPathRetired && bot.Mover != null)
                {
                    // Retire the previously chosen long-detour BotMover target
                    // once. Keeping it alive competes with exact world-space
                    // Player.Move even while Follow owns the only write scope.
                    bot.Mover.BotMoverStop("R1-9D804919CCB8",@bot);
                    step.Rc48MoverPathRetired = true;
                }
                if (bot.Mover != null)
                    bot.Mover.BotMoverSprint("R1-7A967ADA038E",@bot,false,false);
                Vector3 toEntry = step.Entry - bot.Position;
                toEntry.y = 0f;
                float dt = Mathf.Max(0.001f, Time.deltaTime);
                Vector3 direction = toEntry.normalized;
                float walkScale = Mathf.Clamp(distance / 2.25f, 0.42f, 0.67f);
                committed = (ApplyCollisionActivePhysicalRouteMotion(
                    bot, player, direction,
                    direction * Mathf.Min(distance, ChainLocalSuccessorBridgeSpeed * walkScale * dt),
                    dt, 1f, sprint: false,
                    targetMoveSpeed: ProbeMomentumNormalMoveSpeed * walkScale,
                    preserveSprintMode: false, exactWorldDirectionInput: true,deliveryTarget:step.Entry,deliveryPurpose:CapabilityMotionPurpose.FollowIngress)).Accepted;
                if (committed && bot.Mover != null)
                {
                    // Keep manual BotMover routing from overriding the already
                    // issued single-writer world-space input in this frame.
                    bot.Mover.SetBotMoverPause("R1-9E289028D9EF",@bot,true);
                    bot.Mover.SetBotMoverIsMoving("R1-150A09B472DC",@bot,false);
                }
                if (committed)
                    try { bot.Steering?.BotSteeringLookToDirection("R1-B5E42228C51B",@bot,axis); } catch { }
            }
            if (ShouldEmitDiagnostic("v2rc66SameEpisodeWTNearSidePhysicalIngress:" +
                    mission.MissionId.ToString(CultureInfo.InvariantCulture) + ":" +
                    step.EdgeId.ToString(CultureInfo.InvariantCulture), bot, 0.22f))
                WriteEvent("v2rc66SameEpisodeWTNearSidePhysicalIngress", bot, new
                {
                    missionId = mission.MissionId,
                    stepIndex = mission.CurrentStepIndex,
                    edgeId = step.EdgeId,
                    episodeId = step.EpisodeId,
                    body = Vec(bot.Position),
                    entry = Vec(step.Entry),
                    entryDistance = distance, longitudinal, lateral,
                    measuredWrongWay, velocityToEntryDot,
                    strictPathComplete = strict?.StrictComplete == true,
                    strictPathLength = strict?.PathLength ?? -1f,
                    issuedInput = committed,
                    nativeAttempted = false,
                    physicalCrossingProven = false,
                    bodyRelocated = false,
                    action = "singleExistingFollowWriterClosesObservedWTEntryWithoutWindingNavOrFalseSuccess"
                });
            return committed;
        }

        // Climb ingress uses the single shared capability ingress host.

        private static bool TryDriveV2RC57ExistingNonClimbPreProbePhysicalLane(
            BotOwner bot,
            FollowerRouteMission mission,
            FollowerRouteMissionStep step,
            Vector3 physicalTarget,
            string laneSource,
            bool preserveExecutorHeadingAtPositionFrontier,
            out float bodyDisplacement,
            out float targetClosure)
        {
            bodyDisplacement = 0f;
            targetClosure = 0f;
            bool learningV448JumpOpportunityProducer =
                (step != null) &&
                string.Equals(step.Kind, "Jump", StringComparison.OrdinalIgnoreCase);
            if ((bot == null) || mission == null || step == null ||
                !IsFinite(physicalTarget) || HasActiveTraversalProbe(bot) ||
                (!learningV448JumpOpportunityProducer &&
                 string.Equals(step.Kind, "Jump", StringComparison.OrdinalIgnoreCase)) ||
                !GetLearningV354ActionProfile(step.Kind).RequiresActuator)
            {
                return false;
            }

            Player player = bot.GetPlayer ?? bot.AIData?.Player;
            if (player?.MovementContext == null)
            {
                return false;
            }

            // RC54: measured ACTOR-SIDE native entry must outrank a retired
            // navigation strategy. RC53 Mission30/Edge25 physically reached
            // RawEntry to 0.071m, with 0.036m lateral offset and 16.1 deg
            // unresolved yaw, AFTER RC49 marked its old fallback retired.
            // The old early return then held the stale mover and prevented
            // RC45/46's pose/sprint handshake; another producer displaced the
            // grounded body before yaw reached the original 12 deg limit.
            // This is NOT a new approach route or native admission path:
            // preserve original geometry/grounding/transition constraints,
            // one Follow writer, real measured body, and native admission.
            if (string.Equals(step.Kind, "Climb", StringComparison.OrdinalIgnoreCase) &&
                (step.Rc48IngressFallbackActive || step.Rc49FallbackRetired) &&
                !HasActiveTraversalProbe(bot) &&
                TryGetLearningV424ActuatorExecutorAdmission(
                    bot, step,
                    out bool rc54AlreadyAdmissible, out _,
                    out float rc54EntryDistance, out float rc54Forward,
                    out float rc54Lateral, out float rc54MaxLateral,
                    out float rc54Angle, out float rc54MaxAngle,
                    out float rc54MaxForward, out float rc54Trigger, out _) &&
                !rc54AlreadyAdmissible &&
                rc54EntryDistance <= rc54Trigger &&
                rc54Lateral <= rc54MaxLateral &&
                rc54Forward >= -rc54Trigger &&
                rc54Forward <= rc54MaxForward &&
                Mathf.Abs(bot.Position.y - step.RawEntry.y) <= 0.40f &&
                player.MovementContext.IsGrounded)
            {
                Vector3 rc54Axis = GetFlatDirection(step.RawExit - step.RawEntry);
                bool rc54SprintUnsettled = bot.Mover?.Sprinting == true ||
                    player.MovementContext.IsSprintEnabled ||
                    player.CurrentStateName == EPlayerState.Sprint;
                bool rc54TransitionUnsettled =
                    player.CurrentStateName == EPlayerState.Transition;
                if (rc54Axis.sqrMagnitude > 0.5f &&
                    (rc54Angle > rc54MaxAngle || rc54SprintUnsettled ||
                     rc54TransitionUnsettled))
                {
                    if (!TryEnterV2CurrentIngressScope(
                            bot, mission, step, "v2rc54PhysicalNativeEntryPosePriority",
                            out FollowerMovementWriterAuthority.WriteScope rc54Scope, out _))
                        return false;
                    using (rc54Scope)
                    {
                        // A preliminary NavMesh stage is advisory. Physical
                        // arrival at the exact native entry is stronger than an
                        // old stage goal; do not chase the stage backwards.
                        if (step.Rc47ClimbStageSelected)
                        {
                            step.Rc47ClimbStageReached = true;
                            step.Rc47ClimbStageHeadingReady = true;
                        }
                        TryRequestCF13ClimbSprintRelease(bot, step.EdgeId,
                            "rc54PhysicalNativeEntryOutranksRetiredFallback");
                        if (bot.Mover != null)
                        {
                            bot.Mover.BotMoverStop("R1-014416544D2D",@bot);
                            bot.Mover.SetBotMoverPause("R1-08AA8929CCF0",@bot,true);
                            bot.Mover.SetBotMoverIsMoving("R1-5CF3801737C8",@bot,false);
                            bot.Mover.BotMoverSprint("R1-58DF0ECD194A",@bot,false,false);
                        }
                        player.SetPlayerInputDirection("R1-3DC74BC58C31",@bot,Vector2.zero);
                        player.MovementContext.SetMovementContextMovementDirection("R1-042D3B206844",@bot,Vector2.zero);
                        try { bot.Steering?.BotSteeringLookToDirection("R1-3948176941D8",@bot,rc54Axis); } catch { }
                    }
                    if (ShouldEmitDiagnostic("v2rc54PhysicalNativeEntryPosePriority:" +
                            mission.MissionId.ToString(CultureInfo.InvariantCulture) + ":" +
                            step.EdgeId.ToString(CultureInfo.InvariantCulture), bot, 0.18f))
                        WriteEvent("v2rc54PhysicalNativeEntryPosePriority", bot, new
                        {
                            missionId = mission.MissionId,
                            edgeId = step.EdgeId,
                            position = Vec(bot.Position),
                            rawEntry = Vec(step.RawEntry),
                            entryDistance = rc54EntryDistance,
                            forward = rc54Forward,
                            lateral = rc54Lateral,
                            angle = rc54Angle,
                            maxAngle = rc54MaxAngle,
                            sprintUnsettled = rc54SprintUnsettled,
                            transitionUnsettled = rc54TransitionUnsettled,
                            retiredFallback = step.Rc49FallbackRetired,
                            nativeAdmissible = false,
                            teleport = false,
                            action = "measuredExactNativeEntryBeatsExpiredNavigationStage;HoldPositionReleaseSprintAndSteer"
                        });
                    return true;
                }
            }

            // RC56: The RC55 Mission30/Edge25 counterexample had a genuinely
            // aligned (0 deg), grounded body at 0.713 m from RawEntry, yet the
            // previous RC44 BotMover retained PathComplete without moving for
            // >1 s. RC48 then handed the same point to collision fallback only
            // after the body was already pulled sideways.  A PRE-NATIVE short
            // closeout belongs to the SAME Follow writer, not to an extra planner.
            //
            // This is a *pose/translation* controller, NEVER actuator admission:
            // keep original triggerDistance, exact raw entry, lateral, side,
            // Grounded, strict NavMesh and native action gates unchanged.
            // At the supported near-side frontier: turn first, then use the
            // existing collision-aware EFT.Player.Move producer with a bounded
            // speed while retaining the native axis. Do not change Dropdown,
            // Jump, WalkThrough, or a working distant BotMover route.
            bool rc56NearSideStalled = false;
            if (string.Equals(step.Kind, "Climb", StringComparison.OrdinalIgnoreCase) &&
                step.Rc47ClimbStageSelected && step.Rc47ClimbStageReached &&
                !step.Rc48IngressFallbackActive && !step.Rc49FallbackRetired &&
                !preserveExecutorHeadingAtPositionFrontier &&
                TryGetLearningV424ActuatorExecutorAdmission(
                    bot, step, out bool rc56Admissible, out _,
                    out float rc56Distance, out float rc56Forward,
                    out float rc56Lateral, out float rc56MaxLateral,
                    out float rc56Angle, out float rc56MaxAngle,
                    out _, out float rc56Trigger, out _) &&
                !rc56Admissible && rc56Distance > rc56Trigger &&
                rc56Distance <= rc56Trigger + 0.42f &&
                rc56Forward < -rc56Trigger + 0.10f &&
                rc56Forward >= -(rc56Trigger + 0.48f) &&
                rc56Lateral <= Mathf.Min(rc56MaxLateral, 0.30f) &&
                Mathf.Abs(bot.Position.y - step.RawEntry.y) <= 0.40f &&
                player.MovementContext.IsGrounded &&
                player.CurrentStateName != EPlayerState.Transition)
            {
                Vector3 rc56Axis = GetFlatDirection(step.RawExit - step.RawEntry);
                Vector3 rc56ToEntry = step.RawEntry - bot.Position;
                rc56ToEntry.y = 0f;
                StrictPathResult rc56Path = CalculateStrictPath(bot.Position, step.RawEntry);
                bool rc56Supported = rc56Axis.sqrMagnitude > 0.5f &&
                    rc56ToEntry.sqrMagnitude > 0.0001f &&
                    rc56Path?.StrictComplete == true &&
                    rc56Path.PathLength <= rc56Distance * 1.30f + 0.10f;
                bool rc56ProgressKnown = rc56Supported &&
                    TryObserveRC48PhysicalIngress(bot, mission, step,
                        step.RawEntry, out rc56NearSideStalled);
                if (rc56Supported && rc56ProgressKnown && !rc56NearSideStalled &&
                    TryEnterV2CurrentIngressScope(
                        bot, mission, step, "v2rc56ActorSideClimbPoseCloseout",
                        out FollowerMovementWriterAuthority.WriteScope rc56Scope,
                        out _))
                {
                    bool rc56TurnOnly = rc56Angle > rc56MaxAngle ||
                        bot.Mover?.Sprinting == true ||
                        player.MovementContext.IsSprintEnabled ||
                        player.CurrentStateName == EPlayerState.Sprint;
                    bool rc56InputCommitted = false;
                    using (rc56Scope)
                    {
                        TryRequestCF13ClimbSprintRelease(bot, step.EdgeId,
                            "rc56SameOwnerActorSideClimbCloseout");
                        if (rc56TurnOnly)
                        {
                            if (bot.Mover != null)
                            {
                                bot.Mover.SetBotMoverPause("R1-B6D3EAB93316",@bot,true);
                                bot.Mover.SetBotMoverIsMoving("R1-3E988F1924D4",@bot,false);
                                bot.Mover.BotMoverSprint("R1-41CD064C5209",@bot,false,false);
                            }
                            player.SetPlayerInputDirection("R1-903DE16354C4",@bot,Vector2.zero);
                            player.MovementContext.SetMovementContextMovementDirection("R1-7AF408FDADA0",@bot,Vector2.zero);
                            try { bot.Steering?.BotSteeringLookToDirection("R1-BC2A9C1C54C0",@bot,rc56Axis); } catch { }
                        }
                        else
                        {
                            // Do not let the old RC44 path issue an independent
                            // direction while this identical Follow owner drives
                            // the final few centimetres through EFT locomotion.
                            step.Rc48MoverCommandAccepted = false;
                            float rc56Dt = Mathf.Max(0.001f, Time.deltaTime);
                            float rc56SpeedScale = Mathf.Clamp(
                                (rc56Distance - rc56Trigger) / 0.42f,
                                0.45f, 0.70f);
                            Vector3 rc56Direction = rc56ToEntry.normalized;
                            rc56InputCommitted = (ApplyCollisionActivePhysicalRouteMotion(
                                bot, player, rc56Direction,
                                rc56Direction * Mathf.Min(rc56Distance - rc56Trigger + 0.035f,
                                    ChainLocalSuccessorBridgeSpeed * rc56SpeedScale * rc56Dt),
                                rc56Dt, 1f, sprint: false,
                                targetMoveSpeed: ProbeMomentumNormalMoveSpeed * rc56SpeedScale,
                                preserveSprintMode: false,
                                exactWorldDirectionInput: true,deliveryTarget:step.RawEntry,deliveryPurpose:CapabilityMotionPurpose.FollowIngress)).Accepted;
                            // Route translation and native action yaw are separate
                            // contracts: the final direction remains the exact
                            // Climb axis, not a stale BotMover waypoint heading.
                            try { bot.Steering?.BotSteeringLookToDirection("R1-310DB36878A5",@bot,rc56Axis); } catch { }
                        }
                    }
                    if (ShouldEmitDiagnostic("v2rc56ActorSideClimbPoseCloseout:" +
                            mission.MissionId.ToString(CultureInfo.InvariantCulture) + ":" +
                            step.EdgeId.ToString(CultureInfo.InvariantCulture), bot, 0.18f))
                        WriteEvent("v2rc56ActorSideClimbPoseCloseout", bot, new
                        {
                            missionId = mission.MissionId, edgeId = step.EdgeId,
                            body = Vec(bot.Position), rawEntry = Vec(step.RawEntry),
                            entryDistance = rc56Distance, triggerDistance = rc56Trigger,
                            forward = rc56Forward, lateral = rc56Lateral,
                            headingError = rc56Angle, maxHeadingError = rc56MaxAngle,
                            strictPathLength = rc56Path.PathLength,
                            turnOnly = rc56TurnOnly, inputCommitted = rc56InputCommitted,
                            nativeAttemptRecorded = false, bodyRelocated = false,
                            action = "sameFollowOwnerClosesVerifiedNearSidePoseWithoutNativeAdmissionOrNewPath"
                        });
                    return true;
                }
            }

            // RC48: strategy ACK is the FOLLOWING frames' body/path cost, not
            // the same-frame Player.Move return (which is commonly zero in EFT).
            // Every RouteStep owns one target epoch and one actual-progress receipt.
            // A valid NavMesh path is only an admission predicate, never progress.
            if (string.Equals(step.Kind, "Climb", StringComparison.OrdinalIgnoreCase) &&
                !preserveExecutorHeadingAtPositionFrontier &&
                !step.Rc48IngressFallbackActive &&
                IsFinite(physicalTarget) &&
                TryObserveRC48PhysicalIngress(bot, mission, step, physicalTarget,
                    out bool rc48Stalled) && (rc48Stalled || rc56NearSideStalled))
            {
                step.Rc48IngressFallbackActive = true;
                // The collision-active strategy has a different physical target
                // and must start a NEW measurement epoch on this same RouteStep.
                // RC48 previously carried the exhausted mover receipt forward
                // but never observed fallback progress at all.
                step.Rc48IngressLastSampleAt = 0f;
                step.Rc48IngressFirstObservedAt = 0f;
                step.Rc48MoverCommandAccepted = false;
                // RC51: V363 and V379 must own the SAME immutable target in a
                // Follow step. RC49 invalidated the validated Climb pre-stage
                // the instant EFT BotMover stalled, then V379 walked toward
                // RawEntry while V363 still produced the pre-stage lane. That
                // target split is observed at RC50 Mission27/Edge25 and causes
                // a direction reversal BEFORE any native Climb attempt.
                //
                // Keep the current V363 goal, including a verified pre-stage,
                // when the CURRENT body has a bounded STRICT path to it. Only
                // fall back to the original RawEntry if that path is unproven.
                // This is a producer switch, NOT a new planner, writer, epoch,
                // teleport, action completion, or relaxed actuator admission.
                Vector3 rc51Candidate =
                    step.Rc47ClimbStageSelected && !step.Rc47ClimbStageReached
                        ? step.Rc47ClimbStageTarget : physicalTarget;
                StrictPathResult rc51CandidatePath = IsFinite(rc51Candidate)
                    ? CalculateStrictPath(bot.Position, rc51Candidate) : null;
                float rc51CandidateDirect = IsFinite(rc51Candidate)
                    ? GetPlanarDistance(bot.Position, rc51Candidate) : float.PositiveInfinity;
                bool rc51CandidateSafe = rc51CandidatePath?.StrictComplete == true &&
                    rc51CandidateDirect <= RouteMissionTraversalAdjacentBridgeMaxDistance &&
                    rc51CandidatePath.PathLength <= Mathf.Min(
                        RouteMissionTraversalAdjacentBridgeMaxDistance,
                        rc51CandidateDirect * 1.65f + 0.30f) &&
                    Mathf.Abs(bot.Position.y - rc51Candidate.y) <= 0.55f;
                bool rc51StagePreserved = rc51CandidateSafe &&
                    step.Rc47ClimbStageSelected && !step.Rc47ClimbStageReached;
                step.Rc48NormalStageRevoked =
                    step.Rc47ClimbStageSelected && !rc51StagePreserved;
                if (step.Rc48NormalStageRevoked)
                    step.Rc47ClimbStageSelected = false;
                step.Rc48MoverCommandAccepted = false;
                Vector3 rc51FallbackTarget = rc51CandidateSafe
                    ? rc51Candidate : step.RawEntry;
                // Strictly revalidate the ACTUAL fallback target after every
                // producer switch; a PathComplete return is not physical ACK.
                StrictPathResult rc48FallbackPath = CalculateStrictPath(
                    bot.Position, rc51FallbackTarget);
                float rc48Direct = GetPlanarDistance(bot.Position, rc51FallbackTarget);
                bool rc48SafeFallback = rc48FallbackPath?.StrictComplete == true &&
                    rc48Direct <= RouteMissionTraversalAdjacentBridgeMaxDistance &&
                    rc48FallbackPath.PathLength <= Mathf.Min(
                        RouteMissionTraversalAdjacentBridgeMaxDistance,
                        rc48Direct * 1.65f + 0.30f) &&
                    Mathf.Abs(bot.Position.y - rc51FallbackTarget.y) <= 0.55f;
                // Reuse RC48's observed target as the pinned goal for the
                // collision-active producer, resetting its measurement epoch.
                step.Rc48IngressObservedTarget = rc51FallbackTarget;
                WriteEvent("v2rc51CanonicalIngressFallbackTargetBound", bot, new
                {
                    missionId = mission.MissionId, edgeId = step.EdgeId,
                    failedMoverTarget = Vec(physicalTarget),
                    physicalFallbackTarget = Vec(rc51FallbackTarget),
                    stagePreserved = rc51StagePreserved,
                    candidateStrict = rc51CandidateSafe,
                    fallbackStrict = rc48SafeFallback,
                    nativeAttemptRecorded = false, bodyRelocated = false,
                    action = "sameRouteStepRetainsOneStrictGoalAcrossMoverAndCollisionProducers"
                });
                if (ShouldEmitDiagnostic("v2rc48PhysicalIngressLivenessFallback:" +
                        mission.MissionId.ToString(CultureInfo.InvariantCulture) + ":" +
                        step.EdgeId.ToString(CultureInfo.InvariantCulture), bot, 0.2f))
                    WriteEvent("v2rc48PhysicalIngressLivenessFallback", bot, new
                    {
                        missionId = mission.MissionId, edgeId = step.EdgeId,
                        actualPosition = Vec(bot.Position), failedTarget = Vec(physicalTarget),
                        bestPathCost = step.Rc48IngressBestPathCost,
                        progressAge = Time.time - step.Rc48IngressLastProgressAt,
                        normalStageRevoked = step.Rc48NormalStageRevoked,
                        strictFallback = rc48SafeFallback,
                        nativeSuccessRecorded = false, bodyRelocated = false,
                        action = "withdrawNoProgressMoverStrategyWithoutGrantingTraversalSuccess"
                    });
                if (!rc48SafeFallback)
                    return false;
                physicalTarget = rc51FallbackTarget;
                laneSource = "currentRaidBreadcrumb:rc51CanonicalCollisionFallback";
            }
            // RC49: a RouteStep liveness receipt must cover BOTH physical producers.
            // RC48 stopped measuring progress at the exact moment it abandoned
            // BotMover for collision-active movement. The RC48 raid proves that
            // the latter then walked/orbited away from the Climb entry for ~7s.
            // Keep the SAME Step/Writer and existing strict path truth. Never
            // allow a fallback with no useful body->entry closure to issue another
            // unchecked collision input, even when CalculateStrictPath is complete.
            if (step.Rc48IngressFallbackActive &&
                string.Equals(step.Kind, "Climb", StringComparison.OrdinalIgnoreCase))
            {
                // RC51: keep the one goal already accepted by the original
                // V363 producer. A finished normal pre-stage is the only
                // legitimate reason to move on to its existing final target.
                if (step.Rc47ClimbStageSelected && !step.Rc47ClimbStageReached)
                    physicalTarget = step.Rc47ClimbStageTarget;
                else if (!step.Rc47ClimbStageSelected &&
                    IsFinite(step.Rc48IngressObservedTarget) &&
                    GetPlanarDistance(bot.Position, step.Rc48IngressObservedTarget) > 0.42f)
                    physicalTarget = step.Rc48IngressObservedTarget;
                laneSource = "currentRaidBreadcrumb:rc51CanonicalCollisionFallback";
                if (!step.Rc49FallbackRetired)
                {
                    bool rc49Observed = TryObserveRC48PhysicalIngress(
                        bot, mission, step, physicalTarget, out bool rc49Stalled);
                    float rc49Cost = 0f;
                    bool rc49HasCost = false;
                    if (rc49Observed)
                    {
                        StrictPathResult rc49Path = CalculateStrictPath(bot.Position, physicalTarget);
                        if (rc49Path?.StrictComplete == true)
                        {
                            rc49Cost = rc49Path.PathLength;
                            rc49HasCost = true;
                        }
                    }
                    // A complete path can still be walking the WRONG way. Here
                    // "regression" means increased remaining strictly navigable
                    // cost relative to the same-step best witnessed body position.
                    bool rc49Regressed = rc49Observed && rc49HasCost &&
                        rc49Cost >= step.Rc48IngressBestPathCost + 0.32f &&
                        Time.time - step.Rc48IngressFirstObservedAt >= 0.65f &&
                        rc49Cost >= 0.55f;
                    // RC48's original >0.55m gate was for *distant* BotMover
                    // staging. After its bounded fallback even a 0.35m remaining
                    // segment may be genuinely stuck. Require 1.35s of unchanged
                    // NEXT-FRAME path cost rather than treating every near-entry
                    // pause/heading adjustment as a failure.
                    bool rc49NearStalled = rc49Observed &&
                        !float.IsNaN(rc49Cost) && rc49Cost > 0.26f &&
                        Time.time - step.Rc48IngressFirstObservedAt >= 1.35f &&
                        Time.time - step.Rc48IngressLastProgressAt >= 1.35f;
                    // When an unsupported path disappears, no forward writer can
                    // cite it as a physical navigation contract. Do not fabricate
                    // a recovery path or cross the obstacle.
                    if (!rc49Observed || !rc49HasCost || rc49Stalled || rc49Regressed || rc49NearStalled)
                    {
                        step.Rc49FallbackRetired = true;
                        if (ShouldEmitDiagnostic("v2rc49PhysicalFallbackRetired:" +
                                mission.MissionId.ToString(CultureInfo.InvariantCulture) + ":" +
                                step.EdgeId.ToString(CultureInfo.InvariantCulture), bot, 0.20f))
                            WriteEvent("v2rc49PhysicalFallbackRetired", bot, new
                            {
                                missionId = mission.MissionId, edgeId = step.EdgeId,
                                position = Vec(bot.Position), rawEntry = Vec(step.RawEntry),
                                activeFallbackTarget = Vec(physicalTarget),
                                pathStillComplete = rc49Observed && rc49HasCost,
                                currentPathCost = rc49Cost,
                                bestPathCost = step.Rc48IngressBestPathCost,
                                stalled = rc49Stalled || rc49NearStalled,
                                pathCostRegressed = rc49Regressed,
                                nativeAttemptRecorded = false,
                                bodyRelocated = false,
                                action = "retireUnproductiveCollisionFallbackAndYieldToExistingRouteRecovery"
                            });
                    }
                }
                if (step.Rc49FallbackRetired)
                {
                    // One existing Follow writer holds the failed step quiet
                    // until the route's pre-existing bounded recovery lease
                    // advances. Never return to the stale BotMover or emit a
                    // second physical producer within this Step.
                    if (TryEnterV2CurrentIngressScope(
                            bot, mission, step, "v2rc49RetiredFallbackPhysicalHold",
                            out FollowerMovementWriterAuthority.WriteScope rc49Scope, out _))
                    {
                        using (rc49Scope)
                        {
                            if (bot.Mover != null)
                            {
                                if (!step.Rc49StopPublished)
                                {
                                    bot.Mover.BotMoverStop("R1-F4D76A21E17E",@bot);
                                    step.Rc49StopPublished = true;
                                }
                                bot.Mover.SetBotMoverPause("R1-578E1922272F",@bot,true);
                                bot.Mover.SetBotMoverIsMoving("R1-41B674AAFDC2",@bot,false);
                                bot.Mover.BotMoverSprint("R1-E6E8F9F9FE09",@bot,false,false);
                            }
                        }
                    }
                    return true;
                }
            }
            Vector3 flat = physicalTarget - bot.Position;
            flat.y = 0f;
            float distance = flat.magnitude;
            // RC47: after the once-only perpendicular pre-stage, the same Follow
            // writer first turns the body toward the original Climb axis. A valid
            // NavMesh path to the stage alone is NOT native readiness. Do not let
            // the next GoToPoint re-accelerate tangentially while the body is still
            // looking along the wall; if the turn is unresolved, hold only the
            // existing mover and steering input, not a new state machine.
            if (step.Rc47ClimbStageSelected && step.Rc47ClimbStageReached &&
                !step.Rc47ClimbStageHeadingReady)
            {
                Vector3 rc47Axis = GetFlatDirection(step.RawExit - step.RawEntry);
                Vector3 rc47Facing = GetFlatDirection(
                    player.MovementContext.PlayerRealForward);
                float rc47Angle = rc47Facing.sqrMagnitude > 0.5f &&
                    rc47Axis.sqrMagnitude > 0.5f
                    ? Vector3.Angle(rc47Facing, rc47Axis) : 180f;
                if (rc47Angle > InjectedNativeVaultEntryMaxAlignmentAngle)
                {
                    if (!TryEnterV2CurrentIngressScope(
                            bot, mission, step, "v2rc47ClimbStageTurnSameFollowWriter",
                            out FollowerMovementWriterAuthority.WriteScope rc47Scope, out _))
                        return false;
                    using (rc47Scope)
                    {
                        if (bot.Mover != null)
                        {
                            bot.Mover.SetBotMoverPause("R1-F37F49D3633E",@bot,true);
                            bot.Mover.SetBotMoverIsMoving("R1-7BA406676F35",@bot,false);
                        }
                        try { bot.Steering?.BotSteeringLookToDirection("R1-6B19CB02FC87",@bot,rc47Axis); } catch { }
                    }
                    if (ShouldEmitDiagnostic("v2rc47ClimbNormalStageHeading:" +
                            mission.MissionId.ToString(CultureInfo.InvariantCulture) + ":" +
                            step.EdgeId.ToString(CultureInfo.InvariantCulture), bot, 0.25f))
                        WriteEvent("v2rc47ClimbNormalStageHeading", bot, new
                        {
                            missionId = mission.MissionId, edgeId = step.EdgeId,
                            angle = rc47Angle, maxAngle = InjectedNativeVaultEntryMaxAlignmentAngle,
                            translationSuppressed = true, nativeAdmissionGranted = false,
                            action = "existingFollowWriterAlignsBeforeFinalNormalIngress"
                        });
                    return true;
                }
                step.Rc47ClimbStageHeadingReady = true;
            }
            if (distance <= 0.001f)
            {
                return true;
            }

            // Before an actuator probe exists, Follow owns the approach epoch. Reuse that existing
            // writer domain rather than minting a Traversal generation early; the normal probe start
            // still performs the authoritative Follow->Traversal handoff once readiness is true.
            Vector3 direction = flat / distance;
            // RC43: V379 previously steered *through* an obstacle toward the
            // fixed staging point, ignoring the current body's local complete
            // NavMesh ingress corners. This produced the repeated off-axis
            // Climb orbit with a perfectly stable V363 target (Mission 94).
            // Use only the first forward-compatible corner of the exact
            // current-raid lane; movement stays in the SAME Follow writer.
            bool rc43StrictCornerUsed = false;
            float rc43StrictPathLength = 0f;
            Vector3 rc43Corner = physicalTarget;
            if (string.Equals(step.Kind, "Climb", StringComparison.OrdinalIgnoreCase) &&
                !preserveExecutorHeadingAtPositionFrontier &&
                !string.IsNullOrEmpty(laneSource) &&
                laneSource.StartsWith("currentRaidBreadcrumb", StringComparison.Ordinal) &&
                (laneSource.IndexOf(":navMeshLane", StringComparison.Ordinal) >= 0 ||
                 step.Rc48IngressFallbackActive) &&
                distance > 0.35f && distance <= RouteMissionTraversalAdjacentBridgeMaxDistance)
            {
                StrictPathResult rc43Path = CalculateStrictPath(bot.Position, physicalTarget);
                if (rc43Path?.StrictComplete == true &&
                    rc43Path.Corners != null && rc43Path.Corners.Length > 1 &&
                    rc43Path.PathLength >= distance - 0.05f &&
                    rc43Path.PathLength <= RouteMissionTraversalAdjacentBridgeMaxDistance)
                {
                    rc43StrictPathLength = rc43Path.PathLength;
                    foreach (Vector3 corner in rc43Path.Corners)
                    {
                        Vector3 toCorner = corner - bot.Position;
                        toCorner.y = 0f;
                        float cornerDistance = toCorner.magnitude;
                        if (!IsFinite(corner) || cornerDistance < 0.18f ||
                            cornerDistance > RouteMissionTraversalAdjacentBridgeMaxDistance)
                            continue;
                        Vector3 cornerDirection = toCorner / cornerDistance;
                        if (Vector3.Dot(cornerDirection, direction) < -0.10f)
                            continue;
                        direction = cornerDirection;
                        rc43Corner = corner;
                        rc43StrictCornerUsed = true;
                        break;
                    }
                }
            }
            if (ShouldEmitDiagnostic("v2rc43ClimbIngressPhysicalCorner:" +
                    mission.MissionId.ToString(CultureInfo.InvariantCulture) + ":" +
                    step.EdgeId.ToString(CultureInfo.InvariantCulture), bot, 0.50f) &&
                string.Equals(step.Kind, "Climb", StringComparison.OrdinalIgnoreCase))
            {
                WriteEvent("v2rc43ClimbIngressPhysicalCorner", bot, new
                {
                    missionId = mission.MissionId, stepIndex = mission.CurrentStepIndex,
                    edgeId = step.EdgeId, position = Vec(bot.Position),
                    target = Vec(physicalTarget), corner = Vec(rc43Corner),
                    strictCornerUsed = rc43StrictCornerUsed,
                    strictPathLength = rc43StrictPathLength,
                    preserveExecutorHeadingAtPositionFrontier,
                    bodyRelocated = false, traversalSuccessRecorded = false,
                    action = "sameFollowWriterUsesCurrentBodyNavMeshIngressCornerOrPreservesOriginal"
                });
            }
            // The V475 collision writer may be used only when a CURRENT strict
            // corner proves a grounded path to the original entry.
            if (step.Rc48IngressFallbackActive && !rc43StrictCornerUsed &&
                distance > 0.35f)
                return false;
            float dt = Mathf.Max(0.001f, Time.deltaTime);
            Vector3 before = bot.Position;
            float beforeDistance = distance;
            bool rc44NativeClimbIngressOwned = false;
            bool rc45ClimbPoseHandoffOwned = false;
            try
            {
                // V2-RC17: pre-probe actuator runup is still Follow-owned;
                // mint no owner until there is an actual physical move to commit.
                if (!TryEnterV2CurrentIngressScope(
                           bot, mission, step, "learningV379ActuatorPreProbePhysicalProducer",
                           out FollowerMovementWriterAuthority.WriteScope physicalScope, out _))
                    return false;
                using (physicalScope)
                {
                    if (bot.Mover != null)
                    {
                        if (step.Rc48IngressFallbackActive &&
                            string.Equals(step.Kind, "Climb", StringComparison.OrdinalIgnoreCase) &&
                            !step.Rc48MoverPathRetired)
                        {
                            // Cancel stale BotMover translation ONCE before
                            // returning to the V475 collision-active writer.
                            bot.Mover.BotMoverStop("R1-3A3481B816D8",@bot);
                            step.Rc48MoverPathRetired = true;
                        }
                        bot.Mover.SetBotMoverPause("R1-ACB2EDBBA3F9",@bot,false);
                        bot.Mover.LocalAvoidance?.GClass492DropOffset("R1-3CB6666393FA",@bot);
                    }
                    // RC45: RC44 got the physical body to the exact Climb entry
                    // (2 cm away) yet its heading remained 146 degrees from the
                    // authored native climb axis. The subsequent direction-only
                    // staging producer drove the body out of that entry again.
                    // Heading and translation are ONE Follow-owned pre-probe epoch:
                    // when the existing native positional envelope is satisfied,
                    // release sprint even if heading is not ready, retire local
                    // staging translation, and let EFT Steering turn the supported
                    // body to the ACTUAL executor direction. Only a real executor
                    // admission may promote the next Traversal attempt.
                    if (string.Equals(step.Kind, "Climb", StringComparison.OrdinalIgnoreCase) &&
                        !HasActiveTraversalProbe(bot) &&
                        TryGetLearningV424ActuatorExecutorAdmission(
                            bot, step,
                            out bool rc45ExecutorAdmissible,
                            out _, out float rc45EntryDistance,
                            out float rc45ForwardProjection,
                            out float rc45Lateral,
                            out float rc45MaxLateral,
                            out float rc45AlignmentAngle,
                            out float rc45MaxAlignmentAngle,
                            out float rc45ForwardLimit,
                            out float rc45TriggerDistance, out _) &&
                        !rc45ExecutorAdmissible &&
                        rc45EntryDistance <= rc45TriggerDistance &&
                        rc45Lateral <= rc45MaxLateral &&
                        // RC46: position acquisition is NOT native execution admission.
                        // The signed projection can be slightly behind the entry while the
                        // grounded body is already within the ORIGINAL native radius and
                        // lateral envelope. RC45's -0.04 gate rejected a witnessed
                        // 0.217 m entry acquisition at projection -0.131 m, so V379
                        // resumed translation and drove away before EFT could turn.
                        rc45ForwardProjection >= -rc45TriggerDistance &&
                        rc45ForwardProjection <= rc45ForwardLimit &&
                        player.MovementContext.IsGrounded &&
                        player.CurrentStateName != EPlayerState.Transition)
                    {
                        Vector3 rc45NativeAxis = GetFlatDirection(step.RawExit - step.RawEntry);
                        if (rc45NativeAxis.sqrMagnitude > 0.5f)
                        {
                            // CF13 had been waiting for ALIGNMENT before releasing
                            // sprint; with a live staging mover that is circular.
                            TryRequestCF13ClimbSprintRelease(bot, step.EdgeId,
                                "rc45ExactEntryPoseHandoff");
                            bool rc46NativeModeSettling =
                                bot.Mover?.Sprinting == true ||
                                player.MovementContext.IsSprintEnabled ||
                                player.CurrentStateName == EPlayerState.Sprint;
                            // Keep translation parked while either orientation OR
                            // sprint mode is unresolved. Otherwise a heading-ready
                            // but still-sprinting body is pushed out of the entry.
                            if (rc45AlignmentAngle > rc45MaxAlignmentAngle ||
                                rc46NativeModeSettling)
                            {
                                rc45ClimbPoseHandoffOwned = true;
                                if (ShouldEmitDiagnostic("v2rc46ClimbEntryPoseAcquired:" +
                                        mission.MissionId.ToString(CultureInfo.InvariantCulture) + ":" +
                                        step.EdgeId.ToString(CultureInfo.InvariantCulture), bot, 0.20f))
                                {
                                    WriteEvent("v2rc46ClimbEntryPoseAcquired", bot, new
                                    {
                                        missionId = mission.MissionId,
                                        edgeId = step.EdgeId,
                                        position = Vec(bot.Position),
                                        entry = Vec(step.RawEntry),
                                        entryDistance = rc45EntryDistance,
                                        triggerDistance = rc45TriggerDistance,
                                        lateral = rc45Lateral,
                                        forwardProjection = rc45ForwardProjection,
                                        alignmentAngle = rc45AlignmentAngle,
                                        modeSettling = rc46NativeModeSettling,
                                        nativeAdmissible = rc45ExecutorAdmissible,
                                        nativeSuccessRecorded = false,
                                        translationSuppressed = true,
                                        bodyRelocated = false,
                                        action = "separateSupportedEntryAcquisitionFromNativeAdmission"
                                    });
                                }
                                if (bot.Mover != null)
                                    bot.Mover.SetBotMoverPause("R1-F41CD9E36514",@bot,true);
                                try { bot.Steering?.BotSteeringLookToDirection("R1-FB52AFB4F4BD",@bot,rc45NativeAxis); } catch { }
                                if (ShouldEmitDiagnostic("v2rc45ClimbExactEntryPoseHandoff:" +
                                        mission.MissionId.ToString(CultureInfo.InvariantCulture) + ":" +
                                        step.EdgeId.ToString(CultureInfo.InvariantCulture), bot, 0.20f))
                                {
                                    WriteEvent("v2rc45ClimbExactEntryPoseHandoff", bot, new
                                    {
                                        missionId = mission.MissionId,
                                        stepIndex = mission.CurrentStepIndex,
                                        edgeId = step.EdgeId,
                                        position = Vec(bot.Position),
                                        rawEntry = Vec(step.RawEntry),
                                        nativeAxis = Vec(rc45NativeAxis),
                                        entryDistance = rc45EntryDistance,
                                        lateral = rc45Lateral,
                                        forwardProjection = rc45ForwardProjection,
                                        alignmentAngle = rc45AlignmentAngle,
                                        maxAlignmentAngle = rc45MaxAlignmentAngle,
                                        sprintReleaseRequested = true,
                                        translationSuppressed = true,
                                        traversalSuccessRecorded = false,
                                        bodyRelocated = false,
                                        action = "existingFollowWriterYieldsStagingTranslationWhileEFTSteersExactClimbPose"
                                    });
                                }
                            }
                        }
                    }
                    // RC44: RC43 proved a StrictComplete Climb path on 15 samples,
                    // but still issued direction-only Player.Move. Its measured body
                    // repeatedly orbited a straight, connected staging destination.
                    // Let the existing EFT BotMover own BOTH the PathComplete path and
                    // the native steering/input until the original actuator position
                    // envelope is reached. Do not simultaneously issue V379 Player.Move.
                    // Preserve the old collision-active producer when NavMesh is absent,
                    // the path is incomplete or native GoToPoint rejects the destination.
                    if (rc43StrictCornerUsed &&
                        string.Equals(step.Kind, "Climb", StringComparison.OrdinalIgnoreCase) &&
                        !step.Rc48IngressFallbackActive &&
                        !preserveExecutorHeadingAtPositionFrontier && bot.Mover != null &&
                        distance > 0.35f)
                    {
                        // RC48: destination identity and owning RouteStep epoch,
                        // rather than first-corner heading, determine reuse. An
                        // EFT mover may turn at real corners without losing its
                        // currently commanded destination.
                        bool sameForwardMover = step.Rc48MoverCommandAccepted &&
                            GetPlanarDistance(step.Rc48MoverCommandTarget, physicalTarget) <= 0.10f &&
                            bot.Mover.HasPathAndNoComplete && !bot.Mover.Pause;
                        NavMeshPathStatus rc44PathStatus = NavMeshPathStatus.PathInvalid;
                        if (sameForwardMover)
                        {
                            rc44NativeClimbIngressOwned = true;
                        }
                        else
                        {
                            // EFT BotMover owns the ENTIRE validated path to the
                            // route target, rather than a changing first corner.
                            rc44PathStatus = bot.BotOwnerGoToPoint("R1-EC95F805D2A9",@bot,physicalTarget,true,-1f,false,false);
                            rc44NativeClimbIngressOwned = rc44PathStatus == NavMeshPathStatus.PathComplete;
                            step.Rc48MoverCommandAccepted = rc44NativeClimbIngressOwned;
                            if (rc44NativeClimbIngressOwned)
                                step.Rc48MoverCommandTarget = physicalTarget;
                        }
                        if (rc44NativeClimbIngressOwned)
                        {
                            bot.Mover.SetBotMoverPause("R1-CC7D9927EE09",@bot,false);
                            if (ShouldEmitDiagnostic("v2rc44ClimbMoverIngress:" +
                                    mission.MissionId.ToString(CultureInfo.InvariantCulture) + ":" +
                                    step.EdgeId.ToString(CultureInfo.InvariantCulture), bot, 0.40f))
                            {
                                WriteEvent("v2rc44ClimbNativeMoverIngress", bot, new
                                {
                                    missionId = mission.MissionId, stepIndex = mission.CurrentStepIndex,
                                    edgeId = step.EdgeId, position = Vec(bot.Position),
                                    nativeTarget = Vec(physicalTarget),
                                    navMeshStrictComplete = true,
                                    pathStatus = sameForwardMover ? "ExistingPathComplete" : rc44PathStatus.ToString(),
                                    nativeMoverOwned = true, directPlayerMoveIssued = false,
                                    traversalSuccessRecorded = false, bodyRelocated = false,
                                    action = "existingFollowWriterDelegatesCompleteClimbIngressToNativeBotMover"
                                });
                            }
                        }
                    }
                    // V2-RC40: the pre-probe Follow epoch owns both the physical
                    // target and the corresponding approach heading.  Previously
                    // the native heading was requested only AFTER the position
                    // envelope was reached; outside it, a collision-active body
                    // could translate while EFT steering followed an unrelated
                    // heading and orbit the same fixed lane target (Climb Edge 25).
                    // Align to the active staging producer's own forward direction,
                    // not to raw entry, and let existing actuator admission switch
                    // to its authored raw-exit heading inside the ready envelope.
                    // No heading-only movement lease, new controller or cross-wall
                    // route is created here; the Follow write scope is already live.
                    // V2-RC41: actuator-position readiness and actuator-heading readiness
                    // are DIFFERENT facts. The V3.63 admission producer has already
                    // steered toward RawExit-RawEntry inside the position envelope.
                    // RC40 accidentally overwrote that final heading with the short
                    // staging waypoint, leaving Climb spinning at the entry plane.
                    // Keep translation on the SAME Follow writer and SAME target;
                    // only the final heading source changes once position is ready.
                    if (!learningV448JumpOpportunityProducer &&
                        !preserveExecutorHeadingAtPositionFrontier &&
                        !rc44NativeClimbIngressOwned && !rc45ClimbPoseHandoffOwned &&
                        direction.sqrMagnitude > 0.5f)
                    {
                        try { bot.Steering?.BotSteeringLookToDirection("R1-356D59454298",@bot,direction); } catch { }
                    }
                    bool learningV448PreserveJumpSprint =
                        learningV448JumpOpportunityProducer &&
                        (bot.Mover?.Sprinting == true ||
                         player.CurrentStateName == EPlayerState.Sprint);
                    if (!rc44NativeClimbIngressOwned && !rc45ClimbPoseHandoffOwned)
                    {
                        // RC49: the RC48 collision fallback got within ~0.45m of
                        // the exact entry but the next movement interval overshot
                        // its narrow native envelope. Modulate ONLY the already
                        // existing fallback writer by current remaining distance;
                        // this is stopping-distance control, not an expanded native
                        // trigger or an extra movement producer. Normal V475-style
                        // locomotion and all other action kinds are untouched.
                        float rc49FallbackEntrySpeed =
                            step.Rc48IngressFallbackActive &&
                            string.Equals(step.Kind, "Climb", StringComparison.OrdinalIgnoreCase)
                                ? Mathf.Clamp(distance / 0.90f, 0.55f, 1f) : 1f;
                        ApplyCollisionActivePhysicalRouteMotion(
                            bot, player, direction,
                            direction * Mathf.Min(distance,
                                ChainLocalSuccessorBridgeSpeed * rc49FallbackEntrySpeed * dt),
                            dt, 1f,
                            sprint: learningV448PreserveJumpSprint,
                            targetMoveSpeed: learningV448PreserveJumpSprint
                                ? ProbeMomentumFullMoveSpeed
                                : ProbeMomentumNormalMoveSpeed * rc49FallbackEntrySpeed,
                            preserveSprintMode: learningV448JumpOpportunityProducer,
                            // RC52: the Climb collision fallback owns an exact world-space
                            // stage goal. Do not inherit an obsolete BotMover-local input
                            // vector after retiring the old native path.
                            exactWorldDirectionInput: step.Rc48IngressFallbackActive &&
                                string.Equals(step.Kind, "Climb", StringComparison.OrdinalIgnoreCase),deliveryTarget:physicalTarget,deliveryPurpose:CapabilityMotionPurpose.FollowIngress);
                    }
                }
            }
            catch (Exception ex)
            {
                WriteEvent("learningV379ActuatorPreProbePhysicalDriveException", bot, new
                {
                    missionId = mission.MissionId,
                    stepIndex = mission.CurrentStepIndex,
                    edgeId = step.EdgeId,
                    kind = step.Kind,
                    laneSource,
                    error = ex.GetType().Name + ": " + ex.Message
                });
                return true;
            }

            Vector3 after = bot.Position;
            bodyDisplacement = GetPlanarDistance(before, after);
            targetClosure = beforeDistance - GetPlanarDistance(after, physicalTarget);
            if (bodyDisplacement >= LearningV350MeasuredPhysicalProgressDistance ||
                targetClosure >= LearningV350MeasuredTargetProgressDistance)
            {
                mission.LastProgressAt = Time.time;
                mission.LastProgressPosition = after;
                mission.ApproachDeferredSince = 0f;
                mission.ApproachDeferredCount = 0;
                mission.ApproachDeferredLastEntryDistance = float.MaxValue;
            }

            if (ShouldEmitDiagnostic(
                    "learningV379ActuatorPreProbePhysicalProducer:" +
                        mission.MissionId.ToString(CultureInfo.InvariantCulture) + ":" +
                        step.EdgeId.ToString(CultureInfo.InvariantCulture),
                    bot, 0.18f))
            {
                WriteEvent("learningV379ActuatorPreProbePhysicalProducer", bot, new
                {
                    missionId = mission.MissionId,
                    stepIndex = mission.CurrentStepIndex,
                    episodeId = step.EpisodeId,
                    edgeId = step.EdgeId,
                    kind = step.Kind,
                    laneSource,
                    target = Vec(physicalTarget),
                    before = Vec(before),
                    after = Vec(after),
                    bodyDisplacement,
                    targetClosure,
                    nativeMoverOwned = rc44NativeClimbIngressOwned,
                    rc45ClimbPoseHandoffOwned,
                    directPlayerMoveIssued = !rc44NativeClimbIngressOwned && !rc45ClimbPoseHandoffOwned,
                    traversalSuccessRecorded = false,
                    persistentLearningChanged = false,
                    action = rc45ClimbPoseHandoffOwned
                        ? "existingFollowWriterOwnsClimbPoseWithoutNewTranslation"
                        : (rc44NativeClimbIngressOwned
                            ? "completeNativeMoverPathOwnsClimbIngressUntilOriginalActuatorReadiness"
                            : "collisionActiveFollowProducerOwnsFinalActuatorStagingUntilNativeReadiness")
                });
            }
            return true;
        }
    }
}
