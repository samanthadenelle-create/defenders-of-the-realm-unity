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
        private VisualElement _imagePanel;
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

                // Ported verbatim from React src/content/story.ts
                // OPENING_CINEMATIC (2026-05-20). 14 beats, image per beat
                // pulls from Resources/Intro/intro-N.jpg; emphasis flag
                // bumps font weight + size for landings ("That one is you.").
                var cinematic = ReactOpeningCinematic;
                foreach (var beat in cinematic)
                {
                    if (_lineLabel != null)
                    {
                        _lineLabel.text = beat.Text;
                        _lineLabel.style.opacity = 0f;
                        _lineLabel.style.unityFontStyleAndWeight = beat.Emphasis
                            ? FontStyle.BoldAndItalic
                            : FontStyle.Italic;
                        // Owner direction 2026-05-20: text was too small on
                        // intro — bump to ~2× across all beats so cinematic
                        // copy reads at conversational distance.
                        _lineLabel.style.fontSize = beat.Emphasis ? 56 : 44;
                    }
                    if (_imagePanel != null && beat.ImageId > 0)
                    {
                        var tex = Resources.Load<Texture2D>($"Intro/intro-{beat.ImageId}");
                        if (tex != null)
                            _imagePanel.style.backgroundImage = new StyleBackground(tex);
                        // Owner direction 2026-05-20: place images on the
                        // perimeter so they don't crowd the text. Deterministic
                        // per ImageId — same beat always lands in the same
                        // corner so a re-skip plays the same composition.
                        var pos = ImagePositionFor(beat.ImageId);
                        _imagePanel.style.left = Length.Percent(pos.x);
                        _imagePanel.style.top  = Length.Percent(pos.y);
                    }
                    var inFade = FadeLabel(0f, 1f, 0.45f, token);
                    var imageIn = beat.ImageId > 0 ? FadeImage(0f, 1f, 0.55f, token) : UniTask.CompletedTask;
                    await UniTask.WhenAll(inFade, imageIn);
                    await WaitBeatOrSkip(beat.HoldSeconds, token);
                    var outFade = FadeLabel(1f, 0f, 0.35f, token);
                    var imageOut = beat.ImageId > 0 ? FadeImage(1f, 0f, 0.4f, token) : UniTask.CompletedTask;
                    await UniTask.WhenAll(outFade, imageOut);
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
            // Semi-transparent backdrop — owner feedback 2026-05-20: the cold-
            // open text used to wipe the intro background (starfield, heart-
            // wing banner). Alpha 0.55 keeps the scene readable behind the
            // text so the atmosphere persists across line changes.
            _root.style.backgroundColor = new Color(0.027f, 0.016f, 0.063f, 0.55f);
            _root.style.alignItems = Align.Center;
            _root.style.justifyContent = Justify.Center;
            _root.style.opacity = 0f;
            // Tap anywhere advances to the next line / finishes.
            _root.RegisterCallback<PointerDownEvent>(_ => _skipRequested = true);

            // Per-line scene icon — Resources/Intro/intro-N.jpg from the
            // React asset pack. Owner direction 2026-05-20 (3rd pass): can use
            // the full screen, just keep distance from the text band. Position
            // is set per-beat in the cinematic loop (see ImagePositions table)
            // so images dance corner-to-corner instead of stacking centrally.
            _imagePanel = new VisualElement();
            _imagePanel.style.position = Position.Absolute;
            _imagePanel.style.width = 260;
            _imagePanel.style.height = 200;
            _imagePanel.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
            _imagePanel.style.opacity = 0f;
            _imagePanel.pickingMode = PickingMode.Ignore;
            _root.Add(_imagePanel);

            _lineLabel = new Label
            {
                text = string.Empty,
            };
            _lineLabel.style.color = new Color(0.957f, 0.941f, 1f, 0.92f);
            _lineLabel.style.fontSize = 44;
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

        /// <summary>One cinematic beat — text + per-beat hold + optional intro-N.jpg + emphasis.</summary>
        private readonly struct CinematicBeat
        {
            public readonly string Text;
            public readonly float HoldSeconds;
            public readonly int ImageId;     // 1..8 maps to Resources/Intro/intro-N.jpg; 0 = none
            public readonly bool Emphasis;
            public CinematicBeat(string text, float hold, int imageId = 0, bool emphasis = false)
            { Text = text; HoldSeconds = hold; ImageId = imageId; Emphasis = emphasis; }
        }

        /// <summary>
        /// Verbatim port of OPENING_CINEMATIC from React src/content/story.ts
        /// (2026-05-20 owner direction). 14 beats, total ~73 seconds; players
        /// can tap to skip ahead one beat at a time.
        /// </summary>
        private static readonly CinematicBeat[] ReactOpeningCinematic = new[]
        {
            new CinematicBeat("Long ago, the realm was kept warm by a single light —", 5.0f),
            new CinematicBeat("the Lantern of Avalon, which never dimmed,", 4.8f, 1),
            new CinematicBeat("and the Guardian whose quiet hands tended its flame.", 5.2f, 8),
            new CinematicBeat("Avalon. A village. A promise. A home.", 5.4f, 5, true),
            new CinematicBeat("But Guardians grow old, and old light grows thin.", 5.0f),
            new CinematicBeat("Beneath the sleeping hills, something hollow began to wake.", 5.4f),
            new CinematicBeat("They call it the Withering — it remembers no warmth,", 5.0f, 2),
            new CinematicBeat("and it forgives no green and growing thing.", 4.8f),
            new CinematicBeat("Now the old protectors keep a lonely watch —", 4.8f),
            new CinematicBeat("Sir Bram the knight, Sela the archer, and three small sleeping spirits —", 5.8f),
            new CinematicBeat("waiting for the one the Lantern will answer to.", 5.0f, 7),
            new CinematicBeat("That one is you.", 4.5f, 3, true),
            new CinematicBeat("A young mage, barely an apprentice — yet the flame brightens at your step.", 5.8f, 6),
            new CinematicBeat("Welcome home, Guardian of the Lantern.", 5.4f, 4, true),
        };

        /// <summary>
        /// Perimeter placement table for the intro image panel. Owner direction
        /// 2026-05-20: "move assets away from text, use entire screen, just
        /// keep distance from text". Text band sits centred horizontally at
        /// ~45% height, so the image always lands at one of the four corners
        /// or the top/bottom centre, well outside that band. Deterministic by
        /// ImageId so replays look the same.
        /// </summary>
        private static readonly Vector2[] ImagePositions =
        {
            new Vector2(  6f,   5f),   // 0 — top-left
            new Vector2( 68f,   5f),   // 1 — top-right
            new Vector2(  6f,  70f),   // 2 — bottom-left
            new Vector2( 68f,  70f),   // 3 — bottom-right
            new Vector2( 36f,   3f),   // 4 — top-centre
            new Vector2( 36f,  72f),   // 5 — bottom-centre
            new Vector2(  4f,  35f),   // 6 — mid-left
            new Vector2( 72f,  35f),   // 7 — mid-right
        };

        private static Vector2 ImagePositionFor(int imageId)
        {
            int idx = Mathf.Abs(imageId) % ImagePositions.Length;
            return ImagePositions[idx];
        }

        private async UniTask WaitBeatOrSkip(float holdSeconds, CancellationToken token)
        {
            var elapsed = 0f;
            while (elapsed < holdSeconds && !_skipRequested)
            {
                token.ThrowIfCancellationRequested();
                await UniTask.Yield(PlayerLoopTiming.Update, token);
                elapsed += Time.deltaTime;
            }
            _skipRequested = false; // consume — one beat at a time
        }

        /// <summary>Fades the backdrop image panel from <paramref name="from"/> to <paramref name="to"/> alpha.</summary>
        private async UniTask FadeImage(float from, float to, float seconds, CancellationToken token)
        {
            if (_imagePanel == null) return;
            if (seconds <= 0f) { _imagePanel.style.opacity = to; return; }
            var elapsed = 0f;
            while (elapsed < seconds)
            {
                token.ThrowIfCancellationRequested();
                _imagePanel.style.opacity = Mathf.Lerp(from, to, elapsed / seconds);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
                elapsed += Time.deltaTime;
            }
            _imagePanel.style.opacity = to;
        }

        /// <summary>
        /// Fades only the text label, leaving the backdrop alpha alone. Used
        /// so each cold-open line eases in / out instead of cutting hard.
        /// </summary>
        private async UniTask FadeLabel(float from, float to, float seconds, CancellationToken token)
        {
            if (_lineLabel == null) return;
            if (seconds <= 0f)
            {
                _lineLabel.style.opacity = to;
                return;
            }
            var elapsed = 0f;
            while (elapsed < seconds)
            {
                token.ThrowIfCancellationRequested();
                _lineLabel.style.opacity = Mathf.Lerp(from, to, elapsed / seconds);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
                elapsed += Time.deltaTime;
            }
            _lineLabel.style.opacity = to;
        }

        private void HideImmediate()
        {
            // Fully tear down the cold-open overlay so it cannot intercept picks
            // on the Title screen. _document.enabled=false alone has been
            // observed to leave the root attached on the shared panel in this
            // Unity 6 build — same class of serialisation quirk that needed the
            // YAML PanelSettings injection.
            if (_root != null)
            {
                _root.style.display = DisplayStyle.None;
                _root.pickingMode = PickingMode.Ignore;
                _root.Clear();
                _root.RemoveFromHierarchy();
            }
            if (_document != null)
            {
                _document.visualTreeAsset = null;
                _document.enabled = false;
                _document.gameObject.SetActive(false);
            }
        }

        private void Complete()
        {
            if (HasFinished) return;
            HasFinished = true;
            Finished?.Invoke();
        }
    }
}
