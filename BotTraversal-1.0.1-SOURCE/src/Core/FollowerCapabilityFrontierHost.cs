using System;
using System.Linq;
using EFT;
using HarmonyLib;
using UnityEngine;
namespace BotTraversal.Core
{
    internal static partial class FollowerTraversalTestRuntime
    {
        private static bool TryCaptureCapabilityLandingEntryFrontier(

            ProbeRuntime probe, LearnedTraversalCommitment commitment, Vector3 position,

            out FollowerRouteMissionStep successor, out float entryAhead, out float entryLateral)

        {

            successor = null;

            entryAhead = 0f;

            entryLateral = 0f;

            if (probe?.Bot == null || probe.Player?.MovementContext == null ||

                commitment == null || !probe.Player.MovementContext.IsGrounded ||

                probe.NativeDropDownLandingStableSince < 0f ||

                !probe.NativeDropDownSawAirborne || !probe.NativeDropDownSawDescent ||

                !probe.NativeDropDownSawFarSide ||

                !TryResolveLearningV376MissionForProbe(

                    probe, out FollowerRouteMission mission, out int index) ||

                mission?.Steps == null || mission.CurrentStepIndex != index ||

                index < 0 || index + 1 >= mission.Steps.Count ||

                !IsRouteMissionSameChainImmediateSuccessor(mission, index + 1))

                return false;

            FollowerRouteMissionStep current = mission.Steps[index];

            FollowerRouteMissionStep next = mission.Steps[index + 1];

            if (current == null || next == null ||

                current.EdgeId != commitment.EdgeId ||

                current.EpisodeId != probe.TraversalEpisodeId ||

                !string.Equals(next.Kind, "DropDown", StringComparison.OrdinalIgnoreCase) ||

                next.AttemptStartedObserved || !IsFinite(position) ||

                !IsFinite(next.RawEntry) || !IsFinite(next.RawExit))

                return false;

            Vector3 axis = GetFlatDirection(next.RawExit - next.RawEntry);

            if (axis.sqrMagnitude < 0.5f ||

                next.RawEntry.y - next.RawExit.y < 0.25f ||

                Mathf.Abs(position.y - next.RawEntry.y) > 0.30f)

                return false;

            Vector3 delta = next.RawEntry - position;

            delta.y = 0f;

            entryAhead = Vector3.Dot(delta, axis);

            entryLateral = (delta - axis * entryAhead).magnitude;

            if (entryAhead < -0.015f ||

                entryAhead > NativeDropDownEntryTriggerDistance - 0.025f ||

                entryLateral > 0.20f ||

                delta.magnitude > NativeDropDownEntryTriggerDistance - 0.025f)

                return false;

            successor = next;

            return true;

        }
        internal static Vector3 ConstrainCapabilityMotionFrontier(MovementContext context,Player player,Vector3 motion)
        {
            var bot=player?.AIData?.BotOwner;
            if(bot==null||context==null||!IsFinite(motion)||
                !FollowerRouteMissions.TryGetValue(bot.ProfileId,out var mission)||mission==null||
                mission.CurrentStepIndex<0||mission.CurrentStepIndex>=mission.Steps.Count)return motion;
            var step=mission.Steps[mission.CurrentStepIndex];
            var port=new CapabilityFrontierPort(bot,player,context,mission,step,motion);
            if(!FollowerCapabilityFrontierDriver.Commit(port))
            {
                ObserveCapabilityLandingMotionFrontier(bot,player,context,motion,port.IsCurrent);
                return motion;
            }
            if(ShouldEmitDiagnostic("capabilityMotionFrontier:"+step.EpisodeId,bot,0.12f))
                WriteEvent("capabilityNativeMotionFrontierConstrained",bot,new{missionId=mission.MissionId,
                    stepIndex=mission.CurrentStepIndex,edgeId=step.EdgeId,episodeId=step.EpisodeId,
                    reason=port.Kind.ToString(),phase=port.observedProbe?.Stage.ToString(),requested=Vec(motion),constrained=Vec(port.Result),
                    body=Vec(player.Position),forwardBudget=port.Budget,verticalPreserved=motion.y==port.Result.y,
                    nativeSuccessRecorded=false,bodyRelocated=false});
            return port.Result;
        }
        private sealed class CapabilityFrontierPort:ICapabilityFrontierPort
        {
            private readonly BotOwner bot;private readonly Player player;private readonly MovementContext context;
            private readonly FollowerRouteMission mission;private readonly FollowerRouteMissionStep step;
            private readonly FollowerTraversalLifecycle.Identity origin;private readonly Vector3 requested;internal ProbeRuntime observedProbe;
            internal Vector3 Result;private Vector3 axis;internal float Budget;internal CapabilityFrontierKind Kind;
            internal CapabilityFrontierPort(BotOwner b,Player p,MovementContext c,FollowerRouteMission m,FollowerRouteMissionStep s,Vector3 motion)
            {bot=b;player=p;context=c;mission=m;step=s;requested=motion;Result=motion;
                origin=LifecycleIdentity(m,s,m.CurrentStepIndex,FollowerMovementWriterAuthority.PeekExactGeneration(b,out _));}
            public bool IsCurrent
            {
                get
                {
                    long gen=FollowerMovementWriterAuthority.PeekExactGeneration(bot,out var owner);
                    return origin.Valid&&ReferenceEquals(bot.GetPlayer,player)&&ReferenceEquals(player.MovementContext,context)&&
                        (owner==FollowerMovementWriterAuthority.WriterDomain.Follow||owner==FollowerMovementWriterAuthority.WriterDomain.Traversal)&&
                        gen>0&&gen==origin.Writer&&!mission.Lifecycle.Retired&&mission.LifecycleRaid==raidGeneration&&mission.LifecycleBotId==bot.ProfileId&&Time.time<=mission.ExpiresAt&&
                        FollowerRouteMissions.TryGetValue(bot.ProfileId,out var live)&&ReferenceEquals(live,mission)&&
                        mission.CurrentStepIndex>=0&&mission.CurrentStepIndex<mission.Steps.Count&&
                        ReferenceEquals(mission.Steps[mission.CurrentStepIndex],step)&&step.Lifecycle.DebtOutstanding&&
                        origin.Equals(LifecycleIdentity(mission,step,mission.CurrentStepIndex,gen))&&
                        (observedProbe==null||IsProbeLifecycleCurrent(observedProbe)&&observedProbe.Lifecycle.Origin.Equals(origin)&&ReferenceEquals(observedProbe.Player,player))&&
                        (Kind!=CapabilityFrontierKind.JumpPreparation||!HasActiveTraversalProbe(bot));
                }
            }
            public bool TryObserve(out CapabilityFrontierSnapshot snapshot)
            {
                snapshot=default;
                var follower=BossPlayers.GetFollowerByProfileId(bot.ProfileId);
                if(follower!=null&&follower.TryPeekActiveCommand(out var command,out _,out _)&&command!=FollowerCommandType.None)return false;
                if(IsLearningV350TerminalPhysicalRecoveryEligible(bot)||IsTeleportTransactionActive(bot)||context.OverridenState!=null||context.OverridenControlsState!=null||
                    FollowerEnvironment.TryGetImmediateSurvivalThreat(bot,out _)||
                    FollowerStateAuthorityCoordinator.HasCombatMovementClaim(bot,out _))return false;
                if(TryGetActiveProbe(bot,out var probe))
                {
                    if(!IsCapabilityDropDownConsumerPhase(probe)||!IsProbeLifecycleCurrent(probe)||!probe.Lifecycle.Origin.Equals(origin)||!ReferenceEquals(probe.Player,player)||
                        !ReferenceEquals(probe.LifecycleMission,mission)||!ReferenceEquals(probe.LifecycleStep,step)||
                        probe.FrozenTraversalCommitment==null||!TryCaptureCapabilityLandingProofMotionFrontier(
                            probe,probe.FrozenTraversalCommitment,player.Position,requested,out var next,out float ahead,out float budget))return false;
                    axis=GetFlatDirection(next.RawExit-next.RawEntry);
                    observedProbe=probe;Budget=budget;Kind=CapabilityFrontierKind.DropDownBoundary;
                }
                else
                {
                    if(!string.Equals(step.Kind,"Jump",StringComparison.OrdinalIgnoreCase)||!context.IsGrounded||
                        IsRetiredCapabilityJumpPreparation(mission,step))return false;
                    var entry=GetLearningV442JumpAdmissionEntry(step);
                    axis=GetFlatDirection((IsFinite(step.RawExit)?step.RawExit:step.Exit)-(IsFinite(step.RawEntry)?step.RawEntry:step.Entry));
                    if(!IsFinite(entry)||axis.sqrMagnitude<0.5f)return false;
                    var offset=player.Position-entry;offset.y=0f;float forward=Vector3.Dot(offset,axis);
                    if(forward < -RouteMissionBreadcrumbLookAheadDistance||forward>LearningV352JumpTakeoffCorridorMaxForwardOvershoot)return false;
                    float lateral=(offset-axis*forward).magnitude;
                    bool locomotion=(player.CurrentStateName==EPlayerState.Run||player.CurrentStateName==EPlayerState.Sprint)&&context.CanJump;
                    if(!LearningV354ActionProfiles.TryGetValue("Jump",out var profile)||profile==null)return false;
                    float lead=FollowerCapabilityFrontierPolicy.PreparationLead(GetLearningV329PlanarSpeed(player),RollingTraversalMinSpeed,
                        profile.ActuatorLeadSeconds,LearningV354JumpActuatorLeadInitialSeconds,LearningV354JumpActuatorLeadMinSeconds,
                        LearningV354JumpActuatorLeadMaxSeconds,InjectedNativeVaultEntryStagingDistance);
                    Budget=FollowerCapabilityFrontierPolicy.JumpBudget(forward,lead,
                        locomotion,lateral<=LearningV352JumpTakeoffCorridorMaxLateral,
                        Vector3.Dot(GetFlatDirection(context.PlayerRealForward),axis)>=LearningV352JumpTakeoffCorridorMinFacingDot);
                    Kind=CapabilityFrontierKind.JumpPreparation;
                }
                if(axis.sqrMagnitude<0.5f||!IsFinite(Budget))return false;
                snapshot=new CapabilityFrontierSnapshot(Kind,Vector3.Dot(requested,axis),Budget);return true;
            }
            public void RestrictForward(float removed)
            {
                var constrained=requested-axis*removed;
                if(Kind==CapabilityFrontierKind.DropDownBoundary)
                    constrained=requested*FollowerCapabilityHandoffPolicy.PlanarRetention(Vector3.Dot(requested,axis),removed);
                Result=new Vector3(constrained.x,requested.y,constrained.z); // Native gravity is retained exactly.
            }
        }
        private static bool IsRetiredCapabilityJumpPreparation(FollowerRouteMission mission,FollowerRouteMissionStep step)=>
            (FollowerCapabilityActuationPolicy.RetiredPhysicalStrategy(
                mission.CurrentStepIndex,mission.LearningV446StrategyEpochStepIndex,step.EdgeId,
                mission.LearningV446StrategyEpochSemanticEdgeId,mission.LearningV446RetiredPhysicalStrategyEdgeId,
                mission.LearningV446RetiredPhysicalStrategyKind,"Jump"));
        // Native locomotion phase preparation is owned by FollowerCapabilityPhaseHandoffHost.
        private static void TickCapabilityJumpPreparationFrames()
        {
            TickCapabilityPreparationConsumerFrames();
            TickCapabilityClimbIngressFrames();
        }
    }
    internal static class FollowerCapabilityFrontierPatch
    {
        internal static void Apply(Harmony harmony)
        {
            foreach(var type in new[]{typeof(MovementContext),typeof(ClientPlayerMovementContext)}.Where(t=>t!=null))
            {
                var method=AccessTools.DeclaredMethod(type,"ApplyMotion",new[]{typeof(Vector3),typeof(float)});
                if(method!=null)harmony.Patch(method,prefix:new HarmonyMethod(AccessTools.Method(typeof(FollowerCapabilityFrontierPatch),nameof(Prefix))){priority=Priority.Last});
            }
        }
        private static void Prefix(MovementContext __instance,Player ____player,ref Vector3 __0)
        {__0=FollowerTraversalTestRuntime.ConstrainCapabilityMotionFrontier(__instance,____player,__0);}
    }
}
