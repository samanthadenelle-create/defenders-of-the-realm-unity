// =============================================================================
// AbilityVfxPool — pooled hosts/particle-units/lights for the PROCEDURAL VFX
// paths (owner directive 2026-07-02: "VFX must use POOLING — no Instantiate/
// Destroy churn per cast/impact").
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WHY: the prefab-based paths are already pooled (VFXManager per-type pools,
// MoverProjectilePool, ImpactFXPool). The remaining churn was the PROCEDURAL
// kit: AbilityVfxKit.SpawnAbilityVfx* created a fresh host GameObject + 1-4
// ParticleSystem children + a Light per cast and Object.Destroy'd them 2.6s
// later — a GC spike per swing/cast (WebGL frame hitch). This pool rents those
// pieces instead, mirroring the existing pool idiom (self-bootstrapped DDOL
// singleton with a pool root, like VFXManager/MoverProjectilePool — not a new
// idiom).
//
// SHAPE (per the directive):
//   • Pre-warms a small count on load (hosts+units+lights).
//   • Rent/return with AUTO-RETURN via ReturnHostAfter (particle completion
//     window — the kit's effects are all <= ~2.6s).
//   • HARD CAP: overflow = force-return (reuse) the OLDEST live host; never
//     unbounded growth.
//   • Census: a FlowTrace.Throttle'd budget line every ~5s —
//       "[Flow:VFX] live systems=N particles~M | pool hosts=... units=... lights=..."
//     so the fleet captures particle-budget regressions (mobile lens: <64
//     particles typical, <200 burst peak per effect).
//
// A unit = a GameObject with a ParticleSystem + ParticleSystemRenderer whose
// MATERIAL is applied once at build (AbilityVfxKit.ApplyParticleMaterial — the
// URP-proofed soft dot). ResetUnit() restores every module the kit mutates so a
// rented unit never inherits the previous effect's state (trails/stretch/
// velocity/gravity are the dangerous carriers).
//
// Village → Core only (FlowTrace). No reflection. ASCII only.
// =============================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>Pool for the procedural ability-VFX pieces (hosts, particle units,
    /// flash lights). Rent via <see cref="RentHost"/>/<see cref="RentUnit"/>/
    /// <see cref="RentLight"/>; hosts auto-return everything parented under them.</summary>
    [DisallowMultipleComponent]
    public sealed class AbilityVfxPool : MonoBehaviour
    {
        public static AbilityVfxPool Instance { get; private set; }

        // ── Budget knobs (mobile lens) ────────────────────────────────────────
        private const int PrewarmHosts  = 4;
        private const int PrewarmUnits  = 12;
        private const int PrewarmLights = 4;
        private const int MaxLiveHosts  = 16;   // overflow => steal the oldest live host
        private const float CensusPeriod = 5f;  // seconds between budget census lines

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            new GameObject("[AbilityVfxPool]").AddComponent<AbilityVfxPool>();
        }

        private Transform _root;
        private readonly Queue<GameObject> _hosts = new Queue<GameObject>();
        private readonly Queue<ParticleSystem> _units = new Queue<ParticleSystem>();
        private readonly Queue<Light> _lights = new Queue<Light>();

        // Live hosts in rent order (oldest first) so overflow can reuse the oldest.
        private readonly List<GameObject> _liveHosts = new List<GameObject>();

        private float _nextCensus;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            var rootGo = new GameObject("[AbilityVfxPool_Dormant]");
            rootGo.transform.SetParent(transform, false);
            _root = rootGo.transform;

            for (int i = 0; i < PrewarmHosts; i++)  _hosts.Enqueue(BuildHost());
            for (int i = 0; i < PrewarmUnits; i++)  _units.Enqueue(BuildUnit());
            for (int i = 0; i < PrewarmLights; i++) _lights.Enqueue(BuildLight());

            FlowTrace.Step("VFX",
                $"AbilityVfxPool warmed: hosts={PrewarmHosts} units={PrewarmUnits} lights={PrewarmLights} " +
                $"(cap {MaxLiveHosts} live hosts; overflow reuses oldest).");
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── Rent / return ─────────────────────────────────────────────────────

        /// <summary>Rent an empty host GameObject at <paramref name="position"/>. At the
        /// hard cap the OLDEST live host is force-returned and reused (never unbounded).</summary>
        public GameObject RentHost(string name, Vector3 position)
        {
            GameObject host;
            if (_hosts.Count > 0)
            {
                host = _hosts.Dequeue();
            }
            else if (_liveHosts.Count >= MaxLiveHosts && _liveHosts.Count > 0)
            {
                // Overflow: reclaim the oldest live effect (it is near its end anyway).
                host = _liveHosts[0];
                ReturnHost(host);           // strips its children back into the queues
                _hosts.Dequeue();           // ReturnHost enqueued it — take it right back
                FlowTrace.Throttle("VFX", "pool-host-steal", 1f,
                    $"AbilityVfxPool at cap ({MaxLiveHosts}) — reused the oldest live host for '{name}'.");
            }
            else
            {
                host = BuildHost();
            }

            host.name = name;
            host.transform.SetParent(null, false);
            host.transform.position = position;
            host.transform.rotation = Quaternion.identity;
            host.SetActive(true);
            _liveHosts.Add(host);
            return host;
        }

        /// <summary>Rent a reset particle unit, parented under <paramref name="host"/> at
        /// <paramref name="worldPos"/>. The unit's modules are back at kit defaults.</summary>
        public ParticleSystem RentUnit(GameObject host, string name, Vector3 worldPos)
        {
            ParticleSystem ps = _units.Count > 0 ? _units.Dequeue() : BuildUnit();
            var go = ps.gameObject;
            go.name = name;
            go.transform.SetParent(host != null ? host.transform : null, false);
            go.transform.position = worldPos;
            go.transform.rotation = Quaternion.identity;
            go.SetActive(true);
            ResetUnit(ps);
            return ps;
        }

        /// <summary>Rent a point light parented under <paramref name="host"/>.</summary>
        public Light RentLight(GameObject host, Vector3 worldPos)
        {
            Light l = _lights.Count > 0 ? _lights.Dequeue() : BuildLight();
            var go = l.gameObject;
            go.transform.SetParent(host != null ? host.transform : null, false);
            go.transform.position = worldPos;
            go.SetActive(true);
            l.enabled = true;
            return l;
        }

        /// <summary>Auto-return: give the effect its on-screen lifetime, then reclaim the
        /// host and every pooled child (units + lights) in one sweep.</summary>
        public void ReturnHostAfter(GameObject host, float seconds)
        {
            if (host == null) return;
            StartCoroutine(ReturnRoutine(host, seconds));
        }

        private IEnumerator ReturnRoutine(GameObject host, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (host != null && _liveHosts.Contains(host)) ReturnHost(host);
        }

        private void ReturnHost(GameObject host)
        {
            _liveHosts.Remove(host);

            // Reclaim pooled children back into their queues (reparent to the dormant root).
            var units = host.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in units)
            {
                if (ps == null) continue;
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.gameObject.SetActive(false);
                ps.transform.SetParent(_root, false);
                _units.Enqueue(ps);
            }
            var lights = host.GetComponentsInChildren<Light>(true);
            foreach (var l in lights)
            {
                if (l == null) continue;
                l.enabled = false;
                l.gameObject.SetActive(false);
                l.transform.SetParent(_root, false);
                _lights.Enqueue(l);
            }
            // Any non-pooled stragglers a builder parented in (none today) die with a warn.
            for (int i = host.transform.childCount - 1; i >= 0; i--)
            {
                var stray = host.transform.GetChild(i).gameObject;
                FlowTrace.Warn("VFX", $"AbilityVfxPool: non-pooled child '{stray.name}' under returned host — destroyed.");
                Destroy(stray);
            }

            host.SetActive(false);
            host.transform.SetParent(_root, false);
            _hosts.Enqueue(host);
        }

        // ── Builders ──────────────────────────────────────────────────────────

        private GameObject BuildHost()
        {
            var go = new GameObject("[VfxHost]");
            go.transform.SetParent(_root, false);
            go.SetActive(false);
            return go;
        }

        private ParticleSystem BuildUnit()
        {
            var go = new GameObject("[VfxUnit]");
            go.transform.SetParent(_root, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var r = go.GetComponent<ParticleSystemRenderer>();
            if (r != null)
            {
                // Material applied ONCE per unit (URP-proofed soft dot) — rents never
                // instantiate a material (no per-cast material churn).
                AbilityVfxKit.ApplyParticleMaterial(r, AbilityVfxKit.SoftDotTexture);
                r.renderMode = ParticleSystemRenderMode.Billboard;
            }
            go.SetActive(false);
            return ps;
        }

        private Light BuildLight()
        {
            var go = new GameObject("[VfxLight]");
            go.transform.SetParent(_root, false);
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.shadows = LightShadows.None;
            l.enabled = false;
            go.SetActive(false);
            return l;
        }

        // ── Unit reset — restore EVERY module the kit mutates ─────────────────

        /// <summary>Restore a unit to the kit's NewPS defaults. Must cover every module
        /// any builder touches (trails, stretch renderer, velocity, gravity are the
        /// dangerous carriers between effects).</summary>
        private static void ResetUnit(ParticleSystem ps)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop            = false;
            main.playOnAwake     = false;
            main.duration        = 0.6f;
            main.startDelay      = 0f;
            main.startLifetime   = 0.5f;
            main.startSpeed      = 1f;
            main.startSize       = 0.15f;
            main.startColor      = Color.white;
            main.gravityModifier = 0f;
            main.maxParticles    = 200;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction      = ParticleSystemStopAction.None;

            var em = ps.emission;
            em.enabled = true;
            em.rateOverTime = 0f;
            em.SetBursts(System.Array.Empty<ParticleSystem.Burst>());

            var sh = ps.shape;
            sh.enabled = false;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = 25f;
            sh.radius = 1f;
            sh.radiusThickness = 1f;
            sh.rotation = Vector3.zero;
            sh.length = 5f;

            var col = ps.colorOverLifetime; col.enabled = false;
            var sol = ps.sizeOverLifetime;  sol.enabled = false;
            var vel = ps.velocityOverLifetime; vel.enabled = false;
            var tr  = ps.trails; tr.enabled = false;

            var r = ps.GetComponent<ParticleSystemRenderer>();
            if (r != null)
            {
                r.renderMode = ParticleSystemRenderMode.Billboard;
                r.velocityScale = 0f;
                r.lengthScale = 2f;
                r.trailMaterial = null;
            }
        }

        // ── Census — the fleet-captured VFX budget line ───────────────────────

        private void Update()
        {
            if (!FlowTrace.Enabled) return;
            if (Time.unscaledTime < _nextCensus) return;
            _nextCensus = Time.unscaledTime + CensusPeriod;

            // Global live-particle census (0.2 Hz — cheap enough; only while tracing).
            int systems = 0, particles = 0;
            var all = FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None);
            foreach (var ps in all)
            {
                if (ps == null || !ps.isPlaying) continue;
                systems++;
                particles += ps.particleCount;
            }

            FlowTrace.Throttle("VFX", "census", CensusPeriod - 0.5f,
                $"live systems={systems} particles~{particles} | pool hosts free={_hosts.Count} " +
                $"inUse={_liveHosts.Count}/{MaxLiveHosts} units free={_units.Count} lights free={_lights.Count}");
        }
    }
}
