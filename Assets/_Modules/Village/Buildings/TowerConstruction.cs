// =============================================================================
// TowerConstruction — DEF-76 (Linear). Drives one tower's build: raises
// scaffolding + a worker VFX + a world-space progress bar, runs a build timer,
// and reveals the finished tower on completion.
// -----------------------------------------------------------------------------
// namespace DeNelle.Village. DEF-76 Correction Pass 1 applied:
//   • StartConstruction(TowerData) is PUBLIC — called by TowerConstructionQueue
//     (Issue 9). It resolves its Tower via GetComponent (same GameObject).
//   • _data null guard at the top of Update() (CP1).
//   • The final-tower visual is toggled via a dedicated _finalTowerVisual
//     reference, NEVER `foreach (Transform child in transform) SetActive(false)`
//     (Issue 10). In this project's runtime path there is no baked final visual
//     (no tower prefab — see TowerConstructionQueue header), so the reveal is done
//     by calling Tower.Initialize() in CompleteConstruction; the _finalTowerVisual
//     SetActive ordering is kept (null-safe) for any future prefab-baked tower.
//   • _progressBar is a [SerializeField]-style cached reference created once in
//     StartConstruction — never GetComponent'd in Update (DEF-76 ProgressBar note).
// =============================================================================

using UnityEngine;
using DeNelle.Core.Data;
using DeNelle.Village.UI;

namespace DeNelle.Village
{
    /// <summary>Runs a single tower's timed construction and reveals it when done.</summary>
    public class TowerConstruction : MonoBehaviour
    {
        // Prefab-baked final visual to hide during the build (null in the runtime
        // path, where Tower.Initialize builds the visual on completion instead).
        [SerializeField] private GameObject _finalTowerVisual;

        private TowerData _data;
        private Tower _tower;
        private GameObject _scaffolding;
        private ParticleSystem _workerVfx;
        private ProgressBar _progressBar;
        private float _elapsed;
        private bool _complete;

        /// <summary>True once the tower has finished building.</summary>
        public bool IsComplete => _complete;

        /// <summary>
        /// Begin construction for <paramref name="data"/>. Public — invoked by
        /// TowerConstructionQueue.BuildTowerRoutine right after the components are added.
        /// </summary>
        public void StartConstruction(TowerData data)
        {
            _data = data;
            _tower = GetComponent<Tower>();
            _elapsed = 0f;
            _complete = false;

            // Hide ONLY the final tower visual — never iterate children (CP1 Issue 10).
            if (_finalTowerVisual != null) _finalTowerVisual.SetActive(false);

            BuildScaffolding();
            StartWorkerVfx();
            _progressBar = ProgressBar.Create(transform, Vector3.up * 2.5f);
            if (_progressBar != null) _progressBar.SetProgress(0f);
        }

        private void Update()
        {
            if (_data == null || _complete) return;   // _data null guard (CP1)

            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / Mathf.Max(0.01f, _data.buildTime));
            if (_progressBar != null) _progressBar.SetProgress(t);

            if (t >= 1f) CompleteConstruction();
        }

        /// <summary>Reveal the finished tower and tear down the build dressing.</summary>
        public void CompleteConstruction()
        {
            if (_complete) return;
            _complete = true;

            // Show ONLY the final tower visual (CP1 Issue 10). In the runtime path
            // there is no baked visual, so Initialize() builds + reveals it now.
            if (_finalTowerVisual != null) _finalTowerVisual.SetActive(true);
            if (_tower != null && _tower.Data == null && _data != null) _tower.Initialize(_data);
            var audio = FindAnyObjectByType<TowerAudioController>();
            if (audio != null) audio.PlayBuildComplete();

            if (_scaffolding != null) Destroy(_scaffolding);
            if (_workerVfx != null) Destroy(_workerVfx.gameObject);
            if (_progressBar != null) { Destroy(_progressBar.gameObject); _progressBar = null; }
        }

        // --- Build dressing (authored prefab, or a procedural placeholder) -------

        private void BuildScaffolding()
        {
            if (_data != null && _data.scaffoldingPrefab != null)
            {
                _scaffolding = Instantiate(_data.scaffoldingPrefab, transform);
                _scaffolding.transform.localPosition = Vector3.zero;
                _scaffolding.transform.localRotation = Quaternion.identity;
                return;
            }

            // Code placeholder: a low wooden-brown base frame so the site reads as
            // "under construction" even with no authored scaffolding art.
            _scaffolding = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _scaffolding.name = "Scaffolding";
            _scaffolding.transform.SetParent(transform, false);
            _scaffolding.transform.localScale = new Vector3(1.4f, 0.4f, 1.4f);
            _scaffolding.transform.localPosition = new Vector3(0f, 0.2f, 0f);

            var col = _scaffolding.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var rend = _scaffolding.GetComponent<Renderer>();
            if (rend != null)
            {
                Shader sh = Shader.Find("Universal Render Pipeline/Lit")
                            ?? Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
                var mat = new Material(sh);
                var brown = new Color(0.45f, 0.30f, 0.15f);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", brown);
                else mat.color = brown;
                rend.sharedMaterial = mat;
            }
        }

        private void StartWorkerVfx()
        {
            if (_data == null || _data.workerHammerVFX == null) return;
            _workerVfx = Instantiate(_data.workerHammerVFX, transform);
            _workerVfx.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            _workerVfx.Play();
        }
    }
}
