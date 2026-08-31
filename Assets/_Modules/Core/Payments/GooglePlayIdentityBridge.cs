using System;
using System.Threading.Tasks;

namespace DeNelle.Core.Payments
{
    /// <summary>Provider-neutral door to identity code compiled only into GOOGLE_PLAY artifacts.</summary>
    public static class GooglePlayIdentityBridge
    {
        private static Func<Task<bool>> _signIn;
        public static bool Available => _signIn != null;
        public static void Register(Func<Task<bool>> signIn) => _signIn = signIn;
        public static Task<bool> EnsureSignedInAsync() =>
            _signIn != null ? _signIn() : Task.FromResult(false);
        public static void ResetForTests() => _signIn = null;
    }
}
