# 1008. Pulse staleness is judged per beacon

- **Status:** Accepted
- **Date:** 2026-11-25
- **Deciders:** NCode Group maintainers
- **Amends:** 1002

## Context

[ADR-1002](1002-beacons-are-adapters-collected-in-the-background-and-an-outage-degrades-never-fails.md) refuses a pulse
older than `MaxPulseAge` by its own timestamp, a stalled beacon or mirror, and the limit is 10 minutes for every
beacon. Beacons do not publish at one rhythm: CURBy-RNG publishes a pulse every minute, and CURBy-Q completes a round
about every 13 minutes while it runs (an estimate, read 2026-11-25), longer than the limit. One limit long
enough for CURBy-Q widens the window in which a stalled or replaying CURBy-RNG mirror goes unnoticed, and one limit
short enough for CURBy-RNG refuses every CURBy-Q round.

## Decision

The core judges staleness per beacon, with a generic setting that names no vendor.

- **The setting.** `RandomnessOptions.MaxPulseAgeByBeacon` (`Randomness:MaxPulseAgeByBeacon:<name>`) holds the oldest
  a pulse may be, by beacon name. For a beacon it names, the entry replaces `MaxPulseAge`; every other beacon keeps
  `MaxPulseAge`. The dictionary is empty by default, and each entry is at least one second.
- **An unknown name fails the host at start.** `BeaconTracker` throws `InvalidOperationException` for an entry that no
  registered beacon has, as a misspelled key fails the host elsewhere.
- **The CURBy-Q default.** `AddCurbyQuantumBeacon()` post-configures `curby-q` to 30 minutes, roughly twice a round,
  unless the host sets an entry for `curby-q` or its `MaxPulseAge` is already 30 minutes or more. CURBy-Q works with the
  defaults, and CURBy-RNG keeps its 10-minute limit.

The clock-skew limit, `MaxClockSkew`, stays one value for every beacon.

## Options considered

- **One limit for every beacon.** It forces the slowest beacon's limit on all of them, or leaves the slow beacon
  refused.
- **A member of `IRandomnessBeacon` declaring a cadence.** It changes the contract for every adapter, and a host still
  needs an override when a beacon's real rhythm differs from the declared one.
- **A limit in each adapter's own options section.** The core would have to ask each adapter for it, and one concern
  would live in many sections.

## Consequences

- A host tunes one beacon without loosening the others.
- An adapter may supply a default where the host set none, as the CURBy-Q registration does; a value the host sets
  wins.
- A misspelled beacon name fails at start instead of leaving the default limit in force.
- A host reads one place, `Randomness:MaxPulseAgeByBeacon`, for every beacon's limit, and a beacon with a limit of its
  own shows in the options the host resolves.

## References

- [ADR-1002](1002-beacons-are-adapters-collected-in-the-background-and-an-outage-degrades-never-fails.md),
  [ADR-1006](1006-options-classes-have-setters-for-the-options-pattern.md) and
  [ADR-1007](1007-curby-q-rounds-are-verified-as-curbys-client-verifies-them-and-their-extraction-is-left-to-curby-verify.md).
- `src/NCode.Randomness/Hosting/BeaconTracker.cs`.
