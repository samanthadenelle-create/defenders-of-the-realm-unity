// =============================================================================
// PoiCalloutSystem (WO-VFX-POI) — the singleton driver that renders POI callouts.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// THE FEATURE (colorblind-safe wayfinding — owner is red/green colorblind, so callouts
// read by MOTION / SHAPE / LUMINANCE / VERTICALITY, never hue):
//   * NEAR-FIELD (PoiTier.Node): every un-spent node within CalloutRadius keeps a small
//     looping ground aura ("Poi_NodeAura") that stops once the hero is basically on it
//     (within HandoffRadius — the interact prompt takes over), spent, or out of range.
//     CAPPED to the nearest ~6 auras so we respect VFXManager's shared loop budget.
//   * FAR-FIELD (PoiTier.Landmark): an enemy fortress etc. holds a TALL looping pillar/
//     beam ("Poi_Landmark") whenever it exists and is un-spent (NOT discovery-gated — it
//     is a landmark you can see from range), scaled/faded DOWN as the hero closes within
//     HandoffRadius so it never blocks the arrival view.
//
// STRUCTURE mirrors NodeDiscoverySystem: [RuntimeInitializeOnLoadMethod(AfterSceneLoad)]
// self-bootstrap, DDOL singleton, hero via FindWithTag("Player"), per-frame poll of
// PoiRegistry. NULL-SAFE + WebGL-SAFE: PlayKey no-ops until the "Poi_*" catalog keys
// exist, and the bootstrap path is wrapped in try/catch. Gated by FeatureFlags.PoiCallouts.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core;

namespace DeNelle.Village
{
    /// <summary>Drives near-field node auras + far-field landmark pillars from the
    /// <see cref="PoiRegistry"/>. Self-bootstrapping; owns the VFX handles, never the POIs.</summary>
    public sealed class PoiCalloutSystem : MonoBehaviour
    {
        public static PoiCalloutSystem Instance { get; private set; }

        // ── Tunables (code-only) ─────────────────────────────────────────────
        [Tooltip("Max simultaneous near-field NODE auras (respects VFXManager's shared loop budget).")]
        public int MaxNodeAuras = 6;

        [Tooltip("Seconds between callout re-evaluations. Callouts are ambient — no need for every frame.")]
        public float TickInterval = 0.35f;

        // Catalog keys — must match the rows HovlVfxCatalogGenerator authors.
        // Owner 2026-07-24: harvest NODES get a SUBTLE, DISTINCT aura (drifting motes, no ring) —
        // TreeofLifeAura_Aura (ParticlePack FireFlies) instead of the old "Poi_NodeAura" magic-circle
        // (which shared the "Magic circle sun loop" prefab with the Arcane Spire + Cathedral). Reads by
        // MOTION/LUMINANCE, colorblind-safe. SWAPPABLE default — the owner may retag in the VFX Caster.
        private const string NodeAuraKey = "TreeofLifeAura_Aura";
        private const string LandmarkKey = "Poi_Landmark";

        // ── Live callout handles (one per beacon currently showing) ──────────
        private readonly Dictionary<PoiBeacon, VFXHandle> _live =
            new Dictionary<PoiBeacon, VFXHandle>();

        // Scratch reused each tick (no per-frame alloc churn).
        private readonly List<PoiBeacon> _nodeCandidates = new List<PoiBeacon>(32);
        private readonly List<PoiBeacon> _toStop = new List<PoiBeacon>(16);

        private Transform _hero;
        private float _heroFindTimer;
        private float _tickTimer;
        private const float HeroFindInterval = 0.5f;

        // =====================================================================
        // Self-bootstrap (no scene edit). Runs after every scene load; idempotent.
        // WebGL-safe: any failure degrades to "no callouts", never throws into the loader.
        // =====================================================================
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            try
            {
                if (Instance != null) return;
                if (!FeatureFlags.PoiCallouts) return;
                var go = new GameObject("PoiCalloutSystem");
                go.AddComponent<PoiCalloutSystem>();
                Object.DontDestroyOnLoad(go);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[PoiCallout] bootstrap skipped: " + ex.Message);
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            StopAll();
            Instance = null;
        }

        private void Update()
        {
            _tickTimer -= Time.deltaTime;
            if (_tickTimer > 0f) return;
            _tickTimer = Mathf.Max(0.1f, TickInterval);

            EnsureHero();
            Tick();
        }

        // =====================================================================
        // Per-tick evaluation: decide which beacons should be showing a callout,
        // start the missing ones, stop the stale ones, update landmark scale.
        // =====================================================================
        private void Tick()
        {
            // F8 2026-07-11 "white rod in arena / carry over from the castle": the arena is
            // staged 7km away IN THE SAME SCENE, and the Landmark pillar has an infinite
            // callout radius — an uncleared outpost's 4x beacon was visible from inside the
            // battle. No open-world POI callout may render while a battle owns the screen.
            if (DeNelle.Village.Arena.BattleArena.AnyBattleInProgress)
            {
                StopAll();
                return;
            }

            bool heroValid = _hero != null;
            Vector3 heroPos = heroValid ? _hero.position : Vector3.zero;

            _nodeCandidates.Clear();
            _toStop.Clear();

            // First pass over the live registry: landmarks handled immediately,
            // node candidates gathered for the distance-capped second pass.
            var beacons = PoiRegistry.All;
            if (beacons != null)
            {
                foreach (var b in beacons)
                {
                    if (b == null) continue;

                    if (b.Tier == PoiBeacon.PoiTier.Landmark)
                    {
                        EvaluateLandmark(b);
                        continue;
                    }

                    // Node tier — must be alive, un-spent, within callout ring and
                    // NOT yet inside the handoff ring (the interact prompt owns close range).
                    if (!b.IsActiveCallout) { MarkStop(b); continue; }
                    if (!heroValid)         { MarkStop(b); continue; }

                    float sqr = (b.transform.position - heroPos).sqrMagnitude;
                    float outer = Mathf.Max(b.CalloutRadius, b.HandoffRadius);
                    float inner = b.HandoffRadius;
                    if (sqr > outer * outer || sqr <= inner * inner) { MarkStop(b); continue; }

                    _nodeCandidates.Add(b);
                }
            }

            // Node budget: keep only the nearest MaxNodeAuras auras live (shared loop budget).
            if (_nodeCandidates.Count > 0 && heroValid)
            {
                if (_nodeCandidates.Count > MaxNodeAuras)
                {
                    _nodeCandidates.Sort((a, c) =>
                        (a.transform.position - heroPos).sqrMagnitude
                            .CompareTo((c.transform.position - heroPos).sqrMagnitude));
                }

                int budget = Mathf.Max(0, MaxNodeAuras);
                for (int i = 0; i < _nodeCandidates.Count; i++)
                {
                    if (i < budget) EnsureNodeAura(_nodeCandidates[i]);
                    else            MarkStop(_nodeCandidates[i]);   // over budget this tick
                }
            }

            // Stop everything flagged (out of range / spent / over budget / dead).
            for (int i = 0; i < _toStop.Count; i++)
                StopCallout(_toStop[i]);

            // Prune handles whose beacon was destroyed without an OnDisable stop.
            PruneDeadHandles();
        }

        // A far-field landmark pillar: show whenever it exists + is un-spent (not discovery-
        // gated). Scale/fade DOWN as the hero closes within HandoffRadius so arrival is clean.
        private void EvaluateLandmark(PoiBeacon b)
        {
            if (!b.IsActiveCallout) { MarkStop(b); return; }

            // Presence is the load-bearing behavior: keep the pillar alive whenever the
            // landmark exists + is un-spent (visible from range — NOT discovery-gated). A
            // handoff-distance scale-down is a future nicety; VFXHandle exposes no scale hook,
            // so arrival cleanliness is left to the prefab's own falloff.
            EnsureLandmark(b);
        }

        // =====================================================================
        // Handle lifecycle — one loop VFX per showing beacon, followed to its transform.
        // =====================================================================
        private void EnsureNodeAura(PoiBeacon b)
        {
            if (b == null) return;
            if (_live.TryGetValue(b, out var h) && h != null && h.IsAlive) return;

            var handle = VFXManager.PlayKey(NodeAuraKey, b.transform.position,
                Quaternion.identity, parent: null, color: b.Tint, scale: 0f, lifetime: 0f,
                follow: b.transform);
            if (handle != null) _live[b] = handle;   // null == key not authored yet / budget hit
        }

        private void EnsureLandmark(PoiBeacon b)
        {
            if (b == null) return;
            if (_live.TryGetValue(b, out var h) && h != null && h.IsAlive) return;

            var handle = VFXManager.PlayKey(LandmarkKey, b.transform.position,
                Quaternion.identity, parent: null, color: b.Tint, scale: 0f, lifetime: 0f,
                follow: b.transform);
            if (handle != null) _live[b] = handle;
        }

        private void MarkStop(PoiBeacon b)
        {
            if (b != null && _live.ContainsKey(b)) _toStop.Add(b);
        }

        private void StopCallout(PoiBeacon b)
        {
            if (b == null) return;
            if (_live.TryGetValue(b, out var h))
            {
                h?.Stop();
                _live.Remove(b);
            }
        }

        private void StopAll()
        {
            foreach (var kv in _live)
                kv.Value?.Stop(true);
            _live.Clear();
        }

        // Drop handles whose beacon key went null (destroyed POI) so the dict never leaks.
        private void PruneDeadHandles()
        {
            _toStop.Clear();
            foreach (var kv in _live)
                if (kv.Key == null) _toStop.Add(kv.Key);
            for (int i = 0; i < _toStop.Count; i++)
            {
                if (_live.TryGetValue(_toStop[i], out var h)) h?.Stop(true);
                _live.Remove(_toStop[i]);
            }
            _toStop.Clear();
        }

        // =====================================================================
        // Helpers.
        // =====================================================================
        private void EnsureHero()
        {
            if (_hero != null) return;
            _heroFindTimer -= Time.deltaTime;
            if (_heroFindTimer > 0f) return;
            _heroFindTimer = HeroFindInterval;
            var p = SafeFindWithTag("Player");
            _hero = p != null ? p.transform : null;
        }

        /// <summary>Undefined-tag-safe FindWithTag (Unity throws on an undefined tag).</summary>
        private static GameObject SafeFindWithTag(string tag)
        {
            try { return GameObject.FindWithTag(tag); }
            catch (UnityEngine.UnityException) { return null; }
        }
    }
}
