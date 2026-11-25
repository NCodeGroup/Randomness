# 1003. CURBy pulses are verified against a pinned chain before any byte is used

- **Status:** Accepted
- **Date:** 2026-11-25
- **Deciders:** NCode Group maintainers

## Context

CURBy publishes through the Twine protocol (`specification: twine/1.0.x`), served by `https://api.entwine.me`. A chain
document holds the chain's RSA public key as a JWK; each pulse is a DAG-CBOR block whose content identifier (CID) is
the SHA3-512 hash of the block, signed with RS256 by the chain's key. Each pulse's payload holds a 64-byte `salt` and a
precommitment `pre`. The CURBy-RNG chain publishes a pulse every minute; its randomness is the 64-byte SHA3-512 digest
in the pulse's CID. Facts read from CURBy's client library (`@buff-beacon-project/curby-client` 1.2.0) and the
`@twine-protocol/twine-core` 0.1.0 sources, and from the API, on 2026-11-25.

## Decision

The CURBy adapter makes exactly the checks CURBy's own client library makes, and refuses a pulse on any failure:

1. The chain document hashes to the **pinned** chain CID (`CurbyDefaults.RngChainId`, configurable), which fixes its
   key: no server, mirror or certificate authority can substitute another key. Its specification is twine 1.x, rng 1.x,
   and its key is RSA of at least 2048 bits.
2. The chain, the pulse and the pulse's predecessor each hash to their declared CIDs (DAG-CBOR, SHA3-512, CIDv1), and
   each signature is a compact JWS whose header names only `RS256` (with optional `typ` or `kid`), over the multihash
   of the SHA3-512 of the content's DAG-CBOR. RS256 is required because a deterministic signature leaves the beacon no
   choice that changes the CID, so it cannot grind its own randomness.
3. The CID, and so the randomness, must be fixed by what is signed. A document holds exactly `cid` and `data`, and
   `data` exactly `content` and `signature`; every JWS segment is canonical base64url, and the signature is exactly
   the modulus length. Otherwise a server could add a member or respell the signature, leave the signed content
   untouched, and grind the CID to pick a draw's outcome.
4. The predecessor is the pulse's first link and exactly one round earlier, and the pulse's salt satisfies the
   predecessor's precommitment: SHA3-512(salt XOR the predecessor's digest) equals the predecessor's `pre`.

## Options considered

- **Trust TLS and the API.** A compromised or mirrored API could serve any bytes.
- **Fetch the key from the API each time.** The key would be as trustworthy as the API; pinning the CID is free.
- **Use the CURBy-Q (DIRNG) chain directly.** Its quantum certificate needs CURBy's Trevisan extractor to verify, which
  CURBy's own client library also leaves to a separate tool; the CURBy-RNG chain mixes the quantum chains' pulses in
  through its mixins, and is what that library serves by default.

## Consequences

- The adapter carries a strict DAG-JSON reader, a DAG-CBOR encoder, CID parsing and SHA3-512
  ([ADR-1005](1005-twine-verification-is-implemented-in-the-adapter-without-an-ipld-dependency.md)), tested against
  documents CURBy served.
- If CURBy ever rotates to a new chain, the pin is changed deliberately, in a release or in configuration.

## References

- https://random.colorado.edu/api; https://github.com/buff-beacon-project/curby-js-client (`src/extract-randomness.ts`).
- `src/NCode.Randomness.Curby/`.
