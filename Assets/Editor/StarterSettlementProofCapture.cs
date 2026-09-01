using System;
using System.IO;
using System.Reflection;
using DeNelle.Core.State;
using DeNelle.Village;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Editor
{
    public static class StarterSettlementProofCapture
    {
        private const string Request = "starter.proof.request";
        private const string Phase = "starter.proof.phase";
        private const string HadSave = "starter.proof.hadsave";
        private const string SaveCopy = "starter.proof.savecopy";
        private static double _deadline;
        private static double _settledAt;

        [InitializeOnLoadMethod]
        private static void ArmAfterReload()
        {
            EditorApplication.playModeStateChanged -= OnPlayMode;
            EditorApplication.playModeStateChanged += OnPlayMode;
            if (SessionState.GetBool(Request, false) && EditorApplication.isPlaying)
                ArmTick();
        }

        public static void Run()
        {
            string key = SaveSchema.PlayerPrefsKey;
            SessionState.SetBool(HadSave, PlayerPrefs.HasKey(key));
            SessionState.SetString(SaveCopy, PlayerPrefs.GetString(key, ""));
            SessionState.SetBool(Request, true);
            SessionState.SetString(Phase, "inject");
            string scene = "Assets/Scenes/Main_Castle_Overworld.unity";
            EditorSceneManager.OpenScene(scene, OpenSceneMode.Single);
            Debug.Log("STARTER_SETTLEMENT_PROOF_BEGIN isolated throwaway state");
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayMode(PlayModeStateChange change)
        {
            if (!SessionState.GetBool(Request, false)) return;
            if (change == PlayModeStateChange.EnteredPlayMode) ArmTick();
            if (change == PlayModeStateChange.EnteredEditMode)
            {
                RestoreSave();
                SessionState.SetBool(Request, false);
                EditorApplication.Exit(0);
            }
        }

        private static void ArmTick()
        {
            _deadline = EditorApplication.timeSinceStartup + 90d;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying) return;
            if (EditorApplication.timeSinceStartup > _deadline)
            { Finish(false, "timeout waiting for starter settlement"); return; }

            string phase = SessionState.GetString(Phase, "inject");
            var svc = GameStateService.Instance;
            if (svc == null || svc.State == null) return;

            if (phase == "inject")
            {
                var state = ScriptableObject.CreateInstance<GameState>();
                state.BaseLayout = new System.Collections.Generic.List<PlacedStructureData>();
                state.EverBuiltStructureIds = new System.Collections.Generic.List<string>();
                state.StrategicPlacementMigrated = false;
                state.Onboarded = false;
                state.EchoCount = 1;
                var field = typeof(GameStateService).GetField("_state", BindingFlags.NonPublic | BindingFlags.Instance);
                if (field == null) { Finish(false, "GameStateService._state seam missing"); return; }
                field.SetValue(svc, state);
                svc.MarkTutorialSeen(StarterSettlementCompletion.SelectedKey);
                SessionState.SetString(Phase, "wait");
                SceneManager.LoadScene("Main_Castle_Overworld");
                return;
            }

            bool complete = svc.State.SeenTutorials != null &&
                svc.State.SeenTutorials.TryGetValue(StarterSettlementCompletion.CompletedKey, out bool seen) && seen;
            if (!complete) return;
            if (_settledAt <= 0d) { _settledAt = EditorApplication.timeSinceStartup + 3d; return; }
            if (EditorApplication.timeSinceStartup < _settledAt) return;

            int towers = Count(svc.State, "tower_ground_archer");
            string[] essentials = { "workshop", "collector_forge", "lumberyard", "foundry", "silo" };
            for (int i = 0; i < essentials.Length; i++)
                if (Count(svc.State, essentials[i]) < 1)
                { Finish(false, "missing BaseLayout record " + essentials[i]); return; }
            if (towers != 4) { Finish(false, "expected 4 Archer Towers, got " + towers); return; }

            string capturePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "starter-settlement-proof.png"));
            if (!CaptureWorld(Camera.main, capturePath, out string captureError))
            { Finish(false, captureError); return; }
            var fi = new FileInfo(capturePath);
            if (!fi.Exists || fi.Length < 4096) { Finish(false, "screenshot was blank/truncated"); return; }
            Debug.Log("STARTER_SETTLEMENT_PROOF_OK essentials=5 towers=4 layout=" +
                      svc.State.BaseLayout.Count + " screenshot=" + capturePath +
                      " bytes=" + fi.Length);
            Finish(true, "complete");
        }

        private static bool CaptureWorld(Camera camera, string path, out string error)
        {
            error = null;
            if (camera == null) { error = "no main camera available for proof capture"; return false; }
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            var target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
                return true;
            }
            catch (Exception ex)
            {
                error = "camera capture failed: " + ex.Message;
                return false;
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                UnityEngine.Object.DestroyImmediate(image);
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static int Count(GameState state, string id)
        {
            int n = 0;
            if (state.BaseLayout != null)
                for (int i = 0; i < state.BaseLayout.Count; i++) if (state.BaseLayout[i].itemId == id) n++;
            return n;
        }

        private static void Finish(bool ok, string reason)
        {
            EditorApplication.update -= Tick;
            if (!ok) Debug.LogError("STARTER_SETTLEMENT_PROOF_FAIL " + reason);
            SessionState.SetString(Phase, ok ? "done" : "failed");
            EditorApplication.ExitPlaymode();
        }

        private static void RestoreSave()
        {
            string key = SaveSchema.PlayerPrefsKey;
            if (SessionState.GetBool(HadSave, false)) PlayerPrefs.SetString(key, SessionState.GetString(SaveCopy, ""));
            else PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
            Debug.Log("STARTER_SETTLEMENT_PROOF_SAVE_RESTORED");
        }
    }
}
