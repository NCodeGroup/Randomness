// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Randomness.Drand.Rounds;

/// <summary>
/// drand data that is malformed or fails verification. The beacon reports it as
/// <see cref="BeaconError.InvalidPulse"/>.
/// </summary>
/// <param name="message">The message that describes the failure.</param>
/// <param name="innerException">The exception that caused it, if any.</param>
internal sealed class DrandFormatException(string message, Exception? innerException = null)
    : Exception(message, innerException);
