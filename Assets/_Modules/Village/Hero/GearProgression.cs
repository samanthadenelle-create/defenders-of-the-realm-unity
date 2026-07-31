// =============================================================================
// GearProgression — WO-808 Option A (owner-locked 2026-07-30): per-instance gear
// power levels. The SAME owned weapon/armor levels up in place ("improve THIS
// sword"); rarity stays identity, LEVEL is the power ladder. Resources-only V1.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Mirrors the shipped WO-771.9 troop-level stack piece for piece:
//   • gear-levels.json (dual-copy Canonical) = troop-upgrades.json   (curves)
//   • GearLevelCatalog                        = TroopUpgradeCatalog  (loader)
//   • GearStatResolver                        = TroopStatResolver    (pure math)
//   • GearProgression (pure state logic)      = BarracksProgression
//   • GearProgression.Improve* (live facade)  = BarracksService (ledger spend)
// Differences (deliberate):
//   • Improve is INSTANT V1 — no Obsidian job/channel (WO scope: "default lean
//     instant"; a timer variant is an owner call later).
//   • Levels key by GEAR ID in GameState.GearLevels (Dictionary<string,int>,
//     additive default-on-read — NO save-version bump; the troopLevels
//     precedent, see SaveSchema.cs).
// Combat apply: GearLoadout.ApplyStats calls GearStatResolver.Effective* so the
// published WeaponMult/ArmorDefense scalars — the single choke point every
// combat consumer reads — carry the level automatically.
// =============================================================================

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using DeNelle.Core.State;
using DeNelle.Core.Diagnostics;
using Ledger = DeNelle.Village.Buildings.Progression;

namespace DeNelle.Village
{
    /// <summary>One rarity band's level ladder: stat multiplier + cost curves.
    /// Index 0 == level 1 (always the authored baseline / free).</summary>
    [Serializable]
    public sealed class GearLevelBand
    {
        [JsonProperty("rarity")]   public string Rarity;
        [JsonProperty("statMult")] public float[] StatMult;
        [JsonProperty("costWood")] public int[] CostWood;
        [JsonProperty("costIron")] public int[] CostIron;

        /// <summary>Max reachable level for this band (curve length; 1 when unauthored).</summary>
        public int MaxLevel => StatMult != null && StatMult.Length > 0 ? StatMult.Length : 1;

        /// <summary>The stat multiplier at <paramref name="level"/> (clamped; 1.0 baseline).</summary>
        public float MultAt(int level)
        {
            if (StatMult == null || StatMult.Length == 0) return 1f;
            int i = Mathf.Clamp(level, 1, StatMult.Length) - 1;
            return StatMult[i] > 0f ? StatMult[i] : 1f;
        }
    }

    [Serializable]
    public sealed class GearLevelCatalogData
    {
        [JsonProperty("version")] public int Version;
        [JsonProperty("bands")]   public List<GearLevelBand> Bands;
    }

    /// <summary>Memoized loader for gear-levels.json (mirror of TroopUpgradeCatalog).</summary>
    public static class GearLevelCatalog
    {
        private const string StreamingRelativePath = "Data/Canonical/gear-levels.json";

        private static GearLevelCatalogData _data;

        /// <summary>The band for a rarity string (case-insensitive). Null when unauthored —
        /// callers treat null as "no ladder" (max level 1, mult 1.0), never throw.</summary>
        public static GearLevelBand BandFor(string rarity)
        {
            if (string.IsNullOrEmpty(rarity)) return null;
            EnsureLoaded();
            foreach (var b in _data.Bands)
                if (b != null && string.Equals(b.Rarity, rarity, StringComparison.OrdinalIgnoreCase))
                    return b;
            return null;
        }

        /// <summary>Forces a re-read (tests / data sync).</summary>
        public static void Reload() { _data = null; EnsureLoaded(); }

        private static void EnsureLoaded()
        {
            if (_data != null) return;
            _data = Load();
        }

        private static GearLevelCatalogData Load()
        {
            try
            {
                string json = DeNelle.Core.CanonicalJson.Read(StreamingRelativePath);
                if (!string.IsNullOrEmpty(json))
                {
                    var parsed = JsonConvert.DeserializeObject<GearLevelCatalogData>(json);
                    if (parsed != null && parsed.Bands != null) return parsed;
                    Debug.LogError("[GearLevelCatalog] gear-levels.json parsed empty.");
                }
                else
                {
                    Debug.LogError("[GearLevelCatalog] gear-levels.json not found (Resources or StreamingAssets).");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GearLevelCatalog] Failed to read gear-levels.json: {ex.Message}");
            }
            return new GearLevelCatalogData { Bands = new List<GearLevelBand>() };
        }
    }

    /// <summary>
    /// PURE stat math: catalog def × level ladder. Side-effect free, null-safe —
    /// callable from tests/oracles with no live scene (the TroopStatResolver twin).
    /// </summary>
    public static class GearStatResolver
    {
        /// <summary>Effective weapon damage multiplier at <paramref name="level"/>.
        /// Level 1 / unauthored band == the authored damageMult exactly.</summary>
        public static float EffectiveDamageMult(WeaponDef def, int level)
        {
            if (def == null) return 1f;
            float baseMult = Mathf.Max(0.1f, def.damageMult);
            var band = GearLevelCatalog.BandFor(def.rarity);
            return baseMult * (band != null ? band.MultAt(level) : 1f);
        }

        /// <summary>Effective armor damage-reduction fraction at <paramref name="level"/>.
        /// Multiplied THEN clamped to the same 0..0.9 window ApplyStats always enforced —
        /// a levelled legendary can never approach immunity.</summary>
        public static float EffectiveDefense(ArmorDef def, int level)
        {
            if (def == null) return 0f;
            var band = GearLevelCatalog.BandFor(def.rarity);
            return Mathf.Clamp(def.defense * (band != null ? band.MultAt(level) : 1f), 0f, 0.9f);
        }
    }

    /// <summary>
    /// Gear-level state logic + the live Improve facade (WO-808). Pure readers are
    /// GameState-parameterised (testable); Improve* charge the SAME ResourceLedger
    /// wallet everything else uses (never EconomyService's in-session pool).
    /// </summary>
    public static class GearProgression
    {
        public const int DefaultLevel = 1;

        /// <summary>Raised after a successful Improve so UI repaints (shop rows, inventory, equip card).</summary>
        public static event Action Changed;

        // ── Pure readers ─────────────────────────────────────────────────────

        /// <summary>The owned instance's level (1 with no state / never improved).</summary>
        public static int GearLevelOf(GameState state, string gearId)
        {
            if (state == null || state.GearLevels == null || string.IsNullOrEmpty(gearId)) return DefaultLevel;
            return state.GearLevels.TryGetValue(gearId, out int lvl) ? Mathf.Max(DefaultLevel, lvl) : DefaultLevel;
        }

        /// <summary>Max level for a rarity (band curve length; 1 when unauthored).</summary>
        public static int MaxLevelFor(string rarity)
        {
            var band = GearLevelCatalog.BandFor(rarity);
            return band != null ? band.MaxLevel : 1;
        }

        public static bool HasNextLevel(string rarity, int currentLevel) =>
            currentLevel < MaxLevelFor(rarity);

        /// <summary>Resource cost to REACH <paramref name="targetLevel"/> (index target-1 in the
        /// band's cost curves). Zero cost when unauthored/at-baseline.</summary>
        public static ResourceCost ImproveCost(string rarity, int targetLevel)
        {
            var band = GearLevelCatalog.BandFor(rarity);
            if (band == null) return new ResourceCost();
            int i = targetLevel - 1;
            int wood = band.CostWood != null && i >= 0 && i < band.CostWood.Length ? band.CostWood[i] : 0;
            int iron = band.CostIron != null && i >= 0 && i < band.CostIron.Length ? band.CostIron[i] : 0;
            return new ResourceCost(wood: wood, iron: iron);
        }

        /// <summary>Clamped level write: bumps the id one level (creates the dict lazily).
        /// Returns the new level, or the current one when already at max.</summary>
        public static int ApplyImprove(GameState state, string gearId, string rarity)
        {
            if (state == null || string.IsNullOrEmpty(gearId)) return DefaultLevel;
            if (state.GearLevels == null) state.GearLevels = new Dictionary<string, int>();
            int cur = GearLevelOf(state, gearId);
            int next = Mathf.Min(cur + 1, MaxLevelFor(rarity));
            state.GearLevels[gearId] = next;
            return next;
        }

        // ── Live facade (the Improve verb) ───────────────────────────────────

        private static GameState State =>
            GameStateService.Instance != null ? GameStateService.Instance.State : null;

        /// <summary>True when <paramref name="gearId"/> (rarity <paramref name="rarity"/>) can be
        /// improved right now; <paramref name="reason"/> carries the player-facing block.</summary>
        public static bool CanImprove(string gearId, string rarity, out string reason)
        {
            reason = null;
            var state = State;
            if (state == null) { reason = "No game state."; return false; }
            int cur = GearLevelOf(state, gearId);
            if (!HasNextLevel(rarity, cur)) { reason = "Already at max level."; return false; }
            var cost = LedgerCost(ImproveCost(rarity, cur + 1));
            if (!Ledger.ResourceLedger.CanAfford(cost)) { reason = MissingOf(cost); return false; }
            return true;
        }

        /// <summary>
        /// Spend the next level's resource cost and bump the instance level IN PLACE
        /// (instant V1 — no job/channel). Persists + recomputes any live GearLoadout so
        /// the power is felt immediately. Returns the new level, or -1 on refusal.
        /// </summary>
        public static int Improve(string gearId, string rarity)
        {
            if (!CanImprove(gearId, rarity, out string reason))
            {
                FlowTrace.Warn("Gear", $"Improve refused ({gearId}): {reason}");
                return -1;
            }

            var state = State;
            int cur = GearLevelOf(state, gearId);
            var cost = LedgerCost(ImproveCost(rarity, cur + 1));
            if (!Ledger.ResourceLedger.TrySpend(cost))
            {
                FlowTrace.Warn("Gear", $"Improve spend failed ({gearId}).");
                return -1;
            }

            int next = ApplyImprove(state, gearId, rarity);
            GameStateService.Instance?.Save();

            // Re-publish combat scalars on every live loadout carrying this piece —
            // ApplyStats reads the level through GearStatResolver, so a refresh is enough.
            Guard.Try("Gear", "refresh loadouts after improve", () =>
            {
                foreach (var lo in UnityEngine.Object.FindObjectsByType<GearLoadout>(FindObjectsSortMode.None))
                    lo.RefreshStats();
            });

            FlowTrace.Step("Gear", $"'{gearId}' improved L{cur}->L{next} ({rarity} band; instant, ledger-charged).");
            Changed?.Invoke();
            return next;
        }

        // ── Wallet plumbing (the BarracksService idiom — GameState ledger only) ──

        private static List<Ledger.ResourceCost> LedgerCost(ResourceCost cost)
        {
            var list = new List<Ledger.ResourceCost>(4);
            if (cost.Wood > 0)     list.Add(new Ledger.ResourceCost(Ledger.HarvestResource.Wood, cost.Wood));
            if (cost.Food > 0)     list.Add(new Ledger.ResourceCost(Ledger.HarvestResource.Food, cost.Food));
            if (cost.Iron > 0)     list.Add(new Ledger.ResourceCost(Ledger.HarvestResource.Iron, cost.Iron));
            if (cost.Crystals > 0) list.Add(new Ledger.ResourceCost(Ledger.HarvestResource.Crystals, cost.Crystals));
            return list;
        }

        private static string MissingOf(List<Ledger.ResourceCost> lines)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var line in lines)
                if (Ledger.ResourceLedger.Balance(line.Resource) < line.Amount)
                    sb.Append(sb.Length > 0 ? ", " : "").Append(line.Resource);
            return sb.Length > 0 ? "Need more " + sb + "." : "Not enough resources.";
        }
    }
}
