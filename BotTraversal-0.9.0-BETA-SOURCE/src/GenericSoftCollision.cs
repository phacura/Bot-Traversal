using Comfort.Common;
using BotTraversal.Core;
using EFT;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace BotTraversal
{
    /// <summary>Pairs only acknowledged player-following AI; never disables entire Player layers.
    /// Captures prior ignore state and restores only changes owned by this component.</summary>
    public sealed class GenericSoftCollision : MonoBehaviour
    {
        private const float PushStrength=2f;
        private const float MaximumPushSpeed=1.5f;
        private const float StationarySpeedThresholdSqr=.15f*.15f;
        private Player localPlayer;
        private readonly Dictionary<string, Pair> active = new Dictionary<string, Pair>();
        private readonly HashSet<string> expected = new HashSet<string>();
        private readonly List<string> remove = new List<string>();
        private sealed class PomRecord
        {
            public ColliderExtrusion Extrusion;
            public Collider Other;
            public bool WasIgnored;
        }
        private sealed class Pair
        {
            public Player First, Second;
            public Collider A, B, PomA, PomB;
            public bool UnityIgnored, EftAB, EftBA, PomAIgnored, PomBIgnored;
            public readonly List<PomRecord> Extrusions = new List<PomRecord>();
        }
        private static readonly FieldInfo HeadExtrusion =
            typeof(PlayerOverlapManager).GetField("_headColliderExtrusion", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo CameraExtrusion =
            typeof(PlayerOverlapManager).GetField("_cameraColliderExtrusion", BindingFlags.Instance | BindingFlags.NonPublic);
        private static bool Healthy(Player p) => p?.HealthController?.IsAlive == true &&
            p.CharacterController?.GetCollider() != null;
        private static string Key(Player a, Player b) =>
            string.CompareOrdinal(a.ProfileId, b.ProfileId) <= 0
                ? a.ProfileId + ":" + b.ProfileId : b.ProfileId + ":" + a.ProfileId;
        private void OnDisable() => RestoreAll();
        private void OnDestroy() => RestoreAll();
        public void SetParticipants(Player player, List<BotOwner> followers)
        {
            expected.Clear();
            if (!BotTraversalPlugin.PassThroughEnabled.Value || !Healthy(player) || followers == null)
            {
                RestoreAll(); return;
            }
            if (!ReferenceEquals(localPlayer, player)) RestoreAll();
            localPlayer = player;
            var members = new List<Player>();
            foreach (BotOwner bot in followers)
            {
                Player p = bot?.GetPlayer;
                if (!Healthy(p) || p.ProfileId == player.ProfileId ||
                    !FollowerEnvironment.PassThroughPermitted(bot)) continue;
                members.Add(p);
                Connect(player, p);
            }
            for (int i = 0; i < members.Count; i++)
                for (int j = i + 1; j < members.Count; j++) Connect(members[i], members[j]);
            remove.Clear();
            foreach (var pair in active)
                if (!expected.Contains(pair.Key)) remove.Add(pair.Key);
            foreach (string key in remove) { Restore(active[key]); active.Remove(key); }
        }
        private void FixedUpdate()
        {
            // Exactly the V475 contract: push ONLY the locally controlled player when both
            // characters are almost stationary and their real controllers overlap. IgnoreCollision
            // is kept for friendlies; world/door/terrain collision stays active via EFT Move().
            Player player = localPlayer;
            if (player == null || !BotTraversalPlugin.PassThroughEnabled.Value ||
                !ReferenceEquals(Singleton<GameWorld>.Instance?.MainPlayer, player) ||
                !Healthy(player)) return;
            Vector3 velocity = player.Velocity;
            velocity.y = 0f;
            if (velocity.sqrMagnitude > StationarySpeedThresholdSqr) return;

            Vector3 correction = Vector3.zero;
            foreach (Pair pair in active.Values)
            {
                if (!ReferenceEquals(pair.First, player) || !Healthy(pair.Second) ||
                    pair.A == null || pair.B == null || !pair.A.enabled || !pair.B.enabled ||
                    !pair.A.gameObject.activeInHierarchy || !pair.B.gameObject.activeInHierarchy)
                    continue;
                Vector3 otherVelocity = pair.Second.Velocity;
                otherVelocity.y = 0f;
                if (otherVelocity.sqrMagnitude > StationarySpeedThresholdSqr) continue;
                try
                {
                    if (!Physics.ComputePenetration(
                        pair.A, pair.A.transform.position, pair.A.transform.rotation,
                        pair.B, pair.B.transform.position, pair.B.transform.rotation,
                        out Vector3 direction, out float distance) || distance <= 0f) continue;
                    Vector3 horizontal = Vector3.ProjectOnPlane(direction.normalized * distance, Vector3.up);
                    if (horizontal.sqrMagnitude > 0.000001f) correction += horizontal;
                }
                catch { /* Disabled/disposed collider while Fika despawns. */ }
            }
            if (correction.sqrMagnitude <= 0.000001f) return;
            Vector3 pushVelocity = Vector3.ClampMagnitude(correction * PushStrength, MaximumPushSpeed);
            Vector3 motion = pushVelocity * Time.fixedDeltaTime;
            if (motion.sqrMagnitude <= 0.000001f) return;
            try { player.CharacterController.TryICharacterControllerMove("R1-EF91BF071517",null,motion,Time.fixedDeltaTime); }
            catch { /* Fail closed; never write Transform.position. */ }
        }

        private void Connect(Player a, Player b)
        {
            if (string.IsNullOrEmpty(a.ProfileId) || string.IsNullOrEmpty(b.ProfileId)) return;
            string key = Key(a,b); expected.Add(key);
            Collider ca = a.CharacterController?.GetCollider(), cb = b.CharacterController?.GetCollider();
            if (ca == null || cb == null || ca == cb) return;
            Collider pa = a.POM?.Collider, pb = b.POM?.Collider;
            if (active.TryGetValue(key, out Pair found))
            {
                if (ReferenceEquals(found.A,ca) && ReferenceEquals(found.B,cb) &&
                    ReferenceEquals(found.PomA,pa) && ReferenceEquals(found.PomB,pb))
                { Apply(found); return; }
                Restore(found); active.Remove(key);
            }
            var pair = new Pair { First=a, Second=b, A=ca, B=cb, PomA=pa, PomB=pb };
            try
            {
                pair.UnityIgnored = Physics.GetIgnoreCollision(ca,cb);
                pair.EftAB = EFTPhysicsClass.GetIgnoreCollision(ca,cb);
                pair.EftBA = EFTPhysicsClass.GetIgnoreCollision(cb,ca);
                pair.PomAIgnored = pa != null && Physics.GetIgnoreCollision(pa, cb);
                pair.PomBIgnored = pb != null && Physics.GetIgnoreCollision(pb, ca);
                AddPomExtrusions(pair.Extrusions, a.POM, cb);
                AddPomExtrusions(pair.Extrusions, b.POM, ca);
                active[key] = pair; Apply(pair);
            }
            catch { active.Remove(key); Restore(pair); }
        }
        private static void Apply(Pair p)
        {
            try
            {
                if (p.A == null || p.B == null) return;
                if (!Physics.GetIgnoreCollision(p.A,p.B)) global::BotTraversal.Core.FollowerControlBackend.PhysicsIgnoreCollision("R1-2B9F76BB391C",null,p.A,p.B,true);
                if (!EFTPhysicsClass.GetIgnoreCollision(p.A,p.B) ||
                    !EFTPhysicsClass.GetIgnoreCollision(p.B,p.A))
                    global::BotTraversal.Core.FollowerControlBackend.EFTPhysicsClassIgnoreCollision("R1-4A348AA4ECFA",null,p.A,p.B,true);
                if (p.PomA != null && !Physics.GetIgnoreCollision(p.PomA,p.B))
                    global::BotTraversal.Core.FollowerControlBackend.PhysicsIgnoreCollision("R1-75D5E1ADA4B7",null,p.PomA,p.B,true);
                if (p.PomB != null && !Physics.GetIgnoreCollision(p.PomB,p.A))
                    global::BotTraversal.Core.FollowerControlBackend.PhysicsIgnoreCollision("R1-82E6EB85D12D",null,p.PomB,p.A,true);
                foreach (PomRecord record in p.Extrusions)
                    if (record.Extrusion != null && record.Other != null &&
                        !record.Extrusion.method_1(record.Other))
                        record.Extrusion.ColliderExtrusionAddIgnoredCollider("R1-4A4845E8CA67",null,record.Other);
            }
            catch { /* Respawn and disposal races fail closed. */ }
        }
        private static void AddPomExtrusions(List<PomRecord> records, PlayerOverlapManager pom, Collider other)
        {
            if (pom == null || other == null) return;
            foreach (FieldInfo field in new[] { HeadExtrusion, CameraExtrusion })
            {
                try
                {
                    if (field?.GetValue(pom) is not ColliderExtrusion extrusion) continue;
                    bool ignored = extrusion.method_1(other);
                    records.Add(new PomRecord { Extrusion = extrusion, Other = other, WasIgnored = ignored });
                }
                catch { }
            }
        }
        private static void Restore(Pair p)
        {
            try
            {
                if (p.A != null && p.B != null)
                {
                    // Preserve pre-existing ignores registered by another mod.
                    if (!p.UnityIgnored) global::BotTraversal.Core.FollowerControlBackend.PhysicsIgnoreCollision("R1-BDBA47A67FF6",null,p.A,p.B,false);
                    if (!p.EftAB) global::BotTraversal.Core.FollowerControlBackend.EFTPhysicsClasssmethod_0("R1-9B8B8C7C38A0",null,p.A,p.B,false);
                    if (!p.EftBA) global::BotTraversal.Core.FollowerControlBackend.EFTPhysicsClasssmethod_0("R1-7C2F4E289C08",null,p.B,p.A,false);
                }
                if (p.PomA != null && p.B != null && !p.PomAIgnored)
                    global::BotTraversal.Core.FollowerControlBackend.PhysicsIgnoreCollision("R1-0655C1DA020D",null,p.PomA,p.B,false);
                if (p.PomB != null && p.A != null && !p.PomBIgnored)
                    global::BotTraversal.Core.FollowerControlBackend.PhysicsIgnoreCollision("R1-C7723A4146B7",null,p.PomB,p.A,false);
                foreach (PomRecord record in p.Extrusions)
                {
                    if (record.Extrusion == null || record.Other == null) continue;
                    bool ignored = record.Extrusion.method_1(record.Other);
                    if (record.WasIgnored && !ignored) record.Extrusion.ColliderExtrusionAddIgnoredCollider("R1-DFF73A094299",null,record.Other);
                    else if (!record.WasIgnored && ignored) record.Extrusion.ColliderExtrusionRemoveIgnoredCollider("R1-B5A88A7ED132",null,record.Other);
                }
            }
            catch { /* Game objects may have been destroyed. */ }
        }
        private void RestoreAll()
        {
            foreach (Pair p in active.Values) Restore(p);
            active.Clear(); expected.Clear(); localPlayer = null;
        }
    }
}
