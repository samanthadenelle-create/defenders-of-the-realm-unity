using UnityEngine;

namespace DeNelle.Dungeons.RoomForge
{
    [DisallowMultipleComponent]
    public sealed class DungeonKitMovingPlatform : MonoBehaviour
    {
        [SerializeField] private Vector3 localA;
        [SerializeField] private Vector3 localB = Vector3.up * 4f;
        [SerializeField, Min(0.1f)] private float speed = 1.5f;
        [SerializeField, Min(0f)] private float dwellSeconds = 0.75f;
        private float _phase;
        private float _dwellUntil;
        private bool _towardB = true;

        public DungeonKitMovingPlatform Configure(Vector3 a, Vector3 b)
        {
            localA = a; localB = b; transform.localPosition = localA; return this;
        }

        private void FixedUpdate()
        {
            if (Time.time < _dwellUntil) return;
            float distance = Mathf.Max(0.01f, Vector3.Distance(localA, localB));
            _phase = Mathf.MoveTowards(_phase, _towardB ? 1f : 0f, speed * Time.fixedDeltaTime / distance);
            transform.localPosition = Vector3.Lerp(localA, localB, _phase);
            if ((_towardB && _phase >= 1f) || (!_towardB && _phase <= 0f))
            {
                _towardB = !_towardB;
                _dwellUntil = Time.time + dwellSeconds;
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision.transform.CompareTag("Player")) collision.transform.SetParent(transform, true);
        }

        private void OnCollisionExit(Collision collision)
        {
            if (collision.transform.CompareTag("Player") && collision.transform.parent == transform)
                collision.transform.SetParent(null, true);
        }
    }
}
