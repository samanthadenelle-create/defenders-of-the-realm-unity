// =============================================================================
// TowerRangeRing — a faint ground ring showing a defensive structure's attack
// RANGE (Tower.CurrentRange), so the player can READ coverage and place/upgrade
// towers correctly ("add range circles so they place right").
// -----------------------------------------------------------------------------
// Code-built (no art asset; runtime ring texture on a flat quad), attached by
// Tower.ApplyVisualForLevel. The radius tracks CurrentRange LIVE — it grows when
// the tower upgrades (each level's range from TowerData) or swaps type. Material
// setup mirrors HeroReachRing / HeroTargetIndicator (URP/Unlit transparent,
// double-sided) — proven to render in WebGL builds.
// =============================================================================

using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>Flat ground ring at a tower's attack radius (reads Tower.CurrentRange).</summary>
    [DisallowMultipleComponent]
    public sealed class TowerRangeRing : MonoBehaviour
    {
        [Tooltip("Local height above the tower base so the ring sits on the ground.")]
        [SerializeField] private float _groundY = 0.06f;

        [Tooltip("Ring tint — kept faint so overlapping coverage reads without dominating.")]
        [SerializeField] private Color _color = new Color(1f, 0.82f, 0.30f, 0.22f);

        private Tower _tower;
        private Transform _ring;
        private Material _mat;
        private float _builtRadius = -1f;
        private static Texture2D _ringTex;

        private void Awake() { _tower = GetComponent<Tower>(); }

        private void LateUpdate()
        {
            if (_tower == null) _tower = GetComponent<Tower>();
            float r = _tower != null ? _tower.CurrentRange : 0f;

            if (r <= 0.01f)
            {
                if (_ring != null) _ring.gameObject.SetActive(false);
                return;
            }

            if (_ring == null) Build();
            if (_ring == null) return;
            _ring.gameObject.SetActive(true);

            // Re-scale only when the radius changes (level-up / type-swap).
            if (!Mathf.Approximately(r, _builtRadius))
            {
                _builtRadius = r;
                _ring.localScale = new Vector3(r * 2f, r * 2f, 1f);   // diameter
            }
        }

        private void Build()
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "TowerRangeRing";
            var col = quad.GetComponent<Collider>();
            if (col != null) Destroy(col);

            // Lay flat on the ground (face +Y) at the tower base.
            quad.transform.SetParent(transform, false);
            quad.transform.localPosition = new Vector3(0f, _groundY, 0f);
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var mr = quad.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                                ?? Shader.Find("Sprites/Default")
                                ?? Shader.Find("Unlit/Transparent");
                if (shader != null)
                {
                    _mat = new Material(shader);
                    Texture2D ring = RingTexture();
                    if (_mat.HasProperty("_BaseMap")) _mat.SetTexture("_BaseMap", ring);
                    if (_mat.HasProperty("_MainTex")) _mat.SetTexture("_MainTex", ring);
                    _mat.mainTexture = ring;
                    if (_mat.HasProperty("_BaseColor")) _mat.SetColor("_BaseColor", _color);
                    _mat.color = _color;

                    if (_mat.HasProperty("_Surface")) _mat.SetFloat("_Surface", 1f);
                    if (_mat.HasProperty("_Blend"))   _mat.SetFloat("_Blend", 0f);
                    if (_mat.HasProperty("_SrcBlend")) _mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    if (_mat.HasProperty("_DstBlend")) _mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    if (_mat.HasProperty("_ZWrite"))   _mat.SetInt("_ZWrite", 0);
                    if (_mat.HasProperty("_Cull"))     _mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                    _mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    _mat.EnableKeyword("_ALPHABLEND_ON");
                    _mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                    mr.sharedMaterial = _mat;
                }
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }

            _ring = quad.transform;
        }

        /// <summary>A cached 128×128 soft ring (thin rim band), drawn once.</summary>
        private static Texture2D RingTexture()
        {
            if (_ringTex != null) return _ringTex;

            const int S = 128;
            var t = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float c = (S - 1) * 0.5f;
            const float outer = 62f, inner = 55f;   // rim band near the edge
            var clear = new Color(1f, 1f, 1f, 0f);
            for (int y = 0; y < S; y++)
            {
                for (int x = 0; x < S; x++)
                {
                    float dx = x - c, dy = y - c;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float band = Mathf.InverseLerp(outer, outer - 3f, r) * Mathf.InverseLerp(inner, inner + 3f, r);
                    t.SetPixel(x, y, band > 0f ? new Color(1f, 1f, 1f, Mathf.Clamp01(band)) : clear);
                }
            }
            t.Apply();
            _ringTex = t;
            return t;
        }
    }
}
