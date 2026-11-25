// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;

namespace NCode.Randomness.Nist;

/// <summary>
/// The NIST Randomness Beacon's published identifiers.
/// </summary>
public static class NistDefaults
{
    /// <summary>
    /// The name the NIST beacon is registered under: <c>nist</c>.
    /// </summary>
    public const string BeaconName = "nist";

    /// <summary>
    /// The base address of the NIST Randomness Beacon 2.0 HTTP API.
    /// </summary>
    public const string BaseAddress = "https://beacon.nist.gov/beacon/2.0/";

    /// <summary>
    /// The index of the chain the beacon trusts: chain 2, which publishes a pulse every minute.
    /// </summary>
    public const long ChainIndex = 2;

    /// <summary>
    /// The name of the <see cref="System.Net.Http.HttpClient"/> the beacon uses, for a host that adds its own handlers,
    /// such as a proxy, with <c>services.AddHttpClient(NistDefaults.HttpClientName)</c>.
    /// </summary>
    public const string HttpClientName = "NCode.Randomness.Nist";

    /// <summary>
    /// Gets the identifiers, oldest first, of the certificates NIST signs chain 2 with. An identifier is the SHA-512
    /// hash of a certificate's DER encoding in lowercase hex, so pinning it pins the certificate and its key: no server,
    /// mirror or TLS certificate can substitute another.
    /// </summary>
    public static ImmutableArray<string> CertificateIds { get; } =
    [
        "ed8e1b9745e337e38745a54579d97b21562cb703e44926666a1d6499a9e3399448cdf6b1403ec056314c129910e82b0e8af48c1e062a88828e178dfc82983c2b",
        "aa1e1ed43ed72a4a8e8535687074690c593adc683c122dbed16842acbfd697020ddb1c382bf06893b11836da1fc9df982ec34e4d9dc3c105a816950948a25005",
        "e692b0f6b208322ac84c963e167245f6356c21f31fa95274b25e92329510b5a210d5179afb6ee25e12dd9fc2691f49ed778d9a709aa3c2ea0f3ff2eaf8c47a62",
        "528943a555f5f8ca54423be6dfb95925a35c7b552046420e7d7cd072058a14d6536ad3a8e9754b6582f164a90b0cd86a65d659f5426a2659a947595d1c816c8c",
        "5b6386e6ac8a993bac8d99d6549deda44af82f2fa4a0e9c0025b6474ab0cbfcab67bfff264fda41aac003b0c9ee7fb77d2938dc0f286fb73cc8fa79864ec0c18",
    ];
}
