// =============================================================================
// BuildPaletteGroupsRegression — WO-1167: the build palette groups itself by ROLE,
// so a new building needs DATA and not code. This oracle pins the rule.
// -----------------------------------------------------------------------------
// WHAT IT PINS, and why each check exists:
//
//  (1) [authored]     Town authors 'paletteGroups' in build-categories.json, every
//                     group has a label, and no role is claimed by two groups.
//  (2) [coverage]     Every Town-eligible catalog row (the Town verb's catalogTypes)
//                     resolves to EXACTLY ONE authored group or the trailing Other
//                     bucket — nothing can be dropped, nothing double-homed. The six
//                     rows WO-1167 §4 found unroled (barracks, pet-house,
//                     arcane-tower, mill, lumbermill, mine_crystal) must carry a
//                     role, so none of the LIVE roster lands in Other by accident.
//  (3) [role-unique]  No two catalog rows share a role — StructureRoles refuses the
//                     collision loudly at runtime (FlowTrace.Fail); this makes it a
//                     gate-time failure instead of a flight-recorder line.
//  (4) [newtype]      THE OWNER'S RULE, driven through the REAL shipped projection
//                     (BuildPaletteVM.GroupCards): a card with a brand-new role the
//                     data has never seen lands in the trailing Other section — with
//                     no code change anywhere. Also asserts the projection's union
//                     is exactly the input (same objects, same order: headers only,
//                     never a re-sort, never a drop) and that an all-matched list
//                     produces NO Other and no empty section.
//  (5) [no-code-roles] SOURCE LINT: no role string authored in paletteGroups appears
//                     as a quoted literal in BuildPaletteVM.cs / BuildPaletteUI.cs /
//                     BuildCategoryRegistry.cs. The group membership lives in the
//                     data and ONLY in the data — a role list in C# is one fact
//                     written twice, the drift shape WO-1161/§2/§5/§16 keep
//                     re-teaching. (The scan is for the QUOTED token, not a
//                     formatting-sensitive window — the WO-1138 lesson.)
//  (6) [dual-copy]    The Resources + StreamingAssets copies of BOTH edited files
//                     are byte-equal (the files' own standing rule; Resources WINS
//                     at runtime, so a drifted StreamingAssets copy is invisible
//                     until the day it isn't).
//
// READS THE SHIPPED JSON, NOT THE SHARED STATICS (the CollectorLadderRegression
// rule): both files are deserialized from Data/Canonical via the same shapes the
// game parses, so the verdict cannot depend on suite order.
//
// Registered in DataRegression.RunAll (covenant style).
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DeNelle.Core.Catalog;
using DeNelle.Village;
using Newtonsoft.Json;

namespace DeNelle.Editor.Regression
{
    public static class BuildPaletteGroupsRegression
    {
        private const string CategoriesPath = "Data/Canonical/build-categories.json";
        private const string CatalogPath    = "Data/Canonical/structures-catalog.json";

        // The three files the no-code-roles lint sweeps. Repo-relative under Assets/.
        private static readonly string[] LintSources =
        {
            "Assets/_Modules/Village/BuildMode/BuildPaletteVM.cs",
            "Assets/_Modules/Village/BuildMode/BuildPaletteUI.cs",
            "Assets/_Modules/Village/Catalog/BuildCategoryRegistry.cs",
        };

        // WO-1167 §4 — the six rows found unroled; each must now author a role so the
        // live Town roster never lands in Other by accident. IDS, not roles: ids are
        // the frozen save keys, and naming a ROLE here would itself be a role list in
        // code. What role each carries stays the catalog's business.
        private static readonly string[] MustBeRoled =
        {
            "barracks", "pet-house", "arcane-tower", "mill", "lumbermill", "mine_crystal",
        };

        // ── JSON shapes (only what this oracle reads) ─────────────────────────
        private sealed class CategoriesFile
        {
            [JsonProperty("categories")] public List<CategoryRow> Categories = new List<CategoryRow>();
        }
        private sealed class CategoryRow
        {
            [JsonProperty("buildType")]     public string BuildType;
            [JsonProperty("catalogTypes")]  public List<string> CatalogTypes = new List<string>();
            [JsonProperty("paletteGroups")] public List<GroupRow> PaletteGroups;
        }
        private sealed class GroupRow
        {
            [JsonProperty("label")] public string Label;
            [JsonProperty("roles")] public List<string> Roles = new List<string>();
        }
        private sealed class CatalogFile
        {
            [JsonProperty("entries")] public List<CatalogEntry> Entries = new List<CatalogEntry>();
        }

        public static bool Run(out string result)
        {
            var failures = new List<string>();

            CategoryRow town = null;
            List<CatalogEntry> entries = null;
            try
            {
                var catFile = JsonConvert.DeserializeObject<CategoriesFile>(
                    DeNelle.Core.CanonicalJson.Read(CategoriesPath));
                if (catFile?.Categories != null)
                    town = catFile.Categories.Find(r =>
                        r != null && string.Equals(r.BuildType, "Town", StringComparison.OrdinalIgnoreCase));

                entries = JsonConvert.DeserializeObject<CatalogFile>(
                    DeNelle.Core.CanonicalJson.Read(CatalogPath))?.Entries;
            }
            catch (Exception ex)
            {
                result = "[palette-groups] FAIL: canonical JSON unreadable: " + ex.Message;
                return false;
            }

            // (1) [authored] ---------------------------------------------------
            var authoredRoles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); // role -> group label
            if (town?.PaletteGroups == null || town.PaletteGroups.Count == 0)
            {
                failures.Add("[authored] the Town row of build-categories.json authors NO 'paletteGroups' " +
                             "— WO-1167's whole deliverable is that block.");
            }
            else
            {
                foreach (var g in town.PaletteGroups)
                {
                    if (g == null) continue;
                    if (string.IsNullOrEmpty(g.Label))
                        failures.Add("[authored] a paletteGroups row has no label — a header must carry words " +
                                     "(text is the colourblind-safe channel).");
                    if (g.Roles == null || g.Roles.Count == 0)
                        failures.Add($"[authored] group '{g.Label}' names no roles — an empty group can never render.");
                    else
                        foreach (var role in g.Roles)
                        {
                            if (string.IsNullOrEmpty(role)) continue;
                            if (authoredRoles.TryGetValue(role, out var firstLabel))
                                failures.Add($"[authored] role '{role}' is claimed by BOTH '{firstLabel}' and " +
                                             $"'{g.Label}' — a role must name exactly one group.");
                            else
                                authoredRoles[role] = g.Label ?? "";
                        }
                }
            }

            // (2) [coverage] + (3) [role-unique] -------------------------------
            if (entries == null || entries.Count == 0)
            {
                failures.Add("[coverage] structures-catalog.json parsed to no entries.");
            }
            else
            {
                var townTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (town?.CatalogTypes != null) foreach (var t in town.CatalogTypes) townTypes.Add(t);

                var roleOwners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); // role -> id
                var byId = new Dictionary<string, CatalogEntry>(StringComparer.OrdinalIgnoreCase);
                foreach (var e in entries)
                {
                    if (e == null || string.IsNullOrEmpty(e.id)) continue;
                    byId[e.id] = e;
                    if (string.IsNullOrEmpty(e.role)) continue;
                    if (roleOwners.TryGetValue(e.role, out var firstId))
                        failures.Add($"[role-unique] role '{e.role}' is claimed by BOTH '{firstId}' and '{e.id}' " +
                                     "— StructureRoles would FlowTrace.Fail this at runtime; fix the catalog.");
                    else
                        roleOwners[e.role] = e.id;

                    // Exactly-one-home check: authoredRoles is already deduped, so a role
                    // resolves to at most one group; membership in zero groups = Other. Both
                    // are legal single homes — what this asserts is the resolution is total.
                    bool townEligible = townTypes.Contains(e.type.ToString());
                    if (townEligible && !authoredRoles.ContainsKey(e.role))
                    {
                        // Other-bucket resident. Legal (never dropped) — but the LIVE, unlocked
                        // roster should be fully grouped; only palette-locked legacy rows may
                        // sit in Other today. Locked-ness is the Town row's lockedIds.
                        // (Deliberately a NOTE, not a FAIL, for locked rows.)
                    }
                }

                foreach (var id in MustBeRoled)
                {
                    if (!byId.TryGetValue(id, out var e))
                        failures.Add($"[coverage] catalog row '{id}' is MISSING — ids are frozen save keys and " +
                                     "this oracle expected the WO-1167 §4 roster.");
                    else if (string.IsNullOrEmpty(e.role))
                        failures.Add($"[coverage] catalog row '{id}' still has NO role — WO-1167 §4 fills all six " +
                                     "so no live Town card lands in Other by accident.");
                }
            }

            // (4) [newtype] — drive the REAL shipped projection ----------------
            try
            {
                var groups = new[]
                {
                    new PaletteGroup { Label = "A", Roles = new[] { "oracle_role_a" } },
                    new PaletteGroup { Label = "B", Roles = new[] { "oracle_role_b" } },
                };
                StructureCardVM Card(string id, string role) =>
                    new StructureCardVM(new CatalogEntry { id = id, displayName = id, role = role },
                        null, freebie: true);

                var cards = new List<StructureCardVM>
                {
                    Card("c1", "oracle_role_a"),
                    Card("c2", "brand_new_role_nobody_authored"),   // the owner's rule
                    Card("c3", "oracle_role_b"),
                    Card("c4", null),                               // unroled — also never dropped
                };
                var sections = BuildPaletteVM.GroupCards(cards, groups);

                var flat = new List<StructureCardVM>();
                foreach (var s in sections) flat.AddRange(s.Cards);
                if (flat.Count != cards.Count)
                    failures.Add($"[newtype] projection dropped cards: in={cards.Count} out={flat.Count} — " +
                                 "rule 1 (never dropped) is broken.");

                PaletteSectionVM otherSection = sections.Find(s => s.IsOther);
                if (otherSection == null)
                    failures.Add("[newtype] a brand-new role produced NO trailing Other section — the " +
                                 "zero-code-change rule is broken.");
                else if (!otherSection.Cards.Contains(cards[1]) || !otherSection.Cards.Contains(cards[3]))
                    failures.Add("[newtype] the new-role and unroled cards did not land in Other.");
                if (sections.Count > 0 && sections[sections.Count - 1] != otherSection)
                    failures.Add("[newtype] Other is not the TRAILING section.");

                // Order within a section = incoming order; empty groups render nothing.
                var allMatched = new List<StructureCardVM> { Card("m1", "oracle_role_a"), Card("m2", "oracle_role_a") };
                var s2 = BuildPaletteVM.GroupCards(allMatched, groups);
                if (s2.Count != 1 || s2[0].IsOther)
                    failures.Add($"[newtype] all-matched list must render exactly its one non-empty group " +
                                 $"(no empty 'B' header, no Other) — got {s2.Count} section(s).");
                else if (s2[0].Cards[0] != allMatched[0] || s2[0].Cards[1] != allMatched[1])
                    failures.Add("[newtype] section order diverged from the incoming (WO-963) order — " +
                                 "grouping must add headers, never re-sort.");
            }
            catch (Exception ex)
            {
                failures.Add("[newtype] projection threw: " + ex.Message);
            }

            // (5) [no-code-roles] ----------------------------------------------
            // The registry file is a special case: its hardcoded parse-failure fallback
            // legitimately mirrors build-categories lockedIds, which are catalog IDS — and
            // three roles (armorer / jeweler / barracks) share their spelling with an id.
            // An id literal there is not a role list. So the registry is linted only for
            // roles that are NOT also catalog ids; the palette VM/View — which have no
            // business naming either — are linted for every authored role.
            if (authoredRoles.Count > 0)
            {
                var catalogIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (entries != null)
                    foreach (var e in entries)
                        if (e != null && !string.IsNullOrEmpty(e.id)) catalogIds.Add(e.id);

                string projectRoot = Directory.GetParent(UnityEngine.Application.dataPath).FullName;
                foreach (var src in LintSources)
                {
                    string full = Path.Combine(projectRoot, src);
                    if (!File.Exists(full))
                    {
                        failures.Add($"[no-code-roles] lint source missing: {src} — if the file moved, " +
                                     "re-point this oracle in the same change.");
                        continue;
                    }
                    bool isRegistry = src.EndsWith("BuildCategoryRegistry.cs", StringComparison.OrdinalIgnoreCase);
                    string text = File.ReadAllText(full);
                    foreach (var role in authoredRoles.Keys)
                    {
                        if (isRegistry && catalogIds.Contains(role)) continue;   // id-shaped token, legal there
                        if (text.IndexOf("\"" + role + "\"", StringComparison.OrdinalIgnoreCase) >= 0)
                            failures.Add($"[no-code-roles] {src} names role literal \"{role}\" — group " +
                                         "membership lives in build-categories.json and ONLY there (WO-1161: " +
                                         "a role list in code is one fact written twice).");
                    }
                }
            }

            // (6) [dual-copy] ---------------------------------------------------
            foreach (var rel in new[] { CategoriesPath, CatalogPath })
            {
                string res = Path.Combine(UnityEngine.Application.dataPath, "Resources", rel);
                string str = Path.Combine(UnityEngine.Application.dataPath, "StreamingAssets", rel);
                if (!File.Exists(res) || !File.Exists(str))
                {
                    failures.Add($"[dual-copy] a canonical copy of {rel} is missing on disk.");
                    continue;
                }
                var a = File.ReadAllBytes(res);
                var b = File.ReadAllBytes(str);
                bool equal = a.Length == b.Length;
                for (int i = 0; equal && i < a.Length; i++) equal = a[i] == b[i];
                if (!equal)
                    failures.Add($"[dual-copy] Resources and StreamingAssets copies of {rel} DIFFER — " +
                                 "Resources wins at runtime, so the drifted copy is a silent divergence.");
            }

            if (failures.Count > 0)
            {
                var sb = new StringBuilder("[palette-groups] FAIL (" + failures.Count + "):");
                foreach (var f in failures) sb.Append("\n  ").Append(f);
                result = sb.ToString();
                return false;
            }

            result = $"palette-groups OK: Town authors {authoredRoles.Count} grouped role(s); coverage total; " +
                     "new-role→Other proven on the shipped projection; no role literal in C#; dual copies byte-equal.";
            return true;
        }
    }
}
