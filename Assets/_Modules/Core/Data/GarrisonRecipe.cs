// =============================================================================
// GarrisonRecipe — the DATA that drives the recipe-first garrison/dungeon system.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.World
//
// THE VISION (owner): "virtually no code, just JSON params." Adding a new fort /
// cave / region = ONE JSON line in garrison-recipes.json, ZERO code. The generic
// GarrisonSceneBuilder reads each recipe and realizes a whole Garrison_<id>.unity:
// ground + moody lighting + a prop layout + spawn points + a GarrisonController
// wired to the recipe's enemy types and level band. No per-theme hardcoded methods.
//
// A recipe is a flat, WebGL-safe, JsonConvert-deserializable record. It lives in
// DeNelle.Core so BOTH the Editor builder (DeNelle.Editor references DeNelle.Core)
// AND the runtime GarrisonController (DeNelle.Village references DeNelle.Core) read
// the SAME type — one schema, no reflection on the recipe itself.
//
// Canon: the village is Elarion (never Avalon). ASCII-only strings.
// =============================================================================

using System.Collections.Generic;
using Newtonsoft.Json;

namespace DeNelle.Core.World
{
    /// <summary>
    /// One garrison/dungeon scene, expressed purely as data. The generic builder
    /// turns this into Garrison_&lt;Id&gt;.unity; the runtime GarrisonController reads
    /// Enemies + LevelRange to spawn a level-banded squad. Every field has a safe
    /// default so a sparse JSON line ("just an id + a couple params") still builds.
    /// </summary>
    public sealed class GarrisonRecipe
    {
        /// <summary>Stable id. Drives the scene name (Garrison_&lt;Id&gt;.unity) + nav asset path.</summary>
        [JsonProperty("id")] public string Id = "garrison";

        /// <summary>"garrison" (open-air fort) or "dungeon" (enclosed cave/keep). Drives the
        /// prop palette role-set + whether a roof/enclosure pass runs. Absent => "garrison".</summary>
        [JsonProperty("kind")] public string Kind = "garrison";

        /// <summary>Footprint scale: "small" | "medium" | "large". Scales the palisade/floor
        /// half-extents + spawn count. Absent => "medium".</summary>
        [JsonProperty("size")] public string Size = "medium";

        /// <summary>Theme/lighting token: "troll" | "ruined" | "hill" | "frost" | "ember" ...
        /// Drives the moody-lighting tint + ground colour. Unknown themes fall to a neutral
        /// dusk so a new theme name never breaks the bake. Absent => "troll".</summary>
        [JsonProperty("theme")] public string Theme = "troll";

        /// <summary>Alias for <see cref="Theme"/> — if "lighting" is set it wins. Lets a recipe
        /// author say lighting separately from a theme label if they want. Optional.</summary>
        [JsonProperty("lighting")] public string Lighting;

        /// <summary>Enemy ids that staff this garrison (EnemyFactory model-map keys, e.g.
        /// "troll", "orc-berserker", "hollow-walker"). The controller round-robins these
        /// across the spawn points. Absent/empty => ["troll"].</summary>
        [JsonProperty("enemies")] public List<string> Enemies = new List<string>();

        /// <summary>[minLevel, maxLevel] band. Each spawned defender rolls a level in this
        /// inclusive band; stats scale with the level (see GarrisonController). Absent =>
        /// [1, 1] (level-1 defenders, unscaled). A single value or malformed => clamped.</summary>
        [JsonProperty("levelRange")] public List<int> LevelRange = new List<int>();

        /// <summary>Difficulty/threat tier (0..3+). Folds into the level-scale as a flat bump
        /// so an author can make a same-level fort tougher. Absent => 2.</summary>
        [JsonProperty("threat")] public int Threat = 2;

        /// <summary>Prop ROLES to place (resolved to real catalog prefabs by the builder's
        /// prop-role map, e.g. "wall_wood", "watchtower", "campfire", "stalagmite",
        /// "ice_floe"). Absent => a sensible default set for the kind/theme.</summary>
        [JsonProperty("props")] public List<string> Props = new List<string>();

        /// <summary>Elemental flavour for a dungeon ("fire"|"water"|"ice"|null). Currently a
        /// tint/lighting + prop-accent hint; reserved for future elemental hazards. Optional.</summary>
        [JsonProperty("element")] public string Element;

        // ---- convenience accessors (null/empty-safe; used by builder + controller) ----

        /// <summary>The lighting token that actually drives tint: Lighting if set, else Theme.</summary>
        [JsonIgnore] public string LightingToken =>
            !string.IsNullOrEmpty(Lighting) ? Lighting : Theme;

        /// <summary>True when this recipe is an enclosed dungeon (cave/keep) rather than a fort.</summary>
        [JsonIgnore] public bool IsDungeon =>
            !string.IsNullOrEmpty(Kind) && Kind.ToLowerInvariant() == "dungeon";

        /// <summary>Min level (inclusive), clamped to >= 1. Defaults to 1 if the band is absent.</summary>
        [JsonIgnore] public int MinLevel
        {
            get
            {
                if (LevelRange != null && LevelRange.Count >= 1)
                    return System.Math.Max(1, LevelRange[0]);
                return 1;
            }
        }

        /// <summary>Max level (inclusive), clamped to >= MinLevel.</summary>
        [JsonIgnore] public int MaxLevel
        {
            get
            {
                int min = MinLevel;
                if (LevelRange != null && LevelRange.Count >= 2)
                    return System.Math.Max(min, LevelRange[1]);
                return min;
            }
        }

        /// <summary>Non-empty enemy id list (defaults to a single troll if the author left it blank).</summary>
        [JsonIgnore] public IReadOnlyList<string> EnemyIds =>
            (Enemies != null && Enemies.Count > 0) ? Enemies : DefaultEnemies;

        private static readonly List<string> DefaultEnemies = new List<string> { "troll" };

        /// <summary>The scene name this recipe builds to (Garrison_&lt;Id&gt;).</summary>
        [JsonIgnore] public string SceneName => "Garrison_" + (string.IsNullOrEmpty(Id) ? "garrison" : Id);
    }

    /// <summary>Top-level JSON shape: { "recipes": [ ... ] }.</summary>
    public sealed class GarrisonRecipeFile
    {
        [JsonProperty("recipes")] public List<GarrisonRecipe> Recipes = new List<GarrisonRecipe>();
    }
}
