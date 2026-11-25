// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using NCode.Randomness.Draws;
using NCode.Randomness.Generation;

namespace NCode.Randomness;

/// <summary>
/// A reproducible random stream derived from one verified beacon pulse and a context label: a draw that anyone can
/// check, such as a lottery, a raffle, an audit sample or a jury selection.
/// </summary>
/// <remarks>
/// <para>
/// The stream is HMAC_DRBG (HMAC-SHA-512, NIST SP 800-90A) instantiated with the pulse's randomness as its entropy
/// input, the pulse's identity as its nonce and the context as its personalization string, read as consecutive
/// 64-byte blocks. With the typed members of <see cref="RandomSourceExtensions"/>, the same pulse, context and calls
/// give the same values on every machine, for every version that names the same <see cref="Algorithm"/>.
/// </para>
/// <para>
/// Its bytes are public to everyone who reads the pulse, so it is deliberately not an <see cref="ISecureRandom"/>:
/// never derive a secret from it. For a draw to be fair, publish the round and the context before the beacon publishes
/// that round, so no one can pick a pulse that favors them.
/// </para>
/// <para>
/// Consume it from one thread, in a fixed order: values depend on the order in which they are drawn.
/// </para>
/// </remarks>
public sealed class VerifiableRandom : IRandomSource, IDisposable
{
    /// <summary>
    /// The longest context, in characters.
    /// </summary>
    public const int MaxContextLength = 1024;

    private VerifiableRandom(BeaconPulse pulse, string context, HmacDrbg drbg)
    {
        Pulse = pulse;
        Context = context;
        Drbg = drbg;
    }

    /// <summary>
    /// Gets the name and version of the derivation and sampling algorithms, which docs/draws.md specifies:
    /// <c>ncode-randomness-draw/1</c>.
    /// </summary>
    public static string Algorithm => DrawSeed.Algorithm;

    /// <summary>
    /// Gets the pulse the stream is derived from.
    /// </summary>
    public BeaconPulse Pulse { get; }

    /// <summary>
    /// Gets the context label the stream is derived with.
    /// </summary>
    public string Context { get; }

    private HmacDrbg Drbg { get; }

    private byte[] Block { get; } = new byte[HmacDrbg.OutputLength];

    private int Position { get; set; } = HmacDrbg.OutputLength;

    private Lock Gate { get; } = new();

    private bool IsDisposed { get; set; }

    /// <summary>
    /// Derives the stream of <paramref name="pulse"/> for <paramref name="context"/>.
    /// </summary>
    /// <remarks>
    /// The draw is as trustworthy as the pulse: this method verifies nothing. Pass a pulse a beacon adapter returned,
    /// or use <see cref="IVerifiableRandomFactory.CreateAsync"/>, which fetches and verifies the round itself. To check
    /// a published draw without the network, pass the pulse a beacon adapter's offline verifier returns for the
    /// documents the draw published, such as the CURBy adapter's.
    /// </remarks>
    /// <param name="pulse">A verified pulse, such as one from <see cref="IRandomnessBeacon.GetPulseAsync"/>.</param>
    /// <param name="context">What the draw is for, published with the round before the draw, such as
    /// <c>"raffle-2026-12-01"</c>; at most <see cref="MaxContextLength"/> characters. Different contexts give
    /// independent streams from one pulse.</param>
    /// <returns>The stream.</returns>
    public static VerifiableRandom Create(BeaconPulse pulse, string context)
    {
        ArgumentNullException.ThrowIfNull(pulse);
        ArgumentException.ThrowIfNullOrEmpty(context);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            context.Length,
            MaxContextLength,
            nameof(context)
        );
        var nonce = DrawSeed.Nonce(pulse);
        var personalization = DrawSeed.Personalization(context);
        var drbg = HmacDrbg.Instantiate(pulse.Randomness, nonce, personalization);
        return new VerifiableRandom(pulse, context, drbg);
    }

    /// <inheritdoc />
    public void Fill(Span<byte> destination)
    {
        lock (Gate)
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            while (!destination.IsEmpty)
            {
                if (Position == HmacDrbg.OutputLength)
                {
                    Drbg.Generate(Block, []);
                    Position = 0;
                }

                var count = Math.Min(destination.Length, HmacDrbg.OutputLength - Position);
                Block.AsSpan(Position, count).CopyTo(destination);
                Position += count;
                destination = destination[count..];
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (Gate)
        {
            IsDisposed = true;
            CryptographicOperations.ZeroMemory(Block);
            Position = HmacDrbg.OutputLength;
            Drbg.Dispose();
        }
    }
}
