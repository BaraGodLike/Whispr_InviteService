using Application.Abstractions;
using Application.Options;
using Application.Services;
using Infrastructure.Security;
using Infrastructure.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Migrator;
using Testcontainers.PostgreSql;

namespace Tests.Integration;

[TestClass]
public sealed class InviteApplicationServiceIntegrationTests
{
    [TestMethod]
    public async Task CreateAndRedeemInvite_RoundTripsServerSecret()
    {
        await using var harness = await InviteTestHarness.CreateAsync();

        var created = await harness.Service.CreateInviteAsync(CancellationToken.None);
        var challenge = await harness.Service.GetRedeemChallengeAsync(created.InviteId, CancellationToken.None);
        var solution = harness.SolveProofOfWork(created.InviteId, created.Pin, challenge.Nonce!, challenge.Attempts);

        var redeemed = await harness.Service.RedeemInviteAsync(
            created.InviteId,
            created.Pin,
            challenge.Nonce!,
            solution,
            CancellationToken.None);

        CollectionAssert.AreEqual(created.ServerSecret, redeemed.ServerSecret);

        var afterRedeemChallenge = await harness.Service.GetRedeemChallengeAsync(
            created.InviteId,
            CancellationToken.None);

        Assert.AreEqual(Domain.RedeemInviteStatus.Succeeded, redeemed.Status);
        Assert.AreEqual(Domain.RedeemChallengeStatus.Used, afterRedeemChallenge.Status);
    }

    [TestMethod]
    public async Task InvalidPin_AccumulatesAttemptsAndLocksAfterThirdFailure()
    {
        await using var harness = await InviteTestHarness.CreateAsync();

        var created = await harness.Service.CreateInviteAsync(CancellationToken.None);

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            var challenge = await harness.Service.GetRedeemChallengeAsync(created.InviteId, CancellationToken.None);
            var solution = harness.SolveProofOfWork(created.InviteId, "WRONGPIN", challenge.Nonce!, challenge.Attempts);
            var result = await harness.Service.RedeemInviteAsync(
                created.InviteId,
                "WRONGPIN",
                challenge.Nonce!,
                solution,
                CancellationToken.None);

            Assert.AreEqual(Domain.RedeemInviteStatus.InvalidPin, result.Status);

            if (attempt < 3)
            {
                Assert.IsNull(result.LockedUntilUtc);
            }
            else
            {
                Assert.AreEqual(harness.DateTimeProvider.UtcNow.AddMinutes(2), result.LockedUntilUtc);
            }
        }

        var lockedChallenge = await harness.Service.GetRedeemChallengeAsync(created.InviteId, CancellationToken.None);

        Assert.AreEqual(Domain.RedeemChallengeStatus.Locked, lockedChallenge.Status);
        Assert.AreEqual(3, lockedChallenge.Attempts);
    }

    [TestMethod]
    public async Task RevokeInvite_MarksInviteAsRevoked()
    {
        await using var harness = await InviteTestHarness.CreateAsync();

        var created = await harness.Service.CreateInviteAsync(CancellationToken.None);
        var revoked = await harness.Service.RevokeInviteAsync(
            created.InviteId,
            created.RevokeToken,
            CancellationToken.None);

        var challenge = await harness.Service.GetRedeemChallengeAsync(created.InviteId, CancellationToken.None);
        var revokedAgain = await harness.Service.RevokeInviteAsync(
            created.InviteId,
            created.RevokeToken,
            CancellationToken.None);

        Assert.AreEqual(Domain.RevokeInviteStatus.Succeeded, revoked.Status);
        Assert.AreEqual(Domain.RedeemChallengeStatus.Revoked, challenge.Status);
        Assert.AreEqual(Domain.RevokeInviteStatus.Revoked, revokedAgain.Status);
    }

    private sealed class InviteTestHarness : IAsyncDisposable
    {
        private readonly PostgreSqlContainer _container;
        private readonly HmacPowService _powService;

        private InviteTestHarness(
            PostgreSqlContainer container,
            InviteApplicationService service,
            FakeDateTimeProvider dateTimeProvider,
            HmacPowService powService)
        {
            _container = container;
            Service = service;
            DateTimeProvider = dateTimeProvider;
            _powService = powService;
        }

        public FakeDateTimeProvider DateTimeProvider { get; }

        public InviteApplicationService Service { get; }

        public static async Task<InviteTestHarness> CreateAsync()
        {
            var container = new PostgreSqlBuilder()
                .WithImage("postgres:16-alpine")
                .WithDatabase("invites")
                .WithUsername("postgres")
                .WithPassword("postgres")
                .Build();

            await container.StartAsync();

            var connectionFactory = new PostgresConnectionFactory(container.GetConnectionString());
            await MigrationRunnerExecutor.MigrateUpAsync(container.GetConnectionString(), CancellationToken.None);

            var dateTimeProvider = new FakeDateTimeProvider
            {
                UtcNow = new DateTime(2026, 5, 18, 12, 0, 0, DateTimeKind.Utc)
            };

            var inviteOptions = new InviteOptions();
            var lockoutOptions = new LockoutOptions();
            var powOptions = new PowOptions
            {
                BaseDifficulty = 4,
                MaxDifficulty = 8,
                ChallengeTtl = TimeSpan.FromMinutes(2),
                BucketDuration = TimeSpan.FromMinutes(1),
                SigningKeyBase64 = "QEFCQ0RFRkdISUpLTE1OT1BRUlNUVVZXWFlaW1xdXl8="
            };

            var encryptionOptions = new EncryptionOptions
            {
                KeyBase64 = "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8="
            };

            var hashingOptions = new HashingOptions
            {
                KeyBase64 = "ICEiIyQlJicoKSorLC0uLzAxMjM0NTY3ODk6Ozw9Pj8="
            };

            IInviteRepository repository = new PostgresInviteRepository(connectionFactory);
            IInviteHasher hasher = new HmacInviteHasher(hashingOptions);
            var protector = new AesGcmSecretProtector(encryptionOptions);
            var powService = new HmacPowService(powOptions);
            var service = new InviteApplicationService(
                repository,
                hasher,
                protector,
                powService,
                dateTimeProvider,
                inviteOptions,
                lockoutOptions);

            return new InviteTestHarness(container, service, dateTimeProvider, powService);
        }

        public ulong SolveProofOfWork(Guid inviteId, string pin, string nonce, int attempts)
        {
            var normalizedPin = pin.ToUpperInvariant();

            for (ulong solution = 0; solution < ulong.MaxValue; solution++)
            {
                try
                {
                    _powService.ValidateChallenge(
                        inviteId,
                        attempts,
                        normalizedPin,
                        nonce,
                        solution,
                        DateTimeProvider.UtcNow);

                    return solution;
                }
                catch (Application.Exceptions.InsufficientProofOfWorkException)
                {
                }
            }

            throw new InvalidOperationException("Unable to solve the proof of work challenge.");
        }

        public async ValueTask DisposeAsync()
        {
            await _container.DisposeAsync();
        }
    }
}
