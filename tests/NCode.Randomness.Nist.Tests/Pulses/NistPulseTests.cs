// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using NCode.Randomness.Nist.Pulses;
using NCode.Randomness.Nist.Tests.Fixtures;
using NCode.Randomness.Nist.Tests.TestNist;

namespace NCode.Randomness.Nist.Tests.Pulses;

public class NistPulseTests
{
    private const int LengthPrefixBytes = 4;
    private const int StatusCodeBytes = 4;
    private const int HashBytes = 64;
    private const int MaxSignatureBytes = 1024;
    private const int ExternalLength =
        LengthPrefixBytes + HashBytes + StatusCodeBytes + LengthPrefixBytes + HashBytes;

    private static string Hex(int bytes) => new('A', bytes * 2);

    private static JsonElement Document(long index, string? path = null, string? json = null)
    {
        var fixture = NistFixtures.Pulse(index);
        var node = JsonNode.Parse(fixture);
        if (path is not null)
        {
            node = Mutate(node, path, json);
        }

        var text = node?.ToJsonString() ?? "null";
        return JsonElement.Parse(
            text,
            new JsonDocumentOptions { MaxDepth = 16, AllowDuplicateProperties = false }
        );
    }

    private static JsonNode? Mutate(JsonNode? root, string path, string? json)
    {
        if (path.Length == 0)
        {
            return JsonNode.Parse(json ?? "null");
        }

        var segments = path.Split('.');
        var parent = root;
        foreach (var segment in segments[..^1])
        {
            parent = Child(parent, segment);
        }

        var last = segments[^1];
        var value = json is null ? null : JsonNode.Parse(json);
        switch (parent)
        {
            case JsonArray array:
                array[int.Parse(last, CultureInfo.InvariantCulture)] = value;
                break;
            case JsonObject obj when json is null:
                obj.Remove(last);
                break;
            case JsonObject obj:
                obj[last] = value;
                break;
        }

        return root;
    }

    private static JsonNode? Child(JsonNode? parent, string segment) =>
        parent is JsonArray array
            ? array[int.Parse(segment, CultureInfo.InvariantCulture)]
            : parent?[segment];

    private static NistPulse ParseBuilt(string json)
    {
        var document = JsonElement.Parse(
            json,
            new JsonDocumentOptions { MaxDepth = 16, AllowDuplicateProperties = false }
        );
        return NistPulse.Parse(document);
    }

    private static (NistPulse Predecessor, NistPulse Pulse) BuiltPair(
        NistTestSigner signer,
        Func<NistPulseBuilder, NistPulseBuilder>? alter
    )
    {
        var predecessorJson = NistPulseBuilder.For(10).Build(signer);
        var previous = NistPulseBuilder.OutputOf(predecessorJson);
        var builder = NistPulseBuilder.For(11) with { Previous = previous };
        if (alter is not null)
        {
            builder = alter(builder);
        }

        var predecessor = ParseBuilt(predecessorJson);
        var pulseJson = builder.Build(signer);
        var pulse = ParseBuilt(pulseJson);
        return (predecessor, pulse);
    }

    public static TheoryData<string, string?, string> BrokenDocuments =>
        new()
        {
            { "", "[]", "not a JSON object" },
            { "pulse", null, "'pulse'" },
            { "pulse.pulseIndex", null, "'pulseIndex'" },
            { "pulse.pulseIndex", "0", "'pulseIndex'" },
            { "pulse.chainIndex", "0", "'chainIndex'" },
            { "pulse.statusCode", "-1", "'statusCode'" },
            {
                "pulse.localRandomValue",
                $"\"{Hex(HashBytes - 1)}\"",
                "'localRandomValue' is not 64 bytes"
            },
            { "pulse.outputValue", "\"not hex\"", "'outputValue' is not hexadecimal" },
            { "pulse.signatureValue", "\"\"", "'signatureValue' has 0 bytes" },
            {
                "pulse.signatureValue",
                $"\"{Hex(MaxSignatureBytes + 1)}\"",
                "'signatureValue' has 1025 bytes"
            },
            { "pulse.precommitmentValue", "\"ABC\"", "'precommitmentValue' is not hexadecimal" },
            { "pulse.external.sourceId", "\"not hex\"", "'external.sourceId' is not hexadecimal" },
            {
                "pulse.external.value",
                $"\"{Hex(HashBytes + 1)}\"",
                "'external.value' is not 64 bytes"
            },
            {
                "pulse.listValues.1.value",
                $"\"{Hex(HashBytes - 1)}\"",
                "'listValues[].value' is not 64 bytes"
            },
            { "pulse.listValues", "[]", "'listValues'" },
            { "pulse.listValues.0.type", "\"hour\"", "'previous'" },
            { "pulse.external", "\"none\"", "'external'" },
            { "pulse.timeStamp", "\"2026-11-25T14:26:00Z\"", "'timeStamp'" },
        };

    #region Parse Tests

    [Fact]
    public void Parse_WhenPulseIsNists_ReadsItsMembers()
    {
        var document = Document(NistFixtures.LatestIndex);
        var pulse = NistPulse.Parse(document);
        var certificateId = Convert.FromHexString(NistFixtures.CurrentCertificateId);
        var output = Convert.FromHexString(NistFixtures.LatestOutput);
        var previous = Convert.FromHexString(NistFixtures.PreviousOutput);

        Assert.Equal("2.0", pulse.Version);
        Assert.Equal(0u, pulse.CipherSuite);
        Assert.Equal<byte>(certificateId, pulse.CertificateId);
        Assert.Equal(2, pulse.ChainIndex);
        Assert.Equal(NistFixtures.LatestIndex, pulse.PulseIndex);
        Assert.Equal(NistFixtures.LatestTimestamp, pulse.Timestamp);
        Assert.Equal<byte>(output, pulse.Output);
        Assert.Equal<byte>(previous, pulse.Previous);
        Assert.Equal(HashBytes, pulse.LocalRandomValue.Length);
        Assert.Equal(HashBytes, pulse.Precommitment.Length);
        Assert.Equal(512, pulse.Signature.Length);
    }

    [Theory]
    [InlineData(2043331)]
    [InlineData(1484769)]
    public void Parse_WhenPulseIsNists_RebuildsBytesThatHashToItsOutput(long index)
    {
        var json = Document(index);

        var pulse = NistPulse.Parse(json);

        var hashed = new byte[pulse.SignedBytes.Length + pulse.Signature.Length];
        pulse.SignedBytes.CopyTo(hashed, 0);
        pulse.Signature.CopyTo(hashed, pulse.SignedBytes.Length);
        var expected = SHA512.HashData(hashed);
        Assert.Equal<byte>(expected, pulse.Output);
    }

    [Fact]
    public void Parse_WhenExternalIsAbsent_SignsExternalOut()
    {
        var withExternalDocument = Document(NistFixtures.LatestIndex);
        var withExternal = NistPulse.Parse(withExternalDocument);
        var withoutDocument = Document(NistFixtures.LatestIndex, "pulse.external");

        var without = NistPulse.Parse(withoutDocument);

        Assert.Equal(withExternal.SignedBytes.Length - ExternalLength, without.SignedBytes.Length);
    }

    [Fact]
    public void Parse_WhenExternalIsNull_SignsExternalOut()
    {
        var withExternalDocument = Document(NistFixtures.LatestIndex);
        var withExternal = NistPulse.Parse(withExternalDocument);
        var withoutDocument = Document(NistFixtures.LatestIndex, "pulse.external", "null");

        var without = NistPulse.Parse(withoutDocument);

        Assert.Equal(withExternal.SignedBytes.Length - ExternalLength, without.SignedBytes.Length);
    }

    [Theory]
    [MemberData(nameof(BrokenDocuments))]
    public void Parse_WhenMemberIsBroken_ThrowsNamingTheMember(
        string path,
        string? json,
        string fragment
    )
    {
        var document = Document(NistFixtures.LatestIndex, path, json);

        var exception = Assert.Throws<NistFormatException>(() => NistPulse.Parse(document));

        Assert.Contains(fragment, exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(MaxSignatureBytes)]
    public void Parse_WhenSignatureHasOneToTheMostBytes_ReadsIt(int bytes)
    {
        var signature = $"\"{Hex(bytes)}\"";
        var document = Document(NistFixtures.LatestIndex, "pulse.signatureValue", signature);

        var pulse = NistPulse.Parse(document);

        Assert.Equal(bytes, pulse.Signature.Length);
    }

    [Fact]
    public void Parse_WithNistPulseBytes_ReadsItsIndex()
    {
        var json = NistFixtures.Pulse(NistFixtures.LatestIndex);
        var bytes = Encoding.UTF8.GetBytes(json);
        var output = Convert.FromHexString(NistFixtures.LatestOutput);

        var pulse = NistPulse.Parse(bytes);

        Assert.Equal(NistFixtures.LatestIndex, pulse.PulseIndex);
        Assert.Equal<byte>(output, pulse.Output);
    }

    [Fact]
    public void Parse_WithMalformedJsonBytes_Throws()
    {
        var exception = Assert.Throws<NistFormatException>(() => NistPulse.Parse("{"u8));

        Assert.Contains("not valid JSON", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_WithDuplicateMemberBytes_Throws()
    {
        // The API path refuses repeated members too.
        var exception = Assert.Throws<NistFormatException>(() =>
            NistPulse.Parse("""{"pulse":{},"pulse":{}}"""u8)
        );

        Assert.Contains("not valid JSON", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_WithInvalidUtf8InAString_Throws()
    {
        byte[] json = [.. "{\"pulse\":{\"uri\":\""u8, 0x80, .. "\"}}"u8];

        var exception = Assert.Throws<NistFormatException>(() => NistPulse.Parse(json));

        Assert.Contains("not well-formed Unicode", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_WithLoneSurrogateEscapeInAName_Throws()
    {
        var exception = Assert.Throws<NistFormatException>(() =>
            NistPulse.Parse("""{"\uD800":1}"""u8)
        );

        Assert.Contains("not well-formed Unicode", exception.Message, StringComparison.Ordinal);
    }

    #endregion

    #region Verify Tests

    [Theory]
    [InlineData(2043330, "5b6386e6", NistFixtures.CurrentCertificateId)]
    [InlineData(2043331, "5b6386e6", NistFixtures.CurrentCertificateId)]
    [InlineData(1484768, "e692b0f6", NistFixtures.PreviousCertificateId)]
    [InlineData(1484769, "528943a5", NistFixtures.RotatedCertificateId)]
    public void Verify_WhenPulseIsNists_Succeeds(long index, string prefix, string id)
    {
        var document = Document(index);
        var pulse = NistPulse.Parse(document);
        var certificateDer = NistFixtures.Certificate(prefix);
        var certificateId = Convert.FromHexString(id);
        using var certificate = NistCertificate.Parse(certificateDer, certificateId);

        pulse.Verify(certificate);
    }

    [Fact]
    public void Verify_WhenPulseNamesAnotherCertificate_Throws()
    {
        var document = Document(NistFixtures.LatestIndex);
        var pulse = NistPulse.Parse(document);
        var certificateDer = NistFixtures.Certificate("e692b0f6");
        var certificateId = Convert.FromHexString(NistFixtures.PreviousCertificateId);
        using var certificate = NistCertificate.Parse(certificateDer, certificateId);

        var exception = Assert.Throws<NistFormatException>(() => pulse.Verify(certificate));

        Assert.Contains("names another certificate", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_WhenLocalRandomValueIsAltered_Throws()
    {
        var document = Document(
            NistFixtures.LatestIndex,
            "pulse.localRandomValue",
            $"\"{Hex(HashBytes)}\""
        );
        var pulse = NistPulse.Parse(document);
        var certificateDer = NistFixtures.Certificate("5b6386e6");
        var certificateId = Convert.FromHexString(NistFixtures.CurrentCertificateId);
        using var certificate = NistCertificate.Parse(certificateDer, certificateId);

        var exception = Assert.Throws<NistFormatException>(() => pulse.Verify(certificate));

        Assert.Contains("does not verify", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_WhenBuiltPulseIsValid_Succeeds()
    {
        using var signer = new NistTestSigner();
        using var certificate = signer.Certificate();
        var json = NistPulseBuilder.For(5).Build(signer);
        var pulse = ParseBuilt(json);

        pulse.Verify(certificate);
    }

    [Fact]
    public void Verify_WhenBuiltPulseHasNoExternal_Succeeds()
    {
        using var signer = new NistTestSigner();
        using var certificate = signer.Certificate();
        var builder = NistPulseBuilder.For(5) with { IncludeExternal = false };
        var json = builder.Build(signer);
        var pulse = ParseBuilt(json);

        pulse.Verify(certificate);
    }

    [Fact]
    public void Verify_WhenPulseIsDatedAfterTheCertificate_Throws()
    {
        var notBefore = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var notAfter = new DateTimeOffset(2027, 12, 31, 0, 0, 0, TimeSpan.Zero);
        using var signer = new NistTestSigner(notBefore: notBefore, notAfter: notAfter);
        using var certificate = signer.Certificate();
        var builder = NistPulseBuilder.For(5) with { Timestamp = notAfter.AddMilliseconds(1) };
        var json = builder.Build(signer);
        var pulse = ParseBuilt(json);

        var exception = Assert.Throws<NistFormatException>(() => pulse.Verify(certificate));

        Assert.Contains("validity", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_WhenPulseIsDatedBeforeTheCertificate_Throws()
    {
        var notBefore = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var notAfter = new DateTimeOffset(2027, 12, 31, 0, 0, 0, TimeSpan.Zero);
        using var signer = new NistTestSigner(notBefore: notBefore, notAfter: notAfter);
        using var certificate = signer.Certificate();
        var builder = NistPulseBuilder.For(5) with { Timestamp = notBefore.AddMilliseconds(-1) };
        var json = builder.Build(signer);
        var pulse = ParseBuilt(json);

        var exception = Assert.Throws<NistFormatException>(() => pulse.Verify(certificate));

        Assert.Contains("validity", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_WhenPulseIsDatedAtNotBefore_Succeeds()
    {
        var notBefore = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var notAfter = new DateTimeOffset(2027, 12, 31, 0, 0, 0, TimeSpan.Zero);
        using var signer = new NistTestSigner(notBefore: notBefore, notAfter: notAfter);
        using var certificate = signer.Certificate();
        var builder = NistPulseBuilder.For(5) with { Timestamp = notBefore };
        var json = builder.Build(signer);
        var pulse = ParseBuilt(json);

        pulse.Verify(certificate);
    }

    [Fact]
    public void Verify_WhenPulseIsDatedAtNotAfter_Succeeds()
    {
        var notBefore = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var notAfter = new DateTimeOffset(2027, 12, 31, 0, 0, 0, TimeSpan.Zero);
        using var signer = new NistTestSigner(notBefore: notBefore, notAfter: notAfter);
        using var certificate = signer.Certificate();
        var builder = NistPulseBuilder.For(5) with { Timestamp = notAfter };
        var json = builder.Build(signer);
        var pulse = ParseBuilt(json);

        pulse.Verify(certificate);
    }

    [Fact]
    public void Verify_WhenVersionIsNotSupported_Throws()
    {
        using var signer = new NistTestSigner();
        using var certificate = signer.Certificate();
        var builder = NistPulseBuilder.For(5) with { Version = "2.1" };
        var json = builder.Build(signer);
        var pulse = ParseBuilt(json);

        var exception = Assert.Throws<NistFormatException>(() => pulse.Verify(certificate));

        Assert.Contains("version", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_WhenCipherSuiteIsNotSupported_Throws()
    {
        using var signer = new NistTestSigner();
        using var certificate = signer.Certificate();
        var builder = NistPulseBuilder.For(5) with { CipherSuite = 1 };
        var json = builder.Build(signer);
        var pulse = ParseBuilt(json);

        var exception = Assert.Throws<NistFormatException>(() => pulse.Verify(certificate));

        Assert.Contains("Cipher suite", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_WhenOutputIsReplaced_Throws()
    {
        using var signer = new NistTestSigner();
        using var certificate = signer.Certificate();
        var builder = NistPulseBuilder.For(5) with
        {
            AfterSigning = node => node["outputValue"] = Hex(HashBytes),
        };
        var json = builder.Build(signer);
        var pulse = ParseBuilt(json);

        var exception = Assert.Throws<NistFormatException>(() => pulse.Verify(certificate));

        Assert.Contains(
            "The output value of pulse 5 is not the hash",
            exception.Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Verify_WhenSignatureIsCut_Throws()
    {
        using var signer = new NistTestSigner();
        using var certificate = signer.Certificate();
        const int cutBytes = 255;
        var builder = NistPulseBuilder.For(5) with
        {
            AfterSigning = node =>
                node["signatureValue"] = ((string?)node["signatureValue"])?[..(cutBytes * 2)],
        };
        var json = builder.Build(signer);
        var pulse = ParseBuilt(json);

        var exception = Assert.Throws<NistFormatException>(() => pulse.Verify(certificate));

        Assert.Contains(
            "The signature has 255 bytes, not 256.",
            exception.Message,
            StringComparison.Ordinal
        );
    }

    #endregion

    #region VerifyFollows Tests

    [Theory]
    [InlineData(2043330, 2043331)]
    [InlineData(1484768, 1484769)]
    public void VerifyFollows_WhenPulsesAreNists_Succeeds(long predecessor, long index)
    {
        var document = Document(index);
        var pulse = NistPulse.Parse(document);
        var predecessorDocument = Document(predecessor);
        var predecessorPulse = NistPulse.Parse(predecessorDocument);

        pulse.VerifyFollows(predecessorPulse);
    }

    [Fact]
    public void VerifyFollows_WhenPredecessorIsNotAdjacent_Throws()
    {
        var document = Document(NistFixtures.LatestIndex);
        var pulse = NistPulse.Parse(document);
        var predecessorDocument = Document(1484768);
        var predecessor = NistPulse.Parse(predecessorDocument);

        var exception = Assert.Throws<NistFormatException>(() => pulse.VerifyFollows(predecessor));

        Assert.Contains("does not precede", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void VerifyFollows_WhenPredecessorIsOnAnotherChain_Throws()
    {
        using var signer = new NistTestSigner();
        var predecessorBuilder = NistPulseBuilder.For(10) with { ChainIndex = 3 };
        var predecessorJson = predecessorBuilder.Build(signer);
        var previous = NistPulseBuilder.OutputOf(predecessorJson);
        var pulseBuilder = NistPulseBuilder.For(11) with { Previous = previous };
        var pulseJson = pulseBuilder.Build(signer);
        var predecessor = ParseBuilt(predecessorJson);
        var pulse = ParseBuilt(pulseJson);

        var exception = Assert.Throws<NistFormatException>(() => pulse.VerifyFollows(predecessor));

        Assert.Contains(
            "Pulse 10 of chain 3 does not precede pulse 11 of chain 2.",
            exception.Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void VerifyFollows_WhenBuiltPulsesFollow_Succeeds()
    {
        using var signer = new NistTestSigner();
        var (predecessor, pulse) = BuiltPair(signer, null);

        pulse.VerifyFollows(predecessor);
    }

    [Fact]
    public void VerifyFollows_WhenPreviousIsWrong_Throws()
    {
        using var signer = new NistTestSigner();
        var (predecessor, pulse) = BuiltPair(signer, builder =>
            builder with
            {
                Previous = NistPulseBuilder.LocalRandom(99),
            }
        );

        var exception = Assert.Throws<NistFormatException>(() => pulse.VerifyFollows(predecessor));

        Assert.Contains("does not chain to", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void VerifyFollows_WhenPrecommitmentIsZero_Throws()
    {
        using var signer = new NistTestSigner();
        var predecessorBuilder = NistPulseBuilder.For(10) with { Precommitment = new byte[64] };
        var predecessorJson = predecessorBuilder.Build(signer);
        var previous = NistPulseBuilder.OutputOf(predecessorJson);
        var pulseBuilder = NistPulseBuilder.For(11) with { Previous = previous };
        var pulseJson = pulseBuilder.Build(signer);
        var predecessor = ParseBuilt(predecessorJson);
        var pulse = ParseBuilt(pulseJson);

        var exception = Assert.Throws<NistFormatException>(() => pulse.VerifyFollows(predecessor));

        Assert.Contains("precommitment", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void VerifyFollows_WhenPredecessorIsLater_Throws()
    {
        using var signer = new NistTestSigner();
        var later = NistPulseBuilder.For(11).Timestamp.AddMinutes(1);
        var predecessorBuilder = NistPulseBuilder.For(10) with { Timestamp = later };
        var predecessorJson = predecessorBuilder.Build(signer);
        var previous = NistPulseBuilder.OutputOf(predecessorJson);
        var pulseBuilder = NistPulseBuilder.For(11) with { Previous = previous };
        var pulseJson = pulseBuilder.Build(signer);
        var predecessor = ParseBuilt(predecessorJson);
        var pulse = ParseBuilt(pulseJson);

        var exception = Assert.Throws<NistFormatException>(() => pulse.VerifyFollows(predecessor));

        Assert.Contains("is not earlier", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void VerifyFollows_WhenPredecessorHasTheSameTimestamp_Throws()
    {
        using var signer = new NistTestSigner();
        var same = NistPulseBuilder.For(11).Timestamp;
        var predecessorBuilder = NistPulseBuilder.For(10) with { Timestamp = same };
        var predecessorJson = predecessorBuilder.Build(signer);
        var previous = NistPulseBuilder.OutputOf(predecessorJson);
        var pulseBuilder = NistPulseBuilder.For(11) with { Previous = previous };
        var pulseJson = pulseBuilder.Build(signer);
        var predecessor = ParseBuilt(predecessorJson);
        var pulse = ParseBuilt(pulseJson);

        var exception = Assert.Throws<NistFormatException>(() => pulse.VerifyFollows(predecessor));

        Assert.Contains("is not earlier", exception.Message, StringComparison.Ordinal);
    }

    #endregion
}
