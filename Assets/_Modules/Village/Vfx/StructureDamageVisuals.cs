// =============================================================================
// StructureDamageVisuals — WO-672 Slices B+D: the ONE presentation observer for
// structure damage state (F8-50, owner 2026-07-11: "is there a way to visually
// tell what is damaged? health bar or any notification, damaged maybe on fire?").
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WO-892 (2026-08-06) RE-SKINNED THE RECIPES AND ADDED THE CRITICAL-SAVE BEACON.
// The observer logic below is UNCHANGED by design — the scan, the tracked set, the
// threshold reads, the worst-first cap and the read-only contract are all WO-672's.
// What changed is WHICH recipe each state plays, and that a fourth state exists.
//
// LAW (WO-672 / ARCHITECTURE §2): presentation NEVER touches the objects. This
// system only READS each structure's damage surface (HpFraction / IsBroken /
// RepairTarget.DamageFraction) and drives world tells from data thresholds:
//
//   hp <= smolder (0.5) : Damage_Smolder — a thin, slow smoke wisp. No flame.
//   hp <= fire    (0.25): Damage_Fire — MediumFlames + a merged SmokeEffect layer,
//                         so the step up is a FLAME APPEARING plus roughly double
//                         the smoke. The bar's own critical pulse lands here too.
//   hp <= criticalBeacon: Damage_CriticalBeacon — an ADDITIONAL held loop, strobed
//                         at a fixed fast alarm cadence, plus a billboarded "!"
//                         glyph. THIS IS THE GAP WO-892 EXISTS TO CLOSE: fire alone
//                         says "damaged", it never says "act NOW or lose it".
//   broken (hp == 0)    : one-shot Damage_BreakBurst at the BREAK TRANSITION + a
//                         persistent Damage_Ruin column over the shell — and the
//                         beacon STOPS, because a destroyed structure cannot be
//                         saved (WO-753: destroyed is gone; only a full-cost
//                         rebuild returns it, so an alarm would be a lie).
//   any damage          : FloatingHealthBar (hideAtFull — bar only when damaged)
//
// GREYSCALE IS THE ACCEPTANCE CHANNEL (the owner is red/green colourblind). With
// all colour removed the four states are still four different things:
//   smolder  1 layer  · thin slow smoke     · no flame · steady
//   fire     2 layers · dense smoke volume  · FLAME    · steady
//   critical + a 4-layer spark loop with NO smoke, blinking hard at a fixed fast
//            rate, under a "!" glyph — RHYTHM and a GLYPH, not a tint
//   broken   one 5-layer grounded debris scatter, then a low wide 3-layer gutter
//            over something that is no longer standing
// The escalation smolder -> fire is DENSITY, fire -> critical is RHYTHM, and
// critical -> broken is a SHAPE change. None of the three is a hue.
//
// LANDSCAPE PHONE (2670x1200): the vertical axis is the scarce one and an upward
// column is what crops. Every recipe here is tuned low and close to the structure;
// only the "!" glyph is placed high, and it sits just above the health bar the
// player is already looking at.
//
// WHAT THE RECIPES REPLACE, and why this was a defect and not a re-paint:
//   * "Ember_Burn" is declared in HovlVfxCatalogGenerator.Map as a Hovl path that
//     DOES NOT EXIST ("Debuff 1.prefab"; the pack ships "Debuff chain" and "Debuff
//     scythe"). The generator skips a row whose prefab will not load, so the key
//     never reached HovlVfxCatalog.asset, and PlayKey on an absent key is a
//     throttled no-op. THE SMOLDER AND FIRE LOOPS HAD NEVER RENDERED.
//   * "Raid_Explosion" does resolve, but into /Assets/Hovl Studio/, which is
//     gitignored with zero files tracked — so the break burst only existed on a
//     machine carrying the 236 MB pack.
//   Both now point at TRACKED Particle Pack mirrors under Resources/VFX/Damage/.
//
// All VFX go through VFXManager.PlayKey (POOLED). TWO SEPARATE worst-first caps
// apply on top of VFXManager's own global 20-loop cap: maxBurnLoops for the
// smolder/fire/ruin loop and maxCriticalBeacons for the alarm. A structure holds
// AT MOST one of each, so the worst case this whole system can hold is
// maxBurnLoops + maxCriticalBeacons (8 + 3 = 11 by default). Every loop is held by
// a field on its Tracked record and released on every exit path — tier change,
// break, repair, cap eviction, host destroyed, OnDisable, OnDestroy.
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
            // WO-892: the HP fraction at/below which the "repair me NOW" alarm arms.
            // Defaults to the fire threshold (the registry places the beacon AT the fire
            // threshold) but is a separate dial so the owner can felt-tune "how early does
            // the game start shouting" without moving where flames appear.
            public float criticalBeacon = 0.25f;
            public float barOffset = 2.2f;
            public int maxBurnLoops = 8;
            // WO-892: a SEPARATE worst-first cap from maxBurnLoops. The beacon is an alarm,
            // and an alarm on eight things at once is noise rather than a call to action -
            // it should point at the two or three most urgent. Keeping it separate also
            // bounds this system's total loop footprint at maxBurnLoops + this, which
            // matters against VFXManager's global 20-loop cap.
            public int maxCriticalBeacons = 3;
        }

        [Serializable]
        public sealed class TypeOverrideDef
        {
            public bool optOut;
            public float? smolder;
            public float? fire;
            public float? criticalBeacon;
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

        /// <summary>
        /// WO-892: HP fraction at/below which the critical-save alarm arms (an ADDITIONAL
        /// loop on top of the fire tell, plus the "!" glyph). Separate dial from
        /// <see cref="Fire"/> so "when does it start shouting" is tunable independently of
        /// "when does it catch fire".
        /// </summary>
        public static float CriticalBeacon(string typeKey) =>
            Resolve(typeKey, o => o.criticalBeacon, _d().criticalBeacon);

        /// <summary>Minimum world-space health-bar height above the structure pivot.</summary>
        public static float BarOffset(string typeKey) => Resolve(typeKey, o => o.barOffset, _d().barOffset);

        /// <summary>Cap on simultaneous burn loops (worst-first) across all structures.</summary>
        public static int MaxBurnLoops { get { EnsureLoaded(); return Mathf.Max(1, _defaults.maxBurnLoops); } }

        /// <summary>WO-892: cap on simultaneous critical-save beacons (worst-first).</summary>
        public static int MaxCriticalBeacons
        {
            get { EnsureLoaded(); return Mathf.Max(1, _defaults.maxCriticalBeacons); }
        }

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
    /// on first damage, and drives capped smolder / fire / critical-beacon / ruin
    /// tells from the damage-states thresholds. Read-only over the structures.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StructureDamageVisuals : MonoBehaviour
    {
        // Scan (FindObjectsByType — the expensive part) is throttled hard; the
        // per-record evaluation (cheap delegate reads over the tracked set) runs
        // faster so a break burst lands near the actual transition, not seconds late.
        private const float ScanInterval = 2.0f;
        private const float EvalInterval = 0.3f;

        // ── WO-892 RECIPE KEYS (the re-skin; data, not logic) ────────────────────
        // Every one is a TRACKED Particle Pack mirror under Resources/VFX/Damage/,
        // authored by ParticlePackVfxBatchBuilder and wired in HovlVfxCatalogGenerator.
        // They replace "Ember_Burn" (declared against a Hovl path that does not exist,
        // so the key never reached the catalog and the smolder + fire loops had never
        // rendered on any machine) and "Raid_Explosion" (real, but gitignored Hovl art).
        private const string SmolderKey = "Damage_Smolder";        // A loop — thin smoke, no flame
        private const string FireKey    = "Damage_Fire";           // A loop — flame + smoke volume
        private const string RuinKey    = "Damage_Ruin";           // A loop — low wide gutter over a shell
        private const string BreakKey   = "Damage_BreakBurst";     // B one-shot — grounded collapse
        private const string BeaconKey  = "Damage_CriticalBeacon"; // A loop — the alarm

        // Burn-loop scale buckets (shape/motion tell, WO-672): the smolder recipe is
        // already thinned in the prefab, so this is a size step on top of a density step.
        private const float SmolderScale = 0.55f;
        private const float FireScale = 1.0f;

        // ── WO-892 alarm cadence (the colour-free "act NOW" channel) ────────────
        // FIXED rate, deliberately. HeroHpStateAura's breath ACCELERATES with severity
        // because it answers "how close am I to dying"; this answers a yes/no question -
        // "is this building about to be lost" - so it must read as a steady alarm, not a
        // gauge. A constant fast blink is also what the eye picks out of a busy raid.
        private const float BeaconHz = 2.6f;
        // The wave is SHARPENED (raised to a power) so most of each cycle is dark and the
        // bright part is a snap. A sine would read as breathing; this reads as blinking,
        // and blinking is what an alarm does.
        private const float BeaconWaveSharpness = 4f;
        private const float BeaconTrough = 0.12f;   // very nearly out between blinks
        private const float BeaconCrest  = 2.6f;    // hard over-drive on the blink
        private const float BeaconSimSpeed = 1.35f; // snappier pops, not a lazy sparkle

        // The "!" glyph. Placed just above the health bar the player is already reading,
        // and pulsed on the SAME wave as the sparks so glyph and effect are one signal.
        private const string BeaconGlyph = "!";
        private const float BeaconGlyphRise = 0.55f;   // above the bar
        private const float BeaconGlyphScaleMin = 0.85f;
        private const float BeaconGlyphScaleMax = 1.35f;

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
            public VFXHandle Burn;      // live smolder/fire/ruin loop (null = none)
            public int BurnTier;        // 0 none · 1 smolder · 2 fire/broken
            // WO-892: WHICH recipe the live burn loop is playing. Tier alone is no longer
            // enough to decide whether the loop needs restarting: a standing structure on
            // fire and a broken ruin are BOTH tier 2 but play different recipes, so
            // without this a building that broke while burning would keep its flame loop
            // and never show the ruin.
            public string BurnKey;
            public int PendingTier;     // this eval's desired tier (pre-cap)
            public bool WasBroken;
            public bool Observed;       // first eval done (no burst for arrived-broken shells)
            public bool CleanedUpOnBreak; // structure-death cleanup done (bar torn down + aura stopped)

            // -- WO-892 critical-save beacon ------------------------------------
            public VFXHandle Beacon;    // the live alarm loop (null = none)
            public bool WantsBeacon;    // this eval's desired state (pre-cap)
            public float BeaconPhase;   // blink accumulator (radians)
            public Transform BeaconTag; // the billboarded "!" glyph, built on demand
        }

        private readonly Dictionary<GameObject, Tracked> _tracked =
            new Dictionary<GameObject, Tracked>();
        private readonly List<GameObject> _dead = new List<GameObject>();      // scratch
        private readonly List<Tracked> _burnWants = new List<Tracked>();        // scratch
        private readonly List<Tracked> _beaconWants = new List<Tracked>();      // scratch
        // Records with a LIVE beacon, so the per-frame blink walks three entries rather
        // than the whole tracked set. Rebuilt by Evaluate; never the source of truth.
        private readonly List<Tracked> _beaconLive = new List<Tracked>();
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

        /// <summary>
        /// WO-892: this component being switched off is an EXIT PATH like any other. It
        /// was not one before — only OnDestroy released loops — so a disabled observer
        /// (a pooled/parked host, an editor toggle) would have stranded every held loop
        /// against VFXManager's global 20-loop cap with nothing able to release them.
        /// Releasing here is safe because the state is fully re-derivable: the next
        /// Evaluate re-acquires whatever the thresholds still call for.
        /// </summary>
        private void OnDisable() => ReleaseAllHeld("OnDisable");

        private void OnDestroy()
        {
            ReleaseAllHeld("OnDestroy");
            _tracked.Clear();
        }

        /// <summary>
        /// Return EVERY loop this observer holds — burn/ruin and beacon — and tear down
        /// the "!" glyphs. Idempotent, and safe to call with nothing held. A scene swap
        /// or a disable must never strand a loop; a stranded loop is invisible, permanent,
        /// and silently starves every later effect in the session.
        /// </summary>
        private void ReleaseAllHeld(string reason)
        {
            int loops = 0;
            foreach (var rec in _tracked.Values)
            {
                if (rec.Burn != null) { rec.Burn.Stop(immediate: true); rec.Burn = null; loops++; }
                rec.BurnTier = 0;
                rec.BurnKey = null;
                if (rec.Beacon != null) { rec.Beacon.Stop(immediate: true); rec.Beacon = null; loops++; }
                DestroyBeaconTag(rec);
            }
            _beaconLive.Clear();
            if (loops > 0)
                FlowTrace.Step("DamageVis",
                    $"released {loops} held loop(s) ({reason}) - burn + beacon slots returned to the pool.");
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

            // WO-892: the alarm blink runs EVERY FRAME, not on the 0.3 s eval tick — a
            // 2.6 Hz blink sampled at 3.3 Hz would alias into a random flicker, which
            // reads as a broken effect rather than an alarm. Walks _beaconLive (at most
            // maxCriticalBeacons entries), so this costs nothing when nothing is critical.
            if (_beaconLive.Count > 0)
                Guard.Try("DamageVis", "critical beacon pulse", DriveBeacons);
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

            // WO-891 (adjacent, reported): the PER-HIT flinch. Everything above this line
            // is a damage-STATE ladder driven by the 0.3 s Evaluate poll against DATA
            // THRESHOLDS - so a hit that does not cross 0.5 or 0.25 HP produced no reaction
            // at all, and one that did produced it up to a third of a second after the blow.
            // Being hit and being badly hurt were the same channel, and the first was silent.
            //
            // This is the ONE place every damageable structure in the game already passes
            // through, and it already holds the read-only HP delegate the flinch needs - so
            // walls, buildings, gates, towers, collectors and harvest sites are all covered
            // by one line, with no new damage model and no edit to any gameplay class.
            // Family B one-shot: it cannot consume a loop slot, and it is rate-limited
            // per-structure inside the component. The state ladder is untouched.
            StructureHitReaction.Attach(host, hp, string.IsNullOrEmpty(name) ? typeKey : name);
        }

        // ── EVALUATE — drive the tells from the observed state (fast, cheap) ────

        private void Evaluate()
        {
            _dead.Clear();
            _burnWants.Clear();
            _beaconWants.Clear();

            foreach (var kv in _tracked)
            {
                var rec = kv.Value;
                if (rec.Host == null)
                {
                    // Host destroyed under us — BOTH held loops go back, and the "!" glyph
                    // with them (it is parented to the host, but a Destroy on the host is
                    // deferred to end-of-frame and the glyph is torn down explicitly so the
                    // teardown is not order-dependent).
                    rec.Burn?.Stop(immediate: true);
                    rec.Burn = null;
                    rec.BurnTier = 0;
                    rec.BurnKey = null;
                    StopBeacon(rec, "host destroyed");
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
                    VFXManager.PlayKey(BreakKey, rec.VfxAnchor);
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
                    rec.BurnKey = null;
                }

                // ── WO-892: the CRITICAL-SAVE alarm ──────────────────────────────
                // Armed only on a structure that is BOTH critically damaged AND still
                // standing. A broken shell is deliberately excluded: WO-753 rules that a
                // destroyed structure is gone and returns only through a full-cost
                // rebuild, so an alarm on a ruin would be telling the player to do
                // something the game will not let them do.
                rec.WantsBeacon = !broken && hp <= DamageStatesCatalog.CriticalBeacon(rec.TypeKey);
                if (rec.WantsBeacon) _beaconWants.Add(rec);
                else if (rec.Beacon != null) StopBeacon(rec, "no longer critical");
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
                    rec.BurnKey = null;
                    continue;
                }

                // WO-892: WHICH recipe this tier wants. Tier 1 is the smoke wisp; tier 2
                // splits by whether the thing is still standing - a burning building and a
                // smoking ruin are different pictures, and before this they were the same
                // loop at the same scale.
                string wantKey = tier == 1 ? SmolderKey : (rec.WasBroken ? RuinKey : FireKey);

                // Restart when the TIER changed, when the RECIPE changed (the broken
                // transition, which keeps tier 2), or when the loop was lost / cap-skipped.
                if (rec.BurnTier == tier && rec.BurnKey == wantKey &&
                    rec.Burn != null && rec.Burn.IsAlive) continue;

                // Pooled; PlayKey returns null on the global loop cap or an absent key -
                // leave tier at 0 so the next eval retries rather than latching dark.
                rec.Burn?.Stop(immediate: true);
                rec.Burn = VFXManager.PlayKey(wantKey, rec.VfxAnchor,
                    scale: tier == 1 ? SmolderScale : FireScale);
                rec.BurnTier = rec.Burn != null ? tier : 0;
                rec.BurnKey  = rec.Burn != null ? wantKey : null;
                if (rec.Burn != null)
                    FlowTrace.Step("DamageVis",
                        $"burn {(tier == 1 ? "SMOLDER" : (rec.WasBroken ? "RUIN" : "FIRE"))} " +
                        $"('{wantKey}'): '{rec.Name}' ({rec.TypeKey})");
                else
                    FlowTrace.Throttle("DamageVis", "burn-refused", 5f,
                        $"burn loop REFUSED for '{rec.Name}' ({rec.TypeKey}) key='{wantKey}' - " +
                        "global loop cap hit, or the key is absent from HovlVfxCatalog. " +
                        "Retrying next eval; the structure shows no damage state meanwhile.");
            }

            // ── WO-892: capped critical-beacon assignment, worst-first ────────────
            // Deliberately a SECOND, SMALLER cap rather than sharing maxBurnLoops. An
            // alarm is only useful if it points somewhere: eight simultaneous alarms are
            // wallpaper, three are a decision. It also bounds this component's total loop
            // footprint at maxBurnLoops + maxCriticalBeacons against the global cap of 20.
            int beaconCap = DamageStatesCatalog.MaxCriticalBeacons;
            _beaconWants.Sort((a, b) =>
                Mathf.Clamp01(a.Hp != null ? a.Hp() : 1f)
                    .CompareTo(Mathf.Clamp01(b.Hp != null ? b.Hp() : 1f)));
            if (_beaconWants.Count > beaconCap)
                FlowTrace.Throttle("DamageVis", "beacon-cap", 5f,
                    $"critical beacons capped: {_beaconWants.Count} structures are critical, " +
                    $"cap={beaconCap} (worst-first kept - the alarm points at the ones closest to lost).");

            _beaconLive.Clear();
            for (int i = 0; i < _beaconWants.Count; i++)
            {
                var rec = _beaconWants[i];
                if (i >= beaconCap) { StopBeacon(rec, "cap eviction"); continue; }
                if (rec.Beacon != null && !rec.Beacon.IsAlive) StopBeacon(rec, "handle went dead");
                if (rec.Beacon == null) StartBeacon(rec);
                if (rec.Beacon != null) _beaconLive.Add(rec);
            }
        }

        // ── WO-892: the critical-save beacon ─────────────────────────────────────

        /// <summary>
        /// Arm the alarm on one structure: a held pooled loop plus the "!" glyph. A refused
        /// start (global loop cap / absent key) leaves the record with no beacon so the next
        /// Evaluate retries - a refusal must never latch, or a structure that became
        /// critical during a busy moment would stay silent for the rest of the raid.
        /// </summary>
        private void StartBeacon(Tracked rec)
        {
            if (rec == null || rec.Host == null) return;

            // Seated ON the structure body (the bounds centre), not above it: the phone is
            // landscape at 2670x1200 and the vertical axis is the one that crops. Parented
            // to the host so a moved/rebuilt structure carries its alarm with it and the
            // VFXManager destroyed-host sweep can reclaim the instance if the host dies
            // between evals.
            rec.Beacon = VFXManager.PlayKey(BeaconKey, rec.VfxAnchor,
                Quaternion.identity, rec.Host.transform);
            if (rec.Beacon == null)
            {
                FlowTrace.Throttle("DamageVis", "beacon-refused", 2f,
                    $"CRITICAL beacon REFUSED for '{rec.Name}' ({rec.TypeKey}) - global loop cap hit " +
                    "or key absent. This is the 'save it NOW' read; retrying next eval.");
                return;
            }

            rec.BeaconPhase = 0f;   // every alarm starts ON the beat, so it reads immediately

            // Clear any modulation left by this pooled instance's previous owner before
            // driving it (VfxLoopModulator restores on return, but capturing a clean
            // baseline here costs nothing and makes the first blink correct).
            var mod = rec.Beacon.Modulator;
            if (mod != null)
            {
                mod.SetSimulationSpeed(BeaconSimSpeed);
                mod.SetEmissionScale(BeaconCrest);
            }

            EnsureBeaconTag(rec);

            FlowTrace.Step("DamageVis",
                $"CRITICAL beacon ARMED: '{rec.Name}' ({rec.TypeKey}) hp={(rec.Hp != null ? rec.Hp() : 1f):0.00} " +
                $"- {BeaconHz:0.0} Hz blink + '{BeaconGlyph}' glyph (rhythm + glyph, not colour).");
        }

        /// <summary>
        /// Disarm the alarm and return its loop slot. Idempotent and safe with nothing
        /// held. Called from EVERY exit path: no longer critical, broken (a ruin cannot be
        /// saved), cap eviction, a dead handle, host destroyed, OnDisable and OnDestroy.
        /// </summary>
        private void StopBeacon(Tracked rec, string reason)
        {
            if (rec == null) return;
            bool had = rec.Beacon != null;
            if (had)
            {
                rec.Beacon.Stop();   // Stop restores the instance's modulation before pooling
                rec.Beacon = null;
            }
            DestroyBeaconTag(rec);
            if (had)
                FlowTrace.Step("DamageVis",
                    $"CRITICAL beacon released: '{rec.Name}' ({rec.TypeKey}) reason={reason} - loop slot returned.");
        }

        /// <summary>
        /// The per-frame blink. Runs over <c>_beaconLive</c> only (at most
        /// maxCriticalBeacons entries), and drives BOTH the spark emission and the glyph
        /// scale off ONE wave so the two are visibly the same signal rather than two
        /// effects that happen to be near each other.
        /// </summary>
        private void DriveBeacons()
        {
            float step = Time.deltaTime * BeaconHz * Mathf.PI * 2f;

            for (int i = _beaconLive.Count - 1; i >= 0; i--)
            {
                var rec = _beaconLive[i];
                if (rec == null || rec.Beacon == null || !rec.Beacon.IsAlive)
                {
                    // The pool reclaimed it under us (host destroyed, scene teardown).
                    // Drop it from the live list; Evaluate owns re-arming.
                    if (rec != null && rec.Beacon != null) StopBeacon(rec, "handle died mid-blink");
                    _beaconLive.RemoveAt(i);
                    continue;
                }

                rec.BeaconPhase += step;
                if (rec.BeaconPhase > Mathf.PI * 2f) rec.BeaconPhase -= Mathf.PI * 2f;

                // 0..1 sine, then SHARPENED: most of the cycle sits near zero and the peak
                // is a snap. A plain sine reads as breathing (which is what the hero's
                // wounded aura does, on purpose); an alarm has to read as blinking.
                float wave01 = 0.5f + 0.5f * Mathf.Sin(rec.BeaconPhase);
                float blink  = Mathf.Pow(wave01, BeaconWaveSharpness);

                var mod = rec.Beacon.Modulator;
                if (mod != null) mod.SetEmissionScale(Mathf.Lerp(BeaconTrough, BeaconCrest, blink));

                if (rec.BeaconTag != null)
                {
                    float s = Mathf.Lerp(BeaconGlyphScaleMin, BeaconGlyphScaleMax, blink);
                    rec.BeaconTag.localScale = new Vector3(s, s, s);

                    // Billboard the glyph so "!" is never read edge-on. Flat look-at, no
                    // roll - the same idiom DungeonExitBeacon uses for its EXIT label.
                    var cam = Camera.main;
                    if (cam != null)
                        rec.BeaconTag.rotation =
                            Quaternion.LookRotation(rec.BeaconTag.position - cam.transform.position);
                }
            }
        }

        /// <summary>
        /// Build the "!" glyph once per armed beacon. A legacy TextMesh, because code-built
        /// UI is the law here (UXML does not work in builds) and TextMesh is the idiom the
        /// project already uses for world labels (DungeonExitBeacon, BuildingSign). If no
        /// built-in font resolves, the beacon ships without the glyph and says so - the
        /// spark blink still carries the read on its own.
        /// </summary>
        private void EnsureBeaconTag(Tracked rec)
        {
            if (rec == null || rec.Host == null || rec.BeaconTag != null) return;

            var font = Guard.Try("DamageVis", "resolve builtin font",
                () => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"), null);
            if (font == null)
                font = Guard.Try("DamageVis", "resolve builtin font (Arial fallback)",
                    () => Resources.GetBuiltinResource<Font>("Arial.ttf"), null);
            if (font == null)
            {
                FlowTrace.Warn("DamageVis",
                    $"no builtin font resolved - '{rec.Name}' gets the blink without the '{BeaconGlyph}' glyph.");
                return;
            }

            var go = new GameObject("CriticalSaveTag");
            go.transform.SetParent(rec.Host.transform, false);
            // Just above the health bar the player is already reading, so the glyph and the
            // empty bar are one glance rather than two.
            go.transform.localPosition = Vector3.up * (rec.BarOffset + BeaconGlyphRise);

            var tm = go.AddComponent<TextMesh>();
            tm.text = BeaconGlyph;
            tm.font = font;
            tm.fontSize = 64;
            tm.characterSize = 0.16f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            // Neutral white ON PURPOSE. A red "!" would put the single most urgent signal
            // in this system on the one channel the owner cannot see - the exact defect
            // WO-888 fixed for low HP. The urgency is the GLYPH and the BLINK.
            tm.color = Color.white;

            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                if (font.material != null) mr.sharedMaterial = font.material;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }

            rec.BeaconTag = go.transform;
        }

        /// <summary>Tear the "!" glyph down. Safe when there is none, and when the host is
        /// already gone (the glyph is its child, but the teardown is explicit so it is not
        /// order-dependent).</summary>
        private void DestroyBeaconTag(Tracked rec)
        {
            if (rec == null || rec.BeaconTag == null) return;
            var go = rec.BeaconTag.gameObject;
            rec.BeaconTag = null;
            if (go != null) Destroy(go);
        }
    }
}
