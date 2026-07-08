// =============================================================================
// StructureOrientationLocalStore — local-first structure-orientation overlay.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Owner 2026-07-08 ("when i use the placement tool it doesnt translate data back
// to game" / "should it save locally as well?"): the Orient tool applied dials to
// the LIVE CatalogEntry only and appended a breadcrumb to orientation-recipes.json
// inside the BUILD's data folder — wiped on every rebuild, parsed by nothing
// (write-only; the ledger's known JSONL flag). Dials never survived a session.
//
// This store is the same architecture as the 2026-07-07 gear-offsets fix
// (AttachmentOffsetRegistry): every Orient-tool Confirm UPSERTS the pose into
//   Application.persistentDataPath/structure-orientations.json
// and CatalogBootstrap overlays that file onto the freshly-loaded catalog at
// startup — LOCAL WINS over shipped data (manual=true), so an owner dial sticks
// in-game immediately AND across sessions/rebuilds. The [OrientRecipe] console
// line remains the copy-paste source for the CLI to bake the pose into the repo
// catalog; once baked, the local row and the shipped row agree (idempotent).
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using DeNelle.Core.Catalog;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>
    /// Persists owner-dialed structure orientations locally and overlays them onto
    /// the catalog at load. Local entries WIN over shipped structures-catalog.json
    /// (they are manual corrections — the same "manual is canon" rule the baker obeys).
    /// </summary>
    public static class StructureOrientationLocalStore
    {
        private static string FilePath =>
            Path.Combine(Application.persistentDataPath, "structure-orientations.json");

        [Serializable]
        private sealed class Row
        {
            public string id;
            public float[] euler;
            public float[] offset;
            public float scale = 1f;
            public float[] scaleAxis;
            public string savedUtc;
        }

        [Serializable]
        private sealed class FileShape
        {
            public int version = 1;
            public List<Row> orientations = new List<Row>();
        }

        /// <summary>Save (insert or replace) one dialed orientation. Never throws to the UI.</summary>
        public static void Upsert(string id, OrientationFix fix)
        {
            if (string.IsNullOrEmpty(id) || fix == null) return;
            Guard.Try("Orient", $"local-save orientation '{id}'", () =>
            {
                var file = LoadFile() ?? new FileShape();
                file.orientations.RemoveAll(r => r != null && r.id == id);
                file.orientations.Add(new Row
                {
                    id        = id,
                    euler     = fix.euler,
                    offset    = fix.offset,
                    scale     = fix.scale,
                    scaleAxis = fix.scaleAxis,
                    savedUtc  = DateTime.UtcNow.ToString("o"),
                });
                File.WriteAllText(FilePath, JsonConvert.SerializeObject(file, Formatting.Indented));
                FlowTrace.Step("Orient", $"local-saved orientation '{id}' -> {FilePath} " +
                    $"({file.orientations.Count} local pose(s); local WINS over shipped catalog at load).");
            });
        }

        /// <summary>
        /// Overlay every locally-saved pose onto the registered catalog entries.
        /// Call AFTER the catalog registers (CatalogBootstrap). Returns overlaid count.
        /// </summary>
        public static int ApplyAll()
        {
            int applied = 0;
            Guard.Try("Orient", "apply local orientation overlay", () =>
            {
                var file = LoadFile();
                if (file == null || file.orientations == null || file.orientations.Count == 0) return;
                foreach (var row in file.orientations)
                {
                    if (row == null || string.IsNullOrEmpty(row.id)) continue;
                    var entry = CatalogRegistry.Get(row.id);
                    if (entry == null)
                    {
                        FlowTrace.Warn("Orient", $"local pose '{row.id}' has no catalog entry — skipped (stale save?).");
                        continue;
                    }
                    entry.orientation = new OrientationFix
                    {
                        corrected = true,
                        manual    = true,   // owner-dialed → StructureFactory/GhostPreview apply it
                        euler     = row.euler     ?? new[] { 0f, 0f, 0f },
                        offset    = row.offset    ?? new[] { 0f, 0f, 0f },
                        scale     = row.scale <= 0f ? 1f : row.scale,
                        scaleAxis = row.scaleAxis ?? new[] { 1f, 1f, 1f },
                        note      = "local overlay (owner dial " + row.savedUtc + ")",
                    };
                    applied++;
                }
                if (applied > 0)
                    FlowTrace.Step("Orient", $"local orientation overlay applied: {applied} pose(s) " +
                        "win over shipped catalog (bake via the [OrientRecipe] lines to converge).");
            });
            return applied;
        }

        private static FileShape LoadFile()
        {
            try
            {
                if (!File.Exists(FilePath)) return null;
                return JsonConvert.DeserializeObject<FileShape>(File.ReadAllText(FilePath));
            }
            catch (Exception e)
            {
                FlowTrace.Warn("Orient", $"local orientation file unreadable ({e.Message}) — ignoring it.");
                return null;
            }
        }
    }
}
