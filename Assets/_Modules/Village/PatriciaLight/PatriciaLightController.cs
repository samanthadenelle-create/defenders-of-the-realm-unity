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
        private const float HeroFireCooldown    = 0.14f;   // fast auto-fire — waves come in hard here
        private const float HeroFireRange       = 90f;     // direct-fire fallback reach
        private const float HeroFireDamage      = 18f;     // direct-fire fallback damage
        private const int   WisdomReward        = 8;       // Phase-2 win bonus (bigger than Phase-1's 3)
        private const float TowerMaxHp          = 100f;    // HeartController is 0..100
        private const int   PetCount            = 3;       // pets to field (3-4 spawn points per WO)

        private const string GroundEnemyId      = "hollow-walker";  // canonical ground enemy
        private const string BossEnemyId        = "necromancer";    // fallback boss when no apex prefab

        // Hero / tower layout in the arena.
        private static readonly Vector3 TowerPos    = Vector3.zero;
        private const float TowerHeight             = 12f;    // solid spire height (m) — feet at y=0
        // FIXED balcony height — the hero stands here. Deliberately a const, NOT
        // derived from a fragile model's world bounds (the shattered-Tripo trap).
        private const float BalconyHeight           = 8f;     // hero perch / platform height (m)
        private const float BalconyDepth            = 0f;     // centred on the tower axis
        // Hero balcony / spawn perch — a FIXED world point the hero, the air-glide
        // descent target and the platform mesh all share. No bounds derivation.
        private static readonly Vector3 _balconyPos = new Vector3(0f, BalconyHeight, BalconyDepth);
        // Bird's-eye framing: high ABOVE on +Y, only slightly BEHIND on -Z, so the
        // camera looks steeply DOWN at the tower/hero (owner: "needs more birds eye";
        // was 3.5,-9 which sat too low/behind). Tunable — raise Y / shrink -Z for more
        // overhead. (see ThirdPersonCameraFollow)
        // In FRONT of the hero (+Z) and high (+Y) — the look-at-hero geometry then
        // lands at ~70 degrees downward (atan(10.99/4) ~= 70).
        private static readonly Vector3 CamOffset   = new Vector3(0f, 12.5f, 4f);

        // ── State ─────────────────────────────────────────────────────────────
        private HeartController _heart;
        private Transform _towerTransform;
        private Bounds _spireBounds;   // world bounds of the fitted spire mesh
        private bool   _hasSpire;      // true once the real spire is loaded + fitted

        // Hero ledge — a simple square platform + rail in front of the tower the
        // hero stands on and strafes along (placeholder until the spire's own
        // ledge is wired). Set in BuildTower; read in SpawnHero + TickHeroStrafe.
        private Vector3 _heroLedgePos;
        private Quaternion _heroSpawnRot = Quaternion.identity;
        private bool       _hasSpawnRot;   // true when a baked HeroSpawn marker supplies the facing
        private float   _strafeCenterX;
        private bool    _arenaBaked;   // true when PatriciaLightSceneBuilder baked the arena into the scene
        private const float StrafeHalfWidth = 4.2f;
        private DeNelle.Village.Defend.HeroOverShoulderCamera _camFollow;

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

        // ── Polish: lighting + reliable combat FX ─────────────────────────────
        private LastChanceLightingPreset _lighting;
        private VisualElement _flashOverlay;    // red full-screen tower-hit pulse
        private Label _bossBanner;               // boss-intro banner

        // Simple code-driven UI tweens (UI Toolkit has no built-in tween).
        private float _flashTimer;               // seconds left on the red flash fade
        private float _flashPeak;                // starting alpha of the current flash
        private const float FlashDuration = 0.45f;
        private float _bannerTimer;              // seconds left the boss banner is shown
        private const float BannerDuration = 2.4f;

        // ── HUD ───────────────────────────────────────────────────────────────
        private UIDocument _hudDoc;
        private PanelSettings _ownPanelSettings;
        private VisualElement _hpFill;
        private Label _hpLabel;
        private Label _waveLabel;
        private Label _statusLabel;
        private readonly List<Button> _petButtons = new List<Button>();

        // Action bar — the player's tap targets (ATTACK + one button per ability
        // slot). Each ability button tracks its slot so Update can grey it out
        // while on cooldown / out of mana.
        private Button _attackButton;
        private readonly List<Button> _abilityButtons = new List<Button>();
        private readonly List<AbilitySlot> _abilitySlots = new List<AbilitySlot>();

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
            BuildMusic();
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
            // UI FX tweens run regardless of the assault state so a flash/banner
            // started right before win/lose still finishes its fade.
            TickFxTweens();

            if (!_running) return;

            // Prune dead / destroyed enemies.
            for (int i = _liveEnemies.Count - 1; i >= 0; i--)
            {
                Enemy e = _liveEnemies[i];
                if (e == null || e.IsDead) _liveEnemies.RemoveAt(i);
            }

            DriveAirEnemies();
            TickHeroStrafe();
            TickHeroFire();
            RefreshActionBar();

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
        /// <summary>Soft, looping battle music (Resources/Audio/bellssteel-panic).</summary>
        private void BuildMusic()
        {
            var clip = Resources.Load<AudioClip>("Audio/bellssteel-panic");
            if (clip == null)
            {
                Debug.LogWarning("[PatriciaLight] Resources/Audio/bellssteel-panic not found — no battle music.");
                return;
            }
            var go = new GameObject("BattleMusic");
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.loop = true;
            src.volume = 0.18f;        // soft background
            src.spatialBlend = 0f;     // 2D
            src.playOnAwake = false;
            src.Play();
        }

        private void BuildArena()
        {
            // If the scene was baked by PatriciaLightSceneBuilder, the arena objects
            // (floor / tower / stand / HeroSpawn) already exist — reuse them instead
            // of building at runtime (so the editor Hierarchy is the source of truth).
            _arenaBaked = GameObject.Find("DefendTowerArena") != null;

            if (!_arenaBaked)
            {
                // Solid floor: a big thick CUBE slab (not a one-sided Plane) whose top
                // sits just ABOVE y=0, so it overdraws any scene Terrain/Plane at y=0
                // and the coplanar z-fighting glitch (two ground surfaces) disappears.
                var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ground.name = "Arena Ground";
                ground.transform.SetParent(transform, false);
                ground.transform.position = new Vector3(0f, -0.08f, 0f);
                ground.transform.localScale = new Vector3(240f, 0.2f, 240f);
                TintUrp(ground, new Color(0.22f, 0.24f, 0.20f));
            }

            if (UnityEngine.Object.FindAnyObjectByType<Light>() == null)
            {
                var lightGo = new GameObject("Directional Light");
                lightGo.transform.SetParent(transform, false);
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                light.intensity = 1f;
            }

            // Atmospheric fog — OFF for now (owner). Re-enable + tune later:
            //   fog=true, ExponentialSquared, color (0.10,0.10,0.16), density ~0.0024.
            RenderSettings.fog = false;
        }

        /// <summary>
        /// Builds the central tower as a SOLID, upright procedural gothic keep — a
        /// guaranteed-intact structure the hero stands on, carrying the canonical
        /// <see cref="HeartController"/> (the tower HP the whole game can see — no
        /// forked HP). It is a fixed-scale stack of primitives:
        ///   • a wider base drum at the foot,
        ///   • a tall octagonal stone shaft (~12 m, feet at y=0),
        ///   • a balcony ring platform at the FIXED <see cref="BalconyHeight"/> the
        ///     hero stands on,
        ///   • a ring of crenellation cubes around the balcony's edge.
        /// Built from primitives (not the shattered Tripo FBX) so it ALWAYS reads
        /// as a strong, solid tower regardless of import / material quirks. No
        /// world-bounds recenter hack: every piece sits at a known local offset.
        /// </summary>
        private void BuildTower()
        {
            // Baked scene path: reuse the editor-authored arena objects and skip the
            // whole runtime build (tower2 + stand).
            if (_arenaBaked) { UseBakedArena(); return; }

            var towerGo = new GameObject("Tower");
            towerGo.transform.SetParent(transform, false);
            towerGo.transform.position = TowerPos;
            _towerTransform = towerGo.transform;

            Color stone     = new Color(0.46f, 0.45f, 0.50f); // weathered grey stone
            Color stoneDark = new Color(0.34f, 0.33f, 0.39f); // shadowed base / merlons

            // ── Base drum: a wide squat cylinder anchoring the tower to the ground ─
            // Cylinder primitive is 2 m tall at scale 1, so localScale.y = halfHeight.
            const float baseHeight = 2.4f;
            var baseDrum = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            baseDrum.name = "TowerBase";
            baseDrum.transform.SetParent(towerGo.transform, false);
            baseDrum.transform.localScale = new Vector3(6.5f, baseHeight * 0.5f, 6.5f);
            baseDrum.transform.localPosition = new Vector3(0f, baseHeight * 0.5f, 0f);
            TintUrp(baseDrum, stoneDark);

            // ── Main shaft: a tall octagonal keep (an 8-sided cylinder reads gothic) ─
            // Feet at y=0, top at TowerHeight (~12 m). Cylinder height = 2 m @ scale 1.
            var shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shaft.name = "TowerShaft";
            shaft.transform.SetParent(towerGo.transform, false);
            shaft.transform.localScale = new Vector3(4.6f, TowerHeight * 0.5f, 4.6f);
            shaft.transform.localRotation = Quaternion.Euler(0f, 22.5f, 0f); // octagonal facets read
            shaft.transform.localPosition = new Vector3(0f, TowerHeight * 0.5f, 0f);
            TintUrp(shaft, stone);

            // ── Balcony ring platform: the SOLID disc the hero's feet rest on ──────
            // Its TOP surface sits exactly at BalconyHeight (the fixed perch). The
            // cylinder is centred, so push it down by its half-thickness.
            const float platformThickness = 0.5f;
            var balcony = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            balcony.name = "Balcony";
            balcony.transform.SetParent(towerGo.transform, false);
            balcony.transform.localScale = new Vector3(5.6f, platformThickness * 0.5f, 5.6f);
            balcony.transform.localPosition = new Vector3(0f, BalconyHeight - platformThickness * 0.5f, 0f);
            TintUrp(balcony, stoneDark);

            // ── Crenellations: a ring of merlon cubes around the balcony edge ──────
            const int merlons = 12;
            const float merlonRadius = 2.7f;
            for (int i = 0; i < merlons; i++)
            {
                float ang = (i / (float)merlons) * Mathf.PI * 2f;
                var merlon = GameObject.CreatePrimitive(PrimitiveType.Cube);
                merlon.name = "Merlon";
                merlon.transform.SetParent(towerGo.transform, false);
                merlon.transform.localScale = new Vector3(0.7f, 1.1f, 0.7f);
                merlon.transform.localPosition = new Vector3(
                    Mathf.Cos(ang) * merlonRadius,
                    BalconyHeight + 0.55f,
                    Mathf.Sin(ang) * merlonRadius);
                TintUrp(merlon, stoneDark);
            }

            // ── Roof cap: a cone-ish spire on top so the keep reads finished ───────
            const float capHeight = 3f;
            var cap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cap.name = "TowerCap";
            cap.transform.SetParent(towerGo.transform, false);
            cap.transform.localScale = new Vector3(3.4f, capHeight * 0.5f, 3.4f);
            cap.transform.localPosition = new Vector3(0f, TowerHeight + capHeight * 0.5f, 0f);
            TintUrp(cap, new Color(0.30f, 0.20f, 0.26f)); // dark slate roof

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

            // ── Swap the placeholder blocks for the authored spire ────────────────
            // Resources/PatriciaLight/Tower is the real "Defend the Tower" structure
            // (explicitly placed for this mode). Hide the primitive blocks (keeping
            // their colliders + the HeartController for gameplay) and show the real
            // mesh, fitted by renderer bounds so any FBX import scale / off-pivot is
            // normalised (the Tripo displacement trap).
            var spirePrefab = Resources.Load<GameObject>("PatriciaLight/tower2");
            if (spirePrefab != null)
            {
                foreach (var mr in towerGo.GetComponentsInChildren<MeshRenderer>())
                    mr.enabled = false;   // hide placeholder blocks; colliders stay

                var spire = Instantiate(spirePrefab, towerGo.transform);
                spire.name = "TowerSpire";
                // tower2 imports lying on its side — stand it upright. -90 about X is
                // the usual Z-up-FBX fix; owner can confirm/adjust live in the editor.
                spire.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                spire.AddComponent<DeNelle.Core.TripoMaterialFixer>();   // Tripo -> URP on Awake
                _spireBounds = FitToTowerFootprint(spire, TowerHeight + 5f);
                _hasSpire = true;
            }
            else
            {
                Debug.LogWarning("[PatriciaLight] Resources/PatriciaLight/tower2 not found — keeping placeholder blocks.");
            }

            BuildHeroStand();
        }

        /// <summary>
        /// Wires gameplay to the editor-baked arena (DefendTowerArena): the baked
        /// Tower becomes the enemy target, a clean unscaled hit-box at the tower
        /// position carries the HeartController (HP) + collider so enemies can
        /// register attacks, and HeroSpawn becomes the hero's perch. No runtime
        /// geometry is created — the editor Hierarchy is the source of truth.
        /// </summary>
        private void UseBakedArena()
        {
            var arena = GameObject.Find("DefendTowerArena");
            Transform towerT = arena != null ? arena.transform.Find("Tower") : null;
            _towerTransform = towerT != null ? towerT : (arena != null ? arena.transform : transform);

            // The baked FBX has no collider, and its authored scale/rotation make a
            // direct box unreliable — so put HP + a hit-box on a clean unscaled child
            // at the tower's footprint. Enemies path to the tower and strike this.
            var hitGo = new GameObject("TowerHitBox");
            hitGo.transform.SetParent(transform, false);
            hitGo.transform.position = new Vector3(_towerTransform.position.x, 6f, _towerTransform.position.z);
            var box = hitGo.AddComponent<BoxCollider>();
            box.size = new Vector3(4f, 12f, 4f);

            _heart = hitGo.AddComponent<HeartController>();
            try
            {
                var f = typeof(HeartController).GetField("_useAuthoredTransform",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                f?.SetValue(_heart, true);
            }
            catch { }
            _heart.SetHp(TowerMaxHp);

            Transform spawn = arena != null ? arena.transform.Find("HeroSpawn") : null;
            _heroLedgePos  = spawn != null ? spawn.position : (TowerPos + _balconyPos);
            _strafeCenterX = _heroLedgePos.x;
            // Let the baked HeroSpawn marker drive the hero's facing — rotate the
            // marker in the editor to aim the hero/FP view (no code change needed).
            if (spawn != null) { _heroSpawnRot = spawn.rotation; _hasSpawnRot = true; }
        }

        /// <summary>
        /// Builds a simple square platform + front rail OUT IN FRONT of the tower
        /// (+Z) for the hero to stand on and strafe along. Placeholder until the
        /// spire's own ledge is wired; keeps the spire behind the FP camera.
        /// </summary>
        private void BuildHeroStand()
        {
            // A raised WOODEN STAND set BACK from the tower (+Z), facing it. The hero
            // defends from here in first person, looking out across the field at
            // tower2 (owner: "first person view from a stand facing the tower2 so we
            // have a better landscape view of everything").
            const float standTop = 7f;                                   // platform-top height
            float standZ = _hasSpire ? _spireBounds.max.z + 14f : 18f;   // distance back from the tower

            Color wood     = new Color(0.45f, 0.30f, 0.16f);
            Color woodDark = new Color(0.30f, 0.20f, 0.10f);

            var root = new GameObject("HeroStand");
            root.transform.SetParent(transform, false);

            var platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
            platform.name = "StandPlatform";
            platform.transform.SetParent(root.transform, false);
            platform.transform.position   = new Vector3(0f, standTop, standZ);
            platform.transform.localScale = new Vector3(7f, 0.5f, 5f);
            TintUrp(platform, wood);

            // Four corner legs down to the ground.
            foreach (var c in new[] { new Vector2(-3f, -2f), new Vector2(3f, -2f),
                                      new Vector2(-3f,  2f), new Vector2(3f,  2f) })
            {
                var leg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                leg.name = "StandLeg";
                leg.transform.SetParent(root.transform, false);
                leg.transform.position   = new Vector3(c.x, standTop * 0.5f, standZ + c.y);
                leg.transform.localScale = new Vector3(0.4f, standTop, 0.4f);
                TintUrp(leg, woodDark);
            }

            // Rail on the tower-facing (-Z) edge so it reads as a watch-stand.
            var rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rail.name = "StandRail";
            rail.transform.SetParent(root.transform, false);
            rail.transform.position   = new Vector3(0f, standTop + 0.55f, standZ - 2.3f);
            rail.transform.localScale = new Vector3(7f, 0.8f, 0.25f);
            TintUrp(rail, woodDark);

            // Hero stands near the tower-facing edge of the platform.
            _heroLedgePos  = new Vector3(0f, standTop + 0.35f, standZ - 1.3f);
            _strafeCenterX = 0f;
        }

        /// <summary>
        /// Scales a freshly-instantiated structure to <paramref name="targetHeight"/>
        /// by its combined renderer bounds, seats its base at the tower's ground
        /// point and centres it on the tower axis — robust to any FBX import scale
        /// or off-centre pivot.
        /// </summary>
        private static Bounds FitToTowerFootprint(GameObject go, float targetHeight)
        {
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return new Bounds(TowerPos, Vector3.one);

            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            if (b.size.y < 0.0001f) return b;

            go.transform.localScale *= targetHeight / b.size.y;

            // Recompute after scaling, then shift so the base sits at y=0 and the
            // mesh is centred on the tower axis (TowerPos).
            rends = go.GetComponentsInChildren<Renderer>();
            b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            Vector3 shift = new Vector3(
                TowerPos.x - b.center.x, TowerPos.y - b.min.y, TowerPos.z - b.center.z);
            go.transform.position += shift;
            b.center += shift;
            return b;   // world bounds of the seated spire
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
            // Stand the hero on the ledge built in front of the tower, facing OUT
            // over the field (+Z). The hero strafes left/right along it.
            heroRoot.transform.position = _heroLedgePos;
            // Facing: use the baked HeroSpawn marker's rotation when present (so you
            // aim the hero/FP view by rotating that marker in the editor); otherwise
            // default to facing the tower across the field.
            if (_hasSpawnRot)
            {
                heroRoot.transform.rotation = _heroSpawnRot;
            }
            else
            {
                Vector3 toTower = TowerPos - _heroLedgePos; toTower.y = 0f;
                heroRoot.transform.rotation = toTower.sqrMagnitude > 0.01f
                    ? Quaternion.LookRotation(toTower.normalized, Vector3.up)
                    : Quaternion.identity;
            }
            _heroTransform = heroRoot.transform;

            // Body from Resources/Heroes/<Class> (the canonical hero pickup path).
            var prefab = Resources.Load<GameObject>("Heroes/" + slug);
            if (prefab != null)
            {
                var body = Instantiate(prefab, heroRoot.transform);
                body.name = "HeroBody";
                body.transform.localPosition = Vector3.zero;
                body.transform.localRotation = Quaternion.Euler(0f, 90f, 0f); // -90 from 180: rotate hero 90 left so it faces the field + fires the right way
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

            // Camera: over-the-shoulder follow (hero-relative offset, looks past the
            // hero at the tower/battle). Bind to the rendering main camera, silence
            // EVERY other follow controller on it (incl. the baked DefendTowerCamera),
            // then hand the OTS rig the hero.
            var cam = Camera.main != null
                ? Camera.main
                : UnityEngine.Object.FindAnyObjectByType<Camera>();
            if (cam != null)
            {
                foreach (var vc in cam.GetComponents<VillageCamera>())                      vc.enabled = false;
                foreach (var sc in cam.GetComponents<SmartMobileCamera>())                  sc.enabled = false;
                foreach (var tp in cam.GetComponents<ThirdPersonCameraFollow>())            tp.enabled = false;
                foreach (var fp in cam.GetComponents<FirstPersonTowerCamera>())             fp.enabled = false;
                foreach (var dt in cam.GetComponents<DeNelle.Village.Defend.DefendTowerCamera>()) dt.enabled = false;
                _camFollow = cam.GetComponent<DeNelle.Village.Defend.HeroOverShoulderCamera>()
                          ?? cam.gameObject.AddComponent<DeNelle.Village.Defend.HeroOverShoulderCamera>();
            }
            else
            {
                _camFollow = UnityEngine.Object.FindAnyObjectByType<DeNelle.Village.Defend.HeroOverShoulderCamera>();
            }
            if (_camFollow != null)
            {
                _camFollow.enabled = true;
                _camFollow.Target  = _heroTransform;
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

        private void TickHeroStrafe()
        {
            if (_heroTransform == null) return;

            float x = 0f;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null)
            {
                if (kb.leftArrowKey.isPressed  || kb.aKey.isPressed) x -= 1f;
                if (kb.rightArrowKey.isPressed || kb.dKey.isPressed) x += 1f;
            }
            var gp = UnityEngine.InputSystem.Gamepad.current;
            if (gp != null) x += gp.leftStick.ReadValue().x;

            if (Mathf.Abs(x) < 0.01f) return;

            const float strafeSpeed = 9f;
            var p = _heroTransform.position;
            // Negated: left/right reads correctly from the over-the-shoulder view.
            p.x = Mathf.Clamp(
                p.x - Mathf.Clamp(x, -1f, 1f) * strafeSpeed * Time.deltaTime,
                _strafeCenterX - StrafeHalfWidth, _strafeCenterX + StrafeHalfWidth);
            _heroTransform.position = p;
        }

        private void TickHeroFire()
        {
            _fireCooldown -= Time.deltaTime;
            if (_fireCooldown > 0f) return;
            _fireCooldown = HeroFireCooldown;
            if (_heroTransform == null) return;

            // Auto-target the nearest hostile and fire a visible projectile at it.
            IDamageable target = NearestHostile(_heroTransform.position, HeroFireRange);
            if (target == null) return;

            Vector3 muzzle = _heroTransform.position + Vector3.up * 1.3f + _heroTransform.forward * 0.6f;
            StartCoroutine(FireProjectile(muzzle, target, HeroFireDamage));
        }

        /// <summary>Spawns a visible VFX projectile that flies from <paramref name="from"/>
        /// to <paramref name="target"/>, then plays an impact and applies damage.</summary>
        private System.Collections.IEnumerator FireProjectile(Vector3 from, IDamageable target, float damage)
        {
            if (target == null) yield break;

            var projGo = new GameObject("HeroShot");
            projGo.transform.position = from;
            VFXHandle handle = VFXManager.Instance != null
                ? VFXManager.Instance.PlayProjectile(VFXType.Projectile_Arrow, projGo.transform)
                : null;

            const float speed = 48f;
            while (target != null && target.IsAlive)
            {
                Vector3 tp  = target.WorldPosition + Vector3.up * 0.6f;
                Vector3 cur = projGo.transform.position;
                float step  = speed * Time.deltaTime;
                if ((tp - cur).sqrMagnitude <= step * step) { projGo.transform.position = tp; break; }
                Vector3 dir = (tp - cur).normalized;
                projGo.transform.position = cur + dir * step;
                projGo.transform.forward  = dir;
                yield return null;
            }

            Vector3 impactPos = projGo.transform.position;
            handle?.Stop();
            VFXManager.Play(VFXType.Impact_Physical, impactPos);
            if (target != null && target.IsAlive)
            {
                target.TakeDamage(damage, DamageElement.None);
                DamageAttribution.Record(target, HeroProgression.Id, damage);
            }
            Destroy(projGo);
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

        /// <summary>Nearest live hostile within <paramref name="maxAngleDeg"/> of the
        /// horizontal <paramref name="aimDir"/> — i.e. the enemy under the crosshair.</summary>
        private IDamageable NearestHostileInCone(Vector3 origin, Vector3 aimDir, float range, float maxAngleDeg)
        {
            aimDir.y = 0f;
            if (aimDir.sqrMagnitude < 0.0001f) aimDir = Vector3.forward;
            aimDir.Normalize();
            float cosMax = Mathf.Cos(maxAngleDeg * Mathf.Deg2Rad);

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
                Vector3 to = dmg.WorldPosition - origin; to.y = 0f;
                if (to.sqrMagnitude < 0.0001f) continue;
                if (Vector3.Dot(to.normalized, aimDir) < cosMax) continue;   // outside the aim cone
                float sqr = to.sqrMagnitude;
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
                Vector3 target = TowerPos + new Vector3(0f, _balconyPos.y, 0f);
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
            // Boss intro flourish: banner + a stronger flash + a big camera shake.
            ShowBossFlourish();

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
            // Spawn off to the SIDE (-X) like the lane enemies, so the boss comes in
            // from the left toward the tower — not behind the tower or the hero.
            Vector3 pos = TowerPos + new Vector3(-AirSpawnDist, AirHeight, 0f);
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
                // A slain enemy (not a breach — that path unsubscribes first) gets a
                // small spark burst at its position. Floating damage numbers are
                // already handled by the damage path's DamageNumberSpawner.
                bool air = enemy.GetComponent<AirEnemyTag>() != null;
                SpawnDeathBurst(enemy.transform.position, air);

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
            FlashTowerHit(strong: false);   // red pulse + camera shake on every breach

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
            if (boss != null)
            {
                SpawnDeathBurst(boss.transform.position, air: true); // a bigger pop for the boss
                ShakeCamera(0.7f, 0.6f);
                boss.Died -= HandleApexBossDied;
            }
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
        /// Applies the dramatic "last chance" lighting mood by installing the
        /// dedicated <see cref="LastChanceLightingPreset"/> (dark flat ambient +
        /// amber/violet exp² fog + a warm raked amber KEY and a cool blue RIM for
        /// separation, with a subtle key flicker). Cheap + scene-local; no URP
        /// post-process Volume (those no-op in this project's setup).
        /// </summary>
        private void ApplyLastChanceLighting()
        {
            _lighting = LastChanceLightingPreset.Install(transform);
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

            BuildActionBar(root);
            BuildPetToggles(root);
            BuildFxOverlays(root);
            RefreshHud();
        }

        // =====================================================================
        //  Action bar — ATTACK + per-slot ability buttons (player taps to act)
        // =====================================================================

        /// <summary>
        /// Builds the bottom-centre control bar so the player can ACT: a large
        /// ATTACK button (fires the hero's primary at the nearest hostile) plus one
        /// button per remaining ability slot (W/E/R) read from the hero's class
        /// loadout via <see cref="AbilityCatalog"/>. Each ability button casts its
        /// own slot through <see cref="HeroAbilities.TryCast"/>, so cooldown + mana
        /// gates are respected; the auto-fire in <see cref="TickHeroFire"/> stays as
        /// a gentle fallback, but tapping ATTACK visibly fires Q on demand. Pure
        /// code-built UI Toolkit (WO-47: no UXML), sized as thumb-reachable tap
        /// targets for the portrait-ish layout.
        /// </summary>
        private void BuildActionBar(VisualElement root)
        {
            _abilityButtons.Clear();
            _abilitySlots.Clear();

            // A horizontal row pinned to the bottom-centre, above the integrity bar.
            var bar = new VisualElement();
            bar.style.position = Position.Absolute;
            bar.style.bottom = 84f;          // sits above the TOWER INTEGRITY bar (bottom:28)
            bar.style.left = 0f; bar.style.right = 0f;
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.justifyContent = Justify.Center;
            bar.style.alignItems = Align.FlexEnd;
            root.Add(bar);

            // ── Ability buttons for the non-primary slots (W/E/R) ─────────────────
            // Read the hero's actual class loadout; one button per ability beyond Q.
            string heroClass = _hero != null ? _hero.HeroClass : AbilityCatalog.DefaultClass;
            foreach (AbilitySlot slot in new[] { AbilitySlot.W, AbilitySlot.E, AbilitySlot.R })
            {
                AbilityDef def = AbilityCatalog.Find(heroClass, slot);
                if (def == null) continue; // class may not define every slot

                AbilitySlot captured = slot;
                var btn = new Button(() => CastSlot(captured));
                StyleAbilityButton(btn, def);
                bar.Add(btn);
                _abilityButtons.Add(btn);
                _abilitySlots.Add(slot);
            }

            // ── The big ATTACK button (primary Q) — last, so it sits rightmost and
            // is the largest / most thumb-reachable target. ──────────────────────
            _attackButton = new Button(FireAttack);
            _attackButton.text = "ATTACK";
            _attackButton.style.width = 130f; _attackButton.style.height = 80f;
            _attackButton.style.marginLeft = 10f;
            _attackButton.style.fontSize = 20f;
            _attackButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            _attackButton.style.color = new StyleColor(Color.white);
            _attackButton.style.backgroundColor = new StyleColor(new Color(0.62f, 0.18f, 0.18f, 0.96f));
            RoundCorners(_attackButton, 16f);
            ZeroBorders(_attackButton);
            bar.Add(_attackButton);
        }

        /// <summary>Styles one ability slot button: name on top, glyph below, tinted to the ability colour.</summary>
        private static void StyleAbilityButton(Button btn, AbilityDef def)
        {
            string glyph = !string.IsNullOrEmpty(def.Icon)
                ? def.Icon
                : (!string.IsNullOrEmpty(def.Name) ? def.Name.Substring(0, 1).ToUpperInvariant() : "?");
            string name = !string.IsNullOrEmpty(def.Name) ? def.Name : def.SlotEnum.ToString();
            btn.text = $"{glyph}\n{name}";

            btn.style.width = 92f; btn.style.height = 70f;
            btn.style.marginLeft = 8f; btn.style.marginRight = 0f;
            btn.style.fontSize = 13f;
            btn.style.whiteSpace = WhiteSpace.Normal;
            btn.style.unityFontStyleAndWeight = FontStyle.Bold;
            btn.style.unityTextAlign = TextAnchor.MiddleCenter;
            btn.style.color = new StyleColor(Color.white);
            Color c = def.UnityColor; c.a = 0.92f;
            btn.style.backgroundColor = new StyleColor(c);
            RoundCorners(btn, 12f);
            ZeroBorders(btn);
        }

        private static void RoundCorners(VisualElement ve, float r)
        {
            ve.style.borderTopLeftRadius = r; ve.style.borderTopRightRadius = r;
            ve.style.borderBottomLeftRadius = r; ve.style.borderBottomRightRadius = r;
        }

        private static void ZeroBorders(VisualElement ve)
        {
            ve.style.borderTopWidth = 0f; ve.style.borderBottomWidth = 0f;
            ve.style.borderLeftWidth = 0f; ve.style.borderRightWidth = 0f;
        }

        /// <summary>
        /// ATTACK tap — faces the hero at the nearest hostile so the cast reads,
        /// then fires the primary (Q). Casting through HeroAbilities animates +
        /// scales + attributes the shot. If Q is somehow unavailable (no catalog),
        /// fall back to the same direct damage sweep the auto-fire uses so the tap
        /// always does SOMETHING the player can see.
        /// </summary>
        private void FireAttack()
        {
            FaceNearestHostile();
            if (_hero != null && _hero.TryCast(AbilitySlot.Q)) return;

            IDamageable target = NearestHostile(
                _heroTransform != null ? _heroTransform.position : transform.position, HeroFireRange);
            if (target == null) return;
            target.TakeDamage(HeroFireDamage, DamageElement.None);
            DamageAttribution.Record(target, HeroProgression.Id, HeroFireDamage);
        }

        /// <summary>Ability-slot tap — faces the nearest hostile, then casts that slot (respects cooldown/mana).</summary>
        private void CastSlot(AbilitySlot slot)
        {
            FaceNearestHostile();
            if (_hero != null) _hero.TryCast(slot);
        }

        /// <summary>Snaps the hero to face the nearest hostile so a tapped cast reads (no slerp — instant on tap).</summary>
        private void FaceNearestHostile()
        {
            if (_heroTransform == null) return;
            IDamageable target = NearestHostile(_heroTransform.position, HeroFireRange);
            if (target == null) return;
            Vector3 face = target.WorldPosition - _heroTransform.position; face.y = 0f;
            if (face.sqrMagnitude > 0.01f)
                _heroTransform.rotation = Quaternion.LookRotation(face);
        }

        /// <summary>Greys out ability buttons that are on cooldown / out of mana so the bar reads as live.</summary>
        private void RefreshActionBar()
        {
            if (_hero == null) return;
            for (int i = 0; i < _abilityButtons.Count && i < _abilitySlots.Count; i++)
            {
                bool ready = _hero.CanCast(_abilitySlots[i]);
                _abilityButtons[i].style.opacity = ready ? 1f : 0.45f;
            }
        }

        /// <summary>
        /// Builds the code-only FX overlays on the HUD root: a full-screen red
        /// pulse element (flashed on tower damage) and a hidden boss-intro banner.
        /// Both are pure VisualElements (no UXML, per WO-47) and ignore picking so
        /// they never eat input.
        /// </summary>
        private void BuildFxOverlays(VisualElement root)
        {
            // Red full-screen tower-hit pulse — starts fully transparent.
            _flashOverlay = new VisualElement();
            _flashOverlay.pickingMode = PickingMode.Ignore;
            _flashOverlay.style.position = Position.Absolute;
            _flashOverlay.style.top = 0f; _flashOverlay.style.left = 0f;
            _flashOverlay.style.right = 0f; _flashOverlay.style.bottom = 0f;
            _flashOverlay.style.backgroundColor = new StyleColor(new Color(0.85f, 0.10f, 0.10f, 0f));
            root.Add(_flashOverlay);

            // Boss-intro banner — centred, hidden until the boss spawns.
            _bossBanner = new Label("THE NECROMANCER APPROACHES");
            _bossBanner.pickingMode = PickingMode.Ignore;
            _bossBanner.style.position = Position.Absolute;
            _bossBanner.style.top = Length.Percent(40f);
            _bossBanner.style.left = 0f; _bossBanner.style.right = 0f;
            _bossBanner.style.unityTextAlign = TextAnchor.MiddleCenter;
            _bossBanner.style.color = new StyleColor(new Color(1f, 0.30f, 0.28f, 1f));
            _bossBanner.style.fontSize = 30f;
            _bossBanner.style.unityFontStyleAndWeight = FontStyle.Bold;
            _bossBanner.style.opacity = 0f;
            root.Add(_bossBanner);
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
        //  Reliable combat FX (no URP post-processing — WO-47)
        // =====================================================================

        /// <summary>Drives the code-only UI tweens (red flash fade + boss banner fade).</summary>
        private void TickFxTweens()
        {
            if (_flashTimer > 0f && _flashOverlay != null)
            {
                _flashTimer -= Time.deltaTime;
                float a = Mathf.Clamp01(_flashTimer / FlashDuration) * _flashPeak;
                _flashOverlay.style.backgroundColor = new StyleColor(new Color(0.85f, 0.10f, 0.10f, a));
            }

            if (_bannerTimer > 0f && _bossBanner != null)
            {
                _bannerTimer -= Time.deltaTime;
                float k = Mathf.Clamp01(_bannerTimer / BannerDuration); // 1 → 0
                // Fade in fast (first 20%), hold, then fade out over the last 40%.
                float opacity;
                float age = 1f - k;
                if (age < 0.2f) opacity = age / 0.2f;
                else if (k < 0.4f) opacity = k / 0.4f;
                else opacity = 1f;
                _bossBanner.style.opacity = opacity;
                if (_bannerTimer <= 0f) _bossBanner.style.opacity = 0f;
            }
        }

        /// <summary>
        /// Flashes the red full-screen pulse + shakes the camera — the tower took a
        /// hit. <paramref name="strong"/> drives a heavier pulse + shake (boss arrival).
        /// </summary>
        private void FlashTowerHit(bool strong)
        {
            _flashPeak = strong ? 0.55f : 0.32f;
            _flashTimer = FlashDuration;
            if (_flashOverlay != null)
                _flashOverlay.style.backgroundColor = new StyleColor(new Color(0.85f, 0.10f, 0.10f, _flashPeak));
            ShakeCamera(strong ? 0.6f : 0.22f, strong ? 0.5f : 0.28f);
        }

        /// <summary>The boss-intro flourish: banner + a stronger flash + a big shake.</summary>
        private void ShowBossFlourish()
        {
            if (_bossBanner != null)
            {
                _bossBanner.text = string.Equals(BossEnemyId, "necromancer", StringComparison.OrdinalIgnoreCase)
                    ? "THE NECROMANCER APPROACHES" : "THE BOSS APPROACHES";
                _bannerTimer = BannerDuration;
                _bossBanner.style.opacity = 0f;
            }
            _flashPeak = 0.6f;
            _flashTimer = FlashDuration;
            ShakeCamera(0.8f, 0.7f);
        }

        private void ShakeCamera(float intensity, float duration)
        {
            if (_camFollow != null) _camFollow.Shake(intensity, duration);
        }

        /// <summary>
        /// A small spark burst at a slain enemy. Reuses <see cref="AbilityVfxKit"/>'s
        /// generic impact (the Strike treatment: tracer-less spark + light flash)
        /// so death reads with a pop. Particle counts stay low (mobile, WO-47).
        /// </summary>
        private void SpawnDeathBurst(Vector3 at, bool air)
        {
            // Violet for air kills, ember-orange for ground — a readable, cheap pop.
            Color c = air ? new Color(0.70f, 0.45f, 0.95f) : new Color(1.0f, 0.55f, 0.25f);
            try
            {
                // Strike centred on the enemy with no foe hint → just the impact spark
                // + a brief light flash (no long tracer). Small radius keeps it cheap.
                AbilityVfxKit.SpawnAbilityVfx(AbilityEffect.Strike, c, at + Vector3.up * 0.6f, 0.6f, at + Vector3.up * 0.6f);
            }
            catch { /* VFX is non-essential — never let it break the loop */ }
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

        /// <summary>Strips any Animator the FBX brings (the static tower must not animate).</summary>
        private static void StripAnimators(GameObject go)
        {
            foreach (var a in go.GetComponentsInChildren<Animator>(true)) if (a != null) Destroy(a);
        }

        /// <summary>World-space encapsulated bounds of every renderer under <paramref name="go"/>.</summary>
        private static Bounds WorldBounds(GameObject go)
        {
            var rends = go.GetComponentsInChildren<Renderer>(true);
            Bounds b = default; bool has = false;
            foreach (var r in rends)
            {
                if (r == null) continue;
                if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
            }
            return has ? b : new Bounds(go.transform.position, Vector3.one);
        }

        /// <summary>Resolves a type by full name across loaded assemblies (so this
        /// asmdef needn't reference DeNelle.Core for the TripoMaterialFixer).</summary>
        private static Type FindTypeByName(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName, false);
                if (t != null) return t;
            }
            return null;
        }

        /// <summary>
        /// Safety-net Phong→URP material rebuild, used only if the canonical
        /// <c>DeNelle.Core.TripoMaterialFixer</c> type can't be found at runtime.
        /// Mirrors HeroBodySwapper.RetargetMaterialsToUrp (base colour + base map +
        /// normal carried across to a fresh URP/Lit) so the tower never renders as a
        /// magenta Phong ghost.
        /// </summary>
        private static void RetargetMaterialsToUrp(GameObject go)
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit")
                      ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                      ?? Shader.Find("Standard");
            if (lit == null) return;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                var mats = r.sharedMaterials;
                if (mats == null) continue;
                for (int i = 0; i < mats.Length; i++)
                {
                    var src = mats[i];
                    if (src == null) continue;
                    if (src.shader != null && src.shader.name.StartsWith(
                        "Universal Render Pipeline/", StringComparison.Ordinal)) continue;

                    Texture baseTex = null;
                    if (src.HasProperty("_MainTex")) baseTex = src.GetTexture("_MainTex");
                    if (baseTex == null && src.HasProperty("_BaseMap")) baseTex = src.GetTexture("_BaseMap");
                    Color baseColor = Color.white;
                    if (src.HasProperty("_BaseColor")) baseColor = src.GetColor("_BaseColor");
                    else if (src.HasProperty("_Color")) baseColor = src.GetColor("_Color");
                    Texture normalTex = null;
                    if (src.HasProperty("_BumpMap")) normalTex = src.GetTexture("_BumpMap");

                    var m = new Material(lit) { name = (src.name ?? "Tripo") + " (URP)" };
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", baseColor);
                    if (m.HasProperty("_Color"))     m.SetColor("_Color", baseColor);
                    if (baseTex != null)
                    {
                        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", baseTex);
                        if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", baseTex);
                    }
                    if (normalTex != null && m.HasProperty("_BumpMap"))
                    { m.SetTexture("_BumpMap", normalTex); m.EnableKeyword("_NORMALMAP"); }
                    mats[i] = m;
                }
                r.sharedMaterials = mats;
            }
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
