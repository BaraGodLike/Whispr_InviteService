using Application.Abstractions;
using Dapper;
using Domain;

namespace Infrastructure.Storage;

public sealed class PostgresInviteRepository : IInviteRepository
{
    private readonly PostgresConnectionFactory _connectionFactory;

    public PostgresInviteRepository(PostgresConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task CreateAsync(Invite invite, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO invites (
                invite_id,
                protected_server_secret,
                pin_hash,
                revoke_token_hash,
                created_at_utc,
                expires_at_utc,
                is_used,
                attempts,
                locked_until_utc,
                is_revoked
            )
            VALUES (
                @InviteId,
                @ProtectedServerSecret,
                @PinHash,
                @RevokeTokenHash,
                @CreatedAtUtc,
                @ExpiresAtUtc,
                @IsUsed,
                @Attempts,
                @LockedUntilUtc,
                @IsRevoked
            );
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(
            new CommandDefinition(sql, invite, cancellationToken: cancellationToken));
    }

    public async Task<Invite?> GetByInviteIdAsync(Guid inviteId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                id AS Id,
                invite_id AS InviteId,
                protected_server_secret AS ProtectedServerSecret,
                pin_hash AS PinHash,
                revoke_token_hash AS RevokeTokenHash,
                created_at_utc AS CreatedAtUtc,
                expires_at_utc AS ExpiresAtUtc,
                is_used AS IsUsed,
                attempts AS Attempts,
                locked_until_utc AS LockedUntilUtc,
                is_revoked AS IsRevoked
            FROM invites
            WHERE invite_id = @InviteId;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Invite>(
            new CommandDefinition(sql, new { InviteId = inviteId }, cancellationToken: cancellationToken));
    }

    public async Task<byte[]?> TryRedeemAsync(
        Guid inviteId,
        byte[] pinHash,
        int expectedAttempts,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE invites
            SET is_used = TRUE
            WHERE invite_id = @InviteId
              AND pin_hash = @PinHash
              AND attempts = @ExpectedAttempts
              AND is_used = FALSE
              AND is_revoked = FALSE
              AND expires_at_utc > @UtcNow
              AND (locked_until_utc IS NULL OR locked_until_utc <= @UtcNow)
            RETURNING protected_server_secret;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<byte[]>(
            new CommandDefinition(
                sql,
                new
                {
                    InviteId = inviteId,
                    PinHash = pinHash,
                    ExpectedAttempts = expectedAttempts,
                    UtcNow = utcNow
                },
                cancellationToken: cancellationToken));
    }

    public async Task<bool> TryRecordFailedRedeemAsync(
        Guid inviteId,
        int expectedAttempts,
        int nextAttempts,
        DateTime? lockedUntilUtc,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE invites
            SET
                attempts = @NextAttempts,
                locked_until_utc = @LockedUntilUtc
            WHERE invite_id = @InviteId
              AND attempts = @ExpectedAttempts
              AND is_used = FALSE
              AND is_revoked = FALSE;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var affected = await connection.ExecuteAsync(
            new CommandDefinition(
                sql,
                new
                {
                    InviteId = inviteId,
                    ExpectedAttempts = expectedAttempts,
                    NextAttempts = nextAttempts,
                    LockedUntilUtc = lockedUntilUtc
                },
                cancellationToken: cancellationToken));

        return affected == 1;
    }

    public async Task<bool> TryMarkRevokedAsync(Guid inviteId, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE invites
            SET is_revoked = TRUE
            WHERE invite_id = @InviteId
              AND is_revoked = FALSE
              AND is_used = FALSE;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var affected = await connection.ExecuteAsync(
            new CommandDefinition(sql, new { InviteId = inviteId }, cancellationToken: cancellationToken));

        return affected == 1;
    }

    public async Task DeleteExpiredAsync(DateTime utcNow, CancellationToken cancellationToken)
    {
        const string sql = """
            DELETE FROM invites
            WHERE expires_at_utc <= @UtcNow;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(
            new CommandDefinition(sql, new { UtcNow = utcNow }, cancellationToken: cancellationToken));
    }
}
