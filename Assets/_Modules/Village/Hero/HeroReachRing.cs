// =============================================================================
// HeroReachRing — a faint ground ring at the hero's feet showing melee/engagement
// reach, so the player can READ how close they must be to land a basic swing
// (answers "I can't tell my hitbox"). Code-built (no art asset; runtime ring
// texture on a flat quad), runtime-attached by HeroControlEnsurer. The radius
// auto-syncs to the sibling PlayerAttackController's attack range when present,
// so the ring always shows the REAL hitbox, not a guess.
//
// Material setup mirrors HeroTargetIndicator / PetDeployer.BuildSpriteBillboard
// (URP/Unlit transparent, double-sided) — proven to render in WebGL builds.
// =============================================================================

using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>Flat ground ring at the hero's feet visualising basic-attack reach.</summary>
    [DisallowMultipleComponent]
    public sealed class HeroReachRing : MonoBehaviour
    {
        [Tooltip("Ring radius (m). Auto-synced to PlayerAttackController.AttackRange if present.")]
        [SerializeField, Min(0.5f)] private float _radius = 3.2f;

        [Tooltip("Local height above the hero root so the ring sits on the ground.")]
        [SerializeField] private float _groundY = 0.06f;

        [Tooltip("Ring tint (kept faint so it informs without dominating).")]
        [SerializeField] private Color _color = new Color(0.45f, 0.80f, 1f, 0.30f);

        [Tooltip("Gentle breathing pulse so the ring reads as a UI affordance.")]
        [SerializeField, Min(0f)] private float _pulseSpeed = 2.0f;
        [SerializeField, Range(0f, 0.5f)] private float _pulseAmount = 0.15f;

        private Transform _ring;
        private Material _mat;
        private float _baseAlpha;
        private static Texture2D _ringTex;

        private void Start()
        {
            // Sync the ring to the real melee hitbox if the swing controller is present.
            var pac = GetComponent<PlayerAttackController>();
            if (pac != null) _radius = pac.AttackRange;

            BuildRing();
        }

        private void OnDestroy()
        {
            if (_ring != null) Destroy(_ring.gameObject);
        }

        private void Update()
        {
            if (_mat == null) return;
            float a = _baseAlpha * (1f + Mathf.Sin(Time.time * _pulseSpeed) * _pulseAmount);
            Color c = _color; c.a = Mathf.Clamp01(a);
            if (_mat.HasProperty("_BaseColor")) _mat.SetColor("_BaseColor", c);
            _mat.color = c;
        }

        private void BuildRing()
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "HeroReachRing";
            var col = quad.GetComponent<Collider>();
            if (col != null) Destroy(col);

            // Lay the quad flat on the ground (face +Y) and size it to the reach diameter.
            quad.transform.SetParent(transform, false);
            quad.transform.localPosition = new Vector3(0f, _groundY, 0f);
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            quad.transform.localScale = new Vector3(_radius * 2f, _radius * 2f, 1f);

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
            _baseAlpha = _color.a;
        }

        /// <summary>A cached 128×128 soft ring (thin band near the rim), drawn once.</summary>
        private static Texture2D RingTexture()
        {
            if (_ringTex != null) return _ringTex;

            const int S = 128;
            var t = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float c = (S - 1) * 0.5f;
            const float outer = 62f, inner = 56f;   // thin rim band
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
