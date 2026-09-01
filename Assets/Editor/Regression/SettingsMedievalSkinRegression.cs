#if UNITY_EDITOR
using System;
using System.IO;

namespace DeNelle.Editor.Regression
{
    /// <summary>Locks the approved medieval Settings shell without changing live persistence.</summary>
    public static class SettingsMedievalSkinRegression
    {
        public static bool Run(out string result)
        {
            try
            {
                string source = File.ReadAllText("Assets/_Modules/Settings/SettingsController.cs");
                Require(source, "MedievalUiSkin.ApplyShell(_modal.chrome)");
                Require(source, "ApplyMedievalPresentation()");
                Require(source, "UI/ElarionMedieval/progress/progress-track-empty");
                Require(source, "UI/ElarionMedieval/frames/circular-bezel-four-point");
                Require(source, "UI/ElarionMedieval/frames/square-icon-frame");
                Require(source, "string.Equals(button.gameObject.name, \"Scrim\"");
                Require(source, "PanelManager.RegisterBattleAllowed(\"Settings\"");
                Require(source, "RefreshFromModel();");
                Forbid(source, "new GameObject(\"TitleBacking\"");

                result = "Settings uses one medieval shell, skinned buttons/sliders/toggles, excludes its invisible scrim from bulk styling, and retains live model refresh.";
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
                throw new InvalidOperationException("Settings medieval contract missing: " + token);
        }

        private static void Forbid(string source, string token)
        {
            if (source.IndexOf(token, StringComparison.Ordinal) >= 0)
                throw new InvalidOperationException("Legacy Settings presentation returned: " + token);
        }
    }
}
#endif
