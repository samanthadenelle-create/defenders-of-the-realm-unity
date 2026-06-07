// =============================================================================
// EncounterTrigger (Sandbox) — a deep-dungeon random/boss encounter zone that
// launches OUR real ATB battle.
// -----------------------------------------------------------------------------
// Grok assumed an API like `BattleManager.Instance.StartATBBattle(enemyGroup)`.
// That does NOT exist in this project. The REAL ATB entry-point is:
//
//     DeNelle.Core.SceneRouter.GoBattle(BattleParams p)
//         -> stashes p on SceneRouter.PendingBattle
//         -> loads the "ATBBattle" scene (with fade)
//         -> BattleController.Start() reads SceneRouter.PendingBattle, builds the
//            roster from BattleParams.BreachedIds (mapped to engine ENEMY_DEFS),
//            and runs the turn-based fight; on resolve it returns to
//            BattleParams.ReturnScene.
//
// So an "encounter enemy set" is a `string[]` of enemy ids carried on
// BattleParams.BreachedIds. The ids are mapped to ATB engine archetypes by
// BattleController.MapToEngineDef (e.g. "skeleton"/"bruiser"/"necromancer", or
// village family ids like "hollow-warrior"). Wave == 0 marks a dungeon (not a
// village breach) so Flee stays available and no Heart damage is applied.
//
// This is the SAME canonical route the production dungeon uses
// (Assets/_Modules/Dungeons/EncounterTrigger.cs + DungeonStubEncounter.cs).
//
// WHAT IS FULLY WIRED HERE:
//   • trigger collider + Player tag check + chance roll + one-shot guard
//   • the real ATB launch via SceneRouter.GoBattle (fire-and-forget UniTask)
//   • an EncounterEnemySet (depth/level + family id -> scaled enemy id list)
//
// WHAT THE FULL DUNGEON<->ATB<->RETURN TRANSITION STILL NEEDS (flagged):
//   • a return scene: this sandbox deep dungeon is generated at runtime, not a
//     real registered scene, so ReturnScene defaults to the village. A real
//     dungeon scene name (SceneRouter.Dungeon...) + a DungeonRuntimeState handoff
//     (BeginEncounterHandoff / ResumeAfterEncounter) is what the production
//     EncounterTrigger uses to round-trip hero HP/mana + restore position. Wiring
//     that here requires this generator to be promoted into a real DungeonScene
//     + a DungeonController. Until then this fires the battle and lands back in
//     the village on resolve (the launch itself is correct + real).
// =============================================================================

using Cysharp.Threading.Tasks;
using DeNelle.Core;
using UnityEngine;

namespace DeNelle.Sandbox
{
    /// <summary>
    /// Maps a dungeon depth/level + an enemy-family id to the concrete enemy-id
    /// roster handed to the ATB battle. Deeper levels yield a harder family and
    /// more enemies, so encounters scale with descent. The ids are resolved to
    /// ATB engine archetypes on the far side by BattleController.MapToEngineDef.
    /// </summary>
    [System.Serializable]
    public sealed class EncounterEnemySet
    {
        [Tooltip("Dungeon depth this encounter sits at (0 = top level). Deeper = harder.")]
        public int level = 0;

        [Tooltip("Enemy family id. Maps to ATB engine archetypes via MapToEngineDef " +
                 "(e.g. 'skeleton', 'bruiser', 'necromancer', or a hollow-* family id).")]
        public string familyId = "skeleton";

        [Tooltip("If true, this is the guaranteed boss encounter at the deepest level.")]
        public bool isBoss = false;

        /// <summary>
        /// Build the concrete enemy-id roster for this set. Count scales with
        /// <see cref="level"/> (1 base + 1 per 2 levels of depth, capped at 6 to
        /// keep the fight playable). A boss set returns a single, tougher family.
        /// </summary>
        public string[] BuildRoster()
        {
            if (isBoss)
            {
                // One tough enemy for the boss room. "hollow-king" is a real engine
                // def; if a family was named, prefer the heaviest archetype token so
                // MapToEngineDef resolves it to the Tank/Caster archetype.
                string bossId = string.IsNullOrEmpty(familyId) ? "hollow-king" : familyId + "-boss";
                return new[] { bossId };
            }

            int count = Mathf.Clamp(1 + Mathf.Max(0, level) / 2, 1, 6);
            string id = string.IsNullOrEmpty(familyId) ? "skeleton" : familyId;
            var roster = new string[count];
            for (int i = 0; i < count; i++) roster[i] = id;
            return roster;
        }
    }

    /// <summary>
    /// A trigger-collider encounter zone. When a <c>Player</c>-tagged collider
    /// enters and a chance roll succeeds, it launches OUR ATB battle through the
    /// canonical <see cref="SceneRouter.GoBattle"/> entry-point. Fires once
    /// (guarded). Attach a trigger <see cref="BoxCollider"/> (isTrigger = true) —
    /// <see cref="DeepDungeonBuilder.AddEncounters"/> wires these up automatically.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EncounterTrigger : MonoBehaviour
    {
        [Header("Encounter")]
        [Tooltip("The enemy set for this zone — depth/level + family, scaled into a roster.")]
        public EncounterEnemySet enemySet = new EncounterEnemySet();

        [Header("Roll")]
        [Tooltip("0..1 chance the encounter fires when the Player enters. Boss zones " +
                 "should set this to 1 (guaranteed).")]
        [Range(0f, 1f)]
        public float encounterChance = 0.5f;

        [Tooltip("Scene to return to after the battle resolves. The runtime-generated " +
                 "sandbox dungeon is not a registered scene, so this defaults to the " +
                 "village (see file header — full dungeon round-trip is flagged).")]
        public string returnScene = SceneRouter.Village;

        // One-shot guard: a fired trigger never re-fires (no battle-loop on the pad).
        private bool _triggered;

        private void OnTriggerEnter(Collider other)
        {
            if (_triggered) return;
            if (other == null || !other.CompareTag("Player")) return;

            // Chance roll — a guaranteed (boss) zone uses encounterChance = 1.
            if (Random.value > Mathf.Clamp01(encounterChance)) return;

            _triggered = true;

            string[] roster = enemySet != null
                ? enemySet.BuildRoster()
                : new[] { "skeleton" };

            Debug.Log($"[EncounterTrigger] Player entered encounter (level " +
                      $"{(enemySet != null ? enemySet.level : 0)}, family " +
                      $"'{(enemySet != null ? enemySet.familyId : "skeleton")}', boss=" +
                      $"{(enemySet != null && enemySet.isBoss)}) — launching ATB battle " +
                      $"with {roster.Length} enemy(ies).");

            LaunchBattle(roster).Forget();
        }

        /// <summary>
        /// Hands off to OUR real ATB battle via <see cref="SceneRouter.GoBattle"/>.
        /// Wave == 0 is the dungeon marker (no village Heart damage; Flee allowed).
        /// BreachedIds carries the encounter roster; BattleController maps each id
        /// to an engine ENEMY_DEF on the far side. Returns a UniTask — never async void.
        /// </summary>
        private async UniTask LaunchBattle(string[] roster)
        {
            var p = new BattleParams
            {
                Wave = 0, // dungeon encounter, not a village wave
                BreachedIds = roster ?? System.Array.Empty<string>(),
                ParticipatingPetIds = System.Array.Empty<string>(),
                // FLAGGED: a real dungeon scene name + DungeonRuntimeState handoff
                // would round-trip hero vitals + restore position. The sandbox
                // dungeon is generated at runtime (no registered scene), so we
                // default to the village. The LAUNCH itself is the real entry-point.
                ReturnScene = string.IsNullOrEmpty(returnScene) ? SceneRouter.Village : returnScene,
            };
            await SceneRouter.GoBattle(p);
        }

        private void OnDrawGizmosSelected()
        {
            bool boss = enemySet != null && enemySet.isBoss;
            Gizmos.color = boss
                ? new Color(0.9f, 0.1f, 0.1f, 0.35f)
                : new Color(0.85f, 0.4f, 0.2f, 0.30f);
            var box = GetComponent<BoxCollider>();
            if (box != null)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawWireCube(box.center, box.size);
            }
            else
            {
                Gizmos.DrawWireSphere(transform.position, 1.5f);
            }
        }
    }
}
