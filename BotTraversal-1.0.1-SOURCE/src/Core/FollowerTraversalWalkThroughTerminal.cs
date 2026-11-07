using System;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace BotTraversal.Core
{
    internal static partial class FollowerTraversalTestRuntime
    {
        private readonly struct V2WalkThroughFarSideObservation
        {
            internal readonly Vector3 Entry, Exit, Direction;
            internal readonly float FarSideProjection, PlayerProjection, BodyProjection;
            internal readonly float LateralDistance, VerticalDistance;
            internal V2WalkThroughFarSideObservation(Vector3 entry, Vector3 exit,
                Vector3 direction, float projection, float playerProjection,
                float bodyProjection, float lateral, float vertical)
            {
                Entry = entry; Exit = exit; Direction = direction;
                FarSideProjection = projection; PlayerProjection = playerProjection;
                BodyProjection = bodyProjection;
                LateralDistance = lateral; VerticalDistance = vertical;
            }
        }

        // Shared current-Episode geometry observation. The same measured span
        // drives both the unchanged completion gate and the forward-only plan.
        private static bool TryMeasureV2CurrentEpisodeWalkThroughFarSide(
            ProbeRuntime probe, out V2WalkThroughFarSideObservation observation)
        {
            observation = default(V2WalkThroughFarSideObservation);
            if (probe?.Player == null || probe.Bot == null ||
                !probe.LearningV372CurrentEpisodeWalkThroughCorridorAuthority ||
                probe.TraversalEpisodeId <= 0)
                return false;
            TraversalEpisodeMetadata episode = TraversalEpisodeStream
                .Where(item => item != null && item.EpisodeId == probe.TraversalEpisodeId &&
                               IsLearningV435WalkThroughEpisodeSemanticAlias(
                                   item, probe.LearnedEdgeId, out _, out _))
                .OrderByDescending(item => item.EpisodeId)
                .FirstOrDefault();
            if (episode == null || !episode.LearningV372HasCurrentExecutionGeometry ||
                !IsFinite(episode.LearningV372CurrentExecutionStart) ||
                !IsFinite(episode.LearningV372CurrentExecutionEnd) ||
                !IsFinite(probe.Player.Position) || !IsFinite(probe.Bot.Position))
            {
                return false;
            }

            Vector3 currentEpisodeEntry = episode.LearningV372CurrentExecutionStart;
            Vector3 currentEpisodeExit = episode.LearningV372CurrentExecutionEnd;
            Vector3 axis = currentEpisodeExit - currentEpisodeEntry;
            axis.y = 0f;
            float span = axis.magnitude;
            if (span < WalkThroughPassageSampleMinPlanarSpacing)
            {
                return false;
            }

            Vector3 direction = axis / span;
            Vector3 playerDelta = probe.Player.Position - currentEpisodeExit;
            playerDelta.y = 0f;
            Vector3 bodyDelta = probe.Bot.Position - currentEpisodeExit;
            bodyDelta.y = 0f;
            float playerFarSideProjection = Vector3.Dot(playerDelta, direction);
            float bodyFarSideProjection = Vector3.Dot(bodyDelta, direction);
            float farSideProjection = Mathf.Min(playerFarSideProjection, bodyFarSideProjection);

            Vector3 playerAxisPoint = currentEpisodeExit + direction * playerFarSideProjection;
            Vector3 bodyAxisPoint = currentEpisodeExit + direction * bodyFarSideProjection;
            float lateralDistance = Mathf.Max(
                GetPlanarDistance(probe.Player.Position, playerAxisPoint),
                GetPlanarDistance(probe.Bot.Position, bodyAxisPoint));
            float verticalDistance = Mathf.Max(
                Mathf.Abs(probe.Player.Position.y - currentEpisodeExit.y),
                Mathf.Abs(probe.Bot.Position.y - currentEpisodeExit.y));

            observation = new V2WalkThroughFarSideObservation(currentEpisodeEntry,
                currentEpisodeExit, direction, farSideProjection,
                playerFarSideProjection, bodyFarSideProjection,
                lateralDistance, verticalDistance);
            return true;
        }

        // Adapter only: one plan, at most one call to the EXISTING exact probe
        // commit boundary, no new writer, timer, relocation or success authority.
        private static void DispatchV2WalkThroughTerminalPlan(
            ProbeRuntime probe, Vector3 terminalForward)
        {
            bool available = TryMeasureV2CurrentEpisodeWalkThroughFarSide(
                probe, out V2WalkThroughFarSideObservation observation);
            float agreement = available
                ? Vector3.Dot(observation.Direction, terminalForward) : -1f;
            FollowerWalkThroughTerminalPolicy.Plan plan = FollowerWalkThroughTerminalPolicy.Select(
                probe.LearningV372CurrentEpisodeWalkThroughCorridorAuthority,
                probe.LearningV372WalkThroughPhysicalPassageProven,
                probe.LearningV420ActiveTerminalRouteRejoinProven,
                available, observation.FarSideProjection, observation.LateralDistance,
                observation.VerticalDistance, agreement, LearningV32EntryProgressEpsilon,
                WalkThroughPassageStraightLateralTolerance, WalkThroughMaxVerticalDelta);
            if (plan.Kind == FollowerWalkThroughTerminalPolicy.Action.Verify)
            {
                BeginUnifiedTraversalEgressOrVerify(
                    probe, "learningV419WalkThroughTerminalFrontierCrossed");
                return;
            }

            Vector3 target = probe.Player.Position;
            bool targetInCorridor = false;
            bool published = false;
            if (plan.Kind == FollowerWalkThroughTerminalPolicy.Action.Advance)
            {
                target += observation.Direction * plan.ForwardDistance;
                // Keep the exact existing finite corridor safety envelope.
                // Never steer to a newly extended endpoint beyond that envelope.
                targetInCorridor = IsFinite(target) &&
                    TryGetLearningV372CurrentEpisodeWalkThroughCorridorWitness(
                        probe, target, out _, out _, out float targetLateral,
                        out float targetVertical) &&
                    targetLateral <= WalkThroughPassageStraightLateralTolerance &&
                    targetVertical <= WalkThroughPassageMicroVerticalDeviation;
                if (targetInCorridor)
                    published = TryPublishLearningV414PathlessNativeInputLease(
                        probe, target, GetWalkThroughPrecisionSpeed(probe),
                        "walkThroughCurrentEpisodeTerminalContinuation");
            }
            if (ShouldEmitDiagnostic("v2rc78WalkThroughTerminalPlan:" +
                    probe.LearnedEdgeId.ToString(CultureInfo.InvariantCulture), probe.Bot, 0.25f))
                WriteEvent("v2rc78WalkThroughTerminalPlan", probe.Bot, new
                {
                    edgeId = probe.LearnedEdgeId, episodeId = probe.TraversalEpisodeId,
                    chainId = probe.TraversalChainId,
                    movementGeneration = probe.MovementWriterGeneration,
                    plan = plan.Kind.ToString(), currentGeometryAvailable = available,
                    body = Vec(probe.Player.Position),
                    currentEntry = available ? Vec(observation.Entry) : null,
                    currentExit = available ? Vec(observation.Exit) : null,
                    currentFarSideProjection = observation.FarSideProjection,
                    currentFarSideLateral = observation.LateralDistance,
                    currentFarSideVertical = observation.VerticalDistance,
                    directionAgreement = agreement,
                    forwardDistance = plan.ForwardDistance,
                    target = Vec(target), targetInCorridor, inputPublished = published,
                    nativeSuccessRecorded = false, bodyRelocated = false,
                    action = published
                        ? "refreshExistingExactInputThenRequireLaterFullPhysicalPassageProof"
                        : "retainUnprovenDebtForExistingBoundedRecovery"
                });
        }
    }
}
