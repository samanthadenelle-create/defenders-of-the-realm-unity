// =============================================================================
// MoatWaterShimmer (DEF-195) — makes the flat translucent moat ring READ AS WATER.
// -----------------------------------------------------------------------------
// The moat (VillageSceneBuilder.Fortify.cs BuildMoat) is ~200+ flat quads that all
// share ONE translucent teal URP/Lit material ("MoatWater"). On its own that reads
// as a still glass pane. This component adds cheap, stylized, mobile-friendly LIFE:
//
//   1) A small procedural tiling RIPPLE NORMAL MAP is generated once at runtime and
//      assigned to the shared material's _BumpMap (only if it has none), so the flat
//      quads gain a wavelet surface that catches light — a soft specular shimmer
//      (no glassy "crystal" look; smoothness stays low, owner-tuned).
//   2) Each frame the _BumpMap UV offset is SCROLLED (two layers at different speeds
//      / directions via a second bump-scale trick) so the wavelets drift like a slow
//      current. Because every quad shares the one material, animating the SHARED
//      material moves the whole ring at once — one MonoBehaviour, zero per-tile cost.
//
// Owner constraint (2026-06-01): keep the de-glossed teal tint. This component does
// NOT touch _BaseColor / _Smoothness / _Metallic — it only adds motion + a subtle
// normal so the teal water shimmers instead of sitting flat. It never regresses the
// tint toward a glassy crystal.
//
// Self-contained: drop it on the Moat root, point it at the shared material (or let
// it auto-resolve from a child renderer). No scene wiring, no Core dependency.
// =============================================================================

using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// Scrolls a procedural ripple normal map across the shared moat-water material
    /// so the moat ring reads as gently flowing water (DEF-195). Mobile-cheap: one
    /// component drives the single shared material that every moat quad uses.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MoatWaterShimmer : MonoBehaviour
    {
        [Tooltip("The shared moat water material to animate. If null, the first " +
                 "child renderer's sharedMaterial is used at runtime.")]
        [SerializeField] private Material _waterMaterial;

        [Tooltip("UV scroll speed of the ripple normal map (units/sec).")]
        [SerializeField] private Vector2 _scrollSpeed = new Vector2(0.015f, 0.022f);

        [Tooltip("How strongly the ripple normal perturbs the surface (0 = flat).")]
        [SerializeField, Range(0f, 1f)] private float _bumpStrength = 0.35f;

        [Tooltip("How many times the ripple texture tiles across each quad's UVs.")]
        [SerializeField] private float _tiling = 2.0f;

        // URP/Lit shader property ids.
        private static readonly int BumpMapId    = Shader.PropertyToID("_BumpMap");
        private static readonly int BumpScaleId  = Shader.PropertyToID("_BumpScale");
        private static readonly int BumpMapStId  = Shader.PropertyToID("_BumpMap_ST");
        private static readonly int BaseMapStId  = Shader.PropertyToID("_BaseMap_ST");

        private Material _mat;
        private Vector2 _offset;

        private void Awake()
        {
            ResolveMaterial();
            if (_mat == null) { enabled = false; return; }
            EnsureRippleNormal();
        }

        private void ResolveMaterial()
        {
            if (_waterMaterial != null) { _mat = _waterMaterial; return; }
            // Fall back to the first child renderer's shared material (the moat quads).
            var rend = GetComponentInChildren<Renderer>();
            if (rend != null) _mat = rend.sharedMaterial;
        }

        /// <summary>
        /// Builds a small tiling ripple normal map (sine wavelets) and assigns it to
        /// the material's _BumpMap once, so the flat quads gain a shimmering surface.
        /// Skips if the material already carries a bump map (respects an authored one).
        /// </summary>
        private void EnsureRippleNormal()
        {
            if (_mat == null) return;
            if (!_mat.HasProperty(BumpMapId)) return;

            if (_mat.GetTexture(BumpMapId) == null)
                _mat.SetTexture(BumpMapId, BuildRippleNormalTex());

            _mat.EnableKeyword("_NORMALMAP");
            if (_mat.HasProperty(BumpScaleId))
                _mat.SetFloat(BumpScaleId, _bumpStrength);

            // Tile the normal so the wavelets are small + read at moat scale.
            if (_mat.HasProperty(BumpMapStId))
                _mat.SetVector(BumpMapStId, new Vector4(_tiling, _tiling, 0f, 0f));
        }

        private void Update()
        {
            if (_mat == null) return;

            _offset += _scrollSpeed * Time.deltaTime;
            // Wrap so the float offset never grows unbounded over a long session.
            _offset.x %= 1f;
            _offset.y %= 1f;

            // Scroll the ripple normal — this is the visible "flow".
            if (_mat.HasProperty(BumpMapStId))
                _mat.SetVector(BumpMapStId,
                    new Vector4(_tiling, _tiling, _offset.x, _offset.y));

            // If the material also carries a base map, drift it the opposite way at
            // half speed for a subtle two-layer current (no-op when _BaseMap is null).
            if (_mat.HasProperty(BaseMapStId) && _mat.GetTexture("_BaseMap") != null)
                _mat.SetVector(BaseMapStId,
                    new Vector4(1f, 1f, -_offset.x * 0.5f, -_offset.y * 0.5f));
        }

        /// <summary>
        /// Generates a small seamless ripple normal map procedurally — overlapping
        /// sine wavelets baked into tangent-space normals. Mobile-cheap (32x32,
        /// no mipmaps needed at this scale) and asset-free (no imported texture).
        /// </summary>
        private static Texture2D BuildRippleNormalTex()
        {
            const int N = 32;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false, true)
            {
                name = "MoatRippleNormal",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
            };

            var px = new Color32[N * N];
            const float twoPi = Mathf.PI * 2f;
            for (int y = 0; y < N; y++)
            {
                for (int x = 0; x < N; x++)
                {
                    float u = (float)x / N;
                    float v = (float)y / N;
                    // Two crossing wave sets -> a believable choppy wavelet height field.
                    float h = Mathf.Sin(twoPi * (u * 3f + v * 1f))
                            + Mathf.Sin(twoPi * (u * 1f - v * 2f)) * 0.6f;
                    // Partial derivatives (finite-difference of the height field) give
                    // the surface tilt -> tangent-space normal.
                    float hX = Mathf.Cos(twoPi * (u * 3f + v * 1f)) * 3f
                             + Mathf.Cos(twoPi * (u * 1f - v * 2f)) * 0.6f;
                    float hY = Mathf.Cos(twoPi * (u * 3f + v * 1f)) * 1f
                             - Mathf.Cos(twoPi * (u * 1f - v * 2f)) * 1.2f;
                    _ = h;
                    var n = new Vector3(-hX * 0.12f, -hY * 0.12f, 1f).normalized;
                    px[y * N + x] = new Color32(
                        (byte)((n.x * 0.5f + 0.5f) * 255f),
                        (byte)((n.y * 0.5f + 0.5f) * 255f),
                        (byte)((n.z * 0.5f + 0.5f) * 255f),
                        255);
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }
    }
}
