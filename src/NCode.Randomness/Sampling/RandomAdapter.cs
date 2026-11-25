// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Randomness.Sampling;

/// <summary>
/// Presents an <see cref="IRandomSource"/> as a <see cref="Random"/>, for APIs that take one. Every member draws from
/// the source through <see cref="UniformSampler"/>, so the values are unbiased and as secret as the source's bytes.
/// </summary>
internal sealed class RandomAdapter(IRandomSource source) : Random
{
    public IRandomSource Source { get; } = source;

    public override int Next() => UniformSampler.NextInteger(Source, 0, int.MaxValue);

    public override int Next(int maxValue)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxValue);
        return maxValue == 0 ? 0 : UniformSampler.NextInteger(Source, 0, maxValue);
    }

    public override int Next(int minValue, int maxValue)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minValue, maxValue);
        return minValue == maxValue
            ? minValue
            : UniformSampler.NextInteger(Source, minValue, maxValue);
    }

    public override long NextInt64() => UniformSampler.NextInteger(Source, 0L, long.MaxValue);

    public override long NextInt64(long maxValue)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxValue);
        return maxValue == 0 ? 0 : UniformSampler.NextInteger(Source, 0L, maxValue);
    }

    public override long NextInt64(long minValue, long maxValue)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minValue, maxValue);
        return minValue == maxValue
            ? minValue
            : UniformSampler.NextInteger(Source, minValue, maxValue);
    }

    public override double NextDouble() => UniformSampler.NextDouble(Source);

    public override float NextSingle() => UniformSampler.NextSingle(Source);

    public override void NextBytes(byte[] buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        Source.Fill(buffer);
    }

    public override void NextBytes(Span<byte> buffer) => Source.Fill(buffer);

    protected override double Sample() => UniformSampler.NextDouble(Source);
}
