// R3V_OBSTACLE_PACKAGE_REVIEWED
using System;
using System.Linq;
using EFT;
using EFT.Vaulting;
using System.Reflection;
using System.Globalization;
using System.Collections.Generic;
using UnityEngine.AI;
using UnityEngine;
namespace BotTraversal.Core
{
    internal static partial class FollowerTraversalTestRuntime
    {
        private static bool TryBeginInjectedNativeVault(
            ProbeRuntime probe,
            LearnedTraversalCommitment commitment,
            NativeVaultParameters nativeParameters,
            string source,
            out string failure)
        {
            failure = string.Empty;
            if (probe == null || probe.Player?.MovementContext == null || nativeParameters == null)
            {
                failure = "native vault parameters are unavailable";
                return false;
            }

            if (IsLearningV319TransientSkillTransfer(commitment))
            {
                LearnedTraversalEdge skillTemplate = LearnedTraversalEdges.FirstOrDefault(candidate =>
                    candidate != null && candidate.Id == commitment.SkillTemplateEdgeId);
                nativeParameters = CreateLearningV319TransferredNativeVaultParameters(skillTemplate, commitment);
                if (nativeParameters == null)
                {
                    failure = "zero-shot native vault parameters could not be transformed";
                    return false;
                }
            }

            if (!TryParseNativeVaultStrategy(nativeParameters.Strategy, out EVaultingStrategy strategy))
            {
                strategy = string.Equals(commitment.Kind, "Climb", StringComparison.OrdinalIgnoreCase)
                    ? EVaultingStrategy.Climb
                    : EVaultingStrategy.Vault;
            }

            float height = nativeParameters.VaultingHeight;
            float length = nativeParameters.VaultingLength;
            float behindObstacleRatio = IsFinite(nativeParameters.BehindObstacleRatio)
                ? nativeParameters.BehindObstacleRatio
                : 0f;
            float absoluteForwardVelocity = IsFinite(nativeParameters.AbsoluteForwardVelocity)
                ? Mathf.Max(0f, nativeParameters.AbsoluteForwardVelocity)
                : 0f;
            if (!IsFinite(height) || !IsFinite(length) || height <= 0.01f || length <= 0.01f)
            {
                failure = $"native vault geometry is invalid height={height:0.000} length={length:0.000}";
                return false;
            }

            if (!IsActiveProbe(probe) || !TryGetLearnedCommitmentForProbe(probe,out var currentCommitment) ||
                (currentCommitment.EdgeId!=commitment.EdgeId || currentCommitment.RawEntry!=commitment.RawEntry || currentCommitment.RawExit!=commitment.RawExit))
            {
                failure = "native obstacle capability origin retired or replaced";
                return false;
            }
            if (probe.NativeExecutorConsumed)
            {
                failure = "native Vault/Climb executor is single-shot and was already consumed for this probe";
                WriteEvent("nativeTraversalDuplicateTriggerBlocked", probe.Bot, new
                {
                    edgeId = commitment.EdgeId,
                    source,
                    kind = commitment.Kind,
                    stateObserved = probe.NativeStateObserved,
                    position = Vec(probe.Player.Position)
                });
                return false;
            }
            if ((!probe.LearningV32NativeEntryAcquired))
            {
                return TryBeginLearningV32NativeEntryAcquisition(probe, commitment, source, out failure);
            }
            if(FollowerCapabilityPassagePolicy.RescueScanRequired(IsLearningV319TransientSkillTransfer(commitment),
                HasCapabilityFailureStrategy(probe.Bot),probe.LifecycleMission?.FailureSequence?.Stage==FollowerCapabilityFailureSequence.Stage.SelfRescue)&&
                !TryObserveCapabilityRescueObstacle(probe.Bot,
                GetFlatDirection(commitment.RawExit-commitment.RawEntry),out failure))return false;
            probe.NativeExecutorConsumed = true;

            Vector3 rawEntry = IsFinite(commitment.RawEntry) ? commitment.RawEntry : commitment.Entry;
            Vector3 rawExit = IsFinite(commitment.RawExit) ? commitment.RawExit : commitment.Exit;
            Vector3 direction = GetFlatDirection(rawExit - rawEntry);
            if (direction.sqrMagnitude < 0.5f)
            {
                direction = probe.RequestedDirection;
            }

            // V3.65 telemetry-only coverage for visually skewed Vault/Climb execution. Capture the
            // physical body pose at actuator trigger so a future "angle looked wrong" report can be
            // reconciled without scene-name hardcodes or changing the actuator tolerances.
            Vector3 learningV365ActuatorBodyForward = GetFlatDirection(
                probe.Player.MovementContext?.PlayerRealForward ?? probe.Player.LookDirection);
            float learningV365ActuatorAlignmentAngle =
                FollowerCapabilityDecisionRules.TryBeginInjectedNativeVault_learningV365ActuatorAlignmentAngle_1(direction.sqrMagnitude,learningV365ActuatorBodyForward.sqrMagnitude,Vector3.Angle(learningV365ActuatorBodyForward, direction));
            Vector3 learningV365ActuatorEntryOffset = probe.Player.Position - rawEntry;
            learningV365ActuatorEntryOffset.y = 0f;
            float learningV365ActuatorForwardProjection = direction.sqrMagnitude > 0.5f
                ? Vector3.Dot(learningV365ActuatorEntryOffset, direction)
                : 0f;
            Vector3 learningV365ActuatorLateral = direction.sqrMagnitude > 0.5f
                ? learningV365ActuatorEntryOffset - direction * learningV365ActuatorForwardProjection
                : learningV365ActuatorEntryOffset;
            float learningV365ActuatorLateralOffset = learningV365ActuatorLateral.magnitude;

            try
            {
                RestoreAssistedObstacleCollisionLease(probe, "injectedNativeVault");
                RestoreAssistedTraversalAnimation(probe);

                probe.InjectedNativeVaultAttempted = true;
                probe.InjectedNativeVaultParameters = CloneNativeVaultParameters(nativeParameters);
                probe.InjectedNativeVaultTriggeredAt = Time.time;
                {
                    probe.LearningV382NativeActuatorPendingBodyObserved = false;
                    probe.LearningV382NativeActuatorPendingBestProjection = 0f;
                    probe.LearningV382NativeActuatorPendingLastProgressAt = Time.time;
                }
                TransitionProbeStage(probe, ProbeStage.WaitingForState);
                probe.StageStartedAt = Time.time;
                probe.ExclusiveMoverGate = true;
                probe.TraversalKind = strategy == EVaultingStrategy.Climb ? "Climb" : "Vault";
                probe.TriggerPosition = probe.Player.Position;
                BeginLearningV32NativeExecutionEpoch(probe, probe.TraversalKind, source);
                probe.SawVaultState = false;
                probe.NativeTraversalStarted = false;

                probe.Player.SetPlayerInputDirection("R1-9A2D773B59FE",@probe?.Bot,Vector2.zero);
                probe.Player.MovementContext.SetMovementContextMovementDirection("R1-F56242448EC1",@probe?.Bot,Vector2.zero);
                probe.Bot?.Mover?.BotMoverStop("R1-747C689ABE03",@probe?.Bot);
                probe.Bot?.Mover?.LocalAvoidance?.GClass492DropOffset("R1-F09BD8F4AEE9",@probe?.Bot);
                probe.Bot?.BotOwnerStopMove("R1-DEF5470015F4",@probe?.Bot);
                if (probe.Bot?.Mover != null)
                {
                    probe.Bot.Mover.SetBotMoverPause("R1-685E42494A97",@probe?.Bot,false);
                    probe.Bot.Mover.BotMoverSprint("R1-9A5BFD1571CF",@probe?.Bot,false,false);
                }
                if (direction.sqrMagnitude > 0.5f)
                {
                    probe.RequestedDirection = direction;
                    probe.Bot?.Steering?.BotSteeringLookToDirection("R1-166F313EAD7D",@probe?.Bot,direction);
                }

                MovementContext movement = probe.Player.MovementContext;
                movement.MovementContextPlayerAnimatorSetObstacleHeight("R1-20F9355FE5F0",@probe?.Bot,height);
                movement.MovementContextPlayerAnimatorSetObstacleLength("R1-A2574ED76057",@probe?.Bot,length);
                movement.MovementContextPlayerAnimatorSetBehindObstacleRatio("R1-36F5530497DE",@probe?.Bot,behindObstacleRatio);
                movement.MovementContextPlayerAnimatorSetAbsoluteForwardVelocity("R1-2FCA2FA7FB41",@probe?.Bot,absoluteForwardVelocity);

                // Do not copy DemonstratedVaultingSpeed here. GClass2137.Enter -> OnVault/OnClimb
                // recalculates the speed from this follower's own weight and physical state.
                if (strategy == EVaultingStrategy.Climb)
                {
                    movement.MovementContextPlayerAnimatorSetDoVault("R1-82EC532E353D",@probe?.Bot,false);
                    movement.MovementContextPlayerAnimatorSetDoClimb("R1-6E8E101CE77F",@probe?.Bot,true);
                }
                else
                {
                    movement.MovementContextPlayerAnimatorSetDoClimb("R1-D7F7E27C2FE8",@probe?.Bot,false);
                    movement.MovementContextPlayerAnimatorSetDoVault("R1-7EF16F095B4E",@probe?.Bot,true);
                }

                // TryVaulting() normally performs this handoff before the animator bool is set.
                // Injected Vault intentionally skips the scanner/TryVaulting path, so reproduce the
                // non-scanner side effect explicitly and publish transport-neutral network intent.
                probe.Player.PlayerRemoveLeftHandItem("R1-0913BFCCC600",@probe?.Bot,2f);
                FollowerNativeVaultNetworkBridge.Publish(new FollowerNativeVaultNetworkBridge.VaultEvent
                {
                    Player = probe.Player,
                    ProfileId = probe.Player.ProfileId,
                    Strategy = strategy,
                    VaultingPoint = IsFinite(nativeParameters.VaultingPoint)
                        ? nativeParameters.VaultingPoint
                        : probe.Player.Position,
                    VaultingHeight = height,
                    VaultingLength = length,
                    DemonstratedVaultingSpeed = nativeParameters.DemonstratedVaultingSpeed,
                    BehindObstacleRatio = behindObstacleRatio,
                    AbsoluteForwardVelocity = absoluteForwardVelocity,
                    EdgeId = commitment.EdgeId,
                    TriggeredAt = Time.time
                });

                WriteEvent("injectedNativeVaultTriggered", probe.Bot, new
                {
                    edgeId = commitment.EdgeId,
                    source,
                    strategy = strategy.ToString(),
                    rawEntry = Vec(rawEntry),
                    rawExit = Vec(rawExit),
                    height,
                    length,
                    demonstratedSpeed = nativeParameters.DemonstratedVaultingSpeed,
                    behindObstacleRatio,
                    absoluteForwardVelocity,
                    position = Vec(probe.Player.Position)
                });
                WriteEvent("learningV365ActuatorExecutionAlignmentSample", probe.Bot, new
                {
                    edgeId = commitment.EdgeId,
                    kind = commitment.Kind,
                    strategy = strategy.ToString(),
                    position = Vec(probe.Player.Position),
                    rawEntry = Vec(rawEntry),
                    rawExit = Vec(rawExit),
                    authoredDirection = Vec(direction),
                    bodyForward = Vec(learningV365ActuatorBodyForward),
                    alignmentAngle = learningV365ActuatorAlignmentAngle,
                    forwardEntryProjection = learningV365ActuatorForwardProjection,
                    lateralEntryOffset = learningV365ActuatorLateralOffset,
                    source,
                    action = "telemetryOnlyNoActuatorThresholdOrPoseMutation"
                });
                return true;
            }
            catch (Exception ex)
            {
                // The injected trigger may have partially succeeded before a later call failed.
                // Best-effort clear both animator transaction flags and release PIT movement ownership
                // so recovery can enter the quiescence/teleport path instead of leaving a stuck vault bool.
                try
                {
                    MovementContext movement = probe.Player?.MovementContext;
                    movement?.MovementContextPlayerAnimatorSetDoVault("R1-FB9F51A0676E",@probe?.Bot,false);
                    movement?.MovementContextPlayerAnimatorSetDoClimb("R1-B7C667A1699D",@probe?.Bot,false);
                }
                catch
                {
                    // Recovery below is deliberately fail-open.
                }
                try
                {
                    if (probe.Bot?.Mover != null)
                    {
                        probe.Bot.Mover.SetBotMoverPause("R1-DFB2F0BAAA4D",@probe?.Bot,false);
                        probe.Bot.Mover.BotMoverSprint("R1-E66F7FE97EA2",@probe?.Bot,false,false);
                    }
                }
                catch
                {
                }
                probe.ExclusiveMoverGate = false;
                probe.InjectedNativeVaultAttempted = false;
                probe.InjectedNativeVaultParameters = null;
                failure = ex.GetType().Name + ": " + ex.Message;
                WriteEvent("injectedNativeVaultFailed", probe.Bot, new
                {
                    edgeId = commitment.EdgeId,
                    source,
                    strategy = strategy.ToString(),
                    error = failure
                });
                return false;
            }
        }

        private static bool UpdateLearningV382NativeActuatorPendingBodyOwnership(
            ProbeRuntime probe,
            out float forwardProjection,
            out float displacement,
            out float noProgressSeconds)
        {
            forwardProjection = 0f;
            displacement = 0f;
            noProgressSeconds = 0f;
            if ((probe?.Player == null) ||
                !probe.InjectedNativeVaultAttempted)
            {
                return false;
            }

            LearnedTraversalCommitment commitment = null;
            TryGetLearnedCommitmentForProbe(probe, out commitment);
            Vector3 rawEntry = commitment != null && IsFinite(commitment.RawEntry)
                ? commitment.RawEntry
                : probe.TriggerPosition;
            Vector3 rawExit = commitment != null && IsFinite(commitment.RawExit)
                ? commitment.RawExit
                : (probe.TriggerPosition + probe.RequestedDirection);
            Vector3 direction = GetFlatDirection(rawExit - rawEntry);
            if (direction.sqrMagnitude < 0.5f)
            {
                direction = GetFlatDirection(probe.RequestedDirection);
            }
            if (direction.sqrMagnitude < 0.5f)
            {
                return false;
            }

            Vector3 current = probe.Player.Position;
            Vector3 fromTrigger = current - probe.TriggerPosition;
            fromTrigger.y = 0f;
            forwardProjection = Vector3.Dot(fromTrigger, direction);
            displacement = fromTrigger.magnitude;

            bool forwardProgress =
                FollowerCapabilityHelperRules.UpdateLearningV382NativeActuatorPendingBodyOwnership_forwardProgress_8BE97B48_1(forwardProjection,probe.LearningV382NativeActuatorPendingBestProjection,LearningV32EntryProgressEpsilon);
            if (forwardProgress)
            {
                probe.LearningV382NativeActuatorPendingBestProjection =
                    Mathf.Max(probe.LearningV382NativeActuatorPendingBestProjection, forwardProjection);
                probe.LearningV382NativeActuatorPendingLastProgressAt = Time.time;
            }

            // Lateral drift cannot keep a rejected actuator alive.  Pending ownership is earned only
            // by the same forward-motion evidence already used by V3.2 entry acquisition.
            bool credibleBodyEvidence =
                probe.LearningV382NativeActuatorPendingBestProjection >= LearningV32EntryForwardMotionEvidence;
            if (credibleBodyEvidence && !probe.LearningV382NativeActuatorPendingBodyObserved)
            {
                probe.LearningV382NativeActuatorPendingBodyObserved = true;
                probe.LearningV382NativeActuatorPendingLastProgressAt = Time.time;
                WriteEvent("learningV382NativeActuatorPendingBodyOwnership", probe.Bot, new
                {
                    edgeId = probe.LearnedEdgeId,
                    kind = probe.TraversalKind ?? probe.RequestedTraversalKind,
                    forwardProjection,
                    displacement,
                    progressEpsilon = LearningV32EntryProgressEpsilon,
                    forwardMotionEvidence = LearningV32EntryForwardMotionEvidence,
                    existingNoProgressBudget = InjectedNativeVaultEntryNoProgressTimeout,
                    state = probe.Player.CurrentStateName.ToString(),
                    action = "physicalBodyMotionProvesTriggeredNativeActuatorStillPendingDoNotDuplicateTrigger"
                });
            }

            noProgressSeconds = probe.LearningV382NativeActuatorPendingLastProgressAt >= 0f
                ? Mathf.Max(0f, Time.time - probe.LearningV382NativeActuatorPendingLastProgressAt)
                : 0f;
            return probe.LearningV382NativeActuatorPendingBodyObserved &&
                   noProgressSeconds < InjectedNativeVaultEntryNoProgressTimeout;
        }

        private static bool IsNativeVaultDirectionCompatible(
            LearnedTraversalEdge edge,
            LearnedTraversalCommitment commitment)
        {
            if (edge == null || commitment == null)
            {
                return false;
            }
            if (IsLearningV319TransientSkillTransfer(commitment))
            {
                return true;
            }

            Vector3 authored = GetFlatDirection(edge.RawEnd - edge.RawStart);
            Vector3 oriented = GetFlatDirection(commitment.RawExit - commitment.RawEntry);
            if (authored.sqrMagnitude < 0.5f || oriented.sqrMagnitude < 0.5f)
            {
                return false;
            }
            return Vector3.Dot(authored, oriented) >= 0.65f;
        }

        private static bool TryBeginPartialNativeVaultCompletion(
            ProbeRuntime probe,
            LearnedTraversalCommitment commitment,
            string source,
            out string failure)
        {
            failure = string.Empty;
            if (probe == null || commitment == null || probe.Player?.CharacterController == null ||
                probe.AssistedPartialNativeContinuation)
            {
                failure = "partial native completion unavailable";
                return false;
            }

            LearnedTraversalEdge edge = LearnedTraversalEdges.FirstOrDefault(candidate =>
                candidate != null && candidate.Id == commitment.EdgeId);
            LearnedTraversalEdge collisionEdge = IsLearningV319TransientSkillTransfer(commitment) ? null : edge;
            Vector3 rawEntry = IsFinite(commitment.RawEntry) ? commitment.RawEntry : commitment.Entry;
            Vector3 rawExit = IsFinite(commitment.RawExit) ? commitment.RawExit : commitment.Exit;
            Vector3 current = probe.Player.Position;
            Vector3 runOutExit = commitment.HasSettledExit && IsFinite(commitment.SettledExit)
                ? commitment.SettledExit
                : rawExit;
            Vector3 axis = rawExit - rawEntry;
            axis.y = 0f;
            float length = axis.magnitude;
            if (!IsFinite(current) || !IsFinite(rawEntry) || !IsFinite(rawExit) ||
                !IsFinite(runOutExit) || length < 0.35f)
            {
                failure = "partial native endpoints invalid";
                return false;
            }

            Vector3 direction = axis / length;
            Vector3 fromEntry = current - rawEntry;
            fromEntry.y = 0f;
            float projection = Vector3.Dot(fromEntry, direction);
            float requiredProjection = length - NativeTraversalRunOutFarSideSlack;
            float progressRatio = projection / Mathf.Max(0.25f, length);
            if (string.Equals(commitment.Kind, "Vault", StringComparison.OrdinalIgnoreCase) &&
                progressRatio < VaultPartialNativeMinimumProgressRatio)
            {
                failure = $"partial native progress below semantic threshold ratio={progressRatio:0.00} required={VaultPartialNativeMinimumProgressRatio:0.00}";
                return false;
            }
            if (projection <= 0.10f || projection >= requiredProjection)
            {
                failure = $"partial native state not in continuation envelope projection={projection:0.00}/{requiredProjection:0.00}";
                return false;
            }

            float remainingCross = GetPlanarDistance(current, rawExit);
            if (remainingCross < 0.18f || remainingCross > 3.25f)
            {
                failure = $"partial native remaining crossing rejected distance={remainingCross:0.00}m";
                return false;
            }

            // The only collision suppressed here is the demonstrated obstacle fingerprint. This
            // lets the already-started native motion finish crossing the same barrier without
            // granting the follower world-geometry noclip.
            if (!TryAcquireAssistedObstacleCollisionLease(
                    probe, collisionEdge, current, rawExit, "partialNativeFarSideCompletion"))
            {
                failure = "partial native obstacle collision lease unavailable";
                return false;
            }

            float replaySpeed = probe.DemonstratedApproachSpeed > 0.50f
                ? Mathf.Clamp(probe.DemonstratedApproachSpeed, 1.35f, 4.25f)
                : AssistedReplayMotionSpeed;
            float crossDuration = Mathf.Clamp(
                remainingCross / Mathf.Max(1.35f, replaySpeed),
                0.18f,
                0.70f);
            float runOutDistance = GetPlanarDistance(rawExit, runOutExit);
            float runOutDuration = runOutDistance <= 0.10f
                ? 0f
                : Mathf.Clamp(
                    runOutDistance / AssistedRunOutSpeed,
                    AssistedRunOutMinSeconds,
                    AssistedRunOutMaxSeconds);

            probe.AssistedReplayAttempted = true;
            probe.AssistedStrategySelected = true;
            probe.AssistedPartialNativeContinuation = true;
            TransitionProbeStage(probe, ProbeStage.AssistedReplay);
            probe.StageStartedAt = Time.time;
            probe.ExclusiveMoverGate = true;
            probe.AssistedReplayStart = current;
            probe.AssistedReplayRawEntry = current;
            probe.AssistedReplayRawExit = rawExit;
            probe.AssistedReplayRunOutExit = runOutExit;
            probe.AssistedReplayExit = runOutExit;
            probe.AssistedLeadInDuration = 0f;
            probe.AssistedCrossDuration = crossDuration;
            probe.AssistedRunOutDuration = runOutDuration;
            probe.AssistedReplayDuration = crossDuration + runOutDuration;
            probe.AssistedMotionSamples = BuildLinearTraversalMotionFallback(
                current, rawExit, crossDuration);
            probe.AssistedReplayLastDistance = Vector3.Distance(current, runOutExit);
            probe.AssistedReplayLastProgressAt = Time.time;
            probe.AssistedReplayLastObservedPosition = current;
            probe.AssistedReplayActualDisplacement = 0f;
            probe.AssistedReplayPhase = "Cross";
            probe.AssistedDivergenceSince = -1f;
            probe.AssistedCollisionRecoveryAttempted = true;
            probe.TraversalKind = string.IsNullOrWhiteSpace(commitment.Kind)
                ? probe.RequestedTraversalKind
                : commitment.Kind;
            probe.TriggerPosition = current;
            probe.RequestedDirection = direction;

            try
            {
                probe.Bot.Mover?.BotMoverStop("R1-8BAB5642BCC5",@probe?.Bot);
                if (probe.Bot.Mover != null)
                {
                    probe.Bot.Mover.SetBotMoverPause("R1-634EAD03C930",@probe?.Bot,false);
                    probe.Bot.Mover.BotMoverSprint("R1-A746F3494390",@probe?.Bot,false,false);
                    probe.Bot.Mover.LocalAvoidance?.GClass492DropOffset("R1-09D4D49574D2",@probe?.Bot);
                }
                probe.Bot.Steering?.BotSteeringLookToDirection("R1-5464E019306F",@probe?.Bot,direction);
                HoldForwardInput(probe);
            }
            catch
            {
                // The collision-leased CharacterController replay remains authoritative.
            }

            WriteEvent("partialNativeFarSideCompletionStarted", probe.Bot, new
            {
                edgeId = commitment.EdgeId,
                kind = commitment.Kind,
                source,
                position = Vec(current),
                rawEntry = Vec(rawEntry),
                rawExit = Vec(rawExit),
                runOutExit = Vec(runOutExit),
                projection,
                requiredProjection,
                progressRatio,
                remainingCross,
                crossDuration,
                runOutDuration
            });
            return true;
        }

        private static bool TryBeginControlledVaultCrossingBridge(
            ProbeRuntime probe,
            LearnedTraversalCommitment commitment,
            string source,
            out string failure)
        {
            failure = string.Empty;
            if (probe == null || commitment == null || probe.Player?.CharacterController == null ||
                probe.VaultControlledBridgeActive || probe.VaultControlledBridgeAttempted ||
                !string.Equals(commitment.Kind, "Vault", StringComparison.OrdinalIgnoreCase))
            {
                failure = "controlled Vault bridge unavailable";
                return false;
            }

            LearnedTraversalEdge edge = LearnedTraversalEdges.FirstOrDefault(candidate => candidate != null && candidate.Id == commitment.EdgeId);
            LearnedTraversalEdge collisionEdge = IsLearningV319TransientSkillTransfer(commitment) ? null : edge;
            Vector3 rawEntry = IsFinite(commitment.RawEntry) ? commitment.RawEntry : commitment.Entry;
            Vector3 rawExit = IsFinite(commitment.RawExit) ? commitment.RawExit : commitment.Exit;
            Vector3 current = probe.Player.Position;
            Vector3 axis = rawExit - rawEntry;
            axis.y = 0f;
            float length = axis.magnitude;
            if (!IsFinite(current) || !IsFinite(rawEntry) || !IsFinite(rawExit) || length < 0.45f || length > 4.75f)
            {
                failure = $"controlled Vault bridge geometry rejected length={length:0.00}m";
                return false;
            }

            Vector3 direction = axis / length;
            Vector3 runOut = commitment.HasSettledExit && IsFinite(commitment.SettledExit)
                ? commitment.SettledExit
                : rawExit + direction * VaultControlledBridgeFarSideExtra;
            if (GetPlanarDistance(rawExit, runOut) > 3.0f)
            {
                runOut = rawExit + direction * VaultControlledBridgeFarSideExtra;
            }
            Vector3 crossTarget = rawExit + direction * VaultControlledBridgeFarSideExtra;
            crossTarget.y = rawExit.y + 0.08f;

            RestoreAssistedObstacleCollisionLease(probe, "controlledVaultBridgeReset");
            if (!TryAcquireAssistedObstacleCollisionLease(probe, collisionEdge, current, crossTarget, "controlledVaultCrossingBridge"))
            {
                failure = "controlled Vault bridge collision lease unavailable";
                return false;
            }

            probe.VaultControlledBridgeAttempted = true;
            probe.VaultControlledBridgeActive = true;
            probe.VaultControlledBridgeStartedAt = Time.time;
            probe.VaultControlledBridgeDeadlineAt = Time.time + VaultControlledBridgeMaxSeconds;
            probe.VaultControlledBridgeLastProgressAt = Time.time;
            probe.VaultControlledBridgeLastDistance = GetPlanarDistance(current, crossTarget);
            probe.VaultControlledBridgeCrossTarget = crossTarget;
            probe.VaultControlledBridgeRunOutTarget = runOut;
            probe.VaultControlledBridgePhase = "WaitNativeRelease";
            probe.VaultControlledBridgeNativeWaitLogged = false;
            probe.VaultControlledBridgeNextMotionTraceAt = Time.time;
            probe.AssistedReplayTerminalized = false;
            probe.AssistedNativeOwned = false;
            probe.AssistedPartialNativeContinuation = false;
            TransitionProbeStage(probe, ProbeStage.AssistedReplay);
            probe.StageStartedAt = Time.time;
            probe.ExclusiveMoverGate = true;
            probe.TraversalKind = "Vault";
            probe.RequestedDirection = direction;
            probe.TriggerPosition = current;

            try
            {
                probe.Player.MovementContext?.MovementContextPlayerAnimatorSetDoVault("R1-64C1703048FC",@probe?.Bot,false);
                probe.Player.MovementContext?.MovementContextPlayerAnimatorSetDoClimb("R1-A451A960D482",@probe?.Bot,false);
                probe.Bot.Mover?.BotMoverStop("R1-FA758DC08CDF",@probe?.Bot);
                if (probe.Bot.Mover != null)
                {
                    probe.Bot.Mover.SetBotMoverPause("R1-E0AE79AEC273",@probe?.Bot,true);
                    probe.Bot.Mover.BotMoverSprint("R1-4E8AB35963DC",@probe?.Bot,false,false);
                    probe.Bot.Mover.LocalAvoidance?.GClass492DropOffset("R1-CDA4BE403593",@probe?.Bot);
                }
                probe.Bot.Steering?.BotSteeringLookToDirection("R1-057B1D6EEAEB",@probe?.Bot,direction);
                probe.Player.SetPlayerInputDirection("R1-19A554977DB5",@probe?.Bot,Vector2.zero);
                probe.Player.MovementContext.SetMovementContextMovementDirection("R1-DEC4CE4435BE",@probe?.Bot,Vector2.zero);
            }
            catch { }

            WriteEvent("controlledVaultCrossingBridgeStarted", probe.Bot, new
            {
                edgeId = commitment.EdgeId,
                kind = commitment.Kind,
                source = source ?? "unknown",
                position = Vec(current),
                rawEntry = Vec(rawEntry),
                rawExit = Vec(rawExit),
                crossTarget = Vec(crossTarget),
                runOutTarget = Vec(runOut),
                deadlineSeconds = VaultControlledBridgeMaxSeconds
            });
            return true;
        }

        private static void UpdateControlledVaultCrossingBridge(ProbeRuntime probe)
        {
            if (probe == null || !probe.VaultControlledBridgeActive || !IsActiveProbe(probe) || probe.Player?.CharacterController == null)
            {
                return;
            }

            EPlayerState state = probe.Player.CurrentStateName;
            float elapsed = Time.time - probe.VaultControlledBridgeStartedAt;
            bool nativeOwnsBody = IsProbeTraversalState(state) || state == EPlayerState.JumpLanding;
            if (nativeOwnsBody)
            {
                try
                {
                    probe.Bot.Mover?.BotMoverStop("R1-21450937FD13",@probe?.Bot);
                    if (probe.Bot.Mover != null) probe.Bot.Mover.SetBotMoverPause("R1-879AD56D4DF3",@probe?.Bot,true);
                }
                catch { }
                if (!probe.VaultControlledBridgeNativeWaitLogged)
                {
                    probe.VaultControlledBridgeNativeWaitLogged = true;
                    WriteEvent("controlledVaultCrossingBridgeWaitingNativeRelease", probe.Bot, new
                    {
                        edgeId = probe.LearnedEdgeId,
                        state = state.ToString(),
                        elapsed
                    });
                }
                // CompileFix35 used `wait || before-deadline`, which accidentally waited the entire
                // bridge lifetime. Fix36 grants the native state only its bounded release window.
                if (elapsed < VaultControlledBridgeNativeReleaseWaitSeconds)
                {
                    return;
                }
                try
                {
                    probe.Player.MovementContext?.MovementContextPlayerAnimatorSetDoVault("R1-256CEBDCE8CA",@probe?.Bot,false);
                    probe.Player.MovementContext?.MovementContextPlayerAnimatorSetDoClimb("R1-5BD5F27CA73A",@probe?.Bot,false);
                }
                catch { }
            }

            if (Time.time >= probe.VaultControlledBridgeDeadlineAt)
            {
                RestoreAssistedObstacleCollisionLease(probe, "controlledVaultBridgeTimeout");
                probe.VaultControlledBridgeActive = false;
                FinalizeOrEscalateTraversalFailure(probe, "crossingNotFarSideBeforeEgress:controlledVaultBridgeTimeout");
                return;
            }

            if (!TryGetLearnedCommitmentForProbe(probe, out LearnedTraversalCommitment commitment) || commitment == null)
            {
                RestoreAssistedObstacleCollisionLease(probe, "controlledVaultBridgeCommitmentMissing");
                probe.VaultControlledBridgeActive = false;
                FinalizeUnifiedTraversalFailure(probe, "controlledVaultBridgeCommitmentMissing");
                return;
            }

            bool farSide = IsVaultClimbFarSideForEgress(probe, commitment, out float projection, out float requiredProjection);
            Vector3 target = farSide ? probe.VaultControlledBridgeRunOutTarget : probe.VaultControlledBridgeCrossTarget;
            string phase = farSide ? "RunOut" : "Cross";
            if (!string.Equals(probe.VaultControlledBridgePhase, phase, StringComparison.Ordinal))
            {
                probe.VaultControlledBridgePhase = phase;
                probe.VaultControlledBridgeLastDistance = GetPlanarDistance(probe.Player.Position, target);
                probe.VaultControlledBridgeLastProgressAt = Time.time;
                WriteEvent("controlledVaultCrossingBridgePhase", probe.Bot, new
                {
                    edgeId = commitment.EdgeId,
                    phase,
                    projection,
                    requiredProjection,
                    position = Vec(probe.Player.Position),
                    target = Vec(target)
                });
            }

            float planarDistance = GetPlanarDistance(probe.Player.Position, target);
            float spatialDistance = Vector3.Distance(probe.Player.Position, target);
            if (farSide && planarDistance <= VaultControlledBridgeStopDistance && Mathf.Abs(probe.Player.Position.y - target.y) <= 0.80f)
            {
                RestoreAssistedObstacleCollisionLease(probe, "controlledVaultBridgeCompleted");
                probe.VaultControlledBridgeActive = false;
                probe.TraversalVerifyTarget = target;
                WriteEvent("controlledVaultCrossingBridgeCompleted", probe.Bot, new
                {
                    edgeId = commitment.EdgeId,
                    elapsed,
                    projection,
                    requiredProjection,
                    position = Vec(probe.Player.Position),
                    target = Vec(target)
                });
                BeginUnifiedTraversalVerify(probe, "controlledVaultBridgeCompleted");
                return;
            }

            Vector3 before = probe.Player.Position;
            Vector3 delta = target - before;
            float speed = farSide ? VaultControlledBridgeRunOutSpeed : VaultControlledBridgeSpeed;
            float dt = Mathf.Max(0.001f, Time.deltaTime);
            float step = Mathf.Min(spatialDistance, speed * dt);
            Vector3 motion = spatialDistance > 0.001f ? delta / spatialDistance * step : Vector3.zero;
            try
            {
                // CompileFix36: CharacterController drive must own a real locomotion intent. Fix35
                // paused the mover and zeroed both input vectors, then expected CC.Move alone to
                // cross the rail; runtime showed only 0.13 m progress. Keep BotMover paused so it
                // cannot fight the bridge, but restore pose/speed/forward MovementContext intent.
                probe.Bot.Mover?.BotMoverStop("R1-C3C5C40D6D34",@probe?.Bot);
                if (probe.Bot.Mover != null)
                {
                    probe.Bot.Mover.SetBotMoverPause("R1-060417177D6F",@probe?.Bot,true);
                    probe.Bot.Mover.LocalAvoidance?.GClass492DropOffset("R1-0E3274C0C33C",@probe?.Bot);
                    if (probe.Bot.Mover.Sprinting)
                    {
                        probe.Bot.Mover.BotMoverSprint("R1-98C416FC41BD",@probe?.Bot,false,false);
                    }
                    probe.Bot.Mover.BotMoverSetPose("R1-D16A46C3F16C",@probe?.Bot,1f);
                    probe.Bot.Mover.BotMoverSetTargetMoveSpeed("R1-E04C357666C1",@probe?.Bot,1f);
                }
                probe.Bot.BotOwnerSetTargetMoveSpeed("R1-A98033D55752",@probe?.Bot,1f);
                HoldForwardInput(probe);
                probe.Player.MovementContext.MovementContextPlayerAnimatorSetDoVault("R1-DFC6379ECD9D",@probe?.Bot,false);
                probe.Player.MovementContext.MovementContextPlayerAnimatorSetDoClimb("R1-FC046A32F55C",@probe?.Bot,false);
                Vector3 flatDirection = GetFlatDirection(delta);
                if (flatDirection.sqrMagnitude > 0.5f)
                {
                    probe.Bot.Steering?.BotSteeringLookToDirection("R1-70960054EF06",@probe?.Bot,flatDirection);
                }
                probe.Player.CharacterController.TryICharacterControllerMove("R1-3CA495D59F1A",@probe?.Bot,motion,dt);
            }
            catch (Exception ex)
            {
                RestoreAssistedObstacleCollisionLease(probe, "controlledVaultBridgeException");
                probe.VaultControlledBridgeActive = false;
                WriteEvent("controlledVaultCrossingBridgeFailed", probe.Bot, new
                {
                    edgeId = commitment.EdgeId,
                    reason = "moveException",
                    error = ex.GetType().Name + ": " + ex.Message
                });
                FinalizeOrEscalateTraversalFailure(probe, "crossingNotFarSideBeforeEgress:controlledVaultBridgeException");
                return;
            }

            Vector3 after = probe.Player.Position;
            float requestedDelta = motion.magnitude;
            float actualDelta = Vector3.Distance(before, after);
            float actualPlanarDelta = GetPlanarDistance(before, after);
            float actualToTarget = GetPlanarDistance(after, target);
            if (actualToTarget + 0.025f < probe.VaultControlledBridgeLastDistance)
            {
                probe.VaultControlledBridgeLastDistance = actualToTarget;
                probe.VaultControlledBridgeLastProgressAt = Time.time;
            }

            if (Time.time >= probe.VaultControlledBridgeNextMotionTraceAt)
            {
                probe.VaultControlledBridgeNextMotionTraceAt = Time.time + VaultControlledBridgeMotionTraceIntervalSeconds;
                WriteEvent("controlledVaultCrossingBridgeMotion", probe.Bot, new
                {
                    edgeId = commitment.EdgeId,
                    phase,
                    state = state.ToString(),
                    grounded = probe.Player.MovementContext?.IsGrounded == true,
                    projection,
                    requiredProjection,
                    requestedMotion = Vec(motion),
                    requestedDelta,
                    before = Vec(before),
                    after = Vec(after),
                    actualDelta,
                    actualPlanarDelta,
                    responseRatio = requestedDelta > 0.001f ? actualDelta / requestedDelta : 0f,
                    target = Vec(target),
                    targetDistance = actualToTarget
                });
            }

            float noProgressFor = Time.time - probe.VaultControlledBridgeLastProgressAt;
            if (noProgressFor >= VaultControlledBridgeNoProgressSeconds)
            {
                RestoreAssistedObstacleCollisionLease(probe, "controlledVaultBridgeNoProgress");
                probe.VaultControlledBridgeActive = false;
                IsVaultClimbFarSideForEgress(probe, commitment, out projection, out requiredProjection);
                WriteEvent("controlledVaultCrossingBridgeFailed", probe.Bot, new
                {
                    edgeId = commitment.EdgeId,
                    reason = "noPhysicalProgress",
                    phase,
                    elapsed,
                    noProgressFor,
                    projection,
                    requiredProjection,
                    position = Vec(probe.Player.Position),
                    target = Vec(target),
                    lastRequestedDelta = requestedDelta,
                    lastActualDelta = actualDelta
                });
                FinalizeOrEscalateTraversalFailure(probe, "crossingNotFarSideBeforeEgress:controlledVaultBridgeNoProgress");
            }
        }
        private static bool TryBeginLearningV32NativeEntryAcquisition(
            ProbeRuntime probe,
            LearnedTraversalCommitment commitment,
            string source,
            out string failure)
        {
            failure = string.Empty;
            if ((probe == null) || commitment == null ||
                probe.Player?.MovementContext == null || probe.Bot == null)
            {
                failure = "shared native entry acquisition context unavailable";
                return false;
            }

            if (probe.LearningV32NativeEntryAcquired)
            {
                return true;
            }

            string kind = commitment.Kind ?? probe.RequestedTraversalKind ?? string.Empty;
            bool supportedKind = FollowerCapabilityHelperRules.TryBeginLearningV32NativeEntryAcquisition_supportedKind_A122C32E_1(kind,kind,kind,kind);
            if (!supportedKind)
            {
                failure = "shared native entry acquisition does not support kind=" + kind;
                return false;
            }

            Vector3 identityRawEntry = IsFinite(commitment.RawEntry) ? commitment.RawEntry : commitment.Entry;
            Vector3 rawEntry = string.Equals(kind, "Jump", StringComparison.OrdinalIgnoreCase)
                ? GetLearningV442JumpAdmissionEntry(commitment)
                : identityRawEntry;
            Vector3 rawExit = IsFinite(commitment.RawExit) ? commitment.RawExit : commitment.Exit;
            Vector3 direction = GetFlatDirection(rawExit - identityRawEntry);
            if (!IsFinite(rawEntry) || !IsFinite(identityRawEntry) || !IsFinite(rawExit) || direction.sqrMagnitude < 0.5f)
            {
                failure = "shared native entry geometry/direction invalid";
                return false;
            }

            if (TryRejectLearningV430HistoricalEntryAcquisition(
                    probe, commitment, rawEntry, rawExit, source))
            {
                failure = "learningV430HistoricalEntryReacquireRejected";
                return true;
            }

            if (probe.Stage != ProbeStage.NativeEntryAcquisition)
            {
                Vector3 entryOffset = probe.Player.Position - rawEntry;
                entryOffset.y = 0f;
                float initialForwardProjection = Vector3.Dot(entryOffset, direction);
                TransitionProbeStage(probe, ProbeStage.NativeEntryAcquisition);
                probe.StageStartedAt = Time.time;
                probe.ExclusiveMoverGate = true;
                probe.LearningV32NativeEntrySource = source ?? string.Empty;
                probe.LearningV32NativeEntryStartPosition = probe.Player.Position;
                probe.LearningV32NativeEntryStartForwardProjection = initialForwardProjection;
                probe.LearningV32NativeEntryBestForwardProjection = initialForwardProjection;
                probe.LearningV32NativeEntryLastPosition = probe.Player.Position;
                probe.LearningV32NativeEntryLastProgressAt = Time.time;
                probe.LearningV32NativeEntryBestAngle=float.NaN;
                probe.LearningV32NativeEntryReadySince = -1f;
                probe.LearningV32NativeEntryNextMoverRefreshAt = Time.time;
                probe.LearningV32NativeEntryReacquireAttempts = 0;
                probe.LearningV33PassageBoundaryState = LearningV33PassageBoundaryState.Unknown;
                probe.LearningV33PassageBoundaryChangedAt = Time.time;
                probe.LearningV33DropDownEntryAirborneSince = -1f;
                probe.LearningV33DropDownEntrySawAirborne = false;
                probe.LearningV33DropDownEntryMaxDescent = 0f;
                probe.LearningV33DropDownEntryMaxForwardProjection = initialForwardProjection;
                probe.LearningV34PassageCommitConsumed = false;
                probe.LearningV34FarSideMoverRebaseSucceeded = false;
                probe.LearningV34PassageCommitConsumedAt = -1f;
                probe.OwnershipLeaseExpiresAt = Mathf.Max(
                    probe.OwnershipLeaseExpiresAt,
                    Time.time + LearningV32EntryAcquisitionTimeout * 2f + 1.5f);
                EmitLearningV346PhysicalNavigationEpochSnapshot(probe, "nativeEntryAcquired");

                WriteEvent("learningV32NativeEntryAcquisitionArmed", probe.Bot, new
                {
                    edgeId = commitment.EdgeId,
                    kind,
                    source = source ?? string.Empty,
                    position = Vec(probe.Player.Position),
                    rawEntry = Vec(rawEntry),
                    rawExit = Vec(rawExit),
                    initialForwardProjection,
                    nativeExecutorConsumed = probe.NativeExecutorConsumed,
                    executionEpochStarted = false
                });

                if (((probe.LearningV337SelfRescueControlledEntry || probe.LearningV337SelfRescueControlledEntryRequested)) &&
                    initialForwardProjection <= 0f && !probe.LearningV32NativeEntryReacquireActive)
                {
                    Vector3 actualForward = GetFlatDirection(probe.Player.MovementContext.PlayerRealForward);
                    float alignmentAngle = actualForward.sqrMagnitude > 0.5f
                        ? Vector3.Angle(actualForward, direction)
                        : 180f;
                    Vector3 entryDelta = probe.Player.Position - rawEntry;
                    entryDelta.y = 0f;
                    float distance = entryDelta.magnitude;
                    Vector3 lateral = entryDelta - direction * initialForwardProjection;
                    bool stagingArmed = TryBeginLearningV32NativeEntryReacquire(
                        probe, commitment, rawEntry, direction, distance, alignmentAngle,
                        lateral.magnitude, initialForwardProjection);
                    {
                        probe.LearningV337SelfRescueControlledEntry = stagingArmed;
                    }
                    WriteEvent("learningV337SelfRescueControlledEntryStagingArmed", probe.Bot, new
                    {
                        edgeId = commitment.EdgeId,
                        kind,
                        source = probe.LearningV337SelfRescueControlledEntrySource ?? source ?? string.Empty,
                        stagingArmed,
                        controlledEntryActive = probe.LearningV337SelfRescueControlledEntry,
                        speed = (stagingArmed) ? 0.55f : 0f,
                        sprintAllowed = false,
                        retrySeconds = RouteMissionMoveRetrySeconds,
                        action = "existingLocalStagingControllerFromFirstRetryTick"
                    });
                }
            }

            DriveLearningV32NativeEntryAcquisition(probe, commitment, rawEntry, rawExit, direction);
            return true;
        }

        private static void UpdateSharedNativeEntryAcquisition(ProbeRuntime probe)
        {
            if (probe == null || !IsActiveProbe(probe) || probe.Player?.MovementContext == null ||
                !TryGetLearnedCommitmentForProbe(probe, out LearnedTraversalCommitment commitment) || commitment == null)
            {
                return;
            }

            Vector3 rawEntry = IsFinite(commitment.RawEntry) ? commitment.RawEntry : commitment.Entry;
            Vector3 rawExit = IsFinite(commitment.RawExit) ? commitment.RawExit : commitment.Exit;
            Vector3 direction = GetFlatDirection(rawExit - rawEntry);
            if (!IsFinite(rawEntry) || !IsFinite(rawExit) || direction.sqrMagnitude < 0.5f)
            {
                FinalizeOrEscalateTraversalFailure(probe, "learningV32NativeEntryGeometryLost");
                return;
            }

            Vector3 position = probe.Player.Position;
            Vector3 entryOffset = position - rawEntry;
            entryOffset.y = 0f;
            float distance = entryOffset.magnitude;
            float forwardEntryProjection = Vector3.Dot(entryOffset, direction);
            Vector3 lateral = entryOffset - direction * forwardEntryProjection;
            float lateralEntryOffset = lateral.magnitude;
            Vector3 actualForward = GetFlatDirection(probe.Player.MovementContext.PlayerRealForward);
            float alignmentAngle = actualForward.sqrMagnitude > 0.5f
                ? Vector3.Angle(actualForward, direction)
                : 180f;
            bool grounded = probe.Player.MovementContext.IsGrounded;
            EPlayerState currentState = probe.Player.CurrentStateName;
            bool transitionClear = currentState != EPlayerState.Transition;
            bool runState = FollowerCapabilityDecisionRules.UpdateSharedNativeEntryAcquisition_runState_1((int)(currentState),(int)(EPlayerState.Run),(int)(currentState),(int)(EPlayerState.Sprint));
            bool canJump = probe.Player.MovementContext.CanJump == true;
            bool kindClimb = string.Equals(commitment.Kind, "Climb", StringComparison.OrdinalIgnoreCase);
            bool kindVault = string.Equals(commitment.Kind, "Vault", StringComparison.OrdinalIgnoreCase);
            bool kindJump = string.Equals(commitment.Kind, "Jump", StringComparison.OrdinalIgnoreCase);
            bool kindDropDown = string.Equals(commitment.Kind, "DropDown", StringComparison.OrdinalIgnoreCase);
            int nativeEntryTemplateId = commitment.SkillTransferTransient ? commitment.SkillTemplateEdgeId : commitment.EdgeId;
            LearnedTraversalEdge learningV357EntryEdge =
                (kindVault || kindClimb) && commitment.EdgeId > 0
                    ? LearnedTraversalEdges.FirstOrDefault(candidate =>
                        candidate != null && candidate.Id == nativeEntryTemplateId)
                    : null;
            float learningV357EntryMaxLateral = GetLearningV357ActuatorEntryLateralTolerance(
                learningV357EntryEdge,
                direction,
                (kindVault)
                    ? InjectedNativeVaultRollingMaxLateralOffset
                    : InjectedNativeVaultEntryMaxLateralOffset);
            float learningV374MinimumActuatorForwardProjection = 0f;
            string learningV374ActuatorTriggerProofSource = string.Empty;
            bool learningV374HasLearnedActuatorTriggerFrontier =
                (kindVault || kindClimb) &&
                TryGetLearningV374LearnedActuatorTriggerFrontier(
                    learningV357EntryEdge,
                    out learningV374MinimumActuatorForwardProjection,
                    out learningV374ActuatorTriggerProofSource);
            bool learningV374ActuatorTriggerFrontierReady =
                FollowerCapabilityDecisionRules.UpdateSharedNativeEntryAcquisition_learningV374ActuatorTriggerFrontierReady_2(learningV374HasLearnedActuatorTriggerFrontier,forwardEntryProjection,learningV374MinimumActuatorForwardProjection);

            // V4.61 current-body successor admission. V4.62 tightens the proof contract:
            // being vertically below a historical DropDown entry is NOT sufficient entry authority.
            // The body must first satisfy the already-existing V3.3 portal-crossing chronology.
            // Until that happens, a near-side body keeps the normal continuous forward producer and
            // converges on the real lip instead of firing Native DropDown from obstacle-top support.
            if ((kindDropDown) &&
                !probe.LearningV32NativeEntryAcquired &&
                TryResolveLearningV376MissionForProbe(
                    probe, out FollowerRouteMission learningV461DropMission,
                    out int learningV461DropStepIndex) &&
                learningV461DropMission?.Steps != null &&
                learningV461DropStepIndex == learningV461DropMission.CurrentStepIndex &&
                learningV461DropStepIndex >= 0 &&
                learningV461DropStepIndex < learningV461DropMission.Steps.Count &&
                IsRouteMissionSameChainImmediateSuccessor(
                    learningV461DropMission, learningV461DropStepIndex))
            {
                FollowerRouteMissionStep learningV461DropStep =
                    learningV461DropMission.Steps[learningV461DropStepIndex];
                LearningV354TraversalCapabilityProfile learningV461DropProfile =
                    GetLearningV354ActionProfile(learningV461DropStep?.Kind);
                float learningV461VerticalDropFromHistoricalEntry =
                    rawEntry.y - probe.Player.Position.y;
                bool learningV461CurrentBodyProfileEligible =
                    learningV461DropStep != null &&
                    learningV461DropStep.EdgeId == commitment.EdgeId &&
                    learningV461DropProfile != null &&
                    learningV461DropProfile.CanAcquireFromOffNavMesh &&
                    learningV461DropProfile.SupportsRollingEntry &&
                    learningV461VerticalDropFromHistoricalEntry >=
                        LearningV33DropDownCrossingMinVerticalDrop &&
                    IsEffectivelySupportedForProbe(probe.Bot, probe.Player);

                if ((learningV461CurrentBodyProfileEligible))
                {
                    float learningV462AuthoredDrop = Mathf.Max(0f, rawEntry.y - rawExit.y);
                    float learningV462VerticalCommitThreshold = Mathf.Max(
                        LearningV33DropDownCrossingMinVerticalDrop,
                        learningV462AuthoredDrop * LearningV33DropDownCrossingVerticalFraction);
                    float learningV462PhysicalForwardProgress =
                        forwardEntryProjection - probe.LearningV32NativeEntryStartForwardProjection;
                    bool learningV462FarSide = IsFollowerAlreadyBelowDropDownLanding(
                        probe.Player.Position, rawEntry, rawExit, commitment.Exit);
                    bool learningV462CrossingCommitted =
                        FollowerCapabilityDecisionRules.UpdateSharedNativeEntryAcquisition_learningV462CrossingCommitted_3(learningV462FarSide,forwardEntryProjection,LearningV33DropDownCrossingMinForwardProjection,learningV462PhysicalForwardProgress,LearningV33DropDownCrossingMinForwardProgress,learningV461VerticalDropFromHistoricalEntry,learningV462VerticalCommitThreshold,lateralEntryOffset,LearningV33DropDownCrossingMaxLateral);

                    if (learningV462CrossingCommitted)
                    {
                        WriteEvent("learningV462CurrentBodyDropDownPortalCrossingConfirmed", probe.Bot, new
                        {
                            missionId = learningV461DropMission.MissionId,
                            stepIndex = learningV461DropStepIndex,
                            edgeId = commitment.EdgeId,
                            position = Vec(probe.Player.Position),
                            rawEntry = Vec(rawEntry),
                            rawExit = Vec(rawExit),
                            forwardEntryProjection,
                            lateralEntryOffset,
                            physicalForwardProgress = learningV462PhysicalForwardProgress,
                            verticalDropFromHistoricalEntry =
                                learningV461VerticalDropFromHistoricalEntry,
                            verticalCommitThreshold = learningV462VerticalCommitThreshold,
                            farSide = learningV462FarSide,
                            traversalSuccessRecorded = false,
                            persistentLearningChanged = false,
                            bodyRelocated = false,
                            action = "fullExistingV33PortalCrossingProofMayPromoteCurrentBodyChronology"
                        });

                        if (TryPromoteLearningV430PreAcquisitionPhysicalChronology(
                                probe, commitment,
                                "learningV462CurrentBodyDropDownPortalCrossingConfirmed"))
                        {
                            return;
                        }
                    }
                    else if (ShouldEmitDiagnostic(
                                 "learningV462CurrentBodyDropDownAdmissionDeferred:" +
                                     commitment.EdgeId.ToString(CultureInfo.InvariantCulture),
                                 probe.Bot, 0.18f))
                    {
                        WriteEvent("learningV462CurrentBodyDropDownAdmissionDeferredToPortalCrossing", probe.Bot, new
                        {
                            missionId = learningV461DropMission.MissionId,
                            stepIndex = learningV461DropStepIndex,
                            edgeId = commitment.EdgeId,
                            position = Vec(probe.Player.Position),
                            rawEntry = Vec(rawEntry),
                            rawExit = Vec(rawExit),
                            forwardEntryProjection,
                            lateralEntryOffset,
                            physicalForwardProgress = learningV462PhysicalForwardProgress,
                            verticalDropFromHistoricalEntry =
                                learningV461VerticalDropFromHistoricalEntry,
                            verticalCommitThreshold = learningV462VerticalCommitThreshold,
                            farSide = learningV462FarSide,
                            currentBodyEntryAuthorityGranted = false,
                            traversalSuccessRecorded = false,
                            persistentLearningChanged = false,
                            bodyRelocated = false,
                            action = "lowerNearSideBodyKeepsForwardProducerUntilRealDropDownLipCrossing"
                        });
                    }

                    // V4.62 intentionally falls through to the existing V3.2/V3.11/V3.85 entry
                    // machinery. A lower body that is still on the near side must continue forward;
                    // a body that truly crossed is consumed by V4.30/V3.3 chronology above.
                    probe.LearningV461CurrentBodySuccessorAdmission = false;
                }
                else if (learningV461CurrentBodyProfileEligible)
                {
                    // V4.61 fallback retained only when V4.62 is disabled.
                    probe.LearningV461CurrentBodySuccessorAdmission = true;
                    MarkLearningV32NativeEntryAcquired(
                        probe, rawEntry, rawExit, distance, alignmentAngle, lateralEntryOffset,
                        forwardEntryProjection,
                        "learningV461CurrentBodyInheritedDropDownAdmission",
                        directHandoff: true);
                    WriteEvent("learningV461CurrentBodyDropDownAdmissionInherited", probe.Bot, new
                    {
                        missionId = learningV461DropMission.MissionId,
                        stepIndex = learningV461DropStepIndex,
                        edgeId = commitment.EdgeId,
                        position = Vec(probe.Player.Position),
                        rawEntry = Vec(rawEntry),
                        rawExit = Vec(rawExit),
                        forwardEntryProjection,
                        lateralEntryOffset,
                        verticalDropFromHistoricalEntry =
                            learningV461VerticalDropFromHistoricalEntry,
                        existingVerticalProof =
                            LearningV33DropDownCrossingMinVerticalDrop,
                        traversalSuccessRecorded = false,
                        persistentLearningChanged = false,
                        bodyRelocated = false,
                        action = "sameChainLowerCurrentBodyOwnsDropDownAdmissionHistoricalUpperEntryBecomesOrderingEvidenceOnly"
                    });
                    if (!TryBeginAssistedTraversalReplay(
                            probe, "learningV461CurrentBodyDropDownAdmission",
                            out string learningV461StartFailure))
                    {
                        FinalizeOrEscalateTraversalFailure(
                            probe, "learningV461CurrentBodyDropDownNativeStartFailed:" +
                                   (learningV461StartFailure ?? string.Empty));
                    }
                    return;
                }
            }

            if (probe.LearningV32NativeEntryReacquireActive)
            {
                float stagingDistance = GetPlanarDistance(position, probe.LearningV32NativeEntryReacquireTarget);
                if (stagingDistance <= InjectedNativeVaultEntryStagingReleaseDistance)
                {
                    probe.LearningV32NativeEntryReacquireActive = false;
                    probe.StageStartedAt = Time.time;
                    probe.LearningV32NativeEntryStartPosition = position;
                    probe.LearningV32NativeEntryStartForwardProjection = forwardEntryProjection;
                    probe.LearningV32NativeEntryBestForwardProjection = forwardEntryProjection;
                    probe.LearningV32NativeEntryLastPosition = position;
                    probe.LearningV32NativeEntryLastProgressAt = Time.time;
                    probe.LearningV32NativeEntryBestAngle=float.NaN;
                    probe.LearningV32NativeEntryReadySince = -1f;
                    WriteEvent("learningV32NativeEntryReacquireStagingReached", probe.Bot, new
                    {
                        edgeId = commitment.EdgeId,
                        kind = commitment.Kind,
                        stagingTarget = Vec(probe.LearningV32NativeEntryReacquireTarget),
                        stagingDistance,
                        attempt = probe.LearningV32NativeEntryReacquireAttempts,
                        action = "resumeForwardEntryAcquisition"
                    });
                }
            }

            if(float.IsNaN(probe.LearningV32NativeEntryBestAngle))probe.LearningV32NativeEntryBestAngle=alignmentAngle;
            else if(FollowerCapabilityPortalPolicy.AngularProgress(probe.LearningV32NativeEntryBestAngle,alignmentAngle))
            {
                probe.LearningV32NativeEntryBestAngle=alignmentAngle;
                probe.LearningV32NativeEntryLastProgressAt=Time.time;
            }
            if (forwardEntryProjection > probe.LearningV32NativeEntryBestForwardProjection + LearningV32EntryProgressEpsilon ||
                Vector3.Distance(position, probe.LearningV32NativeEntryLastPosition) > LearningV32EntryProgressEpsilon)
            {
                probe.LearningV32NativeEntryBestForwardProjection = Mathf.Max(
                    probe.LearningV32NativeEntryBestForwardProjection, forwardEntryProjection);
                probe.LearningV32NativeEntryLastPosition = position;
                probe.LearningV32NativeEntryLastProgressAt = Time.time;
            }

            float physicalForwardProgress = forwardEntryProjection - probe.LearningV32NativeEntryStartForwardProjection;
            bool forwardMotionEvidence = FollowerCapabilityDecisionRules.UpdateSharedNativeEntryAcquisition_forwardMotionEvidence_4(physicalForwardProgress,LearningV32EntryForwardMotionEvidence,forwardEntryProjection);

            if (UpdateLearningV33MonotonicPassageBoundary(
                    probe, commitment, rawEntry, rawExit, direction, forwardEntryProjection,
                    lateralEntryOffset, physicalForwardProgress, grounded))
            {
                return;
            }

            bool learningV463DeferredDropDown = false;
            Vector3 learningV463SharedForwardTarget = default;
            if ((kindDropDown) &&
                TryResolveLearningV376MissionForProbe(
                    probe, out FollowerRouteMission learningV463Mission,
                    out int learningV463StepIndex) &&
                learningV463Mission?.Steps != null &&
                learningV463StepIndex == learningV463Mission.CurrentStepIndex &&
                learningV463StepIndex >= 0 &&
                learningV463StepIndex < learningV463Mission.Steps.Count)
            {
                learningV463DeferredDropDown =
                    TryBuildLearningV463DeferredDropDownProducerTarget(
                        probe.Bot, probe, learningV463Mission,
                        learningV463Mission.Steps[learningV463StepIndex], rawEntry, rawExit,
                        out learningV463SharedForwardTarget, out _, out _);
            }

            bool entryEnvelopeReady;
            if (kindDropDown)
            {
                // An elevated historical entry must not confer Native DropDown readiness while
                // the supported body is still near-side on the lower real physical surface.
                entryEnvelopeReady = !learningV463DeferredDropDown &&
                    grounded && runState && transitionClear &&
                    forwardEntryProjection >= -LearningV32DropDownAcquireNearSideDistance &&
                    forwardEntryProjection <= NativeDropDownEntryMaxForwardOvershoot &&
                    lateralEntryOffset <= NativeDropDownEntryMaxLateralOffset &&
                    alignmentAngle <= NativeDropDownEntryMaxAlignmentAngle &&
                    forwardMotionEvidence;
            }
            else if (kindJump)
            {
                float jumpEntryMaxLateral = (LearningV352JumpTakeoffCorridorMaxLateral);
                float jumpEntryMaxAlignment = (Mathf.Acos(LearningV352JumpTakeoffCorridorMinFacingDot) * Mathf.Rad2Deg);
                entryEnvelopeReady = grounded && canJump && runState && transitionClear &&
                    distance <= JumpEntryCaptureDistance &&
                    lateralEntryOffset <= jumpEntryMaxLateral &&
                    alignmentAngle <= jumpEntryMaxAlignment &&
                    forwardEntryProjection <= LearningV352JumpTakeoffCorridorMaxForwardOvershoot &&
                    forwardMotionEvidence;
            }
            else if (kindVault)
            {
                entryEnvelopeReady = grounded && runState && transitionClear &&
                    distance <= InjectedNativeVaultEntryTriggerDistance &&
                    lateralEntryOffset <= learningV357EntryMaxLateral &&
                    alignmentAngle <= ((InjectedNativeVaultRollingMaxAlignmentAngle)) &&
                    forwardEntryProjection <= InjectedNativeVaultEntryMaxForwardOvershoot &&
                    learningV374ActuatorTriggerFrontierReady &&
                    forwardMotionEvidence;
            }
            else // Climb
            {
                bool climbSprintReleased = probe.Bot?.Mover?.Sprinting != true &&
                    !probe.Player.MovementContext.IsSprintEnabled &&
                    currentState != EPlayerState.Sprint && currentState != EPlayerState.Transition;
                entryEnvelopeReady = grounded && transitionClear && climbSprintReleased &&
                    distance <= InjectedNativeVaultEntryTriggerDistance &&
                    lateralEntryOffset <= learningV357EntryMaxLateral &&
                    alignmentAngle <= InjectedNativeVaultEntryMaxAlignmentAngle &&
                    forwardEntryProjection <= InjectedNativeVaultEntryMaxForwardOvershoot &&
                    learningV374ActuatorTriggerFrontierReady &&
                    (forwardMotionEvidence || distance <= 0.30f);
            }

            if ((kindVault || kindClimb) &&
                learningV374HasLearnedActuatorTriggerFrontier &&
                !learningV374ActuatorTriggerFrontierReady &&
                ShouldEmitDiagnostic(
                    "learningV374SharedEntryWaitingForLearnedActuatorTriggerFrontier:" +
                        commitment.EdgeId.ToString(CultureInfo.InvariantCulture), probe.Bot, 0.25f))
            {
                WriteEvent("learningV374SharedEntryWaitingForLearnedActuatorTriggerFrontier", probe.Bot, new
                {
                    edgeId = commitment.EdgeId,
                    kind = commitment.Kind,
                    position = Vec(position),
                    rawEntry = Vec(rawEntry),
                    forwardEntryProjection,
                    requiredMinimumForwardProjection = learningV374MinimumActuatorForwardProjection,
                    lateralEntryOffset,
                    alignmentAngle,
                    proofSource = learningV374ActuatorTriggerProofSource,
                    action = "approachMayUseWideLearnedObstacleLaneButNativeActuatorWaitsForDemonstratedTriggerOrigin"
                });
            }

            if(!entryEnvelopeReady&&ShouldEmitDiagnostic("capabilityNativeEntryAdmissionBlocked",probe.Bot,.4f))
                WriteEvent("capabilityNativeEntryAdmissionBlocked",probe.Bot,new{edgeId=commitment.EdgeId,kind=commitment.Kind,
                    grounded,runState,canJump,transitionClear,forwardMotionEvidence,distance,alignmentAngle,
                    lateralEntryOffset,forwardEntryProjection,nativeExecutorConsumed=probe.NativeExecutorConsumed});
            if (entryEnvelopeReady && !probe.LearningV32NativeEntryReacquireActive)
            {
                if (probe.LearningV32NativeEntryReadySince < 0f)
                {
                    probe.LearningV32NativeEntryReadySince = Time.time;
                    WriteEvent("learningV32NativeEntryReadyCandidate", probe.Bot, new
                    {
                        edgeId = commitment.EdgeId,
                        kind = commitment.Kind,
                        distance,
                        alignmentAngle,
                        lateralEntryOffset,
                        learningV357EntryMaxLateral,
                        forwardEntryProjection,
                        physicalForwardProgress,
                        grounded,
                        state = currentState.ToString()
                    });
                }
                // Selected obstacle admission and actuator consume this exact body sample.
                // Waiting another frame while the approach keeps moving loses narrow frontiers.
                if (FollowerCapabilityIngressMotionPolicy.ConsumeReadyEntry(
                    probe.LifecycleMission != null && probe.NativeStrategySelected,kindVault || kindClimb || kindJump && HasCapabilityReadyJumpMotion(probe.Player,direction),
                    Time.time - probe.LearningV32NativeEntryReadySince >= LearningV32EntryReadyStableSeconds))
                {
                    MarkLearningV32NativeEntryAcquired(
                        probe, rawEntry, rawExit, distance, alignmentAngle, lateralEntryOffset,
                        forwardEntryProjection, "sharedEntryStable", directHandoff: false);
                    if (!TryBeginAssistedTraversalReplay(
                            probe, "learningV32SharedEntryAcquired", out string startFailure))
                    {
                        FinalizeOrEscalateTraversalFailure(
                            probe, "learningV32SharedEntryNativeStartFailed:" + startFailure);
                    }
                    return;
                }
            }
            else
            {
                probe.LearningV32NativeEntryReadySince = -1f;
            }

            // If ordinary collision/gravity carried the follower across before the execution epoch was
            // armed, recover the route but do not promote it as Native. This preserves proof integrity.
            if ((kindDropDown || kindJump || kindVault || kindClimb) &&
                TryAcceptAlreadyCrossedBeforeAssistedReplay(
                    probe, commitment, "learningV32PreExecutionCrossing"))
            {
                return;
            }

            float elapsed = Time.time - probe.StageStartedAt;
            float noProgress = Time.time - probe.LearningV32NativeEntryLastProgressAt;
            float acquisitionBudget=(kindDropDown||kindVault||kindClimb)?FollowerCapabilityFailureSequence.EntryBudget(
                GetPlanarDistance(probe.LearningV32NativeEntryStartPosition,rawEntry),LearningV32EntryAcquisitionTimeout,.55f,4.25f):LearningV32EntryAcquisitionTimeout;
            bool acquisitionExpired = FollowerCapabilityDecisionRules.UpdateSharedNativeEntryAcquisition_acquisitionExpired_5(elapsed,acquisitionBudget,noProgress,LearningV32EntryAcquisitionNoProgressTimeout);
            if (acquisitionExpired)
            {
                WriteEvent("learningV32NativeEntryAcquisitionExpired", probe.Bot, new
                {
                    edgeId = commitment.EdgeId,
                    kind = commitment.Kind,
                    elapsed,
                    noProgress,
                    distance,
                    alignmentAngle,
                    lateralEntryOffset,
                    forwardEntryProjection,
                    physicalForwardProgress,
                    reacquireAttempts = probe.LearningV32NativeEntryReacquireAttempts,
                    maxReacquireAttempts = LearningV32EntryMaxReacquireAttempts
                });

                if (probe.LearningV33PassageBoundaryState < LearningV33PassageBoundaryState.CrossingCommitted &&
                    probe.LearningV32NativeEntryReacquireAttempts < LearningV32EntryMaxReacquireAttempts &&
                    TryBeginLearningV32NativeEntryReacquire(
                        probe, commitment, rawEntry, direction, distance, alignmentAngle,
                        lateralEntryOffset, forwardEntryProjection))
                {
                    return;
                }

                string rejectReason =
                    $"learningV32EntryAcquisitionFailed:kind={commitment.Kind}:distance={distance:0.00}:" +
                    $"angle={alignmentAngle:0.0}:lateral={lateralEntryOffset:0.00}:projection={forwardEntryProjection:0.00}";
                probe.NativeEntryConvergenceRejected = true;
                probe.NativeEntryConvergenceRejectReason = rejectReason;
                if (TryArmLearningV336TraversalSelfRescue(
                        probe, rejectReason, "EntryAcquisition"))
                {
                    return;
                }
                probe.NativeStrategySelected = false;
                probe.AssistedStrategySelected = true;
                WriteEvent("learningV32NativeEntryYieldedToSafetyAssist", probe.Bot, new
                {
                    edgeId = commitment.EdgeId,
                    kind = commitment.Kind,
                    reason = rejectReason,
                    nativeExecutorConsumed = probe.NativeExecutorConsumed,
                    executionEpochStarted = probe.LearningV32NativeExecutionEpochStartedAt >= 0f
                });
                if (!TryBeginAssistedTraversalReplay(
                        probe, "learningV32EntryAcquisitionFailed", out string assistFailure))
                {
                    FinalizeOrEscalateTraversalFailure(
                        probe, rejectReason + ":assist=" + (assistFailure ?? string.Empty));
                }
                return;
            }

            DriveLearningV32NativeEntryAcquisition(
                probe, commitment, rawEntry, rawExit, direction,
                learningV463DeferredDropDown ? learningV463SharedForwardTarget : (Vector3?)null);
            if (ShouldEmitDiagnostic("learningV32NativeEntryAcquisitionProgress", probe.Bot, 0.40f))
            {
                WriteEvent("learningV32NativeEntryAcquisitionProgress", probe.Bot, new
                {
                    edgeId = commitment.EdgeId,
                    kind = commitment.Kind,
                    distance,
                    alignmentAngle,
                    lateralEntryOffset,
                    forwardEntryProjection,
                    physicalForwardProgress,
                    grounded,
                    state = currentState.ToString(),
                    noProgress
                });
            }
        }

        private static bool TryDriveCapabilityObstacleEntry(ProbeRuntime probe,LearnedTraversalCommitment c,Vector3 target,Vector3 entry,Vector3 axis)
        {
            if((c.Kind!="Vault"&&c.Kind!="Climb")||probe.LearningV32NativeEntryReacquireActive||!IsProbeLifecycleCurrent(probe)||
                !IsCapabilityRouteRetrySafe(probe.Bot)||probe.NativeExecutorConsumed)return false;
            long generation=FollowerMovementWriterAuthority.PeekExactGeneration(probe.Bot,out var owner);
            if(owner!=FollowerMovementWriterAuthority.WriterDomain.Traversal||generation!=probe.MovementWriterGeneration||
                !FollowerMovementWriterAuthority.TryEnterBoundPhysicalMotion(probe.Bot,owner,generation,out var scope,out _))return true;
            using(scope)
            {
                if(!IsProbeLifecycleCurrent(probe)||!IsCapabilityRouteRetrySafe(probe.Bot)||probe.NativeExecutorConsumed)return true;
                var player=probe.Player;var offset=GetFlatDelta(entry,player.Position);float forward=Vector3.Dot(offset,axis),side=(offset-axis*forward).magnitude;
                float distance=GetPlanarDistance(player.Position,entry),angle=Vector3.Angle(GetFlatDirection(player.MovementContext.PlayerRealForward),axis);
                var edge=LearnedTraversalEdges.FirstOrDefault(e=>e!=null&&e.Id==(c.SkillTransferTransient?c.SkillTemplateEdgeId:c.EdgeId));
                float maximum=GetLearningV357ActuatorEntryLateralTolerance(edge,axis,
                    c.Kind=="Vault"?InjectedNativeVaultRollingMaxLateralOffset:InjectedNativeVaultEntryMaxLateralOffset);
                float centeringMaximum=(float)FollowerCapabilityPassagePolicy.ObstacleCenterTolerance(maximum,InjectedNativeVaultEntryTriggerDistance);
                bool positionCorrection=FollowerCapabilityPassagePolicy.ObstacleNeedsCenter(side,centeringMaximum);
                bool hold=!positionCorrection&&FollowerCapabilityPassagePolicy.ObstacleHold(true,false,distance,side,maximum,angle,InjectedNativeVaultRollingMaxAlignmentAngle);
                bool safeHeight=FollowerCapabilityPassagePolicy.EntryHeight(player.MovementContext.IsGrounded,entry.y-player.Position.y);
                bool sideOnly=FollowerCapabilityPassagePolicy.CenterSideOnly(positionCorrection,distance);
                // Inside the actuator frontier correct only cross-track debt; a cached
                // staging point must not pull the body backward over the completed prefix.
                var direction=sideOnly?GetFlatDirection(-(offset-axis*forward)):
                    (positionCorrection?GetFlatDirection(entry-player.Position):axis);
                if(direction.sqrMagnitude<.5f)direction=axis;
                float dt=Mathf.Max(.001f,Time.deltaTime);
                float centerAngle=Vector3.Angle(GetFlatDirection(player.MovementContext.PlayerRealForward),direction);
                bool centerTurn=FollowerCapabilityPassagePolicy.SideCenterTurn(sideOnly,positionCorrection,centerAngle);
                bool stationary=hold||centerTurn||!safeHeight;
                bool precise=FollowerCapabilityPassagePolicy.ObstacleNear(distance)||angle>InjectedNativeVaultRollingMaxAlignmentAngle||positionCorrection||c.Kind=="Climb"||HasCapabilityFailureStrategy(probe.Bot);
                bool sprint=!stationary&&!precise&&probe.PreviousSprinting;
                float speed=(float)FollowerCapabilityPassagePolicy.ObstacleEntrySpeed(distance,stationary?0f:(precise?.55f:(sprint?1f:.82f)));
                var motion=stationary?Vector3.zero:direction*dt;
                // Once centered, axis owns both steering and forward input; the old point target cannot oppose it.
                ApplyCollisionActivePhysicalRouteMotion(probe.Bot,player,direction,motion,dt,1f,sprint,speed,!precise,true);
                if(hold||centerTurn||probe.PoseExclusiveOrientationAuthority)
                    DriveTraversalPoseAcquisition(probe,sideOnly?axis:direction,sideOnly?angle:centerAngle,"obstacleEntryCenterTurn",true);
                if(ShouldEmitDiagnostic("capabilityObstacleEntryResource",probe.Bot,.25f))WriteEvent("capabilityObstacleEntryResource",probe.Bot,new{
                    edgeId=c.EdgeId,kind=c.Kind,hold,sideOnly,centerTurn,centerAngle,safeHeight,distance,side,maximum,forward,angle,body=Vec(player.Position),entry=Vec(entry),nativeSuccessRecorded=false});
                return true;
            }
        }
        private static void DriveLearningV32NativeEntryAcquisition(
            ProbeRuntime probe,
            LearnedTraversalCommitment commitment,
            Vector3 rawEntry,
            Vector3 rawExit,
            Vector3 direction,
            Vector3? currentBodyForwardTarget = null)
        {
            if (probe?.Bot == null || probe.Player?.MovementContext == null)
            {
                return;
            }

            bool kindDropDown = string.Equals(commitment.Kind, "DropDown", StringComparison.OrdinalIgnoreCase);
            bool kindClimb = string.Equals(commitment.Kind, "Climb", StringComparison.OrdinalIgnoreCase);
            Vector3 driveTarget = probe.LearningV32NativeEntryReacquireActive &&
                IsFinite(probe.LearningV32NativeEntryReacquireTarget)
                ? probe.LearningV32NativeEntryReacquireTarget
                : (currentBodyForwardTarget.HasValue && IsFinite(currentBodyForwardTarget.Value)
                    ? currentBodyForwardTarget.Value
                    : rawEntry);
            if(kindDropDown&&!probe.LearningV32NativeEntryReacquireActive&&!currentBodyForwardTarget.HasValue)
                driveTarget.y=probe.Player.Position.y;
            if(TryDriveCapabilityObstacleEntry(probe,commitment,driveTarget,rawEntry,direction))return;
            try
            {
                if (probe.Bot.Mover != null)
                {
                    probe.Bot.Mover.SetBotMoverPause("R1-242B1EDD2169",@probe?.Bot,false);
                    probe.Bot.Mover.LocalAvoidance?.GClass492DropOffset("R1-49CCCDBD29EB",@probe?.Bot);
                    if (kindClimb)
                    {
                        probe.Bot.Mover.BotMoverSprint("R1-AA1AB4D931B9",@probe?.Bot,false,false);
                    }
                    if (Time.time >= probe.LearningV32NativeEntryNextMoverRefreshAt && IsFinite(driveTarget))
                    {
                        probe.Bot.Mover.BotMoverGoToPointNoWay("R1-DF670A652982",@probe?.Bot,driveTarget);
                        probe.LearningV32NativeEntryNextMoverRefreshAt = Time.time + 0.12f;
                    }
                }
                // Mover target, body steering and forward input must agree while correcting
                // the entry lane or walking back to a retry staging point.
                Vector3 entryDelta=probe.Player.Position-rawEntry;entryDelta.y=0;
                float lateral=(entryDelta-direction*Vector3.Dot(entryDelta,direction)).magnitude;
                float entryLateralLimit=kindDropDown?NativeDropDownEntryMaxLateralOffset:
                    (commitment.Kind=="Jump"?LearningV352JumpTakeoffCorridorMaxLateral:InjectedNativeVaultRollingMaxLateralOffset);
                Vector3 actualEntryFacing=GetFlatDirection(probe.Player.MovementContext.PlayerRealForward);
                float entryFacingError=actualEntryFacing.sqrMagnitude>.5f?Vector3.Angle(actualEntryFacing,direction):180f;
                bool positionCorrection=FollowerCapabilityFailureSequence.EntryRequiresPointAim(probe.LearningV32NativeEntryReacquireActive,lateral>entryLateralLimit);
                bool correctingEntry=positionCorrection||entryFacingError>InjectedNativeVaultRollingMaxAlignmentAngle;
                Vector3 steeringDirection=positionCorrection ? GetFlatDirection(driveTarget-probe.Player.Position) : direction;
                if(steeringDirection.sqrMagnitude<.5f)steeringDirection=direction;
                probe.Bot.Steering?.BotSteeringLookToDirection("R1-9754FBCBD30C",@probe?.Bot,steeringDirection);
                bool v337ControlledSelfRescue = (probe.LearningV337SelfRescueControlledEntry);
                float entryDriveSpeed = (correctingEntry || v337ControlledSelfRescue)
                    ? 0.55f
                    : (kindClimb ? 0.45f : (probe.PreviousSprinting ? 1.0f : 0.82f));
                ApplyProbeLocomotionIntent(
                    probe,
                    entryDriveSpeed,
                    allowSprint: !kindClimb && !correctingEntry && !v337ControlledSelfRescue);
                HoldForwardInput(probe);
            }
            catch
            {
            }
        }

        private static bool TryBeginLearningV32NativeEntryReacquire(
            ProbeRuntime probe,
            LearnedTraversalCommitment commitment,
            Vector3 rawEntry,
            Vector3 direction,
            float distance,
            float alignmentAngle,
            float lateralEntryOffset,
            float forwardEntryProjection)
        {
            if (probe?.Bot?.Mover == null || probe.Player == null ||
                probe.LearningV33PassageBoundaryState >= LearningV33PassageBoundaryState.CrossingCommitted ||
                probe.LearningV32NativeEntryReacquireAttempts >= LearningV32EntryMaxReacquireAttempts)
            {
                return false;
            }

            Vector3 stagingTarget = rawEntry - direction * LearningV3NativeEntryCorrectionStagingDistance;
            if(IsCapabilityForwardFailurePhase(probe.Bot)&&Vector3.Dot(GetFlatDelta(probe.Player.Position,stagingTarget),direction)<-.14f)
            {
                WriteEvent("capabilitySequenceUpstreamStagingSuppressed",probe.Bot,new{edgeId=probe.LearnedEdgeId,
                    body=Vec(probe.Player.Position),staging=Vec(stagingTarget),nativeSuccessRecorded=false});
                return false; // Use the existing in-place/forward entry convergence, never an earlier segment.
            }
            bool sampled = NavMesh.SamplePosition(
                stagingTarget, out NavMeshHit stagingHit,
                LearningV3NativeEntryCorrectionSampleRadius, NavMesh.AllAreas);
            float verticalTolerance = string.Equals(
                commitment.Kind, "DropDown", StringComparison.OrdinalIgnoreCase) ? 0.35f : 0.75f;
            if (!sampled ||
                GetPlanarDistance(stagingTarget, stagingHit.position) > LearningV3NativeEntryCorrectionMaxSampleOffset ||
                Mathf.Abs(rawEntry.y - stagingHit.position.y) > verticalTolerance)
            {
                WriteEvent("learningV32NativeEntryReacquireRejected", probe.Bot, new
                {
                    edgeId = commitment.EdgeId,
                    kind = commitment.Kind,
                    reason = "stagingSampleInvalid",
                    stagingTarget = Vec(stagingTarget),
                    sampled
                });
                return false;
            }

            stagingTarget = stagingHit.position;
            StrictPathResult stagingPath = CalculateStrictPath(probe.Player.Position, stagingTarget);
            if (stagingPath == null || !stagingPath.StrictComplete)
            {
                WriteEvent("learningV32NativeEntryReacquireRejected", probe.Bot, new
                {
                    edgeId = commitment.EdgeId,
                    kind = commitment.Kind,
                    reason = "stagingUnreachable",
                    stagingTarget = Vec(stagingTarget),
                    status = stagingPath?.Status.ToString() ?? "null",
                    pathLength = stagingPath?.PathLength ?? 0f
                });
                return false;
            }

            // Entry reacquire is explicitly a local correction. PathComplete is not sufficient:
            // runtime edge26 produced a 73.59 m complete path back to a staging point less than a
            // few metres away after the body had crossed the DropDown portal. Reuse the existing
            // adjacent-passage locality contract rather than adding retries or larger tolerances.
            if (stagingPath.PathLength > RouteMissionTraversalAdjacentBridgeMaxDistance)
            {
                WriteEvent("learningV33NativeEntryReacquireRejectedNonLocalRoute", probe.Bot, new
                {
                    edgeId = commitment.EdgeId,
                    kind = commitment.Kind,
                    reason = "stagingRouteNotLocal",
                    stagingTarget = Vec(stagingTarget),
                    directDistance = Vector3.Distance(probe.Player.Position, stagingTarget),
                    pathLength = stagingPath.PathLength,
                    maxLocalPathLength = RouteMissionTraversalAdjacentBridgeMaxDistance,
                    passageState = probe.LearningV33PassageBoundaryState.ToString()
                });
                return false;
            }

            probe.LearningV32NativeEntryReacquireAttempts++;
            probe.LearningV32NativeEntryReacquireActive = true;
            probe.LearningV32NativeEntryReacquireTarget = stagingTarget;
            probe.LearningV32NativeEntryReacquireStartedAt = Time.time;
            probe.StageStartedAt = Time.time;
            probe.LearningV32NativeEntryStartPosition = probe.Player.Position;
            probe.LearningV32NativeEntryStartForwardProjection = forwardEntryProjection;
            probe.LearningV32NativeEntryBestForwardProjection = forwardEntryProjection;
            probe.LearningV32NativeEntryLastPosition = probe.Player.Position;
            probe.LearningV32NativeEntryLastProgressAt = Time.time;
            probe.LearningV32NativeEntryReadySince = -1f;
            probe.LearningV32NativeEntryNextMoverRefreshAt = Time.time + 0.12f;
            probe.PoseExclusiveOrientationAuthority = false;
            try
            {
                probe.Bot.Mover.BotMoverSprint("R1-CE23821478C1",@probe?.Bot,false,false);
                probe.Bot.Mover.LocalAvoidance?.GClass492DropOffset("R1-52556C40E85A",@probe?.Bot);
                probe.Bot.Mover.SetBotMoverPause("R1-B71316D4EA81",@probe?.Bot,false);
                probe.Bot.Mover.BotMoverGoToPointNoWay("R1-B178B56F5C1E",@probe?.Bot,stagingTarget);
                probe.Bot.Steering?.BotSteeringLookToDirection("R1-964776EFAA48",@probe?.Bot,direction);
            }
            catch
            {
            }

            WriteEvent("learningV32NativeEntryReacquireStarted", probe.Bot, new
            {
                edgeId = commitment.EdgeId,
                kind = commitment.Kind,
                attempt = probe.LearningV32NativeEntryReacquireAttempts,
                maxAttempts = LearningV32EntryMaxReacquireAttempts,
                stagingTarget = Vec(stagingTarget),
                pathLength = stagingPath.PathLength,
                distance,
                alignmentAngle,
                lateralEntryOffset,
                forwardEntryProjection,
                action = "reachableStagingThenResumeEntryAcquisition"
            });
            return true;
        }

        private static void MarkLearningV32NativeEntryAcquired(
            ProbeRuntime probe,
            Vector3 rawEntry,
            Vector3 rawExit,
            float distance,
            float alignmentAngle,
            float lateralEntryOffset,
            float forwardEntryProjection,
            string source,
            bool directHandoff)
        {
            if (probe == null)
            {
                return;
            }
            probe.LearningV32NativeEntryAcquired = true;
            CaptureCapabilityNativeJumpEntry(probe);
            probe.LearningV32NativeEntryReadySince = -1f;
            WriteEvent("learningV32NativeEntryAcquired", probe.Bot, new
            {
                edgeId = probe.LearnedEdgeId,
                kind = probe.RequestedTraversalKind,
                source = source ?? string.Empty,
                directHandoff,
                position = Vec(probe.Player?.Position ?? Vector3.zero),
                rawEntry = Vec(rawEntry),
                rawExit = Vec(rawExit),
                distance,
                alignmentAngle,
                lateralEntryOffset,
                forwardEntryProjection,
                reacquireAttempts = probe.LearningV32NativeEntryReacquireAttempts,
                nativeExecutorConsumed = probe.NativeExecutorConsumed
            });
        }

        private static void CaptureCapabilityNativeJumpEntry(ProbeRuntime probe)
        {
            // The shared entry consumer is the same physical witness used by Jump.
            // Do not require an unrelated rolling/reservation branch to mint it again.
            if(FollowerCapabilityPassagePolicy.CaptureJumpEntry(IsProbeLifecycleCurrent(probe),
                probe.NativeStrategySelected,string.Equals(probe.RequestedTraversalKind,"Jump",StringComparison.OrdinalIgnoreCase)))
                probe.JumpEntryCaptured=true;
        }
        private static void BeginLearningV32NativeExecutionEpoch(ProbeRuntime probe, string kind, string source)
        {
            if (probe == null)
            {
                return;
            }
            probe.LearningV32NativeExecutionEpochStartedAt = Time.time;
            probe.LearningV32NativeExecutionEpochKind = kind ?? string.Empty;
            MarkLearningV375ActuatorBorrowed(probe, kind, source);
            WriteEvent("learningV32NativeExecutionEpochStarted", probe.Bot, new
            {
                edgeId = probe.LearnedEdgeId,
                kind = kind ?? string.Empty,
                source = source ?? string.Empty,
                position = Vec(probe.Player?.Position ?? Vector3.zero),
                entryAcquired = probe.LearningV32NativeEntryAcquired,
                nativeExecutorConsumed = probe.NativeExecutorConsumed
            });
        }
        private static void TriggerNativeVaultScan(ProbeRuntime probe, float alignmentAngle, bool velocityReady)
        {
            if(!IsActiveProbe(probe))return;
            BotOwner bot = probe.Bot;
            Player player = probe.Player;
            if (!ValidateProbePreconditions(bot, out string failure))
            {
                if (!TryRecoverPlayerValidatedTraversal(probe, "nativePreconditionChanged"))
                {
                    FinishProbeWithoutReanchor(probe, "preconditionChanged", failure);
                }
                return;
            }

            // Do not StopMove here. V1 did that immediately before TryVaulting(), which made
            // AbsoluteForwardVelocity consistently zero in the first real-world probe. T1.1 keeps
            // forward intent alive long enough for MovementContext/VaultingParameters to observe it.
            bot.Mover?.LocalAvoidance?.GClass492DropOffset("R1-2E95ADD5869D",@bot);
            HoldForwardInput(probe);

            object restrictions = CreateVaultRestrictionPayload(player);
            object beforeParameters = CreateVaultParameterPayload(player);
            bool movementContextResult;
            try
            {
                movementContextResult = player.MovementContext.MovementContextTryVaulting("R1-D273DD9C5A54",@bot);
            }
            catch (Exception ex)
            {
                WriteEvent("vaultProbeException", bot, new { phase = "MovementContext.TryVaulting", error = ex.ToString() });
                if (!TryRecoverPlayerValidatedTraversal(probe, "nativeTryVaultingException"))
                {
                    FinishProbeWithoutReanchor(probe, "tryVaultingException", ex.Message);
                }
                return;
            }

            bool directComponentAttempted = false;
            bool directComponentResult = false;
            string directComponentError = null;
            if (!movementContextResult && player.VaultingComponent != null && !IsPlayerValidatedTraversalProbe(probe))
            {
                // Diagnostic-only fallback. This separates a MovementContext wrapper rejection from
                // a rejection inside EFT's native IVaultingComponent without touching animator state manually.
                directComponentAttempted = true;
                try
                {
                    directComponentResult = player.VaultingComponent.IVaultingComponentTryVaulting("R1-582F33A1CE12",@bot);
                }
                catch (Exception ex)
                {
                    directComponentError = ex.GetType().Name + ": " + ex.Message;
                }
            }

            bool result = movementContextResult || directComponentResult;
            object parameters = CreateVaultParameterPayload(player);
            WriteEvent("vaultProbeScan", bot, new
            {
                result,
                movementContextResult,
                directComponentAttempted,
                directComponentResult,
                directComponentError,
                velocityReady,
                alignmentAngle,
                position = Vec(player.Position),
                forward = Vec(player.MovementContext.PlayerRealForward),
                inputDirection = Vec2(player.InputDirection),
                movementDirection = Vec2(player.MovementContext.MovementDirection),
                isForwardInputDirection = ReadBoolProperty(player, "IsForwardInputDirection"),
                restrictions,
                beforeParameters,
                parameters
            });

            if (!result)
            {
                // V3.49: scanner viability and route semantic truth are independent. The V348
                // bridge DropDown repeatedly entered the legacy Vault scanner, received Strategy=None,
                // terminalized, and immediately recreated the exact same transaction. A demonstrated
                // DropDown therefore vetoes only this scanner and transfers to the existing native
                // DropDown executor; the route edge itself stays authoritative.
                if (FollowerCapabilityPhasePolicy.TypedDropAfterScannerRefusal(true,
                    probe.NativeDropDownAttempted,probe.TraversalKind ?? probe.RequestedTraversalKind) &&
                    TryGetLearnedCommitmentForProbe(probe, out LearnedTraversalCommitment learningV349DropCommitment) &&
                    learningV349DropCommitment != null)
                {
                    TryRecordLearningV349ExecutorStrategyVeto(
                        probe, "LegacyVaultScanner", "MovementContext.TryVaulting returned false for typed DropDown");
                    if (TryBeginNativeDropDown(
                            probe, learningV349DropCommitment,
                            "learningV349VaultScannerRejectedForDropDown",
                            out string learningV349DropDownFailure))
                    {
                        WriteEvent("learningV349ExecutorStrategyTransferred", probe.Bot, new
                        {
                            edgeId = probe.LearnedEdgeId,
                            kind = "DropDown",
                            rejectedStrategy = "LegacyVaultScanner",
                            replacementStrategy = "NativeDropDown",
                            action = "continueSamePhysicalRouteSemanticWithKindCompatibleExecutor"
                        });
                        return;
                    }
                    WriteEvent("learningV349ExecutorStrategyTransferRejected", probe.Bot, new
                    {
                        edgeId = probe.LearnedEdgeId,
                        kind = "DropDown",
                        rejectedStrategy = "LegacyVaultScanner",
                        replacementStrategy = "NativeDropDown",
                        failure = learningV349DropDownFailure ?? string.Empty
                    });
                }

                // V1.3.17: a player-validated Vault/Climb is stronger evidence than Strategy=None.
                // Native refusal immediately transfers ownership to Assisted Replay; only an Assisted
                // failure may use the exact-exit teleport fallback.
                if (TryRecoverPlayerValidatedTraversal(probe, "nativeScanRefused"))
                {
                    return;
                }

                // A low fence can legitimately be crossed by EFT's native Jump even when the native
                // Vault scanner refuses it. Try a real Jump before the synthetic collision-respecting
                // Forward-Hop fallback. This also mirrors how the player may choose to clear a low wall.
                if (TryBeginNativeJumpAfterVaultRefusal(probe, out string jumpFailure))
                {
                    return;
                }

                if (TryBeginSafeForwardHopFallback(probe, out string hopFailure))
                {
                    return;
                }

                FinishProbeWithoutReanchor(
                    probe,
                    "scanRefused",
                    (directComponentAttempted
                        ? "MovementContext.TryVaulting and VaultingComponent.TryVaulting both returned false"
                        : "MovementContext.TryVaulting returned false") +
                    (string.IsNullOrEmpty(jumpFailure) ? string.Empty : $"; jumpFallback={jumpFailure}") +
                    (string.IsNullOrEmpty(hopFailure) ? string.Empty : $"; hopFallback={hopFailure}"));
                return;
            }

            probe.ExclusiveMoverGate = true;
            probe.TriggerPosition = player.Position;
            if (!probe.SawVaultState)
            {
                TransitionProbeStage(probe, ProbeStage.WaitingForState);
                probe.StageStartedAt = Time.time;
            }
            LogInfo($"native vault scan accepted for {GetBotName(bot)} source={(movementContextResult ? "MovementContext" : "VaultingComponent")}");
        }

        private static bool TryTickNativeVaultScanner(Player player, out string error)
        {
            error = string.Empty;
            object component = player?.VaultingComponent;
            if (component == null)
            {
                error = "vaulting component unavailable";
                return false;
            }

            try
            {
                MethodInfo tick = component.GetType().GetMethod(
                    "DoVaultingTick",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    Type.EmptyTypes,
                    null);
                if (tick == null)
                {
                    error = "DoVaultingTick() was not found on " + component.GetType().FullName;
                    return false;
                }

                tick.MethodBaseInvoke("R1-EA25DC0908FE",@player?.AIData?.BotOwner,component,null);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.GetBaseException().GetType().Name + ": " + ex.GetBaseException().Message;
                return false;
            }
        }

        private static bool EnsureVaultingComponent(Player player, out string failure)
        {
            failure = string.Empty;
            if (player?.MovementContext == null)
            {
                failure = "player movement context is unavailable";
                return false;
            }

            try
            {
                if (player.VaultingComponent == null)
                {
                    player.PlayerInitVaultingComponent("R1-20ED4A847FB3",@player?.AIData?.BotOwner,false);
                }

                if (player.VaultingComponent == null || player.VaultingGameplayRestrictions == null)
                {
                    failure = "EFT vaulting is disabled or component initialization returned null";
                    return false;
                }

                if (player.VaultingComponent is GClass2679 nativeComponent)
                {
                    nativeComponent.SetGClass2679Func_0("R1-20558E61DB00",@player?.AIData?.BotOwner,() => false);
                }

                return true;
            }
            catch (Exception ex)
            {
                failure = $"{ex.GetType().Name}: {ex.Message}";
                return false;
            }
        }
    }
}
