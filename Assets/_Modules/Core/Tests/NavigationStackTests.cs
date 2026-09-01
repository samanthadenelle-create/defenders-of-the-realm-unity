using NUnit.Framework;
using DeNelle.Core.UI;

namespace DeNelle.Core.Tests
{
    public sealed class NavigationStackTests
    {
        [Test]
        public void Root_IsStable_AndBackNeverMeansClose()
        {
            var stack = new NavigationStack<string>();
            stack.OpenRoot("Realm");

            Assert.That(stack.Current, Is.EqualTo("Realm"));
            Assert.That(stack.CanGoBack, Is.False);
            Assert.That(stack.Back(), Is.False);
            Assert.That(stack.Current, Is.EqualTo("Realm"));
        }

        [Test]
        public void PushAndBack_ReturnExactlyOneLevel()
        {
            var stack = new NavigationStack<string>();
            stack.OpenRoot("Hero");
            stack.Push("Bag");
            stack.Push("Equipment");

            Assert.That(stack.Back(), Is.True);
            Assert.That(stack.Current, Is.EqualTo("Bag"));
            Assert.That(stack.Count, Is.EqualTo(2));
        }

        [Test]
        public void OpeningRoot_DropsStaleHistory()
        {
            var stack = new NavigationStack<string>();
            stack.OpenRoot("Journey");
            stack.Push("Quests");
            stack.OpenRoot("Manage");

            Assert.That(stack.Count, Is.EqualTo(1));
            Assert.That(stack.Current, Is.EqualTo("Manage"));
            Assert.That(stack.CanGoBack, Is.False);
        }

        [Test]
        public void PushBeforeRoot_IsRejected()
        {
            var stack = new NavigationStack<string>();
            Assert.Throws<System.InvalidOperationException>(() => stack.Push("Orphan"));
        }
    }
}
