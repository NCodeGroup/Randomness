# drand Fixtures

Documents served by the drand API for the quicknet chain, fetched on 2026-11-25 and saved byte for byte. They pin the
round parser, the hash to G1 and the BLS12-381 signature check to data produced by drand's own implementation: the
chain's info, round 1000, and the newest round when they were fetched. Never edit them; fetch them again instead:

```bash
D=https://api.drand.sh/52db9ba70e0cc0f6eaf7803dd07447a1f5477735fd3f661792ba94600c84e971
curl -sf $D/info -o info.json
curl -sf $D/public/1000 -o public-1000.json
curl -sf $D/public/34271222 -o public-34271222.json
```

`info.json` holds the chain's group public key, period, genesis time and hash; the constants in `DrandDefaults` match
it. Round 34271222 was the newest round on 2026-11-25.
