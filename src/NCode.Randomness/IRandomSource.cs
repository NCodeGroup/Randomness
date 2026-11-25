// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Randomness;

/// <summary>
/// Produces uniformly distributed random bytes. Every typed value, range, shuffle and selection in this library is
/// built on this one operation by the extension members of <see cref="RandomSourceExtensions"/>.
/// </summary>
/// <remarks>
/// An implementation is safe to call from many threads at once unless its documentation says otherwise. Whether its
/// bytes are secret depends on the implementation: inject <see cref="ISecureRandom"/> where the bytes must be
/// unpredictable to everyone, such as keys, tokens and nonces.
/// </remarks>
public interface IRandomSource
{
    /// <summary>
    /// Fills <paramref name="destination"/> with random bytes.
    /// </summary>
    /// <param name="destination">The buffer to fill. An empty buffer is left as it is.</param>
    void Fill(Span<byte> destination);
}
