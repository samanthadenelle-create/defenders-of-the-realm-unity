// =============================================================================
// CollectorStackView — diegetic CoC-style collector-fill visual (WO-665a).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Buildings.Progression
//
// A SEPARATE presentation component (presentation-separate law): the gameplay
// object (ResourceCollector) only exposes state (FilledSteps / IsFull / IsBroken /
// StepChanged); THIS view renders it and never mutates the model. Mirrors the
// NodeFillIndicator static-Attach world-space-UI pattern and the FloatingHealthBar
// billboard / scale-comp / pulse patterns; the FULL glint reuses VFXManager.Play
// and the one-time FULL toast reuses GearGrantToast.
//
// WHAT IT SHOWS — each collector grows a diegetic pile of its resource:
//   Wood -> logs, Iron -> ingots/ore, Food -> grain sacks, Crystals -> shards.
//   Every 5% of capacity = one prop; 20 props = 100% = FULL. The props are POOLED:
//   all StepCount(20) are instanced ONCE at Attach and only SetActive-toggled on the
//   event-driven StepChanged tick (never per-frame instantiate).
//
// STATES:
//   empty   (0 steps)      -> no props, dim numeric readout
//   filling (1..19 steps)  -> pile grows bottom-up; live "N/20" readout
//   FULL    (20 steps)     -> gentle bob + billboarded "!" + one-shot glint VFX +
//                             one-time HUD toast ("<Building> is full ...")
//   broken  (IsBroken)     -> props scattered + hidden (siege raid tell)
//
// PROP SOURCE: CollectorStackPropCatalog loaded from Resources (mirrors VFXManager.
// EnsureCatalog). ABSTRACT-BAR FALLBACK: if the catalog is absent OR the resource has
// no prop wired (pack prefabs are gitignored on fresh clone), the view draws a
// NodeFillIndicator-style world-space fill bar instead of props — never blank.
//
// COLORBLIND-SAFE (owner is red/green colorblind): fill is encoded by SHAPE + HEIGHT
// + COUNT of props and a redundant NUMERIC "N/20" readout — never by colour alone.
// =============================================================================

using UnityEngine;
using UnityEngine.UI;

namespace DeNelle.Village.Buildings.Progression
{
    [DisallowMultipleComponent]
    public sealed class CollectorStackView : MonoBehaviour
    {
        // ── Layout constants ──────────────────────────────────────────────────
        private const float HeightOffset = 0.05f;   // props sit just above the host base
        private const int   GridColumns  = 4;       // pile is 4 props wide -> 5 layers tall at full
        private const float BobAmplitude = 0.12f;   // FULL bob height (world units)
        private const float BobSpeed     = 2.2f;

        // NEAR-FULL threshold (owner articulation 2026-07-24): the tight collect-loop wants the
        // player to SEE a collector filling up before it caps. At/above this fill fraction the
        // bar shifts to the amber "near-full" tint (a redundant tell layered on the always-present
        // fill % / step count — never hue-alone, colorblind-safe).
        private const float NearFullFraction = 0.85f;

        // Fallback-bar palette (kept high-luminance-contrast; meaning carried by fill %, not hue).
        private static readonly Color BarTrack   = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color BarFill    = new Color(0.85f, 0.72f, 0.30f, 1f);  // amber gold
        private static readonly Color BarNearFull = new Color(0.95f, 0.62f, 0.15f, 1f); // deeper amber at ~85%+
        private static readonly Color BarFull    = new Color(1f, 0.92f, 0.55f, 1f);     // bright gold at full

        // ── Model seam (read-only) ────────────────────────────────────────────
        private ResourceCollector _collector;

        // ── Prop-stack state ──────────────────────────────────────────────────
        private Transform _stackRoot;               // parent for the pooled props (bobs when full)
        private GameObject[] _props;                // pooled, instanced ONCE; SetActive-toggled
        private Vector3[] _propHome;                // each prop's stacked local position
        private bool _usesProps;                    // false -> abstract-bar fallback

        // ── Fallback bar ──────────────────────────────────────────────────────
        private Canvas _barCanvas;
        private RectTransform _fillRect;
        private Image _fillImg;
        private Vector2 _barSize = new Vector2(1.4f, 0.20f);

        // ── Shared readout / FULL tell (both paths) ───────────────────────────
        private Canvas _infoCanvas;                 // world-space "N/20" + "!" billboard
        private Text _countText;
        private GameObject _fullBang;               // the "!" marker, shown only when full
        private Transform _cam;

        private int _lastSteps = -1;
        private bool _wasFull;                      // debounces the one-time FULL toast
        private bool _built;

        // Catalog is loaded once and shared (mirrors VFXManager.EnsureCatalog).
        private static CollectorStackPropCatalog s_catalog;
        private static bool s_catalogLoaded;

        // ─────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Attach (or reuse) a stack view on a collector's host transform. No scene edit —
        /// the bootstrap calls this after Configure. Skips DDOL logical-fallback hosts that
        /// sit at the world origin (they have no visible host to decorate).
        /// </summary>
        public static CollectorStackView Attach(ResourceCollector collector)
        {
            if (collector == null) return null;

            // Skip the origin-parked logical-fallback collectors (DDOL host children at 0,0,0).
            var host = collector.transform;
            if (host.position.sqrMagnitude < 0.01f) return null;

            var existing = host.GetComponent<CollectorStackView>();
            if (existing != null) return existing;

            var view = host.gameObject.AddComponent<CollectorStackView>();
            view._collector = collector;
            return view;
        }

        private void Awake()
        {
            if (_collector == null) _collector = GetComponent<ResourceCollector>();
        }

        private void Start()
        {
            Build();
            var c = Camera.main;
            _cam = c != null ? c.transform : null;

            // Subscribe to the model's single "re-render" signal + paint the initial state.
            if (_collector != null) _collector.StepChanged += OnStepChanged;
            Refresh();

            AttachHarvestAura();
        }

        // ── WO-890: the held VFX loop that DECORATES this tell (never replaces it) ──
        //
        // The "!" bang, the bob, the numeric readout, the one-shot LevelUp_Celebration
        // glint and the coalesced FULL toast above are UNCHANGED - WO-890 says build ON
        // the existing full-tell, and this does. What was missing is a PERSISTENT read:
        // the glint fires once at the transition and is gone, so a player who looks away
        // for two seconds never learns the collector capped. Collector_Ready is the
        // standing beacon (rising bob = "come pick me up"), and the per-resource harvest
        // aura is the standing "this thing is working" read below it.
        //
        // Ownership of the loop is entirely HarvestAura's: one handle, one beat at a
        // time, a nearest-N budget across every harvest surface in the town, and a stop
        // on every exit path (beat change, collected, broken, disable, destroy, scene
        // unload). This view supplies STATE and nothing else - it never holds a handle,
        // so it cannot leak one.
        private void AttachHarvestAura()
        {
            if (_collector == null) return;

            var col = _collector;   // capture the field, not `this`, so the delegate has no view dependency
            HarvestAura.Attach(gameObject,
                () =>
                {
                    if (col == null || col.IsBroken || !col.IsActive) return HarvestAura.Beat.None;
                    return col.IsFull ? HarvestAura.Beat.Ready : HarvestAura.Beat.Harvesting;
                },
                () => HarvestAura.TypeForResource(col != null ? col.Resource : HarvestResource.Wood),
                "collector:" + col.BuildingId);
        }

        private void OnDestroy()
        {
            if (_collector != null) _collector.StepChanged -= OnStepChanged;
        }

        // ── Build (once) ──────────────────────────────────────────────────────
        private void Build()
        {
            if (_built) return;
            _built = true;

            EnsureCatalog();

            _stackRoot = new GameObject("CollectorStack").transform;
            _stackRoot.SetParent(transform, false);
            _stackRoot.localPosition = new Vector3(0f, HeightOffset, 0f);

            var res = _collector != null ? _collector.Resource : HarvestResource.Wood;
            _usesProps = s_catalog != null && s_catalog.TryGet(res, out var entry) && entry.Prop != null;

            if (_usesProps)
                BuildProps(res);
            else
                BuildFallbackBar();

            BuildInfoCanvas();
        }

        private static void EnsureCatalog()
        {
            if (s_catalogLoaded) return;
            s_catalogLoaded = true;
            s_catalog = Resources.Load<CollectorStackPropCatalog>(CollectorStackPropCatalog.ResourcesPath);
            DeNelle.Core.Diagnostics.FlowTrace.Step("Harvest",
                s_catalog != null
                    ? $"CollectorStackView: loaded prop catalog ({s_catalog.Entries?.Length ?? 0} rows)."
                    : "CollectorStackView: no prop catalog at Resources/" + CollectorStackPropCatalog.ResourcesPath +
                      " — collectors use the abstract fill-bar fallback.");
        }

        // Instantiate all StepCount props ONCE, stacked bottom-up; start inactive.
        private void BuildProps(HarvestResource res)
        {
            // Fetch from the single TryGet source (prefab / scale / footprint).
            if (s_catalog == null || !s_catalog.TryGet(res, out var entry) || entry.Prop == null)
            {
                _usesProps = false;
                BuildFallbackBar();
                return;
            }

            float scale = entry.PropScale > 0f ? entry.PropScale : 1f;
            Vector3 slot = entry.SlotSize.sqrMagnitude > 0.0001f ? entry.SlotSize : new Vector3(1.2f, 1.0f, 0.6f);

            int cols = GridColumns;
            int rows = Mathf.CeilToInt(ResourceCollector.StepCount / (float)cols);
            float spacingX = slot.x / Mathf.Max(1, cols);
            float spacingY = slot.y / Mathf.Max(1, rows);

            _props = new GameObject[ResourceCollector.StepCount];
            _propHome = new Vector3[ResourceCollector.StepCount];

            for (int i = 0; i < ResourceCollector.StepCount; i++)
            {
                int col = i % cols;
                int row = i / cols;
                float x = (col - (cols - 1) * 0.5f) * spacingX;
                float y = row * spacingY;
                float z = (row % 2 == 0 ? 1f : -1f) * spacingX * 0.15f;   // slight brick offset per layer

                var prop = Instantiate(entry.Prop, _stackRoot);
                prop.name = $"StackProp_{i:D2}";
                prop.transform.localPosition = new Vector3(x, y, z);
                prop.transform.localScale = Vector3.one * scale;
                StripColliders(prop);   // decoration only — never intercept siege contact / clicks
                prop.SetActive(false);

                _props[i] = prop;
                _propHome[i] = prop.transform.localPosition;
            }
        }

        // NodeFillIndicator-style world-space fill bar (never blank when props are absent).
        private void BuildFallbackBar()
        {
            var canvasGo = new GameObject("CollectorFillBar");
            canvasGo.transform.SetParent(transform, false);
            canvasGo.transform.localPosition = new Vector3(0f, 1.6f, 0f);

            _barCanvas = canvasGo.AddComponent<Canvas>();
            _barCanvas.renderMode = RenderMode.WorldSpace;
            var crt = _barCanvas.GetComponent<RectTransform>();
            crt.sizeDelta = _barSize;

            var bgGo = new GameObject("Track");
            bgGo.transform.SetParent(canvasGo.transform, false);
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.color = BarTrack;
            bgImg.raycastTarget = false;
            var bgRect = bgGo.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero; bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero; bgRect.offsetMax = Vector2.zero;

            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(canvasGo.transform, false);
            _fillImg = fillGo.AddComponent<Image>();
            _fillImg.color = BarFill;
            _fillImg.raycastTarget = false;
            _fillRect = fillGo.GetComponent<RectTransform>();
            _fillRect.anchorMin = new Vector2(0f, 0f);
            _fillRect.anchorMax = new Vector2(0f, 1f);
            _fillRect.pivot = new Vector2(0f, 0.5f);
            _fillRect.offsetMin = Vector2.zero; _fillRect.offsetMax = Vector2.zero;
            _fillRect.sizeDelta = new Vector2(0f, 0f);
        }

        // Redundant numeric readout ("N/20") + the FULL "!" marker (shape tell, not colour).
        private void BuildInfoCanvas()
        {
            var canvasGo = new GameObject("CollectorInfo");
            canvasGo.transform.SetParent(transform, false);
            canvasGo.transform.localPosition = new Vector3(0f, 2.0f, 0f);

            _infoCanvas = canvasGo.AddComponent<Canvas>();
            _infoCanvas.renderMode = RenderMode.WorldSpace;
            var crt = _infoCanvas.GetComponent<RectTransform>();
            crt.sizeDelta = new Vector2(1.6f, 0.9f);
            canvasGo.transform.localScale = Vector3.one * 0.01f;   // world-space canvas → shrink to ~metres

            // "!" bang marker — a bold, high-contrast shape shown ONLY when full.
            _fullBang = new GameObject("FullBang");
            _fullBang.transform.SetParent(canvasGo.transform, false);
            var bangText = _fullBang.AddComponent<Text>();
            bangText.font = BuiltinFont();
            bangText.text = "!";
            bangText.fontSize = 64;
            bangText.fontStyle = FontStyle.Bold;
            bangText.alignment = TextAnchor.MiddleCenter;
            bangText.color = new Color(1f, 0.95f, 0.6f, 1f);
            bangText.raycastTarget = false;
            var bangRt = _fullBang.GetComponent<RectTransform>();
            bangRt.anchorMin = new Vector2(0.5f, 0f); bangRt.anchorMax = new Vector2(0.5f, 0f);
            bangRt.pivot = new Vector2(0.5f, 0f);
            bangRt.anchoredPosition = new Vector2(0f, 30f);
            bangRt.sizeDelta = new Vector2(60f, 80f);
            _fullBang.SetActive(false);

            // Numeric readout — always present; the colorblind-safe redundant channel.
            var countGo = new GameObject("Count");
            countGo.transform.SetParent(canvasGo.transform, false);
            _countText = countGo.AddComponent<Text>();
            _countText.font = BuiltinFont();
            _countText.text = "0/" + ResourceCollector.StepCount;
            _countText.fontSize = 28;
            _countText.fontStyle = FontStyle.Bold;
            _countText.alignment = TextAnchor.MiddleCenter;
            _countText.color = Color.white;
            _countText.raycastTarget = false;
            var ctRt = countGo.GetComponent<RectTransform>();
            ctRt.anchorMin = Vector2.zero; ctRt.anchorMax = Vector2.one;
            ctRt.offsetMin = Vector2.zero; ctRt.offsetMax = Vector2.zero;
        }

        // ── Event-driven refresh (StepChanged only — never per-frame) ─────────
        private void OnStepChanged(ResourceCollector _) => Refresh();

        private void Refresh()
        {
            if (_collector == null) return;

            bool broken = _collector.IsBroken;
            int steps = broken ? 0 : _collector.FilledSteps;
            bool full = !broken && _collector.IsFull;

            // Prop pile: SetActive the bottom `steps` props (pooled toggle, no instantiate).
            if (_usesProps && _props != null)
            {
                for (int i = 0; i < _props.Length; i++)
                {
                    if (_props[i] == null) continue;
                    bool on = i < steps;
                    if (_props[i].activeSelf != on) _props[i].SetActive(on);
                    if (on && broken == false) _props[i].transform.localPosition = _propHome[i];
                }
                if (broken) ScatterProps();
            }

            // Fallback bar: width = fill fraction; amber near-full (~85%+), bright gold at full.
            // The tint is a REDUNDANT tell on top of the width (fill %) + numeric readout —
            // shape/height + words carry the meaning, never hue alone (colorblind-safe).
            if (!_usesProps && _fillRect != null)
            {
                float frac = broken ? 0f : Mathf.Clamp01(steps / (float)ResourceCollector.StepCount);
                _fillRect.sizeDelta = new Vector2(_barSize.x * frac, 0f);
                if (_fillImg != null)
                    _fillImg.color = full ? BarFull
                                   : (frac >= NearFullFraction ? BarNearFull : BarFill);
            }

            // Numeric readout (redundant, colorblind-safe channel).
            if (_countText != null)
            {
                _countText.text = broken
                    ? "RAIDED"
                    : steps + "/" + ResourceCollector.StepCount;
                _countText.color = full ? new Color(1f, 0.95f, 0.6f, 1f) : Color.white;
            }

            // FULL "!" shape tell.
            if (_fullBang != null && _fullBang.activeSelf != full) _fullBang.SetActive(full);

            // Edge: empty/■ -> FULL fires the one-shot glint + one-time toast (debounced).
            if (full && !_wasFull)
            {
                PlayFullTell();
                ShowFullToast();
            }
            _wasFull = full;
            _lastSteps = steps;
        }

        // Scatter the (already-active) props outward as a raid tell, then hide them next frame
        // is unnecessary — RAIDED steps==0 hides them; scatter animates the ones caught full.
        private void ScatterProps()
        {
            if (_props == null) return;
            for (int i = 0; i < _props.Length; i++)
            {
                if (_props[i] == null || !_props[i].activeSelf) continue;
                Vector3 dir = new Vector3(Random.Range(-1f, 1f), Random.Range(0f, 0.5f), Random.Range(-1f, 1f));
                _props[i].transform.localPosition = _propHome[i] + dir.normalized * 0.4f;
                _props[i].SetActive(false);   // raided → gone
            }
        }

        private void PlayFullTell()
        {
            Vector3 pos = _stackRoot != null ? _stackRoot.position : transform.position + Vector3.up * 1.5f;
            // Reuse the shared VFX pool — a gold celebratory glint. Null-safe (no-op pre-init).
            VFXManager.Play(VFXType.LevelUp_Celebration, pos + Vector3.up * 0.6f);
        }

        // -- Aggregated FULL toast (WO-900 sec.3, defect 2) -----------------------
        // Each view fires its own toast, so three collectors capping in the same frame threw
        // THREE stacked toasts at the player. The tell is per-collector but the ANNOUNCEMENT is
        // a town-level fact, so it is coalesced here: names are collected into a static pending
        // set and flushed ONCE at end of frame, naming every building that filled.
        // Coalescing is driven off the EXISTING LateUpdate pump rather than a coroutine on
        // purpose: a coroutine started on a view that is destroyed in the same frame (scene
        // unload / siege) would never resume, wedging its "flush queued" flag true and killing
        // every future toast for the whole app run. LateUpdate cannot wedge - any surviving
        // view drains the buffer, and if no view survives there is nobody to toast anyway.
        private static readonly System.Collections.Generic.List<string> s_pendingFullNames =
            new System.Collections.Generic.List<string>(4);

        private void ShowFullToast()
        {
            // Refresh() runs from StepChanged, which fires inside the harvester's Update loop -
            // so every collector that capped this frame has been buffered before ANY LateUpdate
            // runs, and the flush below sees the complete set.
            string label = ResolveBuildingLabel();
            if (!s_pendingFullNames.Contains(label)) s_pendingFullNames.Add(label);
        }

        private static void FlushFullToast()
        {
            if (s_pendingFullNames.Count == 0) return;

            // Singleton-correct wording (owner 2026-07-24): a collector is one-of-a-kind, so
            // "place another" is wrong — the player collects it, or upgrades it to hold more.
            // ASCII-only (hyphen, not em dash). "Storage" is deliberately never used here:
            // that word belongs to the town BANK (WO-857), and the player must never be shown
            // two different notions of "full" (WO-900 sec.4 copy law).
            string subject = s_pendingFullNames.Count == 1
                ? s_pendingFullNames[0] + " is full"
                : string.Join(", ", s_pendingFullNames.ToArray()) + " are full";
            s_pendingFullNames.Clear();

            GearGrantToast.Show($"{subject} - collect it, or upgrade it to hold more.");
        }

        private string ResolveBuildingLabel()
        {
            if (_collector == null) return "Collector";
            var def = ResourceBuildingProgression.Find(_collector.BuildingId);
            if (def != null && !string.IsNullOrWhiteSpace(def.DisplayName)) return def.DisplayName;
            return ResourceBuildingProgression.LabelFor(_collector.Resource);
        }

        // Builtin uGUI font with the project-standard LegacyRuntime -> Arial fallback
        // (mirrors VfxParadeRuntime) so the readout text never renders with a null font.
        private static Font BuiltinFont()
        {
            var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return f;
        }

        // Decoration props must never carry colliders (would intercept siege contact / clicks).
        private static void StripColliders(GameObject go)
        {
            foreach (var col in go.GetComponentsInChildren<Collider>(true))
                col.enabled = false;
        }

        private void LateUpdate()
        {
            // Drain the coalesced FULL announcement buffer (see ShowFullToast): exactly ONE
            // toast per frame no matter how many collectors capped together.
            FlushFullToast();

            // Gentle FULL bob (cheap; only while full). Shape/height tell, not colour.
            if (_stackRoot != null)
            {
                float bob = (_wasFull) ? Mathf.Abs(Mathf.Sin(Time.time * BobSpeed)) * BobAmplitude : 0f;
                _stackRoot.localPosition = new Vector3(0f, HeightOffset + bob, 0f);
            }

            // Billboard the info canvas (and the "!") toward the camera, pinned upright.
            if (_infoCanvas == null) return;
            if (_cam == null)
            {
                var cm = Camera.main;
                _cam = cm != null ? cm.transform : null;
            }
            if (_cam != null)
            {
                Vector3 toCam = _infoCanvas.transform.position - _cam.position;
                Vector3 flat = new Vector3(toCam.x, 0f, toCam.z);
                Vector3 fwd = flat.sqrMagnitude > 1e-6f ? flat : toCam;
                if (fwd.sqrMagnitude > 1e-6f)
                    _infoCanvas.transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
            }
            if (_barCanvas != null && _cam != null)
            {
                Vector3 toCam = _barCanvas.transform.position - _cam.position;
                Vector3 flat = new Vector3(toCam.x, 0f, toCam.z);
                Vector3 fwd = flat.sqrMagnitude > 1e-6f ? flat : toCam;
                if (fwd.sqrMagnitude > 1e-6f)
                    _barCanvas.transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
            }
        }
    }
}
