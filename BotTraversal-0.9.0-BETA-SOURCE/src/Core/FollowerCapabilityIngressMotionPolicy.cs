using System;
namespace BotTraversal.Core
{
    internal static class FollowerCapabilityIngressMotionPolicy
    {
        private static bool Finite(double x)=>!double.IsNaN(x)&&!double.IsInfinity(x);
        internal static bool EntryCorrection(bool entry,bool local,double lateral,double maximum,double facing,double minimum)
            => entry&&local&&Finite(lateral)&&Finite(maximum)&&maximum>0&&Finite(facing)&&Finite(minimum)&&
                (lateral>maximum||facing<minimum);
        internal static bool WalkingGaitCorrection(bool entry,bool precision)=>entry&&precision;
        internal static double WalkingTurnSpeed(double requested,bool precision)
            => precision&&Finite(requested)&&requested>0?Math.Min(requested,.65):requested;
        internal static bool WalkingTurnPrecision(double distance,double turnDot,double speed)
            => Finite(distance)&&distance>=-.15&&Finite(turnDot)&&turnDot>=-1.001&&turnDot<.94&&
                Finite(speed)&&speed>=0&&distance<=BrakingDistance(speed);
        internal static bool WalkingLanePrecision(double side,double height,double heading)
            => !Finite(side)||side<0||side>.22||!Finite(height)||Math.Abs(height)>.35||
                !Finite(heading)||heading<.94||heading>1.001;
        internal static bool ExecutionSprint(bool demonstrated,bool current,bool canSprint,bool correction,
            double forward,double lateral,double heading,double tangent,double speed)
            => canSprint&&!correction&&(demonstrated||current)&&Finite(forward)&&Finite(lateral)&&lateral>=0&&
                Finite(heading)&&heading>=-1.001&&heading<=1.001&&Finite(tangent)&&tangent>=-1.001&&tangent<=1.001&&Finite(speed)&&speed>=0&&speed<=8;
        internal static double ChordDeviation(double ax,double az,double bx,double bz,double px,double pz)
        {
            if(!Finite(ax)||!Finite(az)||!Finite(bx)||!Finite(bz)||!Finite(px)||!Finite(pz))return double.PositiveInfinity;
            double dx=bx-ax,dz=bz-az,den=dx*dx+dz*dz;
            double t=den>1e-10?Math.Max(0,Math.Min(1,((px-ax)*dx+(pz-az)*dz)/den)):0;
            double x=px-(ax+t*dx),z=pz-(az+t*dz);return Math.Sqrt(x*x+z*z);
        }
        internal static bool ChordInside(double deviation,double width)
            => Finite(deviation)&&Finite(width)&&width>0&&deviation<=width*.5;
        internal static bool RejoinChordInside(double deviation,double width,double initial,double fraction)
            => Finite(deviation)&&deviation>=0&&Finite(width)&&width>0&&Finite(initial)&&initial>=0&&
                Finite(fraction)&&fraction>=0&&fraction<=1&&deviation<=width*.5+initial*(1-fraction);
        internal static bool ExecutionGaitPhase(bool egress,bool verify,bool commit)=>!egress&&!verify&&!commit;
        internal static bool OwnedIngress(bool live,bool current,bool admitted,bool owns,bool debt)
            => live&&current&&admitted&&owns&&debt;
        internal static bool QueueMovingAway(bool active,bool strategy,bool inFlight,double best,double distance,double speed,double heading)
            => !active&&!strategy&&!inFlight&&Finite(best)&&best>=0&&Finite(distance)&&distance>best+.75&&
                Finite(speed)&&speed>.5&&Finite(heading)&&heading<-.15;
        internal static bool VerifyExitCorrection(bool drop,bool moving,bool teleport,bool grounded,bool supported,bool nav,bool exitSide,
            double distance,double forward,double height,double belowEntry)
            => drop&&!moving&&!teleport&&grounded&&supported&&nav&&!exitSide&&Finite(distance)&&distance>0&&distance<=.85&&
                Finite(forward)&&forward>.28&&forward<=.85&&Finite(height)&&Math.Abs(height)<=.35&&Finite(belowEntry)&&belowEntry>.20;
        internal static bool WallBlocks(bool hit,double normalY,double opposition,double distance)
            => hit&&Finite(normalY)&&normalY<.65&&Finite(opposition)&&opposition>.10&&Finite(distance)&&distance>=0;
        internal static bool KeepNativeMove(bool boundBefore,bool boundAfter)=>!boundBefore||boundAfter;
        internal static bool Walk(bool activeActuator,bool waiting,bool retry,bool strategy,bool localNonJump,bool jumpUnready)
            => !activeActuator&&(waiting||retry||strategy||localNonJump||jumpUnready);
        internal static bool JumpSprint(bool centered,bool facing,bool current,bool demonstratedSprint,double forward)
            => centered&&facing&&demonstratedSprint&&Finite(forward)&&(current||forward<=-1.2);
        internal static bool ReadyJumpMotion(double speed,double heading,double minimumSpeed,double minimumHeading)
            => Finite(speed)&&Finite(heading)&&Finite(minimumSpeed)&&Finite(minimumHeading)&&minimumSpeed>=0&&
                minimumHeading>=0&&minimumHeading<=1&&speed>=minimumSpeed&&heading>=minimumHeading&&heading<=1.001;
        internal static bool ConsumeReadyEntry(bool selected,bool actuator,bool stable)=>stable||selected&&actuator;
        internal static double BrakingDistance(double speed)=>Finite(speed)&&speed>=0?Math.Max(1.2,.25+.30*speed+.08*speed*speed):double.PositiveInfinity;
        internal static bool RouteSprint(bool jump,bool demonstrated,bool current,bool canJump,double forward,double lateral,double heading,double tangent,double speed)
        {
            if(!demonstrated||!Finite(forward)||!Finite(lateral)||lateral<0||lateral>.25||
                !Finite(heading)||heading<.97||heading>1.001||!Finite(tangent)||tangent<.98||tangent>1.001||
                !Finite(speed)||speed<0||speed>8)return false;
            if(jump)return JumpSprint(true,true,current,true,forward)&&forward<=.16&&
                (canJump||-forward>BrakingDistance(speed));
            return -forward>BrakingDistance(speed)+(current?0:.60)&&(current||-forward>=5);
        }
        internal static bool Connector(bool owns,bool safe,bool active,bool failure,bool ground,double distance,double join,double segment,double vertical,bool support,bool blocked,bool recordedNearEntry=false)
            => owns&&safe&&!active&&!failure&&ground&&Finite(distance)&&distance>(recordedNearEntry?.65:2.25)&&distance<=5&&
                Finite(join)&&join>=0&&join<=.85&&Finite(segment)&&segment>.15&&segment<=1.5&&
                Finite(vertical)&&Math.Abs(vertical)<=.45&&support&&!blocked;
        internal static bool StopCompletedFollowInput(bool exact,bool safe,bool grounded,bool active,bool pending,bool path,bool moving,bool input,double distance,double age)
            => exact&&safe&&grounded&&!active&&!pending&&!path&&!moving&&input&&Finite(distance)&&distance>=0&&distance<=1.25&&Finite(age)&&age>=0&&age<=6;
        internal static bool StrategyExpired(double elapsed,double noProgress)
            => !Finite(elapsed)||!Finite(noProgress)||elapsed<0||noProgress<0||elapsed>10||noProgress>3;
        internal static bool PendingDeadline(string reason)
            => reason=="sequenceStrategyDispatchTimeout"||reason=="sequenceStrategyApproachNoMeasuredProgress";
        internal static bool NativeExpired(double elapsed,double noProgress,double span=0)
            => !Finite(elapsed)||!Finite(noProgress)||!Finite(span)||elapsed<0||noProgress<0||span<0||elapsed>Math.Max(12,span*2+6)||noProgress>4;
        internal static bool ProtectLocalBody(bool safe,bool waiting,bool returning,bool selected)
            => safe&&(waiting||returning||selected);
        internal static bool RejectPostCompletionProjection(bool safe,bool exactEpoch,bool grounded,double age,double planar,double vertical)
            => safe&&exactEpoch&&grounded&&Finite(age)&&age>=0&&age<=6&&Finite(planar)&&planar>=0&&
                Finite(vertical)&&(planar>.20||Math.Abs(vertical)>.30);
        internal static float ApproachSpeed(float requested,float distance,bool precise,bool aligned,bool jump)
        {
            if(float.IsNaN(requested)||!Finite(distance)||distance<0)return 0;
            float cap=precise?FollowerCapabilityRouteRetryPolicy.ReturnSpeed(aligned&&jump?Math.Max(distance,1.4f):distance):
                aligned?(jump?1f:distance>1f?1f:.85f):distance<=.65f?.35f:distance<=1.4f?.60f:.80f;
            return Math.Min(Math.Max(0,requested),cap);
        }
        internal static bool PrefixPath(bool hasPrefix,double body,double entry,double target,double corner)
            => !hasPrefix||(Finite(body)&&Finite(entry)&&Finite(target)&&Finite(corner)&&
                target>=Math.Min(body,Math.Min(0,entry))-.14&&corner>=Math.Min(body,Math.Min(0,entry))-.14);
        internal static float WalkSpeed(float requested,float distance)
            => float.IsNaN(requested)?0f:Math.Min(Math.Max(0,requested),FollowerCapabilityRouteRetryPolicy.ReturnSpeed(distance));
    }
}
