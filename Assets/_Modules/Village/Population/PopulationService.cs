// =============================================================================
// PopulationService -- the Population growth COORDINATOR (WORK_ORDER_587).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Population
//
// Population is a milestone-driven counter that EARNS additional Echo workforce
// SLOTS. It does NOT own a second echo economy -- it DRIVES the existing
// EchoService (memory echo-workforce-drag-drop): when a data-driven milestone is
// met, PopulationService raises the unlocked-slot count and grants the next Echo
// through EchoService.GrantEcho (the same "unlock the next Echo" path the wave
// hook uses). EchoService stays the workforce OWNER (auto-harvest / silo / dump);
// PopulationService only decides WHEN a new slot opens.
//
// Earned inputs (wired by PopulationBootstrap from EXISTING events):
//   quest complete  -> AddPopulationXP(x, "quest")    (+ quest counter)
//   outpost cleared -> AddPopulationXP(x, "outpost")  (+ outpost counter)
//   wave victory    -> AddPopulationXP(x, "wave")     (waves read from GameState)
//   village upgrade -> OnVillageLevelChanged()        (raises the derived cap)
//
// Milestone conditions read accumulated counters: xp / questsCompleted /
// outpostsCleared / wavesCleared (GameState.WavesCompleted) / villageLevel
// (VillageTierService.Current). Table = population-milestones.json (owner-tunable).
//
// PERSISTED via GameState (PopulationXP / PopulationQuests / PopulationOutposts /
// PopulationEchoSlots, schema v28) -- the SAME additive-at-the-END pattern
// EchoService used for EchoCount (v25). populationCap is DERIVED from the village
// tier (not persisted; recomputed on read). Self-bootstrapping DDOL (see
// PopulationBootstrap) -- no scene authoring, mirroring EchoService.
// =============================================================================

using System;
using UnityEngine;
using DeNelle.Core;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.Population;
using DeNelle.Core.State;
using DeNelle.Village.Buildings.Progression;

namespace DeNelle.Village.Population
{
    /// <summary>
    /// Coordinates Population growth: accumulates earned XP + counters, re-evaluates
    /// the data-driven milestones, and unlocks the next Echo slot (≤5) by driving
    /// <see cref="EchoService"/>. Persisted via <see cref="GameState"/> (schema v28).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PopulationService : MonoBehaviour, IPopulationService
    {
        public static PopulationService Instance { get; private set; }

        /// <summary>Hard cap on echo workforce slots (3 organic + 2 flex, memory echo-workforce-drag-drop).</summary>
        public const int MaxEchoSlots = 5;

        // -- Population cap per Village/Stronghold tier (owner-tunable). Index = tier 0..MaxTier. --
        // Tier 0 (fresh village) supports 5; each upgrade lifts the housing ceiling.
        private static readonly int[] CapByTier = { 5, 8, 12, 16 };

        /// <summary>Raised after XP / cap / unlocked-slot count changes (HUD + VMs listen).</summary>
        public event Action Changed;

        /// <summary>Raised once when a new echo slot unlocks (arg = the slot number 2..5).</summary>
        public event Action<int> EchoSlotUnlocked;

        // -- Convenience accessor over the persisted state ---------------------
        private static GameState State => GameStateService.Instance != null ? GameStateService.Instance.State : null;

        // =====================================================================
        //  IPopulationService reads
        // =====================================================================

        /// <summary>Accumulated population XP (0 when state is absent).</summary>
        public int CurrentXP { get { var s = State; return s != null ? Mathf.Max(0, s.PopulationXP) : 0; } }

        /// <summary>Population cap, derived from the current village tier (owner-tunable map).</summary>
        public int PopulationCap => CapForTier(VillageTierService.Current);

        /// <summary>Unlocked echo workforce slots (1..5). 1 when state is absent (the starter Wood echo).</summary>
        public int EchoSlotsUnlocked
        {
            get { var s = State; return s != null ? Mathf.Clamp(s.PopulationEchoSlots, 1, MaxEchoSlots) : 1; }
        }

        /// <summary>
        /// The population cap at an arbitrary Village/Stronghold tier — the SAME ladder
        /// <see cref="PopulationCap"/> reads, exposed so the Heart Level unlock preview can say
        /// what a level actually grants (WO-2004).
        /// <para>⚠ READ-ONLY PROJECTION, NOT A SECOND TABLE. <see cref="CapByTier"/> stays the one
        /// authority and stays private; this method is the only way out of it. HeartProgression
        /// .UnlocksAt calls it rather than re-listing 5/8/12/16, because a hand-copied unlock table
        /// is precisely what WO-2004's "no duplicated Heart-level unlock tables" forbids. It is
        /// also why this is a static reader and not a service instance call — the preview must work
        /// before PopulationService exists in the scene.</para>
        /// <para>⛔ Owner rules on the numbers. The ladder is CODE-side today, not authored JSON;
        /// that is recorded as a gap in the WO-2004 gate audit, not fixed here (moving it would be
        /// a balance-data change smuggled into a progression change — CLAUDE.md §5's lane rule).</para>
        /// </summary>
        public static int CapAtVillageTier(int tier) => CapForTier(tier);

        private static int CapForTier(int tier)
        {
            if (tier < 0) tier = 0;
            if (tier >= CapByTier.Length) tier = CapByTier.Length - 1;
            return CapByTier[tier];
        }

        // =====================================================================
        //  Lifecycle
        // =====================================================================

        private void Awake()
        {
            // Destroy(this) -- NOT Destroy(gameObject): may share a host
            // (CLAUDE.md memory: singleton-dedup-destroys-host).
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            CoreServices.RegisterPopulation(this);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            CoreServices.UnregisterPopulation(this);
        }

        private void Start()
        {
            // Deferred one frame so GameStateService (loads the save in its Awake) is up
            // before we read the persisted population counters + catch up any milestone
            // that the loaded save already satisfies (mirrors EchoService's deferred Start).
            StartCoroutine(EvaluateNextFrame());
        }

        private System.Collections.IEnumerator EvaluateNextFrame()
        {
            yield return null;
            EvaluateMilestones("load");
            Changed?.Invoke();
        }

        // =====================================================================
        //  Mutation -- the single earned-XP entry point
        // =====================================================================

        /// <summary>
        /// Add population XP from an earned <paramref name="source"/>, advance the
        /// matching counter, then re-evaluate milestones (may unlock the next slot).
        /// Logs the source via FlowTrace (§12). Null-safe no-op before the save loads.
        /// </summary>
        public void AddPopulationXP(int amount, string source)
        {
            var s = State;
            if (s == null)
            {
                FlowTrace.Warn("Population", $"AddPopulationXP({amount},'{source}') before GameState -- ignored.");
                return;
            }

            // WO-978: measure what the counter actually took (Mathf.Max can absorb a negative
            // request) rather than echoing `amount` back.
            int xpBefore = s.PopulationXP;
            if (amount > 0) s.PopulationXP = Mathf.Max(0, s.PopulationXP + amount);
            int xpCredited = s.PopulationXP - xpBefore;
            // NOT a shortfall check. Unlike EconomyService.Grant there is no cap seam here: the
            // only clamp is Mathf.Max(0, ...), and with amount > 0 and the PopulationXP >= 0
            // invariant this method itself maintains, the sum is always positive and the clamp is
            // a no-op — so credited can never be LESS than requested. A "SHORT" warn here would be
            // unreachable by construction (the hollow-assertion class this whole pass is closing),
            // and would send the next reader hunting a cap that does not exist. The only way the
            // numbers can disagree is signed-int overflow, so THAT is what is asserted.
            if (amount > 0 && xpCredited != amount)
                FlowTrace.Warn("Population",
                    $"AddPopulationXP OVERFLOW from '{source}': credited {xpCredited} for a request of " +
                    $"{amount} -> xp={s.PopulationXP}. PopulationXP has wrapped or been corrupted; " +
                    "this is not a cap.");

            // Source -> counter. Waves reuse GameState.WavesCompleted (EchoService owns it);
            // village-upgrade carries no XP/counter (cap is derived from the tier).
            switch (source)
            {
                case "quest":   s.PopulationQuests = Mathf.Max(0, s.PopulationQuests + 1); break;
                case "outpost": s.PopulationOutposts = Mathf.Max(0, s.PopulationOutposts + 1); break;
            }

            FlowTrace.Step("Population",
                $"AddPopulationXP credited {xpCredited}/{amount} from '{source}' -> xp={s.PopulationXP}, quests={s.PopulationQuests}, " +
                $"outposts={s.PopulationOutposts}, waves={s.WavesCompleted}, tier={VillageTierService.Current}, " +
                $"slots={EchoSlotsUnlocked}/{MaxEchoSlots}, cap={PopulationCap}.");

            GameStateService.Instance?.Save();
            EvaluateMilestones(source);
            Changed?.Invoke();
        }

        /// <summary>
        /// The village/housing tier changed -- the cap is DERIVED from the tier so it
        /// updates automatically; we only re-evaluate the villageLevel-gated milestones
        /// and notify listeners. Called by PopulationBootstrap when VillageTier rises.
        /// </summary>
        public void OnVillageLevelChanged()
        {
            FlowTrace.Step("Population",
                $"OnVillageLevelChanged -> tier={VillageTierService.Current}, cap={PopulationCap}.");
            EvaluateMilestones("village-upgrade");
            Changed?.Invoke();
        }

        // =====================================================================
        //  Milestone evaluation -- data-driven, sequential, once-only
        // =====================================================================

        /// <summary>
        /// Walk the milestone table in slot order; unlock every milestone whose condition
        /// is now met, strictly one slot at a time (no gaps), each exactly once. Each
        /// unlock raises the persisted slot count, drives EchoService.GrantEcho, and fires
        /// <see cref="EchoSlotUnlocked"/>.
        /// </summary>
        private void EvaluateMilestones(string reason)
        {
            var s = State;
            if (s == null) return;

            var milestones = PopulationMilestonesCatalog.Milestones;
            if (milestones == null) return;

            bool unlockedAny = false;
            foreach (var m in milestones)
            {
                if (m == null) continue;

                int unlocked = EchoSlotsUnlocked;
                if (unlocked >= MaxEchoSlots) break;

                // Strictly the NEXT slot in order -- never skip a gap.
                if (m.EchoSlot != unlocked + 1) continue;

                if (!IsMet(m, s))
                    continue;

                // Grant: persist the new slot, drive the existing workforce, notify.
                //
                // WO-978 -- THIS BLOCK USED TO SELF-REPORT. The trace printed
                // s.PopulationEchoSlots, the field assigned three lines above it, so it could
                // only ever agree with itself; and EchoService.GrantEcho was null-conditional
                // and returns void, so the ECHO half of the unlock could silently not happen
                // (no service, no GameState, or already at EchoService.MaxEchoes) while the log
                // announced "the village grows stronger". Now: read the slot count back through
                // the EchoSlotsUnlocked ACCESSOR (which re-reads and clamps live state, so it can
                // disagree with the write) and measure the echo roster either side of the grant.
                int slotsBefore = EchoSlotsUnlocked;
                int echoesBefore = s.EchoCount;

                s.PopulationEchoSlots = Mathf.Clamp(m.EchoSlot, 1, MaxEchoSlots);

                var echoes = EchoService.Instance;
                if (echoes == null)
                    FlowTrace.Warn("Population",
                        $"milestone slot {m.EchoSlot} ({reason}): no EchoService -- the SLOT opened but NO Echo was " +
                        "granted, so the player has an empty workforce seat and no spirit to fill it.");
                else
                    echoes.GrantEcho($"population-milestone slot {m.EchoSlot} ({reason})");

                GameStateService.Instance?.Save();

                int slotsNow = EchoSlotsUnlocked;          // measured read-back, not the write
                int echoesGranted = s.EchoCount - echoesBefore;

                if (slotsNow != m.EchoSlot || echoesGranted < 1)
                    FlowTrace.Warn("Population",
                        $"Echo slot {m.EchoSlot} ({reason}) did NOT fully land: slots {slotsBefore} -> {slotsNow} " +
                        $"(wanted {m.EchoSlot}), echoes {echoesBefore} -> {s.EchoCount} (granted {echoesGranted}, wanted 1). " +
                        "A slot without an Echo is an unlock the player cannot use -- GrantEcho no-ops at its own cap " +
                        "and before GameState exists.");
                else
                    FlowTrace.Step("Population",
                        $"Echo slot {m.EchoSlot} UNLOCKED ({reason}) -- the village grows stronger. " +
                        $"slots {slotsBefore} -> {slotsNow}/{MaxEchoSlots}, echoes {echoesBefore} -> {s.EchoCount}.");

                EchoSlotUnlocked?.Invoke(m.EchoSlot);
                unlockedAny = true;
            }

            if (unlockedAny) Changed?.Invoke();
        }

        /// <summary>True when the milestone's any/all blocks are both satisfied by the live counters.</summary>
        private static bool IsMet(PopulationMilestone m, GameState s)
        {
            if (m == null || !m.HasAnyCondition) return false;   // never auto-grant a condition-less entry

            bool allOk = m.All == null || m.All.IsEmpty || AllSatisfied(m.All, s);
            bool anyOk = m.Any == null || m.Any.IsEmpty || AnySatisfied(m.Any, s);
            return allOk && anyOk;
        }

        // Every active (>0) field must be reached.
        private static bool AllSatisfied(MilestoneCondition c, GameState s)
        {
            if (c.Xp > 0 && s.PopulationXP < c.Xp) return false;
            if (c.QuestsCompleted > 0 && s.PopulationQuests < c.QuestsCompleted) return false;
            if (c.OutpostsCleared > 0 && s.PopulationOutposts < c.OutpostsCleared) return false;
            if (c.WavesCleared > 0 && s.WavesCompleted < c.WavesCleared) return false;
            if (c.VillageLevel > 0 && VillageTierService.Current < c.VillageLevel) return false;
            return true;
        }

        // At least one active (>0) field reached.
        private static bool AnySatisfied(MilestoneCondition c, GameState s)
        {
            if (c.Xp > 0 && s.PopulationXP >= c.Xp) return true;
            if (c.QuestsCompleted > 0 && s.PopulationQuests >= c.QuestsCompleted) return true;
            if (c.OutpostsCleared > 0 && s.PopulationOutposts >= c.OutpostsCleared) return true;
            if (c.WavesCleared > 0 && s.WavesCompleted >= c.WavesCleared) return true;
            if (c.VillageLevel > 0 && VillageTierService.Current >= c.VillageLevel) return true;
            return false;
        }
    }
}
