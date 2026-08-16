// =============================================================================
// ComposedTrapHazard — WO-1001 slice 7 lightweight floor traps for Pipeline A.
// -----------------------------------------------------------------------------
// Bake places a trigger volume; step → damage with a short re-arm cooldown.
// Colourblind-safe: shape is a flat disk gizmo + optional particle telegraph,
// not colour-only. Damage routes through HeroHealth.TakeDamage.
// =============================================================================

using DeNelle.Core.Diagnostics;
using DeNelle.Village;
using UnityEngine;

namespace DeNelle.Dungeons
{
    /// <summary>One step-on trap in a composed dungeon.</summary>
    [DisallowMultipleComponent]
    public sealed class ComposedTrapHazard : MonoBehaviour
    {
        private const string Sys = "ComposedTrap";

        [SerializeField] private string _id = "trap";
        [SerializeField] private string _kind = "spike"; // spike | grate
        [SerializeField] private float _damage = 12f;
        [SerializeField] private float _radius = 1.4f;
        [SerializeField] private float _rearmSeconds = 1.25f;

        private float _armedAt;
        private SphereCollider _col;

        public string Id => _id;
        public string Kind => _kind;

        public void Configure(string id, string kind, float damage, float radius)
        {
            _id = string.IsNullOrEmpty(id) ? "trap" : id;
            _kind = string.IsNullOrEmpty(kind) ? "spike" : kind;
            _damage = Mathf.Max(1f, damage);
            _radius = Mathf.Max(0.4f, radius);
            EnsureCollider();
        }

        private void Awake() => EnsureCollider();

        // WO-1112: the header above has always promised "an optional particle telegraph", and
        // the ONLY thing this component ever drew was the OnDrawGizmos below — inside
        // #if UNITY_EDITOR, so it renders in the editor and NOWHERE in a player build. The trap
        // was invisible in the shipped game and its damage read as unexplained. The runtime pad
        // is deliberately dull and flat (owner: "the dungeons should be confusing", "im not
        // trying to make them easy") — it rewards looking rather than handing traps away.
        private void Start() => ComposedPropVisuals.BuildTrapPad(gameObject, _radius,
            _kind != null && _kind.IndexOf("grate", System.StringComparison.OrdinalIgnoreCase) >= 0);

        private void EnsureCollider()
        {
            if (_col == null) _col = gameObject.GetComponent<SphereCollider>();
            if (_col == null) _col = gameObject.AddComponent<SphereCollider>();
            _col.isTrigger = true;
            _col.radius = _radius;
            // Flat-ish pad: keep centre near floor.
            _col.center = new Vector3(0f, 0.15f, 0f);
        }

        private void OnTriggerStay(Collider other)
        {
            if (Time.time < _armedAt) return;
            if (other == null) return;
            // Hero capsule / body
            var health = other.GetComponentInParent<HeroHealth>();
            if (health == null) return;

            _armedAt = Time.time + _rearmSeconds;
            health.TakeDamage(_damage);
            FlowTrace.Step(Sys,
                $"TRAP '{_id}' kind='{_kind}' hit hero for {_damage:0} @ {transform.position}");
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            // Shape telegraph (not colour-only): wire disk for spike, wire cube for grate.
            Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.55f);
            if (_kind != null && _kind.IndexOf("grate", System.StringComparison.OrdinalIgnoreCase) >= 0)
                Gizmos.DrawWireCube(transform.position + Vector3.up * 0.1f, new Vector3(_radius * 2f, 0.2f, _radius * 2f));
            else
                Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.15f, _radius);
        }
#endif
    }
}
