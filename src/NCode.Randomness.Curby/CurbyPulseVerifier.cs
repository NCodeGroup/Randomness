// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Randomness.Curby.Twine;

namespace NCode.Randomness.Curby;

/// <summary>
/// Verifies CURBy-RNG pulse documents that a caller supplies, without the network, by every check the CURBy beacon
/// makes: the chain must hash to the pinned CID and be signed by its own key; the pulse and its predecessor must each
/// hash to their CIDs and be signed by the chain's key; the predecessor must be the pulse's first link and one round
/// earlier; and the pulse's salt must match the predecessor's precommitment. An instance is immutable and thread-safe.
/// </summary>
public sealed class CurbyPulseVerifier
{
    /// <summary>
    /// The Twine specification family of the CURBy-RNG chain.
    /// </summary>
    internal const string RngFamily = "rng";

    /// <summary>
    /// Parses a chain identifier a verifier's constructor takes.
    /// </summary>
    /// <param name="chainId">The content identifier (CID) of the chain to pin.</param>
    /// <param name="parameterName">The name of the constructor parameter that holds <paramref name="chainId"/>.</param>
    /// <returns>The parsed identifier.</returns>
    /// <exception cref="ArgumentException"><paramref name="chainId"/> is not a Twine content identifier.</exception>
    internal static Cid ParseChainId(string chainId, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(chainId, parameterName);
        try
        {
            return Cid.Parse(chainId);
        }
        catch (TwineFormatException exception)
        {
            throw new ArgumentException(
                $"'{chainId}' is not a Twine content identifier: {exception.Message}",
                parameterName,
                exception
            );
        }
    }

    /// <summary>
    /// Checks that the pulse directly follows its predecessor and returns it as the CURBy beacon's pulse.
    /// </summary>
    /// <param name="chain">The verified chain.</param>
    /// <param name="pulse">The verified pulse.</param>
    /// <param name="predecessor">The verified pulse one round before <paramref name="pulse"/>.</param>
    /// <returns>The beacon pulse.</returns>
    internal static BeaconPulse ToBeaconPulse(
        TwineChain chain,
        TwinePulse pulse,
        TwinePulse predecessor
    )
    {
        pulse.VerifyFollows(predecessor);
        var chainId = chain.Cid.ToString();
        var id = pulse.Cid.ToString();
        return new BeaconPulse(
            CurbyDefaults.BeaconName,
            chainId,
            pulse.Index,
            id,
            pulse.Timestamp,
            pulse.Cid.Digest
        );
    }

    /// <summary>
    /// Initializes the verifier to pin the CURBy-RNG chain.
    /// </summary>
    public CurbyPulseVerifier()
        : this(CurbyDefaults.RngChainId) { }

    /// <summary>
    /// Initializes the verifier to pin a chain.
    /// </summary>
    /// <param name="chainId">The content identifier (CID) of the chain to pin.</param>
    /// <exception cref="ArgumentNullException"><paramref name="chainId"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="chainId"/> is empty, white space or not a Twine content identifier.
    /// </exception>
    public CurbyPulseVerifier(string chainId)
    {
        PinnedChain = ParseChainId(chainId, nameof(chainId));
    }

    private Cid PinnedChain { get; }

    /// <summary>
    /// Verifies a pulse against the pinned chain and returns it as the CURBy beacon returns it.
    /// </summary>
    /// <param name="chain">The chain document, as the Twine API serves it, in UTF-8 JSON.</param>
    /// <param name="pulse">The pulse document, as the Twine API serves it, in UTF-8 JSON.</param>
    /// <param name="predecessor">
    /// The document of the pulse one round before <paramref name="pulse"/>, as the Twine API serves it, in UTF-8 JSON.
    /// </param>
    /// <returns>The verified pulse.</returns>
    /// <exception cref="BeaconException">A document is malformed or any check fails.</exception>
    public BeaconPulse Verify(
        ReadOnlySpan<byte> chain,
        ReadOnlySpan<byte> pulse,
        ReadOnlySpan<byte> predecessor
    )
    {
        try
        {
            var chainDocument = TwineDocument.Parse(chain);
            using var verifiedChain = TwineChain.Verify(chainDocument, PinnedChain, RngFamily);
            var pulseDocument = TwineDocument.Parse(pulse);
            var verifiedPulse = TwinePulse.Verify(pulseDocument, verifiedChain);
            var predecessorDocument = TwineDocument.Parse(predecessor);
            var verifiedPredecessor = TwinePulse.Verify(predecessorDocument, verifiedChain);
            return ToBeaconPulse(verifiedChain, verifiedPulse, verifiedPredecessor);
        }
        catch (Exception exception) when (exception is not BeaconException)
        {
            throw new BeaconException(
                BeaconError.InvalidPulse,
                CurbyDefaults.BeaconName,
                $"The CURBy documents fail verification: {exception.Message}",
                exception
            );
        }
    }
}
