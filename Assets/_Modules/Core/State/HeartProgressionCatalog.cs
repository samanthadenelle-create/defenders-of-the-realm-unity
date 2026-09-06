// =============================================================================
// HeartProgressionCatalog — typed model + loader for heart-progression.json
// (WO-2004). The ONE authoritative table behind the player-facing HEART LEVEL.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.State
//
// ⛔ THE DEFECT THIS EXISTS FOR. Until 2026-09-06 the realm-progression spine's
// two defining numbers were C# literals inside VillageTierService:
//     public const int MaxTier = 3;
//     return 250 * next;                       // 250 / 500 / 750
// A ceiling and a cost curve for the gate that opens nearly all content, both
// unreachable by an owner who tunes balance in data. That is the same duplicated-
// /buried-state failure CLAUDE.md §2 (the stale WO block), §5 (the retired
// dependency table) and §16 (the copy-pasted verify) all describe, and it is why
// WO-2004's first acceptance line reads "one authoritative progression table".
//
// THE SHAPE. This file is the ONLY reader of heart-progression.json;
// VillageTierService PROJECTS it (MaxTier / NextCost) so every existing call site
// keeps working unchanged, and HeartProgression composes the player words on top.
// Nothing else parses this file, and no second copy of the ladder exists.
//
// ⚠ NO HAND-WRITTEN FALLBACK TABLE, DELIBERATELY. Owner ruling 2026-08-24 (WO-1170,
// pinned by JsonMirrorLiteralRegression): "We need to not have anything pulled
// other than from json." A missing/malformed catalog therefore FAILS LOUDLY and
// LOUDLY EMPTY — FlowTrace.Fail + Debug.LogError, then an empty ladder — exactly
// as BuildingTierCatalog.LoadCatalog does. It does NOT quietly re-supply 3 and 250,
// because a silent fallback is indistinguishable from a working catalog and is what
// let a hand-mirrored table drift in the first place. An empty ladder is visible
// (MaxLevel 0, the Heart reports Max, the [heart-ladder-is-data-driven] case in
// HeartSurfaceRegression goes RED) — a wrong-but-plausible ladder would not be.
//
// ⚠ WHAT THIS FILE IS NOT. It is not an unlock table. What a Heart Level opens is
// DERIVED (HeartProgression.UnlocksAt) from building-tiers.json / troops.json /
// population-milestones.json — see the _authoringNotes in heart-progression.json
// for the full list of fields deliberately absent and why. Adding an unlock list
// here would create the second table WO-2004 exists to prevent.
// =============================================================================

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace DeNelle.Core.State
{
    /// <summary>
    /// One Heart Level transition: the level reached, and what reaching it costs.
    /// <para>⚠ There is no <c>durationSeconds</c> and no prerequisite block on purpose.
    /// <c>VillageTierService.TryUpgrade</c> is INSTANT (spend crystals → tier+1 → Save →
    /// Recompute); no Heart job kind, queue channel or timer exists in the tree, measured
    /// 2026-09-06. WO-2004 asks for a duration; inventing one here would author a promise
    /// nothing keeps. Recorded as an owner gap, not papered over.</para>
    /// </summary>
    [Serializable]
    public sealed class HeartLevelDef
    {
        /// <summary>The Heart Level this record is the transition INTO (1-based; 0 is the
        /// founding state and has no record because nothing is bought to reach it).</summary>
        [JsonProperty("level")] public int Level;

        /// <summary>Crystals spent to reach <see cref="Level"/>. ⛔ Owner rules on balance.</summary>
        [JsonProperty("costCrystal")] public int CostCrystal;
    }

    /// <summary>The parsed heart-progression.json root.</summary>
    [Serializable]
    public sealed class HeartProgressionData
    {
        [JsonProperty("version")] public int Version;

        /// <summary>The highest Heart Level a player can reach. ⛔ THE SINGLE CEILING for this
        /// axis — never re-hardcode it. It is a DIFFERENT axis from
        /// <c>RepoProps.MaxStructureLevel</c> (the per-structure ladder ceiling, 6): a structure
        /// level and a Heart Level are two scales that happen to be spelled with integers, and
        /// conflating them is precisely the WO-1423 dead end.</summary>
        [JsonProperty("maxLevel")] public int MaxLevel;

        [JsonProperty("levels")] public List<HeartLevelDef> Levels = new List<HeartLevelDef>();
    }

    /// <summary>Static surface over heart-progression.json — load + cache + lookup.</summary>
    public static class HeartProgressionCatalog
    {
        private const string StreamingRelativePath = "Data/Canonical/heart-progression.json";
        private static HeartProgressionData _data;

        /// <summary>Every authored Heart Level transition, in catalog order. Never null.</summary>
        public static IReadOnlyList<HeartLevelDef> Levels { get { EnsureLoaded(); return _data.Levels; } }

        /// <summary>
        /// The authored ceiling. Returns the higher of the authored <c>maxLevel</c> and the highest
        /// authored level row, so a ladder that gains a rung without its header being bumped is
        /// reachable rather than silently truncated (fail OPEN on a mismatch that can only make the
        /// ladder longer — the reverse, a header claiming levels no row funds, is caught below).
        /// 0 when the catalog failed to load, which is a LOUD state: the Heart then reports Max.
        /// </summary>
        public static int MaxLevel
        {
            get
            {
                EnsureLoaded();
                int max = _data.MaxLevel;
                for (int i = 0; i < _data.Levels.Count; i++)
                {
                    var l = _data.Levels[i];
                    if (l != null && l.Level > max) max = l.Level;
                }
                return max > 0 ? max : 0;
            }
        }

        /// <summary>The transition record for <paramref name="level"/>, or null when unauthored.</summary>
        public static HeartLevelDef Find(int level)
        {
            EnsureLoaded();
            for (int i = 0; i < _data.Levels.Count; i++)
            {
                var l = _data.Levels[i];
                if (l != null && l.Level == level) return l;
            }
            return null;
        }

        /// <summary>
        /// Crystals to reach <paramref name="level"/>. 0 when the level is unauthored or out of
        /// range — and an unauthored level INSIDE the ceiling is a data hole, so it is traced
        /// rather than returned silently (§12: no silent failure).
        /// </summary>
        public static int CostToReach(int level)
        {
            if (level <= 0) return 0;
            var def = Find(level);
            if (def != null) return def.CostCrystal;

            if (level <= MaxLevel)
                DeNelle.Core.Diagnostics.FlowTrace.Fail("HeartCatalog",
                    "heart-progression.json authors maxLevel " + MaxLevel + " but has NO row for level "
                    + level + " — that level would cost ZERO crystals. Author the row or lower maxLevel.");
            return 0;
        }

        /// <summary>Drop the cache so the next read re-parses (editor tooling / regression fixtures).</summary>
        public static void Reload() { _data = null; EnsureLoaded(); }

        private static void EnsureLoaded()
        {
            if (_data != null) return;
            _data = LoadCatalog();
        }

        private static HeartProgressionData LoadCatalog()
        {
            try
            {
                string json = DeNelle.Core.CanonicalJson.Read(StreamingRelativePath);
                if (!string.IsNullOrEmpty(json))
                {
                    var parsed = JsonConvert.DeserializeObject<HeartProgressionData>(json);
                    if (parsed != null && parsed.Levels != null && parsed.Levels.Count > 0)
                    {
                        DeNelle.Core.Diagnostics.FlowTrace.Step("HeartCatalog",
                            "heart-progression.json v" + parsed.Version + " loaded: maxLevel " + parsed.MaxLevel
                            + ", " + parsed.Levels.Count + " level rows.");
                        return parsed;
                    }
                    DeNelle.Core.Diagnostics.FlowTrace.Fail("HeartCatalog",
                        "heart-progression.json parsed EMPTY (" + json.Length + " chars read) — the Heart "
                        + "ladder is now empty and the Heart will report Max at level 0.");
                    Debug.LogError("[HeartProgressionCatalog] heart-progression.json parsed empty.");
                }
                else
                {
                    DeNelle.Core.Diagnostics.FlowTrace.Fail("HeartCatalog",
                        "heart-progression.json NOT FOUND in Resources or StreamingAssets — the Heart "
                        + "ladder is empty. There is deliberately no hand-written fallback (WO-1170).");
                    Debug.LogError("[HeartProgressionCatalog] heart-progression.json not found (Resources or StreamingAssets).");
                }
            }
            catch (Exception ex)
            {
                DeNelle.Core.Diagnostics.FlowTrace.Fail("HeartCatalog",
                    "heart-progression.json read/parse THREW " + ex.GetType().Name + ": " + ex.Message);
                Debug.LogError($"[HeartProgressionCatalog] Failed to read heart-progression.json: {ex.Message}");
            }
            return new HeartProgressionData { Levels = new List<HeartLevelDef>() };
        }
    }
}
