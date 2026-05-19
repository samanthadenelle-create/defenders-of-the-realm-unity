// =============================================================================
// StoryIntroController — the three-line cold open (Week 1)
// -----------------------------------------------------------------------------
// Port of src/modules/onboarding/StoryIntro.tsx. Plays the opening cinematic:
// the three cold-open lines from narrative-bible.md §7.1, one at a time, over a
// dark backdrop, ~5 seconds total, then hands control back to the Title screen.
//
// FIRST-LAUNCH GATE. The React version stored an `avalon-intro-seen` localStorage
// flag. The v2 port-spec maps onboarding persistence onto the GameState
// `Onboarded` flag (GameState.cs #3 — "true once pet creation + tutorial are
// complete"). There is no Week-1 scene yet where the player finishes onboarding,
// so the practical first-launch signal available now is `!Onboarded`: a brand-
// new save has Onboarded=false and gets the cold open; once the player has
// onboarded, Onboarded=true and the cinematic is skipped. This is the closest
// persisted analog to the React flag and is recorded in the decisions log.
//
// The cold-open TEXT is never hardcoded — CanonStrings.ColdOpenLines() reads it
// from en.json (v2 port-spec Part 4). Lines are shown via a UGUI Text/TMP-less
// path is avoided; this controller drives a UI Toolkit-free CanvasGroup + Text
// is also avoided — instead it owns its own UIDocument-independent overlay made
// of a CanvasGroup the builder wires. To keep the module dependency-light and
// match SplashLoading's fade approach, the overlay here is a UI Toolkit
// VisualElement created at runtime on a dedicated UIDocument.
//
// async UniTask throughout — never async void (port-spec Part 3 mandate).
// =============================================================================

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DeNelle.Core.State;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeNelle.Onboarding
{
    /// <summary>
    /// Plays the first-launch cold-open cinematic — three lines from
    /// <c>en.json</c>, ~5 seconds, then raises <see cref="Finished"/>.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class StoryIntroController : MonoBehaviour
    {
        [Header("Timing")]
        [Tooltip("Seconds each cold-open line holds on screen before the next.")]
        [SerializeField] private float _lineHoldSeconds = 1.6f;

        [Tooltip("Seconds the fade-in / fade-out of the whole overlay takes.")]
        [SerializeField] private float _fadeSeconds = 0.5f;

        [Header("Debug")]
        [Tooltip("Force the cinematic to play even when the save says onboarded. Editor testing only.")]
        [SerializeField] private bool _forcePlay;

        /// <summary>
        /// Raised when the cinematic finishes — either after the last line's
        /// hold expires, when the player taps/keys past it, or immediately when
        /// the first-launch gate says it should not play at all. The host
        /// (the Title scene) shows the title screen after this fires.
        /// </summary>
        public event Action Finished;

        private UIDocument _document;
        private VisualElement _root;
        private Label _lineLabel;
        private bool _skipRequested;
        private CancellationTokenSource _cts;

        /// <summary>True once <see cref="Play"/> has run to completion.</summary>
        public bool HasFinished { get; private set; }

        private void Awake()
        {
            _document = GetComponent<UIDocument>();
        }

        private void OnDisable()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        /// <summary>
        /// True on a brand-new save — the cold open should auto-play. Mirrors
        /// the React <c>avalon-intro-seen</c> check, mapped onto the persisted
        /// <see cref="GameState.Onboarded"/> flag (see the file header).
        /// </summary>
        public bool ShouldAutoPlay
        {
            get
            {
                if (_forcePlay) return true;
                var svc = GameStateService.Instance;
                // No service yet (Core not bootstrapped) — treat as first launch.
                if (svc == null || svc.State == null) return true;
                return !svc.State.Onboarded;
            }
        }

        /// <summary>
        /// Runs the cold open. On first launch it fades in, shows the three
        /// lines, fades out, then raises <see cref="Finished"/>. On a returning
        /// save it raises <see cref="Finished"/> immediately (no flash). Never
        /// <c>async void</c>.
        /// </summary>
        public async UniTask Play()
        {
            if (HasFinished) return;

            if (!ShouldAutoPlay)
            {
                HideImmediate();
                Complete();
                return;
            }

            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            BuildOverlay();
            _skipRequested = false;

            try
            {
                await Fade(0f, 1f, _fadeSeconds, token);

                var lines = CanonStrings.ColdOpenLines();
                foreach (var line in lines)
                {
                    if (_lineLabel != null) _lineLabel.text = line;
                    await WaitLineOrSkip(token);
                    if (_skipRequested) break;
                }

                await Fade(1f, 0f, _fadeSeconds, token);
            }
            catch (OperationCanceledException)
            {
                // Disabled mid-play — fall through to Complete so the host
                // is never left waiting on a dead cinematic.
            }

            HideImmediate();
            Complete();
        }

        // ── Overlay construction (runtime UI Toolkit, no .uxml needed) ───────
        private void BuildOverlay()
        {
            _root = _document.rootVisualElement;
            _root.Clear();
            _root.style.flexGrow = 1;
            _root.style.backgroundColor = new Color(0.027f, 0.016f, 0.063f, 1f); // #07040F
            _root.style.alignItems = Align.Center;
            _root.style.justifyContent = Justify.Center;
            _root.style.opacity = 0f;
            // Tap anywhere advances to the next line / finishes.
            _root.RegisterCallback<PointerDownEvent>(_ => _skipRequested = true);

            _lineLabel = new Label
            {
                text = string.Empty,
            };
            _lineLabel.style.color = new Color(0.957f, 0.941f, 1f, 0.92f);
            _lineLabel.style.fontSize = 22;
            _lineLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            _lineLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _lineLabel.style.whiteSpace = WhiteSpace.Normal;
            _lineLabel.style.maxWidth = 620;
            _root.Add(_lineLabel);

            var skip = new Button(() => _skipRequested = true) { text = "Skip" };
            skip.style.position = Position.Absolute;
            skip.style.top = 18;
            skip.style.right = 18;
            skip.style.minWidth = 64;
            skip.style.height = 44;
            _root.Add(skip);
        }

        private async UniTask WaitLineOrSkip(CancellationToken token)
        {
            var elapsed = 0f;
            while (elapsed < _lineHoldSeconds && !_skipRequested)
            {
                token.ThrowIfCancellationRequested();
                await UniTask.Yield(PlayerLoopTiming.Update, token);
                elapsed += Time.deltaTime;
            }
            // Consume the skip so it advances exactly one line at a time.
            _skipRequested = false;
        }

        private async UniTask Fade(float from, float to, float seconds, CancellationToken token)
        {
            if (_root == null) return;
            if (seconds <= 0f)
            {
                _root.style.opacity = to;
                return;
            }
            var elapsed = 0f;
            while (elapsed < seconds)
            {
                token.ThrowIfCancellationRequested();
                _root.style.opacity = Mathf.Lerp(from, to, elapsed / seconds);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
                elapsed += Time.deltaTime;
            }
            _root.style.opacity = to;
        }

        private void HideImmediate()
        {
            if (_root != null) _root.style.display = DisplayStyle.None;
            if (_document != null) _document.enabled = false;
        }

        private void Complete()
        {
            if (HasFinished) return;
            HasFinished = true;
            Finished?.Invoke();
        }
    }
}
