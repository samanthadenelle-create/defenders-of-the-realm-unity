// =============================================================================
// BattleController — wires the pure-C# ATB engine to scene + UI
// -----------------------------------------------------------------------------
// C# port of src/modules/battle-atb/BattleATB.tsx (the AtbBattleScreen +
// useAtbBattleLoop pair). A MonoBehaviour that lives in ATBBattle.unity and
// owns the bridge between three layers:
//
//   1. The pure engine     — DeNelle.BattleATB.Engine.* (math, no rendering).
//   2. The runtime state   — ATBRuntimeState (the ScriptableObject "store").
//   3. The scene + the UI  — placeholder capsule combatants + the BattleHUD
//                            UI Toolkit document.
//
// NO combat logic lives here. Every mutation routes through ATBRuntimeState,
// which in turn routes through the engine. This controller only:
//   • builds a BattleSetup from SceneRouter.PendingBattle (or a dev fallback),
//   • subscribes to the runtime state's UnityEvents,
//   • re-renders the HUD (ATB / HP bars, log) and the capsules on every change,
//   • forwards the "Attack" button to ATBRuntimeState.ChooseAction.
//
// Week-2 placeholder scope (v2 port-spec Part 5): one hero capsule, one enemy
// capsule, ATB bars in UI Toolkit, an Attack button that submits an action,
// the engine resolves the turn, the battle log scrolls. Multi-combatant
// portraits / abilities / targeting come with the full Week-2+ HUD port.
//
// Async (port-spec mandate): the post-battle hand-back uses `async UniTask`,
// never `async void`. The fire-and-forget call site uses .Forget().
// =============================================================================

using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DeNelle.BattleATB.Engine;
using DeNelle.BattleATB.State;
using DeNelle.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeNelle.BattleATB
{
    /// <summary>
    /// Scene controller for <c>ATBBattle.unity</c>. Bridges the pure engine, the
    /// <see cref="ATBRuntimeState"/> store, the placeholder capsule combatants
    /// and the <c>BattleHUD</c> UI Toolkit document.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class BattleController : MonoBehaviour
    {
        // ---------------------------------------------------------------------
        // Inspector wiring — set by BattleSceneBuilder (reflection) or by hand
        // ---------------------------------------------------------------------

        [Header("Runtime state")]
        [Tooltip("The runtime-only ScriptableObject store. Created by the scene builder.")]
        [SerializeField] private ATBRuntimeState _runtimeState;

        [Header("UI")]
        [Tooltip("The UIDocument carrying BattleHUD.uxml. Defaults to this GameObject's.")]
        [SerializeField] private UIDocument _hudDocument;

        [Header("Placeholder combatants (capsule meshes)")]
        [Tooltip("Transform of the hero capsule.")]
        [SerializeField] private Transform _heroCapsule;

        [Tooltip("Transform of the enemy capsule.")]
        [SerializeField] private Transform _enemyCapsule;

        [Header("Battle setup")]
        [Tooltip("Seed used when no BattleParams were handed off (dev / direct play).")]
        [SerializeField] private int _fallbackSeed = 42;

        [Tooltip("Enemy def id for the dev fallback battle. Must be a key of ENEMY_DEFS.")]
        [SerializeField] private string _fallbackEnemyDefId = "skeleton";

        [Tooltip("Hero display name for the dev fallback battle.")]
        [SerializeField] private string _fallbackHeroName = "Blaise";

        [Tooltip("Seconds to linger on the result before returning to the previous scene.")]
        [SerializeField] private float _returnDelaySeconds = 2.5f;

        // ---------------------------------------------------------------------
        // Cached UI element queries — bound in OnEnable
        // ---------------------------------------------------------------------

        private Label _statusBanner;
        private VisualElement _heroCard;
        private Label _heroName;
        private VisualElement _heroHpFill;
        private VisualElement _heroAtbFill;
        private VisualElement _enemyCard;
        private Label _enemyName;
        private VisualElement _enemyHpFill;
        private VisualElement _enemyAtbFill;
        private Button _attackButton;
        private Button _skillsButton;
        private Button _itemButton;
        private Button _fleeButton;
        private ScrollView _battleLog;
        private VisualElement _battleLogContent;

        /// <summary>How many log entries have already been rendered (append-only).</summary>
        private int _renderedLogCount;

        /// <summary>True once the result hand-back has been kicked off (fire once).</summary>
        private bool _returnScheduled;

        // ---------------------------------------------------------------------
        // Lifecycle
        // ---------------------------------------------------------------------

        private bool _bound;        // BindUi() succeeded (UI elements queried)
        private bool _subscribed;   // runtime-state events + attack button wired

        private void Awake()
        {
            if (_hudDocument == null) _hudDocument = GetComponent<UIDocument>();
        }

        // CRITICAL: UI binding + event subscription happen in Start(), NOT here.
        // A UIDocument builds its rootVisualElement in its OWN OnEnable, and script
        // execution order means THIS OnEnable can run first — so binding here read a
        // null root, BindUi() failed, OnEnable bailed before ever subscribing, and
        // the HUD never rendered. That is the long-standing "ATB shows only the
        // capsule combatants, no 2D turn-based window" bug. By Start() the document
        // root is guaranteed built. OnEnable only RE-subscribes on a later re-enable
        // (once first-time binding in Start has happened).
        private void OnEnable()
        {
            if (_bound) Subscribe();
        }

        private void OnDisable()
        {
            if (_runtimeState != null)
            {
                _runtimeState.OnBattleChanged.RemoveListener(HandleBattleChanged);
                _runtimeState.OnActionSubmitted.RemoveListener(HandleActionSubmitted);
                _runtimeState.OnOutcome.RemoveListener(HandleOutcome);
            }
            if (_attackButton != null) _attackButton.clicked -= HandleAttackClicked;
            if (_skillsButton != null) _skillsButton.clicked -= HandleSkillsClicked;
            if (_itemButton != null) _itemButton.clicked -= HandleItemClicked;
            if (_fleeButton != null) _fleeButton.clicked -= HandleFleeClicked;
            _subscribed = false;
        }

        private void Start()
        {
            // Bind the HUD now that UIDocument.rootVisualElement is built (see the
            // OnEnable note). Without a bound HUD the battle would run invisibly.
            if (!BindUi())
            {
                Debug.LogError("[BattleController] BattleHUD failed to bind in Start — HUD will not render.");
                return;
            }
            _bound = true;
            Subscribe();   // wire events + the attack button AFTER binding

            if (_runtimeState == null)
            {
                Debug.LogError("[BattleController] No ATBRuntimeState assigned — battle cannot run.");
                return;
            }
            _renderedLogCount = 0;
            _returnScheduled = false;

            BattleSetup setup = BuildSetup();
            // The handoff source: a wave > 0 from PendingBattle means a village
            // breach; the dev fallback path is treated as a village battle too.
            _runtimeState.StartBattle(setup, BattleSource.Village);
            // StartBattle fires OnBattleChanged synchronously — the listener is wired
            // above, but render once explicitly as a belt-and-suspenders first paint.
            Render(_runtimeState.Battle);
        }

        /// <summary>Wires the runtime-state events + the attack button. Idempotent.</summary>
        private void Subscribe()
        {
            if (_subscribed || _runtimeState == null) return;
            _runtimeState.OnBattleChanged.AddListener(HandleBattleChanged);
            _runtimeState.OnActionSubmitted.AddListener(HandleActionSubmitted);
            _runtimeState.OnOutcome.AddListener(HandleOutcome);
            if (_attackButton != null) _attackButton.clicked += HandleAttackClicked;
            if (_skillsButton != null) _skillsButton.clicked += HandleSkillsClicked;
            if (_itemButton != null) _itemButton.clicked += HandleItemClicked;
            if (_fleeButton != null) _fleeButton.clicked += HandleFleeClicked;
            _subscribed = true;
        }

        // ---------------------------------------------------------------------
        // Setup construction — port of BattleATB.tsx's setup memo
        // ---------------------------------------------------------------------

        /// <summary>
        /// Build a <see cref="BattleSetup"/> from <see cref="SceneRouter.PendingBattle"/>
        /// when present, else a single-enemy dev fallback so the scene plays when
        /// opened directly in the editor.
        /// </summary>
        private BattleSetup BuildSetup()
        {
            BattleParams handoff = SceneRouter.PendingBattle;

            int wave = handoff != null && handoff.Wave > 0 ? handoff.Wave : 1;
            int seed = _fallbackSeed;

            // Week-2 placeholder: one hero + one enemy. The full breach roster
            // (handoff.BreachedIds → per-enemy BreachEnemySpec) lands with the
            // Week-4 village breach wiring; for now a single enemy proves the loop.
            string enemyDefId = ResolveEnemyDefId(handoff);

            var enemies = new List<BreachEnemySpec>
            {
                new BreachEnemySpec { DefId = enemyDefId },
            };

            return new BattleSetup
            {
                Wave = wave,
                Seed = seed,
                HeroClass = ResolveHeroClass(), // owner: ATB ran as Mage even when you're an Archer
                HeroName = _fallbackHeroName,
                Pets = new List<PartyPetSpec>(),
                Enemies = enemies,
                Inventory = new Dictionary<ItemKind, int>(),
                Reinforcements = false,
            };
        }

        /// <summary>
        /// The hero's class for this battle, read from the live GameState so the ATB
        /// hero matches the class the player chose (owner: "drops into the stub with
        /// Mage — started as Archer"). GameState carries a Core HeroClassOpt; map it
        /// to the engine HeroClass. Falls back to Mage when there is no save / None.
        /// </summary>
        private static HeroClass ResolveHeroClass()
        {
            var svc = DeNelle.Core.State.GameStateService.Instance;
            var opt = (svc != null && svc.State != null)
                ? svc.State.HeroClass
                : DeNelle.Core.State.HeroClassOpt.None;
            HeroClass cls;
            switch (opt)
            {
                case DeNelle.Core.State.HeroClassOpt.Knight: cls = HeroClass.Knight; break;
                case DeNelle.Core.State.HeroClassOpt.Ranger: cls = HeroClass.Ranger; break;
                case DeNelle.Core.State.HeroClassOpt.Mage:   cls = HeroClass.Mage;   break;
                default:                                     cls = HeroClass.Mage;   break; // no save / None
            }
            Debug.Log($"[BattleController] ATB hero class resolved to {cls} (GameState={opt}).");
            return cls;
        }

        /// <summary>
        /// Pick the enemy def id. The Week-2 handoff carries 3D-layer ids in
        /// <see cref="BattleParams.BreachedIds"/>, not engine def ids, so until the
        /// breach mapper exists this always uses the inspector fallback.
        /// </summary>
        private string ResolveEnemyDefId(BattleParams handoff)
        {
            // BreachedIds are 3D-sim ids — mapping them to ENEMY_DEFS keys is the
            // Week-4 breach trigger's job. Use the configured fallback for now.
            string id = _fallbackEnemyDefId;
            if (string.IsNullOrEmpty(id)) id = "skeleton";
            return id;
        }

        // ---------------------------------------------------------------------
        // Event handlers — driven by ATBRuntimeState's UnityEvents
        // ---------------------------------------------------------------------

        /// <summary>Re-render the whole HUD whenever the live snapshot changes.</summary>
        private void HandleBattleChanged(BattleState state)
        {
            Render(state);
        }

        /// <summary>
        /// The hero just submitted an action (before the AI drain). Used purely
        /// for an immediate "resolving…" affordance — the full re-render arrives
        /// via <see cref="HandleBattleChanged"/> right after.
        /// </summary>
        private void HandleActionSubmitted(BattleState state)
        {
            if (_attackButton != null) _attackButton.SetEnabled(false);
            if (_skillsButton != null) _skillsButton.SetEnabled(false);
            if (_itemButton != null) _itemButton.SetEnabled(false);
            if (_fleeButton != null) _fleeButton.SetEnabled(false);
            if (_statusBanner != null) _statusBanner.text = "Resolving…";
        }

        /// <summary>Battle reached a final outcome — schedule the scene hand-back.</summary>
        private void HandleOutcome(AtbBattleResult result)
        {
            if (result == null || _returnScheduled) return;
            _returnScheduled = true;
            ReturnAfterResult(result).Forget();
        }

        /// <summary>The "Attack" button — submit a basic attack on the lowest-HP foe.</summary>
        private void HandleAttackClicked()
        {
            if (_runtimeState == null || !_runtimeState.IsAwaitingPlayer()) return;

            // Attack auto-resolves vs. the lowest-HP living enemy (no picker) —
            // mirrors AtbBattleScreen.tsx's command model for the basic attack.
            BattleUnit target = BattleStateOps.LowestHpEnemy(_runtimeState.Battle);
            if (target == null) return;

            _runtimeState.ChooseAction(BattleAction.MakeAttack(target.Id));
        }

        /// <summary>Skills button — opens skill picker (stub: selects first skill on lowest-HP foe).</summary>
        private void HandleSkillsClicked()
        {
            // TODO: open skill selection panel (DEF-34 full implementation).
            Debug.Log("[BattleController] Skills button clicked — skill picker not yet implemented.");
        }

        /// <summary>Item button — opens inventory picker (stub: logs intent).</summary>
        private void HandleItemClicked()
        {
            // TODO: open item selection panel (DEF-34 full implementation).
            Debug.Log("[BattleController] Item button clicked — item picker not yet implemented.");
        }

        /// <summary>Flee button — attempt to escape the battle (stub: logs intent).</summary>
        private void HandleFleeClicked()
        {
            // TODO: resolve flee attempt via engine (DEF-34 full implementation).
            Debug.Log("[BattleController] Flee button clicked — flee logic not yet implemented.");
        }

        // ---------------------------------------------------------------------
        // Rendering — HUD bars, log, capsule state
        // ---------------------------------------------------------------------

        /// <summary>Re-draw the whole HUD + capsule state from a battle snapshot.</summary>
        private void Render(BattleState state)
        {
            if (state == null) return;

            BattleUnit hero = FirstUnit(state, UnitKind.Hero);
            BattleUnit enemy = FirstUnit(state, Side.Enemy);

            RenderCombatant(hero, _heroCard, _heroName, _heroHpFill, _heroAtbFill, state);
            RenderCombatant(enemy, _enemyCard, _enemyName, _enemyHpFill, _enemyAtbFill, state);
            RenderCapsules(hero, enemy);
            RenderLog(state);
            RenderStatus(state);
            RenderAttackButton(state);
        }

        /// <summary>Bind one combatant's name, HP bar, ATB bar and active highlight.</summary>
        private static void RenderCombatant(
            BattleUnit unit,
            VisualElement card,
            Label nameLabel,
            VisualElement hpFill,
            VisualElement atbFill,
            BattleState state)
        {
            if (unit == null)
            {
                if (card != null) card.style.opacity = 0.35f;
                return;
            }

            if (nameLabel != null) nameLabel.text = unit.Name;

            float hpPct = unit.MaxHp > 0
                ? Mathf.Clamp01((float)unit.Hp / unit.MaxHp)
                : 0f;
            float atbPct = Mathf.Clamp01((float)(unit.Atb / Defs.ATB_FULL));

            SetBarWidth(hpFill, hpPct);
            SetBarWidth(atbFill, atbPct);

            // The ATB bar turns amber once the unit is ready to act.
            if (atbFill != null)
            {
                bool ready = unit.Atb >= Defs.ATB_FULL;
                atbFill.EnableInClassList("bar-fill--atb-ready", ready);
            }

            if (card != null)
            {
                card.style.opacity = unit.Alive ? 1f : 0.35f;
                bool isActive = unit.Alive && state.ActiveUnitId == unit.Id;
                card.EnableInClassList("combatant-card--active", isActive);
            }
        }

        /// <summary>Reflect HP / death visually on the placeholder capsule meshes.</summary>
        private void RenderCapsules(BattleUnit hero, BattleUnit enemy)
        {
            ApplyCapsuleState(_heroCapsule, hero);
            ApplyCapsuleState(_enemyCapsule, enemy);
        }

        /// <summary>Tilt a fallen capsule over; keep a live one upright.</summary>
        private static void ApplyCapsuleState(Transform capsule, BattleUnit unit)
        {
            if (capsule == null) return;
            bool down = unit == null || !unit.Alive;
            capsule.localRotation = down
                ? Quaternion.Euler(90f, capsule.localEulerAngles.y, 0f)
                : Quaternion.Euler(0f, capsule.localEulerAngles.y, 0f);
        }

        /// <summary>Append any new battle-log entries and scroll to the latest.</summary>
        private void RenderLog(BattleState state)
        {
            if (_battleLogContent == null || state.Log == null) return;

            for (int i = _renderedLogCount; i < state.Log.Count; i++)
            {
                BattleLogEntry entry = state.Log[i];
                var line = new Label(entry.Text);
                line.AddToClassList("log-entry");
                string variant = LogVariantClass(entry.Event);
                if (variant != null) line.AddToClassList(variant);
                _battleLogContent.Add(line);
            }
            _renderedLogCount = state.Log.Count;

            // Scroll the newest entry into view.
            if (_battleLog != null && _battleLogContent.childCount > 0)
            {
                VisualElement last = _battleLogContent[_battleLogContent.childCount - 1];
                _battleLog.ScrollTo(last);
            }
        }

        /// <summary>The accent USS class for a log event, or null for the default.</summary>
        private static string LogVariantClass(BattleLogEvent ev)
        {
            switch (ev)
            {
                case BattleLogEvent.BattleStart: return "log-entry--start";
                case BattleLogEvent.Victory: return "log-entry--victory";
                case BattleLogEvent.Defeat: return "log-entry--defeat";
                case BattleLogEvent.Death: return "log-entry--death";
                default: return null;
            }
        }

        /// <summary>Update the status banner from the current phase.</summary>
        private void RenderStatus(BattleState state)
        {
            if (_statusBanner == null) return;

            switch (state.Phase)
            {
                case BattlePhase.AwaitingInput:
                    BattleUnit actor = _runtimeState != null ? _runtimeState.ActiveUnit() : null;
                    _statusBanner.text = actor != null
                        ? $"{actor.Name} — choose an action."
                        : "Choose an action.";
                    break;
                case BattlePhase.Resolving:
                case BattlePhase.Filling:
                    _statusBanner.text = "Resolving…";
                    break;
                case BattlePhase.Ended:
                    _statusBanner.text = state.Outcome == BattleOutcome.Victory
                        ? "Victory — the breach is repelled."
                        : "Defeat — the last stand is lost.";
                    break;
                default:
                    _statusBanner.text = string.Empty;
                    break;
            }
        }

        /// <summary>Enable the Attack/Skills/Item/Flee buttons only while the hero is choosing.</summary>
        private void RenderAttackButton(BattleState state)
        {
            bool canAct = state.Phase == BattlePhase.AwaitingInput
                          && _runtimeState != null
                          && !_runtimeState.Resolving
                          && _runtimeState.Enemies().Count > 0;
            if (_attackButton != null) _attackButton.SetEnabled(canAct);
            if (_skillsButton != null) _skillsButton.SetEnabled(canAct);
            if (_itemButton != null) _itemButton.SetEnabled(canAct);
            // Flee is available whenever it's the player's turn (even with no enemies alive
            // — the battle is already over, but SetEnabled(false) prevents accidental clicks).
            if (_fleeButton != null) _fleeButton.SetEnabled(canAct);
        }

        // ---------------------------------------------------------------------
        // Scene hand-back — async UniTask, never async void
        // ---------------------------------------------------------------------

        /// <summary>
        /// Linger on the result, then return to the scene the battle came from.
        /// The settled outcome already survives on <see cref="ATBRuntimeState.Result"/>
        /// for the caller (the breach trigger / dungeon controller) to read and
        /// apply Heart / building damage or resume the dungeon encounter.
        ///
        /// BUG-008 fix: the destination is <see cref="BattleParams.ReturnScene"/>
        /// off the handoff — a village breach returns to the village, a dungeon
        /// encounter returns to <c>Dungeon_HealersCottage</c>. The hard-coded
        /// village return is gone. A missing handoff (dev / direct play) and an
        /// empty ReturnScene both fall back to the village.
        /// </summary>
        private async UniTask ReturnAfterResult(AtbBattleResult result)
        {
            float delay = Mathf.Max(0f, _returnDelaySeconds);
            await UniTask.Delay(
                System.TimeSpan.FromSeconds(delay),
                ignoreTimeScale: true);

            await SceneRouter.LoadSceneWithFade(ResolveReturnScene());
        }

        /// <summary>
        /// The scene to return to after the battle — the handoff's
        /// <see cref="BattleParams.ReturnScene"/>, defaulting to the village when
        /// no handoff was supplied or the field is blank.
        /// </summary>
        private static string ResolveReturnScene()
        {
            BattleParams handoff = SceneRouter.PendingBattle;
            if (handoff != null && !string.IsNullOrEmpty(handoff.ReturnScene))
                return handoff.ReturnScene;
            return SceneRouter.Village;
        }

        // ---------------------------------------------------------------------
        // UI binding helpers
        // ---------------------------------------------------------------------

        /// <summary>Query every BattleHUD element by name. Returns false if the
        /// document is missing so callers can bail cleanly.</summary>
        private bool BindUi()
        {
            if (_hudDocument == null) _hudDocument = GetComponent<UIDocument>();
            if (_hudDocument == null)
            {
                Debug.LogError("[BattleController] No UIDocument — BattleHUD cannot bind.");
                return false;
            }

            VisualElement root = _hudDocument.rootVisualElement;
            if (root == null)
            {
                Debug.LogError("[BattleController] UIDocument has no rootVisualElement — is BattleHUD.uxml assigned?");
                return false;
            }

            _statusBanner = root.Q<Label>("status-banner");
            _heroCard = root.Q<VisualElement>("hero-card");
            _heroName = root.Q<Label>("hero-name");
            _heroHpFill = root.Q<VisualElement>("hero-hp-fill");
            _heroAtbFill = root.Q<VisualElement>("hero-atb-fill");
            _enemyCard = root.Q<VisualElement>("enemy-card");
            _enemyName = root.Q<Label>("enemy-name");
            _enemyHpFill = root.Q<VisualElement>("enemy-hp-fill");
            _enemyAtbFill = root.Q<VisualElement>("enemy-atb-fill");
            _attackButton = root.Q<Button>("attack-button");
            _skillsButton = root.Q<Button>("skills-button");
            _itemButton = root.Q<Button>("item-button");
            _fleeButton = root.Q<Button>("flee-button");
            _battleLog = root.Q<ScrollView>("battle-log");
            _battleLogContent = root.Q<VisualElement>("battle-log-content");

            if (_attackButton == null)
            {
                // The BattleHUD.uxml does not clone its elements in the player build
                // (the same failure hits BuildMenu.uxml — both UXML docs come up empty
                // while the project's code-built UIDocuments render fine). Rather than
                // ship an uninteractive battle, build a functional HUD in code here and
                // assign the same fields Render() drives. Owner: "ATB lands in a stub".
                Debug.LogWarning("[BattleController] BattleHUD UXML did not bind — building a code HUD fallback.");
                BuildFallbackHud(root);
            }
            return true;
        }

        /// <summary>
        /// Code-built BattleHUD used when the UXML fails to clone in a build. Inline
        /// styles only (no UXML/USS dependency) — mirrors how MusicToggleHud and the
        /// dev panel build reliable UI in code. Assigns every field Render() reads.
        /// </summary>
        private void BuildFallbackHud(VisualElement root)
        {
            root.Clear();
            root.pickingMode = PickingMode.Ignore;   // only the button captures clicks

            var header = new VisualElement();
            header.style.position = Position.Absolute;
            header.style.top = 14; header.style.left = 0; header.style.right = 0;
            header.style.alignItems = Align.Center;
            root.Add(header);

            var title = new Label("The Last Stand");
            title.style.color = new StyleColor(new Color(0.97f, 0.92f, 0.74f, 1f));
            title.style.fontSize = 24; title.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.Add(title);

            _statusBanner = new Label(string.Empty);
            _statusBanner.style.color = new StyleColor(Color.white);
            _statusBanner.style.fontSize = 15; _statusBanner.style.marginTop = 4;
            header.Add(_statusBanner);

            var strip = new VisualElement();
            strip.style.position = Position.Absolute;
            strip.style.top = 84; strip.style.left = 24; strip.style.right = 24;
            strip.style.flexDirection = FlexDirection.Row;
            strip.style.justifyContent = Justify.SpaceBetween;
            strip.style.alignItems = Align.Center;
            root.Add(strip);

            _heroCard = BuildHudCard(strip, "Hero", new Color(0.26f, 0.20f, 0.42f, 0.92f),
                out _heroName, out _heroHpFill, out _heroAtbFill);
            var vs = new Label("vs");
            vs.style.color = new StyleColor(new Color(0.95f, 0.85f, 0.45f, 1f));
            vs.style.fontSize = 18; vs.style.unityFontStyleAndWeight = FontStyle.Bold;
            strip.Add(vs);
            _enemyCard = BuildHudCard(strip, "Enemy", new Color(0.45f, 0.16f, 0.16f, 0.92f),
                out _enemyName, out _enemyHpFill, out _enemyAtbFill);

            _battleLog = new ScrollView(ScrollViewMode.Vertical);
            _battleLog.style.position = Position.Absolute;
            _battleLog.style.left = 24; _battleLog.style.right = 24;
            _battleLog.style.top = 240; _battleLog.style.height = 150;
            _battleLog.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.45f));
            _battleLog.style.paddingLeft = 8; _battleLog.style.paddingRight = 8;
            root.Add(_battleLog);
            _battleLogContent = new VisualElement();
            _battleLog.Add(_battleLogContent);

            // Action button row — Attack / Skills / Item / Flee
            var actionRow = new VisualElement();
            actionRow.style.position = Position.Absolute;
            actionRow.style.bottom = 30;
            actionRow.style.left = 16;
            actionRow.style.right = 16;
            actionRow.style.flexDirection = FlexDirection.Row;
            actionRow.style.justifyContent = Justify.SpaceBetween;
            root.Add(actionRow);

            _attackButton = BuildActionButton("Attack", new Color(0.74f, 0.20f, 0.20f, 1f));
            actionRow.Add(_attackButton);

            _skillsButton = BuildActionButton("Skills", new Color(0.20f, 0.38f, 0.74f, 1f));
            actionRow.Add(_skillsButton);

            _itemButton = BuildActionButton("Item", new Color(0.20f, 0.60f, 0.30f, 1f));
            actionRow.Add(_itemButton);

            _fleeButton = BuildActionButton("Flee", new Color(0.45f, 0.35f, 0.10f, 1f));
            actionRow.Add(_fleeButton);
        }

        /// <summary>Builds one combatant card (name + HP + ATB bars) into <paramref name="parent"/>.</summary>
        private static VisualElement BuildHudCard(VisualElement parent, string fallbackName, Color bg,
            out Label nameLabel, out VisualElement hpFill, out VisualElement atbFill)
        {
            var card = new VisualElement();
            card.style.width = 230;
            card.style.paddingTop = 10; card.style.paddingBottom = 10;
            card.style.paddingLeft = 12; card.style.paddingRight = 12;
            card.style.backgroundColor = new StyleColor(bg);
            card.style.borderTopLeftRadius = 8; card.style.borderTopRightRadius = 8;
            card.style.borderBottomLeftRadius = 8; card.style.borderBottomRightRadius = 8;
            parent.Add(card);

            nameLabel = new Label(fallbackName);
            nameLabel.style.color = new StyleColor(Color.white);
            nameLabel.style.fontSize = 15; nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            card.Add(nameLabel);

            hpFill = AddHudBar(card, "HP", new Color(0.30f, 0.85f, 0.40f));
            atbFill = AddHudBar(card, "ATB", new Color(0.40f, 0.65f, 1f));
            return card;
        }

        /// <summary>A labelled bar track with a 100%-wide fill (driven by SetBarWidth).</summary>
        private static VisualElement AddHudBar(VisualElement card, string label, Color fillColor)
        {
            var lab = new Label(label);
            lab.style.color = new StyleColor(new Color(0.85f, 0.85f, 0.85f, 1f));
            lab.style.fontSize = 10; lab.style.marginTop = 6;
            card.Add(lab);

            var track = new VisualElement();
            track.style.height = 12;
            track.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.5f));
            track.style.borderTopLeftRadius = 4; track.style.borderTopRightRadius = 4;
            track.style.borderBottomLeftRadius = 4; track.style.borderBottomRightRadius = 4;
            card.Add(track);

            var fill = new VisualElement();
            fill.style.height = 12; fill.style.width = Length.Percent(100f);
            fill.style.backgroundColor = new StyleColor(fillColor);
            fill.style.borderTopLeftRadius = 4; fill.style.borderTopRightRadius = 4;
            fill.style.borderBottomLeftRadius = 4; fill.style.borderBottomRightRadius = 4;
            track.Add(fill);
            return fill;
        }

        /// <summary>Creates a consistently-styled action button for the fallback HUD row.</summary>
        private static Button BuildActionButton(string label, Color bgColor)
        {
            var btn = new Button { text = label };
            var s = btn.style;
            s.flexGrow = 1;
            s.marginLeft = 4; s.marginRight = 4;
            s.height = 52; s.fontSize = 16;
            s.unityFontStyleAndWeight = FontStyle.Bold;
            s.color = new StyleColor(Color.white);
            s.backgroundColor = new StyleColor(bgColor);
            s.borderTopLeftRadius = 8; s.borderTopRightRadius = 8;
            s.borderBottomLeftRadius = 8; s.borderBottomRightRadius = 8;
            return btn;
        }

        /// <summary>Set a bar fill's width as a percentage (0..1) of its track.</summary>
        private static void SetBarWidth(VisualElement fill, float pct)
        {
            if (fill == null) return;
            fill.style.width = Length.Percent(Mathf.Clamp01(pct) * 100f);
        }

        /// <summary>First unit on a side (party first, enemies after — engine order).</summary>
        private static BattleUnit FirstUnit(BattleState state, Side side)
        {
            foreach (BattleUnit u in state.Units)
            {
                if (u.Side == side) return u;
            }
            return null;
        }

        /// <summary>First unit of a kind.</summary>
        private static BattleUnit FirstUnit(BattleState state, UnitKind kind)
        {
            foreach (BattleUnit u in state.Units)
            {
                if (u.Kind == kind) return u;
            }
            return null;
        }
    }
}
