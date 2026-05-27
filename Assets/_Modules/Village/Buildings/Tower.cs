// =============================================================================
// Tower — DEF-74 (runtime tower component) + DEF-75 delta (upgrade VFX).
// -----------------------------------------------------------------------------
// namespace DeNelle.Village (DEF-74/75 CP1 Issue 1). Spawned at runtime by
// TowerPlacementSystem.PlaceTower, which calls Initialize(TowerData) immediately
// after creating the GameObject — so _data is NOT [SerializeField] / Inspector-
// assigned (DEF-74 CP1 Issue 2). Manages the current level, swaps the visual model
// per level, and gates upgrades at max level 3.
//
// DEF-74 Correction Pass 1 applied:
//   • Initialize(TowerData) sets _data and applies the level-1 visual; Start() is
//     empty (no ApplyVisualForLevel call there) (Issue 2).
//   • ApplyVisualForLevel bounds-checks `if (level < 1 || level > upgrades.Length)`
//     before indexing upgrades[level - 1] (Issue 3).
//   • Level visual = upgrades[level - 1].visualPrefab — no basePrefab (Issue 7).
//
// DEF-75 delta (ADDITIVE — see the // DEF-75 markers):
//   • Two [SerializeField] particle fields + TriggerUpgradeVFX(), called from
//     Upgrade() AFTER ApplyVisualForLevel.
//   • Burst: instantiate, parent to WORLD, destroy after the clip duration.
//   • Glow: destroy-then-recreate as a CHILD each level (no null guard) (CP1 Issue 5).
//   • Screen shake: the spec's SmartMobileCamera does NOT exist in this project, so
//     we Shake via a null-safe reflection helper that finds any component exposing
//     Shake(float,float) — e.g. ThirdPersonCameraFollow — else no-op (CP1 Issue 6).
//
// Adaptation: tower visualPrefabs are null today (no authored tower art), so
// ApplyVisualForLevel builds a per-level-tinted procedural placeholder primitive.
// =============================================================================

using System;
using System.Reflection;
using UnityEngine;
using DeNelle.Core.Data;

namespace DeNelle.Village
{
    /// <summary>Runtime tower: holds its data, current level, visual, and upgrade state.</summary>
    public class Tower : MonoBehaviour
    {
        public const int MaxLevel = 3;

        // NOT [SerializeField] — runtime-spawned towers are configured via Initialize,
        // never Inspector assignment (DEF-74 CP1 Issue 2).
        private TowerData _data;
        private int _currentLevel = 1;
        private GameObject _currentVisual;

        // DEF-75 — upgrade VFX prefabs (optional; a tiny code burst is built if null).
        [Header("Upgrade VFX (DEF-75)")]
        [SerializeField] private ParticleSystem _upgradeBurstPrefab;
        [SerializeField] private ParticleSystem _levelUpGlowPrefab;

        private ParticleSystem _activeGlow;

        /// <summary>Current upgrade level (1-based, 1..3).</summary>
        public int CurrentLevel => _currentLevel;

        /// <summary>The authoring data this tower was initialized from.</summary>
        public TowerData Data => _data;

        /// <summary>Attack range for the current level (from TowerData; 0 when unset). (WO-82)</summary>
        public float CurrentRange { get { var u = CurrentUpgrade(); return u != null ? u.range : 0f; } }
        /// <summary>Attack damage for the current level (from TowerData; 0 when unset). (WO-82)</summary>
        public float CurrentDamage { get { var u = CurrentUpgrade(); return u != null ? u.damage : 0f; } }

        private TowerUpgrade CurrentUpgrade()
        {
            if (_data == null || _data.upgrades == null) return null;
            int i = _currentLevel - 1;
            return (i >= 0 && i < _data.upgrades.Length) ? _data.upgrades[i] : null;
        }

        /// <summary>
        /// Configure a freshly-spawned tower. Called by TowerPlacementSystem right
        /// after Instantiate; applies the level-1 visual.
        /// </summary>
        public void Initialize(TowerData data)
        {
            _data = data;
            _currentLevel = 1;
            ApplyVisualForLevel(_currentLevel);
            EnsureCombat();   // WO-82 — auto-fire once the tower is built
        }

        // Empty — Initialize() does the level-1 visual (DEF-74 CP1 Issue 2).
        private void Start() { }

        /// <summary>
        /// WO-82 — attach the auto-fire TowerCombat once the tower is built/revealed,
        /// with a FirePoint at the top of the current visual (robust across models +
        /// import scale). Idempotent; TowerCombat reads CurrentLevel/Range/Damage live
        /// so upgrades scale automatically.
        /// </summary>
        private void EnsureCombat()
        {
            if (GetComponent<TowerCombat>() != null) return;

            if (transform.Find("FirePoint") == null)
            {
                float topY = 3f;
                if (_currentVisual != null)
                {
                    var r = _currentVisual.GetComponentInChildren<Renderer>();
                    if (r != null) topY = Mathf.Max(1f, r.bounds.max.y - transform.position.y + 0.3f);
                }
                var fp = new GameObject("FirePoint");
                fp.transform.SetParent(transform);
                fp.transform.localPosition = new Vector3(0f, topY, 0f);
            }

            gameObject.AddComponent<TowerCombat>();   // resolves "FirePoint" in its Awake
        }

        /// <summary>
        /// Swap the tower's visual to <paramref name="level"/>'s prefab (or a
        /// procedural placeholder when none is authored). Bounds-checked.
        /// </summary>
        private void ApplyVisualForLevel(int level)
        {
            if (_data == null)
            {
                Debug.LogError("[Tower] ApplyVisualForLevel called before Initialize.");
                return;
            }
            if (_data.upgrades == null || level < 1 || level > _data.upgrades.Length)
            {
                Debug.LogError($"[Tower] Invalid level {level} for {_data.towerName}");
                return;
            }

            // Drop the previous level's visual.
            if (_currentVisual != null) Destroy(_currentVisual);

            TowerUpgrade upgrade = _data.upgrades[level - 1];
            if (upgrade != null && upgrade.visualPrefab != null)
            {
                _currentVisual = Instantiate(upgrade.visualPrefab, transform);
                _currentVisual.transform.localPosition = Vector3.zero;
                _currentVisual.transform.localRotation = Quaternion.identity;
            }
            else
            {
                _currentVisual = BuildPlaceholderVisual(level);
            }
        }

        /// <summary>
        /// Upgrade one level. Returns false (no-op) when already at <see cref="MaxLevel"/>.
        /// Swaps the visual, then fires the upgrade VFX (DEF-75).
        /// </summary>
        public bool Upgrade()
        {
            if (_data == null) return false;
            if (_currentLevel >= MaxLevel) return false;

            _currentLevel++;
            ApplyVisualForLevel(_currentLevel);

            // DEF-75 — visual feedback fires AFTER the model swap.
            TriggerUpgradeVFX();

            if (_currentLevel - 1 >= 0 && _currentLevel - 1 < _data.upgrades.Length)
            {
                var u = _data.upgrades[_currentLevel - 1];
                if (u != null && u.ability != SpecialAbility.None)
                    ActivateSpecialAbility(u.ability);
            }
            return true;
        }

        /// <summary>
        /// DEF-76 — special-ability dispatch stub. Each ability becomes its own
        /// runtime component in a later ticket; for now this just logs the intent so
        /// the upgrade path is wired end-to-end without dangling behaviour.
        /// </summary>
        private void ActivateSpecialAbility(SpecialAbility ability)
        {
            switch (ability)
            {
                case SpecialAbility.SlowEnemies:
                    // TODO: DEF-?? wire SlowAura component
                    break;
                case SpecialAbility.HealAllies:
                    // TODO: DEF-?? wire HealAura component
                    break;
                case SpecialAbility.FireAura:
                    // TODO: DEF-?? wire FireAura component
                    break;
                case SpecialAbility.FrostNova:
                    // TODO: DEF-?? wire FrostNova ability
                    break;
                case SpecialAbility.MagicalAffinity:
                    // TODO: DEF-?? wire MagicalAffinity buff
                    break;
            }
            Debug.Log($"[Tower] {_data.towerName} L{_currentLevel} ability: {ability} (wiring is a future ticket)");
        }

        // ---------------------------------------------------------------------
        // DEF-75 — upgrade VFX (additive delta on Tower.cs)
        // ---------------------------------------------------------------------

        /// <summary>
        /// One-shot burst (parented to world, auto-destroyed) + a persistent glow
        /// rebuilt fresh each level (CP1 Issue 5: destroy-then-recreate, no null
        /// guard) + a null-safe screen shake (CP1 Issue 6).
        /// </summary>
        private void TriggerUpgradeVFX()
        {
            Vector3 burstPos = transform.position + Vector3.up * 1.5f;

            // --- One-shot burst: world-parented, destroyed after its clip ---------
            if (_upgradeBurstPrefab != null)
            {
                var burst = Instantiate(_upgradeBurstPrefab, burstPos, Quaternion.identity);
                burst.transform.SetParent(null, true);   // parent to WORLD, not the tower
                burst.Play();
                float life = burst.main.duration + burst.main.startLifetime.constantMax;
                Destroy(burst.gameObject, life);
            }
            else
            {
                BuildCodeBurst(burstPos);   // visible even with no authored prefab
            }

            // --- Persistent glow: replaced each level (no `== null` guard) --------
            if (_activeGlow != null) Destroy(_activeGlow.gameObject);
            if (_levelUpGlowPrefab != null)
            {
                _activeGlow = Instantiate(
                    _levelUpGlowPrefab,
                    transform.position + Vector3.up * 1.5f,
                    Quaternion.identity,
                    transform);
            }

            // --- Screen shake via null-safe reflection helper ---------------------
            CameraShakeBridge.Shake(0.6f, 0.4f);
        }

        // ---------------------------------------------------------------------
        // Procedural fallbacks (no authored tower art / VFX yet)
        // ---------------------------------------------------------------------

        /// <summary>A tinted primitive that grows + recolours per level.</summary>
        private GameObject BuildPlaceholderVisual(int level)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"TowerVisual_L{level}";
            go.transform.SetParent(transform, false);

            float h = 1.5f + level * 0.8f;
            go.transform.localScale = new Vector3(1f, h, 1f);
            go.transform.localPosition = new Vector3(0f, h * 0.5f, 0f);

            // Tag so the placement overlap test can see it as an obstacle.
            go.tag = "Untagged";

            var rend = go.GetComponent<Renderer>();
            if (rend != null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                                ?? Shader.Find("Standard")
                                ?? Shader.Find("Sprites/Default");
                var mat = new Material(shader);
                Color tint = level switch
                {
                    1 => new Color(0.55f, 0.55f, 0.60f),   // grey stone
                    2 => new Color(0.40f, 0.55f, 0.80f),   // reinforced blue
                    _ => new Color(0.85f, 0.70f, 0.25f),   // gilded gold
                };
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", tint);
                else mat.color = tint;
                rend.sharedMaterial = mat;
            }
            return go;
        }

        /// <summary>A tiny self-destroying code particle burst (visible with no prefab).</summary>
        private void BuildCodeBurst(Vector3 worldPos)
        {
            var go = new GameObject("TowerUpgradeBurst");
            go.transform.position = worldPos;   // world-parented (not the tower)

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 0.6f;
            main.loop = false;
            main.startLifetime = 0.6f;
            main.startSpeed = 4f;
            main.startSize = 0.18f;
            main.startColor = new Color(1f, 0.85f, 0.3f);
            main.maxParticles = 60;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 40) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.2f;

            // URP-friendly particle material.
            var rend = go.GetComponent<ParticleSystemRenderer>();
            if (rend != null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                                ?? Shader.Find("Particles/Standard Unlit")
                                ?? Shader.Find("Sprites/Default");
                if (shader != null) rend.sharedMaterial = new Material(shader);
            }

            ps.Play();
            float life = main.duration + main.startLifetime.constantMax;
            Destroy(go, life);
        }
    }

    /// <summary>
    /// Null-safe screen-shake bridge (DEF-75). The spec's <c>SmartMobileCamera</c>
    /// does not exist in this project, so we resolve — by reflection — any component
    /// on the main camera (or anywhere in the scene) that exposes a public
    /// <c>Shake(float, float)</c> method (e.g. ThirdPersonCameraFollow) and invoke
    /// it. Every call no-ops safely if no such component is present.
    /// </summary>
    internal static class CameraShakeBridge
    {
        public static void Shake(float intensity, float duration)
        {
            try
            {
                Component target = FindShakeTarget(out MethodInfo shake);
                if (target == null || shake == null) return;
                shake.Invoke(target, new object[] { intensity, duration });
            }
            catch { /* shake is best-effort feedback only */ }
        }

        private static Component FindShakeTarget(out MethodInfo shake)
        {
            shake = null;

            // Prefer a component on the main camera (the shake usually lives there).
            Camera cam = Camera.main;
            if (cam != null)
            {
                foreach (var mb in cam.GetComponents<MonoBehaviour>())
                {
                    var m = MatchShake(mb);
                    if (m != null) { shake = m; return mb; }
                }
            }

            // Fall back to any MonoBehaviour in the scene exposing Shake(float,float).
            foreach (var mb in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                         FindObjectsSortMode.None))
            {
                var m = MatchShake(mb);
                if (m != null) { shake = m; return mb; }
            }
            return null;
        }

        private static MethodInfo MatchShake(MonoBehaviour mb)
        {
            if (mb == null) return null;
            return mb.GetType().GetMethod(
                "Shake",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[] { typeof(float), typeof(float) },
                null);
        }
    }
}
