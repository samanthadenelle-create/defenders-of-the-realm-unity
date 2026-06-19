// =============================================================================
// OnboardingFlow — the first-run guided tutorial (audit P0-11 / §2.5).
// -----------------------------------------------------------------------------
// The missing-components audit flags the Onboarding module as "built only as far
// as the cinematics": the cold open plays, but there is no first-run TEACHING,
// and GameState.Onboarded is "never set to true in normal play" — so the cold
// open re-plays on every launch (the explicit bug in §2.5 / P0-11).
//
// This component is the missing tutorial. It drives TutorialOverlay.uxml: a
// short, SKIPPABLE six-beat coach-mark sequence taught the first time the
// player reaches the village —
//
//   1. Welcome      — you are the Keeper; the realm is yours to hold.
//   2. The Heart    — Elarion, the Heart, is what you defend.
//   3. Force-field  — the Heart-light field on the walls/gates turns enemies
//                     back; a damaged wall thins it — keep the walls mended.
//   4. Build        — open the Build menu and raise a tower.
//   5. Place a pet  — station a starter Warden at a slot.
//   6. Wave 1       — begin the first wave.
//
// THE Onboarded GATE (audit P0-11, the headline fix):
//   - On enable, the flow reads GameStateService.Instance.State.Onboarded.
//     Onboarded == true  -> the tutorial does NOT run (returning player).
//     Onboarded == false -> the tutorial runs.
//   - On the LAST beat's advance OR on Skip at any beat, the flow calls
//     GameStateService.FinishOnboarding(), which sets Onboarded = true, raises
//     PlayerChanged and Save()s it to PlayerPrefs. It never replays after that.
//   - That same flag already gates StoryIntroController.ShouldAutoPlay
//     (`return !svc.State.Onboarded`). The cold open re-played every launch ONLY
//     because nothing ever flipped the flag — this flow is what flips it, so the
//     cold-open replay bug is fixed by completing or skipping the tutorial once.
//     No change to StoryIntroController is required.
//
// MODULE ISOLATION (v2 port-spec Part 2) — IMPORTANT, mirrors the HUD module:
//   DeNelle.Onboarding references DeNelle.Core ONLY (for GameStateService /
//   SceneRouter / canon strings) — it does NOT reference DeNelle.Village or
//   DeNelle.HUD. The flow therefore cannot call BuildMenu.Open(), cannot read
//   the wave manager and cannot place a pet itself. Instead it is a PASSIVE
//   coach-mark display, exactly like VillageHudController:
//     - It exposes UnityEvents the integrator hooks to gameplay actions
//       (OpenBuildMenuRequested, BeginWaveRequested).
//     - It exposes Notify* methods the integrator calls FROM gameplay events
//       (NotifyTowerBuilt, NotifyPetPlaced) so action-beats auto-advance when
//       the player actually does the thing — not just when they tap Next.
//   The seam is Core types + UnityEvents, never a gameplay-module reference.
//   See docs/port-notes/onboarding.md for the integrator wiring.
//
// async UniTask for the fade — never async void (v2 port-spec Part 3 mandate).
// =============================================================================

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.State;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UIElements;

namespace DeNelle.Onboarding
{
    /// <summary>
    /// Which gameplay action, if any, a tutorial beat is waiting on. An
    /// <see cref="Action"/>-typed beat auto-advances when the integrator reports
    /// the action done (or when the player taps Next); a
    /// <see cref="None"/> beat advances on Next only.
    /// </summary>
    public enum TutorialGate
    {
        /// <summary>Pure-narration beat — advances on the Next button only.</summary>
        None,
        /// <summary>Beat completes when a tower is built (NotifyTowerBuilt).</summary>
        BuildTower,
        /// <summary>Beat completes when a starter pet is placed (NotifyPetPlaced).</summary>
        PlacePet,
    }

    /// <summary>
    /// Drives the first-run guided tutorial overlay — a short, skippable
    /// six-beat coach-mark sequence shown only when
    /// <see cref="GameState.Onboarded"/> is false. Marks onboarding complete
    /// (which stops the cold open re-playing — audit P0-11) on completion or
    /// skip. A passive display: gameplay is reached through Core seams and
    /// UnityEvents, never a direct gameplay-module reference (port-spec Part 2).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class OnboardingFlow : MonoBehaviour
    {
        // ── UXML element names — the binding contract with TutorialOverlay.uxml ─
        private const string RootName = "tutorial-root";
        private const string CaptionName = "tutorial-caption";
        private const string ProgressName = "tutorial-progress";
        private const string BodyName = "tutorial-body";
        private const string SkipButtonName = "tutorial-skip";
        private const string NextButtonName = "tutorial-next";

        // ── USS class names — styled by TutorialOverlay.uss ──────────────────
        private const string RootHiddenClass = "tutorial-root--hidden";
        private const string NextBeginClass = "tutorial-next--begin";

        [Header("UI")]
        [Tooltip("UIDocument hosting TutorialOverlay.uxml. Falls back to the component on this GameObject.")]
        [SerializeField] private UIDocument _document;

        [Header("Behaviour")]
        [Tooltip("Auto-run the tutorial in Start() when the save says NOT onboarded. " +
                 "Disable to drive it manually (e.g. from a 'Replay tutorial' option).")]
        [SerializeField] private bool _runOnStart = true;

        [Tooltip("Force the tutorial to run even when the save says onboarded. Editor testing only.")]
        [SerializeField] private bool _forceRun;

        [Tooltip("Seconds the overlay fade-in / fade-out takes.")]
        [SerializeField, Min(0f)] private float _fadeSeconds = 0.35f;

        [Header("Events — wired to gameplay by the integrator (port-spec Part 2)")]
        [Tooltip("Raised on the Build beat — the integrator hooks this to the village BuildMenu.Open(). " +
                 "DeNelle.Onboarding cannot reference BuildMenu, so the wiring happens in the village scene.")]
        public UnityEvent OpenBuildMenuRequested = new UnityEvent();

        [Tooltip("Raised when the tutorial finishes its last beat — the integrator hooks this to the " +
                 "village WaveManager to start Wave 1.")]
        public UnityEvent BeginWaveRequested = new UnityEvent();

        [Tooltip("Raised when the tutorial ends (completed OR skipped) — for the integrator to re-enable " +
                 "any HUD / input it suppressed while the coach-marks were up.")]
        public UnityEvent TutorialClosed = new UnityEvent();

        // ── Resolved overlay elements ────────────────────────────────────────
        private VisualElement _root;
        private Label _caption;
        private Label _progress;
        private Label _body;
        private Button _skipButton;
        private Button _nextButton;

        private int _beatIndex = -1;
        private bool _running;
        private bool _bound;
        private CancellationTokenSource _cts;

        /// <summary>True while the coach-mark sequence is on screen.</summary>
        public bool IsRunning => _running;

        /// <summary>True once the tutorial has ended this session (completed or skipped).</summary>
        public bool HasFinished { get; private set; }

        // =====================================================================
        //  The six tutorial beats
        // -----------------------------------------------------------------------
        //  Deliverable beats: welcome -> the Heart -> the force-field -> open
        //  Build menu / build a tower -> place a starter pet -> Wave 1 begins.
        //  Each beat's narration
        //  is a CANON STRING resolved at runtime from en.json (tutorial.steps.*
        //  — already present in StreamingAssets/Data/Canonical/en.json) — never
        //  typed inline (v2 port-spec Part 4). The kicker captions are short UI
        //  labels (not narrative copy) so they stay in C#.
        // =====================================================================

        /// <summary>One coach-mark beat — caption, canon copy key, gate, controls.</summary>
        private readonly struct Beat
        {
            /// <summary>Short UI kicker shown above the body copy.</summary>
            public readonly string Caption;
            /// <summary>en.json key for the narrated body copy (tutorial.steps.*).</summary>
            public readonly string CopyKey;
            /// <summary>Which gameplay action, if any, this beat waits on.</summary>
            public readonly TutorialGate Gate;
            /// <summary>Label on the advance button for this beat.</summary>
            public readonly string NextLabel;

            public Beat(string caption, string copyKey, TutorialGate gate, string nextLabel)
            {
                Caption = caption;
                CopyKey = copyKey;
                Gate = gate;
                NextLabel = nextLabel;
            }
        }

        // tutorial.steps.1         — "Welcome home, Guardian. The Heart is the Lantern…"
        // tutorial.steps.2         — "The Hollowed come from the dark beyond the walls…"
        // tutorial.steps.forceField— "The walls hold a force-field of the Heart's own light…"
        // tutorial.steps.3         — "Build your defenses with what you have…"
        // tutorial.steps.6         — "Your Wardens fight beside you. Tap one to give…"
        // tutorial.steps.5         — "When you are ready, begin a wave…"
        private static readonly Beat[] Beats =
        {
            new Beat("WELCOME, KEEPER", "tutorial.steps.1",          TutorialGate.None,       "Next"),
            new Beat("THE HEART",       "tutorial.steps.2",          TutorialGate.None,       "Next"),
            // Force-field beat — explains the Heart-light field on the walls /
            // gates that turns enemies back, and that a damaged wall thins it,
            // leading into the wall-repair mechanic another workstream builds.
            new Beat("THE FORCE-FIELD", "tutorial.steps.forceField", TutorialGate.None,       "Next"),
            new Beat("RAISE A TOWER",   "tutorial.steps.3",          TutorialGate.BuildTower, "Open Build menu"),
            new Beat("YOUR WARDENS",    "tutorial.steps.6",          TutorialGate.PlacePet,   "Next"),
            new Beat("HOLD THE LINE",   "tutorial.steps.5",          TutorialGate.None,       "Begin Wave 1"),
        };

        /// <summary>Number of beats in the tutorial sequence.</summary>
        public static int BeatCount => Beats.Length;

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
            if (_skipButton != null) _skipButton.clicked -= OnSkipClicked;
            if (_nextButton != null) _nextButton.clicked -= OnNextClicked;
            _bound = false;

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        private void Start()
        {
            if (_runOnStart)
                TryRun();
        }

        // =====================================================================
        //  UI Toolkit binding
        // =====================================================================

        private void BindElements()
        {
            using var _ = FlowTrace.Enter("Onboarding", "OnboardingFlow.BindElements");
            _root = _document != null ? _document.rootVisualElement : null;
            if (_root == null)
            {
                // No root means the coach-marks render nothing — the first-run teaching is
                // invisible. Fail-loud so it surfaces in the break-log.
                FlowTrace.Fail("Onboarding", "BindElements: NO UIDocument root — tutorial overlay will NOT display (invisible FTUE).");
                return;
            }

            // DEF-153 (root cause): the tutorial is a MODAL coach-mark and must
            // render ABOVE the in-game HUD. The HUD UIDocuments sort at 80-110;
            // this panel never set a sortingOrder, so it defaulted to 0 and the HUD
            // drew on top — making the steps invisible in BOTH orientations. Lift it
            // above everything HUD-side so the overlay is always seen.
            if (_document != null) _document.sortingOrder = 250f;

            _caption = _root.Q<Label>(CaptionName);
            _progress = _root.Q<Label>(ProgressName);
            _body = _root.Q<Label>(BodyName);
            _skipButton = _root.Q<Button>(SkipButtonName);
            _nextButton = _root.Q<Button>(NextButtonName);

            // PIPELINE_STATE §8: a UXML-sourced UIDocument comes up EMPTY in player
            // builds (BuildMenu / BattleHUD hit this), so TutorialOverlay.uxml may
            // load a root with none of the named coach-mark elements. When the
            // named children are missing, build the overlay in code (inline styles,
            // no USS) so the FTUE actually renders in the build. The UXML is kept
            // only as an editor reference. This never touches the Finish() /
            // FinishOnboarding() persistence path — it only supplies the visuals.
            if (_caption == null || _body == null || _nextButton == null || _skipButton == null)
            {
                BuildOverlayInCode();
                _caption = _root.Q<Label>(CaptionName);
                _progress = _root.Q<Label>(ProgressName);
                _body = _root.Q<Label>(BodyName);
                _skipButton = _root.Q<Button>(SkipButtonName);
                _nextButton = _root.Q<Button>(NextButtonName);
            }

            // V — after the (UXML or code-built) overlay, the core coach-mark controls MUST
            // resolve, or the tutorial is on-screen but un-advanceable. Fail-loud if not.
            if (_caption == null || _body == null || _nextButton == null || _skipButton == null)
            {
                FlowTrace.Fail("Onboarding",
                    $"BindElements VERIFY FAILED — coach-mark elements unresolved after code-build " +
                    $"(caption={( _caption != null)} body={(_body != null)} next={(_nextButton != null)} skip={(_skipButton != null)}). " +
                    "Tutorial may render but cannot be advanced.");
            }

            if (_skipButton != null)
            {
                _skipButton.clicked -= OnSkipClicked; // guard a double OnEnable
                _skipButton.clicked += OnSkipClicked;
            }
            if (_nextButton != null)
            {
                _nextButton.clicked -= OnNextClicked;
                _nextButton.clicked += OnNextClicked;
            }

            // Start hidden — Run() reveals it.
            SetOverlayVisible(false);
            _bound = true;
        }

        /// <summary>
        /// Code-built fallback for the coach-mark overlay (PIPELINE_STATE §8 —
        /// UXML does not render in player builds). Builds the same element tree
        /// TutorialOverlay.uxml describes — a dimming scrim plus a bottom-pinned
        /// card with caption / progress / body / Skip + Next — using inline
        /// styles (USS does not apply to a code-built tree in a build). The names
        /// match the binding contract above so BindElements() resolves them
        /// exactly as it would from the UXML. Mirrors how BuildMenu falls back to
        /// a code-built UI. Only called when the UXML left the named elements
        /// absent; it appends the new card UNDER the existing root so the root's
        /// hidden/opacity controls (SetOverlayVisible / FadeOverlay) still work.
        /// </summary>
        private void BuildOverlayInCode()
        {
            if (_root == null) return;

            // Re-tag the document root as the tutorial root so the existing
            // RootHiddenClass / opacity controls drive it. The root fills the
            // screen and lets pointer events through where there is no card.
            _root.name = RootName;
            _root.style.position = Position.Absolute;
            _root.style.left = 0;
            _root.style.right = 0;
            _root.style.top = 0;
            _root.style.bottom = 0;

            // Dimming scrim — swallows world taps while a coach-mark is up.
            var scrim = new VisualElement { name = "tutorial-scrim" };
            scrim.style.position = Position.Absolute;
            scrim.style.left = 0;
            scrim.style.right = 0;
            scrim.style.top = 0;
            scrim.style.bottom = 0;
            scrim.style.backgroundColor = new Color(0f, 0f, 0f, 0.55f);
            _root.Add(scrim);

            // DEF-153: centered "speech-bubble" coach-mark. The card used to be
            // bottom-pinned (bottom=48), landing directly under the mobile HUD
            // cluster (joystick / d-pad / action buttons) that covered it. Center it
            // via the root's flexbox so it reads clearly in BOTH landscape and
            // portrait, and cap the width so it stays a bubble — not a full-width
            // banner — on wide landscape screens. (scrim stays Absolute/full-screen,
            // so flex-centering only acts on the in-flow card.)
            _root.style.justifyContent = Justify.Center;
            _root.style.alignItems = Align.Center;

            var card = new VisualElement { name = "tutorial-card" };
            card.style.position = Position.Relative;
            card.style.width = Length.Percent(86f);
            card.style.maxWidth = 560;
            card.style.paddingLeft = 28;
            card.style.paddingRight = 28;
            card.style.paddingTop = 24;
            card.style.paddingBottom = 24;
            card.style.backgroundColor = new Color(0.07f, 0.08f, 0.12f, 0.97f);
            card.style.borderTopLeftRadius = 22;
            card.style.borderTopRightRadius = 22;
            card.style.borderBottomLeftRadius = 22;
            card.style.borderBottomRightRadius = 22;
            var border = new Color(0.85f, 0.72f, 0.36f, 0.9f);
            card.style.borderTopColor = border;
            card.style.borderBottomColor = border;
            card.style.borderLeftColor = border;
            card.style.borderRightColor = border;
            card.style.borderTopWidth = 3;
            card.style.borderBottomWidth = 3;
            card.style.borderLeftWidth = 3;
            card.style.borderRightWidth = 3;
            _root.Add(card);

            // Caption row — kicker + progress readout.
            var head = new VisualElement { name = "tutorial-card-head" };
            head.style.flexDirection = FlexDirection.Row;
            head.style.justifyContent = Justify.SpaceBetween;
            head.style.marginBottom = 8;
            card.Add(head);

            var caption = new Label { name = CaptionName };
            caption.style.unityFontStyleAndWeight = FontStyle.Bold;
            caption.style.fontSize = 22;
            caption.style.color = new Color(0.95f, 0.84f, 0.5f);
            head.Add(caption);

            var progress = new Label { name = ProgressName };
            progress.style.fontSize = 15;
            progress.style.color = new Color(0.7f, 0.74f, 0.82f);
            head.Add(progress);

            // Narrated body copy.
            var body = new Label { name = BodyName };
            body.style.fontSize = 20;
            body.style.color = new Color(0.92f, 0.94f, 0.98f);
            body.style.whiteSpace = WhiteSpace.Normal;
            body.style.marginBottom = 20;
            card.Add(body);

            // Controls — Skip (left), Next / Begin (right).
            var foot = new VisualElement { name = "tutorial-card-foot" };
            foot.style.flexDirection = FlexDirection.Row;
            foot.style.justifyContent = Justify.SpaceBetween;
            card.Add(foot);

            var skip = new Button { name = SkipButtonName, text = "Skip tutorial" };
            skip.style.fontSize = 15;
            foot.Add(skip);

            var next = new Button { name = NextButtonName, text = "Next" };
            next.style.fontSize = 17;
            next.style.unityFontStyleAndWeight = FontStyle.Bold;
            next.style.minWidth = 160;
            foot.Add(next);

            FlowTrace.Warn("Onboarding",
                "BuildOverlayInCode: TutorialOverlay.uxml had no coach-mark elements " +
                "(UXML does not render in builds) — built the overlay in code (fallback path).");
        }

        // =====================================================================
        //  Run gate — the Onboarded check (audit P0-11)
        // =====================================================================

        /// <summary>
        /// Runs the tutorial only when it should — i.e. the save reports the
        /// player has NOT completed onboarding (or <c>_forceRun</c> is set for
        /// editor testing). A returning player (<see cref="GameState.Onboarded"/>
        /// == true) is left untouched and <see cref="TutorialClosed"/> is raised
        /// immediately so the integrator does not wait on a tutorial that will
        /// never show. Returns true when the tutorial actually started.
        /// </summary>
        public bool TryRun()
        {
            if (_running || HasFinished) return false;

            if (!ShouldRun)
            {
                // Returning player — nothing to teach. Signal closed so the
                // integrator's "tutorial done" continuation still fires.
                HasFinished = true;
                TutorialClosed?.Invoke();
                return false;
            }

            Run();
            return true;
        }

        /// <summary>
        /// True when the first-run tutorial should play — a brand-new save has
        /// <see cref="GameState.Onboarded"/> == false. Mirrors the gate
        /// <see cref="StoryIntroController.ShouldAutoPlay"/> uses for the cold
        /// open, so the two first-run surfaces stay in lock-step.
        /// </summary>
        public bool ShouldRun
        {
            get
            {
                if (_forceRun) return true;
                var svc = GameStateService.Instance;
                // No service yet (Core not bootstrapped) — treat as first launch
                // so a new player is never silently denied the tutorial.
                if (svc == null || svc.State == null) return true;
                return !svc.State.Onboarded;
            }
        }

        // =====================================================================
        //  Run / advance / finish
        // =====================================================================

        /// <summary>
        /// Starts the coach-mark sequence at beat 1 unconditionally — skipping
        /// the <see cref="ShouldRun"/> gate. Public so a future "Replay
        /// tutorial" settings option can re-show it on demand; normal first-run
        /// entry goes through <see cref="TryRun"/>.
        /// </summary>
        public void Run()
        {
            using var _ = FlowTrace.Enter("Onboarding", "OnboardingFlow.Run");
            if (_running) { FlowTrace.Step("Onboarding", "Run: already running — no-op."); return; }
            if (!_bound) BindElements();

            _running = true;
            HasFinished = false;
            _beatIndex = 0;

            SetOverlayVisible(true);
            ShowBeat(_beatIndex);
            FadeOverlay(0f, 1f).Forget();

            // V — the flow advanced to beat 1 with a bound body label. If the body is
            // null the coach-marks are up but show no copy — surface it.
            if (_body == null)
                FlowTrace.Fail("Onboarding", "Run: tutorial started but body label is null — coach-marks will show no copy.");
            else
                FlowTrace.Step("Onboarding", $"Run: tutorial started at beat 1/{Beats.Length}.");
        }

        /// <summary>Renders one beat into the coach-mark card.</summary>
        private void ShowBeat(int index)
        {
            if (index < 0 || index >= Beats.Length) return;
            var beat = Beats[index];

            if (_caption != null) _caption.text = beat.Caption;
            if (_progress != null) _progress.text = $"{index + 1} / {Beats.Length}";

            // Body copy — canon string from en.json, never typed inline.
            if (_body != null) _body.text = CanonStrings.Locale(beat.CopyKey);

            if (_nextButton != null)
            {
                _nextButton.text = beat.NextLabel;
                // The final beat's advance button becomes the amber CTA.
                _nextButton.EnableInClassList(NextBeginClass, index == Beats.Length - 1);
            }
        }

        /// <summary>
        /// Advances past the current beat. A gated beat (Build / Place pet)
        /// fires its integrator hook and then advances; the integrator may also
        /// auto-advance the same beat early via <see cref="NotifyTowerBuilt"/> /
        /// <see cref="NotifyPetPlaced"/> when the player completes the action.
        /// The last beat's advance ends the tutorial.
        /// </summary>
        private void OnNextClicked()
        {
            if (!_running) return;
            var beat = Beats[_beatIndex];

            // Fire the integrator hook for a gated beat as the player taps
            // through it — e.g. tapping "Open Build menu" opens the menu.
            switch (beat.Gate)
            {
                case TutorialGate.BuildTower:
                    OpenBuildMenuRequested?.Invoke();
                    break;
            }

            AdvanceFromBeat(_beatIndex);
        }

        /// <summary>Skip — ends the tutorial immediately from any beat.</summary>
        private void OnSkipClicked()
        {
            if (!_running) return;
            Finish(completed: false);
        }

        /// <summary>
        /// Moves on from <paramref name="fromIndex"/>: shows the next beat, or
        /// finishes the tutorial when the last beat is left.
        /// </summary>
        private void AdvanceFromBeat(int fromIndex)
        {
            if (fromIndex != _beatIndex) return; // stale call — beat already moved

            if (_beatIndex >= Beats.Length - 1)
            {
                Finish(completed: true);
                return;
            }

            _beatIndex++;
            ShowBeat(_beatIndex);
        }

        // =====================================================================
        //  Integrator notifications — gameplay reports an action done
        // -----------------------------------------------------------------------
        //  The village scene wires its gameplay events to these so an action
        //  beat advances when the player actually DOES the thing. Calling them
        //  when the tutorial is not on that beat is a harmless no-op, so the
        //  integrator can wire them unconditionally.
        // =====================================================================

        /// <summary>
        /// Integrator hook — call when the player places a building/tower.
        /// Auto-advances the "Raise a tower" beat. No-op on any other beat.
        /// </summary>
        public void NotifyTowerBuilt()
        {
            if (!_running) return;
            if (Beats[_beatIndex].Gate == TutorialGate.BuildTower)
                AdvanceFromBeat(_beatIndex);
        }

        /// <summary>
        /// Integrator hook — call when the player stations a pet at a slot.
        /// Auto-advances the "Your Wardens" beat. No-op on any other beat.
        /// </summary>
        public void NotifyPetPlaced()
        {
            if (!_running) return;
            if (Beats[_beatIndex].Gate == TutorialGate.PlacePet)
                AdvanceFromBeat(_beatIndex);
        }

        // =====================================================================
        //  Finish — set Onboarded, persist, fix the cold-open replay (P0-11)
        // =====================================================================

        /// <summary>
        /// Ends the tutorial. Whether the player completed all beats or skipped,
        /// this calls <see cref="GameStateService.FinishOnboarding"/> — which
        /// sets <see cref="GameState.Onboarded"/> = true and Save()s it — so the
        /// tutorial never replays AND the cold-open cinematic (gated on the same
        /// flag) stops re-playing every launch. On a completed run it also
        /// raises <see cref="BeginWaveRequested"/> so Wave 1 starts.
        /// </summary>
        private void Finish(bool completed)
        {
            using var _ = FlowTrace.Enter("Onboarding", $"OnboardingFlow.Finish (completed={completed})");
            if (!_running) { FlowTrace.Step("Onboarding", "Finish: not running — no-op."); return; }
            _running = false;
            HasFinished = true;

            // ── THE FIX (audit P0-11) ────────────────────────────────────────
            // Persist Onboarded = true through the Core save layer. FinishOnboarding
            // sets the flag, raises PlayerChanged and writes PlayerPrefs. This is
            // the single line that was missing from "normal play" — without it
            // Onboarded stayed false forever and StoryIntroController re-played
            // the cold open on every launch.
            var svc = GameStateService.Instance;
            if (svc != null)
            {
                svc.FinishOnboarding();
                // V — confirm the flag actually flipped; if not, the cold open replays
                // every launch (the P0-11 bug). Fail-loud so a stuck flag self-reports.
                bool onboarded = svc.State != null && svc.State.Onboarded;
                if (onboarded)
                    FlowTrace.Step("Onboarding", "Finish: FinishOnboarding ran — Onboarded=true persisted (cold-open replay fixed).");
                else
                    FlowTrace.Fail("Onboarding",
                        "Finish: FinishOnboarding ran but State.Onboarded is STILL false — the flag did not flip; cold open will replay.");
            }
            else
            {
                FlowTrace.Warn("Onboarding",
                    "Finish: No GameStateService — Onboarded was NOT persisted. The tutorial / cold open may replay next launch.");
            }

            // A completed run flows straight into Wave 1; a skip just closes.
            if (completed)
                BeginWaveRequested?.Invoke();

            FadeOverlay(1f, 0f).ContinueWith(() =>
            {
                SetOverlayVisible(false);
                TutorialClosed?.Invoke();
            }).Forget();
        }

        // =====================================================================
        //  Overlay visibility + fade
        // =====================================================================

        private void SetOverlayVisible(bool visible)
        {
            if (_root == null) return;
            // USS-class toggle for the UXML path …
            _root.EnableInClassList(RootHiddenClass, !visible);
            // … plus an inline display toggle so the code-built fallback overlay
            // (PIPELINE_STATE §8 — USS does not apply in builds) hides/shows too.
            _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>Fades the whole overlay opacity. Never <c>async void</c>.</summary>
        private async UniTask FadeOverlay(float from, float to)
        {
            if (_root == null) return;

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            if (_fadeSeconds <= 0f)
            {
                _root.style.opacity = to;
                return;
            }

            var elapsed = 0f;
            try
            {
                while (elapsed < _fadeSeconds)
                {
                    token.ThrowIfCancellationRequested();
                    _root.style.opacity = Mathf.Lerp(from, to, elapsed / _fadeSeconds);
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                    elapsed += Time.deltaTime;
                }
                _root.style.opacity = to;
            }
            catch (OperationCanceledException)
            {
                // Disabled / superseded mid-fade — snap to the target so the
                // overlay is never left at a half-faded opacity.
                if (_root != null) _root.style.opacity = to;
            }
        }
    }
}

// =============================================================================
// INTEGRATOR NOTES — wiring the first-run tutorial into the Village scene.
// -----------------------------------------------------------------------------
// DeNelle.Onboarding deliberately cannot see DeNelle.Village / DeNelle.HUD, so
// the village scene builder / VillageController owns every connection below:
//
//   1. Add a UIDocument to a tutorial GameObject in the Village scene; assign
//      TutorialOverlay.uxml as its Source Asset and the Onboarding panel
//      settings as its Panel Settings. Add this OnboardingFlow component beside
//      it. Put its UIDocument sort order ABOVE the VillageHud document so the
//      coach-marks paint over the HUD.
//
//   2. Run gate: leave _runOnStart = true — OnboardingFlow.Start() checks
//      GameState.Onboarded itself and shows the tutorial only on a first run.
//
//   3. Wire the tutorial's requests TO gameplay:
//        flow.OpenBuildMenuRequested.AddListener(buildMenu.Open);
//        flow.BeginWaveRequested.AddListener(() => waveManager.BeginLoop().Forget());
//        flow.TutorialClosed.AddListener(villageController.OnOnboardingClosed);
//      (WaveManager.BeginLoop() starts the wave loop at its _startWave — keep
//      that at 1; BeginLoop returns a UniTask, hence the .Forget().)
//
//   4. Wire gameplay events TO the tutorial so action-beats auto-advance:
//        buildMenu.BuildingPlaced += (_, _) => flow.NotifyTowerBuilt();
//        petDeployer.PetPlaced    += () => flow.NotifyPetPlaced();
//      (NotifyTowerBuilt / NotifyPetPlaced are safe no-ops off-beat — wire them
//      unconditionally. BuildMenu.BuildingPlaced already exists; PetDeployer
//      needs a similar placed-event or call NotifyPetPlaced from its placement
//      path.)
//
//   5. Hold Wave 1 until the tutorial closes — do NOT auto-call
//      WaveManager.BeginLoop() in the village scene's Start() for a first run;
//      let BeginWaveRequested be the sole kickoff so a first-run player is
//      taught before the dark arrives. A returning player (Onboarded already
//      true) gets TutorialClosed raised immediately by TryRun(), so the
//      integrator can start the loop from that listener instead.
//
// THE COLD-OPEN FIX: no StoryIntroController change is needed. Its
// ShouldAutoPlay already returns `!Onboarded`; it re-played only because the
// flag never flipped. OnboardingFlow.Finish() calls
// GameStateService.FinishOnboarding() on completion OR skip, which persists
// Onboarded = true — after the first run the cold open is correctly skipped.
//
// See docs/port-notes/onboarding.md.
// =============================================================================
