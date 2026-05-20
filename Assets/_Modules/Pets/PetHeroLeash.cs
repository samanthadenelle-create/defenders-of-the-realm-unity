// =============================================================================
// PetHeroLeash — keeps the pet's HomePost anchored to a slot trailing the hero
// so every deployed pet drifts toward (and around) the player as they roam the
// village.
// -----------------------------------------------------------------------------
// Owner direction 2026-05-20: "focus on pets as i cant see them and should be
// on auto follow hero". The deploy slot ringing the Heart was 15+ metres from
// the spawn position, so the wider camera never framed the pets. Pet.cs itself
// only chases enemies — in Defend mode it returns to HomePost when the field
// is clear. So we just re-anchor HomePost to a leash point behind the hero
// every frame; the pet's existing kinematic drifter does the rest.
//
// Cross-module note: DeNelle.Pets cannot reference DeNelle.Village (asmdef
// isolation), so HeroLocomotion is resolved by reflection — name-matched once,
// cached, refreshed on scene reload.
// =============================================================================

using System;
using System.Reflection;
using UnityEngine;

namespace DeNelle.Pets
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Pet))]
    public sealed class PetHeroLeash : MonoBehaviour
    {
        private const float ResolveRetrySeconds = 1.0f;
        private const float OrbitRadius = 2.2f;     // ring radius around hero
        private const float HeightAboveGround = 0f;

        private Pet _pet;
        private Transform _heroT;
        private float _resolveTimer;
        private int _slotSeed;

        private static Type s_heroType;

        private void Awake()
        {
            _pet = GetComponent<Pet>();
            // A stable per-pet seed so two pets don't crowd the same orbit slot.
            _slotSeed = (gameObject.GetInstanceID() & 0xFFFF);
        }

        private void Update()
        {
            if (_heroT == null)
            {
                _resolveTimer -= Time.deltaTime;
                if (_resolveTimer <= 0f)
                {
                    _resolveTimer = ResolveRetrySeconds;
                    _heroT = ResolveHeroTransform();
                }
                if (_heroT == null) return;
            }

            // Three pets fan out around the hero on an orbit; the orbit rotates
            // slowly so the pack reads as alive even when the hero is still.
            float angle = (Time.time * 18f + _slotSeed) * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(
                Mathf.Sin(angle) * OrbitRadius,
                HeightAboveGround,
                Mathf.Cos(angle) * OrbitRadius - 1.0f);   // slight bias behind
            Vector3 target = _heroT.position + offset;
            target.y = Mathf.Max(0f, target.y);
            _pet.SetHomePost(target);
        }

        private static Transform ResolveHeroTransform()
        {
            try
            {
                if (s_heroType == null)
                {
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        var t = asm.GetType("DeNelle.Village.HeroLocomotion", false);
                        if (t != null) { s_heroType = t; break; }
                    }
                }
                if (s_heroType == null) return null;
                var found = UnityEngine.Object.FindObjectOfType(s_heroType) as Component;
                return found != null ? found.transform : null;
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>Keeps a TextMesh facing the main camera (used for pet name tags).</summary>
    [DisallowMultipleComponent]
    internal sealed class PetNameTagBillboard : MonoBehaviour
    {
        private void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
        }
    }
}
