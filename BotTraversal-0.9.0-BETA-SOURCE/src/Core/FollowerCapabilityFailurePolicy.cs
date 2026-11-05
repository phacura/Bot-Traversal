using System;
namespace BotTraversal.Core
{
    internal enum CapabilityFailureAction { Continue, Terminalize, Quarantine, PreEntryRetry, PreActuatorRetry, NativeJumpRetry, AssistedReplay, TerminalRecovery }
    internal static class FollowerCapabilityFailurePolicy
    {
        internal static bool Intercept(bool current,bool validated,bool teleportAttempted)=>current&&validated&&!teleportAttempted;
        internal static CapabilityFailureAction Initial(bool singleShot,bool teleportAttempted,bool walkThrough,bool directDriveEnabled,bool directDriveExhausted)=>
            singleShot&&teleportAttempted?CapabilityFailureAction.Terminalize:
            walkThrough&&directDriveEnabled&&directDriveExhausted?CapabilityFailureAction.Quarantine:CapabilityFailureAction.Continue;
        internal static CapabilityFailureAction BeforeExecution(bool isolate,string kind,bool entryRejected,bool dropAttempted,float epoch,bool jumpAttempted,string source)
        {
            source=source??string.Empty;
            if(isolate&&string.Equals(kind,"DropDown",StringComparison.OrdinalIgnoreCase)&&entryRejected&&!dropAttempted&&epoch<0f&&
                (source.IndexOf("learningV32EntryAcquisitionFailed",StringComparison.OrdinalIgnoreCase)>=0||source.IndexOf("dropDownEntrySevereMismatch",StringComparison.OrdinalIgnoreCase)>=0))return CapabilityFailureAction.PreEntryRetry;
            if(isolate&&string.Equals(kind,"Jump",StringComparison.OrdinalIgnoreCase)&&!jumpAttempted&&
                (source.IndexOf("learnedJumpTakeoffReadyTimeout",StringComparison.OrdinalIgnoreCase)>=0||source.IndexOf("learnedJumpTakeoffDirectionLost",StringComparison.OrdinalIgnoreCase)>=0))return CapabilityFailureAction.PreActuatorRetry;
            return CapabilityFailureAction.Continue;
        }
        internal static CapabilityFailureAction AfterExecution(bool singleTrajectory,string kind,bool jumpAttempted,bool assistedAttempted)=>
            singleTrajectory&&string.Equals(kind,"Jump",StringComparison.OrdinalIgnoreCase)&&jumpAttempted&&!assistedAttempted?CapabilityFailureAction.NativeJumpRetry:
            !string.Equals(kind,"WalkThrough",StringComparison.OrdinalIgnoreCase)&&!assistedAttempted?CapabilityFailureAction.AssistedReplay:CapabilityFailureAction.TerminalRecovery;
        internal static bool CanContinueNativeJump(bool qualifiedWitness,bool alreadyRetried)=>qualifiedWitness&&!alreadyRetried;
    }
}
