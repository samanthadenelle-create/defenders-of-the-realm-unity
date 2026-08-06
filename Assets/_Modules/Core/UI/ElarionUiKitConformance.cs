// =============================================================================
// ElarionUiKit.Conformance — WO-714 Phase 1 shared primitives (fix at the FACTORY).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.UI   (partial of ElarionUiKit)
//
// The defect classes from the WO-675/680/683/693/697/713 arc, promoted to
// kit-level so every screen inherits the fix instead of re-authoring it:
//   P1  BuildTabRow        — the ONE uniform tab row (element_tab plates, optional
//                            icon + label, fit-never-truncate, exclusive selection).
//   P2  BuildWalletRow     — the ONE wallet strip of CurrencyChips (CompactNumber
//                            formatting lives in the chip; currency-ellipsis forbidden).
//   P4  BuildRaritySlot /  — the slot grammar: sprite-first Inventory_Slot plate +
//       BuildSparseSlotGrid  rarity_1..5 rim; EMPTY = dim plate (sparse-grid law —
//                            a grid reads as a grid even when sparse).
//   P5  ShowToast          — the ONE transient non-blocking toast (auto-fade +
//                            destroy, one-at-a-time; no stuck status text ever).
//   P8  PanelOpenCloseFx   — the flagged BuildingUpgradePanelMvvm open/close tween,
//                            promoted to the kit (AttachPanelOpenFx / ClosePanelWithFx).
//   P9  BlinkChromeActive  — sprite-first law: Blink chrome suppression keys on ART
//                            PRESENCE, never on the raw ff.blinkchrome flag alone.
//   P10 SpacedDisplayName  — raw itemIds are never player-visible: "iron_sword" /
//                            "IronSword" -> "Iron Sword" (ASCII only).
//
// STRICTLY ADDITIVE (WO-714 coordination rule — four screen lanes consume the kit
// in parallel tonight): new builders/nested types only; no existing kit method is
// renamed, removed, or re-signatured by this file.
// ASCII-only structural strings; callers pass their own display text.
// =============================================================================

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Core.UI
{
    public static partial class ElarionUiKit
    {
        // =====================================================================
        // P9 — sprite-first ALWAYS: chrome suppression keys on ART PRESENCE.
        // ---------------------------------------------------------------------
        // The old gates read FeatureFlags.BlinkChrome raw: with the flag ON but
        // the mirrored art ABSENT (fresh clone / art-absent build) the kit both
        // skipped the procedural rims AND had no frame art — bare panels. The
        // dual-state contract (BLINK_OBSIDIAN doc §3.6) demands each surface look
        // right with the art present AND absent, so the flag only suppresses the
        // procedural chrome when the Blink art can actually stand in for it.
        // =====================================================================

        private static bool _blinkArtChecked;
        private static bool _blinkArtPresent;

        /// <summary>True only when the BlinkChrome flag is ON AND the mirrored Blink art is
        /// actually present (Resources/RpgUi) — the ONE gate the kit's chrome-suppression
        /// sites read (WO-714 P9). With the flag ON but art absent the procedural chrome
        /// stays on, so a fresh clone can never render bare panels.</summary>
        public static bool BlinkChromeActive
        {
            get
            {
                if (!DeNelle.Core.FeatureFlags.BlinkChrome) return false;
                if (!_blinkArtChecked)
                {
                    _blinkArtChecked = true;
                    _blinkArtPresent =
                        RpgUiCatalog.Get(RpgUiCatalog.RoleFrame, RpgUiCatalog.FrameCore) != null ||
                        RpgUiCatalog.Get(RpgUiCatalog.RoleElement, RpgUiCatalog.ElementStat) != null ||
                        RpgUiCatalog.Get(RpgUiCatalog.RoleSlot, RpgUiCatalog.SlotItem) != null;
                    if (!_blinkArtPresent)
                        FlowTrace.Warn("UI", "BlinkChrome flag ON but no mirrored Blink art resolved " +
                            "(Resources/RpgUi empty?) -- procedural chrome stays ON (sprite-first law, WO-714 P9)");
                }
                return _blinkArtPresent;
            }
        }

        // =====================================================================
        // P1 — the ONE uniform tab row.
        // =====================================================================

        /// <summary>Live handle of a <see cref="BuildTabRow"/>: the tabs + exclusive selection.</summary>
        public sealed class TabRowHandle
        {
            /// <summary>The row's tabs, in build order.</summary>
            public TabHandle[] tabs;
            internal Action<int> onSelect;
            /// <summary>Currently selected index (-1 before the first Select).</summary>
            public int Selected { get; private set; } = -1;

            /// <summary>Select a tab exclusively (highlights it, un-highlights the rest).
            /// notify:true fires the row's onSelect callback.</summary>
            public void Select(int index, bool notify = true)
            {
                if (tabs == null || index < 0 || index >= tabs.Length) return;
                Selected = index;
                for (int i = 0; i < tabs.Length; i++)
                    if (tabs[i] != null) tabs[i].SetSelected(i == index);
                if (notify && onSelect != null) onSelect(index);
            }
        }

        /// <summary>
        /// The ONE uniform tab row (WO-714 P1): N <see cref="BuildTab"/> tabs (element_tab
        /// plates, arrow_box_on selection, §1.14 fit-never-truncate labels) evenly spaced
        /// across <paramref name="parent"/>, wired for exclusive selection. Optional
        /// per-tab icon concepts (<see cref="UiStyle.Icon"/>; an unresolved concept simply
        /// renders label-only — never a blank tab). No screen builds its own tab strip.
        /// </summary>
        public static TabRowHandle BuildTabRow(Transform parent, string[] labels,
            Action<int> onSelect, int initial = 0, string[] iconConcepts = null,
            float gapFrac = 0.012f)
        {
            var h = new TabRowHandle { onSelect = onSelect };
            int n = labels != null ? labels.Length : 0;
            h.tabs = new TabHandle[n];
            if (n == 0)
            {
                FlowTrace.Warn("UI", "BuildTabRow: called with no labels -- empty row");
                return h;
            }
            gapFrac = Mathf.Clamp(gapFrac, 0f, 0.2f / n);
            float w = (1f - gapFrac * (n - 1)) / n;
            for (int i = 0; i < n; i++)
            {
                int idx = i;
                float x0 = i * (w + gapFrac);
                var tab = BuildTab(parent, labels[i] ?? "",
                    new Vector2(x0, 0f), new Vector2(x0 + w, 1f),
                    () => h.Select(idx));

                // Optional icon left of the label (icon + text -- meaning never by color alone).
                Sprite icon = (iconConcepts != null && i < iconConcepts.Length &&
                               !string.IsNullOrEmpty(iconConcepts[i]))
                    ? UiStyle.Icon(iconConcepts[i]) : null;
                if (icon != null && tab != null && tab.button != null)
                {
                    var ig = new GameObject("TabIcon", typeof(Image));
                    ig.transform.SetParent(tab.button.transform, false);
                    var irt = (RectTransform)ig.transform;
                    irt.anchorMin = new Vector2(0.05f, 0.20f);
                    irt.anchorMax = new Vector2(0.22f, 0.80f);
                    irt.offsetMin = Vector2.zero; irt.offsetMax = Vector2.zero;
                    var img = ig.GetComponent<Image>();
                    img.sprite = icon;
                    img.preserveAspect = true;
                    img.raycastTarget = false;
                    if (tab.label != null)
                    {
                        var lrt = tab.label.rectTransform;
                        lrt.anchorMin = new Vector2(0.24f, lrt.anchorMin.y);
                        tab.label.alignment = TextAlignmentOptions.MidlineLeft;
                    }
                }
                h.tabs[i] = tab;
            }
            h.Select(Mathf.Clamp(initial, 0, n - 1), notify: false);
            return h;
        }

        // =====================================================================
        // P2 — the ONE wallet strip of CurrencyChips.
        // =====================================================================

        /// <summary>
        /// The ONE wallet strip (WO-714 P2): one <see cref="CurrencyChip"/> per kind, evenly
        /// spaced across <paramref name="parent"/> (a footer zone / chip band). The chip owns
        /// ALL currency presentation — CompactNumber formatting, gold primacy, icon-first
        /// identity — so no screen ever hand-rolls a wallet or formats a currency string
        /// (currency-ellipsis forbidden, WO-697). Returns the handles in kind order; drive
        /// amounts via <see cref="CurrencyChipHandle.SetAmount"/>.
        /// </summary>
        public static CurrencyChipHandle[] BuildWalletRow(Transform parent, CurrencyKind[] kinds,
            bool goldPrimary = true, float gapFrac = 0.02f)
        {
            int n = kinds != null ? kinds.Length : 0;
            var handles = new CurrencyChipHandle[n];
            if (n == 0)
            {
                FlowTrace.Warn("UI", "BuildWalletRow: called with no kinds -- empty strip");
                return handles;
            }
            gapFrac = Mathf.Clamp(gapFrac, 0f, 0.2f / n);
            float w = (1f - gapFrac * (n - 1)) / n;
            for (int i = 0; i < n; i++)
            {
                float x0 = i * (w + gapFrac);
                handles[i] = CurrencyChip(parent, kinds[i],
                    new Vector2(x0, 0f), new Vector2(x0 + w, 1f),
                    primary: goldPrimary && kinds[i] == CurrencyKind.Gold);
            }
            return handles;
        }

        // =====================================================================
        // P4 — slot grammar: rarity slot + the sparse-grid law.
        // =====================================================================

        /// <summary>Live handle of a <see cref="BuildRaritySlot"/>.</summary>
        public sealed class RaritySlotHandle
        {
            /// <summary>The slot root.</summary>
            public GameObject root;
            /// <summary>The Inventory_Slot plate (chrome).</summary>
            public Image plate;
            /// <summary>The rarity_1..5 rim overlay (null-art fallback = tinted ring); hidden when empty.</summary>
            public Image rim;
            /// <summary>The item icon (hidden when empty / no sprite).</summary>
            public Image icon;
            /// <summary>Stack count (bottom-right, gilt).</summary>
            public TMP_Text count;
            /// <summary>The tap target.</summary>
            public Button button;

            internal float plateRestAlpha = 1f;
            private bool _empty;

            /// <summary>Assign the item icon sprite (hidden on null / while empty).</summary>
            public void SetIcon(Sprite s)
            {
                if (icon == null) return;
                icon.sprite = s;
                icon.enabled = s != null && !_empty;
            }

            /// <summary>Set the stack count ("" under 2 — a single item shows no number).</summary>
            public void SetCount(int n)
            {
                if (count != null) count.text = n > 1 ? n.ToString() : "";
            }

            /// <summary>The sparse-grid law: an EMPTY slot stays visible as a DIM plate (the
            /// grid reads as a grid), with rim/icon/count hidden and the tap disabled.</summary>
            public void SetEmpty(bool empty)
            {
                _empty = empty;
                if (plate != null)
                {
                    var c = plate.color;
                    c.a = empty ? plateRestAlpha * 0.45f : plateRestAlpha;
                    plate.color = c;
                }
                if (rim != null) rim.enabled = !empty;
                if (icon != null) icon.enabled = !empty && icon.sprite != null;
                if (count != null && empty) count.text = "";
                if (button != null) button.interactable = !empty;
            }
        }

        /// <summary>
        /// The ONE rarity slot (WO-714 P4): sprite-first Inventory_Slot plate (9-sliced,
        /// chrome-tinted; procedural cell fallback) + the rarity_1..5 rim overlay
        /// (RaritySlotName art; tinted-ring fallback so the tier still reads), an icon
        /// well, a gilt stack count, and a tap target. empty:true renders the dim plate
        /// per the sparse-grid law. Rarity index is the canonical 0..4 ladder.
        /// </summary>
        public static RaritySlotHandle BuildRaritySlot(Transform parent, int rarityIndex,
            Vector2 anchorMin, Vector2 anchorMax, bool empty = false, Action onTap = null)
        {
            var h = new RaritySlotHandle();

            var go = new GameObject("RaritySlot", typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            h.root = go;

            // Plate — sprite-first (SlotItem), procedural cell fallback. Never blanks.
            h.plate = go.GetComponent<Image>();
            var plateSprite = RpgUiCatalog.Get(RpgUiCatalog.RoleSlot, RpgUiCatalog.SlotItem);
            if (plateSprite != null)
            {
                h.plate.sprite = plateSprite;
                h.plate.type = Image.Type.Sliced;
                h.plate.fillCenter = true;
                h.plate.color = ChromeTint;                        // chrome
            }
            else
            {
                h.plate.color = Cell;
                ApplyRounded(h.plate);
            }
            h.plateRestAlpha = h.plate.color.a;

            // Rarity rim — the rarity_1..5 art over the plate; tinted-ring fallback.
            var rimSprite = RpgUiCatalog.Get(RpgUiCatalog.RoleSlot, RpgUiCatalog.RaritySlotName(rarityIndex));
            if (rimSprite != null)
            {
                var rg = new GameObject("RarityRim", typeof(Image));
                rg.transform.SetParent(go.transform, false);
                var rrt = (RectTransform)rg.transform;
                rrt.anchorMin = Vector2.zero; rrt.anchorMax = Vector2.one;
                rrt.offsetMin = Vector2.zero; rrt.offsetMax = Vector2.zero;
                h.rim = rg.GetComponent<Image>();
                h.rim.sprite = rimSprite;
                h.rim.type = Image.Type.Sliced;
                h.rim.fillCenter = false;                          // rim only — the plate carries the well
                h.rim.color = Color.white;                         // rarity art's own colour is CONTENT
                h.rim.raycastTarget = false;
            }
            else
            {
                // Null-art fallback: a true ring in the canonical rarity colour (rim never
                // tints the interior; strength escalates by tier like Slot/Card).
                Color rc = RarityColor(rarityIndex);
                h.rim = AddRoundedRing(go.transform, "RarityRim", 1f,
                    new Color(rc.r, rc.g, rc.b, RarityFrameStrength(rarityIndex)), 3f);
            }

            // Icon well — hidden until SetIcon.
            var ig = new GameObject("Icon", typeof(Image));
            ig.transform.SetParent(go.transform, false);
            var irt = (RectTransform)ig.transform;
            irt.anchorMin = new Vector2(0.12f, 0.12f);
            irt.anchorMax = new Vector2(0.88f, 0.88f);
            irt.offsetMin = Vector2.zero; irt.offsetMax = Vector2.zero;
            h.icon = ig.GetComponent<Image>();
            h.icon.preserveAspect = true;
            h.icon.raycastTarget = false;
            h.icon.enabled = false;

            // Stack count — gilt, bottom-right, fit-protected.
            h.count = Label(go.transform, "", 0.02f, 0.34f, ElarionUi.Gilt,
                            ElarionUi.FontMicro, TextAlignmentOptions.BottomRight, 0.50f, 0.94f, bold: true);
            h.count.raycastTarget = false;
            FitSingleLine(h.count);

            h.button = go.GetComponent<Button>();
            h.button.targetGraphic = h.plate;
            StyleButtonColors(h.button);
            if (onTap != null) h.button.onClick.AddListener(() => onTap());

            if (empty) h.SetEmpty(true);
            return h;
        }

        /// <summary>
        /// The sparse-grid law (WO-714 P4): build a FULL columns x rows grid of rarity
        /// slots; indices below <paramref name="filledCount"/> are live (rarity via
        /// <paramref name="rarityOf"/>, taps via <paramref name="onTap"/> with the index),
        /// the remainder render as EMPTY dim plates — so a sparse inventory still reads
        /// as a grid, never as floating islands. Row-major, top-left first.
        /// </summary>
        public static RaritySlotHandle[] BuildSparseSlotGrid(Transform parent, int columns, int rows,
            int filledCount, Func<int, int> rarityOf = null, Action<int> onTap = null,
            float gapFrac = 0.015f)
        {
            columns = Mathf.Max(1, columns);
            rows = Mathf.Max(1, rows);
            int total = columns * rows;
            var handles = new RaritySlotHandle[total];
            float cw = (1f - gapFrac * (columns - 1)) / columns;
            float ch = (1f - gapFrac * (rows - 1)) / rows;
            for (int i = 0; i < total; i++)
            {
                int col = i % columns;
                int row = i / columns;
                float x0 = col * (cw + gapFrac);
                float y1 = 1f - row * (ch + gapFrac);   // top-left first
                bool filled = i < filledCount;
                int idx = i;
                handles[i] = BuildRaritySlot(parent,
                    filled && rarityOf != null ? rarityOf(i) : 0,
                    new Vector2(x0, y1 - ch), new Vector2(x0 + cw, y1),
                    empty: !filled,
                    onTap: filled && onTap != null ? () => onTap(idx) : (Action)null);
            }
            return handles;
        }

        // =====================================================================
        // P5 — the ONE transient toast (no stuck status text, ever).
        // =====================================================================

        private static GameObject s_transientToast;

        /// <summary>
        /// The ONE transient feedback toast (WO-714 P5, the BuildFeedbackToast pattern
        /// promoted): a self-contained ToastCard on its own overlay canvas that fades and
        /// destroys itself after <paramref name="lifeSeconds"/>. One at a time (a new call
        /// replaces the old — rapid events never stack); never raycast-blocks gameplay.
        /// Use this for ALL transient status ("Saved", "Not enough Gold", "Equipped") —
        /// a screen must never park status text in a label that can go stale.
        /// </summary>
        /// <param name="cardWidth">Card width in reference px. The 480x76 default holds ~2 lines of
        /// the 24px legacy Text at ~442px usable width (~80 chars). A longer sentence needs a bigger
        /// card: ToastCard's label is VerticalWrapMode.Overflow, so a third line draws OUTSIDE the
        /// plate instead of being clipped. Appended as trailing optionals — every existing caller
        /// keeps the exact card it has today.</param>
        /// <param name="cardHeight">Card height in reference px (see <paramref name="cardWidth"/>).</param>
        public static void ShowToast(string message, ToastTone tone = ToastTone.Info,
            float lifeSeconds = 2.2f, int sortingOrder = 720,
            float cardWidth = 480f, float cardHeight = 76f)
        {
            if (string.IsNullOrEmpty(message) || !Application.isPlaying) return;

            if (s_transientToast != null)
            {
                UnityEngine.Object.Destroy(s_transientToast);
                s_transientToast = null;
            }

            var go = new GameObject("KitTransientToast");
            s_transientToast = go;

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            var group = go.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;                          // never swallow input

            var parts = ToastCard(go.transform, tone, accentLeft: true, align: TextAnchor.MiddleLeft);
            var crt = (RectTransform)parts.card.transform;
            crt.anchorMin = new Vector2(0.5f, 0f);
            crt.anchorMax = new Vector2(0.5f, 0f);
            crt.pivot = new Vector2(0.5f, 0f);
            crt.anchoredPosition = new Vector2(0f, 220f);          // clear of thumb bands / palette trays
            crt.sizeDelta = new Vector2(Mathf.Max(240f, cardWidth), Mathf.Max(60f, cardHeight));
            if (parts.label != null) parts.label.text = message;

            var life = go.AddComponent<UiKitTransientToast>();
            life.Init(group, Mathf.Max(0.8f, lifeSeconds));
            FlowTrace.Step("UI", "kit toast -> '" + message + "' tone=" + tone);
        }

        /// <summary>P5 lifetime driver: unscaled-time auto-fade + self-destroy.</summary>
        private sealed class UiKitTransientToast : MonoBehaviour
        {
            private const float FadeSeconds = 0.45f;
            private CanvasGroup _group;
            private float _life;
            private float _shownAt;

            public void Init(CanvasGroup group, float lifeSeconds)
            {
                _group = group;
                _life = lifeSeconds;
                _shownAt = Time.unscaledTime;
            }

            private void Update()
            {
                float elapsed = Time.unscaledTime - _shownAt;
                if (elapsed >= _life) { Destroy(gameObject); return; }
                float fadeStart = _life - FadeSeconds;
                if (_group != null && elapsed > fadeStart)
                    _group.alpha = Mathf.Clamp01((_life - elapsed) / FadeSeconds);
            }

            private void OnDestroy()
            {
                if (s_transientToast == gameObject) s_transientToast = null;
            }
        }

        // =====================================================================
        // P8 — PanelOpenCloseFx, promoted to the kit (the WO-675 §8 flagged item).
        // ---------------------------------------------------------------------
        // Nested inside ElarionUiKit so the Village-internal copy of the same name
        // (BuildingUpgradePanelMvvm.cs — now the DEPRECATED private twin; migrate
        // on-touch to ElarionUiKit.PanelOpenCloseFx) can never ambiguate a using.
        // =====================================================================

        /// <summary>
        /// The ONE panel open/close tween (WO-714 P8): ease-out scale 0.92-&gt;1 + fade-in on
        /// open (~0.18s); ease-in fade/scale-out then self-destroy on close (~0.14s).
        /// Unscaled time (panels open while gameplay may be paused); the CanvasGroup blocks
        /// input while closing. Attach via <see cref="AttachPanelOpenFx"/>; close via
        /// <see cref="ClosePanelWithFx"/>.
        /// </summary>
        public sealed class PanelOpenCloseFx : MonoBehaviour
        {
            private const float OpenSec = 0.18f;
            private const float CloseSec = 0.14f;

            private CanvasGroup _group;
            private RectTransform _scaled;
            private bool _closing;

            /// <summary>Play the open ease. <paramref name="scaleTarget"/> = the PANEL rect
            /// (never the overlay canvas root — scale there does not render).</summary>
            public void PlayOpen(RectTransform scaleTarget)
            {
                _group = gameObject.GetComponent<CanvasGroup>();
                if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
                _scaled = scaleTarget;
                _group.alpha = 0f;
                if (_scaled != null) _scaled.localScale = Vector3.one * 0.92f;
                StartCoroutine(Ease(open: true, OpenSec, onDone: null));
            }

            /// <summary>Play the close ease, then Destroy this GameObject. Input is blocked
            /// for the duration; idempotent while already closing.</summary>
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

            private IEnumerator Ease(bool open, float duration, Action onDone)
            {
                float t = 0f;
                while (t < duration)
                {
                    t += Time.unscaledDeltaTime;
                    float x = Mathf.Clamp01(t / duration);
                    // open = ease-OUT cubic; close = ease-IN cubic (owner-specified feel).
                    float k = open ? 1f - Mathf.Pow(1f - x, 3f) : 1f - Mathf.Pow(x, 3f);
                    if (_group != null) _group.alpha = k;
                    if (_scaled != null)
                        _scaled.localScale = Vector3.one * Mathf.Lerp(open ? 0.92f : 0.94f, 1f, k);
                    yield return null;
                }
                if (_group != null) _group.alpha = open ? 1f : 0f;
                if (_scaled != null && open) _scaled.localScale = Vector3.one;
                if (onDone != null) onDone();
            }
        }

        /// <summary>Attach the shared open/close FX to a panel canvas root and play the open
        /// ease. <paramref name="scaleTarget"/> = the panel rect (e.g. chrome.root's
        /// RectTransform). Editor-time / headless-safe: outside play mode it only attaches.</summary>
        public static PanelOpenCloseFx AttachPanelOpenFx(GameObject canvasRoot, RectTransform scaleTarget)
        {
            if (canvasRoot == null) return null;
            var fx = canvasRoot.GetComponent<PanelOpenCloseFx>();
            if (fx == null) fx = canvasRoot.AddComponent<PanelOpenCloseFx>();
            if (Application.isPlaying) fx.PlayOpen(scaleTarget);
            return fx;
        }

        /// <summary>Close a panel through the shared FX when present/active (eased fade-out +
        /// self-destroy), else destroy immediately. Pair with <see cref="AttachPanelOpenFx"/>.</summary>
        public static void ClosePanelWithFx(GameObject canvasRoot)
        {
            if (canvasRoot == null) return;
            var fx = canvasRoot.GetComponent<PanelOpenCloseFx>();
            if (Application.isPlaying && fx != null && fx.isActiveAndEnabled) fx.PlayCloseAndDestroy();
            else UnityEngine.Object.Destroy(canvasRoot);
        }

        // =====================================================================
        // P10 — raw itemIds are NEVER player-visible.
        // =====================================================================

        /// <summary>
        /// The ONE raw-id -&gt; display-name formatter (WO-714 P10): "iron_sword",
        /// "ironSword", "IronSword", "iron-sword" all render "Iron Sword". Every screen
        /// that would otherwise print an itemId routes it through here (prefer the
        /// catalog's authored displayName when one exists — this is the fallback so a
        /// missing displayName still never leaks snake_case to the player). ASCII only.
        /// </summary>
        public static string SpacedDisplayName(string rawId)
        {
            if (string.IsNullOrEmpty(rawId)) return "";
            var sb = new System.Text.StringBuilder(rawId.Length + 8);
            bool newWord = true;
            for (int i = 0; i < rawId.Length; i++)
            {
                char c = rawId[i];
                if (c == '_' || c == '-' || c == ' ' || c == '.')
                {
                    if (sb.Length > 0 && sb[sb.Length - 1] != ' ') sb.Append(' ');
                    newWord = true;
                    continue;
                }
                // camelCase boundary: lower/digit followed by an upper starts a new word.
                if (char.IsUpper(c) && !newWord && i > 0 &&
                    (char.IsLower(rawId[i - 1]) || char.IsDigit(rawId[i - 1])))
                {
                    sb.Append(' ');
                    newWord = true;
                }
                sb.Append(newWord ? char.ToUpperInvariant(c) : c);
                newWord = false;
            }
            return sb.ToString();
        }
    }
}
