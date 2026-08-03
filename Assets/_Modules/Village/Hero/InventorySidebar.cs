// =============================================================================
// InventorySidebar — selection DETAIL strip (restored in WO-585).
// -----------------------------------------------------------------------------
// The grid is "browse + quick-equip"; the RICH tap-to-change drawer belongs to
// Gear Preview / EquipmentPanel (WO-582). This partial holds only the minimal
// FELT-RESPONSE the inventory was missing: when an item is tapped (select-only,
// see InventoryGrid.onTap), this drops the selected item's name + stats + an
// explicit Equip/Use CTA + the equip Status line into the thin _sidebarRoot strip
// (built in InventoryUIBuilder.BuildRoot). Tap = select+detail; the CTA = equip,
// so re-tapping an already-equipped item is no longer a silent no-op, and every
// equip shows a visible confirmation (vm.Status). Renders PURELY from vm.* (no
// state pulls) — exactly like the grid. Reuses the shared AddImage/AddLabel/
// AddButton helpers; invents no new UI system.
// =============================================================================

using UnityEngine;
using DeNelle.Core.UI;
using DeNelle.Core.Diagnostics;
using DeNelle.Village.Hero;

namespace DeNelle.Village
{
    public sealed partial class HeroInventoryController : MonoBehaviour
    {
        // Repaint the detail strip from vm.Selected / vm.Status ONLY. Called from Render()
        // after the grid so the strip always reflects the current selection + last action.
        private void RebuildSidebar()
        {
            if (_sidebarRoot == null) return;
            for (int i = _sidebarRoot.transform.childCount - 1; i >= 0; i--)
                Destroy(_sidebarRoot.transform.GetChild(i).gameObject);
            if (_vm == null) return;

            var sel = _vm.SelectedId != null ? _vm.Selected : null;

            // A faint obsidian backing so the strip reads as one inlaid detail bar.
            var bar = AddImage(_sidebarRoot.transform, "DetailBar",
                               new Vector2(0f, 0f), new Vector2(1f, 1f), GlassDeep);
            AddInnerRim(bar, AccentSoft);
            NoRaycast(bar);

            // NOTHING selected → a quiet hint so the strip never reads as broken.
            // WO-713 A.5: action feedback ("Used X.") is a TRANSIENT kit toast now (fired at
            // the CTA site, WO-714 P5 ShowToast) — the strip never parks status text that
            // can go stale.
            if (sel == null)
            {
                // Eyes-sweep 2026-07-06: every strip label fits-or-ellipsizes inside its band
                // (§1.14 NoWrap+ellipsis) so the thin strip's copy never paints outside it.
                var hint = AddLabel(bar.transform, "Tap an item to inspect it.", 0f, 1f, InkDim,
                         ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Center, 0.03f, 0.97f);
                ElarionUiKit.FitSingleLine(hint, 0f, ElarionUi.FontLabel);
                return;
            }

            var d = sel.Value;

            // LEFT: item name (top, SPACED — WO-714 P10: a raw itemId like "BoneFragment"/
            // "bone_fragment" must never reach the player) + count + one-line effect (bottom).
            // RIGHT: the explicit Equip/Use CTA.
            string displayName = ElarionUiKit.SpacedDisplayName(d.Name ?? "");
            var nmLbl = AddLabel(bar.transform, displayName, 0.50f, 1f, GiltInk,
                     ElarionUi.FontBody, TMPro.TextAlignmentOptions.MidlineLeft, 0.03f, 0.62f, bold: true);
            ElarionUiKit.FitSingleLine(nmLbl, 0f, ElarionUi.FontBody);
            // Owned/stack count (WO-683 detail grammar) — text chip right of the name band.
            if (d.StackCount > 1)
            {
                var cntLbl = AddLabel(bar.transform, "x" + d.StackCount, 0.50f, 1f, InkDim,
                         ElarionUi.FontLabel, TMPro.TextAlignmentOptions.MidlineRight, 0.62f, 0.74f, bold: true);
                ElarionUiKit.FitSingleLine(cntLbl, 0f, ElarionUi.FontLabel);
            }
            var stLbl = AddLabel(bar.transform, d.Stats ?? "", 0f, 0.50f, InkDim,
                     ElarionUi.FontMicro, TMPro.TextAlignmentOptions.MidlineLeft, 0.03f, 0.74f);
            ElarionUiKit.FitSingleLine(stLbl, 0f, ElarionUi.FontMicro);

            // The CTA: Equip for gear, Use for a consumable. The action lives HERE (not on the
            // grid tap) — so it always fires on an explicit press and always surfaces vm.Status.
            // F8-641: a crafting MATERIAL is neither usable nor equippable. It used to be typed
            // as a consumable and got a live "Use" button that could only ever fail; now the
            // strip simply states what the row is instead of offering a verb it does not have.
            bool isConsumable = d.CanUse && !d.CanEquip;
            if (!d.CanUse && !d.CanEquip)
            {
                var noteLbl = AddLabel(bar.transform, "Crafting material", 0f, 1f, InkDim,
                         ElarionUi.FontMicro, TMPro.TextAlignmentOptions.MidlineRight, 0.76f, 0.97f);
                ElarionUiKit.FitSingleLine(noteLbl, 0f, ElarionUi.FontMicro);
                return;
            }
            string ctaLabel = isConsumable ? "Use" : "Equip";
            bool ctaEnabled = isConsumable ? d.CanUse : d.CanEquip;
            var cta = AddButton(bar.transform, ctaLabel,
                                new Vector2(0.87f, 0.11f), new Vector2(0.18f, 0.82f),
                                ctaEnabled ? ElarionUi.GoldButton : Cell,
                                ctaEnabled ? BuildEquipAction(isConsumable) : null,
                                ctaEnabled ? ButtonKind.Gold : ButtonKind.Neutral);
            if (cta != null) DressButtonPack(cta);
        }

        // The CTA handler: instrument (§12) the explicit equip/use + the resulting vm.Status,
        // then route to the VM command. WO-713 A.5: the confirmation ("Used X." / "Equipped
        // X.") surfaces as the ONE transient kit toast (WO-714 P5) — auto-fading, never a
        // parked strip label; any raw itemId inside the VM's status line is spaced before it
        // reaches the player (WO-714 P10 id-leak law).
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
