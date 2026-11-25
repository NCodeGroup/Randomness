# 1002. Beacons are adapters collected in the background, and an outage degrades, never fails

- **Status:** Accepted (amended by 1008)
- **Date:** 2026-11-25
- **Deciders:** NCode Group maintainers

## Context

CURBy is one research beacon run by one university; NIST, drand and others publish beacons with different protocols.
Any of them can be slow, down for maintenance, or answer with data that fails verification. Some deployments are
air-gapped; others want beacon input once at start and nothing afterwards.

## Decision

- **One port.** `IRandomnessBeacon` (`Name`, `GetLatestPulseAsync`, `GetPulseAsync`) is the only contract a vendor
  adapter implements, in a package of its own, and it returns only pulses it has verified as its vendor's protocol
  specifies, or throws `BeaconException` with a `BeaconError`. Adapters register with `AddBeacon<T>()`; names must be
  unique, which the composition checks.
- **Background collection.** A hosted service runs one loop per beacon, so a slow beacon delays no other and no
  caller. Each fetch has its own timeout (`FetchTimeout`); failures back off exponentially from `RefreshInterval` up
  to `MaxRetryDelay`, scaled by a random factor in [0.5, 1).
- **Acceptance.** A fetched pulse is mixed in only if it names its beacon, belongs to the same chain as the beacon's
  earlier pulses, is not older than the last accepted round of
  its chain (rollback), does not reuse an accepted round with other content (equivocation), and is no older than
  `MaxPulseAge` and no further ahead than `MaxClockSkew` by its own timestamp (a stalled beacon or mirror).
- **Modes.** `Continuous` (the default) keeps fetching; `InitializeOnly` fetches until each beacon gave one pulse and
  then stops; `Offline` contacts no beacon even when some are registered.
- **Failure posture.** An outage is logged once when it starts and once when it ends, counted in
  `ncode.randomness.beacon.pulses`, and reported `Degraded` by the health check, never `Unhealthy`, since generation
  stays secure. `RequireInitialPulse` is an explicit opt-in that fails the host's start when no pulse arrives within
  `InitialPulseTimeout`, for a policy that demands beacon input.

## Options considered

- **Fetch on demand.** Adds the beacon's latency and availability to every call: rejected.
- **Fail closed on any outage.** Turns a university's maintenance window into an outage of every consumer, for no gain
  in secrecy ([ADR-1000](1000-beacon-randomness-is-mixed-into-a-local-generator-and-never-used-alone-for-secrets.md)).
- **A single fallback chain of beacons.** Mixing every available beacon is stronger: no single beacon is trusted, and
  each one that answers adds its pulse.

## Consequences

- Resilience to an outage of Boulder comes from three layers: operating-system entropy, other registered beacons, and
  the HTTP resilience pipeline of each adapter (retries, attempt and total timeouts, circuit breaker).
- A new vendor is a new package; the core never changes for it. `docs/beacon-adapters.md` is the adapter contract.

## References

- `src/NCode.Randomness/Hosting/`, `src/NCode.Randomness/HealthChecks/`.
