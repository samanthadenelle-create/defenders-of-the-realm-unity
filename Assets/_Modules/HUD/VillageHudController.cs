// =============================================================================
// VillageHudController — drives the in-game village HUD overlay (Week 4).
// -----------------------------------------------------------------------------
// Architecture review item ARC-003: "DeNelle.HUD is an empty asmdef." This is
// the module's first real component — the controller behind VillageHud.uxml,
// the glanceable readout for the Week-4 "playable Wave 1" loop.
//
// MODULE ISOLATION (port spec Part 2) — IMPORTANT:
//   The HUD is a PASSIVE display. It owns NO gameplay state and never reaches
//   into DeNelle.Village (the Heart, the wave manager, the hero ability block).
//   The DeNelle.HUD asmdef references DeNelle.Core + UI Toolkit ONLY. All HUD
//   data is PUSHED IN by the integrator through the public setters below:
//
//     SetHeartHp(current, max)            — Heart HP bar
//     SetCrystals(amount)                 — crystal counter
//     SetWave(number, countdown)          — wave indicator + between-wave timer
//     SetAbilityCooldown(slot, rem, tot)  — one Q/W/E/R cooldown sweep
//     SetMana(current, max)               — hero mana pool
//
//   The "Build" button raises the BuildRequested UnityEvent — the integrator
//   hooks that to the village's BuildMenu.Open() (BuildMenu lives in
//   DeNelle.Village, so the wiring CANNOT happen here). See the integrator
//   notes at the foot of this file and docs/port-notes/hud-module.md.
//
// The four ability cells are built ONCE at runtime into the "ability-bar"
// container — VillageHud.uxml only supplies the bar shell + corner panels.
// =============================================================================

using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UIElements;

namespace DeNelle.HUD
{
    /// <summary>
    /// Drives the village in-game HUD: Heart HP bar, crystal counter, wave
    /// indicator, the Q/W/E/R hero ability bar and the Build button. A passive
    /// display — gameplay code pushes data in through the public setters; the
    /// HUD never reads gameplay modules. Lives on the village HUD
    /// <see cref="UIDocument"/>; wired by the village scene builder / integrator.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class VillageHudController : MonoBehaviour
    {
        /// <summary>Number of hero ability slots — the Q/W/E/R kit.</summary>
        public const int AbilitySlotCount = 4;

        [Header("UI")]
        [Tooltip("UIDocument hosting VillageHud.uxml. Falls back to the component on this GameObject.")]
        [SerializeField] private UIDocument _document;

        [Header("Heart HP thresholds (fraction of max)")]
        [Tooltip("At or below this HP fraction the Heart bar turns amber (warning).")]
        [SerializeField, Range(0f, 1f)] private float _heartWarningFraction = 0.5f;

        [Tooltip("At or below this HP fraction the Heart bar turns red (critical).")]
        [SerializeField, Range(0f, 1f)] private float _heartCriticalFraction = 0.25f;

        [Header("Wave countdown")]
        [Tooltip("Seconds remaining at or below which the countdown text turns amber + bold.")]
        [SerializeField, Min(0f)] private float _countdownUrgentSeconds = 3f;

        [Header("Repair prompt")]
        [Tooltip("Seconds the repair-feedback toast stays on screen before it auto-hides.")]
        [SerializeField, Min(0.5f)] private float _repairToastSeconds = 2.5f;

        [Header("Events")]
        [Tooltip("Raised when the player taps the Build button. The integrator hooks this " +
                 "to the village BuildMenu.Open() — the HUD cannot reference BuildMenu itself.")]
        public UnityEvent BuildRequested = new UnityEvent();

        [Tooltip("Raised when the player taps the repair prompt's Repair button. The integrator " +
                 "hooks this to WallRepairController.ConfirmRepair() (cross-module — see " +
                 "WallRepairSceneSetup.cs). The HUD cannot reference WallRepairController itself.")]
        public UnityEvent RepairConfirmRequested = new UnityEvent();

        [Tooltip("Raised when the player taps the repair prompt's Cancel button (or the prompt " +
                 "is dismissed). The integrator hooks this to WallRepairController.CancelRepair().")]
        public UnityEvent RepairCancelRequested = new UnityEvent();

        /// <summary>
        /// Raised when the player taps one of the Q/W/E/R ability slots in the
        /// HUD. Arg = slot index (0=Q, 1=W, 2=E, 3=R). The integrator hooks
        /// this to <c>HeroAbilities.TryCast</c> via a cross-asmdef bridge —
        /// the HUD cannot reference DeNelle.Village itself.
        /// </summary>
        [System.Serializable] public sealed class AbilitySlotEvent : UnityEvent<int> { }
        public AbilitySlotEvent AbilityRequested = new AbilitySlotEvent();

        // ── UXML element names — the binding contract with VillageHud.uxml ───
        private const string RootName = "village-hud-root";
        private const string HeartHpFillName = "heart-hp-fill";
        private const string HeartHpLabelName = "heart-hp-label";
        private const string CrystalCountName = "crystal-count";
        private const string WaveNumberName = "wave-number";
        private const string WaveCountdownName = "wave-countdown";
        private const string WaveCountdownTimerName = "wave-countdown-timer";
        private const string WaveCountdownIconName = "wave-countdown-icon";
        private const string AbilityBarName = "ability-bar";
        private const string ManaFillName = "mana-fill";
        private const string ManaLabelName = "mana-label";
        private const string BuildButtonName = "build-button";

        // Repair-prompt elements (Workstream B — player wall-repair mechanic).
        private const string RepairPromptName = "repair-prompt";
        private const string RepairPromptSubtitleName = "repair-prompt-subtitle";
        private const string RepairPromptCostName = "repair-prompt-cost";
        private const string RepairPromptConfirmName = "repair-prompt-confirm";
        private const string RepairPromptCancelName = "repair-prompt-cancel";
        private const string RepairToastName = "repair-toast";

        // ── USS class names — styled by VillageHud.uss ───────────────────────
        private const string HeartWarningClass = "bar-fill--heart-warning";
        private const string HeartCriticalClass = "bar-fill--heart-critical";
        private const string CountdownUrgentClass = "wave-countdown--urgent";
        private const string CountdownTimerUrgentClass = "wave-countdown-timer--urgent";
        private const string CountdownIconUrgentClass = "wave-countdown-icon--urgent";
        private const string RepairCostUnaffordableClass = "repair-prompt-cost--unaffordable";
        private const string RepairToastErrorClass = "repair-toast--error";
        private const string AbilitySlotClass = "ability-slot";
        private const string AbilitySlotReadyClass = "ability-slot--ready";
        private const string AbilitySlotCoolingClass = "ability-slot--cooling";
        private const string AbilityKeyClass = "ability-key";
        private const string AbilityIconClass = "ability-icon";
        private const string AbilityCooldownFillClass = "ability-cooldown-fill";
        private const string AbilityCooldownLabelClass = "ability-cooldown-label";

        // The Q/W/E/R hotkey labels + placeholder glyphs for the four slots.
        // Glyphs are visual stand-ins until ability icon art lands (Week 4+).
        private static readonly string[] SlotKeys = { "Q", "W", "E", "R" };
        private static readonly string[] SlotGlyphs = { "✦", "❄", "✚", "☄" };

        // ── Bound UI elements ────────────────────────────────────────────────
        private VisualElement _root;
        private VisualElement _heartHpFill;
        private Label _heartHpLabel;
        private Label _crystalCount;
        private Label _waveNumber;
        private Label _waveCountdown;
        private VisualElement _waveCountdownTimer;
        private Label _waveCountdownIcon;
        private Button _startWaveButton;
        private float _reachTimer;
        private int _reachRuns;
        private VisualElement _abilityBar;
        private VisualElement _manaFill;
        private Label _manaLabel;
        private Button _buildButton;

        // Repair-prompt elements (Workstream B — player wall-repair mechanic).
        private VisualElement _repairPrompt;
        private Label _repairPromptSubtitle;
        private Label _repairPromptCost;
        private Button _repairPromptConfirm;
        private Button _repairPromptCancel;
        private Label _repairToast;

        // Auto-hide bookkeeping for the repair-feedback toast.
        private float _repairToastHideAt = -1f;

        /// <summary>One built ability cell — the cooldown sweep + numeral handles.</summary>
        private struct AbilityCell
        {
            public VisualElement Slot;
            public VisualElement CooldownFill;
            public Label CooldownLabel;
            public Label KeyLabel;     // hotkey badge (Q/W/E/R or a class-specific key)
            public Label IconLabel;    // ability glyph
        }

        private readonly AbilityCell[] _abilityCells = new AbilityCell[AbilitySlotCount];
        private bool _bound;

        // ── WO-39/WO-40 animated-feedback state (Update-driven) ──────────────
        // Compass arms whose inbound direction is live — pulsed at ~1 Hz (WO-39).
        private readonly bool[] _compassActive = new bool[4]; // N, E, S, W
        // WO-40 wave-imminent vignette: breathes alpha while on, fades over ~0.5s off.
        private VisualElement _imminentVignette;
        private bool _imminentBreathing;       // breathe alpha while a wave is imminent
        private float _imminentFade = 0f;       // 0..1 fade envelope (1 = fully shown)
        private const float ImminentFadeOutPerSecond = 2f;  // ~0.5s fade-out
        // WO-40 compass alert: flash ALL arms amber at 2 Hz during the alert.
        private bool _compassImminent;

        // =====================================================================
        //  Lifecycle
        // =====================================================================

        private void Awake()
        {
            if (_document == null) _document = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            BindElements();
        }

        private void OnDisable()
        {
            if (_buildButton != null) _buildButton.clicked -= OnBuildClicked;
            if (_repairPromptConfirm != null) _repairPromptConfirm.clicked -= OnRepairConfirmClicked;
            if (_repairPromptCancel != null) _repairPromptCancel.clicked -= OnRepairCancelClicked;
            _bound = false;
        }

        private void Update()
        {
            // Re-assert HUD reachability for the first ~2s to catch overlay panels
            // that bootstrap AFTER this HUD binds (owner 2026-05-25: HUD dead — a
            // full-screen overlay root was swallowing clicks). Skips the BuildMenu,
            // which manages its own root pickingMode.
            if (_reachRuns < 5)
            {
                _reachTimer -= Time.unscaledDeltaTime;
                if (_reachTimer <= 0f) { _reachTimer = 0.4f; _reachRuns++; EnsureHudReachable(); }
            }

            // Auto-hide the repair-feedback toast once its dwell time elapses.
            if (_repairToastHideAt >= 0f && Time.unscaledTime >= _repairToastHideAt)
            {
                _repairToastHideAt = -1f;
                if (_repairToast != null)
                {
                    _repairToast.text = string.Empty;
                    _repairToast.style.display = DisplayStyle.None;
                }
            }

            // WO-39/WO-40 animated feedback — pulse the compass arms, breathe /
            // fade the wave-imminent vignette, and flash the compass during alerts.
            TickAnimatedFeedback();
        }

        // ── WO-39 + WO-40 per-frame animation tick ───────────────────────────
        /// <summary>
        /// Drives the time-based HUD juice each frame: the active compass arms
        /// pulse opacity at ~1 Hz (WO-39), the wave-imminent vignette breathes its
        /// alpha while on and fades out over ~0.5 s when cleared (WO-40), and the
        /// whole compass flashes amber at 2 Hz during a wave-imminent alert (WO-40).
        /// All effects use <c>Time.unscaledTime</c> so they animate even if the
        /// game is time-scaled during the alert.
        /// </summary>
        private void TickAnimatedFeedback()
        {
            float t = Time.unscaledTime;

            // WO-40 compass alert flash takes priority over the WO-39 pulse so the
            // whole rose reads as a single amber warning while a wave is imminent.
            if (_compassImminent)
            {
                TickCompassImminentFlash(t);
            }
            else
            {
                TickCompassPulse(t);
            }

            TickImminentVignette(t);
        }

        // WO-39: active arms pulse opacity 0.45→1.0 on a ~1 Hz sine; idle arms stay dim.
        private void TickCompassPulse(float t)
        {
            var rose = _root != null ? _root.Q<VisualElement>("compass-rose") : null;
            if (rose == null) return;

            // 0..1 sine at ~1 Hz.
            float wave = 0.5f + 0.5f * Mathf.Sin(t * Mathf.PI * 2f);
            float pulseAlpha = Mathf.Lerp(0.45f, 1f, wave);

            PulseCompassArm(rose, "compass-n", _compassActive[0], pulseAlpha);
            PulseCompassArm(rose, "compass-e", _compassActive[1], pulseAlpha);
            PulseCompassArm(rose, "compass-s", _compassActive[2], pulseAlpha);
            PulseCompassArm(rose, "compass-w", _compassActive[3], pulseAlpha);
        }

        private static void PulseCompassArm(VisualElement rose, string name, bool active, float pulseAlpha)
        {
            var lbl = rose.Q<Label>(name);
            if (lbl == null) return;
            lbl.style.color = active
                ? new Color(0.95f, 0.22f, 0.16f, pulseAlpha)  // attack-red, pulsing
                : new Color(1f, 1f, 1f, 0.22f);                // dim when idle
        }

        // WO-40: while a wave is imminent flash EVERY arm amber on a 2 Hz square-ish
        // pulse so the compass reads as a klaxon regardless of which way enemies come.
        private void TickCompassImminentFlash(float t)
        {
            var rose = _root != null ? _root.Q<VisualElement>("compass-rose") : null;
            if (rose == null) return;

            // 0..1 at 2 Hz; bias toward a flash by squaring the sine.
            float s = 0.5f + 0.5f * Mathf.Sin(t * Mathf.PI * 4f);
            float a = Mathf.Lerp(0.25f, 1f, s * s);
            var amber = new Color(1f, 0.74f, 0.18f, a);

            var n = rose.Q<Label>("compass-n"); if (n != null) n.style.color = amber;
            var e = rose.Q<Label>("compass-e"); if (e != null) e.style.color = amber;
            var sArm = rose.Q<Label>("compass-s"); if (sArm != null) sArm.style.color = amber;
            var w = rose.Q<Label>("compass-w"); if (w != null) w.style.color = amber;
        }

        // WO-40: breathe the vignette alpha 0.35→0.60 at ~1 Hz while imminent, then
        // fade the whole overlay out over ~0.5 s when cleared (not a snap).
        private void TickImminentVignette(float t)
        {
            if (_imminentVignette == null) return;

            if (_imminentBreathing)
            {
                if (_imminentFade < 1f)
                    _imminentFade = Mathf.Min(1f, _imminentFade + Time.unscaledDeltaTime * ImminentFadeOutPerSecond);
            }
            else
            {
                if (_imminentFade > 0f)
                    _imminentFade = Mathf.Max(0f, _imminentFade - Time.unscaledDeltaTime * ImminentFadeOutPerSecond);

                if (_imminentFade <= 0f)
                {
                    _imminentVignette.style.display = DisplayStyle.None;
                    return;
                }
            }

            // Breathing band 0.35→0.60, scaled by the fade envelope.
            float wave = 0.5f + 0.5f * Mathf.Sin(t * Mathf.PI * 2f);
            float breathAlpha = Mathf.Lerp(0.35f, 0.60f, wave);
            float alpha = breathAlpha * _imminentFade;

            var red = new Color(0.86f, 0.12f, 0.10f, alpha);
            var s = _imminentVignette.style;
            s.borderTopColor = red; s.borderBottomColor = red;
            s.borderLeftColor = red; s.borderRightColor = red;
        }

        // =====================================================================
        //  UI Toolkit binding
        // =====================================================================

        private void BindElements()
        {
            _root = _document != null ? _document.rootVisualElement : null;
            if (_root == null)
            {
                Debug.LogWarning("[VillageHudController] No UIDocument root — HUD will not display.");
                return;
            }

            _heartHpFill = _root.Q<VisualElement>(HeartHpFillName);
            _heartHpLabel = _root.Q<Label>(HeartHpLabelName);
            _crystalCount = _root.Q<Label>(CrystalCountName);
            _waveNumber = _root.Q<Label>(WaveNumberName);
            _waveCountdown = _root.Q<Label>(WaveCountdownName);
            _waveCountdownTimer = _root.Q<VisualElement>(WaveCountdownTimerName);
            _waveCountdownIcon = _root.Q<Label>(WaveCountdownIconName);
            _abilityBar = _root.Q<VisualElement>(AbilityBarName);
            _manaFill = _root.Q<VisualElement>(ManaFillName);
            _manaLabel = _root.Q<Label>(ManaLabelName);
            _buildButton = _root.Q<Button>(BuildButtonName);

            // Repair-prompt elements (Workstream B).
            _repairPrompt = _root.Q<VisualElement>(RepairPromptName);
            _repairPromptSubtitle = _root.Q<Label>(RepairPromptSubtitleName);
            _repairPromptCost = _root.Q<Label>(RepairPromptCostName);
            _repairPromptConfirm = _root.Q<Button>(RepairPromptConfirmName);
            _repairPromptCancel = _root.Q<Button>(RepairPromptCancelName);
            _repairToast = _root.Q<Label>(RepairToastName);

            if (_buildButton != null)
            {
                _buildButton.clicked -= OnBuildClicked; // guard against a double OnEnable
                _buildButton.clicked += OnBuildClicked;
            }

            if (_repairPromptConfirm != null)
            {
                _repairPromptConfirm.clicked -= OnRepairConfirmClicked;
                _repairPromptConfirm.clicked += OnRepairConfirmClicked;
            }
            if (_repairPromptCancel != null)
            {
                _repairPromptCancel.clicked -= OnRepairCancelClicked;
                _repairPromptCancel.clicked += OnRepairCancelClicked;
            }

            // The repair prompt + toast start hidden until the repair flow runs.
            HideRepairPrompt();
            if (_repairToast != null)
            {
                _repairToast.text = string.Empty;
                _repairToast.style.display = DisplayStyle.None;
            }

            BuildAbilityCells();
            BuildTriggerWaveButton();
            BuildStartWaveButton();
            EnsureHudReachable();
            MoveManaPanelToTopLeft();
            _bound = true;
            Debug.Log($"[VillageHudController] Bound. root={_root != null}, heart={_heartHpFill != null}, mana={_manaFill != null}, abilityBar={_abilityBar != null}");
        }

        // Visible, unmistakable START WAVE button (owner 2026-05-25: "gear icon so
        // I can trigger wave" — the only trigger was a non-obvious click on the
        // countdown text). Built in code + parented to the HUD root so it doesn't
        // depend on the UXML; calls the same ForceBeginNextWave reflection path.
        private void BuildStartWaveButton()
        {
            if (_root == null) return;
            if (_startWaveButton != null) { _startWaveButton.RemoveFromHierarchy(); _startWaveButton = null; }

            var btn = new Button(OnTriggerWaveClicked) { text = "⚔ START WAVE" };
            btn.name = "StartWaveButton";
            btn.pickingMode = PickingMode.Position;
            var s = btn.style;
            s.position = Position.Absolute;
            s.top = 110f;                                  // just under the wave timer / compass
            s.left = Length.Percent(50f);
            s.translate = new Translate(Length.Percent(-50f), 0f, 0f);
            s.paddingLeft = 18f; s.paddingRight = 18f; s.paddingTop = 9f; s.paddingBottom = 9f;
            s.backgroundColor = new Color(0.82f, 0.27f, 0.16f, 0.96f); // attack-red so it reads as 'send the wave'
            s.color = Color.white;
            s.unityFontStyleAndWeight = FontStyle.Bold;
            s.fontSize = 16f;
            s.borderTopLeftRadius = 9f; s.borderTopRightRadius = 9f;
            s.borderBottomLeftRadius = 9f; s.borderBottomRightRadius = 9f;
            _root.Add(btn);
            _startWaveButton = btn;
        }

        // ── WO-38: wave-complete celebration banner ──────────────────────────
        /// <summary>
        /// Flashes a centred "WAVE n REPELLED" banner that auto-dismisses after the
        /// player survives a wave. Built in code + parented to the HUD root (mirrors
        /// the DailyQuest toast). Called from the Village-side WaveFeedbackDirector
        /// by reflection (DeNelle.Village can't reference DeNelle.HUD).
        /// </summary>
        public void ShowWaveClearBanner(int waveNumber, int crystals)
        {
            if (_root == null) return;
            var banner = new Label(crystals > 0
                ? $"WAVE {waveNumber} REPELLED\n+{crystals} ◆"
                : $"WAVE {waveNumber} REPELLED");
            banner.pickingMode = PickingMode.Ignore;
            var s = banner.style;
            s.position = Position.Absolute;
            s.top = Length.Percent(32f);
            s.left = Length.Percent(50f);
            s.translate = new Translate(Length.Percent(-50f), 0f, 0f);
            s.paddingLeft = 26f; s.paddingRight = 26f; s.paddingTop = 14f; s.paddingBottom = 14f;
            s.backgroundColor = new Color(0.10f, 0.08f, 0.16f, 0.92f);
            s.color = new Color(1f, 0.86f, 0.45f);
            s.unityFontStyleAndWeight = FontStyle.Bold;
            s.fontSize = 26f;
            s.unityTextAlign = TextAnchor.MiddleCenter;
            s.whiteSpace = WhiteSpace.Normal;
            s.borderTopLeftRadius = 12f; s.borderTopRightRadius = 12f;
            s.borderBottomLeftRadius = 12f; s.borderBottomRightRadius = 12f;
            _root.Add(banner);
            banner.schedule.Execute(() => { if (banner != null) banner.RemoveFromHierarchy(); }).StartingIn(3600);
        }

        // ── WO-40: wave-imminent red edge vignette (breathing + fade) ─────────
        /// <summary>
        /// Shows/hides a full-screen red edge vignette warning a wave is imminent.
        /// UI Toolkit has no radial gradient, so this is a thick translucent red
        /// inset border on a pass-through overlay (created once, then toggled).
        /// While on, the alpha BREATHES ~0.35→0.60 at ~1 Hz; when set false it
        /// FADES out over ~0.5 s rather than snapping off (both driven from
        /// <see cref="TickImminentVignette"/> in Update). Called from
        /// WaveFeedbackDirector by reflection.
        /// </summary>
        public void SetWaveImminent(bool on)
        {
            if (_root == null) return;

            if (_imminentVignette == null)
                _imminentVignette = _root.Q<VisualElement>("wave-imminent-vignette");

            if (on)
            {
                if (_imminentVignette == null)
                {
                    _imminentVignette = new VisualElement { name = "wave-imminent-vignette" };
                    _imminentVignette.pickingMode = PickingMode.Ignore;
                    var s = _imminentVignette.style;
                    s.position = Position.Absolute;
                    s.top = 0f; s.left = 0f; s.right = 0f; s.bottom = 0f;
                    s.borderTopWidth = 40f; s.borderBottomWidth = 40f;
                    s.borderLeftWidth = 40f; s.borderRightWidth = 40f;
                    var red = new Color(0.86f, 0.12f, 0.10f, 0f); // alpha animates in via the fade envelope
                    s.borderTopColor = red; s.borderBottomColor = red;
                    s.borderLeftColor = red; s.borderRightColor = red;
                    _root.Add(_imminentVignette);
                }
                _imminentVignette.style.display = DisplayStyle.Flex;
                _imminentVignette.BringToFront();
                _imminentBreathing = true;   // Update fades-in then breathes the alpha
            }
            else
            {
                // Don't snap off — let TickImminentVignette fade the alpha to 0
                // over ~0.5 s, then hide the overlay.
                _imminentBreathing = false;
            }
        }

        /// <summary>
        /// WO-40 compass alert: flashes ALL compass arms amber at 2 Hz while a wave
        /// is imminent (driven from <see cref="TickCompassImminentFlash"/> in
        /// Update). Cleared on wave start, which restores the per-direction WO-39
        /// pulse. Called from WaveFeedbackDirector by reflection.
        /// </summary>
        public void SetCompassImminent(bool on)
        {
            _compassImminent = on;
            // Make sure the rose exists so the flash has arms to drive even before
            // any enemies have lit a direction.
            if (on && _root != null)
            {
                var rose = _root.Q<VisualElement>("compass-rose");
                if (rose == null) { rose = BuildCompassRose(); _root.Add(rose); }
            }
        }

        // ── WO-39: enemy-direction compass (pulsing) ─────────────────────────
        /// <summary>
        /// Lights the compass arms (N/E/S/W) toward which live enemies are
        /// attacking. Built once in code (top-centre, under the wave timer). The
        /// active arms PULSE their opacity at ~1 Hz (driven from
        /// <see cref="TickCompassPulse"/> in Update) so inbound directions read as
        /// live; idle arms stay dim. This setter only records which arms are
        /// active. Called from WaveFeedbackDirector by reflection.
        /// </summary>
        public void SetAttackDirections(bool n, bool e, bool s, bool w)
        {
            if (_root == null) return;
            var rose = _root.Q<VisualElement>("compass-rose");
            if (rose == null) { rose = BuildCompassRose(); _root.Add(rose); }

            _compassActive[0] = n;
            _compassActive[1] = e;
            _compassActive[2] = s;
            _compassActive[3] = w;

            // Seed the colours immediately so a freshly-built rose isn't blank for
            // a frame; the per-frame pulse then animates the active arms. Skipped
            // while a wave-imminent alert owns the rose (amber flash).
            if (!_compassImminent)
            {
                SetCompassArm(rose, "compass-n", n);
                SetCompassArm(rose, "compass-e", e);
                SetCompassArm(rose, "compass-s", s);
                SetCompassArm(rose, "compass-w", w);
            }
        }

        private VisualElement BuildCompassRose()
        {
            var rose = new VisualElement { name = "compass-rose" };
            rose.pickingMode = PickingMode.Ignore;
            var rs = rose.style;
            rs.position = Position.Absolute;
            rs.top = 150f;                         // below the wave timer + START WAVE button
            rs.left = Length.Percent(50f);
            rs.translate = new Translate(Length.Percent(-50f), 0f, 0f);
            rs.width = 64f; rs.height = 64f;

            AddCompassArm(rose, "compass-n", "▲", 22f, 0f);    // ▲ top
            AddCompassArm(rose, "compass-s", "▼", 22f, 44f);   // ▼ bottom
            AddCompassArm(rose, "compass-e", "▶", 44f, 22f);   // ▶ right
            AddCompassArm(rose, "compass-w", "◀", 0f, 22f);    // ◀ left
            return rose;
        }

        private static void AddCompassArm(VisualElement rose, string name, string glyph, float left, float top)
        {
            var lbl = new Label(glyph) { name = name };
            lbl.pickingMode = PickingMode.Ignore;
            var s = lbl.style;
            s.position = Position.Absolute;
            s.left = left; s.top = top;
            s.width = 20f; s.height = 20f;
            s.fontSize = 16f;
            s.unityTextAlign = TextAnchor.MiddleCenter;
            s.color = new Color(1f, 1f, 1f, 0.22f);   // dim when idle
            rose.Add(lbl);
        }

        private static void SetCompassArm(VisualElement rose, string name, bool active)
        {
            var lbl = rose.Q<Label>(name);
            if (lbl == null) return;
            lbl.style.color = active
                ? new Color(0.95f, 0.22f, 0.16f, 1f)   // attack-red when enemies inbound
                : new Color(1f, 1f, 1f, 0.22f);
        }

        // Owner 2026-05-25 ("HUD non-responsive — a panel makes them unreachable"):
        // a full-screen overlay UIDocument root with pickingMode=Position sits over
        // the HUD and swallows every click. Relax EVERY other panel's ROOT to
        // Ignore so clicks fall through to the real buttons; the buttons themselves
        // (children) keep their own pickingMode and stay clickable. The BuildMenu
        // is skipped — it toggles its own root (Position when open). Only logs the
        // panels it actually had to relax, so the culprit is obvious in the log.
        private void EnsureHudReachable()
        {
            foreach (var doc in UnityEngine.Object.FindObjectsOfType<UIDocument>(true))
            {
                if (doc == null || doc == _document) continue;
                var r = doc.rootVisualElement;
                if (r == null) continue;
                if (doc.gameObject.name.IndexOf("Build", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    continue; // BuildMenu manages its own root pickingMode
                if (r.pickingMode != PickingMode.Ignore)
                {
                    Debug.Log($"[VillageHudController] HUD-reach: '{doc.gameObject.name}' " +
                              $"sort={doc.sortingOrder} childCount={r.childCount} root Position->Ignore (was blocking clicks)");
                    r.pickingMode = PickingMode.Ignore;
                }
            }
        }

        /// <summary>
        /// Owner direction 2026-05-20 ("HP bar top left, mana is bottom —
        /// please move both to top left"): the UXML places mana in the
        /// hud-bottom strip above the ability bar; this runtime override
        /// reparents it to absolute-positioned top-left, just under the
        /// heart-hp card.
        /// </summary>
        private void MoveManaPanelToTopLeft()
        {
            if (_root == null) return;
            var manaPanel = _root.Q<VisualElement>("mana-panel");
            if (manaPanel == null) return;
            manaPanel.style.position = Position.Absolute;
            manaPanel.style.top = 64;       // under heart-hp card
            manaPanel.style.left = 16;
            manaPanel.style.width = 220;
        }

        /// <summary>
        /// Owner 2026-05-20 ("two boxes for timer, only using one"): the
        /// separate "▶ Start Wave" button doubled up the wave HUD. Instead
        /// of adding a second card, make the existing wave timer label
        /// clickable — single tap on the countdown skips the prepare phase
        /// and calls WaveManager.ForceBeginNextWave via reflection.
        /// </summary>
        private void BuildTriggerWaveButton()
        {
            if (_waveCountdownTimer != null)
            {
                _waveCountdownTimer.pickingMode = PickingMode.Position;
                _waveCountdownTimer.RegisterCallback<ClickEvent>(_ => OnTriggerWaveClicked());
            }
            if (_waveNumber != null)
            {
                _waveNumber.pickingMode = PickingMode.Position;
                _waveNumber.RegisterCallback<ClickEvent>(_ => OnTriggerWaveClicked());
            }
        }

        private static System.Type s_waveManagerType;
        private void OnTriggerWaveClicked()
        {
            try
            {
                if (s_waveManagerType == null)
                {
                    foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                    {
                        var t = asm.GetType("DeNelle.Village.WaveManager", false);
                        if (t != null) { s_waveManagerType = t; break; }
                    }
                }
                if (s_waveManagerType == null) { Debug.LogWarning("[VillageHudController] WaveManager type not found."); return; }
                var inst = UnityEngine.Object.FindObjectOfType(s_waveManagerType) as Component;
                if (inst == null) { Debug.LogWarning("[VillageHudController] No WaveManager in scene."); return; }
                var m = s_waveManagerType.GetMethod("ForceBeginNextWave");
                m?.Invoke(inst, null);
                Debug.Log("[VillageHudController] Trigger Wave button → ForceBeginNextWave.");
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[VillageHudController] Trigger Wave failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Builds the four Q/W/E/R cells into the "ability-bar" container once.
        /// Each cell carries a hotkey badge, a placeholder glyph, a bottom-anchored
        /// cooldown sweep and a seconds-remaining numeral.
        /// </summary>
        private void BuildAbilityCells()
        {
            if (_abilityBar == null) return;
            _abilityBar.Clear();

            for (int i = 0; i < AbilitySlotCount; i++)
            {
                var slot = new VisualElement { name = $"ability-slot-{i}" };
                slot.AddToClassList(AbilitySlotClass);
                slot.AddToClassList(AbilitySlotReadyClass);

                var icon = new Label(SlotGlyphs[i]) { name = "ability-icon" };
                icon.AddToClassList(AbilityIconClass);
                slot.Add(icon);

                // Cooldown sweep — drawn over the icon, height driven 0..100%.
                var cooldownFill = new VisualElement { name = "ability-cooldown-fill" };
                cooldownFill.AddToClassList(AbilityCooldownFillClass);
                cooldownFill.pickingMode = PickingMode.Ignore;
                slot.Add(cooldownFill);

                var cooldownLabel = new Label(string.Empty) { name = "ability-cooldown-label" };
                cooldownLabel.AddToClassList(AbilityCooldownLabelClass);
                cooldownLabel.pickingMode = PickingMode.Ignore;
                slot.Add(cooldownLabel);

                // Hotkey badge last so it paints above the sweep.
                var key = new Label(SlotKeys[i]) { name = "ability-key" };
                key.AddToClassList(AbilityKeyClass);
                key.pickingMode = PickingMode.Ignore;
                slot.Add(key);

                // Tap-to-cast — slot raises AbilityRequested(index). The
                // bridge in DeNelle.Village forwards to HeroAbilities.TryCast.
                int slotIndex = i;
                slot.pickingMode = PickingMode.Position;
                slot.RegisterCallback<PointerDownEvent>(_ => AbilityRequested?.Invoke(slotIndex));

                _abilityBar.Add(slot);
                _abilityCells[i] = new AbilityCell
                {
                    Slot = slot,
                    CooldownFill = cooldownFill,
                    CooldownLabel = cooldownLabel,
                    KeyLabel = key,
                    IconLabel = icon,
                };
            }
        }

        /// <summary>
        /// WO-36 (visual half): retargets one ability slot's hotkey badge, glyph
        /// and (optionally) name to the active hero's loadout — so the bar stops
        /// showing the hard-coded Mage kit for a Knight/Ranger. The Village-side
        /// HeroAbilitiesHudBridge resolves each slot via
        /// <c>AbilityCatalog.Find(heroClass, slot)</c> and pushes the per-slot
        /// key/glyph/name in (by reflection, mirroring SetAbilityCooldown/SetMana).
        /// A null/empty <paramref name="glyph"/> or <paramref name="key"/> leaves
        /// that part of the cell unchanged. <paramref name="name"/> is stored as
        /// the slot's tooltip for hover/inspection (art-light placeholder).
        /// </summary>
        public void SetAbilitySlot(int slot, string key, string glyph, string name)
        {
            if (slot < 0 || slot >= AbilitySlotCount) return;
            AbilityCell cell = _abilityCells[slot];
            if (cell.Slot == null) return;

            if (cell.KeyLabel != null && !string.IsNullOrEmpty(key))
                cell.KeyLabel.text = key;

            if (cell.IconLabel != null && !string.IsNullOrEmpty(glyph))
                cell.IconLabel.text = glyph;

            if (!string.IsNullOrEmpty(name))
                cell.Slot.tooltip = name;
        }

        // =====================================================================
        //  Public API — the integrator pushes HUD data through these setters.
        // =====================================================================

        /// <summary>
        /// Sets the Heart HP bar. <paramref name="current"/> and
        /// <paramref name="max"/> are in whatever units the caller tracks
        /// (e.g. the Heart's 0-100 HP) — the bar fills by their ratio and the
        /// label shows the rounded values. The bar tints amber then red as HP
        /// crosses the warning / critical fractions.
        /// </summary>
        public void SetHeartHp(float current, float max)
        {
            float safeMax = Mathf.Max(1f, max);
            float clamped = Mathf.Clamp(current, 0f, safeMax);
            float fraction = clamped / safeMax;

            SetBarWidth(_heartHpFill, fraction);

            if (_heartHpFill != null)
            {
                bool critical = fraction <= _heartCriticalFraction;
                bool warning = !critical && fraction <= _heartWarningFraction;
                _heartHpFill.EnableInClassList(HeartCriticalClass, critical);
                _heartHpFill.EnableInClassList(HeartWarningClass, warning);
            }

            if (_heartHpLabel != null)
                _heartHpLabel.text = $"{Mathf.RoundToInt(clamped)} / {Mathf.RoundToInt(safeMax)}";
        }

        /// <summary>Sets the crystal counter. Negative values are clamped to zero.</summary>
        public void SetCrystals(int amount)
        {
            if (_crystalCount != null)
                _crystalCount.text = Mathf.Max(0, amount).ToString();
        }

        /// <summary>
        /// Sets the wave indicator. <paramref name="number"/> is the 1-based wave
        /// ordinal; <paramref name="countdown"/> is the seconds remaining in the
        /// between-wave Prepare Phase — pass 0 (or less) while a wave is active to
        /// clear the countdown line.
        /// </summary>
        public void SetWave(int number, float countdown)
        {
            if (_waveNumber != null)
                _waveNumber.text = $"Wave {Mathf.Max(1, number)}";

            if (_waveCountdown == null) return;

            bool counting = countdown > 0f;
            bool urgent = counting && countdown <= _countdownUrgentSeconds;

            if (counting)
            {
                // Compact "M:SS" / "Ns" timer text — small enough for the
                // top-centre pill (owner ask: a small timer at the top).
                int seconds = Mathf.CeilToInt(countdown);
                _waveCountdown.text = FormatCountdown(seconds);
            }
            else
            {
                _waveCountdown.text = string.Empty;
            }

            // Toggle the whole top-centre pill — it only shows during the
            // between-wave Prepare Phase, then collapses while a wave is active.
            if (_waveCountdownTimer != null)
            {
                _waveCountdownTimer.style.display =
                    counting ? DisplayStyle.Flex : DisplayStyle.None;
                _waveCountdownTimer.EnableInClassList(CountdownTimerUrgentClass, urgent);
            }
            _waveCountdown.EnableInClassList(CountdownUrgentClass, urgent);
            if (_waveCountdownIcon != null)
                _waveCountdownIcon.EnableInClassList(CountdownIconUrgentClass, urgent);
        }

        /// <summary>
        /// Formats a between-wave countdown into a compact timer string —
        /// <c>M:SS</c> when a minute or more remains, plain <c>Ns</c> below a
        /// minute. Kept short so the top-centre pill stays small.
        /// </summary>
        private static string FormatCountdown(int totalSeconds)
        {
            totalSeconds = Mathf.Max(0, totalSeconds);
            if (totalSeconds >= 60)
            {
                int m = totalSeconds / 60;
                int s = totalSeconds % 60;
                return $"{m}:{s:00}";
            }
            return $"{totalSeconds}s";
        }

        /// <summary>
        /// Sets the cooldown state of one ability slot (0 = Q, 1 = W, 2 = E,
        /// 3 = R). <paramref name="remaining"/> is the seconds left on cooldown
        /// and <paramref name="total"/> the ability's full cooldown duration; the
        /// cell shows a sweep + a seconds numeral while cooling and clears to a
        /// ready cell once <paramref name="remaining"/> reaches 0.
        /// </summary>
        public void SetAbilityCooldown(int slot, float remaining, float total)
        {
            if (slot < 0 || slot >= AbilitySlotCount) return;
            AbilityCell cell = _abilityCells[slot];
            if (cell.Slot == null) return;

            bool cooling = remaining > 0f && total > 0f;

            // Sweep height: 100% just cast → 0% ready. A vertical wipe stands in
            // for a radial sweep until ability icon art lands.
            float fraction = cooling ? Mathf.Clamp01(remaining / total) : 0f;
            if (cell.CooldownFill != null)
                cell.CooldownFill.style.height = Length.Percent(fraction * 100f);

            if (cell.CooldownLabel != null)
                cell.CooldownLabel.text = cooling ? Mathf.CeilToInt(remaining).ToString() : string.Empty;

            cell.Slot.EnableInClassList(AbilitySlotCoolingClass, cooling);
            cell.Slot.EnableInClassList(AbilitySlotReadyClass, !cooling);
        }

        /// <summary>
        /// Sets the hero mana bar. The bar fills by the current/max ratio and the
        /// label shows the rounded values.
        /// </summary>
        public void SetMana(float current, float max)
        {
            float safeMax = Mathf.Max(1f, max);
            float clamped = Mathf.Clamp(current, 0f, safeMax);

            SetBarWidth(_manaFill, clamped / safeMax);

            if (_manaLabel != null)
                _manaLabel.text = $"{Mathf.RoundToInt(clamped)} / {Mathf.RoundToInt(safeMax)}";
        }

        /// <summary>
        /// Enables / disables the Build button — e.g. the integrator may grey it
        /// out while a wave is active. Purely cosmetic; the button still raises
        /// <see cref="BuildRequested"/> only while enabled.
        /// </summary>
        public void SetBuildButtonEnabled(bool enabled)
        {
            if (_buildButton != null) _buildButton.SetEnabled(enabled);
        }

        /// <summary>True once VillageHud.uxml has been bound and the cells built.</summary>
        public bool IsBound => _bound;

        // =====================================================================
        //  Repair prompt — Workstream B (player wall-repair mechanic).
        //  A passive display, exactly like the rest of this HUD: the village
        //  WallRepairController pushes the prompt data in through these setters
        //  and listens to RepairConfirmRequested / RepairCancelRequested. The
        //  cross-module wiring is done by WallRepairSceneSetup.cs (the HUD asmdef
        //  cannot reference DeNelle.Village).
        // =====================================================================

        /// <summary>
        /// Shows the repair-confirm prompt for a selected structure.
        /// <paramref name="subtitle"/> is the prompt's sub-line (already composed
        /// + localized by the caller — the HUD displays it verbatim),
        /// <paramref name="crystalCost"/> is the crystal price, and
        /// <paramref name="affordable"/> greys the Repair button + reddens the
        /// cost when the player cannot pay.
        /// </summary>
        public void ShowRepairPrompt(string subtitle, int crystalCost, bool affordable)
        {
            if (_repairPrompt == null) return;

            if (_repairPromptSubtitle != null)
                _repairPromptSubtitle.text = subtitle ?? string.Empty;

            if (_repairPromptCost != null)
            {
                _repairPromptCost.text = Mathf.Max(0, crystalCost).ToString();
                _repairPromptCost.EnableInClassList(RepairCostUnaffordableClass, !affordable);
            }

            if (_repairPromptConfirm != null)
                _repairPromptConfirm.SetEnabled(affordable);

            _repairPrompt.style.display = DisplayStyle.Flex;
        }

        /// <summary>Hides the repair-confirm prompt.</summary>
        public void HideRepairPrompt()
        {
            if (_repairPrompt != null)
                _repairPrompt.style.display = DisplayStyle.None;
        }

        /// <summary>True while the repair-confirm prompt is on screen.</summary>
        public bool IsRepairPromptVisible =>
            _repairPrompt != null && _repairPrompt.style.display == DisplayStyle.Flex;

        /// <summary>
        /// Shows a brief repair-feedback toast (success or insufficient-funds).
        /// <paramref name="isError"/> flips the toast to the red error look. The
        /// toast auto-hides after <c>_repairToastSeconds</c>. An empty / null
        /// message hides the toast immediately.
        /// </summary>
        public void ShowRepairFeedback(string message, bool isError)
        {
            if (_repairToast == null) return;

            if (string.IsNullOrEmpty(message))
            {
                _repairToast.text = string.Empty;
                _repairToast.style.display = DisplayStyle.None;
                _repairToastHideAt = -1f;
                return;
            }

            _repairToast.text = message;
            _repairToast.EnableInClassList(RepairToastErrorClass, isError);
            _repairToast.style.display = DisplayStyle.Flex;
            _repairToastHideAt = Time.unscaledTime + _repairToastSeconds;
        }

        // =====================================================================
        //  Internals
        // =====================================================================

        /// <summary>Forwards the Build button click to the BuildRequested event.</summary>
        private void OnBuildClicked()
        {
            int listeners = BuildRequested?.GetPersistentEventCount() ?? 0;
            Debug.Log("[VillageHud] Build CLICK — persistent listeners: " + listeners);
            BuildRequested?.Invoke();
            // Belt-and-braces: also poke BuildMenu directly via reflection in
            // case the bridge listener wasn't wired this session (owner
            // 2026-05-20: "complete build and skillset not clickable").
            try
            {
                System.Type t = null;
                foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    t = asm.GetType("DeNelle.Village.BuildMenu", false);
                    if (t != null) break;
                }
                if (t != null)
                {
                    var inst = UnityEngine.Object.FindObjectOfType(t) as Component;
                    if (inst != null)
                    {
                        var open = t.GetMethod("Open");
                        open?.Invoke(inst, null);
                        Debug.Log("[VillageHud] BuildMenu.Open invoked directly via reflection.");
                    }
                    else Debug.LogWarning("[VillageHud] BuildMenu instance not found in scene.");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[VillageHud] Direct BuildMenu.Open failed: " + ex.Message);
            }
        }

        /// <summary>Forwards the repair-prompt Repair button to RepairConfirmRequested.</summary>
        private void OnRepairConfirmClicked()
        {
            RepairConfirmRequested?.Invoke();
        }

        /// <summary>Forwards the repair-prompt Cancel button to RepairCancelRequested.</summary>
        private void OnRepairCancelClicked()
        {
            HideRepairPrompt();
            RepairCancelRequested?.Invoke();
        }

        /// <summary>Sets a bar fill's width as a percentage (0..1) of its track.</summary>
        private static void SetBarWidth(VisualElement fill, float fraction)
        {
            if (fill == null) return;
            fill.style.width = Length.Percent(Mathf.Clamp01(fraction) * 100f);
        }
    }
}

// =============================================================================
// INTEGRATOR NOTES — wiring the village HUD into the village scene.
// -----------------------------------------------------------------------------
// This file (and the DeNelle.HUD asmdef) deliberately cannot see DeNelle.Village,
// so the scene builder / VillageController owns every connection below:
//
//   1. Add a UIDocument to the village HUD GameObject; assign VillageHud.uxml as
//      its Source Asset. Add this VillageHudController component beside it.
//
//   2. Build button → BuildMenu:
//        hud.BuildRequested.AddListener(buildMenu.Open);
//      (BuildMenu.Open() is parameterless — a direct UnityEvent listener.)
//
//   3. Push data each frame / on change from the village sub-systems:
//        - HeartController : hud.SetHeartHp(heart.Hp, 100f);
//        - crystal balance : hud.SetCrystals(GameStateService.Instance.State.Resources.Crystals);
//                            (or subscribe to GameStateService.ResourcesChanged)
//        - WaveManager     : hud.SetWave(wave.CurrentWaveId, wave.CountdownRemaining);
//                            WaveManager also exposes OnCountdownTick / OnWaveStarted
//                            UnityEvents — bind those to refresh on change.
//        - HeroAbilities   : for each slot Q/W/E/R, hud.SetAbilityCooldown(
//                                (int)slot, hero.CooldownRemaining(slot), <ability cooldown>);
//                            and hud.SetMana(hero.Mana, hero.MaxMana);
//
//   4. Wall-repair prompt (Workstream B):
//        - WallRepairController raises PromptShown / PromptHidden / FeedbackShown;
//          wire those to hud.ShowRepairPrompt / hud.HideRepairPrompt /
//          hud.ShowRepairFeedback.
//        - hud.RepairConfirmRequested -> WallRepairController.ConfirmRepair();
//          hud.RepairCancelRequested  -> WallRepairController.CancelRepair().
//        The cross-module wiring is done by Assets/Editor/WallRepairSceneSetup.cs
//        (the HUD asmdef cannot reference DeNelle.Village).
//
// The HUD holds one short timer for the repair-feedback toast auto-hide; every
// other readout is integrator-driven. See docs/port-notes/hud-module.md.
// =============================================================================
