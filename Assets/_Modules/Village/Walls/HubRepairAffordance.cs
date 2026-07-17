// =============================================================================
// HubRepairAffordance - the OUT-OF-BATTLE (hub / overworld) structure-repair
// affordance (owner felt-test 2026-07-15: "WE do need a repair outside of battle
// in case they could not afford after the battle and needed to farm").
// -----------------------------------------------------------------------------
// THE GAP THIS CLOSES (proven from the code):
//   The repair BACKEND (WallRepairController: CostFor / RepairAll / RepairAllCost
//   / CanAffordMaterials, which also un-breaks broken-shell Towers via Tower.Repair)
//   is complete, but it was only ever REACHABLE in the WAVE context:
//     * WaveFeedbackDirector.OnWaveCleared -> SurfaceWorstRepair() : a ONE-SHOT
//       nudge fired the instant a wave is cleared;
//     * EndStateVM "Repair All" CTA : the ONE-SHOT end-of-battle screen;
//     * WallRepairController tap-to-select : only alive because
//       WaveFeedbackDirector.TrySpawn self-installs the controller, and that
//       method returns early when the scene has NO WaveManager
//       ("if (wave == null) return; // not a wave scene").
//   So a player who cannot AFFORD the repair right after a battle, leaves to FARM,
//   and returns to the hub has NO repair affordance waiting - the one-shot nudge
//   and end-state CTA are gone, and a pure hub scene has no controller at all.
//
// WHAT THIS ADDS (no second repair system - it REUSES the backend):
//   A self-installing, persistent, re-openable "REPAIR ALL" button that appears
//   whenever there are damaged structures AND we are NOT in an active wave. It
//   prices the whole damaged set through WallRepairController.RepairAllCost(),
//   and on tap:
//     * AFFORDABLE (wallet covers the FULL cost) -> WallRepairController.RepairAll()
//       (worst-first, un-breaks broken Towers, spends through the SAME
//       EconomyService path build-mode placement charges);
//     * NOT AFFORDABLE -> shows the cost + the exact SHORTFALL ("go farm") and
//       spends NOTHING (owner ruling: don't spend if they can't afford). Once the
//       player has farmed enough, the SAME button flips to affordable and repairs.
//
// MODULE ISOLATION: lives in DeNelle.Village; references only Village types +
// DeNelle.Core (ElarionUiKit / FlowTrace). Builds its own code-built uGUI canvas
// on the kit (no UXML, per PIPELINE_STATE) - it does NOT touch the HUD kit or the
// WallRepairHudBridge, so it is robust to the HUD's own repair-prompt wiring.
// =============================================================================

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.UI;
using CoreCost = DeNelle.Core.Catalog.ResourceCost;

namespace DeNelle.Village
{
    /// <summary>
    /// Persistent out-of-battle "Repair All" affordance for the hub / overworld.
    /// Self-installs into any gameplay scene that has repairable structures and
    /// drives <see cref="WallRepairController"/> (the one repair backend) so the
    /// player can repair damaged structures anytime after farming - gated on full
    /// affordability, with the shortfall shown when they cannot yet afford it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HubRepairAffordance : MonoBehaviour
    {
        // Below Build HUD (906) / selection (910) chrome; above the world.
        private const int SortingOrder = 905;
        private const float RefreshInterval = 0.75f;

        private WallRepairController _repair;
        private GameObject _canvas;
        private Button _button;
        private Image _buttonImg;
        private TextMeshProUGUI _label;
        private float _timer;

        // Last-announced state so FlowTrace logs transitions, not every poll.
        private enum Vis { Uninit, Hidden, AvailableAffordable, AvailableShort }
        private Vis _last = Vis.Uninit;

        // =====================================================================
        //  Self-install (mirrors WaveFeedbackDirector's spawn pattern)
        // =====================================================================

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallHook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TrySpawn();   // the first scene is already loaded when this runs
        }

        private static void OnSceneLoaded(Scene s, LoadSceneMode mode) => TrySpawn();

        private static void TrySpawn()
        {
            if (UnityEngine.Object.FindAnyObjectByType<HubRepairAffordance>() != null) return;
            if (!SceneHasRepairables()) return;   // Title / HeroSelect / menus: nothing to repair.

            var go = new GameObject("HubRepairAffordance");
            go.AddComponent<HubRepairAffordance>();
            FlowTrace.Step("Repair",
                $"hub repair affordance installed (scene='{SceneManager.GetActiveScene().name}')");
        }

        /// <summary>True when the scene has at least one repairable structure kind present.</summary>
        private static bool SceneHasRepairables()
        {
            return UnityEngine.Object.FindAnyObjectByType<WallSegment>() != null
                || UnityEngine.Object.FindAnyObjectByType<Gate>() != null
                || UnityEngine.Object.FindAnyObjectByType<Building>() != null
                || UnityEngine.Object.FindAnyObjectByType<Tower>() != null;
        }

        // =====================================================================
        //  Lifecycle
        // =====================================================================

        private void Awake()
        {
            BuildCanvas();
            SetVisible(false);
        }

        private void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = RefreshInterval;
            Refresh();
        }

        // =====================================================================
        //  Repair backend access
        // =====================================================================

        /// <summary>
        /// Resolves the one shared repair backend. Reuses an existing controller
        /// (a wave scene installs one); otherwise creates a LOGIC-ONLY controller
        /// (disabled so its own tap-to-select / highlight loop never runs in the
        /// hub) purely to price + apply Repair-All. Never a second repair system.
        /// </summary>
        private WallRepairController EnsureRepair()
        {
            if (_repair != null) return _repair;
            _repair = UnityEngine.Object.FindAnyObjectByType<WallRepairController>();
            if (_repair == null)
            {
                var cgo = new GameObject("WallRepair_HubEngine");
                _repair = cgo.AddComponent<WallRepairController>();
                // Logic-only: we call RepairAllCost / RepairAll directly; disabling
                // stops the controller's Update raycast so no unprompted selection
                // highlight appears in the hub.
                _repair.enabled = false;
                FlowTrace.Step("Repair",
                    "hub affordance self-installed a logic-only WallRepairController (no wave scene present)");
            }
            return _repair;
        }

        // =====================================================================
        //  Refresh - decide visibility + label, gated on out-of-battle + damage
        // =====================================================================

        private void Refresh()
        {
            // OUT-OF-BATTLE gate. A pure hub scene (MainCastle_Hall - no WaveManager)
            // always qualifies. In a scene that DOES run waves, show only in the calm
            // postures (Idle before the first wave, Countdown between waves); hide
            // during Active/Breached (a wave is on) and Complete/Defeated (the one-shot
            // EndState "Repair All" CTA owns that moment). This is the farm-then-return
            // hub posture the owner asked for, never mid-battle chrome.
            var wave = UnityEngine.Object.FindAnyObjectByType<WaveManager>();
            bool outOfBattle = wave == null
                || wave.Phase == WavePhase.Idle || wave.Phase == WavePhase.Countdown;
            if (!outOfBattle)
            {
                Announce(Vis.Hidden, default, default, false);
                SetVisible(false);
                return;
            }

            var repair = EnsureRepair();
            CoreCost cost = repair != null ? repair.RepairAllCost() : default;
            if (WallRepairController.MaterialsZero(cost))
            {
                // Nothing damaged - no affordance.
                Announce(Vis.Hidden, default, default, false);
                SetVisible(false);
                return;
            }

            bool affordable = repair != null && repair.CanAffordMaterials(cost);
            CoreCost shortfall = Shortfall(cost);

            SetVisible(true);
            if (affordable)
            {
                _label.text = "REPAIR ALL  (tap)\n" + WallRepairController.DescribeMaterials(cost);
                _label.color = ElarionUi.Parchment;
                if (_buttonImg != null) _buttonImg.color = ElarionUi.ConfirmFace;
                Announce(Vis.AvailableAffordable, cost, shortfall, true);
            }
            else
            {
                // Meaning never by colour alone (kit rule): the shortfall is in TEXT.
                _label.text = "NEED MORE TO REPAIR\n" +
                              WallRepairController.DescribeMaterials(shortfall) + " short - go farm";
                _label.color = ElarionUi.Parchment;
                if (_buttonImg != null) _buttonImg.color = ElarionUi.DangerFace;
                Announce(Vis.AvailableShort, cost, shortfall, false);
            }
        }

        /// <summary>FlowTrace the affordance state, but only when it CHANGES (not every poll).</summary>
        private void Announce(Vis state, CoreCost cost, CoreCost shortfall, bool affordable)
        {
            if (state == _last) return;
            _last = state;
            switch (state)
            {
                case Vis.Hidden:
                    FlowTrace.Step("Repair", "hub repair affordance: hidden (no damage / in battle)");
                    break;
                case Vis.AvailableAffordable:
                    FlowTrace.Step("Repair",
                        $"hub repair affordance AVAILABLE + affordable: cost {WallRepairController.DescribeMaterials(cost)}, wallet={WalletLine()}");
                    break;
                case Vis.AvailableShort:
                    FlowTrace.Step("Repair",
                        $"hub repair affordance AVAILABLE + short: cost {WallRepairController.DescribeMaterials(cost)}, " +
                        $"short {WallRepairController.DescribeMaterials(shortfall)}, wallet={WalletLine()}");
                    break;
            }
        }

        // =====================================================================
        //  Click - repair when affordable, otherwise refuse + show shortfall
        // =====================================================================

        private void OnClick()
        {
            var repair = EnsureRepair();
            if (repair == null) return;

            CoreCost cost = repair.RepairAllCost();
            if (WallRepairController.MaterialsZero(cost))
            {
                Refresh();
                return;
            }

            if (!repair.CanAffordMaterials(cost))
            {
                // Owner ruling: do NOT spend when they cannot afford. Show the shortfall.
                CoreCost shortfall = Shortfall(cost);
                FlowTrace.Step("Repair",
                    $"hub repair REFUSED (cannot afford): cost {WallRepairController.DescribeMaterials(cost)}, " +
                    $"short {WallRepairController.DescribeMaterials(shortfall)}, wallet={WalletLine()} - farm then return");
                Refresh();   // re-render the shortfall
                return;
            }

            // Affordable: repair everything (worst-first). RepairAll un-breaks broken
            // Towers (Tower.Repair clears the broken shell + re-enables its fire loop)
            // and spends through the SAME construction-economy path as build placement.
            var result = repair.RepairAll();
            FlowTrace.Step("Repair",
                $"hub repair AFFORDED: repaired={result.repairedCount} " +
                $"spent {WallRepairController.DescribeMaterials(result.spent)} " +
                $"remaining={result.remainingDamaged} wallet={WalletLine()}");
            _last = Vis.Uninit;   // force a fresh Announce on the next Refresh
            Refresh();
        }

        // =====================================================================
        //  Cost helpers
        // =====================================================================

        /// <summary>Per-material amount the wallet is missing to cover <paramref name="cost"/>.</summary>
        private static CoreCost Shortfall(CoreCost cost)
        {
            var econ = EconomyService.Instance;
            int w = econ != null ? econ.Wood : 0;
            int i = econ != null ? econ.Iron : 0;
            int f = econ != null ? econ.Food : 0;
            return new CoreCost
            {
                wood = Mathf.Max(0, cost.wood - w),
                iron = Mathf.Max(0, cost.iron - i),
                food = Mathf.Max(0, cost.food - f),
                crystals = 0,
            };
        }

        /// <summary>Compact wallet line for FlowTrace (matches WallRepairController's format).</summary>
        private static string WalletLine()
        {
            var econ = EconomyService.Instance;
            if (econ == null) return "<no EconomyService>";
            return $"W{econ.Wood} I{econ.Iron} F{econ.Food}";
        }

        // =====================================================================
        //  UI - one code-built uGUI canvas + button (ElarionUiKit; no UXML)
        // =====================================================================

        private void BuildCanvas()
        {
            if (_canvas != null) return;

            _canvas = new GameObject("HubRepairCanvas");
            _canvas.transform.SetParent(transform, false);
            var canvas = _canvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            var scaler = _canvas.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            _canvas.AddComponent<GraphicRaycaster>();

            // DEFAULT PLACEMENT (owner may move): mid-LEFT vertical band - the HUD
            // chrome clusters top (banners / Skip Tutorial / Menu) and bottom (ability
            // + build bars), so a mid-left box is the lowest-collision seat. Sized well
            // above the kit touch floor by the fraction rect.
            _button = ElarionUiKit.Button(_canvas.transform, "REPAIR ALL", ElarionUiKit.ButtonKind.Confirm,
                new Vector2(0.015f, 0.42f), new Vector2(0.205f, 0.58f), OnClick);
            if (_button != null)
            {
                _buttonImg = _button.GetComponent<Image>();
                _label = _button.GetComponentInChildren<TextMeshProUGUI>();
                if (_label != null)
                {
                    _label.enableAutoSizing = false;
                    _label.fontSize = 26f;
                }
            }
        }

        private void SetVisible(bool visible)
        {
            if (_canvas != null && _canvas.activeSelf != visible)
                _canvas.SetActive(visible);
        }
    }
}
