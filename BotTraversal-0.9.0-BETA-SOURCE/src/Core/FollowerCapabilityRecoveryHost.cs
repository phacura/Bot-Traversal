using System;
using EFT;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.AI;
namespace BotTraversal.Core
{
    internal static partial class FollowerTraversalTestRuntime
    {
        private static void BeginJumpLandingReleaseGate(ProbeRuntime probe, string source)
        {
            if (probe == null || !IsActiveProbe(probe))
            {
                return;
            }

            if (probe.Player.CurrentStateName != EPlayerState.JumpLanding)
            {
                BeginUnifiedTraversalEgressOrVerify(probe, source + ":alreadyReleased");
                return;
            }

            TransitionProbeStage(probe, ProbeStage.WaitingForState);
            probe.StageStartedAt = Time.time;
            probe.JumpLandingReleaseGate = true;
            probe.JumpLandingReleaseSource = source;
            probe.JumpLandingReleaseRepairAt = Time.time + JumpLandingNaturalReleaseWaitSeconds;
            probe.JumpLandingRepairAttempted = false;
            probe.JumpLandingRepairAttempts = 0;
            probe.JumpLandingHardRecoveryAttempted = false;
            probe.OwnershipLeaseExpiresAt = Mathf.Max(
                probe.OwnershipLeaseExpiresAt,
                Time.time + JumpLandingHardRecoverySeconds + 2.0f);

            try
            {
                probe.Bot.BotOwnerStopMove("R1-1FC267E8449A",@probe?.Bot);
                probe.Bot.Mover.SetBotMoverPause("R1-060D236C7F1B",@probe?.Bot,false);
                if (probe.Bot.Mover.Sprinting)
                {
                    probe.Bot.Mover.BotMoverSprint("R1-1AFB9CB28A0B",@probe?.Bot,false,false);
                }
                probe.Bot.BotOwnerSetTargetMoveSpeed("R1-B1D81C4D76A5",@probe?.Bot,0.65f);
                HoldForwardInput(probe);
            }
            catch
            {
            }

            WriteEvent("jumpLandingReleaseGateStarted", probe.Bot, new
            {
                edgeId = probe.LearnedEdgeId,
                source,
                position = Vec(probe.Player.Position),
                grounded = probe.Player.MovementContext.IsGrounded,
                state = probe.Player.CurrentStateName.ToString()
            });
        }

        private static bool NeedsJumpPostLandingChainCommit(ProbeRuntime probe)
        {
            if (probe?.Player?.MovementContext == null ||
                !string.Equals(probe.TraversalKind ?? probe.RequestedTraversalKind, "Jump", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // CompileFix45: a genuine MovementContext.TryJump() success must preserve forward
            // locomotion into the successor edge.  The legacy post-landing commit deliberately
            // stopped BotMover, zeroed InputDirection and waited on a fixed anchor; Fix44 telemetry
            // showed that this can turn a real Jump -> DropDown chain into a stationary 0.16 s
            // commit and can even bless an anti-rollback correction as success.
            //
            // TryFinalizeTraversalChainIntermediate() already requires the body to be supported
            // and within the successor handoff envelope before this predicate is evaluated, so a
            // native Jump that reached that point needs no extra stationary authority phase.
            if (probe.NativeStrategySelected && probe.JumpAttempted && probe.NativeTraversalStarted)
            {
                return false;
            }

            EPlayerState state = probe.Player.CurrentStateName;
            bool nativeLandingState = FollowerCapabilityHelperRules.NeedsJumpPostLandingChainCommit_nativeLandingState_746BD1E0_1((int)(state),(int)(EPlayerState.JumpLanding),(int)(state),(int)(EPlayerState.Transition)) ||
                                      IsProbeTraversalState(state);
            bool supported = IsEffectivelySupportedForProbe(probe.Bot, probe.Player);
            return nativeLandingState || !supported;
        }

        private static void BeginJumpPostLandingChainCommit(ProbeRuntime probe, string source)
        {
            if (probe == null || !IsActiveProbe(probe) || probe.Player?.MovementContext == null)
            {
                return;
            }
            if (probe.Stage == ProbeStage.PostLandingCommit)
            {
                return;
            }

            TransitionProbeStage(probe, ProbeStage.PostLandingCommit);
            probe.StageStartedAt = Time.time;
            probe.JumpPostLandingCommitStartedAt = Time.time;
            probe.JumpPostLandingStableSince = -1f;
            probe.JumpPostLandingCommitLastPosition = probe.Player.Position;
            probe.JumpPostLandingCommitAnchor = probe.Player.Position;
            probe.JumpPostLandingAnchorCorrectionAttempts = 0;
            bool initialGrounded = probe.Player.MovementContext.IsGrounded;
            probe.JumpPostLandingLastGroundedAt = initialGrounded ? Time.time : -1f;
            probe.JumpPostLandingGroundedEvidenceSamples = initialGrounded ? 1 : 0;
            probe.JumpPostLandingLastResetReason = string.Empty;
            probe.JumpPostLandingCommitPolicyFailure = false;
            probe.JumpPostLandingCommitSource = source ?? "jumpChainIntermediate";
            probe.ExclusiveMoverGate = true;
            probe.OwnershipLeaseExpiresAt = Mathf.Max(
                probe.OwnershipLeaseExpiresAt,
                Time.time + JumpPostLandingCommitMaxSeconds + 0.75f);
            try
            {
                probe.Bot?.Mover?.BotMoverStop("R1-0F5F8F101382",@probe?.Bot);
                if (probe.Bot?.Mover != null)
                {
                    probe.Bot.Mover.SetBotMoverPause("R1-14AE9A0315AE",@probe?.Bot,true);
                }
                probe.Player.SetPlayerInputDirection("R1-C28B9DE2D08D",@probe?.Bot,Vector2.zero);
                probe.Player.MovementContext.SetMovementContextMovementDirection("R1-45D0E66119A9",@probe?.Bot,Vector2.zero);
                // Flush the residual approach vector too. Fix32 already stopped BotMover, but
                // runtime still showed the native Jump/Transition writer moving the body ~40 ms
                // later; zero ApplyApproachMotion keeps MovementContext itself synchronized while
                // the landing state settles.
                probe.Player.MovementContext.TryMovementContextApplyApproachMotion(
"R1-734287B9F7AB",@probe?.Bot,                    Vector3.zero,Mathf.Max(0.001f, Time.deltaTime));
            }
            catch
            {
            }
            WriteEvent("jumpPostLandingChainCommitArmed", probe.Bot, new
            {
                edgeId = probe.LearnedEdgeId,
                chainId = probe.TraversalChainId,
                source = probe.JumpPostLandingCommitSource,
                state = probe.Player.CurrentStateName.ToString(),
                grounded = probe.Player.MovementContext.IsGrounded,
                position = Vec(probe.Player.Position),
                landingAnchor = Vec(probe.JumpPostLandingCommitAnchor),
                stableSeconds = JumpPostLandingCommitStableSeconds,
                maxSeconds = JumpPostLandingCommitMaxSeconds
            });
        }

        private static void UpdateJumpPostLandingChainCommit(ProbeRuntime probe)
        {
            if (probe == null || !IsActiveProbe(probe) || probe.Player?.MovementContext == null)
            {
                return;
            }

            try
            {
                probe.Bot?.Mover?.BotMoverStop("R1-D86BB88C0171",@probe?.Bot);
                if (probe.Bot?.Mover != null)
                {
                    probe.Bot.Mover.SetBotMoverPause("R1-CCC7D875AD10",@probe?.Bot,true);
                }
                probe.Player.SetPlayerInputDirection("R1-025418E801BB",@probe?.Bot,Vector2.zero);
                probe.Player.MovementContext.SetMovementContextMovementDirection("R1-4DC064042B85",@probe?.Bot,Vector2.zero);
                probe.Player.MovementContext.TryMovementContextApplyApproachMotion(
"R1-C9F1B6EBD939",@probe?.Bot,                    Vector3.zero,Mathf.Max(0.001f, Time.deltaTime));
            }
            catch
            {
            }

            EPlayerState state = probe.Player.CurrentStateName;
            bool stateHealthy = FollowerCapabilityHelperRules.UpdateJumpPostLandingChainCommit_stateHealthy_F8FF73A2_1((int)(state),(int)(EPlayerState.JumpLanding),(int)(state),(int)(EPlayerState.Transition)) &&
                                !IsProbeTraversalState(state);
            bool actualGrounded = probe.Player.MovementContext.IsGrounded;
            if (actualGrounded)
            {
                probe.JumpPostLandingLastGroundedAt = Time.time;
                probe.JumpPostLandingGroundedEvidenceSamples = Math.Min(
                    8, probe.JumpPostLandingGroundedEvidenceSamples + 1);
            }
            float groundedEvidenceAge = probe.JumpPostLandingLastGroundedAt > 0f
                ? Mathf.Max(0f, Time.time - probe.JumpPostLandingLastGroundedAt)
                : float.MaxValue;
            bool groundedEvidence = actualGrounded ||
                (FollowerCapabilityHelperRules.UpdateJumpPostLandingChainCommit_groundedEvidence_F8FF73A2_2(probe.JumpPostLandingGroundedEvidenceSamples,JumpPostLandingGroundedEvidenceSamplesRequired,groundedEvidenceAge,JumpPostLandingGroundedEvidenceGraceSeconds));
            bool supported = IsEffectivelySupportedForProbe(probe.Bot, probe.Player);
            Vector3 position = probe.Player.Position;
            Vector3 anchor = IsFinite(probe.JumpPostLandingCommitAnchor)
                ? probe.JumpPostLandingCommitAnchor
                : position;
            Vector3 anchorDelta = position - anchor;
            float anchorPlanarDisplacement = new Vector2(anchorDelta.x, anchorDelta.z).magnitude;
            float anchorVerticalDisplacement = Mathf.Abs(anchorDelta.y);
            float anchorThreeDimensionalDisplacement = anchorDelta.magnitude;
            bool anchorSafe = FollowerCapabilityHelperRules.UpdateJumpPostLandingChainCommit_anchorSafe_F8FF73A2_3(anchorPlanarDisplacement,JumpPostLandingAnchorMaxPlanarDisplacement,anchorVerticalDisplacement,JumpPostLandingAnchorMaxVerticalDisplacement,anchorThreeDimensionalDisplacement,JumpPostLandingAnchorMaxThreeDimensionalDisplacement);

            float sampleDisplacement = IsFinite(probe.JumpPostLandingCommitLastPosition)
                ? Vector3.Distance(position, probe.JumpPostLandingCommitLastPosition)
                : 0f;
            probe.JumpPostLandingCommitLastPosition = position;
            bool sampleStable = sampleDisplacement <= JumpPostLandingCommitMaxSampleDisplacement;

            // CompileFix45: anti-rollback may diagnose a failed assisted Jump, but it must never
            // manufacture traversal success by teleporting the body back to the remembered anchor.
            // If the body falls/rolls back after the traversal state released, the physical Jump
            // did not hold.  Mark the commit policy failed and advance monotonically to the normal
            // Assist/Recovery ladder instead of correcting to the anchor and later counting the
            // stationary body as FullEgressSuccess.
            if (stateHealthy && !anchorSafe)
            {
                bool anchorNavStable = NavMesh.SamplePosition(
                        anchor, out NavMeshHit anchorNavHit,
                        JumpPostLandingAnchorCorrectionNavRadius, NavMesh.AllAreas) &&
                    Mathf.Abs(anchorNavHit.position.y - anchor.y) <= JumpPostLandingAnchorCorrectionMaxNavVertical;
                probe.JumpPostLandingCommitPolicyFailure = true;
                probe.JumpPostLandingStableSince = -1f;
                probe.JumpPostLandingLastResetReason = "physicalRollbackDetected";
                WriteEvent("jumpPostLandingRollbackDetected", probe.Bot, new
                {
                    edgeId = probe.LearnedEdgeId,
                    chainId = probe.TraversalChainId,
                    state = state.ToString(),
                    grounded = actualGrounded,
                    landingAnchor = Vec(anchor),
                    current = Vec(position),
                    anchorPlanarDisplacement,
                    anchorVerticalDisplacement,
                    anchorThreeDimensionalDisplacement,
                    anchorNavStable,
                    action = "rejectSuccessAndEscalate"
                });

                // Native Jump normally bypasses this stationary commit entirely.  If an assisted
                // fallback reached this legacy commit and then rolled back, AssistedReplayAttempted
                // is already true, therefore the monotonic failure escalator proceeds to Recovery
                // instead of replaying/correcting the same failed landing.
                FinalizeOrEscalateTraversalFailure(probe, "jumpPostLandingPhysicalRollback");
                return;
            }
            else
            {
                bool postLandingStable = FollowerCapabilityHelperRules.UpdateJumpPostLandingChainCommit_postLandingStable_F8FF73A2_4(stateHealthy,groundedEvidence,supported,sampleStable,anchorSafe);
                if (postLandingStable)
                {
                    if (probe.JumpPostLandingStableSince < 0f)
                    {
                        probe.JumpPostLandingStableSince = Time.time;
                        probe.JumpPostLandingLastResetReason = string.Empty;
                        WriteEvent("jumpPostLandingChainCommitCandidate", probe.Bot, new
                        {
                            edgeId = probe.LearnedEdgeId,
                            state = state.ToString(),
                            grounded = actualGrounded,
                            groundedEvidence,
                            groundedEvidenceAge,
                            groundedEvidenceSamples = probe.JumpPostLandingGroundedEvidenceSamples,
                            position = Vec(position),
                            landingAnchor = Vec(anchor),
                            sampleDisplacement,
                            anchorPlanarDisplacement,
                            anchorVerticalDisplacement,
                            anchorThreeDimensionalDisplacement
                        });
                    }
                    else if (Time.time - probe.JumpPostLandingStableSince >= JumpPostLandingCommitStableSeconds)
                    {
                        probe.JumpPostLandingCommitSatisfied = true;
                        string source = probe.JumpPostLandingCommitSource ?? "jumpChainIntermediate";
                        WriteEvent("jumpPostLandingChainCommitSatisfied", probe.Bot, new
                        {
                            edgeId = probe.LearnedEdgeId,
                            chainId = probe.TraversalChainId,
                            source,
                            state = state.ToString(),
                            grounded = actualGrounded,
                            groundedEvidence,
                            groundedEvidenceAge,
                            groundedEvidenceSamples = probe.JumpPostLandingGroundedEvidenceSamples,
                            position = Vec(position),
                            landingAnchor = Vec(anchor),
                            anchorPlanarDisplacement,
                            anchorVerticalDisplacement,
                            anchorThreeDimensionalDisplacement,
                            stableFor = Time.time - probe.JumpPostLandingStableSince,
                            elapsed = Time.time - probe.JumpPostLandingCommitStartedAt
                        });
                        if (TryFinalizeTraversalChainIntermediate(probe, source + ":postLandingStable"))
                        {
                            return;
                        }
                        BeginUnifiedTraversalEgressOrVerify(probe, source + ":postLandingStable");
                        return;
                    }
                }
                else if (probe.JumpPostLandingStableSince >= 0f)
                {
                    string resetReason = !stateHealthy ? "stateNotReleased" :
                        !anchorSafe ? "anchorUnsafe" :
                        !supported ? "unsupported" :
                        !sampleStable ? "sampleMoved" :
                        !groundedEvidence ? "groundedEvidenceExpired" : "unknown";
                    probe.JumpPostLandingLastResetReason = resetReason;
                    WriteEvent("jumpPostLandingChainCommitReset", probe.Bot, new
                    {
                        edgeId = probe.LearnedEdgeId,
                        chainId = probe.TraversalChainId,
                        resetReason,
                        state = state.ToString(),
                        grounded = actualGrounded,
                        groundedEvidence,
                        groundedEvidenceAge,
                        groundedEvidenceSamples = probe.JumpPostLandingGroundedEvidenceSamples,
                        supported,
                        sampleStable,
                        anchorSafe,
                        stableFor = Time.time - probe.JumpPostLandingStableSince,
                        sampleDisplacement,
                        anchorPlanarDisplacement,
                        anchorVerticalDisplacement,
                        anchorThreeDimensionalDisplacement
                    });
                    probe.JumpPostLandingStableSince = -1f;
                }
            }

            float elapsed = Time.time - probe.JumpPostLandingCommitStartedAt;
            if (elapsed >= JumpPostLandingCommitMaxSeconds)
            {
                probe.JumpPostLandingCommitPolicyFailure = true;
                WriteEvent("jumpPostLandingChainCommitTimeout", probe.Bot, new
                {
                    edgeId = probe.LearnedEdgeId,
                    chainId = probe.TraversalChainId,
                    state = state.ToString(),
                    supported,
                    grounded = actualGrounded,
                    groundedEvidence,
                    groundedEvidenceAge,
                    groundedEvidenceSamples = probe.JumpPostLandingGroundedEvidenceSamples,
                    lastResetReason = probe.JumpPostLandingLastResetReason,
                    anchorSafe,
                    anchorPlanarDisplacement,
                    anchorVerticalDisplacement,
                    anchorThreeDimensionalDisplacement,
                    correctionAttempts = probe.JumpPostLandingAnchorCorrectionAttempts,
                    sampleDisplacement,
                    elapsed,
                    position = Vec(position),
                    landingAnchor = Vec(anchor)
                });
                EnterSafetyHold(probe, "jump post-landing native transition did not settle at verified anchor");
            }
        }

        private static void UpdateJumpLandingReleaseGate(ProbeRuntime probe)
        {
            if (probe == null || !IsActiveProbe(probe) || probe.Player?.MovementContext == null)
            {
                return;
            }

            if (FollowerCapabilityPhasePolicy.LandingStateReleased((int)probe.Player.CurrentStateName,(int)EPlayerState.JumpLanding))
            {
                string source = probe.JumpLandingReleaseSource ?? "jumpLandingRelease";
                probe.JumpLandingReleaseGate = false;
                WriteEvent("jumpLandingReleaseGateCompleted", probe.Bot, new
                {
                    edgeId = probe.LearnedEdgeId,
                    source,
                    elapsed = Time.time - probe.StageStartedAt,
                    nextState = probe.Player.CurrentStateName.ToString(),
                    position = Vec(probe.Player.Position)
                });
                BeginUnifiedTraversalEgressOrVerify(probe, source + ":stateReleased");
                return;
            }

            // Do not run PIT's post-landing egress servo while EFT still owns JumpLanding.
            // Forward intent is enough for a healthy EFT state to naturally become Run/Idle.
            HoldForwardInput(probe);
            if (probe.Bot?.Mover != null)
            {
                probe.Bot.Mover.SetBotMoverPause("R1-ADC86B960A13",@probe?.Bot,false);
                if (probe.Bot.Mover.Sprinting)
                {
                    probe.Bot.Mover.BotMoverSprint("R1-0823D58D8CF0",@probe?.Bot,false,false);
                }
                probe.Bot.BotOwnerSetTargetMoveSpeed("R1-50A10FACAB18",@probe?.Bot,0.65f);
            }

            float elapsed = Time.time - probe.StageStartedAt;
            if (FollowerCapabilityPhasePolicy.SoftLandingRepair(Time.time,probe.JumpLandingReleaseRepairAt,
                probe.JumpLandingRepairAttempts,JumpLandingMaximumSoftRepairAttempts,elapsed,JumpLandingHardRecoverySeconds))
            {
                bool repaired = TryRepairStuckJumpLanding(probe.Bot, probe, "activeProbeGate", out string detail);
                probe.JumpLandingRepairAttempted = true;
                probe.JumpLandingRepairAttempts++;
                probe.JumpLandingReleaseRepairAt = Time.time + JumpLandingRepairCooldownSeconds;
                WriteEvent(repaired ? "jumpLandingStateRecovered" : "jumpLandingStateRecoveryAttempt", probe.Bot, new
                {
                    edgeId = probe.LearnedEdgeId,
                    source = "activeProbeGate",
                    elapsed,
                    attempt = probe.JumpLandingRepairAttempts,
                    state = probe.Player.CurrentStateName.ToString(),
                    repaired,
                    detail
                });

                if (repaired && FollowerCapabilityPhasePolicy.LandingStateReleased((int)probe.Player.CurrentStateName,(int)EPlayerState.JumpLanding))
                {
                    return;
                }
            }

            if (FollowerCapabilityPhasePolicy.HardLandingRepair(elapsed,JumpLandingHardRecoverySeconds,
                probe.JumpLandingRepairAttempts,JumpLandingMaximumSoftRepairAttempts,probe.JumpLandingHardRecoveryAttempted))
            {
                probe.JumpLandingHardRecoveryAttempted = true;
                bool repaired = TryHardRecoverStuckJumpLanding(probe.Bot, probe, "activeProbeHard", out string detail);
                WriteEvent(repaired ? "jumpLandingStateHardRecovered" : "jumpLandingStateHardRecoveryAttempt", probe.Bot, new
                {
                    edgeId = probe.LearnedEdgeId,
                    source = "activeProbeHard",
                    elapsed,
                    softAttempts = probe.JumpLandingRepairAttempts,
                    state = probe.Player.CurrentStateName.ToString(),
                    repaired,
                    detail
                });
            }
        }

        private static bool TryRecoverPlayerValidatedTraversal(ProbeRuntime probe, string source)
        {
            if (probe == null || !IsLiveBot(probe.Bot) || !IsPlayerValidatedTraversalProbe(probe) ||
                !TryGetLearnedCommitmentForProbe(probe, out LearnedTraversalCommitment commitment) || commitment == null)
            {
                return probe?.LifecycleMission!=null&&TryRequestCapabilityFullRouteRetry(probe,"nativeRecoveryUnvalidated:"+source,true);
            }

            if(TryCloseCapabilityClimbAtSuccessor(probe,commitment))return true;
            bool zeroShotTransfer = IsLearningV319TransientSkillTransfer(commitment);
            Vector3 requestedExit = IsFinite(commitment.RawExit) ? commitment.RawExit : commitment.Exit;
            if (!IsFinite(requestedExit))
            {
                return false;
            }

            float exitDistance = GetPlanarDistance(probe.Player.Position, requestedExit);

            // V4.41: when a genuine Native Jump body is physically standing at the entry of the
            // exact typed successor, that successor is the completion consumer. The route/action
            // profiles already encode whether this boundary may live off NavMesh; requiring an
            // unrelated ground NavMesh sample here made a successful fence-top Jump fail, move
            // backward, and replay Vault/egress before DropDown could ever own the body.
            if (TryProveLearningV441JumpTypedSuccessorFrontier(
                    probe, commitment,
                    out TraversalEpisodeMetadata learningV441CurrentEpisode,
                    out TraversalEpisodeMetadata learningV441SuccessorEpisode,
                    out LearnedTraversalEdge learningV441SuccessorEdge,
                    out Vector3 learningV441SuccessorEntry,
                    out float learningV441SuccessorDistance,
                    out float learningV441ForwardProjection,
                    out float learningV441RequiredProjection,
                    out float learningV441VerticalDelta,
                    out string learningV441AuthoritySource))
            {
                if (TryFinalizeTraversalChainIntermediate(
                        probe, "learningV441JumpTypedSuccessorOffMeshHandoff"))
                {
                    WriteEvent("learningV441JumpTypedSuccessorOffMeshHandoff", probe.Bot, new
                    {
                        edgeId = probe.LearnedEdgeId,
                        episodeId = learningV441CurrentEpisode?.EpisodeId ?? 0,
                        successorEdgeId = learningV441SuccessorEdge?.Id ?? 0,
                        successorEpisodeId = learningV441SuccessorEpisode?.EpisodeId ?? 0,
                        successorKind = learningV441SuccessorEdge?.Kind ?? string.Empty,
                        authoritySource = learningV441AuthoritySource,
                        position = Vec(probe.Player.Position),
                        successorEntry = Vec(learningV441SuccessorEntry),
                        successorDistance = learningV441SuccessorDistance,
                        verticalDelta = learningV441VerticalDelta,
                        forwardProjection = learningV441ForwardProjection,
                        requiredProjection = learningV441RequiredProjection,
                        navMeshRequired = false,
                        genericEgressStarted = false,
                        assistedReplayStarted = false,
                        bodyRelocated = false,
                        persistentLearningChanged = false,
                        action = "nativeJumpDebtTransfersAtomicallyToTypedSuccessorBeforeGenericEgressOrRecovery"
                    });
                    return true;
                }
                WriteEvent("learningV441JumpTypedSuccessorHandoffDeferred", probe.Bot, new
                {
                    edgeId = probe.LearnedEdgeId,
                    successorEdgeId = learningV441SuccessorEdge?.Id ?? 0,
                    successorKind = learningV441SuccessorEdge?.Kind ?? string.Empty,
                    authoritySource = learningV441AuthoritySource,
                    successorDistance = learningV441SuccessorDistance,
                    forwardProjection = learningV441ForwardProjection,
                    requiredProjection = learningV441RequiredProjection,
                    action = "physicalFrontierProvenButCanonicalSuccessorConsumerNotYetReadyKeepExistingProbe"
                });
            }

            bool nativeFarSide = IsNativeTraversalAlreadyOnFarSide(
                probe,
                commitment,
                requestedExit,
                out float nativeForwardProjection,
                out float nativeRequiredProjection);
            NavMeshHit currentNav = default;
            bool currentNavSampled =
                NavMesh.SamplePosition(probe.Player.Position, out currentNav, 1.35f, NavMesh.AllAreas);
            bool learningV451MonotonicOffNavMeshJumpCompletion =
                HasLearningV451MonotonicJumpPhysicalPassageProof(probe) &&
                GetLearningV354ActionProfile("Jump").CanFinishOffNavMesh;
            if (FollowerCapabilityPhasePolicy.FarSideRecoveryAccepted(nativeFarSide,currentNavSampled,learningV451MonotonicOffNavMeshJumpCompletion))
            {
                // V1.3.17: proximity to RawEnd alone is never Native success. The v1.3.16 Climb
                // test was only 0.84m away horizontally while still ~1.14m below the authored exit,
                // causing a false completion immediately after TryVaulting() returned false. A real
                // EFT traversal state plus far-side/vertical progress is now mandatory.
                WriteEvent("nativeTraversalFarSideAccepted", probe.Bot, new
                {
                    source,
                    exitDistance,
                    nativeFarSide,
                    forwardProjection = nativeForwardProjection,
                    requiredProjection = nativeRequiredProjection,
                    position = Vec(probe.Player.Position),
                    requestedExit = Vec(requestedExit),
                    navMeshSampled = currentNavSampled,
                    monotonicOffNavMeshJumpCompletion = learningV451MonotonicOffNavMeshJumpCompletion
                });
                probe.TraversalEgressTimedOut = false;
                if ((string.Equals(commitment.Kind, "Jump", StringComparison.OrdinalIgnoreCase)))
                {
                    probe.NativePartialCrossingObserved = false;
                }
                probe.TraversalVerifyTarget = currentNavSampled
                    ? currentNav.position
                    : probe.Player.Position;
                bool learningV425VaultClimbAirborneFarSide =
                    (!probe.Player.MovementContext.IsGrounded) &&
                    (string.Equals(commitment.Kind, "Vault", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(commitment.Kind, "Climb", StringComparison.OrdinalIgnoreCase));
                if (learningV425VaultClimbAirborneFarSide &&
                    TryFinalizeTraversalChainIntermediate(
                        probe, "learningV425AirborneFarSideSuccessorEpochHandoff"))
                {
                    WriteEvent("learningV425AirborneVaultClimbSuccessorEpochHandoff", probe.Bot, new
                    {
                        edgeId = probe.LearnedEdgeId,
                        kind = commitment.Kind,
                        source = source ?? string.Empty,
                        position = Vec(probe.Player.Position),
                        requestedExit = Vec(requestedExit),
                        forwardProjection = nativeForwardProjection,
                        requiredProjection = nativeRequiredProjection,
                        grounded = false,
                        bodyRelocated = false,
                        action = "nativeFarSideBodyHandsDirectlyToImmediateCanonicalSuccessorBeforeGenericEgressCanPullAirborneBody"
                    });
                    return true;
                }
                BeginUnifiedTraversalEgressOrVerify(probe, "nativeExitSideCompleted");
                return true;
            }

            // A landing callback is not a failed attempt. Keep the existing physical
            // forward continuations ahead of terminal disposition; never replay Native.
            if (FollowerCapabilityPhasePolicy.NativeRecoveryConsumed(probe.InjectedNativeVaultAttempted,probe.NativeExecutorConsumed))
            {
                if (TryBeginNativeTraversalRunOut(probe,commitment,source)) return true;
                if (FollowerCapabilityFailureSequence.ContinueExecutedJump(IsProbeLifecycleCurrent(probe),
                    probe.NativeExecutorConsumed,probe.JumpAttempted,probe.NativeTraversalStarted,
                    probe.NativePartialCrossingObserved,probe.NativeLandingRejected) &&
                    string.Equals(commitment.Kind,"Jump",StringComparison.OrdinalIgnoreCase) &&
                    TryBeginLearningV408JumpPartialCrossingForwardContinuation(probe,commitment,source,"Landing")) return true;
            }
            if(probe?.LifecycleMission!=null&&TryRequestCapabilityFullRouteRetry(probe,"nativeRecovery:"+source,true))return true;

            if (FollowerCapabilityPhasePolicy.NativeRecoveryConsumed(probe.InjectedNativeVaultAttempted,probe.NativeExecutorConsumed))
            {
                // First prefer a genuine far-side native run-out. Once a native state has started,
                // the native executor is permanently consumed for this probe: recovery continues
                // from the current position and may never trigger DoVault/DoClimb a second time.
                if (TryBeginNativeTraversalRunOut(probe, commitment, source))
                {
                    return true;
                }

                var observed = FollowerCapabilityRecoveryPolicy.Observed(
                    FollowerCapabilityPhasePolicy.NativeRecoveryObserved(probe.NativeStateObserved,probe.NativeTraversalStarted,probe.SawVaultState),
                    FollowerCapabilityPhasePolicy.JumpLandingRecovery(probe.NativeLandingRejected,commitment.Kind));
                if (observed != NativeRecoveryObservation.AwaitingNativeState)
                {
                    if (observed == NativeRecoveryObservation.RejectedJumpLanding)
                    {
                        if (FollowerCapabilityRecoveryPolicy.PreserveJumpTrajectory(true,probe.JumpAttempted))
                        {
                            if (TryBeginLearningV408JumpPartialCrossingForwardContinuation(
                                    probe, commitment, source, "Landing"))
                            {
                                return true;
                            }
                            if (TryArmLearningV336TraversalSelfRescue(
                                    probe, "jumpPartialCrossing:" + source, "Landing"))
                            {
                                return true;
                            }
                            string retryKey = (probe.Bot?.ProfileId ?? string.Empty) + ":" +
                                              probe.LearnedEdgeId.ToString(CultureInfo.InvariantCulture);
                            LearnedTraversalRetryAfter[retryKey] = Time.time + ((RouteMissionMoveRetrySeconds));
                            WriteEvent("learningV329NativeJumpSingleTrajectoryTerminated", probe.Bot, new
                            {
                                edgeId = probe.LearnedEdgeId,
                                source,
                                reason = probe.NativeLandingRejectReason,
                                lateralDeviation = probe.NativeLandingRejectLateralDeviation,
                                exitDistance = probe.NativeLandingRejectExitDistance,
                                retrySeconds = (RouteMissionMoveRetrySeconds),
                                action = ("noSameAttemptAssistReplayFastSameStepRetry")
                            });
                            FinalizeUnifiedTraversalFailure(
                                probe, "learningV329NativeJumpLandingPartialSingleTrajectory");
                            return true;
                        }

                        string landingAssistFailure = string.Empty;
                        if (FollowerCapabilityRecoveryPolicy.CanOfferAssist(probe.AssistedReplayAttempted) &&
                            TryBeginAssistedTraversalReplay(
                                probe, "nativeLandingRejected:" + source, out landingAssistFailure))
                        {
                            WriteEvent("nativeJumpLandingAssistedRecoveryStarted", probe.Bot, new
                            {
                                edgeId = probe.LearnedEdgeId,
                                source,
                                lateralDeviation = probe.NativeLandingRejectLateralDeviation,
                                exitDistance = probe.NativeLandingRejectExitDistance
                            });
                            return true;
                        }
                        if (!string.IsNullOrEmpty(landingAssistFailure))
                        {
                            WriteEvent("nativeJumpLandingAssistedRecoveryUnavailable", probe.Bot, new
                            {
                                edgeId = probe.LearnedEdgeId,
                                source,
                                failure = landingAssistFailure
                            });
                        }
                        if (TryTraversalPoseLastResortRecovery(probe, "nativeLandingRejected:" + source))
                        {
                            return true;
                        }
                        EnterSafetyHold(probe, "native landing rejected; awaiting hard recovery");
                        return true;
                    }

                    string partialCompletionFailure = string.Empty;
                    if (FollowerCapabilityRecoveryPolicy.CanCompletePartial(commitment.Kind) &&
                        TryBeginPartialNativeVaultCompletion(
                            probe,
                            commitment,
                            source,
                            out partialCompletionFailure))
                    {
                        return true;
                    }

                    if ((string.Equals(commitment.Kind, "Jump", StringComparison.OrdinalIgnoreCase)))
                    {
                        WriteEvent("learningV324JumpPartialCrossingNativeEgressRejected", probe.Bot, new
                        {
                            edgeId = probe.LearnedEdgeId,
                            source,
                            position = Vec(probe.Player.Position),
                            requestedExit = Vec(requestedExit),
                            partialCrossingObserved = probe.NativePartialCrossingObserved,
                            action = "nativeFailureMustUseAssistOrRecovery"
                        });
                        if (FollowerCapabilityRecoveryPolicy.PreserveJumpTrajectory(true,probe.JumpAttempted))
                        {
                            if (TryBeginLearningV408JumpPartialCrossingForwardContinuation(
                                    probe, commitment, source, "PartialCrossing"))
                            {
                                return true;
                            }
                            string retryKey = (probe.Bot?.ProfileId ?? string.Empty) + ":" +
                                              probe.LearnedEdgeId.ToString(CultureInfo.InvariantCulture);
                            LearnedTraversalRetryAfter[retryKey] = Time.time + ((RouteMissionMoveRetrySeconds));
                            WriteEvent("learningV329NativeJumpSingleTrajectoryTerminated", probe.Bot, new
                            {
                                edgeId = probe.LearnedEdgeId,
                                source,
                                position = Vec(probe.Player.Position),
                                requestedExit = Vec(requestedExit),
                                partialCrossingObserved = probe.NativePartialCrossingObserved,
                                retrySeconds = (RouteMissionMoveRetrySeconds),
                                action = ("noSameAttemptAssistReplayFastSameStepRetry")
                            });
                            FinalizeUnifiedTraversalFailure(
                                probe, "learningV329NativeJumpPartialSingleTrajectory");
                            return true;
                        }
                        if (FollowerCapabilityRecoveryPolicy.CanOfferAssist(probe.AssistedReplayAttempted) &&
                            TryBeginAssistedTraversalReplay(
                                probe, "learningV324JumpPhysicalCrossingAuthority:" + source, out string jumpAssistFailure))
                        {
                            return true;
                        }
                        if (TryTraversalPoseLastResortRecovery(
                            probe, "learningV324JumpPartialCrossing:" + source))
                        {
                            return true;
                        }
                        EnterSafetyHold(probe, "partial Jump crossing cannot be promoted through generic egress");
                        return true;
                    }

                    WriteEvent("postNativeContinuationSelected", probe.Bot, new
                    {
                        edgeId = probe.LearnedEdgeId,
                        kind = probe.TraversalKind ?? commitment.Kind,
                        source,
                        position = Vec(probe.Player.Position),
                        requestedExit = Vec(requestedExit),
                        nativeExecutorConsumed = probe.NativeExecutorConsumed,
                        partialCompletionFailure
                    });
                    BeginUnifiedTraversalEgressOrVerify(probe, "postNativeContinuation:" + source);
                    return true;
                }

                string partialAssistedFailure = string.Empty;
                if (FollowerCapabilityRecoveryPolicy.CanOfferAssist(probe.AssistedReplayAttempted) &&
                    TryArmLearningV336TraversalSelfRescue(
                        probe, "nativeDidNotEnter:" + source, "Actuator"))
                {
                    return true;
                }
                if (FollowerCapabilityRecoveryPolicy.CanOfferAssist(probe.AssistedReplayAttempted) &&
                    TryBeginAssistedTraversalReplay(probe, "nativeDidNotEnter:" + source, out partialAssistedFailure))
                {
                    return true;
                }

                if (!string.IsNullOrEmpty(partialAssistedFailure))
                {
                    WriteEvent("partialNativeCrossingAssistedRejected", probe.Bot, new
                    {
                        source,
                        failure = partialAssistedFailure,
                        position = Vec(probe.Player.Position),
                        requestedExit = Vec(requestedExit),
                        action = "keepMissionUntilHardStuck"
                    });
                }

                if (TryTraversalPoseLastResortRecovery(probe, "nativeContinuationUnavailable:" + source))
                {
                    return true;
                }
                EnterSafetyHold(probe, "native continuation unavailable; preserving Route Mission before hard recovery");
                return true;
            }

            var unconsumed = FollowerCapabilityRecoveryPolicy.Unconsumed(probe.AssistedReplayAttempted,zeroShotTransfer);
            if (unconsumed.TryAssist)
            {
                if (TryBeginAssistedTraversalReplay(probe, source, out string assistedFailure))
                {
                    return true;
                }

                WriteEvent("assistedTraversalReplayRejected", probe.Bot, new
                {
                    source,
                    failure = assistedFailure,
                    position = Vec(probe.Player.Position),
                    requestedExit = Vec(requestedExit)
                });
                if (unconsumed.Terminal == NativeRecoveryTerminal.FailTransient)
                {
                    FinalizeUnifiedTraversalFailure(probe, "learningV319ZeroShotAssistRejected:" + assistedFailure);
                    return true;
                }
                return TryTeleportPlayerValidatedTraversalAtExit(probe, "assistedReplayRejected:" + assistedFailure);
            }

            if (unconsumed.Terminal == NativeRecoveryTerminal.FailTransient)
            {
                FinalizeUnifiedTraversalFailure(probe, "learningV319ZeroShotRecoveryExhausted:" + (source ?? "unknown"));
                return true;
            }
            return TryTeleportPlayerValidatedTraversalAtExit(probe, source);
        }
    }
}
