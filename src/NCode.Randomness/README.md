# NCode.Randomness

Cryptographically secure random numbers for .NET, strengthened by public randomness beacons such as
[CURBy](https://random.colorado.edu/), the [NIST Randomness Beacon](https://beacon.nist.gov/) and
[drand](https://drand.love/), and reproducible draws that anyone can check against a beacon pulse.

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
- **`RandomnessHost`**: the same generator and beacons without dependency injection, for console tools and
  libraries: build it with `RandomnessHost.CreateBuilder()` and `BuildAsync`, and dispose it to stop the beacons.
- **`SystemRandomSource.Instance`**: the operating system's generator behind the same API, with no beacon and
  nothing to dispose.

```csharp
builder.Services
    .AddRandomness(builder.Configuration.GetSection(RandomnessOptions.SectionName))
    .AddCurbyBeacon(); // from NCode.Randomness.Curby

public sealed class TokenService(ISecureRandom random)
{
    public string NewRecoveryCode() => random.GetString("ABCDEFGHJKLMNPQRSTUVWXYZ23456789", 16);

    public int RollDie() => random.NextInteger(1, 7);
}
```

For a key, call `Fill` with a `stackalloc` or pinned span and clear it with `CryptographicOperations.ZeroMemory` after
use, rather than keeping it in the `byte[]` that `GetBytes` returns. A key that outlives one method belongs in a pinned
buffer that is zeroed on dispose, such as one from [NCode.Buffers](https://www.nuget.org/packages/NCode.Buffers/)'
`BufferFactory.Rent(32, isSensitive: true, out Span<byte> key)`.

Each beacon has an adapter package of its own, which verifies every pulse against a pinned trust anchor before any
byte is used:

| Package | Beacon |
|---|---|
| [NCode.Randomness.Curby](https://www.nuget.org/packages/NCode.Randomness.Curby/) | CURBy's CURBy-RNG and CURBy-Q chains, verified against pinned chains |
| [NCode.Randomness.Nist](https://www.nuget.org/packages/NCode.Randomness.Nist/) | The NIST Randomness Beacon 2.0, verified against pinned certificates and its hash chain |
| [NCode.Randomness.Drand](https://www.nuget.org/packages/NCode.Randomness.Drand/) | drand's quicknet chain, verified against the League of Entropy's pinned BLS key |

Other beacon adapters implement `IRandomnessBeacon` and register with `AddBeacon<TBeacon>()`. The
[repository](https://github.com/NCodeGroup/Randomness) holds the full guide, the draw specification and the decision
records.
