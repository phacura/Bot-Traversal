using System;
namespace BotTraversal.Core
{
    internal readonly struct CapabilityApproachPlan
    {
        internal readonly bool Active,Centering;
        internal readonly float Forward,Lateral,Speed;
        internal CapabilityApproachPlan(bool active,bool centering,float forward,float lateral,float speed)
        {Active=active;Centering=centering;Forward=forward;Lateral=lateral;Speed=speed;}
    }
    internal static class FollowerCapabilityApproachPolicy
    {
        private static bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v);
        internal static CapabilityApproachPlan Jump(bool grounded,bool local,bool terminal,float lateral,float maxLateral,float forwardSpeed,float centeringSpeed)
        {
            bool active=grounded&&local&&!terminal&&Finite(lateral)&&Finite(maxLateral)&&maxLateral>0f&&
                lateral>=0f&&lateral<=2f*maxLateral&&Finite(forwardSpeed)&&forwardSpeed>0f&&Finite(centeringSpeed)&&centeringSpeed>0f;
            bool center=active&&lateral>maxLateral;
            // A small forward tangent preserves axis-facing ownership; the existing native
            // frontier independently removes any advance while alignment is not ready.
            return new CapabilityApproachPlan(active,center,center?.01f:1f,center?-lateral:0f,center?Math.Min(forwardSpeed,centeringSpeed):forwardSpeed);
        }
        internal static float VaultLead(float forward,float lateral,float maximum)
        {
            if(!Finite(forward)||!Finite(lateral)||!Finite(maximum)||lateral<0f||maximum<=0f||lateral>maximum||forward>=0f||forward< -1.40f)return 0f;
            return Math.Min(.45f,-forward);
        }
        internal static bool ForwardSample(float requestedLead,float sampledForward,float sampledSide)=>
            Finite(requestedLead)&&Finite(sampledForward)&&Finite(sampledSide)&&requestedLead>0f&&
            sampledForward>=requestedLead*.65f&&sampledSide>=0f&&sampledSide<=.08f;
        internal static bool SprintFloor(float speed,int samples,float minimum)=>
            Finite(speed)&&speed>=0f&&Finite(minimum)&&minimum>=0f&&(samples<=0||minimum<=0f||speed>=minimum);
        internal static float VaultSideStep(float lateral)=>!Finite(lateral)||lateral<0f?0f:Math.Min(.18f,lateral);
        // Keep the existing .55m circular native trigger reachable without demanding
        // the demonstrated centre line. Reserve .30m of forward approach, the existing
        // native forward-input evidence distance, inside that same trigger circle.
        internal static float VaultPortalExcess(float lateral,float maximum,float trigger)
        {
            if(!Finite(lateral)||lateral<0f||!Finite(maximum)||maximum<=0f||!Finite(trigger)||trigger<=.30f)return lateral;
            float halfWidth=Math.Min(maximum,(float)Math.Sqrt(trigger*trigger-.30f*.30f));
            return Math.Max(0f,lateral-halfWidth);
        }
        internal static bool RotateVelocityAnchor(float age)=>Finite(age)&&age>=.06f;
        internal static bool PreviousVelocityAnchor(float age)=>Finite(age)&&age>=0f&&age<.04f;
        internal static bool BodyVelocityObservation(bool current,float speed,float duration,float age)=>
            current&&Finite(speed)&&speed>=0f&&Finite(duration)&&duration>=.04f&&duration<=.12f&&Finite(age)&&age>=0f&&age<=.12f;
        internal static bool CarryBodyObservation(bool sameMission,bool exactCurrent,bool traversal,long oldGeneration,long newGeneration,float age,float planar,float vertical)=>
            sameMission&&exactCurrent&&traversal&&oldGeneration>0&&newGeneration>oldGeneration&&Finite(age)&&age>=0f&&age<=.12f&&
            Finite(planar)&&planar>=0f&&planar<=.65f&&Finite(vertical)&&vertical>=0f&&vertical<=.65f;
        internal static bool WalkPrefixJump(bool current,bool sameChain,float axisDot,float entryAlong,float span,float endpointOverlap,
            float bodyAlong,float bodySide,float jumpSide,float height)=>current&&sameChain&&Finite(axisDot)&&axisDot>=.94f&&
            Finite(entryAlong)&&entryAlong>=.20f&&Finite(span)&&span>=.20f&&entryAlong<=span+.16f&&
            Finite(endpointOverlap)&&endpointOverlap>=-.16f&&endpointOverlap<=1.50f&&Finite(bodyAlong)&&bodyAlong>=Math.Max(.20f,entryAlong-.95f)&&bodyAlong<=entryAlong+.16f&&
            Finite(bodySide)&&bodySide>=0f&&bodySide<=.65f&&Finite(jumpSide)&&jumpSide>=0f&&jumpSide<=.36f&&Finite(height)&&height>=0f&&height<=.65f;
        internal static bool MissedJumpWindow(bool current,float forward,float side,float duration)=>current&&
            Finite(forward)&&forward>.16f&&forward<=.75f&&Finite(side)&&side>=0f&&side<=.72f&&
            Finite(duration)&&duration>=.04f&&duration<=.12f;
        internal static bool CrossedJumpWindow(bool current,float before,float after,float duration,float side)=>current&&
            Finite(before)&&before<=.16f&&before>=-.95f&&Finite(after)&&after>.16f&&after<=.75f&&after-before>=.04f&&
            Finite(duration)&&duration>=.04f&&duration<=.12f&&Finite(side)&&side>=0f&&side<=.36f;
        // Retained RC118 comparison rule; production now uses BodyVelocityObservation,
        // while native grounding/state remain in the existing native Jump guards.
        internal static bool ReconcileJumpVelocity(bool current,bool grounded,bool locomotion,
            float native,float measured,float duration,float age,float heading,float demonstrated)=>
            current&&grounded&&locomotion&&Finite(native)&&Finite(measured)&&measured>=1.1f&&
            Finite(duration)&&duration>=.04f&&duration<=.12f&&Finite(age)&&age>=0f&&age<=.12f&&
            Finite(heading)&&heading>=.94f&&native>=measured*2.5f&&
            FollowerCapabilityRouteRetryPolicy.UnsafeJumpSpeed(native,demonstrated)&&
            !FollowerCapabilityRouteRetryPolicy.UnsafeJumpSpeed(measured,demonstrated);
        internal static bool ColdRunTrial(bool currentDemo,bool consumed,bool run,bool grounded,bool canJump,
            float forward,float lead,float lateral,float facing,float heading,float speed)=>
            currentDemo&&!consumed&&run&&grounded&&canJump&&Finite(forward)&&Finite(lead)&&lead>0f&&forward>=-lead&&forward<=.16f&&
            Finite(lateral)&&lateral>=0f&&lateral<=.36f&&Finite(facing)&&facing>=.955f&&Finite(heading)&&heading>=.94f&&Finite(speed)&&speed>=1.1f;
        internal static bool JumpContact(float forward,float lateral,float facing,float heading,float speed,
            float distance,float normalY,float opposition)=>Finite(forward)&&forward>=-.55f&&forward<=.16f&&
            Finite(lateral)&&lateral>=0f&&lateral<=.36f&&Finite(facing)&&facing>=.955f&&Finite(heading)&&heading>=.94f&&
            Finite(speed)&&speed>=1.1f&&Finite(distance)&&distance>=0f&&distance<=.15f&&
            Finite(normalY)&&normalY>=-.70f&&normalY<=.70f&&Finite(opposition)&&opposition>=.35f;
        internal static bool StalledJump(bool attempted,float age,float forward,float lateral,float speed)=>attempted&&
            Finite(age)&&age>=2f&&Finite(forward)&&forward>=-.55f&&forward<=.16f&&Finite(lateral)&&lateral>=0f&&lateral<=.36f&&
            Finite(speed)&&speed>=0f&&speed<1.1f;
        internal static bool StepSupport(bool grounded,bool active,float age,float lift)=>
            (grounded||active)&&Finite(age)&&age>=0f&&age<.48f&&Finite(lift)&&lift>=-.08f&&lift<.38f;
        internal static bool LowStep(bool current,bool grounded,bool walking,bool blocked,bool raisedClear,bool support,
            float stall,float facing,float height,float progress)=>current&&grounded&&walking&&blocked&&raisedClear&&support&&
            Finite(stall)&&stall>=.28f&&Finite(facing)&&facing>=.95f&&Finite(height)&&height>=-.08f&&height<=.38f&&
            Finite(progress)&&progress>=-.05f&&progress<.18f;
        internal static bool HorizontalProgress(float displacement,float forward,float minDisplacement,float minForward)=>
            Finite(displacement)&&Finite(forward)&&displacement>=minDisplacement&&forward>=minForward;
        internal static bool Landing(bool current,bool phase,bool attempted,bool dropDown,bool pathless,
            bool legacyRegion,float progress,float required,float ahead,float height,float aboveExit)=>
            current&&phase&&attempted&&dropDown&&pathless&&legacyRegion&&Finite(progress)&&Finite(required)&&
            progress>=required&&required>=.20f&&Finite(ahead)&&ahead>.025f&&Finite(height)&&height<=.30f&&Finite(aboveExit)&&aboveExit>=.40f;
    }
}
