// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Numerics;
using Microsoft.Extensions.Options;
using NCode.Randomness.Diagnostics;
using NCode.Randomness.Entropy;

namespace NCode.Randomness.Generation;

/// <summary>
/// The hybrid secure generator: one <see cref="DrbgShard"/> per processor, picked by thread, each seeded and reseeded
/// from the operating system's entropy and diversified by every beacon pulse mixed in. Its bytes are at least as
/// unpredictable as the operating system's generator, whatever a beacon publishes, and a call never waits on the
/// network.
/// </summary>
internal sealed class DefaultSecureRandom : ISecureRandom, IDisposable
{
    private const int MaxShards = 64;

    public DefaultSecureRandom(
        IEntropyInput entropy,
        IBeaconEntropy beacon,
        IOptions<RandomnessOptions> options,
        TimeProvider time,
        RandomnessMetrics metrics
    )
    {
        ArgumentNullException.ThrowIfNull(options);
        Policy = ReseedPolicy.From(options.Value);
        Metrics = metrics;
        var count = (int)
            BitOperations.RoundUpToPowerOf2(
                (uint)Math.Clamp(Environment.ProcessorCount, 1, MaxShards)
            );
        Mask = count - 1;
        var shards = ImmutableArray.CreateBuilder<DrbgShard>(count);
        for (var index = 0; index < count; index++)
        {
            shards.Add(new DrbgShard(index, entropy, beacon, time));
        }

        Shards = shards.MoveToImmutable();
    }

    private ReseedPolicy Policy { get; }

    private RandomnessMetrics Metrics { get; }

    private int Mask { get; }

    private ImmutableArray<DrbgShard> Shards { get; }

    private bool IsDisposed { get; set; }

    public void Fill(Span<byte> destination)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (destination.IsEmpty)
        {
            return;
        }

        var shard = Shards[Environment.CurrentManagedThreadId & Mask];
        string? reason;
        lock (shard.Gate)
        {
            reason = shard.Fill(destination, Policy);
        }

        if (reason is not null)
        {
            Metrics.RecordReseed(reason);
        }
    }

    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        IsDisposed = true;
        foreach (var shard in Shards)
        {
            lock (shard.Gate)
            {
                shard.Dispose();
            }
        }
    }
}
