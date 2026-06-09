// =============================================================================
// ArenaAttackRecruitController — the Arena ATTACK recruit loop (WO-389 #3, MVP).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Arena
//
// The ATTACK twin of ArenaDefenseSetupController, kept SLIM and SIMPLER: there is
// NO grid placement here — the player RECRUITS a squad (a roster, a List<string>
// of troop ids), spending the point pool (DefensePointPool = 50, reused as the
// squad budget). Each card tap ADDS one troop; the running sum must stay <= 50
// (reuses ArenaDefenseCatalog.CanAfford on a synthesized PlacedDefenderData list).
//
// On Launch the controller commits the recruited roster to ArenaMode.AttackSquad
// (a static MVP store — see the field) and starts the raid (ArenaMode.TryStartRaid),
// which spawns the squad hero-leashed to the Captain (StoryCompanionInjector
// .SpawnAttacker). On Cancel it clears the in-progress roster and hides the UI.
//
// Singleton; reuses the catalog + ArenaAttackPaletteUI rather than forking a
// parallel system. Code-built UI (UXML renders empty in builds).
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core.State;

namespace DeNelle.Village.Arena
{
    /// <summary>
    /// Drives the Arena Attack recruit session: enter/exit, tap-to-recruit a squad
    /// roster within the point pool, then commit + launch the raid. Singleton; reuses
    /// <see cref="ArenaDefenseCatalog"/> + <see cref="ArenaAttackPaletteUI"/>.
    /// </summary>
    public sealed class ArenaAttackRecruitController : MonoBehaviour
    {
        public static ArenaAttackRecruitController Instance { get; private set; }

        /// <summary>Broadcast on enter (true) / exit (false) so a HUD bridge can hide
        /// overlapping combat UI during recruiting (mirrors the defense controller).</summary>
        public static event System.Action<bool> RecruitModeChanged;

        /// <summary>True while a recruit session is active.</summary>
        public bool IsActive { get; private set; }

        // The roster being assembled this session (troop ids). Committed to
        // ArenaMode.AttackSquad on Launch; abandoned on Cancel.
        private readonly List<string> _squad = new List<string>();

        // The opponent the launched raid targets. Defaults to the first seeded
        // ArenaCatalog opponent (MVP) until a caller sets one via SetOpponent.
        private ArenaOpponentDef _opponent;

        private ArenaAttackPaletteUI _palette;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Ensure a controller exists (entry point for a HUD "Attack" button).</summary>
        public static ArenaAttackRecruitController EnsureExists()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("ArenaAttackRecruitController");
            return go.AddComponent<ArenaAttackRecruitController>();
        }

        /// <summary>Which opponent the launched raid fights. Null falls back to the
        /// first seeded ArenaCatalog opponent at Launch (MVP).</summary>
        public void SetOpponent(ArenaOpponentDef opponent) => _opponent = opponent;

        /// <summary>Toggle the recruit session.</summary>
        public void Toggle()
        {
            if (IsActive) Exit();
            else Enter();
        }

        // =====================================================================
        //  Enter / Exit
        // =====================================================================

        /// <summary>Enter recruiting: clear the roster, show the recruit palette.</summary>
        public void Enter()
        {
            if (IsActive) return;
            IsActive = true;

            _squad.Clear();

            EnsurePalette();
            ShowPalette();

            // Suppress the whole gameplay HUD so the live town/wave readout doesn't
            // bleed through this full-screen modal (the recruit palette adds its own
            // covering backdrop; this also kills the wave text behind it).
            ArenaHudBridge.SetVisible(false);

            RecruitModeChanged?.Invoke(true);
            Debug.Log("[ArenaAttack] Entered Arena Attack recruiting.");
        }

        /// <summary>Exit recruiting WITHOUT launching (Cancel) — hide UI, drop the roster.</summary>
        public void Exit()
        {
            if (!IsActive) return;
            IsActive = false;

            _palette?.Hide();
            _squad.Clear();

            // Restore the gameplay HUD suppressed on Enter.
            ArenaHudBridge.SetVisible(true);

            RecruitModeChanged?.Invoke(false);
            Debug.Log("[ArenaAttack] Exited Arena Attack recruiting (cancelled).");
        }

        // =====================================================================
        //  Recruit loop
        // =====================================================================

        /// <summary>
        /// Add one troop to the squad if the pool can still afford it. The point pool is
        /// the single budget gate (reuses <see cref="ArenaDefenseCatalog.CanAfford"/> on
        /// the current roster). Re-renders the palette against the new spend.
        /// </summary>
        private void Recruit(ArenaDefenseDef def)
        {
            if (def == null) return;

            if (!ArenaDefenseCatalog.CanAfford(SquadAsPlaced(), def.Id))
            {
                Debug.Log($"[ArenaAttack] Not enough squad points to recruit '{def.Id}'.");
                return;
            }

            _squad.Add(def.Id);
            Debug.Log($"[ArenaAttack] Recruited '{def.Id}' — squad now {_squad.Count} units, " +
                      $"{ArenaDefenseCatalog.RemainingPoints(SquadAsPlaced())} pts left.");
            RefreshPalette();
        }

        /// <summary>
        /// Commit the recruited roster and start the raid. The squad is handed to
        /// <see cref="ArenaMode.AttackSquad"/> (the spawn hook reads it on raid start),
        /// then <see cref="ArenaMode.TryStartRaid"/> runs. A failed start (no opponent /
        /// can't afford the wager) leaves recruiting open so the player can retry.
        /// </summary>
        private void Launch()
        {
            if (_squad.Count == 0)
            {
                Debug.Log("[ArenaAttack] Cannot launch — no troops recruited.");
                return;
            }

            var opponent = _opponent;
            if (opponent == null)
            {
                // MVP fallback: the first seeded opponent. A real caller sets one via
                // SetOpponent before Enter(). // TODO data-driven: opponent selection UI.
                var all = ArenaCatalog.All;
                opponent = (all != null && all.Count > 0) ? all[0] : null;
            }
            if (opponent == null)
            {
                Debug.LogWarning("[ArenaAttack] Cannot launch — no opponent available.");
                return;
            }

            // Commit the roster so ArenaMode's attack-spawn hook can read it.
            ArenaMode.AttackSquad = new List<string>(_squad);

            IsActive = false;
            _palette?.Hide();
            // Restore the gameplay HUD suppressed on Enter (the live raid HUD takes over).
            ArenaHudBridge.SetVisible(true);
            RecruitModeChanged?.Invoke(false);

            Debug.Log($"[ArenaAttack] Launching raid vs '{opponent.DisplayName}' with a {_squad.Count}-unit squad.");
            bool started = ArenaMode.Instance.TryStartRaid(opponent);
            if (!started)
            {
                // Raid blocked (e.g. can't afford the wager) — clear the committed squad
                // so a stale roster doesn't spawn on a later defend raid.
                ArenaMode.AttackSquad = null;
                Debug.LogWarning("[ArenaAttack] Raid did not start — squad cleared.");
            }
        }

        // =====================================================================
        //  Budget helpers
        // =====================================================================

        /// <summary>
        /// The recruited roster expressed as a <see cref="PlacedDefenderData"/> list so
        /// the point-pool math (SpentPoints / RemainingPoints / CanAfford) — which is
        /// written against placed-defender lists — works unchanged. Cell coords are
        /// irrelevant for an attack squad (no placement), so they are 0.
        /// </summary>
        private List<PlacedDefenderData> SquadAsPlaced()
        {
            var list = new List<PlacedDefenderData>(_squad.Count);
            foreach (var id in _squad)
                list.Add(new PlacedDefenderData(id, 0, 0, 0));
            return list;
        }

        // =====================================================================
        //  Palette wiring
        // =====================================================================

        private void EnsurePalette()
        {
            if (_palette != null) return;
            var go = new GameObject("ArenaAttackPaletteUI");
            _palette = go.AddComponent<ArenaAttackPaletteUI>();
            _palette.OnRecruit += Recruit;
            _palette.OnLaunchRequested += Launch;
            _palette.OnCancelRequested += Exit;
        }

        private void ShowPalette()
        {
            var placed = SquadAsPlaced();
            int spent = ArenaDefenseCatalog.SpentPoints(placed);
            int remaining = ArenaDefenseCatalog.RemainingPoints(placed);
            _palette?.Show(spent, remaining, _squad.Count);
        }

        private void RefreshPalette()
        {
            var placed = SquadAsPlaced();
            int spent = ArenaDefenseCatalog.SpentPoints(placed);
            int remaining = ArenaDefenseCatalog.RemainingPoints(placed);
            _palette?.Render(spent, remaining, _squad.Count);
        }
    }
}
