// =============================================================================
// TitleController — the Title scene orchestrator (Week 1 -> WO-C uGUI conversion)
// -----------------------------------------------------------------------------
// WO-C part 1 (2026-07-03, coverage matrix row #15): UIDocument/UITK title
// -> code-built uGUI on the Blink Obsidian kit (ElarionUiKit). The title is the
// FIRST-IMPRESSION surface, so it now renders through the same proven uGUI path
// as the rest of the game (HelpMenu is the reference conversion) instead of the
// UI Toolkit panel that fought four other UIDocuments over the one shared
// "OnboardingPanelSettings" asset (the duplicate-UIDocument input-eating bug).
// This controller no longer renders through a UIDocument AT ALL; the legacy
// scene documents it used to own are explicitly DISABLED in Awake so they can
// neither draw nor steal input.
//
// FLOW CONTRACT (preserved verbatim from the UITK version):
//   * Owner 2026-06-04 SPLASH GATE — the scene opens on a static title screen;
//     the first button press is the browser's audio-unlock gesture.
//   * Continue    -> resume into the Castle home hub (persists Knight at the
//                    load source if the save carries no HeroClass — V1 single-hero).
//   * Start New   -> full save + dialogue reset, fast-path onboarding
//                    (OnboardingMode.ChooseFastPath), route to the HeroSelect
//                    carousel (WO-559: HeroClass=None so the carousel builds).
//   * Play Intro  -> OnboardingMode.ChooseFullTutorial + clear persisted hero,
//                    then the 9-screen cinematic via Core.IntroLauncher; falls
//                    back to the StoryIntro cold-open when no intro player is
//                    registered (build without dialogue assets).
//   * DEF-253 watchdog — the cold-open fallback can never strand the player:
//     SafeStage times each stage out AND an unscaled Update timer force-returns
//     to the title menu.
//
// The old in-Title 4-card hero-select (BuildTitleScreen) is RETIRED: WO-559
// moved hero selection to the HeroSelect carousel scene and no route reached
// the in-Title cards any more (only the watchdog fallback did, showing a screen
// the real flow never used). The watchdog now returns to the title MENU instead.
// With it go the UITK-only workarounds it dragged along (NeutralizeOverlayPanels,
// the WebGL orphan re-assert, PANELDIAG) — none apply to a uGUI canvas.
//
// Visuals: full-screen title art (Resources/Title/Title_L landscape,
// Title_H portrait — the title text is baked into this art), with a vertical
// stack of Obsidian family buttons (Continue = Green when a save exists,
// Start New = Yellow, Play Intro = Gray). When the art is missing the screen
// falls back to an obsidian backdrop with the kit-typography title block
// (CanonStrings — never hardcoded, v2 port-spec Part 4) so it can never blank.
//
// async UniTask for the arrival flow — never async void (port-spec Part 3).
// =============================================================================

using Cysharp.Threading.Tasks;
using DeNelle.Core;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.State;
using DeNelle.Core.UI;
using UnityEngine;
using UnityEngine.UI;

namespace DeNelle.Onboarding
{
    /// <summary>
    /// Drives the Title scene: the splash-gate title menu (Continue / Start New /
    /// Play Intro) and the cold-open fallback arrival sequence. Code-built uGUI
    /// on the Obsidian kit — no UIDocument.
    /// </summary>
    public sealed class TitleController : MonoBehaviour
    {
        [Header("Arrival sequence")]
        [Tooltip("The studio bumper — CUT (owner 2026-06-04) but kept wired by OnboardingSceneBuilder.")]
        [SerializeField] private SplashLoading _splash;

        [Tooltip("The cold-open cinematic — the Play-Intro fallback when IntroLauncher is absent.")]
        [SerializeField] private StoryIntroController _storyIntro;

        [Header("Legacy (WO-C)")]
        [Tooltip("The retired Title UIDocument (TitleScreen.uxml). Still wired by " +
                 "OnboardingSceneBuilder; disabled in Awake so it cannot render or eat input.")]
        [SerializeField] private UnityEngine.UIElements.UIDocument _titleDocument;

        // ── Code-built uGUI title menu ────────────────────────────────────────
        private GameObject _canvas;              // the whole title screen
        private Image _backdropFill;             // obsidian floor — never blank
        private Image _backdropArt;              // Title_L / Title_H cover art
        private AspectRatioFitter _backdropFitter;
        private bool _backdropArtLandscape;      // which orientation art is loaded
        private bool _backdropArtLoaded;
        private readonly Sprite[] _titleArt = new Sprite[2];   // [0]=portrait, [1]=landscape

        // Owner 2026-06-04: web-standard SPLASH GATE. Browsers block audio until a
        // user gesture; the first button press on this menu is that gesture.
        // _splashActive = the menu is up and accepting a choice.
        private bool _splashActive;

        // The 9-screen cinematic / cold-open owns the screen (suppresses the watchdog).
        private bool _introPlaying;

        // DEF-253 hard watchdog: if the cold-open fallback stalls (a WebGL await that
        // never resolves), this plain-Update unscaled timer force-returns to the title
        // menu. Belt-and-braces on top of RunArrival's SafeStage timeouts.
        private bool _arrivalRunning;
        private float _arrivalStart;
        private const float MaxIntroSeconds = 8f;

        private void Awake()
        {
            DisableLegacyUiDocuments();
        }

        private void OnEnable()
        {
            // Animated star/comet background — replaces the React build's
            // landing-page parallax that owners said pulled players in during
            // the 10-15 s decision window. Spawned once per Title scene load.
            if (GameObject.Find("TitleStarfield") == null)
                new GameObject("TitleStarfield").AddComponent<TitleStarfield>();
        }

        private void Start()
        {
            using var _ = FlowTrace.Enter("Onboarding", "TitleController.Start (uGUI title menu)");
            BuildTitleMenu();
            SetTitleVisible(true);
            _splashActive = true;
        }

        private void OnDestroy()
        {
            if (_canvas != null) Destroy(_canvas);
        }

        /// <summary>
        /// WO-C: this controller renders in uGUI now, but the Title scene (built by
        /// OnboardingSceneBuilder before the conversion) still carries the legacy
        /// UIDocuments — the "TitleScreen UIDocument" and the one RequireComponent
        /// used to force onto this GameObject. Five enabled documents sharing the
        /// one OnboardingPanelSettings asset was the input-eating duplicate-panel
        /// bug, so disable ours explicitly: they render nothing for us any more and
        /// must not keep a PanelRaycaster in the click stack.
        /// </summary>
        private void DisableLegacyUiDocuments()
        {
            Guard.Try("Onboarding", "disable legacy Title UIDocuments", () =>
            {
                int disabled = 0;
                if (_titleDocument != null && _titleDocument.enabled)
                {
                    _titleDocument.enabled = false;
                    disabled++;
                }
                var own = GetComponent<UnityEngine.UIElements.UIDocument>();
                if (own != null && own.enabled)
                {
                    own.enabled = false;
                    disabled++;
                }
                // Census note: the bumper (SplashLoading) + MusicSelectionPanel docs are
                // owned by their controllers and are deliberately NOT touched here.
                FlowTrace.Step("Onboarding",
                    $"WO-C: disabled {disabled} legacy Title UIDocument(s) — the title renders via uGUI now " +
                    $"(bumper wired={_splash != null}, left to its owner).");
            });
        }

        // =====================================================================
        //  Title menu (code-built uGUI on the Obsidian kit)
        // =====================================================================

        private void BuildTitleMenu()
        {
            if (_canvas != null) return;

            _canvas = ElarionUiKit.BuildModalCanvas("TitleScreenUI", 100);
            SceneRootAdopt(_canvas);

            // Obsidian floor — the screen can NEVER blank, even with no art on disk.
            var fillGo = new GameObject("BackdropFill", typeof(Image));
            fillGo.transform.SetParent(_canvas.transform, false);
            Stretch(fillGo);
            _backdropFill = fillGo.GetComponent<Image>();
            _backdropFill.color = ElarionUiKit.ObsidianFill;
            _backdropFill.raycastTarget = true;   // eat stray taps outside the buttons

            // Cover art (title text is baked into the art). AspectRatioFitter in
            // EnvelopeParent mode reproduces the old ScaleAndCrop: the art always
            // covers the screen, cropping the overflow edge, never letterboxing.
            var artGo = new GameObject("BackdropArt", typeof(Image), typeof(AspectRatioFitter));
            artGo.transform.SetParent(_canvas.transform, false);
            Stretch(artGo);
            _backdropArt = artGo.GetComponent<Image>();
            _backdropArt.raycastTarget = false;
            _backdropArt.preserveAspect = false;
            _backdropFitter = artGo.GetComponent<AspectRatioFitter>();
            _backdropFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            ApplyBackdropArt(force: true);

            // Art-missing fallback: the kit-typography title block (canon strings).
            if (!_backdropArtLoaded)
                BuildTitleTextBlock(_canvas.transform);

            BuildButtonColumn(_canvas.transform);
            BuildSkrBadge(_canvas.transform);

            FlowTrace.Step("Onboarding",
                $"Title menu built (uGUI) — art={( _backdropArtLoaded ? "loaded" : "MISSING (text fallback)")} " +
                $"saveExists={HasExistingSave()}.");
        }

        /// <summary>Landscape/portrait cover art, swapped when the orientation flips.</summary>
        private void ApplyBackdropArt(bool force = false)
        {
            bool landscape = Screen.width >= Screen.height;
            if (!force && _backdropArtLoaded && landscape == _backdropArtLandscape) return;

            int slot = landscape ? 1 : 0;
            if (_titleArt[slot] == null)
            {
                var tex = Resources.Load<Texture2D>(landscape ? "Title/Title_L" : "Title/Title_H");
                if (tex != null)
                    _titleArt[slot] = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
                                                    new Vector2(0.5f, 0.5f), 100f);
            }

            var sprite = _titleArt[slot];
            if (sprite == null)
            {
                if (_backdropArt != null) _backdropArt.enabled = false;
                _backdropArtLoaded = false;
                FlowTrace.Warn("Onboarding",
                    $"Title art Resources/Title/{(landscape ? "Title_L" : "Title_H")} not found — obsidian + text fallback.");
                return;
            }

            _backdropArt.enabled = true;
            _backdropArt.sprite = sprite;
            _backdropFitter.aspectRatio = sprite.rect.height > 0f
                ? sprite.rect.width / sprite.rect.height : 1f;
            _backdropArtLandscape = landscape;
            _backdropArtLoaded = true;
        }

        /// <summary>Game title / series / tagline in the kit's title typography —
        /// shown only when the cover art (which bakes the title in) is absent.</summary>
        private static void BuildTitleTextBlock(Transform parent)
        {
            var title = ElarionUiKit.Label(parent, CanonStrings.GameTitle,
                0.70f, 0.84f, ElarionUi.Parchment, 64,
                TMPro.TextAlignmentOptions.Center, 0.05f, 0.95f, spacing: 3f, bold: true);
            ElarionUiKit.EnsureFont(title, ElarionUiKit.FontRole.Title);

            var series = ElarionUiKit.Label(parent, CanonStrings.GameSubtitle,
                0.655f, 0.70f, ElarionUi.Gold, 26,
                TMPro.TextAlignmentOptions.Center, 0.05f, 0.95f, spacing: 5f, bold: true);
            ElarionUiKit.EnsureFont(series, ElarionUiKit.FontRole.Title);

            var tagline = ElarionUiKit.Label(parent, CanonStrings.Tagline,
                0.60f, 0.65f, ElarionUi.ParchmentDim, 24,
                TMPro.TextAlignmentOptions.Center, 0.05f, 0.95f);
            ElarionUiKit.EnsureFont(tagline, ElarionUiKit.FontRole.Body);
        }

        /// <summary>The Obsidian button row (owner F8 2026-07-03): small, clean, rounded
        /// buttons on ONE horizontal row, bottom-centre over the art. "Start New" label
        /// is forced WHITE for high contrast ("pops").</summary>
        private void BuildButtonColumn(Transform parent)
        {
            var row = new GameObject("TitleButtons", typeof(RectTransform));
            row.transform.SetParent(parent, false);
            var rt = (RectTransform)row.transform;
            // A single low, wide band — small clean buttons side-by-side. Kept a healthy
            // ~7% screen-height so the touch target stays tappable on mobile.
            rt.anchorMin = new Vector2(0.10f, 0.070f);
            rt.anchorMax = new Vector2(0.90f, 0.140f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            // Continue is Green and only present when a save exists (owner spec) —
            // resuming players see it first; fresh installs see Start New leftmost.
            bool hasSave = HasExistingSave();
            var entries = new System.Collections.Generic.List<(string label,
                ElarionUiKit.ObsidianButtonColor color, System.Action onClick, bool whiteLabel)>();
            if (hasSave)
                entries.Add(("Continue", ElarionUiKit.ObsidianButtonColor.Green, OnContinue, false));
            entries.Add(("Start New", ElarionUiKit.ObsidianButtonColor.Yellow, OnStartNew, true));
            entries.Add(("Play Intro", ElarionUiKit.ObsidianButtonColor.Gray, OnPlayIntro, false));

            // Even HORIZONTAL distribution across the row, left to right.
            const float slotGap = 0.03f;
            float slotW = (1f - slotGap * (entries.Count - 1)) / entries.Count;
            for (int i = 0; i < entries.Count; i++)
            {
                float x0 = i * (slotW + slotGap);
                float x1 = x0 + slotW;
                var e = entries[i];
                var btn = ElarionUiKit.BuildObsidianButton(row.transform, e.label,
                    ElarionUiKit.ObsidianButtonStyle.Style1, e.color,
                    new Vector2(x0, 0f), new Vector2(x1, 1f), e.onClick);

                // Owner F8: "make the start new text white as well" — force the label
                // TMP colour to white for high contrast where requested.
                if (e.whiteLabel && btn != null)
                {
                    var lbl = btn.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
                    if (lbl != null) lbl.color = Color.white;
                }
            }
        }

        /// <summary>"POWERED WITH SKR" grant badge (owner 2026-07-04, ff.skrpreview). Gated OFF by
        /// default so normal players never see it; the grant-recording build flips ff.skrpreview ON
        /// (menu / PlayerPrefs / ?skrpreview=1). One tap opens the read-only, clearly-labeled
        /// <see cref="DeNelle.Core.UI.SkrShowcasePanel"/> — branding + honest value-prop, NO wallet call.
        /// A small gold pill high-center over the art so it reads on camera without crowding the menu.</summary>
        private static void BuildSkrBadge(Transform parent)
        {
            // ?skrpreview=1 (WebGL) is picked up here so the grant build needs no rebuild to flip on.
            FeatureFlags.ApplyUrlActivationOnce();
            if (!FeatureFlags.SkrPreview) return;

            var btn = ElarionUiKit.BuildObsidianButton(parent, "Powered with SKR",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Yellow,
                new Vector2(0.34f, 0.905f), new Vector2(0.66f, 0.965f),
                () => DeNelle.Core.UI.SkrShowcasePanel.Open());

            FlowTrace.Step("Onboarding", "Title: 'Powered with SKR' grant badge shown (ff.skrpreview ON).");
        }

        /// <summary>True when persisted progress exists — a chosen hero or a completed
        /// onboarding. Gates the Continue button (fresh installs have nothing to resume).</summary>
        private static bool HasExistingSave()
        {
            var svc = GameStateService.Instance;
            if (svc == null || svc.State == null) return false;
            return svc.State.HeroClass.ToNullable().HasValue || svc.State.Onboarded;
        }

        // =====================================================================
        //  Menu actions (flow contract preserved from the UITK version)
        // =====================================================================

        // Start New: a genuinely FRESH game, routed to the HeroSelect carousel.
        private void OnStartNew()
        {
            if (!_splashActive) return;
            _splashActive = false;

            // Wipe the save progression (this also clears SeenTutorials, so once-only
            // recruit/intro beats replay) AND all dialogue state — the $-toggle
            // variable storage and the gameplay->dialogue event latches — so no stale
            // toggle from a prior run carries over. Continue does NOT do this.
            GameStateService.Instance?.ResetToNewGame();
            DeNelle.Core.DialogueResetService.ResetForNewGame();

            // DEF onboarding fast-path (owner: fast into battle). "Start New" takes the
            // FAST PATH — a brief companion hook in the village then straight to Wave 1.
            DeNelle.Core.OnboardingMode.ChooseFastPath();

            // WO-559: route to the HeroSelect CAROUSEL scene. ResetToNewGame set
            // HeroClass=None and Save()'d, so the carousel BUILDS instead of
            // self-skipping to the castle.
            FlowTrace.Step("Onboarding", "OnStartNew: routing to the HeroSelect carousel (fresh HeroClass=None).");
            SceneRouter.GoHeroSelect();
        }

        // Play Intro: the full 9-screen cinematic intro, which ends by routing to
        // hero select itself. Falls back to the StoryIntro cold-open if the intro
        // player isn't registered (a build without the dialogue assets).
        private void OnPlayIntro()
        {
            if (!_splashActive) return;
            _splashActive = false;

            // "Play Intro" opts INTO the full tutorial experience — the cinematic
            // here, then the full FTUE companion meeting in the village.
            DeNelle.Core.OnboardingMode.ChooseFullTutorial();

            // WO-559: the intro ends at the HeroSelect carousel, which SELF-SKIPS to
            // the castle when a hero is already persisted. Play Intro is a fresh
            // playthrough, so clear the persisted hero HERE so the carousel builds.
            // Does NOT touch the Continue path.
            var svc = GameStateService.Instance;
            if (svc != null && svc.State != null)
            {
                svc.State.HeroClass = HeroClassOpt.None;
                svc.Save();
                FlowTrace.Step("Onboarding", "OnPlayIntro: cleared persisted HeroClass (None) so the post-intro carousel builds.");
            }

            if (DeNelle.Core.IntroLauncher.Play != null)
            {
                // The cinematic owns the screen until it routes onward; hide the
                // title menu under it and suppress the watchdog.
                _introPlaying = true;
                SetTitleVisible(false);
                DeNelle.Core.IntroLauncher.Play.Invoke();
            }
            else
            {
                RunArrival().Forget();
            }
        }

        // Continue: resume into the Castle home hub (loads the save).
        private void OnContinue()
        {
            if (!_splashActive) return;
            _splashActive = false;
            // START-FLOW GUARANTEE: Continue routes straight to the castle (no
            // hero-select), so if a loaded/stale save has no HeroClass persisted the
            // body builder would reach build with an unset class. Set it HERE at the
            // load source before routing. V1 is single-hero (Knight); ChooseHero also
            // applies the KnightOnly force.
            var svc = GameStateService.Instance;
            if (svc != null && (svc.State == null || !svc.State.HeroClass.ToNullable().HasValue))
            {
                FlowTrace.Warn("Onboarding",
                    "OnContinue: loaded save had no HeroClass — persisting Knight (V1) at the load source before GoCastle.");
                svc.ChooseHero(HeroClass.Knight);
            }
            SceneRouter.GoCastle();
        }

        // =====================================================================
        //  Cold-open fallback (Play Intro without a registered IntroLauncher)
        // =====================================================================

        /// <summary>
        /// Plays the StoryIntro cold-open then returns to the title menu. Each stage
        /// is time-boxed (SafeStage) AND covered by the DEF-253 Update watchdog so a
        /// stalled WebGL await can never strand the player on a dead screen.
        /// </summary>
        private async UniTask RunArrival()
        {
            FlowTrace.Step("Onboarding", "Arrival (cold-open fallback): start.");
            _arrivalRunning = true;
            _arrivalStart = Time.unscaledTime;
            SetTitleVisible(false);

            // The cold open is a multi-beat cinematic: SafeStage passes ForceHide as
            // the on-timeout KILL so a timed-out cinematic is genuinely CANCELLED
            // (CTS cancelled, overlay torn down), not merely abandoned to render on.
            if (_storyIntro != null)
                await SafeStage(_storyIntro.Play(), "storyIntro", () => _storyIntro.ForceHide());
            // Belt-and-braces: ForceHide is idempotent — ensure the overlay is down
            // even on the success path.
            if (_storyIntro != null) _storyIntro.ForceHide();

            if (!_arrivalRunning) return;   // watchdog already returned us to the menu
            _arrivalRunning = false;
            SetTitleVisible(true);
            _splashActive = true;
            FlowTrace.Step("Onboarding", "Arrival: cold-open done — title menu restored.");
        }

        /// <summary>
        /// Awaits an arrival stage but never lets it hang the boot: on timeout or
        /// exception it invokes <paramref name="onTimeout"/> (the authoritative KILL
        /// — pass <c>StoryIntroController.ForceHide</c>) and returns, so the title
        /// menu is always reachable. UNSCALED timeout (DEF-253): a scaled .Timeout
        /// never elapses if anything set Time.timeScale=0.
        /// </summary>
        private static async UniTask SafeStage(UniTask stage, string name, System.Action onTimeout = null)
        {
            try
            {
                await stage.Timeout(System.TimeSpan.FromSeconds(6f),
                                    Cysharp.Threading.Tasks.DelayType.UnscaledDeltaTime);
            }
            catch (System.Exception e)
            {
                try { onTimeout?.Invoke(); }
                catch (System.Exception killEx)
                {
                    FlowTrace.Warn("Onboarding", $"Arrival stage '{name}' kill threw: {killEx.Message}");
                }
                FlowTrace.Warn("Onboarding",
                    $"Arrival stage '{name}' skipped (timeout/exception) — cancelled + returning to the title. {e.Message}");
            }
        }

        private void Update()
        {
            // Orientation flip — swap the landscape/portrait cover art.
            if (_backdropArt != null && _canvas != null && _canvas.activeSelf)
                ApplyBackdropArt();

            // While the 9-screen cinematic plays it OWNS the screen and routes onward
            // itself — never force the title up over it.
            if (_introPlaying) return;

            // DEF-253 BLOCKER watchdog: if the cold-open fallback stalled past every
            // SafeStage timeout, force the title menu back (never-stuck fallback).
            if (_arrivalRunning && Time.unscaledTime - _arrivalStart > MaxIntroSeconds)
            {
                FlowTrace.Warn("Onboarding",
                    "DEF-253 watchdog tripped — force-returning to the title menu (never-stuck fallback).");
                _arrivalRunning = false;
                if (_storyIntro != null) _storyIntro.ForceHide();
                SetTitleVisible(true);
                _splashActive = true;
            }
        }

        // =====================================================================
        //  Small helpers
        // =====================================================================

        private void SetTitleVisible(bool visible)
        {
            if (_canvas != null) _canvas.SetActive(visible);
        }

        /// <summary>Keeps the built canvas in this controller's scene so scene unload
        /// tears it down with the Title scene (BuildModalCanvas creates at root).</summary>
        private void SceneRootAdopt(GameObject go)
        {
            if (go != null && go.scene != gameObject.scene)
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, gameObject.scene);
        }

        /// <summary>Full-rect stretch for a fresh uGUI element.</summary>
        private static void Stretch(GameObject go)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
