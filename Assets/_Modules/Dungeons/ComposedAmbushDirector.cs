// =============================================================================
// ComposedAmbushDirector — WO-1001 slice 6 darkness ambush for Pipeline A.
// -----------------------------------------------------------------------------
// Cottage random ambushes use EncounterTrigger + ATB; composed dungeons use room
// spawners. When the lantern is dark, this director rolls the same RandomEncounter
// table (with DarknessRateMult) and, on fire, seats a small hollow-group near the
// hero so "push into the dark" has a real cost without requiring ATB scene round-trips.
// =============================================================================

using DeNelle.Core.Diagnostics;
using DeNelle.Village;
using UnityEngine;
using UnityEngine.AI;

namespace DeNelle.Dungeons
{
    /// <summary>Periodic darkness ambush rolls while a composed dungeon run is live.</summary>
    [DisallowMultipleComponent]
    public sealed class ComposedAmbushDirector : MonoBehaviour
    {
        private const string Sys = "ComposedAmbush";
        private const float TickSeconds = 6f;
        private const float MinSecondsBetweenAmbushes = 28f;

        private Lantern _lantern;
        private Transform _hero;
        private RandomEncounterTable _table;
        private DungeonRuntimeState _state;
        private float _tickAcc;
        private float _cooldownLeft;
        private int _ambushCount;
        private bool _loggedDarkOnce;

        /// <summary>True once the Keeper has been in darkness this visit (legendary gate).</summary>
        public bool HasBeenInDarkness { get; private set; }

        public void Configure(Lantern lantern, Transform hero, DungeonRuntimeState state, int tier = 1)
        {
            _lantern = lantern;
            _hero = hero;
            _state = state;
            _table = new RandomEncounterTable(Mathf.Max(1, tier));
            _tickAcc = 0f;
            _cooldownLeft = 8f; // grace after entry
            FlowTrace.Step(Sys, $"armed tier={tier} hero='{(hero != null ? hero.name : "<null>")}'");
        }

        private void Update()
        {
            if (_hero == null || _table == null) return;
            if (_cooldownLeft > 0f) _cooldownLeft -= Time.deltaTime;

            bool dark = _lantern != null && _lantern.IsInDarkness;
            if (dark)
            {
                if (!HasBeenInDarkness)
                {
                    HasBeenInDarkness = true;
                    FlowTrace.Step(Sys, "Keeper entered DARKNESS (oil critical) — legendary gate unlocks, ambush odds up");
                }
                if (!_loggedDarkOnce)
                {
                    _loggedDarkOnce = true;
                    FlowTrace.Step(Sys, $"darkness active oil={(_lantern != null ? _lantern.OilFraction : -1f):0.00}");
                }
            }

            _tickAcc += Time.deltaTime;
            if (_tickAcc < TickSeconds) return;
            float dt = _tickAcc;
            _tickAcc = 0f;

            if (!dark || _cooldownLeft > 0f) return;

            int seedBase = _state != null
                ? _state.DungeonRunSeed + _state.RandomEncounterCount + _ambushCount
                : _ambushCount * 997;
            var verdict = _table.Roll(
                secondsSinceLastEncounter: Mathf.Max(MinSecondsBetweenAmbushes, 30f),
                randomEncounterCount: _ambushCount,
                inDarkness: true,
                dtSeconds: dt,
                seed: seedBase);
            if (!verdict.Fire) return;

            if (SpawnAmbushNearHero())
            {
                _ambushCount++;
                _cooldownLeft = MinSecondsBetweenAmbushes;
                if (_state != null) _state.RegisterRandomEncounter();
                FlowTrace.Step(Sys,
                    $"AMBUSH fired in darkness (#{_ambushCount}) chance={verdict.RolledChance:0.000} " +
                    $"near {_hero.position}");
            }
        }

        private bool SpawnAmbushNearHero()
        {
            // Prefer an existing OutpostEnemyGroupSpawner so family tables stay consistent.
            var spawner = FindFirstObjectByType<OutpostEnemyGroupSpawner>();
            Vector3 centre = _hero.position + _hero.forward * 4f;
            if (NavMesh.SamplePosition(centre, out var hit, 8f, NavMesh.AllAreas))
                centre = hit.position;

            if (spawner != null)
            {
                spawner.SpawnGroup(centre, seed: _ambushCount * 131 + 17, min: 1, max: 2);
                return true;
            }

            // Fallback: one weighted hollow via factory if no spawner in scene.
            string id = OutpostEnemyGroupSpawner.WeightedIdFor("hollow-group",
                new System.Random(_ambushCount + 42));
            if (string.IsNullOrEmpty(id)) return false;
            // DefFor is private — use public SpawnGroup only when spawner exists.
            FlowTrace.Warn(Sys, "no OutpostEnemyGroupSpawner in scene — ambush roll wasted (bake spawners first)");
            return false;
        }
    }
}
