// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Randomness.Generation;

namespace NCode.Randomness.Tests.TestDoubles;

/// <summary>
/// Serves the bytes it was given, in order, and fails when they run out, so a test states exactly what a sampler reads.
/// </summary>
internal sealed class ScriptedRandomSource(params byte[] bytes) : IEntropyInput
{
    private Queue<byte> Bytes { get; } = new(bytes);

    public int Remaining => Bytes.Count;

    public int Requests { get; private set; }

    public void Fill(Span<byte> destination)
    {
        Requests++;
        if (destination.Length > Bytes.Count)
        {
            throw new InvalidOperationException(
                $"The script has {Bytes.Count} bytes left; {destination.Length} were asked for."
            );
        }

        for (var i = 0; i < destination.Length; i++)
        {
            destination[i] = Bytes.Dequeue();
        }
    }
}
