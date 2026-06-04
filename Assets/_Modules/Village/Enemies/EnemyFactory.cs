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
            var vis = VisualFactory.Skin(go.transform, "Enemies/" + model, SkinOptions.Enemy(height));
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
            return enemy;
        }

        /// <summary>Enemy id/role → skeleton model. The only humanoid enemy models in
        /// Resources/Enemies today; chosen by role/size so the silhouette reads the
        /// threat. Swap to bespoke models here (one line per id) when the packs land.</summary>
        public static string ModelForEnemy(EnemyDef def)
        {
            string id = def != null ? def.Id : null;
            switch (id)
            {
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
