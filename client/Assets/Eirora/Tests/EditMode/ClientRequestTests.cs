using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
        public void GettingTheCloudSave() => Sends("cloud-save.http", _server.GetCloudSaveAsync("__ACCESS_TOKEN__"));

        [Test]
        public void UploadingTheCloudSave()
        {
            byte[] world = Encoding.UTF8.GetBytes("eirora-save 3\n");
            byte[] contributions = Encoding.UTF8.GetBytes("eirora-sealed 1\n");

            Sends(
                "cloud-save-upload.http",
                _server.UploadCloudSaveAsync("__ACCESS_TOKEN__", new CloudSaveUpload(0, 1, world, contributions)),
                new Dictionary<string, string> { ["WORLD"] = Convert.ToBase64String(world), ["CONTRIBUTIONS"] = Convert.ToBase64String(contributions) });
        }

        [Test]
        public void CreatingAKeyTransfer()
        {
            byte[] publicKey = new byte[32];

            Sends(
                "key-transfer-create.http",
                _server.CreateKeyTransferAsync("__ACCESS_TOKEN__", publicKey),
                new Dictionary<string, string> { ["RECEIVER_PUBLIC_KEY"] = Convert.ToBase64String(publicKey) });
        }

        [Test]
        public void GettingAKeyTransfer() => Sends("key-transfer.http", _server.GetKeyTransferAsync("__ACCESS_TOKEN__", "__TRANSFER_ID__"));

        [Test]
        public void DeliveringAKeyTransfer()
        {
            byte[] sealedKey = Encoding.UTF8.GetBytes("eirora-transfer 1\n");

            Sends(
                "key-transfer-deliver.http",
                _server.DeliverKeyTransferAsync("__ACCESS_TOKEN__", "__TRANSFER_ID__", sealedKey),
                new Dictionary<string, string> { ["SEALED_KEY"] = Convert.ToBase64String(sealedKey) });
        }

        [Test]
        public void DeletingAKeyTransfer() => Sends("key-transfer-delete.http", _server.DeleteKeyTransferAsync("__ACCESS_TOKEN__", "__TRANSFER_ID__"));

        [Test]
        public void TheKeyTransferIsReadAsTheServerSendsIt()
        {
            var transfer = AccountServer.ParseKeyTransfer(Read("key-transfer-response.json"))!;

            Assert.That(transfer.Id, Is.EqualTo("Ab12Cd34Ef56Gh78Ij90Kl"));
            Assert.That(transfer.ReceiverPublicKey, Is.EqualTo(new byte[32]));
            Assert.That(transfer.SealedKey, Is.Null);
            Assert.That(transfer.ExpiresAt, Is.EqualTo(new DateTimeOffset(2026, 10, 21, 20, 10, 0, TimeSpan.Zero)));
        }

        [Test]
        public void TheCloudSaveIsReadAsTheServerSendsIt()
        {
            var save = AccountServer.ParseCloudSave(Read("cloud-save-response.json"))!;

            Assert.That((save.Version, save.KeyEpoch), Is.EqualTo((1L, 1)));
            Assert.That(Encoding.UTF8.GetString(save.World), Is.EqualTo("eirora-save 3\n"));
            Assert.That(Encoding.UTF8.GetString(save.Contributions), Is.EqualTo("eirora-sealed 1\n"));
            Assert.That(save.SavedAt, Is.EqualTo(new DateTimeOffset(2026, 10, 21, 20, 0, 0, TimeSpan.Zero)));
        }

        [Test]
        public void TheCloudSaveVersionIsReadAsTheServerSendsIt()
        {
            var version = AccountServer.ParseCloudSaveVersion(Read("cloud-save-version-response.json"))!;

            Assert.That((version.Version, version.KeyEpoch), Is.EqualTo((2L, 1)));
        }

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

        private void Sends<T>(string fixture, Task<T> call, Dictionary<string, string>? values = null)
        {
            call.GetAwaiter().GetResult();
            var sent = _http.Calls.Single();
            string text = Read(fixture);
            foreach (var value in values ?? new Dictionary<string, string>())
            {
                text = text.Replace($"__{value.Key}__", value.Value);
            }

            var expected = Request.Parse(text);

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
