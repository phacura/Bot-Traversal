using BepInEx;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using BotTraversal.Core;

namespace BotTraversal
{
    [BepInPlugin("bottraversal.standalone", "Bot Traversal", "0.9.0")]
    public sealed class BotTraversalPlugin : BaseUnityPlugin
    {
        internal static ConfigEntry<bool> TraversalEnabled;
        internal static ConfigEntry<bool> PassThroughEnabled;
        internal static ConfigEntry<bool> DebugLogEnabled;
        internal static bool LegacyPITCompatibilityEnabled => true;
        internal static string DataRoot;
        private GenericSoftCollision collisions;
        private string map;
        private bool active;
        private float nextScan;
        private float missingFollowersSince = -1f;
        private const float MissingFollowerGraceSeconds = 3f;
        private List<BotOwner> followers = new List<BotOwner>();
        private static ConfigDescription F12(string description, string label, int order) =>
            new ConfigDescription(description, null, new ConfigurationManagerAttributes { DispName = label, Order = order });
        private void Awake()
        {
            Core.Logger.Instance = Logger;
            Logger.LogInfo("Bot Traversal 0.9.0 Beta 18 loaded: V2-RC155-Beta18");
            TraversalEnabled = Config.Bind("Bot Traversal", "01 Traversal功能", true,
                F12("Enable full Traversal, including learned routes, off-NavMesh movement, Vault, Jump, Climb, WalkThrough and DropDown.", "01 Traversal", -1));
            PassThroughEnabled = Config.Bind("Bot Traversal", "02 友軍互相穿透", true,
                F12("Allow registered friendly bots to pass through the player and one another. Friendly mover repulsion is disabled. Stationary overlaps gently push the local player away. When off, shared traversal entries are queued.", "02 Friendly Pass-Through", -2));
            DebugLogEnabled = Config.Bind("Bot Traversal", "03 Debug Log寫入", false,
                F12("Write detailed JSONL Traversal diagnostics. This can generate large log files.", "03 Debug Logging", -3));
            Config.Remove(new ConfigDefinition("Bot Traversal", "04 Legacy PIT Compatibility"));
            DataRoot=Path.Combine(BepInEx.Paths.PluginPath,"BotTraversal","Data");
            Directory.CreateDirectory(Path.Combine(DataRoot,"Learning"));
            Directory.CreateDirectory(Path.Combine(DataRoot,"Logs"));
            collisions=gameObject.AddComponent<GenericSoftCollision>();
            // Patches belong to this assembly; PIT must not also apply them.
            try
            {
                new FollowerTraversalTestBotMoverPatch().Enable();
                new FollowerTraversalTestBotMoverRelinkPatch().Enable();
                new FollowerTraversalTestMotionRelinkPatch().Enable();
                new FollowerTraversalTestSetPlayerToNavMeshPatch().Enable();
                new FollowerTraversalTestBotMoverBodyRelinkPatch().Enable();
                FollowerMovementWriterAuthorityPatch.Apply(new HarmonyLib.Harmony("bottraversal.authority"));
                FollowerFriendlyAvoidancePatch.Apply(new HarmonyLib.Harmony("bottraversal.friendlyAvoidance"));
                FollowerTraversalTestRuntime.Initialize();
            }
            catch(Exception e) { Logger.LogError("BotTraversal patches failed: "+e); }
            TraversalEnabled.SettingChanged+=SettingsChanged;
            DebugLogEnabled.SettingChanged+=SettingsChanged;
            PassThroughEnabled.SettingChanged+=SettingsChanged;
            WriteOptions();
        }
        private void SettingsChanged(object sender,EventArgs e)
        {
            WriteOptions();
            if(!TraversalEnabled.Value && active) { FollowerTraversalTestRuntime.EndRaid("toggleOff");active=false; }
            // Runtime diagnostic logging must be independently gated by the migrated diagnostics code.
        }
        private void WriteOptions()
        {
            try
            {
                string path=Path.Combine(DataRoot,"bottraversal.options.json");
                string tmp=path+".tmp";
                File.WriteAllText(tmp,"{\"Traversal\":"+TraversalEnabled.Value.ToString().ToLowerInvariant()+
                    ",\"PassThrough\":"+PassThroughEnabled.Value.ToString().ToLowerInvariant()+
                    ",\"DebugLog\":"+DebugLogEnabled.Value.ToString().ToLowerInvariant()+"}");
                if(File.Exists(path)) File.Delete(path);
                File.Move(tmp,path);
            }
            catch(Exception e) { Logger.LogWarning("Options write failed: "+e.Message); }
        }
        private void Update()
        {
            using var phase = FollowerControlBackend.EnterPhase(ControlUpdatePhase.MainUpdate);
            try
            {
                // Attach only to the source-verified V475 legacy runtime. Unknown versions
                // remain untouched, avoiding two simultaneous movement producers.
                if (TraversalEnabled.Value && LegacyPITCompatibilityEnabled)
                    LegacyPITCompatibility.TryAttach();
                LegacyPITCompatibility.DiagnosePhysicalF4();
                var world=Singleton<GameWorld>.Instance;
                if(world?.MainPlayer?.HealthController?.IsAlive!=true)
                {
                    if(active) FollowerTraversalTestRuntime.EndRaid("worldGone");
                    active=false;map=null;followers.Clear();missingFollowersSince=-1f;
                    collisions.SetParticipants(null,null);
                    FollowerEnvironment.Clear();
                    LegacyOriginalPITFollowBridge.Reset();return;
                }
                if(Time.unscaledTime>=nextScan)
                {
                    nextScan=Time.unscaledTime+0.12f;
                    LegacyPITCompatibility.EnforceCollisionSingleOwner();
                    LegacyPITF12English.Refresh();
                    followers=FollowerEnvironment.FindFollowers();
                    collisions.SetParticipants(PassThroughEnabled.Value?world.MainPlayer:null,
                        PassThroughEnabled.Value?followers:null);
                }
                if(!TraversalEnabled.Value)
                {
                    if(active){FollowerTraversalTestRuntime.EndRaid("traversalDisabled");active=false;}
                    missingFollowersSince=-1f;
                    return;
                }
                if(followers.Count==0)
                {
                    // A short Fika spawn/despawn or registration gap is not a raid boundary.
                    // Pause processing and keep the current raid's diagnostics generation.
                    if(missingFollowersSince<0f) missingFollowersSince=Time.unscaledTime;
                    if(active && Time.unscaledTime-missingFollowersSince>=MissingFollowerGraceSeconds)
                    { FollowerTraversalTestRuntime.EndRaid("noFollowersSustained");active=false; }
                    return;
                }
                missingFollowersSince=-1f;
                if(!active || map!=world.LocationId)
                {
                    map=world.LocationId;
                    FollowerTraversalTestRuntime.StartRaid(map);
                    active=true;
                }
                FollowerTraversalTestRuntime.TickMainThread();
                // Every registered follower receives one update per frame; no PIT bot list is consumed.
                foreach(BotOwner bot in followers)
                    if(FollowerEnvironment.Owns(bot))
                    {
                        BotOwnerUpdateHub.Invoke(bot);
                        if (LegacyPITCompatibilityEnabled)
                            LegacyOriginalPITFollowBridge.Tick(bot);
                    }
            }
            catch(Exception e) { Logger.LogError("BotTraversal main update: "+e); }
        }
        // PIT's own Update has already had a chance to process this frame's
        // key-up. The fallback is restricted to the legacy beta and the exact
        // original PIT private command, and never touches Traversal's writer.
        private void LateUpdate()
        {
            try
            {
                if (LegacyPITCompatibilityEnabled)
                    LegacyPITCompatibility.DeliverMissingOriginalF4OnRelease();
            }
            catch (Exception e) { Logger.LogError("Original PIT F4 release bridge: " + e); }
        }

        private void OnDestroy()
        {
            try { if(active)FollowerTraversalTestRuntime.EndRaid("pluginDestroyed");
                FollowerTraversalTestRuntime.Shutdown();BotOwnerUpdateHub.Clear();
                collisions?.SetParticipants(null,null); }
            catch(Exception e) { Logger.LogError(e); }
        }
    }
}
