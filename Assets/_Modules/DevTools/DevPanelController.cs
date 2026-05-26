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

        // ── Live typed-action input values ───────────────────────────────────
        private string _packIdInput;
        private int _waveInput;
        private double _mockBalanceInput;

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

            _cornerTap = _root.Q<VisualElement>(CornerTapName);
            _window = _root.Q<VisualElement>(WindowName);
            _closeButton = _root.Q<Button>(CloseButtonName);
            _status = _root.Q<Label>(StatusName);
            _groupList = _root.Q<VisualElement>(GroupListName);

            if (_closeButton != null)
            {
                _closeButton.clicked -= Close; // guard a double OnEnable
                _closeButton.clicked += Close;
            }

            if (_cornerTap != null)
            {
                _cornerTap.UnregisterCallback<ClickEvent>(OnCornerTapped);
                _cornerTap.RegisterCallback<ClickEvent>(OnCornerTapped);
            }

            BuildActionGroups();
            _bound = true;
        }

        private void OnCornerTapped(ClickEvent _) => SetOpen(!_isOpen);

        /// <summary>Opens / closes the panel window.</summary>
        public void SetOpen(bool open)
        {
            _isOpen = open;
            if (_window != null)
                _window.EnableInClassList(WindowOpenClass, open);
            if (open) RefreshToggleButtons();
        }

        /// <summary>Closes the panel.</summary>
        public void Close() => SetOpen(false);

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
            AddButton(resources, $"+{_crystalSmallGrant} Crystals",
                () => GiveCrystals(_crystalSmallGrant));
            AddButton(resources, $"+{_crystalLargeGrant} Crystals",
                () => GiveCrystals(_crystalLargeGrant));
            AddButton(resources, "+500 Stone/Iron/Wood", GiveBuildMaterials);
            AddButton(resources, "+5 Wisdom (talents)", () => GiveWisdom(5));
            AddButton(resources, "+25 Wisdom (talents)", () => GiveWisdom(25));
            AddButton(resources, "+150 XP (hero)", () => GiveHeroXp(150f));
            AddButton(resources, "Level up hero", LevelHero);

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

            var captionLabel = new Label(caption);
            captionLabel.AddToClassList(GroupCaptionClass);
            group.Add(captionLabel);

            var row = new VisualElement { name = "dev-group-row" };
            row.AddToClassList(GroupRowClass);
            group.Add(row);

            _groupList.Add(group);
            return row;
        }

        /// <summary>Adds a plain action button to a group row.</summary>
        private Button AddButton(VisualElement row, string label, Action onClick)
        {
            var button = new Button { text = label };
            button.AddToClassList(ActionButtonClass);
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

        /// <summary>Tops up the gathered build materials (Stone / Iron / Wood).</summary>
        private void GiveBuildMaterials()
        {
            var state = RequireState("give materials");
            if (state == null) return;

            state.Stone += 500;
            state.Iron += 500;
            state.Wood += 500;
            SaveAndNotifyResources();
            SetStatus($"Materials topped up — Stone {state.Stone}, Iron {state.Iron}, Wood {state.Wood}.");
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
