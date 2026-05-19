// =============================================================================
// SplashLoading — the DeNelle Studios studio bumper (Week 1)
// -----------------------------------------------------------------------------
// Port of src/modules/onboarding/SplashLoading.tsx + StudioBumper.tsx. Plays the
// one-time "developed by DeNelle Studios" bumper before the Title screen takes
// over: a UnityEngine.Video.VideoPlayer streams studio-bumper.mp4 (~3 seconds),
// then the overlay fades out and control passes to the Title screen.
//
// The React StudioBumper had three render states (video / static fallback /
// not-rendered-for-returning-players). Week 1 keeps the two that matter for a
// desktop build: VIDEO playback, and a STATIC FALLBACK card ("DeNelle Studios /
// presents") shown when the clip is missing, errors, or stalls. The studio name
// on the fallback card is read from canon-strings.json (key "publisher") — never
// hardcoded (v2 port-spec Part 4).
//
// async UniTask throughout — never async void (port-spec Part 3 mandate).
// =============================================================================

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Video;

namespace DeNelle.Onboarding
{
    /// <summary>
    /// Plays the studio bumper video (~3s) then fades to the Title screen.
    /// Falls back to a static "DeNelle Studios presents" card if the clip is
    /// missing or errors.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class SplashLoading : MonoBehaviour
    {
        [Header("Video")]
        [Tooltip("The DeNelle Studios bumper — studio-bumper.mp4 imported as a VideoClip.")]
        [SerializeField] private VideoPlayer _videoPlayer;

        [Tooltip("The RenderTexture the VideoPlayer renders into; shown on the overlay.")]
        [SerializeField] private RenderTexture _videoTexture;

        [Tooltip("Play the studio-bumper.mp4 video. ON: the clip is imported with " +
                 "VideoClip transcoding enabled (see BumperVideoImport), so Unity " +
                 "re-encodes it to a decoder-safe format and it no longer hangs the " +
                 "Windows video decoder. Turn OFF to fall back to the static " +
                 "'DeNelle Studios presents' card if a clip ever misbehaves.")]
        [SerializeField] private bool _playBumperVideo = true;

        [Header("Timing")]
        [Tooltip("Hard cap on bumper duration — if the clip runs long or stalls, finish anyway.")]
        [SerializeField] private float _maxBumperSeconds = 3.5f;

        [Tooltip("If the video has not started playing within this many seconds, use the static fallback card.")]
        [SerializeField] private float _videoStartTimeoutSeconds = 1f;

        [Tooltip("How long the static fallback card holds before finishing.")]
        [SerializeField] private float _staticHoldSeconds = 1.5f;

        [Tooltip("Seconds the overlay's fade-out takes before handing off to the Title screen.")]
        [SerializeField] private float _fadeSeconds = 0.5f;

        /// <summary>
        /// Raised once the bumper completes — video ended, fallback timed out,
        /// or the duration cap fired. The host shows the Title screen after.
        /// </summary>
        public event Action Finished;

        private UIDocument _document;
        private VisualElement _root;
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
        /// Plays the bumper. Streams the video into the overlay, waits for it
        /// to end (or the duration cap), fades out, then raises
        /// <see cref="Finished"/>. Never <c>async void</c>.
        /// </summary>
        public async UniTask Play()
        {
            if (HasFinished) return;

            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            BuildOverlay(out var videoElement, out var fallbackCard);

            try
            {
                var playedVideo = await TryPlayVideo(videoElement, fallbackCard, token);
                if (!playedVideo)
                {
                    // Static fallback path — hold the card, then move on.
                    await UniTask.Delay(TimeSpan.FromSeconds(_staticHoldSeconds),
                        DelayType.DeltaTime, PlayerLoopTiming.Update, token);
                }
                await Fade(1f, 0f, _fadeSeconds, token);
            }
            catch (OperationCanceledException)
            {
                // Disabled mid-play — fall through so the host is not stranded.
            }

            HideImmediate();
            Complete();
        }

        // =====================================================================
        //  Video playback
        // =====================================================================

        private async UniTask<bool> TryPlayVideo(
            VisualElement videoElement, VisualElement fallbackCard, CancellationToken token)
        {
            // Video disabled (see _playBumperVideo) or no clip — go straight to
            // the static "DeNelle Studios presents" card. This guard is what
            // keeps a malformed clip from deadlocking the native video decoder.
            if (!_playBumperVideo || _videoPlayer == null || _videoPlayer.clip == null)
            {
                ShowFallback(videoElement, fallbackCard);
                return false;
            }

            var errored = false;
            void OnError(VideoPlayer _, string message)
            {
                Debug.LogWarning($"[SplashLoading] Studio bumper video error: {message}");
                errored = true;
            }
            _videoPlayer.errorReceived += OnError;

            try
            {
                _videoPlayer.isLooping = false;
                _videoPlayer.Prepare();

                // Wait for the clip to be ready, bounded by the start timeout.
                var waited = 0f;
                while (!_videoPlayer.isPrepared && !errored && waited < _videoStartTimeoutSeconds)
                {
                    token.ThrowIfCancellationRequested();
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                    waited += Time.deltaTime;
                }

                if (errored || !_videoPlayer.isPrepared)
                {
                    ShowFallback(videoElement, fallbackCard);
                    return false;
                }

                _videoPlayer.Play();

                // Play until the clip ends, an error fires, or the safety cap is
                // hit. The cap follows the clip's own length (+1s margin) so a
                // longer bumper plays in full; _maxBumperSeconds is the floor for
                // a very short clip.
                float playCap = _videoPlayer.length > 0d
                    ? Mathf.Max(_maxBumperSeconds, (float)_videoPlayer.length + 1f)
                    : _maxBumperSeconds;
                var elapsed = 0f;
                while (_videoPlayer.isPlaying && !errored && elapsed < playCap)
                {
                    token.ThrowIfCancellationRequested();
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                    elapsed += Time.deltaTime;
                }

                if (errored)
                {
                    ShowFallback(videoElement, fallbackCard);
                    return false;
                }

                _videoPlayer.Stop();
                return true;
            }
            finally
            {
                _videoPlayer.errorReceived -= OnError;
            }
        }

        private static void ShowFallback(VisualElement videoElement, VisualElement fallbackCard)
        {
            if (videoElement != null) videoElement.style.display = DisplayStyle.None;
            if (fallbackCard != null) fallbackCard.style.display = DisplayStyle.Flex;
        }

        // =====================================================================
        //  Overlay construction (runtime UI Toolkit)
        // =====================================================================

        private void BuildOverlay(out VisualElement videoElement, out VisualElement fallbackCard)
        {
            _root = _document.rootVisualElement;
            _root.Clear();
            _root.style.flexGrow = 1;
            _root.style.backgroundColor = Color.black;
            _root.style.alignItems = Align.Center;
            _root.style.justifyContent = Justify.Center;
            _root.style.opacity = 1f;

            // The video surface — shows the VideoPlayer's RenderTexture.
            videoElement = new VisualElement { name = "bumper-video" };
            videoElement.style.flexGrow = 1;
            videoElement.style.width = Length.Percent(100);
            videoElement.style.height = Length.Percent(100);
            if (_videoTexture != null)
            {
                videoElement.style.backgroundImage = Background.FromRenderTexture(_videoTexture);
                videoElement.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
            }
            _root.Add(videoElement);

            // The static fallback card — hidden unless the video fails.
            fallbackCard = new VisualElement { name = "bumper-fallback" };
            fallbackCard.style.position = Position.Absolute;
            fallbackCard.style.alignItems = Align.Center;
            fallbackCard.style.justifyContent = Justify.Center;
            fallbackCard.style.display = DisplayStyle.None;

            var studioLabel = new Label(CanonStrings.Publisher);
            studioLabel.style.color = new Color(1f, 0.81f, 0.4f, 0.9f);
            studioLabel.style.fontSize = 40;
            studioLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            studioLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            fallbackCard.Add(studioLabel);

            var presentsLabel = new Label("presents");
            presentsLabel.style.color = new Color(1f, 1f, 1f, 0.4f);
            presentsLabel.style.fontSize = 12;
            presentsLabel.style.letterSpacing = 5;
            presentsLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            presentsLabel.style.marginTop = 8;
            fallbackCard.Add(presentsLabel);

            _root.Add(fallbackCard);
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
