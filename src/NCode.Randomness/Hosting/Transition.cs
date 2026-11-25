// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Randomness.Hosting;

/// <summary>
/// How a fetch changed a beacon's health, so an outage is logged once and its end once.
/// </summary>
internal enum Transition
{
    None = 0,
    BecameUnavailable = 1,
    Recovered = 2,
}
