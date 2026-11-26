<p align="center">
  <img src="NCodeGroup.png" alt="NCode" width="96" height="96">
</p>

<h1 align="center">NCode.Randomness</h1>

<p align="center">
  Cryptographically secure random numbers for .NET, strengthened by public randomness beacons such as CURBy, the NIST Randomness Beacon and drand, and reproducible draws that anyone can check against a beacon pulse.
</p>

<p align="center">
  <a href="https://www.nuget.org/packages/NCode.Randomness/"><img alt="NuGet version" src="https://img.shields.io/nuget/v/NCode.Randomness?logo=nuget"></a>
  <a href="https://www.nuget.org/packages/NCode.Randomness/"><img alt="NuGet downloads" src="https://img.shields.io/nuget/dt/NCode.Randomness?logo=nuget"></a>
  <a href="https://github.com/NCodeGroup/Randomness/actions/workflows/ci.yml?query=branch%3Amain"><img alt="CI" src="https://github.com/NCodeGroup/Randomness/actions/workflows/ci.yml/badge.svg?branch=main"></a>
  <a href="https://www.nuget.org/packages/NCode.Randomness#supportedframeworks-body-tab"><img alt="Target framework" src="https://img.shields.io/badge/dynamic/xml?url=https%3A%2F%2Fraw.githubusercontent.com%2FNCodeGroup%2FRandomness%2Fmain%2Fsrc%2FNCode.Randomness%2FNCode.Randomness.csproj&query=%2F%2FTargetFramework%20%7C%20%2F%2FTargetFrameworks&label=target&logo=dotnet&color=512BD4"></a>
  <a href="https://randomness.ncode.io/"><img alt="Documentation" src="https://img.shields.io/badge/docs-randomness.ncode.io-512BD4"></a>
  <a href="LICENSE.txt"><img alt="License" src="https://img.shields.io/github/license/NCodeGroup/Randomness"></a>
  <a href="https://scorecard.dev/viewer/?uri=github.com/NCodeGroup/Randomness"><img alt="OpenSSF Scorecard" src="https://api.scorecard.dev/projects/github.com/NCodeGroup/Randomness/badge"></a>
  <a href="https://www.bestpractices.dev/projects/17129"><img alt="OpenSSF Best Practices" src="https://www.bestpractices.dev/projects/17129/badge"></a>
</p>

CURBy, the [University of Colorado Boulder's randomness beacon](https://random.colorado.edu/), publishes 512 bits of randomness every minute, certified by a loophole-free Bell test, and signs every pulse. That is ideal for **public** randomness, a raffle or an audit that anyone can check, and dangerous for **secrets**: everyone who reads the beacon sees the same bytes. NCode.Randomness gives each use what it needs, with CURBy, the [NIST Randomness Beacon](https://beacon.nist.gov/) and [drand](https://drand.love/) behind one API. Random values never wait on the network: beacon pulses are fetched and verified in the background, and an outage in Boulder only stops the extra diversification.

This page is the full guide; nuget.org shows the shorter [package readme](src/NCode.Randomness/README.md).

The [documentation site](https://randomness.ncode.io/) has a quick start, a guide for each task, the rules behind them, a reference of every beacon and an API map of every public member.

## Installation

```shell
dotnet add package NCode.Randomness
dotnet add package NCode.Randomness.Curby
dotnet add package NCode.Randomness.Nist
dotnet add package NCode.Randomness.Drand
```

Add only the beacon adapters you use; each adapter package depends on `NCode.Randomness`. Each package targets `net10.0`.

| Package | What it holds |
|---|---|
| `NCode.Randomness` | The secure generator, typed values and ranges, the beacon port, the background collector, draws, a health check and metrics |
| `NCode.Randomness.Curby` | The CURBy adapter: pulses of the CURBy-RNG chain and rounds of the CURBy-Q chain, each verified against a pinned chain |
| `NCode.Randomness.Nist` | The NIST Randomness Beacon 2.0 adapter: pulses verified against pinned certificates and their hash chain |
| `NCode.Randomness.Drand` | The drand adapter: quicknet rounds verified against the League of Entropy's pinned BLS key |

## Quick start

Register the generator, the CURBy, NIST and drand beacons, and a health check that reports them:

<!-- snippet: QuickStart -->
```cs
var builder = Host.CreateApplicationBuilder(args);
var configuration = builder.Configuration;
var randomness = configuration.GetSection(RandomnessOptions.SectionName);
var curby = configuration.GetSection(CurbyBeaconOptions.SectionName);
var nist = configuration.GetSection(NistBeaconOptions.SectionName);
var drand = configuration.GetSection(DrandBeaconOptions.SectionName);
builder
    .Services.AddRandomness(randomness)
    .AddCurbyBeacon(curby)
    .AddNistBeacon(nist)
    .AddDrandBeacon(drand);
builder.Services.AddHealthChecks().AddRandomnessBeacons("ready");
```
<!-- endSnippet -->

Register several beacons so no beacon is trusted alone; leave out the adapters you do not use.

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

## Usage

### Choosing an API

| You need | Use | What you get |
|---|---|---|
| Keys, tokens, salts, nonces, passwords | `ISecureRandom` | An SP 800-90A HMAC_DRBG seeded by the operating system and reseeded with every beacon pulse: at least as strong as the OS generator, whatever a beacon does |
| A draw others can verify | `IVerifiableRandomFactory` | A deterministic stream from one verified pulse and a context label, reproducible by anyone, specified in [docs/draws.md](docs/draws.md) |
| No dependency injection | `RandomnessHost` | The same generator and beacons, built and disposed by your code, for console tools and libraries |
| No beacon at all | `SystemRandomSource.Instance` | The operating system's generator behind the same API |

### Making a secret

Fill a key straight into memory that is cleared after use, such as a `stackalloc` span, rather than taking a `byte[]`
from `GetBytes`: the garbage collector can move an array and leave copies of the key behind.

<!-- snippet: FillKey -->
```cs
Span<byte> key = stackalloc byte[32];
try
{
    random.Fill(key);
    HMACSHA256.HashData(key, message, mac);
}
finally
{
    CryptographicOperations.ZeroMemory(key);
}
// The key is all zeros again, and mac holds the code.
```
<!-- endSnippet -->

A key that outlives one method belongs in a pinned buffer that is zeroed on dispose, such as one from
[NCode.Buffers](https://www.nuget.org/packages/NCode.Buffers/)'
`BufferFactory.Rent(32, isSensitive: true, out Span<byte> key)`. `Fill` takes any span, so the two packages work
together without either depending on the other.

### Drawing typed values

Every value is unbiased: integer ranges use rejection sampling, never a modulo. One generic method covers every
integer type, from `byte` to `UInt128`:

<!-- snippet: TypedValues -->
```cs
var roll = random.NextInteger(1, 7); // int in [1, 7)
long offset = random.NextInteger(-1_000L, 1_000L); // long in [-1000, 1000)
UInt128 id = random.NextInteger<UInt128>(); // all 128 bits
double ratio = random.NextDouble(); // [0, 1), 53 random bits
Guid token = random.NextGuid(); // RFC 9562 version 4
random.Shuffle<int>(deck); // Fisher–Yates
var color = random.GetItem<string>(["red", "green", "blue"]);
Random legacy = random.AsRandom(); // for APIs that take a System.Random
```
<!-- endSnippet -->

### Running without dependency injection

Console tools and libraries that run without a host build the same generator and beacons with `RandomnessHost`:

<!-- snippet: WithoutDependencyInjection -->
```cs
// The Randomness section of any IConfiguration.
var section = configuration.GetSection(RandomnessOptions.SectionName);
var builder = RandomnessHost.CreateBuilder(section);
builder.AddCurbyBeacon().AddNistBeacon().AddDrandBeacon();
await using var randomness = await builder.BuildAsync(cancellationToken);

Guid id = randomness.SecureRandom.NextGuid();
```
<!-- endSnippet -->

`CreateBuilder` takes nothing, an `Action<RandomnessOptions>` or a configuration section, as `AddRandomness` does.
`BuildAsync` checks every setting, as a host does at start, and starts collecting pulses in the background; disposing
the host stops the collection and zeroes the generator's state. Add a logging provider through `builder.Services` to
see the collector's logs.

### Choosing a mode

Set `Randomness:Mode` in configuration, or `options.Mode` in code:

| Mode | Behavior |
|---|---|
| `Continuous` (default) | Fetches a pulse from every beacon at start and every `RefreshInterval` (one minute) |
| `InitializeOnly` | Fetches until each beacon gave one pulse, then never contacts a beacon again: start online, then run offline |
| `Offline` | Contacts no beacon, even if some are registered, for air-gapped deployments |

```json
{
  "Randomness": { "Mode": "InitializeOnly", "RequireInitialPulse": true },
  "RandomnessBeacons": { "Curby": { "BaseAddress": "https://api.entwine.me/" } }
}
```

`RequireInitialPulse` makes the host refuse to start until a pulse is mixed in, for a policy that demands beacon input.
Without it, the default, the application starts and runs even when every beacon is down. Every setting is bound
strictly and validated when the host starts, so a misspelled key stops the host instead of being ignored.

### Running a reproducible draw

Commit to a future round and a context first, then draw:

<!-- snippet: PublicDraw -->
```cs
using var draw = await draws.CreateAsync(
    "curby",
    round: 1620423,
    context: "example-raffle-2026-11-25",
    cancellationToken
);
var winner = entrants[draw.NextInteger(0, entrants.Length)];
// Publish draw.Pulse.Id with the result, so anyone can recompute it.
```
<!-- endSnippet -->

[docs/draws.md](docs/draws.md) gives the procedure, the exact algorithm, and a short reference implementation in
Python that reproduces any draw without .NET.

### Checking a draw offline

To check a published draw without the network, the beacon adapter's verifier, `CurbyPulseVerifier`,
`CurbyQuantumRoundVerifier`, `NistPulseVerifier` or `DrandRoundVerifier`, verifies the documents the beacon served for
the round against its pinned anchor and returns the pulse to derive the draw from. This one checks a CURBy-RNG round:

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
// first is e43e360c9e2b9cd6a094e04a64e65922a3581ba6864da1cb1490244522f9986e,
// and rolls is 80, 38, 69, 95, 91, the values the reference
// implementation in docs/draws.md prints for this pulse and context.
```
<!-- endSnippet -->

## Security model

- Secret output rests on the operating system's CSPRNG; beacon pulses are only additional input, so a forged, replayed
  or missing pulse cannot weaken it
  ([ADR-1000](docs/decisions/1000-beacon-randomness-is-mixed-into-a-local-generator-and-never-used-alone-for-secrets.md)).
- Every CURBy pulse is verified before use: the chain must hash to a pinned content identifier, which fixes its key,
  each document must hash to its content identifier, each signature must be RS256 by the chain's key, and each salt
  must match the previous pulse's precommitment
  ([ADR-1003](docs/decisions/1003-curby-pulses-are-verified-against-a-pinned-chain-before-any-byte-is-used.md)).
- Every CURBy-Q round is verified as CURBy's client verifies it: each pulse is signed by the pinned chain, the Bell
  response and the seed pulse are in causal order with the round's pulses, and the seed expands to the right number of
  bits. The Bell-test certificate and the Trevisan extraction are not verified in process; CURBy's `curby_verify` tool
  audits them
  ([ADR-1007](docs/decisions/1007-curby-q-rounds-are-verified-as-curbys-client-verifies-them-and-their-extraction-is-left-to-curby-verify.md)).
- Every NIST pulse is verified before use: its certificate must hash to a pinned identifier and cover the pulse's
  timestamp, its RSA signature must verify over the signed fields, its output must be the hash of those fields and the
  signature, and it must chain to its predecessor's output and match the predecessor's precommitment
  ([ADR-1009](docs/decisions/1009-nist-pulses-are-verified-against-pinned-certificates-and-their-hash-chain-before-any-byte-is-used.md)).
- Every drand round is verified before use: the BLS signature of the round number, hashed to the curve as RFC 9380
  specifies, must verify against the pinned quicknet group key, and the randomness is the SHA-256 of that signature
  ([ADR-1010](docs/decisions/1010-drand-rounds-are-verified-against-the-pinned-quicknet-key-with-a-managed-bls12-381-library.md)).
- Documents must hold only the members Twine defines and signatures must be canonically encoded, so a server cannot
  grind a pulse's randomness without breaking its signature or hash. JSON whose text is not well-formed Unicode is
  refused before any member is read.
- Pulses that roll back, switch chains, equivocate or are out of date are refused
  ([ADR-1002](docs/decisions/1002-beacons-are-adapters-collected-in-the-background-and-an-outage-degrades-never-fails.md)).
- [docs/assurance-case.md](docs/assurance-case.md) states the requirements and the threat model.

## Resilience and operations

- Each beacon is fetched on its own loop, with a per-fetch timeout, HTTP retries and a circuit breaker, and back-off
  with jitter while it fails.
- An outage is logged once when it starts and once when it ends, and the health check reports `Degraded`, never
  `Unhealthy`: no instance should leave service because a university beacon is down.
- `IBeaconStatus` shows each beacon's health and last pulse; `docs/metrics.md` lists the metrics.
- More beacons mean no single point of trust: [docs/beacon-adapters.md](docs/beacon-adapters.md) is the contract for
  an adapter to another vendor.

## Project

- [Documentation site](https://randomness.ncode.io/): the quick start, the guides, the concepts, the beacon reference and the API map.
- [CONTRIBUTING.md](CONTRIBUTING.md): how to set up, build, test and propose a change, and the AI-assisted workflow.
- [Issues](https://github.com/NCodeGroup/Randomness/issues): report a bug or ask for a feature.
- [SECURITY.md](SECURITY.md): how to report a vulnerability privately.
- [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md), [GOVERNANCE.md](GOVERNANCE.md) and [ROADMAP.md](ROADMAP.md): how we work together, who decides, and where the project is going.
- [docs/architecture.md](docs/architecture.md) and [docs/assurance-case.md](docs/assurance-case.md): how the project is built, and why it meets its security requirements.
- [docs/decisions/](docs/decisions/README.md): the decisions behind the design, the project's and the template's.
- [CHANGELOG.md](CHANGELOG.md): in the Keep a Changelog format, what the product does until its first stable release, and what changed in each release from then on.
- [LICENSE.txt](LICENSE.txt): Apache License 2.0, copyright NCode Group.
