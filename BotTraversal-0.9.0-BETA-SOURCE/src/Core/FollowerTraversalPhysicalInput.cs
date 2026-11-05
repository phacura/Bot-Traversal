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
        private static CapabilityMotionCommit ApplyCollisionActivePhysicalRouteMotion(
            BotOwner bot,
            Player player,
            Vector3 direction,
            Vector3 motion,
            float deltaTime,
            float pose,
            bool sprint = false,
            float targetMoveSpeed = ProbeMomentumNormalMoveSpeed,
            bool preserveSprintMode = false,
            bool exactWorldDirectionInput = false,
            Vector3? deliveryTarget = null,
            CapabilityMotionPurpose deliveryPurpose = CapabilityMotionPurpose.NativeProbe)
        {
            if (!FollowerMovementWriterAuthority.CanCommitScopedPhysicalMotion(bot, out string v2CommitBlockReason))
            {
                // A native movement input is NOT a protected BotMover Harmony entrypoint;
                // check before any pose, sprint, Player.Move, or body write occurs.
                if (bot != null && ShouldEmitDiagnostic("v2PhysicalCommitRejected:" + v2CommitBlockReason, bot, 0.8f))
                    WriteEvent("v2PhysicalCommitRejected", bot, new
                    {
                        reason = v2CommitBlockReason,
                        physicalWritePerformed = false,
                        traversalSuccessRecorded = false
                    });
                return CapabilityMotionCommit.Rejected;
            }
            if (bot == null || player?.MovementContext == null)
            {
                return CapabilityMotionCommit.Rejected;
            }

            if(IsCapabilityPortalWaiting(bot)&&IsCapabilityRouteRetrySafe(bot)&&!HasActiveTraversalProbe(bot))
            {
                CommitCapabilityPortalHold(bot,FollowerRouteMissions[bot.ProfileId]);
                return CapabilityMotionCommit.ManualFrame;
            }
            if(motion.sqrMagnitude>0.000001f&&TryGetCapabilityRouteSprint(bot,direction,out bool routeSprint))
            {sprint=routeSprint;preserveSprintMode=false;if(sprint)targetMoveSpeed=ProbeMomentumFullMoveSpeed;}
            // Route Sprint restores its normal speed above; the finite WT envelope must be applied AFTER that boost.
            if(motion.sqrMagnitude>0.000001f&&TryGetActiveProbe(bot,out var precisionProbe)&&precisionProbe.Stage==ProbeStage.WalkThrough&&
                NeedsCapabilityWalkThroughPrecision(precisionProbe,direction))
                targetMoveSpeed=(float)FollowerCapabilityIngressMotionPolicy.WalkingTurnSpeed(targetMoveSpeed,true);
            if(motion.sqrMagnitude>0.000001f)PrepareCapabilityJumpLocomotion(bot,player);
            if(motion.sqrMagnitude>0.000001f)PrepareCapabilityDropIngressLocomotion(bot,player,deliveryPurpose);

            if (TryApplyMeasuredGroundDelivery(bot,player,out CapabilityMotionCommit deliveryCommit,deliveryTarget,deliveryPurpose,direction,motion.sqrMagnitude>0.000001f,pose,targetMoveSpeed)) return deliveryCommit;

            bool holdJumpAlignment=false;
            Vector3 facingDirection=direction;
            if(TryGetCapabilityObstacleCenterFacing(bot,out var obstacleAxis))facingDirection=obstacleAxis;
            if(TryGetCapabilityJumpPreparationFacing(bot,deliveryTarget,deliveryPurpose,direction,out var jumpAxis,out bool forwardOnly))
            {
                facingDirection=jumpAxis;
                exactWorldDirectionInput=true;
                if(forwardOnly){direction=jumpAxis;motion=jumpAxis*motion.magnitude;}
                if(TryGetCapabilityCurrentJumpGait(bot,jumpAxis,out var readyFacing,out var jumpSprint))
                {
                    sprint=jumpSprint;preserveSprintMode=false;holdJumpAlignment=!readyFacing;
                    if(holdJumpAlignment){motion=Vector3.zero;targetMoveSpeed=0f;}
                }
            }
            else if(TryGetCapabilityVaultIngressFacing(bot,deliveryTarget,deliveryPurpose,out var vaultAxis,out bool vaultCentering))
            {
                facingDirection=vaultAxis;exactWorldDirectionInput=true;
                if(vaultCentering){targetMoveSpeed=Mathf.Min(targetMoveSpeed,.55f);sprint=false;preserveSprintMode=false;}
            }
            else if(TryGetCapabilityClimbIngressFacing(bot,deliveryTarget,deliveryPurpose,out var climbAxis))
            {
                facingDirection=climbAxis;
                exactWorldDirectionInput=true;
            }
            bot.Steering?.BotSteeringLookToDirection("R1-D3D25D3AA7C2",@bot,facingDirection);
            bot.BotOwnerSetPose("R1-F395D83EA4B7",@bot,Mathf.Clamp01(pose));
            bot.BotOwnerSetTargetMoveSpeed("R1-ADFF743F408E",@bot,targetMoveSpeed);
            if (bot.Mover != null)
            {
                bot.Mover.BotMoverSetTargetMoveSpeed("R1-EA7F673C599F",@bot,targetMoveSpeed);
                if (!preserveSprintMode)
                {
                    bot.Mover.BotMoverSprint("R1-79C180D4C2D8",@bot,sprint,false);
                }
            }

            if(holdJumpAlignment)
            {
                TryRetireNativeMoverPathPreserveLocomotion(bot,"jumpAlignmentBeforeAcceleration",out _);
                CommitCapabilityRouteRetryZeroInput(bot,player);
                if(ShouldEmitDiagnostic("capabilityJumpAlignmentHeld",bot,.25f))
                    WriteEvent("capabilityJumpAlignmentHeld",bot,new{axis=Vec(facingDirection),body=Vec(player.Position),nativeJumpTriggered=false});
                return CapabilityMotionCommit.ManualFrame;
            }
            {
                // Source-grounded native BotMover contract:
                //   route/steering owns intent -> Player.Move owns locomotion input -> the active
                //   EFT movement state owns CharacterController motion -> the next body sample owns
                //   progress truth. Traversal never schedules/directly applies body displacement.
                // Retire only stale NavMesh topology first so BotMover.FixedUpdate cannot publish a
                // competing path input or project PositionOnWay back onto an obsolete path.
                string retireDetail = "noPathToRetire";
                if (bot.Mover == null)
                {
                    return CapabilityMotionCommit.Rejected;
                }
                if (bot.Mover.HasPathAndNoComplete)
                {
                    TryRetireNativeMoverPathPreserveLocomotion(
                        bot, "learningV409NativeLocomotionIntent", out retireDetail);
                }

                Vector3 nativeDirection = direction;
                nativeDirection.y = 0f;
                if (!IsFinite(nativeDirection) || nativeDirection.sqrMagnitude <= 0.000001f)
                {
                    return CapabilityMotionCommit.Rejected;
                }
                nativeDirection.Normalize();
                bot.Mover.SetBotMoverNormDirCurPoint("R1-29966EF5EA31",@bot,nativeDirection);
                bot.Mover.SetBotMoverPause("R1-BFF38D4BD8D1",@bot,false);
                bot.Mover.SetBotMoverIsMoving("R1-3191D7555A81",@bot,motion.sqrMagnitude > 0.000001f);
                if (player.CharacterController != null)
                {
                    player.CharacterController.ICharacterControllerSetSteerDirection("R1-40E2BFE175D3",@bot,nativeDirection);
                }

                if ((player.CurrentStateName == EPlayerState.Transition) &&
                    motion.sqrMagnitude > 0.000001f)
                {
                    TryReleaseLearningV445PostCompletionInheritedTransition(bot, player, nativeDirection);
                }

                if ((player.CurrentStateName == EPlayerState.Transition) &&
                    motion.sqrMagnitude > 0.000001f)
                {
                    // EFT TransitionStateClass does not override Move(Vector2): the base
                    // MovementState.Move is a no-op. Its native locomotion consumer is instead
                    // ProcessAnimatorMovement -> TransitionStateClass.BlendMotion, which reads
                    // MovementContext.LastBlendMotionDelta. Publish the bounded physical-route
                    // velocity into that exact native consumer rather than pretending Player.Move
                    // can translate the body while Transition owns the state machine.
                    float transitionDt = Mathf.Max(0.001f, deltaTime);
                    float requestedPlanarSpeedFloor = motion.magnitude / transitionDt;
                    Vector3 previousBlendVelocity = player.MovementContext.LastBlendMotionDelta;
                    Vector3 previousPlanarVelocity = previousBlendVelocity;
                    previousPlanarVelocity.y = 0f;
                    float previousPlanarSpeed = IsFinite(previousPlanarVelocity)
                        ? previousPlanarVelocity.magnitude : 0f;
                    float previousForwardSpeed = IsFinite(previousPlanarVelocity)
                        ? Vector3.Dot(previousPlanarVelocity, nativeDirection) : 0f;

                    long movementGeneration = FollowerMovementWriterAuthority.GetCurrentGeneration(
                        bot, out FollowerMovementWriterAuthority.WriterDomain transitionOwner);
                    string transitionProfileId = bot.ProfileId ?? string.Empty;
                    float learningV444GeometryMomentumFloor = requestedPlanarSpeedFloor;
                    float learningV444RemainingNearSideGap = -1f;
                    float learningV444GapFraction = 0f;
                    bool learningV444EpochActive = false;
                    LearningV444TransitionMomentumEpoch learningV444Epoch = null;
                    if ((transitionOwner == FollowerMovementWriterAuthority.WriterDomain.Traversal) &&
                        movementGeneration > 0 &&
                        !string.IsNullOrEmpty(transitionProfileId) &&
                        FollowerRouteMissions.TryGetValue(transitionProfileId, out FollowerRouteMission learningV444Mission) &&
                        learningV444Mission != null && learningV444Mission.Steps != null &&
                        learningV444Mission.CurrentStepIndex >= 0 &&
                        learningV444Mission.CurrentStepIndex < learningV444Mission.Steps.Count)
                    {
                        FollowerRouteMissionStep learningV444Step =
                            learningV444Mission.Steps[learningV444Mission.CurrentStepIndex];
                        LearningV354TraversalCapabilityProfile learningV444Profile =
                            learningV444Step != null ? GetLearningV354ActionProfile(learningV444Step.Kind) : null;
                        Vector3 learningV444RawEntry = learningV444Step != null && IsFinite(learningV444Step.RawEntry)
                            ? learningV444Step.RawEntry : (learningV444Step != null ? learningV444Step.Entry : default);
                        Vector3 learningV444RawExit = learningV444Step != null && IsFinite(learningV444Step.RawExit)
                            ? learningV444Step.RawExit : (learningV444Step != null ? learningV444Step.Exit : default);
                        Vector3 learningV444Axis = GetFlatDirection(learningV444RawExit - learningV444RawEntry);
                        if (learningV444Step != null && learningV444Profile?.RequiresActuator == true &&
                            IsFinite(learningV444RawEntry) && IsFinite(learningV444RawExit) &&
                            learningV444Axis.sqrMagnitude > 0.5f &&
                            Vector3.Dot(learningV444Axis, nativeDirection) >= LearningV2RevisionMinDirectionDot)
                        {
                            Vector3 learningV444BodyOffset = bot.Position - learningV444RawEntry;
                            learningV444BodyOffset.y = 0f;
                            float learningV444ForwardProjection =
                                Vector3.Dot(learningV444BodyOffset, learningV444Axis);
                            learningV444RemainingNearSideGap = Mathf.Max(0f, -learningV444ForwardProjection);

                            bool learningV444NeedNewEpoch =
                                !LearningV444TransitionMomentumEpochs.TryGetValue(transitionProfileId, out learningV444Epoch) ||
                                learningV444Epoch == null ||
                                learningV444Epoch.MovementGeneration != movementGeneration ||
                                learningV444Epoch.MissionId != learningV444Mission.MissionId ||
                                learningV444Epoch.StepIndex != learningV444Mission.CurrentStepIndex ||
                                learningV444Epoch.EdgeId != learningV444Step.EdgeId;
                            if (learningV444NeedNewEpoch)
                            {
                                LearningV444TransitionMomentumEpochs.Remove(transitionProfileId);
                                if (previousForwardSpeed > requestedPlanarSpeedFloor &&
                                    learningV444RemainingNearSideGap > 0.001f)
                                {
                                    learningV444Epoch = new LearningV444TransitionMomentumEpoch
                                    {
                                        MovementGeneration = movementGeneration,
                                        MissionId = learningV444Mission.MissionId,
                                        StepIndex = learningV444Mission.CurrentStepIndex,
                                        EdgeId = learningV444Step.EdgeId,
                                        Kind = learningV444Step.Kind,
                                        RouteAxis = learningV444Axis,
                                        InitialNearSideGap = learningV444RemainingNearSideGap,
                                        EntryForwardSpeed = previousForwardSpeed
                                    };
                                    LearningV444TransitionMomentumEpochs[transitionProfileId] = learningV444Epoch;
                                    WriteEvent("learningV444TransitionMomentumEpochLatched", bot, new
                                    {
                                        missionId = learningV444Mission.MissionId,
                                        stepIndex = learningV444Mission.CurrentStepIndex,
                                        edgeId = learningV444Step.EdgeId,
                                        kind = learningV444Step.Kind,
                                        owner = transitionOwner.ToString(),
                                        generation = movementGeneration,
                                        entryForwardSpeed = previousForwardSpeed,
                                        initialNearSideGap = learningV444RemainingNearSideGap,
                                        requestedPlanarSpeedFloor,
                                        routeAxis = Vec(learningV444Axis),
                                        traversalSuccessRecorded = false,
                                        persistentLearningChanged = false,
                                        bodyRelocated = false,
                                        action = "latchOneTransitionEpochFromLiveNativeMomentumAndCurrentActuatorNearSideGap"
                                    });
                                }
                            }

                            if (learningV444Epoch != null &&
                                learningV444Epoch.MovementGeneration == movementGeneration &&
                                learningV444Epoch.MissionId == learningV444Mission.MissionId &&
                                learningV444Epoch.StepIndex == learningV444Mission.CurrentStepIndex &&
                                learningV444Epoch.EdgeId == learningV444Step.EdgeId &&
                                learningV444Epoch.InitialNearSideGap > 0.001f &&
                                Vector3.Dot(learningV444Epoch.RouteAxis, nativeDirection) >=
                                    LearningV2RevisionMinDirectionDot)
                            {
                                learningV444GapFraction = Mathf.Clamp01(
                                    learningV444RemainingNearSideGap / learningV444Epoch.InitialNearSideGap);
                                learningV444GeometryMomentumFloor = Mathf.Max(
                                    requestedPlanarSpeedFloor,
                                    learningV444Epoch.EntryForwardSpeed * learningV444GapFraction);
                                learningV444EpochActive =
                                    learningV444GeometryMomentumFloor > requestedPlanarSpeedFloor + 0.01f;
                            }
                        }
                    }

                    // V4.40: LastBlendMotionDelta is EFT's existing native transition velocity, not a
                    // target-speed slot. V4.39 wrote the route floor (typically 1.85m/s) over a
                    // 4-5m/s landing vector and therefore created a visible brake even though the
                    // producer never went idle. Preserve the native planar vector verbatim and only
                    // add a missing forward component. V4.44 raises that minimum only while the
                    // current actuator still has a measured near-side gap; the extra floor decays by
                    // route geometry instead of recursively reusing EFT's already-blended output.
                    float effectiveTransitionForwardFloor = Mathf.Max(
                        requestedPlanarSpeedFloor, learningV444GeometryMomentumFloor);
                    Vector3 nativeTransitionVelocity;
                    bool preservedNativeMomentum =
                        (IsFinite(previousPlanarVelocity)) && previousForwardSpeed > 0f;
                    if (preservedNativeMomentum)
                    {
                        nativeTransitionVelocity = previousPlanarVelocity;
                        if (previousForwardSpeed < effectiveTransitionForwardFloor)
                        {
                            nativeTransitionVelocity += nativeDirection *
                                (effectiveTransitionForwardFloor - previousForwardSpeed);
                        }
                    }
                    else
                    {
                        nativeTransitionVelocity = nativeDirection * effectiveTransitionForwardFloor;
                    }
                    // Only lease planar route momentum. TransitionStateClass/Base MovementState
                    // owns gravity/vertical motion; carrying Jump Y velocity here would fabricate
                    // a second airborne impulse.
                    nativeTransitionVelocity.y = 0f;
                    if (!FollowerMovementWriterAuthority.TryCommitNativeTransitionVelocity(
                            bot, player, nativeTransitionVelocity))
                        return CapabilityMotionCommit.Rejected;
                    float publishedPlanarSpeed = nativeTransitionVelocity.magnitude;

                    if (learningV444EpochActive &&
                        ShouldEmitDiagnostic(
                            "learningV444TransitionEpochGeometryMomentum:" + transitionProfileId,
                            bot, 0.12f))
                    {
                        WriteEvent("learningV444TransitionEpochGeometryMomentum", bot, new
                        {
                            missionId = learningV444Epoch.MissionId,
                            stepIndex = learningV444Epoch.StepIndex,
                            edgeId = learningV444Epoch.EdgeId,
                            kind = learningV444Epoch.Kind,
                            owner = transitionOwner.ToString(),
                            generation = movementGeneration,
                            entryForwardSpeed = learningV444Epoch.EntryForwardSpeed,
                            initialNearSideGap = learningV444Epoch.InitialNearSideGap,
                            remainingNearSideGap = learningV444RemainingNearSideGap,
                            remainingGapFraction = learningV444GapFraction,
                            requestedPlanarSpeedFloor,
                            geometryMomentumFloor = learningV444GeometryMomentumFloor,
                            previousForwardSpeed,
                            publishedPlanarSpeed,
                            traversalSuccessRecorded = false,
                            persistentLearningChanged = false,
                            bodyRelocated = false,
                            action = "geometryConsumesLatchedTransitionMomentumUntilExistingActuatorAdmissionPlane"
                        });
                    }

                    if (ShouldEmitDiagnostic(
                            "learningV440NativeTransitionMomentumFloor:" +
                                (bot.ProfileId ?? string.Empty), bot, 0.12f))
                    {
                        WriteEvent("learningV440NativeTransitionMomentumFloor", bot, new
                        {
                            owner = transitionOwner.ToString(),
                            generation = movementGeneration,
                            state = player.CurrentStateName.ToString(),
                            direction = Vec(nativeDirection),
                            requestedMotionMagnitude = motion.magnitude,
                            deltaTime = transitionDt,
                            requestedPlanarSpeedFloor,
                            effectiveTransitionForwardFloor,
                            learningV444EpochActive,
                            learningV444GeometryMomentumFloor,
                            learningV444RemainingNearSideGap,
                            learningV444GapFraction,
                            previousBlendVelocity = Vec(previousBlendVelocity),
                            previousPlanarSpeed,
                            previousForwardSpeed,
                            publishedBlendVelocity = Vec(nativeTransitionVelocity),
                            publishedPlanarSpeed,
                            preservedNativeMomentum,
                            nativeMomentumWasClampedDown =
                                preservedNativeMomentum && publishedPlanarSpeed + 0.01f < previousPlanarSpeed,
                            playerMoveCalled = false,
                            bodyRelocated = false,
                            traversalSuccessRecorded = false,
                            persistentLearningChanged = false,
                            action = ("transitionUsesGeometryBoundedEpochMomentumFloorNeverClampsNativeSpeedDown")
                        });
                    }
                    return CapabilityMotionCommit.ManualFrame;
                }

                if ((!string.IsNullOrEmpty(bot.ProfileId)))
                {
                    LearningV444TransitionMomentumEpochs.Remove(bot.ProfileId);
                }

                exactWorldDirectionInput=FollowerCapabilityPassagePolicy.WorldInput(exactWorldDirectionInput,
                    deliveryPurpose!=CapabilityMotionPurpose.NativeProbe&&deliveryPurpose!=CapabilityMotionPurpose.NativeExecution,HasCapabilityFailureStrategy(bot));
                Vector2 moverNativeInput = bot.Mover.method_17();
                Vector2 nativeInput = moverNativeInput;
                if (exactWorldDirectionInput)
                {
                    // RC52: V379 may retire a stale BotMover path but the mover's
                    // method_17() can still produce an input in its OLD local
                    // steering frame. At RC51 Edge25 the published direction
                    // aimed at the pre-stage yet method_17 returned backward /
                    // sideward movement, and the next body sample moved away.
                    // The authoritative route direction is a WORLD vector.
                    // Project it into the actual player's current horizontal
                    // facing frame (X=right strafe, Y=forward) before Player.Move.
                    // Do not modify World position, NavMesh, Native admissions,
                    // leases, epochs, or any non-opted-in motion producer.
                    Vector3 playerForward = GetFlatDirection(
                        player.MovementContext.PlayerRealForward);
                    if (playerForward.sqrMagnitude < 0.5f ||
                        nativeDirection.sqrMagnitude < 0.5f)
                        return CapabilityMotionCommit.Rejected;
                    Vector3 playerRight = Vector3.Cross(Vector3.up, playerForward);
                    nativeInput = new Vector2(
                        Vector3.Dot(nativeDirection, playerRight),
                        Vector3.Dot(nativeDirection, playerForward));
                    if (nativeInput.sqrMagnitude < 0.5f)
                        return CapabilityMotionCommit.Rejected;
                    nativeInput.Normalize();
                    if (ShouldEmitDiagnostic("v2rc52WorldLockedIngressInput", bot, 0.15f))
                    {
                        WriteEvent("v2rc52WorldLockedIngressInput", bot, new
                        {
                            direction = Vec(nativeDirection),
                            playerFacing = Vec(playerForward),
                            moverNativeInput = Vec2(moverNativeInput),
                            projectedPlayerInput = Vec2(nativeInput),
                            requestedMotionMagnitude = motion.magnitude,
                            nativeState = player.CurrentStateName.ToString(),
                            pathStatusIsProof = false,
                            action = "existingFollowProducerProjectsExactWorldGoalOntoPlayerInputFrame"
                        });
                    }
                }
                bool movementIntentPublished = false;
                if (motion.sqrMagnitude > 0.000001f && nativeInput.sqrMagnitude > 0.000001f)
                {
                    movementIntentPublished = CommitCapabilityNativePlayerInput(
                        bot, player, nativeInput);
                    if (!movementIntentPublished)
                        return CapabilityMotionCommit.Rejected;
                }
                else if(motion.sqrMagnitude<=0.000001f)
                {
                    nativeInput=Vector2.zero;
                    movementIntentPublished=CommitCapabilityRouteRetryZeroInput(bot,player);
                }

                if (ShouldEmitDiagnostic("learningV409NativeLocomotionIntentPublished", bot, 0.20f))
                {
                    long movementGeneration = FollowerMovementWriterAuthority.GetCurrentGeneration(
                        bot, out FollowerMovementWriterAuthority.WriterDomain owner);
                    WriteEvent("learningV409NativeLocomotionIntentPublished", bot, new
                    {
                        owner = owner.ToString(),
                        generation = movementGeneration,
                        state = player.CurrentStateName.ToString(),
                        direction = Vec(nativeDirection),
                        input = Vec2(nativeInput),
                        requestedMotionMagnitude = motion.magnitude,
                        deltaTime,
                        sprint,
                        targetMoveSpeed,
                        preserveSprintMode,
                        movementIntentPublished,
                        stalePathRetireDetail = retireDetail,
                        bodyRelocated = false,
                        traversalSuccessRecorded = false,
                        persistentLearningChanged = false,
                        action = "publishNativePlayerMoveIntentActualBodyDeltaIsOnlyProgressProof"
                    });
                }
                return CapabilityMotionCommit.ManualFrame;
            }

            player.SetPlayerInputDirection("R1-EF468CE5DF5E",@bot,Vector2.up);

            bool nativeMovementApplied = false;
            Exception nativeMovementFailure = null;
            {
                try
                {
                    // The canonical writer still owns the exact displacement. Route it through EFT's
                    // native movement context so body translation, velocity/grounding and locomotion
                    // state observe the same movement epoch instead of bypassing state via a direct
                    // CharacterController write.
                    nativeMovementApplied = FollowerMovementWriterAuthority.TryCommitNativeApproachMotion(
                        bot, player, motion, deltaTime, false);
                }
                catch (Exception ex)
                {
                    nativeMovementFailure = ex;
                }
            }

            if (!nativeMovementApplied)
            {
                if (player.CharacterController != null)
                {
                    if (!FollowerMovementWriterAuthority.TryCommitCharacterControllerMotion(
                            bot, player, motion, deltaTime))
                        return CapabilityMotionCommit.Rejected;
                }
                else ;
            }

            // V407 remains a presentation-only compatibility layer. Running it after V408's native
            // movement backend lets it observe the actual EFT locomotion state first; it never adds
            // a second translation.
            TryApplyLearningV407CollisionActiveLocomotionPresentation(
                bot, player, motion, sprint, targetMoveSpeed);

            if (nativeMovementFailure != null && ShouldEmitDiagnostic(
                    "learningV408NativeMovementContextFallback", bot, 0.35f))
            {
                WriteEvent("learningV408NativeMovementContextFallback", bot, new
                {
                    error = nativeMovementFailure.GetType().Name + ": " + nativeMovementFailure.Message,
                    motionMagnitude = motion.magnitude,
                    deltaTime,
                    fallback = player.CharacterController != null
                        ? "CharacterController.Move"
                        : "noneNoSecondNativeRetry",
                    traversalSuccessRecorded = false,
                    persistentLearningChanged = false,
                    action = "nativeMovementContextExceptionFallsBackWithoutAddingSecondWriter"
                });
            }
            return CapabilityMotionCommit.ManualFrame;
        }
    }
}
