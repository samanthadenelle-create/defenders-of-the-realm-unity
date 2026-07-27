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
//   • Talk button appears   — visibility from PostureSignals.TalkAvailable
//     (Core static; the stale one-shot reflection push is retired).
//   • raid-"x"/harvest-"Y"  — not rebuilt (earns-its-place: no verified
//     backing feature surface); Pi sign-in stays off the HUD (Title-gated).
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
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
        private Button _talkButton, _fleeButton, _startWaveButton;
        // Owner report 2026-07-06 ("I do not see Quest changing to upgrade walking to upgradable
        // buildings") — the HudBuildingFocus reroute was ported from the retired HUD but its
        // VISIBLE face swap wasn't (classified 07-06: write-side fires from 3 pollers; this was
        // the only missing reader). The context button now relabels Quests <-> Upgrade on focus.
        private Button _questContextButton;
        private TMP_Text _questContextLabel;
        private bool _questContextUpgradeFace;
        private TMP_Text _fleeLabel;
        private TMP_Text _waveLabel, _waveCountdown;
        private ElarionUiKit.BarHandle _waveProgress;
        private GameObject _waveBlockRoot;
        private ElarionUiKit.NameplateHandle[] _cycleRows;
        private string[] _cycleIds;
        private ElarionUiKit.SlideDockHandle _slideDock;   // WO-439: left slide-out (Chat/Ranks/Music/Settings)
        private HudCompassWidget _compass;

        // model subscriptions (for teardown)
        private readonly List<Action> _unsubscribe = new List<Action>();

        private bool _startWaveAvailable;
        private float _chipsExpandUntil;   // collapsed chips: tap-expand window

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
                var atkIcon = UiStyle.Icon("energy-sword", "attack", "sword", "melee");
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

            // ── town action buttons (WO-778: 5 equal-width — Build / Talk / Bag / Work / Quests) ──
            // Work = work-queue panel (Builders / Training / Research) via ObsidianQueueGate.
            const float btnGap = 0.01f;
            const float btnW = (1f - btnGap * 4f) / 5f;   // five equal faces across 0..1
            float bx = 0f;
            Vector2 BtnMin(float x) => new Vector2(x, 0.10f);
            Vector2 BtnMax(float x) => new Vector2(x + btnW, 0.95f);

            var build = ElarionUiKit.BuildObsidianButton(pool, "Build",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Yellow,
                BtnMin(bx), BtnMax(bx),
                () => { if (_owner != null) _owner.BuildRequested?.Invoke(); });
            // Carry-over (WO-T2 working-tree intent): the tutorial spotlight target.
            TutorialHighlightRegistry.Register("hud.build_button", (RectTransform)build.transform);
            Register("buildButton", WrapAsWidget("buildButton", build.gameObject));
            bx += btnW + btnGap;

            _talkButton = ElarionUiKit.BuildObsidianButton(pool, "Talk",
                ElarionUiKit.ObsidianButtonStyle.Style2, ElarionUiKit.ObsidianButtonColor.Green,
                BtnMin(bx), BtnMax(bx), () =>
                {
                    FlowTrace.Step("HudKit", "Talk tapped -> HudCommands.Talk + TalkRequested");
                    HudCommands.Talk();
                    if (_owner != null) _owner.TalkRequested?.Invoke();   // legacy bridge compat
                });
            Register("talkButton", WrapAsWidget("talkButton", _talkButton.gameObject));
            bx += btnW + btnGap;

            var bag = ElarionUiKit.BuildObsidianButton(pool, "Bag",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Gray,
                BtnMin(bx), BtnMax(bx), () =>
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
            Register("bagButton", WrapAsWidget("bagButton", bag.gameObject));
            bx += btnW + btnGap;

            // WO-778 P0-A: work-queue reachability — was dark (RequestToggle had zero callers).
            var work = ElarionUiKit.BuildObsidianButton(pool, "Work",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Gray,
                BtnMin(bx), BtnMax(bx), () =>
                {
                    FlowTrace.Step("HudKit", "Work tapped -> ObsidianQueueGate.RequestToggle");
                    ObsidianQueueGate.RequestToggle();
                });
            Register("workQueueButton", WrapAsWidget("workQueueButton", work.gameObject));
            bx += btnW + btnGap;

            // Context button — relabels Quests <-> Upgrade via the Update() focus poll (07-06).
            var quest = ElarionUiKit.BuildObsidianButton(pool, "Quests",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Gray,
                BtnMin(bx), BtnMax(bx), OnContextAction);
            Register("questButton", WrapAsWidget("questButton", quest.gameObject));
            _questContextButton = quest;
            _questContextLabel = quest.GetComponentInChildren<TMP_Text>(true);
            _questContextUpgradeFace = false;

            // ── moveCluster -> HudMoveInput ──
            if (FeatureFlags.CombatHud611)
            {
                // WO-611: a VIRTUAL D-PAD (cross/plus) replaces the 4-round-button cluster.
                var dpad = ElarionUiKit.BuildVirtualDPad(pool, new Vector2(0.5f, 0.5f), HudMoveInput.Set);
                Register("moveCluster", WrapAsWidget("moveCluster", dpad.root));
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
            Register("hpPotionSlot", WrapAsWidget("hpPotionSlot", _hpPotionSlot.root));

            _manaPotionSlot = ElarionUiKit.BuildActionSlot(pool,
                new Vector2(0.85f, 0.10f), new Vector2(0.99f, 0.95f), HudCommands.ManaPotion);
            var manaIcon = UiStyle.Icon("mana", "consumable", "crystal");
            if (manaIcon == null) manaIcon = UiStyle.Icon("potion", "consumable", "mana");
            if (manaIcon != null) _manaPotionSlot.SetIcon(manaIcon);
            Register("manaPotionSlot", WrapAsWidget("manaPotionSlot", _manaPotionSlot.root));

            if (FeatureFlags.CombatHud611)
            {
                // WO-611 (mockup v8): the two potions are ROUND in the housed action bar — the
                // medallion face without a key badge (they overlay the obsidian housing, killing
                // the tan Blink slot faces the 07-05 capture showed).
                ElarionUiKit.StyleAsRoundMedallion(_hpPotionSlot);
                ElarionUiKit.StyleAsRoundMedallion(_manaPotionSlot);
            }
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
            // WO-431: Gold (primary) + Wood/Iron/Food/Crystal chips live in an OBSIDIAN dark
            // frame under a gold "Resources" header, and the frame HUGS its content via a
            // VerticalLayoutGroup + ContentSizeFitter (dynamic width — no fixed olive slab).
            // Each chip draws OUR resource icon through the CurrencyChip concept resolver
            // (concept-icons.json gold/wood/iron/food/crystal -> Icons_Obsidian) — the icon
            // choice is DATA, never hard-coded here. Count-tween only, NO flash.
            // WO-440: the always-visible resources panel now lives in a DOCK — a right-edge tab
            // (always shown when the widget is occupied) + the collapsible chips panel that the tab
            // toggles. Collapsed by default; tap the tab to expand, tap again to collapse. SetResources
            // (OnEconomy) updates the chip values whether the panel is open or closed (labels persist).
            _resDock = new GameObject("ResourceDock", typeof(RectTransform));
            _resDock.transform.SetParent(pool, false);
            var drt = (RectTransform)_resDock.transform;
            drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one;
            drt.offsetMin = Vector2.zero; drt.offsetMax = Vector2.zero;

            _resExpandedRow = new GameObject("ResourceChips", typeof(RectTransform));
            _resExpandedRow.transform.SetParent(_resDock.transform, false);
            var rrt = (RectTransform)_resExpandedRow.transform;
            // Top-RIGHT pivot so the fitter grows the frame down/left, tucked under the right-edge tab.
            rrt.anchorMin = new Vector2(1f, 1f); rrt.anchorMax = new Vector2(1f, 1f);
            // -84 (was -52): the toggle tab grew taller (y 0.80-0.99, F8 2026-07-08 label-fit fix),
            // so the expanded panel drops further below the tab's new bottom edge — no overlap.
            rrt.pivot = new Vector2(1f, 1f); rrt.anchoredPosition = new Vector2(-6f, -84f);

            // Obsidian dark frame + gold inner rim (reused kit chrome, near-black ObsidianFill
            // — NOT the olive Panel()). ignoreLayout so it stretches to the fitter-sized content.
            var frame = ElarionUiKit.AddImage(_resExpandedRow.transform, "ResFrame",
                Vector2.zero, Vector2.one, ElarionUiKit.ObsidianFill, rounded: true);
            ElarionUiKit.AddInnerRim(frame, ElarionUiKit.ObsidianTrim);
            var frameImg = frame.GetComponent<Image>();
            if (frameImg != null) frameImg.raycastTarget = false;
            frame.AddComponent<LayoutElement>().ignoreLayout = true;

            // Vertical stack + ContentSizeFitter => dynamic width/height to the content.
            var vlg = _resExpandedRow.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(12, 12, 8, 10);
            vlg.spacing = 4f;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;  vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            var fitter = _resExpandedRow.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

            // Gold "Resources" header (kit Label; no crest glyph — avoids build-font tofu).
            var header = ElarionUiKit.Label(_resExpandedRow.transform, "Resources", 0f, 1f,
                ElarionUi.Gilt, ElarionUi.FontLabel, TextAlignmentOptions.MidlineLeft, 0f, 1f, bold: true);
            var headerLe = header.gameObject.AddComponent<LayoutElement>();
            headerLe.minHeight = 26f; headerLe.preferredHeight = 26f; headerLe.minWidth = 168f;

            var kinds = new[]
            {
                ElarionUiKit.CurrencyKind.Gold, ElarionUiKit.CurrencyKind.Wood,
                ElarionUiKit.CurrencyKind.Iron, ElarionUiKit.CurrencyKind.Food,
                ElarionUiKit.CurrencyKind.Crystal,
            };
            // WO-697 (RES-1) icon-first rows: the mirrored RpgUi/currency/* art now exists, so
            // the chip builder shows the ICON as the identity carrier and DROPS the text label
            // (colorblind-safe: icon = shape identity). The tags below are the no-art FALLBACK
            // only (flag_03's never-a-naked-number law, now enforced inside the builder); values
            // render compact via ElarionUi.CompactNumber and the chips content-fit their width.
            var tags = new[] { "Gold", "Wood", "Iron", "Food", "Crystal" };
            _resChips = new ElarionUiKit.CurrencyChipHandle[kinds.Length];
            for (int i = 0; i < kinds.Length; i++)
            {
                // OWNER 2026-07-15 (colorblind): in THIS resource strip Gold must read the SAME
                // size + color as Wood/Iron/Food/Crystal — the earlier primary:Gold gave it gilt
                // digits + FontHead (bigger) + bold (ElarionUiKitObsidian CurrencyChip:850-853),
                // so it stood out. All five chips are peers here; the icon carries identity, never
                // color/size. primary:false makes every chip uniform (Parchment, FontLabel, normal).
                _resChips[i] = ElarionUiKit.CurrencyChip(_resExpandedRow.transform, kinds[i],
                    Vector2.zero, Vector2.one, primary: false,
                    tag: tags[i]);
                var le = _resChips[i].root.AddComponent<LayoutElement>();
                le.minHeight = 34f; le.preferredHeight = 34f; le.minWidth = 168f;
            }
            // WO-440: right-edge tab that toggles the chips panel (collapsed by default).
            // F8-25a (flag_01/02): the old x 0.95..1.0 anchors are fractions of the actionRail
            // ZONE mount (0.780..0.995 screen, ~232px at the 1080 ref — HudAreasHost.cs:97), NOT
            // the screen — the tab rendered ~12px wide, squashing the ornate button1_yellow
            // 9-slice (24px borders need >=48px) into a thin stretched vertical strip with the
            // newly-resolving coin icon inside it. x 0.60 gives the tab ~93px — room for the
            // sliced chrome, the coin icon and the "Resources" word.
            // TAB HEIGHT FIX (F8 2026-07-08): the tab was only y 0.86-0.99 (~13% of the dock →
            // ~50px), and with the coin icon riding above the word the "Resources" label was
            // squished into the lower ~36% (~18px) — the guard CULLED its glyphs ("0 visible
            // glyphs, rect 167x18"). Give the tab ~19% (y 0.80-0.99, ~73px) so the label band
            // below the icon is tall enough to seat the ≥20px font.
            var resTab = ElarionUiKit.BuildObsidianButton(_resDock.transform, "$",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Yellow,
                new Vector2(0.60f, 0.80f), new Vector2(1.0f, 0.99f), ToggleResourcePanel);
            // Owner F8 07-06 (flag_03): the collapsed dock read as an anonymous box — the "$"
            // glyph was swapped for a coin icon with NO text. The tab now ALWAYS says
            // "Resources" (icon rides above the word when it resolves; text never hides).
            var resTabLbl = resTab.GetComponentInChildren<TMP_Text>();
            if (resTabLbl != null)
            {
                resTabLbl.text = "Resources";
                resTabLbl.fontSize = ElarionUi.FontMicro;
                ElarionUiKit.FitSingleLine(resTabLbl);   // narrow tab — never clip the word
            }
            var resTabIcon = UiStyle.Icon("gold", "coin", "resources");
            if (resTabIcon != null && resTabLbl != null)
            {
                var ico = ElarionUiKit.AddImage(resTab.transform, "TabIcon",
                    new Vector2(0.30f, 0.56f), new Vector2(0.70f, 0.96f), Color.white, rounded: false);
                var icoImg = ico.GetComponent<Image>();
                icoImg.sprite = resTabIcon; icoImg.preserveAspect = true; icoImg.raycastTarget = false;
                // Word takes the LOWER HALF of the (now taller) tab so its rect can seat the font
                // instead of clipping to nothing (F8 2026-07-08 guard FAIL). Icon rides the top.
                var lblRt = (RectTransform)resTabLbl.transform;
                lblRt.anchorMin = new Vector2(0.02f, 0.03f);
                lblRt.anchorMax = new Vector2(0.98f, 0.52f);
                lblRt.offsetMin = Vector2.zero; lblRt.offsetMax = Vector2.zero;
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
            Register("resourceChipsCollapsed", WrapAsWidget("resourceChipsCollapsed", tapGo));
        }

        // =====================================================================
        // MODEL BINDING — VM Changed events only (§1.1 rule 4 / §5 rule 3).
        // =====================================================================

        private void BindModels()
        {
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
            PostureSignals.TalkChanged += OnTalkChanged; _unsubscribe.Add(() => PostureSignals.TalkChanged -= OnTalkChanged);
            OnTalkChanged();
            FlowTrace.Step("HudKit", "models bound (vitals/economy/wave/world/abilities/cycle/target/cast)");
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
                _vitals.ManaFill.fillAmount = v.MaxMana > 0 ? Mathf.Clamp01((float)v.Mana / v.MaxMana) : 0f;
            if (_vitals.NameLabel != null)
                _vitals.NameLabel.text = (string.IsNullOrEmpty(v.ClassId) ? "Hero" : Cap(v.ClassId)) +
                                         "  Lv " + Mathf.Max(1, v.Level);
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
                // WO-611: combat HUD medallions use the SOFT under-glow; else the hard radial sweep.
                if (medallion)
                {
                    _abilityGlows[i].Set(s.CooldownRemaining, s.CooldownTotal);
                    // The glow path skips SetCooldown, so keep the tap-gate contract here.
                    if (h.button != null) h.button.interactable = !(s.CooldownRemaining > 0f && s.CooldownTotal > 0f);
                }
                else
                    h.SetCooldown(s.CooldownRemaining, s.CooldownTotal);
            }
        }

        // WO-611: present an UNASSIGNED combat medallion — dimmed face, no icon, no tap, no stale
        // cooldown text — so the Q/W/E/R arc always renders in hostile postures (combat-HUD only;
        // callers gate on the glow driver's presence, which exists only when the flag was ON at build).
        private static void SetEmptyMedallion(ElarionUiKit.ActionSlotHandle h)
        {
            if (h == null) return;
            h.SetLabel(null);   // 2026-07-11: drop a stale text face (Dodge/Attack) with the icon
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
                    _hpPotionSlot.button.interactable =
                        c.HpPotionCount > 0 && HudCommands.HasPotion && c.HpCooldownRemaining <= 0f;
            }
            if (_manaPotionSlot != null)
            {
                _manaPotionSlot.SetCount(c.ManaPotionCount);
                _manaPotionSlot.SetCooldown(c.ManaCooldownRemaining, c.ManaCooldownTotal);
                if (_manaPotionSlot.button != null)
                    _manaPotionSlot.button.interactable =
                        c.ManaPotionCount > 0 && HudCommands.HasManaPotion && c.ManaCooldownRemaining <= 0f;
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

        // TALK §0 FIX: visibility follows the Core signal — never a stale reflection push.
        private void OnTalkChanged()
        {
            if (_talkButton == null) return;
            _talkButton.interactable = PostureSignals.TalkAvailable;
            if (!_talkButton.TryGetComponent(out CanvasGroup cg))
                cg = _talkButton.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = PostureSignals.TalkAvailable ? 1f : 0.45f;
        }

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

        // WO-440: right-edge resource tab toggle — expand/collapse the chips panel. Values are
        // still updated by OnEconomy regardless of this state (labels persist while hidden).
        private void ToggleResourcePanel() => SetResourcePanelOpen(!_resPanelOpen);

        private void SetResourcePanelOpen(bool open)
        {
            _resPanelOpen = open;
            if (_resExpandedRow != null && _resExpandedRow.activeSelf != open)
                _resExpandedRow.SetActive(open);
        }

        // Context action (carried over from the old HUD, Core-only): focused upgradable
        // building -> Upgrade; else the Quest/Rumor board.
        private void OnContextAction()
        {
            string id = HudBuildingFocus.CurrentBuildingId;
            var custom = HudBuildingFocus.CurrentUpgradeAction;
            if (custom != null) { FlowTrace.Step("HudKit", "context action -> custom upgrade"); custom(); }
            else if (!string.IsNullOrEmpty(id)) PanelRouter.Open(PanelId.BuildingUpgrade, id);
            else if (!PanelRouter.Open(PanelId.RumorBoard))
                FlowTrace.Warn("HudKit", "RumorBoard opener not registered — quest board unreachable");
        }

        // WO-439: the LEFT slide-out dock — a gear tab pinned to the left screen edge (collapsed by
        // default) that slides open a panel with FOUR tabs: Chat / Leaderboard / Music / Settings.
        // Built from the shared ElarionUiKit.BuildSlideTab helper; registered under the same "chatDock"
        // widget id so the hud-areas.json occupancy rows are unchanged. Icons resolve through the HUD's
        // concept-icon path (UiStyle.Icon) with a text fallback so nothing blanks.
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
            var dockPanelRt = _slideDock.panel;
            dockPanelRt.anchorMin = new Vector2(0f, 0.5f);
            dockPanelRt.anchorMax = new Vector2(0f, 0.5f);
            dockPanelRt.pivot = new Vector2(0f, 0.5f);
            dockPanelRt.anchoredPosition = Vector2.zero;
            // Height carries FIVE tabs now (Pause folded in — cosmetic flag A) at ~112px
            // touch targets each: 700 / 5 = 140px slot, well above MinTouchPx.
            dockPanelRt.sizeDelta = new Vector2(400f, 700f);
            var dockTabRt = (RectTransform)_slideDock.tab.transform;
            dockTabRt.anchorMin = new Vector2(0f, 0.5f);
            dockTabRt.anchorMax = new Vector2(0f, 0.5f);
            dockTabRt.pivot = new Vector2(0f, 0.5f);
            dockTabRt.anchoredPosition = Vector2.zero;
            dockTabRt.sizeDelta = new Vector2(84f, 84f);

            AddDockTab(_slideDock.panel, 0, "Chat",        "chat",        OpenClanChat);
            AddDockTab(_slideDock.panel, 1, "Leaderboard", "leaderboard", OpenLeaderboard);
            AddDockTab(_slideDock.panel, 2, "Music",       "music",       OpenJukebox);
            AddDockTab(_slideDock.panel, 3, "Settings",    "settings",    OpenSettings);
            // Pause folded into the LEFT gear (cosmetic flag A, 2026-07-24): the standalone
            // top-right pause chip (PauseHudBootstrap.PauseHudButton) was culled to leave ONE
            // door. PauseController/SettingsController stay installed by PauseHudBootstrap; this
            // tab is the caller that opens Pause/Quit-to-Title via PauseGate.RequestBack().
            AddDockTab(_slideDock.panel, 4, "Pause",       "pause",       () => PauseGate.RequestBack());

            Register("chatDock", WrapAsWidget("chatDock", _slideDock.root));
        }

        // One labelled + icon-badged tab inside the slide-out (stacked vertically, top-to-bottom).
        private void AddDockTab(RectTransform panel, int i, string label, string iconConcept, Action onTap)
        {
            const int n = 5;   // Chat/Leaderboard/Music/Settings/Pause (Pause folded in, flag A)
            float y1 = 1f - (i / (float)n) - 0.02f;
            float y0 = 1f - ((i + 1) / (float)n) + 0.02f;
            var btn = ElarionUiKit.BuildObsidianButton(panel, label,
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Gray,
                new Vector2(0.06f, y0), new Vector2(0.94f, y1), onTap);
            var icon = UiStyle.Icon(iconConcept);
            if (icon != null)
            {
                var ico = ElarionUiKit.AddImage(btn.transform, "TabIcon",
                    new Vector2(0.05f, 0.18f), new Vector2(0.28f, 0.82f), Color.white, rounded: false);
                var img = ico.GetComponent<Image>();
                img.sprite = icon; img.preserveAspect = true; img.raycastTarget = false;
            }
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
            OnTalkChanged();
            OnConsumables();
            OnPlayerStatus();
            OnTargetStatus();
            OnWave();   // wave block phase gate re-evaluates with the posture

            FlowTrace.Step("HudKit", "occupancy applied: posture " + HudPostureKeys.Key(posture) +
                           " -> " + shown + " widgets live");
        }

        private void Update()
        {
            // WO-611: drive the animated lock crosshair badge from the target model (combat HUD only).
            // 0 = no target (unlocked/faint), 1 = target held but not locked (acquiring pulse),
            // 2 = manual lock (locked/gold). Bound to TargetModel.HasTarget/Locked.
            if (_lockBadge != null && _models != null && _models.Target != null)
            {
                var t = _models.Target;
                _lockBadge.SetState(!t.HasTarget ? 0 : (t.Locked ? 2 : 1));
            }

            // Context-button face swap (owner 07-06): HudBuildingFocus is written by the three
            // proximity pollers; the tap already rerouted (OnContextAction) but nothing VISIBLE
            // changed — the face-swap reader was never ported from the retired HUD. Poll the
            // Core static (no event exists) and relabel Quests <-> Upgrade on transitions.
            if (_questContextLabel != null)
            {
                bool upgradeFace = !string.IsNullOrEmpty(HudBuildingFocus.CurrentBuildingId);
                if (upgradeFace != _questContextUpgradeFace)
                {
                    _questContextUpgradeFace = upgradeFace;
                    _questContextLabel.text = upgradeFace ? "Upgrade" : "Quests";
                    FlowTrace.Step("HudKit", "context button face -> " + (upgradeFace ? "Upgrade" : "Quests") +
                        " (focus='" + (HudBuildingFocus.CurrentBuildingId ?? "<none>") + "')");
                }
            }

            // Cheap availability polls (no model event exists for these Core statics).
            if (_widgets.TryGetValue("fleeButton", out var flee))
            {
                bool want = HudCommands.HasFlee &&
                            _config.Occupancy(_evaluator.Posture).ContainsKey("fleeButton");
                if (flee.activeSelf != want) flee.SetActive(want);
            }
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
