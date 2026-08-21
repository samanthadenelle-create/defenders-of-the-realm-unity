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
using DeNelle.Core;             // FeatureFlags (ff.enemyweapons gate on the held-weapon attach)
using DeNelle.Core.Combat;      // ActorAnimator (attached so Enemy drives work)
using DeNelle.Core.Enemies;     // WO-772: EnemyResolver — id -> family -> DISTINCT model authority
using DeNelle.Core.Validation;  // WO-315/WO-363: opt-in OrientationGuard on the enemy root
using DeNelle.Core.Diagnostics; // root-cause FlowTrace on the single enemy-creation path
using DeNelle.Core.Geometry;

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

            // DEF-268 + WO-791: seat the spawn ON the baked NavMesh BEFORE the agent is
            // added. The old single 6m sample kept the RAW (possibly airborne) position on
            // a miss and only console-logged — the owner-felt "frozen + floating" garrison
            // enemies (2026-07-30), which ALSO zeroed their damage: a body hovering above
            // its slot sits outside HeroHealth's 1.5m engage sphere, so it swings and lands
            // nothing (WO-792, same root). Now: progressively wider snap; on a total miss
            // the body is at least raycast-grounded (never floats), and the defect WARNS
            // through FlowTrace so the F8 harness captures it.
            pos = SeatSpawnOnNavMesh(pos, def);

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

            // FIX 1 (PAIN_POINTS_2026-07-26 §1.1) — WILDLANDS DEFERRAL GATE at the single
            // enemy-creation chokepoint. The living Wildlands roster (orc-raider / caveman /
            // feral-wolf / tiefling-cultist) has NO shippable art — the Orc_Berserker Tripo
            // body retargets to EXPLODED geometry — yet RegionMobSpawner (active while
            // ff.overworldencounter is OFF) + every camp/tribe/ward/garrison spawner (all
            // defaulting to "orc-raider") funnel these ids through here. If the id is NOT
            // combat-approved, REDIRECT it to a ratified Hollow substitute (real committed
            // art, routes through EnemyResolver) so the region still spawns a VALID enemy —
            // never the exploded orc. ONE edit at Build covers ALL spawners. The
            // Orc_Berserker rig/material fix itself is a DEFERRED Phase-2 art task.
            string reqId = def != null ? def.Id : null;
            if (!string.IsNullOrEmpty(reqId) && !EnemyResolver.IsCombatApproved(reqId))
            {
                string subId = EnemyResolver.SubstituteHollowId(
                    reqId, def != null ? def.Role : null, height);
                if (EnemyResolver.TryResolveHollowModel(subId, null, out string subModel))
                {
                    FlowTrace.Warn("Enemy",
                        $"Wildlands id '{reqId}' deferred (PAIN_POINTS 1.1) -> substituted '{subId}'");
                    model = subModel;
                }
            }

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
            // blanket-rotate. (The old warning here — that "Troll" fell through to KayKit
            // HumanoidMedium and got 0 rotation — is DEAD as of 2026-08-09: Troll.fbx landed,
            // RigFor routes it to LargeHumanoid, and the AccuRigIntake branch rotates it.)
            // ⛔ RESOLVES THROUGH EnemyAssetLoader (resident-first, per-family, NON-BLOCKING).
            // Extracted into TrySkinBody so the LATE RE-SKIN can run the exact same recipe when the
            // family bundle lands — one body-building path, never a second one that drifts.
            var vis = TrySkinBody(go, def, model, height);
            if (vis == null)
            {
                ReportNoRenderableMesh(def, model);
                AddCapsuleFallback(go, sizeScale);
                // ⛔ A CAPSULE MUST NEVER BE PERMANENT because the player spawned two seconds early.
                // Arm the re-skin: it polls the resident cache and swaps the real body in the moment
                // the family bundle lands. It self-destructs on success, and on a proven-missing
                // address it stops and says so once (see EnemyLateSkinner).
                EnemyLateSkinner.Arm(go, def, model, height, sizeScale);
            }

            var agent = go.AddComponent<NavMeshAgent>();
            // Share the hero's agent type (0) so enemies traverse the SAME NavMeshLinks
            // the hero uses (StairNavLink builds links for agentTypeID 0 — rampart + any
            // player-built stairs). Match hero radius/height for uniform "as mobile as the
            // player" pathing on the shared single-agent navmesh.
            agent.agentTypeID = 0;
            agent.radius = 0.4f;
            agent.height = 1.8f;

            // AGENT ON-MESH VERIFY + REPAIR (WO-791; was TGVRU report-only): a NavMeshAgent
            // that wakes with isOnNavMesh==false NEVER paths — the "idle, never chases"
            // class of bug. The spawn was already seated above, but an agent can still wake
            // a hair off-surface. Instead of only reporting, REPAIR it: Warp onto the
            // nearest sampled point (Warp is the canonical off-mesh re-seat — see Enemy.cs's
            // own teleport note). Only if even that fails is the loud Warn kept — that line
            // now genuinely means "the bake is missing here", with zero ambiguity.
            if (!agent.isOnNavMesh)
            {
                bool repaired = NavMesh.SamplePosition(go.transform.position, out NavMeshHit wakeHit, 12f, NavMesh.AllAreas)
                                && agent.Warp(wakeHit.position);
                if (repaired)
                    FlowTrace.Step("Enemy",
                        $"NavMeshAgent for '{(def != null ? def.Id : "enemy")}' (model '{model}') woke OFF-mesh " +
                        $"at {pos} — REPAIRED via Warp to {go.transform.position} (isOnNavMesh={agent.isOnNavMesh}).");
                else
                FlowTrace.Warn("Enemy",
                    $"NavMeshAgent for '{(def != null ? def.Id : "enemy")}' (model '{model}') spawned " +
                    $"OFF the navmesh at {go.transform.position} (isOnNavMesh=false) and Warp found no mesh " +
                    "within 12m — it will idle and never chase. Check the spawn point / bake.");
            }

            var enemy = go.AddComponent<Enemy>();                          // RequireComponent pulls EnemyDamageable
            if (go.GetComponent<EnemyDamageable>() == null)
                go.AddComponent<EnemyDamageable>();

            // Ensure ActorAnimator on the logical root (it finds the Animator on the
            // skinned vis child set by EnemyAnimatorFactory.Apply). This makes
            // Enemy.cs drives (SetLocomotion/PlayAttack/Die) work for skeleton/orc/troll etc.
            if (go.GetComponent<ActorAnimator>() == null)
                go.AddComponent<ActorAnimator>();

            // WO-VFX-WEAPON-TRAILS: enemies share the same rig + ActorAnimator, so give them the
            // blade-trail flash too (owner: "both hero and enemy"). It self-drives off
            // ActorAnimator.AttackStarted; with no GearLoadout it uses the steel-common default
            // trail and anchors on the RightHand bone. DisallowMultipleComponent => safe re-add.
            if (go.GetComponent<WeaponTrailController>() == null)
                go.AddComponent<WeaponTrailController>();

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


        // =====================================================================
        //  BODY BUILD — shared by the spawn path and by the LATE RE-SKIN
        // =====================================================================

        /// <summary>
        /// Build the skinned visual child for <paramref name="model"/> under <paramref name="go"/>,
        /// or null when the art is not resolvable RIGHT NOW. Never blocks: the prefab comes from
        /// EnemyAssetLoader, which answers from the resident cache and asks for a miss
        /// asynchronously (per family) instead of waiting for it.
        /// <para>⛔ This is the ONLY place an enemy body is skinned. EnemyLateSkinner re-runs THIS
        /// method when content arrives, so a re-skinned enemy is byte-for-byte the enemy it would
        /// have been had the bundle been local at spawn.</para>
        /// </summary>
        internal static GameObject TrySkinBody(GameObject go, EnemyDef def, string model, float height)
        {
            if (go == null || string.IsNullOrEmpty(model)) return null;
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
            else if (AccuRigIntake.Contains(model))
            {
                // 2026-08-09: the intake joins the proven Troll case below — same CC_Base export,
                // same upright +X-forward convention, same raw-Tripo materials. Without the fixer
                // they import FbxSurfacePhong and render magenta/unlit in a URP player build.
                // Ticket #2 (DATA-PROVEN 2026-06-21, DiagGarrisonRoster oracle): the old X=-90 pitch
                // (Euler(-90,-90,0)) laid the troll ON ITS BACK — captured worldUp=(1,0,0), localEuler
                // (270,270,0), tipped=True. The Troll imports UPRIGHT like Demon/OgreMage (same rig class,
                // captured upright at Euler(0,-90,0) worldUp=(0,1,0)). Drop the pitch: -90 YAW ONLY, like
                // Demon. Keep the Tripo->URP fixer (raw-Tripo material). This is the proven 'troll y+90' fix.
                skinOpts.LocalRotation = Quaternion.Euler(0f, -90f, 0f);
                skinOpts.FixTripoMaterials = true;
            }
            // ⛔ RESOLVES THROUGH EnemyAssetLoader, NOT VisualFactory's path overload.
            // That overload resolves through StructureAssetLoader — correct for buildings, WRONG
            // for enemies, and it is why the owner's 2026-08-20 capture contained ZERO
            // [Flow:EnemyAssets] lines while enemies were plainly loading: the enemy seam was
            // never being entered at all. Enemy art then missed the STRUCTURE residency cache
            // (it only warms "Structures/"), missed Resources (Assets/Resources/Enemies is
            // DELETED), and every body fell through to the tinted capsule.
            //
            // EnemyAssetLoader is resident-first and NON-BLOCKING: a miss returns null after
            // asking for this family's bundle asynchronously. Never wait here — an enemy spawn is
            // reachable from wave callbacks and scene-entry paths, exactly the nesting that turned
            // the structure seam's wait into a three-minute deadlock.
            GameObject bodyPrefab = DeNelle.Core.EnemyAssetLoader.LoadEnemyPrefab(model);
            var vis = bodyPrefab != null ? VisualFactory.Skin(go.transform, bodyPrefab, skinOpts) : null;
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
                // GATED OFF (owner F8 2026-07-04): "enemies spamming weapons in all sorts of odd ways —
                // maybe we not add a weapon unless we perfect one." No held weapon is attached until the
                // Offset Forge grip is perfected on ONE weapon. Flip ff.enemyweapons = 1 to re-enable.
                if (FeatureFlags.EnemyWeapons && model == "Orc_Berserker")
                    AttachEnemyWeapon(vis, "axe_A", 1.0f,
                        defaultEuler: new Vector3(-25f, 90f, 0f), defaultPos: Vector3.zero, defaultScale: 1f);

                // WO-482: the new Tripo orc FAMILY (Orc_Warrior/Tank/Mage) ships EXTERNAL textures with an
                // unbound _MainTex → renders solid WHITE under the URP fixer unless the per-orc basecolor is
                // bound as the fixer's FALLBACK (the exact fix ATB slice 2c needed). The fixer was attached by
                // VisualFactory (skinOpts.FixTripoMaterials, set above for the OrcHumanoid rig); bind the
                // fallback NOW — synchronous, before the fixer's deferred Start() — so it paints the atlas.
                if (rigForModel == EnemyRig.OrcHumanoid)
                {
                    // 2026-08-09: resolved rather than hardcoded, so Orc_Warlord (new) and the
                    // REPLACED Orc_Mage pick up their TripoTex maps. The old three-way ternary
                    // returned null for any orc it did not name, which rendered the warlord solid
                    // white — a new model silently had no skin path at all.
                    string orcTex = ResolveBasecolor(model);
                    // ⛔ MISS-TINT FIRST, ALWAYS (owner report 2026-08-20, "enemies not having
                    // coloring"). SetMissTint is TEXTURE-MISS-ONLY (TripoMaterialFixer.cs:187):
                    // a slot that resolves a real map is byte-unchanged; a slot that resolves
                    // NOTHING takes this colour instead of the fixer's unpainted 0.5 grey
                    // (TripoMaterialFixer.cs:121). It is therefore safe to set unconditionally,
                    // and it closes the asymmetry that made this branch the silent one: the
                    // OrcWarband arm below has an ELSE that paints a family tint when no
                    // basecolor resolves, and this arm had NONE — so an OrcHumanoid model with
                    // no atlas (Orc_Warlord is exactly that case, see the 2026-08-09 note above)
                    // fell through with no texture, no tint and NO TRACE LINE, and rendered flat
                    // grey. Silent by construction, which is why it survived this long.
                    var missFixer = vis.GetComponentInChildren<DeNelle.Core.TripoMaterialFixer>();
                    if (missFixer != null) missFixer.SetMissTint(FamilyFallbackTint(def, model));
                    if (string.IsNullOrEmpty(orcTex))
                        FlowTrace.Warn("Enemy",
                            $"OrcHumanoid model '{model}' (id '{(def != null ? def.Id : "?")}') resolved NO basecolor " +
                            "under Enemies/TripoTex or Enemies/OrcTex — any slot whose own map is missing would have " +
                            $"rendered FLAT GREY with nothing said. Bound the family MISS-TINT " +
                            $"{FamilyFallbackTint(def, model)} as the floor. Permanent fix = ship " +
                            $"'Enemies/TripoTex/{model}_basecolor'.");
                    if (!string.IsNullOrEmpty(orcTex))
                    {
                        // Explicit Unity null-check (NOT ?. — GetComponent returns a fake-null the
                        // null-conditional operator won't catch; the project lints against it).
                        var orcFixer = vis.GetComponentInChildren<DeNelle.Core.TripoMaterialFixer>();
                        if (orcFixer != null) orcFixer.SetFallbackTexture(orcTex);
                    }
                }
                else if (rigForModel == EnemyRig.OrcWarband || AccuRigIntake.Contains(model))
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
                        // WO-790: ALBEDO-RESTORE SEAM. The Warband/Troll family's authored Tripo
                        // basecolors never travelled from the authoring machine's export tree
                        // (Orc_Berserker.json records a MACHINE-LOCAL export dir -- "C:/EoA/Assets/
                        // Resources/Enemies" -- that no longer resolves anywhere; the repo root is
                        // machine-dependent, so treat that string as provenance, never as a path to
                        // read; the .fbx.meta texture remaps dangle to guids that
                        // exist nowhere; a binary scan of the FBXs finds zero embedded images). So
                        // the tint below is what the owner saw as "flat green/orange enemies".
                        // When the owner stages the restored art as Enemies/OrcTex/<model>_basecolor
                        // (the exact convention the OrcHumanoid branch uses), bind the REAL skin and
                        // SKIP the tint entirely -- TripoMaterialFixer multiplies tint OVER textures
                        // (TripoMaterialFixer.cs:331), so texture-or-tint must be EXCLUSIVE or the
                        // restored skin renders green-multiplied. Absent (today) => optional load
                        // misses quietly and the tint carries the look exactly as before.
                        string warbandTex = ResolveBasecolor(model);
                        if (warbandTex != null)
                        {
                            warbandFixer.SetFallbackTexture(warbandTex, optional: true);
                            FlowTrace.Step("Enemy",
                                $"garrison albedo '{warbandTex}' bound to '{model}' — restored Warband basecolor wins over the solid tint (WO-790)");
                        }
                        else
                        {
                        // STAND-IN TINT (no Troll.fbx / OgreMage.fbx yet): troll & ogre reuse an
                        // OrcWarband orc model (see ModelForEnemy) but are DISTINGUISHED by tint so a
                        // player reads them as a different foe, not just another orc. Keyed by def.Id/
                        // Family (the model is now an orc, so "model == Troll" no longer fires).
                        //   troll/caveman → grey-green troll hide; ogre → cold ogre grey;
                        //   real Warband orcs → orc green/brown.
                        string tId  = def != null ? (def.Id ?? "").Trim().ToLowerInvariant() : "";
                        Color fallbackTint = FamilyFallbackTint(def, model);
                        warbandFixer.SetFallbackTint(fallbackTint);
                        // Same colour as the MISS floor, so a slot the fixer cannot texture and a
                        // slot the fixer never sees agree instead of drifting apart.
                        warbandFixer.SetMissTint(fallbackTint);
                        FlowTrace.Step("Enemy",
                            $"garrison fallback TINT {fallbackTint} bound to '{model}' (id '{tId}', rig {rigForModel}) — " +
                            "no OrcTex basecolor for Warband/Troll family, paints solid colour not white " +
                            "(grunt arm = WO-956 hostile-palette placeholder, never the green axis)");
                        }
                    }
                }

                // ⛔ COLOUR-VERIFY THE FINAL BODY (owner report 2026-08-20, "enemies not having
                // coloring"). Everything above is INTENT — "bind this atlas", "bind that tint".
                // Nothing above proves the body the player sees ends up coloured, and for every
                // rig OUTSIDE the three branches above (HumanoidMedium / SkeletonHumanoid — 16 of
                // the 20 enemy spawns in the pid-6783 capture) there is no colour code at ALL.
                // This guard reads the FINAL materials one frame later — after TripoMaterialFixer's
                // own Awake/Start rebuild — names any white/grey slot in the trace, and repaints it.
                // Armed HERE, at the single skin choke point, so the late re-skin gets it too.
                EnemyBodyColorGuard.Arm(vis, def, model, rigForModel.ToString(), FamilyFallbackTint(def, model));

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
            return vis;
        }

        /// <summary>
        /// The tinted placeholder body. Hittable and visible, so a spawner never ships a ghost —
        /// but it is a PLACEHOLDER, and EnemyLateSkinner is responsible for replacing it.
        /// </summary>
        internal static void AddCapsuleFallback(GameObject go, float sizeScale)
        {
            if (go == null) return;
            var cap = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            // NAMED, deliberately: EnemyLateSkinner finds and destroys the placeholder by this
            // exact name when the real body arrives. A rename here orphans a capsule INSIDE the
            // re-skinned enemy, so the two constants live together.
            cap.name = CapsuleName;
            if (cap.TryGetComponent(out Collider cc)) Object.Destroy(cc);
            cap.transform.SetParent(go.transform, false);
            cap.transform.localPosition = new Vector3(0f, 0.9f * sizeScale, 0f);
            cap.transform.localScale = Vector3.one * sizeScale;
            TintCapsule(cap.GetComponent<Renderer>());

            // PILL-SWAP MITIGATION (owner report 2026-08-20: "One enemy came in as a pill then
            // switched to enemy"). Hold the placeholder INVISIBLE for a short grace, so a family
            // bundle that lands quickly is swapped in with no pill ever on screen. Armed on the
            // CAPSULE, not on EnemyLateSkinner — the skinner is not armed for a proven-missing
            // model, and a capsule that is never revealed would be an invisible-but-hittable
            // enemy. See EnemyPlaceholderReveal for why the window cannot be closed entirely.
            EnemyPlaceholderReveal.Arm(cap);
        }

        /// <summary>Name of the placeholder capsule child. Shared with EnemyLateSkinner, which
        /// destroys it on a successful re-skin.</summary>
        internal const string CapsuleName = "PlaceholderCapsule";

        /// <summary>
        /// ROOT-CAUSE TRACE (MOST IMPORTANT): the model failed to load/skin OR loaded but did not
        /// render, so this body becomes a tinted capsule — silent variety loss. If many families hit
        /// this, every enemy looks the same despite varied ids.
        /// <para>⛔ IT MUST SAY WHY, because the two causes need different fixes and used to read
        /// identically. NOT-YET-DOWNLOADED is transient and re-skins itself; GENUINELY-MISSING is
        /// permanent and someone has to ship the address. EnemyAssetLoader has already logged the
        /// authoritative [Flow:EnemyAssets] line with the catalog state; this one names the ENEMY.</para>
        /// </summary>
        private static void ReportNoRenderableMesh(EnemyDef def, string model)
        {
            string address = DeNelle.Core.EnemyAssetLoader.EnemyAddrPrefix + model;
            string family  = DeNelle.Core.EnemyContentWarmer.FamilyOf(model);
            bool registered = DeNelle.Core.EnemyContentWarmer.IsRegisteredAddress(address);
            bool settled    = DeNelle.Core.EnemyContentWarmer.IsSettled;
            bool missing    = DeNelle.Core.EnemyContentWarmer.IsKnownAbsent<GameObject>(address) ||
                              (settled && !registered);
            string id = def != null ? def.Id : "?";

            if (missing)
            {
                // Permanent. No amount of waiting fixes it, and there is no Resources copy left to
                // catch it (Assets/Resources/Enemies was deleted by the CDN migration).
                FlowTrace.Fail("Enemy",
                    $"model '{model}' (id '{id}') has NO renderable mesh at '{address}' and the asset is " +
                    $"GENUINELY MISSING (registeredInCatalog={registered}, catalogState=" +
                    $"{DeNelle.Core.EnemyContentWarmer.State}) — FALLBACK to tinted capsule, and it will NOT " +
                    "re-skin. Ship this address in the enemy Addressable group. This is a VISUAL defect; " +
                    "the enemy still spawns, moves and fights.");
            }
            else
            {
                FlowTrace.Warn("Enemy",
                    $"model '{model}' (id '{id}') has no renderable mesh at '{address}' YET — the family " +
                    $"'{family}' bundle is NOT YET DOWNLOADED (familyDownloading=" +
                    $"{DeNelle.Core.EnemyContentWarmer.IsFamilyDownloading(family)}, catalogState=" +
                    $"{DeNelle.Core.EnemyContentWarmer.State}). Spawning the tinted capsule NOW and " +
                    "RE-SKINNING when it lands — deliberately not waiting, because waiting on this seam is " +
                    "what deadlocked the game on 2026-08-20.");
            }
        }

        // WO-791: seat a spawn position on the baked NavMesh — REPAIR, not just report.
        // Progressive radii so a stale bake / prop-carved footprint still seats nearby;
        // on total failure the position is raycast-grounded so the body can never hover,
        // and the miss self-reports via FlowTrace (captured by the F8 break harness).
        private static Vector3 SeatSpawnOnNavMesh(Vector3 want, EnemyDef def)
        {
            string id = def != null ? def.Id : "enemy";
            float[] radii = { 6f, 12f, 24f };
            for (int i = 0; i < radii.Length; i++)
            {
                if (NavMesh.SamplePosition(want, out NavMeshHit hit, radii[i], NavMesh.AllAreas))
                {
                    if (i > 0)
                        FlowTrace.Warn("Enemy",
                            $"spawn for '{id}' at {want} missed the 6m NavMesh snap — wide snap ({radii[i]}m) " +
                            $"seated it at {hit.position} (moved {(hit.position - want).magnitude:F1}m). " +
                            "Check the spawn point / bake.");
                    return hit.position;
                }
            }

            // No NavMesh anywhere near: the agent WILL be off-mesh and hold (Enemy.cs
            // degrades to idle). At minimum kill the FLOAT: seat the body on the first
            // solid surface under the spawn so it stands on the ground while broken.
            Vector3 grounded = want;
            if (Physics.Raycast(want + Vector3.up * 30f, Vector3.down, out RaycastHit groundHit, 120f,
                                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                grounded = groundHit.point;
            FlowTrace.Warn("Enemy",
                $"NO baked NavMesh within 24m of spawn {want} for '{id}' — agent will hold position (frozen). " +
                $"Body grounded at {grounded} so it does not float. FIX THE BAKE / spawn point.");
            return grounded;
        }

        /// <summary>
        /// The 2026-08-09 AccuRig intake. These share ONE export convention with the orcs and
        /// Demon/OgreMage — CC_Base Humanoid, +X-forward, raw Tripo materials — so they all need
        /// the same -90 yaw and the runtime Tripo→URP fixer. Listed by NAME rather than by rig
        /// class because rig class does not imply export convention: the KayKit Skeleton_*/Boss
        /// rigs already face +Z and must NOT be rotated.
        /// </summary>
        private static readonly System.Collections.Generic.HashSet<string> AccuRigIntake =
            new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal)
            {
                "Troll", "Troll_Mage", "Troll_Overlord", "Skeleton_Golem_NEW", "Necromancer_NEW",
                // Orc_Warlord + Orc_Mage are in the intake too, but resolve to the OrcHumanoid rig,
                // which already applies the same yaw + fixer in the branch above.

                // 2026-08-20 intake: the two owner-delivered AccuRig bodies that retire the KayKit
                // Skeleton_Minion stand-in. Same CC_Base +X-forward export as the rest of this set.
                // ⚠ THESE ARE HERE DESPITE ROUTING TO THE SkeletonHumanoid RIG, and that is the whole
                // point of the doc comment above: rig class is which CLIPS a body plays, the intake is
                // which way its mesh FACES. The KayKit Skeleton_* bodies share the controller and face
                // +Z; these two share the controller and face +X. Judging either by the other is how a
                // body ends up standing sideways in the raid.
                "Hollow_Walker", "Cellar_Hollow",
            };

        /// <summary>
        /// Resolves a model's real basecolor, preferring the 2026-08-09 TripoTex maps over the older
        /// OrcTex ones. Returns null when neither exists, which is the signal to fall back to a tint.
        ///
        /// ⚠ TEXTURE AND TINT ARE EXCLUSIVE. TripoMaterialFixer MULTIPLIES tint over the texture
        /// (TripoMaterialFixer.cs:331), so binding both renders the authored skin green-multiplied —
        /// which is precisely how good art comes to look like a material bug. Callers bind ONE.
        ///
        /// TripoTex wins on collision (Orc_Mage exists in both): the mesh was REPLACED on 2026-08-09,
        /// so the older OrcTex atlas no longer matches its UVs.
        /// </summary>
        public static string ResolveBasecolor(string model)
        {
            if (string.IsNullOrEmpty(model)) return null;

            string hit = TryBasecolor(model);
            if (hit != null) return hit;

            // NO SILENT TINT for a model we shipped art for. The caller's fallback is a solid
            // colour, which looks like a deliberate style choice rather than a missing lookup —
            // so an intake model with no resolvable skin says so ONCE, by name, in the trace.
            // WO-1129: the miss now NAMES EVERY CANDIDATE IT TRIED. A miss that does not say
            // what it looked for is what sent a seat to the owner with "no texture anywhere in
            // the project" on 2026-08-20, when the art was in a folder nobody had listed.
            if (AccuRigIntake.Contains(model))
                FlowTrace.Once("Enemy", "basecolor-miss-" + model,
                    $"model '{model}' has NO resolvable basecolor — falling back to a SOLID TINT. " +
                    "The authored skin is not being used. " +
                    DeNelle.Core.EnemyArtPaths.DescribeCandidates(model));
            return null;
        }

        /// <summary>
        /// One basecolor probe, DERIVED — never a typed path (WO-1129 §3.1/§3.3).
        /// <para>The candidate list (atlas folder precedence, the "_NEW" alias, the "_basecolor"
        /// suffix) lives in ONE place: <see cref="DeNelle.Core.EnemyArtPaths"/>. It used to live
        /// here AND, independently, in EnemyArtCoverageRegression, whose agreement with this
        /// method was asserted only by a COMMENT — the duplicated-state failure CLAUDE.md
        /// catalogues in §2, §5 and §16. The oracle and the runtime now read the same array, so
        /// a pass in edit mode means the same thing it means on screen BY CONSTRUCTION.</para>
        /// <para>⚠ ORDER IS FOLDER-MAJOR: TripoTex(name), TripoTex(name-minus-_NEW),
        /// OrcTex(name), OrcTex(name-minus-_NEW). TripoTex wins on collision because the
        /// 2026-08-09 mesh replacement means the older OrcTex atlas no longer matches those UVs.
        /// (This is a deliberate refinement of the previous name-major order; no live model is
        /// affected, since no "_NEW" model has an OrcTex entry under its suffixed name.)</para>
        /// </summary>
        private static string TryBasecolor(string name)
        {
            var candidates = DeNelle.Core.EnemyArtPaths.ResourceCandidates(name);
            for (int i = 0; i < candidates.Count; i++)
            {
                string key = candidates[i];
                if (DeNelle.Core.HeroTextureLoader.Load(key, optional: true) != null) return key;
            }
            return null;
        }

        /// <summary>Enemy id/role → skeleton model. Grouped by family (Hollow/Skeleton Legion,
        /// Orc Warband, Troll/Stonebelly, etc.) with class variety (Tank=brute/golem,
        /// DPS=rogue/warrior, Healer=mage/shaman). Basic strategy in EnemyBrain (DPS
        /// focus-fire healers first, Tank protects, Healer prioritizes allies).
        /// Chosen by role/size for silhouette. Swap to bespoke when packs land.</summary>
        public static string ModelForEnemy(EnemyDef def)
        {
            string id = def != null ? def.Id : null;

            // ── HOLLOW ONES → EnemyResolver (WO-772 Phase 1, ratified 2026-07-26) ─────
            // THE GENERIC-SKELETON FIX: every APPROVED Hollow id (incl. the ones that
            // used to fall through to the size DEFAULT — hollow-mage / hollow-reaper /
            // hollow-brute / cellar-hollow / the canon mini-boss hollow-apprentice) now
            // resolves through the ONE shared family/class table to its OWN model, so
            // distinct ids never collapse to one generic Skeleton_Minion / _Golem again.
            // Data wins (A4): enemies.json's modelKey overrides the table when it names a
            // committed Hollow mesh. Non-Hollow ids return false and keep the paths below.
            if (EnemyResolver.TryResolveHollowModel(id, def != null ? def.ModelKey : null, out string hollowModel))
            {
                FlowTrace.Once("Enemy", $"resolve-{id}",
                    $"EnemyResolver: Hollow id '{id}' -> model '{hollowModel}' (distinct — no generic-skeleton fallback)");
                return hollowModel;
            }

            // ── DATA FIRST for EVERY OTHER FAMILY (WO-954) ───────────────────────────
            // The Hollows have honoured enemies.json's modelKey since WO-772; every other
            // family did NOT — the switch below won outright, so a row could say "OgreMage"
            // while the code returned "Orc_Shaman" and nothing anywhere failed. That silent
            // data/code divergence is the bug WO-954 names. enemies.json is now the FIRST
            // authority for all families; the switch below is demoted to the last-resort
            // fallback for ids with no row (synthesised roamers/bosses) and for rows whose
            // key names art that is not committed.
            //
            // BEHAVIOUR-PRESERVING as seeded (verified row-by-row against the 19 enemies.json
            // rows on 2026-08-14): every row whose key IS committed already agreed with the
            // switch, and the one row that disagrees ('ogre' -> "OgreMage") is rejected by the
            // registry gate and keeps its documented Orc_Shaman stand-in. Deliberate: the
            // first commit changes no pixel, it only moves who decides.
            if (EnemyResolver.TryResolveDataModel(id, def != null ? def.ModelKey : null,
                                                  out string dataModel, out string dataReject))
            {
                FlowTrace.Once("Enemy", $"data-model-{id}",
                    $"ModelForEnemy: id '{id}' -> model '{dataModel}' from the enemies.json modelKey (data is the authority).");
                return dataModel;
            }
            // §1.4b: a rejected/absent data key NEVER logs a hollow "model load failed" — the
            // reason names the id, the key it tried, and why the code table won instead. Only
            // an actually-present-but-rejected key is a Warn; "no row" is the normal case for a
            // synthesised def and stays out of the warning channel.
            if (def != null && !string.IsNullOrEmpty(def.ModelKey))
                FlowTrace.Once("Enemy", $"data-model-reject-{id}-{def.ModelKey}",
                    "ModelForEnemy: " + dataReject);

            switch (id)
            {
                // (Hollow Ones are resolved ABOVE via EnemyResolver — the old hard-cased
                // hollow-walker/warrior/rogue/acolyte/necromancer cases moved into the
                // shared table so the mini-boss + mage/reaper/brute/cellar resolve too.)

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
                // 2026-08-09: the warlord gets its OWN mesh. It previously borrowed
                // Orc_Necromancer, so the raid boss shared a silhouette with a camp elite the
                // player also fights — the boss read as a re-skin rather than an escalation.
                case "orc-warlord":      return "Orc_Warlord";       // outpost raid boss — dedicated mesh

                // ── BLINK STYLIZED ORCS (WO-680) — vendor Humanoid family, ADDITIVE ─
                // Staged by BlinkOrcImporter into Resources/Enemies/Blink/ (committed
                // mirrors — never a direct Assets/Blink reference; the pack is gitignored).
                // Side-by-side with the Tripo orcs above for an in-game felt-compare:
                // NO existing spawner emits these ids — they spawn only via the
                // DevHotkeys-gated EnemyFamilyTestSpawner 'B' compare, or a future
                // data-table entry, so live balance is untouched.
                case "blink-orc-warrior": return "Blink/Blink_Orc_Warrior"; // melee DPS
                case "blink-orc-hunter":  return "Blink/Blink_Orc_Hunter";  // ranged/skirmisher silhouette
                case "blink-orc-warlock": return "Blink/Blink_Orc_Warlock"; // caster
                case "blink-orc-boss":    return "Blink/Blink_Orc_Boss";    // boss (own 22-clip set)

                // ── BRUTES / OGRES / BOSSES ──────────────────────────────────────
                // STAND-INS (no Troll.fbx / OgreMage.fbx in Resources/Enemies — those render
                // as tinted capsules): reuse EXISTING OrcWarband-rig orc models until real
                // Tripo troll/ogre art lands. troll → big Orc_Berserker, ogre → Orc_Shaman.
                // Both go through the OrcWarband SetFallbackTint path below, so the tint block
                // paints troll grey-green and ogre grey to keep them visually distinct.
                // 2026-08-09: the stand-in is RETIRED. Troll.fbx now exists (AccuRig, Humanoid,
                // LargeHumanoid controller) and enemies.json has asked for modelKey "Troll" the
                // whole time — the data was already correct, only the mesh was missing.
                case "troll":            return "Troll";             // real Cave Troll
                // The troll family's two casters SHARE one mesh on purpose — Troll_Mage is the
                // only caster silhouette the family has, and the pair is differentiated by stats
                // and role (DPS vs healer), not by model. Distinct ids so a spawner can pick one
                // without the other, and so the healer can be focus-fired as its own target.
                case "troll-mage":       return "Troll_Mage";        // DPS caster
                case "troll-shaman":     return "Troll_Mage";        // healer / buffer
                case "troll-overlord":   return "Troll_Overlord";    // camp boss
                case "ogre":             return "Orc_Shaman";        // STAND-IN: ogre brute (was "OgreMage" — missing)
                case "ogre-mage":        return "Orc_Shaman";        // STAND-IN: ogre caster (was "OgreMage" — missing)
                case "demon":            return "Demon";             // demon
                // Apex flyer. The licensed Asset-Store dragon (product 71047, source at
                // Assets/Dragon) ships as Resources/Enemies/Boss_Dragon.prefab, built by
                // DragonAnimatorSetup (WO-760). The old 3DHaupt CC-BY-NC "Dragon" fbx was
                // RETIRED 2026-07-24 — resolve the dragon keys to the licensed prefab.
                case "boss-dragon":      return "Boss_Dragon";        // apex flyer -> licensed rig
                case "dragon":           return "Boss_Dragon";
            }

            // ── FAMILY FALLBACK ──────────────────────────────────────────────────
            // Any spawner that DID set a Family (garrison/tribe set orc/tribe/beast/
            // cult) but used an id not cased above still reads as its faction rather
            // than collapsing to a skeleton. All targets are verified Resources files.
            string family = def != null ? (def.Family ?? "").Trim().ToLowerInvariant() : "";
            switch (family)
            {
                case "orc":   return def != null && def.Role == "caster" ? "Orc_Shaman" : "Orc_Berserker";
                case "troll": return "Troll";          // real mesh as of 2026-08-09 (stand-in retired)
                case "ogre":  return "Orc_Shaman";      // STAND-IN (no OgreMage.fbx) — tinted grey below
                case "demon":
                case "cult":  return "Demon";
                case "dragon": return "Boss_Dragon";
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
            WeaponBoundsOrient.NormalizeInto(prop, gripRoot.transform, heldLength,
                WeaponBoundsOrient.GripAnchor.HiltEnd);
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
            gripRoot.transform.localRotation = EquipmentController.ApplyGlobalWeaponYaw(Quaternion.Euler(euler));
            gripRoot.transform.localPosition = pos;
            gripRoot.transform.localScale = Vector3.one * scale;
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

        /// <summary>
        /// SPAWN-INTENT PREWARM: ask for the family bundles of a roster BEFORE anything from it
        /// is built. Non-blocking and idempotent — it starts the fetch and returns immediately.
        /// <para>The pill window is download latency (0.6s–6.4s in the pid-6783 capture), and the
        /// fetch normally starts on the very frame the first body is skinned. A spawner that knows
        /// its roster earlier — a garrison whose composition is fixed when its room is authored —
        /// can start the same fetch sooner and shrink the window for free. It downloads exactly the
        /// families the roster will use, so it stays inside the owner's PER-FAMILY ruling: never
        /// the whole 64 MiB enemy set.</para>
        /// </summary>
        public static void PrewarmForIds(System.Collections.Generic.IEnumerable<string> enemyIds)
        {
            if (enemyIds == null) return;
            var seen = new System.Collections.Generic.HashSet<string>();
            foreach (string id in enemyIds)
            {
                if (string.IsNullOrEmpty(id)) continue;
                string m = ModelForEnemy(new EnemyDef { Id = id });
                if (string.IsNullOrEmpty(m) || !seen.Add(m)) continue;
                DeNelle.Core.EnemyAssetLoader.PrewarmFamily(m);
            }
            if (seen.Count > 0)
                FlowTrace.Step("Enemy",
                    $"spawn-intent PREWARM: asked for {seen.Count} enemy model famil(ies) [{string.Join(", ", seen)}] " +
                    "before the first body is built — the bundle fetch starts earlier, so the placeholder window " +
                    "is shorter. Non-blocking; per-family, never the whole enemy set.");
        }

        /// <summary>
        /// THE SINGLE AUTHORITY for "what colour is this enemy when it has no skin".
        /// <para>Extracted 2026-08-20 (owner: "enemies not having coloring"). It was previously
        /// an inline ternary buried in the OrcWarband arm of <see cref="TrySkinBody"/>, which is
        /// why the OrcHumanoid arm and every other rig had NO colour floor at all — the fallback
        /// existed, it just was not reachable from anywhere else. Three call sites now share it:
        /// the Warband fallback tint, the miss-tint floor on every fixer, and
        /// <see cref="EnemyBodyColorGuard"/>'s last-resort repaint — so a family cannot read as
        /// one colour on one path and another colour on the next.</para>
        /// <para>⛔ EVERY colour returned here is CHROMATIC by design (see
        /// <see cref="EnemyBodyColorGuard.ChromaFloor"/>) — an achromatic "fallback" is
        /// indistinguishable from the unpainted grey it is supposed to replace, so the guard
        /// would flag its own repair. EnemyTintRegression pins that.</para>
        /// </summary>
        public static Color FamilyFallbackTint(EnemyDef def, string model)
        {
            string tId  = def != null ? (def.Id ?? "").Trim().ToLowerInvariant() : "";
            string tFam = def != null ? (def.Family ?? "").Trim().ToLowerInvariant() : "";

            // STAND-IN TINT (no Troll.fbx / OgreMage.fbx yet): troll & ogre reuse an OrcWarband
            // orc model (see ModelForEnemy) but are DISTINGUISHED by tint so a player reads them
            // as a different foe, not just another orc. Keyed by def.Id/Family (the model is now
            // an orc, so "model == Troll" no longer fires on its own).
            bool isTroll = model == "Troll" || tId == "troll" || tId == "caveman" || tFam == "troll";
            bool isOgre  = tId == "ogre" || tId == "ogre-mage" || tFam == "ogre";
            // Warlord/Necromancer BOSS distinct from grunt orcs (owner F8 2026-07-10 "enemy green
            // needs fixed" — confirmed the BOSS): the flat G-dominant orc tint read as a material
            // defect on the prominent boss. Give it a dark, desaturated undead slate so it reads
            // as an elite necromancer, not flat-green error — by LUMINANCE (dark), colorblind-safe.
            bool isWarlord = model == "Orc_Necromancer" || tId == "orc-warlord" || tId == "orc-necromancer";

            // WO-956 (owner F8 seq 2269, 2026-08-10 "the one is green"): the old grunt arm was the
            // saturated G-dominant orc green (0.30, 0.42, 0.22) — a whole ENEMY BODY painted the
            // SAFE hue (owner is red/green colourblind; the 07-10 "enemy green needs fixed" ruling
            // had spared the grunts as "intended", and that F8 re-flagged exactly that). Hostile
            // never wears green: the grunt arm reads HostilePalette.PlaceholderBodyTint (umber
            // PLACEHOLDER — final hue = owner look pass). Troll/ogre/warlord stay: all three are
            // desaturated near-neutrals that fail the green-dominance margin (they read grey/slate).
            return isTroll   ? new Color(0.38f, 0.40f, 0.34f) :   // grey-green troll hide (near-neutral, reads grey)
                   isOgre    ? new Color(0.48f, 0.47f, 0.52f) :   // cold ogre grey
                   isWarlord ? new Color(0.22f, 0.20f, 0.26f) :   // Warlord/Necromancer boss — dark undead slate
                               HostilePalette.PlaceholderBodyTint; // Warband grunts — WO-956 umber placeholder
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
