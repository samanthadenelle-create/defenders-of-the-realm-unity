// =============================================================================
// EquipmentPanel — code-built UI for equipping items (WO-109 foundation).
// -----------------------------------------------------------------------------
// Opened via Yarn command "OpenEquip" from NPC dialogue.
// Lists simple inventory (from VillageInventory/ItemInventory), allows equip
// to HeroEquipment (visual + stats).
// Code-built Canvas (no UXML, per project rules for builds).
// Minimal for end-to-end: shows current + equip buttons for demo items.
// =============================================================================
using UnityEngine;
using UnityEngine.UI;
using DeNelle.Village.Crafting;

namespace DeNelle.Village.Hero
{
    public sealed class EquipmentPanel : MonoBehaviour
    {
        private GameObject _ui;
        private HeroEquipment _equip;

        public void Open()
        {
            if (_ui != null) return;
            _equip = FindObjectOfType<HeroEquipment>();
            if (_equip == null)
            {
                var hero = GameObject.FindWithTag("Player");
                if (hero) _equip = hero.AddComponent<HeroEquipment>();
            }

            _ui = new GameObject("EquipmentPanel");
            _ui.transform.SetParent(transform, false);

            var canvas = _ui.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;

            var panel = new GameObject("Panel", typeof(Image));
            panel.transform.SetParent(_ui.transform, false);
            var rect = panel.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(5f, 4f);
            rect.localPosition = new Vector3(0, 4f, 0);
            panel.GetComponent<Image>().color = new Color(0.1f, 0.08f, 0.06f, 0.95f);

            CreateText(panel.transform, "Equipment", new Vector2(0, 1.5f), 24, Color.white);
            CreateText(panel.transform, "Current: " + (_equip != null && _equip.GetEquipped(EquipmentSlot.MainHand) != null ? _equip.GetEquipped(EquipmentSlot.MainHand).ItemId : "None"), new Vector2(0, 0.8f), 14, Color.yellow);

            // Demo buttons for craftable equippables (assume in inventory after craft).
            CreateButton(panel.transform, "Equip Basic Sword", new Vector2(-1f, -0.5f), () => TryEquip("basic_sword"));
            CreateButton(panel.transform, "Equip Leather Armor", new Vector2(1f, -0.5f), () => TryEquip("leather_armor"));
            CreateButton(panel.transform, "Close", new Vector2(0, -1.5f), Close);

            Debug.Log("[EquipmentPanel] Opened (Yarn command flow). Equip to see visual/stat change on hero.");
        }

        private void CreateText(Transform parent, string txt, Vector2 anchored, int size, Color c)
        {
            var go = new GameObject("Text", typeof(TMPro.TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var tmp = go.GetComponent<TMPro.TextMeshProUGUI>();
            tmp.text = txt;
            tmp.fontSize = size;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.color = c;
            var r = go.GetComponent<RectTransform>();
            r.anchoredPosition = anchored;
            r.sizeDelta = new Vector2(4.5f, 0.5f);
        }

        private void CreateButton(Transform parent, string label, Vector2 anchored, System.Action onClick)
        {
            var btnGO = new GameObject("Btn", typeof(Button), typeof(Image));
            btnGO.transform.SetParent(parent, false);
            var rect = btnGO.GetComponent<RectTransform>();
            rect.anchoredPosition = anchored;
            rect.sizeDelta = new Vector2(2f, 0.6f);
            btnGO.GetComponent<Image>().color = new Color(0.2f, 0.18f, 0.15f);

            var btn = btnGO.GetComponent<Button>();
            btn.onClick.AddListener(() => onClick());

            var txtGO = new GameObject("Label", typeof(TMPro.TextMeshProUGUI));
            txtGO.transform.SetParent(btnGO.transform, false);
            var tmp = txtGO.GetComponent<TMPro.TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 12;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.color = Color.white;
            var tRect = txtGO.GetComponent<RectTransform>();
            tRect.anchorMin = Vector2.zero; tRect.anchorMax = Vector2.one; tRect.sizeDelta = Vector2.zero;
        }

        private void TryEquip(string id)
        {
            if (_equip != null && _equip.Equip(id))
            {
                Debug.Log($"[EquipmentPanel] Equipped {id} — check hero for visual (sword/ armor) and stat (reach/damage) change.");
                Close();
            }
        }

        private void Close()
        {
            if (_ui != null) Destroy(_ui);
            _ui = null;
        }
    }
}
