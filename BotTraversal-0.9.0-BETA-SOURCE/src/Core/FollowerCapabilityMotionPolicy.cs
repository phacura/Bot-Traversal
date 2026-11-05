using Ground = BotTraversal.Core.FollowerCapabilityDeliveryPolicy;
namespace BotTraversal.Core
{
    internal enum CapabilityMotionPurpose { NativeProbe, TypedRoute, PreActuator, FollowIngress, Recovery, NativeExecution }
    internal enum CapabilityMotionCommitKind { Rejected, ManualFrameAccepted, NativeWayAccepted, Blocked }
    internal readonly struct CapabilityMotionCommit
    {
        internal readonly CapabilityMotionCommitKind Kind;
        internal readonly bool NativeMoverOwned;
        internal bool Accepted => Kind==CapabilityMotionCommitKind.ManualFrameAccepted || Kind==CapabilityMotionCommitKind.NativeWayAccepted;
        internal bool UsesExclusiveManualInput => Kind==CapabilityMotionCommitKind.ManualFrameAccepted;
        internal CapabilityMotionCommit(CapabilityMotionCommitKind kind,bool nativeMoverOwned=false){Kind=kind;NativeMoverOwned=nativeMoverOwned;}
        internal static CapabilityMotionCommit Rejected => new CapabilityMotionCommit(CapabilityMotionCommitKind.Rejected);
        internal static CapabilityMotionCommit ManualFrame => new CapabilityMotionCommit(CapabilityMotionCommitKind.ManualFrameAccepted);
    }
    internal static class FollowerCapabilityMotionPolicy
    {
        internal static bool CanRetainConsumer(FollowerTraversalLifecycle.Identity observed,
            FollowerTraversalLifecycle.Identity live,bool sameNativePath,bool hasPath,bool activeProbe,bool exactNativeExecution=false) =>
            observed.Valid&&live.Valid&&observed.Equals(live)&&sameNativePath&&hasPath&&(!activeProbe||exactNativeExecution);
        internal static bool MissedDropEntry(float forward,float lateral,float bodyToEntryHeight,
            float maxForward,float maxLateral,float heightTolerance) =>
            bodyToEntryHeight < -heightTolerance ||
            (forward>maxForward&&lateral<=maxLateral&&bodyToEntryHeight<=heightTolerance);
        internal enum Delivery { PreserveActuator, PublishGroundWay, PublishPhysicalWay, KeepNativeWay, Blocked }
        internal readonly struct Snapshot
        {
            internal readonly Ground.Snapshot Ground;
            internal readonly bool ExplicitGoal, IntentMoving, DirectionCompatible, PriorStepPhysical, MissedEntry, DownOrLevel, OwnedNativeConsumer, NativeExecution;
            internal readonly float Distance, MaximumDistance;
            internal Snapshot(Ground.Snapshot ground,bool explicitGoal,bool moving,bool aligned,bool prior,bool missed,bool down,float distance,float maximum,bool ownedNativeConsumer=false,bool nativeExecution=false)
            {Ground=ground;ExplicitGoal=explicitGoal;IntentMoving=moving;DirectionCompatible=aligned;PriorStepPhysical=prior;MissedEntry=missed;DownOrLevel=down;Distance=distance;MaximumDistance=maximum;OwnedNativeConsumer=ownedNativeConsumer;NativeExecution=nativeExecution;}
        }
        internal readonly struct Plan
        {
            internal readonly Delivery Kind;internal readonly string Reason;internal readonly bool KeepExistingConsumer;
            internal Plan(Delivery kind,string reason,bool keep=false){Kind=kind;Reason=reason;KeepExistingConsumer=keep;}
        }
        internal static Plan Evaluate(Snapshot s)
        {
            var g=s.Ground;
            bool nativeExecution=s.NativeExecution&&g.ActiveProbe&&g.Kind==Ground.Capability.DropDown;
            if((g.ActiveProbe&&!nativeExecution) || !s.ExplicitGoal || !s.IntentMoving) return new Plan(Delivery.PreserveActuator,"existingProbeOrNoExplicitMovingGoal");
            bool passage=g.Kind==Ground.Capability.WalkThrough;
            bool missedDrop=g.Kind==Ground.Capability.DropDown && s.PriorStepPhysical && s.MissedEntry;
            if(!passage&&!missedDrop&&!nativeExecution) return new Plan(Delivery.PreserveActuator,"nativeActuatorEntryRemainsAuthoritative");
            if(!g.ExactOrigin||!s.DirectionCompatible||float.IsNaN(s.Distance)||float.IsInfinity(s.Distance)||s.Distance<0f||float.IsNaN(s.MaximumDistance)||float.IsInfinity(s.MaximumDistance)||s.MaximumDistance<=0f||s.Distance>s.MaximumDistance)
                return new Plan(Delivery.Blocked,"invalidOriginOrIntentEnvelope");
            if(!g.CanWalk) return new Plan(Delivery.Blocked,"nativeMovementContextCannotWalk");
            if(float.IsNaN(g.NoProgressSeconds)||float.IsInfinity(g.NoProgressSeconds)||g.NoProgressSeconds<0f||
               float.IsNaN(g.NoProgressBudget)||float.IsInfinity(g.NoProgressBudget)||g.NoProgressBudget<=0f)
                return new Plan(Delivery.Blocked,"invalidExistingProgressWindow");
            if(g.MatchingNativePath) return g.NoProgressSeconds>=g.NoProgressBudget
                ?new Plan(Delivery.Blocked,"nativeConsumerStalledKeepDebt",true)
                :new Plan(Delivery.KeepNativeWay,"nativeConsumerOwnsMotionDoNotRetireOrPause",true);
            if(nativeExecution) return !g.RetryReady
                ?new Plan(Delivery.Blocked,"existingNativeExecutionRetryBudget",s.OwnedNativeConsumer)
                :new Plan(Delivery.PublishPhysicalWay,"admittedNativeDropDownNeedsExecutableConsumer");
            var ground=Ground.Evaluate(g);
            if(ground.Kind==Ground.Delivery.PublishNativeGroundPath)return new Plan(Delivery.PublishGroundWay,ground.Reason);
            if(ground.Kind==Ground.Delivery.Blocked)return new Plan(Delivery.Blocked,ground.Reason,s.OwnedNativeConsumer&&ground.Reason=="existingMoveRetryBudget");
            if(g.NoProgressSeconds<1f&&!s.OwnedNativeConsumer)return new Plan(Delivery.PreserveActuator,"awaitExistingMeasuredProgressWindow");
            if(!g.RetryReady)return new Plan(Delivery.Blocked,"existingMoveRetryBudget",s.OwnedNativeConsumer);
            if(g.Grounded&&g.StrictGroundRoute)return new Plan(Delivery.PublishGroundWay,"updatedExistingGroundConsumerGoal");
            if(!s.DownOrLevel)return new Plan(Delivery.PreserveActuator,"unsupportedUpwardIntentRequiresActuator");
            return new Plan(Delivery.PublishPhysicalWay,"existingPhysicalIntentNeedsNativeMoverConsumer");
        }
    }
}
