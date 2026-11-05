using System;
using System.Globalization;
using System.Linq;
using UnityEngine.AI;
using EFT;
using HarmonyLib;
using UnityEngine;
namespace BotTraversal.Core
{
    internal static partial class FollowerTraversalTestRuntime
    {
        private static bool IsCF68NextDropDownEntryFrontierProtected(
            ProbeRuntime probe, LearnedTraversalCommitment commitment, Vector3 position,
            out FollowerRouteMissionStep successor, out float entryAhead,
            out float entryLateral)
        {
            successor = null;
            entryAhead = 0f;
            entryLateral = 0f;
            if (probe?.Bot == null || commitment == null ||
                !probe.CF67DropDownConsumerEverAccepted ||
                !IsFinite(position) ||
                !FollowerRouteMissions.TryGetValue(probe.Bot.ProfileId ?? string.Empty,
                    out FollowerRouteMission mission) || mission?.Steps == null ||
                mission.CurrentStepIndex < 0 ||
                mission.CurrentStepIndex + 1 >= mission.Steps.Count ||
                !IsRouteMissionSameChainImmediateSuccessor(
                    mission, mission.CurrentStepIndex + 1))
            {
                return false;
            }
            FollowerRouteMissionStep current = mission.Steps[mission.CurrentStepIndex];
            FollowerRouteMissionStep next = mission.Steps[mission.CurrentStepIndex + 1];
            if (current == null || next == null ||
                current.EdgeId != commitment.EdgeId ||
                !string.Equals(next.Kind, "DropDown", StringComparison.OrdinalIgnoreCase) ||
                next.AttemptStartedObserved ||
                !IsFinite(next.RawEntry) || !IsFinite(next.RawExit))
            {
                return false;
            }
            Vector3 axis = GetFlatDirection(next.RawExit - next.RawEntry);
            if (axis.sqrMagnitude < 0.5f)
            {
                return false;
            }
            Vector3 delta = next.RawEntry - position;
            float vertical = Mathf.Abs(delta.y);
            delta.y = 0f;
            entryAhead = Vector3.Dot(delta, axis);
            entryLateral = (delta - axis * entryAhead).magnitude;
            // CF72: the first exact CF68 portal encounter establishes a
            // persistent *negative* authority: the predecessor may not extend
            // its V334 landing mover across this unstarted successor. On CF71
            // the guard was true at 325.829 but false by 325.959, after the
            // body left a 0.90m instantaneous observation sphere. V334 then
            // refreshed past the portal, leaving the next DropDown behind.
            // The latch belongs to this same live predecessor probe, mission,
            // successor edge/episode/chain; it does not imply Native success.
            bool cf72SameSuccessor =
                probe.CF72NextDropDownFrontierMissionId == mission.MissionId &&
                probe.CF72NextDropDownFrontierEdgeId == next.EdgeId &&
                probe.CF72NextDropDownFrontierEpisodeId == next.EpisodeId &&
                probe.CF72NextDropDownFrontierChainId == next.ChainId &&
                probe.CF72NextDropDownFrontierEdgeId > 0 &&
                probe.CF72NextDropDownFrontierEpisodeId > 0 &&
                probe.CF72NextDropDownFrontierChainId > 0;
            if (!cf72SameSuccessor &&
                (vertical > 0.90f ||
                 entryAhead < -0.20f || entryAhead > 0.80f ||
                 entryLateral > NativeDropDownEntryTriggerDistance ||
                 delta.magnitude > 0.90f))
            {
                return false;
            }
            if (!cf72SameSuccessor)
            {
                // A non-identified successor must never be latched. CF68's
                // legacy instantaneous protection still applies if identity
                // metadata is missing, but cannot leak to other attempts.
                if (mission.MissionId > 0 && next.EdgeId > 0 &&
                    next.EpisodeId > 0 && next.ChainId > 0 &&
                    current.ChainId == next.ChainId &&
                    current.EpisodeId == probe.TraversalEpisodeId)
                {
                    probe.CF72NextDropDownFrontierMissionId = mission.MissionId;
                    probe.CF72NextDropDownFrontierEdgeId = next.EdgeId;
                    probe.CF72NextDropDownFrontierEpisodeId = next.EpisodeId;
                    probe.CF72NextDropDownFrontierChainId = next.ChainId;
                }
            }
            successor = next;
            return true;
        }

        private static bool TryGetV2RC42LandingProofSuccessorFrontier(
            ProbeRuntime probe, LearnedTraversalCommitment commitment,
            Vector3 position, out Vector3 frontier, out FollowerRouteMissionStep successor)
        {
            frontier = default;
            successor = null;
            if (probe?.Bot == null || commitment == null ||
                !TryResolveLearningV376MissionForProbe(
                    probe, out FollowerRouteMission mission, out int probeStepIndex) ||
                mission?.Steps == null || mission.CurrentStepIndex != probeStepIndex ||
                probeStepIndex < 0 || probeStepIndex + 1 >= mission.Steps.Count ||
                !IsRouteMissionSameChainImmediateSuccessor(mission, probeStepIndex + 1))
            {
                return false;
            }
            FollowerRouteMissionStep current = mission.Steps[probeStepIndex];
            FollowerRouteMissionStep next = mission.Steps[probeStepIndex + 1];
            if (current == null || next == null ||
                current.EdgeId != commitment.EdgeId ||
                current.EpisodeId != probe.TraversalEpisodeId ||
                !string.Equals(next.Kind, "DropDown", StringComparison.OrdinalIgnoreCase) ||
                next.AttemptStartedObserved || !IsFinite(next.RawEntry) ||
                !IsFinite(next.RawExit) || !IsFinite(position))
            {
                return false;
            }
            Vector3 successorAxis = GetFlatDirection(next.RawExit - next.RawEntry);
            if (successorAxis.sqrMagnitude < 0.5f)
            {
                return false;
            }
            Vector3 delta = next.RawEntry - position;
            float verticalError = Mathf.Abs(delta.y);
            delta.y = 0f;
            float forward = Vector3.Dot(delta, successorAxis);
            float lateral = (delta - successorAxis * forward).magnitude;
            // RC69: the predecessor's FIRST grounded landing candidate can be
            // ~1.47m before the next ledge (RC68 failed Edge78). The legacy
            // <=0.80m frontier lookup then installs a generic ~1.2m V331
            // breadcrumb and freezes its refresh at CF68, leaving the successor
            // 0.36m SHORT of the ledge with no inherited momentum. Look farther
            // ONLY for a proven same-chain grounded candidate and accept ONLY a
            // strict near-side path ending within 0.25m of the real entry.
            // CF68/CF72's independent overshoot protection stays unchanged.
            bool rc69EarlyAdjacentLandingFrontier =
                probe.NativeDropDownLandingStableSince >= 0f &&
                probe.Player?.MovementContext?.IsGrounded == true &&
                forward > 0.80f && forward <= 1.85f &&
                lateral <= 0.22f && verticalError <= 0.60f &&
                delta.magnitude <= 1.90f;
            if (!rc69EarlyAdjacentLandingFrontier &&
                (verticalError > 0.90f || forward < -0.35f || forward > 0.80f ||
                 lateral > NativeDropDownEntryTriggerDistance || delta.magnitude > 0.90f))
                return false;
            // RC44: at the FIRST grounded landing sample, a linked successor
            // can already be ON its entry plane. RC42/43 required its entry to
            // be 5 cm ahead, so they rejected this exact case and let V331 run
            // another ~1 m beyond the second DropDown before CF68 could latch.
            // Reuse CF68's existing [-0.20, +0.80] encounter envelope. An
            // entry-plane body needs an actual native near-zero path consumer,
            // NOT a fictional approach waypoint beyond the upcoming ledge.
            // No successor Native Attempt/success is produced by this branch.
            if (forward <= 0.05f)
            {
                // RC45: first contact in the recorded failure is already 0.206 m
                // beyond the *overlapping* next entry, just 0.006 m outside
                // CF68's historical -0.20 encounter limit. A forward V331
                // consumer then overflew an additional 1.98 m and landed below
                // the next portal. The body must physically return to the
                // same-platform near side by a short STRICT navmesh path, not
                // move forward or silently count the second DropDown as done.
                // This correction is local (<0.35 m behind entry), only during
                // the still-live predecessor proof; never a teleport or rewind
                // of route chronology. If unreachable, fail closed.
                bool rc45SlightlyCrossedEntry = forward < -0.20f;
                Vector3 rc45Candidate = rc45SlightlyCrossedEntry
                    ? next.RawEntry - successorAxis * 0.10f
                    : position;
                if (!NavMesh.SamplePosition(position, out NavMeshHit bodyHit,
                        0.35f, NavMesh.AllAreas) ||
                    GetPlanarDistance(position, bodyHit.position) > 0.20f ||
                    Mathf.Abs(position.y - bodyHit.position.y) > UnifiedTraversalVerifyMaxNavVertical ||
                    !NavMesh.SamplePosition(rc45Candidate, out NavMeshHit atEntryHit,
                        0.35f, NavMesh.AllAreas) ||
                    !IsFinite(atEntryHit.position) ||
                    GetPlanarDistance(rc45Candidate, atEntryHit.position) > 0.20f ||
                    Mathf.Abs(rc45Candidate.y - atEntryHit.position.y) > 0.30f)
                    return false;
                if (rc45SlightlyCrossedEntry)
                {
                    Vector3 check = atEntryHit.position - next.RawEntry;
                    check.y = 0f;
                    if (Vector3.Dot(check, successorAxis) > -0.04f)
                        return false;
                    StrictPathResult rc45ReturnPath = CalculateStrictPath(position, atEntryHit.position);
                    if (rc45ReturnPath?.StrictComplete != true ||
                        rc45ReturnPath.PathLength > 0.65f ||
                        rc45ReturnPath.PathLength < 0.15f)
                        return false;
                }
                frontier = atEntryHit.position;
                successor = next;
                return true;
            }
            Vector3 nearSide = next.RawEntry - successorAxis * 0.12f;
            // RC43: RC42's single projected point was often off NavMesh at a
            // ledge, so the original full-speed V331 consumer ran past the NEXT
            // DropDown despite a valid current-body approach corridor. Search
            // only the already-proven near side between the body and the same
            // chain's immediate entry. Every candidate is navmesh sampled and
            // independently StrictComplete. Never sample on the far side, turn
            // a path into native proof, or read a future mission/chain.
            Vector3[] rc43Candidates = rc69EarlyAdjacentLandingFrontier
                ? new Vector3[]
                {
                    nearSide,
                    Vector3.Lerp(position, next.RawEntry, 0.90f),
                    Vector3.Lerp(position, next.RawEntry, 0.84f)
                }
                : new Vector3[]
                {
                    nearSide,
                    Vector3.Lerp(position, next.RawEntry, 0.55f),
                    Vector3.Lerp(position, next.RawEntry, 0.40f)
                };
            int rc51SampleReject = 0, rc51PathReject = 0;
            int rc51PlaneReject = 0, rc51NoAdvanceReject = 0;
            for (int rc43Index = 0; rc43Index < rc43Candidates.Length; rc43Index++)
            {
                Vector3 rc43Candidate = rc43Candidates[rc43Index];
                if (!NavMesh.SamplePosition(
                        rc43Candidate, out NavMeshHit nearSideHit, 0.35f, NavMesh.AllAreas) ||
                    !IsFinite(nearSideHit.position) ||
                    GetPlanarDistance(rc43Candidate, nearSideHit.position) > 0.20f ||
                    GetPlanarDistance(position, nearSideHit.position) < 0.20f)
                {
                    rc51SampleReject++;
                    continue;
                }
                StrictPathResult safePath = CalculateStrictPath(position, nearSideHit.position);
                if (safePath?.StrictComplete != true)
                {
                    rc51PathReject++;
                    continue;
                }
                // No sampled candidate may advance beyond the immediate
                // successor entry plane, even when a nearby NavMesh polygon is
                // on the opposite side of the ledge.
                Vector3 actual = nearSideHit.position - next.RawEntry;
                actual.y = 0f;
                float candidateForward = Vector3.Dot(actual, successorAxis);
                // The expanded probe must land at the ledge, NOT choose a
                // mid-corridor breadcrumb which still leaves Native stalled.
                if (rc69EarlyAdjacentLandingFrontier && candidateForward < -0.25f)
                {
                    rc51NoAdvanceReject++;
                    continue;
                }
                if (candidateForward > 0.02f)
                {
                    rc51PlaneReject++;
                    continue;
                }
                Vector3 rc43Travel = nearSideHit.position - position;
                rc43Travel.y = 0f;
                if (Vector3.Dot(rc43Travel, successorAxis) <= 0.05f)
                {
                    rc51NoAdvanceReject++;
                    continue;
                }
                frontier = nearSideHit.position;
                successor = next;
                if (rc69EarlyAdjacentLandingFrontier &&
                    ShouldEmitDiagnostic("v2rc69GroundedAdjacentNearSidePath:" +
                        commitment.EdgeId.ToString(CultureInfo.InvariantCulture), probe.Bot, 0.20f))
                    WriteEvent("v2rc69GroundedAdjacentNearSidePath", probe.Bot, new
                    {
                        predecessorEdgeId = commitment.EdgeId,
                        successorEdgeId = next.EdgeId,
                        entryAhead = forward, entryLateral = lateral,
                        candidateForward, body = Vec(position),
                        frontier = Vec(frontier),
                        nativeSuccessRecorded = false, bodyRelocated = false,
                        action = "currentGroundedSameChainStrictPathApproachesLedgeWithoutCrossingEntryPlane"
                    });
                return true;
            }
            // RC51: the RC50 failure had a CF68-valid immediate successor
            // but could not select a strict NavMesh near-side landing target.
            // Capture the specific reason; no braking, invented path or native
            // success is authorized when these guards fail.
            if (ShouldEmitDiagnostic("v2rc51LandingFrontierRejected:" +
                    commitment.EdgeId.ToString(CultureInfo.InvariantCulture),
                    probe.Bot, 0.40f))
                WriteEvent("v2rc51LandingFrontierRejected", probe.Bot, new
                {
                    predecessorEdgeId = commitment.EdgeId,
                    successorEdgeId = next.EdgeId,
                    landingBody = Vec(position), successorRawEntry = Vec(next.RawEntry),
                    entryAhead = forward, entryLateral = lateral, verticalError,
                    rejectedNavSample = rc51SampleReject,
                    rejectedIncompletePath = rc51PathReject,
                    rejectedFarSide = rc51PlaneReject,
                    rejectedNoProgress = rc51NoAdvanceReject,
                    bodyRelocated = false, nativeSuccessRecorded = false,
                    action = "diagnoseFailClosedNearSideSuccessorSelectionNoPhysicalOverride"
                });
            return false;
        }

        private static bool TryRefreshLearningV334DropDownLandingForwardLookahead(
            ProbeRuntime probe,
            LearnedTraversalCommitment commitment,
            Vector3 position,
            Vector3 authoredDirection,
            Vector3 downstreamTarget,
            string downstreamSource)
        {
            if(probe?.LifecycleMission==null||probe.LifecycleStep==null||
                !TryEnterCapabilityRoutePhysicalScope(probe.Bot,probe.Player,probe.LifecycleMission,probe.LifecycleStep,
                    probe.MovementWriterGeneration,probe,null,CapabilityContinuationPhase.NativeLanding,
                    out var continuationScope,out _))return false;
            using(continuationScope)
            {
            if ((probe == null) || commitment == null ||
                !IsActiveProbe(probe) || probe.Bot?.Mover == null ||
                !probe.LearningV331DropDownLandingForwardHandoffActive ||
                !IsFinite(probe.LearningV331DropDownLandingForwardHandoffTarget))
            {
                return false;
            }

            // CF68: in a tightly linked DropDown->DropDown chain the successor's
            // exact entry lies in this landing proof's current movement horizon.
            // Retain the already accepted V331 Mover; suppress ONLY the V334
            // one-metre refresh which would overrun the next executor frontier.
            // The successor still needs a real V3.2 Native attempt/landing proof.
            if (IsCF68NextDropDownEntryFrontierProtected(
                    probe, commitment, position,
                    out FollowerRouteMissionStep cf68Next,
                    out float cf68EntryAhead, out float cf68EntryLateral))
            {
                if (ShouldEmitDiagnostic("cf68NextDropDownAdmissionFrontierProtected:" +
                        commitment.EdgeId.ToString(CultureInfo.InvariantCulture), probe.Bot, 0.12f))
                {
                    WriteEvent("cf68NextDropDownAdmissionFrontierProtected", probe.Bot, new
                    {
                        predecessorEdgeId = commitment.EdgeId,
                        successorEdgeId = cf68Next.EdgeId,
                        successorEpisodeId = cf68Next.EpisodeId,
                        cf72ExactSuccessorLatchActive =
                            probe.CF72NextDropDownFrontierMissionId > 0 &&
                            probe.CF72NextDropDownFrontierEdgeId == cf68Next.EdgeId &&
                            probe.CF72NextDropDownFrontierEpisodeId == cf68Next.EpisodeId &&
                            probe.CF72NextDropDownFrontierChainId == cf68Next.ChainId,
                        currentPosition = Vec(position),
                        successorRawEntry = Vec(cf68Next.RawEntry),
                        entryAhead = cf68EntryAhead,
                        entryLateral = cf68EntryLateral,
                        currentConsumerTarget = Vec(probe.LearningV331DropDownLandingForwardHandoffTarget),
                        movementGenerationChanged = false,
                        nativeSuccessRecordedForSuccessor = false,
                        bodyRelocated = false,
                        action = "doNotRefreshPredecessorMoverAcrossUnstartedSameChainDropDownEntry"
                    });
                }
                return false;
            }

            float remainingToProducerTarget = GetPlanarDistance(
                position, probe.LearningV331DropDownLandingForwardHandoffTarget);
            if (remainingToProducerTarget > ChainHandoffStaleDestinationTolerance)
            {
                return false;
            }

            Vector3 direction = GetFlatDirection(authoredDirection);
            if (direction.sqrMagnitude < 0.5f)
            {
                return false;
            }

            Vector3 downstreamDirection = IsFinite(downstreamTarget)
                ? downstreamTarget - position
                : direction;
            downstreamDirection.y = 0f;
            float downstreamDistance = downstreamDirection.magnitude;
            if (downstreamDistance < 0.20f ||
                Vector3.Dot(GetFlatDirection(downstreamDirection), direction) < 0.25f)
            {
                downstreamDirection = direction;
                downstreamDistance = 1.20f;
            }
            else
            {
                downstreamDirection /= downstreamDistance;
            }

            Vector3 boundedTarget = position + downstreamDirection * Mathf.Min(1.20f, downstreamDistance);

            try
            {
                probe.Bot.Mover.SetBotMoverPause("R1-C82DA1E5A915",@probe?.Bot,false);
                // CF66: path refresh is not an owner authorized to change EFT
                // Run/Sprint state while V413 physical movement is still settling.
                ;
                Vector3 resolvedTarget = boundedTarget;
                string targetSource = "exactBoundedLookahead";
                NavMeshPathStatus status = probe.Bot.BotOwnerGoToPoint(
"R1-ED3A82AC1641",@probe?.Bot,                    resolvedTarget,true,-1f,false,false);
                if ((status != NavMeshPathStatus.PathComplete))
                {
                    if (!NavMesh.SamplePosition(boundedTarget, out NavMeshHit boundedHit, 0.70f, NavMesh.AllAreas) ||
                        GetPlanarDistance(position, boundedHit.position) <= ChainHandoffStaleDestinationTolerance)
                    {
                        return false;
                    }
                    resolvedTarget = boundedHit.position;
                    targetSource = "navSampleFallback";
                    status = probe.Bot.BotOwnerGoToPoint("R1-0BC50EABD0B5",@probe?.Bot,resolvedTarget,true,-1f,false,false);
                }
                else if (status != NavMeshPathStatus.PathComplete)
                {
                    return false;
                }
                if (status != NavMeshPathStatus.PathComplete ||
                    GetPlanarDistance(position, resolvedTarget) <= ChainHandoffStaleDestinationTolerance)
                {
                    return false;
                }

                probe.Bot.BotOwnerSetTargetMoveSpeed("R1-C66C95EBE5DE",@probe?.Bot,0.65f);
                probe.Bot.Mover.BotMoverSetTargetMoveSpeed("R1-1A6D1F63E3F2",@probe?.Bot,0.65f);
                ;
                // V334 accepted a fresh PathComplete; the Mover is now sole
                // native translation producer until this bounded landing proof ends.
                if ((ShouldEmitDiagnostic("cf66V334ConsumerRefresh:" +
                        commitment.EdgeId.ToString(CultureInfo.InvariantCulture), probe.Bot, 0.10f)))
                {
                    WriteEvent("cf66LandingMoverLookaheadExclusive", probe.Bot, new
                    {
                        edgeId = commitment.EdgeId,
                        previousTarget = Vec(probe.LearningV331DropDownLandingForwardHandoffTarget),
                        refreshedTarget = Vec(resolvedTarget),
                        pathStatus = status.ToString(),
                        moverHasPath = probe.Bot.Mover.HasPathAndNoComplete,
                        nativeSuccessRecorded = false,
                        bodyRelocated = false,
                        action = "pathCompleteConsumerRefreshWithoutSprintToggleOrDirectInputOverwrite"
                    });
                }
                Vector3 previousTarget = probe.LearningV331DropDownLandingForwardHandoffTarget;
                probe.LearningV331DropDownLandingForwardHandoffTarget = resolvedTarget;
                WriteEvent("learningV334DropDownLandingForwardLookaheadRefreshed", probe.Bot, new
                {
                    edgeId = commitment.EdgeId,
                    position = Vec(position),
                    previousTarget = Vec(previousTarget),
                    previousRemainingDistance = remainingToProducerTarget,
                    target = Vec(resolvedTarget),
                    targetSource,
                    downstreamSource = downstreamSource ?? string.Empty,
                    pathStatus = status.ToString(),
                    staleDestinationTolerance = ChainHandoffStaleDestinationTolerance,
                    successCommitted = false,
                    action = ("tryExactBoundedProducerBeforeNavSampleFallback")
                });
                return true;
            }
            catch
            {
                return false;
            }
                    }
        }

        // One geometry observation serves the existing intent and the native consumer.
        private static float GetCapabilityAdjacentDropDownNativeSpeed(
            ProbeRuntime probe,LearnedTraversalCommitment commitment,Vector3 body,
            out int nextEdgeId,out float successorEntryAhead,out float successorLateral)
        {
            nextEdgeId=0;successorEntryAhead=float.MaxValue;successorLateral=float.MaxValue;
            if(!TryCaptureCapabilityAdjacentLanding(probe,commitment,body,out var next,out var geometry) ||
                !FollowerCapabilityAdjacentLandingPolicy.LegacyRegion(geometry))return ProbeMomentumFullMoveSpeed;
            nextEdgeId=next.EdgeId;successorEntryAhead=geometry.Ahead;successorLateral=geometry.Lateral;
            return FollowerCapabilityAdjacentLandingPolicy.MoveSpeed(geometry);
        }
        private static bool TryCaptureCapabilityAdjacentLanding(
            ProbeRuntime probe,LearnedTraversalCommitment commitment,Vector3 body,
            out FollowerRouteMissionStep successor,out AdjacentLandingGeometry geometry)
        {
            successor=null;geometry=default;
            if (probe?.Bot == null || commitment == null || !IsFinite(body) ||
                !TryResolveLearningV376MissionForProbe(probe,
                    out FollowerRouteMission mission, out int index) ||
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
                next.AttemptStartedObserved || !IsFinite(next.RawEntry) ||
                !IsFinite(next.RawExit) || next.RawEntry.y - next.RawExit.y < 0.25f ||
                Mathf.Abs(body.y - next.RawEntry.y) > 1.35f)
                return false;
            Vector3 successorAxis = GetFlatDirection(next.RawExit - next.RawEntry);
            Vector3 currentAxis = GetFlatDirection(commitment.RawExit - commitment.RawEntry);
            if (successorAxis.sqrMagnitude < 0.5f ||
                currentAxis.sqrMagnitude < 0.5f ||
                Vector3.Dot(successorAxis, currentAxis) < 0.60f)
                return false;
            Vector3 toEntry = next.RawEntry - body;
            toEntry.y = 0f;
            float ahead = Vector3.Dot(toEntry, successorAxis);
            float lateral = (toEntry - successorAxis * ahead).magnitude;
            successor=next;
            geometry=new AdjacentLandingGeometry(ahead,lateral,toEntry.magnitude,
                Mathf.Abs(body.y-next.RawEntry.y),GetPlanarDistance(next.RawEntry,next.RawExit),
                probe.Player?.MovementContext?.IsGrounded==true,body.y-next.RawExit.y,probe.Player?.MovementContext!=null);
            return true;
        }
        internal static float ConstrainCapabilityNativeSprintSpeed(MovementContext context,Player player,float requested)
        {
            BotOwner bot=player?.AIData?.BotOwner;
            if(!TryGetActiveProbe(bot,out ProbeRuntime probe)||probe.Lifecycle==null)return requested;
            var port=new CapabilityAdjacentLandingPort(probe,bot,player,context,requested);
            if(FollowerCapabilityAdjacentLandingDriver.Commit(port)!=FollowerCapabilityAdjacentLandingDriver.Receipt.Clamped)
                return requested;
            if(ShouldEmitDiagnostic("capabilityAdjacentLandingNativeEnvelope:"+probe.LearnedEdgeId,bot,0.15f))
                WriteEvent("capabilityAdjacentLandingNativeEnvelopeApplied",bot,new{
                    missionId=probe.Lifecycle.Origin.Mission,episodeId=probe.TraversalEpisodeId,
                    edgeId=probe.LearnedEdgeId,successorEdgeId=port.SuccessorEdge,
                    writer=probe.MovementWriterGeneration,stage=probe.Stage.ToString(),
                    requestedNativeSprintSpeed=requested,appliedNativeSprintSpeed=port.Result,
                    targetMoveSpeed=FollowerCapabilityAdjacentLandingPolicy.MoveSpeed(port.Geometry),
                    successorEntryAhead=port.Geometry.Ahead,successorLateral=port.Geometry.Lateral,
                    extendedPastEntry=port.Geometry.Ahead < -0.30f,body=Vec(player.Position),
                    actualParameterChannel="MovementContext.SprintSpeed",nativeSuccessRecorded=false,
                    bodyRelocated=false,action="sameCurrentTraversalEnvelopeConstrainsNativeConsumerWithoutSprintToggle"});
            return port.Result;
        }
        private sealed class CapabilityAdjacentLandingPort:ICapabilityAdjacentLandingPort
        {
            private readonly ProbeRuntime probe;private readonly BotOwner bot;private readonly Player player;
            private readonly MovementContext context;private readonly FollowerTraversalLifecycle.Identity origin;
            private readonly float requested;
            internal float Result;internal AdjacentLandingGeometry Geometry;internal int SuccessorEdge;
            internal CapabilityAdjacentLandingPort(ProbeRuntime p,BotOwner b,Player actor,MovementContext resource,float value)
            {probe=p;bot=b;player=actor;context=resource;origin=p.Lifecycle.Origin;requested=value;Result=value;}
            public bool IsCurrent
            {
                get
                {
                    long generation=FollowerMovementWriterAuthority.PeekExactGeneration(bot,out var owner);
                    return probe.LifecycleMission!=null && IsProbeLifecycleCurrent(probe) && origin.Equals(probe.Lifecycle.Origin) &&
                        ReferenceEquals(probe.Player,player) && ReferenceEquals(bot.GetPlayer,player) &&
                        ReferenceEquals(player.MovementContext,context) && generation>0 &&
                        generation==probe.MovementWriterGeneration && generation==origin.Writer &&
                        owner==FollowerMovementWriterAuthority.WriterDomain.Traversal &&
                        (probe.Stage==ProbeStage.RollingApproach || probe.Stage==ProbeStage.NativeEntryAcquisition ||
                         probe.Stage==ProbeStage.NativeDropDown) &&
                        string.Equals(probe.RequestedTraversalKind,"DropDown",StringComparison.OrdinalIgnoreCase);
                }
            }
            public bool TryObserve(out AdjacentLandingSnapshot snapshot)
            {
                snapshot=default;
                // Native setter observation never initializes or refreshes route anchors.
                var commitment=probe.FrozenTraversalCommitment;
                if(commitment==null || commitment.EdgeId!=probe.LearnedEdgeId ||
                    !TryCaptureCapabilityAdjacentLanding(probe,commitment,player.Position,out var next,out var geometry))return false;
                Geometry=geometry;SuccessorEdge=next.EdgeId;
                snapshot=new AdjacentLandingSnapshot(geometry,requested);return true;
            }
            public void Restrict(float value){Result=value;}
        }
    }
    // Constrain the parameter at consumption, including BotMover and SprintAcceleration
    // rewrites. No tick is suppressed; the original EFT setter/Animator owns actuation.
    internal static class FollowerCapabilityAdjacentLandingPatch
    {
        internal static void Apply(Harmony harmony)
        {
            var setter=AccessTools.PropertySetter(typeof(MovementContext),"SprintSpeed");
            if(setter==null)throw new MissingMethodException("MovementContext.SprintSpeed setter");
            harmony.Patch(setter,prefix:new HarmonyMethod(AccessTools.Method(
                typeof(FollowerCapabilityAdjacentLandingPatch),nameof(Prefix))){priority=Priority.Last});
        }
        private static void Prefix(MovementContext __instance,Player ____player,ref float __0)
        {
            __0=FollowerTraversalTestRuntime.ConstrainCapabilityNativeSprintSpeed(__instance,____player,__0);
        }
    }
}
