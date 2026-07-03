// =============================================================================
// HudPosture — the owner's posture taxonomy (A4.3/A4.4) as a first-class type.
// (HUD_OBSIDIAN_ARCHITECTURE_2026-07-03 amendments A4-A4.7 — P23 HUDKIT.)
// -----------------------------------------------------------------------------
// Assembly: DeNelle.HUD   Namespace: DeNelle.HUD.Kit
//
// The posture arc is the MASTER STATE (A4.7): calm(town) [+build variant] ->
// calm(explore) -> hostile(prebattle) -> hostile(activebattle) ->
// hostile(postbattle), with modal as the mute-everything overlay. Every
// presentation submodel (HUD occupancy, animation, audio) consumes it; the HUD
// submodel's occupancy rows live in hud-areas.json keyed by PostureKey().
// =============================================================================

namespace DeNelle.HUD.Kit
{
    /// <summary>The six HUD postures (A4.3/A4.4). Order is calm -> hostile -> modal.</summary>
    public enum HudPosture
    {
        /// <summary>Home ground — the domestic HUD (wave block between waves, build, resources, heart).</summary>
        CalmTown,
        /// <summary>Friendly posture in the open — travel affordances, resources hidden-till-tapped, NO wave chrome.</summary>
        CalmExplore,
        /// <summary>calm(town) variant: a Build Mode edit session — near-empty HUD + settings.</summary>
        Build,
        /// <summary>The engagement window (A4.5): about to engage / being pursued — loadout + telemetrics only.</summary>
        HostilePrebattle,
        /// <summary>The fight — hostile tree crowned, friendly tree at combat essentials (A4.6: the arena is pure battle).</summary>
        HostileActiveBattle,
        /// <summary>The decision node — the EndState template owns the screen; the kit stands down.</summary>
        HostilePostbattle,
        /// <summary>A modal overlay is open — both trees muted.</summary>
        Modal,
    }

    /// <summary>Key helpers between the enum and the hud-areas.json row keys.</summary>
    public static class HudPostureKeys
    {
        /// <summary>The canonical JSON row key for a posture (owner's spelling, A4.4).</summary>
        public static string Key(HudPosture p)
        {
            switch (p)
            {
                case HudPosture.CalmTown:            return "calm(town)";
                case HudPosture.CalmExplore:         return "calm(explore)";
                case HudPosture.Build:               return "build";
                case HudPosture.HostilePrebattle:    return "hostile(prebattle)";
                case HudPosture.HostileActiveBattle: return "hostile(activebattle)";
                case HudPosture.HostilePostbattle:   return "hostile(postbattle)";
                default:                             return "modal";
            }
        }
    }
}
