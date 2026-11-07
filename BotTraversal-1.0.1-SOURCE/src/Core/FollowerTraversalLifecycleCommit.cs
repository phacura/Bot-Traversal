using System;
using System.Linq;
using EFT;
using UnityEngine;
using Life = BotTraversal.Core.FollowerTraversalLifecycle;

namespace BotTraversal.Core
{
    internal static partial class FollowerTraversalTestRuntime
    {
        private enum RouteDebtEvidence { PhysicalPass, CursorFloor, ProbeCompletion, TeleportRecovery, RouteSuperseded, PolicySuppression }

        private static Life.Identity LifecycleIdentity(FollowerRouteMission mission,
            FollowerRouteMissionStep step, int index, long writer)
        {
            return new Life.Identity(mission?.LifecycleBotId, mission?.LifecycleRaid ?? 0,
                mission?.MissionId ?? 0, index, step?.EpisodeId ?? 0, step?.EdgeId ?? 0,
                step?.ChainId ?? 0, step?.Lifecycle.GeometryVersion ?? 0, writer);
        }

        private readonly struct RouteDebtObservation
        {
            internal readonly FollowerRouteMission Mission;
            internal readonly FollowerRouteMissionStep Step;
            internal readonly Life.Identity Origin;
            internal readonly int Index;
            internal readonly RouteDebtObservation[] Span;
            internal RouteDebtObservation(FollowerRouteMission mission,FollowerRouteMissionStep step,
                Life.Identity origin,int index,RouteDebtObservation[] span=null)
            { Mission=mission;Step=step;Origin=origin;Index=index;Span=span; }
        }
        private static RouteDebtObservation CaptureRouteDebtObservation(BotOwner bot,FollowerRouteMission mission,
            FollowerRouteMissionStep expected,bool range=false)
        {
            if (bot==null || mission?.Steps==null || mission.Lifecycle.Retired || mission.CurrentStepIndex<0 ||
                mission.CurrentStepIndex>=mission.Steps.Count) return default(RouteDebtObservation);
            int first=mission.CurrentStepIndex;
            long writer=FollowerMovementWriterAuthority.PeekExactGeneration(bot,out _);
            RouteDebtObservation One(int index)
            {
                var step=mission.Steps[index];
                // An existing probe carries its immutable birth ticket, never a
                // substitute ticket from refreshed geometry or a newer owner.
                var origin=step?.Lifecycle.Probe?.Origin ?? LifecycleIdentity(mission,step,index,writer);
                return new RouteDebtObservation(mission,step,origin,index);
            }
            if (!range) return ReferenceEquals(expected,mission.Steps[first]) ? One(first) : default(RouteDebtObservation);
            var span=new RouteDebtObservation[mission.Steps.Count-first];
            for(int i=0;i<span.Length;i++)span[i]=One(first+i);
            return new RouteDebtObservation(mission,span[0].Step,span[0].Origin,first,span);
        }

        // The only semantic Mission cursor advance. Source geometry validators
        // supply evidence; they cannot mutate cursor or resolve debt themselves.
        private static bool TryAdvanceRouteMission(BotOwner bot, FollowerRouteMission mission,
            int targetIndex, RouteDebtEvidence evidence, string source, RouteDebtObservation observation)
        {
            if (mission?.Steps==null || mission.Lifecycle.Retired || targetIndex<mission.CurrentStepIndex ||
                targetIndex>mission.Steps.Count || bot==null || mission.LifecycleBotId!=bot.ProfileId ||
                mission.LifecycleRaid!=raidGeneration || !ReferenceEquals(observation.Mission,mission) ||
                observation.Index!=mission.CurrentStepIndex) return false;
            while (mission.CurrentStepIndex<targetIndex)
            {
                int index=mission.CurrentStepIndex;
                var step=mission.Steps[index];
                int offset=index-observation.Index;
                if (offset<0 || (observation.Span==null && offset!=0) || (observation.Span!=null && offset>=observation.Span.Length)) return false;
                var observed=observation.Span==null ? observation : observation.Span[offset];
                if (!ReferenceEquals(observed.Step,step) || !ReferenceEquals(observed.Mission,mission)) return false;
                if (step==null) { if (!mission.Lifecycle.SkipEmpty(index)) return false; continue; }
                var action=GetLearningV354ActionProfile(step.Kind);
                bool actuator=action!=null && action.RequiresActuator;
                // Chronological floor is never an actuator landing certificate.
                bool qualified=evidence!=RouteDebtEvidence.CursorFloor||CanConsumeCapabilityCursorFloor(bot,step);
                if(evidence==RouteDebtEvidence.PhysicalPass&&IsLearningV369PhysicalCorridorStep(step))
                    qualified=IsFollowerRouteMissionStepPhysicallyPassed(bot,step,out _,out _);
                Life.Resolution resolution=evidence==RouteDebtEvidence.PolicySuppression ? Life.Resolution.Suppressed :
                    evidence==RouteDebtEvidence.TeleportRecovery ? Life.Resolution.TeleportBypass :
                    evidence==RouteDebtEvidence.RouteSuperseded ? Life.Resolution.Cancelled : Life.Resolution.Subsumed;
                var probe=step.Lifecycle.Probe;
                if (evidence==RouteDebtEvidence.ProbeCompletion)
                {
                    qualified=probe!=null && probe.CompletionPublished;
                    resolution=Life.Resolution.Executed;
                }
                if (evidence==RouteDebtEvidence.TeleportRecovery && probe!=null && !probe.CompletionPublished)
                    probe.PublishCompletion(LifecycleIdentity(mission,step,index,probe.Origin.Writer),step.Lifecycle,Life.ActionOutcome.Teleport);
                long writer=probe?.Origin.Writer ?? FollowerMovementWriterAuthority.PeekExactGeneration(bot,out _);
                var identity=LifecycleIdentity(mission,step,index,writer);
                var result=mission.Lifecycle.Resolve(step.Lifecycle,identity,step.Lifecycle,
                    observed.Origin,resolution,qualified,actuator);
                if (result!=Life.CommitResult.Applied)
                {
                    if (ShouldEmitDiagnostic("lifecycleDebtRejected:"+source,bot,1f))
                        WriteEvent("routeLifecycleDebtRetained",bot,new { source,missionId=mission.MissionId,
                            stepIndex=index,episodeId=step.EpisodeId,edgeId=step.EdgeId,evidence=evidence.ToString(),
                            geometry=step.Lifecycle.GeometryVersion,qualified,phase=step.Lifecycle.Status.ToString(),
                            nativeSuccessRecorded=false,routeStepAdvanced=false });
                    return false;
                }
                if(resolution==Life.Resolution.Executed||resolution==Life.Resolution.Subsumed||resolution==Life.Resolution.TeleportBypass)
                {
                    CapabilityResolvedDebt.Mark(raidGeneration,bot.ProfileId,step.EpisodeId,step.EdgeId);
                    CapabilityResolvedDebt.MarkWriter(raidGeneration,bot.ProfileId,writer);
                    CommitCapabilityResolvedNavigation(bot,mission,step);
                }
                // This clock belongs to the resolved step. Only an applied identity advance
                // may clear it; polling the successor must retain its own attempt backoff.
                CommitCapabilityMoveRetry(mission,FollowerCapabilityRetryPolicy.Change.Reset,0f);
                PruneCapabilityResolvedCommitment(bot);
                if(mission.CurrentStepIndex>=mission.Steps.Count)global::BotTraversal.LegacyOriginalPITFollowBridge.CancelDelivery(bot.ProfileId);
                CapabilityPortalQueue.Complete(bot.ProfileId,mission.MissionId,index,UnityEngine.Time.time);
                if(mission.FailureSequence!=null)mission.FailureSequence=null;
                if(IsCapabilityFullRouteReplay(mission))mission.RouteRetry.ProgressFloor=Math.Max(mission.RouteRetry.ProgressFloor,step.EndBreadcrumbSequence);
                WriteEvent("routeLifecycleStepResolved",bot,new { source,missionId=mission.MissionId,
                    stepIndex=index,episodeId=step.EpisodeId,edgeId=step.EdgeId,chainId=step.ChainId,
                    resolution=resolution.ToString(),geometry=identity.Geometry,writer=identity.Writer,
                    nativeSuccessRecordedByThisTransition=false,currentStepIndex=mission.CurrentStepIndex });
            }
            return true;
        }

        private static bool BindProbeLifecycle(ProbeRuntime probe)
        {
            if (probe?.Bot==null || probe.Generation!=raidGeneration) return false;
            if (probe.Lifecycle!=null) return !probe.Lifecycle.Retired;
            string id=probe.Bot.ProfileId;
            if (ActiveProbes.TryGetValue(id,out var other) && !ReferenceEquals(other,probe)) return false;
            var commitment=probe.FrozenTraversalCommitment;
            if (commitment!=null && commitment.RouteMissionId>0)
            {
                if (!FollowerRouteMissions.TryGetValue(id,out var mission) ||
                    mission.MissionId!=commitment.RouteMissionId || mission.Lifecycle.Retired ||
                    mission.CurrentStepIndex!=commitment.RouteMissionStepIndex ||
                    mission.CurrentStepIndex<0 || mission.CurrentStepIndex>=mission.Steps.Count) return false;
                var step=mission.Steps[mission.CurrentStepIndex];
                if (step==null || step.EpisodeId!=probe.TraversalEpisodeId || step.EdgeId!=probe.LearnedEdgeId) return false;
                var origin=LifecycleIdentity(mission,step,mission.CurrentStepIndex,probe.MovementWriterGeneration);
                if (!origin.Valid || origin.Writer<=0) return false;
                CaptureCapabilityRouteRetryPlan(mission);
                var state=new Life.ProbeState(origin,step.Lifecycle);
                if (!step.Lifecycle.Attach(state)) return false;
                probe.LifecycleMission=mission;probe.LifecycleStep=step;probe.Lifecycle=state;
            }
            else probe.Lifecycle=new Life.ProbeState(default(Life.Identity),null);
            return true;
        }

        private static bool IsProbeLifecycleCurrent(ProbeRuntime probe, bool allowResolved=false)
        {
            if (probe?.Lifecycle==null || probe.Lifecycle.Retired || !IsActiveProbe(probe)) return false;
            if (probe.LifecycleMission==null) return true; // Explicit standalone diagnostics.
            var mission=probe.LifecycleMission;var step=probe.LifecycleStep;
            if (!FollowerRouteMissions.TryGetValue(probe.Bot.ProfileId,out var live) ||
                !ReferenceEquals(live,mission) || mission.Lifecycle.Retired || step==null ||
                mission.LifecycleRaid!=raidGeneration || probe.Lifecycle.Origin.Geometry!=step.Lifecycle.GeometryVersion)
                return false;
            int index=probe.Lifecycle.Origin.Step;
            if (index<0 || index>=mission.Steps.Count || !ReferenceEquals(mission.Steps[index],step)) return false;
            if (allowResolved && probe.Lifecycle.CompletionPublished && !step.Lifecycle.DebtOutstanding)
                return mission.CurrentStepIndex>index;
            return index==mission.CurrentStepIndex && probe.Lifecycle.Matches(
                LifecycleIdentity(mission,step,index,probe.MovementWriterGeneration),step.Lifecycle);
        }

        private static bool PublishProbeLifecycleCompletion(ProbeRuntime probe, int episode, int edge)
        {
            if (!IsProbeLifecycleCurrent(probe,true)) return false;
            if (probe.LifecycleMission==null) return true;
            if (episode!=probe.Lifecycle.Origin.Episode || edge!=probe.Lifecycle.Origin.Edge)
                return false; // Composite/coalesced debt requires independent physical proof.
            var outcome=probe.TeleportFallbackUsed || probe.TraversalTeleportAttempted ? Life.ActionOutcome.Teleport :
                !probe.LearningV341ForcedEgressPhysicalWriterUsed && IsLearningV3RetainableNativeSuccess(probe,GetTraversalCompletionMode(probe,"lifecycleCompletion"))
                ? Life.ActionOutcome.NativePhysical : Life.ActionOutcome.NonNative;
            if (probe.Lifecycle.CompletionPublished) return false;
            return probe.Lifecycle.PublishCompletion(LifecycleIdentity(probe.LifecycleMission,
                probe.LifecycleStep,probe.Lifecycle.Origin.Step,probe.MovementWriterGeneration),
                probe.LifecycleStep.Lifecycle,outcome)==Life.CommitResult.Applied;
        }

        private static void CaptureLifecycleRecovery(BotOwner bot,FollowerMovementRecoveryState state,
            FollowerRouteMission mission,FollowerRouteMissionStep step)
        {
            if (state.LifecycleRecoveryOrigin.Valid) return; // No origin renewal on repeated stalls.
            state.LifecycleRecoveryOrigin=LifecycleIdentity(mission,step,mission.CurrentStepIndex,
                FollowerMovementWriterAuthority.PeekExactGeneration(bot,out _));
            state.LifecycleRecoveryMission=mission;state.LifecycleRecoveryStep=step;
            step.Lifecycle.Recover();
        }
        private static bool MatchLifecycleRecovery(FollowerMovementRecoveryState state,FollowerRouteMission mission)
        {
            if (!ReferenceEquals(state?.LifecycleRecoveryMission,mission) || mission==null || mission.Lifecycle.Retired ||
                mission.CurrentStepIndex<0 || mission.CurrentStepIndex>=mission.Steps.Count) return false;
            var step=mission.Steps[mission.CurrentStepIndex];
            return ReferenceEquals(step,state.LifecycleRecoveryStep) && step!=null && step.Lifecycle.DebtOutstanding &&
                state.LifecycleRecoveryOrigin.Equals(LifecycleIdentity(mission,step,mission.CurrentStepIndex,state.LifecycleRecoveryOrigin.Writer));
        }

        private static bool RegisterLifecycleMission(BotOwner bot,FollowerRouteMission mission)
        {
            if (bot==null || mission==null || mission.Lifecycle.Retired || mission.LifecycleRaid!=raidGeneration ||
                mission.LifecycleBotId!=bot.ProfileId || FollowerRouteMissions.ContainsKey(bot.ProfileId)) return false;
            FollowerRouteMissions[bot.ProfileId]=mission;return true;
        }
        private static void InstallLifecycleProbe(ProbeRuntime probe)
        {
            ActiveProbes[probe.Bot.ProfileId]=probe;
        }
        private static void RetireLifecycleProbe(ProbeRuntime probe) { probe?.Lifecycle?.Retire(); }
        private static bool UninstallLifecycleProbe(ProbeRuntime probe)
        {
            if (probe?.Bot==null || !ActiveProbes.TryGetValue(probe.Bot.ProfileId,out var current) ||
                !ReferenceEquals(current,probe)) return false;
            RetireLifecycleProbe(probe);return ActiveProbes.Remove(probe.Bot.ProfileId);
        }
        private static void ClearLifecycleProbes()
        {
            foreach(var probe in ActiveProbes.Values) RetireLifecycleProbe(probe);
            ActiveProbes.Clear();
        }

        private static bool IsLifecycleCompletionForStep(BotOwner bot,RecentTraversalCompletion receipt,FollowerRouteMissionStep step)
        {
            if (bot==null || receipt==null || step==null || !receipt.LifecycleOrigin.Valid ||
                !ReferenceEquals(receipt.LifecycleStep,step)) return false;
            var origin=receipt.LifecycleOrigin;
            return origin.Bot==bot.ProfileId && origin.Raid==raidGeneration &&
                FollowerRouteMissions.TryGetValue(bot.ProfileId,out var mission) && mission.MissionId==origin.Mission &&
                origin.Step>=0 && origin.Step<mission.Steps.Count && ReferenceEquals(mission.Steps[origin.Step],step) &&
                origin.Episode==step.EpisodeId && origin.Edge==step.EdgeId &&
                origin.Chain==step.ChainId && origin.Geometry==step.Lifecycle.GeometryVersion;
        }

        private static bool RemoveLifecycleMissionForProbe(ProbeRuntime probe,string source)
        {
            if (probe?.LifecycleMission==null || !IsActiveProbe(probe)) return false;
            return RemoveLifecycleMission(probe.Bot.ProfileId,source,probe.LifecycleMission);
        }
        private static bool RemoveLifecycleMission(string profileId, string source, FollowerRouteMission expected=null)
        {
            if (string.IsNullOrEmpty(profileId) || !FollowerRouteMissions.TryGetValue(profileId,out var mission) ||
                (expected!=null && !ReferenceEquals(expected,mission))) return false;
            if(!mission.Lifecycle.Retired&&mission.FailureSequence!=null&&source!="manualAbort")return false;
            if(!mission.Lifecycle.Retired&&IsCapabilityRouteRetryReturning(mission)&&source!="manualAbort")return false;
            if(!mission.Lifecycle.Retired&&IsCapabilityFullRouteReplay(mission)&&!CanFinalizeLifecycleMission(mission)&&source!="manualAbort")return false;
            mission.Lifecycle.Retire(mission.CurrentStepIndex==mission.Steps.Count && !mission.Steps.Any(s=>s?.Lifecycle.DebtOutstanding==true));
            // Retiring the route never grants physical completion to remaining debt.
            return FollowerRouteMissions.Remove(profileId);
        }
        private static void ClearLifecycleMissions(string source)
        {
            foreach(var mission in FollowerRouteMissions.Values) mission?.Lifecycle.Retire();
            FollowerRouteMissions.Clear();
            CapabilityPortalQueue.Clear();
        }

        private static bool RemoveLifecycleMissionStep(FollowerRouteMission mission,int index,string source)
        {
            if(mission?.FailureSequence!=null||IsCapabilityFullRouteReplay(mission)||IsCapabilityRouteRetryReturning(mission))return false;
            if (mission?.Steps==null || index<0 || index>=mission.Steps.Count || mission.Lifecycle.Retired) return false;
            var step=mission.Steps[index];
            if ((index<=mission.CurrentStepIndex && HasExecutingLifecycleProbe(mission)) ||
                (step?.Lifecycle.Probe!=null && !step.Lifecycle.Probe.Retired)) return false;
            bool outstanding=step?.Lifecycle.DebtOutstanding==true;
            if (outstanding) step.Lifecycle.Resolve(Life.Resolution.Suppressed);
            mission.Steps.RemoveAt(index);return mission.Lifecycle.RemovedAt(index,outstanding);
        }
        private static bool ReplaceLifecycleMissionStep(FollowerRouteMission mission,int index,FollowerRouteMissionStep value)
        {
            if(mission?.FailureSequence!=null||IsCapabilityFullRouteReplay(mission)||IsCapabilityRouteRetryReturning(mission))return false;
            if (mission?.Steps==null || value==null || index<mission.CurrentStepIndex || index>=mission.Steps.Count ||
                mission.Lifecycle.Retired || mission.Steps[index]?.Lifecycle.Probe!=null) return false;
            mission.Steps[index]=value;return true;
        }
        private static bool HasExecutingLifecycleProbe(FollowerRouteMission mission) =>
            mission?.Steps!=null && mission.Steps.Any(s=>s?.Lifecycle.Probe!=null &&
                !s.Lifecycle.Probe.Retired && !s.Lifecycle.Probe.CompletionPublished);
        private static bool SetLifecycleMissionSteps(FollowerRouteMission mission,System.Collections.Generic.List<FollowerRouteMissionStep> steps)
        {
            if(mission?.FailureSequence!=null||IsCapabilityFullRouteReplay(mission)||IsCapabilityRouteRetryReturning(mission))return false;
            if (mission==null || steps==null || mission.Lifecycle.Retired || HasExecutingLifecycleProbe(mission)) return false;
            var prefix=mission.Steps.Take(mission.CurrentStepIndex).ToList();
            mission.Steps=prefix.Concat(steps.Where(s=>!prefix.Contains(s))).ToList();return true;
        }
        private static bool AddLifecycleMissionStep(FollowerRouteMission mission,FollowerRouteMissionStep step)
        {
            if(mission?.FailureSequence!=null||IsCapabilityFullRouteReplay(mission)||IsCapabilityRouteRetryReturning(mission))return false;
            if (mission?.Steps==null || step==null || mission.Lifecycle.Retired || HasExecutingLifecycleProbe(mission)) return false;
            var pending=mission.Steps.Skip(mission.CurrentStepIndex).ToList();
            return TryAddCanonicalRouteMissionStep(pending,step) &&
                SetLifecycleMissionSteps(mission,mission.Steps.Take(mission.CurrentStepIndex).Concat(pending).ToList());
        }
        private static bool InsertLifecycleMissionStep(FollowerRouteMission mission,int index,FollowerRouteMissionStep step)
        {
            if(mission?.FailureSequence!=null||IsCapabilityFullRouteReplay(mission)||IsCapabilityRouteRetryReturning(mission))return false;
            if (mission?.Steps==null || step==null || index<mission.CurrentStepIndex || index>mission.Steps.Count ||
                mission.Lifecycle.Retired || HasExecutingLifecycleProbe(mission)) return false;
            mission.Steps.Insert(index,step);return true;
        }
        private static bool SwapLifecycleMissionSteps(FollowerRouteMission mission,int first,int second)
        {
            if(mission?.FailureSequence!=null||IsCapabilityFullRouteReplay(mission)||IsCapabilityRouteRetryReturning(mission))return false;
            if (mission?.Steps==null || first<mission.CurrentStepIndex || second<mission.CurrentStepIndex ||
                first>=mission.Steps.Count || second>=mission.Steps.Count || mission.Lifecycle.Retired || HasExecutingLifecycleProbe(mission)) return false;
            var old=mission.Steps[first];mission.Steps[first]=mission.Steps[second];mission.Steps[second]=old;return true;
        }

        private static bool TransitionProbeStage(ProbeRuntime probe, ProbeStage next)
        {
            if (probe==null || (probe.Lifecycle!=null && !IsProbeLifecycleCurrent(probe))) return false;
            probe.ApplyLifecycleStage(next);return true;
        }

        private static bool CanFinalizeLifecycleMission(FollowerRouteMission mission)
        {
            if (mission?.Steps==null) return false;
            bool probeRunning=mission.Steps.Any(s=>s?.Lifecycle.Probe!=null &&
                !s.Lifecycle.Probe.Retired && !s.Lifecycle.Probe.CompletionPublished);
            bool debt=mission.Steps.Any(s=>s?.Lifecycle.DebtOutstanding==true);
            return mission.Lifecycle.CanHandoffToFollow(mission.Steps.Count,probeRunning,debt);
        }
    }
}
