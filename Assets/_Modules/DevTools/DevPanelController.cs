// =============================================================================
// DevPanelController — the DEV-ONLY in-game QA / debug console (DeNelle.DevTools)
// -----------------------------------------------------------------------------
// An in-game console for QA: it loads resources and jumps game state so the
// docs/qa/qa-test-plan.md test cases and docs/qa/uat-script.md UAT steps can be
// set up without a full playthrough (e.g. jump straight to a wave, top up
// crystals, grant a pack, spawn Syndrath the dragon boss).
//
// ── RELEASE-SAFE — the single most important property of this file ──────────
// The ENTIRE file body is wrapped in `#if DEVELOPMENT_BUILD || UNITY_EDITOR`.
// A shipped (non-development) player build compiles it to an EMPTY file —
// nothing of this panel ships. Belt-and-braces, the DeNelle.DevTools.asmdef
// also carries a define constraint ("UNITY_EDITOR || DEVELOPMENT_BUILD") so the
// whole assembly is skipped in a release build. Any call site that spawns this
// panel (see DevBootstrap.cs) is gated the same way. See the integrator notes
// at the foot of this file and docs/port-notes/dev-panel.md.
//
// ── MODULE ISOLATION EXCEPTION ───────────────────────────────────────────────
// The project's modules are normally isolated (port spec Part 2). DevTools is
// TOOLING, not gameplay — it MAY reference the gameplay modules (Core, Village,
// Wallet, HUD) because it is dev-only and compiled out of release. It reaches
// systems through their existing PUBLIC APIs / Core seams where reasonable:
//   - GameStateService — crystals / entitlements / Heart-adjacent state.
//   - SceneRouter       — scene jumps.
//   - HeartController / WaveManager / DragonBoss — found in the loaded scene.
//   - WalletService + StubWalletProvider — mock balances.
// Everything is null-guarded: a scene that lacks (say) a WaveManager simply
// reports "no WaveManager in scene" instead of throwing.
//
// ── UI ───────────────────────────────────────────────────────────────────────
// A UI Toolkit overlay (DevPanel.uxml/.uss). Toggled by a hotkey (F1 by
// default) AND by tapping the on-screen "DEV" corner chip. The action groups
// and their buttons are built ONCE at runtime into the "dev-group-list"
// container — matches the VillageHudController ability-bar pattern.
// =============================================================================

#if DEVELOPMENT_BUILD || UNITY_EDITOR

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DeNelle.Core;
using DeNelle.Core.State;
using DeNelle.HUD;
using DeNelle.Village;
using DeNelle.Wallet;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeNelle.DevTools
{
    /// <summary>
    /// DEV-ONLY in-game QA / debug console. A UI Toolkit overlay toggled by a
    /// hotkey or the on-screen corner chip; its buttons load resources and jump
    /// game state so QA can set up a scenario without a full playthrough.
    /// <para>
    /// Compiled ONLY into Editor + Development builds — the whole file is
    /// <c>#if DEVELOPMENT_BUILD || UNITY_EDITOR</c> and the DeNelle.DevTools
    /// asmdef carries a matching define constraint. A release player build
    /// contains none of it.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class DevPanelController : MonoBehaviour
    {
        // ── Inspector wiring ──────────────────────────────────────────────────

        [Header("UI")]
        [Tooltip("UIDocument hosting DevPanel.uxml. Falls back to the component on this GameObject.")]
        [SerializeField] private UIDocument _document;

        [Header("Hotkey")]
        [Tooltip("Key that toggles the dev console open / closed. Default F1.")]
        [SerializeField] private KeyCode _toggleKey = KeyCode.F1;

        [Tooltip("Open the panel automatically when the scene starts.")]
        [SerializeField] private bool _openOnStart;

        [Header("Crystal grants")]
        [Tooltip("Amount the small 'GIVE crystals' button adds.")]
        [SerializeField] private int _crystalSmallGrant = 100;

        [Tooltip("Amount the large 'GIVE crystals' button adds.")]
        [SerializeField] private int _crystalLargeGrant = 1000;

        [Header("Defaults for the typed actions")]
        [Tooltip("Default pack / entitlement SKU pre-filled in the GRANT field.")]
        [SerializeField] private string _defaultPackId = "hearth-spark";

        [Tooltip("Default wave number pre-filled in the JUMP-to-wave field.")]
        [SerializeField] private int _defaultJumpWave = 5;

        [Tooltip("Default mock wallet balance applied to every rail by MOCK wallet.")]
        [SerializeField] private double _defaultMockBalance = 100d;

        [Header("Spawn prefabs (dev-only)")]
        [Tooltip("Boss_Dragon prefab (carries a DragonBoss). Assigned for the " +
                 "'Spawn Syndrath' action. Optional — the action reports cleanly if blank.")]
        [SerializeField] private DragonBoss _dragonBossPrefab;

        // ── UXML element names — the binding contract with DevPanel.uxml ─────
        private const string RootName = "dev-panel-root";
        private const string CornerTapName = "dev-corner-tap";
        private const string WindowName = "dev-panel-window";
        private const string CloseButtonName = "dev-panel-close";
        private const string StatusName = "dev-status";
        private const string GroupListName = "dev-group-list";

        // ── USS class names — styled by DevPanel.uss ─────────────────────────
        private const string WindowOpenClass = "dev-panel-window--open";
        private const string GroupClass = "dev-group";
        private const string GroupCaptionClass = "dev-group__caption";
        private const string GroupRowClass = "dev-group__row";
        private const string ActionButtonClass = "dev-action-button";
        private const string ToggleOnClass = "dev-action-button--toggle-on";
        private const string ToggleOffClass = "dev-action-button--toggle-off";
        private const string TextFieldClass = "dev-text-field";

        // ── Bound UI elements ────────────────────────────────────────────────
        private VisualElement _root;
        private VisualElement _cornerTap;
        private VisualElement _window;
        private Button _closeButton;
        private Label _status;
        private VisualElement _groupList;
        private bool _bound;
        private bool _isOpen;

        // ── Live metrics readout (the "decked-out" telemetry; dev-only) ───────
        // A code-built panel of label:value rows refreshed a few times a second
        // while the console is open. Keyed by a short id so UpdateMetrics() can
        // set each value without re-querying the visual tree.
        private VisualElement _metricsPanel;
        private readonly Dictionary<string, Label> _metricValues = new Dictionary<string, Label>();
        private float _fpsSmoothed;
        private float _metricsTimer;
        private const float MetricsInterval = 0.2f;   // ~5 refreshes / second

        // ── Live typed-action input values ───────────────────────────────────
        private string _packIdInput;
        private int _waveInput;
        private double _mockBalanceInput;
        private int _levelInput = 10;

        // ── God-mode / instant-win toggles ───────────────────────────────────
        // DevTools owns these flags; the integrator reads them from gameplay
        // (see the integrator notes at the foot of this file). They are static
        // so gameplay can query them without holding a panel reference.

        /// <summary>
        /// DEV god-mode flag. When true the integrator suppresses damage to the
        /// Heart / hero. Read by gameplay; never on in a release build (this
        /// whole type is compiled out).
        /// </summary>
        public static bool GodMode { get; private set; }

        /// <summary>
        /// DEV instant-win-wave flag. When true the integrator clears the active
        /// wave immediately. Read by gameplay.
        /// </summary>
        public static bool InstantWinWave { get; private set; }

        /// <summary>Raised when <see cref="GodMode"/> changes (so gameplay can refresh).</summary>
        public static event Action<bool> GodModeChanged;

        /// <summary>Raised when <see cref="InstantWinWave"/> changes.</summary>
        public static event Action<bool> InstantWinWaveChanged;

        /// <summary>Buttons whose label / class reflect a toggle — refreshed after every action.</summary>
        private readonly List<ToggleBinding> _toggleButtons = new List<ToggleBinding>();

        /// <summary>One toggle button + the predicate that decides its on/off look.</summary>
        private struct ToggleBinding
        {
            public Button Button;
            public string OnLabel;
            public string OffLabel;
            public Func<bool> IsOn;
        }

        // =====================================================================
        //  Lifecycle
        // =====================================================================

        private void Awake()
        {
            if (_document == null) _document = GetComponent<UIDocument>();
            _packIdInput = _defaultPackId;
            _waveInput = Mathf.Max(1, _defaultJumpWave);
            _mockBalanceInput = _defaultMockBalance;
        }

        private void OnEnable()
        {
            BindElements();
            SetOpen(_openOnStart);
        }

        private void OnDisable()
        {
            if (_closeButton != null) _closeButton.clicked -= Close;
            if (_cornerTap != null)
                _cornerTap.UnregisterCallback<ClickEvent>(OnCornerTapped);
            _bound = false;
        }

        private void Update()
        {
            // Hotkey toggle. Input.GetKeyDown is fine for a dev tool — no need to
            // route a dev console through the Input System action maps.
            if (Input.GetKeyDown(_toggleKey))
                SetOpen(!_isOpen);

            // Smooth FPS every frame (unscaled so it reads true during slow-mo).
            float dt = Time.unscaledDeltaTime;
            if (dt > 0f)
            {
                float inst = 1f / dt;
                _fpsSmoothed = _fpsSmoothed <= 0f ? inst : Mathf.Lerp(_fpsSmoothed, inst, 0.1f);
            }

            // Refresh the readout a few times a second while open (cheap for a dev tool).
            if (_isOpen)
            {
                _metricsTimer += dt;
                if (_metricsTimer >= MetricsInterval)
                {
                    _metricsTimer = 0f;
                    UpdateMetrics();
                }
            }
        }

        // =====================================================================
        //  UI Toolkit binding
        // =====================================================================

        private void BindElements()
        {
            _root = _document != null ? _document.rootVisualElement : null;
            if (_root == null)
            {
                Debug.LogWarning("[DevPanelController] No UIDocument root — dev console will not display.");
                return;
            }

            // ── Code-built scaffold (NOT bound from DevPanel.uxml) ────────────
            // This project's UXML templates render EMPTY in player builds (the same
            // trap that blanked BattleHUD / BuildMenu / PackStore). To guarantee the
            // console draws in a development build, the whole UI is constructed here
            // in C# with inline styles — no dependency on the .uxml/.uss assets.
            _root.Clear();
            _root.style.flexGrow = 1f;
            _root.pickingMode = PickingMode.Ignore;   // never eat gameplay input

            // Always-visible corner chip — toggles the console (touch-friendly).
            _cornerTap = new Label("DEV") { name = CornerTapName };
            StyleCornerChip(_cornerTap);
            _cornerTap.RegisterCallback<ClickEvent>(OnCornerTapped);
            _root.Add(_cornerTap);

            // The console window — hidden until opened.
            _window = new VisualElement { name = WindowName };
            StyleWindow(_window);
            _root.Add(_window);

            // Header row: title + close (✕).
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 6;
            var title = new Label("◆ DEV CONSOLE");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 15;
            title.style.color = new Color(0.85f, 0.92f, 1f);
            header.Add(title);
            _closeButton = new Button { text = "✕", name = CloseButtonName };
            StyleChromeButton(_closeButton);
            _closeButton.clicked += Close;
            header.Add(_closeButton);
            _window.Add(header);

            // Live metrics readout — the decked-out telemetry block.
            BuildMetricsPanel(_window);

            // Status line.
            _status = new Label("Ready.") { name = StatusName };
            _status.style.color = new Color(0.7f, 0.85f, 0.7f);
            _status.style.fontSize = 11;
            _status.style.marginTop = 4;
            _status.style.marginBottom = 4;
            _status.style.whiteSpace = WhiteSpace.Normal;
            _window.Add(_status);

            // Scrollable action-group list (the existing code-built groups).
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1f;
            _window.Add(scroll);
            _groupList = scroll.contentContainer;

            BuildActionGroups();
            _bound = true;
        }

        private void OnCornerTapped(ClickEvent _) => SetOpen(!_isOpen);

        /// <summary>Opens / closes the panel window.</summary>
        public void SetOpen(bool open)
        {
            _isOpen = open;
            if (_window != null)
                _window.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            // Hide the corner chip while open (the window covers that corner anyway).
            if (_cornerTap != null)
                _cornerTap.style.display = open ? DisplayStyle.None : DisplayStyle.Flex;
            if (open)
            {
                RefreshToggleButtons();
                UpdateMetrics();
            }
        }

        /// <summary>Closes the panel.</summary>
        public void Close() => SetOpen(false);

        // =====================================================================
        //  Live metrics readout — built once, refreshed ~5x/sec while open
        // =====================================================================

        /// <summary>Builds the telemetry block (label:value rows by category).</summary>
        private void BuildMetricsPanel(VisualElement parent)
        {
            _metricsPanel = new VisualElement();
            _metricsPanel.style.backgroundColor = new Color(0.04f, 0.05f, 0.08f, 0.92f);
            SetRadius(_metricsPanel, 6);
            SetPadding(_metricsPanel, 7);
            _metricsPanel.style.marginBottom = 6;

            AddMetricSection(_metricsPanel, "PERFORMANCE");
            AddMetricRow(_metricsPanel, "fps", "FPS");
            AddMetricRow(_metricsPanel, "frame", "Frame ms");

            AddMetricSection(_metricsPanel, "WAVE / ENEMIES");
            AddMetricRow(_metricsPanel, "wave", "Wave #");
            AddMetricRow(_metricsPanel, "phase", "Phase");
            AddMetricRow(_metricsPanel, "countdown", "Countdown");
            AddMetricRow(_metricsPanel, "enemies", "Live enemies");
            AddMetricRow(_metricsPanel, "boss", "Apex boss");

            AddMetricSection(_metricsPanel, "HERO");
            AddMetricRow(_metricsPanel, "level", "Level");
            AddMetricRow(_metricsPanel, "xp", "XP → next");
            AddMetricRow(_metricsPanel, "dmg", "Dmg mult");

            AddMetricSection(_metricsPanel, "HEART");
            AddMetricRow(_metricsPanel, "heart", "HP");
            AddMetricRow(_metricsPanel, "heartstate", "State");

            AddMetricSection(_metricsPanel, "ECONOMY");
            AddMetricRow(_metricsPanel, "crystals", "Crystals");
            AddMetricRow(_metricsPanel, "food", "Food");
            AddMetricRow(_metricsPanel, "coins", "Coins");
            AddMetricRow(_metricsPanel, "materials", "Stone / Iron / Wood");
            AddMetricRow(_metricsPanel, "wisdom", "Wisdom");

            AddMetricSection(_metricsPanel, "FLAGS");
            AddMetricRow(_metricsPanel, "cheats", "Cheats");

            parent.Add(_metricsPanel);
        }

        /// <summary>Adds a small caption that heads a metric category.</summary>
        private static void AddMetricSection(VisualElement parent, string caption)
        {
            var l = new Label(caption);
            l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.style.fontSize = 9;
            l.style.color = new Color(0.45f, 0.6f, 0.85f);
            l.style.marginTop = 4;
            l.style.letterSpacing = 1f;
            parent.Add(l);
        }

        /// <summary>Adds a label:value row and registers its value label under <paramref name="key"/>.</summary>
        private void AddMetricRow(VisualElement parent, string key, string caption)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.alignItems = Align.Center;

            var cap = new Label(caption);
            cap.style.color = new Color(0.66f, 0.7f, 0.78f);
            cap.style.fontSize = 11;

            var val = new Label("—");
            val.style.color = new Color(0.95f, 0.97f, 1f);
            val.style.fontSize = 11;
            val.style.unityFontStyleAndWeight = FontStyle.Bold;
            val.style.whiteSpace = WhiteSpace.Normal;
            val.style.unityTextAlign = TextAnchor.MiddleRight;
            val.style.flexShrink = 1f;

            row.Add(cap);
            row.Add(val);
            parent.Add(row);
            _metricValues[key] = val;
        }

        /// <summary>Pulls live values from the loaded scene's systems into the readout.</summary>
        private void UpdateMetrics()
        {
            if (_metricValues.Count == 0) return;

            SetMetric("fps", _fpsSmoothed.ToString("0"));
            SetMetric("frame", (_fpsSmoothed > 0f ? 1000f / _fpsSmoothed : 0f).ToString("0.0"));

            // ── Wave ──────────────────────────────────────────────────────────
            var wm = FindFirst<WaveManager>();
            if (wm != null)
            {
                SetMetric("wave", wm.CurrentWaveId.ToString());
                SetMetric("phase", wm.Phase.ToString());
                SetMetric("countdown", wm.Phase == WavePhase.Countdown
                    ? wm.CountdownRemaining.ToString("0.0") + "s" : "—");
            }
            else { SetMetric("wave", "—"); SetMetric("phase", "no WaveManager"); SetMetric("countdown", "—"); }

            // ── Live enemies + family breakdown ────────────────────────────────
            var enemies = UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None);
            if (enemies == null || enemies.Length == 0)
            {
                SetMetric("enemies", "0");
            }
            else
            {
                var byId = new Dictionary<string, int>();
                foreach (var e in enemies)
                {
                    if (e == null) continue;
                    string id = string.IsNullOrEmpty(e.EnemyDefId) ? "?" : e.EnemyDefId;
                    byId.TryGetValue(id, out int n);
                    byId[id] = n + 1;
                }
                var sb = new System.Text.StringBuilder();
                sb.Append(enemies.Length).Append("  (");
                bool first = true;
                foreach (var kv in byId)
                {
                    if (!first) sb.Append(", ");
                    first = false;
                    sb.Append(kv.Value).Append('×').Append(kv.Key);
                }
                sb.Append(')');
                SetMetric("enemies", sb.ToString());
            }

            var boss = FindFirst<DragonBoss>();
            SetMetric("boss", boss == null ? "—" : (boss.IsDead ? "dead" : "ALOFT — " + boss.BossId));

            // ── Hero ──────────────────────────────────────────────────────────
            var hero = HeroProgression.Instance;
            if (hero != null)
            {
                SetMetric("level", hero.Level.ToString());
                float pct = hero.XpToNext > 0f ? (hero.Xp / hero.XpToNext) * 100f : 0f;
                SetMetric("xp", $"{hero.Xp:0} / {hero.XpToNext:0}  ({pct:0}%)");
                SetMetric("dmg", "×" + hero.DamageMultiplier.ToString("0.00"));
            }
            else { SetMetric("level", "—"); SetMetric("xp", "no hero"); SetMetric("dmg", "—"); }

            // ── Heart ─────────────────────────────────────────────────────────
            var heart = FindHeart();
            if (heart != null)
            {
                SetMetric("heart", heart.Hp.ToString("0") + "%");
                SetMetric("heartstate", heart.State.ToString());
            }
            else { SetMetric("heart", "—"); SetMetric("heartstate", "no Heart"); }

            // ── Economy ───────────────────────────────────────────────────────
            var svc = GameStateService.Instance;
            if (svc != null && svc.State != null)
            {
                var st = svc.State;
                var r = st.Resources;
                SetMetric("crystals", r.Crystals.ToString("N0"));
                SetMetric("food", r.Food.ToString("N0"));
                SetMetric("coins", r.Coins.ToString("N0"));
                SetMetric("materials", $"{st.Stone} / {st.Iron} / {st.Wood}");
            }
            else { SetMetric("crystals", "—"); SetMetric("food", "—"); SetMetric("coins", "—"); SetMetric("materials", "—"); }

            var wis = DeNelle.Village.Talents.WisdomCurrencyService.Instance;
            SetMetric("wisdom", wis != null ? wis.Wisdom.ToString() : "—");

            // ── Flags ─────────────────────────────────────────────────────────
            string flags = (GodMode ? "GOD-MODE  " : "") + (InstantWinWave ? "INSTANT-WIN" : "");
            SetMetric("cheats", string.IsNullOrEmpty(flags) ? "off" : flags.Trim());
        }

        /// <summary>Sets a metric value label by key (no-op if the row is missing).</summary>
        private void SetMetric(string key, string value)
        {
            if (_metricValues.TryGetValue(key, out var lbl) && lbl != null) lbl.text = value;
        }

        // =====================================================================
        //  Inline style helpers (no USS dependency — renders in dev builds)
        // =====================================================================

        private static void StyleWindow(VisualElement w)
        {
            w.style.position = Position.Absolute;
            w.style.top = 8;
            w.style.right = 8;
            w.style.width = 360;
            w.style.maxHeight = Length.Percent(94);
            w.style.backgroundColor = new Color(0.07f, 0.08f, 0.11f, 0.97f);
            SetPadding(w, 10);
            SetRadius(w, 8);
            w.style.display = DisplayStyle.None;
            var bc = new Color(0.30f, 0.40f, 0.62f, 0.85f);
            w.style.borderLeftWidth = 1; w.style.borderRightWidth = 1;
            w.style.borderTopWidth = 1; w.style.borderBottomWidth = 1;
            w.style.borderLeftColor = bc; w.style.borderRightColor = bc;
            w.style.borderTopColor = bc; w.style.borderBottomColor = bc;
        }

        private static void StyleCornerChip(VisualElement chip)
        {
            chip.style.position = Position.Absolute;
            chip.style.top = 8;
            chip.style.right = 8;
            chip.style.paddingLeft = 9; chip.style.paddingRight = 9;
            chip.style.paddingTop = 3; chip.style.paddingBottom = 3;
            chip.style.backgroundColor = new Color(0.15f, 0.20f, 0.35f, 0.92f);
            chip.style.color = new Color(0.82f, 0.90f, 1f);
            chip.style.unityFontStyleAndWeight = FontStyle.Bold;
            chip.style.fontSize = 11;
            SetRadius(chip, 4);
        }

        private static void StyleChromeButton(Button b)
        {
            b.style.backgroundColor = new Color(0.18f, 0.20f, 0.28f);
            b.style.color = new Color(0.9f, 0.92f, 0.96f);
            b.style.unityFontStyleAndWeight = FontStyle.Bold;
            b.style.fontSize = 13;
            b.style.paddingLeft = 8; b.style.paddingRight = 8;
            b.style.paddingTop = 1; b.style.paddingBottom = 1;
            b.style.marginLeft = 0; b.style.marginRight = 0;
            SetRadius(b, 4);
        }

        private static void SetPadding(VisualElement e, float p)
        {
            e.style.paddingLeft = p; e.style.paddingRight = p;
            e.style.paddingTop = p; e.style.paddingBottom = p;
        }

        private static void SetRadius(VisualElement e, float r)
        {
            e.style.borderTopLeftRadius = r; e.style.borderTopRightRadius = r;
            e.style.borderBottomLeftRadius = r; e.style.borderBottomRightRadius = r;
        }

        // =====================================================================
        //  Action-group construction — built once into "dev-group-list"
        // =====================================================================

        /// <summary>
        /// Builds the grouped action buttons into the "dev-group-list" container.
        /// Mirrors the VillageHudController ability-bar build pattern.
        /// </summary>
        private void BuildActionGroups()
        {
            if (_groupList == null) return;
            _groupList.Clear();
            _toggleButtons.Clear();

            // ── RESOURCES ────────────────────────────────────────────────────
            var resources = AddGroup("Resources");
            AddButton(resources, "+100 Crystals", () => GiveCrystals(100));
            AddButton(resources, "+1000 Crystals", () => GiveCrystals(1000));
            AddButton(resources, "Load up (full base): +50k Wood/Stone/Iron", GiveBuildMaterials);
            AddButton(resources, "+5 Wisdom (talents)", () => GiveWisdom(5));
            AddButton(resources, "+25 Wisdom (talents)", () => GiveWisdom(25));
            AddButton(resources, "+150 XP (hero)", () => GiveHeroXp(150f));
            AddButton(resources, "+10,000 XP (hero)", () => GiveHeroXp(10000f));
            AddButton(resources, "Level up hero", LevelHero);
            AddTextField(resources, _levelInput.ToString(),
                v => { if (int.TryParse(v, out var n)) _levelInput = Mathf.Max(1, n); });
            AddButton(resources, "Set hero to level N", SetHeroLevel);
            AddButton(resources, "Trigger wave (skip countdown)", TriggerWave);

            // ── ENTITLEMENTS ─────────────────────────────────────────────────
            var entitlements = AddGroup("Grant pack / entitlement");
            AddTextField(entitlements, _packIdInput, v => _packIdInput = v);
            AddButton(entitlements, "Grant by id", () => GrantEntitlement(_packIdInput));
            AddButton(entitlements, "Grant ALL packs", GrantAllPacks);

            // ── HEART ────────────────────────────────────────────────────────
            var heart = AddGroup("Heart");
            AddButton(heart, "HP 100%", () => SetHeartHp(100f));
            AddButton(heart, "HP 50%", () => SetHeartHp(50f));
            AddButton(heart, "HP 10%", () => SetHeartHp(10f));
            AddButton(heart, "State: Serene", () => SetHeartState(HeartState.Serene));
            AddButton(heart, "State: Warning", () => SetHeartState(HeartState.Warning));
            AddButton(heart, "State: Critical", () => SetHeartState(HeartState.Critical));
            AddButton(heart, "State: Boss", () => SetHeartState(HeartState.Boss));

            // ── WAVES / ENEMIES ──────────────────────────────────────────────
            var waves = AddGroup("Waves & enemies");
            AddButton(waves, "Spawn enemy", SpawnEnemy);
            AddButton(waves, "Spawn Syndrath (dragon boss)", SpawnSyndrath);
            AddTextField(waves, _waveInput.ToString(),
                v => { if (int.TryParse(v, out var n)) _waveInput = Mathf.Max(1, n); });
            AddButton(waves, "Jump to wave N", () => JumpToWave(_waveInput));
            AddToggleButton(waves, "Instant-win wave: ON", "Instant-win wave: OFF",
                () => InstantWinWave, ToggleInstantWinWave);

            // ── SCENE ────────────────────────────────────────────────────────
            var scene = AddGroup("Scene jump");
            AddButton(scene, "Title", () => JumpScene(SceneRouter.Title));
            AddButton(scene, "Village", () => JumpScene(SceneRouter.Village));
            AddButton(scene, "Dungeon", () => JumpScene(SceneRouter.DungeonHealersCottage));
            AddButton(scene, "ATBBattle", () => JumpScene(SceneRouter.ATBBattle));

            // ── WALLET ───────────────────────────────────────────────────────
            var wallet = AddGroup("Mock wallet balance");
            AddTextField(wallet, _mockBalanceInput.ToString("0.###"),
                v => { if (double.TryParse(v, out var d)) _mockBalanceInput = Math.Max(0d, d); });
            AddButton(wallet, "Mock SOL", () => MockWallet(CurrencyKind.Sol));
            AddButton(wallet, "Mock USDC", () => MockWallet(CurrencyKind.Usdc));
            AddButton(wallet, "Mock SKR", () => MockWallet(CurrencyKind.Skr));
            AddButton(wallet, "Mock ALL rails", MockWalletAll);

            // ── CHEATS ───────────────────────────────────────────────────────
            var cheats = AddGroup("Cheats");
            AddToggleButton(cheats, "God-mode: ON", "God-mode: OFF",
                () => GodMode, ToggleGodMode);

            RefreshToggleButtons();
        }

        /// <summary>Adds a captioned action group and returns its button row.</summary>
        private VisualElement AddGroup(string caption)
        {
            var group = new VisualElement { name = $"dev-group-{caption}" };
            group.AddToClassList(GroupClass);
            group.style.marginTop = 6;

            var captionLabel = new Label(caption);
            captionLabel.AddToClassList(GroupCaptionClass);
            captionLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            captionLabel.style.fontSize = 10;
            captionLabel.style.color = new Color(0.55f, 0.68f, 0.9f);
            captionLabel.style.marginBottom = 2;
            group.Add(captionLabel);

            var row = new VisualElement { name = "dev-group-row" };
            row.AddToClassList(GroupRowClass);
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            group.Add(row);

            _groupList.Add(group);
            return row;
        }

        /// <summary>Adds a plain action button to a group row.</summary>
        private Button AddButton(VisualElement row, string label, Action onClick)
        {
            var button = new Button { text = label };
            button.AddToClassList(ActionButtonClass);
            button.style.backgroundColor = new Color(0.16f, 0.19f, 0.27f);
            button.style.color = new Color(0.88f, 0.91f, 0.96f);
            button.style.fontSize = 11;
            button.style.paddingLeft = 7; button.style.paddingRight = 7;
            button.style.paddingTop = 3; button.style.paddingBottom = 3;
            button.style.marginLeft = 2; button.style.marginRight = 2;
            button.style.marginTop = 2; button.style.marginBottom = 2;
            button.style.borderTopLeftRadius = 4; button.style.borderTopRightRadius = 4;
            button.style.borderBottomLeftRadius = 4; button.style.borderBottomRightRadius = 4;
            button.clicked += () =>
            {
                try { onClick?.Invoke(); }
                catch (Exception ex)
                {
                    Debug.LogError($"[DevPanelController] Action '{label}' threw: {ex}");
                    SetStatus($"'{label}' failed — {ex.Message}");
                }
            };
            row.Add(button);
            return button;
        }

        /// <summary>
        /// Adds a button whose label + colour reflect a boolean toggle. The
        /// <paramref name="onAction"/> flips the underlying flag.
        /// </summary>
        private void AddToggleButton(
            VisualElement row, string onLabel, string offLabel,
            Func<bool> isOn, Action onAction)
        {
            var button = AddButton(row, offLabel, () =>
            {
                onAction?.Invoke();
                RefreshToggleButtons();
            });
            _toggleButtons.Add(new ToggleBinding
            {
                Button = button,
                OnLabel = onLabel,
                OffLabel = offLabel,
                IsOn = isOn,
            });
        }

        /// <summary>Adds an inline text-entry field; pushes edits through <paramref name="onChange"/>.</summary>
        private void AddTextField(VisualElement row, string initial, Action<string> onChange)
        {
            var field = new TextField { value = initial ?? string.Empty };
            field.AddToClassList(TextFieldClass);
            field.style.minWidth = 60;
            field.style.marginLeft = 2; field.style.marginRight = 2;
            field.style.marginTop = 2; field.style.marginBottom = 2;
            field.RegisterValueChangedCallback(evt => onChange?.Invoke(evt.newValue));
            row.Add(field);
        }

        /// <summary>Re-syncs every toggle button's label + on/off class to its flag.</summary>
        private void RefreshToggleButtons()
        {
            foreach (var t in _toggleButtons)
            {
                if (t.Button == null) continue;
                bool on = t.IsOn != null && t.IsOn();
                t.Button.text = on ? t.OnLabel : t.OffLabel;
                t.Button.EnableInClassList(ToggleOnClass, on);
                t.Button.EnableInClassList(ToggleOffClass, !on);
            }
        }

        // =====================================================================
        //  Actions — RESOURCES
        // =====================================================================

        /// <summary>Adds <paramref name="amount"/> crystals to the live game state.</summary>
        private void GiveCrystals(int amount)
        {
            var state = RequireState("give crystals");
            if (state == null) return;

            var r = state.Resources;
            r.Crystals += amount;
            state.Resources = r;
            SaveAndNotifyResources();
            SetStatus($"Gave {amount} crystals — now {r.Crystals}.");
        }

        /// <summary>Grants Wisdom (hero talent currency) for fast skill-tree testing.</summary>
        private void GiveWisdom(int amount)
        {
            var svc = DeNelle.Village.Talents.WisdomCurrencyService.Instance;
            if (svc == null) { SetStatus("WisdomCurrencyService not in scene yet."); return; }
            svc.Grant(amount);
            SetStatus($"Gave {amount} Wisdom — now {svc.Wisdom}.");
        }

        /// <summary>Grants the hero raw XP for fast level/progression testing.</summary>
        private void GiveHeroXp(float amount)
        {
            var hp = UnityEngine.Object.FindAnyObjectByType<DeNelle.Village.HeroProgression>();
            if (hp == null) { SetStatus("HeroProgression not in scene yet."); return; }
            int levels = hp.AddXp(amount);
            if (levels > 0)
                DeNelle.Village.DamageNumberSpawner.SpawnLabel(
                    $"LEVEL UP!  Lv.{hp.Level}", hp.WorldPosition, new Color(0.45f, 1f, 0.55f, 1f), 1.4f);
            SetStatus($"Gave {amount:0} XP — hero is Lv.{hp.Level}"
                      + (levels > 0 ? $" (+{levels} level)" : "") + ".");
        }

        /// <summary>Forces one hero level (also grants its Wisdom + stat bonus).</summary>
        private void LevelHero()
        {
            var hp = UnityEngine.Object.FindAnyObjectByType<DeNelle.Village.HeroProgression>();
            if (hp == null) { SetStatus("HeroProgression not in scene yet."); return; }
            hp.AddXp(hp.XpToNext + 1f);
            DeNelle.Village.DamageNumberSpawner.SpawnLabel(
                $"LEVEL UP!  Lv.{hp.Level}", hp.WorldPosition, new Color(0.45f, 1f, 0.55f, 1f), 1.4f);
            SetStatus($"Hero leveled to Lv.{hp.Level}.");
        }

        /// <summary>Levels the hero up to the typed target level (repeated AddXp until reached).</summary>
        private void SetHeroLevel()
        {
            var hp = UnityEngine.Object.FindAnyObjectByType<DeNelle.Village.HeroProgression>();
            if (hp == null) { SetStatus("HeroProgression not in scene yet."); return; }
            int target = Mathf.Max(1, _levelInput);
            int guard = 0;
            while (hp.Level < target && guard++ < 500)
                hp.AddXp(hp.XpToNext + 1f);
            DeNelle.Village.DamageNumberSpawner.SpawnLabel(
                $"LEVEL {hp.Level}", hp.WorldPosition, new Color(0.45f, 1f, 0.55f, 1f), 1.4f);
            SetStatus($"Set hero to Lv.{hp.Level} (target {target}).");
        }

        /// <summary>Skips the prep countdown and starts the wave now (WaveManager.ForceBeginNextWave).</summary>
        private void TriggerWave()
        {
            var wm = UnityEngine.Object.FindAnyObjectByType<DeNelle.Village.WaveManager>();
            if (wm == null) { SetStatus("WaveManager not in scene yet."); return; }
            wm.ForceBeginNextWave();
            SetStatus("Triggered the wave — countdown skipped.");
        }

        /// <summary>Tops up the gathered build materials + the four harvestables
        /// (Wood/Food/Iron/Crystals) and the Magic tech axis (DEF-121) for testing
        /// the resource-building upgrade loop incl. the Magic-gated arcane tier.</summary>
        private void GiveBuildMaterials()
        {
            var state = RequireState("give materials");
            if (state == null) return;

            state.Stone += 50000;   // legacy build material (BuildMenu costs) — generous one-click load for full-base testing
            state.Iron += 50000;
            state.Wood += 50000;
            // Four-harvestable wallet + Magic tech axis (DEF-121).
            var bal = state.Resources;
            bal.Food += 25000;
            bal.Crystals += 25000;
            state.Resources = bal;
            state.Magic += 100;
            SaveAndNotifyResources();
            SetStatus($"Topped up — Wood {state.Wood}, Food {state.Resources.Food}, " +
                      $"Iron {state.Iron}, Crystals {state.Resources.Crystals}, Magic {state.Magic}.");
        }

        // =====================================================================
        //  Actions — ENTITLEMENTS
        // =====================================================================

        /// <summary>
        /// Grants a pack / entitlement by id — records the SKU in OwnedItemIds
        /// (the entitlement key) and, if the id is a known pack, also applies its
        /// economy top-up and its cosmetic SKUs. Mirrors PackStore.ApplyPackContents.
        /// </summary>
        private void GrantEntitlement(string id)
        {
            var state = RequireState("grant entitlement");
            if (state == null) return;

            if (string.IsNullOrWhiteSpace(id))
            {
                SetStatus("Grant skipped — enter a pack / entitlement id.");
                return;
            }
            id = id.Trim();

            RecordOwned(state, id);

            var pack = PackCatalog.Find(id);
            if (pack != null)
            {
                ApplyPackEconomy(state, pack);
                if (pack.Contents != null && pack.Contents.Cosmetics != null)
                    foreach (var sku in pack.Contents.Cosmetics)
                        RecordOwned(state, sku);
                SetStatus($"Granted pack '{pack.Name}' ({id}) + contents.");
            }
            else
            {
                SetStatus($"Granted entitlement '{id}' (not a known pack — recorded as owned only).");
            }

            GameStateService.Instance.Save();
            GameStateService.Instance.PlayerChanged.Invoke();
        }

        /// <summary>Grants every pack in the canonical catalogue.</summary>
        private void GrantAllPacks()
        {
            var state = RequireState("grant all packs");
            if (state == null) return;

            int count = 0;
            foreach (var pack in PackCatalog.Packs)
            {
                if (pack == null || string.IsNullOrEmpty(pack.Sku)) continue;
                RecordOwned(state, pack.Sku);
                ApplyPackEconomy(state, pack);
                if (pack.Contents != null && pack.Contents.Cosmetics != null)
                    foreach (var sku in pack.Contents.Cosmetics)
                        RecordOwned(state, sku);
                count++;
            }
            GameStateService.Instance.Save();
            GameStateService.Instance.PlayerChanged.Invoke();
            GameStateService.Instance.ResourcesChanged.Invoke();
            SetStatus($"Granted all {count} packs + contents.");
        }

        private static void ApplyPackEconomy(GameState state, PackDef pack)
        {
            var econ = pack.Contents != null ? pack.Contents.Economy : null;
            if (econ == null) return;
            var r = state.Resources;
            r.Crystals += econ.Crystals;
            r.Food += econ.Food;
            r.Coins += econ.Coins;
            state.Resources = r;
        }

        private static void RecordOwned(GameState state, string sku)
        {
            if (state.OwnedItemIds == null) state.OwnedItemIds = new List<string>();
            if (!string.IsNullOrEmpty(sku) && !state.OwnedItemIds.Contains(sku))
                state.OwnedItemIds.Add(sku);
        }

        // =====================================================================
        //  Actions — HEART
        // =====================================================================

        /// <summary>Sets the Heart's HP (0-100) on the HeartController in the loaded scene.</summary>
        private void SetHeartHp(float hp)
        {
            var heart = FindHeart();
            if (heart == null)
            {
                SetStatus("No HeartController in scene — load the Village scene first.");
                return;
            }
            heart.SetHp(hp);
            PushHeartHpToHud(heart);
            SetStatus($"Heart HP set to {hp:0}.");
        }

        /// <summary>Sets the Heart's threat state on the HeartController in the loaded scene.</summary>
        private void SetHeartState(HeartState heartState)
        {
            var heart = FindHeart();
            if (heart == null)
            {
                SetStatus("No HeartController in scene — load the Village scene first.");
                return;
            }
            heart.SetState(heartState);
            SetStatus($"Heart state set to {heartState}.");
        }

        /// <summary>Pushes the Heart's HP into the VillageHud if one is present (display sync).</summary>
        private static void PushHeartHpToHud(HeartController heart)
        {
            var hud = FindFirst<VillageHudController>();
            if (hud != null) hud.SetHeartHp(heart.Hp, 100f);
        }

        // =====================================================================
        //  Actions — WAVES / ENEMIES
        // =====================================================================

        /// <summary>
        /// Asks the WaveManager in the scene to begin its loop at the chosen wave.
        /// WaveManager.BeginLoop re-enters the countdown for that wave; the
        /// integrator can also expose a direct jump (see the integrator notes).
        /// </summary>
        private void JumpToWave(int wave)
        {
            var manager = FindFirst<WaveManager>();
            if (manager == null)
            {
                SetStatus("No WaveManager in scene — load the Village scene first.");
                return;
            }
            // _startWave is a serialized private field on WaveManager; the public
            // seam is BeginLoop(), which re-enters the loop at the manager's
            // configured start wave. To jump to an ARBITRARY wave the integrator
            // exposes WaveManager.JumpToWave (see the integrator notes / port
            // note). Until then this restarts the loop, the safe public call.
            manager.BeginLoop().Forget();
            SetStatus($"Wave loop (re)started — target wave {wave}. " +
                      "Wire WaveManager.JumpToWave for an arbitrary jump (see port note).");
        }

        /// <summary>
        /// Spawns one extra enemy by restarting / nudging the wave loop. The
        /// WaveManager owns enemy instantiation from its prefab + catalogue, so
        /// the dev panel asks IT to spawn rather than instantiating a bare Enemy
        /// (which would have no EnemyDef / NavMesh config).
        /// </summary>
        private void SpawnEnemy()
        {
            var manager = FindFirst<WaveManager>();
            if (manager == null)
            {
                SetStatus("No WaveManager in scene — load the Village scene first.");
                return;
            }
            // Enemy spawning is internal to WaveManager's batch loop. The clean
            // dev seam is a WaveManager.DevSpawnOne() the integrator adds (see
            // the port note). Until then, kicking the loop is the public path.
            manager.BeginLoop().Forget();
            SetStatus("Asked WaveManager to run a wave. " +
                      "Wire WaveManager.DevSpawnOne for a single-enemy spawn (see port note).");
        }

        /// <summary>
        /// Spawns Syndrath the Devourer — the apex dragon boss. Instantiates the
        /// DragonBoss prefab assigned in the inspector (or finds an existing one)
        /// over the Heart and Configures it. Falls back with a clear status when
        /// no prefab is wired.
        /// </summary>
        private void SpawnSyndrath()
        {
            var heart = FindHeart();
            Transform anchor = heart != null ? heart.transform : null;

            var existing = FindFirst<DragonBoss>();
            if (existing != null && !existing.IsDead)
            {
                SetStatus($"Syndrath '{existing.BossId}' is already aloft.");
                return;
            }

            if (_dragonBossPrefab == null)
            {
                SetStatus("No DragonBoss prefab wired on DevPanelController — " +
                          "assign Boss_Dragon in the inspector to spawn Syndrath.");
                return;
            }

            Vector3 spawnPos = (anchor != null ? anchor.position : Vector3.zero)
                               + new Vector3(0f, 22f, 0f);
            var dragon = Instantiate(_dragonBossPrefab, spawnPos, Quaternion.identity);
            dragon.Configure("dev-syndrath", anchor);
            SetStatus("Spawned Syndrath the Devourer over the Heart.");
        }

        /// <summary>Toggles the DEV instant-win-wave flag.</summary>
        private void ToggleInstantWinWave()
        {
            InstantWinWave = !InstantWinWave;
            InstantWinWaveChanged?.Invoke(InstantWinWave);
            SetStatus($"Instant-win-wave {(InstantWinWave ? "ENABLED" : "disabled")}.");
        }

        // =====================================================================
        //  Actions — SCENE
        // =====================================================================

        /// <summary>Jumps to a canonical scene through the SceneRouter.</summary>
        private void JumpScene(string sceneName)
        {
            SetStatus($"Loading scene '{sceneName}'…");
            SceneRouter.LoadScene(sceneName);
        }

        // =====================================================================
        //  Actions — WALLET
        // =====================================================================

        /// <summary>
        /// Sets a mock balance for one currency rail. Routes through
        /// <see cref="DevWalletProbe"/> — a dev-only StubWalletProvider seam — so
        /// the panel never has to fabricate a wallet flow itself.
        /// </summary>
        private void MockWallet(CurrencyKind currency)
        {
            DevWalletProbe.SetMockBalance(currency, _mockBalanceInput);
            SetStatus($"Mock {currency} balance set to {_mockBalanceInput:0.###} " +
                      "(applies to a DevWalletProbe-backed WalletService).");
        }

        /// <summary>Sets the mock balance on all three rails (SOL / USDC / SKR).</summary>
        private void MockWalletAll()
        {
            DevWalletProbe.SetMockBalance(CurrencyKind.Sol, _mockBalanceInput);
            DevWalletProbe.SetMockBalance(CurrencyKind.Usdc, _mockBalanceInput);
            DevWalletProbe.SetMockBalance(CurrencyKind.Skr, _mockBalanceInput);
            SetStatus($"Mock balance set to {_mockBalanceInput:0.###} on SOL / USDC / SKR.");
        }

        // =====================================================================
        //  Actions — CHEATS
        // =====================================================================

        /// <summary>Toggles the DEV god-mode flag.</summary>
        private void ToggleGodMode()
        {
            GodMode = !GodMode;
            GodModeChanged?.Invoke(GodMode);
            SetStatus($"God-mode {(GodMode ? "ENABLED" : "disabled")}.");
        }

        // =====================================================================
        //  Helpers
        // =====================================================================

        /// <summary>Returns the live GameState, or null (with a status) when no service exists.</summary>
        private GameState RequireState(string action)
        {
            var service = GameStateService.Instance;
            if (service == null || service.State == null)
            {
                SetStatus($"Cannot {action} — no GameStateService in the scene.");
                return null;
            }
            return service.State;
        }

        /// <summary>Saves the state and raises the resources-changed event.</summary>
        private static void SaveAndNotifyResources()
        {
            var service = GameStateService.Instance;
            if (service == null) return;
            service.Save();
            service.ResourcesChanged.Invoke();
        }

        /// <summary>Finds the HeartController in the loaded scene, or null.</summary>
        private static HeartController FindHeart() => FindFirst<HeartController>();

        /// <summary>First active object of type T in the loaded scene, or null.</summary>
        private static T FindFirst<T>() where T : UnityEngine.Object
        {
            return UnityEngine.Object.FindAnyObjectByType<T>();
        }

        /// <summary>Writes a line to the status label (and the console).</summary>
        private void SetStatus(string message)
        {
            if (_status != null) _status.text = message;
            Debug.Log($"[DevPanel] {message}");
        }

        /// <summary>True once DevPanel.uxml has been bound and the groups built.</summary>
        public bool IsBound => _bound;
    }
}

#endif // DEVELOPMENT_BUILD || UNITY_EDITOR

// =============================================================================
// INTEGRATOR NOTES — wiring the dev console (DEV builds only).
// -----------------------------------------------------------------------------
// This whole file is compiled out of a release build. To use it in the Editor
// or a Development build:
//
//   1. Either let DevBootstrap.cs spawn the panel automatically (it has a
//      [RuntimeInitializeOnLoadMethod] bootstrap, also #if-gated), OR add a
//      GameObject with a UIDocument (Source Asset = DevPanel.uxml) and this
//      DevPanelController component beside it. The bootstrap is the no-touch
//      path — nothing to add per scene.
//
//   2. Hotkey: F1 toggles the console (configurable on the component). The
//      on-screen "DEV" corner chip toggles it too (touch-friendly).
//
//   3. "Spawn Syndrath" needs the Boss_Dragon prefab assigned to
//      DevPanelController._dragonBossPrefab.
//
//   4. The two cheat flags are READ by gameplay (DevTools must not reach into
//      a gameplay damage path). In a DEV build the integrator can gate, e.g.:
//        #if DEVELOPMENT_BUILD || UNITY_EDITOR
//        if (DeNelle.DevTools.DevPanelController.GodMode) return; // skip damage
//        #endif
//      and listen to GodModeChanged / InstantWinWaveChanged.
//
//   5. JUMP-to-wave / SPAWN-enemy: WaveManager keeps enemy spawning + the
//      start-wave internal. For an ARBITRARY wave jump and a single-enemy
//      spawn the integrator adds two small public dev seams to WaveManager,
//      both #if-gated, e.g.:
//        #if DEVELOPMENT_BUILD || UNITY_EDITOR
//        public void DevJumpToWave(int wave) { EnterCountdown(Mathf.Max(1, wave)); }
//        public void DevSpawnOne(string enemyType) { /* SpawnBatch one */ }
//        #endif
//      Until those exist the panel falls back to BeginLoop() and says so in
//      its status line. See docs/port-notes/dev-panel.md.
//
//   6. MOCK wallet: the panel sets balances on DevWalletProbe. For them to be
//      visible, the wallet-using screen must build its WalletService over a
//      DevWalletProbe (a DEV-only IWalletProvider) — see DevWalletProbe.cs.
// =============================================================================
