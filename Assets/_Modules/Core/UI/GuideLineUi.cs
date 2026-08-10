// =============================================================================
// GuideLineUi — WO-1012 §2b kit piece 4: the guide's ONE-LINE presence.
// -----------------------------------------------------------------------------
// A small lower-left plate: the guide's PORTRAIT (the kit medallion — the same
// ElarionUiKit.Portrait / PortraitForClass path the dialogue view uses) + ONE
// line of speech. No modal card, no Next button: it auto-dismisses after a
// length-scaled dwell, and callers force-dismiss it on beat completion
// (TutorialFlow) — speech never gates.
//
// P1 use: OnboardingFlow's three welcome CARDS become guide one-liners through
// this piece; TutorialFlow hides it on every beat completion so a stale line
// can never outlive its beat. Full step dialogue STAYS on DialogueService (its
// dialogue.ended:<id> completions are load-bearing bones — P3 decides what
// migrates here).
//
// Placement: fixed-PIXEL plate (the fraction-band lesson), anchored lower-left
// but CLEAR of the registered HUD areas — MoveCluster tops at y 0.330 and the
// chat Dock band is x 0.000-0.230 / y 0.330-0.430 (HudAreasHost), so the plate
// anchors at x 0.245 / y 0.335: the lowest-left band that overlaps NEITHER.
// Disjoint from F8/dev overlays (top edge) by construction.
//
// Code-built uGUI, kit language, UNSCALED time, NO raycaster (never blocks).
// [Flow:Tutorial] on show/dismiss/hide.
// =============================================================================

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeNelle.Core.UI
{
    /// <summary>
    /// Static lower-left guide one-liner: portrait + one line, auto-dismissing.
    /// <see cref="Show"/> returns the dwell it will hold the line for (so a
    /// caller sequencing lines can await it); <see cref="Hide"/> force-dismisses
    /// (beat completion).
    /// </summary>
    public sealed class GuideLineUi : MonoBehaviour
    {
        private const float FadeSeconds = 0.2f;
        private const int CanvasSortOrder = 4320;    // above strip/skip; far below dialogs (30000+)
        private const float PlateWidth = 560f;       // fixed px (fraction-band lesson)
        private const float PlateHeight = 92f;
        // Lower-left but clear of MoveCluster (tops 0.330) and the chat Dock
        // (x<=0.230, y<=0.430) — see the file header.
        private static readonly Vector2 AnchorFraction = new Vector2(0.245f, 0.335f);
        private const float PortraitPx = 68f;
        private const float MinDwellSeconds = 2.6f;
        private const float MaxDwellSeconds = 6f;
        private const float DwellPerChar = 0.045f;   // reading-speed scale

        private static GuideLineUi _instance;

        private CanvasGroup _group;
        private RectTransform _portraitHost;
        private TextMeshProUGUI _speakerLabel;
        private TextMeshProUGUI _lineLabel;
        private string _portraitSpeaker;    // speaker the built portrait belongs to

        private bool _visible;
        private float _fadeT;
        private float _dismissAt;           // unscaled time the line auto-dismisses (0 = never)

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Show ONE guide line from <paramref name="speaker"/>. Auto-dismisses
        /// after <paramref name="seconds"/>; pass 0 for a length-scaled dwell
        /// (clamped 2.6-6s). Returns the dwell actually used, so a caller playing a
        /// sequence of one-liners can await each. Never blocks input.</summary>
        public static float Show(string speaker, string line, float seconds = 0f)
        {
            // WO-1012 P2: callers may pass the "{guide}" data token (or the guide's
            // name) — resolve through the guide-identity seam so the kicker always
            // shows the live guide (the pet-Echo today; a rotation hero if the parked
            // mechanism ever revives). Non-guide speakers pass through untouched.
            speaker = Tutorial.TutorialGuide.ResolveToken(speaker);
            var g = Ensure();
            float dwell = seconds > 0f
                ? seconds
                : Mathf.Clamp(1.4f + (line != null ? line.Length : 0) * DwellPerChar,
                              MinDwellSeconds, MaxDwellSeconds);
            g._visible = true;
            g._dismissAt = Time.unscaledTime + dwell;
            if (g._lineLabel != null) g._lineLabel.text = line ?? "";
            if (g._speakerLabel != null) g._speakerLabel.text = (speaker ?? "").ToUpperInvariant();
            g.EnsurePortrait(speaker);
            Diagnostics.FlowTrace.Step("Tutorial",
                $"GuideLine SHOW speaker='{speaker}' dwell={dwell:0.0}s line='{line}'");
            return dwell;
        }

        /// <summary>Force-dismiss (beat completion / flow teardown). Safe when hidden.</summary>
        public static void Hide()
        {
            if (_instance == null || !_instance._visible) return;
            _instance._visible = false;
            _instance._dismissAt = 0f;
            Diagnostics.FlowTrace.Step("Tutorial", "GuideLine HIDE (beat complete / dismissed)");
        }

        // ── Internals ─────────────────────────────────────────────────────────

        private static GuideLineUi Ensure()
        {
            if (_instance != null) return _instance;
            var go = new GameObject("GuideLine");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<GuideLineUi>();
            _instance.Build();
            return _instance;
        }

        private void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = CanvasSortOrder;
            gameObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            // Deliberately NO GraphicRaycaster — a guide line never eats a tap.
            _group = gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;

            // Plate — obsidian glass, lower-left, fixed px.
            var plate = new GameObject("Plate", typeof(RectTransform), typeof(Image));
            plate.transform.SetParent(transform, false);
            var prt = (RectTransform)plate.transform;
            prt.anchorMin = prt.anchorMax = AnchorFraction;
            prt.pivot = new Vector2(0f, 0f);
            prt.anchoredPosition = Vector2.zero;
            prt.sizeDelta = new Vector2(PlateWidth, PlateHeight);
            var pimg = plate.GetComponent<Image>();
            pimg.color = new Color(ElarionUiKit.ObsidianFill.r, ElarionUiKit.ObsidianFill.g,
                                   ElarionUiKit.ObsidianFill.b, 0.88f);
            pimg.raycastTarget = false;

            // Gold rule along the left edge — the guide's accent seam.
            var rule = new GameObject("Rule", typeof(RectTransform), typeof(Image));
            rule.transform.SetParent(prt, false);
            var rrt = (RectTransform)rule.transform;
            rrt.anchorMin = new Vector2(0f, 0f);
            rrt.anchorMax = new Vector2(0f, 1f);
            rrt.pivot = new Vector2(0f, 0.5f);
            rrt.sizeDelta = new Vector2(2f, 0f);
            var rimg = rule.GetComponent<Image>();
            rimg.color = new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.85f);
            rimg.raycastTarget = false;

            // Portrait medallion host — left side, square, centered vertically.
            var ph = new GameObject("PortraitHost", typeof(RectTransform));
            ph.transform.SetParent(prt, false);
            _portraitHost = (RectTransform)ph.transform;
            _portraitHost.anchorMin = _portraitHost.anchorMax = new Vector2(0f, 0.5f);
            _portraitHost.pivot = new Vector2(0f, 0.5f);
            _portraitHost.anchoredPosition = new Vector2(12f, 0f);
            _portraitHost.sizeDelta = new Vector2(PortraitPx, PortraitPx);

            // Speaker kicker — gilt micro line above the speech.
            var sp = new GameObject("Speaker", typeof(RectTransform));
            sp.transform.SetParent(prt, false);
            var srt = (RectTransform)sp.transform;
            srt.anchorMin = new Vector2(0f, 0.62f);
            srt.anchorMax = new Vector2(1f, 0.95f);
            srt.offsetMin = new Vector2(PortraitPx + 24f, 0f);
            srt.offsetMax = new Vector2(-10f, 0f);
            _speakerLabel = sp.AddComponent<TextMeshProUGUI>();
            ElarionUiKit.EnsureFont(_speakerLabel);
            _speakerLabel.fontSize = 12f;
            _speakerLabel.fontStyle = FontStyles.Bold;
            _speakerLabel.color = ElarionUi.Gilt;
            _speakerLabel.alignment = TextAlignmentOptions.BottomLeft;
            _speakerLabel.textWrappingMode = TextWrappingModes.NoWrap;
            _speakerLabel.overflowMode = TextOverflowModes.Ellipsis;
            _speakerLabel.raycastTarget = false;

            // The ONE line — parchment, up to two visual lines, ellipsized.
            var ln = new GameObject("Line", typeof(RectTransform));
            ln.transform.SetParent(prt, false);
            var lrt = (RectTransform)ln.transform;
            lrt.anchorMin = new Vector2(0f, 0.06f);
            lrt.anchorMax = new Vector2(1f, 0.62f);
            lrt.offsetMin = new Vector2(PortraitPx + 24f, 0f);
            lrt.offsetMax = new Vector2(-10f, 0f);
            _lineLabel = ln.AddComponent<TextMeshProUGUI>();
            ElarionUiKit.EnsureFont(_lineLabel);
            _lineLabel.fontSize = 16f;
            _lineLabel.color = ElarionUi.Parchment;
            _lineLabel.alignment = TextAlignmentOptions.TopLeft;
            _lineLabel.textWrappingMode = TextWrappingModes.Normal;
            _lineLabel.overflowMode = TextOverflowModes.Ellipsis;
            _lineLabel.raycastTarget = false;
        }

        /// <summary>(Re)build the portrait medallion when the speaker changes. The GUIDE
        /// resolves its pet-Echo portrait through the identity seam (WO-1012 P2 —
        /// TutorialGuide.PortraitSprite, the Echoes/Portraits art); everyone else rides
        /// the SAME kit path the dialogue view uses (PortraitForClass). An unresolvable
        /// speaker keeps the kit's placeholder disc — never blank.</summary>
        private void EnsurePortrait(string speaker)
        {
            if (_portraitHost == null) return;
            if (string.Equals(_portraitSpeaker, speaker, System.StringComparison.OrdinalIgnoreCase) &&
                _portraitHost.childCount > 0)
                return;
            _portraitSpeaker = speaker;
            for (int i = _portraitHost.childCount - 1; i >= 0; i--)
                Destroy(_portraitHost.GetChild(i).gameObject);
            var sprite = Tutorial.TutorialGuide.IsGuideSpeaker(speaker)
                ? Tutorial.TutorialGuide.PortraitSprite()
                : null;
            if (sprite == null) sprite = ElarionUiKit.PortraitForClass(speaker);
            ElarionUiKit.Portrait(_portraitHost, sprite, active: false);
        }

        private void Update()
        {
            // Auto-dismiss after the dwell (never gates — the line just leaves).
            if (_visible && _dismissAt > 0f && Time.unscaledTime >= _dismissAt)
            {
                _visible = false;
                _dismissAt = 0f;
                Diagnostics.FlowTrace.Step("Tutorial", "GuideLine auto-dismissed (dwell elapsed)");
            }

            float dir = _visible ? 1f : -1f;
            _fadeT = Mathf.Clamp01(_fadeT + dir * (Time.unscaledDeltaTime / FadeSeconds));
            _group.alpha = _fadeT * _fadeT * (3f - 2f * _fadeT);
        }
    }
}
