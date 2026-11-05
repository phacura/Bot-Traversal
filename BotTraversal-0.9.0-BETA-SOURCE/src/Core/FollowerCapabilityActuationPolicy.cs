namespace BotTraversal.Core
{
    // Scalar decisions only. Execution identity, engine reads and writes stay in Host.
    internal static class FollowerCapabilityActuationPolicy
    {
        internal readonly struct JumpWindowPlan
        {
            internal readonly bool Inside,Centered,Facing,Heading;
            internal JumpWindowPlan(bool inside,bool centered,bool facing,bool heading)
            {Inside=inside;Centered=centered;Facing=facing;Heading=heading;}
        }
        internal static JumpWindowPlan JumpWindow(float forward,float lead,float maxForward,float lateral,
            float maxLateral,float facing,float minFacing,float heading,float minHeading) =>
            new JumpWindowPlan(forward>=-lead&&forward<=maxForward,lateral<=maxLateral,
                facing>=minFacing,heading>=minHeading);
        internal static bool RetiredPhysicalStrategy(int current,int epoch,int edge,int epochEdge,
            int retiredEdge,string retiredKind,string requestedKind) =>
            current==epoch&&edge==epochEdge&&edge==retiredEdge&&
            !string.IsNullOrWhiteSpace(retiredKind)&&string.Equals(retiredKind,requestedKind,System.StringComparison.OrdinalIgnoreCase);
        internal static bool CanDispatchNativeJump(bool physicalWindow,bool locomotion,bool grounded,
            bool canJump,bool centered,bool facing,bool heading,bool measuredMomentum) =>
            physicalWindow&&locomotion&&grounded&&canJump&&centered&&facing&&heading&&measuredMomentum;
        internal static bool AtomicJumpReceipt(bool supplied,bool current,bool consumed,bool lead,bool reserved,bool writer)
            => supplied&&current&&consumed&&lead&&reserved&&writer;
        internal static bool JumpTriggerWindow(bool physical,bool successor,bool atomic,bool forecast)
            => physical||successor||(atomic&&forecast);
        internal static bool LiveJumpAttempt(bool window,bool entry,bool captured,bool grounded,bool canJump,bool locomotion,
            bool input,bool position,bool facing,bool heading,bool momentum)
            => entry&&captured&&input&&CanDispatchNativeJump(window,locomotion,grounded,canJump,position,facing,heading,momentum);
        internal static bool NativeDropConsumer(bool nativeDrop,bool attempted,bool lifecycleCurrent,
            bool sameMission,bool sameStep,bool landingConsumer,bool farSide) =>
            nativeDrop&&attempted&&lifecycleCurrent&&sameMission&&sameStep&&!landingConsumer&&!farSide;
        internal readonly struct JumpIntent
        {
            internal readonly bool AxisFacing,ForwardMotion;
            internal JumpIntent(bool axisFacing,bool forwardMotion){AxisFacing=axisFacing;ForwardMotion=forwardMotion;}
        }
        private static bool Finite(float f)=>!float.IsNaN(f)&&!float.IsInfinity(f);
        internal static JumpIntent PrepareJump(bool exact,bool explicitGoal,bool activeProbe,
            float forward,float preparationLead,float maxForward,float lateral,float maxLateral,float directionDot)
        {
            bool axis=exact&&explicitGoal&&!activeProbe&&Finite(forward)&&Finite(preparationLead)&&
                Finite(maxForward)&&Finite(lateral)&&Finite(maxLateral)&&Finite(directionDot)&&preparationLead>0f&&maxLateral>0f&&
                forward>=-preparationLead&&forward<=maxForward&&lateral>=0f&&lateral<=2f*maxLateral&&directionDot>=0f;
            // Legal centerline tolerance is a corridor. Do not over-correct to its
            // zero-error axis point and destroy the very heading admission requires.
            return new JumpIntent(axis,axis&&lateral<=maxLateral);
        }
    }
}
