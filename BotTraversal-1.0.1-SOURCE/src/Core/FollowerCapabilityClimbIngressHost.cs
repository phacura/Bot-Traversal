// R3O_CLIMB_EXTENSION_REVIEWED
using System;
using System.Globalization;
using System.Linq;
using EFT;
using UnityEngine;
using UnityEngine.AI;
namespace BotTraversal.Core
{
    internal static partial class FollowerTraversalTestRuntime
    {
        private static bool TryDriveLearningV379ActuatorPreProbePhysicalLane(
            BotOwner bot, FollowerRouteMission mission, FollowerRouteMissionStep step,
            Vector3 physicalTarget, string laneSource,
            bool preserveExecutorHeadingAtPositionFrontier,
            out float bodyDisplacement, out float targetClosure)
        {
            bodyDisplacement = 0f;
            targetClosure = 0f;
            if (step == null || !string.Equals(step.Kind, "Climb", StringComparison.OrdinalIgnoreCase))
                return TryDriveV2RC57ExistingNonClimbPreProbePhysicalLane(
                    bot, mission, step, physicalTarget, laneSource,
                    preserveExecutorHeadingAtPositionFrontier,
                    out bodyDisplacement, out targetClosure);
            if ((!TryCaptureCapabilityClimbIngress(bot,mission,step,out _)) || bot == null || mission == null ||
                !IsFinite(physicalTarget) || HasActiveTraversalProbe(bot))
                return false;
            Player player = bot.GetPlayer ?? bot.AIData?.Player;
            if (player?.MovementContext == null || !IsFinite(bot.Position))
                return false;
            Vector3 axis = GetFlatDirection(step.RawExit - step.RawEntry);
            if (axis.sqrMagnitude < 0.5f) return false;
            // RC69: the prior *measured* motion may run opposite to Player.Move.
            // RC68 Edge25 reversed 0.53m and 0.45m while issued intent remained
            // toward its staging target. Record the disproof before publishing
            // another input; do not count a requested direction as progress.
            bool rc69LastInputContradicted = false;
            float rc69LastIntentDot = 0f;
            float rc69LastMeasuredLength = 0f;
            // RC58: measure LAST physics frame versus LAST explicitly issued world
            // input. No Player.Move call is treated as displacement or success.
            if (step.Rc48IngressLastSampleAt > 0f &&
                Time.time > step.Rc48IngressLastSampleAt &&
                IsFinite(step.Rc48IngressObservedTarget))
            {
                Vector3 measured = bot.Position - step.Rc48IngressObservedTarget;
                measured.y = 0f;
                Vector3 expected = step.Rc48MoverCommandTarget;
                expected.y = 0f;
                float measuredLength = measured.magnitude;
                float intentDot = MeasureRC70IngressDirectionalAgreement(
                    measured, expected, 0.015f * 0.015f, 0.5f, 0f);
                rc69LastIntentDot = intentDot;
                rc69LastMeasuredLength = measuredLength;
                rc69LastInputContradicted = measuredLength >= 0.12f && intentDot < -0.35f &&
                    expected.sqrMagnitude > 0.5f && step.Rc48MoverPathRetired;
                if (ShouldEmitDiagnostic("v2rc58ClimbPhysicalIntentDisplacement:" +
                        mission.MissionId.ToString(CultureInfo.InvariantCulture) + ":" +
                        step.EdgeId.ToString(CultureInfo.InvariantCulture), bot, 0.30f))
                {
                    WriteEvent("v2rc58ClimbPhysicalIntentDisplacement", bot, new
                    {
                        missionId = mission.MissionId, edgeId = step.EdgeId,
                        body = Vec(bot.Position),
                        previousBody = Vec(step.Rc48IngressObservedTarget),
                        previousWorldInput = Vec(expected),
                        measuredBodyDelta = Vec(measured), measuredLength, intentDot,
                        inputFrameHasMeasuredProgress = measuredLength > 0.015f,
                        nativeSuccessRecorded = false,
                        action = "comparePreviousWorldInputWithNextPhysicalBodyDeltaNoSyntheticMovementCredit"
                    });
                }
            }
            bool geometryReady = TryGetLearningV424ActuatorExecutorAdmission(
                bot, step, out bool nativeAdmissible, out _,
                out float entryDistance, out float forward, out float lateral,
                out float maxLateral, out float heading, out float maxHeading,
                out float maxForward, out float nativeTrigger, out _);
            bool supportedPose = geometryReady &&
                entryDistance <= nativeTrigger && lateral <= maxLateral &&
                forward >= -nativeTrigger && forward <= maxForward &&
                Mathf.Abs(bot.Position.y - step.RawEntry.y) <= 0.40f &&
                player.MovementContext.IsGrounded &&
                player.CurrentStateName != EPlayerState.Transition;
            bool sprintUnsettled = bot.Mover?.Sprinting == true ||
                player.MovementContext.IsSprintEnabled ||
                player.CurrentStateName == EPlayerState.Sprint;
            // RC59: the Native position frontier and the *fully admissible* executor
            // pose are distinct facts. RC58 Edge25 at 303.665s had entryDistance
            // 0.085m, heading 39.9 degrees, yet supportedPose=false and thus
            // published another move. That carried the bot away from the entry.
            // At a safe same-height near-side frontier, native orientation and
            // sprint release take precedence over any approach target/producer.
            // This does NOT alter the executor's lateral/forward/admission rules.
            bool sameHeightGrounded = geometryReady &&
                Mathf.Abs(bot.Position.y - step.RawEntry.y) <= 0.40f &&
                player.MovementContext.IsGrounded &&
                player.CurrentStateName != EPlayerState.Transition;
            bool safeNearSide = sameHeightGrounded &&
                forward >= -(nativeTrigger + 1.45f) &&
                forward <= Mathf.Max(0.15f, maxForward) &&
                lateral <= Mathf.Max(0.75f, maxLateral);
            bool nearEntryPose = safeNearSide && entryDistance <= nativeTrigger;
            // RC60: the already-retired native mover path is the route step's
            // existing one-way ingress ownership witness. Once the V2 Follow
            // writer took Climb physical approach, transient lateral/projection
            // changes must not re-arm ordinary Run/Sprint or switch producers.
            // The latch belongs to this exact RouteStep and expires with it.
            bool climbWalkControlOwned = step.Rc48MoverPathRetired;
            bool walkAndAlignFrontier = sameHeightGrounded &&
                entryDistance <= nativeTrigger + 0.75f &&
                forward >= -(nativeTrigger + 1.65f) &&
                forward <= Mathf.Max(0.15f, maxForward) &&
                lateral <= 1.45f;
            // Disarm inherited Sprint one short stopping distance earlier than
            // alignment; a 4-5m/s actor can cross the 0.55m Native frontier
            // between diagnostic frames if sprint is only released at 0.55m.
            bool earlyWalkFrontier = sameHeightGrounded &&
                entryDistance <= nativeTrigger + 1.95f &&
                forward >= -(nativeTrigger + 2.30f) &&
                forward <= Mathf.Max(0.15f, maxForward) &&
                lateral <= 2.25f;
            bool retainClimbWalk = climbWalkControlOwned || earlyWalkFrontier;
            // RC69: on the SAME grounded near-side Climb Step, a measured
            // counter-motion is a reason to turn/settle, not to publish another
            // blind forward input. No extra state or retry clock is introduced.
            bool rc69CounterMotionPose = rc69LastInputContradicted &&
                earlyWalkFrontier && climbWalkControlOwned;
            // True native readiness belongs exclusively to the original executor.
            // Do not write another locomotion frame when it may start native now.
            if (supportedPose && nativeAdmissible && !sprintUnsettled)
                return false;

            Vector3 target = physicalTarget;
            // Only a near, demonstrably same-side STRAIGHT ground path may close
            // directly to the original entry. No route around a wall is inferred.
            if (IsFinite(step.RawEntry) &&
                (nearEntryPose || (!supportedPose &&
                 entryDistance > nativeTrigger && entryDistance <= nativeTrigger + 0.80f)) &&
                lateral <= Mathf.Max(0.35f, maxLateral) &&
                forward < -0.04f && Mathf.Abs(bot.Position.y - step.RawEntry.y) <= 0.40f &&
                player.MovementContext.IsGrounded &&
                !NavMesh.Raycast(bot.Position, step.RawEntry, out _, NavMesh.AllAreas))
            {
                StrictPathResult close = CalculateStrictPath(bot.Position, step.RawEntry);
                if (close?.StrictComplete == true &&
                    close.PathLength <= entryDistance * 1.25f + 0.10f)
                    target = step.RawEntry;
            }
            Vector3 flat = target - bot.Position;
            flat.y = 0f;
            float distance = flat.magnitude;
            // RC59: being physically at the entry is enough to OWN turn-in-place,
            // even when the original executor reports not-yet-admissible yaw,
            // lateral, forward, or sprint state. Never synthesize Native success.
            // At the close approach frontier, turn before moving when yaw is
            // severe; if EFT is still Sprint, wait for the actual mode to settle.
            bool poseOnly = FollowerCapabilityClimbIngressPolicy.Pose(
                nearEntryPose,walkAndAlignFrontier,earlyWalkFrontier,climbWalkControlOwned,
                heading,maxHeading,sprintUnsettled,false,rc69CounterMotionPose,distance);
            if (!poseOnly && distance <= 0.001f)
                return true;
            if (!TryEnterCapabilityClimbIngressScope(
                    bot, mission, step, "v2rc57SingleClimbPhysicalProducer",
                    out FollowerMovementWriterAuthority.WriteScope scope, out _))
                return false;
            Vector3 before = bot.Position;
            CapabilityMotionCommit motionCommit = default(CapabilityMotionCommit);
            bool issuedInput = false;
            try
            {
                using (scope)
                {
                    if(!TryCaptureCapabilityClimbIngress(bot,mission,step,out _))return false;
                    // Retire a prior EFT NavMesh path ONCE per existing RouteStep.
                    // Direct world-locked movement then has a single input origin.
                    if (!step.Rc48MoverPathRetired)
                    {
                        if (bot.Mover != null)
                            bot.Mover.BotMoverStop("R1-7F401AFA0CBF",@bot);
                        step.Rc48MoverPathRetired = true;
                    }
                    // No second movement owner is created. The existing RouteStep
                    // records that this exact Climb must stay in walk/pose control
                    // until an actual Native attempt takes ownership or this Step
                    // is retired. Reacquiring a route mission is a fresh Step.
                    climbWalkControlOwned = true;
                    retainClimbWalk = true;
                    // Request native EFT sprint release BEFORE reaching the
                    // exact 0.55m/12deg admission gate. Calling Player.Move with
                    // sprint=false does not prove that current EFT Sprint state
                    // (or its residual momentum) has ended.
                    if (retainClimbWalk && sprintUnsettled)
                        TryRequestCF13ClimbSprintRelease(bot, step.EdgeId,
                            "rc60ClimbStepOwnedWalkPoseUntilNative");
                    // RC63: the initial sprintUnsettled is a PRE-release observation.
                    // BotMover.Sprint(false) may clear both native sprint flags during
                    // this scope. Holding that stale snapshot in poseOnly leaves a
                    // grounded Idle actor laterally displaced and motionless, even
                    // though the safe walking correction is still outstanding.
                    // Preserve true Sprint/Transition brakes, but re-observe the
                    // actual EFT state before deciding whether to translate.
                    bool rc63SprintAfterRelease = bot.Mover?.Sprinting == true ||
                        player.MovementContext.IsSprintEnabled ||
                        player.CurrentStateName == EPlayerState.Sprint;
                    bool rc63TransitionAfterRelease =
                        player.CurrentStateName == EPlayerState.Transition;
                    bool rc63PoseAfterRelease = FollowerCapabilityClimbIngressPolicy.Pose(
                        nearEntryPose,walkAndAlignFrontier,earlyWalkFrontier,true,
                        heading,maxHeading,rc63SprintAfterRelease,rc63TransitionAfterRelease,
                        rc69CounterMotionPose,distance);
                    if (poseOnly != rc63PoseAfterRelease &&
                        ShouldEmitDiagnostic("v2rc63ClimbPostSprintRecheck:" +
                            mission.MissionId.ToString(CultureInfo.InvariantCulture) + ":" +
                            step.EdgeId.ToString(CultureInfo.InvariantCulture), bot, 0.25f))
                    {
                        WriteEvent("v2rc63ClimbPostSprintPoseRecheck", bot, new
                        {
                            missionId = mission.MissionId, edgeId = step.EdgeId,
                            entryDistance, lateral, forward, heading,
                            poseBeforeRelease = poseOnly,
                            poseAfterRelease = rc63PoseAfterRelease,
                            sprintBeforeRelease = sprintUnsettled,
                            sprintAfterRelease = rc63SprintAfterRelease,
                            transitionAfterRelease = rc63TransitionAfterRelease,
                            stateAfterRelease = player.CurrentStateName.ToString(),
                            nativeSuccessRecorded = false,
                            action = "sameFollowWriterReobservesEftSprintBeforeLateralWalkingCorrection"
                        });
                    }
                    poseOnly = rc63PoseAfterRelease;
                    if (poseOnly)
                    {
                        if (rc69CounterMotionPose)
                        {
                            // Retire the contradicted measurement, otherwise a
                            // stationary correction frame would compare forever
                            // with the previous moving frame. The next real
                            // motion still must be remeasured and requalified.
                            step.Rc48IngressObservedTarget = before;
                            step.Rc48MoverCommandTarget = Vector3.zero;
                            step.Rc48IngressLastSampleAt = Time.time;
                            if (ShouldEmitDiagnostic("v2rc69ClimbCounterMotionPose:" +
                                mission.MissionId.ToString(CultureInfo.InvariantCulture) + ":" +
                                step.EdgeId.ToString(CultureInfo.InvariantCulture), bot, 0.30f))
                                WriteEvent("v2rc69ClimbCounterMotionPose", bot, new
                                {
                                    missionId = mission.MissionId, edgeId = step.EdgeId,
                                    entryDistance, lateral, forward, heading,
                                    measuredLength = rc69LastMeasuredLength,
                                    measuredIntentDot = rc69LastIntentDot,
                                    nativeSuccessRecorded = false, bodyRelocated = false,
                                    action = "oneFollowWriterNeutralizesMeasuredReverseInputBeforeNextClimbWalk"
                                });
                        }
                        TryRequestCF13ClimbSprintRelease(bot, step.EdgeId,
                            "rc59ClimbExactPosePriority");
                        if (bot.Mover != null)
                        {
                            bot.Mover.SetBotMoverPause("R1-CF1C2E177B98",@bot,true);
                            bot.Mover.SetBotMoverIsMoving("R1-922C53315D7C",@bot,false);
                            bot.Mover.BotMoverSprint("R1-FE463414FA5F",@bot,false,false);
                        }
                        player.SetPlayerInputDirection("R1-74FA3AED4924",@bot,Vector2.zero);
                        player.MovementContext.SetMovementContextMovementDirection("R1-31703EEF36CD",@bot,Vector2.zero);
                        // RC67: EFT TransitionStateClass consumes native
                        // LastBlendMotionDelta, NOT the zero Player.Move input.
                        // RC66 195.298s: body at 0.276m/9.2deg, Transition,
                        // InputDirection=zero, yet the next sample moved away.
                        // Neutralize ONLY grounded, same-height, geometrically
                        // valid Climb turn-in-place carry. Preserve vertical
                        // component and never modify native Jump/DropDown momentum.
                        if (geometryReady &&
                            player.CurrentStateName == EPlayerState.Transition &&
                            player.MovementContext.IsGrounded &&
                            entryDistance <= nativeTrigger + 0.45f &&
                            lateral <= maxLateral + 0.15f &&
                            forward >= -(nativeTrigger + 0.50f) &&
                            forward <= Mathf.Max(0.15f, maxForward) &&
                            Mathf.Abs(bot.Position.y - step.RawEntry.y) <= 0.40f)
                        {
                            Vector3 originalBlend = player.MovementContext.LastBlendMotionDelta;
                            Vector3 neutralBlend = new Vector3(0f, originalBlend.y, 0f);
                            if (IsFinite(originalBlend) &&
                                FollowerMovementWriterAuthority.TryCommitNativeTransitionVelocity(
                                    bot, player, neutralBlend) &&
                                ShouldEmitDiagnostic("v2rc67ClimbTransitionCarryNeutralized:" +
                                    mission.MissionId.ToString(CultureInfo.InvariantCulture) + ":" +
                                    step.EdgeId.ToString(CultureInfo.InvariantCulture), bot, 0.30f))
                            {
                                WriteEvent("v2rc67ClimbTransitionCarryNeutralized", bot, new
                                {
                                    missionId = mission.MissionId, edgeId = step.EdgeId,
                                    entryDistance, forward, lateral, heading,
                                    originalBlend = Vec(originalBlend),
                                    neutralBlend = Vec(neutralBlend),
                                    nativeAttempted = false,
                                    bodyRelocated = false,
                                    action = "existingFollowWriterStopsGroundedClimbTransitionPlanarCarryOnly"
                                });
                            }
                        }
                        try { bot.Steering?.BotSteeringLookToDirection("R1-043AA5DFA359",@bot,axis); } catch { }
                    }
                    else
                    {
                        if (bot.Mover != null)
                        {
                            bot.Mover.SetBotMoverPause("R1-1784D4FF16B5",@bot,false);
                            bot.Mover.LocalAvoidance?.GClass492DropOffset("R1-48488B03EDC2",@bot);
                        }
                        // V475 approach geometry, V2 protected Follow writer,
                        // and RC52 world-local projection without stale mover input.
                        Vector3 direction = flat / distance;
                        float dt = Mathf.Max(0.001f, Time.deltaTime);
                        // World-distance * motion does not cap EFT Player.Move
                        // locomotion speed: that API consumes a normalized local
                        // input. Govern the actual native target-speed setting.
                        // Only the Climb pre-probe final 1.30m is walking; keep
                        // the existing non-Climb/landing/successor route untouched.
                        // Once the exact Climb step acquired physical ingress,
                        // never switch back to ordinary Run just because the bot
                        // strayed outside the narrow near-side window. Movement
                        // remains native walking until Native or bounded recovery.
                        float speed = retainClimbWalk
                            ? Mathf.Clamp(distance / 1.25f, 0.22f,
                                walkAndAlignFrontier ? 0.40f : 0.48f)
                            : Mathf.Clamp(distance / 0.90f, 0.55f, 1f);
                        var inputOrigin=CaptureRouteDebtObservation(bot,mission,step).Origin;
                        issuedInput = (motionCommit = ApplyCollisionActivePhysicalRouteMotion(
                            bot, player, direction,
                            direction * Mathf.Min(distance,
                                ChainLocalSuccessorBridgeSpeed * speed * dt),
                            dt, 1f, sprint: false,
                            targetMoveSpeed: ProbeMomentumNormalMoveSpeed * speed,
                            preserveSprintMode: false, exactWorldDirectionInput: true,deliveryTarget:target,deliveryPurpose:CapabilityMotionPurpose.FollowIngress)).Accepted;
                        // RC58: V409 Player.Move already owns the actual locomotion input.
                        // Do not leave EFT/SAIN BotMover manual navigation re-enabled by
                        // ApplyCollisionActivePhysicalRouteMotion (Pause=false, IsMoving=true)
                        // on the very same frame, or it can independently steer/reverse.
                        // This is an existing Follow write scope, not another movement owner.
                        if (motionCommit.UsesExclusiveManualInput && bot.Mover != null)
                        {
                            bot.Mover.SetBotMoverPause("R1-E4F293B7B3DB",@bot,true);
                            bot.Mover.SetBotMoverIsMoving("R1-61E0C04D87EA",@bot,false);
                        }
                        // Player.Move projects world intent into the current
                        // actor frame. Keep actor-facing aligned with the native
                        // axis during the last walking segment, without changing
                        // the actual world-space collision input direction.
                        if (issuedInput && earlyWalkFrontier)
                        {
                            // Center on the same native axis before the final
                            // 1.30m; avoid steering towards a moving staging
                            // point while keeping the physical world input.
                            try { bot.Steering?.BotSteeringLookToDirection("R1-0FFC5CAF3D1E",@bot,axis); } catch { }
                        }
                        if (issuedInput)
                        {
                            step.CapabilityClimbIngressInputOrigin=inputOrigin;
                            step.Rc48IngressObservedTarget = before;
                            step.Rc48MoverCommandTarget = direction;
                            step.Rc48IngressLastSampleAt = Time.time;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                WriteEvent("v2rc57ClimbPhysicalProducerException", bot, new
                {
                    missionId = mission.MissionId, edgeId = step.EdgeId,
                    error = ex.GetType().Name + ": " + ex.Message
                });
                return true;
            }
            bodyDisplacement = GetPlanarDistance(before, bot.Position);
            targetClosure = distance - GetPlanarDistance(bot.Position, target);
            // Same-frame Player.Move is not a completion receipt. The next
            // measured actor pose / original Native Attempt is authoritative.
            if (ShouldEmitDiagnostic("v2rc57SingleClimbPhysicalProducer:" +
                    mission.MissionId.ToString(CultureInfo.InvariantCulture) + ":" +
                    step.EdgeId.ToString(CultureInfo.InvariantCulture), bot, 0.30f))
                WriteEvent("v2rc57SingleClimbPhysicalProducer", bot, new
                {
                    missionId = mission.MissionId, edgeId = step.EdgeId,
                    body = Vec(bot.Position), target = Vec(target), rawEntry = Vec(step.RawEntry),
                    entryDistance, heading, maxHeading, poseOnly, issuedInput,
                    walkAndAlignFrontier, earlyWalkFrontier, nearEntryPose, sprintUnsettled,
                    climbWalkControlOwned, retainClimbWalk,
                    nativeAdmissible, nativeAttemptRecorded = false,
                    rc58NativeMoverPausedAfterDirectInput = issuedInput && bot.Mover?.Pause == true,
                    action = "rc60StepBoundWalkPoseControlNoGenericReentryUntilNativeOrStepExit"
                });
            if (retainClimbWalk && ShouldEmitDiagnostic(
                    "v2rc59ClimbWalkPoseGate:" + mission.MissionId.ToString(CultureInfo.InvariantCulture) + ":" +
                    step.EdgeId.ToString(CultureInfo.InvariantCulture), bot, 0.25f))
            {
                WriteEvent("v2rc59ClimbWalkPoseGate", bot, new
                {
                    missionId = mission.MissionId, edgeId = step.EdgeId,
                    entryDistance, nativeTrigger, heading, maxHeading,
                    forward, lateral, nearEntryPose, walkAndAlignFrontier, earlyWalkFrontier,
                    sprintUnsettled, poseOnly, issuedInput,
                    climbWalkControlOwned, retainClimbWalk,
                    eftSprintEnabled = player.MovementContext.IsSprintEnabled,
                    moverSprinting = bot.Mover?.Sprinting == true,
                    actualState = player.CurrentStateName.ToString(),
                    target = Vec(target), actualBody = Vec(bot.Position),
                    reason = poseOnly ? "turnOrSprintSettlingNoTranslation" : "boundedClimbWalkOnly",
                    nativeAttemptRecorded = false,
                    action = "sameWriterClimbNearEntryTurnBeforeMotionNotNativeAdmission"
                });
            }
            return true;
        }

        private readonly struct CapabilityClimbIngressFrame
        {
            internal readonly Player Player;
            internal readonly Vector3 Axis;
            internal readonly bool Admissible;
            internal readonly float Heading,MaximumHeading,Forward,MaximumForward,Distance;
            internal CapabilityClimbIngressFrame(Player player,Vector3 axis,bool admissible,
                float heading,float maximumHeading,float forward,float maximumForward,float distance)
            {Player=player;Axis=axis;Admissible=admissible;Heading=heading;MaximumHeading=maximumHeading;
             Forward=forward;MaximumForward=maximumForward;Distance=distance;}
        }
        private static bool TryCaptureCapabilityClimbIngress(BotOwner bot,FollowerRouteMission mission,
            FollowerRouteMissionStep step,out CapabilityClimbIngressFrame frame)
        {
            frame=default;
            if(bot==null||!IsLiveBot(bot)||step==null||!string.Equals(step.Kind,"Climb",StringComparison.OrdinalIgnoreCase)||
                !TryValidateV2CurrentTypedStep(bot,mission,step,true,out _)||Time.time>mission.ExpiresAt||
                IsLearningV350TerminalPhysicalRecoveryEligible(bot)||IsTeleportTransactionActive(bot)||
                FollowerStateAuthorityCoordinator.HasCombatMovementClaim(bot,out _)||
                FollowerEnvironment.TryGetImmediateSurvivalThreat(bot,out _))return false;
            if((FollowerCapabilityActuationPolicy.RetiredPhysicalStrategy(
                mission.CurrentStepIndex,mission.LearningV446StrategyEpochStepIndex,step.EdgeId,
                mission.LearningV446StrategyEpochSemanticEdgeId,mission.LearningV446RetiredPhysicalStrategyEdgeId,
                mission.LearningV446RetiredPhysicalStrategyKind,"Climb")))return false;
            var player=bot.GetPlayer;
            if(player?.MovementContext==null||player.MovementContext.OverridenState!=null||
                player.MovementContext.OverridenControlsState!=null)return false;
            var follower=BossPlayers.GetFollowerByProfileId(bot.ProfileId);
            if(follower==null||(follower.TryPeekActiveCommand(out var command,out _,out _)&&command!=FollowerCommandType.None))return false;
            long generation=FollowerMovementWriterAuthority.PeekExactGeneration(bot,out var owner);
            if(generation<=0||(owner!=FollowerMovementWriterAuthority.WriterDomain.Follow&&
                owner!=FollowerMovementWriterAuthority.WriterDomain.Traversal))return false;
            if(!IsFinite(step.RawEntry)||!IsFinite(step.RawExit)||!IsFinite(player.Position))return false;
            var axis=GetFlatDirection(step.RawExit-step.RawEntry);
            if(axis.sqrMagnitude<.5f||!TryGetLearningV424ActuatorExecutorAdmission(bot,step,out bool admissible,out _,
                out float distance,out float forward,out float lateral,out float maxLateral,
                out float heading,out float maxHeading,out float maxForward,out _,out _))return false;
            if(!FollowerCapabilityClimbIngressPolicy.Capture(true,player.MovementContext.IsGrounded,
                player.Position.y-step.RawEntry.y,distance,RouteMissionTraversalAdjacentBridgeMaxDistance,
                forward,maxForward,lateral,Mathf.Max(maxLateral,2.25f)))return false;
            frame=new CapabilityClimbIngressFrame(player,axis,admissible,heading,maxHeading,forward,maxForward,distance);
            return true;
        }
        private static bool TryEnterCapabilityClimbIngressScope(BotOwner bot,FollowerRouteMission mission,
            FollowerRouteMissionStep step,string source,out FollowerMovementWriterAuthority.WriteScope scope,out string reason)
        {
            scope=default;reason="invalidCurrentClimb";
            if(!TryCaptureCapabilityClimbIngress(bot,mission,step,out _))return false;
            long generation=FollowerMovementWriterAuthority.PeekExactGeneration(bot,out var owner);
            if(owner==FollowerMovementWriterAuthority.WriterDomain.Traversal)
                return TryEnterV2CurrentTypedTraversalScope(bot,mission,step,generation,source,out scope,out reason);
            return owner==FollowerMovementWriterAuthority.WriterDomain.Follow&&
                TryEnterV2CurrentIngressScope(bot,mission,step,source,out scope,out reason);
        }
        private static bool TryGetCapabilityClimbIngressFacing(BotOwner bot,Vector3? target,
            CapabilityMotionPurpose purpose,out Vector3 axis)
        {
            axis=default;
            if(purpose!=CapabilityMotionPurpose.FollowIngress||!target.HasValue||!IsFinite(target.Value)||
                !FollowerRouteMissions.TryGetValue(bot.ProfileId,out var mission)||mission?.Steps==null||
                mission.CurrentStepIndex<0||mission.CurrentStepIndex>=mission.Steps.Count||
                !TryCaptureCapabilityClimbIngress(bot,mission,mission.Steps[mission.CurrentStepIndex],out var frame))return false;
            axis=frame.Axis;return true;
        }
        private static bool ShouldPrepareCapabilityClimbIngressRun(BotOwner bot,Player player,
            FollowerRouteMission mission,FollowerRouteMissionStep step)
        {
            if(!TryCaptureCapabilityClimbIngress(bot,mission,step,out var frame)||!ReferenceEquals(frame.Player,player))return false;
            return FollowerCapabilityClimbIngressPolicy.Resume(true,step.Rc48MoverPathRetired,
                player.CurrentStateName==EPlayerState.Idle,bot.Mover?.Sprinting==true||
                player.MovementContext.IsSprintEnabled,frame.Admissible,frame.Heading,frame.MaximumHeading);
        }
        private static bool HasFreshCapabilityClimbIngressInput(BotOwner bot,Player player)
        {
            if(!FollowerRouteMissions.TryGetValue(bot.ProfileId,out var mission)||mission?.Steps==null||
                mission.CurrentStepIndex<0||mission.CurrentStepIndex>=mission.Steps.Count)return false;
            var step=mission.Steps[mission.CurrentStepIndex];
            if(!TryCaptureCapabilityClimbIngress(bot,mission,step,out var frame)||!ReferenceEquals(frame.Player,player))return false;
            long generation=FollowerMovementWriterAuthority.PeekExactGeneration(bot,out _);
            bool currentInput=step.CapabilityClimbIngressInputOrigin.Valid&&
                step.CapabilityClimbIngressInputOrigin.Equals(LifecycleIdentity(mission,step,mission.CurrentStepIndex,generation));
            return FollowerCapabilityClimbIngressPolicy.KeepInput(currentInput,step.Rc48MoverPathRetired,
                step.Rc48MoverCommandTarget.sqrMagnitude>.5f&&player.InputDirection.sqrMagnitude>.0025f,
                Time.time-step.Rc48IngressLastSampleAt,Time.deltaTime);
        }
        private static bool TryConsumeCapabilityClimbIngress(BotOwner bot,FollowerRouteMission mission,FollowerRouteMissionStep step)
        {
            if(!TryCaptureCapabilityClimbIngress(bot,mission,step,out var frame))return false;
            if(frame.Admissible)
            {
                EnsureFollowerRouteMissionCommitment(bot,mission);
                return TryStartProbeForBot(bot,frame.Axis,"learnedTraversalCommitment",step.EdgeId,"Climb",
                    "capabilityClimbIngress",out _);
            }
            Vector3 target=bot.Position;string source="capabilityClimbIngressPose";bool selected=false;
            if(TryGetLearningV363ActuatorRouteEntryLaneTarget(bot,mission,step,out var lane,out _,out _,
                out _,out _,out _,out _,out _,out var laneSource)&&IsFinite(lane))
            {
                float direct=GetPlanarDistance(bot.Position,lane);
                bool clear=!NavMesh.Raycast(bot.Position,lane,out _,NavMesh.AllAreas);
                var path=clear?CalculateStrictPath(bot.Position,lane):null;
                float projection=Vector3.Dot(lane-step.RawEntry,frame.Axis);
                if(FollowerCapabilityClimbIngressPolicy.Target(clear,path?.StrictComplete==true,direct,
                    path?.PathLength??float.PositiveInfinity,lane.y-bot.Position.y,projection,frame.MaximumForward))
                {target=lane;source=laneSource;selected=true;}
            }
            if(!selected&&TrySelectCapabilityPhysicalClimbApproach(bot,mission,step,out var physical))
            {target=physical;source="currentEpisodePhysicalCorridor";selected=true;}
            if(!TryCaptureCapabilityClimbIngress(bot,mission,step,out _))return false;
            bool handled=TryDriveLearningV379ActuatorPreProbePhysicalLane(bot,mission,step,target,source,true,out _,out _);
            bool inputAccepted=step.Rc48IngressLastSampleAt==Time.time&&step.Rc48MoverCommandTarget.sqrMagnitude>.5f;
            ObserveV2CurrentStepPhysicalProgressReceipt(bot,mission,step,target,"capabilityClimbIngress",inputAccepted,selected);
            if(ShouldEmitDiagnostic("capabilityClimbIngress:"+step.EpisodeId,bot,.25f))
                WriteEvent("capabilityClimbIngressConsumerFrame",bot,new{missionId=mission.MissionId,
                    stepIndex=mission.CurrentStepIndex,episodeId=step.EpisodeId,edgeId=step.EdgeId,
                    target=Vec(target),source,selected,handled,inputAccepted,axis=Vec(frame.Axis),
                    heading=frame.Heading,state=frame.Player.CurrentStateName.ToString(),
                    nativeSuccessRecorded=false,bodyRelocated=false});
            return handled;
        }
        private static void TickCapabilityClimbIngressFrames()
        {
            foreach(var pair in FollowerRouteMissions.ToArray())
            {
                var mission=pair.Value;
                if(mission?.Steps==null||mission.CurrentStepIndex<0||mission.CurrentStepIndex>=mission.Steps.Count)continue;
                var bot=BossPlayers.GetFollowerByProfileId(pair.Key)?.GetBot();
                TryConsumeCapabilityClimbIngress(bot,mission,mission.Steps[mission.CurrentStepIndex]);
            }
        }
    }
}
