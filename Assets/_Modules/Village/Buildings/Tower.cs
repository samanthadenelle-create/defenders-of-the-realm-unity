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
using System.Collections;
using System.Reflection;
using UnityEngine;
using DeNelle.Core.Data;

namespace DeNelle.Village
{
    /// <summary>Runtime tower: holds its data, current level, visual, and upgrade state.</summary>
    public class Tower : MonoBehaviour, IDamageableStructure
    {
        public const int MaxLevel = 3;

        // ── WO7: Instant Swap — long-press detection ──────────────────────────

        /// <summary>
        /// Static event — fires when any tower is held for <see cref="LongPressSeconds"/>.
        /// <see cref="TowerSwapService"/> subscribes to this to open the swap menu.
        /// </summary>
        public static event Action<Tower> AnyLongPressed;

        private const float LongPressSeconds = 0.6f;
        private Coroutine _longPressRoutine;

        private void OnMouseDown()
            => _longPressRoutine = StartCoroutine(LongPressRoutine());

        private void OnMouseUp()
        {
            if (_longPressRoutine != null)
            {
                StopCoroutine(_longPressRoutine);
                _longPressRoutine = null;
            }
        }

        private IEnumerator LongPressRoutine()
        {
            yield return new WaitForSeconds(LongPressSeconds);
            _longPressRoutine = null;
            AnyLongPressed?.Invoke(this);
        }

        // ── WO7: Instant Swap — hot-swap data + visual ────────────────────────

        /// <summary>
        /// Instantly replaces this tower's type while keeping its current level,
        /// position, and empowerment state. Called by <see cref="TowerSwapService"/>
        /// after a confirmed Solana Pay transaction.
        /// </summary>
        /// <param name="newData">The TowerData for the target tower type.</param>
        public void SwapToType(TowerData newData)
        {
            if (newData == null)
            {
                Debug.LogError("[Tower] SwapToType called with null TowerData.");
                return;
            }

            _data = newData;
            ApplyVisualForLevel(_currentLevel);
            // TowerCombat reads CurrentRange/CurrentDamage live — no further wiring needed.
            Debug.Log($"[Tower] Swapped to '{newData.towerName}' at level {_currentLevel}.");
        }

        // NOT [SerializeField] — runtime-spawned towers are configured via Initialize,
        // never Inspector assignment (DEF-74 CP1 Issue 2).
        private TowerData _data;
        private int _currentLevel = 1;
        private GameObject _currentVisual;

        // ── Empowerment (Level 3 prestige — Aether Crystals) ─────────────────
        // Empowerment is NOT a 4th upgrade level — MaxLevel stays at 3. It is a
        // one-time, irreversible prestige state available only at max level.
        // The ability and crystal cost are authored on TowerData.empowerment.

        /// <summary>True once the player has paid the crystal cost and activated this tower's empowerment.</summary>
        public bool IsEmpowered { get; private set; }

        // ── IDamageableStructure — enemy contact attack target ─────────────────
        [Header("Combat — enemy targeting")]
        [Tooltip("Maximum HP. Enemies deal contact damage to towers they path to.")]
        [SerializeField, Min(10f)] private float _maxHp = 200f;

        private float _hp;

        /// <summary>IDamageableStructure — true while this tower still stands.</summary>
        public bool IsAlive => _hp > 0f;

        /// <summary>
        /// Fired when enemies destroy this tower (HP hits 0).
        /// WaveManager / TowerPersistenceService can subscribe to clean up.
        /// </summary>
        public event System.Action<Tower> TowerDestroyed;

        // DEF-75 — upgrade VFX prefabs (optional; a tiny code burst is built if null).
        [Header("Upgrade VFX (DEF-75)")]
        [SerializeField] private ParticleSystem _upgradeBurstPrefab;
        [SerializeField] private ParticleSystem _levelUpGlowPrefab;

        private ParticleSystem _activeGlow;

        // DEF-87 — per-tower world-space upgrade UI (NOT [SerializeField] — runtime AddComponent)
        private GameObject _activeUpgradeUI;

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
            _hp = _maxHp;   // IDamageableStructure: full HP on spawn
            ApplyVisualForLevel(_currentLevel);
            EnsureCombat();   // WO-82 — auto-fire once the tower is built

            // DEF-87 — spawn per-tower world-space upgrade UI
            if (data.upgradeUIPrefab != null)
            {
                if (_activeUpgradeUI != null) Destroy(_activeUpgradeUI);
                _activeUpgradeUI = Instantiate(
                    data.upgradeUIPrefab,
                    transform.position + Vector3.up * 4f,
                    Quaternion.identity,
                    transform
                );
                var btn = _activeUpgradeUI.GetComponentInChildren<UnityEngine.UI.Button>();
                if (btn != null)
                {
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() => Upgrade());
                }
            }
        }

        // Empty — Initialize() does the level-1 visual (DEF-74 CP1 Issue 2).
        private void Start() { }

        private void OnDestroy()
        {
            // DEF-87 — clean up world-space upgrade UI when the tower is destroyed
            if (_activeUpgradeUI != null) Destroy(_activeUpgradeUI);
        }

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
                // Authored tower models (BlastTower.fbx) import at a tiny native
                // scale -> the placed tower read ~10x too small. Normalize to a
                // sensible world height from renderer bounds (grows per level).
                NormalizeVisualHeight(_currentVisual, 4.5f + (level - 1) * 0.6f);
            }
            else
            {
                _currentVisual = BuildPlaceholderVisual(level);
            }

            EnsureBodyCollider();
        }

        /// <summary>
        /// Ensures the tower root carries a solid collider, so the hero cannot walk
        /// through it, OnMouseDown long-press selection fires, and the placement
        /// overlap-check can detect the tower. Sized from the current visual's
        /// renderer bounds and rebuilt each level so it tracks the tower's growth.
        /// Also assigns the Tower (or Building) layer + tag when they exist so the
        /// placement layer-mask and tag checks recognise this structure.
        /// </summary>
        private void EnsureBodyCollider()
        {
            int towerLayer = LayerMask.NameToLayer("Tower");
            if (towerLayer < 0) towerLayer = LayerMask.NameToLayer("Building");
            if (towerLayer >= 0) gameObject.layer = towerLayer;
            try { gameObject.tag = "Tower"; } catch { /* tag undefined in project — keep default */ }

            float height = 4.5f, radius = 0.9f;
            if (_currentVisual != null)
            {
                var renderers = _currentVisual.GetComponentsInChildren<Renderer>();
                if (renderers != null && renderers.Length > 0)
                {
                    Bounds b = renderers[0].bounds;
                    for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
                    height = Mathf.Max(1f, b.size.y);
                    radius = Mathf.Max(0.4f, Mathf.Max(b.size.x, b.size.z) * 0.5f);
                }
            }

            var capsule = GetComponent<CapsuleCollider>();
            if (capsule == null) capsule = gameObject.AddComponent<CapsuleCollider>();
            capsule.isTrigger = false;
            capsule.height = height;
            capsule.radius = radius;
            capsule.center = new Vector3(0f, height * 0.5f, 0f);
        }

        /// <summary>
        /// Scales a freshly-instantiated tower model to a sensible world height from
        /// its renderer bounds, regardless of FBX import scale, then re-seats its
        /// base at the tower origin. Fixes the "tower 10x too small" import-scale bug.
        /// </summary>
        private static void NormalizeVisualHeight(GameObject visual, float targetHeight)
        {
            if (visual == null) return;
            var renderers = visual.GetComponentsInChildren<Renderer>();
            if (renderers == null || renderers.Length == 0) return;

            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            if (b.size.y <= 0.001f) return;

            visual.transform.localScale *= targetHeight / b.size.y;

            // Re-seat the base at the tower origin after scaling (bounds shift).
            Bounds b2 = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b2.Encapsulate(renderers[i].bounds);
            float feet = b2.min.y - visual.transform.position.y;
            if (feet < 0f) visual.transform.localPosition -= new Vector3(0f, feet, 0f);
        }

        // ── Empowerment — public API ───────────────────────────────────────────

        /// <summary>
        /// Attempts to empower this tower. Validates max level, deducts Aether Crystals
        /// via <see cref="CrystalEconomy"/>, fires the empowerment VFX sequence, and
        /// notifies <see cref="TowerCombat"/> so it activates the new behavior.
        /// Returns false when the tower is below max level, already empowered,
        /// has no empowerment data authored, or the player can't afford the cost.
        /// </summary>
        public bool TryEmpower()
        {
            if (_data == null)
            {
                Debug.LogWarning("[Tower] TryEmpower called on uninitialised tower.");
                return false;
            }
            if (_currentLevel < MaxLevel)
            {
                Debug.Log($"[Tower] {_data.towerName} must reach Level {MaxLevel} before empowerment.");
                return false;
            }
            if (IsEmpowered)
            {
                Debug.Log($"[Tower] {_data.towerName} is already empowered.");
                return false;
            }

            var emp = _data.empowerment;
            if (emp == null || emp.ability == EmpowermentAbility.None)
            {
                Debug.LogWarning($"[Tower] {_data.towerName} has no empowerment data — assign TowerEmpowermentData in the TowerData asset.");
                return false;
            }

            // CrystalEconomy guards the balance and writes the save.
            var economy = CrystalEconomy.Instance;
            if (economy == null)
            {
                Debug.LogWarning("[Tower] CrystalEconomy service not found in scene — add CrystalEconomy to a scene GameObject.");
                return false;
            }
            if (!economy.TrySpend(emp.crystalCost)) return false;

            IsEmpowered = true;
            ApplyEmpowermentVFX();

            // Notify TowerCombat — it picks up the new behavior on the next fire tick.
            GetComponent<TowerCombat>()?.OnEmpowered(emp.ability);

            // Hide the standard upgrade UI (already hidden at MaxLevel, but be explicit).
            if (_activeUpgradeUI != null) _activeUpgradeUI.SetActive(false);

            Debug.Log($"[Tower] '{_data.towerName}' empowered — ability: {emp.ability}  cost paid: {emp.crystalCost} Crystals.");
            return true;
        }

        /// <summary>
        /// Instantiates the one-shot nova burst + the persistent aura loop authored
        /// on <see cref="TowerData.empowerment"/>. Falls back to a code-built particle
        /// ring coloured by ability element when no prefabs are assigned.
        /// </summary>
        /// <summary>
        /// DEV-ONLY: force this tower into the empowered state with <paramref name="ability"/>
        /// and play the elemental aura/burst, bypassing the level / crystal / asset-data
        /// gates. Lets a debug hotkey showcase the empowerment VFX before the TowerData
        /// assets have empowerment authored. Body is compiled out of release builds.
        /// </summary>
        public void DebugForceEmpower(EmpowermentAbility ability)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (IsEmpowered) return;
            IsEmpowered = true;

            BuildCodeBurst(transform.position + Vector3.up * 2.0f);
            BuildCodeAura(ability);
            CameraShakeBridge.Shake(0.9f, 0.5f);

            var combat = GetComponent<TowerCombat>();
            if (combat != null) combat.OnEmpowered(ability);

            Debug.Log($"[Tower] DEBUG force-empower {(_data != null ? _data.towerName : name)} -> {ability}");
#endif
        }

        private void ApplyEmpowermentVFX()
        {
            var emp = _data?.empowerment;
            if (emp == null) return;

            // One-shot nova burst — world-parented, auto-destroyed.
            if (emp.empowerNovaPrefab != null)
            {
                var nova = Instantiate(emp.empowerNovaPrefab,
                    transform.position + Vector3.up * 1.5f, Quaternion.identity);
                nova.transform.SetParent(null, true);
                Destroy(nova, 4f);
            }
            else
            {
                // Code fallback — reuse the upgrade burst builder (slightly larger).
                BuildCodeBurst(transform.position + Vector3.up * 2.0f);
            }

            // Persistent aura loop — child of the tower, lasts until tower is destroyed.
            if (emp.empowerAuraPrefab != null)
            {
                var aura = Instantiate(emp.empowerAuraPrefab,
                    transform.position + Vector3.up * 1.5f, Quaternion.identity, transform);
                aura.name = "EmpowerAura";
            }
            else
            {
                BuildCodeAura(emp.ability);
            }

            CameraShakeBridge.Shake(0.9f, 0.5f);
        }

        /// <summary>
        /// Procedural aura ring used when no authored <see cref="TowerEmpowermentData.empowerAuraPrefab"/>
        /// is assigned. Colour is chosen per empowerment ability.
        /// </summary>
        private void BuildCodeAura(EmpowermentAbility ability)
        {
            var go = new GameObject("EmpowerAura_Code");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up * 1.5f;

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            // Ability → colour map — matches elemental-codex.md colour assignments.
            UnityEngine.Color auraColor = ability switch
            {
                EmpowermentAbility.ManaSurge    => new UnityEngine.Color(0.55f, 0.25f, 1.0f),   // violet
                EmpowermentAbility.GlacialCore  => new UnityEngine.Color(0.25f, 0.75f, 1.0f),   // ice blue
                EmpowermentAbility.EternalEmber => new UnityEngine.Color(1.0f,  0.38f, 0.05f),  // ember orange
                EmpowermentAbility.TrueAim      => new UnityEngine.Color(0.20f, 0.90f, 0.40f),  // hunter green
                _                               => UnityEngine.Color.white,
            };

            var main = ps.main;
            main.loop = true;
            main.startLifetime = 1.6f;
            main.startSpeed = 0.6f;
            main.startSize = 0.13f;
            main.startColor = auraColor;
            main.maxParticles = 80;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 25f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.75f;

            // URP-compatible particle material.
            var rend = go.GetComponent<ParticleSystemRenderer>();
            if (rend != null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                                ?? Shader.Find("Particles/Standard Unlit")
                                ?? Shader.Find("Sprites/Default");
                if (shader != null) rend.sharedMaterial = new Material(shader);
            }

            ps.Play();
        }

        // ── IDamageableStructure ──────────────────────────────────────────────

        /// <summary>
        /// IDamageableStructure — called by Enemy contact attack every tick.
        /// Reduces HP; destroys the tower and fires TowerDestroyed at zero.
        /// </summary>
        public void ApplyContactDamage(float amount)
        {
            if (_hp <= 0f) return;
            _hp -= amount;
            if (_hp <= 0f)
            {
                _hp = 0f;
                Debug.Log($"[Tower] {(_data != null ? _data.towerName : name)} destroyed by enemies.");
                TowerDestroyed?.Invoke(this);
                Destroy(gameObject);
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
            var audio = FindAnyObjectByType<TowerAudioController>();
            if (audio != null) audio.PlayUpgrade();

            // DEF-87 — hide upgrade UI once max level is reached
            if (_currentLevel >= MaxLevel && _activeUpgradeUI != null)
                _activeUpgradeUI.SetActive(false);

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

        /// <summary>
        /// A distinct per-type placeholder so each tower reads differently until
        /// authored art (TowerUpgrade.visualPrefab) is assigned. Silhouette + palette
        /// are keyed off TowerData.towerName; the body grows and an accent piece
        /// recolours per level so upgrades still read. Child primitive colliders are
        /// stripped — the tower root owns the single body collider (EnsureBodyCollider).
        /// </summary>
        private GameObject BuildPlaceholderVisual(int level)
        {
            var root = new GameObject($"TowerVisual_L{level}");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = Vector3.zero;

            string n = (_data.towerName ?? string.Empty).ToLowerInvariant();
            float lift = level * 0.45f;   // body grows with level

            // Per-level accent (kept from the old visual so upgrades still read).
            Color accent = level switch
            {
                1 => new Color(0.60f, 0.60f, 0.66f),
                2 => new Color(0.45f, 0.62f, 0.85f),
                _ => new Color(0.90f, 0.75f, 0.30f),
            };

            if (n.Contains("frost") || n.Contains("ice"))
            {
                Color ice = new Color(0.55f, 0.80f, 0.95f);
                AddVisualPart(root, PrimitiveType.Cube, new Vector3(1.4f, 0.6f, 1.4f),        new Vector3(0f, 0.3f, 0f),                 Quaternion.identity,         ice * 0.7f);
                AddVisualPart(root, PrimitiveType.Cube, new Vector3(0.7f, 2.2f + lift, 0.7f), new Vector3(0f, 1.5f + lift * 0.5f, 0f),   Quaternion.Euler(0, 45, 0),  ice);
                AddVisualPart(root, PrimitiveType.Cube, new Vector3(0.35f, 0.9f, 0.35f),      new Vector3(0f, 2.9f + lift, 0f),          Quaternion.Euler(0, 45, 0),  accent);
            }
            else if (n.Contains("flame") || n.Contains("fire") || n.Contains("ember"))
            {
                Color fire = new Color(0.80f, 0.30f, 0.15f);
                AddVisualPart(root, PrimitiveType.Cylinder, new Vector3(1.2f, 0.35f, 1.2f),       new Vector3(0f, 0.35f, 0f),          Quaternion.identity, fire * 0.6f);
                AddVisualPart(root, PrimitiveType.Cylinder, new Vector3(0.9f, 1.6f + lift, 0.9f), new Vector3(0f, 1.6f + lift, 0f),    Quaternion.identity, fire);
                AddVisualPart(root, PrimitiveType.Sphere,   new Vector3(0.7f, 0.7f, 0.7f),        new Vector3(0f, 3.2f + lift * 2f, 0f), Quaternion.identity, accent);
            }
            else if (n.Contains("arcane") || n.Contains("mage") || n.Contains("mana"))
            {
                Color arc = new Color(0.55f, 0.35f, 0.85f);
                AddVisualPart(root, PrimitiveType.Cylinder, new Vector3(0.7f, 2.0f + lift, 0.7f), new Vector3(0f, 2.0f + lift, 0f),      Quaternion.identity,           arc);
                AddVisualPart(root, PrimitiveType.Cube,     new Vector3(0.6f, 0.6f, 0.6f),        new Vector3(0f, 4.2f + lift * 2f, 0f), Quaternion.Euler(45, 45, 0),   accent);
            }
            else   // archer watchtower (default)
            {
                Color wood = new Color(0.45f, 0.32f, 0.20f);
                AddVisualPart(root, PrimitiveType.Cube,     new Vector3(1.5f, 1.0f, 1.5f), new Vector3(0f, 0.5f, 0f),           Quaternion.identity, wood);
                AddVisualPart(root, PrimitiveType.Cube,     new Vector3(1.7f, 0.5f, 1.7f), new Vector3(0f, 1.5f + lift, 0f),    Quaternion.identity, wood * 1.2f);
                AddVisualPart(root, PrimitiveType.Cylinder, new Vector3(0.2f, 1.0f, 0.2f), new Vector3(0f, 2.6f + lift, 0f),    Quaternion.identity, accent);
            }

            return root;
        }

        /// <summary>Adds a tinted primitive child (collider stripped) to a tower visual root.</summary>
        private static void AddVisualPart(GameObject parent, PrimitiveType type, Vector3 scale, Vector3 localPos, Quaternion localRot, Color color)
        {
            var part = GameObject.CreatePrimitive(type);
            part.transform.SetParent(parent.transform, false);
            part.transform.localScale = scale;
            part.transform.localPosition = localPos;
            part.transform.localRotation = localRot;

            // The tower root owns the single body collider; strip the primitive
            // colliders so they neither fight it nor block the placement cursor ray.
            var col = part.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var rend = part.GetComponent<Renderer>();
            if (rend != null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                                ?? Shader.Find("Standard")
                                ?? Shader.Find("Sprites/Default");
                var mat = new Material(shader);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                else mat.color = color;
                rend.sharedMaterial = mat;
            }
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
