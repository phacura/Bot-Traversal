using System;
namespace BotTraversal.Core
{
    internal readonly struct AdjacentLandingGeometry
    {
        internal readonly float Ahead,Lateral,Distance,VerticalError,SuccessorSpan,AboveExit;
        internal readonly bool Grounded,HasMotionContext;
        internal AdjacentLandingGeometry(float ahead,float lateral,float distance,float verticalError,float span,bool grounded,float aboveExit,bool hasMotionContext=true)
        {Ahead=ahead;Lateral=lateral;Distance=distance;VerticalError=verticalError;SuccessorSpan=span;Grounded=grounded;AboveExit=aboveExit;HasMotionContext=hasMotionContext;}
    }
    internal readonly struct AdjacentLandingSnapshot
    {
        internal readonly AdjacentLandingGeometry Geometry;internal readonly float Requested;
        internal AdjacentLandingSnapshot(AdjacentLandingGeometry geometry,float requested){Geometry=geometry;Requested=requested;}
    }
    internal readonly struct AdjacentLandingPlan
    {
        internal readonly bool Active;internal readonly float NativeSprintCap;
        internal AdjacentLandingPlan(bool active,float cap){Active=active;NativeSprintCap=cap;}
    }
    internal static class FollowerCapabilityAdjacentLandingPolicy
    {
        private static bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v);
        private static bool Valid(AdjacentLandingGeometry g)=>Finite(g.Ahead)&&Finite(g.Lateral)&&Finite(g.Distance)&&
            Finite(g.VerticalError)&&Finite(g.SuccessorSpan)&&Finite(g.AboveExit)&&g.Lateral>=0f&&g.Distance>=0f&&
            g.VerticalError>=0f&&g.SuccessorSpan>0f&&g.VerticalError<=1.35f;
        internal static bool LegacyRegion(AdjacentLandingGeometry g)=>Valid(g)&&g.Ahead>=-0.30f&&g.Ahead<=1.55f&&g.Lateral<=0.38f&&g.Distance<=1.65f;
        internal static float MoveSpeed(AdjacentLandingGeometry g)
        {
            float bounded=Math.Max(0.24f,Math.Min(1f,0.24f+Math.Max(0f,g.Ahead)*0.43f));
            return g.HasMotionContext&&!g.Grounded&&g.AboveExit>0.40f?Math.Max(bounded,0.55f):bounded;
        }
        internal static AdjacentLandingPlan Evaluate(AdjacentLandingSnapshot s)
        {
            var g=s.Geometry;
            // A slightly overrun, still upper landing does not release a current
            // predecessor's speed contract during its existing stable-proof window.
            // This expands only the speed region, never successor entry/success.
            bool settling=Valid(g)&&g.Ahead < -0.30f&&g.Ahead>=-Math.Max(0.30f,g.SuccessorSpan)&&
                g.Ahead<=1.55f&&g.Lateral<=0.38f&&g.Distance<=Math.Max(1.65f,g.SuccessorSpan)&&g.AboveExit>0.40f;
            if(!Finite(s.Requested)||!(LegacyRegion(g)||settling))return new AdjacentLandingPlan(false,0f);
            return new AdjacentLandingPlan(true,Math.Max(0.01f,Math.Min(2f,MoveSpeed(g)*2f)));
        }
    }
    internal interface ICapabilityAdjacentLandingPort
    {
        bool IsCurrent{get;}
        bool TryObserve(out AdjacentLandingSnapshot snapshot);
        void Restrict(float value);
    }
    internal static class FollowerCapabilityAdjacentLandingDriver
    {
        internal enum Receipt{Rejected,Unconstrained,Clamped}
        internal static Receipt Commit(ICapabilityAdjacentLandingPort port)
        {
            if(port==null||!port.IsCurrent)return Receipt.Rejected;
            if(!port.TryObserve(out var snapshot))return Receipt.Unconstrained;
            var plan=FollowerCapabilityAdjacentLandingPolicy.Evaluate(snapshot);
            if(!port.IsCurrent)return Receipt.Rejected;
            if(!plan.Active||snapshot.Requested<=plan.NativeSprintCap)return Receipt.Unconstrained;
            port.Restrict(plan.NativeSprintCap);return Receipt.Clamped;
        }
    }
}
