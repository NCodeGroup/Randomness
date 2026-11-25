# NCode.Randomness

Cryptographically secure random numbers for .NET, strengthened by public randomness beacons such as
[CURBy](https://random.colorado.edu/), the University of Colorado Boulder's randomness beacon, and reproducible draws
that anyone can check against a beacon pulse.

[//]: # "Badges: to show the CI status, add this line below, with the repository's owner and name:"
[//]: # "[![CI](https://github.com/OWNER/REPO/actions/workflows/ci.yml/badge.svg)](https://github.com/OWNER/REPO/actions/workflows/ci.yml)"
[//]: # "Badges: to show the OpenSSF Scorecard, add this line below, with the repository's owner and name:"
[//]: # "[![OpenSSF Scorecard](https://api.scorecard.dev/projects/github.com/OWNER/REPO/badge)](https://scorecard.dev/viewer/?uri=github.com/OWNER/REPO)"
[//]: # "Badges: once the project is registered at bestpractices.dev, record its number as docs/best-practices-criteria.md says, then add the OpenSSF Best Practices badge that page shows."

## Why

CURBy publishes 512 bits of randomness every minute, certified by a loophole-free Bell test, and signs every pulse.
That is ideal for **public** randomness, a raffle or an audit that anyone can check, and dangerous for **secrets**:
everyone who reads the beacon sees the same bytes. This library gives each use what it needs:

| You need | Use | What you get |
|---|---|---|
| Keys, tokens, salts, nonces, passwords | `ISecureRandom` | An SP 800-90A HMAC_DRBG seeded by the operating system and reseeded with every beacon pulse: at least as strong as the OS generator, whatever a beacon does |
| A draw others can verify | `IVerifiableRandomFactory` | A deterministic stream from one verified pulse and a context label, reproducible by anyone, specified in [docs/draws.md](docs/draws.md) |
| No dependency injection, or no beacon | `SystemRandomSource.Instance` | The operating system's generator behind the same API |

Random values never wait on the network: beacon pulses are fetched and verified in the background, and an outage in
Boulder only stops the extra diversification.

## Packages

| Package | What it holds |
|---|---|
| `NCode.Randomness` | The secure generator, typed values and ranges, the beacon port, the background collector, draws, a health check and metrics |
| `NCode.Randomness.Curby` | The CURBy adapter: pulses of the CURBy-RNG chain and rounds of the CURBy-Q chain, each verified against a pinned chain |

## Quick Start

```csharp
var builder = Host.CreateApplicationBuilder(args);
builder.Services
    .AddRandomness(builder.Configuration.GetSection(RandomnessOptions.SectionName))
    .AddCurbyBeacon(builder.Configuration.GetSection(CurbyBeaconOptions.SectionName));
builder.Services.AddHealthChecks().AddRandomnessBeacons("ready");
```

Inject `ISecureRandom` wherever a secret is made:

```csharp
public sealed class TokenService(ISecureRandom random)
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public byte[] NewKey() => random.GetBytes(32);

    public string NewRecoveryCode() => random.GetString(Alphabet, 16);

    public int RollDie() => random.NextInteger(1, 7);
}
```

Every value is unbiased: integer ranges use rejection sampling, never a modulo. One generic method covers every
integer type, from `byte` to `UInt128`:

```csharp
var roll = random.NextInteger(1, 7);                        // int in [1, 7)
long offset = random.NextInteger(-1_000L, 1_000L);          // long in [-1000, 1000)
UInt128 id = random.NextInteger<UInt128>();                 // all 128 bits
double ratio = random.NextDouble();                         // [0, 1), 53 random bits
Guid token = random.NextGuid();                             // RFC 9562 version 4
random.Shuffle<int>(deck);                                  // Fisher–Yates
var color = random.GetItem<string>(["red", "green", "blue"]);
Random legacy = random.AsRandom();                          // for APIs that take a System.Random
```

## Modes

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

## Reproducible Draws

Commit to a future round and a context first, then draw:

```csharp
using var draw = await draws.CreateAsync("curby", round: 1620423, context: "example-raffle-2026-11-25", cancellationToken);
var winner = entrants[draw.NextInteger(0, entrants.Length)];
// Publish draw.Pulse.Id with the result, so anyone can recompute it.
```

[docs/draws.md](docs/draws.md) gives the procedure, the exact algorithm, and a short reference implementation in
Python that reproduces any draw without .NET.

## Security Model

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
- Documents must hold only the members Twine defines and signatures must be canonically encoded, so a server cannot
  grind a pulse's randomness without breaking its signature or hash.
- Pulses that roll back, switch chains, equivocate or are out of date are refused
  ([ADR-1002](docs/decisions/1002-beacons-are-adapters-collected-in-the-background-and-an-outage-degrades-never-fails.md)).
- [docs/assurance-case.md](docs/assurance-case.md) states the requirements and the threat model.

## Resilience and Operations

- Each beacon is fetched on its own loop, with a per-fetch timeout, HTTP retries and a circuit breaker, and back-off
  with jitter while it fails.
- An outage is logged once when it starts and once when it ends, and the health check reports `Degraded`, never
  `Unhealthy`: no instance should leave service because a university beacon is down.
- `IBeaconStatus` shows each beacon's health and last pulse; `docs/metrics.md` lists the metrics.
- More beacons mean no single point of trust: [docs/beacon-adapters.md](docs/beacon-adapters.md) is the contract for
  an adapter to another vendor.

## Development

Run from the repository root in PowerShell 7:

| Task | Command |
|---|---|
| Build | `dotnet build` |
| Test | `dotnet test` |

## AI-Assisted Workflow

This repository is set up for plan-then-execute coding: Claude Opus plans each change into `PLAN.md`, a cheaper model executes it one verified step at a time, and any step that needs a decision goes back to Opus. [AGENTS.md](AGENTS.md) is the index of the instructions every agent follows.

Work in Claude Code or GitHub Copilot, and switch whenever you like: both have the same commands, and `PLAN.md` is the only state they share, so one can plan and the other execute. Each bills its own subscription.

| Tool | Plan | Execute | Re-plan one step | Review | Land, then finish |
|---|---|---|---|---|---|
| Claude Code | `/plan-feature <what to build>` (or `/plan-bugfix`, `/plan-refactor`, `/plan-tests`, …), or `/plan-issue #42` for an issue, then review `PLAN.md` | `/orchestrate-plan` | `/replan-step N` | `/review-plan-diff` | `/ship-plan`, then `/finish-plan` |
| Copilot Chat in VS Code | The Copilot Planner agent on Opus, or `/plan-feature` or `/plan-issue #42` | Run all steps with subagents, or Execute next step | Re-plan with the planner | Review the work | `/ship-plan`, then `/finish-plan` |
| Copilot CLI | `copilot --model claude-opus-5.5`, then `/plan-feature` or `/plan-issue #42` | `/orchestrate-plan` | `/replan-step N` | `/review-plan-diff` | `/ship-plan`, then `/finish-plan` |

In Claude Code, plan in Accept edits mode and execute in Auto.

Each plan gets its own git worktree under `.claude/worktrees/`, in every tool, so several sessions can work on different plans at once.

The workflow files come from a Copier template, which owns them: never edit `.template/`, `.template-version`, `.githooks/pre-push`, `.github/instructions/project-template.instructions.md`, `.copier-answers.yml`, `CLAUDE.md` or the commands, agents and skills the template generates. To change how agents work here, edit `AGENTS.md` or a file it lists. To pull in template improvements, run `.\.template\Update-Project.ps1`: it updates itself, replaces the template's files, merges template changes into the shared settings files, and never touches `AGENTS.md` or this README. To plan the update first, with a trial run, the project files that need changing and the checks, run `/plan-template-update`. The weekly `template-update` workflow opens an issue, labeled `template-update`, when a newer template version is out, and closes it once the project has updated; `/plan-issue` plans it.

## Project

- [CONTRIBUTING.md](CONTRIBUTING.md): how to set up, build, test and propose a change.
- [SECURITY.md](SECURITY.md): how to report a vulnerability privately.
- [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md), [GOVERNANCE.md](GOVERNANCE.md) and [ROADMAP.md](ROADMAP.md): how we work together, who decides, and where the project is going.
- [docs/architecture.md](docs/architecture.md) and [docs/assurance-case.md](docs/assurance-case.md): how the project is built, and why it meets its security requirements.
- [docs/decisions/](docs/decisions/README.md): the decisions behind the design, the project's and the template's.
- [CHANGELOG.md](CHANGELOG.md): in the Keep a Changelog format, what the product does until its first stable release, and what changed in each release from then on.
- [LICENSE.txt](LICENSE.txt): the license.

## License

NCode.Randomness is licensed under the [Apache License 2.0](LICENSE.txt).
