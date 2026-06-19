// =============================================================================
// EquipmentPanel — code-built "browse your gear and equip" screen (WO-109+ ).
// -----------------------------------------------------------------------------
// Opened via Yarn command "OpenEquip" from NPC dialogue (DialogueCommandBridge.CmdOpenEquip).
//
// WO-434 Phase C — REBOUND ONTO EquipVM (MVVM). The panel is now an IPanelView: ALL
// equip STATE + LOGIC (owned-vs-class compatible list, equip/unequip/swap, the per-
// member target picker, stat readouts, equipped marks) lives in EquipVM. This View is
// a DUMB SKIN: in Open() it resolves the live targets (hero + companions) + the owned
// store at the open-site, injects them as IEquipTarget[] / IInventoryStore, constructs
// EquipVM, and Bind()s it. Render() repaints widgets from vm.* on every vm.Changed;
// taps route back as vm commands. It never reads GearLoadout / VillageInventory / the
// catalog directly anymore — those come through the seams the VM owns.
//
// SLOT MODEL (kept faithful): the model supports a weapon (mainhand) + an armor (chest)
// slot today. The old Weapon / Armor FILTER tabs now SELECT the slot (vm.SelectSlot);
// the list under them is vm.CompatibleItems for the selected slot (owned + fit-by-class).
// An UNEQUIP CTA clears the selected slot (vm.Unequip — the Phase A addition).
//
// LAYOUT (the ShopPanel zero-height-collapse lesson — UNCHANGED): rows are laid out by
// a VerticalLayoutGroup + ContentSizeFitter, each row carries a LayoutElement height,
// and after populating we Canvas.ForceUpdateCanvases() then ForceRebuildLayoutImmediate
// so the list gets real height on the same frame instead of collapsing to nothing.
//
// PRESENTATION: every surface routes through the shared DeNelle.Core.UI kit
// (ElarionUiKit + ElarionUi palette + the RPG pack). Sprite-FIRST with the kit's
// procedural fallback, so it is correct with or without pack art on disk (WebGL-safe).
// Blink dressing (WO-432/433 pattern) is flag-gated on FeatureFlags.BlinkChrome.
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

        // WO-434 Phase C — the bound ViewModel + the model seams injected at the open-site.
        private EquipVM _vm;
        private InventoryStore _store;
        private readonly List<GearLoadoutEquipTarget> _targetAdapters = new List<GearLoadoutEquipTarget>();

        // WO-434 Phase D — the live hero preview viewer + the per-target live body it previews.
        // The body GameObjects are resolved at the open-site (same place we resolve the equip
        // targets) so the preview can switch with the target picker. The viewer is a NEW widget
        // bound to a RawImage in the medallion; it degrades gracefully (no body / no RT = no
        // preview, never an NRE).
        private HeroPreviewViewer _preview;
        private RawImage _previewImage;
        private readonly List<GameObject> _targetBodies = new List<GameObject>();
        private int _previewTargetIndex = -1;   // which target the preview currently shows (-1 = none yet)

        // Legacy demo-def equip system (preserved): still equips basic_sword / leather_armor on
        // the HERO so its visual/stat path still fires. Mirrors the old DoEquip side-effect.
        private HeroEquipment _equip;

        // Live regions so equip can repaint in place without a full rebuild.
        private Transform _panelTransform;
        private GameObject _medallionHost;
        private static readonly Vector2 MedAnchorMin = new Vector2(0.04f, 0.80f);
        private static readonly Vector2 MedAnchorMax = new Vector2(0.96f, 0.905f);

        private GameObject _listContentArea; // the content-area host (replaced per slot)
        private RectTransform _scrollContent; // the active VerticalLayoutGroup content (for the rebuild)
        private TMPro.TextMeshProUGUI _summaryLabel;
        private GameObject _tabBar;
        private GameObject _targetBar;

        private static readonly Color TabSelectedTint = new Color(1.15f, 1.10f, 0.92f, 1f);
        private static readonly Color TabRestTint     = new Color(0.58f, 0.55f, 0.50f, 1f);

        private const float RowHeightPx = 64f;
        private const float RowGapPx    = 4f;

        public void Open()
        {
            if (_ui != null) return;

            ConstructViewModel();

            // Kit modal: ScreenSpaceOverlay canvas + scrim + ornate framed panel
            // (same boilerplate the inventory / shop modals use → identical depth).
            _ui = ElarionUiKit.BuildModalCanvas("EquipmentPanel", sortingOrder: 2500);
            _ui.transform.SetParent(transform, false);
            var canvas = _ui.GetComponent<Canvas>();
            if (canvas != null) canvas.overrideSorting = true;
            ElarionUiKit.Scrim(_ui.transform, () => _vm?.Close());

            // Near-black backdrop so the world behind vanishes and the screen reads as its
            // own premium space (mirrors ShopPanel). Visual-only (raycast off) so the scrim
            // below still owns tap-to-close.
            var backdrop = ElarionUiKit.AddImage(_ui.transform, "EquipBackdrop",
                Vector2.zero, Vector2.one, new Color(0.02f, 0.015f, 0.012f, 0.94f), rounded: false);
            var bdImg = backdrop.GetComponent<Image>();
            if (bdImg != null) bdImg.raycastTarget = false;

            // Centred ornate dark-WOOD plate (D3 vendor board) — cohesive with the shop.
            var panel = ElarionUiKit.PanelFramed(_ui.transform,
                                                 new Vector2(0.14f, 0.10f), new Vector2(0.86f, 0.92f),
                                                 deep: true, packSpriteName: RpgUiCatalog.PanelVendor);

            // Solid heavy dark fill inside the frame so it reads premium, not see-through
            // (inset so the carved wood border still shows). Same recipe as ShopPanel.
            var solidFill = ElarionUiKit.AddImage(panel.transform, "EquipSolidFill",
                new Vector2(0.025f, 0.02f), new Vector2(0.975f, 0.98f),
                // Flag OFF = our premium dark plate; flag ON = invisible (kept for layout) so the Blink Obsidian panel shows clean.
                new Color(0.08f, 0.06f, 0.045f, DeNelle.Core.FeatureFlags.BlinkChrome ? 0f : 0.985f));
            var sfImg = solidFill.GetComponent<Image>();
            if (sfImg != null) sfImg.raycastTarget = false;
            solidFill.transform.SetAsFirstSibling();

            // Gilt crest header + gold rule, matching the town HUD / inventory / shop.
            ElarionUiKit.Header(panel.transform, "EQUIPMENT", x0: 0.04f, x1: 0.96f, y0: 0.91f, y1: 0.98f);

            // ── Character medallion (driven by vm.Portrait / vm.CharacterLabel) ──
            _panelTransform = panel.transform;
            BuildCharacterMedallion(panel.transform, MedAnchorMin, MedAnchorMax);

            // ── WO-434 Phase D: live hero preview (a NEW widget) ──────────────────────────
            // A square 3D portrait on the LEFT, just under the medallion band — it shows the
            // ACTUAL hero body and the equipped weapon, refreshing on every vm.Changed. Sits in
            // the gap between the medallion (≥0.80) and the target picker (≤0.79), on the left
            // third, so it never overlaps the picker / summary / tabs / scroll list. Built ONCE
            // here (persists across renders, unlike the rebuilt medallion) and Begun in Bind().
            BuildPreviewWidget(panel.transform);

            // ── Equip-target picker (hero + companions) — vm.TargetNames / vm.SelectTarget ──
            BuildTargetBar(panel.transform, new Vector2(0.04f, 0.715f), new Vector2(0.96f, 0.79f));

            // ── Equipped summary + total bonuses (under the picker, on a recessed well) ──
            ElarionUiKit.Well(panel.transform, new Vector2(0.04f, 0.655f), new Vector2(0.96f, 0.71f));
            _summaryLabel = ElarionUiKit.Label(panel.transform, "", 0.66f, 0.71f, ElarionUi.Parchment,
                                               ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Center,
                                               0.06f, 0.94f);

            // ── Slot-selector tabs (WEAPONS=mainhand / ARMOR=chest) + an UNEQUIP CTA ──
            var tabBar = new GameObject("FilterBar", typeof(RectTransform));
            tabBar.transform.SetParent(panel.transform, false);
            var tb = tabBar.GetComponent<RectTransform>();
            tb.anchorMin = new Vector2(0.04f, 0.575f);
            tb.anchorMax = new Vector2(0.96f, 0.645f);
            tb.offsetMin = Vector2.zero; tb.offsetMax = Vector2.zero;
            CreamTab(ElarionUiKit.ButtonPack(tabBar.transform, "WEAPONS", ElarionUiKit.ButtonKind.Gold,
                                    new Vector2(0.02f, 0.05f), new Vector2(0.40f, 0.95f),
                                    () => SelectSlotKey(EquipVM.SlotMainhand), RpgUiCatalog.ButtonFrame));
            CreamTab(ElarionUiKit.ButtonPack(tabBar.transform, "ARMOR", ElarionUiKit.ButtonKind.Gold,
                                    new Vector2(0.41f, 0.05f), new Vector2(0.79f, 0.95f),
                                    () => SelectSlotKey(EquipVM.SlotChest), RpgUiCatalog.ButtonFrame));
            CreamTab(ElarionUiKit.ButtonPack(tabBar.transform, "UNEQUIP", ElarionUiKit.ButtonKind.Quiet,
                                    new Vector2(0.81f, 0.05f), new Vector2(0.98f, 0.95f),
                                    () => _vm?.Unequip(), RpgUiCatalog.ButtonFrame));
            _tabBar = tabBar;

            // ── Scrollable list content host (rebuilt per slot) ────────────────
            _listContentArea = new GameObject("ListArea", typeof(RectTransform));
            _listContentArea.transform.SetParent(panel.transform, false);
            var la = _listContentArea.GetComponent<RectTransform>();
            la.anchorMin = new Vector2(0.04f, 0.10f);
            la.anchorMax = new Vector2(0.96f, 0.565f);
            la.offsetMin = Vector2.zero; la.offsetMax = Vector2.zero;

            // Close — bottom centre, cream pack button drawn last so it takes taps.
            var closeBtn = ElarionUiKit.ButtonPack(panel.transform, "Close", ElarionUiKit.ButtonKind.Quiet,
                                new Vector2(0.34f, 0.015f), new Vector2(0.66f, 0.085f),
                                () => _vm?.Close(), RpgUiCatalog.ButtonFrame);
            CreamTab(closeBtn);
            if (closeBtn != null) closeBtn.transform.SetAsLastSibling();

            Bind(_vm);
            Debug.Log("[EquipmentPanel] Opened — bound EquipVM (MVVM). Equip to see visual/stat change on hero.");
        }

        // ── Construct the model seams + the pure ViewModel at the open-site ──────────
        // Resolve the assignable targets (hero + every live companion body with a GearLoadout),
        // wrap each in a GearLoadoutEquipTarget, wrap the owned store, and inject both into EquipVM.
        private void ConstructViewModel()
        {
            DisposeViewModel();

            _store = new InventoryStore(VillageInventory.Instance);

            var targets = new List<IEquipTarget>();
            _targetAdapters.Clear();
            _targetBodies.Clear();

            // Legacy demo-def equip system (preserved) — still resolved on the player.
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
                // WO-434 Phase D — the live body the preview clones (the "HeroBody" child the
                // swapper builds; the hero root itself as a fallback). Parallel to `targets`.
                _targetBodies.Add(ResolveBody(hero));
            }

            // Companions: each StoryCompanion body has a GearLoadout bound to its class.
            foreach (var comp in FindObjectsByType<StoryCompanion>(FindObjectsSortMode.None))
            {
                if (comp == null) continue;
                var cl = comp.GetComponent<GearLoadout>();
                if (cl == null) continue;   // non-geared body (e.g. a fallback capsule) — skip
                string cjob = comp.Hero.ToString().ToLowerInvariant();
                var adapter = new GearLoadoutEquipTarget(cl, comp.DisplayName, cjob);
                _targetAdapters.Add(adapter);
                targets.Add(adapter);
                _targetBodies.Add(ResolveBody(comp.gameObject));
            }

            _vm = new EquipVM(_store, targets, onClose: Close);
        }

        // WO-434 Phase D — the live visible body to clone for the preview: the "HeroBody" child
        // HeroBodySwapper / the companion injector build (the skinned class FBX), falling back to
        // the root itself when no such child exists (e.g. a fallback capsule). Null-safe.
        private static GameObject ResolveBody(GameObject root)
        {
            if (root == null) return null;
            var body = root.transform.Find("HeroBody");
            return body != null ? body.gameObject : root;
        }

        // The active target's currently-equipped weapon id (for seating the preview's mesh). "" when none.
        private string ActiveWeaponId()
        {
            if (_vm == null) return null;
            int idx = _vm.ActiveTargetIndex;
            if (idx < 0 || idx >= _targetAdapters.Count) return null;
            var w = _targetAdapters[idx].EquippedWeapon;
            return w != null ? w.id : null;
        }

        // The active target's live body (parallel to the VM's target list). Null when out of range.
        private GameObject ActiveBody()
        {
            if (_vm == null) return null;
            int idx = _vm.ActiveTargetIndex;
            return (idx >= 0 && idx < _targetBodies.Count) ? _targetBodies[idx] : null;
        }

        // The hero's class id from HeroAbilities (the same source GearLoadout uses), defaulting
        // to the catalog default when abilities aren't ready yet.
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

        // Repaint widgets from vm.* ONLY.
        private void Render()
        {
            if (_vm == null) return;
            HighlightTabs();
            HighlightTargets();
            RebuildMedallion();
            RefreshSummary();
            RebuildList();
            RenderPreview();
        }

        // WO-434 Phase D — drive the live preview from vm state. A TARGET switch rebuilds the
        // clone (new body); any other change (equip/unequip/swap) just re-seats the weapon mesh.
        // Begun lazily on the first render after Bind so the body + weapon are resolvable.
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

        // ── Slot select (the old Weapon/Armor filter) ───────────────────────────────
        private void SelectSlotKey(string slotKey)
        {
            if (_vm == null) return;
            for (int i = 0; i < _vm.EquipSlots.Count; i++)
                if (_vm.EquipSlots[i].SlotKey == slotKey) { _vm.SelectSlot(i); return; }
        }

        // ── Target picker (hero + companions) — vm.TargetNames / vm.SelectTarget ─────
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

        private void HighlightTabs()
        {
            if (_tabBar == null || _vm == null) return;
            string active = _vm.SelectedSlotKey == EquipVM.SlotMainhand ? "Btn_WEAPONS" : "Btn_ARMOR";
            foreach (Transform child in _tabBar.transform)
            {
                if (child == null) continue;
                if (child.name == "Btn_UNEQUIP") continue;   // the unequip CTA is not a slot selector
                var img = child.GetComponent<Image>();
                if (img != null) img.color = child.name == active ? TabSelectedTint : TabRestTint;
            }
        }

        // Repaint the equipped-summary + total bonuses from vm.Stats + vm.CharacterLabel.
        private void RefreshSummary()
        {
            if (_summaryLabel == null || _vm == null) return;

            string who = _vm.CharacterLabel;
            string weapon = "none";
            string armor = "none";

            var ws = _vm.EquipSlots;
            for (int i = 0; i < ws.Count; i++)
            {
                var c = ws[i].Content;
                if (ws[i].SlotKey == EquipVM.SlotMainhand) weapon = c.HasValue ? c.Value.Name : "none";
                else if (ws[i].SlotKey == EquipVM.SlotChest) armor = c.HasValue ? c.Value.Name : "none";
            }

            string dmg = StatLabelValue("Damage");
            string def = StatLabelValue("Defense");
            _summaryLabel.text = $"{who}:   {weapon}  /  {armor}      Bonuses:  {dmg} dmg   {def} def";
        }

        // Pull a stat row's bar label by stat name from vm.Stats (e.g. "+20%"). "" when absent.
        private string StatLabelValue(string label)
        {
            if (_vm == null) return "";
            foreach (var s in _vm.Stats)
                if (s.Label == label) return s.Bar.Label ?? "";
            return "";
        }

        // ── List build ───────────────────────────────────────────────────────────
        private void RebuildList()
        {
            using var _ = FlowTrace.Enter("Equip", $"EquipmentPanel.RebuildList slot={_vm?.SelectedSlotKey}");
            // Tear down any previous list inside the content host.
            _scrollContent = null;
            if (_listContentArea != null)
            {
                for (int i = _listContentArea.transform.childCount - 1; i >= 0; i--)
                {
                    var c = _listContentArea.transform.GetChild(i);
                    if (c != null) Destroy(c.gameObject);
                }
            }

            var listRoot = BuildScrollContent();
            int wantCount = _vm != null ? _vm.CompatibleItems.Count : 0;

            // Guard EACH gear row so one bad ItemVM is logged + skipped, never aborting the list.
            var (built, failed) = _vm != null
                ? Guard.TryEach("Equip", "build gear row", _vm.CompatibleItems,
                    item => CreateGearRow(listRoot, item))
                : (0, 0);

            // STOCKED-N COMMIT SEAM: rows offered vs built — data-empty vs built-but-broken.
            FlowTrace.Step("Equip",
                $"EquipmentPanel stocked {built} gear row(s) (wanted {wantCount}, failed {failed}).");

            // VERIFY rows>0: show a VISIBLE empty-state row instead of a blank gear list.
            if (built == 0)
            {
                if (wantCount == 0)
                    FlowTrace.Warn("Equip",
                        $"EquipmentPanel has NO compatible items for slot {_vm?.SelectedSlotKey} — showing empty-state row (data-empty).");
                else
                    FlowTrace.Fail("Equip",
                        $"EquipmentPanel had {wantCount} item(s) but built 0 rows ({failed} failed) — showing empty-state row (built-but-broken).");
                CreateEmptyStateRow(listRoot, "No gear available for this slot.");
            }

            FinalizeScroll();
        }

        // A single visible row carrying the empty-state copy — the never-blank fallback.
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

        // Build the masked, vertically-scrolling content tray and return the
        // VerticalLayoutGroup content the rows parent to. Mirrors the ShopPanel
        // scroll mechanism EXACTLY (well backing + masked viewport + top-anchored
        // content + VerticalLayoutGroup + ContentSizeFitter) — the proven anti-
        // collapse layout. Do not revert to fraction-anchored rows.
        private Transform BuildScrollContent()
        {
            var well = ElarionUiKit.Well(_listContentArea.transform, Vector2.zero, Vector2.one);
            var wImg = well.GetComponent<Image>();
            if (wImg != null)
            {
                wImg.raycastTarget = false;
                // BlinkChrome ON: neutralize the shared well so the Blink panel shows through
                // behind the per-item slot plates (mirrors ShopPanel.BuildScrollContent). Flag OFF → unchanged.
                if (DeNelle.Core.FeatureFlags.BlinkChrome)
                {
                    var c = wImg.color; c.a = 0f; wImg.color = c;
                }
            }

            var viewport = new GameObject("Viewport", typeof(Image), typeof(Mask), typeof(ScrollRect));
            viewport.transform.SetParent(_listContentArea.transform, false);
            var vr = viewport.GetComponent<RectTransform>();
            vr.anchorMin = Vector2.zero; vr.anchorMax = Vector2.one;
            vr.offsetMin = Vector2.zero; vr.offsetMax = Vector2.zero;
            var vImg = viewport.GetComponent<Image>();
            vImg.color = new Color(0f, 0f, 0f, 0.001f); // near-invisible but a valid mask graphic
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            var content = new GameObject("ScrollContent", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            var cr = content.GetComponent<RectTransform>();
            cr.anchorMin = new Vector2(0f, 1f);
            cr.anchorMax = new Vector2(1f, 1f);
            cr.pivot = new Vector2(0.5f, 1f);
            cr.anchoredPosition = Vector2.zero;
            cr.sizeDelta = Vector2.zero; // height driven by the ContentSizeFitter

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

        // CRITICAL (ShopPanel zero-height-collapse lesson): the modal is built AND
        // populated in one synchronous frame before the ScreenSpaceOverlay canvas
        // resolves its rects, so the top-anchored content (sizeDelta.y starts 0) can
        // collapse every row to nothing. Force the canvas rects to resolve, then
        // rebuild the content-area + content layout so rows get real height NOW.
        private void FinalizeScroll()
        {
            if (_scrollContent == null) return;
            Canvas.ForceUpdateCanvases();
            var contentArea = _listContentArea != null ? _listContentArea.transform as RectTransform : null;
            if (contentArea != null) LayoutRebuilder.ForceRebuildLayoutImmediate(contentArea);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_scrollContent);
        }

        // One gear row: a tech-pack gear socket (weapon vs armor frame) + name + an Equip button.
        // Data comes from the bound ItemVM (id/name/icon-keys/rarity/equipped). The equipped row is
        // tinted + tagged; the Equip button routes to vm.Equip(id). Blink slot plate flag-gated.
        private void CreateGearRow(Transform parent, ItemVM row)
        {
            bool isWeapon = _vm != null && _vm.SelectedSlotKey == EquipVM.SlotMainhand;

            var go = new GameObject("GearRow_" + row.Id, typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var le = go.GetComponent<LayoutElement>();
            le.preferredHeight = RowHeightPx;
            le.minHeight = RowHeightPx;
            var rowImg = go.GetComponent<Image>();
            DressRowPlate(rowImg, row.Equipped);

            // Tech gear socket (left) — weapon vs armor frame from the pack.
            var sock = ElarionUiKit.TechGearSocket(go.transform, "Socket",
                new Vector2(0.02f, 0.12f), new Vector2(0.16f, 0.88f),
                new Color(0.85f, 0.7f, 0.2f, 0.9f), isWeapon: isWeapon);
            sock.GetComponent<Image>().raycastTarget = false;
            // Drop the pack sword/shield glyph into the socket, sprite-FIRST.
            var iconSprite = RpgUiCatalog.Get(RpgUiCatalog.RoleIcons,
                isWeapon ? RpgUiCatalog.IconSword : RpgUiCatalog.IconShield);
            var iconGo = ElarionUiKit.AddImage(sock.transform, "Icon",
                new Vector2(0.18f, 0.18f), new Vector2(0.82f, 0.82f), Color.white, rounded: false);
            var iconImg = iconGo.GetComponent<Image>();
            iconImg.raycastTarget = false;
            if (iconSprite != null)
            {
                iconImg.sprite = iconSprite;
                iconImg.preserveAspect = true;
            }
            else
            {
                iconImg.color = new Color(0f, 0f, 0f, 0f);
                ElarionUiKit.Label(iconGo.transform, isWeapon ? "/" : "[]", 0f, 1f,
                    ElarionUi.Parchment, ElarionUi.FontTitle, TMPro.TextAlignmentOptions.Center, 0f, 1f, bold: true);
            }

            // Name (upper) + a marker for the equipped row.
            string nameText = row.Name;
            if (row.Equipped) nameText += "   [Equipped]";
            ElarionUiKit.Label(go.transform, nameText, 0.48f, 0.92f,
                row.Equipped ? ElarionUi.Gilt : ElarionUi.Parchment,
                ElarionUi.FontBody, TMPro.TextAlignmentOptions.Left, 0.18f, 0.74f, bold: row.Equipped);

            // Equip button (primary tech CTA). Disabled on the already-equipped row.
            string id = row.Id;
            var btn = ElarionUiKit.TechPrimaryButton(go.transform, row.Equipped ? "Equipped" : "Equip",
                new Vector2(0.76f, 0.14f), new Vector2(0.98f, 0.86f),
                () => DoEquip(id, isWeapon));
            if (btn != null) btn.interactable = !row.Equipped;
        }

        // Row plate dressing (flag-gated, sprite-first) — mirrors ShopPanel.DressRowPlate.
        // BlinkChrome ON + slot plate present → dress with the Blink per-item slot plate
        // (9-sliced, white). Flag OFF (or plate missing) → the exact current Cell/CellSelected look.
        private static void DressRowPlate(Image rowImg, bool equipped)
        {
            if (rowImg == null) return;
            if (DeNelle.Core.FeatureFlags.BlinkChrome)
            {
                var plate = RpgUiCatalog.Get(RpgUiCatalog.RoleSlot, RpgUiCatalog.SlotItem);
                if (plate != null)
                {
                    rowImg.sprite = plate;
                    rowImg.type   = Image.Type.Sliced;
                    // Keep the equipped affordance: a warm hold tint over the white plate.
                    rowImg.color  = equipped ? new Color(1.15f, 1.10f, 0.92f, 1f) : Color.white;
                    return;
                }
            }
            // Fallback (flag OFF, or plate not imported): the original look, verbatim.
            rowImg.color = equipped ? ElarionUiKit.CellSelected : ElarionUiKit.Cell;
            ElarionUiKit.ApplyRounded(rowImg);
        }

        // Equip routes through the VM (the model seam under it applies damageMult / defense +
        // drives the body visual via GearVisualApplier; the summary reads back from vm.Stats).
        // The legacy HeroEquipment demo path is preserved for the two ids it knows, but ONLY for
        // the HERO (active target index 0) — HeroEquipment lives on the player.
        private void DoEquip(string id, bool isWeapon)
        {
            if (_vm == null) return;
            _vm.Equip(id);

            if (_vm.ActiveTargetIndex == 0 && _equip != null && (id == "basic_sword" || id == "leather_armor"))
                _equip.Equip(id);

            Debug.Log($"[EquipmentPanel] Equipped {id} via EquipVM — hero visual/stat updated.");
        }

        // ── WO-434 Phase D: the live hero preview widget + viewer lifecycle ──────────────
        // The RawImage is built ONCE (persists across vm.Changed renders, unlike the medallion
        // which is destroyed+rebuilt each render). It sits in the medallion band's LEFT crest
        // slot — a real 3D portrait of the hero replacing the static crest glyph — so it adds
        // nothing to the vertical stack and never touches the picker / summary / tabs / scroll.
        private void BuildPreviewWidget(Transform parent)
        {
            // Anchored over the medallion band's left crest circle (matches ClassCrest's slot).
            var host = ElarionUiKit.AddImage(parent, "HeroPreviewPortrait",
                new Vector2(0.05f, 0.795f), new Vector2(0.20f, 0.915f),
                new Color(0.02f, 0.047f, 0.094f, 1f), rounded: false);
            var hostImg = host.GetComponent<Image>();
            if (hostImg != null) hostImg.raycastTarget = false;

            var imgGo = new GameObject("PreviewRawImage", typeof(RectTransform), typeof(RawImage));
            imgGo.transform.SetParent(host.transform, false);
            var rt = imgGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.06f, 0.06f);
            rt.anchorMax = new Vector2(0.94f, 0.94f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            _previewImage = imgGo.GetComponent<RawImage>();
            _previewImage.raycastTarget = false;
            _previewImage.color = Color.white;
            _previewImage.enabled = false;   // hidden until a valid RenderTexture exists
        }

        // Begin (or retarget) the preview against the ACTIVE target's live body + equipped
        // weapon, and bind its RenderTexture to the RawImage. Graceful: any missing piece (no
        // RawImage, no body, RT failure) simply leaves the preview hidden — never an NRE/blank.
        private void BeginOrRetargetPreview()
        {
            if (_previewImage == null) return;

            var body = ActiveBody();
            string weaponId = ActiveWeaponId();
            if (body == null) { HidePreview(); return; }

            bool ok;
            if (_preview == null)
            {
                _preview = new HeroPreviewViewer();
                ok = _preview.Begin(body, textureSize: 384, weaponId: weaponId);
            }
            else
            {
                ok = _preview.Retarget(body, weaponId);
                if (!ok) ok = _preview.IsValid;   // retarget no-op'd but the rig is still valid
            }

            if (ok && _preview.IsValid && _preview.Texture != null)
            {
                _previewImage.texture = _preview.Texture;
                _previewImage.enabled = true;
                // A slight yaw reads as a 3/4 portrait (FrameCamera already angles the camera;
                // this just biases the body so the equipped weapon hand faces the viewer).
                _preview.SetRotation(18f);
            }
            else
            {
                HidePreview();
            }
        }

        // Refresh the equipped-weapon mesh on the preview (cheap — drives the existing rig).
        private void RefreshPreviewWeapon()
        {
            if (_preview == null || !_preview.IsValid) return;
            _preview.RefreshWeapon(ActiveWeaponId());
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

        // ── Character medallion (the gold sunburst PORTRAIT MEDALLION, profile_frame) ──
        // Driven by vm.Portrait (class crest) + vm.CharacterLabel (name — class). Sprite-FIRST:
        // when profile_frame is absent it falls back to a plain Niche backing so a null sprite
        // never blanks the screen.
        private void BuildCharacterMedallion(Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var host = ElarionUiKit.AddImage(parent, "CharacterMedallion", anchorMin, anchorMax,
                new Color(0, 0, 0, 0), rounded: false);
            _medallionHost = host;
            var hostImg = host.GetComponent<Image>();
            if (hostImg != null) hostImg.raycastTarget = false;

            var frame = RpgUiCatalog.Get(RpgUiCatalog.RolePanel, RpgUiCatalog.PanelProfile);
            if (frame != null)
            {
                var fImg = host.GetComponent<Image>();
                fImg.sprite = frame; fImg.color = Color.white; fImg.type = Image.Type.Simple;
                fImg.preserveAspect = false; fImg.raycastTarget = false;
            }
            else
            {
                var niche = ElarionUiKit.Niche(host.transform, Vector2.zero, Vector2.one);
                var nImg = niche.GetComponent<Image>(); if (nImg != null) nImg.raycastTarget = false;
            }

            // Hero crest in the LEFT sunburst circle (the class crest glyph reads as the hero token).
            string portraitClass = _vm != null ? _vm.Portrait.IconName : "";
            ElarionUiKit.Label(host.transform, ClassCrest(portraitClass), 0.10f, 0.90f, ElarionUi.Gilt,
                ElarionUi.FontTitle + 14, TMPro.TextAlignmentOptions.Center, 0.02f, 0.40f, bold: true);

            // Character label (name — class) on the RIGHT-top, from the VM.
            string label = _vm != null ? _vm.CharacterLabel : "Hero";
            ElarionUiKit.Label(host.transform, label, 0.56f, 0.92f, ElarionUi.Parchment,
                ElarionUi.FontHead, TMPro.TextAlignmentOptions.Left, 0.45f, 0.98f, bold: true);

            // TWO bar slots on the RIGHT — HP (red) over MP (blue), driven from vm.Stats fills.
            BarSlot(host.transform, "HP", 0.34f, 0.52f, RpgUiCatalog.BarFrameRed, RpgUiCatalog.BarFillRed,
                new Color(0.62f, 0.16f, 0.14f, 1f), StatFill("HP"));
            BarSlot(host.transform, "MP", 0.12f, 0.30f, RpgUiCatalog.BarFrameBlue, RpgUiCatalog.BarFillBlue,
                new Color(0.18f, 0.33f, 0.62f, 1f), StatFill("MP"));
        }

        // Read a stat row's normalized fill by name from vm.Stats (1f when absent — full, the old look).
        private float StatFill(string label)
        {
            if (_vm == null) return 1f;
            foreach (var s in _vm.Stats)
                if (s.Label == label) return s.Bar.Fill01;
            return 1f;
        }

        // Destroy + rebuild the medallion so its crest/name match the active target (on switch).
        private void RebuildMedallion()
        {
            if (_panelTransform == null) return;
            if (_medallionHost != null) Destroy(_medallionHost);
            BuildCharacterMedallion(_panelTransform, MedAnchorMin, MedAnchorMax);
        }

        // One horizontal bar slot in the medallion's right column (sprite-first frame+fill,
        // procedural tinted fallback). fillFrac in [0..1].
        private void BarSlot(Transform host, string caps, float y0, float y1,
                             string frameSprite, string fillSprite, Color fallbackFill, float fillFrac)
        {
            const float x0 = 0.45f, x1 = 0.97f;
            var frameGo = ElarionUiKit.AddImage(host, "Bar_" + caps + "_frame",
                new Vector2(x0, y0), new Vector2(x1, y1), Color.white, rounded: false);
            var fImg = frameGo.GetComponent<Image>();
            if (fImg != null) fImg.raycastTarget = false;
            var fSprite = RpgUiCatalog.Get(RpgUiCatalog.RoleBars, frameSprite);
            if (fSprite != null) { fImg.sprite = fSprite; fImg.type = Image.Type.Sliced; fImg.color = Color.white; }
            else { fImg.color = new Color(0f, 0f, 0f, 0.35f); ElarionUiKit.ApplyRounded(fImg); }

            float fw = Mathf.Clamp01(fillFrac);
            float fillX1 = 0.04f + (0.97f - 0.04f) * fw;
            var fillGo = ElarionUiKit.AddImage(frameGo.transform, "Bar_" + caps + "_fill",
                new Vector2(0.04f, 0.20f), new Vector2(fillX1, 0.80f), fallbackFill, rounded: false);
            var fillImg = fillGo.GetComponent<Image>();
            if (fillImg != null)
            {
                fillImg.raycastTarget = false;
                var fillS = RpgUiCatalog.Get(RpgUiCatalog.RoleBars, fillSprite);
                if (fillS != null) { fillImg.sprite = fillS; fillImg.type = Image.Type.Sliced; fillImg.color = Color.white; }
                else ElarionUiKit.ApplyRounded(fillImg);
            }
            ElarionUiKit.Label(frameGo.transform, caps, 0f, 1f, ElarionUi.Parchment,
                ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Left, 0.03f, 0.30f, bold: true);
        }

        // Apply the cohesive CREAM bold + dark-outline label treatment to a pack button,
        // drawn last so the label sits crisp above the dark frame interior (matches ShopPanel).
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

        private static string ClassCrest(string job)
        {
            switch ((job ?? "").ToLowerInvariant())
            {
                case "knight": return "/";   // sword
                case "mage":   return "S";   // staff
                case "ranger": return "B";   // bow
                case "cleric": return "C";   // censer
                default:        return "*";
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
            DisposePreview();            // WO-434 Phase D — free the clone + RenderTexture + camera (no leak)
            if (_ui != null) Destroy(_ui);
            _ui = null;
            _listContentArea = null;
            _scrollContent = null;
            _summaryLabel = null;
            _tabBar = null;
            _targetBar = null;
            _medallionHost = null;
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
