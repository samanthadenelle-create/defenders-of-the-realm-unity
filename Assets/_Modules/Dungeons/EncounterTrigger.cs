// =============================================================================
// EncounterTrigger — scripted + random dungeon encounter zones (Week 6).
// -----------------------------------------------------------------------------
// Port spec Part 3 row:
//   src/modules/dungeons/encounters/ -> _Modules/Dungeons/EncounterTrigger.cs
//                                       + RandomEncounterTable.cs
// Port spec Part 5 Week 6: "scripted encounter zones + a random encounter table.
// Trigger an ATB battle via SceneRouter.LoadScene('ATBBattle', ...). After
// resolution, return to dungeon scene with hero HP/mana state preserved."
//
// C# port of src/modules/dungeons/3d/encounters/. Two trigger kinds, one
// MonoBehaviour:
//   - SCRIPTED — a room-placed, hand-authored fight. Fires ONCE the first time
//     the Keeper crosses the trigger's proximity zone (design §4 Beats 1/3/4).
//   - RANDOM   — a probability roll gated by a cooldown, drawing from the
//     dungeon's weighted encounter pool. v1 gates random encounters OFF
//     (DungeonLayout.disableRandomEncounters) — the Cottage uses scripted
//     encounters only; the random path stays intact for v1.1.
//
// The ATB battle handoff goes through SceneRouter.GoBattle(BattleParams) — the
// dungeon module references DeNelle.Core, not DeNelle.BattleATB, so the battle
// scene is reached by the canonical route (port spec Part 3: "Trigger an ATB
// battle via SceneRouter"). The battle scene reads SceneRouter.PendingBattle
// and the dungeon resumes when control returns.
//
// Random-roll math (cooldown / quiet-stretch ramp / reward cap) lives in the
// pure-C# RandomEncounterTable — this MonoBehaviour only owns scene wiring.
// =============================================================================

using Cysharp.Threading.Tasks;
using DeNelle.Core;
using UnityEngine;
using UnityEngine.Events;

namespace DeNelle.Dungeons
{
    /// <summary>Which kind of encounter zone this trigger represents.</summary>
    public enum EncounterKind
    {
        /// <summary>A room-placed, hand-authored fight — fires once on proximity.</summary>
        Scripted = 0,
        /// <summary>A roll-driven random encounter — gated by cooldown (v1.1).</summary>
        Random = 1,
    }

    /// <summary>
    /// A dungeon encounter zone. A scripted trigger fires its authored roster
    /// once on first proximity; a random trigger rolls each tick the Keeper is
    /// inside it. Either way the fight is launched through
    /// <see cref="SceneRouter.GoBattle"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EncounterTrigger : MonoBehaviour
    {
        [Header("Kind")]
        [Tooltip("Scripted (authored, fires once) or Random (rolled — v1.1).")]
        [SerializeField] private EncounterKind _kind = EncounterKind.Scripted;

        [Header("State")]
        [Tooltip("The runtime run state — supplies the encounter clock + combat lock.")]
        [SerializeField] private DungeonRuntimeState _runtimeState;

        [Header("Events")]
        [Tooltip("Raised the moment this trigger commits to a battle, before the " +
                 "scene routes away — lets the scene snapshot hero HP/mana.")]
        public UnityEvent<string> EncounterFired = new UnityEvent<string>();

        // ── Runtime data (handed in via Configure) ───────────────────────────

        private DungeonController _controller;
        private Transform _hero;

        /// <summary>Authored scripted-encounter definition — set for Scripted triggers.</summary>
        private DungeonScriptedEncounter _scriptedDef;

        /// <summary>The dungeon's random-encounter pool — set for Random triggers.</summary>
        private DungeonEncounterPool _pool;

        /// <summary>The random-roll table — pure-C# cooldown / ramp / cap math.</summary>
        private RandomEncounterTable _table;

        /// <summary>The current room's kind — random encounters never fire in safe rooms.</summary>
        private string _currentRoomKind = "standard";

        /// <summary>True once this scripted trigger has fired (it only fires once).</summary>
        private bool _hasFired;

        /// <summary>True when this trigger represents the dungeon's mini-boss fight.</summary>
        private bool _isBossTrigger;

        /// <summary>True while the Keeper stands inside this trigger's proximity zone.</summary>
        public bool HeroInside { get; private set; }

        /// <summary>The id of the encounter this trigger fires (scripted-encounter / boss id).</summary>
        public string EncounterId => _scriptedDef?.id ?? string.Empty;

        // ── Configuration ────────────────────────────────────────────────────

        /// <summary>
        /// Configures a SCRIPTED encounter trigger from its authored definition.
        /// Positions the trigger at the layout's coordinates.
        /// </summary>
        public void ConfigureScripted(
            DungeonController controller,
            DungeonScriptedEncounter def,
            DungeonRuntimeState runtimeState,
            Transform hero)
        {
            _kind = EncounterKind.Scripted;
            _controller = controller;
            _scriptedDef = def;
            _runtimeState = runtimeState;
            _hero = hero;
            _isBossTrigger = false;
            if (def != null)
                transform.position = def.triggerPosition.ToWorld();
            // A scripted encounter already cleared on a resumed run does not re-fire.
            _hasFired = def != null && runtimeState != null
                && runtimeState.HasFiredScriptedEncounter(def.id);
        }

        /// <summary>
        /// Configures this trigger as the dungeon's MINI-BOSS encounter — the
        /// climax fight (design §4 Beat 6, the Apprentice of the Apothecary). A
        /// boss trigger is a scripted trigger whose victory marks the boss
        /// defeated and unlocks the dungeon exit. The <paramref name="boss"/>
        /// def's <c>position</c> doubles as the proximity-trigger centre.
        /// </summary>
        public void ConfigureBoss(
            DungeonController controller,
            DungeonMiniBoss boss,
            float triggerRadius,
            DungeonRuntimeState runtimeState,
            Transform hero)
        {
            _kind = EncounterKind.Scripted;
            _controller = controller;
            _runtimeState = runtimeState;
            _hero = hero;
            _isBossTrigger = true;

            // Adapt the mini-boss def into the scripted-encounter shape so the
            // one TickScripted path drives both. The boss is a single enemy.
            _scriptedDef = boss == null ? null : new DungeonScriptedEncounter
            {
                id = boss.id,
                roomId = boss.roomId,
                triggerPosition = boss.position,
                triggerRadius = triggerRadius > 0f ? triggerRadius : 4f,
                enemyTypes = string.IsNullOrEmpty(boss.enemyType)
                    ? System.Array.Empty<string>()
                    : new[] { boss.enemyType },
                waveLabel = boss.displayName,
            };
            if (_scriptedDef != null)
                transform.position = _scriptedDef.triggerPosition.ToWorld();
            _hasFired = _scriptedDef != null && runtimeState != null
                && runtimeState.HasFiredScriptedEncounter(_scriptedDef.id);
        }

        /// <summary>
        /// Configures a RANDOM encounter trigger covering a room. v1 leaves these
        /// dormant (<see cref="DungeonLayout.disableRandomEncounters"/>); the
        /// path is wired so v1.1 can flip the dungeon flag with no code change.
        /// </summary>
        public void ConfigureRandom(
            DungeonController controller,
            DungeonEncounterPool pool,
            int dungeonTier,
            DungeonRuntimeState runtimeState,
            Transform hero)
        {
            _kind = EncounterKind.Random;
            _controller = controller;
            _pool = pool;
            _runtimeState = runtimeState;
            _hero = hero;
            _isBossTrigger = false;
            _table = new RandomEncounterTable(dungeonTier);
        }

        /// <summary>Updates the room kind — random encounters never fire in safe rooms.</summary>
        public void SetCurrentRoomKind(string roomKind)
        {
            if (!string.IsNullOrEmpty(roomKind)) _currentRoomKind = roomKind;
        }

        // ── Per-frame ────────────────────────────────────────────────────────

        private void Update()
        {
            if (_runtimeState == null || !_runtimeState.RunActive) return;
            // Never act while a battle is being resolved.
            if (_runtimeState.InCombat) return;

            switch (_kind)
            {
                case EncounterKind.Scripted:
                    TickScripted();
                    break;
                case EncounterKind.Random:
                    TickRandom(Time.deltaTime);
                    break;
            }
        }

        // ── Scripted ─────────────────────────────────────────────────────────

        /// <summary>
        /// Fires the scripted encounter the first time the Keeper crosses its
        /// proximity zone. One-shot — a scripted fight never repeats in a run.
        /// </summary>
        private void TickScripted()
        {
            if (_hasFired || _scriptedDef == null || _hero == null) return;

            Vector3 a = transform.position; a.y = 0f;
            Vector3 b = _hero.position; b.y = 0f;
            float r = _scriptedDef.triggerRadius;
            HeroInside = (a - b).sqrMagnitude <= r * r;
            if (!HeroInside) return;

            _hasFired = true;
            // RegisterScriptedEncounter resets the cooldown clock + locks combat;
            // BeginEncounterHandoff stashes the encounter id + resume position so
            // they survive the ATB scene round-trip and the dungeon can resume.
            _runtimeState.RegisterScriptedEncounter(_scriptedDef.id);
            _runtimeState.BeginEncounterHandoff(
                _scriptedDef.id, _isBossTrigger, _hero.position);
            EncounterFired.Invoke(_scriptedDef.id);
            LaunchBattle(_scriptedDef.enemyTypes, _scriptedDef.waveLabel).Forget();
        }

        // ── Random ───────────────────────────────────────────────────────────

        /// <summary>
        /// Rolls a random encounter each tick the Keeper is in the zone. Gated
        /// by the cooldown / quiet-stretch ramp in <see cref="RandomEncounterTable"/>
        /// and never fired in checkpoint / boss rooms. Dormant in v1.
        /// </summary>
        private void TickRandom(float dt)
        {
            if (_table == null || _pool == null || _hero == null) return;
            if (_currentRoomKind == "checkpoint" || _currentRoomKind == "boss") return;

            var verdict = _table.Roll(
                secondsSinceLastEncounter: _runtimeState.SecondsSinceLastEncounter,
                randomEncounterCount: _runtimeState.RandomEncounterCount,
                inDarkness: false,
                dtSeconds: dt,
                seed: _runtimeState.DungeonRunSeed + _runtimeState.RandomEncounterCount);
            if (!verdict.Fire) return;

            string[] roster = _table.RollEnemies(_pool);
            _runtimeState.RegisterRandomEncounter();
            _runtimeState.BeginEncounterHandoff(
                "random-encounter", false, _hero.position);
            EncounterFired.Invoke("random-encounter");
            LaunchBattle(roster, "Ambush in the dark").Forget();
        }

        // ── Resume — called by DungeonController on dungeon re-entry ─────────

        /// <summary>
        /// Resolves a pending encounter when the dungeon scene reloads after the
        /// ATB battle. Clears the run's combat lock, marks the mini-boss defeated
        /// on a boss-fight victory, and re-arms <see cref="_hasFired"/> so the
        /// cleared encounter never re-triggers. Idempotent — a no-op when this
        /// trigger is not the one that started the in-flight battle.
        ///
        /// <paramref name="victory"/> comes from the battle result the dungeon
        /// scene reads off the ATB runtime state on return. The hero's HP / mana
        /// were already round-tripped through <see cref="DungeonRuntimeState"/>
        /// (and, in v1, fully restored by the ATB engine's post-fight reset).
        /// </summary>
        public bool ResumePendingEncounter(bool victory)
        {
            if (_runtimeState == null || _scriptedDef == null) return false;
            if (_runtimeState.PendingEncounterId != _scriptedDef.id) return false;

            // This trigger owns the in-flight battle — settle it.
            _hasFired = true;
            bool resumed = _runtimeState.ResumeAfterEncounter(victory);
            return resumed;
        }

        // ── ATB battle handoff ───────────────────────────────────────────────

        /// <summary>
        /// Hands the encounter off to the ATB battle scene through
        /// <see cref="SceneRouter.GoBattle"/>. The dungeon module references
        /// DeNelle.Core (not DeNelle.BattleATB), so the battle is reached by the
        /// canonical route; the battle scene reads <see cref="SceneRouter.PendingBattle"/>.
        /// Returns a <see cref="UniTask"/> — never <c>async void</c>.
        ///
        /// <c>BattleParams.Wave == 0</c> is the dungeon marker — a village breach
        /// always carries a wave &gt; 0. The battle scene returns to the dungeon
        /// (rather than the village) off that distinction. The Keeper's vitals
        /// were snapshotted into <see cref="DungeonRuntimeState"/> by the trigger
        /// before this call, so they survive the scene round-trip.
        /// </summary>
        private async UniTask LaunchBattle(string[] enemyTypes, string waveLabel)
        {
            var p = new BattleParams
            {
                Wave = 0, // a dungeon encounter is not a village wave
                BreachedIds = enemyTypes ?? System.Array.Empty<string>(),
                ParticipatingPetIds = System.Array.Empty<string>(),
            };
            // waveLabel is the authored pre-fight banner; the battle scene reads
            // it off the params. (BattleParams gains a Label field when the
            // Week-2 battle spec lands; until then the roster carries the fight.)
            await SceneRouter.GoBattle(p);
        }

        private void OnDrawGizmosSelected()
        {
            if (_scriptedDef != null)
            {
                Gizmos.color = new Color(1f, 0.5f, 0.3f, 0.35f);
                Gizmos.DrawWireSphere(
                    _scriptedDef.triggerPosition.ToWorld(), _scriptedDef.triggerRadius);
            }
        }
    }
}
