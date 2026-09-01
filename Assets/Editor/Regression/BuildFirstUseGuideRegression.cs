using System.Collections.Generic;
using System.IO;
using UnityEngine;
using DeNelle.Village;

namespace DeNelle.Editor.Regression
{
    public static class BuildFirstUseGuideRegression
    {
        public static bool Run(out string report)
        {
            var failures = new List<string>();
            BuildFirstUseGuide.ResetForTests();
            try
            {
                if (BuildFirstUseGuide.Current != BuildFirstUseGuide.Step.Category)
                    failures.Add("guide does not begin at category selection");
                BuildFirstUseGuide.CategorySelected();
                BuildFirstUseGuide.ItemSelected();
                if (BuildFirstUseGuide.Current != BuildFirstUseGuide.Step.MoveGhost ||
                    BuildFirstUseGuide.Copy.IndexOf("Pinch in or out", System.StringComparison.Ordinal) < 0)
                    failures.Add("move step lacks the explicit pinch in/out phone hint");

                // Confirmation out of order must not persist completion.
                BuildFirstUseGuide.PlacementConfirmed();
                if (BuildFirstUseGuide.IsComplete)
                    failures.Add("guide completed before move and rotate actions");

                BuildFirstUseGuide.GhostMoved();
                BuildFirstUseGuide.Rotated();
                if (BuildFirstUseGuide.Current != BuildFirstUseGuide.Step.Confirm ||
                    BuildFirstUseGuide.Copy.IndexOf("check mark", System.StringComparison.OrdinalIgnoreCase) < 0)
                    failures.Add("rotate does not advance to the explicit check-mark instruction");
                BuildFirstUseGuide.PlacementConfirmed();
                if (!BuildFirstUseGuide.IsComplete || PlayerPrefs.GetInt(BuildFirstUseGuide.CompletionKey, 0) != 1)
                    failures.Add("successful ordered confirmation did not persist completion");

                string controller = File.ReadAllText("Assets/_Modules/Village/BuildMode/BuildModeController.cs");
                if (!controller.Contains("BuildFirstUseGuide.GhostMoved();") ||
                    !controller.Contains("BuildFirstUseGuide.Rotated();") ||
                    !controller.Contains("BuildFirstUseGuide.PlacementConfirmed();"))
                    failures.Add("live Build controller lost one or more real-action guide emitters");

                string browser = File.ReadAllText("Assets/_Modules/Village/BuildMode/BuildCollectionBrowser.cs");
                if (!browser.Contains("BuildFirstUseGuide.CategorySelected();") ||
                    !browser.Contains("Done(BuildFirstUseGuide.ItemSelected"))
                    failures.Add("collection browser lost category/item action emitters");
                if (BuildFirstUseGuide.Copy.Contains("...") || BuildFirstUseGuide.Copy.Contains("…"))
                    failures.Add("guide copy contains forbidden ellipsis");
            }
            finally
            {
                BuildFirstUseGuide.ResetForTests();
            }

            report = failures.Count == 0
                ? "BUILD_FIRST_USE_GUIDE_OK: category -> item -> move/pinch -> rotate -> check; completion persists only after confirm"
                : "BUILD_FIRST_USE_GUIDE_FAIL: " + string.Join(" | ", failures);
            return failures.Count == 0;
        }
    }
}
