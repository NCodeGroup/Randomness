# 1012. Secrets live only in stack or pinned memory, and the packages take no buffer library dependency

- **Status:** Accepted
- **Date:** 2026-11-25
- **Deciders:** NCode Group maintainers
- **Refines:** project-template ADR-0112, whose rules 2 and 3 rent transient buffers from `ArrayPool<T>.Shared` and
  clear secret material on release

## Context

NCode.Randomness makes keys and holds the state of the generator that makes them
([ADR-1001](1001-the-secure-generator-is-a-sharded-buffered-hmac-drbg-reseeded-from-the-operating-system.md)).
Managed memory can leak a secret in two ways: the garbage collector's compacting phase moves an unpinned array and
leaves the old bytes in freed memory until an allocation reuses it, and an array returned to `ArrayPool<T>.Shared` goes
to the next renter anywhere in the process. [NCode.Buffers](https://www.nuget.org/packages/NCode.Buffers/) (5.5.0, read
2026-11-25) packages pinned, pooled buffers that zero themselves on dispose, and depends on Nerdbank.Streams. The
generator's key, value and output buffer are pinned arrays, and its seeds are `stackalloc` memory, each cleared with
`CryptographicOperations.ZeroMemory`. The adapters pool only public data: response bodies and the bytes a pulse's
signature covers.

## Decision

Secret material, meaning a key, a seed, the generator's state, or bytes from `ISecureRandom` kept for a secret, lives
only in a `stackalloc` span or a pinned array, and is cleared with `CryptographicOperations.ZeroMemory` on every path
out. It never goes in `ArrayPool<T>.Shared` or an unpinned array. Public data may still rent from
`ArrayPool<T>.Shared`.

The packages take no dependency on a buffer library, NCode.Buffers included. Their members take and fill
`Span<byte>`, so a caller keeps a secret in the memory it chooses, such as a buffer from NCode.Buffers'
`BufferFactory.Rent(32, isSensitive: true, out Span<byte> key)`. `GetBytes` returns an ordinary array for values that
aren't secret, and its documentation steers a key to `Fill`.

For secret material, this rule wins over project-template ADR-0112 and the project template's C# rule on binary data.

## Options considered

- **Depend on NCode.Buffers.** It would replace two internal pieces: `HmacDrbg`'s fallback for inputs over 512 bytes,
  which no caller reaches, and the NIST adapter's `PooledBufferWriter`, which holds only public bytes. In exchange,
  every consumer of a security library takes two more runtime packages, and each major version of NCode.Buffers can
  conflict with the one a consumer's other packages need.
  [ADR-1005](1005-twine-verification-is-implemented-in-the-adapter-without-an-ipld-dependency.md) and
  [ADR-1010](1010-drand-rounds-are-verified-against-the-pinned-quicknet-key-with-a-managed-bls12-381-library.md) keep
  dependencies off the trust path for the same reason.
- **Ban `byte[]`.** Too broad. An immutable value the packages store and expose as `ReadOnlySpan<byte>`, such as a
  pulse's randomness, is correctly an array, and a ban would push `IMemoryOwner<byte>` lifetimes into the public API.
  The risk is secret material in unpinned or pooled memory, which the rule names.
- **Rent transient secrets from `ArrayPool<T>.Shared` and clear them before return.** Clearing never reaches a copy
  the garbage collector left behind when it moved the array.

## Consequences

- A reviewer checks the rule mechanically: no buffer that holds secret material comes from `ArrayPool` or `new byte[]`.
- An input to `HmacDrbg` longer than its 512-byte stack threshold gets a pinned array of its own on each call.
- A caller who wants pooled, pinned, self-zeroing buffers adds NCode.Buffers itself; the READMEs show `Fill` with a
  span.
- `VerifiableRandom`'s block holds no secret
  ([ADR-1004](1004-reproducible-draws-are-a-versioned-specified-algorithm-and-never-a-secure-random.md)) and stays an
  ordinary array.

## References

- [project-template ADR-0112](project-template/0112-binary-data-uses-spans-pooled-buffers-and-cleared-secrets.md): the
  template's rule for binary data that this record refines for secrets.
- `src/NCode.Randomness/Generation/HmacDrbg.cs`, `src/NCode.Randomness/Generation/DrbgShard.cs`.
- [ADR-1000](1000-beacon-randomness-is-mixed-into-a-local-generator-and-never-used-alone-for-secrets.md): secrets come
  only from `ISecureRandom`.
- NCode.Buffers: https://github.com/NCodeGroup/Buffers.
