using Eirora.Server.Accounts;
using Eirora.Server.SignIn;
using Microsoft.EntityFrameworkCore;

namespace Eirora.Server.Data;

/// <summary>The server's database: accounts, redeemed provider tokens and OpenIddict's token entries (ADR-0008).</summary>
public sealed class EiroraDbContext(DbContextOptions<EiroraDbContext> options) : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<AccountLogin> AccountLogins => Set<AccountLogin>();

    public DbSet<RedeemedIdToken> RedeemedIdTokens => Set<RedeemedIdToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>(account =>
        {
            account.HasKey(a => a.Id);
            account.HasMany(a => a.Logins).WithOne().HasForeignKey(l => l.AccountId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AccountLogin>(login =>
        {
            login.HasKey(l => new { l.Provider, l.Subject });
            login.Property(l => l.Provider).HasMaxLength(16);
            login.Property(l => l.Subject).HasMaxLength(255);
            login.HasIndex(l => new { l.AccountId, l.Provider }).IsUnique();
        });

        modelBuilder.Entity<RedeemedIdToken>(token =>
        {
            token.HasKey(t => t.Hash);
            token.Property(t => t.Hash).HasMaxLength(64);
            token.HasIndex(t => t.ExpiresAt);
        });

        modelBuilder.UseOpenIddict<Guid>();
    }
}
