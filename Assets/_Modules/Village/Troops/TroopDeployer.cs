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
using UnityEngine.AI;
using DeNelle.Core.State;
using DeNelle.Core.Diagnostics;   // FlowTrace - [Flow:TroopVisual]
using DeNelle.Village.World.Camps; // RaidSpire — WO-1595 march axis

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

            // §12 spawn seam (owner defect 2026-08-02): this is the MID-RAID entry point — it runs
            // long after the last SceneManager.sceneLoaded, which is precisely why the scene-load-only
            // MagentaGuard sweep never saw these bodies. One line per deploy so a headless raid run
            // ties every [Flow:TroopVisual] / [Flow:MagentaProbe] line below back to its deploy.
            FlowTrace.Step("TroopVisual",
                $"deploy id='{troopId}' model='{def.Model ?? "<none>"}' role='{def.Role ?? "<none>"}' at {pos} " +
                "(runtime spawn, post-sceneLoaded).");

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

            // WO-1595: deploy into formation along the march axis (tap → RaidSpire), not a
            // role-blind spiral ring. RingOffset remains as fallback when no spire / unknown role.
            Vector3 spawnPos = pos + FormationOrRingOffset(t.TroopDefId, stackIndex, pos, spread);

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
        /// WO-1595 formation slot when a RaidSpire exists; otherwise the legacy spiral ring.
        /// CLI review 2026-09-07: sample onto NavMesh after offset (Support can land ~11 m
        /// off the tap / off-mesh past the deploy gate); fall back to RingOffset on a miss.
        /// Stack index is per assault JOB among live Active troops so two melee types do not
        /// share one lateral lane.
        /// </summary>
        private static Vector3 FormationOrRingOffset(string troopDefId, int stackIndex, Vector3 tapPos, float spread)
        {
            var def = TroopCatalog.Find(troopDefId);
            var spire = RaidSpire.Active;
            if (def == null || spire == null || !spire.IsAlive)
                return RingOffset(stackIndex, spread);

            Vector3 march = spire.WorldPosition - tapPos;
            march.y = 0f;
            if (march.sqrMagnitude < 0.01f) march = Vector3.forward;

            var job = RaidAssaultAi.JobFromRole(def.Role);
            int roleStack = CountActiveJob(job);
            Vector3 offset = RaidAssaultAi.FormationWorldOffset(job, roleStack, march);
            Vector3 candidate = tapPos + offset;

            // Sample the formation slot onto walkable mesh. Max distance covers a Support
            // back-line (~5 m) plus lateral lanes without accepting a far-off wrong cell.
            const float sampleRadius = 3.5f;
            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, sampleRadius, NavMesh.AllAreas))
            {
                Vector3 sampled = hit.position - tapPos;
                sampled.y = 0f;
                FlowTrace.Step("RaidAI",
                    $"formation-deploy id='{troopDefId}' job={job} roleStack={roleStack} " +
                    $"typeStack={stackIndex} offset={sampled.ToString("F1")} " +
                    $"march={march.normalized.ToString("F1")} nav=sampled");
                return sampled;
            }

            FlowTrace.Warn("RaidAI",
                $"formation-deploy id='{troopDefId}' job={job} roleStack={roleStack} " +
                $"nav=MISS@{sampleRadius:F1}m — falling back to RingOffset(typeStack={stackIndex}).");
            return RingOffset(stackIndex, spread);
        }

        /// <summary>How many live Active troops already map to this assault job (for lateral lanes).</summary>
        private static int CountActiveJob(RaidAssaultJob job)
        {
            int n = 0;
            var list = TroopController.ActiveTroops;
            for (int i = 0; i < list.Count; i++)
            {
                var t = list[i];
                if (t == null || !t.IsAlive) continue;
                string id = t.TroopId;
                if (string.IsNullOrEmpty(id)) continue;
                var d = TroopCatalog.Find(id);
                if (d == null) continue;
                if (RaidAssaultAi.JobFromRole(d.Role) == job) n++;
            }
            return n;
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
