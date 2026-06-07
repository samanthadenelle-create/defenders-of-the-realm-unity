// =============================================================================
// EnemyFactory — the ONE place a skinned, hittable Enemy body is built. Every
// spawner (waves, roaming, tribes, wards, family-test) routes through here so the
// project has a SINGLE enemy-creation path (CLAUDE.md §9: no parallel spawn
// systems) and no spawner can ever ship a placeholder "pill" again.
// -----------------------------------------------------------------------------
// Mirrors the proven PatriciaLight.BuildEnemy recipe: a bare UNIT-SCALE root
// carries the offset trigger capsule + Enemy + NavMeshAgent; the mesh is a fit
// visual child via VisualFactory + EnemyAnimatorFactory, with a tinted-capsule
// fallback ONLY if the model is missing. The factory builds the BODY; the caller
// owns Configure(), targeting (SetBrainTarget — Heart for a siege wave, a roam
// anchor for a wanderer), wave-scaling, and event hooks.
// =============================================================================

using UnityEngine;
using UnityEngine.AI;
using DeNelle.Core.Combat; // ActorAnimator (attached so Enemy drives work)

namespace DeNelle.Village
{
    /// <summary>Single skinned-enemy builder shared by every spawner.</summary>
    public static class EnemyFactory
    {
        /// <summary>Builds a skinned, hittable enemy at <paramref name="pos"/> and returns
        /// its <see cref="Enemy"/> (already carrying EnemyDamageable + a NavMeshAgent on
        /// the Enemy layer). The caller calls Configure() and sets the brain target.</summary>
        public static Enemy Build(EnemyDef def, Vector3 pos, Quaternion rot, Transform parent, string modelOverride = null)
        {
            float height = def != null ? Mathf.Max(0.8f, def.Height) : 1.9f;
            float sizeScale = Mathf.Clamp(height / 1.9f, 0.55f, 1.6f);

            // DEF-268: a NavMeshAgent AddComponent'd off the baked NavMesh logs
            // "Failed to create agent because there is no valid NavMesh" and the agent
            // never paths. Spawners (camp-defense raiders / roaming mobs / late-loaded
            // waves) sometimes hand us a point just off the surface. Snap the spawn to
            // the nearest navmesh point BEFORE we add the agent so it always lands on a
            // valid surface. Only snaps when a navmesh is genuinely within reach; a far
            // miss is logged once and the enemy still spawns (agent simply holds, exactly
            // as Enemy.cs already degrades) rather than being silently dropped.
            if (NavMesh.SamplePosition(pos, out NavMeshHit navHit, 6f, NavMesh.AllAreas))
            {
                pos = navHit.position;
            }
            else
            {
                Debug.LogWarning($"[EnemyFactory] No baked NavMesh within 6m of spawn {pos} " +
                                 $"for '{(def != null ? def.Id : "enemy")}' — agent will hold position. " +
                                 "Check the spawn point / bake.");
            }

            var go = new GameObject(def != null ? $"Enemy ({def.Id})" : "Enemy");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, rot);

            int enemyLayer = LayerMask.NameToLayer("Enemy");
            if (enemyLayer >= 0) go.layer = enemyLayer;

            // Trigger capsule, offset up to wrap the body. Root stays unit-scale
            // (scaling a NavMeshAgent root misbehaves) — only the visual is fit bigger.
            var col = go.AddComponent<CapsuleCollider>();
            col.isTrigger = true;
            col.radius = 0.42f * sizeScale;
            col.height = 1.8f * sizeScale;
            col.center = new Vector3(0f, 0.9f * sizeScale, 0f);

            // Skin (load -> fit -> strip colliders) + animator; tinted-capsule fallback
            // only if the model is genuinely missing (so an enemy still spawns hittable).
            string model = modelOverride ?? ModelForEnemy(def);

            // WO-315: rig-forward correction. The +X-forward Tripo/People families (the
            // Orc Warband — same export convention as the heroes, which use -90f) need a
            // -90 yaw on the visual child so the authored forward aligns to the root's +Z
            // that Enemy.DriveNav's face-velocity drives. The KayKit Skeleton_* / Boss /
            // Dragon rigs already face +Z and must NOT be rotated. Family is resolved by
            // the single authoritative source (EnemyAnimatorFactory.RigFor) so we never
            // blanket-rotate. ⚠ "Troll" is mapped but RigFor falls it to KayKit
            // (HumanoidMedium) → 0 rotation here; playtest if a Tripo Troll lands.
            var skinOpts = SkinOptions.Enemy(height);
            if (EnemyAnimatorFactory.RigFor(model) == EnemyRig.OrcWarband)
                skinOpts.LocalRotation = Quaternion.Euler(0f, -90f, 0f);
            var vis = VisualFactory.Skin(go.transform, "Enemies/" + model, skinOpts);
            if (vis != null)
            {
                EnemyAnimatorFactory.Apply(vis, model);   // walk/attack/die controller
            }
            else
            {
                var cap = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                if (cap.TryGetComponent(out Collider cc)) Object.Destroy(cc);
                cap.transform.SetParent(go.transform, false);
                cap.transform.localPosition = new Vector3(0f, 0.9f * sizeScale, 0f);
                cap.transform.localScale = Vector3.one * sizeScale;
                TintCapsule(cap.GetComponent<Renderer>());
            }

            var agent = go.AddComponent<NavMeshAgent>();
            // Share the hero's agent type (0) so enemies traverse the SAME NavMeshLinks
            // the hero uses (StairNavLink builds links for agentTypeID 0 — rampart + any
            // player-built stairs). Match hero radius/height for uniform "as mobile as the
            // player" pathing on the shared single-agent navmesh.
            agent.agentTypeID = 0;
            agent.radius = 0.4f;
            agent.height = 1.8f;
            var enemy = go.AddComponent<Enemy>();                          // RequireComponent pulls EnemyDamageable
            if (go.GetComponent<EnemyDamageable>() == null)
                go.AddComponent<EnemyDamageable>();

            // Ensure ActorAnimator on the logical root (it finds the Animator on the
            // skinned vis child set by EnemyAnimatorFactory.Apply). This makes
            // Enemy.cs drives (SetLocomotion/PlayAttack/Die) work for skeleton/orc/troll etc.
            if (go.GetComponent<ActorAnimator>() == null)
                go.AddComponent<ActorAnimator>();
            return enemy;
        }

        /// <summary>Enemy id/role → skeleton model. Grouped by family (Hollow/Skeleton Legion,
        /// Orc Warband, Troll/Stonebelly, etc.) with class variety (Tank=brute/golem,
        /// DPS=rogue/warrior, Healer=mage/shaman). Basic strategy in EnemyBrain (DPS
        /// focus-fire healers first, Tank protects, Healer prioritizes allies).
        /// Chosen by role/size for silhouette. Swap to bespoke when packs land.</summary>
        public static string ModelForEnemy(EnemyDef def)
        {
            string id = def != null ? def.Id : null;
            switch (id)
            {
                // DEF-250: the three HOLLOW wave archetypes get DISTINCT silhouettes so a
                // mixed wave reads as a varied fight, not clones. Previously all three fell
                // through to the height-based default (every one → Skeleton_Minion), so the
                // grunt/brute/skirmisher were visually identical despite different stats.
                //   grunt      → Skeleton_Minion  (lean, basic — the numerous rusher)
                //   brute/tank → Skeleton_Golem   (big, LargeEnemy rig — slow heavy wall)
                //   skirmisher → Skeleton_Rogue   (low, quick — the flanker)
                case "hollow-walker":    return "Skeleton_Minion";   // grunt
                case "hollow-warrior":   return "Skeleton_Golem";    // brute / tank
                case "hollow-rogue":     return "Skeleton_Rogue";    // fast skirmisher

                case "orc-raider":       return "Skeleton_Warrior";  // heavy melee
                case "caveman":          return "Skeleton_Golem";    // big brute
                case "feral-wolf":       return "Skeleton_Rogue";    // fast skirmisher
                case "tiefling-cultist": return "Skeleton_Mage";     // caster
                case "necromancer":      return "Necromancer";       // dedicated elite
                // DEF-221 Orc Warband family — Humanoid Tripo orcs (Resources/Enemies),
                // animated by OrcWarband.controller via EnemyAnimatorFactory.
                case "orc-berserker":    return "Orc_Berserker";     // brute / charger
                case "orc-shaman":       return "Orc_Shaman";        // caster
                case "orc-necromancer":  return "Orc_Necromancer";   // camp elite
                // 'troll' family — Tripo Cave Troll (Resources/Enemies/Troll). Falls back
                // to the tinted capsule if the model isn't imported yet (LogWarning, not error).
                case "troll":            return "Troll";             // brute / mini
            }
            // Unmapped roster (wave / tribe / ward) → pick by body size.
            if (def != null && def.Height >= 2.3f) return "Skeleton_Golem";
            return "Skeleton_Minion";
        }

        private static void TintCapsule(Renderer mr)
        {
            if (mr == null) return;
            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (sh == null) return;
            var m = new Material(sh);
            var tint = new Color(0.55f, 0.30f, 0.35f);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint); else m.color = tint;
            mr.sharedMaterial = m;
        }
    }
}
