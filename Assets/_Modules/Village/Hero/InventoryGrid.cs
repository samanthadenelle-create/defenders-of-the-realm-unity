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
            try { owned = _store != null ? _store.OwnedCounts.Count : -1; } catch { owned = -2; }
            int slotCount = (_vm != null && _vm.Slots != null) ? _vm.Slots.Count : -1;
            // The inventory model is resolved by InventoryVM.CreateDefault now; this View reads the
            // store presence it holds, never VillageInventory.Instance (strict-MVVM: no state read here).
            FlowTrace.Step("Inventory",
                $"RebuildGrid tab={_vm?.ActiveTab} store={(_store == null ? "NULL" : "ok")} " +
                $"inventorySource={(_store == null ? "NULL" : "store-ok")} ownedCounts={owned} slots={slotCount}");

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
                                  selected: selected, lockText: "", level: it.Level,
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

        // WO-713 A.4 — an empty slot is now the kit's dim plate (WO-714 P4 sparse-grid law):
        // same BuildRaritySlot construction as a live cell, empty:true (rim/icon hidden, tap
        // disabled). Sized by the parent GridLayoutGroup exactly like a real cell.
        private void BuildEmptySlot(Transform content)
        {
            var slot = ElarionUiKit.BuildRaritySlot(content, 0, Vector2.zero, Vector2.one, empty: true);
            if (slot != null && slot.root != null) slot.root.name = "EmptySlot";
        }

        // Pick the cell icon from the VM's role/name KEYS — presentation mapping (a key -> art),
        // not a state pull. Mirrors ShopPanel.ResolveIcon: real item art first, pack icon fallback.
        private static Sprite ResolveItemIcon(string role, string id)
        {
            switch (role)
            {
                case InventoryVM.IconRoleWeapon:
                {
                    // Icon resolves through the presentation seam (GearIconCatalog does the
                    // GearCatalog.Find*+ItemIconCatalog.For* pair internally), so this View
                    // never names GearCatalog. Pack-icon fallback kept.
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
                    // F8-641: a MATERIAL resolves art from its OWN row only (authored iconPath,
                    // then a mat_* sheet by its authored category) and never from the potion
                    // fallbacks. Null here means the cell shows this row's authored glyph.
                    var row = ItemIdentity.Resolve(id);
                    return ItemIconCatalog.ForMaterial(id, row.IconPath, row.Category);
                }
            }
            return null;
        }

        // The row's own authored Resources sprite (catalog iconPath), or null.
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
                // Weapon/armor glyph fallback resolves through the seam too (GearIconCatalog.Glyph
                // does the GearCatalog.Find* + type-glyph internally) so the View drops GearCatalog.
                case InventoryVM.IconRoleWeapon: return GearIconCatalog.Glyph(InventoryVM.IconRoleWeapon, id);
                case InventoryVM.IconRoleArmor:  return GearIconCatalog.Glyph(InventoryVM.IconRoleArmor, id);
                case InventoryVM.IconRolePotion:
                case InventoryVM.IconRoleMaterial:
                {
                    // The row's AUTHORED glyph is the truth (materials.json / consumables.json
                    // both carry one); the keyword derivation is only the last resort for an id
                    // no catalog owns.
                    var row = ItemIdentity.Resolve(id);
                    if (!string.IsNullOrEmpty(row.Glyph)) return row.Glyph;
                    return ConsumableTypeGlyph(id, row.DisplayName);
                }
            }
            return "?";
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

        // WO-713 A.4 — one live cell = the kit rarity slot (WO-714 P4 BuildRaritySlot):
        // sprite-first Inventory_Slot plate + the rarity_1..5 rim (ornate PER-TIER art —
        // shape carries the tier, colorblind-safe; the letter chip reinforces it). All the
        // old hand-rolled frame/glow/Tech-socket construction dies (kit over hand-rolled).
        // The parent GridLayoutGroup drives the cell rect, so full-rect anchors are fine.
        private void BuildGearCell(Transform content, string icon, Sprite iconSprite, string name, string rarity,
                                   bool equipped, bool locked, bool selected, string lockText, System.Action onTap,
                                   int level = 1)
        {
            var slot = ElarionUiKit.BuildRaritySlot(content, RarityIndex(rarity),
                Vector2.zero, Vector2.one, empty: false, onTap: onTap);
            if (slot == null || slot.root == null) return;
            slot.root.name = "Cell_" + (string.IsNullOrEmpty(name) ? "item" : name);

            // Icon: real item art first; the BMP type-glyph stays the no-art fallback.
            if (iconSprite != null)
            {
                slot.SetIcon(iconSprite);
                if (slot.icon != null && locked)
                    slot.icon.color = new Color(1f, 1f, 1f, 0.6f);
            }
            else
            {
                var glyphLbl = AddLabel(slot.root.transform, string.IsNullOrEmpty(icon) ? "?" : icon,
                    0.18f, 0.82f, RarityInk(rarity), ElarionUi.FontTitle + 4,
                    TMPro.TextAlignmentOptions.Center, 0.10f, 0.90f, bold: true);
                glyphLbl.raycastTarget = false;
            }

            // SELECTED = gold inner rim + lit plate (shape + brightness — never color-only;
            // the detail strip also names the selection).
            if (selected)
            {
                AddInnerRim(slot.root, new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.95f));
                if (slot.plate != null)
                    slot.plate.color = new Color(1.15f, 1.10f, 0.92f, slot.plate.color.a);
            }

            // Rarity letter / lock text (bottom-right) — text reinforcement of the rim art.
            // (Skip whitespace: a 0-glyph label trips the TextFitGuard — owner F8 2026-07-10.)
            string numText = !string.IsNullOrEmpty(lockText) ? lockText
                           : (!string.IsNullOrEmpty(rarity) ? rarity.Substring(0, 1).ToUpper() : "");
            if (!string.IsNullOrWhiteSpace(numText))
            {
                var numLbl = AddLabel(slot.root.transform, numText, 0.04f, 0.30f,
                         Ink, ElarionUi.FontMicro + 2, TMPro.TextAlignmentOptions.Center, 0.70f, 0.98f, bold: true);
                numLbl.raycastTarget = false;
                ElarionUiKit.FitSingleLine(numLbl, 0f, ElarionUi.FontMicro + 2);
            }

            if (equipped)
            {
                // "EQ" chip bottom-left — TEXT carries the state (colorblind law), the green
                // tint is reinforcement only.
                var chip = AddImage(slot.root.transform, "Equipped",
                                    new Vector2(0.02f, 0.04f), new Vector2(0.30f, 0.28f),
                                    new Color(ElarionUi.Affordable.r, ElarionUi.Affordable.g, ElarionUi.Affordable.b, 0.95f));
                NoRaycast(chip);
                var chipLbl = AddLabel(chip.transform, "EQ", 0f, 1f, ElarionUi.Ink, ElarionUi.FontMicro,
                         TMPro.TextAlignmentOptions.Center, 0f, 1f, bold: true);
                chipLbl.raycastTarget = false;
                ElarionUiKit.FitSingleLine(chipLbl, 0f, ElarionUi.FontMicro);
            }

            // WO-808: gear power level chip, TOP-RIGHT (the only free band — bottom edge is
            // consumed by the rarity letter + EQ chip; inset x>=0.60 keeps it on the rim's
            // flat top edge, clear of the ornate corner). Text carries the state; NOT a
            // Button (a badge must never inflate to the 112px touch floor). Level 1 = no chip.
            if (level > 1)
            {
                var lvChip = AddImage(slot.root.transform, "GearLevel",
                                      new Vector2(0.60f, 0.72f), new Vector2(0.98f, 0.96f),
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

        // Canonical 0..4 rarity ladder for the kit's rarity_1..5 rim art.
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