// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;

namespace NCode.Randomness.Generation;

/// <summary>
/// Forwards to <see cref="RandomNumberGenerator"/>.
/// </summary>
internal sealed class SystemEntropyInput : IEntropyInput
{
    public void Fill(Span<byte> destination) => RandomNumberGenerator.Fill(destination);
}
