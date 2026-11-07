// R3O_CLIMB_EXTENSION_REVIEWED
using System;
namespace BotTraversal.Core
{
    internal static class FollowerCapabilityClimbIngressPolicy
    {
        private static bool Finite(float x)=>!float.IsNaN(x)&&!float.IsInfinity(x);
        internal static bool Capture(bool current,bool grounded,float height,float distance,float maximumDistance,
            float forward,float maximumForward,float lateral,float maximumLateral)=>
            current&&grounded&&Finite(height)&&Math.Abs(height)<=.40f&&Finite(distance)&&distance>=0f&&
            Finite(maximumDistance)&&maximumDistance>0f&&distance<=maximumDistance&&
            Finite(forward)&&Finite(maximumForward)&&forward<=maximumForward&&
            Finite(lateral)&&lateral>=0f&&Finite(maximumLateral)&&maximumLateral>0f&&lateral<=maximumLateral;
        internal static bool Pose(bool near,bool walk,bool early,bool owned,float heading,float maximumHeading,
            bool sprint,bool transition,bool counterMotion,float targetDistance)=>
            !Finite(heading)||!Finite(maximumHeading)||maximumHeading<=0f||
            (near&&(heading>maximumHeading||sprint))||
            (walk&&(sprint||heading>Math.Max(35f,maximumHeading+8f)))||
            (early&&heading>Math.Max(45f,maximumHeading+20f))||
            (owned&&heading>maximumHeading)||sprint||transition||counterMotion||targetDistance<=.001f;
        internal static bool Target(bool clearRay,bool complete,float distance,float pathLength,float height,
            float forward,float maximumForward)=>
            clearRay&&complete&&Finite(distance)&&distance>=0f&&Finite(pathLength)&&pathLength>=0f&&
            pathLength<=distance*1.25f+.10f&&Finite(height)&&Math.Abs(height)<=.40f&&
            Finite(forward)&&Finite(maximumForward)&&forward<=maximumForward;
        internal static bool Resume(bool current,bool owned,bool idle,bool sprint,bool admissible,float heading,float maximumHeading)=>
            current&&owned&&idle&&!sprint&&!admissible&&Finite(heading)&&Finite(maximumHeading)&&heading<=maximumHeading;
        internal static bool KeepInput(bool current,bool owned,bool moving,float age,float deltaTime)=>
            current&&owned&&moving&&Finite(age)&&age>=0f&&Finite(deltaTime)&&deltaTime>0f&&age<=Math.Max(.001f,2f*deltaTime);
    }
}
