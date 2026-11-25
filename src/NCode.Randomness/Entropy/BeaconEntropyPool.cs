// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace NCode.Randomness.Entropy;

/// <summary>
/// Accumulates accepted pulses into one digest: D(n) = SHA-512(label || D(n-1) || pulse), with every field of the
/// pulse length-prefixed so no two pulses encode alike. The digest is public data; it only diversifies the generator.
/// </summary>
internal sealed class BeaconEntropyPool : IBeaconEntropy
{
    private static ReadOnlySpan<byte> Label => "NCode.Randomness/beacon-pool/1"u8;

    private BeaconEntropySnapshot _current = BeaconEntropySnapshot.Empty;

    private Lock Gate { get; } = new();

    public BeaconEntropySnapshot Current => Volatile.Read(ref _current);

    public BeaconEntropySnapshot Mix(BeaconPulse pulse)
    {
        ArgumentNullException.ThrowIfNull(pulse);
        lock (Gate)
        {
            var previous = _current;
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA512);
            hash.AppendData(Label);
            hash.AppendData(previous.Digest);
            AppendText(hash, pulse.Beacon);
            AppendText(hash, pulse.Chain);
            Span<byte> round = stackalloc byte[sizeof(long)];
            BinaryPrimitives.WriteInt64BigEndian(round, pulse.Round);
            hash.AppendData(round);
            AppendText(hash, pulse.Id);
            AppendBytes(hash, pulse.Randomness);
            var digest = hash.GetHashAndReset();
            var next = new BeaconEntropySnapshot(previous.Epoch + 1, digest);
            Volatile.Write(ref _current, next);
            return next;
        }
    }

    private static void AppendText(IncrementalHash hash, string value) =>
        AppendBytes(hash, Encoding.UTF8.GetBytes(value));

    private static void AppendBytes(IncrementalHash hash, ReadOnlySpan<byte> value)
    {
        Span<byte> length = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(length, value.Length);
        hash.AppendData(length);
        hash.AppendData(value);
    }
}
