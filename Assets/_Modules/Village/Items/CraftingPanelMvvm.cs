// =============================================================================
// CraftingPanelMvvm — the consumable-crafting (Alchemy) VIEW (MVVM slice). A DUMB
// SKIN: it INHERITS the shared kit chrome (BuildObsidianPanel: FrameCrafting master-
// detail + zones + the ONE shared Close) and BINDS a CraftingVM. ALL state/logic
// (recipes, have/need counts, can-craft, craft command) lives in the VM — the View
// never reads or defines game state; it only DISPLAYS and routes commands.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Items
//
// WO-F conversion (2026-07-03): the old 3-column card grid -> the owner-ratified
// FrameCrafting MASTER-DETAIL template (matches VillageCraftingPanel/Workshop):
//   * bodyLeft  (dark well)      = recipe rows (Obsidian buttons, selected=Yellow,
//                                  right-aligned readable state "+ Ready" / "2 of 3")
//   * bodyRight (parchment well) = the WO-693 shared COMPACT DETAIL CARD
//                                  (ElarionUiKit.BuildParchmentDetailCard: icon plate +
//                                  name -> REQUIRES ingredient rows -> the ONE Craft CTA
//                                  carrying its blocker when disabled)
//   * footer — EMPTY (WO-693 deleted the tiny instruction strip; its content lives on
//                                  the CTA blocker + the empty-state explanation)
// The ONE shared Close is the chrome's (no per-panel X / close_normal / footer close).
// Mobile-first (WO-693): all detail text on the ElarionUi ladder, Fit-protected with
// ElarionUi.FontFloorMobile — never below the readable floor; met/unmet carried by
// ASCII glyph + have/need counts (colorblind law), color reinforcement only.
//
// Code-built uGUI ONLY (no UXML — §8). Registers PanelId.ConsumableCrafting
// (SEPARATE from the gear Workshop, PanelId.Crafting). Spawned by
// CraftingPanelBootstrap once a hero exists.
// =============================================================================

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DeNelle.Core.UI;
using DeNelle.Core.UI.Mvvm;

namespace DeNelle.Village.Items
{
    [DisallowMultipleComponent]
    public sealed class CraftingPanelMvvm : MonoBehaviour, IPanelView
    {
        private CraftingVM _vm;

        private GameObject _ui;
        private Transform _recipeHost;   // bodyLeft — dark list well
        private Transform _detailHost;   // bodyRight — parchment detail well
        private TextMeshProUGUI _headerLabel;

        private string _selectedRecipeId;
        private PanelHandle _panelHandle;

        public bool IsOpen => _ui != null;

        // WO-693: parchment ink lives in the kit now (ElarionUiKit.ParchmentInk*) — the
        // shared compact detail card owns all detail-pane presentation.

        // ── Registration (mirror HeroSkillTreePanelMvvm) ──────────────────────────

        private void Awake()
        {
            _panelHandle = PanelManager.Register("Alchemy", Close, () => IsOpen);
            PanelRouter.Register(PanelId.ConsumableCrafting, Open);
        }

        private void OnDestroy()
        {
            Unbind();
            if (_vm != null) { _vm.Dispose(); _vm = null; }
            if (_ui != null) Destroy(_ui);
            _ui = null;
            PanelRouter.Unregister(PanelId.ConsumableCrafting, Open);
        }

        // ── Open: build chrome, construct + bind the VM ───────────────────────────

        public void Open()
        {
            Close();
            BuildChrome();

            _vm = new CraftingVM(Close);
            Bind(_vm);

            if (!PanelManager.NotifyOpened(_panelHandle))
                return; // rejected (e.g. in battle) — NotifyOpened already invoked Close.

            Debug.Log("[CraftingPanelMvvm] Opened. Bound CraftingVM (MVVM).");
        }

        // ── IPanelView ────────────────────────────────────────────────────────────

        public void Bind(IPanelViewModel vm)
        {
            Unbind();
            _vm = vm as CraftingVM;
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
            if (_headerLabel != null) _headerLabel.text = _vm.Title;
            RebuildMasterDetail();
        }

        // ── Master-detail (recipe list left / parchment-ink detail right) ─────────

        private void RebuildMasterDetail()
        {
            if (_recipeHost == null || _detailHost == null || _vm == null) return;

            var recipes = _vm.Recipes;
            int n = recipes != null ? recipes.Count : 0;

            // Keep the selection valid (first recipe by default).
            if (n > 0 && (string.IsNullOrEmpty(_selectedRecipeId) || FindIndex(recipes) < 0))
                _selectedRecipeId = recipes[0].RecipeId;

            // Recipe rows (dark well, left).
            for (int i = _recipeHost.childCount - 1; i >= 0; i--)
                Destroy(_recipeHost.GetChild(i).gameObject);

            if (n == 0)
            {
                var none = MakeText(_recipeHost, "No recipes.\nDefeat enemies to gather ingredients.",
                    ElarionUi.FontLabel, ElarionUi.ParchmentDim, FontStyles.Italic,
                    TextAlignmentOptions.Center,
                    new Vector2(0.05f, 0.38f), new Vector2(0.95f, 0.62f));
                ElarionUiKit.FitBlock(none, ElarionUi.FontFloorMobile);
            }
            else
            {
                const float rowH = 0.105f, gap = 0.015f;
                float top = 0.98f;
                for (int i = 0; i < n; i++)
                {
                    var r = recipes[i];
                    string id = r.RecipeId;
                    bool selected = id == _selectedRecipeId;
                    string name = string.IsNullOrEmpty(r.OutputName) ? r.DisplayName : r.OutputName;
                    // WO-693: no dangling "+/-" suffix — the row carries a readable right-
                    // aligned state ("+ Ready" / "2 of 3"); selection keeps the gold rim (Yellow).
                    var rowBtn = ElarionUiKit.BuildObsidianButton(_recipeHost, name,
                        ElarionUiKit.ObsidianButtonStyle.Style1,
                        selected ? ElarionUiKit.ObsidianButtonColor.Yellow
                                 : ElarionUiKit.ObsidianButtonColor.Gray,
                        new Vector2(0.04f, top - rowH), new Vector2(0.96f, top),
                        () => { _selectedRecipeId = id; RebuildMasterDetail(); });
                    ElarionUiKit.AddRowStateSuffix(rowBtn, RowState(r),
                        r.CanCraft ? ElarionUi.Affordable : ElarionUi.ParchmentDim);
                    top -= rowH + gap;
                    if (top - rowH < 0f) break;   // bounded: never overflow the well
                }
            }

            // Detail (parchment well, right — the shared compact card, WO-693).
            for (int i = _detailHost.childCount - 1; i >= 0; i--)
                Destroy(_detailHost.GetChild(i).gameObject);

            int sel = FindIndex(recipes);
            if (sel >= 0) BuildDetail(recipes[sel]);
            else
                ElarionUiKit.BuildParchmentDetailEmpty(_detailHost, "Select a recipe to inspect",
                    "Combine ingredients dropped by enemies into potions and bombs.");
        }

        /// <summary>Readable list-row state: met -> "+ Ready"; else "met of total" progress.
        /// Glyph + counts carry the state (colorblind law); color is reinforcement only.</summary>
        private static string RowState(CraftRecipeVM r)
        {
            if (r.CanCraft) return "+ Ready";
            var lines = r.Ingredients;
            int total = lines != null ? lines.Count : 0;
            if (total == 0) return "";
            int met = 0;
            for (int i = 0; i < total; i++) if (lines[i].Met) met++;
            return met + " of " + total;
        }

        private int FindIndex(IReadOnlyList<CraftRecipeVM> recipes)
        {
            if (recipes == null || string.IsNullOrEmpty(_selectedRecipeId)) return -1;
            for (int i = 0; i < recipes.Count; i++)
                if (recipes[i].RecipeId == _selectedRecipeId) return i;
            return -1;
        }

        private void BuildDetail(CraftRecipeVM recipe)
        {
            string display = string.IsNullOrEmpty(recipe.OutputName) ? recipe.DisplayName : recipe.OutputName;
            string recipeId = recipe.RecipeId;

            // REQUIRES — "[OK|X] Name .... have / need"; glyph + counts carry the state.
            var requires = new List<ElarionUiKit.DetailCardRow>();
            var lines = recipe.Ingredients;
            for (int i = 0; i < lines.Count; i++)
            {
                var ing = lines[i];
                requires.Add(new ElarionUiKit.DetailCardRow(
                    ing.Met ? "OK" : "X",
                    ing.Name,
                    ing.Have + " / " + ing.Need,
                    ing.Met ? ElarionUiKit.DetailRowTone.Good : ElarionUiKit.DetailRowTone.Bad));
            }

            // Consumables carry no bestows/rarity/cost in their defs — those sections are
            // simply omitted by the shared card (no empty headers).
            ElarionUiKit.BuildParchmentDetailCard(_detailHost, new ElarionUiKit.DetailCardSpec
            {
                IconPath = recipe.OutputIconPath,
                Title = display,
                Requires = requires,
                RequiresHeader = "INGREDIENTS",
                CtaLabel = CtaLabel(recipe),
                CtaEnabled = recipe.CanCraft,
                OnCta = () => { if (_vm != null) _vm.Craft(recipeId); },
            });
        }

        /// <summary>The ONE action carries the blocker when disabled ("CRAFT - missing 2
        /// ingredients") — the old instruction strip's job now lives on the CTA / empty-state.</summary>
        private static string CtaLabel(CraftRecipeVM recipe)
        {
            if (recipe.CanCraft) return "CRAFT";
            int missing = 0;
            var lines = recipe.Ingredients;
            for (int i = 0; i < lines.Count; i++)
                if (!lines[i].Met) missing += lines[i].Need - lines[i].Have;
            if (missing > 0)
                return "CRAFT - missing " + missing + (missing == 1 ? " ingredient" : " ingredients");
            return "CRAFT - unavailable";   // gated (e.g. ItemDropSystem disabled)
        }

        // ── Chrome (presentation only; INHERITS the shared kit frame + zones) ─────

        private void BuildChrome()
        {
            _ui = ElarionUiKit.BuildModalCanvas("CraftingPanelMvvmUI", 31000);
            var canvas = _ui.GetComponent<Canvas>();
            if (canvas != null) canvas.overrideSorting = true;
            ElarionUiKit.Scrim(_ui.transform, onTapClose: () => { if (_vm != null) _vm.Close(); });

            // SHARED Obsidian chrome (FrameCrafting master-detail): black panel + gold trim +
            // gold header + medallion + the ONE shared Close — all built by the kit. The panel
            // adds NO chrome and NO close of its own.
            var chrome = ElarionUiKit.BuildObsidianPanel(_ui.transform, "Alchemy",
                new Vector2(0.10f, 0.08f), new Vector2(0.90f, 0.92f), () => { if (_vm != null) _vm.Close(); },
                frameName: RpgUiCatalog.FrameCrafting, medallionIcon: "potion");
            _headerLabel = chrome.title;

            // Drop into the frame's pre-split master-detail zones; fall back to the single body
            // (or the panel content) when the frame art is absent (procedural chrome path).
            var layout = chrome.layout;
            _recipeHost = layout != null && layout.bodyLeft != null
                ? (Transform)layout.bodyLeft
                : (layout != null && layout.body != null ? (Transform)layout.body : chrome.content.transform);
            _detailHost = layout != null && layout.bodyRight != null
                ? (Transform)layout.bodyRight
                : (layout != null && layout.body != null ? (Transform)layout.body : chrome.content.transform);

            // WO-693: the tiny footer instruction strip is DELETED — its content lives on
            // the CTA blocker text and the empty-state explanation (one-action law).

            // Close is the SHARED kit Close (top-right) — no per-panel close.
        }

        // ── uGUI helper (mirrors VillageCraftingPanel.MakeText) ───────────────────

        private static TextMeshProUGUI MakeText(Transform parent, string text, float size,
            Color color, FontStyles style, TextAlignmentOptions align, Vector2 min, Vector2 max)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = min; rt.anchorMax = max;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.fontStyle = style;
            t.alignment = align;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            ElarionUiKit.EnsureFont(t);
            return t;
        }

        // ── Teardown ──────────────────────────────────────────────────────────────

        private void Close()
        {
            Unbind();
            if (_vm != null) { _vm.Dispose(); _vm = null; }
            _headerLabel = null;
            _recipeHost = null;
            _detailHost = null;
            if (_ui != null) Destroy(_ui);
            _ui = null;
            PanelManager.NotifyClosed(_panelHandle);
        }
    }
}
