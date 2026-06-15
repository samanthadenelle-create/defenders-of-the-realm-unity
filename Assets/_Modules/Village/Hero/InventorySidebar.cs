// =============================================================================
// InventorySidebar — sidebar/detail rendering (split from HeroInventoryController).
// -----------------------------------------------------------------------------
// Exact extraction. RebuildSidebar, Build*Sidebar, StatRow, DetailFlavour, header,
// Equip CTA (Tech for W/A). No behavior change. Matches dark-wood + gold.
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
        private void RebuildSidebar()
        {
            if (_sidebarRoot == null) return;
            for (int i = _sidebarRoot.transform.childCount - 1; i >= 0; i--)
                Destroy(_sidebarRoot.transform.GetChild(i).gameObject);

            if (_selWeapon == null && _selArmor == null && _selConsumable == null)
            {
                AddLabel(_sidebarRoot.transform, "Tap an item to view + equip.", 0.40f, 0.60f,
                         InkDim, ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Center, 0.06f, 0.94f);
                return;
            }

            if (_selWeapon != null) BuildWeaponSidebar(_selWeapon);
            else if (_selArmor != null) BuildArmorSidebar(_selArmor);
            else if (_selConsumable != null) BuildConsumableSidebar(_selConsumable);
        }

        private void BuildDetailHeader(string icon, Sprite iconSprite, string name, string rarity, string subline)
        {
            Color rc    = RarityColor(rarity);
            Color rcInk = RarityInk(rarity);

            var techMed = ElarionUiKit.TechGearSocket(_sidebarRoot.transform, "TechDetailSocket",
                new Vector2(0.060f, 0.40f), new Vector2(0.190f, 0.92f), new Color(rc.r, rc.g, rc.b, 0.14f),
                isWeapon: _selWeapon != null);
            NoRaycast(techMed);
            AddIcon(techMed.transform, iconSprite, string.IsNullOrEmpty(icon) ? "?" : icon,
                    ElarionUi.FontHead, rcInk, 1f);

            var band = AddImage(_sidebarRoot.transform, "RarityBand",
                                new Vector2(0.015f, 0.22f), new Vector2(0.235f, 0.37f),
                                new Color(rc.r, rc.g, rc.b, 0.22f));
            AddInnerRim(band, new Color(rc.r, rc.g, rc.b, 0.70f));
            AddLabel(band.transform, name, 0f, 1f, rcInk, ElarionUi.FontLabel,
                     TMPro.TextAlignmentOptions.Center, 0.04f, 0.96f, bold: true);
            AddLabel(_sidebarRoot.transform, RarityGlyph(rarity) + " " + subline, 0.045f, 0.19f,
                     InkDim, ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Center, 0.015f, 0.235f, spacing: 1f);
        }

        private void StatRow(int slot, string label, string value, float delta)
        {
            float y1 = 0.88f - slot * 0.245f;
            float y0 = y1 - 0.205f;
            var row = AddImage(_sidebarRoot.transform, "Stat_" + label,
                               new Vector2(SbMidX0, y0), new Vector2(SbMidX1, y1), Track);
            AddLabel(row.transform, label, 0f, 1f, InkDim, ElarionUi.FontLabel,
                     TMPro.TextAlignmentOptions.Left, 0.06f, 0.55f);
            AddLabel(row.transform, value, 0f, 1f, Ink, ElarionUi.FontLabel,
                     TMPro.TextAlignmentOptions.Right, 0.45f, 0.74f, bold: true);
            if (Mathf.Abs(delta) > 0.0001f)
            {
                bool up = delta > 0f;
                AddLabel(row.transform, up ? "^" : "v", 0f, 1f,
                         up ? new Color(0.20f, 0.45f, 0.18f, 1f) : new Color(0.62f, 0.16f, 0.14f, 1f),
                         ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Right, 0.45f, 0.94f, bold: true);
            }
        }

        private void DetailFlavour(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            AddLabel(_sidebarRoot.transform, "\"" + text + "\"", 0.05f, 0.165f,
                     InkDim, ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Center, SbMidX0, SbMidX1);
        }

        private void BuildWeaponSidebar(WeaponDef w)
        {
            int level = HeroLevel();
            bool locked = w.req != null && level < w.req.level;
            bool equipped = _loadout != null && _loadout.EquippedWeapon != null &&
                            string.Equals(_loadout.EquippedWeapon.id, w.id, System.StringComparison.OrdinalIgnoreCase);
            WeaponDef cur = _loadout != null ? _loadout.EquippedWeapon : null;

            BuildDetailHeader(WeaponTypeGlyph(w), ItemIconCatalog.ForWeapon(w), w.name, w.rarity, Cap(w.rarity) + " - " + Cap(w.job));

            int slot = 0;
            float curDmg = cur != null ? cur.damageMult : 0f;
            StatRow(slot++, "Damage", $"x{w.damageMult:0.0#}", equipped ? 0f : w.damageMult - curDmg);
            if (w.reach > 0f)
            {
                float curReach = cur != null ? cur.reach : 0f;
                StatRow(slot++, "Reach", $"{w.reach:0.0} m", equipped ? 0f : w.reach - curReach);
            }
            if (w.req != null && w.req.level > 1)
                StatRow(slot++, "Requires", "Lv " + w.req.level, 0f);

            DetailFlavour(!string.IsNullOrEmpty(w.flavor) ? w.flavor : w.saga);

            var equipBtn = ElarionUiKit.TechPrimaryButton(_sidebarRoot.transform, equipped ? "EQUIPPED" : (locked ? "LOCKED" : "EQUIP"),
                                                            new Vector2(0.72f, 0.25f), new Vector2(0.98f, 0.75f),
                                                            () =>
                                                            {
                                                                if (_loadout != null && !equipped && !locked)
                                                                    _loadout.EquipWeaponById(w.id);
                                                            });
            if (equipped || locked) equipBtn.interactable = false;
        }

        private void BuildArmorSidebar(ArmorDef a)
        {
            int level = HeroLevel();
            bool locked = a.req != null && level < a.req.level;
            bool equipped = _loadout != null && _loadout.EquippedArmor != null &&
                            string.Equals(_loadout.EquippedArmor.id, a.id, System.StringComparison.OrdinalIgnoreCase);
            ArmorDef cur = _loadout != null ? _loadout.EquippedArmor : null;

            BuildDetailHeader(ArmorTypeGlyph(a), ItemIconCatalog.ForArmor(a), a.name, a.rarity, Cap(a.rarity) + " - " + Cap(a.job));

            int slot = 0;
            float curDef = cur != null ? cur.defense : 0f;
            StatRow(slot++, "Defense", $"{a.defense * 100f:0}%", equipped ? 0f : a.defense - curDef);
            if (a.hpBonus > 0f)
            {
                float curHp = cur != null ? cur.hpBonus : 0f;
                StatRow(slot++, "HP Bonus", $"+{a.hpBonus:0}", equipped ? 0f : a.hpBonus - curHp);
            }
            if (a.req != null && a.req.level > 1)
                StatRow(slot++, "Requires", "Lv " + a.req.level, 0f);

            DetailFlavour(!string.IsNullOrEmpty(a.flavor) ? a.flavor : a.saga);

            var equipBtn = ElarionUiKit.TechPrimaryButton(_sidebarRoot.transform, equipped ? "EQUIPPED" : (locked ? "LOCKED" : "EQUIP"),
                                                            new Vector2(0.72f, 0.25f), new Vector2(0.98f, 0.75f),
                                                            () =>
                                                            {
                                                                if (_loadout != null && !locked)
                                                                {
                                                                    if (equipped) _loadout.EquipArmorById(null);
                                                                    else _loadout.EquipArmorById(a.id);
                                                                }
                                                            });
            if (locked) equipBtn.interactable = false;
        }

        private void BuildConsumableSidebar(ConsumableSel c)
        {
            string name = c.def != null && !string.IsNullOrEmpty(c.def.DisplayName) ? c.def.DisplayName : c.id;
            string glyph = ConsumableTypeGlyph(c.id, name);
            BuildDetailHeader(glyph, ConsumableIcon(c.id, name), name, "common", "Owned x" + c.count);

            int slot = 0;
            if (c.def != null)
            {
                StatRow(slot++, Cap(c.def.EffectRaw), c.def.Magnitude.ToString("0"), 0f);
                if (c.def.Duration > 0f)
                    StatRow(slot++, "Duration", $"{c.def.Duration:0}s", 0f);
                StatRow(slot++, "Use", c.def.UsableInFight ? "In combat" : "Rest only", 0f);
            }

            AddLabel(_sidebarRoot.transform, "Use from the combat hotbar.", 0.40f, 0.62f,
                     InkDim, ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Center, 0.705f, 0.985f);
        }

        private void BuildEquipButton(bool equipped, bool locked, System.Action equip, System.Action unequip)
        {
            string label;
            Color color;
            ButtonKind kind;
            System.Action action;

            if (locked)
            {
                label = "LOCKED"; color = new Color(ElarionUi.Danger.r, ElarionUi.Danger.g, ElarionUi.Danger.b, 0.55f);
                kind = ButtonKind.Danger; action = null;
            }
            else if (equipped)
            {
                label = "v EQUIPPED";
                color = new Color(ElarionUi.Affordable.r, ElarionUi.Affordable.g, ElarionUi.Affordable.b, 0.55f);
                kind = ButtonKind.Confirm; action = null;
            }
            else
            {
                label = "EQUIP"; color = ElarionUi.GoldButton; kind = ButtonKind.Gold; action = equip;
            }

            if (action != null)
                NoRaycast(AddImage(_sidebarRoot.transform, "EquipGlow", new Vector2(0.700f, 0.18f), new Vector2(0.990f, 0.82f),
                                   new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.20f)));

            var btn = AddButton(_sidebarRoot.transform, label, new Vector2(0.845f, 0.140f),
                                new Vector2(0.24f, 0.76f), color, action, kind);
            DressButtonPack(btn);
            btn.interactable = action != null;
        }

        private void ClearSelection()
        {
            _selWeapon = null; _selArmor = null; _selConsumable = null;
        }

        private sealed class ConsumableSel
        {
            public string id;
            public ConsumableDef def;
            public int count;
        }

    }
}