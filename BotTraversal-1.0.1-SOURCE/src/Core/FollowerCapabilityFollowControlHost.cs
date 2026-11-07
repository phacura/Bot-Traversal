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
        private static void ClearCapabilityNativeDestinationData(BotOwner bot)
        {
            if(bot?.GetPlayer==null||!FollowerMovementWriterAuthority.CanCommitScopedPhysicalMotion(bot,out _))return;
            bot.GoToSomePointData?.BotGoToPointDataSetPoint("R1-769EDF221C64",bot,bot.GetPlayer.Position);
            bot.GoToSomePointData?.BotGoToPointDataUpdateToGo("R1-74DBD2CEF7CD",bot,false);
        }
        internal static bool TryPrimeFreshFollowPathAfterAuthorityReset(
            BotOwner bot, string reason, string pathSource, out string detail, bool preserveLocomotion = false)
        {
            detail = "unavailable";
            if ((bot?.GetPlayer == null) || bot.Mover == null)
            {
                return false;
            }

            Player leader = FollowerEnvironment.LocalPlayer;
            if (!IsLivePlayer(leader) || !IsFinite(leader.Position) || !IsFinite(bot.GetPlayer.Position))
            {
                detail = "leaderUnavailable";
                return false;
            }

            string source = string.IsNullOrEmpty(pathSource) ? "authority-reacquire" : pathSource;
            if (!IsLeaderFollowPathSource(source))
            {
                source = "authority-reacquire";
            }

            {
                long currentGeneration = FollowerMovementWriterAuthority.GetCurrentGeneration(
                    bot, out FollowerMovementWriterAuthority.WriterDomain currentOwner);
                if (currentOwner == FollowerMovementWriterAuthority.WriterDomain.Traversal &&
                    currentGeneration > 0)
                {
                    bool handedOff = TryHandoffLearningV419TraversalToFreshFollowConsumer(
                        bot, currentGeneration,
                        "learningV420PrimeFreshFollow:" + (reason ?? "authorityReset"),
                        source, out string handoffDetail);
                    detail = "learningV420TwoPhase:" + handoffDetail;
                    return handedOff;
                }
            }

            if (!CanAcceptFollowPath(bot, leader.Position, source, out float pathLength, out float detourRatio))
            {
                RecordFollowPathSanityRejected(bot, leader.Position, source, pathLength, detourRatio);
                detail = "pathRejected:length=" + pathLength.ToString("0.00", CultureInfo.InvariantCulture) +
                         ":detour=" + detourRatio.ToString("0.00", CultureInfo.InvariantCulture);
                return false;
            }

            // V2-RC18: the fresh Follow successor issues one exact physical
            // ticket; do not split owner acquisition and scope publication.
            if (!TryEnterCapabilityFreshFollowScope(
                bot, "learningV351FreshFollow:" + (reason ?? "authorityReset"),
                out var v2FollowScope, out string v2FollowTicketReason))
            {
                detail = "v2FollowOriginRejected:" + v2FollowTicketReason;
                return false;
            }
            long generation = v2FollowScope.IssuedGeneration;
            try
            {
                using (v2FollowScope)
                {
                    if (!preserveLocomotion)
                    {
                        bot.Mover.BotMoverStop("R1-6D923C1204AC",@bot);
                        bot.BotOwnerStopMove("R1-866D5D2796F5",@bot);
                        ClearCapabilityNativeDestinationData(bot);
                    }
                    bot.Mover.LocalAvoidance?.GClass492DropOffset("R1-7433A0BFAB5B",@bot);
                    bot.Mover.SetBotMoverPause("R1-700EC92D453E",@bot,false);
                    // V2-RC23: the exact bound Follow ticket is revalidated at
                    // the low-level destination write, not only when this scope opens.
                    if (!FollowerMovementWriterAuthority.TryCommitScopedFollowPath(
                        bot, leader.Position, out NavMeshPathStatus status))
                    {
                        detail = "v2FollowPathCommitRejected";
                        return false;
                    }
                    RecordFollowPathResult(bot, leader.Position, source, status);
                    if (status != NavMeshPathStatus.PathComplete)
                    {
                        if (preserveLocomotion)
                        {
                            // Successor-first failed to install a real consumer. Fall back to the
                            // V3.51 stale-path retirement boundary rather than leaving an old mover
                            // route live under the new Follow generation.
                            bot.Mover.BotMoverStop("R1-1FEC86422402",@bot);
                            bot.BotOwnerStopMove("R1-7368B78F628C",@bot);
                            bot.GoToSomePointData?.BotGoToPointDataSetPoint("R1-20C7EFACD669",@bot,bot.GetPlayer.Position);
                            bot.GoToSomePointData?.BotGoToPointDataUpdateToGo("R1-092AA0AD3CEA",@bot,false);
                        }
                        detail = "pathPrime=" + status + (preserveLocomotion ? ":continuousFallbackStopped" : string.Empty);
                        return false;
                    }
                    bot.BotOwnerSetTargetMoveSpeed("R1-605B4044C1DA",@bot,ProbeMomentumFullMoveSpeed);
                    bot.Mover.BotMoverSetTargetMoveSpeed("R1-5711EE155BD1",@bot,ProbeMomentumFullMoveSpeed);
                    detail = "primed:generation=" + generation.ToString(CultureInfo.InvariantCulture) +
                             ":length=" + pathLength.ToString("0.00", CultureInfo.InvariantCulture) +
                             ":detour=" + detourRatio.ToString("0.00", CultureInfo.InvariantCulture) +
                             ":preserveLocomotion=" + preserveLocomotion;
                    return true;
                }
            }
            catch (Exception ex)
            {
                RecordFollowPathWriteException(bot, leader.Position, source, ex);
                detail = "primeFailed:" + ex.GetType().Name;
                return false;
            }
        }

        internal static void ArmFollowReacquireAfterAuthorityReset(BotOwner bot, string reason)
        {
            string profileId = bot?.ProfileId ?? string.Empty;
            if (string.IsNullOrEmpty(profileId))
            {
                return;
            }

            string resolvedReason = string.IsNullOrWhiteSpace(reason) ? "authorityReset" : reason;
            FollowReacquireWatches[profileId] = new FollowReacquireWatch
            {
                ArmedAt = Time.time,
                NextAttemptAt = Time.time + FollowReacquireWatchDelaySeconds,
                Reason = resolvedReason,
                Attempts = 0
            };
            // CompileFix48: give the just-reset body one real Follow reacquire beat. A stale
            // Memory.HaveEnemy/SAIN combat layer may not reclaim movement in the same frame unless
            // the state coordinator can prove an actionable/immediate threat.
            FollowerStateAuthorityCoordinator.ArmFollowRecoveryGrace(
                bot,
                Mathf.Max(1.10f, FollowReacquireWatchDelaySeconds + 0.85f),
                resolvedReason);
            WriteEvent("movementAuthorityResetFollowReacquireArmed", bot, new
            {
                reason = resolvedReason,
                delay = FollowReacquireWatchDelaySeconds
            });
        }

        private static bool TryGetCurrentFollowMovementTarget(
            BotOwner bot,
            out Vector3 target,
            out string source)
        {
            target = default;
            source = "none";
            if (bot == null) return false;
            try
            {
                if (bot.Mover?.TargetPoint is Vector3 moverTarget && IsFinite(moverTarget))
                {
                    target = moverTarget;
                    source = "moverTarget";
                    return true;
                }
                if (bot.GoToSomePointData != null && bot.GoToSomePointData.HaveTarget() && IsFinite(bot.GoToSomePointData.Point))
                {
                    target = bot.GoToSomePointData.Point;
                    source = "goToSomePointData";
                    return true;
                }
            }
            catch { }
            return false;
        }

        private static bool TryValidateCurrentMoverFollowPath(
            BotOwner bot,
            string followSource,
            out Vector3 moverTarget,
            out string moverTargetSource,
            out float pathLength,
            out float detourRatio)
        {
            moverTarget = default;
            moverTargetSource = "none";
            pathLength = 0f;
            detourRatio = 0f;
            if (bot?.Mover == null || bot.Mover.ActualPathController == null)
            {
                return false;
            }

            if (!TryGetCurrentFollowMovementTarget(bot, out moverTarget, out moverTargetSource) ||
                !string.Equals(moverTargetSource, "moverTarget", StringComparison.Ordinal))
            {
                return false;
            }

            // V3.10 consumer-ready contract: the semantic target is not enough. The live BotMover
            // must own an actual path from the CURRENT body, and that exact target must pass the
            // existing Follow detour policy. No new distance/tolerance is introduced here.
            if (bot.Mover.ActualPathController.HavePath != true ||
                bot.Mover.HasPathAndNoComplete != true)
            {
                return false;
            }

            return CanAcceptFollowPath(bot, moverTarget, followSource, out pathLength, out detourRatio);
        }

        private static void SuspendPostTraversalFollowEvaluationForReacquire(BotOwner bot, string reason)
        {
            string profileId = bot?.ProfileId ?? string.Empty;
            if (string.IsNullOrEmpty(profileId) ||
                !PostTraversalIntegrityWatches.TryGetValue(profileId, out PostTraversalIntegrityWatch watch) ||
                watch == null)
            {
                return;
            }
            watch.AwaitingFollowAck = true;
            watch.ForcedFollowAckFailure = false;
            watch.ExpiresAt = float.MaxValue;
            WriteEvent("postTraversalEndToEndWatchSuspendedForReacquire", bot, new
            {
                edgeId = watch.EdgeId,
                reason = reason ?? "postAckOwnershipLost"
            });
        }

        private static bool TryInstallPostTraversalFreshFollowEpoch(
            BotOwner bot,
            Vector3 leaderPosition,
            PostTraversalChaseLease lease,
            string source)
        {
            if (bot == null || lease == null || lease.NormalFollowFreshEpochIssued ||
                bot.Mover == null || !IsFinite(leaderPosition))
            {
                return false;
            }

            lease.NormalFollowFreshEpochIssued = true;
            lease.NormalFollowFreshEpochIssuedAt = Time.time;
            lease.NormalFollowFreshEpochTarget = leaderPosition;
            const string followSource = "post-traversal-fresh-epoch";
            if (!CanAcceptFollowPath(bot, leaderPosition, followSource, out float pathLength, out float detourRatio))
            {
                lease.NormalFollowFreshEpochStatus = "PathRejected";
                RecordFollowPathSanityRejected(bot, leaderPosition, followSource, pathLength, detourRatio);
                WriteEvent("postTraversalFreshFollowEpochRejected", bot, new
                {
                    edgeId = lease.EdgeId,
                    source = source ?? "unknown",
                    target = Vec(leaderPosition),
                    pathLength,
                    detourRatio,
                    retryScheduled = false
                });
                return false;
            }

            NavMeshPathStatus status = NavMeshPathStatus.PathInvalid;
            try
            {
                bot.Mover.SetBotMoverPause("R1-117D93013D62",@bot,false);
                status = bot.BotOwnerGoToPoint("R1-BFCCFC5FA04E",@bot,leaderPosition,true,-1f,false,false);
                RecordFollowPathResult(bot, leaderPosition, followSource, status);
                if (status == NavMeshPathStatus.PathComplete)
                {
                    bot.BotOwnerSetTargetMoveSpeed("R1-6AD82BC49B0C",@bot,ProbeMomentumFullMoveSpeed);
                    bot.Mover.BotMoverSetTargetMoveSpeed("R1-335E56B6AD7E",@bot,ProbeMomentumFullMoveSpeed);
                }
            }
            catch (Exception ex)
            {
                RecordFollowPathWriteException(bot, leaderPosition, followSource, ex);
                status = NavMeshPathStatus.PathInvalid;
            }

            lease.NormalFollowFreshEpochStatus = status.ToString();
            WriteEvent("postTraversalFreshFollowEpochInstalled", bot, new
            {
                edgeId = lease.EdgeId,
                source = source ?? "unknown",
                target = Vec(leaderPosition),
                status = status.ToString(),
                pathLength,
                detourRatio,
                oneShot = true
            });
            if (status == NavMeshPathStatus.PathComplete)
            {
                MarkLearningV352PostTraversalConsumerPathProven(
                    bot, "normal-follow-fresh-epoch");
                return true;
            }
            return false;
        }

        private static bool QueueLearningV430DeferredPostTraversalFollowRollover(
            BotOwner bot,
            int edgeId,
            string reason,
            bool armFollowReacquire = false)
        {
            if ((bot == null) || string.IsNullOrEmpty(bot.ProfileId))
            {
                return false;
            }

            long generation = FollowerMovementWriterAuthority.GetCurrentGeneration(
                bot, out FollowerMovementWriterAuthority.WriterDomain owner);
            if (generation <= 0 || owner != FollowerMovementWriterAuthority.WriterDomain.PostTraversal)
            {
                return false;
            }

            LearningV430PendingPostTraversalFollowRollovers[bot.ProfileId] =
                new LearningV430DeferredPostTraversalFollowRollover
                {
                    ExpectedGeneration = generation,
                    EdgeId = edgeId,
                    Reason = reason ?? "learningV430DeferredPostTraversalFollowRollover",
                    RequestedAt = Time.time,
                    ArmFollowReacquire = armFollowReacquire
                };
            WriteEvent("learningV430PostTraversalFollowOwnerRolloverDeferred", bot, new
            {
                edgeId,
                generation,
                reason = reason ?? "unknown",
                armFollowReacquire,
                action = "finishCurrentPostTraversalWriterScopeBeforeMintingFollowGeneration"
            });
            return true;
        }

        internal static void CommitLearningV430DeferredPostTraversalFollowHandoff(BotOwner bot)
        {
            if ((bot == null) || string.IsNullOrEmpty(bot.ProfileId) ||
                !LearningV430PendingPostTraversalFollowRollovers.TryGetValue(
                    bot.ProfileId, out LearningV430DeferredPostTraversalFollowRollover pending) ||
                pending == null)
            {
                return;
            }

            // Remove before attempting the transition so a failed/stale request cannot re-fire on
            // every Follow tick. A newer owner/generation wins and makes this request obsolete.
            LearningV430PendingPostTraversalFollowRollovers.Remove(bot.ProfileId);
            bool transitioned = FollowerMovementWriterAuthority.TransitionPostTraversalToFollowIfCurrent(
                bot, pending.ExpectedGeneration, pending.Reason);
            long currentGeneration = FollowerMovementWriterAuthority.GetCurrentGeneration(
                bot, out FollowerMovementWriterAuthority.WriterDomain currentOwner);
            if (transitioned && pending.ArmFollowReacquire)
            {
                ArmFollowReacquireAfterAuthorityReset(bot, pending.Reason);
            }
            bool persistentChronologyEvidenceChanged = transitioned &&
                RecordLearningV430MonotonicConsumerEvidence(
                    pending.EdgeId,
                    "deferredConsumerGenerationRollover",
                    pending.Reason);

            WriteEvent(
                transitioned
                    ? "learningV430PostTraversalFollowOwnerRolloverCommitted"
                    : "learningV430PostTraversalFollowOwnerRolloverSuperseded",
                bot,
                new
                {
                    edgeId = pending.EdgeId,
                    expectedGeneration = pending.ExpectedGeneration,
                    currentGeneration,
                    currentOwner = currentOwner.ToString(),
                    reason = pending.Reason ?? "unknown",
                    deferredFor = Mathf.Max(0f, Time.time - pending.RequestedAt),
                    ordinaryFollowReacquireArmed = transitioned && pending.ArmFollowReacquire,
                    persistentChronologyEvidenceChanged,
                    action = transitioned
                        ? "generationChangesOnlyAfterOldWriterScopeDisposed"
                        : "newerMovementOwnerWinsWithoutReplayingHistoricalRollover"
                });
        }

        private static void RestartPostTraversalFollowEvaluationAfterAck(BotOwner bot)
        {
            string profileId = bot?.ProfileId ?? string.Empty;
            if (string.IsNullOrEmpty(profileId) ||
                !PostTraversalIntegrityWatches.TryGetValue(profileId, out PostTraversalIntegrityWatch watch) ||
                watch == null || !watch.EvaluateEndToEndFollow)
            {
                return;
            }

            Player leader = FollowerEnvironment.LocalPlayer;
            float distance = IsLivePlayer(leader) && IsFinite(leader.Position)
                ? GetPlanarDistance(bot.Position, leader.Position)
                : float.MaxValue;
            watch.AwaitingFollowAck = false;
            watch.ForcedFollowAckFailure = false;
            watch.ArmedAt = Time.time;
            watch.ExpiresAt = Time.time + PostTraversalIntegrityWatchSeconds;
            watch.ReleasePosition = bot.Position;
            watch.InitialLeaderDistance = distance;
            watch.BestLeaderDistance = distance;
            watch.LastLeaderDistance = distance;
            watch.LastFollowProgressAt = Time.time;
            watch.MaxReleaseDisplacement = 0f;
            watch.SawHealthyMoverPath = bot.Mover?.HasPathAndNoComplete == true;
            watch.LastMoverEvidenceAt = watch.SawHealthyMoverPath ? Time.time : 0f;
            WriteEvent("postTraversalEndToEndWatchRestartedAfterPhysicalAck", bot, new
            {
                edgeId = watch.EdgeId,
                initialLeaderDistance = distance,
                seconds = PostTraversalIntegrityWatchSeconds
            });
        }

        private static bool IsSuspiciousFollowPath(float directDistance, StrictPathResult path)
        {
            return !path.StrictComplete ||
                   path.PathLength >= Mathf.Max(
                       directDistance * FollowDetourRatioThreshold,
                       directDistance + FollowDetourExtraDistanceThreshold);
        }

        private static bool IsSuddenFollowPathInflation(
            string profileId,
            float directDistance,
            StrictPathResult currentPath)
        {
            if (string.IsNullOrEmpty(profileId) ||
                !FollowPathBaselines.TryGetValue(profileId, out FollowPathBaseline previous) ||
                Time.time - previous.SampledAt > FollowPathBaselineTtlSeconds)
            {
                return false;
            }

            float pathIncrease = currentPath.PathLength - previous.PathLength;
            float directIncrease = directDistance - previous.DirectDistance;
            return pathIncrease >= FollowSuddenInflationMinIncrease &&
                   directIncrease <= FollowSuddenInflationMaxDirectIncrease;
        }

        private static void UpdateFollowPathBaseline(
            string profileId,
            float directDistance,
            float pathLength,
            bool strictComplete)
        {
            if (string.IsNullOrEmpty(profileId))
            {
                return;
            }

            FollowPathBaselines[profileId] = new FollowPathBaseline
            {
                DirectDistance = directDistance,
                PathLength = pathLength,
                StrictComplete = strictComplete,
                SampledAt = Time.time
            };
        }

        private static bool IsLeaderFollowPathSource(string source)
        {
            return string.Equals(source, "leader-primary", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(source, "leader-retry", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(source, "authority-reacquire", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(source, "exit-authority-follow-handoff", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(source, "post-traversal-chase", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(source, "post-traversal-fresh-epoch", StringComparison.OrdinalIgnoreCase);
        }

        private static float GetAllowedFollowPathLength(float directDistance, bool leaderRoute)
        {
            float direct = Mathf.Max(0f, directDistance);
            if (direct <= 5f)
            {
                return Mathf.Max(9f, direct * 3.0f);
            }

            if (direct <= 10f)
            {
                return Mathf.Max(16f, direct * 2.75f);
            }

            if (!leaderRoute)
            {
                return float.MaxValue;
            }

            // R8.5 tiered leader-follow policy. It is intentionally much looser than the
            // breadcrumb "suspicious" threshold: reasonable routes around buildings remain legal,
            // while pathological map-scale detours are refused. Example: 13 m direct permits
            // roughly 36 m, not 190-205 m.
            if (direct <= 20f)
            {
                return Mathf.Max(direct + 18f, direct * 2.75f);
            }

            if (direct <= 35f)
            {
                return Mathf.Max(direct + 28f, direct * 2.40f);
            }

            if (direct <= 60f)
            {
                return Mathf.Max(direct + 40f, direct * 2.10f);
            }

            return Mathf.Max(direct + 80f, direct * 2.0f);
        }

        private static void CacheFollowPathSanity(
            BotOwner bot,
            Vector3 target,
            float directDistance,
            StrictPathResult path)
        {
            string profileId = bot?.ProfileId ?? string.Empty;
            if (string.IsNullOrEmpty(profileId) || path == null || !IsFinite(target))
            {
                return;
            }

            FollowPathSanitySamples[profileId] = new FollowPathSanitySample
            {
                Start = bot.Position,
                Target = target,
                DirectDistance = directDistance,
                PathLength = path.PathLength,
                DetourRatio = directDistance > 0.01f ? path.PathLength / directDistance : 1f,
                StrictComplete = path.StrictComplete,
                SampledAt = Time.time
            };
        }

        private static bool TryGetCachedFollowPathSanity(
            BotOwner bot,
            Vector3 target,
            out FollowPathSanitySample sample)
        {
            sample = null;
            string profileId = bot?.ProfileId ?? string.Empty;
            if (string.IsNullOrEmpty(profileId) ||
                !FollowPathSanitySamples.TryGetValue(profileId, out FollowPathSanitySample cached) ||
                cached == null)
            {
                return false;
            }

            if (Time.time - cached.SampledAt > FollowPathSanityCacheSeconds ||
                Vector3.Distance(cached.Target, target) > FollowPathSanityCacheTargetTolerance ||
                Vector3.Distance(cached.Start, bot.Position) > FollowPathSanityCacheStartTolerance)
            {
                return false;
            }

            sample = cached;
            return true;
        }

        private static void UpdateCF63DormantFollowConsumerWatch(BotOwner bot)
        {
            string profileId = bot?.ProfileId ?? string.Empty;
            if (string.IsNullOrEmpty(profileId) || bot.Mover == null ||
                !IsLiveBot(bot) || bot.GetPlayer?.MovementContext?.IsGrounded != true ||
                HasActiveTraversalProbe(bot) ||
                TryGetActiveFollowerRouteMission(bot, out FollowerRouteMission cf63Mission) ||
                FollowerStateAuthorityCoordinator.HasCombatMovementClaim(bot, out _) ||
                FollowerMedical.IsUsingMedical(bot) ||
                FollowerMedical.IsHandsInteractionActiveForAuthority(bot.GetPlayer))
            {
                CF63DormantFollowWatches.Remove(profileId);
                return;
            }
            BotFollowerPlayer follower = BossPlayers.GetFollowerByProfileId(profileId);
            if ((follower != null && follower.IsTeleportStabilizing) ||
                (follower != null && follower.TryPeekActiveCommand(
                    out FollowerCommandType cf63Command, out _, out _) &&
                    cf63Command != FollowerCommandType.None))
            {
                CF63DormantFollowWatches.Remove(profileId);
                return;
            }
            long writerGeneration = FollowerMovementWriterAuthority.GetCurrentGeneration(
                bot, out FollowerMovementWriterAuthority.WriterDomain writerOwner);
            if (writerOwner != FollowerMovementWriterAuthority.WriterDomain.Follow &&
                writerOwner != FollowerMovementWriterAuthority.WriterDomain.Unknown)
            {
                CF63DormantFollowWatches.Remove(profileId);
                return;
            }
            Player leader = FollowerEnvironment.LocalPlayer;
            if (!IsLivePlayer(leader) || !IsFinite(leader.Position) || !IsFinite(bot.Position) ||
                bot.Mover.Pause || bot.Mover.IsMoving || bot.Mover.HasPathAndNoComplete ||
                bot.Mover.ActualPathController?.HavePath == true ||
                Mathf.Abs(bot.Position.y - leader.Position.y) > 3.0f ||
                Vector3.Distance(bot.Position, leader.Position) < 8.0f)
            {
                CF63DormantFollowWatches.Remove(profileId);
                return;
            }

            if (!CF63DormantFollowWatches.TryGetValue(profileId, out CF63DormantFollowWatch watch))
            {
                watch = new CF63DormantFollowWatch
                {
                    Position = bot.Position,
                    Since = Time.time,
                    Generation = writerGeneration
                };
                CF63DormantFollowWatches[profileId] = watch;
                return;
            }
            float moved = GetPlanarDistance(bot.Position, watch.Position);
            if (writerGeneration != watch.Generation || moved >= 0.18f)
            {
                watch.Position = bot.Position;
                watch.Since = Time.time;
                watch.Generation = writerGeneration;
                watch.Armed = false;
                return;
            }
            // One attempt per measured stationary episode. The existing reacquire watch
            // performs its own path sanity, command, combat and bounded retry checks.
            if (watch.Armed || Time.time - watch.Since < 1.20f ||
                FollowReacquireWatches.ContainsKey(profileId))
            {
                return;
            }
            watch.Armed = true;
            ArmFollowReacquireAfterAuthorityReset(bot, "cf63MeasuredPathlessOrdinaryFollow");
            WriteEvent("cf63DormantFollowConsumerRecoveryArmed", bot, new
            {
                stationarySeconds = Time.time - watch.Since,
                leaderDistance = Vector3.Distance(bot.Position, leader.Position),
                owner = writerOwner.ToString(),
                writerGeneration,
                moverHasPath = false,
                nativeSuccessRecorded = false,
                bodyRelocated = false,
                action = "oncePerMeasuredStationaryPathlessFollowEpisodeNeverOverrideTraversalOrTeleport"
            });
        }

        private static void UpdateCF64FollowPhysicalWitness(BotOwner bot)
        {
            string profileId = bot?.ProfileId ?? string.Empty;
            if (string.IsNullOrEmpty(profileId) ||
                !CF64FollowPhysicalWatches.TryGetValue(profileId, out CF64FollowPhysicalWitness watch) ||
                watch == null)
            {
                return;
            }
            float now = Time.time;
            long generation = FollowerMovementWriterAuthority.GetCurrentGeneration(
                bot, out FollowerMovementWriterAuthority.WriterDomain owner);
            BotFollowerPlayer follower = BossPlayers.GetFollowerByProfileId(profileId);
            bool isUnsafe = !IsLiveBot(bot) || !IsFinite(bot.Position) ||
                generation != watch.Generation ||
                owner != FollowerMovementWriterAuthority.WriterDomain.Follow ||
                HasActiveTraversalProbe(bot) ||
                TryGetActiveFollowerRouteMission(bot, out FollowerRouteMission cf64Mission) ||
                follower?.IsTeleportStabilizing == true ||
                (follower != null && follower.TryPeekActiveCommand(
                    out FollowerCommandType cf64Command, out _, out _) &&
                    cf64Command != FollowerCommandType.None) ||
                FollowerStateAuthorityCoordinator.HasCombatMovementClaim(bot, out _) ||
                FollowerMedical.IsUsingMedical(bot) ||
                FollowerMedical.IsHandsInteractionActiveForAuthority(bot.GetPlayer);
            float elapsed = now - watch.StartedAt;
            float dt = now - watch.LastAt;
            float delta = !isUnsafe ? GetPlanarDistance(bot.Position, watch.Position) : 0f;
            bool implausibleDelta = dt > 0f && delta > Mathf.Max(0.40f, dt * 12.0f);
            if (isUnsafe || implausibleDelta || dt > 0.35f || elapsed > 2.0f)
            {
                CF64FollowPhysicalWatches.Remove(profileId);
                WriteEvent("cf64FollowPhysicalRejoinUnproven", bot, new
                {
                    elapsedSeconds = elapsed,
                    accumulatedPlanarDistance = watch.AccumulatedPlanarDistance,
                    sampleCount = watch.Samples,
                    originalGeneration = watch.Generation,
                    observedGeneration = generation,
                    observedOwner = owner.ToString(),
                    reason = isUnsafe ? "authorityOrActionChanged" :
                        implausibleDelta ? "nonPhysicalPositionDiscontinuity" :
                        dt > 0.35f ? "largeFrameSamplingGap" : "noMeasuredProgressBeforeDeadline",
                    nativeSuccessRecorded = false,
                    bodyRelocated = false,
                    action = "pathReacquiredIsNotPhysicalFollowProof"
                });
                return;
            }
            if (dt <= 0f)
            {
                return;
            }
            watch.LastAt = now;
            watch.Position = bot.Position;
            watch.AccumulatedPlanarDistance += delta;
            watch.Samples++;
            // Same physical epoch, credible consecutive updates, and live native Mover.
            if (watch.Samples >= 2 && watch.AccumulatedPlanarDistance >= 0.20f &&
                bot.Mover?.IsMoving == true &&
                bot.Mover.ActualPathController?.HavePath == true)
            {
                CF64FollowPhysicalWatches.Remove(profileId);
                WriteEvent("cf64FollowPhysicalRejoinVerified", bot, new
                {
                    elapsedSeconds = elapsed,
                    measuredPlanarDistance = watch.AccumulatedPlanarDistance,
                    sampleCount = watch.Samples,
                    writerGeneration = generation,
                    moverHasPath = true,
                    moverMoving = true,
                    bodyRelocated = false,
                    traversalSuccessRecorded = false,
                    action = "sameWriterEpochNativeMoverAndMultiSamplePhysicalBodyProgress"
                });
            }
        }

        private static void UpdateFollowReacquireWatch(BotOwner bot)
        {
            string profileId = bot?.ProfileId ?? string.Empty;
            if (string.IsNullOrEmpty(profileId) ||
                !FollowReacquireWatches.TryGetValue(profileId, out FollowReacquireWatch watch) || watch == null)
            {
                return;
            }

            BotFollowerPlayer followerData = BossPlayers.GetFollowerByProfileId(profileId);
            if (followerData?.IsTeleportStabilizing == true || HasActiveTraversalProbe(bot))
            {
                return;
            }

            // CompileFix25: the reacquire watch is a low-priority ordinary-Follow repair. Never let
            // it fight a combat claim, a player command, or a hands/medical transaction that appeared
            // after the watch was armed.
            if (FollowerStateAuthorityCoordinator.HasCombatMovementClaim(bot, out _) ||
                FollowerMedical.IsUsingMedical(bot) ||
                FollowerMedical.IsHandsInteractionActiveForAuthority(bot.GetPlayer))
            {
                return;
            }
            if (followerData != null &&
                followerData.TryPeekActiveCommand(out FollowerCommandType activeCommand, out _, out _) &&
                activeCommand != FollowerCommandType.None)
            {
                return;
            }

            Player leader = FollowerEnvironment.LocalPlayer;
            if (!IsLivePlayer(leader) || !IsFinite(leader.Position) || !IsFinite(bot.Position))
            {
                if (Time.time - watch.ArmedAt > FollowReacquireWatchMaxSeconds)
                {
                    FollowReacquireWatches.Remove(profileId);
                }
                return;
            }

            float leaderDistance = Vector3.Distance(bot.Position, leader.Position);
            BotPathController controller = bot.Mover?.ActualPathController;
            bool moverAccepted = bot.Mover != null &&
                                !bot.Mover.Pause &&
                                (controller?.HavePath == true || bot.Mover.IsMoving);
            if (moverAccepted)
            {
                FollowReacquireWatches.Remove(profileId);
                // CF64: acceptance is only a NavMesh consumer witness. The separate
                // same-generation body trace must prove movement before success is claimed.
                if (diagnosticsEnabled &&
                    string.Equals(watch.Reason, "cf63MeasuredPathlessOrdinaryFollow", StringComparison.Ordinal))
                {
                    long cf64Generation = FollowerMovementWriterAuthority.GetCurrentGeneration(
                        bot, out FollowerMovementWriterAuthority.WriterDomain cf64Owner);
                    if (cf64Owner == FollowerMovementWriterAuthority.WriterDomain.Follow && IsFinite(bot.Position))
                    {
                        CF64FollowPhysicalWatches[profileId] = new CF64FollowPhysicalWitness
                        {
                            Position = bot.Position,
                            StartedAt = Time.time,
                            LastAt = Time.time,
                            Generation = cf64Generation
                        };
                    }
                }
                WriteEvent("movementAuthorityResetFollowReacquired", bot, new
                {
                    reason = watch.Reason,
                    attempts = watch.Attempts,
                    leaderDistance,
                    moverAccepted = true
                });
                return;
            }

            // Immediately after a regroup teleport the follower is intentionally placed near the
            // leader. Proximity is not proof that Follow reacquired movement; keep observing until
            // either BotMover accepts a path or the leader starts to pull away.
            if (Time.time - watch.ArmedAt > FollowReacquireWatchMaxSeconds ||
                watch.Attempts >= FollowReacquireWatchMaxAttempts)
            {
                FollowReacquireWatches.Remove(profileId);
                WriteEvent("movementAuthorityResetFollowReacquireExpired", bot, new
                {
                    reason = watch.Reason,
                    attempts = watch.Attempts,
                    leaderDistance
                });
                return;
            }

            if (leaderDistance <= FollowReacquireWatchSatisfiedDistance || Time.time < watch.NextAttemptAt)
            {
                return;
            }

            watch.Attempts++;
            watch.NextAttemptAt = Time.time + FollowReacquireWatchRetrySeconds;
            FollowReacquireWatches[profileId] = watch;

            NavMeshPathStatus primeStatus = NavMeshPathStatus.PathInvalid;
            float primePathLength = 0f;
            float primeDetourRatio = 0f;
            try
            {
                if (bot.Mover != null)
                {
                    bot.Mover.SetBotMoverPause("R1-A81F4BC9E86B",@bot,false);
                }

                // SoftReset clears stale combat/mover residue, but it is not a Follow command.
                // CompileFix26 immediately installs a fresh ordinary NavMesh target so reacquire
                // success no longer depends on a later BigBrain tick happening to write a path.
                FollowerRecovery.SoftReset(bot);
                const string reacquireSource = "authority-reacquire";
                if (CanAcceptFollowPath(bot, leader.Position, reacquireSource, out primePathLength, out primeDetourRatio))
                {
                    primeStatus = bot.BotOwnerGoToPoint("R1-F1CAF3638CAC",@bot,leader.Position,true,-1f,false,false);
                    RecordFollowPathResult(bot, leader.Position, reacquireSource, primeStatus);
                    if (primeStatus == NavMeshPathStatus.PathComplete && bot.Mover != null)
                    {
                        bot.BotOwnerSetTargetMoveSpeed("R1-EE3E7F5A4A0F",@bot,ProbeMomentumFullMoveSpeed);
                        bot.Mover.BotMoverSetTargetMoveSpeed("R1-851683EE4DD2",@bot,ProbeMomentumFullMoveSpeed);
                        if (leaderDistance > 9f && !bot.Mover.Sprinting)
                        {
                            bot.Mover.BotMoverSprint("R1-12C7CF167DA0",@bot,true,false);
                        }
                    }
                }
                else
                {
                    RecordFollowPathSanityRejected(
                        bot, leader.Position, reacquireSource, primePathLength, primeDetourRatio);
                }
            }
            catch (Exception ex)
            {
                RecordFollowPathWriteException(bot, leader.Position, "authority-reacquire", ex);
            }

            BotPathController refreshedController = bot.Mover?.ActualPathController;
            WriteEvent("movementAuthorityResetFollowReacquire", bot, new
            {
                reason = watch.Reason,
                attempt = watch.Attempts,
                leaderDistance,
                pathPrimeStatus = primeStatus.ToString(),
                pathLength = primePathLength,
                detourRatio = primeDetourRatio,
                hasPath = refreshedController?.HavePath == true,
                isMoving = bot.Mover?.IsMoving == true
            });
        }

        private static void TryCF38ReprimeLostFollowConsumerPath(
            BotOwner bot,
            Player leader,
            FollowerMovementRecoveryState state,
            float leaderDistance,
            float stalledFor)
        {
            if (bot?.Mover == null || leader == null || state == null ||
                stalledFor < TraversalRouteRecoveryDebounceSeconds ||
                leaderDistance < RouteMissionProactiveLeaderDistance ||
                bot.Mover.Pause || bot.Mover.IsMoving ||
                bot.Mover.HasPathAndNoComplete == true ||
                bot.Mover.ActualPathController?.HavePath == true ||
                Time.time <= state.LearningV349GenericPassageLeaseUntil ||
                !IsFinite(bot.Position) || !IsFinite(leader.Position))
            {
                return;
            }

            long generation = FollowerMovementWriterAuthority.GetCurrentGeneration(
                bot, out FollowerMovementWriterAuthority.WriterDomain owner);
            if (owner != FollowerMovementWriterAuthority.WriterDomain.Follow ||
                generation <= 0 || state.CF38LastFollowReprimeGeneration == generation)
            {
                return;
            }

            // The same physical Follow epoch gets one bounded corrective publication.
            // Even a failed preflight consumes this attempt; the existing measured-progress
            // watchdog remains the authority for any later physical rescue/escalation.
            state.CF38LastFollowReprimeGeneration = generation;
            const string pathSource = "post-traversal-fresh-epoch";
            bool accepted = CanAcceptFollowPath(
                bot, leader.Position, pathSource, out float pathLength, out float detourRatio);
            NavMeshPathStatus status = NavMeshPathStatus.PathInvalid;
            if (accepted)
            {
                try
                {
                    if (TryEnterCapabilityFollowRecoveryScope(
                        bot,bot.GetPlayer,CapabilityFollowRecoveryMode.FollowPath,generation,null,
                        out var v2ReprimeScope, out _))
                    using (v2ReprimeScope)
                    {
                        // RC24: the same Follow generation used for this repair
                        // must also authorize the native destination write.
                        bool v2PathCommitted = FollowerMovementWriterAuthority.TryCommitScopedFollowPath(
                            bot, leader.Position, out status);
                        if (v2PathCommitted)
                            RecordFollowPathResult(bot, leader.Position, pathSource, status);
                        if (status == NavMeshPathStatus.PathComplete &&
                            bot.Mover.ActualPathController?.HavePath == true)
                        {
                            bot.BotOwnerSetTargetMoveSpeed("R1-C0B954C2E849",@bot,ProbeMomentumFullMoveSpeed);
                            bot.Mover.BotMoverSetTargetMoveSpeed("R1-1BE0D9D05106",@bot,ProbeMomentumFullMoveSpeed);
                        }
                    }
                }
                catch (Exception ex)
                {
                    RecordFollowPathWriteException(bot, leader.Position, pathSource, ex);
                }
            }
            else
            {
                RecordFollowPathSanityRejected(
                    bot, leader.Position, pathSource, pathLength, detourRatio);
            }

            WriteEvent("cf38LostFollowConsumerPathReprimeAttempted", bot, new
            {
                followGeneration = generation,
                leaderDistance,
                stalledFor,
                pathAccepted = accepted,
                pathLength,
                detourRatio,
                nativePathStatus = status.ToString(),
                moverHasPath = bot.Mover.ActualPathController?.HavePath == true,
                moverMoving = bot.Mover.IsMoving,
                traversalSuccessRecorded = false,
                persistentLearningChanged = false,
                movementGenerationChanged = false,
                bodyRelocated = false,
                action = "reprimeLostFollowPathOncePerExistingFollowGenerationKeepMeasuredWatchdogActive"
            });
        }

        private static bool TryCF45RepairStaleTerminalFollowConsumer(
            BotOwner bot, Player leader, FollowerMovementRecoveryState state,
            long followGeneration, float leaderDistance)
        {
            if (bot == null || leader == null || state == null ||
                bot.Mover?.ActualPathController == null ||
                followGeneration <= 0 ||
                state.CF45LastTerminalFollowRepairGeneration == followGeneration ||
                leaderDistance <= RouteMissionProactiveLeaderDistance ||
                !IsFinite(bot.Position) || !IsFinite(leader.Position) ||
                !bot.Mover.ActualPathController.HavePath ||
                !bot.Mover.IsMoving ||
                !PhysicalNavigationEpochHandoffs.TryGetValue(
                    bot.ProfileId ?? string.Empty, out PhysicalNavigationEpochHandoff handoff) ||
                handoff == null || handoff.Generation != raidGeneration ||
                handoff.FreshConsumerPathObserved ||
                handoff.SourceMovementGeneration >= followGeneration ||
                !IsFinite(handoff.PhysicalAnchor))
            {
                return false;
            }

            Vector3 currentCorner;
            try
            {
                currentCorner = bot.Mover.ActualPathController.CurrentCornerPoint;
            }
            catch
            {
                return false;
            }
            if (!IsFinite(currentCorner) ||
                GetPlanarDistance(currentCorner, handoff.PhysicalAnchor) > 0.75f ||
                GetPlanarDistance(bot.Position, currentCorner) < 3f)
            {
                return false;
            }
            Vector3 toCorner = currentCorner - bot.Position;
            Vector3 toLeader = leader.Position - bot.Position;
            toCorner.y = 0f;
            toLeader.y = 0f;
            if (toCorner.sqrMagnitude < 9f || toLeader.sqrMagnitude < 9f ||
                Vector3.Dot(toCorner.normalized, toLeader.normalized) >= -0.25f)
            {
                return false;
            }

            // The stale physical corner, Follow owner and terminal epoch all match.
            // Consume this generation's ONLY repair admission, including a failed path
            // sanity preflight. A new traversal cannot inherit this repair right.
            state.CF45LastTerminalFollowRepairGeneration = followGeneration;
            const string source = "post-traversal-fresh-epoch";
            bool pathAccepted = CanAcceptFollowPath(
                bot, leader.Position, source, out float pathLength, out float detourRatio);
            NavMeshPathStatus status = NavMeshPathStatus.PathInvalid;
            string detail = "preflightRejected";
            if (pathAccepted)
            {
                try
                {
                    if (TryEnterCapabilityFollowRecoveryScope(
                        bot,bot.GetPlayer,CapabilityFollowRecoveryMode.FollowPath,followGeneration,null,
                        out var v2RepairScope, out var v2RepairReason))
                    using (v2RepairScope)
                    {
                        Vector3 physicalBody = bot.Position;
                        // Retire stale native route topology only; re-anchor to the measured
                        // body before requesting a fresh Follow route. No body relocation or
                        // forced-forward locomotion, no additional writer generation.
                        bot.Mover.BotMoverStop("R1-8C421A090980",@bot);
                        bot.Mover.BotMoverSetPointOnWay("R1-0C348788B96B",@bot,physicalBody);
                        bot.Mover.SetBotMoverPositionOnWayInner("R1-E44DEE3B8710",@bot,physicalBody);
                        bot.Mover.SetBotMoverPrevSuccessLinkedFrom_1("R1-EA8B46A28FB3",@bot,physicalBody);
                        bot.Mover.SetBotMoverPrevPosLinkedTime_1("R1-7F26B1772A6D",@bot,Time.time);
                        bot.Mover.LocalAvoidance?.GClass492DropOffset("R1-6A4D9C6F48C6",@bot);
                        bot.Mover.SetBotMoverPause("R1-DD5ACCA04DAA",@bot,false);
                        // RC24: protect the repair destination with its pre-existing
                        // exact Follow ticket; never report an unissued path as written.
                        bool v2PathCommitted = FollowerMovementWriterAuthority.TryCommitScopedFollowPath(
                            bot, leader.Position, out status);
                        if (v2PathCommitted)
                            RecordFollowPathResult(bot, leader.Position, source, status);
                        detail = v2PathCommitted ? "freshPath=" + status : "v2TerminalFollowPathCommitRejected";
                        if (status == NavMeshPathStatus.PathComplete)
                        {
                            bot.BotOwnerSetTargetMoveSpeed("R1-FA43A835941C",@bot,ProbeMomentumFullMoveSpeed);
                            bot.Mover.BotMoverSetTargetMoveSpeed("R1-C59CA02F7FB5",@bot,ProbeMomentumFullMoveSpeed);
                        }
                    }
                    else
                    {
                        detail = "v2TerminalFollowOriginRejected:" + v2RepairReason;
                    }
                }
                catch (Exception ex)
                {
                    RecordFollowPathWriteException(bot, leader.Position, source, ex);
                    detail = "freshPathFailed:" + ex.GetType().Name;
                }
            }
            else
            {
                RecordFollowPathSanityRejected(
                    bot, leader.Position, source, pathLength, detourRatio);
            }
            WriteEvent("cf45TerminalFollowStaleCornerConsumerReprime", bot, new
            {
                followGeneration,
                terminalSince = state.LearningV350TerminalRecoverySince,
                leaderDistance,
                oldCorner = Vec(currentCorner),
                previousTraversalAnchor = Vec(handoff.PhysicalAnchor),
                body = Vec(bot.Position),
                pathAccepted,
                pathLength,
                detourRatio,
                pathStatus = status.ToString(),
                detail,
                oneShotPerFollowGeneration = true,
                traversalSuccessRecorded = false,
                persistentLearningChanged = false,
                movementGenerationChanged = false,
                bodyRelocated = false,
                forwardInputInjected = false,
                action = "stalePreviousTraversalCornerRebasedOnlyAfterMeasuredFollowNoProgress"
            });
            return true;
        }
    }
}
