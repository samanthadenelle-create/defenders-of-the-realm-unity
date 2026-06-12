using NUnit.Framework;
using UnityEngine;
using DeNelle.Village.World.Camps;

namespace DeNelle.Tests.EditMode
{
    /// <summary>
    /// Regression guard for the castle's 4 gate exits (S/W/N/E): RaidOutpostSystem must
    /// materialise FOUR cardinal enemy outposts — one raid target out of every gate — not
    /// the original single east outpost. If the anchor list ever drops back to 1 (or moves
    /// off the cardinals), these go RED before the owner loses 3 of the 4 raid targets.
    /// Pure data assertion — no scene load, no spawn.
    /// </summary>
    public class RaidOutpostCardinalsTest
    {
        private const float A = 70f;   // cardinal anchor distance from origin

        [Test]
        public void HasFourCardinalOutposts()
        {
            Assert.AreEqual(4, RaidOutpostSystem.CardinalOutpostCount,
                "RaidOutpostSystem must drive 4 cardinal outposts (one per castle gate exit) — " +
                "a drop to 1 means 3 gates have no raid target.");
            Assert.AreEqual(4, RaidOutpostSystem.OutpostAnchorPositions().Length,
                "Anchor list length must match the cardinal count.");
        }

        [Test]
        public void AnchorsSitAtTheFourCardinals()
        {
            var anchors = RaidOutpostSystem.OutpostAnchorPositions();

            Assert.Contains(new Vector3( A, 0f,  0f), anchors, "East anchor (70,0,0) missing.");
            Assert.Contains(new Vector3(-A, 0f,  0f), anchors, "West anchor (-70,0,0) missing.");
            Assert.Contains(new Vector3( 0f, 0f,  A), anchors, "North anchor (0,0,70) missing.");
            Assert.Contains(new Vector3( 0f, 0f, -A), anchors, "South anchor (0,0,-70) missing.");
        }
    }
}
