// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Randomness.Entropy;

/// <summary>
/// The beacon entropy mixed in so far: a digest over every accepted pulse, and an epoch that counts them.
/// </summary>
internal sealed class BeaconEntropySnapshot(long epoch, byte[] digest)
{
    public const int DigestLength = 64;

    public static BeaconEntropySnapshot Empty { get; } = new(0, new byte[DigestLength]);

    public long Epoch { get; } = epoch;

    public ReadOnlySpan<byte> Digest => DigestBytes;

    private byte[] DigestBytes { get; } = digest;
}
