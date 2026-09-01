// =============================================================================
// InventorySidebar — the Bag's PANE: detail and comparison, always present.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WO-1133 D3. This replaces the thin full-width strip whose entire vocabulary was
// "Tap an item to inspect it." — a gold band louder than the two items it described
// (captured defect 5). The pane is 30% of the interior, always on screen, and always
// says something the player did not already know:
//
//   * NOTHING SELECTED -> what is worn, and the highest-value GAP. The screen answers
//     "what am I wearing" before it is asked, which is half the ticket's acceptance test.
//   * ITEM SELECTED    -> Worn | This columns, the verdict in plain words, the action,
//     and a line naming what the action REPLACES.
//
// ⛔ THE DELTA COLUMN IS ABSENT ON PURPOSE, AND THAT IS THE HONEST CHOICE (D8).
// The player's real question at a weapon tile is "is this better than what I have?",
// and answering it needs the EQUIPPED item's stats alongside the candidate's.
// InventoryVM already resolves equippedId internally, but InventoryDetail does not
// expose a comparison — and adding one is a MODEL change, which this ticket's OUT OF
// SCOPE rule sends to a separate CLI ticket. So the pane renders the candidate's
// stats and states plainly that there is nothing to compare against yet. It NEVER
// fabricates a delta. A made-up "+3" on the one screen whose job is trust would be
// worse than an honest blank, and it would be invisible to every gate we own.
//
// Every sentence here comes from canon-strings.json via InventoryStrings. There are
// no player-facing literals in this file.
// =============================================================================

using UnityEngine;
using DeNelle.Core.UI;
using DeNelle.Core.Diagnostics;
using DeNelle.Village.Hero;

namespace DeNelle.Village
{
    public sealed partial class HeroInventoryController : MonoBehaviour
    {
        /// <summary>Repaint the pane from vm.Selected ONLY. Called after the stage on every Render.</summary>
        private void RebuildPane()
        {
            if (_paneRoot == null) return;
            for (int i = _paneRoot.transform.childCount - 1; i >= 0; i--)
            {
                var child = _paneRoot.transform.GetChild(i);
                child.SetParent(null, false);
                Destroy(child.gameObject);
            }
            if (_vm == null) return;

            // The pane sits in its own carved well so it reads as a separate surface from the
            // stage, not as more grid.
            var well = ElarionUiKit.Well(_paneRoot.transform, Vector2.zero, Vector2.one);
            well.name = "PaneWell";

            var sel = _vm.SelectedId != null ? _vm.Selected : null;
            if (sel == null) { BuildPaneNoSelection(well.transform); return; }
            BuildPaneSelection(well.transform, sel.Value);
        }

        // ── NOTHING SELECTED ─────────────────────────────────────────────────
        // What is worn, and the cheapest way to improve it. This is the state the player is in
        // when the screen OPENS, so it is the state that has to earn the tap.
        private void BuildPaneNoSelection(Transform host)
        {
            if (_railIndex == RailGear)
            {
                BuildGearGuidance(host);
                return;
            }

            var title = AddLabel(host, InventoryStrings.Get(InventoryStrings.KeyPaneNoSelection),
                     0.90f, 0.98f, InkMicro, ElarionUi.FontMicro,
                     TMPro.TextAlignmentOptions.MidlineLeft, 0.06f, 0.94f, spacing: 2f);
            title.raycastTarget = false;
            ElarionUiKit.FitSingleLine(title, 0f, ElarionUi.FontMicro);

            // The worn set, as words. Read off the live loadout the same way the Gear section
            // does — one resolve, two placements, so the two can never disagree.
            var rows = new[]
            {
                new WornSlot(InventoryStrings.KeySlotMainHand, WornName(_loadout != null ? _loadout.EquippedWeapon  : null)),
                new WornSlot(InventoryStrings.KeySlotOffHand,  WornName(_loadout != null ? _loadout.EquippedOffHand : null)),
                new WornSlot(InventoryStrings.KeySlotArmor,    WornArmorName(_loadout != null ? _loadout.EquippedArmor : null)),
                new WornSlot(InventoryStrings.KeySlotAmulet,   WornAccessoryName(_loadout != null ? _loadout.EquippedAmulet : null)),
                new WornSlot(InventoryStrings.KeySlotRing,     WornAccessoryName(_loadout != null ? _loadout.EquippedRing   : null)),
            };

            const float top = 0.86f, rowH = 0.075f, gap = 0.012f;
            int vacant = 0;
            for (int i = 0; i < rows.Length; i++)
            {
                float y1 = top - i * (rowH + gap);
                bool empty = string.IsNullOrEmpty(rows[i].ItemName);
                if (empty) vacant++;

                var key = AddLabel(host, InventoryStrings.Get(rows[i].LabelKey).ToUpperInvariant(),
                         y1 - rowH, y1, InkMicro, ElarionUi.FontMicro,
                         TMPro.TextAlignmentOptions.MidlineLeft, 0.06f, 0.46f, spacing: 1f);
                key.raycastTarget = false;
                ElarionUiKit.FitSingleLine(key, 0f, ElarionUi.FontMicro);

                var val = AddLabel(host,
                         empty ? InventoryStrings.Get(InventoryStrings.KeySlotEmpty)
                               : ElarionUiKit.SpacedDisplayName(rows[i].ItemName),
                         y1 - rowH, y1, empty ? InkDim : Ink, ElarionUi.FontMicro,
                         TMPro.TextAlignmentOptions.MidlineRight, 0.48f, 0.94f, bold: !empty);
                val.raycastTarget = false;
                if (empty) val.fontStyle |= TMPro.FontStyles.Italic;
                ElarionUiKit.FitSingleLine(val, 0f, ElarionUi.FontMicro);
            }

            // The highest-value gap. ⚠ D9 flags this sentence as written for the two-empty-slot
            // case and leaves the dynamic form as an implementing call. It is shown ONLY when the
            // count it describes is the count that is true, because a screen that says "two slots
            // are empty" over five empty slots is a screen the player stops believing. When the
            // count differs, the honest generic hint stands in its place.
            string gapLine = vacant == 2
                ? InventoryStrings.Get(InventoryStrings.KeyPaneGearGaps)
                : InventoryStrings.Get(InventoryStrings.KeyNextCountHint);

            var gapLbl = AddLabel(host, gapLine, 0.06f, 0.34f, InkDim, ElarionUi.FontMicro,
                     TMPro.TextAlignmentOptions.TopLeft, 0.06f, 0.94f);
            gapLbl.raycastTarget = false;

            FlowTrace.Step("Inventory",
                $"Pane (no selection): {rows.Length - vacant}/{rows.Length} slots worn, {vacant} vacant; " +
                $"gap line={(vacant == 2 ? "authored two-slot sentence" : "generic hint (count is not two)")}.");
        }

        /// <summary>
        /// Gear already owns the authoritative five-row loadout at left. Repeating those names in
        /// this pane made the phone screenshot read like two competing sources of truth. The pane
        /// instead explains the useful next action and names only vacancies, which are information
        /// the player can act on. The Off Hand row remains visible at left even when its world prop
        /// is missing; presentation must never conceal that separate renderer defect.
        /// </summary>
        private void BuildGearGuidance(Transform host)
        {
            var title = AddLabel(host, InventoryStrings.Get(InventoryStrings.KeyGearPaneTitle),
                0.84f, 0.96f, GiltInk, ElarionUi.FontBody,
                TMPro.TextAlignmentOptions.MidlineLeft, 0.08f, 0.92f, bold: true);
            title.raycastTarget = false;
            ElarionUiKit.FitSingleLine(title, 0f, ElarionUi.FontBody);

            var guide = AddLabel(host, InventoryStrings.Get(InventoryStrings.KeyGearPaneGuide),
                0.56f, 0.78f, Ink, ElarionUi.FontMicro,
                TMPro.TextAlignmentOptions.TopLeft, 0.08f, 0.92f);
            guide.raycastTarget = false;

            var vacant = new System.Collections.Generic.List<string>();
            if (_loadout == null || _loadout.EquippedWeapon == null) vacant.Add(InventoryStrings.Get(InventoryStrings.KeySlotMainHand));
            if (_loadout == null || _loadout.EquippedOffHand == null) vacant.Add(InventoryStrings.Get(InventoryStrings.KeySlotOffHand));
            if (_loadout == null || _loadout.EquippedArmor == null) vacant.Add(InventoryStrings.Get(InventoryStrings.KeySlotArmor));
            if (_loadout == null || _loadout.EquippedAmulet == null) vacant.Add(InventoryStrings.Get(InventoryStrings.KeySlotAmulet));
            if (_loadout == null || _loadout.EquippedRing == null) vacant.Add(InventoryStrings.Get(InventoryStrings.KeySlotRing));

            string gapTitle = vacant.Count == 0
                ? InventoryStrings.Get(InventoryStrings.KeyGearPaneComplete)
                : InventoryStrings.Format(InventoryStrings.KeyGearPaneOpenSlots, vacant.Count);
            var gaps = AddLabel(host, gapTitle, 0.35f, 0.49f, InkMicro, ElarionUi.FontMicro,
                TMPro.TextAlignmentOptions.MidlineLeft, 0.08f, 0.92f, spacing: 2f, bold: true);
            gaps.raycastTarget = false;

            // The count is actionable; repeating five slot names already visible in the stage
            // wastes the narrow pane and previously overflowed into the wallet strip.

            FlowTrace.Step("Inventory", $"Gear guidance pane: no duplicated loadout rows; vacancies={vacant.Count}; worn rows route to item tabs.");
        }

        // ── ITEM SELECTED ────────────────────────────────────────────────────
        private void BuildPaneSelection(Transform host, InventoryDetail d)
        {
            // Name. A raw itemId ("BoneFragment" / "bone_fragment") must never reach the player.
            string displayName = ElarionUiKit.SpacedDisplayName(d.Name ?? "");
            bool isConsumable = d.CanUse && !d.CanEquip;
            if (isConsumable)
            {
                BuildConsumableSelection(host, d, displayName);
                return;
            }
            var nmLbl = AddLabel(host, displayName, 0.88f, 0.98f, GiltInk, ElarionUi.FontBody,
                     TMPro.TextAlignmentOptions.MidlineLeft, 0.06f, 0.94f, bold: true);
            nmLbl.raycastTarget = false;
            ElarionUiKit.FitSingleLine(nmLbl, 0f, ElarionUi.FontBody);

            bool isWorn = SelectedIsEquipped();
            bool comparisonContext = CanOfferComparisonGuidance(isWorn);

            var descLbl = AddLabel(host, d.Description ?? "", 0.78f, 0.87f, InkDim,
                ElarionUi.FontMicro, TMPro.TextAlignmentOptions.TopLeft, 0.06f, 0.94f);
            descLbl.raycastTarget = false;
            if (isWorn)
            {
                // The WORD carries the state (D5), plus a plate — never a green tint.
                var badge = AddImage(host, "WornBadge", new Vector2(0.06f, 0.80f), new Vector2(0.36f, 0.87f),
                                     new Color(ElarionUi.Gold.r * 0.5f, ElarionUi.Gold.g * 0.42f, 0.06f, 0.92f));
                NoRaycast(badge);
                AddInnerRim(badge, new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.85f));
                var bl = AddLabel(badge.transform, InventoryStrings.Get(InventoryStrings.KeyPaneWornBadge),
                         0f, 1f, Ink, ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Center, 0f, 1f, bold: true);
                bl.raycastTarget = false;
                ElarionUiKit.FitSingleLine(bl, 0f, ElarionUi.FontMicro);
            }

            if (d.StackCount > 1)
            {
                var cntLbl = AddLabel(host, "x" + d.StackCount, 0.88f, 0.98f, InkDim,
                         ElarionUi.FontLabel, TMPro.TextAlignmentOptions.MidlineRight, 0.60f, 0.94f, bold: true);
                cntLbl.raycastTarget = false;
                ElarionUiKit.FitSingleLine(cntLbl, 0f, ElarionUi.FontLabel);
            }

            // ── The compare block. Both column headers are drawn, because the SHAPE of the
            //    answer is part of the answer: the player can see there is a "Worn" column and
            //    that we have nothing to put in it, which is very different from us not asking.
            var hWorn = AddLabel(host, InventoryStrings.Get(InventoryStrings.KeyPaneColumnWorn),
                     0.70f, 0.77f, InkMicro, ElarionUi.FontMicro,
                     TMPro.TextAlignmentOptions.MidlineLeft, 0.06f, 0.48f, spacing: 2f);
            hWorn.raycastTarget = false;
            ElarionUiKit.FitSingleLine(hWorn, 0f, ElarionUi.FontMicro);
            hWorn.gameObject.SetActive(comparisonContext);

            var hThis = AddLabel(host, InventoryStrings.Get(InventoryStrings.KeyPaneColumnThis),
                     0.70f, 0.77f, InkMicro, ElarionUi.FontMicro,
                     TMPro.TextAlignmentOptions.MidlineRight, 0.52f, 0.94f, spacing: 2f);
            hThis.raycastTarget = false;
            ElarionUiKit.FitSingleLine(hThis, 0f, ElarionUi.FontMicro);
            hThis.gameObject.SetActive(comparisonContext);

            ElarionUiKit.Rule(host, 0.685f, 0.06f, 0.94f);

            // "This" — the candidate's own stats, which the model DOES expose.
            var stLbl = AddLabel(host, d.Stats ?? "", isConsumable ? 0.60f : 0.64f, isConsumable ? 0.76f : 0.685f, Ink, ElarionUi.FontMicro,
                     comparisonContext ? TMPro.TextAlignmentOptions.TopRight : TMPro.TextAlignmentOptions.TopLeft,
                     comparisonContext ? 0.52f : 0.06f, 0.94f);
            stLbl.raycastTarget = false;

            // "Worn" — stated as absent, never faked. See the file header.
            var wornLbl = AddLabel(host, InventoryStrings.Get(InventoryStrings.KeyPaneNothingToCompare),
                     0.64f, 0.685f, InkDim, ElarionUi.FontMicro,
                     TMPro.TextAlignmentOptions.TopLeft, 0.06f, 0.48f);
            wornLbl.raycastTarget = false;
            wornLbl.gameObject.SetActive(comparisonContext);

            // The one-line plain-words verdict. Only the claims we can actually support are made:
            // "this is what you are carrying now" is knowable from the equipped flag; "better" and
            // "worse" are NOT knowable without the worn item's stats, so they are not asserted.
            string verdict = isWorn
                ? InventoryStrings.Get(InventoryStrings.KeyVerdictWearing)
                : (comparisonContext ? InventoryStrings.Get(InventoryStrings.KeyNextCompareHint) : "");
            var vLbl = AddLabel(host, verdict, 0.53f, 0.62f, InkDim, ElarionUi.FontMicro,
                     TMPro.TextAlignmentOptions.TopLeft, 0.06f, 0.94f);
            vLbl.raycastTarget = false;

            // ── The action, under the right thumb (D6). A crafting MATERIAL is neither usable nor
            //    equippable; it used to get a live "Use" button that could only ever fail, so the
            //    pane now states what the row IS instead of offering a verb it does not have.
            if (!d.CanUse && !d.CanEquip)
            {
                var noteLbl = AddLabel(host, ElarionUiKit.SpacedDisplayName(d.Rarity ?? ""), 0.06f, 0.40f,
                         InkDim, ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Center, 0.06f, 0.94f);
                noteLbl.raycastTarget = false;
                return;
            }

            string ctaKey = isWorn ? InventoryStrings.KeyActionWorn
                          : (isConsumable ? InventoryStrings.KeyActionUse : InventoryStrings.KeyActionEquip);
            if (isWorn)
            {
                // Already equipped is a status, not a disabled CTA. Device testing showed the
                // button-shaped Worn control looked tappable and made its correct no-op feel broken.
                var state = AddLabel(host, InventoryStrings.Get(InventoryStrings.KeyActionWorn),
                    0.06f, 0.44f, GiltInk, ElarionUi.FontLabel,
                    TMPro.TextAlignmentOptions.Center, 0.06f, 0.94f, bold: true);
                state.raycastTarget = false;
            }
            else
            {
                var cta = ElarionUiKit.ButtonPack(host, InventoryStrings.Get(ctaKey),
                    ElarionUiKit.ButtonKind.Gold,
                    new Vector2(0.06f, 0.04f), new Vector2(0.94f, 0.44f),
                    BuildEquipAction(isConsumable));
                if (cta != null) ElarionUiKit.ClampMinTouch(cta);
            }

            // The line naming what the action REPLACES (D3). Only drawn when we actually know the
            // worn item in that slot — an unknown replacement is left unsaid, not guessed at.
            string replaced = ReplacedBySelection(d);
            if (!string.IsNullOrEmpty(replaced))
            {
                var rep = AddLabel(host,
                         InventoryStrings.Format(InventoryStrings.KeyNextReplaces,
                                                 ElarionUiKit.SpacedDisplayName(replaced)),
                         0.45f, 0.52f, InkDim, ElarionUi.FontMicro,
                         TMPro.TextAlignmentOptions.Center, 0.06f, 0.94f);
                rep.raycastTarget = false;
                ElarionUiKit.FitSingleLine(rep, 0f, ElarionUi.FontMicro);
            }
        }

        private void BuildConsumableSelection(Transform host, InventoryDetail d, string displayName)
        {
            string heading = displayName + (d.StackCount > 1 ? "   x" + d.StackCount : "");
            var title = AddLabel(host, heading, 0.82f, 0.96f, GiltInk, ElarionUi.FontBody,
                TMPro.TextAlignmentOptions.MidlineLeft, 0.06f, 0.94f, bold: true);
            title.raycastTarget = false;
            ElarionUiKit.FitSingleLine(title, 20f, ElarionUi.FontBody);

            var description = AddLabel(host, d.Description ?? "", 0.72f, 0.81f, InkDim,
                ElarionUi.FontMicro, TMPro.TextAlignmentOptions.TopLeft, 0.06f, 0.94f);
            description.raycastTarget = false;

            ElarionUiKit.Rule(host, 0.69f, 0.06f, 0.94f);
            var effect = AddLabel(host, d.Stats ?? "", 0.48f, 0.67f, Ink,
                ElarionUi.FontMicro, TMPro.TextAlignmentOptions.TopLeft, 0.06f, 0.94f);
            effect.raycastTarget = false;

            var cta = ElarionUiKit.ButtonPack(host,
                InventoryStrings.Get(InventoryStrings.KeyActionUse), ElarionUiKit.ButtonKind.Gold,
                new Vector2(0.06f, 0.04f), new Vector2(0.94f, 0.44f),
                BuildEquipAction(isConsumable: true));
            if (cta != null) ElarionUiKit.ClampMinTouch(cta);
        }

        private bool CanOfferComparisonGuidance(bool isWorn)
        {
            if (isWorn || _vm == null || _vm.Slots == null) return false;
            if (_railIndex != RailWeapons && _railIndex != RailOffHand && _railIndex != RailArmor) return false;
            return _vm.Slots.Count > 1;
        }

        /// <summary>
        /// Is the selected id the one currently worn? Read off the VM's OWN slot projection
        /// (ItemVM.Equipped), not from a second lookup — the grid draws its WORN badge from the
        /// same flag, so the two can never disagree about the same item.
        /// </summary>
        private bool SelectedIsEquipped()
        {
            if (_vm == null || _vm.Slots == null || _vm.SelectedId == null) return false;
            foreach (var s in _vm.Slots)
                if (string.Equals(s.Id, _vm.SelectedId, System.StringComparison.OrdinalIgnoreCase))
                    return s.Equipped;
            return false;
        }

        /// <summary>
        /// What equipping the selection would displace, when that is knowable: the worn item in
        /// the matching slot. Returns null for a consumable (nothing is replaced), for an already
        /// worn item, and for any slot the loadout does not expose — the caller then says nothing
        /// rather than naming a guess.
        /// </summary>
        private string ReplacedBySelection(InventoryDetail d)
        {
            if (_loadout == null || !d.CanEquip || SelectedIsEquipped()) return null;
            switch (d.IconRole)
            {
                case InventoryVM.IconRoleWeapon:
                    return _railIndex == RailOffHand
                        ? WornName(_loadout.EquippedOffHand)
                        : WornName(_loadout.EquippedWeapon);
                case InventoryVM.IconRoleArmor:  return WornArmorName(_loadout.EquippedArmor);
            }
            return null;
        }

        // The CTA handler: instrument (§12) the explicit equip/use + the resulting vm.Status, then
        // route to the VM command. The confirmation ("Used X." / "Equipped X.") surfaces as the ONE
        // transient kit toast — auto-fading, never a parked label that can go stale; any raw itemId
        // inside the VM's status line is spaced before it reaches the player.
        private System.Action BuildEquipAction(bool isConsumable)
        {
            return () =>
            {
                if (_vm == null) return;
                string rawName = _vm.Selected.HasValue ? _vm.Selected.Value.Name : null;
                FlowTrace.Step("Inventory",
                    $"Equip CTA id={_vm.SelectedId} tab={_vm.ActiveTab} consumable={isConsumable} (ACTION)");
                if (isConsumable) _vm.Use();
                else _vm.Equip();
                FlowTrace.Step("Inventory",
                    $"Equip CTA post-action Status='{_vm.Status}'");
                string msg = _vm.Status;
                if (!string.IsNullOrEmpty(msg))
                {
                    if (!string.IsNullOrEmpty(rawName))
                        msg = msg.Replace(rawName, ElarionUiKit.SpacedDisplayName(rawName));
                    ElarionUiKit.ShowToast(msg, ElarionUiKit.ToastTone.Info);
                }
            };
        }
    }
}
