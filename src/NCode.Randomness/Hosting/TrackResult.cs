// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Randomness.Hosting;

/// <summary>
/// What the tracker made of one fetch.
/// </summary>
internal readonly record struct TrackResult(
    string Outcome,
    BeaconError? Error,
    Transition Transition
);
