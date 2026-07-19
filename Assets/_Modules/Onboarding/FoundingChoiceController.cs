// =============================================================================
// FoundingChoiceController — the founding "Default Town vs Build Your Own" screen
// (WO-748). Shown ONCE at founding, after PetSelect and BEFORE first hub entry.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Onboarding   Namespace: DeNelle.Onboarding
// Module isolation (port-spec Part 2, mirrors OnboardingFlow / PetSelectController):
// this references DeNelle.Core ONLY. It never touches DeNelle.Village — so the
// apply below is a CORE-ONLY GameState write, never a Village call.
//
// THE CHOICE (owner-requested 2026-07-18):
//   * "Default Town"   — drop in the prebuilt ring town, but as INDIVIDUALLY
//                        MOVABLE BaseLayout records (owner's explicit requirement).
//   * "Build Your Own" — today's blank template + the FTUE.
//
// HOW "Default Town" APPLIES (the reuse, resolving the coordinate landmine):
//   The prebuilt ring already exists as a scene bake (CastleHubBuilder) and there
//   is a proven ONE-SHOT writer — StrategicPlacementMigration.RunIfNeeded — that
//   converts each baked ring storefront into a BaseLayout PlacedStructureData
//   record AT ITS LIVE SCENE POSITION (grid-quantised via the live PlacementGrid),
//   then stands the bakes down so the records are the movable owners. On a normal
//   New Game that writer is disabled because ResetToNewGame sets
//   StrategicPlacementMigrated = true (the blank template).
//
//   So "Default Town" is simply: set StrategicPlacementMigrated = FALSE before the
//   Castle scene loads. With the marker false the injector keeps the baked ring
//   VISIBLE and the migration writer runs on Castle load, migrating the ring into
//   movable records at the LIVE grid cells — NOT the 2026-06 authored locals
//   (WO-748 RISK #1). "Build Your Own" leaves the marker true (blank template).
//
//   This is CORE-ONLY (a GameState field + Save) — no Village reference, no seam,
//   no coordinate authoring. The town positions come from the live scene, so the
//   merged-world castle flatten (WorldMergeBuilder.LowerCastleToGround) is honoured
//   automatically. See the RESIDUAL note at the foot of this file.
//
// UI: code-built uGUI on the Blink Obsidian kit (ElarionUiKit) — its own
// ScreenSpaceOverlay canvas, exactly like OnboardingFlow's coach-marks. NO UXML /
// UIDocument / PanelSettings (CLAUDE.md Section 8). ASCII-only copy (device tofu).
// Colour never carries meaning (owner colourblind) — each button is labelled.
// =============================================================================

using System;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.State;
using DeNelle.Core.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeNelle.Onboarding
{
    /// <summary>
    /// The founding layout choice. Presented once, after PetSelect and before the
    /// first hub load, by <see cref="PresentOrContinue"/>. "Default Town" flips
    /// <see cref="GameState.StrategicPlacementMigrated"/> to false (so the Castle-load
    /// migration writer converts the live baked ring into movable records); "Build
    /// Your Own" is a no-op (blank template + FTUE). Then it routes onward.
    /// </summary>
    public sealed class FoundingChoiceController : MonoBehaviour
    {
        // Session latch — the choice is offered at most once per session. New Game +
        // this being pre-hub means it never needs to re-offer within a run.
        private static bool _decidedThisSession;

        private Action _onContinue;
        private bool _routed;
        private GameObject _canvas;

        /// <summary>
        /// True when a founding choice should be offered: a genuinely FRESH founding —
        /// the player has NOT completed onboarding and nothing is built yet (empty
        /// BaseLayout). A returning player (Onboarded) or one who already founded
        /// (non-empty BaseLayout) is never re-offered. Also false once decided this
        /// session, or when the save service is not ready.
        /// </summary>
        public static bool ShouldOffer
        {
            get
            {
                if (_decidedThisSession) return false;
                var svc = GameStateService.Instance;
                if (svc == null || svc.State == null) return false;
                if (svc.State.Onboarded) return false;                    // returning player
                var layout = svc.State.BaseLayout;
                bool blank = layout == null || layout.Count == 0;         // nothing founded yet
                return blank;
            }
        }

        /// <summary>
        /// Entry point the intro flow calls in place of <c>SceneRouter.GoCastle</c>.
        /// If a founding choice is due (<see cref="ShouldOffer"/>) it builds the
        /// two-button screen and invokes <paramref name="onContinue"/> only once the
        /// player picks. Otherwise it invokes <paramref name="onContinue"/> immediately
        /// (no screen). <paramref name="onContinue"/> is the "enter the hub" action —
        /// typically <c>SceneRouter.GoCastle</c>.
        /// </summary>
        public static void PresentOrContinue(Action onContinue)
        {
            using var _ = FlowTrace.Enter("Founding", "FoundingChoiceController.PresentOrContinue");

            if (!ShouldOffer)
            {
                FlowTrace.Step("Founding",
                    "not offering the founding choice (already decided / returning player / already founded / no save) — continuing straight to the hub.");
                onContinue?.Invoke();
                return;
            }

            _decidedThisSession = true;   // this presentation IS the decision surface — never re-offer

            var host = new GameObject("FoundingChoiceUI");
            var ctrl = host.AddComponent<FoundingChoiceController>();
            ctrl._onContinue = onContinue;
            ctrl.Build();
            FlowTrace.Step("Founding", "founding choice presented (Default Town vs Build Your Own).");
        }

        // =====================================================================
        //  Overlay construction (code-built uGUI on the Blink Obsidian kit)
        // =====================================================================

        private void Build()
        {
            using var _ = FlowTrace.Enter("Founding", "FoundingChoiceController.Build (uGUI Obsidian)");

            // Own overlay canvas, well above the intro UIDocument. Parent it into the
            // active (intro) scene so it tears down with the scene on the hub load.
            _canvas = ElarionUiKit.BuildModalCanvas("FoundingChoiceCanvas", 31000);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(
                _canvas, gameObject.scene);

            // Raycast-blocking scrim (mirrors OnboardingFlow) — swallows taps to the
            // intro UI beneath while the choice is up.
            var scrim = ElarionUiKit.AddImage(_canvas.transform, "Scrim",
                Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0.72f), rounded: false);
            var scrimImg = scrim.GetComponent<Image>();
            if (scrimImg != null) scrimImg.raycastTarget = true;

            // Centred Obsidian panel — the shared Close is hidden (this is a forced
            // choice, no dismiss). withBackdrop:false — the scrim above already dims.
            var chrome = ElarionUiKit.BuildObsidianPanel(_canvas.transform, "FOUND YOUR TOWN",
                new Vector2(0.12f, 0.24f), new Vector2(0.88f, 0.76f), onClose: null,
                withBackdrop: false);
            if (chrome.close != null) chrome.close.gameObject.SetActive(false);

            Transform body = chrome.layout != null && chrome.layout.body != null
                ? chrome.layout.body.transform
                : chrome.content.transform;

            // Body copy — teaches that BOTH options stay editable, so the choice is
            // low-stakes (owner: every building is movable). Inline ASCII literal (not a
            // canon key) so it never shows a missing-key placeholder.
            var copy = ElarionUiKit.Label(body,
                "How would you like to begin? Start with a ready-made town you can rearrange, " +
                "or a clear field to raise from the ground up. Either way, every building can be " +
                "moved, upgraded, or sold later.",
                0.56f, 0.94f, ElarionUi.Parchment, ElarionUi.FontBody,
                TextAlignmentOptions.Center, 0.06f, 0.94f);
            copy.textWrappingMode = TextWrappingModes.Normal;
            copy.raycastTarget = false;

            // Two stacked full-width buttons (large mobile touch targets). Meaning is in
            // the LABEL, not the colour (owner colourblind): "Default Town" (Green CTA)
            // and "Build Your Own" (Gray).
            ElarionUiKit.BuildObsidianButton(body, "Default Town",
                ElarionUiKit.ObsidianButtonStyle.Style2, ElarionUiKit.ObsidianButtonColor.Green,
                new Vector2(0.08f, 0.30f), new Vector2(0.92f, 0.48f), OnDefaultTown);

            ElarionUiKit.BuildObsidianButton(body, "Build Your Own",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Gray,
                new Vector2(0.08f, 0.06f), new Vector2(0.92f, 0.24f), OnBuildYourOwn);
        }

        // =====================================================================
        //  Choices
        // =====================================================================

        /// <summary>
        /// "Default Town" — flip StrategicPlacementMigrated to false so the Castle-load
        /// migration writer converts the LIVE baked ring into movable BaseLayout records,
        /// then continue to the hub. GRANTED: no cost, no Place(), does NOT touch
        /// FreeBuildsUsed (the one-free-total first placement is preserved).
        /// </summary>
        private void OnDefaultTown()
        {
            if (_routed) return;
            FlowTrace.Step("Founding", "choice = DEFAULT TOWN.");

            var svc = GameStateService.Instance;
            if (svc == null || svc.State == null)
            {
                // Save service vanished between offer and tap — cannot arm the town.
                // Fail-loud, then fall through to Build-Your-Own (blank) rather than strand.
                FlowTrace.Fail("Founding",
                    "Default Town chosen but GameStateService is null — cannot clear StrategicPlacementMigrated; " +
                    "falling back to the blank template.");
                Continue();
                return;
            }

            // Guard the state write (Section 12): one bad op logs (break-log) and is
            // skipped, never a silent failure. Clearing the marker re-enables the proven
            // one-shot migration writer, which reads LIVE positions (RISK #1 resolved).
            bool ok = Guard.Try("Founding", "arm Default Town (clear StrategicPlacementMigrated)", () =>
            {
                svc.State.StrategicPlacementMigrated = false;
                svc.Save();
            });

            if (ok)
                FlowTrace.Step("Founding",
                    "StrategicPlacementMigrated cleared + saved — the Castle-load migration writer will convert " +
                    "the live baked ring into MOVABLE BaseLayout records at the live grid cells. " +
                    "(Handover contract: the ring is visible on the founding load and becomes movable on the next hub load.)");
            else
                FlowTrace.Warn("Founding",
                    "Default Town arm FAILED (see the Guard line above) — the marker may still be set; the hub could load blank. " +
                    "Residual: verify StrategicPlacementMigrated == false in the save before entering the hub.");

            Continue();
        }

        /// <summary>"Build Your Own" — no-op: leave the blank template + FTUE. Continue.</summary>
        private void OnBuildYourOwn()
        {
            if (_routed) return;
            FlowTrace.Step("Founding", "choice = BUILD YOUR OWN (blank template + FTUE) — no state change.");
            Continue();
        }

        /// <summary>Tear the overlay down and invoke the continue action exactly once.</summary>
        private void Continue()
        {
            if (_routed) return;
            _routed = true;
            var cont = _onContinue;
            _onContinue = null;
            if (_canvas != null) Destroy(_canvas);
            Destroy(gameObject);
            cont?.Invoke();
        }

        private void OnDestroy()
        {
            if (_canvas != null) Destroy(_canvas);
        }
    }
}

// =============================================================================
// RESIDUAL RISKS FLAGGED FOR THE PO / HEADLESS VERIFY (WO-748) — could not be
// fully validated from static reading; the orchestrator should confirm headless:
//
//  R1 (RISK #1 coordinate mismatch — RESOLVED by design, verify):
//      Default Town does NOT author cell coordinates. It re-enables
//      StrategicPlacementMigration.RunIfNeeded, which reads each baked ring
//      storefront's LIVE world position and grid-quantises it via the live
//      PlacementGrid. So the merged-world castle flatten is honoured. VERIFY the
//      baked ring objects (Blacksmith_Weapons_Storefront, Lumbermill_Wood_Storefront,
//      Windmill_Food_Storefront, EchoHollow_Pets_RoamingArea, Forge_Armor_Storefront,
//      ArcaneTower_MagicUpgrades, Marketplace_Monetization) are actually present +
//      ACTIVE in the live home hub (Main_Castle_Overworld / MainCastle_Hall) on the
//      founding load with the marker false — RunIfNeeded's FindByName excludes
//      inactive objects, so any that are baked-inactive migrate as "absent" (skipped).
//
//  R2 (handover timing): with the marker cleared, the migration writer runs on the
//      FIRST hub load and latches that scene handle, so the records replay as MOVABLE
//      on the NEXT hub load (the established one-shot-migration contract). On the
//      founding load itself the player sees the baked ring (immovable that session).
//      This matches how legacy saves migrate. Confirm this is acceptable founding UX.
//
//  R3 (entry coverage): this is presented from PetSelectController's route-to-hub
//      (the canonical Start New chokepoint: card-pick + BypassPetSelect). If any
//      OTHER path reaches the hub for a fresh founding WITHOUT passing PetSelect
//      (e.g. a Play-Intro cinematic that routes straight to GoCastle), the choice is
//      not offered and that player gets the blank template. Verify the live intro
//      graph funnels every fresh founding through PetSelect.
// =============================================================================
