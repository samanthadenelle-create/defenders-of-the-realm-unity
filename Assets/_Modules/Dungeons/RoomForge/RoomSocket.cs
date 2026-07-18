// =============================================================================
// RoomSocket — authorable doorway / arch / stair socket on a room prefab root.
// -----------------------------------------------------------------------------
// Attach one component per opening. Room Forge + DungeonBaker mate sockets by
// id across rooms (door-touch-door hard gate). Unmated sockets can be sealed
// (wall) or marked secret (illusory / hidden room).
// =============================================================================

using UnityEngine;

namespace DeNelle.Dungeons.RoomForge
{
    /// <summary>
    /// One connection point on a room prefab. Transform is the socket origin
    /// (wall-centre facing outward); local pose is the transform itself.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoomSocket : MonoBehaviour
    {
        [Tooltip("Stable id within this room prefab, e.g. north_door_01.")]
        public string id = "socket_0";

        [Tooltip("Door / Arch / Stair — must be compatible with the mate.")]
        public RoomSocketType type = RoomSocketType.Door;

        [Tooltip("Cardinal hint for authoring (N/E/S/W). Bake uses transform.")]
        public string facing = "N";

        [Tooltip("When true, unmated seal may become a secret / illusory opening.")]
        public bool isSecret;

        [Tooltip("Bake-time / debug: mate connection id written by DungeonBaker.")]
        public string matedTo;

        [Tooltip("World-space half-width of the opening (KayKit door ~1–2u).")]
        public float halfWidth = 1f;

        /// <summary>World position of the socket origin.</summary>
        public Vector3 WorldPosition => transform.position;

        /// <summary>Outward normal (transform.forward) for alignment checks.</summary>
        public Vector3 Outward => transform.forward;

        /// <summary>True when baker has paired this socket.</summary>
        public bool IsMated => !string.IsNullOrEmpty(matedTo);

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Color c = type switch
            {
                RoomSocketType.Door => new Color(0.2f, 0.75f, 1f, 0.9f),
                RoomSocketType.Arch => new Color(0.3f, 1f, 0.4f, 0.9f),
                RoomSocketType.StairUp => new Color(1f, 0.85f, 0.2f, 0.9f),
                RoomSocketType.StairDown => new Color(1f, 0.45f, 0.15f, 0.9f),
                _ => Color.white,
            };
            Gizmos.color = c;
            Gizmos.DrawSphere(transform.position + Vector3.up * 0.5f, 0.25f);
            Gizmos.DrawRay(transform.position + Vector3.up * 0.5f, transform.forward * 1.5f);
        }
#endif
    }
}
