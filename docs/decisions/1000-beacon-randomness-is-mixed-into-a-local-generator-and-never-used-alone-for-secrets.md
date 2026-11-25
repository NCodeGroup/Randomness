# 1000. Beacon randomness is mixed into a local generator and never used alone for secrets

- **Status:** Accepted
- **Date:** 2026-11-25
- **Deciders:** NCode Group maintainers

## Context

A public randomness beacon, such as CURBy at the University of Colorado Boulder, publishes its randomness to everyone.
CURBy's quantum chain derives its bits from a loophole-free Bell test, so they are unpredictable before publication,
but once a pulse is published every reader holds the same 64 bytes. A key, token, salt or nonce derived only from a
pulse is known to anyone who reads that pulse, and an attacker who can guess which pulse was used (there is one a
minute) can recompute it.

The library is meant both for cryptographic operations, where outputs must be secret, and for draws that others must
be able to check, where outputs must be public and reproducible. Those are opposite requirements of the same bytes.

## Decision

The secure generator, `ISecureRandom`, draws its entropy from the operating system's CSPRNG and uses beacon pulses only
as additional input: as the personalization string of each HMAC_DRBG instantiation and as the additional input of each
reseed ([ADR-1001](1001-the-secure-generator-is-a-sharded-buffered-hmac-drbg-reseeded-from-the-operating-system.md)).
Its output is therefore at least as unpredictable as the operating system's generator whatever a beacon publishes,
including a forged, replayed or withheld pulse.

Public, reproducible randomness is a different type, `VerifiableRandom`, which implements `IRandomSource` and not
`ISecureRandom` ([ADR-1004](1004-reproducible-draws-are-a-versioned-specified-algorithm-and-never-a-secure-random.md)),
so the compiler refuses to pass it where a secret is required.

## Options considered

- **Serve beacon bytes directly as the random source.** Simplest, and what "truly random from Boulder" first suggests,
  but every output is public: insecure for any secret.
- **XOR the beacon stream with the operating system's stream.** As secure as the stronger input, but it either repeats
  the pulse's bytes, or needs a stream cipher keyed by the pulse, and it doubles the work per call.
- **Mix the pulse into a standard DRBG as additional input (chosen).** NIST SP 800-90A defines exactly this input for
  data that may be public. Security rests on the operating system's entropy, every new pulse diversifies the state, and
  the beacon adds defense in depth against a weak or cloned operating-system generator, such as a restored virtual
  machine snapshot that replays its state, since the pulses that arrive afterwards differ.

## Consequences

- A beacon outage never weakens or stops generation; it only stops the diversification
  ([ADR-1002](1002-beacons-are-adapters-collected-in-the-background-and-an-outage-degrades-never-fails.md)).
- The documentation never claims that secret output is "quantum": it is computationally secure output of a vetted
  DRBG whose seed includes quantum-certified public input.
- Code that needs secrets depends on `ISecureRandom`; code that needs a checkable draw depends on
  `IVerifiableRandomFactory`. Reviewers reject any path that derives a secret from `BeaconPulse.Randomness`.

## References

- NIST SP 800-90A Rev. 1, sections 8.7.2 (personalization string) and 9.1–9.3 (additional input).
- The CURBy client library's guidance on committing to a future round for public draws:
  https://github.com/buff-beacon-project/curby-js-client
