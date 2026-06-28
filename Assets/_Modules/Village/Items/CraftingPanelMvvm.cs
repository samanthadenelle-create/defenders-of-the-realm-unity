// =============================================================================
// CraftingPanelMvvm — the consumable-crafting (Alchemy) VIEW (MVVM slice). A DUMB
// SKIN: it builds presentation (ElarionUiKit dark-glass + gold frame / Obsidian)
// and BINDS a CraftingVM. ALL state/logic (recipes, have/need counts, can-craft,
// craft command) lives in the VM — the View never reads game state.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Items
//
// Code-built uGUI ONLY (no UXML — §8; UXML HUDs render empty in player builds).
// Mirrors HeroSkillTreePanelMvvm / HeroInventoryController construction. Layout:
//   * Header: "Alchemy" + a one-line hint.
//   * A grid of recipe cards (3 columns). Each card:
//       - output icon + output name (top)
//       - an ingredient checklist: "Name  have/need", green when met / red when short
//       - a Craft button that DIMS (non-interactable) when CanCraft == false
//
// Registers PanelId.ConsumableCrafting (SEPARATE from the gear Workshop,
// PanelId.Crafting). Spawned by CraftingPanelBootstrap once a hero exists.
// =============================================================================

using System.Collections.Generic;
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
        private GameObject _contentRoot;          // the cards host (rebuilt on Render)
        private TMPro.TextMeshProUGUI _headerLabel;

        private PanelHandle _panelHandle;

        public bool IsOpen => _ui != null;

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
                    "No recipes available. Defeat enemies to gather ingredients.",
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

        // ── One recipe card (presentation; data from the bound CraftRecipeVM) ─────

        private void BuildRecipeCard(Transform parent, CraftRecipeVM recipe, float x0, float x1, float y0, float y1)
        {
            var card = ElarionUiKit.AddImage(parent, "Recipe_" + recipe.RecipeId,
                new Vector2(x0, y0), new Vector2(x1, y1),
                new Color(ElarionUiKit.Cell.r, ElarionUiKit.Cell.g, ElarionUiKit.Cell.b, 0.55f));
            var cardImg = card.GetComponent<Image>();
            if (cardImg != null)
            {
                cardImg.raycastTarget = false;
                // Obsidian item-plate standard (matches EquipmentPanel / InventoryGrid / ShopPanel
                // item cells): dress sprite-FIRST with the RpgUiCatalog per-item slot plate so the
                // recipe cards read as one Obsidian surface. Procedural Cell tint stays as the
                // WebGL-safe fallback when the pack art is absent (LogWarning-not-error contract upheld
                // inside RpgUiCatalog). Additive — no data binding touched.
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

            // Output icon (top-center).
            var outSprite = LoadIcon(recipe.OutputIconPath);
            if (outSprite != null)
            {
                var iconGo = new GameObject("OutIcon", typeof(Image));
                iconGo.transform.SetParent(t, false);
                var ir = iconGo.GetComponent<RectTransform>();
                ir.anchorMin = new Vector2(0.40f, 0.74f); ir.anchorMax = new Vector2(0.60f, 0.97f);
                ir.offsetMin = Vector2.zero; ir.offsetMax = Vector2.zero;
                var iImg = iconGo.GetComponent<Image>();
                iImg.sprite = outSprite;
                iImg.preserveAspect = true;
                iImg.raycastTarget = false;
            }

            // Output name (gilt).
            ElarionUiKit.Label(t, recipe.OutputName, 0.63f, 0.73f,
                ElarionUi.Gilt, ElarionUi.FontBody, TMPro.TextAlignmentOptions.Center,
                0.04f, 0.96f, bold: true);

            // Ingredient checklist (stacked between y 0.20 and 0.60).
            var lines = recipe.Ingredients;
            int li = lines != null ? lines.Count : 0;
            float top = 0.60f, bot = 0.20f;
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

            // Craft button (dims when not craftable).
            var btn = ElarionUiKit.ButtonPack(t,
                recipe.CanCraft ? "Craft" : "Need Materials",
                recipe.CanCraft ? ElarionUiKit.ButtonKind.Gold : ElarionUiKit.ButtonKind.Quiet,
                new Vector2(0.10f, 0.025f), new Vector2(0.90f, 0.16f),
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

        // ── Chrome (presentation only; mirrors HeroSkillTreePanelMvvm) ────────────

        private void BuildChrome()
        {
            _ui = ElarionUiKit.BuildModalCanvas("CraftingPanelMvvmUI", 31000);
            var canvas = _ui.GetComponent<Canvas>();
            if (canvas != null) canvas.overrideSorting = true;
            ElarionUiKit.Scrim(_ui.transform, onTapClose: () => { if (_vm != null) _vm.Close(); });

            var backdrop = ElarionUiKit.AddImage(_ui.transform, "CraftBackdrop",
                Vector2.zero, Vector2.one, new Color(0.02f, 0.015f, 0.012f, 0.94f), rounded: false);
            var bdImg = backdrop.GetComponent<Image>();
            if (bdImg != null) bdImg.raycastTarget = false;

            var panelGo = ElarionUiKit.PanelFramed(_ui.transform, new Vector2(0.07f, 0.05f), new Vector2(0.93f, 0.95f),
                                                   deep: true, packSpriteName: RpgUiCatalog.PanelWindowDark);
            var panel = panelGo.transform;

            Color fillColor = new Color(0.07f, 0.055f, 0.042f, 0.985f);
            if (DeNelle.Core.FeatureFlags.BlinkChrome) fillColor.a = 0f;
            var solidFill = ElarionUiKit.AddImage(panel, "CraftSolidFill",
                new Vector2(0.025f, 0.02f), new Vector2(0.975f, 0.98f), fillColor);
            var sfImg = solidFill.GetComponent<Image>();
            if (sfImg != null) sfImg.raycastTarget = false;
            solidFill.transform.SetAsFirstSibling();

            _headerLabel = ElarionUiKit.Header(panel, "Alchemy", x0: 0.04f, x1: 0.96f, y0: 0.91f, y1: 0.975f);

            // One-line hint under the header.
            ElarionUiKit.Label(panel, "Combine ingredients dropped by enemies into potions and bombs.",
                0.85f, 0.90f, ElarionUi.ParchmentDim, ElarionUi.FontLabel,
                TMPro.TextAlignmentOptions.Center, 0.04f, 0.96f);

            // Cards host.
            _contentRoot = new GameObject("Cards", typeof(RectTransform));
            _contentRoot.transform.SetParent(panel, false);
            var cr = _contentRoot.GetComponent<RectTransform>();
            cr.anchorMin = new Vector2(0.04f, 0.15f); cr.anchorMax = new Vector2(0.96f, 0.83f);
            cr.offsetMin = Vector2.zero; cr.offsetMax = Vector2.zero;

            // Close.
            var closeBtn = ElarionUiKit.ButtonPack(panel, "Close", ElarionUiKit.ButtonKind.Quiet,
                new Vector2(0.06f, 0.04f), new Vector2(0.30f, 0.10f), () => { if (_vm != null) _vm.Close(); },
                packSpriteName: RpgUiCatalog.ButtonFrame);
            var closeLbl = closeBtn != null ? closeBtn.GetComponentInChildren<TMPro.TextMeshProUGUI>() : null;
            if (closeLbl != null)
            {
                closeLbl.color = ElarionUi.Parchment; closeLbl.fontStyle = TMPro.FontStyles.Bold;
                closeLbl.transform.SetAsLastSibling();
            }
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
