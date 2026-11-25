# NIST Fixtures

Documents served by the NIST Randomness Beacon 2.0, fetched on 2026-11-25 and saved byte for byte with their CRLF line
endings (`.gitattributes` marks them `-text`). They pin the pulse parser, the signed-bytes encoding, the output hash,
the RSA check and the certificate pinning to data produced by NIST's own implementation. Never edit them; fetch them
again instead:

```bash
N=https://beacon.nist.gov/beacon/2.0
curl -sf $N/chain/2/pulse/2043330 -o pulse-2043330.json
curl -sf $N/chain/2/pulse/2043331 -o pulse-2043331.json
curl -sf $N/chain/2/pulse/1484768 -o pulse-1484768.json
curl -sf $N/chain/2/pulse/1484769 -o pulse-1484769.json
curl -sf $N/certificate/5b6386e6ac8a993bac8d99d6549deda44af82f2fa4a0e9c0025b6474ab0cbfcab67bfff264fda41aac003b0c9ee7fb77d2938dc0f286fb73cc8fa79864ec0c18 -o certificate-5b6386e6.pem
curl -sf $N/certificate/528943a555f5f8ca54423be6dfb95925a35c7b552046420e7d7cd072058a14d6536ad3a8e9754b6582f164a90b0cd86a65d659f5426a2659a947595d1c816c8c -o certificate-528943a5.pem
curl -sf $N/certificate/e692b0f6b208322ac84c963e167245f6356c21f31fa95274b25e92329510b5a210d5179afb6ee25e12dd9fc2691f49ed778d9a709aa3c2ea0f3ff2eaf8c47a62 -o certificate-e692b0f6.pem
```

Pulses 2043330 and 2043331 are consecutive and signed under certificate `5b6386e6…`. Pulses 1484768 and 1484769
straddle the rotation from certificate `e692b0f6…` to certificate `528943a5…`.
