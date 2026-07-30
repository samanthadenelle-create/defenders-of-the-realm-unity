// =============================================================================
// RaidVictoryController — the MISSING subscriber that closes the core loop:
//   walk to a base -> CLEAR it -> CLAIM the base -> trigger the NEXT COMPANION
//   -> RETURN home (no soft-lock).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.World.Camps
//
// THE GAP THIS CLOSES (FeatureFlags.Raid was OFF because of exactly this):
//   "RaidGarrisonSpawner.OnCleared has no subscriber ... a cleared raid soft-locks."
// Everything UP TO clear already works (entry, garrison spawn, combat, RETREAT all
// reuse proven systems — see RaidGarrisonSpawner / RaidDeployController). What was
// missing was the VICTORY half: detect the clear, claim the base, hand the player
// the next companion, and route them home. This component is that half.
//
// SELF-INSTALL: mirrors RaidDeployController — a RuntimeInitialize hook adds ONE
// controller to any RaidBase_* scene (idempotent). It then finds the scene's
// RaidGarrisonSpawner and subscribes to OnCleared (or, if the garrison already
// cleared before we bound — e.g. an empty composition — handles it immediately).
//
// THE FOUR STEPS (each FlowTrace-instrumented, system "Raid"):
//   1. VICTORY  — OnCleared fires (last defender dead). Guard against double-fire.
//   2. CLAIM    — RaidClaimService.MarkClaimed(configId) persists the win, and
//                 SceneOwnership.SetEnemyOwned(false) flips the live scene PLAYER-
//                 owned (the inverse of the spawner's SetEnemyOwned(true)) so the
//                 base reads as YOURS for the rest of this session.
//   3. COMPANION— on a NEW claim only, unlock the next canon companion into the
//                 persisted party (GameStateService.AddToParty) — the rescue beat.
//   4. RETURN   — a code-built victory banner with a "Return to Castle" button
//                 (SceneRouter.GoCastle), plus an auto-return safety timer so the
//                 player is NEVER stranded on a cleared raid.
//
// SCOPE / STUBS (flagged): the full WO-431 star-scoring + reward-breakdown victory
// SCREEN and the WO-441 Phase-C special-node auto-harvest outpost are OUT of this
// spine — this builds the victory->claim->next-companion->return BACKBONE end-to-
// end (minimal but real), so RAID can flip ON without a soft-lock. See REPORT.
//
// Code-built uGUI (NO UXML — repo rule), via the shared ElarionUiKit so it matches
// the raid deploy HUD. ASCII-only runtime strings. Canon: Elarion (never Avalon).
// =============================================================================

using System.Collections;
using UnityEngine;
using DeNelle.Core;
using DeNelle.Core.State;
using DeNelle.Core.UI;
using DeNelle.Core.Diagnostics;
using DeNelle.Village.UI;

namespace DeNelle.Village.World.Camps
{
    /// <summary>
    /// Subscribes to <see cref="RaidGarrisonSpawner.OnCleared"/> and runs the raid
    /// victory flow: claim the base, unlock the next companion, and return the hero
    /// home (with a victory banner). Self-installs into any <c>RaidBase_*</c> scene.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RaidVictoryController : MonoBehaviour
    {
        [Tooltip("Seconds after victory before the hero auto-returns to the castle if the " +
                 "player never taps the button (anti-soft-lock safety net).")]
        [SerializeField] private float _autoReturnSeconds = 12f;

        private RaidGarrisonSpawner _spawner;
        private bool _handled;     // victory handled once (guards a double OnCleared)
        private bool _returning;   // a return is already in flight

        // =====================================================================
        //  Self-install — one controller per RaidBase_* scene
        // =====================================================================

        /// <summary>
        /// On every scene load, if the active scene is a <c>RaidBase_*</c> the victory
        /// controller installs itself (idempotent). Mirrors RaidDeployController's hook,
        /// so the two command surfaces (deploy/retreat + victory) sit side by side.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallHook()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
            TryInstall(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene,
                                          UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            TryInstall(scene.name);
        }

        private static void TryInstall(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return;
            if (!sceneName.StartsWith("RaidBase", System.StringComparison.OrdinalIgnoreCase)) return;
            if (FindAnyObjectByType<RaidVictoryController>() != null) return;

            var go = new GameObject("RaidVictoryController");
            go.AddComponent<RaidVictoryController>();
            FlowTrace.Step("Raid", $"RaidVictoryController self-installed in raid scene '{sceneName}'.");
        }

        // =====================================================================
        //  Bind to the garrison spawner (it spawns its garrison one frame after its
        //  own Start, so we poll a few frames for it rather than assuming Start order).
        // =====================================================================

        private void Start()
        {
            StartCoroutine(BindRoutine());
        }

        private void OnDestroy()
        {
            if (_spawner != null) _spawner.OnCleared -= HandleCleared;
        }

        private IEnumerator BindRoutine()
        {
            // The spawner lives on the RaidBase_<id> root and arms its garrison a frame
            // after Start; give it a handful of frames to appear, then bind.
            for (int i = 0; i < 10 && _spawner == null; i++)
            {
                _spawner = FindAnyObjectByType<RaidGarrisonSpawner>();
                if (_spawner != null) break;
                yield return null;
            }

            if (_spawner == null)
            {
                FlowTrace.Warn("Raid", "RaidVictoryController: no RaidGarrisonSpawner found in this raid scene — " +
                                       "victory cannot be detected (the loop would soft-lock). Leaving the deploy/retreat exit as the only out.");
                yield break;
            }

            // If the garrison already cleared before we bound (empty composition / no
            // navmesh path => MarkCleared in ActivateRoutine), handle it now; otherwise
            // subscribe for the live last-defender-dies event.
            if (_spawner.Cleared)
            {
                FlowTrace.Step("Raid", "RaidVictoryController: garrison was ALREADY cleared on bind — running victory immediately.");
                HandleCleared(_spawner);
            }
            else
            {
                _spawner.OnCleared -= HandleCleared;
                _spawner.OnCleared += HandleCleared;
                FlowTrace.Step("Raid", $"RaidVictoryController bound to OnCleared (garrison of {_spawner.TotalGarrison} defender(s)).");
            }
        }

        // =====================================================================
        //  STEP 1 — VICTORY. The last defender died (or the garrison was empty).
        // =====================================================================

        private void HandleCleared(RaidGarrisonSpawner spawner)
        {
            if (_handled) { FlowTrace.Step("Raid", "victory already handled — ignoring duplicate OnCleared."); return; }
            _handled = true;
            if (_spawner != null) _spawner.OnCleared -= HandleCleared;

            string configId = ResolveConfigId(spawner);
            FlowTrace.Step("Raid", $"VICTORY — raid '{configId}' garrison wiped. Running claim -> next-companion -> return.");

            // Victory fanfare (reuse the audio service; null-safe cross-module call).
            // PlayMusic(Victory) is the clean cross-module call available on IAudioService
            // (PlaySfx takes a raw AudioClip the Village side can't see) — swaps the driving
            // Raid brass for the victory track.
            CoreServices.Audio?.PlayMusic(DeNelle.Core.Audio.MusicTrack.Victory);

            // STEP 2 — claim the base (persist + flip ownership PLAYER-owned).
            bool newClaim = ClaimBase(configId);

            // STEP 3 — on a NEW claim, unlock the next companion (the rescue beat).
            string joined = newClaim ? UnlockNextCompanion() : null;

            // STEP 3.5 (WO-771.6) — settle the V1 SCORE (0-3 stars from the real-time
            // clear/clock) and GRANT the loot. This is the win/stars/loot half that was
            // flagged OUT (this file :34). Null-safe: with no scorer the screen falls
            // back to the star-less banner and no loot is granted.
            RaidScoring scoring = RaidScoring.Instance;
            RaidResult result = scoring != null ? scoring.Finalize(true) : null;
            ResourceCost loot = scoring != null ? scoring.LootFor(result) : default(ResourceCost);
            GrantLoot(loot);

            // STEP 3.6 - SETTLE THE ARMY. A WON raid must cost troops and pay veterancy
            // exactly as the retreat exit does. Before this, ReconcileAfterRaid had a single
            // caller (RaidDeployController.DoRetreat), so only LOSING an assault ever cost a
            // troop and AddVeterancy had ZERO callers repo-wide - winning was free. The deploy
            // HUD owns the deployed ledger (a fallen body is destroyed seconds after death, so
            // nothing here could reconstruct it), so the win routes through ITS one latched
            // reconcile. Runs BEFORE the screen so a presentation throw - which ShowVictoryScreen
            // catches - can never skip the settlement.
            ReconcileArmy(result);

            // STEP 4 — show the victory screen + route home (anti-soft-lock). The shared
            // Obsidian EndState template owns the presentation, the ONE primary action
            // (Return to Castle -> ReturnHome), the EventSystem, and the auto-dismiss
            // softlock guard (fed the same _autoReturnSeconds so the timing is unchanged).
            ShowVictoryScreen(configId, joined, result, loot);
        }

        // =====================================================================
        //  LOOT GRANT (WO-771.6) — reuse the village economy, never invent one.
        // =====================================================================

        /// <summary>
        /// Grants the raid loot into the player's economy. Prefers the canonical village
        /// <see cref="EconomyService"/> reward grant (the same path wave rewards use);
        /// falls back to the persistent <see cref="GameStateService"/> crystal/food
        /// mutators when a raid scene has no EconomyService (both target the SAME
        /// GameState.Resources wallet, so the grant lands and persists either way).
        /// </summary>
        private void GrantLoot(ResourceCost loot)
        {
            if (loot.IsZero) return;

            var eco = EconomyService.Instance;
            if (eco != null)
            {
                eco.Grant(loot);
                FlowTrace.Step("Raid", $"LOOT granted via EconomyService: +{loot.Crystals} crystals, +{loot.Food} food.");
                return;
            }

            var gs = GameStateService.Instance;
            if (gs != null)
            {
                if (loot.Crystals != 0) gs.AddCrystals(loot.Crystals);
                if (loot.Food != 0) gs.AddFood(loot.Food);
                FlowTrace.Step("Raid", $"LOOT granted via GameStateService fallback: +{loot.Crystals} crystals, +{loot.Food} food.");
            }
            else
            {
                FlowTrace.Warn("Raid", "LOOT NOT granted — no EconomyService and no GameStateService present.");
            }
        }

        // =====================================================================
        //  ARMY RECONCILE (the WIN half of the wounded / veterancy model)
        // =====================================================================

        /// <summary>
        /// Settles the army for a WON raid through the deploy HUD's single latched reconcile
        /// (RaidDeployController.ReconcileRaidEnd): every troop that was deployed but did not
        /// survive is marked wounded, and on a 3-star clear each survivor gains a veterancy
        /// rank. Called while the surviving bodies are still on the field - the victory path
        /// tears down no troops and the scene only unloads at ReturnHome - so the survivor set
        /// is real. Persists immediately so the cost and the reward cannot be lost if the
        /// player closes the app on the victory screen.
        /// </summary>
        private void ReconcileArmy(RaidResult result)
        {
            var deploy = FindAnyObjectByType<RaidDeployController>();
            if (deploy == null)
            {
                FlowTrace.Warn("Raid", "victory: no RaidDeployController in this raid scene - " +
                                       "there is no troop ledger to reconcile (nothing was deployed through the HUD).");
                return;
            }

            int stars = result != null ? result.Stars : 0;
            if (result == null)
                FlowTrace.Warn("Raid", "victory: no RaidResult (no scorer) - reconciling at 0 stars, no veterancy granted.");

            Guard.Try("Raid", "victory army reconcile", () => deploy.ReconcileRaidEnd(stars));
            GameStateService.Instance?.Save();
            FlowTrace.Step("Raid", $"army settled for the WIN (stars {stars}) and saved.");
        }

        // The raid's scene-config id: prefer the spawner's stored id (via the public
        // garrison API), else derive it from the baked scene name (RaidBase_<id>).
        private string ResolveConfigId(RaidGarrisonSpawner spawner)
        {
            // The baked scene is named RaidBase_<configId>; strip the prefix.
            string scene = gameObject.scene.name;
            const string prefix = "RaidBase_";
            if (!string.IsNullOrEmpty(scene) &&
                scene.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
                return scene.Substring(prefix.Length);
            return string.IsNullOrEmpty(scene) ? "unknown" : scene;
        }

        // =====================================================================
        //  STEP 2 — CLAIM. Persist the win + flip the live scene PLAYER-owned.
        // =====================================================================

        private bool ClaimBase(string configId)
        {
            bool newClaim = RaidClaimService.MarkClaimed(configId);

            // THE FLIP (WO-441 Phase-C payoff beat, the spine of it): the inverse of the
            // spawner's SceneOwnership.SetEnemyOwned(true) — the cleared base now reads as
            // the player's for the rest of this session (death no longer retreats as if in
            // enemy territory; build mode is permitted). Persisted ownership is in
            // RaidClaimService; this flips the LIVE runtime flag too.
            SceneOwnership.SetEnemyOwned(false);
            FlowTrace.Step("Raid", $"CLAIM — '{configId}' flipped ENEMY -> PLAYER-owned " +
                                   $"(newClaim={newClaim}). The base is yours.");

            // Persist immediately so the claim survives even if the player closes the app
            // before the return completes.
            GameStateService.Instance?.Save();
            return newClaim;
        }

        // =====================================================================
        //  STEP 3 — NEXT COMPANION. Unlock the next canon companion into the party.
        // =====================================================================

        /// <summary>
        /// Adds the NEXT canon companion (a class != the player's hero, not already in
        /// the party) to the persisted roster — the "rescue the held hero -> he joins"
        /// beat. Returns the joined companion's display name (for the banner), or null
        /// if the party is already full (all three companions recruited).
        /// </summary>
        private string UnlockNextCompanion()
        {
            var svc = GameStateService.Instance;
            if (svc == null || svc.State == null)
            {
                FlowTrace.Warn("Raid", "next-companion: no GameStateService — cannot enrol a companion.");
                return null;
            }

            HeroClass player = svc.State.HeroClass.ToNullable() ?? HeroClass.Knight;

            // The three companion classes are every class EXCEPT the player's own (the
            // player embodies their class on the field; the roster fills with the other
            // three). Canon join feel: Knight(Grom), Ranger(Sylas), Cleric(Elara),
            // Mage(Thrain) — we add the first one not yet recruited, in this stable order.
            HeroClass[] order = { HeroClass.Ranger, HeroClass.Cleric, HeroClass.Knight, HeroClass.Mage };
            foreach (var cls in order)
            {
                if (cls == player) continue;                 // never the player's own class
                if (svc.IsInParty(cls.ToString())) continue; // already recruited
                svc.AddToParty(cls.ToString());              // fires PlayerChanged -> StoryCompanionInjector spawns the body + Save
                string name = CompanionDialogue.NameFor(cls);
                FlowTrace.Step("Raid", $"NEXT COMPANION — rescued {name} ({cls}); enrolled into the party.");
                return name;
            }

            FlowTrace.Step("Raid", "next-companion: party already complete (all three companions recruited) — no new join.");
            return null;
        }

        // =====================================================================
        //  STEP 4 — RETURN. Victory banner + route home (never soft-lock).
        // =====================================================================

        private void ShowVictoryScreen(string configId, string joinedCompanionName,
                                       RaidResult result, ResourceCost loot)
        {
            try
            {
                // Route the win through the ONE shared Obsidian EndState template. Its
                // single primary action (Return to Castle) fires ReturnHome, and its
                // AutoDismissSeconds (fed _autoReturnSeconds) IS the anti-soft-lock guard
                // that previously lived in AutoReturnRoutine — same route, same timing.
                // WO-771.6: the win now carries stars + %-destruction + the loot breakdown.
                EndStateView.Show(EndStateVM.FromRaidVictory(
                    joinedCompanionName, ReturnHome, _autoReturnSeconds,
                    result != null ? result.Stars : -1,
                    result != null ? result.DestructionPercent : -1,
                    result != null ? result.ElapsedSeconds : -1f,
                    loot.Crystals, loot.Food));

                FlowTrace.Step("Raid", $"RETURN — victory screen shown for '{configId}' " +
                    (joinedCompanionName != null ? $"(+{joinedCompanionName})" : "(party already full)") +
                    "; tap or auto-dismiss routes to the castle.");
            }
            catch (System.Exception e)
            {
                // A presentation failure must NEVER strand the player — fall straight through to return.
                FlowTrace.Fail("Raid", "victory screen build threw — returning home directly: " + e.Message);
                ReturnHome();
            }
        }

        private void ReturnHome()
        {
            if (_returning) return;
            _returning = true;
            FlowTrace.Step("Raid", "RETURN -> SceneRouter.GoCastle() (loop continues, no soft-lock).");
            GameStateService.Instance?.Save();
            // Clear the runtime enemy-owned flag before we leave so the home hub never
            // inherits a stale enemy-owned read from this raid.
            SceneOwnership.SetEnemyOwned(false);
            SceneRouter.GoCastle();
        }
    }
}
