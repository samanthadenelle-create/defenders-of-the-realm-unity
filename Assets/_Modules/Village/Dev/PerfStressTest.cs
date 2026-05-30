// =============================================================================
// PerfStressTest — generic validation object for the engine's scaling claim.
// -----------------------------------------------------------------------------
// The architecture bets that a build game's massive repetition is CHEAP because
// identical catalog parts GPU-instance (1000 walls ~ a few draw batches). Don't
// trust that — measure it. This spawns N copies of the SIMPLEST generic engine
// object (one shared mesh + one instanced material, same as a catalog cell) and
// reports live FPS / memory / count, so we find the real mobile threshold + prove
// it doesn't create problems (draw-call explosions, leaks, frame cliffs).
//
// Dev-only (DEVELOPMENT_BUILD / editor). Self-installs DORMANT (0 objects, 0 cost).
//   F9  = spawn +1000     F10 = clear
// Read the on-screen + logged FPS as you ramp; in the editor, the Stats window's
// "Batches" confirms instancing is collapsing the draws.
// =============================================================================
#if DEVELOPMENT_BUILD || UNITY_EDITOR
using UnityEngine;
using UnityEngine.InputSystem;

namespace DeNelle.Village
{
    public sealed class PerfStressTest : MonoBehaviour
    {
        private const int Batch = 1000;

        private int _count;
        private Mesh _mesh;
        private Material _mat;
        private Transform _root;
        private float _fps;
        private float _accum;
        private int _frames;
        private float _timer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindObjectOfType<PerfStressTest>() != null) return;
            var go = new GameObject("[PerfStressTest]");
            DontDestroyOnLoad(go);
            go.AddComponent<PerfStressTest>();
            Debug.Log("[PerfStressTest] installed (dormant). F9 = +1000 generic instanced objects, F10 = clear.");
        }

        private void Awake()
        {
            // The generic object: a primitive cube mesh + ONE instanced material — the
            // cheapest thing the engine could create, and the worst case for draw calls
            // (one renderer each), so if THIS scales, catalog parts scale.
            var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _mesh = probe.GetComponent<MeshFilter>().sharedMesh;
            Destroy(probe);

            var sh = Shader.Find("Universal Render Pipeline/Lit");
            _mat = new Material(sh) { name = "PerfInstanced", enableInstancing = true };
            if (_mat.HasProperty("_BaseColor")) _mat.SetColor("_BaseColor", new Color(0.35f, 0.70f, 1f));

            _root = new GameObject("PerfObjects").transform;
        }

        private void Spawn(int n)
        {
            for (int i = 0; i < n; i++)
            {
                int idx = _count + i;
                float x = -40f + (idx % 80);
                float z = -40f + ((idx / 80) % 80);
                float y = 1f + (idx / 6400) * 1.5f;   // stack layers after the 80x80 grid fills

                var go = new GameObject("p");
                go.transform.SetParent(_root, false);
                go.transform.position = new Vector3(x, y, z);
                go.AddComponent<MeshFilter>().sharedMesh = _mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = _mat;
            }
            _count += n;
            long mem = System.GC.GetTotalMemory(false) / (1024 * 1024);
            Debug.Log($"[PerfStressTest] objects={_count}  fps={_fps:F0}  managedMem~={mem}MB " +
                      "(shared mesh + instanced mat — editor Stats 'Batches' shows draw collapse)");
        }

        private void Update()
        {
            _accum += 1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
            _frames++;
            _timer += Time.unscaledDeltaTime;
            if (_timer >= 0.5f) { _fps = _accum / Mathf.Max(1, _frames); _accum = 0f; _frames = 0; _timer = 0f; }

            bool spawn = false, clear = false;

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame ||
                    kb.spaceKey.wasPressedThisFrame || kb.f9Key.wasPressedThisFrame) spawn = true;
                if (kb.backspaceKey.wasPressedThisFrame || kb.f10Key.wasPressedThisFrame) clear = true;
            }
            // Legacy fallback (project's activeInputHandler = Both) in case the new-system
            // device singletons aren't populated in this build.
            if (UnityEngine.Input.GetKeyDown(KeyCode.Return) || UnityEngine.Input.GetKeyDown(KeyCode.KeypadEnter) ||
                UnityEngine.Input.GetKeyDown(KeyCode.Space)  || UnityEngine.Input.GetKeyDown(KeyCode.F9)) spawn = true;
            if (UnityEngine.Input.GetKeyDown(KeyCode.Backspace) || UnityEngine.Input.GetKeyDown(KeyCode.F10)) clear = true;

            if (spawn) Spawn(Batch);
            if (clear)
            {
                for (int i = _root.childCount - 1; i >= 0; i--) Destroy(_root.GetChild(i).gameObject);
                _count = 0;
                Debug.Log("[PerfStressTest] cleared.");
            }
        }

        private void OnGUI()
        {
            var s = new GUIStyle(GUI.skin.label) { fontSize = 22, normal = { textColor = Color.white } };
            GUI.Label(new Rect(14, 90, 760, 80),
                $"PERF STRESS   objects = {_count:N0}   fps = {_fps:F0}\nENTER / SPACE / F9 = +{Batch}     BACKSPACE / F10 = clear", s);
        }
    }
}
#endif
