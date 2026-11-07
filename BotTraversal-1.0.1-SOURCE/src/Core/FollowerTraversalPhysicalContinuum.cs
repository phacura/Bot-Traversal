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
        private static bool TryContinueLearningV365ContinuousTypedPhysicalRoute(
            BotOwner bot,
            FollowerRouteMission mission,
            FollowerRouteMissionStep step,
            out int targetSequence)
        {
            targetSequence = 0;
            string profileId = bot?.ProfileId ?? string.Empty;
            if (string.IsNullOrEmpty(profileId) || mission == null || step == null || HasActiveTraversalProbe(bot) ||
                !LearningV365ContinuousRouteProducerStates.TryGetValue(profileId, out LearningV365ContinuousRouteProducerState producer) ||
                producer == null || producer.Generic || producer.MissionId != mission.MissionId ||
                producer.StepIndex != mission.CurrentStepIndex || producer.EdgeId != step.EdgeId)
            {
                return false;
            }
            if (TryRetireLearningV435WalkThroughDebtOnDownstreamNavMeshRejoin(
                    bot, mission, step, "continuousTypedPhysicalRoute"))
            {
                return true;
            }
            if (TryHandoffLearningV388PhysicalCorridorToReadySuccessor(bot, mission, step))
            {
                return true;
            }
            if (!TryAdvanceLearningV365TypedPhysicalTarget(bot, mission, step, producer))
            {
                if ((IsLearningV369PhysicalCorridorStep(step)))
                {
                    int learningV391PreviousStepIndex = mission.CurrentStepIndex;
                    bool learningV391Committed = EnsureFollowerRouteMissionCommitment(bot, mission);
                    if (learningV391Committed && mission.CurrentStepIndex != learningV391PreviousStepIndex)
                    {
                        WriteEvent("learningV391PhysicalProducerCommittedPassedFrontier", bot, new
                        {
                            missionId = mission.MissionId,
                            previousStepIndex = learningV391PreviousStepIndex,
                            currentStepIndex = mission.CurrentStepIndex,
                            edgeId = step.EdgeId,
                            kind = step.Kind,
                            traversalSuccessRecorded = false,
                            persistentLearningChanged = false,
                            action = "typedPhysicalFrontierImmediatelyHandsExecutionCursorToCanonicalMissionSuccessor"
                        });
                    }
                }
                return false;
            }

            // The breadcrumb advance may also advance the canonical RouteStep.
            // Do not renew recovery or publish a physical frame for a retired Step.
            if (!TryValidateV2CurrentTypedStep(bot, mission, step, true,
                    out string currentStepRejectReason))
            {
                RecordV2TypedStepLeaseRejection(bot, mission, step,
                    "learningV365ContinuousTypedPhysicalRoute:afterTargetAdvance",
                    currentStepRejectReason, "v2rc72TypedTraversalLeaseRejected");
                return false;
            }
            RefreshLearningV424RecoveryLeaseFromTypedPhysicalProgress(
                bot, producer, bot.Position, "typedBreadcrumbAdvance");

            targetSequence = producer.TargetSequence;
            int frame = Time.frameCount;
            if (LearningV365ContinuousRouteLastDriveFrame.TryGetValue(profileId, out int lastDriveFrame) && lastDriveFrame == frame)
            {
                return true;
            }

            FollowerTraversalRouteCursor learningV376Cursor = GetOrCreateLearningV375RouteCursor(bot);
            bool learningV376UsePersistentTraversalWriter = (learningV376Cursor != null) && learningV376Cursor.LearningV375TransactionActive &&
                learningV376Cursor.LearningV375RouteMissionId == mission.MissionId &&
                learningV376Cursor.LearningV375MovementGeneration > 0 &&
                FollowerMovementWriterAuthority.GetCurrentGeneration(
                    bot, out FollowerMovementWriterAuthority.WriterDomain learningV376Owner) ==
                    learningV376Cursor.LearningV375MovementGeneration &&
                learningV376Owner == FollowerMovementWriterAuthority.WriterDomain.Traversal;
            // V2-RC19: do not mint/renew an owner for an inert or missing-physics frame.
            // The actual moving frame below issues one exact physical write scope.
            Player player = bot.GetPlayer ?? bot.AIData?.Player;
            if (player?.MovementContext == null)
            {
                return false;
            }
            Vector3 physicalDelta = producer.Target - bot.Position;
            Vector3 flat = physicalDelta;
            flat.y = 0f;
            float flatDistance = flat.magnitude;
            float distance = GetLearningV380PhysicalBreadcrumbDistance(step, bot.Position, producer.Target);
            if (distance <= 0.001f)
            {
                return true;
            }
            Vector3 direction = flatDistance > 0.001f
                ? flat / flatDistance
                : GetFlatDirection(step.Exit - step.Entry);
            bool learningV386ThreeDimensionalRoute = UsesLearningV386ThreeDimensionalPhysicalRoute(step);
            Vector3 motionDirection = learningV386ThreeDimensionalRoute
                ? physicalDelta / distance
                : direction;
            float dt = Mathf.Max(0.001f, Time.deltaTime);
            Vector3 before = bot.Position;
            float beforeTargetDistance = distance;
            long generation = 0L; // Diagnostic-only, copied from the issued scope.
            try
            {
                FollowerMovementWriterAuthority.WriteScope physicalScope;
                if (learningV376UsePersistentTraversalWriter)
                {
                    // Both the live Traversal epoch and this SAME RouteStep must
                    // still own the physical producer; the helper never mints an owner.
                    if (!TryEnterV2CurrentTypedTraversalScope(
                            bot, mission, step,
                            learningV376Cursor.LearningV375MovementGeneration,
                            "learningV376PreProbePhysicalProducer:mission=" + mission.MissionId.ToString(CultureInfo.InvariantCulture),
                            out physicalScope, out _))
                        return false;
                }
                else if (!TryEnterV2CurrentIngressScope(
                    bot, mission, step, "learningV365ContinuousEmpiricalRouteCorridor",
                    out physicalScope, out _))
                {
                    return false;
                }
                generation = physicalScope.IssuedGeneration;
                // A denied ticket is not a physical drive. Do not burn this
                // frame's deduplication slot before the owner accepts the write.
                LearningV365ContinuousRouteLastDriveFrame[profileId] = frame;
                using (physicalScope)
                {
                    if (bot.Mover != null)
                    {
                        if ((learningV376UsePersistentTraversalWriter))
                        {
                            if (!bot.Mover.Pause)
                            {
                                bot.Mover.SetBotMoverPause("R1-BA7D425A7A2E",@bot,true);
                                producer.MoverPausedForExclusiveTranslation = true;
                                // Preserve the issuer of the real Pause write, not a later snapshot.
                                producer.ExclusivePauseWriterGeneration = generation;
                            }
                        }
                        else
                        {
                            bot.Mover.SetBotMoverPause("R1-43256B13556B",@bot,false);
                        }
                    }
                    float learningV381PhysicalDriveSpeed = GetLearningV381PhysicalCorridorDriveSpeed(step);
                    bool learningV381PhysicalSprintIntent = GetLearningV381PhysicalCorridorSprintIntent(step);
                    if(TryPrepareCapabilityAdjacentJumpRunup(bot,player,mission,step,direction))learningV381PhysicalSprintIntent=true;
                    bool stepMotion=IsLearningV369PhysicalCorridorStep(step)&&
                        FollowerMovementRecoveryStates.TryGetValue(profileId,out var recovery)&&
                        TryDriveCapabilityGenericLowStep(bot,player,recovery,producer,direction,generation,dt);
                    if(!stepMotion)ApplyCollisionActivePhysicalRouteMotion(
                        bot, player, direction,
                        motionDirection * Mathf.Min(distance, learningV381PhysicalDriveSpeed * dt),
                        dt, producer.Pose,
                        learningV381PhysicalSprintIntent,
                        learningV381PhysicalSprintIntent
                            ? ProbeMomentumFullMoveSpeed
                            : ProbeMomentumNormalMoveSpeed,deliveryTarget:producer.Target,deliveryPurpose:CapabilityMotionPurpose.TypedRoute);
                }
            }
            catch (Exception ex)
            {
                WriteEvent("learningV365ContinuousPhysicalRouteDriveException", bot, new
                {
                    missionId = mission.MissionId,
                    edgeId = step.EdgeId,
                    breadcrumbSequence = producer.TargetSequence,
                    error = ex.GetType().Name + ": " + ex.Message
                });
                return true;
            }

            Vector3 after = bot.Position;
            bool learningV380ThreeDimensionalProgress = learningV386ThreeDimensionalRoute;
            float afterTargetDistance = GetLearningV380PhysicalBreadcrumbDistance(step, after, producer.Target);
            float frameBodyDistance = learningV380ThreeDimensionalProgress
                ? Vector3.Distance(before, after)
                : GetPlanarDistance(before, after);
            float frameTargetClosure = beforeTargetDistance - afterTargetDistance;
            Vector3 cumulative = after - mission.LastProgressPosition;
            if (!learningV380ThreeDimensionalProgress)
            {
                cumulative.y = 0f;
            }
            float cumulativeDisplacement = cumulative.magnitude;
            Vector3 learningV380ProgressDirection = learningV380ThreeDimensionalProgress
                ? motionDirection
                : direction;
            float cumulativeForward = learningV380ProgressDirection.sqrMagnitude > 0.5f
                ? Vector3.Dot(cumulative, learningV380ProgressDirection)
                : 0f;
            bool measured;
            {
                if (producer.LearningV386ProgressTargetSequence != producer.TargetSequence)
                {
                    producer.LearningV386ProgressTargetSequence = producer.TargetSequence;
                    producer.LearningV386AccumulatedBodyDistance = 0f;
                    producer.LearningV386AccumulatedTargetClosure = 0f;
                }
                producer.LearningV386AccumulatedBodyDistance += frameBodyDistance;
                producer.LearningV386AccumulatedTargetClosure += frameTargetClosure;
                measured = producer.LearningV386AccumulatedBodyDistance >=
                               LearningV350MeasuredPhysicalProgressDistance &&
                           producer.LearningV386AccumulatedTargetClosure >=
                               LearningV350MeasuredTargetProgressDistance;
                if (measured)
                {
                    producer.LearningV386AccumulatedBodyDistance = 0f;
                    producer.LearningV386AccumulatedTargetClosure = 0f;
                    producer.LearningV386LastMeasuredForwardProgressAt = Time.time;
                }
            }
            if (measured)
            {
                mission.LastProgressAt = Time.time;
                mission.LastProgressPosition = after;

                RefreshLearningV424RecoveryLeaseFromTypedPhysicalProgress(
                    bot, producer, after, "typedLocalMeasuredProgress");
            }
            if (ShouldEmitDiagnostic(
                    "learningV365ContinuousPhysicalRouteProducer:" + mission.MissionId.ToString(CultureInfo.InvariantCulture) + ":" +
                    step.EdgeId.ToString(CultureInfo.InvariantCulture), bot, 0.35f))
            {
                WriteEvent("learningV365ContinuousPhysicalRouteProducer", bot, new
                {
                    missionId = mission.MissionId,
                    stepIndex = mission.CurrentStepIndex,
                    episodeId = step.EpisodeId,
                    edgeId = step.EdgeId,
                    kind = step.Kind,
                    breadcrumbSequence = producer.TargetSequence,
                    physicalApproachBreadcrumbSequence = mission.PhysicalApproachBreadcrumbSequence,
                    target = Vec(producer.Target),
                    physicalDisplacement = learningV380ThreeDimensionalProgress
                        ? Vector3.Distance(before, after)
                        : GetPlanarDistance(before, after),
                    targetClosure = frameTargetClosure,
                    localProgressBodyAccumulator = (producer.LearningV386AccumulatedBodyDistance),
                    localProgressTargetClosureAccumulator = (producer.LearningV386AccumulatedTargetClosure),
                    localBreadcrumbProgressTruth = true,
                    verticalTargetError = producer.Target.y - before.y,
                    threeDimensionalPhysicalPassage = learningV380ThreeDimensionalProgress,
                    cumulativePhysicalDisplacement = cumulativeDisplacement,
                    cumulativeRouteForwardProgress = cumulativeForward,
                    measuredPhysicalProgress = measured,
                    frame,
                    producer = learningV376UsePersistentTraversalWriter
                        ? "persistentRouteTransaction"
                        : "typedRouteMission",
                    movementGeneration = generation,
                    missionScopedTransaction = learningV376UsePersistentTraversalWriter,
                    action = learningV376UsePersistentTraversalWriter
                        ? "preProbePhysicalRouteUsesSameMissionScopedTraversalWriterAsFutureActionNodes"
                        : "integrateAlreadyAdmittedPhysicalRouteWithoutPerFramePathReplanning"
                });
            }
            return true;
        }
    }
}
