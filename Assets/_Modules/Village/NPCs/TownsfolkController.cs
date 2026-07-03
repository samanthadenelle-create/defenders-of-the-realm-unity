// =============================================================================
// TownsfolkController — the ambient-townsfolk registry for Elarion (D).
// -----------------------------------------------------------------------------
// A tiny scene-level coordinator for the village's ambient NPCs. It is NOT
// required for an AmbientNPC to function — each NPC resolves its own Keeper
// transform as a fallback — but it gives the scene one clean place to:
//
//   • hand every villager the Keeper transform once it is known (so they don't
//     each have to scene-search), and
//   • broadcast a reduced-motion preference to the whole crowd at once.
//
// VillageSceneBuilder adds this to the "Townsfolk" sub-root by reflection and
// calls SetHero(...) with the Hero rig it built. The controller then pushes
// that transform onto every AmbientNPC under it on Start (and again whenever
// SetHero is called later).
//
// DeNelle.Village module only — no cross-module dependency.
// =============================================================================

using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// Scene coordinator for the ambient townsfolk: distributes the Keeper
    /// transform and the reduced-motion preference to every <see cref="AmbientNPC"/>
    /// in its hierarchy. Built into the Village scene by <c>VillageSceneBuilder</c>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TownsfolkController : MonoBehaviour
    {
        [Tooltip("The Keeper / hero transform the townsfolk watch. Assigned by " +
                 "the scene builder; falls back to a 'Hero'-named object on Start.")]
        [SerializeField] private Transform _hero;

        [Tooltip("When true every townsperson freezes its idle sway (reduced motion).")]
        [SerializeField] private bool _reducedMotion;

        private AmbientNPC[] _npcs;

        private void Start()
        {
            _npcs = GetComponentsInChildren<AmbientNPC>(includeInactive: true);

            // Fallback hero lookup — name-based, not tag-based: the project
            // defines no "Player" tag and FindGameObjectWithTag throws on an
            // undefined tag. The village builder names the rig "Hero (Blaise)".
            if (_hero == null)
            {
                foreach (var t in
                         FindObjectsByType<Transform>())
                {
                    if (t != null && t.name.StartsWith("Hero")) { _hero = t; break; }
                }
            }

            ApplyToAll();
        }

        /// <summary>
        /// Sets the Keeper transform and pushes it onto every registered NPC.
        /// Safe to call before or after <see cref="Start"/>.
        /// </summary>
        public void SetHero(Transform hero)
        {
            _hero = hero;
            ApplyToAll();
        }

        /// <summary>Sets the reduced-motion preference and broadcasts it to every NPC.</summary>
        public void SetReducedMotion(bool reduced)
        {
            _reducedMotion = reduced;
            ApplyToAll();
        }

        /// <summary>The number of ambient townsfolk this controller coordinates.</summary>
        public int TownsfolkCount => _npcs != null ? _npcs.Length : 0;

        private void ApplyToAll()
        {
            if (_npcs == null)
                _npcs = GetComponentsInChildren<AmbientNPC>(includeInactive: true);

            foreach (var npc in _npcs)
            {
                if (npc == null) continue;
                if (_hero != null) npc.SetHero(_hero);
                npc.SetReducedMotion(_reducedMotion);
            }
        }
    }
}
