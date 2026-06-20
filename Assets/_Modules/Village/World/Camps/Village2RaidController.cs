// =============================================================================
// Village2RaidController — makes Village2 a PLAYABLE raid destination (WO-433 v1).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.World.Camps
//
// Village2 is "where they go" — the enemy stronghold the player reaches via the
// castle -> OuterWorld -> cave-portal flow (confirmed working 2026-06-20). It was
// BUILT + baked (EnemyStrongholdBuilder) with 8 spawn points + a GarrisonController
// wired, but nothing ACTIVATED it (no enemies) and nothing detected the CLEAR (no
// victory). This component closes both ends — mirrors the proven RaidVictoryController
// (RaidBase_* scenes) but for Village2 + its GarrisonController:
//
//   SELF-INSTALL  — a RuntimeInitialize hook adds ONE controller to the Village2 scene
//                   (idempotent), like RaidVictoryController does for RaidBase_*.
//   ACTIVATE      — find the scene's GarrisonController + Activate() it AFTER the
//                   navmesh is live, so the garrison (orc-berserker/orc-shaman/troll/
//                   hollow-warrior, per garrison-recipes.json "village2_stronghold")
//                   spawns + the watchtower turrets arm. Idempotent.
//   VICTORY       — subscribe to GarrisonController.OnCleared (last defender dies).
//   CLAIM/COMPANION/RETURN — reuse the EXACT raid-victory services: claim the base
//                   (RaidClaimService + flip PLAYER-owned), unlock the next companion,
//                   victory banner + "Return to Castle" (+ auto-return anti-soft-lock).
//
// v1 WIN-CONDITION = CLEAR THE GARRISON (the spec default; owner may later switch to
// kill-boss / destroy-altar). Boss is whatever the recipe authors (currently none);
// reward = claim + next companion (the documented default). Code-built uGUI, ASCII
// runtime strings, Elarion canon. Headless-verified by the autopilot Village2 phase.
// =============================================================================

using System.Collections;
using UnityEngine;
using DeNelle.Core;
using DeNelle.Core.State;
using DeNelle.Core.UI;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village.World.Camps
{
    /// <summary>
    /// Activates the Village2 garrison on scene load and runs the raid-victory flow
    /// (claim -> next companion -> return home) when the garrison is cleared.
    /// Self-installs into the <c>Village2</c> scene. Mirrors RaidVictoryController.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Village2RaidController : MonoBehaviour
    {
        private const string SceneName = "Village2";
        private const string ConfigId  = "Village2";

        [Tooltip("Seconds after victory before the hero auto-returns to the castle if the " +
                 "player never taps the button (anti-soft-lock safety net).")]
        [SerializeField] private float _autoReturnSeconds = 12f;

        private GarrisonController _garrison;
        private GameObject _ui;
        private bool _handled;     // victory handled once (guards a double OnCleared)
        private bool _returning;   // a return is already in flight

        // =====================================================================
        //  Self-install — one controller in the Village2 scene
        // =====================================================================

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
            if (!string.Equals(sceneName, SceneName, System.StringComparison.OrdinalIgnoreCase)) return;
            if (FindAnyObjectByType<Village2RaidController>() != null) return;

            var go = new GameObject("Village2RaidController");
            go.AddComponent<Village2RaidController>();
            FlowTrace.Step("Raid", $"Village2RaidController self-installed in '{sceneName}'.");
        }

        // =====================================================================
        //  Bind — find the garrison, ACTIVATE it (spawn enemies), subscribe to clear.
        //  The garrison is wired by EnemyStrongholdBuilder but never auto-activated
        //  (activateOnStart=false); we activate it here, once the navmesh is live.
        // =====================================================================

        private void Start()
        {
            StartCoroutine(BindRoutine());
        }

        private void OnDestroy()
        {
            if (_garrison != null) _garrison.OnCleared -= HandleCleared;
        }

        private IEnumerator BindRoutine()
        {
            // The stronghold root + its GarrisonController exist at scene load; give a
            // few frames for additive load + navmesh to settle before we spawn.
            for (int i = 0; i < 10 && _garrison == null; i++)
            {
                _garrison = FindGarrisonInThisScene();
                if (_garrison != null) break;
                yield return null;
            }

            if (_garrison == null)
            {
                FlowTrace.Warn("Raid", "Village2RaidController: no GarrisonController found in Village2 — " +
                                       "cannot populate the stronghold (no enemies, no victory). Leaving the return seam as the only out.");
                yield break;
            }

            // ACTIVATE — spawn the garrison + arm the turrets (idempotent; a no-op if the
            // scene/builder already activated it).
            _garrison.Activate();
            FlowTrace.Step("Raid", $"Village2 garrison ACTIVATED — {_garrison.AliveCount}/{_garrison.TotalGarrison} defender(s) live.");

            // If the garrison was empty / already cleared (e.g. no spawn points), handle the
            // clear immediately; otherwise subscribe for the last-defender-dies event.
            if (_garrison.Cleared)
            {
                FlowTrace.Step("Raid", "Village2 garrison was ALREADY cleared on bind — running victory immediately.");
                HandleCleared(_garrison);
            }
            else
            {
                _garrison.OnCleared -= HandleCleared;
                _garrison.OnCleared += HandleCleared;
                FlowTrace.Step("Raid", $"Village2RaidController bound to OnCleared (garrison of {_garrison.TotalGarrison} defender(s)).");
            }
        }

        // Find the GarrisonController that lives in THIS controller's scene (Village2),
        // not a garrison from any other additively-loaded scene.
        private GarrisonController FindGarrisonInThisScene()
        {
            var all = FindObjectsByType<GarrisonController>(FindObjectsSortMode.None);
            if (all == null) return null;
            var myScene = gameObject.scene;
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null && all[i].gameObject.scene == myScene) return all[i];
            // Fallback: any garrison (single-garrison project) if the scene match misses.
            return all.Length > 0 ? all[0] : null;
        }

        // =====================================================================
        //  VICTORY — last defender died (or the garrison was empty).
        // =====================================================================

        private void HandleCleared(GarrisonController garrison)
        {
            if (_handled) { FlowTrace.Step("Raid", "Village2 victory already handled — ignoring duplicate OnCleared."); return; }
            _handled = true;
            if (_garrison != null) _garrison.OnCleared -= HandleCleared;

            FlowTrace.Step("Raid", $"VICTORY — Village2 stronghold garrison wiped. Running claim -> next-companion -> return.");

            CoreServices.Audio?.PlayMusic(DeNelle.Core.Audio.MusicTrack.Victory);

            bool newClaim = ClaimBase();
            string joined = newClaim ? UnlockNextCompanion() : null;

            BuildVictoryBanner(joined);
            StartCoroutine(AutoReturnRoutine());
        }

        // =====================================================================
        //  CLAIM — persist the win + flip the live scene PLAYER-owned.
        // =====================================================================

        private bool ClaimBase()
        {
            bool newClaim = RaidClaimService.MarkClaimed(ConfigId);
            SceneOwnership.SetEnemyOwned(false);
            FlowTrace.Step("Raid", $"CLAIM — '{ConfigId}' flipped ENEMY -> PLAYER-owned (newClaim={newClaim}). The stronghold is yours.");
            GameStateService.Instance?.Save();
            return newClaim;
        }

        // =====================================================================
        //  NEXT COMPANION — unlock the next canon companion into the party.
        // =====================================================================

        private string UnlockNextCompanion()
        {
            var svc = GameStateService.Instance;
            if (svc == null || svc.State == null)
            {
                FlowTrace.Warn("Raid", "next-companion: no GameStateService — cannot enrol a companion.");
                return null;
            }

            HeroClass player = svc.State.HeroClass.ToNullable() ?? HeroClass.Knight;
            HeroClass[] order = { HeroClass.Ranger, HeroClass.Cleric, HeroClass.Knight, HeroClass.Mage };
            foreach (var cls in order)
            {
                if (cls == player) continue;
                if (svc.IsInParty(cls.ToString())) continue;
                svc.AddToParty(cls.ToString());
                string name = CompanionDialogue.NameFor(cls);
                FlowTrace.Step("Raid", $"NEXT COMPANION — rescued {name} ({cls}); enrolled into the party.");
                return name;
            }

            FlowTrace.Step("Raid", "next-companion: party already complete — no new join.");
            return null;
        }

        // =====================================================================
        //  RETURN — victory banner + route home (never soft-lock).
        // =====================================================================

        private void BuildVictoryBanner(string joinedCompanionName)
        {
            try
            {
                if (_ui != null) Destroy(_ui);
                _ui = ElarionUiKit.BuildModalCanvas("Village2VictoryBanner", 32000);
                ElarionUiKit.Scrim(_ui.transform, onTapClose: null);

                var panel = ElarionUiKit.Panel(_ui.transform, new Vector2(0.22f, 0.34f), new Vector2(0.78f, 0.70f), deep: true);

                ElarionUiKit.Header(panel.transform, "STRONGHOLD CLEARED", x0: 0.06f, x1: 0.94f, y0: 0.74f, y1: 0.93f);

                string body = joinedCompanionName != null
                    ? $"The enemy stronghold is CLAIMED — it is yours now.\n\n{joinedCompanionName} joins your party."
                    : "The enemy stronghold is CLAIMED — it is yours now.";
                ElarionUiKit.Label(panel.transform, body, 0.10f, 0.40f,
                    ElarionUi.Parchment, ElarionUi.FontBody, TMPro.TextAlignmentOptions.Center, 0.06f, 0.94f);

                ElarionUiKit.Button(panel.transform, "Return to Castle", ElarionUiKit.ButtonKind.Gold,
                    new Vector2(0.28f, 0.10f), new Vector2(0.72f, 0.28f), ReturnHome);

                FlowTrace.Step("Raid", "RETURN — Village2 victory banner shown" +
                    (joinedCompanionName != null ? $" (+{joinedCompanionName})" : "") + "; tap or auto-return routes to the castle.");
            }
            catch (System.Exception e)
            {
                FlowTrace.Fail("Raid", "Village2 victory banner build threw — returning home directly: " + e.Message);
                ReturnHome();
            }
        }

        private IEnumerator AutoReturnRoutine()
        {
            float t = Mathf.Max(2f, _autoReturnSeconds);
            yield return new WaitForSeconds(t);
            if (!_returning)
            {
                FlowTrace.Step("Raid", "Village2 auto-return timer elapsed — routing home (anti-soft-lock).");
                ReturnHome();
            }
        }

        private void ReturnHome()
        {
            if (_returning) return;
            _returning = true;
            FlowTrace.Step("Raid", "RETURN -> SceneRouter.GoCastle() (loop continues, no soft-lock).");
            GameStateService.Instance?.Save();
            SceneOwnership.SetEnemyOwned(false);
            SceneRouter.GoCastle();
        }
    }
}
