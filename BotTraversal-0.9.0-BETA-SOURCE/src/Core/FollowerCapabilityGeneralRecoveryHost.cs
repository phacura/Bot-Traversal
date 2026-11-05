// R3Y_FOLLOW_RECOVERY_REVIEWED
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
        private static FollowerCapabilityFollowRecoveryPolicy.Snapshot CaptureCapabilityFollowRecovery(BotOwner bot,Player player,
            CapabilityFollowRecoveryMode mode,long issuedGeneration,ProbeRuntime probe)
        {
            if(bot==null||string.IsNullOrEmpty(bot.ProfileId)||player?.MovementContext==null)return new FollowerCapabilityFollowRecoveryPolicy.Snapshot(issuedGeneration,0,
                FollowerMovementWriterAuthority.WriterDomain.Unknown,mode,false,false,false,false,false,false,true);
            long live=FollowerMovementWriterAuthority.PeekExactGeneration(bot,out var owner);
            bool actor=IsLiveBot(bot)&&ReferenceEquals(bot.GetPlayer,player);
            bool native=HasActiveTraversalProbe(bot);
            bool probeCurrent=probe!=null&&ReferenceEquals(probe.Bot,bot)&&ReferenceEquals(probe.Player,player)&&
                probe.MovementWriterGeneration==issuedGeneration&&IsActiveProbe(probe)&&IsProbeLifecycleCurrent(probe)&&
                (probe.LifecycleMission==null||probe.Lifecycle.Origin.Writer==issuedGeneration);
            bool canonical=FollowerTraversalRouteCursors.TryGetValue(bot.ProfileId,out var cursor)&&cursor!=null&&
                cursor.LearningV375TransactionActive&&cursor.LearningV375MovementGeneration==live&&
                (owner==FollowerMovementWriterAuthority.WriterDomain.Traversal||owner==FollowerMovementWriterAuthority.WriterDomain.PostTraversal);
            bool mission=TryGetActiveFollowerRouteMission(bot,out var currentMission);
            bool recoveryEpoch=mission&&FollowerMovementRecoveryStates.TryGetValue(bot.ProfileId,out var recovery)&&
                recovery!=null&&recovery.LearningV350TerminalRecoveryEligible&&MatchLifecycleRecovery(recovery,currentMission);
            var follower=BossPlayers.GetFollowerByProfileId(bot.ProfileId);var context=player.MovementContext;
            bool preempted=follower==null||(follower.TryPeekActiveCommand(out var command,out _,out _)&&command!=FollowerCommandType.None)||
                context.OverridenState!=null||context.OverridenControlsState!=null||context.IgnoreDeltaMovement||
                follower?.IsTeleportStabilizing==true||IsTeleportTransactionActive(bot)||
                FollowerMedical.IsUsingMedical(bot)||FollowerMedical.IsHandsInteractionActiveForAuthority(player)||
                FollowerEnvironment.TryGetImmediateSurvivalThreat(bot,out _)||FollowerStateAuthorityCoordinator.HasCombatMovementClaim(bot,out _);
            return new FollowerCapabilityFollowRecoveryPolicy.Snapshot(
                issuedGeneration,live,owner,mode,actor,native,probeCurrent,canonical,mission,recoveryEpoch,preempted);
        }
        private static CapabilityFollowRecoveryFailure ObserveCapabilityFollowRecovery(BotOwner bot,Player player,
            CapabilityFollowRecoveryMode mode,long issuedGeneration,ProbeRuntime probe)=>
            FollowerCapabilityFollowRecoveryPolicy.Evaluate(CaptureCapabilityFollowRecovery(bot,player,mode,issuedGeneration,probe));
        private static bool TryEnterCapabilityFreshFollowScope(BotOwner bot,string source,
            out FollowerMovementWriterAuthority.WriteScope scope,out string reason)
        {
            scope=default;
            var failure=FollowerCapabilityFollowRecoveryPolicy.EvaluateFresh(CaptureCapabilityFollowRecovery(
                bot,bot?.GetPlayer,CapabilityFollowRecoveryMode.FollowPath,0,null));
            reason=failure.ToString();
            if(failure!=CapabilityFollowRecoveryFailure.None)return false;
            if(!FollowerMovementWriterAuthority.TryEnterFollowPhysicalMotion(bot,source,out scope,out reason))return false;
            failure=ObserveCapabilityFollowRecovery(bot,bot?.GetPlayer,CapabilityFollowRecoveryMode.FollowPath,scope.IssuedGeneration,null);
            if(failure==CapabilityFollowRecoveryFailure.None)return true;
            scope.Dispose();scope=default;reason=failure.ToString();return false;
        }
        private static bool TryHardRecoverCapabilityProbeLocomotion(ProbeRuntime probe,string source,out string detail)
        {
            return probe?.Player?.CurrentStateName==EPlayerState.JumpLanding
                ?TryHardRecoverStuckJumpLanding(probe.Bot,probe,source,out detail)
                :TryRecoverBoundFollowerLocomotion(probe?.Bot,probe,source,out detail);
        }
        private static bool TryEnterCapabilityFollowRecoveryScope(BotOwner bot,Player player,
            CapabilityFollowRecoveryMode mode,long issuedGeneration,ProbeRuntime probe,
            out FollowerMovementWriterAuthority.WriteScope scope,out CapabilityFollowRecoveryFailure failure)
        {
            scope=default;
            failure=ObserveCapabilityFollowRecovery(bot,player,mode,issuedGeneration,probe);
            if(failure==CapabilityFollowRecoveryFailure.None)
            {
                FollowerMovementWriterAuthority.PeekExactGeneration(bot,out var owner);
                if(FollowerMovementWriterAuthority.TryEnterBoundPhysicalMotion(bot,owner,issuedGeneration,out scope,out _))
                {
                    failure=ObserveCapabilityFollowRecovery(bot,player,mode,issuedGeneration,probe);
                    if(failure==CapabilityFollowRecoveryFailure.None)return true;
                    scope.Dispose();scope=default;
                }
                else failure=CapabilityFollowRecoveryFailure.ScopeRejected;
            }
            if(bot!=null&&ShouldEmitDiagnostic("capabilityFollowRecoveryRejected:"+mode+":"+failure,bot,.75f))
                WriteEvent("capabilityFollowRecoveryRejected",bot,new{mode=mode.ToString(),failure=failure.ToString(),issuedGeneration,
                    nativeSuccessRecorded=false,forwardInputPublished=false,bodyRelocated=false});
            return false;
        }
        private static bool TryRecoverGeneralFollowerLocomotion(BotOwner bot,string source,out string detail)
        {
            return TryRecoverBoundFollowerLocomotion(bot,null,source,out detail);
        }

        private static void ReleaseLearningV353TerminalRecoveryRetryLease(BotOwner bot, string reason)
        {
            if ((bot == null))
            {
                return;
            }
            string profileId = bot.ProfileId ?? string.Empty;
            if (string.IsNullOrEmpty(profileId) ||
                !FollowerRouteMissions.TryGetValue(profileId, out FollowerRouteMission mission) || mission == null)
            {
                return;
            }

            bool hadLease = mission.NextMoveAttemptAt > Time.time ||
                            mission.TraversalRetryMovementHoldUntil > Time.time;
            CommitCapabilityMoveRetry(mission,FollowerCapabilityRetryPolicy.Change.Reset,0f);
            mission.TraversalRetryMovementHoldUntil = 0f;
            if (hadLease && ShouldEmitDiagnostic("learningV353TerminalRecoveryRetryLeaseReleased", bot, 0.35f))
            {
                WriteEvent("learningV353TerminalRecoveryRetryLeaseReleased", bot, new
                {
                    missionId = mission.MissionId,
                    stepIndex = mission.CurrentStepIndex,
                    reason = reason ?? "terminalRecovery",
                    action = "navigationRecoveryOwnsMovementUntilExplicitRecoveryBoundary"
                });
            }
        }

        private static void CaptureLearningV362TerminalRecoveryEpoch(
            BotOwner bot,
            FollowerMovementRecoveryState state,
            string source)
        {
            if ((state == null))
            {
                return;
            }

            state.LearningV362TerminalRecoveryLatchEpisodeId = GetLatestTraversalEpisodeId();
            state.LearningV362TerminalRecoveryLatchBreadcrumbSequence =
                Math.Max(GetLearningV362LatestBreadcrumbSequence(),
                         Math.Max(0, state.LearningV349GenericPassageCursorSequence));
            state.LearningV362TerminalRecoveryLatchAt = Time.time;
            state.LearningV362TerminalRecoveryLatchSource =
                string.IsNullOrWhiteSpace(source) ? "terminalRecovery" : source;

            // V3.85: the global/latest player episode is useful chronology, but it is not the
            // identity of the body transaction that actually stalled. Persist that route-step
            // identity across mission object rebuilds so the same debt cannot reappear with a new
            // AttemptStartedObserved=false bit and erase terminal recovery.
            state.LearningV385TerminalRecoveryExecutionEpisodeId = 0;
            state.LearningV385TerminalRecoveryExecutionEdgeId = 0;
            state.LearningV385TerminalRecoveryExecutionStartBreadcrumbSequence = 0;
            state.LearningV385TerminalRecoveryExecutionEndBreadcrumbSequence = 0;
            state.V2TerminalRecoveryWriterYieldReceipt = default(
                FollowerMovementV2IntentGateway.TerminalRecoveryWriterYieldReceipt);
            if ((bot != null))
            {
                string profileId = bot.ProfileId ?? string.Empty;
                if (!string.IsNullOrEmpty(profileId) &&
                    FollowerRouteMissions.TryGetValue(profileId, out FollowerRouteMission mission) &&
                    mission?.Steps != null && mission.CurrentStepIndex >= 0 &&
                    mission.CurrentStepIndex < mission.Steps.Count)
                {
                    FollowerRouteMissionStep stalledStep = mission.Steps[mission.CurrentStepIndex];
                    if (stalledStep != null)
                    {
                        CaptureLifecycleRecovery(bot,state,mission,stalledStep);
                        state.LearningV385TerminalRecoveryExecutionEpisodeId = stalledStep.EpisodeId;
                        state.LearningV385TerminalRecoveryExecutionEdgeId = stalledStep.EdgeId;
                        state.LearningV385TerminalRecoveryExecutionStartBreadcrumbSequence =
                            stalledStep.StartBreadcrumbSequence;
                        state.LearningV385TerminalRecoveryExecutionEndBreadcrumbSequence =
                            stalledStep.EndBreadcrumbSequence;
                    }
                }
            }
        }

        private static bool TrySupersedeLearningV362TerminalRecoveryByFreshRouteEpoch(
            BotOwner bot,
            FollowerRouteMission mission,
            string source)
        {
            if ((bot == null) || mission == null ||
                mission.CurrentStepIndex < 0 || mission.CurrentStepIndex >= mission.Steps.Count)
            {
                return false;
            }

            string profileId = bot.ProfileId ?? string.Empty;
            if (string.IsNullOrEmpty(profileId) ||
                !FollowerMovementRecoveryStates.TryGetValue(
                    profileId, out FollowerMovementRecoveryState state) ||
                state == null || !state.LearningV350TerminalRecoveryEligible)
            {
                return false;
            }

            FollowerRouteMissionStep step = null;
            int freshStepIndex = -1;
            bool newerEpisode = false;
            bool newerBreadcrumbEpoch = false;
            for (int index = Math.Max(0, mission.CurrentStepIndex); index < mission.Steps.Count; index++)
            {
                FollowerRouteMissionStep candidate = mission.Steps[index];
                if (candidate == null || !candidate.CurrentRaidRouteAuthority)
                {
                    continue;
                }

                bool learningV385SameTerminalExecutionDebt =
                    (state.LearningV385TerminalRecoveryExecutionEpisodeId > 0) &&
                    candidate.EpisodeId == state.LearningV385TerminalRecoveryExecutionEpisodeId &&
                    (state.LearningV385TerminalRecoveryExecutionEdgeId <= 0 ||
                     candidate.EdgeId == state.LearningV385TerminalRecoveryExecutionEdgeId);
                if (learningV385SameTerminalExecutionDebt)
                {
                    if (ShouldEmitDiagnostic(
                            "learningV385TerminalRecoverySameExecutionDebtRejectedAsFreshEpoch:" +
                            candidate.EpisodeId.ToString(CultureInfo.InvariantCulture) + ":" +
                            candidate.EdgeId.ToString(CultureInfo.InvariantCulture), bot, 0.50f))
                    {
                        WriteEvent("learningV385TerminalRecoverySameExecutionDebtRejectedAsFreshEpoch", bot, new
                        {
                            missionId = mission.MissionId,
                            candidateStepIndex = index,
                            episodeId = candidate.EpisodeId,
                            edgeId = candidate.EdgeId,
                            kind = candidate.Kind,
                            startBreadcrumbSequence = candidate.StartBreadcrumbSequence,
                            endBreadcrumbSequence = candidate.EndBreadcrumbSequence,
                            terminalExecutionEpisodeId = state.LearningV385TerminalRecoveryExecutionEpisodeId,
                            terminalExecutionEdgeId = state.LearningV385TerminalRecoveryExecutionEdgeId,
                            terminalExecutionStartBreadcrumbSequence =
                                state.LearningV385TerminalRecoveryExecutionStartBreadcrumbSequence,
                            terminalExecutionEndBreadcrumbSequence =
                                state.LearningV385TerminalRecoveryExecutionEndBreadcrumbSequence,
                            traversalSuccessRecorded = false,
                            persistentLearningChanged = false,
                            action = "missionObjectRebuildCannotRefreshSameStalledExecutionDebt"
                        });
                    }
                    continue;
                }

                bool learningV403CurrentExecutionStep = index == mission.CurrentStepIndex;
                bool learningV404LivePersistentMovementHandoff = false;
                long learningV404MovementGeneration = 0;
                FollowerMovementWriterAuthority.WriterDomain learningV404MovementOwner =
                    FollowerMovementWriterAuthority.WriterDomain.Unknown;
                if ((FollowerTraversalRouteCursors.TryGetValue(
                        profileId, out FollowerTraversalRouteCursor learningV404Cursor)) &&
                    learningV404Cursor != null &&
                    learningV404Cursor.LearningV375TransactionActive &&
                    learningV404Cursor.LearningV375RouteMissionId == mission.MissionId &&
                    learningV404Cursor.LearningV375MovementGeneration > 0)
                {
                    learningV404MovementGeneration =
                        FollowerMovementWriterAuthority.GetCurrentGeneration(
                            bot, out learningV404MovementOwner);
                    learningV404LivePersistentMovementHandoff =
                        learningV404MovementOwner == FollowerMovementWriterAuthority.WriterDomain.Traversal &&
                        learningV404MovementGeneration == learningV404Cursor.LearningV375MovementGeneration;
                }
                bool learningV404ApplyV403SuccessorGate =
                    FollowerCapabilityHelperRules.TrySupersedeLearningV362TerminalRecoveryByFreshRouteEpoch_learningV404ApplyV403SuccessorGate_AB0256DE_1(learningV404LivePersistentMovementHandoff);
                bool learningV403DownstreamSameEpisodeCandidate =
                    learningV404ApplyV403SuccessorGate &&
                    !learningV403CurrentExecutionStep &&
                    !candidate.AttemptStartedObserved &&
                    candidate.EpisodeId > 0 &&
                    candidate.EpisodeId == state.LearningV362TerminalRecoveryLatchEpisodeId &&
                    string.Equals(
                        state.LearningV350TerminalRecoveryReason,
                        "genericPhysicalPassageNoMeasuredProgress",
                        StringComparison.OrdinalIgnoreCase);
                bool learningV404ReleasedV403GateWithoutLivePersistentMovement =
                    FollowerCapabilityHelperRules.TrySupersedeLearningV362TerminalRecoveryByFreshRouteEpoch_learningV404ReleasedV403GateWithoutLivePersistentMovement_AB0256DE_2(learningV403CurrentExecutionStep) &&
                    !candidate.AttemptStartedObserved &&
                    candidate.EpisodeId > 0 &&
                    candidate.EpisodeId == state.LearningV362TerminalRecoveryLatchEpisodeId &&
                    string.Equals(
                        state.LearningV350TerminalRecoveryReason,
                        "genericPhysicalPassageNoMeasuredProgress",
                        StringComparison.OrdinalIgnoreCase) &&
                    !learningV404LivePersistentMovementHandoff;
                if (learningV404ReleasedV403GateWithoutLivePersistentMovement &&
                    ShouldEmitDiagnostic(
                        "learningV404DownstreamSuccessorGateReleasedWithoutLivePersistentMovement:" +
                            candidate.EpisodeId.ToString(CultureInfo.InvariantCulture) + ":" +
                            candidate.EdgeId.ToString(CultureInfo.InvariantCulture),
                        bot, 0.50f))
                {
                    WriteEvent("learningV404DownstreamSuccessorGateReleasedWithoutLivePersistentMovement", bot, new
                    {
                        missionId = mission.MissionId,
                        currentStepIndex = mission.CurrentStepIndex,
                        candidateStepIndex = index,
                        candidateEpisodeId = candidate.EpisodeId,
                        candidateEdgeId = candidate.EdgeId,
                        candidateKind = candidate.Kind,
                        terminalExecutionEpisodeId = state.LearningV385TerminalRecoveryExecutionEpisodeId,
                        terminalExecutionEdgeId = state.LearningV385TerminalRecoveryExecutionEdgeId,
                        terminalLatchEpisodeId = state.LearningV362TerminalRecoveryLatchEpisodeId,
                        terminalLatchBreadcrumbSequence = state.LearningV362TerminalRecoveryLatchBreadcrumbSequence,
                        matchingLivePersistentMovementHandoff = learningV404LivePersistentMovementHandoff,
                        movementOwner = learningV404MovementOwner.ToString(),
                        movementGeneration = learningV404MovementGeneration,
                        traversalSuccessRecorded = false,
                        persistentLearningChanged = false,
                        routeStepAdvanced = false,
                        bodyRelocated = false,
                        action = "v403GateRequiresLiveMatchingV375MovementEpochOtherwisePreserveV402FreshRouteAdmission"
                    });
                }

                if (learningV403DownstreamSameEpisodeCandidate &&
                    ShouldEmitDiagnostic(
                        "learningV403DownstreamSuccessorCannotSupersedeCurrentTerminalRecovery:" +
                            candidate.EpisodeId.ToString(CultureInfo.InvariantCulture) + ":" +
                            candidate.EdgeId.ToString(CultureInfo.InvariantCulture),
                        bot, 0.50f))
                {
                    WriteEvent("learningV403DownstreamSuccessorCannotSupersedeCurrentTerminalRecovery", bot, new
                    {
                        missionId = mission.MissionId,
                        currentStepIndex = mission.CurrentStepIndex,
                        candidateStepIndex = index,
                        candidateEpisodeId = candidate.EpisodeId,
                        candidateEdgeId = candidate.EdgeId,
                        candidateKind = candidate.Kind,
                        candidateCreatedAt = candidate.CreatedAt,
                        terminalLatchEpisodeId = state.LearningV362TerminalRecoveryLatchEpisodeId,
                        terminalLatchBreadcrumbSequence = state.LearningV362TerminalRecoveryLatchBreadcrumbSequence,
                        terminalLatchAt = state.LearningV362TerminalRecoveryLatchAt,
                        terminalExecutionEpisodeId = state.LearningV385TerminalRecoveryExecutionEpisodeId,
                        terminalExecutionEdgeId = state.LearningV385TerminalRecoveryExecutionEdgeId,
                        terminalReason = state.LearningV350TerminalRecoveryReason ?? string.Empty,
                        traversalSuccessRecorded = false,
                        persistentLearningChanged = false,
                        routeStepAdvanced = false,
                        movementCursorChanged = false,
                        action = "downstreamSuccessorCannotEraseTerminalRecoveryForStillCurrentExecutionStep"
                    });
                }

                bool learningV376UnattemptedCurrentRouteSameEpisodeRecovery =
                    FollowerCapabilityHelperRules.TrySupersedeLearningV362TerminalRecoveryByFreshRouteEpoch_learningV376UnattemptedCurrentRouteSameEpisodeRecovery_AB0256DE_3(learningV404ApplyV403SuccessorGate,learningV403CurrentExecutionStep) &&
                    !candidate.AttemptStartedObserved &&
                    candidate.EpisodeId > 0 &&
                    candidate.EpisodeId == state.LearningV362TerminalRecoveryLatchEpisodeId &&
                    string.Equals(
                        state.LearningV350TerminalRecoveryReason,
                        "genericPhysicalPassageNoMeasuredProgress",
                        StringComparison.OrdinalIgnoreCase);
                if (candidate.CreatedAt <= state.LearningV362TerminalRecoveryLatchAt + 0.001f &&
                    !learningV376UnattemptedCurrentRouteSameEpisodeRecovery)
                {
                    continue;
                }

                bool candidateNewerEpisode =
                    candidate.EpisodeId > 0 &&
                    candidate.EpisodeId > state.LearningV362TerminalRecoveryLatchEpisodeId;
                bool candidateNewerBreadcrumbEpoch =
                    candidate.StartBreadcrumbSequence > 0 &&
                    candidate.StartBreadcrumbSequence > state.LearningV362TerminalRecoveryLatchBreadcrumbSequence;
                bool learningV373TypedSameEpisodeSupersedesGenericRecovery =
                    FollowerCapabilityHelperRules.TrySupersedeLearningV362TerminalRecoveryByFreshRouteEpoch_learningV373TypedSameEpisodeSupersedesGenericRecovery_AB0256DE_4(learningV376UnattemptedCurrentRouteSameEpisodeRecovery,learningV404ApplyV403SuccessorGate,learningV403CurrentExecutionStep) &&
                    candidate.EpisodeId > 0 &&
                    candidate.EpisodeId == state.LearningV362TerminalRecoveryLatchEpisodeId &&
                    string.Equals(
                        state.LearningV350TerminalRecoveryReason,
                        "genericPhysicalPassageNoMeasuredProgress",
                        StringComparison.OrdinalIgnoreCase);
                if (!candidateNewerEpisode && !candidateNewerBreadcrumbEpoch &&
                    !learningV373TypedSameEpisodeSupersedesGenericRecovery)
                {
                    continue;
                }

                // RC35: never let a newer, unconsumed SAME-chain successor erase
                // terminal recovery of the still-current predecessor. This
                // converts CF74's shadow-only observation into a pure live-step
                // admission at the single recovery supersession seam. Keep
                // searching for an independent chain instead of blocking it.
                FollowerRouteMissionStep terminalCurrentStep =
                    mission.Steps[mission.CurrentStepIndex];
                bool terminalSuccessorAdmitted =
                    FollowerMovementV2IntentGateway.CanSupersedeTerminalRecoveryWithRouteCandidate(
                        mission.CurrentStepIndex, index,
                        terminalCurrentStep?.ChainId ?? 0, candidate.ChainId);
                if (!terminalSuccessorAdmitted)
                {
                    if (ShouldEmitDiagnostic(
                            "rc35SameChainUnconsumedTerminalPredecessor:" +
                            mission.MissionId.ToString(CultureInfo.InvariantCulture) + ":" +
                            index.ToString(CultureInfo.InvariantCulture), bot, 1.0f))
                    {
                        WriteEvent("followerTraversalTerminalSameChainSuccessorRejected", bot, new
                        {
                            missionId = mission.MissionId,
                            currentStepIndex = mission.CurrentStepIndex,
                            candidateStepIndex = index,
                            predecessorEpisodeId = terminalCurrentStep?.EpisodeId ?? 0,
                            predecessorEdgeId = terminalCurrentStep?.EdgeId ?? 0,
                            predecessorChainId = terminalCurrentStep?.ChainId ?? 0,
                            candidateEpisodeId = candidate.EpisodeId,
                            candidateEdgeId = candidate.EdgeId,
                            candidateChainId = candidate.ChainId,
                            terminalExecutionEpisodeId = state.LearningV385TerminalRecoveryExecutionEpisodeId,
                            terminalExecutionEdgeId = state.LearningV385TerminalRecoveryExecutionEdgeId,
                            nativeSuccessRecorded = false,
                            routeStepAdvanced = false,
                            recoveryStateCleared = false,
                            action = "sameChainDownstreamChronologyCannotRetireUnconsumedTerminalDebt"
                        });
                    }
                    continue;
                }

                step = candidate;
                freshStepIndex = index;
                newerEpisode = candidateNewerEpisode || learningV373TypedSameEpisodeSupersedesGenericRecovery;
                newerBreadcrumbEpoch = candidateNewerBreadcrumbEpoch;
                break;
            }
            if (step == null)
            {
                return false;
            }

            if (HasActiveTraversalProbe(bot))
            {
                return false;
            }

            FollowerMovementWriterAuthority.GetCurrentGeneration(
                bot, out FollowerMovementWriterAuthority.WriterDomain movementOwner);
            if (movementOwner == FollowerMovementWriterAuthority.WriterDomain.Teleport)
            {
                return false;
            }

            float terminalSince = state.LearningV350TerminalRecoverySince;
            string terminalReason = state.LearningV350TerminalRecoveryReason ?? string.Empty;
            int latchEpisodeId = state.LearningV362TerminalRecoveryLatchEpisodeId;
            int latchBreadcrumbSequence = state.LearningV362TerminalRecoveryLatchBreadcrumbSequence;
            float latchAt = state.LearningV362TerminalRecoveryLatchAt;
            string latchSource = state.LearningV362TerminalRecoveryLatchSource ?? string.Empty;

            FollowerRouteMissionStep staleStep = mission.Steps[mission.CurrentStepIndex];
            var r2DebtObservation6 = CaptureRouteDebtObservation(bot, mission, null, true);

            int previousStepIndex = mission.CurrentStepIndex;
            bool learningV369IndependentFreshChain =
                (staleStep != null) && step != null &&
                staleStep.ChainId > 0 && step.ChainId > 0 && staleStep.ChainId != step.ChainId;
            int learningV369RetiredExecutionFloor = 0;
            if (learningV369IndependentFreshChain)
            {
                for (int index = previousStepIndex; index < freshStepIndex; index++)
                {
                    FollowerRouteMissionStep retired = mission.Steps[index];
                    if (retired != null)
                    {
                        learningV369RetiredExecutionFloor = Math.Max(
                            learningV369RetiredExecutionFloor, retired.EndBreadcrumbSequence);
                    }
                }
                if (!TryAdvanceRouteMission(bot, mission, freshStepIndex, RouteDebtEvidence.RouteSuperseded, "TrySupersedeLearningV362TerminalRecoveryByFreshRouteEpoch:1", r2DebtObservation6)) return false;
                if (learningV369RetiredExecutionFloor > 0)
                {
                    int previousExecutionFloor = GetFollowerRouteExecutionFloor(profileId);
                    int newExecutionFloor = Math.Max(previousExecutionFloor, learningV369RetiredExecutionFloor);
                    FollowerRouteExecutionFloors[profileId] = newExecutionFloor;
                    mission.ResumeFloorBreadcrumbSequence = Math.Max(
                        mission.ResumeFloorBreadcrumbSequence, newExecutionFloor);
                }
                LearnedTraversalCommitments.Remove(profileId);
                ClearLearningV365ContinuousPhysicalRoute(bot, "learningV369FreshIndependentRouteEpoch");
            }

            // CF74: observe a newer downstream episode clearing terminal recovery while the
            // mission cursor still points to an uncompleted same-chain predecessor.  Do not
            // silently call that downstream episode a successful Climb/WT, and do not change
            // any existing retirement or recovery behavior during the shadow stage.
            if (staleStep != null && step != null &&
                freshStepIndex > previousStepIndex &&
                staleStep.ChainId > 0 && staleStep.ChainId == step.ChainId &&
                !staleStep.AttemptStartedObserved &&
                !learningV369IndependentFreshChain)
            {
                WriteEvent("cf74ShadowTerminalSuccessorBeforePredecessorCompletion", bot, new
                {
                    missionId = mission.MissionId,
                    currentStepIndex = previousStepIndex,
                    downstreamStepIndex = freshStepIndex,
                    predecessorEdgeId = staleStep.EdgeId,
                    predecessorEpisodeId = staleStep.EpisodeId,
                    predecessorKind = staleStep.Kind,
                    successorEdgeId = step.EdgeId,
                    successorEpisodeId = step.EpisodeId,
                    successorKind = step.Kind,
                    chainId = staleStep.ChainId,
                    observedMovementOwner = movementOwner.ToString(),
                    terminalReason,
                    predecessorPhysicalSuccessProven = false,
                    routeStepAdvanced = false,
                    behaviorChanged = false,
                    action = "shadowOnlyCurrentMissionDebtOutlivesTerminalLease"
                });
            }

            // Only an accepted, independent fresh demonstration receives a fresh existing mission lifetime.
            mission.ExpiresAt = FollowerCapabilityPreparationPolicy.FreshDeadline(mission.ExpiresAt, Time.time,
                RouteMissionLifetimeSeconds, learningV369IndependentFreshChain, step.CreatedAt, terminalSince);
            ClearLearningV351TerminalRecoveryState(
                bot,
                "learningV362FreshTraversalEpoch:" + (source ?? "routeMission"));
            ReleaseLearningV353TerminalRecoveryRetryLease(
                bot,
                "learningV362FreshTraversalEpoch:" + (source ?? "routeMission"));

            if (learningV369IndependentFreshChain)
            {
                WriteEvent("learningV369FreshRouteEpochSupersededStaleMissionTransaction", bot, new
                {
                    missionId = mission.MissionId,
                    previousStepIndex,
                    freshStepIndex,
                    staleEpisodeId = staleStep?.EpisodeId ?? 0,
                    staleEdgeId = staleStep?.EdgeId ?? 0,
                    staleKind = staleStep?.Kind ?? string.Empty,
                    staleChainId = staleStep?.ChainId ?? 0,
                    freshEpisodeId = step.EpisodeId,
                    freshEdgeId = step.EdgeId,
                    freshKind = step.Kind,
                    freshChainId = step.ChainId,
                    retiredExecutionFloor = learningV369RetiredExecutionFloor,
                    terminalReason,
                    traversalSuccessRecorded = false,
                    persistentLearningChanged = false,
                    action = "explicitTerminalRecoveryBoundaryRetiresOnlyStaleRuntimeExecutionTransactionThenAdmitsNewIndependentPlayerRouteEpoch"
                });
            }

            WriteEvent("learningV362FreshTraversalEpochSupersededTerminalRecovery", bot, new
            {
                missionId = mission.MissionId,
                currentStepIndex = mission.CurrentStepIndex,
                freshStepIndex,
                episodeId = step.EpisodeId,
                edgeId = step.EdgeId,
                kind = step.Kind,
                stepCreatedAt = step.CreatedAt,
                stepStartBreadcrumbSequence = step.StartBreadcrumbSequence,
                stepEndBreadcrumbSequence = step.EndBreadcrumbSequence,
                latchEpisodeId,
                latchBreadcrumbSequence,
                latchAt,
                latchSource,
                terminalSince,
                terminalReason,
                movementOwner = movementOwner.ToString(),
                source = source ?? "routeMission",
                traversalSuccessRecorded = false,
                persistentLearningChanged = false,
                routeStepAdvanced = false,
                movementCursorChanged = false,
                action = "retireOnlyStaleTerminalRecoveryLeaseAllowFreshCurrentRaidRouteEpoch"
            });
            return true;
        }

        private static void ClearLearningV351TerminalRecoveryState(BotOwner bot, string reason)
        {
            string profileId = bot?.ProfileId ?? string.Empty;
            if (string.IsNullOrEmpty(profileId) ||
                !FollowerMovementRecoveryStates.TryGetValue(profileId, out FollowerMovementRecoveryState state) ||
                state == null)
            {
                return;
            }

            bool wasLatched = state.LearningV350TerminalRecoveryEligible;
            state.LifecycleRecoveryOrigin=default(FollowerTraversalLifecycle.Identity);
            state.LifecycleRecoveryMission=null;state.LifecycleRecoveryStep=null;
            state.LearningV350TerminalRecoveryEligible = false;
            state.LearningV350TerminalRecoverySince = -1f;
            state.LearningV350TerminalRecoveryReason = string.Empty;
            state.LearningV362TerminalRecoveryLatchEpisodeId = 0;
            state.LearningV362TerminalRecoveryLatchBreadcrumbSequence = 0;
            state.LearningV362TerminalRecoveryLatchAt = -1f;
            state.LearningV362TerminalRecoveryLatchSource = string.Empty;
            state.LearningV385TerminalRecoveryExecutionEpisodeId = 0;
            state.LearningV385TerminalRecoveryExecutionEdgeId = 0;
            state.LearningV385TerminalRecoveryExecutionStartBreadcrumbSequence = 0;
            state.LearningV385TerminalRecoveryExecutionEndBreadcrumbSequence = 0;
            state.V2TerminalRecoveryWriterYieldReceipt = default(
                FollowerMovementV2IntentGateway.TerminalRecoveryWriterYieldReceipt);
            state.LearningV350GenericPassageArmedAt = -1f;
            state.LearningV350GenericPassageArmCursorSequence = 0;
            state.LearningV349GenericPassageLeaseUntil = -1f;
            state.LearningV349PhysicalStallSince = -1f;
            state.LearningV349LastPhysicalPosition = bot.Position;
            state.LearningV349LastPhysicalProgressAt = Time.time;
            if (wasLatched)
            {
                WriteEvent("learningV351TerminalRecoveryClearedAtRecoveryBoundary", bot, new
                {
                    reason = reason ?? "recoveryBoundary",
                    position = Vec(bot.Position)
                });
            }
        }

        internal static bool IsLearningV350TerminalPhysicalRecoveryEligible(BotOwner bot)
        {
            if ((bot == null) ||
                string.IsNullOrEmpty(bot.ProfileId))
            {
                return false;
            }

            return FollowerMovementRecoveryStates.TryGetValue(
                       bot.ProfileId, out FollowerMovementRecoveryState state) &&
                   state != null && state.LearningV350TerminalRecoveryEligible;
        }

        private static bool TryDriveLearningV374NearSideRetryForwardProducer(
            BotOwner bot,
            string source)
        {
            if ((bot == null) ||
                string.IsNullOrEmpty(bot.ProfileId) ||
                !FollowerRouteMissions.TryGetValue(bot.ProfileId, out FollowerRouteMission mission) ||
                mission == null || mission.CurrentStepIndex < 0 ||
                mission.CurrentStepIndex >= mission.Steps.Count)
            {
                return false;
            }

            FollowerRouteMissionStep step = mission.Steps[mission.CurrentStepIndex];
            if (step == null || step.EdgeId <= 0)
            {
                return false;
            }

            string retryKey = bot.ProfileId + ":" +
                step.EdgeId.ToString(CultureInfo.InvariantCulture);
            if (!LearningV374NearSideForwardReapproachUntil.TryGetValue(
                    retryKey, out float reapproachUntil))
            {
                return false;
            }
            if (Time.time >= reapproachUntil)
            {
                LearningV374NearSideForwardReapproachUntil.Remove(retryKey);
                return false;
            }

            LearnedTraversalEdge edge = LearnedTraversalEdges.FirstOrDefault(candidate =>
                candidate != null && candidate.Id == step.EdgeId);
            if (!TryGetLearningV374LearnedActuatorTriggerFrontier(
                    edge,
                    out float minimumForwardProjection,
                    out string proofSource))
            {
                LearningV374NearSideForwardReapproachUntil.Remove(retryKey);
                return false;
            }

            Vector3 rawEntry = IsFinite(step.RawEntry) ? step.RawEntry :
                (edge != null && IsFinite(edge.RawStart) ? edge.RawStart : step.Entry);
            Vector3 rawExit = IsFinite(step.RawExit) ? step.RawExit :
                (edge != null && IsFinite(edge.RawEnd) ? edge.RawEnd : step.Exit);
            Vector3 direction = GetFlatDirection(rawExit - rawEntry);
            if (!IsFinite(rawEntry) || !IsFinite(rawExit) || direction.sqrMagnitude < 0.5f)
            {
                return false;
            }

            try
            {
                bot.Mover.SetBotMoverPause("R1-F314632DF9B2",@bot,false);
                bot.Mover.LocalAvoidance?.GClass492DropOffset("R1-04400810FF0B",@bot);
                bot.Mover.BotMoverGoToPointNoWay("R1-CE8F1FA0F260",@bot,rawEntry);
                bot.Steering?.BotSteeringLookToDirection("R1-2A7D7C52DB58",@bot,direction);
            }
            catch
            {
                return false;
            }

            if (ShouldEmitDiagnostic(
                    "learningV374NearSideRetryForwardProducer:" +
                        step.EdgeId.ToString(CultureInfo.InvariantCulture), bot, 0.35f))
            {
                Vector3 bodyOffset = bot.Position - rawEntry;
                bodyOffset.y = 0f;
                float forwardEntryProjection = Vector3.Dot(bodyOffset, direction);
                WriteEvent("learningV374NearSideRetryForwardProducer", bot, new
                {
                    missionId = mission.MissionId,
                    stepIndex = mission.CurrentStepIndex,
                    edgeId = step.EdgeId,
                    kind = step.Kind,
                    rawEntry = Vec(rawEntry),
                    rawExit = Vec(rawExit),
                    forwardEntryProjection,
                    minimumForwardProjection,
                    remaining = Mathf.Max(0f, reapproachUntil - Time.time),
                    proofSource,
                    source = source ?? "routeRetry",
                    action = "preserveRetryCooldownButReplaceStopIdleWithRouteLocalForwardReapproach"
                });
            }
            return true;
        }

        private static void UpdateLearningV349GenericPhysicalProgressWatchdog(
            BotOwner bot,
            Player player,
            FollowerMovementRecoveryState state)
        {
            if ((bot == null) || player?.MovementContext == null || state == null ||
                player.CurrentStateName == EPlayerState.JumpLanding || IsTeleportTransactionActive(bot) ||
                FollowerStateAuthorityCoordinator.HasCombatMovementClaim(bot, out _) ||
                FollowerMedical.IsUsingMedical(bot) ||
                FollowerMedical.IsHandsInteractionActiveForAuthority(player) ||
                bot.DoorOpener?.Interacting == true)
            {
                if (state != null)
                {
                    state.LearningV349PhysicalStallSince = -1f;
                    state.LearningV349LastPhysicalContext = string.Empty;
                }
                return;
            }

            string profileId = bot.ProfileId ?? string.Empty;
            BotFollowerPlayer follower = BossPlayers.GetFollowerByProfileId(profileId);
            if (follower?.IsTeleportStabilizing == true ||
                (follower != null && follower.TryPeekActiveCommand(
                    out FollowerCommandType activeCommand, out _, out _) &&
                 activeCommand != FollowerCommandType.None))
            {
                state.LearningV349PhysicalStallSince = -1f;
                return;
            }

            Player leader = FollowerEnvironment.LocalPlayer;
            if (!IsLivePlayer(leader) || !IsFinite(leader.Position) || !IsFinite(bot.Position))
            {
                state.LearningV349PhysicalStallSince = -1f;
                return;
            }

            bool haveProbe = TryGetActiveProbe(bot, out ProbeRuntime probe) && probe != null;
            bool haveMission = FollowerRouteMissions.TryGetValue(profileId, out FollowerRouteMission mission) &&
                               mission != null && Time.time <= mission.ExpiresAt;
            float leaderDistance = GetPlanarDistance(bot.Position, leader.Position);
            if ((state.LearningV350TerminalRecoveryEligible))
            {
                // One-way ownership boundary: stale body jitter or route refresh may not recreate
                // a traversal executor before a real recovery boundary clears this latch. V3.53
                // additionally releases the short route-retry STOP lease: terminal recovery itself
                // is now the movement owner, otherwise Follow/NavigationRecovery can never age into
                // Suspected/Hard/Teleport and the bot waits forever at the failed entry.
                state.LearningV349GenericPassageLeaseUntil = -1f;
                state.LearningV349PhysicalStallSince = Time.time;
                if (haveMission)
                {
                    LearnedTraversalCommitments.Remove(profileId);
                    {
                        ReleaseLearningV353TerminalRecoveryRetryLease(bot, "genericPhysicalProgressWatchdog");
                        TryCF54HandoffTerminalRecoveryWriter(
                            bot, state, mission, "stickyTerminalRecovery");
                    }
                }
                return;
            }
            bool expectedToProgress = FollowerCapabilityHelperRules.UpdateLearningV349GenericPhysicalProgressWatchdog_expectedToProgress_958583D5_1(haveProbe,haveMission,leaderDistance,RouteMissionProactiveLeaderDistance);
            if (!expectedToProgress)
            {
                state.LearningV349PhysicalStallSince = -1f;
                state.LearningV349LastPhysicalPosition = bot.Position;
                state.LearningV349LastPhysicalProgressAt = Time.time;
                return;
            }

            if (state.LearningV349LastPhysicalProgressAt < 0f || !IsFinite(state.LearningV349LastPhysicalPosition))
            {
                state.LearningV349LastPhysicalPosition = bot.Position;
                state.LearningV349LastPhysicalProgressAt = Time.time;
                state.LearningV349PhysicalStallSince = Time.time;
                return;
            }

            float moved = Vector3.Distance(bot.Position, state.LearningV349LastPhysicalPosition);
            bool learningV386LocalBreadcrumbForwardProgress = false;
            if ((haveMission) && !haveProbe &&
                mission.CurrentStepIndex >= 0 && mission.CurrentStepIndex < mission.Steps.Count &&
                LearningV365ContinuousRouteProducerStates.TryGetValue(
                    profileId, out LearningV365ContinuousRouteProducerState learningV386WatchdogProducer) &&
                learningV386WatchdogProducer != null && !learningV386WatchdogProducer.Generic)
            {
                FollowerRouteMissionStep learningV386WatchdogStep = mission.Steps[mission.CurrentStepIndex];
                learningV386LocalBreadcrumbForwardProgress = learningV386WatchdogStep != null &&
                    learningV386WatchdogProducer.MissionId == mission.MissionId &&
                    learningV386WatchdogProducer.StepIndex == mission.CurrentStepIndex &&
                    learningV386WatchdogProducer.EdgeId == learningV386WatchdogStep.EdgeId &&
                    learningV386WatchdogProducer.LearningV386LastMeasuredForwardProgressAt >= 0f &&
                    Time.time - learningV386WatchdogProducer.LearningV386LastMeasuredForwardProgressAt <=
                        LearnedTraversalMissedFallbackNoProgressSeconds;
            }
            bool learningV355RouteRelativeProgress = false;
            bool learningV355RouteWrongWay = false;
            float learningV355EntryDistance = -1f;
            float learningV355RouteProjection = 0f;
            if ((haveMission) && !haveProbe &&
                !learningV386LocalBreadcrumbForwardProgress &&
                mission.CurrentStepIndex >= 0 && mission.CurrentStepIndex < mission.Steps.Count &&
                mission.Steps[mission.CurrentStepIndex] != null)
            {
                FollowerRouteMissionStep progressStep = mission.Steps[mission.CurrentStepIndex];
                Vector3 rawEntry = IsFinite(progressStep.RawEntry) ? progressStep.RawEntry : progressStep.Entry;
                Vector3 rawExit = IsFinite(progressStep.RawExit) ? progressStep.RawExit : progressStep.Exit;
                Vector3 axis = rawExit - rawEntry; axis.y = 0f;
                float span = axis.magnitude;
                Vector3 direction = span > 0.01f ? axis / span : Vector3.zero;
                Vector3 fromEntry = bot.Position - rawEntry; fromEntry.y = 0f;
                learningV355RouteProjection = span > 0.01f ? Vector3.Dot(fromEntry, direction) : 0f;
                learningV355EntryDistance = GetPlanarDistance(bot.Position, progressStep.Entry);
                if (mission.LearningV355ProgressStepIndex != mission.CurrentStepIndex)
                {
                    mission.LearningV355ProgressStepIndex = mission.CurrentStepIndex;
                    mission.LearningV355BestEntryDistance = learningV355EntryDistance;
                    mission.LearningV355BestRouteProjection = learningV355RouteProjection;
                    mission.LearningV355WrongWaySince = -1f;
                }
                bool entryImproved = FollowerCapabilityHelperRules.UpdateLearningV349GenericPhysicalProgressWatchdog_entryImproved_958583D5_2(learningV355EntryDistance,mission.LearningV355BestEntryDistance,LearningV354RouteDirectionalProgressEpsilon);
                bool projectionImproved = FollowerCapabilityHelperRules.UpdateLearningV349GenericPhysicalProgressWatchdog_projectionImproved_958583D5_3(learningV355RouteProjection,mission.LearningV355BestRouteProjection,LearningV354RouteDirectionalProgressEpsilon);
                if (entryImproved || projectionImproved)
                {
                    mission.LearningV355BestEntryDistance = Math.Min(mission.LearningV355BestEntryDistance, learningV355EntryDistance);
                    mission.LearningV355BestRouteProjection = Math.Max(mission.LearningV355BestRouteProjection, learningV355RouteProjection);
                    mission.LearningV355WrongWaySince = -1f;
                    learningV355RouteRelativeProgress = true;
                }
                bool projectionRegressed = FollowerCapabilityHelperRules.UpdateLearningV349GenericPhysicalProgressWatchdog_projectionRegressed_958583D5_4(learningV355RouteProjection,mission.LearningV355BestRouteProjection,LearningV354RouteWrongWayProjectionSlack);
                bool entryRegressed = FollowerCapabilityHelperRules.UpdateLearningV349GenericPhysicalProgressWatchdog_entryRegressed_958583D5_5(learningV355EntryDistance,mission.LearningV355BestEntryDistance,LearningV354RouteWrongWayEntryDistanceSlack);
                if (projectionRegressed && entryRegressed)
                {
                    if (mission.LearningV355WrongWaySince < 0f) mission.LearningV355WrongWaySince = Time.time;
                    learningV355RouteWrongWay = Time.time - mission.LearningV355WrongWaySince >= LearningV354RouteWrongWayGraceSeconds;
                }
                else if (!projectionRegressed || !entryRegressed)
                {
                    mission.LearningV355WrongWaySince = -1f;
                }
                if (learningV355RouteWrongWay && ShouldEmitDiagnostic(
                    "learningV355RouteWrongWay:" + mission.MissionId.ToString(CultureInfo.InvariantCulture), bot, 0.35f))
                {
                    WriteEvent("learningV355RouteWrongWay", bot, new
                    {
                        missionId = mission.MissionId, stepIndex = mission.CurrentStepIndex,
                        edgeId = progressStep.EdgeId, kind = progressStep.Kind,
                        entryDistance = learningV355EntryDistance, bestEntryDistance = mission.LearningV355BestEntryDistance,
                        routeProjection = learningV355RouteProjection, bestRouteProjection = mission.LearningV355BestRouteProjection,
                        worldSpeedEvidence = moved,
                        action = "worldMotionDoesNotRefreshRouteProgressSwitchProducerTowardRecovery"
                    });
                    // Age only the no-progress observation, not traversal truth. The existing V3.49/V3.50
                    // bounded self-rescue/physical-passage/terminal chain owns the actual recovery.
                    if (state.LearningV349PhysicalStallSince < 0f)
                        state.LearningV349PhysicalStallSince = Time.time - LearnedTraversalMissedFallbackNoProgressSeconds;
                }
            }
            bool genericPhysicalLeaseOwnsProgressProof =
                (!haveProbe) &&
                Time.time <= state.LearningV349GenericPassageLeaseUntil;
            bool worldMotionProgress = moved >= ((LearningV350MeasuredPhysicalProgressDistance));
            bool movementCountsAsProgress = FollowerCapabilityHelperRules.UpdateLearningV349GenericPhysicalProgressWatchdog_movementCountsAsProgress_958583D5_6(haveMission,haveProbe,learningV386LocalBreadcrumbForwardProgress,learningV355RouteRelativeProgress,worldMotionProgress);
            if (!genericPhysicalLeaseOwnsProgressProof && !learningV355RouteWrongWay && movementCountsAsProgress)
            {
                state.LearningV349LastPhysicalPosition = bot.Position;
                state.LearningV349LastPhysicalProgressAt = Time.time;
                // V2-RC20: the existing route-relative measured-body watchdog is
                // the observation side of the SAME Authority-issued physical input.
                // This is audit evidence only: no success, cursor, or Recovery write.
                FollowerMovementWriterAuthority.TryObserveIssuedPhysicalProgress(
                    bot, moved, "LearningV355RouteRelativeBodyWatchdog");
                state.LearningV349PhysicalStallSince = -1f;
                state.LearningV349PhysicalRecoveryAttempts = 0;
                if ((((!state.LearningV350TerminalRecoveryEligible))))
                {
                    state.LearningV350TerminalRecoveryEligible = false;
                    state.LearningV350TerminalRecoverySince = -1f;
                    state.LearningV350TerminalRecoveryReason = string.Empty;
                }
                return;
            }

            if (state.LearningV349PhysicalStallSince < 0f)
            {
                state.LearningV349PhysicalStallSince = Time.time;
                return;
            }

            // Observations from a previous attempt cannot terminalize a newly dispatched actuator.
            // This bounds observation age; it does not renew progress or grant movement/success.
            float stalledFor = (float)FollowerCapabilityPassagePolicy.AttemptStallAge(
                haveProbe&&IsProbeLifecycleCurrent(probe),Time.time,state.LearningV349PhysicalStallSince,probe?.RequestedAt??0f);
            // CF38: the CF37 run had a valid 13 m Follow route at 585.315, but its
            // native mover lost the path at 586.699 while the bot was still 11 m behind.
            // Re-publish the existing Follow consumer once before the 3 s generic rescue;
            // never infer physical progress from NavMeshPathStatus alone.
            if (!haveProbe && !haveMission &&
                ((!state.LearningV350TerminalRecoveryEligible)))
            {
                TryCF38ReprimeLostFollowConsumerPath(
                    bot, leader, state, leaderDistance, stalledFor);
            }
            if (stalledFor < LearnedTraversalMissedFallbackNoProgressSeconds)
            {
                return;
            }

            if ((haveMission) && !haveProbe &&
                mission.CurrentStepIndex >= 0 && mission.CurrentStepIndex < mission.Steps.Count)
            {
                FollowerRouteMissionStep completedPassage = mission.Steps[mission.CurrentStepIndex];
                LearningV354TraversalCapabilityProfile completedProfile = completedPassage != null
                    ? GetLearningV354ActionProfile(completedPassage.Kind)
                    : null;
                if (completedPassage != null && completedProfile != null &&
                    completedProfile.PhysicalPassageCanProveCompletion &&
                    IsFollowerRouteMissionStepPhysicallyPassed(
                        bot, completedPassage, out float completedProjection,
                        out float completedRequiredProjection))
                {
                    int previousStepIndex = mission.CurrentStepIndex;
                    int previousExecutionFloor = GetFollowerRouteExecutionFloor(profileId);
                    int newExecutionFloor = Math.Max(
                        previousExecutionFloor, completedPassage.EndBreadcrumbSequence);
                    FollowerRouteExecutionFloors[profileId] = newExecutionFloor;
                    mission.ResumeFloorBreadcrumbSequence = Math.Max(
                        mission.ResumeFloorBreadcrumbSequence, newExecutionFloor);
                    ConsumeFollowerRouteMissionPhysicalPass(
                        bot, mission, completedPassage,
                        "learningV388WatchdogPhysicalPassBeforeSelfRescue",
                        completedProjection, completedRequiredProjection);
                    MarkLearningV388PhysicalPassageExecutionDebtConsumed(
                        bot, completedPassage,
                        "learningV388WatchdogPhysicalPassBeforeSelfRescue");
                    MarkFollowerTraversalEpisodeCompleted(
                        bot, completedPassage.EpisodeId, completedPassage.EdgeId);
                    // V3.90: MarkFollowerTraversalEpisodeCompleted is the sole RouteMission
                    // advancement writer. It already advances CurrentStepIndex across consumed
                    // episodes; incrementing here a second time skipped the real actuator successor.
                    int learningV390AdvancedStepIndex = mission.CurrentStepIndex;
                    mission.LastProgressAt = Time.time;
                    mission.LastProgressPosition = bot.Position;
                    CommitCapabilityMoveRetry(mission,FollowerCapabilityRetryPolicy.Change.Reset,0f);
                    mission.TraversalRetryMovementHoldUntil = 0f;
                    mission.LearningV355ProgressStepIndex = -1;
                    state.LearningV349PhysicalStallSince = -1f;
                    state.LearningV349PhysicalRecoveryAttempts = 0;
                    state.LearningV349LastPhysicalPosition = bot.Position;
                    state.LearningV349LastPhysicalProgressAt = Time.time;
                    state.LearningV350GenericPassageArmedAt = -1f;
                    state.LearningV349GenericPassageLeaseUntil = -1f;
                    ClearLearningV365ContinuousPhysicalRoute(
                        bot, "learningV388WatchdogPhysicalPassBeforeSelfRescue");
                    WriteEvent("learningV388WatchdogPhysicalPassConsumedBeforeSelfRescue", bot, new
                    {
                        missionId = mission.MissionId,
                        previousStepIndex,
                        currentStepIndex = mission.CurrentStepIndex,
                        learningV390AdvancedStepIndex,
                        learningV390SingleAdvanceAuthority = true,
                        episodeId = completedPassage.EpisodeId,
                        edgeId = completedPassage.EdgeId,
                        kind = completedPassage.Kind,
                        projection = completedProjection,
                        requiredProjection = completedRequiredProjection,
                        previousExecutionFloor,
                        newExecutionFloor,
                        traversalSuccessRecorded = false,
                        persistentLearningChanged = false,
                        action = "bodyAlreadyCrossedPhysicalPassageConsumeExecutionDebtInsteadOfRewindingIntoFallbackActuator"
                    });
                    {
                        WriteEvent("learningV390PhysicalPassSingleAdvanceAuthority", bot, new
                        {
                            missionId = mission.MissionId,
                            previousStepIndex,
                            currentStepIndex = mission.CurrentStepIndex,
                            episodeId = completedPassage.EpisodeId,
                            edgeId = completedPassage.EdgeId,
                            kind = completedPassage.Kind,
                            advancementWriter = "MarkFollowerTraversalEpisodeCompleted",
                            manualSecondAdvance = false,
                            persistentLearningChanged = false,
                            action = "physicalPassCompletionAdvancesExecutableMissionExactlyOnce"
                        });
                    }
                    UpdateLearningV375PersistentExecutablePhysicalRouteTransaction(bot);
                    return;
                }
            }

            if ((haveMission) && !haveProbe &&
                mission.CurrentStepIndex >= 0 && mission.CurrentStepIndex < mission.Steps.Count)
            {
                FollowerRouteMissionStep actuatorFrontierStep = mission.Steps[mission.CurrentStepIndex];
                if (actuatorFrontierStep != null && !IsLearningV369PhysicalCorridorStep(actuatorFrontierStep) &&
                    TryAdoptLearningV386ProvenSiblingActuatorStrategy(bot, mission, actuatorFrontierStep))
                {
                    state.LearningV349PhysicalStallSince = Time.time;
                    state.LearningV350GenericPassageArmedAt = -1f;
                    state.LearningV349GenericPassageLeaseUntil = -1f;
                    return;
                }
            }

            if ((haveMission) && !haveProbe &&
                mission.CurrentStepIndex >= 0 && mission.CurrentStepIndex < mission.Steps.Count)
            {
                FollowerRouteMissionStep physicalStep = mission.Steps[mission.CurrentStepIndex];
                int maxActuatorFallbacks = Math.Max(1, LearningV32EntryMaxReacquireAttempts);
                LearnedTraversalEdge physicalEdge = physicalStep != null
                    ? LearnedTraversalEdges.FirstOrDefault(candidate => candidate != null && candidate.Id == physicalStep.EdgeId)
                    : null;
                if (physicalStep != null && IsLearningV369PhysicalCorridorStep(physicalStep) &&
                    !physicalStep.LearningV384ActuatorFallbackArmed &&
                    physicalStep.LearningV384ActuatorFallbackAttempts <= maxActuatorFallbacks &&
                    physicalEdge != null &&
                    TryGetLearningV381ProvenPortalActuatorStrategy(
                        physicalEdge, physicalStep, out LearnedTraversalEdge learningV384FallbackActuator,
                        out bool learningV384FallbackForward, out float learningV384FallbackMatchScore,
                        out string learningV384FallbackMatchSource))
                {
                    physicalStep.LearningV384ActuatorFallbackArmed = true;
                    CommitCapabilityMoveRetry(mission,FollowerCapabilityRetryPolicy.Change.Reset,0f);
                    mission.TraversalRetryMovementHoldUntil = 0f;
                    // Producer changes do not fabricate body progress. They receive only the existing
                    // watchdog window, and the attempt count is bounded by the pre-existing V3.2
                    // reacquire budget so this can never become an actuator/physical infinite loop.
                    state.LearningV349PhysicalStallSince = Time.time;
                    state.LearningV350GenericPassageArmedAt = -1f;
                    state.LearningV349GenericPassageLeaseUntil = -1f;
                    WriteEvent("learningV384PhysicalPassageActuatorSelfRescueArmed", bot, new
                    {
                        missionId = mission.MissionId,
                        stepIndex = mission.CurrentStepIndex,
                        episodeId = physicalStep.EpisodeId,
                        physicalEdgeId = physicalStep.EdgeId,
                        actuatorEdgeId = learningV384FallbackActuator?.Id ?? 0,
                        actuatorKind = learningV384FallbackActuator?.Kind ?? string.Empty,
                        forward = learningV384FallbackForward,
                        portalMatchScore = learningV384FallbackMatchScore,
                        portalMatchSource = learningV384FallbackMatchSource ?? string.Empty,
                        fallbackAttempts = physicalStep.LearningV384ActuatorFallbackAttempts,
                        maxFallbackRetries = maxActuatorFallbacks,
                        stalledFor,
                        traversalSuccessRecorded = false,
                        persistentLearningChanged = false,
                        action = "demonstratedRunSprintPassageStalledUseProvenActuatorAsBoundedSelfRescue"
                    });

                    // V3.84 armed the fallback but relied on a later commitment pass to consume it.
                    // Continuous PhysicalCorridor ownership can keep that later pass from occurring,
                    // leaving a log-only self-rescue. V3.85 performs the already-authorized strategy
                    // transition now; the existing bounded fallback count and Native entry contract
                    // still own whether/when the actuator actually fires.
                    if ((TryAdoptLearningV381ProvenPortalActuatorStrategy(bot, mission, physicalStep)))
                    {
                        WriteEvent("learningV385PhysicalPassageActuatorSelfRescueDispatched", bot, new
                        {
                            missionId = mission.MissionId,
                            stepIndex = mission.CurrentStepIndex,
                            episodeId = physicalStep.EpisodeId,
                            physicalEdgeId = physicalStep.EdgeId,
                            actuatorEdgeId = learningV384FallbackActuator?.Id ?? 0,
                            actuatorKind = learningV384FallbackActuator?.Kind ?? string.Empty,
                            stalledFor,
                            traversalSuccessRecorded = false,
                            persistentLearningChanged = false,
                            action = "armedBoundedPortalActuatorBecomesCurrentExecutionStrategyWithoutWaitingForProducerReentry"
                        });
                    }
                    return;
                }
            }

            state.LearningV349PhysicalRecoveryAttempts++;
            string context = haveProbe
                ? "activeTraversal:" + probe.Stage
                : haveMission ? "routeMission" : "untypedFollow";
            state.LearningV349LastPhysicalContext = context;

            if ((haveProbe) &&
                probe.LearningV349PhysicalPassageCrossed &&
                (probe.Stage == ProbeStage.ExitCommit || probe.Stage == ProbeStage.Verify || probe.Stage == ProbeStage.Egress) &&
                TryFinalizeTraversalChainIntermediate(
                    probe, "learningV349GenericPhysicalProgressWatchdogFarSide"))
            {
                WriteEvent("learningV349FarSideAtomicSuccessorHandoff", bot, new
                {
                    edgeId = probe.LearnedEdgeId,
                    kind = probe.TraversalKind ?? probe.RequestedTraversalKind,
                    stage = probe.Stage.ToString(),
                    stalledFor,
                    recoveryAttempt = state.LearningV349PhysicalRecoveryAttempts,
                    action = "closeCrossedPhysicalTransactionAndTransferToKnownSuccessor"
                });
                state.LearningV349PhysicalStallSince = -1f;
                state.LearningV349LastPhysicalPosition = bot.Position;
                state.LearningV349LastPhysicalProgressAt = Time.time;
                return;
            }

            if (haveProbe && !haveMission)
            {
                // The legacy V3.36 rescue requires a RouteMission. Give a direct/known-edge probe
                // one opportunity to recover its authored player-route context before declaring
                // that the current executor transaction itself is the stale object.
                TryArmOrRefreshFollowerRouteMission(
                    bot, leader.Position, "learningV349GenericPhysicalProgressWatchdog", true);
                haveMission = FollowerRouteMissions.TryGetValue(profileId, out mission) &&
                              mission != null && Time.time <= mission.ExpiresAt;
            }

            if(haveProbe&&IsProbeLifecycleCurrent(probe)&&probe.LifecycleMission!=null)
            {
                // A typed WT stall is a Native failure too. It must not escape into the old
                // untyped physical-producer switch and skip Alternate/SelfRescue/Return.
                TryRequestCapabilityFullRouteRetry(probe,"genericWatchdogCurrentTypedAttemptStalled",true);
                return;
            }
            bool learningV380YieldPassageProbeDirectly =
                (haveProbe) &&
                (FollowerCapabilityHelperRules.UpdateLearningV349GenericPhysicalProgressWatchdog_learningV380YieldPassageProbeDirectly_958583D5_7(probe.TraversalKind,probe.RequestedTraversalKind,probe.TraversalKind,probe.RequestedTraversalKind));
            if (haveProbe && !learningV380YieldPassageProbeDirectly &&
                TryArmLearningV336TraversalSelfRescue(
                    probe,
                    "learningV349GenericPhysicalProgressWatchdog",
                    probe.Stage.ToString()))
            {
                WriteEvent("learningV349GenericPhysicalProgressRecoveryEscalated", bot, new
                {
                    context,
                    edgeId = probe.LearnedEdgeId,
                    kind = probe.TraversalKind ?? probe.RequestedTraversalKind,
                    stalledFor,
                    recoveryAttempt = state.LearningV349PhysicalRecoveryAttempts,
                    action = "reuseBoundedRouteLevelSelfRescueWithoutTeleport"
                });
                // V3.50: arming another executor is not physical progress. Give the new executor
                // one fresh watchdog window, but keep the last measured body-progress timestamp
                // unchanged so it cannot indefinitely defer terminal recovery by retrying commands.
                state.LearningV349PhysicalStallSince = Time.time;
                return;
            }

            if (haveProbe)
            {
                if (learningV380YieldPassageProbeDirectly)
                {
                    WriteEvent("learningV380WalkThroughStallYieldedToPhysicalProducer", bot, new
                    {
                        edgeId = probe.LearnedEdgeId,
                        kind = probe.TraversalKind ?? probe.RequestedTraversalKind,
                        stage = probe.Stage.ToString(),
                        stalledFor,
                        recoveryAttempt = state.LearningV349PhysicalRecoveryAttempts,
                        traversalSuccessRecorded = false,
                        persistentLearningChanged = false,
                        action = "retireNonProgressingSemanticExecutorOnceContinueSameCurrentEpisodePhysicalRoute"
                    });
                }
                // If the typed transaction cannot arm the bounded route-level rescue (no matching
                // mission, exhausted V3.36 budget, or a stale executor lifecycle), release only the
                // executor transaction and switch to the demonstrated physical corridor. This is not
                // traversal success and does not mutate persistent semantic truth.
                int releasedEdgeId = probe.LearnedEdgeId;
                string releasedKind = probe.TraversalKind ?? probe.RequestedTraversalKind ?? string.Empty;
                string releasedStage = probe.Stage.ToString();
                bool crossedBeforeRelease = probe.LearningV349PhysicalPassageCrossed;
                ForceReleaseTraversalProbe(
                    probe, "learningV349GenericPhysicalProgressProducerSwitch");
                haveProbe = false;
                if ((state.LearningV350GenericPassageArmedAt < 0f) ||
                    Time.time > state.LearningV349GenericPassageLeaseUntil)
                {
                    state.LearningV350GenericPassageArmedAt = Time.time;
                    state.LearningV350GenericPassageArmCursorSequence =
                        Math.Max(0, state.LearningV349GenericPassageCursorSequence);
                }
                state.LearningV349GenericPassageLeaseUntil = Mathf.Max(
                    state.LearningV349GenericPassageLeaseUntil,
                    Time.time + RouteMissionHardRecoverySeconds);
                if (state.LearningV349GenericPassageCursorSequence <= 0)
                {
                    state.LearningV349GenericPassageCursorSequence =
                        Math.Max(0, FindFollowerRouteStartBreadcrumbSequence(bot));
                    if (state.LearningV350GenericPassageArmCursorSequence <= 0)
                    {
                        state.LearningV350GenericPassageArmCursorSequence =
                            state.LearningV349GenericPassageCursorSequence;
                    }
                }
                WriteEvent("learningV349StaleTraversalExecutorReleasedToPhysicalRoute", bot, new
                {
                    edgeId = releasedEdgeId,
                    kind = releasedKind,
                    stage = releasedStage,
                    crossedBeforeRelease,
                    stalledFor,
                    recoveryAttempt = state.LearningV349PhysicalRecoveryAttempts,
                    persistentLearningChanged = false,
                    traversalSuccessRecorded = false,
                    action = "retireStaleExecutorTransactionContinuePlayerPhysicalRouteWithoutTeleport"
                });
            }

            if (!haveProbe)
            {
                bool genericPassageAlreadyArmed =
                    (state.LearningV350GenericPassageArmedAt >= 0f) &&
                    Time.time <= state.LearningV349GenericPassageLeaseUntil;
                float genericProgressReference = Mathf.Max(
                    state.LearningV350GenericPassageArmedAt,
                    state.LearningV349LastPhysicalProgressAt);
                bool genericPassageMadeNoMeasuredProgress =
                    genericPassageAlreadyArmed &&
                    Time.time - genericProgressReference >= LearnedTraversalMissedFallbackNoProgressSeconds;

                if (genericPassageMadeNoMeasuredProgress)
                {
                    state.LearningV350TerminalRecoveryEligible = true;
                    state.LearningV350TerminalRecoverySince = Time.time;
                    state.LearningV350TerminalRecoveryReason =
                        "genericPhysicalPassageNoMeasuredProgress";
                    CaptureLearningV362TerminalRecoveryEpoch(
                        bot, state, "genericPhysicalPassageNoMeasuredProgress");
                    // V2-RC21: snapshot OLD writer origin BEFORE CF54 hands it
                    // to Follow, avoiding false attribution of stale progress to
                    // a newly minted Recovery/Follow generation.
                    var physicalEvidenceBeforeRecovery =
                        FollowerMovementWriterAuthority.SnapshotPhysicalEvidenceForDiagnostics(bot);
                    if (haveMission)
                    {
                        TryCF54HandoffTerminalRecoveryWriter(
                            bot, state, mission, "genericPhysicalPassageNoMeasuredProgress");
                    }
                    state.LearningV349GenericPassageLeaseUntil = -1f;
                    state.LearningV349PhysicalStallSince = Time.time;
                    if (haveMission)
                    {
                        CommitCapabilityMoveRetry(mission,FollowerCapabilityRetryPolicy.Change.Reset,0f);
                        mission.TraversalRetryMovementHoldUntil = 0f;
                    }
                    WriteEvent("learningV350TerminalRecoveryLatched", bot, new
                    {
                        context,
                        missionId = haveMission ? mission.MissionId : 0,
                        stepIndex = haveMission ? mission.CurrentStepIndex : -1,
                        leaderDistance,
                        stalledFor,
                        recoveryAttempt = state.LearningV349PhysicalRecoveryAttempts,
                        cursorSequence = state.LearningV349GenericPassageCursorSequence,
                        armCursorSequence = state.LearningV350GenericPassageArmCursorSequence,
                        terminalRecoveryEpochEpisodeId = state.LearningV362TerminalRecoveryLatchEpisodeId,
                        terminalRecoveryEpochBreadcrumbSequence = state.LearningV362TerminalRecoveryLatchBreadcrumbSequence,
                        terminalRecoveryEpochAt = state.LearningV362TerminalRecoveryLatchAt,
                        genericPassageArmedAt = state.LearningV350GenericPassageArmedAt,
                        lastMeasuredPhysicalProgressAt = state.LearningV349LastPhysicalProgressAt,
                        noMeasuredProgressSeconds = Time.time - genericProgressReference,
                        physicalEvidenceOwnerBeforeHandoff = physicalEvidenceBeforeRecovery.Owner,
                        physicalEvidenceGenerationBeforeHandoff = physicalEvidenceBeforeRecovery.Generation,
                        physicalEvidenceStateBeforeHandoff = physicalEvidenceBeforeRecovery.EvidenceState,
                        physicalFirstInputAgeSeconds = physicalEvidenceBeforeRecovery.FirstInputAgeSeconds,
                        physicalLatestInputAgeSeconds = physicalEvidenceBeforeRecovery.LatestInputAgeSeconds,
                        physicalLatestProgressAgeSeconds = physicalEvidenceBeforeRecovery.LatestProgressAgeSeconds,
                        physicalFirstLaterFrameProgressObserved = physicalEvidenceBeforeRecovery.FirstLaterFrameProgressObserved,
                        physicalEvidenceIsDiagnosticOnly = true,
                        action = "yieldPhysicalRouteAuthorityToTerminalRecovery"
                    });
                    return;
                }

                if (!genericPassageAlreadyArmed)
                {
                    state.LearningV350GenericPassageArmedAt = Time.time;
                    state.LearningV350GenericPassageArmCursorSequence =
                        Math.Max(0, state.LearningV349GenericPassageCursorSequence);
                    state.LearningV349GenericPassageLeaseUntil =
                        Time.time + RouteMissionHardRecoverySeconds;
                }
                else ;
                if (state.LearningV349GenericPassageCursorSequence <= 0)
                {
                    state.LearningV349GenericPassageCursorSequence =
                        Math.Max(0, FindFollowerRouteStartBreadcrumbSequence(bot));
                    if (state.LearningV350GenericPassageArmCursorSequence <= 0)
                    {
                        state.LearningV350GenericPassageArmCursorSequence =
                            state.LearningV349GenericPassageCursorSequence;
                    }
                }
                if (haveMission)
                {
                    CommitCapabilityMoveRetry(mission,FollowerCapabilityRetryPolicy.Change.Reset,0f);
                    mission.TraversalRetryMovementHoldUntil = 0f;
                }
                WriteEvent("learningV349GenericPhysicalProgressWatchdogArmedPassage", bot, new
                {
                    context,
                    missionId = haveMission ? mission.MissionId : 0,
                    stepIndex = haveMission ? mission.CurrentStepIndex : -1,
                    leaderDistance,
                    stalledFor,
                    recoveryAttempt = state.LearningV349PhysicalRecoveryAttempts,
                    cursorSequence = state.LearningV349GenericPassageCursorSequence,
                    armCursorSequence = state.LearningV350GenericPassageArmCursorSequence,
                    genericPassageArmedAt = state.LearningV350GenericPassageArmedAt,
                    action = "switchToCollisionActivePlayerBreadcrumbProducerWithoutTeleportOrSemanticMutation"
                });
            }

            // V3.50: changing producer/lease state is not body progress. Restart only the stall
            // observation window; the measured-progress clock remains unchanged and therefore
            // provides a monotonic path to terminal recovery if the body stays jammed.
            state.LearningV349PhysicalStallSince = Time.time;
        }

        private static void UpdateFollowerMovementStateRecovery(BotOwner bot)
        {
            string profileId = bot?.ProfileId ?? string.Empty;
            Player player = bot?.GetPlayer;
            if (string.IsNullOrEmpty(profileId) || player?.MovementContext == null)
            {
                return;
            }

            if (!FollowerMovementRecoveryStates.TryGetValue(profileId, out FollowerMovementRecoveryState state) || state == null)
            {
                state = new FollowerMovementRecoveryState
                {
                    LastPosition = bot.Position,
                    LastMovedAt = Time.time,
                    LearningV349LastPhysicalPosition = bot.Position,
                    LearningV349LastPhysicalProgressAt = Time.time
                };
                FollowerMovementRecoveryStates[profileId] = state;
            }

            UpdateLearningV349GenericPhysicalProgressWatchdog(bot, player, state);

            if (player.CurrentStateName != EPlayerState.JumpLanding)
            {
                state.JumpLandingSince = -1f;
                state.RepairAttempts = 0;
                state.HardRecoveryAttempted = false;
                FollowerStateAuthorityCoordinator.ClearLocomotionFault(
                    bot, "jumpLandingWatchdogStateReleased");

                TryNeutralizeLearningV432StationaryVisualLocomotionResidue(bot, player);

                // CompileFix48 low-level locomotion health watch. This specifically targets the
                // Fix47 failure signature: BotMover has a real incomplete path, isMoving=true,
                // forward input is asserted, the body is grounded, yet world position does not move.
                // Do not run during traversal/teleport transactions or when the leader is already close.
                BotFollowerPlayer followerState = BossPlayers.GetFollowerByProfileId(profileId);
                PathControllerClass pathController = bot.Mover?.ActualPathController;
                Player leader = FollowerEnvironment.LocalPlayer;
                float leaderDistance = IsLivePlayer(leader) && IsFinite(leader.Position)
                    ? GetPlanarDistance(bot.Position, leader.Position)
                    : 0f;
                bool forwardIntent = player.InputDirection.y > 0.30f ||
                                     player.MovementContext.MovementDirection.y > 0.30f;
                bool genericFollowLocomotionCandidate =
                    followerState?.IsTeleportStabilizing != true &&
                    !HasActiveTraversalProbe(bot) &&
                    !IsTeleportTransactionActive(bot) &&
                    !FollowerStateAuthorityCoordinator.HasCombatMovementClaim(bot, out _) &&
                    player.MovementContext.IsGrounded &&
                    bot.Mover?.Pause != true &&
                    bot.Mover?.Blocked != true &&
                    bot.DoorOpener?.Interacting != true &&
                    bot.Mover?.IsMoving == true &&
                    bot.Mover?.HasPathAndNoComplete == true &&
                    pathController?.HavePath == true &&
                    forwardIntent &&
                    leaderDistance > 2.5f;

                long learningV401MovementGeneration = FollowerMovementWriterAuthority.GetCurrentGeneration(
                    bot, out FollowerMovementWriterAuthority.WriterDomain learningV401MovementOwner);
                bool learningV401PersistentRouteTransactionActive =
                    FollowerTraversalRouteCursors.TryGetValue(profileId, out FollowerTraversalRouteCursor learningV401Cursor) &&
                    learningV401Cursor != null && learningV401Cursor.LearningV375TransactionActive;
                bool learningV401CanonicalRouteOwnsLocomotion =
                    ((learningV401MovementOwner == FollowerMovementWriterAuthority.WriterDomain.Traversal ||
                     learningV401MovementOwner == FollowerMovementWriterAuthority.WriterDomain.PostTraversal ||
                     learningV401PersistentRouteTransactionActive));
                bool learningV402TerminalRecoveryOwnsLocomotion =
                    (IsLearningV350TerminalPhysicalRecoveryEligible(bot));
                bool learningV402ExceptionalLocomotionOwner =
                    learningV401CanonicalRouteOwnsLocomotion || learningV402TerminalRecoveryOwnsLocomotion;

                float generalMoved = Vector3.Distance(bot.Position, state.LastPosition);

                // CF78: CF75 proved that an already-committed Follow consumer can own an exact
                // PathComplete epoch while the grounded body still has zero world-space progress
                // until the generic 0.85 s stall watchdog clears residual Native animation state.
                // Reuse that SAME established recovery action at an earlier, physically observed
                // handoff boundary; do not mint a writer, resubmit GoToPoint, declare a successor
                // successful, change Native admission, or synthesize/force sprinting movement.
                // No unconditional repair on path commit: require a stable grounded body, current
                // Follow generation, no live traversal/terminal/combat/teleport owner, and real
                // zero displacement after a bounded settling interval. One attempt per generation;
                // the existing 0.85 s watchdog remains available if correction does not move us.
                if (genericFollowLocomotionCandidate &&
                    !learningV402ExceptionalLocomotionOwner &&
                    generalMoved < 0.055f &&
                    (player.CurrentStateName == EPlayerState.Run ||
                     player.CurrentStateName == EPlayerState.Sprint) &&
                    CF75FollowPhysicalWatches.TryGetValue(
                        profileId, out CF75FollowPhysicalWatch cf78FollowWatch) &&
                    cf78FollowWatch != null &&
                    !cf78FollowWatch.CF78ZeroProgressCorrectionAttempted &&
                    !cf78FollowWatch.FirstPhysicalSeen &&
                    cf78FollowWatch.RaidGeneration == raidGeneration &&
                    cf78FollowWatch.FollowGeneration == learningV401MovementGeneration &&
                    learningV401MovementGeneration > 0 &&
                    learningV401MovementOwner == FollowerMovementWriterAuthority.WriterDomain.Follow &&
                    string.Equals(cf78FollowWatch.Source, "post-traversal-fresh-epoch",
                        StringComparison.Ordinal) &&
                    Time.time - cf78FollowWatch.StartedAt >= 0.42f &&
                    Time.time - cf78FollowWatch.StartedAt <= 1.20f &&
                    GetPlanarDistance(bot.Position, cf78FollowWatch.StartBody) < 0.055f &&
                    Mathf.Abs(bot.Position.y - cf78FollowWatch.StartBody.y) < 0.15f &&
                    !FollowerRouteMissions.ContainsKey(profileId) &&
                    FollowerMovementWriterAuthority.PeekOwnerGenerationForCF75(
                        bot, out FollowerMovementWriterAuthority.WriterDomain cf78PeekOwner) ==
                            cf78FollowWatch.FollowGeneration &&
                    cf78PeekOwner == FollowerMovementWriterAuthority.WriterDomain.Follow)
                {
                    // Mark before invoking any existing recovery helper: even an exception must
                    // not permit repeated producer writes within the same Follow epoch.
                    cf78FollowWatch.CF78ZeroProgressCorrectionAttempted = true;
                    bool cf78Requested = TryRecoverGeneralFollowerLocomotion(
                        bot, "cf78FreshFollowZeroProgress", out string cf78Detail);
                    WriteEvent("cf78FreshFollowZeroProgressCorrectionAttempted", bot, new
                    {
                        raidGeneration,
                        followGeneration = cf78FollowWatch.FollowGeneration,
                        observedOwner = cf78PeekOwner.ToString(),
                        handoffAgeSeconds = Time.time - cf78FollowWatch.StartedAt,
                        bodyDisplacementPlanar = GetPlanarDistance(
                            bot.Position, cf78FollowWatch.StartBody),
                        grounded = player.MovementContext.IsGrounded,
                        pathCompleteConsumer = true,
                        locomotionRecoveryRequested = cf78Requested,
                        detail = cf78Detail,
                        traversalSuccessRecorded = false,
                        persistentLearningChanged = false,
                        bodyRelocated = false,
                        action = "oneShotExistingGeneralLocomotionRecoveryAfterPhysicalZeroProgressNoNewWriterOrPath"
                    });
                    // Do not stamp progress or claim seamlessness; the CF75 body ledger measures
                    // whether the correction actually resulted in subsequent physical motion.
                }

                if (learningV402ExceptionalLocomotionOwner && genericFollowLocomotionCandidate &&
                    generalMoved < 0.055f)
                {
                    string learningV402GenericYieldEvent = learningV402TerminalRecoveryOwnsLocomotion
                        ? "learningV402GenericLocomotionRecoveryYieldedToTerminalRecoveryAuthority"
                        : "learningV401GenericLocomotionRecoveryYieldedToCanonicalRouteAuthority";
                    if (ShouldEmitDiagnostic(learningV402GenericYieldEvent, bot, 0.90f))
                    {
                        WriteEvent(learningV402GenericYieldEvent, bot, new
                        {
                            movementOwner = learningV401MovementOwner.ToString(),
                            movementGeneration = learningV401MovementGeneration,
                            persistentRouteTransactionActive = learningV401PersistentRouteTransactionActive,
                            terminalRecoveryAuthority = learningV402TerminalRecoveryOwnsLocomotion,
                            position = Vec(bot.Position),
                            leaderDistance,
                            hasPath = pathController?.HavePath == true,
                            hasPathAndNoComplete = bot.Mover?.HasPathAndNoComplete == true,
                            isMoving = bot.Mover?.IsMoving == true,
                            inputDirection = Vec2(player.InputDirection),
                            state = player.CurrentStateName.ToString(),
                            traversalSuccessRecorded = false,
                            persistentLearningChanged = false,
                            movementStateMutated = false,
                            action = learningV402TerminalRecoveryOwnsLocomotion
                                ? "genericFollowLocomotionWatchdogMustNotOverwriteTerminalRecoveryAuthority"
                                : "genericFollowLocomotionWatchdogMustNotOverwriteCanonicalRouteLocomotionFacingOrAnimator"
                        });
                    }
                    // CF45: V402's blanket diagnostic-only yield masked a real Follow
                    // consumer that inherited the finished DropDown's old native corner.
                    // Keep canonical recovery authority, but verify body liveness and
                    // repair ONLY a proven stale physical-epoch corner once per Follow gen.
                    bool cf45TerminalFollowWitness =
                        learningV402TerminalRecoveryOwnsLocomotion &&
                        !learningV401CanonicalRouteOwnsLocomotion &&
                        learningV401MovementOwner ==
                            FollowerMovementWriterAuthority.WriterDomain.Follow &&
                        learningV401MovementGeneration > 0 && IsLivePlayer(leader);
                    if (cf45TerminalFollowWitness)
                    {
                        if (state.GeneralLocomotionStallSince < 0f)
                        {
                            state.GeneralLocomotionStallSince = Time.time;
                        }
                        else if (Time.time - state.GeneralLocomotionStallSince >= 0.85f &&
                                 Time.time >= state.NextGeneralLocomotionRepairAt &&
                                 TryCF45RepairStaleTerminalFollowConsumer(
                                     bot, leader, state, learningV401MovementGeneration,
                                     leaderDistance))
                        {
                            state.NextGeneralLocomotionRepairAt = Time.time + 1.25f;
                            state.GeneralLocomotionStallSince = -1f;
                        }
                    }
                    else
                    {
                        state.GeneralLocomotionStallSince = -1f;
                    }
                    state.GeneralLocomotionRepairAttempts = 0;
                    state.LastPosition = bot.Position;
                    state.LastMovedAt = Time.time;
                    return;
                }

                bool lowLevelExpectedToMove = genericFollowLocomotionCandidate &&
                    !learningV402ExceptionalLocomotionOwner;
                if (!lowLevelExpectedToMove || generalMoved >= 0.055f)
                {
                    state.GeneralLocomotionStallSince = -1f;
                    state.GeneralLocomotionRepairAttempts = 0;
                    state.LastPosition = bot.Position;
                    state.LastMovedAt = Time.time;
                    return;
                }

                if (state.GeneralLocomotionStallSince < 0f)
                {
                    state.GeneralLocomotionStallSince = Time.time;
                    state.LastPosition = bot.Position;
                    return;
                }

                float stalledFor = Time.time - state.GeneralLocomotionStallSince;
                if (stalledFor < 0.85f || Time.time < state.NextGeneralLocomotionRepairAt)
                {
                    return;
                }

                state.NextGeneralLocomotionRepairAt = Time.time + 1.25f;
                state.GeneralLocomotionRepairAttempts++;
                bool recovered = TryRecoverGeneralFollowerLocomotion(
                    bot,
                    "lowLevelMoverStall",
                    out string generalRecoveryDetail);
                WriteEvent(recovered ? "lowLevelLocomotionRecovered" : "lowLevelLocomotionRecoveryAttempt", bot, new
                {
                    stalledFor,
                    attempt = state.GeneralLocomotionRepairAttempts,
                    position = Vec(bot.Position),
                    leaderDistance,
                    hasPath = pathController?.HavePath == true,
                    hasPathAndNoComplete = bot.Mover?.HasPathAndNoComplete == true,
                    isMoving = bot.Mover?.IsMoving == true,
                    grounded = player.MovementContext.IsGrounded,
                    inputDirection = Vec2(player.InputDirection),
                    state = player.CurrentStateName.ToString(),
                    detail = generalRecoveryDetail
                });
                if (!recovered)
                {
                    FollowerStateAuthorityCoordinator.MarkLocomotionFault(
                        bot,
                        "lowLevelMoverStall:" + generalRecoveryDetail);
                }
                state.GeneralLocomotionStallSince = -1f;
                state.LastPosition = bot.Position;
                state.LastMovedAt = Time.time;
                return;
            }

            if (state.JumpLandingSince < 0f)
            {
                state.JumpLandingSince = Time.time;
                state.LastPosition = bot.Position;
                state.LastMovedAt = Time.time;
                WriteEvent("jumpLandingWatchdogArmed", bot, new
                {
                    position = Vec(bot.Position),
                    grounded = player.MovementContext.IsGrounded
                });
                return;
            }

            float moved = Vector3.Distance(bot.Position, state.LastPosition);
            if (moved >= 0.08f)
            {
                state.LastPosition = bot.Position;
                state.LastMovedAt = Time.time;
            }

            // R8.4: a frozen JumpLanding can report grounded=false forever on a platform lip.
            // Recovery is driven by state age + true 3D no-progress, not the unreliable grounded flag.
            if (Time.time - state.JumpLandingSince < JumpLandingNaturalReleaseWaitSeconds ||
                Time.time - state.LastMovedAt < JumpLandingRepairNoProgressSeconds ||
                Time.time < state.NextRepairAt)
            {
                return;
            }

            state.NextRepairAt = Time.time + JumpLandingRepairCooldownSeconds;
            bool hardPhase = FollowerCapabilityDecisionRules.UpdateFollowerMovementStateRecovery_hardPhase_1(state.RepairAttempts,JumpLandingMaximumSoftRepairAttempts,Time.time,state.JumpLandingSince,JumpLandingHardRecoverySeconds);
            bool repaired;
            string repairDetail;
            if (hardPhase)
            {
                if (state.HardRecoveryAttempted)
                {
                    return;
                }

                state.HardRecoveryAttempted = true;
                repaired = TryHardRecoverStuckJumpLanding(
                    bot,
                    TryGetActiveProbe(bot, out ProbeRuntime hardProbe) ? hardProbe : null,
                    "followerWatchdogHard",
                    out repairDetail);
                WriteEvent(repaired ? "jumpLandingStateHardRecovered" : "jumpLandingStateHardRecoveryAttempt", bot, new
                {
                    source = "followerWatchdogHard",
                    elapsed = Time.time - state.JumpLandingSince,
                    noProgress = Time.time - state.LastMovedAt,
                    softAttempts = state.RepairAttempts,
                    state = player.CurrentStateName.ToString(),
                    repaired,
                    detail = repairDetail
                });
            }
            else
            {
                state.RepairAttempts++;
                repaired = TryRepairStuckJumpLanding(
                    bot,
                    TryGetActiveProbe(bot, out ProbeRuntime activeProbe) ? activeProbe : null,
                    "followerWatchdog",
                    out repairDetail);

                WriteEvent(repaired ? "jumpLandingStateRecovered" : "jumpLandingStateRecoveryAttempt", bot, new
                {
                    source = "followerWatchdog",
                    elapsed = Time.time - state.JumpLandingSince,
                    noProgress = Time.time - state.LastMovedAt,
                    attempt = state.RepairAttempts,
                    state = player.CurrentStateName.ToString(),
                    repaired,
                    detail = repairDetail
                });
            }

            if (repaired)
            {
                state.JumpLandingSince = -1f;
                state.LastMovedAt = Time.time;
                state.RepairAttempts = 0;
                state.HardRecoveryAttempted = false;
            }
        }

        private static bool TryNeutralizeLearningV432StationaryVisualLocomotionResidue(
            BotOwner bot,
            Player player)
        {
            if ((bot == null) ||
                player?.MovementContext == null ||
                player.CurrentStateName != EPlayerState.Idle ||
                HasActiveTraversalProbe(bot) ||
                IsTeleportTransactionActive(bot) ||
                FollowerStateAuthorityCoordinator.HasCombatMovementClaim(bot, out _))
            {
                return false;
            }

            if (bot.Mover?.IsMoving == true ||
                bot.Mover?.HasPathAndNoComplete == true ||
                bot.Mover?.ActualPathController?.HavePath == true)
            {
                return false;
            }

            if(HasFreshCapabilityClimbIngressInput(bot,player))return false;

            Vector2 input = player.InputDirection;
            Vector2 movement = player.MovementContext.MovementDirection;
            if (input.sqrMagnitude < 0.0025f && movement.sqrMagnitude < 0.0025f)
            {
                return false;
            }

            long generation = FollowerMovementWriterAuthority.GetCurrentGeneration(
                bot, out FollowerMovementWriterAuthority.WriterDomain owner);
            if (generation <= 0 ||
                (owner != FollowerMovementWriterAuthority.WriterDomain.Follow &&
                 owner != FollowerMovementWriterAuthority.WriterDomain.Recovery))
            {
                return false;
            }

            if (!TryEnterCapabilityFollowRecoveryScope(
                bot, player, CapabilityFollowRecoveryMode.GeneralRecovery, generation, null,
                out FollowerMovementWriterAuthority.WriteScope idleNeutralizeScope, out _))
                return false;
            using (idleNeutralizeScope)
            {
                player.SetPlayerInputDirection("R1-EBE17FF14744",@bot,Vector2.zero);
                player.MovementContext.SetMovementContextMovementDirection("R1-3B3CDE84D808",@bot,Vector2.zero);
                player.MovementContext.TryMovementContextApplyApproachMotion(
"R1-C0A0D51AEABF",@bot,                    Vector3.zero,Mathf.Max(0.001f, Time.deltaTime));
            }

            if (ShouldEmitDiagnostic("learningV432StationaryVisualLocomotionNeutralized", bot, 0.75f))
            {
                WriteEvent("learningV432StationaryVisualLocomotionNeutralized", bot, new
                {
                    previousInput = Vec2(input),
                    previousMovementDirection = Vec2(movement),
                    movementOwner = owner.ToString(),
                    movementGeneration = generation,
                    state = player.CurrentStateName.ToString(),
                    moverMoving = bot.Mover?.IsMoving == true,
                    moverHasPath = bot.Mover?.HasPathAndNoComplete == true,
                    traversalSuccessRecorded = false,
                    persistentLearningChanged = false,
                    bodyRelocated = false,
                    action = "idlePresentationCannotRetainStaleUnitLocomotionVector"
                });
            }
            return true;
        }

        private static bool TryRepairStuckJumpLanding(
            BotOwner bot,
            ProbeRuntime probe,
            string source,
            out string detail)
        {
            detail = "no-op";
            Player player = bot?.GetPlayer;
            if (player?.MovementContext == null || player.CurrentStateName != EPlayerState.JumpLanding)
            {
                detail = "stateAlreadyReleased";
                return true;
            }

            if(!TryEnterCapabilityFollowRecoveryScope(bot,player,probe==null?CapabilityFollowRecoveryMode.GeneralRecovery:CapabilityFollowRecoveryMode.ProbeRecovery,probe?.MovementWriterGeneration??FollowerMovementWriterAuthority.PeekExactGeneration(bot,out _),probe,
                out var capabilityRecoveryScope,out var capabilityRecoveryFailure))
            {
                detail="capabilityRecoveryRejected:"+capabilityRecoveryFailure;
                return false;
            }
            using(capabilityRecoveryScope)
            {
            List<string> actions = new List<string>();
            TryInvokeOptionalBoolMethod(player?.AIData?.BotOwner, player.MovementContext, "PlayerAnimatorEnableJump", false, actions);
            TryInvokeOptionalBoolMethod(player?.AIData?.BotOwner, player.MovementContext, "PlayerAnimatorEnableLanding", false, actions);

            // Run is known to exist in the SPT 4.0.13 EPlayerState surface and is also the
            // natural player transition observed after JumpLanding in the captured diagnostics.
            EPlayerState target = EPlayerState.Run;
            bool transitionInvoked = TryInvokeMovementStateTransition(player?.AIData?.BotOwner, player.MovementContext, target, actions) ||
                                     TryInvokeMovementStateTransition(player?.AIData?.BotOwner, player, target, actions, allowSignatureFallback: false);

            try
            {
                player.SetPlayerInputDirection("R1-CB99080FED0C",@bot,Vector2.up);
                player.MovementContext.SetMovementContextMovementDirection("R1-8AEB5AD5D6D2",@bot,Vector2.up);
                bot.Mover.SetBotMoverPause("R1-F5978F7CE58B",@bot,false);
                if (bot.Mover.Sprinting)
                {
                    bot.Mover.BotMoverSprint("R1-D7A05D6C1E7F",@bot,false,false);
                }
                bot.BotOwnerSetTargetMoveSpeed("R1-D8CEC91293F7",@bot,0.70f);
                actions.Add("locomotionIntent");
            }
            catch (Exception ex)
            {
                actions.Add("locomotionIntent:" + ex.GetType().Name);
            }

            TryCrossFadeFollowerLocomotion(player, probe?.PreviousSprinting == true, actions);
            bool released = player.CurrentStateName != EPlayerState.JumpLanding;
            if (released)
            {
                FollowerStateAuthorityCoordinator.ClearLocomotionFault(
                    bot, "softJumpLandingRecovery");
            }
            detail = string.Join(",", actions);
            if (!released && transitionInvoked)
            {
                // Some MovementContext transitions commit on the next player tick.
                detail += ",transitionPending";
            }
            return released;
            }
        }

        private static bool TryRecoverBoundFollowerLocomotion(BotOwner bot, ProbeRuntime probe, string source, out string detail)
        {
            detail = "playerUnavailable";
            Player player = bot?.GetPlayer;
            if (player?.MovementContext == null)
            {
                return false;
            }

            if(!TryEnterCapabilityFollowRecoveryScope(bot,player,probe==null?CapabilityFollowRecoveryMode.GeneralRecovery:CapabilityFollowRecoveryMode.ProbeRecovery,probe?.MovementWriterGeneration??FollowerMovementWriterAuthority.PeekExactGeneration(bot,out _),probe,
                out var capabilityRecoveryScope,out var capabilityRecoveryFailure))
            {
                detail="capabilityRecoveryRejected:"+capabilityRecoveryFailure;
                return false;
            }
            using(capabilityRecoveryScope)
            {
            List<string> actions = new List<string>();
            try
            {
                player.MovementContext.MovementContextPlayerAnimatorSetDoVault("R1-A383EB5B4D3E",@bot,false);
                player.MovementContext.MovementContextPlayerAnimatorSetDoClimb("R1-BE1C874A3EDE",@bot,false);
                player.MovementContext.TryMovementContextApplyApproachMotion("R1-A77E5B3BC8D4",@bot,Vector3.zero,Mathf.Max(0.001f, Time.deltaTime));
                actions.Add("clearTraversalAnimatorFlags");
            }
            catch (Exception ex)
            {
                actions.Add("clearTraversalAnimatorFlags:" + ex.GetType().Name);
            }

            TryInvokeOptionalBoolMethod(player?.AIData?.BotOwner, player.MovementContext, "PlayerAnimatorEnableJump", false, actions);
            TryInvokeOptionalBoolMethod(player?.AIData?.BotOwner, player.MovementContext, "PlayerAnimatorEnableLanding", false, actions);
            TryInvokeMovementStateTransition(player?.AIData?.BotOwner, player.MovementContext, EPlayerState.Run, actions);
            TryInvokeMovementStateTransition(player?.AIData?.BotOwner, player, EPlayerState.Run, actions, allowSignatureFallback: false);

            try
            {
                player.SetPlayerInputDirection("R1-2E22814EFA94",@bot,Vector2.up);
                player.MovementContext.SetMovementContextMovementDirection("R1-6B6578F44A82",@bot,Vector2.up);
                if (bot.Mover != null)
                {
                    bot.Mover.SetBotMoverPause("R1-D1A645FAD3B0",@bot,false);
                    bot.Mover.LocalAvoidance?.GClass492DropOffset("R1-96A46E72346C",@bot);
                }
                bot.BotOwnerSetTargetMoveSpeed("R1-34D56F7022BD",@bot,0.82f);
                actions.Add("forwardLocomotionIntent");
            }
            catch (Exception ex)
            {
                actions.Add("forwardLocomotionIntent:" + ex.GetType().Name);
            }

            bool animatorRecovered = TryCrossFadeFollowerLocomotion(
                player,
                bot.Mover?.Sprinting == true,
                actions);
            bool stateHealthy = FollowerCapabilityDecisionRules.TryRecoverGeneralFollowerLocomotion_stateHealthy_1((int)(player.CurrentStateName),(int)(EPlayerState.Run),(int)(player.CurrentStateName),(int)(EPlayerState.Sprint));
            detail = (source ?? "generalLocomotionRecovery") + ":" +
                     string.Join(",", actions) +
                     ":state=" + player.CurrentStateName +
                     ":animatorRecovered=" + animatorRecovered;
            return stateHealthy || animatorRecovered;
            }
        }

        internal static bool TryQuiesceFollowerLocomotionForTeleport(
            BotOwner bot,
            string source,
            out string detail, long issuedTeleportGeneration)
        {
            detail = "playerUnavailable";
            Player player = bot?.GetPlayer;
            if ((player?.MovementContext == null))
            {
                return false;
            }

            if(!TryEnterCapabilityFollowRecoveryScope(bot,player,CapabilityFollowRecoveryMode.TeleportQuiescence,issuedTeleportGeneration,null,
                out var capabilityRecoveryScope,out var capabilityRecoveryFailure))
            {
                detail="capabilityRecoveryRejected:"+capabilityRecoveryFailure;
                return false;
            }
            using(capabilityRecoveryScope)
            {
            List<string> actions = new List<string>();
            try
            {
                player.MovementContext.MovementContextPlayerAnimatorSetDoVault("R1-F525BA0156B1",@bot,false);
                player.MovementContext.MovementContextPlayerAnimatorSetDoClimb("R1-3F4BADE7245E",@bot,false);
                TryInvokeOptionalBoolMethod(player?.AIData?.BotOwner, player.MovementContext, "PlayerAnimatorEnableJump", false, actions);
                TryInvokeOptionalBoolMethod(player?.AIData?.BotOwner, player.MovementContext, "PlayerAnimatorEnableLanding", false, actions);

                player.SetPlayerInputDirection("R1-A7137C31FE3E",@bot,Vector2.zero);
                player.MovementContext.SetMovementContextMovementDirection("R1-4E109A9B81C3",@bot,Vector2.zero);
                player.MovementContext.TryMovementContextApplyApproachMotion(
"R1-D9669581366D",@bot,                    Vector3.zero,Mathf.Max(0.001f, Time.deltaTime));

                if (bot.Mover != null)
                {
                    bot.Mover.BotMoverSprint("R1-6ECFBB861D02",@bot,false,false);
                    bot.Mover.LocalAvoidance?.GClass492DropOffset("R1-55F62D22D47B",@bot);
                    bot.Mover.BotMoverStop("R1-A92A0295DC38",@bot);
                    bot.Mover.SetBotMoverPause("R1-1C8161D3C103",@bot,true);
                }
                bot.BotOwnerStopMove("R1-F964C3463DEB",@bot);
                bot.GoToSomePointData?.BotGoToPointDataSetPoint("R1-EC3B66C94776",@bot,bot.Position);
                bot.GoToSomePointData?.BotGoToPointDataUpdateToGo("R1-DE2BA7468C87",@bot,false);
                bot.PatrollingData?.PatrollingDataPause("R1-FE6D963B2953",@bot);
                actions.Add("zeroMovementIntent");
                actions.Add("nativeMoverQuiesced");
            }
            catch (Exception ex)
            {
                actions.Add("quiesce:" + ex.GetType().Name);
            }

            // JumpLanding/Traversal states may retain animation ownership after the body has been
            // relocated. Ask only for the neutral Run state; unlike the ordinary hard-recovery
            // helper, do NOT inject forward input, target speed, sprint, or locomotion crossfade.
            EPlayerState beforeState = player.CurrentStateName;
            if (beforeState == EPlayerState.JumpLanding || IsProbeTraversalState(beforeState))
            {
                TryInvokeMovementStateTransition(player?.AIData?.BotOwner, player.MovementContext, EPlayerState.Run, actions);
                TryInvokeMovementStateTransition(player?.AIData?.BotOwner, player, EPlayerState.Run, actions, allowSignatureFallback: false);
                try
                {
                    player.SetPlayerInputDirection("R1-0DF52F0F5FBC",@bot,Vector2.zero);
                    player.MovementContext.SetMovementContextMovementDirection("R1-4A15DEEB41BF",@bot,Vector2.zero);
                    player.MovementContext.TryMovementContextApplyApproachMotion(
"R1-D7A124BB8E27",@bot,                        Vector3.zero,Mathf.Max(0.001f, Time.deltaTime));
                }
                catch
                {
                }
            }

            bool healthy = player.CurrentStateName != EPlayerState.JumpLanding;
            detail = (source ?? "teleportQuiescence") + ":" +
                     string.Join(",", actions) +
                     ":before=" + beforeState +
                     ":state=" + player.CurrentStateName +
                     ":forwardIntentInjected=False";
            return healthy;
            }
        }

        internal static bool TryHardRecoverFollowerLocomotionAuthority(BotOwner bot, string source, out string detail)
        {
            long current=FollowerMovementWriterAuthority.PeekExactGeneration(bot,out var owner);
            if(owner==FollowerMovementWriterAuthority.WriterDomain.Teleport)
            {
                if(FollowerMovementWriterAuthority.CanCommitScopedPhysicalMotion(bot,out _))
                    return TryQuiesceFollowerLocomotionForTeleport(bot,source,out detail,current);
                detail="teleportQuiescenceRequiresExistingOriginScope";
                return false;
            }
            if (bot?.GetPlayer?.CurrentStateName == EPlayerState.JumpLanding)
            {
                return TryHardRecoverStuckJumpLanding(bot, null, source, out detail);
            }
            return TryRecoverGeneralFollowerLocomotion(bot, source, out detail);
        }

        private static bool TryHardRecoverStuckJumpLanding(BotOwner bot, ProbeRuntime probe, string source, out string detail)
        {
            detail = "no-op";
            Player player = bot?.GetPlayer;
            if (player?.MovementContext == null || player.CurrentStateName != EPlayerState.JumpLanding)
            {
                detail = "stateAlreadyReleased";
                return true;
            }

            if(!TryEnterCapabilityFollowRecoveryScope(bot,player,probe==null?CapabilityFollowRecoveryMode.GeneralRecovery:CapabilityFollowRecoveryMode.ProbeRecovery,probe?.MovementWriterGeneration??FollowerMovementWriterAuthority.PeekExactGeneration(bot,out _),probe,
                out var capabilityRecoveryScope,out var capabilityRecoveryFailure))
            {
                detail="capabilityRecoveryRejected:"+capabilityRecoveryFailure;
                return false;
            }
            using(capabilityRecoveryScope)
            {
            List<string> actions = new List<string>();
            try
            {
                bot.Mover?.BotMoverStop("R1-3424EA5378BB",@bot);
                bot.BotOwnerStopMove("R1-39A4372CF9AC",@bot);
                if (bot.Mover != null)
                {
                    bot.Mover.SetBotMoverPause("R1-7958E0FAF8B7",@bot,false);
                    bot.Mover.BotMoverSprint("R1-CD0D9C677D62",@bot,false,false);
                    bot.Mover.LocalAvoidance?.GClass492DropOffset("R1-21551EDB1FFC",@bot);
                }
                player.SetPlayerInputDirection("R1-BBCC30D63358",@bot,Vector2.zero);
                player.MovementContext.SetMovementContextMovementDirection("R1-939CC8726B1A",@bot,Vector2.zero);
                player.MovementContext.MovementContextPlayerAnimatorSetDoVault("R1-260B55DBCBB8",@bot,false);
                player.MovementContext.MovementContextPlayerAnimatorSetDoClimb("R1-CF65F358D496",@bot,false);
                player.MovementContext.TryMovementContextApplyApproachMotion("R1-86B56C20B0DD",@bot,Vector3.zero,Mathf.Max(0.001f, Time.deltaTime));
                actions.Add("quiesce");
            }
            catch (Exception ex)
            {
                actions.Add("quiesce:" + ex.GetType().Name);
            }

            TryInvokeOptionalBoolMethod(player?.AIData?.BotOwner, player.MovementContext, "PlayerAnimatorEnableJump", false, actions);
            TryInvokeOptionalBoolMethod(player?.AIData?.BotOwner, player.MovementContext, "PlayerAnimatorEnableLanding", false, actions);

            // Hard phase deliberately requests the known Run state on both MovementContext and Player.
            // Reflection keeps this compatible with SPT's obfuscated
            // concrete movement implementation while remaining bounded to a single hard attempt.
            TryInvokeMovementStateTransition(player?.AIData?.BotOwner, player.MovementContext, EPlayerState.Run, actions);
            TryInvokeMovementStateTransition(player?.AIData?.BotOwner, player, EPlayerState.Run, actions, allowSignatureFallback: false);

            try
            {
                player.SetPlayerInputDirection("R1-445FD427155D",@bot,Vector2.up);
                player.MovementContext.SetMovementContextMovementDirection("R1-65D521CD82EB",@bot,Vector2.up);
                bot.BotOwnerSetTargetMoveSpeed("R1-B612694BD7D8",@bot,0.70f);
                actions.Add("resumeIntent");
            }
            catch (Exception ex)
            {
                actions.Add("resumeIntent:" + ex.GetType().Name);
            }

            TryCrossFadeFollowerLocomotion(player, probe?.PreviousSprinting == true, actions);
            bool released = player.CurrentStateName != EPlayerState.JumpLanding;
            detail = source + ":" + string.Join(",", actions) + ":state=" + player.CurrentStateName;
            if (released)
            {
                FollowerStateAuthorityCoordinator.ClearLocomotionFault(
                    bot, "hardJumpLandingRecovery");
            }
            else
            {
                FollowerStateAuthorityCoordinator.MarkLocomotionFault(bot, detail);
            }
            return released;
            }
        }

        private static bool TryInvokeOptionalBoolMethod(BotOwner controlBot, object instance, string methodName, bool value, List<string> actions)
        {
            if (instance == null)
            {
                return false;
            }
            try
            {
                MethodInfo method = instance.GetType().GetMethod(
                    methodName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(bool) },
                    null);
                if (method == null)
                {
                    return false;
                }
                method.MethodBaseInvoke("R1-96FCB9293456",@controlBot,instance,new object[] { value });
                actions?.Add(methodName + "(" + value + ")");
                return true;
            }
            catch (Exception ex)
            {
                actions?.Add(methodName + ":" + ex.GetType().Name);
                return false;
            }
        }

        private static bool TryInvokeMovementStateTransition(
            BotOwner controlBot, object instance,
            EPlayerState target,
            List<string> actions,
            bool allowSignatureFallback = true)
        {
            if (instance == null)
            {
                return false;
            }

            string[] candidateNames =
            {
                "ChangeState",
                "SetState",
                "SwitchState",
                "SetCurrentState",
                "TransitionToState"
            };
            Type type = instance.GetType();
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (string candidate in candidateNames)
            {
                try
                {
                    MethodInfo method = type.GetMethod(
                        candidate,
                        flags,
                        null,
                        new[] { typeof(EPlayerState) },
                        null);
                    if (method == null)
                    {
                        continue;
                    }
                    method.MethodBaseInvoke("R1-A402CF010D85",@controlBot,instance,new object[] { target });
                    actions?.Add(type.Name + "." + candidate + "(" + target + ")");
                    return true;
                }
                catch (Exception ex)
                {
                    actions?.Add(type.Name + "." + candidate + ":" + ex.GetType().Name);
                }
            }

            if (!allowSignatureFallback)
            {
                return false;
            }

            // R8.11: SPT/EFT movement implementations are obfuscated and the actual state writer
            // does not have to retain one of the semantic names above. Discover compatible
            // one-argument EPlayerState methods by signature. Invoke only when there is a unique
            // best candidate; otherwise log the signatures and fail safely instead of guessing.
            try
            {
                MethodInfo[] compatible = type
                    .GetMethods(flags)
                    .Where(method =>
                    {
                        if (method == null || method.IsSpecialName)
                        {
                            return false;
                        }
                        ParameterInfo[] parameters = method.GetParameters();
                        return parameters.Length == 1 &&
                               parameters[0].ParameterType == typeof(EPlayerState) &&
                               (method.ReturnType == typeof(void) || method.ReturnType == typeof(bool));
                    })
                    .Distinct()
                    .ToArray();

                if (compatible.Length == 0)
                {
                    actions?.Add(type.Name + ".stateTransitionSignature:none");
                    return false;
                }

                Func<MethodInfo, int> score = method =>
                {
                    string name = method.Name?.ToLowerInvariant() ?? string.Empty;
                    int value = 0;
                    if (name.Contains("state")) value += 6;
                    if (name.Contains("transition")) value += 5;
                    if (name.Contains("change")) value += 4;
                    if (name.Contains("switch")) value += 4;
                    if (name.Contains("set")) value += 3;
                    if (name.Contains("move")) value += 1;
                    return value;
                };

                int bestScore = compatible.Max(score);
                MethodInfo[] best = compatible
                    .Where(method => score(method) == bestScore)
                    .ToArray();

                MethodInfo selected = best.Length == 1
                    ? best[0]
                    : (compatible.Length == 1 ? compatible[0] : null);
                if (selected == null)
                {
                    string candidates = string.Join(
                        "|",
                        compatible.Take(12).Select(method => method.Name));
                    actions?.Add(type.Name + ".stateTransitionCandidates:" + candidates);
                    return false;
                }

                object result = selected.MethodBaseInvoke("R1-619BAC671168",@controlBot,instance,new object[] { target });
                bool accepted = selected.ReturnType != typeof(bool) || (result is bool boolResult && boolResult);
                actions?.Add(
                    type.Name + "." + selected.Name + "(" + target + "):signatureFallback=" + accepted);
                return accepted;
            }
            catch (Exception ex)
            {
                actions?.Add(type.Name + ".stateTransitionSignature:" + ex.GetType().Name);
                return false;
            }
        }

        private static bool TryCrossFadeFollowerLocomotion(Player player, bool preferSprint, List<string> actions)
        {
            if (player == null)
            {
                return false;
            }
            try
            {
                Animator[] animators = player.GetComponentsInChildren<Animator>(true);
                if (animators == null)
                {
                    return false;
                }
                // CompileFix48: recover the main Player animator first. Fix47's failure was on
                // animator=Player layer 0; taking the first arbitrary child animator can repair a
                // weapon/arms controller while leaving body locomotion unchanged.
                IEnumerable<Animator> orderedAnimators = animators
                    .Where(animator => animator != null && animator.isActiveAndEnabled)
                    .OrderBy(animator => string.Equals(animator.name, "Player", StringComparison.OrdinalIgnoreCase) ? 0 : 1);
                string[] names = preferSprint
                    ? new[] { "Sprint", "SprintForward", "Run", "Movement", "Idle" }
                    : new[] { "Run", "RunForward", "Movement", "Walk", "Idle" };
                foreach (Animator animator in orderedAnimators)
                {
                    for (int layer = 0; layer < animator.layerCount; layer++)
                    {
                        foreach (string name in names)
                        {
                            int hash = Animator.StringToHash(name);
                            if (!animator.HasState(layer, hash))
                            {
                                continue;
                            }
                            animator.AnimatorCrossFadeInFixedTime("R1-B0B0F5B4D6DB",@player?.AIData?.BotOwner,hash,0.08f,layer,0f);
                            actions?.Add("animator:" + name);
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                actions?.Add("animatorRecovery:" + ex.GetType().Name);
            }
            return false;
        }
        private static bool TryApplyLearningV407CollisionActiveLocomotionPresentation(
            BotOwner bot,
            Player player,
            Vector3 authorizedMotion,
            bool sprint,
            float targetMoveSpeed)
        {
            if ((bot == null) || player?.MovementContext == null ||
                !IsFinite(authorizedMotion) || authorizedMotion.sqrMagnitude <= 0.000001f ||
                !player.MovementContext.IsGrounded)
            {
                return false;
            }

            if(!TryEnterCapabilityFollowRecoveryScope(bot,player,CapabilityFollowRecoveryMode.GeneralRecovery,
                FollowerMovementWriterAuthority.PeekExactGeneration(bot,out _),null,out var compatibilityScope,out _))return false;
            using(compatibilityScope)
            {
            EPlayerState stateBefore = player.CurrentStateName;
            if (stateBefore == EPlayerState.Transition || IsProbeTraversalState(stateBefore) ||
                (stateBefore != EPlayerState.Idle &&
                 stateBefore != EPlayerState.Run &&
                 stateBefore != EPlayerState.Sprint))
            {
                return false;
            }

            List<string> actions = null;
            bool requestedStateTransition = false;
            bool animatorSynchronized = false;
            player.SetPlayerInputDirection("R1-639AED053723",@bot,Vector2.up);
            player.MovementContext.SetMovementContextMovementDirection("R1-08C21697978B",@bot,Vector2.up);

            if (stateBefore == EPlayerState.Idle)
            {
                actions = new List<string>();
                requestedStateTransition =
                    TryInvokeMovementStateTransition(player?.AIData?.BotOwner, player.MovementContext, EPlayerState.Run, actions) ||
                    TryInvokeMovementStateTransition(player?.AIData?.BotOwner, player, EPlayerState.Run, actions, allowSignatureFallback: false);
                animatorSynchronized = TryCrossFadeFollowerLocomotion(player, sprint, actions);
            }

            if (ShouldEmitDiagnostic(
                    "learningV407CollisionActiveLocomotionPresentationSynchronized", bot, 0.20f))
            {
                long generation = FollowerMovementWriterAuthority.GetCurrentGeneration(
                    bot, out FollowerMovementWriterAuthority.WriterDomain owner);
                WriteEvent("learningV407CollisionActiveLocomotionPresentationSynchronized", bot, new
                {
                    owner = owner.ToString(),
                    generation,
                    stateBefore = stateBefore.ToString(),
                    stateAfter = player.CurrentStateName.ToString(),
                    grounded = player.MovementContext.IsGrounded,
                    motionMagnitude = authorizedMotion.magnitude,
                    inputDirection = Vec2(player.InputDirection),
                    movementDirection = Vec2(player.MovementContext.MovementDirection),
                    sprint,
                    targetMoveSpeed,
                    requestedStateTransition,
                    animatorSynchronized,
                    actions = actions != null ? string.Join(",", actions) : string.Empty,
                    translationChanged = false,
                    routeTargetChanged = false,
                    traversalSuccessRecorded = false,
                    persistentLearningChanged = false,
                    action = "mirrorAuthorizedGroundedPhysicalTranslationIntoNativeLocomotionPresentation"
                });
            }
            return true;
            }
        }
    }
}
