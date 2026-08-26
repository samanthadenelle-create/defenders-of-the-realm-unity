// =============================================================================
// HudMinimapWidget — the corner "you are here" minimap (WO-828, program WO-825).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.HUD   Namespace: DeNelle.HUD.Kit
//
// WHY IT EXISTS: the compass gives BEARING but not PLACE. HudCompassWidget can
// tell you the gate is 40 degrees to your right; it cannot tell you that you are
// in the north-east courtyard. This widget is the diagram half of that pair, and
// it deliberately COMPLEMENTS the compass rather than replacing it — both sit in
// the calm postures, on different area mounts, reading the SAME three providers.
//
// ⛔ THE COST RULE, AND IT IS THE WHOLE DESIGN (WO-828 "cheap", spec ruled AGAINST
//    a live second Camera -> RenderTexture on mobile):
//   * NO Camera. NO RenderTexture. NO render pass of any kind is added.
//   * The backdrop is a STATIC dark-glass plate — two Images, built once.
//   * Every "live" element is a pooled RectTransform whose anchoredPosition moves.
//     Drawing the minimap costs the same as drawing a handful of quads that were
//     already in the canvas batch.
//   * LAYOUT RUNS AT 10 Hz, NOT PER FRAME (WO-828 §7). LateUpdate's per-frame body
//     is a float decrement and an early return; the projection maths happens on
//     every 6th frame at 60 fps. Provider polls are slower still (4 Hz), because
//     they are reflection scans (FindObjectsByType) — same throttle the compass
//     uses, for the same reason.
//   * The pools never shrink and never re-allocate in steady state: zero alloc on
//     the hot path.
//
// PRESENTATION-ONLY (§5 / HP B2B): it READS world positions through provider
// delegates wired by HudKitController, and owns NO game state. DeNelle.HUD keeps
// its "HUD -> Core only" edge — the Village types are resolved by REFLECTION on
// the controller side, never referenced here.
//
// NORTH-UP, CENTRED ON THE HERO. Two deliberate choices:
//   * Centred on the hero (WO-828 §2 default) — one projection path for hub AND
//     overworld, so there is no "which scene am I in" branch to get wrong.
//   * NORTH-UP rather than heading-up, because the compass is ALREADY heading-up.
//     A heading-up minimap would duplicate the compass's job and spin under the
//     player's thumb; a fixed north gives the stable mental map the compass can't.
//
// ⛔ COLOURBLIND LAW (CLAUDE.md §7): every mark is a distinct SILHOUETTE first
// (disc = you, diamond = objective, apex-up triangle = threat, ring/square/bar =
// content pins) and the region NAME + danger TIER are always spelled out in the
// chip beneath the map. Desaturate the screen and nothing is lost. The threat pip
// is literally the compass's own sprite (HudCompassWidget.EnemyPipSprite) so the
// two widgets can never drift apart on what a threat looks like.
//
// FlowTrace tag: "Minimap" (WO-828 acceptance).
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.UI;
using DeNelle.Core.World;

namespace DeNelle.HUD.Kit
{
    /// <summary>The corner minimap kit widget (see header). Built by HudKitController,
    /// placed by the hud-areas.json "minimap" occupancy rows.</summary>
    [DisallowMultipleComponent]
    public sealed class HudMinimapWidget : MonoBehaviour
    {
        // ── Provider seams (set by HudKitController; presentation reads the world) ──
        /// <summary>The hero transform — the map centre.</summary>
        public Func<Transform> HeroProvider;
        /// <summary>World position of the current objective / region-gate seam, or null.
        /// The SAME provider the compass reads, so the two never point different ways.</summary>
        public Func<Vector3?> ObjectiveProvider;
        /// <summary>Live threat transforms (may be null/empty).</summary>
        public Func<IReadOnlyList<Transform>> EnemyProvider;

        // ── Tuning ────────────────────────────────────────────────────────────
        /// <summary>World metres from the hero to the map rim. Beyond this a threat/pin
        /// is CULLED (WO-828 §2). The objective is the one exception — it clamps to the
        /// rim instead of vanishing, because "where do I go" must never disappear (the
        /// same guarantee HudCompassWidget.UpdateObjective gives).</summary>
        private const float RadiusWorld = 150f;

        /// <summary>Map plate edge, in canvas reference units. ~200 ref units resolves to
        /// roughly 220 device px on a 2340x1080 phone - inside WO-828's 120-160 dp corner brief.
        /// WARNING WO-1219: the VALUE lives in HudLayoutBands, not here. The Minimap MOUNT is
        /// sized around this plate PLUS the status-line band beneath it, so the plate size and
        /// the band it has to fit inside must move together, or the column silently
        /// over-subscribes again - which is how the gear/Store row ended up on the map's lower
        /// edge and the status line ended up under the gear.</summary>
        private const float PlateSize = HudLayoutBands.MinimapPlatePx;
        /// <summary>Region status-line height in reference units (font floor + padding). Shared
        /// with HudLayoutBands, which reserves the band this line is drawn in.</summary>
        private const float ChipHeight = HudLayoutBands.StatusLinePx;

        /// <summary>Layout cadence. WO-828 §7 asks for 4-10 Hz; 10 Hz is the top of that
        /// band — smooth enough to read as motion, 6x cheaper than per-frame.</summary>
        private const float LayoutInterval = 0.10f;
        /// <summary>Provider cadence. These are reflection scans on the controller side,
        /// so they stay at the compass's 4 Hz and never touch the hot path.</summary>
        private const float ProviderPollInterval = 0.25f;

        /// <summary>Hard cap on drawn threat pips. A wave can field far more enemies than
        /// a 200-unit plate can show without becoming noise; past this the count is
        /// reported in the chip TEXT instead (which is also the colourblind channel).</summary>
        private const int MaxThreatPips = 16;

        // ── Runtime refs ──────────────────────────────────────────────────────
        private RectTransform _frame;      // the square plate
        private Image _rim;                // danger/withering-tinted border
        private RectTransform _dotLayer;   // RectMask2D-clipped: everything that moves
        private RectTransform _heroDot;
        private RectTransform _objDot;
        private TextMeshProUGUI _chip;     // region name + tier + threat count (ALWAYS text)
        private readonly List<RectTransform> _threatPool = new List<RectTransform>();
        private readonly List<RectTransform> _pinPool = new List<RectTransform>();
        private readonly List<Image> _pinImages = new List<Image>();
        // The kind each pin SLOT is currently styled as (-1 = never styled). A pooled slot
        // is re-styled only when the pin occupying it changes kind — without this the 10 Hz
        // pass would re-assign sprite/size/rotation for every pin, every tick, forever.
        private readonly List<int> _pinStyledAs = new List<int>();
        private readonly List<Transform> _threatBuf = new List<Transform>();

        private Transform _hero;
        private Vector3? _objective;
        private float _pollTimer;
        private float _layoutTimer;
        private bool _built;
        private int _lastThreatCount = -1;
        private string _lastChipText = "";
        private int _lastTier = -99;

        // ── Probe seams (headless AutoPilot / regression — read-only + one nudge) ──
        /// <summary>True once the plate is constructed.</summary>
        public bool IsBuilt => _built;
        /// <summary>True when HudKitController wired the hero provider.</summary>
        public bool ProvidersWired => HeroProvider != null;
        /// <summary>Threats in the buffer at the last provider poll.</summary>
        public int ThreatMarkCount => _threatBuf.Count;
        /// <summary>Marks (threat pips + content pins) ACTIVE this frame — the
        /// "dot count updates with fake providers" assert of WO-828 §6.</summary>
        public int ActiveDotCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _threatPool.Count; i++)
                    if (_threatPool[i] != null && _threatPool[i].gameObject.activeSelf) n++;
                for (int i = 0; i < _pinPool.Count; i++)
                    if (_pinPool[i] != null && _pinPool[i].gameObject.activeSelf) n++;
                if (_objDot != null && _objDot.gameObject.activeSelf) n++;
                return n;
            }
        }
        /// <summary>The region chip's current text (never empty once laid out) — lets a
        /// headless probe assert the colourblind text channel is actually populated.</summary>
        public string ChipText => _chip != null ? _chip.text : "";

        /// <summary>Poll the providers and re-lay-out NOW, skipping both throttle windows.
        /// Probe determinism only; read-only with respect to game state. Works on an
        /// inactive instance (the compass learned that a timer-only nudge is dead there).</summary>
        public void ForceRefresh()
        {
            _pollTimer = 0f;
            _layoutTimer = 0f;
            if (HeroProvider != null) _hero = HeroProvider();
            _objective = ObjectiveProvider != null ? ObjectiveProvider() : (Vector3?)null;
            RefreshThreats();
            Layout();
        }

        /// <summary>Kit-builder factory: create the minimap under a parent RectTransform
        /// (HudKitController wraps + registers it like every other widget).</summary>
        public static HudMinimapWidget Create(Transform parent)
        {
            var go = new GameObject("MinimapWidget", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var w = go.AddComponent<HudMinimapWidget>();
            w.Build();
            return w;
        }

        private void Build()
        {
            if (_built) return;
            _built = true;

            using var _flow = FlowTrace.Enter("Minimap", "Build corner plate (WO-828)");

            Guard.Try("Minimap", "minimap plate construction", () =>
            {
                // The plate hangs from the mount's TOP-LEFT at a FIXED reference size rather
                // than filling the band. A fraction-of-band square would change aspect with
                // the device (the band's height is a fraction of screen height, its width a
                // fraction of screen width) — i.e. the map would be a rectangle on most
                // phones. Fixed units keep it square everywhere, and leave the bottom of the
                // band free for the region chip.
                _frame = NewRect("MinimapPlate", (RectTransform)transform);
                _frame.anchorMin = new Vector2(0f, 1f);
                _frame.anchorMax = new Vector2(0f, 1f);
                _frame.pivot     = new Vector2(0f, 1f);
                _frame.sizeDelta = new Vector2(PlateSize, PlateSize);
                _frame.anchoredPosition = Vector2.zero;

                // Rim FIRST (behind), then the dark-glass face inset over it — the same
                // rim/plate recipe the compass strip uses, so the two read as one kit.
                _rim = AddPlate("PlateRim", _frame, new Color(0.604f, 0.498f, 0.243f, 0.60f), 0f);
                AddPlate("PlateFace", _frame, new Color(0.043f, 0.047f, 0.059f, 0.88f), 3f);

                // Faint crosshair through the centre: sells "this point is you" without a
                // second live element (two static 1-unit Images, never touched again).
                AddHairline(_frame, horizontal: true);
                AddHairline(_frame, horizontal: false);

                // The clipped layer everything live lives in. A dot sliding past the rim is
                // MASKED rather than overhanging the plate.
                var layerGo = new GameObject("MinimapDots", typeof(RectTransform), typeof(RectMask2D));
                layerGo.transform.SetParent(_frame, false);
                _dotLayer = layerGo.GetComponent<RectTransform>();
                _dotLayer.anchorMin = Vector2.zero; _dotLayer.anchorMax = Vector2.one;
                _dotLayer.offsetMin = new Vector2(6f, 6f);
                _dotLayer.offsetMax = new Vector2(-6f, -6f);

                // NORTH tick — the one piece of orientation a north-up map owes the player.
                // ASCII only (the build's LiberationSans SDF renders tofu otherwise).
                var north = AddText(_frame, "N", 22f, ElarionUi.Gilt, TextAlignmentOptions.Top);
                var nrt = (RectTransform)north.transform;
                nrt.anchorMin = new Vector2(0f, 0.80f);
                nrt.anchorMax = new Vector2(1f, 1.00f);
                nrt.offsetMin = Vector2.zero; nrt.offsetMax = Vector2.zero;
                north.fontStyle = FontStyles.Bold;
                north.enableAutoSizing = true;
                north.fontSizeMin = 14f;
                north.fontSizeMax = 22f;

                // ── the HERO disc: static at the centre (the map moves, you don't) ──
                _heroDot = NewRect("HeroDot", _dotLayer);
                CentreAnchor(_heroDot, 14f, 14f);
                var heroImg = _heroDot.gameObject.AddComponent<Image>();
                ApplyShape(heroImg, _heroDot, RealmPinShape.Circle, ElarionUi.Gilt);

                // ── the OBJECTIVE diamond: the compass's own objective language ──
                _objDot = NewRect("ObjectiveDot", _dotLayer);
                CentreAnchor(_objDot, 18f, 18f);
                var objImg = _objDot.gameObject.AddComponent<Image>();
                ApplyShape(objImg, _objDot, RealmPinShape.Diamond, ElarionUi.Gilt);
                _objDot.gameObject.SetActive(false);

                // ── the region chip: the COLOURBLIND CHANNEL. Region name + danger tier +
                // threat count, in words, always. Every tint on this widget is decoration
                // on top of this line; none of them is load-bearing.
                _chip = AddText((RectTransform)transform, "", ElarionUi.FontMicro,
                                ElarionUi.Parchment, TextAlignmentOptions.Left);
                // ⭐ WO-1219 (owner-approved design, 2026-08-26) - THE STATUS LINE GETS ITS OWN
                // BAND, DIRECTLY BELOW THE PLATE. It has now been in two wrong seats and the
                // reason both were wrong is the same: it was placed against whatever space this
                // widget happened to have, not against what the COLUMN had.
                //   * Originally it stacked UNDER the plate inside a 241-unit band that could not
                //     hold PlateSize + 4 + ChipHeight = 234 plus the gear/Store row below it, so
                //     the row overflowed up and "Elarion - Safe - N threats" read from UNDER the
                //     gear (tmp/screen-103219.png, tmp/shield-seat-101829.png).
                //   * The interim fix moved it BESIDE the plate, which bought height back but put
                //     a text line in the middle of the column at the plate's own eye level.
                // The column is now one table (DeNelle.Core.UI.HudLayoutBands) and the Minimap
                // MOUNT is deliberately taller than the plate: the plate takes the top
                // MinimapPlatePx of it, this line takes its own band immediately beneath, and the
                // gear/Store row lives in the Dock band below that with clear air between them.
                // HudUiRegression check 8 asserts exactly that - the line lies wholly BELOW the
                // plate and inside the mount, and no two left-column bands intersect.
                // It STRETCHES to the mount's edges rather than carrying a fixed width: a fixed
                // PlateSize + 60 would overhang the mount on a narrower aspect, which is the same
                // class of bug one band up.
                var crt = (RectTransform)_chip.transform;
                crt.anchorMin = new Vector2(0f, 1f);
                crt.anchorMax = new Vector2(1f, 1f);
                crt.pivot     = new Vector2(0f, 1f);
                crt.offsetMin = new Vector2(HudLayoutBands.StatusLineGapPx,
                                            -(PlateSize + HudLayoutBands.StatusLineGapPx + ChipHeight));
                crt.offsetMax = new Vector2(-HudLayoutBands.StatusLineGapPx,
                                            -(PlateSize + HudLayoutBands.StatusLineGapPx));
                _chip.enableAutoSizing = true;
                _chip.fontSizeMin = 16f;
                _chip.fontSizeMax = ElarionUi.FontMicro;

                FlowTrace.Step("Minimap",
                    $"plate built: {PlateSize:0}x{PlateSize:0} ref units, north-up, hero-centred, " +
                    $"radius={RadiusWorld:0}u, layout={1f / LayoutInterval:0} Hz, no camera/RT.");
            });
        }

        // =====================================================================
        // PER-FRAME BODY. Read this and the cost of the widget is fully visible:
        // two float decrements and (5 frames in 6) a return. Nothing else.
        // =====================================================================
        private void LateUpdate()
        {
            if (!_built) return;

            _pollTimer -= Time.unscaledDeltaTime;
            if (_pollTimer <= 0f)
            {
                _pollTimer = ProviderPollInterval;
                if (HeroProvider != null) _hero = HeroProvider();
                _objective = ObjectiveProvider != null ? ObjectiveProvider() : (Vector3?)null;
                RefreshThreats();
            }

            _layoutTimer -= Time.unscaledDeltaTime;
            if (_layoutTimer > 0f) return;
            _layoutTimer = LayoutInterval;

            Layout();
        }

        // ── the 10 Hz body: pure RectTransform moves, no allocation ────────────
        private void Layout()
        {
            if (_dotLayer == null) return;

            bool haveHero = _hero != null && _hero;
            if (_heroDot != null && _heroDot.gameObject.activeSelf != haveHero)
                _heroDot.gameObject.SetActive(haveHero);

            if (!haveHero)
            {
                HideAll(_threatPool);
                HideAll(_pinPool);
                if (_objDot != null && _objDot.gameObject.activeSelf) _objDot.gameObject.SetActive(false);
                SetChip("Locating...");
                return;
            }

            Vector3 centre = _hero.position;
            float half = Mathf.Min(_dotLayer.rect.width, _dotLayer.rect.height) * 0.5f;
            // Layout may not have run on the first tick after a posture flip; a 0-size rect
            // would collapse every dot onto the centre and read as "the minimap is broken".
            // Fall back to the authored plate size rather than laying out into nothing.
            if (half <= 1f) half = (PlateSize - 12f) * 0.5f;

            LayoutObjective(centre, half);
            LayoutThreats(centre, half);
            LayoutPins(centre, half);
            UpdateChip(centre);
        }

        // The objective CLAMPS to the rim instead of culling — the one mark that must
        // never disappear (WO-828 §3 "Objective from compass/map-travel appears when set").
        private void LayoutObjective(Vector3 centre, float half)
        {
            if (_objDot == null) return;
            if (_objective == null)
            {
                if (_objDot.gameObject.activeSelf) _objDot.gameObject.SetActive(false);
                return;
            }
            Vector2 p = Project(_objective.Value, centre, half);
            float len = p.magnitude;
            float rim = half - 10f;
            if (len > rim && len > 0.001f) p *= rim / len;   // pin to the rim, keep the bearing
            if (!_objDot.gameObject.activeSelf) _objDot.gameObject.SetActive(true);
            _objDot.anchoredPosition = p;
        }

        private void LayoutThreats(Vector3 centre, float half)
        {
            int n = Mathf.Min(_threatBuf.Count, MaxThreatPips);
            EnsurePool(_threatPool, n, RealmPinShape.TriangleUp, ElarionUi.Danger, 12f, 14f);

            for (int i = 0; i < _threatPool.Count; i++)
            {
                var dot = _threatPool[i];
                if (i >= n || _threatBuf[i] == null)
                {
                    if (dot.gameObject.activeSelf) dot.gameObject.SetActive(false);
                    continue;
                }
                Vector3 world = _threatBuf[i].position;
                // CULL beyond the radius (unlike the objective) — a threat 400 m away
                // pinned to the rim would read as "something is right there".
                if (SqrPlanarDistance(world, centre) > RadiusWorld * RadiusWorld)
                {
                    if (dot.gameObject.activeSelf) dot.gameObject.SetActive(false);
                    continue;
                }
                if (!dot.gameObject.activeSelf) dot.gameObject.SetActive(true);
                dot.anchoredPosition = Project(world, centre, half);
            }
        }

        // WO-829 §6 — the minimap MIRRORS the shared content pins rather than deriving a
        // second set. RealmPinBoard is the one registry both this widget and the parchment
        // Realm Map read, so a raid camp cannot appear on one surface and not the other.
        private void LayoutPins(Vector3 centre, float half)
        {
            var pins = RealmPinBoard.Pins;
            int wanted = pins != null ? pins.Count : 0;
            EnsurePool(_pinPool, wanted, RealmPinShape.Ring, ElarionUi.Parchment, 14f, 14f);

            for (int i = 0; i < _pinPool.Count; i++)
            {
                var dot = _pinPool[i];
                if (i >= wanted)
                {
                    if (dot.gameObject.activeSelf) dot.gameObject.SetActive(false);
                    continue;
                }

                var pin = pins[i];
                // "You" is already the centre disc, and "Objective" already has its diamond —
                // mirroring them would double-draw the two marks the player reads most.
                if (pin.Kind == RealmPinKind.You || pin.Kind == RealmPinKind.Objective)
                {
                    if (dot.gameObject.activeSelf) dot.gameObject.SetActive(false);
                    continue;
                }

                // REGION-ANCHORED pins belong to the parchment map, not to this widget: they
                // carry a region id and (0,0) for metres. Projecting one would plant it on
                // the WORLD ORIGIN — every such pin stacked next to the Heart, reading as
                // "there is a dungeon right there". A pin that lies about WHERE is worse
                // than a pin that is absent, so this widget declines to draw them at all.
                if (!pin.WorldAnchored)
                {
                    if (dot.gameObject.activeSelf) dot.gameObject.SetActive(false);
                    continue;
                }

                var world = new Vector3(pin.WorldX, centre.y, pin.WorldZ);
                if (SqrPlanarDistance(world, centre) > RadiusWorld * RadiusWorld)
                {
                    if (dot.gameObject.activeSelf) dot.gameObject.SetActive(false);
                    continue;
                }

                if (i < _pinStyledAs.Count && _pinStyledAs[i] != (int)pin.Kind &&
                    i < _pinImages.Count && _pinImages[i] != null)
                {
                    var style = RealmAtmosphereStyle.Pin(pin.Kind);
                    // Reset to the pool's authored square before re-shaping, so the shape
                    // maths never compounds off a previous kind's squashed rect.
                    dot.sizeDelta = new Vector2(14f, 14f);
                    ApplyShape(_pinImages[i], dot, style.Shape, style.Tint);
                    _pinStyledAs[i] = (int)pin.Kind;
                }

                if (!dot.gameObject.activeSelf) dot.gameObject.SetActive(true);
                dot.anchoredPosition = Project(world, centre, half);
            }
        }

        // THE projection. World XZ -> plate XY, north-up, hero-centred, linear.
        // Every mark on this widget goes through this ONE function — nothing re-derives it.
        private static Vector2 Project(Vector3 world, Vector3 centre, float half)
        {
            float dx = (world.x - centre.x) / RadiusWorld;
            float dz = (world.z - centre.z) / RadiusWorld;
            return new Vector2(dx * half, dz * half);   // +Z (north) maps to +Y (up)
        }

        private static float SqrPlanarDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return dx * dx + dz * dz;
        }

        // ── the chip: region name + danger tier + threat count, ALWAYS in words ──
        private void UpdateChip(Vector3 centre)
        {
            var zone = Guard.Try("Minimap", "classify zone", () => ZoneManager.ZoneAt(centre), null);
            string name = zone != null && !string.IsNullOrEmpty(zone.DisplayName) ? zone.DisplayName : "The Realm";
            int tier = zone != null ? zone.DangerTier : 0;

            int visible = 0;
            for (int i = 0; i < _threatPool.Count; i++)
                if (_threatPool[i] != null && _threatPool[i].gameObject.activeSelf) visible++;

            string text = tier <= 0 ? name + "  -  Safe" : name + "  -  Tier " + tier;
            if (visible > 0) text += "  -  " + visible + (visible == 1 ? " threat" : " threats");
            SetChip(text);

            // The rim tint tracks the danger tier (WO-828 §4). Decoration ONLY — the tier
            // is already spelled out above, so nothing depends on reading this colour.
            if (_rim != null && tier != _lastTier)
            {
                _lastTier = tier;
                var c = RealmAtmosphereStyle.DangerRim(tier);
                _rim.color = new Color(c.r, c.g, c.b, 0.60f);
            }
        }

        private void SetChip(string text)
        {
            if (_chip == null || _lastChipText == text) return;
            _lastChipText = text;
            _chip.text = text;
        }

        private void RefreshThreats()
        {
            _threatBuf.Clear();
            var list = EnemyProvider != null ? EnemyProvider() : null;
            if (list != null)
                for (int i = 0; i < list.Count; i++)
                    if (list[i] != null) _threatBuf.Add(list[i]);

            // §12 instrumentation: ON CHANGE only, never per-poll. This is the line that
            // splits "the provider is dead" (count stays 0) from "built but invisible"
            // (count > 0 while the owner sees an empty plate) without a second run.
            if (_threatBuf.Count != _lastThreatCount)
            {
                FlowTrace.Step("Minimap",
                    $"threat source count {_lastThreatCount} -> {_threatBuf.Count} " +
                    $"(provider={(EnemyProvider != null ? "wired" : "NULL")}, " +
                    $"heroRef={(_hero != null ? _hero.name : "NULL")}).");
                _lastThreatCount = _threatBuf.Count;
            }
        }

        // ── pooling: grows to the high-water mark, never shrinks, never re-allocs ──
        private void EnsurePool(List<RectTransform> pool, int count, RealmPinShape shape,
                                Color tint, float w, float h)
        {
            bool isPinPool = ReferenceEquals(pool, _pinPool);
            while (pool.Count < count)
            {
                var go = new GameObject(isPinPool ? "Pin" : "ThreatDot",
                                        typeof(RectTransform), typeof(Image));
                go.transform.SetParent(_dotLayer, false);
                var rt = go.GetComponent<RectTransform>();
                CentreAnchor(rt, w, h);
                var img = go.GetComponent<Image>();
                ApplyShape(img, rt, shape, tint);
                go.SetActive(false);
                pool.Add(rt);
                if (isPinPool) { _pinImages.Add(img); _pinStyledAs.Add(-1); }
            }
        }

        private static void HideAll(List<RectTransform> pool)
        {
            for (int i = 0; i < pool.Count; i++)
                if (pool[i] != null && pool[i].gameObject.activeSelf) pool[i].gameObject.SetActive(false);
        }

        // ── shape kit: the ONE place a RealmPinShape becomes pixels ────────────
        // Shape is the colourblind-safe channel, so it gets a single owner here rather
        // than being open-coded per call site where one branch would quietly drift.
        private static void ApplyShape(Image img, RectTransform rt, RealmPinShape shape, Color tint)
        {
            if (img == null) return;
            img.color = tint;
            img.raycastTarget = false;     // the minimap is a READOUT: it never eats a tap
            img.type = Image.Type.Simple;
            Vector2 size = rt != null ? rt.sizeDelta : new Vector2(14f, 14f);
            float unit = Mathf.Max(size.x, size.y);
            if (unit <= 0f) unit = 14f;

            switch (shape)
            {
                case RealmPinShape.Circle:
                    img.sprite = ElarionUiKit.CircleSprite;
                    if (img.sprite == null) ElarionUiKit.ApplyRounded(img);
                    if (rt != null) { rt.localRotation = Quaternion.identity; rt.sizeDelta = new Vector2(unit, unit); }
                    break;
                case RealmPinShape.Ring:
                    img.sprite = ElarionUiKit.RingSprite;
                    if (img.sprite == null) ElarionUiKit.ApplyRounded(img);
                    if (rt != null) { rt.localRotation = Quaternion.identity; rt.sizeDelta = new Vector2(unit, unit); }
                    break;
                case RealmPinShape.Diamond:
                    ElarionUiKit.ApplyRounded(img);
                    if (rt != null) { rt.sizeDelta = new Vector2(unit, unit); rt.localRotation = Quaternion.Euler(0f, 0f, 45f); }
                    break;
                case RealmPinShape.TriangleUp:
                    // The compass's own pip sprite — one owner, one threat silhouette.
                    img.sprite = HudCompassWidget.EnemyPipSprite();
                    if (rt != null) rt.localRotation = Quaternion.identity;
                    break;
                case RealmPinShape.Square:
                    img.sprite = null;      // a plain quad IS the hard square
                    if (rt != null) { rt.localRotation = Quaternion.identity; rt.sizeDelta = new Vector2(unit, unit); }
                    break;
                case RealmPinShape.BarHorizontal:
                    ElarionUiKit.ApplyRounded(img);
                    if (rt != null) { rt.localRotation = Quaternion.identity; rt.sizeDelta = new Vector2(unit, unit * 0.45f); }
                    break;
                case RealmPinShape.BarVertical:
                    ElarionUiKit.ApplyRounded(img);
                    if (rt != null) { rt.localRotation = Quaternion.identity; rt.sizeDelta = new Vector2(unit * 0.45f, unit); }
                    break;
            }
        }

        // ── uGUI helpers (mirror HudCompassWidget's conventions exactly) ───────
        private static void CentreAnchor(RectTransform rt, float w, float h)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = Vector2.zero;
        }

        private static Image AddPlate(string name, RectTransform parent, Color color, float inset)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
            var img = go.GetComponent<Image>();
            ElarionUiKit.ApplyRounded(img);
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static void AddHairline(RectTransform parent, bool horizontal)
        {
            var go = new GameObject(horizontal ? "HairlineH" : "HairlineV",
                                    typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            if (horizontal)
            {
                rt.anchorMin = new Vector2(0.12f, 0.5f);
                rt.anchorMax = new Vector2(0.88f, 0.5f);
                rt.sizeDelta = new Vector2(0f, 1f);
            }
            else
            {
                rt.anchorMin = new Vector2(0.5f, 0.12f);
                rt.anchorMax = new Vector2(0.5f, 0.88f);
                rt.sizeDelta = new Vector2(1f, 0f);
            }
            rt.anchoredPosition = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = new Color(0.604f, 0.498f, 0.243f, 0.22f);
            img.raycastTarget = false;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.AddComponent<RectTransform>();
        }

        private static TextMeshProUGUI AddText(Transform parent, string text, float size,
            Color color, TextAlignmentOptions align)
        {
            var go = new GameObject("Txt");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            return tmp;
        }
    }
}
