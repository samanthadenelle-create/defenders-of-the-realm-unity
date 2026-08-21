using System.Collections.Generic;
using DeNelle.Core.Diagnostics;
using DeNelle.Village;
using UnityEngine;

namespace DeNelle.Dungeons.RoomForge
{
    public enum CommonDoorPolicy { Proximity, Interaction, Locked }

    /// <summary>Shared runtime visual, blocker, animation and traversal for a Door socket.</summary>
    [DisallowMultipleComponent]
    public sealed class CommonDungeonDoor : MonoBehaviour
    {
        private const float OpenDistance = 2.6f;
        private const float CloseDistance = 4.2f;
        private const float OpenAngle = 100f;
        private const float DegreesPerSecond = 240f;
        private const int PromptPriority = 60;
        private static readonly HashSet<string> ClaimedConnections = new HashSet<string>();

        private RoomSocket _socket;
        private Transform _hero;
        private Transform _hinge;
        private Collider _blocker;
        private CommonDoorPolicy _policy;
        private bool _open;
        private float _angle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetClaims() => ClaimedConnections.Clear();

        public void Configure(RoomSocket socket)
        {
            _socket = socket;
            _policy = socket != null ? socket.doorPolicy : CommonDoorPolicy.Proximity;
        }

        private void Start()
        {
            if (_socket == null) _socket = GetComponent<RoomSocket>();
            if (_socket == null || _socket.type != RoomSocketType.Door || !_socket.commonDoor) { enabled = false; return; }
            string key = string.IsNullOrEmpty(_socket.matedTo)
                ? $"{gameObject.scene.handle}:{transform.position.x:0.00}:{transform.position.y:0.00}:{transform.position.z:0.00}"
                : _socket.matedTo;
            if (!ClaimedConnections.Add(key)) { enabled = false; return; }
            BuildDoor();
            FlowTrace.Step("DungeonDoor", $"common door ready socket='{_socket.id}' connection='{key}' policy={_policy}.");
        }

        private void BuildDoor()
        {
            float halfWidth = Mathf.Max(0.75f, _socket.halfWidth);
            var hingeGo = new GameObject("CommonDoor_Hinge");
            _hinge = hingeGo.transform;
            _hinge.SetParent(transform, false);
            _hinge.localPosition = new Vector3(-halfWidth, 0f, 0f);
            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.name = "CommonDoor_Slab";
            slab.transform.SetParent(_hinge, false);
            slab.transform.localPosition = new Vector3(halfWidth, 1.2f, 0f);
            slab.transform.localScale = new Vector3(halfWidth * 2f, 2.4f, 0.16f);
            _blocker = slab.GetComponent<Collider>();
            var renderer = slab.GetComponent<Renderer>();
            if (renderer != null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader != null)
                {
                    var mat = new Material(shader) { name = "CommonDungeonDoor_Wood" };
                    mat.color = new Color(0.20f, 0.105f, 0.045f, 1f);
                    renderer.sharedMaterial = mat;
                }
            }
        }

        private void Update()
        {
            if (_hinge == null) return;
            if (_hero == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) _hero = player.transform;
                if (_hero == null) return;
            }
            float distance = Vector3.Distance(_hero.position, transform.position);
            if (_policy == CommonDoorPolicy.Proximity && distance <= OpenDistance) SetOpen(true);
            else if (_policy == CommonDoorPolicy.Proximity && distance >= CloseDistance) SetOpen(false);
            if (distance <= OpenDistance && _policy != CommonDoorPolicy.Proximity)
            {
                string label = _policy == CommonDoorPolicy.Locked ? "Locked" : "Open Door";
                MobileInteractButton.Request(this, label,
                    _policy == CommonDoorPolicy.Locked ? (System.Action)(() => { }) : () => SetOpen(true),
                    PromptPriority);
            }
            float target = _open ? OpenAngle : 0f;
            _angle = Mathf.MoveTowards(_angle, target, DegreesPerSecond * Time.deltaTime);
            _hinge.localRotation = Quaternion.Euler(0f, _angle, 0f);
        }

        private void SetOpen(bool open)
        {
            if (_open == open) return;
            _open = open;
            if (_blocker != null) _blocker.enabled = !open;
            FlowTrace.Step("DungeonDoor", $"door '{name}' {(open ? "OPEN" : "CLOSED")} freeTraversal={open}.");
        }

        private void OnDisable() => MobileInteractButton.Release(this);
    }
}
