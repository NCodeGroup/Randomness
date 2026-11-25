// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Randomness.Nist.Pulses;

/// <summary>
/// NIST data that is malformed or fails verification. The beacon reports it as
/// <see cref="BeaconError.InvalidPulse"/>.
/// </summary>
internal sealed class NistFormatException(string message) : Exception(message);
