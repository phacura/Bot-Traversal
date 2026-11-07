using System;
namespace BotTraversal.Core
{
    // Consumer readiness and mission evidence use separate clocks and receipts.
    internal static class FollowerCapabilityPreparationPolicy
    {
        private static bool Finite(float x)=>!float.IsNaN(x)&&!float.IsInfinity(x);
        internal static bool CanMaterialize(int episode,bool alreadyMaterialized,int end,int executionFloor)=>
            episode>0&&!alreadyMaterialized&&end>executionFloor;
        internal static float FreshDeadline(float current,float now,float lifetime,bool independent,float created,float terminalSince)=>
            independent&&Finite(current)&&Finite(now)&&Finite(lifetime)&&lifetime>0f&&created>terminalSince
                ? Math.Max(current,now+lifetime):current;
        internal static bool LandingHold(bool candidate,bool airborne,bool descent,bool farSide,
            float ahead,float lateral,float vertical,float requestedForward,float trigger)=>
            candidate&&airborne&&descent&&farSide&&Finite(ahead)&&Finite(lateral)&&Finite(vertical)&&Finite(requestedForward)&&
            ahead>=-.015f&&ahead<=trigger+Math.Max(0f,requestedForward)&&lateral<=.20f&&vertical<=.30f;
        internal static bool CanPrepare(bool grounded,bool local,bool centered,bool facing,bool terminal)=>
            grounded&&local&&centered&&facing&&!terminal;
    }
}
