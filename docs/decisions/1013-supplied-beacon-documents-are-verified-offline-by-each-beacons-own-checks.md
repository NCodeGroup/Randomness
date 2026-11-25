# 1013. Supplied beacon documents are verified offline by each beacon's own checks

- **Status:** Accepted
- **Date:** 2026-11-25
- **Deciders:** NCode Group maintainers

## Context

A published draw names its round, and checking it means fetching that round from the beacon's API, so the check
depends on the API being reachable and still serving the round. `VerifiableRandom.Create(pulse, context)` verifies
nothing. The documents each API serves for a round hold everything its beacon checks: the CURBy chain, pulse and
predecessor ([ADR-1003](1003-curby-pulses-are-verified-against-a-pinned-chain-before-any-byte-is-used.md)); a CURBy-Q
round, its parameters, and the chain and pulse documents it links to
([ADR-1007](1007-curby-q-rounds-are-verified-as-curbys-client-verifies-them-and-their-extraction-is-left-to-curby-verify.md));
a NIST pulse, its predecessor and the certificates they name
([ADR-1009](1009-nist-pulses-are-verified-against-pinned-certificates-and-their-hash-chain-before-any-byte-is-used.md));
and a drand round, whose chain is pinned by constants
([ADR-1010](1010-drand-rounds-are-verified-against-the-pinned-quicknet-key-with-a-managed-bls12-381-library.md)).

## Decision

Each adapter has a public verifier beside its beacon: `CurbyPulseVerifier` and `CurbyQuantumRoundVerifier` in
`NCode.Randomness.Curby`, `NistPulseVerifier` in `NCode.Randomness.Nist` and `DrandRoundVerifier` in
`NCode.Randomness.Drand`. A verifier takes the documents its beacon fetches for a round, as the vendor's API serves
them (UTF-8 JSON, and PEM for a NIST certificate), makes every check its beacon makes against the same pinned anchor
with the beacon's code, and returns the `BeaconPulse` the beacon returns for that round, built by one method both call.
It never uses the network. Any failure is a `BeaconException` with `InvalidPulse`. The caller derives the draw with
`VerifiableRandom.Create`, so the derivation stays as
[ADR-1004](1004-reproducible-draws-are-a-versioned-specified-algorithm-and-never-a-secure-random.md) specifies it.

The constructor pins what the beacon's defaults pin, or another anchor the beacon's options could pin, and validates it
once: an invalid one is an `ArgumentException` when the verifier is constructed. `CurbyPulseVerifier` pins a chain
identifier, `CurbyQuantumRoundVerifier` the CURBy-Q and Bell chain identifiers, and `NistPulseVerifier` a chain index
and certificate identifiers. The drand beacon pins quicknet by constants, so `DrandRoundVerifier` pins quicknet alone
and needs no chain document.

`NistPulseVerifier` takes the certificate each pulse names, so a pulse that follows a certificate rotation, whose
predecessor names the previous certificate, can be checked too. `CurbyQuantumRoundVerifier` takes the round and its
parameters, and the chain and pulse documents as one collection, which it indexes by content identifier and reads where
the beacon fetches by that identifier; the walks back along the Bell and seed chains are the beacon's code.

No check depends on the clock: a NIST certificate must cover the pulse's timestamp, as for the beacon. The checks only
a fetch needs, such as that the API answered the round asked for, have no offline counterpart: the caller compares the
pulse's round and identifier with the ones the draw published. JSON nesting and duplicate members are limited as for
fetched documents; the caller bounds the size of what it supplies.

## Options considered

- **Verify in the core with a vendor-neutral document type.** The core would carry each vendor's protocol, against
  [ADR-1002](1002-beacons-are-adapters-collected-in-the-background-and-an-outage-degrades-never-fails.md).
- **Make `VerifiableRandom.Create` verify.** The core has no vendor knowledge, and its contract is ADR-1004's.
- **Return a `VerifiableRandom` from the verifier.** Two jobs in one call, and a second place that derives draws.
- **A static method with the pin as a parameter.** The instance validates the pin once and keeps the call to the
  documents.
- **Feed the documents through the HTTP client with a stub handler.** An offline path that drags in HTTP.
- **A NIST verifier that takes one certificate.** A pulse that follows a certificate rotation names another
  certificate than its predecessor, and could not be checked.
- **A CURBy-Q verifier with one parameter per document.** Seven inputs, two of them lists whose length depends on the
  round, in an order the caller must get right; the beacon itself addresses each document by its content identifier.
- **Share the beacons' asynchronous crawls by blocking on them offline.** Blocking on a task is banned; the
  synchronous walk steps are shared instead.
- **A drand verifier that pins any chain.** The beacon reads only quicknet, so this library makes no draw over another
  chain to check.

## Consequences

- Anyone holding the documents can check a draw over any of the four beacons, also after the vendor stops serving the
  round, so a draw publishes or archives them with its result.
- Each beacon and its verifier share the code that checks the documents and builds the pulse, so a fix to one is a fix
  to both.
- A CURBy-Q round's Bell-test certificate and Trevisan extraction stay unverified offline too, as ADR-1007 leaves them
  to CURBy's `curby_verify`.

## References

- [#3](https://github.com/NCodeGroup/Randomness/issues/3); ADR-1003; ADR-1004; ADR-1007; ADR-1009; ADR-1010;
  `src/NCode.Randomness.Curby/CurbyPulseVerifier.cs`; `src/NCode.Randomness.Curby/CurbyQuantumRoundVerifier.cs`;
  `src/NCode.Randomness.Nist/NistPulseVerifier.cs`; `src/NCode.Randomness.Drand/DrandRoundVerifier.cs`;
  [draws.md](../draws.md).
