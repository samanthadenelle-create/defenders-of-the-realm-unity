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

        // ⛔ THE FLASH MUST RESTORE WHAT IT FOUND, NOT WIPE IT (2026-08-20).
        // ClearTint used to call SetPropertyBlock(null), which does not mean "undo my
        // flash" — it means "drop EVERY property override on this renderer, whoever set
        // it". Nothing else overrode enemy colour until EnemyBodyColorGuard started
        // repainting textureless white/grey bodies with their family tint, and the very
        // first hit would then have wiped that repair and put the enemy back to white.
        // So snapshot the pre-flash block per renderer and re-apply THAT. Strictly more
        // correct than the null even with no guard present: an empty snapshot restores
        // exactly what null did.
        private MaterialPropertyBlock[] _restBlocks;
        private bool _flashing;

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
            // Snapshot the resting overrides ONCE PER FLASH, and only when a flash is not
            // already running. Flash() StopCoroutine()s the previous routine BEFORE ClearTint
            // has run, so a re-entrant hit (spam-clicking a mob) would otherwise capture the
            // RED flash colour as the resting colour and leave the enemy permanently red.
            bool capture = !_flashing;
            _flashing = true;
            if (_restBlocks == null || _restBlocks.Length != _renderers.Length)
                _restBlocks = new MaterialPropertyBlock[_renderers.Length];

            for (int i = 0; i < _renderers.Length; i++)
            {
                var r = _renderers[i];
                if (r == null) continue;

                // SetTint runs exactly once per flash (FlashRoutine's first line), so capturing
                // here IS "once per flash". Reuse the block instances — allocating one per hit
                // would be per-swing GC churn on mobile, which is the very thing the
                // MaterialPropertyBlock approach exists to avoid.
                if (capture)
                {
                    if (_restBlocks[i] == null) _restBlocks[i] = new MaterialPropertyBlock();
                    r.GetPropertyBlock(_restBlocks[i]);   // empty when nothing has overridden this renderer
                }

                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(BaseColorId, c);
                _mpb.SetColor(ColorId, c);   // legacy/standard shader fallback
                r.SetPropertyBlock(_mpb);
            }
        }

        private void ClearTint()
        {
            // Restore the PRE-FLASH block instead of nuking every override on the renderer.
            // See the _restBlocks note above: SetPropertyBlock(null) also discarded
            // EnemyBodyColorGuard's repaint of a textureless body, so one hit turned a
            // repaired enemy back into the white/grey one the owner reported on 2026-08-20.
            _flashing = false;
            if (_renderers == null) return;
            for (int i = 0; i < _renderers.Length; i++)
            {
                var r = _renderers[i];
                if (r == null) continue;
                if (_restBlocks != null && i < _restBlocks.Length && _restBlocks[i] != null)
                    r.SetPropertyBlock(_restBlocks[i]);
                else
                    r.SetPropertyBlock(null);
            }
            // Snapshots are NOT dropped: SetTint re-captures into the same instances on the
            // next flash, so a colour the guard changed in the meantime is still picked up
            // and no block is allocated per hit.
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
