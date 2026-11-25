// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;

namespace NCode.Randomness.Generation;

/// <summary>
/// HMAC_DRBG with HMAC-SHA-512, as NIST SP 800-90A Rev. 1 section 10.1.2 specifies it, without prediction resistance
/// (the owner reseeds). Its security strength is 256 bits. Its key and value live in pinned arrays, so the garbage
/// collector never leaves a copy behind, and <see cref="Dispose"/> zeroes them; an input too long for the stack goes in
/// a pinned array of its own, zeroed after use. Not thread-safe: the owner serializes calls.
/// </summary>
internal sealed class HmacDrbg : IDisposable
{
    public const int OutputLength = 64;
    public const int MinEntropyLength = 32;
    public const int MaxBytesPerRequest = 1 << 16;
    private const long MaxRequestsBetweenReseeds = 1L << 48;
    private const int StackThreshold = 512;

    private HmacDrbg()
    {
        Key = GC.AllocateArray<byte>(OutputLength, pinned: true);
        Value = GC.AllocateArray<byte>(OutputLength, pinned: true);
    }

    private byte[] Key { get; }

    private byte[] Value { get; }

    /// <summary>
    /// Gets the number of generate requests since the last instantiate or reseed, plus one, as the specification
    /// counts it.
    /// </summary>
    public long ReseedCounter { get; private set; }

    private bool IsDisposed { get; set; }

    /// <summary>
    /// Instantiates a generator from <paramref name="entropy"/>, <paramref name="nonce"/> and an optional
    /// <paramref name="personalization"/> string.
    /// </summary>
    public static HmacDrbg Instantiate(
        ReadOnlySpan<byte> entropy,
        ReadOnlySpan<byte> nonce,
        ReadOnlySpan<byte> personalization
    )
    {
        ThrowIfTooLittleEntropy(entropy);
        var drbg = new HmacDrbg();
        drbg.Value.AsSpan().Fill(0x01);
        drbg.UpdateWithSeed(entropy, nonce, personalization);
        drbg.ReseedCounter = 1;
        return drbg;
    }

    /// <summary>
    /// Reseeds the generator with fresh <paramref name="entropy"/> and optional <paramref name="additionalInput"/>.
    /// </summary>
    public void Reseed(ReadOnlySpan<byte> entropy, ReadOnlySpan<byte> additionalInput)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        ThrowIfTooLittleEntropy(entropy);
        UpdateWithSeed(entropy, additionalInput, []);
        ReseedCounter = 1;
    }

    /// <summary>
    /// Fills <paramref name="output"/>, at most <see cref="MaxBytesPerRequest"/> bytes, then updates the state with
    /// <paramref name="additionalInput"/>, so the bytes cannot be recomputed from any later state.
    /// </summary>
    public void Generate(Span<byte> output, ReadOnlySpan<byte> additionalInput)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (output.Length > MaxBytesPerRequest)
        {
            throw new ArgumentOutOfRangeException(
                nameof(output),
                output.Length,
                $"A request is at most {MaxBytesPerRequest} bytes."
            );
        }

        if (ReseedCounter > MaxRequestsBetweenReseeds)
        {
            throw new CryptographicException(
                "The generator must be reseeded before it generates more output."
            );
        }

        if (!additionalInput.IsEmpty)
        {
            Update(additionalInput);
        }

        var remaining = output;
        while (!remaining.IsEmpty)
        {
            Mac(Value, Value);
            var count = Math.Min(remaining.Length, OutputLength);
            Value.AsSpan(0, count).CopyTo(remaining);
            remaining = remaining[count..];
        }

        Update(additionalInput);
        ReseedCounter++;
    }

    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        CryptographicOperations.ZeroMemory(Key);
        CryptographicOperations.ZeroMemory(Value);
        IsDisposed = true;
    }

    private static void ThrowIfTooLittleEntropy(ReadOnlySpan<byte> entropy)
    {
        if (entropy.Length < MinEntropyLength)
        {
            throw new ArgumentException(
                $"The entropy input must be at least {MinEntropyLength} bytes for a security strength of 256 bits.",
                nameof(entropy)
            );
        }
    }

    private void UpdateWithSeed(
        ReadOnlySpan<byte> first,
        ReadOnlySpan<byte> second,
        ReadOnlySpan<byte> third
    )
    {
        var length = first.Length + second.Length + third.Length;
        var seed =
            length <= StackThreshold
                ? stackalloc byte[StackThreshold]
                : GC.AllocateUninitializedArray<byte>(length, pinned: true);
        seed = seed[..length];
        try
        {
            first.CopyTo(seed);
            second.CopyTo(seed[first.Length..]);
            third.CopyTo(seed[(first.Length + second.Length)..]);
            Update(seed);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(seed);
        }
    }

    /// <summary>
    /// HMAC_DRBG_Update: K = HMAC(K, V || 0x00 || data), V = HMAC(K, V), and, when there is data, the same again with
    /// 0x01.
    /// </summary>
    private void Update(ReadOnlySpan<byte> providedData)
    {
        var length = OutputLength + 1 + providedData.Length;
        var message =
            length <= StackThreshold
                ? stackalloc byte[StackThreshold]
                : GC.AllocateUninitializedArray<byte>(length, pinned: true);
        message = message[..length];
        try
        {
            UpdateRound(message, 0x00, providedData);
            if (!providedData.IsEmpty)
            {
                UpdateRound(message, 0x01, providedData);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(message);
        }
    }

    private void UpdateRound(Span<byte> message, byte separator, ReadOnlySpan<byte> providedData)
    {
        Value.CopyTo(message);
        message[OutputLength] = separator;
        providedData.CopyTo(message[(OutputLength + 1)..]);
        Mac(message, Key);
        Mac(Value, Value);
    }

    /// <summary>
    /// Computes HMAC(K, <paramref name="message"/>) into <paramref name="destination"/> through a scratch block, since
    /// the destination may be the key or the message itself.
    /// </summary>
    private void Mac(ReadOnlySpan<byte> message, Span<byte> destination)
    {
        Span<byte> block = stackalloc byte[OutputLength];
        HMACSHA512.HashData(Key, message, block);
        block.CopyTo(destination);
        CryptographicOperations.ZeroMemory(block);
    }
}
