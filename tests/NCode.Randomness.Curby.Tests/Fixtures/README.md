# CURBy Fixtures

Documents served by the Twine HTTP API that CURBy publishes through, fetched on 2026-11-25 and saved byte for byte.
They pin the parser, the DAG-CBOR encoder, the SHA3-512 hash and the RS256 check to data produced by CURBy's own
implementation. Never edit them; fetch them again instead:

```bash
C=bafyriqci6f3st2mg7gq733ho4zvvth32zpy2mtiylixwmhoz6d627eo3jfpmbxepe54u2zdvymonq5sp3armtm4rodxsynsirr5g3xsbd3q4s
curl -s https://api.entwine.me/chains/$C -o chain.json
curl -s https://api.entwine.me/chains/$C/pulses/1620421 -o pulse-1620421.json
curl -s https://api.entwine.me/chains/$C/pulses/1620422 -o pulse-1620422.json
curl -s https://api.entwine.me/chains/$C/pulses/1620423 -o pulse-1620423.json
```
