using System;

namespace BotTraversal.Core
{
    // Shared value-only delivery contract. This is a prerequisite for the full
    // capability migration, not a claim that the historical strategies are pure.
    internal static class FollowerCapabilityDeliveryPolicy
    {
        internal enum Capability { WalkThrough, Jump, DropDown, Climb, Vault, Follow, Recovery, Unsupported }
        internal enum Delivery { PreserveExisting, PublishNativeGroundPath, KeepNativeGroundPath, Blocked }
        internal readonly struct Snapshot
        {
            internal readonly Capability Kind;
            internal readonly bool ExactOrigin, ActiveProbe, Grounded, CanWalk, StrictGroundRoute;
            internal readonly bool RequiresActuator, NativeEntryAdmissible, MatchingNativePath, RetryReady;
            internal readonly float NoProgressSeconds, NoProgressBudget;
            internal Snapshot(Capability kind,bool exact,bool active,bool grounded,bool canWalk,bool strict,
                bool actuator,bool admissible,bool matching,bool retry,float noProgress,float noProgressBudget=3f)
            { Kind=kind;ExactOrigin=exact;ActiveProbe=active;Grounded=grounded;CanWalk=canWalk;
              StrictGroundRoute=strict;RequiresActuator=actuator;NativeEntryAdmissible=admissible;
              MatchingNativePath=matching;RetryReady=retry;NoProgressSeconds=noProgress;NoProgressBudget=noProgressBudget; }
        }
        internal readonly struct Plan
        {
            internal readonly Delivery Kind;
            internal readonly string Reason;
            internal Plan(Delivery kind,string reason) { Kind=kind;Reason=reason; }
            // Delivery is never proof of body progress, a Native Attempt, or Step completion.
        }
        internal static Capability Parse(string kind)
        {
            if (string.Equals(kind,"PhysicalCorridor",StringComparison.OrdinalIgnoreCase)) return Capability.WalkThrough;
            Capability parsed;
            return Enum.TryParse(kind,true,out parsed) && Enum.IsDefined(typeof(Capability),parsed)
                ? parsed : Capability.Unsupported;
        }
        internal static Plan Evaluate(Snapshot s)
        {
            if (!s.ExactOrigin || s.Kind==Capability.Unsupported) return new Plan(Delivery.Blocked,"invalidOriginOrCapability");
            if (s.ActiveProbe) return new Plan(Delivery.PreserveExisting,"activeNativeProbeOwnsBody");
            if (!s.CanWalk) return new Plan(Delivery.Blocked,"nativeMovementContextCannotWalk");
            // Never bypass Jump/Climb/Vault or an admissible DropDown entry.
            bool ordinaryGround=s.Kind==Capability.WalkThrough || s.Kind==Capability.Follow || s.Kind==Capability.Recovery;
            bool overflownDrop=s.Kind==Capability.DropDown && !s.NativeEntryAdmissible;
            if ((!ordinaryGround && !overflownDrop) || (s.RequiresActuator && !overflownDrop)) return new Plan(Delivery.PreserveExisting,"actuatorMustOwnCrossing");
            if (!s.Grounded || !s.StrictGroundRoute) return new Plan(Delivery.PreserveExisting,"groundRouteNotProven");
            if (s.NoProgressBudget<=0f || float.IsNaN(s.NoProgressBudget) || float.IsInfinity(s.NoProgressBudget))
                return new Plan(Delivery.Blocked,"invalidExistingProgressBudget");
            if (float.IsNaN(s.NoProgressSeconds) || float.IsInfinity(s.NoProgressSeconds) || s.NoProgressSeconds<0f)
                return new Plan(Delivery.Blocked,"invalidMeasuredProgressWindow");
            if (s.MatchingNativePath && s.NoProgressSeconds>=s.NoProgressBudget)
                return new Plan(Delivery.Blocked,"nativePathAlsoStalledKeepDebtForExistingRecovery");
            if (s.MatchingNativePath) return new Plan(Delivery.KeepNativeGroundPath,"nativePathOwnsDeliveryDoNotRetireEachFrame");
            if (float.IsNaN(s.NoProgressSeconds) || float.IsInfinity(s.NoProgressSeconds) || s.NoProgressSeconds<1f)
                return new Plan(Delivery.PreserveExisting,"awaitExistingMeasuredProgressWindow");
            if (!s.RetryReady) return new Plan(Delivery.Blocked,"existingMoveRetryBudget");
            return new Plan(Delivery.PublishNativeGroundPath,"issuedInputWithoutBodyProgressRequiresDifferentDelivery");
        }
    }
}
