namespace BotTraversal.Core
{
    internal enum CapabilityContinuationPhase { CanonicalRoute, NativeLanding }
    internal enum CapabilityContinuationFailure
    {
        None, MissingOrigin, ReplacedOrigin, StaleStep, ForeignActor, WrongOwner,
        CursorMismatch, NativeOwnsMotion, MissingNativeProbe, Preempted, ScopeRejected, ProducerDeclined
    }
    internal static class FollowerCapabilityRouteContinuationPolicy
    {
        internal readonly struct Snapshot
        {
            internal readonly FollowerTraversalLifecycle.Identity Origin,Live;
            internal readonly CapabilityContinuationPhase Phase;
            internal readonly bool CurrentStep,CurrentActor,TraversalOwner,CursorCurrent,NativeActive,Preempted;
            internal Snapshot(FollowerTraversalLifecycle.Identity origin,FollowerTraversalLifecycle.Identity live,
                CapabilityContinuationPhase phase,bool currentStep,bool currentActor,bool traversalOwner,
                bool cursorCurrent,bool nativeActive,bool preempted)
            {Origin=origin;Live=live;Phase=phase;CurrentStep=currentStep;CurrentActor=currentActor;
             TraversalOwner=traversalOwner;CursorCurrent=cursorCurrent;NativeActive=nativeActive;Preempted=preempted;}
        }
        internal static CapabilityContinuationFailure Evaluate(Snapshot s)
        {
            if(!s.Origin.Valid||!s.Live.Valid||s.Origin.Writer<=0||s.Live.Writer<=0)return CapabilityContinuationFailure.MissingOrigin;
            if(!s.Origin.Equals(s.Live))return CapabilityContinuationFailure.ReplacedOrigin;
            if(!s.CurrentStep)return CapabilityContinuationFailure.StaleStep;
            if(!s.CurrentActor)return CapabilityContinuationFailure.ForeignActor;
            if(!s.TraversalOwner)return CapabilityContinuationFailure.WrongOwner;
            if(s.Phase!=CapabilityContinuationPhase.CanonicalRoute&&s.Phase!=CapabilityContinuationPhase.NativeLanding)
                return CapabilityContinuationFailure.MissingOrigin;
            if(s.Phase==CapabilityContinuationPhase.CanonicalRoute)
            {
                if(!s.CursorCurrent)return CapabilityContinuationFailure.CursorMismatch;
                if(s.NativeActive)return CapabilityContinuationFailure.NativeOwnsMotion;
            }
            else if(!s.NativeActive)return CapabilityContinuationFailure.MissingNativeProbe;
            return s.Preempted?CapabilityContinuationFailure.Preempted:CapabilityContinuationFailure.None;
        }
        internal enum CanonicalReceipt { NotSelected, Published, Rejected }
        internal static CanonicalReceipt ResolveCanonical(bool selected,bool published)=>
            !selected?CanonicalReceipt.NotSelected:published?CanonicalReceipt.Published:CanonicalReceipt.Rejected;
        // Reservation means handled by an existing canonical mission, never body progress.
        internal static bool ReserveLegacyBridge(bool canonicalMissionOwnsSuccessor)=>canonicalMissionOwnsSuccessor;
    }
}
