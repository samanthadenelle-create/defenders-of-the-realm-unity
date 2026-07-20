// =============================================================================
// FlagCaptureButton - the MOBILE equivalent of the F8 bug-flag key.
// -----------------------------------------------------------------------------
// WHY THIS EXISTS (owner 2026-07-16): the owner felt-tests on an Android device
//   and CANNOT press F8 (no keyboard). F8 is how BreakCaptureHarness records a
//   subjective/visual bug the moment it is seen (screenshot + break-log entry +
//   the recent [Flow:*]/Guard/exception trace tail). On mobile that key is
//   unreachable, so this is a small always-visible ON-SCREEN chip that, on tap,
//   fires the SAME capture the F8 key fires. This is CLAUDE.md 14 on mobile:
//   "the owner is NEVER the bug detector" - she just taps FLAG when she sees it.
//
// SAME CAPTURE PATH AS F8 (not a parallel copy):
//   F8 lives in BreakCaptureHarness.Update():  if (Input.GetKeyDown(F8)) FlagHere();
//   FlagHere() -> (type note) -> CommitFlag() -> Record("flagged", ...).
//   FlagHere/CommitFlag are private and the note step needs a keyboard, so this
//   button calls the harness's public FlagFromButton(...) entry point, which runs
//   the IDENTICAL clean-frame screenshot + Record(kind="flagged") + per-session
//   flag counter + confirmation toast, minus the (keyboard) note step.
//
// ASSEMBLY: DeNelle.Core (same assembly as BreakCaptureHarness + FeatureFlags +
//   FlowTrace), so the call is DIRECT + null-safe (no reflection).
//
// DEV/TESTER GATE (ShouldShow): Application.isEditor || Debug.isDebugBuild ||
//   FeatureFlags.FlagButton. The tester APK is a RELEASE build (BuildOptions.None),
//   so Debug.isDebugBuild is FALSE there - the FLAG button surfaces via the flag,
//   which DEFAULTS ON for the local tester APK. Set PlayerPrefs "ff.flagbutton" = 0
//   (or default it OFF) before a PUBLIC/STORE release so real players never see it.
//
// SELF-BOOTSTRAP: a RuntimeInitializeOnLoadMethod (AfterSceneLoad) spawns ONE
//   DontDestroyOnLoad host + its OWN ScreenSpaceOverlay canvas - NO scene is edited
//   (mirrors ResourceDevTool / OwnerDevToolsOverlay). Fully guarded.
//
// PLACEMENT: a small (~104x76), semi-transparent grey chip at the TOP-LEFT, seated
//   BELOW the vitals plate. Deliberately clear of the other persistent controls -
//   the DEV chip (left edge, vertical centre), the Menu/gear (top-right), the
//   D-pad (bottom-left) and the attack pill (bottom-right) - so it can be tapped in
//   any state (incl. mid-combat) without covering them. ASCII-only, colorblind-safe
//   (the owner is red/green colorblind, so meaning is carried by the TEXT label,
//   never by hue): idle "FLAG", flashes "FLAGGED" for ~1s on tap.
// =============================================================================

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using DeNelle.Core;                 // FeatureFlags
using DeNelle.Core.Diagnostics;     // FlowTrace, BreakCaptureHarness
using Debug = UnityEngine.Debug;

namespace DeNelle.Core.Dev
{
    /// <summary>
    /// Mobile on-screen bug-flag chip: the tap-to-capture equivalent of the F8 key.
    /// Self-bootstraps behind a dev/tester gate and fires the SAME BreakCaptureHarness
    /// "flagged" capture the F8 key fires.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FlagCaptureButton : MonoBehaviour
    {
        public static FlagCaptureButton Instance { get; private set; }

        // ASCII-only labels (owner is red/green colorblind - meaning via TEXT, not hue).
        private const string IdleLabel    = "FLAG";
        private const string FlaggedLabel = "FLAGGED";
        private const float  FlashSeconds = 1.0f;

        // Grey/white kit style, semi-transparent so it does not wreck screenshots.
        private static readonly Color IdleColor    = new Color(0.22f, 0.24f, 0.28f, 0.55f);
        private static readonly Color FlaggedColor = new Color(0.85f, 0.85f, 0.88f, 0.95f);
        private static readonly Color IdleText     = new Color(0.95f, 0.95f, 0.95f, 0.95f);
        private static readonly Color FlaggedText  = new Color(0.06f, 0.06f, 0.07f, 1.00f);

        private GameObject _canvasGo;
        private Image _buttonImage;
        private Text  _label;
        private Coroutine _flash;

        // ------------------------------------------------------------------
        // DEV / TESTER GATE (mirrors ResourceDevTool.ShouldShow)
        // ------------------------------------------------------------------
        private static bool ShouldShow()
        {
            // Editor + any Development build: always available.
            if (Application.isEditor || Debug.isDebugBuild) return true;
            // Release build (incl. the release-signed local tester APK): only when the
            // flag is ON. Default ON now; ff.flagbutton=0 hides it for a public release.
            return FeatureFlags.FlagButton;
        }

        // ------------------------------------------------------------------
        // SELF-BOOTSTRAP (mirrors ResourceDevTool.Bootstrap)
        // ------------------------------------------------------------------
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            if (!ShouldShow())
            {
                FlowTrace.Once("FlagButton", "flagbutton-gate-blocked",
                    "FlagCaptureButton gate BLOCKED (release build, ff.flagbutton OFF) - not spawned.");
                return;
            }
            try
            {
                var go = new GameObject("[FlagCaptureButton]");
                DontDestroyOnLoad(go);
                Instance = go.AddComponent<FlagCaptureButton>();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[FlagCaptureButton] Bootstrap failed (non-fatal): " + e.Message);
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            try
            {
                BuildOverlay();
                FlowTrace.Step("FlagButton", "on-screen FLAG button ready (mobile F8 equivalent).");
            }
            catch (Exception e)
            {
                FlowTrace.Warn("FlagButton", $"BuildOverlay threw: {e.GetType().Name}: {e.Message}");
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ------------------------------------------------------------------
        // UI
        // ------------------------------------------------------------------
        private void BuildOverlay()
        {
            _canvasGo = new GameObject("FlagButtonCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvasGo.transform.SetParent(transform, false);
            var canvas = _canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5300;   // above gameplay HUD, below OwnerDev(5500)/DevPanel(9000) modals

            // Mirror the HUD kit scaler (HudAreasHost: ScaleWithScreenSize, 1080x1920, match 0.5)
            // so this dev chip lives in the SAME coordinate space as the vitals/heart cluster and
            // its fractional anchor lands consistently across resolutions. ConstantPixelSize let the
            // old hardcoded (12,-156) offset drift ON TOP of the HP/MP bars on scaled devices.
            var scaler = _canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // --- the FLAG chip: LEFT EDGE, in the EMPTY mid-left band (between the Dock area at
            // y0.330-0.430 and the HeartStatus area at y0.700-0.792) so the dev overlay is clear
            // of the real vitals plate (y0.800-0.985), the SKILL chip and the Heart of Elarion bar. ---
            var go = new GameObject("Btn_Flag", typeof(Image), typeof(Button));
            go.transform.SetParent(_canvasGo.transform, false);
            _buttonImage = go.GetComponent<Image>();
            _buttonImage.color = IdleColor;

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.012f, 0.55f);   // left edge, mid-left empty band
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(120f, 84f);               // mobile-safe touch target (>=112 rule)

            var btn = go.GetComponent<Button>();
            btn.onClick.AddListener(OnFlagTapped);

            _label = MakeText(go.transform, IdleLabel, 20, TextAnchor.MiddleCenter, IdleText);
            var lrt = _label.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(2f, 2f); lrt.offsetMax = new Vector2(-2f, -2f);
            _label.raycastTarget = false;
        }

        // ------------------------------------------------------------------
        // TAP -> SAME capture the F8 key fires
        // ------------------------------------------------------------------
        private void OnFlagTapped()
        {
            FlowTrace.Step("FlagButton", "on-screen FLAG tapped -> BreakCaptureHarness force-capture.");

            var harness = BreakCaptureHarness.Instance;
            if (harness != null)
            {
                // null-safe cross-call: the harness runs the identical "flagged" record + clean-frame PNG.
                harness?.FlagFromButton("on-screen FLAG button (mobile)");
            }
            else
            {
                // No silent failure (CLAUDE.md 12): a dead capture self-reports its cause.
                FlowTrace.Warn("FlagButton",
                    "BreakCaptureHarness.Instance is null - harness not installed on this platform (WebGL?); capture skipped.");
            }

            FlashFeedback();
        }

        // Brief on-screen confirmation so the owner knows the tap registered.
        private void FlashFeedback()
        {
            if (_flash != null) StopCoroutine(_flash);
            _flash = StartCoroutine(FlashRoutine());
        }

        private IEnumerator FlashRoutine()
        {
            if (_label != null)       _label.text = FlaggedLabel;
            if (_label != null)       _label.color = FlaggedText;
            if (_buttonImage != null) _buttonImage.color = FlaggedColor;

            // Realtime wait so the flash is unaffected by any timeScale changes.
            yield return new WaitForSecondsRealtime(FlashSeconds);

            if (_label != null)       _label.text = IdleLabel;
            if (_label != null)       _label.color = IdleText;
            if (_buttonImage != null) _buttonImage.color = IdleColor;
            _flash = null;
        }

        // ------------------------------------------------------------------
        // UI HELPER (grey/white, ASCII, colorblind-safe - mirror ResourceDevTool)
        // ------------------------------------------------------------------
        private static Text MakeText(Transform parent, string content, int size, TextAnchor anchor, Color color)
        {
            var go = new GameObject("Text", typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                     ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            t.text = content;
            t.fontSize = size;
            t.fontStyle = FontStyle.Bold;
            t.alignment = anchor;
            t.color = color;
            return t;
        }
    }
}
