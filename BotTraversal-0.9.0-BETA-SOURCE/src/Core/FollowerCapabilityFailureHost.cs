using System;
using System.Globalization;
using UnityEngine;
namespace BotTraversal.Core
{
    internal static partial class FollowerTraversalTestRuntime
    {
        private static void FinalizeOrEscalateTraversalFailure(ProbeRuntime probe, string reason)
        {
            // A terminal failure must offer the route's complete return before legacy
            // Assist/Teleport can acquire the body and retire its current probe identity.
            if (TryRequestCapabilityFullRouteRetry(probe,reason??"terminalFailure",true)) return;
            bool current = probe != null && IsActiveProbe(probe);
            if (FollowerCapabilityFailurePolicy.Intercept(current,
                current && IsPlayerValidatedTraversalProbe(probe), probe?.TraversalTeleportAttempted == true))
            {
                if (EscalateTraversalFailureMonotonic(probe, reason ?? "unknown"))
                {
                    WriteEvent("traversalTerminalFailureIntercepted", probe.Bot, new
                    {
                        edgeId = probe.LearnedEdgeId,
                        kind = probe.TraversalKind ?? probe.RequestedTraversalKind,
                        stage = probe.Stage.ToString(),
                        reason = reason ?? "unknown",
                        assistedReplayAttempted = probe.AssistedReplayAttempted,
                        recoveryTeleportAttempted = probe.TraversalTeleportAttempted
                    });
                    return;
                }
            }

            // A player-validated step may reach this leaf only when the single recovery teleport
            // has already been attempted/rejected, or when the probe no longer has enough valid
            // authored state to recover safely. This prevents recovery loops.
            FinalizeUnifiedTraversalFailure(probe, reason);
        }

        private static bool EscalateTraversalFailureMonotonic(ProbeRuntime probe, string source)
        {
            // Direct egress/verify callers must enter the same failure ladder as terminal leaves.
            if(TryRequestCapabilityFullRouteRetry(probe,"monotonicFailure:"+source,true))return true;
            if (probe == null || !IsActiveProbe(probe) || !IsPlayerValidatedTraversalProbe(probe))
            {
                return false;
            }

            // V3.66: a terminal recovery is a single-shot transaction.  Once the recovery leaf has
            // attempted its bounded teleport, no Verify/lease/safety caller may resurrect the same
            // failed traversal probe and arm another teleport.  A rejected teleport terminates the
            // attempt as failure; a committed teleport is terminalized immediately at the call site.
            if (FollowerCapabilityFailurePolicy.Initial(true, probe.TraversalTeleportAttempted,
                false, false, false) == CapabilityFailureAction.Terminalize)
            {
                WriteEvent("learningV366RepeatedTerminalRecoverySuppressed", probe.Bot, new
                {
                    edgeId = probe.LearnedEdgeId,
                    kind = probe.TraversalKind ?? probe.RequestedTraversalKind,
                    source = source ?? string.Empty,
                    teleportCommitted = probe.TraversalTeleportCommitted,
                    recoveryExitAlreadySatisfied = probe.RecoveryExitAlreadySatisfied,
                    action = "terminalizeCurrentTraversalTransactionNeverRearmTeleport"
                });
                FinalizeUnifiedTraversalFailure(
                    probe,
                    "learningV366TerminalRecoveryAlreadyAttempted:" + (source ?? "unknown"));
                return true;
            }

            string kind = probe.TraversalKind ?? probe.RequestedTraversalKind ?? string.Empty;
            bool walkThrough = string.Equals(kind, "WalkThrough", StringComparison.OrdinalIgnoreCase);

            // V3.21: a WalkThrough that already transferred from failed full-corridor control to
            // PIT's local monotonic cursor has exhausted two independent physical executors. A
            // recovery teleport would fabricate crossing success and feed bad route knowledge back
            // into learning. Quarantine this follower+edge for the raid and let navigation recovery
            // replan from the REAL body position instead.
            if (FollowerCapabilityFailurePolicy.Initial(false, false, walkThrough, true,
                probe.LearningV321WalkThroughDirectDriveAfterNoProgress) == CapabilityFailureAction.Quarantine)
            {
                QuarantineFollowerTraversalEdge(
                    probe.Bot, probe.LearnedEdgeId, "learningV321WalkThroughDirectDriveExhausted");
                ClearLearnedTraversalCommitment(
                    probe.Bot, probe.LearnedEdgeId, "learningV321WalkThroughDirectDriveExhausted");
                WriteEvent("learningV321WalkThroughDirectDriveExhaustedYielded", probe.Bot, new
                {
                    edgeId = probe.LearnedEdgeId,
                    source = source ?? string.Empty,
                    position = probe.Player != null ? Vec(probe.Player.Position) : null,
                    action = "quarantineAndReplanFromPhysicalBody",
                    legacyTeleportUsed = false
                });
                FinishProbeWithoutReanchor(
                    probe,
                    "learningV321WalkThroughDirectDriveExhaustedYielded",
                    source ?? "WalkThrough direct-drive exhausted");
                return true;
            }

            var preExecution = FollowerCapabilityFailurePolicy.BeforeExecution(
                true, kind, probe.NativeEntryConvergenceRejected,
                probe.NativeDropDownAttempted, probe.LearningV32NativeExecutionEpochStartedAt, probe.JumpAttempted, source);
            bool preEntryDropDownFailure = preExecution == CapabilityFailureAction.PreEntryRetry;
            bool preActuatorJumpFailure = preExecution == CapabilityFailureAction.PreActuatorRetry;
            if (preEntryDropDownFailure || preActuatorJumpFailure)
            {
                if (TryArmLearningV336TraversalSelfRescue(
                        probe,
                        preActuatorJumpFailure
                            ? "preActuatorJump:" + (source ?? "unknown")
                            : "preEntryDropDown:" + (source ?? "unknown"),
                        preActuatorJumpFailure ? "Actuator" : "EntryAcquisition"))
                {
                    return true;
                }
                string retryKey = (probe.Bot?.ProfileId ?? string.Empty) + ":" +
                                  probe.LearnedEdgeId.ToString(CultureInfo.InvariantCulture);
                LearnedTraversalRetryAfter[retryKey] = Time.time + LearnedTraversalRetryCooldown;
                WriteEvent("learningV329PreEntrySyntheticRecoveryDeferred", probe.Bot, new
                {
                    edgeId = probe.LearnedEdgeId,
                    kind,
                    source = source ?? string.Empty,
                    position = probe.Player != null ? Vec(probe.Player.Position) : null,
                    retrySeconds = LearnedTraversalRetryCooldown,
                    executionEpochStarted = false,
                    physicalTraversalAttempted = false,
                    preEntryDropDownFailure,
                    preActuatorJumpFailure,
                    action = "releasePreExecutionAttemptKeepRouteMissionBarrier"
                });
                FinalizeUnifiedTraversalFailure(
                    probe,
                    (preActuatorJumpFailure
                        ? "learningV329PreActuatorJumpMomentumFailure:"
                        : "learningV329PreEntryDropDownApproachFailure:") + (source ?? "unknown"));
                return true;
            }

            // V3.31: V3.29's single-trajectory contract must survive into ExitCommit too.
            // A real Jump may never be converted into a second CharacterController replay merely
            // because the post-landing mover/body reconciled.  One latched valid witness may get
            // one normal ground-egress retry; otherwise terminate and let the same RouteMission
            // step retry later.
            bool learningV331ConsumedPhysicalJump = FollowerCapabilityFailurePolicy.AfterExecution(true,
                kind, probe.JumpAttempted, probe.AssistedReplayAttempted) == CapabilityFailureAction.NativeJumpRetry;
            if (learningV331ConsumedPhysicalJump)
            {
                if (FollowerCapabilityFailurePolicy.CanContinueNativeJump(probe.LearningV330NativeJumpQualifiedLandingWitness,
                    probe.LearningV331JumpExitCommitRecoveryAttempted) &&
                    TryGetLearnedCommitmentForProbe(probe, out LearnedTraversalCommitment learningV331JumpCommitment) &&
                    learningV331JumpCommitment != null)
                {
                    probe.LearningV331JumpExitCommitRecoveryAttempted = true;
                    WriteEvent("learningV331NativeJumpExitCommitForwardRecovery", probe.Bot, new
                    {
                        edgeId = probe.LearnedEdgeId,
                        source = source ?? string.Empty,
                        current = probe.Player != null ? Vec(probe.Player.Position) : null,
                        witness = Vec(probe.LearningV330NativeJumpQualifiedLandingPosition),
                        projection = probe.LearningV330NativeJumpQualifiedLandingProjection,
                        requiredProjection = probe.LearningV330NativeJumpQualifiedLandingRequiredProjection,
                        action = "normalGroundMoverFromCurrentBodyNoAssistNoBackwardReplay"
                    });
                    if (TryBeginLearningV331NativeGroundContinuation(
                            probe, learningV331JumpCommitment, "exitCommitRollback:" + (source ?? "unknown")))
                    {
                        return true;
                    }
                }

                string retryKey = (probe.Bot?.ProfileId ?? string.Empty) + ":" +
                                  probe.LearnedEdgeId.ToString(CultureInfo.InvariantCulture);
                LearnedTraversalRetryAfter[retryKey] = Time.time +
                    ((RouteMissionMoveRetrySeconds));
                WriteEvent("learningV331NativeJumpSingleTrajectoryExitCommitTerminated", probe.Bot, new
                {
                    edgeId = probe.LearnedEdgeId,
                    source = source ?? string.Empty,
                    position = probe.Player != null ? Vec(probe.Player.Position) : null,
                    retrySeconds = (RouteMissionMoveRetrySeconds),
                    action = ("noSameAttemptAssistOrTeleportFastSameStepRetry")
                });
                FinalizeUnifiedTraversalFailure(probe, "learningV331NativeJumpSingleTrajectoryExitCommit:" + (source ?? "unknown"));
                return true;
            }

            // WalkThrough's Direct Corridor controller is already the assisted/forced crossing
            // phase. Every other player-demonstrated traversal gets exactly one AssistedReplay
            // opportunity before the final recovery teleport.
            if (FollowerCapabilityFailurePolicy.AfterExecution(true,
                kind, probe.JumpAttempted, probe.AssistedReplayAttempted) == CapabilityFailureAction.AssistedReplay)
            {
                if (TryBeginAssistedTraversalReplay(
                        probe,
                        "compileFix43Assist:" + (source ?? "unknown"),
                        out string assistFailure))
                {
                    WriteEvent("traversalFailureEscalatedToAssist", probe.Bot, new
                    {
                        edgeId = probe.LearnedEdgeId,
                        kind,
                        source = source ?? "unknown"
                    });
                    return true;
                }

                WriteEvent("traversalAssistUnavailableBeforeRecoveryTeleport", probe.Bot, new
                {
                    edgeId = probe.LearnedEdgeId,
                    kind,
                    source = source ?? "unknown",
                    failure = assistFailure ?? string.Empty
                });
            }

            return TryTeleportPlayerValidatedTraversalAtExit(
                probe,
                "compileFix43RecoveryTeleport:" + (source ?? "unknown"));
        }
    }
}
