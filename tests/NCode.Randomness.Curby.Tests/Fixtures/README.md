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

## CURBy-Q

Documents of the CURBy-Q (DIRNG) chain, its Bell chain and its seed chain, for round 28297, the newest complete round
when they were fetched on 2026-11-25. They live in the `CurbyQ` folder, are saved byte for byte, and are never edited;
fetch them again instead:

```bash
A=https://api.entwine.me
Q=bafyriqhijw5soalbtppuwwjbskfiriqy6swa5qd5trgbzx4nypnu7mp7iyhx7m2iin2nxnglmngvjgirzb7bvlisxq3uygoik7ozbybxnftiw
B=bafyriqe3zxf5g4ifgqhea5zozxpdcfi5qcpkfpogtxzbizmmuxdjuzuq44a2cifbr7xplo4kcfsdz2c5pxfxektavrqxxb3nvbmclxz7qiz6e
S=bafyriqa5k2d3t3r774geicueaed2wc2fosjwqeexfhwbptfgq7rcn5mwucnhfeuxu2nxbrch3rl6yqjlozhuswo5ln3xwjm35iftt3tpqlcgs
cd CurbyQ
curl -sf $A/chains/$Q -o chain.json
curl -sf $A/curbyq/round/28297 -o round-28297.json
curl -sf $A/curbyq/round/28297/params -o round-28297-params.json
curl -sf $A/chains/$B -o bell-chain.json
curl -sf $A/chains/$B/pulses/59313 -o bell-pulse-59313.json
curl -sf $A/chains/$S -o seed-chain.json
curl -sf $A/chains/$S/pulses/1914066 -o seed-pulse-1914066.json
curl -sf $A/chains/$S/pulses/1914067 -o seed-pulse-1914067.json
curl -sf $A/chains/$S/pulses/1914068 -o seed-pulse-1914068.json
```
