// =============================================================================
// VFXManager.Hovl — string-key Hovl-prefab VFX path (WO-VFX-002).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village   (partial of VFXManager)
//
// WHY A PARTIAL, NOT A SECOND SYSTEM:
//   The existing VFXManager already owns the singleton, the DontDestroyOnLoad
//   _poolRoot, the oneshot/loop caps, the FlowTrace instrumentation, and the
//   ParticleSystem play/stop/duration helpers. WO-VFX-002 adds a *second key
//   space* (arbitrary string keys -> Hovl Studio prefabs) that reuses ALL of that.
//   So this is a partial of the SAME class, not a duplicate manager. The VFXType
//   enum path (VFXManager.cs) is untouched and keeps working exactly as before.
//
// WHAT THIS ADDS:
//   • PlayKey("Fireball_Projectile", pos, parent, color, scale, lifetime, follow)
//     — spawn any Hovl prefab by string key, routed through the shared pool.
//   • Object pooling keyed by string (mobile-critical): instantiate once, return
//     on lifetime end — no per-call Instantiate. Reuses _poolRoot + the caps.
//   • Override knobs: world-space position + rotation, PARENT to a transform,
//     HDR COLOR (recolour via ParticleSystem StartColor across all children —
//     the Hovl HS_Blend_CG effects tint at runtime), SCALE, LIFETIME, and a
//     FOLLOW target (HovlVfxFollower keeps the effect on a moving transform).
//   • Designer-friendly registration WITHOUT code: HovlVfxCatalog ScriptableObject
//     (Resources/VFX/HovlVfxCatalog.asset) holds { Key, Prefab, PoolSize,
//     DefaultScale, DefaultLifetime, Recolorable, IsLoop } rows. Authored in-editor
//     (Hovl prefabs are NOT under Resources/, so the catalog holds serialized prefab
//     refs; the .asset itself lives in Resources/ and is the only new Resources item).
//
// AUTHORING (copy-paste, 8-10 example rows) — exact Hovl paths from the shortlist
// in Docs/VFX/HovlStudio_Inventory.md §5. Run Defenders/VFX/Generate Hovl VFX Catalog
// (HovlVfxCatalogGenerator) to author the .asset from this table, or wire by hand:
//
//   KEY                    PREFAB (under Assets/Hovl Studio/)                                              LOOP  SCALE
//   Fireball_Projectile    AAA Projectiles Vol 1/Prefabs/Projectile VFX loop/Projectile 16 fire.prefab      Y    1
//   Fireball_Cast          AAA Projectiles Vol 1/Prefabs/Flash and hits/Flash 16 fire.prefab                N    1
//   Fireball_Impact        AAA Projectiles Vol 1/Prefabs/Flash and hits/Hit 16 fire.prefab                  N    1
//   Thunderbolt_Projectile AAA Projectiles Vol 1/Prefabs/Projectile VFX loop/Projectile 2 electro.prefab    Y    1
//   Thunderbolt_Impact     AAA Projectiles Vol 1/Prefabs/Flash and hits/Hit 2 electro.prefab                N    1
//   Arcane_Projectile      AAA Projectiles Vol 1/Prefabs/Projectile VFX loop/Projectile 17 nova violet.prefab Y  1
//   Arcane_Cast            AAA Projectiles Vol 1/Prefabs/Flash and hits/Flash 17 nova violet.prefab          N    1
//   Arcane_Impact          AAA Projectiles Vol 1/Prefabs/Flash and hits/Hit 17 nova violet.prefab            N    1
//   Frost_Projectile       AAA Projectiles Vol 1/Prefabs/Projectile VFX loop/Projectile 26 blue diamond.prefab Y 1
//   Frost_Impact           AAA Projectiles Vol 1/Prefabs/Flash and hits/Hit 26 blue crystal.prefab           N    1
//   Collector_Full         RPG VFX Bundle/Random effect prefabs/Gold dot.prefab                             Y    1
//   Raid_Explosion         AOE Magic spells Vol.1/Prefabs/Meteor hit.prefab                                 N    1.5
//   LevelUp_Burst          RPG VFX Bundle/Random effect prefabs/Lvl up.prefab                               N    1
//
//   — WO-VFX-003 Knight skill-tree actives (13 new keys; map = Docs/VFX/SkillTree_VFX_Mapping.md) —
//   Thunderbolt_Cast       AAA Projectiles Vol 1/Prefabs/Flash and hits/Flash 2 electro.prefab              N    1
//   Spear_Projectile       AAA Projectiles Vol 1/Prefabs/Projectile VFX loop/Projectile 11 orange arrow.prefab Y  1
//   Spear_Impact           AAA Projectiles Vol 1/Prefabs/Flash and hits/Hit 11 orange arrow.prefab          N    1
//   Melee_Slash            AOE Magic spells Vol.1/Prefabs/Flower slash.prefab                               N    1
//   Melee_Impact           RPG VFX Bundle/Random effect prefabs/Punch Hit.prefab                            N    1
//   Cleave_Impact          AOE Magic spells Vol.1/Prefabs/Energy explosion.prefab                           N    1.3
//   Heal_Cast              Magic circles/Prefabs/Magic circle sun.prefab                                    N    1
//   Heal_Aura              RPG VFX Bundle/Random effect prefabs/Buff heal.prefab                            Y    1  (loop)
//   Taunt_Roar             AOE Magic spells Vol.1/Prefabs/Energy explosion.prefab                           N    1
//   Taunt_Aura             Magic circles/Prefabs/Loop version/Magic circle blood loop.prefab               Y    1  (loop)
//   Aegis_Cast             Magic circles/Prefabs/Magic shield holy.prefab                                   N    1
//   Aegis_Shield           Magic circles/Prefabs/Loop version/Magic shield holy loop.prefab                Y    1  (loop)
//   Ember_Burn             RPG VFX Bundle/Random effect prefabs/Debuff 1.prefab                             Y    1  (loop)
//   Dash_Blink             RPG VFX Bundle/Random effect prefabs/Buff white twist.prefab                     N    1
//
// USAGE:
//   // oneshot cast flash on the hero's hand, tinted blue, 1.3x scale:
//   VFXManager.PlayKey("Fireball_Cast", hand.position, hand.rotation, hand, new Color(0.4f,0.6f,1f,1f), 1.3f);
//   // projectile trail that follows a moving target transform, returns a handle:
//   var h = VFXManager.PlayKey("Fireball_Projectile", spawn, default, null, null, 0f, 0f, targetTf);
//   h?.Stop();                                  // call on impact
//   // collector FULL glow (loop) — keep the handle:
//   var glow = VFXManager.PlayKey("Collector_Full", stackTop);
//   glow?.Stop();                               // when the collector is emptied
// =============================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    public sealed partial class VFXManager
    {
        // ── Hovl catalog + pools (string-keyed, parallel to the VFXType path) ────

        [Header("Hovl VFX (WO-VFX-002)")]
        [Tooltip("String-key catalog of Hovl Studio prefabs. Auto-loaded from " +
                 "Resources/VFX/HovlVfxCatalog when null. Author via " +
                 "Defenders/VFX/Generate Hovl VFX Catalog.")]
        [SerializeField] private HovlVfxCatalog _hovlCatalog;

        // Per-key queue of dormant instances ready to reuse (mirrors _pools).
        private readonly Dictionary<string, Queue<GameObject>> _hovlPools
            = new Dictionary<string, Queue<GameObject>>();

        // Which key a live pooled object belongs to (so a VFXHandle can return it).
        private readonly Dictionary<GameObject, string> _hovlKeyOf
            = new Dictionary<GameObject, string>();

        // Loop objects, so the shared _activeLoops counter is decremented for the
        // right bucket on return (mirrors _loopObjects for the VFXType path).
        private readonly HashSet<GameObject> _hovlLoopObjects = new HashSet<GameObject>();

        // WO-VFX #2 (hue-shift tint): the AUTHORED startColor of each child ParticleSystem,
        // cached the first time that PS is recolored. Every pooled reuse then hue-shifts from
        // the ORIGINAL saturation / value (brightness) / alpha instead of a previously written
        // (already hue-shifted) value - so an HDR round-trip can't drag the S/V off the authored
        // bright core across reuses, and a later acquire with a DIFFERENT hue still starts clean.
        // Keyed by the PS; pooled instances live for the app lifetime, so the entry stays valid.
        private readonly Dictionary<ParticleSystem, ParticleSystem.MinMaxGradient> _hovlAuthoredStartColor
            = new Dictionary<ParticleSystem, ParticleSystem.MinMaxGradient>();

        // ── VFXType -> Hovl string-key bridge (aura wiring) ──────────────────────
        // A few VFXType LOOPS have no VFXType-catalog (VFXCatalog.asset) prefab but a
        // curated Hovl loop wired by string key in the HovlVfxCatalog. PlayLoop consults
        // this map BEFORE the procedural fallback, so the real pooled Hovl glow plays
        // instead of the textureless additive billboard SQUARES the procedural system
        // draws. Aura_HeartPulse is shared by BOTH HeartAuraController (the Heart-of-
        // Elarion tree nucleus) and EchoSpiritPresentation (the founding-Echo spirit) --
        // both call PlayAura(Aura_HeartPulse) -- so one bridge row fixes both auras.
        // Add a row here (+ the matching key in HovlVfxCatalogGenerator.Map + a catalog
        // regen) to route any other unwired VFXType loop to a Hovl prefab.
        private static readonly Dictionary<VFXType, string> _hovlKeyForType
            = new Dictionary<VFXType, string>
            {
                { VFXType.Aura_HeartPulse, "Aura_HeartPulse" },
            };

        /// <summary>True + the Hovl catalog key when <paramref name="type"/> should
        /// resolve through the string-keyed Hovl path instead of the VFXType pool.</summary>
        private static bool TryGetHovlKeyForType(VFXType type, out string key)
            => _hovlKeyForType.TryGetValue(type, out key);

        // ── Catalog load / pool pre-warm ─────────────────────────────────────────

        private void EnsureHovlCatalog()
        {
            if (_hovlCatalog != null) return;
            _hovlCatalog = Resources.Load<HovlVfxCatalog>("VFX/HovlVfxCatalog");
            if (_hovlCatalog == null)
                FlowTrace.Warn("VFXManager",
                    "EnsureHovlCatalog: no _hovlCatalog assigned and Resources/VFX/HovlVfxCatalog not found — " +
                    "PlayKey('...') calls will no-op until the catalog is authored " +
                    "(Defenders/VFX/Generate Hovl VFX Catalog).");
            else
                FlowTrace.Step("VFXManager",
                    $"EnsureHovlCatalog: loaded HovlVfxCatalog ({_hovlCatalog.Rows?.Length ?? 0} rows).");
        }

        private void InitialiseHovlPools()
        {
            if (_hovlCatalog == null) return;
            _hovlCatalog.BuildLookup();

            foreach (var row in _hovlCatalog.Rows)
            {
                if (string.IsNullOrEmpty(row.Key) || row.Prefab == null || row.PoolSize <= 0) continue;
                if (!_hovlPools.ContainsKey(row.Key))
                    _hovlPools[row.Key] = new Queue<GameObject>();

                for (int i = 0; i < row.PoolSize; i++)
                {
                    var go = CreateHovlInstance(row.Prefab, row.Key);
                    if (go != null) _hovlPools[row.Key].Enqueue(go);
                }
            }
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Spawn a Hovl prefab by string key, routed through the shared pool. Null-safe —
        /// no-ops (returns null) if VFXManager or the catalog is not ready, or the key is
        /// unknown. Returns a <see cref="VFXHandle"/> for LOOP effects (call Stop() when
        /// done); oneshots auto-return and yield null.
        /// </summary>
        /// <param name="key">Catalog key, e.g. "Fireball_Projectile" / "Collector_Full".</param>
        /// <param name="position">World-space spawn position.</param>
        /// <param name="rotation">World rotation (default = identity).</param>
        /// <param name="parent">Optional transform to parent to (effect moves with it).</param>
        /// <param name="color">Optional HDR tint — applied as ParticleSystem StartColor across
        /// all children when the row is Recolorable.</param>
        /// <param name="scale">Uniform scale override (&gt;0). 0 = row DefaultScale, else 1.</param>
        /// <param name="lifetime">Lifetime override in seconds (&gt;0). 0 = row DefaultLifetime,
        /// else auto-detected from the particle systems. Ignored for loops.</param>
        /// <param name="follow">Optional target the effect keeps its position on each frame
        /// (a small mover, for projectiles/trails on a moving transform).</param>
        public static VFXHandle PlayKey(string key, Vector3 position,
                                        Quaternion rotation = default, Transform parent = null,
                                        Color? color = null, float scale = 0f, float lifetime = 0f,
                                        Transform follow = null)
            => Instance?.PlayKeyInternal(key, position, rotation, parent, color, scale, lifetime, follow);

        // ── Core spawn ──────────────────────────────────────────────────────────

        private VFXHandle PlayKeyInternal(string key, Vector3 position, Quaternion rotation,
                                          Transform parent, Color? color, float scale, float lifetime,
                                          Transform follow)
        {
            if (string.IsNullOrEmpty(key)) return null;

            EnsureHovlCatalog();
            if (_hovlCatalog == null || !_hovlCatalog.TryGet(key, out var row))
            {
                FlowTrace.Throttle("VFXManager", $"hovl-nokey:{key}", 2f,
                    $"PlayKey('{key}'): no HovlVfxCatalog row for this key — nothing spawned. " +
                    "Add a row (Defenders/VFX/Generate Hovl VFX Catalog) or check the key spelling.");
                return null;
            }
            if (row.Prefab == null)
            {
                FlowTrace.Throttle("VFXManager", $"hovl-noprefab:{key}", 2f,
                    $"PlayKey('{key}'): catalog row has a null Prefab (pack not imported?) — nothing spawned.");
                return null;
            }

            // Shared caps with the VFXType path (mobile budget). Loop vs oneshot bucket.
            if (row.IsLoop)
            {
                if (_activeLoops >= _maxActiveLoops)
                {
                    FlowTrace.Throttle("VFXManager", "hovl-loop-cap", 1f,
                        $"PlayKey('{key}') SKIPPED — active loops {_activeLoops}/{_maxActiveLoops} (cap hit).");
                    return null;
                }
            }
            else if (_activeOneshots >= _maxActiveOneshots)
            {
                FlowTrace.Throttle("VFXManager", "hovl-oneshot-cap", 1f,
                    $"PlayKey('{key}') SKIPPED — active oneshots {_activeOneshots}/{_maxActiveOneshots} (cap hit).");
                return null;
            }

            var go = AcquireHovl(key, row);
            if (go == null)
            {
                FlowTrace.Warn("VFXManager", $"PlayKey('{key}'): Acquire returned null — nothing spawned.");
                return null;
            }

            // Position / rotation / scale.
            go.transform.SetParent(null, false);
            go.transform.position = position;
            bool rotValid = !(rotation.x == 0f && rotation.y == 0f && rotation.z == 0f && rotation.w == 0f);
            go.transform.rotation = rotValid ? rotation : Quaternion.identity;

            float s = scale > 0f ? scale
                    : row.DefaultScale > 0f ? row.DefaultScale
                    : 1f;
            go.transform.localScale = Vector3.one * s;

            if (parent != null) go.transform.SetParent(parent, true);
            go.SetActive(true);

            // HDR colour override — recolour the Hovl effect via ParticleSystem StartColor.
            if (color.HasValue && row.Recolorable)
                ApplyStartColor(go, color.Value);

            VerifyHovlHasParticles(go, key);
            PlayAllParticles(go);

            // Follow a moving transform (projectile/trail) without parenting.
            if (follow != null)
            {
                var f = go.GetComponent<HovlVfxFollower>();
                if (f == null) f = go.AddComponent<HovlVfxFollower>();
                f.Begin(follow);
            }

            if (row.IsLoop)
            {
                _activeLoops++;
                _hovlLoopObjects.Add(go);
                return new VFXHandle(go, key);
            }

            _activeOneshots++;
            float life = lifetime > 0f ? lifetime
                       : row.DefaultLifetime > 0f ? row.DefaultLifetime
                       : DetectDuration(go) + 0.3f;
            StartCoroutine(ReturnHovlAfterSeconds(go, key, life));
            // Oneshot auto-returns; a handle is intentionally not surfaced (would risk a
            // double-return if the caller also Stop()'d it).
            return null;
        }

        // ── Pool management (string-keyed; reuses _poolRoot + the helpers) ─────────

        private GameObject AcquireHovl(string key, in HovlVfxCatalog.Row row)
        {
            if (_hovlPools.TryGetValue(key, out var q))
            {
                while (q.Count > 0)
                {
                    var reused = q.Dequeue();
                    if (reused != null)
                    {
                        reused.transform.SetParent(null, false);
                        return reused;
                    }
                    // Drop destroyed entries and keep looking.
                }
            }
            // Pool empty (or all-null) — instantiate a fresh one (pooled after use).
            return CreateHovlInstance(row.Prefab, key);
        }

        private GameObject CreateHovlInstance(GameObject prefab, string key)
        {
            if (prefab == null)
            {
                FlowTrace.Warn("VFXManager",
                    $"CreateHovlInstance('{key}'): null prefab — no pooled instance built.");
                return null;
            }
            var go = Instantiate(prefab, _poolRoot);
            go.name = $"[Hovl_{key}]";
            // NOTE: no URP-proof pass here — Hovl packs ship URP-clean HS_* shader graphs
            // (Docs/VFX/HovlStudio_Inventory.md §2.2 GREEN, no magenta). The VFXType path's
            // ProofUrpParticleShaders is only for the legacy-built Lana/Spells prefabs.
            _hovlKeyOf[go] = key;
            go.SetActive(false);
            return go;
        }

        /// <summary>
        /// Return a Hovl-keyed instance to its pool (called by the lifetime coroutine or
        /// via VFXHandle.Stop). Stops particles, resets scale, decrements the right cap
        /// bucket, and re-parents under the pool root. Public so VFXHandle can call it.
        /// </summary>
        public void ReturnHovlToPool(GameObject go, string key)
        {
            if (go == null) return;

            StopAllParticles(go);

            var follower = go.GetComponent<HovlVfxFollower>();
            if (follower != null) follower.EndFollow();

            // Reparent the pooled loop back under the pool root so it tracks nothing while dormant.
            // Unity LOGS AN ERROR (it does NOT throw) from Transform.SetParent when the current parent
            // is mid-(de)activation — e.g. an Arcane Spire being deactivated by a tier reskin, or a
            // scene unload. Because it never throws, the old Guard.Try wrapper caught nothing and the
            // LogError still reached the F8 recorder: proven by data — the guard was in place at
            // 06:03 yet the error fired 55 min later at 06:58, across 22 captures (owner F8 2026-07-17).
            //
            // The tell for "parent is mid-(de)activation" is exactly `!activeInHierarchy` — during the
            // deactivation propagation the child reads inactive-in-hierarchy while OnDisable runs. So we
            // reparent ONLY when the object is still active (the normal loop-stop, where returning to the
            // pool root keeps things tidy) and DEACTIVATE IN PLACE otherwise. This never issues the
            // illegal call, so nothing to log. AcquireHovl re-seats the parent on next use regardless
            // (SetParent(null) then SetParent(newParent), lines ~242/252) and tolerates the object being
            // destroyed with its tower (drops null entries, instantiates fresh) — so leaving a dormant
            // loop parented under a torn-down tower is harmless.
            if (go.activeInHierarchy && go.transform.parent != _poolRoot)
                go.transform.SetParent(_poolRoot, false);
            go.transform.localScale = Vector3.one;   // clear any scale override for reuse
            go.SetActive(false);

            if (!_hovlPools.ContainsKey(key))
                _hovlPools[key] = new Queue<GameObject>();
            _hovlPools[key].Enqueue(go);
            _hovlKeyOf[go] = key;

            bool wasLoop = _hovlLoopObjects.Remove(go);
            if (wasLoop) { if (_activeLoops > 0) _activeLoops--; }
            else         { if (_activeOneshots > 0) _activeOneshots--; }
        }

        /// <summary>Defer a Hovl pool return by <paramref name="delay"/> seconds (graceful loop stop).</summary>
        public void ReturnHovlAfterDelay(GameObject go, string key, float delay)
        {
            if (go == null) return;
            StartCoroutine(ReturnHovlAfterSeconds(go, key, delay));
        }

        private IEnumerator ReturnHovlAfterSeconds(GameObject go, string key, float delay)
        {
            yield return new WaitForSeconds(delay);
            ReturnHovlToPool(go, key);
        }

        // ── Overrides / helpers ───────────────────────────────────────────────────

        /// <summary>
        /// Recolour a Hovl effect toward <paramref name="color"/> the way the VENDOR's own
        /// recolor tool does (HS_CameraHolder.Counter/OnGUI, docs/HOVL_STUDIO_SME.md sec 4d):
        /// cache each particle system's AUTHORED startColor as HSV on first acquire, then
        /// shift only the HUE to the target hue while PRESERVING its cached saturation / value
        /// (brightness) / alpha - so the bright-core / soft-halo layering (and the HDR luminance
        /// bloom feeds on) survives. The old flat MinMaxGradient(color) flood-fill stamped every
        /// layer one identical color, flattening the authored art ("not like the demo"). Near-
        /// white hot cores (saturation &lt; 0.05) are left untouched - hue means nothing on them
        /// and pushing the tint in is exactly the flood-fill artifact. Idempotent under pooling:
        /// the S/V/A always come from the cached ORIGINAL, never from an already-shifted value,
        /// so repeated reuses (and later hue changes) never drift the authored brightness.
        /// Gradient startColor modes are hue-rotated key-by-key (never thrown on).
        /// </summary>
        private void ApplyStartColor(GameObject go, Color color)
        {
            Color.RGBToHSV(color, out float targetHue, out _, out _);
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (ps == null) continue;
                var main = ps.main;
                // Cache the AUTHORED startColor the first time this PS is recolored, so every
                // pooled reuse hue-shifts from the original S/V/A rather than a prior result.
                if (!_hovlAuthoredStartColor.TryGetValue(ps, out var authored))
                {
                    authored = main.startColor;
                    _hovlAuthoredStartColor[ps] = authored;
                }
                main.startColor = ShiftHue(authored, targetHue);
            }
        }

        /// <summary>Return a hue-shifted copy of a ParticleSystem startColor
        /// <see cref="ParticleSystem.MinMaxGradient"/>, preserving each source colour's authored
        /// saturation / value / alpha across every gradient mode (Color / TwoColors / Gradient /
        /// TwoGradients). Never mutates <paramref name="sc"/>; safe on the cached authored value.</summary>
        private static ParticleSystem.MinMaxGradient ShiftHue(ParticleSystem.MinMaxGradient sc, float targetHue)
        {
            switch (sc.mode)
            {
                case ParticleSystemGradientMode.Color:
                    return new ParticleSystem.MinMaxGradient(ShiftHue(sc.color, targetHue));
                case ParticleSystemGradientMode.TwoColors:
                    return new ParticleSystem.MinMaxGradient(ShiftHue(sc.colorMin, targetHue),
                                                             ShiftHue(sc.colorMax, targetHue));
                case ParticleSystemGradientMode.Gradient:
                    return new ParticleSystem.MinMaxGradient(ShiftHue(sc.gradient, targetHue));
                case ParticleSystemGradientMode.TwoGradients:
                    return new ParticleSystem.MinMaxGradient(ShiftHue(sc.gradientMin, targetHue),
                                                             ShiftHue(sc.gradientMax, targetHue));
                default:
                    return sc;
            }
        }

        /// <summary>Move <paramref name="src"/>'s hue to <paramref name="targetHue"/>,
        /// keeping its authored saturation/value (HDR-safe) and alpha.</summary>
        private static Color ShiftHue(Color src, float targetHue)
        {
            Color.RGBToHSV(src, out _, out float s, out float v);
            if (s < 0.05f) return src;   // white-hot core layer — leave it white
            var c = Color.HSVToRGB(targetHue, s, v, hdr: true);
            c.a = src.a;
            return c;
        }

        private static Gradient ShiftHue(Gradient g, float targetHue)
        {
            if (g == null) return null;
            var keys = g.colorKeys;
            for (int i = 0; i < keys.Length; i++)
                keys[i].color = ShiftHue(keys[i].color, targetHue);
            var ng = new Gradient { mode = g.mode };
            ng.SetKeys(keys, g.alphaKeys);
            return ng;
        }

        // A Hovl VFX object must carry at least one ParticleSystem (or visible Renderer) to be
        // seen. Traced Once per key so a bad catalog prefab self-reports in the break-log.
        private static void VerifyHovlHasParticles(GameObject go, string key)
        {
            if (go == null) return;
            int particles = go.GetComponentsInChildren<ParticleSystem>(true).Length;
            int renderers = go.GetComponentsInChildren<Renderer>(true).Length;
            if (particles == 0 && renderers == 0)
                FlowTrace.Once("VFXManager", $"hovl-novisual:{key}",
                    $"PlayKey('{key}'): prefab has NO ParticleSystem and NO Renderer — plays but " +
                    "renders nothing (invisible VFX). Check the catalog prefab for this key.");
        }
    }
}
