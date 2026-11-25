// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Randomness.Generation;

namespace NCode.Randomness.Tests.TestDoubles;

/// <summary>
/// Entropy that is a counter, so the generator's output is reproducible within a test, and that records how much was
/// drawn.
/// </summary>
internal sealed class CountingEntropyInput : IEntropyInput
{
    private Lock Gate { get; } = new();

    private byte Next { get; set; }

    public int Calls { get; private set; }

    public void Fill(Span<byte> destination)
    {
        lock (Gate)
        {
            Calls++;
            for (var i = 0; i < destination.Length; i++)
            {
                destination[i] = Next++;
            }
        }
    }
}
