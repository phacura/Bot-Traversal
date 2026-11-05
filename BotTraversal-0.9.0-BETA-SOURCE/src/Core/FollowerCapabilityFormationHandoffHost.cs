using System;
using EFT;
using UnityEngine;
using UnityEngine.AI;
namespace BotTraversal.Core
{
    internal static partial class FollowerTraversalTestRuntime
    {
        // One synchronous PIT Follow call owns this receipt. It is never stored in a Bot registry.
        private sealed class CapabilityGenericFollowHandoff
        {
            internal BotOwner Bot;internal long Generation;internal float Lease,Armed,Limit;
            internal LearningV365ContinuousRouteProducerState Producer;
            internal FollowerMovementRecoveryState Recovery;internal PhysicalNavigationEpochHandoff Epoch;
            internal bool Finished;
        }
        private static bool HasCapabilityNormalFormationConsumer(BotOwner bot)
        {
            long generation=FollowerMovementWriterAuthority.PeekExactGeneration(bot,out var owner);
            return FollowerCapabilityPassagePolicy.NormalFormation(generation>0&&owner==FollowerMovementWriterAuthority.WriterDomain.Follow&&IsCapabilityRouteRetrySafe(bot),
                HasActiveTraversalProbe(bot),HasCapabilityUnresolvedRouteWork(bot)||HasCapabilityNewRecordedWork(bot),HasCapabilityLivePassageProducer(bot),HasCurrentCapabilityNativeWay(bot,out _));
        }
        internal static object? PrepareCapabilityGenericFollowHandoff(BotOwner bot,float followDistance,ref bool force,ref float suppliedDistance)
        {
            if(bot?.GetPlayer?.MovementContext==null||bot.Mover==null||!IsLiveBot(bot)||
                !LearningV365ContinuousRouteProducerStates.TryGetValue(bot.ProfileId,out var producer)||producer==null||!producer.Generic||
                producer.LearningV423TraversalOwnedGeneration>0||!FollowerMovementRecoveryStates.TryGetValue(bot.ProfileId,out var recovery)||recovery==null||
                recovery.LearningV350TerminalRecoveryEligible||recovery.LearningV349GenericPassageLeaseUntil<Time.time||
                !PhysicalNavigationEpochHandoffs.TryGetValue(bot.ProfileId,out var epoch)||epoch==null||epoch.Generation!=raidGeneration||
                epoch.SourceEdgeId<=0||CapabilityGenericResumeFloor(bot)<=0)return null;
            var player=bot.GetPlayer;var leader=FollowerEnvironment.LocalPlayer;if(!IsLivePlayer(leader))return null;
            long generation=FollowerMovementWriterAuthority.PeekExactGeneration(bot,out var owner);
            bool current=generation>0&&owner==FollowerMovementWriterAuthority.WriterDomain.Follow&&epoch.SourceMovementGeneration<=generation;
            bool pending=HasActiveTraversalProbe(bot)||HasCapabilityUnresolvedRouteWork(bot)||HasCapabilityNewRecordedWork(bot)||IsCapabilityPortalWaiting(bot);
            bool actor=IsProbeTraversalState(player.CurrentStateName)||player.MovementContext.OverridenState!=null||player.MovementContext.OverridenControlsState!=null||player.MovementContext.IgnoreDeltaMovement;
            bool tight=NavMesh.SamplePosition(player.Position,out var sample,ExitAuthorityTopologyRejoinSampleRadius,NavMesh.AllAreas)&&
                GetPlanarDistance(player.Position,sample.position)<=ExitAuthorityTopologyRejoinSampleRadius&&Mathf.Abs(player.Position.y-sample.position.y)<=ExitAuthorityTopologyRejoinMaxVertical;
            bool path=CanAcceptFollowPath(bot,leader.Position,"leader-primary",out float length,out _);
            float distance=Vector3.Distance(player.Position,leader.Position);
            bool recent=recovery.LearningV350GenericPassageArmedAt>=0&&recovery.LearningV349LastPhysicalProgressAt>=recovery.LearningV350GenericPassageArmedAt&&
                Time.time-recovery.LearningV349LastPhysicalProgressAt<=TraversalRouteRecoveryDebounceSeconds;
            if(!FollowerCapabilityPassagePolicy.GenericFollowStart(IsCapabilityRouteRetrySafe(bot),player.MovementContext.IsGrounded,current,pending,actor,tight,path,
                distance,length,followDistance,recent)||!FollowerMovementWriterAuthority.TryEnterBoundPhysicalMotion(bot,owner,generation,out var scope,out _))return null;
            using(scope)
            {
                if(!ReferenceEquals(LearningV365ContinuousRouteProducerStates[bot.ProfileId],producer)||
                    !TryRetireNativeMoverPathPreserveLocomotion(bot,"genericToOriginalFollowCall",out _)||
                    !TryScrubLearningV364NativeMoverEpochAnchor(bot,player.Position,"genericToOriginalFollowCall",out _))return null;
                var receipt=new CapabilityGenericFollowHandoff{Bot=bot,Generation=generation,Producer=producer,Recovery=recovery,Epoch=epoch,
                    Lease=recovery.LearningV349GenericPassageLeaseUntil,Armed=recovery.LearningV350GenericPassageArmedAt,Limit=followDistance};
                // Suspend only for this actual native call. No timeout or progress clock is renewed.
                LearningV365ContinuousRouteProducerStates.Remove(bot.ProfileId);
                global::BotTraversal.LegacyOriginalPITFollowBridge.CancelDelivery(bot.ProfileId,preserveCadence:true);
                epoch.FreshConsumerPathObserved=false;epoch.NativeBookkeepingSynchronized=false;
                // Force the existing native decision once; otherwise its 1-2s cached cadence can
                // skip the very call intended to create a consumer. Distance comes from this body.
                force=true;suppliedDistance=distance;
                WriteEvent("capabilityGenericFollowConsumerCallPrepared",bot,new{generation,distance,followDistance,
                    targetSequence=producer.TargetSequence,progressClockReset=false,nativeSuccessRecorded=false});
                return receipt;
            }
        }
        internal static void CompleteCapabilityGenericFollowHandoff(BotOwner bot,object? state,bool ran)
        {
            var receipt=state as CapabilityGenericFollowHandoff;
            if(receipt==null||receipt.Finished||!ReferenceEquals(receipt.Bot,bot))return;
            receipt.Finished=true;
            long generation=FollowerMovementWriterAuthority.PeekExactGeneration(bot,out var owner);
            bool same=bot?.GetPlayer?.MovementContext!=null&&generation==receipt.Generation&&owner==FollowerMovementWriterAuthority.WriterDomain.Follow&&
                FollowerMovementRecoveryStates.TryGetValue(bot.ProfileId,out var recovery)&&ReferenceEquals(recovery,receipt.Recovery)&&
                recovery.LearningV349GenericPassageLeaseUntil==receipt.Lease&&recovery.LearningV350GenericPassageArmedAt==receipt.Armed&&
                PhysicalNavigationEpochHandoffs.TryGetValue(bot.ProfileId,out var epoch)&&ReferenceEquals(epoch,receipt.Epoch)&&epoch.Generation==raidGeneration&&
                !LearningV365ContinuousRouteProducerStates.ContainsKey(bot.ProfileId)&&IsCapabilityRouteRetrySafe(bot)&&
                !HasActiveTraversalProbe(bot)&&!HasCapabilityUnresolvedRouteWork(bot)&&!HasCapabilityNewRecordedWork(bot)&&
                bot.GetPlayer.MovementContext.IsGrounded&&!IsProbeTraversalState(bot.GetPlayer.CurrentStateName)&&
                bot.GetPlayer.MovementContext.OverridenState==null&&bot.GetPlayer.MovementContext.OverridenControlsState==null&&!bot.GetPlayer.MovementContext.IgnoreDeltaMovement;
            bool path=bot?.Mover?.ActualPathController?.HavePath==true&&bot.Mover.HasPathAndNoComplete&&bot.Mover.IsMoving&&!bot.Mover.Pause&&!bot.Mover.Blocked&&
                receipt.Epoch.FreshConsumerPathObserved&&receipt.Epoch.FreshConsumerPathGeneration==generation&&
                receipt.Epoch.NativeBookkeepingSynchronized&&receipt.Epoch.SynchronizedMovementGeneration==generation;
            bool idle=bot?.Mover!=null&&bot.Mover.ActualPathController?.HavePath!=true&&!bot.Mover.HasPathAndNoComplete&&!bot.Mover.IsMoving;
            var leader=FollowerEnvironment.LocalPlayer;
            float distance=IsLivePlayer(leader)&&bot!=null?Vector3.Distance(bot.Position,leader.Position):float.PositiveInfinity;
            bool accepted=FollowerCapabilityPassagePolicy.GenericFollowCommit(ran,same,path,idle,distance,receipt.Limit);
            if(accepted)
            {
                var old=receipt.Recovery;
                old.LearningV349GenericPassageLeaseUntil=-1;old.LearningV350GenericPassageArmedAt=-1;
                old.LearningV350GenericPassageArmCursorSequence=0;old.LearningV349PhysicalStallSince=-1;
                old.LowStepActive=false;old.LowStepAttempted=false;
            }
            else if(same)
            {
                // Refusal or exception restores the SAME bounded producer and original deadline.
                LearningV365ContinuousRouteProducerStates[bot.ProfileId]=receipt.Producer;
            }
            WriteEvent("capabilityGenericFollowConsumerCallCompleted",bot,new{generation,accepted,ran,same,
                nativePath=path,nativeIdle=idle,distance,followDistance=receipt.Limit,restored=!accepted&&same,
                originalLease=receipt.Lease,nativeSuccessRecorded=false,bodyRelocated=false});
        }
    }
}
