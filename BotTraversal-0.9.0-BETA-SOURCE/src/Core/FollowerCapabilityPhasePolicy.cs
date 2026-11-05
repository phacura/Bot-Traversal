namespace BotTraversal.Core
{
    // Phase choices consume observations; native calls, timers and receipts stay in Host.
    internal static class FollowerCapabilityPhasePolicy
    {
        internal enum NativeEntry { RejectConsumed, AcquireEntry, Continue }
        internal static NativeEntry DropDownEntry(bool consumed,bool sharedEntry,bool acquired) =>
            consumed?NativeEntry.RejectConsumed:(sharedEntry&&!acquired?NativeEntry.AcquireEntry:NativeEntry.Continue);
        internal static bool DirectionValid(bool entryFinite,bool exitFinite,float directionSquared) =>
            entryFinite&&exitFinite&&!(directionSquared<0.5f);
        internal static bool LandingStateReleased(int actual,int landing) => actual!=landing;
        internal static bool SoftLandingRepair(float now,float next,int attempts,int maximum,float elapsed,float hardBudget) =>
            now>=next&&attempts<maximum&&elapsed<hardBudget;
        internal static bool HardLandingRepair(float elapsed,float hardBudget,int attempts,int maximum,bool attempted) =>
            (elapsed>=hardBudget||attempts>=maximum)&&!attempted;
        internal static bool TypedDropAfterScannerRefusal(bool enabled,bool attempted,string kind) =>
            enabled&&!attempted&&string.Equals(kind,"DropDown",System.StringComparison.OrdinalIgnoreCase);
        internal static bool FarSideRecoveryAccepted(bool farSide,bool navSampled,bool offMeshProof) =>
            farSide&&(navSampled||offMeshProof);
        internal static bool NativeRecoveryConsumed(bool injected,bool consumed) => injected||consumed;
        internal static bool NativeRecoveryObserved(bool stateObserved,bool traversalStarted,bool vaultObserved) =>
            stateObserved||traversalStarted||vaultObserved;
        internal static bool JumpLandingRecovery(bool rejected,string kind) =>
            rejected&&string.Equals(kind,"Jump",System.StringComparison.OrdinalIgnoreCase);
    }
}
