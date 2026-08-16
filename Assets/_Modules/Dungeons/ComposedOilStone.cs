// =============================================================================
// ComposedOilStone — bake-time marker for oil refill points in composed dungeons.
// -----------------------------------------------------------------------------
// WO-1001 slice 5. DungeonBaker places these; ComposedDungeonBootstrap collects
// them at load and hands DungeonOilStone data to Lantern.ConfigureStandalone.
// =============================================================================

using UnityEngine;

namespace DeNelle.Dungeons
{
    /// <summary>One oil refill point in a composed (Pipeline A) dungeon scene.</summary>
    [DisallowMultipleComponent]
    public sealed class ComposedOilStone : MonoBehaviour
    {
        [SerializeField] private string _id = "oil";
        [SerializeField] private float _radius = 2.5f;

        public string Id => string.IsNullOrEmpty(_id) ? name : _id;
        public float Radius => Mathf.Max(0.5f, _radius);

        public void Configure(string id, float radius)
        {
            _id = id ?? "oil";
            _radius = Mathf.Max(0.5f, radius);
        }

        // WO-1112: DungeonBaker.PlaceComposeOilStones bakes an empty GameObject + this marker,
        // no Renderer — so the lantern's ONLY refill point was invisible. That mattered even
        // when the flask emptied in 62.5s; now that the burn is tripled and the player is meant
        // to PLAN refills, a refill point you cannot see is not a decision at all.
        private void Start() => ComposedPropVisuals.BuildOilStone(gameObject);

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.45f);
            Gizmos.DrawWireSphere(transform.position, Radius);
        }
#endif
    }
}
