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

            var hero = GameObject.Find("HeroCapsule");
            var enemy = GameObject.Find("EnemyCapsule");
            if (hero != null) SwapHero(hero.transform);
            if (enemy != null) TintEnemy(enemy.transform);
        }

        // ── Hero: capsule pill → real class model ────────────────────────────
        private static void SwapHero(Transform capsule)
        {
            if (capsule.Find("AtbHeroModel") != null) return;   // already swapped

            string slug = ResolveHeroSlug();
            var prefab = Resources.Load<GameObject>("Heroes/" + slug);
            if (prefab == null) return;                          // keep the capsule

            var model = UnityEngine.Object.Instantiate(prefab, capsule);
            model.name = "AtbHeroModel";
            model.transform.localPosition = Vector3.zero;
            // Hero stands on the LEFT facing the enemy on the RIGHT (+X). The Tripo
            // heroes' visual forward is local -X (see HeroLocomotion WO-32 note), so
            // 180° yaw turns that -X to point +X toward the foe. Tunable if it reads wrong.
            model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            NormalizeHeight(model, 2.0f);
            StripCamerasAndColliders(model);

            // URP material fix (heroes import with Tripo Phong materials).
            var fixer = FindType("DeNelle.Core.TripoMaterialFixer");
            if (fixer != null) { try { model.AddComponent(fixer); } catch { } }

            HideOwnRenderer(capsule);   // hide the pill; the model shows in its place
        }

        // ── Enemy: tint the capsule (no runtime enemy model in Resources) ────
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
            try
            {
                var t = FindType("DeNelle.Core.State.GameStateService");
                var inst = t?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                var state = inst?.GetType().GetProperty("State")?.GetValue(inst);
                var hc = state?.GetType().GetProperty("HeroClass")?.GetValue(state);
                string s = hc?.ToString();
                if (s != null)
                {
                    if (s.IndexOf("Knight", StringComparison.OrdinalIgnoreCase) >= 0) return "Knight";
                    if (s.IndexOf("Ranger", StringComparison.OrdinalIgnoreCase) >= 0) return "Ranger";
                }
            }
            catch { }
            return "Mage";
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
