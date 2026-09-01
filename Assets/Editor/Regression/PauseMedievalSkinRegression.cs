#if UNITY_EDITOR
using System;
using System.IO;

namespace DeNelle.Editor.Regression
{
    /// <summary>Locks the approved compact Pause composition and shared skin seam.</summary>
    public static class PauseMedievalSkinRegression
    {
        public static bool Run(out string result)
        {
            try
            {
                string pause = File.ReadAllText("Assets/_Modules/Settings/PauseController.cs");
                Require(pause, "MedievalUiSkin.ApplyShell(_modal.chrome, compact: true)");
                Require(pause, "AspectRatioFitter.AspectMode.HeightControlsWidth");
                Require(pause, "\"Resume\"");
                Require(pause, "\"Settings\"");
                Require(pause, "\"Quit to Title\"");
                Require(pause, "MedievalUiSkin.ApplyButton(resume, primary: true)");
                Require(pause, "WorldHold.Acquire(WorldHold.ReasonPauseMenu)");
                Forbid(pause, "BuildButtonColumn(body");

                string skin = File.ReadAllText("Assets/_Modules/Core/UI/MedievalUiSkin.cs");
                Require(skin, "Image.Type.Sliced");
                Require(skin, "buttons/button-disabled-empty");
                Require(skin, "buttons/close-ornate");
                Require(skin, "bool authoredLabel");
                Require(skin, "GetComponentsInChildren<TMP_Text>(true)");
                Require(skin, "label.gameObject.SetActive(false)");

                string buttons = File.ReadAllText("Assets/_Modules/Core/UI/ElarionUiKitObsidian.cs");
                Require(buttons, "CanonicalizeButtonLabels");
                Require(buttons, "GetComponentsInChildren<TMP_Text>(true)");
                Require(buttons, "candidate.gameObject.SetActive(false)");

                result = "Pause uses the compact medieval shell, the baked Close label is the sole authority, three approved actions, and authoritative WorldHold.";
                return true;
            }
            catch (Exception ex)
            {
                result = ex.Message;
                return false;
            }
        }

        private static void Require(string source, string token)
        {
            if (source.IndexOf(token, StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("Pause reskin contract missing: " + token);
        }

        private static void Forbid(string source, string token)
        {
            if (source.IndexOf(token, StringComparison.Ordinal) >= 0)
                throw new InvalidOperationException("Legacy Pause layout returned: " + token);
        }
    }
}
#endif
