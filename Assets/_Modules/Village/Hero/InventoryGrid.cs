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
            grid.constraintCount = isLandscape ? 5 : 4;
            grid.cellSize = new Vector2(78f, 72f);
            grid.spacing = new Vector2(6f, 6f);
            grid.padding = new RectOffset(4, 4, 4, 4);
            grid.childAlignment = TextAnchor.UpperCenter;

            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            switch (_tab)
            {
                case Tab.Weapons:     BuildWeaponCells(content.transform); break;
                case Tab.Armor:       BuildArmorCells(content.transform); break;
                case Tab.Outfits:     BuildOutfitCells(content.transform); break;
                case Tab.Consumables: BuildConsumableCells(content.transform); break;
            }
        }

        private void BuildWeaponCells(Transform content)
        {
            string job = HeroJob;
            int level = HeroLevel();
            bool any = false;
            foreach (var w in GearCatalog.AllWeapons())
            {
                if (w == null || !JobEligible(w.job, job)) continue;
                any = true;
                bool equipped = _loadout != null && _loadout.EquippedWeapon != null &&
                                string.Equals(_loadout.EquippedWeapon.id, w.id, System.StringComparison.OrdinalIgnoreCase);
                bool locked = w.req != null && level < w.req.level;
                bool selected = _selWeapon != null &&
                                string.Equals(_selWeapon.id, w.id, System.StringComparison.OrdinalIgnoreCase);
                var def = w;
                Sprite techWeaponIcon = null;
                try { techWeaponIcon = Resources.Load<Sprite>("Tech hud elements/Sprites/Sword icons/Sword icons"); } catch { }
                Sprite iconSp = techWeaponIcon ?? ItemIconCatalog.ForWeapon(w);
                BuildGearCell(content, WeaponTypeGlyph(w), iconSp, w.name, w.rarity, equipped, locked, selected,
                              locked ? "Lv " + w.req.level : "",
                              () => { 
                                  if (_loadout != null && !locked) _loadout.EquipWeaponById(w.id);
                                  _selWeapon = def; _selArmor = null; _selConsumable = null; 
                                  RebuildGrid(); RebuildPaperDoll(); 
                              });
            }
            if (!any) BuildEmptyNote(content, "No weapons for this class.");
        }

        private void BuildArmorCells(Transform content)
        {
            string job = HeroJob;
            int level = HeroLevel();
            bool any = false;
            foreach (var a in GearCatalog.AllArmors())
            {
                if (a == null || !JobEligible(a.job, job)) continue;
                any = true;
                bool equipped = _loadout != null && _loadout.EquippedArmor != null &&
                                string.Equals(_loadout.EquippedArmor.id, a.id, System.StringComparison.OrdinalIgnoreCase);
                bool locked = a.req != null && level < a.req.level;
                bool selected = _selArmor != null &&
                                string.Equals(_selArmor.id, a.id, System.StringComparison.OrdinalIgnoreCase);
                var def = a;
                Sprite techArmorIcon = null;
                try { techArmorIcon = Resources.Load<Sprite>("Tech hud elements/Sprites/Profile tabs/Profiletab 1/Profiletab 1"); } catch { }
                if (techArmorIcon == null)
                    try { techArmorIcon = Resources.Load<Sprite>("Tech hud elements/Sprites/Healing Tabs/H1"); } catch { }
                Sprite iconSp = techArmorIcon ?? ItemIconCatalog.ForArmor(a);
                BuildGearCell(content, ArmorTypeGlyph(a), iconSp, a.name, a.rarity, equipped, locked, selected,
                              locked ? "Lv " + a.req.level : "",
                              () => { 
                                  if (_loadout != null && !locked) _loadout.EquipArmorById(a.id);
                                  _selArmor = def; _selWeapon = null; _selConsumable = null; 
                                  RebuildGrid(); RebuildPaperDoll(); 
                              });
            }
            if (!any) BuildEmptyNote(content, "No armor for this class yet.\n(armor.json may be empty)");
        }

        private void BuildOutfitCells(Transform content)
        {
            BuildEmptyNote(content, "Outfits arrive with the cosmetics pass.\n(no owned skins yet)");
        }

        private void BuildConsumableCells(Transform content)
        {
            var owned = ItemInventory.OwnedConsumables();
            if (owned == null || owned.Count == 0)
            {
                BuildEmptyNote(content, "No consumables.\nCraft potions at the Workshop.");
                return;
            }
            foreach (var kv in owned)
            {
                var def = ConsumableCatalog.Find(kv.Key);
                string name = def != null && !string.IsNullOrEmpty(def.DisplayName) ? def.DisplayName : kv.Key;
                string glyph = ConsumableTypeGlyph(kv.Key, name);
                var sel = new ConsumableSel { id = kv.Key, def = def, count = kv.Value };
                bool selected = _selConsumable != null &&
                                string.Equals(_selConsumable.id, kv.Key, System.StringComparison.OrdinalIgnoreCase);
                BuildGearCell(content, glyph, ConsumableIcon(kv.Key, name), name + "  x" + kv.Value, "common", false, false, selected, "",
                              () => { _selConsumable = sel; _selWeapon = null; _selArmor = null; RebuildGrid(); RebuildPaperDoll(); });
            }
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
            } catch { }
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

            // Use RPG UI kit assets (PanelInventory etc.) for the main cell tile to make the inventory grid look clean and professional.
            // Outer frame keeps Tech rarity tint + sockets for W/A gear feel; inner tile uses kit for consistent clean RPG inventory aesthetic.
            var rpgTile = RpgUiCatalog.Get(RpgUiCatalog.RolePanel, RpgUiCatalog.PanelInventory);
            if (rpgTile != null) {
                img.sprite = rpgTile;
                img.type = Image.Type.Sliced;
                img.color = Color.white;  // kit sprite provides the base; rarity applied via outer frame
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

            // Small number in bottom right corner (e.g. count, level, or "4" style from mockup).
            string numText = lockText != "" ? lockText : (rarity != null ? rarity.Substring(0,1).ToUpper() : " ");
            AddLabel(cell.transform, numText, 0.65f, 0.88f,
                     Ink, ElarionUi.FontMicro + 2, TMPro.TextAlignmentOptions.Center, 0.75f, 0.98f, bold: true);

            NoRaycast(AddImage(cell.transform, "Gem", new Vector2(0.05f, 0.80f), new Vector2(0.20f, 0.95f),
                               new Color(rc.r, rc.g, rc.b, 0.95f)));

            if (equipped)
            {
                var chip = AddImage(cell.transform, "Equipped", new Vector2(0.62f, 0.80f), new Vector2(0.96f, 0.96f),
                                    new Color(ElarionUi.Affordable.r, ElarionUi.Affordable.g, ElarionUi.Affordable.b, 0.95f));
                NoRaycast(chip);
                AddLabel(chip.transform, "v", 0f, 1f, ElarionUi.Ink, ElarionUi.FontLabel,
                         TMPro.TextAlignmentOptions.Center, 0f, 1f, bold: true);
            }
            if (locked)
            {
                NoRaycast(AddImage(cell.transform, "Veil", Vector2.zero, Vector2.one,
                                   new Color(0.965f, 0.945f, 0.890f, 0.45f)));
                var chip = AddImage(cell.transform, "Locked", new Vector2(0.26f, 0.40f), new Vector2(0.74f, 0.62f),
                                    new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.90f));
                NoRaycast(chip);
                AddLabel(chip.transform, "[ " + lockText + " ]", 0f, 1f, Ink,
                         ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Center, 0f, 1f, bold: true);
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