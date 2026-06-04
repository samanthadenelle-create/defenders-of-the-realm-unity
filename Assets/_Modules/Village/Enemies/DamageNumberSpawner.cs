// =============================================================================
// DamageNumberSpawner — asset-free floating combat text for enemy hits.
// -----------------------------------------------------------------------------
// When a hero ability, pet or tower lands damage on an Enemy (everything routes
// through Enemy.TakeDamage(float)), we pop a world-space integer that floats up
// from the enemy's head and fades out, then self-destroys. This is the visible
// confirmation the player needs to SEE damage rise after unlocking a damage
// talent — bigger, brighter numbers for bigger hits.
//
// FULLY CODE-BUILT, NO ASSETS: the spawner mirrors the project's existing
// world-space text idiom (a TextMesh with characterSize / anchor / alignment,
// billboarded to the camera each LateUpdate — same shape as the dungeon portal
// sign and TownsfolkBubble). Nothing here needs a prefab, scene wiring or a
// PanelSettings, so it works in a fresh headless build with zero setup.
//
// CHEAP: each number is a short-lived GameObject that animates from cached
// start values (no per-frame allocation, no GetComponent in Update) and Destroys
// itself after the lifetime. The only allocation per hit is the GameObject +
// its TextMesh + the int->string of the damage value, which is unavoidable for
// floating text and is bounded by the hit rate.
//
// CAMERA-SAFE: spawning and billboarding both null-guard Camera.main, so a frame
// with no active camera simply skips the float without throwing.
// =============================================================================

using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// A single floating damage number. Built entirely in code by
    /// <see cref="Spawn"/> — a billboarded world-space <see cref="TextMesh"/>
    /// that rises while fading out, then destroys itself. No prefab or scene
    /// wiring required.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DamageNumberSpawner : MonoBehaviour
    {
        // ── Animation tuning ──────────────────────────────────────────────────

        /// <summary>Total seconds the number lives before it is destroyed.
        /// DEF-260 #6: shortened from 0.8s so the number pops and clears faster
        /// (owner: "fade faster").</summary>
        private const float Lifetime = 0.55f;

        /// <summary>World-units the number rises over its <see cref="Lifetime"/>.</summary>
        private const float RiseDistance = 1.0f;

        /// <summary>
        /// Base character size of the TextMesh (matches the village's other
        /// world-space text). Scaled up a little for bigger hits.
        /// DEF-260 #6: reduced from 0.18 — the prior "18" read as oversized on the
        /// PatriciaLight stage; this keeps numbers legible but no longer dominant.
        /// </summary>
        private const float BaseCharacterSize = 0.11f;

        /// <summary>Damage value that maps to a "full size" number (caps the scale ramp).</summary>
        private const float BigHitDamage = 40f;

        // Normal-hit colour (warm gold) and big-hit colour (hot orange-red). We
        // lerp between them by hit magnitude so a talent-buffed hit reads brighter
        // as well as bigger. No crit flag is exposed on the damage path, so we do
        // NOT invent one — magnitude is the honest signal we have.
        private static readonly Color NormalColor = new Color(1.0f, 0.92f, 0.45f, 1f);
        private static readonly Color BigColor    = new Color(1.0f, 0.45f, 0.20f, 1f);

        // ── Cached per-instance animation state ───────────────────────────────

        private TextMesh _text;
        private Transform _tf;
        private Camera _faceCamera;
        private Vector3 _startPos;
        private Color _startColor;
        private float _baseScale;
        private float _age;
        private float _lifetime = Lifetime;   // overridable so labels can linger longer
        private float _rise = RiseDistance;

        /// <summary>
        /// Spawns a floating damage number for <paramref name="amount"/> at
        /// <paramref name="worldPos"/>. No-op (returns null) when there is no
        /// active camera. Everything is built in code — call it and forget it.
        /// </summary>
        /// <param name="amount">Damage dealt; shown as a rounded integer.</param>
        /// <param name="worldPos">World position to float up from (the enemy's head).</param>
        public static DamageNumberSpawner Spawn(float amount, Vector3 worldPos)
        {
            if (amount <= 0f) return null;

            // Guard the camera up front — no point building a billboard with
            // nothing to face. Cheap early-out for any off-screen / no-camera frame.
            Camera cam = Camera.main;
            if (cam == null) return null;

            var go = new GameObject("DamageNumber");
            go.transform.position = worldPos;

            var num = go.AddComponent<DamageNumberSpawner>();
            num.Build(amount, cam);
            return num;
        }

        /// <summary>As <see cref="Spawn(float,Vector3)"/> but tints the number to
        /// <paramref name="color"/> (source-coded: hero hits vs pet hits).</summary>
        public static DamageNumberSpawner Spawn(float amount, Vector3 worldPos, Color color)
        {
            var num = Spawn(amount, worldPos);
            if (num != null) num.ApplyColor(color);
            return num;
        }

        /// <summary>Overrides the built number's colour (source tint).</summary>
        public void ApplyColor(Color color)
        {
            _startColor = color;
            if (_text != null) _text.color = color;
        }

        /// <summary>
        /// Spawns a floating TEXT label (e.g. "LEVEL UP! Lv.3") at
        /// <paramref name="worldPos"/> — same rise/fade/billboard as a damage
        /// number but bold, coloured and lingering longer. Used by the XP system
        /// for level-up feedback. No-op (null) when there is no active camera.
        /// </summary>
        public static DamageNumberSpawner SpawnLabel(string label, Vector3 worldPos, Color color, float scale = 1.2f)
        {
            if (string.IsNullOrEmpty(label)) return null;
            Camera cam = Camera.main;
            if (cam == null) return null;

            var go = new GameObject("FloatingLabel");
            go.transform.position = worldPos;

            var n = go.AddComponent<DamageNumberSpawner>();
            n.BuildLabel(label, color, scale, cam);
            return n;
        }

        /// <summary>
        /// Builds the TextMesh and caches the animation start state. Bigger hits
        /// start larger and hotter so a damage-talent upgrade is visible at a glance.
        /// </summary>
        private void Build(float amount, Camera cam)
        {
            _tf = transform;
            _faceCamera = cam;
            _startPos = _tf.position;
            _age = 0f;

            // 0..1 magnitude ramp → size + colour. A normal melee hit sits low on
            // the ramp; a buffed ability hit pushes toward the big end.
            float t = Mathf.Clamp01(amount / BigHitDamage);

            // DEF-260 #6: gentler scale ramp (was 0.85→1.5) so even a big hit no
            // longer balloons across the screen; bigger hits still read a touch larger.
            _baseScale = Mathf.Lerp(0.8f, 1.2f, t);
            _tf.localScale = Vector3.one * _baseScale;

            _startColor = Color.Lerp(NormalColor, BigColor, t);

            _text = gameObject.AddComponent<TextMesh>();
            _text.text = Mathf.RoundToInt(amount).ToString();
            _text.anchor = TextAnchor.MiddleCenter;
            _text.alignment = TextAlignment.Center;
            _text.characterSize = BaseCharacterSize;
            _text.fontSize = 96;            // crisp glyphs; size is driven by characterSize + scale
            _text.richText = false;
            _text.color = _startColor;

            // Render on top of the world geometry so the number is never buried
            // behind the enemy mesh. TextMesh exposes its shared material's queue.
            MeshRenderer mr = GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                if (mr.sharedMaterial != null)
                    mr.sharedMaterial.renderQueue = 4000; // after Transparent (3000)
            }
        }

        /// <summary>Builds a bold, longer-lived text label (level-up popups).</summary>
        private void BuildLabel(string label, Color color, float scale, Camera cam)
        {
            _tf = transform;
            _faceCamera = cam;
            _startPos = _tf.position;
            _age = 0f;
            _lifetime = 1.6f;   // linger ~2x a damage number so the player reads it
            _rise = 1.6f;

            _baseScale = scale;
            _tf.localScale = Vector3.one * _baseScale;
            _startColor = color;

            _text = gameObject.AddComponent<TextMesh>();
            _text.text = label;
            _text.anchor = TextAnchor.MiddleCenter;
            _text.alignment = TextAlignment.Center;
            _text.characterSize = BaseCharacterSize;
            _text.fontSize = 96;
            _text.fontStyle = FontStyle.Bold;
            _text.richText = false;
            _text.color = _startColor;

            MeshRenderer mr = GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                if (mr.sharedMaterial != null)
                    mr.sharedMaterial.renderQueue = 4000;
            }
        }

        private void LateUpdate()
        {
            _age += Time.deltaTime;
            float k = _age / _lifetime;        // 0..1 progress
            if (k >= 1f)
            {
                UnityEngine.Object.Destroy(gameObject);
                return;
            }

            // Rise: ease-out so the number pops up then settles. Pure local math,
            // no allocation.
            float rise = (1f - (1f - k) * (1f - k)) * _rise;
            _tf.position = _startPos + Vector3.up * rise;

            // Fade: DEF-260 #6 — hold full opacity only briefly, then fade to zero
            // (owner: "fade faster"). Was holding for the first third of life.
            float alpha = k < 0.18f ? 1f : Mathf.InverseLerp(1f, 0.18f, k);
            Color c = _startColor;
            c.a = alpha;
            _text.color = c;

            // Gentle pop-then-shrink for extra readability on the way up.
            float scale = _baseScale * (1f + 0.15f * Mathf.Sin(k * Mathf.PI));
            _tf.localScale = Vector3.one * scale;

            // Billboard to the camera (same idiom as TownsfolkBubble). Re-resolve
            // Camera.main only if our cached camera went away (scene swap / death).
            if (_faceCamera == null) _faceCamera = Camera.main;
            if (_faceCamera != null)
            {
                Vector3 toCam = _tf.position - _faceCamera.transform.position;
                if (toCam.sqrMagnitude > 0.0001f)
                    _tf.rotation = Quaternion.LookRotation(toCam, Vector3.up);
            }
        }
    }
}
