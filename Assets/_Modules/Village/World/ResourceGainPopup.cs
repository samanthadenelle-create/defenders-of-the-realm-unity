// =============================================================================
// ResourceGainPopup — floating "+X Wood" / "+3 Food" world text for harvest income.
// -----------------------------------------------------------------------------
// Uses the same world-space popup pattern as DamageNumberSpawner (see Enemy.cs
// and PlayerAttackController) so it feels consistent. Simple, allocation-light,
// code-built (no prefab dependency). Rises, fades, then destroys.
//
// All pet / outpost / node harvest popups MUST go through this (or a future
// shared VFX manager) so the visual language for Economy income is unified.
// =============================================================================
using UnityEngine;
using TMPro;

namespace DeNelle.Village.World
{
    /// <summary>
    /// Lightweight world-space floating resource gain text. Spawn via the static
    /// method. The popup animates upward and fades over its lifetime.
    /// </summary>
    public sealed class ResourceGainPopup : MonoBehaviour
    {
        private const float LifeTime = 1.6f;
        private const float RiseSpeed = 1.8f;
        private const float StartAlpha = 1f;

        private TextMeshPro _text;
        private float _time;
        private Color _baseColor;
        private Vector3 _velocity;

        /// <summary>
        /// Spawn a floating resource gain popup at world position.
        /// </summary>
        /// <param name="worldPos">Base world position (usually above pet or structure).</param>
        /// <param name="message">"+5 Wood" or similar.</param>
        /// <param name="tint">Resource-themed color.</param>
        public static void Spawn(Vector3 worldPos, string message, Color tint)
        {
            var go = new GameObject("ResourceGainPopup");
            go.transform.position = worldPos + Vector3.up * 1.2f;

            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = message;
            tmp.fontSize = 2.2f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = tint;
            tmp.outlineWidth = 0.15f;
            tmp.outlineColor = new Color(0f, 0f, 0f, 0.6f);

            // Billboard toward camera (simple)
            var cam = Camera.main;
            if (cam != null)
                go.transform.rotation = Quaternion.LookRotation(cam.transform.forward);

            var popup = go.AddComponent<ResourceGainPopup>();
            popup._text = tmp;
            popup._baseColor = tint;
            popup._velocity = Vector3.up * RiseSpeed + Random.insideUnitSphere * 0.3f;
        }

        private void Update()
        {
            _time += Time.deltaTime;
            float t = _time / LifeTime;

            transform.position += _velocity * Time.deltaTime;
            _velocity *= 0.96f; // damp

            if (_text != null)
            {
                float alpha = Mathf.Lerp(StartAlpha, 0f, t);
                _text.color = new Color(_baseColor.r, _baseColor.g, _baseColor.b, alpha);
            }

            if (_time >= LifeTime)
            {
                Destroy(gameObject);
            }
        }
    }
}
