namespace DeNelle.Core.UI.Mvvm
{
    /// <summary>
    /// Binding contract for a fill bar (HP / MP / cast / progress). View-agnostic value type: the fill
    /// is normalized 0..1 and the color is a ROLE string the View resolves — no UnityEngine.Color here
    /// (UI_MVVM_BINDING_MAP.md §3).
    /// </summary>
    public readonly struct BarVM
    {
        /// <summary>Normalized fill, 0..1.</summary>
        public readonly float Fill01;
        /// <summary>Optional overlay label (e.g. "120 / 200").</summary>
        public readonly string Label;
        /// <summary>Color role the View maps to a concrete fill color (e.g. "hp", "mp", "cast").</summary>
        public readonly string ColorRole;

        public BarVM(float fill01, string label = null, string colorRole = null)
        {
            Fill01 = fill01 < 0f ? 0f : (fill01 > 1f ? 1f : fill01);
            Label = label;
            ColorRole = colorRole;
        }
    }
}
