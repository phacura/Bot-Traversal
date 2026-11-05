using System;
namespace BotTraversal.Core
{
    internal enum DropDownConsumerPhase { None, Execute, Validate, Commit }
    internal readonly struct DropDownBoundarySnapshot
    {
        internal readonly DropDownConsumerPhase Phase;
        internal readonly bool DropDown,NativeAttempted,UnownedSuccessor;
        internal readonly float Progress,Required,Ahead,Lateral,VerticalError,AboveExit,Requested,Trigger;
        internal DropDownBoundarySnapshot(DropDownConsumerPhase phase,bool dropDown,bool nativeAttempted,bool unowned,
            float progress,float required,float ahead,float lateral,float vertical,float aboveExit,float requested,float trigger)
        {Phase=phase;DropDown=dropDown;NativeAttempted=nativeAttempted;UnownedSuccessor=unowned;
         Progress=progress;Required=required;Ahead=ahead;Lateral=lateral;VerticalError=vertical;AboveExit=aboveExit;Requested=requested;Trigger=trigger;}
    }
    internal readonly struct DropDownBoundaryPlan
    {
        internal readonly bool Active;internal readonly float ForwardBudget;
        internal DropDownBoundaryPlan(bool active,float budget){Active=active;ForwardBudget=budget;}
    }
    internal static class FollowerCapabilityDropDownTransitionPolicy
    {
        private static bool Finite(float x)=>!float.IsNaN(x)&&!float.IsInfinity(x);
        // The existing NativeDropDown projection formula, shared by execution and its consumer boundary.
        internal static float RequiredProjection(float length,float ratio)=>Math.Max(.20f,length*ratio);
        internal static bool OwnsBoundary(DropDownConsumerPhase phase)=>phase==DropDownConsumerPhase.Execute||
            phase==DropDownConsumerPhase.Validate||phase==DropDownConsumerPhase.Commit;
        internal static DropDownBoundaryPlan Plan(DropDownBoundarySnapshot s)
        {
            bool active=s.DropDown&&s.NativeAttempted&&s.UnownedSuccessor&&OwnsBoundary(s.Phase)&&
                Finite(s.Progress)&&Finite(s.Required)&&s.Required>=.20f&&s.Progress>=s.Required&&
                Finite(s.Ahead)&&Finite(s.Lateral)&&Finite(s.VerticalError)&&Finite(s.AboveExit)&&Finite(s.Requested)&&Finite(s.Trigger)&&
                s.Lateral>=0f&&s.Lateral<=.20f&&s.VerticalError>=0f&&s.VerticalError<=.30f&&s.AboveExit>=.40f&&
                s.Trigger>0f&&s.Ahead>=-.015f&&s.Ahead<=s.Trigger+Math.Max(0f,s.Requested);
            return new DropDownBoundaryPlan(active,active?Math.Max(0f,s.Ahead-.025f):0f);
        }
    }
}
