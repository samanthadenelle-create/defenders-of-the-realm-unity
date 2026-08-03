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

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using DeNelle.Core.Combat;        // AnimParams - the canonical animator parameter vocabulary
using DeNelle.Core.Diagnostics;   // FlowTrace - [Flow:TroopVisual]

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
            string troopId = def != null ? def.Id : "troop";
            FlowTrace.Step("TroopVisual",
                $"id={troopId}: model resolved from def = '{model ?? "<none>"}' (yaw={(def != null ? def.ModelYaw : 0f)}) " +
                $"-> Resources 'Heroes/{model ?? "<none>"}'.");
            if (!string.IsNullOrEmpty(model))
            {
                var skinOpts = SkinOptions.Enemy(height);
                // Body facing is per-pack and authored on the def (Tripo bodies face +X → -90;
                // Supercyan humanoids face +Z → 0). Default -90 keeps legacy bodies correct.
                skinOpts.LocalRotation = Quaternion.Euler(0f, def.ModelYaw, 0f);
                vis = VisualFactory.Skin(go.transform, "Heroes/" + model, skinOpts);
            }

            // ANIMATOR (owner defect 2026-08-02: "raid troops slide / T-pose instead of walking
            // and attacking"). MUST run BEFORE the TroopController AddComponent below — AddComponent
            // runs Awake SYNCHRONOUSLY, and TroopController.Awake caches which of Speed/Attack/Hit/
            // Dead the bound controller declares. Bind after that and every param write is skipped
            // for the life of the troop.
            if (vis != null) ApplyTroopAnimator(vis, def, model);

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

        // =====================================================================
        //  ANIMATOR — the missing half of the troop body (owner defect 2026-08-02)
        // =====================================================================
        //
        // THE STRUCTURAL DIFFERENCE vs the paths that DO animate (this is the evidence, not a theory):
        //
        //   ENEMY      EnemyFactory.cs:174  -> EnemyAnimatorFactory.Apply(vis, model), which loads
        //                                     Resources/Enemies/<rig>.controller and assigns
        //                                     anim.runtimeAnimatorController (EnemyAnimatorFactory.cs:163).
        //   COMPANION  StoryCompanionInjector.cs:560-566 -> HeroAssetLoader.LoadHeroController(slug)
        //                                     then anim.runtimeAnimatorController = ctrl.
        //   HERO       HeroBodySwapper.cs:547 -> anim.runtimeAnimatorController = controller.
        //   TROOP      TroopFactory.Build     -> NOTHING. A grep for `runtimeAnimatorController =`
        //                                     across Assets/_Modules returns ZERO hits under Troops/.
        //
        // That gap produces the symptom two different ways, one per art pack in troops.json:
        //
        //   (A) model "Knight"  (troop-shieldguard, troop-echo-legionnaire)
        //       Resources/Heroes/Knight.fbx is a MODEL prefab. Its Animator carries the generated
        //       Humanoid avatar but m_Controller is None — a model prefab never ships a controller.
        //       So runtimeAnimatorController == null, TroopController.cs:292 short-circuits the
        //       parameter scan, and _hasSpeed/_hasAttack/_hasHit/_hasDead all stay FALSE. The rig
        //       holds its bind pose while the NavMeshAgent slides the root. Meanwhile
        //       Resources/Heroes/Knight.controller EXISTS and declares exactly Speed/Attack/Hit/Dead
        //       — it was simply never loaded on this path.
        //
        //   (B) model "SC_Footman" / "SC_Archer" (footman, archer, spearman, outrider, battlemage)
        //       These are prefab VARIANTS of the Supercyan pack bodies (SupercyanResourceWire.cs),
        //       so they DO ship an Animator with a bound controller — Supercyan's own
        //       StrafeMovement.controller. Its parameter list is MoveHorizontal / MoveVertical /
        //       Grounded / MoveState / WeaponFire / IsDead / TakingHit1 ... It declares NO "Speed",
        //       NO "Attack", NO "Hit" and NO "Dead". Same outcome by a different route: the four
        //       flags in TroopController.Awake stay false, TroopController.cs:330 never writes a
        //       float, TroopController.cs:425/442/458 never fire a trigger, and the state machine
        //       sits in whatever its default state is (with Grounded defaulting to false) while the
        //       agent slides the body. SupercyanResourceWire.cs:16 asserts "the idle/walk/attack
        //       clips play once the NavMeshAgent drives position" — that assertion is FALSE, nothing
        //       drives that controller's parameters.
        //
        // THE FIX binds a controller whose parameters ARE the vocabulary TroopController already
        // speaks (AnimParams). All four Resources/Heroes controllers (Knight/Ranger/Cleric/Mage)
        // declare the full Speed/InCombat/Attack/Combo/Cast/WindUp/Block/Hit/HitDir/Dead/DeathDir/
        // Victory/Injured set, and their clips are Humanoid — which retargets onto ANY Humanoid
        // avatar, exactly how EnemyAnimatorFactory shares one Mixamo controller across the whole orc
        // family. The Supercyan rig is Humanoid (its FBX meta is animationType: 3), so a hero
        // controller poses it. An already-driveable controller is KEPT, never clobbered.
        private static void ApplyTroopAnimator(GameObject vis, TroopDef def, string model)
        {
            if (vis == null) return;
            string id = def != null ? def.Id : "troop";
            string what = $"id={id} model='{model ?? "<none>"}'";

            try
            {
                var anim = vis.GetComponentInChildren<Animator>(true);
                if (anim == null)
                {
                    anim = vis.AddComponent<Animator>();
                    FlowTrace.Warn("TroopVisual",
                        $"{what}: skinned body shipped NO Animator - added one on the visual root " +
                        "(a rig-less prop model would end up here; the troop still fights).");
                }
                var avatar = anim.avatar;
                bool avatarValid = avatar != null && avatar.isValid;
                FlowTrace.Step("TroopVisual",
                    $"{what}: animator present on '{anim.gameObject.name}' isHuman={anim.isHuman} " +
                    $"avatar='{(avatar != null ? avatar.name : "<null>")}' avatarValid={avatarValid} " +
                    $"boundController='{(anim.runtimeAnimatorController != null ? anim.runtimeAnimatorController.name : "<null>")}'.");

                // Root motion OFF: the locomotion clips carry baked root curves that would fight the
                // NavMeshAgent.Move() displacement TroopController.MoveToward applies to the ROOT
                // (the same reason HeroBodySwapper.cs:553 and EnemyAnimatorFactory.cs:129 clear it).
                // ELIMINATED as a cause of this defect, not a fix for it: both packs already ship
                // applyRootMotion = 0 — asserted here so it can never silently regress.
                anim.applyRootMotion = false;
                // Off-screen culling, matching every other spawned body (EnemyAnimatorFactory.cs:138).
                // NOT CullCompletely — that can desync gameplay-driven anim events.
                anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

                // Already driveable? Keep it — never clobber a controller that speaks our vocabulary.
                if (HasDriveParams(anim))
                {
                    FlowTrace.Step("TroopVisual",
                        $"{what}: bound controller '{anim.runtimeAnimatorController.name}' already declares " +
                        $"'{AnimParams.Speed}' - kept as-is (no rebind needed).");
                    return;
                }

                string rejected = anim.runtimeAnimatorController != null
                    ? anim.runtimeAnimatorController.name
                    : "<null>";

                foreach (string cand in ControllerCandidates(def, model))
                {
                    var ctrl = Resources.Load<RuntimeAnimatorController>("Heroes/" + cand);
                    FlowTrace.Step("TroopVisual",
                        $"{what}: controller candidate 'Heroes/{cand}' -> {(ctrl == null ? "NULL" : ctrl.name)}.");
                    if (ctrl == null) continue;

                    anim.runtimeAnimatorController = ctrl;
                    if (!HasDriveParams(anim))
                    {
                        // Loaded, but it does not speak AnimParams either — do not ship a controller
                        // nothing can drive; say so and keep looking.
                        FlowTrace.Warn("TroopVisual",
                            $"{what}: candidate 'Heroes/{cand}' loaded but declares no '{AnimParams.Speed}' " +
                            "parameter - TroopController could not drive it; trying the next candidate.");
                        continue;
                    }

                    // Rebind reconnects the bone references after the runtime Instantiate, else the
                    // mesh T-poses for ~10 frames after the swap (HeroBodySwapper.cs:574).
                    anim.Rebind();

                    FlowTrace.Step("TroopVisual",
                        $"{what}: controller ASSIGNED 'Heroes/{cand}' (replaced '{rejected}' which declared no " +
                        $"'{AnimParams.Speed}'); applyRootMotion=false, culling=CullUpdateTransforms.");

                    // AVATAR VALIDITY — the second half of "why it T-poses". A Humanoid clip can ONLY
                    // pose a rig through a valid Humanoid avatar; with none, the Animator holds the
                    // bind pose while the agent slides it. Self-report LOUDLY (do NOT hide the troop —
                    // it must still spawn damageable). Mirrors EnemyAnimatorFactory.cs:172-192.
                    bool humanoidClips = ControllerHasHumanoidClips(ctrl);
                    if (anim.isHuman && !avatarValid)
                        FlowTrace.Fail("TroopVisual",
                            $"{what}: controller 'Heroes/{cand}' bound but the Animator has NO valid Humanoid " +
                            $"avatar (avatar={(anim.avatar == null ? "<null>" : "invalid")}) - a humanoid clip " +
                            "will hold the bind/T-pose while the agent slides it (the sliding-statue path). " +
                            "Re-import the model as Humanoid with a valid avatar.");
                    else if (!anim.isHuman && humanoidClips)
                        FlowTrace.Fail("TroopVisual",
                            $"{what}: rig is GENERIC but controller 'Heroes/{cand}' carries Humanoid clips - " +
                            "a Humanoid clip cannot pose a Generic rig at all, so this troop WILL T-pose. " +
                            "Re-import the model as Humanoid.");
                    else
                        FlowTrace.Step("TroopVisual",
                            $"{what}: avatar OK for 'Heroes/{cand}' (isHuman={anim.isHuman} " +
                            $"humanoidClips={humanoidClips}) - rig should pose.");

                    // Deferred pose-verify: samples a real bone over ~8 frames and FAILS LOUD under
                    // [Flow:EnemyPose] if nothing ever drives the rig. Shared with the enemy path
                    // (same assembly, same failure mode) rather than duplicated — so "troop bound a
                    // controller but is still frozen" self-reports instead of costing an owner cycle.
                    EnemyPoseVerifier.Attach(vis, anim, model, cand, humanoidClips);
                    return;
                }

                // Nothing resolved. This is the state the troop shipped in BEFORE this fix, so name it
                // exactly — a headless raid run must never leave "no animation" un-attributed again.
                FlowTrace.Fail("TroopVisual",
                    $"{what}: NO animator controller resolved from Resources/Heroes (tried " +
                    $"{string.Join(", ", ControllerCandidates(def, model))}); the bound controller " +
                    $"'{rejected}' declares no '{AnimParams.Speed}'. This troop WILL slide with no walk/" +
                    "attack/death animation. Run the hero animator setup so Resources/Heroes/*.controller exist.");
            }
            catch (System.Exception e)
            {
                // A missing/gitignored art pack must never take the spawn down — the troop still
                // needs to exist and fight. Warn + continue, never an error, never a throw.
                FlowTrace.Warn("TroopVisual",
                    $"{what}: animator setup threw {e.GetType().Name}: {e.Message} - troop spawns un-animated but playable.");
            }
        }

        /// <summary>
        /// Controllers to try, most specific first. A troop model that HAS a same-named controller
        /// (Knight -> Resources/Heroes/Knight.controller) uses its own; everything else falls back to
        /// the shared humanoid controller for its ROLE (ranged -> Ranger, melee -> Knight), which is
        /// data-driven off troops.json rather than a per-model hard-coded table. All four hero
        /// controllers declare the identical AnimParams set, so any of them can drive a troop.
        /// </summary>
        private static string[] ControllerCandidates(TroopDef def, string model)
        {
            string role = (def != null && def.Role != null) ? def.Role.Trim().ToLowerInvariant() : "melee";
            string roleCtrl = role == "ranged" ? "Ranger" : "Knight";
            // Deduped, order preserved: a duplicate candidate would double the Resources.Load and the
            // trace line for no gain, and "tried Knight, Knight" reads like a bug in the log.
            var list = new List<string>(3);
            if (!string.IsNullOrEmpty(model)) list.Add(model);
            if (!list.Contains(roleCtrl)) list.Add(roleCtrl);
            if (!list.Contains("Knight")) list.Add("Knight");   // last resort: always a driveable controller
            return list.ToArray();
        }

        /// <summary>
        /// True when the animator's CURRENTLY BOUND controller declares the canonical
        /// <see cref="AnimParams.Speed"/> float — i.e. TroopController can actually drive it.
        /// Speed is the load-bearing one: it is the only per-frame write (TroopController.cs:330),
        /// and every controller the project builds that declares Speed also declares Attack/Hit/Dead.
        /// </summary>
        private static bool HasDriveParams(Animator anim)
        {
            if (anim == null || anim.runtimeAnimatorController == null) return false;
            var ps = anim.parameters;
            if (ps == null) return false;
            for (int i = 0; i < ps.Length; i++)
                if (ps[i] != null && ps[i].nameHash == AnimParams.SpeedHash) return true;
            return false;
        }

        /// <summary>True if any clip on the controller is Humanoid motion — such a clip can only
        /// pose the rig through a valid Humanoid avatar (a Generic rig T-poses).</summary>
        private static bool ControllerHasHumanoidClips(RuntimeAnimatorController ctrl)
        {
            if (ctrl == null) return false;
            var clips = ctrl.animationClips;
            if (clips == null) return false;
            for (int i = 0; i < clips.Length; i++)
                if (clips[i] != null && clips[i].humanMotion) return true;
            return false;
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
