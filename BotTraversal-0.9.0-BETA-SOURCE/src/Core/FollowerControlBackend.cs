using System;
using EFT;
using UnityEngine;

namespace BotTraversal.Core
{
    // Sole BT engine-control dispatch surface. Producers retain decisions in
    // round 1, but cannot issue an unlisted native call outside this backend.
    internal static partial class FollowerControlBackend
    {
        [ThreadStatic]
        private static PhaseState currentPhase;
        private sealed class PhaseState
        {
            internal ControlUpdatePhase Phase;
            internal PhaseState Previous;
            internal bool Retired;
        }

        internal readonly struct PhaseScope : IDisposable
        {
            private readonly PhaseState state;
            private PhaseScope(PhaseState value)
            {
                state = value;
            }

            public void Dispose()
            {
                if (state == null)
                    return;
                state.Retired = true;
                if (ReferenceEquals(currentPhase, state))
                {
                    currentPhase = state.Previous;
                    while (currentPhase != null && currentPhase.Retired)
                        currentPhase = currentPhase.Previous;
                }
            }

            internal static PhaseScope Create(ControlUpdatePhase phase)
            {
                var state = new PhaseState
                {
                    Phase = phase,
                    Previous = currentPhase
                };
                currentPhase = state;
                return new PhaseScope(state);
            }
        }

        internal static PhaseScope EnterPhase(ControlUpdatePhase phase) => PhaseScope.Create(phase);
        internal static ControlUpdatePhase CurrentPhase => currentPhase?.Phase ?? ControlUpdatePhase.ExternalCallback;

        private static bool TargetBelongsTo(BotOwner bot, object target)
        {
            if (bot == null || target == null)
                return false;
            Player player = bot.GetPlayer ?? bot.AIData?.Player;
            if (ReferenceEquals(target, bot) || ReferenceEquals(target, bot.Mover) || ReferenceEquals(target, bot.Steering) || ReferenceEquals(target, bot.GoToSomePointData) || ReferenceEquals(target, bot.PatrollingData) || ReferenceEquals(target, bot.Mover?.LocalAvoidance) || ReferenceEquals(target, player) || ReferenceEquals(target, player?.MovementContext) || ReferenceEquals(target, player?.CharacterController) || ReferenceEquals(target, player?.VaultingComponent))
                return true;
            if (target is Component component && player != null)
                return component.transform == player.gameObject.transform || component.transform.IsChildOf(player.gameObject.transform);
            return false;
        }

        private static bool Authorize(BotOwner supplied, object target, string siteId, string operation, string nativeWriteKind)
        {
            ControlSite? found = FindSite(siteId);
            if (found == null || !string.Equals(found.Value.Operation, operation, StringComparison.Ordinal))
                return false;
            ControlSite site = found.Value;
            if (site.Mode == ControlSiteMode.Infrastructure)
                return FollowerControlEntryPolicy.Decide(true, true, site.Mode, false, false, false, false) == ControlAdmission.Infrastructure;
            BotOwner actual = target as BotOwner ?? (target as Player)?.AIData?.BotOwner;
            if (actual == null && target is BotMover mover)
                actual = FollowerMovementWriterAuthorityPatch.ResolveBotOwner(mover);
            BotOwner bot = actual ?? supplied;
            bool targetBound = bot != null && (supplied == null || ReferenceEquals(supplied, bot)) && TargetBelongsTo(bot, target);
            bool hasScope = FollowerMovementWriterAuthority.HasExplicitControlScope;
            bool exact = targetBound && FollowerMovementWriterAuthority.CanCommitScopedPhysicalMotion(bot, out _);
            // Legacy callsites are individually pinned in the reviewed catalog.
            // This preserves the EXISTING compatibility decision; no token is
            // minted and no newer generation is substituted for an old origin.
            bool compatibility = targetBound && (!hasScope || exact) && FollowerMovementWriterAuthority.CanWrite(bot, nativeWriteKind, out _);
            ControlAdmission admission = FollowerControlEntryPolicy.Decide(true, true, site.Mode, targetBound, hasScope, exact && compatibility, compatibility);
            if (admission == ControlAdmission.Reject)
                FollowerTraversalTestRuntime.RecordControlBackendDecision(bot, siteId, operation, CurrentPhase.ToString(), admission.ToString());
            return admission != ControlAdmission.Reject;
        }
    }

    internal static partial class FollowerMovementWriterAuthority
    {
        // Read the existing scope only, including invalid/retired scopes. It is
        // never permissible to reinterpret one as a compatibility request.
        internal static bool HasExplicitControlScope => CurrentScope != null;
    }
}
