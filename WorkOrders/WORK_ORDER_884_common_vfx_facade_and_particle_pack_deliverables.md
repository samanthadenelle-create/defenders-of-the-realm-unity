> ## RECONCILED 2026-08-08 - true status is PARTIAL
> Audit `docs/reference/WO_TRUE_STATUS_2026-08-08.md`. Evidence: 4 of the 5 sec.9 CREATE files do NOT exist - VfxFacade.cs, VfxSocket.cs, VfxElement.cs and VfxEmitter.cs are all absent (VERIFIED at source 2026-08-08). Only the particle-pack half shipped (a12c6d22). `Vfx.On(` = 0 hits, `VfxBones` = 0 hits.
> The previous Status line read "READY TO IMPLEMENT" and was wrong.

# WORK ORDER 884 — Common VFX facade (one low-cost class) + 5 Particle-Pack deliverables

**Status:** PARTIAL (reconciled 2026-08-08) — particle-pack half shipped, facade half never landed
**Silo:** Village combat / VFX (parallel-safe with the boss-breath slice — see §0.1)
**PO:** Samantha (owner)
**Author:** UI seat
**For:** CLAUDE CLI (sole committer, build-verifier)
**Date:** 2026-08-05
**Unity:** 6000.4.8f1 + URP

**Related:** WO-759 (Particle Pack playbook), WO-757 (boss breath — ALREADY in tree), WO-66
(EliteVFXController precedent), WO-785 (117 catalog rows point at gitignored art — the
constraint that decides §3's prefab strategy).

---

## 0. Mission (paste into the implementing prompt)

```
Build ONE common, low-cost, simple VFX facade over the EXISTING VFXManager bus, then apply
it to 5 deliverables: boss fire breath (already built — verify only), turret muzzle+elemental
projectile, hero spell/weapon elemental charge+impact, dungeon flickering candles, dungeon
rising steam. Owner's target one-liner: Vfx.On(dragon).AddStream(Fire).OnBone("Jaw").Play().
No second VFX bus. Append-only VFXType. Duplicate pack prefabs into committed Resources via a
BossFireBreathBuilder-style editor script (NOT raw gitignored pack-path refs — WO-785).
Instrument with FlowTrace. Village->Core only. Gate: COMPILE_GATE_OK + VFX_CATALOG_OK.
```

### 0.1 Concurrency note (READ FIRST)
At authoring time the working tree holds an **uncommitted boss-breath slice** (another session):
`VFXType.Boss_FireBreath`, [DragonBoss.cs](../Assets/_Modules/Village/Enemies/DragonBoss.cs)
breath methods, [Boss_FireBreath.prefab](../Assets/Resources/VFX/Boss/Boss_FireBreath.prefab),
[BossFireBreathBuilder.cs](../Assets/Editor/BossFireBreathBuilder.cs), the catalog row, the
`DeNelle-URP.asset` depth flag, and the `Boss_Dragon` socket. **Do NOT revert or duplicate
these.** This WO's enum + catalog edits are **append-only on top of that state**; reconcile by
explicit path. Deliverable #1 (breath) is therefore a **verify-only** item here.

---

## 0.2 RATIFIED — LOCKED CONTRACT (owner 2026-08-05) — DO NOT RE-LITIGATE

This WO and **WO-760** (the architecture ADR / rationale + apply matrix) are ONE decision. Where they
differed, the owner ruled:

**Runtime contract (LOCKED):**
```
Vfx.On(root).Add{Family}(element).OnBone(name).Play()
   → VfxElementTables.Resolve(family, element) → VFXType → VFXManager ONLY
```
- The fluent form above is canonical. A flat sugar (`Vfx.Projectile(element, socket)`, WO-760 §4.1) MAY wrap it 1:1 — same impl, never a second path.
- **`VfxElementTables` is the single resolution point** — but it **DELEGATES to `SpellVfxFactory`'s existing element→VFXType map for Cast/Projectile/Impact** (reuse, don't fork — WO-760 §5.2) and only OWNS the new families (Stream/Aura/Ambient/Muzzle).
- **Bone resolve = ONE shared resolver** (`VfxSocket.Resolve`), **extracted from `ActionBundlePlayer.ResolveAttachBone`** + jaw/mouth/chin/`VFX_BreathSocket` aliases; delete ActionBundlePlayer's private copy (WO-760 §5.3). Do NOT fork a second bone search.
- The **registry** (`docs/vfx/VFX_CREATIVE_PICKS_REGISTRY.md`) is **DATA for `VfxElementTables`, not a second runtime system.** No per-ability particle code.
- `VFXManager` stays the only engine (pools/quality/URP). No second bus.

**Prefab strategy (LOCKED — this WO's §3 wins over WO-760 §5.5):** shipped P1 recipes are duplicated
pack → **committed `Resources/VFX/`** via the `BossFireBreathBuilder`/`ParticlePackVfxBuilder` CopyAsset
pattern (WO-785 survivability). Catalog rows point at the committed copy. Pack-path + procedural-fallback
is allowed ONLY for non-shipped/experimental rows — never for shipped P1.

**Loop budget (LOCKED numbers):** scene-tiered `_maxActiveLoops` — **village 24 / dungeon 48 / boss 32**
(up from 20). **Nearest-N for enemy/pet AURAS only (6–8 nearest to camera/player)** — never for one-shot
impacts; reuse the `PoiCalloutSystem` nearest-N pattern (it already nearest-N's harvest auras).
FlowTrace-throttle-log once when nearest-N culls, so silent drops stay visible.

**Pick-row hard notes (LOCKED):**
- **Ice = DustMotes ONLY with COLD motion** — slow drift, downward/outward settle, NOT firefly upward. Same prefab + wrong velocity = "dust in a barn," not frost. Put this in the Ice catalog-row note.
- **Portal flame accent stays SECONDARY** — don't re-skin portals as FlameThrower or fire-portals blur with fireballs.
- **Despawn_Dissolve / Blink = one-shot** — play once, no auto-repeat, Stop/return-to-pool. Do NOT drag `SpawnEffect`'s demo pause-loop into combat.

**Ratified creative calls (registry §8):** ship approximations (no custom snow), drop Wind, holy/heal/
lightning procedural, blink=Dissolve, append-only enums only, low-HP **world-aura primary + vignette demoted**.

**LOCKED sequencing:**
0. Platform — `Vfx` / `VfxSocket` / `VfxElementTables` + loop-cap + nearest-N (BEFORE mass aura wiring)
1. WO-884 P1 five (breath verify · turret · hero hand cast · candles · steam)
2. Death ladder (burst-heavy, high feel, low loop pressure)
3. On-hit surface map (burst)
4. Heal + HP-state auras (fixes the red-vignette accessibility bug — **promote before 2 if accessibility-first**)
5. Combat auras (ONLY after nearest-N exists)
6. Harvest / structures
7. Portals / spawn / dissolve-blink

**Canon pointer:** `docs/vfx/VFX_PREFAB_HANDBOOK.md` is the canonical **pipeline** doc (how a pack prefab
ships: measure Family → CopyAsset whole tree → Resources/VFX → append VFXType → generator IsLoop → facade).
Follow its Step 1–8 checklist. This WO = the facade contract + first-5 deliverables; the registry = creative picks.

**ENUM-APPEND = SINGLE OWNER (coordination lock) — ✅ LANDED.** The append-only enum is ordinal-serialized into the
catalog, so only ONE author appends. **Grok owns the append; the batch LANDED 2026-08-05** in `VFXType.cs` after
`Boss_FireBreath` (UI-verified append-only, clean braces): `Env_Candle`, `Env_SteamVent`, `Env_SteamBurst`,
`Cast_MuzzleFlash`, `Enemy_Spawn`, `Despawn_Dissolve`, `Aura_LowHealth`, `Aura_NearDeath`, `Aura_HealingInProgress`,
`Aura_ItemHeal`, `Harvest_Iron/Wood/Food/Crystal/Gold`, `Collector_Ready` (healer field reuses `Aura_Healer` — no new
value). **CLI references these landed names only — do NOT mint enum values.** Any further new moment → back to Grok + registry.

---

## 1. Architecture determination (the design CLI implements)

**A presentation-layer facade, not a system.** It owns no pool, calls no `Instantiate`, spawns
no ParticleSystem — every path ends in an existing `VFXManager` call, so pooling,
quality-gating, the leak-proof oneshot registry, and the legacy→URP `ProofUrpParticleShaders`
proof are inherited for free. That single-bus discipline **is** the "low cost" guarantee and
honors HP-B2B (presentation is a separate layer; Village→Core only; append-only `VFXType`;
reflection catalog-generator wiring).

**It generalizes three existing precedents:** `EnvironmentVFX` (declarative env loop),
`EliteVFXController.PulseAura` (child-Light pulse → candle flicker), and the dragon's
`ResolveBreathSocket`/`AimBreathSocket` (bone-anchored aimed stream). **Leave those three files
as-is** (rewriting shipped prefabs' components = §0 YAML-garble risk for no player-felt gain);
`VfxEmitter` supersedes them for NEW placements, migrate later as logged leverage work.

**Two surfaces, one implementation of truth:**
- `Vfx` / `VfxBuilder` — imperative fluent one-liner for code call-sites (turret, hero).
- `VfxEmitter` — declarative add-component for scene/prefab placement (candle, steam); its
  `Play()` is literally `Vfx.On(anchor).Add{Family}(element)...Play()`.

---

## 2. Files to CREATE (all `DeNelle.Village`, Core-only deps)

`Assets/_Modules/Village/Vfx/VfxElement.cs`, `VfxSocket.cs`, `VfxFacade.cs`, `VfxEmitter.cs`.

The reference implementations below are **brace-balanced and compile against the current enum**
(they map only onto EXISTING `VFXType` values, so they build even before §4's new values land).
CLI owns final correctness + the compile gate.

### 2.1 `VfxElement.cs`
```csharp
namespace DeNelle.Village
{
    /// Element a caller wants (superset of SpellVfxFactory.SpellElement / Core element enums; neither touched).
    public enum VfxElement { Fire = 0, Ice, Arcane, Physical, Nature, Shadow, Steam, Holy, Lightning }

    /// Effect KIND — decides which VFXManager entry point runs + oneshot vs loop.
    public enum VfxFamily
    {
        Impact = 0, // oneshot burst at a point (VFXManager.Play)
        Muzzle,     // oneshot flash at a fire point
        Cast,       // oneshot charge/wind-up on the caster
        Projectile, // travel loop on a moving tf (PlayProjectile); Stop() on hit
        Stream,     // sustained cone/jet loop from a socket (PlayAura); Stop() to end
        Aura,       // persistent aura loop on a bone (PlayAura)
        Ambient,    // persistent env loop — candle/steam/torch (PlayEnvironment)
    }
}
```

### 2.2 `VfxSocket.cs` — cached, NEVER-NULL bone resolver (generalizes `ResolveBreathSocket`)
```csharp
using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    public static class VfxSocket
    {
        private static readonly Dictionary<(int, string), Transform> _cache = new Dictionary<(int, string), Transform>();

        public static readonly string[] BreathHints = { "jaw", "mouth", "snout", "chin", "head" };
        public static readonly string[] MuzzleHints = { "firepoint", "muzzle", "barrel", "gun", "tip" };
        public static readonly string[] HandHints   = { "righthand", "hand_r", "hand.r", "hand", "palm", "wrist", "chest" };

        public static Transform Resolve(Transform root, Transform explicitTf, string socketName, string[] fallbackHints)
        {
            if (explicitTf != null) return explicitTf;
            if (root == null) return null;

            string key = socketName ?? string.Empty;
            var cacheKey = (root.GetInstanceID(), key);
            if (_cache.TryGetValue(cacheKey, out var cached))
            {
                if (cached != null) return cached;
                _cache.Remove(cacheKey);
            }

            Transform found = null;
            if (!string.IsNullOrEmpty(socketName)) found = FindByExactName(root, socketName);
            if (found == null && fallbackHints != null)
            {
                found = FindByHints(root, fallbackHints, out string hit);
                if (found != null)
                    FlowTrace.Once("VfxSocket", "fallback:" + root.GetInstanceID() + ":" + key,
                        "Resolve('" + root.name + "', name='" + key + "'): exact name missed — anchored to '" +
                        found.name + "' via hint '" + hit + "'. Rename the bone or pass an explicit socket.");
            }
            if (found == null)
            {
                found = root;
                FlowTrace.Once("VfxSocket", "root:" + root.GetInstanceID() + ":" + key,
                    "Resolve('" + root.name + "', name='" + key + "'): no socket/hint matched — anchoring to ROOT.");
            }
            _cache[cacheKey] = found;
            return found;
        }

        public static Transform ResolveFor(Transform root, Transform explicitTf, string socketName, VfxFamily family)
        {
            string[] hints =
                family == VfxFamily.Stream ? BreathHints :
                (family == VfxFamily.Muzzle || family == VfxFamily.Projectile) ? MuzzleHints :
                family == VfxFamily.Cast ? HandHints : null;
            return Resolve(root, explicitTf, socketName, hints);
        }

        public static void ClearCache() => _cache.Clear();

        private static Transform FindByExactName(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t != root && string.Equals(t.name, name, System.StringComparison.OrdinalIgnoreCase)) return t;
            return null;
        }

        private static Transform FindByHints(Transform root, string[] hints, out string hit)
        {
            hit = null;
            foreach (var hint in hints)
            {
                if (string.IsNullOrEmpty(hint)) continue;
                string h = hint.ToLowerInvariant();
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t != root && t.name.ToLowerInvariant().Contains(h)) { hit = hint; return t; }
            }
            return null;
        }
    }
}
```

### 2.3 `VfxFacade.cs` — `Vfx` + `VfxBuilder` (value-type fluent, no GC) + `VfxElementTables` + `VfxRunner`
```csharp
using System.Collections;
using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    public readonly struct Vfx
    {
        private readonly Transform _root;
        private Vfx(Transform root) { _root = root; }
        public static Vfx On(Transform target) => new Vfx(target);
        public static Vfx On(Component target) => new Vfx(target != null ? target.transform : null);

        public VfxBuilder AddImpact(VfxElement e)     => VfxBuilder.Create(_root, VfxFamily.Impact, e);
        public VfxBuilder AddMuzzle(VfxElement e)     => VfxBuilder.Create(_root, VfxFamily.Muzzle, e);
        public VfxBuilder AddCast(VfxElement e)       => VfxBuilder.Create(_root, VfxFamily.Cast, e);
        public VfxBuilder AddProjectile(VfxElement e) => VfxBuilder.Create(_root, VfxFamily.Projectile, e);
        public VfxBuilder AddStream(VfxElement e)     => VfxBuilder.Create(_root, VfxFamily.Stream, e);
        public VfxBuilder AddAura(VfxElement e)       => VfxBuilder.Create(_root, VfxFamily.Aura, e);
        public VfxBuilder AddAmbient(VfxElement e)    => VfxBuilder.Create(_root, VfxFamily.Ambient, e);
    }

    public struct VfxBuilder
    {
        private Transform _root; private VfxFamily _family; private VfxElement _element;
        private Transform _socket; private string _socketName; private string[] _fallbacks;
        private Vector3 _worldPos; private bool _hasWorldPos;
        private Transform _aimTarget; private Vector3 _aimPos; private bool _hasAim; private bool _aimIsTransform;
        private Transform _follow; private float _duration;

        internal static VfxBuilder Create(Transform root, VfxFamily family, VfxElement element)
            => new VfxBuilder { _root = root, _family = family, _element = element };

        public VfxBuilder OnBone(string socket, params string[] fallbacks)
        { _socketName = socket; if (fallbacks != null && fallbacks.Length > 0) _fallbacks = fallbacks; return this; }
        public VfxBuilder OnBone(Transform socket) { _socket = socket; return this; }
        public VfxBuilder At(Vector3 worldPos) { _worldPos = worldPos; _hasWorldPos = true; return this; }
        public VfxBuilder AimAt(Transform target) { _aimTarget = target; _hasAim = true; _aimIsTransform = true; return this; }
        public VfxBuilder AimAt(Vector3 worldPos) { _aimPos = worldPos; _hasAim = true; _aimIsTransform = false; return this; }
        public VfxBuilder Follow(Transform moving) { _follow = moving; return this; }
        public VfxBuilder ForSeconds(float duration) { _duration = duration; return this; }

        public VFXHandle Play()
        {
            if (_root == null && !_hasWorldPos)
            { FlowTrace.Warn("Vfx", "Play(" + _family + "/" + _element + "): no anchor + no At() — skipped."); return null; }

            VFXType type = VfxElementTables.Resolve(_family, _element);
            if (type == VFXType.None)
            { FlowTrace.Warn("Vfx", "Play(" + _family + "/" + _element + "): VFXType.None — no effect."); return null; }

            Transform socket = _socket;
            if (socket == null && !_hasWorldPos && _root != null)
                socket = VfxSocket.ResolveFor(_root, null, _socketName, _family);

            Vector3 pos = _hasWorldPos ? _worldPos
                : (socket != null ? socket.position : (_root != null ? _root.position : Vector3.zero));
            Quaternion rot = ComputeRotation(pos, socket);
            var mgr = VFXManager.Instance;

            switch (_family)
            {
                case VfxFamily.Impact:
                case VfxFamily.Muzzle:
                case VfxFamily.Cast:
                    VFXManager.Play(type, pos, rot); return null;
                case VfxFamily.Projectile:
                {
                    if (mgr == null) { VFXManager.Play(type, pos, rot); return null; }
                    Transform follow = _follow != null ? _follow : socket;
                    if (follow == null) { VFXManager.Play(type, pos, rot); return null; }
                    return mgr.PlayProjectile(type, follow);
                }
                case VfxFamily.Stream:
                case VfxFamily.Aura:
                case VfxFamily.Ambient:
                {
                    if (mgr == null) return null;
                    Transform parent = socket != null ? socket : _root;
                    if (parent == null) return null;
                    VFXHandle h = (_family == VfxFamily.Ambient) ? mgr.PlayEnvironment(type, parent) : mgr.PlayAura(type, parent);
                    if (h != null && _duration > 0f) VfxRunner.StopAfter(h, _duration);
                    return h;
                }
            }
            return null;
        }

        private Quaternion ComputeRotation(Vector3 pos, Transform socket)
        {
            if (_hasAim)
            {
                Vector3 target = _aimIsTransform ? (_aimTarget != null ? _aimTarget.position : pos) : _aimPos;
                Vector3 dir = target - pos;
                if (dir.sqrMagnitude > 0.0001f) return Quaternion.LookRotation(dir);
            }
            return socket != null ? socket.rotation : Quaternion.identity;
        }
    }

    /// The ONE home for (family × element) -> VFXType. Maps onto EXISTING values today; re-point
    /// a line when a dedicated pack-backed type lands (Env_Candle, Cast_MuzzleFlash, Env_SteamVent).
    public static class VfxElementTables
    {
        public static VFXType Resolve(VfxFamily f, VfxElement e) => f switch
        {
            VfxFamily.Impact => ImpactType(e), VfxFamily.Muzzle => MuzzleType(e), VfxFamily.Cast => CastType(e),
            VfxFamily.Projectile => ProjectileType(e), VfxFamily.Stream => StreamType(e),
            VfxFamily.Aura => AuraType(e), VfxFamily.Ambient => AmbientType(e), _ => VFXType.None,
        };
        private static VFXType ImpactType(VfxElement e) => e switch {
            VfxElement.Fire => VFXType.Impact_Flame, VfxElement.Ice => VFXType.Impact_Ice,
            VfxElement.Arcane => VFXType.Impact_Aether, VfxElement.Physical => VFXType.Impact_Physical,
            VfxElement.Nature => VFXType.Impact_Heal, VfxElement.Shadow => VFXType.Impact_ExplosionAether,
            VfxElement.Steam => VFXType.Impact_SmokeWisps, VfxElement.Holy => VFXType.Impact_Heal,
            VfxElement.Lightning => VFXType.Impact_Aether, _ => VFXType.Impact_Physical };
        // Muzzle: reuse element cast/impact until dedicated Cast_MuzzleFlash lands (§4).
        private static VFXType MuzzleType(VfxElement e) => e switch {
            VfxElement.Fire => VFXType.Impact_Flame, VfxElement.Ice => VFXType.Impact_Ice,
            VfxElement.Arcane => VFXType.Cast_MageCharge, VfxElement.Physical => VFXType.Impact_Physical,
            VfxElement.Nature => VFXType.Cast_RangerDraw, VfxElement.Shadow => VFXType.Cast_NecromancerSummon,
            _ => VFXType.Cast_MageCharge };
        private static VFXType CastType(VfxElement e) => e switch {
            VfxElement.Fire => VFXType.Cast_FireCharge, VfxElement.Ice => VFXType.Cast_FrostNova,
            VfxElement.Arcane => VFXType.Cast_MageCharge, VfxElement.Physical => VFXType.Cast_KnightSlam,
            VfxElement.Nature => VFXType.Cast_RangerDraw, VfxElement.Shadow => VFXType.Cast_NecromancerSummon,
            VfxElement.Holy => VFXType.Cast_Heal, _ => VFXType.Cast_MageCharge };
        private static VFXType ProjectileType(VfxElement e) => e switch {
            VfxElement.Fire => VFXType.Projectile_TowerFire, VfxElement.Ice => VFXType.Projectile_TowerIce,
            VfxElement.Arcane => VFXType.Projectile_TowerArcane, VfxElement.Physical => VFXType.Projectile_Arrow,
            VfxElement.Nature => VFXType.Projectile_Arrow, VfxElement.Shadow => VFXType.Projectile_EnemyCasterBolt,
            VfxElement.Holy => VFXType.Projectile_ArcaneBolt, _ => VFXType.Projectile_ArcaneBolt };
        // Stream: Fire uses the real breath recipe (already in tree); others nearest loop.
        private static VFXType StreamType(VfxElement e) => e switch {
            VfxElement.Fire => VFXType.Boss_FireBreath, VfxElement.Ice => VFXType.Aura_Ice,
            VfxElement.Arcane => VFXType.Aura_EnemyCaster, VfxElement.Steam => VFXType.Env_GroundFog,
            VfxElement.Shadow => VFXType.Aura_Necromancer, _ => VFXType.Aura_Flame };
        private static VFXType AuraType(VfxElement e) => e switch {
            VfxElement.Fire => VFXType.Aura_Flame, VfxElement.Ice => VFXType.Aura_Ice,
            VfxElement.Arcane => VFXType.Aura_EnemyCaster, VfxElement.Physical => VFXType.Aura_Dust,
            VfxElement.Nature => VFXType.Aura_Healer, VfxElement.Shadow => VFXType.Aura_Necromancer,
            VfxElement.Steam => VFXType.Aura_SmokeReaper, VfxElement.Holy => VFXType.Aura_Healer,
            _ => VFXType.Aura_EnemyCaster };
        // Ambient: candle(Fire)->torch, steam->ground fog INTERIM; re-point these two after §4.
        private static VFXType AmbientType(VfxElement e) => e switch {
            VfxElement.Fire => VFXType.Env_TorchFlame, VfxElement.Ice => VFXType.Env_GroundFog,
            VfxElement.Arcane => VFXType.Env_LanternGlow, VfxElement.Steam => VFXType.Env_GroundFog,
            VfxElement.Shadow => VFXType.Env_GroundFog, VfxElement.Holy => VFXType.Env_LanternGlow,
            VfxElement.Lightning => VFXType.Env_LanternGlow, _ => VFXType.Env_TorchFlame };
    }

    /// Hidden singleton that runs ForSeconds auto-stop (value-type builder can't host a coroutine).
    internal sealed class VfxRunner : MonoBehaviour
    {
        private static VfxRunner _instance;
        private static VfxRunner Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[VfxRunner]"); go.hideFlags = HideFlags.HideInHierarchy;
                    Object.DontDestroyOnLoad(go); _instance = go.AddComponent<VfxRunner>();
                }
                return _instance;
            }
        }
        public static void StopAfter(VFXHandle handle, float seconds)
        { if (handle == null) return; Instance.StartCoroutine(Instance.StopRoutine(handle, seconds)); }
        private IEnumerator StopRoutine(VFXHandle handle, float seconds)
        { yield return new WaitForSeconds(seconds); if (handle != null && handle.IsAlive) handle.Stop(); }
    }
}
```

### 2.4 `VfxEmitter.cs` — declarative component (generalizes `EnvironmentVFX` + `EliteVFXController` flicker)
```csharp
using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    public sealed class VfxEmitter : MonoBehaviour
    {
        [SerializeField] private VfxElement _element = VfxElement.Fire;
        [SerializeField] private VfxFamily  _family  = VfxFamily.Ambient;
        [SerializeField] private string     _socketName = "";
        [SerializeField] private string[]   _socketFallbacks;
        [SerializeField] private Vector3    _localOffset = new Vector3(0f, 0.5f, 0f);
        [SerializeField] private bool        _followTransform = true;
        [SerializeField] private bool        _autoStart = true;
        [Header("Light flicker (candle/torch — optional)")]
        [SerializeField] private bool    _flicker = false;
        [SerializeField] private float   _flickerHz = 6f;
        [SerializeField] private Vector2 _flickerRange = new Vector2(0.7f, 1.15f);

        private VFXHandle _handle; private Transform _anchor; private bool _anchorDetached;
        private Light _flickerLight; private float _flickerBaseIntensity; private float _flickerSeed;

        private void OnEnable()  { if (_autoStart) Play(); }
        private void OnDisable() => Stop();
        private void OnDestroy() => Stop();

        private void Update()
        {
            if (!_flicker || _flickerLight == null) return;
            float n = Mathf.PerlinNoise(_flickerSeed + Time.time * _flickerHz, _flickerSeed);
            _flickerLight.intensity = _flickerBaseIntensity * Mathf.Lerp(_flickerRange.x, _flickerRange.y, n);
        }

        public void Play()
        {
            if (_handle != null && _handle.IsAlive) return;
            Transform anchor = ResolveAnchor();
            _handle = BuildFor(anchor).Play();
            if (_flicker) SetupFlickerLight();
            if (_handle == null && IsLoopFamily(_family))
                FlowTrace.Warn("VfxEmitter", "Play '" + name + "': " + _family + "/" + _element + " NULL handle — effect-less.");
            else
                FlowTrace.Step("VfxEmitter", "Play '" + name + "': " + _family + "/" + _element + " (flicker=" + _flicker + ").");
        }

        public void Stop()
        {
            _handle?.Stop(); _handle = null;
            if (_anchorDetached && _anchor != null) Destroy(_anchor.gameObject);
            _anchor = null; _anchorDetached = false;
        }
        public void SetElement(VfxElement e) { Stop(); _element = e; Play(); }

        private VfxBuilder BuildFor(Transform anchor)
        {
            var v = Vfx.On(anchor);
            return _family switch {
                VfxFamily.Ambient => v.AddAmbient(_element), VfxFamily.Aura => v.AddAura(_element),
                VfxFamily.Stream => v.AddStream(_element), VfxFamily.Impact => v.AddImpact(_element),
                VfxFamily.Muzzle => v.AddMuzzle(_element), VfxFamily.Cast => v.AddCast(_element),
                VfxFamily.Projectile => v.AddProjectile(_element), _ => v.AddAmbient(_element) };
        }

        private Transform ResolveAnchor()
        {
            if (_anchor != null) return _anchor;
            Transform baseTf = transform;
            if (!string.IsNullOrEmpty(_socketName)) baseTf = VfxSocket.Resolve(transform, null, _socketName, _socketFallbacks);
            if (_localOffset == Vector3.zero && _followTransform) return baseTf;
            var go = new GameObject("[VfxEmitterAnchor:" + name + "]");
            if (_followTransform) { go.transform.SetParent(baseTf, false); go.transform.localPosition = _localOffset; _anchorDetached = false; }
            else { go.transform.position = baseTf.TransformPoint(_localOffset); _anchorDetached = true; }
            _anchor = go.transform; return _anchor;
        }

        private void SetupFlickerLight()
        {
            if (_flickerLight == null) _flickerLight = GetComponentInChildren<Light>(true);
            if (_flickerLight == null) return;
            _flickerBaseIntensity = _flickerLight.intensity;
            _flickerSeed = (GetInstanceID() & 0x3FF) * 0.123f; // deterministic per-instance phase (headless-safe)
        }

        private static bool IsLoopFamily(VfxFamily f)
            => f == VfxFamily.Ambient || f == VfxFamily.Aura || f == VfxFamily.Stream || f == VfxFamily.Projectile;

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.6f, 0f, 0.7f);
            Vector3 w = transform.TransformPoint(_localOffset);
            Gizmos.DrawWireSphere(w, 0.1f); Gizmos.DrawLine(transform.position, w);
        }
    }
}
```

---

## 3. Pack-backed prefabs — the WO-785-safe way (CopyAsset into committed Resources)

The ParticlePack (`Assets/UnityTechnologies/**`) is **gitignored**. Per WO-785, do NOT point
catalog rows at gitignored pack paths. Instead, mirror the **`BossFireBreathBuilder` pattern**:
an editor script that `AssetDatabase.CopyAsset`s the whole multi-layer pack tree into
`Assets/Resources/VFX/<Category>/<Name>.prefab` (verified descendant + ParticleSystem counts,
clear `playOnAwake`, idempotent, distinct OK/FAIL markers). `VFXManager.ProofUrpParticleShaders`
re-shades the legacy pack materials to URP at load.

**CREATE `Assets/Editor/ParticlePackVfxBuilder.cs`** (one builder, one menu item
`Defenders/VFX/Build Particle Pack VFX`, batch `DeNelle.Editor.ParticlePackVfxBuilder.Build`,
markers `PPACK_VFX_BUILD_OK` / `_FAIL`) that copies these pack recipes → committed Resources:

| Pack recipe (source) | → committed dest | Family | Loop |
|---|---|---|---|
| `.../Misc Effects/Prefabs/Candles.prefab` | `Resources/VFX/Env/Env_Candle.prefab` | Ambient | ✅ |
| `.../Smoke & Steam Effects/Prefabs/RisingSteam.prefab` | `Resources/VFX/Env/Env_SteamVent.prefab` | Ambient | ✅ |
| `.../Smoke & Steam Effects/Prefabs/PressurisedSteam.prefab` | `Resources/VFX/Env/Env_SteamBurst.prefab` | Impact | ❌ |
| `.../Weapon Effects/Prefabs/MuzzleFlash.prefab` | `Resources/VFX/Combat/Cast_MuzzleFlash.prefab` | Muzzle | ❌ |
| `.../Fire & Explosion Effects/Prefabs/FireBall.prefab` | `Resources/VFX/Combat/Projectile_TowerFire.prefab` | Projectile | ✅ |
| `.../Magic Effects/Prefabs/IceLance.prefab` | `Resources/VFX/Combat/Projectile_TowerIce.prefab` | Projectile | ✅ |

(Keep whole trees — never flatten. If a pack material still resolves gitignored on clone, the
Resources prefab still loads and `ProofUrpParticleShaders` handles the shader; a fresh clone
without the pack degrades to procedural via the generator's existing missing-prefab skip.)

---

## 4. Enum + catalog changes (APPEND-ONLY)

**`Assets/_Modules/Village/Vfx/VFXType.cs`** — append AFTER `Boss_FireBreath` (do not reorder;
serialized by ordinal into the catalog):
```
Cast_MuzzleFlash,   // turret/tower muzzle burst (pack MuzzleFlash)
Env_Candle,         // dungeon candle flicker loop (pack Candles)
Env_SteamVent,      // dungeon rising-steam loop (pack RisingSteam)
Env_SteamBurst,     // pressurised steam oneshot (pack PressurisedSteam)
```

**`Assets/Editor/VFXCatalogGenerator.cs`** — add a `Res`-relative const for the new committed
folders and these rows (point at the §3 committed duplicates, NOT the pack):
```csharp
{ "Env_Candle",           new Pick(ResEnv + "Env_Candle.prefab",            isLoop: true,  minQuality: 1, poolSize: 8) },
{ "Env_SteamVent",        new Pick(ResEnv + "Env_SteamVent.prefab",         isLoop: true,  minQuality: 1, poolSize: 4) },
{ "Env_SteamBurst",       new Pick(ResEnv + "Env_SteamBurst.prefab",        isLoop: false, minQuality: 1, poolSize: 3) },
{ "Cast_MuzzleFlash",     new Pick(ResCombat + "Cast_MuzzleFlash.prefab",   isLoop: false, poolSize: 6) },
{ "Projectile_TowerFire", new Pick(ResCombat + "Projectile_TowerFire.prefab", isLoop: true, poolSize: 6) },
{ "Projectile_TowerIce",  new Pick(ResCombat + "Projectile_TowerIce.prefab",  isLoop: true, poolSize: 6) },
```
Then, once new values + prefabs exist, re-point `VfxElementTables`: Ambient Fire→`Env_Candle`,
Ambient Steam→`Env_SteamVent`, Muzzle→`Cast_MuzzleFlash`. Run `Defenders/VFX/Generate VFX Catalog`
(marker `VFX_CATALOG_OK`).

---

## 5. The 5 integration recipes (same class expresses all five)

**5.1 Boss fire breath — VERIFY ONLY.** Already built in the tree ([DragonBoss.cs](../Assets/_Modules/Village/Enemies/DragonBoss.cs)
`FireBreath`/`TickBreath` on the `VFX_BreathSocket`). Do NOT rewrite. Confirm the catalog row +
`Boss_FireBreath.prefab` + URP depth are committed and the stream renders. Equivalent facade
form (reference only): `Vfx.On(this).AddStream(VfxElement.Fire).OnBone("VFX_BreathSocket","jaw","mouth","snout","head").AimAt(TargetPosition()).ForSeconds(_breathDuration).Play();`

**5.2 Turret muzzle + elemental projectile** — [TowerCombat.cs](../Assets/_Modules/Village/Buildings/TowerCombat.cs)
muzzle block L359-383 + `FireSingleProjectile`; impact `OnProjectileImpact` L573:
```csharp
Vfx.On(this).AddMuzzle(ToVfxElement(element)).OnBone(_firePoint).Play();               // muzzle
var trail = Vfx.On(proj).AddProjectile(ToVfxElement(element)).Follow(proj.transform).Play();  // trail; trail?.Stop() on impact
Vfx.On(this).AddImpact(ToVfxElement(element)).At(hitPosition).Play();                   // impact
```
(Add a private `ToVfxElement(TowerElement)` mapping; keep the existing Hovl `PlayKey` layer if desired — additive.)

**5.3 Hero spell / weapon-skill elemental charge + impact** — [HeroAbilities.cs](../Assets/_Modules/Village/Hero/HeroAbilities.cs)
`PlayCastVfxKey` L2002, `PlayImpactVfxKey` L2038, `FlyCosmeticProjectile` L2107. This is where the
owner's *"add fire/ice/anything to weapon skills"* lands, and the **Mage magic showcase** (WO-909):
```csharp
Vfx.On(hero).AddCast(element).OnBone("RightHand","Hand_R","Chest").Play();
Vfx.On(hero).AddImpact(element).At(hitPos).Play();
var t = Vfx.On(proj).AddProjectile(element).Follow(proj.transform).Play();  // t.Stop() on hit
```
(Derive `element` from the ability's school/`AbilityDef`. Respect the existing `HasAuthoredHovlVfx`
guard — additive, don't double up where a Hovl key already owns the beat.)

**5.4 Dungeon flickering candles** — [DungeonSceneBuilder.cs](../Assets/Editor/DungeonSceneBuilder.cs)
`DressRoom` (L664), beside each candle `LitFixture`/`candle_lit.fbx` (L694/724/747). Add a
`VfxEmitter` **by reflection** (Editor can't ref Village — mirror how it adds Village components):
`element=Fire, family=Ambient, flicker=true, localOffset≈tip`.

**5.5 Dungeon rising steam vent** — same `DressRoom`, at chosen vent points:
`VfxEmitter element=Steam, family=Ambient, flicker=false`.

Provide one reflection helper `AddVfxEmitter(Transform host, Vector3 localPos, string element, string family, bool flicker)`
in `DungeonSceneBuilder` (uses `Type.GetType("DeNelle.Village.VfxEmitter, DeNelle.Village")` +
SerializedObject field writes), then call it for candles + vents.

---

## 6. Loop-cap watch (real risk — do not skip)
Env loops share `VFXManager._maxActiveLoops = 20`. A dungeon full of candles + steam can exceed
it → silent drop (throttled `loop-cap` trace). Mitigate: raise the serialized cap for dungeon
scenes, OR have `VfxEmitter` only emit for on-screen/nearest-N fixtures. Decide at implementation;
**do not ship 30 candles against a 20-loop budget.** `log()`/FlowTrace any culling.

## 7. Anti-patterns (fail review)
Second VFX bus · flatten a multi-layer pack prefab · aim by rewriting Shape angle (angle=width;
direction=socket rotation) · parent a stream at the target · `Instantiate` outside the pool ·
reimport the pack · point a catalog row at gitignored pack art (§3) · rewrite `DragonBoss`/
`EnvironmentVFX`/`EliteVFXController` on this pass · hand-edit `.unity`/`.prefab` YAML (§0/§3 —
socket/component authoring goes through an editor script).

## 8. Acceptance
**Engineering:** `COMPILE_GATE_OK`; `PPACK_VFX_BUILD_OK`; `VFX_CATALOG_OK`; `REGRESSION_OK`; new
loop paths FlowTrace-instrumented; no nullref when a socket/bone is missing (root fallback).
**Felt (owner closes):** turret shots read elemental (fire/ice/arcane muzzle + bolt + impact);
hero weapon-skills/spells read elemental (Mage especially — the magic showcase); dungeon candles
flicker like flame; steam rises at vents; boss breath unchanged. Headless screenshot-verify the
dungeon + a turret volley + a hero cast before handing to owner (memory: open the PNGs).

## 9. Files summary
**CREATE:** `Vfx/VfxElement.cs`, `Vfx/VfxSocket.cs`, `Vfx/VfxFacade.cs`, `Vfx/VfxEmitter.cs`,
`Editor/ParticlePackVfxBuilder.cs`.
**EDIT (append/additive):** `Vfx/VFXType.cs` (+4), `Editor/VFXCatalogGenerator.cs` (+6 rows +
consts), `Buildings/TowerCombat.cs`, `Hero/HeroAbilities.cs`, `Editor/DungeonSceneBuilder.cs`.
**VERIFY ONLY (do not touch):** `Enemies/DragonBoss.cs`, `EnvironmentVFX.cs`, `EliteVFXController.cs`,
`BossFireBreathBuilder.cs`, `DeNelle-URP.asset`.

## 10. RESULT
Write `WorkOrders/WORK_ORDER_884_common_vfx_facade_and_particle_pack_deliverables.RESULT.md`:
files created/edited, all markers, the loop-cap decision (§6), and the 3 headless screenshots.
