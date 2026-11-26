# NCode.Randomness

[![CI](https://github.com/NCodeGroup/Randomness/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/NCodeGroup/Randomness/actions/workflows/ci.yml?query=branch%3Amain) [![Target framework](https://img.shields.io/badge/dynamic/xml?url=https%3A%2F%2Fraw.githubusercontent.com%2FNCodeGroup%2FRandomness%2Fmain%2Fsrc%2FNCode.Randomness%2FNCode.Randomness.csproj&query=%2F%2FTargetFramework%20%7C%20%2F%2FTargetFrameworks&label=target&logo=dotnet&color=512BD4)](https://www.nuget.org/packages/NCode.Randomness#supportedframeworks-body-tab) [![Documentation](https://img.shields.io/badge/docs-randomness.ncode.io-512BD4)](https://randomness.ncode.io/) [![License](https://img.shields.io/github/license/NCodeGroup/Randomness)](https://github.com/NCodeGroup/Randomness/blob/main/LICENSE.txt)

Cryptographically secure random numbers for .NET, strengthened by public randomness beacons such as
[CURBy](https://random.colorado.edu/), the [NIST Randomness Beacon](https://beacon.nist.gov/) and
[drand](https://drand.love/), and reproducible draws that anyone can check against a beacon pulse.

## Installation

```shell
dotnet add package NCode.Randomness
```

Beacon adapters are separate packages, each of which depends on this one; the Beacon adapters table below lists them,
and you add only the ones you use. Each package targets `net10.0`.

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

## Example

Register the generator and the beacons you use. The adapters come from their own packages, here
`NCode.Randomness.Curby`, `NCode.Randomness.Nist` and `NCode.Randomness.Drand`:

<!-- snippet: QuickStart -->
```cs
var builder = Host.CreateApplicationBuilder(args);
builder
    .Services.AddRandomness(
        builder.Configuration.GetSection(RandomnessOptions.SectionName)
    )
    .AddCurbyBeacon(
        builder.Configuration.GetSection(CurbyBeaconOptions.SectionName)
    )
    .AddNistBeacon(
        builder.Configuration.GetSection(NistBeaconOptions.SectionName)
    )
    .AddDrandBeacon(
        builder.Configuration.GetSection(DrandBeaconOptions.SectionName)
    );
builder.Services.AddHealthChecks().AddRandomnessBeacons("ready");
```
<!-- endSnippet -->

Inject `ISecureRandom` wherever a secret is made:

<!-- snippet: TokenService -->
```cs
public sealed class TokenService(ISecureRandom random)
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public string NewRecoveryCode() => random.GetString(Alphabet, 16);

    public int RollDie() => random.NextInteger(1, 7);
}
```
<!-- endSnippet -->

For a key, call `Fill` with a `stackalloc` or pinned span and clear it with `CryptographicOperations.ZeroMemory` after
use, rather than keeping it in the `byte[]` that `GetBytes` returns. A key that outlives one method belongs in a pinned
buffer that is zeroed on dispose, such as one from [NCode.Buffers](https://www.nuget.org/packages/NCode.Buffers/)'
`BufferFactory.Rent(32, isSensitive: true, out Span<byte> key)`.

## Beacon adapters

Each beacon has an adapter package of its own, which verifies every pulse against a pinned trust anchor before any
byte is used:

| Package | Beacon |
|---|---|
| [NCode.Randomness.Curby](https://www.nuget.org/packages/NCode.Randomness.Curby/) | CURBy's CURBy-RNG and CURBy-Q chains, verified against pinned chains |
| [NCode.Randomness.Nist](https://www.nuget.org/packages/NCode.Randomness.Nist/) | The NIST Randomness Beacon 2.0, verified against pinned certificates and its hash chain |
| [NCode.Randomness.Drand](https://www.nuget.org/packages/NCode.Randomness.Drand/) | drand's quicknet chain, verified against the League of Entropy's pinned BLS key |

Other beacon adapters implement `IRandomnessBeacon` and register with `AddBeacon<TBeacon>()`.

## Learn more

- [Documentation site](https://randomness.ncode.io/): a quick start, a guide for each task, the rules behind them, a beacon reference and the API map
- [Quick start](https://randomness.ncode.io/start/quick-start/)
- [Which API?](https://randomness.ncode.io/start/which-api/): the generator, a reproducible draw, a host without dependency injection or the operating system's generator
- [API map](https://randomness.ncode.io/reference/api-map/): every public member
- [README and samples](https://github.com/NCodeGroup/Randomness#readme): the full guide, with a tested sample for each task
- [Reproducible draws](https://github.com/NCodeGroup/Randomness/blob/main/docs/draws.md): the procedure and the exact algorithm
- [Changelog](https://github.com/NCodeGroup/Randomness/blob/main/CHANGELOG.md)
- [Report a vulnerability](https://github.com/NCodeGroup/Randomness/blob/main/SECURITY.md)
