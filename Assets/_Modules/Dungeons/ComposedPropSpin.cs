// =============================================================================
// ComposedPropSpin -- the slow spin + bob on a composed-dungeon pickup body.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Dungeons   Namespace: DeNelle.Dungeons
//
// ONE MonoBehaviour PER FILE, NAMED FOR THE FILE. That rule is not style here: it
// cost this module a shipped defect. ComposedKeyPickup and ComposedLockedPort were
// split out of a shared ComposedKeyLock.cs precisely because Unity binds a
// serialized MonoBehaviour to its script asset BY FILE NAME, and every baked key
// silently failed to deserialize while the bake still reported saved=True. This
// component is only ever added at runtime, but it lives in its own file anyway so
// nobody has to re-derive that reasoning if it is ever baked.
//
// Motion is the colourblind-safe half of "that is a pickup": a key that turns and
// bobs is legible without relying on its brass tint at all.
// =============================================================================

using UnityEngine;

namespace DeNelle.Dungeons
{
    /// <summary>Spins and bobs a pickup's visual body in place.</summary>
    [DisallowMultipleComponent]
    public sealed class ComposedPropSpin : MonoBehaviour
    {
        [SerializeField] private float _degreesPerSecond = 70f;
        [SerializeField] private float _bobAmplitude = 0.12f;
        [SerializeField] private float _bobHz = 0.5f;

        private Vector3 _baseLocalPos;
        private float _phase;

        /// <summary>Runtime wiring (ComposedPropVisuals is the only caller).</summary>
        public void Configure(float degreesPerSecond, float bobAmplitude, float bobHz)
        {
            _degreesPerSecond = degreesPerSecond;
            _bobAmplitude = Mathf.Max(0f, bobAmplitude);
            _bobHz = Mathf.Max(0f, bobHz);
        }

        private void Start()
        {
            _baseLocalPos = transform.localPosition;
            // Desynchronise multiple keys in one room so they do not pulse in lockstep.
            _phase = Random.value * Mathf.PI * 2f;
        }

        private void Update()
        {
            transform.Rotate(Vector3.up, _degreesPerSecond * Time.deltaTime, Space.Self);
            if (_bobAmplitude <= 0f) return;
            float y = Mathf.Sin(_phase + Time.time * _bobHz * Mathf.PI * 2f) * _bobAmplitude;
            transform.localPosition = _baseLocalPos + new Vector3(0f, y, 0f);
        }
    }
}
