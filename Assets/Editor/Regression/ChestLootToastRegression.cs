// =============================================================================
// ChestLootToastRegression [chest-loot-toast] -- WO-1589.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Village + DeNelle.Core).
//
// THE DEFECT IT PINS (owner, Seeker, dg_sunken_vault, 2026-09-07 09:36):
//   "when i open a chest no toast to what i found"
// Device log, same session, 25 seconds apart:
//   09:35:34 [Flow:Loot]   Chest_crate opened -> dropped 2 loot line(s) as a world mote
//   09:35:59 [Flow:Reward] KILL REWARD TOAST '+17 XP  +7 gold' ... routed=CombatText(Reward)
// The kill path spoke; the loot path ended at the drop and said nothing.
//
// THE RULE THIS SUITE HOLDS -- and it is TWO-SIDED on purpose:
//   * loot that BANKS is announced ONCE, through the kill path's own bounded
//     CombatText(Reward) stamp, with EVERY loot line named;
//   * loot that has only DROPPED announces NOTHING. A mote the player walked past
//     granted nothing, and a toast there would claim what is not held. A one-sided
//     suite would go green on a fix that toasts at the chest open -- the exact
//     wrong fix -- so the negative case is load-bearing, not decoration.
//
// Marker: CHEST_LOOT_TOAST_OK / CHEST_LOOT_TOAST_FAIL. Expected: GREEN.
//
// Wire (DataRegression.RunAll):
//   if (!ChestLootToastRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[chest-loot-toast] " + r);
// =============================================================================
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using DeNelle.Village.Items;

namespace DeNelle.Editor
{
    /// <summary>
    /// Pins the WO-1589 loot-reward announcement contract: one toast at the bank,
    /// zero at the drop, every line named, routed through the ONE stamp seam.
    /// Returns true (summary) / false (detail); never throws.
    /// </summary>
    public static class ChestLootToastRegression
    {
        // Real crate-common rows (Assets/Resources/Data/Canonical/loot-tables.json,
        // table 'crate-common' -> BoneFragment / IronScrap / HealthHerb). The fixture
        // uses SHIPPED ids so a rename of an authored material fails this suite rather
        // than passing against invented test data.
        private const string LineA = "BoneFragment";
        private const string LineB = "IronScrap";

        /// <summary>PlayerPrefs key the persisted save lives under (mirrors EnemyRewardRegression).</summary>
        private const string SaveKey = "dotr-save";

        private const string MarkerPath ="Assets/_Modules/Village/Items/ItemPickupMarker.cs";
        private const string ChestPath = "Assets/_Modules/Village/World/BreakableContainer.cs";

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- CHEST LOOT TOAST (WO-1589: what did I just find?) ---");

            Case(failures, "label-names-every-line", () => Case1_LabelNamesEveryLine(failures, log));

            // Cases 2+3 drive the REAL grant path (ItemInventory.GrantDrop -> VillageInventory
            // -> GameState.GearInventory), so they can write the persisted save if an earlier
            // suite in this ~200-suite run left a live VillageInventory singleton bound. Snapshot
            // and restore 'dotr-save' around them, the same guard EnemyRewardRegression keeps for
            // the same reason - a suite must not depend on, or change, run order.
            bool hadSave = PlayerPrefs.HasKey(SaveKey);
            string rawSave = hadSave ? PlayerPrefs.GetString(SaveKey, null) : null;
            try
            {
                Case(failures, "pickup-announces-once", () => Case2_PickupAnnouncesExactlyOnce(failures, log));
                Case(failures, "drop-without-pickup-silent", () => Case3_DropWithoutPickupIsSilent(failures, log));
            }
            finally
            {
                if (hadSave) PlayerPrefs.SetString(SaveKey, rawSave); else PlayerPrefs.DeleteKey(SaveKey);
                PlayerPrefs.Save();
            }

            Case(failures, "one-seam-source-lint", () => Case4_OneSeamSourceLint(failures, log));

            if (failures.Count > 0)
            {
                reason = "CHEST_LOOT_TOAST_FAIL: " + string.Join(" | ", failures) + "\n" + log;
                return false;
            }
            reason = "CHEST_LOOT_TOAST_OK - 4/4 cases\n" + log;
            return true;
        }

        // -- Case 1: the label names EVERY line, deterministically -------------------
        // The owner's complaint is not "no popup", it is "no toast to WHAT I found".
        // A label that names one of two lines is the same defect in a smaller font, so
        // the assertion is per-line, not "non-empty". Determinism is asserted too: the
        // source is a Dictionary, whose enumeration order is not stable, and the same
        // roll reading two different ways would be worse than reading none.
        private static void Case1_LabelNamesEveryLine(List<string> failures, StringBuilder log)
        {
            var lines = new Dictionary<string, int> { { LineA, 2 }, { LineB, 1 } };
            string label = LootRewardToast.ComposeLabel(lines);

            if (string.IsNullOrEmpty(label))
            {
                failures.Add("[chest-loot-toast] ComposeLabel returned empty for a 2-line roll (nothing would be said)");
                return;
            }

            string nameA = ItemIdentity.DisplayName(LineA);
            string nameB = ItemIdentity.DisplayName(LineB);
            if (string.IsNullOrEmpty(nameA)) nameA = LineA;
            if (string.IsNullOrEmpty(nameB)) nameB = LineB;

            if (!label.Contains(nameA))
                failures.Add($"[chest-loot-toast] label '{label}' does not name loot line '{LineA}' (display '{nameA}')");
            if (!label.Contains(nameB))
                failures.Add($"[chest-loot-toast] label '{label}' does not name loot line '{LineB}' (display '{nameB}')");
            if (!label.Contains("+2 "))
                failures.Add($"[chest-loot-toast] label '{label}' drops the COUNT of the 2x line (a count-less toast under-reports the grant)");

            // Determinism over a re-ordered dictionary carrying the SAME roll.
            var reordered = new Dictionary<string, int> { { LineB, 1 }, { LineA, 2 } };
            string again = LootRewardToast.ComposeLabel(reordered);
            if (!string.Equals(label, again, StringComparison.Ordinal))
                failures.Add($"[chest-loot-toast] label is not deterministic: '{label}' vs '{again}' for the same roll");

            // Nothing bankable -> nothing said (a zero/blank line must not produce "+0 ").
            string empty = LootRewardToast.ComposeLabel(new Dictionary<string, int> { { LineA, 0 }, { "", 3 } });
            if (!string.IsNullOrEmpty(empty))
                failures.Add($"[chest-loot-toast] a roll with no bankable line composed '{empty}' (must be empty -> no toast)");

            log.AppendLine($"  label: '{label}' (names both lines, deterministic, empty-safe)");
        }

        // -- Case 2: the BANK announces exactly once --------------------------------
        // Drives the real ItemPickupMarker.Collect -- the moment the mote's contents
        // move into the larder -- and asserts the producer routed ONE stamp naming both
        // lines. CombatText.Show itself no-ops outside play mode (CombatTextLayer:49),
        // so the observable is LootRewardToast's own permanent counter, not a rendered
        // label; that limit is stated rather than papered over.
        private static void Case2_PickupAnnouncesExactlyOnce(List<string> failures, StringBuilder log)
        {
            bool priorEnabled = ItemDropSystem.Enabled;
            GameObject mote = null;
            try
            {
                ItemDropSystem.Enabled = true;
                LootRewardToast.ResetCounters();

                mote = new GameObject("ItemDropMote (chest-loot-toast oracle)");
                mote.transform.position = new Vector3(3f, 0f, 7f);
                var marker = mote.AddComponent<ItemPickupMarker>();
                marker.Init(new Dictionary<string, int> { { LineA, 2 }, { LineB, 1 } });

                var collect = typeof(ItemPickupMarker).GetMethod(
                    "Collect", BindingFlags.Instance | BindingFlags.NonPublic);
                if (collect == null)
                {
                    failures.Add("[chest-loot-toast] ItemPickupMarker.Collect no longer exists - the bank moment moved, and this oracle cannot see the new one");
                    return;
                }

                // No exception is tolerated here. Collect's teardown is editor-boundary-safe
                // (Application.isPlaying ? Destroy : DestroyImmediate), so a throw from this
                // invoke is a real defect, not an edit-mode artefact to be swallowed.
                try { collect.Invoke(marker, null); }
                catch (TargetInvocationException tie)
                {
                    failures.Add("[chest-loot-toast] ItemPickupMarker.Collect threw: " +
                                 (tie.InnerException != null ? tie.InnerException.Message : "(none)"));
                }

                if (LootRewardToast.AnnouncedCount != 1)
                {
                    failures.Add($"[chest-loot-toast] a mote pickup routed {LootRewardToast.AnnouncedCount} reward stamp(s), expected exactly 1 " +
                                 "(0 = the WO-1589 silent grant is back; >1 = the loot bank grew a second producer)");
                    return;
                }

                string label = LootRewardToast.LastLabel ?? "";
                string nameA = ItemIdentity.DisplayName(LineA);
                string nameB = ItemIdentity.DisplayName(LineB);
                if (string.IsNullOrEmpty(nameA)) nameA = LineA;
                if (string.IsNullOrEmpty(nameB)) nameB = LineB;
                if (!label.Contains(nameA) || !label.Contains(nameB))
                    failures.Add($"[chest-loot-toast] the pickup toast '{label}' does not name every banked line ('{nameA}' + '{nameB}')");

                log.AppendLine($"  pickup -> 1 stamp, label '{label}'");
            }
            finally
            {
                if (mote != null) UnityEngine.Object.DestroyImmediate(mote);
                ItemDropSystem.Enabled = priorEnabled;
            }
        }

        // -- Case 3: a DROP nobody walked over says nothing --------------------------
        // This is the case that makes the suite falsifiable. Spawn the mote a chest open
        // produces and never collect it: the counter must stay at zero. A "fix" that
        // toasts at BreakableContainer.Open -- claiming loot the player does not hold --
        // fails right here.
        private static void Case3_DropWithoutPickupIsSilent(List<string> failures, StringBuilder log)
        {
            bool priorEnabled = ItemDropSystem.Enabled;
            GameObject spawned = null;
            try
            {
                ItemDropSystem.Enabled = true;
                LootRewardToast.ResetCounters();

                var lines = new Dictionary<string, int> { { LineA, 2 }, { LineB, 1 } };
                ItemPickupSpawner.Spawn(new Vector3(-11f, 0f, 4f), lines);

                // Find what Spawn built so the oracle can tear it down (Spawn returns void).
                foreach (var go in UnityEngine.Object.FindObjectsByType<ItemPickupMarker>(
                             FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (go != null && go.name.StartsWith("ItemDropMote_", StringComparison.Ordinal))
                    { spawned = go.gameObject; break; }
                }
                if (spawned == null)
                    log.AppendLine("  note: the spawned mote could not be re-found for teardown (world-pickup lane may be off in this environment)");

                if (LootRewardToast.AnnouncedCount != 0)
                    failures.Add($"[chest-loot-toast] a DROP that was never picked up routed {LootRewardToast.AnnouncedCount} reward stamp(s), expected 0 " +
                                 "(the toast moved to the drop and now claims loot the player does not hold - WO-1589 says the toast belongs at the BANK)");
                else
                    log.AppendLine("  drop without pickup -> 0 stamps (the mote stays in the world, unclaimed and unannounced)");
            }
            finally
            {
                if (spawned != null) UnityEngine.Object.DestroyImmediate(spawned);
                ItemDropSystem.Enabled = priorEnabled;
            }
        }

        // -- Case 4: ONE seam, and it is the kill path's ------------------------------
        // Source lint, the same probe shape EnemyRewardRegression uses for the kill toast.
        // It is here because the value cases above cannot see WHICH presentation was used:
        // a second, unbounded loot toast would satisfy the counter and still be the defect
        // WO-1103 removed from the kill path.
        private static void Case4_OneSeamSourceLint(List<string> failures, StringBuilder log)
        {
            string markerSrc = ReadOrFail(MarkerPath, failures);
            string chestSrc = ReadOrFail(ChestPath, failures);
            if (markerSrc == null || chestSrc == null) return;

            if (!markerSrc.Contains("CombatText.Show(CombatTextKind.Reward"))
                failures.Add("[chest-loot-toast] LootRewardToast no longer routes through the bounded CombatText(Reward) stamp (a second toast system has appeared)");
            if (!markerSrc.Contains("CHEST REWARD TOAST"))
                failures.Add("[chest-loot-toast] the 'CHEST REWARD TOAST' FlowTrace line is gone - the device log can no longer prove the toast fired (WO-1589 acceptance)");
            if (!chestSrc.Contains("ItemPickupSpawner.Spawn(at, lines, \"chest\")"))
                failures.Add("[chest-loot-toast] the chest no longer tags its mote 'chest' - a device log can no longer tell a chest pickup from a kill-drop pickup (WO-1589 acceptance is per-chest)");
            if (!markerSrc.Contains("LootRewardToast.Announce(_carried"))
                failures.Add("[chest-loot-toast] ItemPickupMarker.Collect no longer announces what it banked - the silent grant is back");
            if (markerSrc.Contains("ElarionUiKit.ShowToast"))
                failures.Add("[chest-loot-toast] the loot bank grew a SECOND toast surface (ElarionUiKit.ShowToast) beside the CombatText stamp");

            // The world-mote branch of Open() must stay silent. Anchor on the trace line
            // that branch prints, so this fails by name if a toast is added beside it.
            int branchStart = chestSrc.IndexOf("if (ItemDropSystem.UseWorldPickups", StringComparison.Ordinal);
            int anchor = branchStart < 0 ? -1 : chestSrc.IndexOf("as a world mote (table", branchStart, StringComparison.Ordinal);
            int branchEnd = anchor < 0 ? -1 : chestSrc.IndexOf("else", anchor, StringComparison.Ordinal);
            if (branchStart < 0 || anchor < 0 || branchEnd < 0)
            {
                failures.Add("[chest-loot-toast] BreakableContainer's world-mote drop branch could not be located (the UseWorldPickups guard or its drop trace moved) - this lint cannot police what it cannot find");
            }
            else if (chestSrc.Substring(branchStart, branchEnd - branchStart).Contains("LootRewardToast.Announce"))
            {
                failures.Add("[chest-loot-toast] BreakableContainer announces at the DROP - it claims loot that is still lying on the floor");
            }

            if (!chestSrc.Contains("LootRewardToast.Announce(lines"))
                failures.Add("[chest-loot-toast] BreakableContainer's direct-deposit fallback banks straight to the larder and says nothing (the same silent grant, on the no-world-pickups path)");

            log.AppendLine("  seam lint: one CombatText(Reward) producer; announce at both banks; the drop branch stays silent");
        }

        private static string ReadOrFail(string path, List<string> failures)
        {
            try { return System.IO.File.ReadAllText(path); }
            catch (Exception ex)
            {
                failures.Add($"[chest-loot-toast] could not read {path}: {ex.Message}");
                return null;
            }
        }

        private static void Case(List<string> failures, string id, Action body)
        {
            try { body(); }
            catch (Exception e) { failures.Add($"{id} THREW: {e.GetType().Name}: {e.Message}"); }
        }

        /// <summary>Standalone entry point (run-unity-method).</summary>
        public static void RunAll()
        {
            bool ok = Run(out string reason);
            Debug.Log(reason);
            if (!ok) EditorApplication.Exit(1);
        }
    }
}
