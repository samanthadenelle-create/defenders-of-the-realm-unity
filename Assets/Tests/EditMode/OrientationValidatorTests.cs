// =============================================================================
// OrientationValidatorTests (EditMode) — WO-363 character orientation gate.
// -----------------------------------------------------------------------------
// Guards the WO-363 rule via the pure DeNelle.Core.Validation.OrientationValidator:
// a character's facing must match its movement input (UP->face up, etc.), idle
// preserves last facing, ~30° tolerance for smooth rotation. Convention used here:
//   facing = a forward Vector3 (transform.forward style)
//   input  = new Vector3(inputX, 0, inputZ), where +Z = UP, +X = RIGHT.
// =============================================================================

using NUnit.Framework;
using UnityEngine;
using DeNelle.Core.Validation;

namespace DeNelle.Tests.EditMode
{
    [TestFixture]
    public class OrientationValidatorTests
    {
        // Direction conventions (XZ plane): UP = +Z, DOWN = -Z, RIGHT = +X, LEFT = -X.
        private static readonly Vector3 Up    = Vector3.forward;  // (0,0, 1)
        private static readonly Vector3 Down   = Vector3.back;    // (0,0,-1)
        private static readonly Vector3 Right  = Vector3.right;   // ( 1,0,0)
        private static readonly Vector3 Left   = Vector3.left;    // (-1,0,0)

        [Test]
        public void CharacterFacesUpWhenMovingUp()
        {
            // Facing up, input up -> aligned -> PASS.
            Assert.IsTrue(OrientationValidator.IsValid(Up, Up),
                "facing UP while moving UP must pass");
            // Facing down while moving up -> 180° mismatch -> FAIL.
            Assert.IsFalse(OrientationValidator.IsValid(Down, Up),
                "facing DOWN while moving UP must fail (180° mismatch)");
        }

        [Test]
        public void CharacterFacesDownWhenMovingDown()
        {
            Assert.IsTrue(OrientationValidator.IsValid(Down, Down),
                "facing DOWN while moving DOWN must pass");
            Assert.IsFalse(OrientationValidator.IsValid(Up, Down),
                "facing UP while moving DOWN must fail");
        }

        [Test]
        public void CharacterFacesLeftWhenMovingLeft()
        {
            Assert.IsTrue(OrientationValidator.IsValid(Left, Left),
                "facing LEFT while moving LEFT must pass");
            Assert.IsFalse(OrientationValidator.IsValid(Right, Left),
                "facing RIGHT while moving LEFT must fail");
        }

        [Test]
        public void CharacterFacesRightWhenMovingRight()
        {
            Assert.IsTrue(OrientationValidator.IsValid(Right, Right),
                "facing RIGHT while moving RIGHT must pass");
            Assert.IsFalse(OrientationValidator.IsValid(Left, Right),
                "facing LEFT while moving RIGHT must fail");
        }

        [Test]
        public void PreservesLastFacingWhenIdle()
        {
            // No input -> idle -> facing is NOT asserted, any facing passes.
            Assert.IsTrue(OrientationValidator.IsValid(Up, Vector3.zero),
                "idle (no input) must preserve last facing — never a failure");
            Assert.IsTrue(OrientationValidator.IsValid(Left, Vector3.zero),
                "idle with a sideways facing must still pass");
            Assert.IsFalse(OrientationValidator.HasInput(Vector3.zero),
                "zero input must register as no-input");
        }

        [Test]
        public void Diagonal45()
        {
            // Moving up-right; facing exactly up-right -> aligned -> PASS.
            Vector3 upRight = (Up + Right).normalized;
            Assert.IsTrue(OrientationValidator.IsValid(upRight, upRight),
                "facing up-right while moving up-right must pass");

            // Facing straight UP while moving up-right is 45° off — just OUTSIDE the
            // 30° tolerance -> FAIL (proves the tolerance band is enforced).
            Assert.IsFalse(OrientationValidator.IsValid(Up, upRight),
                "45° off (facing UP, moving up-right) exceeds the 30° tolerance -> fail");

            // Sanity on the measured angle for the 45° case.
            Assert.AreEqual(45f, OrientationValidator.AngleBetween(Up, upRight), 0.5f);
        }

        [Test]
        public void WithinTolerancePassesSmoothRotation()
        {
            // 20° off (mid-slerp toward the heading) is inside the 30° band -> PASS.
            Vector3 facing = Quaternion.Euler(0f, 20f, 0f) * Up;
            Assert.IsTrue(OrientationValidator.IsValid(facing, Up),
                "20° off (smooth-rotation in flight) is within tolerance -> pass");
            Assert.AreEqual(20f, OrientationValidator.AngleBetween(facing, Up), 0.5f);
        }

        [Test]
        public void YComponentIsIgnored()
        {
            // A vertical tilt on either vector must not affect the XZ heading check.
            Vector3 facing = Up + Vector3.up * 5f;
            Vector3 input = Up + Vector3.down * 3f;
            Assert.IsTrue(OrientationValidator.IsValid(facing, input),
                "Y component must be ignored — only XZ heading matters");
        }

        [Test]
        public void TryValidateYieldsReasonOnFailure()
        {
            bool ok = OrientationValidator.TryValidate(Down, Up, out string reason);
            Assert.IsFalse(ok);
            Assert.IsNotEmpty(reason, "a failing sample must yield a human-readable reason");

            bool ok2 = OrientationValidator.TryValidate(Up, Up, out string reason2);
            Assert.IsTrue(ok2);
            Assert.IsEmpty(reason2, "a passing sample must yield an empty reason");
        }
    }
}
