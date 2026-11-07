namespace BotTraversal.Core
{
    internal static class FollowerCapabilityProbeFramePolicy
    {
        internal enum Combat { Continue, AbortWalkThrough, AbortBeforeNative, DeferUntilLanding }
        internal static Combat CombatAction(bool walkThroughStage,bool exclusive,bool alreadyDeferred)=>
            walkThroughStage?Combat.AbortWalkThrough:!exclusive?Combat.AbortBeforeNative:
            alreadyDeferred?Combat.Continue:Combat.DeferUntilLanding;
        internal static bool StartPendingWalkThrough(bool enabled,bool pending,bool aligning,string kind)=>
            enabled&&pending&&aligning&&string.Equals(kind,"WalkThrough",System.StringComparison.OrdinalIgnoreCase);
    }
    internal interface ICapabilityProbeFramePort
    {
        bool IsCurrent {get;}
        void RejectLifecycle();
        void CaptureActors();
        void ObserveMeaningfulProgress();
        void ObservePhysicalPassage();
        bool TryImmediateThreat(out string reason);
        void InterruptForThreat(string reason);
        void IsolateMovement();
        bool LeaseExpired {get;}
        void ExpireLease();
        bool TryCombatClaim(out string reason);
        FollowerCapabilityProbeFramePolicy.Combat CombatAction {get;}
        void AbortWalkThroughForCombat();
        void AbortBeforeNativeForCombat();
        void DeferCombat(string reason);
        bool PendingWalkThrough {get;}
        void StartPendingWalkThrough();
        void DispatchStage();
    }
    internal static class FollowerCapabilityProbeFrameDriver
    {
        internal enum Receipt { Rejected, Interrupted, Expired, CombatAborted, PendingStarted, StageDispatched }
        private static bool Live(ICapabilityProbeFramePort port)
        {
            if(port.IsCurrent)return true;
            port.RejectLifecycle();return false;
        }
        internal static Receipt Commit(ICapabilityProbeFramePort port)
        {
            if(port==null)return Receipt.Rejected;
            if(!Live(port))return Receipt.Rejected;
            port.CaptureActors();
            if(!Live(port))return Receipt.Rejected;
            port.ObserveMeaningfulProgress();
            if(!Live(port))return Receipt.Rejected;
            port.ObservePhysicalPassage();
            if(!Live(port))return Receipt.Rejected;
            if(port.TryImmediateThreat(out string threat))
            {
                if(!Live(port))return Receipt.Rejected;
                port.InterruptForThreat(threat);return Receipt.Interrupted;
            }
            if(!Live(port))return Receipt.Rejected;
            port.IsolateMovement();
            if(!Live(port))return Receipt.Rejected;
            if(port.LeaseExpired)
            {
                if(!Live(port))return Receipt.Rejected;
                port.ExpireLease();return Receipt.Expired;
            }
            if(port.TryCombatClaim(out string combat))
            {
                if(!Live(port))return Receipt.Rejected;
                var combatAction=port.CombatAction;
                if(!Live(port))return Receipt.Rejected;
                switch(combatAction)
                {
                    case FollowerCapabilityProbeFramePolicy.Combat.AbortWalkThrough:
                        port.AbortWalkThroughForCombat();return Receipt.CombatAborted;
                    case FollowerCapabilityProbeFramePolicy.Combat.AbortBeforeNative:
                        port.AbortBeforeNativeForCombat();return Receipt.CombatAborted;
                    case FollowerCapabilityProbeFramePolicy.Combat.DeferUntilLanding:
                        port.DeferCombat(combat);break;
                }
            }
            if(port.PendingWalkThrough)
            {
                if(!Live(port))return Receipt.Rejected;
                port.StartPendingWalkThrough();return Receipt.PendingStarted;
            }
            if(!Live(port))return Receipt.Rejected;
            port.DispatchStage();return Receipt.StageDispatched;
        }
    }
}
