// =============================================================================
// HeroTargetIndicator — a camera-facing reticle billboard over the hero's
// current target, with manual target cycling, so open-world combat is readable
// and controllable (see what you'll hit, and switch which enemy that is).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Pairs with the open-world targeting fix (Enemy auto-carries the EnemyDamageable
// IDamageable adapter). Each scan it gathers the alive Hostile IDamageables in
// range (the same detection the hero's melee/ability sweeps use) and parks a ring
// billboard over the current target's head.
//
// TARGET LOCK / CYCLE: by default the reticle tracks the NEAREST hostile. Press
// Tab (keyboard) or the right shoulder (gamepad) to CYCLE to the next hostile in
// range — that manual lock turns the reticle red and is fed to
// HeroAbilities.AimPointOverride so ranged spells aim at it. The lock auto-clears
// (back to nearest) when the target dies or leaves range. Self-installed on the
// hero by HeroControlEnsurer — no scene edit, no art asset (ring drawn at runtime).
//
// The transparent-material setup mirrors PetDeployer.BuildSpriteBillboard, which
// is proven to render in WebGL builds (URP/Unlit transparent, double-sided).
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using DeNelle.Core.Combat;

namespace DeNelle.Village
{
    /// <summary>
    /// Shows a camera-facing reticle over the hero's current hostile target and
    /// lets the player cycle targets. Attach to the hero root (HeroControlEnsurer
    /// does this automatically).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HeroTargetIndicator : MonoBehaviour
    {
        [Tooltip("Radius (m) within which hostiles can be targeted.")]
        [SerializeField, Min(1f)] private float _acquireRange = 18f;

        [Tooltip("Seconds between target re-scans (the reticle follows every frame).")]
        [SerializeField, Min(0.02f)] private float _scanInterval = 0.12f;

        [Tooltip("Height (m) above the target's position to float the reticle.")]
        [SerializeField] private float _headHeight = 2.2f;

        [Tooltip("Reticle world size (m).")]
        [SerializeField, Min(0.1f)] private float _size = 1.2f;

        [Tooltip("Reticle tint when auto-tracking the nearest hostile.")]
        [SerializeField] private Color _autoTint = new Color(1f, 0.88f, 0.30f, 0.95f);

        [Tooltip("Reticle tint when the player has manually locked a target.")]
        [SerializeField] private Color _lockTint = new Color(1f, 0.32f, 0.28f, 0.98f);

        /// <summary>The hostile the hero is currently targeting (locked or nearest), or null.</summary>
        public IDamageable CurrentTarget { get; private set; }

        private Transform _reticle;
        private Material _reticleMat;
        private Camera _cam;
        private HeroAbilities _abilities;
        private float _nextScan;

        private IDamageable _locked;   // manual lock (null = auto-nearest)
        private readonly List<IDamageable> _candidates = new List<IDamageable>();

        private static readonly Collider[] _hits = new Collider[64];
        private static Texture2D _ringTex;

        private void Awake()
        {
            BuildReticle();
            _abilities = GetComponent<HeroAbilities>();
        }

        private void OnDestroy()
        {
            if (_reticle != null) Destroy(_reticle.gameObject);
            // Don't leave a stale aim override on the abilities component.
            if (_abilities != null) _abilities.AimPointOverride = null;
        }

        private void Update()
        {
            if (CyclePressed()) CycleTarget();
        }

        private void LateUpdate()
        {
            if (Time.time >= _nextScan)
            {
                _nextScan = Time.time + _scanInterval;
                RebuildCandidates();
            }

            // Drop a manual lock that died or wandered out of range.
            if (_locked != null && (!_locked.IsAlive || !_candidates.Contains(_locked)))
                _locked = null;

            CurrentTarget = _locked ?? NearestCandidate();

            // Feed a manual lock to the ability aim so spells hit it (null = village default = nearest).
            if (_abilities == null) _abilities = GetComponent<HeroAbilities>();
            if (_abilities != null)
                _abilities.AimPointOverride = _locked != null ? (Vector3?)_locked.WorldPosition : null;

            if (CurrentTarget == null || !CurrentTarget.IsAlive)
            {
                SetVisible(false);
                return;
            }

            SetVisible(true);
            if (_cam == null) _cam = Camera.main;
            if (_reticleMat != null)
            {
                Color want = _locked != null ? _lockTint : _autoTint;
                if (_reticleMat.HasProperty("_BaseColor")) _reticleMat.SetColor("_BaseColor", want);
                _reticleMat.color = want;
            }

            Vector3 p = CurrentTarget.WorldPosition + Vector3.up * _headHeight;
            _reticle.position = p;
            if (_cam != null)
            {
                Vector3 toCam = _cam.transform.position - p;
                toCam.y = 0f;
                if (toCam.sqrMagnitude > 0.0001f)
                    _reticle.rotation = Quaternion.LookRotation(-toCam.normalized, Vector3.up);
            }
        }

        // ── Targeting ─────────────────────────────────────────────────────────

        private bool CyclePressed()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.tabKey.wasPressedThisFrame) return true;
            var gp = Gamepad.current;
            if (gp != null && gp.rightShoulder.wasPressedThisFrame) return true;
            if (UnityEngine.Input.GetKeyDown(KeyCode.Tab)) return true;
            return false;
        }

        private void CycleTarget()
        {
            RebuildCandidates();
            if (_candidates.Count == 0) { _locked = null; return; }
            int idx = _locked != null ? _candidates.IndexOf(_locked) : -1;
            idx = (idx + 1) % _candidates.Count;
            _locked = _candidates[idx];
        }

        private void RebuildCandidates()
        {
            _candidates.Clear();
            int n = Physics.OverlapSphereNonAlloc(
                transform.position, _acquireRange, _hits, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                var c = _hits[i];
                if (c == null) continue;
                var d = c.GetComponentInParent<IDamageable>();
                if (d == null || !d.IsAlive || d.Faction != CombatFaction.Hostile) continue;
                if (!_candidates.Contains(d)) _candidates.Add(d);
            }
            // Stable nearest-first order so Tab cycles outward predictably.
            Vector3 me = transform.position;
            _candidates.Sort((a, b) =>
                (a.WorldPosition - me).sqrMagnitude.CompareTo((b.WorldPosition - me).sqrMagnitude));
        }

        private IDamageable NearestCandidate()
            => _candidates.Count > 0 ? _candidates[0] : null;

        private void SetVisible(bool on)
        {
            if (_reticle != null && _reticle.gameObject.activeSelf != on)
                _reticle.gameObject.SetActive(on);
        }

        // ── Visual build (no art asset; runtime ring texture + transparent quad) ──

        private void BuildReticle()
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "HeroTargetReticle";
            var col = quad.GetComponent<Collider>();
            if (col != null) Destroy(col);
            quad.transform.localScale = new Vector3(_size, _size, _size);

            var mr = quad.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                                ?? Shader.Find("Sprites/Default")
                                ?? Shader.Find("Unlit/Transparent");
                if (shader != null)
                {
                    var mat = new Material(shader);
                    Texture2D ring = RingTexture();
                    if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", ring);
                    if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", ring);
                    mat.mainTexture = ring;
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", _autoTint);
                    mat.color = _autoTint;

                    // Transparent blend — mirrors PetDeployer.BuildSpriteBillboard (WebGL-safe).
                    if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
                    if (mat.HasProperty("_Blend"))   mat.SetFloat("_Blend", 0f);
                    if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    if (mat.HasProperty("_ZWrite"))   mat.SetInt("_ZWrite", 0);
                    if (mat.HasProperty("_Cull"))     mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                    mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    mat.EnableKeyword("_ALPHABLEND_ON");
                    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                    mr.sharedMaterial = mat;
                    _reticleMat = mr.material;   // instance we recolour per lock state
                }
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }

            _reticle = quad.transform;
            _reticle.gameObject.SetActive(false);
        }

        /// <summary>A cached 64×64 soft ring (target-bracket) texture, drawn once.</summary>
        private static Texture2D RingTexture()
        {
            if (_ringTex != null) return _ringTex;

            const int S = 64;
            var t = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float c = (S - 1) * 0.5f;
            const float outer = 30f, inner = 23f;
            var clear = new Color(1f, 1f, 1f, 0f);
            for (int y = 0; y < S; y++)
            {
                for (int x = 0; x < S; x++)
                {
                    float dx = x - c, dy = y - c;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float band = Mathf.InverseLerp(outer, outer - 2f, r) * Mathf.InverseLerp(inner, inner + 2f, r);
                    t.SetPixel(x, y, band > 0f ? new Color(1f, 1f, 1f, Mathf.Clamp01(band)) : clear);
                }
            }
            t.Apply();
            _ringTex = t;
            return t;
        }
    }
}
