# NCode.Randomness.Curby

The [CURBy](https://random.colorado.edu/) adapter for NCode.Randomness. CURBy, the CU Randomness Beacon of the
University of Colorado Boulder, publishes randomness certified by a loophole-free Bell test through the Twine protocol.
This package fetches pulses of its CURBy-RNG chain, one a minute, and verifies each one before any byte is used:

- the chain document hashes to the pinned content identifier `CurbyDefaults.RngChainId`, which fixes the chain's RSA
  key, so no server, mirror or certificate authority can substitute another;
- the pulse and its predecessor each hash to their content identifiers (DAG-CBOR, SHA3-512) and carry an RS256
  signature by the chain's key;
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
pulses but never forge one), `ChainId` and `MaxResponseBytes`. Requests use a named `HttpClient`,
`CurbyDefaults.HttpClientName`, with retries, attempt and total timeouts and a circuit breaker; add a proxy or other
handlers to it with `services.AddHttpClient(CurbyDefaults.HttpClientName)`.
