// =============================================================================
// PursuitBattleProbe — "actively pursued = in battle" battle-lock source
// (ticket F8-46, owner ruling OPTION A, 2026-07-11).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.Combat
//
// THE GAP THIS CLOSES: every hero OUTGOING combat input is gated on
// BattleLock.IsInBattle() (HeroAbilityInput.Update, PlayerAttackController) —
// but BattleLock is only raised by the STAGED battle owners (ATBCombatManager,
// ArenaMode, BattleArena) plus HeroCombatEngagement's in-scene duelists. A hero
// being CHASED in the overworld (wave rep / stronghold pursuer) had NO probe:
// pursuit was already surfaced to the HUD posture arc via
// PostureSignals.ReportPursuit (Enemy.DriveNav chasingHero, RegionMobSpawner
// aggro loop, OverworldEncounterSpawner chase drive), yet combat inputs stayed
// dead until contact staged the arena. Owner ruling: while actively PURSUED,
// ranged/melee/Blink must work.
//
// THE FIX (correct layer, additive): register one BattleLock probe that reads
// PostureSignals.PursuitActive. The pursuit pulse self-expires after
// PostureSignals.PursuitTtl (1.5 s), which gives the probe natural hysteresis —
// the lock lingers briefly after the last pursuer breaks off, never flickers.
// Contact-engage still stages the BattleArena exactly as before; town idle
// (no pursuers) reads false, so the town no-combat-buttons rule
// (owner 2026-06-24) is untouched.
//
// Bootstrap follows the established [RuntimeInitializeOnLoadMethod] convention
// (ATBCombatManager.cs:50, AutoPilotInstaller.cs:41, SettingsBootstrap.cs:65).
// Reset-safe: a domain reload clears BattleLock's probe list AND this static;
// the load hook re-registers. RegisterProbe dedups by delegate equality, so a
// repeated AfterSceneLoad fire can never double-add the probe.
// =============================================================================

using UnityEngine;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.HudModel;

namespace DeNelle.Core.Combat
{
    /// <summary>
    /// Feeds <see cref="BattleLock"/> from the A4.5 pursuit pulse
    /// (<see cref="PostureSignals.PursuitActive"/>): while any enemy is actively
    /// pursuing the hero, <see cref="BattleLock.IsInBattle"/> reports true so
    /// combat inputs (melee/ranged/Blink) are live during the chase (F8-46).
    /// </summary>
    public static class PursuitBattleProbe
    {
        // Last value the probe reported — edge-trigger for the transition trace
        // only (no per-frame spam; a steady state logs nothing).
        private static bool _wasActive;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            _wasActive = false;
            BattleLock.RegisterProbe(Probe);
            FlowTrace.Once("Combat", "pursuit-probe-install",
                "pursuit battle-probe registered (BattleLock now honours PostureSignals.PursuitActive, F8-46).");
        }

        private static bool Probe()
        {
            bool active = PostureSignals.PursuitActive;
            if (active != _wasActive)
            {
                _wasActive = active;
                // WO-1603 — EDGE-ONLY, AND IT NAMES THE PULSER. This probe is only the READER of
                // the pursuit ring, yet F8 seq 4701/4702 reported it as the battle-lock HOLDER and
                // the capture went no further, because nothing in the message pointed past the
                // messenger. The transition trace now renders the ring's owner tags + stamp ages.
                // It stays on the TRANSITION (a steady state still logs nothing), so this cannot
                // become the per-frame firehose CLAUDE.md §12 forbids on an aggro path.
                FlowTrace.Step("Combat", active
                    ? "pursuit battle-probe RAISED (PursuitActive) -> combat inputs live during chase. " +
                      $"Pulsed by: {PostureSignals.DescribePursuits()}"
                    : "pursuit battle-probe CLEARED (pursuit pulses expired) -> battle-lock released.");
            }
            return active;
        }
    }
}
