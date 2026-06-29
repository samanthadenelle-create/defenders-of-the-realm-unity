// =============================================================================
// EquipmentPanel — the "Gear Preview" showcase + equip screen (WO Gear-Preview, 2026-06-28).
// -----------------------------------------------------------------------------
// Opened via Yarn "OpenEquip" / PanelRouter. Restyled to the owner's mock: a large
// central 3D HERO preview (live, equipped gear visible) framed by labeled Obsidian
// SLOT plates — Full Armor Set, Shield (Off Hand), Weapon (Main Hand), Amulet, Ring.
// Tapping a slot opens a bottom DRAWER listing the compatible owned items for that
// slot, with Equip / Unequip. We DELINEATE the main-hand weapon (sword / 1H / 2H)
// from the OFF-HAND shield (owner requirement): shields appear only in the off-hand
// list; the main-hand list excludes them. The model's EnforceHandSlots still resolves
// 2H↔off-hand conflicts on equip.
//
// MVVM (WO-434): an IPanelView bound to EquipVM. ALL state/logic (slots, equipped
// items, compatible lists, equip/unequip/swap, target picker, stat readouts) lives in
// EquipVM; this View is a DUMB SKIN that repaints from vm.* on vm.Changed and routes
// taps back as commands. It never reads GearLoadout / inventory / catalog directly.
//
// PRESENTATION: routes through the shared DeNelle.Core.UI kit (ElarionUiKit + ElarionUi
// + RpgUiCatalog). Sprite-FIRST with procedural fallback (WebGL-safe). This screen LEADS
// the Obsidian look — it uses the slot/panel pack art whenever present.
// =============================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DeNelle.Core.UI;
using DeNelle.Core.UI.Mvvm;
using DeNelle.Core.Diagnostics;
using DeNelle.Village;
using DeNelle.Village.Crafting;

namespace DeNelle.Village.Hero
{
    public sealed class EquipmentPanel : MonoBehaviour, IPanelView
    {
        private GameObject _ui;

        // WO-434 — the bound ViewModel + the model seams injected at the open-site.
        private EquipVM _vm;
        private InventoryStore _store;
        private readonly List<GearLoadoutEquipTarget> _targetAdapters = new List<GearLoadoutEquipTarget>();

        // Live 3D hero preview (the showcase centerpiece) + the per-target body it previews.
        private HeroPreviewViewer _preview;
        private RawImage _previewImage;
        private readonly List<GameObject> _targetBodies = new List<GameObject>();
        private int _previewTargetIndex = -1;

        // Legacy demo-def equip (preserved) — still equips basic_sword / leather_armor on the HERO.
        private HeroEquipment _equip;

        // Live regions.
        private Transform _panelTransform;
        private GameObject _slotsHost;          // holds the 5 labeled slot plates (rebuilt per render)
        private GameObject _targetBar;

        // Change-drawer (tap a slot → browse compatible items for it).
        private GameObject _drawerHost;
        private string _drawerSlotKey;          // the slot the drawer is editing (null = closed)
        private GameObject _listContentArea;    // the drawer's list host (set when the drawer opens)
        private RectTransform _scrollContent;

        private static readonly Color TabSelectedTint = new Color(1.15f, 1.10f, 0.92f, 1f);
        private static readonly Color TabRestTint     = new Color(0.58f, 0.55f, 0.50f, 1f);

        private const float RowHeightPx = 64f;
        private const float RowGapPx    = 4f;

        public void Open()
        {
            if (_ui != null) return;

            ConstructViewModel();

            _ui = ElarionUiKit.BuildModalCanvas("EquipmentPanel", sortingOrder: 2500);
            _ui.transform.SetParent(transform, false);
            var canvas = _ui.GetComponent<Canvas>();
            if (canvas != null) canvas.overrideSorting = true;
            ElarionUiKit.Scrim(_ui.transform, () => _vm?.Close());

            // SHARED Obsidian chrome (WO-554): black panel + gold trim + gold header +
            // the ONE standard Close button. Replaces the old backdrop + brown PanelFramed +
            // dark solidFill + per-panel "X". Content lives on chrome.content (0..1 anchors).
            var chrome = ElarionUiKit.BuildObsidianPanel(_ui.transform, "CHARACTER",
                new Vector2(0.12f, 0.06f), new Vector2(0.88f, 0.95f),
                () => _vm?.Close(), headerX0: 0.10f, headerX1: 0.90f,
                frameName: RpgUiCatalog.FrameCharacter);
            var panel = chrome.content;

            // UI_BLINK_TEMPLATE_CANON §3/§4: drop content into the frame's BODY drop-zone
            // (the templated, pre-positioned area inside the ornate border) instead of floating
            // over the whole panel rect at 0..1 — that overlapped FrameCharacter's border.
            // FrameCharacter exposes only a body zone (no medallion/footer); the central 3D hero
            // preview stays the medallion-equivalent in the body. Falls back to the panel rect
            // when no frame art is present (WebGL-safe). All fraction layouts below are now
            // relative to this body zone.
            var bodyHost = (chrome.layout != null && chrome.layout.body != null)
                ? chrome.layout.body : (RectTransform)panel.transform;
            _panelTransform = bodyHost;

            // Central 3D hero preview (the showcase centerpiece).
            BuildPreviewWidget(bodyHost);

            // The five labeled slot plates around the hero (rebuilt per render).
            _slotsHost = new GameObject("Slots", typeof(RectTransform));
            _slotsHost.transform.SetParent(bodyHost, false);
            var sh = _slotsHost.GetComponent<RectTransform>();
            sh.anchorMin = Vector2.zero; sh.anchorMax = Vector2.one;
            sh.offsetMin = Vector2.zero; sh.offsetMax = Vector2.zero;

            // Slim target picker (only when there is more than one assignable member).
            if (_vm != null && _vm.TargetNames.Count > 1)
                BuildTargetBar(bodyHost, new Vector2(0.30f, 0.875f), new Vector2(0.70f, 0.91f));

            Bind(_vm);
            Debug.Log("[EquipmentPanel] Opened — Gear Preview showcase bound to EquipVM (MVVM).");
        }

        // ── Construct the model seams + the pure ViewModel at the open-site ──────────
        private void ConstructViewModel()
        {
            DisposeViewModel();

            var targets = new List<IEquipTarget>();
            _targetAdapters.Clear();
            _targetBodies.Clear();

            _equip = FindObjectOfType<HeroEquipment>();
            var hero = GameObject.FindWithTag("Player");
            if (_equip == null && hero != null) _equip = hero.AddComponent<HeroEquipment>();
            if (hero == null)
            {
                var loco = FindObjectOfType<HeroLocomotion>();
                if (loco != null) hero = loco.gameObject;
            }
            if (hero != null)
            {
                var hl = hero.GetComponent<GearLoadout>();
                if (hl == null) hl = hero.AddComponent<GearLoadout>();
                string hjob = ResolveHeroJob(hl);
                var adapter = new GearLoadoutEquipTarget(hl, HeroName(hjob), hjob);
                _targetAdapters.Add(adapter);
                targets.Add(adapter);
                _targetBodies.Add(ResolveBody(hero));
            }

            foreach (var comp in FindObjectsByType<StoryCompanion>(FindObjectsSortMode.None))
            {
                if (comp == null) continue;
                var cl = comp.GetComponent<GearLoadout>();
                if (cl == null) continue;
                string cjob = comp.Hero.ToString().ToLowerInvariant();
                var adapter = new GearLoadoutEquipTarget(cl, comp.DisplayName, cjob);
                _targetAdapters.Add(adapter);
                targets.Add(adapter);
                _targetBodies.Add(ResolveBody(comp.gameObject));
            }

            // WO-578: build the store AFTER the targets so OwnedWeapons/OwnedArmor UNION the gear each
            // party member has auto-equipped (what the Forge surfaces as owned) with VillageInventory —
            // making the Gear Preview drawer agree with the inventory + the Forge on "owned."
            _store = new InventoryStore(VillageInventory.Instance, targets);

            _vm = new EquipVM(_store, targets, onClose: Close);
        }

        private static GameObject ResolveBody(GameObject root)
        {
            if (root == null) return null;
            var body = root.transform.Find("HeroBody");
            return body != null ? body.gameObject : root;
        }

        private string ActiveWeaponId()
        {
            if (_vm == null) return null;
            int idx = _vm.ActiveTargetIndex;
            if (idx < 0 || idx >= _targetAdapters.Count) return null;
            var w = _targetAdapters[idx].EquippedWeapon;
            return w != null ? w.id : null;
        }

        private GameObject ActiveBody()
        {
            if (_vm == null) return null;
            int idx = _vm.ActiveTargetIndex;
            return (idx >= 0 && idx < _targetBodies.Count) ? _targetBodies[idx] : null;
        }

        // WO-567: off-hand (shield) id + armor tier for the active target, so the Gear Preview
        // mirrors the FULL equipped look (weapon + shield + armor tint), not just the weapon.
        private string ActiveOffHandId()
        {
            if (_vm == null) return null;
            int idx = _vm.ActiveTargetIndex;
            if (idx < 0 || idx >= _targetAdapters.Count) return null;
            var o = _targetAdapters[idx].EquippedOffHand;
            return o != null ? o.id : null;
        }

        private int ActiveArmorTier()
        {
            if (_vm == null) return 0;
            int idx = _vm.ActiveTargetIndex;
            if (idx < 0 || idx >= _targetAdapters.Count) return 0;
            return GearLoadout.ArmorVisualTier(_targetAdapters[idx].EquippedArmor);
        }

        private static string ResolveHeroJob(GearLoadout loadout)
        {
            var ha = loadout != null ? loadout.GetComponent<HeroAbilities>() : null;
            string j = ha != null ? ha.HeroClass : null;
            return string.IsNullOrEmpty(j) ? AbilityCatalog.DefaultClass : j;
        }

        // ── IPanelView ──────────────────────────────────────────────────────────────
        public void Bind(IPanelViewModel vm)
        {
            Unbind();
            _vm = vm as EquipVM;
            if (_vm == null) return;
            _vm.Changed += Render;
            Render();
        }

        public void Unbind()
        {
            if (_vm != null) _vm.Changed -= Render;
        }

        private void Render()
        {
            if (_vm == null) return;
            HighlightTargets();
            RebuildSlots();
            RenderPreview();
            if (_drawerSlotKey != null) RebuildList();   // keep the open drawer fresh
        }

        // WO-434 Phase D — drive the live preview from vm state.
        private void RenderPreview()
        {
            if (_previewImage == null || _vm == null) return;
            int idx = _vm.ActiveTargetIndex;
            if (_preview == null || idx != _previewTargetIndex)
            {
                BeginOrRetargetPreview();
                _previewTargetIndex = idx;
            }
            else
            {
                RefreshPreviewWeapon();
            }
        }

        // ── The five labeled slot plates around the hero ─────────────────────────────
        private void RebuildSlots()
        {
            if (_slotsHost == null) return;
            for (int i = _slotsHost.transform.childCount - 1; i >= 0; i--)
            {
                var c = _slotsHost.transform.GetChild(i);
                if (c != null) Destroy(c.gameObject);
            }

            // LEFT column: Full Armor Set (large) + Shield (Off Hand).
            BuildGearSlot(EquipVM.SlotChest,   new Vector2(0.035f, 0.46f), new Vector2(0.235f, 0.82f));
            BuildGearSlot(EquipVM.SlotOffHand, new Vector2(0.035f, 0.10f), new Vector2(0.235f, 0.42f));

            // RIGHT column: Weapon (Main Hand) (large) + Amulet + Ring.
            BuildGearSlot(EquipVM.SlotMainhand, new Vector2(0.765f, 0.52f), new Vector2(0.965f, 0.82f));
            BuildGearSlot(EquipVM.SlotAmulet,   new Vector2(0.765f, 0.33f), new Vector2(0.965f, 0.49f));
            BuildGearSlot(EquipVM.SlotRing,     new Vector2(0.765f, 0.10f), new Vector2(0.965f, 0.30f));
        }

        // One labeled slot: caption above + a framed plate showing the equipped item's glyph +
        // name (rarity-tinted) or an empty placeholder. Tapping opens the change-drawer.
        private void BuildGearSlot(string slotKey, Vector2 anchorMin, Vector2 anchorMax)
        {
            var slot = FindSlot(slotKey);

            // Caption above the plate.
            float capH = (anchorMax.y - anchorMin.y) * 0.16f;
            ElarionUiKit.Label(_slotsHost.transform, SlotCaption(slotKey),
                anchorMax.y, anchorMax.y + capH, ElarionUi.Gilt,
                ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Center, anchorMin.x, anchorMax.x, bold: true);

            // Framed plate (button).
            var go = new GameObject("Slot_" + slotKey, typeof(Image), typeof(Button));
            go.transform.SetParent(_slotsHost.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();

            bool selected = slotKey == _drawerSlotKey;
            // Real Obsidian equipment-slot plate (UI_BLINK_TEMPLATE_CANON §4) — sprite-FIRST,
            // sliced, white; the procedural Cell tint is the WebGL-safe null fallback.
            var plate = RpgUiCatalog.Get(RpgUiCatalog.RoleSlot, RpgUiCatalog.SlotArmor);
            if (plate != null)
            {
                img.sprite = plate; img.type = Image.Type.Sliced;
                img.color = selected ? TabSelectedTint : Color.white;
            }
            else
            {
                img.color = selected ? ElarionUiKit.CellSelected : ElarionUiKit.Cell;
                ElarionUiKit.ApplyRounded(img);
            }

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            ElarionUiKit.StyleButtonColors(btn);
            string key = slotKey;
            btn.onClick.AddListener(() => OnSlotTapped(key));

            bool filled = slot.HasValue && slot.Value.Content.HasValue;
            var item = filled ? slot.Value.Content.Value : default;

            // Role glyph icon (centered, upper portion).
            var iconSprite = RpgUiCatalog.Get(RpgUiCatalog.RoleIcons, SlotIconName(slotKey));
            var iconGo = ElarionUiKit.AddImage(go.transform, "Icon",
                new Vector2(0.28f, filled ? 0.34f : 0.30f), new Vector2(0.72f, filled ? 0.86f : 0.78f),
                Color.white, rounded: false);
            var iconImg = iconGo.GetComponent<Image>();
            iconImg.raycastTarget = false;
            if (iconSprite != null)
            {
                iconImg.sprite = iconSprite; iconImg.preserveAspect = true;
                iconImg.color = filled ? RarityTint(item.Rarity) : new Color(1f, 1f, 1f, 0.30f);
            }
            else
            {
                iconImg.color = new Color(0f, 0f, 0f, 0f);
                ElarionUiKit.Label(iconGo.transform, "?", 0f, 1f,
                    filled ? ElarionUi.Parchment : ElarionUi.ParchmentDim,
                    ElarionUi.FontTitle, TMPro.TextAlignmentOptions.Center, 0f, 1f, bold: true);
            }

            // Item name (or "Empty") along the bottom band.
            string nameText = filled ? item.Name : "Empty";
            ElarionUiKit.Label(go.transform, nameText, 0.04f, filled ? 0.30f : 0.26f,
                filled ? ElarionUi.Parchment : ElarionUi.ParchmentDim,
                ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Center, 0.04f, 0.96f, bold: filled);
        }

        private void OnSlotTapped(string slotKey)
        {
            if (_vm == null) return;
            // Toggle: tapping the open slot again closes the drawer.
            if (_drawerSlotKey == slotKey) { CloseDrawer(); return; }
            SelectSlotKey(slotKey);
            OpenDrawer(slotKey);
        }

        private void SelectSlotKey(string slotKey)
        {
            if (_vm == null) return;
            for (int i = 0; i < _vm.EquipSlots.Count; i++)
                if (_vm.EquipSlots[i].SlotKey == slotKey) { _vm.SelectSlot(i); return; }
        }

        // ── Change-drawer: a bottom tray listing compatible items for the chosen slot ──
        private void OpenDrawer(string slotKey)
        {
            CloseDrawer();
            _drawerSlotKey = slotKey;

            _drawerHost = ElarionUiKit.PanelFramed(_panelTransform,
                new Vector2(0.06f, 0.02f), new Vector2(0.94f, 0.40f),
                deep: true, packSpriteName: RpgUiCatalog.PanelWindowDark);
            var fill = ElarionUiKit.AddImage(_drawerHost.transform, "DrawerFill",
                new Vector2(0.02f, 0.03f), new Vector2(0.98f, 0.97f),
                new Color(0.04f, 0.045f, 0.05f, 0.99f));
            var fImg = fill.GetComponent<Image>();
            if (fImg != null) fImg.raycastTarget = false;
            fill.transform.SetAsFirstSibling();

            ElarionUiKit.Label(_drawerHost.transform, "Change " + SlotCaption(slotKey), 0.84f, 0.97f,
                ElarionUi.Gilt, ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Center, 0.04f, 0.96f, bold: true);

            // Unequip + Done buttons (top row of the drawer).
            var unequip = ElarionUiKit.ButtonPack(_drawerHost.transform, "Unequip", ElarionUiKit.ButtonKind.Quiet,
                new Vector2(0.06f, 0.84f), new Vector2(0.26f, 0.965f), () => { _vm?.Unequip(); }, RpgUiCatalog.ButtonFrame);
            CreamTab(unequip);
            var done = ElarionUiKit.ButtonPack(_drawerHost.transform, "Done", ElarionUiKit.ButtonKind.Gold,
                new Vector2(0.74f, 0.84f), new Vector2(0.94f, 0.965f), CloseDrawer, RpgUiCatalog.ButtonFrame);
            CreamTab(done);

            // List host inside the drawer.
            _listContentArea = new GameObject("ListArea", typeof(RectTransform));
            _listContentArea.transform.SetParent(_drawerHost.transform, false);
            var la = _listContentArea.GetComponent<RectTransform>();
            la.anchorMin = new Vector2(0.05f, 0.06f); la.anchorMax = new Vector2(0.95f, 0.80f);
            la.offsetMin = Vector2.zero; la.offsetMax = Vector2.zero;

            RebuildList();
            RebuildSlots();   // refresh slot highlight
        }

        private void CloseDrawer()
        {
            _drawerSlotKey = null;
            _listContentArea = null;
            _scrollContent = null;
            if (_drawerHost != null) { Destroy(_drawerHost); _drawerHost = null; }
            RebuildSlots();
        }

        // ── Target picker (hero + companions) ────────────────────────────────────────
        private void BuildTargetBar(Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var bar = new GameObject("TargetBar", typeof(RectTransform));
            bar.transform.SetParent(parent, false);
            var br = bar.GetComponent<RectTransform>();
            br.anchorMin = anchorMin; br.anchorMax = anchorMax;
            br.offsetMin = Vector2.zero; br.offsetMax = Vector2.zero;
            _targetBar = bar;

            var names = _vm != null ? _vm.TargetNames : (IReadOnlyList<string>)new List<string>();
            int n = Mathf.Max(1, names.Count);
            const float gap = 0.012f;
            float w = (1f - gap * (n + 1)) / n;
            for (int i = 0; i < names.Count; i++)
            {
                int idx = i;
                float x0 = gap + i * (w + gap);
                var btn = ElarionUiKit.ButtonPack(bar.transform, names[i], ElarionUiKit.ButtonKind.Gold,
                    new Vector2(x0, 0.06f), new Vector2(x0 + w, 0.94f),
                    () => _vm?.SelectTarget(idx), RpgUiCatalog.ButtonFrame);
                CreamTab(btn);
                if (btn != null) btn.name = "Tgt_" + idx;
            }
            HighlightTargets();
        }

        private void HighlightTargets()
        {
            if (_targetBar == null || _vm == null) return;
            string active = "Tgt_" + _vm.ActiveTargetIndex;
            foreach (Transform child in _targetBar.transform)
            {
                if (child == null) continue;
                var img = child.GetComponent<Image>();
                if (img != null) img.color = child.name == active ? TabSelectedTint : TabRestTint;
            }
        }

        // ── List build (inside the drawer) ───────────────────────────────────────────
        private void RebuildList()
        {
            using var _ = FlowTrace.Enter("Equip", $"EquipmentPanel.RebuildList slot={_vm?.SelectedSlotKey}");
            _scrollContent = null;
            if (_listContentArea == null) return;
            for (int i = _listContentArea.transform.childCount - 1; i >= 0; i--)
            {
                var c = _listContentArea.transform.GetChild(i);
                if (c != null) Destroy(c.gameObject);
            }

            var listRoot = BuildScrollContent();
            int wantCount = _vm != null ? _vm.CompatibleItems.Count : 0;

            var (built, failed) = _vm != null
                ? Guard.TryEach("Equip", "build gear row", _vm.CompatibleItems,
                    item => CreateGearRow(listRoot, item))
                : (0, 0);

            FlowTrace.Step("Equip",
                $"EquipmentPanel stocked {built} gear row(s) (wanted {wantCount}, failed {failed}).");

            if (built == 0)
            {
                if (wantCount == 0)
                    FlowTrace.Warn("Equip",
                        $"EquipmentPanel has NO compatible items for slot {_vm?.SelectedSlotKey} — empty-state row (data-empty).");
                else
                    FlowTrace.Fail("Equip",
                        $"EquipmentPanel had {wantCount} item(s) but built 0 rows ({failed} failed) — empty-state row (built-but-broken).");
                CreateEmptyStateRow(listRoot, "No gear available for this slot.");
            }

            FinalizeScroll();
        }

        private void CreateEmptyStateRow(Transform parent, string msg)
        {
            var go = new GameObject("EmptyStateRow", typeof(TMPro.TextMeshProUGUI), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var le = go.GetComponent<LayoutElement>();
            le.preferredHeight = RowHeightPx;
            le.minHeight = RowHeightPx;
            var t = go.GetComponent<TMPro.TextMeshProUGUI>();
            t.text = msg;
            t.fontSize = ElarionUi.FontLabel;
            t.color = ElarionUi.ParchmentDim;
            t.alignment = TMPro.TextAlignmentOptions.Center;
            t.raycastTarget = false;
        }

        private Transform BuildScrollContent()
        {
            var well = ElarionUiKit.Well(_listContentArea.transform, Vector2.zero, Vector2.one);
            var wImg = well.GetComponent<Image>();
            if (wImg != null) wImg.raycastTarget = false;

            var viewport = new GameObject("Viewport", typeof(Image), typeof(Mask), typeof(ScrollRect));
            viewport.transform.SetParent(_listContentArea.transform, false);
            var vr = viewport.GetComponent<RectTransform>();
            vr.anchorMin = Vector2.zero; vr.anchorMax = Vector2.one;
            vr.offsetMin = Vector2.zero; vr.offsetMax = Vector2.zero;
            var vImg = viewport.GetComponent<Image>();
            vImg.color = new Color(0f, 0f, 0f, 0.001f);
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            var content = new GameObject("ScrollContent", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            var cr = content.GetComponent<RectTransform>();
            cr.anchorMin = new Vector2(0f, 1f);
            cr.anchorMax = new Vector2(1f, 1f);
            cr.pivot = new Vector2(0.5f, 1f);
            cr.anchoredPosition = Vector2.zero;
            cr.sizeDelta = Vector2.zero;

            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.spacing = RowGapPx;
            vlg.padding = new RectOffset(4, 4, 4, 4);
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = viewport.GetComponent<ScrollRect>();
            scroll.viewport = vr;
            scroll.content = cr;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 25f;

            _scrollContent = cr;
            return content.transform;
        }

        private void FinalizeScroll()
        {
            if (_scrollContent == null) return;
            Canvas.ForceUpdateCanvases();
            var contentArea = _listContentArea != null ? _listContentArea.transform as RectTransform : null;
            if (contentArea != null) LayoutRebuilder.ForceRebuildLayoutImmediate(contentArea);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_scrollContent);
        }

        // One gear row in the drawer: slot-appropriate glyph + name + Equip CTA.
        private void CreateGearRow(Transform parent, ItemVM row)
        {
            var go = new GameObject("GearRow_" + row.Id, typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var le = go.GetComponent<LayoutElement>();
            le.preferredHeight = RowHeightPx;
            le.minHeight = RowHeightPx;
            var rowImg = go.GetComponent<Image>();
            DressRowPlate(rowImg, row.Equipped);

            string slotKey = _vm != null ? _vm.SelectedSlotKey : EquipVM.SlotMainhand;
            var sock = ElarionUiKit.TechGearSocket(go.transform, "Socket",
                new Vector2(0.02f, 0.12f), new Vector2(0.16f, 0.88f),
                new Color(0.85f, 0.7f, 0.2f, 0.9f), isWeapon: slotKey == EquipVM.SlotMainhand);
            sock.GetComponent<Image>().raycastTarget = false;
            var iconSprite = RpgUiCatalog.Get(RpgUiCatalog.RoleIcons, SlotIconName(slotKey));
            var iconGo = ElarionUiKit.AddImage(sock.transform, "Icon",
                new Vector2(0.18f, 0.18f), new Vector2(0.82f, 0.82f), Color.white, rounded: false);
            var iconImg = iconGo.GetComponent<Image>();
            iconImg.raycastTarget = false;
            if (iconSprite != null) { iconImg.sprite = iconSprite; iconImg.preserveAspect = true; }
            else iconImg.color = new Color(0f, 0f, 0f, 0f);

            string nameText = row.Name;
            if (row.Equipped) nameText += "   [Equipped]";
            ElarionUiKit.Label(go.transform, nameText, 0.18f, 0.92f,
                row.Equipped ? ElarionUi.Gilt : ElarionUi.Parchment,
                ElarionUi.FontBody, TMPro.TextAlignmentOptions.Left, 0.18f, 0.74f, bold: row.Equipped);

            string id = row.Id;
            bool isWeaponSlot = slotKey == EquipVM.SlotMainhand;
            var btn = ElarionUiKit.TechPrimaryButton(go.transform, row.Equipped ? "Equipped" : "Equip",
                new Vector2(0.76f, 0.14f), new Vector2(0.98f, 0.86f),
                () => DoEquip(id, isWeaponSlot));
            if (btn != null) btn.interactable = !row.Equipped;
        }

        private static void DressRowPlate(Image rowImg, bool equipped)
        {
            if (rowImg == null) return;
            var plate = RpgUiCatalog.Get(RpgUiCatalog.RoleSlot, RpgUiCatalog.SlotItem);
            if (plate != null)
            {
                rowImg.sprite = plate;
                rowImg.type   = Image.Type.Sliced;
                rowImg.color  = equipped ? new Color(1.15f, 1.10f, 0.92f, 1f) : Color.white;
                return;
            }
            rowImg.color = equipped ? ElarionUiKit.CellSelected : ElarionUiKit.Cell;
            ElarionUiKit.ApplyRounded(rowImg);
        }

        private void DoEquip(string id, bool isWeapon)
        {
            if (_vm == null) return;
            _vm.Equip(id);
            if (_vm.ActiveTargetIndex == 0 && _equip != null && (id == "basic_sword" || id == "leather_armor"))
                _equip.Equip(id);
            Debug.Log($"[EquipmentPanel] Equipped {id} via EquipVM — hero visual/stat updated.");
        }

        // ── Live hero preview widget (centerpiece) + viewer lifecycle ────────────────
        private void BuildPreviewWidget(Transform parent)
        {
            var host = ElarionUiKit.AddImage(parent, "HeroPreview",
                new Vector2(0.32f, 0.10f), new Vector2(0.68f, 0.88f),
                new Color(0.02f, 0.047f, 0.094f, 1f), rounded: false);
            var hostImg = host.GetComponent<Image>();
            if (hostImg != null)
            {
                hostImg.raycastTarget = false;
                // Central hero plate: real Obsidian character socket (UI_BLINK_TEMPLATE_CANON §4),
                // sprite-FIRST; the dark glass fill stays as the WebGL-safe null fallback.
                var charPlate = RpgUiCatalog.Get(RpgUiCatalog.RoleSlot, RpgUiCatalog.SlotCharacter);
                if (charPlate != null)
                {
                    hostImg.sprite = charPlate;
                    hostImg.type   = Image.Type.Sliced;
                    hostImg.color  = Color.white;
                }
            }

            var imgGo = new GameObject("PreviewRawImage", typeof(RectTransform), typeof(RawImage));
            imgGo.transform.SetParent(host.transform, false);
            var rt = imgGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.04f, 0.04f);
            rt.anchorMax = new Vector2(0.96f, 0.96f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            _previewImage = imgGo.GetComponent<RawImage>();
            _previewImage.raycastTarget = false;
            _previewImage.color = Color.white;
            _previewImage.enabled = false;
        }

        private void BeginOrRetargetPreview()
        {
            if (_previewImage == null) return;

            var body = ActiveBody();
            string weaponId = ActiveWeaponId();
            string offHandId = ActiveOffHandId();
            int armorTier = ActiveArmorTier();
            if (body == null) { HidePreview(); return; }

            bool ok;
            if (_preview == null)
            {
                _preview = new HeroPreviewViewer();
                ok = _preview.Begin(body, textureSize: 512, weaponId: weaponId,
                                    offHandId: offHandId, armorTier: armorTier);
            }
            else
            {
                ok = _preview.Retarget(body, weaponId, offHandId, armorTier);
                if (!ok) ok = _preview.IsValid;
            }

            if (ok && _preview.IsValid && _preview.Texture != null)
            {
                _previewImage.texture = _preview.Texture;
                _previewImage.enabled = true;
                _preview.SetRotation(18f);
            }
            else
            {
                HidePreview();
            }
        }

        private void RefreshPreviewWeapon()
        {
            if (_preview == null || !_preview.IsValid) return;
            // WO-567: mirror the full equipped look (weapon + shield + armor tint), not just weapon.
            _preview.RefreshGear(ActiveWeaponId(), ActiveOffHandId(), ActiveArmorTier());
        }

        private void HidePreview()
        {
            if (_previewImage != null) { _previewImage.enabled = false; _previewImage.texture = null; }
        }

        private void DisposePreview()
        {
            _preview?.Dispose();
            _preview = null;
            HidePreview();
        }

        // ── Helpers ──────────────────────────────────────────────────────────────────
        private SlotVM? FindSlot(string slotKey)
        {
            if (_vm == null) return null;
            foreach (var s in _vm.EquipSlots)
                if (s.SlotKey == slotKey) return s;
            return null;
        }

        private static string SlotCaption(string slotKey)
        {
            switch (slotKey)
            {
                case EquipVM.SlotMainhand: return "Weapon (Main Hand)";
                case EquipVM.SlotOffHand:  return "Shield (Off Hand)";
                case EquipVM.SlotChest:    return "Full Armor Set";
                case EquipVM.SlotAmulet:   return "Amulet";
                case EquipVM.SlotRing:     return "Ring";
                default:                   return slotKey;
            }
        }

        private static string SlotIconName(string slotKey)
        {
            switch (slotKey)
            {
                case EquipVM.SlotMainhand: return RpgUiCatalog.IconSword;
                case EquipVM.SlotOffHand:  return RpgUiCatalog.IconShield;
                case EquipVM.SlotChest:    return RpgUiCatalog.IconInventory;
                case EquipVM.SlotAmulet:   return RpgUiCatalog.IconHeart;
                case EquipVM.SlotRing:     return RpgUiCatalog.IconCompass;
                default:                   return RpgUiCatalog.IconInventory;
            }
        }

        private static Color RarityTint(string rarity)
        {
            switch ((rarity ?? "").ToLowerInvariant())
            {
                case "rare":      return new Color(0.55f, 0.75f, 1f, 1f);
                case "epic":      return new Color(0.78f, 0.55f, 1f, 1f);
                case "legendary": return new Color(1f, 0.84f, 0.32f, 1f);
                default:          return Color.white;
            }
        }

        private static void CreamTab(Button btn)
        {
            if (btn == null) return;
            var lbl = btn.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (lbl == null) return;
            lbl.color = ElarionUi.Parchment;
            lbl.fontStyle = TMPro.FontStyles.Bold;
            lbl.outlineColor = new Color32(20, 12, 4, 235);
            lbl.outlineWidth = 0.22f;
            lbl.transform.SetAsLastSibling();
        }

        private static string HeroName(string job)
        {
            switch ((job ?? "").ToLowerInvariant())
            {
                case "knight": return "Grom";
                case "mage":   return "Thrain";
                case "ranger": return "Sylas";
                case "cleric": return "Elara";
                default:        return Cap(job);
            }
        }

        private static string Cap(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return char.ToUpperInvariant(s[0]) + s.Substring(1);
        }

        private void DisposeViewModel()
        {
            Unbind();
            _vm?.Dispose();
            _vm = null;
            _store?.Dispose();
            _store = null;
            foreach (var a in _targetAdapters) a?.Dispose();
            _targetAdapters.Clear();
        }

        private void Close()
        {
            DisposeViewModel();
            DisposePreview();
            if (_ui != null) Destroy(_ui);
            _ui = null;
            _slotsHost = null;
            _targetBar = null;
            _drawerHost = null;
            _drawerSlotKey = null;
            _listContentArea = null;
            _scrollContent = null;
            _panelTransform = null;
            _previewImage = null;
            _previewTargetIndex = -1;
            _targetBodies.Clear();
        }

        private void OnDestroy()
        {
            DisposeViewModel();
            DisposePreview();
            if (_ui != null) Destroy(_ui);
        }
    }
}
