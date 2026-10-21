using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Eirora.Client.Accounts;
using NUnit.Framework;

namespace Eirora.Client.Tests
{
    /// <summary>
    /// The web sign-ins: Sign in with Apple on Android through the server's callback, and Google on iOS with PKCE
    /// (TASK-022, ADR-0008). The browser is a fake that answers like the provider would.
    /// </summary>
    public sealed class WebSignInTests
    {
        private const string NonceHash = "5f2b0c2a2bb0f0d1e1f48a1e6e6c6e4b5b4e1d1e8a0f6f1c5a3e2b1d0c9b8a7f";
        private SynchronizationContext? _context;

        [SetUp]
        public void SetUp()
        {
            _context = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
        }

        [TearDown]
        public void TearDown() => SynchronizationContext.SetSynchronizationContext(_context);

        [Test]
        public void AppleOnAndroidAsksForNoScopesAndPostsToTheServersCallback()
        {
            var browser = new FakeBrowser(request => $"eirora-signin://apple?code=c1&id_token=apple.jwt&state={request["state"]}");
            var login = new AppleWebLogin("se.eirora.signin", "https://eirora.test/", browser);

            var signIn = Run(login.SignInAsync(NonceHash));

            Assert.That(browser.Url, Does.StartWith("https://appleid.apple.com/auth/authorize?"));
            Assert.That(browser.Request["client_id"], Is.EqualTo("se.eirora.signin"));
            Assert.That(browser.Request["redirect_uri"], Is.EqualTo("https://eirora.test/signin/apple/callback"));
            Assert.That(browser.Request["response_type"], Is.EqualTo("code id_token"));
            Assert.That(browser.Request["response_mode"], Is.EqualTo("form_post"));
            Assert.That(browser.Request["nonce"], Is.EqualTo(NonceHash));
            Assert.That(browser.Request.ContainsKey("scope"), Is.False, "Apple sends name and email only when asked (D-72).");
            Assert.That(browser.Scheme, Is.EqualTo(AppleWebLogin.CallbackScheme));
            Assert.That(signIn.IdToken, Is.EqualTo("apple.jwt"));
            Assert.That(signIn.AuthorizationCode, Is.EqualTo("c1"));
        }

        [Test]
        public void AnAnswerToAnotherSignInIsRejected()
        {
            var browser = new FakeBrowser(_ => "eirora-signin://apple?code=c1&id_token=apple.jwt&state=forged");
            var login = new AppleWebLogin("se.eirora.signin", "https://eirora.test", browser);

            Assert.Throws<LoginProviderException>(() => Run(login.SignInAsync(NonceHash)));
        }

        [Test]
        public void ClosingApplesPageIsACancel()
        {
            var browser = new FakeBrowser(request => $"eirora-signin://apple?error=user_cancelled_authorize&state={request["state"]}");
            var login = new AppleWebLogin("se.eirora.signin", "https://eirora.test", browser);

            Assert.Throws<SignInCancelledException>(() => Run(login.SignInAsync(NonceHash)));
        }

        [Test]
        public void GoogleOnIosExchangesTheCodeWithPkceForAnIdToken()
        {
            const string clientId = "1234-abc.apps.googleusercontent.com";
            var browser = new FakeBrowser(request => $"com.googleusercontent.apps.1234-abc:/oauth2redirect?state={request["state"]}&code=g1&scope=openid");
            var google = new FakeGoogle();
            var login = new GoogleWebLogin(clientId, browser, google);

            var signIn = Run(login.SignInAsync(NonceHash));

            Assert.That(browser.Url, Does.StartWith(GoogleWebLogin.AuthorizeUrl + "?"));
            Assert.That(browser.Scheme, Is.EqualTo("com.googleusercontent.apps.1234-abc"));
            Assert.That(browser.Request["scope"], Is.EqualTo("openid"), "No email or profile (D-72).");
            Assert.That(browser.Request["nonce"], Is.EqualTo(NonceHash));
            Assert.That(browser.Request["code_challenge_method"], Is.EqualTo("S256"));
            Assert.That(google.Form["code"], Is.EqualTo("g1"));
            Assert.That(google.Form["client_id"], Is.EqualTo(clientId));
            Assert.That(google.Form["redirect_uri"], Is.EqualTo(browser.Request["redirect_uri"]));
            Assert.That(GoogleWebLogin.Challenge(google.Form["code_verifier"]), Is.EqualTo(browser.Request["code_challenge"]));
            Assert.That(google.Form.ContainsKey("client_secret"), Is.False);
            Assert.That(signIn.IdToken, Is.EqualTo("google.jwt"));
        }

        [Test]
        public void ThePkceChallengeFollowsRfc7636()
        {
            // RFC 7636, appendix B.
            Assert.That(GoogleWebLogin.Challenge("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk"), Is.EqualTo("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM"));
        }

        [Test]
        public void TheNonceIsHashedAsLowercaseHex()
        {
            Assert.That(Nonce.Hash("abc"), Is.EqualTo("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad"));
            Assert.That(Nonce.Create(), Has.Length.EqualTo(43));
            Assert.That(Nonce.Create(), Is.Not.EqualTo(Nonce.Create()));
        }

        private static T Run<T>(Task<T> task)
        {
            Assert.That(task.IsCompleted, Is.True);
            return task.GetAwaiter().GetResult();
        }

        private sealed class FakeBrowser : IWebAuthenticator
        {
            private readonly Func<Dictionary<string, string>, string> _answer;

            public FakeBrowser(Func<Dictionary<string, string>, string> answer) => _answer = answer;

            public string Url { get; private set; } = string.Empty;

            public string Scheme { get; private set; } = string.Empty;

            public Dictionary<string, string> Request { get; private set; } = new();

            public Task<string> AuthenticateAsync(string url, string callbackScheme)
            {
                Url = url;
                Scheme = callbackScheme;
                Request = UrlQuery.ParseUrl(url);
                return Task.FromResult(_answer(Request));
            }
        }

        private sealed class FakeGoogle : IHttpTransport
        {
            public Dictionary<string, string> Form { get; private set; } = new();

            public Task<HttpReply> SendAsync(HttpCall call)
            {
                Assert.That(call.Url, Is.EqualTo(GoogleWebLogin.TokenUrl));
                Assert.That(call.ContentType, Is.EqualTo(HttpCall.Form));
                Form = UrlQuery.Parse(call.Body!);
                return Task.FromResult(new HttpReply(200, "{\"access_token\":\"a\",\"expires_in\":3599,\"id_token\":\"google.jwt\",\"token_type\":\"Bearer\"}"));
            }
        }
    }
}
