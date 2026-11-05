namespace BotTraversal.Core
{
    internal static partial class FollowerTraversalTestRuntime
    {
        internal static void RecordControlBackendDecision(EFT.BotOwner bot, string site,
            string operation, string phase, string admission)
        {
            if (bot==null || !ShouldEmitDiagnostic("round1Control:"+site,bot,1f)) return;
            WriteEvent("round1ControlEntryRejected",bot,new
            { site, operation, phase, admission, nativeWritePerformed=false,
              bodyRelocated=false, nativeSuccessRecorded=false });
        }
    }
}
