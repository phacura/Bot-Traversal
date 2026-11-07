using System;
namespace BotTraversal.Core
{
    // One transaction per unresolved semantic step. Engine failures advance exactly once;
    // unavailable learned routes are explicit failed selections, never silent rejection loops.
    internal static class FollowerCapabilityFailureSequence
    {
        internal enum Stage { Native, Alternate, SelfRescue, ReturnToEntry, RetryAlternate, RecoveryTeleport }
        internal static Stage AfterFailure(Stage s) => s>=Stage.Native&&s<Stage.RecoveryTeleport?(Stage)((int)s+1):Stage.RecoveryTeleport;
        // A selected physical attempt owns allocation until its own failure or semantic completion.
        internal static bool OwnsStrategy(Stage s,bool current) => current &&
            (s==Stage.Alternate||s==Stage.SelfRescue||s==Stage.RetryAlternate);
        internal static bool CanAllocateStrategy(bool current,bool pending,bool ownedRequest,bool exactCandidate)
            => current&&pending&&ownedRequest&&exactCandidate;
        internal static bool ContinueExecutedJump(bool current,bool consumed,bool attempted,bool nativeStarted,bool partial,bool rejected)
            => current&&consumed&&attempted&&(nativeStarted||partial||rejected);
        internal static bool IsCancellation(string reason)
        {
            string r=(reason??string.Empty).ToLowerInvariant();
            return r.Contains("manual")||r.Contains("consoleabort")||r.Contains("raidover");
        }
        internal static bool CanClosePassageBoundary(bool current,bool passageProof,bool finitePass,bool supported,bool released,bool recordedAdjacentActuator,bool clean)
            => current&&passageProof&&finitePass&&supported&&released&&recordedAdjacentActuator&&clean;
        internal static bool PastObstacleEntry(bool obstacle,float forward,float maximum)
            => obstacle&&(float.IsNaN(forward)||float.IsInfinity(forward)||forward>maximum);
        internal static float EntryBudget(float distance,float baseline,float speed,float maximum)
        {
            if(float.IsNaN(distance)||float.IsInfinity(distance)||distance<0)return baseline;
            return Math.Max(baseline,Math.Min(maximum,baseline+distance/Math.Max(.5f,speed)));
        }
        internal static bool EntryRequiresPointAim(bool retryStaging,bool outsideLane)=>retryStaging||outsideLane;
        internal static bool WalkingOnly(Stage s)=>s==Stage.ReturnToEntry||s==Stage.RetryAlternate;
        internal static double Rank(int successes,int failures,double evaluation,double distance)
        {
            if(double.IsNaN(evaluation)||double.IsInfinity(evaluation))evaluation=.5;
            if(double.IsNaN(distance)||double.IsInfinity(distance)||distance<0)return double.NegativeInfinity;
            double rate=(Math.Max(0,successes)+1.0)/(Math.Max(0,successes)+Math.Max(0,failures)+2.0);
            return 4*rate+2*Math.Max(0,Math.Min(1,evaluation))-.05*distance;
        }
        internal static bool ForwardCandidate(double entryFromBody,double exitFromBody,double prefixFloor,double entryArc)
            => !double.IsNaN(entryFromBody)&&!double.IsNaN(exitFromBody)&&!double.IsNaN(prefixFloor)&&
                !double.IsNaN(entryArc)&&!double.IsInfinity(entryFromBody)&&!double.IsInfinity(exitFromBody)&&
                !double.IsInfinity(prefixFloor)&&!double.IsInfinity(entryArc)&&
                entryFromBody>=-.14&&exitFromBody>.20&&entryArc>=prefixFloor-.14;
    }
}
