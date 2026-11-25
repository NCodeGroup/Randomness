// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Randomness.Curby;

/// <summary>
/// The CURBy beacon's published identifiers.
/// </summary>
public static class CurbyDefaults
{
    /// <summary>
    /// The name the CURBy beacon is registered under: <c>curby</c>.
    /// </summary>
    public const string BeaconName = "curby";

    /// <summary>
    /// The content identifier of the CURBy-RNG chain, the beacon's randomness chain, which publishes a pulse every
    /// minute. Because a content identifier is the hash of the chain's document, which holds the chain's signing key,
    /// pinning it pins the key: no server, mirror or TLS certificate can substitute another.
    /// </summary>
    public const string RngChainId =
        "bafyriqci6f3st2mg7gq733ho4zvvth32zpy2mtiylixwmhoz6d627eo3jfpmbxepe54u2zdvymonq5sp3armtm4rodxsynsirr5g3xsbd3q4s";

    /// <summary>
    /// The name the CURBy-Q beacon is registered under: <c>curby-q</c>.
    /// </summary>
    public const string QuantumBeaconName = "curby-q";

    /// <summary>
    /// The content identifier of the CURBy-Q (DIRNG) chain, whose rounds publish randomness extracted from a
    /// loophole-free Bell test. Because a content identifier is the hash of the chain's document, which holds the
    /// chain's signing key, pinning it pins the key.
    /// </summary>
    public const string QuantumChainId =
        "bafyriqhijw5soalbtppuwwjbskfiriqy6swa5qd5trgbzx4nypnu7mp7iyhx7m2iin2nxnglmngvjgirzb7bvlisxq3uygoik7ozbybxnftiw";

    /// <summary>
    /// The content identifier of the Bell chain, on which the Bell experiment answers each CURBy-Q request with the
    /// hash of its data. Pinning it pins its signing key.
    /// </summary>
    public const string BellChainId =
        "bafyriqe3zxf5g4ifgqhea5zozxpdcfi5qcpkfpogtxzbizmmuxdjuzuq44a2cifbr7xplo4kcfsdz2c5pxfxektavrqxxb3nvbmclxz7qiz6e";

    /// <summary>
    /// The base address of the HTTP API that serves CURBy's Twine chains.
    /// </summary>
    public const string BaseAddress = "https://api.entwine.me/";

    /// <summary>
    /// The name of the <see cref="System.Net.Http.HttpClient"/> the beacon uses, for a host that adds its own handlers,
    /// such as a proxy, with <c>services.AddHttpClient(CurbyDefaults.HttpClientName)</c>.
    /// </summary>
    public const string HttpClientName = "NCode.Randomness.Curby";
}
