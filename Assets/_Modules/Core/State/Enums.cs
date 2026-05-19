// =============================================================================
// Enums — the six persisted-state string/number-union enums
// -----------------------------------------------------------------------------
// Port of the TS string-union types (settingsSlice / tutorialSlice / playerSlice
// / gameDesign). Every member carries an [EnumMember] value so Newtonsoft's
// StringEnumConverter serializes to the EXACT lowercase/kebab string the React
// save uses — data/*.json and imported saves are cross-engine, so the wire
// strings must match byte-for-byte (spec §1.5).
//
// TutorialStep is the awkward one: a `1..7 | 'done'` union. It serializes via a
// custom converter (TutorialStepConverter) — numbers 1..7 raw, Done as "done".
// HeroClass is a real enum; the SO uses the inspector-friendly HeroClassOpt
// nullable wrapper (improvement #5 NOT adopted) — see HeroClassOpt.cs.
// =============================================================================

using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace DeNelle.Core.State
{
    /// <summary>Gameplay difficulty preference (settingsSlice).</summary>
    public enum Difficulty
    {
        [EnumMember(Value = "easy")] Easy,
        [EnumMember(Value = "normal")] Normal,
        [EnumMember(Value = "hard")] Hard,
    }

    /// <summary>Hero movement control style (docs/touch-movement-spec.md §2.2).</summary>
    public enum MovementStyle
    {
        [EnumMember(Value = "auto")] Auto,
        [EnumMember(Value = "joystick")] Joystick,
        [EnumMember(Value = "tap")] Tap,
        [EnumMember(Value = "both")] Both,
    }

    /// <summary>Which breach battle system fires when an enemy crosses the ring.</summary>
    public enum BreachStyle
    {
        [EnumMember(Value = "ask")] Ask,
        [EnumMember(Value = "atb")] Atb,
        [EnumMember(Value = "tower-sim")] TowerSim,
    }

    /// <summary>Player's chosen hero class (playerSlice / content/story).</summary>
    public enum HeroClass
    {
        [EnumMember(Value = "mage")] Mage,
        [EnumMember(Value = "knight")] Knight,
        [EnumMember(Value = "ranger")] Ranger,
    }

    /// <summary>Pet species (gameDesign.ts).</summary>
    public enum PetSpecies
    {
        [EnumMember(Value = "aether-sprite")] AetherSprite,
        [EnumMember(Value = "flame-pup")] FlamePup,
        [EnumMember(Value = "ice-wolf")] IceWolf,
    }

    /// <summary>
    /// First-time tutorial progression — TS union <c>1..7 | 'done'</c>. Steps
    /// serialize as raw numbers 1..7; <see cref="Done"/> serializes as the
    /// literal string "done" (see <c>TutorialStepConverter</c>).
    /// </summary>
    [JsonConverter(typeof(TutorialStepConverter))]
    public enum TutorialStep
    {
        Step1 = 1,
        Step2 = 2,
        Step3 = 3,
        Step4 = 4,
        Step5 = 5,
        Step6 = 6,
        Step7 = 7,
        /// <summary>Tutorial finished. Wire value: the string "done".</summary>
        Done = 99,
    }
}
