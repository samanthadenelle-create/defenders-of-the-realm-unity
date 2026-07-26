// =============================================================================
// RaidHudController — the LIVE raid HUD (WO-771.11, LOCKED teleport/deploy loop).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// The passive top-of-screen readout during a raid: a 180s countdown, a star-
// progress indicator, live %-destruction, and troops alive/deployed. It reads
// RaidScoring.Instance every frame (passive — it never drives combat) and renders
// through code-built uGUI (NO UXML — repo rule §8), mirroring RaidDeployController's
// self-install + ElarionUiKit chrome so the deploy tray (bottom) and this HUD (top)
// sit on complementary edges and never overlap.
//
// COLOURBLIND-SAFE (repo law): every state reads by SHAPE / MOTION / NUMBER, never
// hue alone — the timer is a number + a shrinking bar (+ a pulse under 30s), stars
// are filled/empty DIAMONDS + an "n/3" count, destruction is a number + a fill bar,
// troops are a plain "alive/deployed" number.
//
// ASCII-only runtime strings. Canon: the village is Elarion (never Avalon).
// =============================================================================

using System;
using UnityEngine;
using UnityEngine.UI;
using DeNelle.Core.UI;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>
    /// The live raid HUD (timer / stars / destruction% / troop counts). Passive —
    /// binds <see cref="RaidScoring"/> and renders; never mutates game state.
    /// Self-installs into any <c>RaidBase_*</c> scene (idempotent).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RaidHudController : MonoBehaviour
    {
        // Refresh cadence — the HUD polls the scorer ~10x/sec (cheap; the numbers
        // change slowly). The timer text still counts smoothly enough at 10Hz.
        private const float RefreshInterval = 0.1f;
        private float _refreshTimer;

        private GameObject _ui;

        // Widgets refreshed each poll.
        private TMPro.TextMeshProUGUI _timerLabel;
        private RectTransform _timerFill;          // shrinks left->right with the clock
        private TMPro.TextMeshProUGUI _destLabel;
        private RectTransform _destFill;           // grows with destruction%
        private TMPro.TextMeshProUGUI _troopLabel;
        private TMPro.TextMeshProUGUI _starCount;  // "n/3"
        private readonly Image[] _starDiamonds = new Image[3];

        private static readonly Color StarLit = ElarionUi.Gilt;
        private static readonly Color StarDim = new Color(1f, 1f, 1f, 0.14f);

        // =====================================================================
        //  Self-install — one HUD per RaidBase_* scene
        // =====================================================================

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallHook()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
            TryInstall(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene,
                                          UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            TryInstall(scene.name);
        }

        private static void TryInstall(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return;
            if (!sceneName.StartsWith("RaidBase", StringComparison.OrdinalIgnoreCase)) return;
            if (FindAnyObjectByType<RaidHudController>() != null) return;

            var go = new GameObject("RaidHudController");
            go.AddComponent<RaidHudController>();
            FlowTrace.Step("Raid", $"RaidHudController self-installed in raid scene '{sceneName}'.");
        }

        // =====================================================================
        //  Lifecycle
        // =====================================================================

        private void Start()
        {
            BuildHud();
        }

        private void OnDestroy()
        {
            if (_ui != null) Destroy(_ui);
        }

        private void Update()
        {
            _refreshTimer -= Time.unscaledDeltaTime;
            if (_refreshTimer > 0f) return;
            _refreshTimer = RefreshInterval;
            Refresh();
        }

        // =====================================================================
        //  HUD construction (code-built uGUI, top strip — clear of the deploy tray)
        // =====================================================================

        private void BuildHud()
        {
            if (_ui != null) Destroy(_ui);

            // Below the deploy HUD's 30000 so the deploy tray/buttons stay tappable on top.
            _ui = ElarionUiKit.BuildModalCanvas("RaidHud", 29000);

            // A framed dark-glass strip across the TOP (the deploy tray owns the bottom).
            var bar = ElarionUiKit.Panel(_ui.transform, new Vector2(0.02f, 0.86f), new Vector2(0.98f, 0.99f), deep: true);
            // Passive HUD: the strip must never intercept a deploy/rally tap.
            var barImg = bar.GetComponent<Image>();
            if (barImg != null) barImg.raycastTarget = false;
            var barT = bar.transform;

            // ── Left: TIMER (big number + shrinking bar under it) ──────────────
            _timerLabel = MakeLabel(barT, "3:00", new Vector2(0.02f, 0.42f), new Vector2(0.22f, 0.96f),
                ElarionUi.Parchment, ElarionUi.FontBody, TMPro.TextAlignmentOptions.Left, bold: true);

            var timerTrack = ElarionUiKit.AddImage(barT, "TimerTrack",
                new Vector2(0.02f, 0.14f), new Vector2(0.22f, 0.30f), new Color(0f, 0f, 0f, 0.5f), rounded: false);
            timerTrack.GetComponent<Image>().raycastTarget = false;
            var timerFillGo = ElarionUiKit.AddImage(timerTrack.transform, "TimerFill",
                new Vector2(0f, 0f), new Vector2(1f, 1f), ElarionUi.Gilt, rounded: false);
            timerFillGo.GetComponent<Image>().raycastTarget = false;
            _timerFill = (RectTransform)timerFillGo.transform;

            // ── Centre: STAR PROGRESS (3 diamonds + n/3) ───────────────────────
            for (int i = 0; i < 3; i++)
            {
                float cx = 0.42f + i * 0.055f;
                var d = ElarionUiKit.AddImage(barT, "Star" + i,
                    new Vector2(cx, 0.5f), new Vector2(cx, 0.5f), StarDim, rounded: false);
                var img = d.GetComponent<Image>();
                img.raycastTarget = false;
                var rt = img.rectTransform;
                rt.sizeDelta = new Vector2(34f, 34f);
                rt.localRotation = Quaternion.Euler(0f, 0f, 45f);   // diamond
                _starDiamonds[i] = img;
            }
            _starCount = MakeLabel(barT, "0/3", new Vector2(0.60f, 0.42f), new Vector2(0.70f, 0.96f),
                ElarionUi.Gilt, ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Left, bold: true);

            // ── Right: DESTRUCTION % (number + fill bar) ───────────────────────
            _destLabel = MakeLabel(barT, "Razed 0%", new Vector2(0.72f, 0.42f), new Vector2(0.98f, 0.96f),
                ElarionUi.Parchment, ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Right);

            var destTrack = ElarionUiKit.AddImage(barT, "DestTrack",
                new Vector2(0.72f, 0.14f), new Vector2(0.98f, 0.30f), new Color(0f, 0f, 0f, 0.5f), rounded: false);
            destTrack.GetComponent<Image>().raycastTarget = false;
            var destFillGo = ElarionUiKit.AddImage(destTrack.transform, "DestFill",
                new Vector2(0f, 0f), new Vector2(0f, 1f), ElarionUi.Affordable, rounded: false);
            destFillGo.GetComponent<Image>().raycastTarget = false;
            _destFill = (RectTransform)destFillGo.transform;

            // ── Troops alive/deployed (a plain number under the stars) ─────────
            _troopLabel = MakeLabel(barT, "Troops 0/0", new Vector2(0.42f, 0.05f), new Vector2(0.60f, 0.34f),
                ElarionUi.ParchmentDim, ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Left);

            Refresh();
        }

        private static TMPro.TextMeshProUGUI MakeLabel(Transform parent, string text,
            Vector2 anchorMin, Vector2 anchorMax, Color color, int fontSize,
            TMPro.TextAlignmentOptions align, bool bold = false)
        {
            var go = new GameObject("Label", typeof(TMPro.TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var t = go.GetComponent<TMPro.TextMeshProUGUI>();
            t.text = text;
            t.color = color;
            t.fontSize = fontSize;
            t.fontStyle = bold ? TMPro.FontStyles.Bold : TMPro.FontStyles.Normal;
            t.alignment = align;
            t.raycastTarget = false;
            t.enableWordWrapping = false;
            return t;
        }

        // =====================================================================
        //  Refresh — pull the live scorer numbers into the widgets (passive)
        // =====================================================================

        private void Refresh()
        {
            var s = RaidScoring.Instance;
            if (s == null) return;

            // Timer: number + shrinking bar. Pulse (motion, not hue) under 30s so a
            // colourblind player still reads "running out".
            float remaining = s.RemainingSeconds;
            if (_timerLabel != null) _timerLabel.text = FormatTime(remaining);
            if (_timerFill != null)
            {
                float frac = s.ClockSeconds > 0f ? Mathf.Clamp01(remaining / s.ClockSeconds) : 0f;
                _timerFill.anchorMax = new Vector2(frac, 1f);
                float pulse = remaining <= 30f && remaining > 0f
                    ? 1f + 0.12f * Mathf.Sin(Time.unscaledTime * 8f) : 1f;
                if (_timerLabel != null) _timerLabel.transform.localScale = Vector3.one * pulse;
            }

            // Stars: filled/empty diamonds (shape) + n/3 (number).
            int stars = s.ProjectedStars;
            for (int i = 0; i < _starDiamonds.Length; i++)
                if (_starDiamonds[i] != null)
                    _starDiamonds[i].color = i < stars ? StarLit : StarDim;
            if (_starCount != null) _starCount.text = stars + "/3";

            // Destruction: number + fill bar.
            int pct = Mathf.Clamp(Mathf.RoundToInt(s.DestructionPct * 100f), 0, 100);
            if (_destLabel != null) _destLabel.text = "Razed " + pct + "%";
            if (_destFill != null) _destFill.anchorMax = new Vector2(pct / 100f, 1f);

            // Troops alive / deployed (plain number).
            if (_troopLabel != null) _troopLabel.text = "Troops " + s.TroopsAlive + "/" + s.TroopsDeployed;
        }

        private static string FormatTime(float seconds)
        {
            int total = Mathf.Max(0, Mathf.RoundToInt(seconds));
            return $"{total / 60}:{total % 60:00}";
        }
    }
}
