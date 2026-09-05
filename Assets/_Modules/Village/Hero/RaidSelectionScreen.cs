// =============================================================================
// RaidSelectionScreen — the Raids-tab grid of raid CARDS (screen 2 of
// docs/RAID_TROOP_UI.md). Code-built uGUI (NO UXML — UXML does not render in
// player builds, project hard rule), routed through the SHARED presentation kit
// (DeNelle.Core.UI.ElarionUiKit) so it reads as the SAME designed game as the
// town HUD / ShopPanel / TroopTrainingPanel: dark-wood + gold framing, gold serif
// title, framed cards.
// -----------------------------------------------------------------------------
// MIRRORS ShopPanel / TroopTrainingPanel: BuildModalCanvas (sortingOrder 31000 +
// overrideSorting, above the world-HUD band) + tap-outside Scrim + a framed
// dark-glass panel + a Header. The RAIDS banner heads the panel (Resources.Load,
// null-safe — decorative; the panel works without it). A scrollable grid of raid
// cards is built from SceneConfigCatalog.All, filtered to the 4 flagship enemy
// raids (raider_camp_small / fortified_garrison / mage_enclave / iron_bastion).
//
// Each card reads SceneConfigDef: displayName (gold serif), difficulty (a colour-
// tinted badge: green/yellow/red = Regular/Hard/Extreme), recommendedClearTime
// (the 3-star target, rendered m:ss), and a reward hint from rewardMultiplier +
// shardDropChance (resource icon + an Echo-Shard hint). Tapping a card opens
// RaidDeployScreen.Open(def).
//
// ENTRY: static RaidSelectionScreen.Open() self-heals a host GameObject and opens
// the screen — call it from a Raids-tab button / dev panel. (No town button is
// wired here to avoid colliding with the other lane; see Open() docs.)
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DeNelle.Core.UI;
using DeNelle.Core.UI.Mvvm;
using DeNelle.Village;
using DeNelle.Village.UI;   // StarRatingRow (tofu-proof star row)

namespace DeNelle.Village.Hero
{
    public sealed class RaidSelectionScreen : MonoBehaviour
    {
        // The flagship-raid ids + the catalog projection now live in RaidSelectionVM.

        private GameObject _ui;
        private RectTransform _bodyZone;              // chrome.layout.body — the ONE content well
        private ElarionUiKit.ScrollZoneHandle _scroll; // kit fit-or-scroll handle (§1.14)

        // The pure ViewModel owns the SceneConfigCatalog projection; this View renders
        // vm.Raids + the per-card helpers and never touches the catalog itself.
        private RaidSelectionVM _vm;

        // UIF-01: single-modal arbiter handle. Registering this makes opening the grid close
        // any prior panel (Shop/Train/etc) and lets the Android/ESC back button dismiss it via
        // PanelManager.CloseOpen. Mirrors the Echo roster->card single-modal precedent.
        private PanelHandle _panelHandle;

        // Cached self-instance so the static entry never FindObjectsByType-scans the scene
        // (a View locating its own singleton screen — routed through this cache instead).
        private static RaidSelectionScreen _instance;

        /// <summary>
        /// WO-725: true while the camp-select list owns the screen (reflects the _ui
        /// lifetime — set in <see cref="OpenInternal"/>, cleared in <see cref="Close"/> /
        /// <see cref="OnDestroy"/>). Polled by the Arena Herald (Path A entry) to suppress
        /// its world "Enter Arena" proximity prompt while the list is up and to emit the
        /// Arena open/close FlowTrace edge. Static so it survives a scene-change destroy.
        /// </summary>
        public static bool IsScreenOpen { get; private set; }

        // Card pixel height in the scroll list (tall plaque — banner + badge + time + reward).
        // Four flagship camps must fit in the first fold at the shortest supported
        // landscape height. Scrolling remains available for future catalog growth.
        private const float CardHeightPx = 142f;
        private const float CardGapPx    = 12f;

        // ── Entry hook ───────────────────────────────────────────────────────

        /// <summary>
        /// Self-healing static entry: finds or creates a host GameObject carrying a
        /// RaidSelectionScreen and opens the grid. The intended trigger is the town /
        /// castle Raids-tab button (or the dev panel) — wire that to call this. Not
        /// auto-wired to a town button here to avoid colliding with the parallel
        /// raids-tab lane.
        /// </summary>
        public static void Open()
        {
            // =============================================================
            // WO-1374 — THE CAPABILITY GATE, AND IT IS FIRST FOR TWO REASONS.
            // =============================================================
            // (1) THE ARENA HERALD BYPASS. WO-1357 taught the Journey card to read
            //     PostureSignals.RaidCapable and lock gracefully - but the Arena Herald
            //     in the world calls THIS method directly (ArenaHeraldSpawner.OpenArena),
            //     and nothing on that path ever asked the question. So the front door was
            //     locked and a side door stood open: a player with no Barracks could walk
            //     to the monument, tap Enter Arena, and be handed a camp list for a raid
            //     they cannot start.
            //
            //     (!) THE FIX IS DELIBERATELY HERE AND NOT AT THE HERALD. Adding the check
            //     to ArenaHeraldSpawner would fix the one caller we know about and leave
            //     the next one to rediscover the bug - which is exactly how this one
            //     survived WO-1357. Open() is the single door every raid entry passes
            //     through (Herald, Journey card, HUD face, dev panel), so gating it here
            //     closes the class rather than the instance.
            //
            // (2) THE REFUSAL MUST NAME WHAT IS ACTUALLY MISSING. Every refusal below this
            //     point talks about troops and barracks slots, because until now the army
            //     check was the ONLY check. A player whose real blocker was "raids are off
            //     in this build" or "your Barracks was destroyed" was told to go train
            //     troops - advice that cannot possibly work, given to someone who then
            //     trains troops and finds the door still shut.
            //
            // ⛔ THIS READS THE ONE PREDICATE, IT DOES NOT WRITE A SECOND ONE.
            // PostureSignals.RaidCapable / RaidLock are published by
            // RaidCapabilityHudBridge and consumed identically by the bar face and the
            // Journey card; RaidLockCopy is the ONE owner of the words. A hand-rolled
            // StructureSingleton.IsBuilt("barracks") here would be the second check that
            // WO-1357's header forbids by name - two checks drift, and the drift IS the
            // defect. Both signals default to the open state, so a headless run, a
            // pre-publish frame or an absent GameState can never false-block the door.
            if (!DeNelle.Core.HudModel.PostureSignals.RaidCapable)
            {
                var lockReason = DeNelle.Core.HudModel.PostureSignals.RaidLock;
                string lockCopy = DeNelle.Core.HudModel.PostureSignals.RaidLockCopy(lockReason);
                DeNelle.Core.Diagnostics.FlowTrace.Step("Raid",
                    "raid entry REFUSED at the capability gate: lock=" + lockReason +
                    " -> \"" + (lockCopy ?? "(no copy)") + "\". This is the gate the Arena " +
                    "Herald used to walk straight past (WO-1374).");
                ElarionUiKit.ShowToast(
                    // Never a generic "Locked": the copy names the missing thing AND the
                    // remedy, because the owner is red/green colourblind and the tell has
                    // to be words. The fallback can only be reached if a new lock reason is
                    // added without copy, and it says so rather than pretending.
                    lockCopy ?? ("Raids are unavailable right now (" + lockReason + ")."),
                    ElarionUiKit.ToastTone.Info);
                // ⛔ And NO training panel. The army redirect below is right when the
                // blocker is troops; opening it here would send a player with no Barracks
                // to train units they have nowhere to train, which is the exact
                // wrong-advice failure this gate exists to stop.
                return;
            }

            // WO-813 SAFETY NET, upgraded to the FULL-ARMY gate (owner ruling: raids need a
            // full army counting ready + queued troops). This Village-side check is the
            // AUTHORITATIVE one — it recomputes via ArmyReadiness.Compute, the ONE readiness
            // formula (owner review 2026-08-01; same math the status publisher relays) and
            // never reads the HUD's polled mirror. When not ready it toasts AND opens the
            // drillmaster training panel directly, then returns. Stateless/headless (no
            // GameState) -> Compute returns READY, so it opens normally — never a false block.
            var st = DeNelle.Core.State.GameStateService.Instance != null
                ? DeNelle.Core.State.GameStateService.Instance.State : null;
            var readiness = ArmyReadiness.Compute(st);
            // TEST BYPASS (owner ask 2026-08-16: "i need flagged on to test"). The full-army gate is
            // CORRECT product behaviour and stays the default — but it means ~10 training jobs before
            // the raid grid opens at all, which makes the whole raid pillar untestable in one sitting.
            // ff.raidtest=1 opens the grid regardless. Default OFF, so shipping behaviour is unchanged.
            // Loud on purpose: a bypassed gate must never be mistaken for a passed one in a capture.
            if (!readiness.Ready && DeNelle.Core.FeatureFlags.RaidTestBypassArmyGate)
            {
                DeNelle.Core.Diagnostics.FlowTrace.Warn("Raid",
                    "ARMY GATE BYPASSED by ff.raidtest — opening raids with " +
                    readiness.DeployableSlots + " deployable + " + readiness.QueuedSlots +
                    " queued of cap " + readiness.CapSlots + ". This is a TEST path; " +
                    "shipping players still fill every slot first.");
            }
            else if (!readiness.Ready)
            {
                // WO-932: concrete fill numbers so the gate never feels like a silent softlock.
                // WO-1008: TWO DISTINCT REFUSALS, never one generic line. Post-WO-1008 the Raids
                // face is VISIBLE-and-greyed the moment a Barracks exists (it used to vanish), so
                // this refusal is now also reached with a completely EMPTY army — and "Army 0/5,
                // fill every slot" reads as a maths puzzle when the real instruction is "you have
                // no troops at all, go train some". The dim reason on the face
                // (HudActionBarModel.RaidDimReason) and this copy tell the SAME two stories.
                // WO-823 Phase E: the denominator is REQUIREDSLOTS, not CapSlots. On a save
                // that has never raided the bar is the softened 3, and telling that player to
                // "fill every slot" of 10 would be the copy contradicting the gate that
                // produced it - the same disagreement Phase E removed from RaidDeployScreen.
                // FirstRaidSoftGate WORDS this line; it does NOT decide it. The decision was
                // already made by readiness.Ready above.
                int have = readiness.DeployableSlots + readiness.QueuedSlots;
                int need = Mathf.Max(1, readiness.RequiredSlots > 0 ? readiness.RequiredSlots : readiness.CapSlots);
                bool noTroopsAtAll = have <= 0;
                DeNelle.Core.Diagnostics.FlowTrace.Step("Raid",
                    (noTroopsAtAll ? "NO-TROOPS redirect: " : "full-army redirect: ") +
                    "raids opened with " + readiness.DeployableSlots +
                    " deployable + " + readiness.QueuedSlots + " queued of cap " +
                    readiness.CapSlots + " (required " + readiness.RequiredSlots +
                    (readiness.FirstRaidSoftGate ? ", FIRST-RAID SOFT GATE" : "") +
                    ") -> drillmaster training panel.");
                ElarionUiKit.ShowToast(
                    noTroopsAtAll
                        ? "No troops yet - train troops at the Barracks, then open Raids."
                        : readiness.FirstRaidSoftGate
                            ? "Army " + have + "/" + need + " slots - your first raid only needs " + need +
                              ". Train at the Barracks, then open Raids."
                            : "Army " + have + "/" + need + " - fill every slot at the Barracks, then open Raids.",
                    ElarionUiKit.ToastTone.Info);
                TroopDialogueCommands.ShowTrainingUI();
                return;
            }

            var existing = _instance;
            if (existing == null)
            {
                var host = new GameObject("RaidSelectionScreen");
                existing = host.AddComponent<RaidSelectionScreen>();   // Awake caches _instance
            }
            existing.OpenInternal();
        }

        private void Awake()
        {
            if (_instance == null) _instance = this;
        }

        // ── Build ─────────────────────────────────────────────────────────────

        private void OpenInternal()
        {
            Close();

            // VM FIRST — it resolves the flagship raids (fallback to all enemy raids) from
            // the catalog, so this View never touches SceneConfigCatalog.
            // 2026-09-04 ESCALATION GATE - the View supplies both inputs the pure VM cannot
            // reach for itself, and this is the ONLY place either is wired.
            //
            // (a) THE COUNTER. GameState.RaidVictories (GameState.cs:629) is the persisted
            //     total, incremented once per win by RaidVictoryController.RecordVictory and
            //     one-shot backfilled for saves that predate it. Read through the same
            //     GameStateService.Instance?.State this screen already reads for army
            //     readiness; a headless/stateless run yields 0, which locks the gated tiers
            //     VISIBLY (with their reason) rather than silently opening them.
            // (b) THE AVAILABILITY PROBE. SceneRouter.IsSceneInBuild is the public probe
            //     already documented for raid CTAs ("False = toast under construction, never
            //     a silent strand"). RaidBase_IronBastion is registered DISABLED, so it reads
            //     false and its card carries a sentence instead of a dead tap.
            RaidSelectionVM.VictoryCountProvider =
                () => DeNelle.Core.State.GameStateService.Instance?.State?.RaidVictories ?? 0;
            RaidSelectionVM.SceneAvailableProvider = DeNelle.Core.SceneRouter.IsSceneInBuild;
            _vm = RaidSelectionVM.CreateDefault(Close);

            // Modal canvas + tap-outside scrim, both from the shared kit. Pin
            // sortingOrder 31000 + overrideSorting (mirrors ShopPanel) so the panel +
            // its scrim render ABOVE the world-HUD band but below the top overlays.
            _ui = ElarionUiKit.BuildModalCanvas("RaidSelectionScreenUI", 31000);
            var canvas = _ui.GetComponent<Canvas>();
            if (canvas != null) canvas.overrideSorting = true;
            ElarionUiKit.Scrim(_ui.transform, onTapClose: Close);

            // WO-562: canonical obsidian chrome (black + gold trim + gold header "RAIDS" + shared
            // Close) replaces PanelFramed + a bespoke Header + a per-panel "X" Danger button.
            var chrome = ElarionUiKit.BuildObsidianPanel(_ui.transform, "RAIDS",
                new Vector2(0.16f, 0.06f), new Vector2(0.84f, 0.94f), Close, withBackdrop: false,
                frameName: RpgUiCatalog.FrameCore);
            MedievalUiSkin.ApplyShell(chrome);

            // (#28) The decorative RAIDS banner Niche was REMOVED — with BlinkChrome off (the
            // default look) the Niche paints an opaque warm-stone slab that covered the frame's
            // own gold "RAIDS" header. The FrameCore header zone already carries the title; per
            // canon the frame IS the chrome, so the screen adds none.

            // WO-714 W4: the card grid drops into the FACTORY body zone (chrome.layout.body —
            // close-band reservation + zone backing owned by the kit), never a custom fraction
            // rect on chrome.content (the "unprotected class" named in the kit's own §12 line).
            _bodyZone = chrome.layout != null && chrome.layout.body != null
                ? chrome.layout.body
                : (RectTransform)chrome.content.transform;
            if (_bodyZone != null && chrome.layout != null)
            {
                // The generic frame body reserves far more footer space than this selector
                // uses, leaving the third of only three camps clipped above a dead lower half.
                // Reclaim that verified-empty band while preserving the Close keep-out.
                _bodyZone.anchorMin = new Vector2(_bodyZone.anchorMin.x, 0.20f);
                _bodyZone.anchorMax = new Vector2(_bodyZone.anchorMax.x, 0.80f);
                _bodyZone.offsetMin = Vector2.zero;
                _bodyZone.offsetMax = Vector2.zero;
            }

            // WO-714 P8: the ONE shared open ease (scale target = the panel rect, never the canvas).
            ElarionUiKit.AttachPanelOpenFx(_ui,
                chrome.root != null ? chrome.root.transform as RectTransform : null);

            BuildCards();

            // ApplyShell skins the factory-owned, reserved Close control. Do not add a
            // second panel-local Close: it overlaps the scroll well and falls through the
            // ornate bottom border at ultrawide aspect ratios.
            MedievalUiSkin.ApplyClose(chrome.close);
            if (chrome.close != null)
            {
                chrome.close.gameObject.SetActive(true);
                var closeImage = chrome.close.targetGraphic as Image ?? chrome.close.GetComponent<Image>();
                // close-ornate is a complete baked control, not a stretchable border.
                if (closeImage != null) closeImage.type = Image.Type.Simple;
            }

            // UIF-01: join the single-modal arbiter. A battle-lock rejection tears this down
            // (handle.Close, which also clears IsScreenOpen) and returns before arming the Herald.
            if (_panelHandle == null)
                _panelHandle = PanelManager.Register("Raids", Close, () => _ui != null);
            if (!PanelManager.NotifyOpened(_panelHandle))
                return;

            IsScreenOpen = true;   // WO-725: arm the Herald's prompt-suppression + close-edge trace
            Debug.Log("[RaidSelectionScreen] Opened — raid card grid.");
        }

        private void BuildCards()
        {
            ClearContent();

            // The VM owns the flagship-then-fallback catalog projection.
            var raids = _vm != null ? _vm.Raids : null;
            if (raids == null || raids.Count == 0)
            {
                // Empty state sits directly on the body zone (a stretched label inside the
                // scroll column reports height 0 under the kit's childControlHeight:false law).
                ElarionUiKit.Label(_bodyZone, "No raids available.", 0.4f, 0.6f, ElarionUi.ParchmentDim,
                    ElarionUi.FontBody, TMPro.TextAlignmentOptions.Center);
                Debug.LogWarning("[RaidSelectionScreen] No enemy raids projected — empty grid.");
                return;
            }

            // WO-714 W4: the ONE kit scroll zone (§1.14) replaces the hand-rolled
            // viewport/content/fitter plumbing — screens add no scroll plumbing of their own.
            _scroll = ElarionUiKit.MakeScrollZone(_bodyZone, spacing: CardGapPx, padding: 8);
            foreach (var item in raids)
                CreateRaidCard(_scroll.content, item);

            FinalizeScroll();
        }

        // One framed raid plaque: difficulty-tinted frame, fortress name (gold serif),
        // a difficulty badge, the 3-star target time (m:ss), and a reward hint
        // (resource + Echo-Shard). The whole card is one tap target -> RaidDeployScreen.
        private void CreateRaidCard(Transform parent, ItemVM item)
        {
            string id = item.Id;
            Color tint = DifficultyColor(_vm.DifficultyFor(id));

            // WO-1379 (2026-09-05) - THE PER-CAMP WALL IS RETIRED ON THIS SURFACE. This card
            // used to read RaidCooldownService.RemainingSeconds(id) here and paint "Recovering -
            // raidable in 12h" plus a dim; the owner ruled "Heartfire replaces the camp wall"
            // (WO-1379 section 3), so the ONE gate on WHEN you may raid is the Heartfire charge,
            // checked at the door (OnCardTapped). A card that still said "Recovering" while the
            // door let the player through would be the wrong-advice failure the lock copy below
            // exists to stop. The recovery RECORD is still stamped on every clear
            // (RaidCooldownService.BeginAfterClear) - it is save evidence, not a gate - and
            // nothing on this screen reads it. HeartfireRegression PIN F reds this file if a
            // RaidCooldownService reference reappears.

            // 2026-09-04 — THE ESCALATION GATE. item.Locked / item.LockReason come from
            // RaidSelectionVM.ResolveLock (authored unlockVictories, then scene availability).
            // The ItemVM fields were always here; nothing read them, so every tier showed open.
            bool locked = item.Locked;
            string lockCopy = item.LockReason;
            if (locked && string.IsNullOrEmpty(lockCopy))
            {
                // Reachable only if a new lock path is added without copy. It SAYS so rather
                // than pretending, and it leaves a trace - never a bare "Locked" (the owner is
                // red/green colourblind; the words are the whole signal).
                DeNelle.Core.Diagnostics.FlowTrace.Warn("Raid",
                    "raid card '" + id + "' is LOCKED with no LockReason - a lock path was added " +
                    "without player-facing copy. Showing a placeholder sentence.");
                lockCopy = "This expedition is not available yet.";
            }
            // Locked is the ONLY dimmed state left on a card (WO-1379 retired the cooldown dim).
            bool dimmed = locked;

            // Card root: a Cell tile (LayoutElement-sized for the scroll layout) with a
            // difficulty-tinted inner rim, and a Button so the whole plaque taps.
            var card = new GameObject("RaidCard_" + id, typeof(Image), typeof(Button));
            card.transform.SetParent(parent, false);
            // Kit scroll-column row law (MakeScrollZone runs childControlHeight:false): rows
            // carry their own height via sizeDelta, not a LayoutElement.
            var cardRt = card.GetComponent<RectTransform>();
            cardRt.sizeDelta = new Vector2(0f, CardHeightPx);
            var cardImg = card.GetComponent<Image>();
            // (#28) Obsidian row plate. Was ElarionUiKit.Cell (warm) + AddInnerRim(difficulty@0.7),
            // and AddInnerRim paints a near-full-surface tint (not a thin border) — with BlinkChrome
            // off that washed each whole card saturated green/yellow/red. A raised near-black tile +
            // a thin difficulty accent bar reads obsidian; the badge chip still carries the tier.
            cardImg.color = new Color(0.07f, 0.07f, 0.08f, 0.98f);
            ElarionUiKit.ApplyRounded(cardImg);
            var cardBtn = card.GetComponent<Button>();
            cardBtn.targetGraphic = cardImg;
            ElarionUiKit.StyleButtonColors(cardBtn);
            MedievalUiSkin.ApplyButton(cardBtn, primary: !dimmed);
            var medievalCard = Resources.Load<Sprite>("UI/ElarionMedieval/frames/content-panel");
            if (medievalCard != null)
            {
                cardImg.sprite = medievalCard;
                cardImg.type = Image.Type.Simple;
                cardImg.color = dimmed
                    ? new Color(.46f, .46f, .48f, .86f)
                    : Color.white;
            }
            string idCopy = id;
            cardBtn.onClick.AddListener(() => OnCardTapped(idCopy));

            // Difficulty accent — a thin left edge bar (the only colour on the obsidian tile).
            var accent = ElarionUiKit.AddImage(card.transform, "DiffAccent",
                new Vector2(0f, 0f), new Vector2(0.014f, 1f),
                new Color(tint.r, tint.g, tint.b, 0.95f), rounded: false);
            accent.GetComponent<Image>().raycastTarget = false;

            // Fortress name — gold serif title, top band. WO-714 P10: a raw id is never
            // player-visible — missing displayName routes through the ONE kit formatter.
            string name = string.IsNullOrEmpty(item.Name)
                ? ElarionUiKit.SpacedDisplayName(id) : item.Name;
            var nameLabel = ElarionUiKit.Label(card.transform, name, 0.66f, 0.91f, ElarionUi.Gilt,
                30, TMPro.TextAlignmentOptions.Left, 0.05f, 0.70f, bold: true);
            nameLabel.raycastTarget = false;
            // §1.14 fit-never-truncate: a long fortress name shrinks, never clips, at phone aspect.
            ElarionUiKit.FitSingleLine(nameLabel);

            // Difficulty badge — colour-tinted chip, top-right.
            var badge = ElarionUiKit.AddImage(card.transform, "DiffBadge",
                new Vector2(0.72f, 0.68f), new Vector2(0.96f, 0.92f),
                Color.white);
            var badgeImage = badge.GetComponent<Image>();
            var badgeFrame = Resources.Load<Sprite>("UI/ElarionMedieval/frames/status-panel-icon-socket");
            if (badgeImage != null)
            {
                badgeImage.raycastTarget = false;
                if (badgeFrame != null) { badgeImage.sprite = badgeFrame; badgeImage.type = Image.Type.Sliced; }
            }
            var badgeLbl = ElarionUiKit.Label(badge.transform, DifficultyLabel(_vm.DifficultyFor(id)), 0f, 1f,
                tint, 22, TMPro.TextAlignmentOptions.Center, 0.05f, 0.95f, bold: true);
            badgeLbl.raycastTarget = false;

            // 3-star target time — m:ss in gilt, mid band. Tofu fix (2026-07-02):
            // ★ (U+2605) is in NO project SDF font (scanned — zero m_Unicode:9733
            // hits), so the old "★★★" text rendered as boxes in builds. Procedural
            // gold diamonds instead (EndStateView's pattern via StarRatingRow).
            StarRatingRow.Build(card.transform, 3, 3, 0.05f, 0.40f, 0.20f, 0.58f, sizePx: 11f);
            // WO-1389 pressure point 4: the SCOUT LINE ("Iron walls . 15 defenders") shares the
            // clock band, right half, so a LOCKED card already says what the wins buy - and an
            // open card says what it is walking into. The clock label gives up its right half
            // (0.95 -> 0.54); both fit-never-clip. Absent on a def that authors neither fact.
            string scoutLine = _vm.ScoutLineFor(id);
            bool hasScout = !string.IsNullOrEmpty(scoutLine);
            var timeLabel = ElarionUiKit.Label(card.transform,
                "Clock: " + FormatTime(_vm.TargetTimeFor(id)), 0.38f, 0.60f,
                ElarionUi.Parchment, 28, TMPro.TextAlignmentOptions.Left, 0.22f, hasScout ? 0.54f : 0.95f);
            timeLabel.raycastTarget = false;
            ElarionUiKit.FitSingleLine(timeLabel);
            if (hasScout)
            {
                var scoutLabel = ElarionUiKit.Label(card.transform, scoutLine, 0.38f, 0.60f,
                    dimmed ? ElarionUi.ParchmentDim : ElarionUi.Parchment, 22,
                    TMPro.TextAlignmentOptions.Right, 0.56f, 0.95f);
                scoutLabel.raycastTarget = false;
                ElarionUiKit.FitSingleLine(scoutLabel);
                DeNelle.Core.Diagnostics.FlowTrace.Step("Raid",
                    "raid card '" + id + "' scout line: \"" + scoutLine + "\"" + (locked ? " (locked - shown before entry)" : ""));
            }
            else
            {
                DeNelle.Core.Diagnostics.FlowTrace.Warn("Raid",
                    "raid card '" + id + "' has NO scout line - scene-configs.json authors neither wallTier " +
                    "nor a garrison composition for it, so the locked card cannot say what the wins buy.");
            }

            // Bottom band - the reward hint when the camp is available, the LOCK SENTENCE
            // when it is not.
            //
            // ⛔ THE STATE IS CARRIED BY THE WORDS, NOT BY THE COLOUR (WO-728). The owner is
            // red/green colourblind, so a card that signalled its state by going grey or by
            // tinting the badge red would say NOTHING to her - and a card that just stops
            // responding to taps reads as a frozen game (the WO-1110 §2 dead-tap defect, found
            // on this very screen). So a locked camp SAYS what unlocks it; the dimming below is
            // decoration on top of a sentence that already stands on its own in greyscale.
            // (WO-1379: the "Recovering - raidable in {0}" branch that used to sit here is
            // retired with the per-camp wall; an empty Heartfire pool is answered at the door,
            // in the Heart's words, by OnCardTapped.)
            string bottomLine = locked
                ? lockCopy
                : RewardHint(_vm.RewardMultiplierFor(id), _vm.ShardChanceFor(id));
            var rewardLabel = ElarionUiKit.Label(card.transform,
                bottomLine, 0.18f, 0.34f,
                dimmed ? ElarionUi.ParchmentDim : ElarionUi.Affordable,
                22, TMPro.TextAlignmentOptions.Left, 0.05f, 0.95f, bold: true);
            rewardLabel.raycastTarget = false;
            // Kit 1.14 fit-never-truncate: the longest lock sentence must never clip.
            ElarionUiKit.FitSingleLine(rewardLabel);

            // THE CANON LINE — one sentence of target copy under the reward/lock band
            // (docs/CREATIVE_CANON_ELARION_2026-09-04.md §3 "Line on the target card"). It is
            // authored per row in scene-configs.json description; absent = the band is simply
            // not built, so every non-raid row and any future unauthored row stays correct.
            string flavour = _vm.DescriptionFor(id);
            if (!string.IsNullOrEmpty(flavour))
            {
                var flavourLabel = ElarionUiKit.Label(card.transform,
                    flavour, 0.03f, 0.17f, ElarionUi.ParchmentDim,
                    18, TMPro.TextAlignmentOptions.Left, 0.05f, 0.95f);
                flavourLabel.raycastTarget = false;
                // §1.14 fit-never-truncate: the longest authored line (The Broken Garrison,
                // 92 chars) must shrink, not clip, at phone aspect.
                ElarionUiKit.FitSingleLine(flavourLabel);
            }

            if (dimmed)
            {
                // Decoration only — the sentence above is the signal. The card stays TAPPABLE
                // on purpose: OnCardTapped answers with the refusal (the unlock requirement, or
                // the Heart's rekindle sentence), which is strictly more useful than an inert
                // button (and is what makes the state discoverable for a player who did not
                // read the line).
                cardImg.color = new Color(0.05f, 0.05f, 0.055f, 0.98f);
                nameLabel.color = ElarionUi.ParchmentDim;
            }
        }

        private void OnCardTapped(string id)
        {
            var def = _vm != null ? _vm.DefFor(id) : null;
            if (def == null)
            {
                // WO-1110 §2 — this was a bare `return`: the card visibly depressed and then
                // NOTHING happened, with no toast and no log. A dead tap reads to the player as
                // a frozen game, and left no trace for whoever gets the bug report.
                DeNelle.Core.Diagnostics.FlowTrace.Warn("Raid",
                    "raid card tap resolved NO SceneConfigDef - id='" + (id ?? "(null)") +
                    "' vm=" + (_vm == null ? "null" : "present") + ". The tap is dead; " +
                    "the card is on the grid but its def is missing from the catalog.");
                ElarionUiKit.ShowToast("That raid is unavailable right now.",
                    ElarionUiKit.ToastTone.Danger);
                return;
            }

            // 2026-09-04 - THE ESCALATION GATE, checked BEFORE the Heartfire gate. An unearned
            // camp cannot be marched on however many charges the Heart holds; answering with
            // a rekindle time would be advice that cannot possibly work. Never a silent no-op:
            // the toast repeats the exact sentence already printed on the card, so the two can
            // never drift.
            string tapLock = _vm != null ? _vm.LockReasonFor(id) : null;
            if (!string.IsNullOrEmpty(tapLock))
            {
                DeNelle.Core.Diagnostics.FlowTrace.Step("Raid",
                    "raid card tap REFUSED - '" + id + "' is locked (needs " +
                    _vm.UnlockVictoriesFor(id) + " victories; player has " + _vm.Victories +
                    "). Told the player: \"" + tapLock + "\"");
                ElarionUiKit.ShowToast(tapLock, ElarionUiKit.ToastTone.Info);
                return;
            }

            // WO-1379 (2026-09-05) - THE ONE GATE ON WHEN YOU MAY RAID, AND IT IS HEARTFIRE.
            // Owner, asked directly: "Heartfire replaces the camp wall." This block used to
            // refuse on RaidCooldownService.IsOnCooldown(id) (the WO-728 per-camp wall); that
            // gate is RETIRED here and must never come back beside this one - two lockouts
            // "reads as a bug" (WO-1379 section 3), and HeartfireRegression PIN F reds the
            // file if a second WHEN gate reappears.
            //
            // THE CHECK IS A READ, NOT THE SPEND. HeartfireService.HasCharge reconciles the
            // pool against the server-anchored clock and answers; the charge itself is spent
            // ONCE, at the raid ENTRY seam (RaidDeployController.TryInstall -> TrySpend), the
            // same seam every RaidBase_* entry funnels through. Spending here would double-
            // charge a player who backs out of the deploy screen. The Fail line that seam
            // logs on an empty pool is now unreachable from this door and stays in the code
            // (CLAUDE.md section 12: never strip FlowTrace) as the tripwire for any OTHER
            // door that opens a raid scene without passing this one.
            //
            // THE REFUSAL IS THE HEART'S SENTENCE, IN WORDS, WITH THE WAIT NAMED - never a
            // bare timer, never a colour (the owner is red/green colourblind), never a silent
            // no-op (the WO-1110 dead-tap defect this screen already shipped once). Kept at
            // the ONE door into RaidDeployScreen rather than inside the deploy screen:
            // refusing after the player has committed a warband would be a worse moment to
            // say no.
            int heartfireCharges = DeNelle.Village.World.Camps.HeartfireService.Charges;
            if (!DeNelle.Village.World.Camps.HeartfireService.HasCharge)
            {
                string heartfireBlocked = DeNelle.Village.World.Camps.HeartfireService.BlockedMessage();
                DeNelle.Core.Diagnostics.FlowTrace.Step(DeNelle.Village.World.Camps.HeartfireService.Sys,
                    "door refused: " + heartfireBlocked + " (camp='" + id + "', charges " +
                    heartfireCharges + "/" + DeNelle.Village.World.Camps.HeartfireService.Max + ")");
                ElarionUiKit.ShowToast(heartfireBlocked, ElarionUiKit.ToastTone.Info);
                return;
            }
            DeNelle.Core.Diagnostics.FlowTrace.Step(DeNelle.Village.World.Camps.HeartfireService.Sys,
                "door: charges " + heartfireCharges + " -> open (camp='" + id + "'). The charge is " +
                "spent at raid entry, not here.");

            RaidDeployScreen.Open(def);
            // UIF-01: the deploy screen registers with the single-modal arbiter, so opening it
            // now CLOSES this grid (one modal at a time — the Echo roster->card precedent). The
            // deploy screen is the sole visible modal; closing it returns to the world, not the grid.
        }

        // ── Card data helpers (read straight off VM-projected values) ──────────

        // Difficulty -> tint: green (Regular) / yellow (Hard) / red (Extreme).
        private static Color DifficultyColor(string difficulty)
        {
            switch ((difficulty ?? "Regular").Trim().ToLowerInvariant())
            {
                case "extreme": return ElarionUi.Danger;                       // red
                case "hard":    return new Color(0.92f, 0.78f, 0.28f, 1f);      // yellow/gold
                default:        return ElarionUi.Affordable;                    // green (Regular)
            }
        }

        private static string DifficultyLabel(string difficulty)
        {
            if (string.IsNullOrEmpty(difficulty)) return "Regular";
            string d = difficulty.Trim();
            return char.ToUpper(d[0]) + (d.Length > 1 ? d.Substring(1).ToLowerInvariant() : "");
        }

        // Seconds -> m:ss. A non-positive time reads "--:--".
        private static string FormatTime(float seconds)
        {
            if (seconds <= 0f) return "--:--";
            int total = Mathf.RoundToInt(seconds);
            int m = total / 60;
            int s = total % 60;
            return m + ":" + s.ToString("00");
        }

        // Honest loot mult: RaidScoring.ComputeLoot applies rewardMultiplier to crystals/food.
        // Echo-Shard % is NOT a live currency grant path — do not show it as a drop chance.
        private static string RewardHint(float rewardMultiplier, float shardDropChance)
        {
            float mult = rewardMultiplier <= 0f ? 1f : rewardMultiplier;
            // SWEEP 9413 R2 (#3): "◆" is not in the build TMP font — ASCII only.
            // shardDropChance intentionally unused until a real shard grant ships.
            _ = shardDropChance;
            return "- x" + mult.ToString("0.#") + " Loot";
        }

        // ── Scroll list — the kit scroll zone owns all plumbing (WO-714 W4) ────

        private void FinalizeScroll()
        {
            if (_scroll == null || _scroll.content == null) return;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_scroll.content);
        }

        private void ClearContent()
        {
            _scroll = null;
            if (_bodyZone == null) return;
            for (int i = _bodyZone.childCount - 1; i >= 0; i--)
            {
                var c = _bodyZone.GetChild(i);
                // The kit's zone backing plate is the FIRST child the factory adds — keep any
                // Image-only backing named by the kit, clear everything the screen added.
                if (c != null && c.name != "ZoneBacking") Destroy(c.gameObject);
            }
        }

        public void Close()
        {
            // UIF-01: release the arbiter slot (no-op if already swapped out).
            if (_panelHandle != null) PanelManager.NotifyClosed(_panelHandle);
            _vm?.Dispose();
            _vm = null;
            // WO-714 P8: eased fade/scale-out through the ONE kit FX (falls back to an
            // immediate Destroy when the FX is absent / not playing).
            if (_ui != null) ElarionUiKit.ClosePanelWithFx(_ui);
            _ui = null;
            _bodyZone = null;
            _scroll = null;
            IsScreenOpen = false;   // WO-725: lets the Herald re-arm + fires its Arena close trace
        }

        private void OnDestroy()
        {
            // UIF-01: don't leak the arbiter slot if destroyed while open (scene unload).
            if (_panelHandle != null) PanelManager.NotifyClosed(_panelHandle);
            _vm?.Dispose();
            _vm = null;
            if (_instance == this) _instance = null;
            if (_ui != null) Destroy(_ui);
            IsScreenOpen = false;   // WO-725: scene-change safety — never leave the static stuck true
        }
    }
}
