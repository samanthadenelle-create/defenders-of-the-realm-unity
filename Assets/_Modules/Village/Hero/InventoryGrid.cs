// =============================================================================
// InventoryGrid — grid and cells (split from HeroInventoryController).
// -----------------------------------------------------------------------------
// Exact extraction. RebuildGrid, Build*Cells, BuildGearCell. Tech-heavy for W/A
// cells (Profile, Healing, Sword icons per current). No behavior change.
// Matches ElarionUiKit dark-wood + gold look.
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
        private void RebuildGrid()
        {
            if (_gridRoot == null) return;
            for (int i = _gridRoot.transform.childCount - 1; i >= 0; i--)
                Destroy(_gridRoot.transform.GetChild(i).gameObject);

            // ── WO-573 INSTRUMENTATION (§12) — the decisive data-empty vs render-broken capture.
            // Logs the whole chain at one line: store/inventory presence, the raw owned-count size
            // (GameState.GearInventory), the active tab, and the projected slot count. If owned=0
            // across tabs the grid is DATA-EMPTY (no owned-item model — see header data gap), NOT a
            // build failure; if owned>0 but slots=0 the projection is broken; if slots>0 but no
            // content children get built (logged after the grid pass) it is built-but-invisible.
            int owned = -1;
            bool invPresent = DeNelle.Village.Crafting.VillageInventory.Instance != null;
            try { owned = _store != null ? _store.OwnedCounts.Count : -1; } catch { owned = -2; }
            int slotCount = (_vm != null && _vm.Slots != null) ? _vm.Slots.Count : -1;
            FlowTrace.Step("Inventory",
                $"RebuildGrid tab={_vm?.ActiveTab} store={(_store == null ? "NULL" : "ok")} " +
                $"villageInventory={(invPresent ? "present" : "NULL")} ownedCounts={owned} slots={slotCount}");

            // WO-582: ALWAYS render a GRID (owner: "still no grids"). The grid is a fixed set of
            // styled Obsidian Inventory_Slot frames; owned items drop into the first cells and the
            // rest stay as empty slots — so a tab with no loose items still reads as a real grid, not
            // a bare note. Only a missing VM (no inventory at all) shows the empty-state.
            if (_vm == null)
            {
                BuildEmptyState(_gridRoot.transform, "No inventory.");
                FlowTrace.Step("Inventory", $"RebuildGrid: empty-state shown (no VM).");
                return;
            }

            var viewport = AddImage(_gridRoot.transform, "Viewport",
                                    new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.98f),
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
            crt.sizeDelta = new Vector2(0f, 0f);
            scroll.content = crt;
            scroll.viewport = viewport.GetComponent<RectTransform>();

            var grid = content.AddComponent<GridLayoutGroup>();
            // Redesigned for the mockup: 5 columns in the grid (matching the landscape mockup with 5-6 columns).
            // Cell size tuned to fit 5 items cleanly per row with good margins and ornate frames.
            // In portrait, it will be 4 or 5 depending on width; scroll for additional items.
            // Uses RPG kit for clean tiles + Tech for sockets.
            bool isLandscape = Screen.width > Screen.height;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = isLandscape ? 5 : 4;
            grid.cellSize = new Vector2(78f, 72f);
            grid.spacing = new Vector2(6f, 6f);
            grid.padding = new RectOffset(4, 4, 4, 4);
            grid.childAlignment = TextAnchor.UpperCenter;

            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            BuildCellsFromVM(content.transform);

            // Force a layout pass so the grid + content size resolve immediately (mirrors
            // EquipmentPanel.FinalizeScroll) — without this the GridLayoutGroup/ContentSizeFitter
            // may not have run before first paint, leaving cells un-positioned.
            Canvas.ForceUpdateCanvases();
            var vrt = viewport.GetComponent<RectTransform>();
            if (vrt != null) LayoutRebuilder.ForceRebuildLayoutImmediate(vrt);
            LayoutRebuilder.ForceRebuildLayoutImmediate(crt);

            // WO-573 (§12): how many cells actually landed in the grid content — the built-but-
            // invisible discriminator. children>0 here with an empty-looking grid = a layout/clip
            // issue; children==0 with slots>0 = every cell threw (BuildCellsFromVM logs Fail).
            FlowTrace.Step("Inventory",
                $"RebuildGrid: grid built, content children={content.transform.childCount} (slots={_vm?.Slots?.Count ?? -1}).");
        }

        // WO-434 Phase C — the grid is now a pure projection of vm.Slots (OWNED items in the
        // active tab). Every cell's id/name/icon-keys/rarity/equipped come from the ItemVM; a
        // tap routes to vm.SelectById(id) THEN the existing equip-on-tap (vm.Equip for gear,
        // vm.Use for a consumable). No GearCatalog / GearLoadout / VillageInventory pulls remain
        // here — they live behind the VM. Selection highlight is driven by vm.SelectedId.
        private void BuildCellsFromVM(Transform content)
        {
            using var _ = FlowTrace.Enter("Inventory", $"BuildCellsFromVM tab={_vm?.ActiveTab}");
            if (_vm == null)
            {
                FlowTrace.Warn("Inventory", "BuildCellsFromVM: no bound VM — showing empty note (data-empty).");
                BuildEmptyNote(content, "No inventory.");
                return;
            }

            var slots = _vm.Slots;
            string selId = _vm.SelectedId;
            bool isConsumables = _vm.ActiveTab == InventoryTabKind.Consumables;
            int wantCount = slots != null ? slots.Count : 0;
            int built = 0, failed = 0;

            // Build the OWNED items into the first cells (guarded so one bad ItemVM never aborts the grid).
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
                                  selected: selected, lockText: "",
                                  onTap: () =>
                                  {
                                      if (_vm == null) return;
                                      // WO-585 (§12) — the decisive felt-test capture: the tap chain was
                                      // never instrumented (only grid BUILD was). Log the tapped id +
                                      // tab + consumable flag, then the resolved SelectedId post-select.
                                      FlowTrace.Step("Inventory",
                                          $"onTap id={it.Id} name='{it.Name}' tab={_vm.ActiveTab} consumable={isConsumables} (SELECT)");
                                      // WO-585 — SEPARATE select from equip: a tap now ONLY selects
                                      // (highlights the cell + opens the detail strip with an explicit
                                      // Equip/Use CTA). The actual equip/use happens on that CTA button,
                                      // so re-tapping an already-equipped item is no longer a silent no-op.
                                      _vm.SelectById(it.Id);
                                      FlowTrace.Step("Inventory",
                                          $"onTap post-select SelectedId={_vm.SelectedId}");
                                  });
                });
                built = b; failed = f;
            }

            // WO-582: PAD with empty Obsidian slots so the body always reads as a full grid (the owner's
            // "still no grids"). A tab with no loose items now shows a clean grid of empty slot frames
            // instead of a note. Fill to at least a full visible page (cols x rows), rounding owned up
            // to whole rows so the grid never ends mid-row.
            int cols = (Screen.width > Screen.height) ? 5 : 4;
            const int minRows = 5;
            int rowsForOwned = (built + cols - 1) / cols;
            int target = Mathf.Max(cols * minRows, (rowsForOwned + 1) * cols);
            for (int i = built; i < target; i++) BuildEmptySlot(content);

            FlowTrace.Step("Inventory",
                $"Inventory stocked {built} owned + {(target - built)} empty slot(s) (wanted {wantCount}, failed {failed}).");

            if (wantCount > 0 && built == 0)
                FlowTrace.Fail("Inventory",
                    $"Inventory had {wantCount} owned slot(s) but built 0 cells ({failed} failed) — grid shows empties only (built-but-broken).");
        }

        // WO-582 — an empty Obsidian inventory slot: the Blink Inventory_Slot frame (committed via
        // RpgUi/slot), tinted faint so it reads as an available-but-empty cell. Non-interactive.
        // Sized by the parent GridLayoutGroup, exactly like a real cell, so the grid stays uniform.
        private void BuildEmptySlot(Transform content)
        {
            var go = new GameObject("EmptySlot", typeof(Image));
            go.transform.SetParent(content, false);
            var img = go.GetComponent<Image>();
            var slotSprite = RpgUiCatalog.Get(RpgUiCatalog.RoleSlot, RpgUiCatalog.SlotItem);
            if (slotSprite != null)
            {
                img.sprite = slotSprite;
                img.type = Image.Type.Sliced;
                img.color = new Color(1f, 1f, 1f, 0.55f);  // faint = empty
            }
            else
            {
                img.color = new Color(Cell.r, Cell.g, Cell.b, 0.35f);
                ApplyRounded(img);
            }
            img.raycastTarget = false;
        }

        // Pick the cell icon from the VM's role/name KEYS — presentation mapping (a key -> art),
        // not a state pull. Mirrors ShopPanel.ResolveIcon: real item art first, pack icon fallback.
        private static Sprite ResolveItemIcon(string role, string id)
        {
            switch (role)
            {
                case InventoryVM.IconRoleWeapon:
                {
                    var w = GearCatalog.FindWeapon(id);
                    var s = ItemIconCatalog.ForWeapon(w);
                    return s != null ? s : RpgUiCatalog.Get(RpgUiCatalog.RoleIcons, RpgUiCatalog.IconSword);
                }
                case InventoryVM.IconRoleArmor:
                {
                    var a = GearCatalog.FindArmor(id);
                    var s = ItemIconCatalog.ForArmor(a);
                    return s != null ? s : RpgUiCatalog.Get(RpgUiCatalog.RoleIcons, RpgUiCatalog.IconShield);
                }
                case InventoryVM.IconRolePotion:
                {
                    var name = ConsumableNameFor(id);
                    return ConsumableIcon(id, name);
                }
            }
            return null;
        }

        // The at-a-glance type glyph fallback (when no icon art) keyed off the VM role.
        private string GlyphForRole(string role, string id)
        {
            switch (role)
            {
                case InventoryVM.IconRoleWeapon: return WeaponTypeGlyph(GearCatalog.FindWeapon(id));
                case InventoryVM.IconRoleArmor:  return ArmorTypeGlyph(GearCatalog.FindArmor(id));
                case InventoryVM.IconRolePotion: return ConsumableTypeGlyph(id, ConsumableNameFor(id));
            }
            return "?";
        }

        private static string ConsumableNameFor(string id)
        {
            var def = ConsumableCatalog.Find(id);
            return def != null && !string.IsNullOrEmpty(def.DisplayName) ? def.DisplayName : id;
        }

        // The empty-tab copy per category. Weapons/Armor point the player at the Gear Preview —
        // the hero's equipped gear lives there (gear is currently class+level auto-equip, so the
        // inventory has no owned-loot list yet; see the header data gap + WO-573 owner-decision flag).
        private static string EmptyTabNote(InventoryTabKind tab)
        {
            switch (tab)
            {
                case InventoryTabKind.Weapons:     return "No loose weapons in your pack.\nYour equipped weapon is shown in Gear Preview.";
                case InventoryTabKind.Armor:       return "No loose armor in your pack.\nYour equipped armor is shown in Gear Preview.";
                case InventoryTabKind.Outfits:     return "Outfits arrive with the cosmetics pass.\n(no owned skins yet)";
                case InventoryTabKind.Consumables: return "No consumables.\nCraft potions at the Workshop.";
                default:                           return "Nothing here.";
            }
        }

        // WO-573 — a STYLED obsidian empty-state that fills the grid well (black panel + thin gold
        // inner rim, the WO-554 chrome), replacing the bare gray frame. Built directly under the
        // grid root (NOT inside the GridLayoutGroup content), so the message renders full-size and
        // readable instead of being squished into a single 78x72 cell.
        private void BuildEmptyState(Transform gridRoot, string msg)
        {
            var box = AddImage(gridRoot, "EmptyState",
                               new Vector2(0.06f, 0.30f), new Vector2(0.94f, 0.70f),
                               new Color(0.02f, 0.02f, 0.03f, 0.92f));
            AddInnerRim(box, new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.45f));
            NoRaycast(box);
            AddLabel(box.transform, msg, 0f, 1f, InkDim, ElarionUi.FontLabel,
                     TMPro.TextAlignmentOptions.Center, 0.08f, 0.92f);
        }

        private void BuildGearCell(Transform content, string icon, Sprite iconSprite, string name, string rarity,
                                   bool equipped, bool locked, bool selected, string lockText, System.Action onTap)
        {
            Color rc    = RarityColor(rarity);
            Color rcInk = RarityInk(rarity);

            float frameAlpha = RarityFrameStrength(rarity);
            Color frameCol = selected
                ? new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 1f)
                : new Color(rc.r, rc.g, rc.b, locked ? frameAlpha * 0.5f : frameAlpha);
            var frame = new GameObject("CellFrame", typeof(Image));
            frame.transform.SetParent(content, false);
            var fimg = frame.GetComponent<Image>();
            Sprite techCellFrame = null;
            try {
                if (icon != null && (icon.Contains("/") || icon == "B" || icon == "S" || icon == "A" || icon == "H" || icon == "D"))
                    techCellFrame = Resources.Load<Sprite>("Tech hud elements/Sprites/Healing Tabs/H3");
                else
                    techCellFrame = Resources.Load<Sprite>("Tech hud elements/Sprites/Profile tabs/P1/fill.png");
                if (techCellFrame == null) techCellFrame = Resources.Load<Sprite>("Tech hud elements/Sprites/Menu Bars/Menu Bar 1");
            } catch (System.Exception ex) {
                // No silent failure (§12): a Tech-pack load that throws falls back to the committed
                // RpgUi plate below — but it must be logged, never swallowed blind.
                FlowTrace.Warn("Inventory",
                    $"BuildGearCell: Tech cell-frame load threw ({ex.GetType().Name}: {ex.Message}) — using RpgUi plate fallback.");
            }
            // Clean-build fallback (Tech pack gitignored): committed RpgUi grid plate frame.
            if (techCellFrame == null) techCellFrame = RpgUiCatalog.Get(RpgUiCatalog.RolePanel, RpgUiCatalog.PanelGrid);
            if (techCellFrame != null) { fimg.sprite = techCellFrame; fimg.type = Image.Type.Sliced; fimg.color = frameCol; }
            else { fimg.color = frameCol; ApplyRounded(fimg); }
            // Extra professional composition: subtle inner rim on every cell frame for depth (dark wood + gilt Forge look)
            if (techCellFrame != null)
            {
                AddInnerRim(frame, new Color(0.2f, 0.15f, 0.1f, 0.7f));
            }

            if (selected)
            {
                var glow = new GameObject("SelGlow", typeof(Image));
                glow.transform.SetParent(frame.transform, false);
                var grt = glow.GetComponent<RectTransform>();
                grt.anchorMin = new Vector2(-0.10f, -0.10f); grt.anchorMax = new Vector2(1.10f, 1.10f);
                grt.offsetMin = Vector2.zero; grt.offsetMax = Vector2.zero;
                var gimg = glow.GetComponent<Image>();
                gimg.color = new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.45f);
                gimg.raycastTarget = false;
                ApplyRounded(gimg);
                glow.transform.SetAsFirstSibling();
            }

            var cell = new GameObject("Cell", typeof(Image), typeof(Button));
            cell.transform.SetParent(frame.transform, false);
            var crt = cell.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0.04f, 0.05f); crt.anchorMax = new Vector2(0.96f, 0.95f);
            crt.offsetMin = Vector2.zero; crt.offsetMax = Vector2.zero;
            var img = cell.GetComponent<Image>();
            img.color = locked ? new Color(Cell.r, Cell.g, Cell.b, 0.85f)
                       : selected ? CellSel
                       : equipped ? CellSel : Cell;

            // WO-434 Phase C — Blink dressing (flag-gated). BlinkChrome ON + the Blink per-item
            // slot plate present → dress the inner cell tile with the Obsidian slot plate (the same
            // slot_item the shop rows use) so the grid reads as one Obsidian surface. Flag OFF (or
            // plate missing) → the EXACT current look: the RPG kit PanelInventory tile, else rounded.
            Sprite cellTile = RpgUiCatalog.Get(RpgUiCatalog.RoleSlot, RpgUiCatalog.SlotItem);
            if (cellTile == null)
                cellTile = RpgUiCatalog.Get(RpgUiCatalog.RolePanel, RpgUiCatalog.PanelInventory);
            if (cellTile != null) {
                img.sprite = cellTile;
                img.type = Image.Type.Sliced;
                img.color = Color.white;  // kit/Blink sprite provides the base; rarity applied via outer frame
            } else {
                ApplyRounded(img);
            }

            var btn = cell.GetComponent<Button>();
            btn.targetGraphic = img;
            StyleButtonColors(btn);
            if (onTap != null) btn.onClick.AddListener(() => onTap());

            bool isW = icon != null && (icon.Contains("/") || icon.Contains("B") || icon.Contains("S") || icon.Contains("A") || icon.Contains("H") || icon.Contains("D"));
            var techSock = ElarionUiKit.TechGearSocket(cell.transform, "TechIconWell", new Vector2(0.26f, 0.38f), new Vector2(0.74f, 0.95f),
                new Color(rc.r, rc.g, rc.b, locked ? 0.30f : 0.55f), isWeapon: isW);
            NoRaycast(techSock);
            // Icon larger in center, no long name label (matches mockup's icon-focused cards with number in corner).
            // Small number/glyph in corner like the mockup's "2","4","3".
            AddIcon(techSock.transform, iconSprite, icon, ElarionUi.FontTitle + 4,
                    locked ? InkMicro : rcInk, locked ? 0.6f : 1f);

            // Eyes-sweep 2026-07-06: the rarity letter (C/E) sat at y 0.65–0.88 and overflowed
            // LEFT over the icon well (0.26–0.74 x, 0.38–0.95 y). Moved to the free bottom-right
            // corner band (below the icon well) and fitted (§1.14) so it can never spill.
            string numText = lockText != "" ? lockText
                           : (!string.IsNullOrEmpty(rarity) ? rarity.Substring(0,1).ToUpper() : "");
            // No rarity letter / lock text -> no glyph to draw. Building a Label for whitespace creates a
            // permanently 0-glyph "dead" label (a space is never visible), tripping the TextFitGuard on
            // every cell (owner F8 2026-07-10 "dead-button law violated"). Skip the Label entirely.
            if (!string.IsNullOrWhiteSpace(numText))
            {
                var numLbl = AddLabel(cell.transform, numText, 0.04f, 0.30f,
                         Ink, ElarionUi.FontMicro + 2, TMPro.TextAlignmentOptions.Center, 0.70f, 0.98f, bold: true);
                ElarionUiKit.FitSingleLine(numLbl, 0f, ElarionUi.FontMicro + 2);
            }

            NoRaycast(AddImage(cell.transform, "Gem", new Vector2(0.05f, 0.80f), new Vector2(0.20f, 0.95f),
                               new Color(rc.r, rc.g, rc.b, 0.95f)));

            if (equipped)
            {
                // Was (0.62,0.80)-(0.96,0.96) — the chip's left half painted over the icon well's
                // top-right corner. Bottom-LEFT band is free (gem = top-left, letter = bottom-right).
                var chip = AddImage(cell.transform, "Equipped", new Vector2(0.02f, 0.04f), new Vector2(0.30f, 0.28f),
                                    new Color(ElarionUi.Affordable.r, ElarionUi.Affordable.g, ElarionUi.Affordable.b, 0.95f));
                NoRaycast(chip);
                var chipLbl = AddLabel(chip.transform, "v", 0f, 1f, ElarionUi.Ink, ElarionUi.FontLabel,
                         TMPro.TextAlignmentOptions.Center, 0f, 1f, bold: true);
                ElarionUiKit.FitSingleLine(chipLbl, 0f, ElarionUi.FontLabel);
            }
            if (locked)
            {
                NoRaycast(AddImage(cell.transform, "Veil", Vector2.zero, Vector2.one,
                                   new Color(0.965f, 0.945f, 0.890f, 0.45f)));
                var chip = AddImage(cell.transform, "Locked", new Vector2(0.26f, 0.40f), new Vector2(0.74f, 0.62f),
                                    new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.90f));
                NoRaycast(chip);
                var lockLbl = AddLabel(chip.transform, "[ " + lockText + " ]", 0f, 1f, Ink,
                         ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Center, 0f, 1f, bold: true);
                ElarionUiKit.FitSingleLine(lockLbl, 0f, ElarionUi.FontMicro);
            }
        }

        private static void NoRaycast(GameObject go)
        {
            var img = go.GetComponent<Image>();
            if (img != null) img.raycastTarget = false;
        }

        private void BuildEmptyNote(Transform content, string msg)
        {
            var note = new GameObject("Empty", typeof(RectTransform));
            note.transform.SetParent(content, false);
            var le = note.AddComponent<LayoutElement>();
            le.preferredWidth = 600f; le.preferredHeight = 120f;
            AddLabel(note.transform, msg, 0f, 1f, InkDim, ElarionUi.FontLabel,
                     TMPro.TextAlignmentOptions.Center, 0f, 1f);
        }

    }
}