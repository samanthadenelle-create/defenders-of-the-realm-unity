// =============================================================================
// BuildWalletRow — the Build HUD resource strip (Grok slice 3).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// A left-aligned row of resource chips for the Build HUD top bar — Wood / Iron /
// Food / Crystals (+ Gold), each an ASCII letter badge + the compact amount
// (ElarionUi.CompactNumber). Replaces the crystals-only header the palette used
// to show (Grok reuse ledger: "Resource strip -> BuildWalletRow all pools").
//
// Amounts come from EconomyService (Wood/Iron are in-session; Food/Crystals/Coins
// are GameState-backed). Refresh is driven by both GameStateService.ResourcesChanged
// (Food/Crystals/Coins mutations) and EconomyService.OnChanged (Wood/Iron in-session
// mutations) so every pool stays live. Meaning is carried by the LETTER badge +
// number, never colour alone (owner is red/green colourblind). ASCII-only TMP;
// code-built uGUI on the kit; ZERO UXML.
// =============================================================================

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DeNelle.Core.State;
using DeNelle.Core.UI;

namespace DeNelle.Village
{
    /// <summary>
    /// Build HUD resource chips (Wood/Iron/Food/Crystals/Gold). <see cref="Build"/>
    /// populates a parent band; <see cref="Refresh"/> re-reads the live wallet.
    /// </summary>
    public sealed class BuildWalletRow : MonoBehaviour
    {
        // Chip geometry (NAMED, not the kit MinTouchPx floor — read-outs, not buttons).
        private const float ChipWidthPx = 150f;
        private const float ChipHeightPx = 64f;
        private const float ChipSpacingPx = 10f;

        private readonly Dictionary<string, TextMeshProUGUI> _amountLabels =
            new Dictionary<string, TextMeshProUGUI>();
        private bool _built;

        /// <summary>Build the chip row into <paramref name="parent"/> (a left-anchored band).</summary>
        public void Build(Transform parent)
        {
            if (_built) return;
            _built = true;

            var rowGo = new GameObject("WalletChips",
                typeof(RectTransform), typeof(HorizontalLayoutGroup));
            rowGo.transform.SetParent(parent, false);
            var rrt = (RectTransform)rowGo.transform;
            rrt.anchorMin = new Vector2(0f, 0.5f);
            rrt.anchorMax = new Vector2(0f, 0.5f);
            rrt.pivot = new Vector2(0f, 0.5f);
            rrt.anchoredPosition = new Vector2(24f, 0f);
            rrt.sizeDelta = new Vector2(0f, ChipHeightPx);
            var layout = rowGo.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = ChipSpacingPx;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.MiddleLeft;

            // Order: Wood, Iron, Food, Crystals, Gold. Letter badges are the colour-free tell.
            Chip(rowGo.transform, "wood",     "W");
            Chip(rowGo.transform, "iron",     "I");
            Chip(rowGo.transform, "food",     "F");
            Chip(rowGo.transform, "crystals", "C");
            Chip(rowGo.transform, "gold",     "G");

            Refresh();
        }

        private void Chip(Transform parent, string key, string badge)
        {
            var chipGo = new GameObject("Chip_" + key,
                typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            chipGo.transform.SetParent(parent, false);
            var crt = (RectTransform)chipGo.transform;
            crt.sizeDelta = new Vector2(ChipWidthPx, ChipHeightPx);
            var le = chipGo.GetComponent<LayoutElement>();
            le.preferredWidth = ChipWidthPx;
            le.preferredHeight = ChipHeightPx;

            var bg = chipGo.GetComponent<Image>();
            bg.color = ElarionUiKit.ObsidianFill;   // near-black (WO-562 — do not lighten)
            bg.raycastTarget = false;
            ElarionUiKit.ApplyRounded(bg);

            // Letter badge (the colour-free identity of the pool) left, amount right.
            var badgeLabel = MakeText(chipGo.transform, badge, 22, ElarionUi.Gilt,
                FontStyles.Bold, TextAlignmentOptions.Center,
                new Vector2(0.04f, 0.1f), new Vector2(0.34f, 0.9f));
            badgeLabel.raycastTarget = false;

            var amount = MakeText(chipGo.transform, "0", 20, ElarionUi.Parchment,
                FontStyles.Bold, TextAlignmentOptions.Right,
                new Vector2(0.36f, 0.1f), new Vector2(0.94f, 0.9f));
            amount.raycastTarget = false;
            _amountLabels[key] = amount;
        }

        /// <summary>Re-read the live wallet and update every chip's amount (compact >= 10k).</summary>
        public void Refresh()
        {
            var econ = EconomyService.Instance;
            int wood     = econ != null ? econ.Wood : 0;
            int iron     = econ != null ? econ.Iron : 0;
            int food     = econ != null ? econ.Food : 0;
            int crystals = econ != null ? econ.Crystals : 0;
            int gold     = econ != null ? econ.Coins : 0;

            Set("wood", wood);
            Set("iron", iron);
            Set("food", food);
            Set("crystals", crystals);
            Set("gold", gold);
        }

        private void Set(string key, int value)
        {
            if (_amountLabels.TryGetValue(key, out var label) && label != null)
                label.text = ElarionUi.CompactNumber(value);
        }

        private void OnEnable()
        {
            var gs = GameStateService.Instance;
            if (gs != null)
            {
                gs.ResourcesChanged.RemoveListener(Refresh);
                gs.ResourcesChanged.AddListener(Refresh);
            }
            var econ = EconomyService.Instance;
            if (econ != null)
            {
                econ.OnChanged -= OnEconomyChanged;
                econ.OnChanged += OnEconomyChanged;
            }
            if (_built) Refresh();
        }

        private void OnDisable()
        {
            var gs = GameStateService.Instance;
            if (gs != null) gs.ResourcesChanged.RemoveListener(Refresh);
            var econ = EconomyService.Instance;
            if (econ != null) econ.OnChanged -= OnEconomyChanged;
        }

        private void OnEconomyChanged(ResourceSnapshot _) => Refresh();

        // ── uGUI helper (BuildPaletteUI/BuildSelectionUI shape) ────────────────
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
    }
}
