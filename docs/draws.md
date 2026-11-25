# Reproducible Draws

How to run a draw that anyone can check, such as a raffle, an audit sample or a jury selection, and the exact
algorithm, `ncode-randomness-draw/1`, that turns a beacon pulse into its values. The reasons are in
[ADR-1004](decisions/1004-reproducible-draws-are-a-versioned-specified-algorithm-and-never-a-secure-random.md).

A draw's values are public to everyone who reads the pulse. Never derive a key, token or other secret from one; use
`ISecureRandom` for those.

## Running a Fair Draw

1. **Commit first.** Before the round is published, publish the beacon (`curby`, `nist` or `drand`), a future round
   number, the context label, the list of entrants in its final order, and the code or this algorithm's name. CURBy
   publishes a round every minute, so the latest round plus 60 is about an hour ahead. Committing first is what stops
   anyone from choosing a pulse that favors them. CURBy-Q rounds (beacon `curby-q`) are numbered by round, and the ID is
   the randomness pulse's CID. NIST (beacon `nist`) publishes a pulse a minute: the round is the pulse index, and the ID
   is the pulse's output in uppercase hex. drand (beacon `drand`) publishes a round every 3 seconds, and the ID is the
   round's signature in lowercase hex.
2. **Wait for the round, then draw.**

   ```csharp
   using var draw = await draws.CreateAsync("curby", round: 1620423, context: "example-raffle-2026-11-25", cancellationToken);
   var winner = entrants[draw.NextInteger(0, entrants.Count)];
   ```

   `IVerifiableRandomFactory` fetches the round from the beacon, which verifies it
   ([ADR-1003](decisions/1003-curby-pulses-are-verified-against-a-pinned-chain-before-any-byte-is-used.md) for CURBy),
   and derives the stream. `VerifiableRandom.Create(pulse, context)` derives it from a pulse already in hand.
3. **Publish the result** with the pulse's ID (`draw.Pulse.Id`), so anyone can fetch the same pulse and recompute it,
   with this library or with the reference implementation below.

If the beacon never publishes the committed round, the procedure you committed to in step 1 decides what happens, such
as taking the first published round after it.

## The Algorithm

All lengths are counts of bytes. `LP(x)` is the 4-byte big-endian length of `x` followed by `x`; text is UTF-8.

1. **Instantiate** HMAC_DRBG with HMAC-SHA-512 (NIST SP 800-90A Rev. 1, section 10.1.2) with:
   - entropy input: the pulse's randomness (for CURBy, the 64-byte SHA3-512 digest in the pulse's CID; for CURBy-Q,
     beacon `curby-q`, the round's 64-byte `randomness`; for NIST, beacon `nist`, the pulse's 64-byte `outputValue`;
     for drand, beacon `drand`, the 32-byte SHA-256 of the round's signature);
   - nonce: `LP(beacon) || LP(chain) || round as an 8-byte big-endian integer || LP(pulse ID)`;
   - personalization string: `LP("ncode-randomness-draw/1") || LP(context)`.
2. **Stream** the outputs of consecutive generate requests of exactly 64 bytes each, with no additional input, and
   read them in order. How a caller splits its reads does not change the bytes.
3. **Sample** each value from the next bytes of the stream:
   - An integer in `[min, max)`: let `r = max - min`. If `r = 1`, the value is `min` and no byte is read. Otherwise let
     `b` be the bit length of `r - 1`; read `ceil(b / 8)` bytes as a little-endian unsigned integer, keep its low `b`
     bits, and repeat until the result is less than `r`. The value is `min` plus the result.
   - An integer over every value of an `n`-byte type: read `n` bytes as little-endian two's complement.
   - A double in `[0, 1)`: read 8 bytes as a little-endian unsigned integer, shift it right by 11, and multiply by
     2<sup>-53</sup>. A single: 4 bytes, shifted right by 8, times 2<sup>-24</sup>. A boolean: the low bit of 1 byte.
   - A shuffle of `n` items: for `i` from `n - 1` down to 1, draw `j` in `[0, i + 1)` and swap items `i` and `j`.
   - A choice from `n` items: draw an index in `[0, n)`. A string from an alphabet: one choice per character.
   - A GUID: read 16 bytes, set the version nibble to 4 and the RFC 9562 variant, as `NextGuid` documents.

## Reference Implementation

This Python program, which uses only the standard library and shares no code with the .NET library, reproduces a draw.
The library's tests pin their output to what it prints for the example, CURBy-RNG round 1620423 with the context
`example-raffle-2026-11-25`:

```text
first 32 bytes: e43e360c9e2b9cd6a094e04a64e65922a3581ba6864da1cb1490244522f9986e
five rolls of 1..100: [80, 38, 69, 95, 91]
shuffle of 1..10: [10, 2, 8, 1, 4, 9, 5, 3, 6, 7]
```

```python
import hashlib
import hmac
import struct

ALGORITHM = "ncode-randomness-draw/1"


class HmacDrbg:
    """HMAC_DRBG with HMAC-SHA-512, NIST SP 800-90A Rev. 1 section 10.1.2, without prediction resistance."""

    def __init__(self, entropy: bytes, nonce: bytes, personalization: bytes):
        self.key = b"\x00" * 64
        self.value = b"\x01" * 64
        self._update(entropy + nonce + personalization)

    def _mac(self, data: bytes) -> bytes:
        return hmac.new(self.key, data, hashlib.sha512).digest()

    def _update(self, data: bytes) -> None:
        self.key = self._mac(self.value + b"\x00" + data)
        self.value = self._mac(self.value)
        if data:
            self.key = self._mac(self.value + b"\x01" + data)
            self.value = self._mac(self.value)

    def generate_block(self) -> bytes:
        """One generate request of exactly 64 bytes with no additional input."""
        self.value = self._mac(self.value)
        block = self.value
        self._update(b"")
        return block


def field(data: bytes) -> bytes:
    return struct.pack(">I", len(data)) + data


class Draw:
    def __init__(self, beacon: str, chain: str, round_: int, pulse_id: str, randomness: bytes, context: str):
        nonce = field(beacon.encode()) + field(chain.encode()) + struct.pack(">q", round_) + field(pulse_id.encode())
        personalization = field(ALGORITHM.encode()) + field(context.encode())
        self.drbg = HmacDrbg(randomness, nonce, personalization)
        self.buffer = b""

    def read(self, count: int) -> bytes:
        while len(self.buffer) < count:
            self.buffer += self.drbg.generate_block()
        out, self.buffer = self.buffer[:count], self.buffer[count:]
        return out

    def below(self, range_: int) -> int:
        """A uniform integer in [0, range_) by bitmask rejection over whole little-endian bytes."""
        largest = range_ - 1
        if largest == 0:
            return 0
        bits = largest.bit_length()
        mask = (1 << bits) - 1
        while True:
            candidate = int.from_bytes(self.read((bits + 7) // 8), "little") & mask
            if candidate < range_:
                return candidate

    def integer(self, minimum: int, maximum_exclusive: int) -> int:
        return minimum + self.below(maximum_exclusive - minimum)

    def shuffle(self, values: list) -> list:
        values = list(values)
        for i in range(len(values) - 1, 0, -1):
            j = self.below(i + 1)
            values[i], values[j] = values[j], values[i]
        return values


EXAMPLE = dict(
    beacon="curby",
    chain="bafyriqci6f3st2mg7gq733ho4zvvth32zpy2mtiylixwmhoz6d627eo3jfpmbxepe54u2zdvymonq5sp3armtm4rodxsynsirr5g3xsbd3q4s",
    round_=1620423,
    pulse_id="bafyriqbtxzd22eybdovubearwfjyzlyowyhxqdkbzyboftamh6rhob2jafopfz4h2nf3eec4nawu2gil2vpgn57qilrlh23piwyozlugpmcvc",
    randomness=bytes.fromhex("33be47ad13011bab409011b1538caf0eb60f780d41ce02e2cc0c3fa2770749015cf2e787d34bb2105c682d4d190bd55e66f7f042e2b3eb6f45b0ecae867b0551"),
    context="example-raffle-2026-11-25",
)

if __name__ == "__main__":
    draw = Draw(**EXAMPLE)
    print("first 32 bytes:", draw.read(32).hex())
    print("five rolls of 1..100:", [draw.integer(1, 101) for _ in range(5)])
    print("shuffle of 1..10:", draw.shuffle(range(1, 11)))
```
