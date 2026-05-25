// =============================================================================
// DungeonDef — WO-19: data-driven definition for one village dungeon entrance.
// One shared entrance prefab + one DungeonDef per dungeon gives per-dungeon
// identity (name / accent colour / banner / target scene) at ~1/8th the wiring
// cost of distinct models (owner-chosen hybrid option `a`).
// =============================================================================

using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// Per-dungeon identity for a <see cref="DungeonEntrance"/>: which scene it
    /// loads, its display name, accent emissive colour and optional banner.
    /// Authored as ScriptableObject assets under Resources/Dungeons/ so the
    /// runtime placement bootstrap can <c>Resources.LoadAll</c> them.
    /// </summary>
    [CreateAssetMenu(menuName = "Defenders/Dungeon Def", fileName = "DungeonDef")]
    public sealed class DungeonDef : ScriptableObject
    {
        [Tooltip("Stable id, e.g. \"HealersCottage\" (the part after the Dungeon_ scene prefix).")]
        public string DungeonId;

        [Tooltip("Localization key for the display name (falls back to DisplayName).")]
        public string NameKey;

        [Tooltip("Plain display name shown until NameKey is localized.")]
        public string DisplayName;

        [Tooltip("Full scene name, e.g. \"Dungeon_HealersCottage\" — passed to SceneRouter.LoadScene.")]
        public string SceneName;

        [Tooltip("Banner sprite shown above the entrance (optional; placeholder authoring may leave null).")]
        public Sprite Banner;

        [ColorUsage(true, true)]
        [Tooltip("Accent / emissive colour tinting the entrance — the per-dungeon identity cue.")]
        public Color AccentColor = Color.white;

        [TextArea]
        [Tooltip("One-line preview shown to the player on approach.")]
        public string ShortDescription;

        [Tooltip("False for scaffold stubs whose Dungeon_*.unity scene does not exist yet.")]
        public bool SceneExists = true;

        /// <summary>Display name fallback chain: DisplayName → DungeonId → SceneName.</summary>
        public string ResolveName()
        {
            if (!string.IsNullOrEmpty(DisplayName)) return DisplayName;
            if (!string.IsNullOrEmpty(DungeonId)) return DungeonId;
            return SceneName;
        }
    }
}
