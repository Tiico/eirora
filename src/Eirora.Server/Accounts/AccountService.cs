using Eirora.Contracts;
using Eirora.Server.Data;
using Eirora.Server.SignIn;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OpenIddict.Abstractions;

namespace Eirora.Server.Accounts;

/// <summary>Creates, links and deletes accounts (ACC-01, ACC-05, D-72).</summary>
public sealed partial class AccountService(
    EiroraDbContext db,
    TimeProvider time,
    IOpenIddictTokenManager tokens,
    IOpenIddictAuthorizationManager authorizations,
    AppleIdClient apple,
    ILogger<AccountService> logger)
{
    /// <summary>The account the login belongs to, created if this is the login's first sign-in.</summary>
    public async Task<(Guid AccountId, bool Created)> SignInAsync(ExternalIdentity identity, CancellationToken cancellationToken)
    {
        if (await FindAccountIdAsync(identity, cancellationToken) is { } existing)
        {
            return (existing, false);
        }

        var now = time.GetUtcNow();
        var account = new Account
        {
            Id = Guid.CreateVersion7(now),
            CreatedAt = now,
            Logins = [new AccountLogin { Provider = identity.Provider, Subject = identity.Subject, LinkedAt = now }],
        };
        db.Accounts.Add(account);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException e) when (IsUniqueViolation(e))
        {
            // Two first sign-ins with the same login raced; the other one created the account.
            db.ChangeTracker.Clear();
            return (await FindAccountIdAsync(identity, cancellationToken) ?? throw new InvalidOperationException("Login vanished.", e), false);
        }

        LogCreated(logger);
        return (account.Id, true);
    }

    public async Task<bool> ExistsAsync(Guid accountId, CancellationToken cancellationToken) =>
        await db.Accounts.AnyAsync(a => a.Id == accountId, cancellationToken);

    public async Task<AccountInfo?> GetAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var account = await db.Accounts.AsNoTracking().Include(a => a.Logins)
            .SingleOrDefaultAsync(a => a.Id == accountId, cancellationToken);
        return account is null
            ? null
            : new AccountInfo(
                account.Id,
                account.CreatedAt,
                [.. account.Logins.OrderBy(l => l.LinkedAt).Select(l => new LinkedLogin(l.Provider, l.LinkedAt))]);
    }

    /// <summary>Links a login to the account, after a fresh sign-in with one of the account's own logins.</summary>
    public async Task<LinkResult> LinkAsync(
        Guid accountId,
        ExternalIdentity confirmation,
        ExternalIdentity identity,
        CancellationToken cancellationToken)
    {
        if (await FindAccountIdAsync(confirmation, cancellationToken) != accountId)
        {
            return LinkResult.NotThisAccount;
        }

        var owner = await FindAccountIdAsync(identity, cancellationToken);
        if (owner == accountId)
        {
            return LinkResult.Linked;
        }

        if (owner is not null)
        {
            return LinkResult.LinkedToAnotherAccount;
        }

        if (await db.AccountLogins.AnyAsync(l => l.AccountId == accountId && l.Provider == identity.Provider, cancellationToken))
        {
            return LinkResult.ProviderAlreadyLinked;
        }

        db.AccountLogins.Add(new AccountLogin
        {
            Provider = identity.Provider,
            Subject = identity.Subject,
            AccountId = accountId,
            LinkedAt = time.GetUtcNow(),
        });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException e) when (IsUniqueViolation(e))
        {
            db.ChangeTracker.Clear();
            return LinkResult.LinkedToAnotherAccount;
        }

        return LinkResult.Linked;
    }

    /// <summary>Removes the account's login with a provider, after a fresh sign-in with one of the account's own logins.</summary>
    public async Task<UnlinkResult> UnlinkAsync(
        Guid accountId,
        ExternalIdentity confirmation,
        string provider,
        CancellationToken cancellationToken)
    {
        var logins = await db.AccountLogins.Where(l => l.AccountId == accountId).ToListAsync(cancellationToken);
        if (!logins.Any(l => l.Provider == confirmation.Provider && l.Subject == confirmation.Subject))
        {
            return UnlinkResult.NotThisAccount;
        }

        var login = logins.SingleOrDefault(l => l.Provider == provider);
        if (login is null)
        {
            return UnlinkResult.NotLinked;
        }

        // Signing in with another linked login is how an account is recovered, so the last one only goes with the account.
        if (logins.Count == 1)
        {
            return UnlinkResult.LastLogin;
        }

        db.AccountLogins.Remove(login);
        await db.SaveChangesAsync(cancellationToken);
        return UnlinkResult.Unlinked;
    }

    /// <summary>
    /// Deletes the account and everything stored for it, after a fresh sign-in with one of its logins; with an Apple
    /// login, that sign-in must be Apple's so that the app's access to the Apple ID can be revoked.
    /// </summary>
    public async Task<DeleteResult> DeleteAsync(
        Guid accountId,
        ExternalIdentity confirmation,
        string? appleAuthorizationCode,
        CancellationToken cancellationToken)
    {
        var account = await db.Accounts.Include(a => a.Logins).SingleOrDefaultAsync(a => a.Id == accountId, cancellationToken);
        if (account is null)
        {
            return DeleteResult.NotFound;
        }

        if (!account.Logins.Any(l => l.Provider == confirmation.Provider && l.Subject == confirmation.Subject))
        {
            return DeleteResult.NotThisAccount;
        }

        if (account.Logins.Any(l => l.Provider == LoginProviders.Apple))
        {
            if (confirmation.Provider != LoginProviders.Apple || string.IsNullOrEmpty(appleAuthorizationCode))
            {
                return DeleteResult.AppleConfirmationRequired;
            }

            // From here on the confirmation is spent, so the deletion runs to the end even when the player hangs up;
            // Apple's own timeout bounds the wait. Deleting is the player's right, so a failure at Apple is logged and the
            // account is deleted anyway.
            await apple.RevokeAsync(appleAuthorizationCode, confirmation.Audience);
        }

        await DeleteConfirmedAsync(account);
        return DeleteResult.Deleted;
    }

    /// <summary>Deletes an account whose deletion has been confirmed; not cancellable, since the confirmation is spent.</summary>
    private async Task DeleteConfirmedAsync(Account account)
    {
        // Through OpenIddict's managers, so that no cached entry keeps an access token valid. Collected first, since
        // deleting while the query is still being read would need a second command on the connection.
        var subject = account.Id.ToString();
        foreach (var token in await ToListAsync(tokens.FindBySubjectAsync(subject, CancellationToken.None)))
        {
            await tokens.DeleteAsync(token, CancellationToken.None);
        }

        foreach (var authorization in await ToListAsync(authorizations.FindBySubjectAsync(subject, CancellationToken.None)))
        {
            await authorizations.DeleteAsync(authorization, CancellationToken.None);
        }

        db.Accounts.Remove(account);
        await db.SaveChangesAsync(CancellationToken.None);
        LogDeleted(logger);
    }

    private async Task<Guid?> FindAccountIdAsync(ExternalIdentity identity, CancellationToken cancellationToken) =>
        await db.AccountLogins
            .Where(l => l.Provider == identity.Provider && l.Subject == identity.Subject)
            .Select(l => (Guid?)l.AccountId)
            .SingleOrDefaultAsync(cancellationToken);

    private static async Task<List<object>> ToListAsync(IAsyncEnumerable<object> source)
    {
        var list = new List<object>();
        await foreach (var item in source)
        {
            list.Add(item);
        }

        return list;
    }

    private static bool IsUniqueViolation(DbUpdateException e) =>
        e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    [LoggerMessage(Level = LogLevel.Information, Message = "Created an account")]
    private static partial void LogCreated(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleted an account")]
    private static partial void LogDeleted(ILogger logger);
}

public enum LinkResult
{
    Linked,
    NotThisAccount,
    LinkedToAnotherAccount,
    ProviderAlreadyLinked,
}

public enum UnlinkResult
{
    Unlinked,
    NotThisAccount,
    NotLinked,
    LastLogin,
}

public enum DeleteResult
{
    Deleted,
    NotFound,
    NotThisAccount,
    AppleConfirmationRequired,
}
