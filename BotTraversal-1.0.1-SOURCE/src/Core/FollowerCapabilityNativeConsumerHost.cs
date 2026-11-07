using System;
using System.Reflection;
using EFT;
using HarmonyLib;

namespace BotTraversal.Core
{
    internal static partial class FollowerTraversalTestRuntime
    {
        internal static void MaintainCapabilityInputBeforeNativeConsumer(MovementState consumer,Player player)
        {
            var context=player?.MovementContext;
            if(consumer==null||context==null||!ReferenceEquals(consumer.MovementContext,context)||
                !ReferenceEquals(context.CurrentState,consumer)||!ReferenceEquals(player.CurrentManagedState,consumer)||
                (consumer.Name!=EPlayerState.Run&&consumer.Name!=EPlayerState.Sprint&&consumer.Name!=EPlayerState.Idle)||
                context.OverridenState!=null||context.OverridenControlsState!=null||context.IgnoreDeltaMovement)return;
            // No frameCount dedup: Sprint clears Direction on EVERY native update.
            // Reuse current input, never manufacture forward input or physical proof.
            var bot=player.AIData?.BotOwner;
            TryDispatchCapabilityJumpAtNativeUpdate(bot);
            if(!ReferenceEquals(context.CurrentState,consumer)||!ReferenceEquals(player.CurrentManagedState,consumer))return;
            bool accepted=TryMaintainCapabilityNativeInput(bot,player,true);
            if(!accepted&&TryGetActiveProbe(bot,out var probe)&&IsProbeLifecycleCurrent(probe)&&
                ShouldEmitDiagnostic("capabilityNativeConsumerInputNotPublished:"+probe.TraversalEpisodeId,bot,.15f))
                WriteEvent("capabilityNativeConsumerInputNotPublished",bot,new{edgeId=probe.LearnedEdgeId,
                    phase=probe.Stage.ToString(),state=consumer.Name.ToString(),grounded=context.IsGrounded,
                    input=Vec2(player.InputDirection),
                    nativeDirection=Vec2(consumer.Direction),nativeSuccessRecorded=false});
        }
    }
    internal static class FollowerCapabilityNativeConsumerPatch
    {
        // Verified native field; read-only actor resolution, never a writer/owner store.
        private static readonly FieldInfo PlayerField=AccessTools.Field(typeof(MovementContext),"_player");
        internal static void Apply(Harmony harmony)
        {
            var method=AccessTools.DeclaredMethod(typeof(MovementState),"OnStateMove",new[]{typeof(float)});
            if(method==null||PlayerField==null||PlayerField.FieldType!=typeof(Player))
                throw new MissingMemberException("Native MovementState.OnStateMove / MovementContext._player");
            harmony.Patch(method,prefix:new HarmonyMethod(AccessTools.Method(typeof(FollowerCapabilityNativeConsumerPatch),nameof(Prefix))){priority=Priority.Last});
        }
        private static bool Prefix(MovementState __instance)
        {
            var context=__instance?.MovementContext;if(context==null)return true;
            var player=PlayerField.GetValue(context) as Player;var bot=player?.AIData?.BotOwner;
            if(bot==null||!BossPlayers.IsFollower(bot))return true;
            bool boundBefore=ReferenceEquals(context.CurrentState,__instance)&&ReferenceEquals(player.CurrentManagedState,__instance);
            if(boundBefore)FollowerTraversalTestRuntime.MaintainCapabilityInputBeforeNativeConsumer(__instance,player);
            bool boundAfter=ReferenceEquals(context.CurrentState,__instance)&&ReferenceEquals(player.CurrentManagedState,__instance);
            return FollowerCapabilityIngressMotionPolicy.KeepNativeMove(boundBefore,boundAfter);
        }
    }
}
