# 1010. drand rounds are verified against the pinned quicknet key with a managed BLS12-381 library

- **Status:** Accepted
- **Date:** 2026-11-25
- **Deciders:** NCode Group maintainers

## Context

drand, run by the League of Entropy, publishes threshold BLS signatures. The quicknet chain (hash `52db9ba7…`, a round
every 3 seconds, genesis 2023-08-23T15:09:27Z, scheme `bls-unchained-g1-rfc9380`) signs each round number, and the
signature is the round's randomness source. The default chain `8990e7a9…` is chained, takes 30 seconds per round and
signs with G2. The API is `https://api.drand.sh/{hash}/public/{round|latest}`, mirrored at `api2.drand.sh`,
`api3.drand.sh` and `drand.cloudflare.com`; a future round answers 425 and `/public/0` returns the latest round.

.NET has no BLS12-381. `Nethermind.Crypto.Bls` 0.3.17.15 binds blst natively, for Windows, glibc Linux and macOS only.
`Neo.Cryptography.BLS12_381` 3.9.0, Neo's managed port of zkcrypto's `bls12_381`, has the field and curve arithmetic,
subgroup-checked decoding and the pairing, but no hash to curve. Facts read from the drand API and package metadata on
2026-11-25.

## Decision

The drand adapter follows quicknet only, with its hash, G2 public key, genesis and period as constants in
`DrandDefaults`; it fetches no info document. A round verifies when `e(sig, g2) == e(H(SHA-256(round as 8-byte
big-endian)), key)`, where H is RFC 9380 `BLS12381G1_XMD:SHA-256_SSWU_RO_` with the domain separation tag
`BLS_SIG_BLS12381G1_XMD:SHA-256_SSWU_RO_NUL_`, and the signature is not the identity.

The randomness is the SHA-256 of the signature, the `Id` is the signature in lowercase hex, the `Chain` is the chain
hash, and the timestamp is the genesis plus (round − 1) × 3 seconds.

BLS12-381 arithmetic and the pairing come from `Neo.Cryptography.BLS12_381` 3.9.0, which is managed and MIT-licensed
(released 2026-01-17). The hash to G1 is implemented in the adapter: `expand_message_xmd` with SHA-256, the simplified
SWU map on the 11-isogenous curve, the 11-isogeny, and the library's cofactor clearing. It is pinned to the RFC 9380
vectors (K.1 and J.9.1) and to rounds drand served.

## Options considered

- **`Nethermind.Crypto.Bls`.** Native code would sit on the trust path, and there is no musl build.
- **Implement the pairing as well.** Far more code would sit on the trust path for no gain over a maintained library.
- **The chained default chain.** It is slower, and chaining adds nothing to unique threshold signatures.

## Consequences

- The adapter is pure managed code and runs wherever .NET runs.
- A verification costs about 0.15 seconds per round (an estimate, 2026-11-25).
- The library is on the trust path. Dependabot keeps it current and a test pins its subgroup check.
- A successor to quicknet is pinned in a release.
- [ADR-1005](1005-twine-verification-is-implemented-in-the-adapter-without-an-ipld-dependency.md) is the precedent for
  carrying a verification in the adapter instead of a dependency.

## References

- RFC 9380; https://api.drand.sh/52db9ba70e0cc0f6eaf7803dd07447a1f5477735fd3f661792ba94600c84e971/info;
  https://github.com/neo-project/Neo.Cryptography.BLS12_381.
- [ADR-1002](1002-beacons-are-adapters-collected-in-the-background-and-an-outage-degrades-never-fails.md);
  [ADR-1005](1005-twine-verification-is-implemented-in-the-adapter-without-an-ipld-dependency.md); issue #2.
- `src/NCode.Randomness.Drand/`.
