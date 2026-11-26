# NCode.Randomness.Curby

[![CI](https://github.com/NCodeGroup/Randomness/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/NCodeGroup/Randomness/actions/workflows/ci.yml?query=branch%3Amain) [![Target framework](https://img.shields.io/badge/dynamic/xml?url=https%3A%2F%2Fraw.githubusercontent.com%2FNCodeGroup%2FRandomness%2Fmain%2Fsrc%2FNCode.Randomness.Curby%2FNCode.Randomness.Curby.csproj&query=%2F%2FTargetFramework%20%7C%20%2F%2FTargetFrameworks&label=target&logo=dotnet&color=512BD4)](https://www.nuget.org/packages/NCode.Randomness.Curby#supportedframeworks-body-tab) [![Documentation](https://img.shields.io/badge/docs-randomness.ncode.io-512BD4)](https://randomness.ncode.io/) [![License](https://img.shields.io/github/license/NCodeGroup/Randomness)](https://github.com/NCodeGroup/Randomness/blob/main/LICENSE.txt)

The [CURBy](https://random.colorado.edu/) adapter for NCode.Randomness. CURBy, the CU Randomness Beacon of the
University of Colorado Boulder, publishes randomness certified by a loophole-free Bell test through the Twine protocol.
This package fetches pulses of its CURBy-RNG chain, one a minute, and verifies each one before any byte is used:

- every document's text is well-formed Unicode before any member is read;
- the chain document hashes to the pinned content identifier `CurbyDefaults.RngChainId`, which fixes the chain's RSA
  key, so no server, mirror or certificate authority can substitute another;
- the pulse and its predecessor each hash to their content identifiers (DAG-CBOR, SHA3-512), hold only the members
  Twine defines, and carry a canonically encoded RS256 signature by the chain's key, so no server can change a
  pulse's randomness without breaking a check;
- the predecessor is the pulse's first link and one round earlier, and the pulse's salt matches the predecessor's
  precommitment.

The randomness is the pulse's 64-byte SHA3-512 digest, as CURBy's own client library defines it. SHA3-512 comes from
the operating system where it has one and from a managed implementation elsewhere, such as Windows Server 2022.

## Installation

```shell
dotnet add package NCode.Randomness.Curby
```

The package depends on NCode.Randomness, so adding it brings the generator too. It targets `net10.0`.

## Registration

`AddCurbyBeacon()` registers the CURBy-RNG beacon, and `AddCurbyQuantumBeacon()`, described below, registers the
CURBy-Q beacon; this sample registers both:

<!-- snippet: CurbyRegistration -->
```cs
var randomness = builder.Configuration.GetSection(RandomnessOptions.SectionName);
var curby = builder.Configuration.GetSection(CurbyBeaconOptions.SectionName);
builder
    .Services.AddRandomness(randomness)
    .AddCurbyBeacon(curby)
    .AddCurbyQuantumBeacon(curby);
```
<!-- endSnippet -->

Settings bind from `RandomnessBeacons:Curby`: `BaseAddress` (default `https://api.entwine.me/`; a mirror can withhold
pulses but never forge one), `ChainId`, `QuantumChainId`, `BellChainId`, `MaxResponseBytes` and `ChainRetryDelay`
(default 1 minute; how long a failed chain fetch is remembered; each chain is fetched once, under its own 30-second
timeout, for every caller). Requests use a named `HttpClient`, `CurbyDefaults.HttpClientName`, with retries, attempt and
total timeouts and a circuit breaker; add a proxy or other handlers to it with
`services.AddHttpClient(CurbyDefaults.HttpClientName)`.

## Verifying saved documents

`CurbyPulseVerifier` makes the same checks without the network, on documents a caller supplies: the chain, a pulse
and its predecessor, each the UTF-8 JSON the Twine API serves. It pins `CurbyDefaults.RngChainId`, or the chain
identifier its constructor takes, and returns the pulse the beacon returns for that round, or throws a
`BeaconException` with `InvalidPulse`. Pass the pulse to `VerifiableRandom.Create` to recompute a published draw, as
[docs/draws.md](https://github.com/NCodeGroup/Randomness/blob/main/docs/draws.md) describes. This sample recomputes the
reference draw of that page, from recorded documents of round 1620423 and the context `example-raffle-2026-11-25`:

<!-- snippet: CurbyVerify -->
```cs
var pulse = new CurbyPulseVerifier().Verify(
    chainJson,
    pulseJson,
    predecessorJson
);
using var draw = VerifiableRandom.Create(pulse, context);
var bytes = draw.GetBytes(32);
var first = Convert.ToHexStringLower(bytes);
var rolls = Enumerable
    .Range(0, 5)
    .Select(_ => draw.NextInteger(1, 101))
    .ToArray();
// first is e43e360c9e2b9cd6a094e04a64e65922a3581ba6864da1cb1490244522f9986e and
// rolls is 80, 38, 69, 95, 91, the values the reference implementation in
// docs/draws.md prints for this pulse and context.
```
<!-- endSnippet -->

## CURBy-Q

`AddCurbyQuantumBeacon()` registers a second beacon, `curby-q`, which reads the CURBy-Q (DIRNG) chain. It shares the
settings, the `HttpClient` and the Twine verification code with the CURBy-RNG beacon, and the two can be registered
together.

A CURBy-Q round is the set of pulses CURBy publishes for one run of the Bell test: a request, a precommitment and, when
the run succeeds, a randomness pulse. The round's randomness is the 64 bytes in the randomness pulse, the output of the
Trevisan extractor that CURBy publishes. The beacon's pulse carries the DIRNG round number, the randomness pulse's
content identifier and timestamp, and those bytes. A round that failed, or has not completed, gives no pulse.

The beacon refuses a round unless it passes the checks CURBy's own client library makes:

- the chain document hashes to the pinned content identifier `CurbyDefaults.QuantumChainId`, which fixes the chain's RSA
  key;
- every pulse of the round hashes to its content identifier, holds only the members Twine defines, and carries a
  canonically encoded RS256 signature by the chain's key; the pulses are consecutive and carry one round number;
- the Bell chain is the pinned `CurbyDefaults.BellChainId`, and one of the ten Bell pulses up to the one the
  precommitment mixes in answers the round's request with the precommitment's data hash, which the randomness pulse
  carries too;
- the seed chain is the one the signed precommitment names, the seed pulse is the one its rule selects among the seed
  chain's pulses that mix in the precommitment, and each pulse crawled to reach it is the one requested and precedes
  the one after it;
- the round's parameters hash to the randomness pulse's `paramsHash`, and SHAKE256 of the seed pulse's digest, expanded
  to the precommitment's `seedLength` bits, equals the parameters' `seed`.

The beacon does not verify the Bell-test certificate or the Trevisan extraction in process. The extractor is
GPL-licensed code with no specification, and a round needs about 9 MB of raw Bell data, so
[ADR-1007](https://github.com/NCodeGroup/Randomness/blob/main/docs/decisions/1007-curby-q-rounds-are-verified-as-curbys-client-verifies-them-and-their-extraction-is-left-to-curby-verify.md)
leaves them to CURBy's [`curby_verify`](https://github.com/buff-beacon-project/curby_verify) tool, which checks a
round with `docker compose run --rm verify <round>`. The round number of a draw's pulse is the one to give it. Those
bytes are only additional input to the generator and never the sole source of a secret, so what stays unverified does
not weaken one.

CURBy-Q completes a round about every 13 minutes while it runs (as of 2026-11-25, measured as
[ADR-1007](https://github.com/NCodeGroup/Randomness/blob/main/docs/decisions/1007-curby-q-rounds-are-verified-as-curbys-client-verifies-them-and-their-extraction-is-left-to-curby-verify.md)'s
Context records), longer than the default `Randomness:MaxPulseAge` of 10 minutes. `AddCurbyQuantumBeacon()` allows
`curby-q` pulses up to 30 minutes old through `Randomness:MaxPulseAgeByBeacon:curby-q`, which a host can set itself,
and leaves every other beacon's limit as it is. CURBy-Q has published no round since 2025-08-22 (as of 2026-11-25, from
the same record), so while it stays stalled the beacon reports stale pulses and the health check reports `Degraded`;
draws over published rounds are unaffected.

SHAKE256 comes from the operating system where it has one and from a managed implementation elsewhere, as SHA3-512
does.

### Verifying saved rounds

`CurbyQuantumRoundVerifier` makes the same checks without the network, on documents a caller supplies: the round
and its parameters, as the API serves them at `curbyq/round/<round>` and `curbyq/round/<round>/params`, and the
chain and pulse documents the round links to, as it serves them at `chains/<chain>` and
`chains/<chain>/pulses/<cid>`, in any order: the CURBy-Q, Bell and seed chains, the Bell pulses from the one the
precommitment mixes in back to the response, and the seed pulses from the one the randomness pulse mixes in back to
the first that does not mix in the precommitment. It pins `CurbyDefaults.QuantumChainId` and
`CurbyDefaults.BellChainId`, or the chain identifiers its constructor takes, looks up each document the beacon
would fetch by its content identifier, and returns the pulse the beacon returns for that round, or throws a
`BeaconException` with `InvalidPulse`. It leaves the Bell-test certificate and the extraction to `curby_verify`,
as the beacon does:

<!-- snippet: CurbyQuantumVerify -->
```cs
var pulse = new CurbyQuantumRoundVerifier().Verify(
    roundJson,
    parameters,
    documents
);
using var draw = VerifiableRandom.Create(pulse, "example-raffle-2026-11-25");
```
<!-- endSnippet -->

## Learn more

- [Add beacons](https://randomness.ncode.io/guides/add-beacons/): register the CURBy beacons next to the others
- [Check a draw](https://randomness.ncode.io/guides/check-a-draw/): recompute a published draw from saved documents
- [Beacon reference](https://randomness.ncode.io/reference/beacons/): what each beacon publishes and how it is verified
- [Configuration reference](https://randomness.ncode.io/reference/configuration/): every setting, including `RandomnessBeacons:Curby`
- [Documentation site](https://randomness.ncode.io/)
- [Reproducible draws](https://github.com/NCodeGroup/Randomness/blob/main/docs/draws.md): the procedure and the exact algorithm
- [Changelog](https://github.com/NCodeGroup/Randomness/blob/main/CHANGELOG.md)
