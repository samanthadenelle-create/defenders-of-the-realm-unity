// =============================================================================
// EnemyArtPaths — WO-1129 §3.1: THE ONE DERIVED ANSWER to "where does model M's
// basecolor live?". Sibling to AssetRoots; same owner ruling, one level down.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core
//
// AssetRoots answers WHERE THE TREE IS. This file answers WHERE INSIDE IT A MAP
// IS — which is the half that was still being re-invented at every call site.
//
// ── THE INCIDENT THAT BOUGHT THIS FILE (2026-08-20) ──────────────────────────
// A seat searched EnemyContent/ and TripoTex/ for Orc_Berserker's textures, found
// none, and told the owner "no texture anywhere in the project" — then recommended
// commissioning art. The owner found the answer in FIVE MINUTES by searching every
// filename containing the token "basecolor", which surfaced EnemyContent/OrcTex/ —
// a folder the seat did not know existed.
//
// Two lessons. The first is METHOD (survey by the common token, not by the name
// you already guessed) and lives in project memory. The SECOND is STRUCTURE, and
// it is this file: the only reason a token sweep was NECESSARY is that the same
// job was being done four different ways, because every seat that touched enemy
// art invented its own home for it.
//
// ── THE FOUR CONVENTIONS THIS FILE MAKES ONE QUESTION ────────────────────────
//   TripoTex/<Model>_basecolor.jpg      Troll, Troll_Mage, Troll_Overlord,
//                                       Necromancer, Skeleton_Golem, Orc_Mage,
//                                       Orc_Warlord, Orc_Tank, Orc_Warrior
//   OrcTex/<Model>_basecolor.jpg        Orc_Mage, Orc_Tank, Orc_Warrior
//   <Model>.fbm/Material_Pbr_Diffuse.*  Skeleton_Warrior/_Rogue/_Healer/_Mage,
//                                       Cellar_Hollow, Demon, Hollow_Walker, ...
//   loose image beside the FBX          the KayKit bodies (skeleton_texture_A.png)
//
// ⚠ Orc_Mage, Orc_Tank and Orc_Warrior appear in TWO of them with DIFFERENT
// textures. "Which one wins at runtime" was previously unanswerable without
// reading the resolver. It is now answerable by reading ONE line: AtlasFolders.
// TRIPOTEX WINS. The 2026-08-09 mesh replacement means the older OrcTex atlas no
// longer matches those UVs — bind OrcTex to the new mesh and the skin slides.
//
// ── THE ONE RULE, IN ONE SENTENCE A FUTURE SEAT CANNOT MISREAD ───────────────
//   An enemy colour map is <AssetRoots.EnemyContent>/<AtlasFolder>/<Model>_<map>,
//   with the model's own <Model>.fbm/ embedded art as the fallback, and the
//   candidate order in AtlasFolders is the precedence — first hit wins.
//
// ⛔ DO NOT TYPE "TripoTex", "OrcTex", ".fbm" OR "_basecolor" AT A CALL SITE.
// Ask this file. That is the whole ticket: a literal at a call site cannot be
// re-pointed, cannot be traced on a miss, and cannot be asserted by an oracle —
// it can only be found later, by hand, by the owner.
//
// ── WHY BOTH A RESOURCES FORM AND AN ASSET FORM ──────────────────────────────
// The runtime probe (EnemyFactory.TryBasecolor) loads through Resources with a
// folder-relative key and no extension; the editor oracle
// (EnemyArtCoverageRegression) stats real files and therefore needs the project
// path AND the extension. Same convention, two spellings — declared ONCE here so
// they cannot disagree. Before this file they were two independent copies whose
// agreement was asserted by a COMMENT ("EnemyFactory.ResolveBasecolor applies the
// identical rule at runtime"), which is exactly the duplicated-state failure
// CLAUDE.md catalogues in §2, §5 and §16.
//
// ⚠ NAMED, NOT GUESSED — the "_NEW" alias. Skeleton_Golem_NEW and Necromancer_NEW
// carry that suffix only because a LEGACY mesh of the same name already occupied
// the tree. "_NEW" DISAMBIGUATES A MESH FILE, NOT A CHARACTER — their authored
// maps ship under the BASE name. Without the alias the lookup misses and the model
// silently falls back to a solid tint. That is the defect that shipped the two
// bodies the owner had specifically asked for as white silhouettes, for days.
// The alias lives HERE so the runtime and the oracle cannot drift apart on it.
// =============================================================================

using System;
using System.Collections.Generic;

namespace DeNelle.Core
{
    /// <summary>Which map of a PBR set is wanted. The enum, not a typed suffix.</summary>
    public enum EnemyArtMap
    {
        /// <summary>Albedo / colour. The one the coverage oracle gates on.</summary>
        BaseColor = 0,
        Normal = 1,
        Roughness = 2,
    }

    /// <summary>
    /// The single derived answer to "where does model M's &lt;map&gt; live?".
    /// Every method returns CANDIDATES in precedence order and never touches the
    /// filesystem — resolution (Resources.Load / File.Exists) belongs to the
    /// caller, which is what keeps this usable from runtime AND editor code.
    /// </summary>
    public static class EnemyArtPaths
    {
        // ── The convention, declared once ────────────────────────────────────

        /// <summary>Atlas folders under the enemy content root, IN PRECEDENCE ORDER.
        /// <para>⚠ TripoTex is FIRST and that is load-bearing: Orc_Mage / Orc_Tank /
        /// Orc_Warrior exist in both, and the 2026-08-09 mesh replacement means the
        /// older OrcTex atlas no longer matches those UVs. Reordering this array
        /// re-skins three orcs wrongly and nothing will fail loudly.</para></summary>
        public static readonly string[] AtlasFolders = { "TripoTex", "OrcTex" };

        /// <summary>Sidecar folder suffix Unity gives a model's extracted embedded art.</summary>
        public const string EmbeddedFolderSuffix = ".fbm";

        /// <summary>Stem of the embedded diffuse Tripo exports write into a .fbm folder.</summary>
        public const string EmbeddedDiffuseStem = "Material_Pbr_Diffuse";

        /// <summary>The suffix that DISCOVERS art, per the 2026-08-20 token sweep.
        /// Almost every delivered map in this project carries it.</summary>
        public const string BaseColorSuffix = "_basecolor";

        /// <summary>Alternate colour-map token used by the raw Tripo/FBX exports
        /// (Material_Pbr_Diffuse, tripo_mat_*_Pbr_Diffuse). Surveying for BOTH is
        /// what makes a sweep discover rather than confirm.</summary>
        public const string DiffuseToken = "diffuse";

        /// <summary>Resources-relative folder the enemy tree is addressed under at
        /// runtime (Resources.Load("Enemies/...")).</summary>
        public const string ResourcesEnemyPrefix = "Enemies";

        /// <summary>Suffix that disambiguates a MESH FILE, never a character. Stripped
        /// when looking for authored maps. See the header.</summary>
        public const string MeshDisambiguatorSuffix = "_NEW";

        /// <summary>Image extensions a delivered map may carry, in the order the
        /// deliveries actually use them.</summary>
        public static readonly string[] ImageExtensions = { ".jpg", ".png", ".jpeg", ".tga" };

        // ── Map naming ───────────────────────────────────────────────────────

        /// <summary>Filename suffix for a map kind, e.g. BaseColor -&gt; "_basecolor".</summary>
        public static string SuffixFor(EnemyArtMap map)
        {
            switch (map)
            {
                case EnemyArtMap.Normal: return "_normal";
                case EnemyArtMap.Roughness: return "_roughness";
                case EnemyArtMap.BaseColor:
                default: return BaseColorSuffix;
            }
        }

        // ── Name aliasing — the ONE home of the "_NEW" rule ──────────────────

        /// <summary>
        /// The names a model's art may be filed under, in probe order: the model's
        /// own name first, then the name with <see cref="MeshDisambiguatorSuffix"/>
        /// stripped. NEVER throws; a null/empty model yields an empty list.
        /// </summary>
        public static IReadOnlyList<string> NameAliases(string model)
        {
            var list = new List<string>(2);
            if (string.IsNullOrEmpty(model)) return list;
            list.Add(model);
            if (model.EndsWith(MeshDisambiguatorSuffix, StringComparison.Ordinal))
            {
                string stripped = model.Substring(0, model.Length - MeshDisambiguatorSuffix.Length);
                if (stripped.Length > 0) list.Add(stripped);
            }
            return list;
        }

        // ── RUNTIME form: Resources keys, no extension ───────────────────────

        /// <summary>
        /// Resources keys to probe for a model's map, in precedence order
        /// (atlas folder x name alias). Extension-free, as Resources.Load requires.
        /// <para>Example: "Enemies/TripoTex/Orc_Mage_basecolor".</para>
        /// </summary>
        public static IReadOnlyList<string> ResourceCandidates(string model, EnemyArtMap map = EnemyArtMap.BaseColor)
        {
            var keys = new List<string>();
            var aliases = NameAliases(model);
            if (aliases.Count == 0) return keys;

            string suffix = SuffixFor(map);
            for (int f = 0; f < AtlasFolders.Length; f++)
                for (int a = 0; a < aliases.Count; a++)
                    keys.Add(ResourcesEnemyPrefix + "/" + AtlasFolders[f] + "/" + aliases[a] + suffix);
            return keys;
        }

        // ── EDITOR form: project asset paths, with extensions ────────────────

        /// <summary>
        /// Project-relative asset paths to stat for a model's atlas map, in
        /// precedence order (atlas folder x name alias x extension).
        /// <para>Root comes from <see cref="AssetRoots.EnemyContent"/> — ⛔ never
        /// re-type the literal.</para>
        /// <para>⚠ FINDING, 2026-08-21: AssetRoots.cs:46 claims "AssetRootsRegression
        /// fails the build if the string reappears". THERE IS NO SUCH SUITE — a
        /// repo-wide search finds that name only inside that comment and this one.
        /// Nothing was enforcing the rule, which is precisely why a re-typed
        /// "Assets/EnemyContent" was still sitting in EnemyArtCoverageRegression
        /// until WO-1129 removed it. Do not trust the claim; the gate is owed.</para>
        /// </summary>
        public static IReadOnlyList<string> AtlasAssetCandidates(string model, EnemyArtMap map = EnemyArtMap.BaseColor)
        {
            var paths = new List<string>();
            var aliases = NameAliases(model);
            if (aliases.Count == 0) return paths;

            string suffix = SuffixFor(map);
            for (int f = 0; f < AtlasFolders.Length; f++)
                for (int a = 0; a < aliases.Count; a++)
                    for (int e = 0; e < ImageExtensions.Length; e++)
                        paths.Add(AssetRoots.EnemyContent + "/" + AtlasFolders[f] + "/" +
                                  aliases[a] + suffix + ImageExtensions[e]);
            return paths;
        }

        /// <summary>
        /// The model's OWN extracted embedded-art folder, e.g.
        /// "Assets/EnemyContent/Skeleton_Warrior.fbm". Art here ships WITH the mesh,
        /// so its UVs cannot mismatch — which is why it outranks the shared atlases
        /// as evidence of a correct look, even though it still needs binding.
        /// </summary>
        public static string EmbeddedFolder(string model)
        {
            if (string.IsNullOrEmpty(model)) return null;
            return AssetRoots.EnemyContent + "/" + model + EmbeddedFolderSuffix;
        }

        /// <summary>The model's own FBX under the enemy content root.</summary>
        public static string FbxPath(string model)
        {
            if (string.IsNullOrEmpty(model)) return null;
            return AssetRoots.EnemyContent + "/" + model + ".fbx";
        }

        /// <summary>True when a filename stem reads as a colour map under EITHER
        /// convention (the "_basecolor" deliveries and the raw "*_Diffuse" exports).
        /// This is the token test the 2026-08-20 sweep proved is the discovering one.</summary>
        public static bool IsColorMapStem(string stem)
        {
            if (string.IsNullOrEmpty(stem)) return false;
            return stem.IndexOf(BaseColorSuffix, StringComparison.OrdinalIgnoreCase) >= 0
                || stem.IndexOf(DiffuseToken, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // ── Traced misses (WO-1129 §3.1: "a miss says WHICH candidates it tried") ──

        /// <summary>
        /// One line naming every candidate that was tried, for a FlowTrace on a miss.
        /// A miss that does not say what it looked for is what sent a seat to the
        /// owner with "no texture anywhere in the project".
        /// </summary>
        public static string DescribeCandidates(string model, EnemyArtMap map = EnemyArtMap.BaseColor)
        {
            var res = ResourceCandidates(model, map);
            string embedded = EmbeddedFolder(model);
            return "model='" + (model ?? "null") + "' map=" + map +
                   " tried resources[" + string.Join(", ", ToArray(res)) + "]" +
                   " then own embedded art in '" + (embedded ?? "n/a") + "/'";
        }

        private static string[] ToArray(IReadOnlyList<string> src)
        {
            var arr = new string[src.Count];
            for (int i = 0; i < src.Count; i++) arr[i] = src[i];
            return arr;
        }
    }
}
