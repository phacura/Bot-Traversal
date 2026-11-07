using System;

namespace BotTraversal.Core
{
    internal enum NativeRecoveryObservation { AwaitingNativeState, RejectedJumpLanding, ObservedContinuation }
    internal enum NativeRecoveryTerminal { FailTransient, Teleport }

    internal readonly struct UnconsumedRecoveryPlan
    {
        internal readonly bool TryAssist;
        internal readonly NativeRecoveryTerminal Terminal;
        internal UnconsumedRecoveryPlan(bool tryAssist, NativeRecoveryTerminal terminal)
        { TryAssist=tryAssist; Terminal=terminal; }
    }

    // Value-only choices for the existing native recovery host. These choices
    // neither authorize another actuator nor establish physical completion.
    internal static class FollowerCapabilityRecoveryPolicy
    {
        internal static NativeRecoveryObservation Observed(bool nativeObserved,bool rejectedJumpLanding)=>
            !nativeObserved?NativeRecoveryObservation.AwaitingNativeState:
            rejectedJumpLanding?NativeRecoveryObservation.RejectedJumpLanding:NativeRecoveryObservation.ObservedContinuation;

        internal static bool PreserveJumpTrajectory(bool singleTrajectory,bool jumpAttempted)=>
            singleTrajectory&&jumpAttempted;

        internal static bool CanCompletePartial(string kind)=>
            string.Equals(kind,"Vault",StringComparison.OrdinalIgnoreCase)||string.Equals(kind,"Climb",StringComparison.OrdinalIgnoreCase);

        internal static bool CanOfferAssist(bool assistedAttempted)=>!assistedAttempted;

        internal static UnconsumedRecoveryPlan Unconsumed(bool assistedAttempted,bool zeroShot)=>
            new UnconsumedRecoveryPlan(!assistedAttempted,zeroShot?NativeRecoveryTerminal.FailTransient:NativeRecoveryTerminal.Teleport);
    }
}
