// =============================================================================
// HudPostureRegression — headless pursuit-pulse lifecycle oracle (HUD flip contract).
// -----------------------------------------------------------------------------
// Proves PostureSignals.RevokePursuit drops one threat while others keep combat HUD
// armed, and ClearPursuits returns to peaceful — the owner rule:
//   town calm when safe · combat when pursued · calm when last threat dies.
// =============================================================================

using System.Text;
using DeNelle.Core.HudModel;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class HudPostureRegression
    {
        public static bool Run(out string summary)
        {
            var log = new StringBuilder();
            log.AppendLine("[hud-posture] pursuit pulse lifecycle:");

            PostureSignals.ClearPursuits();

            PostureSignals.ReportPursuit(101);
            PostureSignals.ReportPursuit(202);
            if (!PostureSignals.PursuitActive)
            {
                summary = "two ReportPursuit calls did not open PursuitActive";
                return false;
            }
            log.AppendLine("  two pursuers -> PursuitActive=true OK");

            PostureSignals.RevokePursuit(101);
            if (!PostureSignals.PursuitActive)
            {
                summary = "RevokePursuit(101) cleared window while 202 still live";
                return false;
            }
            log.AppendLine("  revoke one of two -> still active OK");

            PostureSignals.RevokePursuit(202);
            if (PostureSignals.PursuitActive)
            {
                summary = "RevokePursuit(202) left PursuitActive true with zero pursuers";
                return false;
            }
            log.AppendLine("  revoke last -> PursuitActive=false OK");

            PostureSignals.ReportPursuit(303);
            PostureSignals.ClearPursuits();
            if (PostureSignals.PursuitActive)
            {
                summary = "ClearPursuits did not drop PursuitActive";
                return false;
            }
            log.AppendLine("  ClearPursuits -> peaceful OK");

            summary = log.ToString().TrimEnd();
            return true;
        }
    }
}