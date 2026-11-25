# NCode.Randomness.Drand

The [drand](https://drand.love/) adapter for NCode.Randomness. drand is a distributed randomness beacon run by the
League of Entropy, a group of independent organizations that jointly sign each round with a BLS12-381 threshold
signature. This package fetches rounds of the quicknet chain, which publishes a round every 3 seconds, registered as
the beacon `drand`, and verifies each one before any byte is used:

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

```csharp
builder.Services
    .AddRandomness(builder.Configuration.GetSection(RandomnessOptions.SectionName))
    .AddDrandBeacon(builder.Configuration.GetSection(DrandBeaconOptions.SectionName));
```

Settings bind from `RandomnessBeacons:Drand`: `BaseAddress` (default `https://api.drand.sh/`) and `MaxResponseBytes`
(default 16 KiB; a round is about 200 bytes). The same chain is served by other relays, such as
`https://api2.drand.sh/`, `https://api3.drand.sh/` and `https://drand.cloudflare.com/`. Because every round is verified
against the pinned group public key, a mirror can withhold rounds but never forge one. Requests use a named
`HttpClient`, `DrandDefaults.HttpClientName`, with retries, attempt and total timeouts and a circuit breaker; add a
proxy or other handlers to it with `services.AddHttpClient(DrandDefaults.HttpClientName)`.

## BLS12-381

The pairing arithmetic comes from the managed `Neo.Cryptography.BLS12_381` library, so the package has no native
dependency and runs wherever .NET does. That library does not hash to the curve, so the adapter implements the RFC 9380
hash to G1 itself, and its tests pin the result to the RFC's test vectors. Verifying one round takes about 0.15 seconds.
[ADR-1010](https://github.com/NCodeGroup/Randomness/blob/main/docs/decisions/1010-drand-rounds-are-verified-against-the-pinned-quicknet-key-with-a-managed-bls12-381-library.md)
records the choice.
