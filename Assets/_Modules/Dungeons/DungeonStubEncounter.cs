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

using Cysharp.Threading.Tasks;
using DeNelle.Core;
using UnityEngine;

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

        /// <summary>Configures the stub encounter at run time (optional — the scene builder pre-wires the fields).</summary>
        public void Configure(string[] enemyTypes, string returnScene, float triggerRadius)
        {
            if (enemyTypes != null) _enemyTypes = enemyTypes;
            if (!string.IsNullOrEmpty(returnScene)) _returnScene = returnScene;
            if (triggerRadius > 0f) _triggerRadius = triggerRadius;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_fired) return;
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
            // F-key fallback mirrors DungeonStubReturn so the owner can force it.
            if (inside || (Input.GetKey(KeyCode.F) && sqr <= (_triggerRadius + 1f) * (_triggerRadius + 1f)))
                Fire();
        }

        private void Fire()
        {
            if (_fired) return;
            _fired = true;
            Debug.Log("[DungeonStubEncounter] Encounter zone entered — launching ATB battle.");
            LaunchBattle().Forget();
        }

        private async UniTask LaunchBattle()
        {
            var p = new BattleParams
            {
                Wave = 0, // a dungeon encounter is not a village wave
                BreachedIds = _enemyTypes ?? System.Array.Empty<string>(),
                ParticipatingPetIds = System.Array.Empty<string>(),
                ReturnScene = string.IsNullOrEmpty(_returnScene) ? SceneRouter.Village : _returnScene,
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
