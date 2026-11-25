// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using NCode.Randomness.Nist.Pulses;
using NCode.Randomness.Nist.Tests.Fixtures;
using NCode.Randomness.Nist.Tests.TestNist;

namespace NCode.Randomness.Nist.Tests.Pulses;

public class NistPulseTests
{
    private const int ExternalLength = 4 + 64 + 4 + 4 + 64;

    private static string Hex(int bytes) => new('A', bytes * 2);

    private static JsonElement Document(long index, string? path = null, string? json = null)
    {
        var node = JsonNode.Parse(NistFixtures.Pulse(index));
        if (path is not null)
        {
            node = Mutate(node, path, json);
        }

        return JsonElement.Parse(
            node?.ToJsonString() ?? "null",
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

    public static TheoryData<string, string?, string> BrokenDocuments =>
        new()
        {
            { "", "[]", "not a JSON object" },
            { "pulse", null, "'pulse'" },
            { "pulse.pulseIndex", null, "'pulseIndex'" },
            { "pulse.pulseIndex", "0", "'pulseIndex'" },
            { "pulse.statusCode", "-1", "'statusCode'" },
            { "pulse.localRandomValue", $"\"{Hex(63)}\"", "'localRandomValue'" },
            { "pulse.outputValue", "\"not hex\"", "'outputValue' is not hexadecimal" },
            { "pulse.listValues", "[]", "'listValues'" },
            { "pulse.listValues.0.type", "\"hour\"", "'previous'" },
            { "pulse.external", "\"none\"", "'external'" },
            { "pulse.timeStamp", "\"2026-11-25T14:26:00Z\"", "'timeStamp'" },
        };

    #region Parse Tests

    [Fact]
    public void Parse_WhenPulseIsNists_ReadsItsMembers()
    {
        var pulse = NistPulse.Parse(Document(NistFixtures.LatestIndex));

        Assert.Equal("2.0", pulse.Version);
        Assert.Equal(0u, pulse.CipherSuite);
        Assert.Equal<byte>(
            Convert.FromHexString(NistFixtures.CurrentCertificateId),
            pulse.CertificateId
        );
        Assert.Equal(2, pulse.ChainIndex);
        Assert.Equal(NistFixtures.LatestIndex, pulse.PulseIndex);
        Assert.Equal(NistFixtures.LatestTimestamp, pulse.Timestamp);
        Assert.Equal<byte>(Convert.FromHexString(NistFixtures.LatestOutput), pulse.Output);
        Assert.Equal<byte>(Convert.FromHexString(NistFixtures.PreviousOutput), pulse.Previous);
        Assert.Equal(64, pulse.LocalRandomValue.Length);
        Assert.Equal(64, pulse.Precommitment.Length);
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
        Assert.Equal<byte>(SHA512.HashData(hashed), pulse.Output);
    }

    [Fact]
    public void Parse_WhenExternalIsAbsent_SignsExternalOut()
    {
        var withExternal = NistPulse.Parse(Document(NistFixtures.LatestIndex));

        var without = NistPulse.Parse(Document(NistFixtures.LatestIndex, "pulse.external"));

        Assert.Equal(withExternal.SignedBytes.Length - ExternalLength, without.SignedBytes.Length);
    }

    [Fact]
    public void Parse_WhenExternalIsNull_SignsExternalOut()
    {
        var withExternal = NistPulse.Parse(Document(NistFixtures.LatestIndex));

        var without = NistPulse.Parse(Document(NistFixtures.LatestIndex, "pulse.external", "null"));

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

    #endregion

    #region Verify Tests

    [Theory]
    [InlineData(2043330, "5b6386e6", NistFixtures.CurrentCertificateId)]
    [InlineData(2043331, "5b6386e6", NistFixtures.CurrentCertificateId)]
    [InlineData(1484768, "e692b0f6", NistFixtures.PreviousCertificateId)]
    [InlineData(1484769, "528943a5", NistFixtures.RotatedCertificateId)]
    public void Verify_WhenPulseIsNists_Succeeds(long index, string prefix, string id)
    {
        var pulse = NistPulse.Parse(Document(index));
        using var certificate = NistCertificate.Parse(
            NistFixtures.Certificate(prefix),
            Convert.FromHexString(id)
        );

        pulse.Verify(certificate);
    }

    [Fact]
    public void Verify_WhenPulseNamesAnotherCertificate_Throws()
    {
        var pulse = NistPulse.Parse(Document(NistFixtures.LatestIndex));
        using var certificate = NistCertificate.Parse(
            NistFixtures.Certificate("e692b0f6"),
            Convert.FromHexString(NistFixtures.PreviousCertificateId)
        );

        var exception = Assert.Throws<NistFormatException>(() => pulse.Verify(certificate));

        Assert.Contains("names another certificate", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_WhenLocalRandomValueIsAltered_Throws()
    {
        var pulse = NistPulse.Parse(
            Document(NistFixtures.LatestIndex, "pulse.localRandomValue", $"\"{Hex(64)}\"")
        );
        using var certificate = NistCertificate.Parse(
            NistFixtures.Certificate("5b6386e6"),
            Convert.FromHexString(NistFixtures.CurrentCertificateId)
        );

        var exception = Assert.Throws<NistFormatException>(() => pulse.Verify(certificate));

        Assert.Contains("does not verify", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_WhenBuiltPulseIsValid_Succeeds()
    {
        using var signer = new NistTestSigner();
        using var certificate = signer.Certificate();
        var pulse = ParseBuilt(NistPulseBuilder.For(5).Build(signer));

        pulse.Verify(certificate);
    }

    [Fact]
    public void Verify_WhenBuiltPulseHasNoExternal_Succeeds()
    {
        using var signer = new NistTestSigner();
        using var certificate = signer.Certificate();
        var builder = NistPulseBuilder.For(5) with { IncludeExternal = false };
        var pulse = ParseBuilt(builder.Build(signer));

        pulse.Verify(certificate);
    }

    [Fact]
    public void Verify_WhenPulseIsDatedAfterTheCertificate_Throws()
    {
        using var signer = new NistTestSigner();
        using var certificate = signer.Certificate();
        var builder = NistPulseBuilder.For(5) with
        {
            Timestamp = new DateTimeOffset(2028, 1, 1, 0, 0, 0, TimeSpan.Zero),
        };
        var pulse = ParseBuilt(builder.Build(signer));

        var exception = Assert.Throws<NistFormatException>(() => pulse.Verify(certificate));

        Assert.Contains("validity", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_WhenPulseIsDatedBeforeTheCertificate_Throws()
    {
        using var signer = new NistTestSigner();
        using var certificate = signer.Certificate();
        var builder = NistPulseBuilder.For(5) with
        {
            Timestamp = new DateTimeOffset(2025, 12, 31, 0, 0, 0, TimeSpan.Zero),
        };
        var pulse = ParseBuilt(builder.Build(signer));

        var exception = Assert.Throws<NistFormatException>(() => pulse.Verify(certificate));

        Assert.Contains("validity", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_WhenVersionIsNotSupported_Throws()
    {
        using var signer = new NistTestSigner();
        using var certificate = signer.Certificate();
        var builder = NistPulseBuilder.For(5) with { Version = "2.1" };
        var pulse = ParseBuilt(builder.Build(signer));

        var exception = Assert.Throws<NistFormatException>(() => pulse.Verify(certificate));

        Assert.Contains("version", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_WhenCipherSuiteIsNotSupported_Throws()
    {
        using var signer = new NistTestSigner();
        using var certificate = signer.Certificate();
        var builder = NistPulseBuilder.For(5) with { CipherSuite = 1 };
        var pulse = ParseBuilt(builder.Build(signer));

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
            AfterSigning = node => node["outputValue"] = Hex(64),
        };
        var pulse = ParseBuilt(builder.Build(signer));

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
        var builder = NistPulseBuilder.For(5) with
        {
            AfterSigning = node =>
                node["signatureValue"] = ((string?)node["signatureValue"])?[..(255 * 2)],
        };
        var pulse = ParseBuilt(builder.Build(signer));

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
        var pulse = NistPulse.Parse(Document(index));

        pulse.VerifyFollows(NistPulse.Parse(Document(predecessor)));
    }

    [Fact]
    public void VerifyFollows_WhenPredecessorIsNotAdjacent_Throws()
    {
        var pulse = NistPulse.Parse(Document(NistFixtures.LatestIndex));
        var predecessor = NistPulse.Parse(Document(1484768));

        var exception = Assert.Throws<NistFormatException>(() => pulse.VerifyFollows(predecessor));

        Assert.Contains("does not precede", exception.Message, StringComparison.Ordinal);
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
        var predecessorJson = (
            NistPulseBuilder.For(10) with
            {
                Precommitment = new byte[64],
            }
        ).Build(signer);
        var pulseJson = (
            NistPulseBuilder.For(11) with
            {
                Previous = NistPulseBuilder.OutputOf(predecessorJson),
            }
        ).Build(signer);
        var predecessor = ParseBuilt(predecessorJson);
        var pulse = ParseBuilt(pulseJson);

        var exception = Assert.Throws<NistFormatException>(() => pulse.VerifyFollows(predecessor));

        Assert.Contains("precommitment", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void VerifyFollows_WhenPredecessorIsLater_Throws()
    {
        using var signer = new NistTestSigner();
        var predecessorJson = (
            NistPulseBuilder.For(10) with
            {
                Timestamp = NistPulseBuilder.For(11).Timestamp.AddMinutes(1),
            }
        ).Build(signer);
        var pulseJson = (
            NistPulseBuilder.For(11) with
            {
                Previous = NistPulseBuilder.OutputOf(predecessorJson),
            }
        ).Build(signer);
        var predecessor = ParseBuilt(predecessorJson);
        var pulse = ParseBuilt(pulseJson);

        var exception = Assert.Throws<NistFormatException>(() => pulse.VerifyFollows(predecessor));

        Assert.Contains("is not earlier", exception.Message, StringComparison.Ordinal);
    }

    #endregion

    private static NistPulse ParseBuilt(string json) =>
        NistPulse.Parse(
            JsonElement.Parse(
                json,
                new JsonDocumentOptions { MaxDepth = 16, AllowDuplicateProperties = false }
            )
        );

    private static (NistPulse Predecessor, NistPulse Pulse) BuiltPair(
        NistTestSigner signer,
        Func<NistPulseBuilder, NistPulseBuilder>? alter
    )
    {
        var predecessorJson = NistPulseBuilder.For(10).Build(signer);
        var builder = NistPulseBuilder.For(11) with
        {
            Previous = NistPulseBuilder.OutputOf(predecessorJson),
        };
        if (alter is not null)
        {
            builder = alter(builder);
        }

        return (ParseBuilt(predecessorJson), ParseBuilt(builder.Build(signer)));
    }
}
