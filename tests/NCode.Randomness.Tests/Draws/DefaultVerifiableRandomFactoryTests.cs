// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Moq;
using NCode.Randomness.Draws;
using NCode.Randomness.Tests.TestDoubles;

namespace NCode.Randomness.Tests.Draws;

public class DefaultVerifiableRandomFactoryTests : UnitTestBase
{
    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_WithRegisteredBeacon_DerivesTheDrawFromTheRound()
    {
        var pulse = VerifiableRandomTests.CreateReferencePulse();
        var beacon = CreateBeacon("curby");
        beacon
            .Setup(x => x.GetPulseAsync(pulse.Round, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pulse)
            .Verifiable();
        var factory = new DefaultVerifiableRandomFactory([beacon.Object]);

        using var draw = await factory.CreateAsync(
            "curby",
            pulse.Round,
            "raffle",
            CancellationToken.None
        );

        Assert.Same(pulse, draw.Pulse);
        Assert.Equal("raffle", draw.Context);
    }

    [Fact]
    public async Task CreateAsync_WhenBeaconAnswersAnotherRound_Throws()
    {
        var pulse = Pulses.Create("curby", 9, Pulses.Epoch);
        var beacon = CreateBeacon("curby");
        beacon
            .Setup(x => x.GetPulseAsync(8, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pulse)
            .Verifiable();
        var factory = new DefaultVerifiableRandomFactory([beacon.Object]);

        var exception = await Assert.ThrowsAsync<BeaconException>(async () =>
            await factory.CreateAsync("curby", 8, "raffle", CancellationToken.None)
        );

        Assert.Equal(BeaconError.InvalidPulse, exception.Error);
        Assert.Equal("curby", exception.Beacon);
    }

    [Fact]
    public async Task CreateAsync_WhenBeaconIsNotRegistered_Throws()
    {
        var factory = new DefaultVerifiableRandomFactory([CreateBeacon("curby").Object]);

        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await factory.CreateAsync("nist", 1, "raffle", CancellationToken.None)
        );

        Assert.Equal("beacon", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithDuplicateNames_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new DefaultVerifiableRandomFactory([
                CreateBeacon("curby").Object,
                CreateBeacon("curby").Object,
            ])
        );

        Assert.Contains("'curby'", exception.Message, StringComparison.Ordinal);
    }

    #endregion

    private Mock<IRandomnessBeacon> CreateBeacon(string name)
    {
        var beacon = CreateStrictMock<IRandomnessBeacon>();
        beacon.Setup(x => x.Name).Returns(name).Verifiable();
        return beacon;
    }
}
