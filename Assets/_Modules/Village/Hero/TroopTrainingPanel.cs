// =============================================================================
// TroopTrainingPanel — the Barracks "train troops" UI (WO-453 troop-training flow;
// WO-733 unlock ladder + WO-737 Obsidian master-detail layout).
// A DUMB SKIN over the SHARED kit chrome: it INHERITS BuildObsidianPanel
// (FrameCrafting master-detail + zones + the ONE shared Close) and only DISPLAYS +
// routes commands. ALL logic (catalog, cost, cap, UNLOCK, train) lives in the
// services it CALLS (TroopCatalog / TroopUnlock / ArmyStorage / EconomyService /
// TroopDialogueCommands) — the panel defines none of it. Presentation NEVER invents
// a game rule; it only projects IsTrainable / LockedReason / afford / cap.
// -----------------------------------------------------------------------------
// WO-737 layout contract (owner-ratified FrameCrafting master-detail template):
//   * bodyLeft  (dark well)      = a SCROLLABLE ladder of ALL 7 troops (kit
//                                  MakeScrollZone), sorted by UnlockBarracksTier then
//                                  catalog order. LOCKED troops stay VISIBLE (ladder
//                                  education) — selected=Yellow, unlocked=Gray,
//                                  locked=Gray+LockedTint plate + lock chip + dim icon.
//   * bodyRight (parchment well) = the selected troop's detail card in dark INK, laid
//                                  out in non-overlapping Y bands: name / role.slots.
//                                  unlock / portrait socket / owned.recovering / army
//                                  cap / combat stats / cost / STATE BLOCK / hint / CTA.
//                                  STATE BLOCK is mutually exclusive A(locked) /
//                                  B(cant-afford) / C(affordable).
//   * footer    (action strip)   = the ONE kit wallet row (wood/iron/food/crystal).
// The ONE shared Close is the chrome's (no per-panel X / bespoke close). No second
// BuildObsidianPanel is nested — the detail lives in the parchment bodyRight zone.
//
// Code-built uGUI (NO UXML — canon §8 / §1). Open()/Close() API — opened by
// TroopDialogueCommands.ShowTrainingUI (the <<ShowTrainingUI>> command), which
// self-heals a host if none exists.
// =============================================================================

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DeNelle.Core.UI;
using DeNelle.Core.State;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village.Hero
{
    public sealed class TroopTrainingPanel : MonoBehaviour
    {
        private GameObject _ui;
        private Transform _troopHost;      // bodyLeft — dark list well (hosts the scroll zone)
        private RectTransform _listContent; // the scroll content the troop rows parent into
        private Transform _detailHost;     // bodyRight — parchment detail well
        // WO-714 P2: the footer wallet is a row of kit CurrencyChips — the ONE currency
        // read (chip owns CompactNumber/icon/tag; no hand-formatted wallet string ever).
        private ElarionUiKit.CurrencyChipHandle[] _wallet;
        private System.Action<ResourceSnapshot> _ecoHandler;

        private string _selectedTroopId;
        // Static instruction (never mutates — transient train feedback is a kit toast, P5).
        private const string DetailHint = "Train troops to defend Elarion and raid enemy camps.";

        // Dark ink for text sitting ON the parchment detail well (family convention).
        private static readonly Color Ink     = new Color(0.16f, 0.12f, 0.08f, 1f);
        private static readonly Color InkDim  = new Color(0.34f, 0.28f, 0.20f, 1f);
        private static readonly Color InkGood = new Color(0.10f, 0.42f, 0.16f, 1f);
        private static readonly Color InkBad  = new Color(0.55f, 0.12f, 0.10f, 1f);

        // Row plate state tints (dark list well). Selected = warm gold; unlocked = neutral
        // steel; locked = neutral * LockedTint (mirrors BuildingUpgradePanelMvvm.LockedTint).
        private static readonly Color RowSelected = new Color(0.42f, 0.34f, 0.14f, 0.95f);
        private static readonly Color RowUnlocked = new Color(0.16f, 0.16f, 0.18f, 0.92f);
        private static readonly Color LockedTint  = new Color(0.52f, 0.52f, 0.55f, 0.80f);

        // Sprite-first row plate (canon §5) — the talent slot plate; procedural fallback on null.
        private const string SlotTalentPlate = "slot_talent_1";
        private const string SlotItemPlate   = "slot_item";

        private const float RowHeightPx = 80f;   // two-line row + touch floor (mobile)
        private const float RowGapPx    = 6f;

        public void Open()
        {
            // WO-724: instrument the train-UI open path (acceptance #5). The panel is only
            // reachable once the Barracks is unlocked (ff.barracks + founding-complete).
            FlowTrace.Step("Barracks", "TroopTrainingPanel.Open - building the train UI (kit chrome, no UXML).");

            Close();

            // Modal canvas + tap-outside scrim, both from the shared kit. Pin sortingOrder
            // 31000 + overrideSorting so the panel + its scrim render ABOVE the world-HUD band.
            _ui = ElarionUiKit.BuildModalCanvas("TroopTrainingPanelUI", 31000);
            var canvas = _ui.GetComponent<Canvas>();
            if (canvas != null) canvas.overrideSorting = true;
            ElarionUiKit.Scrim(_ui.transform, onTapClose: Close);

            // SHARED Obsidian chrome (FrameCrafting master-detail): black panel + gold trim +
            // gold header + medallion + the ONE shared Close — all built by the kit. The panel
            // adds NO chrome and NO close of its own. ASCII title (no em-dash; device tofu risk).
            var chrome = ElarionUiKit.BuildObsidianPanel(_ui.transform, "Barracks - Train",
                new Vector2(0.10f, 0.08f), new Vector2(0.90f, 0.92f), Close,
                frameName: RpgUiCatalog.FrameCrafting, medallionIcon: "sword");

            var layout = chrome.layout;
            _troopHost = layout != null && layout.bodyLeft != null
                ? (Transform)layout.bodyLeft
                : (layout != null && layout.body != null ? (Transform)layout.body : chrome.content.transform);
            _detailHost = layout != null && layout.bodyRight != null
                ? (Transform)layout.bodyRight
                : (layout != null && layout.body != null ? (Transform)layout.body : chrome.content.transform);

            // WO-737: the dark list well becomes a vertical scroller (kit §1.14 MakeScrollZone) —
            // the 7-troop ladder can exceed the well on a phone. ONE call; the rows parent into
            // the returned content column. Built ONCE here; Rebuild only repaints the rows.
            var scroll = ElarionUiKit.MakeScrollZone(_troopHost, RowGapPx, 6);
            _listContent = scroll != null ? scroll.content : null;

            // WO-714 P2: the footer wallet = the ONE kit wallet strip (CurrencyChip rows —
            // icon + tag + CompactNumber owned by the chip; no hand-formatted string).
            var footHost = layout != null && layout.footer != null
                ? (Transform)layout.footer : chrome.content.transform;
            _wallet = ElarionUiKit.BuildWalletRow(footHost, new[]
            {
                ElarionUiKit.CurrencyKind.Wood,
                ElarionUiKit.CurrencyKind.Iron,
                ElarionUiKit.CurrencyKind.Food,
                ElarionUiKit.CurrencyKind.Crystal,
            });

            // WO-714 P8: the ONE shared open ease (scale target = the panel rect).
            ElarionUiKit.AttachPanelOpenFx(_ui,
                chrome.root != null ? chrome.root.transform as RectTransform : null);

            // The economy readout tracks the wallet live (unchanged seam; presentation refresh).
            if (EconomyService.Instance != null)
            {
                _ecoHandler = _ => Rebuild();
                EconomyService.Instance.OnChanged += _ecoHandler;
            }

            Rebuild();

            Debug.Log("[TroopTrainingPanel] Opened — barracks troop training.");
        }

        // The persisted army roster (GameState.Army), null when no save service is live.
        private static ArmyStorage Army()
        {
            var svc = GameStateService.Instance;
            return svc != null && svc.State != null ? svc.State.Army : null;
        }

        // ALL catalog troops, sorted by UnlockBarracksTier ASC then catalog order (stable
        // insertion sort — preserves catalog order for equal tiers). Locked troops are
        // NEVER filtered out (ladder education, WO-733/737).
        private static List<TroopDef> SortedRoster()
        {
            var list = new List<TroopDef>();
            foreach (var d in TroopCatalog.All) if (d != null) list.Add(d);
            for (int i = 1; i < list.Count; i++)
            {
                var key = list[i];
                int j = i - 1;
                while (j >= 0 && list[j].UnlockBarracksTier > key.UnlockBarracksTier)
                {
                    list[j + 1] = list[j];
                    j--;
                }
                list[j + 1] = key;
            }
            return list;
        }

        // Re-project the whole master-detail from the live services after every train.
        private void Rebuild()
        {
            if (_detailHost == null) return;

            FlowTrace.Step("Barracks", "TroopTrainingPanel.Rebuild - projecting the roster ladder + detail.");

            UpdateWallet();

            var army = Army();
            var troops = SortedRoster();

            // Keep the selection valid (first troop by default).
            if (troops.Count > 0)
            {
                bool found = false;
                foreach (var d in troops) if (d.Id == _selectedTroopId) { found = true; break; }
                if (!found) _selectedTroopId = troops[0].Id;
            }

            // ── bodyLeft: the troop ladder (dark well, scrollable) ──
            var rowHost = _listContent != null ? (Transform)_listContent : _troopHost;
            if (rowHost != null)
            {
                for (int i = rowHost.childCount - 1; i >= 0; i--)
                    Destroy(rowHost.GetChild(i).gameObject);

                if (troops.Count == 0)
                {
                    MakeText(rowHost, "No troops available.", 13, ElarionUi.ParchmentDim,
                        FontStyles.Italic, TextAlignmentOptions.Center,
                        new Vector2(0.05f, 0.40f), new Vector2(0.95f, 0.60f));
                }
                else
                {
                    // §12: guard EACH row so one bad def logs + is skipped, never blanks the list.
                    Guard.TryEach("Barracks", "troop-row", troops, d => BuildRow(rowHost, d, army));
                }
            }

            // ── bodyRight: the selected troop's detail card (parchment well) ──
            for (int i = _detailHost.childCount - 1; i >= 0; i--)
                Destroy(_detailHost.GetChild(i).gameObject);

            var def = TroopCatalog.Find(_selectedTroopId);
            if (def != null) BuildDetail(def, army);
            else
                MakeText(_detailHost, "Select a troop.", 15, InkDim, FontStyles.Italic,
                    TextAlignmentOptions.Center, new Vector2(0.05f, 0.45f), new Vector2(0.95f, 0.55f));
        }

        // ── One troop ROW (dark list well). Anatomy L->R: icon | name + role line |
        //    owned xN badge + tier chip / lock glyph. Locked rows stay selectable so the
        //    detail card can explain the unlock. State by plate tint + text + chip (not
        //    colour alone — colorblind-safe). ──
        private void BuildRow(Transform parent, TroopDef def, ArmyStorage army)
        {
            string id = def.Id;
            bool selected = id == _selectedTroopId;
            bool locked   = !TroopUnlock.IsTrainable(def);
            float dim = locked ? 0.5f : 1f;

            var row = new GameObject("TroopRow_" + id, typeof(Image), typeof(Button), typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            var le = row.GetComponent<LayoutElement>();
            le.preferredHeight = RowHeightPx;
            le.minHeight = RowHeightPx;

            // Plate — sprite-first talent slot (procedural fallback), tinted by state.
            var plate = row.GetComponent<Image>();
            var slot = RpgUiCatalog.Get(RpgUiCatalog.RoleSlot, SlotTalentPlate);
            if (slot != null) { plate.sprite = slot; plate.type = Image.Type.Sliced; plate.fillCenter = true; }
            Color baseCol = selected ? RowSelected : RowUnlocked;
            if (locked)
                baseCol = new Color(baseCol.r * LockedTint.r, baseCol.g * LockedTint.g,
                                    baseCol.b * LockedTint.b, baseCol.a * LockedTint.a);
            plate.color = baseCol;

            var btn = row.GetComponent<Button>();
            btn.targetGraphic = plate;
            ElarionUiKit.StyleButtonColors(btn);
            // EVERY row is selectable (locked included) — tapping a locked row selects it so
            // the detail card explains the unlock (WO-737 row interaction table).
            btn.onClick.AddListener(() => { _selectedTroopId = id; Rebuild(); });

            // Selected non-colour cue: a gold left edge bar (shape + position, not hue alone).
            if (selected)
            {
                var bar = new GameObject("SelBar", typeof(Image));
                bar.transform.SetParent(row.transform, false);
                var brt = bar.GetComponent<RectTransform>();
                brt.anchorMin = new Vector2(0f, 0.08f); brt.anchorMax = new Vector2(0.02f, 0.92f);
                brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero;
                var bImg = bar.GetComponent<Image>();
                bImg.color = ElarionUi.Gilt;
                bImg.raycastTarget = false;
            }

            // ICON (left) — sprite when it resolves, else a role glyph; dim 0.5 when locked.
            var iconSprite = TroopIcon(def);
            if (iconSprite != null)
            {
                var iconGo = new GameObject("Icon", typeof(Image));
                iconGo.transform.SetParent(row.transform, false);
                var irt = iconGo.GetComponent<RectTransform>();
                irt.anchorMin = new Vector2(0.04f, 0.16f); irt.anchorMax = new Vector2(0.17f, 0.84f);
                irt.offsetMin = Vector2.zero; irt.offsetMax = Vector2.zero;
                var iImg = iconGo.GetComponent<Image>();
                iImg.sprite = iconSprite;
                iImg.preserveAspect = true;
                iImg.raycastTarget = false;
                iImg.color = new Color(1f, 1f, 1f, dim);
            }
            else
            {
                var g = ElarionUiKit.Label(row.transform, RoleGlyph(def),
                    0.16f, 0.84f,
                    new Color(ElarionUi.Gilt.r, ElarionUi.Gilt.g, ElarionUi.Gilt.b, dim),
                    ElarionUi.FontTitle, TextAlignmentOptions.Center, 0.04f, 0.17f, bold: true);
                g.raycastTarget = false;
                ElarionUiKit.FitSingleLine(g);
            }

            // NAME (line 1) — DisplayName, never a raw id.
            Color nameCol = selected ? ElarionUi.Gilt : ElarionUi.Parchment;
            nameCol = new Color(nameCol.r, nameCol.g, nameCol.b, dim);
            var nameLbl = ElarionUiKit.Label(row.transform, DisplayName(def), 0.52f, 0.92f,
                nameCol, ElarionUi.FontBody, TextAlignmentOptions.MidlineLeft, 0.20f, 0.74f, bold: true);
            nameLbl.raycastTarget = false;
            ElarionUiKit.FitSingleLine(nameLbl);

            // ROLE line (line 2) — melee / ranged, dim.
            string role = string.IsNullOrEmpty(def.Role) ? "" : Capitalize(def.Role);
            if (!string.IsNullOrEmpty(role))
            {
                var roleLbl = ElarionUiKit.Label(row.transform, role, 0.10f, 0.48f,
                    new Color(ElarionUi.ParchmentDim.r, ElarionUi.ParchmentDim.g, ElarionUi.ParchmentDim.b, dim),
                    ElarionUi.FontLabel, TextAlignmentOptions.MidlineLeft, 0.20f, 0.74f);
                roleLbl.raycastTarget = false;
                ElarionUiKit.FitSingleLine(roleLbl);
            }

            // RIGHT chip — locked: "T{n}" (the tier needed) + lock text; unlocked: owned "xN".
            if (locked)
            {
                var chip = ElarionUiKit.Label(row.transform, "T" + def.UnlockBarracksTier + " LOCK",
                    0.30f, 0.70f,
                    new Color(ElarionUi.ParchmentDim.r, ElarionUi.ParchmentDim.g, ElarionUi.ParchmentDim.b, 0.9f),
                    ElarionUi.FontLabel, TextAlignmentOptions.MidlineRight, 0.74f, 0.97f, bold: true);
                chip.raycastTarget = false;
                ElarionUiKit.FitSingleLine(chip);
            }
            else
            {
                int owned = OwnedCount(army, id);
                if (owned > 0)
                {
                    var badge = ElarionUiKit.Label(row.transform, "x" + owned, 0.30f, 0.70f,
                        ElarionUi.Gilt, ElarionUi.FontBody, TextAlignmentOptions.MidlineRight,
                        0.74f, 0.97f, bold: true);
                    badge.raycastTarget = false;
                    ElarionUiKit.FitSingleLine(badge);
                }
            }
        }

        // ── The selected troop's detail card (parchment well) — non-overlapping Y bands
        //    (WO-737 bodyRight anatomy). The STATE BLOCK (band 0.16-0.37) is mutually
        //    exclusive A(locked) / B(cant-afford) / C(affordable); the CTA row (0.03-0.14)
        //    is Green+interactable only in C. ──
        private void BuildDetail(TroopDef def, ArmyStorage army)
        {
            string name = DisplayName(def);
            int owned = OwnedCount(army, def.Id);

            bool trainable = TroopUnlock.IsTrainable(def);
            var cost = CostOf(def);
            bool affordable = EconomyService.Instance == null || EconomyService.Instance.CanAfford(cost);
            bool hasRoom = army == null || army.CanTrain(def.Id, TroopDialogueCommands.SlotOf);
            bool canTrain = trainable && affordable && hasRoom;

            // 0.92-0.99  DisplayName (bold title).
            MakeText(_detailHost, name, 20, Ink, FontStyles.Bold,
                TextAlignmentOptions.Center, new Vector2(0.06f, 0.92f), new Vector2(0.94f, 0.99f));

            // 0.86-0.91  Role . Slots . Unlock  (ASCII " - " separators).
            string slotWord = def.Slots == 1 ? "1 slot" : def.Slots + " slots";
            string roleLine = Capitalize(def.Role) + "  -  " + slotWord + "  -  Barracks T" + def.UnlockBarracksTier;
            MakeText(_detailHost, roleLine, 13, InkDim, FontStyles.Normal,
                TextAlignmentOptions.Center, new Vector2(0.06f, 0.855f), new Vector2(0.94f, 0.915f));

            // 0.72-0.85  Portrait / icon socket (slot art + troop icon; dim when locked).
            BuildPortraitSocket(def, trainable);

            // 0.64-0.71  Owned . Recovering.
            int woundedOfType = WoundedCount(army, def.Id);
            MakeText(_detailHost, "Owned:  " + owned, 14, InkDim, FontStyles.Normal,
                TextAlignmentOptions.Center, new Vector2(0.06f, 0.64f), new Vector2(0.50f, 0.71f));
            if (woundedOfType > 0)
                MakeText(_detailHost, "Recovering:  " + woundedOfType, 13, InkBad, FontStyles.Italic,
                    TextAlignmentOptions.Center, new Vector2(0.50f, 0.64f), new Vector2(0.94f, 0.71f));

            // 0.58-0.63  Army cap.
            string capLine;
            if (army == null) capLine = "Army:  -";
            else capLine = "Army:  " + army.SlotsUsed(TroopDialogueCommands.SlotOf) + " / " + army.MaxArmySize + " slots";
            Color capColor = (army != null && !hasRoom) ? InkBad : InkDim;
            MakeText(_detailHost, capLine, 13, capColor, FontStyles.Normal,
                TextAlignmentOptions.Center, new Vector2(0.06f, 0.575f), new Vector2(0.94f, 0.635f));

            // 0.48-0.57  Combat stats (one line).
            string statLine = "HP " + Mathf.RoundToInt(def.MaxHp) +
                              "   -   DMG " + Mathf.RoundToInt(def.AttackDamage) +
                              "   -   Range " + def.AttackRange.ToString("0.#");
            MakeText(_detailHost, statLine, 13, Ink, FontStyles.Normal,
                TextAlignmentOptions.Center, new Vector2(0.06f, 0.48f), new Vector2(0.94f, 0.555f));

            // 0.38-0.47  Cost (tinted Good/Bad by afford; only meaningful once unlocked).
            Color costColor = !trainable ? InkDim
                : (EconomyService.Instance == null ? InkDim : (affordable ? InkGood : InkBad));
            MakeText(_detailHost, "Cost:  " + CostString(def), 15, costColor, FontStyles.Bold,
                TextAlignmentOptions.Center, new Vector2(0.06f, 0.385f), new Vector2(0.94f, 0.465f));

            // 0.16-0.37  STATE BLOCK (mutually exclusive) + 0.16-0.26 Hint.
            if (!trainable)
            {
                // A — LOCKED plate (never red; lock is not destructive). Parchment veil.
                var plate = new GameObject("LockPlate", typeof(Image));
                plate.transform.SetParent(_detailHost, false);
                var prt = plate.GetComponent<RectTransform>();
                prt.anchorMin = new Vector2(0.08f, 0.16f); prt.anchorMax = new Vector2(0.92f, 0.37f);
                prt.offsetMin = Vector2.zero; prt.offsetMax = Vector2.zero;
                var pImg = plate.GetComponent<Image>();
                var slotSprite = RpgUiCatalog.Get(RpgUiCatalog.RoleSlot, SlotItemPlate);
                if (slotSprite != null) { pImg.sprite = slotSprite; pImg.type = Image.Type.Sliced; pImg.fillCenter = true; }
                pImg.color = new Color(0.30f, 0.26f, 0.20f, 0.45f);
                pImg.raycastTarget = false;

                MakeText(plate.transform, "LOCKED", 15, InkDim, FontStyles.Bold,
                    TextAlignmentOptions.Center, new Vector2(0.04f, 0.66f), new Vector2(0.96f, 0.98f));
                // LockedReason already carries "Unlocks at Barracks Tier {n} - {TierName}".
                MakeText(plate.transform, TroopUnlock.LockedReason(def), 13, Ink, FontStyles.Normal,
                    TextAlignmentOptions.Center, new Vector2(0.04f, 0.36f), new Vector2(0.96f, 0.66f));
                MakeText(plate.transform, "Upgrade the Barracks to recruit.", 12, InkDim, FontStyles.Italic,
                    TextAlignmentOptions.Center, new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.34f));
            }
            else
            {
                // B / C — a one-line readiness note in the state band (never colour alone).
                string note; Color noteCol;
                if (canTrain) { note = "Ready to train."; noteCol = InkGood; }
                else if (!hasRoom) { note = "Army cap full - deploy or expand."; noteCol = InkBad; }
                else { note = "Not enough resources."; noteCol = InkBad; }
                MakeText(_detailHost, note, 13, noteCol, FontStyles.Bold,
                    TextAlignmentOptions.Center, new Vector2(0.06f, 0.285f), new Vector2(0.94f, 0.365f));

                // Static hint (WO-714 P5: transient feedback is a kit toast, not a stale label).
                MakeText(_detailHost, DetailHint, 12, InkDim, FontStyles.Italic,
                    TextAlignmentOptions.Center, new Vector2(0.06f, 0.16f), new Vector2(0.94f, 0.26f));
            }

            // 0.03-0.14  CTA row — Train / Train x5. Green + interactable only when canTrain
            // (state C); Gray + non-interactable in A (locked) and B (cap/afford). Text + enabled
            // state carry the meaning (colorblind-safe); NEVER red for a lock.
            var b1 = ElarionUiKit.BuildObsidianButton(_detailHost, "Train",
                ElarionUiKit.ObsidianButtonStyle.Style1,
                canTrain ? ElarionUiKit.ObsidianButtonColor.Green : ElarionUiKit.ObsidianButtonColor.Gray,
                new Vector2(0.08f, 0.03f), new Vector2(0.50f, 0.14f),
                () => TrainAndRefresh(def.Id, 1));
            if (b1 != null) b1.interactable = canTrain;

            var b5 = ElarionUiKit.BuildObsidianButton(_detailHost, "Train x5",
                ElarionUiKit.ObsidianButtonStyle.Style1,
                canTrain ? ElarionUiKit.ObsidianButtonColor.Yellow : ElarionUiKit.ObsidianButtonColor.Gray,
                new Vector2(0.52f, 0.03f), new Vector2(0.92f, 0.14f),
                () => TrainAndRefresh(def.Id, 5));
            if (b5 != null) b5.interactable = canTrain;
        }

        // Portrait / icon socket (detail band 0.72-0.85): slot art plate + centred troop icon.
        private void BuildPortraitSocket(TroopDef def, bool trainable)
        {
            var socket = new GameObject("PortraitSocket", typeof(Image));
            socket.transform.SetParent(_detailHost, false);
            var srt = socket.GetComponent<RectTransform>();
            srt.anchorMin = new Vector2(0.40f, 0.72f); srt.anchorMax = new Vector2(0.60f, 0.85f);
            srt.offsetMin = Vector2.zero; srt.offsetMax = Vector2.zero;
            var sImg = socket.GetComponent<Image>();
            var slotSprite = RpgUiCatalog.Get(RpgUiCatalog.RoleSlot, SlotItemPlate);
            if (slotSprite != null) { sImg.sprite = slotSprite; sImg.type = Image.Type.Sliced; sImg.fillCenter = true; }
            sImg.color = new Color(0.22f, 0.18f, 0.13f, trainable ? 0.85f : 0.45f);
            sImg.raycastTarget = false;

            var iconSprite = TroopIcon(def);
            float dim = trainable ? 1f : 0.5f;
            if (iconSprite != null)
            {
                var iconGo = new GameObject("Icon", typeof(Image));
                iconGo.transform.SetParent(socket.transform, false);
                var irt = iconGo.GetComponent<RectTransform>();
                irt.anchorMin = new Vector2(0.15f, 0.15f); irt.anchorMax = new Vector2(0.85f, 0.85f);
                irt.offsetMin = Vector2.zero; irt.offsetMax = Vector2.zero;
                var iImg = iconGo.GetComponent<Image>();
                iImg.sprite = iconSprite;
                iImg.preserveAspect = true;
                iImg.raycastTarget = false;
                iImg.color = new Color(1f, 1f, 1f, dim);
            }
            else
            {
                var g = MakeText(socket.transform, RoleGlyph(def), 22,
                    new Color(ElarionUi.Parchment.r, ElarionUi.Parchment.g, ElarionUi.Parchment.b, dim),
                    FontStyles.Bold, TextAlignmentOptions.Center,
                    new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.95f));
                g.raycastTarget = false;
            }
        }

        // WO-714 P2: amounts flow through the chips' SetAmount (count-tween; CompactNumber
        // formatting lives inside the chip — WO-697 law, currency-ellipsis forbidden).
        private void UpdateWallet()
        {
            if (_wallet == null || _wallet.Length < 4 || EconomyService.Instance == null) return;
            var e = EconomyService.Instance;
            if (_wallet[0] != null) _wallet[0].SetAmount(e.Wood);
            if (_wallet[1] != null) _wallet[1].SetAmount(e.Iron);
            if (_wallet[2] != null) _wallet[2].SetAmount(e.Food);
            if (_wallet[3] != null) _wallet[3].SetAmount(e.Crystals);
        }

        private void TrainAndRefresh(string troopId, int qty)
        {
            var def = TroopCatalog.Find(troopId);
            // WO-714 P10: never toast a raw troop id.
            string name = def != null && !string.IsNullOrEmpty(def.DisplayName)
                ? def.DisplayName : ElarionUiKit.SpacedDisplayName(troopId);

            // WO-733/737: presentation projects the unlock rule for the refuse toast. The CTA is
            // already disabled when locked, so this is a defensive belt-and-suspenders path.
            if (def != null && !TroopUnlock.IsTrainable(def))
            {
                ElarionUiKit.ShowToast(name + " unlocks at Barracks Tier " + def.UnlockBarracksTier + ".",
                    ElarionUiKit.ToastTone.Danger);
                Rebuild();
                return;
            }

            int trained = TroopDialogueCommands.Train(troopId, qty);
            if (trained > 0)
            {
                // WO-714 P5: transient feedback through the ONE kit toast, never a stuck label.
                ElarionUiKit.ShowToast("Trained " + trained + "x " + name + ".", ElarionUiKit.ToastTone.Confirm);
                // Push the fresh economy snapshot to the town HUD too (mirrors ShopPanel).
                var eco = EconomyService.Instance;
                if (eco != null)
                    DeNelle.Core.CoreServices.Hud?.SetResources(eco.Wood, eco.Iron, eco.Food, eco.Crystals);
                GameStateService.Instance?.Save();
            }
            else
            {
                ElarionUiKit.ShowToast("Couldn't train " + name + " - army cap full or not enough resources.",
                    ElarionUiKit.ToastTone.Danger);
            }
            Rebuild();   // re-project owned counts / cap / affordability after the attempt
        }

        // WO-714 P10: DisplayName only — a raw troop id is never player-visible.
        private static string DisplayName(TroopDef def)
        {
            if (def == null) return "";
            return string.IsNullOrEmpty(def.DisplayName)
                ? ElarionUiKit.SpacedDisplayName(def.Id) : def.DisplayName;
        }

        // Role glyph fallback when no icon sprite resolves (ASCII initial).
        private static string RoleGlyph(TroopDef def)
        {
            if (def != null && def.Role == "ranged") return "R";
            return "M";
        }

        private static string Capitalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return char.ToUpperInvariant(s[0]) + (s.Length > 1 ? s.Substring(1) : "");
        }

        // Troop icon: the authored IconId (WO-735 art) first, else a role glyph icon from the
        // committed RpgUi icon set. Null-safe — a null result draws a letter glyph instead.
        private static Sprite TroopIcon(TroopDef def)
        {
            if (def == null) return null;
            if (!string.IsNullOrEmpty(def.IconId))
            {
                var s = RpgUiCatalog.Get(RpgUiCatalog.RoleIcons, def.IconId);
                if (s != null) return s;
            }
            string roleIcon = def.Role == "ranged" ? "icon_combat" : "icon_sword";
            return RpgUiCatalog.Get(RpgUiCatalog.RoleIcons, roleIcon);
        }

        private static int OwnedCount(ArmyStorage army, string troopId)
        {
            if (army == null || army.Owned == null) return 0;
            int n = 0;
            foreach (var t in army.Owned)
                if (t != null && t.TroopDefId == troopId) n++;
            return n;
        }

        // WO-724: count owned troops of this type currently wounded (recovering) - blocked
        // from deploy until ArmyStorage.TickRecovery clears the flag.
        private static int WoundedCount(ArmyStorage army, string troopId)
        {
            if (army == null || army.Owned == null) return 0;
            int n = 0;
            foreach (var t in army.Owned)
                if (t != null && t.TroopDefId == troopId && t.Wounded) n++;
            return n;
        }

        private static ResourceCost CostOf(TroopDef def)
        {
            // ResourceCost ctor order is (wood, food, iron, crystals).
            return def == null ? new ResourceCost() : new ResourceCost(def.CostWood, def.CostFood, def.CostIron);
        }

        private string CostString(TroopDef def)
        {
            if (def == null) return "Free";
            var parts = new List<string>();
            if (def.CostWood > 0) parts.Add(def.CostWood + "W");
            if (def.CostIron > 0) parts.Add(def.CostIron + "I");
            if (def.CostFood > 0) parts.Add(def.CostFood + "F");
            return parts.Count == 0 ? "Free" : string.Join(" ", parts);
        }

        // ── uGUI helper (mirrors VillageCraftingPanel.MakeText) ───────────────────

        private static TextMeshProUGUI MakeText(Transform parent, string text, float size,
            Color color, FontStyles style, TextAlignmentOptions align, Vector2 min, Vector2 max)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = min; rt.anchorMax = max;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.fontStyle = style;
            t.alignment = align;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            ElarionUiKit.EnsureFont(t);
            return t;
        }

        public void Close()
        {
            if (_ecoHandler != null && EconomyService.Instance != null)
                EconomyService.Instance.OnChanged -= _ecoHandler;
            _ecoHandler = null;
            _wallet = null;
            _troopHost = null;
            _listContent = null;
            _detailHost = null;
            // WO-714 P8: eased fade/scale-out through the ONE kit FX (falls back to an
            // immediate Destroy when the FX is absent / not playing).
            if (_ui != null) ElarionUiKit.ClosePanelWithFx(_ui);
            _ui = null;
        }

        private void OnDestroy()
        {
            if (_ecoHandler != null && EconomyService.Instance != null)
                EconomyService.Instance.OnChanged -= _ecoHandler;
            _ecoHandler = null;
            if (_ui != null) Destroy(_ui);
        }
    }
}
