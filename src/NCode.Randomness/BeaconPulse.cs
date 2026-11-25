// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Randomness;

/// <summary>
/// One verified output of a randomness beacon. A beacon adapter returns a pulse only after it has verified it as its
/// vendor's protocol specifies, so every pulse that reaches the library is authentic.
/// </summary>
/// <remarks>
/// A pulse's randomness is public: everyone who reads the beacon sees the same bytes. The secure generator only mixes it
/// in; it never uses it alone. A <see cref="VerifiableRandom"/> draw uses it alone on purpose, so others can reproduce
/// the result.
/// </remarks>
public sealed class BeaconPulse
{
    /// <summary>
    /// The fewest random bytes a pulse may carry: enough to seed a generator at a security strength of 256 bits.
    /// </summary>
    public const int MinRandomnessLength = 32;

    /// <summary>
    /// Initializes a pulse.
    /// </summary>
    /// <param name="beacon">The name the beacon is registered under, such as <c>curby</c>.</param>
    /// <param name="chain">The vendor's identifier of the sequence the pulse belongs to, such as a chain's content
    /// identifier.</param>
    /// <param name="round">The pulse's position in its chain.</param>
    /// <param name="id">The vendor's identifier of this pulse, such as its content identifier.</param>
    /// <param name="timestamp">When the beacon published the pulse.</param>
    /// <param name="randomness">The pulse's random bytes, at least <see cref="MinRandomnessLength"/> of them; the pulse
    /// keeps a copy.</param>
    public BeaconPulse(
        string beacon,
        string chain,
        long round,
        string id,
        DateTimeOffset timestamp,
        ReadOnlySpan<byte> randomness
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(beacon);
        ArgumentException.ThrowIfNullOrWhiteSpace(chain);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentOutOfRangeException.ThrowIfNegative(round);
        if (randomness.Length < MinRandomnessLength)
        {
            throw new ArgumentException(
                $"A pulse carries at least {MinRandomnessLength} random bytes.",
                nameof(randomness)
            );
        }

        Beacon = beacon;
        Chain = chain;
        Round = round;
        Id = id;
        Timestamp = timestamp;
        RandomnessBytes = randomness.ToArray();
    }

    /// <summary>
    /// Gets the name the beacon is registered under.
    /// </summary>
    public string Beacon { get; }

    /// <summary>
    /// Gets the vendor's identifier of the sequence the pulse belongs to.
    /// </summary>
    public string Chain { get; }

    /// <summary>
    /// Gets the pulse's position in its chain.
    /// </summary>
    public long Round { get; }

    /// <summary>
    /// Gets the vendor's identifier of this pulse.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets when the beacon published the pulse.
    /// </summary>
    public DateTimeOffset Timestamp { get; }

    /// <summary>
    /// Gets the pulse's public random bytes.
    /// </summary>
    public ReadOnlySpan<byte> Randomness => RandomnessBytes;

    private byte[] RandomnessBytes { get; }

    /// <inheritdoc />
    public override string ToString() => $"{Beacon} round {Round} ({Id})";
}
