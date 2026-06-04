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

            DisableStrayVillageHero();

            var hero = GameObject.Find("HeroCapsule");
            var enemy = GameObject.Find("EnemyCapsule");
            if (hero != null) SwapHero(hero.transform);
            if (enemy != null) SwapEnemy(enemy.transform);
        }

        /// <summary>
        /// The village hero (DontDestroyOnLoad, HeroLocomotion) can ride into the
        /// ATB scene and stay player-controllable — a stray "navigable pill" next to
        /// the turn-based combatant. Deactivate it here so only the ATB combatant
        /// remains; HeroControlEnsurer re-activates it when the Village scene reloads.
        /// Reflection: BattleATB does not reference DeNelle.Village.
        /// </summary>
        private static void DisableStrayVillageHero()
        {
            try
            {
                var locoType = FindType("DeNelle.Village.HeroLocomotion");
                if (locoType == null) return;
                var found = UnityEngine.Object.FindObjectsByType(locoType, FindObjectsSortMode.None);
                if (found == null) return;
                foreach (var obj in found)
                {
                    if (obj is Component c && c != null)
                    {
                        c.gameObject.SetActive(false);
                        Debug.Log("[AtbCombatantSwapper] Disabled stray village hero in ATB: " + c.gameObject.name);
                    }
                }
            }
            catch { /* best-effort — never block the ATB load */ }
        }

        // ── Hero: capsule pill → real class model ────────────────────────────
        private static void SwapHero(Transform capsule)
        {
            if (capsule.Find("AtbHeroModel") != null) return;   // already swapped

            string slug = ResolveHeroSlug();
            var prefab = Resources.Load<GameObject>("Heroes/" + slug);
            if (prefab == null) return;                          // keep the capsule

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
            // Hero stands on the LEFT facing the enemy on the RIGHT (+X). The Tripo
            // heroes' visual forward is local -X (HeroLocomotion WO-32 note), so 180°
            // yaw turns -X to point +X toward the foe. Tunable if it reads wrong.
            model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            StripCamerasAndColliders(model);

            // URP material fix (heroes import with Tripo Phong materials).
            var fixer = FindType("DeNelle.Core.TripoMaterialFixer");
            if (fixer != null) { try { model.AddComponent(fixer); } catch { } }

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
        private static void SwapEnemy(Transform capsule)
        {
            if (capsule.Find("AtbEnemyModel") != null) return;   // already swapped

            var prefab = Resources.Load<GameObject>("Enemies/" + ResolveEnemySlug());
            if (prefab == null) { TintEnemy(capsule); return; }  // no model -> keep the tinted pill

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
            // DEF-259: Enemy stands on the RIGHT, facing the hero on the LEFT (-X). KayKit
            // enemies' visual forward is +Z, so +90° yaw turns +Z to face -X toward the
            // hero (mirror of the hero on the left). Tunable.
            model.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
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
