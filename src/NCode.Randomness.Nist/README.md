# NCode.Randomness.Nist

The [NIST Randomness Beacon 2.0](https://beacon.nist.gov/home) adapter for NCode.Randomness. The beacon of the National
Institute of Standards and Technology publishes a signed, hash-chained pulse of 512 random bits every minute. This
package fetches pulses of its chain 2, registered as the beacon `nist`, and verifies each one before any byte is used:

- the pulse's certificate is one of the pinned identifiers in `NistDefaults.CertificateIds`, each the SHA-512 hash of
  a certificate's DER encoding, so no server, mirror or certificate authority can substitute another key; the
  certificate holds an RSA key of at least 2048 bits and its validity covers the pulse's timestamp;
- the pulse is version `2.0` with cipher suite 0 (RSA PKCS #1 v1.5 with SHA-512) on the pinned chain, and its signature
  has the exact length of the certificate's modulus and verifies over the pulse's signed fields, which the adapter
  rebuilds byte for byte as the NIST XSD lists them;
- the pulse's output value is the SHA-512 hash of its signed bytes and its signature;
- the pulse is used only with its verified predecessor, on the same chain, one index earlier and earlier in time: the
  pulse's first list value, of type `previous`, is the predecessor's output value, and the SHA-512 hash of the pulse's
  local random value is the predecessor's precommitment value.

Because every pulse needs a predecessor, a round below 2 is reported as not found. Members of a pulse that are not
signed are ignored.

The randomness is the pulse's 64-byte output value. The beacon's pulse carries the chain index `2` as its chain, the
pulse index as its round, the output value in uppercase hex as its identifier, and the pulse's timestamp.

```csharp
builder.Services
    .AddRandomness(builder.Configuration.GetSection(RandomnessOptions.SectionName))
    .AddNistBeacon(builder.Configuration.GetSection(NistBeaconOptions.SectionName));
```

Settings bind from `RandomnessBeacons:Nist`: `BaseAddress` (default `https://beacon.nist.gov/beacon/2.0/`; a mirror can
withhold pulses but never forge one), `ChainIndex` (default 2), `CertificateIds` (empty by default, which trusts
`NistDefaults.CertificateIds`; a configured list replaces them) and `MaxResponseBytes` (default 64 KiB). Requests use a
named `HttpClient`, `NistDefaults.HttpClientName`, with retries, attempt and total timeouts and a circuit breaker; add a
proxy or other handlers to it with `services.AddHttpClient(NistDefaults.HttpClientName)`.

The pinned certificates are not validated as a WebPKI chain, which would trust any public certificate authority's
certificate for the name; they are trusted by identifier alone, as
[ADR-1009](https://github.com/NCodeGroup/Randomness/blob/main/docs/decisions/1009-nist-pulses-are-verified-against-pinned-certificates-and-their-hash-chain-before-any-byte-is-used.md)
records.

## Certificate rotation

NIST replaces its signing certificate about once a year, and the current one expires on 2027-03-20. The beacon refuses
a pulse signed with a certificate that is not pinned, and a release pins the next certificate only once NIST publishes
it. Until one does, list every identifier to trust under `RandomnessBeacons:Nist:CertificateIds`, which replaces the
defaults, after checking that:

- `https://beacon.nist.gov/beacon/2.0/certificate/<id>` is NIST's `engine.beacon.nist.gov` certificate; and
- the SHA-512 hash of its DER encoding is the identifier.

The pulses 1925734 to 1996051 are signed by a certificate whose identifier is not pinned, so the beacon refuses them.
