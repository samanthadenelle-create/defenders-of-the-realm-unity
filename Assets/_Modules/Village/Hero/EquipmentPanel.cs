// =============================================================================
// EquipmentPanel — code-built "browse your gear and equip" screen (WO-109+ ).
// -----------------------------------------------------------------------------
// Opened via Yarn command "OpenEquip" from NPC dialogue.
//
// UPGRADE (Grok-inspired, owner-approved): the panel is now a BROWSE-AND-EQUIP
// LIST — a Weapon / Armor filter at the top, a scrollable list of gear rows for
// the active filter (each row = a tech-pack gear socket + name + its bonus + an
// Equip button), the currently-equipped row clearly marked, and a live
// equipped-summary + total bonuses across the top. It replaces the old two fixed
// slot-sockets + fixed-id Equip/Unequip buttons (which could only equip the demo
// "basic_sword" / "leather_armor").
//
// SOURCE OF THE LIST (reconciled, not greenfield):
//   • OWNED gear from VillageInventory (the canonical owned-gear store the shop
//     Adds to on purchase). For each owned id we look up the real WeaponDef /
//     ArmorDef in GearCatalog so we can show its true bonus.
//   • If the player owns NO gear of the active filter (a fresh demo player), we
//     FALL BACK to the full GearCatalog for that slot, so the list is never empty
//     and the demo is always playable. Catalog-fallback rows are tagged "(catalog)".
//
// EQUIP (existing systems, NOT a new one):
//   • Catalog ids equip through GearLoadout.EquipWeaponById / EquipArmorById — the
//     real path that applies damageMult / defense + drives GearVisualApplier. The
//     equipped-summary reads GearLoadout.EquippedWeapon / EquippedArmor.
//   • The legacy HeroEquipment is still resolved + still equips the two demo defs
//     it knows (basic_sword / leather_armor) so nothing that depended on it breaks.
//     Open()/Close() and the hero resolution are UNCHANGED in surface.
//
// LAYOUT (the ShopPanel zero-height-collapse lesson — DO NOT skip): rows are laid
// out by a VerticalLayoutGroup + ContentSizeFitter, each row carries a
// LayoutElement height, and after populating we Canvas.ForceUpdateCanvases() then
// LayoutRebuilder.ForceRebuildLayoutImmediate(content) so the list gets real
// height on the same frame instead of collapsing to nothing.
//
// PRESENTATION: every surface routes through the shared DeNelle.Core.UI kit
// (ElarionUiKit + ElarionUi palette + the RPG "Tech hud elements" pack) so it
// reads as the SAME designed game as the town HUD / inventory / shop. Sprite-FIRST
// with the kit's procedural fallback, so it is correct with or without pack art
// on disk (WebGL-safe).
// =============================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DeNelle.Core.UI;
using DeNelle.Village;
using DeNelle.Village.Crafting;

namespace DeNelle.Village.Hero
{
    public sealed class EquipmentPanel : MonoBehaviour
    {
        private enum Filter { Weapon, Armor }

        private GameObject _ui;

        // Legacy demo-def equip system (preserved). Still equips basic_sword / leather_armor.
        private HeroEquipment _equip;
        // Real catalog-driven equip + the equipped-state read surface for the ACTIVE target
        // (damageMult / defense + visuals). Repointed to the selected party member by SelectTarget.
        private GearLoadout _loadout;

        // EQUIP TARGET (owner 2026-06-16): one entry per party member you can assign gear to —
        // the hero plus each live companion. Selecting one repoints _loadout/_activeClass so the
        // list filters to what that member's class can use and equips route to its loadout.
        private sealed class Target
        {
            public string name;     // display label (Grom / Sylas / …)
            public string job;      // class id (knight / ranger / …) — drives the restriction filter
            public GearLoadout loadout;
        }
        private readonly List<Target> _targets = new List<Target>();
        private int _activeIndex;
        private GameObject _targetBar;
        // The active target's class id; drives weapon job-match + armor weight filtering + medallion.
        private string _activeClass = "knight";

        // Medallion is rebuilt on target-switch so its portrait/name reflect the active member.
        private Transform _panelTransform;
        private GameObject _medallionHost;
        private static readonly Vector2 MedAnchorMin = new Vector2(0.04f, 0.80f);
        private static readonly Vector2 MedAnchorMax = new Vector2(0.96f, 0.905f);

        private Filter _filter = Filter.Weapon;

        // Live regions so equip can repaint in place without a full rebuild.
        private GameObject _listContentArea; // the content-area host (replaced per filter)
        private RectTransform _scrollContent; // the active VerticalLayoutGroup content (for the rebuild)
        private TMPro.TextMeshProUGUI _summaryLabel;
        private GameObject _tabBar;

        private static readonly Color TabSelectedTint = new Color(1.15f, 1.10f, 0.92f, 1f);
        private static readonly Color TabRestTint     = new Color(0.58f, 0.55f, 0.50f, 1f);

        private const float RowHeightPx = 64f;
        private const float RowGapPx    = 4f;

        public void Open()
        {
            if (_ui != null) return;

            ResolveTargets();

            // Kit modal: ScreenSpaceOverlay canvas + scrim + ornate framed panel
            // (same boilerplate the inventory / shop modals use → identical depth).
            _ui = ElarionUiKit.BuildModalCanvas("EquipmentPanel", sortingOrder: 2500);
            _ui.transform.SetParent(transform, false);
            var canvas = _ui.GetComponent<Canvas>();
            if (canvas != null) canvas.overrideSorting = true;
            ElarionUiKit.Scrim(_ui.transform, Close);

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

            // ── Character medallion: the gold sunburst PORTRAIT MEDALLION (profile_frame) ─
            // Left circle = hero crest/portrait, right slots = name + HP/MP bars. Sprite-
            // first, falls back to a Niche backing when the pack art is absent.
            _panelTransform = panel.transform;
            BuildCharacterMedallion(panel.transform, MedAnchorMin, MedAnchorMax);

            // ── Equip-target picker (hero + companions) — assign gear to a chosen member ──
            BuildTargetBar(panel.transform, new Vector2(0.04f, 0.715f), new Vector2(0.96f, 0.79f));

            // ── Equipped summary + total bonuses (under the picker, on a recessed well) ──
            ElarionUiKit.Well(panel.transform, new Vector2(0.04f, 0.655f), new Vector2(0.96f, 0.71f));
            _summaryLabel = ElarionUiKit.Label(panel.transform, "", 0.66f, 0.71f, ElarionUi.Parchment,
                                               ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Center,
                                               0.06f, 0.94f);

            // ── Weapon / Armor filter tabs (cream pack labels) ────────────────────
            var tabBar = new GameObject("FilterBar", typeof(RectTransform));
            tabBar.transform.SetParent(panel.transform, false);
            var tb = tabBar.GetComponent<RectTransform>();
            tb.anchorMin = new Vector2(0.04f, 0.575f);
            tb.anchorMax = new Vector2(0.96f, 0.645f);
            tb.offsetMin = Vector2.zero; tb.offsetMax = Vector2.zero;
            CreamTab(ElarionUiKit.ButtonPack(tabBar.transform, "WEAPONS", ElarionUiKit.ButtonKind.Gold,
                                    new Vector2(0.02f, 0.05f), new Vector2(0.49f, 0.95f),
                                    () => SetFilter(Filter.Weapon), RpgUiCatalog.ButtonFrame));
            CreamTab(ElarionUiKit.ButtonPack(tabBar.transform, "ARMOR", ElarionUiKit.ButtonKind.Gold,
                                    new Vector2(0.51f, 0.05f), new Vector2(0.98f, 0.95f),
                                    () => SetFilter(Filter.Armor), RpgUiCatalog.ButtonFrame));
            _tabBar = tabBar;

            // ── Scrollable list content host (rebuilt per filter) ────────────────
            _listContentArea = new GameObject("ListArea", typeof(RectTransform));
            _listContentArea.transform.SetParent(panel.transform, false);
            var la = _listContentArea.GetComponent<RectTransform>();
            la.anchorMin = new Vector2(0.04f, 0.10f);
            la.anchorMax = new Vector2(0.96f, 0.565f);
            la.offsetMin = Vector2.zero; la.offsetMax = Vector2.zero;

            // Close — bottom centre, cream pack button drawn last so it takes taps.
            var closeBtn = ElarionUiKit.ButtonPack(panel.transform, "Close", ElarionUiKit.ButtonKind.Quiet,
                                new Vector2(0.34f, 0.015f), new Vector2(0.66f, 0.085f),
                                Close, RpgUiCatalog.ButtonFrame);
            CreamTab(closeBtn);
            if (closeBtn != null) closeBtn.transform.SetAsLastSibling();

            SetFilter(_filter);
            Debug.Log("[EquipmentPanel] Opened — browse-and-equip list. Equip to see visual/stat change on hero.");
        }

        // Build the assignable-target list: the player hero first, then every live companion
        // body (each carries its own GearLoadout + class). Preserves the legacy HeroEquipment
        // resolution on the Player. Active target starts on the hero.
        private void ResolveTargets()
        {
            _targets.Clear();

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
                _targets.Add(new Target { name = HeroName(hjob), job = hjob, loadout = hl });
            }

            // Companions: each StoryCompanion body has a GearLoadout bound to its class.
            foreach (var comp in FindObjectsByType<StoryCompanion>(FindObjectsSortMode.None))
            {
                if (comp == null) continue;
                var cl = comp.GetComponent<GearLoadout>();
                if (cl == null) continue;   // non-geared body (e.g. a fallback capsule) — skip
                string cjob = comp.Hero.ToString().ToLowerInvariant();
                _targets.Add(new Target { name = comp.DisplayName, job = cjob, loadout = cl });
            }

            // Never leave the panel target-less (hero not yet in the scene).
            if (_targets.Count == 0)
                _targets.Add(new Target { name = "Hero", job = "knight", loadout = null });

            _activeIndex = 0;
            ApplyActiveTarget();
        }

        // The hero's class id from HeroAbilities (the same source GearLoadout uses), defaulting
        // to the catalog default when abilities aren't ready yet.
        private static string ResolveHeroJob(GearLoadout loadout)
        {
            var ha = loadout != null ? loadout.GetComponent<HeroAbilities>() : null;
            string j = ha != null ? ha.HeroClass : null;
            return string.IsNullOrEmpty(j) ? AbilityCatalog.DefaultClass : j;
        }

        // Point _loadout / _activeClass at the currently-selected target.
        private void ApplyActiveTarget()
        {
            if (_targets.Count == 0) return;
            _activeIndex = Mathf.Clamp(_activeIndex, 0, _targets.Count - 1);
            var t = _targets[_activeIndex];
            _loadout = t.loadout;
            _activeClass = string.IsNullOrEmpty(t.job) ? "knight" : t.job;
        }

        // A horizontal row of member chips (hero + companions); tapping one re-targets equip.
        private void BuildTargetBar(Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var bar = new GameObject("TargetBar", typeof(RectTransform));
            bar.transform.SetParent(parent, false);
            var br = bar.GetComponent<RectTransform>();
            br.anchorMin = anchorMin; br.anchorMax = anchorMax;
            br.offsetMin = Vector2.zero; br.offsetMax = Vector2.zero;
            _targetBar = bar;

            int n = Mathf.Max(1, _targets.Count);
            const float gap = 0.012f;
            float w = (1f - gap * (n + 1)) / n;
            for (int i = 0; i < _targets.Count; i++)
            {
                int idx = i;
                float x0 = gap + i * (w + gap);
                var btn = ElarionUiKit.ButtonPack(bar.transform, _targets[i].name, ElarionUiKit.ButtonKind.Gold,
                    new Vector2(x0, 0.06f), new Vector2(x0 + w, 0.94f),
                    () => SelectTarget(idx), RpgUiCatalog.ButtonFrame);
                CreamTab(btn);
                if (btn != null) btn.name = "Tgt_" + idx;
            }
            HighlightTargets();
        }

        // Re-target equip to member <paramref name="index"/>: repoint the loadout/class, then
        // repaint the medallion + filtered list + summary for the newly-selected member.
        private void SelectTarget(int index)
        {
            _activeIndex = index;
            ApplyActiveTarget();
            HighlightTargets();
            RebuildMedallion();
            RebuildList();
            RefreshSummary();
        }

        private void HighlightTargets()
        {
            if (_targetBar == null) return;
            string active = "Tgt_" + _activeIndex;
            foreach (Transform child in _targetBar.transform)
            {
                if (child == null) continue;
                var img = child.GetComponent<Image>();
                if (img != null) img.color = child.name == active ? TabSelectedTint : TabRestTint;
            }
        }

        private void SetFilter(Filter filter)
        {
            _filter = filter;
            HighlightTabs();
            RebuildList();
            RefreshSummary();
        }

        private void HighlightTabs()
        {
            if (_tabBar == null) return;
            string active = _filter == Filter.Weapon ? "Btn_WEAPONS" : "Btn_ARMOR";
            foreach (Transform child in _tabBar.transform)
            {
                if (child == null) continue;
                var img = child.GetComponent<Image>();
                if (img != null) img.color = child.name == active ? TabSelectedTint : TabRestTint;
            }
        }

        // Repaint the equipped-summary + total bonuses from the LIVE GearLoadout
        // (the real equipped state). Falls back to a neutral line if no loadout.
        private void RefreshSummary()
        {
            if (_summaryLabel == null) return;
            if (_loadout == null)
            {
                _summaryLabel.text = "No hero to equip.";
                return;
            }

            string weapon = _loadout.EquippedWeapon != null
                ? (string.IsNullOrEmpty(_loadout.EquippedWeapon.name) ? _loadout.EquippedWeapon.id : _loadout.EquippedWeapon.name)
                : "none";
            string armor = _loadout.EquippedArmor != null
                ? (string.IsNullOrEmpty(_loadout.EquippedArmor.name) ? _loadout.EquippedArmor.id : _loadout.EquippedArmor.name)
                : "none";

            // Totals expressed from the loadout's applied stats: weapon damage as a
            // percent over the 1.0 baseline, armor as its fractional reduction.
            int dmgPct = Mathf.RoundToInt((_loadout.WeaponMult - 1f) * 100f);
            int defPct = Mathf.RoundToInt(_loadout.ArmorDefense * 100f);
            string who = (_activeIndex >= 0 && _activeIndex < _targets.Count) ? _targets[_activeIndex].name : "Hero";
            _summaryLabel.text = $"{who}:   {weapon}  /  {armor}      Bonuses:  +{dmgPct}% dmg   +{defPct}% def";
        }

        // ── List build ───────────────────────────────────────────────────────────

        private void RebuildList()
        {
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

            // Gather the rows for the active filter: OWNED first, catalog fallback.
            var rows = _filter == Filter.Weapon ? BuildWeaponRows() : BuildArmorRows();

            var listRoot = BuildScrollContent();
            foreach (var r in rows) CreateGearRow(listRoot, r);

            FinalizeScroll();
        }

        // A single resolved row: id + display + bonus text + equip closure + state.
        private sealed class GearRow
        {
            public string id;
            public string name;
            public string bonus;
            public bool isWeapon;
            public bool equipped;
            public bool fromCatalog; // true = not actually owned, shown so the list isn't empty
        }

        private List<GearRow> BuildWeaponRows()
        {
            var rows = new List<GearRow>();
            string equippedId = _loadout != null && _loadout.EquippedWeapon != null ? _loadout.EquippedWeapon.id : null;

            // OWNED weapons (VillageInventory) resolved against the catalog.
            var inv = VillageInventory.Instance;
            var seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            if (inv != null)
            {
                foreach (var kv in inv.Counts)
                {
                    if (kv.Value <= 0) continue;
                    var w = GearCatalog.FindWeapon(kv.Key);
                    if (w == null) continue;
                    if (!GearCatalog.WeaponFitsClass(w, _activeClass)) continue;   // class restriction
                    seen.Add(w.id);
                    rows.Add(MakeWeaponRow(w, equippedId, fromCatalog: false));
                }
            }

            // Fallback: own nothing this member can use → show the catalog for its class so the
            // list is never empty (a demo player can still browse + equip).
            if (rows.Count == 0)
            {
                foreach (var w in GearCatalog.AllWeapons())
                {
                    if (w == null || seen.Contains(w.id)) continue;
                    if (!GearCatalog.WeaponFitsClass(w, _activeClass)) continue;   // class restriction
                    rows.Add(MakeWeaponRow(w, equippedId, fromCatalog: true));
                }
            }
            return rows;
        }

        private List<GearRow> BuildArmorRows()
        {
            var rows = new List<GearRow>();
            string equippedId = _loadout != null && _loadout.EquippedArmor != null ? _loadout.EquippedArmor.id : null;

            var inv = VillageInventory.Instance;
            var seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            if (inv != null)
            {
                foreach (var kv in inv.Counts)
                {
                    if (kv.Value <= 0) continue;
                    var a = GearCatalog.FindArmor(kv.Key);
                    if (a == null) continue;
                    if (!GearCatalog.ArmorFitsClass(a, _activeClass)) continue;   // weight-class restriction
                    seen.Add(a.id);
                    rows.Add(MakeArmorRow(a, equippedId, fromCatalog: false));
                }
            }

            if (rows.Count == 0)
            {
                foreach (var a in GearCatalog.AllArmors())
                {
                    if (a == null || seen.Contains(a.id)) continue;
                    if (!GearCatalog.ArmorFitsClass(a, _activeClass)) continue;   // weight-class restriction
                    rows.Add(MakeArmorRow(a, equippedId, fromCatalog: true));
                }
            }
            return rows;
        }

        // The catalog defs carry NO flat "+2 reach / +3 damage" fields. A WeaponDef
        // exposes damageMult (a multiplier; 1.2 = +20%) + reach (melee hitbox metres);
        // an ArmorDef exposes defense (fractional reduction; 0.04 = 4%) + hpBonus. We
        // surface those true numbers rather than inventing flat bonuses.
        private GearRow MakeWeaponRow(WeaponDef w, string equippedId, bool fromCatalog)
        {
            int dmgPct = Mathf.RoundToInt((Mathf.Max(0.1f, w.damageMult) - 1f) * 100f);
            string bonus = $"+{dmgPct}% dmg";
            if (w.reach > 0f) bonus += $"   reach {w.reach:0.#}m";
            return new GearRow
            {
                id = w.id,
                name = string.IsNullOrEmpty(w.name) ? w.id : w.name,
                bonus = bonus,
                isWeapon = true,
                equipped = !string.IsNullOrEmpty(equippedId) && string.Equals(equippedId, w.id, System.StringComparison.OrdinalIgnoreCase),
                fromCatalog = fromCatalog,
            };
        }

        private GearRow MakeArmorRow(ArmorDef a, string equippedId, bool fromCatalog)
        {
            int defPct = Mathf.RoundToInt(Mathf.Clamp(a.defense, 0f, 0.9f) * 100f);
            string bonus = $"+{defPct}% def";
            if (a.hpBonus > 0f) bonus += $"   +{a.hpBonus:0.#} hp";
            return new GearRow
            {
                id = a.id,
                name = string.IsNullOrEmpty(a.name) ? a.id : a.name,
                bonus = bonus,
                isWeapon = false,
                equipped = !string.IsNullOrEmpty(equippedId) && string.Equals(equippedId, a.id, System.StringComparison.OrdinalIgnoreCase),
                fromCatalog = fromCatalog,
            };
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
            if (wImg != null) wImg.raycastTarget = false;

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
            Debug.Log($"[EquipmentPanel] FinalizeScroll: rows={_scrollContent.childCount}, " +
                      $"contentH={_scrollContent.rect.height:F0}");
        }

        // One gear row: a tech-pack gear socket (weapon vs armor frame) + name + its
        // bonus + an Equip button. The currently-equipped row is tinted + tagged and
        // its socket shows the equipped-check; catalog-fallback rows are tagged.
        private void CreateGearRow(Transform parent, GearRow row)
        {
            var go = new GameObject("GearRow_" + row.id, typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var le = go.GetComponent<LayoutElement>();
            le.preferredHeight = RowHeightPx;
            le.minHeight = RowHeightPx;
            var rowImg = go.GetComponent<Image>();
            rowImg.color = row.equipped ? ElarionUiKit.CellSelected : ElarionUiKit.Cell;
            ElarionUiKit.ApplyRounded(rowImg);

            // Tech gear socket (left) — weapon vs armor frame from the pack.
            var sock = ElarionUiKit.TechGearSocket(go.transform, "Socket",
                new Vector2(0.02f, 0.12f), new Vector2(0.16f, 0.88f),
                new Color(0.85f, 0.7f, 0.2f, 0.9f), isWeapon: row.isWeapon);
            sock.GetComponent<Image>().raycastTarget = false;
            // Drop the pack sword/shield glyph into the socket, sprite-FIRST.
            var iconSprite = RpgUiCatalog.Get(RpgUiCatalog.RoleIcons,
                row.isWeapon ? RpgUiCatalog.IconSword : RpgUiCatalog.IconShield);
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
                ElarionUiKit.Label(iconGo.transform, row.isWeapon ? "/" : "[]", 0f, 1f,
                    ElarionUi.Parchment, ElarionUi.FontTitle, TMPro.TextAlignmentOptions.Center, 0f, 1f, bold: true);
            }

            // Name (upper) + bonus (lower) text column.
            string nameText = row.name;
            if (row.equipped) nameText += "   [Equipped]";
            else if (row.fromCatalog) nameText += "   (catalog)";
            ElarionUiKit.Label(go.transform, nameText, 0.48f, 0.92f,
                row.equipped ? ElarionUi.Gilt : ElarionUi.Parchment,
                ElarionUi.FontBody, TMPro.TextAlignmentOptions.Left, 0.18f, 0.74f, bold: row.equipped);
            ElarionUiKit.Label(go.transform, row.bonus, 0.10f, 0.50f, ElarionUi.Affordable,
                ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Left, 0.18f, 0.74f);

            // Equip button (primary tech CTA). Disabled on the already-equipped row.
            var btn = ElarionUiKit.TechPrimaryButton(go.transform, row.equipped ? "Equipped" : "Equip",
                new Vector2(0.76f, 0.14f), new Vector2(0.98f, 0.86f),
                () => DoEquip(row));
            if (btn != null) btn.interactable = !row.equipped;
        }

        // Equip routes through the EXISTING systems — never a new one:
        //   • GearLoadout for real catalog gear (applies damageMult / defense +
        //     drives the body visual via GearVisualApplier). This is the source of
        //     truth the summary reads back.
        //   • HeroEquipment additionally handles the two legacy demo defs it knows
        //     (basic_sword / leather_armor) so its visual/stat path still fires.
        // Then we repaint the list (re-mark equipped) + the summary (recompute bonus).
        private void DoEquip(GearRow row)
        {
            if (_loadout != null)
            {
                if (row.isWeapon) _loadout.EquipWeaponById(row.id);
                else              _loadout.EquipArmorById(row.id);
            }

            // Preserve the legacy demo-def path for the ids HeroEquipment recognises — but ONLY
            // when the HERO is the active target (index 0). HeroEquipment lives on the player, so
            // running it while a companion is selected would wrongly re-equip the hero.
            if (_activeIndex == 0 && _equip != null && (row.id == "basic_sword" || row.id == "leather_armor"))
                _equip.Equip(row.id);

            Debug.Log($"[EquipmentPanel] Equipped {row.id} — hero visual/stat updated; list + summary refreshed.");
            RebuildList();
            RefreshSummary();
        }

        // ── Character medallion (the gold sunburst PORTRAIT MEDALLION, profile_frame) ──
        // 758x396 frame: circular gold sunburst portrait socket on the LEFT (~0.0-0.42),
        // a name area + TWO horizontal bar slots on the RIGHT. We seat the hero crest in
        // the left circle, the hero name on the right-top, and drive the two bar slots as
        // HP / MP fills. Sprite-FIRST: when profile_frame is absent it falls back to a
        // plain Niche backing so a null sprite never blanks the screen.
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

            // Hero crest in the LEFT sunburst circle (no portrait art exists yet — the class
            // crest glyph reads as the hero token, same as the inventory paper-doll).
            string job = HeroClassName();
            ElarionUiKit.Label(host.transform, ClassCrest(job), 0.10f, 0.90f, ElarionUi.Gilt,
                ElarionUi.FontTitle + 14, TMPro.TextAlignmentOptions.Center, 0.02f, 0.40f, bold: true);

            // Hero name on the RIGHT-top.
            ElarionUiKit.Label(host.transform, HeroName(job), 0.56f, 0.92f, ElarionUi.Parchment,
                ElarionUi.FontHead, TMPro.TextAlignmentOptions.Left, 0.45f, 0.98f, bold: true);

            // TWO bar slots on the RIGHT — HP (red) over MP (blue). Driven full for now (no
            // live HP/MP feed in this assembly); the fills sit in the frame's bar windows.
            BarSlot(host.transform, "HP", 0.34f, 0.52f, RpgUiCatalog.BarFrameRed, RpgUiCatalog.BarFillRed,
                new Color(0.62f, 0.16f, 0.14f, 1f));
            BarSlot(host.transform, "MP", 0.12f, 0.30f, RpgUiCatalog.BarFrameBlue, RpgUiCatalog.BarFillBlue,
                new Color(0.18f, 0.33f, 0.62f, 1f));
        }

        // Destroy + rebuild the medallion so its crest/name match the active target (on switch).
        private void RebuildMedallion()
        {
            if (_panelTransform == null) return;
            if (_medallionHost != null) Destroy(_medallionHost);
            BuildCharacterMedallion(_panelTransform, MedAnchorMin, MedAnchorMax);
        }

        // One horizontal bar slot in the medallion's right column (sprite-first frame+fill,
        // procedural tinted fallback). fillFrac in [0..1]; here we show full.
        private void BarSlot(Transform host, string caps, float y0, float y1,
                             string frameSprite, string fillSprite, Color fallbackFill)
        {
            const float x0 = 0.45f, x1 = 0.97f;
            var frameGo = ElarionUiKit.AddImage(host, "Bar_" + caps + "_frame",
                new Vector2(x0, y0), new Vector2(x1, y1), Color.white, rounded: false);
            var fImg = frameGo.GetComponent<Image>();
            if (fImg != null) fImg.raycastTarget = false;
            var fSprite = RpgUiCatalog.Get(RpgUiCatalog.RoleBars, frameSprite);
            if (fSprite != null) { fImg.sprite = fSprite; fImg.type = Image.Type.Sliced; fImg.color = Color.white; }
            else { fImg.color = new Color(0f, 0f, 0f, 0.35f); ElarionUiKit.ApplyRounded(fImg); }

            var fillGo = ElarionUiKit.AddImage(frameGo.transform, "Bar_" + caps + "_fill",
                new Vector2(0.04f, 0.20f), new Vector2(0.97f, 0.80f), fallbackFill, rounded: false);
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

        // The medallion now reflects the ACTIVE target (hero or selected companion), not always
        // the player hero — so its crest + name change as you switch members.
        private string HeroClassName()
        {
            return string.IsNullOrEmpty(_activeClass) ? AbilityCatalog.DefaultClass : _activeClass;
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

        private void Close()
        {
            if (_ui != null) Destroy(_ui);
            _ui = null;
            _listContentArea = null;
            _scrollContent = null;
            _summaryLabel = null;
            _tabBar = null;
            _targetBar = null;
            _medallionHost = null;
            _panelTransform = null;
            _targets.Clear();
        }

        private void OnDestroy()
        {
            if (_ui != null) Destroy(_ui);
        }
    }
}
