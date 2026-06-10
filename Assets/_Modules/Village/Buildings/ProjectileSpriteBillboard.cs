// =============================================================================
// ProjectileSpriteBillboard — gives a projectile a world-space, camera-facing
// sprite for its travel body (and a helper to pop a one-shot impact sprite).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// The arrow/bolt art sheets are 2D side-on sprites. To render them on a moving
// 3D projectile we use a child SpriteRenderer billboarded toward the camera each
// frame, BUT we keep the sprite ALIGNED to the travel direction (the arrow points
// where it flies) by rotating around the camera-facing axis. This reads correctly
// from a 3rd-person / top-down camera without authoring a mesh per projectile.
//
// USAGE (pooled tower bolt — PooledProjectile):
//   var bb = GetComponent<ProjectileSpriteBillboard>() ?? gameObject.AddComponent<>();
//   bb.SetSprite(ProjectileArtCatalog.ForElement(element));
//   // every Update, after moving: bb.FaceCameraAlongVelocity(forward);
//
// USAGE (impact burst): ProjectileSpriteBillboard.SpawnImpact(sprite, pos, 0.4f);
//
// All visuals are nullable-graceful: a null sprite hides the renderer, so callers
// keep their existing procedural visual when the art hasn't been sliced yet.
// =============================================================================

using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>Camera-facing sprite quad for a projectile's travel body, kept
    /// aligned to the flight direction. WebGL-safe (plain SpriteRenderer).</summary>
    [DisallowMultipleComponent]
    public sealed class ProjectileSpriteBillboard : MonoBehaviour
    {
        [SerializeField] private float _size = 0.9f;   // world height of the sprite

        private SpriteRenderer _sr;
        private Transform _spriteTf;
        private Camera _cam;

        // When true, the billboard faces the camera every LateUpdate using the
        // projectile root's forward (set by ProjectileMover for hero shots). Pooled
        // tower bolts drive FaceCameraAlongVelocity manually and leave this off.
        private bool _autoFollow;

        /// <summary>Enable per-frame self-billboarding off the root's forward (hero
        /// projectiles driven by ProjectileMover, which orients transform.forward).</summary>
        public void EnableAutoFollow() => _autoFollow = true;

        private void LateUpdate()
        {
            if (_autoFollow) FaceCameraAlongVelocity(transform.forward);
        }

        // The art sheets draw arrows pointing RIGHT (+local X). We rotate the
        // sprite so its local +X aligns with the projectile's travel direction.
        private const float SpriteForwardIsLocalRight = 1f;

        /// <summary>Assign the travel sprite. Null hides the billboard (caller keeps
        /// its procedural visual). Safe to call repeatedly (pooled reuse).</summary>
        public void SetSprite(Sprite sprite, float worldSize = -1f)
        {
            if (worldSize > 0f) _size = worldSize;
            EnsureRenderer();
            _sr.sprite = sprite;
            _sr.enabled = sprite != null;
            if (sprite != null) RescaleToSize(sprite);
        }

        private void EnsureRenderer()
        {
            if (_sr != null) return;

            var child = new GameObject("ProjectileSprite");
            child.transform.SetParent(transform, false);
            _spriteTf = child.transform;

            _sr = child.AddComponent<SpriteRenderer>();
            _sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _sr.receiveShadows = false;
            // Render slightly in front so it isn't occluded by its own bolt mesh.
            _sr.sortingOrder = 10;
        }

        private void RescaleToSize(Sprite sprite)
        {
            if (sprite == null || _spriteTf == null) return;
            // Sprite world height = bounds.size.y * scale -> scale to target _size.
            float h = Mathf.Max(0.0001f, sprite.bounds.size.y);
            float k = _size / h;
            _spriteTf.localScale = new Vector3(k, k, k);
        }

        /// <summary>Billboard the sprite to face the camera while pointing along
        /// <paramref name="velocityDir"/> (world). Call after moving each frame.</summary>
        public void FaceCameraAlongVelocity(Vector3 velocityDir)
        {
            if (_sr == null || !_sr.enabled || _spriteTf == null) return;
            if (_cam == null) _cam = Camera.main;

            Vector3 toCam = _cam != null
                ? (_cam.transform.position - _spriteTf.position).normalized
                : Vector3.back;

            // Build a basis: the sprite plane faces the camera (normal = toCam),
            // and within that plane its local +X points along the travel direction
            // (projected onto the plane so the arrow doesn't foreshorten oddly).
            Vector3 dir = velocityDir.sqrMagnitude > 1e-6f ? velocityDir.normalized : _spriteTf.right;
            Vector3 inPlaneDir = Vector3.ProjectOnPlane(dir, toCam);
            if (inPlaneDir.sqrMagnitude < 1e-6f) inPlaneDir = Vector3.up;
            inPlaneDir.Normalize();

            Vector3 up = Vector3.Cross(toCam, inPlaneDir); // sprite-up within the plane
            // LookRotation(forward=-toCam) faces the quad at the camera; then we
            // orient its local-right to inPlaneDir via the chosen up vector.
            _spriteTf.rotation = Quaternion.LookRotation(-toCam, up);
            // Rotate so local +X (the arrow's nose) lands on inPlaneDir.
            _spriteTf.Rotate(0f, 0f, 90f * SpriteForwardIsLocalRight, Space.Self);
        }

        /// <summary>Pop a short-lived, camera-facing impact sprite at <paramref name="pos"/>.
        /// No-op when <paramref name="sprite"/> is null. Self-destructs.</summary>
        public static void SpawnImpact(Sprite sprite, Vector3 pos, float worldSize = 1.0f, float life = 0.35f)
        {
            if (sprite == null) return;

            var go = new GameObject("ProjectileImpact");
            go.transform.position = pos;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            sr.receiveShadows = false;
            sr.sortingOrder = 12;

            float h = Mathf.Max(0.0001f, sprite.bounds.size.y);
            float k = worldSize / h;
            go.transform.localScale = new Vector3(k, k, k);

            // Face the camera once (impacts are brief — no per-frame billboard).
            var cam = Camera.main;
            if (cam != null)
                go.transform.rotation = Quaternion.LookRotation(
                    (go.transform.position - cam.transform.position).normalized, Vector3.up);

            go.AddComponent<ImpactSpriteFade>().Begin(sr, life);
        }
    }

    /// <summary>Fades + scales an impact sprite over its short life, then destroys
    /// the GameObject. Kept tiny + self-contained (pooled-free, brief one-shots).</summary>
    public sealed class ImpactSpriteFade : MonoBehaviour
    {
        private SpriteRenderer _sr;
        private float _life;
        private float _t;
        private Vector3 _baseScale;

        public void Begin(SpriteRenderer sr, float life)
        {
            _sr = sr;
            _life = Mathf.Max(0.05f, life);
            _baseScale = transform.localScale;
        }

        private void Update()
        {
            if (_sr == null) { Destroy(gameObject); return; }
            _t += Time.deltaTime;
            float u = Mathf.Clamp01(_t / _life);

            // Grow ~25% and fade out.
            transform.localScale = _baseScale * (1f + 0.25f * u);
            var c = _sr.color;
            c.a = 1f - u;
            _sr.color = c;

            if (u >= 1f) Destroy(gameObject);
        }
    }
}
