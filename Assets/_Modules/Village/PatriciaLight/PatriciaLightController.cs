// =============================================================================
// PatriciaLightController — the "Defend the Tower" director (WO-47 Phase 2).
// -----------------------------------------------------------------------------
// The runtime orchestrator for the DEDICATED PatriciaLightMode.unity scene (the
// owner's target experience). The scene ships nearly empty (built by the editor
// PatriciaLightSceneBuilder: a root with this controller, a Main Camera with
// ThirdPersonCameraFollow, and a UIDocument host). At runtime this controller:
//
//   • Builds a tower (the Heart, the canonical tower HP) + a hero balcony.
//   • Spawns the player's hero (Ranger / Mage — Knight defaults to Ranger) on
//     the balcony from Resources/Heroes/<Class>, wires HeroAbilities, and points
//     the third-person camera at it.
//   • Streams real DeNelle.Village.Enemy instances from FOUR spawners
//     (Left/Right Ground + Left/Right Air) across 5 waves + a boss toward the
//     tower. Air enemies fly (offset Y, kinematic glide); ground enemies use the
//     Enemy NavMesh march — with a runtime-baked NavMesh under the arena.
//   • Auto-fires the hero at the nearest hostile on a short cooldown (HeroAbilities
//     .TryCast, else a direct IDamageable sweep — the SAME path as Phase 1).
//   • Spawns a couple of pets with an Attack/Repair toggle (PetRepairAdapter):
//     Attack = the Pet's own Defend hunt AI; Repair = move to tower + restore Hp.
//   • Drives a Tower-HP UI (code-built UI-Toolkit slider + gradient + N/Max text).
//   • WIN (survive 5 waves + boss) → repel remaining + a Wisdom bonus → village.
//     LOSE (Hp 0) → village. Applies an optional "last chance" lighting mood.
//
// HARD CONSTRAINTS honoured: REUSES Enemy / HeartController / Pet / HeroAbilities
// / WisdomCurrencyService / DamageAttribution + IDamageable (no clean-room
// duplicates, no EnemyHealth, no new PetController, no forked tower HP); code-
// built UI (no UXML); lives in DeNelle.Village (no new asmdef).
// =============================================================================

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DeNelle.Core;
using DeNelle.Core.Combat;
using DeNelle.Core.State;
using DeNelle.Pets;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UIElements;

namespace DeNelle.Village
{
    /// <summary>
    /// The runtime director for the dedicated Defend-the-Tower scene. Spawns the
    /// tower, hero, four spawners, pets and HUD; runs the 5-wave + boss assault;
    /// resolves win/lose and returns to the village. Added to the scene root by
    /// <c>DeNelle.Editor.PatriciaLightSceneBuilder</c>; everything else is built
    /// here at runtime so the saved .unity stays trivially simple.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PatriciaLightController : MonoBehaviour
    {
        // ── Assault tuning ────────────────────────────────────────────────────
        private const int   WaveCount          = 5;       // 5 waves...
        private const float SpawnInterval      = 1.1f;    // seconds between spawns in a wave
        private const float WaveGapSeconds      = 2.5f;    // calm beat between waves
        private const float GroundSpawnDist     = 30f;     // metres left/right of the tower (ground)
        private const float AirSpawnDist        = 26f;     // metres left/right (air lanes)
        private const float AirHeight           = 9f;      // cruise height for flying enemies
        private const float HeartHitDamage      = 9f;      // tower HP lost per enemy arrival
        private const float HeroFireCooldown    = 0.4f;    // spam-friendly auto-fire cadence
        private const float HeroFireRange       = 90f;     // direct-fire fallback reach
        private const float HeroFireDamage      = 18f;     // direct-fire fallback damage
        private const int   WisdomReward        = 8;       // Phase-2 win bonus (bigger than Phase-1's 3)
        private const float TowerMaxHp          = 100f;    // HeartController is 0..100
        private const int   PetCount            = 3;       // pets to field (3-4 spawn points per WO)

        private const string GroundEnemyId      = "hollow-walker";  // canonical ground enemy
        private const string BossEnemyId        = "necromancer";    // fallback boss when no apex prefab

        // Hero / tower layout in the arena.
        private static readonly Vector3 TowerPos    = Vector3.zero;
        private static readonly Vector3 BalconyPos  = new Vector3(0f, 4f, -1.5f);
        private static readonly Vector3 CamOffset   = new Vector3(0f, 8f, -12f);

        // ── State ─────────────────────────────────────────────────────────────
        private HeartController _heart;
        private Transform _towerTransform;
        private ThirdPersonCameraFollow _camFollow;

        private HeroAbilities _hero;
        private Transform _heroTransform;
        private float _fireCooldown;
        private readonly Collider[] _overlap = new Collider[64];

        private EnemyCatalog _enemyCatalog;
        private EnemyDef _groundDef;
        private EnemyDef _bossDef;

        private readonly List<Enemy> _liveEnemies = new List<Enemy>();
        private readonly List<PetRepairAdapter> _pets = new List<PetRepairAdapter>();
        private DragonBoss _liveApexBoss;

        private int _idCounter;
        private int _currentWave;
        private bool _running;
        private bool _resolved;
        private string _returnScene = SceneRouter.Village;
        private LayerMask _enemyMask = ~0;

        // ── HUD ───────────────────────────────────────────────────────────────
        private UIDocument _hudDoc;
        private PanelSettings _ownPanelSettings;
        private VisualElement _hpFill;
        private Label _hpLabel;
        private Label _waveLabel;
        private Label _statusLabel;
        private readonly List<Button> _petButtons = new List<Button>();

        // =====================================================================
        //  Lifecycle
        // =====================================================================

        private void Start()
        {
            var p = SceneRouter.PendingPatriciaLight;
            if (p != null)
            {
                _currentWave = Mathf.Max(1, p.Wave);
                if (!string.IsNullOrEmpty(p.ReturnScene)) _returnScene = p.ReturnScene;
            }

            Run().Forget();
        }

        private async UniTask Run()
        {
            BuildArena();
            BuildTower();
            ResolveEnemyMask();

            // NavMesh under the arena so ground enemies can march (Enemy needs it).
            BakeArenaNavMesh();

            await LoadEnemyDefs();
            if (_groundDef == null)
            {
                Debug.LogError("[PatriciaLight] No enemy def available — returning to village.");
                ReturnHome();
                return;
            }

            SpawnHero();
            SpawnPets();
            ApplyLastChanceLighting();
            BuildHud();

            _heart.SetState(HeartState.Critical);
            RefreshHud();

            _running = true;
            RunAssault().Forget();
        }

        private void Update()
        {
            if (!_running) return;

            // Prune dead / destroyed enemies.
            for (int i = _liveEnemies.Count - 1; i >= 0; i--)
            {
                Enemy e = _liveEnemies[i];
                if (e == null || e.IsDead) _liveEnemies.RemoveAt(i);
            }

            DriveAirEnemies();
            TickHeroFire();

            if (_heart != null && _heart.Hp <= 0f) { Lose(); return; }
        }

        // =====================================================================
        //  Arena + tower
        // =====================================================================

        /// <summary>
        /// Lays a simple ground plane + directional light for the dedicated scene
        /// (the saved .unity is otherwise near-empty). Kept deliberately trivial —
        /// the corruption risk the WO warns about is from re-saving the COMPLEX
        /// village scene, not from a fresh flat arena.
        /// </summary>
        private void BuildArena()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Arena Ground";
            ground.transform.SetParent(transform, false);
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(12f, 1f, 12f); // 120m plane
            TintUrp(ground, new Color(0.20f, 0.22f, 0.26f));

            if (UnityEngine.Object.FindAnyObjectByType<Light>() == null)
            {
                var lightGo = new GameObject("Directional Light");
                lightGo.transform.SetParent(transform, false);
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                light.intensity = 1f;
            }
        }

        /// <summary>
        /// Builds the central tower: a tall stack carrying the canonical
        /// <see cref="HeartController"/> (the tower HP the whole game can see — no
        /// forked HP) plus a balcony platform the hero stands on.
        /// </summary>
        private void BuildTower()
        {
            var towerGo = new GameObject("Tower");
            towerGo.transform.SetParent(transform, false);
            towerGo.transform.position = TowerPos;
            _towerTransform = towerGo.transform;

            var spire = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            spire.name = "Spire";
            spire.transform.SetParent(towerGo.transform, false);
            spire.transform.localScale = new Vector3(3f, 4f, 3f);
            spire.transform.localPosition = new Vector3(0f, 4f, 0f);
            TintUrp(spire, new Color(0.42f, 0.40f, 0.50f));

            var balcony = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            balcony.name = "Balcony";
            balcony.transform.SetParent(towerGo.transform, false);
            balcony.transform.localScale = new Vector3(4.5f, 0.3f, 4.5f);
            balcony.transform.localPosition = new Vector3(0f, BalconyPos.y - 0.3f, 0f);
            TintUrp(balcony, new Color(0.30f, 0.28f, 0.36f));

            // HeartController is the canonical tower HP (0..100). Use its authored
            // transform so it does not snap to origin/scale at Awake.
            _heart = towerGo.AddComponent<HeartController>();
            try
            {
                var f = typeof(HeartController).GetField("_useAuthoredTransform",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                f?.SetValue(_heart, true);
            }
            catch { /* non-fatal — Awake's origin heuristic also leaves a non-origin tower alone */ }
            _heart.SetHp(TowerMaxHp);
        }

        private void ResolveEnemyMask()
        {
            // The enemy layer is project layer 8 ("Enemy"), mirrored from
            // VillageSceneBuilder.EnemyLayer. Enemies are placed on it so the
            // hero/pet OverlapSphere sweeps (which mask to this layer) find them.
            int layer = LayerMask.NameToLayer("Enemy");
            _enemyMask = layer >= 0 ? (1 << layer) : ~0;
        }

        // =====================================================================
        //  Enemy data
        // =====================================================================

        private async UniTask LoadEnemyDefs()
        {
            _enemyCatalog = await WaveDataLoader.LoadEnemiesAsync();
            if (_enemyCatalog == null) return;

            _groundDef = _enemyCatalog.Find(GroundEnemyId);
            if (_groundDef == null && _enemyCatalog.Enemies != null && _enemyCatalog.Enemies.Count > 0)
                _groundDef = _enemyCatalog.Enemies[0];

            _bossDef = _enemyCatalog.Find(BossEnemyId) ?? _groundDef;
        }

        // =====================================================================
        //  Hero
        // =====================================================================

        /// <summary>
        /// Spawns the player's hero on the balcony. Reads the saved class; only
        /// Ranger / Mage are playable here (WO-47), so Knight defaults to Ranger.
        /// Loads Resources/Heroes/&lt;Class&gt;, wires HeroAbilities + its class,
        /// and points the follow camera at it. Falls back to a primitive turret
        /// firing from the tower when no body resource is present.
        /// </summary>
        private void SpawnHero()
        {
            HeroClass cls = ResolvePlayableClass(out bool defaulted);
            string slug = cls.ToString(); // "Ranger" / "Mage"
            if (defaulted)
                Debug.Log($"[PatriciaLight] Knight save is melee-only here — defaulting to {slug}.");

            var heroRoot = new GameObject($"Hero ({slug})");
            heroRoot.transform.SetParent(transform, false);
            heroRoot.transform.position = TowerPos + BalconyPos;
            heroRoot.transform.rotation = Quaternion.identity; // faces +Z (into the arena)
            _heroTransform = heroRoot.transform;

            // Body from Resources/Heroes/<Class> (the canonical hero pickup path).
            var prefab = Resources.Load<GameObject>("Heroes/" + slug);
            if (prefab != null)
            {
                var body = Instantiate(prefab, heroRoot.transform);
                body.name = "HeroBody";
                body.transform.localPosition = Vector3.zero;
                body.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // Tripo -Z forward flip
                NormalizeHeight(body, 2.0f);
                StripCollidersAndCameras(body);
            }
            else
            {
                Debug.LogWarning($"[PatriciaLight] Resources/Heroes/{slug} not found — using a turret stand-in.");
                var stand = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                stand.name = "HeroBody";
                stand.transform.SetParent(heroRoot.transform, false);
                stand.transform.localScale = new Vector3(0.8f, 1f, 0.8f);
                stand.transform.localPosition = new Vector3(0f, 1f, 0f);
                if (stand.TryGetComponent(out Collider col)) col.isTrigger = true;
                TintUrp(stand, new Color(0.45f, 0.55f, 0.85f));
            }

            // HeroAbilities — the SAME kit the village HUD trigger uses. Wire its
            // class so a Ranger fires arrows, not the Mage loadout, and hand it
            // the tower so Healing Beacon (E) tops up tower HP.
            _hero = heroRoot.AddComponent<HeroAbilities>();
            _hero.SetHeroClass(slug);
            _hero.SetHeart(_heart);
            TrySetHeroEnemyMask(_hero, _enemyMask);

            // Point the third-person camera at the hero.
            _camFollow = UnityEngine.Object.FindAnyObjectByType<ThirdPersonCameraFollow>();
            if (_camFollow == null && Camera.main != null)
                _camFollow = Camera.main.gameObject.AddComponent<ThirdPersonCameraFollow>();
            if (_camFollow != null)
            {
                _camFollow.SetOffset(CamOffset);
                _camFollow.Target = _heroTransform;
            }
        }

        private static HeroClass ResolvePlayableClass(out bool defaulted)
        {
            defaulted = false;
            var svc = GameStateService.Instance;
            HeroClass cls = (svc != null && svc.State != null)
                ? (svc.State.HeroClass.ToNullable() ?? HeroClass.Ranger)
                : HeroClass.Ranger;

            if (cls == HeroClass.Knight) { defaulted = true; return HeroClass.Ranger; }
            if (cls == HeroClass.Mage || cls == HeroClass.Ranger) return cls;
            return HeroClass.Ranger;
        }

        private static void TrySetHeroEnemyMask(HeroAbilities hero, LayerMask mask)
        {
            // HeroAbilities has no public mask setter; write the private field so
            // its OverlapSphere sweeps target the enemy layer (matches how the
            // village scene builder sets it). Non-fatal if the field moves.
            try
            {
                var f = typeof(HeroAbilities).GetField("_enemyMask",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                f?.SetValue(hero, mask);
            }
            catch { /* leave the default ~0 mask — still finds enemies */ }
        }

        private void TickHeroFire()
        {
            _fireCooldown -= Time.deltaTime;
            if (_fireCooldown > 0f) return;
            _fireCooldown = HeroFireCooldown;

            // Face the nearest hostile so the cast reads, then fire. Prefer the
            // hero's own primary slot (animates + scales + attributes); fall back
            // to a direct sweep so the tower keeps firing when on cooldown/mana.
            IDamageable target = NearestHostile(
                _heroTransform != null ? _heroTransform.position : transform.position, HeroFireRange);
            if (target != null && _heroTransform != null)
            {
                Vector3 face = target.WorldPosition - _heroTransform.position; face.y = 0f;
                if (face.sqrMagnitude > 0.01f)
                    _heroTransform.rotation = Quaternion.Slerp(
                        _heroTransform.rotation, Quaternion.LookRotation(face), 12f * Time.deltaTime);
            }

            if (_hero != null && _hero.TryCast(AbilitySlot.Q)) return;
            if (target == null) return;
            target.TakeDamage(HeroFireDamage, DamageElement.None);
            DamageAttribution.Record(target, HeroProgression.Id, HeroFireDamage);
        }

        private IDamageable NearestHostile(Vector3 origin, float range)
        {
            int count = Physics.OverlapSphereNonAlloc(
                origin, range, _overlap, ~0, QueryTriggerInteraction.Collide);
            IDamageable best = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var col = _overlap[i];
                if (col == null) continue;
                var dmg = col.GetComponentInParent<IDamageable>();
                if (dmg == null || !dmg.IsAlive || dmg.Faction != CombatFaction.Hostile) continue;
                float sqr = (dmg.WorldPosition - origin).sqrMagnitude;
                if (sqr < bestSqr) { bestSqr = sqr; best = dmg; }
            }
            return best;
        }

        // =====================================================================
        //  Pets — reuse DeNelle.Pets.Pet via PetDeployer + the Attack/Repair adapter
        // =====================================================================

        /// <summary>
        /// Fields a handful of pets via the existing <see cref="PetDeployer"/>
        /// (same meshes / textures / bond ranks as the village), then attaches a
        /// <see cref="PetRepairAdapter"/> to each so the HUD can flip it between
        /// Attack (the Pet's own Defend hunt AI) and Repair (move to tower + heal).
        /// </summary>
        private void SpawnPets()
        {
            var deployerGo = new GameObject("PetDeployer");
            deployerGo.transform.SetParent(transform, false);
            var deployer = deployerGo.AddComponent<PetDeployer>();
            deployer.SetHeartPosition(TowerPos);
            deployer.SetEnemyMask(_enemyMask);

            int aether = 0, flame = 0, ice = 0;
            var svc = GameStateService.Instance;
            if (svc != null && svc.State != null && svc.State.PetBonds != null)
            {
                var b = svc.State.PetBonds;
                if (b.Count > 0) aether = b[0];
                if (b.Count > 1) flame = b[1];
                if (b.Count > 2) ice = b[2];
            }
            deployer.SetBondRanks(aether, flame, ice);
            deployer.DeployStarterPets();

            int added = 0;
            foreach (Pet pet in deployer.DeployedPets)
            {
                if (pet == null || added >= PetCount) continue;
                // The village leash drags pets toward the hero; here they ring
                // the tower instead, so drop it.
                var leash = pet.GetComponent<PetHeroLeash>();
                if (leash != null) Destroy(leash);

                var adapter = pet.gameObject.AddComponent<PetRepairAdapter>();
                adapter.Initialize(pet, _heart, repairPerSecond: 4f);
                _pets.Add(adapter);
                added++;
            }
        }

        // =====================================================================
        //  Assault — 4 spawners, 5 waves + a boss
        // =====================================================================

        private async UniTask RunAssault()
        {
            for (int wave = 1; wave <= WaveCount; wave++)
            {
                if (!_running) return;
                _currentWave = wave;
                RefreshHud();
                SetStatus($"Wave {wave} of {WaveCount} — defend the tower!");

                int perLane = 2 + wave;     // ramps: 3,4,5,6,7 per lane
                await SpawnWave(perLane);

                // Hold until the field is clear (or the tower falls) before the next wave.
                await WaitForFieldClear();
                if (!_running) return;

                if (wave < WaveCount)
                    await UniTask.Delay(TimeSpan.FromSeconds(WaveGapSeconds));
            }

            // ── Boss ──────────────────────────────────────────────────────────
            if (!_running) return;
            _currentWave = WaveCount + 1;
            RefreshHud();
            SetStatus("The Necromancer comes — repel the boss!");
            await SpawnBoss();
            await WaitForFieldClear();

            if (_running) Win();
        }

        /// <summary>
        /// Releases one wave across the four lanes: Left/Right Ground + Left/Right
        /// Air, <paramref name="perLane"/> enemies each, staggered by SpawnInterval.
        /// </summary>
        private async UniTask SpawnWave(int perLane)
        {
            for (int i = 0; i < perLane; i++)
            {
                if (!_running) return;
                SpawnAtLane(-1f, ground: true);   // Left  Ground
                SpawnAtLane(+1f, ground: true);   // Right Ground
                SpawnAtLane(-1f, ground: false);  // Left  Air
                SpawnAtLane(+1f, ground: false);  // Right Air
                await UniTask.Delay(TimeSpan.FromSeconds(SpawnInterval));
            }
        }

        /// <summary>Spawns one enemy on a lane (side = -1 left / +1 right; ground or air).</summary>
        private void SpawnAtLane(float side, bool ground)
        {
            if (_groundDef == null) return;

            float dist = ground ? GroundSpawnDist : AirSpawnDist;
            // Slight depth jitter so the lane isn't a single file.
            float depth = UnityEngine.Random.Range(-4f, 4f);
            Vector3 pos = TowerPos + new Vector3(side * dist, ground ? 0f : AirHeight, depth);

            string id = $"pl-w{_currentWave}-{(ground ? "g" : "a")}-{_idCounter++}";
            Enemy enemy = SpawnEnemy(_groundDef, pos, ground);
            if (enemy == null) return;

            enemy.Configure(id, _groundDef, _towerTransform);
            enemy.Died += HandleEnemyDied;
            enemy.ReachedHeart += HandleEnemyReachedHeart;
            if (!ground) enemy.gameObject.AddComponent<AirEnemyTag>(); // marks it for the air glide
            _liveEnemies.Add(enemy);
        }

        /// <summary>
        /// Instantiates a bare <see cref="Enemy"/> (capsule body + NavMeshAgent +
        /// EnemyDamageable) for the arena. Mirrors WaveManager.BuildPlaceholderEnemy
        /// — this scene has no authored enemy prefab, and a primitive enemy is all
        /// the slice needs. Ground enemies snap to the baked NavMesh; air enemies
        /// keep their agent disabled and glide kinematically (DriveAirEnemies).
        /// </summary>
        private Enemy SpawnEnemy(EnemyDef def, Vector3 pos, bool ground)
        {
            if (ground && NavMesh.SamplePosition(pos, out var hit, 8f, NavMesh.AllAreas))
                pos = hit.position;

            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = ground ? "Enemy (ground)" : "Enemy (air)";
            int layer = LayerMask.NameToLayer("Enemy");
            if (layer >= 0) go.layer = layer;
            go.transform.SetParent(transform, false);
            go.transform.position = pos;
            TintUrp(go, ground ? new Color(0.6f, 0.25f, 0.25f) : new Color(0.55f, 0.3f, 0.6f));

            // The capsule's own collider must be a trigger so it does not block the
            // hero/pet sphere sweeps' QueryTriggerInteraction.Collide finds it but
            // Enemy's own contact probe (Ignore) skips it.
            if (go.TryGetComponent(out Collider col)) col.isTrigger = true;

            var agent = go.AddComponent<NavMeshAgent>();
            if (!ground) agent.enabled = false; // air enemies glide, not nav-walk

            var enemy = go.AddComponent<Enemy>();
            if (go.GetComponent<EnemyDamageable>() == null) go.AddComponent<EnemyDamageable>();
            return enemy;
        }

        /// <summary>
        /// Glides air enemies toward the tower (their NavMeshAgent is disabled).
        /// They descend onto the balcony height as they close so the hero can hit
        /// them; arrival is detected here and routed through the same Heart-hit.
        /// </summary>
        private void DriveAirEnemies()
        {
            for (int i = _liveEnemies.Count - 1; i >= 0; i--)
            {
                Enemy e = _liveEnemies[i];
                if (e == null || e.IsDead || e.GetComponent<AirEnemyTag>() == null) continue;

                Vector3 self = e.transform.position;
                Vector3 target = TowerPos + new Vector3(0f, BalconyPos.y, 0f);
                Vector3 to = target - self;
                float dist = to.magnitude;

                if (dist <= 2.5f)
                {
                    HandleEnemyReachedHeart(e);
                    continue;
                }

                float speed = 4.5f;
                e.transform.position = Vector3.MoveTowards(self, target, speed * Time.deltaTime);
                Vector3 face = to; face.y = 0f;
                if (face.sqrMagnitude > 0.01f)
                    e.transform.rotation = Quaternion.LookRotation(face);
            }
        }

        private async UniTask SpawnBoss()
        {
            // Prefer the apex DragonBoss prefab if one is reachable in Resources;
            // else field a high-HP ground enemy (the necromancer) as the boss.
            var bossPrefab = Resources.Load<DragonBoss>("Generated/Boss_Dragon")
                          ?? Resources.Load<DragonBoss>("Boss_Dragon");
            if (bossPrefab != null)
            {
                Vector3 spawn = TowerPos + new Vector3(0f, 16f, AirSpawnDist);
                _liveApexBoss = Instantiate(bossPrefab, spawn, Quaternion.identity, transform);
                _liveApexBoss.Configure($"pl-boss-{_idCounter++}", _towerTransform, 600f);
                _liveApexBoss.Died += HandleApexBossDied;
                return;
            }

            // Fallback boss: the highest-HP enemy in the catalog (the Necromancer,
            // via _bossDef), flying in down the centre and scaled up so it reads as
            // the boss. Its HP comes from its enemies.json def (Configure sets it).
            EnemyDef def = _bossDef ?? _groundDef;
            Vector3 pos = TowerPos + new Vector3(0f, AirHeight, AirSpawnDist);
            string id = $"pl-boss-{_idCounter++}";
            Enemy boss = SpawnEnemy(def, pos, ground: false);
            if (boss != null)
            {
                boss.Configure(id, def, _towerTransform);
                boss.transform.localScale *= 2.2f;   // visibly the boss
                boss.Died += HandleEnemyDied;
                boss.ReachedHeart += HandleEnemyReachedHeart;
                boss.gameObject.AddComponent<AirEnemyTag>();
                _liveEnemies.Add(boss);
            }
            await UniTask.CompletedTask;
        }

        private async UniTask WaitForFieldClear()
        {
            while (_running)
            {
                _liveEnemies.RemoveAll(e => e == null || e.IsDead);
                bool bossUp = _liveApexBoss != null && !_liveApexBoss.IsDead;
                if (_liveEnemies.Count == 0 && !bossUp) return;
                await UniTask.Delay(TimeSpan.FromMilliseconds(200));
            }
        }

        // =====================================================================
        //  Enemy event handlers
        // =====================================================================

        private void HandleEnemyDied(Enemy enemy)
        {
            if (enemy != null)
            {
                enemy.Died -= HandleEnemyDied;
                enemy.ReachedHeart -= HandleEnemyReachedHeart;
            }
            _liveEnemies.Remove(enemy);
        }

        private void HandleEnemyReachedHeart(Enemy enemy)
        {
            if (!_running || _heart == null) return;

            _heart.SetHp(_heart.Hp - HeartHitDamage);
            RefreshHud();

            if (enemy != null)
            {
                enemy.Died -= HandleEnemyDied;
                enemy.ReachedHeart -= HandleEnemyReachedHeart;
                _liveEnemies.Remove(enemy);
                enemy.Kill(); // breached, not slain — no kill XP
            }
        }

        private void HandleApexBossDied(DragonBoss boss)
        {
            if (boss != null) boss.Died -= HandleApexBossDied;
            if (_liveApexBoss == boss) _liveApexBoss = null;
        }

        // =====================================================================
        //  Win / lose
        // =====================================================================

        private void Win()
        {
            if (_resolved) return;
            _resolved = true;
            _running = false;

            // Repel anything still standing.
            RepelAll();

            var wisdom = DeNelle.Village.Talents.WisdomCurrencyService.Instance;
            if (wisdom != null) wisdom.Grant(WisdomReward);

            SetStatus($"The tower holds! +{WisdomReward} Wisdom");
            Debug.Log($"[PatriciaLight] WIN — assault repelled, +{WisdomReward} Wisdom. Returning to {_returnScene}.");
            FinishAndReturn().Forget();
        }

        private void Lose()
        {
            if (_resolved) return;
            _resolved = true;
            _running = false;

            SetStatus("The tower has fallen…");
            Debug.Log("[PatriciaLight] LOSE — tower integrity reached 0. Returning home.");
            FinishAndReturn().Forget();
        }

        private void RepelAll()
        {
            foreach (Enemy e in _liveEnemies)
                if (e != null) e.Kill();
            _liveEnemies.Clear();
            if (_liveApexBoss != null)
            {
                _liveApexBoss.Died -= HandleApexBossDied;
                Destroy(_liveApexBoss.gameObject);
                _liveApexBoss = null;
            }
        }

        private async UniTask FinishAndReturn()
        {
            await UniTask.Delay(TimeSpan.FromSeconds(1.8f));
            ReturnHome();
        }

        private void ReturnHome()
        {
            if (_returnScene == SceneRouter.Village) SceneRouter.GoVillage();
            else SceneRouter.LoadScene(_returnScene);
        }

        // =====================================================================
        //  Last-chance lighting mood (optional polish)
        // =====================================================================

        /// <summary>
        /// Dims + tints the scene to a tense "last chance" mood: lowers ambient,
        /// pushes the key light to a low amber. Cheap + scene-local; skipped
        /// silently if no light is present.
        /// </summary>
        private void ApplyLastChanceLighting()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.16f, 0.12f, 0.14f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.012f;
            RenderSettings.fogColor = new Color(0.12f, 0.08f, 0.12f);

            var key = UnityEngine.Object.FindAnyObjectByType<Light>();
            if (key != null && key.type == LightType.Directional)
            {
                key.color = new Color(1f, 0.62f, 0.42f); // low amber
                key.intensity = 0.7f;
            }
        }

        // =====================================================================
        //  HUD — code-built UI Toolkit (WO-47: no UXML)
        // =====================================================================

        private void BuildHud()
        {
            // The dedicated scene's UIDocument host carries a PanelSettings; borrow
            // it. If none exists (host not built), create one so the HUD renders —
            // a null-PanelSettings UIDocument draws nothing (the intro regression).
            PanelSettings ps = BorrowOrCreatePanelSettings(out float topSort);

            var go = new GameObject("PatriciaLightHud");
            go.transform.SetParent(transform, false);
            go.SetActive(false);
            _hudDoc = go.AddComponent<UIDocument>();
            _hudDoc.panelSettings = ps;
            _hudDoc.sortingOrder = topSort + 50;
            go.SetActive(true);

            VisualElement root = _hudDoc.rootVisualElement;
            if (root == null) return;
            root.Clear();
            root.pickingMode = PickingMode.Ignore;

            // Title.
            var title = new Label("DEFEND THE TOWER");
            title.style.position = Position.Absolute;
            title.style.top = 14f; title.style.left = 0f; title.style.right = 0f;
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            title.style.color = new StyleColor(new Color(0.97f, 0.92f, 0.74f, 1f));
            title.style.fontSize = 24f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            root.Add(title);

            _waveLabel = new Label(string.Empty);
            _waveLabel.style.position = Position.Absolute;
            _waveLabel.style.top = 44f; _waveLabel.style.left = 0f; _waveLabel.style.right = 0f;
            _waveLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _waveLabel.style.color = new StyleColor(Color.white);
            _waveLabel.style.fontSize = 15f;
            root.Add(_waveLabel);

            _statusLabel = new Label(string.Empty);
            _statusLabel.style.position = Position.Absolute;
            _statusLabel.style.top = 66f; _statusLabel.style.left = 0f; _statusLabel.style.right = 0f;
            _statusLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _statusLabel.style.color = new StyleColor(new Color(0.86f, 0.86f, 0.92f, 1f));
            _statusLabel.style.fontSize = 13f;
            root.Add(_statusLabel);

            // ── Tower Integrity slider (horizontal bar + gradient + N/Max text) ─
            var barFrame = new VisualElement();
            barFrame.style.position = Position.Absolute;
            barFrame.style.bottom = 28f; barFrame.style.left = 0f; barFrame.style.right = 0f;
            barFrame.style.alignItems = Align.Center;
            root.Add(barFrame);

            var caption = new Label("TOWER INTEGRITY");
            caption.style.color = new StyleColor(new Color(0.78f, 0.80f, 0.78f, 1f));
            caption.style.fontSize = 11f;
            caption.style.marginBottom = 4f;
            barFrame.Add(caption);

            var track = new VisualElement();
            track.style.width = 360f; track.style.height = 22f;
            track.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.55f));
            track.style.borderTopLeftRadius = 6f; track.style.borderTopRightRadius = 6f;
            track.style.borderBottomLeftRadius = 6f; track.style.borderBottomRightRadius = 6f;
            track.style.justifyContent = Justify.FlexStart;
            track.style.flexDirection = FlexDirection.Row;
            barFrame.Add(track);

            _hpFill = new VisualElement();
            _hpFill.style.width = Length.Percent(100f);
            _hpFill.style.height = Length.Percent(100f);
            _hpFill.style.backgroundColor = new StyleColor(new Color(0.30f, 0.85f, 0.40f, 1f));
            _hpFill.style.borderTopLeftRadius = 6f; _hpFill.style.borderBottomLeftRadius = 6f;
            track.Add(_hpFill);

            _hpLabel = new Label("100 / 100");
            _hpLabel.style.marginTop = 3f;
            _hpLabel.style.color = new StyleColor(new Color(0.88f, 0.92f, 0.88f, 1f));
            _hpLabel.style.fontSize = 12f;
            _hpLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            barFrame.Add(_hpLabel);

            BuildPetToggles(root);
            RefreshHud();
        }

        /// <summary>One Attack/Repair toggle button per fielded pet, lower-left.</summary>
        private void BuildPetToggles(VisualElement root)
        {
            _petButtons.Clear();
            if (_pets.Count == 0) return;

            var column = new VisualElement();
            column.style.position = Position.Absolute;
            column.style.left = 16f; column.style.bottom = 24f;
            root.Add(column);

            for (int i = 0; i < _pets.Count; i++)
            {
                int idx = i;
                PetRepairAdapter pet = _pets[i];
                string label = pet.Pet != null && !string.IsNullOrEmpty(pet.Pet.Species)
                    ? pet.Pet.Species : $"Pet {i + 1}";

                var btn = new Button(() =>
                {
                    pet.Toggle();
                    UpdatePetButton(idx);
                });
                btn.style.width = 150f; btn.style.height = 34f;
                btn.style.marginTop = 4f;
                btn.style.fontSize = 12f;
                btn.style.unityFontStyleAndWeight = FontStyle.Bold;
                btn.style.color = new StyleColor(Color.white);
                btn.style.borderTopLeftRadius = 8f; btn.style.borderTopRightRadius = 8f;
                btn.style.borderBottomLeftRadius = 8f; btn.style.borderBottomRightRadius = 8f;
                btn.style.borderTopWidth = 0f; btn.style.borderBottomWidth = 0f;
                btn.style.borderLeftWidth = 0f; btn.style.borderRightWidth = 0f;
                btn.userData = label;
                column.Add(btn);
                _petButtons.Add(btn);
                UpdatePetButton(idx);
            }
        }

        private void UpdatePetButton(int idx)
        {
            if (idx < 0 || idx >= _petButtons.Count || idx >= _pets.Count) return;
            Button btn = _petButtons[idx];
            PetRepairAdapter pet = _pets[idx];
            string label = btn.userData as string ?? $"Pet {idx + 1}";
            bool attacking = pet.CurrentRole == PetRepairAdapter.Role.Attack;
            btn.text = $"{label}: {(attacking ? "Attack" : "Repair")}";
            btn.style.backgroundColor = new StyleColor(attacking
                ? new Color(0.45f, 0.16f, 0.16f, 0.95f)   // attack red
                : new Color(0.18f, 0.42f, 0.30f, 0.95f));  // repair green
        }

        private PanelSettings BorrowOrCreatePanelSettings(out float topSort)
        {
            PanelSettings ps = null;
            topSort = 0f;
            foreach (var d in UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsInactive.Include))
            {
                if (d == null || d.panelSettings == null) continue;
                if (d.sortingOrder >= topSort) { topSort = d.sortingOrder; ps = d.panelSettings; }
            }
            if (ps != null) return ps;

            // None in the scene — create one at runtime so the HUD renders.
            _ownPanelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _ownPanelSettings.name = "PatriciaLightPanelSettings";
            var themeFromAny = Resources.Load<ThemeStyleSheet>("UnityThemes/UnityDefaultRuntimeTheme");
            if (themeFromAny != null) _ownPanelSettings.themeStyleSheet = themeFromAny;
            return _ownPanelSettings;
        }

        private void RefreshHud()
        {
            if (_waveLabel != null)
                _waveLabel.text = _currentWave > WaveCount
                    ? "BOSS"
                    : $"Wave {Mathf.Clamp(_currentWave, 1, WaveCount)} / {WaveCount}";

            if (_heart == null) return;
            float frac = Mathf.Clamp01(_heart.Hp / TowerMaxHp);
            if (_hpFill != null)
            {
                _hpFill.style.width = Length.Percent(frac * 100f);
                Color c = frac > 0.5f
                    ? Color.Lerp(new Color(0.95f, 0.75f, 0.20f), new Color(0.30f, 0.85f, 0.40f), (frac - 0.5f) / 0.5f)
                    : Color.Lerp(new Color(0.86f, 0.27f, 0.27f), new Color(0.95f, 0.75f, 0.20f), frac / 0.5f);
                _hpFill.style.backgroundColor = new StyleColor(c);
            }
            if (_hpLabel != null)
                _hpLabel.text = $"{Mathf.RoundToInt(_heart.Hp)} / {Mathf.RoundToInt(TowerMaxHp)}";
        }

        private void SetStatus(string text)
        {
            if (_statusLabel != null) _statusLabel.text = text;
        }

        // =====================================================================
        //  NavMesh — runtime bake under the arena so ground enemies can march
        // =====================================================================

        /// <summary>
        /// Builds a NavMesh over the flat arena plane at runtime so ground
        /// <see cref="Enemy"/> agents can path to the tower. Uses the runtime
        /// NavMeshBuilder (no editor-only bake). If unavailable, ground enemies
        /// hold position and log once; the air lanes + hero still resolve the win.
        /// </summary>
        private void BakeArenaNavMesh()
        {
            try
            {
                var sources = new List<NavMeshBuildSource>();
                // One large box source matching the arena floor (120 m plane).
                var box = new NavMeshBuildSource
                {
                    shape = NavMeshBuildSourceShape.Box,
                    transform = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one),
                    size = new Vector3(120f, 0.1f, 120f),
                    area = 0,
                };
                sources.Add(box);

                var settings = NavMesh.GetSettingsByID(0);
                var bounds = new Bounds(Vector3.zero, new Vector3(140f, 20f, 140f));
                var data = NavMeshBuilder.BuildNavMeshData(
                    settings, sources, bounds, Vector3.zero, Quaternion.identity);
                if (data != null) NavMesh.AddNavMeshData(data);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[PatriciaLight] Runtime NavMesh bake failed (air lanes still work): " + ex.Message);
            }
        }

        // =====================================================================
        //  Small helpers
        // =====================================================================

        private static void TintUrp(GameObject go, Color color)
        {
            var r = go.GetComponent<Renderer>();
            if (r == null) return;
            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (sh == null) return;
            var mat = new Material(sh);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color"))     mat.SetColor("_Color", color);
            r.sharedMaterial = mat;
        }

        private static void NormalizeHeight(GameObject go, float targetHeight)
        {
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends == null || rends.Length == 0) return;
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            if (b.size.y <= 0.01f) return;
            go.transform.localScale *= targetHeight / b.size.y;
        }

        private static void StripCollidersAndCameras(GameObject go)
        {
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) if (c != null) Destroy(c);
            foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true)) if (rb != null) Destroy(rb);
            foreach (var cam in go.GetComponentsInChildren<Camera>(true)) if (cam != null) Destroy(cam);
            foreach (var al in go.GetComponentsInChildren<AudioListener>(true)) if (al != null) Destroy(al);
        }

        private void OnDestroy()
        {
            if (_ownPanelSettings != null) Destroy(_ownPanelSettings);
        }
    }

    /// <summary>
    /// Marker for an air-lane enemy — its <c>NavMeshAgent</c> is disabled and
    /// <see cref="PatriciaLightController"/> glides it toward the tower instead.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AirEnemyTag : MonoBehaviour { }
}
