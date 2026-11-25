// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Randomness.Generation;

/// <summary>
/// When a shard reseeds: after <see cref="Interval"/> of time, after <see cref="MaxBytes"/> of output, or as soon as a
/// new beacon pulse is mixed in.
/// </summary>
internal sealed class ReseedPolicy(TimeSpan interval, long maxBytes)
{
    public TimeSpan Interval { get; } = interval;

    public long MaxBytes { get; } = maxBytes;

    public static ReseedPolicy From(RandomnessOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new(options.ReseedInterval, options.ReseedAfterBytes);
    }
}
