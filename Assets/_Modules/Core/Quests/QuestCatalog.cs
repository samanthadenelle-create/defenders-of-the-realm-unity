// =============================================================================
// QuestCatalog — the GENERAL (story / vendor / forgemaster / pet-narrative)
// quest catalog, distinct from the daily-quest system. Loads
// StreamingAssets/Data/Canonical/quests.json (WebGL-safe via CanonicalJson —
// Resources dual-copy wins at load).
// -----------------------------------------------------------------------------
// One file holds:
//   • QuestReward / QuestStage / QuestDef — the JSON shape of quests.json.
//   • QuestCatalogData — the file root ({ version, quests:[…] }).
//   • QuestCatalog — static loader (mirrors DailyQuestCatalog EXACTLY).
//
// SCOPE: data + lookup only. Runtime progress lives in QuestService (reads/writes
// GameState.Quests); reward dispensing happens on the Village side via the
// RewardEarned event (Core never references the wallet).
// =============================================================================

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace DeNelle.Core.Quests
{
    // ── JSON DTOs ────────────────────────────────────────────────────────────

    /// <summary>One stage's reward. Zero / empty slots mean "no reward there".
    /// Economy grants happen on the Village side (QuestRewardBridge) — Core only
    /// carries the numbers.</summary>
    [Serializable]
    public sealed class QuestReward
    {
        [JsonProperty("crystals")] public int Crystals;
        [JsonProperty("food")] public int Food;
        [JsonProperty("magic")] public int Magic;
        [JsonProperty("grantItemId")] public string GrantItemId;
    }

    /// <summary>One ordered step of a quest.</summary>
    [Serializable]
    public sealed class QuestStage
    {
        [JsonProperty("stageId")] public string StageId;
        [JsonProperty("objectiveText")] public string ObjectiveText;
        [JsonProperty("reward")] public QuestReward Reward;
        [JsonProperty("requiresFlag")] public string RequiresFlag;
        [JsonProperty("grantsKeystone")] public bool GrantsKeystone;
    }

    /// <summary>A complete quest definition (a stage chain).</summary>
    [Serializable]
    public sealed class QuestDef
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("title")] public string Title;
        [JsonProperty("stages")] public List<QuestStage> Stages = new List<QuestStage>();
    }

    [Serializable]
    public sealed class QuestCatalogData
    {
        [JsonProperty("version")] public int Version;
        [JsonProperty("quests")] public List<QuestDef> Quests = new List<QuestDef>();
    }

    // ── Loader ───────────────────────────────────────────────────────────────

    /// <summary>Static surface over StreamingAssets/Data/Canonical/quests.json.</summary>
    public static class QuestCatalog
    {
        private const string StreamingRelativePath = "Data/Canonical/quests.json";

        private static QuestCatalogData _data;

        public static IReadOnlyList<QuestDef> Quests
        { get { EnsureLoaded(); return _data.Quests; } }

        public static QuestDef FindQuest(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            EnsureLoaded();
            foreach (var q in _data.Quests) if (q != null && q.Id == id) return q;
            return null;
        }

        /// <summary>Stage list for a quest id (empty list if unknown).</summary>
        public static IReadOnlyList<QuestStage> Stages(string id)
        {
            var q = FindQuest(id);
            return q != null && q.Stages != null ? q.Stages : new List<QuestStage>();
        }

        public static void Reload() { _data = null; EnsureLoaded(); }

        private static void EnsureLoaded()
        {
            if (_data != null) return;
            // WebGL-safe: CanonicalJson reads the Resources dual-copy first (works in
            // a browser build) and falls back to StreamingAssets on desktop. Raw
            // File.ReadAllText would throw in WebGL → empty quest list.
            try
            {
                string text = CanonicalJson.Read(StreamingRelativePath);
                if (!string.IsNullOrEmpty(text))
                {
                    var parsed = JsonConvert.DeserializeObject<QuestCatalogData>(text);
                    if (parsed != null && parsed.Quests != null && parsed.Quests.Count > 0)
                    { _data = parsed; return; }
                    Debug.LogError("[QuestCatalog] quests.json parsed empty.");
                }
                else Debug.LogError($"[QuestCatalog] quests.json not found ({StreamingRelativePath}).");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[QuestCatalog] Failed to read quests.json: {ex.Message}");
            }
            _data = new QuestCatalogData();
        }
    }
}
