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
using DeNelle.Core.Combat;
using DeNelle.Core.Data;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>Runtime tower: holds its data, current level, visual, and upgrade state.</summary>
    public class Tower : MonoBehaviour, IDamageableStructure
    {
        public const int MaxLevel = 3;

        // ── WO-403: live registry (no whole-scene FindObjectsByType) ──────────
        // The HUD's town-metrics feed needs a "towers built" count. Polling it via
        // FindObjectsByType<Tower> every HUD tick (TownHudBridge) was a per-frame
        // whole-scene scan and a leading suspect in the overworld CPU leak. Each
        // tower self-registers on enable and de-registers on disable/destroy, so the
        // count is an O(1) read with zero scanning.
        private static readonly System.Collections.Generic.List<Tower> _registry
            = new System.Collections.Generic.List<Tower>();

        /// <summary>Live tower count (registry read — no scene scan).</summary>
        public static int ActiveCount => _registry.Count;

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

        // WO-672 Slice A (owner rulings F8-39 "either they exist or do not" + F8-42
        // broken = inoperable until repaired): at 0 HP the tower BREAKS instead of
        // Destroy(gameObject)ing — it stays in the world as an inoperable shell until
        // Repair() restores it. Mirrors the ResourceCollector Broken model.
        private bool _broken;

        /// <summary>IDamageableStructure — true while this tower still stands (hp &gt; 0 and not broken).</summary>
        public bool IsAlive => _hp > 0f && !_broken;

        /// <summary>True once enemies broke this tower (hp 0) — inoperable until <see cref="Repair"/>. (WO-672)</summary>
        public bool IsBroken => _broken;

        /// <summary>Health 0..1 — the wave damage-report fraction (WO-672; mirrors ResourceCollector.HpFraction).</summary>
        public float HpFraction => _maxHp > 0f ? Mathf.Clamp01(_hp / _maxHp) : 0f;

        /// <summary>
        /// Fired when enemies destroy this tower (HP hits 0).
        /// WaveManager / TowerPersistenceService can subscribe to clean up.
        /// WO-672: fires at the BREAK moment (renamed semantics: "broke") — the tower
        /// now persists as an inoperable shell, but listeners release targets exactly
        /// as before.
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

        /// <summary>
        /// The effective tier this tower applies from <see cref="TowerPerkTable"/>: the placed
        /// upgrade level (1..3), OR the capstone tier 4 once Empowered. This is the single tier the
        /// data-driven perk row is read for (no per-level if/else anywhere).
        /// </summary>
        public int EffectiveTier => IsEmpowered ? 4 : Mathf.Clamp(_currentLevel, 1, MaxLevel);

        /// <summary>
        /// Attack range for the current level. (WO-82 base from TowerData) + the WC3 tower-upgrade
        /// tech applied: TowerPerkTable rangeAdd for this tier, then the village-wide Arcane-Tower
        /// research range perk (ModifierService.TowerRangeMult). 0 when unset. (WO-432)
        /// </summary>
        public float CurrentRange
        {
            get
            {
                var u = CurrentUpgrade();
                if (u == null) return 0f;
                float ranged = TowerPerkTable.EffectiveRange(u.range, EffectiveTier);
                // WO-676: + Farsight Emplacements (towerRange, flat metres) — the BULWARK
                // talent read at this tower's existing range choke point. 0 at Σ=0.
                return ranged * DeNelle.Core.State.ModifierService.Active.TowerRangeMult
                       + TalentRangeAdd();
            }
        }

        /// <summary>
        /// Attack damage for the current level. (WO-82 base from TowerData) + the WC3 tower-upgrade
        /// tech applied: TowerPerkTable damageMult/damageAdd for this tier, then the village-wide
        /// research damage perk (ModifierService.TowerDamageMult). 0 when unset. (WO-432)
        /// </summary>
        public float CurrentDamage
        {
            get
            {
                var u = CurrentUpgrade();
                if (u == null) return 0f;
                float dmg = TowerPerkTable.EffectiveDamage(u.damage, EffectiveTier);
                // WO-676: × Keen Ballistics (towerDamage, fractional) — the BULWARK talent
                // read at this tower's existing damage choke point. ×1 at Σ=0.
                return dmg * DeNelle.Core.State.ModifierService.Active.TowerDamageMult
                       * TalentDamageMult();
            }
        }

        // ── WO-676 (BULWARK talents) ──────────────────────────────────────────
        // Placed towers are ALWAYS player-owned (spawned by TowerPlacementSystem;
        // garrison turrets are DefenseTower with Allegiance.EnemyOwned), so the
        // hero's strategic tree applies unconditionally here. Sums come from
        // HeroTalentModifiers.StatSum — the SAME Σ-registry pattern
        // HeroHealth.TakeDamage consumes — TTL-cached (0.5s) because CurrentRange
        // is polled per frame by TowerRangeRing. Σ=0 → identity (×1 / +0), so
        // baseline stats are byte-identical.
        // towerAttackSpeed (Standing Orders) is cached HERE alongside damage/range
        // (one pattern, one refresh) and consumed by TowerCombat's fire tick via
        // TalentAttackSpeedMult() — TowerCombat divides its EffectiveCooldown by it
        // (TowerCombat.cs Update), so it never invents a second cache/class-resolve.
        private float _talentDamageMult = 1f;
        private float _talentRangeAdd;
        private float _talentAttackSpeedMult = 1f;
        private float _talentSumsNextRefresh = -1f;

        private static string ActiveHeroClass()
        {
            var hero = HeroHealth.Instance;
            var abilities = hero != null ? hero.GetComponent<HeroAbilities>() : null;
            return abilities != null ? abilities.HeroClass : "knight";
        }

        private void RefreshTalentSumsIfDue()
        {
            if (Time.time < _talentSumsNextRefresh) return;
            _talentSumsNextRefresh = Time.time + 0.5f;

            string heroClass = ActiveHeroClass();
            float dmg = Talents.HeroTalentModifiers.StatSum(heroClass, "towerDamage");
            float rng = Talents.HeroTalentModifiers.StatSum(heroClass, "towerRange");
            // Standing Orders — the clamped accessor (0..+100%, A3 G2 cap) rather than a raw
            // StatSum, because fire-rate divides a cooldown (an unclamped Σ could zero it).
            float spd = Talents.HeroTalentModifiers.TowerAttackSpeedBonus(heroClass);
            _talentDamageMult      = 1f + Mathf.Max(0f, dmg);
            _talentRangeAdd        = Mathf.Max(0f, rng);
            _talentAttackSpeedMult = 1f + spd;

            if (dmg > 0f) FlowTrace.Once("Tower", "talent-towerDamage",
                $"BULWARK towerDamage applied to placed towers: +{dmg:P0} (Keen Ballistics).");
            if (rng > 0f) FlowTrace.Once("Tower", "talent-towerRange",
                $"BULWARK towerRange applied to placed towers: +{rng:0.#}m (Farsight Emplacements).");
            if (spd > 0f) FlowTrace.Once("Tower", "talent-towerAttackSpeed",
                $"BULWARK towerAttackSpeed applied to placed towers: +{spd:P0} fire rate (Standing Orders).");
        }

        private float TalentDamageMult() { RefreshTalentSumsIfDue(); return _talentDamageMult; }
        private float TalentRangeAdd()   { RefreshTalentSumsIfDue(); return _talentRangeAdd; }

        /// <summary>WO-676 BULWARK: 1 + Standing Orders fire-rate bonus (TTL-cached with the
        /// damage/range sums above). Same-assembly consumer: TowerCombat divides its effective
        /// cooldown by this. Placed towers are ALWAYS player-owned (see block comment above),
        /// so the hero's strategic tree applies unconditionally. ×1 at Σ=0.</summary>
        internal float TalentAttackSpeedMult() { RefreshTalentSumsIfDue(); return _talentAttackSpeedMult; }

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
            // WO-672 persistence note: placed towers are REBUILT from BaseLayout on load
            // (fresh spawn through this Initialize), so a tower that broke last session
            // comes back INTACT — today's behavior for survivors, kept deliberately.
            // Persisting the broken state needs a save-schema change (v29 precedent) and
            // is a deferred, separately-reviewed lane — do NOT add it here.
            _broken = false;
            ApplyVisualForLevel(_currentLevel);
            EnsureCombat();   // WO-82 — auto-fire once the tower is built

            // Canonical upgrade surface (owner 2026-06-27, tower-upgrade CONSOLIDATION):
            // every placed tower gets the SAME proximity context-button affordance the
            // buildings use — the hero approaches an upgradable tower, the HUD's bottom
            // context (diamond) button swaps Quest -> Upgrade, and the tap runs the
            // cost-enforced Tower.TryUpgrade. No hand-authored prefab required; the
            // affordance is a code component added to every tower so deprecating the
            // menus never leaves a tower un-upgradable.
            EnsureUpgradeInteractable();

            // DEPRECATED (owner 2026-06-27): the DEF-87 per-tower world-space upgrade UI
            // (data.upgradeUIPrefab) wired its button straight to the FREE Upgrade() —
            // one of the three duplicate, cost-bypassing upgrade paths. It is no longer
            // spawned; the canonical surface is the proximity context button above,
            // routed through the cost-gated Tower.TryUpgrade. If a prefab is ever
            // authored again it must call TryUpgrade(), never Upgrade(), to stay enforced.
        }

        /// <summary>
        /// Adds the shared-with-buildings proximity upgrade affordance to this tower
        /// (idempotent). Mirrors BuildingInteractable: while the hero is near and the
        /// tower is upgradable it claims the HUD context button via HudBuildingFocus.
        /// </summary>
        private void EnsureUpgradeInteractable()
        {
            if (GetComponent<TowerInteractable>() == null)
                gameObject.AddComponent<TowerInteractable>();
        }

        // Empty — Initialize() does the level-1 visual (DEF-74 CP1 Issue 2).
        private void Start() { }

        // WO-403: maintain the live registry so the HUD can read the tower count
        // without a per-frame FindObjectsByType scan.
        private void OnEnable()
        {
            if (!_registry.Contains(this)) _registry.Add(this);
        }

        private void OnDisable()
        {
            _registry.Remove(this);
        }

        private void OnDestroy()
        {
            _registry.Remove(this);
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
            using var _ = FlowTrace.Enter("Tower", $"ApplyVisualForLevel L{level} ('{(_data != null ? _data.towerName : name)}')");

            if (_data == null)
            {
                FlowTrace.Fail("Tower", "ApplyVisualForLevel called before Initialize — no visual spawned.");
                return;
            }
            if (_data.upgrades == null || level < 1 || level > _data.upgrades.Length)
            {
                FlowTrace.Fail("Tower", $"ApplyVisualForLevel: invalid level {level} for '{_data.towerName}' — no visual spawned.");
                return;
            }

            // Drop the previous level's visual.
            if (_currentVisual != null) Destroy(_currentVisual);

            TowerUpgrade upgrade = _data.upgrades[level - 1];
            if (upgrade != null && upgrade.visualPrefab != null)
            {
                // Guard the authored-model spawn: a thrown Instantiate/retarget/normalize
                // step must NOT leave the tower visual-less. On any failure we roll back to
                // the procedural placeholder (the never-invisible-tower fallback).
                GameObject spawned = null;
                bool ok = Guard.Try("Tower", $"instantiate authored tower visual L{level}", () =>
                {
                    spawned = Instantiate(upgrade.visualPrefab, transform);
                    spawned.transform.localPosition = Vector3.zero;
                    spawned.transform.localRotation = Quaternion.identity;
                    // DEF-134: BlastTower.fbx ships with EMBEDDED (non-URP) materials
                    // (materialLocation=1) which render as untextured gray/magenta cubes
                    // under URP in the player build — the "floating untextured tower"
                    // owner reported. Retarget every renderer to a real URP/Lit material
                    // (mirrors PatriciaLightController.RetargetMaterialsToUrp / the Tripo
                    // fixer) so the placed tower always reads as solid stone, not raw cubes.
                    RetargetMaterialsToUrp(spawned);
                    // Authored tower models (BlastTower.fbx) import at a tiny native
                    // scale -> the placed tower read ~10x too small. Normalize to a
                    // sensible world height from renderer bounds (grows per level).
                    NormalizeVisualHeight(spawned, 4.5f + (level - 1) * 0.6f);
                });

                // RENDER-VERIFY (owner directive 2026-06-19, mirror VerifyArmorRendersNow):
                // an authored model that instantiated but carries no enabled renderer with a
                // mesh is the "floating untextured / invisible tower" symptom. PROVE it renders
                // before we keep it; otherwise drop it and fall back to the procedural placeholder
                // so the tower is NEVER silently broken/invisible.
                if (!ok || spawned == null || !VerifyVisualRendersNow(spawned, level))
                {
                    FlowTrace.Fail("Tower",
                        $"ApplyVisualForLevel L{level} ('{_data.towerName}'): authored visual failed to spawn/render — " +
                        "rolling back to procedural placeholder (no invisible tower).");
                    if (spawned != null) Destroy(spawned);
                    _currentVisual = BuildPlaceholderVisual(level);
                }
                else
                {
                    _currentVisual = spawned;
                    FlowTrace.Step("Tower", $"ApplyVisualForLevel L{level}: authored visual spawned + render-verified.");
                }
            }
            else
            {
                _currentVisual = BuildPlaceholderVisual(level);
                FlowTrace.Step("Tower", $"ApplyVisualForLevel L{level}: no authored prefab — built procedural placeholder.");
            }

            EnsureBodyCollider();

            // Coverage ring so the player reads this tower's attack range + places/upgrades
            // correctly. Reads CurrentRange live (grows with level); faint so it never clutters.
            if (GetComponent<TowerRangeRing>() == null) gameObject.AddComponent<TowerRangeRing>();
        }

        /// <summary>
        /// RENDER-VERIFY (synchronous, no camera/scene dependency — mirrors
        /// HeroArmorVisual.VerifyArmorRendersNow): the spawned tower visual MUST carry
        /// >=1 ENABLED Renderer with a non-null mesh. A model that instantiated but renders
        /// nothing is the "floating untextured / invisible tower" (TGVRU) symptom. Traces the
        /// exact counts so a capture splits "no enabled renderer" vs "no mesh" with zero
        /// guessing. Returns false => caller rolls back to the procedural placeholder.
        /// </summary>
        private static bool VerifyVisualRendersNow(GameObject visual, int level)
        {
            if (visual == null)
            {
                FlowTrace.Fail("Tower", $"VerifyVisualRenders: L{level} visual is null.");
                return false;
            }

            int total = 0, enabled = 0, withMesh = 0;
            foreach (var r in visual.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                total++;
                if (r.enabled) enabled++;

                Mesh mesh = null;
                if (r is SkinnedMeshRenderer smr) mesh = smr.sharedMesh;
                else
                {
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf != null) mesh = mf.sharedMesh;
                }
                // ParticleSystem / LineRenderer etc. carry no MeshFilter but still draw —
                // count them as "renders" so a legitimately mesh-less visual isn't rejected.
                bool drawsWithoutMesh = !(r is MeshRenderer) && !(r is SkinnedMeshRenderer);
                if (mesh != null || drawsWithoutMesh) withMesh++;
            }

            bool renders = enabled > 0 && withMesh > 0;
            FlowTrace.Step("Tower",
                $"VerifyVisualRenders L{level} on '{visual.name}': renderers total={total} enabled={enabled} withMesh={withMesh} => renders={renders}");

            if (!renders)
            {
                FlowTrace.Fail("Tower",
                    $"VerifyVisualRenders FAILED L{level} on '{visual.name}': renders={renders} " +
                    $"(total={total}, enabled={enabled}, withMesh={withMesh}) — no visible mesh.");
                return false;
            }
            return true;
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
            try { gameObject.tag = "Tower"; } catch (Exception e) { FlowTrace.Warn("Tower", $"tag set to 'Tower' failed (tag undefined in project, keeping default): {e.GetType().Name}: {e.Message}"); }

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

        /// <summary>
        /// DEF-134: Re-targets every renderer on an instantiated authored tower model
        /// to a real URP/Lit material, carrying base colour + base/normal maps across.
        /// FBX models with embedded (non-URP) materials render as untextured gray or
        /// magenta cubes under URP in the player build; this guarantees a lit, tinted
        /// surface instead. No-op for materials already on a URP shader. Mirrors
        /// PatriciaLightController.RetargetMaterialsToUrp (the same fix for the spire).
        /// </summary>
        private static void RetargetMaterialsToUrp(GameObject go)
        {
            if (go == null) return;
            Shader lit = Shader.Find("Universal Render Pipeline/Lit")
                      ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                      ?? Shader.Find("Standard");
            if (lit == null)
            {
                FlowTrace.Warn("Tower", "RetargetMaterialsToUrp: no URP/Lit/Standard shader found — leaving authored materials (may render untextured).");
                return;
            }

            // Guard EACH renderer independently: one bad material slot logs + is skipped,
            // never aborts the retarget of the rest (mirrors Guard.TryEach per-item discipline).
            Guard.TryEach("Tower", "retarget renderer to URP",
                go.GetComponentsInChildren<Renderer>(true), r =>
            {
                if (r == null) return;
                var mats = r.sharedMaterials;
                if (mats == null) return;
                for (int i = 0; i < mats.Length; i++)
                {
                    var src = mats[i];
                    // A null slot is the classic "magenta" cause; replace it with a
                    // neutral stone URP material so the tower never shows the error shader.
                    if (src == null)
                    {
                        var fallback = new Material(lit) { name = "TowerStone (URP)" };
                        if (fallback.HasProperty("_BaseColor"))
                            fallback.SetColor("_BaseColor", new Color(0.52f, 0.50f, 0.55f));
                        if (fallback.HasProperty("_Color"))
                            fallback.SetColor("_Color", new Color(0.52f, 0.50f, 0.55f));
                        mats[i] = fallback;
                        continue;
                    }
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

                    var m = new Material(lit) { name = (src.name ?? "Tower") + " (URP)" };
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
            });
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

            // One-shot nova burst — world-parented, auto-destroyed. Guarded so a bad
            // prefab logs + falls back to the code burst, never throws out of empowerment.
            if (emp.empowerNovaPrefab != null)
            {
                bool ok = Guard.Try("Tower", "instantiate empower nova", () =>
                {
                    var nova = Instantiate(emp.empowerNovaPrefab,
                        transform.position + Vector3.up * 1.5f, Quaternion.identity);
                    nova.transform.SetParent(null, true);
                    Destroy(nova, 4f);
                });
                if (!ok) BuildCodeBurst(transform.position + Vector3.up * 2.0f);
            }
            else
            {
                // Code fallback — reuse the upgrade burst builder (slightly larger).
                BuildCodeBurst(transform.position + Vector3.up * 2.0f);
            }

            // Persistent aura loop — child of the tower, lasts until tower is destroyed.
            if (emp.empowerAuraPrefab != null)
            {
                bool ok = Guard.Try("Tower", "instantiate empower aura", () =>
                {
                    var aura = Instantiate(emp.empowerAuraPrefab,
                        transform.position + Vector3.up * 1.5f, Quaternion.identity, transform);
                    aura.name = "EmpowerAura";
                });
                if (!ok) BuildCodeAura(emp.ability);
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
        /// Reduces HP; at zero the tower BREAKS (WO-672): it stays in the world as an
        /// inoperable shell, TowerDestroyed fires (listeners release targets exactly as
        /// before), and TowerCombat is disabled so it stops firing until repaired.
        /// </summary>
        public void ApplyContactDamage(float amount)
        {
            if (_hp <= 0f || _broken) return;
            _hp -= amount;
            if (_hp <= 0f)
            {
                _hp = 0f;
                _broken = true;
                FlowTrace.Step("Structure",
                    $"'{(_data != null ? _data.towerName : name)}' BROKE (hp 0) — inoperable until repaired");
                TowerDestroyed?.Invoke(this);
                // WO-672 Slice C: gate the fire path — TowerCombat owns this tower's fire
                // loop (Update lives there), so disabling the component stops all firing
                // while broken. Repair() re-enables it. IsAlive is already false, so enemy
                // sweeps release/skip the shell too.
                var combat = GetComponent<TowerCombat>();
                if (combat != null) combat.enabled = false;
            }
        }

        /// <summary>
        /// WO-672 (F8-42): full restore — HP back to max, broken cleared, the fire loop
        /// (TowerCombat) re-enabled. Cost enforcement lives with the caller (the repair
        /// flow), mirroring ResourceCollector.Repair.
        /// </summary>
        public void Repair()
        {
            _broken = false;
            _hp = _maxHp;
            var combat = GetComponent<TowerCombat>();
            if (combat != null) combat.enabled = true;
            FlowTrace.Step("Structure",
                $"'{(_data != null ? _data.towerName : name)}' REPAIRED (hp {_maxHp:0})");
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

            // WO-432 — the upgrade now GRANTS the designed per-tier tech (data-driven, no longer a
            // no-op): TowerCombat reads CurrentDamage/CurrentRange (perk row + village modifier) and
            // the fire cadence live, so this level-up immediately raises dmg/range/fire-rate. Trace the
            // applied row so a capture PROVES the gain (instrument-first, §12).
            var perkRow = TowerPerkTable.Get(EffectiveTier);
            FlowTrace.Step("Tower",
                $"Upgrade -> L{_currentLevel} ('{_data.towerName}') applied perk tier {EffectiveTier} " +
                $"({perkRow.Name}): dmgMult={perkRow.DamageMult:0.00} +{perkRow.DamageAdd} | rangeAdd=+{perkRow.RangeAdd} " +
                $"| fireRateMult={perkRow.FireRateMult:0.00} => CurrentDamage={CurrentDamage:0.0} CurrentRange={CurrentRange:0.0}");

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

        /// <summary>Outcome of <see cref="TryUpgrade"/> — lets dumb UI reflect the result.</summary>
        public enum UpgradeResult
        {
            Success,        // paid + leveled
            Maxed,          // already at MaxLevel — no action
            Uninitialized,  // no TowerData — no action
            UnknownCost,    // next level's cost is not authored — refuse (never free)
            CantAfford,     // economy short — no action
            NoEconomy,      // EconomyService missing — cannot charge, no action
        }

        /// <summary>True when this tower can still be upgraded AND its next cost is known.</summary>
        public bool CanUpgrade =>
            _data != null && _currentLevel < MaxLevel && NextUpgradeCost != int.MaxValue;

        /// <summary>
        /// Cost (Wood) to upgrade INTO the next level, read from
        /// <c>TowerData.upgrades[currentLevel].upgradeCost</c>. Returns
        /// <see cref="int.MaxValue"/> (treated as "unaffordable, never free") when the
        /// tower is maxed or the next level's cost is not authored.
        /// </summary>
        public int NextUpgradeCost
        {
            get
            {
                if (_data == null || _data.upgrades == null) return int.MaxValue;
                int idx = _currentLevel;   // upgrades[nextLevel-1] == upgrades[currentLevel]
                if (idx < 0 || idx >= _data.upgrades.Length || _data.upgrades[idx] == null)
                    return int.MaxValue;
                return _data.upgrades[idx].upgradeCost;
            }
        }

        /// <summary>
        /// THE single, cost-enforced upgrade transaction (owner 2026-06-27 — tower-upgrade
        /// CONSOLIDATION). All upgrade callers route here, so cost can never be bypassed:
        /// reads the next level's cost from <see cref="TowerData"/>, gates on
        /// <see cref="EconomyService"/> (CanAfford → atomic TrySpend), then performs the
        /// existing <see cref="Upgrade"/> level-up. Blocks at <see cref="MaxLevel"/> and
        /// when the cost is unknown or the economy is missing. UI stays dumb — it calls
        /// this and reflects the returned <see cref="UpgradeResult"/>.
        /// </summary>
        public UpgradeResult TryUpgrade()
        {
            using var _ = FlowTrace.Enter("Tower",
                $"TryUpgrade '{(_data != null ? _data.towerName : name)}' L{_currentLevel}");

            if (_data == null)
            {
                FlowTrace.Fail("Tower", "TryUpgrade on uninitialised tower — refused.");
                return UpgradeResult.Uninitialized;
            }
            if (_currentLevel >= MaxLevel)
            {
                FlowTrace.Step("Tower", $"TryUpgrade: already MAXED (L{_currentLevel}/{MaxLevel}) — refused.");
                return UpgradeResult.Maxed;
            }

            int cost = NextUpgradeCost;
            if (cost == int.MaxValue)
            {
                FlowTrace.Warn("Tower", $"TryUpgrade: next-level cost not authored for '{_data.towerName}' — refused (never free).");
                return UpgradeResult.UnknownCost;
            }

            var economy = EconomyService.Instance;
            if (economy == null)
            {
                FlowTrace.Warn("Tower", "TryUpgrade: EconomyService.Instance is null — cannot charge, refused.");
                return UpgradeResult.NoEconomy;
            }

            // #60: tower upgrades cost MULTI-resource (wood + iron + crystal) so the gathering
            // economy (all three node types) feeds tower progression, not a single resource. The
            // single TowerUpgrade.upgradeCost int drives all three amounts; CanAfford/TrySpend
            // already enforce every ResourceCost field atomically (EconomyService).
            var price = new ResourceCost(wood: cost, iron: cost, crystals: cost);
            if (!economy.CanAfford(price))
            {
                FlowTrace.Step("Tower", $"TryUpgrade: CANT-AFFORD next level (cost={cost} wood+iron+crystal, have W={economy.Wood} I={economy.Iron} C={economy.Crystals}).");
                return UpgradeResult.CantAfford;
            }
            if (!economy.TrySpend(price))
            {
                // Race: balance changed between CanAfford and TrySpend. No mutation occurred.
                FlowTrace.Warn("Tower", $"TryUpgrade: TrySpend failed for cost={cost} wood+iron+crystal (balance changed) — refused.");
                return UpgradeResult.CantAfford;
            }

            bool leveled = Upgrade();   // performs the visual swap + VFX + ability dispatch
            if (!leveled)
            {
                // Should not happen (we re-checked max above), but never leave a silent spend.
                FlowTrace.Fail("Tower", $"TryUpgrade: spent {cost} wood+iron+crystal but Upgrade() no-opped — leveled={leveled}.");
                return UpgradeResult.Maxed;
            }

            FlowTrace.Step("Tower", $"TryUpgrade: SPENT {cost} wood+iron+crystal + LEVELED -> L{_currentLevel}/{MaxLevel}.");
            return UpgradeResult.Success;
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
                bool ok = Guard.Try("Tower", "instantiate upgrade burst", () =>
                {
                    var burst = Instantiate(_upgradeBurstPrefab, burstPos, Quaternion.identity);
                    burst.transform.SetParent(null, true);   // parent to WORLD, not the tower
                    burst.Play();
                    float life = burst.main.duration + burst.main.startLifetime.constantMax;
                    Destroy(burst.gameObject, life);
                });
                if (!ok) BuildCodeBurst(burstPos);   // fall back to the code burst on a bad prefab
            }
            else
            {
                BuildCodeBurst(burstPos);   // visible even with no authored prefab
            }

            // --- Persistent glow: replaced each level (no `== null` guard) --------
            if (_activeGlow != null) Destroy(_activeGlow.gameObject);
            if (_levelUpGlowPrefab != null)
            {
                Guard.Try("Tower", "instantiate level-up glow", () =>
                {
                    _activeGlow = Instantiate(
                        _levelUpGlowPrefab,
                        transform.position + Vector3.up * 1.5f,
                        Quaternion.identity,
                        transform);
                });
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
                // Feel pass 2026-07-02: player-facing shake toggle. PlayerPrefs "camerashake"
                // (1 = on, DEFAULT ON; 0 = off) — a comfort/accessibility dial every shake
                // caller inherits because this bridge is the single shake entry point.
                if (PlayerPrefs.GetInt("camerashake", 1) == 0) return;

                // Never shake while a dialogue/panel has suppressed hero input (the same gate
                // PlayerAttackController honors) — a camera kick under a conversation or an
                // open panel reads as a glitch, not feedback.
                if (HeroLocomotion.InputSuppressed) return;

                Component target = FindShakeTarget(out MethodInfo shake);
                if (target == null || shake == null) return;
                shake.Invoke(target, new object[] { intensity, duration });
            }
            catch (Exception e)
            {
                // Shake is best-effort feedback, but no silent failure (§12) — self-report.
                FlowTrace.Warn("Tower", $"CameraShakeBridge.Shake failed: {e.GetType().Name}: {e.Message} — shake skipped (cosmetic only).");
            }
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
            foreach (var mb in UnityEngine.Object.FindObjectsByType<MonoBehaviour>())
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

    // =========================================================================
    // TowerPerkTable — WO-432 (owner 2026-06-28). The THIN data-driven interpreter
    // for tower-perks.json: one perk Row per tier (1..3 = the placed Level 1/2/3
    // upgrades, 4 = the max-level Empowerment capstone) carrying the WC3-style stat
    // deltas (damageMult/damageAdd, rangeAdd, fireRateMult). It owns NO per-tier
    // if/else — Tower.CurrentDamage/CurrentRange and TowerCombat read a single row
    // for the tower's EffectiveTier and apply it. Loaded once via the SAME WebGL-safe
    // CanonicalJson loader the rest of the catalogs use; ships a built-in fallback
    // table so a missing/broken JSON never leaves towers un-upgraded (no silent
    // failure, §12). Replaces the old "upgrade = visual swap only / TODO no-op".
    // =========================================================================
    public static class TowerPerkTable
    {
        /// <summary>One tier's designed upgrade deltas (a row of tower-perks.json).</summary>
        public sealed class Row
        {
            [Newtonsoft.Json.JsonProperty("tier")]             public int Tier;
            [Newtonsoft.Json.JsonProperty("name")]             public string Name = "";
            [Newtonsoft.Json.JsonProperty("damageMult")]       public float DamageMult = 1f;
            [Newtonsoft.Json.JsonProperty("damageAdd")]        public float DamageAdd = 0f;
            [Newtonsoft.Json.JsonProperty("rangeAdd")]         public float RangeAdd = 0f;
            [Newtonsoft.Json.JsonProperty("fireRateMult")]     public float FireRateMult = 1f;
            [Newtonsoft.Json.JsonProperty("signatureAbility")] public string SignatureAbility = "";
        }

        private sealed class File
        {
            [Newtonsoft.Json.JsonProperty("version")] public int Version;
            [Newtonsoft.Json.JsonProperty("tiers")]   public System.Collections.Generic.List<Row> Tiers
                = new System.Collections.Generic.List<Row>();
        }

        public const string RelativePath = "Data/Canonical/tower-perks.json";

        // tier (1-based) -> Row. Index 0 unused; built lazily, rebuildable via Reload().
        private static Row[] _rows;

        /// <summary>The hard-coded fallback table — identical to the shipped JSON — so a
        /// missing/corrupt tower-perks.json can never silently make upgrades a no-op again.</summary>
        private static Row[] BuiltInFallback() => new[]
        {
            null,
            new Row { Tier = 1, Name = "Built",      DamageMult = 1.08f, DamageAdd = 0f,  RangeAdd = 0f, FireRateMult = 1.00f, SignatureAbility = "" },
            new Row { Tier = 2, Name = "Reinforced", DamageMult = 1.25f, DamageAdd = 3f,  RangeAdd = 2f, FireRateMult = 0.55f, SignatureAbility = "" },
            new Row { Tier = 3, Name = "Masterwork", DamageMult = 1.45f, DamageAdd = 6f,  RangeAdd = 4f, FireRateMult = 0.40f, SignatureAbility = "overcharge" },
            new Row { Tier = 4, Name = "Empowered",  DamageMult = 1.70f, DamageAdd = 10f, RangeAdd = 6f, FireRateMult = 0.30f, SignatureAbility = "overcharge" },
        };

        /// <summary>Force a fresh read of tower-perks.json (used by the editor regression + on first use).</summary>
        public static void Reload()
        {
            Row[] built = null;
            Guard.Try("TowerPerkTable", "load tower-perks.json", () =>
            {
                string json = DeNelle.Core.CanonicalJson.Read(RelativePath);
                if (string.IsNullOrWhiteSpace(json)) return;
                var file = Newtonsoft.Json.JsonConvert.DeserializeObject<File>(json);
                if (file == null || file.Tiers == null || file.Tiers.Count == 0) return;

                int maxTier = 1;
                foreach (var r in file.Tiers) if (r != null && r.Tier > maxTier) maxTier = r.Tier;
                var arr = new Row[maxTier + 1];
                foreach (var r in file.Tiers)
                    if (r != null && r.Tier >= 1 && r.Tier < arr.Length) arr[r.Tier] = r;
                built = arr;
            });

            if (built == null)
            {
                FlowTrace.Warn("TowerPerkTable",
                    $"tower-perks.json missing/empty/unparsable at '{RelativePath}' — using the built-in fallback table (upgrades still grant stats).");
                built = BuiltInFallback();
            }
            _rows = built;
        }

        private static Row[] Rows()
        {
            if (_rows == null) Reload();
            return _rows;
        }

        /// <summary>The perk Row for <paramref name="tier"/> (clamped into the authored range).
        /// Never null — falls back to a neutral identity row if a tier is unauthored.</summary>
        public static Row Get(int tier)
        {
            var rows = Rows();
            if (rows == null || rows.Length <= 1) return new Row { Tier = tier };
            int maxTier = rows.Length - 1;
            int t = Mathf.Clamp(tier, 1, maxTier);
            return rows[t] ?? new Row { Tier = t };
        }

        /// <summary>Effective damage for a base damage at a tier: base*damageMult + damageAdd. Pure.</summary>
        public static float EffectiveDamage(float baseDamage, int tier)
        {
            var r = Get(tier);
            return baseDamage * r.DamageMult + r.DamageAdd;
        }

        /// <summary>Effective range for a base range at a tier: base + rangeAdd. Pure.</summary>
        public static float EffectiveRange(float baseRange, int tier)
        {
            var r = Get(tier);
            return baseRange + r.RangeAdd;
        }

        /// <summary>Effective fire cooldown for a base cooldown at a tier: base*fireRateMult (lower = faster). Pure.</summary>
        public static float EffectiveCooldown(float baseCooldown, int tier)
        {
            var r = Get(tier);
            float mult = r.FireRateMult > 0.01f ? r.FireRateMult : 1f;   // guard a bad 0 from divide-by-fire
            return baseCooldown * mult;
        }
    }
}
