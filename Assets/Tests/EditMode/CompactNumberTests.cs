// EditMode tests for ElarionUi.CompactNumber (WO-697, ticket RES-1) — the ONE
// compact currency formatter the CurrencyChip builder (and every cost/reward
// string) routes through. Locks the owner-ruled threshold table: < 10,000
// verbatim plain digits (no group separators); >= 10,000 one truncated decimal
// below 100 of the tier unit ("98.6k"), none at/above ("100k", "1.2m");
// trailing ".0" trimmed; ASCII only; sign preserved.

using NUnit.Framework;
using DeNelle.Core.UI;

namespace DeNelle.Tests.EditMode
{
    public class CompactNumberTests
    {
        [TestCase(0L, "0")]
        [TestCase(7L, "7")]
        [TestCase(999L, "999")]
        [TestCase(1000L, "1000")]          // verbatim below 10k — and NO "1,000" grouping
        [TestCase(9999L, "9999")]
        public void Below_ten_thousand_renders_verbatim_plain_digits(long v, string expected)
        {
            Assert.AreEqual(expected, ElarionUi.CompactNumber(v));
        }

        [TestCase(10000L, "10k")]          // trailing ".0" trimmed — never "10.0k"
        [TestCase(10500L, "10.5k")]
        [TestCase(98600L, "98.6k")]        // the spec's own example
        [TestCase(99999L, "99.9k")]        // truncation, never a "100.0k" round-up
        [TestCase(12345L, "12.3k")]
        public void Ten_k_to_hundred_k_gets_one_truncated_decimal(long v, string expected)
        {
            Assert.AreEqual(expected, ElarionUi.CompactNumber(v));
        }

        [TestCase(100000L, "100k")]        // the spec's own example — no decimal at/above 100k
        [TestCase(123456L, "123k")]
        [TestCase(999999L, "999k")]        // truncation keeps the k tier — never "1000k"
        public void Hundred_k_to_one_million_has_no_decimal(long v, string expected)
        {
            Assert.AreEqual(expected, ElarionUi.CompactNumber(v));
        }

        [TestCase(1000000L, "1m")]
        [TestCase(1234567L, "1.2m")]       // the spec's "1.2m class" + the 7-digit acceptance value
        [TestCase(99999999L, "99.9m")]
        [TestCase(100000000L, "100m")]
        [TestCase(1234567890L, "1.2b")]
        public void Million_and_billion_tiers_follow_the_same_grammar(long v, string expected)
        {
            Assert.AreEqual(expected, ElarionUi.CompactNumber(v));
        }

        [TestCase(-7L, "-7")]
        [TestCase(-98600L, "-98.6k")]
        public void Negative_values_keep_their_sign(long v, string expected)
        {
            Assert.AreEqual(expected, ElarionUi.CompactNumber(v));
        }

        [Test]
        public void Long_min_value_does_not_overflow()
        {
            // |long.MinValue| can't be negated in long — the formatter widens via ulong.
            // b is the top tier, so extreme magnitudes render as whole billions.
            Assert.AreEqual("-9223372036b", ElarionUi.CompactNumber(long.MinValue));
        }

        [Test]
        public void Int_overload_matches_long_overload()
        {
            Assert.AreEqual(ElarionUi.CompactNumber(98600L), ElarionUi.CompactNumber(98600));
        }

        [Test]
        public void Output_is_always_ascii()
        {
            long[] samples = { 0, 999, 9999, 10000, 98600, 100000, 1234567, -1234567, long.MaxValue };
            foreach (long v in samples)
                foreach (char c in ElarionUi.CompactNumber(v))
                    Assert.Less((int)c, 128, $"non-ASCII char in CompactNumber({v})");
        }
    }
}
