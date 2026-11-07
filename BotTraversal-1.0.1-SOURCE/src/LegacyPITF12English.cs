using BepInEx.Configuration;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using BotTraversal.Core;

namespace BotTraversal
{
    // A metadata-only compatibility layer for ORIGINAL PIT V475 F12 settings.
    // It reflects PIT's own embedded English fallback rather than translating any value.
    // Exact field-to-language mapping audited against PIT V475 source.
    internal static class LegacyPITF12English
    {
        private const BindingFlags PublicStatic = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        private static readonly FieldInfo DescriptionField =
            typeof(ConfigEntryBase).GetField("<Description>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        private static float nextRefresh;
        private static bool warned;
        private static readonly Dictionary<string, string> Fields = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            {"scanDistance", "scanDistance"},
            {"patrolRadius", "patrolRadius"},
            {"followDistance", "followDistance"},
            {"enemyRemember", "enemyRemember"},
            {"heatlhMultiplier", "healthMultiplier"},
            {"statusSound", "statusSound"},
            {"enemyMarker", "enemyMarker"},
            {"autoDisplayCombatStatus", "autoDisplayCombatStatus"},
            {"autoDisplayKillMarker", "autoDisplayKillMarker"},
            {"enemyKilledDisplayTime", "enemyKilledDisplayTime"},
            {"enemyKilledRetainTime", "enemyKilledRetainTime"},
            {"pickupEnabled", "pickup"},
            {"tieredPickup", "tieredPickup"},
            {"maximumPickup", "maximumPickup"},
            {"recruitPickup", "recruitPickup"},
            {"teamEscape", "teamEscape"},
            {"teamEscapeUseAnyExtract", "teamEscapeUseAnyExtract"},
            {"lootMinimumPrice", "lootMinimumPrice"},
            {"lootMaximumPrice", "lootMaximumPrice"},
            {"lootFilterFood", "lootFilterFood"},
            {"lootFilterMeds", "lootFilterMeds"},
            {"lootFilterValuables", "lootFilterValuables"},
            {"lootFilterGear", "lootFilterGear"},
            {"lootFilterWeapons", "lootFilterWeapons"},
            {"lootAllowGearSwapping", "lootAllowGearSwapping"},
            {"npcSendMessage", "npcSendMessage"},
            {"pitFireTeamFLAG", "pitFireTeam"},
            {"badGuy", "badGuy"},
            {"factionHostilities", "factionHostilities"},
            {"englishBear", "englishBear"},
            {"pmcArmbands", "pmcArmbands"},
            {"loadoutManagementMode", "loadoutManagement"},
            {"restrictedGearMaintenance", "loadoutManagementRestrictedGearMaintenance"},
            {"botGrenades", "botGrenades"},
            {"regroupRadius", "regroupRadius"},
            {"pingKey", "pingSquad"},
            {"pingRadioVolume", "pingRadioVolume"},
            {"pingTime", "pingTime"},
            {"statusReportHighlight", "statusReportHighlight"},
            {"statusReportHighlightColor", "statusReportHighlightColor"},
            {"statusReportHealthColoring", "statusReportHealthColoring"},
            {"statusReportFullHealthColor", "statusReportFullHealthColor"},
            {"statusReportMediumHealthColor", "statusReportMediumHealthColor"},
            {"statusReportLowHealthColor", "statusReportLowHealthColor"},
            {"statusReportAlwaysHighlight", "statusReportAlwaysHighlight"},
            {"statusReportShowName", "statusReportShowName"},
            {"statusReportShowDistance", "statusReportShowDistance"},
            {"statusReportShowHealth", "statusReportShowHealth"},
            {"statusReportShowTactic", "statusReportShowTactic"},
            {"statusReportShowCombatStatus", "statusReportShowCombatStatus"},
            {"fireteamOverlayEnabled", "fireteamOverlay"},
            {"fireteamOverlayFeetOffset", "fireteamOverlayFeetOffset"},
            {"fireteamPassThroughEnabled", "fireteamPassThrough"},
            {"contactKey", "enemyContact"},
            {"overThereKey", "overThere"},
            {"teleportKey", "botTeleport"},
            {"healKey", "botHeal"},
            {"botPrefetch", "botPrefetch"},
            {"botTalk", "botTalk"},
            {"spawnPoint", "spawnPoint"},
            {"goToDistance", "goToDistance"},
            {"hideUnsupportedCommands", "hideUnsupportedCommands"},
            {"battleRecorderEnabled", "battleRecorder"},
            {"battleRecorderSnapshotIntervalMs", "battleRecorderSnapshotIntervalMs"}
        };

        internal static void Refresh()
        {
            if (!LegacyPITCompatibility.Attached || Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 2f;
            try
            {
                Assembly pit = typeof(LegacyPITF12English).Assembly;
                foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
                    if (string.Equals(a.GetName().Name, "pitFireTeam", StringComparison.OrdinalIgnoreCase))
                    { pit = a; break; }
                Type plugin = pit.GetType("pitTeam.pitFireTeam", false);
                Type englishProvider = pit.GetType("pitTeam.Localization.EmbeddedEnglishLanguageProvider", false);
                object english = englishProvider?.GetProperty("Fallback", PublicStatic)?.GetValue(null, null);
                if (plugin == null || english == null) return;
                foreach (KeyValuePair<string, string> field in Fields)
                {
                    var config = plugin.GetField(field.Key, PublicStatic)?.GetValue(null) as ConfigEntryBase;
                    if (config == null) continue; // PIT may not have bound F12 options yet
                    var language = english.GetType().GetProperty(field.Value, BindingFlags.Public | BindingFlags.Instance)?.GetValue(english, null)
                        as Dictionary<string, string>;
                    if (language == null || !language.TryGetValue("Description", out string description)) continue;
                    language.TryGetValue("Name", out string label);
                    ConfigDescription old = config.Description;
                    if (old == null) continue;
                    object[] tags = old.Tags ?? Array.Empty<object>();
                    foreach (object tag in tags)
                    {
                        FieldInfo display = tag?.GetType().GetField("DispName", BindingFlags.Public | BindingFlags.Instance);
                        if (display != null && display.FieldType == typeof(string) && !string.IsNullOrEmpty(label))
                            display.FieldInfoSetValue("R1-E95405B3AB19",null,tag,label);
                    }
                    if (DescriptionField != null)
                        DescriptionField.FieldInfoSetValue("R1-A95A7DB7BFA5",null,config,new ConfigDescription(description, old.AcceptableValues, tags));
                }
            }
            catch (Exception ex)
            {
                if (!warned) { warned = true; Core.Logger.LogWarning("Legacy PIT F12 English metadata unavailable: " + ex.Message); }
            }
        }
    }
}
