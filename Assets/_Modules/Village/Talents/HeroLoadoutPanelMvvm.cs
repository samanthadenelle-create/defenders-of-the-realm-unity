// =============================================================================
// HeroLoadoutPanelMvvm — the loadout-chooser VIEW (MVVM slice). A DUMB SKIN: it
// builds presentation (ElarionUiKit Obsidian frame + the WO-714 slot grammar) and
// BINDS a HeroLoadoutVM. ALL state/logic (slot map, unlocked-skill grid, equip
// routing) lives in the VM — the View never reads game state.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Talents
//
// Code-built uGUI ONLY (no UXML — §8). This fills the HOT-SWAP bar (the player-
// assignable bottom-middle battle row); the bottom-RIGHT bar is the FIXED class kit
// and is not edited here (owner-correct, 2026-06-28).
//
// WO-714 W5 conformance (pack SOCKETING/slot grammar — kit primitives only):
//   * TOP ROW — the hot-swap sockets as kit RARITY SLOTS (BuildRaritySlot:
//     Inventory_Slot plate + rarity rim; EMPTY = dim plate per the sparse-grid law,
//     kept tappable — they are sockets, not inventory cells).
//   * BOTTOM — the unlocked skills as a kit SPARSE SLOT GRID (BuildSparseSlotGrid:
//     a grid reads as a grid even when sparse). TAP a skill, then tap a socket
//     (tap-tap, WebGL-safe — no drag). State reads as TEXT chips ("ON BAR" /
//     "PICKED" / "TAP TO ASSIGN"), never by color alone (colorblind law).
//   * Transient VM status (assigned / cleared / can't-edit) surfaces as the ONE
//     kit toast (ShowToast, P5) — no parked status label that can go stale.
//   * Open/close ride the shared PanelOpenCloseFx (P8); raw ids route through
//     SpacedDisplayName (P10) so snake_case never reaches the player.
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

        // P5 toast plumbing: the VM's Status is a hint/result line; the View toasts
        // CHANGES only (the initial hint lives in the static caption, not a toast).
        private string _lastStatus;
        private bool _statusSeeded;

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
            _statusSeeded = false;          // fresh VM → seed its opening hint silently
            _lastStatus = null;
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
            SurfaceStatus();
            RebuildSlots();
            RebuildGrid();
        }

        /// <summary>P5: transient VM status changes surface as the ONE kit toast —
        /// never a parked label that can go stale. The FIRST status (the opening
        /// hint) is seeded silently; the caption already teaches the flow.</summary>
        private void SurfaceStatus()
        {
            string st = _vm.Status ?? "";
            if (!_statusSeeded)
            {
                _statusSeeded = true;
                _lastStatus = st;
                return;
            }
            if (string.Equals(st, _lastStatus)) return;
            _lastStatus = st;
            if (string.IsNullOrEmpty(st)) return;
            ElarionUiKit.ShowToast(st, ToneFor(st));
        }

        private static ElarionUiKit.ToastTone ToneFor(string status)
        {
            // Presentation-only read of the result line: success reads Confirm,
            // blocked reads Danger, hints read Info. Meaning still carried by TEXT.
            if (status.StartsWith("Assigned", System.StringComparison.OrdinalIgnoreCase) ||
                status.StartsWith("Added", System.StringComparison.OrdinalIgnoreCase) ||
                status.StartsWith("Slot cleared", System.StringComparison.OrdinalIgnoreCase))
                return ElarionUiKit.ToastTone.Confirm;
            if (status.IndexOf("Can't", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                status.StartsWith("No hero", System.StringComparison.OrdinalIgnoreCase))
                return ElarionUiKit.ToastTone.Danger;
            return ElarionUiKit.ToastTone.Info;
        }

        /// <summary>P10 guard: the VM already prefers the catalog displayName; when a
        /// raw id leaked through (name == id, or snake_case), space it.</summary>
        private static string DisplayName(string name, string abilityId)
        {
            if (string.IsNullOrEmpty(name)) return ElarionUiKit.SpacedDisplayName(abilityId);
            if (name.IndexOf('_') >= 0 || name.IndexOf('-') >= 0 ||
                string.Equals(name, abilityId, System.StringComparison.OrdinalIgnoreCase))
                return ElarionUiKit.SpacedDisplayName(name);
            return name;
        }

        // ── Hot-swap sockets (top row) — kit rarity slots ─────────────────────────

        private void RebuildSlots()
        {
            ClearChildren(_slotsRoot);
            if (_slotsRoot == null || _vm == null) return;

            var slots = _vm.Slots;
            int n = slots != null ? slots.Count : 0;
            if (n <= 0) return;

            bool aSkillIsPicked = !string.IsNullOrEmpty(_vm.SelectedAbilityId);

            float gap = 0.03f;
            float w = (1f - gap * (n - 1)) / n;
            for (int i = 0; i < n; i++)
            {
                float x0 = i * (w + gap);
                BuildSocket(_slotsRoot.transform, slots[i], aSkillIsPicked,
                    new Vector2(x0, 0f), new Vector2(x0 + w, 1f));
            }
        }

        private void BuildSocket(Transform parent, LoadoutSlotVM slot, bool aSkillIsPicked,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            int slotIndex = slot.SlotIndex;

            // THE slot grammar (WO-714 P4): Inventory_Slot plate + rarity rim.
            var h = ElarionUiKit.BuildRaritySlot(parent, 0, anchorMin, anchorMax,
                empty: false,
                onTap: () => { if (_vm != null) _vm.OnSlotTapped(slotIndex); });
            if (h == null || h.root == null) return;
            h.root.name = "Slot_" + slot.SlotKey;

            if (slot.IsEmpty)
            {
                // Sparse-grid law visuals (dim plate, rim off) — but a SOCKET stays
                // tappable: it is the assign target of the tap-tap flow.
                h.SetEmpty(true);
                if (h.button != null) h.button.interactable = true;
                // Discoverability (owner: "not intuitive how to assign"): once a skill
                // is picked, the rim lights and the socket SAYS what to do — text, not
                // a color-only glow (colorblind law).
                if (aSkillIsPicked && h.rim != null) h.rim.enabled = true;
                ElarionUiKit.Label(h.root.transform,
                    aSkillIsPicked ? "TAP TO\nASSIGN" : "+",
                    0.14f, 0.62f,
                    aSkillIsPicked ? ElarionUi.Gilt : ElarionUi.ParchmentDim,
                    aSkillIsPicked ? ElarionUi.FontMicro : ElarionUi.FontTitle,
                    TMPro.TextAlignmentOptions.Center, 0.06f, 0.94f, bold: aSkillIsPicked);
            }
            else
            {
                var nameLabel = ElarionUiKit.Label(h.root.transform,
                    DisplayName(slot.AbilityName, slot.AbilityId),
                    0.26f, 0.70f, ElarionUi.Parchment, ElarionUi.FontMicro,
                    TMPro.TextAlignmentOptions.Center, 0.06f, 0.94f, bold: true);
                ElarionUiKit.FitSingleLine(nameLabel, ElarionUi.FontFloorMobile);
                // Action affordance as TEXT in the count corner (kit-built, gilt).
                if (h.count != null)
                    h.count.text = aSkillIsPicked ? "tap: replace" : "tap: clear";
            }

            // Slot number — top-left, gilt (the socket's key, "1".."4").
            var key = ElarionUiKit.Label(h.root.transform, slot.SlotKey, 0.64f, 0.98f,
                ElarionUi.Gilt, ElarionUi.FontMicro,
                TMPro.TextAlignmentOptions.TopLeft, 0.08f, 0.50f, bold: true);
            key.raycastTarget = false;
        }

        // ── Unlocked skills (bottom) — kit sparse slot grid ───────────────────────

        private void RebuildGrid()
        {
            ClearChildren(_gridRoot);
            if (_gridRoot == null || _vm == null) return;

            var choices = _vm.UnlockedSkills;
            int n = choices != null ? choices.Count : 0;

            // The sparse-grid law: build the FULL grid; unfilled cells render as dim
            // plates so the area reads as a grid even before any skill is unlocked.
            const int cols = 4;
            int rows = Mathf.Clamp(Mathf.CeilToInt(n / (float)cols), 2, 4);
            int visible = Mathf.Min(n, cols * rows);

            if (n <= 0)
            {
                // Sentence sits in the TOP band of the grid so the fixed-pixel CTA band
                // below can never reach it (worst-case clearance 33.3 ref px at
                // 2670x1200; 36.6 at 2340x1080; 61.8 at 1920x1080).
                var empty = ElarionUiKit.Label(_gridRoot.transform,
                    "No skills unlocked yet.",
                    0.72f, 0.95f, ElarionUi.Parchment, ElarionUi.FontLabel,
                    TMPro.TextAlignmentOptions.Center, 0.05f, 0.95f, bold: true);
                empty.raycastTarget = false;

                var openSkills = ElarionUiKit.BuildObsidianButton(_gridRoot.transform,
                    "OPEN " + HudStrings.HeroFaceLabel(HudStrings.KeyHeroSkills, "button"),
                    ElarionUiKit.ObsidianButtonStyle.Style1,
                    ElarionUiKit.ObsidianButtonColor.Yellow,
                    new Vector2(0.30f, 0.22f), new Vector2(0.70f, 0.48f),
                    OpenSkillsFromLoadout);
                // AUTHOR THE BAND AT THE FLOOR (WO-1410 geometry): the 0.22-0.48 anchor
                // fractions of this grid resolve to 66.9-77.2 ref px across the three
                // landscape aspects - UNDER ElarionUiKit.MinTouchPx(112) - so
                // ClampMinTouch grew the band symmetrically at runtime and spilled it
                // into both neighbours. Re-seat as a FIXED-PIXEL band (the sanctioned
                // SeatSharedCloseInside shape): bottom-pinned CtaBottomGapPx above the
                // grid floor, CanonCtaHeight tall, so the resolved height no longer
                // depends on the aspect. ClampMinTouch stays armed and is now a no-op.
                SeatFixedCtaBand(openSkills, 0.30f, 0.70f);
                ElarionUiKit.ClampMinTouch(openSkills);
                return;
            }

            var handles = ElarionUiKit.BuildSparseSlotGrid(_gridRoot.transform, cols, rows,
                visible,
                rarityOf: i => 0,
                onTap: i =>
                {
                    if (_vm == null) return;
                    var list = _vm.UnlockedSkills;
                    if (list != null && i >= 0 && i < list.Count)
                        _vm.SelectSkill(list[i].AbilityId);
                },
                gapFrac: 0.02f);

            for (int i = 0; i < visible && i < handles.Length; i++)
                DressChoice(handles[i], choices[i]);

            // Overflow beyond the grid cap — say so instead of silently dropping.
            if (n > visible)
            {
                var more = ElarionUiKit.Label(_gridRoot.transform,
                    "+" + (n - visible) + " more unlocked", -0.06f, 0.0f,
                    ElarionUi.ParchmentDim, ElarionUi.FontMicro,
                    TMPro.TextAlignmentOptions.Center, 0.05f, 0.95f);
                more.raycastTarget = false;
            }
        }

        /// <summary>Gap in reference px between the grid's floor and the CTA band's bottom edge.</summary>
        private const float CtaBottomGapPx = 20f;

        /// <summary>
        /// Seat a just-built kit button as a FIXED-PIXEL bottom-pinned band inside
        /// <c>_gridRoot</c>: width stays fraction-of-grid (x0..x1), height is stamped at
        /// <see cref="ElarionUiKit.CanonCtaHeight"/> so it is aspect-independent and can never
        /// resolve under <see cref="ElarionUiKit.MinTouchPx"/>. Position/size only - no restyle,
        /// no re-wire. Walks up to the rect that BuildObsidianButton actually anchored (the
        /// prefab path can return a nested Button), so the stamp lands on the anchored root.
        /// </summary>
        private void SeatFixedCtaBand(Button button, float x0, float x1)
        {
            if (button == null || _gridRoot == null) return;
            Transform t = button.transform;
            while (t != null && t.parent != _gridRoot.transform) t = t.parent;
            var rt = (t != null ? t : button.transform) as RectTransform;
            if (rt == null) return;
            rt.anchorMin = new Vector2(x0, 0f);
            rt.anchorMax = new Vector2(x1, 0f);
            rt.pivot = new Vector2(0.5f, 0f);         // seat by the BOTTOM edge, grow up
            rt.anchoredPosition = new Vector2(0f, CtaBottomGapPx);
            rt.sizeDelta = new Vector2(0f, ElarionUiKit.CanonCtaHeight);
        }

        private void DressChoice(ElarionUiKit.RaritySlotHandle h, SkillChoiceVM choice)
        {
            if (h == null || h.root == null) return;
            h.root.name = "Choice_" + choice.AbilityId;

            bool selected = !string.IsNullOrEmpty(_vm.SelectedAbilityId) &&
                            string.Equals(_vm.SelectedAbilityId, choice.AbilityId,
                                System.StringComparison.OrdinalIgnoreCase);

            // Skill name — center of the plate (no icon art for abilities yet).
            var nameLabel = ElarionUiKit.Label(h.root.transform,
                DisplayName(choice.Name, choice.AbilityId),
                0.34f, 0.86f, selected ? ElarionUi.Gilt : ElarionUi.Parchment,
                ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Center,
                0.06f, 0.94f, bold: true);
            ElarionUiKit.FitSingleLine(nameLabel, ElarionUi.FontFloorMobile);

            // State chip — TEXT carries the meaning (colorblind law), color assists.
            string chip = choice.IsEquipped ? "ON BAR" : (selected ? "PICKED" : "");
            if (!string.IsNullOrEmpty(chip))
            {
                var c = ElarionUiKit.Label(h.root.transform, chip, 0.06f, 0.30f,
                    choice.IsEquipped ? ElarionUi.Affordable : ElarionUi.Gilt,
                    ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Center,
                    0.06f, 0.94f, bold: true);
                c.raycastTarget = false;
            }
        }

        // ── Chrome ────────────────────────────────────────────────────────────────

        private void BuildChrome()
        {
            _ui = ElarionUiKit.BuildModalCanvas("HeroLoadoutPanelMvvmUI", 31050);
            var canvas = _ui.GetComponent<Canvas>();
            if (canvas != null) canvas.overrideSorting = true;
            ElarionUiKit.Scrim(_ui.transform, onTapClose: () => { if (_vm != null) _vm.Close(); });

            // INHERIT the common Obsidian kit (owner: one common frame + one common close): the Talent
            // master-frame + its measured drop-zones + the ONE shared kit Close (built by
            // BuildObsidianPanel). No bespoke chrome/frame/close of our own.
            var chrome = ElarionUiKit.BuildObsidianPanel(_ui.transform,
                HudStrings.HeroFaceLabel(HudStrings.KeyHeroLoadout, "chrome"),
                new Vector2(0.10f, 0.08f), new Vector2(0.90f, 0.92f),
                () => { if (_vm != null) _vm.Close(); },
                frameName: RpgUiCatalog.FrameTalent, medallionIcon: "talent");
            _headerLabel = chrome.title;

            // P8 — the shared open ease (scale target = the PANEL rect, never the canvas).
            ElarionUiKit.AttachPanelOpenFx(_ui,
                chrome.root != null ? chrome.root.GetComponent<RectTransform>() : null);

            // Lay out INSIDE the frame's BODY drop-zone (mobile-first: compact + centered).
            // Falls back to the transparent content overlay when no frame art.
            var bodyHost = (chrome.layout != null && chrome.layout.body != null)
                ? chrome.layout.body : (RectTransform)chrome.content.transform;
            Transform panel = bodyHost;

            // Caption: the persistent teach line (transient results ride the P5 toast).
            ElarionUiKit.Label(panel,
                "Your class kit is fixed. Tap a skill, then a socket, to fill your hot-swap bar.",
                0.94f, 0.99f, ElarionUi.ParchmentDim, ElarionUi.FontMicro,
                TMPro.TextAlignmentOptions.Center, 0.08f, 0.92f);

            // Hot-swap socket strip — centered + compact (mobile thumb-zone).
            _slotsRoot = new GameObject("SlotsRow", typeof(RectTransform));
            _slotsRoot.transform.SetParent(panel, false);
            var sr = _slotsRoot.GetComponent<RectTransform>();
            sr.anchorMin = new Vector2(0.18f, 0.74f); sr.anchorMax = new Vector2(0.82f, 0.90f);
            sr.offsetMin = Vector2.zero; sr.offsetMax = Vector2.zero;

            // Divider caption.
            ElarionUiKit.Label(panel, "Unlocked Skills", 0.665f, 0.715f, ElarionUi.Gilt,
                ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Center, 0.08f, 0.92f, bold: true);

            // Unlocked-skill grid host — transparent (the kit slots ARE the chrome;
            // no per-screen well behind them).
            _gridRoot = new GameObject("SkillGrid", typeof(RectTransform));
            _gridRoot.transform.SetParent(panel, false);
            var gr = _gridRoot.GetComponent<RectTransform>();
            gr.anchorMin = new Vector2(0.10f, 0.06f); gr.anchorMax = new Vector2(0.90f, 0.64f);
            gr.offsetMin = Vector2.zero; gr.offsetMax = Vector2.zero;

            // NO per-panel Close/Done button — the shared kit Close (built by
            // BuildObsidianPanel above) is the ONE close game-wide (owner: one common close).
        }

        private void OpenSkillsFromLoadout()
        {
            DeNelle.Core.Diagnostics.FlowTrace.Step("Hero",
                "Loadout empty-state door -> " + HudStrings.Get(HudStrings.KeyHeroSkills));
            if (_vm != null) _vm.Close();
            if (!PanelRouter.Open(PanelId.HeroSkillTree))
                DeNelle.Core.Diagnostics.FlowTrace.Warn("Hero",
                    "LOADOUT empty-state OPEN SKILLS door could not open HeroSkillTree.");
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
            _lastStatus = null;
            _statusSeeded = false;
            if (_ui != null) ElarionUiKit.ClosePanelWithFx(_ui);   // P8 shared close ease
            _ui = null;
            _slotsRoot = null;
            _gridRoot = null;
            PanelManager.NotifyClosed(_panelHandle);
        }
    }
}
