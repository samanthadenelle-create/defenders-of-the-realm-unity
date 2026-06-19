// =============================================================================
// DungeonStubReturn — sit on the exit pad of a stub dungeon scene; when the
// hero enters the trigger, route back to the village. Used by every
// secondary dungeon built via DungeonStubBuilder.
// -----------------------------------------------------------------------------
// The full Healer's Cottage dungeon has a richer exit flow (boss completion +
// reward summary + scripted Bryn line). The stub doesn't — touching the pad
// just sends the player home. Once a stub gets promoted into a fully
// authored scene, this component is removed.
// =============================================================================

using Cysharp.Threading.Tasks;
using DeNelle.Core;
using UnityEngine;

namespace DeNelle.Dungeons
{
    [DisallowMultipleComponent]
    public sealed class DungeonStubReturn : MonoBehaviour
    {
        private bool _firing;

        private void OnTriggerEnter(Collider other)
        {
            if (_firing) return;
            // Any non-static body counts as the hero in stub scenes —
            // the player is the only mobile collider in the room.
            if (other.attachedRigidbody == null && !other.CompareTag("Player") && other.gameObject.layer == 0)
            {
                // The stub hero placeholder is a Capsule on layer 0 with no
                // rigidbody. Match by name as a best-effort fallback.
                if (!other.gameObject.name.Contains("Hero") &&
                    !other.gameObject.name.Contains("hero") &&
                    !other.gameObject.name.Contains("Player"))
                    return;
            }
            _firing = true;
            Debug.Log("[DungeonStubReturn] Exit pad hit — returning to village.");
            try { SceneRouter.GoVillage(); }
            catch (System.Exception ex)
            {
                Debug.LogError("[DungeonStubReturn] GoVillage threw: " + ex);
                _firing = false;
            }
        }

        // The 'F' force-return key was REMOVED — the OnTriggerEnter proximity path
        // is the sole return mechanism (touching the exit pad sends the player home).
    }
}
