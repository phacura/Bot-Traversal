using EFT;
using UnityEngine;

namespace BotTraversal.Core
{
    // CF15 mechanical extraction: one physical-route progress decision owner.
    // This is the same static partial runtime as CF14, not an independent mover,
    // path selector, timer, lease, or recovery loop. No persistent data changes.
    internal static partial class FollowerTraversalTestRuntime
    {
        private static void EvaluatePhysicalRouteProgress(
            BotOwner bot,
            bool haveMission,
            FollowerRouteMission mission,
            FollowerTraversalRouteCursor cursor,
            Vector3 position,
            float learningV376BodySampleDistance,
            Vector3 learningV377BodyDelta,
            out float learningV377CurrentRouteArc,
            out bool learningV377RouteRegressionInvalidatesProducer,
            out bool learningV395PhysicalBodyRouteRegression)
        {
            learningV377RouteRegressionInvalidatesProducer = false;
            learningV395PhysicalBodyRouteRegression = false;
            learningV377CurrentRouteArc = cursor.LearningV375RouteArcLength;
            if (haveMission)
            {
                cursor.LearningV375RouteMissionId = mission.MissionId;
                if (TryProjectLearningV375RouteArc(
                        mission, position,
                        out float routeArc, out Vector3 tangent,
                        out Vector3 projectedPoint, out int projectedSequence))
                {
                    cursor.LearningV375RouteArcLength = routeArc;
                    learningV377CurrentRouteArc = routeArc;
                    if (routeArc + 0.05f >= cursor.LearningV375BestRouteArcLength)
                    {
                        cursor.LearningV375BestRouteArcLength = Mathf.Max(cursor.LearningV375BestRouteArcLength, routeArc);
                        cursor.LearningV375RouteTangent = tangent;
                    }
                    else
                    {
                        float physicalMovementDot = 0f;
                        bool physicalMovementBackward = false;
                        if (learningV376BodySampleDistance > 0.01f &&
                            cursor.LearningV375RouteTangent.sqrMagnitude > 0.25f)
                        {
                            physicalMovementDot = Vector3.Dot(
                                learningV377BodyDelta.normalized,
                                cursor.LearningV375RouteTangent.normalized);
                            physicalMovementBackward = physicalMovementDot < 0f;
                        }
                        // CF27: a real EFT Vault/Climb animation may retract its body for a
                        // frame while crossing a lip. V377 protects NAVIGATION producers from
                        // running backward; it must not revoke the *same* typed native actuator
                        // while that actuator exclusively owns body translation. The physical
                        // route arc, best-progress watermark and negative body dot are preserved.
                        // This exemption is forbidden for pre-admission, stale/wrong-episode
                        // probes, Follow/other writer epochs, Egress/Landing, failed or assisted
                        // execution, and ordinary WalkThrough/Jump/DropDown navigation.
                        bool cf27NativeActuatorOwnsAnimationRegression = false;
                        if (physicalMovementBackward && cursor.LearningV375ActuatorBorrowed &&
                            cursor.LearningV375RouteMissionId == mission.MissionId &&
                            mission.CurrentStepIndex >= 0 &&
                            mission.CurrentStepIndex < mission.Steps.Count &&
                            (cursor.LearningV375ActuatorKind == "Climb" ||
                             cursor.LearningV375ActuatorKind == "Vault") &&
                            TryGetActiveProbe(bot, out ProbeRuntime cf27Probe) &&
                            cf27Probe != null && IsActiveProbe(cf27Probe) &&
                            cf27Probe.Bot == bot && cf27Probe.Player != null &&
                            cf27Probe.Stage == ProbeStage.Traversing &&
                            cf27Probe.NativeExecutorConsumed && cf27Probe.NativeStateObserved &&
                            !cf27Probe.AssistedReplayAttempted &&
                            !cf27Probe.TraversalTeleportAttempted &&
                            !cf27Probe.TeleportFallbackUsed &&
                            !cf27Probe.NativeLandingRejected &&
                            IsProbeTraversalState(cf27Probe.Player.CurrentStateName) &&
                            cf27Probe.MovementWriterGeneration > 0 &&
                            cf27Probe.MovementWriterGeneration == cursor.LearningV375MovementGeneration)
                        {
                            FollowerRouteMissionStep cf27Step = mission.Steps[mission.CurrentStepIndex];
                            cf27NativeActuatorOwnsAnimationRegression = cf27Step != null &&
                                cf27Step.EdgeId > 0 &&
                                cf27Step.EdgeId == cf27Probe.LearnedEdgeId &&
                                cf27Step.EpisodeId > 0 &&
                                cf27Step.EpisodeId == cf27Probe.TraversalEpisodeId &&
                                cf27Step.Kind == cursor.LearningV375ActuatorKind;
                        }
                        // CF29: the canonical route arc may move backwards while the SAME
                        // current-episode Climb/Vault probe is making measured lateral entry
                        // convergence. Do not revoke its existing physical navigation producer
                        // for that corrective step. Require the actual body to move closer to
                        // the authored entry lane WITHOUT moving farther from RawEntry. Do not
                        // alter the route arc, best watermark, body, or actuator admission.
                        bool cf29MeasuredEntryLaneConvergence = false;
                        if (physicalMovementBackward && !cf27NativeActuatorOwnsAnimationRegression &&
                            learningV376BodySampleDistance > 0.01f &&
                            cursor.LearningV375RouteMissionId == mission.MissionId &&
                            mission.CurrentStepIndex >= 0 &&
                            mission.CurrentStepIndex < mission.Steps.Count &&
                            TryGetActiveProbe(bot, out ProbeRuntime cf29Probe) &&
                            cf29Probe != null && IsActiveProbe(cf29Probe) &&
                            cf29Probe.Bot == bot && cf29Probe.Player != null &&
                            cf29Probe.Stage == ProbeStage.RollingApproach &&
                            !cf29Probe.NativeExecutorConsumed &&
                            !cf29Probe.AssistedReplayAttempted &&
                            !cf29Probe.TraversalTeleportAttempted &&
                            (string.Equals(cf29Probe.RequestedTraversalKind, "Climb", System.StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(cf29Probe.RequestedTraversalKind, "Vault", System.StringComparison.OrdinalIgnoreCase)))
                        {
                            FollowerRouteMissionStep cf29Step = mission.Steps[mission.CurrentStepIndex];
                            long cf29Generation = FollowerMovementWriterAuthority.GetCurrentGeneration(
                                bot, out FollowerMovementWriterAuthority.WriterDomain cf29Owner);
                            if (cf29Step != null && cf29Step.EdgeId == cf29Probe.LearnedEdgeId &&
                                cf29Step.EpisodeId > 0 &&
                                cf29Step.EpisodeId == cf29Probe.TraversalEpisodeId &&
                                cf29Step.ChainId > 0 &&
                                cf29Step.ChainId == cursor.LearningV375TransactionChainId &&
                                cf29Owner == FollowerMovementWriterAuthority.WriterDomain.Traversal &&
                                cf29Generation > 0 && cf29Generation == cf29Probe.MovementWriterGeneration)
                            {
                                Vector3 cf29Entry = IsFinite(cf29Step.RawEntry)
                                    ? cf29Step.RawEntry : cf29Step.Entry;
                                Vector3 cf29Exit = IsFinite(cf29Step.RawExit)
                                    ? cf29Step.RawExit : cf29Step.Exit;
                                Vector3 cf29Axis = GetFlatDirection(cf29Exit - cf29Entry);
                                Vector3 cf29PreviousBody = position - learningV377BodyDelta;
                                Vector3 cf29Now = position - cf29Entry;
                                Vector3 cf29Before = cf29PreviousBody - cf29Entry;
                                cf29Now.y = 0f;
                                cf29Before.y = 0f;
                                float cf29NowProjection = Vector3.Dot(cf29Now, cf29Axis);
                                float cf29NowLateral = (cf29Now - cf29Axis * cf29NowProjection).magnitude;
                                float cf29BeforeProjection = Vector3.Dot(cf29Before, cf29Axis);
                                float cf29BeforeLateral = (cf29Before - cf29Axis * cf29BeforeProjection).magnitude;
                                cf29MeasuredEntryLaneConvergence =
                                    IsFinite(cf29Entry) && IsFinite(cf29Exit) &&
                                    cf29Axis.sqrMagnitude > 0.5f &&
                                    cf29Now.magnitude <= 2f &&
                                    cf29NowProjection < 0.10f &&
                                    cf29BeforeLateral - cf29NowLateral >= 0.012f &&
                                    cf29Now.magnitude <= cf29Before.magnitude + 0.02f;
                                if (cf29MeasuredEntryLaneConvergence &&
                                    ShouldEmitDiagnostic("cf29MeasuredEntryLaneConvergenceRetained", bot, 0.20f))
                                {
                                    WriteEvent("cf29MeasuredEntryLaneConvergenceRetained", bot, new
                                    {
                                        missionId = mission.MissionId,
                                        edgeId = cf29Step.EdgeId,
                                        episodeId = cf29Step.EpisodeId,
                                        generation = cf29Generation,
                                        kind = cf29Step.Kind,
                                        lateralBefore = cf29BeforeLateral,
                                        lateralNow = cf29NowLateral,
                                        distanceBefore = cf29Before.magnitude,
                                        distanceNow = cf29Now.magnitude,
                                        routeArc,
                                        bestRouteArc = cursor.LearningV375BestRouteArcLength,
                                        physicalMovementDot,
                                        traversalSuccessRecorded = false,
                                        persistentLearningChanged = false,
                                        bodyRelocated = false,
                                        action = "exactLiveEntryLaneCorrectionPreservesProducerWithoutForgingRouteProgress"
                                    });
                                }
                            }
                        }
                        learningV377RouteRegressionInvalidatesProducer =
                            (physicalMovementBackward) &&
                            !cf27NativeActuatorOwnsAnimationRegression &&
                            !cf29MeasuredEntryLaneConvergence;
                        learningV395PhysicalBodyRouteRegression = learningV377RouteRegressionInvalidatesProducer;
                        if (cf27NativeActuatorOwnsAnimationRegression &&
                            ShouldEmitDiagnostic("cf27NativeActuatorAnimationRegressionRetained", bot, 0.25f))
                        {
                            WriteEvent("cf27NativeActuatorAnimationRegressionRetained", bot, new
                            {
                                chainId = cursor.LearningV375TransactionChainId,
                                missionId = mission.MissionId,
                                edgeId = mission.Steps[mission.CurrentStepIndex].EdgeId,
                                episodeId = mission.Steps[mission.CurrentStepIndex].EpisodeId,
                                generation = cursor.LearningV375MovementGeneration,
                                kind = cursor.LearningV375ActuatorKind,
                                nativeState = bot.GetPlayer.CurrentStateName.ToString(),
                                routeArc,
                                bestRouteArc = cursor.LearningV375BestRouteArcLength,
                                bodySampleDistance = learningV376BodySampleDistance,
                                physicalMovementDot,
                                producerInvalidated = false,
                                traversalSuccessRecorded = false,
                                persistentLearningChanged = false,
                                bodyRelocated = false,
                                action = "retainExactNativeActuatorDuringAnimationOnlyDoNotResetRouteProgressOrNavigationSafety"
                            });
                        }
                        if (ShouldEmitDiagnostic("learningV375RouteArcRegression", bot, 0.25f))
                        {
                            WriteEvent(
                                learningV377RouteRegressionInvalidatesProducer
                                    ? "learningV377PhysicalRouteRegressionInvalidatedProducer"
                                    : "learningV375RouteArcRegressionObserved",
                                bot, new
                                {
                                    chainId = cursor.LearningV375TransactionChainId,
                                    missionId = mission.MissionId,
                                    routeArc,
                                    bestRouteArc = cursor.LearningV375BestRouteArcLength,
                                    position = Vec(position),
                                    projectedPoint = Vec(projectedPoint),
                                    producer = cursor.LearningV375ProducerKind ?? string.Empty,
                                    bodySampleDistance = learningV376BodySampleDistance,
                                    physicalMovementDot,
                                    producerInvalidated = learningV377RouteRegressionInvalidatesProducer,
                                    action = learningV377RouteRegressionInvalidatesProducer
                                        ? "physicalBodyRegressionInvalidatesNavigationProducerNeverRepairByMovingBodyBackward"
                                        : "observeProjectionRegressionWithoutFabricatingPhysicalFailure"
                                });
                        }
                    }
                }
            }

        }
    }
}
