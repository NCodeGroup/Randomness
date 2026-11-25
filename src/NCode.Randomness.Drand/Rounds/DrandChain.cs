// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Neo.Cryptography.BLS12_381;

namespace NCode.Randomness.Drand.Rounds;

/// <summary>
/// A drand chain pinned by its hash, group public key, genesis time and period.
/// </summary>
/// <param name="hash">The chain hash, in lowercase hex.</param>
/// <param name="publicKey">The group public key every round is verified against.</param>
/// <param name="genesisTime">The time of round 1, in Unix seconds.</param>
/// <param name="period">The seconds between rounds.</param>
internal sealed class DrandChain(string hash, G2Affine publicKey, long genesisTime, int period)
{
    /// <summary>
    /// Gets the quicknet chain, pinned from <see cref="DrandDefaults"/>.
    /// </summary>
    public static DrandChain Quicknet { get; } = CreateQuicknet();

    /// <summary>
    /// Creates the quicknet chain from <see cref="DrandDefaults"/>.
    /// </summary>
    /// <returns>The quicknet chain.</returns>
    private static DrandChain CreateQuicknet()
    {
        var publicKeyBytes = Convert.FromHexString(DrandDefaults.PublicKey);
        var publicKey = G2Affine.FromCompressed(publicKeyBytes);
        return new DrandChain(
            DrandDefaults.ChainHash,
            publicKey,
            DrandDefaults.GenesisTime,
            DrandDefaults.Period
        );
    }

    /// <summary>
    /// Gets the chain hash, in lowercase hex.
    /// </summary>
    public string Hash { get; } = hash;

    /// <summary>
    /// Gets the group public key every round is verified against.
    /// </summary>
    public G2Affine PublicKey { get; } = publicKey;

    /// <summary>
    /// Gets the time of round 1, in Unix seconds.
    /// </summary>
    public long GenesisTime { get; } = genesisTime;

    /// <summary>
    /// Gets the seconds between rounds.
    /// </summary>
    public int Period { get; } = period;

    /// <summary>
    /// Gets the last round whose time <see cref="DateTimeOffset"/> can represent.
    /// </summary>
    public long MaxRound =>
        (DateTimeOffset.MaxValue.ToUnixTimeSeconds() - GenesisTime) / Period + 1;

    /// <summary>
    /// Gets the time a round is due: the genesis time plus the periods before it.
    /// </summary>
    /// <param name="round">The round, from 1 to <see cref="MaxRound"/>.</param>
    /// <returns>The round's time.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The round is not in 1 to <see cref="MaxRound"/>.</exception>
    public DateTimeOffset TimeOf(long round)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(round, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(round, MaxRound);

        return DateTimeOffset.FromUnixTimeSeconds(GenesisTime + (round - 1) * Period);
    }
}
