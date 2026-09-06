// =============================================================================
// HealingCaravanMobility — the Healing Caravan's support shell (WO-991, split by
// WO-1424): heal-field owner + glass HP + status chip, and — in an OFFENSIVE
// context only — the slow-roll follow.
// -----------------------------------------------------------------------------
// Attached only on healing_caravan. Keeps HealingFountain heal-out-of-battle.
//
// ⚠ THE NAME SAYS "Mobility"; THE JOB IS WIDER, AND DELIBERATELY SO. This class
// is the single OWNER of the caravan's whole support shell — it attaches the
// FloatingHealthBar (glass HP), the CaravanStatusChip and the CaravanHealField,
// and CaravanHealField.TickField reads its IsAlive/IsRolling to decide whether
// the field is live and at what strength. The type is NOT renamed on purpose:
// CombatCastCaravanMarkRegression.GateCaravan greps this exact file path and
// StructureFactory for this exact token, and ff.caravanmobile OFF skips the
// AddComponent entirely (StructureFactory.cs:1245-1254), which amputates the heal
// field, the ring, the chip and the glass HP along with the follow. Never reach
// for that flag to stop the caravan moving; never delete this component.
//
// ── THE OFFENSIVE / DEFENSIVE SPLIT (owner ruling 2026-09-06, WO-1424) ────────
// Owner, verbatim: "it slow follows as a combat attack item as defensive item it
// stationary". This is a SPLIT, not a reversal — the WO-991 ruling of 2026-08-15
// ("Slow-rolls following hero movement") STANDS for the offensive case:
//
//   • OFFENSIVE (enemy-owned scene — a raid/garrison the player attacked into):
//     the caravan is an escorted combat item. It slow-rolls after the hero at
//     FollowSpeed, exactly as WO-991 specified, and heals at MovingHealMultiplier
//     while it does.
//   • DEFENSIVE (the player's own town — a placed defensive structure): it is
//     STATIONARY where the player placed it, and heals at FULL strength.
//
// The defect this closes (owner playtest, build 2026.09.06.357599): "it pins hero
// against wall and cannot move". The caravan's NavMeshAgent (radius 0.6, default
// avoidancePriority 50) re-issued the hero's EXACT position as its destination and
// parked 2.5 m away with a 1.5 m box half-width — crowd separation then wedged the
// hero against geometry. In town the follow simply no longer runs; in a raid the
// StoryCompanion discipline below (slim radius + yielding priority) keeps it from
// shoving. STATIONARY IS THE FAIL-SAFE DEFAULT: any scene the ownership map cannot
// resolve reads Player-owned (SceneOwnership.cs:16-20), so an unknown scene parks.
//
// DISCRIMINATOR: SceneOwnership.IsEnemyOwned — this repo's ONE runtime signal for
// "is the active scene enemy-owned?" (SceneOwnership.cs:33-39), already the gate
// for build mode (BuildModeController.cs:488), the hero's death-retreat
// (HeroHealth.cs:876) and gate faction (Gate.cs:170). Reused, not reinvented.
// It also covers the config-generated RaidBase_<id> scenes, which resolve by NAME
// to Player-owned and are corrected via SceneOwnership.SetEnemyOwned (:50-53) —
// a scene-name test would miss them.
//
// ⚠ LATCHED ONCE, IN Awake, INTO _followsHero. Two live reads of the signal at two
// different times is how the caravan ends up with an agent AND a carving obstacle
// on one root (the failure recorded at Start() below) or with neither. The carve
// decision in BaseLayoutLoader.Spawn reads the SAME latched bool through
// FollowsHero, so the agent and the carve can never disagree.
// =============================================================================

using UnityEngine;
using UnityEngine.AI;
using DeNelle.Core.Catalog;
using DeNelle.Core.Combat;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    [DisallowMultipleComponent]
    public sealed class HealingCaravanMobility : MonoBehaviour, IDamageableStructure
    {
        // Crawl: hero walk ~4–6; caravan must feel useless as permanent escort.
        private const float FollowSpeed = 1.05f;
        private const float CatchUpStart = 6f;
        private const float CatchUpStop = 2.5f;
        private const float MaxHpGlass = 48f;
        private const float DamageTakenMult = 1.75f; // very easily damagable

        private NavMeshAgent _agent;
        private Transform _hero;
        private float _hp = MaxHpGlass;
        private bool _dead;
        private bool _moving;
        private float _nextHeroResolve;
        private Vector3 _lastDestination = new Vector3(float.MinValue, 0f, float.MinValue);

        /// <summary>
        /// LATCHED in Awake from <see cref="SceneOwnership.IsEnemyOwned"/> — true only in the
        /// OFFENSIVE context. Never re-read after Awake: see the header's latch note.
        /// </summary>
        private bool _followsHero;

        public bool IsAlive => !_dead && _hp > 0f;
        public float CurrentHp => _hp;

        /// <summary>
        /// True when this caravan is the OFFENSIVE, hero-following escort (an enemy-owned
        /// scene); false when it is the DEFENSIVE town structure, stationary where the player
        /// placed it. THE SINGLE SOURCE for the split — <c>BaseLayoutLoader.Spawn</c> keys the
        /// NavMesh carve on this same latched value so the agent and the carve never disagree.
        /// </summary>
        public bool FollowsHero => _followsHero;

        public void Configure(CatalogEntry entry)
        {
            // The MODE is in the line on purpose: a capture must prove which shape the
            // caravan built in without anyone re-deriving it from the scene name (§12).
            FlowTrace.Step("Caravan",
                $"HealingCaravanMobility Configure id='{entry?.id}' glassHp={MaxHpGlass} " +
                $"mode={(_followsHero ? "OFFENSIVE (follows hero, followSpeed=" + FollowSpeed + ")" : "DEFENSIVE (STATIONARY, carves navmesh)")}");
        }

        private void Awake()
        {
            // WO-753 / WO-1424 seam #4: compose the ONE-owner death lifecycle onto the caravan —
            // the SAME line, in the SAME place (Awake), as Building.cs:270 and DefenseTower.cs:368.
            // ⚠ IT SITS ABOVE THE DEFENSIVE EARLY-RETURN ON PURPOSE: both modes must be destructible.
            //
            // Before this, Die() called a raw Destroy(gameObject, 0.4f) and NOTHING else, so a
            // killed caravan skipped every one of Destructible.NotifyBroken's duties (VFX teardown,
            // grid Free, loader Forget, BaseLayout record removal, free-build burn, singleton
            // notify — Destructible.cs:150-193). The consequences were all real and all observed:
            // the persisted record outlived the body (the F8 census line "REPLAYABLE record(s) have
            // NO live body = [healing_caravan@(18,18)]"), its grid cell stayed Occupied for the rest
            // of the session, the dead caravan resurrected FREE on the next replay against the
            // WO-753 ruling, and — because healing_caravan sets repo.singleton=true in
            // structures-catalog.json and StructureSingleton.cs:543-549 answers HasPlacedInstance
            // from the BaseLayout RECORD ALONE, returning true before it ever looks at live bodies
            // (:550-560) — the build card
            // stayed on "Built" with the heal field gone and no way for the player to get it back.
            // This was the ONLY structure-death bypass in Assets/_Modules/Village.
            Destructible.Ensure(gameObject);

            // WO-1424 — latch the offensive/defensive split ONCE (header: DISCRIMINATOR + LATCH).
            _followsHero = SceneOwnership.IsEnemyOwned;

            if (!_followsHero)
            {
                // DEFENSIVE: a placed town structure. No agent at all — an agent is what
                // produced the crowd-separation pin, and a stationary caravan needs the
                // carving NavMeshObstacle instead (which an agent on the same root would
                // fight). A prefab-borne agent is DISABLED, not destroyed, and says so.
                var existing = GetComponent<NavMeshAgent>();
                if (existing != null && existing.enabled)
                {
                    existing.enabled = false;
                    FlowTrace.Warn("Caravan",
                        "DEFENSIVE caravan carried a NavMeshAgent from its prefab — disabled it. A stationary " +
                        "caravan carves the navmesh (BaseLayoutLoader.Spawn), and an agent on the same root " +
                        "would carve its own mesh away.");
                }
                FlowTrace.Step("Caravan",
                    "built DEFENSIVE (player-owned scene) — STATIONARY where placed, no agent, heals at FULL strength.");
                return;
            }

            // OFFENSIVE: the WO-991 escort shell, unchanged in speed/feel.
            _agent = GetComponent<NavMeshAgent>();
            if (_agent == null) _agent = gameObject.AddComponent<NavMeshAgent>();
            _agent.speed = FollowSpeed;
            _agent.acceleration = 4f;
            _agent.angularSpeed = 120f;
            _agent.stoppingDistance = CatchUpStop * 0.6f;
            _agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            _agent.height = 2f;
            // ANTI-SHOVE, copied verbatim from the project's own precedent for a follower that
            // must never push the player: StoryCompanion.cs:354-355 ("slim, won't shove" /
            // "yields to the hero/pets"). Belt-and-braces for the offensive case — the town
            // case is already fixed by not moving at all.
            _agent.radius = 0.35f;             // slim, won't shove (was 0.6f — the pin's other half)
            _agent.avoidancePriority = 60;     // yields to the hero/pets
            FlowTrace.Step("Caravan",
                $"built OFFENSIVE (enemy-owned scene) — slow-roll escort per WO-991; agent radius={_agent.radius:0.##} " +
                $"avoidancePriority={_agent.avoidancePriority} (StoryCompanion anti-shove discipline).");
        }

        private void Start()
        {
            // DEFENSE-IN-DEPTH (2026-08-15 review finding #1), NOW OFFENSIVE-ONLY (WO-1424): the
            // footprint pipeline adds a carving NavMeshObstacle AFTER Awake (same frame, after
            // StructureFactory.Create returns). An agent + a carving obstacle on ONE root means the
            // agent carves its own mesh away and never moves — so the FOLLOWING caravan still strips
            // it, loudly.
            // ⚠ THE STATIONARY CARAVAN MUST KEEP THAT OBSTACLE. It has no agent to starve, and a
            // 3x4x3 non-trigger BoxCollider with no carving obstacle is precisely the shape pets and
            // NPCs wedge against forever (PetHeroLeash.cs:556 documents that failure). Stripping the
            // carve here would have traded the hero-pin for a pet-pin.
            if (_followsHero)
            {
                var obstacle = GetComponent<NavMeshObstacle>();
                if (obstacle != null)
                {
                    FlowTrace.Warn("Caravan",
                        "NavMeshObstacle found on the OFFENSIVE (following) caravan root — a carving obstacle kills the agent's own navmesh. Removing it (a moving unit must not carve).");
                    Destroy(obstacle);
                }
            }

            // WO-991 item 5 — the ONE status chip: HP half = the existing
            // FloatingHealthBar (glass HP in the shared combat-bar language;
            // hideAtFull:false — an escorted support unit must be locatable at a
            // glance), state half = CaravanStatusChip ("FOLLOWING / IDLE" by
            // text + luminance, colourblind-safe). Guarded: a chip failure logs
            // and is skipped — it must never take the follow/damage shell down.
            // ⚠ WO-1424 FOLLOW-UP, NOT FIXED HERE: the chip's state half reads off IsRolling, so a
            // DEFENSIVE town caravan shows "IDLE" permanently — accurate about movement, but it now
            // undersells a structure that is healing at FULL strength. CaravanStatusChip.cs is
            // outside this WO's file scope; handed back for a copy pass (e.g. "HEALING" while the
            // field is on). Recorded rather than silently left to drift (CLAUDE.md §15).
            Guard.Try("Caravan", "attach-status-chip", () =>
            {
                FloatingHealthBar.Attach(gameObject,
                    fraction: () => _hp / MaxHpGlass,
                    isDead: () => _dead,
                    heightOffset: 2.9f,
                    hideAtFull: false,
                    destroyOnDead: true);
                CaravanStatusChip.Attach(this);
            });

            // WO-991 slice 2 (owner VFX tag 2026-08-16): the healing aura + the
            // Safe Zone range ring. Own Guard so a field/ring failure can never
            // take the chip or the follow/damage shell down (and vice versa).
            // Same ff.caravanmobile gate — this component only exists when ON.
            Guard.Try("Caravan", "attach-heal-field", () => CaravanHealField.Attach(this));
        }

        private void Update()
        {
            if (_dead) return;
            // WO-1424 — the DEFENSIVE caravan never follows: no hero scan, no destination, no
            // agent. IsRolling stays false forever, which is what makes it heal at FULL strength
            // (CaravanHealField.cs:144-145). This single early-return IS the fix for the owner's
            // "it pins hero against wall and cannot move".
            if (!_followsHero) return;
            // Re-resolve ONLY while unresolved (hero destroyed/respawned resets via the null
            // check) — the fallback FindFirstObjectByType is a whole-scene scan and must not
            // run twice a second forever on untagged rigs (2026-08-15 review, efficiency).
            if (_hero == null && Time.time >= _nextHeroResolve)
            {
                _nextHeroResolve = Time.time + 0.5f;
                ResolveHero();
            }
            if (_hero == null || _agent == null || !_agent.isOnNavMesh) return;

            float dist = Vector3.Distance(
                new Vector3(transform.position.x, 0f, transform.position.z),
                new Vector3(_hero.position.x, 0f, _hero.position.z));

            if (!_moving && dist > CatchUpStart)
            {
                _moving = true;
                FlowTrace.Throttle("Caravan", "start-follow", 2f,
                    $"start follow dist={dist:F1}m hero={_hero.name}");
            }
            else if (_moving && dist < CatchUpStop)
            {
                _moving = false;
                if (_agent.isOnNavMesh) _agent.ResetPath();
                FlowTrace.Throttle("Caravan", "stop-follow", 2f,
                    $"stop follow dist={dist:F1}m");
            }

            if (_moving && _agent.isOnNavMesh)
            {
                _agent.speed = FollowSpeed;
                // Re-path only when the hero has actually moved (>0.5m) from the last
                // requested destination — a 1.05 m/s crawler does not need 60 fresh
                // NavMesh path requests per second (2026-08-15 review, efficiency).
                if ((_hero.position - _lastDestination).sqrMagnitude > 0.25f)
                {
                    _lastDestination = _hero.position;
                    _agent.SetDestination(_lastDestination);
                }
            }
        }

        private void ResolveHero()
        {
            var tagged = GameObject.FindGameObjectWithTag("Player");
            if (tagged != null) { _hero = tagged.transform; return; }
            var loco = Object.FindFirstObjectByType<HeroLocomotion>();
            if (loco != null) _hero = loco.transform;
        }

        public void ApplyContactDamage(float amount)
        {
            if (_dead || amount <= 0f) return;
            float applied = amount * DamageTakenMult;
            _hp = Mathf.Max(0f, _hp - applied);
            FlowTrace.Throttle("Caravan", "hit", 0.5f,
                $"ApplyContactDamage raw={amount:F1} applied={applied:F1} hp={_hp:F0}/{MaxHpGlass}");
            if (_hp <= 0f) Die();
        }

        private void Die()
        {
            if (_dead) return;
            _dead = true;
            _moving = false;
            if (_agent != null && _agent.enabled && _agent.isOnNavMesh) _agent.isStopped = true;
            FlowTrace.Step("Caravan", "DESTROYED — glass support unit killed (WO-991)");

            // WO-1424 seam #4 — route the death through the ONE owner of structure death, exactly
            // as Building.cs:230 and DefenseTower.cs:356 do. NotifyBroken frees the grid cell,
            // Forgets it from the loader, DROPS THE PERSISTED BaseLayout RECORD (so it does not
            // resurrect free on the next replay and the singleton build card goes buildable again),
            // burns the free-build so the rebuild costs full price per WO-753, and Destroy()s the
            // GameObject itself (Destructible.cs:193) — which is why there is no Destroy call here.
            //
            // ⚠ THE 0.4s CORPSE LINGER IS DELIBERATELY GONE, not overlooked: NotifyBroken destroys
            // at end-of-frame, matching every other structure. Nothing is orphaned by that — the
            // field's ring is released through CaravanHealField.OnDestroy -> SetField(false), and
            // TickField's own !IsAlive exit (CaravanHealField.cs:133-137) already fires this frame.
            //
            // FAIL-LOUD, never silent: a caravan with no Destructible would otherwise vanish here
            // with the record intact — the exact bug being closed — so say so and still remove it.
            var destructible = Destructible.For(gameObject);
            if (destructible != null)
            {
                destructible.NotifyBroken("HealingCaravan hp0");
            }
            else
            {
                FlowTrace.Fail("Caravan",
                    "Die(): no Destructible on the caravan root (Start's Ensure did not run — killed before Start?). " +
                    "Falling back to a raw Destroy, which LEAVES THE PERSISTED BaseLayout RECORD BEHIND: expect a " +
                    "'REPLAYABLE record(s) have NO live body' census line and a singleton build card stuck on 'Built'.");
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// True while actively crawling after the hero — i.e. only ever in the OFFENSIVE
        /// (enemy-owned scene) mode. A DEFENSIVE town caravan is permanently false, which is
        /// what parks it at FULL heal strength in CaravanHealField.
        /// </summary>
        public bool IsRolling => _moving;
    }
}
