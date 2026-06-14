// =============================================================================
// TroopFactory — the ONE place a skinned, damageable friendly troop is built
// (WO-453 Step 1). Mirrors EnemyFactory's recipe so the project keeps a SINGLE
// troop-creation path.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// A bare UNIT-SCALE root carries a NON-TRIGGER capsule collider + a NavMeshAgent
// + the TroopController; the mesh is a fit visual child via VisualFactory, with a
// tinted-capsule fallback ONLY if the model is missing (same as EnemyFactory).
//
// TWO deliberate differences from EnemyFactory:
//   1. The root sits on a DEFAULT-ish (probe-visible) layer, NOT "Enemy" — the
//      enemy contact-attack probe (Enemy.ProbeForStructure) uses
//      QueryTriggerInteraction.Ignore and resolves IDamageableStructure, so the
//      troop must be on a layer that probe sees and carry a NON-TRIGGER collider
//      for the enemy to find + damage it (same requirement StoryCompanion meets).
//   2. The collider is NON-trigger (enemies hit it); the troop's OWN hunt scan
//      uses QueryTriggerInteraction.Collide on the enemy mask, so the enemies'
//      trigger capsules are still found by the troop.
//
// The factory builds the BODY; the caller (TroopDeployer) owns SetEnemyMask().
// =============================================================================

using UnityEngine;
using UnityEngine.AI;

namespace DeNelle.Village
{
    /// <summary>Single skinned-troop builder shared by every troop spawner.</summary>
    public static class TroopFactory
    {
        /// <summary>
        /// Builds a skinned, damageable troop at <paramref name="pos"/> and returns its
        /// <see cref="TroopController"/> (already carrying a non-trigger collider + a
        /// NavMeshAgent on a probe-visible layer). The caller calls SetEnemyMask().
        /// </summary>
        public static TroopController Build(TroopDef def, Vector3 pos, Quaternion rot, Transform parent)
        {
            // Snap the spawn to the nearest navmesh point BEFORE adding the agent so it
            // always lands on a valid surface (mirrors EnemyFactory — a NavMeshAgent
            // AddComponent'd off-mesh never paths). A far miss is logged once.
            if (NavMesh.SamplePosition(pos, out NavMeshHit navHit, 6f, NavMesh.AllAreas))
            {
                pos = navHit.position;
            }
            else
            {
                Debug.LogWarning($"[TroopFactory] No baked NavMesh within 6m of spawn {pos} " +
                                 $"for '{(def != null ? def.Id : "troop")}' — agent will hold position. " +
                                 "Check the spawn point / bake.");
            }

            var go = new GameObject(def != null ? $"Troop ({def.Id})" : "Troop");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, rot);

            // Layer: a probe-visible DEFAULT-ish layer (NOT "Enemy"), so the enemy
            // contact-attack probe (QueryTriggerInteraction.Ignore) hits the troop's
            // non-trigger collider — the same requirement StoryCompanion's hitbox meets.
            // Leave the GameObject on layer 0 (Default) explicitly.
            go.layer = 0;

            // NON-TRIGGER capsule so enemies can physically reach + damage the troop.
            // Offset up to wrap the body; root stays unit-scale (scaling a NavMeshAgent
            // root misbehaves) — only the visual is fit bigger.
            var col = go.AddComponent<CapsuleCollider>();
            col.isTrigger = false;
            col.radius = 0.42f;
            col.height = 1.8f;
            col.center = new Vector3(0f, 0.9f, 0f);

            // Skin (load -> fit -> strip colliders) + animator; tinted-capsule fallback
            // only if the model is genuinely missing (so a troop still spawns damageable).
            // Hero/companion bodies import facing +X and need a -90° yaw to face +Z
            // (DEF-232 — set on SkinOptions so it's applied BEFORE fit/seat).
            string model = def != null ? def.Model : null;
            float height = 1.8f;
            GameObject vis = null;
            if (!string.IsNullOrEmpty(model))
            {
                var skinOpts = SkinOptions.Enemy(height);
                // Body facing is per-pack and authored on the def (Tripo bodies face +X → -90;
                // Supercyan humanoids face +Z → 0). Default -90 keeps legacy bodies correct.
                skinOpts.LocalRotation = Quaternion.Euler(0f, def.ModelYaw, 0f);
                vis = VisualFactory.Skin(go.transform, "Heroes/" + model, skinOpts);
            }

            if (vis == null)
            {
                // Model missing → tinted-capsule fallback (mirrors EnemyFactory ~139-153)
                // so the troop is still visible + damageable, just without a silhouette.
                Debug.LogWarning($"[TroopFactory] model 'Heroes/{model}' " +
                                 $"(id '{(def != null ? def.Id : "?")}') had no loadable mesh — " +
                                 "FALLBACK to a tinted capsule.");
                var cap = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                if (cap.TryGetComponent(out Collider cc)) Object.Destroy(cc);
                cap.transform.SetParent(go.transform, false);
                cap.transform.localPosition = new Vector3(0f, 0.9f, 0f);
                cap.transform.localScale = Vector3.one;
                TintCapsule(cap.GetComponent<Renderer>());
            }

            // NavMeshAgent — share the hero's agent type (0) so the troop traverses the
            // SAME NavMeshLinks the hero/enemies use. TroopController.Awake re-asserts
            // these (radius/height/Move-driven) but set them here too so it's path-ready.
            var agent = go.AddComponent<NavMeshAgent>();
            agent.agentTypeID = 0;
            agent.radius = 0.4f;
            agent.height = 1.8f;

            var troop = go.AddComponent<TroopController>();
            troop.Configure(def, pos);
            return troop;
        }

        private static void TintCapsule(Renderer mr)
        {
            if (mr == null) return;
            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (sh == null) return;
            var m = new Material(sh);
            // Friendly blue tint, to read distinct from the enemy fallback's red-brown.
            var tint = new Color(0.30f, 0.45f, 0.75f);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint); else m.color = tint;
            mr.sharedMaterial = m;
        }
    }
}
