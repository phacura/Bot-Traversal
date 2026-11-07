// R3X_ROUTE_CONTINUATION_REVIEWED
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
    internal static partial class FollowerTraversalTestRuntime
    {
        private static CapabilityContinuationFailure ObserveCapabilityRouteContinuation(
            BotOwner bot,Player player,FollowerRouteMission mission,FollowerRouteMissionStep step,
            long issuedGeneration,ProbeRuntime probe,FollowerTraversalRouteCursor cursor,CapabilityContinuationPhase phase)
        {
            if(bot==null||mission==null||step==null||player?.MovementContext==null)
                return CapabilityContinuationFailure.MissingOrigin;
            if(phase==CapabilityContinuationPhase.NativeLanding&&probe==null)return CapabilityContinuationFailure.MissingNativeProbe;
            bool cursorCurrent=cursor!=null&&cursor.LearningV375TransactionActive&&
                cursor.LearningV375RouteMissionId==mission.MissionId&&cursor.LearningV375MovementGeneration==issuedGeneration;
            long generation=FollowerMovementWriterAuthority.PeekExactGeneration(bot,out var owner);
            var origin=probe?.Lifecycle?.Origin??LifecycleIdentity(mission,step,mission.CurrentStepIndex,issuedGeneration);
            var live=LifecycleIdentity(mission,step,mission.CurrentStepIndex,generation);
            bool current=Time.time<=mission.ExpiresAt&&TryValidateV2CurrentTypedStep(bot,mission,step,false,out _);
            bool actor=IsLiveBot(bot)&&ReferenceEquals(bot.GetPlayer,player);
            if(probe!=null)
            {
                current=current&&IsActiveProbe(probe)&&IsProbeLifecycleCurrent(probe)&&
                    ReferenceEquals(probe.LifecycleMission,mission)&&ReferenceEquals(probe.LifecycleStep,step)&&
                    probe.MovementWriterGeneration==issuedGeneration;
                actor=actor&&ReferenceEquals(probe.Player,player);
            }
            var follower=BossPlayers.GetFollowerByProfileId(bot.ProfileId);
            var context=player.MovementContext;
            bool preempted=follower==null||
                (follower.TryPeekActiveCommand(out var command,out _,out _)&&command!=FollowerCommandType.None)||
                context.OverridenState!=null||context.OverridenControlsState!=null||context.IgnoreDeltaMovement||
                IsTeleportTransactionActive(bot)||IsLearningV350TerminalPhysicalRecoveryEligible(bot)||
                FollowerMedical.IsUsingMedical(bot)||FollowerMedical.IsHandsInteractionActiveForAuthority(player)||
                FollowerEnvironment.TryGetImmediateSurvivalThreat(bot,out _)||
                FollowerStateAuthorityCoordinator.HasCombatMovementClaim(bot,out _);
            return FollowerCapabilityRouteContinuationPolicy.Evaluate(new FollowerCapabilityRouteContinuationPolicy.Snapshot(
                origin,live,phase,current,actor,owner==FollowerMovementWriterAuthority.WriterDomain.Traversal,
                cursorCurrent,HasActiveTraversalProbe(bot),preempted));
        }
        private static void RecordCapabilityRouteContinuationRejection(BotOwner bot,FollowerRouteMission mission,
            FollowerRouteMissionStep step,CapabilityContinuationPhase phase,CapabilityContinuationFailure failure,string detail=null)
        {
            if(bot==null||!ShouldEmitDiagnostic("capabilityRouteContinuationRejected:"+phase+":"+failure,bot,.75f))return;
            WriteEvent("capabilityRouteContinuationRejected",bot,new{missionId=mission?.MissionId??0,
                stepIndex=mission?.CurrentStepIndex??-1,episodeId=step?.EpisodeId??0,edgeId=step?.EdgeId??0,
                phase=phase.ToString(),failure=failure.ToString(),detail=detail??string.Empty,
                legacyFallbackAllowed=false,nativeSuccessRecorded=false,bodyRelocated=false});
        }
        private static bool TryEnterCapabilityRouteProducerScope(BotOwner bot,FollowerRouteMission mission,
            FollowerRouteMissionStep step,FollowerTraversalRouteCursor cursor,
            FollowerMovementV2IntentGateway.MovementIntent intent,
            out FollowerMovementWriterAuthority.WriteScope scope,out string reason)
        {
            scope=default;reason=string.Empty;
            long issued=cursor?.LearningV375MovementGeneration??0;
            var failure=ObserveCapabilityRouteContinuation(bot,bot?.GetPlayer,mission,step,issued,null,
                cursor,CapabilityContinuationPhase.CanonicalRoute);
            if(failure!=CapabilityContinuationFailure.None)
            {
                reason=failure.ToString();RecordCapabilityRouteContinuationRejection(bot,mission,step,CapabilityContinuationPhase.CanonicalRoute,failure);return false;
            }
            if(!FollowerMovementWriterAuthority.TryEnterVerifiedRouteIntent(bot,intent,out scope,out reason))
            {RecordCapabilityRouteContinuationRejection(bot,mission,step,CapabilityContinuationPhase.CanonicalRoute,CapabilityContinuationFailure.ScopeRejected,reason);return false;}
            failure=ObserveCapabilityRouteContinuation(bot,bot?.GetPlayer,mission,step,issued,null,cursor,CapabilityContinuationPhase.CanonicalRoute);
            if(failure==CapabilityContinuationFailure.None)return true;
            scope.Dispose();scope=default;reason=failure.ToString();
            RecordCapabilityRouteContinuationRejection(bot,mission,step,CapabilityContinuationPhase.CanonicalRoute,failure);return false;
        }
        private static bool TryEnterCapabilityRoutePhysicalScope(BotOwner bot,Player player,
            FollowerRouteMission mission,FollowerRouteMissionStep step,long issuedGeneration,
            ProbeRuntime probe,FollowerTraversalRouteCursor cursor,CapabilityContinuationPhase phase,
            out FollowerMovementWriterAuthority.WriteScope scope,out CapabilityContinuationFailure failure)
        {
            scope=default;
            failure=ObserveCapabilityRouteContinuation(bot,player,mission,step,issuedGeneration,probe,cursor,phase);
            if(failure==CapabilityContinuationFailure.None)
            {
                if(FollowerMovementWriterAuthority.TryEnterBoundPhysicalMotion(bot,
                    FollowerMovementWriterAuthority.WriterDomain.Traversal,issuedGeneration,out scope,out var reason))
                {
                    failure=ObserveCapabilityRouteContinuation(bot,player,mission,step,issuedGeneration,probe,cursor,phase);
                    if(failure==CapabilityContinuationFailure.None)return true;
                    scope.Dispose();scope=default;
                }
                else failure=CapabilityContinuationFailure.ScopeRejected;
            }
            RecordCapabilityRouteContinuationRejection(bot,mission,step,phase,failure);return false;
        }

        private static FollowerTraversalRouteCursor GetOrCreateLearningV375RouteCursor(BotOwner bot)
        {
            string profileId = bot?.ProfileId ?? string.Empty;
            if (string.IsNullOrEmpty(profileId))
            {
                return null;
            }
            if (!FollowerTraversalRouteCursors.TryGetValue(profileId, out FollowerTraversalRouteCursor cursor) || cursor == null)
            {
                cursor = new FollowerTraversalRouteCursor();
                FollowerTraversalRouteCursors[profileId] = cursor;
            }
            return cursor;
        }

        private static bool TryGetLearningV375MissionForChain(
            BotOwner bot, int chainId, out FollowerRouteMission mission)
        {
            mission = null;
            string profileId = bot?.ProfileId ?? string.Empty;
            if (string.IsNullOrEmpty(profileId) || chainId <= 0 ||
                !FollowerRouteMissions.TryGetValue(profileId, out mission) || mission == null ||
                mission.Steps == null || mission.Steps.Count == 0 || mission.ExpiresAt < Time.time)
            {
                mission = null;
                return false;
            }
            return mission.Steps.Any(step => step != null && step.ChainId == chainId);
        }

        private static bool TryGetLearningV376MissionById(
            BotOwner bot, int missionId, out FollowerRouteMission mission)
        {
            mission = null;
            string profileId = bot?.ProfileId ?? string.Empty;
            if ((string.IsNullOrEmpty(profileId)) ||
                missionId <= 0 || !FollowerRouteMissions.TryGetValue(profileId, out mission) || mission == null ||
                mission.MissionId != missionId || mission.ExpiresAt < Time.time || mission.Steps == null ||
                mission.CurrentStepIndex < 0 || mission.CurrentStepIndex >= mission.Steps.Count)
            {
                mission = null;
                return false;
            }
            return true;
        }

        private static bool TryResolveLearningV376MissionForProbe(
            ProbeRuntime probe, out FollowerRouteMission mission, out int stepIndex)
        {
            mission = null;
            stepIndex = -1;
            if ((probe?.Bot == null))
            {
                return false;
            }

            if (probe.LifecycleMission!=null)
            {
                mission=probe.LifecycleMission;stepIndex=probe.Lifecycle?.Origin.Step ?? -1;
                return FollowerRouteMissions.TryGetValue(probe.Bot.ProfileId,out var live) && ReferenceEquals(live,mission) &&
                    !mission.Lifecycle.Retired && mission.LifecycleRaid==raidGeneration &&
                    stepIndex>=0 && stepIndex<mission.Steps.Count && ReferenceEquals(mission.Steps[stepIndex],probe.LifecycleStep) &&
                    probe.Lifecycle.Origin.Geometry==probe.LifecycleStep.Lifecycle.GeometryVersion;
            }
            int requestedMissionId = probe.FrozenTraversalCommitment?.RouteMissionId ?? 0;
            if (requestedMissionId > 0 && TryGetLearningV376MissionById(probe.Bot, requestedMissionId, out mission))
            {
                stepIndex = mission.Steps.FindIndex(candidate => candidate != null &&
                    ((probe.TraversalEpisodeId > 0 && candidate.EpisodeId == probe.TraversalEpisodeId) ||
                     (candidate.EdgeId == probe.LearnedEdgeId &&
                      (probe.TraversalEpisodeId <= 0 || candidate.EpisodeId == probe.TraversalEpisodeId))));
                if (stepIndex >= 0)
                {
                    return true;
                }
            }

            if (TryGetActiveFollowerRouteMission(probe.Bot, out mission) && mission != null)
            {
                stepIndex = mission.Steps.FindIndex(candidate => candidate != null &&
                    ((probe.TraversalEpisodeId > 0 && candidate.EpisodeId == probe.TraversalEpisodeId) ||
                     (candidate.EdgeId == probe.LearnedEdgeId && candidate.ChainId == probe.TraversalChainId)));
                if (stepIndex >= 0)
                {
                    return true;
                }
            }
            mission = null;
            stepIndex = -1;
            return false;
        }

        private static bool IsLearningV376CursorCompatibleWithProbe(
            FollowerTraversalRouteCursor cursor, ProbeRuntime probe, out FollowerRouteMission mission)
        {
            mission = null;
            if (cursor == null || probe?.Bot == null || !cursor.LearningV375TransactionActive)
            {
                return false;
            }
            if ((cursor.LearningV375RouteMissionId > 0) &&
                TryResolveLearningV376MissionForProbe(probe, out FollowerRouteMission probeMission, out _) &&
                probeMission != null && probeMission.MissionId == cursor.LearningV375RouteMissionId &&
                ((cursor.LearningV375TransactionChainId <= 0) ||
                 cursor.LearningV375TransactionChainId == probe.TraversalChainId))
            {
                mission = probeMission;
                return true;
            }
            return cursor.LearningV375RouteMissionId <= 0 &&
                   cursor.LearningV375TransactionChainId > 0 &&
                   cursor.LearningV375TransactionChainId == probe.TraversalChainId;
        }

        private static bool TryResolveLearningV376CursorMission(
            BotOwner bot, FollowerTraversalRouteCursor cursor, out FollowerRouteMission mission)
        {
            mission = null;
            if (cursor == null || bot == null)
            {
                return false;
            }
            if (cursor.LearningV375RouteMissionId > 0 &&
                TryGetLearningV376MissionById(bot, cursor.LearningV375RouteMissionId, out mission))
            {
                return true;
            }
            if (cursor.LearningV375TransactionChainId > 0 &&
                TryGetLearningV375MissionForChain(bot, cursor.LearningV375TransactionChainId, out mission))
            {
                return true;
            }
            mission = null;
            return false;
        }

        private static long EnsureLearningV376PreProbeRouteTransaction(
            BotOwner bot, FollowerRouteMission mission, FollowerRouteMissionStep step, string source)
        {
            if ((bot == null) || mission == null || step == null ||
                mission.CurrentStepIndex < 0 || mission.CurrentStepIndex >= mission.Steps.Count ||
                IsTeleportTransactionActive(bot) || HasActiveTraversalProbe(bot))
            {
                return 0;
            }

            FollowerTraversalRouteCursor cursor = GetOrCreateLearningV375RouteCursor(bot);
            long currentGeneration = FollowerMovementWriterAuthority.GetCurrentGeneration(
                bot, out FollowerMovementWriterAuthority.WriterDomain owner);
            if (cursor != null && cursor.LearningV375TransactionActive &&
                cursor.LearningV375RouteMissionId == mission.MissionId &&
                ((cursor.LearningV375TransactionChainId <= 0) ||
                 cursor.LearningV375TransactionChainId == step.ChainId) &&
                cursor.LearningV375MovementGeneration > 0 &&
                owner == FollowerMovementWriterAuthority.WriterDomain.Traversal &&
                currentGeneration == cursor.LearningV375MovementGeneration)
            {
                long continued = FollowerMovementWriterAuthority.ContinueTraversal(
                    bot, cursor.LearningV375MovementGeneration,
                    "learningV376PreProbeContinue:mission=" + mission.MissionId.ToString(CultureInfo.InvariantCulture));
                if (continued > 0)
                {
                    cursor.LearningV376CurrentProvenanceChainId = step.ChainId;
                    cursor.LearningV375TransactionChainId = step.ChainId;
                    cursor.LearningV376PreProbeProducerActive = true;
                    return continued;
                }
            }

            // Never steal a real exceptional physical epoch. Stale terminal recovery is cleared by
            // the explicit V3.62/V3.76 recovery-epoch contract before this helper is reached.
            if (owner == FollowerMovementWriterAuthority.WriterDomain.Teleport ||
                owner == FollowerMovementWriterAuthority.WriterDomain.Medical ||
                owner == FollowerMovementWriterAuthority.WriterDomain.Combat ||
                owner == FollowerMovementWriterAuthority.WriterDomain.Recovery)
            {
                return 0;
            }

            long generation = FollowerMovementWriterAuthority.BeginTraversal(
                bot,
                "learningV376PreProbeStart:mission=" + mission.MissionId.ToString(CultureInfo.InvariantCulture) +
                ":edge=" + step.EdgeId.ToString(CultureInfo.InvariantCulture) +
                ":chain=" + step.ChainId.ToString(CultureInfo.InvariantCulture));
            if (generation <= 0 || cursor == null)
            {
                return generation;
            }

            cursor.LearningV375TransactionActive = true;
            cursor.LearningV375RouteMissionId = mission.MissionId;
            cursor.LearningV375TransactionChainId = step.ChainId;
            cursor.LearningV376CurrentProvenanceChainId = step.ChainId;
            cursor.LearningV376PreProbeProducerActive = true;
            cursor.LearningV375MovementGeneration = generation;
            cursor.LearningV375PhysicalFrontier = bot.Position;
            cursor.LearningV375LastBodyPosition = bot.Position;
            cursor.LearningV375LastBodySampleAt = Time.time;
            cursor.LearningV375PhysicalSpeed = 0f;
            cursor.LearningV375ProducerKind = "PreProbePhysicalRoute";
            cursor.LearningV375ProducerGapStartedAt = -1f;
            if (TryProjectLearningV375RouteArc(mission, bot.Position, out float routeArc, out Vector3 tangent, out _, out _))
            {
                cursor.LearningV375RouteArcLength = routeArc;
                cursor.LearningV375BestRouteArcLength = routeArc;
                cursor.LearningV375RouteTangent = tangent;
            }
            WriteEvent("learningV376PreProbeRouteTransactionStarted", bot, new
            {
                missionId = mission.MissionId,
                stepIndex = mission.CurrentStepIndex,
                episodeId = step.EpisodeId,
                edgeId = step.EdgeId,
                kind = step.Kind,
                provenanceChainId = step.ChainId,
                generation,
                position = Vec(bot.Position),
                action = "routeMissionPhysicalProducerExistsBeforeFirstTraversalProbe"
            });
            return generation;
        }

        private static bool ApplyLearningV405CanonicalNonActuatorRouteMovementIntent(
            BotOwner bot,
            FollowerRouteMission mission,
            FollowerTraversalRouteCursor cursor,
            FollowerRouteMissionStep step,
            LearningV354TraversalCapabilityProfile profile,
            bool locomotionEvidenceAvailable,
            bool preferSprint,
            string dominantState,
            float observedMeanSpeed,
            float measuredBodySampleDistance,
            Vector3 measuredBodyDelta,
            Vector3 routeTangent,
            Vector3 target,
            bool targetCapped,
            float originalTargetArc,
            float stepExitArc,
            float cappedTargetArc,
            out bool sprintRestored,
            out bool measuredForwardProgress,
            out float forwardDot,
            out float appliedSpeed)
        {
            sprintRestored = false;
            measuredForwardProgress = false;
            forwardDot = 0f;
            appliedSpeed = 0f;
            if ((bot?.Mover == null) ||
                mission == null || cursor == null || step == null || profile == null ||
                profile.RequiresActuator || !profile.PhysicalPassageCanProveCompletion ||
                !cursor.LearningV375TransactionActive || cursor.LearningV375RouteMissionId != mission.MissionId ||
                mission.CurrentStepIndex < 0 || mission.CurrentStepIndex >= mission.Steps.Count ||
                mission.Steps[mission.CurrentStepIndex] != step)
            {
                return false;
            }

            Vector3 canonicalTangent = routeTangent;
            canonicalTangent.y = 0f;
            if (canonicalTangent.sqrMagnitude > 0.25f && measuredBodyDelta.sqrMagnitude > 0.0001f)
            {
                forwardDot = Vector3.Dot(measuredBodyDelta.normalized, canonicalTangent.normalized);
                measuredForwardProgress = measuredBodySampleDistance > 0.01f &&
                    cursor.LearningV375PhysicalSpeed >= RollingTraversalMinSpeed &&
                    forwardDot >= PostTraversalNormalFollowAckTargetForwardDot;
            }

            Player player = bot.GetPlayer ?? bot.AIData?.Player;
            bool sprintBefore = bot.Mover.Sprinting;
            bool mayRestoreSprint = FollowerCapabilityHelperRules.ApplyLearningV405CanonicalNonActuatorRouteMovementIntent_mayRestoreSprint_1AACD652_1(locomotionEvidenceAvailable,preferSprint,sprintBefore,measuredForwardProgress) && !bot.Mover.NoSprint && bot.Memory?.HaveEnemy != true &&
                player?.MovementContext?.CanSprint == true && player.MovementContext.CanWalk == true;
            if (mayRestoreSprint)
            {
                bot.Mover.BotMoverSprint("R1-CE6EF48A2360",@bot,true,false);
                sprintRestored = bot.Mover.Sprinting;
            }

            bool sprintAfter = bot.Mover.Sprinting;
            appliedSpeed = sprintAfter
                ? ProbeMomentumFullMoveSpeed
                : ((ProbeMomentumNormalMoveSpeed));
            if (canonicalTangent.sqrMagnitude > 0.25f)
            {
                try { bot.Steering?.BotSteeringLookToDirection("R1-66D7BE56F6DB",@bot,canonicalTangent.normalized); } catch { }
            }

            if (sprintRestored)
            {
                WriteEvent("learningV405CanonicalRouteMovementIntentSprintRestored", bot, new
                {
                    missionId = mission.MissionId,
                    stepIndex = mission.CurrentStepIndex,
                    edgeId = step.EdgeId,
                    kind = step.Kind,
                    chainId = step.ChainId,
                    generation = cursor.LearningV375MovementGeneration,
                    measuredPhysicalSpeed = cursor.LearningV375PhysicalSpeed,
                    measuredBodySampleDistance,
                    forwardDot,
                    dominantState = dominantState ?? string.Empty,
                    observedMeanSpeed,
                    target = Vec(target),
                    targetCapped,
                    originalTargetArc,
                    stepExitArc,
                    cappedTargetArc,
                    traversalSuccessRecorded = false,
                    persistentLearningChanged = false,
                    bodyRelocated = false,
                    action = "measuredForwardCanonicalBodyProgressMayRestoreRouteEvidenceSprintWithoutRunInPlace"
                });
            }

            if (ShouldEmitDiagnostic(
                    "learningV405CanonicalRouteMovementIntentApplied:" +
                        mission.MissionId.ToString(CultureInfo.InvariantCulture) + ":" +
                        mission.CurrentStepIndex.ToString(CultureInfo.InvariantCulture), bot, 0.35f))
            {
                WriteEvent("learningV405CanonicalRouteMovementIntentApplied", bot, new
                {
                    missionId = mission.MissionId,
                    stepIndex = mission.CurrentStepIndex,
                    edgeId = step.EdgeId,
                    kind = step.Kind,
                    chainId = step.ChainId,
                    generation = cursor.LearningV375MovementGeneration,
                    locomotionEvidenceAvailable,
                    dominantState = dominantState ?? string.Empty,
                    requestedSprint = locomotionEvidenceAvailable && preferSprint,
                    sprintBefore,
                    sprintAfter,
                    sprintRestored,
                    measuredForwardProgress,
                    measuredPhysicalSpeed = cursor.LearningV375PhysicalSpeed,
                    measuredBodySampleDistance,
                    forwardDot,
                    observedMeanSpeed,
                    targetMoveSpeed = appliedSpeed,
                    target = Vec(target),
                    routeTangent = Vec(canonicalTangent),
                    targetCapped,
                    originalTargetArc,
                    stepExitArc,
                    cappedTargetArc,
                    traversalSuccessRecorded = false,
                    persistentLearningChanged = false,
                    bodyRelocated = false,
                    action = "singleCanonicalNonActuatorExecutorOwnsTargetFacingAndLocomotionState"
                });
            }
            return true;
        }

        private static bool ExecuteLearningV406AtomicCanonicalNonActuatorRouteMovementIntent(
            BotOwner bot,
            FollowerRouteMission mission,
            FollowerTraversalRouteCursor cursor,
            FollowerRouteMissionStep step,
            LearningV354TraversalCapabilityProfile profile,
            Vector3 position,
            Vector3 target,
            Vector3 routeTangent,
            bool locomotionEvidenceAvailable,
            bool preferSprint,
            string dominantState,
            float observedMeanSpeed,
            float canonicalTargetMoveSpeed,
            bool targetCapped,
            float originalTargetArc,
            float stepExitArc,
            float cappedTargetArc,
            out string detail)
        {
            detail = "notEligible";
            if ((bot?.Mover == null) ||
                mission == null || cursor == null || step == null || profile == null ||
                profile.RequiresActuator || !profile.PhysicalPassageCanProveCompletion ||
                !cursor.LearningV375TransactionActive || cursor.LearningV375RouteMissionId != mission.MissionId ||
                cursor.LearningV375MovementGeneration <= 0 ||
                mission.CurrentStepIndex < 0 || mission.CurrentStepIndex >= mission.Steps.Count ||
                mission.Steps[mission.CurrentStepIndex] != step || !IsFinite(position) || !IsFinite(target))
            {
                return false;
            }

            long generation = FollowerMovementWriterAuthority.GetCurrentGeneration(
                bot, out FollowerMovementWriterAuthority.WriterDomain owner);
            if (owner != FollowerMovementWriterAuthority.WriterDomain.Traversal ||
                generation != cursor.LearningV375MovementGeneration)
            {
                detail = "movementOwnerMismatch:" + owner + ":generation=" +
                         generation.ToString(CultureInfo.InvariantCulture);
                return false;
            }

            bool sprintStateBeforeNativeMoverRetirement = bot.Mover.Sprinting;
            bool newExecutorEpoch = cursor.LearningV406AtomicMissionId != mission.MissionId ||
                                    cursor.LearningV406AtomicStepIndex != mission.CurrentStepIndex ||
                                    cursor.LearningV406AtomicGeneration != generation;
            string moverRetireDetail = "alreadyCanonical";
            bool moverRetired = false;
            if (newExecutorEpoch)
            {
                moverRetired = (TryRetireNativeMoverPathPreserveLocomotion(
                        bot, "learningV409NativeLocomotionExecutorEntry", out moverRetireDetail));
                cursor.LearningV406AtomicMissionId = mission.MissionId;
                cursor.LearningV406AtomicStepIndex = mission.CurrentStepIndex;
                cursor.LearningV406AtomicGeneration = generation;
                cursor.LearningV406NativeMoverRetired = moverRetired;
                WriteEvent("learningV406NativeMoverTranslationEpochRetired", bot, new
                {
                    missionId = mission.MissionId,
                    stepIndex = mission.CurrentStepIndex,
                    edgeId = step.EdgeId,
                    kind = step.Kind,
                    chainId = step.ChainId,
                    generation,
                    moverRetired,
                    moverRetireDetail,
                    traversalSuccessRecorded = false,
                    persistentLearningChanged = false,
                    bodyRelocated = false,
                    action = ("retireOnlyStalePathTopologyKeepNativeLocomotionStateAliveForExactStep")
                });
            }

            Player player = bot.GetPlayer ?? bot.AIData?.Player;
            if (player?.MovementContext == null)
            {
                detail = "movementContextUnavailable";
                return false;
            }

            Vector3 canonicalTangent = routeTangent;
            canonicalTangent.y = 0f;
            if (canonicalTangent.sqrMagnitude <= 0.0001f)
            {
                detail = "routeTangentUnavailable";
                return false;
            }
            canonicalTangent.Normalize();

            Vector3 delta = target - position;
            delta.y = 0f;
            float distance = delta.magnitude;
            Vector3 direction = distance > 0.001f ? delta / distance : canonicalTangent;
            float targetForwardDot = Vector3.Dot(direction, canonicalTangent);
            bool learningV423LocalLookAheadRebound = false;
            if (targetForwardDot < PostTraversalNormalFollowAckTargetForwardDot)
            {
                if ((TryProjectLearningV375RouteArc(
                        mission, position,
                        out float learningV423BodyArc,
                        out _, out _, out _)) &&
                    TryGetLearningV375RouteLookAheadTarget(
                        mission, learningV423BodyArc,
                        out Vector3 learningV423LocalTarget,
                        out Vector3 learningV423LocalTangent,
                        out float learningV423LocalTargetArc))
                {
                    Vector3 learningV423LocalDelta = learningV423LocalTarget - position;
                    learningV423LocalDelta.y = 0f;
                    float learningV423LocalDistance = learningV423LocalDelta.magnitude;
                    if (learningV423LocalDistance > 0.001f)
                    {
                        float learningV423MaxLocalArc = FollowerCapabilityHelperRules.ExecuteLearningV406AtomicCanonicalNonActuatorRouteMovementIntent_learningV423MaxLocalArc_97839871_1(stepExitArc,learningV423BodyArc,stepExitArc,PostTraversalRollbackSideMargin)
                            ? stepExitArc + PostTraversalRollbackSideMargin
                            : learningV423LocalTargetArc;
                        if (learningV423LocalTargetArc > learningV423MaxLocalArc + 0.001f &&
                            TryGetLearningV405RoutePointAtArc(
                                mission, learningV423MaxLocalArc,
                                out Vector3 learningV423CappedLocalTarget,
                                out Vector3 learningV423CappedLocalTangent,
                                out float learningV423CappedLocalArc))
                        {
                            learningV423LocalTarget = learningV423CappedLocalTarget;
                            learningV423LocalTangent = learningV423CappedLocalTangent;
                            learningV423LocalTargetArc = learningV423CappedLocalArc;
                            learningV423LocalDelta = learningV423LocalTarget - position;
                            learningV423LocalDelta.y = 0f;
                            learningV423LocalDistance = learningV423LocalDelta.magnitude;
                        }

                        Vector3 originalTarget = target;
                        float originalTargetForwardDot = targetForwardDot;
                        target = learningV423LocalTarget;
                        direction = learningV423LocalDistance > 0.001f
                            ? learningV423LocalDelta / learningV423LocalDistance
                            : canonicalTangent;
                        distance = learningV423LocalDistance;
                        targetForwardDot = Vector3.Dot(direction, canonicalTangent);
                        learningV423LocalLookAheadRebound = learningV423LocalDistance > 0.001f;
                        if (learningV423LocalTangent.sqrMagnitude > 0.25f)
                        {
                            canonicalTangent = learningV423LocalTangent.normalized;
                        }

                        if (learningV423LocalLookAheadRebound && ShouldEmitDiagnostic(
                                "learningV423CanonicalRouteLocalLookAheadRebound:" +
                                    mission.MissionId.ToString(CultureInfo.InvariantCulture) + ":" +
                                    mission.CurrentStepIndex.ToString(CultureInfo.InvariantCulture),
                                bot, 0.20f))
                        {
                            WriteEvent("learningV423CanonicalRouteLocalLookAheadRebound", bot, new
                            {
                                missionId = mission.MissionId,
                                stepIndex = mission.CurrentStepIndex,
                                edgeId = step.EdgeId,
                                episodeId = step.EpisodeId,
                                kind = step.Kind,
                                bodyArc = learningV423BodyArc,
                                localTargetArc = learningV423LocalTargetArc,
                                originalTarget = Vec(originalTarget),
                                localTarget = Vec(learningV423LocalTarget),
                                originalTargetForwardDot,
                                localTargetForwardDot = targetForwardDot,
                                localTargetDistance = learningV423LocalDistance,
                                traversalSuccessRecorded = false,
                                persistentLearningChanged = false,
                                bodyRelocated = false,
                                action = stepExitArc >= 0f &&
                                         learningV423BodyArc >= stepExitArc + PostTraversalRollbackSideMargin
                                    ? "bodyAlreadyPastStepProofUsesForwardLocalRouteLookAheadInsteadOfZeroMotionHold"
                                    : "chronologicalLocalRouteLookAheadOutranksFarChordDirectionNoZeroMotionBrake"
                            });
                        }
                    }
                }

                if (!learningV423LocalLookAheadRebound)
                {
                    direction = canonicalTangent;
                    distance = 0f;
                }
            }

            float driveSpeed = GetLearningV381PhysicalCorridorDriveSpeed(step);
            float dt = Mathf.Max(0.001f, Time.deltaTime);
            Vector3 motion = distance > 0.001f
                ? direction * Mathf.Min(distance, Mathf.Max(0f, driveSpeed) * dt)
                : Vector3.zero;
            bool sprint = locomotionEvidenceAvailable && preferSprint &&
                (bot.Mover.Sprinting || sprintStateBeforeNativeMoverRetirement);
            float moveSpeed = sprint ? ProbeMomentumFullMoveSpeed : ProbeMomentumNormalMoveSpeed;
            if (canonicalTargetMoveSpeed > 0f && !sprint)
            {
                moveSpeed = ProbeMomentumNormalMoveSpeed;
            }

            if (!TryEnterCapabilityRoutePhysicalScope(bot,player,mission,step,generation,null,cursor,
                    CapabilityContinuationPhase.CanonicalRoute,out var physicalScope,out var continuationFailure))
            {
                detail="routeContinuationRejected:"+continuationFailure;
                return false;
            }
            using (physicalScope)
            {
                ApplyCollisionActivePhysicalRouteMotion(
                    bot, player, direction, motion, dt, 1f, sprint, moveSpeed,deliveryTarget:target,deliveryPurpose:CapabilityMotionPurpose.TypedRoute);
            }
            cursor.LearningV375ProducerKind = ("V409NativeLocomotionIntent");
            cursor.LearningV375ProducerTarget = target;
            cursor.LearningV375RouteTangent = canonicalTangent;

            if (ShouldEmitDiagnostic(
                    "learningV406AtomicCanonicalPhysicalExecutorApplied:" +
                        mission.MissionId.ToString(CultureInfo.InvariantCulture) + ":" +
                        mission.CurrentStepIndex.ToString(CultureInfo.InvariantCulture), bot, 0.20f))
            {
                WriteEvent("learningV406AtomicCanonicalPhysicalExecutorApplied", bot, new
                {
                    missionId = mission.MissionId,
                    stepIndex = mission.CurrentStepIndex,
                    edgeId = step.EdgeId,
                    kind = step.Kind,
                    chainId = step.ChainId,
                    generation,
                    position = Vec(position),
                    target = Vec(target),
                    canonicalTangent = Vec(canonicalTangent),
                    targetForwardDot,
                    driveSpeed,
                    targetMoveSpeed = moveSpeed,
                    sprint,
                    locomotionEvidenceAvailable,
                    requestedSprint = locomotionEvidenceAvailable && preferSprint,
                    dominantState = dominantState ?? string.Empty,
                    observedMeanSpeed,
                    targetCapped,
                    originalTargetArc,
                    stepExitArc,
                    cappedTargetArc,
                    moverRetiredThisEpoch = newExecutorEpoch && moverRetired,
                    moverRetireDetail,
                    traversalSuccessRecorded = false,
                    persistentLearningChanged = false,
                    bodyRelocatedByTeleport = false,
                    action = ((distance > 0.001f
                            ? "canonicalRoutePublishesNativePlayerMoveIntentPhysicalProgressMeasuredNextBodySample"
                            : "canonicalRouteHoldsWithoutBackwardIntent"))
                });
            }

            detail = (("nativeInputPublished")) +
                     ":distance=" + distance.ToString("0.000", CultureInfo.InvariantCulture) +
                     ":driveSpeed=" + driveSpeed.ToString("0.000", CultureInfo.InvariantCulture) +
                     ":sprint=" + sprint;
            return true;
        }

        private static bool TryGetLearningV395RouteLocomotionEvidence(
            FollowerRouteMission mission,
            float bodyArc,
            float targetArc,
            out bool preferSprint,
            out string dominantState,
            out int sprintSegments,
            out int runSegments,
            out float sprintArc,
            out float runArc,
            out float observedMeanSpeed)
        {
            preferSprint = false;
            dominantState = string.Empty;
            sprintSegments = 0;
            runSegments = 0;
            sprintArc = 0f;
            runArc = 0f;
            observedMeanSpeed = 0f;
            if ((mission?.RouteBreadcrumbs == null) || mission.RouteBreadcrumbs.Count < 2)
            {
                return false;
            }

            List<PlayerBreadcrumbNode> nodes = mission.RouteBreadcrumbs
                .Where(node => node != null && IsFinite(node.Position))
                .OrderBy(node => node.Sequence)
                .ToList();
            if (nodes.Count < 2)
            {
                return false;
            }

            float windowStart = Mathf.Max(0f, bodyArc);
            float windowEnd = Mathf.Max(windowStart, targetArc);
            float cumulative = 0f;
            float weightedSpeedDistance = 0f;
            float weightedSpeedTime = 0f;
            for (int index = 0; index < nodes.Count - 1; index++)
            {
                PlayerBreadcrumbNode aNode = nodes[index];
                PlayerBreadcrumbNode bNode = nodes[index + 1];
                Vector3 delta = bNode.Position - aNode.Position;
                delta.y = 0f;
                float length = delta.magnitude;
                if (length <= 0.001f)
                {
                    continue;
                }

                float segmentStart = cumulative;
                float segmentEnd = cumulative + length;
                cumulative = segmentEnd;
                if (segmentEnd < windowStart || segmentStart > windowEnd)
                {
                    continue;
                }

                float overlap = Mathf.Max(0f, Mathf.Min(segmentEnd, windowEnd) - Mathf.Max(segmentStart, windowStart));
                if (overlap <= 0.001f)
                {
                    continue;
                }

                string state = !string.IsNullOrEmpty(bNode.State) ? bNode.State : aNode.State;
                if (string.Equals(state, "Sprint", StringComparison.OrdinalIgnoreCase))
                {
                    sprintSegments++;
                    sprintArc += overlap;
                }
                else if (string.Equals(state, "Run", StringComparison.OrdinalIgnoreCase))
                {
                    runSegments++;
                    runArc += overlap;
                }

                float dt = bNode.CreatedAt - aNode.CreatedAt;
                if (dt > 0.001f && float.IsNaN(dt) == false && float.IsInfinity(dt) == false)
                {
                    float overlapRatio = Mathf.Clamp01(overlap / length);
                    weightedSpeedDistance += length * overlapRatio;
                    weightedSpeedTime += dt * overlapRatio;
                }
            }

            if (sprintArc <= 0.001f && runArc <= 0.001f)
            {
                return false;
            }

            preferSprint = sprintArc > runArc;
            dominantState = preferSprint ? "Sprint" : "Run";
            if (weightedSpeedTime > 0.001f)
            {
                observedMeanSpeed = weightedSpeedDistance / weightedSpeedTime;
            }
            return true;
        }

        private static bool PublishLearningV396RouteLocomotionIntent(
            BotOwner bot,
            FollowerRouteMission mission,
            FollowerRouteMissionStep step,
            bool preferSprint,
            out string result)
        {
            result = "disabled";
            if ((bot == null) || mission == null ||
                step == null || step.ChainId <= 0 || string.IsNullOrEmpty(bot.ProfileId))
            {
                return false;
            }

            string profileId = bot.ProfileId;
            if (TraversalChainLocomotionIntents.TryGetValue(
                    profileId, out TraversalChainLocomotionIntent existing) &&
                existing != null && Time.time <= existing.ExpiresAt &&
                existing.ChainId == step.ChainId &&
                !string.Equals(existing.Source, "learningV396RouteLocomotionEvidence", StringComparison.Ordinal))
            {
                // A real traversal handoff already owns stronger locomotion intent for this chain.
                // Route presentation evidence is not allowed to overwrite it.
                result = "strongerChainIntentRetained";
                return false;
            }

            TraversalChainLocomotionIntents[profileId] = new TraversalChainLocomotionIntent
            {
                ChainId = step.ChainId,
                Sprinting = preferSprint,
                Running = true,
                ExpiresAt = Time.time + 2.0f,
                Source = "learningV396RouteLocomotionEvidence"
            };
            result = preferSprint ? "sprintIntentPublished" : "runIntentPublished";
            if (ShouldEmitDiagnostic(
                    "learningV396RouteLocomotionIntentPublished:" +
                        mission.MissionId.ToString(CultureInfo.InvariantCulture) + ":" +
                        step.EdgeId.ToString(CultureInfo.InvariantCulture),
                    bot, 0.35f))
            {
                WriteEvent("learningV396RouteLocomotionIntentPublished", bot, new
                {
                    missionId = mission.MissionId,
                    stepIndex = mission.CurrentStepIndex,
                    edgeId = step.EdgeId,
                    kind = step.Kind,
                    chainId = step.ChainId,
                    preferSprint,
                    expiresInSeconds = 2.0f,
                    traversalSuccessRecorded = false,
                    persistentLearningChanged = false,
                    moverSprintStateChanged = false,
                    action = "publishRouteLocomotionIntentForExactChainConsumerWithoutGenericMoverStateWrite"
                });
            }
            return true;
        }

        private static bool TryArmOrRefreshLearningV400MeasuredRollingPassageSprintLease(
            BotOwner bot,
            FollowerRouteMission mission,
            FollowerTraversalRouteCursor cursor,
            FollowerRouteMissionStep step,
            bool preferSprint,
            float measuredBodySampleDistance,
            Vector3 measuredBodyDelta,
            string source)
        {
            if ((bot?.Mover == null) ||
                mission == null || cursor == null || step == null || !preferSprint ||
                mission.CurrentStepIndex < 0 || mission.CurrentStepIndex >= mission.Steps.Count ||
                mission.Steps[mission.CurrentStepIndex] != step ||
                cursor.LearningV375RouteMissionId != mission.MissionId ||
                !cursor.LearningV375TransactionActive)
            {
                return false;
            }

            LearningV354TraversalCapabilityProfile profile = GetLearningV354ActionProfile(
                GetLearningV354SemanticKind(step.EdgeId, step.Kind));
            if (profile == null || profile.RequiresActuator || !profile.PhysicalPassageCanProveCompletion)
            {
                return false;
            }

            Player player = bot.GetPlayer ?? bot.AIData?.Player;
            bool moverSprinting = bot.Mover.Sprinting == true;
            bool playerSprinting = player?.MovementContext?.IsSprintEnabled == true;
            if (!moverSprinting || !playerSprinting || bot.Mover.NoSprint ||
                player?.MovementContext?.CanSprint != true || player.MovementContext.CanWalk != true ||
                bot.Memory?.HaveEnemy == true || measuredBodySampleDistance <= 0.01f ||
                cursor.LearningV375PhysicalSpeed < RollingTraversalMinSpeed ||
                cursor.LearningV375RouteTangent.sqrMagnitude <= 0.25f || measuredBodyDelta.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            float forwardDot = Vector3.Dot(
                measuredBodyDelta.normalized,
                cursor.LearningV375RouteTangent.normalized);
            if (forwardDot < PostTraversalNormalFollowAckTargetForwardDot)
            {
                return false;
            }

            cursor.LearningV400RollingSprintMissionId = mission.MissionId;
            cursor.LearningV400RollingSprintStepIndex = mission.CurrentStepIndex;
            cursor.LearningV400RollingSprintGeneration = cursor.LearningV375MovementGeneration;
            cursor.LearningV400RollingSprintLeaseUntil = Time.time + PostTraversalNormalFollowAckStableSeconds;

            if (ShouldEmitDiagnostic(
                    "learningV400MeasuredRollingSprintLease:" +
                    mission.MissionId.ToString(CultureInfo.InvariantCulture) + ":" +
                    mission.CurrentStepIndex.ToString(CultureInfo.InvariantCulture), bot, 0.35f))
            {
                WriteEvent("learningV400MeasuredRollingSprintLease", bot, new
                {
                    missionId = mission.MissionId,
                    stepIndex = mission.CurrentStepIndex,
                    edgeId = step.EdgeId,
                    kind = step.Kind,
                    chainId = step.ChainId,
                    generation = cursor.LearningV375MovementGeneration,
                    measuredPhysicalSpeed = cursor.LearningV375PhysicalSpeed,
                    measuredBodySampleDistance,
                    forwardDot,
                    forwardDotMeasuredThisFrame = true,
                    leaseSeconds = PostTraversalNormalFollowAckStableSeconds,
                    source = source ?? string.Empty,
                    moverSprinting,
                    playerSprinting,
                    sprintEnabledByV400 = false,
                    traversalSuccessRecorded = false,
                    persistentLearningChanged = false,
                    action = "protectOnlyAlreadyRealSprintWhileMeasuredCanonicalBodyProgressContinues"
                });
            }
            return true;
        }

        private static void TryPrimeLearningV400SuccessorRollingSprintLease(
            BotOwner bot, FollowerRouteMission mission, FollowerTraversalRouteCursor cursor, string source)
        {
            if ((bot?.Mover == null) ||
                mission == null || cursor == null || mission.CurrentStepIndex < 0 ||
                mission.CurrentStepIndex >= mission.Steps.Count || !bot.Mover.Sprinting ||
                !cursor.LearningV375TransactionActive || cursor.LearningV375RouteMissionId != mission.MissionId)
            {
                return;
            }

            Player player = bot.GetPlayer ?? bot.AIData?.Player;
            if (player?.MovementContext?.IsSprintEnabled != true ||
                !TryProjectLearningV375RouteArc(
                    mission, bot.Position, out float bodyArc, out _, out _, out _))
            {
                return;
            }

            FollowerRouteMissionStep step = mission.Steps[mission.CurrentStepIndex];
            LearningV354TraversalCapabilityProfile profile = step != null
                ? GetLearningV354ActionProfile(GetLearningV354SemanticKind(step.EdgeId, step.Kind))
                : null;
            if (step == null || profile == null || profile.RequiresActuator ||
                !profile.PhysicalPassageCanProveCompletion ||
                !TryGetLearningV395RouteLocomotionEvidence(
                    mission, bodyArc, bodyArc + RouteMissionBreadcrumbReachDistance,
                    out bool preferSprint, out _, out _, out _, out _, out _, out _) || !preferSprint)
            {
                return;
            }

            // At a just-completed passage boundary the producer may already have updated
            // LastBodyPosition to the current body, so a same-frame delta can legitimately be zero.
            // Reuse only the RECENT measured speed from the existing route cursor; never synthesize
            // momentum from breadcrumb speed. The lease still does not enable Sprint from rest.
            bool recentMeasuredProgress = cursor.LearningV375LastBodySampleAt >= 0f &&
                Time.time - cursor.LearningV375LastBodySampleAt <= PostTraversalNormalFollowAckStableSeconds &&
                cursor.LearningV375PhysicalSpeed >= RollingTraversalMinSpeed &&
                cursor.LearningV375RouteTangent.sqrMagnitude > 0.25f;
            if (!recentMeasuredProgress)
            {
                return;
            }

            cursor.LearningV400RollingSprintMissionId = mission.MissionId;
            cursor.LearningV400RollingSprintStepIndex = mission.CurrentStepIndex;
            cursor.LearningV400RollingSprintGeneration = cursor.LearningV375MovementGeneration;
            cursor.LearningV400RollingSprintLeaseUntil = Time.time + PostTraversalNormalFollowAckStableSeconds;
            WriteEvent("learningV400MeasuredRollingSprintLease", bot, new
            {
                missionId = mission.MissionId,
                stepIndex = mission.CurrentStepIndex,
                edgeId = step.EdgeId,
                kind = step.Kind,
                chainId = step.ChainId,
                generation = cursor.LearningV375MovementGeneration,
                measuredPhysicalSpeed = cursor.LearningV375PhysicalSpeed,
                measuredBodySampleDistance = 0f,
                forwardDot = (float?)null,
                forwardDotMeasuredThisFrame = false,
                leaseSeconds = PostTraversalNormalFollowAckStableSeconds,
                source = string.IsNullOrWhiteSpace(source) ? "successorBoundary" : source,
                moverSprinting = bot.Mover.Sprinting == true,
                playerSprinting = player.MovementContext.IsSprintEnabled,
                sprintEnabledByV400 = false,
                traversalSuccessRecorded = false,
                persistentLearningChanged = false,
                action = "preserveAlreadyRealSprintAcrossPassageBoundaryUsingRecentMeasuredRouteProgress"
            });
        }

        internal static bool ShouldProtectLearningV400PhysicalPassageSprint(BotOwner bot, out string reason)
        {
            reason = string.Empty;
            if ((bot == null) ||
                string.IsNullOrEmpty(bot.ProfileId) || bot.Mover == null || bot.Mover.NoSprint ||
                !FollowerTraversalRouteCursors.TryGetValue(bot.ProfileId, out FollowerTraversalRouteCursor cursor) ||
                cursor == null || !cursor.LearningV375TransactionActive ||
                Time.time > cursor.LearningV400RollingSprintLeaseUntil)
            {
                return false;
            }

            long generation = FollowerMovementWriterAuthority.GetCurrentGeneration(
                bot, out FollowerMovementWriterAuthority.WriterDomain owner);
            if (owner != FollowerMovementWriterAuthority.WriterDomain.Traversal ||
                generation != cursor.LearningV400RollingSprintGeneration ||
                generation != cursor.LearningV375MovementGeneration ||
                !TryResolveLearningV376CursorMission(bot, cursor, out FollowerRouteMission mission) ||
                mission == null || mission.MissionId != cursor.LearningV400RollingSprintMissionId ||
                mission.CurrentStepIndex != cursor.LearningV400RollingSprintStepIndex ||
                mission.CurrentStepIndex < 0 || mission.CurrentStepIndex >= mission.Steps.Count)
            {
                return false;
            }

            FollowerRouteMissionStep step = mission.Steps[mission.CurrentStepIndex];
            LearningV354TraversalCapabilityProfile profile = step != null
                ? GetLearningV354ActionProfile(GetLearningV354SemanticKind(step.EdgeId, step.Kind))
                : null;
            Player player = bot.GetPlayer ?? bot.AIData?.Player;
            if (step == null || profile == null || profile.RequiresActuator ||
                !profile.PhysicalPassageCanProveCompletion || bot.Memory?.HaveEnemy == true ||
                player?.MovementContext?.CanSprint != true || player.MovementContext.CanWalk != true)
            {
                return false;
            }

            reason = "learningV400MeasuredRollingPhysicalPassage";
            return true;
        }

        private static void RebaseLearningV375PersistentRouteTransactionAfterRecovery(
            BotOwner bot, int chainId, long generation, Vector3 position, string source)
        {
            if ((bot == null) ||
                chainId <= 0 || generation <= 0 ||
                !TryGetLearningV375MissionForChain(bot, chainId, out FollowerRouteMission mission) ||
                mission == null)
            {
                return;
            }
            FollowerTraversalRouteCursor cursor = GetOrCreateLearningV375RouteCursor(bot);
            if (cursor == null)
            {
                return;
            }
            cursor.LearningV375TransactionActive = true;
            cursor.LearningV375TransactionChainId = chainId;
            cursor.LearningV376CurrentProvenanceChainId = chainId;
            cursor.LearningV375RouteMissionId = mission.MissionId;
            cursor.LearningV376PreProbeProducerActive = false;
            cursor.LearningV375MovementGeneration = generation;
            cursor.LearningV375PhysicalFrontier = position;
            cursor.LearningV375LastBodyPosition = position;
            cursor.LearningV375LastBodySampleAt = Time.time;
            cursor.LearningV375PhysicalSpeed = 0f;
            cursor.LearningV375ActuatorBorrowed = false;
            cursor.LearningV375ActuatorEdgeId = 0;
            cursor.LearningV375ActuatorKind = string.Empty;
            cursor.LearningV375ActuatorBorrowedAt = -1f;
            cursor.LearningV375ProducerKind = "TraversalRecoveryEpoch";
            cursor.LearningV375ProducerGapStartedAt = -1f;
            if (TryProjectLearningV375RouteArc(
                    mission, position, out float routeArc, out Vector3 tangent, out _, out _))
            {
                cursor.LearningV375RouteArcLength = routeArc;
                cursor.LearningV375BestRouteArcLength = Mathf.Max(cursor.LearningV375BestRouteArcLength, routeArc);
                cursor.LearningV375RouteTangent = tangent;
            }
            WriteEvent("learningV375RouteTransactionRebasedAfterRecovery", bot, new
            {
                chainId,
                missionId = mission.MissionId,
                generation,
                position = Vec(position),
                routeArc = cursor.LearningV375BestRouteArcLength,
                source = source ?? string.Empty,
                action = "exceptionRecoveryMintsNewPhysicalEpochButRetainsSameSemanticRouteTransaction"
            });
        }

        private static long AcquireLearningV375PersistentRouteTransaction(
            ProbeRuntime probe, string source, out bool reused)
        {
            reused = false;
            if ((probe?.Bot == null) ||
                probe.TraversalChainId <= 0)
            {
                return FollowerMovementWriterAuthority.BeginTraversal(
                    probe?.Bot,
                    $"{source} edge={probe?.LearnedEdgeId ?? 0} chain={probe?.TraversalChainId ?? 0} kind={probe?.RequestedTraversalKind ?? string.Empty}");
            }

            FollowerTraversalRouteCursor cursor = GetOrCreateLearningV375RouteCursor(probe.Bot);
            TryResolveLearningV376MissionForProbe(probe, out FollowerRouteMission probeMission, out _);
            long currentGeneration = FollowerMovementWriterAuthority.GetCurrentGeneration(
                probe.Bot, out FollowerMovementWriterAuthority.WriterDomain currentOwner);
            bool compatibleTransaction = cursor != null && cursor.LearningV375TransactionActive &&
                ((IsLearningV376CursorCompatibleWithProbe(cursor, probe, out _)));
            if (compatibleTransaction && cursor.LearningV375MovementGeneration > 0 &&
                currentOwner == FollowerMovementWriterAuthority.WriterDomain.Traversal &&
                currentGeneration == cursor.LearningV375MovementGeneration)
            {
                long continued = FollowerMovementWriterAuthority.ContinueTraversal(
                    probe.Bot, cursor.LearningV375MovementGeneration,
                    $"learningV376Continue:{source}:edge={probe.LearnedEdgeId}:mission={cursor.LearningV375RouteMissionId}:chain={probe.TraversalChainId}");
                if (continued > 0)
                {
                    reused = true;
                    cursor.LearningV375TransactionChainId = probe.TraversalChainId;
                    cursor.LearningV376CurrentProvenanceChainId = probe.TraversalChainId;
                    cursor.LearningV376PreProbeProducerActive = false;
                    cursor.LearningV375BoundaryCount++;
                    cursor.LearningV375LastBoundaryAt = Time.time;
                    cursor.LearningV375LastBoundarySpeed = cursor.LearningV375PhysicalSpeed;
                    cursor.LearningV375BoundaryGeneration = continued;
                    WriteEvent("learningV376PersistentRouteWriterReusedAcrossMissionStep", probe.Bot, new
                    {
                        edgeId = probe.LearnedEdgeId,
                        episodeId = probe.TraversalEpisodeId,
                        provenanceChainId = probe.TraversalChainId,
                        missionId = cursor.LearningV375RouteMissionId,
                        generation = continued,
                        boundaryCount = cursor.LearningV375BoundaryCount,
                        speed = cursor.LearningV375PhysicalSpeed,
                        producer = cursor.LearningV375ProducerKind ?? string.Empty,
                        action = ("sameAuthoredChainKeepsOneTraversalWriterRouteMissionOnlyOrdersCrossChainSteps")
                    });
                    return continued;
                }
            }

            long generation = FollowerMovementWriterAuthority.BeginTraversal(
                probe.Bot,
                $"learningV376Start:{source}:edge={probe.LearnedEdgeId}:chain={probe.TraversalChainId}:kind={probe.RequestedTraversalKind}");
            if (generation <= 0 || cursor == null)
            {
                return generation;
            }
            TryCarryCapabilityJumpBodyObservation(probe,probeMission,cursor,currentGeneration,generation,currentOwner,source);
            cursor.LearningV375TransactionActive = true;
            cursor.LearningV375TransactionChainId = probe.TraversalChainId;
            cursor.LearningV376CurrentProvenanceChainId = probe.TraversalChainId;
            cursor.LearningV375RouteMissionId = probeMission?.MissionId ?? 0;
            cursor.LearningV376PreProbeProducerActive = false;
            cursor.LearningV375MovementGeneration = generation;
            cursor.LearningV375PhysicalFrontier = probe.Player?.Position ?? probe.Bot.Position;
            cursor.LearningV375LastBodyPosition = cursor.LearningV375PhysicalFrontier;
            cursor.LearningV375LastBodySampleAt = Time.time;
            cursor.LearningV375PhysicalSpeed = 0f;
            cursor.LearningV375RouteArcLength = 0f;
            cursor.LearningV375BestRouteArcLength = 0f;
            cursor.LearningV375RouteTangent = Vector3.zero;
            cursor.LearningV375ProducerTarget = Vector3.zero;
            cursor.LearningV375ProducerKind = "TraversalProbe";
            cursor.LearningV375ActuatorBorrowed = false;
            cursor.LearningV375ActuatorEdgeId = 0;
            cursor.LearningV375ActuatorKind = string.Empty;
            cursor.LearningV375ProducerGapStartedAt = -1f;
            cursor.LearningV375MaxProducerGapSeconds = 0f;
            cursor.LearningV375BoundaryCount = 0;
            cursor.LearningV375BoundaryGeneration = generation;
            WriteEvent("learningV376PersistentRouteTransactionStarted", probe.Bot, new
            {
                edgeId = probe.LearnedEdgeId,
                episodeId = probe.TraversalEpisodeId,
                provenanceChainId = probe.TraversalChainId,
                missionId = cursor.LearningV375RouteMissionId,
                generation,
                position = Vec(cursor.LearningV375PhysicalFrontier),
                action = ("routeMissionOrdersExecutionPhysicalTransactionIdentityIsAuthoredChain")
            });
            return generation;
        }

        private static bool TryGetLearningV375FutureSameChainSuccessor(
            ProbeRuntime probe,
            out FollowerRouteMission mission,
            out FollowerRouteMissionStep step)
        {
            mission = null;
            step = null;
            if ((probe?.Bot == null) ||
                !TryResolveLearningV376MissionForProbe(probe, out mission, out int probeStepIndex) ||
                mission.CurrentStepIndex < 0 || mission.CurrentStepIndex >= mission.Steps.Count)
            {
                return false;
            }
            for (int index = Math.Max(probeStepIndex + 1, mission.CurrentStepIndex); index < mission.Steps.Count; index++)
            {
                FollowerRouteMissionStep candidate = mission.Steps[index];
                if (candidate == null)
                {
                    continue;
                }
                if ((candidate.ChainId > 0) && candidate.ChainId != probe.TraversalChainId)
                {
                    return false;
                }
                step = candidate;
                return true;
            }
            return false;
        }

        private static bool TryGetLearningV410CurrentRouteSuccessorAfterProbeNoMutation(
            ProbeRuntime probe,
            out FollowerRouteMission mission,
            out FollowerRouteMissionStep step)
        {
            mission = null;
            step = null;
            if ((probe?.Bot == null) ||
                !TryResolveLearningV376MissionForProbe(probe, out mission, out int probeStepIndex) ||
                mission?.Steps == null || mission.CurrentStepIndex < 0 ||
                mission.CurrentStepIndex >= mission.Steps.Count || mission.CurrentStepIndex <= probeStepIndex)
            {
                return false;
            }

            FollowerRouteMissionStep current = mission.Steps[mission.CurrentStepIndex];
            if (current == null ||
                (current.EpisodeId == probe.TraversalEpisodeId && current.EdgeId == probe.LearnedEdgeId))
            {
                return false;
            }

            step = current;
            return true;
        }

        private static bool TryGetLearningV375CurrentSameChainSuccessorAfterProbe(
            ProbeRuntime probe,
            out FollowerRouteMission mission,
            out FollowerRouteMissionStep step)
        {
            mission = null;
            step = null;
            if ((probe?.Bot == null) ||
                !TryResolveLearningV376MissionForProbe(probe, out mission, out int probeStepIndex) ||
                mission.CurrentStepIndex < 0 || mission.CurrentStepIndex >= mission.Steps.Count ||
                mission.CurrentStepIndex <= probeStepIndex)
            {
                return false;
            }

            // Mission advancement, not the existence of a future ChainId, proves predecessor
            // completion. This keeps V3.75's failed-step negative control while allowing one
            // executable RouteMission to cross recorder chain boundaries without losing movement.
            FollowerRouteMissionStep current = mission.Steps[mission.CurrentStepIndex];
            bool learningV390CrossChainCanonicalSuccessor =
                (((current != null))) &&
                current.ChainId > 0 && probe.TraversalChainId > 0 &&
                current.ChainId != probe.TraversalChainId;
            if (current == null ||
                (current.EpisodeId == probe.TraversalEpisodeId && current.EdgeId == probe.LearnedEdgeId) ||
                ((current.ChainId > 0) && current.ChainId != probe.TraversalChainId &&
                 !learningV390CrossChainCanonicalSuccessor))
            {
                return false;
            }
            step = current;
            bool learningV391CanonicalSuccessorMoverRebased = false;
            string learningV391CanonicalSuccessorMoverRebaseDetail = string.Empty;
            bool learningV391CanonicalSuccessorMoverForwardCompatible = true;
            float learningV391CanonicalSuccessorMoverCornerArc = 0f;
            float learningV391CanonicalSuccessorMoverCornerForwardDot = 1f;
            string learningV391CanonicalSuccessorMoverCompatibilityReason = "notInspected";
            if ((learningV390CrossChainCanonicalSuccessor))
            {
                FollowerTraversalRouteCursor learningV391Cursor =
                    GetOrCreateLearningV375RouteCursor(probe.Bot);
                float learningV391PhysicalFrontierArc =
                    learningV391Cursor?.LearningV375BestRouteArcLength ?? 0f;
                Vector3 learningV391RouteTangent =
                    learningV391Cursor?.LearningV375RouteTangent ?? Vector3.zero;
                learningV391CanonicalSuccessorMoverForwardCompatible =
                    TryGetLearningV377ActiveMoverForwardCompatibility(
                        probe.Bot,
                        mission,
                        learningV391PhysicalFrontierArc,
                        learningV391RouteTangent,
                        out learningV391CanonicalSuccessorMoverCornerArc,
                        out learningV391CanonicalSuccessorMoverCornerForwardDot,
                        out learningV391CanonicalSuccessorMoverCompatibilityReason);
                if (!learningV391CanonicalSuccessorMoverForwardCompatible)
                {
                    learningV391CanonicalSuccessorMoverRebased =
                        TryRetireNativeMoverPathAtCurrentBody(
                            probe.Bot,
                            "learningV391CanonicalSuccessorRebase",
                            out learningV391CanonicalSuccessorMoverRebaseDetail);
                }
                else
                {
                    learningV391CanonicalSuccessorMoverRebaseDetail =
                        "preserved:" + (learningV391CanonicalSuccessorMoverCompatibilityReason ?? string.Empty);
                }
                WriteEvent("learningV391CanonicalSuccessorMoverEpochReconciled", probe.Bot, new
                {
                    missionId = mission.MissionId,
                    completedStepIndex = probeStepIndex,
                    currentStepIndex = mission.CurrentStepIndex,
                    completedEpisodeId = probe.TraversalEpisodeId,
                    completedEdgeId = probe.LearnedEdgeId,
                    completedChainId = probe.TraversalChainId,
                    successorEpisodeId = current.EpisodeId,
                    successorEdgeId = current.EdgeId,
                    successorKind = current.Kind,
                    successorChainId = current.ChainId,
                    moverForwardCompatible = learningV391CanonicalSuccessorMoverForwardCompatible,
                    moverCornerArc = learningV391CanonicalSuccessorMoverCornerArc,
                    moverCornerForwardDot = learningV391CanonicalSuccessorMoverCornerForwardDot,
                    moverCompatibilityReason =
                        learningV391CanonicalSuccessorMoverCompatibilityReason ?? string.Empty,
                    moverRebased = learningV391CanonicalSuccessorMoverRebased,
                    detail = learningV391CanonicalSuccessorMoverRebaseDetail ?? string.Empty,
                    writerGenerationPreserved = true,
                    semanticChainMerged = false,
                    persistentLearningChanged = false,
                    action = learningV391CanonicalSuccessorMoverForwardCompatible
                        ? "forwardCompatibleMoverPathSurvivesCanonicalSuccessorBoundary"
                        : "backwardPredecessorMoverPathRetiredBeforeCanonicalSuccessorReusesSameTraversalWriter"
                });
            }
            if (learningV390CrossChainCanonicalSuccessor)
            {
                WriteEvent("learningV390CanonicalMissionSuccessorSurvivesProbeUnregister", probe.Bot, new
                {
                    missionId = mission.MissionId,
                    completedStepIndex = probeStepIndex,
                    currentStepIndex = mission.CurrentStepIndex,
                    completedEpisodeId = probe.TraversalEpisodeId,
                    completedEdgeId = probe.LearnedEdgeId,
                    completedChainId = probe.TraversalChainId,
                    successorEpisodeId = current.EpisodeId,
                    successorEdgeId = current.EdgeId,
                    successorKind = current.Kind,
                    successorChainId = current.ChainId,
                    learningV391CanonicalSuccessorMoverRebased,
                    learningV391CanonicalSuccessorMoverRebaseDetail =
                        learningV391CanonicalSuccessorMoverRebaseDetail ?? string.Empty,
                    semanticChainMerged = false,
                    persistentLearningChanged = false,
                    action = "canonicalExecutableMissionSuccessorPreventsTerminalConsumerFromEndingLivePhysicalTransaction"
                });
            }
            return true;
        }

        private static void MarkLearningV375ActuatorBorrowed(ProbeRuntime probe, string kind, string source)
        {
            if ((probe?.Bot == null) || probe.TraversalChainId <= 0)
            {
                return;
            }
            FollowerTraversalRouteCursor cursor = GetOrCreateLearningV375RouteCursor(probe.Bot);
            if (cursor == null || !cursor.LearningV375TransactionActive ||
                ((!IsLearningV376CursorCompatibleWithProbe(cursor, probe, out _))))
            {
                return;
            }
            cursor.LearningV375TransactionChainId = probe.TraversalChainId;
            cursor.LearningV376CurrentProvenanceChainId = probe.TraversalChainId;
            cursor.LearningV375ActuatorBorrowed = true;
            cursor.LearningV375ActuatorEdgeId = probe.LearnedEdgeId;
            cursor.LearningV375ActuatorKind = kind ?? string.Empty;
            cursor.LearningV375ActuatorBorrowedAt = Time.time;
            cursor.LearningV375ProducerKind = "Actuator:" + (kind ?? string.Empty);
            WriteEvent("learningV375ActuatorBodyAuthorityBorrowed", probe.Bot, new
            {
                edgeId = probe.LearnedEdgeId,
                chainId = probe.TraversalChainId,
                missionId = cursor.LearningV375RouteMissionId,
                generation = cursor.LearningV375MovementGeneration,
                kind = kind ?? string.Empty,
                source = source ?? string.Empty,
                routeArc = cursor.LearningV375BestRouteArcLength,
                speed = cursor.LearningV375PhysicalSpeed,
                action = "actuatorBorrowsBodyInsidePersistentRouteTransaction"
            });
        }

        private static void ReturnLearningV375ActuatorBodyAuthority(ProbeRuntime probe, string source)
        {
            if ((probe?.Bot == null) || probe.TraversalChainId <= 0)
            {
                return;
            }
            FollowerTraversalRouteCursor cursor = GetOrCreateLearningV375RouteCursor(probe.Bot);
            if (cursor == null || !cursor.LearningV375TransactionActive ||
                ((!IsLearningV376CursorCompatibleWithProbe(cursor, probe, out _))) ||
                !cursor.LearningV375ActuatorBorrowed)
            {
                return;
            }
            float borrowedFor = cursor.LearningV375ActuatorBorrowedAt >= 0f
                ? Mathf.Max(0f, Time.time - cursor.LearningV375ActuatorBorrowedAt) : 0f;
            WriteEvent("learningV375ActuatorBodyAuthorityReturned", probe.Bot, new
            {
                edgeId = cursor.LearningV375ActuatorEdgeId,
                chainId = probe.TraversalChainId,
                missionId = cursor.LearningV375RouteMissionId,
                generation = cursor.LearningV375MovementGeneration,
                kind = cursor.LearningV375ActuatorKind ?? string.Empty,
                source = source ?? string.Empty,
                borrowedFor,
                position = Vec(probe.Player?.Position ?? probe.Bot.Position),
                action = "bodyAuthorityReturnsToSamePersistentRouteProducer"
            });
            cursor.LearningV375TransactionChainId = probe.TraversalChainId;
            cursor.LearningV376CurrentProvenanceChainId = probe.TraversalChainId;
            cursor.LearningV375ActuatorBorrowed = false;
            cursor.LearningV375ActuatorEdgeId = 0;
            cursor.LearningV375ActuatorKind = string.Empty;
            cursor.LearningV375ActuatorBorrowedAt = -1f;
            cursor.LearningV375ProducerKind = "TraversalRoute";
        }

        private static void EndLearningV375PersistentRouteTransaction(BotOwner bot, string reason)
        {
            if ((bot == null))
            {
                return;
            }
            FollowerTraversalRouteCursor cursor = GetOrCreateLearningV375RouteCursor(bot);
            if (cursor == null || !cursor.LearningV375TransactionActive)
            {
                return;
            }
            long generation = cursor.LearningV375MovementGeneration;
            WriteEvent("learningV375PersistentRouteTransactionCompleted", bot, new
            {
                chainId = cursor.LearningV375TransactionChainId,
                missionId = cursor.LearningV375RouteMissionId,
                generation,
                routeArc = cursor.LearningV375BestRouteArcLength,
                maxProducerGapSeconds = cursor.LearningV375MaxProducerGapSeconds,
                boundaryCount = cursor.LearningV375BoundaryCount,
                reason = reason ?? string.Empty,
                action = "terminalConsumerEndsRouteTransactionOnlyAfterLastActionNode"
            });
            cursor.LearningV375TransactionActive = false;
            cursor.LearningV375TransactionChainId = 0;
            cursor.LearningV375RouteMissionId = 0;
            cursor.LearningV376CurrentProvenanceChainId = 0;
            cursor.LearningV376PreProbeProducerActive = false;
            cursor.LearningV375MovementGeneration = 0;
            cursor.LearningV375ActuatorBorrowed = false;
            cursor.LearningV375ActuatorEdgeId = 0;
            cursor.LearningV375ActuatorKind = string.Empty;
            cursor.LearningV375ProducerGapStartedAt = -1f;
        }

        private static bool TryYieldLearningV402PersistentRouteTransactionToTerminalRecovery(
            BotOwner bot,
            FollowerRouteMission mission,
            FollowerTraversalRouteCursor cursor)
        {
            if ((bot == null) || mission == null ||
                cursor == null || !cursor.LearningV375TransactionActive ||
                !FollowerCapabilityPassagePolicy.TerminalMayYield(IsLearningV350TerminalPhysicalRecoveryEligible(bot),HasActiveTraversalProbe(bot)))
            {
                return false;
            }

            // V350 already proved measured no-progress for this movement epoch. Continuing V375
            // under the same Traversal generation after that verdict creates an authority
            // contradiction: Follow/NavigationRecovery is supposed to own the next decision while
            // the persistent producer can still write route targets. End only the runtime movement
            // epoch here; keep RouteMission and persistent semantic data readable so the existing
            // V362/V369 fresh-route supersession logic can admit genuinely newer player evidence.
            string profileId = bot.ProfileId ?? string.Empty;
            int stepIndex = mission.CurrentStepIndex;
            FollowerRouteMissionStep step = stepIndex >= 0 && stepIndex < mission.Steps.Count
                ? mission.Steps[stepIndex]
                : null;
            long traversalGeneration = cursor.LearningV375MovementGeneration;
            float terminalSince = -1f;
            string terminalReason = string.Empty;
            int terminalEpisodeId = 0;
            int terminalBreadcrumbSequence = 0;
            if (!string.IsNullOrEmpty(profileId) &&
                FollowerMovementRecoveryStates.TryGetValue(
                    profileId, out FollowerMovementRecoveryState recoveryState) &&
                recoveryState != null)
            {
                terminalSince = recoveryState.LearningV350TerminalRecoverySince;
                terminalReason = recoveryState.LearningV350TerminalRecoveryReason ?? string.Empty;
                terminalEpisodeId = recoveryState.LearningV362TerminalRecoveryLatchEpisodeId;
                terminalBreadcrumbSequence = recoveryState.LearningV362TerminalRecoveryLatchBreadcrumbSequence;
            }

            ClearLearningV365ContinuousPhysicalRoute(
                bot, "learningV402TerminalRecoveryCanonicalMovementBoundary");
            LearnedTraversalCommitments.Remove(profileId);
            FollowRejectLeases.Remove(profileId);
            TryScrubLearningV364NativeMoverEpochAnchor(
                bot, bot.Position, "learningV402TerminalRecoveryCanonicalMovementBoundary",
                out string scrubDetail);

            cursor.LearningV375TransactionActive = false;
            cursor.LearningV375TransactionChainId = 0;
            cursor.LearningV375RouteMissionId = 0;
            cursor.LearningV376CurrentProvenanceChainId = 0;
            cursor.LearningV376PreProbeProducerActive = false;
            cursor.LearningV375MovementGeneration = 0;
            cursor.LearningV375ActuatorBorrowed = false;
            cursor.LearningV375ActuatorEdgeId = 0;
            cursor.LearningV375ActuatorKind = string.Empty;
            cursor.LearningV375ActuatorBorrowedAt = -1f;
            cursor.LearningV375ProducerGapStartedAt = -1f;
            cursor.LearningV400RollingSprintMissionId = 0;
            cursor.LearningV400RollingSprintStepIndex = -1;
            cursor.LearningV400RollingSprintGeneration = 0;
            cursor.LearningV400RollingSprintLeaseUntil = -1f;

            FollowerMovementWriterAuthority.TransitionTraversalToFollowIfCurrent(
                bot, traversalGeneration, "learningV402TerminalRecoveryCanonicalMovementBoundary");

            WriteEvent("learningV402PersistentRouteMovementYieldedToTerminalRecovery", bot, new
            {
                missionId = mission.MissionId,
                stepIndex,
                episodeId = step?.EpisodeId ?? 0,
                edgeId = step?.EdgeId ?? 0,
                kind = step?.Kind ?? string.Empty,
                traversalGeneration,
                terminalSince,
                terminalReason,
                terminalEpisodeId,
                terminalBreadcrumbSequence,
                position = Vec(bot.Position),
                scrubDetail,
                traversalSuccessRecorded = false,
                persistentLearningChanged = false,
                bodyRelocated = false,
                routeMissionRetained = true,
                action = "terminalMeasuredNoProgressEndsOnlyPersistentMovementEpochThenExistingFollowRecoveryOwnsNextDecision"
            });
            return true;
        }

        private static bool TryCapLearningV375ProducerBeforeActuatorFrontier(
            FollowerRouteMission mission,
            float bodyArc,
            ref Vector3 target,
            ref Vector3 tangent,
            ref float targetArc,
            out int cappedEdgeId,
            out string cappedKind)
        {
            cappedEdgeId = 0;
            cappedKind = string.Empty;
            if ((mission == null) ||
                mission.CurrentStepIndex < 0 || mission.CurrentStepIndex >= mission.Steps.Count)
            {
                return false;
            }
            FollowerRouteMissionStep step = mission.Steps[mission.CurrentStepIndex];
            if (step == null || !GetLearningV354ActionProfile(step.Kind).RequiresActuator || !IsFinite(step.Entry))
            {
                return false;
            }
            if (!TryGetLearningV375SequenceArc(
                    mission, step.StartBreadcrumbSequence, out float entryArc, out Vector3 routeEntryPoint) ||
                entryArc <= bodyArc || targetArc <= entryArc)
            {
                return false;
            }

            Vector3 actuatorDirection = GetFlatDirection(step.RawExit - step.RawEntry);
            target = step.Entry;
            targetArc = entryArc;
            if (actuatorDirection.sqrMagnitude >= 0.5f)
            {
                tangent = actuatorDirection;
            }
            cappedEdgeId = step.EdgeId;
            cappedKind = step.Kind ?? string.Empty;
            return true;
        }

        private static void TryRequestCF13ClimbSprintRelease(BotOwner bot, int edgeId, string source)
        {
            if (bot?.Mover == null || bot.GetPlayer?.MovementContext == null ||
                !IsLiveBot(bot) || IsTeleportTransactionActive(bot) ||
                bot.Memory?.HaveEnemy == true || bot.Memory?.IsUnderFire == true)
                return;
            Player player = bot.GetPlayer;
            bool moverSprint = bot.Mover.Sprinting == true;
            bool eftSprint = player.MovementContext.IsSprintEnabled;
            bool stateSprint = player.CurrentStateName == EPlayerState.Sprint;
            if (!moverSprint && !eftSprint && !stateSprint)
                return;
            try
            {
                // Sprint(false) is the native BotMover command. Never write the
                // MovementContext sprint flag, animator, position, or NavMesh anchor.
                bot.Mover.BotMoverSprint("R1-0043E072ECD3",@bot,false,false);
                if (ShouldEmitDiagnostic("cf13ClimbSprintHandoff:" + edgeId, bot, 0.25f))
                {
                    WriteEvent("cf13ClimbSprintReleaseRequested", bot, new
                    {
                        edgeId, source,
                        moverSprintingBefore = moverSprint,
                        eftSprintEnabledBefore = eftSprint,
                        stateBefore = player.CurrentStateName.ToString(),
                        moverSprintingAfter = bot.Mover.Sprinting == true,
                        eftSprintEnabledAfter = player.MovementContext.IsSprintEnabled,
                        action = "nativeMoverRequestsSprintReleaseWithoutStoppingPhysicalRouteProducer"
                    });
                }
            }
            catch (Exception ex)
            {
                if (ShouldEmitDiagnostic("cf13ClimbSprintReleaseFailed:" + edgeId, bot, 0.5f))
                    WriteEvent("cf13ClimbSprintReleaseFailed", bot, new
                    {
                        edgeId, source, error = ex.GetType().Name + ": " + ex.Message
                    });
            }
        }

        private static void UpdateLearningV375PersistentExecutablePhysicalRouteTransaction(BotOwner bot)
        {
            // Semantic route WHAT cannot steer alongside its selected physical HOW.
            if (IsCapabilityPortalWaiting(bot) || HasCapabilityFailureStrategy(bot) || IsCapabilityRecoveryPending(bot) || IsCapabilityReturnInProgress(bot)) return;
            // A semantic target alone is not a live approach resource. Keep the existing
            // route producer when the target has no local, prefix-preserving NavMesh path.
            if(TryGetCapabilityPortalApproachTarget(bot,out var approachTarget,out _)&&
                TryValidateCapabilityPortalApproachPath(bot,approachTarget))return;
            if ((bot == null) || !IsLiveBot(bot) || !BossPlayers.IsFollower(bot))
            {
                return;
            }
            FollowerTraversalRouteCursor cursor = GetOrCreateLearningV375RouteCursor(bot);
            if (cursor == null || !cursor.LearningV375TransactionActive ||
                (cursor.LearningV375RouteMissionId <= 0 && cursor.LearningV375TransactionChainId <= 0))
            {
                return;
            }

            long currentGeneration = FollowerMovementWriterAuthority.GetCurrentGeneration(
                bot, out FollowerMovementWriterAuthority.WriterDomain owner);
            if (owner != FollowerMovementWriterAuthority.WriterDomain.Traversal ||
                currentGeneration != cursor.LearningV375MovementGeneration)
            {
                if (owner == FollowerMovementWriterAuthority.WriterDomain.Follow ||
                    owner == FollowerMovementWriterAuthority.WriterDomain.PostTraversal)
                {
                    EndLearningV375PersistentRouteTransaction(bot, "movementOwnerLeftTraversal:" + owner);
                }
                return;
            }

            Vector3 position = bot.Position;
            float now = Time.time;
            float learningV376BodySampleDistance = 0f;
            Vector3 learningV377BodyDelta = Vector3.zero;
            if (cursor.LearningV375LastBodySampleAt >= 0f && now > cursor.LearningV375LastBodySampleAt)
            {
                float elapsed = now - cursor.LearningV375LastBodySampleAt;
                learningV377BodyDelta = position - cursor.LearningV375LastBodyPosition;
                learningV377BodyDelta.y = 0f;
                learningV376BodySampleDistance = learningV377BodyDelta.magnitude;
                cursor.LearningV375PhysicalSpeed = learningV376BodySampleDistance / Mathf.Max(0.001f, elapsed);
                ObserveCapabilityPlanarBodyVelocity(cursor,position,now,currentGeneration);
                if (learningV376BodySampleDistance > 0.01f)
                {
                    cursor.LearningV375PhysicalFrontier = position;
                }
            }
            // CF80 read-only full Y sample; do not mistake a falling bot for frozen body.
            float cf80VerticalBodyDelta = cursor.LearningV375LastBodySampleAt >= 0f
                ? Mathf.Abs(position.y - cursor.LearningV375LastBodyPosition.y)
                : float.PositiveInfinity;
            cursor.LearningV375LastBodyPosition = position;
            cursor.LearningV375LastBodySampleAt = now;
            if ((ShouldEmitDiagnostic("learningV409MeasuredNativeLocomotionProgress", bot, 0.20f)))
            {
                WriteEvent("learningV409MeasuredNativeLocomotionProgress", bot, new
                {
                    missionId = cursor.LearningV375RouteMissionId,
                    chainId = cursor.LearningV375TransactionChainId,
                    generation = cursor.LearningV375MovementGeneration,
                    producer = cursor.LearningV375ProducerKind ?? string.Empty,
                    position = Vec(position),
                    bodyDelta = Vec(learningV377BodyDelta),
                    bodySampleDistance = learningV376BodySampleDistance,
                    physicalSpeed = cursor.LearningV375PhysicalSpeed,
                    traversalSuccessRecorded = false,
                    persistentLearningChanged = false,
                    action = "consumeNextFrameMeasuredBodyDeltaAsOnlyNativeLocomotionProgressTruth"
                });
            }

            FollowerRouteMission mission = null;
            bool haveMission = (TryResolveLearningV376CursorMission(bot, cursor, out mission));
            // CF80: observe the specific ungrounded/Run/zero-delta successor limbo before
            // altering any physical owner. This is a read-only lifecycle audit, NOT rescue,
            // success credit, forced grounding, new writer, or bypass of Native admission.
            // Require a real active same-mission DropDown step and no active native probe.
            if (haveMission && mission != null && mission.Steps != null &&
                mission.CurrentStepIndex >= 0 &&
                mission.CurrentStepIndex < mission.Steps.Count &&
                mission.Steps[mission.CurrentStepIndex] != null &&
                string.Equals(mission.Steps[mission.CurrentStepIndex].Kind,
                    "DropDown", StringComparison.OrdinalIgnoreCase) &&
                Time.time - mission.LastProgressAt > 1.0f &&
                learningV376BodySampleDistance < 0.005f &&
                cf80VerticalBodyDelta < 0.005f &&
                bot.GetPlayer?.MovementContext != null &&
                !bot.GetPlayer.MovementContext.IsGrounded &&
                (bot.GetPlayer.CurrentStateName == EPlayerState.Run ||
                 bot.GetPlayer.CurrentStateName == EPlayerState.Sprint) &&
                !HasActiveTraversalProbe(bot) &&
                !IsTeleportTransactionActive(bot) &&
                !FollowerStateAuthorityCoordinator.HasCombatMovementClaim(bot, out _) &&
                ShouldEmitDiagnostic("cf80StalledAirborneSuccessorPhysicalConsumer", bot, 0.85f))
            {
                FollowerRouteMissionStep cf80Step =
                    mission.Steps[mission.CurrentStepIndex];
                WriteEvent("cf80StalledAirborneSuccessorPhysicalConsumer", bot, new
                {
                    missionId = mission.MissionId,
                    stepIndex = mission.CurrentStepIndex,
                    episodeId = cf80Step.EpisodeId,
                    edgeId = cf80Step.EdgeId,
                    chainId = cf80Step.ChainId,
                    generation = currentGeneration,
                    movementOwner = owner.ToString(),
                    body = Vec(position),
                    entry = Vec(cf80Step.Entry),
                    exit = Vec(cf80Step.Exit),
                    lastMeasuredProgressAge = Time.time - mission.LastProgressAt,
                    groundReported = bot.GetPlayer.MovementContext.IsGrounded,
                    playerState = bot.GetPlayer.CurrentStateName.ToString(),
                    producer = cursor.LearningV375ProducerKind ?? string.Empty,
                    measuredPhysicalDelta = learningV376BodySampleDistance,
                    measuredVerticalBodyDelta = cf80VerticalBodyDelta,
                    hasCompleteNativePath = bot.Mover?.HasPathAndNoComplete == true,
                    // RC48: a predecessor's supported-landing FALLBACK is not
                    // the successor's EFT grounded/Run admission. Surface the
                    // actual blocking geometry without inventing Native success.
                    successorEntryPlanarDistance = GetPlanarDistance(position, cf80Step.RawEntry),
                    successorEntryHeightDelta = position.y - cf80Step.RawEntry.y,
                    successorCannotNativeStartWithoutGrounded = true,
                    successRecorded = false,
                    nativeAdmissionMutated = false,
                    bodyRelocated = false,
                    stateOrInputWritten = false,
                    action = "inspectOffNavMeshNativeLocomotionBlackholeWithoutNewWriter"
                });
            }

            // V4.69: V4.55 physically consumes a real Jump HOW/current-body far-side WHAT
            // and advances CurrentStepIndex. If this exhausts Steps, V3.76's ordinary
            // cursor->mission resolver deliberately stops exposing the mission (it requires
            // a valid CURRENT step). Consequently V3.75 never gets to terminalize it, and
            // V3.50's unrelated 3+3s watchdog must take over. Inspect ONLY that hidden
            // terminal identity; do not weaken the shared resolver or admit a new traversal.
            // The last breadcrumb must actually have been consumed by the same current
            // physical transaction, so rejected/skipped/unproven steps cannot claim credit.
            if ((!haveMission) &&
                !string.IsNullOrEmpty(bot.ProfileId) &&
                FollowerRouteMissions.TryGetValue(bot.ProfileId, out FollowerRouteMission learningV469TerminalMission) &&
                learningV469TerminalMission != null &&
                learningV469TerminalMission.MissionId == cursor.LearningV375RouteMissionId &&
                learningV469TerminalMission.ExpiresAt >= Time.time &&
                learningV469TerminalMission.Steps != null &&
                learningV469TerminalMission.Steps.Count > 0 &&
                learningV469TerminalMission.CurrentStepIndex >= learningV469TerminalMission.Steps.Count &&
                learningV469TerminalMission.Steps[learningV469TerminalMission.Steps.Count - 1] != null &&
                learningV469TerminalMission.Steps[learningV469TerminalMission.Steps.Count - 1].EndBreadcrumbSequence > 0 &&
                // A mere advanced breadcrumb floor is not action completion. The last
                // consumed episode identity must match this exact terminal mission step.
                // This prevents a later unrelated route from closing an abandoned mission.
                learningV469TerminalMission.Steps[learningV469TerminalMission.Steps.Count - 1].EpisodeId > 0 &&
                cursor.LastCompletedEpisodeId ==
                    learningV469TerminalMission.Steps[learningV469TerminalMission.Steps.Count - 1].EpisodeId &&
                cursor.ConsumedBreadcrumbSequence >=
                    learningV469TerminalMission.Steps[learningV469TerminalMission.Steps.Count - 1].EndBreadcrumbSequence &&
                !HasActiveTraversalProbe(bot) &&
                !IsTeleportTransactionActive(bot) &&
                !IsLearningV452PendingAdjacentPlayerTraversalForMission(
                    learningV469TerminalMission, out _, out _, out _))
            {
                WriteEvent("learningV469TerminalSemanticDebtConsumerClosed", bot, new
                {
                    missionId = learningV469TerminalMission.MissionId,
                    completedSteps = learningV469TerminalMission.Steps.Count,
                    completedEpisodeId = cursor.LastCompletedEpisodeId,
                    consumedBreadcrumbSequence = cursor.ConsumedBreadcrumbSequence,
                    requiredEndBreadcrumbSequence = learningV469TerminalMission.Steps[
                        learningV469TerminalMission.Steps.Count - 1].EndBreadcrumbSequence,
                    physicalGeneration = cursor.LearningV375MovementGeneration,
                    physicalChainId = cursor.LearningV375TransactionChainId,
                    nativeSuccessRecorded = false,
                    traversalSuccessRecorded = false,
                    persistentLearningChanged = false,
                    bodyRelocated = false,
                    action = "exhaustedPhysicallyConsumedMissionRejoinsExistingTerminalFollowWithoutWatchdog"
                });
                EnsureFollowerRouteMissionCommitment(bot, learningV469TerminalMission);
                return;
            }
            if (haveMission &&
                TryAdvanceLearningV468ObservedDropDownLandingDebt(bot, mission, cursor))
            {
                // A completed route-local natural descent may expose an entirely new successor.
                // Do not let this frame's cached movement target run after cursor consumption.
                return;
            }
            if ((haveMission) &&
                TryAdvanceLearningV424PhysicallySubsumedActuatorDebt(
                    bot, mission, "persistentRoutePreActuatorAdmission"))
            {
                // Consumption may finish the mission or bind a later canonical step. Never let the
                // just-retired actuator's cached frontier run again in this body epoch.
                return;
            }
            if (haveMission &&
                TryYieldLearningV402PersistentRouteTransactionToTerminalRecovery(
                    bot, mission, cursor))
            {
                return;
            }
            if (haveMission &&
                TryConsumeLearningV408ImmediatePhysicalPassageCompletion(bot, mission, cursor))
            {
                // Completion may advance to an actuator successor or finish the mission. Do not let
                // the old step's cached producer state execute again in this update; the next route
                // update starts from the already-primed successor/current body epoch.
                return;
            }

            if ((haveMission) &&
                (cursor.LearningV393NavMeshStarvedMissionId != mission.MissionId ||
                 cursor.LearningV393NavMeshStarvedStepIndex != mission.CurrentStepIndex))
            {
                cursor.LearningV393NavMeshStarvedMissionId = 0;
                cursor.LearningV393NavMeshStarvedStepIndex = -1;
                cursor.LearningV395NavMeshStarvedRouteArc = -1f;
                cursor.LearningV395NavMeshRequalificationCount = 0;
            }
            // CF15: keep the V375/V377/V395 route-progress decision atomic in one
            // module. No threshold, owner, producer invalidation, or semantic credit changes.
            EvaluatePhysicalRouteProgress(
                bot, haveMission, mission, cursor, position,
                learningV376BodySampleDistance, learningV377BodyDelta,
                out float learningV377CurrentRouteArc,
                out bool learningV377RouteRegressionInvalidatesProducer,
                out bool learningV395PhysicalBodyRouteRegression);

            if(haveMission&&mission?.Steps!=null&&mission.CurrentStepIndex>=0&&mission.CurrentStepIndex<mission.Steps.Count&&
                TryConsumeCapabilityClimbIngress(bot,mission,mission.Steps[mission.CurrentStepIndex]))
                return; // Current Climb owns pose/input; never replay an older route-prefix target.

            // The persistent route must consume the same accepted preparation input.
            // Otherwise its old center-point target overwrites that input in the same update.
            if (haveMission && mission?.Steps != null && mission.CurrentStepIndex >= 0 &&
                mission.CurrentStepIndex < mission.Steps.Count &&
                string.Equals(mission.Steps[mission.CurrentStepIndex]?.Kind, "Jump", StringComparison.OrdinalIgnoreCase) &&
                TryDriveCapabilityJumpPreparationConsumer(bot, mission, mission.Steps[mission.CurrentStepIndex]))
            {
                if (ShouldEmitDiagnostic("capabilityPersistentPreparation:" + mission.MissionId, bot, .25f))
                    WriteEvent("capabilityPersistentPreparationConsumerSelected", bot, new
                    {
                        missionId = mission.MissionId, stepIndex = mission.CurrentStepIndex,
                        episodeId = mission.Steps[mission.CurrentStepIndex].EpisodeId,
                        generation = currentGeneration, inputAccepted = true, nativeSuccessRecorded = false,
                        action = "persistentRouteYieldsToSameAcceptedPreparationConsumer"
                    });
                return;
            }


            if (haveMission &&
                TryArbitrateLearningV447RegressedJumpPreAdmissionProducer(
                    bot, mission, cursor, learningV377RouteRegressionInvalidatesProducer))
            {
                return;
            }

            if (haveMission &&
                TryRetireLearningV397ExhaustedActuatorDebtByCanonicalSuccessorBodyEpoch(
                    bot, mission, cursor))
            {
                return;
            }

            if (haveMission &&
                TryRetireLearningV443StaleActuatorDebtByMonotonicBodyFrontier(
                    bot, mission, cursor))
            {
                return;
            }

            if (haveMission && TryRetireLearningV394DownstreamPassageExecutionDebt(bot, mission, cursor))
            {
                return;
            }

            if (haveMission && mission.CurrentStepIndex >= 0 &&
                mission.CurrentStepIndex < mission.Steps.Count &&
                TryHandoffLearningV388PhysicalCorridorToReadySuccessor(
                    bot, mission, mission.Steps[mission.CurrentStepIndex]))
            {
                return;
            }

            bool haveActiveProbe = TryGetActiveProbe(bot, out ProbeRuntime activeProbe) && activeProbe != null;
            // Reuse the exact body-displacement evidence that already advances V375's physical
            // frontier. A semantic Probe is not a movement producer unless the body actually moved
            // this sample; no new speed/timeout threshold is introduced by V376.
            bool learningV376ProbeHasMeasuredPhysicalProgress = FollowerCapabilityHelperRules.UpdateLearningV375PersistentExecutablePhysicalRouteTransaction_learningV376ProbeHasMeasuredPhysicalProgress_CC9769FF_1(haveActiveProbe,learningV376BodySampleDistance);
            bool learningV377ActiveMoverForwardCompatible = true;
            float learningV377ActiveMoverCornerArc = cursor.LearningV375BestRouteArcLength;
            float learningV377ActiveMoverCornerForwardDot = 1f;
            string learningV377ActiveMoverReason = "notInspected";
            if ((haveMission) && bot.Mover?.IsMoving == true)
            {
                learningV377ActiveMoverForwardCompatible =
                    TryGetLearningV377ActiveMoverForwardCompatibility(
                        bot, mission, cursor.LearningV375BestRouteArcLength,
                        cursor.LearningV375RouteTangent,
                        out learningV377ActiveMoverCornerArc,
                        out learningV377ActiveMoverCornerForwardDot,
                        out learningV377ActiveMoverReason);
                if (!learningV377ActiveMoverForwardCompatible)
                {
                    learningV377RouteRegressionInvalidatesProducer = true;
                    if (ShouldEmitDiagnostic(
                            "learningV377ActiveMoverPhysicalEpochRejected:" +
                                mission.MissionId.ToString(CultureInfo.InvariantCulture),
                            bot, 0.18f))
                    {
                        WriteEvent("learningV377ActiveMoverPhysicalEpochRejected", bot, new
                        {
                            missionId = mission.MissionId,
                            generation = cursor.LearningV375MovementGeneration,
                            bestRouteArc = cursor.LearningV375BestRouteArcLength,
                            currentRouteArc = learningV377CurrentRouteArc,
                            cornerArc = learningV377ActiveMoverCornerArc,
                            cornerForwardDot = learningV377ActiveMoverCornerForwardDot,
                            reason = learningV377ActiveMoverReason,
                            traversalSuccessRecorded = false,
                            persistentLearningChanged = false,
                            action = "staleBackwardMoverPathCannotCountAsPersistentPhysicalProducer"
                        });
                    }
                }
            }
            bool learningV380ActiveActuatorLifecycleOwnsCurrentStep = false;
            if ((haveActiveProbe) && haveMission &&
                activeProbe.LearningV32NativeEntryAcquired &&
                activeProbe.FrozenTraversalCommitment != null &&
                mission.CurrentStepIndex >= 0 && mission.CurrentStepIndex < mission.Steps.Count)
            {
                FollowerRouteMissionStep learningV380LifecycleStep = mission.Steps[mission.CurrentStepIndex];
                LearningV354TraversalCapabilityProfile learningV380LifecycleProfile =
                    learningV380LifecycleStep != null
                        ? GetLearningV354ActionProfile(
                            GetLearningV354SemanticKind(
                                learningV380LifecycleStep.EdgeId, learningV380LifecycleStep.Kind))
                        : null;
                LearnedTraversalCommitment learningV380LifecycleCommitment =
                    activeProbe.FrozenTraversalCommitment;
                bool learningV380SameMissionStep = learningV380LifecycleStep != null &&
                    learningV380LifecycleCommitment.RouteMissionId == mission.MissionId &&
                    learningV380LifecycleCommitment.RouteMissionStepIndex == mission.CurrentStepIndex &&
                    (learningV380LifecycleStep.EdgeId <= 0 || activeProbe.LearnedEdgeId <= 0 ||
                     learningV380LifecycleStep.EdgeId == activeProbe.LearnedEdgeId) &&
                    (learningV380LifecycleStep.EpisodeId <= 0 || activeProbe.TraversalEpisodeId <= 0 ||
                     learningV380LifecycleStep.EpisodeId == activeProbe.TraversalEpisodeId);
                bool learningV381ActuatorStillOwnsBodyStage =
                    ((activeProbe.Stage != ProbeStage.Egress &&
                     activeProbe.Stage != ProbeStage.Verify &&
                     activeProbe.Stage != ProbeStage.ExitCommit &&
                     activeProbe.Stage != ProbeStage.TeleportArm &&
                     activeProbe.Stage != ProbeStage.SafetyHold));
                learningV380ActiveActuatorLifecycleOwnsCurrentStep = learningV380SameMissionStep &&
                    learningV380LifecycleProfile != null && learningV380LifecycleProfile.RequiresActuator &&
                    learningV381ActuatorStillOwnsBodyStage;
                if ((learningV380SameMissionStep) &&
                    learningV380LifecycleProfile != null && learningV380LifecycleProfile.RequiresActuator &&
                    !learningV381ActuatorStillOwnsBodyStage &&
                    ShouldEmitDiagnostic(
                        "learningV381ActuatorLifecycleReturnedToRoute:" +
                            mission.MissionId.ToString(CultureInfo.InvariantCulture) + ":" +
                            activeProbe.LearnedEdgeId.ToString(CultureInfo.InvariantCulture),
                        bot, 0.25f))
                {
                    WriteEvent("learningV381ActuatorLifecycleReturnedToRoute", bot, new
                    {
                        missionId = mission.MissionId,
                        stepIndex = mission.CurrentStepIndex,
                        edgeId = activeProbe.LearnedEdgeId,
                        episodeId = activeProbe.TraversalEpisodeId,
                        kind = activeProbe.TraversalKind ?? activeProbe.RequestedTraversalKind,
                        stage = activeProbe.Stage.ToString(),
                        actuatorBorrowed = cursor.LearningV375ActuatorBorrowed,
                        traversalSuccessRecorded = false,
                        persistentLearningChanged = false,
                        action = "egressVerifyExitCommitBelongToPersistentRouteConsumerNotNativeEntryLifecycle"
                    });
                }
                if (learningV380ActiveActuatorLifecycleOwnsCurrentStep &&
                    ShouldEmitDiagnostic(
                        "learningV380ActiveActuatorLifecycleOwnership:" +
                            mission.MissionId.ToString(CultureInfo.InvariantCulture) + ":" +
                            activeProbe.LearnedEdgeId.ToString(CultureInfo.InvariantCulture),
                        bot, 0.25f))
                {
                    WriteEvent("learningV380ActiveActuatorLifecycleOwnership", bot, new
                    {
                        missionId = mission.MissionId,
                        stepIndex = mission.CurrentStepIndex,
                        edgeId = activeProbe.LearnedEdgeId,
                        episodeId = activeProbe.TraversalEpisodeId,
                        kind = activeProbe.TraversalKind ?? activeProbe.RequestedTraversalKind,
                        stage = activeProbe.Stage.ToString(),
                        entryAcquired = activeProbe.LearningV32NativeEntryAcquired,
                        movementGeneration = activeProbe.MovementWriterGeneration,
                        traversalSuccessRecorded = false,
                        persistentLearningChanged = false,
                        action = "activeNativeActuatorOwnsBodyUntilProbeTerminalizesNeverRestageCurrentEntry"
                    });
                }
            }

            bool learningV419ExactActiveActuatorPostFrontierOwnsPhysicalProducer = false;
            if ((haveActiveProbe) && haveMission &&
                activeProbe.FrozenTraversalCommitment != null &&
                mission.CurrentStepIndex >= 0 && mission.CurrentStepIndex < mission.Steps.Count)
            {
                FollowerRouteMissionStep learningV419Step = mission.Steps[mission.CurrentStepIndex];
                LearnedTraversalCommitment learningV419Commitment = activeProbe.FrozenTraversalCommitment;
                LearningV354TraversalCapabilityProfile learningV419Profile = learningV419Step != null
                    ? GetLearningV354ActionProfile(
                        GetLearningV354SemanticKind(learningV419Step.EdgeId, learningV419Step.Kind))
                    : null;
                bool learningV419SameCurrentStep = learningV419Step != null &&
                    learningV419Commitment.RouteMissionId == mission.MissionId &&
                    learningV419Commitment.RouteMissionStepIndex == mission.CurrentStepIndex &&
                    (learningV419Step.EdgeId <= 0 || activeProbe.LearnedEdgeId <= 0 ||
                     learningV419Step.EdgeId == activeProbe.LearnedEdgeId) &&
                    (learningV419Step.EpisodeId <= 0 || activeProbe.TraversalEpisodeId <= 0 ||
                     learningV419Step.EpisodeId == activeProbe.TraversalEpisodeId) &&
                    activeProbe.MovementWriterGeneration == cursor.LearningV375MovementGeneration;
                bool learningV419PostActuatorBodyStage =
                    activeProbe.Stage == ProbeStage.Egress ||
                    activeProbe.Stage == ProbeStage.Verify ||
                    activeProbe.Stage == ProbeStage.ExitCommit;
                learningV419ExactActiveActuatorPostFrontierOwnsPhysicalProducer =
                    learningV419SameCurrentStep && learningV419Profile != null &&
                    learningV419Profile.RequiresActuator && learningV419PostActuatorBodyStage;
                if (learningV419ExactActiveActuatorPostFrontierOwnsPhysicalProducer &&
                    ShouldEmitDiagnostic(
                        "learningV419CurrentProbePostActuatorFrontierOwnsRouteProducer:" +
                            mission.MissionId.ToString(CultureInfo.InvariantCulture) + ":" +
                            activeProbe.LearnedEdgeId.ToString(CultureInfo.InvariantCulture),
                        bot, 0.18f))
                {
                    WriteEvent("learningV419CurrentProbePostActuatorFrontierOwnsRouteProducer", bot, new
                    {
                        missionId = mission.MissionId,
                        stepIndex = mission.CurrentStepIndex,
                        edgeId = activeProbe.LearnedEdgeId,
                        episodeId = activeProbe.TraversalEpisodeId,
                        kind = activeProbe.TraversalKind ?? activeProbe.RequestedTraversalKind,
                        stage = activeProbe.Stage.ToString(),
                        movementGeneration = activeProbe.MovementWriterGeneration,
                        body = Vec(bot.Position),
                        traversalSuccessRecorded = false,
                        persistentLearningChanged = false,
                        action = "activeProbeOwnsCurrentStepThroughEgressVerifyExitCommitNeverRestageHistoricalEntry"
                    });
                }
            }

            bool learningV385WalkThroughPreEntryProbeOwnsTranslation = false;
            bool learningV410ExactActiveWalkThroughProbeOwnsPhysicalProducer = false;
            if ((haveActiveProbe) && haveMission &&
                activeProbe.FrozenTraversalCommitment != null &&
                mission.CurrentStepIndex >= 0 && mission.CurrentStepIndex < mission.Steps.Count)
            {
                FollowerRouteMissionStep learningV385WalkThroughStep = mission.Steps[mission.CurrentStepIndex];
                LearnedTraversalCommitment learningV385WalkThroughCommitment = activeProbe.FrozenTraversalCommitment;
                bool learningV385ExactCurrentWalkThroughStep = learningV385WalkThroughStep != null &&
                    string.Equals(
                        learningV385WalkThroughStep.Kind,
                        "WalkThrough",
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(
                        activeProbe.TraversalKind ?? activeProbe.RequestedTraversalKind,
                        "WalkThrough",
                        StringComparison.OrdinalIgnoreCase) &&
                    learningV385WalkThroughCommitment.RouteMissionId == mission.MissionId &&
                    learningV385WalkThroughCommitment.RouteMissionStepIndex == mission.CurrentStepIndex &&
                    (learningV385WalkThroughStep.EdgeId <= 0 || activeProbe.LearnedEdgeId <= 0 ||
                     learningV385WalkThroughStep.EdgeId == activeProbe.LearnedEdgeId) &&
                    (learningV385WalkThroughStep.EpisodeId <= 0 || activeProbe.TraversalEpisodeId <= 0 ||
                     learningV385WalkThroughStep.EpisodeId == activeProbe.TraversalEpisodeId) &&
                    activeProbe.MovementWriterGeneration == cursor.LearningV375MovementGeneration;
                learningV385WalkThroughPreEntryProbeOwnsTranslation =
                    learningV385ExactCurrentWalkThroughStep && activeProbe.Stage == ProbeStage.Aligning;
                learningV410ExactActiveWalkThroughProbeOwnsPhysicalProducer =
                    (learningV385ExactCurrentWalkThroughStep);
                if ((learningV410ExactActiveWalkThroughProbeOwnsPhysicalProducer) &&
                    activeProbe.Stage != ProbeStage.Aligning &&
                    ShouldEmitDiagnostic(
                        "learningV411ExactWalkThroughFullLifecycleProducerOwnership:" +
                            mission.MissionId.ToString(CultureInfo.InvariantCulture) + ":" +
                            activeProbe.LearnedEdgeId.ToString(CultureInfo.InvariantCulture),
                        bot, 0.25f))
                {
                    WriteEvent("learningV411ExactWalkThroughFullLifecycleProducerOwnership", bot, new
                    {
                        missionId = mission.MissionId,
                        stepIndex = mission.CurrentStepIndex,
                        edgeId = activeProbe.LearnedEdgeId,
                        episodeId = activeProbe.TraversalEpisodeId,
                        stage = activeProbe.Stage.ToString(),
                        movementGeneration = activeProbe.MovementWriterGeneration,
                        moverHasPath = activeProbe.Bot?.Mover?.HasPathAndNoComplete == true,
                        traversalSuccessRecorded = false,
                        persistentLearningChanged = false,
                        action = "repairV410AligningOnlyOuterGateExactWalkThroughOwnsPhysicalProducerUntilTerminalization"
                    });
                }
                if (learningV410ExactActiveWalkThroughProbeOwnsPhysicalProducer &&
                    ShouldEmitDiagnostic(
                        "learningV410ExactActiveProbeOwnsPhysicalProducer:" +
                            mission.MissionId.ToString(CultureInfo.InvariantCulture) + ":" +
                            activeProbe.LearnedEdgeId.ToString(CultureInfo.InvariantCulture),
                        bot, 0.25f))
                {
                    WriteEvent("learningV410ExactActiveProbeOwnsPhysicalProducer", bot, new
                    {
                        missionId = mission.MissionId,
                        stepIndex = mission.CurrentStepIndex,
                        edgeId = activeProbe.LearnedEdgeId,
                        episodeId = activeProbe.TraversalEpisodeId,
                        stage = activeProbe.Stage.ToString(),
                        movementGeneration = activeProbe.MovementWriterGeneration,
                        moverHasPath = activeProbe.Bot?.Mover?.HasPathAndNoComplete == true,
                        traversalSuccessRecorded = false,
                        persistentLearningChanged = false,
                        action = "exactActiveWalkThroughProbeOwnsPhysicalProducerForFullLifecycleNoParallelRouteWriter"
                    });
                }
                if (learningV385WalkThroughPreEntryProbeOwnsTranslation &&
                    ShouldEmitDiagnostic(
                        "learningV385WalkThroughPreEntryProbeOwnsTranslation:" +
                            mission.MissionId.ToString(CultureInfo.InvariantCulture) + ":" +
                            activeProbe.LearnedEdgeId.ToString(CultureInfo.InvariantCulture),
                        bot, 0.25f))
                {
                    WriteEvent("learningV385WalkThroughPreEntryProbeOwnsTranslation", bot, new
                    {
                        missionId = mission.MissionId,
                        stepIndex = mission.CurrentStepIndex,
                        edgeId = activeProbe.LearnedEdgeId,
                        episodeId = activeProbe.TraversalEpisodeId,
                        stage = activeProbe.Stage.ToString(),
                        movementGeneration = activeProbe.MovementWriterGeneration,
                        traversalSuccessRecorded = false,
                        persistentLearningChanged = false,
                        action = "exactWalkThroughEntryProbeKeepsMoverTargetUntilEntryAuthorityHandoff"
                    });
                }
            }

            bool learningV386ContinuousPhysicalRouteOwnsTranslation = false;
            if ((haveMission) &&
                mission.CurrentStepIndex >= 0 && mission.CurrentStepIndex < mission.Steps.Count &&
                LearningV365ContinuousRouteProducerStates.TryGetValue(
                    bot.ProfileId ?? string.Empty, out LearningV365ContinuousRouteProducerState learningV386ContinuousProducer) &&
                learningV386ContinuousProducer != null && !learningV386ContinuousProducer.Generic)
            {
                FollowerRouteMissionStep learningV386ContinuousStep = mission.Steps[mission.CurrentStepIndex];
                learningV386ContinuousPhysicalRouteOwnsTranslation = learningV386ContinuousStep != null &&
                    learningV386ContinuousProducer.MissionId == mission.MissionId &&
                    learningV386ContinuousProducer.StepIndex == mission.CurrentStepIndex &&
                    learningV386ContinuousProducer.EdgeId == learningV386ContinuousStep.EdgeId;
            }

            bool activeProducer = FollowerCapabilityHelperRules.UpdateLearningV375PersistentExecutablePhysicalRouteTransaction_activeProducer_CC9769FF_2(cursor.LearningV375ActuatorBorrowed,learningV380ActiveActuatorLifecycleOwnsCurrentStep,learningV419ExactActiveActuatorPostFrontierOwnsPhysicalProducer,learningV385WalkThroughPreEntryProbeOwnsTranslation,learningV410ExactActiveWalkThroughProbeOwnsPhysicalProducer,learningV386ContinuousPhysicalRouteOwnsTranslation) ||
                                  (bot.Mover?.IsMoving == true && learningV377ActiveMoverForwardCompatible &&
                                   !learningV377RouteRegressionInvalidatesProducer) ||
                                  (learningV376ProbeHasMeasuredPhysicalProgress &&
                                   !learningV377RouteRegressionInvalidatesProducer);
            if (activeProducer)
            {
                if (cursor.LearningV375ProducerGapStartedAt >= 0f)
                {
                    float gap = Mathf.Max(0f, now - cursor.LearningV375ProducerGapStartedAt);
                    cursor.LearningV375MaxProducerGapSeconds = Mathf.Max(cursor.LearningV375MaxProducerGapSeconds, gap);
                    WriteEvent("learningV375RouteProducerGapClosed", bot, new
                    {
                        chainId = cursor.LearningV375TransactionChainId,
                        generation = cursor.LearningV375MovementGeneration,
                        gapSeconds = gap,
                        maxGapSeconds = cursor.LearningV375MaxProducerGapSeconds,
                        activeProbe = haveActiveProbe,
                        semanticProbeOnly = haveActiveProbe && !learningV376ProbeHasMeasuredPhysicalProgress &&
                            !cursor.LearningV375ActuatorBorrowed && bot.Mover?.IsMoving != true,
                        measuredPhysicalSpeed = cursor.LearningV375PhysicalSpeed,
                        measuredBodySampleDistance = learningV376BodySampleDistance,
                        physicalFrontierSampleEpsilon = 0.01f,
                        actuatorBorrowed = cursor.LearningV375ActuatorBorrowed,
                        moverMoving = bot.Mover?.IsMoving == true
                    });
                    cursor.LearningV375ProducerGapStartedAt = -1f;
                }
                if (cursor.LearningV375ActuatorBorrowed)
                {
                    cursor.LearningV375ProducerKind = "Actuator:" + (cursor.LearningV375ActuatorKind ?? string.Empty);
                }
                else if (learningV380ActiveActuatorLifecycleOwnsCurrentStep)
                {
                    cursor.LearningV375ProducerKind = "TraversalProbeActuatorLifecycle";
                }
                else if (learningV419ExactActiveActuatorPostFrontierOwnsPhysicalProducer)
                {
                    cursor.LearningV375ProducerKind = "V419ActuatorPostFrontierProbe";
                }
                else if (learningV410ExactActiveWalkThroughProbeOwnsPhysicalProducer)
                {
                    cursor.LearningV375ProducerKind = ("V411ExactWalkThroughProbe");
                }
                else if (learningV385WalkThroughPreEntryProbeOwnsTranslation)
                {
                    cursor.LearningV375ProducerKind = "WalkThroughPreEntryProbe";
                }
                else if (learningV386ContinuousPhysicalRouteOwnsTranslation)
                {
                    cursor.LearningV375ProducerKind = "ContinuousPhysicalRoute";
                }
                else if (bot.Mover?.IsMoving == true)
                {
                    cursor.LearningV375ProducerKind = "BotMover";
                }
                else
                {
                    cursor.LearningV375ProducerKind = "TraversalProbePhysicalProgress";
                }
                return;
            }

            if (cursor.LearningV375ProducerGapStartedAt < 0f)
            {
                cursor.LearningV375ProducerGapStartedAt = now;
            }
            if (!haveMission || mission.CurrentStepIndex < 0 || mission.CurrentStepIndex >= mission.Steps.Count)
            {
                return;
            }
            {
                FollowerRouteMissionStep learningV455CurrentStep = mission.Steps[mission.CurrentStepIndex];
                var r2DebtObservation22 = CaptureRouteDebtObservation(bot, mission, learningV455CurrentStep);
                if (learningV455CurrentStep != null &&
                    learningV455CurrentStep.LearningV455NativeJumpHowObserved &&
                    string.Equals(learningV455CurrentStep.Kind, "Jump", StringComparison.OrdinalIgnoreCase) &&
                    TryEvaluateLearningV452CurrentBodySemanticFrontier(
                        bot, learningV455CurrentStep,
                        out float learningV455Projection, out float learningV455RequiredProjection,
                        out float learningV455Lateral, out string learningV455Proof) &&
                    ConsumeFollowerRouteMissionPhysicalPass(
                        bot, mission, learningV455CurrentStep,
                        "learningV455NativeJumpHowCurrentBodySemanticClosure:" + learningV455Proof,
                        learningV455Projection, learningV455RequiredProjection))
                {
                    int learningV455CompletedStepIndex = mission.CurrentStepIndex;
                    if (!TryAdvanceRouteMission(bot, mission, mission.CurrentStepIndex + 1, RouteDebtEvidence.PhysicalPass, "UpdateLearningV375PersistentExecutablePhysicalRouteTransaction:20", r2DebtObservation22)) return;
                    mission.LastProgressAt = now;
                    mission.LastProgressPosition = bot.Position;
                    CommitCapabilityMoveRetry(mission,FollowerCapabilityRetryPolicy.Change.Reset,0f);
                    mission.TraversalRetryMovementHoldUntil = 0f;
                    WriteEvent("learningV455NativeJumpHowCurrentBodySemanticClosure", bot, new
                    {
                        missionId = mission.MissionId,
                        completedStepIndex = learningV455CompletedStepIndex,
                        edgeId = learningV455CurrentStep.EdgeId,
                        body = Vec(bot.Position),
                        projection = learningV455Projection,
                        requiredProjection = learningV455RequiredProjection,
                        lateralDistance = learningV455Lateral,
                        proofSource = learningV455Proof,
                        traversalSuccessRecorded = false,
                        nativeSuccessRecorded = false,
                        persistentLearningChanged = false,
                        bodyRelocated = false,
                        action = "realNativeJumpHOWPlusCurrentBodyFarSideWHATRetiresOnlyRaidLocalExecutionDebt"
                    });
                    // V4.70: V4.55 just consumed exact same-episode physical WHAT and moved
                    // CurrentStepIndex beyond the final action. The post-step V4.69 fallback
                    // was not reached on the observed exhausted physical producer. Close now,
                    // with no extra frame, no fake Native completion, and no watchdog timeout.
                    if ((mission.Steps != null) &&
                        learningV455CompletedStepIndex == mission.Steps.Count - 1 &&
                        mission.CurrentStepIndex == mission.Steps.Count &&
                        cursor.LearningV375TransactionActive &&
                        cursor.LearningV375RouteMissionId == mission.MissionId &&
                        cursor.LastCompletedEpisodeId == learningV455CurrentStep.EpisodeId &&
                        cursor.ConsumedBreadcrumbSequence >= learningV455CurrentStep.EndBreadcrumbSequence &&
                        !string.IsNullOrEmpty(bot.ProfileId) &&
                        FollowerRouteMissions.TryGetValue(bot.ProfileId, out FollowerRouteMission learningV470CurrentMission) &&
                        ReferenceEquals(learningV470CurrentMission, mission) &&
                        !HasActiveTraversalProbe(bot) &&
                        !IsTeleportTransactionActive(bot) &&
                        !IsLearningV452PendingAdjacentPlayerTraversalForMission(
                            mission, out _, out _, out _))
                    {
                        long learningV470OwnerGeneration = FollowerMovementWriterAuthority.GetCurrentGeneration(
                            bot, out FollowerMovementWriterAuthority.WriterDomain learningV470Owner);
                        if (learningV470Owner == FollowerMovementWriterAuthority.WriterDomain.Traversal &&
                            learningV470OwnerGeneration == cursor.LearningV375MovementGeneration)
                        {
                            WriteEvent("learningV470PhysicalJumpTerminalConsumerCommitted", bot, new
                            {
                                missionId = mission.MissionId,
                                lastEpisodeId = learningV455CurrentStep.EpisodeId,
                                lastEdgeId = learningV455CurrentStep.EdgeId,
                                physicalGeneration = learningV470OwnerGeneration,
                                consumedBreadcrumbSequence = cursor.ConsumedBreadcrumbSequence,
                                requiredEndBreadcrumbSequence = learningV455CurrentStep.EndBreadcrumbSequence,
                                nativeSuccessRecorded = false,
                                traversalSuccessRecorded = false,
                                persistentLearningChanged = false,
                                bodyRelocated = false,
                                action = "exactRealBodyCompletionHandsTerminalRouteToExistingFollowConsumerSameFrame"
                            });
                            EnsureFollowerRouteMissionCommitment(bot, mission);
                        }
                    }
                    return;
                }
            }
            if (!TryProjectLearningV375RouteArc(
                    mission, position,
                    out float bodyArc, out Vector3 bodyTangent,
                    out _, out _) ||
                !TryGetLearningV375RouteLookAheadTarget(
                    mission, Mathf.Max(bodyArc, cursor.LearningV375BestRouteArcLength),
                    out Vector3 target, out Vector3 targetTangent, out float targetArc))
            {
                return;
            }
            bool learningV377ActuatorReady = false;
            bool learningV377ActuatorProducerTargetAvailable = false;
            string learningV377ActuatorFrontierSource = string.Empty;
            bool learningV377ActuatorPhysicalFrontierOwnsCurrentStep =
                TryGetLearningV377ActuatorPhysicalFrontierDecision(
                    bot, mission, bodyArc,
                    ref target, ref targetTangent, ref targetArc,
                    out learningV377ActuatorReady,
                    out learningV377ActuatorProducerTargetAvailable,
                    out int learningV377ActuatorEdgeId,
                    out string learningV377ActuatorKind,
                    out learningV377ActuatorFrontierSource);

            bool learningV375ActuatorFrontierCapped = false;
            int learningV375CappedEdgeId = 0;
            string learningV375CappedKind = string.Empty;
            if (!learningV377ActuatorPhysicalFrontierOwnsCurrentStep)
            {
                learningV375ActuatorFrontierCapped = TryCapLearningV375ProducerBeforeActuatorFrontier(
                    mission, bodyArc, ref target, ref targetTangent, ref targetArc,
                    out learningV375CappedEdgeId, out learningV375CappedKind);
            }
            else
            {
                learningV375ActuatorFrontierCapped = true;
                learningV375CappedEdgeId = learningV377ActuatorEdgeId;
                learningV375CappedKind = learningV377ActuatorKind;
            }

            FollowerRouteMissionStep learningV405CurrentStep =
                mission.CurrentStepIndex >= 0 && mission.CurrentStepIndex < mission.Steps.Count
                    ? mission.Steps[mission.CurrentStepIndex]
                    : null;

            LearningV354TraversalCapabilityProfile learningV405CurrentProfile =
                learningV405CurrentStep != null
                    ? GetLearningV354ActionProfile(
                        GetLearningV354SemanticKind(
                            learningV405CurrentStep.EdgeId, learningV405CurrentStep.Kind))
                    : null;
            bool learningV405TargetCapped = false;
            float learningV405OriginalTargetArc = targetArc;
            float learningV405StepExitArc = -1f;
            float learningV405CappedTargetArc = targetArc;
            if (!learningV377ActuatorPhysicalFrontierOwnsCurrentStep &&
                learningV405CurrentStep != null &&
                learningV405CurrentProfile != null && !learningV405CurrentProfile.RequiresActuator &&
                learningV405CurrentProfile.PhysicalPassageCanProveCompletion)
            {
                learningV405TargetCapped = TryCapLearningV405NonActuatorTargetAtCurrentStepExit(
                    mission, learningV405CurrentStep, learningV405CurrentProfile, bodyArc,
                    ref target, ref targetTangent, ref targetArc,
                    out learningV405OriginalTargetArc, out learningV405StepExitArc,
                    out learningV405CappedTargetArc);
                if (learningV405TargetCapped && ShouldEmitDiagnostic(
                        "learningV405NonActuatorExitTargetCapped:" +
                            mission.MissionId.ToString(CultureInfo.InvariantCulture) + ":" +
                            mission.CurrentStepIndex.ToString(CultureInfo.InvariantCulture), bot, 0.35f))
                {
                    WriteEvent("learningV405NonActuatorExitTargetCapped", bot, new
                    {
                        missionId = mission.MissionId,
                        stepIndex = mission.CurrentStepIndex,
                        edgeId = learningV405CurrentStep.EdgeId,
                        kind = learningV405CurrentStep.Kind,
                        generation = cursor.LearningV375MovementGeneration,
                        bodyArc,
                        originalTargetArc = learningV405OriginalTargetArc,
                        stepExitArc = learningV405StepExitArc,
                        completionSafetyMargin = PostTraversalRollbackSideMargin,
                        cappedTargetArc = learningV405CappedTargetArc,
                        target = Vec(target),
                        traversalSuccessRecorded = false,
                        persistentLearningChanged = false,
                        bodyRelocated = false,
                        action = "nonActuatorCurrentStepCannotDrivePastItsCanonicalExitProofBoundary"
                    });
                }
            }

            if (learningV377ActuatorPhysicalFrontierOwnsCurrentStep && learningV377ActuatorReady)
            {
                bool learningV380AdoptedByActiveProbe = false;
                if ((TryGetActiveProbe(bot, out ProbeRuntime learningV380ActiveProbe)) &&
                    learningV380ActiveProbe != null &&
                    learningV380ActiveProbe.Stage == ProbeStage.RollingApproach &&
                    learningV380ActiveProbe.LearnedEdgeId == learningV377ActuatorEdgeId &&
                    string.Equals(
                        learningV380ActiveProbe.TraversalKind ?? learningV380ActiveProbe.RequestedTraversalKind,
                        learningV377ActuatorKind, StringComparison.OrdinalIgnoreCase) &&
                    (learningV380ActiveProbe.FrozenTraversalCommitment?.RouteMissionId ?? 0) == mission.MissionId)
                {
                    learningV380ActiveProbe.LearningV380RouteActuatorReady = true;
                    learningV380ActiveProbe.LearningV380RouteActuatorReadySource =
                        learningV377ActuatorFrontierSource ?? string.Empty;
                    learningV380ActiveProbe.LearningV380RouteActuatorReadyGeneration =
                        learningV380ActiveProbe.MovementWriterGeneration;
                    learningV380ActiveProbe.RollingApproachLastProgressAt = now;
                    learningV380AdoptedByActiveProbe = true;
                    if (ShouldEmitDiagnostic(
                            "learningV380ActiveActuatorProbeAdoptsRouteReadiness:" +
                                learningV377ActuatorEdgeId.ToString(CultureInfo.InvariantCulture),
                            bot, 0.18f))
                    {
                        WriteEvent("learningV380ActiveActuatorProbeAdoptsRouteReadiness", bot, new
                        {
                            missionId = mission.MissionId,
                            stepIndex = mission.CurrentStepIndex,
                            edgeId = learningV377ActuatorEdgeId,
                            kind = learningV377ActuatorKind,
                            stage = learningV380ActiveProbe.Stage.ToString(),
                            movementGeneration = learningV380ActiveProbe.MovementWriterGeneration,
                            source = learningV377ActuatorFrontierSource,
                            traversalSuccessRecorded = false,
                            persistentLearningChanged = false,
                            action = "feedExactV363RouteLaneReadinessIntoExistingRollingProbeNeverSpawnSecondExecutor"
                        });
                    }
                }
                string learningV378DispatchResult = learningV380AdoptedByActiveProbe
                    ? "existingRollingProbeAdoptedRouteReadiness"
                    : "notDispatched";
                bool learningV378Dispatched = !learningV380AdoptedByActiveProbe &&
                    TryDispatchLearningV378PersistentActuatorFrontierProbe(
                        bot, mission, learningV377ActuatorEdgeId, learningV377ActuatorKind,
                        out learningV378DispatchResult);
                cursor.LearningV375ProducerKind = learningV378Dispatched
                    ? "ActuatorFrontierDispatched"
                    : "ActuatorFrontierReady";
                if (ShouldEmitDiagnostic(
                        "learningV378PersistentProducerActuatorReady:" +
                            mission.MissionId.ToString(CultureInfo.InvariantCulture) + ":" +
                            learningV377ActuatorEdgeId.ToString(CultureInfo.InvariantCulture),
                        bot, 0.18f))
                {
                    WriteEvent("learningV378PersistentProducerActuatorReady", bot, new
                    {
                        missionId = mission.MissionId,
                        stepIndex = mission.CurrentStepIndex,
                        edgeId = learningV377ActuatorEdgeId,
                        kind = learningV377ActuatorKind,
                        source = learningV377ActuatorFrontierSource,
                        dispatched = learningV378Dispatched,
                        activeProbeAdopted = learningV380AdoptedByActiveProbe,
                        dispatchResult = learningV380AdoptedByActiveProbe
                            ? "existingRollingProbeAdoptedRouteReadiness"
                            : learningV378DispatchResult,
                        bodyArc,
                        bestRouteArc = cursor.LearningV375BestRouteArcLength,
                        traversalSuccessRecorded = false,
                        persistentLearningChanged = false,
                        action = learningV380AdoptedByActiveProbe
                            ? "existingRollingProbeConsumesExactRouteReadinessWithoutWriterOrProbeChurn"
                            : (learningV378Dispatched
                                ? "persistentProducerHandsBodyDirectlyToExactActuatorExecutor"
                                : "retainPhysicalFrontierWhileExactActuatorDispatchIsTemporarilyIneligible")
                    });
                }
                return;
            }

            if ((learningV377ActuatorPhysicalFrontierOwnsCurrentStep && !learningV377ActuatorReady) &&
                TryGetActiveProbe(bot, out ProbeRuntime learningV380NotReadyProbe) &&
                learningV380NotReadyProbe != null &&
                learningV380NotReadyProbe.LearnedEdgeId == learningV377ActuatorEdgeId)
            {
                learningV380NotReadyProbe.LearningV380RouteActuatorReady = false;
                learningV380NotReadyProbe.LearningV380RouteActuatorReadySource = string.Empty;
            }

            if (learningV377ActuatorPhysicalFrontierOwnsCurrentStep &&
                !learningV377ActuatorProducerTargetAvailable)
            {
                bool learningV436JumpRetainsPhysicalFrontierAdmissionOrdering =
                    (string.Equals(
                        learningV377ActuatorKind, "Jump",
                        StringComparison.OrdinalIgnoreCase));
                if ((!learningV436JumpRetainsPhysicalFrontierAdmissionOrdering) &&
                    !HasActiveTraversalProbe(bot))
                {
                    bool learningV435Dispatched =
                        TryDispatchLearningV378PersistentActuatorFrontierProbe(
                            bot, mission,
                            learningV377ActuatorEdgeId,
                            learningV377ActuatorKind,
                            out string learningV435DispatchResult);
                    if (learningV435Dispatched)
                    {
                        cursor.LearningV375ProducerKind = "ActuatorFrontierImmediateAdmission";
                        WriteEvent("learningV435ActuatorFrontierHoldConvertedToImmediateAdmission", bot, new
                        {
                            missionId = mission.MissionId,
                            stepIndex = mission.CurrentStepIndex,
                            edgeId = learningV377ActuatorEdgeId,
                            kind = learningV377ActuatorKind,
                            actuatorReady = learningV377ActuatorReady,
                            producerTargetAvailable = false,
                            source = learningV377ActuatorFrontierSource,
                            dispatchResult = learningV435DispatchResult ?? string.Empty,
                            bodyArc,
                            bestRouteArc = cursor.LearningV375BestRouteArcLength,
                            traversalSuccessRecorded = false,
                            persistentLearningChanged = false,
                            bodyRelocated = false,
                            action = "noStagingTargetDoesNotParkExactActuatorDebtNormalProbeAdmissionDecidesWhetherPhysicalExecutorCanStartNow"
                        });
                        return;
                    }

                    if (ShouldEmitDiagnostic(
                            "learningV435ActuatorFrontierImmediateAdmissionDeferred:" +
                                mission.MissionId.ToString(CultureInfo.InvariantCulture) + ":" +
                                learningV377ActuatorEdgeId.ToString(CultureInfo.InvariantCulture),
                            bot, 0.35f))
                    {
                        WriteEvent("learningV435ActuatorFrontierImmediateAdmissionDeferred", bot, new
                        {
                            missionId = mission.MissionId,
                            stepIndex = mission.CurrentStepIndex,
                            edgeId = learningV377ActuatorEdgeId,
                            kind = learningV377ActuatorKind,
                            actuatorReady = learningV377ActuatorReady,
                            producerTargetAvailable = false,
                            source = learningV377ActuatorFrontierSource,
                            dispatchResult = learningV435DispatchResult ?? string.Empty,
                            traversalSuccessRecorded = false,
                            persistentLearningChanged = false,
                            bodyRelocated = false,
                            action = "existingProbeAdmissionContractRejectedThisFrameRetainFrontierHoldWithoutInventingRetryOrGeometryThreshold"
                        });
                    }
                }

                cursor.LearningV375ProducerKind = "ActuatorFrontierHold";
                if (ShouldEmitDiagnostic(
                        "learningV377PersistentProducerYieldedToActuatorFrontier:" +
                            mission.MissionId.ToString(CultureInfo.InvariantCulture) + ":" +
                            learningV377ActuatorEdgeId.ToString(CultureInfo.InvariantCulture),
                        bot, 0.18f))
                {
                    WriteEvent("learningV377PersistentProducerYieldedToActuatorFrontier", bot, new
                    {
                        missionId = mission.MissionId,
                        stepIndex = mission.CurrentStepIndex,
                        edgeId = learningV377ActuatorEdgeId,
                        kind = learningV377ActuatorKind,
                        actuatorReady = learningV377ActuatorReady,
                        source = learningV377ActuatorFrontierSource,
                        bodyArc,
                        bestRouteArc = cursor.LearningV375BestRouteArcLength,
                        traversalSuccessRecorded = false,
                        persistentLearningChanged = false,
                        action = "unresolvedActuatorDebtCannotBeDrivenThroughByGenericRouteProducer"
                    });
                }
                return;
            }

            // CF28: a completed native DropDown may leave the real grounded body beyond
            // an immediate same-chain DropDown successor's historical entry/exit. V453's
            // generic admission target is then an *upstream* point, not a movement producer.
            // Keep that actuator debt alive (V382/V428/V468 still own physical proof), but
            // never send the collision-active body back through the finished descent to
            // manufacture a local entry. No success, teleport, new writer or geometry budget.
            if (learningV377ActuatorPhysicalFrontierOwnsCurrentStep &&
                learningV377ActuatorProducerTargetAvailable && !learningV377ActuatorReady &&
                string.Equals(learningV377ActuatorKind, "DropDown", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(learningV377ActuatorFrontierSource,
                    "learningV453SameChainSuccessorCurrentBodyProducer", StringComparison.Ordinal) &&
                mission.CurrentStepIndex > 0 && mission.CurrentStepIndex < mission.Steps.Count &&
                IsRouteMissionSameChainImmediateSuccessor(mission, mission.CurrentStepIndex) &&
                !HasActiveTraversalProbe(bot) && !IsTeleportTransactionActive(bot) &&
                bot.GetPlayer?.MovementContext?.IsGrounded == true &&
                cursor.LearningV375RouteMissionId == mission.MissionId &&
                targetArc + 0.35f < bodyArc &&
                targetArc + 0.35f < cursor.LearningV375BestRouteArcLength)
            {
                FollowerRouteMissionStep cf28Previous = mission.Steps[mission.CurrentStepIndex - 1];
                FollowerRouteMissionStep cf28Successor = mission.Steps[mission.CurrentStepIndex];
                Vector3 cf28Entry = cf28Successor != null && IsFinite(cf28Successor.RawEntry)
                    ? cf28Successor.RawEntry : (cf28Successor != null ? cf28Successor.Entry : default);
                Vector3 cf28Exit = cf28Successor != null && IsFinite(cf28Successor.RawExit)
                    ? cf28Successor.RawExit : (cf28Successor != null ? cf28Successor.Exit : default);
                Vector3 cf28Axis = GetFlatDirection(cf28Exit - cf28Entry);
                Vector3 cf28ToTarget = GetFlatDirection(target - position);
                float cf28Span = GetPlanarDistance(cf28Entry, cf28Exit);
                Vector3 cf28FromEntry = position - cf28Entry;
                cf28FromEntry.y = 0f;
                float cf28Projection = Vector3.Dot(cf28FromEntry, cf28Axis);
                // CF31: snapshot a genuine predecessor completion into this mission's
                // nonpersistent transaction. RecentTraversalCompletions is an anti-relatch
                // cache and may be removed after its 2.75s TTL; it cannot be the authority
                // for a still-live, same-generation far-side route producer guard.
                // Declare the out value independently to avoid CF30 CS0165 when a prior
                // short-circuit leaves the dictionary lookup unevaluated.
                RecentTraversalCompletion cf28Recent = null;
                bool cf31IdentityMatches = cf28Previous != null && cf28Successor != null &&
                    cf28Previous.EdgeId > 0 && cf28Previous.EpisodeId > 0 &&
                    cf28Previous.ChainId > 0 && cf28Previous.ChainId == cf28Successor.ChainId &&
                    cf28Previous.EpisodeId == cursor.LastCompletedEpisodeId &&
                    cf28Previous.ChainId == cursor.LastCompletedChainId &&
                    !string.IsNullOrEmpty(bot.ProfileId);
                bool cf31WriterMatches = cf31IdentityMatches &&
                    cursor.LearningV375TransactionActive &&
                    cursor.LearningV375RouteMissionId == mission.MissionId &&
                    cursor.LearningV375TransactionChainId == cf28Successor.ChainId &&
                    cursor.LearningV375MovementGeneration > 0 &&
                    FollowerMovementWriterAuthority.GetCurrentGeneration(
                        bot, out FollowerMovementWriterAuthority.WriterDomain cf31Owner) ==
                        cursor.LearningV375MovementGeneration &&
                    cf31Owner == FollowerMovementWriterAuthority.WriterDomain.Traversal;
                bool cf31RecentVerified = cf31IdentityMatches && cf31WriterMatches &&
                    RecentTraversalCompletions.TryGetValue(
                        MakeRecentTraversalCompletionKey(bot.ProfileId, cf28Previous.EdgeId),
                        out cf28Recent) && cf28Recent != null &&
                    cf28Recent.CompletedAt >= mission.CreatedAt &&
                    cf28Recent.CompletedAt <= Time.time &&
                    Time.time <= cf28Recent.ExpiresAt;
                if (cf31RecentVerified &&
                    (mission.Cf31FarSideProofStepIndex != mission.CurrentStepIndex ||
                     mission.Cf31FarSideProofGeneration != cursor.LearningV375MovementGeneration ||
                     mission.Cf31FarSideProofPredecessorEpisodeId != cf28Previous.EpisodeId))
                {
                    mission.Cf31FarSideProofStepIndex = mission.CurrentStepIndex;
                    mission.Cf31FarSideProofPredecessorEdgeId = cf28Previous.EdgeId;
                    mission.Cf31FarSideProofPredecessorEpisodeId = cf28Previous.EpisodeId;
                    mission.Cf31FarSideProofSuccessorEpisodeId = cf28Successor.EpisodeId;
                    mission.Cf31FarSideProofChainId = cf28Previous.ChainId;
                    mission.Cf31FarSideProofGeneration = cursor.LearningV375MovementGeneration;
                    mission.Cf31FarSideProofCompletedAt = cf28Recent.CompletedAt;
                    mission.Cf31FarSideProofRelatchExpiresAt = cf28Recent.ExpiresAt;
                    WriteEvent("cf31MissionPhysicalPredecessorProofLatched", bot, new
                    {
                        missionId = mission.MissionId,
                        stepIndex = mission.CurrentStepIndex,
                        predecessorEdgeId = cf28Previous.EdgeId,
                        predecessorEpisodeId = cf28Previous.EpisodeId,
                        successorEpisodeId = cf28Successor.EpisodeId,
                        chainId = cf28Previous.ChainId,
                        generation = cursor.LearningV375MovementGeneration,
                        predecessorCompletedAt = cf28Recent.CompletedAt,
                        traversalSuccessRecorded = false,
                        persistentLearningChanged = false,
                        action = "retainExistingCompletedPredecessorIdentityWithinOnlyThisLiveRouteTransaction"
                    });
                }
                bool cf28PhysicalPredecessor = cf31IdentityMatches && cf31WriterMatches &&
                    mission.Cf31FarSideProofStepIndex == mission.CurrentStepIndex &&
                    mission.Cf31FarSideProofPredecessorEdgeId == cf28Previous.EdgeId &&
                    mission.Cf31FarSideProofPredecessorEpisodeId == cf28Previous.EpisodeId &&
                    mission.Cf31FarSideProofSuccessorEpisodeId == cf28Successor.EpisodeId &&
                    mission.Cf31FarSideProofChainId == cf28Previous.ChainId &&
                    mission.Cf31FarSideProofGeneration == cursor.LearningV375MovementGeneration &&
                    mission.Cf31FarSideProofCompletedAt >= mission.CreatedAt &&
                    mission.Cf31FarSideProofCompletedAt <= Time.time;
                if (cf28PhysicalPredecessor && IsFinite(cf28Entry) && IsFinite(cf28Exit) &&
                    cf28Span >= 0.25f && cf28Axis.sqrMagnitude > 0.5f &&
                    cf28Projection >= cf28Span + 0.25f &&
                    position.y <= cf28Exit.y + DropDownAlreadyBelowLandingMargin &&
                    Vector3.Dot(cf28Axis, cf28ToTarget) < -0.25f)
                {
                    // CF58: CF32 already proved an exact SAME-CHAIN physical predecessor,
                    // grounded far-side body, and an inadmissible backwards successor target.
                    // Before latching terminal recovery, reuse V373's stricter *independent*
                    // operational downstream breadcrumb + path proof to retire only the stale
                    // EXECUTION obligation. V373 normally requires V357 exhausted self-rescue;
                    // here the exact CF32 far-side proof is stronger, so grant that admission
                    // only for this single call and restore its markers on failure. An actual
                    // Jump/Climb/DropDown success is NEVER inferred from body chronology.
                    if (mission.CurrentStepIndex == mission.Steps.Count - 1 &&
                        !HasActiveTraversalProbe(bot) && !IsTeleportTransactionActive(bot) &&
                        bot.GetPlayer?.MovementContext?.IsGrounded == true &&
                        !string.IsNullOrEmpty(bot.ProfileId) &&
                        cf28Previous != null && cf28Successor != null &&
                        cf28Previous.EpisodeId == cursor.LastCompletedEpisodeId &&
                        cf28Successor.EndBreadcrumbSequence > 0 &&
                        FollowerMovementRecoveryStates.TryGetValue(
                            bot.ProfileId, out FollowerMovementRecoveryState cf58RecoveryState) &&
                        cf58RecoveryState != null &&
                        !cf58RecoveryState.LearningV350TerminalRecoveryEligible)
                    {
                        int cf58PriorExhaustedIndex = mission.LearningV357ExhaustedBarrierStepIndex;
                        int cf58PriorExhaustedEdge = mission.LearningV357ExhaustedBarrierEdgeId;
                        int cf58PriorExhaustedEnd = mission.LearningV357ExhaustedBarrierEndBreadcrumbSequence;
                        float cf58PriorExhaustedAt = mission.LearningV357ExhaustedBarrierAt;
                        // Only V373's read-only proof runs with this temporary authority.
                        mission.LearningV357ExhaustedBarrierStepIndex = mission.CurrentStepIndex;
                        mission.LearningV357ExhaustedBarrierEdgeId = cf28Successor.EdgeId;
                        mission.LearningV357ExhaustedBarrierEndBreadcrumbSequence =
                            cf28Successor.EndBreadcrumbSequence;
                        mission.LearningV357ExhaustedBarrierAt = Time.time;
                        bool cf58PhysicalEpochRetired =
                            TryRetireLearningV373ExhaustedPhysicalDebtByDownstreamBodyEpoch(
                                bot, mission, "cf58ExactFarSidePredecessorWithDownstreamPathProof",
                                out int cf58RetiredEdgeId, out int cf58RetiredEpisodeId);
                        if (cf58PhysicalEpochRetired)
                        {
                            long cf58TraversalGeneration = cursor.LearningV375MovementGeneration;
                            // The route mission has advanced beyond its last step. Use V402's
                            // existing single-writer cleanup; the terminal latch is transient
                            // here because V373 already consumed execution-only debt.
                            cf58RecoveryState.LearningV350TerminalRecoveryEligible = true;
                            cf58RecoveryState.LearningV350TerminalRecoverySince = Time.time;
                            cf58RecoveryState.LearningV350TerminalRecoveryReason =
                                "cf58ExactFarSideExecutionOnlyRetirement";
                            bool cf58Yielded =
                                TryYieldLearningV402PersistentRouteTransactionToTerminalRecovery(
                                    bot, mission, cursor);
                            if (!cf58Yielded)
                            {
                                ClearLearningV365ContinuousPhysicalRoute(
                                    bot, "cf58FarSideRetirementFallbackProducerRelease");
                                FollowerMovementWriterAuthority.TransitionTraversalToFollowIfCurrent(
                                    bot, cf58TraversalGeneration,
                                    "cf58FarSideRetirementFallbackWriterRelease");
                            }
                            ClearLearningV351TerminalRecoveryState(
                                bot, "cf58ExactFarSideExecutionDebtRetiredByDownstreamBodyEpoch");
                            WriteEvent("cf58ExactFarSideTerminalExecutionDebtRetired", bot, new
                            {
                                missionId = mission.MissionId,
                                predecessorEdgeId = cf28Previous.EdgeId,
                                predecessorEpisodeId = cf28Previous.EpisodeId,
                                retiredEdgeId = cf58RetiredEdgeId,
                                retiredEpisodeId = cf58RetiredEpisodeId,
                                chainId = cf28Successor.ChainId,
                                cf58Yielded,
                                body = Vec(position),
                                successorProjection = cf28Projection,
                                successorSpan = cf28Span,
                                traversalSuccessRecorded = false,
                                nativeSuccessRecorded = false,
                                routeExecutionDebtRetired = true,
                                persistentLearningChanged = false,
                                bodyRelocated = false,
                                action = "verifiedNewerOperationalBodyRouteRetiresOnlyUnattemptedTerminalExecutorNotSemanticOrNativeTruth"
                            });
                            return;
                        }
                        mission.LearningV357ExhaustedBarrierStepIndex = cf58PriorExhaustedIndex;
                        mission.LearningV357ExhaustedBarrierEdgeId = cf58PriorExhaustedEdge;
                        mission.LearningV357ExhaustedBarrierEndBreadcrumbSequence = cf58PriorExhaustedEnd;
                        mission.LearningV357ExhaustedBarrierAt = cf58PriorExhaustedAt;
                    }

                    // CF32: the final same-chain DropDown executor debt is now physically
                    // inadmissible. The already-grounded body is beyond its exit, the original
                    // admission target lies behind the measured frontier, and a real predecessor
                    // completion has been tied to this exact mission/writer generation. Holding
                    // traversal authority until the generic *no-motion* watchdog fires causes
                    // a terminal stall even while the follower legitimately moves forwards.
                    // Yield this non-executable FINAL step via the existing V362/V385/V402
                    // terminal-recovery boundary; never record physical completion for it.
                    // Intermediate steps MUST remain owned by the normal actuator consumer.
                    if ((mission.CurrentStepIndex == mission.Steps.Count - 1) &&
                        !string.IsNullOrEmpty(bot.ProfileId) &&
                        FollowerMovementRecoveryStates.TryGetValue(
                            bot.ProfileId, out FollowerMovementRecoveryState cf32RecoveryState) &&
                        cf32RecoveryState != null &&
                        !cf32RecoveryState.LearningV350TerminalRecoveryEligible)
                    {
                        long cf32MovementGeneration = cursor.LearningV375MovementGeneration;
                        cf32RecoveryState.LearningV350TerminalRecoveryEligible = true;
                        cf32RecoveryState.LearningV350TerminalRecoverySince = Time.time;
                        cf32RecoveryState.LearningV350TerminalRecoveryReason =
                            "cf32SameChainFarSideFinalActuatorDebtInadmissible";
                        CaptureLearningV362TerminalRecoveryEpoch(
                            bot, cf32RecoveryState,
                            "cf32SameChainFarSideFinalActuatorDebtInadmissible");
                        if (TryYieldLearningV402PersistentRouteTransactionToTerminalRecovery(
                                bot, mission, cursor))
                        {
                            WriteEvent("cf32FarSideFinalDebtYieldedToFollowRecovery", bot, new
                            {
                                missionId = mission.MissionId,
                                stepIndex = mission.CurrentStepIndex,
                                predecessorEdgeId = cf28Previous.EdgeId,
                                predecessorEpisodeId = cf28Previous.EpisodeId,
                                successorEdgeId = cf28Successor.EdgeId,
                                successorEpisodeId = cf28Successor.EpisodeId,
                                chainId = cf28Previous.ChainId,
                                traversalGeneration = cf32MovementGeneration,
                                body = Vec(position),
                                discardedBackwardTarget = Vec(target),
                                bodyArc,
                                targetArc,
                                successorProjection = cf28Projection,
                                successorSpan = cf28Span,
                                nativeSuccessRecorded = false,
                                successorDebtConsumed = false,
                                traversalSuccessRecorded = false,
                                routeStepAdvanced = false,
                                persistentLearningChanged = false,
                                bodyRelocated = false,
                                action = "unexecutableTerminalActuatorDebtYieldsToCanonicalFollowRecoveryWithoutForgingLandingOrErasingSemanticDebt"
                            });
                            return;
                        }
                        // No partial terminal latch may survive a failed writer handoff.
                        ClearLearningV351TerminalRecoveryState(
                            bot, "cf32FarSideFinalDebtWriterHandoffNotCommitted");
                    }

                    // The old V453 target is behind the proven physical frontier. Preserve
                    // the current real-world body and the unresolved executor debt; let the
                    // existing bounded mission consumer/recovery decide the next action.
                    // Scrub only stale native mover bookkeeping, without StopMove/teleport.
                    if (cursor.LearningV375ProducerKind != "CF28FarSideSuccessorHold")
                    {
                        TryScrubLearningV364NativeMoverEpochAnchor(
                            bot, position, "cf28FarSideSuccessorTargetRejected",
                            out string cf28ScrubDetail);
                        if (ShouldEmitDiagnostic(
                            "cf28FarSideSuccessorTargetRejected:" +
                                mission.MissionId.ToString(CultureInfo.InvariantCulture) + ":" +
                                cf28Successor.EdgeId.ToString(CultureInfo.InvariantCulture), bot, 0.3f))
                        {
                            WriteEvent("cf28FarSideSuccessorTargetRejected", bot, new
                            {
                                missionId = mission.MissionId,
                                stepIndex = mission.CurrentStepIndex,
                                predecessorEdgeId = cf28Previous.EdgeId,
                                predecessorEpisodeId = cf28Previous.EpisodeId,
                                successorEdgeId = cf28Successor.EdgeId,
                                successorEpisodeId = cf28Successor.EpisodeId,
                                movementGeneration = cursor.LearningV375MovementGeneration,
                                body = Vec(position),
                                rejectedTarget = Vec(target),
                                bodyArc,
                                targetArc,
                                bestRouteArc = cursor.LearningV375BestRouteArcLength,
                                successorProjection = cf28Projection,
                                successorSpan = cf28Span,
                                grounded = true,
                                scrubDetail = cf28ScrubDetail,
                                traversalSuccessRecorded = false,
                                nativeSuccessRecorded = false,
                                actuatorDebtConsumed = false,
                                persistentLearningChanged = false,
                                bodyRelocated = false,
                                action = "sameChainGroundedFarSideBodyCannotReapproachUnprovenDropDownByBackwardMovement"
                            });
                        }
                    }
                    // Once the short anti-relatch timer expires, explain why we still
                    // reject the same stale physical target. Do not award execution truth.
                    if (Time.time > mission.Cf31FarSideProofRelatchExpiresAt &&
                        ShouldEmitDiagnostic(
                            "cf30MissionScopedFarSideHoldAfterRelatchExpiry:" +
                                mission.MissionId.ToString(CultureInfo.InvariantCulture) + ":" +
                                cf28Successor.EdgeId.ToString(CultureInfo.InvariantCulture),
                            bot, 30.0f))
                    {
                        WriteEvent("cf30MissionScopedFarSideHoldAfterRelatchExpiry", bot, new
                        {
                            missionId = mission.MissionId,
                            stepIndex = mission.CurrentStepIndex,
                            predecessorEdgeId = cf28Previous.EdgeId,
                            predecessorEpisodeId = cf28Previous.EpisodeId,
                            successorEdgeId = cf28Successor.EdgeId,
                            successorEpisodeId = cf28Successor.EpisodeId,
                            chainId = cf28Previous.ChainId,
                            movementGeneration = cursor.LearningV375MovementGeneration,
                            predecessorAge = Time.time - mission.Cf31FarSideProofCompletedAt,
                            antiRelatchExpiredBy = Time.time - mission.Cf31FarSideProofRelatchExpiresAt,
                            bodyArc,
                            targetArc,
                            grounded = true,
                            nativeSuccessRecorded = false,
                            successorDebtConsumed = false,
                            persistentLearningChanged = false,
                            bodyRelocated = false,
                            action = "sameMissionPhysicalPredecessorTruthOutlivesShortAntiRelatchTimerNeverPublishStaleBackwardDropDownTarget"
                        });
                    }
                    cursor.LearningV375ProducerKind = "CF28FarSideSuccessorHold";
                    return;
                }
            }

            try
            {
                // V2-RC2: route producer submits an exact typed intent to the SAME
                // authority. Neither a stale mission step nor an unrelated episode
                // may borrow the current Traversal writer token. No new executor.
                FollowerRouteMissionStep v2RouteStep =
                    mission.CurrentStepIndex >= 0 && mission.CurrentStepIndex < mission.Steps.Count
                        ? mission.Steps[mission.CurrentStepIndex] : null;
                var v2RouteIntent = new FollowerMovementV2IntentGateway.MovementIntent(
                    bot.ProfileId, raidGeneration, mission.MissionId, mission.CurrentStepIndex,
                    v2RouteStep?.EpisodeId ?? -1, v2RouteStep?.ChainId ?? -1,
                    cursor.LearningV375MovementGeneration,
                    FollowerMovementWriterAuthority.WriterDomain.Traversal,
                    FollowerMovementV2IntentGateway.MovementIntentKind.Navigation,
                    "V375CanonicalRoute");
                // V2-RC5: owner generation is read INDEPENDENTLY from the shared
                // authority state. No self-constructed live reference and no
                // duplicate offer/verification decision in this producer.
                FollowerMovementWriterAuthority.WriteScope v2RouteScope;
                bool v2RouteVerified = TryEnterCapabilityRouteProducerScope(
                    bot,mission,v2RouteStep,cursor,v2RouteIntent,out v2RouteScope,out string v2RouteReason);
                // A failed admission must not fall back to the legacy unscoped path.
                if (!v2RouteVerified && ShouldEmitDiagnostic(
                    "v2RouteIdentityRejected:" + mission.MissionId.ToString(CultureInfo.InvariantCulture), bot, 0.8f))
                {
                    WriteEvent("v2RouteIdentityRejected", bot, new
                    {
                        missionId = mission.MissionId,
                        stepIndex = mission.CurrentStepIndex,
                        movementGeneration = cursor.LearningV375MovementGeneration,
                        reason = v2RouteReason,
                        traversalSuccessRecorded = false,
                        bodyRelocated = false
                    });
                }
                if (!v2RouteVerified)
                    return;
                using (v2RouteScope)
                {
                    if ((learningV375ActuatorFrontierCapped) &&
                        ((!learningV377ActuatorPhysicalFrontierOwnsCurrentStep)) &&
                        mission.CurrentStepIndex >= 0 && mission.CurrentStepIndex < mission.Steps.Count)
                    {
                        FollowerRouteMissionStep actuatorStep = mission.Steps[mission.CurrentStepIndex];
                        Vector3 actuatorDirection = actuatorStep != null
                            ? GetFlatDirection(actuatorStep.RawExit - actuatorStep.RawEntry)
                            : Vector3.zero;
                        if (actuatorDirection.sqrMagnitude > 0.5f)
                        {
                            try { bot.Steering?.BotSteeringLookToDirection("R1-59C04306AFDB",@bot,actuatorDirection); } catch { }
                        }
                    }

                    if (bot.Mover != null)
                    {
                        bot.Mover.SetBotMoverPause("R1-CCCADB648BB8",@bot,false);
                        bot.Mover.LocalAvoidance?.GClass492DropOffset("R1-42E6ECC9EB2E",@bot);
                        bool learningV395LocomotionEvidenceAvailable = false;
                        bool learningV395PreferSprint = bot.Mover.Sprinting;
                        string learningV395DominantLocomotionState = string.Empty;
                        int learningV395SprintSegments = 0;
                        int learningV395RunSegments = 0;
                        float learningV395SprintArc = 0f;
                        float learningV395RunArc = 0f;
                        float learningV395ObservedMeanSpeed = 0f;
                        bool learningV395CurrentStepIsNonActuatorPassage = false;
                        FollowerRouteMissionStep learningV395CurrentStep =
                            mission.CurrentStepIndex >= 0 && mission.CurrentStepIndex < mission.Steps.Count
                                ? mission.Steps[mission.CurrentStepIndex]
                                : null;
                        LearningV354TraversalCapabilityProfile learningV395CurrentProfile =
                            learningV395CurrentStep != null
                                ? GetLearningV354ActionProfile(
                                    GetLearningV354SemanticKind(
                                        learningV395CurrentStep.EdgeId, learningV395CurrentStep.Kind))
                                : null;
                        learningV395CurrentStepIsNonActuatorPassage = learningV395CurrentProfile != null &&
                            !learningV395CurrentProfile.RequiresActuator &&
                            learningV395CurrentProfile.PhysicalPassageCanProveCompletion;
                        bool learningV395FallbackPresentationOwnsCurrentStep =
                            string.Equals(cursor.LearningV375ProducerKind, "PhysicalCorridor", StringComparison.Ordinal) ||
                            (cursor.LearningV393NavMeshStarvedMissionId == mission.MissionId &&
                             cursor.LearningV393NavMeshStarvedStepIndex == mission.CurrentStepIndex);
                        if (learningV395CurrentStepIsNonActuatorPassage &&
                            (learningV395FallbackPresentationOwnsCurrentStep ||
                             true) &&
                            !learningV377ActuatorPhysicalFrontierOwnsCurrentStep &&
                            (!learningV375ActuatorFrontierCapped ||
                             true))
                        {
                            learningV395LocomotionEvidenceAvailable = TryGetLearningV395RouteLocomotionEvidence(
                                mission, bodyArc, targetArc,
                                out learningV395PreferSprint,
                                out learningV395DominantLocomotionState,
                                out learningV395SprintSegments,
                                out learningV395RunSegments,
                                out learningV395SprintArc,
                                out learningV395RunArc,
                                out learningV395ObservedMeanSpeed);
                        }
                        bool learningV396LocomotionIntentPublished = false;
                        string learningV396LocomotionIntentResult = "noEvidence";
                        if (learningV395LocomotionEvidenceAvailable)
                        {
                            learningV396LocomotionIntentPublished = PublishLearningV396RouteLocomotionIntent(
                                bot, mission, learningV395CurrentStep, learningV395PreferSprint,
                                out learningV396LocomotionIntentResult);
                        }
                        bool learningV405SprintRestored = false;
                        bool learningV405MeasuredForwardProgress = false;
                        float learningV405ForwardDot = 0f;
                        float speed = bot.Mover.Sprinting
                            ? ProbeMomentumFullMoveSpeed
                            : LearningV357RouteEnvelopeRunSpeed;
                        bool learningV405IntentApplied = learningV395CurrentStepIsNonActuatorPassage &&
                            !learningV377ActuatorPhysicalFrontierOwnsCurrentStep &&
                            ApplyLearningV405CanonicalNonActuatorRouteMovementIntent(
                                bot, mission, cursor, learningV395CurrentStep, learningV395CurrentProfile,
                                learningV395LocomotionEvidenceAvailable, learningV395PreferSprint,
                                learningV395DominantLocomotionState, learningV395ObservedMeanSpeed,
                                learningV376BodySampleDistance, learningV377BodyDelta,
                                targetTangent.sqrMagnitude > 0.25f ? targetTangent : bodyTangent, target,
                                learningV405TargetCapped, learningV405OriginalTargetArc,
                                learningV405StepExitArc, learningV405CappedTargetArc,
                                out learningV405SprintRestored, out learningV405MeasuredForwardProgress,
                                out learningV405ForwardDot, out speed);
                        if (!learningV405IntentApplied)
                        {
                            // Historical V3.96 fallback remains for non-V405 paths. It expresses speed
                            // only through the Mover's current state and never toggles Sprint.
                            bool currentSprint = bot.Mover.Sprinting;
                            speed = currentSprint ? ProbeMomentumFullMoveSpeed : LearningV357RouteEnvelopeRunSpeed;
                        }
                        bot.BotOwnerSetTargetMoveSpeed("R1-66371669EE00",@bot,speed);
                        bot.Mover.BotMoverSetTargetMoveSpeed("R1-28A44958D276",@bot,speed);
                        if (learningV395LocomotionEvidenceAvailable && learningV395PreferSprint &&
                            bot.Mover.Sprinting)
                        {
                            TryArmOrRefreshLearningV400MeasuredRollingPassageSprintLease(
                                bot, mission, cursor, learningV395CurrentStep, true,
                                learningV376BodySampleDistance, learningV377BodyDelta,
                                learningV405SprintRestored
                                    ? "learningV405CanonicalIntentRestoredMeasuredSprint"
                                    : "persistentPhysicalCorridorMeasuredProgress");
                        }
                        bool sprint = bot.Mover.Sprinting;
                        if (learningV395LocomotionEvidenceAvailable &&
                            ShouldEmitDiagnostic(
                                "learningV395RouteLocomotionEvidenceApplied:" +
                                    mission.MissionId.ToString(CultureInfo.InvariantCulture),
                                bot, 0.35f))
                        {
                            WriteEvent("learningV395RouteLocomotionEvidenceApplied", bot, new
                            {
                                missionId = mission.MissionId,
                                stepIndex = mission.CurrentStepIndex,
                                edgeId = learningV395CurrentStep?.EdgeId ?? 0,
                                kind = learningV395CurrentStep?.Kind ?? string.Empty,
                                bodyArc,
                                targetArc,
                                dominantState = learningV395DominantLocomotionState,
                                sprintSegments = learningV395SprintSegments,
                                runSegments = learningV395RunSegments,
                                sprintArc = learningV395SprintArc,
                                runArc = learningV395RunArc,
                                observedMeanSpeed = learningV395ObservedMeanSpeed,
                                requestedSprint = learningV395PreferSprint,
                                moverSprinting = sprint,
                                targetMoveSpeed = speed,
                                chainId = learningV395CurrentStep?.ChainId ?? 0,
                                locomotionIntentPublished = learningV396LocomotionIntentPublished,
                                locomotionIntentResult = learningV396LocomotionIntentResult,
                                learningV405IntentApplied,
                                learningV405SprintRestored,
                                learningV405MeasuredForwardProgress,
                                learningV405ForwardDot,
                                learningV405TargetCapped,
                                traversalSuccessRecorded = false,
                                persistentLearningChanged = false,
                                action = learningV405IntentApplied
                                    ? "routeLocomotionEvidenceConsumedByCanonicalNonActuatorSingleExecutor"
                                    : "routeLocomotionEvidencePublishesChainIntentWithoutGenericSprintStateAuthority"
                            });
                        }
                        StrictPathResult path = CalculateStrictPath(position, target);
                        bool learningV377NavMeshAccepted = IsLearningV377NavMeshProducerForwardCompatible(
                            mission, position, target, path,
                            Mathf.Max(bodyArc, cursor.LearningV375BestRouteArcLength),
                            targetTangent.sqrMagnitude > 0.25f ? targetTangent : bodyTangent,
                            out float learningV377AllowedPathLength,
                            out float learningV377FirstCornerArc,
                            out float learningV377FirstCornerForwardDot,
                            out string learningV377NavMeshReason);
                        bool learningV393NavMeshStarvationLatched =
                            (cursor.LearningV393NavMeshStarvedMissionId == mission.MissionId) &&
                            cursor.LearningV393NavMeshStarvedStepIndex == mission.CurrentStepIndex;
                        bool learningV393NavMeshFailedToProduceProgress =
                            (!learningV393NavMeshStarvationLatched) &&
                            string.Equals(cursor.LearningV375ProducerKind, "NavMesh", StringComparison.Ordinal) &&
                            learningV376BodySampleDistance <= 0.01f &&
                            bot.Mover?.IsMoving != true;
                        if (learningV393NavMeshFailedToProduceProgress)
                        {
                            cursor.LearningV393NavMeshStarvedMissionId = mission.MissionId;
                            cursor.LearningV393NavMeshStarvedStepIndex = mission.CurrentStepIndex;
                            cursor.LearningV395NavMeshStarvedRouteArc = bodyArc;
                            learningV393NavMeshStarvationLatched = true;
                            WriteEvent("learningV393NavMeshProducerLivenessRevoked", bot, new
                            {
                                missionId = mission.MissionId,
                                stepIndex = mission.CurrentStepIndex,
                                generation = cursor.LearningV375MovementGeneration,
                                previousProducer = "NavMesh",
                                bodyArc,
                                bestRouteArc = cursor.LearningV375BestRouteArcLength,
                                targetArc,
                                bodySampleDistance = learningV376BodySampleDistance,
                                physicalFrontierSampleEpsilon = 0.01f,
                                measuredPhysicalSpeed = cursor.LearningV375PhysicalSpeed,
                                moverMoving = bot.Mover?.IsMoving == true,
                                moverHavePath = bot.Mover?.ActualPathController?.HavePath == true,
                                navMeshAccepted = learningV377NavMeshAccepted,
                                navMeshReason = learningV377NavMeshReason,
                                traversalSuccessRecorded = false,
                                persistentLearningChanged = false,
                                bodyRelocated = false,
                                action = "acceptedNavigationBackendWithoutMeasuredBodyProgressLosesCurrentStepMovementAuthority"
                            });
                        }
                        bool learningV395NavMeshRequalified = false;
                        string learningV395MoverRetireDetail = string.Empty;
                        if ((learningV393NavMeshStarvationLatched) &&
                            learningV395CurrentStepIsNonActuatorPassage &&
                            !learningV395PhysicalBodyRouteRegression &&
                            learningV377NavMeshAccepted &&
                            cursor.LearningV395NavMeshStarvedRouteArc >= 0f &&
                            bodyArc >= cursor.LearningV395NavMeshStarvedRouteArc + RouteMissionBreadcrumbReachDistance)
                        {
                            bool retiredMover = TryRetireNativeMoverPathAtCurrentBody(
                                bot, "learningV395NavMeshProducerRequalification",
                                out learningV395MoverRetireDetail);
                            if (retiredMover)
                            {
                                cursor.LearningV393NavMeshStarvedMissionId = 0;
                                cursor.LearningV393NavMeshStarvedStepIndex = -1;
                                cursor.LearningV395NavMeshStarvedRouteArc = -1f;
                                cursor.LearningV395NavMeshRequalificationCount++;
                                learningV393NavMeshStarvationLatched = false;
                                // The only route-regression evidence at this point may be the stale
                                // mover topology we just retired. Actual physical-body regression
                                // remains an unconditional veto above.
                                learningV377RouteRegressionInvalidatesProducer = false;
                                learningV395NavMeshRequalified = true;
                                WriteEvent("learningV395NavMeshProducerRequalified", bot, new
                                {
                                    missionId = mission.MissionId,
                                    stepIndex = mission.CurrentStepIndex,
                                    edgeId = learningV395CurrentStep?.EdgeId ?? 0,
                                    kind = learningV395CurrentStep?.Kind ?? string.Empty,
                                    generation = cursor.LearningV375MovementGeneration,
                                    bodyArc,
                                    targetArc,
                                    bestRouteArc = cursor.LearningV375BestRouteArcLength,
                                    requiredPhysicalProgress = RouteMissionBreadcrumbReachDistance,
                                    navMeshReason = learningV377NavMeshReason,
                                    moverRetireDetail = learningV395MoverRetireDetail,
                                    requalificationCount = cursor.LearningV395NavMeshRequalificationCount,
                                    traversalSuccessRecorded = false,
                                    persistentLearningChanged = false,
                                    bodyRelocated = false,
                                    action = "measuredPhysicalFallbackProgressMayRequalifyFreshForwardNavMeshFromCurrentBodyEpoch"
                                });
                            }
                        }
                        bool learningV406AtomicPhysicalExecutorApplied = false;
                        string learningV406AtomicPhysicalExecutorDetail = "notEligible";
                        if (learningV405IntentApplied && learningV395CurrentStepIsNonActuatorPassage &&
                            !learningV377ActuatorPhysicalFrontierOwnsCurrentStep)
                        {
                            learningV406AtomicPhysicalExecutorApplied =
                                ExecuteLearningV406AtomicCanonicalNonActuatorRouteMovementIntent(
                                    bot, mission, cursor, learningV395CurrentStep, learningV395CurrentProfile,
                                    position, target,
                                    targetTangent.sqrMagnitude > 0.25f ? targetTangent : bodyTangent,
                                    learningV395LocomotionEvidenceAvailable, learningV395PreferSprint,
                                    learningV395DominantLocomotionState, learningV395ObservedMeanSpeed, speed,
                                    learningV405TargetCapped, learningV405OriginalTargetArc,
                                    learningV405StepExitArc, learningV405CappedTargetArc,
                                    out learningV406AtomicPhysicalExecutorDetail);
                        }

                        // A selected canonical producer may decline safely, but must never
                        // revive an older NavMesh/NoWay producer after its failed admission.
                        if(FollowerCapabilityRouteContinuationPolicy.ResolveCanonical(
                            (learningV405IntentApplied)&&
                                learningV395CurrentStepIsNonActuatorPassage&&!learningV377ActuatorPhysicalFrontierOwnsCurrentStep,
                            learningV406AtomicPhysicalExecutorApplied)==FollowerCapabilityRouteContinuationPolicy.CanonicalReceipt.Rejected)
                        {
                            RecordCapabilityRouteContinuationRejection(bot,mission,learningV395CurrentStep,
                                CapabilityContinuationPhase.CanonicalRoute,CapabilityContinuationFailure.ProducerDeclined,
                                learningV406AtomicPhysicalExecutorDetail);
                            return;
                        }

                        bool learningV378ActuatorPhysicalStaging =
                            FollowerCapabilityHelperRules.UpdateLearningV375PersistentExecutablePhysicalRouteTransaction_learningV378ActuatorPhysicalStaging_CC9769FF_3(learningV377ActuatorPhysicalFrontierOwnsCurrentStep,learningV377ActuatorReady,learningV377ActuatorProducerTargetAvailable);
                        if (learningV406AtomicPhysicalExecutorApplied)
                        {
                            // V406 already executed the exact current non-actuator passage using
                            // one collision-active canonical backend. Do not also install NavMesh
                            // or NoWay translation in this frame.
                        }
                        else if (learningV378ActuatorPhysicalStaging)
                        {
                            // The final actuator approach is a short physical lane, not a navigation
                            // destination. V3.77 showed a stable dead-zone where BotMover considered
                            // a ~sub-meter staging target reached while the actuator readiness plane
                            // still required more body progress. Drive this bounded lane with the same
                            // collision-active physical primitive already used by empirical routes.
                            Player stagingPlayer = bot.GetPlayer ?? bot.AIData?.Player;
                            Vector3 stagingDelta = target - position;
                            stagingDelta.y = 0f;
                            float stagingDistance = stagingDelta.magnitude;
                            if (stagingPlayer?.MovementContext != null && stagingDistance > 0.001f)
                            {
                                Vector3 stagingDirection = stagingDelta / stagingDistance;
                                float stagingDt = Mathf.Max(0.001f, Time.deltaTime);
                                bool learningV436JumpLocomotionContinuity =
                                    (string.Equals(
                                        learningV377ActuatorKind, "Jump",
                                        StringComparison.OrdinalIgnoreCase));
                                bool learningV438BoundaryLocomotionLease =
                                    (true);
                                bool learningV438ObservedSprint =
                                    bot.Mover?.Sprinting == true ||
                                    stagingPlayer.CurrentStateName == EPlayerState.Sprint;
                                bool learningV438RouteSprintEvidence =
                                    learningV395LocomotionEvidenceAvailable && learningV395PreferSprint;
                                bool learningV438SprintIntent = FollowerCapabilityHelperRules.UpdateLearningV375PersistentExecutablePhysicalRouteTransaction_learningV438SprintIntent_CC9769FF_4(learningV438BoundaryLocomotionLease,learningV438ObservedSprint,learningV438RouteSprintEvidence);
                                float learningV438MoveSpeed = learningV438SprintIntent
                                    ? ProbeMomentumFullMoveSpeed
                                    : ProbeMomentumNormalMoveSpeed;
                                // V2-RC41: a physically verified, short SAME-BODY
                                // ground rejoin is no longer an airborne actuator
                                // run-up. V3.77/CF77 already proved its strict current
                                // NavMesh corridor, but the pathless physical input
                                // was issued against an unrelated EFT heading and
                                // remained stationary. Point the EXISTING Traversal
                                // producer at the ground-rejoin target before Move;
                                // never mint Follow authority or infer a DropDown.
                                bool rc41StrictSupportedGroundRejoinHeading =
                                    FollowerCapabilityHelperRules.UpdateLearningV375PersistentExecutablePhysicalRouteTransaction_rc41StrictSupportedGroundRejoinHeading_CC9769FF_5(learningV377ActuatorFrontierSource,learningV377NavMeshAccepted,learningV377RouteRegressionInvalidatesProducer) &&
                                    path?.StrictComplete == true &&
                                    IsEffectivelySupportedForProbe(bot, stagingPlayer);
                                if (rc41StrictSupportedGroundRejoinHeading)
                                {
                                    try { bot.Steering?.BotSteeringLookToDirection("R1-289CE70E60FF",@bot,stagingDirection); } catch { }
                                }
                                ApplyCollisionActivePhysicalRouteMotion(
                                    bot, stagingPlayer, stagingDirection,
                                    stagingDirection * Mathf.Min(
                                        stagingDistance, ChainLocalSuccessorBridgeSpeed * stagingDt),
                                    stagingDt, 1f,
                                    sprint: learningV438SprintIntent,
                                    targetMoveSpeed: learningV438MoveSpeed,
                                    preserveSprintMode: learningV438BoundaryLocomotionLease,deliveryTarget:target,deliveryPurpose:CapabilityMotionPurpose.PreActuator);
                                cursor.LearningV375ProducerKind = "ActuatorPhysicalPreEntry";
                                if (learningV438BoundaryLocomotionLease && ShouldEmitDiagnostic(
                                        "learningV438ActuatorBoundaryLocomotionLease:" +
                                            learningV377ActuatorEdgeId.ToString(CultureInfo.InvariantCulture),
                                        bot, 0.18f))
                                {
                                    WriteEvent("learningV438ActuatorBoundaryLocomotionLeasePreserved", bot, new
                                    {
                                        missionId = mission.MissionId,
                                        stepIndex = mission.CurrentStepIndex,
                                        edgeId = learningV377ActuatorEdgeId,
                                        kind = learningV377ActuatorKind,
                                        state = stagingPlayer.CurrentStateName.ToString(),
                                        moverSprinting = bot.Mover?.Sprinting == true,
                                        routeLocomotionEvidence = learningV395LocomotionEvidenceAvailable,
                                        routeDominantState = learningV395DominantLocomotionState ?? string.Empty,
                                        routeRequestedSprint = learningV438RouteSprintEvidence,
                                        routeObservedMeanSpeed = learningV395ObservedMeanSpeed,
                                        targetMoveSpeed = learningV438MoveSpeed,
                                        target = Vec(target),
                                        stagingDistance,
                                        nativeSprintMutationSuppressed = true,
                                        traversalSuccessRecorded = false,
                                        persistentLearningChanged = false,
                                        bodyRelocated = false,
                                        action = "pathlessActuatorBoundaryKeepsExistingNativeLocomotionModeUntilExactActuatorOwnsBody"
                                    });
                                }
                            }
                            else if (bot.Mover != null)
                            {
                                bot.Mover.BotMoverGoToPointNoWay("R1-F8CC28C97B6F",@bot,target);
                                cursor.LearningV375ProducerKind = "ActuatorPhysicalPreEntryFallback";
                            }
                        }
                        else if (learningV377NavMeshAccepted && !learningV377RouteRegressionInvalidatesProducer &&
                                 !learningV393NavMeshStarvationLatched)
                        {
                            bot.BotOwnerGoToPoint("R1-9081F9C1057B",@bot,target,true,-1f,false,false);
                            cursor.LearningV375ProducerKind = "NavMesh";
                        }
                        else
                        {
                            if (learningV377RouteRegressionInvalidatesProducer ||
                                !learningV377ActiveMoverForwardCompatible ||
                                learningV393NavMeshStarvationLatched)
                            {
                                // Exactly the V3.64 physical-navigation epoch rule, now applied to
                                // the persistent route producer: scrub stale native mover bookkeeping
                                // from the CURRENT body epoch, never relocate or hard-brake the body.
                                // The collision-active NoWay producer below replaces navigation as
                                // the executable backend in this same update.
                                TryScrubLearningV364NativeMoverEpochAnchor(
                                    bot, position, "learningV377PersistentProducer",
                                    out string learningV377ScrubDetail);
                                if (ShouldEmitDiagnostic(
                                        "learningV377PersistentProducerPhysicalEpochRebased:" +
                                            mission.MissionId.ToString(CultureInfo.InvariantCulture),
                                        bot, 0.18f))
                                {
                                    WriteEvent("learningV377PersistentProducerPhysicalEpochRebased", bot, new
                                    {
                                        missionId = mission.MissionId,
                                        generation = cursor.LearningV375MovementGeneration,
                                        position = Vec(position),
                                        target = Vec(target),
                                        currentRouteArc = bodyArc,
                                        bestRouteArc = cursor.LearningV375BestRouteArcLength,
                                        activeMoverReason = learningV377ActiveMoverReason,
                                        navMeshReason = learningV377NavMeshReason,
                                        navMeshLivenessRevoked = learningV393NavMeshStarvationLatched,
                                        scrubDetail = learningV377ScrubDetail,
                                        traversalSuccessRecorded = false,
                                        persistentLearningChanged = false,
                                        bodyRelocated = false,
                                        action = "discardBackwardNativeMoverTopologyThenContinueFromCurrentPhysicalBodyEpoch"
                                    });
                                }
                            }
                            bot.Mover.BotMoverGoToPointNoWay("R1-84FD1AAE3AB4",@bot,target);
                            cursor.LearningV375ProducerKind = "PhysicalCorridor";
                        }

                        if ((ShouldEmitDiagnostic(
                                "learningV377PersistentProducerDecision:" +
                                    mission.MissionId.ToString(CultureInfo.InvariantCulture),
                                bot, 0.25f)))
                        {
                            WriteEvent("learningV377PersistentProducerDecision", bot, new
                            {
                                missionId = mission.MissionId,
                                generation = cursor.LearningV375MovementGeneration,
                                position = Vec(position),
                                target = Vec(target),
                                bodyArc,
                                bestRouteArc = cursor.LearningV375BestRouteArcLength,
                                routeRegressionInvalidated = learningV377RouteRegressionInvalidatesProducer,
                                activeMoverForwardCompatible = learningV377ActiveMoverForwardCompatible,
                                activeMoverReason = learningV377ActiveMoverReason,
                                strictComplete = path?.StrictComplete == true,
                                strictPathLength = path != null ? path.PathLength : 0f,
                                allowedPathLength = learningV377AllowedPathLength,
                                firstMeaningfulCornerArc = learningV377FirstCornerArc,
                                firstMeaningfulCornerForwardDot = learningV377FirstCornerForwardDot,
                                navMeshAccepted = learningV377NavMeshAccepted &&
                                    !learningV377RouteRegressionInvalidatesProducer &&
                                    !learningV393NavMeshStarvationLatched,
                                navMeshReason = learningV377NavMeshReason,
                                navMeshLivenessRevoked = learningV393NavMeshStarvationLatched,
                                navMeshRequalified = learningV395NavMeshRequalified,
                                learningV406AtomicPhysicalExecutorApplied,
                                learningV406AtomicPhysicalExecutorDetail,
                                routeLocomotionEvidence = learningV395LocomotionEvidenceAvailable,
                                routeLocomotionState = learningV395DominantLocomotionState,
                                routeRequestedSprint = learningV395LocomotionEvidenceAvailable && learningV395PreferSprint,
                                selectedProducer = cursor.LearningV375ProducerKind,
                                actuatorFrontierSource = learningV377ActuatorFrontierSource,
                                traversalSuccessRecorded = false,
                                persistentLearningChanged = false,
                                action = learningV378ActuatorPhysicalStaging
                                    ? "actuatorPreEntryUsesCollisionActivePhysicalProducerNavMeshIsAdvisoryOnly"
                                    : "navigationBackendMustAgreeWithMonotonicPhysicalRouteFrontier"
                            });
                        }
                    }
                }
                cursor.LearningV375ProducerTarget = target;
                cursor.LearningV375RouteTangent = targetTangent.sqrMagnitude > 0.25f ? targetTangent : bodyTangent;
                WriteEvent("learningV375ContinuousRouteProducerBridged", bot, new
                {
                    chainId = cursor.LearningV375TransactionChainId,
                    missionId = mission.MissionId,
                    generation = cursor.LearningV375MovementGeneration,
                    routeArc = cursor.LearningV375BestRouteArcLength,
                    targetArc,
                    target = Vec(target),
                    tangent = Vec(cursor.LearningV375RouteTangent),
                    producer = cursor.LearningV375ProducerKind,
                    actuatorFrontierCapped = learningV375ActuatorFrontierCapped,
                    cappedEdgeId = learningV375CappedEdgeId,
                    cappedKind = learningV375CappedKind,
                    action = learningV375ActuatorFrontierCapped
                        ? "approachRequiredActuatorFrontierWithoutDrivingThroughUnownedActionNode"
                        : "fillActionBoundaryWithoutDestroyingPersistentRouteWriter"
                });
            }
            catch (Exception ex)
            {
                if (ShouldEmitDiagnostic("learningV375ContinuousRouteProducerBridgeFailed", bot, 0.35f))
                {
                    WriteEvent("learningV375ContinuousRouteProducerBridgeFailed", bot, new
                    {
                        chainId = cursor.LearningV375TransactionChainId,
                        generation = cursor.LearningV375MovementGeneration,
                        error = ex.GetType().Name + ": " + ex.Message
                    });
                }
            }
        }

        private static bool TryGetLearningV375CanonicalSuccessorArcAuthority(
            ProbeRuntime probe,
            LearnedTraversalEdge nextEdge,
            out float forwardOffset,
            out float bodyArc,
            out float nextArc)
        {
            forwardOffset = 0f;
            bodyArc = 0f;
            nextArc = 0f;
            if ((probe?.Bot == null) || nextEdge == null ||
                !TryResolveLearningV376MissionForProbe(probe, out FollowerRouteMission mission, out _))
            {
                return false;
            }

            int currentIndex = -1;
            int nextIndex = -1;
            for (int index = 0; index < mission.Steps.Count; index++)
            {
                FollowerRouteMissionStep candidate = mission.Steps[index];
                if (candidate == null)
                {
                    continue;
                }
                if (currentIndex < 0 &&
                    (candidate.EpisodeId == probe.TraversalEpisodeId || candidate.EdgeId == probe.LearnedEdgeId))
                {
                    currentIndex = index;
                    continue;
                }
                if (currentIndex >= 0 && index > currentIndex && candidate.EdgeId == nextEdge.Id)
                {
                    nextIndex = index;
                    break;
                }
            }
            if (currentIndex < 0 || nextIndex <= currentIndex)
            {
                return false;
            }

            FollowerRouteMissionStep successor = mission.Steps[nextIndex];
            if (!TryProjectLearningV375RouteArc(
                    mission, probe.Player.Position,
                    out bodyArc, out _, out _, out _) ||
                !TryGetLearningV375SequenceArc(
                    mission,
                    successor.StartBreadcrumbSequence > 0
                        ? successor.StartBreadcrumbSequence
                        : successor.EndBreadcrumbSequence,
                    out nextArc, out _))
            {
                return false;
            }
            forwardOffset = nextArc - bodyArc;
            WriteEvent("learningV375CanonicalSuccessorArcAuthority", probe.Bot, new
            {
                edgeId = probe.LearnedEdgeId,
                nextEdgeId = nextEdge.Id,
                chainId = probe.TraversalChainId,
                currentStepIndex = currentIndex,
                nextStepIndex = nextIndex,
                bodyArc,
                nextArc,
                forwardOffset,
                action = "routePolylineOrderOutranksPredecessorRawAxis"
            });
            return true;
        }

        internal static bool TryDriveChainLocalSuccessorBridge(
            BotOwner bot,
            Vector3 successorEntry,
            int successorEdgeId)
        {
            string profileId = bot?.ProfileId ?? string.Empty;
            Player player = bot?.GetPlayer;
            if (string.IsNullOrEmpty(profileId) || player?.MovementContext == null ||
                !IsFinite(successorEntry) ||
                !ChainHandoffAuthorityGuards.TryGetValue(profileId, out ChainHandoffAuthorityGuard guard) ||
                guard == null || guard.Generation != raidGeneration || Time.time > guard.ExpiresAt ||
                (guard.NextEdgeId > 0 && successorEdgeId > 0 && guard.NextEdgeId != successorEdgeId))
            {
                return false;
            }

            if (true)
            {
                LearnedTraversalEdge successorEdge = LearnedTraversalEdges.FirstOrDefault(candidate =>
                    candidate != null && candidate.Id == successorEdgeId);
                bool successorConsumed = successorEdge != null && IsTraversalEpisodeAlreadyConsumed(bot, successorEdge);
                bool haveBridgeMission = FollowerRouteMissions.TryGetValue(
                    profileId, out FollowerRouteMission bridgeMission) &&
                    bridgeMission != null && bridgeMission.Steps != null;
                bool learningV376MissionStillOwnsEquivalentSuccessor =
                    true && haveBridgeMission &&
                    bridgeMission.Steps.Skip(Math.Max(0, bridgeMission.CurrentStepIndex))
                        .Any(step => IsLearningV376RouteStepEquivalentToEdgeId(step, successorEdgeId));
                if(FollowerCapabilityRouteContinuationPolicy.ReserveLegacyBridge(
                    learningV376MissionStillOwnsEquivalentSuccessor&&TryGetActiveFollowerRouteMission(bot,out var canonicalMission)&&
                    ReferenceEquals(canonicalMission,bridgeMission)))
                {
                    if(ShouldEmitDiagnostic("capabilityCanonicalRouteBridgeReserved",bot,.75f))
                        WriteEvent("capabilityCanonicalRouteBridgeReserved",bot,new{missionId=bridgeMission.MissionId,
                            stepIndex=bridgeMission.CurrentStepIndex,successorEdgeId,legacyBridgePublished=false,
                            nativeSuccessRecorded=false,bodyRelocated=false});
                    return true; // Handled by the canonical mission, not a physical success receipt.
                }
                bool missionPastSuccessor = haveBridgeMission && !learningV376MissionStillOwnsEquivalentSuccessor;
                if ((successorConsumed || missionPastSuccessor) && !learningV376MissionStillOwnsEquivalentSuccessor)
                {
                    ChainHandoffAuthorityGuards.Remove(profileId);
                    RetireChainGuardToFollowAtOrigin(bot, guard, "learningV348ChainBridgeClosedByPhysicalRouteProof");
                    WriteEvent("learningV348ChainBridgeClosedByPhysicalRouteProof", bot, new
                    {
                        completedEdgeId = guard.CompletedEdgeId, nextEdgeId = successorEdgeId,
                        chainId = guard.ChainId, successorConsumed, missionPastSuccessor,
                        learningV376MissionStillOwnsEquivalentSuccessor,
                        teleportUsed = false, persistentLearningChanged = false
                    });
                    return false;
                }
                if (learningV376MissionStillOwnsEquivalentSuccessor &&
                    ShouldEmitDiagnostic("learningV376LegacyBridgeRetained:" + successorEdgeId, bot, 0.35f))
                {
                    WriteEvent("learningV376LegacyChainBridgeRetainedByPersistentMissionSuccessor", bot, new
                    {
                        completedEdgeId = guard.CompletedEdgeId,
                        requestedSuccessorEdgeId = successorEdgeId,
                        missionId = bridgeMission.MissionId,
                        currentStepIndex = bridgeMission.CurrentStepIndex,
                        chainId = guard.ChainId,
                        successorConsumed,
                        action = "canonicalOrProvenParentMissionSuccessorKeepsPersistentTraversalWriterAlive"
                    });
                }
            }

            Vector3 current = player.Position;
            Vector3 flat = successorEntry - current;
            flat.y = 0f;
            float distance = flat.magnitude;
            if (distance > ChainLocalSuccessorBridgeMaxDistance)
            {
                return false;
            }

            EPlayerState currentState = player.CurrentStateName;
            string state = currentState.ToString();
            string completedSemanticKind = GetLearningV354SemanticKind(guard.CompletedEdgeId, guard.CompletedKind);
            bool completedRequiresLandingProof =
                GetLearningV354ActionProfile(completedSemanticKind).RequiresLandingProof;

            if (!guard.LocalBridgeActive)
            {
                guard.LocalBridgeActive = true;
                guard.LocalBridgeStartedAt = Time.time;
                guard.LocalBridgeDeadlineAt = Time.time + ChainLocalSuccessorBridgeAbsoluteTimeout;
                guard.LocalBridgeLastDistance = distance;
                guard.LocalBridgeLastProgressAt = Time.time;
                guard.NextLocalBridgeLogAt = 0f;
                guard.ExpiresAt = Mathf.Max(guard.ExpiresAt, guard.LocalBridgeDeadlineAt);
                WriteEvent("chainLocalSuccessorBridgeStarted", bot, new
                {
                    completedEdgeId = guard.CompletedEdgeId,
                    completedKind = guard.CompletedKind,
                    nextEdgeId = successorEdgeId,
                    chainId = guard.ChainId,
                    position = Vec(current),
                    successorEntry = Vec(successorEntry),
                    distance,
                    state,
                    deadlineSeconds = ChainLocalSuccessorBridgeAbsoluteTimeout
                });
            }

            bool nativeLandingOwnsBody = completedRequiresLandingProof &&
                Time.time - guard.LocalBridgeStartedAt <= ChainLocalSuccessorBridgeJumpLandingWait &&
                (currentState == EPlayerState.Transition || IsProbeTraversalState(currentState));

            StrictPathResult learningV357BridgePath = true
                ? CalculateStrictPath(current, successorEntry)
                : null;
            bool learningV357NavBridge = learningV357BridgePath != null && learningV357BridgePath.StrictComplete;
            try
            {
                if (true)
                {
                    if (bot.Mover != null)
                    {
                        bot.Mover.LocalAvoidance?.GClass492DropOffset("R1-DAFAC66ADC52",@bot);
                        bool preserveSprint = false;
                        if (true &&
                            TraversalChainLocomotionIntents.TryGetValue(profileId, out TraversalChainLocomotionIntent learningV370Intent) &&
                            learningV370Intent != null && learningV370Intent.ChainId == guard.ChainId &&
                            Time.time <= learningV370Intent.ExpiresAt)
                        {
                            preserveSprint = learningV370Intent.Sprinting;
                        }
                        if (preserveSprint)
                        {
                            if (!bot.Mover.Sprinting) bot.Mover.BotMoverSprint("R1-D71B4C47D42F",@bot,true,false);
                        }
                        else if (bot.Mover.Sprinting && !true)
                        {
                            bot.Mover.BotMoverSprint("R1-53DC253E1092",@bot,false,false);
                        }
                        float bridgeSpeed = preserveSprint
                            ? ProbeMomentumFullMoveSpeed
                            : LearningV357RouteEnvelopeRunSpeed;
                        bot.BotOwnerSetTargetMoveSpeed("R1-15AB3AA3407F",@bot,bridgeSpeed);
                        bot.Mover.BotMoverSetTargetMoveSpeed("R1-27782AED65F5",@bot,bridgeSpeed);
                        bot.Mover.SetBotMoverPause("R1-69917DB0E843",@bot,!learningV357NavBridge);
                        if (learningV357NavBridge)
                        {
                            bot.Mover.BotMoverGoToPointNoWay("R1-1733C5C934EB",@bot,successorEntry);
                        }
                    }
                    player.SetPlayerInputDirection("R1-A0E6D9096910",@bot,Vector2.up);
                    player.MovementContext.SetMovementContextMovementDirection("R1-58708843BDA1",@bot,Vector2.up);
                    if (flat.sqrMagnitude > 0.001f)
                    {
                        bot.Steering?.BotSteeringLookToDirection("R1-3CF4FCD0ED85",@bot,flat.normalized);
                    }
                }
                else
                {
                    bot.Mover?.BotMoverStop("R1-3F0BCC29D5A1",@bot);
                    if (bot.Mover != null)
                    {
                        bot.Mover.SetBotMoverPause("R1-E4C91895C079",@bot,true);
                        bot.Mover.LocalAvoidance?.GClass492DropOffset("R1-E5C9A88DF74C",@bot);
                    }
                    player.SetPlayerInputDirection("R1-079E726753AC",@bot,Vector2.zero);
                    player.MovementContext.SetMovementContextMovementDirection("R1-54A53C13923D",@bot,Vector2.zero);
                }
            }
            catch
            {
            }

            if (distance <= ChainLocalSuccessorBridgeStopDistance)
            {
                if (guard.NextLocalBridgeLogAt <= Time.time)
                {
                    guard.NextLocalBridgeLogAt = Time.time + 0.40f;
                    WriteEvent("chainLocalSuccessorBridgeReady", bot, new
                    {
                        completedEdgeId = guard.CompletedEdgeId,
                        nextEdgeId = successorEdgeId,
                        chainId = guard.ChainId,
                        position = Vec(current),
                        successorEntry = Vec(successorEntry),
                        distance,
                        state
                    });
                }
                return true;
            }

            if (nativeLandingOwnsBody)
            {
                return true;
            }

            if (distance + 0.035f < guard.LocalBridgeLastDistance)
            {
                guard.LocalBridgeLastDistance = distance;
                guard.LocalBridgeLastProgressAt = Time.time;
            }

            float elapsed = Time.time - guard.LocalBridgeStartedAt;
            float noProgressFor = Time.time - guard.LocalBridgeLastProgressAt;
            if (Time.time >= guard.LocalBridgeDeadlineAt || noProgressFor >= ChainLocalSuccessorBridgeNoProgressTimeout)
            {
                string bridgeFailure = Time.time >= guard.LocalBridgeDeadlineAt
                    ? "absoluteTimeout"
                    : "noPhysicalProgress";
                if (!true &&
                    !guard.LocalBridgeRecoveryTeleportAttempted &&
                    FollowerSafeTeleport.TryValidateLocalTraversalEntryAnchor(
                        bot, successorEntry, out Vector3 safeSuccessorEntry))
                {
                    guard.LocalBridgeRecoveryTeleportAttempted = true;
                    guard.LocalBridgeRecoveryTeleportAt = Time.time;
                    guard.ExpiresAt = Time.time + 5.0f;
                    if (FollowerSafeTeleport.TryTeleportWithOriginReceipt(
                            bot,
                            safeSuccessorEntry,
                            $"chainLocalBridgeRecovery:{successorEdgeId}",
                            out Vector3 recoveredEntry, out long issuedLocalBridgeTeleport))
                    {
                        guard.TeleportWriterGeneration = issuedLocalBridgeTeleport;
                        guard.LocalBridgeActive = false;
                        guard.LastSafeSuccessorPosition = recoveredEntry;
                        guard.HasLastSafeSuccessorPosition = true;
                        guard.LastSafeSuccessorEntryDistance = Vector3.Distance(recoveredEntry, successorEntry);
                        guard.BestSuccessorEntryDistance = guard.LastSafeSuccessorEntryDistance;
                        WriteEvent("traversalChainBridgeRecoveryTeleport", bot, new
                        {
                            completedEdgeId = guard.CompletedEdgeId,
                            completedKind = guard.CompletedKind,
                            nextEdgeId = successorEdgeId,
                            chainId = guard.ChainId,
                            position = Vec(current),
                            requestedEntry = Vec(successorEntry),
                            teleportedTo = Vec(recoveredEntry),
                            distance,
                            elapsed,
                            noProgressFor,
                            reason = bridgeFailure
                        });
                        return true;
                    }
                }

                ChainHandoffAuthorityGuards.Remove(profileId);
                RetireChainGuardToFollowAtOrigin(bot, guard, "traversalChainTerminalAbort:bridgeRecoveryRejected");
                WriteEvent("traversalChainTerminalAbort", bot, new
                {
                    completedEdgeId = guard.CompletedEdgeId,
                    completedKind = guard.CompletedKind,
                    nextEdgeId = successorEdgeId,
                    chainId = guard.ChainId,
                    position = Vec(current),
                    successorEntry = Vec(successorEntry),
                    distance,
                    elapsed,
                    noProgressFor,
                    bridgeRecoveryTeleportAttempted = guard.LocalBridgeRecoveryTeleportAttempted,
                    reason = "bridgeRecoveryRejected:" + bridgeFailure
                });
                return false;
            }

            Vector3 direction = flat / Mathf.Max(0.001f, distance);
            if (true && learningV357NavBridge)
            {
                if (Time.time >= guard.NextLocalBridgeLogAt)
                {
                    guard.NextLocalBridgeLogAt = Time.time + 0.35f;
                    WriteEvent("learningV357ChainLocalSuccessorNavRunEnvelope", bot, new
                    {
                        completedEdgeId = guard.CompletedEdgeId,
                        nextEdgeId = successorEdgeId,
                        chainId = guard.ChainId,
                        position = Vec(player.Position),
                        successorEntry = Vec(successorEntry),
                        distance,
                        state,
                        action = "keepMoverAliveAtRunSpeedUntilSuccessorOwnsTraversal"
                    });
                }
                return true;
            }

            float dt = Mathf.Max(0.001f, Time.deltaTime);
            float stepDistance = Mathf.Min(distance, ChainLocalSuccessorBridgeSpeed * dt);
            Vector3 motion = direction * stepDistance;
            try
            {
                if (!FollowerMovementWriterAuthority.TryEnterBoundPhysicalMotion(
                    bot, FollowerMovementWriterAuthority.WriterDomain.Traversal,
                    guard.MovementWriterGeneration,
                    out FollowerMovementWriterAuthority.WriteScope physicalScope, out _))
                    return false;
                using (physicalScope)
                {
                    if (!(ApplyCollisionActivePhysicalRouteMotion(
                        bot, player, direction, motion, dt, 1f,deliveryTarget:successorEntry,deliveryPurpose:CapabilityMotionPurpose.TypedRoute)).Accepted)
                        return false;
                }
            }
            catch (Exception ex)
            {
                WriteEvent("chainLocalSuccessorBridgeException", bot, new
                {
                    completedEdgeId = guard.CompletedEdgeId,
                    nextEdgeId = successorEdgeId,
                    chainId = guard.ChainId,
                    error = ex.GetType().Name + ": " + ex.Message
                });
                return true;
            }

            if (Time.time >= guard.NextLocalBridgeLogAt)
            {
                guard.NextLocalBridgeLogAt = Time.time + 0.35f;
                WriteEvent("chainLocalSuccessorBridgeProgress", bot, new
                {
                    completedEdgeId = guard.CompletedEdgeId,
                    nextEdgeId = successorEdgeId,
                    chainId = guard.ChainId,
                    position = Vec(player.Position),
                    successorEntry = Vec(successorEntry),
                    distance,
                    elapsed,
                    noProgressFor
                });
            }
            return true;
        }
    }
}
