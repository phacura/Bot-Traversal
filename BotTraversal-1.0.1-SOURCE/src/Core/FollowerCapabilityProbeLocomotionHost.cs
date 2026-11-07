// R3T_ANIMATION_CONTINUITY_EXTENSION_REVIEWED
using System;
using EFT;
namespace BotTraversal.Core
{
    internal static partial class FollowerTraversalTestRuntime
    {
        private static void ApplyProbeLocomotionIntent(
            ProbeRuntime probe, float precisionSpeed, bool allowSprint, bool preserveSprintMode = false,
            bool sameChainDropDownNativeEnvelope = false)
        {
            if (probe?.Bot?.Mover == null)
            {
                return;
            }

            BotOwner bot = probe.Bot;
            bool plannedGait=TryGetCapabilityRouteSprint(bot,probe.RequestedDirection,out bool plannedSprint);
            if(plannedGait){allowSprint=plannedSprint;preserveSprintMode=false;}
            preserveSprintMode = preserveSprintMode || !plannedGait&&ShouldPreserveCapabilityProbeGait(probe);
            bot.Mover.SetBotMoverPause("R1-AF5E5E1E0C00",@bot,false);
            if ((preserveSprintMode))
            {
                // CF29: when the exact native Jump reservation inherits a live EFT Sprint
                // and its Mover is already Sprinting, reissuing even the same speed command
                // at the consumer boundary is not required to preserve movement. Avoid an
                // additional locomotion mode write; retain the already-physical Sprint and
                // forward input. Transition/Run states do not gain jump eligibility here.
                bool cf29ExactReservedNativeSprint = false;
                if ((string.Equals(probe.RequestedTraversalKind, "Jump", StringComparison.OrdinalIgnoreCase)) &&
                    probe.Stage == ProbeStage.RollingApproach &&
                    probe.NativeStrategySelected && !probe.NativeExecutorConsumed &&
                    probe.Player?.CurrentStateName == EPlayerState.Sprint &&
                    bot.Mover.Sprinting && IsActiveProbe(probe) &&
                    TryResolveLearningV376MissionForProbe(probe,
                        out FollowerRouteMission cf29Mission, out int cf29Index) &&
                    cf29Mission?.Steps != null && cf29Index >= 0 &&
                    cf29Index == cf29Mission.CurrentStepIndex &&
                    cf29Index < cf29Mission.Steps.Count)
                {
                    FollowerRouteMissionStep cf29Step = cf29Mission.Steps[cf29Index];
                    long cf29Generation = FollowerMovementWriterAuthority.GetCurrentGeneration(
                        bot, out FollowerMovementWriterAuthority.WriterDomain cf29Owner);
                    cf29ExactReservedNativeSprint =
                        cf29Step != null && cf29Step.EdgeId == probe.LearnedEdgeId &&
                        cf29Step.LearningV429JumpAdmissionReserved &&
                        cf29Owner == FollowerMovementWriterAuthority.WriterDomain.Traversal &&
                        cf29Generation > 0 && cf29Generation == probe.MovementWriterGeneration;
                    if (cf29ExactReservedNativeSprint &&
                        ShouldEmitDiagnostic("cf29ReservedJumpSprintSpeedWriteElided", bot, 0.15f))
                    {
                        WriteEvent("cf29ReservedJumpSprintSpeedWriteElided", bot, new
                        {
                            missionId = cf29Mission.MissionId,
                            edgeId = cf29Step.EdgeId,
                            generation = cf29Generation,
                            state = probe.Player.CurrentStateName.ToString(),
                            existingMoverSprinting = true,
                            traversalSuccessRecorded = false,
                            persistentLearningChanged = false,
                            bodyRelocated = false,
                            action = "inheritAlreadyLiveNativeSprintWithoutRedundantSpeedModeWrite"
                        });
                    }
                }
                if (!cf29ExactReservedNativeSprint)
                {
                    // RC66: RC62 computed a same-chain DropDown speed envelope, but
                    // preserveSprintMode previously replaced it with FULL speed.
                    // Respect the already-computed bound for this exact native
                    // predecessor only. Never mutate the Jump Sprint reservation,
                    // change the successor admission, or stop vertical descent.
                    bot.BotOwnerSetTargetMoveSpeed("R1-595C61E35121",@bot,sameChainDropDownNativeEnvelope
                        ? precisionSpeed
                        : (bot.Mover.Sprinting ? ProbeMomentumFullMoveSpeed
                           : (probe.PreviousRunning ? ProbeMomentumNormalMoveSpeed : precisionSpeed)));
                }
                return;
            }
            if ((probe.PreviousSprinting || plannedGait&&plannedSprint) && allowSprint)
            {
                bot.BotOwnerSetTargetMoveSpeed("R1-1D771E72EF15",@bot,ProbeMomentumFullMoveSpeed);
                if (!bot.Mover.Sprinting)
                {
                    bot.Mover.BotMoverSprint("R1-F6C4DB8BC8CF",@bot,true,false);
                }
                return;
            }

            if (bot.Mover.Sprinting)
            {
                bot.Mover.BotMoverSprint("R1-85ECEC048656",@bot,false,false);
            }

            bot.BotOwnerSetTargetMoveSpeed("R1-86ABCDF0AA52",@bot,probe.PreviousRunning ? ProbeMomentumNormalMoveSpeed : precisionSpeed);
        }
        private static bool ShouldPreserveCapabilityProbeGait(ProbeRuntime probe)
        {
            if(probe?.Bot==null||probe.Player?.MovementContext==null||!IsProbeLifecycleCurrent(probe)||
                !ReferenceEquals(probe.Bot.GetPlayer,probe.Player)||!TryResolveLearningV376MissionForProbe(probe,out var mission,out int index)||
                mission?.Steps==null||TimeExpiredCapabilityPhase(mission)||index!=mission.CurrentStepIndex||index<0||index>=mission.Steps.Count||
                !ReferenceEquals(probe.LifecycleMission,mission)||!ReferenceEquals(probe.LifecycleStep,mission.Steps[index]))return false;
            var context=probe.Player.MovementContext;
            var follower=BossPlayers.GetFollowerByProfileId(probe.Bot.ProfileId);
            bool blocked=follower==null||(follower.TryPeekActiveCommand(out var command,out _,out _)&&command!=FollowerCommandType.None)||
                IsTeleportTransactionActive(probe.Bot)||IsLearningV350TerminalPhysicalRecoveryEligible(probe.Bot)||
                FollowerEnvironment.TryGetImmediateSurvivalThreat(probe.Bot,out _)||
                FollowerStateAuthorityCoordinator.HasCombatMovementClaim(probe.Bot,out _);
            bool jump=string.Equals(probe.RequestedTraversalKind,"Jump",StringComparison.OrdinalIgnoreCase);
            bool phase=probe.Stage==ProbeStage.Egress||probe.Stage==ProbeStage.Verify||probe.Stage==ProbeStage.ExitCommit||
                jump&&probe.Stage==ProbeStage.Landing;
            bool proof=FollowerCapabilityAnimationContinuityPolicy.LandingProofKeepsGait(jump,
                string.Equals(probe.RequestedTraversalKind,"DropDown",StringComparison.OrdinalIgnoreCase),
                jump?probe.JumpAttempted:probe.NativeDropDownAttempted,
                jump?probe.LearningV330NativeJumpQualifiedLandingWitness:probe.NativeDropDownPhysicalLandingAuthority,
                probe.NativeLandingRejected,probe.NativePartialCrossingObserved,probe.LearningV331DropDownLandingForwardHandoffActive);
            long generation=FollowerMovementWriterAuthority.PeekExactGeneration(probe.Bot,out var owner);
            bool current=generation>0&&generation==probe.MovementWriterGeneration&&
                owner==FollowerMovementWriterAuthority.WriterDomain.Traversal;
            return FollowerCapabilityAnimationContinuityPolicy.PreserveProbeGait(current&&IsProbeLifecycleCurrent(probe),
                true,proof,context.IsGrounded,phase,
                context.OverridenState!=null||context.OverridenControlsState!=null,blocked);
        }
        private static float GetCapabilityProbeContinuationSpeed(ProbeRuntime probe,float precisionSpeed)
        {
            return FollowerCapabilityAnimationContinuityPolicy.ContinuationSpeed(ShouldPreserveCapabilityProbeGait(probe),
                probe?.Bot?.Mover?.Sprinting==true,probe?.PreviousRunning==true,
                precisionSpeed,ProbeMomentumNormalMoveSpeed,ProbeMomentumFullMoveSpeed);
        }
    }
}
