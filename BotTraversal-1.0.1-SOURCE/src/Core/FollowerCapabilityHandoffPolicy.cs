// R3R_HANDOFF_EXTENSION_REVIEWED
using System;
namespace BotTraversal.Core
{
    internal static class FollowerCapabilityHandoffPolicy
    {
        private static bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v);
        internal static float LandingBudget(DropDownBoundarySnapshot s,float dt,float stableSeconds,float verticalSpeed,float lateralLimit)
        {
            if(s.Phase!=DropDownConsumerPhase.Execute||!s.DropDown||!s.NativeAttempted||!s.UnownedSuccessor||
                !Finite(s.Progress)||!Finite(s.Required)||s.Required<.20f||s.Progress<s.Required||
                !Finite(s.Ahead)||s.Ahead<-.015f||s.Ahead>1.55f||
                !Finite(lateralLimit)||lateralLimit<0f||lateralLimit>.38f||
                !Finite(s.Lateral)||s.Lateral<0f||s.Lateral>lateralLimit||
                !Finite(s.VerticalError)||s.VerticalError<0f||s.VerticalError>1.35f||
                !Finite(s.AboveExit)||s.AboveExit<.40f||!Finite(s.Requested)||s.Requested<=0f||
                !Finite(s.Trigger)||s.Trigger<=0f||
                !Finite(dt)||dt<=0f||dt>.10f||!Finite(stableSeconds)||stableSeconds<.30f||
                !Finite(verticalSpeed)||verticalSpeed<0f)return -1f;
            float runway=Math.Max(0f,s.Ahead-.025f);
            float fallAllowance=Math.Min(stableSeconds,s.VerticalError/Math.Max(1f,verticalSpeed));
            float budget=runway*dt/(stableSeconds+fallAllowance);
            return Math.Min(s.Requested,Math.Min(runway,budget));
        }
        // Existing proof clock supplies age; this budget never supplies landing credit.
        // Reserve one native update before the exact successor frontier, including dt padding.
        internal static float RollingLandingBudget(DropDownBoundarySnapshot s,float dt,float stableSeconds,
            float verticalSpeed,float lateralLimit,float candidateAge,bool stableContact)
        {
            float fallback=LandingBudget(s,dt,stableSeconds,verticalSpeed,lateralLimit);
            if(fallback<0f||!stableContact||!Finite(candidateAge)||candidateAge<0f)return fallback;
            float runway=Math.Max(0f,s.Ahead-.025f);
            float remaining=Math.Max(dt,stableSeconds-Math.Min(candidateAge,stableSeconds)+dt);
            return Math.Min(s.Requested,Math.Min(runway,runway*dt/remaining));
        }
        internal static bool CanRollLandingProof(bool current,bool nativeWitness,bool stableContact,
            bool sameSuccessor,float ahead,float lateral,float budget)=>
            current&&nativeWitness&&stableContact&&sameSuccessor&&Finite(ahead)&&ahead>.025f&&ahead<=1.55f&&
            Finite(lateral)&&lateral>=0f&&lateral<=.20f&&Finite(budget)&&budget>0f&&budget<=ahead-.025f;
        // Scale the complete native planar request; axis subtraction can reverse
        // its lateral component and drive the body out of the guarded corridor.
        internal static float PlanarRetention(float forward,float removed)=>
            !Finite(forward)||forward<=0f||!Finite(removed)||removed<0f?1f:
            Math.Max(0f,Math.Min(1f,(forward-removed)/forward));
        internal static bool PreserveTerminalSprint(bool current,bool physicalLanding,bool grounded,
            bool topology,bool sprinting,bool overridden,float distance,float settle)=>
            current&&physicalLanding&&grounded&&topology&&sprinting&&!overridden&&
            Finite(distance)&&Finite(settle)&&settle>0f&&distance>settle;
    }
}
