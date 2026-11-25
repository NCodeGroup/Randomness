// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using NCode.Randomness.Nist.Pulses;
using NCode.Randomness.Nist.Tests.Fixtures;

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
}
