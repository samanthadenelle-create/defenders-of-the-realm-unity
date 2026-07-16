// SetAppIcon — set the launcher/app icon to the Defenders of the Realm key art (owner 2026-07-16,
// "use this for the install image instead of the Unity symbol"). Bakes the icon into PlayerSettings
// for Android (APK launcher), Standalone, and the default group, so the next build ships it.
// Headless: run-unity-method.ps1 -Method DeNelle.Editor.SetAppIcon.Run -LogName set-app-icon.log
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class SetAppIcon
    {
        const string IconPath = "Assets/Branding/AppIcon.png";

        [MenuItem("Defenders/Branding/Set App Icon (key art)")]
        public static void Run()
        {
            // Make the source readable + uncompressed so Unity's icon pipeline can resize it cleanly.
            var imp = AssetImporter.GetAtPath(IconPath) as TextureImporter;
            if (imp != null)
            {
                imp.textureType = TextureImporterType.Default;
                imp.isReadable = true;
                imp.mipmapEnabled = false;
                imp.npotScale = TextureImporterNPOTScale.None;
                var s = imp.GetDefaultPlatformTextureSettings();
                s.format = TextureImporterFormat.RGBA32;
                s.textureCompression = TextureImporterCompression.Uncompressed;
                s.maxTextureSize = 1024;
                imp.SetPlatformTextureSettings(s);
                imp.SaveAndReimport();
            }

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (tex == null)
            {
                Debug.LogError("[SetAppIcon] APPICON_FAIL :: could not load " + IconPath);
                return;
            }

            int set = 0;
            foreach (var grp in new[] { BuildTargetGroup.Unknown, BuildTargetGroup.Android, BuildTargetGroup.Standalone })
            {
                try
                {
                    int[] sizes = PlayerSettings.GetIconSizesForTargetGroup(grp);
                    int n = (sizes != null && sizes.Length > 0) ? sizes.Length : 1;
                    var arr = new Texture2D[n];
                    for (int i = 0; i < n; i++) arr[i] = tex;
                    PlayerSettings.SetIconsForTargetGroup(grp, arr);
                    set++;
                    Debug.Log($"[SetAppIcon] set {n} icon slot(s) for {grp}.");
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[SetAppIcon] {grp} icon set skipped: {e.Message}");
                }
            }

            // Android adaptive icons (round/foreground) — API varies by Unity version and the legacy
            // icon above already drives the launcher icon on the phone; guarded so a signature change
            // never breaks the build. BuildTargetGroup overload is the stable one across 6000.x.
            try
            {
                var kinds = PlayerSettings.GetSupportedIconKindsForPlatform(BuildTargetGroup.Android);
                foreach (var kind in kinds)
                {
                    var icons = PlayerSettings.GetPlatformIcons(BuildTargetGroup.Android, kind);
                    if (icons == null) continue;
                    for (int i = 0; i < icons.Length; i++)
                        icons[i].SetTexture(tex, 0);
                    PlayerSettings.SetPlatformIcons(BuildTargetGroup.Android, kind, icons);
                    Debug.Log($"[SetAppIcon] Android adaptive kind set ({icons.Length} slot(s)).");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[SetAppIcon] Android adaptive icons skipped (legacy icon still applied): " + e.Message);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[SetAppIcon] APPICON_OK :: key art applied to {set} target group(s). Next build ships it.");
        }
    }
}
