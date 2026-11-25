// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;

namespace NCode.Randomness;

/// <summary>
/// The operating system's cryptographically secure random number generator
/// (<see cref="RandomNumberGenerator"/>) as an <see cref="ISecureRandom"/>.
/// </summary>
/// <remarks>
/// Use it without dependency injection, in offline deployments, or wherever no beacon is wanted. It is stateless and
/// thread-safe, so the one <see cref="Instance"/> serves every caller. The hybrid generator that
/// <c>AddRandomness()</c> registers also draws its entropy from it.
/// </remarks>
public sealed class SystemRandomSource : ISecureRandom
{
    private SystemRandomSource() { }

    /// <summary>
    /// Gets the shared instance.
    /// </summary>
    public static SystemRandomSource Instance { get; } = new();

    /// <inheritdoc />
    public void Fill(Span<byte> destination) => RandomNumberGenerator.Fill(destination);
}
