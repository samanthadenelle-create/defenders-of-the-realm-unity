namespace DeNelle.Core.Services
{
    /// <summary>
    /// Release gate for the local-only clan/chat prototype. Keep false until WO-1265's
    /// server, moderation, two-wallet, and operator-readiness acceptance is complete.
    /// </summary>
    public static class ClanFeatureGate
    {
        public const bool PlayerFacingEnabled = false;
    }
}

