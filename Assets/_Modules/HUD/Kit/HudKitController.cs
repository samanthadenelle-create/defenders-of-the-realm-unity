// =============================================================================
// HudKitController — assembles the HUD kit: factory widgets in the actionable
// areas, model-bound, posture-occupied from hud-areas.json (P23 HUDKIT).
// (HUD_OBSIDIAN_ARCHITECTURE_2026-07-03 §3.3/§3.4 as A4 area/posture rows.)
// -----------------------------------------------------------------------------
// Assembly: DeNelle.HUD   Namespace: DeNelle.HUD.Kit
//
// MVVM LAW (§5): every widget is built by the ElarionUiKit factory and bound to
// a Core.HudModel model's Changed event — zero raw widget construction, zero
// state pulls, zero .Instance/reflection reads in this file. Commands fire the
// owner VillageHudController's UnityEvents (Village bridges subscribe those) or
// the Core HudCommands sink (Village registers handlers).
//
// THE §0 FELT FIXES delivered here (mechanism in-line at each site):
//   • HP 9/145 renders ~6%  — vitals = BuildNameplate(Player): the §1.1 fill
//     contract (non-null sprite + fillAmount-only) replaces the sprite-less
//     Filled images of BattleHud9Zone.FillBarLeft (:1701) / VillageHudController.
//   • MP live               — bound to HeroVitalsModel (producer follows
//     HeroHealth.Instance); kills the frozen fillAmount=1f (:1878) + SetMana
//     no-op (:2906).
//   • Dead target clears    — BuildTargetFrame.Bind: TargetModel !HasTarget =>
//     total Clear() (fixes the :549 early-return).
//   • Wave chrome law       — waveBlock exists only in the calm(town) row and
//     self-gates to between-waves phases; countdown shows only when real.
//   • No resource flashing  — CurrencyChip.SetAmount count-tweens; no colour
//     flash exists in the component (owner rule enforced by the factory).
//   • 4 round move buttons  — BuildControllerCluster -> HudMoveInput (replaces
//     the square D-pad + VirtualDPadLean).
//   • Talk button appears   — availability from PostureSignals.TalkAvailable
//     (Core static; the stale one-shot reflection push is retired), consumed via
//     HudActionBarModel (WO-835: the face packs in/out, no dim, no hole).
//   • raid-"x"/harvest-"Y"  — not rebuilt (earns-its-place: no verified
//     backing feature surface); Pi sign-in stays off the HUD (Title-gated).
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;   // heartStatus scene gate (see ApplyHeartSceneGate)
using UnityEngine.UI;
using TMPro;
using DeNelle.Core;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.HUD;
using DeNelle.Core.HudModel;
using DeNelle.Core.UI;

namespace DeNelle.HUD.Kit
{
    /// <summary>Builds, binds and posture-occupies every kit widget (see header).</summary>
    public sealed class HudKitController : MonoBehaviour
    {
        private HudAreasHost _host;
        private PostureEvaluator _evaluator;
        private HudAreasConfig _config;
        private VillageHudController _owner;   // command events (Village bridges subscribe them)
        private IHudModel _models;

        // widget id -> root (registry the occupancy rows drive).
        private readonly Dictionary<string, GameObject> _widgets =
            new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);

        // live handles
        private ElarionUiKit.PartyNameplateHandle _vitals;   // WO-432 shared HP/MP plate (+07-06 XP strip)
        private bool _xpStripBound;                          // one-shot FlowTrace on first XP bind

        // WO-997 §3b: mana-bar legibility. OnVitals now records a TARGET fill (from the
        // model's exact floats when present) and Update() eases the shown fill toward it,
        // so regen reads as motion instead of whole-point steps. A DOWNWARD jump (a spend)
        // arms a brief BRIGHTEN flash on the fill image — brightness, never a hue swap
        // (owner is red/green colourblind). -1 target = nothing bound yet (first push snaps).
        private float _manaFillTarget = -1f;                 // 0..1 fill the bar should reach
        private float _manaFillShown  = -1f;                 // 0..1 fill currently painted
        private float _manaFlashUntil;                       // unscaled time the spend flash ends
        private Color _manaFillBaseColor = Color.white;      // the BUILT fill colour, restored after a flash
        private bool  _manaFillBaseCaptured;
        private const float ManaFillLerpSpeed  = 9f;         // exponential ease rate (~0.11s to 63%)
        private const float ManaSpendThreshold = 0.02f;      // fill drop that counts as a spend
        private const float ManaFlashSeconds   = 0.25f;      // spend flash duration

        // WO-1104 — KILL REWARD VISIBILITY (owner felt-test 2026-08-16, verbatim: "I couldn't
        // tell when I killed in the field. I couldn't tell if it awarded anything... whether
        // it's simply just a flashing on the bar"). Every XP gain is MEASURED here from the
        // model's own state delta (never from an amount some producer claims it granted), then
        // presented two ways: the XP strip BRIGHTENS (flash) and a "+N XP" readout pops just
        // under the plate. Repeat gains inside the merge window ADD INTO the live readout, so a
        // five-kill fight visibly climbs to a bigger number than a one-kill fight — that
        // climbing total IS the "more enemies = more experience" read the owner could not see.
        // Number + brightness, never hue (red/green colourblind law).
        private bool  _xpPrevValid;                          // a baseline has been captured
        private int   _xpPrevXp, _xpPrevToNext, _xpPrevLevel;
        private int   _xpGainRunning;                        // merged total currently displayed
        private float _xpGainLastTime;                       // unscaled time of the last merge
        private float _xpGainHoldUntil;                      // unscaled time the readout starts fading
        private float _xpFlashUntil;                         // unscaled time the strip flash ends
        private Color _xpFillBaseColor = Color.white;        // BUILT strip colour, restored after a flash
        private bool  _xpFillBaseCaptured;
        private const float XpGainMergeSeconds = 1.5f;       // repeat gain inside this window merges
        private const float XpGainHoldSeconds  = 1.6f;       // readable hold before the fade
        private const float XpGainFadeSeconds  = 0.45f;      // fade-out duration
        private const float XpFlashSeconds     = 0.35f;      // strip brighten duration
        private ElarionUiKit.CurrencyChipHandle _wisdomChip;
        private ElarionUiKit.PartyNameplateHandle _heartPlate;   // WO-432: Heart of Elarion on the shared plate
        private ElarionUiKit.TargetFrameHandle _targetFrame;
        private ElarionUiKit.CastBarHandle _castBar;
        private ElarionUiKit.ActionSlotHandle[] _abilitySlots;
        private ElarionUiKit.SoftGlowCooldown[] _abilityGlows;   // WO-611: soft under-glow cooldown (combat HUD only)
        private ElarionUiKit.LockCrosshairHandle _lockBadge;     // WO-611: animated target lock crosshair (combat HUD only)
        private ElarionUiKit.ActionSlotHandle[] _assignableSlots;
        private ElarionUiKit.ActionSlotHandle _hpPotionSlot;
        private ElarionUiKit.ActionSlotHandle _manaPotionSlot;
        private ElarionUiKit.ActionSlotHandle[] _playerStatusSlots;
        private ElarionUiKit.ActionSlotHandle[] _enemyStatusSlots;
        private const int StatusSlotCount = 6;
        private ElarionUiKit.ActionSlotHandle _attackSlot;
        // WO-611 ATTACK PILL rect, in fractions of the actionRail zone. Shared with
        // CombatArcLayout611 (below): the Q/W/E/R medallion arc is computed FROM this
        // pill rect at layout time, so pill and arc can never drift apart again
        // (capture 2026-07-06 battle_hud.png — see BuildAbilityRow).
        internal const float Pill611X0 = 0.30f, Pill611Y0 = 0.02f;
        internal const float Pill611X1 = 0.99f, Pill611Y1 = 0.30f;
        private ElarionUiKit.CurrencyChipHandle[] _resChips;      // expanded row
        private ElarionUiKit.CurrencyChipHandle _resGoldOnly;     // collapsed variant
        private GameObject _resExpandedRow;
        private GameObject _resDock;        // WO-440: right-edge tab + collapsible chips container
        private bool _resPanelOpen;         // WO-440: town collapse state (collapsed by default)
        private Button _fleeButton, _startWaveButton;
        // ── WO-835 action bar (owner architecture law 2026-08-02): the bottom bar renders
        // ONLY the applicable buttons, packed + centered. Every predicate (Talk in range,
        // Raids capable, Map onboarded, Upgrade focus, posture set) lives in the Core
        // HudActionBarModel; this View holds the button GameObjects and renders the array
        // it is passed (ApplyActionBar), NOTHING else. The old Update() gate polls
        // (talk dim / raids dim / map hole-hide / Quests<->Upgrade relabel) are RETIRED
        // from this class — moved into the model.
        private HudActionBarModel _barModel;
        private readonly GameObject[] _barButtons = new GameObject[HudActionBarModel.ButtonCount];
        private readonly RectTransform[] _barButtonRects = new RectTransform[HudActionBarModel.ButtonCount];
        // Constant per-button width (owner default: never resize a face as context
        // changes) sized so the 7-button MAX packs the ActionBar zone exactly; smaller
        // sets keep the SAME width and the group centers ((1 - gap*(max-1)) / max).
        private const float BarGap = 0.01f;

        // WO-911 (ruling Q10+Q13) — the section-3b FLAGGED check, settled AT SOURCE.
        // ---------------------------------------------------------------------------------
        // The question was whether a 6-face bar built on 7-slot geometry leaves a dead trailing
        // slot. Read together, HudActionBarModel and ApplyActionBar answer it: the bar renders
        // HudActionBarModel.Active (a LIST whose length varies), NOT ButtonCount, and
        // ApplyActionBar CENTRES the group (x = (1 - groupW) / 2). A shorter set therefore cannot
        // leave a hole or a trailing gap — it just occupies less width, centred.
        //
        // So the fix is not "drop ButtonCount to 6": that const is the ENUM-IDENTITY bound and the
        // face arrays are indexed by ordinal (Upgrade = 6, with Map kept dormant at 4), so lowering
        // it would put Upgrade out of bounds. The number that genuinely went 7 -> 6 is the maximum
        // number of faces that can be VISIBLE at once, and that is what the slot width must derive
        // from. Both literals are now derived from HudActionBarModel.MaxVisibleFaces, so the next
        // face added or removed cannot silently overflow or under-fill the zone again.
        private const float BarSlotW =
            (1f - BarGap * (HudActionBarModel.MaxVisibleFaces - 1)) / HudActionBarModel.MaxVisibleFaces;
        private const float BarY0 = 0.10f, BarY1 = 0.95f;
        // Raids dim visuals (WO-820 semantics preserved: dim toward Disabled, never
        // uninteractable — the tap still reaches the drillmaster redirect). The DECISION
        // (RaidsDimmed) comes from the model; only the built-colour restore lives here.
        private Image _raidsButtonImage;          // targetGraphic face (built colour cached)
        private TMP_Text _raidsButtonLabel;
        private Color _raidsImageBuiltColor = Color.white;
        private Color _raidsLabelBuiltColor = Color.white;
        private TMP_Text _fleeLabel;
        private TMP_Text _waveLabel, _waveCountdown;
        private ElarionUiKit.BarHandle _waveProgress;
        private GameObject _waveBlockRoot;
        private ElarionUiKit.NameplateHandle[] _cycleRows;
        private string[] _cycleIds;
        private ElarionUiKit.SlideDockHandle _slideDock;   // WO-439: left slide-out (Chat/Ranks/Music/Settings)
        private HudCompassWidget _compass;
        // WO-778: persistent Builders/Training status chip (CoC-feel; polls ObsidianQueueGate.Status).
        private TMP_Text _queueChipLabel;
        private QueueRailView _queueRail;     // WO-864: the CoC card rail replaces the WC3 text rows
        private RectTransform _queueRailMount;   // the Builders EXPANDED section (collapsed by default)
        private int _queueStatusVersion = -1;
        private int _queueRailSyncFrames;        // post-expand re-sync countdown (see SetRailSection)

        // ── RIGHT RAIL, ONE CHIP STYLE (owner ruling 2026-08-05) ────────────────────
        // "I love the builder screen on the right. However, it should be minimized like
        //  everything else, like echoes until needed. The echoes, the builders, and the
        //  resources should all be styled similarly. So they're all the same until you
        //  click and open and expand them."
        // The three rail sections open one at a time; this is the whole arbiter state.
        private enum RailSection { None = 0, Builders = 1, Resources = 2 }
        private RailSection _railOpen = RailSection.None;

        // model subscriptions (for teardown)
        private readonly List<Action> _unsubscribe = new List<Action>();

        private bool _startWaveAvailable;
        private float _chipsExpandUntil;   // collapsed chips: tap-expand window

        // heartStatus scene gate (ApplyHeartSceneGate): the hub test is cached per ACTIVE
        // SCENE (Scene.name allocates a string, and the gate runs on every occupancy apply
        // AND every frame's availability poll), and the decision is logged only when it
        // FLIPS, so a per-frame poll cannot spam the trace.
        private int _heartGateSceneHandle = int.MinValue;
        private bool _heartGateIsHub;
        private string _heartGateSceneName = string.Empty;
        private int _heartGateLogged = -1;   // -1 unknown, 0 hidden, 1 shown

        /// <summary>Build the whole kit under a fresh HudAreasHost.</summary>
        public static HudKitController Create(VillageHudController owner)
        {
            var host = HudAreasHost.Create(owner != null ? owner.transform : null);
            var kit = host.gameObject.AddComponent<HudKitController>();
            kit._owner = owner;
            kit._host = host;
            kit._evaluator = host.gameObject.AddComponent<PostureEvaluator>();
            kit._config = HudAreasConfig.Load();
            kit._models = CoreServices.HudModel;
            kit.BuildWidgets();
            kit.BindModels();
            kit._evaluator.PostureChanged += kit.ApplyPosture;
            kit.ApplyPosture(kit._evaluator.Posture);
            FlowTrace.Step("HudKit", "kit assembled: " + kit._widgets.Count + " widgets, posture " +
                           HudPostureKeys.Key(kit._evaluator.Posture));
            return kit;
        }

        /// <summary>The whole-HUD visibility seam (VillageHudController.SetHudVisible adapter).</summary>
        public void SetHudVisible(bool visible)
        {
            if (_host == null || _host.Group == null) return;
            _host.Group.alpha = visible ? 1f : 0f;
            _host.Group.interactable = visible;
            _host.Group.blocksRaycasts = visible;
        }

        /// <summary>Start-Wave availability push (StartWaveHudBridge -> VillageHudController adapter).</summary>
        public void SetStartWaveAvailable(bool available)
        {
            _startWaveAvailable = available;
            if (_startWaveButton != null) _startWaveButton.gameObject.SetActive(available);
        }

        /// <summary>Repair-prompt push adapter — the shared toast PLUS a factory Repair
        /// button firing RepairConfirmRequested (the WallRepairHudBridge contract; the
        /// old prompt's Cancel is the toast simply expiring).</summary>
        public void ShowRepairToast(string wallLabel, float damagePercent)
        {
            var card = ShowToast(ElarionUiKit.ToastTone.Danger,
                (string.IsNullOrEmpty(wallLabel) ? "A wall" : wallLabel) +
                " is damaged (" + Mathf.RoundToInt(damagePercent) + "%)", lifetime: 6f);
            if (card == null) return;
            ElarionUiKit.BuildObsidianButton(card.transform, "Repair",
                ElarionUiKit.ObsidianButtonStyle.Style2, ElarionUiKit.ObsidianButtonColor.Green,
                new Vector2(0.70f, 0.12f), new Vector2(0.97f, 0.88f), () =>
                {
                    if (_owner != null) _owner.RepairConfirmRequested?.Invoke();
                    Destroy(card);
                });
        }

        /// <summary>Wave-clear push adapter — routes the old no-op banner through the shared toast.</summary>
        public void ShowWaveClearToast(int waveNumber, int enemiesDefeated, string flavourLine)
        {
            string line = "Wave " + waveNumber + " cleared! " +
                          (enemiesDefeated > 0 ? enemiesDefeated + " foes defeated. " : "") +
                          (flavourLine ?? "");
            ShowToast(ElarionUiKit.ToastTone.Confirm, line.TrimEnd());
        }

        private GameObject ShowToast(ElarionUiKit.ToastTone tone, string text, float lifetime = 3.5f)
        {
            var mount = _host != null ? _host.Mount(HudArea.Feedback) : null;
            if (mount == null) return null;
            var parts = ElarionUiKit.ToastCard(mount, tone, accentLeft: true, align: TextAnchor.MiddleCenter);
            var rt = (RectTransform)parts.card.transform;
            rt.anchorMin = new Vector2(0.28f, 0.90f);
            rt.anchorMax = new Vector2(0.72f, 0.97f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            parts.label.text = text ?? "";
            Destroy(parts.card, lifetime);
            FlowTrace.Step("HudKit", "toast: " + text);
            return parts.card;
        }

        // =====================================================================
        // WIDGET CONSTRUCTION — factory-only (§5).
        // =====================================================================

        private void BuildWidgets()
        {
            Transform pool = transform;   // widgets are reparented into areas on ApplyPosture

            // ── vitals: WO-432 shared BuildPartyNameplate (name + HP + MP) ──
            // withXpStrip (owner 07-06): thin gold XP-to-next-level strip under HP/MP, built on
            // the SHARED plate path so it renders in BOTH CombatHud611 flag states (a vitals
            // fact, not combat chrome). Bound from HeroVitalsModel in OnVitals.
            _vitals = ElarionUiKit.BuildPartyNameplate(pool, "Hero",
                new Vector2(0f, 0.35f), new Vector2(1f, 1f), withXpStrip: true);
            if (FeatureFlags.CombatHud611)
            {
                // WO-611 (mockup v8): HP/MP bars RECESSED in an inset WELL inside the plate — a darker
                // sub-panel (#06080b @ 50%) with a 1px darker TOP edge (the inset-shadow read), wrapping
                // both bar rows (StatBars spans 0.06..0.94 x, 0.08..0.55 y in BuildPartyNameplate) so
                // the bars never touch the plate edge. Explicit colours — the kit Well() (Track black
                // @45% + BlinkChrome-gated rim) washed out against the ornate plate in the 07-05 capture.
                // First sibling = above the plate face (the root's own Image) but below name + StatBars.
                var vitalsWell = ElarionUiKit.AddImage(_vitals.Root.transform, "VitalsWell",
                    new Vector2(0.02f, 0.02f), new Vector2(0.99f, 0.60f),
                    new Color(0.024f, 0.031f, 0.043f, 0.50f), rounded: true);
                vitalsWell.GetComponent<Image>().raycastTarget = false;
                var wellTop = ElarionUiKit.AddImage(vitalsWell.transform, "TopEdge",
                    new Vector2(0f, 1f), new Vector2(1f, 1f), new Color(0f, 0f, 0f, 0.55f), rounded: false);
                var wtRt = (RectTransform)wellTop.transform;
                wtRt.pivot = new Vector2(0.5f, 1f);
                wtRt.sizeDelta = new Vector2(-4f, 1.5f);   // 1px-ish darker top edge, inset from the corners
                wtRt.anchoredPosition = Vector2.zero;
                wellTop.GetComponent<Image>().raycastTarget = false;
                vitalsWell.transform.SetAsFirstSibling();
            }
            Register("playerNameplate", WrapAsWidget("playerNameplate", _vitals.Root.gameObject));
            BuildStatusRow(pool, "playerBuffRow", out _playerStatusSlots);

            // Owner F8 07-06: the standalone under-plate XP bar is GONE — it rendered frameless
            // (its HudBarXp frame sprite never drew) and duplicated the in-plate XP strip inside
            // the Knight nameplate (ElarionUiKitNameplate), which is THE one XP display. The
            // hud-areas.json "xpBar" occupancy row is now inert (ApplyPosture iterates only
            // registered widgets), so no data change is required.

            // Owner F8 07-06: Wisdom = skill points; the chip's icon art is a known gap, so
            // "434" read as an unlabeled naked number. "SKILL" text tag is ALWAYS visible
            // (colorblind law: icon + TEXT, never icon-or-nothing).
            _wisdomChip = ElarionUiKit.CurrencyChip(pool, ElarionUiKit.CurrencyKind.Wisdom,
                new Vector2(0.02f, 0.00f), new Vector2(0.34f, 0.16f), tag: "SKILL");
            Register("wisdomChip", WrapAsWidget("wisdomChip", _wisdomChip.root));

            // ── status: wave block (calm(town), between waves only) + heart ──
            BuildWaveBlock(pool);
            // WO-432: the Heart of Elarion status — a tree-of-life glyph + "Elarion" label
            // ABOVE its own gold bar, occupying the HeartStatus area (left, below the hero
            // nameplate). Reads as the world-tree/heart status, NOT a second hero HP bar.
            BuildHeartStatus(pool);

            // targetCycle: up to 4 compact enemy rows -> HudCommands.CycleSelect.
            BuildTargetCycle(pool);

            // ── system: flee ──
            // 3-settings-doors -> 1 (owner cosmetic flag A, 2026-07-24): the top-right "Menu"
            // TEXT button was REMOVED here. It reached the same HelpMenu/Settings card the LEFT
            // gold-gear slide-dock's Settings tab already opens, so it was a duplicate door. The
            // single settings entry point is now the left gear (BuildSlideDock: Chat/Leaderboard/
            // Music/Settings/Pause). hud-areas.json "settingsButton" rows go inert automatically
            // (posture only iterates REGISTERED widgets). Flee stays below.

            // Two-tap arm/confirm (anti-misfire, carried from the retired BattleArenaHud
            // flee button): first tap arms for 2s ("Flee?"), second tap inside the window
            // actually flees; the window expiring disarms back to "Flee".
            _fleeButton = ElarionUiKit.BuildObsidianButton(pool, "Flee",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Red,
                new Vector2(0.10f, 0.05f), new Vector2(0.98f, 0.48f), OnFleeTapped);
            _fleeLabel = _fleeButton.GetComponentInChildren<TMP_Text>();
            Register("fleeButton", WrapAsWidget("fleeButton", _fleeButton.gameObject));

            // ── targetInfo: target frame + cast telegraph ──
            _targetFrame = ElarionUiKit.BuildTargetFrame(pool, new Vector2(0f, 0.35f), new Vector2(1f, 1f));
            if (FeatureFlags.CombatHud611)
            {
                // WO-611 (mockup v8): gold "Lv N" right beside the enemy name. The Blink prefab path
                // (MODE 1) has NO *level* text child (FindDeep "level" -> null), so TargetFrameHandle.
                // Set()'s level write had nowhere to land — the 07-05 capture showed no Lv. Give the
                // handle a label; MODE 2 already has one, which is re-tinted to the ratified gold.
                var gold611 = new Color(0.831f, 0.686f, 0.353f, 1f);   // #d4af5a

                // Capture 2026-07-06 (battle_hud.png): the enemy PORTRAIT circle overhung the
                // plate's LEFT edge. Proven in the prefab bytes: TargetNameplate.prefab's
                // TargetIcon is CENTRE-anchored at a FIXED (-191.1, -0.8) offset, 90x90 px,
                // authored on a 480px-wide root — but InstantiateBlinkPrefab STRETCHES the root
                // to the status-zone rect (~410 px at 720p), so the fixed offset lands the
                // circle past the plate (icon left edge -236 < -205 half-width). Re-anchor it
                // FRACTIONALLY inside the plate (inset left column, aspect-true) so it can
                // never leave the plate at any width. No-op when absent (MODE 2 has none).
                var portrait611 = FindTargetPortrait611(_targetFrame.root.transform);
                if (portrait611 != null)
                {
                    var prt = (RectTransform)portrait611.transform;
                    prt.anchorMin = new Vector2(0.03f, 0.14f);
                    prt.anchorMax = new Vector2(0.21f, 0.86f);
                    prt.offsetMin = Vector2.zero; prt.offsetMax = Vector2.zero;
                    portrait611.preserveAspect = true;
                }

                // TITLE ROW (capture 2026-07-06: "! Orcish Raid" clipped MID-WORD and the gold
                // "Lv 4" overlapped the name/plate edge). Proven causes: the prefab's TargetName
                // is a FIXED 259px centre-anchored box that never reflows with the stretched
                // plate and had no kit fit (MODE 1 skipped FitSingleLine), and the previous Lv
                // label was a FULL OVERLAY of that same box. Fix: one measured row — a container
                // on fractional plate anchors (right of the portrait column); the name takes the
                // LEFT ~72% with the §1.14 bounded auto-size/ellipsize (ElarionUiKit.FitSingleLine,
                // the Forge-title law) and the gold Lv takes the RIGHT slice. The name therefore
                // ellipsizes BEFORE it can touch the Lv text, and neither can reach the plate edge.
                if (_targetFrame.name != null)
                {
                    var nameRt = (RectTransform)_targetFrame.name.transform;
                    var titleRow = new GameObject("TitleRow611", typeof(RectTransform));
                    var rowRt = (RectTransform)titleRow.transform;
                    rowRt.SetParent(nameRt.parent, false);
                    rowRt.SetSiblingIndex(nameRt.GetSiblingIndex());   // keep the name's draw order
                    rowRt.anchorMin = new Vector2(0.24f, 0.72f);
                    rowRt.anchorMax = new Vector2(0.88f, 0.97f);
                    rowRt.offsetMin = Vector2.zero; rowRt.offsetMax = Vector2.zero;

                    nameRt.SetParent(rowRt, false);
                    nameRt.anchorMin = Vector2.zero;
                    nameRt.anchorMax = new Vector2(0.72f, 1f);
                    nameRt.offsetMin = Vector2.zero; nameRt.offsetMax = Vector2.zero;
                    _targetFrame.name.alignment = TextAlignmentOptions.MidlineLeft;
                    ElarionUiKit.FitSingleLine(_targetFrame.name);

                    if (_targetFrame.level == null)
                    {
                        _targetFrame.level = ElarionUiKit.Label(rowRt, "", 0f, 1f, gold611,
                            ElarionUi.FontLabel, TextAlignmentOptions.MidlineRight, 0.74f, 1f, bold: true);
                    }
                    else
                    {
                        // MODE 2 built its own Lv at the plate's far left — move it into the row.
                        var lvRt = (RectTransform)_targetFrame.level.transform;
                        lvRt.SetParent(rowRt, false);
                        lvRt.anchorMin = new Vector2(0.74f, 0f);
                        lvRt.anchorMax = Vector2.one;
                        lvRt.offsetMin = Vector2.zero; lvRt.offsetMax = Vector2.zero;
                        _targetFrame.level.alignment = TextAlignmentOptions.MidlineRight;
                    }
                    ElarionUiKit.FitSingleLine(_targetFrame.level);   // §1.14 — Lv never spills either
                }
                else if (_targetFrame.level == null)
                {
                    // Neither build mode should reach here (both guarantee a name label) —
                    // fall back to a plate-anchored Lv clear of the bar rects.
                    _targetFrame.level = ElarionUiKit.Label(_targetFrame.root.transform, "",
                        0.72f, 0.97f, gold611, ElarionUi.FontLabel,
                        TextAlignmentOptions.MidlineRight, 0.60f, 0.88f, bold: true);
                }
                _targetFrame.level.color = gold611;
                _targetFrame.level.raycastTarget = false;

                // 3-state LOCK BADGE chip (crosshair art + uppercase UNLOCKED/LOCKING/LOCKED word),
                // top-right of the plate beside the Lv text; driven from TargetModel in Update().
                _lockBadge = ElarionUiKit.BuildLockCrosshairBadge(_targetFrame.root.transform,
                    new Vector2(0.72f, 0.02f), new Vector2(0.99f, 0.34f));
            }
            Register("targetFrame", WrapAsWidget("targetFrame", _targetFrame.root));
            BuildStatusRow(pool, "enemyBuffRow", out _enemyStatusSlots);

            _castBar = ElarionUiKit.BuildCastBar(pool, 1, new Vector2(0.08f, 0.02f), new Vector2(0.92f, 0.30f));
            Register("castBar", WrapAsWidget("castBar", _castBar.root));

            // ── actionRail: static W/E/R class kit (WO-609 — bottom-right) ──
            BuildAbilityRow(pool);

            // ── actionBar: hotswap extras + dual potions (WO-609 — bottom-center) ──
            BuildAssignableSkillRow(pool);
            BuildPotionSlots(pool);

            // ── actionRail: the big basic-attack slot ──
            if (FeatureFlags.CombatHud611)
            {
                // WO-611: oblong stadium ATTACK PILL, gold-trimmed, bottom-right thumb anchor.
                // Rect = the shared Pill611* constants — the Q/W/E/R arc derives from them.
                _attackSlot = ElarionUiKit.BuildAttackPill(pool,
                    new Vector2(Pill611X0, Pill611Y0), new Vector2(Pill611X1, Pill611Y1), HudCommands.Attack);
                // WO-899 §3: "attack" now leads the fallback chain. It resolves (concept-icons.json)
                // to RpgUi/abilities/attack_sword — the SAME energy-sword artwork, but circle-masked
                // with transparent corners. The old lead concept "energy-sword" resolves to
                // icons/icon_energy_sword, which is a FULLY OPAQUE RECTANGLE: it painted its own grey
                // background square onto the pill, which is the "pasted sprite / amateur" read the
                // owner reported. Kept as the fallback so a missing file still shows a sword.
                var atkIcon = UiStyle.Icon("attack", "energy-sword", "sword", "melee");
                if (atkIcon != null) _attackSlot.SetIcon(atkIcon);
            }
            else
            {
                _attackSlot = ElarionUiKit.BuildActionSlot(pool,
                    new Vector2(0.22f, 0.02f), new Vector2(0.98f, 0.44f), HudCommands.Attack);
                var atkIcon = UiStyle.Icon("attack", "sword", "melee");
                if (atkIcon != null) _attackSlot.SetIcon(atkIcon);
            }
            Register("attackButton", WrapAsWidget("attackButton", _attackSlot.root));

            // resource chips (expanded row) + collapsed gold-only variant (tap-expand).
            BuildResourceChips(pool);

            // ⚠ OWNER RULING 2026-08-07 — THE BUILDERS CHIP IS RETIRED.
            // Verbatim: "with the manage section in the hud we can remove the open builders queue
            // on right side of hud in town ... since it has a natural home ... i had them expanded
            // in manage tab". The Manage/Queues screen now shows every line (Defense / Buildings /
            // Troops / Research) with progress bars, Finish-Now, cancel+refund and bump - so the
            // right-column chip is duplicate furniture on the busiest edge of the screen.
            //
            // This SUPERSEDES WO-911 ruling Q10/Q13, which kept the chip as a status glance after
            // retiring its double-tap door. That ruling's real intent - exactly ONE Queues entry -
            // is unchanged and now simply lands on the bar's Manage face alone.
            //
            // BuildQueueStatusChip and its QueueRailView are LEFT INTACT below, unreferenced: the
            // rail is the shared component the Manage screen also hosts, and the chip is two lines
            // from returning if the owner wants it back. Deleting them would make a reversal a
            // rewrite.
            // BuildQueueStatusChip(pool);   // retired 2026-08-07 (owner)

            // ── town action bar (WO-835 APPLICABILITY REPACK): Build / Talk / Bag /
            // Raids / Map / Quests / Upgrade ──
            // The bar is no longer a fixed-divisor row. The Core HudActionBarModel computes
            // the ordered ACTIVE set from the context signals (posture, talk range, raid
            // capability, Onboarded, building focus) and ApplyActionBar() renders EXACTLY
            // that array — constant button width, group centered, no holes (owner
            // 2026-08-02: "the visible array should only be the ones active"). Buttons are
            // built here at a placeholder slot and positioned by the render pass; each keeps
            // its own widget id so the hud-areas.json occupancy rows still own the roots.
            // Queues stays retired (owner 2026-08-01): the right-column Builders chip is the
            // one Queues entry (ObsidianQueueRegression 7c enforces).
            var slot0Min = new Vector2(0f, BarY0);
            var slot0Max = new Vector2(BarSlotW, BarY1);

            var build = ElarionUiKit.BuildObsidianButton(pool, "Build",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Yellow,
                slot0Min, slot0Max,
                () => { if (_owner != null) _owner.BuildRequested?.Invoke(); });
            // Carry-over (WO-T2 working-tree intent): the tutorial spotlight target.
            TutorialHighlightRegistry.Register("hud.build_button", (RectTransform)build.transform);
            RegisterBarButton(ActionBarButtonId.Build, "buildButton", build);

            var talk = ElarionUiKit.BuildObsidianButton(pool, "Talk",
                ElarionUiKit.ObsidianButtonStyle.Style2, ElarionUiKit.ObsidianButtonColor.Green,
                slot0Min, slot0Max, () =>
                {
                    FlowTrace.Step("HudKit", "Talk tapped -> HudCommands.Talk + TalkRequested");
                    HudCommands.Talk();
                    if (_owner != null) _owner.TalkRequested?.Invoke();   // legacy bridge compat
                });
            // WO-835: Talk HIDES (repacks out) when no NPC is in range — the model drops it
            // from the array; the old dim-to-0.45 CanvasGroup treatment is retired.
            RegisterBarButton(ActionBarButtonId.Talk, "talkButton", talk);

            var bag = ElarionUiKit.BuildObsidianButton(pool, "Bag",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Gray,
                slot0Min, slot0Max, () =>
                {
                    // Owner 07-06 "Clicking bag doesnt do anything" (RCA log-proven): the two
                    // events below had ZERO live listeners in Main_Castle_Overworld (HeroEquipHud
                    // is scene-whitelisted and never spawned). Route through PanelRouter — the
                    // scene-independent Core opener HeroInventoryController registers at boot.
                    // The legacy events still fire for any listener that DOES exist (hub scenes).
                    FlowTrace.Step("HudKit", "Bag tapped -> PanelRouter.Open(Inventory)");
                    PanelRouter.Open(PanelId.Inventory);
                    if (_owner != null) _owner.InventoryRequested?.Invoke();
                    VillageHudController.RaiseInventoryRequested();
                });
            RegisterBarButton(ActionBarButtonId.Bag, "bagButton", bag);

            // Queues entry (owner 2026-08-01): the bar's Queues button was RETIRED — the
            // right-column Builders chip (BuildQueueStatusChip, QueueStatus band above the
            // resources dock) already taps into ObsidianQueueGate.RequestToggle and is the
            // one Queues entry. ObsidianQueueRegression 7c enforces the retirement.

            // RAIDS (owner F8 2026-07-30 "there is no raid option"): the raid loop's only HUD
            // door was the OLD VillageHudController crossed-swords icon — the kit rendered no
            // raid widget at all. Kit button -> Core RaidEntryGate -> Village RaidEntryBridge
            // -> RaidSelectionScreen (whose Open carries the WO-813 zero-troops safety net).
            // Base word from the model (WO-1008) so the live label and the model's dim-state
            // labels can never drift apart.
            var raids = ElarionUiKit.BuildObsidianButton(pool, HudActionBarModel.RaidsBaseLabel,
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Gray,
                slot0Min, slot0Max, () =>
                {
                    FlowTrace.Step("HudKit", "Raids tapped -> RaidEntryGate.RequestOpen");
                    RaidEntryGate.RequestOpen();
                });
            // WO-835 §3d (owner default 1): Raids HIDES when the player cannot raid at all
            // (no barracks / no troops / flag off — the model's RaidCapable input). The
            // WO-820 full-army gate is PRESERVED on a visible face: dim toward Disabled,
            // never uninteractable, so the tap still reaches the drillmaster redirect.
            // Capture the face image + label with their BUILT colours so ApplyRaidsDim can
            // restore exactly what the kit built.
            RegisterBarButton(ActionBarButtonId.Raids, "raidsButton", raids);
            _raidsButtonImage = raids != null ? raids.targetGraphic as Image : null;
            _raidsButtonLabel = raids != null ? raids.GetComponentInChildren<TMP_Text>(true) : null;
            if (_raidsButtonImage != null) _raidsImageBuiltColor = _raidsButtonImage.color;
            if (_raidsButtonLabel != null) _raidsLabelBuiltColor = _raidsButtonLabel.color;

            // MAP — ⚠ NO LONGER A BAR FACE (WO-911, owner ruling Q10+Q13, 2026-08-06).
            // The Realm Map moved INTO Bag as a tab, which is half of how the bar went 7 -> 6
            // faces without needing an 8th slot. The route itself is unchanged and still live:
            // PanelId.RealmMap -> RealmMapPanel (registered by DeNelle.Village at boot), now
            // reached from the Bag tab row. Nothing is built here, so no widget is registered
            // under "mapButton" and the hud-areas.json calm(town) row drops it in the same
            // commit. ActionBarButtonId.Map stays DORMANT at ordinal 4 (never masked in) so the
            // other faces keep their indices.

            // QUESTS (WO-835 §3c): its OWN always-in-town face — the 07-06 Quests<->Upgrade
            // relabel hijack is retired (owner: "allows quests to be active more often").
            var quests = ElarionUiKit.BuildObsidianButton(pool, "Quests",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Gray,
                slot0Min, slot0Max, OnQuestsAction);
            RegisterBarButton(ActionBarButtonId.Quests, "questButton", quests);

            // MANAGE — the RE-POINTED Upgrade face (WO-911, ruling Q10+Q13).
            // -----------------------------------------------------------------
            // Same enum value (ActionBarButtonId.Upgrade = 6), same widget id
            // ("upgradeButton"), same hud-areas.json row — RE-POINTED, NOT ADDED. That is what
            // dissolves the 8th-face problem: no enum extension, no ButtonCount bump, no new
            // canonical row. Only the LABEL and the DESTINATION change.
            //
            // It is no longer a context face: the model now packs it in whenever the town bar is
            // up, because it is the single door onto all three production lines. Gating it on a
            // focused building is exactly the undiscoverability WO-911 exists to remove.
            var manage = ElarionUiKit.BuildObsidianButton(pool, "Manage",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Gray,
                slot0Min, slot0Max, OnManageAction);
            RegisterBarButton(ActionBarButtonId.Upgrade, "upgradeButton", manage);

            // ── moveCluster -> HudMoveInput ──
            if (FeatureFlags.CombatHud611)
            {
                // WO-899 §1 (owner felt-test): the boxy steel D-PAD is replaced by a clean
                // ANALOG STICK — a base ring + a knob that tracks the thumb and emits a
                // CONTINUOUS -1..1 deflection (magnitude = distance/radius). Same gate, same
                // widget id, same HudMoveInput.Set contract, so HeroLocomotion is untouched.
                //
                // BuildVirtualDPad is kept as the FALLBACK (not as the flag-OFF branch — the
                // flag-OFF path must stay byte-identical to the shipping 4-round-button
                // cluster, WO-611 law). It is used only if the stick's guarded construction
                // failed, so the move widget can never be missing.
                var stick = ElarionUiKit.BuildAnalogStick(pool, new Vector2(0.5f, 0.5f), HudMoveInput.Set);
                if (stick == null || stick.root == null)
                {
                    FlowTrace.Warn("HudKit", "analog stick unavailable -- falling back to the WO-611 virtual D-pad.");
                    stick = ElarionUiKit.BuildVirtualDPad(pool, new Vector2(0.5f, 0.5f), HudMoveInput.Set);
                }
                Register("moveCluster", WrapAsWidget("moveCluster", stick.root));
            }
            else
            {
                // THE FOUR ROUND BUTTONS (§1.11).
                var cluster = ElarionUiKit.BuildControllerCluster(pool, new Vector2(0.5f, 0.5f), HudMoveInput.Set);
                Register("moveCluster", WrapAsWidget("moveCluster", cluster.root));
            }

            // ── dock: WO-439 LEFT slide-out (Chat/Leaderboard/Music/Settings), gear tab ──
            // (hidden entirely in build mode via the occupancy rows, same "chatDock" widget id).
            BuildSlideDock(pool);

            // ── status: the COMMON compass widget (navigation cue) ──
            // The reusable kit compass — cardinal heading + gold objective/region-gate
            // bearing + red enemy ticks. Placed by the hud-areas.json "compass" rows into
            // the top-centre Status area in BOTH calm(town) and calm(explore). Presentation
            // reads the world through provider delegates (wired below); owns no state.
            _compass = HudCompassWidget.Create(pool);
            WireCompassProviders(_compass);
            Register("compass", WrapAsWidget("compass", _compass.gameObject));

            // ── feedback: the CombatTextLayer marker (its own capped/pooled canvas) ──
            var fb = new GameObject("FeedbackLayerMarker", typeof(RectTransform));
            fb.transform.SetParent(pool, false);
            if (Application.isPlaying) { var _ = CombatTextLayer.Instance; }   // ensure the layer exists
            Register("feedbackLayer", fb);
        }

        // Wire the compass' presentation-only world readers. DeNelle.HUD keeps its
        // "HUD -> Core only" edge, so the hero/seam/enemy transforms are resolved by
        // REFLECTION against the DeNelle.Village types (the same loose-reflection seam
        // HudKit already uses for the jukebox / DailyQuest bridges). The compass polls
        // these on a ~4 Hz throttle, so the FindObjects scans never hit the hot path.
        private static void WireCompassProviders(HudCompassWidget compass)
        {
            if (compass == null) return;
            var heroT = Type.GetType("DeNelle.Village.HeroLocomotion, DeNelle.Village");
            var linkT = Type.GetType("DeNelle.Village.HeroLinkCrossing, DeNelle.Village");
            var enemyT = Type.GetType("DeNelle.Village.Enemy, DeNelle.Village");
            Transform heroCache = null;

            compass.HeroProvider = () =>
            {
                if ((heroCache == null || !heroCache) && heroT != null)
                {
                    var o = UnityEngine.Object.FindAnyObjectByType(heroT) as Component;
                    heroCache = o != null ? o.transform : null;
                }
                return heroCache;
            };

            // Nearest region-gate seam crossing (HeroLinkCrossing markers) to the hero =
            // "where do I go" — points at the gate to leave town, and the way home in the open.
            compass.ObjectiveProvider = () =>
            {
                if (linkT == null) return (Vector3?)null;
                var hero = compass.Hero;
                if (hero == null || !hero) return (Vector3?)null;
                Vector3 hp = hero.position;
                float best = float.MaxValue; Vector3? bestPos = null;
                var found = UnityEngine.Object.FindObjectsByType(linkT, FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                foreach (var o in found)
                {
                    if (o is Component c && c != null)
                    {
                        float d = (c.transform.position - hp).sqrMagnitude;
                        if (d < best) { best = d; bestPos = c.transform.position; }
                    }
                }
                return bestPos;
            };

            var enemyBuf = new List<Transform>();
            compass.EnemyProvider = () =>
            {
                enemyBuf.Clear();
                if (enemyT == null) return enemyBuf;
                var found = UnityEngine.Object.FindObjectsByType(enemyT, FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                foreach (var o in found)
                    if (o is Component c && c != null) enemyBuf.Add(c.transform);
                return enemyBuf;
            };
        }

        // A stretch wrapper so every widget occupies its area mount uniformly.
        private GameObject WrapAsWidget(string id, GameObject content)
        {
            var wrap = new GameObject("Widget_" + id, typeof(RectTransform));
            wrap.transform.SetParent(transform, false);
            var rt = (RectTransform)wrap.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            content.transform.SetParent(wrap.transform, false);
            return wrap;
        }

        private void Register(string id, GameObject root)
        {
            _widgets[id] = root;
            root.SetActive(false);   // occupancy rows switch widgets on
        }

        // WO-835: capture a bar face for the model-driven render pass. The widget wrap
        // (occupancy-owned root) registers exactly as before; the INNER button starts
        // hidden — ApplyActionBar() activates + positions exactly the model's array
        // (the WO-826 Map inner-button precedent, generalized to every face).
        private void RegisterBarButton(ActionBarButtonId id, string widgetId, Button button)
        {
            _barButtons[(int)id] = button.gameObject;
            _barButtonRects[(int)id] = (RectTransform)button.transform;
            Register(widgetId, WrapAsWidget(widgetId, button.gameObject));
            button.gameObject.SetActive(false);
        }

        private void BuildWaveBlock(Transform pool)
        {
            // Labels + progress + Start Wave (all factory pieces).
            // WO-432: NO olive Panel() slab — a bare transparent container so the wave
            // labels/progress/button read cleanly against the scene (kill the block bg).
            _waveBlockRoot = new GameObject("WaveBlock", typeof(RectTransform));
            _waveBlockRoot.transform.SetParent(pool, false);
            var wbrt = (RectTransform)_waveBlockRoot.transform;
            wbrt.anchorMin = Vector2.zero; wbrt.anchorMax = Vector2.one;
            wbrt.offsetMin = Vector2.zero; wbrt.offsetMax = Vector2.zero;
            // BUTTON/LABEL HEIGHT FIX (F8 2026-07-08): the Start Wave button was authored at only
            // 17% of the block (y 0.01-0.18) → a ~25px label rect that CULLED its glyphs (guard
            // FAIL "0 visible glyphs, rect 333x25"). The stack is re-flowed UPWARD so the button
            // gets ~33% of the block (a ~48px label rect that seats the readable font); the labels
            // + progress bar shift up proportionally so nothing overlaps.
            _waveLabel = ElarionUiKit.Label(_waveBlockRoot.transform, "", 0.70f, 0.99f,
                ElarionUi.Parchment, ElarionUi.FontHead, TextAlignmentOptions.Center, 0.04f, 0.96f, bold: true);
            _waveCountdown = ElarionUiKit.Label(_waveBlockRoot.transform, "", 0.49f, 0.68f,
                ElarionUi.Gilt, ElarionUi.FontLabel, TextAlignmentOptions.Center, 0.04f, 0.96f, bold: true);
            _waveProgress = ElarionUiKit.BuildObsidianBar(_waveBlockRoot.transform,
                ElarionUiKit.ObsidianBarKind.Stat, new Vector2(0.08f, 0.38f), new Vector2(0.92f, 0.46f),
                withValue: false, framed: false);
            _startWaveButton = ElarionUiKit.BuildObsidianButton(_waveBlockRoot.transform, "Start Wave",
                ElarionUiKit.ObsidianButtonStyle.Style2, ElarionUiKit.ObsidianButtonColor.Green,
                new Vector2(0.22f, 0.01f), new Vector2(0.78f, 0.34f),
                () => { if (_owner != null) _owner.StartWaveRequested?.Invoke(); });
            // Carry-over (WO-T2 working-tree intent): the tutorial spotlight target.
            TutorialHighlightRegistry.Register("hud.wave_button", (RectTransform)_startWaveButton.transform);
            _startWaveButton.gameObject.SetActive(false);
            Register("waveBlock", WrapAsWidget("waveBlock", _waveBlockRoot));
        }

        // =====================================================================
        // THE RIGHT RAIL — ONE COLLAPSED CHIP STYLE, THREE INSTANCES
        // ---------------------------------------------------------------------
        // Owner ruling 2026-08-05 (verbatim at the _railOpen field). The device
        // review (docs/qa/UI_REVIEW_2026-08-05_seeker.md, findings 11 + 13 + P2
        // "three different right edges") measured three DIFFERENT treatments on
        // one column: Builders drew as a large permanently-expanded gold-bordered
        // panel, Echoes as a small dark chip, Resources as BOTH a chip AND a
        // headered panel that repeated the word and overlapped the chip.
        //
        // The fix is a single authored chip: same height (AT the touch floor),
        // same width, same Style1/Gray obsidian face, same right edge, collapsed
        // by default; the expanded section hangs below it and only ONE section is
        // ever open (SetRailSection). Every number below is FIXED REFERENCE PIXELS,
        // never a fraction of the parent band — the WO-841/WO-852 defect class is a
        // sub-MinTouchPx fraction band that ClampMinTouch then grows symmetrically
        // about its centre, closing the gap to its neighbour.
        //
        // The width + gutter deliberately MATCH the third chip, which this class
        // does not own: EchoUnlockFeedback.cs:56-60 authors EchoChipWidthPx = 220
        // and insets it ElarionUi.PadPanel * 3 (54 ref px) from the SCREEN edge.
        // HudRailGutter (bottom of this file) reproduces that screen-relative inset
        // for chips whose parent is an AREA mount, so all three share one edge.
        /// <summary>Collapsed chip height — authored AT the tap floor, so ClampMinTouch
        /// has nothing to grow (growth is what pushed WO-868's chip off-screen).</summary>
        private const float RailChipHeightPx = ElarionUiKit.MinTouchPx;   // 112
        /// <summary>Collapsed chip width — == EchoUnlockFeedback.EchoChipWidthPx.</summary>
        private const float RailChipWidthPx = 220f;
        /// <summary>Gap between a chip and its expanded section.</summary>
        private const float RailGapPx = 6f;
        /// <summary>Expanded-section width. Shares the chip's right edge, grows LEFT.</summary>
        private const float RailPanelWidthPx = 420f;
        /// <summary>THE shared rail gutter: distance from the SCREEN's right edge to every
        /// chip and every expanded panel. == the Echoes chip's authored inset, so the one
        /// rail element this class cannot edit lands on the same edge.</summary>
        internal const float RailGutterPx = ElarionUi.PadPanel * 3f;   // 54
        // Resource panel interior, in fixed reference px. Sized so the whole section
        // (chip 112 + gap 6 + panel 240 = 358) stays inside the ActionRail band, which
        // resolves to ~367 ref px on the 2670x1200 Seeker (HudAreasHost 0.040..0.420).
        private const float ResRowHeightPx = 40f;
        private const float ResRowGapPx = 5f;
        private const float ResPanelPadPx = 10f;

        // WO-778: the always-visible CoC-style Builders chip — busy count, tap opens
        // the WORK QUEUE. Player copy: "Builders"/"Training" — never "Obsidian".
        // ASCII-only, text-encoded state (never colour-only).
        // WO-864: the expanded section is a CoC-style CARD RAIL (QueueRailView).
        // 2026-08-05: that rail is now COLLAPSED BY DEFAULT — the owner keeps the
        // panel she likes, it just starts minimized like its two neighbours.
        private void BuildQueueStatusChip(Transform pool)
        {
            var root = new GameObject("QueueStatusChip", typeof(RectTransform));
            root.transform.SetParent(pool, false);
            var rrt = (RectTransform)root.transform;
            rrt.anchorMin = Vector2.zero; rrt.anchorMax = Vector2.one;
            rrt.offsetMin = Vector2.zero; rrt.offsetMax = Vector2.zero;

            // THE shared collapsed chip (identical build to the Resources chip below).
            var chip = BuildRailChip(rrt, "BuildersChip", "Builders", 0f, OnBuildersChipTapped);
            // Wrap + bounded auto-size come from BuildRailChip (see the note there) — the chip
            // reports the SAME string it always did (FormatQueueChip), it just no longer has to
            // ellipsize the Train count off the end of a narrower face.
            _queueChipLabel = chip != null ? chip.GetComponentInChildren<TMP_Text>(true) : null;
            // WO-1012 P3 (TIMERS beat): the FTUE spotlights this chip for its one line on
            // build timers — same registry contract as hud.build_button (line ~490 above).
            if (chip != null)
                TutorialHighlightRegistry.Register("hud.builders_chip", (RectTransform)chip.transform);

            // The Builder card rail. The SHARED component (DeNelle.Core.UI.QueueRailView) —
            // the Work Queue modal hosts the very same one, so the two surfaces can never
            // show a different queue visual. This host supplies only the mount; the rail
            // owns its own chrome, card anatomy and cheap tick. Height comes from the
            // component (HeightOf), never a guessed literal, so the section can never
            // reserve more band than the cards occupy.
            _queueRailMount = RailBand(rrt, "QueueRailMount",
                RailChipHeightPx + RailGapPx,
                QueueRailView.HeightOf(QueueRailView.Options.Default),
                RailPanelWidthPx);
            _queueRail = QueueRailView.Build(_queueRailMount, DeNelle.Core.Jobs.ChannelId.Builder,
                QueueRailView.Options.Default);
            _queueRailMount.gameObject.SetActive(false);   // collapsed by default

            Register("queueStatusChip", WrapAsWidget("queueStatusChip", root));
        }

        // ⚠ WO-911 (ruling Q10, 2026-08-06) — THE CHIP IS NO LONGER A DOOR.
        // ---------------------------------------------------------------------
        // The 2026-08-01 rule was "there is exactly ONE Queues entry". That rule is intact; what
        // changed is WHICH entry. The bar's re-pointed Manage face is now the single door, so the
        // chip SURVIVES as a STATUS GLANCE ONLY (count + timer + the inline peek rail) and its
        // second tap no longer raises ObsidianQueueGate.RequestToggle. Leaving both would give the
        // player two doors and the "one Queues entry" rule nothing left to mean.
        //
        // This also retires B4: the only way into the queue used to be an undiscoverable
        // DOUBLE-TAP on a status chip (ObsidianQueueHud.OpenWorkQueue had zero live callers).
        //
        // The chip's own oracle row (queueStatusChip in hud-areas.json) is unaffected.
        private void OnBuildersChipTapped()
        {
            // Plain toggle: tap to peek the inline card rail, tap again to collapse it.
            if (_railOpen == RailSection.Builders)
            {
                SetRailSection(RailSection.None);
                FlowTrace.Step("HudKit", "Builders chip collapsed (status glance only — the Manage bar face is the door).");
                return;
            }
            SetRailSection(RailSection.Builders);
        }

        /// <summary>THE one collapsed rail chip. Echoes, Builders and Resources are the same
        /// object with a different word on it: Style1/Gray obsidian face, fixed 220x112 ref px,
        /// pinned to the shared rail gutter. Fixed pixels only (WO-841).</summary>
        private Button BuildRailChip(RectTransform parent, string name, string label,
                                     float yFromTopPx, Action onTap)
        {
            var band = RailBand(parent, name + "Band", yFromTopPx, RailChipHeightPx, RailChipWidthPx);
            var btn = ElarionUiKit.BuildObsidianButton(band, label,
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Gray,
                Vector2.zero, Vector2.one, onTap);
            if (btn == null)
            {
                FlowTrace.Warn("HudKit", "rail chip '" + name + "' did not build - the kit returned no button");
                return null;
            }
            btn.gameObject.name = "RailChip_" + name;
            // BuildObsidianButton arms FitSingleLine (no-wrap + ellipsis), which is right for a
            // wide bar face and WRONG here: the chip is 220 ref px wide because that is the width
            // of the Echoes chip it must match, and "Builders 1/2 | Train 3" cannot seat 22
            // characters on one 200 px line above the kit's 30 px legibility floor — it would
            // ellipsize the Train count away, and the collapsed chip is the only HUD surface that
            // reports it. The chip is 112 px TALL, so the label wraps instead: FitBlock keeps the
            // same bounded auto-size and legibility floor, uses the height we already reserved,
            // and nothing is clipped or dropped. Single-word chips ("Resources") are unaffected.
            var lbl = btn.GetComponentInChildren<TMP_Text>(true);
            if (lbl != null) ElarionUiKit.FitBlock(lbl);
            return btn;
        }

        /// <summary>A FIXED-PIXEL band pinned to the TOP-RIGHT of <paramref name="parent"/> and
        /// snapped onto the shared rail gutter by <see cref="HudRailGutter"/>. The kit's anchor
        /// helpers take fractions; rail chrome must not (WO-841) — a fraction band can resolve
        /// under MinTouchPx, and ClampMinTouch then grows it about its centre into its neighbour.</summary>
        private static RectTransform RailBand(RectTransform parent, string name,
            float yFromTopPx, float heightPx, float widthPx)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.sizeDelta = new Vector2(widthPx, heightPx);
            rt.anchoredPosition = new Vector2(0f, -yFromTopPx);
            go.AddComponent<HudRailGutter>();
            return rt;
        }

        /// <summary>THE rail arbiter: at most ONE expanded section, ever. Reused by the Builders
        /// chip, the Resources chip and the calm(explore) tap-expand window, so no caller can
        /// stack two panels on the right column.</summary>
        private void SetRailSection(RailSection section)
        {
            bool builders = section == RailSection.Builders;
            bool resources = section == RailSection.Resources;
            if (_railOpen == section &&
                (_queueRailMount == null || _queueRailMount.gameObject.activeSelf == builders) &&
                (_resExpandedRow == null || _resExpandedRow.activeSelf == resources)) return;

            _railOpen = section;
            if (_queueRailMount != null && _queueRailMount.gameObject.activeSelf != builders)
                _queueRailMount.gameObject.SetActive(builders);
            // Repaint what was hidden. QueueRailView.Sync() takes its cheap text-only path
            // unless the measured width moved — and on the FIRST expand the rail's rect has
            // never been through a layout pass (it was built and deactivated in one frame),
            // so sync AGAIN one frame later, once the fixed-px band has resolved. Without
            // that second pass the cards would keep a zero-width shape until the publisher's
            // next 1 s tick happened to move the version.
            if (builders && _queueRail != null) { _queueRail.Sync(); _queueRailSyncFrames = 2; }
            else _queueRailSyncFrames = 0;

            _resPanelOpen = resources;
            if (_resExpandedRow != null && _resExpandedRow.activeSelf != resources)
                _resExpandedRow.SetActive(resources);

            FlowTrace.Step("HudKit", "right rail: expanded section = " + section +
                           " (one open at a time; the other two stay collapsed)");
        }

        private void ToggleRailSection(RailSection section) =>
            SetRailSection(_railOpen == section ? RailSection.None : section);

        // "Builders 1/2" (+ " | Training N"). NO TIMER — WO-864 bug 1: the chip used to
        // print the soonest countdown AND the job row printed the same value right under
        // it, so the owner saw "3m 13s" twice, stacked. Exactly ONE surface owns a
        // countdown now, and it is the card that the countdown belongs to.
        private static string FormatQueueChip(ObsidianQueueGate.WorkQueueStatus s)
        {
            if (!s.Available) return "Builders";
            string line = "Builders " + s.BuilderBusy + "/" + s.BuilderSlots;
            // "Train", not "Training": at 1920x1080 the longer word ellipsized to
            // "Trainin..." in the 2026-08-03 capture. The counts are the load-bearing part.
            //
            // NEWLINE, NOT " | " (2026-08-05, from a 2670x1200 capture): this chip's font
            // draws the numeral 1 as a BARE VERTICAL STROKE with no flag or base, so
            // "Builders 1/2 | Train 1" rendered as three identical vertical marks carrying
            // three different meanings - two counts and a separator. The pipe was the worst
            // offender because it sat directly between the digits it was being confused with.
            // The chip is MinTouchPx tall and its label wraps, so the height for a second
            // line is already reserved and costs nothing. Two short lines also survive a
            // narrow chip better than one wrapped line.
            // The underlying glyph problem was wider than this chip and is now FIXED AT THE
            // SOURCE (2026-08-05, same day): ElarionUiKit's numeral-legibility gate measured the
            // Body role font (Alata) drawing '1' at 7.23 ink units against its own 'l' 6.84 and
            // '|' 6.14, rejected it, and fell every FontRole.Body surface through to the default
            // chain. The two lines STAY regardless: this chip is 220 ref px wide and 112 tall, so
            // one wrapped line would still ellipsize the Train count off a legible face, and the
            // height for a second line is already reserved. Layout reason, no longer a font one.
            if (s.TrainBusy > 0) line += "\nTrain " + s.TrainBusy;
            return line;
        }

        // WO-432: Heart of Elarion status cluster — a tree-of-life glyph + "Elarion" caption
        // sitting ABOVE its own gold Heart bar, so the whole widget reads as the world-tree /
        // heart status (occupied into the HeartStatus area on the left, below the nameplate)
        // and can never be mistaken for a second hero HP bar. Factory-only (§5).
        private void BuildHeartStatus(Transform pool)
        {
            var root = new GameObject("HeartStatus", typeof(RectTransform));
            root.transform.SetParent(pool, false);
            var rt = (RectTransform)root.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            // Tree-of-life glyph (OUR icon via the concept resolver; hidden if the art is
            // absent — the nameplate carries the "Elarion" caption) marking this as the
            // world tree's status.
            var mark = new GameObject("HeartMark", typeof(Image));
            mark.transform.SetParent(root.transform, false);
            var mrt = (RectTransform)mark.transform;
            mrt.anchorMin = new Vector2(0.02f, 0.30f); mrt.anchorMax = new Vector2(0.15f, 0.95f);
            mrt.offsetMin = Vector2.zero; mrt.offsetMax = Vector2.zero;
            var markImg = mark.GetComponent<Image>();
            markImg.preserveAspect = true; markImg.raycastTarget = false;
            var markSprite = UiStyle.Icon("tree");
            if (markSprite != null) markImg.sprite = markSprite; else mark.SetActive(false);

            // WO-432: the Heart of Elarion now renders on the SHARED PartyNameplate builder
            // (name = "Heart of Elarion" + a single HP bar). Only HealthFill is used; the mana row is
            // hidden so it reads as the world-tree/heart status, never a second hero MP bar.
            // (ASCII name; the old "♥" heart glyph tofu'd on the build font.)
            _heartPlate = ElarionUiKit.BuildPartyNameplate(root.transform, "Heart of Elarion",
                new Vector2(0.16f, 0.02f), new Vector2(0.99f, 0.98f));
            if (_heartPlate.ManaFill != null)
            {
                _heartPlate.ManaFill.fillAmount = 0f;
                var manaBg = _heartPlate.ManaFill.transform.parent;   // ManaBackground row
                if (manaBg != null) manaBg.gameObject.SetActive(false);
            }

            Register("heartStatus", WrapAsWidget("heartStatus", root));
        }

        private void BuildAbilityRow(Transform pool)
        {
            var row = new GameObject("AbilityRow", typeof(RectTransform));
            row.transform.SetParent(pool, false);
            var rrt = (RectTransform)row.transform;
            rrt.anchorMin = Vector2.zero; rrt.anchorMax = Vector2.one;
            rrt.offsetMin = Vector2.zero; rrt.offsetMax = Vector2.zero;

            _abilitySlots = new ElarionUiKit.ActionSlotHandle[4];
            bool combat = FeatureFlags.CombatHud611;
            if (combat) _abilityGlows = new ElarionUiKit.SoftGlowCooldown[4];

            // WO-611 arc611 REDESIGN (capture 2026-07-06 battle_hud.png, 1280x720): the previous
            // arc placed medallion centres as em-FRACTIONS of the actionRail ZONE rect
            // (Em611 = 0.093, mockup offsets Q(8.9,2.5)..R(2.5,7.1) em from the zone's
            // bottom-right). The zone is 0.780-0.995 x 0.040-0.420 of the screen (~275x274 px
            // at 720p), so the medallions scattered across the whole zone — captured at
            // Q~(985,570) W~(1015,505) E~(1077,460) R~(1150,450) — instead of hugging the
            // ~160x50 attack pill at ~(1100,648). Zone fractions scale with the ZONE, never
            // with the PILL. FIX: CombatArcLayout611 (below the controller) recomputes the
            // medallion rects at layout time FROM THE PILL RECT in pill-height units
            // (mockup em ~ pillHeight/3.5): diameter ~0.9x pill height, arcing from
            // just-left-of-pill-top sweeping up over the pill, adjacent medallions nearly
            // touching (gap ~15% of diameter). The pill rect derives from this row's own
            // rect via the shared Pill611* fractions — both widgets stretch the SAME
            // actionRail mount, so no cross-widget reference (and no drift) is possible.
            // Q sits nearest the pill's left, the arc sweeps up over the pill (owner design).
            // WO-750 mobile-input ruling (owner 2026-07-19): this is a touch game — the ability
            // ICON carries identity, so the medallions render with NO Q/W/E/R key-letter badge.
            // The keyboard/gamepad bindings stay live in code (PC/dev fallback); they are just
            // never surfaced on the touch HUD. Pass null keyBadge -> StyleAsRoundMedallion builds
            // no key chip.

            for (int i = 0; i < 4; i++)
            {
                int slot = i;
                Vector2 min, max;
                if (combat)
                {
                    // Placeholder rect — CombatArcLayout611 assigns the real pill-relative
                    // rect once the row's layout resolves (rect is 0x0 at build time).
                    min = Vector2.zero;
                    max = new Vector2(0.01f, 0.01f);
                }
                else
                {
                    min = new Vector2(i * 0.25f + 0.01f, 0.05f);
                    max = new Vector2((i + 1) * 0.25f - 0.01f, 0.95f);
                }
                _abilitySlots[i] = ElarionUiKit.BuildActionSlot(row.transform, min, max,
                    () => { if (_owner != null) _owner.AbilityRequested?.Invoke(slot); });
                if (combat)
                {
                    // WO-750: null keyBadge — no Q/W/E/R letter on the touch medallion (icon = identity).
                    ElarionUiKit.StyleAsRoundMedallion(_abilitySlots[i], null);
                    _abilityGlows[i] = ElarionUiKit.AddSoftCooldownGlow(_abilitySlots[i]);
                }
            }
            if (combat)
            {
                var arc = row.AddComponent<CombatArcLayout611>();
                var meds = new RectTransform[4];
                for (int i = 0; i < 4; i++) meds[i] = (RectTransform)_abilitySlots[i].root.transform;
                arc.Medallions = meds;
            }
            Register("abilityRow", WrapAsWidget("abilityRow", row));
        }

        private void BuildAssignableSkillRow(Transform pool)
        {
            var row = new GameObject("AssignableSkillRow", typeof(RectTransform));
            row.transform.SetParent(pool, false);
            var rrt = (RectTransform)row.transform;
            bool combat = FeatureFlags.CombatHud611;
            // WO-611: the hot-swap bar (+ the potions region to its right) HOUSED full-width; the
            // potion slots (separate, later-registered widgets in the same ActionBar mount) render
            // over the housing. Non-combat keeps the WO-609 left-2/3 row.
            rrt.anchorMin = new Vector2(0f, 0.05f);
            rrt.anchorMax = new Vector2(combat ? 1f : 0.68f, 0.95f);
            rrt.offsetMin = Vector2.zero; rrt.offsetMax = Vector2.zero;

            if (combat)
                ElarionUiKit.BuildActionBarHousing(row.transform, Vector2.zero, Vector2.one);

            _assignableSlots = new ElarionUiKit.ActionSlotHandle[4];
            for (int i = 0; i < 4; i++)
            {
                int slot = i;
                // Combat: pack the 4 hot-swap slots into the left ~62% so the potions sit to the right,
                // both housed. Non-combat: the original quarter-width row.
                float x0 = combat ? i * 0.15f + 0.02f : i * 0.25f + 0.01f;
                float x1 = combat ? (i + 1) * 0.15f - 0.005f : (i + 1) * 0.25f - 0.01f;
                float y0 = combat ? 0.12f : 0.05f, y1 = combat ? 0.88f : 0.95f;
                _assignableSlots[i] = ElarionUiKit.BuildActionSlot(row.transform,
                    new Vector2(x0, y0), new Vector2(x1, y1),
                    () => HudCommands.AssignableCast(slot));
                // WO-611 (capture 07-05): the tan/khaki Blink Action_Bar_Slot faces dominated the
                // housed bar — restyle each housed slot as the mockup's obsidian steel cell.
                if (combat) ElarionUiKit.StyleAsObsidianCell(_assignableSlots[i]);
            }
            Register("assignableSkillRow", WrapAsWidget("assignableSkillRow", row));
        }

        private void BuildStatusRow(Transform pool, string widgetId, out ElarionUiKit.ActionSlotHandle[] slots)
        {
            var row = new GameObject(widgetId, typeof(RectTransform));
            row.transform.SetParent(pool, false);
            var rrt = (RectTransform)row.transform;
            // Sit below the nameplate/target frame in the shared area mount (WO-609 layout).
            rrt.anchorMin = new Vector2(0f, 0f);
            rrt.anchorMax = new Vector2(1f, 0.38f);
            rrt.offsetMin = Vector2.zero;
            rrt.offsetMax = Vector2.zero;

            slots = new ElarionUiKit.ActionSlotHandle[StatusSlotCount];
            for (int i = 0; i < StatusSlotCount; i++)
            {
                float w = 1f / StatusSlotCount;
                float x0 = i * w + 0.005f, x1 = (i + 1) * w - 0.005f;
                slots[i] = ElarionUiKit.BuildActionSlot(row.transform,
                    new Vector2(x0, 0.05f), new Vector2(x1, 0.95f));
                if (slots[i].button != null) slots[i].button.interactable = false;
                slots[i].root.SetActive(false);
            }
            Register(widgetId, WrapAsWidget(widgetId, row));
        }

        private void BuildPotionSlots(Transform pool)
        {
            _hpPotionSlot = ElarionUiKit.BuildActionSlot(pool,
                new Vector2(0.70f, 0.10f), new Vector2(0.83f, 0.95f), HudCommands.Potion);
            var healIcon = UiStyle.Icon("potion", "consumable", "heal");
            if (healIcon != null) _hpPotionSlot.SetIcon(healIcon);
            // Owner ruling 2026-08-05: the potion badges are STACK counts, not ability charges —
            // an empty larder must render a literal ASCII "0" rather than the blank face a
            // single-potion stack shows. Opt in per-slot; every other action slot is unchanged.
            _hpPotionSlot.showZero = true;
            Register("hpPotionSlot", WrapAsWidget("hpPotionSlot", _hpPotionSlot.root));

            _manaPotionSlot = ElarionUiKit.BuildActionSlot(pool,
                new Vector2(0.85f, 0.10f), new Vector2(0.99f, 0.95f), HudCommands.ManaPotion);
            // Owner ruling 2026-08-05 ("the other one looks like a crystal, I think that should be
            // a mana potion"): resolve the MANA POTION concept. concept-icons.json maps
            // mana -> role 'potion' / potion_mana (the blue flask art already on disk at
            // Resources/RpgUi/potion/potion_mana.png). The fallback chain deliberately NO LONGER
            // ends in "crystal" — that currency-crystal sprite IS the wrong icon the owner
            // reported, so if the mana art ever fails to load we degrade to another POTION shape
            // rather than silently reintroducing the defect.
            var manaIcon = UiStyle.Icon("mana", "potion", "consumable");
            if (manaIcon != null) _manaPotionSlot.SetIcon(manaIcon);
            _manaPotionSlot.showZero = true;   // stack semantics, same ruling as the HP slot
            Register("manaPotionSlot", WrapAsWidget("manaPotionSlot", _manaPotionSlot.root));

            if (FeatureFlags.CombatHud611)
            {
                // WO-611 (mockup v8): the two potions are ROUND in the housed action bar — the
                // medallion face without a key badge (they overlay the obsidian housing, killing
                // the tan Blink slot faces the 07-05 capture showed).
                ElarionUiKit.StyleAsRoundMedallion(_hpPotionSlot);
                ElarionUiKit.StyleAsRoundMedallion(_manaPotionSlot);
            }

            // Owner ruling 2026-08-05 (quantity on the quick action): give both potion slots the
            // kit's fixed-pixel STACK badge. MUST run AFTER StyleAsRoundMedallion — that call can
            // add a GoldRim child, and whatever is parented last draws on top; badge-then-rim
            // would bury the number under the medallion ring.
            ElarionUiKit.StyleAsStackBadge(_hpPotionSlot);
            ElarionUiKit.StyleAsStackBadge(_manaPotionSlot);
        }

        private void BuildTargetCycle(Transform pool)
        {
            var col = new GameObject("TargetCycle", typeof(RectTransform));
            col.transform.SetParent(pool, false);
            var crt = (RectTransform)col.transform;
            crt.anchorMin = Vector2.zero; crt.anchorMax = Vector2.one;
            crt.offsetMin = Vector2.zero; crt.offsetMax = Vector2.zero;

            _cycleRows = new ElarionUiKit.NameplateHandle[4];
            _cycleIds = new string[4];
            for (int i = 0; i < 4; i++)
            {
                float x0 = i * 0.25f + 0.005f, x1 = (i + 1) * 0.25f - 0.005f;
                _cycleRows[i] = ElarionUiKit.BuildNameplate(col.transform, ElarionUiKit.NameplateKind.Enemy,
                    new Vector2(x0, 0.05f), new Vector2(x1, 0.95f));
                int idx = i;
                // Tap-to-select: a Button over the compact plate, firing the Core command.
                // (§5 note, reported: the factory nameplate ships raycast-off with no tap
                // helper — a BuildNameplate onTap / TapTarget kit ask is filed to P1; this
                // AddComponent<Button> is the sanctioned interim, no visuals constructed.)
                var btn = _cycleRows[i].root.GetComponent<Button>();
                if (btn == null) btn = _cycleRows[i].root.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                if (_cycleRows[i].plate != null) _cycleRows[i].plate.raycastTarget = true;
                btn.onClick.AddListener(() =>
                {
                    if (!string.IsNullOrEmpty(_cycleIds[idx])) HudCommands.CycleSelect(_cycleIds[idx]);
                });
                _cycleRows[i].root.SetActive(false);
            }
            Register("targetCycle", WrapAsWidget("targetCycle", col));
        }

        private void BuildResourceChips(Transform pool)
        {
            // WO-431: Gold + Wood/Iron/Food/Crystal chips live in an OBSIDIAN dark frame
            // (near-black ObsidianFill + gold inner rim, never the olive Panel()).
            // Each chip draws OUR resource icon through the CurrencyChip concept resolver
            // (concept-icons.json gold/wood/iron/food/crystal -> Icons_Obsidian) — the icon
            // choice is DATA, never hard-coded here. Count-tween only, NO flash.
            // WO-440: the always-visible resources panel lives in a DOCK — a chip + the
            // collapsible panel the chip toggles. Collapsed by default. SetResources (OnEconomy)
            // updates the chip values whether the panel is open or closed (labels persist).
            //
            // 2026-08-05 REBUILD (device review findings 11 + 13, and the P2 "three different
            // right edges"). What was measured on the Seeker, and what each line below fixes:
            //   * the word "Resources" rendered TWICE — once on the collapsed tab, once as a
            //     gold panel header in a different size and colour, with the panel's top edge
            //     OVERLAPPING the tab. The header is GONE; the chip owns the word, once.
            //   * the tab was a Style1/YELLOW fraction-anchored box with an icon stacked over
            //     the word; its two neighbours were Style1/Gray text chips. It is now the SAME
            //     BuildRailChip as Builders and the same as Echoes: Style1/Gray, 220x112 fixed.
            //   * the panel was a ContentSizeFitter frame pinned -6 px off the AREA's right
            //     edge (the area itself ends only 0.005 of the screen from the edge), so the
            //     numbers ran into the screen edge with no padding. It is now a fixed-width
            //     RailBand on the SHARED rail gutter (HudRailGutter), like every other element.
            //   * rows were ICON-ONLY. CurrencyChip DROPS its text tag whenever the currency
            //     icon resolves (ElarionUiKitObsidian.cs:846-857), so on a device with the art
            //     present the five rows were distinguishable mainly by HUE at ~30 px — a
            //     straight breach of the colourblind rule. Each row now carries its NAME as a
            //     sibling label the chip cannot suppress, in its own sub-rect so it can never
            //     collide with the amount.
            _resDock = new GameObject("ResourceDock", typeof(RectTransform));
            _resDock.transform.SetParent(pool, false);
            var drt = (RectTransform)_resDock.transform;
            drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one;
            drt.offsetMin = Vector2.zero; drt.offsetMax = Vector2.zero;

            var kinds = new[]
            {
                ElarionUiKit.CurrencyKind.Gold, ElarionUiKit.CurrencyKind.Wood,
                ElarionUiKit.CurrencyKind.Iron, ElarionUiKit.CurrencyKind.Food,
                ElarionUiKit.CurrencyKind.Crystal,
            };
            var names = new[] { "Gold", "Wood", "Iron", "Food", "Crystal" };

            // The collapsed chip — THE shared rail chip, identical to Builders and Echoes.
            BuildRailChip(drt, "ResourcesChip", "Resources", 0f,
                          () => ToggleRailSection(RailSection.Resources));

            // The expanded section: a fixed-pixel panel on the shared gutter, hanging under
            // the chip. Fixed rows, never a fraction of the band (WO-841).
            float panelH = ResPanelPadPx * 2f + kinds.Length * ResRowHeightPx +
                           (kinds.Length - 1) * ResRowGapPx;
            var rrt = RailBand(drt, "ResourceChips", RailChipHeightPx + RailGapPx,
                               panelH, RailPanelWidthPx);
            _resExpandedRow = rrt.gameObject;

            // Obsidian dark frame + gold inner rim (reused kit chrome, near-black ObsidianFill
            // — NOT the olive Panel()).
            var frame = ElarionUiKit.AddImage(_resExpandedRow.transform, "ResFrame",
                Vector2.zero, Vector2.one, ElarionUiKit.ObsidianFill, rounded: true);
            ElarionUiKit.AddInnerRim(frame, ElarionUiKit.ObsidianTrim);
            var frameImg = frame.GetComponent<Image>();
            if (frameImg != null) frameImg.raycastTarget = false;

            _resChips = new ElarionUiKit.CurrencyChipHandle[kinds.Length];
            for (int i = 0; i < kinds.Length; i++)
            {
                // One row = a fixed-pixel band inside a fixed-pixel panel. Display only (no
                // tap target), so the MinTouchPx floor does not apply to it and nothing grows.
                var row = new GameObject("ResRow_" + names[i], typeof(RectTransform));
                row.transform.SetParent(_resExpandedRow.transform, false);
                var rowRt = (RectTransform)row.transform;
                rowRt.anchorMin = new Vector2(0f, 1f);
                rowRt.anchorMax = new Vector2(1f, 1f);
                rowRt.pivot = new Vector2(0.5f, 1f);
                rowRt.sizeDelta = new Vector2(-ResPanelPadPx * 2f, ResRowHeightPx);
                rowRt.anchoredPosition =
                    new Vector2(0f, -(ResPanelPadPx + i * (ResRowHeightPx + ResRowGapPx)));

                // NAME — the colourblind-safe identity carrier. A sibling of the chip, in its
                // own left sub-rect, so the icon-first rule inside CurrencyChip cannot drop it
                // and a long amount can never overlap it.
                var nameLbl = ElarionUiKit.Label(row.transform, names[i], 0f, 1f,
                    ElarionUi.Parchment, ElarionUi.FontMicro, TextAlignmentOptions.MidlineLeft,
                    0.02f, 0.44f);
                nameLbl.raycastTarget = false;
                ElarionUiKit.FitSingleLine(nameLbl);   // the name never clips its slot

                // OWNER 2026-07-15 (colorblind): in THIS resource strip Gold must read the SAME
                // size + color as Wood/Iron/Food/Crystal — the earlier primary:Gold gave it gilt
                // digits + FontHead (bigger) + bold (ElarionUiKitObsidian CurrencyChip:859-867),
                // so it stood out. All five chips are peers here; the icon carries identity, never
                // color/size. primary:false makes every chip uniform (Parchment, FontLabel, normal).
                _resChips[i] = ElarionUiKit.CurrencyChip(row.transform, kinds[i],
                    new Vector2(0.46f, 0f), new Vector2(1f, 1f), primary: false,
                    tag: names[i]);
            }

            _resPanelOpen = false;
            _resExpandedRow.SetActive(false);   // collapsed by default
            Register("resourceChips", WrapAsWidget("resourceChips", _resDock));

            // Collapsed variant (calm(explore)): gold chip only; TAP expands the row for 6s.
            // WO-697 icon-first: the coin icon carries identity; "Gold" is the no-art
            // fallback tag only (builder-enforced — the chip is never a naked number).
            _resGoldOnly = ElarionUiKit.CurrencyChip(pool, ElarionUiKit.CurrencyKind.Gold,
                new Vector2(0.05f, 0.82f), new Vector2(1f, 1f), primary: true, tag: "Gold");
            var tapGo = _resGoldOnly.root;
            var tapBtn = tapGo.AddComponent<Button>();
            tapBtn.transition = Selectable.Transition.None;
            tapBtn.onClick.AddListener(() =>
            {
                _chipsExpandUntil = Time.unscaledTime + 6f;
                FlowTrace.Step("HudKit", "resource chips tap-expanded (6s window)");
            });
            _resGoldOnly.plate.raycastTarget = true;   // the chip is the tap target here
            // Same shared gutter as the town rail — one right edge in every posture.
            tapGo.AddComponent<HudRailGutter>();
            Register("resourceChipsCollapsed", WrapAsWidget("resourceChipsCollapsed", tapGo));
        }

        // =====================================================================
        // MODEL BINDING — VM Changed events only (§1.1 rule 4 / §5 rule 3).
        // =====================================================================

        private void BindModels()
        {
            BindActionBar();   // WO-835: model-independent of IHudModel — bind first, always
            var m = _models;
            if (m == null)
            {
                // HudModelHost registers after scene load; retry next frame(s).
                FlowTrace.Warn("HudKit", "CoreServices.HudModel not registered yet — binding deferred");
                InvokeRepeating(nameof(TryLateBind), 0.25f, 0.25f);
                return;
            }
            BindAll(m);
        }

        private void TryLateBind()
        {
            var m = CoreServices.HudModel;
            if (m == null) return;
            CancelInvoke(nameof(TryLateBind));
            _models = m;
            BindAll(m);
        }

        private void BindAll(IHudModel m)
        {
            Sub(m.HeroVitals, OnVitals);        OnVitals();
            Sub(m.Economy, OnEconomy);          OnEconomy();
            Sub(m.Wave, OnWave);                OnWave();
            Sub(m.World, OnWorld);              OnWorld();
            Sub(m.Abilities, OnAbilities);      OnAbilities();
            Sub(m.Assignable, OnAssignable);    OnAssignable();
            Sub(m.Consumables, OnConsumables);  OnConsumables();
            Sub(m.PlayerStatus, OnPlayerStatus); OnPlayerStatus();
            Sub(m.TargetStatus, OnTargetStatus); OnTargetStatus();
            Sub(m.TargetCycle, OnTargetCycle);  OnTargetCycle();
            _targetFrame.Bind(m.Target);
            _castBar.Bind(m.Cast);
            FlowTrace.Step("HudKit", "models bound (vitals/economy/wave/world/abilities/cycle/target/cast)");
        }

        // =====================================================================
        // WO-835 ACTION BAR — the View consumes the Core model's array, only.
        // =====================================================================

        // Bind the shared Core applicability model. The View's ONLY action-bar inputs
        // from here on are ActiveButtonsChanged (render the new array) and
        // RaidsDimmedChanged (tint the Raids face) — zero predicate reads remain.
        private void BindActionBar()
        {
            _barModel = HudActionBarModel.Shared;
            _barModel.ActiveButtonsChanged += ApplyActionBar;
            _unsubscribe.Add(() => _barModel.ActiveButtonsChanged -= ApplyActionBar);
            _barModel.RaidsDimmedChanged += ApplyRaidsDim;
            _unsubscribe.Add(() => _barModel.RaidsDimmedChanged -= ApplyRaidsDim);
            // Sync to the model's CURRENT state (a scene-swap kit binds an already-live
            // shared model whose set may not change again for a while).
            ApplyActionBar();
            ApplyRaidsDim();
        }

        // Render pass (purely mechanical): SetActive + position EXACTLY the buttons in
        // the model's ordered array — constant slot width, group centered in the zone,
        // holes impossible by construction. Runs only on ActiveButtonsChanged (and the
        // bind-time sync), never per-frame.
        private void ApplyActionBar()
        {
            if (_barModel == null) return;
            var active = _barModel.Active;
            int n = active.Count;
            float groupW = n > 0 ? n * BarSlotW + (n - 1) * BarGap : 0f;
            float x = (1f - groupW) * 0.5f;

            for (int i = 0; i < _barButtons.Length; i++)
                if (_barButtons[i] != null && _barButtons[i].activeSelf)
                    _barButtons[i].SetActive(false);

            for (int i = 0; i < n; i++)
            {
                int idx = (int)active[i];
                var rt = _barButtonRects[idx];
                var go = _barButtons[idx];
                if (rt == null || go == null) continue;
                rt.anchorMin = new Vector2(x, BarY0);
                rt.anchorMax = new Vector2(x + BarSlotW, BarY1);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                go.SetActive(true);
                x += BarSlotW + BarGap;
            }
            FlowTrace.Step("HudKit", "action bar repacked: " + n + " face(s) centered");
        }

        // Raids dim visuals (WO-820 semantics via the model's decision): tint face +
        // label toward Disabled, restore the BUILT colours; interactable is never
        // touched, so a dimmed tap still reaches the drillmaster redirect.
        //
        // WO-1008 — COLOUR IS NEVER THE TELL. The owner is red/green colourblind, so a grey
        // tint communicates NOTHING on its own; the face must SAY why it is greyed. The model
        // owns the words (HudActionBarModel.RaidsFaceLabel: "Raids" live, "Raids 0/5" nothing
        // trained, "Raids 3/5" army not full) — this View only paints them. Still zero
        // predicates here: the reason is decided in Core.
        private void ApplyRaidsDim()
        {
            bool dim = _barModel != null && _barModel.RaidsDimmed;
            if (_raidsButtonImage != null)
                _raidsButtonImage.color = dim ? ElarionUi.Disabled : _raidsImageBuiltColor;
            if (_raidsButtonLabel != null)
            {
                _raidsButtonLabel.color = dim ? ElarionUi.Disabled : _raidsLabelBuiltColor;
                string face = _barModel != null ? _barModel.RaidsFaceLabel : HudActionBarModel.RaidsBaseLabel;
                if (!string.IsNullOrEmpty(face) && !string.Equals(_raidsButtonLabel.text, face, StringComparison.Ordinal))
                {
                    _raidsButtonLabel.text = face;
                    FlowTrace.Step("HudKit", "Raids face text -> '" + face + "' (dim=" + dim +
                                   ", reason=" + (_barModel != null ? _barModel.RaidsDimReason.ToString() : "n/a") +
                                   ") - the greyed state is carried in WORDS, never hue alone.");
                }
            }
        }

        private void Sub(HeroVitalsModel m, Action h)    { m.Changed += h; _unsubscribe.Add(() => m.Changed -= h); }
        private void Sub(EconomyModel m, Action h)       { m.Changed += h; _unsubscribe.Add(() => m.Changed -= h); }
        private void Sub(WaveModel m, Action h)          { m.Changed += h; _unsubscribe.Add(() => m.Changed -= h); }
        private void Sub(WorldMetricsModel m, Action h)  { m.Changed += h; _unsubscribe.Add(() => m.Changed -= h); }
        private void Sub(AbilityLoadoutModel m, Action h){ m.Changed += h; _unsubscribe.Add(() => m.Changed -= h); }
        private void Sub(AssignableLoadoutModel m, Action h){ m.Changed += h; _unsubscribe.Add(() => m.Changed -= h); }
        private void Sub(ConsumableHotbarModel m, Action h){ m.Changed += h; _unsubscribe.Add(() => m.Changed -= h); }
        private void Sub(StatusEffectsModel m, Action h) { m.Changed += h; _unsubscribe.Add(() => m.Changed -= h); }
        private void Sub(TargetCycleModel m, Action h)   { m.Changed += h; _unsubscribe.Add(() => m.Changed -= h); }

        private void OnVitals()
        {
            var v = _models != null ? _models.HeroVitals : null;
            if (v == null) return;
            // WO-432: drive the shared PartyNameplate fills directly (fillAmount = hp/maxHp,
            // mp/maxMp). The fill sprites are non-null by contract so uGUI honours fillAmount.
            if (_vitals.HealthFill != null)
                _vitals.HealthFill.fillAmount = v.MaxHp > 0 ? Mathf.Clamp01((float)v.Hp / v.MaxHp) : 0f;
            if (_vitals.ManaFill != null)   // MP LIVE (§0 fix)
            {
                // WO-997 §3b: prefer the EXACT floats (sub-point regen reads); the ints stay
                // the fallback for any producer that never pushed them (sentinel -1).
                float curMana = v.ManaExact    >= 0f ? v.ManaExact    : v.Mana;
                float maxMana = v.MaxManaExact >  0f ? v.MaxManaExact : v.MaxMana;
                float target  = maxMana > 0f ? Mathf.Clamp01(curMana / maxMana) : 0f;
                if (!_manaFillBaseCaptured)
                {
                    _manaFillBaseColor = _vitals.ManaFill.color;
                    _manaFillBaseCaptured = true;
                }
                // A DOWNWARD jump is a spend — arm the brighten flash so burn-down reads.
                if (_manaFillTarget >= 0f && target < _manaFillTarget - ManaSpendThreshold)
                    _manaFlashUntil = Time.unscaledTime + ManaFlashSeconds;
                _manaFillTarget = target;
                if (_manaFillShown < 0f)   // first bind: snap, no ease-in from empty
                {
                    _manaFillShown = target;
                    _vitals.ManaFill.fillAmount = target;
                }
                // Steady-state easing runs in Update() (AnimateManaFill).
            }
            // FIX 2026-08-05: this rendered the CLASS WORD ("Ranger  Lv 1"), so nothing in the
            // game ever told a player who picked the Ranger that he is SYLAS. The nameplate now
            // shows the CANON NAME from Data/Canonical/en.json (hero.<job>.name), resolved through
            // HeroCanonNames in Core - the one reader HUD may legally reach (HUD -> Core only).
            // A missing file/key degrades to exactly the old capitalized class word, so the plate
            // can never go blank. NOTE: the nameplate has NO portrait Image socket today; adding
            // one is a layout change and is deliberately left as a follow-up, not smuggled in here.
            if (_vitals.NameLabel != null)
            {
                string heroName = string.IsNullOrEmpty(v.ClassId)
                    ? "Hero"
                    : DeNelle.Core.State.HeroCanonNames.ForJob(v.ClassId);
                // WO-999: append resource identity (Mana / Vigor / Focus) so the bar reads
                // as a class economy, not generic "MP".
                string res = string.IsNullOrEmpty(v.ResourceDisplayName) ? "" : (" · " + v.ResourceDisplayName);
                _vitals.NameLabel.text = heroName + "  Lv " + Mathf.Max(1, v.Level) + res;
            }
            // Owner 07-06: in-plate XP strip — fillAmount = xp/xpToNext, mirroring the HP/MP
            // fill-binding contract (§1.1). XpToNext<=0 = no HeroProgression data yet (the model
            // default; the producer never pushed) -> strip stays hidden, never blank/stuck-full.
            if (_vitals.XpRow != null)
            {
                bool hasXp = v.XpToNext > 0;
                if (_vitals.XpRow.activeSelf != hasXp) _vitals.XpRow.SetActive(hasXp);
                if (hasXp && _vitals.XpFill != null)
                {
                    _vitals.XpFill.fillAmount = Mathf.Clamp01((float)v.Xp / v.XpToNext);
                    if (!_xpStripBound)
                    {
                        _xpStripBound = true;
                        FlowTrace.Step("HudKit", "xp bar bound " + v.Xp + "/" + v.XpToNext);
                    }
                }
                // WO-1104: MEASURE the gain off this push versus the last one, and present it.
                if (hasXp) NoteXpGain(v.Xp, v.XpToNext, v.Level);
            }
            _wisdomChip.SetAmount(v.Wisdom);
        }

        private void OnEconomy()
        {
            var e = _models != null ? _models.Economy : null;
            if (e == null || _resChips == null) return;
            // Count-tween only — the no-flash law lives in CurrencyChip.SetAmount.
            _resChips[0].SetAmount(e.Gold);
            _resChips[1].SetAmount(e.Wood);
            _resChips[2].SetAmount(e.Iron);
            _resChips[3].SetAmount(e.Food);
            _resChips[4].SetAmount(e.Crystals);
            _resGoldOnly.SetAmount(e.Gold);
        }

        private void OnWave()
        {
            var w = _models != null ? _models.Wave : null;
            if (w == null || _waveBlockRoot == null) return;

            // WAVE-CHROME LAW (§0): the block lives only in the calm(town) row (occupancy)
            // AND self-gates to BETWEEN-waves phases. Countdown shows ONLY when real.
            bool betweenWaves = w.Phase == WavePhase.Idle || w.Phase == WavePhase.Countdown ||
                                w.Phase == WavePhase.Cleared;
            _waveBlockRoot.SetActive(betweenWaves);
            if (!betweenWaves) return;

            // WO-432: the wave label shows ONLY during an actual wave (Number > 0); the
            // village-at-rest state hides the label entirely instead of a resting caption.
            bool hasWave = w.Number > 0;
            _waveLabel.gameObject.SetActive(hasWave);
            if (hasWave) _waveLabel.text = "Wave " + w.Number;
            bool realCountdown = w.Phase == WavePhase.Countdown && w.CountdownRemaining > 0f;
            _waveCountdown.text = realCountdown
                ? "Next wave in " + Mathf.CeilToInt(w.CountdownRemaining) + "s" : "";
            _waveProgress.SetValue(w.EnemiesTotal - w.EnemiesLive, Mathf.Max(1, w.EnemiesTotal));
            _waveProgress.track.gameObject.SetActive(w.EnemiesTotal > 0);
            // Owner 07-06 ("missing option to start wave now... they might be fully ready"):
            // the button used to HIDE during Countdown; with countdown now = active battle it
            // must stay available as the skip. Relabel contextually so one control = one action.
            if (_startWaveButton != null)
            {
                _startWaveButton.gameObject.SetActive(_startWaveAvailable);
                var swLabel = _startWaveButton.GetComponentInChildren<TMP_Text>(true);
                if (swLabel != null)
                {
                    string want = realCountdown ? "Start Now" : "Start Wave";
                    if (swLabel.text != want) swLabel.text = want;
                }
            }
        }

        private void OnWorld()
        {
            var wm = _models != null ? _models.World : null;
            if (wm == null) return;
            // WO-432: the Heart of Elarion drives the shared plate's HealthFill (mana row hidden).
            if (_heartPlate.HealthFill != null)
                _heartPlate.HealthFill.fillAmount = wm.HeartMaxHp > 0
                    ? Mathf.Clamp01((float)wm.HeartHp / wm.HeartMaxHp) : 0f;
        }

        private void OnAbilities()
        {
            var a = _models != null ? _models.Abilities : null;
            if (a == null || _abilitySlots == null) return;
            for (int i = 0; i < _abilitySlots.Length; i++)
            {
                var h = _abilitySlots[i];
                // WO-611: a combat-HUD MEDALLION (glow driver present <=> flag was ON at build) always
                // RENDERS — the 07-05 capture showed NO arc in battle because every slot with no
                // resolved def (AbilityLoadoutProducer: equipped = def != null) was SetActive(false)
                // here, hiding the whole arc on an unassigned loadout. Empty = dimmed medallion + key
                // badge, non-interactable (mockup). Flag OFF (glows null) keeps the shipping behavior
                // byte-identical.
                bool medallion = _abilityGlows != null && i < _abilityGlows.Length && _abilityGlows[i] != null;
                if (i >= a.Slots.Count)
                {
                    h.root.SetActive(medallion);
                    if (medallion) SetEmptyMedallion(h);
                    continue;
                }
                var s = a.Slots[i];
                h.root.SetActive(medallion || s.Equipped);
                if (!s.Equipped)
                {
                    if (medallion) SetEmptyMedallion(h);
                    continue;
                }
                if (medallion)
                {
                    if (h.frame != null) h.frame.color = Color.white;   // un-dim (colours baked in the face)
                    if (h.icon != null) h.icon.enabled = true;
                }
                // OWNER PLACEHOLDER (2026-07-11): an IconKey with the in-band "text:" prefix
                // (AbilityLoadoutProducer sets it for Q/knight.q — "use word Dodge/Attack") renders
                // as a centered TEXT face instead of a sprite. SetLabel hides the icon; SetLabel(null)
                // restores icon mode when the loadout changes back. Cooldown glow/press untouched.
                if (!string.IsNullOrEmpty(s.IconKey) && s.IconKey.StartsWith("text:", System.StringComparison.Ordinal))
                {
                    h.SetLabel(s.IconKey.Substring(5));
                }
                else
                {
                    h.SetLabel(null);
                    h.SetIcon(string.IsNullOrEmpty(s.IconKey) ? null : UiStyle.Icon(s.IconKey));
                }
                // ⭐ WO-1105 REVISION (owner 2026-08-16, verbatim: "change the bow and arrow attack
                // to the action bar and leave the attack as the dagger attack"). The bow is an
                // action-bar ability now, so the word she asked for rides ITS slot: "with [Sylas]
                // ... it should be a picture of a bow and arrow. It should be the word shoot" —
                // BOTH, which is exactly what SetCaption gives (a bottom strip that sits WITH the
                // icon, unlike SetLabel, which replaces the face). Empty verb => no strip, so only
                // abilities that AUTHOR a verb in abilities.json show one; the view holds no class
                // knowledge and could not, since DeNelle.HUD may not reference DeNelle.Village.
                // The word is also what keeps the control readable without colour (owner is
                // red/green colourblind — meaning never rides on hue).
                h.SetCaption(s.Verb);
                // WO-611: combat HUD medallions use the SOFT under-glow; else the hard radial sweep.
                bool cooling = s.CooldownRemaining > 0f && s.CooldownTotal > 0f;
                if (medallion)
                {
                    _abilityGlows[i].Set(s.CooldownRemaining, s.CooldownTotal);
                    // Tap gate: cooling OR unaffordable resource (WO-999 mobile economy).
                    if (h.button != null) h.button.interactable = !cooling && s.Affordable;
                }
                else
                    h.SetCooldown(s.CooldownRemaining, s.CooldownTotal);

                // WO-999: cost digit on the face (count badge). Free skills blank.
                // Direct text so cost "1" is not swallowed by SetCount's charge-badge rule.
                if (h.count != null)
                {
                    int costShown = s.ManaCost > 0.05f ? Mathf.RoundToInt(s.ManaCost) : 0;
                    h.count.text = costShown > 0 ? costShown.ToString() : "";
                }
                // Dim face when unaffordable (not on cooldown-only) — luminance, not hue.
                if (h.frame != null && s.Equipped)
                {
                    float frameAlpha = s.Affordable ? 1f : 0.42f;
                    var c = h.frame.color;
                    h.frame.color = new Color(c.r, c.g, c.b, frameAlpha);
                }
                if (h.icon != null && s.Equipped && h.icon.enabled)
                {
                    float iconAlpha = s.Affordable ? 1f : 0.45f;
                    var c = h.icon.color;
                    h.icon.color = new Color(c.r, c.g, c.b, iconAlpha);
                }
                if (!medallion && h.button != null && s.Equipped)
                    h.button.interactable = !cooling && s.Affordable;
            }
        }

        // WO-611: present an UNASSIGNED combat medallion — dimmed face, no icon, no tap, no stale
        // cooldown text — so the Q/W/E/R arc always renders in hostile postures (combat-HUD only;
        // callers gate on the glow driver's presence, which exists only when the flag was ON at build).
        private static void SetEmptyMedallion(ElarionUiKit.ActionSlotHandle h)
        {
            if (h == null) return;
            h.SetLabel(null);   // 2026-07-11: drop a stale text face (Dodge/Attack) with the icon
            h.SetCaption(null); // WO-1105 REVISION: and the verb strip ("Shoot"), or it outlives its ability
            if (h.icon != null) h.icon.enabled = false;
            if (h.frame != null) h.frame.color = new Color(1f, 1f, 1f, 0.45f);
            if (h.button != null) h.button.interactable = false;
            if (h.cdText != null) h.cdText.text = "";
        }

        private void OnAssignable()
        {
            var a = _models != null ? _models.Assignable : null;
            if (a == null || _assignableSlots == null) return;
            for (int i = 0; i < _assignableSlots.Length; i++)
            {
                var h = _assignableSlots[i];
                if (i >= a.Slots.Count) { h.root.SetActive(true); continue; }
                var s = a.Slots[i];
                h.root.SetActive(true);
                h.SetIcon(string.IsNullOrEmpty(s.IconKey) ? null : UiStyle.Icon(s.IconKey));
                h.SetCooldown(s.CooldownRemaining, s.CooldownTotal);
                if (h.button != null) h.button.interactable = s.Equipped;
            }
        }

        private void OnConsumables()
        {
            var c = _models != null ? _models.Consumables : null;
            if (c == null) return;
            if (_hpPotionSlot != null)
            {
                _hpPotionSlot.SetCount(c.HpPotionCount);
                // Cooldown sweep from the model (state owned by ConsumableUseService). SetCooldown
                // drives cdRing/cdText AND sets interactable=!cooling; re-apply the count gate AFTER
                // so a used-up or unbound potion still greys out even when not cooling.
                _hpPotionSlot.SetCooldown(c.HpCooldownRemaining, c.HpCooldownTotal);
                if (_hpPotionSlot.button != null)
                    // Owner ruling 2026-08-05: the count term is GONE from this predicate. A tap at
                    // ZERO must be RECEIVED so ConsumableUseService's empty-larder branch can tell the
                    // player what is wrong and where to get more — a dead button absorbs the tap with
                    // no trace, which is the silence the owner reported. The COOLDOWN term stays, so a
                    // cooling potion keeps its existing greyed-out behaviour (the sweep already says why).
                    _hpPotionSlot.button.interactable =
                        HudCommands.HasPotion && c.HpCooldownRemaining <= 0f;
            }
            if (_manaPotionSlot != null)
            {
                _manaPotionSlot.SetCount(c.ManaPotionCount);
                _manaPotionSlot.SetCooldown(c.ManaCooldownRemaining, c.ManaCooldownTotal);
                if (_manaPotionSlot.button != null)
                    // Same ruling as the HP slot: tap-at-zero is received (toast), cooldown still greys.
                    _manaPotionSlot.button.interactable =
                        HudCommands.HasManaPotion && c.ManaCooldownRemaining <= 0f;
            }
        }

        private void OnPlayerStatus() => RefreshStatusRow(_playerStatusSlots, _models?.PlayerStatus);
        private void OnTargetStatus() => RefreshStatusRow(_enemyStatusSlots, _models?.TargetStatus);

        private static void RefreshStatusRow(ElarionUiKit.ActionSlotHandle[] slots, StatusEffectsModel model)
        {
            if (slots == null) return;
            var icons = model != null ? model.Icons : null;
            for (int i = 0; i < slots.Length; i++)
            {
                var h = slots[i];
                if (h == null) continue;
                bool has = icons != null && i < icons.Count;
                h.root.SetActive(has);
                if (!has) continue;
                var ic = icons[i];
                h.SetIcon(StatusIcon(ic.IconKey, ic.IsBuff));
                h.SetCount(0);
                h.SetCooldown(ic.RemainingSeconds, Mathf.Max(0.01f, ic.TotalSeconds));
                if (h.button != null) h.button.interactable = false;
            }
        }

        private static Sprite StatusIcon(string id, bool isBuff)
        {
            var s = UiStyle.Icon(id, "status", id);
            if (s != null) return s;
            switch (id)
            {
                case "slow":   s = UiStyle.Icon("ice", "frost", "cold"); break;
                case "freeze": s = UiStyle.Icon("ice", "frost", "cold"); break;
                case "burn":   s = UiStyle.Icon("fire", "flame", "ember"); break;
                case "mana-draught": s = UiStyle.Icon("mana", "potion", "consumable"); break;
            }
            if (s != null) return s;
            return UiStyle.Icon(isBuff ? "buff" : "debuff", "status");
        }

        private void OnTargetCycle()
        {
            var tc = _models != null ? _models.TargetCycle : null;
            if (tc == null || _cycleRows == null) return;
            for (int i = 0; i < _cycleRows.Length; i++)
            {
                bool has = i < tc.Targets.Count;
                _cycleRows[i].root.SetActive(has);
                _cycleIds[i] = has ? tc.Targets[i].Id : null;
                if (!has) continue;
                var t = tc.Targets[i];
                _cycleRows[i].SetName(t.Name);
                _cycleRows[i].hp.SetValue(t.HpFraction, 1f);
            }
        }

        // (WO-835: the old OnTalkChanged dim handler is retired — Talk availability now
        // packs the face in/out through HudActionBarModel; see ApplyActionBar.)

        // Two-tap flee (see BuildWidgets) — arm, confirm-inside-window, or disarm.
        private float _fleeArmedUntil;
        private void OnFleeTapped()
        {
            if (Time.unscaledTime < _fleeArmedUntil)
            {
                _fleeArmedUntil = 0f;
                if (_fleeLabel != null) _fleeLabel.text = "Flee";
                HudCommands.Flee();
                return;
            }
            _fleeArmedUntil = Time.unscaledTime + 2f;
            if (_fleeLabel != null) _fleeLabel.text = "Flee?";
            FlowTrace.Step("HudKit", "flee armed (tap again within 2s to confirm)");
        }

        // WO-440: resource chip toggle — expand/collapse the resource panel. Values are
        // still updated by OnEconomy regardless of this state (labels persist while hidden).
        // 2026-08-05: both entry points now route through the ONE rail arbiter, so opening
        // Resources collapses Builders and vice versa — the right column can never stack two
        // expanded panels (the state that produced the overlapping chip + panel in the review).
        private void ToggleResourcePanel() => ToggleRailSection(RailSection.Resources);

        private void SetResourcePanelOpen(bool open)
        {
            if (open) { SetRailSection(RailSection.Resources); return; }
            if (_railOpen == RailSection.Resources) SetRailSection(RailSection.None);
        }

        // WO-835 §3c: the old OpenQuestOrUpgrade context relabel is SPLIT into two
        // dedicated handlers — Quests always opens the board; Upgrade routes the focused
        // building. (Reading HudBuildingFocus here is COMMAND ROUTING — the tap's target
        // argument — not an applicability predicate; visibility lives in the model.)
        private void OnQuestsAction()
        {
            if (!PanelRouter.Open(PanelId.RumorBoard))
                FlowTrace.Warn("HudKit", "RumorBoard opener not registered — quest board unreachable");
        }

        /// <summary>
        /// WO-911 — the RE-POINTED bar face: the SINGLE door onto the unified Manage/Queues screen.
        /// -------------------------------------------------------------------------------------
        /// It raises <see cref="ObsidianQueueGate.RequestToggle"/>, the existing queue verb, which
        /// ManageScreenPanel now subscribes to. Going through the gate (rather than straight to the
        /// router) keeps ONE opening verb for the queues no matter who raises it, and keeps this
        /// controller's call to it — the thing the [obsidian-queue] oracle requires.
        ///
        /// PanelRouter.Open(PanelId.Manage) is the fallback for the case where the gate has no
        /// subscriber yet (a boot race), so the face is never a dead tap.
        ///
        /// The old context behaviour (focused-building -> BuildingUpgrade panel) is NOT lost: the
        /// Manage screen's tabs list every upgradable building and drill in to that very panel, and
        /// walking up to a building still opens it directly via BuildingInteractable.
        /// </summary>
        private void OnManageAction()
        {
            FlowTrace.Step("HudKit", "Manage face tapped -> ObsidianQueueGate.RequestToggle (WO-911 single door)");
            if (ObsidianQueueGate.HasSubscriber)
            {
                ObsidianQueueGate.RequestToggle();
                return;
            }
            if (!PanelRouter.Open(PanelId.Manage))
                FlowTrace.Warn("HudKit",
                    "Manage tapped but neither the queue gate nor PanelId.Manage has a listener — screen unreachable.");
        }

        // WO-439: the LEFT slide-out dock — a gear tab pinned to the left screen edge (collapsed by
        // default) that slides open a panel with FIVE rows: Chat / Leaderboard / Music / Settings /
        // Pause (Pause folded in 2026-07-24, cosmetic flag A).
        // Built from the shared ElarionUiKit.BuildSlideTab helper; registered under the same "chatDock"
        // widget id so the hud-areas.json occupancy rows are unchanged. The GEAR on the handle is the
        // dock's ONE icon (kit-resolved, gilt plate, ASCII "=" fallback so it never blanks); the rows
        // themselves are label-only — see AddDockTab for why (WO-908).
        private void BuildSlideDock(Transform pool)
        {
            _slideDock = ElarionUiKit.BuildSlideTab(pool, ElarionUiKit.SlideEdge.Left,
                tabYCenter: 0.5f, panelWidthFrac: 0.22f, panelHeightFrac: 0.52f,
                tabIconConcept: "settings");   // GEAR tab (owner: replaces the down "v/>" trigger)

            // F8-12 (owner 2026-07-07 "very small font and cells"): this widget re-parents into
            // the Dock AREA mount — only 23% x 10% of the screen (HudAreasHost Dock rect) — and
            // BuildSlideTab sizes by fraction-of-parent, so the tab + slide-out panel rendered at
            // ~5% of screen. Pin both to FIXED reference pixels (1080x1920 canvas units, the same
            // canonical-CTA discipline) so the tiny mount can't scale them: thumb-size tab on the
            // left edge, real-size panel overlaying when open. Cells/fonts inside are fractions of
            // the PANEL, so they inherit the fix.
            //
            // WO-908 (owner felt-test, Seeker 2670x1200 — capture
            // docs/qa/screens/2026-08-05/gear-menu-double-icon.png): the gear HANDLE and the
            // slide-out PANEL were BOTH pinned to the mount's left edge at anchoredPosition zero,
            // so opening the drawer parked the handle plate ON TOP of the panel — dead centre of
            // its height, which is exactly the MUSIC row (row 2 of 5 is centred on 0.5) — and over
            // the panel's left rim, reading as a second, mis-seated gear. Fix: the handle owns its
            // own FIXED-PIXEL column at the edge and the panel STARTS where that column ends. The
            // handle therefore never moves on toggle (the owner taps the same spot to close), never
            // covers a row, and nothing overhangs the frame. Fixed px, never a parent fraction.
            const float dockTabPx = ElarionUiKit.MinTouchPx;   // 112 - the kit touch floor, verbatim
            var dockPanelRt = _slideDock.panel;
            dockPanelRt.anchorMin = new Vector2(0f, 0.5f);
            dockPanelRt.anchorMax = new Vector2(0f, 0.5f);
            dockPanelRt.pivot = new Vector2(0f, 0.5f);
            dockPanelRt.anchoredPosition = new Vector2(dockTabPx, 0f);   // clear of the handle column
            // Height carries FIVE tabs now (Pause folded in — cosmetic flag A) at ~112px
            // touch targets each: 700 / 5 = 140px slot, well above MinTouchPx. Do NOT shrink 700:
            // AddDockTab's rows resolve to EXACTLY 112px (0.16 * 700), so any smaller panel puts
            // them under the floor and ClampMinTouch would grow them about their centres into each
            // other — the documented WO-852/865/868 overlap trap.
            dockPanelRt.sizeDelta = new Vector2(400f, 700f);
            var dockTabRt = (RectTransform)_slideDock.tab.transform;
            dockTabRt.anchorMin = new Vector2(0f, 0.5f);
            dockTabRt.anchorMax = new Vector2(0f, 0.5f);
            dockTabRt.pivot = new Vector2(0f, 0.5f);
            dockTabRt.anchoredPosition = Vector2.zero;
            dockTabRt.sizeDelta = new Vector2(dockTabPx, dockTabPx);   // was 84 - under the 112 floor

            AddDockTab(_slideDock.panel, 0, "Chat",        OpenClanChat);
            AddDockTab(_slideDock.panel, 1, "Leaderboard", OpenLeaderboard);
            AddDockTab(_slideDock.panel, 2, "Music",       OpenJukebox);
            AddDockTab(_slideDock.panel, 3, "Settings",    OpenSettings);
            // Pause folded into the LEFT gear (cosmetic flag A, 2026-07-24): the standalone
            // top-right pause chip (PauseHudBootstrap.PauseHudButton) was culled to leave ONE
            // door. PauseController/SettingsController stay installed by PauseHudBootstrap; this
            // tab is the caller that opens Pause/Quit-to-Title via PauseGate.RequestBack().
            AddDockTab(_slideDock.panel, 4, "Pause",       () => PauseGate.RequestBack());

            Register("chatDock", WrapAsWidget("chatDock", _slideDock.root));
        }

        // One labelled tab inside the slide-out (stacked vertically, top-to-bottom).
        //
        // WO-908: this row used to stamp a leading concept icon over the button. It was removed,
        // for THREE reasons proven from source, not taste:
        //  1. Of the five row concepts only "settings" is mapped in concept-icons.json (line 165) —
        //     chat / leaderboard / music / pause are absent from the table AND there is no art for
        //     them in Assets/Resources/RpgUi/icons/ at all. So the path could only ever badge ONE
        //     row of five: a per-row icon treatment is not achievable, it is a lone odd row out.
        //  2. BuildObsidianButton's label is a FULL-STRETCH centred TMP (ElarionUiKitObsidian.cs:679),
        //     and the icon was added AFTER it, so the icon drew ON TOP of the row's own label — the
        //     gear sat on the "S" of "Settings" in the felt-test capture.
        //  3. Assets/Resources/RpgUi/icons/icon_settings.png is DARK art; bare (no plate) on the
        //     Gray obsidian face it is a near-contrastless smudge, which is the "formatting is wrong
        //     on colour" half of the report.
        // The menu's one gear is now the drawer HANDLE (BuildSlideTab), which carries the same
        // sprite on the kit's gilt plate. Rows are uniformly label-only — ONE treatment.
        private void AddDockTab(RectTransform panel, int i, string label, Action onTap)
        {
            const int n = 5;   // Chat/Leaderboard/Music/Settings/Pause (Pause folded in, flag A)
            float y1 = 1f - (i / (float)n) - 0.02f;
            float y0 = 1f - ((i + 1) / (float)n) + 0.02f;
            ElarionUiKit.BuildObsidianButton(panel, label,
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Gray,
                new Vector2(0.06f, y0), new Vector2(0.94f, y1), onTap);
        }

        // Settings tab -> the Help/Settings card (same target as the gear/Menu button).
        private void OpenSettings()
        {
            if (DeNelle.HUD.HelpMenu.Instance != null)
                DeNelle.HUD.HelpMenu.Instance.ToggleOverlay();
            else if (!PanelRouter.Open(PanelId.GameGuide))
                FlowTrace.Warn("HudKit", "dock: neither HelpMenu nor GameGuide available for Settings");
        }

        // ── dock intents (parity with the retired SocialAccessCluster) ──────
        private void OpenClanChat()
        {
            var p = FindAnyObjectByType<ClanChatPanel>(FindObjectsInactive.Include);
            if (p != null) p.Toggle();
            else FlowTrace.Warn("HudKit", "dock: ClanChatPanel not present");
        }

        private void OpenLeaderboard()
        {
            var p = FindAnyObjectByType<LeaderboardPanel>(FindObjectsInactive.Include);
            if (p != null) p.Toggle();
            else FlowTrace.Warn("HudKit", "dock: LeaderboardPanel not present");
        }

        private void OpenJukebox()
        {
            // MusicSelectionPanel lives in DeNelle.Audio — the same loose-reflection toggle
            // the retired SocialAccessCluster used (HUD -> Core only, no Audio asmdef edge).
            Guard.Try("HudKit", "dock jukebox toggle", () =>
            {
                var t = Type.GetType("DeNelle.Audio.MusicSelectionPanel, DeNelle.Audio");
                if (t == null) return false;
                var panel = FindAnyObjectByType(t) as MonoBehaviour;
                if (panel == null) return false;
                var toggle = t.GetMethod("Toggle");
                if (toggle == null) return false;
                toggle.Invoke(panel, null);
                return true;
            }, false);
        }

        // =====================================================================
        // POSTURE OCCUPANCY — data rows drive everything (A4).
        // =====================================================================

        private void ApplyPosture(HudPosture posture)
        {
            // WO-611 behavior rules (combat HUD only): on the flip to hostile(prebattle|activebattle)
            // every other screen CLOSES + HIDES so ONLY the combat HUD renders (owner rule 1+2).
            if (FeatureFlags.CombatHud611 &&
                (posture == HudPosture.HostilePrebattle || posture == HudPosture.HostileActiveBattle))
            {
                PanelManager.CloseAll();
                FlowTrace.Step("HudKit", "combat HUD is the active screen: CloseAll (posture " +
                               HudPostureKeys.Key(posture) + ")");
            }

            // COLLAPSED IS THE DEFAULT STATE (owner 2026-08-05). A posture flip re-presents
            // the rail, so it re-presents it minimized — the player never returns to town and
            // finds a panel she left open in another posture already occupying the column.
            SetRailSection(RailSection.None);

            var occupancy = _config.Occupancy(posture);
            int shown = 0;
            foreach (var kv in _widgets)
            {
                HudArea area;
                bool present = occupancy.TryGetValue(kv.Key, out area);
                if (present)
                {
                    var mount = _host.Mount(area);
                    if (mount != null && kv.Value.transform.parent != mount)
                        kv.Value.transform.SetParent(mount, false);
                    if (!kv.Value.activeSelf) kv.Value.SetActive(true);
                    shown++;
                }
                else if (kv.Value.activeSelf) kv.Value.SetActive(false);
            }

            // Dynamic gates on top of the rows (availability, never layout):
            if (_widgets.TryGetValue("fleeButton", out var flee) && flee.activeSelf)
                flee.SetActive(HudCommands.HasFlee);
            ApplyHeartSceneGate(posture);
            // WO-835: relay the posture key to the applicability model (a relay of the
            // notification this View already receives — the key->set mapping lives in
            // the model). A set change comes back as ActiveButtonsChanged -> render.
            if (_barModel != null) _barModel.SetPosture(HudPostureKeys.Key(posture));
            OnConsumables();
            OnPlayerStatus();
            OnTargetStatus();
            OnWave();   // wave block phase gate re-evaluates with the posture

            FlowTrace.Step("HudKit", "occupancy applied: posture " + HudPostureKeys.Key(posture) +
                           " -> " + shown + " widgets live");
        }

        // ---------------------------------------------------------------------
        // heartStatus SCENE GATE (owner felt-test, Seeker: the "Heart of Elarion"
        // bar rendered INSIDE Dungeon_HealersCottage).
        //
        // The Heart is the VILLAGE world-tree; its status bar has no meaning outside a
        // hub/town scene. hud-areas.json RIGHTLY lists heartStatus in calm(town) AND in
        // hostile(prebattle|activebattle) — a wave defence is exactly the situation the
        // bar exists for — but posture alone cannot tell a village wave from a dungeon
        // fight: in the dungeon the evaluator resolved hostile(activebattle) (correctly)
        // and the row fired, so the bar appeared. The row is NOT the bug and must NOT be
        // edited; the missing SCENE test is. So this is a dynamic availability gate ON TOP
        // of the rows — the exact fleeButton/HudCommands.HasFlee precedent above.
        //
        // In the hub every posture that lists heartStatus still shows it: IsHub() is true
        // there, so `want` collapses to pure row membership (today's behaviour, unchanged).
        private void ApplyHeartSceneGate(HudPosture posture)
        {
            GameObject heart;
            if (_config == null || !_widgets.TryGetValue("heartStatus", out heart) || heart == null)
                return;

            var scene = SceneManager.GetActiveScene();
            if (scene.handle != _heartGateSceneHandle)
            {
                _heartGateSceneHandle = scene.handle;
                _heartGateSceneName = scene.name;
                _heartGateIsHub = HubScenes.IsHub(_heartGateSceneName);
                _heartGateLogged = -1;   // re-announce the decision once per scene
            }

            bool inRow = _config.Occupancy(posture).ContainsKey("heartStatus");
            bool want = inRow && _heartGateIsHub;
            if (heart.activeSelf != want) heart.SetActive(want);

            // Project law: a decision leaves a logged line. This runs per occupancy apply
            // AND per frame, so log ONLY on a flip (and once per scene) — never per frame.
            int decision = want ? 1 : 0;
            if (decision == _heartGateLogged) return;
            _heartGateLogged = decision;
            if (inRow && !_heartGateIsHub)
                FlowTrace.Warn("HudKit", "heartStatus: posture " + HudPostureKeys.Key(posture) +
                               " lists it, but scene '" + _heartGateSceneName +
                               "' is not a hub -> scene gate HIDES it (the Heart is village-only)");
            else
                FlowTrace.Step("HudKit", "heartStatus scene gate: " + (want ? "show" : "hide") +
                               " (scene " + _heartGateSceneName + ", hub " + _heartGateIsHub +
                               ", inRow " + inRow + ")");
        }

        // WO-997 §3b: per-frame mana-bar animation. Eases the shown fill toward the model's
        // target (so a 1/s regen is visible MOTION, not a 10% step every second) and runs the
        // spend flash: a brief brighten of the fill toward white that decays back to the BUILT
        // colour. Brightness carries the meaning — never a hue swap (red/green colourblind law).
        private void AnimateManaFill()
        {
            var img = _vitals.ManaFill;
            if (img == null || _manaFillTarget < 0f || _manaFillShown < 0f) return;

            if (!Mathf.Approximately(_manaFillShown, _manaFillTarget))
            {
                // Exponential ease — frame-rate independent, snaps when within a hair.
                float k = 1f - Mathf.Exp(-ManaFillLerpSpeed * Time.unscaledDeltaTime);
                _manaFillShown = Mathf.Lerp(_manaFillShown, _manaFillTarget, k);
                if (Mathf.Abs(_manaFillShown - _manaFillTarget) < 0.0015f)
                    _manaFillShown = _manaFillTarget;
                img.fillAmount = _manaFillShown;
            }

            if (_manaFlashUntil > 0f && _manaFillBaseCaptured)
            {
                float remain = _manaFlashUntil - Time.unscaledTime;
                if (remain <= 0f)
                {
                    img.color = _manaFillBaseColor;
                    _manaFlashUntil = 0f;
                }
                else
                {
                    // 0..1 flash strength, strongest at the spend instant, decaying to 0.
                    float t = Mathf.Clamp01(remain / ManaFlashSeconds);
                    img.color = Color.Lerp(_manaFillBaseColor, Color.white, 0.75f * t);
                }
            }
        }

        // =====================================================================
        // WO-1104 — XP GAIN FEEDBACK (owner felt-test 2026-08-16)
        // =====================================================================

        /// <summary>
        /// MEASURE one XP push against the previous one and, when it is a real gain, arm the
        /// two feedback channels: the strip flash + the "+N XP" readout.
        ///
        /// THE AMOUNT IS A MEASURED STATE DELTA, NOT A REQUESTED GRANT. The HUD never sees
        /// "the grant code asked for 14 XP" — it sees the hero's banked XP move, which is the
        /// only number that proves something actually landed. (Project law: never log/present
        /// the amount requested in place of the amount credited.)
        ///
        /// A level-up is folded in: the carry across the boundary is
        /// (prevToNext - prevXp) + newXp, a FLOOR for a multi-level jump (rare; a single kill
        /// never crosses two levels). A level going DOWN, or an implausibly large jump, is a
        /// save restore / dev grant, not a kill — suppressed and traced, never popped.
        /// </summary>
        private void NoteXpGain(int xp, int xpToNext, int level)
        {
            if (xpToNext <= 0) return;

            if (!_xpPrevValid)
            {
                // First bind is a BASELINE, never a gain (the whole banked total would pop).
                _xpPrevValid = true;
                _xpPrevXp = xp; _xpPrevToNext = xpToNext; _xpPrevLevel = level;
                return;
            }

            int gained;
            if (level == _xpPrevLevel) gained = xp - _xpPrevXp;
            else if (level > _xpPrevLevel) gained = Mathf.Max(0, _xpPrevToNext - _xpPrevXp) + xp;
            else gained = 0;   // level DOWN = a restore/reset, never an award

            int levelsGained = Mathf.Max(0, level - _xpPrevLevel);
            _xpPrevXp = xp; _xpPrevToNext = xpToNext; _xpPrevLevel = level;

            if (gained <= 0) return;

            // Restore/dev-grant guard: a single award never exceeds two levels' worth.
            if (gained > xpToNext * 2)
            {
                FlowTrace.Warn("HudKit",
                    $"XP GAIN SUPPRESSED measuredDelta={gained} (> 2x xpToNext={xpToNext}) - " +
                    "reads as a save restore or dev grant, not a kill award; no pop shown.");
                return;
            }

            float now = Time.unscaledTime;
            bool merged = _xpGainRunning > 0 && (now - _xpGainLastTime) <= XpGainMergeSeconds;
            _xpGainRunning = merged ? _xpGainRunning + gained : gained;
            _xpGainLastTime = now;
            _xpGainHoldUntil = now + XpGainHoldSeconds;
            _xpFlashUntil = now + XpFlashSeconds;

            if (_vitals.XpGainLabel != null)
            {
                // Word + number carry the meaning; the flash is the redundant channel.
                string text = "+" + _xpGainRunning + " XP";
                if (levelsGained > 0) text += "  LEVEL UP";
                SetXpGainText(text);
            }

            // §12 permanent trace: the MEASURED delta, the merged running total, and whether
            // the readout surface actually existed - so a capture can tell "credited but not
            // shown" from "never credited". No hollow assertion: 'measuredDelta' is the state
            // move, not an amount anybody asked for.
            FlowTrace.Step("HudKit",
                $"XP GAIN measuredDelta={gained} runningShown={_xpGainRunning} merged={merged} " +
                $"levels={levelsGained} xp={xp}/{xpToNext} lv={level} " +
                $"labelPresent={(_vitals.XpGainLabel != null)} stripPresent={(_vitals.XpFill != null)}");
        }

        /// <summary>Retext + reveal the gain readout at full alpha (single writer for the label).</summary>
        private void SetXpGainText(string text)
        {
            var lbl = _vitals.XpGainLabel;
            if (lbl == null) return;
            lbl.text = text;
            var c = lbl.color; c.a = 1f; lbl.color = c;
            if (!lbl.gameObject.activeSelf) lbl.gameObject.SetActive(true);
        }

        /// <summary>
        /// WO-1104 per-frame XP feedback: brighten-decay on the strip fill (never a hue swap)
        /// and hold-then-fade on the "+N XP" readout. Cheap; early-outs when nothing is armed.
        /// </summary>
        private void AnimateXpGain()
        {
            // 1) Strip flash — brightness pulse toward white, decaying to the BUILT colour.
            var fill = _vitals.XpFill;
            if (fill != null)
            {
                if (!_xpFillBaseCaptured) { _xpFillBaseColor = fill.color; _xpFillBaseCaptured = true; }
                if (_xpFlashUntil > 0f)
                {
                    float remain = _xpFlashUntil - Time.unscaledTime;
                    if (remain <= 0f) { fill.color = _xpFillBaseColor; _xpFlashUntil = 0f; }
                    else
                    {
                        float t = Mathf.Clamp01(remain / XpFlashSeconds);
                        fill.color = Color.Lerp(_xpFillBaseColor, Color.white, 0.85f * t);
                    }
                }
            }

            // 2) Readout — hold at full alpha, then fade out and retire the running total so
            //    the NEXT fight starts its own count (a fresh climb, not a lifetime tally).
            var lbl = _vitals.XpGainLabel;
            if (lbl == null || !lbl.gameObject.activeSelf) return;
            float over = Time.unscaledTime - _xpGainHoldUntil;
            if (over <= 0f) return;
            var col = lbl.color;
            if (over >= XpGainFadeSeconds)
            {
                col.a = 1f; lbl.color = col;          // reset for the next pop
                lbl.gameObject.SetActive(false);
                _xpGainRunning = 0;
            }
            else
            {
                col.a = 1f - (over / XpGainFadeSeconds);
                lbl.color = col;
            }
        }

        private void Update()
        {
            // WO-997 §3b: ease the hero plate's mana fill toward its target + run the
            // spend flash (brightness pulse, colourblind-safe). Cheap; early-outs when idle.
            AnimateManaFill();

            // WO-1104: run the XP strip flash + the "+N XP" readout hold/fade.
            AnimateXpGain();

            // WO-611: drive the animated lock crosshair badge from the target model (combat HUD only).
            // 0 = no target (unlocked/faint), 1 = target held but not locked (acquiring pulse),
            // 2 = manual lock (locked/gold). Bound to TargetModel.HasTarget/Locked.
            if (_lockBadge != null && _models != null && _models.Target != null)
            {
                var t = _models.Target;
                _lockBadge.SetState(!t.HasTarget ? 0 : (t.Locked ? 2 : 1));
            }

            // WO-835: tick the Core applicability model — it polls the no-event Core
            // statics (focus/onboarded/army version/capability) and edge-triggers
            // ActiveButtonsChanged/RaidsDimmedChanged; this View holds zero predicates.
            // (Replaces the retired Quests<->Upgrade relabel, Raids dim and Map hide
            // polls that used to live right here.)
            if (_barModel != null) _barModel.Tick();

            // Cheap availability polls (no model event exists for these Core statics).
            if (_widgets.TryGetValue("fleeButton", out var flee))
            {
                bool want = HudCommands.HasFlee &&
                            _config.Occupancy(_evaluator.Posture).ContainsKey("fleeButton");
                if (flee.activeSelf != want) flee.SetActive(want);
            }
            // Same cheap poll for the Heart's scene gate: a scene change (hub -> dungeon)
            // need not move the posture, so the ApplyPosture call alone can be missed.
            // Self-throttled (cached hub test, log only on a flip) — see ApplyHeartSceneGate.
            if (_evaluator != null) ApplyHeartSceneGate(_evaluator.Posture);
            // Collapsed chips: the tap-expand window temporarily shows the full row.
            if (_widgets.TryGetValue("resourceChipsCollapsed", out var col) && col.activeSelf &&
                _widgets.TryGetValue("resourceChips", out var row))
            {
                bool expand = Time.unscaledTime < _chipsExpandUntil;
                if (row.activeSelf != expand)
                {
                    if (expand)
                    {
                        var mount = _host.Mount(HudArea.ActionRail);
                        if (mount != null && row.transform.parent != mount) row.transform.SetParent(mount, false);
                    }
                    row.SetActive(expand);
                    // WO-440: the explore tap-window shows the full chips panel (not just the tab).
                    SetResourcePanelOpen(expand);
                }
            }

            // WO-778: Builders chip repaint — poll the Core static (the HudBuildingFocus
            // precedent, no model event); repaint only when the published Version moves
            // (BuildTimerService publishes on QueueChanged + its own 1s tick).
            if (_queueChipLabel != null)
            {
                var qs = ObsidianQueueGate.Status;
                if (qs.Version != _queueStatusVersion)
                {
                    _queueStatusVersion = qs.Version;
                    _queueChipLabel.text = FormatQueueChip(qs);
                    // The card rail (WO-864) is self-driving off the same published Version
                    // and repaints only when the queue SHAPE moves — nothing to do here.
                }
            }

            // One post-expand re-sync, once the newly-shown fixed-px band has been laid out.
            if (_queueRailSyncFrames > 0 && --_queueRailSyncFrames == 0 && _queueRail != null)
                _queueRail.Sync();

            // (WO-835: the Raids army-dim poll and the Map Onboarded poll that lived here
            // moved into HudActionBarModel — the View consumes its events above.)
        }

        private static string Cap(string s) =>
            string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        // WO-611: locate the Blink target frame's portrait circle (prefab child "TargetIcon";
        // future re-skins may say "Portrait"). Null when the constructed MODE-2 frame is live.
        private static Image FindTargetPortrait611(Transform root)
        {
            if (root == null) return null;
            var images = root.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                string n = images[i].name.Replace(" ", "").Replace("_", "");
                if (n.IndexOf("targeticon", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("portrait", StringComparison.OrdinalIgnoreCase) >= 0)
                    return images[i];
            }
            return null;
        }

        private void OnDestroy()
        {
            foreach (var u in _unsubscribe) { try { u(); } catch { /* teardown */ } }
            _unsubscribe.Clear();
            if (_targetFrame != null) _targetFrame.Unbind();
            if (_castBar != null) _castBar.Unbind();
            if (_evaluator != null) _evaluator.PostureChanged -= ApplyPosture;
            HudMoveInput.Set(Vector2.zero);
        }
    }

    /// <summary>
    /// THE SHARED RIGHT-RAIL GUTTER (owner ruling 2026-08-05; device review P2 "the right rail
    /// has three different right edges, the '!' chip runs off-screen").
    ///
    /// WHY A COMPONENT AND NOT A CONSTANT. The rail's elements are parented to HudAreasHost AREA
    /// mounts, which are anchored as FRACTIONS of the screen (QueueStatus and ActionRail both end
    /// at x = 0.995 — HudAreasHost.cs:99/117). 0.005 of the canvas is ~5 ref px at the 1080 author
    /// width and ~11 at the 2670x1200 Seeker, so any inset authored against the AREA lands on a
    /// different SCREEN margin at every aspect — which is exactly how the column ended up with
    /// three right edges. This component measures the live gap between its parent's right edge
    /// and the ROOT CANVAS' right edge and writes the difference into anchoredPosition.x, so the
    /// element's right edge sits <see cref="HudKitController.RailGutterPx"/> reference px from the
    /// SCREEN edge at every resolution — the same 54 ref px EchoUnlockFeedback.cs:381 authors for
    /// the Echoes chip, which lives on a different canvas entirely and cannot be edited from here.
    ///
    /// Presentation-only, allocation-free, and it writes ONLY on an actual change (a dirty flag
    /// plus a screen-size compare), so it is not a per-frame layout cost.
    /// Requires: anchorMin.x == anchorMax.x == 1 and pivot.x == 1 (what RailBand authors).
    /// </summary>
    internal sealed class HudRailGutter : MonoBehaviour
    {
        private static readonly Vector3[] Corners = new Vector3[4];

        private RectTransform _rt, _canvasRt;
        private bool _dirty = true;
        private int _lastW = -1, _lastH = -1;
        private bool _warned;

        private void OnEnable() { _dirty = true; }
        private void OnRectTransformDimensionsChange() { _dirty = true; }

        private void LateUpdate()
        {
            if (Screen.width != _lastW || Screen.height != _lastH)
            {
                _lastW = Screen.width; _lastH = Screen.height; _dirty = true;
            }
            if (!_dirty) return;
            Apply();
        }

        private void Apply()
        {
            if (_rt == null) _rt = transform as RectTransform;
            if (_rt == null) { _dirty = false; return; }
            var parent = _rt.parent as RectTransform;
            if (parent == null) return;                      // re-parented by occupancy; retry
            if (_canvasRt == null)
            {
                var c = GetComponentInParent<Canvas>();
                if (c == null) return;                        // not mounted yet; retry
                var root = c.rootCanvas != null ? c.rootCanvas : c;
                _canvasRt = root.transform as RectTransform;
            }
            if (_canvasRt == null) return;

            if (parent.rect.width < 1f || _canvasRt.rect.width < 1f) return;   // unresolved; retry

            // Gap the parent already provides, measured in CANVAS-LOCAL units (== reference px,
            // the unit RailGutterPx and MinTouchPx are authored in). Going through the canvas'
            // own local space rather than lossyScale keeps this correct whatever scale the HUD
            // host happens to be parented under.
            parent.GetWorldCorners(Corners);
            float parentRight = _canvasRt.InverseTransformPoint(Corners[2]).x;
            float gap = _canvasRt.rect.xMax - parentRight;
            float inset = HudKitController.RailGutterPx - gap;
            if (inset < 0f)
            {
                // The parent already sits further inside than the shared gutter. Snapping the
                // chip back OUT would put it on a fourth edge, so hold at the parent's edge and
                // say so once — a real layout finding, never a silent swallow (CLAUDE.md 12.2).
                if (!_warned)
                {
                    _warned = true;
                    FlowTrace.Warn("HudKit", "rail gutter: parent '" + parent.name + "' already insets " +
                                   gap.ToString("F1") + " ref px > the shared " +
                                   HudKitController.RailGutterPx + " - pinning to the parent edge");
                }
                inset = 0f;
            }

            _dirty = false;
            if (Mathf.Approximately(_rt.anchorMin.x, _rt.anchorMax.x))
            {
                // Point-anchored at the right edge (RailBand) — move it.
                if (Mathf.Abs(_rt.anchoredPosition.x + inset) < 0.01f) return;
                _rt.anchoredPosition = new Vector2(-inset, _rt.anchoredPosition.y);
            }
            else
            {
                // Right-STRETCHED (the calm(explore) collapsed gold chip) — inset its right edge
                // so it lands on the same gutter instead of on a fourth edge.
                if (Mathf.Abs(_rt.offsetMax.x + inset) < 0.01f) return;
                _rt.offsetMax = new Vector2(-inset, _rt.offsetMax.y);
            }
        }
    }

    /// <summary>
    /// WO-611: positions the Q/W/E/R medallions AROUND THE ATTACK PILL, in pill-height units,
    /// at layout time (capture 2026-07-06 battle_hud.png — the previous zone-fraction arc
    /// scattered the medallions across the whole actionRail; see BuildAbilityRow).
    ///
    /// Geometry (mockup em = pillHeight / 3.5):
    ///   pill rect  = the row's own rect x the shared HudKitController.Pill611* fractions
    ///                (the AbilityRow widget wrap and the attackButton wrap both stretch the
    ///                same actionRail mount, so the rects agree by construction — works even
    ///                in hostile(prebattle) where the pill widget itself is unoccupied);
    ///   pivot      = pill top-right corner, inset 1.8em left (keeps the last medallion's
    ///                right edge inside the pill/screen);
    ///   centres    = pivot + 8em * (cos, sin) at 171deg -> 92.7deg (Q lowest-left just above
    ///                the pill's top, sweeping up over the pill to R above its right end);
    ///   diameter   = 0.9x pill height; adjacent centre spacing = 2*8em*sin(13.05deg)
    ///                ~ 3.62em = 1.15x diameter => gap ~15% of a diameter (nearly touching).
    ///
    /// Presentation-only re-layout: taps, icons, key badges, dimmed-empty faces and the soft
    /// cooldown glow all live on the slots themselves and are untouched. Recomputes only on
    /// enable/resize (dirty flag), never per-frame.
    /// </summary>
    internal sealed class CombatArcLayout611 : MonoBehaviour
    {
        internal RectTransform[] Medallions;

        private static readonly float[] ArcAngleDeg = { 171.0f, 144.9f, 118.8f, 92.7f };
        private const float ArcRadiusEm = 8.0f;     // arc radius around the pivot, in em
        private const float PivotInsetEm = 1.8f;    // pivot inset left of the pill's top-right
        private const float MedallionPerPillH = 0.9f;
        private bool _dirty = true;

        private void OnEnable() { _dirty = true; }
        private void OnRectTransformDimensionsChange() { _dirty = true; }

        private void LateUpdate()
        {
            if (!_dirty || Medallions == null) return;
            var row = (RectTransform)transform;
            var r = row.rect;
            if (r.width < 1f || r.height < 1f) return;   // layout not resolved yet — retry
            _dirty = false;

            float pillH = (HudKitController.Pill611Y1 - HudKitController.Pill611Y0) * r.height;
            float em = pillH / 3.5f;
            float d = MedallionPerPillH * pillH;
            var pivotPt = new Vector2(
                r.xMin + HudKitController.Pill611X1 * r.width - PivotInsetEm * em,
                r.yMin + HudKitController.Pill611Y1 * r.height);

            for (int i = 0; i < Medallions.Length && i < ArcAngleDeg.Length; i++)
            {
                var m = Medallions[i];
                if (m == null) continue;
                float a = ArcAngleDeg[i] * Mathf.Deg2Rad;
                Vector2 centre = pivotPt + ArcRadiusEm * em * new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                m.anchorMin = m.anchorMax = new Vector2(0.5f, 0.5f);
                m.pivot = new Vector2(0.5f, 0.5f);
                m.sizeDelta = new Vector2(d, d);
                m.anchoredPosition = centre - r.center;
            }
        }
    }
}
