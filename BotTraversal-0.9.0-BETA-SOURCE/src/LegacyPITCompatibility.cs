using EFT;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Diagnostics;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using BotTraversal.Core;

namespace BotTraversal
{
    /// <summary>
    /// Candidate interop for legacy PIT V475 (0.9.1.242) only. Never claim a bot until
    /// old traversal lifecycle hooks are suspended and the old subscriber is removed.
    /// Do not disable PIT combat, Follow, grenade, Marksman, or team creation.
    /// </summary>
    internal static class LegacyPITCompatibility
    {
        private const string LegacyAssembly = "pitFireTeam";
        private const string LegacyVersion = "0.9.1.242";
        private const string PatchId = "bottraversal.legacypit.v475";
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        private static bool attached;
        private static bool originalPITWithoutTraversal;
        private static bool legacyCommandReaderReady;
        private static bool manualTeleportPreparation;
        [ThreadStatic] private static bool originalManualRegroupScope;
        private static bool originalManualRegroupPatchInstalled;
        // CF9: observe the actual original method, not just physical input.
        // A fallback on release is allowed ONLY if PIT has not already handled this frame.
        private static int lastOriginalManualRegroupFrame = -1;
        private static int lastManualRegroupFallbackFrame = -1;
        private static ConfigEntry<KeyboardShortcut> originalTeleportShortcut;
        private static MethodInfo originalTeleportMethod;
        private static Type originalTeleportPluginType;
        // CF10: resolve the legacy Unity component during interop registration, NEVER
        // on the physical F4 release frame. Scene-wide FindObjectOfType may stall the
        // main thread, particularly while the old PIT Awake() has faulted.
        private static UnityEngine.Object originalTeleportPluginComponent;
        private static float nextManualRegroupDiagnostic;
        private static float originalManualRegroupGraceUntil;
        internal static bool IsOriginalManualRegroupInProgress => originalManualRegroupScope ||
            Time.unscaledTime < originalManualRegroupGraceUntil;
        private static readonly Dictionary<string, float> ManualTeleportCooldown = new Dictionary<string, float>(StringComparer.Ordinal);
        internal static bool SupportsOriginalFollowBridge => originalPITWithoutTraversal && legacyCommandReaderReady;
        internal static bool OriginalPITWithoutTraversal => originalPITWithoutTraversal;
        private static bool warned;
        private static float nextAttempt;
        private static Type collisionType;
        internal static bool Attached => attached;

        private static bool BlockLegacyRuntime() => false;

        internal static void TryAttach()
        {
            if (attached || originalPITWithoutTraversal || Time.unscaledTime < nextAttempt) return;
            nextAttempt = Time.unscaledTime + 1.0f;
            Assembly legacy = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a =>
                string.Equals(a.GetName().Name, LegacyAssembly, StringComparison.OrdinalIgnoreCase));
            if (legacy == null) return;
            Type runtime = legacy.GetType("pitTeam.Modules.FollowerTraversalTestRuntime", false);
            // The separately supplied original PIT beta 0.9.1 has NO embedded Traversal runtime,
            // unlike V475. It needs a BT Follow consumer rather than lifecycle suspension.
            if (runtime == null)
            {
                Type originalPlugin = legacy.GetType("pitTeam.pitFireTeam", false);
                bool isOriginalBeta = HasPluginIdentity(originalPlugin, "0.9.1") &&
                    legacy.GetType("pitTeam.Modules.BossPlayers", false) != null &&
                    legacy.GetType("pitTeam.Components.BotFollowerPlayer", false) != null;
                if (isOriginalBeta)
                {
                    originalPITWithoutTraversal = true;
                    InstallOriginalPITCommandReader(legacy);
                    InstallOriginalPITManualRegroupHook(legacy);
                    Core.Logger.LogInfo("[Legacy PIT] Original beta 0.9.1 detected: no embedded Traversal runtime; enabling BT learned-route Follow consumer. Original PIT keeps combat, commands and F4.");
                }
                return;
            }
            // PIT V475 deliberately sets AssemblyVersion=0.9.1.0; the actual build
            // identity is the BepInPlugin attribute, not the CLR assembly version.
            Type plugin = legacy.GetType("pitTeam.pitFireTeam", false);
            bool exactV475 = HasPluginIdentity(plugin, LegacyVersion);
            if (!exactV475)
            {
                WarnOnce("Legacy PIT plugin identity is not V475/0.9.1.242; original PIT remains its own Traversal owner.");
                return;
            }
            Type hub = legacy.GetType("pitTeam.Modules.BotOwnerUpdateHub", false);
            collisionType = legacy.GetType("pitTeam.Components.FireteamSoftCollision", false);
            MethodInfo unsubscribe = hub?.GetMethod("Unregister", Flags, null, new[] { typeof(string) }, null);
            MethodInfo shutdown = runtime.GetMethod("Shutdown", Flags, null, Type.EmptyTypes, null);
            MethodInfo prefix = typeof(LegacyPITCompatibility).GetMethod(nameof(BlockLegacyRuntime), Flags);
            string[] lifecycle = { "Initialize", "EnsureRaidStarted", "StartRaid", "TickMainThread", "OnBotOwnerUpdate" };
            MethodInfo[] target = lifecycle.Select(name => runtime.GetMethods(Flags).SingleOrDefault(method => method.Name == name)).ToArray();
            if (unsubscribe == null || shutdown == null || prefix == null || collisionType == null || target.Any(x => x == null))
            {
                WarnOnce("Legacy PIT V475 interop contract incomplete; preserving original owner and blocking BT takeover.");
                return;
            }
            // Strict preflight completed before any side effects. The legacy source is known
            // to register one UpdateHub callback and to restart via these lifecycle methods.
            var harmony = new Harmony(PatchId);
            var patched = new List<MethodInfo>();
            try
            {
                foreach (MethodInfo method in target)
                {
                    harmony.Patch(method, prefix: new HarmonyMethod(prefix));
                    patched.Add(method);
                }
                shutdown.MethodBaseInvoke("R1-6933DC37A6FA",null,null,null); // safely close old raid and unsubscribe legacy callback
                unsubscribe.MethodBaseInvoke("R1-463496177759",null,null,new object[] { "pitTeam.FollowerTraversalTestRuntime" });
                attached = true;
                InstallOriginalPITCommandReader(legacy);
                Core.Logger.LogInfo("Legacy PIT V475 Traversal suspended; Bot Traversal is now the sole Traversal lifecycle owner (candidate; runtime validation required).");
                EnforceCollisionSingleOwner();
            }
            catch (Exception ex)
            {
                // Roll back all interception patches if any step fails. Never leave
                // a half-installed legacy bridge silently controlling movement.
                foreach (MethodInfo method in patched)
                {
                    try { harmony.Unpatch(method, HarmonyPatchType.Prefix, PatchId); }
                    catch (Exception rollbackError) { Core.Logger.LogError("Legacy PIT rollback failed: " + rollbackError); }
                }
                Core.Logger.LogError("Legacy PIT takeover failed; refusing dual Traversal ownership. Restart the game before trying again: " + ex);
                attached = false;
                warned = true;
            }
        }

        private static bool HasPluginIdentity(Type plugin, string version)
        {
            return plugin != null && plugin.GetCustomAttributesData().Any(attribute =>
                attribute.AttributeType.FullName == "BepInEx.BepInPlugin" &&
                attribute.ConstructorArguments.Count >= 3 &&
                string.Equals(attribute.ConstructorArguments[0].Value as string,
                    "xyz.pit.fireteam", StringComparison.Ordinal) &&
                string.Equals(attribute.ConstructorArguments[2].Value as string,
                    version, StringComparison.Ordinal));
        }

        // Preserve PIT's explicit Hold/Move/Combat commands. The enum is from the OLD
        // assembly, so never cast it to Bot Traversal's enum or assume matching ordinal IDs.
        private static void InstallOriginalPITCommandReader(Assembly legacy)
        {
            try
            {
                Type bossPlayers = legacy.GetType("pitTeam.Modules.BossPlayers", false);
                Type followerType = legacy.GetType("pitTeam.Components.BotFollowerPlayer", false);
                MethodInfo lookup = bossPlayers?.GetMethod("GetFollowerByProfileId", Flags, null, new[] { typeof(string) }, null);
                MethodInfo peek = followerType?.GetMethod("TryPeekActiveCommand",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (lookup == null || peek == null || peek.GetParameters().Length != 3)
                {
                    Core.Logger.LogWarning("[Legacy PIT] No command-reader contract; BT's independent Follow bridge will remain disabled to protect Hold/Move orders.");
                    originalPITWithoutTraversal = false;
                    return;
                }
                FollowerEnvironment.RegisterCommandProvider(id =>
                {
                    try
                    {
                        object follower = lookup.MethodBaseInvoke("R1-3FD49A852843",null,null,new object[] { id });
                        if (follower == null) return 0;
                        object[] args = { null, Vector3.zero, 0f };
                        bool present = peek.MethodBaseInvoke("R1-51EAFE1DF656",null,follower,args) is bool result && result;
                        return present ? (int)FollowerCommandType.HoldPosition : 0;
                    }
                    catch { return 0; }
                });
                legacyCommandReaderReady = true;
            }
            catch (Exception ex)
            {
                originalPITWithoutTraversal = false;
                Core.Logger.LogWarning("[Legacy PIT] Command reader unavailable; refusing independent Follow takeover: " + ex.Message);
            }
        }

        // CF8: intercept the *actual* original PIT command boundary rather than guessing
        // from Unity F4 input or a reflection StackTrace inside a movement-writer prefix.
        private static void InstallOriginalPITManualRegroupHook(Assembly legacy)
        {
            if (originalManualRegroupPatchInstalled) return;
            try
            {
                Type plugin = legacy.GetType("pitTeam.pitFireTeam", false);
                MethodInfo command = plugin?.GetMethod("_BotTeleport",
                    BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                MethodInfo prefix = typeof(LegacyPITCompatibility).GetMethod(nameof(OriginalManualRegroupPrefix), Flags);
                MethodInfo finalizer = typeof(LegacyPITCompatibility).GetMethod(nameof(OriginalManualRegroupFinalizer), Flags);
                if (command == null || prefix == null || finalizer == null ||
                    command.ReturnType != typeof(void) || command.GetParameters().Length != 0)
                {
                    Core.Logger.LogWarning("[Legacy PIT F4] Original _BotTeleport(void) contract unavailable; no unsafe key-based takeover.");
                    return;
                }
                new Harmony("bottraversal.legacypit.manualregroup").Patch(command,
                    prefix: new HarmonyMethod(prefix), finalizer: new HarmonyMethod(finalizer));
                originalManualRegroupPatchInstalled = true;
                originalTeleportMethod = command;
                originalTeleportPluginType = plugin;
                // One-time source-verified lookup. If PIT initialization failed, do not
                // turn each user F4 press into another whole-scene component search.
                originalTeleportPluginComponent = UnityEngine.Object.FindObjectOfType(plugin);
                originalTeleportShortcut = plugin.GetField("teleportKey", Flags)?.GetValue(null)
                    as ConfigEntry<KeyboardShortcut>;
                Core.Logger.LogInfo("[Legacy PIT F4] Original _BotTeleport entry/finalizer instrumentation installed.");
            }
            catch (Exception ex)
            {
                Core.Logger.LogError("[Legacy PIT F4] Could not instrument original PIT manual regroup: " + ex);
            }
        }

        private static void OriginalManualRegroupPrefix()
        {
            lastOriginalManualRegroupFrame = Time.frameCount;
            originalManualRegroupScope = true;
            originalManualRegroupGraceUntil = Time.unscaledTime + 2f;
            Core.Logger.LogInfo("[Legacy PIT F4] Original _BotTeleport ENTER: explicit PIT manual command owns movement.");
        }

        private static Exception OriginalManualRegroupFinalizer(Exception __exception)
        {
            originalManualRegroupScope = false;
            if (__exception != null)
                Core.Logger.LogError("[Legacy PIT F4] Original _BotTeleport THREW: " + __exception);
            else
                Core.Logger.LogInfo("[Legacy PIT F4] Original _BotTeleport RETURNED; check bot displacement (not proof of teleport success).");
            return __exception; // preserve original PIT error semantics; never swallow exceptions
        }

        // Only the original PIT beta's _BotTeleport call can use this escape hatch.
        // On F4 it has already selected a destination; prepare the BT transaction exactly
        // once before the old implementation writes Stop/Player.Teleport. Never make all
        // ordinary programmatic teleports or movement writers fail-open.
        internal static bool PermitOriginalManualRegroup(BotOwner bot, string writeKind)
        {
            if (!originalPITWithoutTraversal || bot == null || !FollowerEnvironment.Owns(bot) ||
                (writeKind != "BotMover.Stop" && writeKind != "Player.Teleport")) return false;
            // Scope is installed on the original PIT method itself. Input.GetKeyDown(F4)
            // is insufficient evidence: it may not be PIT's action or may be UI-consumed.
            if (!originalManualRegroupScope || !originalManualRegroupPatchInstalled) return false;
            if (manualTeleportPreparation) return true;
            string key = bot.ProfileId ?? string.Empty;
            if (string.IsNullOrEmpty(key)) return false;
            if (!ManualTeleportCooldown.TryGetValue(key, out float next) || Time.unscaledTime >= next)
            {
                ManualTeleportCooldown[key] = Time.unscaledTime + 0.65f;
                manualTeleportPreparation = true;
                try
                {
                    FollowerTraversalTestRuntime.PrepareForManualRegroup(bot);
                    Core.Logger.LogInfo("[Legacy PIT F4] Manual regroup authority yielded to original PIT for follower " + key);
                }
                catch (Exception ex) { Core.Logger.LogWarning("[Legacy PIT F4] BT preparation failed: " + ex.Message); }
                finally { manualTeleportPreparation = false; }
            }
            return true;
        }

        internal static void DiagnosePhysicalF4()
        {
            if (!Input.GetKeyDown(KeyCode.F4)) return;
            try
            {
                Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a =>
                    string.Equals(a.GetName().Name, LegacyAssembly, StringComparison.OrdinalIgnoreCase));
                Type plugin = assembly?.GetType("pitTeam.pitFireTeam", false);
                object entry = plugin?.GetField("teleportKey", Flags)?.GetValue(null);
                PropertyInfo valueProperty = entry?.GetType().GetProperty("Value", BindingFlags.Instance | BindingFlags.Public);
                object shortcut = valueProperty?.GetValue(entry, null);
                Core.Logger.LogInfo("[Legacy PIT F4] Physical F4 detected; PIT configured BotTeleport shortcut=" +
                    (shortcut?.ToString() ?? "<unavailable>") +
                    "; BT original-beta Follow bridge=" + originalPITWithoutTraversal +
                    "; V475 ownership handoff=" + attached);
            }
            catch (Exception ex) { Core.Logger.LogWarning("[Legacy PIT F4] Key diagnostic failed: " + ex.Message); }
        }

        // CF9: PIT beta 0.9.1 consumes KeyboardShortcut.IsUp() only from the
        // plugin's Update() conditional chain. BepInEx 5 exact-combination
        // filtering can reject F4 releases while unrelated movement/sprint keys
        // are held. Only deliver the ORIGINAL PIT command, at the end of the
        // Unity Update frame, and only when that command did not already run.
        internal static void DeliverMissingOriginalF4OnRelease()
        {
            if (!originalPITWithoutTraversal || !legacyCommandReaderReady ||
                !originalManualRegroupPatchInstalled || originalTeleportMethod == null ||
                originalTeleportPluginType == null || originalTeleportShortcut == null)
                return;
            int frame = Time.frameCount;
            if (frame == lastManualRegroupFallbackFrame || frame == lastOriginalManualRegroupFrame)
                return;
            KeyboardShortcut shortcut = originalTeleportShortcut.Value;
            if (shortcut.MainKey != KeyCode.F4 || !Input.GetKeyUp(KeyCode.F4))
                return;
            bool pitExactShortcutRelease = shortcut.IsUp();
            // Treat Shift as a normal gameplay sprint key for a plain F4 binding.
            // Explicit Ctrl/Alt bindings are never silently broadened.
            bool looseGameplayRelease = IsOriginalF4GameplayChord(shortcut);
            if (!pitExactShortcutRelease && !looseGameplayRelease)
                return;

            GameWorld world = Comfort.Common.Singleton<GameWorld>.Instance;
            Player player = GamePlayerOwner.MyPlayer;
            if (world?.MainPlayer == null || player == null ||
                player.HealthController?.IsAlive != true ||
                player.ProfileId != world.MainPlayer.ProfileId)
                return;

            // Do not take ownership of PIT's execution or silently teleport bots
            // ourselves. Invoke its existing private method, which keeps its
            // formation spacing, interaction, health, Fika and grace guards.
            // No scene traversal / reflection scan in the key-release hot path. The
            // original component either survived PIT Awake or F4 must remain PIT-owned.
            UnityEngine.Object original = originalTeleportPluginComponent;
            if (original == null)
            {
                TraceManualRegroupDiagnostic("original PIT plugin component not found; no fallback");
                return;
            }
            lastManualRegroupFallbackFrame = frame;
            try
            {
                Core.Logger.LogInfo("[Legacy PIT F4] CF9 dispatching original _BotTeleport on verified F4 release" +
                    " (PIT did not enter this frame; exactShortcut=" + pitExactShortcutRelease +
                    "; gameplayChord=" + looseGameplayRelease + ").");
                originalTeleportMethod.MethodBaseInvoke("R1-3C63BA1560CA",null,original,null);
            }
            catch (TargetInvocationException ex)
            {
                Core.Logger.LogError("[Legacy PIT F4] CF9 original PIT command threw: " +
                    (ex.InnerException ?? ex));
            }
            catch (Exception ex)
            {
                Core.Logger.LogError("[Legacy PIT F4] CF9 fallback dispatch failed: " + ex);
            }
        }

        private static bool IsOriginalF4GameplayChord(KeyboardShortcut shortcut)
        {
            if (shortcut.MainKey != KeyCode.F4) return false;
            // Respect any explicit user-configured modifier keys.
            KeyCode[] required = shortcut.Modifiers.ToArray();
            foreach (KeyCode key in required)
                if (!Input.GetKey(key)) return false;
            // A plain F4 must not steal Alt+F4 or Ctrl+F4. Shift is permitted
            // during sprint, which is a common cause of the original IsUp miss.
            KeyCode[] ctrlAlt = { KeyCode.LeftControl, KeyCode.RightControl,
                KeyCode.LeftAlt, KeyCode.RightAlt };
            foreach (KeyCode key in ctrlAlt)
                if (Input.GetKey(key) && !required.Contains(key)) return false;
            // If the user explicitly bound Shift+F4, the test above requires it;
            // otherwise ordinary sprint Shift must not suppress manual regroup.
            return true;
        }

        private static void TraceManualRegroupDiagnostic(string reason)
        {
            if (Time.unscaledTime < nextManualRegroupDiagnostic) return;
            nextManualRegroupDiagnostic = Time.unscaledTime + 3f;
            Core.Logger.LogWarning("[Legacy PIT F4] " + reason);
        }

        internal static void EnforceCollisionSingleOwner()
        {
            if (!attached) return;
            try
            {
                GameObject oldHelpers = GameObject.Find("PitFireTeam_RuntimeHelpers");
                if (oldHelpers == null) return;
                foreach (MonoBehaviour component in oldHelpers.GetComponents<MonoBehaviour>())
                    if (component != null && component.enabled && collisionType.IsInstanceOfType(component))
                        component.SetBehaviourenabled("R1-D79CBD483E24",null,false); // original OnDisable restores its own collider pairs
            }
            catch (Exception ex) { WarnOnce("Legacy collision handoff could not be verified: " + ex.Message); }
        }

        private static void WarnOnce(string message)
        {
            if (warned) return;
            warned = true;
            Core.Logger.LogWarning(message);
        }
    }
}
