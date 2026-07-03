// =============================================================================
// EchoWorkforceHud -- the Echo Workforce panel (ECHO_WORKFORCE_SPEC).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// OWNER F8 (2026-06-28, WO-555): the Echo / offline-harvest readout used to be an
// ALWAYS-ON top-left widget (count + silo fill + Dump All, live on screen every
// frame in town). The owner called it "a side thought, not the main idea" and asked
// for it TUCKED AWAY behind a button next to Settings. So this is now a HIDDEN panel
// that only appears when the player taps the harvest button in the HUD top-right
// cluster (next to the Settings gear).
//
//   - The HUD button (VillageHudController, DeNelle.HUD) calls
//     HarvestPanelGate.RequestToggle() (Core seam — HUD never references Village, §5).
//   - This view subscribes to HarvestPanelGate.ToggleRequested and shows/hides itself.
//
// The HARVEST LOGIC is untouched (EchoService owns accrual / silo / Dump / unlocks);
// this is a PRESENTATION RELOCATION only. The panel is built with the shared Obsidian
// chrome (ElarionUiKit.BuildObsidianModal — near-black fill + gold trim + one Close)
// so it reads as the same designed game as every other panel. Still code-built uGUI
// (NO UXML -- UXML does not render in player builds; PIPELINE_STATE S8). It owns its
// OWN modal canvas, DISJOINT from VillageHudController so the two never collide.
//
// Lives on the EchoService DDOL host (installed by EchoWorkforceBootstrap).
// =============================================================================
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using DeNelle.Core.UI;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>Tucked-away Echo panel: count + silo fill + Dump All. Opened by the HUD
    /// harvest button (next to Settings) via <see cref="HarvestPanelGate"/>. Driven by
    /// EchoService. Hidden by default — never persistent on-screen chrome.</summary>
    [DisallowMultipleComponent]
    public sealed class EchoWorkforceHud : MonoBehaviour
    {
        private GameObject _modal;      // the whole modal canvas (toggled active/inactive)
        private TextMeshProUGUI _countLabel;
        private TextMeshProUGUI _siloLabel;
        private Image _fill;
        private TextMeshProUGUI _dumpLabel;
        private bool _open;

        // Life-force green for the silo fill (the resource the Echoes accrue).
        private static readonly Color LifeGreen = new Color(0.40f, 0.78f, 0.45f, 1f);

        private void Start()
        {
            Build();
            Hide();                              // tucked away: starts hidden, button-driven
            Refresh();
            if (EchoService.Instance != null)
            {
                EchoService.Instance.Changed += Refresh;
                EchoService.Instance.EchoUnlocked += OnEchoUnlocked;
            }
            HarvestPanelGate.ToggleRequested += Toggle;
            FlowTrace.Step("HUD", "EchoWorkforceHud built (hidden; opens via HarvestPanelGate / harvest button)");
        }

        private void OnDestroy()
        {
            if (EchoService.Instance != null)
            {
                EchoService.Instance.Changed -= Refresh;
                EchoService.Instance.EchoUnlocked -= OnEchoUnlocked;
            }
            HarvestPanelGate.ToggleRequested -= Toggle;
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
            Refresh();
            FlowTrace.Step("HUD", "EchoWorkforceHud OPEN");
        }

        private void Hide()
        {
            if (_modal == null) return;
            _open = false;
            _modal.SetActive(false);
            FlowTrace.Step("HUD", "EchoWorkforceHud CLOSED");
        }

        // -- build (shared Obsidian chrome) ---------------------------------------
        private void Build()
        {
            EnsureEventSystem();

            // The whole modal in one call: canvas + scrim (tap-outside closes) + Obsidian
            // chrome (near-black fill + gold trim + one Close). Compact, centred panel.
            var built = ElarionUiKit.BuildObsidianModal(
                "EchoHarvestPanel", "ECHO HARVEST",
                new Vector2(0.30f, 0.34f), new Vector2(0.70f, 0.66f),
                onClose: Hide, sortingOrder: 4600);   // above gameplay HUD, below the battle overlay (5000)
            _modal = built.canvas;
            var content = built.chrome.content.transform;

            // Echo count line.
            _countLabel = ElarionUiKit.Label(content, "Echoes  1/4", 0.70f, 0.84f,
                ElarionUi.Gilt, ElarionUi.FontHead, TextAlignmentOptions.Center,
                0.08f, 0.92f, bold: true);

            // Silo fill bar (life-force green) — Well track + Image.Type.Filled fill.
            var bar = ElarionUiKit.Bar(content, ElarionUiKit.BarKind.Castle,
                new Vector2(0.10f, 0.50f), new Vector2(0.90f, 0.62f), withValue: false);
            _fill = bar.fill;
            if (_fill != null) { _fill.color = LifeGreen; _fill.fillAmount = 0f; }

            // Silo % + raw value line under the bar.
            _siloLabel = ElarionUiKit.Label(content, "Silo  0%", 0.36f, 0.48f,
                new Color(0.85f, 0.85f, 0.9f, 1f), ElarionUi.FontBody, TextAlignmentOptions.Center,
                0.08f, 0.92f, bold: false);

            // Dump All button (large tap target) -> EchoService.DumpSilos().
            var dumpBtn = ElarionUiKit.Button(content, "Dump All", ElarionUiKit.ButtonKind.Confirm,
                new Vector2(0.22f, 0.16f), new Vector2(0.78f, 0.32f), OnDumpTapped);
            _dumpLabel = dumpBtn != null ? dumpBtn.GetComponentInChildren<TextMeshProUGUI>() : null;
        }

        // -- view refresh (logic -> view) -----------------------------------------
        private void Refresh()
        {
            var svc = EchoService.Instance;
            if (svc == null) return;
            if (_countLabel != null) _countLabel.text = $"Echoes  {svc.EchoCount}/{svc.MaxEchoes}";
            if (_fill != null) _fill.fillAmount = svc.FillFraction;
            if (_siloLabel != null)
            {
                int pct = Mathf.RoundToInt(svc.FillFraction * 100f);
                _siloLabel.text = $"Silo  {pct}%   ({Mathf.FloorToInt((float)svc.Silo)})";
            }
        }

        private void OnDumpTapped()
        {
            int banked = EchoService.Instance != null ? EchoService.Instance.DumpSilos() : 0;
            if (_dumpLabel != null)
            {
                _dumpLabel.text = banked > 0 ? $"+{banked} banked!" : "Silo empty";
                CancelInvoke(nameof(ResetDumpLabel));
                Invoke(nameof(ResetDumpLabel), 1.5f);
            }
            Refresh();
        }

        private void ResetDumpLabel()
        {
            if (_dumpLabel != null) _dumpLabel.text = "Dump All";
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
