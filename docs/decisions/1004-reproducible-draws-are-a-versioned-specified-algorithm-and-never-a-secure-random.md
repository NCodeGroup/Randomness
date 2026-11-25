# 1004. Reproducible draws are a versioned, specified algorithm and never a secure random

- **Status:** Accepted
- **Date:** 2026-11-25
- **Deciders:** NCode Group maintainers

## Context

The use CURBy is built for is public randomness that anyone can check: lotteries, audits, sortition. A result is
checkable only if a third party, possibly without .NET, can recompute it from the pulse and a public description of
the algorithm, and only fair if the round was committed to before it was published.

## Decision

`VerifiableRandom.Create(pulse, context)` derives a byte stream named `ncode-randomness-draw/1`: HMAC_DRBG with
HMAC-SHA-512 instantiated with the pulse's randomness as entropy input, the length-prefixed beacon name, chain, round
and pulse ID as nonce, and the length-prefixed algorithm name and context as personalization string, read as
consecutive 64-byte generate requests without additional input. The typed members of `RandomSourceExtensions` read
that stream with algorithms that are part of the same contract: whole bytes, little-endian, integers by bitmask
rejection, doubles from 53 bits, Fisher–Yates shuffles from the top index down. `docs/draws.md` specifies all of it,
with a Python reference implementation, and the tests pin the C# output to that implementation's.

A change to any of these algorithms is a new algorithm name, never an edit of `/1`.

## Options considered

- **Hash the pulse with a counter (SHA-512 in counter mode).** Equally simple, but less standard; HMAC_DRBG is already
  implemented, NIST-tested and specified.
- **Hand users the raw 64 bytes.** Every user would invent a biased sampler, often a modulo.

## Consequences

- `VerifiableRandom` is not `ISecureRandom`
  ([ADR-1000](1000-beacon-randomness-is-mixed-into-a-local-generator-and-never-used-alone-for-secrets.md)).
- The sampling algorithms of `RandomSourceExtensions` cannot be optimized in a way that changes the bytes they read.

## References

- `docs/draws.md`; `src/NCode.Randomness/Draws/`, `src/NCode.Randomness/Sampling/`.
