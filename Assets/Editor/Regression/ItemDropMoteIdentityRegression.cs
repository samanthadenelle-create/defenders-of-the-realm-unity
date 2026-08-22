// =============================================================================
// ItemDropMoteIdentityRegression [drop-mote]
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Core + DeNelle.Village).
//
// PINS THE DEFECT (found in scope-boundary by the WO-1132 chest agent, fixed
// 2026-08-21): ItemPickupSpawner spawned ONE hardcoded gold sphere for EVERY
// drop. Two chests rolling completely different loot left byte-identical motes
// on the floor - the drop had NO identity, so the only way to learn what fell
// was to walk over it.
//
// THE COLOURBLIND LAW THIS SUITE ENFORCES (binding; the owner is red/green
// colourblind): identity must NEVER rest on hue. The sibling defect on
// IngredientPickup proved the same day that hue could not have carried it
// anyway - the authored tints parsed perfectly, they were just PASTELS on a
// non-emissive URP/Lit sphere, so every mote washed to the same white pellet
// under light. So the mote's identity rides SHAPE, and this suite compares
// COLOUR-FREE silhouette signatures: what survives a greyscale pass.
//
// Cases:
//   1 [glyph-read]     Every dropped id that HAS a catalog row resolves a glyph,
//                      and the resolver honours the AUTHORED glyph verbatim for
//                      every glyph the shape table draws. This is the "read what
//                      is already authored" half - materials.json/consumables.json
//                      have always carried `glyph` and nothing read it for the
//                      world mote. A regression here means we went back to
//                      inventing identity instead of reading it.
//   2 [shape-distinct] Colour stripped, the drop set spreads over many silhouette
//                      families, and no two dropped ids with DIFFERENT authored
//                      glyphs collapse onto the SAME signature. (Ids that SHARE a
//                      glyph sharing a shape is correct and intended - they are
//                      one family; the tint separates within it.)
//   3 [unauthored-ok]  The four loot-tables ids no catalog owns - monster-hide,
//                      wild-herb, tattered-cloth, rare-essence (a PO content gap
//                      the [item-identity] oracle reports) - each resolve a REAL,
//                      DISTINCT family deterministically, never a crash and never
//                      the same silent sphere. Their names still come from
//                      ItemIdentity (which returns the raw id), so the mote is
//                      named, not anonymous. Missing rows are a NOTE, not a fail:
//                      authoring content is the PO's call.
//   4 [greyscale]      The three KIND tints (consumable / material / unauthored)
//                      separate by >= 0.10 Rec.709 luma from each other, so even
//                      the secondary cue survives a greyscale pass - and the tint
//                      is proven to be a KIND cue only (3 buckets), never a
//                      per-item identity.
//   5 [spawner-code]   Source lint on ItemPickupMarker.cs: the deleted-term risks.
//                      The spawner must resolve its shape from ItemMoteShapes,
//                      must DESTROY the primitive colliders (pickup is a DISTANCE
//                      CHECK - a live collider blocks the hero / punches the
//                      NavMesh), must build a URP shader (CreatePrimitive ships
//                      Standard, which URP renders MAGENTA - the "pink floor"
//                      lesson), must be EMISSIVE (a lit-only mote is invisible at
//                      low lantern oil), and must NOT have gone back to a single
//                      hardcoded sphere.
//
// Markers: DROP_MOTE_OK / DROP_MOTE_FAIL.
// Standalone: run-unity-method DeNelle.Editor.Regression.ItemDropMoteIdentityRegression.RunAll
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using DeNelle.Village.Items;

namespace DeNelle.Editor.Regression
{
    public static class ItemDropMoteIdentityRegression
    {
        private const string SpawnerSrc = "Assets/_Modules/Village/Items/ItemPickupMarker.cs";
        private const string ShapesSrc = "Assets/_Modules/Village/Items/ItemMoteShapes.cs";

        /// <summary>Minimum Rec.709 luma separation for a cue to survive a GREYSCALE pass.
        /// This repo's own bar, set by TalentFocusSingletonRegression.</summary>
        private const float GreyscaleBar = 0.10f;

        /// <summary>Floor on how many distinct silhouettes the live drop set must spread over.
        /// Deliberately well under the family count - the point is "many", not "all".</summary>
        private const int MinDistinctFamilies = 8;

        /// <summary>The loot-tables ids that no catalog owns (PO content gap, reported by
        /// [item-identity]). They must still each get their OWN silhouette.</summary>
        private static readonly string[] KnownUnauthored =
            { "monster-hide", "wild-herb", "tattered-cloth", "rare-essence" };

        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("DROP_MOTE_OK - " + reason);
            else Debug.LogError("DROP_MOTE_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            try
            {
                MaterialCatalog.Reload();
                ConsumableCatalog.Reload();
                LootTableCatalog.Reload();

                var dropIds = CollectDropIds();
                if (dropIds.Count == 0)
                    failures.Add("[suite] loot-tables.json yielded NO drop ids - the suite would " +
                                 "pass vacuously, which is worse than failing");

                Case(failures, "glyph-read", () => Case1_GlyphRead(dropIds, failures, notes));
                Case(failures, "shape-distinct", () => Case2_ShapeDistinct(dropIds, failures, notes));
                Case(failures, "unauthored-ok", () => Case3_UnauthoredOk(failures, notes));
                Case(failures, "greyscale", () => Case4_Greyscale(failures, notes));
                Case(failures, "spawner-code", () => Case5_SpawnerCode(failures, notes));
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }

            string noteStr = notes.Count > 0 ? " [notes: " + string.Join("; ", notes.ToArray()) + "]" : "";
            if (failures.Count == 0)
            {
                reason = "DROP MOTE IDENTITY OK - every dropped id draws its AUTHORED glyph as a " +
                         "silhouette, two different items stay apart with all hue stripped, the " +
                         "unauthored ids each get their own shape, and the kind tints clear the " +
                         GreyscaleBar.ToString("F2") + " greyscale bar" + noteStr;
                return true;
            }
            reason = "drop-mote FAIL x" + failures.Count + ": " +
                     string.Join(" | ", failures.ToArray()) + noteStr;
            return false;
        }

        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add("[" + name + "] THREW " + ex.GetType().Name + ": " + ex.Message); }
        }

        // =====================================================================
        //  CASE 1 - the shape is READ from authored data, not invented
        // =====================================================================
        private static void Case1_GlyphRead(List<string> dropIds, List<string> failures, List<string> notes)
        {
            int authored = 0, honoured = 0;
            foreach (string id in dropIds)
            {
                var row = ItemIdentity.Resolve(id);
                if (!row.IsKnown) continue;
                authored++;

                if (string.IsNullOrEmpty(row.Glyph))
                {
                    failures.Add("[glyph-read] dropped id '" + id + "' has a catalog row with NO " +
                                 "glyph - the [item-identity] oracle pins that every row carries " +
                                 "one, so this is the authored identity going missing");
                    continue;
                }

                char want = row.Glyph[0];
                char got = ItemMoteShapes.ResolveGlyph(id);
                if (ItemMoteShapes.IsDrawn(want))
                {
                    if (got != want)
                        failures.Add("[glyph-read] '" + id + "' authors glyph '" + want + "' but the " +
                                     "mote resolves '" + got + "' - the world mote must READ the " +
                                     "authored glyph, never substitute one");
                    else honoured++;
                }
                else
                {
                    notes.Add("glyph '" + want + "' (" + id + ") has no bespoke silhouette yet - " +
                              "mapped deterministically to family '" + ItemMoteShapes.FamilyName(got) + "'");
                }
            }

            if (authored == 0)
                failures.Add("[glyph-read] not ONE dropped id resolved a catalog row - identity " +
                             "resolution is broken, not merely unauthored");
            if (honoured == 0 && authored > 0)
                failures.Add("[glyph-read] no dropped id draws its own authored glyph - the mote is " +
                             "back to inventing identity instead of reading it");

            notes.Add(honoured + "/" + authored + " authored drop ids draw their own glyph verbatim");
        }

        // =====================================================================
        //  CASE 2 - THE COLOURBLIND GATE: two different items differ with hue gone
        // =====================================================================
        private static void Case2_ShapeDistinct(List<string> dropIds, List<string> failures, List<string> notes)
        {
            // signature -> the set of AUTHORED glyphs that landed on it.
            var sigToGlyphs = new Dictionary<string, HashSet<char>>(StringComparer.Ordinal);
            var families = new HashSet<string>(StringComparer.Ordinal);

            foreach (string id in dropIds)
            {
                char g = ItemMoteShapes.ResolveGlyph(id);
                string sig = ItemMoteShapes.SignatureForId(id);
                families.Add(sig);

                if (string.IsNullOrEmpty(sig))
                {
                    failures.Add("[shape-distinct] '" + id + "' has an EMPTY silhouette signature");
                    continue;
                }
                var parts = ItemMoteShapes.PartsFor(g);
                if (parts == null || parts.Count == 0)
                    failures.Add("[shape-distinct] '" + id + "' resolves family '" +
                                 ItemMoteShapes.FamilyName(g) + "' with ZERO parts - an invisible mote");

                var row = ItemIdentity.Resolve(id);
                char authored = row.IsKnown && !string.IsNullOrEmpty(row.Glyph) ? row.Glyph[0] : '\0';
                if (authored == '\0') continue;   // unauthored ids are case 3's business

                HashSet<char> set;
                if (!sigToGlyphs.TryGetValue(sig, out set))
                {
                    set = new HashSet<char>();
                    sigToGlyphs[sig] = set;
                }
                set.Add(authored);
            }

            // The assertion this case exists for: DIFFERENT authored glyphs must never land on
            // the SAME silhouette. Ids that SHARE a glyph sharing a shape is intended - they are
            // one family, and we do not chase a unique silhouette per row.
            foreach (var kv in sigToGlyphs)
            {
                if (kv.Value.Count <= 1) continue;
                var glyphs = new List<char>(kv.Value);
                glyphs.Sort();
                failures.Add("[shape-distinct] glyphs '" + string.Join("', '",
                                 new List<string>(glyphs.ConvertAll(c => c.ToString())).ToArray()) +
                             "' all collapse onto ONE silhouette (" + kv.Key + ") - with hue " +
                             "stripped those drops are indistinguishable, which is the exact " +
                             "defect this suite pins");
            }

            if (families.Count < MinDistinctFamilies)
                failures.Add("[shape-distinct] the live drop set spreads over only " + families.Count +
                             " distinct silhouettes (floor " + MinDistinctFamilies + ") - one " +
                             "hardcoded shape for every drop is how this started");

            notes.Add(dropIds.Count + " drop ids over " + families.Count + " silhouettes");
        }

        // =====================================================================
        //  CASE 3 - the PO content gap degrades gracefully, never silently
        // =====================================================================
        private static void Case3_UnauthoredOk(List<string> failures, List<string> notes)
        {
            var seen = new Dictionary<string, string>(StringComparer.Ordinal);
            var stillMissing = new List<string>();

            foreach (string id in KnownUnauthored)
            {
                if (ItemMoteShapes.HasAuthoredIdentity(id))
                {
                    notes.Add("'" + id + "' now HAS a catalog row - the PO gap is closing");
                    continue;
                }
                stillMissing.Add(id);

                // Never a crash, never a bare sphere: a real family, deterministically.
                char g = ItemMoteShapes.ResolveGlyph(id);
                if (!ItemMoteShapes.IsDrawn(g))
                    failures.Add("[unauthored-ok] '" + id + "' fell off the family roster (got '" +
                                 g + "') - an unauthored id must still land on a REAL silhouette");

                string sig = ItemMoteShapes.SignatureForId(id);
                if (sig.StartsWith("pebble", StringComparison.Ordinal))
                    failures.Add("[unauthored-ok] '" + id + "' fell through to the terminal pebble - " +
                                 "unauthored must not mean shapeless");

                string other;
                if (seen.TryGetValue(sig, out other))
                    failures.Add("[unauthored-ok] '" + id + "' and '" + other + "' draw the SAME " +
                                 "silhouette (" + sig + ") - the four unauthored drops must stay " +
                                 "distinguishable from each other, not four identical spheres");
                else seen[sig] = id;

                // Determinism: the same id must draw the same shape every run, or a mote would
                // change identity between two chests carrying the same thing.
                if (ItemMoteShapes.ResolveGlyph(id) != g)
                    failures.Add("[unauthored-ok] '" + id + "' resolves a DIFFERENT family on a " +
                                 "second call - the fallback must be stable");

                // And it is still NAMED, never anonymous.
                string name = ItemIdentity.DisplayName(id);
                if (string.IsNullOrEmpty(name))
                    failures.Add("[unauthored-ok] '" + id + "' resolves an EMPTY display name - the " +
                                 "fallback must be named (the raw id), never blank");
            }

            // A null/empty id must not throw or produce nothing - Spawn can be handed a
            // degenerate line set from a mis-authored table.
            if (ItemMoteShapes.PartsFor(ItemMoteShapes.ResolveGlyph(null)).Count == 0)
                failures.Add("[unauthored-ok] a NULL id produces a mote with no parts");

            if (stillMissing.Count > 0)
                notes.Add("PO gap (not a failure): loot-tables drops " +
                          string.Join(", ", stillMissing.ToArray()) +
                          " with no consumables/materials row - each currently draws a hash-derived " +
                          "silhouette and shows its raw id as its name");
        }

        // =====================================================================
        //  CASE 4 - the secondary cue is KIND-only, and survives greyscale too
        // =====================================================================
        private static void Case4_Greyscale(List<string> failures, List<string> notes)
        {
            var tints = new[]
            {
                new KeyValuePair<string, Color>("consumable", ItemMoteShapes.ConsumableTint),
                new KeyValuePair<string, Color>("material",   ItemMoteShapes.MaterialTint),
                new KeyValuePair<string, Color>("unauthored", ItemMoteShapes.UnknownTint),
            };

            for (int i = 0; i < tints.Length; i++)
            {
                for (int j = i + 1; j < tints.Length; j++)
                {
                    float gap = Mathf.Abs(Luma(tints[i].Value) - Luma(tints[j].Value));
                    if (gap < GreyscaleBar)
                        failures.Add("[greyscale] the " + tints[i].Key + " and " + tints[j].Key +
                                     " mote tints are only " + gap.ToString("F3") + " Rec.709 luma " +
                                     "apart (bar " + GreyscaleBar.ToString("F2") + ") - with hue " +
                                     "stripped they are the same colour");
                }
            }

            // THE TINT IS A KIND CUE, NEVER AN IDENTITY. Two materials must share a tint;
            // if they ever stop sharing one, colour has quietly become the identity again.
            string a = FirstDropOfKind(ItemIdentityKind.Material);
            string b = SecondDropOfKind(ItemIdentityKind.Material, a);
            if (a != null && b != null && ItemMoteShapes.TintFor(a) != ItemMoteShapes.TintFor(b))
                failures.Add("[greyscale] materials '" + a + "' and '" + b + "' resolve DIFFERENT " +
                             "tints - the tint must carry KIND only; per-item hue is exactly what " +
                             "a red/green colourblind player cannot read");

            notes.Add("kind tint lumas: consumable " + Luma(ItemMoteShapes.ConsumableTint).ToString("F2") +
                      ", material " + Luma(ItemMoteShapes.MaterialTint).ToString("F2") +
                      ", unauthored " + Luma(ItemMoteShapes.UnknownTint).ToString("F2"));
        }

        // =====================================================================
        //  CASE 5 - the deleted-term lint on the spawner
        // =====================================================================
        private static void Case5_SpawnerCode(List<string> failures, List<string> notes)
        {
            string src = ReadSrc(SpawnerSrc);
            if (string.IsNullOrEmpty(src))
            {
                failures.Add("[spawner-code] cannot read " + SpawnerSrc);
                return;
            }
            if (string.IsNullOrEmpty(ReadSrc(ShapesSrc)))
                failures.Add("[spawner-code] cannot read " + ShapesSrc + " - the shape table is gone");

            Require(failures, src, "ItemMoteShapes.PartsFor",
                    "the mote no longer builds its silhouette from the shape table - it is back to " +
                    "one hardcoded shape for every drop");
            Require(failures, src, "ItemMoteShapes.ResolveGlyph",
                    "the mote no longer reads the AUTHORED glyph");
            Require(failures, src, "DestroyImmediate",
                    "the primitive collider is no longer destroyed - pickup is a DISTANCE CHECK, so " +
                    "a live collider only blocks the hero or punches a hole in the NavMesh");
            Require(failures, src, "Universal Render Pipeline/Lit",
                    "the URP shader lookup is gone - CreatePrimitive ships the built-in Standard " +
                    "shader, which URP renders MAGENTA (the pink-floor lesson)");
            Require(failures, src, "_EMISSION",
                    "the mote is no longer emissive - a lit-only mote is invisible at low lantern oil");
            Require(failures, src, "ResolveHeadlineId",
                    "the mote no longer picks a deterministic headline item, so the same roll could " +
                    "draw a different shape each time");

            if (src.IndexOf("CreatePrimitive(PrimitiveType.Sphere)", StringComparison.Ordinal) >= 0)
                failures.Add("[spawner-code] the spawner builds a literal Sphere primitive again - " +
                             "the ONE gold sphere for every drop is the defect this suite pins");

            notes.Add("spawner lint clean");
        }

        // =====================================================================
        //  helpers
        // =====================================================================

        private static void Require(List<string> failures, string src, string term, string why)
        {
            if (src.IndexOf(term, StringComparison.Ordinal) < 0)
                failures.Add("[spawner-code] '" + term + "' is gone from " + SpawnerSrc + " - " + why);
        }

        /// <summary>Rec.709 relative luminance - what a greyscale pass of the capture shows.</summary>
        private static float Luma(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

        /// <summary>Every distinct materialId any loot table can drop, ordinal-sorted for a
        /// stable report.</summary>
        private static List<string> CollectDropIds()
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            var tables = LootTableCatalog.All;
            if (tables != null)
            {
                foreach (var t in tables)
                {
                    if (t == null || t.Drops == null) continue;
                    foreach (var d in t.Drops)
                        if (d != null && !string.IsNullOrEmpty(d.MaterialId)) set.Add(d.MaterialId);
                }
            }
            var list = new List<string>(set);
            list.Sort(StringComparer.Ordinal);
            return list;
        }

        private static string FirstDropOfKind(ItemIdentityKind kind)
        {
            foreach (string id in CollectDropIds())
                if (ItemIdentity.KindOf(id) == kind) return id;
            return null;
        }

        private static string SecondDropOfKind(ItemIdentityKind kind, string skip)
        {
            foreach (string id in CollectDropIds())
                if (ItemIdentity.KindOf(id) == kind && id != skip) return id;
            return null;
        }

        private static string ReadSrc(string relPath)
        {
            try { return File.Exists(relPath) ? File.ReadAllText(relPath) : null; }
            catch { return null; }
        }
    }
}
