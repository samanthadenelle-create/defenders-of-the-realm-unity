// =============================================================================
// GateProximityOpener — WO-08: open a gate's force field when the HERO walks up,
// close it when the hero leaves. Additive on top of Gate's existing HP-derived
// collapse — enemies never trigger it (the damage mechanic is untouched).
// -----------------------------------------------------------------------------
// Physics notes (this codebase, verified WO-08):
//   * The hero root carries a SOLID CapsuleCollider and NO Rigidbody — it moves
//     by pure transform (HeroLocomotion). For trigger events to fire against a
//     transform-moved, RB-less collider, the TRIGGER side must own a Rigidbody.
//     So the proximity zone gets a kinematic Rigidbody.
//   * The zone lives on a CHILD GameObject (the Gate root already owns the solid
//     blocker BoxCollider; a second collider there would muddy that). Unity sends
//     OnTriggerEnter/Exit to the collider's own GameObject, so a tiny relay on
//     the child forwards the events back to this opener.
//   * Hero identity is the HeroLocomotion component (per the WO — not a tag/layer,
//     which are less stable in this project).
// =============================================================================

using UnityEngine;
using DeNelle.Core.Quests;

namespace DeNelle.Village
{
    /// <summary>
    /// Sits alongside <see cref="Gate"/>. Builds a child trigger sphere that
    /// calls <see cref="Gate.RequestOpen"/> when the hero enters and
    /// <see cref="Gate.RequestClose"/> when the hero leaves. Hero-only.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Gate))]
    public sealed class GateProximityOpener : MonoBehaviour
    {
        // WO-158: bumped 4→8 m. The drawbridge sits OUTSIDE the moat (~6 m past the
        // gate line), so a 4 m radius opened the gate too late — the hero hit the
        // blocker before triggering RequestOpen, reading as "gate won't let me out."
        // 8 m opens the gate as the hero steps onto the approach/drawbridge.
        [Tooltip("Radius (world units) within which the hero opens this gate. ~8m so the gate opens before the hero reaches the blocker across the drawbridge.")]
        [SerializeField, Range(1f, 12f)] private float _proximityRadius = 8f;

        private Gate _gate;
        private int _heroesInside; // ref-count: robust to multi-collider heroes / re-entry

        private void Awake()
        {
            _gate = GetComponent<Gate>();
            // WO-158: openers baked into Village.unity before this change froze
            // _proximityRadius at 4 (too small — gate opened after the hero hit the
            // blocker). Floor it to 8 at load so baked + new openers behave the same
            // without needing a rebake.
            if (_proximityRadius < 8f) _proximityRadius = 8f;
            EnsureTrigger();
        }

        private void EnsureTrigger()
        {
            const string TriggerName = "ProximityTrigger";
            Transform existing = transform.Find(TriggerName);
            GameObject triggerGo = existing != null
                ? existing.gameObject
                : new GameObject(TriggerName);

            if (existing == null)
            {
                triggerGo.transform.SetParent(transform, false);
                triggerGo.transform.localPosition = Vector3.zero;
            }
            // Default layer (0) collides with everything in the stock physics
            // matrix — keeps the zone detecting the hero regardless of the gate's
            // own wall layer.
            triggerGo.layer = 0;

            var sphere = triggerGo.GetComponent<SphereCollider>();
            if (sphere == null) sphere = triggerGo.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = _proximityRadius;
            sphere.center = new Vector3(0f, 1f, 0f); // hero capsule centre height

            // Kinematic RB so trigger events fire against the RB-less hero.
            var rb = triggerGo.GetComponent<Rigidbody>();
            if (rb == null) rb = triggerGo.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var relay = triggerGo.GetComponent<GateProximityTriggerRelay>();
            if (relay == null) relay = triggerGo.AddComponent<GateProximityTriggerRelay>();
            relay.Bind(this);
        }

        /// <summary>Called by the relay when a hero collider enters the zone.</summary>
        internal void OnHeroEntered()
        {
            _heroesInside++;
            if (_heroesInside == 1)
            {
                _gate.RequestOpen();
                Debug.Log($"[GateProximityOpener] {_gate.GateId} opened on hero approach.");
                // WO-558: exploration daily-quest progress — one tick per fresh gate approach.
                DailyQuestService.Instance?.Report("explore.visit-gate", 1);
            }
        }

        /// <summary>Called by the relay when a hero collider leaves the zone.</summary>
        internal void OnHeroExited()
        {
            _heroesInside = Mathf.Max(0, _heroesInside - 1);
            if (_heroesInside == 0)
            {
                _gate.RequestClose();
                Debug.Log($"[GateProximityOpener] {_gate.GateId} closed on hero departure.");
            }
        }
    }

    /// <summary>
    /// Trigger relay living on the proximity-sphere child. Forwards hero-only
    /// trigger events up to the parent <see cref="GateProximityOpener"/> (Unity
    /// delivers OnTrigger* to the collider's own GameObject, not the parent).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GateProximityTriggerRelay : MonoBehaviour
    {
        private GateProximityOpener _opener;

        internal void Bind(GateProximityOpener opener) => _opener = opener;

        private void OnTriggerEnter(Collider other)
        {
            if (_opener == null || other == null) return;
            if (other.GetComponentInParent<HeroLocomotion>() != null)
                _opener.OnHeroEntered();
        }

        private void OnTriggerExit(Collider other)
        {
            if (_opener == null || other == null) return;
            if (other.GetComponentInParent<HeroLocomotion>() != null)
                _opener.OnHeroExited();
        }
    }
}
