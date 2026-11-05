using System;
namespace BotTraversal.Core
{
    internal enum CapabilityFrontierKind{None,LandingProof,JumpPreparation,DropDownBoundary}
    internal readonly struct CapabilityFrontierSnapshot
    {
        internal readonly CapabilityFrontierKind Kind;
        internal readonly float RequestedForward,AvailableForward;
        internal CapabilityFrontierSnapshot(CapabilityFrontierKind kind,float requested,float available)
        {Kind=kind;RequestedForward=requested;AvailableForward=available;}
    }
    internal static class FollowerCapabilityFrontierPolicy
    {
        private static bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v);
        internal static float PreparationLead(float speed,float minimumSpeed,float configuredSeconds,float fallbackSeconds,float minSeconds,float maxSeconds,float minimumDistance)
        {
            float seconds=Math.Max(minSeconds,Math.Min(maxSeconds,configuredSeconds>0f?configuredSeconds:fallbackSeconds));
            return Math.Max(minimumDistance,Math.Max(minimumSpeed,Math.Max(0f,speed))*seconds);
        }
        internal static float ForwardRemoval(CapabilityFrontierSnapshot s)=>s.Kind!=CapabilityFrontierKind.None&&
            Finite(s.RequestedForward)&&Finite(s.AvailableForward)
                ? Math.Max(0f,s.RequestedForward-Math.Max(0f,s.AvailableForward)):0f;
        internal static float JumpBudget(float forward,float preparationLead,bool nativeLocomotion,bool centered,bool facing)=>
            !Finite(forward)||!Finite(preparationLead)||preparationLead<=0f||nativeLocomotion&&centered&&facing
                ? float.PositiveInfinity:Math.Max(0f,-preparationLead-forward);
        internal static bool DispatchFrame(bool current,bool jump,bool noProbe,bool matchingCommitment,bool safe,bool local,bool deferred)=>
            current&&jump&&noProbe&&matchingCommitment&&safe&&local&&!deferred;
    }
    internal interface ICapabilityFrontierPort
    {
        bool IsCurrent{get;}
        bool TryObserve(out CapabilityFrontierSnapshot snapshot);
        void RestrictForward(float removed);
    }
    internal static class FollowerCapabilityFrontierDriver
    {
        internal static bool Commit(ICapabilityFrontierPort port)
        {
            if(port==null||!port.IsCurrent||!port.TryObserve(out var snapshot))return false;
            float removal=FollowerCapabilityFrontierPolicy.ForwardRemoval(snapshot);
            if(removal<=0f||!port.IsCurrent)return false;
            port.RestrictForward(removal);return true;
        }
    }
}
