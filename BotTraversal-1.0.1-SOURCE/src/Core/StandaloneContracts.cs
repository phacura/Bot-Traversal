using Comfort.Common;
using EFT;
using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using BotTraversal.Core;

namespace BotTraversal.Core
{
    // Standalone environment: none of these types references or loads PIT Fireteam.
    internal static class Logger
    {
        internal static ManualLogSource Instance;
        internal static void LogInfo(object value) => Instance?.LogInfo(value);
        internal static void LogRuntimeInfo(object value) => Instance?.LogInfo(value);
        internal static void LogWarning(object value) => Instance?.LogWarning(value);
        internal static void LogError(object value) => Instance?.LogError(value);
    }

    internal enum FollowerCommandType { None = 0, HoldPosition, MoveToPoint, ComeCloser,
        RegroupNearBoss, TakeLootItem, OpenDoor, TakeBodyGear, PushEnemy, SuppressEnemy,
        NeedSniper, CombatComeToBossCover, CombatMoveToPointTactical, TakeContainerLoot }

    internal static class FollowerEnvironment
    {
        internal static Player LocalPlayer => Singleton<GameWorld>.Instance?.MainPlayer;
        internal static bool IsActive => LocalPlayer?.HealthController?.IsAlive == true;
        private static readonly Dictionary<string, BotFollowerPlayer> Members = new Dictionary<string, BotFollowerPlayer>(StringComparer.Ordinal);
        private static readonly List<string> Stale = new List<string>();
        private static Func<string,int> publishedCommand;
        private static Func<BotOwner,bool> explicitGrenadeThrowActive;
        private static Func<BotOwner,bool> grenadeTransactionTerminal;
        private static Func<BotOwner,string> immediateSurvivalThreat;
        private static Func<BotOwner,bool> pitPassThroughPermission;

        private static Action<string,Vector3> teleportStarted;
        // Optional producer adapters registered by any companion plugin. Bot Traversal never references their types.
        internal static void RegisterCommandProvider(Func<string,int> query) { publishedCommand=query; }
        // Optional PIT F12 compatibility: BT remains sole collision-pair owner.
        internal static void RegisterPITPassThroughPermission(Func<BotOwner,bool> predicate)
        {
            pitPassThroughPermission = predicate;
        }
        internal static bool PassThroughPermitted(BotOwner bot)
        {
            try { return pitPassThroughPermission?.Invoke(bot) ?? true; }
            catch (Exception ex) { Logger.LogWarning("PIT pass-through provider failed: " + ex.Message); return false; }
        }

        // Historical PIT <=V475 embeds its own full traversal runtime. Never claim the same
        // follower from that assembly; prevent a second movement writer on legacy installs.
        private static bool IsOwnedByLegacyPIT(BotOwner bot)
        {
            try
            {
                object boss = bot?.BotFollower?.BossToFollow;
                if (boss == null) return false;
                var assembly = boss.GetType().Assembly;
                if (!string.Equals(assembly.GetName().Name, "pitFireTeam", StringComparison.OrdinalIgnoreCase)) return false;
                return assembly.GetType("pitTeam.Modules.FollowerTraversalTestRuntime", false) != null;
            }
            catch { return false; }
        }
        internal static void RegisterTeleportObserver(Action<string,Vector3> observer) { teleportStarted=observer; }
        // These delegates are optional. They carry facts, not PIT types or engine control.
        // PIT keeps the transaction state, BT can also work without PIT installed.
        internal static void RegisterGrenadeProvider(Func<BotOwner,bool> isExplicitThrowActive,
            Func<BotOwner,bool> isTerminal)
        {
            explicitGrenadeThrowActive = isExplicitThrowActive;
            grenadeTransactionTerminal = isTerminal;
        }
        internal static void RegisterSurvivalThreatProvider(Func<BotOwner,string> provider)
        {
            immediateSurvivalThreat = provider;
        }
        internal static bool IsPITExplicitGrenadeActive(BotOwner bot)
        {
            try { return explicitGrenadeThrowActive?.Invoke(bot) == true; }
            catch (Exception ex) { Logger.LogWarning("Grenade provider failed: " + ex.Message); return false; }
        }
        internal static bool IsPITGrenadeTransactionTerminal(BotOwner bot)
        {
            try { return grenadeTransactionTerminal?.Invoke(bot) == true; }
            catch (Exception ex) { Logger.LogWarning("Grenade terminal provider failed: " + ex.Message); return false; }
        }
        internal static bool TryGetImmediateSurvivalThreat(BotOwner bot, out string reason)
        {
            reason = string.Empty;
            try { reason = immediateSurvivalThreat?.Invoke(bot) ?? string.Empty; }
            catch (Exception ex) { Logger.LogWarning("Survival provider failed: " + ex.Message); }
            return reason.Length > 0;
        }

        internal static int GetExternalCommand(string profile)
        {
            try { return publishedCommand?.Invoke(profile) ?? 0; } catch { return 0; }
        }
        internal static void PublishTeleport(string profile,Vector3 target)
        {
            try { teleportStarted?.Invoke(profile,target); } catch(Exception e) { Logger.LogWarning("Teleport observer: "+e.Message); }
        }
        internal static bool Owns(BotOwner bot)
        {
            Player local = LocalPlayer;
            if (local == null || bot == null || bot.IsDead || bot.GetPlayer == null || bot.BotFollower == null ||
                !bot.BotFollower.HaveBoss || bot.BotFollower.BossToFollow == null) return false;
            try { return (!IsOwnedByLegacyPIT(bot) || LegacyPITCompatibility.Attached) && bot.BotFollower.BossToFollow.IsMe(local); }
            catch { return false; }
        }
        internal static System.Collections.Generic.IEnumerable<BotOwner> RegisteredFollowers => Members.Values.Select(x=>x.GetBot());
        internal static List<BotOwner> FindFollowers()
        {
            var bots = new List<BotOwner>();
            var world = Singleton<GameWorld>.Instance;
            var players = world?.AllAlivePlayersList;
            if (players == null) { Members.Clear(); return bots; }
            var alive = new HashSet<string>(StringComparer.Ordinal);
            foreach (Player player in players)
            {
                BotOwner bot = player?.AIData?.BotOwner;
                if (!Owns(bot) || string.IsNullOrEmpty(bot.ProfileId) || !alive.Add(bot.ProfileId)) continue;
                bots.Add(bot);
                if (!Members.ContainsKey(bot.ProfileId)) Members[bot.ProfileId] = new BotFollowerPlayer(bot);
            }
            Stale.Clear();
            foreach (var kv in Members)
            {
                if (!alive.Contains(kv.Key)) Stale.Add(kv.Key);
                else { bool _ = kv.Value.IsTeleportStabilizing; }
            }
            foreach (string key in Stale) Members.Remove(key);
            return bots;
        }
        internal static BotFollowerPlayer Lookup(string id) => !string.IsNullOrEmpty(id) && Members.TryGetValue(id, out var v) ? v : null;
        internal static List<BotFollowerPlayer> GetAll() => Members.Values.ToList();
        internal static void Clear() { Members.Clear(); }
    }
    internal static class BossPlayers
    {
        internal static object Instance => FollowerEnvironment.IsActive ? new object() : null;
        internal static bool IsFollower(BotOwner bot) => FollowerEnvironment.Owns(bot);
        internal static BotFollowerPlayer GetFollowerByProfileId(string profileId) => FollowerEnvironment.Lookup(profileId);
        internal static List<BotFollowerPlayer> GetFollowersByBoss(string leaderId) =>
            FollowerEnvironment.LocalPlayer?.ProfileId == leaderId ? FollowerEnvironment.GetAll() : new List<BotFollowerPlayer>();
    }
    internal sealed class BotFollowerPlayer
    {
        private readonly BotOwner bot;
        private float stabilizationUntil;
        private bool pausedByUs;
        internal BotFollowerPlayer(BotOwner owner) { bot = owner; }
        internal BotOwner GetBot() => bot;
        internal bool IsTeleportStabilizing
        {
            get
            {
                if (stabilizationUntil > 0 && Time.time >= stabilizationUntil)
                {
                    stabilizationUntil = 0;
                    if (pausedByUs && bot?.Mover != null) { bot.Mover.SetBotMoverPause("R1-5C974F062F72",null,false); pausedByUs = false; }
                }
                return stabilizationUntil > Time.time;
            }
        }
        internal bool LastTeleportTransactionSucceeded => !IsTeleportStabilizing;
        internal string LastTeleportTransactionResult => IsTeleportStabilizing ? "pending" : "standaloneGraceEnded";
        internal bool HasCombatHandoffSignal => bot?.Memory?.HaveEnemy == true;
        internal void BeginTeleportGrace(Vector3 target)
        {
            stabilizationUntil = Time.time + 1.25f;
            pausedByUs = true;
            FollowerEnvironment.PublishTeleport(bot?.ProfileId, target);
        }
        internal bool TryPeekActiveCommand(out FollowerCommandType command, out Vector3 target, out float untilTime)
        {
            command = FollowerCommandType.None; target = default; untilTime = 0;
            int externalCommand = FollowerEnvironment.GetExternalCommand(bot?.ProfileId);
            if (externalCommand > 0 && externalCommand <= (int)FollowerCommandType.TakeContainerLoot)
            {
                command = (FollowerCommandType)externalCommand;
                untilTime = Time.time + 0.20f;
                return true;
            }
            // Third-party command APIs are not standardized: never guess one from a class name.
            return false;
        }
    }
    internal static class BotOwnerUpdateHub
    {
        private static readonly Dictionary<string, Action<BotOwner>> Subscribers = new Dictionary<string, Action<BotOwner>>(StringComparer.Ordinal);
        internal static void Register(string id, Action<BotOwner> callback) { if (id != null && callback != null) Subscribers[id] = callback; }
        internal static void Unregister(string id) { if (id != null) Subscribers.Remove(id); }
        internal static void Invoke(BotOwner bot)
        {
            foreach (var callback in Subscribers.Values.ToArray())
                try { callback(bot); } catch (Exception e) { Logger.LogError(e); }
        }
        internal static void Clear() => Subscribers.Clear();
    }
    // Read-only grenade transaction view. No PIT assembly reference; does not control hands or throws.
    internal static class FollowerGrenadeRuntimeGate
    {
        internal static bool IsExplicitThrowActive(BotOwner bot) =>
            FollowerEnvironment.IsPITExplicitGrenadeActive(bot);

        internal static bool IsCurrentTransactionTerminal(BotOwner bot) =>
            FollowerEnvironment.IsPITGrenadeTransactionTerminal(bot);
    }
    internal static class FollowerNavigationRecoveryRuntime { internal static void ResetRaid() { } }
    internal static class FollowerEvacuationRuntime
    {
        internal static bool IsDeathEvacuating(BotOwner bot) => false;
    }
    internal static class FollowerMedical
    {
        internal static bool IsUsingMedical(BotOwner bot) => IsHandsInteractionActiveForAuthority(bot?.GetPlayer);
        internal static bool IsHandsInteractionActiveForAuthority(Player player)
        {
            if (player == null) return false;
            try
            {
                var property = player.GetType().GetProperty("HandsController");
                object hands = property?.GetValue(player,null);
                string type = hands?.GetType().Name ?? "";
                if (type.IndexOf("Med",StringComparison.OrdinalIgnoreCase)>=0 ||
                    type.IndexOf("Heal",StringComparison.OrdinalIgnoreCase)>=0 ||
                    type.IndexOf("Surg",StringComparison.OrdinalIgnoreCase)>=0 ||
                    type.IndexOf("Injector",StringComparison.OrdinalIgnoreCase)>=0 ||
                    type.IndexOf("Food",StringComparison.OrdinalIgnoreCase)>=0) return true;
                string state = player.CurrentStateName.ToString();
                return state.IndexOf("Medical",StringComparison.OrdinalIgnoreCase)>=0 ||
                    state.IndexOf("Heal",StringComparison.OrdinalIgnoreCase)>=0 ||
                    state.IndexOf("Surgery",StringComparison.OrdinalIgnoreCase)>=0;
            }
            catch { return false; }
        }
    }
    internal static class FollowerRecovery
    {
        internal static void SoftReset(BotOwner bot) { try { bot?.Mover?.BotMoverStop("R1-4C71D8C16DD2",@bot); } catch { } }
    }
    internal static class FollowerStateAuthorityCoordinator
    {
        private static readonly Dictionary<string,(float until,string reason)> Faults = new Dictionary<string,(float,string)>(StringComparer.Ordinal);
        internal static bool HasCombatMovementClaim(BotOwner bot, out string reason)
        {
            reason = "";
            if (bot == null) return false;
            if (bot.Memory?.HaveEnemy == true) { reason = "eftEnemy"; return true; }
            if (bot.Memory?.IsUnderFire == true) { reason = "eftUnderFire"; return true; }
            if (bot.WeaponManager?.Grenades?.ThrowindNow == true || bot.WeaponManager?.Grenades?.ReadyToThrow == true)
                { reason = "eftGrenade"; return true; }
            return false;
        }
        internal static bool HasImmediateCombatThreatClaim(BotOwner bot, out string reason) => HasCombatMovementClaim(bot,out reason);
        internal static void ClearLocomotionFault(BotOwner bot,string reason) { if (!string.IsNullOrEmpty(bot?.ProfileId)) Faults.Remove(bot.ProfileId); }
        internal static void MarkLocomotionFault(BotOwner bot,string reason) { if (!string.IsNullOrEmpty(bot?.ProfileId)) Faults[bot.ProfileId] = (Time.time+8f,reason); }
        internal static bool IsLocomotionFaulted(BotOwner bot,out float remaining,out string reason)
        {
            remaining=0;reason="";
            if (bot == null || !Faults.TryGetValue(bot.ProfileId,out var fault)) return false;
            remaining=Mathf.Max(0,fault.until-Time.time);reason=fault.reason;return remaining>0;
        }
        internal static void ArmFollowRecoveryGrace(BotOwner bot,float seconds,string reason) { }
        internal static bool IsHandsRecoveryGuarded(BotOwner bot,out string reason,out float remaining)
        {
            bool grenade=bot?.WeaponManager?.Grenades?.ThrowindNow == true || bot?.WeaponManager?.Grenades?.ReadyToThrow == true;
            reason=grenade?"activeGrenade":"";remaining=grenade?0.3f:0f;return grenade;
        }
        internal static void EnsureGrenadeHandsRecovery(BotOwner bot,string reason) { /* do not seize foreign hands state */ }
    }
}
