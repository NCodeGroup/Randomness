# NCode.Randomness.Nist

[![CI](https://github.com/NCodeGroup/Randomness/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/NCodeGroup/Randomness/actions/workflows/ci.yml?query=branch%3Amain) [![Target framework](https://img.shields.io/badge/dynamic/xml?url=https%3A%2F%2Fraw.githubusercontent.com%2FNCodeGroup%2FRandomness%2Fmain%2Fsrc%2FNCode.Randomness.Nist%2FNCode.Randomness.Nist.csproj&query=%2F%2FTargetFramework%20%7C%20%2F%2FTargetFrameworks&label=target&logo=dotnet&color=512BD4)](https://www.nuget.org/packages/NCode.Randomness.Nist#supportedframeworks-body-tab) [![Documentation](https://img.shields.io/badge/docs-randomness.ncode.io-512BD4)](https://randomness.ncode.io/) [![License](https://img.shields.io/github/license/NCodeGroup/Randomness)](https://github.com/NCodeGroup/Randomness/blob/main/LICENSE.txt)

The [NIST Randomness Beacon 2.0](https://beacon.nist.gov/home) adapter for NCode.Randomness. The beacon of the National
Institute of Standards and Technology publishes a signed, hash-chained pulse of 512 random bits every minute. This
package fetches pulses of its chain 2, registered as the beacon `nist`, and verifies each one before any byte is used:

- the pulse's certificate is one of the pinned identifiers in `NistDefaults.CertificateIds`, each the SHA-512 hash of
  a certificate's DER encoding, so no server, mirror or certificate authority can substitute another key; the
  certificate holds an RSA key of at least 2048 bits and its validity covers the pulse's timestamp;
- every document's text is well-formed Unicode before any member is read;
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

## Installation

```shell
dotnet add package NCode.Randomness.Nist
```

The package depends on NCode.Randomness, so adding it brings the generator too. It targets `net10.0`.

## Registration

`AddNistBeacon()` registers the NIST beacon:

<!-- snippet: NistRegistration -->
```cs
var configuration = builder.Configuration;
var randomness = configuration.GetSection(RandomnessOptions.SectionName);
var nist = configuration.GetSection(NistBeaconOptions.SectionName);
builder.Services.AddRandomness(randomness).AddNistBeacon(nist);
```
<!-- endSnippet -->

Settings bind from `RandomnessBeacons:Nist`: `BaseAddress` (default `https://beacon.nist.gov/beacon/2.0/`; a mirror can
withhold pulses but never forge one), `ChainIndex` (default 2), `CertificateIds` (empty by default, which trusts
`NistDefaults.CertificateIds`; a configured list replaces them), `CertificateRetryDelay` (default 1 minute; how long a
failed certificate fetch is remembered before a pulse that names the certificate tries again; each certificate is
fetched once, under its own 30-second timeout, for every caller) and `MaxResponseBytes` (default 64 KiB). Requests use a
named `HttpClient`, `NistDefaults.HttpClientName`, with retries, attempt and total timeouts and a circuit breaker; add a
proxy or other handlers to it with `services.AddHttpClient(NistDefaults.HttpClientName)`.

The pinned certificates are not validated as a WebPKI chain, which would trust any public certificate authority's
certificate for the name; they are trusted by identifier alone, as
[ADR-1009](https://github.com/NCodeGroup/Randomness/blob/main/docs/decisions/1009-nist-pulses-are-verified-against-pinned-certificates-and-their-hash-chain-before-any-byte-is-used.md)
records.

## Verifying saved documents

`NistPulseVerifier` makes the same checks without the network, on documents a caller supplies: a pulse and its
predecessor, each the UTF-8 JSON the API serves at `chain/<chain>/pulse/<index>`, and the PEM certificate it serves
at `certificate/<id>` for the identifier the pulse names, with the predecessor's too when it names another one, as
across a certificate rotation. It pins chain 2 and `NistDefaults.CertificateIds`, or the chain index and
certificate identifiers its constructor takes, and returns the pulse the beacon returns for that index, or throws a
`BeaconException` with `InvalidPulse`. A certificate must cover the pulse's timestamp, not today, so a pulse stays
checkable after its certificate expires. Pass the pulse to `VerifiableRandom.Create` to recompute a published draw,
as [docs/draws.md](https://github.com/NCodeGroup/Randomness/blob/main/docs/draws.md) describes:

<!-- snippet: NistVerify -->
```cs
var pulse = new NistPulseVerifier().Verify(
    pulseJson,
    predecessorJson,
    certificate
);
using var draw = VerifiableRandom.Create(pulse, "example-raffle-2026-11-25");
```
<!-- endSnippet -->

## Certificate rotation

NIST replaces its signing certificate about once a year, and the current one expires on 2027-03-20. The beacon refuses
a pulse signed with a certificate that is not pinned, and a release pins the next certificate only once NIST publishes
it. Until one does, list every identifier to trust under `RandomnessBeacons:Nist:CertificateIds`, which replaces the
defaults, after checking that:

- `https://beacon.nist.gov/beacon/2.0/certificate/<id>` is NIST's `engine.beacon.nist.gov` certificate; and
- the SHA-512 hash of its DER encoding is the identifier.

The pulses 1925734 to 1996050 name a certificate that does not verify them, so the beacon refuses them, and it refuses
pulse 1996051 too, since a pulse is used only with its verified predecessor.

## Learn more

- [Add beacons](https://randomness.ncode.io/guides/add-beacons/): register the NIST beacon next to the others
- [Check a draw](https://randomness.ncode.io/guides/check-a-draw/): recompute a published draw from saved documents
- [Beacon reference](https://randomness.ncode.io/reference/beacons/): what each beacon publishes and how it is verified
- [Configuration reference](https://randomness.ncode.io/reference/configuration/): every setting, including `RandomnessBeacons:Nist`
- [Documentation site](https://randomness.ncode.io/)
- [Reproducible draws](https://github.com/NCodeGroup/Randomness/blob/main/docs/draws.md): the procedure and the exact algorithm
- [Changelog](https://github.com/NCodeGroup/Randomness/blob/main/CHANGELOG.md)
