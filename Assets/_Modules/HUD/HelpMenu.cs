// =============================================================================
// HelpMenu — the Settings/Help modal reachable from the HUD gear button.
// Surfaces: Report Bug (WO-596 BugReportView), Controls, Reset Hero & Pet,
// Dev tools (dev builds only), Credits.
// -----------------------------------------------------------------------------
// WO-F conversion (2026-07-03, coverage matrix row #44): UIDocument/UITK panel
// -> code-built uGUI on the Obsidian master frame (BuildObsidianModal: Blink
// FrameCore + medallion + the ONE shared Close + tap-outside scrim). The old
// UITK card (legacy LegacyRuntime.ttf text, own runtime PanelSettings, borrowed
// theme) is retired — this file is the REFERENCE conversion for the rest of the
// UIDocument family. Spawned by HelpMenuBootstrap (RuntimeInitializeOnLoad).
//
// AdminOverlay handoff kept: "Dev tools" lends AdminOverlay a runtime
// PanelSettings (AdminOverlay is still UITK); we synthesize one on demand now
// that this menu no longer renders through a UIDocument itself.
// =============================================================================

using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using DeNelle.Core.UI;
using DeNelle.Core.Diagnostics;

namespace DeNelle.HUD
{
    [DisallowMultipleComponent]
    public sealed class HelpMenu : MonoBehaviour
    {
        public static HelpMenu Instance { get; private set; }

        private ElarionUiKit.ObsidianModal _modal;
        private ElarionUiKit.ToastParts _toast;
        private float _toastUntil;

        // DEF-212 modal arbiter handle. The Help menu is a full-screen modal, so it
        // MUST route through PanelManager like every other panel — otherwise it stacks
        // over open content and its scrim can trap the player.
        private PanelHandle _panelHandle;

        private void Awake()
        {
            Instance = this;
            _panelHandle = PanelManager.Register("Help", Close, () => IsOpen);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_modal != null && _modal.canvas != null) Destroy(_modal.canvas);
        }

        /// <summary>True while the Help modal is visible.</summary>
        public bool IsOpen => _modal != null && _modal.canvas != null && _modal.canvas.activeSelf;

        // ── AdminOverlay handoff (T-030) ─────────────────────────────────────────
        // AdminOverlay is still UITK and needs a PanelSettings to render. This menu
        // no longer owns a UIDocument, so we synthesize a runtime PanelSettings on
        // demand (own unique name — OnboardingPanelGuard matches by name and must
        // never tear this down; theme borrowed from any live doc so fonts inherit).
        private PanelSettings _adminPanelSettings;

        // ── Hidden dev unlock (owner ask 2026-07-12) ─────────────────────────────
        // Mobile has no Ctrl+Shift+A chord and release builds compile-strip the Dev
        // Tools launcher (LB-11), so on a phone there was NO way to dev-grant
        // resources. 5 taps on this card's TITLE within a 3s window flips a
        // persisted unlock (PlayerPrefs) that reveals a minimal "Grant Resources"
        // action — the grant ONLY, not the full AdminOverlay, so the LB-11 release
        // lock on the admin panel itself stays intact.
        // SECURITY (store-hardening Path A, S1): the 5-tap dev resource-grant is compile-STRIPPED from
        // release (non-Development) builds so a public/store APK cannot self-grant unlimited resources.
        // Preserved in Editor/Development builds so the owner keeps the on-phone dev grant while developing.
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private const string DevUnlockPref = "dotr.devunlock";
        private int _titleTaps;
        private float _lastTitleTapTime;
        private UnityEngine.UI.Button _grantResourcesBtn;   // uGUI (UIElements.Button also in scope)

        private static bool DevUnlocked => PlayerPrefs.GetInt(DevUnlockPref, 0) == 1;
#endif
        public PanelSettings ActivePanelSettings
        {
            get
            {
                if (_adminPanelSettings != null) return _adminPanelSettings;
                _adminPanelSettings = ScriptableObject.CreateInstance<PanelSettings>();
                _adminPanelSettings.name = "HelpRuntimePanelSettings";
                _adminPanelSettings.sortingOrder = 2700;
                foreach (var existing in UnityEngine.Object.FindObjectsByType<UIDocument>(
                             FindObjectsInactive.Include))
                {
                    if (existing == null || existing.panelSettings == null) continue;
                    if (existing.panelSettings.themeStyleSheet != null)
                    {
                        _adminPanelSettings.themeStyleSheet = existing.panelSettings.themeStyleSheet;
                        break;
                    }
                }
                return _adminPanelSettings;
            }
        }

        private void Update()
        {
            if (_toast != null && _toast.card != null && _toastUntil > 0f
                && Time.unscaledTime > _toastUntil)
            {
                _toast.card.SetActive(false);
                _toastUntil = 0f;
            }
        }

        // ── UI construction (lazy — first open builds) ───────────────────────────
        private void EnsureBuilt()
        {
            if (_modal != null && _modal.canvas != null) return;

            // Taller modal so the action rows fit without overlap (owner 2026-07-16 "layers stacked").
            _modal = ElarionUiKit.BuildObsidianModal("HelpMenuUI", "Help",
                new Vector2(0.26f, 0.12f), new Vector2(0.74f, 0.88f), Close,
                frameName: RpgUiCatalog.FrameCore, medallionIcon: "settings");

            bool bodyIsZone = _modal.chrome.layout != null && _modal.chrome.layout.body != null;
            var body = bodyIsZone
                ? _modal.chrome.layout.body.transform
                : _modal.chrome.content.transform;

            // -- WO-795: scrollable button well (RumorBoardPanel Viewport/Content pattern) --
            // The plain BuildButtonColumn VLG does not clip: with 4 release rows (6 in dev
            // builds) at the 112px touch floor / 132px preferred height the tail overflowed
            // the body rect and collided with the kit's bottom-center shared Close band
            // (DefaultCloseZone, fixed 360x132 box growing up from panel y=0.050). Wrap the
            // column in a masked vertical ScrollRect sized to end ABOVE that band:
            //  - Zone_Body path: the kit factory already raises the zone's bottom edge above
            //    the Close box (close-band reservation), so the well fills the zone using the
            //    column's old insets (0.06 side, 0.04 top/bottom) - same visual rect as before.
            //  - chrome.content fallback: no reservation exists there, so anchor the well's
            //    bottom at 0.24 of the panel - clear of the worst-case (landscape) top of the
            //    fixed Close box (~0.22) plus a gap.
            var wellGo = new GameObject("ButtonScrollWell",
                typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.RectMask2D),
                typeof(UnityEngine.UI.ScrollRect));
            wellGo.transform.SetParent(body, false);
            var wellRt = wellGo.GetComponent<RectTransform>();
            wellRt.anchorMin = bodyIsZone ? new Vector2(0.06f, 0.04f) : new Vector2(0.06f, 0.24f);
            wellRt.anchorMax = bodyIsZone ? new Vector2(0.94f, 0.96f) : new Vector2(0.94f, 0.875f);
            wellRt.offsetMin = Vector2.zero;
            wellRt.offsetMax = Vector2.zero;
            wellGo.GetComponent<UnityEngine.UI.Image>().color = new Color(0f, 0f, 0f, 0.001f); // drag catcher

            // Common spaced button column (ElarionUiKit) — guaranteed spacing + no overlap at any
            // screen size (owner "fix in common"). Close is the chrome's ONE shared Close.
            // The column now doubles as the ScrollRect CONTENT: re-anchor it top-stretched
            // (pivot 0.5,1) inside the well - its insets moved onto the well above - and let a
            // ContentSizeFitter grow it to the rows' preferred height so overflow scrolls
            // instead of spilling into the Close band. Rows/behaviors unchanged.
            var stack = ElarionUiKit.BuildButtonColumn(wellGo.transform);
            stack.anchorMin = new Vector2(0f, 1f);
            stack.anchorMax = new Vector2(1f, 1f);
            stack.pivot     = new Vector2(0.5f, 1f);
            stack.offsetMin = Vector2.zero;
            stack.offsetMax = Vector2.zero;
            var stackFit = stack.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
            stackFit.verticalFit   = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            stackFit.horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.Unconstrained;

            var wellScroll = wellGo.GetComponent<UnityEngine.UI.ScrollRect>();
            wellScroll.viewport = wellRt;
            wellScroll.content  = stack;
            wellScroll.horizontal = false;
            wellScroll.vertical   = true;
            wellScroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            wellScroll.scrollSensitivity = 25f;
            FlowTrace.Step("UI", "HelpMenu: button column wrapped in ScrollRect well (WO-795) bodyIsZone=" + bodyIsZone);
            ElarionUiKit.AddColumnButton(stack, "Report a Bug",
                ElarionUiKit.ObsidianButtonColor.Gray, OnReportBug);
            ElarionUiKit.AddColumnButton(stack, "Controls",
                ElarionUiKit.ObsidianButtonColor.Gray, OnShowControls);
            ElarionUiKit.AddColumnButton(stack, "Reset Hero & Pet",
                ElarionUiKit.ObsidianButtonColor.Red, OnResetProgress);
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            // SECURITY (LB-11 / E-DEVTOOLS): "Dev tools" opens AdminOverlay — compile-stripped from
            // release. Force dark Ink on the label (luminance law) wherever the build put it.
            var devBtn = ElarionUiKit.AddColumnButton(stack, "Dev Tools",
                ElarionUiKit.ObsidianButtonColor.Yellow, OnOpenDevTools);
            if (devBtn != null)
            {
                var devLbls = devBtn.GetComponentsInChildren<TMPro.TMP_Text>(true);
                if ((devLbls == null || devLbls.Length == 0) && devBtn.transform.parent != null)
                    devLbls = devBtn.transform.parent.GetComponentsInChildren<TMPro.TMP_Text>(true);
                if (devLbls != null)
                    foreach (var t in devLbls) t.color = ElarionUi.Ink;
            }
            FlowTrace.Step("UI", "Dev tools button wired (HelpMenu Obsidian card)");
#endif
            ElarionUiKit.AddColumnButton(stack, "Credits",
                ElarionUiKit.ObsidianButtonColor.Gray, OnShowCredits);

            // Hidden dev unlock (owner 2026-07-12): "Grant Resources" + the 5-tap title unlock exist in
            // Editor/Development builds only — SECURITY (store-hardening Path A, S1): compile-STRIPPED from
            // release so a public/store APK cannot self-grant unlimited resources (grant grants ONLY
            // resources; AdminOverlay itself was already release-locked at LB-11).
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            _grantResourcesBtn = ElarionUiKit.AddColumnButton(stack, "Grant Resources (dev)",
                ElarionUiKit.ObsidianButtonColor.Yellow, OnGrantResources);
            if (_grantResourcesBtn != null)
                _grantResourcesBtn.gameObject.SetActive(DevUnlocked);

            // 5-tap counter on the card TITLE (a TMP Graphic — it carries the Button
            // directly; no extra widget). Window resets after 3s of no taps.
            if (_modal.chrome != null && _modal.chrome.title != null)
            {
                _modal.chrome.title.raycastTarget = true;
                var titleBtn = _modal.chrome.title.gameObject.GetComponent<UnityEngine.UI.Button>();
                if (titleBtn == null) titleBtn = _modal.chrome.title.gameObject.AddComponent<UnityEngine.UI.Button>();
                titleBtn.transition = UnityEngine.UI.Selectable.Transition.None;
                titleBtn.targetGraphic = _modal.chrome.title;
                titleBtn.onClick.AddListener(OnTitleTapped);
            }
#endif

            // Toast (status messages) — kit ToastCard, low-center, fades after 5s.
            // (dev-unlock handlers live below with the other On* handlers)
            _toast = ElarionUiKit.ToastCard(_modal.canvas.transform,
                ElarionUiKit.ToastTone.Info, accentLeft: true, TextAnchor.MiddleCenter);
            var trt = _toast.card.GetComponent<RectTransform>();
            trt.anchorMin = new Vector2(0.14f, 0.045f);
            trt.anchorMax = new Vector2(0.86f, 0.115f);
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            _toast.card.SetActive(false);

            _modal.canvas.SetActive(false);   // built hidden; SetOpen shows it
        }

        // ── Actions ────────────────────────────────────────────────────────────
        public void ToggleOverlay()
        {
            FlowTrace.Step("UI", $"Settings open requested (gear -> ToggleOverlay; currently open={IsOpen})");
            SetOpen(!IsOpen);
        }

        /// <summary>Explicitly hide the Help modal (shared Close + modal-arbiter close).</summary>
        public void Close() => SetOpen(false);

        private void SetOpen(bool open)
        {
            if (open) EnsureBuilt();
            if (_modal == null || _modal.canvas == null) return;
            _modal.canvas.SetActive(open);
            // Route through the modal arbiter (DEF-212): opening closes any other open
            // panel; closing clears our slot. NotifyOpened/Closed are no-ops when state
            // is unchanged, so the handle's Close callback won't recurse.
            if (open) PanelManager.NotifyOpened(_panelHandle);
            else PanelManager.NotifyClosed(_panelHandle);
            FlowTrace.Step("UI", $"Settings {(open ? "shown" : "hidden")} — kit modal active={_modal.canvas.activeSelf} timeScale={Time.timeScale}");
        }

        /// <summary>WO-596 — route to the player bug-report form. Close FIRST so the
        /// form's clean-frame capture never includes this menu.</summary>
        private void OnReportBug()
        {
            FlowTrace.Step("BugReport", "Settings -> Report a bug — opening BugReportView");
            Close();
            BugReportView.Open();
        }

        private void OnShowControls()
        {
            ShowToast("Controls — WASD/Arrows/dpad: move • 1/2/3/4 + face buttons: cast Q/W/E/R • Build button: tower placement • F: interact • Esc: pause");
        }

        private void OnShowCredits()
        {
            ShowToast("Defenders of the Realm v2 — DeNelle Studios. Models: KayKit + Tripo. Audio: original soundtrack.");
        }

        // SECURITY (store-hardening Path A, S1): the 5-tap dev unlock + resource grant are stripped from
        // release builds (see the guarded call sites + fields above). Preserved in Editor/Development.
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        /// <summary>5-tap title counter (owner 2026-07-12): five taps inside a rolling 3s
        /// window flips the persisted dev unlock and reveals the Grant Resources row.</summary>
        private void OnTitleTapped()
        {
            if (Time.unscaledTime - _lastTitleTapTime > 3f) _titleTaps = 0;
            _lastTitleTapTime = Time.unscaledTime;
            _titleTaps++;
            if (_titleTaps < 5 || DevUnlocked) return;

            PlayerPrefs.SetInt(DevUnlockPref, 1);
            PlayerPrefs.Save();
            if (_grantResourcesBtn != null) _grantResourcesBtn.gameObject.SetActive(true);
            FlowTrace.Step("UI", "HelpMenu: dev unlock flipped ON (5-tap title) — Grant Resources revealed.");
            ShowToast("Dev actions unlocked.");
        }

        /// <summary>
        /// Grants the AdminOverlay full-resource bundle (wood/food/iron/crystals + coins)
        /// through EconomyService.GrantSpendable — which writes Wood/Iron into BOTH
        /// wallets (in-session pool + GameState) so shop AND upgrade flows can spend it.
        /// HUD can't reference DeNelle.Village, so reached by reflection — the exact
        /// AdminOverlay.OnLoadResources idiom (the documented HUD→Village seam).
        /// </summary>
        private void OnGrantResources()
        {
            var ecoType = Type.GetType("DeNelle.Village.EconomyService, DeNelle.Village");
            var instProp = ecoType?.GetProperty("Instance",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            var eco = instProp?.GetValue(null);
            if (eco == null) { ShowToast("Grant failed — economy not alive yet."); return; }

            var grant = ecoType.GetMethod("GrantSpendable",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance, null,
                new[] { typeof(int), typeof(int), typeof(int), typeof(int) }, null);
            if (grant == null) { ShowToast("Grant failed — GrantSpendable not found."); return; }
            grant.Invoke(eco, new object[] { 50000, 25000, 50000, 25000 }); // wood, food, iron, crystals

            var addCoins = ecoType.GetMethod("AddCoins",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance, null,
                new[] { typeof(int) }, null);
            if (addCoins != null) addCoins.Invoke(eco, new object[] { 50000 });

            FlowTrace.Step("UI", "HelpMenu: dev Grant Resources fired (50k wood/iron, 25k food/crystals, 50k coins).");
            ShowToast("Granted: 50k wood/iron, 25k food/crystals, 50k gold.");
        }
#endif // DEVELOPMENT_BUILD || UNITY_EDITOR — 5-tap dev resource grant (store-hardening S1)

        /// <summary>Resets save state via reflection so the player can redo hero + pet
        /// selection, then routes back to HeroSelect.</summary>
        private void OnResetProgress()
        {
            try
            {
                var t = System.Type.GetType("DeNelle.Core.State.GameStateService, DeNelle.Core");
                if (t == null) { ShowToast("Reset failed — GameStateService missing."); return; }
                var instance = t.GetProperty("Instance",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.GetValue(null);
                if (instance == null) { ShowToast("Reset failed — service not alive."); return; }
                var reset = t.GetMethod("ResetToNewGame",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                reset?.Invoke(instance, null);

                var router = System.Type.GetType("DeNelle.Core.SceneRouter, DeNelle.Core");
                var goHero = router?.GetMethod("GoHeroSelect",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (goHero != null)
                {
                    ShowToast("Reset — heading back to Hero Select…");
                    goHero.Invoke(null, null);
                }
                else
                {
                    ShowToast("Reset done — restart the game to redo selection.");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[HelpMenu] Reset failed: " + ex.Message);
                ShowToast("Reset failed — see log.");
            }
        }

        /// <summary>Opens the AdminOverlay (owner tools). SECURITY (LB-11):
        /// compile-stripped from release builds along with its launcher.</summary>
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private void OnOpenDevTools()
        {
            FlowTrace.Step("UI", "DevPanel toggle/click reached (HelpMenu 'Dev tools' -> AdminOverlay)");
            // Spawn-or-find AdminOverlay and hand it a live PanelSettings (T-030: hub
            // scenes ship no UIDocument of their own; without this Open() no-ops).
            var admin = UnityEngine.Object.FindAnyObjectByType<AdminOverlay>(FindObjectsInactive.Include);
            if (admin == null)
            {
                var go = new GameObject("AdminOverlay");
                SceneManager.MoveGameObjectToScene(go, gameObject.scene);
                admin = go.AddComponent<AdminOverlay>();
            }
            if (!admin.TryBuild(ActivePanelSettings))
            {
                FlowTrace.Warn("UI", "DevPanel open FAILED — AdminOverlay.TryBuild returned false " +
                    "(no PanelSettings in this scene; dev tools went nowhere)");
                ShowToast("Dev tools unavailable — no UI panel settings in this scene.");
                return;
            }
            FlowTrace.Step("UI", "DevPanel built — opening AdminOverlay");
            // Close Help FIRST, then open Admin — both route through PanelManager.
            Close();
            admin.Open();
        }
#endif // DEVELOPMENT_BUILD || UNITY_EDITOR — dev tools launcher

        private void ShowToast(string message)
        {
            if (_toast == null || _toast.card == null || _toast.label == null) return;
            _toast.label.text = message;
            _toast.card.SetActive(true);
            _toastUntil = Time.unscaledTime + 5f;
        }
    }
}
