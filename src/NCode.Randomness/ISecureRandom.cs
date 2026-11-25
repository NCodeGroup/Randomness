// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Randomness;

/// <summary>
/// A cryptographically secure random source: its bytes are unpredictable to everyone, including anyone who can read
/// every public randomness beacon, so they are suitable for keys, secrets, tokens, salts and nonces.
/// </summary>
/// <remarks>
/// <para>
/// The interface adds no member to <see cref="IRandomSource"/>: it is a capability that the type system checks.
/// Depend on <see cref="ISecureRandom"/> wherever secrecy matters, so that a reproducible source such as
/// <see cref="VerifiableRandom"/>, whose bytes anyone holding the beacon pulse can compute, can never be passed in its
/// place.
/// </para>
/// <para>
/// Calls never wait on the network: beacon entropy is collected in the background and mixed into a local generator.
/// </para>
/// </remarks>
#pragma warning disable CA1040 // The empty interface is the point: it marks a source whose bytes are secret.
public interface ISecureRandom : IRandomSource;
#pragma warning restore CA1040
