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
        private RaidSpire _spire;  // THE OBJECTIVE (owner concept 2026-08-02) - razing it wins
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
            if (_spire != null) _spire.OnDestroyedEvent -= HandleSpireRazed;
        }

        private IEnumerator BindRoutine()
        {
            // ---- THE OBJECTIVE ------------------------------------------------
            // Owner concept 2026-08-02: a raid is won by RAZING THE CENTRAL SPIRE, not by
            // counting corpses. The spire is baked into the scene so it exists at load.
            // A scene with no spire (a legacy bake) keeps the old garrison-wipe rule below,
            // so nothing that already shipped becomes unwinnable.
            _spire = RaidSpire.Active != null ? RaidSpire.Active : FindAnyObjectByType<RaidSpire>();
            if (_spire != null)
            {
                if (_spire.IsDestroyed)
                {
                    FlowTrace.Step("Raid", "RaidVictoryController: spire was ALREADY razed on bind — running victory immediately.");
                    HandleVictory("spire razed (already down at bind)");
                    yield break;
                }
                _spire.OnDestroyedEvent -= HandleSpireRazed;
                _spire.OnDestroyedEvent += HandleSpireRazed;
                FlowTrace.Step("Raid", $"RaidVictoryController bound to the OBJECTIVE: spire '{_spire.name}' " +
                                       $"({_spire.MaxHp:0} HP). Razing it WINS the raid.");
            }
            else
            {
                FlowTrace.Warn("Raid", "RaidVictoryController: this raid scene has NO RaidSpire — falling back to " +
                                       "the legacy garrison-wipe win condition. Re-bake with " +
                                       "RaidBaseGenerator.BuildAllRaidScenes to get the spire objective.");
            }

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
        //  STEP 1 — VICTORY. THE OBJECTIVE: the central spire falls.
        //  (Legacy fallback: the last defender died, in a scene with no spire.)
        // =====================================================================

        /// <summary>The spire was razed — this is the win, whatever the garrison is doing.</summary>
        private void HandleSpireRazed(RaidSpire spire)
        {
            if (spire != null) spire.OnDestroyedEvent -= HandleSpireRazed;
            HandleVictory("SPIRE RAZED");
        }

        /// <summary>
        /// The garrison was wiped. With a spire objective present this is a MILESTONE, not a
        /// win — the owner's concept moved the win condition off corpse-count. Only a
        /// spire-less (legacy) raid base still wins here, so old bakes never soft-lock.
        /// </summary>
        private void HandleCleared(RaidGarrisonSpawner spawner)
        {
            if (_spawner != null) _spawner.OnCleared -= HandleCleared;

            if (_spire != null && !_spire.IsDestroyed)
            {
                FlowTrace.Step("Raid", "garrison wiped, but the SPIRE still stands — the raid is not over. " +
                                       $"Objective at {_spire.HpFraction:P0} HP. Raze it to win.");
                return;
            }

            HandleVictory(_spire != null ? "garrison wiped after the spire fell" : "garrison wiped (legacy, no spire)");
        }

        private void HandleVictory(string reason)
        {
            if (_handled) { FlowTrace.Step("Raid", "victory already handled — ignoring duplicate signal."); return; }
            _handled = true;
            if (_spawner != null) _spawner.OnCleared -= HandleCleared;
            if (_spire != null) _spire.OnDestroyedEvent -= HandleSpireRazed;

            RaidGarrisonSpawner spawner = _spawner;
            string configId = ResolveConfigId(spawner);
            FlowTrace.Step("Raid", $"VICTORY — raid '{configId}' won ({reason}). Running claim -> next-companion -> return.");

            // Victory fanfare (reuse the audio service; null-safe cross-module call).
            // PlayMusic(Victory) is the clean cross-module call available on IAudioService
            // (PlaySfx takes a raw AudioClip the Village side can't see) — swaps the driving
            // Raid brass for the victory track.
            CoreServices.Audio?.PlayMusic(DeNelle.Core.Audio.MusicTrack.Victory);

            // STEP 1.5 — IS THIS A REPEAT CLEAR? This read MUST happen BEFORE ClaimBase,
            // which flips the persisted flag: query it afterwards and every clear reads as
            // a repeat. The answer feeds the first-clear loot gate at STEP 3.5.
            bool repeatClear = RaidClaimService.IsClaimed(configId);

            // STEP 2 — claim the base (persist + flip ownership PLAYER-owned).
            bool newClaim = ClaimBase(configId);

            // STEP 2.5 (WO-728) — OPEN THE COOLDOWN. A clear is what starts the wait; this
            // runs on EVERY clear, first or repeat, because the entry gate is a different
            // question from the loot gate (RaidClaimService answers "have I ever taken this
            // camp"; the cooldown answers "may I raid it again yet"). Stamped from the
            // SERVER-ANCHORED clock inside the service — never DateTime.UtcNow here.
            // Placed before the presentation so a screen throw can never skip the wait, which
            // is the same reason STEP 3.6 settles the army before ShowVictoryScreen.
            RaidCooldownService.BeginAfterClear(configId);

            // STEP 3 — on a NEW claim, unlock the next companion (the rescue beat).
            string joined = newClaim ? UnlockNextCompanion() : null;

            // STEP 3.5 (WO-771.6) — settle the V1 SCORE (0-3 stars from the real-time
            // clear/clock) and GRANT the loot. This is the win/stars/loot half that was
            // flagged OUT (this file :34). Null-safe: with no scorer the screen falls
            // back to the star-less banner and no loot is granted.
            RaidScoring scoring = RaidScoring.Instance;
            RaidResult result = scoring != null ? scoring.Finalize(true) : null;
            ResourceCost loot = scoring != null ? scoring.LootFor(result) : default(ResourceCost);
            loot = ApplyFirstClearGate(loot, repeatClear, configId);
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
        ///
        /// <para>WO-978 — THIS TRACE REPORTS THE MEASURED CREDIT, NOT THE REQUEST. It used to
        /// print <c>loot.Crystals</c>/<c>loot.Food</c> — the numbers we ASKED for — as though they
        /// had landed. <c>EconomyService.Grant</c> returns <c>void</c> and routes to the
        /// <b>clampable</b> <c>BankGrantKind.EarnedIncome</c> kind (EconomyService.cs :363 → :396),
        /// so a town bank at its storage ceiling credits LESS than the raid awarded — possibly
        /// zero — while the old line still read "+500 crystals". That is exactly the shape of
        /// "I did the raid and got nothing" being unfalsifiable from a capture.
        /// <b>EconomyService itself is honest</b> (its own trace at :416 prints the post-clamp
        /// amount and the resulting total) — the bug was entirely caller-side, and it is fixed
        /// here, not there. Since the API hands back nothing, we take the only honest reading
        /// available: the wallet totals BEFORE and AFTER, and we log the DELTA — a measured
        /// quantity rather than a derived one.</para>
        /// </summary>
        // WO-978 follow-up: the MEASURED credit, kept so the VICTORY SCREEN can show what the
        // player actually received. Fixing only the log was half the ticket — at a capped town
        // bank the trace read "credited 0/500" while the screen still advertised "+500 crystals",
        // which is the same "I raided and got nothing" unfalsifiability one layer up. The sibling
        // ChallengeOutpostVictoryController already does this; the raid path did not.
        private int  _crystalsCredited;
        private int  _foodCredited;
        private bool _rewardShort;

        /// <summary>
        /// THE FIRST-CLEAR GATE (defect sweep 2026-08-15). A base pays its settled loot on
        /// the clear that CLAIMS it; a re-clear of an already-claimed base is scaled by
        /// <see cref="RaidClaimService.RepeatClearLootMultiplier"/> (0 by default = pays
        /// only reduced ordinary resources and never premium crystals).
        ///
        /// <para>THE HOLE THIS CLOSES: loot was never gated on <c>newClaim</c> at all. The
        /// claim set was written and never read, so re-entering a cleared base and razing it
        /// again paid the FULL settled payout, every time, forever - and the raid catalog's
        /// Extreme tier carries rewardMultiplier 2.2, making the most lucrative base in the
        /// game an unbounded resource faucet. The companion unlock beside it was already
        /// gated on newClaim; the resources simply were not.</para>
        ///
        /// <para>Reports on BOTH branches: a player who re-clears a base and receives nothing
        /// must be able to see WHY in a capture, and a first clear must be able to prove it
        /// paid in full. Never silent.</para>
        /// </summary>
        private static ResourceCost ApplyFirstClearGate(ResourceCost loot, bool repeatClear, string configId)
        {
            if (!repeatClear)
            {
                if (!loot.IsZero)
                    FlowTrace.Step("Raid", $"FIRST-CLEAR gate: '{configId}' was unclaimed - paying the settled " +
                                           $"loot IN FULL ({Describe(loot)}).");
                return loot;
            }

            ResourceCost scaled = RaidClaimService.ScaleLootForClear(loot, true);
            FlowTrace.Warn("Raid",
                $"REPEAT CLEAR of '{configId}' (already claimed) - loot scaled by " +
                $"x{RaidClaimService.RepeatClearLootMultiplier:0.##}: {Describe(loot)} -> {Describe(scaled)}. " +
                "A claimed base never pays premium crystals again; the reduced ordinary-resource " +
                "payout keeps practice runs useful without creating a crystal farm.");
            return scaled;
        }

        private void GrantLoot(ResourceCost loot)
        {
            if (loot.IsZero) return;

            _crystalsCredited = 0; _foodCredited = 0; _rewardShort = false;

            var eco = EconomyService.Instance;
            if (eco != null)
            {
                // The wallet properties read straight through to the single GameState-backed
                // store (WO-842), so before/after is a real measurement of what was credited.
                int w0 = eco.Wood, f0 = eco.Food, i0 = eco.Iron, c0 = eco.Crystals, g0 = eco.Coins;
                eco.Grant(loot);
                int dw = eco.Wood - w0, df = eco.Food - f0, di = eco.Iron - i0,
                    dc = eco.Crystals - c0, dg = eco.Coins - g0;
                _crystalsCredited = dc; _foodCredited = df;
                _rewardShort = dw < loot.Wood || df < loot.Food || di < loot.Iron
                            || dc < loot.Crystals || dg < loot.Coins;
                LogCredit("EconomyService", loot, dw, df, di, dc, dg);
                return;
            }

            var gs = GameStateService.Instance;
            var state = gs != null ? gs.State : null;
            if (gs != null && state != null)
            {
                // AddCrystals/AddFood are void too — measure GameState.Resources either side.
                // Note this fallback route has NO wood/iron/gold mover at all, so any of those
                // axes in the loot are DROPPED; LogCredit will say so instead of hiding it.
                int c0 = state.Resources.Crystals, f0 = state.Resources.Food;
                if (loot.Crystals != 0) gs.AddCrystals(loot.Crystals);
                if (loot.Food != 0) gs.AddFood(loot.Food);
                int dcF = state.Resources.Crystals - c0, dfF = state.Resources.Food - f0;
                _crystalsCredited = dcF; _foodCredited = dfF;
                // This route has no wood/iron/gold mover, so those axes are dropped outright —
                // that counts as short for the player-facing caveat, not just for the log.
                _rewardShort = dcF < loot.Crystals || dfF < loot.Food
                            || loot.Wood != 0 || loot.Iron != 0 || loot.Coins != 0;
                LogCredit("GameStateService fallback", loot,
                          0, dfF, 0, dcF, 0);
            }
            else if (gs != null)
            {
                FlowTrace.Fail("Raid", "LOOT LOST — GameStateService is present but has no loaded State; " +
                                       $"the win awarded {Describe(loot)} and NONE of it was credited.");
            }
            else
            {
                FlowTrace.Fail("Raid", "LOOT LOST — no EconomyService and no GameStateService present; " +
                                       $"the win awarded {Describe(loot)} and NONE of it was credited.");
            }
        }

        /// <summary>
        /// WO-978 — the one place a raid loot grant is reported, always as
        /// <c>credited/requested</c> per axis. A shortfall is a <see cref="FlowTrace.Warn"/>
        /// naming both numbers and the consequence, never a routine Step, so a capture SHOWS
        /// the clamp instead of agreeing with the payout that never happened.
        /// </summary>
        private static void LogCredit(string route, ResourceCost requested,
                                      int dWood, int dFood, int dIron, int dCrystals, int dCoins)
        {
            string measured =
                $"wood {dWood}/{requested.Wood}, food {dFood}/{requested.Food}, iron {dIron}/{requested.Iron}, " +
                $"crystals {dCrystals}/{requested.Crystals}, gold {dCoins}/{requested.Coins} (credited/requested)";

            bool shortfall = dWood     < requested.Wood
                          || dFood     < requested.Food
                          || dIron     < requested.Iron
                          || dCrystals < requested.Crystals
                          || dCoins    < requested.Coins;

            if (shortfall)
                FlowTrace.Warn("Raid",
                    $"LOOT SHORT via {route} — the wallet took LESS than the raid awarded: {measured}. " +
                    "Raid loot is EarnedIncome, which TownBankCapacity clamps against the town storage " +
                    "ceiling — the player earned this and did not receive it. (WO-978: what should happen " +
                    "at cap is an OPEN owner question; this line only stops the log from claiming payment.)");
            else
                FlowTrace.Step("Raid", $"LOOT credited via {route}: {measured}.");
        }

        /// <summary>Human-readable requested loot, for the never-credited failure lines.</summary>
        private static string Describe(ResourceCost loot)
            => $"requested wood {loot.Wood}, food {loot.Food}, iron {loot.Iron}, " +
               $"crystals {loot.Crystals}, gold {loot.Coins}";

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
                // WO-978 follow-up: show the CREDITED amounts, never the requested ones. At a
                // capped bank these differ, and the screen is what the player believes.
                var vm = EndStateVM.FromRaidVictory(
                    joinedCompanionName, ReturnHome, _autoReturnSeconds,
                    result != null ? result.Stars : -1,
                    result != null ? result.DestructionPercent : -1,
                    result != null ? result.ElapsedSeconds : -1f,
                    _crystalsCredited, _foodCredited);

                if (_rewardShort && vm != null)
                {
                    // WORDS, never colour alone — the owner is red/green colourblind, so a dimmed
                    // number would carry no information at all. Same sentence the outpost uses.
                    vm.Subtitle = string.IsNullOrEmpty(vm.Subtitle)
                        ? "Some of the reward could not be paid out."
                        : vm.Subtitle + " Some of the reward could not be paid out.";
                }

                EndStateView.Show(vm);

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
