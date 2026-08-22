// =============================================================================
// EchoWorkforceHud -- the Echo Workforce panel (ECHO_WORKFORCE_SPEC). DUMB SKIN.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// OWNER F8 (2026-06-28, WO-555): the Echo / offline-harvest readout is a HIDDEN
// panel that only appears when the player taps the harvest button in the HUD
// top-right cluster (next to the Settings gear).
//   - The HUD button (VillageHudController, DeNelle.HUD) calls
//     HarvestPanelGate.RequestToggle() (Core seam — HUD never references Village, §5).
//   - This view subscribes to HarvestPanelGate.ToggleRequested and shows/hides itself.
//
// MVVM (Silo F): this View reads NO service. Every count / silo / pending / rate value
// and the Collect-All command come from EchoWorkforceVM; the View just repaints the
// VM's strings on Changed. Still code-built uGUI on the shared Obsidian chrome (NO UXML,
// PIPELINE_STATE S8). Lives on the EchoService DDOL host (EchoWorkforceBootstrap).
// =============================================================================
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using DeNelle.Core.UI;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.Ads;
using DeNelle.Village.Monetization;

namespace DeNelle.Village
{
    /// <summary>Tucked-away Echo panel: count + silo fill + Collect All. Opened by the HUD
    /// harvest button (next to Settings) via <see cref="HarvestPanelGate"/>. Binds
    /// <see cref="EchoWorkforceVM"/>. Hidden by default — never persistent on-screen chrome.</summary>
    [DisallowMultipleComponent]
    public sealed class EchoWorkforceHud : MonoBehaviour
    {
        private GameObject _modal;      // the whole modal canvas (toggled active/inactive)
        private TextMeshProUGUI _countLabel;
        private TextMeshProUGUI _siloLabel;
        private Image _fill;
        private TextMeshProUGUI _dumpLabel;
        private Button _boostButton;
        private TextMeshProUGUI _boostButtonLabel;
        private TextMeshProUGUI _boostStatusLabel;
        private bool _adInFlight;
        private float _nextBoostRefreshAt;
        private bool _open;
        private PanelHandle _panelHandle;   // HUD-1: modal arbiter registration (one Echo modal at a time)

        private EchoWorkforceVM _vm;

        // Life-force green for the silo fill (the resource the Echoes accrue).
        private static readonly Color LifeGreen = new Color(0.40f, 0.78f, 0.45f, 1f);

        private void Start()
        {
            Build();
            Hide();                              // tucked away: starts hidden, button-driven

            _vm = EchoWorkforceVM.CreateDefault(Hide);
            _vm.Changed += Refresh;
            _vm.EchoUnlocked += OnEchoUnlocked;
            HarvestBoostService.Changed += RefreshBoostOffer;

            Refresh();
            HarvestPanelGate.ToggleRequested += Toggle;
            FlowTrace.Step("HUD", "EchoWorkforceHud built (hidden; opens via HarvestPanelGate / harvest button)");
        }

        private void OnDestroy()
        {
            if (_vm != null)
            {
                _vm.Changed -= Refresh;
                _vm.EchoUnlocked -= OnEchoUnlocked;
                _vm.Dispose();
                _vm = null;
            }
            HarvestPanelGate.ToggleRequested -= Toggle;
            HarvestBoostService.Changed -= RefreshBoostOffer;
        }

        // -- open / close (button-driven) -----------------------------------------
        private void Toggle()
        {
            if (_open) Hide();
            else Show();
        }

        private void Show()
        {
            if (_modal == null) return;
            _open = true;
            _modal.SetActive(true);

            // HUD-1: register with the single-modal arbiter and announce the open. Opening the
            // harvest panel CLOSES any other Echo modal (roster/card/unlock) that was up.
            // Battle-lock (WO-437): a rejected open self-closes via the registered Hide.
            if (_panelHandle == null)
                _panelHandle = PanelManager.Register("EchoHarvest", Hide, () => _open);
            if (!PanelManager.NotifyOpened(_panelHandle))
            {
                FlowTrace.Warn("HUD", "EchoWorkforceHud open rejected by PanelManager (battle-lock).");
                return;
            }
            Refresh();
            RefreshBoostOffer();
            FlowTrace.Step("HUD", "EchoWorkforceHud OPEN");
        }

        private void Hide()
        {
            if (_modal == null) return;
            _open = false;
            _modal.SetActive(false);
            if (_panelHandle != null) PanelManager.NotifyClosed(_panelHandle);
            FlowTrace.Step("HUD", "EchoWorkforceHud CLOSED");
        }

        // -- build (shared Obsidian chrome) ---------------------------------------
        private void Build()
        {
            EnsureEventSystem();

            var built = ElarionUiKit.BuildObsidianModal(
                "EchoHarvestPanel", "ECHO HARVEST",
                new Vector2(0.30f, 0.24f), new Vector2(0.70f, 0.76f),
                onClose: Hide, sortingOrder: 31000,   // canon MODAL band (was 4600: full-screen scrim modal drew UNDER runtime HUD overlays)
                frameName: RpgUiCatalog.FrameCore);
            _modal = built.canvas;
            var content = built.chrome.content.transform;

            // Echo count line.
            _countLabel = ElarionUiKit.Label(content, "Echoes  1/4", 0.76f, 0.88f,
                ElarionUi.Gilt, ElarionUi.FontHead, TextAlignmentOptions.Center,
                0.08f, 0.92f, bold: true);

            // Silo fill bar (life-force green) — Well track + Image.Type.Filled fill.
            var bar = ElarionUiKit.Bar(content, ElarionUiKit.BarKind.Castle,
                new Vector2(0.10f, 0.60f), new Vector2(0.90f, 0.70f), withValue: false);
            _fill = bar.fill;
            if (_fill != null) { _fill.color = LifeGreen; _fill.fillAmount = 0f; }

            // Silo % + raw value line under the bar.
            _siloLabel = ElarionUiKit.Label(content, "Silo  0%", 0.48f, 0.58f,
                new Color(0.85f, 0.85f, 0.9f, 1f), ElarionUi.FontBody, TextAlignmentOptions.Center,
                0.08f, 0.92f, bold: false);

            // Collect All (CoC pipe-home): collectors pending + echo silo.
            var dumpBtn = ElarionUiKit.Button(content, "Collect All", ElarionUiKit.ButtonKind.Confirm,
                new Vector2(0.22f, 0.30f), new Vector2(0.78f, 0.47f), OnCollectAllTapped);
            _dumpLabel = dumpBtn != null ? dumpBtn.GetComponentInChildren<TextMeshProUGUI>() : null;

            // The free collection action stays primary and immediate. This separate, optional
            // placement starts a TIME boost only after AdGateService receives the SDK's genuine
            // earned-reward callback; dismiss/no-fill can never intercept or alter Collect All.
            var boostGo = ElarionUiKit.Button(content, "Watch Ad - 2x speed for 1 hour",
                ElarionUiKit.ButtonKind.Quiet,
                new Vector2(0.16f, 0.09f), new Vector2(0.84f, 0.26f), OnBoostAdTapped);
            _boostButton = boostGo != null ? boostGo.GetComponent<Button>() : null;
            _boostButtonLabel = boostGo != null ? boostGo.GetComponentInChildren<TextMeshProUGUI>() : null;
            _boostStatusLabel = ElarionUiKit.Label(content, "Optional time boost", 0.035f, 0.085f,
                new Color(0.76f, 0.78f, 0.82f, 1f), ElarionUi.FontBody,
                TextAlignmentOptions.Center, 0.08f, 0.92f, bold: false);
        }

        private void Update()
        {
            if (!_open || Time.unscaledTime < _nextBoostRefreshAt) return;
            _nextBoostRefreshAt = Time.unscaledTime + 1f;
            RefreshBoostOffer();
        }

        // -- view refresh (VM -> view, one direction) -----------------------------
        private void Refresh()
        {
            if (_vm == null || !_vm.HasWorkforce) return;
            if (_countLabel != null) _countLabel.text = _vm.HudCountLine;
            if (_fill != null) _fill.fillAmount = _vm.FillFraction;
            if (_siloLabel != null) _siloLabel.text = _vm.HudSiloLine;
        }

        private void OnCollectAllTapped()
        {
            int banked = _vm != null ? _vm.CollectAll() : 0;
            if (_dumpLabel != null)
            {
                _dumpLabel.text = banked > 0 ? $"+{banked} collected!" : "Nothing to collect";
                CancelInvoke(nameof(ResetDumpLabel));
                Invoke(nameof(ResetDumpLabel), 1.5f);
            }
            // _vm.CollectAll already raised Changed -> Refresh repainted the counts.
        }

        private void ResetDumpLabel()
        {
            if (_dumpLabel != null) _dumpLabel.text = "Collect All";
        }

        private void OnBoostAdTapped()
        {
            if (_adInFlight) return;

            AdOffer offer = AdGateService.Offer("place.harvest.doubler");
            if (!offer.Available)
            {
                RefreshBoostOffer();
                return;
            }

            _adInFlight = true;
            RefreshBoostOffer();
            bool started = AdGateService.Present("place.harvest.doubler", contextGrant: null,
                onComplete: OnBoostAdCompleted);
            if (!started)
            {
                _adInFlight = false;
                RefreshBoostOffer();
            }
        }

        private void OnBoostAdCompleted(AdShowResult result)
        {
            _adInFlight = false;
            if (_boostStatusLabel != null)
            {
                _boostStatusLabel.text = result.Rewarded
                    ? "2x harvest speed is active"
                    : result.Reason == AdUnavailableReason.Abandoned
                        ? "Ad closed early - no boost granted"
                        : new AdOffer(false, result.Reason, null, null, null, null, 0, 0).RefusalText();
            }
            _nextBoostRefreshAt = Time.unscaledTime + 1.5f;
            RefreshBoostOffer(updateStatus: result.Rewarded);
        }

        private void RefreshBoostOffer() => RefreshBoostOffer(updateStatus: true);

        private void RefreshBoostOffer(bool updateStatus)
        {
            if (_boostButton == null) return;

            AdOffer offer = AdGateService.Offer("place.harvest.doubler");
            _boostButton.interactable = !_adInFlight && offer.Available;
            if (_boostButtonLabel != null)
                _boostButtonLabel.text = _adInFlight
                    ? "Playing ad..."
                    : HarvestBoostService.IsActive
                        ? "Watch Ad - add 1 hour of 2x speed"
                        : "Watch Ad - 2x speed for 1 hour";

            if (!updateStatus || _boostStatusLabel == null) return;
            if (HarvestBoostService.IsActive)
            {
                int minutes = Mathf.Max(1, Mathf.CeilToInt((float)(HarvestBoostService.SecondsRemaining / 60.0)));
                _boostStatusLabel.text = $"2x speed active - {minutes} min remaining";
            }
            else
            {
                _boostStatusLabel.text = offer.Available
                    ? "Optional: fills the same silo twice as fast"
                    : offer.RefusalText();
            }
        }

        private void OnEchoUnlocked(int newCount)
        {
            // Lightweight "New Echo joined!" toast on the count label (only meaningful when open).
            if (_countLabel != null)
            {
                _countLabel.text = "New Echo joined!";
                CancelInvoke(nameof(Refresh));
                Invoke(nameof(Refresh), 2.0f);
            }
        }

        // -- helpers --------------------------------------------------------------
        private static void EnsureEventSystem()
        {
            // EventSystem.current is a plain static (NOT a scene query) — no banned FindAnyObjectByType.
            if (EventSystem.current != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
            DontDestroyOnLoad(es);
        }
    }
}
