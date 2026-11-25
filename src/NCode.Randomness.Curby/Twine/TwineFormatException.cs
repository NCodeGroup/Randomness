// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Randomness.Curby.Twine;

/// <summary>
/// Twine data that is malformed or fails verification. The beacon reports it as
/// <see cref="BeaconError.InvalidPulse"/>.
/// </summary>
internal sealed class TwineFormatException(string message) : Exception(message);
