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
using DeNelle.Core;
using DeNelle.Core.HUD;
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
    public sealed class VillageHudController : MonoBehaviour, IVillageHud
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
        private const string AbilityNameClass = "ability-name";

        // The Q/W/E/R hotkey labels + placeholder glyphs for the four slots.
        // Glyphs are visual stand-ins until ability icon art lands (Week 4+).
        // IMPORTANT (DEF blank-buttons fix): the default UI Toolkit font has NO
        // dingbat coverage (✦ ❄ ✚ ☄ etc render as blank/.notdef boxes in WebGL
        // builds — the "no symbols on the spell buttons" report). So the ability
        // bar no longer relies on the glyph FONT to read: each cell draws a
        // CODE-BUILT coloured rune disc (StyleAbilityIcon) tinted by the ability's
        // element, and the glyph text uses an ASCII-safe symbol that ALWAYS renders
        // in the base font. The disc + colour is the real signal; the letter is a
        // bonus. SlotGlyphs are the default (Mage) ASCII symbols before the bridge
        // pushes the per-class loadout in.
        private static readonly string[] SlotKeys = { "Q", "W", "E", "R" };
        private static readonly string[] SlotGlyphs = { "*", "*", "+", "^" };

        // Per-slot fallback accent colours (Q arcane / W frost / E heal / R fire),
        // used to tint the rune disc before the bridge pushes the real per-ability
        // colour from abilities.json. Mirrors the Mage kit's accent hexes.
        private static readonly Color[] SlotAccent =
        {
            new Color(0.70f, 0.53f, 1f,    1f),  // Q — arcane violet (#b388ff)
            new Color(0.49f, 0.83f, 0.99f, 1f),  // W — frost blue   (#7dd3fc)
            new Color(1f,    0.82f, 0.48f, 1f),  // E — heal gold    (#ffd27a)
            new Color(1f,    0.44f, 0.26f, 1f),  // R — fire orange  (#ff7043)
        };

        // ── Elarion HUD palette (DEF-105) ─────────────────────────────────────
        // Shared with FloatingHealthBar, which documents the same values as "echoes
        // VillageHudController / the quest-panel cards". USS doesn't render in builds
        // (CLAUDE.md §8), so the vitals bars are styled in code from this palette so
        // they match the rest of the HUD instead of falling back to the bare theme.
        private static readonly Color PanelCard   = new Color(0.10f, 0.08f, 0.16f, 0.92f); // arcane-violet card
        private static readonly Color PanelRim    = new Color(1f,    0.86f, 0.45f, 0.85f); // themed gold rim
        private static readonly Color BarTrack    = new Color(0.18f, 0.16f, 0.24f, 0.95f); // empty bar track
        private static readonly Color BarValueTxt = new Color(0.96f, 0.93f, 0.82f, 1f);    // parchment-cream readout
        private static readonly Color HeartFill   = new Color(0.86f, 0.22f, 0.30f, 1f);    // life-crimson (Elarion vitals)
        private static readonly Color ManaFill    = new Color(0.36f, 0.55f, 0.95f, 1f);    // arcane-blue mana

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
        private Button _skillsButton;
        private float _reachTimer;
        private int _reachRuns;
        private VisualElement _abilityBar;
        private VisualElement _manaFill;
        private Label _manaLabel;
        private Button _buildButton;

        // ── Resource bar (DEF: on-screen Wood/Iron/Food/Gems) ────────────────
        // Code-built compact wallet readout, top-left, sitting just under the
        // heart card. Shows the four build-economy resources players spend on
        // building/tower upgrades. Gems REUSE the existing _crystalCount label
        // (never duplicated). Fed by HeartHudBridge.SetResources via reflection,
        // sourced from EconomyService.Snapshot (the real banked totals).
        private VisualElement _resourceBar;
        private Label _woodCount;
        private Label _ironCount;
        private Label _foodCount;

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
            public Label NameLabel;    // visible ability name beneath the glyph+key (WO-36)
        }

        private readonly AbilityCell[] _abilityCells = new AbilityCell[AbilitySlotCount];
        private bool _bound;

        // Last wave ordinal pushed through the IVillageHud.SetWave(int) path, so the
        // separate IVillageHud.SetCountdown(float) call can drive the existing
        // rich SetWave(number, countdown) renderer without losing the wave number.
        private int _lastWaveNumber = 1;

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

        // ── WO-112 ward-tether "forgetting" overlay (passive display only) ───
        // A lazily-built full-screen grey wash that fades in as the Keeper steps past
        // the furthest lit ward. NEVER gameplay — pure warmth-removal, fully reversible
        // (driven down to 0 the instant they step back inside reach). Picking-ignored so
        // it never eats input.
        private VisualElement _forgettingOverlay;
        private float _forgettingLevel;          // 0 warm … 1 the song silent
        // Last "Wards of the Marches" summary pushed by WardTetherService (Arcane Tower
        // readout). Stored so a Tower-panel binder can surface it; no own widget yet.
        private string _wardsReadout = "";
        private int _wardsLit, _wardsTotal;

        // =====================================================================
        //  Lifecycle
        // =====================================================================

        private void Awake()
        {
            if (_document == null) _document = GetComponent<UIDocument>();

            // DEF-104 root cause: nothing ever registered this controller as the
            // CoreServices.Hud (IVillageHud), so every cross-module HUD call —
            // including WaveFeedbackDirector's SetAttackDirections — no-oped against
            // a null Hud and the compass never appeared. Register ourselves here so
            // the Village-side bridges actually reach the live HUD.
            CoreServices.RegisterHud(this);
        }

        private void OnDestroy()
        {
            CoreServices.UnregisterHud(this);
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
                : CompassArmIdle;                              // dim-but-readable when idle
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
            BuildSkillsButton();
            EnsureHudReachable();
            MoveManaPanelToTopLeft();
            MoveActionBarToRight();   // mobile thumb layout: actions bottom-RIGHT (opposite the joystick)
            ApplyElarionTheme();   // DEF-105: code-built styling for the vitals bars (USS doesn't render in builds)
            EnsureCompassRose();   // DEF-104: build the direction rose up-front so it's always on screen
            BuildResourceBar();    // DEF: on-screen Wood/Iron/Food/Gems wallet readout (code-built — USS doesn't render in builds)
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

            // No leading dingbat — the base build font has no ⚔ glyph (renders
            // blank). A plain bold label reads clearly on mobile (DEF blank-buttons).
            var btn = new Button(OnTriggerWaveClicked) { text = "START WAVE" };
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

        // Visible "⊕ Skills" button that opens the hero talent tree (the
        // HeroTalentPanel). Players couldn't find the tree — the only way in was
        // the T hotkey, undiscoverable in a build. Built in code + parented to the
        // HUD root (mirrors BuildStartWaveButton). Anchored top-left under the
        // heart/mana panel (mana sits at top=64, width=220) so it clears the
        // centred START WAVE button. On click it finds the HeroTalentPanel in the
        // scene and calls Toggle(); the T hotkey (HeroTalentPanelBootstrap) still
        // works alongside it.
        private void BuildSkillsButton()
        {
            if (_root == null) return;
            if (_skillsButton != null) { _skillsButton.RemoveFromHierarchy(); _skillsButton = null; }

            // No leading dingbat — the base build font has no ⊕ glyph (renders
            // blank). A "+ Skills" reads as 'add/spend skill points' and the '+'
            // IS in the base font (DEF blank-buttons fix).
            var btn = new Button(OnSkillsClicked) { text = "+ Skills" };
            btn.name = "SkillsButton";
            btn.pickingMode = PickingMode.Position;
            var s = btn.style;
            s.position = Position.Absolute;
            s.top = 110f;                                  // under the heart + mana panels (mana ends ~96)
            s.left = 16f;                                  // top-left column, lines up with the mana panel
            s.paddingLeft = 16f; s.paddingRight = 16f; s.paddingTop = 8f; s.paddingBottom = 8f;
            s.backgroundColor = new Color(0.36f, 0.24f, 0.52f, 0.96f); // arcane-violet so it reads as 'hero powers'
            s.color = Color.white;
            s.unityFontStyleAndWeight = FontStyle.Bold;
            s.fontSize = 15f;
            s.borderTopLeftRadius = 9f; s.borderTopRightRadius = 9f;
            s.borderBottomLeftRadius = 9f; s.borderBottomRightRadius = 9f;
            _root.Add(btn);
            _skillsButton = btn;
        }

        // Finds the HeroTalentPanel in the scene and toggles it. The panel lives in
        // the same DeNelle.HUD assembly, so it's referenced directly (no reflection
        // bridge needed). Keeps parity with the T hotkey, which also calls Toggle().
        private void OnSkillsClicked()
        {
            CoreServices.Audio?.PlayUiClick();   // DEF-183: UI click feedback
            var panel = UnityEngine.Object.FindAnyObjectByType<HeroTalentPanel>();
            if (panel == null)
            {
                Debug.LogWarning("[VillageHudController] Skills button: no HeroTalentPanel in scene.");
                return;
            }
            panel.Toggle();
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

            // Drive the compass amber-flash alongside the vignette so the imminent
            // alert reads on the direction rose too. The Village side reaches this
            // through IVillageHud.SetWaveImminent (SetCompassImminent is HUD-internal
            // and not on the interface), so couple them here.
            SetCompassImminent(on);

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

        // =====================================================================
        //  IVillageHud (DeNelle.Core.HUD) — the cross-module surface the Village
        //  bridges (WaveHudBridge, WaveFeedbackDirector) call via CoreServices.Hud.
        //  -------------------------------------------------------------------
        //  SetCrystals(int) and SetAttackDirections(bool,bool,bool,bool) already
        //  match the interface and satisfy it implicitly. The members below differ
        //  in signature from this HUD's richer public setters, so they're EXPLICIT
        //  interface implementations that forward to the existing renderers —
        //  keeping the reflection-based bridges (Heart/Hero/Wall) untouched.
        // =====================================================================

        /// <summary>IVillageHud: set the current wave ordinal (countdown unchanged).</summary>
        void IVillageHud.SetWave(int waveNumber)
        {
            _lastWaveNumber = Mathf.Max(1, waveNumber);
            // Preserve any countdown currently shown by re-reading the timer text is
            // brittle; instead just refresh the wave label and leave the countdown to
            // the dedicated SetCountdown call (the two are pushed together by the
            // WaveHudBridge each tick).
            if (_waveNumber != null)
                _waveNumber.text = $"Wave {_lastWaveNumber}";
        }

        /// <summary>IVillageHud: drive the between-wave countdown timer.</summary>
        void IVillageHud.SetCountdown(float secondsRemaining)
        {
            // Reuse the full wave renderer (label + pill + urgency styling) with the
            // last wave number we were given.
            SetWave(_lastWaveNumber, secondsRemaining);
        }

        /// <summary>IVillageHud: set the Heart bar from a 0..1 normalised fraction.</summary>
        void IVillageHud.SetHeartHp(float normalisedHp)
        {
            float frac = Mathf.Clamp01(normalisedHp);
            // The public renderer works in current/max units; feed it a 0..100 scale
            // so the existing warning/critical tinting thresholds apply unchanged.
            SetHeartHp(frac * 100f, 100f);
        }

        /// <summary>IVillageHud: show the wave-clear banner.</summary>
        void IVillageHud.ShowWaveClearBanner(int waveNumber, int enemiesDefeated, string flavourLine)
        {
            // The existing banner takes (waveNumber, crystals); the interface passes
            // enemiesDefeated + a flavour line. Surface the crystal balance the
            // Village side already credits — forwarded as the count it expects.
            ShowWaveClearBanner(waveNumber, enemiesDefeated);
        }

        /// <summary>IVillageHud: hide the wave-clear banner (auto-dismisses; no-op safe).</summary>
        void IVillageHud.HideWaveClearBanner()
        {
            // The code-built banner removes itself on a schedule; nothing persistent
            // to tear down. Kept as an explicit no-op so the interface is satisfied.
        }

        /// <summary>IVillageHud: show the repair prompt for a damaged wall.</summary>
        void IVillageHud.ShowRepairPrompt(string wallLabel, float damagePercent)
        {
            // Bridge the Core signature to the richer in-HUD prompt. We don't know the
            // crystal cost here, so compose a subtitle and assume affordable; the
            // Village-side WallRepairHudBridge drives the full ShowRepairPrompt
            // (subtitle, cost, affordable) overload when it has those values.
            string subtitle = string.IsNullOrEmpty(wallLabel)
                ? $"Damaged ({Mathf.RoundToInt(Mathf.Clamp01(damagePercent) * 100f)}%)"
                : $"{wallLabel} — {Mathf.RoundToInt(Mathf.Clamp01(damagePercent) * 100f)}% damaged";
            ShowRepairPrompt(subtitle, 0, true);
        }

        /// <summary>IVillageHud (WO-112): drive the ward-tether "forgetting" wash, 0..1.
        /// Pure presentation — a grey full-screen fade that creeps in past the furthest lit
        /// ward and lifts the instant the Keeper steps back inside reach. No gameplay.</summary>
        void IVillageHud.SetForgettingLevel(float level01)
        {
            _forgettingLevel = Mathf.Clamp01(level01);
            EnsureForgettingOverlay();
            if (_forgettingOverlay != null)
            {
                // Quadratic ease so the early fray-edge is barely-there and the deep
                // forgetting reads as a real grey wash. Cap alpha so the screen never
                // fully blacks out (warmth removed, not vision).
                float a = _forgettingLevel * _forgettingLevel * 0.72f;
                _forgettingOverlay.style.backgroundColor = new Color(0.10f, 0.10f, 0.12f, a);
                _forgettingOverlay.style.display = a > 0.001f ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        /// <summary>IVillageHud (WO-112): store the Arcane Tower "Wards of the Marches"
        /// readout. Passive — held for a Tower-panel binder to surface; the HUD reads the
        /// tether, never the reverse.</summary>
        void IVillageHud.SetWardsReadout(int wardsLit, int wardsTotal, string summary)
        {
            _wardsLit = Mathf.Max(0, wardsLit);
            _wardsTotal = Mathf.Max(0, wardsTotal);
            _wardsReadout = summary ?? "";
        }

        /// <summary>Public read of the latest wards readout (for an Arcane Tower panel binder).</summary>
        public string WardsReadout => _wardsReadout;
        /// <summary>Lit / total ward counts from the latest WardTetherService push.</summary>
        public (int lit, int total) WardCounts => (_wardsLit, _wardsTotal);

        // Lazily build the full-screen forgetting wash on first use (so it overlays
        // everything yet ignores input). Parented to the HUD root; alpha driven above.
        private void EnsureForgettingOverlay()
        {
            if (_forgettingOverlay != null || _root == null) return;
            _forgettingOverlay = new VisualElement { name = "ward-forgetting-overlay" };
            _forgettingOverlay.pickingMode = PickingMode.Ignore;
            var s = _forgettingOverlay.style;
            s.position = Position.Absolute;
            s.left = 0; s.right = 0; s.top = 0; s.bottom = 0;
            s.backgroundColor = new Color(0.10f, 0.10f, 0.12f, 0f);
            s.display = DisplayStyle.None;
            _root.Add(_forgettingOverlay);
        }

        // SetWaveImminent(bool) below also satisfies IVillageHud; in addition to the
        // red edge vignette it now flashes the compass arms amber so the imminent
        // alert reads on the direction rose too (the Village side cannot reach the
        // HUD-internal SetCompassImminent directly via IVillageHud).

        // DEF-104: idle arms must stay READABLE (not near-invisible) so the rose
        // reads as a compass even between waves. Dim parchment-cream rather than the
        // old 0.22-alpha white that vanished against the village.
        private static readonly Color CompassArmIdle   = new Color(0.86f, 0.82f, 0.70f, 0.55f);
        private static readonly Color CompassArmActive = new Color(0.95f, 0.22f, 0.16f, 1f); // attack-red inbound

        /// <summary>
        /// DEF-104: builds the direction rose ONCE at bind time (was previously only
        /// built lazily the first time SetAttackDirections/SetCompassImminent ran, so
        /// with no live enemies it never appeared). Idempotent — re-finds an existing
        /// rose and never duplicates it.
        /// </summary>
        private void EnsureCompassRose()
        {
            if (_root == null) return;
            var rose = _root.Q<VisualElement>("compass-rose");
            if (rose == null) { rose = BuildCompassRose(); _root.Add(rose); }
            // Seed the idle look so a freshly-built rose isn't blank for a frame.
            if (!_compassImminent)
            {
                SetCompassArm(rose, "compass-n", _compassActive[0]);
                SetCompassArm(rose, "compass-e", _compassActive[1]);
                SetCompassArm(rose, "compass-s", _compassActive[2]);
                SetCompassArm(rose, "compass-w", _compassActive[3]);
            }
        }

        private VisualElement BuildCompassRose()
        {
            // Elarion-themed backing panel (arcane-violet card + gold rim) so the rose
            // reads as a deliberate HUD widget, consistent with the vitals panels.
            var rose = new VisualElement { name = "compass-rose" };
            rose.pickingMode = PickingMode.Ignore;
            var rs = rose.style;
            rs.position = Position.Absolute;
            rs.top = 150f;                         // below the wave timer + START WAVE button
            rs.left = Length.Percent(50f);
            rs.translate = new Translate(Length.Percent(-50f), 0f, 0f);
            rs.width = 72f; rs.height = 72f;
            rs.backgroundColor = PanelCard;
            rs.borderTopWidth = 1.5f; rs.borderBottomWidth = 1.5f;
            rs.borderLeftWidth = 1.5f; rs.borderRightWidth = 1.5f;
            rs.borderTopColor = PanelRim; rs.borderBottomColor = PanelRim;
            rs.borderLeftColor = PanelRim; rs.borderRightColor = PanelRim;
            rs.borderTopLeftRadius = 36f; rs.borderTopRightRadius = 36f;
            rs.borderBottomLeftRadius = 36f; rs.borderBottomRightRadius = 36f;

            AddCompassArm(rose, "compass-n", "▲", 26f, 4f);    // ▲ top
            AddCompassArm(rose, "compass-s", "▼", 26f, 48f);   // ▼ bottom
            AddCompassArm(rose, "compass-e", "▶", 48f, 26f);   // ▶ right
            AddCompassArm(rose, "compass-w", "◀", 4f, 26f);    // ◀ left
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
            s.color = CompassArmIdle;   // dim-but-readable when idle
            rose.Add(lbl);
        }

        private static void SetCompassArm(VisualElement rose, string name, bool active)
        {
            var lbl = rose.Q<Label>(name);
            if (lbl == null) return;
            lbl.style.color = active ? CompassArmActive : CompassArmIdle;
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
        /// Mobile thumb layout (owner 2026-06-02: "move the action buttons closer to the
        /// right" / "action buttons challenging"): anchors the ability cluster + Build button
        /// bottom-RIGHT — opposite the left movement joystick — and enlarges each cell for
        /// touch. Sizes are set in CODE because USS doesn't render in player builds
        /// (CLAUDE.md §8). Re-applied after every rebind (cells are rebuilt in BuildAbilityCells).
        /// </summary>
        private void MoveActionBarToRight()
        {
            if (_abilityBar != null)
            {
                // DIAMOND layout (console-style, thumb-friendly best practice): the four
                // ability cells sit around a centre — primary Q at the BOTTOM (closest to the
                // resting right thumb), W right, E top, R left — inside a bottom-right box.
                // Each cell is absolutely placed; sizes in code (USS doesn't render in builds).
                const float Cell = 72f;     // touch target (was a cramped row before)
                const float Reach = 54f;    // centre-to-cell spread (tightened so the
                                            // diamond + Build stack fits a portrait phone)
                float box = Cell + Reach * 2f;
                var s = _abilityBar.style;
                s.position = Position.Absolute;
                // DEF-134: lift the diamond OFF the very bottom-right corner — the
                // browser/WebGL fullscreen toggle lives there and was touching the
                // primary (Q) cell. right:24/bottom:64 keeps the whole cluster clear
                // of both the corner toggle and the screen edge on a portrait phone.
                s.right = 24; s.bottom = 64;
                s.left = StyleKeyword.Auto; s.top = StyleKeyword.Auto;
                s.width = box; s.height = box;

                float mid = (box - Cell) * 0.5f;
                // 0=Q bottom (primary), 1=W right, 2=E top, 3=R left
                Vector2[] slots =
                {
                    new Vector2(mid,        box - Cell),
                    new Vector2(box - Cell, mid),
                    new Vector2(mid,        0f),
                    new Vector2(0f,         mid),
                };
                int n = _abilityBar.childCount;
                for (int i = 0; i < n && i < 4; i++)
                {
                    var cs = _abilityBar.ElementAt(i).style;
                    cs.position = Position.Absolute;
                    cs.left = slots[i].x; cs.top = slots[i].y;
                    cs.right = StyleKeyword.Auto; cs.bottom = StyleKeyword.Auto;
                    cs.width = Cell; cs.height = Cell;
                    cs.marginLeft = 0; cs.marginRight = 0; cs.marginTop = 0; cs.marginBottom = 0;
                }
            }
            if (_buildButton != null)
            {
                // Build sits just ABOVE the diamond box, same right margin. DEF-134:
                // derive the offset from the SAME diamond metrics (bottom + box) plus
                // a 12px gap so the button never overlaps the top (E) ability cell —
                // the old hard-coded 18+(76+116)+8 no longer matched the box height.
                const float Cell = 72f, Reach = 54f, Gap = 12f;
                float diamondBox = Cell + Reach * 2f;   // = 180, must match the block above
                var b = _buildButton.style;
                b.position = Position.Absolute;
                b.right = 24; b.bottom = 64 + diamondBox + Gap;
                b.left = StyleKeyword.Auto; b.top = StyleKeyword.Auto;
                b.minWidth = 110; b.height = 42;
            }
        }

        /// <summary>
        /// DEF-105: code-built styling for the vitals bars so they match the Elarion
        /// panel aesthetic (arcane-violet card, gold rim, parchment readout, crimson
        /// life / arcane-blue mana fills). The UXML/USS look does NOT render in player
        /// builds (CLAUDE.md §8), so without this the heart/mana bars fell back to the
        /// bare default theme and looked off-brand. Applied once after bind; every
        /// element lookup is null-guarded so a trimmed/absent panel is a no-op.
        /// </summary>
        private void ApplyElarionTheme()
        {
            if (_root == null) return;

            // Heart (Elarion vitals) panel — the card chrome.
            StylePanelCard(_root.Q<VisualElement>("heart-panel"));
            StylePanelCard(_root.Q<VisualElement>("mana-panel"));

            // Panel captions ("Elarion", "Mana") in themed gold.
            StyleCaption(_root.Q<Label>("heart-title"));
            StyleCaption(_root.Q<Label>("mana-caption"));

            // Heart HP track + fill.
            StyleBarTrack(_root.Q<VisualElement>("heart-hp-track"));
            if (_heartHpFill != null)
            {
                var s = _heartHpFill.style;
                s.backgroundColor = HeartFill;     // default healthy fill; SetHeartHp adds warning/critical tint classes
                s.borderTopLeftRadius = 5f; s.borderBottomLeftRadius = 5f;
                s.borderTopRightRadius = 5f; s.borderBottomRightRadius = 5f;
                s.height = Length.Percent(100f);
            }

            // Mana track + fill.
            StyleBarTrack(_root.Q<VisualElement>("mana-track"));
            if (_manaFill != null)
            {
                var s = _manaFill.style;
                s.backgroundColor = ManaFill;
                s.borderTopLeftRadius = 5f; s.borderBottomLeftRadius = 5f;
                s.borderTopRightRadius = 5f; s.borderBottomRightRadius = 5f;
                s.height = Length.Percent(100f);
            }

            // Numeric readouts ("100 / 100", "10 / 10").
            StyleBarValue(_heartHpLabel);
            StyleBarValue(_manaLabel);
        }

        private static void StylePanelCard(VisualElement panel)
        {
            if (panel == null) return;
            var s = panel.style;
            s.backgroundColor = PanelCard;
            s.paddingLeft = 10f; s.paddingRight = 10f; s.paddingTop = 6f; s.paddingBottom = 6f;
            s.borderTopWidth = 1.5f; s.borderBottomWidth = 1.5f;
            s.borderLeftWidth = 1.5f; s.borderRightWidth = 1.5f;
            s.borderTopColor = PanelRim; s.borderBottomColor = PanelRim;
            s.borderLeftColor = PanelRim; s.borderRightColor = PanelRim;
            s.borderTopLeftRadius = 8f; s.borderTopRightRadius = 8f;
            s.borderBottomLeftRadius = 8f; s.borderBottomRightRadius = 8f;
        }

        private static void StyleCaption(Label caption)
        {
            if (caption == null) return;
            var s = caption.style;
            s.color = PanelRim;
            s.fontSize = 12f;
            s.unityFontStyleAndWeight = FontStyle.Bold;
            s.letterSpacing = 2f;
        }

        private static void StyleBarTrack(VisualElement track)
        {
            if (track == null) return;
            var s = track.style;
            s.backgroundColor = BarTrack;
            s.height = 12f;
            s.borderTopLeftRadius = 6f; s.borderTopRightRadius = 6f;
            s.borderBottomLeftRadius = 6f; s.borderBottomRightRadius = 6f;
            s.overflow = Overflow.Hidden;       // keep the fill inside the rounded track
        }

        private static void StyleBarValue(Label value)
        {
            if (value == null) return;
            var s = value.style;
            s.color = BarValueTxt;
            s.fontSize = 12f;
            s.unityFontStyleAndWeight = FontStyle.Bold;
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
            CoreServices.Audio?.PlayUiClick();   // DEF-183: UI click feedback
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
                icon.pickingMode = PickingMode.Ignore;
                // Code-built coloured rune disc so the cell reads as a symbol even
                // when the glyph font misses the dingbat (USS doesn't render in
                // builds — CLAUDE.md §8). Tinted to the slot's default element until
                // the bridge pushes the real per-ability accent via SetAbilitySlot.
                StyleAbilityIcon(icon, SlotAccent[i]);
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

                // WO-36 (FAIL #2 fix): a VISIBLE ability-name label beneath the
                // glyph+key so the bar tells the player what each slot does — the
                // name used to live ONLY in a hover tooltip (invisible in a build).
                // Absolute-anchored to the cell's bottom edge and styled in code so
                // it renders even without the USS rule (art-light placeholder).
                var nameLabel = new Label(string.Empty) { name = "ability-name" };
                nameLabel.AddToClassList(AbilityNameClass);
                nameLabel.pickingMode = PickingMode.Ignore;
                var ns = nameLabel.style;
                ns.position = Position.Absolute;
                ns.left = 0f; ns.right = 0f; ns.bottom = 1f;
                ns.fontSize = 9f;
                ns.color = new Color(0.92f, 0.92f, 0.98f, 0.95f);
                ns.unityTextAlign = TextAnchor.LowerCenter;
                ns.whiteSpace = WhiteSpace.Normal;          // wrap short names to 1-2 lines
                ns.unityFontStyleAndWeight = FontStyle.Bold;
                ns.overflow = Overflow.Hidden;              // clip if a name overruns the cell
                slot.Add(nameLabel);

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
                    NameLabel = nameLabel,
                };
            }
        }

        /// <summary>
        /// WO-36 (visual half): retargets one ability slot's hotkey badge, glyph,
        /// VISIBLE name label and tooltip to the active hero's loadout — so the bar
        /// stops showing the hard-coded Mage kit for a Knight/Ranger AND clearly
        /// tells the player what each slot does. The Village-side
        /// HeroAbilitiesHudBridge resolves each slot via
        /// <c>AbilityCatalog.Find(heroClass, slot)</c> and pushes the per-slot
        /// key/glyph/name/description in (by reflection, mirroring
        /// SetAbilityCooldown/SetMana). A null/empty <paramref name="glyph"/> or
        /// <paramref name="key"/> leaves that part of the cell unchanged.
        /// <paramref name="name"/> drives the always-visible name label beneath the
        /// glyph+key; <paramref name="description"/> (1-line effect blurb) is stored
        /// as the slot's hover tooltip.
        /// </summary>
        public void SetAbilitySlot(int slot, string key, string glyph, string name, string description)
        {
            SetAbilitySlot(slot, key, glyph, name, description, null);
        }

        /// <summary>
        /// Accent-aware overload (DEF blank-buttons fix). <paramref name="accentHex"/>
        /// is the ability's colour from abilities.json (e.g. "#b388ff"); it tints the
        /// code-built rune disc so each cell reads as a distinct coloured symbol even
        /// when the dingbat glyph isn't in the build font. A null/blank hex keeps the
        /// slot's existing element tint. The 5-arg overload above stays the public
        /// reflection target the HeroAbilitiesHudBridge already binds, and the bridge
        /// also resolves THIS 6-arg overload to push the per-ability colour.
        /// </summary>
        public void SetAbilitySlot(int slot, string key, string glyph, string name, string description, string accentHex)
        {
            if (slot < 0 || slot >= AbilitySlotCount) return;
            AbilityCell cell = _abilityCells[slot];
            if (cell.Slot == null) return;

            if (cell.KeyLabel != null && !string.IsNullOrEmpty(key))
                cell.KeyLabel.text = key;

            if (cell.IconLabel != null && !string.IsNullOrEmpty(glyph))
                cell.IconLabel.text = SymbolFor(glyph);

            // Re-tint the rune disc to the ability's accent colour so the cell is a
            // recognisable coloured badge regardless of glyph-font coverage.
            if (cell.IconLabel != null &&
                !string.IsNullOrEmpty(accentHex) &&
                ColorUtility.TryParseHtmlString(accentHex, out var accent))
            {
                StyleAbilityIcon(cell.IconLabel, accent);
            }

            // FAIL #2 fix: surface the ability NAME on a visible label (not just the
            // invisible-in-build tooltip), so each cell reads differently per class.
            if (cell.NameLabel != null && !string.IsNullOrEmpty(name))
                cell.NameLabel.text = name;

            // Hover tooltip carries the longer effect blurb for inspection; fall
            // back to the name when no description was supplied.
            if (!string.IsNullOrEmpty(description))
                cell.Slot.tooltip = description;
            else if (!string.IsNullOrEmpty(name))
                cell.Slot.tooltip = name;
        }

        // Draws the code-built "rune disc" behind an ability glyph: a rounded,
        // accent-tinted, ringed circle so the cell reads as a coloured symbol badge
        // even when the dingbat glyph isn't in the build font (USS doesn't render in
        // builds — CLAUDE.md §8; the "blank spell buttons" root cause). The glyph
        // text is forced to a high-contrast colour on top of the tint. Idempotent —
        // safe to call on build and on every per-class retint.
        private static void StyleAbilityIcon(Label icon, Color accent)
        {
            if (icon == null) return;
            var s = icon.style;

            // Disc fill: a darkened wash of the accent so the symbol stays legible;
            // ring: the accent at full strength so the colour reads at a glance.
            Color fill = new Color(accent.r * 0.45f + 0.06f, accent.g * 0.45f + 0.06f, accent.b * 0.45f + 0.06f, 0.96f);
            s.backgroundColor = fill;

            s.borderTopWidth = 2f; s.borderRightWidth = 2f;
            s.borderBottomWidth = 2f; s.borderLeftWidth = 2f;
            s.borderTopColor = accent; s.borderRightColor = accent;
            s.borderBottomColor = accent; s.borderLeftColor = accent;

            // Big radius → reads as a disc inside the square cell.
            s.borderTopLeftRadius = 28f; s.borderTopRightRadius = 28f;
            s.borderBottomLeftRadius = 28f; s.borderBottomRightRadius = 28f;

            // Inset the disc a touch from the cell edge + centre the glyph.
            s.marginTop = 6f; s.marginBottom = 6f; s.marginLeft = 6f; s.marginRight = 6f;
            s.unityTextAlign = TextAnchor.MiddleCenter;
            s.unityFontStyleAndWeight = FontStyle.Bold;
            s.fontSize = 26f;

            // High-contrast glyph: white on the dark wash so the ASCII symbol pops.
            s.color = new Color(0.98f, 0.97f, 1f, 1f);
        }

        // Maps an ability glyph to a symbol guaranteed to render in the BASE UI
        // Toolkit font (no dingbat coverage in builds). The canonical abilities.json
        // supplies pretty dingbats (✦ ❄ ✚ ☄ …) that come up blank in WebGL; we keep
        // their MEANING by translating each to an ASCII-safe stand-in. Unknown / safe
        // input passes through unchanged (so a future real-font icon set still works).
        private static string SymbolFor(string glyph)
        {
            if (string.IsNullOrEmpty(glyph)) return "*";
            switch (glyph)
            {
                case "✦": case "✧": case "✶": case "✷": return "*";  // arcane / strike spark
                case "❄": case "❅": case "❆":            return "*";  // frost burst
                case "✚": case "✛": case "✝": case "+":  return "+";  // heal cross
                case "☄": case "✸": case "✹":            return "^";  // meteor / blast
                case "⚔": case "⚒":                      return "/";  // melee
                default:
                    // Already ASCII-printable? keep it. Otherwise fall back to the
                    // first letter (e.g. an unmapped dingbat) so the cell is never
                    // a blank .notdef box.
                    char c = glyph[0];
                    if (c >= 0x20 && c < 0x7f) return glyph;
                    return "*";
            }
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
                // DEF-105: also drive the fill colour in CODE — the USS warning/critical
                // tint classes don't render in player builds (CLAUDE.md §8), so set the
                // themed crimson→amber→red ramp directly so the threshold feedback shows.
                _heartHpFill.style.backgroundColor = critical
                    ? new Color(0.86f, 0.12f, 0.10f, 1f)   // red — critical
                    : warning
                        ? new Color(1f, 0.74f, 0.18f, 1f)  // amber — warning
                        : HeartFill;                        // life-crimson — healthy
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

        // ── On-screen resource bar (DEF: Wood/Iron/Food/Gems) ────────────────
        // Themed-glyph chips for the three build resources the HUD never showed
        // before. Crystals/Gems stay on the existing crystal-panel counter
        // (_crystalCount) so the gem total is never duplicated. Sourced from the
        // real EconomyService wallet via HeartHudBridge — these are the banked
        // totals players spend on building/tower upgrades.

        // Resource chip glyphs (themed letters; no icon art needed yet).
        private const string WoodGlyph = "🪵"; // wood log
        private const string IronGlyph = "⛏"; // pick → iron/ore
        private const string FoodGlyph = "🍖"; // food
        private static readonly Color ResourceChipBg  = new Color(0.10f, 0.08f, 0.16f, 0.88f); // arcane-violet card
        private static readonly Color ResourceChipRim = new Color(1f,    0.86f, 0.45f, 0.70f); // themed gold rim
        private static readonly Color ResourceValueTxt = new Color(0.96f, 0.93f, 0.82f, 1f);    // parchment-cream

        /// <summary>
        /// Builds the compact Wood/Iron/Food chip row once, top-left under the
        /// heart card. Idempotent — re-finds an existing bar and never duplicates
        /// it. Gems are shown by the separate crystal counter, so they're not
        /// added here. Code-built (USS doesn't render in builds — CLAUDE.md §8).
        /// </summary>
        private void BuildResourceBar()
        {
            if (_root == null) return;
            if (_resourceBar != null) { _resourceBar.RemoveFromHierarchy(); _resourceBar = null; }

            var bar = new VisualElement { name = "resource-bar" };
            bar.pickingMode = PickingMode.Ignore;   // passive readout — never eats input
            var s = bar.style;
            s.position = Position.Absolute;
            s.top = 40f;     // just under the heart-hp card (top-left column)
            s.left = 16f;
            s.flexDirection = FlexDirection.Row;
            s.alignItems = Align.Center;

            _woodCount = MakeResourceChip(bar, "wood-count", WoodGlyph);
            _ironCount = MakeResourceChip(bar, "iron-count", IronGlyph);
            _foodCount = MakeResourceChip(bar, "food-count", FoodGlyph);

            _root.Add(bar);
            _resourceBar = bar;
        }

        /// <summary>Builds one glyph + count chip into <paramref name="bar"/> and returns its count label.</summary>
        private static Label MakeResourceChip(VisualElement bar, string countName, string glyph)
        {
            var chip = new VisualElement { name = countName + "-chip" };
            chip.pickingMode = PickingMode.Ignore;
            var cs = chip.style;
            cs.flexDirection = FlexDirection.Row;
            cs.alignItems = Align.Center;
            cs.marginRight = 6f;
            cs.paddingLeft = 6f; cs.paddingRight = 8f; cs.paddingTop = 2f; cs.paddingBottom = 2f;
            cs.backgroundColor = ResourceChipBg;
            cs.borderTopWidth = 1f; cs.borderBottomWidth = 1f;
            cs.borderLeftWidth = 1f; cs.borderRightWidth = 1f;
            cs.borderTopColor = ResourceChipRim; cs.borderBottomColor = ResourceChipRim;
            cs.borderLeftColor = ResourceChipRim; cs.borderRightColor = ResourceChipRim;
            cs.borderTopLeftRadius = 7f; cs.borderTopRightRadius = 7f;
            cs.borderBottomLeftRadius = 7f; cs.borderBottomRightRadius = 7f;

            var glyphLbl = new Label(glyph) { name = countName + "-glyph" };
            glyphLbl.pickingMode = PickingMode.Ignore;
            var gs = glyphLbl.style;
            gs.fontSize = 14f;
            gs.marginRight = 3f;
            gs.unityTextAlign = TextAnchor.MiddleCenter;

            var countLbl = new Label("0") { name = countName };
            countLbl.pickingMode = PickingMode.Ignore;
            var vs = countLbl.style;
            vs.color = ResourceValueTxt;
            vs.fontSize = 13f;
            vs.unityFontStyleAndWeight = FontStyle.Bold;

            chip.Add(glyphLbl);
            chip.Add(countLbl);
            bar.Add(chip);
            return countLbl;
        }

        /// <summary>
        /// Updates the on-screen resource bar with the live wallet. Wood/Iron/Food
        /// fill the chip row; Gems are routed to the existing crystal counter so
        /// the gem total isn't duplicated. Negative values clamp to zero. Called
        /// each economy tick by HeartHudBridge from EconomyService.Snapshot.
        /// </summary>
        public void SetResources(int wood, int iron, int food, int gems)
        {
            if (_woodCount != null) _woodCount.text = Mathf.Max(0, wood).ToString();
            if (_ironCount != null) _ironCount.text = Mathf.Max(0, iron).ToString();
            if (_foodCount != null) _foodCount.text = Mathf.Max(0, food).ToString();
            // Gems reuse the existing crystal counter — keep the two in lockstep.
            SetCrystals(gems);
        }
        // Note: this public SetResources(int,int,int,int) implicitly satisfies
        // IVillageHud.SetResources — no separate explicit impl needed (mirrors how
        // the public SetCrystals(int) satisfies IVillageHud.SetCrystals).

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
            CoreServices.Audio?.PlayUiClick();   // DEF-183: UI click feedback
            int listeners = BuildRequested?.GetPersistentEventCount() ?? 0;
            Debug.Log("[VillageHud] Build CLICK — persistent listeners: " + listeners);
            BuildRequested?.Invoke();
            // NOTE: reflection belt-and-braces removed (2026-05-28) — it caused
            // BuildMenu.Open to fire twice per click. The UnityEvent (BuildRequested)
            // is the single canonical path; ensure BuildMenuHudBridge is wired in scene.
        }

        /// <summary>Forwards the repair-prompt Repair button to RepairConfirmRequested.</summary>
        private void OnRepairConfirmClicked()
        {
            CoreServices.Audio?.PlayUiClick();   // DEF-183: UI click feedback
            RepairConfirmRequested?.Invoke();
        }

        /// <summary>Forwards the repair-prompt Cancel button to RepairCancelRequested.</summary>
        private void OnRepairCancelClicked()
        {
            CoreServices.Audio?.PlayUiClick();   // DEF-183: UI click feedback
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
