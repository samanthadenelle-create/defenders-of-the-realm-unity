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
using DeNelle.Core.Diagnostics;   // FlowTrace — the footprint label must be able to report FAILURE (§12 / §1.4b)
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
        /// <summary>
        /// WO-1013 -- true when this card is VISIBLE-BUT-LOCKED (build-categories
        /// 'visibleLockedIds' with its persisted unlock flag still down). The card renders
        /// with its NORMAL cost plus <see cref="LockReason"/> in words, and can never be
        /// armed/placed while locked. A different axis from the hidden lockedIds filter.
        /// </summary>
        public bool Locked { get; }
        /// <summary>The lock reason IN WORDS (e.g. "Recover the plans"); null when not locked.
        /// Words carry the state -- never colour alone (colorblind law).</summary>
        public string LockReason { get; }

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

        /// <summary>
        /// WO-1425 — the cap-block note for <see cref="EffectiveCost"/>: "" unless some component of
        /// this cost is MORE THAN THE TOWN BANK CAN HOLD, in which case it names the container, the
        /// level and the capacity that unblocks it. The pill renders this UNDER the price so an
        /// unaffordable card explains WHICH kind of unaffordable it is.
        ///
        /// <para>An affordable card never has one (a cost you can pay is by definition one the bank
        /// held), so this is checked only when <see cref="Affordable"/> is false — the palette does
        /// not pay a capacity walk per card for the common case.</para>
        ///
        /// <para>LAZY for the same reason <see cref="FootprintLabel"/> is: the capacity read walks
        /// GameState.BaseLayout, and the palette builds many cards per open. Wrapped in Guard so a
        /// null-service or catalog-less context can never blank a card (§12 — never a silent catch:
        /// Guard logs through FlowTrace.Fail).</para>
        ///
        /// <para>NOTE on this VM's stated purity: the ctor takes an injected IEconomy so it is
        /// unit-testable without a scene. This property reaches TownBankCapacity (static, and
        /// GameState-reading) instead, which is a deliberate, documented exception — with no
        /// GameStateService it resolves to the base cap and cannot throw. It is NOT injected because
        /// the whole point of WO-1425 is that capacity has exactly ONE reader.</para>
        /// </summary>
        public string CapBlockNote
        {
            get
            {
                if (!_capNoteComputed)
                {
                    _capNote = !Affordable
                        ? Guard.Try("Build", "StructureCardVM cap-block note",
                            () => BuildModeController.CapBlockMessage(EffectiveCost) ?? "", "")
                        : "";
                    _capNoteComputed = true;
                }
                return _capNote;
            }
        }
        private string _capNote;
        private bool _capNoteComputed;

        /// <summary>WO-1425 — the same note for <see cref="NextTierCost"/>, so the info panel's
        /// upgrade preview cannot advertise a tier whose price the bank can never hold without
        /// saying so. "" when there is no next tier or the cost fits.</summary>
        public string NextTierCapBlockNote
        {
            get
            {
                if (!_nextCapNoteComputed)
                {
                    _nextCapNote = HasNextTier
                        ? Guard.Try("Build", "StructureCardVM next-tier cap-block note",
                            () => BuildModeController.CapBlockMessage(NextTierCost) ?? "", "")
                        : "";
                    _nextCapNoteComputed = true;
                }
                return _nextCapNote;
            }
        }
        private string _nextCapNote;
        private bool _nextCapNoteComputed;
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

        public StructureCardVM(CatalogEntry entry, IEconomy economy, bool freebie,
            bool locked = false, string lockReason = null)
        {
            // WO-1013: a locked card always shows its REAL cost (the aspiration is the
            // point), so the caller passes freebie=false for locked rows -- belt-and-braces
            // here too, because a zero "FREE" cost on a locked card would contradict both
            // the D20 no-FREE rule and the "normal cost displayed" acceptance line.
            if (locked) freebie = false;
            Locked = locked;
            LockReason = locked ? lockReason : null;

            Entry = entry;
            Id = entry != null ? entry.id : null;
            DisplayName = entry != null && !string.IsNullOrEmpty(entry.displayName) ? entry.displayName
                        : (entry != null ? entry.id : "");

            var repo = entry != null ? entry.repo : null;

            Freebie = freebie;
            // WO-855 Phase 1: the palette card / info panel is the price the player reads BEFORE
            // arming, so it must carry the tower-spam softcap or the ghost would reject
            // CannotAfford at a number this card never showed. SoftcappedCostFor (not
            // EffectiveCostFor) so the INJECTED `freebie` argument stays the single freebie
            // authority for this projection -- ResolveCost below was a private duplicate of
            // BuildModeController.CostFor and could not see the multiplier.
            EffectiveCost = freebie ? default : BuildModeController.SoftcappedCostFor(entry);
            Affordable = ComputeAffordable(economy, EffectiveCost);

            TargetingTag = TargetingTagFor(entry);
            TargetingLine = TargetingLineFor(entry);

            // Same ceiling BuildModeController.MaxLevelFor clamps to -- read off the ONE named
            // constant, never a literal, or the shop card advertises "Lv 1 / 3" for a container
            // the upgrade verb will happily take to 6 (WO-966).
            MaxLevel = repo == null ? 1 : Mathf.Clamp(repo.maxLevel, 1, DeNelle.Core.Catalog.RepoProps.MaxStructureLevel);
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

        // RETIRED (WO-855): the private ResolveCost duplicate of BuildModeController.CostFor
        // lived here and was the reason this card could not see the tower softcap. The ctor
        // now calls BuildModeController.SoftcappedCostFor -- the ONE resolver -- directly.

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

        public static string DescriptionFor(CatalogEntry e)
        {
            if (e == null) return string.Empty;
            if (!string.IsNullOrWhiteSpace(e.description)) return e.description;
            FlowTrace.Once("Build", "desc-unauthored-" + e.id,
                $"description fallback id={e.id} type={e.type} -- author CatalogEntry.description");
            switch (e.type)
            {
                case CatalogType.Tower:    return "A defensive tower — auto-fires on enemies in range.";
                case CatalogType.Wall:     return "A wall segment — blocks and slows the enemy advance.";
                case CatalogType.Gate:     return "A gate — a controlled opening in your defenses.";
                case CatalogType.Resource: return "A resource structure — gathers materials over time.";
                default:                   return "A village structure.";
            }
        }

        /// <summary>
        /// WO-972 follow-through — the panel states the CLAIM, derived from the same authority
        /// placement claims with (StructureFactory.MeasureClaimFootprintMetres), NOT a second
        /// measure of its own. WO-972 decoupled a Wall's grid claim from its fitted mesh
        /// (BuildModeController.IsValidPlacement + BaseLayoutLoader both moved to the claim
        /// metric), but this label was still reading the raw mesh measure — so a wall whose
        /// 3.03 m body ceils to 2 cells would have told the player "2x2 cells" while placement
        /// claimed 1x1. Identical output for every non-Wall row (the claim metric IS the
        /// measured metric there); only a Wall's label changes, and it changes to the truth.
        /// "Derive, don't hand-author" (ARCHITECTURE_PRINCIPLES §4): one claim authority,
        /// read by everyone who reports it.
        /// </summary>
        private static string FootprintFor(CatalogEntry e)
        {
            var grid = PlacementGrid.Instance;
            if (grid != null && e != null)
            {
                // WO-986: report non-square claim cells (same authority as placement).
                Vector2 xz = StructureFactory.MeasureClaimFootprintXZ(e);
                if (xz.x > 0f && xz.y > 0f)
                {
                    Vector2Int f = grid.FootprintCells(xz);
                    int fx = Mathf.Max(1, f.x);
                    int fy = Mathf.Max(1, f.y);
                    return fx + "x" + fy + " cells";
                }
                FlowTrace.Once("Build", "footprint-label-unmeasured-" + (e.id ?? "<null>"),
                    $"FOOTPRINT LABEL '{e.id}': claim XZ returned ({xz.x:0.###},{xz.y:0.###})m (non-positive), " +
                    "so the info panel is showing the 1x1 DEFAULT, not a measured claim.");
            }
            else
            {
                // The other way this label can be wrong, and it reads IDENTICALLY to a real
                // 1x1 on screen: no PlacementGrid (info panel opened outside build mode) or no
                // entry at all. Distinct message per cause so a capture never has to guess.
                FlowTrace.Once("Build",
                    "footprint-label-nogrid-" + (e != null ? e.id : "<null-entry>"),
                    e == null
                        ? "FOOTPRINT LABEL: no CatalogEntry - showing the 1x1 DEFAULT, not a measured claim."
                        : $"FOOTPRINT LABEL '{e.id}': PlacementGrid.Instance is null (no grid to convert " +
                          "metres to cells) - showing the 1x1 DEFAULT, not a measured claim.");
            }
            return "1x1 cells";
        }

        private static string FormatNum(float v)
            => Mathf.Approximately(v, Mathf.Round(v)) ? Mathf.RoundToInt(v).ToString() : v.ToString("0.0");
    }
}
