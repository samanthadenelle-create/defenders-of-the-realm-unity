using UnityEngine;

namespace DeNelle.Core
{
    /// <summary>
    /// Reusable "draw attention" VFX — a square <see cref="LineRenderer"/> frame around a
    /// target whose material texture-offset scrolls so a glow band travels around the frame.
    /// Source-agnostic: a merchant in range, a parry window, a low-HP heal hint, a tutorial
    /// step. Driven by <see cref="Hud.Focus"/> / <see cref="Hud.Unfocus"/> — don't add directly.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public sealed class AttentionGlow : MonoBehaviour
    {
        private LineRenderer _lr;
        private Material _mat;
        private float _scroll;

        public float Speed = 1.4f;                                   // loops per second
        public Color Tint  = new Color(1f, 0.85f, 0.35f, 1f);        // warm gold default

        /// <summary>Spawn a glow frame as a child of <paramref name="target"/> so it follows it.</summary>
        public static AttentionGlow Attach(GameObject target, float size, Color tint, float speed)
        {
            var go = new GameObject("AttentionGlow");
            go.transform.SetParent(target.transform, false);
            go.transform.localPosition = new Vector3(0f, 0.06f, 0f);  // hover just off the ground
            var g = go.AddComponent<AttentionGlow>();
            g.Tint = tint; g.Speed = speed;
            g.Build(Mathf.Max(0.3f, size));
            return g;
        }

        private void Build(float size)
        {
            _lr = GetComponent<LineRenderer>();
            _lr.useWorldSpace = false;
            _lr.loop = true;
            _lr.numCornerVertices = 3;
            _lr.numCapVertices = 0;
            _lr.alignment = LineAlignment.View;
            _lr.textureMode = LineTextureMode.Tile;
            _lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _lr.receiveShadows = false;

            float h = size * 0.5f;
            _lr.positionCount = 4;
            _lr.SetPositions(new[]
            {
                new Vector3(-h, 0f, -h), new Vector3( h, 0f, -h),
                new Vector3( h, 0f,  h), new Vector3(-h, 0f,  h),
            });
            _lr.widthMultiplier = Mathf.Clamp(size * 0.08f, 0.04f, 0.5f);

            _mat = new Material(Shader.Find("Sprites/Default"));
            _mat.mainTexture = StreakTexture();
            _mat.color = Tint;
            _mat.mainTextureScale = new Vector2(6f, 1f);              // ~6 glints tiled around the loop
            _lr.material = _mat;
        }

        // A soft comet of brightness that repeats — scrolling its offset sends it round the frame.
        private static Texture2D _streak;
        private static Texture2D StreakTexture()
        {
            if (_streak != null) return _streak;
            const int w = 64;
            var tex = new Texture2D(w, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat };
            for (int x = 0; x < w; x++)
            {
                float t = x / (float)(w - 1);
                float a = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(t - 0.5f) * 2f), 3f);  // bright centre, soft tails
                tex.SetPixel(x, 0, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            _streak = tex;
            return tex;
        }

        private void Update()
        {
            if (_mat == null) return;
            _scroll -= Speed * Time.deltaTime;
            _mat.mainTextureOffset = new Vector2(_scroll, 0f);
        }

        private void OnDestroy()
        {
            if (_mat != null) Destroy(_mat);
        }
    }
}
