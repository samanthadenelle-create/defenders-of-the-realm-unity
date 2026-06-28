// =============================================================================
// AtbCombatantSwapper — replace the ATB battle's placeholder capsule "pills" with
// real combatant visuals (owner: "still pills on ATB").
// -----------------------------------------------------------------------------
// BattleController renders the two combatants as plain capsule meshes
// (HeroCapsule / EnemyCapsule). This self-installs when ATBBattle.unity loads
// (RuntimeInitializeOnLoadMethod + sceneLoaded — no scene edit) and:
//   • HERO  → instantiates the player's class FBX (Resources/Heroes/<Class>.fbx,
//             class read from GameState by reflection), sized to the capsule,
//             facing the enemy, materials URP-fixed via DeNelle.Core.TripoMaterial-
//             Fixer; the capsule pill's own renderer is hidden. The model is a
//             CHILD of the capsule, so BattleController's death-tilt still works.
//   • ENEMY → tinted Hollow-One violet (there is NO runtime enemy model in
//             Resources — the KayKit skeleton lives in the gitignored Assets/Models
//             and is edit-time only; tinting avoids bloating the repo).
//
// Reflection is used for GameState + TripoMaterialFixer so this carries no extra
// asmdef dependency and no-ops safely if anything is absent.
// =============================================================================

using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using DeNelle.Core.State;   // direct hero-class read (see ResolveHeroSlug)
using DeNelle.Core.Diagnostics;   // FlowTrace — instrument the swap seams + verify-before-hide
using DeNelle.BattleATB.Engine;   // Defs.ENEMY_DEFS — validate breach ids against engine keys

namespace DeNelle.BattleATB
{
    /// <summary>WO-481 combat-pivot: the V1 prototype encounter — the Orc FAMILY (leader first,
    /// then followers). BattleController.BuildEnemyRoster (engine side) and AtbCombatantSwapper
    /// (visual side) BOTH read this so the engine roster and the staged models stay in sync.</summary>
    public static class AtbPrototypeEncounter
    {
        public static readonly string[] OrcFamily = { "orc-warrior", "orc-tank", "orc-mage" };
    }

    /// <summary>Swaps the ATB placeholder capsules for real combatant visuals.</summary>
    public static class AtbCombatantSwapper
    {
        private static bool s_hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            if (s_hooked) return;
            s_hooked = true;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TrySwap(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene s, LoadSceneMode mode) => TrySwap(s);

        private static void TrySwap(Scene scene)
        {
            if (!scene.IsValid()) return;
            if (scene.name.IndexOf("ATBBattle", StringComparison.OrdinalIgnoreCase) < 0) return;

            // WO-381 #1 — keep the arena CLEAN: deactivate any stray world visuals
            // (the village hero, story companions, roaming mobs/pets) that rode into
            // the ATB scene via DontDestroyOnLoad. Only the two ATB combatant capsules
            // (+ their swapped models) should be visible — no village structures/props.
            HideStrayWorldVisuals();

            var hero = GameObject.Find("HeroCapsule");
            var enemy = GameObject.Find("EnemyCapsule");
            if (hero != null) SwapHero(hero.transform);
            if (enemy != null) SwapEnemy(enemy.transform, hero != null ? hero.transform : null);
        }

        // WO-381 #1 — the set of DeNelle.Village component types whose GameObjects are
        // STRAY WORLD VISUALS that must not appear in the clean ATB arena. Each rides
        // into the battle scene via DontDestroyOnLoad (the player hero is locomotion-
        // controllable; companions + roaming mobs + harvest pets keep wandering). We
        // deactivate their GameObjects so only the two ATB combatants remain; the
        // village re-activates / re-spawns them when its scene reloads (HeroControl-
        // Ensurer for the hero, StoryCompanionInjector for companions, the spawners
        // for mobs/pets). Resolved by name via reflection — BattleATB must not
        // reference DeNelle.Village.
        private static readonly string[] StrayVisualTypeNames =
        {
            "DeNelle.Village.HeroLocomotion",            // the player hero pill
            "DeNelle.Village.StoryCompanion",            // join-order story companions
            "DeNelle.Village.Enemy",                     // any roaming village/world enemy
            "DeNelle.Village.PetContextualBehaviour",    // roaming world pets
            "DeNelle.Village.Worker",                    // harvest workers
        };

        /// <summary>
        /// Deactivate every stray world visual (village hero, companions, roaming mobs,
        /// pets) that survived the scene swap via DontDestroyOnLoad, so the ATB arena
        /// shows ONLY the two turn-based combatants — no village structures/props/extras.
        /// Best-effort + null-safe; never blocks the ATB load. (WO-381 #1.)
        /// </summary>
        private static void HideStrayWorldVisuals()
        {
            foreach (var typeName in StrayVisualTypeNames)
            {
                try
                {
                    var t = FindType(typeName);
                    if (t == null) continue;
                    var found = UnityEngine.Object.FindObjectsByType(t, FindObjectsSortMode.None);
                    if (found == null) continue;
                    foreach (var obj in found)
                    {
                        if (obj is Component c && c != null && c.gameObject.activeSelf)
                        {
                            c.gameObject.SetActive(false);
                            Debug.Log("[AtbCombatantSwapper] WO-381: hid stray world visual in ATB: " + c.gameObject.name);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // best-effort — never block the ATB load, but SELF-REPORT (§12) so a stray
                    // village visual that failed to hide is captured, not silently swallowed.
                    FlowTrace.Warn("AtbSwap",
                        $"HideStrayWorldVisuals: '{typeName}' sweep threw {ex.GetType().Name}: {ex.Message} — skipped.");
                }
            }
        }

        // ── Hero: capsule pill → real class model ────────────────────────────
        private static void SwapHero(Transform capsule)
        {
            using var _ = FlowTrace.Enter("AtbSwap", "SwapHero");
            if (capsule.Find("AtbHeroModel") != null) return;   // already swapped

            string slug = ResolveHeroSlug();
            // WO-545 Tier-1 seam: Addressables-first, Resources-fallback (V1-safe — when no hero
            // address is registered this resolves to the exact same Resources/Heroes/<slug> as before).
            var prefab = DeNelle.Core.HeroAssetLoader.LoadHeroPrefab(slug);
            if (prefab == null)
            {
                // No class FBX in Resources — keep the capsule pill shown (never an empty slot).
                FlowTrace.Fail("AtbSwap",
                    $"SwapHero: Resources/Heroes/{slug} not found — keeping the placeholder capsule (no invisible combatant).");
                return;
            }

            // Ensure the canonical ActorAnimator driver (IActorAnimator) is on the logical root
            // so battle actions can drive PlayAttack/PlayCast/PlayHit/Die for immersive knight swings,
            // mage casts, hit flinches, and death falls. Matches EnemyFactory / HeroBodySwapper.
            if (capsule.GetComponent<DeNelle.Core.Combat.ActorAnimator>() == null)
                capsule.gameObject.AddComponent<DeNelle.Core.Combat.ActorAnimator>();

            // Capture the capsule "slot" (world bounds) + its renderers BEFORE adding
            // the model, so we can size/place the model into the exact slot and hide
            // the original pill.
            var capsuleRenderers = capsule.GetComponentsInChildren<Renderer>(true);
            Bounds slot = default; bool haveSlot = false;
            foreach (var r in capsuleRenderers)
            {
                if (r == null) continue;
                if (!haveSlot) { slot = r.bounds; haveSlot = true; } else slot.Encapsulate(r.bounds);
            }

            var model = UnityEngine.Object.Instantiate(prefab, capsule);
            model.name = "AtbHeroModel";
            model.transform.localPosition = Vector3.zero;
            // Hero stands on the LEFT facing the enemy on the RIGHT (+X). Owner-tuned: this
            // model's visual forward needed the opposite yaw, so 0° (not 180°) faces the foe.
            model.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
            StripCamerasAndColliders(model);

            // WO-381 #2 — COLOURING: bind the SAME per-class basecolor atlas the village
            // hero uses (HeroBodySwapper.ApplyExtractedTexture) so the ATB hero reads
            // textured (skin/clothing), not the blown-out WHITE of the raw FBX. The hero
            // FBX imports with an unbound/broken _MainTex, so the plain TripoMaterialFixer
            // rebuild rendered solid white. Setting the fallback texture makes the fixer
            // paint the real basecolor onto every slot (the village's go-live appearance).
            ApplyHeroTexturedMaterial(model, slug);

            // Ticket #7 (RCA 2026-06-21, data-proven asymmetry): bind the per-class hero animator
            // controller. The hero FBX imports Humanoid with a valid avatar but NO controller, so
            // without this the rig holds its bind/T-pose. The enemy path (ApplyEnemyAnimator) and the
            // village path (HeroBodySwapper) BOTH load a controller; the ATB hero path was the only one
            // that didn't — the missing assignment IS the T-pose. Controllers live in Resources/Heroes.
            ApplyHeroAnimator(model, slug);

            // Size to the slot, then RE-CENTER onto it. Tripo pivots are far off
            // centre, so scaling localScale flings the visible mesh away from the
            // capsule (the "hero in empty area" bug — same trap as the buildings).
            // Recentre by world bounds: bounds centre → slot centre (XZ), feet → slot base.
            if (haveSlot)
            {
                NormalizeHeight(model, Mathf.Max(0.5f, slot.size.y));
                Bounds mb = ModelBounds(model);
                Vector3 d = new Vector3(slot.center.x - mb.center.x,
                                        slot.min.y    - mb.min.y,
                                        slot.center.z - mb.center.z);
                model.transform.position += d;
            }
            else NormalizeHeight(model, 2f);

            // RENDER-VERIFY BEFORE HIDE (owner directive 2026-06-19, mirrors HeroArmorVisual):
            // the swapped class FBX is the LITERAL twin of the armor-swap bug — instantiating a
            // body and hiding the placeholder BEFORE proving the body renders leaves an invisible
            // / T-posed combatant in battle. PROVE the model renders (>=1 enabled renderer carrying
            // a mesh) BEFORE we hide the capsule. If it can't, ROLL BACK: drop the half-built model,
            // KEEP the capsule shown (never-invisible fallback). The failure self-reports (Fail).
            if (!VerifyModelRendersNow(model, "hero:" + slug))
            {
                RollbackToCapsule(model, capsuleRenderers, null,
                    $"hero model '{slug}' failed render-verify (no visible mesh)");
                return;
            }

            // Model proven renderable -> now safe to hide the original capsule pill.
            foreach (var r in capsuleRenderers) if (r != null) r.enabled = false;
            FlowTrace.Step("AtbSwap", $"SwapHero: class model '{slug}' shown, capsule pill hidden.");

            // DEFERRED pose-verify: the animator hasn't evaluated yet this frame. If nothing ever
            // drives the rig (no clip for this rig), the body freezes in bind/T-pose — re-show the
            // capsule rather than leave a frozen statue in the arena.
            AtbSwapPoseVerifier.Watch(capsule, model, capsuleRenderers, null, "hero:" + slug);
        }

        // RENDER-VERIFY (synchronous, no camera/scene dependency): the swapped model MUST carry
        // >=1 ENABLED Renderer with a non-null mesh, else the combatant reads invisible the moment
        // we hide the capsule. Traces exact counts so a capture splits "no visible mesh" vs hidden.
        // Returns false => caller rolls back to the capsule. Mirrors HeroArmorVisual.VerifyArmorRendersNow.
        private static bool VerifyModelRendersNow(GameObject model, string label)
        {
            if (model == null)
            {
                FlowTrace.Fail("AtbSwap", $"VerifyModelRenders: '{label}' model is null.");
                return false;
            }

            int total = 0, enabledR = 0, withMesh = 0;
            var rends = model.GetComponentsInChildren<Renderer>(true);
            foreach (var r in rends)
            {
                if (r == null) continue;
                total++;
                if (r.enabled) enabledR++;
                // SkinnedMeshRenderer carries sharedMesh; MeshRenderer's geometry is on a sibling
                // MeshFilter. Count either as "has mesh" so a static FBX isn't falsely rejected.
                bool hasMesh = false;
                if (r is SkinnedMeshRenderer smr) hasMesh = smr.sharedMesh != null;
                else { var mf = r.GetComponent<MeshFilter>(); hasMesh = mf != null && mf.sharedMesh != null; }
                if (hasMesh) withMesh++;
            }

            var anim = model.GetComponentInChildren<Animator>();
            string ctrl = (anim != null && anim.runtimeAnimatorController != null) ? anim.runtimeAnimatorController.name : "<null>";
            bool renders = enabledR > 0 && withMesh > 0;

            FlowTrace.Step("AtbSwap",
                $"VerifyModelRenders '{label}': renderers total={total} enabled={enabledR} withMesh={withMesh}; " +
                $"animator='{(anim == null ? "<none>" : anim.name)}' controller='{ctrl}' => renders={renders}");

            if (!renders)
            {
                FlowTrace.Fail("AtbSwap",
                    $"VerifyModelRenders FAILED '{label}': renders={renders} (enabled={enabledR}, withMesh={withMesh}).");
                return false;
            }
            return true;
        }

        // ROLL BACK a half-built swap: drop the model and RE-SHOW the placeholder capsule (never a
        // hidden capsule + broken model). For the enemy, an optional tint-fallback closure paints
        // the capsule violet instead. Control-flow safety — always runs (not behind the FlowTrace toggle).
        private static void RollbackToCapsule(GameObject model, Renderer[] capsuleRenderers, Action tintFallback, string reason)
        {
            FlowTrace.Fail("AtbSwap", $"RollbackToCapsule: {reason} — destroying model, re-showing capsule pill.");
            if (model != null) UnityEngine.Object.Destroy(model);
            if (tintFallback != null) { tintFallback(); }
            else if (capsuleRenderers != null)
                foreach (var r in capsuleRenderers) if (r != null) r.enabled = true;
        }

        private static Bounds ModelBounds(GameObject go)
        {
            var rends = go.GetComponentsInChildren<Renderer>(true);
            Bounds b = default; bool has = false;
            foreach (var r in rends)
            {
                if (r == null) continue;
                if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
            }
            return has ? b : new Bounds(go.transform.position, Vector3.one);
        }

        // ── Enemy: capsule pill → real enemy model (Resources/Enemies) ───────
        // Mirrors SwapHero. Resources/Enemies now ships runtime-loadable models
        // (Skeleton_*, Orc_*, Necromancer, Dragon — committed), so the "pill" enemy
        // becomes a real foe. Falls back to the violet tint if no model loads.
        private static void SwapEnemy(Transform capsule, Transform heroCapsule)
        {
            using var _ = FlowTrace.Enter("AtbSwap", "SwapEnemy");
            if (capsule.Find("AtbEnemyModel") != null) return;   // already swapped

            string enemySlug = ResolveEnemySlug();
            var prefab = Resources.Load<GameObject>("Enemies/" + enemySlug);
            if (prefab == null)
            {
                // No model in Resources -> keep the tinted pill (the documented fallback).
                FlowTrace.Step("AtbSwap",
                    $"SwapEnemy: Resources/Enemies/{enemySlug} not found — tinting the capsule pill (expected fallback).");
                TintEnemy(capsule);
                return;
            }

            // Ensure ActorAnimator driver for enemy hit reactions, attacks, death falls (matches hero and EnemyFactory).
            if (capsule.GetComponent<DeNelle.Core.Combat.ActorAnimator>() == null)
                capsule.gameObject.AddComponent<DeNelle.Core.Combat.ActorAnimator>();

            var capsuleRenderers = capsule.GetComponentsInChildren<Renderer>(true);
            Bounds slot = default; bool haveSlot = false;
            foreach (var r in capsuleRenderers)
            {
                if (r == null) continue;
                if (!haveSlot) { slot = r.bounds; haveSlot = true; } else slot.Encapsulate(r.bounds);
            }

            var model = UnityEngine.Object.Instantiate(prefab, capsule);
            model.name = "AtbEnemyModel";
            model.transform.localPosition = Vector3.zero;
            // WO-381 #3 — ENEMY FACING: turn the enemy to LOOK AT the hero instead of a
            // hardcoded 90° yaw (which only happened to read right at the fixed left/right
            // staging and broke if either combatant moved). KayKit enemies' visual forward
            // is local +Z, so we aim +Z at the hero: yaw the model so +Z points from the
            // enemy toward the hero capsule's world position (flattened to the ground plane).
            FaceEnemyTowardHero(model.transform, capsule, heroCapsule);
            StripCamerasAndColliders(model);

            // DEF-259 #2: the swapped Skeleton imported with no animator → T-pose. Stamp
            // the shared KayKit enemy controller (idle/attack/hit/death) so it idles + can
            // swing, mirroring EnemyAnimatorFactory (which lives in DeNelle.Village and we
            // cannot reference here). No-op-safe if the controller asset is absent.
            ApplyEnemyAnimator(model, enemySlug);

            // TripoMaterialFixer rebuilds URP materials. WO-481: the new Tripo orcs ship EXTERNAL
            // textures (unbound _MainTex), so set the basecolor FALLBACK (Resources/Enemies/OrcTex/*)
            // — same trick as the hero (ApplyHeroTexturedMaterial) — else they render white. Other
            // enemies have working textures so no fallback is set. Typed (BattleATB refs DeNelle.Core).
            try
            {
                var efixer = model.AddComponent<DeNelle.Core.TripoMaterialFixer>();
                string orcTex = EnemyBasecolorPath(enemySlug);
                if (!string.IsNullOrEmpty(orcTex)) efixer.SetFallbackTexture(orcTex);
            }
            catch (Exception ex)
            {
                FlowTrace.Warn("AtbSwap",
                    $"SwapEnemy: TripoMaterialFixer AddComponent threw {ex.GetType().Name}: {ex.Message} — " +
                    "enemy model kept (may render raw/untextured).");
            }

            if (haveSlot)
            {
                NormalizeHeight(model, Mathf.Max(0.5f, slot.size.y));
                Bounds mb = ModelBounds(model);
                Vector3 d = new Vector3(slot.center.x - mb.center.x,
                                        slot.min.y    - mb.min.y,
                                        slot.center.z - mb.center.z);
                model.transform.position += d;
            }
            else NormalizeHeight(model, 2f);

            // RENDER-VERIFY BEFORE HIDE (same twin-bug guard as SwapHero): prove the enemy model
            // renders BEFORE hiding the capsule. If it can't, ROLL BACK to the TINTED capsule — the
            // documented enemy fallback (violet pill) — never an invisible foe. The failure self-reports.
            if (!VerifyModelRendersNow(model, "enemy:" + enemySlug))
            {
                var cap = capsule;
                RollbackToCapsule(model, capsuleRenderers, () => TintEnemy(cap),
                    $"enemy model '{enemySlug}' failed render-verify (no visible mesh)");
                return;
            }

            // Model proven renderable -> now safe to hide the capsule pill.
            foreach (var r in capsuleRenderers) if (r != null) r.enabled = false;
            FlowTrace.Step("AtbSwap", $"SwapEnemy: enemy model '{enemySlug}' shown, capsule pill hidden.");

            // DEFERRED pose-verify: if the stamped controller drives no clip for this rig the enemy
            // freezes in T-pose — re-show (and tint) the capsule rather than leave a statue.
            var capForPose = capsule;
            AtbSwapPoseVerifier.Watch(capsule, model, capsuleRenderers, () => TintEnemy(capForPose), "enemy:" + enemySlug);

            // WO-481 — stage the REST of the family (followers) in formation behind/flanking the
            // leader, so the battle anchor reads as a led pack. Followers are formation visuals
            // (idle via OrcHumanoid); the leader at the capsule stays the BattleController-driven foe.
            StageEnemyFollowers(capsule, heroCapsule, haveSlot ? slot.size.y : 2f);
        }

        // WO-481 — stage the family's FOLLOWERS (roster[1..]) around the leader capsule. Each is a
        // real Resources/Enemies orc model (idle via OrcHumanoid + textured via the fixer fallback),
        // grounded + facing the hero, placed at a behind-and-flanking formation offset. Visual only
        // (the engine roster already carries the full family); never blocks the load.
        private static void StageEnemyFollowers(Transform leaderCapsule, Transform heroCapsule, float height)
        {
            if (leaderCapsule == null) return;
            if (GameObject.Find("AtbEnemyFollowers") != null) return;   // already staged

            var slugs = FollowerSlugs();
            if (slugs.Count == 0) return;

            var holder = new GameObject("AtbEnemyFollowers");
            holder.transform.position = leaderCapsule.position;

            // Behind (+X, away from the hero on -X) and flanking (±Z) the leader.
            Vector3[] offs =
            {
                new Vector3(0.9f, 0f,  1.2f),
                new Vector3(0.9f, 0f, -1.2f),
                new Vector3(1.9f, 0f,  0f),
            };

            for (int i = 0; i < slugs.Count && i < offs.Length; i++)
            {
                string slug = slugs[i];
                var prefab = Resources.Load<GameObject>("Enemies/" + slug);
                if (prefab == null)
                {
                    FlowTrace.Warn("AtbSwap", $"StageEnemyFollowers: Resources/Enemies/{slug} missing — skipped.");
                    continue;
                }

                var model = UnityEngine.Object.Instantiate(prefab, holder.transform);
                model.name = "AtbFollower_" + slug;
                StripCamerasAndColliders(model);
                ApplyEnemyAnimator(model, slug);
                try
                {
                    var fx = model.AddComponent<DeNelle.Core.TripoMaterialFixer>();
                    string t = EnemyBasecolorPath(slug);
                    if (!string.IsNullOrEmpty(t)) fx.SetFallbackTexture(t);
                }
                catch (Exception ex) { FlowTrace.Warn("AtbSwap", $"StageEnemyFollowers: fixer threw {ex.GetType().Name} — {slug} may render raw."); }

                NormalizeHeight(model, Mathf.Max(0.5f, height));

                // Recenter: bounds-centre XZ → the formation target; feet → ground (y=0).
                Vector3 target = leaderCapsule.position + offs[i];
                Bounds mb = ModelBounds(model);
                model.transform.position += new Vector3(target.x - mb.center.x, 0f - mb.min.y, target.z - mb.center.z);

                // Face the hero (same +Z aim as the leader's FaceEnemyTowardHero).
                if (heroCapsule != null)
                {
                    Vector3 dir = heroCapsule.position - model.transform.position; dir.y = 0f;
                    if (dir.sqrMagnitude > 0.0001f) model.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
                }
                FlowTrace.Step("AtbSwap", $"StageEnemyFollowers: '{slug}' staged at {target}.");
            }
        }

        // Follower model slugs = the roster AFTER the leader (index 0, already at the capsule).
        // Reads the same source as BattleController.BuildEnemyRoster: the breach handoff if present,
        // else the prototype Orc family — so engine roster and visuals stay in sync.
        private static System.Collections.Generic.List<string> FollowerSlugs()
        {
            var list = new System.Collections.Generic.List<string>();
            var handoff = DeNelle.Core.SceneRouter.PendingBattle;
            string[] ids = (handoff != null && handoff.BreachedIds != null && handoff.BreachedIds.Length > 0)
                ? handoff.BreachedIds
                : AtbPrototypeEncounter.OrcFamily;
            for (int i = 1; i < ids.Length; i++)
                list.Add(ModelSlugForEngineDef(EngineDefFor(ids[i])));
            return list;
        }

        // WO-381 #3 — orient the enemy model so its visual forward (+Z) points at the hero.
        // Uses world-space LookRotation (the model is a child of the capsule, which itself
        // carries a scene-builder yaw), flattened to the XZ plane so the enemy never tips.
        // Falls back to the prior fixed 90° yaw when the hero capsule is missing (dev path),
        // so the enemy still faces the standard left-staged hero.
        private static void FaceEnemyTowardHero(Transform model, Transform enemyCapsule, Transform heroCapsule)
        {
            if (model == null) return;
            if (heroCapsule == null)
            {
                model.localRotation = Quaternion.Euler(0f, 90f, 0f);
                return;
            }
            Vector3 dir = heroCapsule.position - enemyCapsule.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) { model.localRotation = Quaternion.Euler(0f, 90f, 0f); return; }
            model.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }

        // Which Resources/Enemies model to show for the enemy that triggered THIS battle.
        // The swapper is a scene-load hook with no BattleController reference, so we read the
        // same handoff BattleController builds its roster from: SceneRouter.PendingBattle.
        // BreachedIds carries the engine def ids (WaveManager passes Enemy.EngineDefId, see
        // BattleController.BuildEnemyRoster) — we take the FIRST breacher (the front-of-roster
        // foe the camera stages) and map its engine def id → a Resources/Enemies model slug.
        // If there is no handoff (dev / direct-play), we vary deterministically by wave so the
        // model is never a hard-coded constant. Mirrors the breach-id heuristics in
        // BattleController.MapToEngineDef (BattleATB cannot reference BattleController's private
        // method, so the small mapping is duplicated here and must be kept in sync).
        private static string ResolveEnemySlug()
        {
            var handoff = DeNelle.Core.SceneRouter.PendingBattle;

            // Prefer the actual breaching enemy that triggered the battle.
            if (handoff != null && handoff.BreachedIds != null && handoff.BreachedIds.Length > 0)
            {
                string first = handoff.BreachedIds[0];
                if (!string.IsNullOrEmpty(first))
                    return ModelSlugForEngineDef(EngineDefFor(first));
            }

            // No handoff → deterministic by wave (cycles the roster, never a constant).
            int wave = handoff != null ? handoff.Wave : 0;
            string[] cycle = { "skeleton", "goblin", "bruiser", "necromancer" };
            string defId = cycle[Mathf.Abs(wave) % cycle.Length];
            return ModelSlugForEngineDef(defId);
        }

        // Normalize a breach id (engine key OR village enemies.json id) to a valid ENEMY_DEFS
        // engine key. Mirror of BattleController.MapToEngineDef (kept deliberately small).
        private static string EngineDefFor(string id)
        {
            if (string.IsNullOrEmpty(id)) return "skeleton";
            if (Defs.ENEMY_DEFS.ContainsKey(id)) return id;     // already a valid engine key

            string lower = id.ToLowerInvariant();
            if (lower == "hollow-warrior") return "bruiser";
            if (lower == "hollow-walker")  return "skeleton";
            if (lower == "hollow-rogue")   return "skeleton";
            if (lower.Contains("necro"))   return "necromancer";
            if (lower.Contains("warrior") || lower.Contains("tank") || lower.Contains("bruiser")
                || lower.Contains("bulwark") || lower.Contains("boss")
                || lower.Contains("dragon")) return "bruiser";
            if (lower.Contains("goblin"))  return "goblin";
            return "skeleton";
        }

        // Map an ENEMY_DEFS engine key → a Resources/Enemies model slug (the prefab/FBX name).
        // The engine has 7 archetype defs; Resources ships a richer model set, so several defs
        // share a model. EnemyControllerFor() then stamps the matching animator by this slug.
        private static string ModelSlugForEngineDef(string engineDef)
        {
            switch (engineDef)
            {
                case "orc-warrior":       return "Orc_Warrior";       // WO-481 family LEADER
                case "orc-tank":          return "Orc_Tank";          // WO-481 follower
                case "orc-mage":          return "Orc_Mage";          // WO-481 follower
                case "goblin":            return "Skeleton_Minion";   // small grunt
                case "bruiser":           return "Skeleton_Golem";    // heavy tank → large rig
                case "necromancer":       return "Necromancer";
                case "hollow-captain":    return "Orc_Berserker";     // elite captain
                case "hollow-king":       return "Dragon";            // boss
                case "hollow-apprentice": return "Skeleton_Mage";     // caster minion
                case "skeleton":
                default:                  return "Skeleton_Warrior";  // standard grunt
            }
        }

        // ── Enemy animator (DEF-259 #2: no-T-pose) ───────────────────────────
        // Mirror of DeNelle.Village.EnemyAnimatorFactory's rig→controller map. We
        // duplicate the tiny mapping here because BattleATB does not (and must not)
        // reference DeNelle.Village. The shared controllers live in Resources/Enemies
        // (built by EnemyAnimatorSetup), so a Resources.Load reaches them at runtime.
        private static void ApplyEnemyAnimator(GameObject model, string modelName)
        {
            if (model == null) return;
            try
            {
                // NOTE: do NOT use ?? here — GetComponentInChildren returns Unity's "fake-null"
                // (a non-null managed ref wrapping a destroyed/absent native object), so ?? would
                // never fall through and we'd skip AddComponent. Unity's overloaded == handles it.
                var anim = model.GetComponentInChildren<Animator>();
                if (anim == null) anim = model.AddComponent<Animator>();
                anim.applyRootMotion = false; // turn-based stage: no locomotion drift
                var ctrl = Resources.Load<RuntimeAnimatorController>("Enemies/" + EnemyControllerFor(modelName));
                if (ctrl != null) anim.runtimeAnimatorController = ctrl;
                else FlowTrace.Warn("AtbSwap",
                    $"ApplyEnemyAnimator: no enemy controller for '{modelName}' — enemy will stay in T-pose. Run EnemyAnimatorSetup.");
            }
            catch (Exception ex)
            {
                // never block the swap — but SELF-REPORT (§12) so a failed animator bind is captured.
                FlowTrace.Warn("AtbSwap",
                    $"ApplyEnemyAnimator: '{modelName}' threw {ex.GetType().Name}: {ex.Message} — enemy may T-pose.");
            }
        }

        // ── Hero animator (ticket #7: no-T-pose) ─────────────────────────────
        // Mirror of ApplyEnemyAnimator for the swapped HERO. The per-class controllers live in
        // Resources/Heroes/{Ranger,Knight,Mage,Cleric}.controller (the same ones HeroBodySwapper
        // loads in the village). Without this the Humanoid hero rig stays in bind/T-pose. We do NOT
        // reference DeNelle.Village — Resources.Load reaches the shared assets at runtime.
        private static void ApplyHeroAnimator(GameObject model, string slug)
        {
            if (model == null) return;
            try
            {
                // GetComponentInChildren returns Unity "fake-null" for an absent native object, so
                // test with Unity's overloaded == (no ??) before AddComponent — same trap as enemies.
                var anim = model.GetComponentInChildren<Animator>();
                if (anim == null) anim = model.AddComponent<Animator>();
                anim.applyRootMotion = false; // turn-based stage: no locomotion drift
                // WO-545 Tier-1 seam: Addressables-first, Resources-fallback (V1-safe).
                var ctrl = DeNelle.Core.HeroAssetLoader.LoadHeroController(slug);
                if (ctrl != null)
                {
                    anim.runtimeAnimatorController = ctrl;
                    anim.Rebind();
                    FlowTrace.Step("AtbSwap", $"ApplyHeroAnimator: bound 'Heroes/{slug}' controller '{ctrl.name}'.");
                }
                else
                {
                    FlowTrace.Fail("AtbSwap",
                        $"ApplyHeroAnimator: no controller Heroes/{slug} — hero WILL T-pose (was the ticket #7 bug).");
                }
            }
            catch (Exception ex)
            {
                // never block the swap — but SELF-REPORT (§12) so a failed animator bind is captured.
                FlowTrace.Warn("AtbSwap",
                    $"ApplyHeroAnimator: '{slug}' threw {ex.GetType().Name}: {ex.Message} — hero may T-pose.");
            }
        }

        private static string EnemyControllerFor(string modelName)
        {
            switch (modelName)
            {
                case "Skeleton_Golem":  return "LargeEnemy";
                case "Necromancer":     return "Boss";
                case "Dragon":          return "Dragon";
                case "Orc_Warrior":     // WO-481 new Tripo orcs are HUMANOID → humanoid controller
                case "Orc_Tank":
                case "Orc_Mage":        return "OrcHumanoid";
                case "Orc_Berserker":
                case "Orc_Shaman":
                case "Orc_Necromancer": return "OrcWarband";
                default:                return "HumanoidEnemy"; // Warrior/Minion/Rogue/Mage
            }
        }

        // ── Enemy: tint the capsule (fallback when no model in Resources) ────
        private static void TintEnemy(Transform capsule)
        {
            var r = capsule.GetComponent<Renderer>(); if (r == null) r = capsule.GetComponentInChildren<Renderer>();
            if (r == null) return;
            if (r.sharedMaterial != null && r.sharedMaterial.name == "AtbEnemyTint") return;
            var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (sh == null) return;
            var m = new Material(sh) { name = "AtbEnemyTint" };
            var c = new Color(0.45f, 0.12f, 0.55f);   // Hollow-One violet
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_EmissionColor")) { m.SetColor("_EmissionColor", c * 0.5f); m.EnableKeyword("_EMISSION"); }
            r.sharedMaterial = m;
        }

        // ── helpers ──────────────────────────────────────────────────────────
        private static string ResolveHeroSlug()
        {
            // Direct read — BattleController (same assembly) reads HeroClass this way.
            // The OLD code used reflection GetProperty("HeroClass"), but HeroClass is a
            // FIELD, so GetProperty returned null and the ATB hero was ALWAYS Mage even
            // when Knight/Ranger was chosen (the village reads it directly and was fine).
            var svc = GameStateService.Instance;
            HeroClassOpt hc = (svc != null && svc.State != null) ? svc.State.HeroClass : HeroClassOpt.None;
            switch (hc)
            {
                case HeroClassOpt.Knight: return "Knight";
                case HeroClassOpt.Ranger: return "Ranger";
                default:                  return "Mage";   // Mage, Cleric (reuses Mage for now), None
            }
        }

        // WO-381 #2 — bind the hero's class basecolor atlas via TripoMaterialFixer so the
        // ATB hero matches the village appearance (textured, not white). The basecolor
        // paths are the SAME ones HeroBodySwapper.ApplyExtractedTexture uses (the reliably
        // Resources.Load-able Heroes/Textures/* plain folder). The fixer runs in its own
        // Start() (deferred a frame), so setting the fallback texture right after AddComponent
        // lands in time. No fallback tint is set: the fixer keeps _BaseColor white so the
        // bound atlas controls the look (matching the village). Typed AddComponent
        // (BattleATB references DeNelle.Core).
        private static void ApplyHeroTexturedMaterial(GameObject model, string slug)
        {
            if (model == null) return;
            string basecolor = HeroBasecolorPath(slug);
            var fixer = model.AddComponent<DeNelle.Core.TripoMaterialFixer>();
            if (!string.IsNullOrEmpty(basecolor))
                fixer.SetFallbackTexture(basecolor);
        }

        // The per-class basecolor texture (Resources path, no extension) — mirrors the
        // canonical map in HeroBodySwapper.ApplyExtractedTexture so the ATB hero shares the
        // village's exact atlas. Keep in sync if the village repoints a class.
        private static string HeroBasecolorPath(string slug)
        {
            switch (slug)
            {
                case "Knight": return "Heroes/Textures/remesh_12_combined_Bake_Diffuse";
                case "Ranger": return "Heroes/Textures/ranger_basecolor";
                case "Cleric": return "Heroes/Textures/Cleric_basecolor";
                default:       return "Heroes/Textures/tripo_mat_9b343081_Pbr_Diffuse"; // Mage (+ None)
            }
        }

        // WO-481 — per-orc basecolor (Resources path, no extension) for the TripoMaterialFixer
        // fallback. The new Tripo orcs ship external textures (copied to Resources/Enemies/OrcTex
        // by PromoteOrcsToResources); other enemies have working/embedded textures → null (no fallback).
        private static string EnemyBasecolorPath(string slug)
        {
            switch (slug)
            {
                case "Orc_Warrior": return "Enemies/OrcTex/Orc_Warrior_basecolor";
                case "Orc_Tank":    return "Enemies/OrcTex/Orc_Tank_basecolor";
                case "Orc_Mage":    return "Enemies/OrcTex/Orc_Mage_basecolor";
                default:            return null;
            }
        }

        private static void NormalizeHeight(GameObject go, float target)
        {
            var rends = go.GetComponentsInChildren<Renderer>(true);
            if (rends.Length == 0) return;
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            float h = b.size.y;
            if (h > 0.001f) go.transform.localScale *= (target / h);
        }

        private static void StripCamerasAndColliders(GameObject go)
        {
            foreach (var cam in go.GetComponentsInChildren<Camera>(true)) if (cam != null) UnityEngine.Object.Destroy(cam);
            foreach (var al in go.GetComponentsInChildren<AudioListener>(true)) if (al != null) UnityEngine.Object.Destroy(al);
            foreach (var col in go.GetComponentsInChildren<Collider>(true)) if (col != null) UnityEngine.Object.Destroy(col);
        }

        private static void HideOwnRenderer(Transform capsule)
        {
            var r = capsule.GetComponent<Renderer>();
            if (r != null) r.enabled = false;
        }

        private static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName, false);
                if (t != null) return t;
            }
            return null;
        }
    }

    // DEFERRED pose-verify host. AtbCombatantSwapper is a static class with no MonoBehaviour to
    // run a coroutine on, so we attach this tiny watcher to the combatant capsule. Over the next
    // few frames it confirms the swapped model is actually PLAYING a clip (layer-0 clipCount > 0),
    // not frozen in the bind/T-pose. If NOTHING ever drives the rig within the window it ROLLS BACK:
    // destroys the model and re-shows (and optionally tints) the placeholder capsule — never a frozen
    // statue, never an invisible combatant. Mirrors HeroArmorVisual.VerifyPoseThenMaybeRollback.
    internal sealed class AtbSwapPoseVerifier : MonoBehaviour
    {
        private GameObject _model;
        private Renderer[] _capsuleRenderers;
        private Action _tintFallback;
        private string _label;

        public static void Watch(Transform capsule, GameObject model, Renderer[] capsuleRenderers,
                                 Action tintFallback, string label)
        {
            if (capsule == null || model == null) return;
            var w = capsule.gameObject.AddComponent<AtbSwapPoseVerifier>();
            w._model = model;
            w._capsuleRenderers = capsuleRenderers;
            w._tintFallback = tintFallback;
            w._label = label;
        }

        private System.Collections.IEnumerator Start()
        {
            const int MaxPoseFrames = 6;
            bool everPlayed = false;
            int lastCount = -1;
            for (int i = 0; i < MaxPoseFrames; i++)
            {
                yield return null;
                // The model was already torn down by a newer swap / death — nothing to verify.
                if (_model == null) { Cleanup(); yield break; }

                var anim = _model.GetComponentInChildren<Animator>();
                if (anim != null && anim.isActiveAndEnabled && anim.runtimeAnimatorController != null)
                {
                    lastCount = anim.GetCurrentAnimatorClipInfoCount(0);
                    if (lastCount > 0) { everPlayed = true; break; }
                }
            }

            FlowTrace.Step("AtbSwap",
                $"VerifyPose '{_label}': everPlayed={everPlayed} lastClipCount={lastCount} " +
                "(>=1 means an animation drives the rig; otherwise frozen T-pose).");

            // A static FBX with no Animator (or a deliberately static prop) legitimately never plays —
            // only roll back when an Animator+controller is present but never drives a clip.
            bool hasAnimator = _model != null && _model.GetComponentInChildren<Animator>() != null;
            if (!everPlayed && hasAnimator)
            {
                FlowTrace.Fail("AtbSwap",
                    $"VerifyPose '{_label}': animated model never posed (T-pose, lastClipCount={lastCount}) — " +
                    "rolling back to the placeholder capsule.");
                if (_model != null) Destroy(_model);
                if (_tintFallback != null) _tintFallback();
                else if (_capsuleRenderers != null)
                    foreach (var r in _capsuleRenderers) if (r != null) r.enabled = true;
            }

            Cleanup();
        }

        private void Cleanup() => Destroy(this);
    }
}
