// =============================================================================
// EnemyPlaceholderReveal — holds the placeholder capsule INVISIBLE for a short grace
// window, so the fast case never shows the owner a pill at all.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village (Village/Enemies). Attached by EnemyFactory.AddCapsuleFallback
// to the capsule itself — NOT to the enemy root, and NOT by EnemyLateSkinner.
//
// ⛔ WHY IT LIVES ON THE CAPSULE (owner report 2026-08-20: "One enemy came in as a pill
// then switched to enemy"). EnemyLateSkinner is armed only when a re-skin is POSSIBLE:
// it returns early for a proven-absent address (EnemyLateSkinner.Arm, the IsKnownAbsent
// / !IsRegisteredAddress guards). If the grace lived there, a genuinely-missing model
// would spawn a capsule that is never armed, never revealed, and never replaced — an
// INVISIBLE-BUT-HITTABLE enemy, which is the precise failure EnemyFactory's render-verify
// exists to prevent. On the capsule, the reveal is unconditional: worst case the player
// waits GraceSeconds and then sees the placeholder, exactly as before this component.
//
// WHAT IT BUYS, measured against the captured session (logs/device/enemy-color.log, pid 6783):
//   Skeleton_Rogue re-skinned 0.6s after spawn, Troll 2.2s, Skeleton_Minion 2.1s,
//   Orc_Warrior 6.4s. The grace erases the pill outright for every arrival inside it and
//   trims the visible pill time off every other one.
//
// ⛔ IT CANNOT REMOVE THE PILL ENTIRELY, and pretending otherwise would be the bug. The
// window is CDN fetch latency for the family bundle, and the fetch already starts on the
// same frame the model id first exists (EnemyAssetLoader.LoadEnemyPrefab requests the
// family on the miss that produced the capsule). Nothing on the runtime path can make a
// 6.4s download finish sooner. Only pre-downloading the family BEFORE the encounter —
// or shipping enemy art locally — closes the remaining window, and neither is a runtime
// change. Waiting is not an option: blocking here is what deadlocked the device on
// 2026-08-20 (see EnemyContentWarmer.cs's header).
// =============================================================================

using DeNelle.Core.Diagnostics;
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// Keeps a placeholder capsule's renderer off for <see cref="GraceSeconds"/>, then reveals
    /// it (and fades the reveal in over <see cref="FadeSeconds"/> by growing it into place, so
    /// a late arrival does not POP). Self-destructs once the capsule is visible.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyPlaceholderReveal : MonoBehaviour
    {
        /// <summary>
        /// How long the placeholder stays hidden. Sized from the captured arrival times: the
        /// fastest observed re-skin was 0.6s, so a longer grace would start hiding real bodies
        /// that already exist, and a much shorter one buys nothing. Deliberately well under the
        /// point where a missing body would read as "the enemy is invisible" — the enemy's
        /// collider, agent and brain are live throughout, this hides only the stand-in mesh.
        /// </summary>
        public const float GraceSeconds = 0.5f;

        /// <summary>Grow-in time once the capsule IS revealed, so the placeholder arrives
        /// instead of popping. Short enough to stay honest about what the player is seeing.</summary>
        public const float FadeSeconds = 0.18f;

        private Renderer _renderer;
        private Vector3 _targetScale;
        private float _hiddenUntil;
        private float _revealedAt = -1f;

        /// <summary>Hide <paramref name="capsule"/>'s renderer and schedule its reveal.</summary>
        internal static void Arm(GameObject capsule)
        {
            if (capsule == null) return;
            var reveal = capsule.GetComponent<EnemyPlaceholderReveal>();
            if (reveal == null) reveal = capsule.AddComponent<EnemyPlaceholderReveal>();
            reveal.Begin();
        }

        private void Begin()
        {
            _renderer = GetComponent<Renderer>();
            _targetScale = transform.localScale;
            _hiddenUntil = Time.realtimeSinceStartup + GraceSeconds;
            _revealedAt = -1f;
            if (_renderer != null) _renderer.enabled = false;

            FlowTrace.Once("Enemy", "placeholder-grace",
                $"placeholder capsules are held INVISIBLE for {GraceSeconds:0.00}s before they are shown. " +
                "A family bundle that lands inside that window is swapped in with NO pill ever visible to the " +
                "player; a slower one still reveals the placeholder, because an unrevealed capsule would be an " +
                "invisible-but-hittable enemy. This shrinks the pill window — the download itself is what sets it.");
        }

        private void Update()
        {
            float now = Time.realtimeSinceStartup;

            if (_revealedAt < 0f)
            {
                if (now < _hiddenUntil) return;
                _revealedAt = now;
                if (_renderer != null) _renderer.enabled = true;
                FlowTrace.Once("Enemy", "placeholder-revealed",
                    $"a placeholder capsule outlived the {GraceSeconds:0.00}s grace and is now VISIBLE — its family " +
                    "bundle is still downloading. This is the residual pill the owner can see; the only way to remove " +
                    "it is to have the family resident BEFORE the encounter, which no runtime change can do.");
            }

            // Grow into place rather than popping. Scale, not alpha: the capsule uses an
            // OPAQUE URP/Lit material (EnemyFactory.TintCapsule) and switching a shared
            // opaque material to transparent for a placeholder is not worth a render-state
            // change on a body that exists to be thrown away.
            float t = FadeSeconds <= 0f ? 1f : Mathf.Clamp01((now - _revealedAt) / FadeSeconds);
            transform.localScale = Vector3.Lerp(_targetScale * 0.35f, _targetScale, t);
            if (t >= 1f)
            {
                transform.localScale = _targetScale;
                Destroy(this);
            }
        }
    }
}
