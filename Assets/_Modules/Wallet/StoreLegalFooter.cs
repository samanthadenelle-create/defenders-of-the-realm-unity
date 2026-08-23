// =============================================================================
// StoreLegalFooter — THE ONE OWNER OF THE STORE'S LEGAL / PROMISE COPY (UI-001).
// -----------------------------------------------------------------------------
// ⛔ WHY THIS IS A COMPONENT AND NOT FOUR MakeText CALLS INSIDE PackStore.
// The four trust claims, the market disclaimer, and the keep-out the canon Close
// carves out of the middle of the band were all authored INLINE in
// PackStore.BuildTrustStrip. That made PackStore the only surface that could ever
// print them correctly: SeasonTrackPanel already re-prints one of the same strings
// on its own (StoreStrings.KeyTrustNeverPower), with its own size and its own
// geometry, which is a second copy of a legal surface with no single owner. This
// project's standing rule is ONE OWNER PER CONCERN (CLAUDE.md §7 — the bar's single
// Queues entry, EchoWorldPresence as the Echo's single appearance owner); the legal
// band is a concern, so it gets an owner.
//
// ⛔ THE CENTRE OF THE BAND IS RESERVED AND THAT IS LOAD-BEARING. The canon Close is
// a CanonCtaWidth-wide visible button seated dead centre of this same row (UI-001 §6:
// legal LEFT, Close CENTRE, promise RIGHT — ONE 132-unit band, never two stacked).
// Any copy that reaches under it is reported by AuditGeometry rule 3 as BUTTON OVER
// TEXT and on a device is simply unreadable. The keep-out is DERIVED from
// ElarionUiKit.CanonCtaWidth, never typed as a fraction, so a future change to the
// canon button width moves the copy with it.
//
// ⛔ COLOUR NEVER CARRIES MEANING HERE. Every line reads as words at or above
// ElarionUi.FontFloorMobile; the gilt on the promise line is emphasis, not state.
// The owner is red/green colourblind — the greyscale capture is the acceptance test.
//
// Presentation only: it reads StoreStrings + PackCatalog and writes nothing.
// =============================================================================

using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DeNelle.Core.UI;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Wallet
{
    /// <summary>Handle onto the one label a caller may later refresh in place.</summary>
    public sealed class StoreLegalFooterHandle
    {
        /// <summary>The market/currency disclaimer. Refreshed when the catalogue reloads.</summary>
        public TextMeshProUGUI DisclaimerLabel;
    }

    /// <summary>
    /// The store's single legal/footer band. Call once per commerce surface.
    /// </summary>
    public static class StoreLegalFooter
    {
        /// <summary>Body size for every line in the band, reference px. At or above
        /// <see cref="ElarionUi.FontFloorMobile"/> by construction — UI-001 §8's legal floor.</summary>
        public const int FontLegalPx = 30;

        /// <summary>Breathing room either side of the canon Close, reference px.</summary>
        public const float CloseBreathingRoomPx = 40f;

        /// <summary>
        /// Half the canon Close plus breathing room — the slice of the band's width the copy must
        /// stay out of. DERIVED from the kit, never typed.
        /// </summary>
        public static float CloseKeepOutPx =>
            (ElarionUiKit.CanonCtaWidth * 0.5f) + CloseBreathingRoomPx;

        /// <summary>
        /// Build the band's copy under <paramref name="host"/>.
        /// </summary>
        /// <param name="host">The bottom band rect. The Close is re-seated into this same rect by
        /// the caller — that is what makes a second band structurally impossible.</param>
        /// <param name="bandWidthPx">The band's own width in reference px, so the keep-out can be
        /// expressed as a fraction of the rect it actually occupies.</param>
        /// <param name="treasuryShort">Already-shortened distributor address. Shortening is the
        /// caller's concern; this component does not reach into WalletService.</param>
        public static StoreLegalFooterHandle Build(Transform host, float bandWidthPx, string treasuryShort)
        {
            var handle = new StoreLegalFooterHandle();
            if (host == null)
            {
                FlowTrace.Fail("Store", "StoreLegalFooter.Build: no host — the legal band is ABSENT, " +
                                        "which is a claims/compliance surface silently missing, not a cosmetic gap.");
                return handle;
            }

            Plate(host, new Color(0f, 0f, 0f, 0.30f));

            float width = bandWidthPx > 1f ? bandWidthPx : 1f;
            float keep = Mathf.Clamp(CloseKeepOutPx / width, 0f, 0.45f);
            float leftX1  = 0.5f - keep;
            float rightX0 = 0.5f + keep;

            // ⛔ THE TWO LANES ARE DELIBERATELY UNEQUAL. The CLAIM lane is the taller one because
            // the claim is the longest string in the band and the Close keep-out costs it ~40% of
            // the width. Split 50/50 it had room for ONE line, so it either wrapped and OVERFLOWED
            // the band upward (the 2026-08-23 capture, before FitBlock) or fitted by TRUNCATING its
            // last word (after FitBlock) — "every payment reaches the…" is a claims surface losing
            // the noun that makes it a claim. A two-line lane fits it whole.
            const float ClaimY0 = 0.42f, ClaimY1 = 0.98f;   // ~74 ref px — two lines at the floor
            const float NoteY0  = 0.02f, NoteY1  = 0.40f;   // ~50 ref px — one line

            // LEFT — the legal half.
            Text(host, StoreStrings.Get(StoreStrings.KeyTrustFee), ElarionUi.Parchment,
                FontStyles.Bold, TextAlignmentOptions.Left,
                new Vector2(0f, ClaimY0), new Vector2(leftX1, ClaimY1));

            handle.DisclaimerLabel = Text(host, PackCatalog.CurrencyDisclaimer, ElarionUi.Parchment,
                FontStyles.Normal, TextAlignmentOptions.Left,
                new Vector2(0f, NoteY0), new Vector2(leftX1, NoteY1));

            // RIGHT — the promise half.
            Text(host, StoreStrings.Get(StoreStrings.KeyTrustNeverPower), ElarionUi.Gold,
                FontStyles.Bold, TextAlignmentOptions.Right,
                new Vector2(rightX0, ClaimY0), new Vector2(1f, ClaimY1));

            Text(host, StoreStrings.Format(StoreStrings.KeyTrustTreasury, treasuryShort ?? string.Empty),
                ElarionUi.Parchment, FontStyles.Normal, TextAlignmentOptions.Right,
                new Vector2(rightX0, NoteY0), new Vector2(1f, NoteY1));

            FlowTrace.Step("Store", "StoreLegalFooter: one legal band built — four claims, " +
                                    "Close keep-out " + keep.ToString("0.000") + " either side of centre.");
            return handle;
        }

        /// <summary>Refresh the disclaimer in place after a catalogue reload.</summary>
        public static void RefreshDisclaimer(StoreLegalFooterHandle handle)
        {
            if (handle == null || handle.DisclaimerLabel == null) return;
            handle.DisclaimerLabel.text = PackCatalog.CurrencyDisclaimer;
        }

        // ── local uGUI helpers: this component owns its own drawing so a second
        //    surface can adopt it without depending on PackStore's privates. ──────
        private static TextMeshProUGUI Text(Transform parent, string text, Color color,
            FontStyles style, TextAlignmentOptions align, Vector2 min, Vector2 max)
        {
            var go = new GameObject("legal", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = min; rt.anchorMax = max;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text ?? string.Empty;
            t.fontSize = Mathf.Max(FontLegalPx, ElarionUi.FontFloorMobile);
            t.color = color;
            t.fontStyle = style;
            t.alignment = align;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            ElarionUiKit.EnsureFont(t);
            // ⛔ FIT, ALWAYS. The band is ONE CanonCtaHeight row split into two half-height lanes,
            // so a claim long enough to wrap does not just look cramped — it OVERFLOWS ITS RECT
            // UPWARD, over the shelf card above the band. The 2026-08-23 capture caught exactly
            // that: "0% STORE FEE - EVERY PAYMENT REACHES THE REALM" wrapped to two lines and the
            // second line drew outside the band. FitBlock auto-sizes DOWN inside
            // [FontFloorMobile .. FontLegalPx] and only then truncates — it never draws outside.
            ElarionUiKit.FitBlock(t, ElarionUi.FontFloorMobile, FontLegalPx);
            return t;
        }

        private static void Plate(Transform parent, Color color)
        {
            if (parent == null) return;
            var go = new GameObject("plate", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            go.transform.SetAsFirstSibling();
        }
    }
}
