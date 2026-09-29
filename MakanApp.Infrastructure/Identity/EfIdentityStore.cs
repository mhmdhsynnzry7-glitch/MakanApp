using System.Data;
using MakanApp.Application.Identity;
using MakanApp.Domain.Identity;
using MakanApp.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace MakanApp.Infrastructure.Identity;

public sealed class EfIdentityStore(MakanDbContext dbContext) : IIdentityStore
{
    public async Task<IIdentityTransaction> BeginSerializableTransactionAsync(
        CancellationToken cancellationToken)
    {
        var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        return new EfIdentityTransaction(transaction);
    }

    public Task<OtpChallenge?> GetLatestOtpChallengeAsync(
        string normalizedPhoneNumber,
        CancellationToken cancellationToken) =>
        dbContext.OtpChallenges
            .Where(challenge => challenge.NormalizedPhoneNumber == normalizedPhoneNumber)
            .OrderByDescending(challenge => challenge.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<OtpChallenge?> GetOtpChallengeAsync(
        Guid challengeId,
        string normalizedPhoneNumber,
        CancellationToken cancellationToken) =>
        dbContext.OtpChallenges.SingleOrDefaultAsync(
            challenge => challenge.Id == challengeId &&
                         challenge.NormalizedPhoneNumber == normalizedPhoneNumber,
            cancellationToken);

    public Task<int> CountOtpChallengesAsync(
        string normalizedPhoneNumber,
        DateTime createdAfterUtc,
        CancellationToken cancellationToken) =>
        dbContext.OtpChallenges.CountAsync(
            challenge => challenge.NormalizedPhoneNumber == normalizedPhoneNumber &&
                         challenge.CreatedAtUtc >= createdAfterUtc,
            cancellationToken);

    public Task<UserCredential?> GetMobileCredentialAsync(
        string normalizedPhoneNumber,
        CancellationToken cancellationToken) =>
        dbContext.UserCredentials.SingleOrDefaultAsync(
            credential => credential.Kind == CredentialKind.MobilePhone &&
                          credential.NormalizedIdentifier == normalizedPhoneNumber,
            cancellationToken);

    public Task<UserCredential?> GetMobileCredentialByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        dbContext.UserCredentials.SingleOrDefaultAsync(
            credential => credential.UserId == userId &&
                          credential.Kind == CredentialKind.MobilePhone,
            cancellationToken);

    public Task<User?> GetUserAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.Users.SingleOrDefaultAsync(user => user.Id == userId, cancellationToken);

    public Task<Person?> GetPersonAsync(Guid personId, CancellationToken cancellationToken) =>
        dbContext.Persons.SingleOrDefaultAsync(person => person.Id == personId, cancellationToken);

    public Task<UserSession?> GetSessionByTokenHashAsync(
        byte[] tokenHash,
        CancellationToken cancellationToken) =>
        dbContext.UserSessions.SingleOrDefaultAsync(
            session => session.TokenHash.SequenceEqual(tokenHash),
            cancellationToken);

    public Task<UserSession?> GetSessionAsync(
        Guid sessionId,
        Guid userId,
        CancellationToken cancellationToken) =>
        dbContext.UserSessions.SingleOrDefaultAsync(
            session => session.Id == sessionId && session.UserId == userId,
            cancellationToken);

    public Task<bool> UsernameExistsAsync(
        string normalizedUsername,
        Guid exceptUserId,
        CancellationToken cancellationToken) =>
        dbContext.Users.AnyAsync(
            user => user.Id != exceptUserId &&
                    user.NormalizedUsername == normalizedUsername,
            cancellationToken);

    public void Add(OtpChallenge challenge) => dbContext.OtpChallenges.Add(challenge);
    public void Add(User user) => dbContext.Users.Add(user);
    public void Add(Person person) => dbContext.Persons.Add(person);
    public void Add(UserCredential credential) => dbContext.UserCredentials.Add(credential);
    public void Add(UserSession session) => dbContext.UserSessions.Add(session);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqlException
            {
                Number: 2601 or 2627
            } sqlException &&
                  sqlException.Message.Contains(
                      "UX_Users_NormalizedUsername",
                      StringComparison.OrdinalIgnoreCase))
        {
            throw new IdentityException(
                IdentityErrorCodes.UsernameAlreadyExists,
                "این نام کاربری قبلاً استفاده شده است.");
        }
    }

    private sealed class EfIdentityTransaction(IDbContextTransaction transaction)
        : IIdentityTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken) =>
            transaction.CommitAsync(cancellationToken);

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
