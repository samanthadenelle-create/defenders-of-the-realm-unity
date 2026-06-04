// =============================================================================
// AbilityCooldownUI — per-ability-button cooldown fill overlay (WO-97 Bug 3).
// -----------------------------------------------------------------------------
// Attach to the same GameObject as an ability button in the HUD. When an
// ability fires, the ability script calls GetComponent<AbilityCooldownUI>()
// ?.StartCooldown(cooldown) to drive a radial/fill sweep over the button.
//
// Requires a UnityEngine.UI.Image with fillMethod set (e.g. Radial360) and
// fillOrigin set up in the inspector to act as the cooldown overlay.
// =============================================================================

using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace DeNelle.Village
{
    /// <summary>
    /// Drives a <see cref="Image.fillAmount"/> sweep to visualise the cooldown
    /// remaining on an ability button. Called by ability scripts after each cast.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AbilityCooldownUI : MonoBehaviour
    {
        [Tooltip("The Image used as the cooldown fill overlay. " +
                 "Set fillMethod to Radial360 (or Linear) and fillOrigin in the inspector. " +
                 "When null, AbilityCooldownUI searches its own GameObject and children.")]
        public Image cooldownFill;

        private Coroutine _routine;

        private void Awake()
        {
            if (cooldownFill == null)
                cooldownFill = GetComponentInChildren<Image>(true);
        }

        /// <summary>
        /// Starts a cooldown sweep over <paramref name="duration"/> seconds.
        /// Calling while a sweep is running cancels the old one first.
        /// </summary>
        /// <param name="duration">Cooldown length in seconds. Values ≤ 0 are ignored.</param>
        public void StartCooldown(float duration)
        {
            if (cooldownFill == null || duration <= 0f) return;
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(CooldownRoutine(duration));
        }

        private IEnumerator CooldownRoutine(float duration)
        {
            cooldownFill.fillAmount = 1f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                cooldownFill.fillAmount = 1f - (elapsed / duration);
                yield return null;
            }
            cooldownFill.fillAmount = 0f;
            _routine = null;
        }
    }
}
