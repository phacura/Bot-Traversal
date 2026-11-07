using EFT;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.AI;
using BotTraversal.Core;

namespace BotTraversal.Core
{
    /// <summary>
    /// CompileFix42 simplified generation-based movement ownership.
    ///
    /// Every explicit PIT movement transaction advances a per-follower generation. High-level PIT
    /// writers enter a short thread-local scope carrying that generation. Harmony entry guards then
    /// reject stale writes from a previous Follow/Traversal/Combat transaction instead of allowing
    /// the last caller to win simply because it ran later in the frame.
    ///
    /// EFT/SAIN movement that is not attributable to a scoped PIT writer is classification-only and
    /// stays fail-open. During Traversal/Teleport/Medical exclusivity, only destructive
    /// transaction boundaries (destination replacement, stop/pause ownership, teleport) remain guarded;
    /// Fix39's chain snapback guard remains the second line of defence below those entry points.
    /// </summary>
    internal static partial class FollowerMovementWriterAuthority
    {
        internal enum WriterDomain
        {
            Unknown = 0,
            Follow = 10,
            Recovery = 20,
            PostTraversal = 30,
            Traversal = 40,
            Combat = 50,
            Teleport = 60,
            Medical = 70
        }

        private const float FollowLeaseSeconds = 1.50f;
        private const float RecoveryLeaseSeconds = 1.25f;
        private const float CombatLeaseSeconds = 1.25f;
        private const float TraversalLeaseSeconds = 20.0f;
        private const float PostTraversalLeaseSeconds = 8.0f;
        private const float TeleportLeaseSeconds = 2.25f;
        private const float MedicalLeaseSeconds = 3.0f;
        private const float BlockTelemetrySeconds = 0.35f;
        private const float ObserveTelemetrySeconds = 0.90f;
        private const float DiagnosticStackSampleSeconds = 0.35f;

        private sealed class State
        {
            public long Generation = 1;
            public WriterDomain Owner = WriterDomain.Unknown;
            public float LeaseUntil;
            public string Reason = string.Empty;
            public float ChangedAt;
            public float NextBlockedTelemetryAt;
            public string LastBlockedKey = string.Empty;
            public float NextObservedTelemetryAt;
            public float NextDiagnosticStackSampleAt;
            public string LastObservedKey = string.Empty;

            // V2-RC20: ONE origin-bound observational receipt lives in the existing
            // owner State, not in a second physical-writer/ACK registry. Input issued
            // is NOT proof of motion. The only ACK comes from a later measured body
            // sample under the SAME owner generation, and is diagnostic-only until
            // all physical writers have migrated to the common seam.
            public long PhysicalInputGeneration;
            public WriterDomain PhysicalInputDomain = WriterDomain.Unknown;
            public float PhysicalInputAt = -1f;
            public int PhysicalInputFrame = -1;
            public float PhysicalProgressObservedAt = -1f;
            // V2-RC21: latest activity for the existing origin; unlike the first
            // one-shot RC20 receipt, these timestamps remain current throughout a
            // long no-NavMesh segment and cannot refresh the writer lease.
            public float PhysicalLastInputAt = -1f;
            public int PhysicalLastInputFrame = -1;
            public float PhysicalLastMeasuredProgressAt = -1f;
            public int PhysicalLastMeasuredProgressFrame = -1;
        }

        // Immutable diagnostic view of the EXISTING per-bot State. This never
        // acquires ownership, introduces a recovery controller, or ACKs a mission.
        // "NoCurrentInput" is not proof that EFT did not move: legacy external
        // writers remain intentionally outside the RC20 physical receipt seam.
        internal readonly struct PhysicalEvidenceSnapshot
        {
            internal readonly string Owner;
            internal readonly long Generation;
            internal readonly string EvidenceState;
            internal readonly float FirstInputAgeSeconds;
            internal readonly float LatestInputAgeSeconds;
            internal readonly float LatestProgressAgeSeconds;
            internal readonly bool FirstLaterFrameProgressObserved;

            internal PhysicalEvidenceSnapshot(string owner, long generation,
                string evidenceState, float firstInputAgeSeconds,
                float latestInputAgeSeconds, float latestProgressAgeSeconds,
                bool firstLaterFrameProgressObserved)
            {
                Owner = owner;
                Generation = generation;
                EvidenceState = evidenceState;
                FirstInputAgeSeconds = firstInputAgeSeconds;
                LatestInputAgeSeconds = latestInputAgeSeconds;
                LatestProgressAgeSeconds = latestProgressAgeSeconds;
                FirstLaterFrameProgressObserved = firstLaterFrameProgressObserved;
            }
        }

        internal sealed class ScopeState
        {
            public string ProfileId = string.Empty;
            public WriterDomain Domain;
            public long Generation;
            public ScopeState Previous;
            // Disposed out of LIFO order? Never let this retired ticket become active again.
            public bool Retired;
        }

        internal readonly struct WriteScope : IDisposable
        {
            private readonly ScopeState _scope;

            internal WriteScope(ScopeState scope)
            {
                _scope = scope;
            }

            // V2-RC17-CF1: the exact origin stamped by the already-admitted
            // physical scope. Diagnostic-only; never acquire/borrow a new owner.
            internal long IssuedGeneration => _scope != null ? _scope.Generation : 0L;

            public void Dispose()
            {
                if (_scope == null) return;
                _scope.Retired = true;
                // A nested callback may dispose an ancestor before its active child.
                // Keep the active child, but skip ALL retired ancestors when it exits.
                if (ReferenceEquals(CurrentScope, _scope))
                {
                    CurrentScope = _scope.Previous;
                    while (CurrentScope != null && CurrentScope.Retired)
                        CurrentScope = CurrentScope.Previous;
                }
            }
        }

        private static readonly Dictionary<string, State> States =
            new Dictionary<string, State>(StringComparer.Ordinal);

        [ThreadStatic]
        private static ScopeState CurrentScope;

        // V2-RC4: even internal movement escape hatches must have an exact owner.
        // Thread-global bypass depth allowed the same thread to touch an unrelated Bot.
        // No second writer registry: the generation is checked against the SAME States map.
        internal sealed class InternalBypassState
        {
            internal string BotId;
            internal long Generation;
            internal InternalBypassState Previous;
            internal bool Retired;
        }

        [ThreadStatic]
        private static InternalBypassState CurrentInternalBypass;

        internal readonly struct InternalBypassScope : IDisposable
        {
            private readonly InternalBypassState _issued;
            internal InternalBypassScope(InternalBypassState issued) { _issued = issued; }
            public void Dispose()
            {
                if (_issued == null) return;
                _issued.Retired = true;
                if (ReferenceEquals(CurrentInternalBypass, _issued))
                {
                    CurrentInternalBypass = _issued.Previous;
                    while (CurrentInternalBypass != null && CurrentInternalBypass.Retired)
                        CurrentInternalBypass = CurrentInternalBypass.Previous;
                }
            }
        }

        // Native-lease Sprint restoration is not a license for arbitrary movement.
        // This source-bound exception is consumed ONLY by the Sprint and EnableSprint
        // Harmony write kinds. Other native writers cannot inherit the exception.
        // It is minted only for an already registered Bot, and checked on every write.
        internal static InternalBypassScope EnterInternalBypass(BotOwner bot)
        {
            if (bot == null || string.IsNullOrEmpty(bot.ProfileId) ||
                !States.TryGetValue(bot.ProfileId, out State state) ||
                state.Generation <= 0 || state.Owner == WriterDomain.Unknown)
                return new InternalBypassScope(null);
            InternalBypassState issued = new InternalBypassState
            {
                BotId = bot.ProfileId,
                Generation = state.Generation,
                Previous = CurrentInternalBypass
            };
            CurrentInternalBypass = issued;
            return new InternalBypassScope(issued);
        }

        // V2-RC13: the only remaining internal bypass is a narrowly typed,
        // synchronous Sprint restoration used by the protected native-lease gate.
        // It must NOT authorize Stop, Teleport, GoToPoint, Pause, or future APIs.
        // Intent permission is checked again at the actual Harmony write boundary,
        // in addition to the existing exact Bot, owner generation and ambient scope.
        private static bool IsAuthorizedSprintCorrectionKind(string writeKind)
        {
            return string.Equals(writeKind, "BotMover.Sprint", StringComparison.Ordinal) ||
                   string.Equals(writeKind, "Player.EnableSprint", StringComparison.Ordinal);
        }

        private static bool IsExactInternalBypass(BotOwner bot, string writeKind)
        {
            InternalBypassState issued = CurrentInternalBypass;
            return IsAuthorizedSprintCorrectionKind(writeKind) &&
                issued != null && !issued.Retired && bot != null &&
                FollowerMovementV2IntentGateway.IsScopeBoundToTarget(issued.BotId, bot.ProfileId) &&
                (CurrentScope == null ||
                 (!CurrentScope.Retired &&
                  FollowerMovementV2IntentGateway.IsScopeBoundToTarget(CurrentScope.ProfileId, bot.ProfileId))) &&
                States.TryGetValue(bot.ProfileId, out State state) &&
                state.Owner != WriterDomain.Unknown && state.Generation > 0 &&
                state.Generation == issued.Generation;
        }

        internal static void Reset(BotOwner bot)
        {
            string profileId = bot?.ProfileId ?? string.Empty;
            if (!string.IsNullOrEmpty(profileId))
            {
                States.Remove(profileId);
            }
        }

        internal static void ClearAll()
        {
            States.Clear();
            CurrentScope = null;
            CurrentInternalBypass = null;
        }

        internal static long BeginTraversal(BotOwner bot, string reason)
        {
            return BeginTransaction(bot, WriterDomain.Traversal, reason, TraversalLeaseSeconds, forceNewGeneration: true);
        }

        // LearningV3.75 Phase 2: adjacent action nodes inside one persistent executable
        // physical route transaction renew the existing Traversal lease without minting a
        // new writer generation. The caller must prove the expected generation/chain; a
        // mismatch fails closed and lets the normal BeginTraversal path create a new epoch.
        internal static long ContinueTraversal(BotOwner bot, long expectedGeneration, string reason)
        {
            if (!TryGetCurrent(bot, out State state) ||
                state.Owner != WriterDomain.Traversal ||
                state.Generation <= 0 ||
                state.Generation != expectedGeneration)
            {
                return 0;
            }

            return BeginTransaction(
                bot, WriterDomain.Traversal, reason, TraversalLeaseSeconds, forceNewGeneration: false);
        }

        internal static long BeginPostTraversal(BotOwner bot, string reason)
        {
            return BeginTransaction(bot, WriterDomain.PostTraversal, reason, PostTraversalLeaseSeconds, forceNewGeneration: true);
        }

        internal static long BeginTeleport(BotOwner bot, string reason)
        {
            bool alreadyTeleport = TryGetCurrent(bot, out State state) && state.Owner == WriterDomain.Teleport;
            return BeginTransaction(
                bot,
                WriterDomain.Teleport,
                reason,
                TeleportLeaseSeconds,
                forceNewGeneration: !alreadyTeleport);
        }

        internal static long BeginMedical(BotOwner bot, string reason)
        {
            return BeginTransaction(bot, WriterDomain.Medical, reason, MedicalLeaseSeconds, forceNewGeneration: true);
        }

        internal static long AcquireRecovery(BotOwner bot, string reason)
        {
            return BeginTransaction(bot, WriterDomain.Recovery, reason, RecoveryLeaseSeconds, forceNewGeneration: false);
        }

        internal static long AcquireCombat(BotOwner bot, string reason)
        {
            return BeginTransaction(bot, WriterDomain.Combat, reason, CombatLeaseSeconds, forceNewGeneration: false);
        }

        internal static long AcquireFollow(BotOwner bot, string reason)
        {
            if (bot == null || string.IsNullOrEmpty(bot.ProfileId))
            {
                return 0;
            }

            State state = GetOrCreate(bot.ProfileId);
            ExpireSoftLeaseIfNeeded(state);

            if (state.Owner == WriterDomain.PostTraversal)
            {
                state.LeaseUntil = Mathf.Max(state.LeaseUntil, Time.time + FollowLeaseSeconds);
                return state.Generation;
            }

            if (state.Owner == WriterDomain.Traversal ||
                state.Owner == WriterDomain.Combat ||
                state.Owner == WriterDomain.Teleport ||
                state.Owner == WriterDomain.Medical)
            {
                return 0;
            }

            return BeginTransaction(bot, WriterDomain.Follow, reason, FollowLeaseSeconds, forceNewGeneration: false);
        }

        // CF75 shadow-only identity peek. Unlike GetCurrentGeneration, it never expires
        // a soft lease or mutates the authoritative ownership state.
        internal static long PeekOwnerGenerationForCF75(BotOwner bot, out WriterDomain owner)
        {
            owner = WriterDomain.Unknown;
            if (bot == null || string.IsNullOrEmpty(bot.ProfileId) ||
                !States.TryGetValue(bot.ProfileId, out State state))
                return 0;
            owner = state.Owner;
            return state.Generation;
        }

        internal static long GetCurrentGeneration(BotOwner bot, out WriterDomain owner)
        {
            owner = WriterDomain.Unknown;
            if (bot == null || string.IsNullOrEmpty(bot.ProfileId) ||
                !States.TryGetValue(bot.ProfileId, out State state))
            {
                return 0;
            }

            ExpireSoftLeaseIfNeeded(state);
            owner = state.Owner;
            return state.Generation;
        }

        // V2-RC2: every explicit Follow retirement crosses this one exact,
        // generation-bound, single-owner transaction. These facades preserve the
        // existing API shape while deleting five independently drifting contracts.
        private static bool RetireExactToFollow(BotOwner bot, WriterDomain requiredOwner,
            long originGeneration, string reason, bool checkLease)
        {
            if (!TryGetCurrent(bot, out State state) ||
                !FollowerMovementV2IntentGateway.CanRetireToFollow(
                    state.Owner, requiredOwner, state.Generation, originGeneration,
                    checkLease, Time.time <= state.LeaseUntil, out _))
                return false;

            long next = BeginTransaction(bot, WriterDomain.Follow, reason,
                FollowLeaseSeconds, forceNewGeneration: true);
            return next > 0 && TryGetCurrent(bot, out State after) &&
                after.Owner == WriterDomain.Follow && after.Generation == next;
        }

        internal static void TransitionTraversalToFollowIfCurrent(BotOwner bot,
            long traversalGeneration, string reason)
        {
            RetireExactToFollow(bot, WriterDomain.Traversal, traversalGeneration, reason, false);
        }

        internal static bool TransitionTraversalToFollowIfIdleExact(BotOwner bot,
            long expectedGeneration, string reason)
        {
            return RetireExactToFollow(bot, WriterDomain.Traversal,
                expectedGeneration, reason, false);
        }

        internal static bool TransitionTeleportToFollowExact(BotOwner bot,
            long expectedGeneration, string reason)
        {
            return RetireExactToFollow(bot, WriterDomain.Teleport,
                expectedGeneration, reason, true);
        }

        internal static bool TransitionPostTraversalToFollowExact(BotOwner bot,
            long expectedGeneration, string reason)
        {
            return RetireExactToFollow(bot, WriterDomain.PostTraversal,
                expectedGeneration, reason, true);
        }

        internal static bool TransitionPostTraversalToFollowIfCurrent(BotOwner bot,
            long expectedGeneration, string reason)
        {
            return RetireExactToFollow(bot, WriterDomain.PostTraversal,
                expectedGeneration, reason, false);
        }

        internal static bool TransitionCombatToFollowExact(BotOwner bot,
            long expectedGeneration, string reason)
        {
            return RetireExactToFollow(bot, WriterDomain.Combat,
                expectedGeneration, reason, true);
        }

        // V2-RC5: the only public route permission/entry point. The old producer
        // was checking its own fabricated reference identity and then separately
        // issuing a scope. Now ownership comes from the ONE authoritative state.
        // Invalid offers never receive a scope, and cannot use the unscoped EFT path.
        internal static bool TryEnterVerifiedRouteIntent(BotOwner bot,
            FollowerMovementV2IntentGateway.MovementIntent offer,
            out WriteScope scope, out string reason)
        {
            scope = default(WriteScope);
            reason = string.Empty;
            if (bot == null || string.IsNullOrEmpty(bot.ProfileId))
            {
                reason = "missingTargetBot";
                return false;
            }
            // V2-RC7: the producer is ONLY allowed to submit an offer. Do not
            // accept a second producer-authored "current" Mission/Step/Episode/Chain.
            // The snapshot is read independently from the runtime's canonical
            // mission and cursor registries and has no creation/mutation side effects.
            if (!FollowerTraversalTestRuntime.TryReadV2LiveRouteAdmission(
                bot, out long currentMissionId, out int currentStep,
                out int currentEpisode, out int currentChain,
                out long routeOriginGeneration, out reason))
                return false;
            WriterDomain currentOwner;
            long currentGeneration = PeekExactGeneration(bot, out currentOwner);
            if (routeOriginGeneration <= 0 || routeOriginGeneration != currentGeneration)
            {
                reason = "v2RouteCursorOriginNoLongerOwnsWriter";
                return false;
            }
            if (!FollowerMovementV2IntentGateway.CanAdmitLiveRoute(
                offer, bot.ProfileId, FollowerTraversalTestRuntime.CurrentRaidGenerationForV2,
                true, currentMissionId, currentStep, currentEpisode, currentChain,
                currentOwner, currentGeneration, out reason))
                return false;
            // A validated route identity is an offer, not an issued physical ticket.
            // The one native-commit gate must independently approve the actual
            // Owner/Generation and return false if the owner changed before issuance.
            return TryEnterBoundPhysicalMotion(bot, WriterDomain.Traversal,
                currentGeneration, out scope, out reason);
        }

        // V2-RC16: the Follow producer requests ONE typed admission here, rather
        // than acquiring owner, opening a scope, and issuing a native write via
        // independent producer rules. Reuses the sole State and exact physical
        // motion gate; does not create a second ticket registry or lease.
        internal static bool TryEnterFollowPhysicalMotion(BotOwner bot, string reason,
            out WriteScope scope, out string blockReason)
        {
            if(FollowerTraversalTestRuntime.IsCapabilityPortalWaiting(bot)&&reason!="portalQueueHold")
            {scope=default(WriteScope);blockReason="portalQueueOwnsWait";return false;}
            scope = default(WriteScope);
            blockReason = string.Empty;
            long issuedGeneration = AcquireFollow(bot, reason);
            if (issuedGeneration <= 0)
            {
                blockReason = "v2FollowOwnerNotGranted";
                return false;
            }
            return TryEnterBoundPhysicalMotion(bot, WriterDomain.Follow,
                issuedGeneration, out scope, out blockReason);
        }

        internal static WriteScope Enter(BotOwner bot, WriterDomain domain, long generation)
        {
            if (bot == null || string.IsNullOrEmpty(bot.ProfileId))
            {
                return new WriteScope(null);
            }
            // V2: a bad token is deliberately carried in the scope so CanWrite rejects it.
            // A null scope would fall through to the legacy unscoped, fail-open compatibility path.


            ScopeState scope = new ScopeState
            {
                ProfileId = bot.ProfileId,
                Domain = domain,
                Generation = generation,
                Previous = CurrentScope
            };
            CurrentScope = scope;
            return new WriteScope(scope);
        }

        internal static long PeekExactGeneration(BotOwner bot, out WriterDomain owner)
        {
            return PeekOwnerGenerationForCF75(bot, out owner);
        }

        // The sole issuance gate for a native-physics movement ticket. This is not
        // a new owner state: it consumes ONLY a known origin generation and the
        // currently registered domain, and never borrows a newer current epoch.
        internal static bool TryEnterBoundPhysicalMotion(BotOwner bot, WriterDomain domain,
            long issuedGeneration, out WriteScope scope, out string reason)
        {
            scope = default(WriteScope);
            reason = string.Empty;
            if (bot == null || string.IsNullOrEmpty(bot.ProfileId) ||
                domain == WriterDomain.Unknown || issuedGeneration <= 0 ||
                !States.TryGetValue(bot.ProfileId, out State state))
            {
                reason = "v2PhysicalOriginMissing";
                return false;
            }
            ExpireSoftLeaseIfNeeded(state);
            if (!FollowerMovementV2IntentGateway.AuthorizeScoped(
                domain, issuedGeneration, state.Owner, state.Generation,
                "V2.NativePhysicalOrigin", out reason))
                return false;
            scope = Enter(bot, domain, issuedGeneration);
            return true;
        }

        // V2-RC6: native Player.Move and CharacterController writes are not Harmony
        // entrypoints. They MUST be guarded at the physical commit seam by an
        // existing, short-lived exact scope. Unlike CanWrite's EFT/SAIN compatibility
        // behavior, this PIT-only physical writer NEVER admits unscoped calls.
        // There is no second registry, token, lease, or owner arbitration here.
        internal static bool CanCommitScopedPhysicalMotion(BotOwner bot, out string reason)
        {
            reason = string.Empty;
            ScopeState scope = CurrentScope;
            if (bot == null || string.IsNullOrEmpty(bot.ProfileId) || scope == null || scope.Retired)
            {
                reason = "v2PhysicalCommitRequiresIssuedScope";
                return false;
            }
            if (!FollowerMovementV2IntentGateway.IsScopeBoundToTarget(scope.ProfileId, bot.ProfileId))
            {
                reason = "v2PhysicalCommitForeignBotScope";
                return false;
            }
            if (!States.TryGetValue(bot.ProfileId, out State state))
            {
                reason = "v2PhysicalCommitMissingOwner";
                return false;
            }
            ExpireSoftLeaseIfNeeded(state);
            return FollowerMovementV2IntentGateway.AuthorizeScoped(
                scope.Domain, scope.Generation, state.Owner, state.Generation,
                "V2.NativePhysicalCommit", out reason);
        }

        // V2-RC23: Follow destination publication is a planning commit, not a physical
        // input or successful path ACK. Issue only under the exact Follow scope
        // already acquired by the producer. Do not fabricate a movement receipt.
        internal static bool TryCommitScopedFollowPath(BotOwner bot, Vector3 destination,
            out NavMeshPathStatus status)
        {
            status = NavMeshPathStatus.PathInvalid;
            ScopeState scope = CurrentScope;
            if (bot == null || scope == null || scope.Domain != WriterDomain.Follow ||
                !CanCommitScopedPhysicalMotion(bot, out _))
                return false;
            status = bot.BotOwnerGoToPoint("R1-A2F5CF73F97F",@bot,destination,true,-1f,false,false);
            return true;
        }

        // V2-RC22: the physical backend commit seam. All canonical route
        // translation/locomotion outputs come through these typed methods. They
        // recheck the *same* ticket AT the native write (entry checks alone are
        // insufficient after nested callbacks), and publish input evidence only
        // after the actual EFT writer returns. No alternate owner or ACK store.
        // A rejected commit is never silently substituted with an unscoped write.
        internal static bool TryCommitNativePlayerMove(BotOwner bot, Player player, Vector2 input)
        {
            if (player == null || !CanCommitScopedPhysicalMotion(bot, out _))
                return false;
            if (!player.TryPlayerMove("R1-9B456E5839EB",@bot,input)) return false;
            RecordScopedPhysicalInputIssued(bot, "EFT.Player.Move");
            return true;
        }

        internal static bool TryCommitNativeTransitionVelocity(BotOwner bot, Player player, Vector3 velocity)
        {
            if (player?.MovementContext == null ||
                !CanCommitScopedPhysicalMotion(bot, out _))
                return false;
            if (!player.MovementContext.TryMovementContextLastBlendMotionDelta("R1-A5D8F4E94F81",@bot,velocity)) return false;
            RecordScopedPhysicalInputIssued(bot, "EFT.Transition.LastBlendMotionDelta");
            return true;
        }

        internal static bool TryCommitNativeApproachMotion(BotOwner bot, Player player,
            Vector3 motion, float deltaTime, bool isFallback)
        {
            if (player?.MovementContext == null ||
                !CanCommitScopedPhysicalMotion(bot, out _))
                return false;
            if (!player.MovementContext.TryMovementContextApplyApproachMotion("R1-80C374FF3832",@bot,motion,deltaTime)) return false;
            RecordScopedPhysicalInputIssued(bot, isFallback
                ? "EFT.MovementContext.ApplyApproachMotionFallback"
                : "EFT.MovementContext.ApplyApproachMotion");
            return true;
        }

        internal static bool TryCommitCharacterControllerMotion(BotOwner bot, Player player,
            Vector3 motion, float deltaTime)
        {
            if (player?.CharacterController == null ||
                !CanCommitScopedPhysicalMotion(bot, out _))
                return false;
            if (!player.CharacterController.TryICharacterControllerMove("R1-17E5B4E1B173",@bot,motion,deltaTime)) return false;
            RecordScopedPhysicalInputIssued(bot, "EFT.CharacterController.MoveFallback");
            return true;
        }

        // V2-RC20: record that an actual native input / native Transition velocity
        // was published after the existing exact physical commit gate approved it.
        // Repeated frames in one generation do NOT allocate a ticket, change owner,
        // refresh a lease, or log again. No physical success is inferred here.
        internal static void RecordScopedPhysicalInputIssued(BotOwner bot, string inputKind)
        {
            if (bot == null || !CanCommitScopedPhysicalMotion(bot, out _) ||
                !States.TryGetValue(bot.ProfileId, out State state))
                return;
            ScopeState scope = CurrentScope;
            if (scope == null || scope.Retired)
                return;

            bool sameOrigin = state.PhysicalInputGeneration == scope.Generation &&
                state.PhysicalInputDomain == scope.Domain;
            if (!sameOrigin)
            {
                state.PhysicalInputDomain = scope.Domain;
                state.PhysicalInputGeneration = scope.Generation;
                state.PhysicalInputAt = Time.time;
                state.PhysicalInputFrame = Time.frameCount;
                state.PhysicalProgressObservedAt = -1f;
                state.PhysicalLastMeasuredProgressAt = -1f;
                state.PhysicalLastMeasuredProgressFrame = -1;
            }
            // Refresh activity ONLY after a real physical input and exact scope;
            // this is not a new issue ticket and never refreshes an owner lease.
            state.PhysicalLastInputAt = Time.time;
            state.PhysicalLastInputFrame = Time.frameCount;
            if (sameOrigin)
                return;

            FollowerTraversalTestRuntime.RecordMovementWriterAuthorityEvent(
                bot, "movementPhysicalInputIssued", new
                {
                    owner = scope.Domain.ToString(), generation = scope.Generation,
                    inputKind = inputKind ?? string.Empty,
                    bodyProgressObserved = false,
                    traversalSuccessRecorded = false,
                    action = "recordOriginOfActualInputNotPhysicalProgressOrCompletion"
                });
        }

        // Called only from the pre-existing measured-body watchdog, NOT from the
        // movement producer or Player.Move. It is intentionally an observation:
        // does not advance route cursor, retire a lease, complete a native action,
        // or reset Recovery. A generation change invalidates the pending receipt.
        internal static bool TryObserveIssuedPhysicalProgress(BotOwner bot,
            float measuredBodyDisplacement, string observationSource)
        {
            if (bot == null || string.IsNullOrEmpty(bot.ProfileId) ||
                !States.TryGetValue(bot.ProfileId, out State state) ||
                float.IsNaN(measuredBodyDisplacement) ||
                float.IsInfinity(measuredBodyDisplacement) ||
                measuredBodyDisplacement < 0.055f ||
                state.PhysicalInputGeneration <= 0 ||
                state.PhysicalInputDomain != state.Owner ||
                state.PhysicalInputGeneration != state.Generation ||
                state.PhysicalInputAt < 0f ||
                // A body sample in the input's own frame is not a next-frame ACK.
                Time.frameCount <= state.PhysicalInputFrame)
                return false;

            // Repeated valid measured progress refreshes the latest activity, but
            // does not generate additional first-ACK events or advance a step.
            state.PhysicalLastMeasuredProgressAt = Time.time;
            state.PhysicalLastMeasuredProgressFrame = Time.frameCount;
            if (state.PhysicalProgressObservedAt >= 0f)
                return true;
            state.PhysicalProgressObservedAt = Time.time;
            FollowerTraversalTestRuntime.RecordMovementWriterAuthorityEvent(
                bot, "movementPhysicalProgressObserved", new
                {
                    owner = state.PhysicalInputDomain.ToString(),
                    generation = state.PhysicalInputGeneration,
                    inputAt = state.PhysicalInputAt,
                    observedAt = state.PhysicalProgressObservedAt,
                    measuredBodyDisplacement,
                    observationSource = observationSource ?? string.Empty,
                    nativeSuccessRecorded = false,
                    traversalSuccessRecorded = false,
                    routeStepAdvanced = false,
                    action = "sameGenerationLaterBodyEvidenceDoesNotProveNativeOrMissionCompletion"
                });
            return true;
        }

        // Snapshot MUST be taken before a Recovery writer handoff advances Owner/
        // Generation; taking it afterwards would mislabel an old Traversal stall
        // as the newly acquired Follow generation's lack of input.
        internal static PhysicalEvidenceSnapshot SnapshotPhysicalEvidenceForDiagnostics(BotOwner bot)
        {
            if (bot == null || string.IsNullOrEmpty(bot.ProfileId) ||
                !States.TryGetValue(bot.ProfileId, out State state))
                return new PhysicalEvidenceSnapshot("Unknown", 0L, "NoOwnerState",
                    -1f, -1f, -1f, false);

            bool sameOrigin = state.PhysicalInputGeneration > 0 &&
                state.PhysicalInputGeneration == state.Generation &&
                state.PhysicalInputDomain == state.Owner;
            bool hasFirstAck = sameOrigin && state.PhysicalProgressObservedAt >= 0f;
            string evidenceState = !sameOrigin ? "NoCurrentGenerationInput" :
                hasFirstAck ? "FirstProgressObserved" : "IssuedAwaitingProgress";
            return new PhysicalEvidenceSnapshot(state.Owner.ToString(), state.Generation,
                evidenceState,
                sameOrigin && state.PhysicalInputAt >= 0f
                    ? Mathf.Max(0f, Time.time - state.PhysicalInputAt) : -1f,
                sameOrigin && state.PhysicalLastInputAt >= 0f
                    ? Mathf.Max(0f, Time.time - state.PhysicalLastInputAt) : -1f,
                hasFirstAck && state.PhysicalLastMeasuredProgressAt >= 0f
                    ? Mathf.Max(0f, Time.time - state.PhysicalLastMeasuredProgressAt) : -1f,
                hasFirstAck);
        }

        internal static bool CanWrite(BotOwner bot, string writeKind, out string blockReason)
        {
            blockReason = string.Empty;
            // V2-RC15 regression containment: the internal exception is authorized
            // ONLY for the two exact Sprint correction operations. It is not a global
            // movement-lock switch. A GoToPoint/Stop/Teleport nested under a Sprint
            // callback must take the ordinary owner/scope/transactional path BELOW,
            // never inherit Sprint permission and never be unconditionally vetoed.
            // Invalid Sprint bypasses remain fail-closed (bot + generation + scope).
            if (CurrentInternalBypass != null && IsAuthorizedSprintCorrectionKind(writeKind))
            {
                if (IsExactInternalBypass(bot, writeKind)) return true;
                blockReason = "v2BypassBotOrEpochMismatch";
                if (bot != null && !string.IsNullOrEmpty(bot.ProfileId) &&
                    States.TryGetValue(bot.ProfileId, out State bypassTargetState))
                    RecordBlocked(bot, bypassTargetState, writeKind, WriterDomain.Unknown, blockReason);
                return false;
            }
            // Non-Sprint calls always reach the same native/EFT/SAIN compatibility
            // and PIT exact-scope checks used when no bypass exists. No extra grant.
            if (bot == null || string.IsNullOrEmpty(bot.ProfileId) || !BossPlayers.IsFollower(bot))
            {
                return true;
            }

            // V2-RC3: an explicit scoped writer must NEVER re-enter the EFT/SAIN
            // unscoped compatibility branch by targeting another Bot or by running
            // before its authority state is registered. This also covers nested scopes.
            ScopeState scope = CurrentScope;
            if (scope != null && scope.Retired)
            {
                blockReason = "v2RetiredMovementScope";
                if (States.TryGetValue(bot.ProfileId, out State retiredState))
                    RecordBlocked(bot, retiredState, writeKind, scope.Domain, blockReason);
                return false;
            }
            if (scope != null &&
                !FollowerMovementV2IntentGateway.IsScopeBoundToTarget(
                    scope.ProfileId, bot.ProfileId))
            {
                blockReason = "v2ForeignBotScopeRejected";
                if (States.TryGetValue(bot.ProfileId, out State foreignTargetState))
                    RecordBlocked(bot, foreignTargetState, writeKind, scope.Domain, blockReason);
                return false;
            }

            if (!States.TryGetValue(bot.ProfileId, out State state))
            {
                if (scope == null)
                    return true; // Genuine, unscoped EFT/SAIN compatibility only.

                // The explicit ticket has no authoritative owner. A missing registry
                // is a rejection, not permission to issue a native movement command.
                state = GetOrCreate(bot.ProfileId);
                blockReason = "v2ScopedWriterHasNoAuthorityState";
                RecordBlocked(bot, state, writeKind, scope.Domain, blockReason);
                return false;
            }

            ExpireSoftLeaseIfNeeded(state);

            if (scope != null)
            {
                if (FollowerMovementV2IntentGateway.AuthorizeScoped(
                        scope.Domain, scope.Generation, state.Owner, state.Generation,
                        writeKind, out blockReason))
                    return true;
                RecordBlocked(bot, state, writeKind, scope.Domain, blockReason);
                return false;
            }

            // CompileFix43: stack classification remains diagnostic-only, but we no longer build a
            // StackTrace for every soft movement API call. Transactional writes under an exclusive
            // owner still classify immediately because the result may decide whether to reject the
            // destructive write; ordinary movement samples at most a few times per second per bot.
            bool exclusiveOwner = state.Owner == WriterDomain.Traversal ||
                                  state.Owner == WriterDomain.PostTraversal ||
                                  state.Owner == WriterDomain.Teleport ||
                                  state.Owner == WriterDomain.Medical;
            bool transactionalWrite = exclusiveOwner && IsTransactionalWrite(writeKind);
            WriterDomain inferred = WriterDomain.Unknown;
            if (transactionalWrite)
            {
                inferred = InferStackWriterDomain();
                if (inferred == state.Owner)
                {
                    return true;
                }

                if (inferred == WriterDomain.Combat &&
                    FollowerStateAuthorityCoordinator.HasCombatMovementClaim(bot, out string combatReason))
                {
                    long combatGeneration = AcquireCombat(bot, "verifiedCombatPreempt:" + (combatReason ?? "combat"));
                    if (combatGeneration > 0)
                    {
                        return true;
                    }
                }

                blockReason = $"exclusiveTransactionRejected writer={inferred} owner={state.Owner} generation={state.Generation}";
                RecordBlocked(bot, state, writeKind, inferred, blockReason);
                return false;
            }

            if (Time.time >= state.NextDiagnosticStackSampleAt)
            {
                state.NextDiagnosticStackSampleAt = Time.time + DiagnosticStackSampleSeconds;
                inferred = InferStackWriterDomain();
                if (inferred != WriterDomain.Unknown &&
                    inferred != state.Owner &&
                    !(state.Owner == WriterDomain.PostTraversal && inferred == WriterDomain.Follow))
                {
                    RecordObserved(bot, state, writeKind, inferred);
                }
            }

            // Ordinary Follow/Recovery/Combat movement is fail-open when it does not carry a PIT
            // scope. Generation enforcement remains strict for all explicit PIT scopes above.
            return true;
        }

        internal static bool ShouldHardBlockSprintDisable(BotOwner bot, out string reason)
        {
            reason = string.Empty;
            // A bypass from another Bot or an obsolete generation does not suppress
            // sprint protection. The regular state-machine rules still apply.
            if (IsExactInternalBypass(bot, "BotMover.Sprint"))
            {
                return false;
            }
            if (bot == null || !BossPlayers.IsFollower(bot))
            {
                return false;
            }

            MovementContext movement = bot.GetPlayer?.MovementContext;
            if (bot.Mover?.NoSprint == true ||
                (movement != null && (!movement.CanSprint || !movement.CanWalk)))
            {
                return false;
            }

            if (!FollowerTraversalTestRuntime.ShouldProtectPostTraversalSprint(bot, out string leaseReason))
            {
                return false;
            }

            WriterDomain owner;
            GetCurrentGeneration(bot, out owner);
            if (owner == WriterDomain.Medical || owner == WriterDomain.Teleport ||
                FollowerMedical.IsUsingMedical(bot) ||
                FollowerMedical.IsHandsInteractionActiveForAuthority(bot.GetPlayer) ||
                FollowerStateAuthorityCoordinator.HasCombatMovementClaim(bot, out _))
            {
                return false;
            }

            reason = string.IsNullOrEmpty(leaseReason)
                ? "postTraversalSprintLease"
                : leaseReason;
            return true;
        }

        internal static WriterDomain InferCurrentWriterDomain()
        {
            if (CurrentScope != null)
            {
                return CurrentScope.Domain;
            }

            return InferStackWriterDomain();
        }

        private static WriterDomain InferStackWriterDomain()
        {
            try
            {
                StackTrace trace = new StackTrace(2, false);
                int frameCount = Math.Min(18, trace.FrameCount);
                for (int i = 0; i < frameCount; i++)
                {
                    Type type = trace.GetFrame(i)?.GetMethod()?.DeclaringType;
                    if (type == null)
                    {
                        continue;
                    }

                    string name = type.FullName ?? type.Name ?? string.Empty;
                    if (type == typeof(FollowerControlBackend) ||
                        name.IndexOf("FollowerMovementWriterAuthority", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("Harmony", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        continue;
                    }

                    if (name.IndexOf("FollowerMedical", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("PatrolHeal", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return WriterDomain.Medical;
                    }

                    if (name.IndexOf("FollowerSafeTeleport", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("Teleport", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return WriterDomain.Teleport;
                    }

                    if (name.IndexOf("FollowerTraversalTestRuntime", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("Traversal", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return WriterDomain.Traversal;
                    }

                    if (name.IndexOf("FollowerNavigationRecoveryRuntime", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("Recovery", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return WriterDomain.Recovery;
                    }

                    if (name.IndexOf("FollowAction", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("FollowerPatrol", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return WriterDomain.Follow;
                    }

                    if (name.StartsWith("SAIN.", StringComparison.Ordinal) ||
                        name.IndexOf("SAINAddon", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("Combat", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("AvoidThreat", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("Flashed", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("Evacuation", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return WriterDomain.Combat;
                    }
                }
            }
            catch
            {
            }

            return WriterDomain.Unknown;
        }

        private static long BeginTransaction(
            BotOwner bot,
            WriterDomain domain,
            string reason,
            float leaseSeconds,
            bool forceNewGeneration)
        {
            if (bot == null || string.IsNullOrEmpty(bot.ProfileId))
            {
                return 0;
            }

            State state = GetOrCreate(bot.ProfileId);
            ExpireSoftLeaseIfNeeded(state);

            if (!FollowerMovementV2IntentGateway.CanAcquire(state.Owner, domain))
            {
                return 0;
            }

            bool changed = state.Owner != domain || forceNewGeneration;
            WriterDomain previousOwner = state.Owner;
            long previousGeneration = state.Generation;
            if (changed)
            {
                state.Generation = Math.Max(1, state.Generation + 1);
                state.Owner = domain;
                state.ChangedAt = Time.time;
            }

            float requestedLeaseUntil = Time.time + Mathf.Max(0.10f, leaseSeconds);
            state.LeaseUntil = changed
                ? requestedLeaseUntil
                : Mathf.Max(state.LeaseUntil, requestedLeaseUntil);
            state.Reason = reason ?? string.Empty;

            if (changed)
            {
                FollowerTraversalTestRuntime.RecordMovementWriterAuthorityEvent(
                    bot,
                    "movementWriterGenerationAdvanced",
                    new
                    {
                        previousOwner = previousOwner.ToString(),
                        owner = domain.ToString(),
                        previousGeneration,
                        generation = state.Generation,
                        reason = state.Reason,
                        leaseSeconds
                    });
            }

            return state.Generation;
        }

        private static void ExpireSoftLeaseIfNeeded(State state)
        {
            if (state == null || Time.time <= state.LeaseUntil)
            {
                return;
            }

            // Traversal is explicitly released by the owning probe/generation. Teleport and Medical
            // retain bounded safety leases so a missing optional completion callback cannot strand a
            // follower in an exclusive owner forever.
            if (state.Owner == WriterDomain.Traversal)
            {
                return;
            }

            if (state.Owner == WriterDomain.PostTraversal || state.Owner == WriterDomain.Combat ||
                state.Owner == WriterDomain.Recovery || state.Owner == WriterDomain.Teleport ||
                state.Owner == WriterDomain.Medical)
            {
                state.Generation = Math.Max(1, state.Generation + 1);
                state.Owner = WriterDomain.Follow;
                state.ChangedAt = Time.time;
                state.LeaseUntil = Time.time + FollowLeaseSeconds;
                state.Reason = "softLeaseExpired";
            }
        }

        private static void RecordBlocked(
            BotOwner bot,
            State state,
            string writeKind,
            WriterDomain writer,
            string reason)
        {
            if (bot == null || state == null)
            {
                return;
            }

            string key = (writeKind ?? "movement") + ":" + writer + ":" + reason;
            bool changed = !string.Equals(state.LastBlockedKey, key, StringComparison.Ordinal);
            if (!changed && Time.time < state.NextBlockedTelemetryAt)
            {
                return;
            }

            state.LastBlockedKey = key;
            state.NextBlockedTelemetryAt = Time.time + BlockTelemetrySeconds;
            FollowerTraversalTestRuntime.RecordMovementWriterAuthorityEvent(
                bot,
                "movementWriterRejected",
                new
                {
                    writeKind = writeKind ?? "movement",
                    writer = writer.ToString(),
                    owner = state.Owner.ToString(),
                    generation = state.Generation,
                    reason
                });
        }

        private static void RecordObserved(
            BotOwner bot,
            State state,
            string writeKind,
            WriterDomain inferred)
        {
            if (bot == null || state == null || inferred == WriterDomain.Unknown)
            {
                return;
            }

            string key = (writeKind ?? "movement") + ":" + inferred + ":" + state.Owner;
            bool changed = !string.Equals(state.LastObservedKey, key, StringComparison.Ordinal);
            if (!changed && Time.time < state.NextObservedTelemetryAt)
            {
                return;
            }

            state.LastObservedKey = key;
            state.NextObservedTelemetryAt = Time.time + ObserveTelemetrySeconds;
            FollowerTraversalTestRuntime.RecordMovementWriterAuthorityEvent(
                bot,
                "movementWriterObservedUnscoped",
                new
                {
                    writeKind = writeKind ?? "movement",
                    inferredWriter = inferred.ToString(),
                    owner = state.Owner.ToString(),
                    generation = state.Generation,
                    hardRejected = false,
                    reason = "stackClassificationDiagnosticOnly"
                });
        }

        private static bool TryGetCurrent(BotOwner bot, out State state)
        {
            state = null;
            return bot != null && !string.IsNullOrEmpty(bot.ProfileId) && States.TryGetValue(bot.ProfileId, out state);
        }

        private static State GetOrCreate(string profileId)
        {
            if (!States.TryGetValue(profileId, out State state))
            {
                state = new State();
                States[profileId] = state;
            }
            return state;
        }
    }
}
