#if UNITY_EDITOR
using System;
using System.IO;

namespace DeNelle.Editor.Regression
{
    /// <summary>Locks class-authored primary attacks and the Knight's held shield pose.</summary>
    public static class ClassPrimaryAndKnightBlockRegression
    {
        public static bool Run(out string reason)
        {
            try
            {
                string bridge = File.ReadAllText("Assets/_Modules/Village/HUD/HudKitCommandBridge.cs");
                Require(bridge, "string.Equals(heroClass, \"mage\"");
                Require(bridge, "string.Equals(heroClass, \"ranger\"");
                Require(bridge, "abilities.TryCast(AbilitySlot.Q)");
                Require(bridge, "atk.TriggerBasicAttack()");

                string hud = File.ReadAllText("Assets/_Modules/HUD/Kit/HudKitController.cs");
                Require(hud, "var q = a.Slots[0]");
                Require(hud, "primary.SetIcon");
                Require(hud, "primary.SetCaption");

                string factory = File.ReadAllText("Assets/Editor/HeroAnimatorFactory.cs");
                Require(factory, "blockClipOverride = \"sword and shield block idle\"");
                Require(factory, "AddCondition(AnimatorConditionMode.If, 0f, \"Block\")");
                Require(factory, "AddCondition(AnimatorConditionMode.IfNot, 0f, \"Block\")");

                string skip = File.ReadAllText("Assets/_Modules/Core/UI/TutorialSkipUi.cs");
                Require(skip, "face.raycastTarget = true");

                string ui = File.ReadAllText("Assets/_Modules/Core/UI/ElarionUiKit.cs");
                Require(ui, "MedallionBounds");
                Require(ui, "AspectRatioFitter.AspectMode.FitInParent");
                Require(ui, "const float iconInset = 0.14f");
                Require(ui, "maskGo.transform.SetParent(medallionBounds");
                Require(ui, "bezelGo.transform.SetParent(medallionBounds");

                reason = "CLASS_PRIMARY_BLOCK_OK: class primaries, Knight shield pose, Skip raycast, and square circular-medallion bounds are locked.";
                return true;
            }
            catch (Exception ex)
            {
                reason = "CLASS_PRIMARY_BLOCK_FAIL: " + ex.Message;
                return false;
            }
        }

        private static void Require(string source, string token)
        {
            if (source.IndexOf(token, StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("missing contract: " + token);
        }
    }
}
#endif
