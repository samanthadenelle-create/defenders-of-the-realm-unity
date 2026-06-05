namespace DeNelle.Core
{
    /// <summary>
    /// Cross-assembly hook for forcing a specific dialogue speaker portrait by Resources
    /// path (e.g. "Portraits/forge"). The Village dialogue bridge sets <see cref="Forced"/>
    /// via the Yarn <c>&lt;&lt;portrait ...&gt;&gt;</c> command; DeNelle.DialogueUI's presenter
    /// reads it and shows that portrait regardless of the line's CharacterName. Lives in
    /// Core so neither side needs to reference the other (mirrors CoreServices). Cleared
    /// when the dialogue completes.
    /// </summary>
    public static class DialoguePortrait
    {
        /// <summary>Resources path of the portrait to force (null/empty = use per-line CharacterName).</summary>
        public static string Forced;
    }
}
