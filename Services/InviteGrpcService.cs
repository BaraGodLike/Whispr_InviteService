using Application.Exceptions;
using Application.Services;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Services.Protos;

namespace Services;

public sealed class InviteGrpcService(
    InviteApplicationService inviteApplicationService,
    ILogger<InviteGrpcService> logger)
    : InviteService.InviteServiceBase
{
    public override async Task<CreateInviteReply> CreateInvite(
        CreateInviteRequest request,
        ServerCallContext context)
    {
        var result = await inviteApplicationService.CreateInviteAsync(context.CancellationToken);

        return new CreateInviteReply
        {
            InviteId = result.InviteId.ToString("D"),
            ServerSecret = Convert.ToBase64String(result.ServerSecret),
            Pin = result.Pin,
            RevokeToken = Convert.ToBase64String(result.RevokeToken),
            ExpiresAt = Timestamp.FromDateTime(result.ExpiresAtUtc)
        };
    }

    public override async Task<GetRedeemChallengeReply> GetRedeemChallenge(
        GetRedeemChallengeRequest request,
        ServerCallContext context)
    {
        var inviteId = ParseInviteId(request.InviteId);
        var result = await inviteApplicationService.GetRedeemChallengeAsync(
            inviteId,
            context.CancellationToken);

        var reply = new GetRedeemChallengeReply
        {
            Status = MapChallengeStatus(result.Status),
            Attempts = result.Attempts
        };

        if (!string.IsNullOrWhiteSpace(result.Nonce))
        {
            reply.Nonce = result.Nonce;
        }

        if (result.Difficulty.HasValue)
        {
            reply.Difficulty = result.Difficulty.Value;
        }

        if (result.ExpiresAtUtc.HasValue)
        {
            reply.ExpiresAt = Timestamp.FromDateTime(result.ExpiresAtUtc.Value);
        }

        if (result.LockedUntilUtc.HasValue)
        {
            reply.LockedUntil = Timestamp.FromDateTime(result.LockedUntilUtc.Value);
        }

        return reply;
    }

    public override async Task<RedeemInviteReply> RedeemInvite(
        RedeemInviteRequest request,
        ServerCallContext context)
    {
        try
        {
            var inviteId = ParseInviteId(request.InviteId);
            var result = await inviteApplicationService.RedeemInviteAsync(
                inviteId,
                request.Pin,
                request.Nonce,
                request.PowSolution,
                context.CancellationToken);

            var reply = new RedeemInviteReply
            {
                Status = MapRedeemStatus(result.Status)
            };

            if (result.ServerSecret is not null)
            {
                reply.ServerSecret = Convert.ToBase64String(result.ServerSecret);
            }

            if (result.LockedUntilUtc.HasValue)
            {
                reply.LockedUntil = Timestamp.FromDateTime(result.LockedUntilUtc.Value);
            }

            return reply;
        }
        catch (InvalidPowChallengeException ex)
        {
            logger.LogWarning(ex, "Redeem request rejected because the proof-of-work challenge is invalid.");
            throw new RpcException(new Status(StatusCode.FailedPrecondition, ex.Message));
        }
        catch (InsufficientProofOfWorkException ex)
        {
            logger.LogWarning(ex, "Redeem request rejected because the proof of work is insufficient.");
            throw new RpcException(new Status(StatusCode.ResourceExhausted, ex.Message));
        }
    }

    public override async Task<RevokeInviteReply> RevokeInvite(
        RevokeInviteRequest request,
        ServerCallContext context)
    {
        try
        {
            var inviteId = ParseInviteId(request.InviteId);
            var revokeToken = ParseBase64(request.RevokeToken, "revoke_token");
            var result = await inviteApplicationService.RevokeInviteAsync(
                inviteId,
                revokeToken,
                context.CancellationToken);

            return new RevokeInviteReply
            {
                Status = MapRevokeStatus(result.Status)
            };
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning(ex, "Revoke request rejected because authorization failed.");
            throw new RpcException(new Status(StatusCode.PermissionDenied, ex.Message));
        }
    }

    private Guid ParseInviteId(string inviteId)
    {
        if (!Guid.TryParse(inviteId, out var parsedInviteId))
        {
            logger.LogWarning("Request rejected because invite_id is not a valid GUID.");
            throw new RpcException(new Status(StatusCode.InvalidArgument, "invite_id must be a valid GUID."));
        }

        return parsedInviteId;
    }

    private byte[] ParseBase64(string value, string fieldName)
    {
        try
        {
            return Convert.FromBase64String(value);
        }
        catch (FormatException)
        {
            logger.LogWarning("Request rejected because {FieldName} is not a valid Base64 string.", fieldName);
            throw new RpcException(
                new Status(StatusCode.InvalidArgument, $"{fieldName} must be a valid Base64 string."));
        }
    }

    private static RedeemChallengeStatus MapChallengeStatus(Domain.RedeemChallengeStatus status) =>
        status switch
        {
            Domain.RedeemChallengeStatus.Available => RedeemChallengeStatus.Available,
            Domain.RedeemChallengeStatus.NotFound => RedeemChallengeStatus.NotFound,
            Domain.RedeemChallengeStatus.Expired => RedeemChallengeStatus.Expired,
            Domain.RedeemChallengeStatus.Used => RedeemChallengeStatus.Used,
            Domain.RedeemChallengeStatus.Locked => RedeemChallengeStatus.Locked,
            Domain.RedeemChallengeStatus.Revoked => RedeemChallengeStatus.Revoked,
            _ => RedeemChallengeStatus.Unspecified
        };

    private static RedeemInviteStatus MapRedeemStatus(Domain.RedeemInviteStatus status) =>
        status switch
        {
            Domain.RedeemInviteStatus.Succeeded => RedeemInviteStatus.Succeeded,
            Domain.RedeemInviteStatus.NotFound => RedeemInviteStatus.NotFound,
            Domain.RedeemInviteStatus.Expired => RedeemInviteStatus.Expired,
            Domain.RedeemInviteStatus.Used => RedeemInviteStatus.Used,
            Domain.RedeemInviteStatus.InvalidPin => RedeemInviteStatus.InvalidPin,
            Domain.RedeemInviteStatus.Locked => RedeemInviteStatus.Locked,
            Domain.RedeemInviteStatus.Revoked => RedeemInviteStatus.Revoked,
            _ => RedeemInviteStatus.Unspecified
        };

    private static RevokeInviteStatus MapRevokeStatus(Domain.RevokeInviteStatus status) =>
        status switch
        {
            Domain.RevokeInviteStatus.Succeeded => RevokeInviteStatus.Succeeded,
            Domain.RevokeInviteStatus.NotFound => RevokeInviteStatus.NotFound,
            Domain.RevokeInviteStatus.Used => RevokeInviteStatus.Used,
            Domain.RevokeInviteStatus.Revoked => RevokeInviteStatus.Revoked,
            _ => RevokeInviteStatus.Unspecified
        };
}
