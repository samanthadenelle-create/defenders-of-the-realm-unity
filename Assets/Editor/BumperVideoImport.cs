// =============================================================================
// BumperVideoImport — enables VideoClip transcoding on the studio-bumper clip.
// -----------------------------------------------------------------------------
// A non-transcoded source clip is shipped to the player as-is; a non-baseline
// H.264 then deadlocks the Windows video decoder (Media Foundation) and freezes
// the game on launch (see SplashLoading + docs/unity-decisions.md). Turning ON
// VideoClip transcoding makes Unity re-encode the clip at import time to a
// player-safe codec, so any source video plays without hanging.
//
// Run headless (transcode + build in one batchmode session):
//   Unity.exe -batchmode -quit -buildTarget Win64 -projectPath <proj> \
//     -executeMethod DeNelle.Editor.BumperVideoImport.TranscodeBumperAndBuild
// =============================================================================

using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>
    /// Editor utility: turns on VideoClip transcoding for the studio-bumper clip
    /// (Unity re-encodes it to a player-safe codec on import), then builds the
    /// Windows player.
    /// </summary>
    public static class BumperVideoImport
    {
        private const string BumperPath =
            "Assets/_Modules/Onboarding/Video/studio-bumper.mp4";

        /// <summary>Enables transcoding on the studio-bumper VideoClip and reimports it.</summary>
        [MenuItem("Defenders/Onboarding/Transcode Bumper Video")]
        public static void TranscodeBumper()
        {
            var importer = AssetImporter.GetAtPath(BumperPath) as VideoClipImporter;
            if (importer == null)
            {
                Debug.LogError($"[BumperVideoImport] No VideoClipImporter at {BumperPath} " +
                               "— is the studio bumper present?");
                return;
            }

            VideoImporterTargetSettings s = importer.defaultTargetSettings;
            s.enableTranscoding = true;
            s.codec = VideoCodec.Auto;            // Unity picks a player-safe codec per platform
            s.bitrateMode = VideoBitrateMode.High;
            importer.defaultTargetSettings = s;

            importer.SaveAndReimport();
            Debug.Log($"[BumperVideoImport] Transcoding ON + reimported: {BumperPath}");
        }

        /// <summary>Transcodes the bumper clip, then builds the Windows player.</summary>
        public static void TranscodeBumperAndBuild()
        {
            TranscodeBumper();
            DesktopBuild.BuildWindows();
        }
    }
}
