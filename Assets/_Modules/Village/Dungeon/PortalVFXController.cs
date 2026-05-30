using System.Collections;
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// Attach to any dungeon portal GameObject. Drives idle vortex VFX,
    /// activation VFX on approach, and entry/exit burst.
    /// </summary>
    public class PortalVFXController : MonoBehaviour
    {
        [Header("Particle Systems")]
        [Tooltip("Looping swirling vortex — plays when portal is active.")]
        public ParticleSystem vortexParticles;
        [Tooltip("One-shot burst played when hero steps through.")]
        public ParticleSystem entryBurstParticles;

        [Header("Light")]
        public Light portalLight;
        [Range(0.5f, 5f)] public float idleLightIntensity   = 1.8f;
        [Range(1f, 8f)]   public float activeLightIntensity = 4.5f;

        [Header("Glow Plane")]
        [Tooltip("Optional additive quad inside the portal arch for interior glow.")]
        public MeshRenderer glowPlane;
        public Color idleGlowColor   = new Color(0.3f, 0f, 0.8f, 0.4f);
        public Color activeGlowColor = new Color(0.6f, 0.2f, 1f, 0.9f);

        [Header("Transition")]
        public float activationRadius = 4f;
        public float flashDuration    = 0.22f;

        private bool _active = false;
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        private void Start()
        {
            if (vortexParticles != null) vortexParticles.Play();
            if (portalLight    != null) portalLight.intensity = idleLightIntensity;
            SetGlowColor(idleGlowColor);
        }

        public void OnHeroApproach()
        {
            if (_active) return;
            _active = true;
            StartCoroutine(ActivateRoutine());
        }

        public void OnHeroEnter()
        {
            entryBurstParticles?.Play();
            // Reconciled to the real APIs: VFXManager.Play is static; the project's
            // camera shake is CameraShakeBridge.Shake(intensity, duration) (there is
            // no CameraShakeManager/ShakeTier). Medium tier ≈ 0.3 intensity / 0.3s.
            VFXManager.Play(VFXType.Portal_Enter, transform.position);
            CameraShakeBridge.Shake(0.3f, 0.3f);
            StartCoroutine(ScreenFlashRoutine());
        }

        public void OnHeroExit()
        {
            entryBurstParticles?.Play();
            VFXManager.Play(VFXType.Portal_Exit, transform.position);
        }

        private IEnumerator ActivateRoutine()
        {
            float elapsed = 0f, rampTime = 0.5f;
            while (elapsed < rampTime)
            {
                float t = elapsed / rampTime;
                if (portalLight != null)
                    portalLight.intensity = Mathf.Lerp(idleLightIntensity, activeLightIntensity, t);
                SetGlowColor(Color.Lerp(idleGlowColor, activeGlowColor, t));
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        private IEnumerator ScreenFlashRoutine()
        {
            var flash = GameObject.FindWithTag("ScreenFlash");
            if (flash == null) yield break;
            var img = flash.GetComponent<UnityEngine.UI.Image>();
            if (img == null) yield break;
            img.color = Color.white;
            float elapsed = 0f;
            while (elapsed < flashDuration)
            {
                img.color = Color.Lerp(Color.white, Color.clear, elapsed / flashDuration);
                elapsed += Time.deltaTime;
                yield return null;
            }
            img.color = Color.clear;
        }

        private void SetGlowColor(Color c)
        {
            if (glowPlane == null) return;
            var mat = glowPlane.material;
            mat.color = c;
            mat.SetColor(EmissionColor, c * 2f);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.5f, 0f, 1f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, activationRadius);
        }
    }
}
