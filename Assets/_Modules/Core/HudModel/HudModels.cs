// =============================================================================
// HudModels — WO-541 Stage 1: the 11 Core HUD models + IHudModel facade + holder.
// -----------------------------------------------------------------------------
// DARK / ADDITIVE: each model is plain read-only data + a `Changed` event + a
// single producer-only mutator that ASSIGNS, FIRES Changed, then FlowTraces the
// transition ([Flow:HUD]). Views (Stage 3) READ props + subscribe Changed.
// Producers (Village, Stage 2) call the mutators. Nothing reads these yet, so
// this layer changes zero runtime behaviour.
//
// Assembly law: DeNelle.Core only — primitives + Core enums. NO UnityEngine UI,
// NO Village types. Contract: WorkOrders/WO541_MODEL_API.md (FROZEN).
// =============================================================================

using System;
using System.Collections.Generic;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Core.HudModel
{
    // ── HeroVitals ────────────────────────────────────────────────────────────

    /// <summary>The controlled hero's vitals + progression (HP/MP/XP/level/class).</summary>
    public sealed class HeroVitalsModel
    {
        /// <summary>Current hit points.</summary>
        public int Hp { get; private set; }
        /// <summary>Maximum hit points.</summary>
        public int MaxHp { get; private set; }
        /// <summary>Current mana.</summary>
        public int Mana { get; private set; }
        /// <summary>Maximum mana.</summary>
        public int MaxMana { get; private set; }
        /// <summary>Current experience toward the next level.</summary>
        public int Xp { get; private set; }
        /// <summary>Experience required to reach the next level.</summary>
        public int XpToNext { get; private set; }
        /// <summary>Current level.</summary>
        public int Level { get; private set; }
        /// <summary>Hero class identifier.</summary>
        public string ClassId { get; private set; }

        /// <summary>Raised after any field changes.</summary>
        public event Action Changed;

        /// <summary>Producer-only mutator: assign all fields, fire Changed, trace (throttled).</summary>
        public void Set(int hp, int maxHp, int mana, int maxMana, int xp, int xpToNext, int level, string classId)
        {
            Hp = hp;
            MaxHp = maxHp;
            Mana = mana;
            MaxMana = maxMana;
            Xp = xp;
            XpToNext = xpToNext;
            Level = level;
            ClassId = classId;
            Changed?.Invoke();
            FlowTrace.Throttle("HUD", "herovitals", 1f, $"HP {Hp}/{MaxHp} MP {Mana}/{MaxMana} XP {Xp}/{XpToNext} Lv{Level} [{ClassId}]");
        }
    }

    // ── Party ─────────────────────────────────────────────────────────────────

    /// <summary>The party roster snapshot.</summary>
    public sealed class PartyModel
    {
        /// <summary>The current party members (never null).</summary>
        public IReadOnlyList<PartyMemberRecord> Members { get; private set; } = Array.Empty<PartyMemberRecord>();

        /// <summary>Raised after the roster changes.</summary>
        public event Action Changed;

        /// <summary>Producer-only mutator: replace the roster, fire Changed, trace (throttled — hot).</summary>
        public void SetMembers(IReadOnlyList<PartyMemberRecord> members)
        {
            Members = members ?? Array.Empty<PartyMemberRecord>();
            Changed?.Invoke();
            FlowTrace.Throttle("HUD", "party", 1f, $"party members={Members.Count}");
        }
    }

    // ── Economy ───────────────────────────────────────────────────────────────

    /// <summary>The player's resource balances.</summary>
    public sealed class EconomyModel
    {
        /// <summary>Gold balance.</summary>
        public int Gold { get; private set; }
        /// <summary>Wood balance.</summary>
        public int Wood { get; private set; }
        /// <summary>Iron balance.</summary>
        public int Iron { get; private set; }
        /// <summary>Food balance.</summary>
        public int Food { get; private set; }
        /// <summary>Crystals balance.</summary>
        public int Crystals { get; private set; }

        /// <summary>Raised after any balance changes.</summary>
        public event Action Changed;

        /// <summary>Producer-only mutator: assign all balances, fire Changed, trace (throttled).</summary>
        public void Set(int gold, int wood, int iron, int food, int crystals)
        {
            Gold = gold;
            Wood = wood;
            Iron = iron;
            Food = food;
            Crystals = crystals;
            Changed?.Invoke();
            FlowTrace.Throttle("HUD", "economy", 1f, $"G{Gold} W{Wood} I{Iron} F{Food} C{Crystals}");
        }
    }

    // ── Wave ──────────────────────────────────────────────────────────────────

    /// <summary>The current/next wave state (phase, counts, lookout, banner).</summary>
    public sealed class WaveModel
    {
        /// <summary>Wave lifecycle phase.</summary>
        public WavePhase Phase { get; private set; }
        /// <summary>Current wave number.</summary>
        public int Number { get; private set; }
        /// <summary>Maximum wave number.</summary>
        public int Max { get; private set; }
        /// <summary>Seconds remaining on the pre-wave countdown.</summary>
        public float CountdownRemaining { get; private set; }
        /// <summary>Whether a wave is imminent.</summary>
        public bool Imminent { get; private set; }
        /// <summary>Lookout status text.</summary>
        public string LookoutStatus { get; private set; }
        /// <summary>Enemies still alive this wave.</summary>
        public int EnemiesLive { get; private set; }
        /// <summary>Total enemies this wave.</summary>
        public int EnemiesTotal { get; private set; }
        /// <summary>Banner text shown on wave clear.</summary>
        public string ClearBanner { get; private set; }

        /// <summary>Raised after any field changes.</summary>
        public event Action Changed;

        /// <summary>Producer-only mutator: assign all fields, fire Changed, trace (throttled).</summary>
        public void Set(WavePhase phase, int number, int max, float countdown, bool imminent,
            string lookout, int live, int total, string banner)
        {
            Phase = phase;
            Number = number;
            Max = max;
            CountdownRemaining = countdown;
            Imminent = imminent;
            LookoutStatus = lookout;
            EnemiesLive = live;
            EnemiesTotal = total;
            ClearBanner = banner;
            Changed?.Invoke();
            FlowTrace.Throttle("HUD", "wave", 1f, $"{Phase} wave {Number}/{Max} live {EnemiesLive}/{EnemiesTotal} cd{CountdownRemaining:F1}");
        }
    }

    // ── Target ────────────────────────────────────────────────────────────────

    /// <summary>The currently-locked single target's detail.</summary>
    public sealed class TargetModel
    {
        /// <summary>Whether a target is currently held.</summary>
        public bool HasTarget { get; private set; }
        /// <summary>Target display name.</summary>
        public string Name { get; private set; }
        /// <summary>Target level.</summary>
        public int Level { get; private set; }
        /// <summary>Target current HP.</summary>
        public int Hp { get; private set; }
        /// <summary>Target maximum HP.</summary>
        public int MaxHp { get; private set; }
        /// <summary>Normalised remaining HP (0..1).</summary>
        public float HpFraction { get; private set; }
        /// <summary>Target combat role.</summary>
        public HudRole Role { get; private set; }
        /// <summary>Whether the target is locked.</summary>
        public bool Locked { get; private set; }

        /// <summary>Raised after the target detail changes (set or cleared).</summary>
        public event Action Changed;

        /// <summary>Producer-only mutator: assign all fields, fire Changed, trace.</summary>
        public void Set(bool has, string name, int level, int hp, int maxHp, float frac, HudRole role, bool locked)
        {
            HasTarget = has;
            Name = name;
            Level = level;
            Hp = hp;
            MaxHp = maxHp;
            HpFraction = frac;
            Role = role;
            Locked = locked;
            Changed?.Invoke();
            FlowTrace.Step("HUD", $"target changed: has={HasTarget} '{Name}' Lv{Level} {Hp}/{MaxHp} ({Role}) locked={Locked}");
        }

        /// <summary>Producer-only mutator: clear the target, fire Changed, trace.</summary>
        public void Clear()
        {
            HasTarget = false;
            Name = null;
            Level = 0;
            Hp = 0;
            MaxHp = 0;
            HpFraction = 0f;
            Role = HudRole.None;
            Locked = false;
            Changed?.Invoke();
            FlowTrace.Step("HUD", "target cleared");
        }
    }

    // ── TargetCycle ───────────────────────────────────────────────────────────

    /// <summary>The cyclable target list (scan + distance-sorted by the producer).</summary>
    public sealed class TargetCycleModel
    {
        /// <summary>The current cyclable targets (never null).</summary>
        public IReadOnlyList<TargetRecord> Targets { get; private set; } = Array.Empty<TargetRecord>();

        /// <summary>Raised after the target list changes.</summary>
        public event Action Changed;

        /// <summary>Producer-only mutator: replace the list, fire Changed, trace (throttled — hot).</summary>
        public void SetTargets(IReadOnlyList<TargetRecord> targets)
        {
            Targets = targets ?? Array.Empty<TargetRecord>();
            Changed?.Invoke();
            FlowTrace.Throttle("HUD", "targetcycle", 1f, $"cycle targets={Targets.Count}");
        }
    }

    // ── AbilityLoadout ────────────────────────────────────────────────────────

    /// <summary>The hero's 4 ability slots + their cooldowns.</summary>
    public sealed class AbilityLoadoutModel
    {
        /// <summary>The current ability slots (4 expected; never null).</summary>
        public IReadOnlyList<AbilitySlotRecord> Slots { get; private set; } = Array.Empty<AbilitySlotRecord>();

        /// <summary>Raised after the slots or a cooldown changes.</summary>
        public event Action Changed;

        /// <summary>Producer-only mutator: replace all slots, fire Changed, trace.</summary>
        public void SetSlots(IReadOnlyList<AbilitySlotRecord> slots)
        {
            Slots = slots ?? Array.Empty<AbilitySlotRecord>();
            Changed?.Invoke();
            FlowTrace.Step("HUD", $"abilities set: slots={Slots.Count}");
        }

        /// <summary>
        /// Producer-only mutator: update one slot's cooldown in place, fire Changed, trace
        /// (throttled — hot per-frame cooldown tick). Out-of-range index is ignored (traced).
        /// </summary>
        public void SetCooldown(int index, float remaining, float total)
        {
            if (Slots == null || index < 0 || index >= Slots.Count)
            {
                FlowTrace.Step("HUD", $"ability cooldown ignored: index {index} out of range (slots={Slots?.Count ?? 0})");
                return;
            }

            // Rebuild the slot record with the new cooldown (records are immutable).
            var src = Slots[index];
            var updated = new AbilitySlotRecord(src.Key, src.Glyph, src.Name, src.Desc, src.IconKey,
                src.AccentHex, src.Equipped, remaining, total);

            var copy = new AbilitySlotRecord[Slots.Count];
            for (int i = 0; i < Slots.Count; i++) copy[i] = Slots[i];
            copy[index] = updated;
            Slots = copy;

            Changed?.Invoke();
            FlowTrace.Throttle("HUD", "abilitycd", 1f, $"slot {index} cd {remaining:F1}/{total:F1}");
        }
    }

    // ── WorldMetrics ──────────────────────────────────────────────────────────

    /// <summary>Holistic world metrics (heart, towers, population, passives, wards, minimap).</summary>
    public sealed class WorldMetricsModel
    {
        /// <summary>Heart current HP.</summary>
        public int HeartHp { get; private set; }
        /// <summary>Heart maximum HP.</summary>
        public int HeartMaxHp { get; private set; }
        /// <summary>Normalised heart HP (0..1).</summary>
        public float HeartPct { get; private set; }
        /// <summary>Towers currently built.</summary>
        public int TowersBuilt { get; private set; }
        /// <summary>Maximum towers.</summary>
        public int TowersMax { get; private set; }
        /// <summary>Current population.</summary>
        public int Population { get; private set; }
        /// <summary>Passive XP gained per minute.</summary>
        public float PassiveXpPerMin { get; private set; }
        /// <summary>Count of passive-generating towers.</summary>
        public int PassiveTowerCount { get; private set; }
        /// <summary>Current "forgetting" level (decay metric).</summary>
        public int ForgettingLevel { get; private set; }
        /// <summary>Wards currently lit.</summary>
        public int WardsLit { get; private set; }
        /// <summary>Total wards.</summary>
        public int WardsTotal { get; private set; }
        /// <summary>Wards summary text.</summary>
        public string WardsSummary { get; private set; }
        /// <summary>The minimap points of interest (never null).</summary>
        public IReadOnlyList<MinimapPoiRecord> Minimap { get; private set; } = Array.Empty<MinimapPoiRecord>();

        /// <summary>Raised after metrics or the minimap change.</summary>
        public event Action Changed;

        /// <summary>Producer-only mutator: assign all scalar metric fields, fire Changed, trace (throttled).</summary>
        public void SetMetrics(int heartHp, int heartMaxHp, float heartPct, int towersBuilt, int towersMax,
            int population, float passiveXpPerMin, int passiveTowerCount, int forgettingLevel,
            int wardsLit, int wardsTotal, string wardsSummary)
        {
            HeartHp = heartHp;
            HeartMaxHp = heartMaxHp;
            HeartPct = heartPct;
            TowersBuilt = towersBuilt;
            TowersMax = towersMax;
            Population = population;
            PassiveXpPerMin = passiveXpPerMin;
            PassiveTowerCount = passiveTowerCount;
            ForgettingLevel = forgettingLevel;
            WardsLit = wardsLit;
            WardsTotal = wardsTotal;
            WardsSummary = wardsSummary;
            Changed?.Invoke();
            FlowTrace.Throttle("HUD", "worldmetrics", 1f, $"heart {HeartHp}/{HeartMaxHp} towers {TowersBuilt}/{TowersMax} pop {Population} wards {WardsLit}/{WardsTotal}");
        }

        /// <summary>Producer-only mutator: replace the minimap POIs, fire Changed, trace (throttled — hot).</summary>
        public void SetMinimap(IReadOnlyList<MinimapPoiRecord> minimap)
        {
            Minimap = minimap ?? Array.Empty<MinimapPoiRecord>();
            Changed?.Invoke();
            FlowTrace.Throttle("HUD", "minimap", 1f, $"minimap pois={Minimap.Count}");
        }
    }

    // ── Momentum ──────────────────────────────────────────────────────────────

    /// <summary>Combat momentum (combo, kill-streak, stars, battle/keep-star timers).</summary>
    public sealed class MomentumModel
    {
        /// <summary>Current combo count.</summary>
        public int Combo { get; private set; }
        /// <summary>Current kill streak.</summary>
        public int KillStreak { get; private set; }
        /// <summary>Earned stars.</summary>
        public int Stars { get; private set; }
        /// <summary>Elapsed battle time in seconds.</summary>
        public float BattleElapsed { get; private set; }
        /// <summary>Seconds counting toward the keep-star bonus.</summary>
        public float KeepStarSeconds { get; private set; }

        /// <summary>Raised after any field changes.</summary>
        public event Action Changed;

        /// <summary>Producer-only mutator: assign all fields, fire Changed, trace (throttled).</summary>
        public void Set(int combo, int killStreak, int stars, float elapsed, float keepStar)
        {
            Combo = combo;
            KillStreak = killStreak;
            Stars = stars;
            BattleElapsed = elapsed;
            KeepStarSeconds = keepStar;
            Changed?.Invoke();
            FlowTrace.Throttle("HUD", "momentum", 1f, $"combo {Combo} streak {KillStreak} stars {Stars} t{BattleElapsed:F1}");
        }
    }

    // ── Echo ──────────────────────────────────────────────────────────────────

    /// <summary>The echo workforce state (count/cap + life-force silo fill).</summary>
    public sealed class EchoModel
    {
        /// <summary>Current echo count.</summary>
        public int EchoCount { get; private set; }
        /// <summary>Maximum echoes.</summary>
        public int MaxEchoes { get; private set; }
        /// <summary>Life-force silo amount.</summary>
        public float Silo { get; private set; }
        /// <summary>Normalised silo fill (0..1).</summary>
        public float FillFraction { get; private set; }

        /// <summary>Raised after any field changes.</summary>
        public event Action Changed;

        /// <summary>Producer-only mutator: assign all fields, fire Changed, trace (throttled).</summary>
        public void Set(int count, int max, float silo, float fill)
        {
            EchoCount = count;
            MaxEchoes = max;
            Silo = silo;
            FillFraction = fill;
            Changed?.Invoke();
            FlowTrace.Throttle("HUD", "echo", 1f, $"echoes {EchoCount}/{MaxEchoes} silo {Silo:F1} fill {FillFraction:F2}");
        }
    }

    // ── HudContext ────────────────────────────────────────────────────────────

    /// <summary>The consolidated HUD context (single context writer feeds this — see Stage 2).</summary>
    public sealed class HudContextModel
    {
        /// <summary>The active HUD context.</summary>
        public HudContext Context { get; private set; }
        /// <summary>Whether the hero is in the village.</summary>
        public bool InVillage { get; private set; }
        /// <summary>Whether combat is active.</summary>
        public bool CombatActive { get; private set; }
        /// <summary>Whether a modal is open.</summary>
        public bool ModalOpen { get; private set; }

        /// <summary>Raised ONLY when <see cref="Context"/> actually changes value.</summary>
        public event Action Changed;

        /// <summary>
        /// Producer-only mutator: assign all fields. Fires Changed ONLY when the Context
        /// value actually changes, but ALWAYS FlowTraces the transition.
        /// </summary>
        public void Set(HudContext ctx, bool inVillage, bool combat, bool modal)
        {
            bool contextChanged = Context != ctx;
            var prev = Context;

            Context = ctx;
            InVillage = inVillage;
            CombatActive = combat;
            ModalOpen = modal;

            if (contextChanged) Changed?.Invoke();
            FlowTrace.Step("HUD", $"context {(contextChanged ? prev + " -> " + ctx : ctx.ToString())} (village={InVillage} combat={CombatActive} modal={ModalOpen})");
        }
    }

    // ── Facade + holder ───────────────────────────────────────────────────────

    /// <summary>
    /// Read-only facade exposing every HUD model. Views resolve this via
    /// <see cref="CoreServices.HudModel"/>, read props, and subscribe each model's Changed.
    /// </summary>
    public interface IHudModel
    {
        /// <summary>The hero vitals model.</summary>
        HeroVitalsModel HeroVitals { get; }
        /// <summary>The party roster model.</summary>
        PartyModel Party { get; }
        /// <summary>The economy/resources model.</summary>
        EconomyModel Economy { get; }
        /// <summary>The wave-state model.</summary>
        WaveModel Wave { get; }
        /// <summary>The single-target detail model.</summary>
        TargetModel Target { get; }
        /// <summary>The cyclable-target-list model.</summary>
        TargetCycleModel TargetCycle { get; }
        /// <summary>The ability loadout model.</summary>
        AbilityLoadoutModel Abilities { get; }
        /// <summary>The world-metrics model.</summary>
        WorldMetricsModel World { get; }
        /// <summary>The combat-momentum model.</summary>
        MomentumModel Momentum { get; }
        /// <summary>The echo-workforce model.</summary>
        EchoModel Echo { get; }
        /// <summary>The HUD-context model.</summary>
        HudContextModel Context { get; }
    }

    /// <summary>
    /// Concrete <see cref="IHudModel"/> holder: constructs and holds exactly one
    /// instance of each model. Producers (Stage 2) write the models; the host
    /// registers this instance with <see cref="CoreServices.RegisterHudModel"/>.
    /// </summary>
    public sealed class HudModel : IHudModel
    {
        /// <inheritdoc/>
        public HeroVitalsModel HeroVitals { get; } = new HeroVitalsModel();
        /// <inheritdoc/>
        public PartyModel Party { get; } = new PartyModel();
        /// <inheritdoc/>
        public EconomyModel Economy { get; } = new EconomyModel();
        /// <inheritdoc/>
        public WaveModel Wave { get; } = new WaveModel();
        /// <inheritdoc/>
        public TargetModel Target { get; } = new TargetModel();
        /// <inheritdoc/>
        public TargetCycleModel TargetCycle { get; } = new TargetCycleModel();
        /// <inheritdoc/>
        public AbilityLoadoutModel Abilities { get; } = new AbilityLoadoutModel();
        /// <inheritdoc/>
        public WorldMetricsModel World { get; } = new WorldMetricsModel();
        /// <inheritdoc/>
        public MomentumModel Momentum { get; } = new MomentumModel();
        /// <inheritdoc/>
        public EchoModel Echo { get; } = new EchoModel();
        /// <inheritdoc/>
        public HudContextModel Context { get; } = new HudContextModel();
    }
}
