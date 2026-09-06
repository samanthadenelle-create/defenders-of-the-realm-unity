// =============================================================================
// HonestFeedbackKeys (WO-1432) - the ONE authority for the honest-feedback save
// keys. Strings only, no behaviour.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.State
//
// WHY THIS LIVES IN CORE AND NOT BESIDE THE SERVICE. Three seats need these
// strings and they are in different assemblies:
//   - DeNelle.Village.Feedback.HonestFeedbackGrant    READS + WRITES them
//   - DeNelle.Village.Feedback.HonestFeedbackService  READS them (the offer gate)
//   - DeNelle.EditorRegression                        READS them (both oracles)
// Village references Core; Core cannot reference Village. Writing the same key
// literal in more than one place is exactly the duplicated-state failure
// CLAUDE.md sections 2 and 5 keep paying for - the copy goes stale and the flag
// quietly changes meaning between the writer and the reader. So the strings live
// once, HERE, at the lowest altitude that every seat can reach. This is the
// RecipeUnlockKeys shape (WO-1235), deliberately.
//
// -----------------------------------------------------------------------------
// NO SAVE-SCHEMA BUMP, AND THE REASON IS NOT "IT WAS EASIER"
// -----------------------------------------------------------------------------
// These keys ride the EXISTING GameState.SeenTutorials map
// (SerializableDict<string,bool>, GameState.cs:142), so the persisted SHAPE is
// unchanged and SaveSchema.CurrentVersion (read it at source - it is the
// authority, and CLAUDE.md section 8 is stale about it) does not move.
//
// The v40 precedent (WO-1235, recorded in the CurrentVersion changelog) bumped
// for ONE reason: its migrator was the only reliable way to tell an EXISTING
// player from a NEW one, because a recipe that everybody could already craft may
// never be taken away. NOTHING HERE HAS THAT PROBLEM. An absent key reads as
// false, and false is the correct answer for every player alive:
//   - a brand-new player has not been offered the thank-you yet -> offer it;
//   - an existing player has not been offered the thank-you yet -> offer it.
// There is no state a migrator would have to derive, so there is nothing for a
// bump to buy. See FOUNDATIONAL_RULINGS.md section 5: a bump needs all four
// conditions together, and this change meets none of them.
//
// ASCII only - the tofu oracle fails a non-ASCII player-facing string, and these
// are save keys besides.
// =============================================================================

namespace DeNelle.Core.State
{
    /// <summary>
    /// Save-key vocabulary for the WO-1432 honest-feedback thank-you. Keys only.
    /// </summary>
    public static class HonestFeedbackKeys
    {
        /// <summary>
        /// SeenTutorials key namespace, so a feedback record can never collide with a
        /// tutorial key or with a RecipeUnlockKeys entry. Every key in this class MUST
        /// begin with it.
        /// </summary>
        public const string KeyPrefix = "honest_feedback:";

        /// <summary>
        /// ⭐ THE ONE-TIME FLAG. True once the thank-you grant has actually landed in the
        /// wallet - written by HonestFeedbackGrant.TryApply AFTER the economy seam returns,
        /// never before. It is the single guard that makes a second claim impossible, and
        /// WO-1432 section 3 forbids a second grant path that could route around it.
        /// <para>Deliberately distinct from <see cref="OfferedKey"/>: a player who opened
        /// the panel and closed it has been OFFERED but not PAID, and conflating the two
        /// would either pay twice or silently withdraw an unclaimed offer.</para>
        /// </summary>
        public const string GrantClaimedKey = KeyPrefix + "thank_you_granted";

        /// <summary>
        /// True once the offer panel has been SHOWN. Stops the one-time panel re-appearing
        /// every session at the player who read it and decided not to write anything - that
        /// player has answered, and asking again is nagging.
        /// <para>Recorded separately from <see cref="GrantClaimedKey"/> so the two questions
        /// ("did we ask?" / "did we pay?") never have to be inferred from one bit.</para>
        /// </summary>
        public const string OfferedKey = KeyPrefix + "offer_shown";
    }
}
