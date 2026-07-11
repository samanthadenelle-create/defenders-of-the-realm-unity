// =============================================================================
// UnderConstructionVisual — scaffold state for a structure with a live build
// timer (WO-612, wires WO-172 BuildTimerService into placement).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// While BuildTimerService.IsBuilding(key): renderers are dimmed, the DefenseTower
// behavior is disabled (a tower under construction does not fire), and a small
// world-space countdown floats above the piece. On JobCompleted (or the offline
// sweep having already finished the job) the visual reveals: colors restore,
// behavior re-enables, this component removes itself.
//
// Self-healing: reveal is driven BOTH by the JobCompleted event and by an
// IsBuilding() check each Update — a missed event (scene churn, load order)
// can delay the reveal by at most one frame, never strand a ghost scaffold.
// =============================================================================

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.State;

namespace DeNelle.Village
{
    /// <summary>
    /// Scaffold state for a structure whose construction timer is running
    /// (WO-612). Attach via <see cref="Attach"/>; it reveals and removes itself
    /// when the job completes.
    /// </summary>
    public sealed class UnderConstructionVisual : MonoBehaviour
    {
        private const float DimFactor = 0.45f;   // scaffold grey-down of the albedo

        private string _key;
        private readonly List<Renderer> _renderers = new List<Renderer>();
        private readonly List<Color[]> _originalColors = new List<Color[]>();
        private Behaviour _disabledTower;
        private TextMeshPro _label;

        /// <summary>Job key for a placed structure — unique per placement (id + cell).</summary>
        public static string KeyFor(PlacedStructureData data)
            => $"{data.itemId}@{data.cellX}_{data.cellZ}";

        /// <summary>Attach the scaffold to a freshly placed / freshly loaded structure.</summary>
        public static void Attach(PlacedStructure ps, string key)
        {
            if (ps == null || string.IsNullOrEmpty(key)) return;
            if (ps.GetComponent<UnderConstructionVisual>() != null) return;   // already scaffolded
            ps.gameObject.AddComponent<UnderConstructionVisual>().Bind(key);
        }

        private void Bind(string key)
        {
            _key = key;
            Guard.Try("Build", "scaffold dim", () =>
            {
                GetComponentsInChildren(true, _renderers);
                foreach (var r in _renderers)
                {
                    var mats = r.materials;   // instances — safe to tint per-structure
                    var saved = new Color[mats.Length];
                    for (int i = 0; i < mats.Length; i++)
                    {
                        if (!mats[i].HasProperty("_BaseColor") && !mats[i].HasProperty("_Color")) continue;
                        string prop = mats[i].HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
                        saved[i] = mats[i].GetColor(prop);
                        Color c = saved[i] * DimFactor;
                        c.a = saved[i].a;
                        mats[i].SetColor(prop, c);
                    }
                    _originalColors.Add(saved);
                }
            });

            // A tower under construction does not fire (walls keep blocking — scaffold
            // is still physically there; only the ACTIVE behavior pauses).
            _disabledTower = GetComponentInChildren<DefenseTower>(true);
            if (_disabledTower != null) _disabledTower.enabled = false;

            Guard.Try("Build", "scaffold label", () =>
            {
                var go = new GameObject("BuildCountdown");
                go.transform.SetParent(transform, false);
                float top = 2.5f;
                var rend = GetComponentInChildren<Renderer>();
                if (rend != null) top = rend.bounds.size.y + 1.2f;
                go.transform.localPosition = new Vector3(0f, top, 0f);
                _label = go.AddComponent<TextMeshPro>();
                _label.fontSize = 4f;
                _label.alignment = TextAlignmentOptions.Center;
                _label.color = new Color(1f, 0.85f, 0.4f);   // kit gold
                _label.text = "…";
            });

            var svc = BuildTimerService.Instance;
            if (svc != null) svc.JobCompleted += OnJobCompleted;
            FlowTrace.Step("Build", $"under-construction armed for '{_key}'");
        }

        /// <summary>
        /// F8-51: the job key is cell-derived, so a structure MOVED mid-timer re-keys this
        /// scaffold (paired with BuildTimerService.RepointJob) — otherwise the self-heal
        /// poll sees IsBuilding(oldKey)==false and reveals early while the job still runs.
        /// </summary>
        public void Rekey(string newKey)
        {
            if (!string.IsNullOrEmpty(newKey)) _key = newKey;
        }

        private void Update()
        {
            var svc = BuildTimerService.Instance;
            if (svc == null || !svc.IsBuilding(_key)) { Reveal(); return; }   // self-heal

            if (_label != null)
            {
                double s = svc.RemainingSeconds(_key);
                _label.text = s >= 60 ? $"{(int)(s / 60)}:{(int)(s % 60):00}" : $"{(int)s}s";
                var cam = Camera.main;
                if (cam != null)
                    _label.transform.rotation = Quaternion.LookRotation(
                        _label.transform.position - cam.transform.position);
            }
        }

        private void OnJobCompleted(BuildJobData job)
        {
            if (job.StructureId == _key) Reveal();
        }

        private void Reveal()
        {
            Guard.Try("Build", "scaffold reveal", () =>
            {
                for (int i = 0; i < _renderers.Count && i < _originalColors.Count; i++)
                {
                    if (_renderers[i] == null) continue;
                    var mats = _renderers[i].materials;
                    var saved = _originalColors[i];
                    for (int m = 0; m < mats.Length && m < saved.Length; m++)
                    {
                        if (!mats[m].HasProperty("_BaseColor") && !mats[m].HasProperty("_Color")) continue;
                        string prop = mats[m].HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
                        if (saved[m] != default) mats[m].SetColor(prop, saved[m]);
                    }
                }
                if (_disabledTower != null) _disabledTower.enabled = true;
                if (_label != null) Destroy(_label.gameObject);
            });
            FlowTrace.Step("Build", $"construction complete — revealed '{_key}'");
            Destroy(this);
        }

        private void OnDestroy()
        {
            var svc = BuildTimerService.Instance;
            if (svc != null) svc.JobCompleted -= OnJobCompleted;
        }
    }
}
