// =============================================================================
// EnemyHitReaction — the classic "blink red when struck" hit-flash. Combat juice.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WHAT IT DOES:
//   Briefly tints the enemy's mesh red on each non-lethal hit, then restores it.
//   This sits ON TOP of Enemy.cs's existing directional flinch + per-type hit
//   VFX (DEF-46) — it adds the missing "I'm connecting" read that flinch alone
//   doesn't give.
//
// DESIGN (deliberately self-contained + mobile-safe):
//   • Self-wires — collects its mesh/skinned renderers in Awake, so it needs NO
//     prefab authoring. Enemy.cs auto-adds it (EnsureHitReaction) and calls
//     Flash() from its hit branch.
//   • No material instancing — the tint is pushed through a MaterialPropertyBlock
//     (_BaseColor for URP Lit, _Color as a fallback), so it never allocates a
//     per-enemy material and never leaks one.
//   • No knockback — WO-84's knockback was CharacterController-based and bailed on
//     NavMeshAgent-driven enemies (which is all of them here), so it is omitted.
// =============================================================================

using System.Collections;
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// Flashes an enemy's renderers red for a few frames on hit. Auto-wired by
    /// <see cref="Enemy"/>; call <see cref="Flash"/> from the damage path.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyHitReaction : MonoBehaviour
    {
        [Tooltip("Tint applied to the mesh during a hit flash.")]
        [SerializeField] private Color _flashColor = new Color(1f, 0.25f, 0.25f, 1f);

        [Tooltip("How long the red tint holds before restoring (seconds).")]
        [SerializeField, Min(0.01f)] private float _flashDuration = 0.08f;

        private Renderer[] _renderers;
        private MaterialPropertyBlock _mpb;
        private Coroutine _routine;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId     = Shader.PropertyToID("_Color");

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();

            // Collect only solid mesh renderers — skip ParticleSystemRenderers and
            // anything else we shouldn't tint. SkinnedMeshRenderer covers rigged
            // enemy bodies; MeshRenderer covers static props on the rig.
            var all = GetComponentsInChildren<Renderer>(true);
            var keep = new System.Collections.Generic.List<Renderer>(all.Length);
            foreach (var r in all)
                if (r is SkinnedMeshRenderer || r is MeshRenderer) keep.Add(r);
            _renderers = keep.ToArray();
        }

        /// <summary>Blink the mesh red, then restore. Safe to spam — re-triggers cleanly.</summary>
        public void Flash()
        {
            if (_renderers == null || _renderers.Length == 0) return;
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(FlashRoutine());
        }

        private IEnumerator FlashRoutine()
        {
            SetTint(_flashColor);
            yield return new WaitForSeconds(_flashDuration);
            ClearTint();
            _routine = null;
        }

        private void SetTint(Color c)
        {
            foreach (var r in _renderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(BaseColorId, c);
                _mpb.SetColor(ColorId, c);   // legacy/standard shader fallback
                r.SetPropertyBlock(_mpb);
            }
        }

        private void ClearTint()
        {
            // Passing null clears the override block → renderer reverts to the
            // material's own colours (no stored original needed).
            foreach (var r in _renderers)
                if (r != null) r.SetPropertyBlock(null);
        }

        private void OnDisable()
        {
            // If disabled mid-flash (e.g. on death), make sure we don't leave the
            // tint stuck on a renderer that gets pooled/reused.
            if (_routine != null) { StopCoroutine(_routine); _routine = null; }
            if (_renderers != null) ClearTint();
        }
    }
}
