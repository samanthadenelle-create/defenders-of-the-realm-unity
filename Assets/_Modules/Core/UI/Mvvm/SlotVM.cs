namespace DeNelle.Core.UI.Mvvm
{
    /// <summary>
    /// Binding contract for an equipment / paperdoll / socket slot: a named slot that may hold an
    /// <see cref="ItemVM"/>. View-agnostic value type (UI_MVVM_BINDING_MAP.md §2/§3).
    /// </summary>
    public readonly struct SlotVM
    {
        /// <summary>Slot key (e.g. "head", "mainhand", "gem0") — identifies the slot to commands.</summary>
        public readonly string SlotKey;
        /// <summary>The occupying item, or null when empty.</summary>
        public readonly ItemVM? Content;
        /// <summary>Whether the View should highlight this slot (valid drop target / selected).</summary>
        public readonly bool Highlighted;

        public SlotVM(string slotKey, ItemVM? content = null, bool highlighted = false)
        {
            SlotKey = slotKey;
            Content = content;
            Highlighted = highlighted;
        }
    }
}
