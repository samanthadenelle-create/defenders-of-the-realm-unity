// =============================================================================
// BossHealthBar — top-of-screen apex-boss HP bar (CODE-BUILT uGUI).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.UI
//
// WHAT IT DOES:
//   Shows a wide HP bar across the top of the screen while an apex boss (the
//   DragonBoss "Syndrath the Devourer") is alive in the scene. The bar reflects
//   the boss's live HP fraction, tints red -> orange -> gold as HP drops through
//   the Phase 2 / Phase 3 thresholds, shows the boss name + phase caption + the
//   raw HP number (4200 max), and marks the two phase-change thresholds with pip
//   lines so the player can see when the dragon will enrage.
//
// WHY A REWRITE (2026-07-08, owner felt-test — "show the dragon boss health bar"):
//   The previous BossHealthBar was UIElements / UIDocument (UXML) based. Project
//   law (CLAUDE.md §8 / PIPELINE_STATE.md): UXML / UIDocument HUDs do NOT render
//   in player builds — so the old bar was effectively dead in a build. It is also
//   never wired to a live DragonBoss anywhere. This rewrite is:
//     * CODE-BUILT uGUI (Canvas + Image + TMP) — mirrors FloatingHealthBar's
//       "no UXML, renders in builds" pattern.
//     * SELF-BOOTSTRAPPING + SELF-DISCOVERING — a RuntimeInitializeOnLoadMethod
//       spawns exactly one persistent instance; it polls for a DragonBoss and
//       shows/hides itself. ZERO prefab wiring, no drag-drop, no WaveManager edit
//       (owner rule: never manual/drag-drop wiring).
//     * ZERO DragonBoss edits — reads only DragonBoss's public surface
//       (Hp / MaxHp / HpFraction / Phase / IsAlive). The dragon has no HP-changed
//       event, so the bar polls each frame WHILE VISIBLE (cheap: one component
//       read; discovery is throttled to ~2 Hz when no boss is present).
//
// LAYERING: its own ScreenSpaceOverlay Canvas at a high sort order so it sits
//   above the gameplay HUD; non-interactive (no GraphicRaycaster) so it never
//   eats input. Built lazily on the first boss sighting, then just hidden.
// =============================================================================

using DeNelle.Core.Diagnostics;
using DeNelle.Core.UI;
using DeNelle.Village;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeNelle.Village.UI
{
    /// <summary>
    /// Code-built uGUI apex-boss HP bar. Self-bootstraps and auto-discovers the
    /// live <see cref="DragonBoss"/>; shows while the boss is alive and hides when
    /// it dies or leaves the scene. See file header.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BossHealthBar : MonoBehaviour
    {
        // HP-fraction thresholds marked with phase pips (mirror DragonBoss phases).
        private const float MidThreshold  = 0.60f;   // Phase 1 -> 2
        private const float CritThreshold = 0.25f;   // Phase 2 -> 3 (enrage)

        // Phase-pip tints — kept distinct by LUMINANCE + position (owner is colorblind), not hue alone.
        private static readonly Color PipMid  = new Color(1f, 0.60f, 0.10f, 0.90f);
        private static readonly Color PipCrit = new Color(1f, 0.90f, 0.10f, 0.95f);

        // ── Cached UI ──────────────────────────────────────────────────────────
        private Canvas _canvas;
        private GameObject _panel;      // Obsidian chrome root — toggled for show/hide
        private ElarionUiKit.BarHandle _hpBar;   // Obsidian Health bar — fill driven via SetImmediate
        private TMP_Text _nameLabel;
        private TMP_Text _hpLabel;
        private bool _built;

        // ── Target ──────────────────────────────────────────────────────────────
        private DragonBoss _dragon;
        private DragonPhase _lastPhase = DragonPhase.Circling;
        private const string BossDisplayName = "Syndrath the Devourer";

        // Discovery throttle while no boss is present (avoids a per-frame FindObject).
        private const float DiscoverInterval = 0.5f;
        private float _discoverTimer;

        // ── Bootstrap ────────────────────────────────────────────────────────────
        private static BossHealthBar _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("BossHealthBar (bootstrapped)");
            _instance = go.AddComponent<BossHealthBar>();
            Object.DontDestroyOnLoad(go);
        }

        // ── Lifecycle ────────────────────────────────────────────────────────────
        private void Awake()
        {
            if (_instance == null) _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            // Discover / re-discover the boss when we don't have a live one.
            if (_dragon == null || !_dragon.IsAlive)
            {
                _discoverTimer -= Time.unscaledDeltaTime;
                if (_discoverTimer <= 0f)
                {
                    _discoverTimer = DiscoverInterval;
                    var found = FindFirstObjectByType<DragonBoss>();
                    if (found != null && found.IsAlive)
                    {
                        _dragon = found;
                        ShowFor(found);
                    }
                    else if (_built && _panel != null && _panel.activeSelf)
                    {
                        Hide();               // boss gone / dead -> hide
                    }
                }
                if (_dragon == null || !_dragon.IsAlive) return;
            }

            // Live poll while visible — the dragon exposes no HP-changed event.
            // Drive the Obsidian bar via its handle (the ONE sanctioned §1.1 path:
            // fillAmount = cur/max); SetImmediate = no ease for a per-frame sweep.
            if (_hpBar != null) _hpBar.SetImmediate(_dragon.Hp, _dragon.MaxHp);
            if (_hpLabel != null)
                _hpLabel.text = Mathf.CeilToInt(_dragon.Hp) + " / " + Mathf.CeilToInt(_dragon.MaxHp);

            var phase = _dragon.Phase;
            if (phase != _lastPhase && _nameLabel != null)
            {
                _lastPhase = phase;
                _nameLabel.text = BossDisplayName + "   " + PhaseLabel(phase);
            }

            if (!_dragon.IsAlive) Hide();
        }

        // ── Public API (also callable directly, e.g. from a WaveManager hook) ────

        /// <summary>Show the bar for a specific <see cref="DragonBoss"/>.</summary>
        public void ShowFor(DragonBoss dragon)
        {
            if (dragon == null) return;
            BuildIfNeeded();
            _dragon = dragon;
            _lastPhase = dragon.Phase;
            if (_nameLabel != null)
                _nameLabel.text = BossDisplayName + "   " + PhaseLabel(dragon.Phase);
            if (_panel != null) _panel.SetActive(true);
            FlowTrace.Step("BossBar", "shown for '" + dragon.BossId + "' HP " +
                           Mathf.CeilToInt(dragon.Hp) + "/" + Mathf.CeilToInt(dragon.MaxHp));
        }

        /// <summary>Hide the boss HP bar.</summary>
        public void Hide()
        {
            if (_panel != null) _panel.SetActive(false);
            _dragon = null;
            FlowTrace.Step("BossBar", "hidden (boss absent/dead)");
        }

        // ── UI construction (code-built uGUI — no UXML) ─────────────────────────

        private void BuildIfNeeded()
        {
            if (_built) return;
            _built = true;

            // Own overlay canvas — high sort order, non-interactive.
            var canvasGo = new GameObject("BossHealthBarCanvas",
                typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 5000;   // above the gameplay HUD
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // ── Container: OBSIDIAN chrome (near-black panel + gold trim) ────────
            // Mirrors ElarionUiKit's own procedural Obsidian panel recipe — a gold-
            // trim rect with a near-black fill inset by the trim thickness — using the
            // kit's PUBLIC chrome constants + builders (ObsidianTrim / ObsidianFill /
            // ObsidianTrimPx / AddImage / AddInnerRim). Built this way rather than
            // BuildObsidianPanel because that bakes in a modal Close button + backdrop,
            // which a passive top-of-screen HUD HP bar must NOT carry. Top-centre, ~60% wide.
            _panel = ElarionUiKit.AddImage(canvasGo.transform, "BossBarPanel",
                new Vector2(0.20f, 0.875f), new Vector2(0.80f, 0.978f),
                ElarionUiKit.ObsidianTrim, rounded: true);
            var trimImg = _panel.GetComponent<Image>();
            if (trimImg != null) trimImg.raycastTarget = false;

            var card = ElarionUiKit.AddImage(_panel.transform, "PanelFill",
                Vector2.zero, Vector2.one, ElarionUiKit.ObsidianFill, rounded: true);
            var cardRt = (RectTransform)card.transform;
            cardRt.offsetMin = new Vector2(ElarionUiKit.ObsidianTrimPx, ElarionUiKit.ObsidianTrimPx);
            cardRt.offsetMax = new Vector2(-ElarionUiKit.ObsidianTrimPx, -ElarionUiKit.ObsidianTrimPx);
            var cardImg = card.GetComponent<Image>();
            if (cardImg != null) cardImg.raycastTarget = false;
            ElarionUiKit.AddInnerRim(card, ElarionUiKit.ObsidianTrim);   // soft gold inner rim
            var content = card.transform;

            // Info row: boss name (gilt, left) + HP number (parchment, right) — kit
            // Label builder handles font-safety + the mobile readable floor (>=40px here).
            _nameLabel = ElarionUiKit.Label(content, BossDisplayName,
                0.52f, 0.95f, ElarionUi.Gilt, ElarionUi.FontBody,
                TextAlignmentOptions.MidlineLeft, x0: 0.04f, x1: 0.72f, bold: true);
            _hpLabel = ElarionUiKit.Label(content, "",
                0.52f, 0.95f, ElarionUi.Parchment, ElarionUi.FontLabel,
                TextAlignmentOptions.MidlineRight, x0: 0.72f, x1: 0.96f, bold: true);

            // HP fill: THE Obsidian bar. ObsidianBarKind.Health = the red/health kind
            // (ObsidianBarTint(Health) => ElarionUi.HpRed) — reads as enemy/boss HP.
            // framed:false so the wide bar shows just the recessed Obsidian track + red
            // fill inside our gold-trim panel (the ornate vitals silhouette is sized for
            // a small nameplate bar and distorts stretched this wide). Drive via BarHandle.
            var barHost = new GameObject("HpBarHost", typeof(RectTransform));
            barHost.transform.SetParent(content, false);
            var barRt = (RectTransform)barHost.transform;
            barRt.anchorMin = new Vector2(0.03f, 0.12f);
            barRt.anchorMax = new Vector2(0.97f, 0.47f);
            barRt.offsetMin = Vector2.zero; barRt.offsetMax = Vector2.zero;
            _hpBar = ElarionUiKit.BuildObsidianBar(barHost.transform,
                ElarionUiKit.ObsidianBarKind.Health, Vector2.zero, Vector2.one,
                withValue: false, framed: false);
            _hpBar.SetImmediate(1f, 1f);

            // Phase pip markers across the bar track (60% + 25% enrage thresholds).
            if (_hpBar != null && _hpBar.track != null)
            {
                AddPip(_hpBar.track, MidThreshold,  PipMid);
                AddPip(_hpBar.track, CritThreshold, PipCrit);
            }

            _panel.SetActive(false);
        }

        // ── Small uGUI builders ─────────────────────────────────────────────────

        private static RectTransform NewRect(string name, Transform parent, Vector2 aMin, Vector2 aMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = aMin; rt.anchorMax = aMax;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return rt;
        }

        // A thin vertical pip at a fractional position across the track.
        private static void AddPip(Transform track, float fraction, Color color)
        {
            var rt = NewRect("Pip", track, new Vector2(fraction, 0f), new Vector2(fraction, 1f));
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(2.5f, 0f);   // 2.5px wide, full track height
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color; img.raycastTarget = false;
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private static string PhaseLabel(DragonPhase phase)
        {
            switch (phase)
            {
                case DragonPhase.Circling: return "Phase I";
                case DragonPhase.Stooping: return "Phase II";
                case DragonPhase.LastWing: return "ENRAGED";
                case DragonPhase.Falling:  return "Falling";
                default:                   return "";
            }
        }
    }
}
