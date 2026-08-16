// =============================================================================
// HudModelTypes — WO-541 Stage 1: shared enums + immutable record structs for the
// Core HUD model layer (namespace DeNelle.Core.HudModel, assembly DeNelle.Core).
// -----------------------------------------------------------------------------
// DARK / ADDITIVE: pure data only. Primitives + Core enums — NO UnityEngine UI,
// NO Village types. Producers (Village, Stage 2) write the models; views (HUD /
// BattleATB, Stage 3) read them. This file holds the value types those models use.
// Contract: WorkOrders/WO541_MODEL_API.md (FROZEN — do not rename/deviate).
// =============================================================================

namespace DeNelle.Core.HudModel
{
    // ── Enums ────────────────────────────────────────────────────────────────

    /// <summary>
    /// The top-level HUD presentation context (which layout/affordances are live).
    /// BuildMode appended (P4, HUD_OBSIDIAN_ARCHITECTURE_2026-07-03 §3.3) — the 4th space
    /// type (Town/World/Battle/BuildMode); appended LAST so existing comparisons are unmoved.
    /// </summary>
    public enum HudContext { Town, Overworld, Battle, Modal, BuildMode }

    /// <summary>A combat role classification, used for target colouring/iconography.</summary>
    public enum HudRole { None, Warrior, Tank, Mage }

    /// <summary>The lifecycle phase of the current/next wave.</summary>
    public enum WavePhase { Idle, Countdown, Active, Cleared, Breached, Defeated }

    // ── Record structs ───────────────────────────────────────────────────────

    /// <summary>One party member's display snapshot (immutable value).</summary>
    public readonly struct PartyMemberRecord
    {
        /// <summary>Display name.</summary>
        public readonly string Name;
        /// <summary>Class identifier (e.g. "warrior").</summary>
        public readonly string ClassId;
        /// <summary>Current hit points.</summary>
        public readonly int Hp;
        /// <summary>Maximum hit points.</summary>
        public readonly int MaxHp;
        /// <summary>Current mana.</summary>
        public readonly int Mana;
        /// <summary>Maximum mana.</summary>
        public readonly int MaxMana;
        /// <summary>Portrait lookup key.</summary>
        public readonly string PortraitKey;
        /// <summary>Whether the member is alive.</summary>
        public readonly bool Alive;
        /// <summary>Whether the member should be shown in the party UI.</summary>
        public readonly bool Visible;

        /// <summary>Constructs an immutable party-member snapshot from all fields.</summary>
        public PartyMemberRecord(string name, string classId, int hp, int maxHp, int mana, int maxMana,
            string portraitKey, bool alive, bool visible)
        {
            Name = name;
            ClassId = classId;
            Hp = hp;
            MaxHp = maxHp;
            Mana = mana;
            MaxMana = maxMana;
            PortraitKey = portraitKey;
            Alive = alive;
            Visible = visible;
        }
    }

    /// <summary>One cyclable target's display snapshot (immutable value).</summary>
    public readonly struct TargetRecord
    {
        /// <summary>Stable target identifier.</summary>
        public readonly string Id;
        /// <summary>Display name.</summary>
        public readonly string Name;
        /// <summary>Normalised remaining HP (0..1).</summary>
        public readonly float HpFraction;
        /// <summary>Combat role classification.</summary>
        public readonly HudRole Role;
        /// <summary>Whether the target is alive.</summary>
        public readonly bool Alive;

        /// <summary>Constructs an immutable target snapshot from all fields.</summary>
        public TargetRecord(string id, string name, float hpFraction, HudRole role, bool alive)
        {
            Id = id;
            Name = name;
            HpFraction = hpFraction;
            Role = role;
            Alive = alive;
        }
    }

    /// <summary>One ability slot's display snapshot (immutable value).</summary>
    public readonly struct AbilitySlotRecord
    {
        /// <summary>Slot/input key (e.g. "1", "Q").</summary>
        public readonly string Key;
        /// <summary>Short glyph for compact display.</summary>
        public readonly string Glyph;
        /// <summary>Ability display name.</summary>
        public readonly string Name;
        /// <summary>Ability description / tooltip text.</summary>
        public readonly string Desc;
        /// <summary>Icon lookup key.</summary>
        public readonly string IconKey;
        /// <summary>Accent colour as a hex string (e.g. "#FFCC00").</summary>
        public readonly string AccentHex;
        /// <summary>Whether an ability is equipped in this slot.</summary>
        public readonly bool Equipped;
        /// <summary>Seconds of cooldown remaining (0 = ready).</summary>
        public readonly float CooldownRemaining;
        /// <summary>Total cooldown duration in seconds.</summary>
        public readonly float CooldownTotal;
        /// <summary>WO-999: resource cost of this skill (0 = free basic / free universal). Display as a digit pip.</summary>
        public readonly float ManaCost;
        /// <summary>WO-999: whether the hero can afford the cost right now (true when free or mana &gt;= cost).</summary>
        public readonly bool Affordable;

        /// <summary>Constructs an immutable ability-slot snapshot from all fields.</summary>
        public AbilitySlotRecord(string key, string glyph, string name, string desc, string iconKey,
            string accentHex, bool equipped, float cooldownRemaining, float cooldownTotal,
            float manaCost = 0f, bool affordable = true)
        {
            Key = key;
            Glyph = glyph;
            Name = name;
            Desc = desc;
            IconKey = iconKey;
            AccentHex = accentHex;
            Equipped = equipped;
            CooldownRemaining = cooldownRemaining;
            CooldownTotal = cooldownTotal;
            ManaCost = manaCost;
            Affordable = affordable;
        }
    }

    /// <summary>One buff/debuff icon for a status-effects row (immutable value).</summary>
    public readonly struct StatusIconRecord
    {
        /// <summary>Stable status id (e.g. "slow", "mana-draught").</summary>
        public readonly string Id;
        /// <summary>Short player-facing label.</summary>
        public readonly string Label;
        /// <summary>Icon lookup key for UiStyle.Icon.</summary>
        public readonly string IconKey;
        /// <summary>True for buffs; false for debuffs.</summary>
        public readonly bool IsBuff;
        /// <summary>Seconds remaining on the effect.</summary>
        public readonly float RemainingSeconds;
        /// <summary>Original applied duration (HUD sweep denominator).</summary>
        public readonly float TotalSeconds;

        /// <summary>Constructs an immutable status-icon snapshot.</summary>
        public StatusIconRecord(string id, string label, string iconKey, bool isBuff,
            float remainingSeconds, float totalSeconds)
        {
            Id = id ?? "";
            Label = label ?? "";
            IconKey = iconKey ?? id ?? "";
            IsBuff = isBuff;
            RemainingSeconds = remainingSeconds;
            TotalSeconds = totalSeconds > 0f ? totalSeconds : remainingSeconds;
        }
    }

    /// <summary>One minimap point-of-interest (immutable value, world XZ + kind tag).</summary>
    public readonly struct MinimapPoiRecord
    {
        /// <summary>World X coordinate.</summary>
        public readonly float X;
        /// <summary>World Z coordinate.</summary>
        public readonly float Z;
        /// <summary>POI kind tag (e.g. "enemy", "tower", "objective").</summary>
        public readonly string Kind;

        /// <summary>Constructs an immutable minimap-POI snapshot from all fields.</summary>
        public MinimapPoiRecord(float x, float z, string kind)
        {
            X = x;
            Z = z;
            Kind = kind;
        }
    }
}
