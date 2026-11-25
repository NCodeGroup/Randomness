// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Randomness.Nist.Tests.Fixtures;

/// <summary>
/// The saved NIST documents described in Fixtures/README.md.
/// </summary>
internal static class NistFixtures
{
    public const string CurrentCertificateId =
        "5b6386e6ac8a993bac8d99d6549deda44af82f2fa4a0e9c0025b6474ab0cbfcab67bfff264fda41aac003b0c9ee7fb77d2938dc0f286fb73cc8fa79864ec0c18";
    public const string RotatedCertificateId =
        "528943a555f5f8ca54423be6dfb95925a35c7b552046420e7d7cd072058a14d6536ad3a8e9754b6582f164a90b0cd86a65d659f5426a2659a947595d1c816c8c";
    public const string PreviousCertificateId =
        "e692b0f6b208322ac84c963e167245f6356c21f31fa95274b25e92329510b5a210d5179afb6ee25e12dd9fc2691f49ed778d9a709aa3c2ea0f3ff2eaf8c47a62";
    public const long LatestIndex = 2043331;
    public const string LatestOutput =
        "3929B29994FCE9EFC13639519EF32EB32EBA7840F734D6AF9610A135ED5ECDFA080925B6AAE3DE8D5A20C8463874D84B228CB51E76747C68178C6C97A003A88E";
    public const string PreviousOutput =
        "7700A4038B374C698A7024A3BC792EC81816166B8EF36B0DC974BAB5F72FA8F8D8A1F531CCD0F6DD71BD75EAF7386A0D8AB24160198DE49EE5315B252A4BF373";

    public static DateTimeOffset LatestTimestamp { get; } =
        new(2026, 11, 25, 14, 26, 0, TimeSpan.Zero);

    public static string Pulse(long index) => File.ReadAllText(Path($"pulse-{index}.json"));

    public static byte[] Certificate(string prefix) =>
        File.ReadAllBytes(Path($"certificate-{prefix}.pem"));

    private static string Path(string name) =>
        System.IO.Path.Combine(AppContext.BaseDirectory, "Fixtures", name);
}
