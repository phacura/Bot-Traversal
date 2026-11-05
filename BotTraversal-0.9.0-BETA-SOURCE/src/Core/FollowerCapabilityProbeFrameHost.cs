using System;
using System.Globalization;
using EFT;
using UnityEngine;
namespace BotTraversal.Core
{
    internal static partial class FollowerTraversalTestRuntime
    {
        // One frame protocol; this adapter owns no writer, progress clock or success registry.
        private sealed class CapabilityProbeFramePort : ICapabilityProbeFramePort
        {
            private readonly ProbeRuntime probe;
            private BotOwner bot;private Player player;
            internal CapabilityProbeFramePort(ProbeRuntime value){probe=value;}
            public bool IsCurrent=>IsProbeLifecycleCurrent(probe);
            public void RejectLifecycle()
            {
                if(IsActiveProbe(probe))FinishProbeWithoutReanchor(probe,"lifecycleOriginChanged","mission, step or geometry changed");
            }
            public void CaptureActors(){bot=probe.Bot;player=probe.Player;}
            public void ObserveMeaningfulProgress()=>UpdateTraversalMeaningfulProgress(probe);
            public void ObservePhysicalPassage()=>UpdateLearningV349MonotonicPhysicalPassageProof(probe);
            public bool TryImmediateThreat(out string reason)=>FollowerEnvironment.TryGetImmediateSurvivalThreat(bot,out reason);
            public void InterruptForThreat(string reason)=>InterruptTraversalForImmediateSurvival(probe,reason);
            public void IsolateMovement()=>ApplyTraversalMovementIsolation(probe);
            public bool LeaseExpired=>probe.OwnershipLeaseExpiresAt>0f && Time.time>=probe.OwnershipLeaseExpiresAt;
            public void ExpireLease()=>ExpireTraversalOwnershipLease(probe);
            public bool TryCombatClaim(out string reason)=>FollowerStateAuthorityCoordinator.HasCombatMovementClaim(bot,out reason);
            public FollowerCapabilityProbeFramePolicy.Combat CombatAction=>FollowerCapabilityProbeFramePolicy.CombatAction(
                probe.Stage==ProbeStage.WalkThrough,probe.ExclusiveMoverGate,probe.AbortAfterLanding);
            public void AbortWalkThroughForCombat()=>AbortWalkThroughProbe(probe,"combatDuringWalkThrough","combat began during grounded seam traversal",false);
            public void AbortBeforeNativeForCombat()=>FinishProbeWithoutReanchor(probe,"combatBeforeTrigger","combat began before native traversal");
            public void DeferCombat(string reason)
            {
                probe.AbortAfterLanding=true;
                WriteEvent("vaultProbeCombatDeferred",bot,new{state=player.CurrentStateName.ToString(),combatClaimReason=reason});
            }
            public bool PendingWalkThrough=>FollowerCapabilityProbeFramePolicy.StartPendingWalkThrough(
                true,probe.LearningV335WalkThroughCorridorStartPending,
                probe.Stage==ProbeStage.Aligning,probe.RequestedTraversalKind);
            public void StartPendingWalkThrough()
            {
                float pendingAlignment=probe.LearningV335WalkThroughCorridorStartAlignmentAngle;
                probe.LearningV335WalkThroughCorridorStartPending=false;
                WriteEvent("learningV335WalkThroughPostCommitCorridorStartConsumed",probe.Bot,new{
                    edgeId=probe.LearnedEdgeId,chainId=probe.TraversalChainId,alignmentAngle=pendingAlignment,
                    action="startCorridorOnFirstTraversalOwnedTickNoSecondPreflight"});
                BeginWalkThroughProbe(probe,pendingAlignment);
            }
            public void DispatchStage()=>DispatchCapabilityProbeStage(probe,bot,player);
        }
        private static void DispatchCapabilityProbeStage(ProbeRuntime probe,BotOwner bot,Player player)
        {

            switch (probe.Stage)
            {
                case ProbeStage.Aligning:
                    if (string.Equals(probe.RequestedTraversalKind, "WalkThrough", StringComparison.OrdinalIgnoreCase))
                    {
                        UpdateWalkThroughEntryConvergence(probe);
                    }
                    else
                    {
                        UpdateProbeAlignment(probe);
                    }
                    break;
                case ProbeStage.Approaching:
                    UpdateProbeApproaching(probe);
                    break;
                case ProbeStage.RollingApproach:
                    UpdateRollingTraversalApproach(probe);
                    break;
                case ProbeStage.NativeEntryAcquisition:
                    UpdateSharedNativeEntryAcquisition(probe);
                    break;
                case ProbeStage.JumpPriming:
                    UpdateProbeJumpPriming(probe);
                    break;
                case ProbeStage.LearnedJumpTakeoffPriming:
                    UpdateLearnedJumpTakeoffPriming(probe);
                    break;
                case ProbeStage.Priming:
                    UpdateProbePriming(probe);
                    break;
                case ProbeStage.ForwardHop:
                    UpdateForwardHopFallback(probe);
                    break;
                case ProbeStage.NativeDropDown:
                    UpdateNativeDropDown(probe);
                    break;
                case ProbeStage.AssistedReplay:
                    UpdateAssistedTraversalReplay(probe);
                    break;
                case ProbeStage.NativeGroundContinuation:
                    UpdateLearningV331NativeGroundContinuation(probe);
                    break;
                case ProbeStage.WalkThrough:
                    UpdateWalkThroughProbe(probe);
                    break;
                case ProbeStage.Egress:
                    if (probe.UnifiedTraversalFlow)
                    {
                        UpdateUnifiedTraversalEgress(probe);
                    }
                    else
                    {
                        UpdatePostTraversalEgress(probe);
                    }
                    break;
                case ProbeStage.Verify:
                    UpdateUnifiedTraversalVerify(probe);
                    break;
                case ProbeStage.ExitCommit:
                    UpdateExitAuthorityCommit(probe);
                    break;
                case ProbeStage.WaitingForState:
                    if (probe.JumpLandingReleaseGate)
                    {
                        UpdateJumpLandingReleaseGate(probe);
                        break;
                    }
                    if (!probe.JumpAttempted && !probe.InjectedNativeVaultAttempted)
                    {
                        HoldForwardInput(probe);
                    }
                    if ((probe.JumpAttempted) &&
                        string.Equals(probe.TraversalKind ?? probe.RequestedTraversalKind, "Jump", StringComparison.OrdinalIgnoreCase) &&
                        (player.CurrentStateName == EPlayerState.Run || player.CurrentStateName == EPlayerState.Sprint))
                    {
                        Vector3 pendingJumpDirection = GetFlatDirection(probe.RequestedDirection);
                        if (pendingJumpDirection.sqrMagnitude > 0.5f)
                        {
                            PublishLearningV411JumpTakeoffLocomotionContinuum(
                                probe, pendingJumpDirection, "learningV413WaitingForJumpState");
                            if (ShouldEmitDiagnostic(
                                    "learningV413JumpPendingStateLocomotion:" +
                                    probe.LearnedEdgeId.ToString(CultureInfo.InvariantCulture), probe.Bot, 0.12f))
                            {
                                WriteEvent("learningV413JumpPendingStateLocomotionPreserved", probe.Bot, new
                                {
                                    edgeId = probe.LearnedEdgeId,
                                    state = player.CurrentStateName.ToString(),
                                    planarSpeed = GetLearningV329PlanarSpeed(player),
                                    moverSprinting = probe.Bot?.Mover?.Sprinting == true,
                                    action = "keepMeasuredForwardProducerAliveUntilJumpStateOrPhysicalTakeoffWitness"
                                });
                            }
                        }
                    }
                    if (probe.JumpAttempted &&
                        string.Equals(probe.TraversalKind ?? probe.RequestedTraversalKind, "Jump", StringComparison.OrdinalIgnoreCase) &&
                        probe.NativeJumpStateCandidateObserved &&
                        !IsProbeTraversalState(player.CurrentStateName))
                    {
                        bool learningV443PhysicalTakeoff =
                            HasNativeJumpPhysicalTakeoffEvidence(
                                probe, out float rejectedRise, out bool rejectedAirborne);
                        bool learningV456PreservedTakeoffWitness =
                            (probe.NativeJumpStateCandidateObserved) &&
                            rejectedRise >= NativeJumpPhysicalTakeoffMinRise &&
                            probe.LearningV332NativeJumpPhysicalTakeoffEvidenceSince >= 0f;
                        bool learningV443PhysicalWitnessPending =
                            (!learningV443PhysicalTakeoff) &&
                            (rejectedAirborne || learningV456PreservedTakeoffWitness) &&
                            rejectedRise >= NativeJumpPhysicalTakeoffMinRise &&
                            probe.LearningV332NativeJumpPhysicalTakeoffEvidenceSince >= 0f &&
                            Time.time - probe.LearningV332NativeJumpPhysicalTakeoffEvidenceSince <
                                LearnedJumpTakeoffStableSeconds;
                        if ((learningV443PhysicalTakeoff))
                        {
                            RecordLearningV355ConfirmedJumpActuatorLatency(
                                probe, "learningV443StateReleasePhysicalTakeoff");
                            WriteEvent("learningV443JumpTakeoffConfirmedAfterStateRelease", probe.Bot, new
                            {
                                edgeId = probe.LearnedEdgeId,
                                state = player.CurrentStateName.ToString(),
                                rise = rejectedRise,
                                airborne = rejectedAirborne,
                                stableWitnessSeconds = LearnedJumpTakeoffStableSeconds,
                                candidateSeconds = probe.NativeJumpStateCandidateAt > 0f
                                    ? Time.time - probe.NativeJumpStateCandidateAt
                                    : 0f,
                                traversalSuccessRecorded = false,
                                persistentLearningChanged = false,
                                bodyRelocated = false,
                                action = "stableLivePhysicalTakeoffOutranksTransientStateLabelRelease"
                            });
                            BeginTraversalStage(
                                probe, EPlayerState.Jump,
                                "learningV443PhysicalTakeoffProofAfterStateRelease");
                            break;
                        }
                        if (learningV443PhysicalWitnessPending)
                        {
                            Vector3 learningV443PendingDirection =
                                GetFlatDirection(probe.RequestedDirection);
                            if (learningV443PendingDirection.sqrMagnitude > 0.5f)
                            {
                                PublishLearningV411JumpTakeoffLocomotionContinuum(
                                    probe,
                                    learningV443PendingDirection,
                                    "learningV443StateReleasePhysicalWitnessPending");
                            }
                            if (ShouldEmitDiagnostic(
                                    "learningV443JumpStateReleaseAwaitingPhysicalWitness:" +
                                        probe.LearnedEdgeId.ToString(CultureInfo.InvariantCulture),
                                    probe.Bot,
                                    0.05f))
                            {
                                WriteEvent("learningV443JumpStateReleaseAwaitingPhysicalWitness", probe.Bot, new
                                {
                                    edgeId = probe.LearnedEdgeId,
                                    state = player.CurrentStateName.ToString(),
                                    rise = rejectedRise,
                                    airborne = rejectedAirborne,
                                    learningV456MonotonicWitnessPreserved = learningV456PreservedTakeoffWitness,
                                    witnessAge = Mathf.Max(
                                        0f,
                                        Time.time -
                                            probe.LearningV332NativeJumpPhysicalTakeoffEvidenceSince),
                                    stableWitnessSeconds = LearnedJumpTakeoffStableSeconds,
                                    traversalSuccessRecorded = false,
                                    persistentLearningChanged = false,
                                    bodyRelocated = false,
                                    action = learningV456PreservedTakeoffWitness
                                        ? "preserveExistingPhysicalTakeoffWitnessAcrossTransientGroundOrStateFlickerUntilExistingStableWindowMatures"
                                        : "doNotDeclareJumpFailureWhileExistingStablePhysicalTakeoffWitnessIsStillMaturing"
                                });
                            }
                            break;
                        }

                        if (HasLearningV451MonotonicJumpPhysicalPassageProof(probe))
                        {
                            WriteEvent("learningV451MonotonicJumpPassageOutranksTakeoffWitness", probe.Bot, new
                            {
                                edgeId = probe.LearnedEdgeId,
                                state = player.CurrentStateName.ToString(),
                                physicalPassageReason = probe.LearningV349PhysicalPassageReason ?? string.Empty,
                                physicalPassagePosition = Vec(probe.LearningV349PhysicalPassagePosition),
                                passageAge = probe.LearningV349PhysicalPassageCrossedAt > 0f
                                    ? Mathf.Max(0f, Time.time - probe.LearningV349PhysicalPassageCrossedAt)
                                    : 0f,
                                traversalSuccessRecorded = false,
                                persistentLearningChanged = false,
                                bodyRelocated = false,
                                action = "laterSupportedFarSidePassageProofCannotBeDowngradedByMissingEarlierTakeoffWitness"
                            });
                            if (TryRecoverPlayerValidatedTraversal(
                                    probe, "learningV451MonotonicJumpPhysicalPassage"))
                            {
                                return;
                            }
                        }

                        WriteEvent("nativeJumpPhysicalTakeoffRejected", probe.Bot, new
                        {
                            edgeId = probe.LearnedEdgeId,
                            state = player.CurrentStateName.ToString(),
                            rise = rejectedRise,
                            airborne = rejectedAirborne,
                            candidateSeconds = probe.NativeJumpStateCandidateAt > 0f
                                ? Time.time - probe.NativeJumpStateCandidateAt
                                : 0f,
                            reason = "stateReleasedWithoutPhysicalTakeoff"
                        });
                        if (TryRecoverPlayerValidatedTraversal(probe, "nativeJumpPhysicalTakeoffNotObserved"))
                        {
                            return;
                        }
                        if (!probe.JumpFallbackToVault)
                        {
                            FallbackJumpToVault(probe, "Jump state released without physical takeoff evidence");
                        }
                        else
                        {
                            EnterSafetyHold(probe, "Jump state released without physical takeoff evidence");
                        }
                        break;
                    }

                    if (IsProbeTraversalState(player.CurrentStateName))
                    {
                        if (probe.JumpAttempted &&
                            string.Equals(probe.TraversalKind ?? probe.RequestedTraversalKind, "Jump", StringComparison.OrdinalIgnoreCase))
                        {
                            bool physicalTakeoff = HasNativeJumpPhysicalTakeoffEvidence(probe, out float takeoffRise, out bool takeoffAirborne);
                            if (physicalTakeoff)
                            {
                                RecordLearningV355ConfirmedJumpActuatorLatency(probe, "pollPhysicalTakeoff");
                                WriteEvent("nativeJumpPhysicalTakeoffConfirmed", probe.Bot, new
                                {
                                    edgeId = probe.LearnedEdgeId,
                                    state = player.CurrentStateName.ToString(),
                                    rise = takeoffRise,
                                    airborne = takeoffAirborne,
                                    trigger = Vec(probe.TriggerPosition),
                                    position = Vec(player.Position)
                                });
                                BeginTraversalStage(probe, player.CurrentStateName, "physicalTakeoffProof");
                            }
                            else if (!probe.NativeJumpStateCandidateObserved)
                            {
                                probe.NativeJumpStateCandidateObserved = true;
                                probe.NativeJumpStateCandidateAt = Time.time;
                                WriteEvent("nativeJumpStateCandidateObserved", probe.Bot, new
                                {
                                    edgeId = probe.LearnedEdgeId,
                                    state = player.CurrentStateName.ToString(),
                                    rise = takeoffRise,
                                    airborne = takeoffAirborne,
                                    physicalTakeoffConfirmed = false
                                });
                            }

                            if ((!physicalTakeoff) &&
                                probe.LearningV317NativeJumpSemanticTrajectoryAuthority)
                            {
                                // Replace the old RawExit NavMesh writer during the Jump-state
                                // candidate window with the same bounded forward semantic intent
                                // already used by CompileFix46 after physical takeoff. No new time
                                // or distance budget is introduced.
                                ApplyProbeLocomotionIntent(probe, ProbeMomentumFullMoveSpeed, allowSprint: true);
                                probe.Bot?.Steering?.BotSteeringLookToDirection("R1-B02499AA5F83",@bot,probe.RequestedDirection);
                                HoldForwardInput(probe);
                            }
                        }
                        else
                        {
                            BeginTraversalStage(probe, player.CurrentStateName, "polled");
                        }
                    }
                    else
                    {
                        if ((probe.InjectedNativeVaultAttempted) &&
                            player.CurrentStateName == EPlayerState.Transition)
                        {
                            if (!probe.LearningV332NativeVaultPendingObserved)
                            {
                                probe.LearningV332NativeVaultPendingObserved = true;
                                WriteEvent("learningV332NativeVaultPendingFenceArmed", probe.Bot, new
                                {
                                    edgeId = probe.LearnedEdgeId,
                                    kind = probe.TraversalKind ?? probe.RequestedTraversalKind,
                                    elapsed = Time.time - probe.StageStartedAt,
                                    legacyTimeout = InjectedVaultStateEnterTimeout,
                                    pendingTimeout = ProbeStateEnterTimeout,
                                    action = "transitionProvesNativeActuatorPendingKeepNativeAuthority"
                                });
                            }
                        }
                        float stateEnterTimeout = probe.InjectedNativeVaultAttempted
                            ? ((probe.LearningV332NativeVaultPendingObserved)
                                ? ProbeStateEnterTimeout
                                : InjectedVaultStateEnterTimeout)
                            : (IsPlayerValidatedTraversalProbe(probe)
                                ? PlayerValidatedNativeStateEnterTimeout
                                : (probe.JumpAttempted ? ProbeJumpStateEnterTimeout : ProbeStateEnterTimeout));
                        if (Time.time - probe.StageStartedAt <= stateEnterTimeout)
                        {
                            break;
                        }
                        if (probe.InjectedNativeVaultAttempted &&
                            UpdateLearningV382NativeActuatorPendingBodyOwnership(
                                probe,
                                out float learningV382PendingForwardProjection,
                                out float learningV382PendingDisplacement,
                                out float learningV382PendingNoProgressSeconds))
                        {
                            if (ShouldEmitDiagnostic(
                                "learningV382NativeActuatorPendingBodyHold:" +
                                    probe.LearnedEdgeId.ToString(CultureInfo.InvariantCulture),
                                probe.Bot,
                                0.25f))
                            {
                                WriteEvent("learningV382NativeActuatorPendingBodyHold", probe.Bot, new
                                {
                                    edgeId = probe.LearnedEdgeId,
                                    kind = probe.TraversalKind ?? probe.RequestedTraversalKind,
                                    elapsed = Time.time - probe.StageStartedAt,
                                    legacyStateTimeout = stateEnterTimeout,
                                    forwardProjection = learningV382PendingForwardProjection,
                                    displacement = learningV382PendingDisplacement,
                                    noProgressSeconds = learningV382PendingNoProgressSeconds,
                                    existingNoProgressBudget = InjectedNativeVaultEntryNoProgressTimeout,
                                    action = "keepSingleTriggeredNativeTransactionPendingUntilStateOrPhysicalStall"
                                });
                            }
                            break;
                        }
                        if (TryRecoverPlayerValidatedTraversal(probe, "nativeStateDidNotBegin"))
                        {
                            return;
                        }

                        if (probe.JumpAttempted && !probe.JumpFallbackToVault)
                        {
                            FallbackJumpToVault(probe, "native jump did not enter Jump/JumpLanding state");
                        }
                        else
                        {
                            EnterSafetyHold(probe, "native scan succeeded but the traversal state did not begin in time");
                        }
                    }
                    break;
                case ProbeStage.PostLandingCommit:
                    UpdateJumpPostLandingChainCommit(probe);
                    break;
                case ProbeStage.Traversing:
                    if (probe.JumpAttempted &&
                        string.Equals(probe.TraversalKind, "Jump", StringComparison.OrdinalIgnoreCase))
                    {
                        UpdateNativeJumpTrajectoryTelemetry(probe);
                        if (!probe.JumpTakeoffPathReleased)
                        {
                            UpdateNativeJumpAirborneMoverCarry(probe);
                        }
                    }
                    if (!probe.InjectedNativeVaultAttempted &&
                        !string.Equals(probe.TraversalKind, "Jump", StringComparison.OrdinalIgnoreCase))
                    {
                        HoldForwardInput(probe);
                    }
                    if (!IsProbeTraversalState(player.CurrentStateName) && probe.SawVaultState)
                    {
                        BeginLandingStage(probe, "polled");
                    }
                    else if (Time.time - probe.StageStartedAt >
                             (IsPlayerValidatedTraversalProbe(probe) ? PlayerValidatedNativeExecutionTimeout : ProbeExecutionTimeout))
                    {
                        if (!TryRecoverPlayerValidatedTraversal(probe, "nativeTraversalTimeout"))
                        {
                            EnterSafetyHold(probe, "native traversal timeout");
                        }
                    }
                    break;
                case ProbeStage.Landing:
                    if (IsPlayerValidatedTraversalProbe(probe) && !IsProbeTraversalState(player.CurrentStateName))
                    {
                        // V3.24 preserves the existing landing cleanup interval but removes the
                        // visible movement dead-zone once the current body is already a qualified
                        // physical far-side result. Success is still decided by the same later
                        // Egress/Verify/ExitCommit contracts.
                        TryMaintainLearningV324PreExitContinuity(probe, "LandingValidation");
                        // PlayerValidated traversal does not wait for literal Grounded/support. Uneven
                        // tops and lips are valid intermediate states; geometry/exit validation owns
                        // completion. A tiny delay lets EFT finish its native landing state cleanup.
                        if (Time.time - probe.StageStartedAt >= 0.12f &&
                            !TryPreserveCf23NativeVaultClimbLandingProgress(probe))
                        {
                            CompleteProbeAtLanding(probe);
                        }
                    }
                    else if (IsEffectivelySupportedForProbe(bot, player) && !IsProbeTraversalState(player.CurrentStateName))
                    {
                        if (probe.GroundedStableSince < 0f)
                        {
                            probe.GroundedStableSince = Time.time;
                        }
                        else if (Time.time - probe.GroundedStableSince >= ProbeLandingStableTime)
                        {
                            CompleteProbeAtLanding(probe);
                        }
                    }
                    else
                    {
                        probe.GroundedStableSince = -1f;
                    }

                    if (IsActiveProbe(probe) &&
                        Time.time - probe.StageStartedAt >
                        (IsPlayerValidatedTraversalProbe(probe) ? PlayerValidatedLandingTimeout : ProbeLandingTimeout))
                    {
                        if (!TryRecoverPlayerValidatedTraversal(probe, "landingTimeout"))
                        {
                            EnterSafetyHold(probe, "effective grounded landing was not confirmed");
                        }
                    }
                    break;
                case ProbeStage.TeleportArm:
                    UpdateDeferredTraversalTeleport(probe);
                    break;
                case ProbeStage.SafetyHold:
                    UpdateSafetyHoldRecovery(probe);
                    break;
            }
        }
    }
}
