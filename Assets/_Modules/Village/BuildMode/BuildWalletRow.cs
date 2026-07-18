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
// Amounts come from the shared WalletVM DTO produced by LiveWalletSource (MVVM
// Silo C) — this View no longer reads the economy/state services directly. The
// source owns the live subscriptions (the in-session Wood/Iron pools + the
// GameState-backed Food/Crystals/Coins) and raises Changed; the row rebinds its
// chips off the DTO.
// Meaning is carried by the LETTER badge + number, never colour alone (owner is
// red/green colourblind). ASCII-only TMP; code-built uGUI on the kit; ZERO UXML.
// =============================================================================

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
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

        // MVVM Silo C — the live wallet DTO producer (owns the service subscriptions).
        // This View binds its Changed event and reads Wallet; it never names a service.
        private LiveWalletSource _wallet;

        /// <summary>Build the chip row into <paramref name="parent"/> (a left-anchored band).</summary>
        public void Build(Transform parent)
        {
            if (_built) return;
            _built = true;

            // Resolve the live wallet source (the sole resolution site) + bind its updates.
            if (_wallet == null) _wallet = LiveWalletSource.CreateDefault();
            _wallet.Changed -= Refresh;
            _wallet.Changed += Refresh;

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

            // Order + letter badges come from the DTO (Wood, Iron, Food, Crystals, Gold).
            // The badge is the colour-free tell (owner is red/green colourblind).
            foreach (var entry in _wallet.Wallet.Entries)
                Chip(rowGo.transform, entry.CurrencyId, entry.IconName);

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

        /// <summary>Re-read the live wallet DTO and update every chip's amount (compact >= 10k).</summary>
        public void Refresh()
        {
            if (_wallet == null) return;
            foreach (var entry in _wallet.Wallet.Entries)
                Set(entry.CurrencyId, entry.Amount);
        }

        private void Set(string key, int value)
        {
            if (_amountLabels.TryGetValue(key, out var label) && label != null)
                label.text = ElarionUi.CompactNumber(value);
        }

        private void OnEnable()
        {
            // Rebind the live source (idempotent) so a re-enabled row stays live.
            if (_wallet != null)
            {
                _wallet.Changed -= Refresh;
                _wallet.Changed += Refresh;
            }
            if (_built) Refresh();
        }

        private void OnDisable()
        {
            if (_wallet != null) _wallet.Changed -= Refresh;
        }

        private void OnDestroy()
        {
            if (_wallet != null) { _wallet.Changed -= Refresh; _wallet.Dispose(); _wallet = null; }
        }

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
