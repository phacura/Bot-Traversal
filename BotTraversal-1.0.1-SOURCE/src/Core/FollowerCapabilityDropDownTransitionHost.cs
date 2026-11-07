using System;
using UnityEngine;
namespace BotTraversal.Core
{
    internal static partial class FollowerTraversalTestRuntime
    {
        private static DropDownConsumerPhase GetCapabilityDropDownConsumerPhase(ProbeRuntime probe)
        {
            if(probe==null||!probe.NativeDropDownAttempted)return DropDownConsumerPhase.None;
            switch(probe.Stage)
            {
                case ProbeStage.NativeDropDown:return DropDownConsumerPhase.Execute;
                case ProbeStage.Egress:
                case ProbeStage.Verify:return DropDownConsumerPhase.Validate;
                case ProbeStage.ExitCommit:return DropDownConsumerPhase.Commit;
                default:return DropDownConsumerPhase.None;
            }
        }
        private static bool IsCapabilityDropDownConsumerPhase(ProbeRuntime probe)=>
            FollowerCapabilityDropDownTransitionPolicy.OwnsBoundary(GetCapabilityDropDownConsumerPhase(probe));
        // Adjacent landing runway orchestration is owned by FollowerCapabilityChainContinuityHost.
    }
}
