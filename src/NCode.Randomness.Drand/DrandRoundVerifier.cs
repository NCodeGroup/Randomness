// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Randomness.Drand.Rounds;

namespace NCode.Randomness.Drand;

/// <summary>
/// Verifies drand quicknet round documents that a caller supplies, without the network, by every check the drand
/// beacon makes: the round's signature must be a point of G1 other than the identity and verify against the pinned
/// group public key for the round number, and a <c>randomness</c> member, when present, must be the SHA-256 of the
/// signature. An instance is immutable and thread-safe.
/// </summary>
public sealed class DrandRoundVerifier
{
    /// <summary>
    /// Returns a verified round as the drand beacon's pulse.
    /// </summary>
    /// <param name="chain">The pinned chain the round was verified against.</param>
    /// <param name="round">The verified round.</param>
    /// <returns>The beacon pulse.</returns>
    internal static BeaconPulse ToBeaconPulse(DrandChain chain, DrandRound round)
    {
        var id = Convert.ToHexStringLower(round.Signature);
        return new BeaconPulse(
            DrandDefaults.BeaconName,
            chain.Hash,
            round.Round,
            id,
            round.Timestamp,
            round.Randomness
        );
    }

    /// <summary>
    /// Initializes the verifier to pin the quicknet chain.
    /// </summary>
    public DrandRoundVerifier()
        : this(DrandChain.Quicknet) { }

    /// <summary>
    /// Initializes the verifier to pin a chain, which lets tests pin a chain other than quicknet.
    /// </summary>
    /// <param name="chain">The pinned chain whose group public key verifies the round.</param>
    internal DrandRoundVerifier(DrandChain chain)
    {
        ArgumentNullException.ThrowIfNull(chain);
        Chain = chain;
    }

    private DrandChain Chain { get; }

    /// <summary>
    /// Verifies a round against the pinned chain and returns it as the drand beacon returns it.
    /// </summary>
    /// <param name="round">The round document, as the drand API serves it, in UTF-8 JSON.</param>
    /// <returns>The verified pulse.</returns>
    /// <exception cref="BeaconException">The document is malformed or a check fails.</exception>
    public BeaconPulse Verify(ReadOnlySpan<byte> round)
    {
        try
        {
            var verified = DrandRound.Verify(round, Chain);
            return ToBeaconPulse(Chain, verified);
        }
        catch (Exception exception) when (exception is not BeaconException)
        {
            throw new BeaconException(
                BeaconError.InvalidPulse,
                DrandDefaults.BeaconName,
                $"The drand round fails verification: {exception.Message}",
                exception
            );
        }
    }
}
