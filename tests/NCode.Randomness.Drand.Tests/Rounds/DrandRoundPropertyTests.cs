// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using FsCheck;
using FsCheck.Xunit;
using NCode.Randomness.Drand.Rounds;
using NCode.Randomness.Drand.Tests.Fixtures;

namespace NCode.Randomness.Drand.Tests.Rounds;

public class DrandRoundPropertyTests
{
    private const string Replay = "20261125,1";

    #region Verify Tests

    [Property(Replay = Replay)]
    public void Verify_WithAnyBytes_ThrowsDrandFormatException(NonNull<byte[]> bytes)
    {
        Assert.Throws<DrandFormatException>(() =>
            DrandRound.Verify(bytes.Get, DrandChain.Quicknet)
        );
    }

    [Property(Replay = Replay, MaxTest = 500)]
    public void Verify_WithOneByteChanged_ThrowsDrandFormatExceptionOrKeepsTheRandomness(
        NonNegativeInt position,
        byte value
    )
    {
        var json = DrandFixtures.Round(DrandFixtures.LatestRound);
        var bytes = Encoding.UTF8.GetBytes(json);
        bytes[position.Get % bytes.Length] = value;

        DrandRound round;
        try
        {
            round = DrandRound.Verify(bytes, DrandChain.Quicknet);
        }
        catch (DrandFormatException)
        {
            return;
        }

        var randomness = Convert.ToHexStringLower(round.Randomness);

        Assert.Equal(DrandFixtures.LatestRandomness, randomness);
    }

    #endregion
}
