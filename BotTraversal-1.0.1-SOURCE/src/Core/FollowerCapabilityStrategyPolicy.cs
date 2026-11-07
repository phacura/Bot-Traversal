using System;
using Kind = BotTraversal.Core.FollowerCapabilityDeliveryPolicy.Capability;

namespace BotTraversal.Core
{
    // Value-only capability choices. Host owns observations, timers, native effects
    // and lifecycle commits. Neither strategy nor traits may hold a Bot/Player.
    internal static class FollowerCapabilityStrategyPolicy
    {
        internal enum Executor { GroundCorridor, NativeJump, NativeDropDown, InjectedVault, Compatibility, FollowPath, RecoveryPath }
        internal readonly struct Traits
        {
            internal readonly bool Actuator, PassageCompletion, FinishOffMesh, AcquireOffMesh, Rolling, Transition, Landing;
            internal readonly float Lead;
            internal readonly string Source;
            internal Traits(bool actuator,bool passage,bool finish,bool acquire,bool rolling,bool transition,bool landing,float lead,string source)
            { Actuator=actuator;PassageCompletion=passage;FinishOffMesh=finish;AcquireOffMesh=acquire;Rolling=rolling;Transition=transition;Landing=landing;Lead=lead;Source=source; }
        }
        internal static Traits Describe(Kind kind,float jumpLead)
        {
            switch(kind)
            {
                case Kind.WalkThrough:return new Traits(false,true,true,true,true,true,false,0f,"provenPassageSemantics");
                case Kind.Jump:return new Traits(true,false,true,false,true,false,true,jumpLead,"provenNativeJumpSemantics");
                case Kind.DropDown:return new Traits(true,false,true,true,true,false,true,0f,"provenNaturalDropSemantics");
                case Kind.Climb:
                case Kind.Vault:return new Traits(true,false,true,false,true,true,false,0f,"provenNativeBarrierSemantics");
                case Kind.Follow:return new Traits(false,false,false,false,false,false,false,0f,"ordinaryFollowPath");
                case Kind.Recovery:return new Traits(false,false,false,false,false,false,false,0f,"existingRecoveryPath");
                default:return new Traits(true,false,false,false,true,false,false,0f,"conservativeUnknown");
            }
        }
        internal readonly struct Selection
        {
            internal readonly Executor Executor;
            internal readonly bool Native, Assisted, AssistFirst, RetiredAssistFirst;
            internal bool NativeSemantic => Executor==Executor.NativeJump || Executor==Executor.NativeDropDown;
            internal Selection(Executor executor,bool native,bool assisted,bool assistFirst=false,bool retiredAssistFirst=false)
            { Executor=executor;Native=native;Assisted=assisted;AssistFirst=assistFirst;RetiredAssistFirst=retiredAssistFirst; }
        }
        internal static Selection Select(Kind kind,bool injectedParameters,bool directionCompatible,bool preferAssist,
            bool freshSession,bool primaryRetired,bool downwardJump)
        {
            if(kind==Kind.WalkThrough) return new Selection(Executor.GroundCorridor,false,false);
            if(kind==Kind.Follow) return new Selection(Executor.FollowPath,false,false);
            if(kind==Kind.Recovery) return new Selection(Executor.RecoveryPath,false,false);
            bool jump=kind==Kind.Jump && !downwardJump;
            bool drop=kind==Kind.DropDown;
            bool retiredFirst=primaryRetired && (jump||drop);
            bool assistFirst=retiredFirst || (freshSession && preferAssist && (jump||drop));
            bool injected=(kind==Kind.Vault||kind==Kind.Climb) && injectedParameters && directionCompatible && !preferAssist && !primaryRetired;
            if(injected) return new Selection(Executor.InjectedVault,true,false);
            if(!assistFirst && jump) return new Selection(Executor.NativeJump,true,false);
            if(!assistFirst && drop) return new Selection(Executor.NativeDropDown,true,false);
            return new Selection(Executor.Compatibility,false,primaryRetired||preferAssist||!injectedParameters||!directionCompatible,assistFirst,retiredFirst);
        }
        internal static bool StartRolling(Kind kind,Executor executor,float distance,float handoff,float armDistance)
        {
            if(float.IsNaN(distance)||float.IsInfinity(distance)||distance<0f) return false;
            bool precision=kind==Kind.Vault||kind==Kind.Climb||executor==Executor.InjectedVault||executor==Executor.NativeJump||executor==Executor.NativeDropDown;
            return distance<=armDistance+0.35f && (precision||distance>handoff);
        }
    }
}
