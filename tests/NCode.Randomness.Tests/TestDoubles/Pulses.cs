// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Randomness.Tests.TestDoubles;

internal static class Pulses
{
    public static DateTimeOffset Epoch { get; } = new(2026, 11, 25, 0, 0, 0, TimeSpan.Zero);

    public static BeaconPulse Create(
        string beacon,
        long round,
        DateTimeOffset timestamp,
        string? id = null,
        string chain = "chain-1"
    )
    {
        var randomness = new byte[64];
        randomness.AsSpan().Fill((byte)round);
        return new BeaconPulse(beacon, chain, round, id ?? $"pulse-{round}", timestamp, randomness);
    }
}
