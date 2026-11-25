# Writing a Beacon Adapter

How to add a randomness beacon other than CURBy, such as the NIST Randomness Beacon or drand. An adapter is a package
of its own that implements one port, `IRandomnessBeacon`; the core never changes for a vendor
([ADR-1002](decisions/1002-beacons-are-adapters-collected-in-the-background-and-an-outage-degrades-never-fails.md)).
The CURBy adapter in `src/NCode.Randomness.Curby` is the worked example.

## The Contract

```csharp
public interface IRandomnessBeacon
{
    string Name { get; }
    ValueTask<BeaconPulse> GetLatestPulseAsync(CancellationToken cancellationToken);
    ValueTask<BeaconPulse> GetPulseAsync(long round, CancellationToken cancellationToken);
}
```

An implementation:

1. **Verifies before it returns.** Every check the vendor's protocol defines (signatures against a pinned key or
   certificate, hash chaining, precommitments) passes before a `BeaconPulse` is built. A pulse is authentic by
   construction; the core never verifies one again.
2. **Fails with `BeaconException`.** `Unavailable` for a network fault, a timeout or an error status;
   `PulseNotFound` when the requested round is not published; `InvalidPulse` for malformed data or any failed check.
   Let an `OperationCanceledException` for the caller's own token propagate. Fail asynchronously, with a faulted
   `ValueTask`, never by throwing before the first `await`.
3. **Pins its trust anchor.** The key, certificate or chain identifier the signatures chain to is a constant or a
   validated option, never something the server supplies.
4. **Bounds every call.** A named `HttpClient` with `AddStandardResilienceHandler`, no redirects, a JSON or binary
   content-type check, and a response body read under a byte limit while it streams.
5. **Fills the pulse with stable identifiers.** `Beacon` is the adapter's registered name; `Chain` and `Id` are the
   vendor's own identifiers, so a draw can be recomputed from them ([draws.md](draws.md)); `Round` increases within a
   chain; `Randomness` is at least 32 bytes.
6. **Is a thread-safe singleton.** Cache what never changes, such as a verified chain or key, and nothing per caller.

## Registration

Expose an extension member on `IRandomnessBuilder` that registers the adapter with `AddBeacon<TBeacon>()`, or with
`TryAddEnumerable`, together with its options, their `IValidateOptions<T>` and its named `HttpClient`:

```csharp
services.AddRandomness().AddCurbyBeacon().AddNistBeacon();
```

The collector fetches from every registered beacon on its own loop and mixes every accepted pulse into the secure
generator, so each additional beacon removes a single point of trust.

## Tests

Pin the parser and the verifier to documents the vendor served, saved byte for byte with the command that fetched
them, and break each check with documents the test builds and signs with a key of its own, as
`tests/NCode.Randomness.Curby.Tests` does. Put calls to the real service behind a fact that runs only when an
environment variable asks for it.
