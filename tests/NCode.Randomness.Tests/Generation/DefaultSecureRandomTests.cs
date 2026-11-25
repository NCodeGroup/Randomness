// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NCode.Randomness.Diagnostics;
using NCode.Randomness.Entropy;
using NCode.Randomness.Generation;
using NCode.Randomness.Tests.TestDoubles;

namespace NCode.Randomness.Tests.Generation;

public sealed class DefaultSecureRandomTests : IDisposable
{
    private Telemetry Telemetry { get; } = new();

    private FakeTimeProvider Time { get; } = new(Pulses.Epoch);

    private BeaconEntropyPool Pool { get; } = new();

    private CountingEntropyInput Entropy { get; } = new();

    public void Dispose() => Telemetry.Dispose();

    #region Fill Tests

    [Fact]
    public void Fill_WithEmptyBuffer_DrawsNothing()
    {
        using var random = Create();
        var calls = Entropy.Calls;

        random.Fill([]);

        Assert.Equal(calls, Entropy.Calls);
    }

    [Fact]
    public void Fill_OnRepeatedCalls_NeverRepeatsABlock()
    {
        using var random = Create();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < 1000; i++)
        {
            Assert.True(seen.Add(Convert.ToHexString(random.GetBytes(16))));
        }
    }

    [Fact]
    public void Fill_WithSameEntropy_IsReproducible()
    {
        using var first = Create();
        using var second = new DefaultSecureRandom(
            new CountingEntropyInput(),
            new BeaconEntropyPool(),
            Options(),
            Time,
            Telemetry.Metrics
        );

        Assert.Equal(first.GetBytes(100), second.GetBytes(100));
    }

    [Fact]
    public void Fill_WithBeaconEntropy_DiffersFromWithout()
    {
        var mixed = new BeaconEntropyPool();
        mixed.Mix(Pulses.Create("test", 1, Pulses.Epoch));
        using var plain = Create();
        using var diversified = new DefaultSecureRandom(
            new CountingEntropyInput(),
            mixed,
            Options(),
            Time,
            Telemetry.Metrics
        );

        Assert.NotEqual(plain.GetBytes(64), diversified.GetBytes(64));
    }

    [Fact]
    public void Fill_AfterPulseIsMixed_ReseedsForTheBeacon()
    {
        using var reseeds = CreateReseedCollector();
        using var random = Create();
        random.GetBytes(8);
        var calls = Entropy.Calls;

        Pool.Mix(Pulses.Create("test", 1, Pulses.Epoch));
        random.GetBytes(8);

        Assert.Equal(calls + 1, Entropy.Calls);
        AssertReseed(reseeds, ReseedReasons.Beacon);
    }

    [Fact]
    public void Fill_AfterReseed_DiscardsBufferedBytes()
    {
        using var reference = Create();
        using var reseeded = new DefaultSecureRandom(
            new CountingEntropyInput(),
            Pool,
            Options(),
            Time,
            Telemetry.Metrics
        );
        var expected = reference.GetBytes(16);
        reference.GetBytes(16);
        var buffered = reference.GetBytes(16);
        Assert.Equal(expected, reseeded.GetBytes(16));

        Pool.Mix(Pulses.Create("test", 1, Pulses.Epoch));
        reseeded.GetBytes(16);

        Assert.NotEqual(buffered, reseeded.GetBytes(16));
    }

    [Fact]
    public void Fill_AfterInterval_ReseedsForTheInterval()
    {
        using var reseeds = CreateReseedCollector();
        using var random = Create();
        random.GetBytes(8);

        Time.Advance(TimeSpan.FromMinutes(1));
        random.GetBytes(8);

        AssertReseed(reseeds, ReseedReasons.Interval);
    }

    [Fact]
    public void Fill_BeforeInterval_DoesNotReseed()
    {
        using var reseeds = CreateReseedCollector();
        using var random = Create();
        random.GetBytes(8);

        Time.Advance(TimeSpan.FromSeconds(59));
        random.GetBytes(8);

        Assert.Empty(reseeds.GetMeasurementSnapshot());
    }

    [Fact]
    public void Fill_AfterVolume_ReseedsForTheVolume()
    {
        using var reseeds = CreateReseedCollector();
        using var random = Create(options => options.ReseedAfterBytes = 100);

        random.GetBytes(64);
        random.GetBytes(64);
        Assert.Empty(reseeds.GetMeasurementSnapshot());
        random.GetBytes(1);

        AssertReseed(reseeds, ReseedReasons.Volume);
    }

    [Fact]
    public void Fill_WithRequestLargerThanOneGenerate_FillsEveryByte()
    {
        using var random = Create(options => options.ReseedAfterBytes = int.MaxValue);
        var bytes = new byte[(3 * HmacDrbg.MaxBytesPerRequest) + 17];

        random.Fill(bytes);

        Assert.Contains(bytes[^17..], value => value != 0);
        Assert.NotEqual(
            bytes[..64],
            bytes[HmacDrbg.MaxBytesPerRequest..(HmacDrbg.MaxBytesPerRequest + 64)]
        );
    }

    [Fact]
    public void Fill_FromManyThreads_Succeeds()
    {
        using var random = Create();

        Parallel.For(0, 64, _ => random.GetBytes(DrbgShard.BufferLength + 3));

        Assert.NotEmpty(random.GetBytes(1));
    }

    [Fact]
    public void Fill_WhenDisposed_Throws()
    {
        var random = Create();
        random.Dispose();
        random.Dispose();

        Assert.Throws<ObjectDisposedException>(() => random.GetBytes(1));
    }

    #endregion

    private static IOptions<RandomnessOptions> Options(Action<RandomnessOptions>? configure = null)
    {
        var options = new RandomnessOptions();
        configure?.Invoke(options);
        return Microsoft.Extensions.Options.Options.Create(options);
    }

    private static void AssertReseed(MetricCollector<long> reseeds, string reason)
    {
        var measurement = Assert.Single(reseeds.GetMeasurementSnapshot());
        Assert.Equal(reason, measurement.Tags[RandomnessMetrics.ReasonTag]);
    }

    private DefaultSecureRandom Create(Action<RandomnessOptions>? configure = null) =>
        new(Entropy, Pool, Options(configure), Time, Telemetry.Metrics);

    private MetricCollector<long> CreateReseedCollector() =>
        new(
            Telemetry.MeterFactory,
            RandomnessDiagnostics.MeterName,
            "ncode.randomness.generator.reseeds"
        );
}
