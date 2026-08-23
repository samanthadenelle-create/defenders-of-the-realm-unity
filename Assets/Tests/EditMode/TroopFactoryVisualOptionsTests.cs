using NUnit.Framework;
using DeNelle.Village;

namespace DeNelle.Tests.EditMode
{
    [TestFixture]
    public sealed class TroopFactoryVisualOptionsTests
    {
        [Test]
        public void SiegeBody_FitsAuthoredHeight_NotLargestDimension()
        {
            const float bodyHeight = 2.4f;

            SkinOptions options = TroopFactory.VisualOptionsFor(true, bodyHeight);

            Assert.That(options.FitHeight, Is.EqualTo(bodyHeight));
            Assert.That(options.FitLargest, Is.Zero);
            Assert.That(options.SeatOnGround, Is.True);
            Assert.That(options.FixTripoMaterials, Is.True);
        }

        [Test]
        public void HumanoidBody_KeepsEnemyFitContract()
        {
            const float bodyHeight = 1.8f;

            SkinOptions options = TroopFactory.VisualOptionsFor(false, bodyHeight);

            Assert.That(options.FitHeight, Is.EqualTo(bodyHeight));
            Assert.That(options.FitLargest, Is.Zero);
        }
    }
}
