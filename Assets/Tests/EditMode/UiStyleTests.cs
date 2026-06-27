// EditMode permission-gate tests for DeNelle.Core.UI.UiStyle (Obsidian spec §6).
// These assert the single-style authority's contract so the later panel migration
// (phases b/c) is provably behaviour-preserving (ARCHITECTURE_PRINCIPLES.md §7).
// All sprite lookups are null-safe by contract, so tests assert behaviour + tints,
// not the presence of pack art (which may be absent in an EditMode run).

using NUnit.Framework;
using UnityEngine;
using DeNelle.Core.UI;

namespace DeNelle.Tests.EditMode
{
    public class UiStyleTests
    {
        [SetUp]
        public void Reset() => UiStyle.Try(Style.Default);

        [TearDown]
        public void Restore() => UiStyle.Try(Style.Default);

        [Test]
        public void Default_active_style_is_Default()
        {
            Assert.AreEqual(Style.Default, UiStyle.Active);
            Assert.IsNotNull(UiStyle.Theme);
        }

        [Test]
        public void Try_swaps_active_style_and_theme()
        {
            UiStyle.Try(Style.Obsidian);
            Assert.AreEqual(Style.Obsidian, UiStyle.Active);
            Assert.AreEqual("slot_talent", UiStyle.Theme.SlotTalent);
        }

        [Test]
        public void Try_raises_Changed_event()
        {
            int fired = 0;
            System.Action handler = () => fired++;
            UiStyle.Changed += handler;
            try { UiStyle.Try(Style.Obsidian); }
            finally { UiStyle.Changed -= handler; }
            Assert.AreEqual(1, fired);
        }

        [Test]
        public void Chrome_reads_FeatureFlags_in_one_place()
        {
            Assert.AreEqual(DeNelle.Core.FeatureFlags.BlinkChrome, UiStyle.Chrome);
        }

        [Test]
        public void StatePlate_unlockable_is_full_alpha_gold_hero_state()
        {
            var plate = UiStyle.StatePlate(SlotState.Unlockable);
            Assert.AreEqual(1f, plate.Tint.a, 0.001f);
            // gold base (ElarionUi.Gold) — red channel notably higher than blue
            Assert.Greater(plate.Tint.r, plate.Tint.b);
        }

        [Test]
        public void StatePlate_locked_is_dim()
        {
            var plate = UiStyle.StatePlate(SlotState.Locked);
            Assert.AreEqual(0.40f, plate.Tint.a, 0.001f);
        }

        [Test]
        public void StatePlate_owned_uses_low_alpha_affordable()
        {
            var plate = UiStyle.StatePlate(SlotState.Owned);
            Assert.AreEqual(0.22f, plate.Tint.a, 0.001f);
            // affordable green — green channel dominant
            Assert.Greater(plate.Tint.g, plate.Tint.r);
        }

        [Test]
        public void PanelFill_is_transparent_when_chrome_on_else_solid()
        {
            var fill = UiStyle.Color.PanelFill(chromeAware: true);
            if (UiStyle.Chrome) Assert.AreEqual(0f, fill.a, 0.001f);
            else Assert.Greater(fill.a, 0f);

            // chromeAware:false always keeps the solid fill regardless of the flag
            Assert.Greater(UiStyle.Color.PanelFill(chromeAware: false).a, 0f);
        }

        [Test]
        public void Color_and_font_tokens_are_deterministic_and_nonzero()
        {
            Assert.Greater(UiStyle.Font.Title, UiStyle.Font.Body);
            Assert.Greater(UiStyle.Font.Body, UiStyle.Font.Micro);
            Assert.Greater(UiStyle.CellSize.x, 0f);
            Assert.Greater(UiStyle.TapTarget, 0f);
        }

        [Test]
        public void Sprite_accessors_are_null_safe_and_do_not_throw()
        {
            // Pack art may be absent in EditMode; the contract is null, never a throw.
            Assert.DoesNotThrow(() =>
            {
                var _ = UiStyle.Frame.Window;
                var __ = UiStyle.Frame.Of(FrameKind.Vendor);
                var ___ = UiStyle.Slot(SlotState.Filled);
                var ____ = UiStyle.Slot(SlotState.Locked, UiStyle.Theme.SlotTalent);
                var _____ = UiStyle.Button(ButtonRole.Primary);
                var ______ = UiStyle.Button(ButtonRole.Close);
                var _______ = UiStyle.Icon("inventory", "bag");
            });
        }
    }
}
