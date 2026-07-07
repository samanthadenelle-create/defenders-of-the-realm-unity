// =============================================================================
// BuildingUpgradePanelMvvm — the building ENHANCEMENT (perk-grid) VIEW (MVVM).
// A DUMB SKIN: it builds presentation through the ElarionUiKit MASTER FRAME
// (BuildObsidianPanel + drop-zones, UI_BLINK_TEMPLATE_CANON) and BINDS a
// BuildingUpgradeVM. ALL state/logic (affordability, unlock, tier gating) lives
// in the VM — the View never reads game state (audit 2026-07-02 §3.1 finish).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Buildings.Progression
//
// Owner redo 2026-07-02: a Warcraft-3-style PERK GRID of tiles (kit slot plates,
// RpgUiCatalog RoleSlot — empty tiers still read as a grid). Each tile: perk
// icon, name, COST, one-line concrete EFFECT (from the perk data). Tap a tile to
// unlock. States: owned=lit, next/affordable=gold affordance, locked=dimmed +
// requirement line. VERBIAGE LAW: "Unlock perk"/"Enhancement" language only.
// ONE shared Close (the Obsidian chrome's); no other buttons — one action = one
// tile (the old duplicate "big CTA" button is gone, per the button law).
//
// Code-built uGUI ONLY (no UXML — §8). Chrome = BuildObsidianPanel(FrameCore):
// title -> layout.header, grid -> layout.body, wallet -> layout.footer.
// Smoothness (owner 2026-07-02): eased open/close (scale+fade, ~0.18s/0.14s,
// via the local PanelOpenCloseFx below — no shared kit tween exists yet; flagged
// for promotion to ElarionUiKit) + button ColorTint fade (never snap).
//
// SHIPS BEHIND FeatureFlags.BuildingUpgradePanel (default ON since WO-476 — this
// panel IS the live upgrade surface; the legacy UIDocument twin was DELETED
// 2026-07-02). Distinct GameObject name ("BuildingUpgradePanelMvvm").
// =============================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DeNelle.Core.UI;
using DeNelle.Core.UI.Mvvm;

namespace DeNelle.Village.Buildings.Progression
{
    [DisallowMultipleComponent]
    public sealed class BuildingUpgradePanelMvvm : MonoBehaviour, IPanelView
    {
        // Owned (lit) tile tint — warm gilt lift over the slot plate.
        private static readonly Color OwnedTint = new Color(1.18f, 1.12f, 0.92f, 1f);
        // Locked tile dim — the plate greys down + drops alpha.
        private static readonly Color LockedTint = new Color(0.52f, 0.52f, 0.55f, 0.80f);

        private BuildingUpgradeVM _vm;

        private GameObject _ui;
        private GameObject _contentRoot;          // scroll host inside layout.body
        private RectTransform _scrollContent;     // GridLayoutGroup content
        private GridLayoutGroup _grid;
        private TMPro.TextMeshProUGUI _walletText;
        private TMPro.TextMeshProUGUI _statusText;

        private PanelHandle _panelHandle;

        public bool IsOpen => _ui != null;

        private const int   GridColumns   = 2;
        private const float TileHeightPx  = 210f;
        private const float TileGapPx     = 12f;
        private const float ButtonFadeSec = 0.12f;   // hover/press transition — never snap

        // ── Registration (mirror HeroSkillTreePanelMvvm) ──────────────────────────

        private void Awake()
        {
            _panelHandle = PanelManager.Register("Building Enhancements", Close, () => IsOpen);
            PanelRouter.Register(PanelId.BuildingUpgrade, OpenGeneric);
            PanelRouter.Register(PanelId.BuildingUpgrade, (System.Action<string>)Open);
        }

        private void OnDestroy()
        {
            Unbind();
            _vm?.Dispose();
            _vm = null;
            if (_ui != null) Destroy(_ui);
            _ui = null;
            PanelRouter.Unregister(PanelId.BuildingUpgrade, OpenGeneric);
            PanelRouter.Unregister(PanelId.BuildingUpgrade, (System.Action<string>)Open);
        }

        // PanelRouter plain (no-context) open — the VM resolves the default building
        // (View-side catalog reads removed per the audit §3.1).
        private void OpenGeneric() => Open(null);

        // ── Open: construct + bind the VM, build chrome ───────────────────────────

        public void Open(string buildingId)
        {
            Close();

            // VM FIRST — it resolves the default building + economy handle itself
            // (BuildingUpgradeVM.CreateDefault), so this View never touches a service,
            // and the chrome's title composes ONCE from the live building name.
            _vm = BuildingUpgradeVM.CreateDefault(buildingId, Close);

            BuildChrome();

            Bind(_vm);

            // Arbiter closes any other open panel first (DEF-212) + applies the battle-lock.
            if (!PanelManager.NotifyOpened(_panelHandle))
            {
                // Rejected (e.g. in battle) — NotifyOpened already invoked our Close.
                return;
            }
        }

        // ── IPanelView ────────────────────────────────────────────────────────────

        public void Bind(IPanelViewModel vm)
        {
            Unbind();
            _vm = vm as BuildingUpgradeVM;
            if (_vm == null) return;
            _vm.Changed += Render;
            Render();
        }

        public void Unbind()
        {
            if (_vm != null) _vm.Changed -= Render;
        }

        // ── Render: repaint from vm.* ONLY ────────────────────────────────────────

        private void Render()
        {
            if (_vm == null) return;

            if (_walletText != null)
                _walletText.text = $"Wood: {_vm.Wood}   Food: {_vm.Food}   Iron: {_vm.Iron}   Crystals: {_vm.Crystals}   Gold: {_vm.Coins}";

            if (_statusText != null) _statusText.text = _vm.Status;

            RebuildGrid();
        }

        private void RebuildGrid()
        {
            ClearContent();

            var gridRoot = BuildScrollContent();
            foreach (var item in _vm.Perks)
                CreateTile(gridRoot, item);
            FinalizeScroll();
        }

        // ── Chrome — MASTER FRAME ONLY (UI_BLINK_TEMPLATE_CANON §2-§4) ────────────
        // BuildObsidianPanel(FrameCore) supplies frame + header title + the ONE shared
        // Close. This View drops chrome-less content into the returned drop-zones:
        //   layout.header -> title (pre-built), layout.body -> perk grid + status,
        //   layout.footer -> wallet strip. No per-screen cards/wells/rims.

        private void BuildChrome()
        {
            _ui = ElarionUiKit.BuildModalCanvas("BuildingUpgradePanelMvvmUI", 31000);
            var canvas = _ui.GetComponent<Canvas>();
            if (canvas != null) canvas.overrideSorting = true;
            ElarionUiKit.Scrim(_ui.transform, onTapClose: () => _vm?.Close());

            string titleText = (_vm != null ? _vm.Title : "Building") + " Enhancements";
            // PORTRAIT sizing (UI review 04): Core_Panel is a PORTRAIT frame (~1210x1815). Anchor the
            // panel to a narrow, tall center column so the rendered aspect matches the template instead
            // of stretching the ornate frame into a landscape slab.
            var chrome = ElarionUiKit.BuildObsidianPanel(_ui.transform, titleText,
                new Vector2(0.33f, 0.05f), new Vector2(0.67f, 0.95f), () => _vm?.Close(),
                frameName: RpgUiCatalog.FrameCore, medallionIcon: "hammer");

            // Zones: frame path returns layout; procedural fallback (art absent) does not —
            // synthesize an equivalent body zone over chrome.content so the screen never blanks.
            RectTransform body = chrome.layout != null
                ? chrome.layout.body
                : MakeZone(chrome.content.transform, "Zone_Body", new Vector2(0.04f, 0.075f), new Vector2(0.96f, 0.855f));

            // Smooth the shared Close button's tint transition too.
            SoftenButton(chrome.close);

            // BODY zone: perk grid (scrolling) above the wallet + a thin status line. The wallet rides
            // the dark well's BASE — NOT the frame's clipped bottom-filigree footer band, which squashed
            // + dimmed the resource line at the very edge (UI review 04). Bright gilt + auto-size so the
            // full "Wood/Food/Iron/Crystals/Gold" line stays legible + un-clipped in the narrow column.
            _contentRoot = MakeZone(body, "GridHost", new Vector2(0f, 0.135f), new Vector2(1f, 1f)).gameObject;

            _walletText = MakeLine(body, "Wallet", ElarionUi.Gilt, ElarionUi.FontLabel,
                new Vector2(0f, 0.065f), new Vector2(1f, 0.128f));
            // §1.14: bounded fit + Truncate (keeps the authored 10px min) so the wallet
            // line can never overflow its strip onto the status line below.
            ElarionUiKit.FitBlock(_walletText, 10f, ElarionUi.FontLabel);

            _statusText = MakeLine(body, "Status", ElarionUi.ParchmentDim, ElarionUi.FontLabel,
                new Vector2(0f, 0f), new Vector2(1f, 0.06f));
            ElarionUiKit.FitSingleLine(_statusText);

            // Eased open (owner smoothness directive): scale 0.92->1 + fade 0->1, ease-out.
            var fx = _ui.AddComponent<PanelOpenCloseFx>();
            fx.PlayOpen(chrome.root != null ? chrome.root.transform as RectTransform : null);
        }

        private static RectTransform MakeZone(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = min; rt.anchorMax = max;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return rt;
        }

        private static TMPro.TextMeshProUGUI MakeLine(Transform parent, string name, Color color,
            float fontSize, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(TMPro.TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = min; rt.anchorMax = max;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var t = go.GetComponent<TMPro.TextMeshProUGUI>();
            t.fontSize = fontSize;
            t.color = color;
            t.alignment = TMPro.TextAlignmentOptions.Center;
            t.raycastTarget = false;
            return t;
        }

        // ── Scroll grid (GridLayoutGroup; anti-collapse via ContentSizeFitter) ────

        private Transform BuildScrollContent()
        {
            var viewport = new GameObject("Viewport", typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            viewport.transform.SetParent(_contentRoot.transform, false);
            var vr = viewport.GetComponent<RectTransform>();
            vr.anchorMin = Vector2.zero; vr.anchorMax = Vector2.one;
            vr.offsetMin = Vector2.zero; vr.offsetMax = Vector2.zero;
            var vImg = viewport.GetComponent<Image>();
            vImg.color = new Color(0f, 0f, 0f, 0.001f);

            var content = new GameObject("ScrollContent", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            var cr = content.GetComponent<RectTransform>();
            cr.anchorMin = new Vector2(0f, 1f);
            cr.anchorMax = new Vector2(1f, 1f);
            cr.pivot = new Vector2(0.5f, 1f);
            cr.anchoredPosition = Vector2.zero;
            cr.sizeDelta = new Vector2(0f, 0f);

            _grid = content.AddComponent<GridLayoutGroup>();
            _grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            _grid.constraintCount = GridColumns;
            _grid.spacing = new Vector2(TileGapPx, TileGapPx);
            _grid.padding = new RectOffset(4, 4, 4, 4);
            _grid.childAlignment = TextAnchor.UpperCenter;
            _grid.cellSize = new Vector2(300f, TileHeightPx);   // corrected in FinalizeScroll

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
            // Size the grid cells from the REAL content width (2 columns fill it).
            if (_grid != null)
            {
                float w = _scrollContent.rect.width;
                if (w > 1f)
                {
                    float cell = (w - _grid.padding.horizontal - TileGapPx * (GridColumns - 1)) / GridColumns;
                    _grid.cellSize = new Vector2(cell, TileHeightPx);
                }
            }
            var contentArea = _contentRoot != null ? _contentRoot.transform as RectTransform : null;
            if (contentArea != null) LayoutRebuilder.ForceRebuildLayoutImmediate(contentArea);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_scrollContent);
        }

        // ── Perk TILE (presentation; data from the bound ItemVM) ──────────────────
        // Kit slot plate (RpgUiCatalog RoleSlot / SlotItem) so empty/locked tiers still
        // read as a grid. Layout inside the tile (fraction anchors):
        //   icon (top center) / name / effect one-liner / cost-or-requirement line.
        // States: owned = lit (gilt tint + "UNLOCKED"), next = gold outline affordance,
        // locked = dimmed plate + requirement line. Tap = _vm.Select(id) (unlock).

        private void CreateTile(Transform parent, ItemVM item)
        {
            var tile = new GameObject("PerkTile_" + item.Id, typeof(Image), typeof(Button));
            tile.transform.SetParent(parent, false);

            var plate = tile.GetComponent<Image>();
            DressTilePlate(plate);
            if (item.Equipped)
            {
                var c = plate.color;
                plate.color = new Color(c.r * OwnedTint.r, c.g * OwnedTint.g, c.b * OwnedTint.b, c.a);
            }
            else if (item.Locked)
            {
                var c = plate.color;
                plate.color = new Color(c.r * LockedTint.r, c.g * LockedTint.g, c.b * LockedTint.b, c.a * LockedTint.a);
            }

            var btn = tile.GetComponent<Button>();
            btn.targetGraphic = plate;
            ElarionUiKit.StyleButtonColors(btn);
            SoftenButton(btn);
            btn.interactable = !item.Locked && !item.Equipped;
            // Owned/locked tiles are non-actionable -> drop the Selectable transition so they
            // show no hover/selection highlight (they read as settled state, not a CTA).
            if (!btn.interactable) btn.transition = Selectable.Transition.None;
            string id = item.Id;
            btn.onClick.AddListener(() => _vm?.Select(id));

            // GOLD AFFORDANCE — the unlockable-now tile carries a gold outline glow.
            if (btn.interactable && item.Affordable)
            {
                var outline = tile.AddComponent<Outline>();
                outline.effectColor = ElarionUiKit.ObsidianTrim;
                outline.effectDistance = new Vector2(3f, 3f);
            }

            float dim = item.Locked ? 0.55f : 1f;

            // ICON — perk sprite (WO-432 <Building>_T1_<Perk> art) or, for tier/villagetier
            // tiles with no art, a numeral/crest glyph so the grid stays uniform.
            Sprite icon = null;
            if (item.IconRole == BuildingUpgradeVM.IconRolePerk && !string.IsNullOrEmpty(item.IconName))
                icon = Resources.Load<Sprite>("HudIcons/BuildingUpgrades/" + item.IconName);
            if (icon != null)
            {
                var iconGo = new GameObject("Icon", typeof(Image));
                iconGo.transform.SetParent(tile.transform, false);
                var irt = iconGo.GetComponent<RectTransform>();
                irt.anchorMin = new Vector2(0.32f, 0.50f); irt.anchorMax = new Vector2(0.68f, 0.94f);
                irt.offsetMin = Vector2.zero; irt.offsetMax = Vector2.zero;
                var iImg = iconGo.GetComponent<Image>();
                iImg.sprite = icon;
                iImg.preserveAspect = true;
                iImg.raycastTarget = false;
                iImg.color = new Color(1f, 1f, 1f, dim);
            }
            else
            {
                string glyph = item.Id == BuildingUpgradeVM.VillageTierRowId
                    ? ElarionUi.CrestGlyph
                    : TierGlyph(item.Id);
                var g = ElarionUiKit.Label(tile.transform, glyph, 0.50f, 0.94f,
                    new Color(ElarionUi.Gilt.r, ElarionUi.Gilt.g, ElarionUi.Gilt.b, dim),
                    ElarionUi.FontTitle, TMPro.TextAlignmentOptions.Center, 0.05f, 0.95f, bold: true);
                g.raycastTarget = false;
                ElarionUiKit.FitSingleLine(g);
            }

            // NAME.
            var nameLbl = ElarionUiKit.Label(tile.transform, item.Name, 0.345f, 0.50f,
                new Color(ElarionUi.Parchment.r, ElarionUi.Parchment.g, ElarionUi.Parchment.b, dim),
                ElarionUi.FontBody, TMPro.TextAlignmentOptions.Center, 0.04f, 0.96f, bold: true);
            nameLbl.raycastTarget = false;
            // §1.14 (eyes-sweep 2026-07-06): each tile label OWNS its strip. A long perk
            // name used to wrap past its band and paint over the effect + cost lines
            // ("Unlock Village" / "Opens tier-2 enhancements" / "500 Crystals" stack).
            // Titles fit single-line (bounded auto-size, then ellipsis) — never spill.
            ElarionUiKit.FitSingleLine(nameLbl);

            // EFFECT — the one-line concrete payoff, from the perk data (VM-relayed).
            string effect = _vm != null ? _vm.EffectFor(item.Id) : "";
            var effLbl = ElarionUiKit.Label(tile.transform, effect, 0.20f, 0.345f,
                new Color(ElarionUi.Gilt.r, ElarionUi.Gilt.g, ElarionUi.Gilt.b, 0.85f * dim),
                ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Center, 0.04f, 0.96f);
            effLbl.raycastTarget = false;
            // Descriptions wrap INSIDE their band and truncate — never onto siblings.
            ElarionUiKit.FitBlock(effLbl);

            // BOTTOM LINE — owned: "UNLOCKED"; locked: the requirement; else the COST.
            string bottom;
            Color bottomColor;
            if (item.Equipped)
            {
                bottom = "UNLOCKED";
                bottomColor = ElarionUi.Gilt;
            }
            else if (item.Locked)
            {
                bottom = !string.IsNullOrEmpty(item.LockReason) ? item.LockReason : "Locked";
                bottomColor = ElarionUi.ParchmentDim;
            }
            else
            {
                bottom = _vm != null ? _vm.CostFor(item.Id) : "";
                bottomColor = item.Affordable ? ElarionUi.Affordable : ElarionUi.Danger;
                // Colorblind law: affordability was encoded by green-vs-red hue ALONE —
                // add a text cue ("Need ...") so the unaffordable state reads without hue.
                if (!item.Affordable && !string.IsNullOrEmpty(bottom)) bottom = "Need " + bottom;
            }
            var botLbl = ElarionUiKit.Label(tile.transform, bottom, 0.05f, 0.20f,
                new Color(bottomColor.r, bottomColor.g, bottomColor.b, dim),
                ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Center, 0.04f, 0.96f, bold: !item.Locked);
            botLbl.raycastTarget = false;
            // Cost/"UNLOCKED" fit single-line; a locked tile's requirement copy may run
            // long — let it wrap-and-truncate INSIDE its band instead of ellipsizing away.
            if (item.Locked) ElarionUiKit.FitBlock(botLbl);
            else ElarionUiKit.FitSingleLine(botLbl);
        }

        private static string TierGlyph(string id)
        {
            // "tier-3" -> "III"-style numeral read; fall back to the raw digit.
            int dash = id != null ? id.LastIndexOf('-') : -1;
            string n = dash >= 0 && dash < id.Length - 1 ? id.Substring(dash + 1) : "";
            return string.IsNullOrEmpty(n) ? "◆" : n;
        }

        private static void DressTilePlate(Image plateImg)
        {
            if (plateImg == null) return;
            if (DeNelle.Core.FeatureFlags.BlinkChrome)
            {
                var plate = RpgUiCatalog.Get(RpgUiCatalog.RoleSlot, RpgUiCatalog.SlotItem);
                if (plate != null)
                {
                    plateImg.sprite = plate;
                    plateImg.type   = Image.Type.Sliced;
                    plateImg.color  = Color.white;
                    return;
                }
            }
            plateImg.color = ElarionUiKit.Cell;
            ElarionUiKit.ApplyRounded(plateImg);
        }

        // Smooth hover/press: keep the kit ColorTint block but give it a real fade
        // (never snap) — owner smoothness directive 2026-07-02.
        private static void SoftenButton(Button btn)
        {
            if (btn == null || btn.transition != Selectable.Transition.ColorTint) return;
            var colors = btn.colors;
            colors.fadeDuration = ButtonFadeSec;
            btn.colors = colors;
        }

        // ── Teardown ──────────────────────────────────────────────────────────────

        private void ClearContent()
        {
            _scrollContent = null;
            _grid = null;
            if (_contentRoot == null) return;
            for (int i = _contentRoot.transform.childCount - 1; i >= 0; i--)
            {
                var c = _contentRoot.transform.GetChild(i);
                if (c != null) Destroy(c.gameObject);
            }
        }

        private void Close()
        {
            Unbind();
            _vm?.Dispose();
            _vm = null;
            _walletText = null;
            _statusText = null;
            if (_ui != null)
            {
                // Eased close (owner smoothness directive): the dying canvas fades/scales out on
                // its own FX component, then destroys itself — panel state is already cleared, so
                // an immediate re-Open builds a fresh canvas without waiting.
                var fx = _ui.GetComponent<PanelOpenCloseFx>();
                if (fx != null && fx.isActiveAndEnabled) fx.PlayCloseAndDestroy();
                else Destroy(_ui);
            }
            _ui = null;
            _contentRoot = null;
            _scrollContent = null;
            _grid = null;
            PanelManager.NotifyClosed(_panelHandle);
        }
    }

    /// <summary>
    /// Minimal shared open/close tween for THIS panel family (no kit tween exists yet —
    /// flagged for promotion into ElarionUiKit). Ease-out scale 0.92-&gt;1 + fade-in on open
    /// (~0.18s); ease-in fade/scale-out then self-destroy on close (~0.14s). Unscaled time
    /// (panels open while gameplay may be paused); CanvasGroup blocks input while closing.
    /// </summary>
    internal sealed class PanelOpenCloseFx : MonoBehaviour
    {
        private const float OpenSec  = 0.18f;
        private const float CloseSec = 0.14f;

        private CanvasGroup _group;
        private RectTransform _scaled;
        private bool _closing;

        public void PlayOpen(RectTransform scaleTarget)
        {
            _group = gameObject.GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
            _scaled = scaleTarget;
            _group.alpha = 0f;
            if (_scaled != null) _scaled.localScale = Vector3.one * 0.92f;
            StartCoroutine(Ease(open: true, OpenSec, onDone: null));
        }

        public void PlayCloseAndDestroy()
        {
            if (_closing) return;
            _closing = true;
            if (_group == null) _group = gameObject.GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
            _group.interactable = false;
            _group.blocksRaycasts = false;
            StartCoroutine(Ease(open: false, CloseSec, onDone: () => Destroy(gameObject)));
        }

        private IEnumerator Ease(bool open, float duration, System.Action onDone)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float x = Mathf.Clamp01(t / duration);
                // open = ease-OUT cubic; close = ease-IN cubic (owner-specified feel).
                float k = open ? 1f - Mathf.Pow(1f - x, 3f) : 1f - Mathf.Pow(x, 3f);
                if (_group != null) _group.alpha = k;
                // open: k 0->1 grows 0.92->1; close: k 1->0 shrinks 1->0.94 (panel rect, not
                // the canvas root — scale on an overlay canvas root does not render).
                if (_scaled != null)
                    _scaled.localScale = Vector3.one * Mathf.Lerp(open ? 0.92f : 0.94f, 1f, k);
                yield return null;
            }
            if (_group != null) _group.alpha = open ? 1f : 0f;
            if (_scaled != null && open) _scaled.localScale = Vector3.one;
            onDone?.Invoke();
        }
    }
}
