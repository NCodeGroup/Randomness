# NCode.Randomness.Curby

The [CURBy](https://random.colorado.edu/) adapter for NCode.Randomness. CURBy, the CU Randomness Beacon of the
University of Colorado Boulder, publishes randomness certified by a loophole-free Bell test through the Twine protocol.
This package fetches pulses of its CURBy-RNG chain, one a minute, and verifies each one before any byte is used:

- the chain document hashes to the pinned content identifier `CurbyDefaults.RngChainId`, which fixes the chain's RSA
  key, so no server, mirror or certificate authority can substitute another;
- the pulse and its predecessor each hash to their content identifiers (DAG-CBOR, SHA3-512), hold only the members
  Twine defines, and carry a canonically encoded RS256 signature by the chain's key, so no server can change a
  pulse's randomness without breaking a check;
- the predecessor is the pulse's first link and one round earlier, and the pulse's salt matches the predecessor's
  precommitment.

The randomness is the pulse's 64-byte SHA3-512 digest, as CURBy's own client library defines it. SHA3-512 comes from
the operating system where it has one and from a managed implementation elsewhere, such as Windows Server 2022.

```csharp
builder.Services
    .AddRandomness(builder.Configuration.GetSection(RandomnessOptions.SectionName))
    .AddCurbyBeacon(builder.Configuration.GetSection(CurbyBeaconOptions.SectionName));
```

Settings bind from `RandomnessBeacons:Curby`: `BaseAddress` (default `https://api.entwine.me/`; a mirror can withhold
pulses but never forge one), `ChainId`, `QuantumChainId`, `BellChainId` and `MaxResponseBytes`. Requests use a named
`HttpClient`, `CurbyDefaults.HttpClientName`, with retries, attempt and total timeouts and a circuit breaker; add a
proxy or other handlers to it with `services.AddHttpClient(CurbyDefaults.HttpClientName)`.

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

The beacon does not verify the Bell-test certificate or the Trevisan extraction in process. The extractor is GPL-licensed
code with no specification, and a round needs about 9 MB of raw Bell data, so
[ADR-1007](https://github.com/NCodeGroup/Randomness/blob/main/docs/decisions/1007-curby-q-rounds-are-verified-as-curbys-client-verifies-them-and-their-extraction-is-left-to-curby-verify.md)
leaves them to CURBy's [`curby_verify`](https://github.com/buff-beacon-project/curby_verify) tool, which checks a
round with `docker compose run --rm verify <round>`. The round number of a draw's pulse is the one to give it. Those
bytes are only additional input to the generator and never the sole source of a secret, so what stays unverified does
not weaken one.

CURBy-Q completes a round about every 13 minutes while it runs, longer than the default `Randomness:MaxPulseAge` of 10
minutes. `AddCurbyQuantumBeacon()` allows `curby-q` pulses up to 30 minutes old through
`Randomness:MaxPulseAgeByBeacon:curby-q`, which a host can set itself, and leaves every other beacon's limit as it is.
CURBy-Q has published no round since 2025-08-22 (as of 2026-11-25), so while it stays stalled the beacon reports stale
pulses and the health check reports `Degraded`; draws over published rounds are unaffected.

SHAKE256 comes from the operating system where it has one and from a managed implementation elsewhere, as SHA3-512
does.
