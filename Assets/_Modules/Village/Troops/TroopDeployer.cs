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
using DeNelle.Core.State;

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
            if (troop != null)
            {
                // WO-771.9 SPAWN-WIRING: resolve this troop's EFFECTIVE stats at its persisted
                // upgrade level ONCE (TroopStatResolver folds troops.json baseline with the
                // troop-upgrades.json reach/strength curves) and apply HP/DPS/reach/aggro +
                // unlocked abilities to the live unit. Applied BEFORE the veterancy/perk
                // multipliers (SpawnFromArmy) so those compound on the upgraded base. Level
                // defaults to 1 (pure baseline) when no BarracksService/state exists (dev spawn).
                int level = BarracksService.TroopLevel(troopId);
                troop.ApplyUpgradeStats(TroopStatResolver.Effective(def, level));
                troop.SetEnemyMask(VillageEnemyMask());
            }
            return troop;
        }

        /// <summary>
        /// Deploys a PERSISTED army troop (WO-453 Step 4): resolves its TroopDef from
        /// <see cref="PlayerTroop.TroopDefId"/>, spawns the body at <paramref name="pos"/>
        /// (offset by a small ring step for a stack so multiple drops don't overlap), stamps
        /// the owning <see cref="PlayerTroop.Id"/> onto the controller, applies the troop's
        /// veterancy <see cref="PlayerTroop.DamageMultiplier"/>, wires the enemy mask, and
        /// returns the live <see cref="TroopController"/> (or null if the def is unknown).
        /// <paramref name="stackIndex"/> spreads several troops dropped from one tap.
        /// </summary>
        public static TroopController SpawnFromArmy(PlayerTroop t, Vector3 pos, int stackIndex = 0, float spread = 1.1f)
        {
            if (t == null)
            {
                Debug.LogWarning("[TroopDeployer] SpawnFromArmy got a null PlayerTroop — not spawned.");
                return null;
            }

            Vector3 spawnPos = pos + RingOffset(stackIndex, spread);

            var troop = SpawnTroop(t.TroopDefId, spawnPos);
            if (troop == null) return null;

            troop.OwnedTroopId = t.Id;
            // BAKE AT SPAWN (WO-430): combine veterancy × the Armorer tier damage perk into ONE
            // damage call (ApplyDamageMultiplier re-bases from the def, so two calls would not
            // compound — multiply them here), then bake the health perk. This runs in the RAID
            // scene at deploy, so the city upgrades carry INTO the raid automatically. Reads the
            // active modifiers ONCE at spawn (perks are fixed for the raid).
            var mods = DeNelle.Core.State.ModifierService.Active;
            troop.ApplyDamageMultiplier(t.DamageMultiplier * mods.TroopDamageMult);
            troop.ApplyHealthMultiplier(mods.TroopHealthMult);
            return troop;
        }

        /// <summary>
        /// A small spiral-ring offset so several troops dropped from one tap fan out
        /// instead of stacking on a single cell. Index 0 = no offset; later indices step
        /// around a widening ring. Pure (deterministic) so a stack lays out the same way.
        /// </summary>
        private static Vector3 RingOffset(int index, float spread)
        {
            if (index <= 0) return Vector3.zero;
            // ~6 troops per ring, ring radius grows every full turn.
            int ring = 1 + (index - 1) / 6;
            float ang = (index % 6) / 6f * Mathf.PI * 2f;
            float r = spread * ring;
            return new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
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
