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
        }

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
            if (FoundingTaught()) { _foundingPending = false; return; }

            // No founding echo yet (defensive; EchoCount is >=1 via the property once a save exists).
            if (svc.EchoCount < 1) { _foundingPending = false; return; }

            // Hold until we can show cleanly: not over a menu scene, not behind the builder.
            if (!IsGameplayScene(SceneManager.GetActiveScene().name) || DeNelle.Core.BuildModeState.IsActive)
            {
                _foundingPending = true;   // Update() + OnActiveSceneChanged re-evaluate until clear
                return;
            }

            // Clear to teach: AnnounceFoundingEcho raises EchoUnlocked(1) -> OnEchoUnlocked renders
            // the same card as echoes #2-6, and persists the one-shot flag ONLY on a confirmed render.
            svc.AnnounceFoundingEcho();
            _foundingPending = !FoundingTaught();   // if the card failed to render, keep retrying
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
        // A small persistent "Pets" HUD button under the count pip that opens the Echo
        // roster grid. Colorblind-safe (a paw glyph + the word "Pets", never hue). Lives
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

            var go = new GameObject("EchoPetBoxButton", typeof(Image), typeof(Button));
            go.transform.SetParent(_pipCanvas.transform, false);
            var rt = go.GetComponent<RectTransform>();
            // Owner 2026-07-19: the pet-box access belongs BELOW the Tree of Life (lower-centre, at the
            // tree's roots where the Echo-spirits gather) — NOT stranded top-left. Bottom-centre anchor,
            // lifted above the bottom verb bar (Build/Talk/Bag/Quests). Y is felt-tunable on device.
            // The roster it opens stays the full-screen 31000 single-arbiter modal (z-fix preserved).
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 200f);   // base of the Tree of Life, clear of the verb bar
            rt.sizeDelta = new Vector2(230f, 56f);

            var img = go.GetComponent<Image>();
            img.color = new Color(0.06f, 0.06f, 0.08f, 0.92f);   // obsidian glass, matches HUD chrome
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() =>
            {
                FlowTrace.Step("Echo", "Pets pet-box button tapped -> open Echo roster.");
                EchoRoster.Open();
            });

            // TMP label via the kit (font-safe) -- "[Echoes] Pets": icon-word + text,
            // colorblind-safe (never hue alone).
            var txt = ElarionUiKit.Label(go.transform, "Pets", 0f, 1f,
                ElarionUi.Parchment, 24, TextAlignmentOptions.Center, 0f, 1f, bold: true);
            txt.raycastTarget = false;
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
