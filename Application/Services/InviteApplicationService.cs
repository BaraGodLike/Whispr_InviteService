using System.Security.Cryptography;
using Application.Abstractions;
using Application.Exceptions;
using Application.Models;
using Application.Options;
using Domain;

namespace Application.Services;

public sealed class InviteApplicationService(
    IInviteRepository inviteRepository,
    IInviteHasher inviteHasher,
    ISecretProtector secretProtector,
    IPowService powService,
    IDateTimeProvider dateTimeProvider,
    InviteOptions inviteOptions,
    LockoutOptions lockoutOptions)
{
    public async Task<CreateInviteResult> CreateInviteAsync(CancellationToken cancellationToken)
    {
        var nowUtc = dateTimeProvider.UtcNow;
        var inviteId = Guid.NewGuid();
        var serverSecret = RandomNumberGenerator.GetBytes(inviteOptions.ServerSecretSizeBytes);
        var revokeToken = RandomNumberGenerator.GetBytes(inviteOptions.RevokeTokenSizeBytes);
        var pin = GeneratePin(inviteOptions.PinAlphabet, inviteOptions.PinLength);
        var protectedServerSecret = secretProtector.Protect(serverSecret);

        var invite = new Invite
        {
            InviteId = inviteId,
            ProtectedServerSecret = protectedServerSecret,
            PinHash = inviteHasher.HashPin(pin),
            RevokeTokenHash = inviteHasher.HashToken(revokeToken),
            CreatedAtUtc = nowUtc,
            ExpiresAtUtc = nowUtc.Add(inviteOptions.Lifetime),
            IsUsed = false,
            Attempts = 0,
            LockedUntilUtc = null,
            IsRevoked = false
        };

        await inviteRepository.CreateAsync(invite, cancellationToken);

        return new CreateInviteResult(
            inviteId,
            serverSecret,
            pin,
            revokeToken,
            invite.ExpiresAtUtc);
    }

    public async Task<RedeemChallengeResult> GetRedeemChallengeAsync(
        Guid inviteId,
        CancellationToken cancellationToken)
    {
        var nowUtc = dateTimeProvider.UtcNow;
        var invite = await inviteRepository.GetByInviteIdAsync(inviteId, cancellationToken);

        if (invite is null)
        {
            return new RedeemChallengeResult(
                RedeemChallengeStatus.NotFound,
                null,
                null,
                0,
                null,
                null);
        }

        var status = GetChallengeStatus(invite, nowUtc);
        if (status != RedeemChallengeStatus.Available)
        {
            return new RedeemChallengeResult(
                status,
                null,
                null,
                invite.Attempts,
                null,
                invite.LockedUntilUtc);
        }

        var challenge = powService.CreateChallenge(inviteId, invite.Attempts, nowUtc);

        return new RedeemChallengeResult(
            RedeemChallengeStatus.Available,
            challenge.Nonce,
            challenge.Difficulty,
            invite.Attempts,
            challenge.ExpiresAtUtc,
            null);
    }

    public async Task<RedeemInviteResult> RedeemInviteAsync(
        Guid inviteId,
        string pin,
        string nonce,
        ulong solution,
        CancellationToken cancellationToken)
    {
        var nowUtc = dateTimeProvider.UtcNow;
        var invite = await inviteRepository.GetByInviteIdAsync(inviteId, cancellationToken);
        if (TryCreateRedeemResult(invite, nowUtc, out var currentResult))
        {
            return currentResult;
        }
        var currentInvite = invite!;

        var normalizedPin = NormalizePin(pin);
        powService.ValidateChallenge(inviteId, currentInvite.Attempts, normalizedPin, nonce, solution, nowUtc);

        var pinHash = inviteHasher.HashPin(normalizedPin);
        var protectedServerSecret = await inviteRepository.TryRedeemAsync(
            inviteId,
            pinHash,
            currentInvite.Attempts,
            nowUtc,
            cancellationToken);

        if (protectedServerSecret is not null)
        {
            var serverSecret = secretProtector.Unprotect(protectedServerSecret);
            return new RedeemInviteResult(RedeemInviteStatus.Succeeded, serverSecret, null);
        }

        return await HandleRedeemFailureAsync(
            inviteId,
            currentInvite,
            pinHash,
            nowUtc,
            cancellationToken);
    }

    public async Task<RevokeInviteResult> RevokeInviteAsync(
        Guid inviteId,
        byte[] revokeToken,
        CancellationToken cancellationToken)
    {
        var invite = await inviteRepository.GetByInviteIdAsync(inviteId, cancellationToken);

        if (invite is null)
        {
            return new RevokeInviteResult(RevokeInviteStatus.NotFound);
        }

        var revokeTokenHash = inviteHasher.HashToken(revokeToken);
        if (!inviteHasher.AreEqual(invite.RevokeTokenHash, revokeTokenHash))
        {
            throw new UnauthorizedAccessException("The supplied revoke token is invalid.");
        }

        if (GetRevokeStatus(invite) is { } status)
        {
            return new RevokeInviteResult(status);
        }

        var revoked = await inviteRepository.TryMarkRevokedAsync(inviteId, cancellationToken);
        if (revoked)
        {
            return new RevokeInviteResult(RevokeInviteStatus.Succeeded);
        }

        var refreshed = await inviteRepository.GetByInviteIdAsync(inviteId, cancellationToken);
        if (refreshed is null)
        {
            return new RevokeInviteResult(RevokeInviteStatus.NotFound);
        }

        if (GetRevokeStatus(refreshed) is { } refreshedStatus)
        {
            return new RevokeInviteResult(refreshedStatus);
        }

        return new RevokeInviteResult(RevokeInviteStatus.Succeeded);
    }

    private async Task<RedeemInviteResult> HandleRedeemFailureAsync(
        Guid inviteId,
        Invite inviteSnapshot,
        byte[] attemptedPinHash,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var refreshed = await inviteRepository.GetByInviteIdAsync(inviteId, cancellationToken);
        if (TryCreateRedeemResult(refreshed, nowUtc, out var refreshedResult))
        {
            return refreshedResult;
        }
        var currentInvite = refreshed!;

        if (currentInvite.Attempts != inviteSnapshot.Attempts)
        {
            throw new InvalidPowChallengeException(
                "The invite state changed. Request a new challenge and try again.");
        }

        if (inviteHasher.AreEqual(currentInvite.PinHash, attemptedPinHash))
        {
            throw new InvalidPowChallengeException(
                "The invite state changed. Request a new challenge and try again.");
        }

        var failedRedeemState = currentInvite.GetFailedRedeemState(
            nowUtc,
            lockoutOptions.FirstThreshold,
            lockoutOptions.FirstDuration,
            lockoutOptions.SecondThreshold,
            lockoutOptions.SecondDuration);

        var updated = await inviteRepository.TryRecordFailedRedeemAsync(
            inviteId,
            currentInvite.Attempts,
            failedRedeemState.Attempts,
            failedRedeemState.LockedUntilUtc,
            cancellationToken);

        if (!updated)
        {
            throw new InvalidPowChallengeException(
                "The invite state changed. Request a new challenge and try again.");
        }

        return new RedeemInviteResult(
            RedeemInviteStatus.InvalidPin,
            null,
            failedRedeemState.LockedUntilUtc);
    }

    private static RedeemInviteStatus GetRedeemStatus(Invite invite, DateTime nowUtc) =>
        invite switch
        {
            { IsRevoked: true } => RedeemInviteStatus.Revoked,
            { IsUsed: true } => RedeemInviteStatus.Used,
            _ when invite.IsExpired(nowUtc) => RedeemInviteStatus.Expired,
            _ when invite.IsLocked(nowUtc) => RedeemInviteStatus.Locked,
            _ => RedeemInviteStatus.InvalidPin
        };

    private static RedeemChallengeStatus GetChallengeStatus(Invite invite, DateTime nowUtc) =>
        invite switch
        {
            { IsRevoked: true } => RedeemChallengeStatus.Revoked,
            { IsUsed: true } => RedeemChallengeStatus.Used,
            _ when invite.IsExpired(nowUtc) => RedeemChallengeStatus.Expired,
            _ when invite.IsLocked(nowUtc) => RedeemChallengeStatus.Locked,
            _ => RedeemChallengeStatus.Available
        };

    private static bool TryCreateRedeemResult(
        Invite? invite,
        DateTime nowUtc,
        out RedeemInviteResult result)
    {
        switch (invite)
        {
            case null:
                result = new RedeemInviteResult(RedeemInviteStatus.NotFound, null, null);
                return true;
            default:
            {
                var status = GetRedeemStatus(invite, nowUtc);
                if (status == RedeemInviteStatus.InvalidPin)
                {
                    result = null!;
                    return false;
                }

                result = new RedeemInviteResult(
                    status,
                    null,
                    status == RedeemInviteStatus.Locked ? invite.LockedUntilUtc : null);
                return true;
            }
        }
    }

    private static RevokeInviteStatus? GetRevokeStatus(Invite invite) =>
        invite switch
        {
            { IsUsed: true } => RevokeInviteStatus.Used,
            { IsRevoked: true } => RevokeInviteStatus.Revoked,
            _ => null
        };

    private static string GeneratePin(string alphabet, int length)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(alphabet);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);

        var chars = new char[length];
        var buffer = new byte[length];
        RandomNumberGenerator.Fill(buffer);

        for (var index = 0; index < length; index++)
        {
            chars[index] = alphabet[buffer[index] % alphabet.Length];
        }

        return new string(chars);
    }

    private static string NormalizePin(string? pin) =>
        (pin ?? string.Empty).Trim().ToUpperInvariant();
}
