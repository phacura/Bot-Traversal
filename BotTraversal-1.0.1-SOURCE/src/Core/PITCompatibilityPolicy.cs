using System;
namespace BotTraversal.Core
{
    internal static class PITCompatibilityPolicy
    {
        internal static bool SupportedStandalone(string version)=>version=="0.9.1"||version=="1.1.1";
        internal static bool ReviewedFollowBuild(Guid mvid)=>mvid==new Guid("e77f8971-589a-4c6d-bee9-07a778a2b081")||
            mvid==new Guid("084ae5d2-4ba7-4f08-ad73-d22b300816e8");
        internal static int CommandClaim(bool readSucceeded,bool present)=>!readSucceeded||present?(int)FollowerCommandType.HoldPosition:0;
    }
}
