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

namespace DeNelle.BattleATB
{
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
                catch { /* best-effort — never block the ATB load */ }
            }
        }

        // ── Hero: capsule pill → real class model ────────────────────────────
        private static void SwapHero(Transform capsule)
        {
            if (capsule.Find("AtbHeroModel") != null) return;   // already swapped

            string slug = ResolveHeroSlug();
            var prefab = Resources.Load<GameObject>("Heroes/" + slug);
            if (prefab == null) return;                          // keep the capsule

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

            // Hide the original capsule pill — the model replaces it.
            foreach (var r in capsuleRenderers) if (r != null) r.enabled = false;
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
            if (capsule.Find("AtbEnemyModel") != null) return;   // already swapped

            var prefab = Resources.Load<GameObject>("Enemies/" + ResolveEnemySlug());
            if (prefab == null) { TintEnemy(capsule); return; }  // no model -> keep the tinted pill

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
            ApplyEnemyAnimator(model, ResolveEnemySlug());

            var fixer = FindType("DeNelle.Core.TripoMaterialFixer");
            if (fixer != null) { try { model.AddComponent(fixer); } catch { } }

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

            foreach (var r in capsuleRenderers) if (r != null) r.enabled = false;
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

        // Which Resources/Enemies model to show. Default to the standard skeleton grunt
        // (matches BattleController's "skeleton" fallback def). TODO: read the live encounter
        // def to vary the model (necromancer/orc/dragon) per battle.
        private static string ResolveEnemySlug() => "Skeleton_Warrior";

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
                var anim = model.GetComponentInChildren<Animator>() ?? model.AddComponent<Animator>();
                anim.applyRootMotion = false; // turn-based stage: no locomotion drift
                var ctrl = Resources.Load<RuntimeAnimatorController>("Enemies/" + EnemyControllerFor(modelName));
                if (ctrl != null) anim.runtimeAnimatorController = ctrl;
                else Debug.LogWarning("[AtbCombatantSwapper] No enemy controller for '" + modelName +
                                      "' — enemy will stay in T-pose. Run EnemyAnimatorSetup.");
            }
            catch { /* never block the swap */ }
        }

        private static string EnemyControllerFor(string modelName)
        {
            switch (modelName)
            {
                case "Skeleton_Golem":  return "LargeEnemy";
                case "Necromancer":     return "Boss";
                case "Dragon":          return "Dragon";
                case "Orc_Berserker":
                case "Orc_Shaman":
                case "Orc_Necromancer": return "OrcWarband";
                default:                return "HumanoidEnemy"; // Warrior/Minion/Rogue/Mage
            }
        }

        // ── Enemy: tint the capsule (fallback when no model in Resources) ────
        private static void TintEnemy(Transform capsule)
        {
            var r = capsule.GetComponent<Renderer>() ?? capsule.GetComponentInChildren<Renderer>();
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
}
