namespace Eirora.Contracts
{
    /// <summary>
    /// Response of <c>GET /api/info</c>: what the server runs, so a client can detect a rules mismatch.
    /// </summary>
    public sealed class ServerInfo
    {
        public ServerInfo(int rulesVersion)
        {
            RulesVersion = rulesVersion;
        }

        /// <summary>The simulation rules version the server validates shared actions with.</summary>
        public int RulesVersion { get; }
    }
}
