// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Randomness.Tests.TestDoubles;

namespace NCode.Randomness.Tests.Draws;

public class VerifiableRandomTests
{
    // CURBy-RNG round 1620423, fetched from https://api.entwine.me on 2026-11-25; its randomness is the SHA3-512 digest
    // in its content identifier. The expected values come from the Python reference implementation in docs/draws.md,
    // which shares no code with this library.
    private const string Chain =
        "bafyriqci6f3st2mg7gq733ho4zvvth32zpy2mtiylixwmhoz6d627eo3jfpmbxepe54u2zdvymonq5sp3armtm4rodxsynsirr5g3xsbd3q4s";
    private const string PulseId =
        "bafyriqbtxzd22eybdovubearwfjyzlyowyhxqdkbzyboftamh6rhob2jafopfz4h2nf3eec4nawu2gil2vpgn57qilrlh23piwyozlugpmcvc";
    private const string Randomness =
        "33be47ad13011bab409011b1538caf0eb60f780d41ce02e2cc0c3fa2770749015cf2e787d34bb2105c682d4d190bd55e66f7f042e2b3eb6f45b0ecae867b0551";
    private const string Context = "example-raffle-2026-11-25";

    #region Create Tests

    [Fact]
    public void Create_WithReferencePulse_MatchesTheReferenceBytes()
    {
        using var draw = VerifiableRandom.Create(CreateReferencePulse(), Context);

        var bytes = draw.GetBytes(32);

        Assert.Equal(
            "e43e360c9e2b9cd6a094e04a64e65922a3581ba6864da1cb1490244522f9986e",
            Convert.ToHexStringLower(bytes)
        );
    }

    [Fact]
    public void Create_WithReferencePulse_MatchesTheReferenceIntegersAndShuffle()
    {
        using var draw = VerifiableRandom.Create(CreateReferencePulse(), Context);
        draw.GetBytes(32);
        int[] values = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];

        int[] rolls = [.. Enumerable.Range(0, 5).Select(_ => draw.NextInteger(1, 101))];
        draw.Shuffle<int>(values);

        Assert.Equal([80, 38, 69, 95, 91], rolls);
        Assert.Equal([10, 2, 8, 1, 4, 9, 5, 3, 6, 7], values);
    }

    [Fact]
    public void Create_WhenReadInDifferentChunks_GivesTheSameStream()
    {
        using var whole = VerifiableRandom.Create(CreateReferencePulse(), Context);
        using var pieces = VerifiableRandom.Create(CreateReferencePulse(), Context);
        var expected = whole.GetBytes(200);

        var actual = new byte[200];
        pieces.Fill(actual.AsSpan(0, 1));
        pieces.Fill(actual.AsSpan(1, 70));
        pieces.Fill(actual.AsSpan(71, 129));

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Create_WithAnotherContext_GivesAnIndependentStream()
    {
        using var first = VerifiableRandom.Create(CreateReferencePulse(), Context);
        using var second = VerifiableRandom.Create(CreateReferencePulse(), Context + "-2");

        Assert.NotEqual(first.GetBytes(32), second.GetBytes(32));
    }

    [Fact]
    public void Create_WithAnotherPulseIdentity_GivesAnIndependentStream()
    {
        var pulse = CreateReferencePulse();
        var relabelled = new BeaconPulse(
            pulse.Beacon,
            pulse.Chain,
            pulse.Round + 1,
            pulse.Id,
            pulse.Timestamp,
            pulse.Randomness
        );
        using var first = VerifiableRandom.Create(pulse, Context);
        using var second = VerifiableRandom.Create(relabelled, Context);

        Assert.NotEqual(first.GetBytes(32), second.GetBytes(32));
    }

    [Fact]
    public void Create_Always_ExposesThePulseContextAndAlgorithm()
    {
        var pulse = CreateReferencePulse();

        using var draw = VerifiableRandom.Create(pulse, Context);

        Assert.Same(pulse, draw.Pulse);
        Assert.Equal(Context, draw.Context);
        Assert.Equal("ncode-randomness-draw/1", VerifiableRandom.Algorithm);
    }

    [Fact]
    public void Create_WithEmptyContext_Throws()
    {
        Assert.Throws<ArgumentException>(() => VerifiableRandom.Create(CreateReferencePulse(), ""));
    }

    [Fact]
    public void Create_WithTooLongContext_Throws()
    {
        var context = new string('x', VerifiableRandom.MaxContextLength + 1);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            VerifiableRandom.Create(CreateReferencePulse(), context)
        );
    }

    [Fact]
    public void Create_IsNotASecureRandom()
    {
        using var draw = VerifiableRandom.Create(CreateReferencePulse(), Context);

        Assert.IsNotAssignableFrom<ISecureRandom>(draw);
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_ThenFill_Throws()
    {
        var draw = VerifiableRandom.Create(CreateReferencePulse(), Context);

        draw.Dispose();

        Assert.Throws<ObjectDisposedException>(() => draw.GetBytes(1));
    }

    [Fact]
    public void Dispose_WithBytesStillBuffered_ThrowsInsteadOfServingZeros()
    {
        var draw = VerifiableRandom.Create(CreateReferencePulse(), Context);
        draw.GetBytes(1);

        draw.Dispose();

        Assert.Throws<ObjectDisposedException>(() => draw.GetBytes(1));
    }

    #endregion

    internal static BeaconPulse CreateReferencePulse() =>
        new("curby", Chain, 1620423, PulseId, Pulses.Epoch, Convert.FromHexString(Randomness));
}
