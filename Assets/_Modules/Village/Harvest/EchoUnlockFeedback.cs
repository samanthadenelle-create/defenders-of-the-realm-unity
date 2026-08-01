// =============================================================================
// EchoUnlockFeedback -- unmissable in-view feedback when a new Echo is granted.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// PROBLEM (owner F8 2026-07-15): clearing every 5 waves DOES grant a new Echo
// (EchoService.OnWaveCleared -> EchoUnlocked fires, count 1->2, a 2nd wisp spawns
// and persists). But the ONLY "New Echo joined!" feedback was a label INSIDE the
// Echo Harvest panel, which is HIDDEN by default (EchoWorkforceHud, opened by the
// harvest button). During Overworld defense the owner saw NOTHING -- no banner, no
// SFX, no visible counter -- and concluded the unlock never happened.
//
// FIX (this file): a feedback surface INDEPENDENT of the hidden harvest panel,
// subscribing to EchoService.EchoUnlocked (carries the new count):
//   1. a persistent, compact "Echoes N/M" PIP (top-left), visible during defense,
//      that updates on every EchoService.Changed / EchoUnlocked;
//   2. an unmissable CENTER banner ("A new Echo has joined Elarion!  (N/M)") on its
//      own overlay canvas -- colorblind-safe (text + a scale pop-in, not hue), and
//   3. a positive unlock SFX (GameSfx.PlayLevelUp -> the rising-chime reward burst,
//      routed through CoreServices.Audio, null-guarded).
//
// REUSE, not greenfield: the banner + pip are built from the ONE shared obsidian
// ElarionUiKit.ToastCard (DeNelle.Core.UI) -- the same surface GearGrantToast uses --
// and the SFX is the existing GameSfx wrapper. No new toast/banner framework.
//
// Lives on the EchoService DDOL host (installed by EchoWorkforceBootstrap next to
// EchoWorkforceHud). Sec.5: DeNelle.Village references Core only; it resolves state
// through EchoService (same assembly) + Core.UI/Core services -- no Village<->HUD ref.
// =============================================================================
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using TMPro;
using DeNelle.Core.UI;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.State;

namespace DeNelle.Village
{
    /// <summary>Persistent "Echoes N/M" pip + unmissable center banner + reward SFX on
    /// <see cref="EchoService.EchoUnlocked"/>. Independent of the hidden harvest panel.</summary>
    [DisallowMultipleComponent]
    public sealed class EchoUnlockFeedback : MonoBehaviour
    {
        private Text _pipLabel;
        private GameObject _pipCanvas;

        // Founding-echo teaching queue (WO: wire founding-echo teaching): TRUE while the
        // founding card is DUE but deferred behind Build Mode or a menu scene. Re-evaluated
        // on the builder-close EDGE (Update) + on scene change until it can show cleanly.
        private bool _foundingPending;
        private bool _buildWasActive;   // prior-frame Build Mode state -> detect the close edge

        private void Start()
        {
            BuildPip();
            BuildPetBoxButton();     // owner 2026-07-17: "add the pet box somewhere" -> opens the Echo roster
            RefreshPip();
            var svc = EchoService.Instance;
            if (svc != null)
            {
                svc.Changed += RefreshPip;
                svc.EchoUnlocked += OnEchoUnlocked;
            }

            // HUD-LEAK GATE (owner F8 2026-07-18): this host is a DontDestroyOnLoad object
            // spawned by EchoWorkforceBootstrap on cold launch (first scene = Title), so the
            // pip + Pets button would paint over the TITLE / HeroSelect / PetSelect menus and
            // persist. EchoService stays global + ticking; only the VISUAL is scene-gated to
            // gameplay scenes. Set initial visibility from the active scene, then follow scene
            // changes.
            ApplySceneVisibility(SceneManager.GetActiveScene().name);
            SceneManager.activeSceneChanged += OnActiveSceneChanged;

            // Founding-echo teaching (WO): the wave path only raises EchoUnlocked at count>=2, so
            // the FOUNDING spirit (granted via the pet path, EchoCount==1) never got the portrait
            // card and its fragile FTUE line got watchdog-swallowed. Fire the SAME card here,
            // tutorial-INDEPENDENT (this host is installed unconditionally by EchoWorkforceBootstrap).
            EvaluateFoundingTeach();
            FlowTrace.Step("Echo", "EchoUnlockFeedback built (persistent pip + Pets pet-box button + unlock dialogue armed; scene-gated; founding teaching armed)");
        }

        private void OnDestroy()
        {
            var svc = EchoService.Instance;
            if (svc != null)
            {
                svc.Changed -= RefreshPip;
                svc.EchoUnlocked -= OnEchoUnlocked;
            }
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        }

        private void Update()
        {
            // Founding-echo teaching queue. Fire on the builder-close EDGE only (not every frame)
            // so a deferred card shows on the FIRST frame the builder closes, without hammering
            // AnnounceFoundingEcho (and its SFX) if a render ever faults. Scene-entry attempts are
            // covered by OnActiveSceneChanged; the initial in-gameplay attempt by Start.
            bool buildActive = DeNelle.Core.BuildModeState.IsActive;
            if (_foundingPending && _buildWasActive && !buildActive) EvaluateFoundingTeach();
            _buildWasActive = buildActive;

            // OWNER RCA 2026-08-01: the new holds (Onboarded / no-other-modal) have no edge
            // event to ride, so while the teach is pending re-evaluate at 1 Hz — the card
            // fires on the first quiet second after the tutorial closes.
            if (_foundingPending && Time.unscaledTime >= _nextFoundingPollAt)
            {
                _nextFoundingPollAt = Time.unscaledTime + 1f;
                EvaluateFoundingTeach();
            }
        }

        private float _nextFoundingPollAt;

        // REVIEW 2026-08-01 softlock cap: if the tutorial never completes (Onboarded never
        // flips) or a modal never closes, the soft holds would defer the founding card
        // FOREVER. Track when the pending card first became eligible-except-for-holds
        // (EchoCount >= 1, not yet taught); per WO-823 Phase B: after 120s of waiting --
        // or just 30s when the tutorial IS complete and only a sticky modal blocks --
        // bypass ONLY the Onboarded / AnyOpen holds and show it anyway. The HARD holds
        // (gameplay scene + not Build Mode) always apply -- the card must never paint
        // over a menu scene or the builder. -1 = clock not running.
        private const float FoundingHoldCapSeconds = 120f;
        private const float FoundingModalOnlyCapSeconds = 30f;   // Onboarded, AnyOpen is the sole soft hold
        private float _foundingHeldSince = -1f;

        // -- HUD-leak scene gate (visibility only; service stays global) -----------
        private void OnActiveSceneChanged(Scene from, Scene to)
        {
            ApplySceneVisibility(to.name);
            // Entering a gameplay scene may make the founding teaching showable.
            EvaluateFoundingTeach();
        }

        // -- founding-echo teaching (the SAME card as echoes #2-6, decoupled from FTUE) -----
        /// <summary>Fire the founding-echo teaching card once, on the SAME portrait-card path as
        /// echoes #2-6. Defers behind Build Mode and the menu scenes; the persisted one-shot flag
        /// (set by <see cref="EchoService.AnnounceFoundingEcho"/> only AFTER the card renders) makes
        /// it idempotent, so an app-quit mid-build never consumes the teaching.</summary>
        private void EvaluateFoundingTeach()
        {
            var svc = EchoService.Instance;
            if (svc == null) return;

            // Already taught this save -> done for good; stop the pending retry loop.
            if (FoundingTaught()) { _foundingPending = false; _foundingHeldSince = -1f; return; }

            // No founding echo yet (defensive; EchoCount is >=1 via the property once a save exists).
            if (svc.EchoCount < 1) { _foundingPending = false; _foundingHeldSince = -1f; return; }

            // Hold until we can show cleanly: not over a menu scene, not behind the builder.
            // OWNER RCA 2026-08-01 (Start New "flashes to the pet screen ... a second later
            // moves along"): the card fired on the FIRST FRAME of castle entry — mid-fade,
            // under the boot storm and the tutorial coach — visible ~1s, buried, and the
            // one-shot burned. Two more holds: (a) the TUTORIAL must be complete (Onboarded —
            // canon: onboarding teaches the claim-loop first, the founding tale lands on a
            // quiet screen after); (b) no OTHER modal may be open (the card would force-close
            // it or vice versa). The pending retry loop below re-evaluates until clear.
            bool onboarded = DeNelle.Core.State.GameStateService.Instance != null
                && DeNelle.Core.State.GameStateService.Instance.State != null
                && DeNelle.Core.State.GameStateService.Instance.State.Onboarded;
            bool gameplayScene = IsGameplayScene(SceneManager.GetActiveScene().name);
            bool buildActive = DeNelle.Core.BuildModeState.IsActive;
            bool anyOpen = DeNelle.Core.UI.PanelManager.AnyOpen;
            if (!gameplayScene || buildActive || !onboarded || anyOpen)
            {
                // Start the softlock-cap clock the first time the card is held (EchoCount>=1
                // and untaught are already proven above).
                if (_foundingHeldSince < 0f) _foundingHeldSince = Time.unscaledTime;

                // Cap expired + the HARD holds are clear -> bypass the SOFT holds
                // (Onboarded / AnyOpen) and force the show below. A tutorial softlock must
                // not eat the founding tale forever. Still hard-held (menu scene / builder
                // open) -> keep waiting regardless of the clock.
                float held = Time.unscaledTime - _foundingHeldSince;
                // WO-823 Phase B: 120s general cap; fast 30s cap when the tutorial is done
                // and a sticky modal is the ONLY thing holding the card.
                float cap = (onboarded && anyOpen) ? FoundingModalOnlyCapSeconds : FoundingHoldCapSeconds;
                bool capExpired = held > cap;
                if (!capExpired || !gameplayScene || buildActive)
                {
                    _foundingPending = true;   // Update() + OnActiveSceneChanged re-evaluate until clear
                    return;
                }
                FlowTrace.Fail("Echo", "founding card held " + (int)held + "s > cap " + (int)cap
                    + "s (Onboarded=" + onboarded + ", AnyOpen=" + anyOpen + ") - forcing show");
            }

            // Clear to teach: AnnounceFoundingEcho raises EchoUnlocked(1) -> OnEchoUnlocked renders
            // the same card as echoes #2-6, and persists the one-shot flag ONLY on a confirmed render.
            svc.AnnounceFoundingEcho();
            _foundingPending = !FoundingTaught();   // if the card failed to render, keep retrying
            if (!_foundingPending) _foundingHeldSince = -1f;   // taught -> stop the cap clock
        }

        /// <summary>Read the persisted founding-taught one-shot flag (the idempotency gate).</summary>
        private static bool FoundingTaught()
        {
            var s = GameStateService.Instance != null ? GameStateService.Instance.State : null;
            return s != null && s.SeenTutorials.TryGetValue(EchoService.FoundingTaughtKey, out bool seen) && seen;
        }

        /// <summary>Show the pip + Pets button only in gameplay scenes; hide them on the
        /// menu / front-door scenes so gameplay HUD chrome never leaks onto the title.</summary>
        private void ApplySceneVisibility(string sceneName)
        {
            bool show = IsGameplayScene(sceneName);
            if (_pipCanvas != null && _pipCanvas.activeSelf != show)
                _pipCanvas.SetActive(show);
            FlowTrace.Step("Echo", $"EchoUnlockFeedback pip/Pets visibility={(show ? "SHOWN" : "hidden")} for scene '{sceneName ?? "<null>"}'");
        }

        /// <summary>FALSE for the menu / non-HUD scenes (Title, HeroSelect, PetSelect,
        /// ATBBattle), TRUE for everything else. A small deny-list of menu scenes (not an
        /// allow-list) so new gameplay scenes -- hub/village/raid/dungeon -- default to SHOWING
        /// the pip. Matches SceneRouter constants: Title="Title", HeroSelect="HeroSelect",
        /// PetSelect="PetSelect", ATBBattle="ATBBattle".</summary>
        private static bool IsGameplayScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return false;
            switch (sceneName)
            {
                case "Title":
                case "HeroSelect":
                case "PetSelect":
                case "ATBBattle":
                    return false;
                default:
                    return true;
            }
        }

        // -- the unlock moment (event -> portrait dialogue + SFX + pip) ------------
        private void OnEchoUnlocked(int newCount)
        {
            // Guard so a missing font/clip logs + skips -- never blocks the unlock itself.
            Guard.Try("Echo", "unlock-feedback", () =>
            {
                // The full "Echoes of Elarion" portrait card, data-driven by the unlocked
                // spirit (REPLACES the old plain center banner -- no double banner). The SFX
                // + persistent pip stay.
                EchoUnlockDialogue.Show(EchoRosterCatalog.ByCount(newCount), newCount);
                GameSfx.PlayLevelUp();   // rising-chime reward burst via CoreServices.Audio (guarded)
                RefreshPip();
                FlowTrace.Step("Echo", $"unlock feedback shown count={newCount}");
            });
        }

        // -- persistent pip (logic -> view) ---------------------------------------
        private void RefreshPip()
        {
            var svc = EchoService.Instance;
            if (svc == null || _pipLabel == null) return;
            _pipLabel.text = $"Echoes  {svc.EchoCount}/{svc.MaxEchoes}";
        }

        private void BuildPip()
        {
            var go = new GameObject("EchoCountPip");
            go.transform.SetParent(transform, false);
            _pipCanvas = go;

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 700;   // above gameplay HUD, below the toasts (720) + panels

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            // Shared obsidian toast card (Info tone) -- never raycast-blocks. Compact,
            // top-left, below the resource row so the count reads during defense.
            var parts = ElarionUiKit.ToastCard(go.transform, ElarionUiKit.ToastTone.Info,
                                               accentLeft: true, align: TextAnchor.MiddleCenter);
            _pipLabel = parts.label;
            var crt = (RectTransform)parts.card.transform;
            crt.anchorMin = new Vector2(0f, 1f);
            crt.anchorMax = new Vector2(0f, 1f);
            crt.pivot = new Vector2(0f, 1f);
            crt.anchoredPosition = new Vector2(20f, -150f);
            crt.sizeDelta = new Vector2(230f, 56f);
            if (_pipLabel != null) { _pipLabel.fontSize = 22; _pipLabel.text = "Echoes  1/6"; }
            else
                FlowTrace.Warn("Echo", "EchoCountPip: ToastCard returned a null label -- the count will not render.");
        }

        // -- Pet-box entry point (owner 2026-07-17: "add the pet box somewhere") ---
        // A small persistent "Echoes" HUD button under the count pip that opens the Echo
        // roster grid. Colorblind-safe (a paw glyph + the word "Echoes", never hue). Lives
        // on the pip's own overlay canvas so it needs no HUD-kit area wiring (the town
        // HUD button row was pruned) -- lightweight + self-contained.
        private void BuildPetBoxButton()
        {
            if (_pipCanvas == null) return;

            // The pip canvas is display-only (ToastCard is non-interactive); a button on it
            // needs a raycaster + an EventSystem in the scene.
            if (_pipCanvas.GetComponent<GraphicRaycaster>() == null)
                _pipCanvas.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();

            // Presentation-separation law (MVVM): the button is a DUMB, kit-styled view.
            // It comes from the presentation kit's Obsidian button factory -- frame, face,
            // label ink, font, and hover feedback all live in the kit -- and the ONLY thing
            // this call site injects is the onClick Action (EchoRoster.Open). No hand-rolled
            // GameObject/Image/Button assembly, no per-caller styling. Style1/Gray = the
            // quiet obsidian face standardized across the HUD (matches ObsidianCloseButton).
            var btn = ElarionUiKit.BuildObsidianButton(
                _pipCanvas.transform, "Echoes",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Gray,
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                onClick: () =>
                {
                    FlowTrace.Step("Echo", "Pets pet-box button tapped -> open Echo roster.");
                    EchoRoster.Open();
                });
            if (btn == null) return;

            // Owner 2026-07-24 felt-test placement, preserved: RIGHT screen edge, vertically
            // centred (the LEFT edge is the HudKit gear slide-dock, so RIGHT is the free edge).
            // A square touch target that meets the mobile MinTouchPx ~112 standard. The roster it
            // opens stays the full-screen 31000 single-arbiter modal (z-fix preserved). The kit
            // anchored the button at (1,0.5) with zero offsets; collapse that to a fixed-size box
            // pinned to the right edge (pivot 1,0.5 + inset), then clamp to the touch floor.
            var rt = btn.transform as RectTransform;
            if (rt != null)
            {
                rt.anchorMin = new Vector2(1f, 0.5f);
                rt.anchorMax = new Vector2(1f, 0.5f);
                rt.pivot = new Vector2(1f, 0.5f);
                // SAFE-AREA INSET (measured off the headless capture 2026-07-30): the old raw
                // -16f resolved to only ~18 device px at 2340x1080 (~7 dp, ~1.15mm on the Seeker)
                // -- reads as flush, and sits inside the rounded-corner / landscape-cutout /
                // gesture band. 3 x PadPanel = 54 ref px ~= 24 dp (~60 device px), 1.5x the
                // Material 16 dp screen margin, using the dp scale in
                // docs/SME/VISUAL_TOUCH_CONTRAST_AUDIT_2026-07-14.md (1 dp ~= 2.21 ref px on
                // this 1080x1920 / match-0.5 canvas). Authored as a deliberate multiple of
                // PadPanel, never a raw literal (WO-779 spacing rule).
                // TODO(WO-779 s5.6): replace with the shared Screen.safeArea helper once it exists.
                rt.anchoredPosition = new Vector2(-(ElarionUi.PadPanel * 3f), 0f);  // 54 ref px right-edge inset
                rt.sizeDelta = new Vector2(120f, 120f);        // >= MinTouchPx (112) -- comfortable tap target
            }
            ElarionUiKit.ClampMinTouch(btn);                   // kit touch floor guard (never shrinks)
        }

        private static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
                DontDestroyOnLoad(es);
            }
        }
    }
}
