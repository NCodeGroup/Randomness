# NCode.Randomness

Cryptographically secure random numbers for .NET, strengthened by public randomness beacons such as
[CURBy](https://random.colorado.edu/), and reproducible draws that anyone can check against a beacon pulse.

- **`ISecureRandom`**: an NIST SP 800-90A HMAC_DRBG (HMAC-SHA-512) per processor, seeded and reseeded from the
  operating system's CSPRNG, with every verified beacon pulse mixed in as additional input. Its output is at least as
  strong as the operating system's generator whatever a beacon publishes, and a call never waits on the network.
- **Typed values over any source**: unbiased integers in any range for every integer type from `byte` to `UInt128`,
  doubles, singles, booleans, version 4 GUIDs, shuffles, choices, strings from an alphabet, and a `System.Random`
  adapter.
- **Beacons in the background**: one loop per registered beacon, with timeouts, back-off and jitter; `Continuous`,
  `InitializeOnly` (start online, then run offline) and `Offline` modes; rollback, equivocation and staleness checks;
  a health check that reports an outage as `Degraded`; metrics through `IMeterFactory`.
- **Reproducible draws**: `IVerifiableRandomFactory` derives a deterministic stream, `ncode-randomness-draw/1`, from
  one verified pulse and a context label, for raffles, audits and sortition. It is deliberately not an
  `ISecureRandom`.
- **`SystemRandomSource.Instance`**: the operating system's generator behind the same API, without dependency
  injection.

```csharp
builder.Services
    .AddRandomness(builder.Configuration.GetSection(RandomnessOptions.SectionName))
    .AddCurbyBeacon(); // from NCode.Randomness.Curby

public sealed class TokenService(ISecureRandom random)
{
    public byte[] NewKey() => random.GetBytes(32);

    public int RollDie() => random.NextInteger(1, 7);
}
```

Beacon adapters implement `IRandomnessBeacon` and register with `AddBeacon<TBeacon>()`. The repository's README and
`docs/` folder hold the full guide, the draw specification and the decision records.
