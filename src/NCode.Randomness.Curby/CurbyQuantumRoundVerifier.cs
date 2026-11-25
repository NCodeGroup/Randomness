// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Randomness.Curby.Quantum;
using NCode.Randomness.Curby.Twine;

namespace NCode.Randomness.Curby;

/// <summary>
/// Verifies a CURBy-Q round from documents that a caller supplies, without the network, by every check the CURBy-Q
/// beacon makes: the CURBy-Q chain must hash to its pinned CID; every pulse of the round must be signed by its key and
/// follow the one before; a pulse of the pinned Bell chain, within ten of the one the precommit mixes in, must answer
/// the request with the precommitted data hash; the seed pulse on the seed chain the precommit names must be the one
/// its rule selects; and the parameters must hash to the randomness pulse's commitment and hold the seed that expands
/// from the seed pulse. The Bell-test certificate and the extraction are not verified. An instance is immutable and
/// thread-safe.
/// </summary>
public sealed class CurbyQuantumRoundVerifier
{
    /// <summary>
    /// The Twine specification family of the CURBy-Q chain.
    /// </summary>
    internal const string QuantumFamily = "diqrng";

    /// <summary>
    /// The Twine specification family of the Bell chain.
    /// </summary>
    internal const string BellFamily = "bell";

    /// <summary>
    /// Initializes the verifier to pin the CURBy-Q and Bell chains.
    /// </summary>
    public CurbyQuantumRoundVerifier()
        : this(CurbyDefaults.QuantumChainId, CurbyDefaults.BellChainId) { }

    /// <summary>
    /// Initializes the verifier to pin a CURBy-Q chain and a Bell chain.
    /// </summary>
    /// <param name="quantumChainId">The content identifier (CID) of the CURBy-Q chain to pin.</param>
    /// <param name="bellChainId">The content identifier (CID) of the Bell chain to pin.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="quantumChainId"/> or <paramref name="bellChainId"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="quantumChainId"/> or <paramref name="bellChainId"/> is empty, white space or not a Twine content
    /// identifier.
    /// </exception>
    public CurbyQuantumRoundVerifier(string quantumChainId, string bellChainId)
    {
        QuantumChain = CurbyPulseVerifier.ParseChainId(quantumChainId, nameof(quantumChainId));
        BellChain = CurbyPulseVerifier.ParseChainId(bellChainId, nameof(bellChainId));
    }

    private Cid QuantumChain { get; }

    private Cid BellChain { get; }

    /// <summary>
    /// Verifies a round against the pinned chains and returns its pulse as the CURBy-Q beacon returns it.
    /// </summary>
    /// <param name="round">
    /// The round, the JSON array of its pulses that the Twine API serves for it, in UTF-8.
    /// </param>
    /// <param name="parameters">The extractor's parameters, as the Twine API serves them for the round.</param>
    /// <param name="documents">
    /// The chain and pulse documents the round links to, each the UTF-8 JSON the Twine API serves, in any order: the
    /// CURBy-Q, Bell and seed chains, the Bell pulses from the one the precommit mixes in back to the response, and the
    /// seed pulses from the one the randomness pulse mixes in back to the first that does not mix in the precommit.
    /// </param>
    /// <returns>The verified pulse.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="documents"/> is <see langword="null"/>.</exception>
    /// <exception cref="BeaconException">
    /// A document is malformed or missing, the round has no randomness, or any check fails.
    /// </exception>
    public BeaconPulse Verify(
        ReadOnlySpan<byte> round,
        ReadOnlySpan<byte> parameters,
        IReadOnlyCollection<ReadOnlyMemory<byte>> documents
    )
    {
        ArgumentNullException.ThrowIfNull(documents);
        try
        {
            var index = Index(documents);
            var quantumDocument = Find(index, QuantumChain, "chain");
            using var chain = TwineChain.Verify(quantumDocument, QuantumChain, QuantumFamily);
            var roundDocuments = TwineDocument.ParseArray(round);
            var verified = QuantumRound.Verify(roundDocuments, chain);
            if (verified.Result is not { } result)
            {
                throw new TwineFormatException(
                    $"Round {verified.Number} of CURBy-Q produced no randomness: {verified.ErrorReason ?? "it has not completed"}."
                );
            }

            var bellDocument = Find(index, BellChain, "chain");
            using var bell = TwineChain.Verify(bellDocument, BellChain, BellFamily);
            var bellWalk = new QuantumBellWalk(result, bell);
            bool answered;
            do
            {
                var document = Find(index, bellWalk.Next, "pulse");
                answered = bellWalk.Visit(document);
            } while (!answered);

            var seedDocument = Find(index, result.SeedChain, "chain");
            using var seedChain = TwineChain.Verify(seedDocument, result.SeedChain, null);
            var seedWalk = new QuantumSeedWalk(result, seedChain);
            TwineSignedPulse? seed = null;
            while (seed is null)
            {
                var document = Find(index, seedWalk.Next, "pulse");
                seed = seedWalk.Visit(document);
            }

            result.VerifySeed(seed, parameters);
            return ToBeaconPulse(chain, verified.Number, result);
        }
        catch (Exception exception) when (exception is not BeaconException)
        {
            throw new BeaconException(
                BeaconError.InvalidPulse,
                CurbyDefaults.QuantumBeaconName,
                $"The CURBy-Q documents fail verification: {exception.Message}",
                exception
            );
        }
    }

    /// <summary>
    /// Parses every supplied document and indexes it by its CID.
    /// </summary>
    /// <param name="documents">The UTF-8 JSON of the chain and pulse documents.</param>
    /// <returns>The documents by CID.</returns>
    private static Dictionary<Cid, TwineDocument> Index(
        IReadOnlyCollection<ReadOnlyMemory<byte>> documents
    )
    {
        var index = new Dictionary<Cid, TwineDocument>(documents.Count);
        foreach (var document in documents)
        {
            var parsed = TwineDocument.Parse(document.Span);
            index.TryAdd(parsed.Cid, parsed);
        }

        return index;
    }

    /// <summary>
    /// Finds the document with a CID among the supplied ones.
    /// </summary>
    /// <param name="index">The supplied documents by CID.</param>
    /// <param name="cid">The CID of the document.</param>
    /// <param name="kind">What the document is, <c>chain</c> or <c>pulse</c>, for the message of a missing one.</param>
    /// <returns>The document.</returns>
    private static TwineDocument Find(Dictionary<Cid, TwineDocument> index, Cid cid, string kind) =>
        index.GetValueOrDefault(cid)
        ?? throw new TwineFormatException($"The documents hold no {kind} {cid}.");

    /// <summary>
    /// Returns a verified round as the CURBy-Q beacon's pulse.
    /// </summary>
    /// <param name="chain">The verified CURBy-Q chain.</param>
    /// <param name="number">The round number.</param>
    /// <param name="result">The round's verified pulses.</param>
    /// <returns>The beacon pulse.</returns>
    internal static BeaconPulse ToBeaconPulse(TwineChain chain, long number, QuantumResult result)
    {
        var chainId = chain.Cid.ToString();
        var id = result.ResultPulse.Cid.ToString();
        return new BeaconPulse(
            CurbyDefaults.QuantumBeaconName,
            chainId,
            number,
            id,
            result.Timestamp,
            result.Randomness
        );
    }
}
