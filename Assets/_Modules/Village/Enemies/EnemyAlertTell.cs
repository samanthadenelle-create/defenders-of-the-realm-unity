// =============================================================================
// EnemyAlertTell — a brief "!" that pops over an enemy the moment it spots the
// hero: the "you've been seen" tell (MGS / Souls style) so combat never starts
// as a blindside. Answers the owner's "know when they're assessing us" ask.
// -----------------------------------------------------------------------------
// Fire-and-forget: EnemyAlertTell.Flash(enemyTransform) spawns a self-destructing
// billboard that pops, rises, and fades. Code-built (TextMesh) — no art/audio
// asset required. A "!" is mirror-safe, so the simple face-camera billboard needs
// no flip. (An audio sting can layer on later via CoreServices.Audio.)
// =============================================================================

using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>Transient "!" alert popped over an enemy when it acquires the hero.</summary>
    [DisallowMultipleComponent]
    public sealed class EnemyAlertTell : MonoBehaviour
    {
        private const float Life = 0.85f;

        private Transform _follow;
        private float _headHeight;
        private TextMesh _text;
        private Camera _cam;
        private float _age;

        /// <summary>Pop a one-shot alert over <paramref name="over"/>'s head.</summary>
        public static void Flash(Transform over, float headHeight = 2.4f)
        {
            if (over == null) return;
            var go = new GameObject("EnemyAlertTell");
            go.transform.position = over.position + Vector3.up * headHeight;
            var tell = go.AddComponent<EnemyAlertTell>();
            tell._follow = over;
            tell._headHeight = headHeight;
            tell.Build();
        }

        private void Build()
        {
            _text = gameObject.AddComponent<TextMesh>();
            _text.text = "!";
            _text.fontSize = 64;
            _text.characterSize = 0.12f;
            _text.anchor = TextAnchor.MiddleCenter;
            _text.alignment = TextAlignment.Center;
            _text.color = new Color(1f, 0.82f, 0.12f, 1f);   // alert amber

            var mr = GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
        }

        private void LateUpdate()
        {
            _age += Time.deltaTime;
            float t = _age / Life;
            if (t >= 1f) { Destroy(gameObject); return; }

            // Follow the enemy's head, rise a little, pop, then fade in the last 40%.
            Vector3 basePos = (_follow != null ? _follow.position : transform.position) + Vector3.up * _headHeight;
            transform.position = basePos + Vector3.up * Mathf.SmoothStep(0f, 0.6f, t);

            float pop = 1f + Mathf.Sin(Mathf.Clamp01(t * 3f) * Mathf.PI) * 0.4f;
            transform.localScale = Vector3.one * pop;

            if (_text != null)
            {
                Color c = _text.color;
                c.a = 1f - Mathf.Clamp01((t - 0.6f) / 0.4f);
                _text.color = c;
            }

            // Billboard (face the camera). Robust camera lookup so it never reads edge-on.
            if (_cam == null || !_cam.isActiveAndEnabled) _cam = ResolveCamera();
            if (_cam != null)
            {
                Vector3 away = transform.position - _cam.transform.position;
                if (away.sqrMagnitude > 0.0001f)
                    transform.rotation = Quaternion.LookRotation(away.normalized, Vector3.up);
            }
        }

        private static Camera ResolveCamera()
        {
            var c = Camera.main;
            if (c != null) return c;
            var smc = SmartMobileCamera.Instance;
            if (smc != null) { var cc = smc.GetComponent<Camera>(); if (cc != null) return cc; }
            return Object.FindAnyObjectByType<Camera>();
        }
    }
}
