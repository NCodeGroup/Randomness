# 1009. NIST pulses are verified against pinned certificates and their hash chain before any byte is used

- **Status:** Accepted
- **Date:** 2026-11-25
- **Deciders:** NCode Group maintainers

## Context

`https://beacon.nist.gov/beacon/2.0/` publishes a pulse every 60 seconds on chain 2, which has run since 2022-09-21.
A pulse names its certificate by the SHA-512 of the certificate's DER, and the certificate is served as PEM at
`/certificate/{id}`. The certificates are DigiCert's for `engine.beacon.nist.gov`; each lasts about a year and holds
its own 4096-bit key: `ed8e1b97…` signs pulses 1–459828, `aa1e1ed4…` up to 978884, `e692b0f6…` up to 1484768,
`528943a5…` up to 1925733, and `5b6386e6…` from 1996051 (valid to 2027-03-20). NIST's note of 2026-10-23 says pulses
1925734–1996050 name `87f27f43…`, which does not verify them. The `statusCode` of a pulse acts as a bit field (2
marks a gap, 4 a certificate change); the link to the previous pulse and the precommitment hold across both. Facts read
from the 2.0 XSD, NIST's beacon pages and live pulses on 2026-11-25.

## Decision

The NIST adapter refuses a pulse unless every check below passes:

1. The certificate is **pinned** by identifier: `NistDefaults.CertificateIds` holds every certificate that signs
   chain 2, and a configured `RandomnessBeacons:Nist:CertificateIds` replaces that list. The fetched PEM must hash to
   its identifier, hold an RSA key of at least 2048 bits, and cover the pulse's timestamp. WebPKI chain validation is
   not used, because it trusts any public CA's certificate for the name.
2. The signed bytes are rebuilt as the 2.0 XSD lists them and live pulses confirm, all big-endian: the length-prefixed
   (4 bytes) UTF-8 `uri` and `version`; the 4-byte `cipherSuite` and `period`; the length-prefixed `certificateId`;
   the 8-byte `chainIndex` and `pulseIndex`; the length-prefixed `timeStamp` (raw text) and `localRandomValue`; when
   `external` is an object, the length-prefixed `sourceId`, the 4-byte `statusCode` and the length-prefixed `value`;
   each `listValues[].value`, length-prefixed, in order; the length-prefixed `precommitmentValue`; and the 4-byte
   `statusCode`. The `outputValue` is the SHA-512 of the signed bytes followed by the raw signature.
3. The pulse has version `2.0`, cipher suite 0 (RSA PKCS #1 v1.5 with SHA-512), the pinned chain (default 2), a
   signature of exactly the modulus length that verifies, and a recomputed output equal to its `outputValue`.
4. The pulse is used only with its verified predecessor, on the same chain, with index one less and an earlier
   timestamp: `listValues[0]` (type `previous`) equals the predecessor's output, and the SHA-512 of the pulse's
   `localRandomValue` equals the predecessor's `precommitmentValue`. Rounds below 2 are therefore `PulseNotFound`.

The randomness is the 64-byte output, the `Id` is the output in uppercase hex, the `Chain` is `"2"` and the `Round` is
the pulse index. Unsigned JSON members are ignored.

## Options considered

- **Validate the certificate chain to a public CA and the host name.** Any CA's certificate for the name would pass,
  and an old pulse would need validation as of its own time.
- **Use the certificate the server serves.** The key would be as trustworthy as the server; pinning the identifier is
  free.
- **Skip the predecessor checks.** They are what stop NIST choosing a pulse after the fact, so a pulse is not used
  without them.

## Consequences

- When NIST rotates its certificate (the current one is valid to 2027-03-20), `nist` refuses pulses signed by the
  unpinned certificate and reports `Degraded` until a release or the host pins the rotated certificate's identifier,
  which the error names.
- Pulses 1925734–1996051 are refused, because the certificate they name does not verify them.
- The adapter carries its own length-prefixed encoder and PEM parsing, tested against pulses and certificates NIST
  served.

## References

- https://beacon.nist.gov/ns/beacon/pulse/2.0/beacon-2.0.xsd;
  https://csrc.nist.gov/projects/interoperable-randomness-beacons/beacon-20.
- [ADR-1002](1002-beacons-are-adapters-collected-in-the-background-and-an-outage-degrades-never-fails.md); issue #2.
- `src/NCode.Randomness.Nist/`.
