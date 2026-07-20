// =============================================================================
// EchoCardCopyRegression [echo-card-copy] -- proves the first-Echo card reads as an
// AWAKENING, not a nonsensical "Leveled Up to 1".
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression. Drives the REAL, pure copy seam
// EchoUnlockDialogue.HeaderFor(int) (reflected -- EchoUnlockDialogue is in
// DeNelle.Village; the method is public + pure precisely so this copy is headlessly
// assertable). newCount==1 is the FOUNDING awaken (no prior Echo to level from), so
// the header must read an awaken line ("waken") and NEVER "Leveled Up to 1"; n>=2 is
// a genuine level-up ("Leveled Up to N").
//
// Marker: ECHO_CARD_COPY_OK / ECHO_CARD_COPY_FAIL. Expected: GREEN (copy fix landed).
// If HeaderFor is ever removed the oracle FAILS-BY-DESIGN naming the missing seam.
//
// Wire (DataRegression.RunAll):
//   if (!EchoCardCopyRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[echo-card-copy] " + r);
// =============================================================================
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class EchoCardCopyRegression
    {
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- ECHO CARD COPY (EchoUnlockDialogue.HeaderFor: awaken@1, level-up@2) ---");

            var t = FindType("DeNelle.Village.EchoUnlockDialogue");
            if (t == null)
            {
                failures.Add("EchoUnlockDialogue type not found");
                reason = Finish(failures, log);
                return failures.Count == 0;
            }
            var headerFor = t.GetMethod("HeaderFor", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(int) }, null);
            if (headerFor == null)
            {
                failures.Add("[echo-card-copy] FAIL-BY-DESIGN: EchoUnlockDialogue.HeaderFor(int) does not exist -- extract the header into a pure HeaderFor(newCount) that returns an awaken line for count 1 (not 'Echo Leveled Up to 1!'). This oracle flips green once it exists and reads correctly.");
                reason = Finish(failures, log);
                return failures.Count == 0;
            }

            try
            {
                string h1 = headerFor.Invoke(null, new object[] { 1 }) as string ?? "";
                string h2 = headerFor.Invoke(null, new object[] { 2 }) as string ?? "";
                log.AppendLine($"  HeaderFor(1)='{h1}'  HeaderFor(2)='{h2}'");

                if (h1.IndexOf("waken", StringComparison.OrdinalIgnoreCase) < 0)
                    failures.Add($"[echo-card-copy] HeaderFor(1)='{h1}' does not read as an awakening (no 'waken') -- the first Echo is a founding awaken, not a level-up");
                if (h1.IndexOf("Leveled Up to 1", StringComparison.OrdinalIgnoreCase) >= 0)
                    failures.Add($"[echo-card-copy] HeaderFor(1)='{h1}' still says 'Leveled Up to 1' -- nonsensical for the first Echo");
                if (h2.IndexOf("Leveled Up to 2", StringComparison.OrdinalIgnoreCase) < 0)
                    failures.Add($"[echo-card-copy] HeaderFor(2)='{h2}' does not read 'Leveled Up to 2' -- the second Echo IS a genuine level-up");
            }
            catch (Exception ex)
            {
                failures.Add($"[echo-card-copy] HeaderFor invoke threw: {ex.GetType().Name}: {ex.Message}");
            }

            reason = Finish(failures, log);
            return failures.Count == 0;
        }

        private static Type FindType(string full)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(full, false);
                if (t != null) return t;
            }
            return null;
        }

        private static string Finish(List<string> failures, StringBuilder log)
        {
            if (failures.Count == 0)
            {
                Debug.Log(log.ToString() + "ECHO_CARD_COPY_OK");
                return "ECHO CARD COPY OK -- HeaderFor(1) reads an awaken line, HeaderFor(2) reads 'Leveled Up to 2'";
            }
            string reason = "echo-card-copy: " + string.Join("; ", failures);
            Debug.LogError(log.ToString() + "ECHO_CARD_COPY_FAIL: " + reason);
            return reason;
        }
    }
}
