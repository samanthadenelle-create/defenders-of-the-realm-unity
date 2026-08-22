// =============================================================================
// DungeonStatusDevMenu — WO-1114. Prove a sealed door with NO backend.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Editor (editor-only)   Namespace: DeNelle.Editor
//
// WHY THIS EXISTS, AND WHY IT IS NOT A TEST HOOK:
//   The whole point of WO-1114 is that a door can close without a build. The
//   backend is the LAST slice of that work, so until it lands there would be no
//   way to see, felt-test or screenshot a sealed door at all — and a feature
//   that cannot be demonstrated does not get ratified, tuned or trusted.
//   This menu writes the REAL cache file that DungeonStatusService reads at
//   boot (persistentDataPath/dungeon-status-cache.json), so it exercises the
//   production path end to end: file -> LoadCache -> ApplyPayload -> catalog ->
//   DungeonPortal gate -> DungeonSealedDoorPanel -> ApplyDoorState.
//   ⛔ It is deliberately NOT a parallel injection API. A second way to seed the
//   catalog would be a second authority, and the one this repo already pays for
//   over and over is duplicated state.
//
// THE STUB IS DEV-SIDE DATA, NOT PLAYER COPY. It writes the STATUS ONLY and
//   ships no headline/body, so the player-facing prose comes from
//   canon-strings.json exactly as it will in production. That is on purpose:
//   typing prose here would create an un-gated copy path that the
//   DungeonStatusRegression [door-copy] oracle cannot see.
//
// Menu: Defenders/Dungeon Status/...
// Headless: -executeMethod DeNelle.Editor.DungeonStatusDevMenu.SealBonecryptHeadless
//           -executeMethod DeNelle.Editor.DungeonStatusDevMenu.ClearHeadless
// =============================================================================

using System;
using System.IO;
using System.Text;
using DeNelle.Core.World;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>Writes / clears the dungeon-status cache stub so a closed door is
    /// reproducible in the editor and headlessly, with no server.</summary>
    public static class DungeonStatusDevMenu
    {
        private const string MenuRoot = "Defenders/Dungeon Status/";
        private const string Marker = "DUNGEON_STATUS_STUB";

        // ─────────────────────────────────────────────────────────────────────
        //  Menu items — one per state, plus the two housekeeping entries
        // ─────────────────────────────────────────────────────────────────────

        [MenuItem(MenuRoot + "Seal 'dg_bonecrypt' (write cache stub)", priority = 10)]
        public static void SealBonecrypt() => WriteStub("dg_bonecrypt", "sealed");

        [MenuItem(MenuRoot + "Collapse 'dg_sunken_vault' (write cache stub)", priority = 11)]
        public static void CollapseSunkenVault() => WriteStub("dg_sunken_vault", "collapsed");

        [MenuItem(MenuRoot + "Flood 'dg_ember_deep' (write cache stub)", priority = 12)]
        public static void FloodEmberDeep() => WriteStub("dg_ember_deep", "flooded");

        [MenuItem(MenuRoot + "Rescue at 'dg_starter_loop' (write cache stub)", priority = 13)]
        public static void RescueStarterLoop() => WriteStub("dg_starter_loop", "rescue");

        [MenuItem(MenuRoot + "OPEN EVERYTHING (delete cache stub)", priority = 30)]
        public static void ClearStub()
        {
            string path = DungeonStatusService.CachePath;
            try
            {
                if (File.Exists(path)) File.Delete(path);
                DungeonStatusCatalog.Clear();
                Debug.Log(Marker + "_CLEARED - deleted '" + path + "'; every dungeon door resolves OPEN. " +
                          "Note: a live backend fetch on the next boot will overwrite this again.");
            }
            catch (Exception ex)
            {
                Debug.LogError(Marker + "_FAIL: could not delete '" + path + "': " +
                               ex.GetType().Name + " " + ex.Message);
            }
        }

        [MenuItem(MenuRoot + "Show cache file path + current table", priority = 31)]
        public static void ShowState()
        {
            string path = DungeonStatusService.CachePath;
            var sb = new StringBuilder();
            sb.Append(Marker).Append("_STATE path='").Append(path).Append("' exists=")
              .Append(File.Exists(path)).Append(" provenance=").Append(DungeonStatusCatalog.Provenance)
              .Append(" rows=").Append(DungeonStatusCatalog.RowCount);
            foreach (string id in DungeonStatusCatalog.PortalDungeonIds)
                sb.Append("\n  ").Append(id).Append(" = ").Append(DungeonStatusCatalog.For(id).State);
            Debug.Log(sb.ToString());
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Headless entry points (batchmode -executeMethod)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Batch entry: seal the bonecrypt. Used to prove the door gate headlessly.</summary>
        public static void SealBonecryptHeadless() => SealBonecrypt();

        /// <summary>Batch entry: delete the stub and reset to all-open.</summary>
        public static void ClearHeadless() => ClearStub();

        // ─────────────────────────────────────────────────────────────────────
        //  The write
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Write a §3-shaped payload closing exactly ONE dungeon and leaving the other
        /// three explicitly open, then apply it immediately so the editor reflects it
        /// without a domain reload. Status only — no prose (see the file header).
        /// </summary>
        private static void WriteStub(string dungeonId, string status)
        {
            var sb = new StringBuilder();
            sb.Append("{\n  \"version\": ").Append(DungeonStatusCatalog.PayloadVersion).Append(",\n  \"dungeons\": {\n");
            var ids = DungeonStatusCatalog.PortalDungeonIds;
            for (int i = 0; i < ids.Length; i++)
            {
                string state = string.Equals(ids[i], dungeonId, StringComparison.Ordinal) ? status : "open";
                sb.Append("    \"").Append(ids[i]).Append("\": { \"status\": \"").Append(state).Append("\" }");
                if (i < ids.Length - 1) sb.Append(',');
                sb.Append('\n');
            }
            sb.Append("  }\n}\n");
            string json = sb.ToString();

            string path = DungeonStatusService.CachePath;
            try
            {
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                Debug.LogError(Marker + "_FAIL: could not write '" + path + "': " +
                               ex.GetType().Name + " " + ex.Message);
                return;
            }

            // Apply right now as well, so an already-running editor session sees it without
            // a reboot. This is the SAME entry point the boot path uses.
            bool applied = DungeonStatusCatalog.ApplyPayload(json, DungeonStatusCatalog.ProvenanceCache);

            Debug.Log(Marker + "_OK id='" + dungeonId + "' status='" + status + "' applied=" + applied +
                      " path='" + path + "'\n" + json +
                      "\nEnter Play in the hub: that portal must show its authored prose and must NOT " +
                      "load a scene. Use 'OPEN EVERYTHING' to undo.");
        }
    }
}
