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

        // Owner 2026-07-24: the CALM "work in progress" tell — the owner-tagged
        // "UpgradeVisual_Aura" (a slowly-circling orb) held as ONE persistent loop parented to
        // the structure WHILE the build/upgrade timer runs. Handle-managed through the single
        // VFXManager Hovl pool; Stop()'d in Reveal (completion) + OnDestroy (cancel/teardown) so
        // it never lingers under the completion fireworks and never leaks a loop slot.
        private VFXHandle _upgradeLoop;

        /// <summary>Job key for a placed structure — unique per placement (id + cell).</summary>
        public static string KeyFor(PlacedStructureData data)
            => $"{data.itemId}@{data.cellX}_{data.cellZ}";

        /// <summary>Attach the scaffold to a freshly placed / freshly loaded structure.</summary>
        public static void Attach(PlacedStructure ps, string key)
            => Attach(ps != null ? ps.gameObject : null, key);

        /// <summary>
        /// Attach the scaffold + world-space countdown to ANY structure/building GameObject
        /// (F8 owner 2026-07-17). Idempotent — a host already scaffolded is a no-op. Used by
        /// placement (through the <see cref="PlacedStructure"/> overload) and by the building
        /// -upgrade seams (through <see cref="AttachToBuildingId"/>).
        /// </summary>
        public static void Attach(GameObject host, string key)
        {
            if (host == null || string.IsNullOrEmpty(key)) return;
            if (host.GetComponent<UnderConstructionVisual>() != null) return;   // already scaffolded
            host.AddComponent<UnderConstructionVisual>().Bind(key);
        }

        /// <summary>
        /// F8 (owner 2026-07-17 "an upgrade timer that doesn't tell"): show the CoC-style
        /// on-building countdown for a CITY / RESOURCE building upgrade. Those upgrades run
        /// through the tabbed MVVM panel / Yarn (BuildingUpgradeService / ResourceBuildingState),
        /// whose only feedback was a panel status line that vanished when the panel closed —
        /// nothing PERSISTENT told the player the upgrade was in flight. This reuses the WO-612
        /// scaffold + world countdown (dim + "M:SS" label) on the LIVE building(s) whose timer is
        /// keyed by <paramref name="buildingId"/> (the SAME id BuildTimerService.StartUpgrade used,
        /// and the SAME id BuildingUpgradeService.ApplyStructureHp targets). The label self-heals to
        /// reveal + the tier/HP applies when the timer completes.
        ///
        /// Matches the building by UpgradeCatalogId (city ids), then BuildingId, then a gameObject
        /// -name contains (resource ids farm / lumbermill / forge). Guard-wrapped so a bad match
        /// logs + skips and NEVER blocks the upgrade; a no-match is a traced no-op (the timer still
        /// runs — only the visual is absent, e.g. the building isn't spawned in this scene).
        /// </summary>
        public static void AttachToBuildingId(string buildingId)
        {
            if (string.IsNullOrEmpty(buildingId)) return;
            Guard.Try("BuildTimerUI", $"attach upgrade countdown for '{buildingId}'", () =>
            {
                var buildings = UnityEngine.Object.FindObjectsByType<Building>(FindObjectsSortMode.None);
                if (buildings == null) return;

                string want = buildingId.ToLowerInvariant();
                int attached = 0;
                foreach (var b in buildings)
                {
                    if (b == null || !BuildingMatches(b, want)) continue;
                    Attach(b.gameObject, buildingId);
                    attached++;
                }

                var svc = BuildTimerService.Instance;
                double rem = svc != null ? svc.RemainingSeconds(buildingId) : 0;
                if (attached > 0)
                    FlowTrace.Step("BuildTimerUI",
                        $"upgrade '{buildingId}' countdown attached to {attached} building(s) remaining={rem:0}s");
                else
                    FlowTrace.Warn("BuildTimerUI",
                        $"upgrade '{buildingId}' countdown found NO live building to anchor (timer still runs) remaining={rem:0}s");
            });
        }

        // Match a live Building to an upgrade-timer id: city catalog id first (same match
        // ApplyStructureHp uses), then the raw BuildingId, then a name contains (resource ids).
        private static bool BuildingMatches(Building b, string wantLower)
        {
            string cat = b.UpgradeCatalogId;
            if (!string.IsNullOrEmpty(cat) && cat.ToLowerInvariant() == wantLower) return true;
            string bid = b.BuildingId;
            if (!string.IsNullOrEmpty(bid) && bid.ToLowerInvariant() == wantLower) return true;
            var go = b.gameObject;
            return go != null && go.name.ToLowerInvariant().Contains(wantLower);
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

            // Start the CALM slow-circling "being worked on" loop (owner-tagged key). Parented to
            // the structure so the orb orbits it; a persistent LOOP (returns a handle) held for the
            // duration of the timer. Null-safe no-op if the key/prefab is missing; motion-based =>
            // colorblind-safe. Reveal()/OnDestroy() Stop() it (immediate) so the fireworks payoff is
            // never muddied by a lingering circle.
            _upgradeLoop = VFXManager.PlayKey("UpgradeVisual_Aura",
                transform.position + Vector3.up * 1f, Quaternion.identity, transform);

            var svc = BuildTimerService.Instance;
            if (svc != null) svc.JobCompleted += OnJobCompleted;
            FlowTrace.Step("Build", $"under-construction armed for '{_key}'"
                + (_upgradeLoop != null ? " (circling upgrade loop held)." : " (upgrade loop no-op — key/catalog not ready)."));
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
                // F8 (owner 2026-07-17): headless proof the countdown SHOWS + TICKS (~1/s).
                FlowTrace.Throttle("BuildTimerUI", _key, 1f, $"'{_key}' remaining={s:0}s");
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
            // Stop the circling loop IMMEDIATELY on completion so it can't linger under the
            // UpgradeStructureComplete_Aura fireworks the upgrade-apply path fires this same beat.
            StopUpgradeLoop();
            FlowTrace.Step("Build", $"construction complete — revealed '{_key}'");
            Destroy(this);
        }

        private void OnDestroy()
        {
            // Catch-all for cancel / host-destroy / scene teardown: never leak the circling loop.
            StopUpgradeLoop();
            var svc = BuildTimerService.Instance;
            if (svc != null) svc.JobCompleted -= OnJobCompleted;
        }

        /// <summary>Return the circling upgrade loop to its pool (idempotent — safe to call from
        /// both Reveal and OnDestroy; the handle is nulled so the second call is a no-op).</summary>
        private void StopUpgradeLoop()
        {
            if (_upgradeLoop == null) return;
            _upgradeLoop.Stop(immediate: true);
            _upgradeLoop = null;
        }
    }
}
