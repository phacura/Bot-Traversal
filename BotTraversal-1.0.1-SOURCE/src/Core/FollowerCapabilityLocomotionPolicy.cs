using BotTraversal.Core;
namespace BotTraversal.Core
{
    // A request to the existing native Animator is not body progress or traversal success.
    internal static class FollowerCapabilityLocomotionPolicy
    {
        internal enum Action { Reject, Preserve, AwaitNativeCallback, RequestNativeRun }
        internal readonly struct Snapshot
        {
            internal readonly FollowerTraversalLifecycle.Identity Origin, Live;
            internal readonly bool QualifiedBoundary, Grounded, AllowedState, OverrideActive, Teleport, SurvivalClaim;
            internal readonly bool AnimatorAvailable, NativeLocomotionCoherent, NativeLocomotionPending;
            internal Snapshot(FollowerTraversalLifecycle.Identity origin,FollowerTraversalLifecycle.Identity live,
                bool qualified,bool grounded,bool allowed,bool overridden,bool teleport,bool survival,
                bool animator,bool coherent,bool pending)
            {Origin=origin;Live=live;QualifiedBoundary=qualified;Grounded=grounded;AllowedState=allowed;
             OverrideActive=overridden;Teleport=teleport;SurvivalClaim=survival;AnimatorAvailable=animator;
             NativeLocomotionCoherent=coherent;NativeLocomotionPending=pending;}
        }
        internal static Action Evaluate(Snapshot s)
        {
            if(!s.Origin.Valid || !s.Live.Valid || s.Origin.Writer<=0 || s.Live.Writer<=0 || !s.Origin.Equals(s.Live) || !s.QualifiedBoundary ||
                !s.Grounded || !s.AllowedState || s.OverrideActive || s.Teleport || s.SurvivalClaim || !s.AnimatorAvailable)
                return Action.Reject;
            if(s.NativeLocomotionCoherent)return Action.Preserve;
            if(s.NativeLocomotionPending)return Action.AwaitNativeCallback;
            return Action.RequestNativeRun;
        }
    }
    internal interface ICapabilityLocomotionPort
    {
        FollowerCapabilityLocomotionPolicy.Snapshot Capture();
        bool IsCurrent(FollowerTraversalLifecycle.Identity origin);
    }
    internal static class FollowerCapabilityLocomotionDriver
    {
        internal enum Receipt { Rejected, Preserved, AwaitingCallback, AnimationRequested, NativeInputRequested, AwaitingInput, NativeStopInterrupted }
        internal static Receipt Commit(ICapabilityLocomotionPort port)
        {
            if(port==null)return Receipt.Rejected;
            var s=port.Capture();var plan=FollowerCapabilityLocomotionPolicy.Evaluate(s);
            if(plan==FollowerCapabilityLocomotionPolicy.Action.Reject || !port.IsCurrent(s.Origin))return Receipt.Rejected;
            // Input is consumed by native updates, not by rendered-frame receipts.
            // Transition/landing wait for EFT; no Play/managed state substitution.
            if(!(port is ICapabilityNativeInputPort nativeInput))return Receipt.Rejected;
            var input=FollowerCapabilityNativeInputDriver.Commit(nativeInput);
            if(input==FollowerCapabilityNativeInputDriver.Receipt.Rejected)return Receipt.Rejected;
            if(input==FollowerCapabilityNativeInputDriver.Receipt.Unavailable)return Receipt.AwaitingCallback;
            if(input==FollowerCapabilityNativeInputDriver.Receipt.AwaitingInput)return Receipt.AwaitingInput;
            return plan==FollowerCapabilityLocomotionPolicy.Action.Preserve?Receipt.Preserved:Receipt.NativeInputRequested;
        }
    }
}
