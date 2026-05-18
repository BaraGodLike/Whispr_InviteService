using Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.Unit;

[TestClass]
public sealed class InviteTests
{
    [TestMethod]
    public void GetCurrentDifficulty_CapsAtConfiguredMaximum()
    {
        var invite = new Invite { Attempts = 12 };

        var difficulty = invite.GetCurrentDifficulty(18, 26);

        Assert.AreEqual(26, difficulty);
    }

    [TestMethod]
    public void GetFailedRedeemState_AppliesFirstLockAtThirdAttempt()
    {
        var nowUtc = new DateTime(2026, 5, 18, 10, 0, 0, DateTimeKind.Utc);
        var invite = new Invite { Attempts = 2 };

        var state = invite.GetFailedRedeemState(
            nowUtc,
            firstLockThreshold: 3,
            firstLockDuration: TimeSpan.FromMinutes(2),
            secondLockThreshold: 5,
            secondLockDuration: TimeSpan.FromMinutes(5));

        Assert.AreEqual(3, state.Attempts);
        Assert.AreEqual(nowUtc.AddMinutes(2), state.LockedUntilUtc);
    }

    [TestMethod]
    public void GetFailedRedeemState_AppliesSecondLockAtFifthAttempt()
    {
        var nowUtc = new DateTime(2026, 5, 18, 10, 0, 0, DateTimeKind.Utc);
        var invite = new Invite { Attempts = 4 };

        var state = invite.GetFailedRedeemState(
            nowUtc,
            firstLockThreshold: 3,
            firstLockDuration: TimeSpan.FromMinutes(2),
            secondLockThreshold: 5,
            secondLockDuration: TimeSpan.FromMinutes(5));

        Assert.AreEqual(5, state.Attempts);
        Assert.AreEqual(nowUtc.AddMinutes(5), state.LockedUntilUtc);
    }
}
