// =============================================================================
// BuildStructureInfoPanel — the Structure Info Preview panel (WO-352).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// A code-built UI-Toolkit panel that appears when the player TAPS a palette card
// in Build Mode — BEFORE the structure is armed. It shows the structure's name,
// tier badge, a short description, the multi-resource build cost, footprint, key
// stats (DPS / Range / Fire Rate), and a next-tier upgrade-cost preview. A
// "Place Structure" button arms the entry (deferring the old immediate-arm tap);
// "Cancel" (or a tap on the dimmed scrim) dismisses without arming.
//
// WHY: tapping a card used to arm + drop a ghost immediately, so players placed
// the wrong structure or didn't understand the cost. This preview prevents the
// regretted placement and clarifies the upgrade path (WO-352).
//
// CODE-BUILT, NO UXML (repo rule: .uxml UIDocuments render empty in player builds
// — see BuildPaletteUI / BuildMenu). Owns its own UIDocument + PanelSettings,
// adopting a sibling's PanelSettings (same pattern as BuildPaletteUI) so it
// renders. All styling routes through ElarionUi (the ONE in-game theme — warm
// parchment + stone + runic gold). WebGL-safe: no Resources.Load, no scene-mesh
// refs, no reflection; data is pulled straight from the CatalogEntry + repo.
//
// Data source: DeNelle.Core.Catalog.CatalogEntry (name/type) + entry.repo
// (RepoProps — range / damage / fireRate / maxLevel / cost / buildCost). Cost +
// upgrade-cost resolution MIRRORS BuildModeController (multi-cost wins, else a
// crystals-only buildCost fallback) so the preview never disagrees with what the
// place / upgrade path actually charges.
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using DeNelle.Core.Catalog;
using DeNelle.Core.UI;

namespace DeNelle.Village
{
    /// <summary>
    /// Build Mode structure preview. Call <see cref="Show"/> with the tapped
    /// CatalogEntry; raises <see cref="OnPlaceRequested"/> when the player commits
    /// to placing it and <see cref="OnCancelRequested"/> when they dismiss. Built
    /// in code so it renders in player builds.
    /// </summary>
    public sealed class BuildStructureInfoPanel : MonoBehaviour
    {
        /// <summary>Raised when "Place Structure" is tapped — arg is the previewed entry.</summary>
        public event Action<CatalogEntry> OnPlaceRequested;

        /// <summary>Raised when "Cancel" / scrim is tapped (dismiss, no arm).</summary>
        public event Action OnCancelRequested;

        private UIDocument _document;
        private VisualElement _root;     // full-screen click-catcher (scrim)
        private VisualElement _panel;    // the parchment card itself
        private CatalogEntry _current;

        // Cached content rows (reused across Show calls — no per-preview reallocation).
        private Label _nameLabel;
        private Label _tierBadge;
        private Label _descLabel;
        private Label _targetingLabel;   // "Land only" / "Land + Air" / "Air only" (towers)
        private Label _costLabel;
        private Label _footprintLabel;
        private VisualElement _statsBox;
        private VisualElement _nextTierBox;
        private Label _nextTierTitle;
        private Label _nextTierStats;
        private Label _nextTierCost;

        private void Awake()
        {
            _document = GetComponent<UIDocument>();
            if (_document == null) _document = gameObject.AddComponent<UIDocument>();
            AdoptPanelSettings();
        }

        /// <summary>
        /// Adopt a sibling UIDocument's PanelSettings so the panel renders (a freshly
        /// created doc has none → invisible). Mirrors BuildPaletteUI; sorts just above
        /// the palette so the preview overlays the card strip.
        /// </summary>
        private void AdoptPanelSettings()
        {
            if (_document == null) return;
            UIDocument hud = null, any = null;
            foreach (var doc in FindObjectsByType<UIDocument>(FindObjectsInactive.Include))
            {
                if (doc == null || doc == _document || doc.panelSettings == null) continue;
                if (any == null) any = doc;
                if (doc.gameObject.name.IndexOf("Hud", StringComparison.OrdinalIgnoreCase) >= 0) { hud = doc; break; }
            }
            var src = hud ?? any;
            if (src != null)
            {
                _document.panelSettings = src.panelSettings;
                _document.sortingOrder = src.sortingOrder + 8;   // above HUD + palette
            }
            else
            {
                Debug.LogWarning("[BuildStructureInfoPanel] No sibling PanelSettings found — preview will not render.");
            }
        }

        // ── Show / Hide ────────────────────────────────────────────────────────

        /// <summary>Render + show the preview for <paramref name="entry"/>.</summary>
        public void Show(CatalogEntry entry)
        {
            if (entry == null) return;
            _current = entry;
            EnsureBuilt();
            if (_root == null) return;
            Populate(entry);
            _root.style.display = DisplayStyle.Flex;
            _root.pickingMode = PickingMode.Position;
        }

        /// <summary>Hide the preview (no arm). Idempotent.</summary>
        public void Hide()
        {
            _current = null;
            if (_root != null)
            {
                _root.style.display = DisplayStyle.None;
                _root.pickingMode = PickingMode.Ignore;
            }
        }

        // ── Build (once) ─────────────────────────────────────────────────────────

        private void EnsureBuilt()
        {
            if (_root != null) return;
            var docRoot = _document != null ? _document.rootVisualElement : null;
            if (docRoot == null) return;

            // Full-screen scrim — a tap on it (outside the panel) dismisses.
            _root = new VisualElement { name = "structure-info-scrim" };
            ElarionUi.StyleScrim(_root);
            // Left-anchored on landscape rather than centred, per the spec; the scrim
            // still fills the screen so click-outside dismiss works.
            _root.style.justifyContent = Justify.Center;
            _root.style.alignItems = Align.FlexStart;
            _root.style.display = DisplayStyle.None;
            // Closed scrim must not intercept pointer input. Show()/Hide() flip this.
            _root.pickingMode = PickingMode.Ignore;
            _root.RegisterCallback<PointerDownEvent>(OnScrimPointerDown);
            docRoot.Add(_root);

            // The parchment card.
            _panel = new VisualElement { name = "structure-info-panel" };
            ElarionUi.StylePanel(_panel);
            _panel.style.width = 300;
            _panel.style.maxHeight = Length.Percent(86f);
            _panel.style.marginLeft = 18;
            _panel.style.paddingTop = ElarionUi.PadPanel;
            _panel.style.paddingBottom = ElarionUi.PadPanel;
            _panel.style.paddingLeft = ElarionUi.PadPanel;
            _panel.style.paddingRight = ElarionUi.PadPanel;
            _panel.style.flexDirection = FlexDirection.Column;
            // Swallow taps so a click INSIDE the panel never bubbles to the scrim-dismiss.
            _panel.RegisterCallback<PointerDownEvent>(e => e.StopPropagation());
            _root.Add(_panel);

            // Header: name + tier badge.
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.alignItems = Align.Center;

            _nameLabel = new Label("Structure");
            _nameLabel.style.color = ElarionUi.Gilt;
            _nameLabel.style.fontSize = ElarionUi.FontHead;
            _nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _nameLabel.style.whiteSpace = WhiteSpace.Normal;
            _nameLabel.style.flexShrink = 1;
            header.Add(_nameLabel);

            _tierBadge = new Label("Lv 1");
            _tierBadge.style.color = ElarionUi.Aether;
            _tierBadge.style.fontSize = ElarionUi.FontLabel;
            _tierBadge.style.unityFontStyleAndWeight = FontStyle.Bold;
            _tierBadge.style.marginLeft = 8;
            _tierBadge.style.flexShrink = 0;
            header.Add(_tierBadge);
            _panel.Add(header);

            _panel.Add(ElarionUi.MakeRule());

            // Short description.
            _descLabel = new Label();
            _descLabel.style.color = ElarionUi.ParchmentDim;
            _descLabel.style.fontSize = ElarionUi.FontLabel;
            _descLabel.style.whiteSpace = WhiteSpace.Normal;
            _descLabel.style.marginBottom = 8;
            _panel.Add(_descLabel);

            // Targeting capability (towers only): "Land only" / "Land + Air" / "Air only".
            // Colorblind-safe: the meaning is carried by the TEXT label + a distinct leading
            // shape glyph per capability (never color alone). Gilt so it reads as a key stat.
            _targetingLabel = new Label();
            _targetingLabel.style.color = ElarionUi.Gilt;
            _targetingLabel.style.fontSize = ElarionUi.FontLabel;
            _targetingLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _targetingLabel.style.whiteSpace = WhiteSpace.Normal;
            _targetingLabel.style.marginBottom = 8;
            _panel.Add(_targetingLabel);

            // Cost.
            _costLabel = MakeKeyValue("Cost", "Free");
            _panel.Add(_costLabel);

            // Footprint.
            _footprintLabel = MakeKeyValue("Footprint", "1x1");
            _panel.Add(_footprintLabel);

            // Stats box (DPS / Range / Fire Rate — rows added per-entry).
            _statsBox = new VisualElement();
            _statsBox.style.marginTop = 8;
            _statsBox.style.flexDirection = FlexDirection.Column;
            _panel.Add(_statsBox);

            // Next-tier preview box (blue/aether info panel).
            _nextTierBox = new VisualElement();
            _nextTierBox.style.marginTop = 10;
            _nextTierBox.style.paddingTop = 8; _nextTierBox.style.paddingBottom = 8;
            _nextTierBox.style.paddingLeft = 10; _nextTierBox.style.paddingRight = 10;
            _nextTierBox.style.backgroundColor = ElarionUi.AetherDim;
            ElarionUi.SetRadius(_nextTierBox, ElarionUi.RadiusMd);
            ElarionUi.SetBorderWidth(_nextTierBox, 1);
            ElarionUi.SetBorderColor(_nextTierBox, new Color(ElarionUi.Aether.r, ElarionUi.Aether.g, ElarionUi.Aether.b, 0.6f));

            _nextTierTitle = new Label("Upgrade to Lv 2");
            _nextTierTitle.style.color = ElarionUi.Parchment;
            _nextTierTitle.style.fontSize = ElarionUi.FontLabel;
            _nextTierTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            _nextTierBox.Add(_nextTierTitle);

            _nextTierStats = new Label();
            _nextTierStats.style.color = ElarionUi.ParchmentDim;
            _nextTierStats.style.fontSize = ElarionUi.FontLabel;
            _nextTierStats.style.whiteSpace = WhiteSpace.Normal;
            _nextTierBox.Add(_nextTierStats);

            _nextTierCost = new Label();
            _nextTierCost.style.color = ElarionUi.Affordable;
            _nextTierCost.style.fontSize = ElarionUi.FontLabel;
            _nextTierCost.style.marginTop = 2;
            _nextTierBox.Add(_nextTierCost);
            _panel.Add(_nextTierBox);

            // Action buttons.
            var actions = new VisualElement();
            actions.style.flexDirection = FlexDirection.Row;
            actions.style.justifyContent = Justify.SpaceBetween;
            actions.style.marginTop = 12;

            var placeBtn = new Button(() =>
            {
                var entry = _current;
                Hide();
                OnPlaceRequested?.Invoke(entry);
            })
            { text = "Place Structure" };
            ElarionUi.StyleButton(placeBtn, ElarionUi.ButtonKind.Gold);
            placeBtn.style.flexGrow = 1;
            placeBtn.style.marginRight = 8;
            actions.Add(placeBtn);

            var cancelBtn = new Button(() =>
            {
                Hide();
                OnCancelRequested?.Invoke();
            })
            { text = "Cancel" };
            ElarionUi.StyleButton(cancelBtn, ElarionUi.ButtonKind.Neutral);
            cancelBtn.style.minWidth = 88;
            actions.Add(cancelBtn);
            _panel.Add(actions);
        }

        /// <summary>A "click outside the panel" on the scrim dismisses the preview.</summary>
        private void OnScrimPointerDown(PointerDownEvent evt)
        {
            // Only the scrim itself (not a child bubbling up — the panel stops props).
            if (evt.target == _root)
            {
                Hide();
                OnCancelRequested?.Invoke();
            }
        }

        // ── Populate (per Show) ──────────────────────────────────────────────────

        private void Populate(CatalogEntry e)
        {
            var repo = e != null ? e.repo : null;

            _nameLabel.text = string.IsNullOrEmpty(e.displayName) ? e.id : e.displayName;

            int maxLevel = MaxLevelFor(repo);
            _tierBadge.text = maxLevel > 1 ? "Lv 1 / " + maxLevel : "Lv 1";

            _descLabel.text = DescriptionFor(e);

            // Targeting line — towers only; hidden (no reserved gap) for non-combat structures.
            string targeting = TargetingLabelFor(e);
            if (string.IsNullOrEmpty(targeting))
            {
                _targetingLabel.style.display = DisplayStyle.None;
            }
            else
            {
                _targetingLabel.style.display = DisplayStyle.Flex;
                _targetingLabel.text = targeting;
            }

            SetKeyValue(_costLabel, "Cost", CostLabel(CostFor(e)));
            SetKeyValue(_footprintLabel, "Footprint", FootprintLabel(e));

            RenderCurrentStats(e, repo);
            RenderNextTierPreview(e, repo, maxLevel);
        }

        /// <summary>Current-tier key stats — DPS, Range, Fire Rate (only the populated ones).</summary>
        private void RenderCurrentStats(CatalogEntry e, RepoProps repo)
        {
            _statsBox.Clear();
            if (repo == null) return;

            bool any = false;
            float dps = repo.damage * (repo.fireRate > 0f ? repo.fireRate : 1f);
            if (repo.damage > 0f) { _statsBox.Add(MakeKeyValue("DPS", FormatNum(dps))); any = true; }
            if (repo.range  > 0f) { _statsBox.Add(MakeKeyValue("Range", FormatNum(repo.range) + "m")); any = true; }
            if (repo.fireRate > 0f) { _statsBox.Add(MakeKeyValue("Fire Rate", FormatNum(repo.fireRate) + "/s")); any = true; }

            if (!any)
            {
                // Non-combat structure (wall / resource / decoration) — show its role line.
                var role = MakeKeyValue("Type", e.type.ToString());
                _statsBox.Add(role);
            }
        }

        /// <summary>
        /// Next-tier upgrade preview: the L1→L2 stat deltas + the upgrade cost. Hidden
        /// for single-tier (maxLevel 1) entries. Cost MIRRORS BuildModeController's
        /// UpgradeCostFor (authored table wins, else the build-cost scaler).
        /// </summary>
        private void RenderNextTierPreview(CatalogEntry e, RepoProps repo, int maxLevel)
        {
            if (repo == null || maxLevel <= 1)
            {
                _nextTierBox.style.display = DisplayStyle.None;
                return;
            }
            _nextTierBox.style.display = DisplayStyle.Flex;

            _nextTierTitle.text = "Upgrade to Lv 2";

            // Per-tier tower multiplier matches BuildModeController.ApplyTierStats (L2 ×1.25).
            const float l2Mul = 1.25f;
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
            _nextTierStats.text = string.Join("\n", parts);

            DeNelle.Core.Catalog.ResourceCost up = UpgradeCostFor(e, repo, 1);
            _nextTierCost.text = "Cost: " + CostLabel(up);
        }

        // ── Cost / stat resolution (mirrors BuildModeController) ──────────────────

        /// <summary>Resolve build cost: authored multi-cost wins, else crystals-only buildCost.</summary>
        private static DeNelle.Core.Catalog.ResourceCost CostFor(CatalogEntry e)
        {
            var repo = e != null ? e.repo : null;
            if (repo == null) return default;
            if (!repo.cost.IsZero) return repo.cost;
            return new DeNelle.Core.Catalog.ResourceCost { crystals = repo.buildCost };
        }

        /// <summary>
        /// Resolve the upgrade cost for <paramref name="fromLevel"/> → fromLevel+1. Mirrors
        /// BuildModeController.UpgradeCostFor: authored repo.upgradeCost wins, else the build
        /// cost scaled by the level being left.
        /// </summary>
        private static DeNelle.Core.Catalog.ResourceCost UpgradeCostFor(CatalogEntry e, RepoProps repo, int fromLevel)
        {
            if (repo == null) return default;
            int idx = Mathf.Max(0, fromLevel - 1);
            if (repo.upgradeCost != null && idx < repo.upgradeCost.Length)
            {
                var authored = repo.upgradeCost[idx];
                if (!authored.IsZero) return authored;
            }
            DeNelle.Core.Catalog.ResourceCost baseCost = CostFor(e);
            int scale = Mathf.Max(1, fromLevel);
            return new DeNelle.Core.Catalog.ResourceCost
            {
                wood     = baseCost.wood     * scale,
                food     = baseCost.food     * scale,
                iron     = baseCost.iron     * scale,
                crystals = baseCost.crystals * scale,
            };
        }

        private static int MaxLevelFor(RepoProps repo)
            => repo == null ? 1 : Mathf.Clamp(repo.maxLevel, 1, 3);

        /// <summary>Compact multi-resource cost string (skips zero slots). Mirrors the palette.</summary>
        private static string CostLabel(DeNelle.Core.Catalog.ResourceCost c)
        {
            if (c.IsZero) return "Free";
            var parts = new List<string>(4);
            if (c.wood     > 0) parts.Add(c.wood     + "W");
            if (c.food     > 0) parts.Add(c.food     + "F");
            if (c.iron     > 0) parts.Add(c.iron     + "I");
            if (c.crystals > 0) parts.Add("*" + c.crystals);
            return string.Join("  ", parts);
        }

        /// <summary>
        /// Footprint in grid cells (e.g. "2×2 cells") via the live PlacementGrid + the
        /// measured upright footprint. Falls back to "1×1" when the grid / measure is
        /// unavailable so the row always shows something sane.
        /// </summary>
        private static string FootprintLabel(CatalogEntry e)
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

        /// <summary>
        /// A short, type-derived description (CatalogEntry carries no description field, so
        /// we synthesize one from the structure role). Keeps the preview readable without a
        /// catalog-schema change.
        /// </summary>
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

        /// <summary>
        /// Targeting-capability descriptor for a DEFENSIVE tower — what layers it can hit,
        /// so the player picks the right anti-air / anti-ground counter BEFORE placing
        /// (owner 2026-07-08: Ballista = Air only, ground towers = Land only, Wizard/Arcane
        /// = Land + Air). Computed from the repo flags (RepoProps.airOnly / canHitAir):
        ///   airOnly            → "Air only"
        ///   canHitAir (!air)   → "Land + Air"
        ///   else               → "Land only"
        /// Returns null for non-tower structures (walls/gates/resources/decorations get no
        /// targeting line). Colorblind-safe: meaning is the TEXT + a distinct leading shape
        /// glyph per capability, never color alone (owner is red/green colorblind).
        /// </summary>
        private static string TargetingLabelFor(CatalogEntry e)
        {
            if (e == null || e.type != CatalogType.Tower) return null;
            var repo = e.repo;
            if (repo == null) return null;
            // Read the flags defensively via the tolerant reader so this survives if the
            // Ballista repurpose swaps how airOnly is surfaced.
            bool airOnly   = repo.airOnly;
            bool canHitAir = repo.canHitAir || airOnly;
            if (airOnly)   return "Targets: Air only";        // up-triangle = sky
            if (canHitAir) return "Targets: Land + Air";      // diamond = both
            return "Targets: Land only";                      // bar = ground
        }

        private static string FormatNum(float v)
            => Mathf.Approximately(v, Mathf.Round(v)) ? Mathf.RoundToInt(v).ToString() : v.ToString("0.0");

        // ── Small key/value row helper ───────────────────────────────────────────

        private static Label MakeKeyValue(string key, string value)
        {
            var row = new Label();
            row.style.color = ElarionUi.Parchment;
            row.style.fontSize = ElarionUi.FontLabel;
            row.style.marginTop = 2;
            SetKeyValue(row, key, value);
            return row;
        }

        private static void SetKeyValue(Label row, string key, string value)
        {
            if (row != null) row.text = key + ":  " + value;
        }
    }
}
