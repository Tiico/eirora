using Eirora.Server.Accounts;
using Eirora.Server.CloudSaves;
using Eirora.Server.KeyTransfers;
using Eirora.Server.SignIn;
using Microsoft.EntityFrameworkCore;

namespace Eirora.Server.Data;

/// <summary>
/// The server's database: accounts, their cloud saves and key transfers, redeemed provider tokens and OpenIddict's token
/// entries (ADR-0008).
/// </summary>
public sealed class EiroraDbContext(DbContextOptions<EiroraDbContext> options) : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<AccountLogin> AccountLogins => Set<AccountLogin>();

    public DbSet<StoredCloudSave> CloudSaves => Set<StoredCloudSave>();

    public DbSet<StoredLockedContributions> LockedContributions => Set<StoredLockedContributions>();

    public DbSet<StoredKeyTransfer> KeyTransfers => Set<StoredKeyTransfer>();

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

        // Both go with the account (ACC-05).
        modelBuilder.Entity<StoredCloudSave>(save =>
        {
            save.HasKey(s => s.AccountId);
            save.HasOne<Account>().WithOne().HasForeignKey<StoredCloudSave>(s => s.AccountId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StoredLockedContributions>(locked =>
        {
            locked.HasKey(l => new { l.AccountId, l.KeyEpoch });
            locked.HasOne<Account>().WithMany().HasForeignKey(l => l.AccountId).OnDelete(DeleteBehavior.Cascade);
            locked.HasIndex(l => l.LockedAt);
        });

        modelBuilder.Entity<StoredKeyTransfer>(transfer =>
        {
            transfer.HasKey(t => t.Id);
            transfer.Property(t => t.Id).HasMaxLength(64);
            transfer.HasOne<Account>().WithMany().HasForeignKey(t => t.AccountId).OnDelete(DeleteBehavior.Cascade);
            transfer.HasIndex(t => t.AccountId);
            transfer.HasIndex(t => t.ExpiresAt);
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
