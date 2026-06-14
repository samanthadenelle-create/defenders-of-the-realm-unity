// =============================================================================
// TroopDeployer — minimal spawn entry point for friendly troops (WO-453 Step 1).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// The one-liner that turns a troop id into a deployed, fighting TroopController:
//   TroopCatalog.Find(id) -> TroopFactory.Build(...) -> SetEnemyMask(Enemy layer).
//
// Step-1 scope is combat only — there is NO deploy-point / rally / retreat UI
// (that is Step 4). This is the seam the later build-queue + army-storage steps
// (Step 2+) and a dev hook call to put a troop on the field.
// =============================================================================

using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// Static spawn helper for friendly troops. Resolves a <see cref="TroopDef"/> from
    /// <see cref="TroopCatalog"/>, builds the body via <see cref="TroopFactory"/>, and
    /// points its hunt scan at the village Enemy layer.
    /// </summary>
    public static class TroopDeployer
    {
        /// <summary>
        /// Spawns the troop with id <paramref name="troopId"/> at <paramref name="pos"/>,
        /// wires its enemy LayerMask to the village "Enemy" layer, and returns the live
        /// <see cref="TroopController"/> (or null if the id is unknown).
        /// </summary>
        public static TroopController SpawnTroop(string troopId, Vector3 pos)
        {
            var def = TroopCatalog.Find(troopId);
            if (def == null)
            {
                Debug.LogWarning($"[TroopDeployer] unknown troop id '{troopId}' — not spawned.");
                return null;
            }

            var troop = TroopFactory.Build(def, pos, Quaternion.identity, null);
            troop?.SetEnemyMask(VillageEnemyMask());
            return troop;
        }

        /// <summary>
        /// The LayerMask the troop sweeps for hostiles — the village "Enemy" layer the
        /// enemy bodies live on (EnemyFactory sets <c>go.layer = NameToLayer("Enemy")</c>).
        /// Falls back to everything when the layer isn't declared so the scan still works.
        /// </summary>
        private static LayerMask VillageEnemyMask()
        {
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            return enemyLayer >= 0 ? (LayerMask)(1 << enemyLayer) : (LayerMask)~0;
        }
    }
}
