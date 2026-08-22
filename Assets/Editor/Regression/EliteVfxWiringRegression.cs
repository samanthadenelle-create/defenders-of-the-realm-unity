// =============================================================================
// EliteVfxWiringRegression [elite-vfx-wire] - the oracle that stops WO-874's
// ruling from being routed around a SECOND time.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression   Namespace: DeNelle.Editor.Regression
//
// THE FAILURE THIS PINS ALREADY HAPPENED, AND IT PASSED EVERY GATE WE HAD.
//
// The owner ruled on 2026-08-04 (RECONFIRMED VERBATIM 2026-08-21, "874 wire ruling
// stands") that EliteVFXController must be WIRED - AddComponent'd on the elite/boss
// spawn path so its spawn, aura, attack and death actually fire. Commit 4c1da079
// instead promoted two of its methods to STATICS and called them from Enemy.cs.
// That delivered the visible half (the spawn tell, the tiered kill shake), so the
// ticket READ as progressed - while `AddComponent<EliteVFXController>` still
// returned ZERO HITS repo-wide and the two behaviours only an INSTANCE can own,
// the pulsing aura and OnEliteAttack, had never run in the shipped game.
//
// Nothing could catch that, because every existing check asks "does the effect
// play?" and the shortcut made effects play. So this suite asks the question the
// ruling actually turns on: IS THE COMPONENT ATTACHED, AND IS ITS INSTANCE API
// CALLED FROM SOMEWHERE. Those are the two things a static shortcut cannot fake.
//
// -----------------------------------------------------------------------------
// WHAT IT MEASURES (and therefore, how it can go red - the honest list):
//
//   (1) SOURCE-LINT, required by the ticket in as many words. It READS the .cs
//       bytes under Assets/_Modules/Village/ and requires a literal
//       AddComponent<EliteVFXController> in at least one of them. Delete the
//       attach or replace it with another static and this goes red on the next
//       run. It scans the VILLAGE tree only, so this file's own prose can never
//       satisfy the check it performs - a self-satisfying lint is worse than none.
//
//   (2) OnEliteAttack HAS A CALLER OUTSIDE ITS OWN FILE. Measured the same way.
//       The attack tell is the behaviour the shortcut left dead; a controller that
//       is attached but never asked to play its attack is the same bug wearing a
//       component. Excludes EliteVFXController.cs itself (its declaration is not a
//       call) and this file.
//
//   (3) ArmForTier IS A REAL PUBLIC INSTANCE METHOD ON THE REAL TYPE, resolved by
//       REFLECTION rather than by grep. (1) and (2) read text; this reads the
//       compiled type, so the two halves cannot both be satisfied by a comment.
//       A method that went private or static fails here.
//
//   (4) DragonBoss REFERENCES VFXType.Boss_Spawn. WO-874 E8: this boss played a
//       burst on every beat it has EXCEPT its arrival, and it does not go through
//       Enemy.Configure's attach seam (it is not an Enemy), so its entrance has to
//       be its own call. Remove it and this goes red.
//
//   (5) CATALOG COVERAGE OF THE Boss_*/Elite_* LADDER, REPORTED, NOT FAILED.
//       WO-874 E9 recorded all ten rows as PROC-only. That is now largely STALE -
//       measured 2026-08-22, nine resolve through VFXCatalogGenerator.Map. The
//       three that do NOT are listed in the pass reason every run so the gap stays
//       visible without blocking. It CANNOT be a failure: under the standing rule
//       (memory vfx-map-owner-tags-no-creative-pick) the OWNER tags the art and the
//       CLI maps her tag verbatim - a red gate here would be pressure on an
//       engineer to pick a prefab, which is the exact thing the rule forbids.
//
// A NOTE ON WHY (1) AND (2) ARE TEXT LINTS AND NOT RUNTIME ASSERTIONS: attaching
// happens inside Enemy.Configure, which needs a spawned enemy, a stat block and a
// NavMesh. An edit-mode suite cannot stand that up, and a PlayMode fixture that
// could would be the slowest gate in the project. The bytes are the honest proxy,
// and they are the exact bytes the audit grepped when it caught the shortcut.
//
// Editor-only file reads + one reflection lookup. No scene, no play mode.
// Registered in DataRegression.RunAll as [elite-vfx-wire].
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class EliteVfxWiringRegression
    {
        /// <summary>The literal the 2026-08-08 audit grepped for and found zero of.</summary>
        private const string AttachLiteral = "AddComponent<EliteVFXController>";

        private const string AttackMethod  = "OnEliteAttack";
        private const string ArmMethod     = "ArmForTier";
        private const string ControllerFile = "EliteVFXController.cs";

        /// <summary>
        /// The shape of a VFXCatalogGenerator.Map row, matched WITHOUT its opening brace:
        /// the QUOTED KEY IMMEDIATELY FOLLOWED BY A COMMA, e.g. <c>"Elite_Death",</c>.
        /// A Map row is the only place that shape occurs - in prose the names appear bare.
        /// <para>
        /// ⚠ Matching the brace too would be marginally tighter and is deliberately NOT
        /// done: CLAUDE.md §1 makes every touched .cs pass a brace-balance count, and that
        /// counter is a plain character tally that cannot tell code from a string literal.
        /// One literal opening brace inside a string here reports this whole file as
        /// MISMATCHED and sends correct work back round the loop.
        /// </para>
        /// </summary>
        private static string MapRowShape(string key) { return "\"" + key + "\","; }

        /// <summary>
        /// The Boss_*/Elite_* VFXType rows WO-874 E9 audited. Listed here as NAMES only -
        /// the suite reads their catalog coverage off VFXCatalogGenerator.cs on disk, so
        /// this array is the question, never the answer.
        /// </summary>
        private static readonly string[] TierLadder =
        {
            "Elite_Spawn", "Elite_Death",
            "Boss_Spawn", "Boss_Death", "Boss_AttackImpact",
            "Boss_PhaseTransition", "Boss_Telegraph",
            "Boss_Aura_Phase1", "Boss_Aura_Phase2", "Boss_Aura_Phase3",
            "Boss_FireBreath",
        };

        public static bool Run(out string reason)
        {
            var fails = new List<string>();
            var log = new StringBuilder();

            string assets = Application.dataPath;
            string villageDir = Path.Combine(assets, "_Modules/Village");

            // ── Gather the Village .cs corpus ONCE ────────────────────────────────
            List<string> villageFiles;
            try
            {
                villageFiles = new List<string>(
                    Directory.GetFiles(villageDir, "*.cs", SearchOption.AllDirectories));
            }
            catch (Exception e)
            {
                reason = "elite-vfx-wire FAIL: could not enumerate '" + villageDir + "': " + e.Message;
                return false;
            }

            if (villageFiles.Count == 0)
            {
                reason = "elite-vfx-wire FAIL: '" + villageDir + "' holds no .cs files - the source " +
                         "lint would pass vacuously, so this is a hard fail rather than a skip.";
                return false;
            }

            // ── (1) THE ATTACH ────────────────────────────────────────────────────
            var attachSites = new List<string>();
            var attackCallers = new List<string>();
            string dragonBossText = null;

            foreach (string path in villageFiles)
            {
                string text;
                try { text = File.ReadAllText(path); }
                catch (Exception e) { fails.Add("unreadable source '" + Rel(path) + "': " + e.Message); continue; }

                string leaf = Path.GetFileName(path);

                if (text.Contains(AttachLiteral)) attachSites.Add(Rel(path));

                // (2) a CALL to OnEliteAttack from anywhere that is not the declaring file.
                if (!string.Equals(leaf, ControllerFile, StringComparison.OrdinalIgnoreCase) &&
                    text.Contains(AttackMethod + "("))
                    attackCallers.Add(Rel(path));

                if (string.Equals(leaf, "DragonBoss.cs", StringComparison.OrdinalIgnoreCase))
                    dragonBossText = text;
            }

            if (attachSites.Count == 0)
                fails.Add("NO '" + AttachLiteral + "' anywhere under Assets/_Modules/Village. This is " +
                          "the exact measurement the 2026-08-08 audit made when it caught commit " +
                          "4c1da079 routing around the owner's WIRE ruling with statics. The ruling " +
                          "was RECONFIRMED verbatim on 2026-08-21 ('874 wire ruling stands') - the " +
                          "controller must be genuinely attached on the elite/boss spawn path, not " +
                          "simulated by a static that plays the same VFXType.");
            else
                log.Append("attach x").Append(attachSites.Count)
                   .Append(" [").Append(string.Join(", ", attachSites.ToArray())).Append("]; ");

            // ── (2) THE ATTACK TELL HAS A CALLER ──────────────────────────────────
            if (attackCallers.Count == 0)
                fails.Add("'" + AttackMethod + "(' is called from NO file under Assets/_Modules/Village " +
                          "other than its own declaration. An attached-but-never-asked controller is " +
                          "the same dead behaviour the WIRE ruling exists to end: OnEliteAttack was " +
                          "one of the two things the static shortcut could not deliver.");
            else
                log.Append(AttackMethod).Append(" called from [")
                   .Append(string.Join(", ", attackCallers.ToArray())).Append("]; ");

            // ── (3) THE INSTANCE API IS REAL (reflection, not text) ───────────────
            Type controller = ResolveControllerType();
            if (controller == null)
            {
                fails.Add("type 'DeNelle.Village.EliteVFXController' did not resolve in any loaded " +
                          "assembly - the component the ruling names does not exist to attach.");
            }
            else
            {
                var arm = controller.GetMethod(ArmMethod,
                    BindingFlags.Public | BindingFlags.Instance);
                if (arm == null)
                    fails.Add("EliteVFXController has no PUBLIC INSTANCE '" + ArmMethod + "'. The attach " +
                              "seam needs an instance entry point that is re-callable on POOLED reuse " +
                              "(Start() runs once per pooled BODY, not once per enemy life). A static " +
                              "here would be the 4c1da079 shape returning.");
                else
                    log.Append(ArmMethod).Append(" public instance OK; ");

                var attack = controller.GetMethod(AttackMethod,
                    BindingFlags.Public | BindingFlags.Instance);
                if (attack == null)
                    fails.Add("EliteVFXController has no PUBLIC INSTANCE '" + AttackMethod + "'.");
            }

            // ── (4) THE DRAGON'S ENTRANCE (E8) ────────────────────────────────────
            if (dragonBossText == null)
                fails.Add("DragonBoss.cs not found under Assets/_Modules/Village - cannot measure the " +
                          "WO-874 E8 entrance.");
            else if (!dragonBossText.Contains("VFXType.Boss_Spawn"))
                fails.Add("DragonBoss.cs does not reference VFXType.Boss_Spawn. WO-874 E8: this boss " +
                          "plays a burst on every beat it has EXCEPT its arrival, and it is not an " +
                          "Enemy, so Enemy.Configure's attach seam never reaches it - the entrance has " +
                          "to be its own call.");
            else
                log.Append("DragonBoss Boss_Spawn entrance present; ");

            // ── (5) LADDER CATALOG COVERAGE - REPORTED, NEVER FAILED ─────────────
            string coverage = MeasureLadderCoverage(assets);
            log.Append(coverage);

            if (fails.Count > 0)
            {
                reason = "elite-vfx-wire FAIL (" + fails.Count + "): " +
                         string.Join(" | ", fails.ToArray());
                return false;
            }

            reason = "elite-vfx-wire OK - " + log;
            return true;
        }

        /// <summary>
        /// Reads VFXCatalogGenerator.cs and reports which Boss_*/Elite_* rows have a Map
        /// entry and which do not. REPORT ONLY - see (5) in the header for why an unmapped
        /// row must never be a failure.
        /// </summary>
        private static string MeasureLadderCoverage(string assets)
        {
            string gen = Path.Combine(assets, "Editor/VFXCatalogGenerator.cs");
            if (!File.Exists(gen))
                return "ladder coverage UNMEASURED (VFXCatalogGenerator.cs missing); ";

            string text;
            try { text = File.ReadAllText(gen); }
            catch (Exception e) { return "ladder coverage UNMEASURED (" + e.Message + "); "; }

            var mapped = new List<string>();
            var unmapped = new List<string>();
            foreach (string row in TierLadder)
            {
                if (text.Contains(MapRowShape(row))) mapped.Add(row);
                else unmapped.Add(row);
            }

            var sb = new StringBuilder();
            sb.Append("ladder catalog coverage ").Append(mapped.Count).Append('/').Append(TierLadder.Length);
            if (unmapped.Count > 0)
                sb.Append(" - AWAITING AN OWNER ART TAG: [")
                  .Append(string.Join(", ", unmapped.ToArray()))
                  .Append("] (these fall through to VFXManager's procedural nova/meteor burst. " +
                          "NOT a failure: the owner tags the prefab, the CLI maps it verbatim.)");
            sb.Append("; ");
            return sb.ToString();
        }

        private static Type ResolveControllerType()
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t;
                try { t = asm.GetType("DeNelle.Village.EliteVFXController", false); }
                catch { continue; }
                if (t != null) return t;
            }
            return null;
        }

        private static string Rel(string absolute)
        {
            int i = absolute.Replace('\\', '/').IndexOf("/Assets/", StringComparison.OrdinalIgnoreCase);
            return i >= 0 ? absolute.Replace('\\', '/').Substring(i + 1) : Path.GetFileName(absolute);
        }
    }
}
