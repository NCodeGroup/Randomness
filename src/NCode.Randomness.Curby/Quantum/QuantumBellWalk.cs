// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Randomness.Curby.Twine;

namespace NCode.Randomness.Curby.Quantum;

/// <summary>
/// The walk back along the Bell chain from the pulse a CURBy-Q round's precommit mixes in to the response to the
/// round's request, one pulse at a time: the caller supplies the document of the pulse <see cref="Next"/> names, and
/// <see cref="Visit"/> verifies it. No more than ten pulses are visited.
/// </summary>
/// <param name="result">The round's verified pulses.</param>
/// <param name="chain">The verified Bell chain.</param>
internal sealed class QuantumBellWalk(QuantumResult result, TwineChain chain)
{
    private const int MaxPulses = 10;

    private QuantumResult Result { get; } = result;

    private TwineChain Chain { get; } = chain;

    /// <summary>
    /// Gets the CID of the pulse to visit next.
    /// </summary>
    public Cid Next { get; private set; } =
        result.Precommit.MixinOf(chain.Cid)
        ?? throw new TwineFormatException("The precommit does not mix in the Bell chain.");

    private TwineSignedPulse? Newer { get; set; }

    private int Visited { get; set; }

    /// <summary>
    /// Verifies the document of the pulse <see cref="Next"/> names as a pulse of the chain that precedes the pulse
    /// visited before it, and returns whether it answers the request. When it does not, <see cref="Next"/> names the
    /// pulse before it.
    /// </summary>
    /// <param name="document">The document of the pulse <see cref="Next"/> names.</param>
    /// <returns><see langword="true"/> when the pulse answers the request; otherwise <see langword="false"/>.</returns>
    /// <exception cref="TwineFormatException">
    /// The pulse does not verify or precede the one visited before it, the chain starts before any response to the
    /// request, or no pulse within ten answers it.
    /// </exception>
    public bool Visit(TwineDocument document)
    {
        var pulse = TwineSignedPulse.Verify(document, Chain);
        Newer?.VerifyPrecededBy(pulse);
        if (Result.IsBellResponse(pulse))
        {
            return true;
        }

        Next = pulse.Previous
            ?? throw new TwineFormatException(
                "The Bell chain starts before any response to the request."
            );
        Visited++;
        if (Visited == MaxPulses)
        {
            throw new TwineFormatException(
                $"No response to the request is within {MaxPulses} Bell pulses of the one the precommit mixes in."
            );
        }

        Newer = pulse;
        return false;
    }
}
