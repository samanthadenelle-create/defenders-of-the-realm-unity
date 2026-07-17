// =============================================================================
// StructureDamageVisuals — WO-672 Slices B+D: the ONE presentation observer for
// structure damage state (F8-50, owner 2026-07-11: "is there a way to visually
// tell what is damaged? health bar or any notification, damaged maybe on fire?").
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// LAW (WO-672 / ARCHITECTURE §2): presentation NEVER touches the objects. This
// system only READS each structure's damage surface (HpFraction / IsBroken /
// RepairTarget.DamageFraction) and drives world tells from data thresholds:
//
//   hp <= smolder (0.5) : Ember_Burn Hovl loop at REDUCED scale ("smoldering")
//   hp <= fire    (0.25): Ember_Burn at FULL scale ("on fire") — the bar's own
//                         critical pulse (<=0.25) lands at the same threshold
//   broken (hp == 0)    : one-shot Raid_Explosion at the BREAK TRANSITION +
//                         a persistent full-scale ember over the shell
//   any damage          : FloatingHealthBar (hideAtFull — bar only when damaged;
//                         pinned empty on a broken shell, never torn down)
//
// Shape/motion carries the meaning (fire loop + bar fill + pulse), never color
// alone — colorblind-safe by construction. All VFX go through VFXManager.PlayKey
// (POOLED); simultaneous burn loops are capped worst-first (data maxBurnLoops)
// on top of VFXManager's own global loop cap.
//
// COVERAGE (each through its EXISTING read-only surface — no new damage model):
//   • WallSegment / Building — wrapped via RepairTarget (uniform 0..1 fraction)
//   • Gate                   — data OPT-OUT (bespoke force-field collapse tell)
//   • HeartController        — never scanned (bespoke 7-state crystal tell)
//   • ResourceCollector      — HpFraction / IsBroken (registry)
//   • Tower / DefenseTower / ArcaneTower / HarvestSite — HpFraction / IsBroken
//     (WO-672 Slice A members, added by the lifecycle lane)
//
// SELF-INSTALLING (mirrors WaveFeedbackDirector): [RuntimeInitializeOnLoadMethod]
// + sceneLoaded hook spawn one instance per scene; no scene edit, no drag-drop.
// Thresholds come from Data/Canonical/damage-states.json via CanonicalJson
// (dual-copy, WebGL-safe) — "data only always"; per-type overrides + optOut.
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Newtonsoft.Json;
using DeNelle.Core;
using DeNelle.Core.Diagnostics;
using DeNelle.Village.Buildings.Progression;

namespace DeNelle.Village
{
    /// <summary>
    /// Typed loader for damage-states.json (WO-672 Slice D): global thresholds
    /// (smolder / fire / barOffset / maxBurnLoops) + per-type overrides with an
    /// optOut flag for structures that carry their own bespoke damage tell
    /// (gate force-field, heart crystal). Lazy, cached, WebGL-safe.
    /// </summary>
    public static class DamageStatesCatalog
    {
        /// <summary>StreamingAssets-relative path (CanonicalJson resolves Resources first).</summary>
        public const string StreamingRelativePath = "Data/Canonical/damage-states.json";

        [Serializable]
        public sealed class DefaultsDef
        {
            public float smolder = 0.5f;
            public float fire = 0.25f;
            public float barOffset = 2.2f;
            public int maxBurnLoops = 8;
        }

        [Serializable]
        public sealed class TypeOverrideDef
        {
            public bool optOut;
            public float? smolder;
            public float? fire;
            public float? barOffset;
        }

        [Serializable]
        private sealed class FileDef
        {
            public int version;
            public DefaultsDef defaults;
            public Dictionary<string, TypeOverrideDef> perType;
        }

        private static DefaultsDef _defaults;
        private static Dictionary<string, TypeOverrideDef> _perType;

        /// <summary>Force a fresh reload on next access (e.g. after an editor JSON edit).</summary>
        public static void Invalidate()
        {
            _defaults = null;
            _perType = null;
        }

        /// <summary>True when <paramref name="typeKey"/> opts out of the shared damage tells.</summary>
        public static bool OptOut(string typeKey)
        {
            EnsureLoaded();
            return _perType.TryGetValue(typeKey ?? string.Empty, out var o) && o != null && o.optOut;
        }

        /// <summary>HP fraction at/below which the reduced-scale smolder loop shows.</summary>
        public static float Smolder(string typeKey) => Resolve(typeKey, o => o.smolder, _d().smolder);

        /// <summary>HP fraction at/below which the full-scale fire loop shows.</summary>
        public static float Fire(string typeKey) => Resolve(typeKey, o => o.fire, _d().fire);

        /// <summary>Minimum world-space health-bar height above the structure pivot.</summary>
        public static float BarOffset(string typeKey) => Resolve(typeKey, o => o.barOffset, _d().barOffset);

        /// <summary>Cap on simultaneous burn loops (worst-first) across all structures.</summary>
        public static int MaxBurnLoops { get { EnsureLoaded(); return Mathf.Max(1, _defaults.maxBurnLoops); } }

        private static DefaultsDef _d() { EnsureLoaded(); return _defaults; }

        private static float Resolve(string typeKey, Func<TypeOverrideDef, float?> pick, float fallback)
        {
            EnsureLoaded();
            if (_perType.TryGetValue(typeKey ?? string.Empty, out var o) && o != null)
            {
                float? v = pick(o);
                if (v.HasValue) return v.Value;
            }
            return fallback;
        }

        private static void EnsureLoaded()
        {
            if (_defaults != null) return;
            _defaults = new DefaultsDef();
            _perType = new Dictionary<string, TypeOverrideDef>(StringComparer.OrdinalIgnoreCase);
            try
            {
                string text = CanonicalJson.Read(StreamingRelativePath);
                if (string.IsNullOrEmpty(text))
                {
                    FlowTrace.Warn("DamageVis",
                        $"DamageStatesCatalog: {StreamingRelativePath} not found — code defaults in effect " +
                        $"(smolder {_defaults.smolder}, fire {_defaults.fire}).");
                    return;
                }
                var file = JsonConvert.DeserializeObject<FileDef>(text);
                if (file == null)
                {
                    FlowTrace.Warn("DamageVis", "DamageStatesCatalog: damage-states.json parsed null — code defaults in effect.");
                    return;
                }
                if (file.defaults != null) _defaults = file.defaults;
                if (file.perType != null)
                    foreach (var kv in file.perType)
                        if (!string.IsNullOrEmpty(kv.Key) && kv.Value != null)
                            _perType[kv.Key] = kv.Value;
                FlowTrace.Step("DamageVis",
                    $"DamageStatesCatalog loaded v{file.version}: smolder={_defaults.smolder} fire={_defaults.fire} " +
                    $"barOffset={_defaults.barOffset} maxBurnLoops={_defaults.maxBurnLoops} perType={_perType.Count}.");
            }
            catch (Exception ex)
            {
                FlowTrace.Fail("DamageVis",
                    $"DamageStatesCatalog: failed to parse damage-states.json ({ex.Message}) — code defaults in effect.");
            }
        }
    }

    /// <summary>
    /// The one pooled damage-presentation observer (WO-672 Slice B): scans for
    /// damageable structures on a throttled timer, attaches a FloatingHealthBar
    /// on first damage, and drives capped Ember_Burn / Raid_Explosion tells from
    /// the damage-states thresholds. Read-only over the structures.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StructureDamageVisuals : MonoBehaviour
    {
        // Scan (FindObjectsByType — the expensive part) is throttled hard; the
        // per-record evaluation (cheap delegate reads over the tracked set) runs
        // faster so a break burst lands near the actual transition, not seconds late.
        private const float ScanInterval = 2.0f;
        private const float EvalInterval = 0.3f;

        // Burn-loop scale buckets (shape/motion tell, WO-672):
        private const float SmolderScale = 0.55f;   // reduced-scale ember ("smoldering")
        private const float FireScale = 1.0f;       // full-scale ember ("on fire" / broken shell)

        /// <summary>One observed structure (read-only view; presentation never mutates it).</summary>
        private sealed class Tracked
        {
            public GameObject Host;
            public string TypeKey;      // damage-states perType key ("wall"/"tower"/...)
            public string Name;         // trace label
            public Func<float> Hp;      // 0..1 HP fraction (1 = pristine)
            public Func<bool> Broken;   // true once an inoperable shell
            public Vector3 VfxAnchor;   // bounds centre — where the ember/burst sits
            public float BarOffset;     // world height for the floating bar
            public bool BarAttached;
            public VFXHandle Burn;      // live ember loop (null = none)
            public int BurnTier;        // 0 none · 1 smolder · 2 fire/broken
            public int PendingTier;     // this eval's desired tier (pre-cap)
            public bool WasBroken;
            public bool Observed;       // first eval done (no burst for arrived-broken shells)
            public bool CleanedUpOnBreak; // structure-death cleanup done (bar torn down + aura stopped)
        }

        private readonly Dictionary<GameObject, Tracked> _tracked =
            new Dictionary<GameObject, Tracked>();
        private readonly List<GameObject> _dead = new List<GameObject>();      // scratch
        private readonly List<Tracked> _burnWants = new List<Tracked>();        // scratch
        private float _scanTimer;
        private float _evalTimer;

        // ── Runtime install (no scene edit — mirrors WaveFeedbackDirector) ──────

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallHook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TrySpawn();   // the first scene is already loaded when this runs
        }

        private static void OnSceneLoaded(Scene s, LoadSceneMode mode) => TrySpawn();

        private static void TrySpawn()
        {
            if (UnityEngine.Object.FindAnyObjectByType<StructureDamageVisuals>() != null) return;
            var go = new GameObject("StructureDamageVisuals");
            go.AddComponent<StructureDamageVisuals>();
            FlowTrace.Step("DamageVis",
                $"installed (scene='{SceneManager.GetActiveScene().name}') — structure damage tells active.");
        }

        // ── Lifecycle ────────────────────────────────────────────────────────────

        private void OnEnable()
        {
            _scanTimer = 0f;    // scan immediately on install
            _evalTimer = 0f;
        }

        private void OnDestroy()
        {
            // Return every live ember loop to the pool — a scene swap must never
            // strand a loop against VFXManager's global cap.
            foreach (var rec in _tracked.Values)
            {
                rec.Burn?.Stop(immediate: true);
                rec.Burn = null;
            }
            _tracked.Clear();
        }

        private void Update()
        {
            _scanTimer -= Time.deltaTime;
            if (_scanTimer <= 0f)
            {
                _scanTimer = ScanInterval;
                Guard.Try("DamageVis", "structure scan", Scan);
            }

            _evalTimer -= Time.deltaTime;
            if (_evalTimer <= 0f)
            {
                _evalTimer = EvalInterval;
                Guard.Try("DamageVis", "damage-state eval", Evaluate);
            }
        }

        // ── SCAN — register every damageable structure (throttled) ──────────────

        private void Scan()
        {
            // Wall / Building through the uniform RepairTarget view (Gate is a data
            // opt-out: its force-field collapse is the already-good bespoke tell; the
            // Heart's crystal states likewise — HeartController is never scanned).
            RegisterRepairables<WallSegment>("wall");
            RegisterRepairables<Building>("building");
            if (!DamageStatesCatalog.OptOut("gate"))
                RegisterRepairables<Gate>("gate");

            // Resource collectors — HpFraction / IsBroken via the registry.
            if (!DamageStatesCatalog.OptOut("collector"))
            {
                foreach (var c in ResourceCollectorRegistry.All)
                {
                    if (c == null || _tracked.ContainsKey(c.gameObject)) continue;
                    var cc = c;   // capture the loop variable, not the iterator
                    Register(c.gameObject, "collector", c.BuildingId,
                        () => cc != null ? cc.HpFraction : 1f,
                        () => cc != null && cc.IsBroken);
                }
            }

            // Towers + harvest sites — the WO-672 Slice A surface (HpFraction /
            // IsBroken, added by the lifecycle lane; broken shells persist).
            if (!DamageStatesCatalog.OptOut("tower"))
                foreach (var t in UnityEngine.Object.FindObjectsByType<Tower>(FindObjectsSortMode.None))
                {
                    if (t == null || _tracked.ContainsKey(t.gameObject)) continue;
                    var tt = t;
                    Register(t.gameObject, "tower", t.gameObject.name,
                        () => tt != null ? tt.HpFraction : 1f, () => tt != null && tt.IsBroken);
                }
            if (!DamageStatesCatalog.OptOut("defensetower"))
                foreach (var t in UnityEngine.Object.FindObjectsByType<DefenseTower>(FindObjectsSortMode.None))
                {
                    if (t == null || _tracked.ContainsKey(t.gameObject)) continue;
                    var tt = t;
                    Register(t.gameObject, "defensetower", t.gameObject.name,
                        () => tt != null ? tt.HpFraction : 1f, () => tt != null && tt.IsBroken);
                }
            if (!DamageStatesCatalog.OptOut("arcanetower"))
                foreach (var t in UnityEngine.Object.FindObjectsByType<ArcaneTower>(FindObjectsSortMode.None))
                {
                    if (t == null || _tracked.ContainsKey(t.gameObject)) continue;
                    var tt = t;
                    Register(t.gameObject, "arcanetower", t.gameObject.name,
                        () => tt != null ? tt.HpFraction : 1f, () => tt != null && tt.IsBroken);
                }
            if (!DamageStatesCatalog.OptOut("harvestsite"))
                foreach (var t in UnityEngine.Object.FindObjectsByType<DeNelle.Village.World.HarvestSite>(FindObjectsSortMode.None))
                {
                    if (t == null || _tracked.ContainsKey(t.gameObject)) continue;
                    var tt = t;
                    Register(t.gameObject, "harvestsite", t.gameObject.name,
                        () => tt != null ? tt.HpFraction : 1f, () => tt != null && tt.IsBroken);
                }
        }

        /// <summary>Register damaged-or-not structures of type <typeparamref name="T"/>
        /// through the uniform RepairTarget wrapping (never re-branching per type).</summary>
        private void RegisterRepairables<T>(string typeKey) where T : Component
        {
            foreach (var s in UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None))
            {
                if (s == null || _tracked.ContainsKey(s.gameObject)) continue;
                var target = RepairTarget.TryWrap(s);
                if (target == null || !target.IsValid) continue;
                Register(s.gameObject, typeKey, target.DisplayName,
                    () => target.IsValid ? 1f - target.DamageFraction : 1f,
                    () => target.IsValid && target.DamageFraction >= 0.999f);
            }
        }

        private void Register(GameObject host, string typeKey, string name,
            Func<float> hp, Func<bool> broken)
        {
            // VFX anchor + bar offset from the renderer bounds (structures do not
            // move); the data barOffset is the floor so a flat foundation still
            // floats its bar clear of the mesh. FloatingHealthBar clamps the top end.
            Vector3 anchor = host.transform.position + Vector3.up * 0.5f;
            float offset = DamageStatesCatalog.BarOffset(typeKey);
            var renderers = host.GetComponentsInChildren<Renderer>();
            if (renderers != null && renderers.Length > 0)
            {
                Bounds b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    if (renderers[i] != null) b.Encapsulate(renderers[i].bounds);
                anchor = b.center;
                offset = Mathf.Max(offset, b.max.y - host.transform.position.y + 0.4f);
            }

            _tracked[host] = new Tracked
            {
                Host = host,
                TypeKey = typeKey,
                Name = string.IsNullOrEmpty(name) ? typeKey : name,
                Hp = hp,
                Broken = broken,
                VfxAnchor = anchor,
                BarOffset = offset,
            };
        }

        // ── EVALUATE — drive the tells from the observed state (fast, cheap) ────

        private void Evaluate()
        {
            _dead.Clear();
            _burnWants.Clear();

            foreach (var kv in _tracked)
            {
                var rec = kv.Value;
                if (rec.Host == null)
                {
                    rec.Burn?.Stop(immediate: true);
                    rec.Burn = null;
                    _dead.Add(kv.Key);
                    continue;
                }

                float hp = Mathf.Clamp01(rec.Hp != null ? rec.Hp() : 1f);
                bool broken = rec.Broken != null && rec.Broken();
                if (broken) hp = 0f;   // a broken shell always reads as empty

                // Structure-death cleanup (owner felt-test 2026-07-15: "tower was
                // destroyed ... but the vfx and 0 health bar still exist"). A DESTROYED
                // (broken) shell tears its HP bar down and STOPS any arcane aura loop -
                // this reverses WO-672's "pin the empty bar on the shell", because the
                // root is NOT destroyed on break (Tower/ArcaneTower go to a broken shell,
                // so ArcaneAura.OnDisable/OnDestroy never fire and the aura keeps looping).
                // A still-standing damaged structure gets the lazy first-damage bar as
                // before; a repaired structure restores both.
                if (broken)
                {
                    if (!rec.CleanedUpOnBreak)
                    {
                        rec.CleanedUpOnBreak = true;

                        var bar = rec.Host.GetComponent<FloatingHealthBar>();
                        bool hadBar = bar != null;
                        if (hadBar) bar.Teardown();
                        rec.BarAttached = false;

                        var aura = rec.Host.GetComponentInChildren<ArcaneAura>(true);
                        bool hadAura = aura != null;
                        if (hadAura) aura.StopAndDisable();

                        FlowTrace.Step("StructureDeath",
                            $"cleanup '{rec.Name}' ({rec.TypeKey}): HP bar {(hadBar ? "TORN-DOWN" : "none")} + " +
                            $"arcane aura {(hadAura ? "STOPPED" : "n/a")} - dead shell shows no empty 0-bar, no aura loop.");
                    }
                }
                else
                {
                    if (rec.CleanedUpOnBreak)
                    {
                        // Repaired / standing again - restore the aura we stopped on break
                        // (symmetric cleanup so a repaired spire does not lose its aura).
                        var aura = rec.Host.GetComponentInChildren<ArcaneAura>(true);
                        if (aura != null && !aura.enabled) aura.enabled = true;
                        rec.CleanedUpOnBreak = false;
                        FlowTrace.Step("StructureDeath",
                            $"restore '{rec.Name}' ({rec.TypeKey}): repaired - aura {(aura != null ? "re-enabled" : "n/a")}.");
                    }

                    // Health bar - attach lazily on first damage (hideAtFull keeps it
                    // invisible again at full HP).
                    if (!rec.BarAttached && hp < 0.999f)
                    {
                        FloatingHealthBar.Attach(rec.Host, rec.Hp, () => false,
                            heightOffset: rec.BarOffset, hideAtFull: true, destroyOnDead: false);
                        rec.BarAttached = true;
                        FlowTrace.Step("DamageVis",
                            $"bar attached: '{rec.Name}' ({rec.TypeKey}) hp={hp:0.00}");
                    }
                }

                // Break transition — one-shot burst at the moment it broke. A shell
                // that was ALREADY broken when first observed (scene load / save
                // restore) gets the persistent ember only, never a phantom burst.
                if (broken && !rec.WasBroken && rec.Observed)
                {
                    VFXManager.PlayKey("Raid_Explosion", rec.VfxAnchor);
                    FlowTrace.Step("DamageVis", $"BREAK burst: '{rec.Name}' ({rec.TypeKey})");
                }
                rec.WasBroken = broken;
                rec.Observed = true;

                // Desired burn tier from the data thresholds.
                int wantTier = broken ? 2
                    : hp <= DamageStatesCatalog.Fire(rec.TypeKey) ? 2
                    : hp <= DamageStatesCatalog.Smolder(rec.TypeKey) ? 1
                    : 0;
                rec.PendingTier = wantTier;
                if (wantTier > 0)
                {
                    _burnWants.Add(rec);   // capped assignment happens below
                }
                else if (rec.BurnTier != 0 || rec.Burn != null)
                {
                    rec.Burn?.Stop();
                    rec.Burn = null;
                    rec.BurnTier = 0;
                }
            }

            foreach (var key in _dead) _tracked.Remove(key);

            // ── Capped burn assignment: worst-first (lowest HP) keeps its loop ────
            int cap = DamageStatesCatalog.MaxBurnLoops;
            _burnWants.Sort((a, b) =>
                Mathf.Clamp01(a.Hp != null ? a.Hp() : 1f)
                    .CompareTo(Mathf.Clamp01(b.Hp != null ? b.Hp() : 1f)));
            if (_burnWants.Count > cap)
                FlowTrace.Throttle("DamageVis", "burn-cap", 5f,
                    $"burn loops capped: {_burnWants.Count} structures want fire, cap={cap} (worst-first kept).");

            for (int i = 0; i < _burnWants.Count; i++)
            {
                var rec = _burnWants[i];
                int tier = i < cap ? rec.PendingTier : 0;

                if (tier == 0)
                {
                    if (rec.Burn != null) { rec.Burn.Stop(); rec.Burn = null; }
                    rec.BurnTier = 0;
                    continue;
                }
                if (rec.BurnTier == tier && rec.Burn != null && rec.Burn.IsAlive) continue;

                // Tier changed (or the loop was lost / cap-skipped earlier): restart
                // at the new scale. Pooled; PlayKey returns null on the global loop
                // cap — leave tier at 0 so the next eval retries.
                rec.Burn?.Stop(immediate: true);
                rec.Burn = VFXManager.PlayKey("Ember_Burn", rec.VfxAnchor,
                    scale: tier == 1 ? SmolderScale : FireScale);
                rec.BurnTier = rec.Burn != null ? tier : 0;
                if (rec.Burn != null)
                    FlowTrace.Step("DamageVis",
                        $"burn {(tier == 1 ? "SMOLDER" : "FIRE")}: '{rec.Name}' ({rec.TypeKey})");
            }
        }
    }
}
