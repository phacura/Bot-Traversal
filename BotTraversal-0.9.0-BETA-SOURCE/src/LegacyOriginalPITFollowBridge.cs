using System;
using System.Collections.Generic;
using System.Diagnostics;
using EFT;
using UnityEngine;
using UnityEngine.AI;
using BotTraversal.Core;

namespace BotTraversal
{
    /// <summary>
    /// CF7: original PIT beta's FollowAction predates V3 traversal delivery.
    /// The BT runtime has already proven the learned topology, but without this
    /// consumer there is no Follow writer to deliver its selected entry. Reuse the
    /// same native selector APIs that split PIT FollowAction consumes; do not
    /// independently invent traversal geometry or teleport a bot.
    /// </summary>
    internal static class LegacyOriginalPITFollowBridge
    {
        private static readonly Dictionary<string, float> NextEvaluation = new Dictionary<string, float>(StringComparer.Ordinal);
        private static readonly Dictionary<string, float> NextTrace = new Dictionary<string, float>(StringComparer.Ordinal);
        private static float nextError;
        // CF10: record only an accepted navigation commitment; do not turn every
        // 350-ms selector result into a fresh BotOwner.GoToPoint path calculation.
        // This state is per bot, per raid, and never changes learning truth.
        private sealed class DeliveryLease
        {
            internal Vector3 Target;
            internal float LastIssuedAt;
            internal float LastAcceptedAt;
            internal string Reason;
            // CF12: a BT-delivered PathComplete approach retains ownership of the
            // selected route, even while the runtime domain is ordinary Follow.
            internal float LeaseUntil;
            internal float LastPhysicalProgressAt;
            internal float BestRemainingDistance;
            internal float NextRejectedTraceAt;
        }
        private static readonly Dictionary<string, DeliveryLease> LastDelivery =
            new Dictionary<string, DeliveryLease>(StringComparer.Ordinal);
        private static readonly Dictionary<string, float> NextDecisionTrace =
            new Dictionary<string, float>(StringComparer.Ordinal);
        private static void TraceDecision(string id, string result)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (NextDecisionTrace.TryGetValue(id, out float next) && Time.unscaledTime < next) return;
            NextDecisionTrace[id] = Time.unscaledTime + 6f;
            Core.Logger.LogInfo("[Legacy PIT Follow] Decision bot=" + id + " " + result);
        }
        internal static void Reset()
        {
            NextEvaluation.Clear(); NextTrace.Clear(); NextDecisionTrace.Clear();
            LastDelivery.Clear();
        }
        internal static void CancelDelivery(string id,bool preserveCadence=false)
        {
            if(string.IsNullOrEmpty(id))return;
            LastDelivery.Remove(id);
            // Completed ordinary Follow repeatedly yields; keep scan/log cadence intact.
            if(preserveCadence)return;
            NextEvaluation.Remove(id);NextTrace.Remove(id);NextDecisionTrace.Remove(id);
        }
        private const float ApproachLeaseSeconds = 2.50f;
        private const float ApproachNoProgressSeconds = 2.75f;
        private const float ApproachMaxSeconds = 30.0f;
        private const float ApproachArrivalMeters = 1.35f;

        internal static bool IsOriginalPITFollowCall()
        {
            // Check the source assembly as well as the precise original action.
            // Other PIT combat/medical/command writers must remain untouched.
            var frames = new StackTrace(2, false).GetFrames();
            if (frames == null) return false;
            for (int i = 0; i < frames.Length && i < 24; i++)
            {
                Type declaringType = frames[i].GetMethod()?.DeclaringType;
                if (declaringType == null) continue;
                string name = declaringType.FullName ?? string.Empty;
                if (!name.StartsWith("pitTeam.BigBrain.Actions.FollowAction", StringComparison.Ordinal))
                    continue;
                if (string.Equals(declaringType.Assembly.GetName().Name, "pitFireTeam", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static bool LeaseAvailable(BotOwner bot, DeliveryLease lease, float now)
        {
            if (bot == null || lease == null || bot.IsDead ||
                now >= lease.LeaseUntil ||
                now - lease.LastAcceptedAt >= ApproachMaxSeconds ||
                now - lease.LastPhysicalProgressAt >= ApproachNoProgressSeconds ||
                LegacyPITCompatibility.IsOriginalManualRegroupInProgress ||
                FollowerTraversalTestRuntime.IsTeleportTransactionActive(bot) ||
                bot.Memory?.HaveEnemy == true || bot.Memory?.IsUnderFire == true ||
                FollowerEnvironment.GetExternalCommand(bot.ProfileId) != 0 ||
                FollowerMedical.IsUsingMedical(bot) ||
                FollowerMedical.IsHandsInteractionActiveForAuthority(bot.GetPlayer))
                return false;
            FollowerMovementWriterAuthority.GetCurrentGeneration(
                bot, out FollowerMovementWriterAuthority.WriterDomain owner);
            if (owner == FollowerMovementWriterAuthority.WriterDomain.Teleport ||
                owner == FollowerMovementWriterAuthority.WriterDomain.Medical ||
                owner == FollowerMovementWriterAuthority.WriterDomain.Combat ||
                owner == FollowerMovementWriterAuthority.WriterDomain.Recovery ||
                owner == FollowerMovementWriterAuthority.WriterDomain.PostTraversal ||
                owner == FollowerMovementWriterAuthority.WriterDomain.Traversal)
                return false; // Those domains already have their own native writer contracts.
            return (lease.Target - bot.Position).sqrMagnitude > ApproachArrivalMeters * ApproachArrivalMeters;
        }

        private static bool TryGetActiveLease(BotOwner bot, out DeliveryLease lease)
        {
            lease = null;
            if (!LegacyPITCompatibility.SupportsOriginalFollowBridge ||
                !FollowerTraversalTestRuntime.IsRaidActive || bot == null ||
                string.IsNullOrEmpty(bot.ProfileId) ||
                !LastDelivery.TryGetValue(bot.ProfileId, out lease))
                return false;
            if (LeaseAvailable(bot, lease, Time.time)) return true;
            LastDelivery.Remove(bot.ProfileId);
            lease = null;
            return false;
        }

        // Called only from the native write boundary, not from the BT selector.
        // There is never a blanket block of EFT, SAIN, combat, F4 or other PIT actions.
        internal static bool TryRejectOriginalPITFollowWrite(BotOwner bot, string writeKind,Vector3? candidate=null)
        {
            if (FollowerMovementWriterAuthority.CanCommitScopedPhysicalMotion(bot,out _)) return false;
            if(FollowerTraversalTestRuntime.TryYieldCapabilityCompletedFollow(bot)&&IsOriginalPITFollowCall())return false;
            if (FollowerCapabilityPortalPolicy.BlockOriginalFollowDestination(writeKind,
                FollowerTraversalTestRuntime.OwnsCapabilityLocalMotion(bot)) && IsOriginalPITFollowCall())
            {
                if(!NextDecisionTrace.TryGetValue(bot.ProfileId,out var traceAt)||Time.unscaledTime>=traceAt)
                {
                    NextDecisionTrace[bot.ProfileId]=Time.unscaledTime+1.5f;
                    FollowerTraversalTestRuntime.RecordMovementWriterAuthorityEvent(bot,"capabilityOriginalFollowDestinationBlocked",new{writeKind,reason="currentUnresolvedTypedActionOwnsMotion",nativeSuccessRecorded=false});
                }
                return true;
            }
            if (!string.Equals(writeKind, "BotOwner.GoToPoint", StringComparison.Ordinal) &&
                !string.Equals(writeKind, "BotOwner.StopMove", StringComparison.Ordinal))
                return false;
            if (!TryGetActiveLease(bot, out DeliveryLease lease)) return false;
            if (!IsOriginalPITFollowCall()) return false;
            float now = Time.time;
            if (now >= lease.NextRejectedTraceAt)
            {
                lease.NextRejectedTraceAt = now + 1.5f;
                FollowerTraversalTestRuntime.RecordMovementWriterAuthorityEvent(
                    bot, "cf12LegacyPITFollowRejectedByBTRouteLease", new
                    {
                        writeKind,
                        target = lease.Target.ToString(),
                        reason = lease.Reason,
                        remainingSeconds = lease.LeaseUntil - now,
                        bestRemainingDistance = lease.BestRemainingDistance,
                        action = "rejectOriginalPITFollowDestinationNotNativeMotion"
                    });
            }
            return true;
        }

        private static void RefreshApproachLease(BotOwner bot, string id)
        {
            if (!TryGetActiveLease(bot, out DeliveryLease lease)) return;
            float now = Time.time;
            float remaining = Vector3.Distance(bot.Position, lease.Target);
            bool healthy = bot.Mover != null && bot.Mover.HasPathAndNoComplete &&
                           bot.Mover.IsMoving && !bot.Mover.Blocked;
            if (healthy && remaining + 0.15f < lease.BestRemainingDistance)
            {
                lease.BestRemainingDistance = remaining;
                lease.LastPhysicalProgressAt = now;
                lease.LeaseUntil = Mathf.Min(now + ApproachLeaseSeconds,
                                               lease.LastAcceptedAt + ApproachMaxSeconds);
            }
        }

        internal static void Tick(BotOwner bot)
        {
            if (!LegacyPITCompatibility.SupportsOriginalFollowBridge ||
                bot == null || !FollowerEnvironment.Owns(bot) ||
                !FollowerTraversalTestRuntime.IsRaidActive || bot.IsDead) return;
            string id = bot.ProfileId;
            if (string.IsNullOrEmpty(id) ||
                (NextEvaluation.TryGetValue(id, out float next) && Time.time < next)) return;
            NextEvaluation[id] = Time.time + 0.35f;
            try
            {
                RefreshApproachLease(bot, id);
                Player leader = FollowerEnvironment.LocalPlayer;
                if (leader == null || bot.Mover == null)
                { TraceDecision(id, "gated:noLeaderOrMover"); return; }
                if (LegacyPITCompatibility.IsOriginalManualRegroupInProgress ||
                    FollowerTraversalTestRuntime.IsTeleportTransactionActive(bot))
                { TraceDecision(id, "gated:manualOrTraversalTeleport"); return; }
                if (bot.DoorOpener?.Interacting == true ||
                    bot.Memory?.HaveEnemy == true || bot.Memory?.IsUnderFire == true ||
                    FollowerMedical.IsUsingMedical(bot) ||
                    FollowerMedical.IsHandsInteractionActiveForAuthority(bot.GetPlayer) ||
                    FollowerEnvironment.GetExternalCommand(id) != 0)
                { TraceDecision(id, "gated:doorCombatHandsOrPITCommand"); return; }
                if (FollowerTraversalTestRuntime.HasActiveTraversalProbe(bot))
                { TraceDecision(id, "gated:BTProbeOwnsMover"); return; }
                // An independent beta-PIT Follow writer must not steal an active
                // BT actuator, successor, landing, recovery or post-exit epoch.
                // The old PIT FollowAction still owns ordinary Follow when safe.
                if(FollowerTraversalTestRuntime.IsCapabilityReturnInProgress(bot))
                {TraceDecision(id,"returnDriverOwnsCurrentUnfinishedEntry");return;}
                if(FollowerTraversalTestRuntime.HasCapabilityFailureStrategy(bot))
                {TraceDecision(id,"failureStrategyOwnsCurrentEntryApproach");return;}
                FollowerTraversalTestRuntime.TryReleaseCapabilityResolvedAuthority(bot);
                FollowerTraversalTestRuntime.TryPrepareCapabilityPortalApproachDelivery(bot);
                FollowerMovementWriterAuthority.GetCurrentGeneration(
                    bot, out FollowerMovementWriterAuthority.WriterDomain writerOwner);
                if (writerOwner == FollowerMovementWriterAuthority.WriterDomain.Traversal ||
                    writerOwner == FollowerMovementWriterAuthority.WriterDomain.PostTraversal ||
                    writerOwner == FollowerMovementWriterAuthority.WriterDomain.Teleport ||
                    writerOwner == FollowerMovementWriterAuthority.WriterDomain.Recovery ||
                    writerOwner == FollowerMovementWriterAuthority.WriterDomain.Medical)
                { TraceDecision(id, "gated:movementEpoch:" + writerOwner); return; }

                Vector3 leaderPosition = leader.Position;
                if (Mathf.Abs(leaderPosition.y - bot.Position.y) > 60f)
                { TraceDecision(id, "gated:unphysicalVerticalLeaderSeparation"); return; }
                if ((leaderPosition - bot.Position).sqrMagnitude < 2.25f)
                { TraceDecision(id, "gated:within1.5m"); return; }
                Vector3 target = default;
                bool activated = false;
                int edgeId = 0;
                string reason = null;
                // Restore V475's full route-consumer chain. CF7 only sampled immediate
                // portal selectors and could not establish a Mission/Breadcrumb route
                // for the PathPartial segments seen in the supplied Ghost trace.
                if(FollowerTraversalTestRuntime.TryYieldCapabilityCompletedFollow(bot))
                {TraceDecision(id,"completedTraversalNativeFormationOwnsFollow");return;}
                if (FollowerTraversalTestRuntime.TryGetCapabilityPortalApproachTarget(bot,out target,out edgeId))
                    reason="portalApproach:"+edgeId;
                else if (FollowerTraversalTestRuntime.TryGetLiveWalkThroughReservationTarget(
                        bot, leaderPosition, out target, out int liveEpisode))
                    reason = "liveWalkThrough:" + liveEpisode;
                else if (FollowerTraversalTestRuntime.HasPendingTraversalMission(bot) &&
                    FollowerTraversalTestRuntime.TryGetLearnedTraversalFollowTarget(
                        bot, leaderPosition, out target, out activated, out edgeId))
                    reason = "priorityTraversal:" + edgeId;
                else if (FollowerTraversalTestRuntime.TryGetKnownWalkThroughPortalPreemptionTarget(
                        bot, leaderPosition, out target, out activated, out edgeId))
                    reason = "knownWalkThrough:" + edgeId;
                else if (FollowerTraversalTestRuntime.TryGetPlayerRouteMissionFollowTarget(
                        bot, leaderPosition, out target, out activated, out edgeId,
                        out int routeSequence))
                    reason = "routeMission:" + edgeId + ":breadcrumb:" + routeSequence;
                else if (FollowerTraversalTestRuntime.ShouldHoldRejectedFollowRoute(bot, leaderPosition))
                { TraceDecision(id, "deferred:negativeRouteLease"); return; }
                else if (FollowerTraversalTestRuntime.TryGetLearnedTraversalFollowTarget(
                        bot, leaderPosition, out target, out activated, out edgeId))
                    reason = "learnedTraversal:" + edgeId;
                else if (FollowerTraversalTestRuntime.TryGetOperationalBreadcrumbFollowTarget(
                        bot, leaderPosition, out target, out int breadcrumbSequence))
                    reason = "breadcrumb:" + breadcrumbSequence;

                if (reason == null) { TraceDecision(id, "noSemanticCandidate"); return; }
                if (activated) { TraceDecision(id, "handledInternally:" + reason); return; }
                // An operational route is only useful if its local destination is reachable.
                // Never convert a failed path into Stop()/Teleport or mutate learned truth.
                if (float.IsNaN(target.x) || float.IsNaN(target.y) || float.IsNaN(target.z) ||
                    float.IsInfinity(target.x) || float.IsInfinity(target.y) || float.IsInfinity(target.z)) return;

                if(reason.StartsWith("portalApproach:",StringComparison.Ordinal)&&
                    !FollowerTraversalTestRuntime.TryValidateCapabilityPortalApproachPath(bot,target))return;

                // CF12: an existing accepted BT approach route wins against later
                // non-priority target changes until it is consumed, objectively stalls,
                // or a LIVE/priority verified successor takes over. A new breadcrumb
                // is not permission to throw away a physically progressing route.
                if (TryGetActiveLease(bot, out DeliveryLease lockedLease))
                {
                    Vector3 change = target - lockedLease.Target;
                    bool directPriority = reason.StartsWith("portalApproach:",StringComparison.Ordinal) || reason.StartsWith("liveWalkThrough:", StringComparison.Ordinal) ||
                                          reason.StartsWith("priorityTraversal:", StringComparison.Ordinal);
                    if (change.sqrMagnitude > 1.0f && !directPriority)
                    {
                        TraceDecision(id, "cf12:retainedBTRouteLease:rejectedSelectorChange:" + reason);
                        return;
                    }
                }
                // CF10: a stable destination is a single commitment, not a repeated
                // path request. Only reissue when the native mover lost a usable path;
                // otherwise the original Follow writer and BT alternate commands,
                // causing approach hesitation and visual correction/snapback.
                float now = Time.time;
                bool movingHealthy = bot.Mover.HasPathAndNoComplete && bot.Mover.IsMoving && !bot.Mover.Blocked;
                if (LastDelivery.TryGetValue(id, out DeliveryLease previous))
                {
                    Vector3 previousDelta = target - previous.Target;
                    previousDelta.y = 0f;
                    if (previousDelta.sqrMagnitude < 0.16f && movingHealthy &&
                        now - previous.LastAcceptedAt < 2.0f)
                    {
                        // Avoid recalculating an identical PathComplete destination
                        // on every 0.35s scan. BT's real-time traversal selector
                        // remains free to issue a different, proven entry.
                        return;
                    }
                    if (previousDelta.sqrMagnitude < 0.16f &&
                        now - previous.LastIssuedAt < 0.65f)
                        return;
                    // A sudden reversal while still driving towards the previous
                    // accepted route point is not evidence of a better path. Hold
                    // just this bridge's transient command for one selector tick;
                    // never reject an actual live traversal/priority entry.
                    bool priority = reason.StartsWith("portalApproach:",StringComparison.Ordinal) || reason.StartsWith("priorityTraversal:", StringComparison.Ordinal) ||
                                    reason.StartsWith("liveWalkThrough:", StringComparison.Ordinal) ||
                                    reason.StartsWith("knownWalkThrough:", StringComparison.Ordinal);
                    Vector3 currentToOld = previous.Target - bot.Position;
                    Vector3 currentToNew = target - bot.Position;
                    currentToOld.y = 0f; currentToNew.y = 0f;
                    if (!priority && movingHealthy &&
                        now - previous.LastAcceptedAt < 0.85f &&
                        currentToOld.sqrMagnitude > 4f && currentToNew.sqrMagnitude > 1f &&
                        Vector3.Dot(currentToOld.normalized, currentToNew.normalized) < -0.35f)
                    { TraceDecision(id, "deferred:opposingNavigationCandidate"); return; }
                }
                // V2-RC16: beta-PIT Follow delivery is a real Follow writer, NOT a
                // Sprint-correction exception. One authority facade acquires the
                // existing Follow generation and enters its exact physical scope.
                // If the owner has changed, the pathless Follow request fails closed.
                if (!FollowerMovementWriterAuthority.TryEnterFollowPhysicalMotion(
                    bot, "legacyBTRouteDelivery:" + reason,
                    out var followScope, out string followAdmissionReason))
                {
                    TraceDecision(id, "v2FollowAdmissionRejected:" + followAdmissionReason);
                    return;
                }
                NavMeshPathStatus status;
                // A PathInvalid request must not mint a stable commitment.
                // A PathComplete response is ONLY route acceptance, not a
                // traversal success or a measured physical-progress ACK.
                using (followScope)
                {
                    status = bot.BotOwnerGoToPoint("R1-194D918BD1D9",@bot,target,true,-1f,false,false);
                }
                if (status == NavMeshPathStatus.PathComplete)
                {
                    if (!LastDelivery.TryGetValue(id, out DeliveryLease lease))
                        LastDelivery[id] = lease = new DeliveryLease();
                    lease.Target = target;
                    lease.Reason = reason;
                    lease.LastIssuedAt = now;
                    lease.LastAcceptedAt = now;
                    lease.BestRemainingDistance = Vector3.Distance(bot.Position, target);
                    lease.LastPhysicalProgressAt = now;
                    lease.LeaseUntil = now + ApproachLeaseSeconds;
                    FollowerTraversalTestRuntime.RecordMovementWriterAuthorityEvent(
                        bot, "cf12BTRouteLeaseCommitted", new
                        {
                            target = target.ToString(),
                            reason,
                            path = status.ToString(),
                            leaseSeconds = ApproachLeaseSeconds,
                            action = "lockNativeFollowApproachAgainstOriginalPITFollowOnly"
                        });
                }
                if (!NextTrace.TryGetValue(id, out float trace) || Time.time >= trace)
                {
                    NextTrace[id] = Time.time + 1.25f;
                    Core.Logger.LogInfo("[Legacy PIT Follow] BT route delivery: bot=" + id +
                        " target=" + target + " reason=" + reason + " path=" + status);
                }
                if (status != NavMeshPathStatus.PathComplete)
                    TraceDecision(id, "routeRejected:" + reason + ":" + status);
                // Failed native admission is NOT a reason to Stop(), teleport, or erase the
                // old PIT path. Let the original FollowAction continue until BT can prove it.
            }
            catch (Exception ex)
            {
                if (Time.unscaledTime >= nextError)
                {
                    nextError = Time.unscaledTime + 8f;
                    Core.Logger.LogError("[Legacy PIT Follow] candidate route delivery failed; original PIT Follow remains active: " + ex);
                }
            }
        }
    }
}
