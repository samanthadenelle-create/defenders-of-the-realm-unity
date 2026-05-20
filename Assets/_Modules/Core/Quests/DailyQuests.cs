// =============================================================================
// DailyQuests — three-slot daily-quest system ported from
// docs/daily-quests-spec.md (React agent worktree).
// -----------------------------------------------------------------------------
// One file holds:
//   • DailyQuestTemplate / DailyQuestCatalogData — the JSON shape of
//     daily-quests.json under StreamingAssets/Data/Canonical/.
//   • DailyQuestCatalog — static loader (mirrors PetCatalog).
//   • DailyQuestInstance — one rolled quest at runtime with progress.
//   • DailyQuestSet — today's three quests + reroll count + date stamp.
//   • DailyQuestService — singleton that rolls today's set at first access,
//     reads/writes the set from PlayerPrefs (PER-DAY scope — missing a day
//     just rolls fresh), and exposes Report(eventId, amount) so gameplay code
//     can tick progress without naming the manager.
//
// SCOPE: Week-1 skeleton. UI panel + reward dispensing + per-event hooks land
// in follow-up tasks. The service is wired and survives scene loads so the
// HUD bridge can subscribe today.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace DeNelle.Core.Quests
{
    // ── JSON DTOs ────────────────────────────────────────────────────────────

    [Serializable]
    public sealed class DailyQuestTemplate
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("slot")] public string Slot;
        [JsonProperty("target")] public int Target;
        [JsonProperty("label")] public string Label;
        [JsonProperty("weight")] public float Weight = 1f;
        [JsonProperty("requiresHero")] public string RequiresHero;
        [JsonProperty("requiresFeature")] public string RequiresFeature;
    }

    [Serializable]
    public sealed class DailyQuestSlotReward
    {
        [JsonProperty("slot")] public string Slot;
        [JsonProperty("rewardCrystals")] public int RewardCrystals;
        [JsonProperty("rewardFood")] public int RewardFood;
        [JsonProperty("rewardGlimmer")] public int RewardGlimmer;
        [JsonProperty("rewardWisdom")] public int RewardWisdom;
        [JsonProperty("rewardRandomItem")] public bool RewardRandomItem;
    }

    [Serializable]
    public sealed class DailyQuestCatalogData
    {
        [JsonProperty("version")] public int Version;
        [JsonProperty("slotCount")] public int SlotCount = 3;
        [JsonProperty("rerollsFreePerDay")] public int RerollsFreePerDay = 1;
        [JsonProperty("rerollCostCrystals")] public int RerollCostCrystals = 50;
        [JsonProperty("rerollsMaxPerDay")] public int RerollsMaxPerDay = 3;
        [JsonProperty("slots")] public List<DailyQuestSlotReward> Slots = new List<DailyQuestSlotReward>();
        [JsonProperty("templates")] public List<DailyQuestTemplate> Templates = new List<DailyQuestTemplate>();
    }

    // ── Loader ───────────────────────────────────────────────────────────────

    /// <summary>Static surface over StreamingAssets/Data/Canonical/daily-quests.json.</summary>
    public static class DailyQuestCatalog
    {
        private const string StreamingRelativePath = "Data/Canonical/daily-quests.json";

        private static DailyQuestCatalogData _data;

        public static IReadOnlyList<DailyQuestTemplate> Templates
        { get { EnsureLoaded(); return _data.Templates; } }

        public static IReadOnlyList<DailyQuestSlotReward> Slots
        { get { EnsureLoaded(); return _data.Slots; } }

        public static int RerollsFreePerDay  { get { EnsureLoaded(); return _data.RerollsFreePerDay; } }
        public static int RerollCostCrystals { get { EnsureLoaded(); return _data.RerollCostCrystals; } }
        public static int RerollsMaxPerDay   { get { EnsureLoaded(); return _data.RerollsMaxPerDay; } }

        public static DailyQuestSlotReward RewardFor(string slot)
        {
            EnsureLoaded();
            foreach (var s in _data.Slots) if (s.Slot == slot) return s;
            return null;
        }

        public static DailyQuestTemplate FindTemplate(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            EnsureLoaded();
            foreach (var t in _data.Templates) if (t.Id == id) return t;
            return null;
        }

        public static void Reload() { _data = null; EnsureLoaded(); }

        private static void EnsureLoaded()
        {
            if (_data != null) return;
            var full = Path.Combine(Application.streamingAssetsPath, StreamingRelativePath);
            try
            {
                if (File.Exists(full))
                {
                    var parsed = JsonConvert.DeserializeObject<DailyQuestCatalogData>(File.ReadAllText(full));
                    if (parsed != null && parsed.Templates != null && parsed.Templates.Count > 0)
                    { _data = parsed; return; }
                    Debug.LogError($"[DailyQuestCatalog] daily-quests.json at {full} parsed empty.");
                }
                else Debug.LogError($"[DailyQuestCatalog] daily-quests.json not found at {full}.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DailyQuestCatalog] Failed to read {full}: {ex.Message}");
            }
            _data = new DailyQuestCatalogData();
        }
    }

    // ── Runtime models ───────────────────────────────────────────────────────

    [Serializable]
    public sealed class DailyQuestInstance
    {
        public string Id;            // unique per slot per day
        public string TemplateId;
        public string Slot;
        public int Target;
        public int Progress;
        public bool Completed;
        public long ClaimedAtUnix;   // 0 = not yet claimed
        public string Label;         // resolved at roll time

        public float ProgressFraction => Target > 0 ? Mathf.Clamp01((float)Progress / Target) : 0f;
    }

    [Serializable]
    public sealed class DailyQuestSet
    {
        public string Date;          // YYYY-MM-DD (local)
        public int RerollsUsed;
        public List<DailyQuestInstance> Quests = new List<DailyQuestInstance>();
    }

    // ── Service ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Singleton service that holds today's three quests, persists them to
    /// PlayerPrefs, and exposes Report() so combat / exploration code can tick
    /// progress without naming this class directly.
    ///
    /// Reset rule: at the first access of a new local-date the prior day's
    /// set is replaced — no streak guilt, no FOMO, matching the spec.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DailyQuestService : MonoBehaviour
    {
        private const string PrefKey = "dotr-daily-quests-v1";

        public static DailyQuestService Instance { get; private set; }
        public event Action SetChanged;

        private DailyQuestSet _today;
        private System.Random _rng;

        public DailyQuestSet Today
        {
            get
            {
                EnsureToday();
                return _today;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("DailyQuestService");
            UnityEngine.Object.DontDestroyOnLoad(go);
            Instance = go.AddComponent<DailyQuestService>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _rng = new System.Random();
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Increments progress for every quest whose template id starts with
        /// <paramref name="eventId"/>. Lets gameplay code post once per event
        /// — "combat.clear-waves" matches all combat clear-wave templates.
        /// </summary>
        public void Report(string eventId, int amount = 1)
        {
            if (string.IsNullOrEmpty(eventId) || amount <= 0) return;
            EnsureToday();
            bool changed = false;
            foreach (var q in _today.Quests)
            {
                if (q == null || q.Completed) continue;
                if (q.TemplateId == eventId || q.TemplateId.StartsWith(eventId + "."))
                {
                    q.Progress = Mathf.Min(q.Target, q.Progress + amount);
                    if (q.Progress >= q.Target) q.Completed = true;
                    changed = true;
                }
            }
            if (changed) { Save(); SetChanged?.Invoke(); }
        }

        /// <summary>
        /// Spends a re-roll on the given slot (free up to RerollsFreePerDay,
        /// then RerollCostCrystals each, capped at RerollsMaxPerDay). Returns
        /// the new quest, or null if the re-roll was denied.
        /// </summary>
        public DailyQuestInstance Reroll(string slot, Func<int, bool> spendCrystals = null)
        {
            EnsureToday();
            if (_today.RerollsUsed >= DailyQuestCatalog.RerollsMaxPerDay) return null;

            bool free = _today.RerollsUsed < DailyQuestCatalog.RerollsFreePerDay;
            if (!free && spendCrystals != null && !spendCrystals(DailyQuestCatalog.RerollCostCrystals))
                return null;

            int idx = _today.Quests.FindIndex(q => q != null && q.Slot == slot);
            if (idx < 0) return null;
            var roll = RollOne(slot, exclude: _today.Quests[idx]?.TemplateId);
            if (roll == null) return null;
            _today.Quests[idx] = roll;
            _today.RerollsUsed++;
            Save();
            SetChanged?.Invoke();
            return roll;
        }

        /// <summary>Forces a fresh roll (used by AdminOverlay / dev tools).</summary>
        public void ForceRollToday()
        {
            _today = RollSet(LocalDateString());
            Save();
            SetChanged?.Invoke();
        }

        // ── Internals ────────────────────────────────────────────────────────

        private void EnsureToday()
        {
            string today = LocalDateString();
            if (_today != null && _today.Date == today) return;
            if (TryLoad(out var loaded) && loaded != null && loaded.Date == today)
            { _today = loaded; return; }
            _today = RollSet(today);
            Save();
            SetChanged?.Invoke();
        }

        private DailyQuestSet RollSet(string date)
        {
            var set = new DailyQuestSet { Date = date, RerollsUsed = 0 };
            foreach (var slot in new[] { "combat", "exploration", "wildcard" })
            {
                var q = RollOne(slot, exclude: null);
                if (q != null) set.Quests.Add(q);
            }
            return set;
        }

        private DailyQuestInstance RollOne(string slot, string exclude)
        {
            var pool = new List<DailyQuestTemplate>();
            float totalWeight = 0f;
            foreach (var t in DailyQuestCatalog.Templates)
            {
                if (t == null || t.Slot != slot) continue;
                if (exclude != null && t.Id == exclude) continue;
                // Skip templates whose required feature isn't shipped (week-7).
                if (!string.IsNullOrEmpty(t.RequiresFeature) && !FeatureShipped(t.RequiresFeature)) continue;
                pool.Add(t);
                totalWeight += Mathf.Max(0.01f, t.Weight);
            }
            if (pool.Count == 0) return null;

            float pick = (float)_rng.NextDouble() * totalWeight;
            DailyQuestTemplate chosen = pool[0];
            foreach (var t in pool)
            {
                pick -= Mathf.Max(0.01f, t.Weight);
                if (pick <= 0f) { chosen = t; break; }
            }

            return new DailyQuestInstance
            {
                Id = chosen.Id + "@" + LocalDateString(),
                TemplateId = chosen.Id,
                Slot = chosen.Slot,
                Target = chosen.Target,
                Progress = 0,
                Completed = false,
                ClaimedAtUnix = 0,
                Label = chosen.Label,
            };
        }

        private static bool FeatureShipped(string feature) => feature switch
        {
            "harvesting"    => false,
            "tower-build"   => false,
            "cosmetic-shop" => false,
            "hero-talents"  => false,
            _ => true,
        };

        private static string LocalDateString() => DateTime.Now.ToString("yyyy-MM-dd");

        private bool TryLoad(out DailyQuestSet set)
        {
            set = null;
            if (!PlayerPrefs.HasKey(PrefKey)) return false;
            try { set = JsonConvert.DeserializeObject<DailyQuestSet>(PlayerPrefs.GetString(PrefKey)); }
            catch (Exception ex) { Debug.LogWarning("[DailyQuestService] load failed: " + ex.Message); }
            return set != null;
        }

        private void Save()
        {
            try
            {
                PlayerPrefs.SetString(PrefKey, JsonConvert.SerializeObject(_today));
                PlayerPrefs.Save();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[DailyQuestService] save failed: " + ex.Message);
            }
        }
    }
}
