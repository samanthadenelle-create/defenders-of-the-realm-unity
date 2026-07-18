// =============================================================================
// StructureCardVM — the PURE projection of ONE catalog structure (MVVM Silo C).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Strict-MVVM migration (UI_MVVM_MIGRATION_PLAN.md §1, Silo C): the shared
// ViewModel behind a build-palette CARD (BuildPaletteUI) AND the Structure Info
// preview (BuildStructureInfoPanel). ALL the cost / affordability / footprint /
// DPS-tier math that used to live in those two Views moves HERE, so the Views
// become dumb skins that render from these read-only fields (ui-mvvm rule).
//
// PURE C# — references only Core.Catalog data + the Village cost seams
// (BuildModeController static cost readers + IEconomy). Never a GameObject /
// Image / Sprite / RectTransform. The View resolves art from the exposed
// id/displayName/type (a Resources look-up is presentation, not state) and
// raises the existing OnEntrySelected/OnCardTapped events off <see cref="Entry"/>
// (the CatalogEntry is pure Core data, not a scene object).
//
// Behaviour is PRESERVED verbatim (§2c): the cost reader is the ONE
// BuildModeController.EffectiveCostFor seam every surface already agrees with,
// the next-tier math mirrors BuildStructureInfoPanel.RenderNextTierPreview, and
// the footprint mirrors its FootprintLabel. The View keeps its OWN cost-string
// formatter (pure presentation) and reads <see cref="EffectiveCost"/> +
// <see cref="Freebie"/> from here.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core.Catalog;
using CoreCost = DeNelle.Core.Catalog.ResourceCost;

namespace DeNelle.Village
{
    /// <summary>
    /// Read-only projection of a single <see cref="CatalogEntry"/> for the build palette
    /// card + the Structure Info preview. Constructed with an injected <see cref="IEconomy"/>
    /// and an explicit freebie flag (the sole game-state read stays in <see cref="CreateForEntry"/>),
    /// so it is unit-testable without a scene.
    /// </summary>
    public sealed class StructureCardVM
    {
        /// <summary>One current-tier stat row for the info panel (key + formatted value).</summary>
        public readonly struct StatRow
        {
            public readonly string Key;
            public readonly string Value;
            public StatRow(string key, string value) { Key = key; Value = value; }
        }

        /// <summary>The source def (pure Core data). The View raises OnEntrySelected/OnCardTapped
        /// off this and resolves the card art from it — it NEVER re-queries the catalog/economy.</summary>
        public CatalogEntry Entry { get; }

        public string Id { get; }
        public string DisplayName { get; }

        // ── Shared (palette card + info panel) ───────────────────────────────
        /// <summary>True while the entry's first-build freebie is live (card/info shows "FREE").</summary>
        public bool Freebie { get; }
        /// <summary>The cost the player actually pays — freebie-aware (default/zero when free). The
        /// ONE BuildModeController.EffectiveCostFor value every build surface agrees with.</summary>
        public CoreCost EffectiveCost { get; }
        /// <summary>Whether the player can currently afford <see cref="EffectiveCost"/>.</summary>
        public bool Affordable { get; }

        /// <summary>Palette short targeting caption ("Land only"/"Land + Air"/"Air only"), or null (non-tower).</summary>
        public string TargetingTag { get; }
        /// <summary>Info-panel targeting line ("Targets: Land only" ...), or null (non-tower).</summary>
        public string TargetingLine { get; }

        // ── Info-panel only ──────────────────────────────────────────────────
        public int MaxLevel { get; }
        /// <summary>"Lv 1" or "Lv 1 / N".</summary>
        public string TierBadge { get; }
        public string Description { get; }
        /// <summary>Footprint in grid cells, e.g. "2x2 cells" (falls back to "1x1 cells"). Computed
        /// LAZILY on first read — the palette never reads it, so it never pays the measure cost;
        /// only the info panel does.</summary>
        public string FootprintLabel
        {
            get
            {
                if (!_footprintComputed) { _footprintLabel = FootprintFor(Entry); _footprintComputed = true; }
                return _footprintLabel;
            }
        }
        private string _footprintLabel;
        private bool _footprintComputed;
        /// <summary>Current-tier stat rows (DPS / Range / Fire Rate — or a single "Type" row).</summary>
        public IReadOnlyList<StatRow> CurrentStats => _currentStats;
        private readonly List<StatRow> _currentStats = new List<StatRow>();

        public bool HasNextTier { get; }
        public string NextTierTitle { get; }
        public string NextTierStats { get; }
        /// <summary>The next-tier upgrade cost (the View formats it with its own cost formatter).</summary>
        public CoreCost NextTierCost { get; }

        /// <summary>The one game-state-touching factory (audit §3.1): resolves the economy handle +
        /// the freebie flag itself, then builds the pure projection. Views call THIS (they never
        /// name EconomyService / BuildModeController).</summary>
        public static StructureCardVM CreateForEntry(CatalogEntry entry)
            => new StructureCardVM(entry, EconomyService.Instance, BuildModeController.FreeBuildAvailable(entry));

        public StructureCardVM(CatalogEntry entry, IEconomy economy, bool freebie)
        {
            Entry = entry;
            Id = entry != null ? entry.id : null;
            DisplayName = entry != null && !string.IsNullOrEmpty(entry.displayName) ? entry.displayName
                        : (entry != null ? entry.id : "");

            var repo = entry != null ? entry.repo : null;

            Freebie = freebie;
            EffectiveCost = freebie ? default : ResolveCost(entry);
            Affordable = ComputeAffordable(economy, EffectiveCost);

            TargetingTag = TargetingTagFor(entry);
            TargetingLine = TargetingLineFor(entry);

            MaxLevel = repo == null ? 1 : Mathf.Clamp(repo.maxLevel, 1, 3);
            TierBadge = MaxLevel > 1 ? "Lv 1 / " + MaxLevel : "Lv 1";
            Description = DescriptionFor(entry);

            BuildCurrentStats(entry, repo);

            // Next-tier preview (mirrors BuildStructureInfoPanel.RenderNextTierPreview) —
            // hidden for single-tier entries.
            if (repo == null || MaxLevel <= 1)
            {
                HasNextTier = false;
                NextTierTitle = null;
                NextTierStats = null;
                NextTierCost = default;
            }
            else
            {
                HasNextTier = true;
                NextTierTitle = "Upgrade to Lv 2";
                const float l2Mul = 1.25f;   // matches BuildModeController.ApplyTierStats (L2 x1.25)
                var parts = new List<string>(3);
                if (repo.damage > 0f)
                {
                    float dps1 = repo.damage * (repo.fireRate > 0f ? repo.fireRate : 1f);
                    float dps2 = (repo.damage * l2Mul) * (repo.fireRate > 0f ? repo.fireRate : 1f);
                    parts.Add("DPS " + FormatNum(dps1) + " -> " + FormatNum(dps2));
                }
                if (repo.range > 0f)
                    parts.Add("Range " + FormatNum(repo.range) + "m -> " + FormatNum(repo.range * l2Mul) + "m");
                if (parts.Count == 0)
                    parts.Add("Sturdier — higher durability tier");
                NextTierStats = string.Join("\n", parts);
                NextTierCost = BuildModeController.UpgradeCostFor(entry, 1);
            }
        }

        private void BuildCurrentStats(CatalogEntry entry, RepoProps repo)
        {
            if (repo == null) return;
            bool any = false;
            float dps = repo.damage * (repo.fireRate > 0f ? repo.fireRate : 1f);
            if (repo.damage > 0f) { _currentStats.Add(new StatRow("DPS", FormatNum(dps))); any = true; }
            if (repo.range  > 0f) { _currentStats.Add(new StatRow("Range", FormatNum(repo.range) + "m")); any = true; }
            if (repo.fireRate > 0f) { _currentStats.Add(new StatRow("Fire Rate", FormatNum(repo.fireRate) + "/s")); any = true; }
            if (!any && entry != null)
                _currentStats.Add(new StatRow("Type", entry.type.ToString()));
        }

        // ── Pure helpers (ported verbatim from the two Views) ────────────────

        /// <summary>Resolve build cost: authored multi-cost wins, else crystals-only buildCost.</summary>
        private static CoreCost ResolveCost(CatalogEntry e)
        {
            var repo = e != null ? e.repo : null;
            if (repo == null) return default;
            if (!repo.cost.IsZero) return repo.cost;
            return new CoreCost { crystals = repo.buildCost };
        }

        private static bool ComputeAffordable(IEconomy economy, CoreCost cost)
        {
            if (economy != null) return economy.CanAfford(BuildModeController.ToEconomy(cost));
            // Service-less fallback: a free (all-zero) cost is affordable, otherwise not.
            return cost.IsZero;
        }

        /// <summary>Compact palette targeting caption from the repo flags, or null for non-towers.</summary>
        private static string TargetingTagFor(CatalogEntry e)
        {
            if (e == null || e.type != CatalogType.Tower) return null;
            var repo = e.repo;
            if (repo == null) return null;
            bool airOnly = repo.airOnly;
            bool canHitAir = repo.canHitAir || airOnly;
            if (airOnly) return "Air only";
            if (canHitAir) return "Land + Air";
            return "Land only";
        }

        /// <summary>Info-panel targeting line from the repo flags, or null for non-towers.</summary>
        private static string TargetingLineFor(CatalogEntry e)
        {
            if (e == null || e.type != CatalogType.Tower) return null;
            var repo = e.repo;
            if (repo == null) return null;
            bool airOnly = repo.airOnly;
            bool canHitAir = repo.canHitAir || airOnly;
            if (airOnly) return "Targets: Air only";
            if (canHitAir) return "Targets: Land + Air";
            return "Targets: Land only";
        }

        private static string DescriptionFor(CatalogEntry e)
        {
            if (e == null) return string.Empty;
            switch (e.type)
            {
                case CatalogType.Tower:    return "A defensive tower — auto-fires on enemies in range.";
                case CatalogType.Wall:     return "A wall segment — blocks and slows the enemy advance.";
                case CatalogType.Gate:     return "A gate — a controlled opening in your defenses.";
                case CatalogType.Resource: return "A resource structure — gathers materials over time.";
                default:                   return "A village structure.";
            }
        }

        private static string FootprintFor(CatalogEntry e)
        {
            var grid = PlacementGrid.Instance;
            if (grid != null && e != null)
            {
                float m = StructureFactory.MeasureUprightFootprintMetres(e);
                if (m > 0f)
                {
                    Vector2Int f = grid.FootprintCells(m);
                    int fx = Mathf.Max(1, f.x);
                    int fy = Mathf.Max(1, f.y);
                    return fx + "x" + fy + " cells";
                }
            }
            return "1x1 cells";
        }

        private static string FormatNum(float v)
            => Mathf.Approximately(v, Mathf.Round(v)) ? Mathf.RoundToInt(v).ToString() : v.ToString("0.0");
    }
}
