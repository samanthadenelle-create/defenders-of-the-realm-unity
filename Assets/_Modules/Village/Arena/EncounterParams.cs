// =============================================================================
// EncounterParams — the hand-off payload for a PvE overworld ENCOUNTER battle.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Arena
//
// WO-482. Built by the overworld "rep" mob when the hero engages it, and read by
// the generic BattleArena controller to build the isolated open-kite arena and
// spawn the enemy family. Deliberately PRESENTATION-FREE (logic only): it carries
// WHICH enemies + how tough + WHERE to return — not models/VFX/HUD (the separate
// presentation layer loads those from the ids). Mirrors the role ArenaOpponentDef
// plays for the PvP path, but for an open-arena PvE family fight (NO fort).
// =============================================================================

using UnityEngine;

namespace DeNelle.Village.Arena
{
    /// <summary>
    /// One PvE encounter's data: the enemy family to stage, its threat scaling, the
    /// backdrop theme (derived from the source scene so the arena "matches where you
    /// were"), and the return pose (so victory lands the hero exactly where the fight
    /// was triggered). Logic-only — the BattleArena reads ids and the presentation
    /// layer (EnemyFactory / HeroAbilities VFX / HUD) renders them.
    /// </summary>
    public sealed class EncounterParams
    {
        /// <summary>The enemy family to spawn — engine/enemy ids (e.g. orc-warrior, orc-tank, orc-mage).
        /// Index 0 is the leader. Length 1..6 (the doc's 1vN range).</summary>
        public string[] EnemyIds = System.Array.Empty<string>();

        /// <summary>Threat level → enemy stat scaling + (later) loot rarity. Mirrors EnemyOutpost threat.</summary>
        public int Threat = 1;

        /// <summary>Backdrop theme key derived from the source scene ("outerworld" / "castle" / "cavern").
        /// The arena copies/selects skybox + ambient + ground so it reads like the engagement location.</summary>
        public string BackdropContext = "outerworld";

        /// <summary>The scene to return to when the battle resolves (the source/engagement scene).</summary>
        public string ReturnScene;

        /// <summary>Hero world position at engage time — victory warps the hero back here.</summary>
        public Vector3 ReturnPosition;

        /// <summary>Hero heading (Y euler) at engage time, for restoring facing on return.</summary>
        public float ReturnYaw;

        /// <summary>Optional id of the overworld rep mob that triggered this — consumed (despawned) on victory.</summary>
        public string RepId;
    }
}
