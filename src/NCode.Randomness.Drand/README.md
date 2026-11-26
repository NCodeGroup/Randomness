# NCode.Randomness.Drand

[![CI](https://github.com/NCodeGroup/Randomness/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/NCodeGroup/Randomness/actions/workflows/ci.yml?query=branch%3Amain) [![Target framework](https://img.shields.io/badge/dynamic/xml?url=https%3A%2F%2Fraw.githubusercontent.com%2FNCodeGroup%2FRandomness%2Fmain%2Fsrc%2FNCode.Randomness.Drand%2FNCode.Randomness.Drand.csproj&query=%2F%2FTargetFramework%20%7C%20%2F%2FTargetFrameworks&label=target&logo=dotnet&color=512BD4)](https://www.nuget.org/packages/NCode.Randomness.Drand#supportedframeworks-body-tab) [![Documentation](https://img.shields.io/badge/docs-randomness.ncode.io-512BD4)](https://randomness.ncode.io/) [![License](https://img.shields.io/github/license/NCodeGroup/Randomness)](https://github.com/NCodeGroup/Randomness/blob/main/LICENSE.txt)

The [drand](https://drand.love/) adapter for NCode.Randomness. drand is a distributed randomness beacon run by the
League of Entropy, a group of independent organizations that jointly sign each round with a BLS12-381 threshold
signature. This package fetches rounds of the quicknet chain, which publishes a round every 3 seconds, registered as
the beacon `drand`, and verifies each one before any byte is used:

- every document's text is well-formed Unicode before any member is read;
- the round's signature is a point of the BLS12-381 group G1, other than the identity;
- the signature verifies against the quicknet group public key pinned in `DrandDefaults.PublicKey`, so no server or
  mirror can substitute another key: the pairing `e(signature, g2)` must equal `e(H(message), key)`, where the message
  is the SHA-256 hash of the round number as 8 big-endian bytes and `H` is the hash to curve of RFC 9380,
  `BLS12381G1_XMD:SHA-256_SSWU_RO_`, with the domain separation tag `BLS_SIG_BLS12381G1_XMD:SHA-256_SSWU_RO_NUL_`;
- when the response carries a `randomness` member, it is the SHA-256 hash of the signature;
- the response answers the round that was asked for.

Only the quicknet chain is supported: its chain hash, group public key, genesis time and period are constants in
`DrandDefaults`, and no chain info document is fetched. Other members of a round that the chain does not sign are
ignored.

The randomness is the 32-byte SHA-256 hash of the round's signature. The beacon's pulse carries the quicknet chain hash
as its chain, the round number as its round, the signature in lowercase hex as its identifier, and the time the round
is due: the genesis time plus 3 seconds for each round before it. Rounds start at 1, so a round below 1 is reported as
not found.

## Installation

```shell
dotnet add package NCode.Randomness.Drand
```

The package depends on NCode.Randomness, so adding it brings the generator too. It targets `net10.0`.

## Registration

`AddDrandBeacon()` registers the drand beacon:

<!-- snippet: DrandRegistration -->
```cs
builder
    .Services.AddRandomness(
        builder.Configuration.GetSection(RandomnessOptions.SectionName)
    )
    .AddDrandBeacon(
        builder.Configuration.GetSection(DrandBeaconOptions.SectionName)
    );
```
<!-- endSnippet -->

Settings bind from `RandomnessBeacons:Drand`: `BaseAddress` (default `https://api.drand.sh/`) and `MaxResponseBytes`
(default 16 KiB; a round is about 200 bytes). The same chain is served by other relays, such as
`https://api2.drand.sh/`, `https://api3.drand.sh/` and `https://drand.cloudflare.com/`. Because every round is verified
against the pinned group public key, a mirror can withhold rounds but never forge one. Requests use a named
`HttpClient`, `DrandDefaults.HttpClientName`, with retries, attempt and total timeouts and a circuit breaker; add a
proxy or other handlers to it with `services.AddHttpClient(DrandDefaults.HttpClientName)`.

## Verifying saved documents

`DrandRoundVerifier` makes the same checks without the network, on a round a caller supplies as the UTF-8 JSON the
API serves at `<chain hash>/public/<round>`. It pins the quicknet chain, as the beacon does, so the round is the
only document it needs, and returns the pulse the beacon returns for that round, or throws a `BeaconException` with
`InvalidPulse`. Pass the pulse to `VerifiableRandom.Create` to recompute a published draw, as
[docs/draws.md](https://github.com/NCodeGroup/Randomness/blob/main/docs/draws.md) describes:

<!-- snippet: DrandVerify -->
```cs
var pulse = new DrandRoundVerifier().Verify(roundJson);
using var draw = VerifiableRandom.Create(pulse, "example-raffle-2026-11-25");
```
<!-- endSnippet -->

## BLS12-381

The pairing arithmetic comes from the managed `Neo.Cryptography.BLS12_381` library, so the package has no native
dependency and runs wherever .NET does. That library does not hash to the curve, so the adapter implements the RFC 9380
hash to G1 itself, and its tests pin the result to the RFC's test vectors. Verifying one round takes about 0.15 seconds.
[ADR-1010](https://github.com/NCodeGroup/Randomness/blob/main/docs/decisions/1010-drand-rounds-are-verified-against-the-pinned-quicknet-key-with-a-managed-bls12-381-library.md)
records the choice.

## Learn more

- [Add beacons](https://randomness.ncode.io/guides/add-beacons/): register the drand beacon next to the others
- [Check a draw](https://randomness.ncode.io/guides/check-a-draw/): recompute a published draw from saved documents
- [Beacon reference](https://randomness.ncode.io/reference/beacons/): what each beacon publishes and how it is verified
- [Configuration reference](https://randomness.ncode.io/reference/configuration/): every setting, including `RandomnessBeacons:Drand`
- [Documentation site](https://randomness.ncode.io/)
- [Reproducible draws](https://github.com/NCodeGroup/Randomness/blob/main/docs/draws.md): the procedure and the exact algorithm
- [Changelog](https://github.com/NCodeGroup/Randomness/blob/main/CHANGELOG.md)
