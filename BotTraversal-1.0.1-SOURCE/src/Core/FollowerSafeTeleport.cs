using Comfort.Common;
using EFT;

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using BotTraversal.Core;

namespace BotTraversal.Core
{
    /// <summary>
    /// Shared collision-aware teleport helper for manual regroup and automatic navigation recovery.
    /// A recovery teleport is intentionally the final safety net: it only accepts a sampled NavMesh
    /// point with reasonable height, clear standing capsule, and a complete path onward to the real
    /// navigation destination.
    /// </summary>
    internal static class FollowerSafeTeleport
    {
        private const float CapsuleRadius = 0.30f;
        private const float CapsuleBottom = 0.38f;
        private const float CapsuleTop = 1.55f;
        private const float MaxVerticalSampleOffset = 1.10f;
        private const float DestinationTerminalTolerance = 1.25f;
        private const float MinDistanceFromDestination = 2.75f;
        private const float MinDistanceBetweenFollowers = 1.50f;

        internal static bool TryTeleport(
            BotOwner bot,
            Vector3 target,
            string reason,
            out Vector3 teleportedTo,
            bool allowAuthoredOffNavMesh = false,
            long issuedTeleportGeneration = 0)
        {
            return TryTeleportAtOrigin(bot, target, reason, out teleportedTo,
                out _, allowAuthoredOffNavMesh, issuedTeleportGeneration);
        }

        // Used only by a chain bridge that must carry the new Teleport origin back
        // into its EXISTING chain guard. No new global ticket registry is created.
        internal static bool TryTeleportWithOriginReceipt(
            BotOwner bot, Vector3 target, string reason,
            out Vector3 teleportedTo, out long committedOrigin)
        {
            return TryTeleportAtOrigin(bot, target, reason, out teleportedTo,
                out committedOrigin, false, 0);
        }

        private static bool TryTeleportAtOrigin(
            BotOwner bot, Vector3 target, string reason,
            out Vector3 teleportedTo, out long committedOrigin,
            bool allowAuthoredOffNavMesh, long issuedTeleportGeneration)
        {
            teleportedTo = default;
            committedOrigin = 0;
            if (bot?.GetPlayer == null || bot.HealthController?.IsAlive != true || !IsFinite(target))
            {
                return false;
            }

            FollowerTraversalTestRuntime.ClearCapabilityRouteWorkForTeleport(bot,reason);
            // Already prepared by Runtime? Use that exact birth generation. Otherwise
            // this standalone teleport issues its own origin precisely once.
            long teleportGeneration = issuedTeleportGeneration > 0
                ? issuedTeleportGeneration
                : FollowerMovementWriterAuthority.BeginTeleport(bot, "safeTeleport:" + (reason ?? "unknown"));
            if (!FollowerMovementWriterAuthority.TryEnterBoundPhysicalMotion(
                bot, FollowerMovementWriterAuthority.WriterDomain.Teleport, teleportGeneration,
                out FollowerMovementWriterAuthority.WriteScope teleportScope,
                out string teleportAdmissionReason))
            {
                FollowerTraversalTestRuntime.RecordTeleportDiagnostic(bot, "v2SafeTeleportOriginRejected", new
                {
                    reason = reason ?? "unknown",
                    originGeneration = teleportGeneration,
                    admission = teleportAdmissionReason,
                    action = "rejectBeforeRelocationOrMoverMutation"
                });
                return false;
            }
            using (teleportScope)
            try
            {
                BotFollowerPlayer follower = BossPlayers.GetFollowerByProfileId(bot.ProfileId);

                // v1.3.25 transaction order: neutralize every PIT-owned route before BotMover.Teleport
                // and only then arm the short stabilization transaction. V1.3.20 armed grace first,
                // while Follow/Recovery could still enqueue a new path in the same frame.
                bot.Mover?.BotMoverStop("R1-F2550856A31E",@bot);
                bot.BotOwnerStopMove("R1-8058917E2ECD",@bot);
                if (bot.Mover != null)
                {
                    bot.Mover.SetBotMoverPause("R1-44D05BCAACBB",@bot,follower != null);
                }
                bot.GoToSomePointData?.BotGoToPointDataSetPoint("R1-B357AC56A644",@bot,bot.Position);
                bot.GoToSomePointData?.BotGoToPointDataUpdateToGo("R1-1B9192AA211B",@bot,false);
                bot.PatrollingData?.PatrollingDataPause("R1-C4575A167D2C",@bot);
                follower?.BeginTeleportGrace(target);
                if (bot.Mover != null)
                {
                    // EFT BotMover.Teleport() updates PositionOnWayInner and calls AllowTeleport(),
                    // but it does NOT update PrevSuccessLinkedFrom_1. Once AllowTeleport is consumed,
                    // method_12 can therefore reconnect the body to that historical point. V432
                    // commits the native mover's linked epoch in the same relocation transaction.
                    bot.Mover.BotMoverTeleport("R1-701B4E0164EB",@bot,target);
                    FollowerTraversalTestRuntime.TryCommitLearningV432RelocationNativeMoverEpoch(
                        bot, target, "safeTeleportImmediate:" + (reason ?? "unknown"), out string _);
                }
                else
                {
                    bot.GetPlayer.PlayerTeleport("R1-9A703FBCF839",@bot,target);
                }
                try
                {
                    bot.GetPlayer.MovementContext?.TryMovementContextApplyApproachMotion("R1-7BC76C3BF4DC",@bot,Vector3.zero,Mathf.Max(0.001f, Time.deltaTime));
                }
                catch
                {
                }
                // LearningV4.31: relocation must finish motionless. The ordinary hard recovery
                // helper intentionally publishes forward locomotion and is correct for stuck
                // followers, but doing that inside a Teleport transaction caused the freshly
                // relocated Sprint body to run ~1.25 m and then be corrected back by stabilization.
                bool locomotionRecovered = FollowerTraversalTestRuntime.TryQuiesceFollowerLocomotionForTeleport(
                    bot,
                    "safeTeleportPost:" + (reason ?? "unknown"),
                    out string locomotionRepairDetail,teleportGeneration);
                if (locomotionRecovered)
                {
                    FollowerStateAuthorityCoordinator.ClearLocomotionFault(bot, "safeTeleportPostQuiescent");
                }

                FollowerTraversalTestRuntime.RecordTeleportDiagnostic(bot, "teleportTransactionStarted", new
                {
                    reason = reason ?? "unknown",
                    target = new { x = target.x, y = target.y, z = target.z },
                    followerTransaction = follower != null,
                    locomotionRecovered,
                    locomotionRepairDetail,
                    state = bot.GetPlayer?.CurrentStateName.ToString()
                });
                teleportedTo = target;
                committedOrigin = teleportGeneration;
                Logger.LogInfo(
                    $"[NavRecovery][Teleport] follower='{bot.Profile?.Nickname ?? bot.ProfileId}' " +
                    $"reason={reason ?? "unknown"} target=({target.x:0.00},{target.y:0.00},{target.z:0.00}).");
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"[NavRecovery][Teleport] Failed for '{bot.Profile?.Nickname ?? bot.ProfileId}'.");
                Logger.LogError(ex);
                return false;
            }
        }


        internal static bool TryTeleportAcrossTraversalExit(
            BotOwner bot,
            Vector3 requestedExit,
            string reason,
            out Vector3 teleportedTo,
            bool allowAuthoredOffNavMesh = false,
            long issuedTeleportGeneration = 0)
        {
            teleportedTo = default;
            if (bot == null || !IsFinite(requestedExit))
            {
                return false;
            }

            float planar = Vector2.Distance(
                new Vector2(bot.Position.x, bot.Position.z),
                new Vector2(requestedExit.x, requestedExit.z));
            if (planar > 4.5f || Mathf.Abs(bot.Position.y - requestedExit.y) > 2.25f)
            {
                return false;
            }

            if (!TryFindValidatedTraversalExit(bot, requestedExit, out Vector3 safeExit, allowAuthoredOffNavMesh))
            {
                return false;
            }

            return TryTeleport(bot, safeExit, reason, out teleportedTo,
                allowAuthoredOffNavMesh: allowAuthoredOffNavMesh,
                issuedTeleportGeneration: issuedTeleportGeneration);
        }


        internal static bool TryValidateTraversalExitAnchor(
            BotOwner bot,
            Vector3 requestedExit,
            out Vector3 result)
        {
            result = default;
            if (bot == null || !IsFinite(requestedExit))
            {
                return false;
            }

            float planar = Vector2.Distance(
                new Vector2(bot.Position.x, bot.Position.z),
                new Vector2(requestedExit.x, requestedExit.z));
            if (planar > 4.5f || Mathf.Abs(bot.Position.y - requestedExit.y) > 2.25f)
            {
                return false;
            }

            // Player-authored traversal exits are already strong evidence that the far side is usable.
            // Keep only the two minimum safeguards that matter for teleporting: a nearby NavMesh point
            // and enough standing capsule clearance. Do not require a complete path all the way to the
            // moving leader, because that was exactly what caused valid short seams to fall through to
            // a much larger generic regroup teleport.
            return TryFindValidatedTraversalExit(bot, requestedExit, out result);
        }

        private static bool TryFindValidatedTraversalExit(BotOwner bot, Vector3 requestedExit, out Vector3 result, bool allowAuthoredOffNavMesh = false)
        {
            result = default;
            if (bot == null || !IsFinite(requestedExit))
            {
                return false;
            }

            Vector3 origin = bot.Position;
            Vector3 forward = requestedExit - origin;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f)
            {
                forward = Vector3.forward;
            }
            else
            {
                forward.Normalize();
            }
            float requestedProjection = Vector3.Dot(requestedExit - origin, forward);

            // First honor the player's authored far-side point. NavMesh.SamplePosition can still
            // snap a point beside a thin obstacle back onto the entry-side polygon, so even this
            // primary candidate must retain almost all of RawExit's forward projection.
            if (TrySampleAndValidateStandingSpot(bot, requestedExit, requestedExit.y, 0.55f, out Vector3 primary) &&
                IsFarSideTeleportCandidate(origin, forward, requestedProjection, primary))
            {
                result = primary;
                return true;
            }

            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            float[] forwardOffsets = { 0.20f, 0.42f, 0.68f };
            float[] lateralOffsets = { 0f, -0.22f, 0.22f, -0.42f, 0.42f };
            for (int f = 0; f < forwardOffsets.Length; f++)
            {
                for (int l = 0; l < lateralOffsets.Length; l++)
                {
                    Vector3 requested = requestedExit + forward * forwardOffsets[f] + right * lateralOffsets[l];
                    if (TrySampleAndValidateStandingSpot(bot, requested, requestedExit.y, 0.50f, out Vector3 candidate))
                    {
                        // Never let the fan fold back toward the entry side.
                        if (IsFarSideTeleportCandidate(origin, forward, requestedProjection, candidate))
                        {
                            result = candidate;
                            return true;
                        }
                    }
                }
            }

            if (allowAuthoredOffNavMesh &&
                TryValidateAuthoredTraversalExitWithoutNavMesh(bot, requestedExit, out Vector3 authored) &&
                IsFarSideTeleportCandidate(origin, forward, requestedProjection, authored))
            {
                result = authored;
                return true;
            }
            return false;
        }

        private static bool TryValidateAuthoredTraversalExitWithoutNavMesh(
            BotOwner bot,
            Vector3 requestedExit,
            out Vector3 result)
        {
            result = default;
            if (bot == null || !IsFinite(requestedExit))
            {
                return false;
            }

            // CompileFix44: a player-authored WalkThrough may deliberately cross private topology
            // that has collision/ground but no NavMesh polygon. Validate the demonstrated physical
            // standing location directly instead of requiring SamplePosition to reinterpret it.
            Vector3 candidate = requestedExit;
            RaycastHit[] groundHits = Physics.RaycastAll(
                requestedExit + Vector3.up * 0.80f,
                Vector3.down,
                1.70f,
                ~0,
                QueryTriggerInteraction.Ignore);
            Array.Sort(groundHits, (a, b) => a.distance.CompareTo(b.distance));
            bool haveGround = false;
            for (int i = 0; i < groundHits.Length; i++)
            {
                Collider collider = groundHits[i].collider;
                if (collider == null || IsFireteamCollider(bot, collider) || IsAiOnlyHelperCollider(collider))
                {
                    continue;
                }
                if (Mathf.Abs(groundHits[i].point.y - requestedExit.y) > 0.85f)
                {
                    continue;
                }
                candidate.y = groundHits[i].point.y;
                haveGround = true;
                break;
            }
            if (!haveGround)
            {
                return false;
            }

            Vector3 bottom = candidate + Vector3.up * CapsuleBottom;
            Vector3 top = candidate + Vector3.up * CapsuleTop;
            Collider[] overlaps = Physics.OverlapCapsule(
                bottom, top, CapsuleRadius, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < overlaps.Length; i++)
            {
                Collider collider = overlaps[i];
                if (collider == null || IsFireteamCollider(bot, collider) || IsAiOnlyHelperCollider(collider))
                {
                    continue;
                }
                return false;
            }

            result = candidate;
            return true;
        }


        private static bool IsFarSideTeleportCandidate(
            Vector3 origin,
            Vector3 forward,
            float requestedProjection,
            Vector3 candidate)
        {
            if (!IsFinite(candidate))
            {
                return false;
            }

            Vector3 delta = candidate - origin;
            delta.y = 0f;
            return Vector3.Dot(delta, forward) + 0.10f >= requestedProjection;
        }

        internal static bool TryFindSafeSpotNearDestination(
            BotOwner bot,
            Vector3 destination,
            out Vector3 result)
        {
            result = default;
            if (bot == null || !IsFinite(destination))
            {
                return false;
            }

            // Prefer an outer ring so Death-Evac still walks the final few metres into the actual
            // extraction volume instead of teleporting directly into success.
            float[] radii = { 6.5f, 5.0f, 8.0f, 4.5f };
            float seed = Mathf.Abs((bot.ProfileId ?? string.Empty).GetHashCode() % 360);
            for (int r = 0; r < radii.Length; r++)
            {
                float radius = radii[r];
                for (int i = 0; i < 12; i++)
                {
                    float radians = (seed + i * 30f) * Mathf.Deg2Rad;
                    Vector3 requested = destination + new Vector3(Mathf.Cos(radians), 0f, Mathf.Sin(radians)) * radius;
                    if (TryValidateCandidate(bot, requested, destination, 1.6f, out result))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        internal static bool TryFindManualRegroupSpot(
            BotOwner bot,
            Vector3 playerPosition,
            IReadOnlyList<Vector3> reserved,
            int index,
            out Vector3 result)
        {
            result = default;
            if (bot == null || !IsFinite(playerPosition))
            {
                return false;
            }

            float seed = UnityEngine.Random.Range(0f, 360f) + index * 67f;
            for (int attempt = 0; attempt < 24; attempt++)
            {
                int ring = attempt / 8;
                float radius = 2.25f + ring * 1.40f;
                float radians = (seed + attempt * 45f) * Mathf.Deg2Rad;
                Vector3 requested = playerPosition + new Vector3(Mathf.Cos(radians), 0f, Mathf.Sin(radians)) * radius;

                if (!TrySampleAndValidateStandingSpot(bot, requested, playerPosition.y, 1.4f, out Vector3 candidate))
                {
                    continue;
                }

                if (Vector3.Distance(candidate, playerPosition) < 2f)
                {
                    continue;
                }

                bool overlaps = false;
                if (reserved != null)
                {
                    for (int i = 0; i < reserved.Count; i++)
                    {
                        if (Vector3.Distance(candidate, reserved[i]) < MinDistanceBetweenFollowers)
                        {
                            overlaps = true;
                            break;
                        }
                    }
                }

                if (!overlaps)
                {
                    result = candidate;
                    return true;
                }
            }

            return false;
        }

        internal static bool TryValidateRecoveryAnchor(
            BotOwner bot,
            Vector3 requested,
            Vector3 destination,
            out Vector3 result)
        {
            return TryValidateCandidate(bot, requested, destination, 1.15f, out result);
        }

        internal static bool TryValidateLocalTraversalEntryAnchor(
            BotOwner bot,
            Vector3 requested,
            out Vector3 result)
        {
            // CompileFix42 chain bridge recovery targets the demonstrated near-side entry of the
            // NEXT traversal. Requiring a complete onward NavMesh path would reject the exact
            // DropDown/Jump seam we are trying to bridge, so validate only local standing safety.
            result = default;
            return bot != null && IsFinite(requested) &&
                   TrySampleAndValidateStandingSpot(bot, requested, requested.y, 0.80f, out result);
        }

        internal static bool TryValidateEmergencyAnchor(
            BotOwner bot,
            Vector3 requested,
            out Vector3 result)
        {
            // R8.2 World-Safety uses a recently observed grounded NavMesh anchor. Once the follower
            // is already falling out of the world its current position cannot satisfy ordinary path
            // or distance gates, so validate only the properties that matter for immediate survival:
            // nearby NavMesh projection and clear standing capsule.
            result = default;
            return bot != null && IsFinite(requested) &&
                   TrySampleAndValidateStandingSpot(bot, requested, requested.y, 0.90f, out result);
        }

        private static bool TryValidateCandidate(
            BotOwner bot,
            Vector3 requested,
            Vector3 destination,
            float sampleRadius,
            out Vector3 result)
        {
            result = default;
            if (!TrySampleAndValidateStandingSpot(bot, requested, requested.y, sampleRadius, out Vector3 candidate))
            {
                return false;
            }

            if (Vector3.Distance(candidate, destination) < MinDistanceFromDestination)
            {
                return false;
            }

            if (Vector3.Distance(candidate, bot.Position) < 1.25f)
            {
                return false;
            }

            if (!HasCompletePath(candidate, destination))
            {
                return false;
            }

            result = candidate;
            return true;
        }

        private static bool TrySampleAndValidateStandingSpot(
            BotOwner bot,
            Vector3 requested,
            float referenceY,
            float sampleRadius,
            out Vector3 candidate)
        {
            candidate = default;
            if (!NavMesh.SamplePosition(requested, out NavMeshHit hit, sampleRadius, NavMesh.AllAreas))
            {
                return false;
            }

            if (Mathf.Abs(hit.position.y - referenceY) > MaxVerticalSampleOffset)
            {
                return false;
            }

            Vector3 bottom = hit.position + Vector3.up * CapsuleBottom;
            Vector3 top = hit.position + Vector3.up * CapsuleTop;
            Collider[] overlaps = Physics.OverlapCapsule(
                bottom,
                top,
                CapsuleRadius,
                ~0,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < overlaps.Length; i++)
            {
                Collider collider = overlaps[i];
                if (collider == null || IsFireteamCollider(bot, collider) || IsAiOnlyHelperCollider(collider))
                {
                    continue;
                }

                Logger.LogInfo(
                    $"[NavRecovery][TeleportCandidateRejected] follower='{bot?.Profile?.Nickname ?? bot?.ProfileId}' " +
                    $"blocker='{collider.name}' layer={collider.gameObject.layer} " +
                    $"candidate=({hit.position.x:0.00},{hit.position.y:0.00},{hit.position.z:0.00}).");
                return false;
            }

            candidate = hit.position;
            return true;
        }

        private static bool IsAiOnlyHelperCollider(Collider collider)
        {
            if (collider?.gameObject == null)
            {
                return false;
            }

            // Customs and several other EFT maps expose AITerrain_* helper colliders on the AI
            // navigation layer. They participate in bot/navigation bookkeeping but are not solid
            // player geometry; treating them as capsule blockers rejected otherwise valid exits.
            string objectName = collider.name ?? string.Empty;
            string gameObjectName = collider.gameObject.name ?? string.Empty;
            return objectName.StartsWith("AITerrain", StringComparison.OrdinalIgnoreCase) ||
                   gameObjectName.StartsWith("AITerrain", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsFireteamCollider(BotOwner bot, Collider collider)
        {
            if (collider == null)
            {
                return false;
            }

            // EFT.Player.Transform is BifacialTransform in SPT 4.0.13, not UnityEngine.Transform.
            // Do not use Transform hierarchy APIs here. The collider hierarchy already gives us
            // the owning EFT.Player for the bot itself, the main player, followers, body parts,
            // armor, weapons and attached equipment.
            Player botPlayer = bot?.GetPlayer;
            Player otherPlayer = collider.GetComponentInParent<Player>();
            if (otherPlayer == null)
            {
                return false;
            }

            if (botPlayer != null && ReferenceEquals(otherPlayer, botPlayer))
            {
                return true;
            }

            Player mainPlayer = Singleton<GameWorld>.Instance?.MainPlayer;
            if (mainPlayer != null && ReferenceEquals(otherPlayer, mainPlayer))
            {
                return true;
            }

            return !string.IsNullOrEmpty(otherPlayer.ProfileId) &&
                   BossPlayers.GetFollowerByProfileId(otherPlayer.ProfileId) != null;
        }

        private static bool HasCompletePath(Vector3 start, Vector3 target)
        {
            if (!NavMesh.SamplePosition(start, out NavMeshHit startHit, 0.75f, NavMesh.AllAreas) ||
                !NavMesh.SamplePosition(target, out NavMeshHit targetHit, 1.5f, NavMesh.AllAreas))
            {
                return false;
            }

            NavMeshPath path = new NavMeshPath();
            if (!NavMesh.CalculatePath(startHit.position, targetHit.position, NavMesh.AllAreas, path) ||
                path.status != NavMeshPathStatus.PathComplete || path.corners == null || path.corners.Length < 1)
            {
                return false;
            }

            return Vector3.Distance(path.corners[path.corners.Length - 1], targetHit.position) <= DestinationTerminalTolerance;
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                   !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }
    }
}
