// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Randomness.Curby.Twine;

namespace NCode.Randomness.Curby.Quantum;

/// <summary>
/// The walk back along the seed chain from the pulse a CURBy-Q round's randomness pulse mixes in to the first pulse
/// that does not mix in the precommit, one pulse at a time, which selects the seed pulse. No more than ten pulses
/// are visited.
/// </summary>
/// <param name="result">The round's verified pulses.</param>
/// <param name="chain">The verified seed chain.</param>
internal sealed class QuantumSeedWalk(QuantumResult result, TwineChain chain)
{
    private const int MaxPulses = 10;

    private QuantumResult Result { get; } = result;

    private TwineChain Chain { get; } = chain;

    /// <summary>
    /// Gets the CID of the pulse to visit next.
    /// </summary>
    public Cid Next { get; private set; } =
        result.ResultPulse.MixinOf(result.SeedChain)
        ?? throw new TwineFormatException("The result does not mix in the seed chain.");

    private List<TwineSignedPulse> Mixing { get; } = new(MaxPulses);

    private TwineSignedPulse? Newer { get; set; }

    /// <summary>
    /// Verifies the document of the pulse <see cref="Next"/> names as a pulse of the chain that precedes the pulse
    /// visited before it, and returns the seed pulse once the walk reaches a pulse that does not mix in the
    /// precommit. Otherwise it returns <see langword="null"/> and <see cref="Next"/> names the pulse before it.
    /// </summary>
    /// <param name="document">The document of the pulse <see cref="Next"/> names.</param>
    /// <returns>The seed pulse, or <see langword="null"/> while the walk continues.</returns>
    /// <exception cref="TwineFormatException">
    /// The pulse does not verify or precede the one visited before it, the chain starts before any pulse that does not
    /// mix in the precommit, or ten pulses in a row mix it in.
    /// </exception>
    public TwineSignedPulse? Visit(TwineDocument document)
    {
        var pulse = TwineSignedPulse.Verify(document, Chain);
        Newer?.VerifyPrecededBy(pulse);
        if (!Result.MixesPrecommit(pulse))
        {
            return Result.SelectSeed(Mixing);
        }

        Mixing.Add(pulse);
        Next = pulse.Previous
            ?? throw new TwineFormatException(
                "The seed chain starts before any pulse that does not mix in the precommit."
            );
        if (Mixing.Count == MaxPulses)
        {
            throw new TwineFormatException(
                $"{MaxPulses} seed pulses in a row mix in the precommit."
            );
        }

        Newer = pulse;
        return null;
    }
}
