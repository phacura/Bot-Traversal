using System;
namespace BotTraversal.Core
{
    // Value-only entry/re-entry geometry. The Mission owns phase, clock and cursor.
    internal static class FollowerCapabilityRouteRetryPolicy
    {
        private static bool Finite(float x)=>!float.IsNaN(x)&&!float.IsInfinity(x);
        // Explicit failure is mandatory. Geometry/probe existence alone never starts recovery.
        internal static bool FailureAdmission(bool executionFailed,bool currentOrigin,bool completionPublished,bool safe)=>
            executionFailed&&currentOrigin&&!completionPublished&&safe;
        internal static bool BorrowHeading(float angle,float maximum)=>Finite(angle)&&Finite(maximum)&&angle>=0f&&maximum>=0f&&angle<=maximum;
        internal static bool UnsafeJumpSpeed(float speed,float demonstrated)=>Finite(speed)&&Finite(demonstrated)&&demonstrated>0f&&speed>Math.Max(8f,demonstrated*1.75f+1f);
        internal static bool ReturnSegment(float planar,float vertical)=>Finite(planar)&&Finite(vertical)&&planar>=0f&&planar<=1.5f&&vertical>=0f&&vertical<=.8f;
        internal static bool ReturnLocal(float age,float deviation,float vertical)=>Finite(age)&&age>=0f&&age<2f&&Finite(deviation)&&deviation>=0f&&deviation<=.65f&&Finite(vertical)&&vertical>=0f&&vertical<=.65f;
        internal static float ReturnSpeed(float distance)=>!Finite(distance)||distance<0f?0f:Math.Min(.55f,Math.Max(.16f,distance*.40f));
        internal static bool PrecisionReturn(bool latched,float distance)=>latched||(Finite(distance)&&distance>=0f&&distance<=2.2f);
        internal enum ReturnAction { Wait,KeepPath,RequestPath }
        internal static bool HoldNeedsStop(bool sameGeneration,bool sameResource)=>!sameGeneration||!sameResource;
        internal static float ReturnBudget(float failureDirectDistance)
        {
            if(!Finite(failureDirectDistance)||failureDirectDistance<0f)return 0f;
            float budget=Math.Max(10f,failureDirectDistance*3f+4f);return Finite(budget)?budget:0f;
        }
        internal static bool PathWithinBudget(float pathLength,float frozenBudget)=>
            Finite(pathLength)&&Finite(frozenBudget)&&pathLength>=0f&&frozenBudget>0f&&pathLength<=frozenBudget;
        internal static bool ReturnDiverging(bool current,float best,float remaining,float displacement)=>current&&
            Finite(best)&&best>=0f&&Finite(remaining)&&remaining>best+.35f&&Finite(displacement)&&displacement>=.10f;
        internal static bool PathProgress(float previousRemaining,float remaining,float bodyDistance)=>
            Finite(previousRemaining)&&Finite(remaining)&&Finite(bodyDistance)&&remaining>=0f&&remaining+.05f<previousRemaining&&bodyDistance>.02f;
        internal static ReturnAction ReturnDecision(bool currentPath,float noProgressAge,bool attemptDue)=>
            currentPath&&Finite(noProgressAge)&&noProgressAge>=0f&&noProgressAge<2f
                ?ReturnAction.KeepPath:attemptDue?ReturnAction.RequestPath:ReturnAction.Wait;
        internal static bool StageReached(float forward,float lateral,float vertical,float speed,float facing,bool grounded)=>
            Finite(forward)&&forward<=-1f&&forward>=-1.50f&&Finite(lateral)&&lateral>=0f&&lateral<=.18f&&
            Finite(vertical)&&vertical>=0f&&vertical<=.30f&&Finite(speed)&&speed>=0f&&speed<=.35f&&
            Finite(facing)&&facing>=.990268f&&grounded;
        internal static bool StagePosition(float forward,float lateral,float vertical,bool grounded)=>
            Finite(forward)&&forward<=-1f&&forward>=-1.50f&&Finite(lateral)&&lateral>=0f&&lateral<=.18f&&
            Finite(vertical)&&vertical>=0f&&vertical<=.30f&&grounded;



    }
}
