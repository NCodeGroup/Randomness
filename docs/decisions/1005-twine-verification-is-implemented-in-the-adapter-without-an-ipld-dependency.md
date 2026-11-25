# 1005. Twine verification is implemented in the adapter without an IPLD dependency

- **Status:** Accepted (amended by 1007)
- **Date:** 2026-11-25
- **Deciders:** NCode Group maintainers

## Context

Verifying a CURBy pulse ([ADR-1003](1003-curby-pulses-are-verified-against-a-pinned-chain-before-any-byte-is-used.md))
needs a DAG-JSON reader, a canonical DAG-CBOR encoder, CIDv1 parsing with base32 multibase, SHA3-512 and RS256. .NET
has RS256 and has SHA3-512 only where the operating system provides it: not on Windows 10 or Windows Server 2022.
.NET IPLD libraries exist but are small, sparsely maintained, and would put a dependency on the trust path.

## Decision

The adapter implements the subset Twine uses, internally: a strict DAG-JSON reader (duplicate keys, floats,
non-canonical base64 or base32 and misuse of the reserved `/` key are refused), a DAG-CBOR encoder (shortest heads,
keys sorted by length then bytes, links as tag 42), CIDv1 with DAG-CBOR and SHA3-512 only, and SHA3-512 through the
platform where `SHA3_512.IsSupported` and through a managed Keccak-f[1600] sponge elsewhere.

## Options considered

- **An IPLD package.** More code than needed, a supply-chain dependency on the trust path, and still no SHA3 fallback.
- **Require a platform with SHA3-512.** Would exclude Windows Server 2022, still a common server.

## Consequences

- The encoder is proved by recomputing the CIDs of documents CURBy served, which CURBy's own implementation produced;
  the managed SHA3 is proved against digests from an independent implementation and against the platform's.
- The subset is small and fails closed: a document using an IPLD feature outside it is refused, not misread.

## References

- DAG-CBOR and DAG-JSON specifications: https://ipld.io/specs/codecs/; FIPS 202.
- `src/NCode.Randomness.Curby/Twine/`.
