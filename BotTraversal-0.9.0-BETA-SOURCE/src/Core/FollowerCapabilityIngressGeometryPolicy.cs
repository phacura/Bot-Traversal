// R3P_GEOMETRY_EXTENSION_REVIEWED
using System;
namespace BotTraversal.Core
{
    internal static class FollowerCapabilityIngressGeometryPolicy
    {
        private static bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v);
        internal static bool KeepPublished(bool observed,bool published,bool finite,float span)=>
            observed&&published&&finite&&Finite(span)&&span>.05f;
        internal static bool AcceptRefresh(bool existing,float span,float start,float end,float alignment,
            float lateral,float maximumLateral,float height,float maximumHeight,float epsilon)=>
            !existing||!Finite(span)||span<=.05f||
            (Finite(start)&&Finite(end)&&Finite(alignment)&&alignment>=.85f&&
             Finite(lateral)&&lateral>=0f&&Finite(maximumLateral)&&maximumLateral>=0f&&lateral<=maximumLateral&&
             Finite(height)&&Finite(maximumHeight)&&maximumHeight>=0f&&Math.Abs(height)<=maximumHeight&&
             Finite(epsilon)&&epsilon>=0f&&start<=epsilon&&end>=span-epsilon);
        internal static bool PhysicalTarget(bool current,float distance,float maximum,float forward,
            float maximumForward,float lateral,float stop)=>
            current&&Finite(distance)&&Finite(maximum)&&distance>.20f&&distance<=maximum&&
            Finite(forward)&&Finite(maximumForward)&&forward<=maximumForward&&
            Finite(lateral)&&lateral>=0f&&Finite(stop)&&stop>0f&&distance>stop+.20f;
    }
}
