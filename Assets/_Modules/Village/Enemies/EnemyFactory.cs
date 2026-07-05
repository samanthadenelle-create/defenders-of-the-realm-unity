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

            // SIZE IS DATA-DRIVEN (owner 2026-06-19, WO-468): the old `model == "Demon"` 4x block
            // was authored for one small AccuRIG wight, but the "Demon" model is SHARED by
            // tiefling-cultist + demon (see ModelForEnemy), so the 4x rendered a 1.9m cultist at
            // ~7.6m — the "enemies way too large" the owner flagged in playtest. VisualFactory.Fit
            // already normalises every enemy to exactly def.Height, so a looming demon's size belongs
            // in its EnemyDef.Height (data), NOT a code multiplier keyed off the shared model name.
            // Removed: enemies now render at their authored height (~1.8m vs the 1.75m hero).

            // WO-315: rig-forward correction. The +X-forward Tripo/People families (the
            // Orc Warband — same export convention as the heroes, which use -90f) need a
            // -90 yaw on the visual child so the authored forward aligns to the root's +Z
            // that Enemy.DriveNav's face-velocity drives. The KayKit Skeleton_* / Boss /
            // Dragon rigs already face +Z and must NOT be rotated. Family is resolved by
            // the single authoritative source (EnemyAnimatorFactory.RigFor) so we never
            // blanket-rotate. ⚠ "Troll" is mapped but RigFor falls it to KayKit
            // (HumanoidMedium) → 0 rotation here; playtest if a Tripo Troll lands.
            var skinOpts = SkinOptions.Enemy(height);
            // WO-482: resolve the rig ONCE here so the orc-yaw/material block AND the post-Skin
            // basecolor fallback below both read the same authority (EnemyAnimatorFactory.RigFor).
            EnemyRig rigForModel = EnemyAnimatorFactory.RigFor(model);
            // WIGHT FIX (RCA 2026-06-13): Demon / OgreMage are newer +X-forward Tripo creature
            // exports (same convention as the Orc Warband + the heroes, all -90f) but they resolve
            // to the HumanoidLarge rig — NOT OrcWarband — so the old condition rotated neither, and
            // SkinOptions.Enemy() does NOT set FixTripoMaterials (the orcs render only because their
            // materials were extracted to external URP .mats at import; Demon/OgreMage ship raw
            // FbxSurfacePhong → magenta/unlit). Treat them like the orcs: same -90 yaw + attach the
            // runtime Tripo→URP material fixer. By name so we never blanket-rotate the rig class.
            if (rigForModel == EnemyRig.OrcWarband || rigForModel == EnemyRig.OrcHumanoid)
            {
                // Orc Warband + WO-482 orc family: upright already, just a -90 yaw so +X-forward faces +Z.
                skinOpts.LocalRotation = Quaternion.Euler(0f, -90f, 0f);
                // Ticket #4 (RCA 2026-06-21): the old assumption above ("orcs render because their
                // materials were extracted to external URP .mats") is STALE — Orc_*.fbx.meta remaps to
                // tripo_mat_*.mat assets that no longer exist, so the orc imports its raw Phong material
                // and renders MAGENTA ("pink people") in the URP player build. Attach the runtime
                // Tripo→URP fixer like Troll/Demon. Idempotent + self-verifying (logs TripoMatFix VERIFY),
                // so it's a safe no-op if an orc IS already URP, and the proof line if it wasn't.
                skinOpts.FixTripoMaterials = true;
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
                // Ticket #2 (DATA-PROVEN 2026-06-21, DiagGarrisonRoster oracle): the old X=-90 pitch
                // (Euler(-90,-90,0)) laid the troll ON ITS BACK — captured worldUp=(1,0,0), localEuler
                // (270,270,0), tipped=True. The Troll imports UPRIGHT like Demon/OgreMage (same rig class,
                // captured upright at Euler(0,-90,0) worldUp=(0,1,0)). Drop the pitch: -90 YAW ONLY, like
                // Demon. Keep the Tripo->URP fixer (raw-Tripo material). This is the proven 'troll y+90' fix.
                skinOpts.LocalRotation = Quaternion.Euler(0f, -90f, 0f);
                skinOpts.FixTripoMaterials = true;
            }
            var vis = VisualFactory.Skin(go.transform, "Enemies/" + model, skinOpts);
            // RENDER-VERIFY (owner directive 2026-06-19, TGVRU on the enemy choke point —
            // mirrors HeroArmorVisual.VerifyArmorRendersNow): VisualFactory.Skin returning
            // non-null only means "an object was instantiated", NOT that it actually RENDERS.
            // A grey/magenta/empty body (no enabled renderer, or a renderer with no sharedMesh)
            // would pass the old null-check and ship as a "real mesh, not capsule" — an
            // invisible-but-hittable enemy. PROVE the skinned body can render before we accept
            // it; on fail, FlowTrace.Fail + DROP it and fall through to the tinted-capsule path
            // so the spawner always ships a VISIBLE hittable body, never a ghost.
            if (vis != null && !VerifyVisualRenders(vis, model, def))
            {
                Object.Destroy(vis);
                vis = null;
            }
            if (vis != null)
            {
                // ROOT-CAUSE TRACE: the model mesh actually loaded + skinned. If this
                // never fires for orc/troll while the capsule-fallback below does, the
                // bug is a MISSING prefab silently degrading variety to one look.
                FlowTrace.Once("Enemy", $"skinned-{model}",
                    $"VisualFactory.Skin OK for model '{model}' (id '{(def != null ? def.Id : "?")}') — real mesh, not capsule");
                EnemyAnimatorFactory.Apply(vis, model);   // walk/attack/die controller

                // WEAPONS-IN-HANDS (2026-07-04): arm the berserker so it no longer fights bare-handed.
                // Any enemy rendered with the Orc_Berserker model (orc-berserker + the orc-raider/caveman/
                // troll stand-ins that reuse it) gets a real axe in its right hand. Seats on CC_Base_R_Hand
                // via the humanoid avatar; grip is DATA-DRIVEN (Offset Forge key "axe_A" in
                // AttachmentOffsetRegistry) with an eyeball default so the owner can felt-tune it in the Forge.
                if (model == "Orc_Berserker")
                    AttachEnemyWeapon(vis, "axe_A", 1.0f,
                        defaultEuler: new Vector3(-25f, 90f, 0f), defaultPos: Vector3.zero, defaultScale: 1f);

                // WO-482: the new Tripo orc FAMILY (Orc_Warrior/Tank/Mage) ships EXTERNAL textures with an
                // unbound _MainTex → renders solid WHITE under the URP fixer unless the per-orc basecolor is
                // bound as the fixer's FALLBACK (the exact fix ATB slice 2c needed). The fixer was attached by
                // VisualFactory (skinOpts.FixTripoMaterials, set above for the OrcHumanoid rig); bind the
                // fallback NOW — synchronous, before the fixer's deferred Start() — so it paints the atlas.
                if (rigForModel == EnemyRig.OrcHumanoid)
                {
                    string orcTex =
                        model == "Orc_Warrior" ? "Enemies/OrcTex/Orc_Warrior_basecolor" :
                        model == "Orc_Tank"    ? "Enemies/OrcTex/Orc_Tank_basecolor"    :
                        model == "Orc_Mage"    ? "Enemies/OrcTex/Orc_Mage_basecolor"    : null;
                    if (!string.IsNullOrEmpty(orcTex))
                    {
                        // Explicit Unity null-check (NOT ?. — GetComponent returns a fake-null the
                        // null-conditional operator won't catch; the project lints against it).
                        var orcFixer = vis.GetComponentInChildren<DeNelle.Core.TripoMaterialFixer>();
                        if (orcFixer != null) orcFixer.SetFallbackTexture(orcTex);
                    }
                }
                else if (rigForModel == EnemyRig.OrcWarband || model == "Troll")
                {
                    // VILLAGE2 GARRISON FIX (RCA 2026-06-28, DiagGarrisonRoster): the village2_stronghold
                    // garrison (orc-berserker/orc-shaman/orc-raider → OrcWarband: Orc_Berserker/Shaman/
                    // Necromancer; + Troll) attaches the Tripo→URP fixer (FixTripoMaterials above), but the
                    // basecolor-fallback bind was gated to the OrcHumanoid rig ONLY. The Warband family + Troll
                    // have NO committed OrcTex basecolor (their FBX remaps point to deleted tripo_mat_*.mat),
                    // so the fixer built a clean URP/Lit with white albedo → flat WHITE orcs. No real texture
                    // exists, so bind a per-family SOLID tint instead of white. Skipped for OrcHumanoid (it
                    // binds OrcTex above) — this branch is the else.
                    var warbandFixer = vis.GetComponentInChildren<DeNelle.Core.TripoMaterialFixer>();
                    if (warbandFixer != null)
                    {
                        // STAND-IN TINT (no Troll.fbx / OgreMage.fbx yet): troll & ogre reuse an
                        // OrcWarband orc model (see ModelForEnemy) but are DISTINGUISHED by tint so a
                        // player reads them as a different foe, not just another orc. Keyed by def.Id/
                        // Family (the model is now an orc, so "model == Troll" no longer fires).
                        //   troll/caveman → grey-green troll hide; ogre → cold ogre grey;
                        //   real Warband orcs → orc green/brown.
                        string tId  = def != null ? (def.Id ?? "").Trim().ToLowerInvariant() : "";
                        string tFam = def != null ? (def.Family ?? "").Trim().ToLowerInvariant() : "";
                        bool isTroll = model == "Troll" || tId == "troll" || tId == "caveman" || tFam == "troll";
                        bool isOgre  = tId == "ogre" || tId == "ogre-mage" || tFam == "ogre";
                        Color fallbackTint =
                            isTroll ? new Color(0.38f, 0.40f, 0.34f) :   // grey-green troll hide
                            isOgre  ? new Color(0.48f, 0.47f, 0.52f) :   // cold ogre grey
                                      new Color(0.30f, 0.42f, 0.22f);     // orc green/brown
                        warbandFixer.SetFallbackTint(fallbackTint);
                        FlowTrace.Step("Enemy",
                            $"garrison fallback TINT {fallbackTint} bound to '{model}' (id '{tId}', rig {rigForModel}) — " +
                            "no OrcTex basecolor for Warband/Troll family, paints solid colour not white");
                    }
                }

                // WIGHT HALF-UNDERGROUND FIX (RCA 2026-06-17): the Tripo/AccuRIG FBXs pivot at
                // the mesh CENTRE, so when the visual is scaled up (the Demon/wight at 4x) the
                // feet sink well below the root's spawn Y=0 and the body renders half-buried.
                // Re-ground the visual exactly like PetDeployer.NormalizePetHeight (L718-727):
                // recompute the skinned bounds and lift the visual child so its feet (bounds.min.y)
                // rest at the root's Y. Null-safe — no renderers → skip. Applies to every model
                // (the offset is ~0 for already-grounded rigs) so no spawner can ship a buried body.
                ReGroundVisual(go.transform, vis);

                // PROPORTION GUARD (F8 2026-07-04, "one is sized really huge, I can see a leg only"):
                // the ONLY intended sizing is VisualFactory.Fit normalising the body to def.Height
                // (heights are data-bounded ~1.2–2.6m; the old Demon 4x multiplier was removed, see the
                // §WO-468 note above; EnemyAnimatorFactory applies NO scale). So a 3–5x giant can only be a
                // Fit MIS-MEASUREMENT: Fit (VisualFactory.Fit) divides target/measure where `measure` is the
                // skinned-mesh world bounds read on the SAME frame as Instantiate — before the rig poses. If
                // one model's pre-pose bounds collapse (degenerate/tiny), target/measure overshoots and the
                // body renders multiples of def.Height, and nothing re-checks the FINAL rendered size. This
                // guard MEASURES the real rendered height post-Skin+animator+reground and, if it has drifted
                // outside [0.5x, 2x] of the canonical family reference (def.Height), re-normalises it to that
                // reference — the belt that guarantees no enemy ever ships >2x (or <0.5x) another.
                EnforceProportion(go.transform, vis, def, model, height);

                // §12 ticket #2 (troll y+90): prove orientation by DATA before any rotation edit. A worldUp
                // far from (0,1,0) means the rig is tipped (the Troll Euler(-90,-90,0) pitch is the suspect).
                // Captured headless; if worldUp ~= (0,1,0) the "mis-rotated" report is stale and NO edit is warranted.
                if (vis != null)
                    FlowTrace.Step("Enemy",
                        $"'{model}' visual localEuler={vis.transform.localEulerAngles} worldUp={vis.transform.up} (upright iff worldUp~=(0,1,0))");
            }
            else
            {
                // ROOT-CAUSE TRACE (MOST IMPORTANT): the model failed to load/skin OR loaded
                // but did not render (render-verify above dropped it), so this body becomes a
                // tinted capsule — silent variety loss. If many families hit this, every enemy
                // looks the same despite varied ids.
                FlowTrace.Warn("Enemy",
                    $"model '{model}' (id '{(def != null ? def.Id : "?")}') had NO renderable mesh at " +
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

            // AGENT ON-MESH VERIFY (TGVRU, owner 2026-06-19): a NavMeshAgent that AddComponent's
            // onto a point just off the baked surface lands with isOnNavMesh==false and then NEVER
            // paths — the "idle, never chases" class of bug. We already SamplePosition-snapped the
            // spawn at the top, but a snap can still miss (no mesh within 6m) or the agent can wake
            // a hair off-mesh. Self-report it here so a capture splits "spawned but won't chase"
            // from "AI logic broke" with zero guessing. Warn (not Fail): Enemy.cs already degrades
            // to holding position, so the enemy still spawns — this is the diagnosable signal.
            if (!agent.isOnNavMesh)
            {
                FlowTrace.Warn("Enemy",
                    $"NavMeshAgent for '{(def != null ? def.Id : "enemy")}' (model '{model}') spawned " +
                    $"OFF the navmesh at {go.transform.position} (isOnNavMesh=false) — it will idle and " +
                    "never chase. Check the spawn point / bake (snap missed or agent woke off-surface).");
            }

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
                case "caveman":          return "Orc_Berserker";     // STAND-IN: no Troll.fbx — big brute reuses Orc_Berserker (tinted grey-green below)
                case "feral-wolf":       return "Skeleton_Rogue";    // no beast model exists — keep the fast, low skirmisher
                case "tiefling-cultist": return "Demon";             // demonic cultist → Demon (distinct horned silhouette)

                // ── ORC WARBAND (DEF-221) — Humanoid Tripo orcs, OrcWarband rig ──
                case "orc-berserker":    return "Orc_Berserker";     // brute / charger
                case "orc-shaman":       return "Orc_Shaman";        // caster
                case "orc-necromancer":  return "Orc_Necromancer";   // camp elite

                // ── ORC FAMILY (WO-482) — new Tripo roster, OrcHumanoid rig ──────
                // The WO-481 overworld-encounter combatants: Warrior leader + Tank + Mage.
                // Distinct models + controller (OrcHumanoid) from the older OrcWarband orcs
                // above; they render via the per-orc OrcTex basecolor fallback (set after Skin).
                case "orc-warrior":      return "Orc_Warrior";       // family leader / DPS
                case "orc-tank":         return "Orc_Tank";          // bulwark
                case "orc-mage":         return "Orc_Mage";          // caster
                // ENEMY-VARIETY EXT (2026-06-13): the EnemyOutpost RAID BOSS ("orc-warlord",
                // EnemyOutpost.BuildBossDef) set NO Family/Role, so it fell to the size
                // default (height 2.6 → Skeleton_Golem) and the warband's capstone boss
                // read as an undead golem — same silhouette as a hollow brute, wrong
                // faction. Keyed by ID (the one signal BuildBossDef sets) to the heaviest
                // VERIFIED orc model so the warlord is visibly a bigger orc than its
                // raiders (orc-raider → Orc_Berserker), keeping the orc faction read.
                case "orc-warlord":      return "Orc_Necromancer";   // outpost raid boss — heaviest orc silhouette

                // ── BRUTES / OGRES / BOSSES ──────────────────────────────────────
                // STAND-INS (no Troll.fbx / OgreMage.fbx in Resources/Enemies — those render
                // as tinted capsules): reuse EXISTING OrcWarband-rig orc models until real
                // Tripo troll/ogre art lands. troll → big Orc_Berserker, ogre → Orc_Shaman.
                // Both go through the OrcWarband SetFallbackTint path below, so the tint block
                // paints troll grey-green and ogre grey to keep them visually distinct.
                case "troll":            return "Orc_Berserker";     // STAND-IN: big brute (was "Troll" — missing)
                case "ogre":             return "Orc_Shaman";        // STAND-IN: ogre brute (was "OgreMage" — missing)
                case "ogre-mage":        return "Orc_Shaman";        // STAND-IN: ogre caster (was "OgreMage" — missing)
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
                case "troll": return "Orc_Berserker";   // STAND-IN (no Troll.fbx) — tinted grey-green below
                case "ogre":  return "Orc_Shaman";      // STAND-IN (no OgreMage.fbx) — tinted grey below
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

        // ── PROPORTION GUARD (F8 2026-07-04) ─────────────────────────────────────────
        // MEASURE the enemy's actual world-space rendered height (combined renderer bounds — the
        // real visible size, NOT the authored scale) once at spawn, then ENFORCE the owner's rule:
        // no enemy may render more than 2x (or less than 0.5x) the canonical family reference. The
        // reference is a STABLE per-family target — def.Height (the authored intended height, already
        // clamped by the caller), NOT a runaway max — so a single mis-Fit outlier can never drag the
        // band up. If the measured height has drifted outside [0.5x, 2x] of that reference, re-normalise
        // the visual to the reference (uniform scale by reference/measured) and re-ground it. Every path
        // logs so a headless / F8 capture PROVES proportions hold and names any correction it made.
        // Deterministic + cheap: measures once, no per-frame work.
        private static void EnforceProportion(Transform root, GameObject vis, EnemyDef def, string model, float reference)
        {
            if (root == null || vis == null) return;
            if (reference <= 0.01f) return;

            string id = def != null ? def.Id : "?";
            if (!TryVisualBounds(vis, out Bounds b)) return;
            float measured = b.size.y;
            if (measured <= 0.01f) return;

            float ratio = measured / reference;
            FlowTrace.Step("EnemySize",
                $"{id} model='{model}' renderedHeight={measured:0.00}m reference={reference:0.00}m ratio={ratio:0.00}x scale={vis.transform.localScale.x:0.000}");

            // Belt: only correct GROSS outliers (>2x or <0.5x the family reference). Normal
            // pose-driven variance (a walk cycle, arms raised) stays untouched.
            if (ratio > 2f || ratio < 0.5f)
            {
                float correction = reference / measured;   // brings the rendered height to the reference
                vis.transform.localScale *= correction;
                ReGroundVisual(root, vis);                 // feet back to Y after the rescale
                FlowTrace.Warn("EnemySize",
                    $"clamped {id} (model='{model}') from {measured:0.00}m -> {reference:0.00}m " +
                    $"({ratio:0.00}x out of the [0.5x,2x] proportion band; scaled by {correction:0.000}) — " +
                    "Fit mis-measured this body; re-normalised to the family reference so it ships in proportion.");
            }
        }

        // Combined world-space renderer AABB of a skinned visual (mirrors ReGroundVisual's measure).
        // False if no renderers or degenerate bounds.
        private static bool TryVisualBounds(GameObject vis, out Bounds bounds)
        {
            bounds = default;
            if (vis == null) return false;
            var renderers = vis.GetComponentsInChildren<Renderer>();
            if (renderers == null || renderers.Length == 0) return false;
            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds.size.y > 0.0001f;
        }

        // ── ENEMY WEAPON ATTACH (WEAPONS-IN-HANDS 2026-07-04) ────────────────────────
        // Give a skinned enemy a real held prop (e.g. the berserker's axe). Additive + reversible:
        // loads a committed Resources prop, size-normalises it to heldLength (robust to the FBX's native
        // import scale), seats the handle end in the palm, parents to the RIGHT-hand bone, then applies a
        // DATA-DRIVEN grip offset from AttachmentOffsetRegistry (Offset Forge key = mesh name, e.g.
        // "axe_A") — falling back to the passed eyeball default when the owner has not tuned it yet. The
        // offset reproduces the Offset Forge preview convention (localRot=Euler(rot), localPos=pos,
        // localScale=one*scale) so a single Forge calibration pass perfects the grip.
        private static void AttachEnemyWeapon(GameObject visual, string meshName, float heldLength,
            Vector3 defaultEuler, Vector3 defaultPos, float defaultScale)
        {
            if (visual == null || string.IsNullOrEmpty(meshName)) return;

            var anim = visual.GetComponentInChildren<Animator>();
            Transform hand = null;
            if (anim != null && anim.isHuman)
                hand = anim.GetBoneTransform(HumanBodyBones.RightHand);
            if (hand == null)
                hand = FindBoneByName(visual.transform, "CC_Base_R_Hand", "R_Hand", "RightHand", "Hand_R");
            if (hand == null)
            {
                FlowTrace.Warn("Enemy",
                    $"AttachEnemyWeapon: no right-hand bone on '{visual.name}' " +
                    $"(isHuman={(anim != null && anim.isHuman)}) — '{meshName}' NOT attached.");
                return;
            }

            var prefab = Resources.Load<GameObject>("Heroes/Props/Weapons/" + meshName);
            if (prefab == null)
            {
                FlowTrace.Warn("Enemy",
                    $"AttachEnemyWeapon: prop 'Heroes/Props/Weapons/{meshName}' missing — enemy stays unarmed.");
                return;
            }

            var prop = Object.Instantiate(prefab);
            prop.name = "EnemyWeapon_" + meshName;
            foreach (var c in prop.GetComponentsInChildren<Collider>(true)) if (c != null) Object.Destroy(c);
            foreach (var rb in prop.GetComponentsInChildren<Rigidbody>(true)) if (rb != null) Object.Destroy(rb);

            var gripRoot = new GameObject("EnemyWeaponGrip_" + meshName);
            NormalizeEnemyProp(prop, gripRoot.transform, heldLength);
            gripRoot.transform.SetParent(hand, false);

            Vector3 euler = defaultEuler;
            Vector3 pos = defaultPos;
            float scale = defaultScale <= 0f ? 1f : defaultScale;
            if (AttachmentOffsetRegistry.TryGetOffset(meshName, out var fo))
            {
                euler = fo.eulerRot; pos = fo.pos; scale = fo.scale > 0f ? fo.scale : 1f;
                FlowTrace.Step("Enemy",
                    $"AttachEnemyWeapon '{meshName}' -> '{hand.name}': Offset Forge grip pos={pos} rot={euler} scale={scale:0.###}");
            }
            else
            {
                FlowTrace.Step("Enemy",
                    $"AttachEnemyWeapon '{meshName}' -> '{hand.name}': no Forge offset — EYEBALL default " +
                    $"pos={pos} rot={euler} scale={scale:0.###} (tune '{meshName}' in Offset Forge).");
            }
            gripRoot.transform.localRotation = Quaternion.Euler(euler);
            gripRoot.transform.localPosition = pos;
            gripRoot.transform.localScale = Vector3.one * scale;
        }

        // Size-normalise a freshly-instantiated held prop under a fresh grip root (identity at the world
        // origin): orient its LONGEST axis to +Y, scale so that axis == heldLength (independent of the
        // FBX's native import scale), and seat the bottom (handle) end + lateral centre at the origin so
        // the palm holds the grip. Compact mirror of the hero NormalizeInto+SeatByHandle intent.
        private static void NormalizeEnemyProp(GameObject prop, Transform gripRoot, float heldLength)
        {
            prop.transform.SetParent(gripRoot, false);
            prop.transform.localPosition = Vector3.zero;
            prop.transform.localRotation = Quaternion.identity;
            prop.transform.localScale = Vector3.one;

            if (!TryWorldBounds(prop, out Bounds b)) return;
            Vector3 size = b.size;
            // Rotate the longest axis onto local +Y.
            if (size.x >= size.y && size.x >= size.z)
                prop.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);   // X -> Y
            else if (size.z >= size.y && size.z >= size.x)
                prop.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // Z -> Y

            if (!TryWorldBounds(prop, out b)) return;
            float longest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            if (longest > 1e-4f && heldLength > 0f)
            {
                prop.transform.localScale *= heldLength / longest;
                if (!TryWorldBounds(prop, out b)) return;
            }

            // Seat: shift so the handle end (bounds min Y) and lateral centre land at the grip origin.
            Vector3 lp = prop.transform.localPosition;
            lp.x -= b.center.x;
            lp.y -= (b.center.y - b.extents.y);
            lp.z -= b.center.z;
            prop.transform.localPosition = lp;
        }

        // Combined world-space renderer AABB of a prop. The grip root is created at the world origin with
        // an identity transform, so world == grip-local for the seating math above. False if no renderers.
        private static bool TryWorldBounds(GameObject prop, out Bounds bounds)
        {
            bounds = default;
            var rends = prop.GetComponentsInChildren<Renderer>();
            if (rends == null || rends.Length == 0) return false;
            bounds = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) bounds.Encapsulate(rends[i].bounds);
            return bounds.size.sqrMagnitude > 1e-8f;
        }

        // Depth-first search for a bone transform whose name contains any of the given tokens
        // (case-insensitive). Fallback when the rig exposes no valid humanoid avatar for GetBoneTransform.
        private static Transform FindBoneByName(Transform root, params string[] tokens)
        {
            if (root == null) return null;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == null) continue;
                string n = t.name;
                foreach (var tok in tokens)
                    if (!string.IsNullOrEmpty(tok) && n.IndexOf(tok, System.StringComparison.OrdinalIgnoreCase) >= 0)
                        return t;
            }
            return null;
        }

        // RENDER-VERIFY (TGVRU, mirrors HeroArmorVisual.VerifyArmorRendersNow): a freshly-skinned
        // enemy visual MUST carry >=1 ENABLED renderer that has a real mesh (sharedMesh on a
        // SkinnedMeshRenderer, or a MeshFilter.sharedMesh on a static MeshRenderer). VisualFactory.Skin
        // returning an object proves nothing renders — an empty/grey/magenta body passes the null-check
        // and ships invisible-but-hittable. Traces the exact counts so a capture pinpoints "no enabled
        // renderer" vs "no mesh" with zero guessing. Returns false => caller drops it to the tinted capsule.
        private static bool VerifyVisualRenders(GameObject vis, string model, EnemyDef def)
        {
            if (vis == null) return false;
            string id = def != null ? def.Id : "?";

            int total = 0, enabledWithMesh = 0;
            var renderers = vis.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                if (r == null) continue;
                total++;
                if (!r.enabled) continue;
                bool hasMesh = false;
                if (r is SkinnedMeshRenderer smr) hasMesh = smr.sharedMesh != null;
                else if (r.TryGetComponent(out MeshFilter mf)) hasMesh = mf.sharedMesh != null;
                if (hasMesh) enabledWithMesh++;
            }

            bool renders = enabledWithMesh > 0;
            FlowTrace.Step("Enemy",
                $"VerifyVisualRenders model='{model}' id='{id}': renderers total={total} enabledWithMesh={enabledWithMesh} => renders={renders}");

            if (!renders)
            {
                FlowTrace.Fail("Enemy",
                    $"VerifyVisualRenders FAILED for model '{model}' (id '{id}'): skinned object loaded but " +
                    $"NO enabled renderer carries a mesh (total={total}) — would ship an invisible-but-hittable " +
                    "enemy. Dropping to the tinted-capsule fallback so the body is visible.");
                return false;
            }
            return true;
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
