using Eirora.Server.Data;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(Eirora.Server.Tests.ServerFixture))]

namespace Eirora.Server.Tests;

/// <summary>
/// One server and one real PostgreSQL 18 for the whole test run, migrated with the server's migrations (ADR-0008).
/// </summary>
/// <remarks>
/// Starts PostgreSQL with Testcontainers, or uses the database in <c>EIRORA_TEST_POSTGRES</c> (a connection string) on
/// machines without Docker. Tests share the server and keep apart by signing in with new logins every time.
/// </remarks>
public sealed class ServerFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;

    public string ConnectionString { get; private set; } = "";

    public ServerFactory Server { get; private set; } = null!;

    /// <summary>A second server on the same database with Google only and no Apple configuration at all.</summary>
    public ServerFactory GoogleOnlyServer { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        var external = Environment.GetEnvironmentVariable("EIRORA_TEST_POSTGRES");
        if (string.IsNullOrEmpty(external))
        {
            _container = new PostgreSqlBuilder("postgres:18.6").Build();
            await _container.StartAsync();
            ConnectionString = _container.GetConnectionString();
        }
        else
        {
            ConnectionString = external;
        }

        await using (var db = CreateDbContext())
        {
            await db.Database.MigrateAsync();
        }

        // Started here, before any test: the factory builds its host on first use without a lock, so tests that start at
        // once could each build one, with its own token keys, and a token from one host would then be rejected by another.
        Server = new ServerFactory(ConnectionString);
        Server.StartServer();
        GoogleOnlyServer = new ServerFactory(ConnectionString, apple: false);
        GoogleOnlyServer.StartServer();
    }

    public EiroraDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<EiroraDbContext>();
        options.UseNpgsql(ConnectionString).UseOpenIddict<Guid>();
        return new EiroraDbContext(options.Options);
    }

    public async ValueTask DisposeAsync()
    {
        if (Server is not null)
        {
            await Server.DisposeAsync();
        }

        if (GoogleOnlyServer is not null)
        {
            await GoogleOnlyServer.DisposeAsync();
        }

        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}
