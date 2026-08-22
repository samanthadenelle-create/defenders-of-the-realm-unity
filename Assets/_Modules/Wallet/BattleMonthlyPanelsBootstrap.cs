// =============================================================================
// BattleMonthlyPanelsBootstrap - the runtime DOOR to the Season Track and the
// Monthly Ledger.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Wallet   Namespace: DeNelle.Wallet
//
// =============================================================================
//  WHY THIS FILE EXISTS AT ALL (2026-08-21 merge)
// -----------------------------------------------------------------------------
// Two seats built these two screens independently and neither could see the other.
// The pair that survived - SeasonTrackPanel + MonthlyLedgerPanel - is the
// canon-compliant one: exact wireframe geometry, every state word pulled from
// canon-strings.json (the owner is red/green colourblind, so a state carried by
// hue alone is a defect), one animated element per screen. It had exactly ONE
// defect: NOTHING REGISTERED IT, so PanelRouter.Open(PanelId.BattlePass) returned
// false and the screens shipped unopenable.
//
// The retired rival (BattleMonthlyPanels.cs, a 145-line static) was wired but typed
// player-facing sentences INLINE ("PLAY ARENA BATTLES TO EARN TIERS", "CLAIMS LEFT",
// "CLAIM TODAY") and derived on-screen state words from enum.ToString(), which puts
// a developer identifier - "PremiumLocked" - on a player's screen. Both violate
// CLAUDE.md section 7. Its full original text is preserved in
// WorkOrders/WORK_ORDER_battle_and_monthly_packs.md under "RETIRED DUPLICATE".
//
// THIS FILE IS THE ONE THING THAT FILE GOT RIGHT, KEPT: the registration. The
// LIFECYCLE half of that discipline lives in the panels themselves, where it
// belongs - each one registers a PanelHandle in Awake, calls NotifyOpened in
// OnEnable and CLOSES ITSELF WHEN THAT RETURNS FALSE (the WO-437 battle-lock
// refusal), and calls NotifyClosed in OnDisable.
//
// =============================================================================
//  HOST-FREE, LIKE PackStoreBootstrap
// -----------------------------------------------------------------------------
// Neither screen is placed in a scene, so the opener FIND-OR-SPAWNS its panel. The
// host GameObject is created ACTIVE, so AddComponent runs Awake (arbiter register)
// then OnEnable (build + show + NotifyOpened) in one step - the same contract
// PackStoreBootstrap relies on. A re-open of a hidden host is a SetActive(true).
//
// DeNelle.Wallet -> DeNelle.Core only (PanelRouter / PanelId / PanelManager live in
// Core.UI). No reflection, no reference to DeNelle.Village.
//
// NEVER AUTO-POPS. There is no URL trigger and no scene hook here (unlike the store's
// demo door): section 8 discovery rule C5 says the player opens these screens, and a
// monetization surface that opens itself is the exact pressure the covenant forbids.
// =============================================================================

using UnityEngine;
using UnityEngine.SceneManagement;
using DeNelle.Core.UI;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Wallet
{
    /// <summary>
    /// Registers the <see cref="PanelId.BattlePass"/> and <see cref="PanelId.MonthlyLedger"/>
    /// openers at boot and find-or-spawns the two surviving panels. Pure static; no scene object.
    /// </summary>
    public static class BattleMonthlyPanelsBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterOpeners()
        {
            PanelRouter.Register(PanelId.BattlePass, OpenSeasonTrack);

            // ⚠ LAMBDAS, NOT METHOD GROUPS, for the ledger. OpenMonthlyLedger is overloaded
            // (parameterless + string), and Register is overloaded on Action / Action<string> -
            // handing it the bare method group is AMBIGUOUS and will not compile. The arity of
            // each lambda picks the overload with no room for doubt.
            PanelRouter.Register(PanelId.MonthlyLedger, () => OpenMonthlyLedger(null));

            // The ledger can also be opened FOCUSED on one card (a pack tile, a merchant verb).
            // The plain opener above stays the "no particular card" fallback and shows the first
            // authored card, exactly as the panel's own ActiveCard does.
            PanelRouter.Register(PanelId.MonthlyLedger, (string sku) => OpenMonthlyLedger(sku));

            FlowTrace.Step("BattlePass",
                "BattleMonthlyPanelsBootstrap: PanelId.BattlePass + PanelId.MonthlyLedger openers registered.");
        }

        /// <summary>
        /// Opens the Season Track (<see cref="SeasonTrackPanel"/>), find-or-spawning its host.
        /// Idempotent: re-opening a live screen just re-renders it.
        /// </summary>
        public static void OpenSeasonTrack()
        {
            using var _ = FlowTrace.Enter("BattlePass", "BattleMonthlyPanelsBootstrap.OpenSeasonTrack");

            var panel = UnityEngine.Object.FindAnyObjectByType<SeasonTrackPanel>(FindObjectsInactive.Include);
            if (panel == null)
            {
                var go = NewHost("SeasonTrack (SeasonTrackPanel)");
                go.AddComponent<SeasonTrackPanel>();   // Awake -> arbiter register; OnEnable -> build + show
                FlowTrace.Step("BattlePass", "SeasonTrackPanel host spawned (host-free first open).");
                return;
            }

            if (!panel.gameObject.activeSelf)
            {
                panel.gameObject.SetActive(true);      // OnEnable -> show + NotifyOpened
                FlowTrace.Step("BattlePass", "existing SeasonTrackPanel re-shown.");
            }
            else
            {
                panel.Render();
                FlowTrace.Step("BattlePass", "SeasonTrackPanel already open - re-rendered.");
            }
        }

        /// <summary>Opens the Monthly Ledger on the first authored card.</summary>
        public static void OpenMonthlyLedger() => OpenMonthlyLedger(null);

        /// <summary>
        /// Opens the Monthly Ledger (<see cref="MonthlyLedgerPanel"/>) focused on
        /// <paramref name="sku"/>; a null/empty sku falls back to the first authored card
        /// (the panel's own ActiveCard rule). Find-or-spawns the host. Idempotent.
        /// </summary>
        public static void OpenMonthlyLedger(string sku)
        {
            using var _ = FlowTrace.Enter("MonthlyCard", "BattleMonthlyPanelsBootstrap.OpenMonthlyLedger");

            var panel = UnityEngine.Object.FindAnyObjectByType<MonthlyLedgerPanel>(FindObjectsInactive.Include);
            if (panel == null)
            {
                var go = NewHost("MonthlyLedger (MonthlyLedgerPanel)");

                // ⚠ INACTIVE FIRST, so Show(sku) lands BEFORE OnEnable renders. Adding the
                // component to an already-active host would run OnEnable immediately and draw
                // the wrong card for one frame before the caller could point it anywhere.
                go.SetActive(false);
                var fresh = go.AddComponent<MonthlyLedgerPanel>();
                if (!string.IsNullOrEmpty(sku)) fresh.Show(sku);
                go.SetActive(true);

                FlowTrace.Step("MonthlyCard", "MonthlyLedgerPanel host spawned (host-free first open).");
                return;
            }

            if (!string.IsNullOrEmpty(sku)) panel.Show(sku);

            if (!panel.gameObject.activeSelf)
            {
                panel.gameObject.SetActive(true);      // OnEnable -> show + NotifyOpened
                FlowTrace.Step("MonthlyCard", "existing MonthlyLedgerPanel re-shown.");
            }
            else
            {
                panel.Render();
                FlowTrace.Step("MonthlyCard", "MonthlyLedgerPanel already open - re-rendered.");
            }
        }

        /// <summary>A host GameObject in the ACTIVE scene, so it dies with the scene like any panel.</summary>
        private static GameObject NewHost(string name)
        {
            var go = new GameObject(name);
            var active = SceneManager.GetActiveScene();
            if (active.IsValid()) SceneManager.MoveGameObjectToScene(go, active);
            return go;
        }
    }
}
