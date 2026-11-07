using System;
using D = BotTraversal.Core.FollowerMovementWriterAuthority.WriterDomain;

namespace BotTraversal.Core
{
    /// <summary>
    /// BotTraversal V2 authority gateway. No BotOwner state, movement commands, Unity callbacks,
    /// competing clock, generation counter or positional fallbacks are allocated here.
    /// Exactly one owner registry (FollowerMovementWriterAuthority.State) remains authoritative.
    /// Future movement features must be pure intent providers; only the existing authority
    /// guarded movement API may commit their intent. This gateway centralizes its admission rules.
    /// </summary>
    internal static class FollowerMovementV2IntentGateway
    {
        internal enum MovementIntentKind
        {
            Navigation, NativeActuator, Landing, Successor, Recovery, Follow,
            SoftLocomotion, Stop, Teleport, Unsupported
        }

        internal readonly struct MovementIntent
        {
            internal readonly string BotId;
            internal readonly int RaidGeneration;
            internal readonly long MissionId;
            internal readonly int StepIndex;
            internal readonly int EpisodeId;
            internal readonly int ChainId;
            internal readonly long WriterGeneration;
            internal readonly FollowerMovementWriterAuthority.WriterDomain Domain;
            internal readonly MovementIntentKind Kind;
            internal readonly string Producer;

            internal MovementIntent(string botId, int raidGeneration, long missionId,
                int stepIndex, int episodeId, int chainId, long writerGeneration,
                FollowerMovementWriterAuthority.WriterDomain domain,
                MovementIntentKind kind, string producer)
            {
                BotId = botId ?? string.Empty;
                RaidGeneration = raidGeneration;
                MissionId = missionId;
                StepIndex = stepIndex;
                EpisodeId = episodeId;
                ChainId = chainId;
                WriterGeneration = writerGeneration;
                Domain = domain;
                Kind = kind;
                Producer = producer ?? string.Empty;
            }
        }

        // Pure validation of candidate identity, not a success or physical ACK predicate.
        internal static bool ValidIdentity(MovementIntent intent)
        {
            return !string.IsNullOrEmpty(intent.BotId) && intent.RaidGeneration > 0 &&
                intent.WriterGeneration > 0 && intent.MissionId >= 0 &&
                intent.StepIndex >= -1 && intent.EpisodeId >= 0 && intent.ChainId >= 0 &&
                intent.Domain != FollowerMovementWriterAuthority.WriterDomain.Unknown;
        }

        // Identity-only guard: a thread-local scope issued for Bot A cannot turn
        // into an unscoped move for Bot B. Both identifiers are mandatory.
        internal static bool IsScopeBoundToTarget(string scopeBotId, string targetBotId)
        {
            return !string.IsNullOrEmpty(scopeBotId) &&
                !string.IsNullOrEmpty(targetBotId) &&
                string.Equals(scopeBotId, targetBotId, StringComparison.Ordinal);
        }

        internal static bool ScopeMatchesOwner(
            FollowerMovementWriterAuthority.WriterDomain scope,
            FollowerMovementWriterAuthority.WriterDomain owner)
        {
            return scope == owner ||
                (owner == FollowerMovementWriterAuthority.WriterDomain.PostTraversal &&
                    scope == FollowerMovementWriterAuthority.WriterDomain.Follow) ||
                (owner == FollowerMovementWriterAuthority.WriterDomain.Follow &&
                    scope == FollowerMovementWriterAuthority.WriterDomain.Recovery);
        }

        internal static bool AuthorizeScoped(
            FollowerMovementWriterAuthority.WriterDomain scopedDomain, long token,
            FollowerMovementWriterAuthority.WriterDomain currentOwner, long currentGeneration,
            string writeKind, out string reason)
        {
            reason = string.Empty;
            if (token <= 0 || currentGeneration <= 0 || token != currentGeneration)
            {
                reason = "v2StaleOrMissingTicket:" + (writeKind ?? string.Empty);
                return false;
            }
            if (!ScopeMatchesOwner(scopedDomain, currentOwner))
            {
                reason = "v2OwnerMismatch:" + (writeKind ?? string.Empty);
                return false;
            }
            return true;
        }

        internal static bool CanAcquire(
            FollowerMovementWriterAuthority.WriterDomain current,
            FollowerMovementWriterAuthority.WriterDomain requested)
        {
            if (current == D.Unknown || current == requested) return true;
            if (requested == D.Medical || requested == D.Teleport) return true;
            if (requested == D.Combat) return current != D.Medical && current != D.Teleport;
            if (requested == D.Traversal)
                return current == D.Follow || current == D.PostTraversal || current == D.Recovery;
            if (requested == D.PostTraversal)
                return current == D.Traversal || current == D.Follow || current == D.PostTraversal;
            if (requested == D.Recovery)
                return current == D.Unknown || current == D.Follow || current == D.Recovery;
            return requested == D.Follow;
        }

        // Single pure lifecycle admission rule. All follow-retirement API facades must use it;
        // never reconstruct a ticket from a different owner or silently upgrade a generation.
        internal static bool CanRetireToFollow(D oldDomain, D requiredDomain,
            long currentGeneration, long originGeneration, bool leaseRequired,
            bool leaseActive, out string reason)
        {
            reason = string.Empty;
            if (requiredDomain == D.Unknown || requiredDomain == D.Follow ||
                originGeneration <= 0 || currentGeneration <= 0 ||
                originGeneration != currentGeneration)
            {
                reason = "staleOrAbsentOriginTicket";
                return false;
            }
            if (oldDomain != requiredDomain)
            {
                reason = "ownerChangedBeforeRetirement";
                return false;
            }
            if (leaseRequired && !leaseActive)
            {
                reason = "exclusiveLeaseExpired";
                return false;
            }
            return true;
        }

        // V2-RC5: ONE route admission predicate. The producer cannot invent a
        // second "live" intent from its own values to approve itself. The real
        // owner domain and writer generation must come from the single authority
        // state; current mission cursor fields come from the live route executor.
        // This is only permission to issue movement, NOT a physical completion ACK.
        internal static bool CanAdmitLiveRoute(MovementIntent candidate,
            string authoritativeBotId, int authoritativeRaidGeneration,
            bool routeTransactionActive, long activeMissionId, int activeStep,
            int activeEpisode, int activeChain,
            FollowerMovementWriterAuthority.WriterDomain ownerDomain,
            long ownerGeneration, out string reason)
        {
            reason = string.Empty;
            if (!routeTransactionActive || ownerDomain != D.Traversal ||
                ownerGeneration <= 0 || string.IsNullOrEmpty(authoritativeBotId) ||
                authoritativeRaidGeneration <= 0 ||
                candidate.Kind != MovementIntentKind.Navigation ||
                candidate.Domain != D.Traversal)
            {
                reason = "routeOwnerOrTransactionNotActive";
                return false;
            }
            MovementIntent current = new MovementIntent(
                authoritativeBotId, authoritativeRaidGeneration, activeMissionId,
                activeStep, activeEpisode, activeChain, ownerGeneration,
                ownerDomain, MovementIntentKind.Navigation, "AuthoritativeLiveRoute");
            return CanOffer(candidate, current, out reason);
        }

        // V2-RC25: one PURE route-cursor transition policy. EpisodeId is an identity,
        // NOT an ordering key: overlapping authored episodes can have lower ids than
        // later physical breadcrumb passage. The cursor floor is monotonic by breadcrumb,
        // while the authored completion id intentionally remains the caller's exact id.
        // No owner acquisition, native success, mission consumption or side effect here.
        internal readonly struct RouteCursorCommitPlan
        {
            internal readonly int EpisodeId;
            internal readonly int ChainId;
            internal readonly int ConsumedBreadcrumbSequence;
            internal readonly bool ClearPreloadedEpisode;
            internal readonly bool Changed;
            internal readonly bool RepeatedIdentity;

            internal RouteCursorCommitPlan(int episodeId, int chainId, int consumed,
                bool clearPreloadedEpisode, bool changed, bool repeatedIdentity)
            {
                EpisodeId = episodeId;
                ChainId = chainId;
                ConsumedBreadcrumbSequence = consumed;
                ClearPreloadedEpisode = clearPreloadedEpisode;
                Changed = changed;
                RepeatedIdentity = repeatedIdentity;
            }
        }

        // RC26: all cursor consumption semantics share one typed policy. An inherited
        // overlapping physical passage and route cancellation never mint a new
        // terminal EpisodeId/ChainId; no secondary route-cursor registry exists.
        internal enum RouteCursorMutationKind
        {
            NativeCompletion,
            MeasuredPhysicalPass,
            InheritedPhysicalPassage,
            RouteIntentCancelled
        }

        internal static RouteCursorCommitPlan PlanRouteCursorCommit(
            int previousEpisodeId, int previousChainId, int previousConsumedBreadcrumbSequence,
            int preloadedEpisodeId, int completingEpisodeId, int completingChainId,
            int completingEndBreadcrumbSequence, RouteCursorMutationKind kind)
        {
            bool identityOnly = kind == RouteCursorMutationKind.InheritedPhysicalPassage ||
                                kind == RouteCursorMutationKind.RouteIntentCancelled;
            int nextEpisodeId = identityOnly ? previousEpisodeId : completingEpisodeId;
            int nextChainId = identityOnly ||
                (kind == RouteCursorMutationKind.MeasuredPhysicalPass && completingChainId <= 0)
                ? previousChainId : completingChainId;
            int nextFloor = Math.Max(previousConsumedBreadcrumbSequence, completingEndBreadcrumbSequence);
            bool clearPreloaded = kind == RouteCursorMutationKind.RouteIntentCancelled
                ? preloadedEpisodeId != 0 : preloadedEpisodeId == completingEpisodeId;
            bool changed = previousEpisodeId != nextEpisodeId || previousChainId != nextChainId ||
                previousConsumedBreadcrumbSequence != nextFloor || clearPreloaded;
            bool repeated = !identityOnly && previousEpisodeId == completingEpisodeId &&
                previousConsumedBreadcrumbSequence >= completingEndBreadcrumbSequence;
            return new RouteCursorCommitPlan(
                nextEpisodeId, nextChainId, nextFloor, clearPreloaded, changed, repeated);
        }

        // V2-RC27: one PURE admission predicate for consuming a live RouteMission step
        // after a terminal or measured-passage cursor commit. Episode IDs are NOT
        // chronological; the physical breadcrumb floor may retire an overlapping
        // older/newer authored episode, but it must never consume an unfinished
        // successor beyond the witnessed floor. This does not grant Native Success
        // or independently authorize a writer-generation transition.
        internal static bool CanConsumeLiveMissionStep(
            int stepEpisodeId, int stepExecutionEdgeId, int stepEndBreadcrumbSequence,
            int completedEpisodeId, int completedExecutionEdgeId, int consumedBreadcrumbSequence)
        {
            return stepEpisodeId == completedEpisodeId ||
                (stepExecutionEdgeId == completedExecutionEdgeId &&
                 stepEndBreadcrumbSequence <= consumedBreadcrumbSequence) ||
                (stepEndBreadcrumbSequence > 0 &&
                 stepEndBreadcrumbSequence <= consumedBreadcrumbSequence);
        }

        // V2-RC28: prevent replay of a completed, no-longer-live episode from
        // replacing a newer terminal cursor identity.  Do NOT order by EpisodeId:
        // recorder episodes can overlap and their numeric IDs do not establish
        // physical chronology. A newly observed breadcrumb floor is proof of
        // forward progress; a LIVE mission step with the exact authored/execution
        // identity is also sufficient to settle overlapping route debt.
        // This admission is NOT proof of Native Success and does not independently
        // validate a Writer Generation or grant any movement authority.
        internal static bool CanPublishTerminalCursorIdentity(
            int completingEpisodeId, int completingEndBreadcrumbSequence,
            int currentCompletedEpisodeId, int consumedBreadcrumbSequence,
            bool liveExactMissionStep)
        {
            if (completingEpisodeId <= 0)
                return false;
            if (currentCompletedEpisodeId == completingEpisodeId)
                return true; // same identity: permits existing preloaded-episode cleanup
            if (completingEndBreadcrumbSequence > consumedBreadcrumbSequence)
                return true; // new physical chronology, independent of numeric EpisodeId
            return liveExactMissionStep; // overlapping authored debt still current
        }

        // V2-RC30: terminal completion and RouteMission reconciliation share the SAME
        // source-identity admission. A newer breadcrumb floor does not make an old
        // completion callback the owner of an unrelated LIVE mission. Independent
        // measured physical passage retains its own typed proof and commit path.
        // A mission-free terminal completion remains legal (cleanup/preloaded cursor).
        // This pure predicate does not mint Native Success or writer generations.
        internal static bool CanAdmitTerminalRouteSource(
            bool liveMissionPresent, bool exactUnconsumedMissionStep)
        {
            return !liveMissionPresent || exactUnconsumedMissionStep;
        }

        // V2-RC35: terminal recovery is keyed to the CURRENT unfinished route debt.
        // A newer downstream step in the SAME chain is chronology, not a physical
        // completion receipt for its unfinished predecessor. Do not clear the
        // terminal latch merely because player breadcrumbs authored that successor.
        // A genuinely independent, positive chain identity may still supersede
        // the old runtime transaction via the pre-existing independent-chain path.
        // No native success, route cursor, or writer authority is minted here.
        internal static bool CanSupersedeTerminalRecoveryWithRouteCandidate(
            int currentStepIndex, int candidateStepIndex,
            int currentChainId, int candidateChainId)
        {
            if (currentStepIndex < 0 || candidateStepIndex < currentStepIndex)
                return false;
            if (candidateStepIndex == currentStepIndex)
                return true; // current debt may enter a genuinely fresh execution epoch
            return currentChainId > 0 && candidateChainId > 0 &&
                currentChainId != candidateChainId;
        }

        // V2-RC36: Follow-owned recovery is NOT a Traversal->Follow handoff.
        // When there is no live Traversal transaction and NO prior handoff receipt,
        // the sole Writer Authority already belongs to Follow. Preserve its
        // generation and let navigation recovery proceed without repeatedly trying
        // to retire a non-existent Traversal producer. This is a NO-OP, never an
        // inferred receipt or a physical/terminal success. If any prior receipt
        // exists, its exact generation must be verified by the RC33 policy instead.
        internal static bool CanPreserveFollowOwnedTerminalRecoveryWithoutHandoff(
            bool transactionActive,
            TerminalRecoveryWriterYieldReceipt existingReceipt,
            FollowerMovementWriterAuthority.WriterDomain currentOwner,
            long currentGeneration)
        {
            return !transactionActive &&
                existingReceipt.OriginTraversalGeneration <= 0 &&
                existingReceipt.YieldedFollowGeneration <= 0 &&
                currentOwner == D.Follow && currentGeneration > 0;
        }

        // V2-RC31: terminal RECOVERY changes the exclusive writer, not the semantic
        // outcome. The stale execution debt may yield only the active Traversal
        // transaction's exact generation for the SAME live Mission and Step.
        // Uses existing route cursor and sole WriterAuthority; no second clock,
        // generation registry or success/physical-passage side effect is created.
        internal static bool CanAdmitTerminalRecoveryWriterHandoff(
            long missionId, int stepIndex, int stepEpisodeId, int stepEdgeId,
            int recoveryEpisodeId, int recoveryEdgeId,
            bool transactionActive, long transactionMissionId,
            long transactionWriterGeneration,
            FollowerMovementWriterAuthority.WriterDomain owner,
            long currentWriterGeneration)
        {
            return missionId > 0 && stepIndex >= 0 &&
                stepEpisodeId > 0 && stepEdgeId > 0 &&
                recoveryEpisodeId == stepEpisodeId && recoveryEdgeId == stepEdgeId &&
                transactionActive && transactionMissionId == missionId &&
                transactionWriterGeneration > 0 &&
                owner == D.Traversal && currentWriterGeneration > 0 &&
                currentWriterGeneration == transactionWriterGeneration;
        }

        // V2-RC32: an EXPLICIT episode is a signed identity, never a search hint.
        // Its authored edge (or exact live alias) must match before terminal work.
        // Legacy edge-only lookup remains available ONLY when no episode was offered.
        // This policy does not grant a writer or produce Native Success.
        internal static bool CanResolveTerminalEpisodeByEdgeOnly(
            int offeredEpisodeId, int offeredExecutionEdgeId)
        {
            return offeredEpisodeId <= 0 && offeredExecutionEdgeId > 0;
        }

        // V2-RC33: one immutable receipt of a *completed* Traversal-to-Follow
        // recovery handoff. This records provenance; it is not another authority,
        // does not confer permission to issue movement, and is stored only in the
        // existing per-bot recovery state until the latch is cleared/replaced.
        internal readonly struct TerminalRecoveryWriterYieldReceipt
        {
            internal readonly long MissionId;
            internal readonly int StepIndex;
            internal readonly int EpisodeId;
            internal readonly int EdgeId;
            internal readonly long OriginTraversalGeneration;
            internal readonly long YieldedFollowGeneration;

            internal TerminalRecoveryWriterYieldReceipt(
                long missionId, int stepIndex, int episodeId, int edgeId,
                long originTraversalGeneration, long yieldedFollowGeneration)
            {
                MissionId = missionId;
                StepIndex = stepIndex;
                EpisodeId = episodeId;
                EdgeId = edgeId;
                OriginTraversalGeneration = originTraversalGeneration;
                YieldedFollowGeneration = yieldedFollowGeneration;
            }
        }

        // Only the exact same failed debt, mission/step and STILL-CURRENT Follow
        // generation can acknowledge a previously completed handoff. A different
        // writer epoch, even another Follow, is not proof of the old handoff.
        // No latch, mission, success, cursor, or producer is modified by this policy.
        internal static bool CanAcknowledgeCompletedTerminalRecoveryYield(
            TerminalRecoveryWriterYieldReceipt receipt,
            long liveMissionId, int liveStepIndex, int liveEpisodeId, int liveEdgeId,
            FollowerMovementWriterAuthority.WriterDomain owner,
            long currentGeneration)
        {
            return receipt.MissionId > 0 && receipt.MissionId == liveMissionId &&
                receipt.StepIndex >= 0 && receipt.StepIndex == liveStepIndex &&
                receipt.EpisodeId > 0 && receipt.EpisodeId == liveEpisodeId &&
                receipt.EdgeId > 0 && receipt.EdgeId == liveEdgeId &&
                receipt.OriginTraversalGeneration > 0 &&
                receipt.YieldedFollowGeneration > receipt.OriginTraversalGeneration &&
                owner == D.Follow && currentGeneration > 0 &&
                currentGeneration == receipt.YieldedFollowGeneration;
        }

        // V2-RC38: immutable execution-debt identity belongs to the unified
        // Recovery disposition itself, not merely to a particular Runtime caller.
        // A future Producer cannot use a Follow owner or a valid-looking receipt
        // to acknowledge another episode, edge, or breadcrumb interval.
        // No clock, BotOwner, mutable registry, native passage, or mission update.
        internal static bool CanBindTerminalRecoveryExecutionDebt(
            long missionId, int stepIndex,
            int stepEpisodeId, int stepEdgeId,
            int stepStartBreadcrumb, int stepEndBreadcrumb,
            int recoveryEpisodeId, int recoveryEdgeId,
            int recoveryStartBreadcrumb, int recoveryEndBreadcrumb)
        {
            return missionId > 0 && stepIndex >= 0 &&
                stepEpisodeId > 0 && stepEdgeId > 0 &&
                stepEpisodeId == recoveryEpisodeId && stepEdgeId == recoveryEdgeId &&
                stepStartBreadcrumb == recoveryStartBreadcrumb &&
                stepEndBreadcrumb == recoveryEndBreadcrumb;
        }

        // V2-RC37: ONE pure, ordered disposition for terminal Recovery writer work.
        // A receipt is an acknowledgement, not an opportunity to claim a new writer.
        // A Follow owner without a transaction needs no handoff and mints no receipt.
        // An actual transfer is permitted only by the exact RC31 live origin ticket.
        // The caller must first establish the same live stalled Mission/Step/debt,
        // including start/end breadcrumb identities. No native/route success here.
        internal enum TerminalRecoveryWriterDisposition
        {
            Reject,
            AlreadyYielded,
            FollowAlreadyOwns,
            TransferTraversal
        }

        internal static TerminalRecoveryWriterDisposition ResolveTerminalRecoveryWriterDisposition(
            TerminalRecoveryWriterYieldReceipt receipt,
            long missionId, int stepIndex, int stepEpisodeId, int stepEdgeId,
            int recoveryEpisodeId, int recoveryEdgeId,
            int stepStartBreadcrumb, int stepEndBreadcrumb,
            int recoveryStartBreadcrumb, int recoveryEndBreadcrumb,
            bool transactionActive, long transactionMissionId,
            long transactionGeneration, D owner, long currentGeneration)
        {
            // V2-RC38: require the original snapshot on ALL four outcomes,
            // including AlreadyYielded and FollowAlreadyOwns. Fail closed before
            // interpreting a receipt or the sole current WriterAuthority.
            if (!CanBindTerminalRecoveryExecutionDebt(
                    missionId, stepIndex, stepEpisodeId, stepEdgeId,
                    stepStartBreadcrumb, stepEndBreadcrumb,
                    recoveryEpisodeId, recoveryEdgeId,
                    recoveryStartBreadcrumb, recoveryEndBreadcrumb))
                return TerminalRecoveryWriterDisposition.Reject;
            if (CanAcknowledgeCompletedTerminalRecoveryYield(
                    receipt, missionId, stepIndex, stepEpisodeId, stepEdgeId,
                    owner, currentGeneration))
                return TerminalRecoveryWriterDisposition.AlreadyYielded;
            if (CanPreserveFollowOwnedTerminalRecoveryWithoutHandoff(
                    transactionActive, receipt, owner, currentGeneration))
                return TerminalRecoveryWriterDisposition.FollowAlreadyOwns;
            if (CanAdmitTerminalRecoveryWriterHandoff(
                    missionId, stepIndex, stepEpisodeId, stepEdgeId,
                    recoveryEpisodeId, recoveryEdgeId,
                    transactionActive, transactionMissionId,
                    transactionGeneration, owner, currentGeneration))
                return TerminalRecoveryWriterDisposition.TransferTraversal;
            return TerminalRecoveryWriterDisposition.Reject;
        }

        // V2-RC39: every attempted traversal publishes ONE source-of-truth outcome
        // for regression accounting. It is deliberately independent of route-retirement
        // and movement ownership: only nativePhysicalSuccess can claim NativePhysical.
        // Teleport or any Assisted/Legacy success must NEVER be promoted by an overall
        // success flag. Callers may use this result for diagnostics; it cannot move bots.
        internal enum TraversalExecutionTruth
        {
            Unproven,
            NativePhysical,
            NonNativeCompletion,
            TeleportRecovery
        }

        internal static TraversalExecutionTruth ClassifyTraversalExecutionTruth(
            bool success, bool nativePhysicalSuccess, bool teleportUsed)
        {
            if (!success) return TraversalExecutionTruth.Unproven;
            if (teleportUsed) return TraversalExecutionTruth.TeleportRecovery;
            if (nativePhysicalSuccess) return TraversalExecutionTruth.NativePhysical;
            return TraversalExecutionTruth.NonNativeCompletion;
        }

        // V2-RC34: the probe already possesses an immutable writer generation from
        // acquisition. A delayed terminal callback from that probe must not borrow a
        // DIFFERENT Traversal writer generation installed by its successor. Follow,
        // PostTraversal and other non-Traversal handoff states remain governed by
        // the existing exact RouteMission/Cursor and physical passage contracts.
        // This check authorizes only a terminal cursor/mission acknowledgement;
        // it never writes movement or grants native/learning success.
        internal static bool CanAdmitProbeTerminalOrigin(
            long probeOriginGeneration,
            FollowerMovementWriterAuthority.WriterDomain currentOwner,
            long currentGeneration)
        {
            return probeOriginGeneration > 0 && currentGeneration > 0 &&
                (currentOwner != D.Traversal ||
                 currentGeneration == probeOriginGeneration);
        }

        // New feature implementations must offer only typed intents. Evaluation does not
        // move a Bot, teleport, clear a debt, consume a Mission or claim a Native outcome.
        internal static bool CanOffer(MovementIntent candidate, MovementIntent current, out string reason)
        {
            reason = string.Empty;
            if (!ValidIdentity(candidate) || !ValidIdentity(current)) { reason = "invalidCandidateIdentity"; return false; }
            if (candidate.BotId != current.BotId ||
                candidate.RaidGeneration != current.RaidGeneration ||
                candidate.WriterGeneration != current.WriterGeneration)
            { reason = "staleOwnerEpoch"; return false; }
            if (candidate.MissionId != current.MissionId ||
                candidate.StepIndex != current.StepIndex ||
                candidate.EpisodeId != current.EpisodeId ||
                candidate.ChainId != current.ChainId)
            { reason = "missionCursorMismatch"; return false; }
            if (!ScopeMatchesOwner(candidate.Domain, current.Domain))
            { reason = "ownerMismatch"; return false; }
            return true;
        }
    }
}
