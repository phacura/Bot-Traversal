using Motion = BotTraversal.Core.FollowerCapabilityMotionPolicy;
namespace BotTraversal.Core
{
    // Resource effects are selected explicitly, independently of semantic completion.
    internal static class FollowerCapabilityMotionTransactionPolicy
    {
        internal enum Action { Reject, ReleaseToManual, RetireAndBlock, InstallNative, MaintainNative }
        internal readonly struct Snapshot
        {
            internal readonly Motion.Delivery Delivery;
            internal readonly bool Keep, OwnsResource, ExactOrigin, HasNativeEndpoint;
            internal Snapshot(Motion.Delivery delivery,bool keep,bool owns,bool exact,bool endpoint)
            {Delivery=delivery;Keep=keep;OwnsResource=owns;ExactOrigin=exact;HasNativeEndpoint=endpoint;}
        }
        internal readonly struct Plan
        {
            internal readonly Action Kind;
            internal readonly bool Retire;
            internal bool Maintain=>Kind==Action.MaintainNative;
            internal bool Install=>Kind==Action.InstallNative;
            internal Plan(Action kind,bool retire=false){Kind=kind;Retire=retire;}
        }
        // "Adapter handled the call" is not ownership of a productive recovery frame.
        // A retained blocked resource gets only the existing move-retry grace window.
        internal static bool RecoveryOwnsFrame(bool handled,CapabilityMotionCommit receipt,bool existingRetryWindowActive)=>
            handled && (receipt.Accepted || (receipt.Kind==CapabilityMotionCommitKind.Blocked &&
                receipt.NativeMoverOwned && existingRetryWindowActive));
        internal static Plan Evaluate(Snapshot s)
        {
            if(!s.ExactOrigin)return new Plan(Action.Reject);
            if(s.Delivery==Motion.Delivery.PreserveActuator)return new Plan(Action.ReleaseToManual,s.OwnsResource);
            if(s.Delivery==Motion.Delivery.Blocked&&!s.Keep)return new Plan(Action.RetireAndBlock,s.OwnsResource);
            if(s.Keep)return s.OwnsResource&&s.HasNativeEndpoint
                ?new Plan(Action.MaintainNative):new Plan(Action.Reject);
            if(s.Delivery==Motion.Delivery.PublishGroundWay||s.Delivery==Motion.Delivery.PublishPhysicalWay)
                return new Plan(Action.InstallNative,true);
            return new Plan(Action.Reject);
        }
    }
}
