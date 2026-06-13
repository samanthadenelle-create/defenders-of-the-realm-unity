// =============================================================================
// EquipmentPanel — code-built UI for equipping items (WO-109 foundation).
// -----------------------------------------------------------------------------
// Opened via Yarn command "OpenEquip" from NPC dialogue.
// Lists simple inventory (from VillageInventory/ItemInventory), allows equip
// to HeroEquipment (visual + stats).
// Code-built Canvas (no UXML, per project rules for builds).
//
// TECH-PACK STYLING PASS: this screen now routes its entire shell, slot sockets,
// header, stat rows and buttons through the SHARED presentation kit
// (DeNelle.Core.UI.ElarionUiKit + ElarionUi palette + the RpgUiCatalog "Tech hud
// elements" ornate sprite pack) so it reads as the SAME designed game as the town
// HUD and the inventory paperdoll — an ornate framed dark-glass modal with a
// gold-rune border, pack slot-sockets behind the weapon/armor slots (rarity
// tinted), a gilt crest header + gold stat labels, and sprite-backed equip /
// unequip buttons. Sprite-FIRST everywhere with the kit's procedural fallback, so
// the panel is correct whether or not the pack art is on disk (WebGL-safe).
//
// Equip/unequip + stat logic and the public Open()/Close() surface are UNCHANGED;
// this was a styling-only pass. The panel moved off the old tiny WorldSpace canvas
// onto the kit's ScreenSpaceOverlay modal (the proven build-reliable path the
// sibling inventory / shop panels use).
// =============================================================================
using UnityEngine;
using UnityEngine.UI;
using DeNelle.Core.UI;
using DeNelle.Village.Crafting;

namespace DeNelle.Village.Hero
{
    public sealed class EquipmentPanel : MonoBehaviour
    {
        private GameObject _ui;
        private HeroEquipment _equip;

        // Live labels so equip/unequip can refresh the panel in place.
        private TMPro.TextMeshProUGUI _mainHandLabel;
        private TMPro.TextMeshProUGUI _armorLabel;
        private TMPro.TextMeshProUGUI _statsLabel;
        private GameObject _mainHandCell;
        private GameObject _armorCell;

        public void Open()
        {
            if (_ui != null) return;
            _equip = FindObjectOfType<HeroEquipment>();
            if (_equip == null)
            {
                var hero = GameObject.FindWithTag("Player");
                if (hero) _equip = hero.AddComponent<HeroEquipment>();
            }

            // Kit modal: ScreenSpaceOverlay canvas + scrim + ornate framed panel
            // (same boilerplate the inventory / shop modals use → identical depth).
            _ui = ElarionUiKit.BuildModalCanvas("EquipmentPanel", sortingOrder: 2500);
            _ui.transform.SetParent(transform, false);
            ElarionUiKit.Scrim(_ui.transform, Close);

            // Centred ornate dark-glass plate with the pack's panel frame + gold rim.
            var panel = ElarionUiKit.PanelFramed(_ui.transform,
                                                 new Vector2(0.10f, 0.20f), new Vector2(0.90f, 0.80f),
                                                 deep: true, packSpriteName: RpgUiCatalog.PanelInventory);

            // Gilt crest header + gold rule, matching the town HUD / inventory.
            ElarionUiKit.Header(panel.transform, "EQUIPMENT");

            // ── Weapon + armor slot sockets (rarity-framed, pack icon inside) ─────
            // Demo items are common-tier; the Slot frame strength reads the tier and
            // the RPG-pack sword/shield icon drops into each socket cell. A caption
            // beneath each slot names what is equipped.
            _mainHandCell = BuildEquipSlot(panel.transform, "WEAPON",
                                           new Vector2(0.10f, 0.44f), new Vector2(0.46f, 0.78f),
                                           RpgUiCatalog.IconSword, out _mainHandLabel);
            _armorCell = BuildEquipSlot(panel.transform, "ARMOR",
                                        new Vector2(0.54f, 0.44f), new Vector2(0.90f, 0.78f),
                                        RpgUiCatalog.IconShield, out _armorLabel);

            // ── Stat readout row (gold labels on a recessed well) ────────────────
            ElarionUiKit.Well(panel.transform, new Vector2(0.10f, 0.30f), new Vector2(0.90f, 0.42f));
            ElarionUiKit.Label(panel.transform, "BONUSES", 0.30f, 0.42f, ElarionUi.Gilt,
                               ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Center,
                               0.10f, 0.90f, spacing: 2f, bold: true);
            _statsLabel = ElarionUiKit.Label(panel.transform, "", 0.30f, 0.40f, ElarionUi.Parchment,
                                             ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Center,
                                             0.12f, 0.88f);

            // ── Equip / unequip buttons (sprite-backed pack frames) ──────────────
            ElarionUiKit.ButtonPack(panel.transform, "Equip Sword", ElarionUiKit.ButtonKind.Gold,
                                    new Vector2(0.10f, 0.18f), new Vector2(0.46f, 0.27f),
                                    () => TryEquip("basic_sword"));
            ElarionUiKit.ButtonPack(panel.transform, "Equip Armor", ElarionUiKit.ButtonKind.Gold,
                                    new Vector2(0.54f, 0.18f), new Vector2(0.90f, 0.27f),
                                    () => TryEquip("leather_armor"));
            ElarionUiKit.Button(panel.transform, "Unequip Weapon", ElarionUiKit.ButtonKind.Danger,
                                new Vector2(0.10f, 0.07f), new Vector2(0.46f, 0.16f),
                                () => DoUnequip(EquipmentSlot.MainHand));
            ElarionUiKit.Button(panel.transform, "Unequip Armor", ElarionUiKit.ButtonKind.Danger,
                                new Vector2(0.54f, 0.07f), new Vector2(0.90f, 0.16f),
                                () => DoUnequip(EquipmentSlot.Armor));
            ElarionUiKit.Button(panel.transform, "Close", ElarionUiKit.ButtonKind.Quiet,
                                new Vector2(0.34f, 0.005f), new Vector2(0.66f, 0.06f),
                                Close);

            Refresh();
            Debug.Log("[EquipmentPanel] Opened (Yarn command flow). Equip to see visual/stat change on hero.");
        }

        // Build one rarity-framed equipment slot: a kit Slot socket carrying the
        // pack's weapon/armor icon, with a gilt caption above and an equipped-name
        // label beneath. Returns the slot CELL (the icon parent) so Refresh() can
        // toggle the equipped-check. The name label is returned via out.
        private GameObject BuildEquipSlot(Transform parent, string caption,
                                          Vector2 anchorMin, Vector2 anchorMax,
                                          string packIconName, out TMPro.TextMeshProUGUI nameLabel)
        {
            // Caption above the socket.
            ElarionUiKit.Label(parent, caption,
                               anchorMax.y - 0.02f, anchorMax.y + 0.02f, ElarionUi.Gilt,
                               ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Center,
                               anchorMin.x, anchorMax.x, spacing: 2f, bold: true);

            // The rarity-framed socket itself (square-ish region inside the band).
            var cell = ElarionUiKit.Slot(parent, (int)ElarionUiKit.Rarity.Common,
                                         new Vector2(anchorMin.x, anchorMin.y + 0.10f),
                                         new Vector2(anchorMax.x, anchorMax.y - 0.06f));

            // Pack icon (sword / shield) dropped into the socket, sprite-FIRST.
            var icon = RpgUiCatalog.Get(RpgUiCatalog.RoleIcons, packIconName);
            var iconGo = ElarionUiKit.AddImage(cell.transform, "SlotIcon",
                                               new Vector2(0.18f, 0.18f), new Vector2(0.82f, 0.82f),
                                               Color.white, rounded: false);
            var iconImg = iconGo.GetComponent<Image>();
            iconImg.raycastTarget = false;
            if (icon != null)
            {
                iconImg.sprite = icon;
                iconImg.type = Image.Type.Simple;
                iconImg.preserveAspect = true;
            }
            else
            {
                // Pack absent — keep the affordance with a glyph fallback.
                iconImg.color = new Color(0f, 0f, 0f, 0f);
                ElarionUiKit.Label(iconGo.transform, packIconName == RpgUiCatalog.IconSword ? "/" : "[]",
                                   0f, 1f, ElarionUi.Parchment, ElarionUi.FontTitle,
                                   TMPro.TextAlignmentOptions.Center, 0f, 1f, bold: true);
            }

            // Equipped-name caption beneath the socket.
            nameLabel = ElarionUiKit.Label(parent, "None",
                                           anchorMin.y, anchorMin.y + 0.09f, ElarionUi.Parchment,
                                           ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Center,
                                           anchorMin.x, anchorMax.x);
            return cell;
        }

        // Repaint the panel from the live HeroEquipment state (slot names, equipped
        // checks, bonus totals). Called on open + after every equip/unequip.
        private void Refresh()
        {
            var weapon = _equip != null ? _equip.GetEquipped(EquipmentSlot.MainHand) : null;
            var armor  = _equip != null ? _equip.GetEquipped(EquipmentSlot.Armor) : null;

            if (_mainHandLabel != null) _mainHandLabel.text = weapon != null ? weapon.ItemId : "None";
            if (_armorLabel != null)    _armorLabel.text    = armor  != null ? armor.ItemId  : "None";

            ElarionUiKit.SetSlotEquipped(_mainHandCell, weapon != null);
            ElarionUiKit.SetSlotEquipped(_armorCell, armor != null);

            float reach = (weapon != null ? weapon.ReachBonus : 0f) + (armor != null ? armor.ReachBonus : 0f);
            float dmg   = (weapon != null ? weapon.DamageBonus : 0f) + (armor != null ? armor.DamageBonus : 0f);
            if (_statsLabel != null)
                _statsLabel.text = $"Reach +{reach:0.#}    Damage +{dmg:0.#}";
        }

        private void TryEquip(string id)
        {
            if (_equip != null && _equip.Equip(id))
            {
                Debug.Log($"[EquipmentPanel] Equipped {id} — check hero for visual (sword/ armor) and stat (reach/damage) change.");
                Refresh();
            }
        }

        private void DoUnequip(EquipmentSlot slot)
        {
            if (_equip == null) return;
            _equip.Unequip(slot);
            Refresh();
        }

        private void Close()
        {
            if (_ui != null) Destroy(_ui);
            _ui = null;
            _mainHandLabel = null;
            _armorLabel = null;
            _statsLabel = null;
            _mainHandCell = null;
            _armorCell = null;
        }
    }
}
