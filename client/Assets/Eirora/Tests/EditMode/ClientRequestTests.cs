using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Eirora.Client.Accounts;
using Eirora.Contracts;
using NUnit.Framework;

namespace Eirora.Client.Tests
{
    /// <summary>
    /// The contract with the server (ADR-0008): every request the client sends is byte for byte one of the requests in
    /// <c>src/Eirora.Contracts/Requests~</c>, and the server's own <c>ClientRequestTests</c> send exactly those requests to
    /// the real server. Either side drifting from the other fails one of the two.
    /// </summary>
    public sealed class ClientRequestTests
    {
        private RecordingTransport _http = null!;
        private AccountServer _server = null!;

        [SetUp]
        public void SetUp()
        {
            _http = new RecordingTransport();
            _server = new AccountServer(FakeAccountServer.Url + "/", _http);
        }

        [Test]
        public void SigningIn() => Sends("sign-in.http", _server.SignInAsync(LoginProviders.Google, "__ID_TOKEN__", "__NONCE__"));

        [Test]
        public void Refreshing() => Sends("refresh.http", _server.RefreshAsync("__REFRESH_TOKEN__"));

        [Test]
        public void SigningOut() => Sends("sign-out.http", _server.RevokeAsync("__REFRESH_TOKEN__"));

        [Test]
        public void GettingTheAccount() => Sends("account.http", _server.GetAccountAsync("__ACCESS_TOKEN__"));

        [Test]
        public void LinkingALogin() => Sends("link.http", _server.LinkAsync(
            "__ACCESS_TOKEN__",
            new ExternalLogin(LoginProviders.Apple, "__LOGIN_ID_TOKEN__", "__LOGIN_NONCE__"),
            new ExternalLogin(LoginProviders.Google, "__CONFIRMATION_ID_TOKEN__", "__CONFIRMATION_NONCE__")));

        [Test]
        public void RemovingALogin() => Sends("login-removal.http", _server.RemoveLoginAsync(
            "__ACCESS_TOKEN__",
            LoginProviders.Apple,
            new ExternalLogin(LoginProviders.Google, "__CONFIRMATION_ID_TOKEN__", "__CONFIRMATION_NONCE__")));

        [Test]
        public void DeletingTheAccount() => Sends("deletion.http", _server.DeleteAsync(
            "__ACCESS_TOKEN__",
            new ExternalLogin(LoginProviders.Apple, "__CONFIRMATION_ID_TOKEN__", "__CONFIRMATION_NONCE__", "__AUTHORIZATION_CODE__")));

        [Test]
        public void TheTokensAreReadAsTheServerSendsThem()
        {
            var tokens = AccountServer.ParseTokens(200, Read("token-response.json"));

            Assert.That(tokens.AccessToken, Is.EqualTo("access"));
            Assert.That(tokens.RefreshToken, Is.EqualTo("refresh"));
            Assert.That(tokens.ExpiresIn, Is.EqualTo(TimeSpan.FromMinutes(15)));
            Assert.That(tokens.AccountCreated, Is.True);
        }

        [Test]
        public void TheAccountIsReadAsTheServerSendsIt()
        {
            var account = AccountServer.ParseAccount(Read("account-response.json"))!;

            Assert.That(account.Id, Is.EqualTo(Guid.Parse("019a0a4e-6b5c-7d2e-9f10-2a3b4c5d6e7f")));
            Assert.That(account.CreatedAt, Is.EqualTo(new DateTimeOffset(2026, 10, 18, 10, 0, 0, TimeSpan.Zero)));
            Assert.That(account.Logins.Select(l => l.Provider), Is.EqualTo(new[] { LoginProviders.Google, LoginProviders.Apple }));
            Assert.That(account.Logins[1].LinkedAt, Is.EqualTo(new DateTimeOffset(2026, 10, 18, 10, 5, 0, TimeSpan.Zero)));
        }

        private void Sends<T>(string fixture, Task<T> call)
        {
            call.GetAwaiter().GetResult();
            var sent = _http.Calls.Single();
            var expected = Request.Parse(Read(fixture));

            Assert.That(sent.Method + " " + sent.Url, Is.EqualTo(expected.Method + " " + FakeAccountServer.Url + expected.Path));
            Assert.That(sent.Bearer, Is.EqualTo(expected.Bearer));
            Assert.That(sent.ContentType, Is.EqualTo(expected.ContentType));
            Assert.That(sent.Body, Is.EqualTo(expected.Body));
        }

        private static string Read(string fixture)
        {
            var contracts = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.eirora.contracts");
            return File.ReadAllText(Path.Combine(contracts.resolvedPath, "Requests~", fixture));
        }

        /// <summary>A request as the fixtures write it: the request line, headers, a blank line and a one-line body.</summary>
        private sealed class Request
        {
            private Request(string method, string path, Dictionary<string, string> headers, string? body)
            {
                Method = method;
                Path = path;
                Bearer = headers.TryGetValue("Authorization", out var authorization) ? authorization.Substring("Bearer ".Length) : null;
                ContentType = headers.TryGetValue("Content-Type", out var type) ? type : null;
                Body = body;
            }

            public string Method { get; }

            public string Path { get; }

            public string? Bearer { get; }

            public string? ContentType { get; }

            public string? Body { get; }

            public static Request Parse(string text)
            {
                var lines = text.Replace("\r", string.Empty).TrimEnd('\n').Split('\n');
                var requestLine = lines[0].Split(' ');
                var headers = lines.Skip(1).TakeWhile(l => l.Length > 0)
                    .Select(l => l.Split(new[] { ": " }, 2, StringSplitOptions.None))
                    .ToDictionary(h => h[0], h => h[1]);
                var body = lines.Skip(1 + headers.Count + 1).SingleOrDefault();
                return new Request(requestLine[0], requestLine[1], headers, body);
            }
        }

        private sealed class RecordingTransport : IHttpTransport
        {
            public List<HttpCall> Calls { get; } = new();

            public Task<HttpReply> SendAsync(HttpCall call)
            {
                Calls.Add(call);
                return Task.FromResult(new HttpReply(0));
            }
        }
    }
}
