// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Randomness.Drand.Rounds;

/// <summary>
/// drand data that is malformed or fails verification. The beacon reports it as
/// <see cref="BeaconError.InvalidPulse"/>.
/// </summary>
internal sealed class DrandFormatException(string message) : Exception(message);
