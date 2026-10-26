using Eirora.Client.Accounts;
using NUnit.Framework;

namespace Eirora.Client.Tests
{
    /// <summary>
    /// What Credential Manager's exceptions mean (TASK-067): the part of Google's sign-in on Android that can be checked
    /// without a phone. The answers are the ones SignInBridge.kt passes on, as a Pixel 7 gave them.
    /// </summary>
    public sealed class SignInAnswersTests
    {
        private const string UserCanceled = "android.credentials.GetCredentialException.TYPE_USER_CANCELED";
        private const string NoCredential = "android.credentials.GetCredentialException.TYPE_NO_CREDENTIAL";

        [Test]
        public void AFailedFlowAfterTheChooserIsAnErrorThoughGoogleCallsItACancellation()
        {
            // With a wrong SHA-1 for the Android client, logcat shows UNREGISTERED_ON_API_CONSOLE and then this (2026-10-26).
            var answer = CredentialManagerAnswer.Failure("cancellation", UserCanceled, "[16] Account reauth failed.");

            Assert.That(answer, Is.TypeOf<LoginProviderException>());
            Assert.That(answer.Message, Is.EqualTo("Credential Manager cancellation " + UserCanceled + " [16]"));
        }

        [Test]
        public void TheReasonKeepsGooglesStatusCodeButNeverItsMessage()
        {
            var answer = CredentialManagerAnswer.Failure("other", "com.google.android.gms.TYPE_SOMETHING", "[28444] Developer console is not set up correctly.");

            Assert.That(answer.Message, Is.EqualTo("Credential Manager other com.google.android.gms.TYPE_SOMETHING [28444]"));
            Assert.That(answer.Message, Does.Not.Contain("console"));
        }

        [Test]
        public void ClosingTheChooserIsTheOnlyCancellation()
        {
            Assert.That(CredentialManagerAnswer.Failure("cancellation", UserCanceled, "User cancelled the selector"), Is.TypeOf<SignInCancelledException>());
            Assert.That(CredentialManagerAnswer.Failure("cancellation", UserCanceled, "[16] Cancelled by user."), Is.TypeOf<SignInCancelledException>());
            Assert.That(CredentialManagerAnswer.Failure("cancellation", UserCanceled, "activity is cancelled by the user."), Is.TypeOf<SignInCancelledException>());
            Assert.That(CredentialManagerAnswer.Failure("cancellation", UserCanceled, null), Is.TypeOf<SignInCancelledException>(), "the framework's own cancellation may say nothing");
        }

        [Test]
        public void ACancellationSaysWhatItWas()
        {
            var answer = CredentialManagerAnswer.Failure("cancellation", UserCanceled, "[16] Cancelled by user.");

            Assert.That(((SignInCancelledException)answer).Reason, Is.EqualTo("Credential Manager cancellation " + UserCanceled + " [16]"));
        }

        [Test]
        public void NoGoogleAccountIsAnError()
        {
            var answer = CredentialManagerAnswer.Failure("no-credential", NoCredential, "[28433] Cannot find a matching credential.");

            Assert.That(answer, Is.TypeOf<LoginProviderException>());
            Assert.That(answer.Message, Is.EqualTo("Credential Manager no-credential " + NoCredential + " [28433]"));
        }
    }
}
