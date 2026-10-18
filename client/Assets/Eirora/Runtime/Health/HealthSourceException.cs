using System;

namespace Eirora.Client.Health
{
    /// <summary>A health store call that failed, such as a permission taken back since the last read.</summary>
    public sealed class HealthSourceException : Exception
    {
        public HealthSourceException(string code, string message)
            : base(message) => Code = code;

        /// <summary>permission_denied, unavailable, invalid_argument, remote, io or error.</summary>
        public string Code { get; }
    }
}
