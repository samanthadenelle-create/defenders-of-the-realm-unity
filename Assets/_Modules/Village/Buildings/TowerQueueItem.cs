// =============================================================================
// TowerQueueItem — DEF-76 (Linear). One pending tower build in the construction
// queue: which TowerData to raise, and where. Own file in namespace
// DeNelle.Village (DEF-76 CP1 Issues 1 & 6 — must NOT be nested inside
// TowerConstructionQueue.cs).
// =============================================================================

using UnityEngine;
using DeNelle.Core.Data;

namespace DeNelle.Village
{
    /// <summary>A single queued tower build (data + world position).</summary>
    public class TowerQueueItem
    {
        public TowerData Data;
        public Vector3 Position;

        public TowerQueueItem(TowerData d, Vector3 p)
        {
            Data = d;
            Position = p;
        }
    }
}
