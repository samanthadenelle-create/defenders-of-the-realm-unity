// =============================================================================
// JewelerPanelMvvm — the jeweler jewelry-crafting VIEW (MVVM slice). A DUMB SKIN:
// it builds the SHARED Obsidian chrome (black + gold trim + ONE Close) and BINDS a
// JewelerVM. ALL state/logic (recipes, base/gem have-need, can-craft, craft command)
// lives in the VM — the View never reads game state.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Items
//
// Code-built uGUI ONLY (no UXML — §8). Mirrors CraftingPanelMvvm exactly. Layout:
//   * Header: "Jeweler's Bench" + a one-line hint.
//   * A grid of recipe cards (3 columns). Each card:
//       - output (upgraded) jewelry icon + gilt name (top)
//       - a base + gem checklist: "Name  have/need", green when met / red when short
//       - a small cost line (iron/crystals) when the recipe has a wallet cost
//       - a Craft button ("Set Gems") that DIMS (non-interactable) when CanCraft == false
//
// Registers PanelId.JewelerCrafting. Spawned by JewelerPanelBootstrap once a hero exists.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DeNelle.Core.UI;
using DeNelle.Core.UI.Mvvm;

namespace DeNelle.Village.Items
{
    [DisallowMultipleComponent]
    public sealed class JewelerPanelMvvm : MonoBehaviour, IPanelView
    {
        private JewelerVM _vm;

        private GameObject _ui;
        private GameObject _contentRoot;          // the cards host (rebuilt on Render)
        private TMPro.TextMeshProUGUI _headerLabel;

        private PanelHandle _panelHandle;

        public bool IsOpen => _ui != null;

        // ── Registration (mirror CraftingPanelMvvm) ───────────────────────────────

        private void Awake()
        {
            _panelHandle = PanelManager.Register("Jeweler's Bench", Close, () => IsOpen);
            PanelRouter.Register(PanelId.JewelerCrafting, Open);
        }

        private void OnDestroy()
        {
            Unbind();
            if (_vm != null) { _vm.Dispose(); _vm = null; }
            if (_ui != null) Destroy(_ui);
            _ui = null;
            PanelRouter.Unregister(PanelId.JewelerCrafting, Open);
        }

        // ── Open: build chrome, construct + bind the VM ───────────────────────────

        public void Open()
        {
            Close();
            BuildChrome();

            _vm = new JewelerVM(Close);
            Bind(_vm);

            if (!PanelManager.NotifyOpened(_panelHandle))
                return; // rejected (e.g. in battle) — NotifyOpened already invoked Close.

            Debug.Log("[JewelerPanelMvvm] Opened. Bound JewelerVM (MVVM).");
        }

        // ── IPanelView ────────────────────────────────────────────────────────────

        public void Bind(IPanelViewModel vm)
        {
            Unbind();
            _vm = vm as JewelerVM;
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
            RebuildCards();
        }

        // ── Cards grid (3 columns; rows sized to recipe count) ────────────────────

        private const int GridCols = 3;

        private void RebuildCards()
        {
            ClearContent();
            if (_contentRoot == null || _vm == null) return;

            var recipes = _vm.Recipes;
            int n = recipes != null ? recipes.Count : 0;
            if (n == 0)
            {
                ElarionUiKit.Label(_contentRoot.transform,
                    "No jewelry recipes available.",
                    0.45f, 0.55f, ElarionUi.ParchmentDim, ElarionUi.FontBody,
                    TMPro.TextAlignmentOptions.Center, 0.05f, 0.95f);
                return;
            }

            int rows = Mathf.CeilToInt(n / (float)GridCols);
            float colGap = 0.02f, rowGap = 0.025f;
            float colW = (1f - colGap * (GridCols - 1)) / GridCols;
            float rowH = (1f - rowGap * (rows - 1)) / rows;

            for (int i = 0; i < n; i++)
            {
                int col = i % GridCols;
                int row = i / GridCols;
                float x0 = col * (colW + colGap);
                float x1 = x0 + colW;
                // Top-down: row 0 at the top.
                float y1 = 1f - row * (rowH + rowGap);
                float y0 = y1 - rowH;
                BuildRecipeCard(_contentRoot.transform, recipes[i], x0, x1, y0, y1);
            }
        }

        // ── One recipe card (presentation; data from the bound JewelerRecipeVM) ───

        private void BuildRecipeCard(Transform parent, JewelerRecipeVM recipe, float x0, float x1, float y0, float y1)
        {
            var card = ElarionUiKit.AddImage(parent, "Recipe_" + recipe.RecipeId,
                new Vector2(x0, y0), new Vector2(x1, y1),
                new Color(ElarionUiKit.Cell.r, ElarionUiKit.Cell.g, ElarionUiKit.Cell.b, 0.55f));
            var cardImg = card.GetComponent<Image>();
            if (cardImg != null)
            {
                cardImg.raycastTarget = false;
                // Obsidian item-plate standard (matches EquipmentPanel / CraftingPanelMvvm cells):
                // dress sprite-FIRST with the RpgUiCatalog per-item slot plate; procedural Cell tint
                // stays as the WebGL-safe fallback when pack art is absent.
                var plate = RpgUiCatalog.Get(RpgUiCatalog.RoleSlot, RpgUiCatalog.SlotItem);
                if (plate != null)
                {
                    cardImg.sprite = plate;
                    cardImg.type   = Image.Type.Sliced;
                    cardImg.color  = Color.white;
                }
            }
            ElarionUiKit.AddInnerRim(card, ElarionUiKit.AccentSoft);

            var t = card.transform;

            // Output (upgraded) icon (top-center).
            var outSprite = LoadIcon(recipe.OutputIconPath);
            if (outSprite != null)
            {
                var iconGo = new GameObject("OutIcon", typeof(Image));
                iconGo.transform.SetParent(t, false);
                var ir = iconGo.GetComponent<RectTransform>();
                ir.anchorMin = new Vector2(0.40f, 0.78f); ir.anchorMax = new Vector2(0.60f, 0.98f);
                ir.offsetMin = Vector2.zero; ir.offsetMax = Vector2.zero;
                var iImg = iconGo.GetComponent<Image>();
                iImg.sprite = outSprite;
                iImg.preserveAspect = true;
                iImg.raycastTarget = false;
            }

            // Output name (gilt).
            ElarionUiKit.Label(t, recipe.OutputName, 0.69f, 0.78f,
                ElarionUi.Gilt, ElarionUi.FontBody, TMPro.TextAlignmentOptions.Center,
                0.04f, 0.96f, bold: true);

            // Base + gem checklist (stacked between y 0.26 and 0.66).
            var lines = recipe.Ingredients;
            int li = lines != null ? lines.Count : 0;
            float top = 0.66f, bot = 0.26f;
            float bandH = li > 0 ? (top - bot) / li : (top - bot);
            for (int k = 0; k < li; k++)
            {
                var ing = lines[k];
                float ly1 = top - k * bandH;
                float ly0 = ly1 - bandH + 0.01f;
                Color c = ing.Met ? ElarionUi.Affordable : ElarionUi.Danger;
                string mark = ing.Met ? "+" : "-";
                ElarionUiKit.Label(t, mark + " " + ing.Name + "   " + ing.Have + "/" + ing.Need,
                    ly0, ly1, c, ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Left,
                    0.07f, 0.95f);
            }

            // Cost line (iron/crystals) — only when the recipe has a wallet cost.
            if (!string.IsNullOrEmpty(recipe.CostLabel))
            {
                ElarionUiKit.Label(t, recipe.CostLabel, 0.175f, 0.245f,
                    ElarionUi.ParchmentDim, ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Center,
                    0.06f, 0.94f);
            }

            // Craft button (dims when not craftable).
            var btn = ElarionUiKit.ButtonPack(t,
                recipe.CanCraft ? "Set Gems" : "Need Gems",
                recipe.CanCraft ? ElarionUiKit.ButtonKind.Gold : ElarionUiKit.ButtonKind.Quiet,
                new Vector2(0.10f, 0.025f), new Vector2(0.90f, 0.155f),
                () => { if (_vm != null) _vm.Craft(recipe.RecipeId); });
            if (btn != null)
            {
                btn.interactable = recipe.CanCraft;
                var lbl = btn.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                if (lbl != null)
                {
                    lbl.color = recipe.CanCraft ? ElarionUi.Parchment : ElarionUi.ParchmentDim;
                    lbl.fontStyle = TMPro.FontStyles.Bold;
                }
            }
        }

        // ── Chrome (presentation only; SHARED Obsidian chrome, WO-554) ────────────

        private void BuildChrome()
        {
            _ui = ElarionUiKit.BuildModalCanvas("JewelerPanelMvvmUI", 31000);
            var canvas = _ui.GetComponent<Canvas>();
            if (canvas != null) canvas.overrideSorting = true;
            ElarionUiKit.Scrim(_ui.transform, onTapClose: () => { if (_vm != null) _vm.Close(); });

            // SHARED Obsidian chrome: black panel + gold trim + gold header + ONE Close.
            var chrome = ElarionUiKit.BuildObsidianPanel(_ui.transform, "Jeweler's Bench",
                new Vector2(0.07f, 0.05f), new Vector2(0.93f, 0.95f), () => { if (_vm != null) _vm.Close(); },
                headerX0: 0.04f, headerX1: 0.96f, frameName: RpgUiCatalog.FrameCrafting,
                medallionIcon: "gem");
            var panel = chrome.content.transform;
            _headerLabel = chrome.title;

            // WO-582: fit content into the frame's BODY drop-zone (the templated area) instead of
            // floating over the whole panel rect — this stops the old card host from overlapping the
            // frame's ornate border. Falls back to the panel rect when no frame is used. Content then
            // uses fractions INSIDE the body zone (mirrors CraftingPanelMvvm).
            var bodyHost = (chrome.layout != null && chrome.layout.body != null)
                ? chrome.layout.body : (RectTransform)panel;

            // One-line hint at the top of the body zone.
            ElarionUiKit.Label(bodyHost, "Set gems into a ring or amulet to forge a finer piece.",
                0.92f, 1.0f, ElarionUi.ParchmentDim, ElarionUi.FontLabel,
                TMPro.TextAlignmentOptions.Center, 0.02f, 0.98f);

            // Cards host — fills the body zone (below the hint).
            _contentRoot = new GameObject("Cards", typeof(RectTransform));
            _contentRoot.transform.SetParent(bodyHost, false);
            var cr = _contentRoot.GetComponent<RectTransform>();
            cr.anchorMin = new Vector2(0.0f, 0.0f); cr.anchorMax = new Vector2(1.0f, 0.90f);
            cr.offsetMin = Vector2.zero; cr.offsetMax = Vector2.zero;

            // Close is the SHARED top-right Obsidian Close button (WO-554) — no per-panel footer Close.
        }

        // Icon cache — Resources.Load is cheap but cached avoids reloading every Render.
        private static readonly Dictionary<string, Sprite> s_iconCache = new Dictionary<string, Sprite>();
        private static Sprite LoadIcon(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (s_iconCache.TryGetValue(path, out var cached)) return cached;
            Sprite sp = Resources.Load<Sprite>(path);
            s_iconCache[path] = sp;   // cache nulls too (atlas not sliced yet) so we don't retry each frame
            return sp;
        }

        // ── Teardown ──────────────────────────────────────────────────────────────

        private void ClearContent()
        {
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
            if (_vm != null) { _vm.Dispose(); _vm = null; }
            _headerLabel = null;
            if (_ui != null) Destroy(_ui);
            _ui = null;
            _contentRoot = null;
            PanelManager.NotifyClosed(_panelHandle);
        }
    }
}
