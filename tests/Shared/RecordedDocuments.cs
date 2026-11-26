// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Randomness;
using NCode.Randomness.Curby;

namespace Recorded;

/// <summary>
/// Reads the documents CURBy, NIST and drand served, saved byte for byte, so a sample runs without the network.
/// </summary>
public static class RecordedDocuments
{
    /// <summary>
    /// Reads a CURBy-RNG document that CURBy served, such as <c>chain.json</c> or <c>pulse-1620423.json</c>.
    /// </summary>
    /// <param name="name">The file name of the document.</param>
    /// <returns>The document's bytes.</returns>
    public static byte[] Curby(string name) => Read("Curby", name);

    /// <summary>
    /// Reads a CURBy-Q document that CURBy served, such as <c>round-28297.json</c>.
    /// </summary>
    /// <param name="name">The file name of the document.</param>
    /// <returns>The document's bytes.</returns>
    public static byte[] CurbyQ(string name) => Read("CurbyQ", name);

    /// <summary>
    /// Reads a document that NIST served, such as <c>pulse-2043330.json</c> or <c>certificate-5b6386e6.pem</c>.
    /// </summary>
    /// <param name="name">The file name of the document.</param>
    /// <returns>The document's bytes.</returns>
    public static byte[] Nist(string name) => Read("Nist", name);

    /// <summary>
    /// Reads a round document that drand served, such as <c>public-34271222.json</c>.
    /// </summary>
    /// <param name="name">The file name of the document.</param>
    /// <returns>The document's bytes.</returns>
    public static byte[] Drand(string name) => Read("Drand", name);

    /// <summary>
    /// Verifies a recorded CURBy-RNG pulse against the CURBy-RNG chain and its predecessor.
    /// </summary>
    /// <param name="round">The round of the pulse, which is one of the recorded rounds after the first.</param>
    /// <returns>The verified pulse.</returns>
    public static BeaconPulse CurbyPulse(long round) =>
        new CurbyPulseVerifier().Verify(
            Curby("chain.json"),
            Curby($"pulse-{round}.json"),
            Curby($"pulse-{round - 1}.json")
        );

    private static byte[] Read(string folder, string name) =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", folder, name));
}
