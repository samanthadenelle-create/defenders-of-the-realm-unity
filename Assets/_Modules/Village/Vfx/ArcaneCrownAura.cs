// =============================================================================
// ArcaneCrownAura - the MAX-LEVEL prestige aura for the arcane tower family.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// OWNER VFX PICK (2026-08-16, verbatim - picks are canon, never substitute):
//   "Assets\Lana Studio\Casual RPG VFX\Prefabs\Top_down_attack\top_down_bomb_rainbow.prefab"
//     -> "used as aura on max level Arcane Towers!"
//
// The mapping: a MAX-LEVEL (L3) arcane-family tower carries the rainbow effect as a
// PERSISTENT looping aura seated at its CROWN - a prestige tell. Only at max level,
// only the arcane family. Below max (or on a downgrade/reskin that breaks the
// condition) the aura is released.
//
// WHO DRIVES IT: ArcaneAura.ApplyLevel - the ONE level seam every arcane-family
// tower's level changes flow through (StructureFactory.ReskinForLevel on placement,
// upgrade-complete via BuildModeController, AND load-from-save via BaseLayoutLoader's
// ReskinForLevel replay for ps.level >= 2 - so an already-max tower loaded from save
// gets the crown too). ArcaneAura only ever lives on the arcane family (its Ensure
// call sites + the StructureFactory towerLike gate: arcane/wizard/spire/mage), so
// riding its level seam IS the family gate - no id matching needed here.
//
// PREFAB RESOLUTION: the same idiom the other 2026-08-16 Lana owner picks use
// (AtbStatusVfx - Resources.Load at VFX/<area>/<verbatim-prefab-name>). The Lana
// pack is NOT under a Resources folder, so the committer places a Resources-reachable
// copy at Assets/Resources/VFX/Aura/top_down_bomb_rainbow.prefab (the existing
// Assets/Resources/VFX/Aura/* pattern). Until then every acquire degrades
// gracefully: FlowTrace.Warn ONCE, no aura, never an error (CLAUDE.md section 4).
//
// WHY DIRECT Instantiate AND NOT THE VFXManager POOL: this follows the Lana
// owner-pick idiom (AtbStatusVfx), and the instance is PARENTED to the tower root -
// so tower destruction destroys the aura with it and no pool slot can ever leak.
// The pooled-loop budget (VFXManager 20-loop cap) is untouched. A broken-to-shell
// tower (root stays active, no lifecycle event) is covered by the ArcaneAura
// StopAndDisable hook releasing the crown explicitly.
//
// COLORBLIND-SAFE (owner is red/green colourblind): the prestige read is carried by
// the effect's MOTION + LUMINANCE at the tower crown, not by any particular hue -
// the rainbow palette is the owner's pick, not a meaning channel.
// =============================================================================

using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>
    /// Holds the owner-picked rainbow prestige aura at the crown of a MAX-LEVEL
    /// arcane-family tower. Driven by <see cref="Sync"/> from ArcaneAura.ApplyLevel;
    /// acquires at level == MaxLevel, releases below it and on every teardown path.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ArcaneCrownAura : MonoBehaviour
    {
        /// <summary>Resources path for the owner-picked prefab (basename VERBATIM =
        /// top_down_bomb_rainbow, Lana Top_down_attack).</summary>
        public const string CrownPrefabPath = "VFX/Aura/top_down_bomb_rainbow";

        /// <summary>The arcane family's level ceiling. 3 across the family, verified at
        /// source 2026-08-16: towers.json levels[] tops at 3 (Warded Spire);
        /// structures-catalog tower_arcane_spire carries 2 upgradeVisualPath rows (1+2=3);
        /// building-tiers arcane-tower (Cathedral of Magic) has 3 tiers; ArcaneAura /
        /// StructureTierVisual both clamp 1..3.</summary>
        public const int MaxLevel = 3;

        // Footprint-relative sizing: uniform scale 1 at a ReferenceFootprint-metre body,
        // clamped so a wall-tower crown never vanishes and the Cathedral never drowns.
        private const float ReferenceFootprintMetres = 4f;
        private const float MinScale = 0.6f;
        private const float MaxScale = 2.5f;

        // The reskin path swaps the visual AFTER ApplyLevel runs (StructureFactory
        // .ReskinForLevel calls EscalateTo before the mesh swap), so the crown measured at
        // acquire time can be the OLD tier's body. A slow re-seat keeps the aura on the
        // real crown once the new mesh lands. Cheap (one renderer walk), max towers only.
        private const float ReseatSeconds = 1.0f;

        // Warn-once latch for the missing-prefab fallback (per domain reload, not per tower -
        // one line names the gap, a hundred towers must not each repeat it).
        private static bool s_missingWarned;
        private static GameObject s_prefab;
        private static bool s_prefabLooked;

        private GameObject _instance;
        private int _level = 1;
        private float _nextReseat;

        /// <summary>True while the prestige aura is held. For diagnostics.</summary>
        public bool IsHeld => _instance != null;

        /// <summary>
        /// The one driver seam: attach-once + set the tower's current level (idempotent).
        /// Called by ArcaneAura.ApplyLevel on every level change (placement, upgrade,
        /// load-from-save replay). Acquires at MaxLevel, releases below it.
        /// </summary>
        public static void Sync(GameObject root, int level)
        {
            if (root == null) return;
            var crown = root.GetComponent<ArcaneCrownAura>();
            if (crown == null)
            {
                if (level < MaxLevel) return;   // below max and none attached - nothing to do
                crown = root.AddComponent<ArcaneCrownAura>();
            }
            crown.SetLevel(level);
        }

        /// <summary>Set the level this component believes and re-resolve the aura.</summary>
        public void SetLevel(int level)
        {
            _level = level;
            Resolve();
        }

        /// <summary>Release the held aura now (broken-to-shell teardown seam - called by
        /// ArcaneAura.StopAndDisable, whose reason for existing is exactly that a broken
        /// tower fires no lifecycle event). Idempotent.</summary>
        public void Release(string reason)
        {
            if (_instance == null) return;
            FlowTrace.Step("TowerVfx",
                "'" + name + "' max-level crown aura RELEASED (reason=" + reason +
                ", level=" + _level + ").");
            Destroy(_instance);
            _instance = null;
        }

        // ── lifecycle: every exit path releases ─────────────────────────────────
        private void OnEnable()  => Resolve();
        private void OnDisable() => Release("OnDisable");
        private void OnDestroy() => Release("OnDestroy");

        private void Update()
        {
            // Held-only slow re-seat: the tier reskin swaps the body mesh after the level
            // seam fired, so the measured crown can go stale. Change-only work.
            if (_instance == null || Time.time < _nextReseat) return;
            _nextReseat = Time.time + ReseatSeconds;
            Seat(_instance.transform);
        }

        // ── resolution ──────────────────────────────────────────────────────────

        private void Resolve()
        {
            bool want = _level >= MaxLevel && isActiveAndEnabled;
            if (want == (_instance != null)) return;   // already correct - change-only path

            if (!want) { Release("level below max (level=" + _level + ")"); return; }

            GameObject prefab = LoadPrefab();
            if (prefab == null) return;   // warned once - graceful, no aura, never an error

            _instance = Instantiate(prefab, transform);
            _instance.name = "ArcaneCrownAura_top_down_bomb_rainbow";
            ForceLooping(_instance);
            Seat(_instance.transform);
            _nextReseat = Time.time + ReseatSeconds;

            FlowTrace.Step("TowerVfx",
                "'" + name + "' max-level crown aura ACQUIRED (owner pick top_down_bomb_rainbow, " +
                "level=" + _level + "/" + MaxLevel + ", scale=" +
                _instance.transform.localScale.x.ToString("0.00") + ").");
        }

        /// <summary>Cached Resources load; a miss warns ONCE (graceful fallback - the
        /// tower simply shows no crown until the committer lands the Resources mirror).</summary>
        private static GameObject LoadPrefab()
        {
            if (s_prefabLooked) return s_prefab;
            s_prefabLooked = true;
            s_prefab = Resources.Load<GameObject>(CrownPrefabPath);
            if (s_prefab == null && !s_missingWarned)
            {
                s_missingWarned = true;
                FlowTrace.Warn("TowerVfx",
                    "max-level crown aura prefab MISSING at Resources/" + CrownPrefabPath +
                    " (owner pick top_down_bomb_rainbow) - towers show no crown aura. Committer: " +
                    "place a copy of 'Assets/Lana Studio/Casual RPG VFX/Prefabs/Top_down_attack/" +
                    "top_down_bomb_rainbow.prefab' at Assets/Resources/VFX/Aura/ (the AtbStatusVfx " +
                    "Lana owner-pick idiom).");
            }
            return s_prefab;
        }

        /// <summary>
        /// The Top_down_attack recipe is authored as a BURST; as a prestige AURA it must
        /// idle forever, so every particle layer is forced to loop. Applied to OUR OWN
        /// non-pooled instance only - the shared prefab asset is never mutated.
        /// </summary>
        private static void ForceLooping(GameObject instance)
        {
            var systems = instance.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < systems.Length; i++)
            {
                var ps = systems[i];
                if (ps == null) continue;
                var main = ps.main;
                main.loop = true;
                main.stopAction = ParticleSystemStopAction.None;   // never self-destroys the loop
            }
        }

        /// <summary>
        /// Seat at the tower's CROWN: XZ = the non-particle body-bounds centre, Y = the
        /// body top (the same body-derived anchoring ArcaneAura proved for off-pivot art -
        /// its Cathedral measured 4.61m root-to-body drift, so root anchoring is a lie).
        /// Uniform scale rides the measured footprint. A body-less host (art not loaded
        /// yet) sits at the root + a nominal height until the re-seat finds the mesh.
        /// </summary>
        private void Seat(Transform aura)
        {
            Bounds body = default; bool hasBody = false;
            var rends = GetComponentsInChildren<Renderer>(false);
            for (int i = 0; i < rends.Length; i++)
            {
                var r = rends[i];
                if (r == null || r is ParticleSystemRenderer) continue;
                if (!hasBody) { body = r.bounds; hasBody = true; } else body.Encapsulate(r.bounds);
            }

            if (hasBody)
            {
                aura.position = new Vector3(body.center.x, body.max.y, body.center.z);
                float footprint = Mathf.Max(body.size.x, body.size.z);
                float scale = Mathf.Clamp(footprint / ReferenceFootprintMetres, MinScale, MaxScale);
                aura.localScale = Vector3.one * scale;
            }
            else
            {
                aura.position = transform.position + Vector3.up * 3f;
                aura.localScale = Vector3.one;
            }
            aura.rotation = Quaternion.identity;
        }
    }
}
