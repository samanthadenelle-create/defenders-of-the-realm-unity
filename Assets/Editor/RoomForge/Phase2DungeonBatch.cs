// =============================================================================
// Phase2DungeonBatch — batch entry points for the WO-1001 Phase 2 themed dungeons.
// -----------------------------------------------------------------------------
// Batch:
//   DeNelle.Editor.RoomForge.Phase2DungeonBatch.ComposeSunkenVault
//   DeNelle.Editor.RoomForge.Phase2DungeonBatch.ComposeBonecrypt
//   DeNelle.Editor.RoomForge.Phase2DungeonBatch.ComposeEmberDeep
//   DeNelle.Editor.RoomForge.Phase2DungeonBatch.ComposeAllPhase2   (one editor boot, all three)
//
// Deliberately a SEPARATE FILE from GraphDungeonComposer: that file is the shared
// engine and is being edited concurrently by the other seat (WO-1001 slices 6-8).
// Adding three entry points there would have made it a merge point for no benefit.
// =============================================================================

using System.IO;
using UnityEditor;
using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Editor.RoomForge
{
    public static class Phase2DungeonBatch
    {
        private const string Sys = "Phase2Dungeon";
        private const string GraphsFolder = "Assets/StreamingAssets/Data/Canonical/dungeon-graphs";

        /// <summary>The three WO-1001 Phase 2 dungeons, in intended play order.</summary>
        public static readonly string[] Phase2Graphs =
        {
            "dg_sunken_vault.json",
            "dg_bonecrypt.json",
            "dg_ember_deep.json",
        };

        [MenuItem("Defenders/Dungeon/Compose Phase 2 - Sunken Vault")]
        public static void ComposeSunkenVaultMenu() => Compose("dg_sunken_vault.json");
        [MenuItem("Defenders/Dungeon/Compose Phase 2 - Bonecrypt")]
        public static void ComposeBonecryptMenu() => Compose("dg_bonecrypt.json");
        [MenuItem("Defenders/Dungeon/Compose Phase 2 - Ember Deep")]
        public static void ComposeEmberDeepMenu() => Compose("dg_ember_deep.json");

        public static void ComposeSunkenVault() { Compose("dg_sunken_vault.json"); EditorApplication.Exit(0); }
        public static void ComposeBonecrypt() { Compose("dg_bonecrypt.json"); EditorApplication.Exit(0); }
        public static void ComposeEmberDeep() { Compose("dg_ember_deep.json"); EditorApplication.Exit(0); }

        /// <summary>
        /// Bake all three in ONE editor boot. Each bake opens a fresh empty scene, so they cannot
        /// contaminate each other; a graph that aborts is reported and the run CONTINUES, because
        /// stopping at the first failure would hide how many of the three are actually broken.
        /// </summary>
        public static void ComposeAllPhase2()
        {
            int ok = 0;
            foreach (string g in Phase2Graphs)
            {
                bool composed = Compose(g);
                if (composed) ok++;
            }
            FlowTrace.Step(Sys, $"PHASE2 SUMMARY composed={ok}/{Phase2Graphs.Length}");
            EditorApplication.Exit(0);
        }

        private static bool Compose(string graphFile)
        {
            string path = Path.Combine(GraphsFolder, graphFile);
            if (!File.Exists(path))
            {
                FlowTrace.Fail(Sys, $"graph missing: {path}");
                return false;
            }

            // populateForPlay: seat the hero + the room spawners so each dungeon is enterable
            // straight off its portal, the same way the starter loop is.
            bool ok = false;
            Guard.Try(Sys, $"compose {graphFile}", () =>
            {
                GraphDungeonComposer.ComposeAndBake(path, populateForPlay: true);
                ok = true;
            });
            return ok;
        }
    }
}
