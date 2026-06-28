// =============================================================================
// HeroLoadoutPanelMvvm — the loadout-chooser VIEW (MVVM slice). A DUMB SKIN: it
// builds presentation (ElarionUiKit dark-glass + gold frame) and BINDS a
// HeroLoadoutVM. ALL state/logic (slot map, unlocked-skill grid, equip routing)
// lives in the VM — the View never reads game state.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Talents
//
// Code-built uGUI ONLY (no UXML — §8). This fills the HOT-SWAP bar (the player-
// assignable bottom-middle battle row); the bottom-RIGHT bar is the FIXED class kit
// and is not edited here (owner-correct, 2026-06-28). Layout:
//   * TOP ROW — the hot-swap slot tiles (1..N). Each shows the assigned skill name,
//     or an empty "+" placeholder.
//   * BOTTOM — a grid of unlocked skills. TAP a skill (it highlights), then tap a
//     hot-swap slot to assign it (tap-tap, WebGL-safe — no drag, per the never-drag
//     rule). Tap a filled slot with nothing picked to clear it.
//   * A status line echoes the VM's hint / result.
//
// Registers PanelId.HeroLoadout (opened from the skill-tree panel's Equip button).
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DeNelle.Core.UI;
using DeNelle.Core.UI.Mvvm;

namespace DeNelle.Village.Talents
{
    [DisallowMultipleComponent]
    public sealed class HeroLoadoutPanelMvvm : MonoBehaviour, IPanelView
    {
        private HeroLoadoutVM _vm;

        private GameObject _ui;
        private GameObject _slotsRoot;
        private GameObject _gridRoot;
        private TMPro.TextMeshProUGUI _headerLabel;
        private TMPro.TextMeshProUGUI _statusText;

        private PanelHandle _panelHandle;

        public bool IsOpen => _ui != null;

        // ── Registration ──────────────────────────────────────────────────────────

        private void Awake()
        {
            _panelHandle = PanelManager.Register("Equip Skills", Close, () => IsOpen);
            PanelRouter.Register(PanelId.HeroLoadout, Open);
        }

        private void OnDestroy()
        {
            Unbind();
            if (_vm != null) { _vm.Dispose(); _vm = null; }
            if (_ui != null) Destroy(_ui);
            _ui = null;
            PanelRouter.Unregister(PanelId.HeroLoadout, Open);
        }

        // ── Open ────────────────────────────────────────────────────────────────

        public void Open()
        {
            Close();
            BuildChrome();

            _vm = new HeroLoadoutVM(Close);
            Bind(_vm);

            if (!PanelManager.NotifyOpened(_panelHandle))
                return;

            Debug.Log("[HeroLoadoutPanelMvvm] Opened. Bound HeroLoadoutVM (MVVM).");
        }

        // ── IPanelView ────────────────────────────────────────────────────────────

        public void Bind(IPanelViewModel vm)
        {
            Unbind();
            _vm = vm as HeroLoadoutVM;
            if (_vm == null) return;
            _vm.Changed += Render;
            Render();
        }

        public void Unbind()
        {
            if (_vm != null) _vm.Changed -= Render;
        }

        // ── Render ────────────────────────────────────────────────────────────────

        private void Render()
        {
            if (_vm == null) return;
            if (_headerLabel != null) _headerLabel.text = _vm.Title;
            if (_statusText != null) _statusText.text = _vm.Status;
            RebuildSlots();
            RebuildGrid();
        }

        private void RebuildSlots()
        {
            ClearChildren(_slotsRoot);
            if (_slotsRoot == null) return;

            var slots = _vm.Slots;
            int n = slots != null ? slots.Count : 0;
            if (n <= 0) return;

            float gap = 0.02f;
            float w = (1f - gap * (n - 1)) / n;
            for (int i = 0; i < n; i++)
            {
                float x0 = i * (w + gap);
                BuildSlotTile(_slotsRoot.transform, slots[i], x0, x0 + w);
            }
        }

        private void BuildSlotTile(Transform parent, LoadoutSlotVM slot, float x0, float x1)
        {
            var tile = new GameObject("Slot_" + slot.SlotKey, typeof(Image), typeof(Button));
            tile.transform.SetParent(parent, false);
            var rt = tile.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(x0, 0.06f); rt.anchorMax = new Vector2(x1, 0.94f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            var img = tile.GetComponent<Image>();
            ElarionUiKit.ApplyRounded(img);
            // Discoverability (owner: "not intuitive how to assign — could only get the first slot"):
            // once a skill is picked, the tappable W/E/R slots glow gold so it's obvious THIS is the
            // next tap target (the tap-skill-then-tap-slot flow). Q (locked) never glows.
            bool aSkillIsPicked = _vm != null && !string.IsNullOrEmpty(_vm.SelectedAbilityId);
            bool isAssignTarget = aSkillIsPicked;
            // Empty slots read as a quiet socket; filled read gold-warm; when a skill is picked
            // every slot glows gold (the tap-skill-then-tap-slot flow). Tap a filled slot with
            // nothing picked to clear it.
            Color fill;
            if (isAssignTarget) fill = new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.42f);
            else if (slot.IsEmpty) fill = ElarionUiKit.Track;
            else fill = new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.20f);
            img.color = fill;

            var btn = tile.GetComponent<Button>();
            btn.targetGraphic = img;
            ElarionUiKit.StyleButtonColors(btn);
            int slotIndex = slot.SlotIndex;
            btn.onClick.AddListener(() => { if (_vm != null) _vm.OnSlotTapped(slotIndex); });

            // Slot number — big, top.
            ElarionUiKit.Label(tile.transform, slot.SlotKey, 0.62f, 0.95f, ElarionUi.Gilt,
                ElarionUi.FontHead, TMPro.TextAlignmentOptions.Center, 0.05f, 0.95f, bold: true);

            // Content line.
            string body;
            Color bodyColor;
            if (slot.IsEmpty)
            {
                body = isAssignTarget ? "tap to assign" : "+";
                bodyColor = isAssignTarget ? ElarionUi.Gilt : ElarionUi.ParchmentDim;
            }
            else
            {
                body = isAssignTarget ? slot.AbilityName : slot.AbilityName + "\n(tap to clear)";
                bodyColor = ElarionUi.Parchment;
            }
            ElarionUiKit.Label(tile.transform, body, 0.08f, 0.58f, bodyColor,
                slot.IsEmpty ? ElarionUi.FontTitle : ElarionUi.FontMicro,
                TMPro.TextAlignmentOptions.Center, 0.05f, 0.95f, bold: !slot.IsEmpty);
        }

        private void RebuildGrid()
        {
            ClearChildren(_gridRoot);
            if (_gridRoot == null) return;

            var choices = _vm.UnlockedSkills;
            int n = choices != null ? choices.Count : 0;
            if (n <= 0)
            {
                ElarionUiKit.Label(_gridRoot.transform, "No unlocked skills yet — unlock SKILL nodes in the tree.",
                    0.45f, 0.55f, ElarionUi.ParchmentDim, ElarionUi.FontLabel,
                    TMPro.TextAlignmentOptions.Center, 0.05f, 0.95f);
                return;
            }

            const int cols = 3;
            float gapX = 0.02f, gapY = 0.03f;
            float cardW = (1f - gapX * (cols - 1)) / cols;
            float cardH = 0.20f;
            for (int i = 0; i < n; i++)
            {
                int row = i / cols;
                int col = i % cols;
                float x0 = col * (cardW + gapX);
                float y1 = 0.98f - row * (cardH + gapY);
                float y0 = y1 - cardH;
                if (y0 < 0.02f) break; // overflow guard (no scroll on the chooser grid)
                BuildChoiceCard(_gridRoot.transform, choices[i], x0, x0 + cardW, y0, y1);
            }
        }

        private void BuildChoiceCard(Transform parent, SkillChoiceVM choice, float x0, float x1, float y0, float y1)
        {
            var card = new GameObject("Choice_" + choice.AbilityId, typeof(Image), typeof(Button));
            card.transform.SetParent(parent, false);
            var rt = card.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(x0, y0); rt.anchorMax = new Vector2(x1, y1);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            var img = card.GetComponent<Image>();
            ElarionUiKit.ApplyRounded(img);

            bool selected = !string.IsNullOrEmpty(_vm.SelectedAbilityId) &&
                            string.Equals(_vm.SelectedAbilityId, choice.AbilityId, System.StringComparison.OrdinalIgnoreCase);
            Color fill;
            if (selected) fill = ElarionUiKit.CellSelected;
            else if (choice.IsEquipped) fill = new Color(ElarionUi.Affordable.r, ElarionUi.Affordable.g, ElarionUi.Affordable.b, 0.18f);
            else fill = ElarionUiKit.Cell;
            img.color = fill;

            var btn = card.GetComponent<Button>();
            btn.targetGraphic = img;
            ElarionUiKit.StyleButtonColors(btn);
            string id = choice.AbilityId;
            btn.onClick.AddListener(() => { if (_vm != null) _vm.SelectSkill(id); });

            ElarionUiKit.Label(card.transform, choice.Name, 0.30f, 0.92f, ElarionUi.Parchment,
                ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Center, 0.06f, 0.94f, bold: true);

            string chip = choice.IsEquipped ? "EQUIPPED" : (selected ? "SELECTED" : "tap to pick");
            Color chipColor = choice.IsEquipped ? ElarionUi.Affordable : (selected ? ElarionUi.Gilt : ElarionUi.ParchmentDim);
            ElarionUiKit.Label(card.transform, chip, 0.06f, 0.28f, chipColor,
                ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Center, 0.06f, 0.94f, bold: selected);
        }

        // ── Chrome ────────────────────────────────────────────────────────────────

        private void BuildChrome()
        {
            _ui = ElarionUiKit.BuildModalCanvas("HeroLoadoutPanelMvvmUI", 31050);
            var canvas = _ui.GetComponent<Canvas>();
            if (canvas != null) canvas.overrideSorting = true;
            ElarionUiKit.Scrim(_ui.transform, onTapClose: () => { if (_vm != null) _vm.Close(); });

            var backdrop = ElarionUiKit.AddImage(_ui.transform, "LoadoutBackdrop",
                Vector2.zero, Vector2.one, new Color(0.02f, 0.015f, 0.012f, 0.94f), rounded: false);
            var bdImg = backdrop.GetComponent<Image>();
            if (bdImg != null) bdImg.raycastTarget = false;

            var panelGo = ElarionUiKit.PanelFramed(_ui.transform, new Vector2(0.10f, 0.10f), new Vector2(0.90f, 0.90f),
                                                   deep: true, packSpriteName: RpgUiCatalog.PanelWindowDark);
            var panel = panelGo.transform;

            Color fillColor = new Color(0.07f, 0.055f, 0.042f, 0.985f);
            if (DeNelle.Core.FeatureFlags.BlinkChrome) fillColor.a = 0f;
            var solidFill = ElarionUiKit.AddImage(panel, "LoadoutSolidFill",
                new Vector2(0.025f, 0.02f), new Vector2(0.975f, 0.98f), fillColor);
            var sfImg = solidFill.GetComponent<Image>();
            if (sfImg != null) sfImg.raycastTarget = false;
            solidFill.transform.SetAsFirstSibling();

            _headerLabel = ElarionUiKit.Header(panel, "Hot-Swap Skills", x0: 0.04f, x1: 0.96f, y0: 0.90f, y1: 0.97f);

            // Caption: the class kit is fixed; this bar is for extra talent skills.
            ElarionUiKit.Label(panel, "Your class kit is fixed — assign extra talent skills to your hot-swap bar.",
                0.865f, 0.90f, ElarionUi.ParchmentDim, ElarionUi.FontMicro,
                TMPro.TextAlignmentOptions.Center, 0.05f, 0.95f);

            // Hot-swap slot strip under the header.
            _slotsRoot = new GameObject("SlotsRow", typeof(RectTransform));
            _slotsRoot.transform.SetParent(panel, false);
            var sr = _slotsRoot.GetComponent<RectTransform>();
            sr.anchorMin = new Vector2(0.05f, 0.68f); sr.anchorMax = new Vector2(0.95f, 0.84f);
            sr.offsetMin = Vector2.zero; sr.offsetMax = Vector2.zero;

            // Divider caption.
            ElarionUiKit.Label(panel, "Unlocked Skills", 0.61f, 0.66f, ElarionUi.Gilt,
                ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Center, 0.05f, 0.95f, bold: true);

            // Unlocked-skill grid.
            _gridRoot = ElarionUiKit.Well(panel, new Vector2(0.05f, 0.16f), new Vector2(0.95f, 0.60f));
            var gImg = _gridRoot.GetComponent<Image>();
            if (gImg != null) gImg.raycastTarget = false;

            // Status line.
            var statusGo = new GameObject("Status", typeof(TMPro.TextMeshProUGUI));
            statusGo.transform.SetParent(panel, false);
            var stRect = statusGo.GetComponent<RectTransform>();
            stRect.anchorMin = new Vector2(0.05f, 0.105f); stRect.anchorMax = new Vector2(0.95f, 0.15f);
            stRect.offsetMin = Vector2.zero; stRect.offsetMax = Vector2.zero;
            _statusText = statusGo.GetComponent<TMPro.TextMeshProUGUI>();
            ElarionUiKit.EnsureFont(_statusText);
            _statusText.fontSize = ElarionUi.FontLabel;
            _statusText.color = ElarionUi.ParchmentDim;
            _statusText.alignment = TMPro.TextAlignmentOptions.Center;
            _statusText.raycastTarget = false;

            // Close (also routes back; PanelManager handles single-modal).
            var closeBtn = ElarionUiKit.ButtonPack(panel, "Done", ElarionUiKit.ButtonKind.Gold,
                new Vector2(0.36f, 0.035f), new Vector2(0.64f, 0.095f), () => { if (_vm != null) _vm.Close(); },
                packSpriteName: DeNelle.Core.FeatureFlags.BlinkChrome ? RpgUiCatalog.ButtonConfirm : RpgUiCatalog.ButtonGold);
            var closeLbl = closeBtn != null ? closeBtn.GetComponentInChildren<TMPro.TextMeshProUGUI>() : null;
            if (closeLbl != null)
            {
                closeLbl.color = ElarionUi.Parchment; closeLbl.fontStyle = TMPro.FontStyles.Bold;
                closeLbl.outlineColor = new Color32(20, 12, 4, 235); closeLbl.outlineWidth = 0.22f;
                closeLbl.transform.SetAsLastSibling();
            }
        }

        // ── Teardown ──────────────────────────────────────────────────────────────

        private void ClearChildren(GameObject host)
        {
            if (host == null) return;
            for (int i = host.transform.childCount - 1; i >= 0; i--)
            {
                var c = host.transform.GetChild(i);
                if (c != null) Destroy(c.gameObject);
            }
        }

        private void Close()
        {
            Unbind();
            if (_vm != null) { _vm.Dispose(); _vm = null; }
            _headerLabel = null;
            _statusText = null;
            if (_ui != null) Destroy(_ui);
            _ui = null;
            _slotsRoot = null;
            _gridRoot = null;
            PanelManager.NotifyClosed(_panelHandle);
        }
    }
}
