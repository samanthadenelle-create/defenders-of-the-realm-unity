// =============================================================================
// DungeonStubEncounter — fire an ATB battle from a stub dungeon scene that has
// NO DungeonController. Used by the secondary dungeons built via the stub /
// FolksGranary builders, where the full EncounterTrigger (which needs a
// DungeonController + DungeonRuntimeState + a layout-hydrated roster) is inert.
// -----------------------------------------------------------------------------
// The fully-authored Healer's Cottage routes encounters the rich way:
//   DungeonController.HydrateEncounters -> EncounterTrigger.ConfigureScripted
//   -> EncounterTrigger.TickScripted -> SceneRouter.GoBattle(ATBBattle).
//
// The stub scenes (e.g. Dungeon_FolksGranary) ship NO DungeonController, so an
// EncounterTrigger's Update() returns on its first line forever (its
// _runtimeState is null and RunActive is never set) — stepping on the trigger
// did NOTHING and the 2D ATB battle "never loaded." This component is the stub
// analog of DungeonStubReturn: it is fully self-sufficient, needs no controller
// and no runtime-state SO, and launches the battle on hero proximity (with an
// F-key fallback, same as DungeonStubReturn).
//
// The battle returns to THIS dungeon scene (ReturnScene), so a stub encounter
// is a true round-trip: fight -> back to the stub dungeon at the same spot.
// =============================================================================

using System.Collections;
using Cysharp.Threading.Tasks;
using DeNelle.Core;
using DeNelle.Core.Diagnostics;
using DeNelle.Village.Arena;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Dungeons
{
    /// <summary>
    /// A controller-free encounter zone for stub dungeon scenes. Launches the
    /// ATB battle through <see cref="SceneRouter.GoBattle"/> the first time the
    /// hero enters its proximity radius (or presses F nearby), then returns to
    /// the scene named by <see cref="_returnScene"/>. Fires once per scene load.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DungeonStubEncounter : MonoBehaviour
    {
        [Header("Proximity")]
        [Tooltip("World radius around this object that fires the encounter when the hero enters.")]
        [SerializeField] private float _triggerRadius = 3f;

        [Tooltip("Name of the hero placeholder GameObject to watch (DungeonStubReturn convention).")]
        [SerializeField] private string _heroObjectName = "DungeonHeroPlaceholder";

        [Header("Battle handoff")]
        [Tooltip("Enemy ids handed to the ATB battle (3D-layer ids; the battle's fallback enemy is used until the breach mapper lands).")]
        [SerializeField] private string[] _enemyTypes = new[] { "hollow_villager_a" };

        [Tooltip("Scene to return to once the battle resolves — this stub dungeon scene.")]
        [SerializeField] private string _returnScene = SceneRouter.DungeonFolksGranary;

        private bool _fired;
        private float _proxCheck;

        // Becomes true once the hero has been observed clearly OUTSIDE the trigger
        // radius. The encounter will not fire until then. Without this, the plain
        // _fired bool reset on every scene load and the battle's round-trip drops
        // the hero back onto the dungeon spawn (which sits on/near this pad) — so
        // the encounter re-fired the instant the player reappeared, looping them
        // into a fresh battle forever ("stuck in an endless loop"). Arming on exit
        // means a returning hero must step off the pad before it can trigger again.
        private bool _armed;

        // ── #18 round-trip position restore ──────────────────────────────────
        // The stub dungeon is its own scene; going to the ATB battle unloads it and
        // returning RELOADS it, so the baked DungeonHeroPlaceholder respawns at the
        // entrance — the owner's "resets hero to dungeon start." Snapshot the hero's
        // pre-battle position when the encounter fires (static so it survives the ATB
        // scene round-trip) and restore it on the reload. Cleared after one restore.
        private static bool    s_hasReturn;
        private static string  s_returnScene;
        private static Vector3 s_returnPos;

        /// <summary>Configures the stub encounter at run time (optional — the scene builder pre-wires the fields).</summary>
        public void Configure(string[] enemyTypes, string returnScene, float triggerRadius)
        {
            if (enemyTypes != null) _enemyTypes = enemyTypes;
            if (!string.IsNullOrEmpty(returnScene)) _returnScene = returnScene;
            if (triggerRadius > 0f) _triggerRadius = triggerRadius;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_fired || !_armed) return;
            if (!LooksLikeHero(other.gameObject)) return;
            Fire();
        }

        private void Update()
        {
            if (_fired) return;

            // Proximity poll (cheap, throttled) — the visual trigger disc on the
            // pad is colliderless, so OnTriggerEnter only fires if the hero's own
            // CharacterController crosses a trigger volume. The poll guarantees the
            // encounter still launches when the hero simply walks onto the zone.
            _proxCheck += Time.deltaTime;
            if (_proxCheck < 0.1f) return;
            _proxCheck = 0f;

            var hero = ResolveHero();
            if (hero == null) return;

            float sqr = (hero.transform.position - transform.position).sqrMagnitude;
            bool inside = sqr <= _triggerRadius * _triggerRadius;

            // Arm only after the hero has been seen outside the zone, so a battle
            // round-trip that returns the hero onto the pad does not instantly
            // re-fire (see _armed). The F-key force still works once armed.
            if (!_armed)
            {
                if (!inside) _armed = true;
                return;
            }

            // The 'F' force-key was REMOVED — the proximity path is the sole entry:
            // the encounter fires the moment the (armed) hero is inside the radius.
            if (inside)
            {
                Fire();
            }
        }

        private void Fire()
        {
            if (_fired) return;
            _fired = true;

            // #18: snapshot where the hero is so the battle round-trip drops them back HERE, not at
            // the dungeon entrance. Restored on scene reload (Start). This is the LEGACY ATB path's
            // return mechanism (the ATB scene unloads + RELOADS this dungeon). On the real-time arena
            // path (WO-591, flag ON) there is NO scene round-trip — BattleArena.BeginEncounter warps
            // the hero back to EncounterParams.ReturnPosition IN this same scene — so the snapshot/
            // Start-restore would be a redundant SECOND warp. Guard it behind the flag-off path.
            var heroNow = ResolveHero();
            if (!FeatureFlags.DungeonRealtimeBattle && heroNow != null)
            {
                s_returnPos   = heroNow.transform.position;
                s_returnScene = string.IsNullOrEmpty(_returnScene) ? SceneRouter.Village : _returnScene;
                s_hasReturn   = true;
            }

            Debug.Log("[DungeonStubEncounter] Encounter zone entered — launching battle.");
            LaunchBattle().Forget();
        }

        // #18: on (re)load of the stub dungeon after a battle, move the placeholder
        // hero back to where it triggered the encounter instead of the baked
        // entrance. Best-effort: a no-op if the snapshot is for another scene or the
        // hero can't be found.
        private void Start()
        {
            if (s_hasReturn && SceneManager.GetActiveScene().name == s_returnScene)
                StartCoroutine(RestoreHeroPosition());
        }

        private IEnumerator RestoreHeroPosition()
        {
            // Let the baked placeholder + any controller finish their own init first.
            yield return null;
            yield return new WaitForSeconds(0.2f);

            var hero = ResolveHero();
            if (hero != null)
            {
                // A CharacterController fights a raw position set — toggle it off
                // for the teleport (no-op when the placeholder has none).
                var cc = hero.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                hero.transform.position = s_returnPos;
                if (cc != null) cc.enabled = true;

                // The hero is now standing ON the pad again. Re-disarm so the
                // encounter can't instantly re-fire (it only re-arms once the hero
                // has been seen OUTSIDE the radius) — keeps the #18 restore and the
                // anti-loop guard consistent.
                _armed = false;

                Debug.Log($"[DungeonStubEncounter] #18 restored hero to pre-encounter position {s_returnPos}.");
            }

            s_hasReturn = false; // one-shot
        }

        private async UniTask LaunchBattle()
        {
            string returnScene = string.IsNullOrEmpty(_returnScene) ? SceneRouter.Village : _returnScene;

            // WO-591 (owner 2026-06-29 "we should retire ATB"): route the dungeon fight to the REAL-TIME
            // isolated BattleArena instead of the flat ATBBattle scene. The arena stages additively at a
            // far offset, warps the hero in, and on victory warps the hero back to ReturnPosition/Yaw IN
            // this same dungeon scene (no scene round-trip). Reversible: ff.dungeonrealtime = 0 restores
            // the legacy ATB GoBattle path below.
            if (FeatureFlags.DungeonRealtimeBattle)
            {
                var hero = ResolveHero();
                Vector3 returnPos = hero != null ? hero.transform.position : Vector3.zero;
                float   returnYaw = hero != null ? hero.transform.eulerAngles.y : 0f;
                var ep = new EncounterParams
                {
                    EnemyIds        = _enemyTypes ?? System.Array.Empty<string>(),
                    Threat          = 1,             // stub encounters are baseline-threat
                    BackdropContext = "cavern",      // underground dungeon look
                    ReturnScene     = returnScene,
                    ReturnPosition  = returnPos,
                    ReturnYaw       = returnYaw,
                };
                FlowTrace.Step("Dungeon",
                    "DungeonStubEncounter.LaunchBattle: ff.dungeonrealtime ON -> real-time BattleArena " +
                    "(enemies=[" + string.Join(",", ep.EnemyIds) + "] return='" + returnScene + "').");
                BattleArena.Instance.BeginEncounter(ep);
                return;
            }

            FlowTrace.Step("Dungeon", "DungeonStubEncounter.LaunchBattle: ff.dungeonrealtime OFF -> legacy ATB GoBattle.");
            var p = new BattleParams
            {
                Wave = 0, // a dungeon encounter is not a village wave
                BreachedIds = _enemyTypes ?? System.Array.Empty<string>(),
                ParticipatingPetIds = System.Array.Empty<string>(),
                ReturnScene = returnScene,
            };
            await SceneRouter.GoBattle(p);
        }

        private GameObject ResolveHero()
        {
            if (!string.IsNullOrEmpty(_heroObjectName))
            {
                var named = GameObject.Find(_heroObjectName);
                if (named != null) return named;
            }
            // Fallbacks — any object that reads as the hero rig.
            var heroByName = GameObject.Find("Keeper");
            return heroByName;
        }

        private static bool LooksLikeHero(GameObject go)
        {
            if (go == null) return false;
            string n = go.name;
            return n.Contains("Hero") || n.Contains("hero") ||
                   n.Contains("Player") || n.Contains("Keeper");
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.85f, 0.3f, 0.2f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, _triggerRadius);
        }
    }
}
