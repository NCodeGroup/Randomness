# 1013. Supplied CURBy documents are verified offline by the beacon's own checks

- **Status:** Accepted
- **Date:** 2026-11-25
- **Deciders:** NCode Group maintainers

## Context

A published draw names its pulse, and checking it means fetching that pulse from CURBy's Twine API, so the check
depends on the API being reachable and still serving the round. `VerifiableRandom.Create(pulse, context)` verifies
nothing. The chain, the pulse and its predecessor, as the API serves them, hold everything
[ADR-1003](1003-curby-pulses-are-verified-against-a-pinned-chain-before-any-byte-is-used.md) checks.

## Decision

`CurbyPulseVerifier`, in `NCode.Randomness.Curby`, takes those three documents as the UTF-8 JSON the API serves, makes
every check of ADR-1003 against a pinned chain (`CurbyDefaults.RngChainId`, or the chain identifier its constructor
takes) with the code the CURBy beacon runs, and returns the `BeaconPulse` the beacon returns for that round. It never
uses the network. Any failure is a `BeaconException` with `InvalidPulse`; an invalid chain identifier is an
`ArgumentException` when the verifier is constructed. The caller derives the draw with `VerifiableRandom.Create`, so
the derivation stays as
[ADR-1004](1004-reproducible-draws-are-a-versioned-specified-algorithm-and-never-a-secure-random.md) specifies it.
JSON nesting is limited to 32 levels, as for fetched documents; the caller bounds the size of what it supplies.

## Options considered

- **Verify in the core with a vendor-neutral document type.** The core would carry each vendor's protocol, against
  [ADR-1002](1002-beacons-are-adapters-collected-in-the-background-and-an-outage-degrades-never-fails.md).
- **Make `VerifiableRandom.Create` verify.** The core has no vendor knowledge, and its contract is ADR-1004's.
- **Return a `VerifiableRandom` from the verifier.** Two jobs in one call, and a second place that derives draws.
- **A static method with the pin as a fourth parameter.** The instance validates the pin once and keeps the call to
  the three documents.
- **Feed the documents through the HTTP client with a stub handler.** An offline path that drags in HTTP.

## Consequences

- Anyone holding the three documents can check a CURBy draw, also after CURBy stops serving the round, so a draw
  publishes or archives them with its result.
- NIST, drand and CURBy-Q documents have no offline verifier.
- The beacon and the verifier share the code that builds the pulse, so a fix to one is a fix to both.

## References

- [#3](https://github.com/NCodeGroup/Randomness/issues/3); ADR-1003; ADR-1004;
  `src/NCode.Randomness.Curby/CurbyPulseVerifier.cs`; [draws.md](../draws.md).
