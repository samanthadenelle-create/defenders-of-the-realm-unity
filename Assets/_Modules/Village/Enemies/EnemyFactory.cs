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
using DeNelle.Core.Combat;      // ActorAnimator (attached so Enemy drives work)
using DeNelle.Core.Validation;  // WO-315/WO-363: opt-in OrientationGuard on the enemy root
using DeNelle.Core.Diagnostics; // root-cause FlowTrace on the single enemy-creation path

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

            // ROOT-CAUSE TRACE: this IS the single enemy-creation path. Print the
            // def id → resolved model (and whether modelOverride forced it) so the log
            // shows what family each body is being built as, on every spawner.
            FlowTrace.Step("Enemy",
                $"EnemyFactory.Build: id='{(def != null ? def.Id : "null")}' " +
                $"-> model '{model}'{(modelOverride != null ? " (OVERRIDE)" : "")} loading 'Enemies/{model}'");

            // WIGHT 4x (owner 2026-06-13): the AccuRIG'd wight fits noticeably small for a
            // looming demon. Scale its FIT HEIGHT (visual, via SkinOptions.Enemy(height) below)
            // AND its trigger capsule up ~4x so the hitbox matches the bigger body. The
            // NavMeshAgent footprint stays standard (root unit-scale) so it still paths normally.
            if (model == "Demon")
            {
                const float wightScale = 4f;
                height     *= wightScale;
                col.radius *= wightScale;
                col.height *= wightScale;
                col.center *= wightScale;
            }

            // WO-315: rig-forward correction. The +X-forward Tripo/People families (the
            // Orc Warband — same export convention as the heroes, which use -90f) need a
            // -90 yaw on the visual child so the authored forward aligns to the root's +Z
            // that Enemy.DriveNav's face-velocity drives. The KayKit Skeleton_* / Boss /
            // Dragon rigs already face +Z and must NOT be rotated. Family is resolved by
            // the single authoritative source (EnemyAnimatorFactory.RigFor) so we never
            // blanket-rotate. ⚠ "Troll" is mapped but RigFor falls it to KayKit
            // (HumanoidMedium) → 0 rotation here; playtest if a Tripo Troll lands.
            var skinOpts = SkinOptions.Enemy(height);
            // WIGHT FIX (RCA 2026-06-13): Demon / OgreMage are newer +X-forward Tripo creature
            // exports (same convention as the Orc Warband + the heroes, all -90f) but they resolve
            // to the HumanoidLarge rig — NOT OrcWarband — so the old condition rotated neither, and
            // SkinOptions.Enemy() does NOT set FixTripoMaterials (the orcs render only because their
            // materials were extracted to external URP .mats at import; Demon/OgreMage ship raw
            // FbxSurfacePhong → magenta/unlit). Treat them like the orcs: same -90 yaw + attach the
            // runtime Tripo→URP material fixer. By name so we never blanket-rotate the rig class.
            if (EnemyAnimatorFactory.RigFor(model) == EnemyRig.OrcWarband)
            {
                // Orc Warband: upright already, just a -90 yaw so +X-forward faces +Z.
                skinOpts.LocalRotation = Quaternion.Euler(0f, -90f, 0f);
            }
            else if (model == "Demon" || model == "OgreMage")
            {
                // AccuRIG'd CC_Base Humanoid (isHuman=True 2026-06-13): wight + ogre-mage import
                // UPRIGHT like the orcs → the SAME orc-style -90 yaw, NOT the old face-down X=-90
                // pitch (which would tip the standing rig over). Textures link via <name>.fbm so
                // FixTripoMaterials is unnecessary.
                skinOpts.LocalRotation = Quaternion.Euler(0f, -90f, 0f);
            }
            else if (model == "Troll")
            {
                // STILL raw-Tripo HumanoidLarge (face-down export class — not yet AccuRIG'd): pitch
                // up with X=-90 to stand + the -90 yaw to face. If facing-wrong, tune the Y; if
                // still face-down, flip X. (Move to the Demon/OgreMage branch once AccuRIG'd.)
                skinOpts.LocalRotation = Quaternion.Euler(-90f, -90f, 0f);
                skinOpts.FixTripoMaterials = true;
            }
            var vis = VisualFactory.Skin(go.transform, "Enemies/" + model, skinOpts);
            if (vis != null)
            {
                // ROOT-CAUSE TRACE: the model mesh actually loaded + skinned. If this
                // never fires for orc/troll while the capsule-fallback below does, the
                // bug is a MISSING prefab silently degrading variety to one look.
                FlowTrace.Once("Enemy", $"skinned-{model}",
                    $"VisualFactory.Skin OK for model '{model}' (id '{(def != null ? def.Id : "?")}') — real mesh, not capsule");
                EnemyAnimatorFactory.Apply(vis, model);   // walk/attack/die controller

                // WIGHT HALF-UNDERGROUND FIX (RCA 2026-06-17): the Tripo/AccuRIG FBXs pivot at
                // the mesh CENTRE, so when the visual is scaled up (the Demon/wight at 4x) the
                // feet sink well below the root's spawn Y=0 and the body renders half-buried.
                // Re-ground the visual exactly like PetDeployer.NormalizePetHeight (L718-727):
                // recompute the skinned bounds and lift the visual child so its feet (bounds.min.y)
                // rest at the root's Y. Null-safe — no renderers → skip. Applies to every model
                // (the offset is ~0 for already-grounded rigs) so no spawner can ship a buried body.
                ReGroundVisual(go.transform, vis);
            }
            else
            {
                // ROOT-CAUSE TRACE (MOST IMPORTANT): the model failed to load/skin, so
                // this body becomes a tinted capsule — silent variety loss. If many
                // families hit this, every enemy looks the same despite varied ids.
                FlowTrace.Warn("Enemy",
                    $"model '{model}' (id '{(def != null ? def.Id : "?")}') had NO loadable mesh at " +
                    $"'Enemies/{model}' — FALLBACK to tinted capsule (no family silhouette)");
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

            // WO-315 / WO-363: attach the opt-in orientation gate to the enemy ROOT —
            // the same transform Enemy.DriveNav slerps toward agent velocity. The guard
            // is INERT in shipping builds (needs its bool ticked AND the ORIENTATION_GATE
            // define / editor), so this is zero-cost at runtime; in a QA/validation run it
            // turns an ambiguous "enemy looks backwards" into a precise GATE-FAILED verdict
            // (root facing vs world displacement) instead of a felt guess. Enemies carried
            // no guard before, so the WO-363 rule was never actually exercised on them.
            if (go.GetComponent<OrientationGuard>() == null)
                go.AddComponent<OrientationGuard>();

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
                // ── HOLLOW ONES (the skeleton wave faction) ──────────────────────
                // DEF-250: the HOLLOW wave archetypes get DISTINCT silhouettes so a
                // mixed wave reads as a varied fight, not clones.
                //   grunt      → Skeleton_Minion  (lean, basic — the numerous rusher)
                //   brute/tank → Skeleton_Golem   (big, LargeEnemy rig — slow heavy wall)
                //   skirmisher → Skeleton_Rogue   (low, quick — the flanker)
                //   caster     → Skeleton_Mage    (robed support / healer)
                case "hollow-walker":    return "Skeleton_Minion";   // grunt
                case "hollow-warrior":   return "Skeleton_Golem";    // brute / tank
                case "hollow-rogue":     return "Skeleton_Rogue";    // fast skirmisher
                case "hollow-acolyte":   return "Skeleton_Mage";     // caster / healer (WO-316 family healer)
                case "necromancer":      return "Necromancer";       // dedicated elite (Boss rig)

                // ── WILDLANDS ROSTER (MainCastle_Hall overworld roamers) ─────────
                // 2026-06-13 owner fix: these ids USED to all resolve to skeletons
                // (orc-raider→Skeleton_Warrior, caveman→Skeleton_Golem, …), so the
                // whole open world read as one undead family despite varied ids
                // ("no families, no trolls, no orcs, no ogre"). They now map to the
                // DISTINCT non-skeleton creature models that ALREADY exist in
                // Resources/Enemies so each Wildlands id has its own silhouette.
                // Keyed by ID — the one signal RegionMobSpawner.BuildRoamerDef sets
                // (it leaves Family/Role at defaults), and the same id the garrison /
                // tribe / outpost spawners all carry. Every target below is a VERIFIED
                // file in Assets/Resources/Enemies.
                case "orc-raider":       return "Orc_Berserker";     // greenskin raider → real orc (OrcWarband rig)
                case "caveman":          return "Troll";             // big brute → Cave Troll silhouette
                case "feral-wolf":       return "Skeleton_Rogue";    // no beast model exists — keep the fast, low skirmisher
                case "tiefling-cultist": return "Demon";             // demonic cultist → Demon (distinct horned silhouette)

                // ── ORC WARBAND (DEF-221) — Humanoid Tripo orcs, OrcWarband rig ──
                case "orc-berserker":    return "Orc_Berserker";     // brute / charger
                case "orc-shaman":       return "Orc_Shaman";        // caster
                case "orc-necromancer":  return "Orc_Necromancer";   // camp elite
                // ENEMY-VARIETY EXT (2026-06-13): the EnemyOutpost RAID BOSS ("orc-warlord",
                // EnemyOutpost.BuildBossDef) set NO Family/Role, so it fell to the size
                // default (height 2.6 → Skeleton_Golem) and the warband's capstone boss
                // read as an undead golem — same silhouette as a hollow brute, wrong
                // faction. Keyed by ID (the one signal BuildBossDef sets) to the heaviest
                // VERIFIED orc model so the warlord is visibly a bigger orc than its
                // raiders (orc-raider → Orc_Berserker), keeping the orc faction read.
                case "orc-warlord":      return "Orc_Necromancer";   // outpost raid boss — heaviest orc silhouette

                // ── BRUTES / OGRES / BOSSES ──────────────────────────────────────
                case "troll":            return "Troll";             // Cave Troll brute
                case "ogre":             return "OgreMage";          // ogre brute → OgreMage
                case "ogre-mage":        return "OgreMage";          // ogre caster
                case "demon":            return "Demon";             // demon
                case "boss-dragon":      return "Dragon";            // wing boss → the Dragon
                case "dragon":           return "Dragon";
            }

            // ── FAMILY FALLBACK ──────────────────────────────────────────────────
            // Any spawner that DID set a Family (garrison/tribe set orc/tribe/beast/
            // cult) but used an id not cased above still reads as its faction rather
            // than collapsing to a skeleton. All targets are verified Resources files.
            string family = def != null ? (def.Family ?? "").Trim().ToLowerInvariant() : "";
            switch (family)
            {
                case "orc":   return def != null && def.Role == "caster" ? "Orc_Shaman" : "Orc_Berserker";
                case "troll": return "Troll";
                case "ogre":  return "OgreMage";
                case "demon":
                case "cult":  return "Demon";
                case "dragon": return "Dragon";
            }

            // ── DEFAULT (unknown family/id) → pick a skeleton by body size ───────
            // ROOT-CAUSE TRACE: an id with no explicit case AND no known family lands
            // here and becomes a generic skeleton. The Warn below names the family so
            // the next run shows which families still need a bespoke model/import.
            string sizeDefault = (def != null && def.Height >= 2.3f) ? "Skeleton_Golem" : "Skeleton_Minion";
            FlowTrace.Warn("Enemy",
                $"ModelForEnemy: id '{(def != null ? def.Id : "null")}' (family " +
                $"'{(string.IsNullOrEmpty(family) ? "?" : family)}') has NO explicit model case " +
                $"— DEFAULTED by size to '{sizeDefault}'. Add a case or import art for this family.");
            return sizeDefault;
        }

        // Lift a freshly-skinned visual child so its FEET rest at the root's Y. The Tripo/
        // AccuRIG FBXs pivot at the mesh centre, so a scaled-up body (the 4x wight) sinks
        // below the spawn surface. Mirrors PetDeployer.NormalizePetHeight's re-ground tail:
        // recompute world bounds post-scale, and if the bounds bottom is below the root,
        // shift the visual up by that gap. Null-safe — no renderers → no-op.
        private static void ReGroundVisual(Transform root, GameObject vis)
        {
            if (root == null || vis == null) return;
            var renderers = vis.GetComponentsInChildren<Renderer>();
            if (renderers == null || renderers.Length == 0) return;
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            if (b.size.y <= 0.01f) return;
            float feetOffset = b.min.y - root.position.y;   // negative when feet are below the root
            if (feetOffset < 0f)
                vis.transform.localPosition -= new Vector3(0f, feetOffset, 0f);
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
