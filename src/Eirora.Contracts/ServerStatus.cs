namespace Eirora.Contracts
{
    /// <summary>Response from the server's status endpoint.</summary>
    public sealed class ServerStatus
    {
        /// <summary>The API version this build of the client and server speaks.</summary>
        public const int CurrentApiVersion = 1;

        /// <summary>The API version the server speaks.</summary>
        public int ApiVersion { get; set; }
    }
}
