// =============================================================================
// DecalSpawner — pooled, procedural ground scorch decals. Combat-feel juice.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WHAT IT DOES:
//   Drops a soft dark scorch mark on the ground where an enemy dies, then fades
//   it out over a few seconds. Persistent marks make a cleared battlefield read
//   as "something happened here" instead of resetting to a pristine field.
//
// DESIGN (deliberately self-contained + low-risk):
//   • No art dependency — the scorch sprite is a procedural radial texture built
//     once at startup, so it renders on a fresh clone before any decal art is
//     authored (mirrors VFXManager's procedural fallback philosophy).
//   • Pooled — quads are reused, capped at MaxActive; the oldest is recycled when
//     the cap is hit so a long fight never leaks GameObjects.
//   • Self-bootstraps — a persistent instance is created on first scene load, so
//     Enemy.Die()'s `DecalSpawner.Instance?.SpawnScorch(pos)` just works without
//     anyone wiring it into a scene. All call sites stay null-safe regardless.
//   • Mobile-friendly — flat unlit transparent quads, no real-time lights, no
//     per-frame allocation. Fades via a MaterialPropertyBlock (no material
//     instancing).
//
// SHADER: uses the first available of Sprites/Default → URP/Unlit → Unlit/
//   Transparent. If none resolve (extremely unlikely), spawning silently no-ops.
// =============================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// Spawns pooled, fading ground scorch decals at world positions. Created
    /// automatically at startup; call <see cref="SpawnScorch"/> from any death
    /// or explosion site (always through the null-safe <see cref="Instance"/>).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DecalSpawner : MonoBehaviour
    {
        // ── Singleton ─────────────────────────────────────────────────────────

        public static DecalSpawner Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("[DecalSpawner]");
            DontDestroyOnLoad(go);
            go.AddComponent<DecalSpawner>();
        }

        // ── Tuning (first-pass; tune for feel) ────────────────────────────────

        [Tooltip("Maximum simultaneous scorch decals. Oldest is recycled past this.")]
        [SerializeField, Min(1)] private int _maxActive = 32;

        [Tooltip("Seconds a scorch lingers at full strength before it starts fading.")]
        [SerializeField, Min(0f)] private float _holdSeconds = 5f;

        [Tooltip("Seconds the scorch takes to fade from full to gone.")]
        [SerializeField, Min(0.1f)] private float _fadeSeconds = 3f;

        [Tooltip("World-space diameter range of a scorch mark (randomised per spawn).")]
        [SerializeField] private Vector2 _sizeRange = new Vector2(1.4f, 2.2f);

        [Tooltip("Lift above the spawn point so the decal sits on the ground without z-fighting.")]
        [SerializeField, Min(0f)] private float _groundOffset = 0.03f;

        [Tooltip("Scorch tint. Alpha is the starting opacity; the fade drives it to 0.")]
        [SerializeField] private Color _scorchColor = new Color(0.05f, 0.04f, 0.03f, 0.75f);

        // ── Runtime ───────────────────────────────────────────────────────────

        private Texture2D _scorchTex;
        private Material  _scorchMat;
        private bool      _renderable;          // false if no transparent shader resolved

        private readonly Queue<GameObject> _pool   = new Queue<GameObject>();
        private readonly List<GameObject>  _active  = new List<GameObject>();
        private MaterialPropertyBlock _mpb;

        // Sprites/Default tints via "_Color"; URP/Unlit via "_BaseColor". We push
        // both so whichever shader resolved picks up the right one.
        private static readonly int ColorId     = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int MainTexId   = Shader.PropertyToID("_MainTex");
        private static readonly int BaseMapId   = Shader.PropertyToID("_BaseMap");

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _mpb = new MaterialPropertyBlock();
            BuildAssets();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Drop a fading scorch decal flat on the ground at <paramref name="worldPos"/>.
        /// Safe to call every kill — pooled and capped. No-op if no transparent
        /// shader was available at startup.
        /// </summary>
        public void SpawnScorch(Vector3 worldPos)
        {
            if (!_renderable) return;

            // Enforce the active cap by recycling the oldest mark.
            if (_active.Count >= _maxActive)
            {
                var oldest = _active[0];
                _active.RemoveAt(0);
                Recycle(oldest);
            }

            var decal = Acquire();
            float size = Random.Range(_sizeRange.x, _sizeRange.y);

            var t = decal.transform;
            t.position = worldPos + Vector3.up * _groundOffset;
            // Lie flat (face up) with a random spin so repeats don't look stamped.
            t.rotation = Quaternion.Euler(90f, Random.Range(0f, 360f), 0f);
            t.localScale = new Vector3(size, size, 1f);

            decal.SetActive(true);
            _active.Add(decal);

            // Bump the generation so any still-scheduled coroutine from a previous
            // life of this pooled object bails instead of fading/recycling THIS one
            // (a pooled decal can be re-acquired the same frame the cap evicts it).
            var tag = decal.GetComponent<ScorchTag>();
            tag.Gen++;
            StartCoroutine(FadeAndReturn(decal, tag, tag.Gen));
        }

        // ── Pool internals ───────────────────────────────────────────────────

        private GameObject Acquire()
        {
            while (_pool.Count > 0)
            {
                var pooled = _pool.Dequeue();
                if (pooled != null) return pooled;   // skip any destroyed-on-scene-unload entries
            }
            return CreateDecal();
        }

        private GameObject CreateDecal()
        {
            // A unit quad faces +Z; we rotate it flat at spawn time.
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "[Scorch]";
            go.transform.SetParent(transform, false);

            // Decals are visual only — strip the auto-added collider.
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            go.AddComponent<ScorchTag>();   // generation marker for coroutine safety

            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial   = _scorchMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows    = false;
            mr.lightProbeUsage   = UnityEngine.Rendering.LightProbeUsage.Off;

            go.SetActive(false);
            return go;
        }

        private void Recycle(GameObject decal)
        {
            if (decal == null) return;
            // Invalidate any in-flight fade coroutine for this object.
            var tag = decal.GetComponent<ScorchTag>();
            if (tag != null) tag.Gen++;
            decal.SetActive(false);
            _pool.Enqueue(decal);
        }

        private IEnumerator FadeAndReturn(GameObject decal, ScorchTag tag, int myGen)
        {
            var mr = decal != null ? decal.GetComponent<MeshRenderer>() : null;
            if (mr == null) yield break;

            ApplyAlpha(mr, _scorchColor.a);

            if (_holdSeconds > 0f) yield return new WaitForSeconds(_holdSeconds);

            float t = 0f;
            while (t < _fadeSeconds)
            {
                // Bail if this object was recycled/reused out from under us.
                if (decal == null || tag == null || tag.Gen != myGen) yield break;
                t += Time.deltaTime;
                ApplyAlpha(mr, Mathf.Lerp(_scorchColor.a, 0f, t / _fadeSeconds));
                yield return null;
            }

            if (decal != null && tag != null && tag.Gen == myGen)
            {
                _active.Remove(decal);
                Recycle(decal);
            }
        }

        private void ApplyAlpha(MeshRenderer mr, float alpha)
        {
            var c = _scorchColor; c.a = alpha;
            mr.GetPropertyBlock(_mpb);
            _mpb.SetColor(ColorId, c);
            _mpb.SetColor(BaseColorId, c);
            mr.SetPropertyBlock(_mpb);
        }

        // ── Asset construction (texture + material + shader resolve) ───────────

        private void BuildAssets()
        {
            _scorchTex = BuildRadialTexture(64);

            var shader = Shader.Find("Sprites/Default")
                      ?? Shader.Find("Universal Render Pipeline/Unlit")
                      ?? Shader.Find("Unlit/Transparent");

            if (shader == null)
            {
                Debug.LogWarning("[DecalSpawner] No transparent shader found — scorch decals disabled.");
                _renderable = false;
                return;
            }

            _scorchMat = new Material(shader) { name = "ScorchDecal (runtime)" };
            // Set whichever texture slot the resolved shader uses.
            if (_scorchMat.HasProperty(MainTexId)) _scorchMat.SetTexture(MainTexId, _scorchTex);
            if (_scorchMat.HasProperty(BaseMapId)) _scorchMat.SetTexture(BaseMapId, _scorchTex);

            // Force a transparent, depth-write-off setup for the URP/Unlit path.
            // Sprites/Default is already configured this way in its shader and has
            // none of these properties, so guard each to avoid "property doesn't
            // exist" warnings on that path.
            if (_scorchMat.HasProperty("_Surface"))
            {
                _scorchMat.SetInt("_Surface", 1);        // 0 = opaque, 1 = transparent (URP)
                _scorchMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                _scorchMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                _scorchMat.SetInt("_ZWrite", 0);
                _scorchMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            _scorchMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            _renderable = true;
        }

        /// <summary>
        /// Builds a soft circular alpha texture (1 at centre → 0 at the rim) with a
        /// little radial noise so the scorch edge isn't a perfect disc.
        /// </summary>
        private static Texture2D BuildRadialTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode  = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "ScorchRadial (runtime)",
            };

            float half = size * 0.5f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float r  = Mathf.Sqrt(dx * dx + dy * dy);

                    // Soft falloff; squared for a denser, more burnt centre.
                    float a = Mathf.Clamp01(1f - r);
                    a *= a;

                    // Gentle angular noise on the rim so the disc looks organic.
                    float ang = Mathf.Atan2(dy, dx);
                    a *= 0.82f + 0.18f * Mathf.PerlinNoise(Mathf.Cos(ang) * 2f + 4f,
                                                           Mathf.Sin(ang) * 2f + 4f);

                    byte alpha = (byte)(Mathf.Clamp01(a) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            return tex;
        }

        /// <summary>
        /// Per-decal generation marker. Incremented whenever the pooled object is
        /// (re)spawned or recycled, so a fade coroutine started for an earlier life
        /// can detect it no longer owns the object and bail.
        /// </summary>
        private sealed class ScorchTag : MonoBehaviour
        {
            public int Gen;
        }
    }
}
