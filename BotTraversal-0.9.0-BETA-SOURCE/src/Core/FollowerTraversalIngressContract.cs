using System;
using EFT;
using UnityEngine;

namespace BotTraversal.Core
{
    // V2 Phase A1/A2: one stateless Typed Route current-step identity
    // contract for ALL pre-native Follow-owned ingress physical writers.
    // This partial supplies neither a new owner nor a retry state machine.
    internal static partial class FollowerTraversalTestRuntime
    {
        // Phase A2.2: identity is PURE and shared by Follow-ingress, the
        // pre-native persistent Traversal transaction, and successor-debt
        // consumers. One Step reference is authoritative within one Mission.
        // Never infer authority from an EdgeId shared by another episode.
        private static bool TryValidateV2CurrentTypedStep(
            BotOwner bot, FollowerRouteMission mission, FollowerRouteMissionStep step,
            bool requirePreNative, out string rejectReason)
        {
            rejectReason = string.Empty;
            int index = mission?.CurrentStepIndex ?? -1;
            if (bot == null || mission?.Steps == null || step == null)
                rejectReason = "missingIngressIdentity";
            else if (mission.Lifecycle.Retired || mission.LifecycleRaid!=raidGeneration ||
                mission.LifecycleBotId!=bot.ProfileId ||
                !FollowerRouteMissions.TryGetValue(bot.ProfileId,out var liveMission) || !ReferenceEquals(liveMission,mission))
                rejectReason = "retiredOrForeignMission";
            else if (IsCapabilityPortalWaiting(bot))
                rejectReason="portalQueueOwnsWait";
            else if (IsCapabilityRecoveryPending(bot))
                rejectReason = "sequenceTerminalRecoveryOwnsMotion";
            else if (IsCapabilityRouteRetryReturning(mission))
                rejectReason = "fullRouteRetryOwnsRecovery";
            else if (!step.Lifecycle.DebtOutstanding)
                rejectReason = "stepDebtAlreadyResolved";
            else if (index < 0 || index >= mission.Steps.Count ||
                !ReferenceEquals(mission.Steps[index], step))
                rejectReason = "staleMissionStep";
            else if (requirePreNative && HasActiveTraversalProbe(bot))
                rejectReason = "nativeAlreadyOwnsMotion";
            return rejectReason.Length == 0;
        }

        private static void RecordV2TypedStepLeaseRejection(
            BotOwner bot, FollowerRouteMission mission, FollowerRouteMissionStep step,
            string source, string reason, string eventName)
        {
            if (bot == null || !ShouldEmitDiagnostic(eventName + ":" + source, bot, 0.75f))
                return;
            WriteEvent(eventName, bot, new
            {
                reason,
                source,
                missionId = mission?.MissionId ?? 0,
                currentStepIndex = mission?.CurrentStepIndex ?? -1,
                candidateEdgeId = step?.EdgeId ?? 0,
                candidateEpisodeId = step?.EpisodeId ?? 0,
                action = "staleOrNativeOwnedTypedStepCannotPublishPhysicalMotion"
            });
        }

        // Follow-ingress never borrows an active Traversal/Native owner.
        private static bool TryEnterV2CurrentIngressScope(
            BotOwner bot, FollowerRouteMission mission, FollowerRouteMissionStep step,
            string source, out FollowerMovementWriterAuthority.WriteScope scope,
            out string rejectReason)
        {
            scope = default(FollowerMovementWriterAuthority.WriteScope);
            if (!TryValidateV2CurrentTypedStep(bot, mission, step, true, out rejectReason))
            {
                RecordV2TypedStepLeaseRejection(bot, mission, step, source,
                    rejectReason, "v2rc70IngressLeaseRejected");
                return false;
            }
            if (!FollowerMovementWriterAuthority.TryEnterFollowPhysicalMotion(
                bot, source, out scope, out rejectReason))return false;
            if (TryValidateV2CurrentTypedStep(bot, mission, step, true, out rejectReason))return true;
            scope.Dispose();scope=default(FollowerMovementWriterAuthority.WriteScope);
            RecordV2TypedStepLeaseRejection(bot, mission, step, source,
                rejectReason, "v2rc70IngressLeaseRejected");
            return false;
        }

        // Pre-probe persistent producer: the SAME live Traversal generation,
        // PLUS the SAME currently executing Typed Step. ContinueTraversal may
        // renew the existing lease but must never mint a new one here.
        private static bool TryEnterV2CurrentTypedTraversalScope(
            BotOwner bot, FollowerRouteMission mission, FollowerRouteMissionStep step,
            long expectedGeneration, string source,
            out FollowerMovementWriterAuthority.WriteScope scope,
            out string rejectReason)
        {
            scope = default(FollowerMovementWriterAuthority.WriteScope);
            if (!TryValidateV2CurrentTypedStep(bot, mission, step, true, out rejectReason))
            {
                RecordV2TypedStepLeaseRejection(bot, mission, step, source,
                    rejectReason, "v2rc72TypedTraversalLeaseRejected");
                return false;
            }
            long currentGeneration = FollowerMovementWriterAuthority.GetCurrentGeneration(
                bot, out FollowerMovementWriterAuthority.WriterDomain owner);
            if (expectedGeneration <= 0 || currentGeneration != expectedGeneration ||
                owner != FollowerMovementWriterAuthority.WriterDomain.Traversal)
            {
                rejectReason = "staleTraversalGeneration";
                RecordV2TypedStepLeaseRejection(bot, mission, step, source,
                    rejectReason, "v2rc72TypedTraversalLeaseRejected");
                return false;
            }
            long continuedGeneration = FollowerMovementWriterAuthority.ContinueTraversal(
                bot, expectedGeneration, source);
            if (continuedGeneration <= 0 || continuedGeneration != expectedGeneration)
            {
                rejectReason = "traversalRenewalRejected";
                RecordV2TypedStepLeaseRejection(bot, mission, step, source,
                    rejectReason, "v2rc72TypedTraversalLeaseRejected");
                return false;
            }
            // Re-check Step identity after renewing, before opening the write.
            if (!TryValidateV2CurrentTypedStep(bot, mission, step, true, out rejectReason))
            {
                RecordV2TypedStepLeaseRejection(bot, mission, step, source,
                    rejectReason, "v2rc72TypedTraversalLeaseRejected");
                return false;
            }
            bool entered = FollowerMovementWriterAuthority.TryEnterBoundPhysicalMotion(
                bot, FollowerMovementWriterAuthority.WriterDomain.Traversal,
                continuedGeneration, out scope, out rejectReason);
            if (!entered)
                RecordV2TypedStepLeaseRejection(bot, mission, step, source,
                    rejectReason, "v2rc72TypedTraversalLeaseRejected");
            return entered;
        }


        // V2 Phase A3.1: one stateless, role-aware adjacency receipt for both
        // pre-native physical handoff and physically-subsumed successor debt.
        // No policy here may grant Native success, mark a Step completed,
        // write movement, create a recovery owner, or relocate the body.
        private static bool TryValidateV2AdjacentPhysicalContinuationBoundary(
            BotOwner bot, FollowerRouteMission mission,
            FollowerRouteMissionStep predecessor, FollowerRouteMissionStep successor,
            bool predecessorIsCurrent, out string rejectReason)
        {
            rejectReason = string.Empty;
            if (bot == null || mission?.Steps == null || predecessor == null || successor == null)
                rejectReason = "missingAdjacentPhysicalIdentity";
            else
            {
                int previousIndex = predecessorIsCurrent
                    ? mission.CurrentStepIndex : mission.CurrentStepIndex - 1;
                if (previousIndex < 0 || previousIndex + 1 >= mission.Steps.Count)
                    rejectReason = "adjacentPhysicalCursorOutOfRange";
                else if (!ReferenceEquals(mission.Steps[previousIndex], predecessor))
                    rejectReason = "stalePhysicalPredecessorStep";
                else if (!ReferenceEquals(mission.Steps[previousIndex + 1], successor))
                    rejectReason = "stalePhysicalSuccessorStep";
            }
            return rejectReason.Length == 0;
        }

        // Observability is distinct from admission. A boundary receipt is not
        // an actuator completion receipt and never grants traversal success.
        private static void RecordV2PhysicalContinuationBoundary(
            BotOwner bot, FollowerRouteMission mission,
            FollowerRouteMissionStep predecessor, FollowerRouteMissionStep successor,
            string source, string evidenceKind)
        {
            if (bot == null || mission == null || predecessor == null || successor == null ||
                !ShouldEmitDiagnostic("v2rc73AdjacentPhysicalBoundary:" + source + ":" +
                    mission.MissionId.ToString() + ":" + successor.EpisodeId.ToString(), bot, 0.30f))
                return;
            long generation = FollowerMovementWriterAuthority.GetCurrentGeneration(
                bot, out FollowerMovementWriterAuthority.WriterDomain owner);
            WriteEvent("v2rc73AdjacentPhysicalBoundaryAdmitted", bot, new
            {
                missionId = mission.MissionId,
                currentStepIndex = mission.CurrentStepIndex,
                predecessorEpisodeId = predecessor.EpisodeId,
                predecessorEdgeId = predecessor.EdgeId,
                successorEpisodeId = successor.EpisodeId,
                successorEdgeId = successor.EdgeId,
                source,
                evidenceKind,
                owner = owner.ToString(),
                generation,
                nativeSuccessRecorded = false,
                traversalSuccessRecorded = false,
                bodyRelocated = false,
                action = "identityOnlyBoundaryReceiptDoesNotReplacePhysicalOrNativeCompletionTruth"
            });
        }

        // Typed Route physical producers use the same current-step gate.
        // Generic Follow recovery is NOT a typed ingress producer: it keeps
        // its separate recovery lease and pre-existing no-live-Mission rules.
        // Never borrow this gate to renew an active Native/Traversal owner.

        // Directional agreement is a PURE measurement, not a movement receipt.
        // Caller selects the minimum observed-vector and intention-vector power;
        // an unavailable measurement retains that caller's existing fallback.
        // It NEVER grants Native admission, completion, retry, or teleport.
        private static float MeasureRC70IngressDirectionalAgreement(
            Vector3 observed, Vector3 intended,
            float minObservedSq, float minIntendedSq, float unknownDot)
        {
            observed.y = 0f;
            intended.y = 0f;
            float observedSq = observed.sqrMagnitude;
            float intendedSq = intended.sqrMagnitude;
            if (observedSq <= minObservedSq || intendedSq <= minIntendedSq)
                return unknownDot;
            return Vector3.Dot(observed / Mathf.Sqrt(observedSq),
                intended / Mathf.Sqrt(intendedSq));
        }
    }
}
