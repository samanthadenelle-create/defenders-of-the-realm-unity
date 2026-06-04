// =============================================================================
// HeroClassOpt — inspector-friendly nullable-HeroClass wrapper
// -----------------------------------------------------------------------------
// `heroClass` in the React save is `'mage' | 'knight' | 'ranger' | null`. The
// cleanest C# would be `HeroClass?`, but Unity's inspector cannot serialize a
// nullable enum on a ScriptableObject. Per the task brief, improvement #5 is
// NOT adopted — the SO keeps this wrapper: a plain enum with a `None` sentinel
// that the editor CAN show, plus conversions to/from the real `HeroClass?`.
//
// The SAVE FILE never sees HeroClassOpt — PersistedState carries a genuine
// `HeroClass?` (None ↔ JSON null). This type only lives on the live SO.
// =============================================================================

namespace DeNelle.Core.State
{
    /// <summary>
    /// Inspector-serializable stand-in for <c>HeroClass?</c>. <see cref="None"/>
    /// is the "no class chosen yet" sentinel (the React <c>null</c>).
    /// </summary>
    public enum HeroClassOpt
    {
        /// <summary>No hero class chosen — the React <c>null</c>.</summary>
        None = 0,
        Mage = 1,
        Knight = 2,
        Ranger = 3,
        Cleric = 4,
    }

    /// <summary>Conversions between <see cref="HeroClassOpt"/> and <c>HeroClass?</c>.</summary>
    public static class HeroClassOptExtensions
    {
        /// <summary>Project the inspector wrapper onto a genuine nullable enum.</summary>
        public static HeroClass? ToNullable(this HeroClassOpt opt)
        {
            switch (opt)
            {
                case HeroClassOpt.Mage: return HeroClass.Mage;
                case HeroClassOpt.Knight: return HeroClass.Knight;
                case HeroClassOpt.Ranger: return HeroClass.Ranger;
                case HeroClassOpt.Cleric: return HeroClass.Cleric;
                default: return null;
            }
        }

        /// <summary>Project a genuine nullable enum onto the inspector wrapper.</summary>
        public static HeroClassOpt ToOpt(this HeroClass? cls)
        {
            if (cls == null) return HeroClassOpt.None;
            switch (cls.Value)
            {
                case HeroClass.Mage: return HeroClassOpt.Mage;
                case HeroClass.Knight: return HeroClassOpt.Knight;
                case HeroClass.Ranger: return HeroClassOpt.Ranger;
                case HeroClass.Cleric: return HeroClassOpt.Cleric;
                default: return HeroClassOpt.None;
            }
        }
    }
}
