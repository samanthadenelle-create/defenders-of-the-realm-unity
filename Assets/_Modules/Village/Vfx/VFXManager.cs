// =============================================================================
// VFXManager — central singleton for all runtime VFX. DEF-VFX-01.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WHAT IT DOES:
//   • Object-pools prefab-based effects (Mirza Beig, Lana Studio, Spells Pack).
//   • Falls back to procedural AbilityVfxKit when no prefab is wired.
//   • Exposes a clear, minimal public API so any system can trigger VFX in one line.
//   • Respects VFXQuality (Low / Medium / High) for mobile performance.
//
// QUICK REFERENCE — how to call from anywhere:
//
//   // Oneshot at a world position:
//   VFXManager.Play(VFXType.Impact_Aether, position);
//   VFXManager.Play(VFXType.Death_Skeleton, enemy.transform.position);
//
//   // Typed helpers (match what the work order specifies):
//   VFXManager.Instance.PlayImpact(VFXType.Impact_Flame, pos, rot);
//   VFXManager.Instance.PlayCasting(VFXType.Cast_MageCharge, heroTransform);
//   VFXManager.Instance.PlayDeath(VFXType.Death_Boss, pos);
//
//   // Persistent loop — keep the handle and Stop() when done:
//   var handle = VFXManager.Instance.PlayAura(VFXType.Aura_Necromancer, transform);
//   handle.Stop();
//
//   // Environment (same as PlayAura, sugar name for readability):
//   var torchHandle = VFXManager.Instance.PlayEnvironment(VFXType.Env_TorchFlame, transform);
//
//   // Projectile (attaches to the projectile's transform):
//   var projHandle = VFXManager.Instance.PlayProjectile(VFXType.Projectile_ArcaneBolt, projTransform);
//   projHandle.Stop(); // call when projectile hits
//
//   // Pet aura that auto-selects level:
//   VFXManager.Instance.PlayPetAura(pet, petLevel);
//
//   // Quality control:
//   VFXManager.Instance.SetQuality(VFXQuality.Low);   // call from settings
//
// SETUP:
//   1. Add VFXManager to a persistent GameObject in your boot/loading scene.
//   2. Assign a VFXCatalog asset (Assets → Create → Defenders / VFX Catalog).
//   3. Wire prefabs in the catalog. Any un-wired entry uses procedural fallback.
// =============================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    // ── Quality enum ─────────────────────────────────────────────────────────

    /// <summary>Mobile-aware VFX quality level. Set via VFXManager.SetQuality().</summary>
    public enum VFXQuality
    {
        Low    = 0,   // oneshot impacts only; no auras, no environment loops
        Medium = 1,   // impacts + critical auras; limited particle counts
        High   = 2,   // all effects at full fidelity (default on desktop/high-end mobile)
    }

    // ── VFXManager ───────────────────────────────────────────────────────────

    /// <summary>
    /// Central singleton for all runtime VFX. Manages per-type object pools,
    /// quality gating, procedural fallback, and hit-stop + screen shake hooks.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VFXManager : MonoBehaviour
    {
        // ── Singleton ─────────────────────────────────────────────────────────

        public static VFXManager Instance { get; private set; }

        // WO-504: VFXManager is not authored into any scene/prefab, so without a
        // self-bootstrap its Instance is null and SpellVfxFactory / Enemy / Env calls
        // SILENTLY no-op or stay procedural. Self-create on load (mirrors VfxPool) so the
        // pooled, catalog-driven authored VFX path is always live. Idempotent - a scene
        // that DOES author a VFXManager wins (the first Awake claims Instance).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("[VFXManager]");
            go.AddComponent<VFXManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureCatalog();
            InitialisePools();
        }

        // WO-504: the VFXCatalog is a ScriptableObject asset (VFXType -> authored prefab).
        // VFXManager is NOT placed in any scene/prefab, so _catalog is never wired in an
        // inspector - without this, every effect falls back to procedural AbilityVfxKit.
        // Auto-load the script-generated catalog from Resources when none is assigned, so
        // the authored Lana/Spells/custom prefabs take effect with zero drag-drop. Only the
        // single asset (+ the prefabs it references) ships - no whole pack in Resources.
        private void EnsureCatalog()
        {
            if (_catalog != null) return;
            _catalog = Resources.Load<VFXCatalog>("VFX/VFXCatalog");
            if (_catalog == null)
                FlowTrace.Warn("VFXManager",
                    "EnsureCatalog: no _catalog assigned and Resources/VFX/VFXCatalog not found - " +
                    "ALL effects use procedural fallback. Run DeNelle.Editor.VFXCatalogGenerator.Generate.");
            else
                FlowTrace.Step("VFXManager",
                    $"EnsureCatalog: loaded VFXCatalog from Resources/VFX/VFXCatalog ({_catalog.Entries?.Length ?? 0} entries).");
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── Inspector fields ──────────────────────────────────────────────────

        [Header("Catalog (wire in Inspector)")]
        [Tooltip("ScriptableObject mapping VFXType → prefab + pool config.")]
        [SerializeField] private VFXCatalog _catalog;

        [Header("Quality")]
        [Tooltip("Starting quality level. Can be changed at runtime via SetQuality().")]
        [SerializeField] private VFXQuality _quality = VFXQuality.High;

        [Header("Performance Limits")]
        [Tooltip("Maximum simultaneous active oneshot GameObjects before new ones are skipped.")]
        [SerializeField, Min(1)] private int _maxActiveOneshots = 40;

        [Tooltip("Maximum simultaneous active aura/loop GameObjects.")]
        [SerializeField, Min(1)] private int _maxActiveLoops = 20;

        [Header("Dungeon (WO-59)")]
        [Tooltip("ScriptableObject with darker prefab overrides for dungeon scenes. " +
                 "Call ApplyDungeonMode(true) on dungeon load to activate.")]
        public DungeonVFXSettings dungeonSettings;

        [Header("Pool Root")]
        [Tooltip("Parent transform for pooled (inactive) objects. Auto-created if null.")]
        [SerializeField] private Transform _poolRoot;

        // ── Pool state ────────────────────────────────────────────────────────

        // Per-type queue of dormant (inactive) GameObjects ready to reuse.
        private readonly Dictionary<VFXType, Queue<GameObject>> _pools
            = new Dictionary<VFXType, Queue<GameObject>>();

        // Tracking how many active effects we currently have.
        private int _activeOneshots;
        private int _activeLoops;
        // bug-triage P1: track which pooled objects are loops so ReturnToPool decrements the
        // RIGHT counter (the old code guessed oneshot-first, drifting counters until VFX of a
        // class hit their cap and were silently skipped — combat went quiet mid-run).
        private readonly HashSet<GameObject> _loopObjects = new HashSet<GameObject>();

        // WO-59: dungeon mode flag — ApplyDungeonMode() toggles this.
        private bool _dungeonMode = false;

        // ── Quality ───────────────────────────────────────────────────────────

        /// <summary>Current VFX quality. Effects with MinQuality above this are skipped.</summary>
        public VFXQuality CurrentQuality => _quality;

        /// <summary>Change quality at runtime (e.g. from the Settings screen).</summary>
        public void SetQuality(VFXQuality q) => _quality = q;

        // ─────────────────────────────────────────────────────────────────────
        // ── PUBLIC API ────────────────────────────────────────────────────────
        // ─────────────────────────────────────────────────────────────────────

        // ── Static shorthand (no Instance null-check boilerplate at call sites) ─

        /// <summary>
        /// Play a oneshot VFX at a world position. Null-safe — does nothing if
        /// VFXManager hasn't been initialised yet.
        /// </summary>
        /// <param name="playSound">
        /// When true (default) a matching SFX is fired through AudioService at the
        /// same position. Pass false to suppress audio without affecting the visual
        /// (e.g. when the caller handles audio itself). WO-62.
        /// </param>
        public static void Play(VFXType type, Vector3 position,
                                Quaternion rotation = default, bool playSound = true)
        {
            Instance?.PlayOneshot(type, position, rotation);
            if (playSound)
                PlayAudio(type, position);
        }

        /// <summary>Play a oneshot and specify an optional parent (effect will move with it).</summary>
        public static void PlayAt(VFXType type, Transform parent)
            => Instance?.PlayOneshot(type, parent.position, parent.rotation, parent);

        // ── Audio bridge (WO-62) ──────────────────────────────────────────────

        /// <summary>
        /// Maps a VFXType to its paired SfxId and fires the sound through
        /// AudioService (if available). Called automatically by Play() unless
        /// <c>playSound:false</c> is passed. Null-safe throughout. WO-62.
        /// </summary>
        private static void PlayAudio(VFXType type, Vector3 position)
        {
            var sfxId = VfxToSfx(type);
            if (sfxId == DeNelle.Audio.SfxId.None) return;
            DeNelle.Audio.AudioService.Instance?.PlaySfxAtPosition(sfxId, position);
        }

        /// <summary>
        /// Maps every VFXType that has a sound to its paired SfxId.
        /// Types without a sound mapping return SfxId.None (silent). WO-62.
        /// </summary>
        private static DeNelle.Audio.SfxId VfxToSfx(VFXType type) => type switch
        {
            VFXType.Impact_ExplosionFire    => DeNelle.Audio.SfxId.FireExplosion,
            VFXType.Impact_Flame            => DeNelle.Audio.SfxId.FireExplosion,
            VFXType.Impact_ExplosionAether  => DeNelle.Audio.SfxId.ArcaneExplosion,
            VFXType.Impact_Aether           => DeNelle.Audio.SfxId.ArcaneExplosion,
            VFXType.Impact_ShockwaveRing    => DeNelle.Audio.SfxId.Shockwave,
            VFXType.Impact_Heal             => DeNelle.Audio.SfxId.Heal,
            VFXType.Cast_MageCharge         => DeNelle.Audio.SfxId.WizardCast,
            VFXType.Cast_KnightSlam         => DeNelle.Audio.SfxId.Shockwave,
            VFXType.Projectile_FlameArrow   => DeNelle.Audio.SfxId.FlameArrowLaunch,
            VFXType.Projectile_TowerArcane  => DeNelle.Audio.SfxId.TowerShot,
            VFXType.Projectile_TowerFire    => DeNelle.Audio.SfxId.TowerShot,
            VFXType.Death_Skeleton          => DeNelle.Audio.SfxId.EnemyDeath,
            VFXType.Death_Boss              => DeNelle.Audio.SfxId.EnemyDeath,
            VFXType.Death_Brute             => DeNelle.Audio.SfxId.EnemyDeath,
            VFXType.Death_Wolf              => DeNelle.Audio.SfxId.EnemyDeath,
            VFXType.Death_Tiefling          => DeNelle.Audio.SfxId.EnemyDeath,
            VFXType.Death_Generic           => DeNelle.Audio.SfxId.EnemyDeath,
            VFXType.WaveClear_Celebration   => DeNelle.Audio.SfxId.WaveClear,
            VFXType.Juice_WaveClear         => DeNelle.Audio.SfxId.WaveClear,
            VFXType.LevelUp_Celebration     => DeNelle.Audio.SfxId.LevelUp,
            VFXType.Juice_LevelUp           => DeNelle.Audio.SfxId.LevelUp,
            VFXType.Combo_Tier1             => DeNelle.Audio.SfxId.ComboSmall,
            VFXType.Juice_KillStreak        => DeNelle.Audio.SfxId.ComboSmall,
            VFXType.Combo_Tier2             => DeNelle.Audio.SfxId.ComboBig,
            VFXType.Pet_Aura_Fire           => DeNelle.Audio.SfxId.PetFireAura,
            VFXType.Pet_Attack              => DeNelle.Audio.SfxId.PetAttack,
            VFXType.ShootingStar            => DeNelle.Audio.SfxId.None,  // WO-52: no SFX entry yet
            // WO-59 dungeon
            VFXType.Death_EnemyExplosion_Dungeon => DeNelle.Audio.SfxId.EnemyDeath,
            // WO-66 elite / boss
            VFXType.Elite_Spawn             => DeNelle.Audio.SfxId.None,
            VFXType.Elite_Death             => DeNelle.Audio.SfxId.EnemyDeath,
            VFXType.Boss_Spawn              => DeNelle.Audio.SfxId.None,
            VFXType.Boss_Death              => DeNelle.Audio.SfxId.EnemyDeath,
            VFXType.Boss_AttackImpact       => DeNelle.Audio.SfxId.Shockwave,
            // WO-66 boss phase VFX
            VFXType.Boss_PhaseTransition    => DeNelle.Audio.SfxId.Shockwave,
            VFXType.Boss_Telegraph          => DeNelle.Audio.SfxId.None,
            _                               => DeNelle.Audio.SfxId.None,
        };

        // ── Typed entry points (match work-order method names exactly) ─────────

        /// <summary>
        /// Oneshot impact VFX at a world position and optional rotation.
        /// Use for hits, explosions, shockwaves, death bursts.
        /// </summary>
        public void PlayImpact(VFXType type, Vector3 position, Quaternion rotation = default)
            => PlayOneshot(type, position, rotation);

        /// <summary>
        /// Attach a projectile trail to a moving Transform. Returns a VFXHandle —
        /// call handle.Stop() when the projectile hits its target.
        /// </summary>
        public VFXHandle PlayProjectile(VFXType type, Transform projectileTransform)
            => PlayLoop(type, projectileTransform.position, projectileTransform);

        /// <summary>
        /// Play a casting / wind-up effect on the caster's Transform.
        /// The effect is a short oneshot that auto-destroys.
        /// </summary>
        public void PlayCasting(VFXType type, Transform casterTransform)
            => PlayOneshot(type, casterTransform.position, casterTransform.rotation, casterTransform);

        /// <summary>
        /// Oneshot death burst at a world position. Automatically picks a larger
        /// explosion for boss-tier enemies.
        /// </summary>
        public void PlayDeath(VFXType type, Vector3 position)
            => PlayOneshot(type, position, Quaternion.identity);

        /// <summary>
        /// Start a persistent aura loop attached to a Transform. Returns a VFXHandle —
        /// call handle.Stop() when the enemy dies or the effect ends.
        /// </summary>
        public VFXHandle PlayAura(VFXType type, Transform parent)
            => PlayLoop(type, parent.position, parent);

        /// <summary>
        /// Start a persistent environment loop (torch, fog, portal …) attached to a
        /// Transform. Returns a VFXHandle. Call handle.Stop() if the object is destroyed.
        /// </summary>
        public VFXHandle PlayEnvironment(VFXType type, Transform parent)
            => PlayLoop(type, parent.position, parent);

        /// <summary>
        /// Play a pet aura that auto-selects the right VFXType for the pet's level.
        /// Level clamped to 1–3. Returns a VFXHandle for later Stop().
        /// </summary>
        public VFXHandle PlayPetAura(Component pet, int petLevel)
        {
            var t = pet != null ? pet.transform : null;
            if (t == null) return null;

            var type = petLevel switch
            {
                1 => VFXType.Aura_PetLevel1,
                2 => VFXType.Aura_PetLevel2,
                _ => VFXType.Aura_PetLevel3,    // 3+
            };
            return PlayLoop(type, t.position, t);
        }

        // ── WO-59: Dungeon mode ───────────────────────────────────────────────

        /// <summary>
        /// Swap to dungeon prefab overrides when <paramref name="active"/> is true,
        /// or restore village prefabs when false. Call on dungeon scene load/unload.
        /// Null-safe — does nothing when <see cref="dungeonSettings"/> is not assigned.
        /// WO-59.
        /// </summary>
        public void ApplyDungeonMode(bool active)
        {
            _dungeonMode = active;
            if (dungeonSettings == null) return;

            foreach (var ov in dungeonSettings.overrides)
            {
                if (ov.prefab == null) continue;

                if (active)
                {
                    // Replace pool entry with dungeon variant.
                    if (_pools.ContainsKey(ov.type)) _pools.Remove(ov.type);
                    // Pre-warm a single instance of the dungeon prefab.
                    _pools[ov.type] = new Queue<GameObject>();
                    _pools[ov.type].Enqueue(CreatePooledInstance(ov.prefab, ov.type));
                }
                else
                {
                    // Remove dungeon pool — next acquire will re-warm from catalog.
                    if (_pools.ContainsKey(ov.type)) _pools.Remove(ov.type);

                    // Restore from catalog if a village prefab is wired.
                    if (_catalog != null && _catalog.TryGet(ov.type, out var entry)
                        && entry.Prefab != null)
                    {
                        _pools[ov.type] = new Queue<GameObject>();
                        _pools[ov.type].Enqueue(CreatePooledInstance(entry.Prefab, ov.type));
                    }
                }
            }
        }

        /// <summary>
        /// Play a VFX in dungeon mode. Routing is automatic — dungeon overrides
        /// are already swapped into the pool by <see cref="ApplyDungeonMode"/>.
        /// Delegates to <see cref="Play"/> for a one-liner call site. WO-59.
        /// </summary>
        public static void PlayDungeon(VFXType type, Vector3 position,
                                       Quaternion rotation = default)
            => Play(type, position, rotation);

        // ─────────────────────────────────────────────────────────────────────
        // ── INTERNAL PLAY LOGIC ───────────────────────────────────────────────
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Core oneshot play — gets from pool (or instantiates), positions, plays.</summary>
        private void PlayOneshot(VFXType type, Vector3 position, Quaternion rotation,
                                 Transform parent = null)
        {
            if (type == VFXType.None) return;

            // T+U §12: a cap hit means combat VFX go SILENTLY quiet mid-fight (the classic
            // "screen stops popping" bug). Throttle-report the drop so a capped run self-detects
            // instead of looking like nothing fired. Hot path → Throttle (~1/sec per cause).
            if (_activeOneshots >= _maxActiveOneshots)
            {
                FlowTrace.Throttle("VFXManager", "oneshot-cap", 1f,
                    $"PlayOneshot('{type}') SKIPPED — active oneshots {_activeOneshots}/{_maxActiveOneshots} " +
                    "(cap hit; combat VFX dropping). Counter-leak or too-low cap?");
                return;
            }

            // Quality gate.
            if (_catalog != null && _catalog.TryGet(type, out var entry))
            {
                if ((int)_quality < entry.MinQuality)
                {
                    FlowTrace.Throttle("VFXManager", $"oneshot-quality:{type}", 2f,
                        $"PlayOneshot('{type}') SKIPPED by quality gate (need {entry.MinQuality}, " +
                        $"have {(int)_quality}) — effect intentionally absent at this quality.");
                    return;
                }

                if (entry.Prefab != null)
                {
                    // Prefab path — use pool.
                    var go = Acquire(type, entry);
                    if (go == null)
                    {
                        FlowTrace.Warn("VFXManager",
                            $"PlayOneshot('{type}'): Acquire returned null — falling back to procedural.");
                        ProceduralFallback(type, position, rotation);
                        return;
                    }
                    go.transform.position = position;
                    go.transform.rotation = rotation;
                    if (parent != null) go.transform.SetParent(parent, true);
                    go.SetActive(true);
                    VerifyHasParticles(go, type, "oneshot");
                    PlayAllParticles(go);
                    _activeOneshots++;

                    float lifetime = entry.LifetimeOverride > 0f
                        ? entry.LifetimeOverride
                        : DetectDuration(go) + 0.3f;   // 0.3 s buffer after last particle
                    StartCoroutine(ReturnAfterSeconds(go, type, lifetime));
                    return;
                }
            }

            // Procedural fallback — no prefab wired for this type.
            ProceduralFallback(type, position, rotation);
        }

        /// <summary>Core loop play — gets from pool (or instantiates), positions, parents, plays.</summary>
        private VFXHandle PlayLoop(VFXType type, Vector3 position, Transform parent = null)
        {
            if (type == VFXType.None) return null;

            // T+U §12: a loop cap hit means auras/trails silently stop appearing. Throttle-report.
            if (_activeLoops >= _maxActiveLoops)
            {
                FlowTrace.Throttle("VFXManager", "loop-cap", 1f,
                    $"PlayLoop('{type}') SKIPPED — active loops {_activeLoops}/{_maxActiveLoops} " +
                    "(cap hit; auras/trails dropping). Handles not Stop()'d, or cap too low?");
                return null;
            }

            if (_catalog != null && _catalog.TryGet(type, out var entry))
            {
                if ((int)_quality < entry.MinQuality)
                {
                    FlowTrace.Throttle("VFXManager", $"loop-quality:{type}", 2f,
                        $"PlayLoop('{type}') SKIPPED by quality gate (need {entry.MinQuality}, " +
                        $"have {(int)_quality}) — loop intentionally absent at this quality.");
                    return null;
                }

                if (entry.Prefab != null)
                {
                    var go = Acquire(type, entry);
                    if (go == null)
                    {
                        FlowTrace.Warn("VFXManager",
                            $"PlayLoop('{type}'): Acquire returned null — falling back to procedural loop.");
                        var fb = ProceduralLoopFallback(type, position, parent);
                        return fb != null ? new VFXHandle(fb, type) : null;
                    }
                    go.transform.position = position;
                    if (parent != null) go.transform.SetParent(parent, true);
                    go.SetActive(true);
                    VerifyHasParticles(go, type, "loop");
                    PlayAllParticles(go);
                    _activeLoops++;
                    _loopObjects.Add(go);   // bug-triage P1: tag as loop for correct return-decrement
                    return new VFXHandle(go, type);
                }
            }

            // Procedural fallback for loop — create a placeholder and return a handle to it.
            var fallback = ProceduralLoopFallback(type, position, parent);
            return fallback != null ? new VFXHandle(fallback, type) : null;
        }

        // ── Pool management ───────────────────────────────────────────────────

        private void InitialisePools()
        {
            if (_poolRoot == null)
            {
                var poolGo = new GameObject("[VFXPool]");
                poolGo.transform.SetParent(transform);
                _poolRoot = poolGo.transform;
            }

            if (_catalog == null)
            {
                // U: route through FlowTrace so it lands in the F8 break-log alongside the rest
                // of the VFX flow. Not a hard Fail — procedural fallback still renders — but a
                // Warn so a build shipping with NO catalog self-reports why every effect is procedural.
                FlowTrace.Warn("VFXManager",
                    "InitialisePools: no VFXCatalog assigned — ALL effects fall back to procedural AbilityVfxKit.");
                return;
            }

            _catalog.BuildLookup();

            foreach (var entry in _catalog.Entries)
            {
                if (entry.Prefab == null || entry.PoolSize <= 0) continue;
                if (!_pools.ContainsKey(entry.Type))
                    _pools[entry.Type] = new Queue<GameObject>();

                for (int i = 0; i < entry.PoolSize; i++)
                    _pools[entry.Type].Enqueue(CreatePooledInstance(entry.Prefab, entry.Type));
            }
        }

        private GameObject CreatePooledInstance(GameObject prefab, VFXType type)
        {
            if (prefab == null)
            {
                FlowTrace.Warn("VFXManager",
                    $"CreatePooledInstance('{type}'): null prefab — no pooled instance built (will use procedural).");
                return null;
            }
            var go = Instantiate(prefab, _poolRoot);
            go.name = $"[VFX_{type}]";

            // WO-602 §12: the authored catalog cast prefabs (Lana Studio orbs, etc.) were
            // built against LEGACY built-in particle shaders ("Particles/Additive" 10720 /
            // "Particles/Alpha Blended" 10721). URP cannot render those — it substitutes the
            // magenta error shader, drawn as opaque billboard quads = the hard purple/blue
            // "cubes" the hero spell-cast showed. WO-420 only magenta-proofed the PROCEDURAL
            // AbilityVfxKit path; these authored prefabs were never proofed. Re-shade every
            // ParticleSystemRenderer to URP/Particles/Unlit ONCE per instance (this method is
            // the single instantiation choke point for warm + on-demand + dungeon paths), blend
            // mode + texture + tint preserved. Guarded — a stripped shader degrades, never throws.
            Guard.Try("VFXManager", $"ProofUrpParticleShaders('{type}')",
                () => ProofUrpParticleShaders(go, type));

            // V §12: a wired prefab that ships NO ParticleSystem (or visible Renderer) pools and
            // "plays" but renders nothing — the silent-invisible-effect class. Verify ONCE per type
            // at warm time so a bad catalog entry self-reports instead of going quiet in combat.
            VerifyHasParticles(go, type, "pooled-warm");

            go.SetActive(false);
            return go;
        }

        // V: a VFX GameObject MUST carry at least one ParticleSystem OR a visible Renderer to be
        // seen. Traced Once per type (warm) / Throttled (play) so a content-side "invisible effect"
        // surfaces in the break-log with the exact type, not as a vague "combat went quiet".
        private static void VerifyHasParticles(GameObject go, VFXType type, string phase)
        {
            if (go == null) return;
            int particles = go.GetComponentsInChildren<ParticleSystem>(true).Length;
            int renderers = go.GetComponentsInChildren<Renderer>(true).Length;
            if (particles == 0 && renderers == 0)
                FlowTrace.Once("VFXManager", $"novisual:{type}",
                    $"VerifyHasParticles({phase}, '{type}'): prefab has NO ParticleSystem and NO Renderer — " +
                    "effect plays but renders nothing (invisible VFX). Check the catalog prefab for this type.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // ── WO-602: URP particle-shader proof (kill the magenta "cubes") ──────
        // ─────────────────────────────────────────────────────────────────────
        // The authored catalog VFX prefabs (Lana Studio Casual RPG VFX orbs, Spells
        // Pack, etc.) reference Unity's LEGACY built-in particle shaders. Under URP those
        // can't render, so Unity swaps in Hidden/InternalErrorShader → opaque magenta
        // billboards (the purple/blue cube cluster). We mirror AbilityVfxKit's proven
        // runtime fix (WO-420) but EXTEND it: instead of stamping a blank material we
        // preserve the source blend mode (additive vs alpha), main texture and tint so the
        // intended look survives. Reuses AbilityVfxKit.ResolveParticleShader() (already
        // caches "Universal Render Pipeline/Particles/Unlit" in a static + logs fallbacks).

        // URP fixed-function blend factor enum values (UnityEngine.Rendering.BlendMode).
        private const int BLEND_ONE                 = 1;   // One
        private const int BLEND_SRC_ALPHA           = 5;   // SrcAlpha
        private const int BLEND_ONE_MINUS_SRC_ALPHA = 10;  // OneMinusSrcAlpha

        /// <summary>
        /// WO-602: walk every ParticleSystemRenderer on <paramref name="go"/> (and children)
        /// and replace any LEGACY/built-in/error particle shader with URP/Particles/Unlit,
        /// preserving blend mode + main texture + tint. Idempotent by construction — called
        /// once per instance from CreatePooledInstance (the only instantiation site), so no
        /// per-Play re-processing. Degrades gracefully (FlowTrace.Warn, leave as-is) when the
        /// URP particle shader is stripped from the build. Never throws.
        /// </summary>
        private static void ProofUrpParticleShaders(GameObject go, VFXType type)
        {
            if (go == null) return;

            // #44: query ALL renderers, not just ParticleSystemRenderer. The authored VFX prefabs
            // (Lana Studio "Casual RPG VFX") carry MESH geometry on MeshRenderers using legacy
            // built-in particle shaders (Particles/Additive, Particles/Alpha Blended) -> those render
            // as Hidden/InternalErrorShader magenta blocks under URP ("blocky purple cubes"). The old
            // particle-only query skipped them. The reshade below is material-gated by
            // IsLegacyParticleShader, so widening the net only touches genuinely-legacy materials and
            // leaves every legitimate mesh/particle material alone.
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0) return;

            Shader urp = AbilityVfxKit.ResolveParticleShader();
            if (urp == null || urp.name.IndexOf("Universal Render Pipeline", System.StringComparison.Ordinal) < 0)
            {
                // ResolveParticleShader already FlowTrace.Warn'd the miss. We require the real
                // URP particle shader for a correct soft-particle result — a non-URP fallback
                // (Sprites/Default etc.) would not honour additive blend the same way, so rather
                // than risk a worse look we leave the authored materials untouched and report.
                FlowTrace.Warn("VFXManager",
                    $"ProofUrpParticleShaders('{type}'): URP Particles/Unlit unavailable (got " +
                    $"'{(urp != null ? urp.name : "null")}') — leaving authored materials as-is " +
                    "(may render magenta if legacy). Add it to GraphicsSettings AlwaysIncludedShaders.");
                return;
            }

            int reshaded = 0;
            foreach (var r in renderers)
            {
                if (r == null) continue;
                var mats = r.sharedMaterials;
                if (mats == null) continue;

                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var src = mats[i];
                    if (src == null) continue;
                    if (!IsLegacyParticleShader(src.shader))
                    {
                        // Owner F8 "spells so pixelated" (§12, read from the asset YAML):
                        // the Spells Pack mats our mirrored projectile/explosion prefabs use
                        // (Glow.mat, Spell 4.mat, Trail.mat, …) are ALREADY on the URP
                        // particle shader but only HALF-upgraded — texture stranded in the
                        // legacy _MainTex slot (_BaseMap NULL) and surface left OPAQUE
                        // (_Surface 0, _ZWrite 1, One/Zero blend) → untextured opaque
                        // billboard SQUARES. The legacy-only skip above left them broken;
                        // finish the migration in place (shared heal, idempotent).
                        if (AbilityVfxKit.HealHalfUpgradedParticleMaterial(src)) reshaded++;

                        // A ParticleSystemRenderer trail slot bulk-stomped with the opaque
                        // MagentaFix_DefaultLit URP/Lit material renders solid grey ribbons —
                        // point the trail at the (healed) slot-0 particle material instead.
                        if (i > 0 && r is ParticleSystemRenderer && mats[0] != null &&
                            src.name.StartsWith("MagentaFix", System.StringComparison.Ordinal))
                        {
                            mats[i] = mats[0];
                            changed = true;
                            reshaded++;
                        }
                        continue;
                    }

                    bool additive = SourceWantsAdditive(src);

                    // Carry over the look from the legacy material BEFORE we lose its props.
                    Texture mainTex = SafeGetMainTexture(src);
                    Color tint = SafeGetTintColor(src);

                    var nm = new Material(urp) { name = $"{src.name}_URPProofed" };
                    ConfigureUrpParticleBlend(nm, additive);
                    if (mainTex != null)
                    {
                        if (nm.HasProperty("_BaseMap")) nm.SetTexture("_BaseMap", mainTex);
                        nm.mainTexture = mainTex;   // _MainTex alias for completeness
                    }
                    if (nm.HasProperty("_BaseColor")) nm.SetColor("_BaseColor", tint);
                    if (nm.HasProperty("_Color"))     nm.SetColor("_Color", tint);
                    nm.color = tint;

                    mats[i] = nm;
                    changed = true;
                    reshaded++;
                }

                if (changed) r.sharedMaterials = mats;
            }

            if (reshaded > 0)
                FlowTrace.Step("VFXManager",
                    $"ProofUrpParticleShaders('{type}'): re-shaded {reshaded} legacy particle " +
                    $"material(s) to URP/Particles/Unlit across {renderers.Length} renderer(s).");
        }

        /// <summary>
        /// True when <paramref name="sh"/> is a legacy/built-in/error particle shader that URP
        /// can't render — null, Hidden/InternalErrorShader, "Legacy Shaders/*", or any
        /// "*Particles/*" that is NOT the URP one.
        /// </summary>
        private static bool IsLegacyParticleShader(Shader sh)
        {
            if (sh == null) return true;
            string n = sh.name ?? string.Empty;
            if (n.IndexOf("Universal Render Pipeline", System.StringComparison.Ordinal) >= 0) return false;
            if (n == "Hidden/InternalErrorShader") return true;
            if (n.StartsWith("Legacy Shaders/", System.StringComparison.Ordinal)) return true;
            if (n.IndexOf("Particles/", System.StringComparison.Ordinal) >= 0) return true;
            return false;
        }

        /// <summary>
        /// Decide additive vs alpha-blend from the legacy source material. Reads the shader name
        /// (Particles/Additive 10720 → additive; Particles/Alpha Blended 10721 → alpha) and, when
        /// the shader is the error shader (original lost), falls back to the material's _DstBlend
        /// if present, else defaults to ADDITIVE (cast orbs/glows are overwhelmingly additive).
        /// </summary>
        private static bool SourceWantsAdditive(Material src)
        {
            if (src == null) return true;
            string n = src.shader != null ? (src.shader.name ?? string.Empty) : string.Empty;

            if (n.IndexOf("Additive", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (n.IndexOf("Alpha Blended", System.StringComparison.OrdinalIgnoreCase) >= 0) return false;
            if (n.IndexOf("AlphaBlend", System.StringComparison.OrdinalIgnoreCase) >= 0) return false;

            // Shader name uninformative (error shader / unknown) — peek at the captured dst blend.
            if (src.HasProperty("_DstBlend"))
            {
                int dst = (int)src.GetFloat("_DstBlend");
                if (dst == BLEND_ONE) return true;                   // additive
                if (dst == BLEND_ONE_MINUS_SRC_ALPHA) return false;  // alpha
            }
            return true;   // default additive
        }

        /// <summary>Configure a URP Particles/Unlit material for transparent additive or alpha blend.</summary>
        private static void ConfigureUrpParticleBlend(Material m, bool additive)
        {
            if (m == null) return;

            // _Surface 1 = Transparent (URP Particles/Unlit). _Blend: 0 = Alpha, 2 = Additive.
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_Blend"))   m.SetFloat("_Blend", additive ? 2f : 0f);

            int dst = additive ? BLEND_ONE : BLEND_ONE_MINUS_SRC_ALPHA;
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", BLEND_SRC_ALPHA);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", dst);
            if (m.HasProperty("_ZWrite"))   m.SetFloat("_ZWrite", 0f);

            // URP transparent keyword + don't write depth + render in the transparent queue.
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.DisableKeyword("_ALPHAMODULATE_ON");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;   // 3000
        }

        private static Texture SafeGetMainTexture(Material src)
        {
            if (src == null) return null;
            if (src.HasProperty("_MainTex")) return src.GetTexture("_MainTex");
            if (src.HasProperty("_BaseMap")) return src.GetTexture("_BaseMap");
            return src.mainTexture;
        }

        private static Color SafeGetTintColor(Material src)
        {
            if (src == null) return Color.white;
            // Legacy Particles/Additive + Alpha Blended use _TintColor; others _Color.
            if (src.HasProperty("_TintColor")) return src.GetColor("_TintColor");
            if (src.HasProperty("_Color"))     return src.GetColor("_Color");
            if (src.HasProperty("_BaseColor")) return src.GetColor("_BaseColor");
            return Color.white;
        }

        /// <summary>Get a dormant instance from the pool or create a new one.</summary>
        private GameObject Acquire(VFXType type, in VFXCatalog.Entry entry)
        {
            if (_pools.TryGetValue(type, out var q) && q.Count > 0)
            {
                var reused = q.Dequeue();
                reused.transform.SetParent(null, false);   // un-parent from pool root
                return reused;
            }
            // Pool empty — instantiate a fresh one (will be pooled after use).
            return CreatePooledInstance(entry.Prefab, type);
        }

        /// <summary>
        /// Return a GameObject to its type pool (called internally after lifetime ends or
        /// via VFXHandle.Stop(immediate:true)).
        /// </summary>
        public void ReturnToPool(GameObject go, VFXType type)
        {
            if (go == null) return;

            StopAllParticles(go);
            go.transform.SetParent(_poolRoot, false);
            go.SetActive(false);

            if (!_pools.ContainsKey(type))
                _pools[type] = new Queue<GameObject>();
            _pools[type].Enqueue(go);

            // bug-triage P1: decrement the counter for the bucket this object actually came
            // from (tracked in _loopObjects at acquire), instead of guessing oneshot-first
            // and drifting until a class hit its cap and went silent.
            bool wasLoop = _loopObjects.Remove(go);
            if (wasLoop) { if (_activeLoops > 0) _activeLoops--; }
            else         { if (_activeOneshots > 0) _activeOneshots--; }
        }

        /// <summary>Defer pool return by <paramref name="delay"/> seconds (for graceful stop).</summary>
        public void ReturnAfterDelay(GameObject go, VFXType type, float delay)
        {
            if (go == null) return;
            StartCoroutine(ReturnAfterSeconds(go, type, delay));
        }

        private IEnumerator ReturnAfterSeconds(GameObject go, VFXType type, float delay)
        {
            yield return new WaitForSeconds(delay);
            ReturnToPool(go, type);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static void PlayAllParticles(GameObject go)
        {
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.Clear();
                ps.Play();
            }
        }

        private static void StopAllParticles(GameObject go)
        {
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        /// <summary>
        /// Auto-detect the longest total duration among all particle systems on the
        /// prefab so we know when it's safe to return to the pool.
        /// </summary>
        private static float DetectDuration(GameObject go)
        {
            float max = 0f;
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                var m = ps.main;
                float d = m.duration + m.startLifetime.constantMax;
                if (d > max) max = d;
            }
            return Mathf.Max(max, 0.5f);
        }

        // ─────────────────────────────────────────────────────────────────────
        // ── PROCEDURAL FALLBACK ───────────────────────────────────────────────
        // Map VFXType → AbilityVfxKit calls so nothing is ever a silent no-op.
        // ─────────────────────────────────────────────────────────────────────

        private static readonly Color _aetherColor = new Color(0.90f, 0.87f, 1.00f);
        private static readonly Color _flameColor  = new Color(1.00f, 0.27f, 0.00f);
        private static readonly Color _iceColor    = new Color(0.50f, 0.80f, 1.00f);
        private static readonly Color _healColor   = new Color(0.35f, 1.00f, 0.55f);
        private static readonly Color _physColor   = new Color(0.69f, 0.69f, 0.63f);
        private static readonly Color _goldColor   = new Color(0.92f, 0.78f, 0.30f);

        private void ProceduralFallback(VFXType type, Vector3 position, Quaternion rotation)
        {
            // Map VFXType to an AbilityVfxKit call. Types without a mapping get a
            // generic Aoe nova so nothing is ever silently missing.
            switch (type)
            {
                // ── Arcane cast chain (2026-07-02 spell language) ─────────────
                // WINDUP: a gathering glow at the caster's hand, sized to the >=1s
                // enemy-caster telegraph — replaces the old generic Strike tracer
                // (owner F8 flag_15: casts must read as a charge, not a hit).
                case VFXType.Cast_MageCharge:
                case VFXType.Cast_NecromancerSummon:
                case VFXType.Cast_EnemyCaster:
                    AbilityVfxKit.SpawnCastWindup(SpellSchool.Arcane, position + Vector3.up * 1.4f, 1.0f);
                    break;

                // IMPACT: flash + shard fan + grounded ring (flat side down).
                case VFXType.Impact_Aether:
                    AbilityVfxKit.SpawnSchoolImpact(SpellSchool.Arcane, position, 1.5f);
                    break;

                case VFXType.Impact_ExplosionAether:
                    AbilityVfxKit.SpawnSchoolImpact(SpellSchool.Arcane, position, 2.5f);
                    break;

                case VFXType.Aura_Necromancer:
                case VFXType.Aura_EnemyCaster:
                case VFXType.Aura_Healer:
                    AbilityVfxKit.SpawnAbilityVfx(AbilityEffect.Heal, _aetherColor, position, 1f, position);
                    break;

                // ── Flame impacts ─────────────────────────────────────────────
                // Fire-school impact language (ember core + grounded scorch ring).
                case VFXType.Impact_Flame:
                case VFXType.Juice_GroundDecal_Flame:
                    AbilityVfxKit.SpawnSchoolImpact(SpellSchool.Fire, position, 1.6f);
                    break;

                case VFXType.Impact_ExplosionFire:
                case VFXType.Death_Tiefling:
                    AbilityVfxKit.SpawnAbilityVfx(AbilityEffect.Meteor, _flameColor, position, 2f, position);
                    break;

                case VFXType.Aura_Flame:
                    AbilityVfxKit.SpawnAbilityVfx(AbilityEffect.Cleave, _flameColor, position, 1f, position);
                    break;

                // ── Ice impacts ───────────────────────────────────────────────
                case VFXType.Impact_Ice:
                case VFXType.Cast_FrostNova:
                case VFXType.Death_Wolf:
                case VFXType.Juice_GroundDecal_Ice:
                    AbilityVfxKit.SpawnAbilityVfx(AbilityEffect.Aoe, _iceColor, position, 2f, position);
                    break;

                case VFXType.Aura_Ice:
                    AbilityVfxKit.SpawnAbilityVfx(AbilityEffect.Snare, _iceColor, position, 1f, position);
                    break;

                // ── Healing ───────────────────────────────────────────────────
                // Nature/heal school: the green-gold rising motes (BuildHeal column) —
                // already the intended language; colour comes from the school body.
                case VFXType.Impact_Heal:
                case VFXType.Cast_Heal:
                    // CLI ④ dedup: Juice_LevelUp / Juice_WaveClear removed here — they
                    // are (correctly) handled in the gold "celebration" block below.
                    AbilityVfxKit.SpawnAbilityVfx(AbilityEffect.Heal, _healColor, position, 1.5f, position);
                    break;

                // ── Physical ──────────────────────────────────────────────────
                case VFXType.Impact_Physical:
                case VFXType.Death_Skeleton:
                case VFXType.Death_Generic:
                case VFXType.Impact_ShardsBurst:
                    AbilityVfxKit.SpawnAbilityVfxForClass(AbilityEffect.Strike, _physColor, position, 1f,
                        position + rotation * Vector3.forward, "knight");
                    break;

                case VFXType.Impact_ShockwaveRing:
                case VFXType.Cast_KnightSlam:
                    AbilityVfxKit.SpawnAbilityVfxForClass(AbilityEffect.Cleave, _physColor, position, 2f,
                        position, "knight");
                    break;

                case VFXType.Death_Brute:
                case VFXType.Death_Boss:
                    AbilityVfxKit.SpawnAbilityVfx(AbilityEffect.Meteor, _physColor, position, 3f, position);
                    break;

                // ── Projectile / ranger ───────────────────────────────────────
                case VFXType.Projectile_Arrow:
                case VFXType.Cast_RangerDraw:
                    AbilityVfxKit.SpawnAbilityVfxForClass(AbilityEffect.Strike, _healColor, position, 1f,
                        position + rotation * Vector3.forward * 3f, "ranger");
                    break;

                case VFXType.Projectile_FlameArrow:
                    AbilityVfxKit.SpawnAbilityVfxForClass(AbilityEffect.Strike, _flameColor, position, 1f,
                        position + rotation * Vector3.forward * 3f, "ranger");
                    break;

                // ── Juice ─────────────────────────────────────────────────────
                case VFXType.Juice_CriticalHit:
                    AbilityVfxKit.SpawnAbilityVfx(AbilityEffect.Cleave, _goldColor, position, 1f, position);
                    break;

                case VFXType.Juice_KillStreak:
                    AbilityVfxKit.SpawnAbilityVfx(AbilityEffect.Aoe, _goldColor, position, 2f, position);
                    break;

                // ── Smoke / wisps / auras ─────────────────────────────────────
                case VFXType.Impact_SmokeWisps:
                case VFXType.Aura_SmokeReaper:
                case VFXType.Aura_Dust:
                    AbilityVfxKit.SpawnAbilityVfx(AbilityEffect.Snare, _physColor, position, 0.8f, position);
                    break;

                // ── Portal ──────────────────────────────────────────────────────────
                case VFXType.Portal_Enter:
                case VFXType.Portal_Exit:
                    AbilityVfxKit.SpawnAbilityVfx(AbilityEffect.Aoe, _aetherColor, position, 2f, position);
                    break;

                // ── Default ───────────────────────────────────────────────────

                // ── WO-62 celebration / combo / pet types ────────────────────
                case VFXType.WaveClear_Celebration:
                case VFXType.Juice_WaveClear:
                    AbilityVfxKit.SpawnAbilityVfx(AbilityEffect.Aoe, _goldColor, position, 2.5f, position);
                    break;

                case VFXType.LevelUp_Celebration:
                case VFXType.Juice_LevelUp:
                    AbilityVfxKit.SpawnAbilityVfx(AbilityEffect.Heal, _goldColor, position, 1.5f, position);
                    break;

                                case VFXType.Combo_Tier1:
                    AbilityVfxKit.SpawnAbilityVfx(AbilityEffect.Cleave, _goldColor, position, 1f, position);
                    break;

                case VFXType.Combo_Tier2:
                    AbilityVfxKit.SpawnAbilityVfx(AbilityEffect.Aoe, _goldColor, position, 2f, position);
                    break;

                case VFXType.Pet_Aura_Fire:
                    AbilityVfxKit.SpawnAbilityVfx(AbilityEffect.Cleave, _flameColor, position, 1f, position);
                    break;

                case VFXType.Pet_Aura_Ice:
                    AbilityVfxKit.SpawnAbilityVfx(AbilityEffect.Snare, _iceColor, position, 1f, position);
                    break;

                case VFXType.Pet_Attack:
                    AbilityVfxKit.SpawnAbilityVfx(AbilityEffect.Strike, _flameColor, position, 1f, position);
                    break;

                // -- Weather / shooting star (WO-52) -----------------------------------------------
                case VFXType.ShootingStar:
                    // Procedural fallback: bright white flash at spawn position.
                    AbilityVfxKit.SpawnAbilityVfx(AbilityEffect.Aoe, Color.white, position, 0.5f, position);
                    break;

                // -- WO-59: dungeon enemy death -----------------------------------------------------
                case VFXType.Death_EnemyExplosion_Dungeon:
                    // Larger, darker explosion than the village default.
                    AbilityVfxKit.SpawnAbilityVfx(AbilityEffect.Meteor, _physColor, position, 3.5f, position);
                    break;

                // -- WO-66: elite / boss VFX -------------------------------------------------------
                case VFXType.Elite_Spawn:
                    AbilityVfxKit.SpawnAbilityVfx(AbilityEffect.Aoe, _aetherColor, position, 1.5f, position);
                    break;

                case VFXType.Elite_Death:
                    AbilityVfxKit.SpawnAbilityVfx(AbilityEffect.Meteor, _aetherColor, position, 2.5f, position);
                    break;

                case VFXType.Boss_Spawn:
                    AbilityVfxKit.SpawnAbilityVfx(AbilityEffect.Aoe, _aetherColor, position, 3f, position);
                    break;

                case VFXType.Boss_Death:
                    AbilityVfxKit.SpawnAbilityVfx(AbilityEffect.Meteor, _physColor, position, 4f, position);
                    break;

                case VFXType.Boss_AttackImpact:
                    AbilityVfxKit.SpawnAbilityVfx(AbilityEffect.Cleave, _physColor, position, 3f, position);
                    break;

                // -- WO-66: boss phase transition / telegraph -----------------------------------
                case VFXType.Boss_PhaseTransition:
                    // Enrage burst — a wide flame nova when the boss crosses a threshold.
                    AbilityVfxKit.SpawnAbilityVfx(AbilityEffect.Aoe, _flameColor, position, 3.5f, position);
                    break;

                case VFXType.Boss_Telegraph:
                    // Wind-up tell — a tight aether gather just before a special attack.
                    AbilityVfxKit.SpawnAbilityVfx(AbilityEffect.Strike, _aetherColor, position, 1.5f, position);
                    break;

                default:
                    // WO-560: unmapped types previously ALL spawned an identical aether nova
                    // (visual sameness). Pick a type-appropriate procedural burst by reading
                    // the enum NAME — element keyword -> colour, category keyword -> effect —
                    // so a new/uncased type still reads roughly right (committed-asset-only,
                    // no pack prefab) until it earns an explicit case above.
                    SpawnHeuristicFallback(type, position, rotation);
                    break;
            }
        }

        /// <summary>
        /// WO-560: name-driven procedural fallback for VFXTypes with no explicit case.
        /// Maps the enum name's element keyword to a colour and its category keyword to an
        /// <see cref="AbilityEffect"/> so unmapped types stop looking identical. Pure
        /// procedural (AbilityVfxKit) — never references a (gitignored) pack prefab.
        /// </summary>
        private static void SpawnHeuristicFallback(VFXType type, Vector3 position, Quaternion rotation)
        {
            string n = type.ToString();

            // Element keyword -> colour.
            Color col =
                (n.Contains("Fire") || n.Contains("Flame")) ? _flameColor :
                (n.Contains("Ice")  || n.Contains("Frost")) ? _iceColor :
                (n.Contains("Heal")) ? _healColor :
                (n.Contains("Physical") || n.Contains("Shard") || n.Contains("Shockwave")) ? _physColor :
                (n.Contains("Celebration") || n.Contains("Juice") || n.Contains("Combo") || n.Contains("LevelUp")) ? _goldColor :
                _aetherColor;

            // Category keyword -> effect + scale.
            if (n.Contains("Death") || n.Contains("Explosion") || n.Contains("Boss"))
                AbilityVfxKit.SpawnAbilityVfx(AbilityEffect.Meteor, col, position, 2.5f, position);
            else if (n.Contains("Cast") || n.Contains("Telegraph") || n.Contains("Projectile"))
                AbilityVfxKit.SpawnAbilityVfx(AbilityEffect.Strike, col, position, 1.5f, position + rotation * Vector3.forward * 2f);
            else if (n.Contains("Aura"))
                AbilityVfxKit.SpawnAbilityVfx(AbilityEffect.Heal, col, position, 1f, position);
            else if (n.Contains("Ring") || n.Contains("Shockwave") || n.Contains("Celebration") || n.Contains("Aoe"))
                AbilityVfxKit.SpawnAbilityVfx(AbilityEffect.Aoe, col, position, 2f, position);
            else
                AbilityVfxKit.SpawnAbilityVfx(AbilityEffect.Strike, col, position, 1.5f, position);
        }

        /// <summary>
        /// Procedural fallback for loop effects -- spawns a simple looping procedural
        /// particle child and returns the host GameObject for the VFXHandle to own.
        /// </summary>
        private GameObject ProceduralLoopFallback(VFXType type, Vector3 position, Transform parent)
        {
            // Build a minimal procedural loop (heal-style upward particles) as placeholder.
            Color col = type switch
            {
                VFXType.Aura_Flame          => _flameColor,
                VFXType.Aura_Ice            => _iceColor,
                VFXType.Aura_Healer         => _healColor,
                VFXType.Env_TorchFlame      => _flameColor,
                // WO-66 boss phase auras — calm -> enraged -> seething.
                VFXType.Boss_Aura_Phase1    => _aetherColor,
                VFXType.Boss_Aura_Phase2    => _goldColor,
                VFXType.Boss_Aura_Phase3    => _flameColor,
                _                           => _aetherColor,
            };

            var host = new GameObject($"[ProceduralLoop_{type}]");
            host.transform.position = position;
            if (parent != null) host.transform.SetParent(parent, true);

            var ps = host.AddComponent<ParticleSystem>();
            var m  = ps.main;
            m.loop           = true;
            m.duration       = 1f;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.8f, 1.2f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
            m.startSize      = new ParticleSystem.MinMaxCurve(0.1f, 0.2f);
            m.startColor     = new ParticleSystem.MinMaxGradient(col);
            m.gravityModifier = -0.1f;
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = 8f;
            var sh = ps.shape;   sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.3f;
            ps.Play();

            _activeLoops++;
            _loopObjects.Add(host);   // bug-triage P1: tag as loop for correct return-decrement
            return host;
        }
    }
}
