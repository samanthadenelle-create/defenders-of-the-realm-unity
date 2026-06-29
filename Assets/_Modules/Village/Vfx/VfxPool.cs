// =============================================================================
// VfxPool — pooled combat VFX (HitImpact / DeathBurst / Telegraph). DEF-52.
// -----------------------------------------------------------------------------
// Mobile-safe: all effects are code-built (no prefabs, no Instantiate per hit)
// and return themselves to the pool when their duration expires. GC-allocation
// is eliminated in steady-state; the pool expands on demand if drained.
//
// EFFECT TYPES
//   HitImpact   — small pop at the enemy position when struck (0.45 s).
//   DeathBurst  — larger burst when an enemy is killed (0.65 s).
//   Telegraph   — a ring on the ground at a spawn point; held until
//                 VfxPool.ReturnTelegraph() is called by the spawner.
//
// BOOTSTRAP: RuntimeInitializeOnLoadMethod — no scene wiring needed, mirrors
// ProjectilePool. All VFX GameObjects are parented to this DontDestroyOnLoad
// root so they survive scene loads.
//
// USAGE
//   VfxPool.SpawnHitImpact(worldPosition);
//   VfxPool.SpawnDeathBurst(worldPosition);
//   var tel = VfxPool.GetTelegraph(spawnPoint);
//   // ... later when the wave actually spawns:
//   VfxPool.ReturnTelegraph(tel);
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>
    /// Persistent object pool for the three combat VFX types (HitImpact,
    /// DeathBurst, Telegraph). Self-bootstraps on load; call the static
    /// spawn helpers from anywhere.
    /// </summary>
    public sealed class VfxPool : MonoBehaviour
    {
        // ── Singleton ────────────────────────────────────────────────────────

        public static VfxPool Instance { get; private set; }

        // ── Pool sizes ───────────────────────────────────────────────────────

        [SerializeField] private int _hitPoolSize       = 30;
        [SerializeField] private int _deathPoolSize     = 20;
        [SerializeField] private int _telegraphPoolSize = 12;

        // ── Internal pools ───────────────────────────────────────────────────

        private readonly Queue<PooledVfx> _hitPool       = new Queue<PooledVfx>();
        private readonly Queue<PooledVfx> _deathPool     = new Queue<PooledVfx>();
        private readonly Queue<PooledVfx> _telegraphPool  = new Queue<PooledVfx>();

        // ── Shared emissive-material cache (security audit E-VFXMAT) ───────────
        // ApplyEmissiveMaterial used to `new Material(sh)` for EVERY renderer at
        // bootstrap (~62 instances), all under DontDestroyOnLoad and never destroyed
        // → a permanent, growing leak. The colour/emissive combos are a tiny fixed
        // set, so cache one shared Material per (shader, colour, emissive) key and
        // reuse it across every renderer. Same visual output (sharedMaterial), no leak.
        private static readonly Dictionary<string, Material> s_emissiveMatCache =
            new Dictionary<string, Material>();

        // =====================================================================
        //  Bootstrap
        // =====================================================================

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            new GameObject("VfxPool").AddComponent<VfxPool>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            for (int i = 0; i < _hitPoolSize;       i++) _hitPool.Enqueue(CreateHit());
            for (int i = 0; i < _deathPoolSize;     i++) _deathPool.Enqueue(CreateDeath());
            for (int i = 0; i < _telegraphPoolSize; i++) _telegraphPool.Enqueue(CreateTelegraph());
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        // =====================================================================
        //  Public API
        // =====================================================================

        /// <summary>
        /// Plays a short hit-impact pop at <paramref name="position"/>.
        /// Returns immediately after scheduling the effect.
        /// </summary>
        public static void SpawnHitImpact(Vector3 position)
        {
            // T+U: a null Instance means combat hits land with NO impact pop — self-report
            // (throttled, this is a hot per-hit path) instead of a silent no-op.
            if (Instance == null)
            {
                FlowTrace.Throttle("VfxPool", "hit-noinstance", 1f,
                    "SpawnHitImpact: no VfxPool.Instance — hit impacts are silently absent.");
                return;
            }
            var vfx = Instance.GetFromPool(Instance._hitPool, Instance.CreateHit);
            if (vfx == null)
            {
                FlowTrace.Throttle("VfxPool", "hit-novfx", 1f,
                    "SpawnHitImpact: pool returned null — no impact shown.");
                return;
            }
            vfx.Play(position, duration: 0.45f, Instance._hitPool);
        }

        /// <summary>
        /// Plays a larger death burst at <paramref name="position"/>.
        /// </summary>
        public static void SpawnDeathBurst(Vector3 position)
        {
            if (Instance == null)
            {
                FlowTrace.Throttle("VfxPool", "death-noinstance", 1f,
                    "SpawnDeathBurst: no VfxPool.Instance — death bursts are silently absent.");
                return;
            }
            var vfx = Instance.GetFromPool(Instance._deathPool, Instance.CreateDeath);
            if (vfx == null)
            {
                FlowTrace.Throttle("VfxPool", "death-novfx", 1f,
                    "SpawnDeathBurst: pool returned null — no burst shown.");
                return;
            }
            vfx.Play(position, duration: 0.65f, Instance._deathPool);
        }

        /// <summary>
        /// Reserves a telegraph ring at <paramref name="position"/> and returns it.
        /// Call <see cref="ReturnTelegraph"/> when the telegraph should disappear.
        /// </summary>
        public static PooledVfx GetTelegraph(Vector3 position)
        {
            if (Instance == null)
            {
                FlowTrace.Throttle("VfxPool", "tel-noinstance", 1f,
                    "GetTelegraph: no VfxPool.Instance — spawn telegraphs are silently absent.");
                return null;
            }
            var vfx = Instance.GetFromPool(Instance._telegraphPool, Instance.CreateTelegraph);
            if (vfx == null)
            {
                FlowTrace.Throttle("VfxPool", "tel-novfx", 1f,
                    "GetTelegraph: pool returned null — no telegraph shown.");
                return null;
            }
            vfx.PlayHeld(position);
            return vfx;
        }

        /// <summary>
        /// Hides and returns a telegraph obtained via <see cref="GetTelegraph"/>.
        /// Safe to call with null.
        /// </summary>
        public static void ReturnTelegraph(PooledVfx tel)
        {
            if (tel == null || Instance == null) return;
            tel.ReturnToPool(Instance._telegraphPool);
        }

        // =====================================================================
        //  Pool helpers
        // =====================================================================

        private PooledVfx GetFromPool(Queue<PooledVfx> pool, System.Func<PooledVfx> factory)
        {
            // Drain any destroyed/null entries first.
            while (pool.Count > 0 && pool.Peek() == null) pool.Dequeue();

            if (pool.Count > 0)
            {
                var entry = pool.Dequeue();
                if (entry != null) return entry;
            }
            // Pool drained — expand. A factory throw must not blank the effect silently.
            return FlowTrace.Try<PooledVfx>("VfxPool", "expand pool (factory)", factory, null);
        }

        // V: confirm a freshly built effect actually carries a visible Renderer with a mesh —
        // a code-built primitive that lost its MeshFilter/MeshRenderer (or material) would play
        // but render nothing. Traced once per kind so a broken build self-reports, not goes quiet.
        private static PooledVfx VerifyBuilt(PooledVfx vfx, string kind)
        {
            if (vfx == null)
            {
                FlowTrace.Once("VfxPool", $"built-null:{kind}",
                    $"Create{kind}: factory returned null PooledVfx — effect will never show.");
                return null;
            }
            int rends = 0, withMesh = 0;
            foreach (var r in vfx.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                rends++;
                var mf = r.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null) withMesh++;
            }
            if (rends == 0 || withMesh == 0)
                FlowTrace.Once("VfxPool", $"built-norender:{kind}",
                    $"Create{kind}: built effect has renderers={rends} withMesh={withMesh} — " +
                    "will render nothing (invisible VFX).");
            return vfx;
        }

        // =====================================================================
        //  Factory — build code visuals
        //  All GameObjects are parented to this root (DontDestroyOnLoad) so they
        //  survive scene transitions.
        // =====================================================================

        /// <summary>
        /// HitImpact — a bright yellow-white sphere that scales up then fades.
        /// Small, snappy — reads as an impact flash.
        /// </summary>
        private PooledVfx CreateHit()
        {
            var go = new GameObject("VfxHit");
            go.SetActive(false);
            go.transform.SetParent(transform, false);

            var inner = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            inner.name = "HitSphere";
            DestroyImmediate(inner.GetComponent<Collider>());
            inner.transform.SetParent(go.transform, false);
            inner.transform.localScale = Vector3.one * 0.18f;
            ApplyEmissiveMaterial(inner.GetComponent<Renderer>(),
                new Color(1f, 0.95f, 0.40f), emissive: 3.5f);   // amber-white flash

            return VerifyBuilt(go.AddComponent<PooledVfx>().Init(maxScale: 0.55f, kind: VfxKind.Hit), "Hit");
        }

        /// <summary>
        /// DeathBurst — a violet-red sphere that bursts outward and fades.
        /// Slightly larger and slower than HitImpact.
        /// </summary>
        private PooledVfx CreateDeath()
        {
            var go = new GameObject("VfxDeath");
            go.SetActive(false);
            go.transform.SetParent(transform, false);

            var inner = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            inner.name = "DeathSphere";
            DestroyImmediate(inner.GetComponent<Collider>());
            inner.transform.SetParent(go.transform, false);
            inner.transform.localScale = Vector3.one * 0.22f;
            ApplyEmissiveMaterial(inner.GetComponent<Renderer>(),
                new Color(0.75f, 0.20f, 0.80f), emissive: 4f);  // death violet

            return VerifyBuilt(go.AddComponent<PooledVfx>().Init(maxScale: 1.1f, kind: VfxKind.Death), "Death");
        }

        /// <summary>
        /// Telegraph — a flat disc / ring on the ground that pulses before a spawn.
        /// Red-amber, XZ-flat (Y scale ~0.02).
        /// </summary>
        private PooledVfx CreateTelegraph()
        {
            var go = new GameObject("VfxTelegraph");
            go.SetActive(false);
            go.transform.SetParent(transform, false);

            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "TelegraphDisc";
            DestroyImmediate(disc.GetComponent<Collider>());
            disc.transform.SetParent(go.transform, false);
            disc.transform.localScale = new Vector3(1.4f, 0.02f, 1.4f);
            ApplyEmissiveMaterial(disc.GetComponent<Renderer>(),
                new Color(0.95f, 0.35f, 0.10f), emissive: 2.5f);  // warning orange

            return VerifyBuilt(go.AddComponent<PooledVfx>().Init(maxScale: 1.4f, kind: VfxKind.Telegraph), "Telegraph");
        }

        /// <summary>
        /// Assigns an emissive-capable URP Unlit material to <paramref name="r"/>.
        /// Falls back to a plain colour if the shader is absent.
        /// </summary>
        private static void ApplyEmissiveMaterial(Renderer r, Color colour, float emissive)
        {
            if (r == null)
            {
                // V+U: no renderer to colour → the built primitive will render the default
                // grey/pink, not the authored emissive flash. Self-report (Once) rather than
                // silently produce an off-colour effect.
                FlowTrace.Once("VfxPool", "emissive-norenderer",
                    "ApplyEmissiveMaterial: null Renderer — VFX primitive has no material applied (off-colour).");
                return;
            }
            Shader sh = Shader.Find("Universal Render Pipeline/Lit")
                     ?? Shader.Find("Universal Render Pipeline/Unlit")
                     ?? Shader.Find("Standard");
            if (sh == null)
            {
                // R+U §12 anti-pattern: a silent return here leaves the renderer on Unity's
                // default magenta/grey — the effect spawns but reads as "invisible/wrong".
                // TRACE the fallback so a missing-shader build self-reports instead of going quiet.
                FlowTrace.Once("VfxPool", "emissive-noshader",
                    "ApplyEmissiveMaterial: no Lit/Unlit/Standard shader found — VFX keeps the " +
                    "renderer's default material (effect may read invisible/off-colour). Check the URP shader set is included.");
                return;
            }
            // One shared Material per (shader, colour, emissive) — built once, reused
            // for every renderer (security audit E-VFXMAT: no per-renderer leak).
            string key = sh.GetInstanceID() + "|" + ColorUtility.ToHtmlStringRGBA(colour) + "|" + emissive.ToString("F3");
            if (!s_emissiveMatCache.TryGetValue(key, out var mat) || mat == null)
            {
                mat = new Material(sh);
                if (mat.HasProperty("_BaseColor"))  mat.SetColor("_BaseColor", colour);
                if (mat.HasProperty("_Color"))      mat.SetColor("_Color", colour);
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.SetColor("_EmissionColor", colour * emissive);
                    mat.EnableKeyword("_EMISSION");
                }
                if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 0f);  // opaque
                s_emissiveMatCache[key] = mat;
            }
            r.sharedMaterial = mat;
        }
    }

    // =========================================================================
    //  VfxKind
    // =========================================================================

    /// <summary>Which pool entry type a <see cref="PooledVfx"/> belongs to.</summary>
    public enum VfxKind { Hit, Death, Telegraph }

    // =========================================================================
    //  PooledVfx — drives a single effect instance
    // =========================================================================

    /// <summary>
    /// Drives one pooled VFX instance. Attached to the same GameObject as the
    /// visual. Call <see cref="Play"/> (auto-return) or <see cref="PlayHeld"/> +
    /// <see cref="ReturnToPool"/> (manual lifetime) from <see cref="VfxPool"/>.
    /// </summary>
    public sealed class PooledVfx : MonoBehaviour
    {
        private float    _maxScale;
        private VfxKind  _kind;
        private float    _duration;
        private float    _elapsed;
        private bool     _playing;
        private bool     _held;   // Telegraph holds indefinitely until ReturnToPool
        private Queue<PooledVfx> _returnPool;

        // ── Alpha cache (WO-410 perf) ─────────────────────────────────────────
        // GetComponentsInChildren<Renderer>() allocated a fresh array EVERY frame
        // for the whole lifetime of each active effect. Cache the renderers once,
        // and drive alpha through a shared MaterialPropertyBlock so we never touch
        // .material (no per-instance material instantiation) and never allocate
        // per frame. Renderers are children of this same GameObject and are built
        // once in the factory, so the set is stable across pool reuse — but we
        // still re-cache lazily (null guard) to stay correct if that ever changes.
        private Renderer[] _cachedRenderers;
        private Color[]    _baseColors;         // RGB authored by the factory, alpha overridden per frame
        private bool[]     _hasBaseColorProp;   // shader exposes _BaseColor
        private bool[]     _hasColorProp;       // shader exposes _Color
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId     = Shader.PropertyToID("_Color");
        [System.NonSerialized] private MaterialPropertyBlock _mpb;

        internal PooledVfx Init(float maxScale, VfxKind kind)
        {
            _maxScale = maxScale;
            _kind     = kind;
            return this;
        }

        /// <summary>
        /// Caches the child renderers and their authored base colours once, so the
        /// per-frame alpha write neither allocates a Renderer[] nor instantiates a
        /// per-instance material. Safe to call repeatedly; rebuilds the cache.
        /// </summary>
        private void CacheRenderers()
        {
            _cachedRenderers  = GetComponentsInChildren<Renderer>(includeInactive: true);
            int n = _cachedRenderers.Length;
            _baseColors       = new Color[n];
            _hasBaseColorProp = new bool[n];
            _hasColorProp     = new bool[n];
            for (int i = 0; i < n; i++)
            {
                var r = _cachedRenderers[i];
                var sh = r != null ? r.sharedMaterial : null;
                if (sh == null) continue;
                bool hasBase = sh.HasProperty(BaseColorId);
                bool hasCol  = sh.HasProperty(ColorId);
                _hasBaseColorProp[i] = hasBase;
                _hasColorProp[i]     = hasCol;
                // Snapshot the authored RGB so the MPB can preserve it each frame.
                _baseColors[i] = hasBase ? sh.GetColor(BaseColorId)
                               : hasCol  ? sh.GetColor(ColorId)
                               : Color.white;
            }
        }

        // ── Public ───────────────────────────────────────────────────────────

        /// <summary>Activates the effect at <paramref name="worldPos"/> and auto-returns after <paramref name="duration"/> seconds.</summary>
        internal void Play(Vector3 worldPos, float duration, Queue<PooledVfx> pool)
        {
            _returnPool = pool;
            _duration   = duration;
            _elapsed    = 0f;
            _playing    = true;
            _held       = false;
            if (_cachedRenderers == null) CacheRenderers();
            transform.position = worldPos;
            gameObject.SetActive(true);
        }

        /// <summary>Activates the telegraph ring at <paramref name="worldPos"/>; stays until <see cref="ReturnToPool"/>.</summary>
        internal void PlayHeld(Vector3 worldPos)
        {
            _elapsed = 0f;
            _playing = true;
            _held    = true;
            if (_cachedRenderers == null) CacheRenderers();
            transform.position = worldPos;
            gameObject.SetActive(true);
        }

        /// <summary>Deactivates the effect and returns it to <paramref name="pool"/>.</summary>
        internal void ReturnToPool(Queue<PooledVfx> pool)
        {
            _playing = false;
            _held    = false;
            gameObject.SetActive(false);
            pool?.Enqueue(this);
        }

        // ── Update ───────────────────────────────────────────────────────────

        private void Update()
        {
            if (!_playing) return;

            _elapsed += Time.deltaTime;

            if (_held)
            {
                // Telegraph pulse — slowly pulsate opacity using sine wave.
                float pulse = 0.55f + Mathf.Sin(_elapsed * 3.5f) * 0.30f;
                ApplyAlpha(pulse);
                return;
            }

            float t = Mathf.Clamp01(_elapsed / _duration);

            // Scale curve: quick pop up (0→0.6 in first 30%) then shrink (0.6→1 of duration).
            float scaleT = t < 0.3f
                ? Mathf.SmoothStep(0f, 1f, t / 0.3f)
                : Mathf.SmoothStep(1f, 0f, (t - 0.3f) / 0.7f);
            transform.localScale = Vector3.one * (_maxScale * scaleT);

            // Alpha fade — fully opaque for first 50%, then fade to 0.
            float alpha = t < 0.5f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.5f) / 0.5f);
            ApplyAlpha(alpha);

            if (t >= 1f && _returnPool != null)
                ReturnToPool(_returnPool);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private void ApplyAlpha(float a)
        {
            // Lazy-cache on first use (and re-cache after pool reuse via Play/PlayHeld).
            if (_cachedRenderers == null) CacheRenderers();
            _mpb ??= new MaterialPropertyBlock();

            var renderers = _cachedRenderers;
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null) continue;
                // Drive colour via a per-renderer MaterialPropertyBlock: no per-frame
                // Renderer[] alloc and no .material instantiation (override is local to
                // this renderer, so pooled instances can't contaminate each other).
                r.GetPropertyBlock(_mpb);
                if (_hasBaseColorProp[i])
                {
                    var c = _baseColors[i]; c.a = a;
                    _mpb.SetColor(BaseColorId, c);
                }
                if (_hasColorProp[i])
                {
                    var c = _baseColors[i]; c.a = a;
                    _mpb.SetColor(ColorId, c);
                }
                r.SetPropertyBlock(_mpb);
            }
        }
    }
}
