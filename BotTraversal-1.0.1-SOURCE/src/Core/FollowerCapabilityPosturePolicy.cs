using System;
namespace BotTraversal.Core
{
    // Posture is a constraint on the existing walking capability, never another movement owner.
    internal static class FollowerCapabilityPosturePolicy
    {
        internal static float Normalize(float pose)=>float.IsNaN(pose)||float.IsInfinity(pose)||pose<0f||pose>1f?1f:pose;
        internal static bool Crouched(float pose)=>Normalize(pose)<=.6f;
        internal static bool Locomotion(string state)=>state=="Run"||state=="Idle"||state=="Walk";
        internal static bool Capture(float from,float to,string fromState,string toState,float distance,float vertical,float seconds)
            =>Crouched(from)&&Crouched(to)&&Locomotion(fromState)&&Locomotion(toState)&&
              !float.IsNaN(distance)&&distance>=.15f&&distance<=1.75f&&vertical>=0f&&vertical<=.85f&&seconds>0f&&seconds<=2f;
        internal static float Required(float from,float to,string fromState,string toState)
            =>Crouched(from)&&Crouched(to)&&Locomotion(fromState)&&Locomotion(toState)?Math.Min(Normalize(from),Normalize(to)):1f;
        internal static bool Same(float a,float b)=>Crouched(a)==Crouched(b)&&(!Crouched(a)||Math.Abs(Normalize(a)-Normalize(b))<=.1f);
        internal static float Resolve(float requested,float contract)=>Crouched(contract)?Math.Min(Normalize(requested),Normalize(contract)):Normalize(requested);
        internal readonly struct Capsule
        {
            internal readonly float Bottom,Top,Radius;
            internal Capsule(float bottom,float top,float radius){Bottom=bottom;Top=top;Radius=radius;}
        }
        private static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        internal static Capsule QueryCapsule(float height,float center,float bodyRadius,float skin)
        {
            if(!Finite(height)||!Finite(center)||!Finite(bodyRadius)||!Finite(skin)||height<.2f||height>3f||
                bodyRadius<.05f||bodyRadius>height*.5f||skin<0f||skin>.5f)
                return new Capsule(.38f,1.42f,.30f);
            float radius=Math.Min(.30f,bodyRadius),inset=Math.Min(.02f,skin);
            return new Capsule(center-height*.5f+radius,center+height*.5f-radius,Math.Max(.03f,radius-inset));
        }
        internal static bool MergeSnapshot(bool published,bool hasCurrent,float snapshotPose,float currentPose)
            =>!published&&(!hasCurrent||Same(snapshotPose,currentPose));
        internal static bool HoldDropBoundary(bool pending,int snapshotId,int activeId)
            =>pending&&snapshotId>0&&snapshotId==activeId;
    }
}
