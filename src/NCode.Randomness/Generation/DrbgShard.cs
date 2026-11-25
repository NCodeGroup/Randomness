// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Binary;
using System.Security.Cryptography;
using NCode.Randomness.Diagnostics;
using NCode.Randomness.Entropy;

namespace NCode.Randomness.Generation;

/// <summary>
/// One HMAC_DRBG instance with a small output buffer, so a request for a few bytes costs a copy, not three HMACs.
/// Served bytes are zeroed in the buffer, and a reseed discards whatever the buffer still holds, so no byte produced
/// before a reseed is served after it. Callers hold <see cref="Gate"/>.
/// </summary>
internal sealed class DrbgShard : IDisposable
{
    public const int BufferLength = 512;
    private const int EntropyLength = 64;
    private const int NonceLength = 32;
    private const int ContextLength =
        25 + sizeof(int) + sizeof(long) + BeaconEntropySnapshot.DigestLength;

    private static ReadOnlySpan<byte> Personalization => "NCode.Randomness/secure/1"u8;

    public DrbgShard(int index, IEntropyInput entropy, IBeaconEntropy beacon, TimeProvider time)
    {
        Index = index;
        Entropy = entropy;
        Beacon = beacon;
        Time = time;
        Buffer = GC.AllocateArray<byte>(BufferLength, pinned: true);
        var snapshot = beacon.Current;
        Span<byte> seed = stackalloc byte[EntropyLength + NonceLength];
        Span<byte> personalization = stackalloc byte[ContextLength];
        try
        {
            entropy.Fill(seed);
            WriteContext(personalization, index, snapshot);
            Drbg = HmacDrbg.Instantiate(
                seed[..EntropyLength],
                seed[EntropyLength..],
                personalization
            );
        }
        finally
        {
            CryptographicOperations.ZeroMemory(seed);
        }

        Epoch = snapshot.Epoch;
        SeededAt = time.GetTimestamp();
    }

    public Lock Gate { get; } = new();

    private int Index { get; }

    private IEntropyInput Entropy { get; }

    private IBeaconEntropy Beacon { get; }

    private TimeProvider Time { get; }

    private HmacDrbg Drbg { get; }

    private byte[] Buffer { get; }

    private int Position { get; set; } = BufferLength;

    private long Epoch { get; set; }

    private long SeededAt { get; set; }

    private long BytesSinceSeed { get; set; }

    /// <summary>
    /// Fills <paramref name="destination"/>, reseeding first when the policy says so, and returns the reason it
    /// reseeded, or <see langword="null"/>.
    /// </summary>
    public string? Fill(Span<byte> destination, ReseedPolicy policy)
    {
        var reason = ReseedIfDue(policy);
        BytesSinceSeed += destination.Length;
        if (destination.Length >= BufferLength)
        {
            GenerateDirect(destination);
            return reason;
        }

        while (!destination.IsEmpty)
        {
            if (Position == BufferLength)
            {
                Drbg.Generate(Buffer, []);
                Position = 0;
            }

            var count = Math.Min(destination.Length, BufferLength - Position);
            var served = Buffer.AsSpan(Position, count);
            served.CopyTo(destination);
            CryptographicOperations.ZeroMemory(served);
            Position += count;
            destination = destination[count..];
        }

        return reason;
    }

    public void Dispose()
    {
        CryptographicOperations.ZeroMemory(Buffer);
        Drbg.Dispose();
    }

    private static void WriteContext(
        Span<byte> destination,
        int index,
        BeaconEntropySnapshot snapshot
    )
    {
        Personalization.CopyTo(destination);
        var rest = destination[Personalization.Length..];
        BinaryPrimitives.WriteInt32BigEndian(rest, index);
        BinaryPrimitives.WriteInt64BigEndian(rest[sizeof(int)..], snapshot.Epoch);
        snapshot.Digest.CopyTo(rest[(sizeof(int) + sizeof(long))..]);
    }

    private void GenerateDirect(Span<byte> destination)
    {
        while (!destination.IsEmpty)
        {
            var count = Math.Min(destination.Length, HmacDrbg.MaxBytesPerRequest);
            Drbg.Generate(destination[..count], []);
            destination = destination[count..];
        }
    }

    private string? ReseedIfDue(ReseedPolicy policy)
    {
        var snapshot = Beacon.Current;
        string? reason = null;
        if (snapshot.Epoch != Epoch)
        {
            reason = ReseedReasons.Beacon;
        }
        else if (Time.GetElapsedTime(SeededAt) >= policy.Interval)
        {
            reason = ReseedReasons.Interval;
        }
        else if (BytesSinceSeed >= policy.MaxBytes)
        {
            reason = ReseedReasons.Volume;
        }

        if (reason is not null)
        {
            Reseed(snapshot);
        }

        return reason;
    }

    private void Reseed(BeaconEntropySnapshot snapshot)
    {
        Span<byte> entropy = stackalloc byte[EntropyLength];
        Span<byte> additional =
            stackalloc byte[sizeof(int) + sizeof(long) + BeaconEntropySnapshot.DigestLength];
        try
        {
            Entropy.Fill(entropy);
            BinaryPrimitives.WriteInt32BigEndian(additional, Index);
            BinaryPrimitives.WriteInt64BigEndian(additional[sizeof(int)..], snapshot.Epoch);
            snapshot.Digest.CopyTo(additional[(sizeof(int) + sizeof(long))..]);
            Drbg.Reseed(entropy, additional);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(entropy);
        }

        CryptographicOperations.ZeroMemory(Buffer);
        Position = BufferLength;
        Epoch = snapshot.Epoch;
        SeededAt = Time.GetTimestamp();
        BytesSinceSeed = 0;
    }
}
