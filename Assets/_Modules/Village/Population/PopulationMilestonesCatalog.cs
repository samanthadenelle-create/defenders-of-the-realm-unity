// =============================================================================
// PopulationMilestonesCatalog — typed model + loader for population-milestones.json
// (WORK_ORDER_587). Mirrors BuildingCatalog exactly: canonical JSON read through
// CanonicalJson (Resources-first, StreamingAssets fallback), parsed by Newtonsoft.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Population
//
// The milestone TABLE is CONTENT, not code (owner thinks in data structures —
// table over control flow). Changing the JSON changes the unlock cadence with NO
// code change. PopulationService reads these records to decide when to unlock the
// next echo workforce slot; DataRegression validates the file (slots ascending
// 2..5, no gaps, each carries >=1 condition).
//
// JSON lives in BOTH (keep in sync; Resources wins at load time):
//   Assets/Resources/Data/Canonical/population-milestones.json     (WebGL-safe)
//   Assets/StreamingAssets/Data/Canonical/population-milestones.json (desktop src)
// =============================================================================

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace DeNelle.Village.Population
{
    /// <summary>
    /// A bag of earned-progress thresholds. A field &gt; 0 is an ACTIVE requirement;
    /// 0 / absent means "not part of this condition". Used as the <c>any</c> (one
    /// satisfies) and/or <c>all</c> (every active field required) block of a milestone.
    /// </summary>
    [Serializable]
    public sealed class MilestoneCondition
    {
        /// <summary>Required accumulated population XP.</summary>
        [JsonProperty("xp")] public int Xp;

        /// <summary>Required cumulative completed quests.</summary>
        [JsonProperty("questsCompleted")] public int QuestsCompleted;

        /// <summary>Required cumulative cleared enemy outposts.</summary>
        [JsonProperty("outpostsCleared")] public int OutpostsCleared;

        /// <summary>Required cumulative cleared defense waves.</summary>
        [JsonProperty("wavesCleared")] public int WavesCleared;

        /// <summary>Required Village/Stronghold tier (VillageTierService.Current).</summary>
        [JsonProperty("villageLevel")] public int VillageLevel;

        /// <summary>True when no field is an active (&gt;0) requirement.</summary>
        public bool IsEmpty =>
            Xp <= 0 && QuestsCompleted <= 0 && OutpostsCleared <= 0 && WavesCleared <= 0 && VillageLevel <= 0;
    }

    /// <summary>
    /// One population step: meeting its condition unlocks <see cref="EchoSlot"/>. An
    /// entry may carry an <see cref="Any"/> block, an <see cref="All"/> block, or both
    /// (both blocks must pass when both are present).
    /// </summary>
    [Serializable]
    public sealed class PopulationMilestone
    {
        /// <summary>The echo workforce slot this milestone unlocks (2..5).</summary>
        [JsonProperty("echoSlot")] public int EchoSlot;

        /// <summary>ANY one active field satisfies this block (null = absent → passes).</summary>
        [JsonProperty("any")] public MilestoneCondition Any;

        /// <summary>ALL active fields required (null = absent → passes).</summary>
        [JsonProperty("all")] public MilestoneCondition All;

        /// <summary>True when the entry declares at least one real condition (DataRegression gate).</summary>
        public bool HasAnyCondition =>
            (Any != null && !Any.IsEmpty) || (All != null && !All.IsEmpty);
    }

    /// <summary>The parsed population-milestones.json root.</summary>
    [Serializable]
    public sealed class PopulationMilestonesData
    {
        /// <summary>Schema version — bumped on a breaking shape change.</summary>
        [JsonProperty("version")] public int Version;

        /// <summary>The milestone table, sorted ascending by <see cref="PopulationMilestone.EchoSlot"/>.</summary>
        [JsonProperty("milestones")] public List<PopulationMilestone> Milestones = new List<PopulationMilestone>();
    }

    /// <summary>
    /// Static surface over the canonical population-milestones.json — loads + caches
    /// the typed milestones, ordered by echo slot. The BuildingCatalog loading pattern.
    /// </summary>
    public static class PopulationMilestonesCatalog
    {
        /// <summary>StreamingAssets-relative path to the canonical milestone data.</summary>
        private const string StreamingRelativePath = "Data/Canonical/population-milestones.json";

        private static PopulationMilestonesData _data;

        /// <summary>All milestones, ordered ascending by <see cref="PopulationMilestone.EchoSlot"/>.</summary>
        public static IReadOnlyList<PopulationMilestone> Milestones
        {
            get { EnsureLoaded(); return _data.Milestones; }
        }

        /// <summary>Forces a re-read of population-milestones.json (used by tests / regression).</summary>
        public static void Reload()
        {
            _data = null;
            EnsureLoaded();
        }

        private static void EnsureLoaded()
        {
            if (_data != null) return;
            _data = LoadCatalog();
            _data.Milestones.Sort((a, b) => a.EchoSlot.CompareTo(b.EchoSlot));
        }

        private static PopulationMilestonesData LoadCatalog()
        {
            // WebGL-safe load via CanonicalJson: Resources.Load first (works in a
            // browser, where File.ReadAllText(streamingAssetsPath) THROWS), then a
            // StreamingAssets fallback on desktop/editor.
            try
            {
                string json = DeNelle.Core.CanonicalJson.Read(StreamingRelativePath);
                if (!string.IsNullOrEmpty(json))
                {
                    var parsed = JsonConvert.DeserializeObject<PopulationMilestonesData>(json);
                    if (parsed != null && parsed.Milestones != null)
                        return parsed;
                    Debug.LogError("[PopulationMilestonesCatalog] population-milestones.json parsed empty.");
                }
                else
                {
                    Debug.LogError("[PopulationMilestonesCatalog] population-milestones.json not found (Resources or StreamingAssets).");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PopulationMilestonesCatalog] Failed to read population-milestones.json: {ex.Message}");
            }

            Debug.LogError("[PopulationMilestonesCatalog] population-milestones.json could not be loaded — using an empty catalog.");
            return new PopulationMilestonesData { Milestones = new List<PopulationMilestone>() };
        }
    }
}
