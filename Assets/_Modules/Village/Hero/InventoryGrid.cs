// =============================================================================
// InventoryGrid — the Bag's STAGE: whichever section the rail has selected.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WO-1133 D3. The stage is 56% of the panel's interior width and holds exactly one
// thing at a time:
//   * the GEAR section (rail entry one) - the hero niche plus the five worn slots;
//   * a CONTENT section - the item grid, six columns, cells sized FROM the stage;
//   * a PSEUDO section (Skills / dormant Map) - its authored sentence, never blank.
//
// TWO NUMBERS CHANGED AND BOTH MATTERED:
//   * The grid was 5 columns of a hardcoded `new Vector2(78f, 72f)` cell. 78x72 is
//     well under the 112 ref px touch floor, which is captured defect 4 exactly -
//     tiny tiles with 1-character rarity letters, near-illegible at arm's length.
//   * The cell size is now DERIVED from the measured stage width, not authored as a
//     literal. D3 states 226 px cells at 2670x1200; that number is what 6 columns
//     across this stage RESOLVES to at that resolution, so deriving it reproduces
//     the design and stays correct at every other resolution instead of being
//     right at exactly one. A literal would be wrong everywhere else.
//
// EMPTY IS THE NORMAL CASE, NOT AN EDGE CASE (D8/D9): two items in twenty-five slots
// is what early game looks like. An empty section never shows nothing - it says WHAT
// FILLS IT, in a sentence that comes from canon-strings.json (via InventoryStrings),
// never typed here.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DeNelle.Core.UI;
using DeNelle.Core.UI.Mvvm;
using DeNelle.Core.Diagnostics;
using DeNelle.Village.Hero;
using DeNelle.Village.Items;

namespace DeNelle.Village
{
    public sealed partial class HeroInventoryController : MonoBehaviour
    {
        /// <summary>Grid gutter between cells, in canvas reference px (D3: 16 device px).</summary>
        private const float GridGapPx = 13f;
        /// <summary>Grid padding inside the stage, reference px (D3: 28 device px each side).</summary>
        private const float GridPadPx = 22f;

        /// <summary>Repaint the stage for the current rail section. Never throws into Render.</summary>
        private void RebuildStage()
        {
            if (_stageRoot == null) return;
            for (int i = _stageRoot.transform.childCount - 1; i >= 0; i--)
            {
                var child = _stageRoot.transform.GetChild(i);
                child.SetParent(null, false);
                Destroy(child.gameObject);
            }

            if (_railIndex == RailGear)  { BuildGearSection(_stageRoot.transform); return; }
            if (_railIndex == RailSkills)
            {
                BuildSectionNote(_stageRoot.transform, InventoryStrings.Get(InventoryStrings.KeyEmptySkills));
                return;
            }
            if (_railIndex == RailMap)
            {
                BuildSectionNote(_stageRoot.transform, InventoryStrings.Get(InventoryStrings.KeyEmptyMapLocked));
                return;
            }
            BuildItemGrid(_stageRoot.transform);
        }

        // =====================================================================
        //  GEAR SECTION (D3) — the promoted gear view, rail entry one.
        // =====================================================================
        //
        // ⚠ WHAT THIS SECTION DELIBERATELY DOES NOT DEPEND ON. D1 answers "make the gear
        // view useful or cut it" with "neither - PROMOTE it", on the grounds that
        // EquipmentPanel already renders a large live 3D hero. Captured evidence since
        // (F8 seq 3585) says that panel's RT probe reports a UNIFORM CLEAR COLOUR: its
        // 3D hero does not draw either. Promoting a screen whose centrepiece is blank
        // would ship the very defect this ticket was raised to remove.
        //
        // So the section is built so that the 3D is the BONUS, not the payload: the five
        // worn slots answer "what am I wearing" from pure model data and always render,
        // the niche mounts the live hero ONLY through TryMountHeroPreview's evidence gate
        // (and shows the honest 2D portrait when the rig drew nothing), and the action
        // Device testing later ruled the blank EquipmentPanel destination non-interactive.
        private void BuildGearSection(Transform stage)
        {
            // The five worn slots use the full stage. A vacant slot reads "empty", never a blank plate.
            var slots = new[]
            {
                new WornSlot(InventoryStrings.KeySlotMainHand, WornName(_loadout != null ? _loadout.EquippedWeapon  : null), RailWeapons),
                new WornSlot(InventoryStrings.KeySlotOffHand,  WornName(_loadout != null ? _loadout.EquippedOffHand : null), RailOffHand),
                new WornSlot(InventoryStrings.KeySlotArmor,    WornArmorName(_loadout != null ? _loadout.EquippedArmor : null), RailArmor),
                new WornSlot(InventoryStrings.KeySlotAmulet,   WornAccessoryName(_loadout != null ? _loadout.EquippedAmulet : null), RailTrinkets),
                new WornSlot(InventoryStrings.KeySlotRing,     WornAccessoryName(_loadout != null ? _loadout.EquippedRing   : null), RailTrinkets),
            };

            int filled = 0;
            {
                // Both aspects use two columns: the portrait stage is deliberately shallow so
                // the detail pane remains useful, and five stacked rows would fall below 112px.
                // A 2x3 arrangement keeps every action visible without scrolling.
                const float xGap = 0.018f, yGap = 0.012f;
                float w = (1f - xGap) * 0.5f;
                float h = (1f - yGap * 2f) / 3f;
                for (int i = 0; i < slots.Length; i++)
                {
                    int row = i / 2, col = i % 2;
                    float x0 = col == 0 ? 0f : w + xGap;
                    float y1 = 1f - row * (h + yGap);
                    BuildWornSlot(stage, slots[i], new Vector2(x0, y1 - h), new Vector2(x0 + w, y1));
                    if (slots[i].ItemName != null) filled++;
                }
            }

            FlowTrace.Step("Inventory",
                $"Gear summary built: {filled}/{slots.Length} worn slots filled; no blank EquipmentPanel action.");
        }

        /// <summary>One worn-slot row: the slot's own name in caps, and what is in it.</summary>
        private readonly struct WornSlot
        {
            /// <summary>canon-strings key for the slot's name (never a literal).</summary>
            public readonly string LabelKey;
            /// <summary>The worn item's display name, or null when the slot is vacant.</summary>
            public readonly string ItemName;
            /// <summary>The visible item category opened when this row is tapped.</summary>
            public readonly int BrowseRailIndex;
            public WornSlot(string labelKey, string itemName, int browseRailIndex)
            {
                LabelKey = labelKey;
                ItemName = itemName;
                BrowseRailIndex = browseRailIndex;
            }
            public WornSlot(string labelKey, string itemName) : this(labelKey, itemName, RailGear) { }
        }

        private void BuildWornSlot(Transform stage, WornSlot slot, Vector2 min, Vector2 max)
        {
            // D4: the worn-gear plate is the kit slot art (slot_armor), not a hand-rolled frame.
            var plate = AddImage(stage, "WornSlot", min, max, Color.white);
            plate.name = "WornSlot";
            var plateImage = plate.GetComponent<Image>();
            var panelSprite = Resources.Load<Sprite>("UI/ElarionMedieval/frames/content-panel");
            if (plateImage != null && panelSprite != null)
            {
                plateImage.sprite = panelSprite;
                plateImage.type = Image.Type.Simple;
                plateImage.color = Color.white;
            }

            // A worn row is useful navigation, not a dead summary plate: tapping it opens the
            // matching pile where replacements can be inspected. This changes no equip semantics.
            var browse = plate.AddComponent<Button>();
            browse.targetGraphic = plate.GetComponent<Image>();
            int destination = slot.BrowseRailIndex;
            browse.onClick.AddListener(() => SelectRail(destination));

            var key = AddLabel(plate.transform, InventoryStrings.Get(slot.LabelKey).ToUpperInvariant(),
                     0.52f, 0.96f, InkMicro, ElarionUi.FontMicro,
                     TMPro.TextAlignmentOptions.MidlineLeft, 0.05f, 0.95f, spacing: 2f);
            key.raycastTarget = false;
            ElarionUiKit.FitSingleLine(key, 0f, ElarionUi.FontMicro);

            bool vacant = string.IsNullOrEmpty(slot.ItemName);
            var val = AddLabel(plate.transform,
                     vacant ? InventoryStrings.Get(InventoryStrings.KeySlotEmpty)
                            : ElarionUiKit.SpacedDisplayName(slot.ItemName),
                     0.06f, 0.50f, vacant ? InkDim : Ink, ElarionUi.FontLabel,
                     TMPro.TextAlignmentOptions.MidlineLeft, 0.05f, 0.95f, bold: !vacant);
            val.raycastTarget = false;
            if (vacant) val.fontStyle |= TMPro.FontStyles.Italic;   // vacancy reads as a state, not a hue
            ElarionUiKit.FitSingleLine(val, 0f, ElarionUi.FontLabel);
        }

        private static string WornName(WeaponDef w) => w != null ? (w.name ?? w.id) : null;
        private static string WornArmorName(ArmorDef a) => a != null ? (a.name ?? a.id) : null;
        private static string WornAccessoryName(AccessoryDef a) => a != null ? (a.name ?? a.id) : null;

        // =====================================================================
        //  CONTENT SECTIONS — the item grid.
        // =====================================================================
        private void BuildItemGrid(Transform stage)
        {
            // §12 — the data-empty vs render-broken discriminator, kept from WO-573. If owned=0
            // across sections the grid is DATA-EMPTY (no owned-item model), NOT a build failure;
            // if owned>0 but slots=0 the projection is broken; if slots>0 but no children get
            // built (logged after the pass) it is built-but-invisible.
            int owned = -1;
            try { owned = _store != null ? _store.OwnedCounts.Count : -1; } catch { owned = -2; }
            int slotCount = (_vm != null && _vm.Slots != null) ? _vm.Slots.Count : -1;
            FlowTrace.Step("Inventory",
                $"BuildItemGrid tab={_vm?.ActiveTab} rail={_railIndex} store={(_store == null ? "NULL" : "ok")} " +
                $"ownedCounts={owned} slots={slotCount}");

            if (_vm == null)
            {
                BuildSectionNote(stage, InventoryStrings.EmptyLineFor(InventoryTabKind.Weapons));
                FlowTrace.Warn("Inventory", "BuildItemGrid: no bound VM — showing the section note (data-empty).");
                return;
            }

            // THE EMPTY SECTION IS DESIGNED, NOT AN AFTERTHOUGHT (D8.5). A section with nothing
            // in it does not paint twenty-five decorative empty plates that say nothing; it says
            // what fills it, in one sentence, and leaves the stage quiet.
            if (_vm.Slots == null || _vm.Slots.Count == 0)
            {
                BuildSectionNote(stage, InventoryStrings.EmptyLineFor(_vm.ActiveTab));
                FlowTrace.Step("Inventory",
                    $"BuildItemGrid: section {_vm.ActiveTab} is empty — showing its authored 'what fills it' line.");
                return;
            }

            BuildPeekStrip(stage);
        }

        private void BuildLegacyItemGrid(Transform stage)
        {

            var viewport = AddImage(stage, "Viewport",
                                    new Vector2(0f, 0f), new Vector2(1f, 1f),
                                    new Color(0, 0, 0, 0));
            var mask = viewport.AddComponent<RectMask2D>();
            var scroll = viewport.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            var crt = content.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0f, 1f);
            crt.anchorMax = new Vector2(1f, 1f);
            crt.pivot = new Vector2(0.5f, 1f);
            crt.anchoredPosition = Vector2.zero;
            crt.sizeDelta = Vector2.zero;
            scroll.content = crt;
            scroll.viewport = viewport.GetComponent<RectTransform>();

            var grid = content.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = GridColumns;
            grid.spacing = new Vector2(GridGapPx, GridGapPx);
            grid.padding = new RectOffset((int)GridPadPx, (int)GridPadPx, (int)GridPadPx, (int)GridPadPx);
            grid.childAlignment = TextAnchor.UpperLeft;

            // DERIVE the cell from the MEASURED stage width (see the file header). A layout pass
            // is forced first so the rect is resolved rather than zero on the creation frame —
            // the same trap PostScaleCanvasHeight documents for the Close band.
            Canvas.ForceUpdateCanvases();
            var srt = stage as RectTransform ?? stage.GetComponent<RectTransform>();
            float stageW = srt != null ? srt.rect.width : 0f;
            float cell = (stageW - GridPadPx * 2f - GridGapPx * (GridColumns - 1)) / GridColumns;
            if (cell < ElarionUiKit.MinTouchPx)
            {
                // Never ship a sub-floor tap target. If the stage is genuinely too narrow for six
                // columns at the floor, the FLOOR wins and the grid drops a column — a five-wide
                // grid of usable cells beats a six-wide grid of unusable ones.
                FlowTrace.Warn("Inventory", string.Format(
                    "Grid: {0} columns across a {1:F0} ref px stage yields a {2:F0} px cell, under the " +
                    "{3:F0} px touch floor. Dropping to {4} columns — the floor wins over the column count.",
                    GridColumns, stageW, cell, ElarionUiKit.MinTouchPx, GridColumns - 1));
                grid.constraintCount = GridColumns - 1;
                cell = (stageW - GridPadPx * 2f - GridGapPx * (GridColumns - 2)) / (GridColumns - 1);
            }
            cell = Mathf.Max(cell, ElarionUiKit.MinTouchPx);
            grid.cellSize = new Vector2(cell, cell);

            FlowTrace.Step("Inventory", string.Format(
                "Grid geometry: stage={0:F0} ref px, cols={1}, pad={2:F0}, gap={3:F0} -> cell {4:F0}x{4:F0} " +
                "(touch floor {5:F0}; authored above it, so ClampMinTouch is a no-op).",
                stageW, grid.constraintCount, GridPadPx, GridGapPx, cell, ElarionUiKit.MinTouchPx));

            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            BuildCellsFromVM(content.transform);

            Canvas.ForceUpdateCanvases();
            var vrt = viewport.GetComponent<RectTransform>();
            if (vrt != null) LayoutRebuilder.ForceRebuildLayoutImmediate(vrt);
            LayoutRebuilder.ForceRebuildLayoutImmediate(crt);

            FlowTrace.Step("Inventory",
                $"BuildItemGrid: built, content children={content.transform.childCount} (slots={_vm?.Slots?.Count ?? -1}).");
        }

        private void BuildPeekStrip(Transform stage)
        {
            const float landscapeCard = 240f;
            const float portraitRow = 160f;

            // The kit owns the viewport, mask, track and gilt thumb. This screen only
            // changes the axis and the visibility policy required by WO-1254.
            var scrollHost = AddImage(stage, "PeekStage",
                _portraitLayout ? new Vector2(0f, 0.14f) : new Vector2(0f, 0.14f),
                Vector2.one, new Color(0f, 0f, 0f, 0f));
            NoRaycast(scrollHost);
            var zone = ElarionUiKit.MakeScrollZone(scrollHost.transform, GridGapPx, (int)GridPadPx);

            // WO-1293 (device seq 4077): an NRE inside this method blanked the peek strip while
            // SafeRun let the rest of the Bag survive. The stack could not say WHICH reference
            // was null, so every kit-handle lookup is probed and named here BEFORE any
            // dereference -- the next capture pinpoints the dead piece from data (CLAUDE.md
            // section 12), and each use below is guarded so a missing piece degrades the strip
            // instead of killing it.
            if (zone == null || zone.content == null || zone.scroll == null)
            {
                FlowTrace.Fail("Inventory",
                    "BuildPeekStrip: MakeScrollZone handle incomplete -- zone=" +
                    (zone == null ? "NULL" : "ok") +
                    " content=" + (zone == null || zone.content == null ? "NULL" : "ok") +
                    " scroll=" + (zone == null || zone.scroll == null ? "NULL" : "ok") +
                    "; cannot build the peek strip.");
                return;
            }
            zone.content.gameObject.name = "PeekContent";

            var column = zone.content.GetComponent<VerticalLayoutGroup>();
            var fitter = zone.content.GetComponent<ContentSizeFitter>();
            var scrollbarRt = zone.scrollbar != null ? zone.scrollbar.transform as RectTransform : null;
            FlowTrace.Step("Inventory",
                "BuildPeekStrip probes: portrait=" + _portraitLayout +
                " column=" + (column == null ? "NULL" : "ok") +
                " fitter=" + (fitter == null ? "NULL" : "ok") +
                " scrollbar=" + (zone.scrollbar == null ? "NULL" : "ok") +
                " scrollbarRt=" + (scrollbarRt == null ? "NULL" : "ok") +
                " viewport=" + (zone.viewport == null ? "NULL" : "ok"));
            if (column == null || fitter == null || zone.scrollbar == null || scrollbarRt == null || zone.viewport == null)
            {
                FlowTrace.Warn("Inventory",
                    "BuildPeekStrip: a kit scroll-zone piece is missing (probe line above names it). " +
                    "MakeScrollZone's contract builds all of them -- a NULL here is the WO-1293 dead " +
                    "reference. Continuing on the hardened path.");
            }

            if (_portraitLayout)
            {
                if (column != null) column.padding.bottom = Mathf.CeilToInt(portraitRow * 0.40f);
                zone.scroll.horizontal = false;
                zone.scroll.vertical = true;
                zone.scroll.verticalScrollbar = zone.scrollbar;
            }
            else
            {
                if (column != null) column.enabled = false;
                // A GameObject may own only one LayoutGroup. The old code disabled the kit's
                // VerticalLayoutGroup and then attempted to AddComponent<HorizontalLayoutGroup>
                // on the SAME object; Unity rejects that combination and the stocked Bag renders
                // an empty stage. Place cards explicitly along X instead. This is deterministic,
                // avoids a deferred component swap, and keeps MakeScrollZone's mask/track intact.
                if (fitter != null)
                {
                    fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                    fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
                }
                zone.content.anchorMin = new Vector2(0f, 0f);
                zone.content.anchorMax = new Vector2(0f, 1f);
                zone.content.pivot = new Vector2(0f, 0.5f);
                zone.scroll.horizontal = true;
                zone.scroll.vertical = false;
                zone.scroll.verticalScrollbar = null;
                zone.scroll.horizontalScrollbar = zone.scrollbar;
                if (zone.scrollbar != null) zone.scrollbar.direction = Scrollbar.Direction.LeftToRight;
                if (scrollbarRt != null)
                {
                    // `as RectTransform` returns null SILENTLY on a mismatch -- that cast is one
                    // of the WO-1293 candidates, which is why srt is probed above, never assumed.
                    scrollbarRt.anchorMin = new Vector2(0f, 0f);
                    scrollbarRt.anchorMax = new Vector2(1f, 0f);
                    scrollbarRt.pivot = new Vector2(0.5f, 0f);
                    scrollbarRt.offsetMin = Vector2.zero;
                    scrollbarRt.offsetMax = new Vector2(0f, 10f);
                }
            }

            BuildCellsFromVM(zone.content);
            float itemExtent = _portraitLayout ? portraitRow : landscapeCard;
            for (int i = 0; i < zone.content.childCount; i++)
            {
                var child = zone.content.GetChild(i) as RectTransform;
                if (child == null) continue;
                if (_portraitLayout)
                {
                    child.sizeDelta = new Vector2(0f, portraitRow);
                }
                else
                {
                    child.anchorMin = new Vector2(0f, 0.5f);
                    child.anchorMax = new Vector2(0f, 0.5f);
                    child.pivot = new Vector2(0f, 0.5f);
                    child.sizeDelta = new Vector2(landscapeCard, landscapeCard);
                    child.anchoredPosition = new Vector2(
                        GridPadPx + i * (landscapeCard + GridGapPx), 0f);
                }
            }
            if (!_portraitLayout)
            {
                float width = GridPadPx + zone.content.childCount * landscapeCard +
                              Mathf.Max(0, zone.content.childCount - 1) * GridGapPx +
                              landscapeCard * 0.40f;
                zone.content.sizeDelta = new Vector2(width, 0f);
            }

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(zone.content);
            float viewportExtent = zone.viewport != null
                ? (_portraitLayout ? zone.viewport.rect.height : zone.viewport.rect.width)
                : 0f;
            int visible = Mathf.Max(1, Mathf.FloorToInt(
                (viewportExtent - GridPadPx * 2f) / (itemExtent + GridGapPx)));
            int overflow = Mathf.Max(0, _vm.Slots.Count - visible);
            bool hasOverflow = overflow > 0;
            if (zone.scrollbar != null) zone.scrollbar.gameObject.SetActive(hasOverflow);
            if (_portraitLayout)
                zone.scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            else
                zone.scroll.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

            if (hasOverflow)
            {
                string key = _portraitLayout ? InventoryStrings.KeyMoreBelow : InventoryStrings.KeyMoreCount;
                var more = AddLabel(stage, InventoryStrings.Format(key, overflow), 0.02f, 0.13f, Ink,
                    ElarionUi.FontMicro, TMPro.TextAlignmentOptions.MidlineLeft, 0.02f, 0.98f, bold: true);
                more.raycastTarget = false;
            }
            FlowTrace.Step("Inventory", $"Peek strip: axis={(_portraitLayout ? "vertical" : "horizontal")} " +
                $"cards={_vm.Slots.Count}, visible={visible}, overflow={overflow}, peek=40%, " +
                $"word={(hasOverflow ? (_portraitLayout ? "more-below" : "more") : "none")}, " +
                $"scrollbar={(hasOverflow ? "Permanent" : "none")}");
        }

        // The grid is a pure projection of vm.Slots (OWNED items in the active section). Every
        // cell's id/name/icon-keys/rarity/equipped come from the ItemVM; a tap routes to
        // vm.SelectById(id) and the PANE does the rest. Selection highlight is driven by
        // vm.SelectedId.
        private void BuildCellsFromVM(Transform content)
        {
            using var _ = FlowTrace.Enter("Inventory", $"BuildCellsFromVM tab={_vm?.ActiveTab}");
            if (_vm == null) return;

            var slots = _vm.Slots;
            string selId = _vm.SelectedId;
            bool isConsumables = _vm.ActiveTab == InventoryTabKind.Consumables;
            int wantCount = slots != null ? slots.Count : 0;
            int built = 0, failed = 0;

            if (slots != null && slots.Count > 0)
            {
                var (b, f) = Guard.TryEach("Inventory", "build inventory cell", slots, item =>
                {
                    var it = item;   // capture for the closure
                    Sprite iconSp = ResolveItemIcon(it.IconRole, it.IconName);
                    string glyph = GlyphForRole(it.IconRole, it.IconName);
                    bool selected = selId != null &&
                                    string.Equals(selId, it.Id, System.StringComparison.OrdinalIgnoreCase);
                    BuildGearCell(content, glyph, iconSp, it.Name, it.Rarity, it.Equipped, locked: false,
                                  selected: selected, lockText: "", level: it.Level,
                                  onTap: () =>
                                  {
                                      if (_vm == null) return;
                                      FlowTrace.Step("Inventory",
                                          $"onTap id={it.Id} name='{it.Name}' tab={_vm.ActiveTab} consumable={isConsumables} (SELECT)");
                                      // A tap ONLY selects. The equip/use happens on the PANE's action
                                      // button, so re-tapping an already-worn item is never a silent no-op.
                                      _vm.SelectById(it.Id);
                                      FlowTrace.Step("Inventory",
                                          $"onTap post-select SelectedId={_vm.SelectedId}");
                                  });
                });
                built = b; failed = f;
            }

            // Pad the LAST ROW ONLY, so the grid never ends mid-row. The old pass padded to a full
            // 5x5 page of empty plates — twenty-three decorative sockets around two real items, on
            // a screen whose loudest complaint was that it said nothing (defects 1 and 5).
            int cols = GridColumns;
            int remainder = built % cols;
            int pad = remainder == 0 ? 0 : cols - remainder;
            for (int i = 0; i < pad; i++) BuildEmptySlot(content);

            FlowTrace.Step("Inventory",
                $"Section stocked {built} owned + {pad} row-fill slot(s) (wanted {wantCount}, failed {failed}).");

            if (wantCount > 0 && built == 0)
                FlowTrace.Fail("Inventory",
                    $"Section had {wantCount} owned slot(s) but built 0 cells ({failed} failed) — " +
                    "the grid shows fillers only (built-but-broken).");
        }

        // An empty slot is the kit's dim plate (the sparse-grid law): same construction as a live
        // cell, empty:true (rim/icon hidden, tap disabled). Sized by the parent GridLayoutGroup.
        private void BuildEmptySlot(Transform content)
        {
            var slot = ElarionUiKit.BuildRaritySlot(content, 0, Vector2.zero, Vector2.one, empty: true);
            if (slot != null && slot.root != null) slot.root.name = "EmptySlot";
        }

        // Pick the cell icon from the VM's role/name KEYS — presentation mapping (a key -> art),
        // not a state pull. Real item art first, pack icon fallback.
        private static Sprite ResolveItemIcon(string role, string id)
        {
            switch (role)
            {
                case InventoryVM.IconRoleWeapon:
                {
                    var s = GearIconCatalog.Resolve(InventoryVM.IconRoleWeapon, id);
                    return s != null ? s : RpgUiCatalog.Get(RpgUiCatalog.RoleIcons, RpgUiCatalog.IconSword);
                }
                case InventoryVM.IconRoleArmor:
                {
                    var s = GearIconCatalog.Resolve(InventoryVM.IconRoleArmor, id);
                    return s != null ? s : RpgUiCatalog.Get(RpgUiCatalog.RoleIcons, RpgUiCatalog.IconShield);
                }
                case InventoryVM.IconRolePotion:
                {
                    // Authored icon on the row wins (same row the name came from); the keyword
                    // sheet + pack-potion fallbacks only apply to a real consumable.
                    var authored = LoadRowIcon(id);
                    if (authored != null) return authored;
                    return ConsumableIcon(id, ItemIdentity.DisplayName(id));
                }
                case InventoryVM.IconRoleMaterial:
                {
                    // F8-641: a MATERIAL resolves art from its OWN row only and never from the
                    // potion fallbacks. Null here means the cell shows this row's authored glyph.
                    var row = ItemIdentity.Resolve(id);
                    return ItemIconCatalog.ForMaterial(id, row.IconPath, row.Category);
                }
            }
            return null;
        }

        /// <summary>The row's own authored Resources sprite (catalog iconPath), or null.</summary>
        private static Sprite LoadRowIcon(string id)
        {
            string path = ItemIdentity.IconPathOf(id);
            return string.IsNullOrEmpty(path) ? null : Resources.Load<Sprite>(path);
        }

        // The at-a-glance type glyph fallback (when no icon art) keyed off the VM role.
        private string GlyphForRole(string role, string id)
        {
            switch (role)
            {
                case InventoryVM.IconRoleWeapon: return GearIconCatalog.Glyph(InventoryVM.IconRoleWeapon, id);
                case InventoryVM.IconRoleArmor:  return GearIconCatalog.Glyph(InventoryVM.IconRoleArmor, id);
                case InventoryVM.IconRolePotion:
                case InventoryVM.IconRoleMaterial:
                {
                    var row = ItemIdentity.Resolve(id);
                    if (!string.IsNullOrEmpty(row.Glyph)) return row.Glyph;
                    return ConsumableTypeGlyph(id, row.DisplayName);
                }
            }
            return "?";
        }

        /// <summary>
        /// A section's one-sentence note, centred on the stage. Used for the empty sections and
        /// for the two pseudo-sections. The sentence ALWAYS comes from canon (the caller resolves
        /// it) and always names what fills the section — "never show nothing" (D2.2).
        /// </summary>
        private void BuildSectionNote(Transform stage, string msg)
        {
            var box = AddImage(stage, "SectionNote",
                               new Vector2(0.04f, 0.34f), new Vector2(0.96f, 0.66f),
                               new Color(0.02f, 0.02f, 0.03f, 0.92f));
            AddInnerRim(box, new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.45f));
            NoRaycast(box);
            var lbl = AddLabel(box.transform, msg, 0f, 1f, InkDim, ElarionUi.FontLabel,
                     TMPro.TextAlignmentOptions.Center, 0.06f, 0.94f);
            lbl.raycastTarget = false;
        }

        // One live cell = the kit rarity slot: sprite-first Inventory_Slot plate + the rarity_1..5
        // rim (ornate PER-TIER art — SHAPE carries the tier, colourblind-safe; the letter chip
        // reinforces it). The parent GridLayoutGroup drives the cell rect.
        private void BuildGearCell(Transform content, string icon, Sprite iconSprite, string name, string rarity,
                                   bool equipped, bool locked, bool selected, string lockText, System.Action onTap,
                                   int level = 1)
        {
            var slot = ElarionUiKit.BuildRaritySlot(content, RarityIndex(rarity),
                Vector2.zero, Vector2.one, empty: false, onTap: onTap);
            if (slot == null || slot.root == null) return;
            slot.root.name = "Cell_" + (string.IsNullOrEmpty(name) ? "item" : name);
            if (slot.button != null) ElarionUiKit.ClampMinTouch(slot.button);

            // Icon: real item art first; the type-glyph stays the no-art fallback.
            if (iconSprite != null)
            {
                slot.SetIcon(iconSprite);
                if (slot.icon != null && locked)
                    slot.icon.color = new Color(1f, 1f, 1f, 0.6f);
            }
            else
            {
                var glyphLbl = AddLabel(slot.root.transform, string.IsNullOrEmpty(icon) ? "?" : icon,
                    0.22f, 0.86f, RarityInk(rarity), ElarionUi.FontTitle + 4,
                    TMPro.TextAlignmentOptions.Center, 0.10f, 0.90f, bold: true);
                glyphLbl.raycastTarget = false;
            }

            // Identity must remain readable even when the icon is unfamiliar. The old rarity
            // slot discarded its `name` argument completely, leaving a picture-only potion with
            // no visible stack count. Reserve a backed lower caption; consumable names already
            // carry their authoritative "xN" suffix from InventoryVM.
            var namePlate = AddImage(slot.root.transform, "ItemNamePlate",
                new Vector2(0.03f, 0.02f), new Vector2(0.97f, 0.24f),
                new Color(0.015f, 0.012f, 0.018f, 0.94f));
            NoRaycast(namePlate);
            var itemName = AddLabel(namePlate.transform, ElarionUiKit.SpacedDisplayName(name ?? ""),
                0f, 1f, Ink, ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Center,
                0.04f, 0.96f, bold: true);
            itemName.raycastTarget = false;
            itemName.enableAutoSizing = true;
            itemName.fontSizeMin = 16f;
            itemName.fontSizeMax = ElarionUi.FontMicro;
            itemName.textWrappingMode = TMPro.TextWrappingModes.Normal;
            itemName.overflowMode = TMPro.TextOverflowModes.Ellipsis;

            // SELECTED = gold inner rim + lit plate (shape + brightness — never colour alone;
            // the pane also names the selection, which is the third, unmissable tell).
            if (selected)
            {
                AddInnerRim(slot.root, new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.95f));
                if (slot.plate != null)
                    slot.plate.color = new Color(1.15f, 1.10f, 0.92f, slot.plate.color.a);
            }

            // ── RARITY THAT SURVIVES GREYSCALE (D5/defect 6) ────────────────────
            // The U / C letters were the RIGHT instinct and only too small to read at arm's
            // length. The cell is now ~2x wider, and the letter is sized off FontLabel rather
            // than FontMicro, so the tier reads as a WORD-mark on top of the rim's shape.
            string numText = !string.IsNullOrEmpty(lockText) ? lockText
                           : (!string.IsNullOrEmpty(rarity) ? rarity.Substring(0, 1).ToUpper() : "");
            if (!string.IsNullOrWhiteSpace(numText))
            {
                var numLbl = AddLabel(slot.root.transform, numText, 0.02f, 0.26f,
                         Ink, ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Center, 0.68f, 0.98f, bold: true);
                numLbl.raycastTarget = false;
                ElarionUiKit.FitSingleLine(numLbl, 0f, ElarionUi.FontLabel);
            }

            if (equipped)
            {
                // The WORN badge — the WORD carries the state (D5), the tint is reinforcement only.
                var chip = AddImage(slot.root.transform, "Equipped",
                                    new Vector2(0.02f, 0.02f), new Vector2(0.46f, 0.24f),
                                    new Color(ElarionUi.Affordable.r, ElarionUi.Affordable.g, ElarionUi.Affordable.b, 0.95f));
                NoRaycast(chip);
                var chipLbl = AddLabel(chip.transform,
                         InventoryStrings.Get(InventoryStrings.KeyPaneWornBadge), 0f, 1f, ElarionUi.Ink,
                         ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Center, 0f, 1f, bold: true);
                chipLbl.raycastTarget = false;
                ElarionUiKit.FitSingleLine(chipLbl, 0f, ElarionUi.FontMicro);
            }

            // Gear power level chip, TOP-RIGHT (the only free band — the bottom edge carries the
            // rarity letter + WORN badge). Text carries the state; NOT a Button (a badge must
            // never inflate to the touch floor). Level 1 = no chip.
            if (level > 1)
            {
                var lvChip = AddImage(slot.root.transform, "GearLevel",
                                      new Vector2(0.58f, 0.74f), new Vector2(0.98f, 0.96f),
                                      new Color(0.10f, 0.095f, 0.09f, 0.92f));
                NoRaycast(lvChip);
                var lvLbl = AddLabel(lvChip.transform, "Lv " + level, 0f, 1f, ElarionUi.Gilt,
                         ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Center, 0f, 1f, bold: true);
                lvLbl.raycastTarget = false;
                ElarionUiKit.FitSingleLine(lvLbl, 0f, ElarionUi.FontMicro);
            }
            if (locked)
            {
                NoRaycast(AddImage(slot.root.transform, "Veil", Vector2.zero, Vector2.one,
                                   new Color(0.05f, 0.05f, 0.07f, 0.55f)));
                if (slot.button != null) slot.button.interactable = false;
            }
        }

        /// <summary>Canonical 0..4 rarity ladder for the kit's rarity_1..5 rim art.</summary>
        private static int RarityIndex(string rarity)
        {
            switch ((rarity ?? "common").ToLowerInvariant())
            {
                case "uncommon":  return 1;
                case "rare":      return 2;
                case "epic":      return 3;
                case "legendary": return 4;
                default:          return 0;
            }
        }

        private static void NoRaycast(GameObject go)
        {
            var img = go.GetComponent<Image>();
            if (img != null) img.raycastTarget = false;
        }
    }
}
