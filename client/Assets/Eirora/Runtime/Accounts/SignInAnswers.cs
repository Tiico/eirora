using System;
using System.Text.RegularExpressions;

namespace Eirora.Client.Accounts
{
    /// <summary>
    /// What Credential Manager's exceptions mean (SignInBridge.kt): the part of Google's sign-in on Android that can be
    /// checked without a phone (TASK-067).
    /// </summary>
    public static class CredentialManagerAnswer
    {
        private static readonly Regex s_statusCode = new(@"^\s*\[(\d+)\]");

        /// <summary>
        /// How the sign-in ended, from the kind of GetCredentialException SignInBridge.kt caught (<c>cancellation</c>,
        /// <c>no-credential</c> or <c>other</c>), its type and Google's message.
        /// </summary>
        /// <remarks>
        /// Google ends a flow that failed after the player chose an account as a cancellation too: with a wrong SHA-1 for
        /// the Android client, <c>[16] Account reauth failed.</c> follows UNREGISTERED_ON_API_CONSOLE. So a cancellation is
        /// the player's only when it says so, or says nothing. The reason keeps the kind, the type and Google's status code,
        /// never the message itself.
        /// </remarks>
        public static Exception Failure(string kind, string type, string? message)
        {
            var status = message is null ? null : s_statusCode.Match(message);
            string reason = $"Credential Manager {kind} {type}" + (status is { Success: true } ? $" [{status.Groups[1].Value}]" : string.Empty);
            bool byThePlayer = kind == "cancellation"
                && (string.IsNullOrWhiteSpace(message) || message!.IndexOf("cancel", StringComparison.OrdinalIgnoreCase) >= 0);
            return byThePlayer ? new SignInCancelledException(reason) : new LoginProviderException(reason);
        }
    }
}
