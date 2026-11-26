// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using System.Text.Json;
using NCode.Randomness.Curby.Tests.Fixtures;
using NCode.Randomness.Curby.Tests.TestTwine;
using NCode.Randomness.Curby.Twine;

namespace NCode.Randomness.Curby.Tests.Twine;

public class TwineVerificationTests
{
    private const string RngFamily = "rng";

    private static Cid Pinned { get; } = Cid.Parse(CurbyFixtures.ChainId);

    private static DateTimeOffset Start { get; } = new(2026, 11, 25, 0, 0, 0, TimeSpan.Zero);

    internal static TwineDocument Document(string json) =>
        TwineDocument.Parse(JsonElement.Parse(json));

    internal static TwineChain VerifyCurbyChain()
    {
        var chainDocument = Document(CurbyFixtures.Chain);
        return TwineChain.Verify(chainDocument, Pinned, RngFamily);
    }

    private static List<JsonElement> RoundPulses() =>
        [.. JsonElement.Parse(CurbyQuantumFixtures.RoundJson).EnumerateArray()];

    #region CURBy Fixture Tests

    [Fact]
    public void Verify_WithCurbyChain_AcceptsThePinnedChain()
    {
        using var chain = VerifyCurbyChain();

        Assert.Equal(Pinned, chain.Cid);
        Assert.Equal(4096, chain.Key.KeySize);
    }

    [Fact]
    public void Verify_WithCurbyPulses_ReadsEachField()
    {
        using var chain = VerifyCurbyChain();

        var pulse = TwinePulse.Verify(
            Document(CurbyFixtures.Pulse(CurbyFixtures.LatestRound)),
            chain
        );

        Assert.Equal(CurbyFixtures.LatestRound, pulse.Index);
        Assert.Equal(CurbyFixtures.LatestPulseId, pulse.Cid.ToString());
        Assert.Equal(CurbyFixtures.LatestRandomness, Convert.ToHexStringLower(pulse.Cid.Digest));
        Assert.Equal(CurbyFixtures.LatestTimestamp, pulse.Timestamp);
        Assert.Equal(
            CurbyFixtures.CidOf(CurbyFixtures.Pulse(CurbyFixtures.LatestRound - 1)),
            pulse.Previous?.ToString()
        );
    }

    [Fact]
    public void VerifyFollows_WithConsecutiveCurbyPulses_AcceptsThePrecommitments()
    {
        using var chain = VerifyCurbyChain();
        var first = TwinePulse.Verify(Document(CurbyFixtures.Pulse(1620421)), chain);
        var second = TwinePulse.Verify(Document(CurbyFixtures.Pulse(1620422)), chain);
        var third = TwinePulse.Verify(Document(CurbyFixtures.Pulse(1620423)), chain);

        second.VerifyFollows(first);
        third.VerifyFollows(second);

        Assert.Throws<TwineFormatException>(() => third.VerifyFollows(first));
    }

    [Fact]
    public void Parse_WithTamperedCurbyPulse_RefusesItsCid()
    {
        var json = CurbyFixtures
            .Pulse(CurbyFixtures.LatestRound)
            .Replace(
                "2026-11-25T02:44:00.150Z",
                "2026-11-25T02:44:00.151Z",
                StringComparison.Ordinal
            );

        var exception = Assert.Throws<TwineFormatException>(() => Document(json));

        Assert.Contains("hashes to", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_WithCurbyChainAgainstAnotherPin_Throws()
    {
        using var builder = new TwineBuilder();
        var chainDocument = Document(CurbyFixtures.Chain);

        var exception = Assert.Throws<TwineFormatException>(() =>
            TwineChain.Verify(chainDocument, builder.ChainCid, RngFamily)
        );

        Assert.Contains("pinned", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_WithPulseOfAnotherChain_Throws()
    {
        using var curby = VerifyCurbyChain();
        using var builder = new TwineBuilder();
        var pulse = builder.Pulses(1, Start)[0];

        var exception = Assert.Throws<TwineFormatException>(() =>
            TwinePulse.Verify(Document(pulse.Json), curby)
        );

        Assert.Contains("belongs to chain", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_WithNonObject_Throws()
    {
        Assert.Throws<TwineFormatException>(() => TwineDocument.Parse(JsonElement.Parse("[]")));
    }

    #endregion

    #region Parse Tests

    [Fact]
    public void Parse_WithCurbyPulseBytes_ReadsItsCid()
    {
        var json = CurbyFixtures.Pulse(CurbyFixtures.LatestRound);
        var bytes = Encoding.UTF8.GetBytes(json);

        var document = TwineDocument.Parse(bytes);

        var cid = document.Cid.ToString();

        Assert.Equal(CurbyFixtures.LatestPulseId, cid);
    }

    [Fact]
    public void Parse_WithMalformedJsonBytes_Throws()
    {
        var exception = Assert.Throws<TwineFormatException>(() => TwineDocument.Parse("{"u8));

        Assert.Contains("not valid JSON", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_WithInvalidUtf8InAName_Throws()
    {
        byte[] json = [.. "{\"cid"u8, 0x80, .. "\":1}"u8];

        var exception = Assert.Throws<TwineFormatException>(() => TwineDocument.Parse(json));

        Assert.Contains("not well-formed Unicode", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_WithLoneSurrogateEscapeInAName_Throws()
    {
        var exception = Assert.Throws<TwineFormatException>(() =>
            TwineDocument.Parse("""{"\uD800":1}"""u8)
        );

        Assert.Contains("not well-formed Unicode", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_WithCurbyQDocuments_RecomputesEveryCid()
    {
        var bellPulse = CurbyQuantumFixtures.BellPulse(CurbyQuantumFixtures.BellResponseIndex);
        var firstSeedPulse = CurbyQuantumFixtures.SeedPulse(
            CurbyQuantumFixtures.FirstSeedPulseIndex
        );
        var secondSeedPulse = CurbyQuantumFixtures.SeedPulse(
            CurbyQuantumFixtures.SecondSeedPulseIndex
        );
        var thirdSeedPulse = CurbyQuantumFixtures.SeedPulse(
            CurbyQuantumFixtures.ThirdSeedPulseIndex
        );
        List<string> documents =
        [
            CurbyQuantumFixtures.Chain,
            CurbyQuantumFixtures.BellChain,
            CurbyQuantumFixtures.SeedChain,
            bellPulse,
            firstSeedPulse,
            secondSeedPulse,
            thirdSeedPulse,
        ];
        var roundDocuments = RoundPulses().Select(element => element.GetRawText()).ToList();
        documents.AddRange(roundDocuments);

        foreach (var json in documents)
        {
            var document = Document(json);
            var expected = CurbyFixtures.CidOf(json);
            var actual = document.Cid.ToString();

            Assert.Equal(expected, actual);
        }
    }

    #endregion

    #region ParseArray Tests

    [Fact]
    public void ParseArray_WithCurbyQRoundBytes_ReadsEachDocument()
    {
        var bytes = Encoding.UTF8.GetBytes(CurbyQuantumFixtures.RoundJson);

        var documents = TwineDocument.ParseArray(bytes);

        Assert.Equal(3, documents.Length);
        var requestId = documents[0].Cid.ToString();
        var precommitId = documents[1].Cid.ToString();
        var resultId = documents[2].Cid.ToString();

        Assert.Equal(CurbyQuantumFixtures.RequestId, requestId);
        Assert.Equal(CurbyQuantumFixtures.PrecommitId, precommitId);
        Assert.Equal(CurbyQuantumFixtures.ResultId, resultId);
    }

    [Fact]
    public void ParseArray_WithObjectBytes_Throws()
    {
        var exception = Assert.Throws<TwineFormatException>(() => TwineDocument.ParseArray("{}"u8));

        Assert.Contains("not an array", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ParseArray_WithMalformedJsonBytes_Throws()
    {
        var exception = Assert.Throws<TwineFormatException>(() => TwineDocument.ParseArray("["u8));

        Assert.Contains("not valid JSON", exception.Message, StringComparison.Ordinal);
    }

    #endregion

    #region Verify Tests

    [Fact]
    public void Verify_WithCurbyQPulses_AcceptsEachSignature()
    {
        var quantumDocument = Document(CurbyQuantumFixtures.Chain);
        var quantumCid = Cid.Parse(CurbyQuantumFixtures.ChainId);
        var bellDocument = Document(CurbyQuantumFixtures.BellChain);
        var bellCid = Cid.Parse(CurbyQuantumFixtures.BellChainId);
        var seedDocument = Document(CurbyQuantumFixtures.SeedChain);
        var seedCid = Cid.Parse(CurbyQuantumFixtures.SeedChainId);
        using var quantum = TwineChain.Verify(quantumDocument, quantumCid, "diqrng");
        using var bell = TwineChain.Verify(bellDocument, bellCid, "bell");
        using var seed = TwineChain.Verify(seedDocument, seedCid, null);

        var round = RoundPulses()
            .Select(element =>
            {
                var json = element.GetRawText();
                var document = Document(json);
                return TwineSignedPulse.Verify(document, quantum);
            })
            .ToList();
        var responseJson = CurbyQuantumFixtures.BellPulse(CurbyQuantumFixtures.BellResponseIndex);
        var responseDocument = Document(responseJson);
        var response = TwineSignedPulse.Verify(responseDocument, bell);
        var seeds = new[]
        {
            CurbyQuantumFixtures.FirstSeedPulseIndex,
            CurbyQuantumFixtures.SecondSeedPulseIndex,
            CurbyQuantumFixtures.ThirdSeedPulseIndex,
        }
            .Select(index =>
            {
                var seedJson = CurbyQuantumFixtures.SeedPulse(index);
                var document = Document(seedJson);
                return TwineSignedPulse.Verify(document, seed);
            })
            .ToList();
        var requestId = round[0].Cid.ToString();
        var precommitId = round[1].Cid.ToString();
        var resultId = round[2].Cid.ToString();
        var responseId = response.Cid.ToString();
        var seedPulseId = seeds[2].Cid.ToString();

        Assert.Equal(3, round.Count);
        Assert.Equal(CurbyQuantumFixtures.RequestId, requestId);
        Assert.Equal(CurbyQuantumFixtures.PrecommitId, precommitId);
        Assert.Equal(CurbyQuantumFixtures.ResultId, resultId);
        Assert.Equal(CurbyQuantumFixtures.BellResponseId, responseId);
        Assert.Equal(CurbyQuantumFixtures.SeedPulseId, seedPulseId);
    }

    #endregion

    #region Chain Rule Tests

    [Fact]
    public void Verify_WithBuiltChain_Succeeds()
    {
        using var builder = new TwineBuilder();
        var chainDocument = Document(builder.ChainJson);

        using var chain = TwineChain.Verify(chainDocument, builder.ChainCid, RngFamily);

        Assert.Equal(builder.ChainCid, chain.Cid);
    }

    [Fact]
    public void Verify_WithUnknownSpecification_Throws()
    {
        using var builder = new TwineBuilder(specification: "twine/2.0.x/rng/1.0.x");
        var chainDocument = Document(builder.ChainJson);

        var exception = Assert.Throws<TwineFormatException>(() =>
            TwineChain.Verify(chainDocument, builder.ChainCid, RngFamily)
        );

        Assert.Contains("twine/2.0.x", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_WithChainOfAnotherFamily_Throws()
    {
        using var builder = new TwineBuilder(specification: "twine/1.0.x/bell/1.0.x");
        var document = Document(builder.ChainJson);

        var exception = Assert.Throws<TwineFormatException>(() =>
            TwineChain.Verify(document, builder.ChainCid, RngFamily)
        );

        Assert.Contains("not twine/1.x/rng/1.x", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_WithoutFamily_AcceptsAnyTwineOneChain()
    {
        using var builder = new TwineBuilder(specification: "twine/1.0.x/bell/1.0.x");
        var document = Document(builder.ChainJson);

        using var chain = TwineChain.Verify(document, builder.ChainCid, null);

        Assert.Equal(builder.ChainCid, chain.Cid);
    }

    [Fact]
    public void Verify_WithoutFamilyAndAnotherTwineVersion_Throws()
    {
        using var builder = new TwineBuilder(specification: "twine/2.0.x/mirror/1.0.x");
        var document = Document(builder.ChainJson);

        var exception = Assert.Throws<TwineFormatException>(() =>
            TwineChain.Verify(document, builder.ChainCid, null)
        );

        Assert.Contains("not twine/1.x.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_WithShortKey_Throws()
    {
        using var builder = new TwineBuilder(keySize: 1024);
        var chainDocument = Document(builder.ChainJson);

        var exception = Assert.Throws<TwineFormatException>(() =>
            TwineChain.Verify(chainDocument, builder.ChainCid, RngFamily)
        );

        Assert.Contains("2048", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{"alg":"RS512"}""")]
    [InlineData("""{"alg":"none"}""")]
    [InlineData("""{"alg":"RS256","jwk":{}}""")]
    [InlineData("""{"alg":"RS256","crit":["b64"],"b64":false}""")]
    [InlineData("""{"typ":"JWT"}""")]
    [InlineData("""[]""")]
    public void Verify_WithDisallowedHeader_Throws(string header)
    {
        using var builder = new TwineBuilder(header: header);
        var chainDocument = Document(builder.ChainJson);

        Assert.Throws<TwineFormatException>(() =>
            TwineChain.Verify(chainDocument, builder.ChainCid, RngFamily)
        );
    }

    [Fact]
    public void Verify_WithHeaderThatIsNotJson_KeepsTheJsonException()
    {
        using var builder = new TwineBuilder(header: "{");
        var chainDocument = Document(builder.ChainJson);

        var exception = Assert.Throws<TwineFormatException>(() =>
            TwineChain.Verify(chainDocument, builder.ChainCid, RngFamily)
        );

        Assert.IsAssignableFrom<JsonException>(exception.InnerException);
    }

    [Theory]
    [InlineData("""{"alg":"\uD800"}""")]
    [InlineData("""{"alg":"RS256","\uDC00":1}""")]
    public void Verify_WithHeaderThatIsNotUnicode_Throws(string header)
    {
        using var builder = new TwineBuilder(header: header);
        var chainDocument = Document(builder.ChainJson);

        var exception = Assert.Throws<TwineFormatException>(() =>
            TwineChain.Verify(chainDocument, builder.ChainCid, RngFamily)
        );

        Assert.Contains("not well-formed Unicode", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_WithHeaderHoldingInvalidUtf8_Throws()
    {
        byte[] header = [.. "{\"alg\":\""u8, 0x80, .. "\"}"u8];
        using var builder = new TwineBuilder(headerUtf8: header);
        var chainDocument = Document(builder.ChainJson);

        var exception = Assert.Throws<TwineFormatException>(() =>
            TwineChain.Verify(chainDocument, builder.ChainCid, RngFamily)
        );

        Assert.Contains("not well-formed Unicode", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_WithHeaderNamingTypeAndKey_Succeeds()
    {
        using var builder = new TwineBuilder(header: """{"alg":"RS256","typ":"JWT","kid":"1"}""");
        var chainDocument = Document(builder.ChainJson);

        using var chain = TwineChain.Verify(chainDocument, builder.ChainCid, RngFamily);

        Assert.Equal(builder.ChainCid, chain.Cid);
    }

    #endregion

    #region Pulse Rule Tests

    [Fact]
    public void Verify_WithBuiltPulses_FollowEachOther()
    {
        using var builder = new TwineBuilder();
        var chainDocument = Document(builder.ChainJson);
        using var chain = TwineChain.Verify(chainDocument, builder.ChainCid, RngFamily);
        var pulses = builder
            .Pulses(3, Start)
            .Select(pulse => TwinePulse.Verify(Document(pulse.Json), chain))
            .ToList();

        pulses[1].VerifyFollows(pulses[0]);
        pulses[2].VerifyFollows(pulses[1]);

        Assert.Null(pulses[0].Previous);
    }

    [Fact]
    public void Verify_WithSignatureOverOtherContent_Throws()
    {
        using var builder = new TwineBuilder();
        var chainDocument = Document(builder.ChainJson);
        using var chain = TwineChain.Verify(chainDocument, builder.ChainCid, RngFamily);
        var content = builder.PulseContent(1, null, new byte[64], new byte[64], Start);
        var other = builder.PulseContent(2, null, new byte[64], new byte[64], Start);
        var forged = builder.Seal(content, builder.Sign(other));

        var exception = Assert.Throws<TwineFormatException>(() =>
            TwinePulse.Verify(Document(forged.Json), chain)
        );

        Assert.Contains("other content", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_WithSignatureOfAnotherKey_Throws()
    {
        using var builder = new TwineBuilder();
        using var impostor = new TwineBuilder();
        var chainDocument = Document(builder.ChainJson);
        using var chain = TwineChain.Verify(chainDocument, builder.ChainCid, RngFamily);
        var content = builder.PulseContent(1, null, new byte[64], new byte[64], Start);
        var forged = builder.Seal(content, impostor.Sign(content));

        var exception = Assert.Throws<TwineFormatException>(() =>
            TwinePulse.Verify(Document(forged.Json), chain)
        );

        Assert.Contains("does not verify", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void VerifyFollows_WhenSaltBreaksThePrecommitment_Throws()
    {
        using var builder = new TwineBuilder();
        var chainDocument = Document(builder.ChainJson);
        using var chain = TwineChain.Verify(chainDocument, builder.ChainCid, RngFamily);
        var first = builder.Pulses(1, Start)[0];
        var content = builder.PulseContent(
            1,
            first.Cid,
            new byte[64],
            new byte[64],
            Start.AddMinutes(1)
        );
        var second = builder.Seal(content);
        var previous = TwinePulse.Verify(Document(first.Json), chain);
        var pulse = TwinePulse.Verify(Document(second.Json), chain);

        var exception = Assert.Throws<TwineFormatException>(() => pulse.VerifyFollows(previous));

        Assert.Contains("precommitment", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void VerifyFollows_WhenRoundIsNotTheNext_Throws()
    {
        using var builder = new TwineBuilder();
        var chainDocument = Document(builder.ChainJson);
        using var chain = TwineChain.Verify(chainDocument, builder.ChainCid, RngFamily);
        var first = builder.Pulses(1, Start)[0];
        var content = builder.PulseContent(
            5,
            first.Cid,
            new byte[64],
            new byte[64],
            Start.AddMinutes(1)
        );
        var previous = TwinePulse.Verify(Document(first.Json), chain);
        var pulse = TwinePulse.Verify(Document(builder.Seal(content).Json), chain);

        var exception = Assert.Throws<TwineFormatException>(() => pulse.VerifyFollows(previous));

        Assert.Contains("does not precede", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(63, 66, "2026-11-25T00:00:00.000Z", "salt")]
    [InlineData(64, 65, "2026-11-25T00:00:00.000Z", "precommitment")]
    [InlineData(64, 66, "2026-11-25 00:00:00", "timestamp")]
    [InlineData(64, 66, "2026-11-25T00:00:00.000+01:00", "timestamp")]
    public void Verify_WithMalformedPayload_Throws(
        int saltLength,
        int preLength,
        string timestamp,
        string field
    )
    {
        using var builder = new TwineBuilder();
        var chainDocument = Document(builder.ChainJson);
        using var chain = TwineChain.Verify(chainDocument, builder.ChainCid, RngFamily);
        var content = builder.PulseContent(1, null, new byte[64], new byte[64], Start);
        var payload = Ipld.Map(
            ("pre", Ipld.Bytes([0x14, 0x40, .. new byte[preLength - 2]])),
            ("salt", Ipld.Bytes(new byte[saltLength])),
            ("timestamp", Ipld.Text(timestamp))
        );
        var malformed = builder.Seal(content.With("payload", payload));

        var exception = Assert.Throws<TwineFormatException>(() =>
            TwinePulse.Verify(Document(malformed.Json), chain)
        );

        Assert.Contains(field, exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("data")]
    [InlineData("document")]
    public void Parse_WithMemberTwineDoesNotDefine_Throws(string where)
    {
        // An extra member changes the CID, and so the randomness, without touching the signed content.
        using var builder = new TwineBuilder();
        var content = builder.PulseContent(1, null, new byte[64], new byte[64], Start);
        var data = Ipld.Map(("content", content), ("signature", Ipld.Text(builder.Sign(content))));
        if (where == "data")
        {
            data = data.With("nonce", Ipld.Integer(42));
        }

        var cid = Cid.ForBlock(DagCbor.Encode(data));
        var root = Ipld.Map(("cid", Ipld.Link(cid)), ("data", data));
        if (where == "document")
        {
            root = root.With("nonce", Ipld.Integer(42));
        }

        var exception = Assert.Throws<TwineFormatException>(() =>
            Document(DagJsonWriter.Write(root))
        );

        Assert.Contains(
            $"The {where} has the member 'nonce'",
            exception.Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Verify_WithSignatureSpelledNonCanonically_Throws()
    {
        // Flipping an unused trailing bit spells the same signature another way; the spelling is hashed into the
        // pulse's CID, so accepting it would let a server grind the randomness.
        using var builder = new TwineBuilder();
        var chainDocument = Document(builder.ChainJson);
        using var chain = TwineChain.Verify(chainDocument, builder.ChainCid, RngFamily);
        var content = builder.PulseContent(1, null, new byte[64], new byte[64], Start);
        var signature = builder.Sign(content);
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";
        var last = alphabet[alphabet.IndexOf(signature[^1], StringComparison.Ordinal) ^ 1];
        var respelled = builder.Seal(content, signature[..^1] + last);

        var exception = Assert.Throws<TwineFormatException>(() =>
            TwinePulse.Verify(Document(respelled.Json), chain)
        );

        Assert.Contains("signature is not", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_WithSignatureHoldingWhiteSpace_Throws()
    {
        using var builder = new TwineBuilder();
        var chainDocument = Document(builder.ChainJson);
        using var chain = TwineChain.Verify(chainDocument, builder.ChainCid, RngFamily);
        var content = builder.PulseContent(1, null, new byte[64], new byte[64], Start);
        var signature = builder.Sign(content);
        var respelled = builder.Seal(content, signature[..^4] + " " + signature[^4..]);

        var exception = Assert.Throws<TwineFormatException>(() =>
            TwinePulse.Verify(Document(respelled.Json), chain)
        );

        Assert.Contains("signature is not", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_WithSignatureLongerThanTheModulus_Throws()
    {
        using var builder = new TwineBuilder();
        var chainDocument = Document(builder.ChainJson);
        using var chain = TwineChain.Verify(chainDocument, builder.ChainCid, RngFamily);
        var content = builder.PulseContent(1, null, new byte[64], new byte[64], Start);
        var parts = builder.Sign(content).Split('.');
        var padded = System.Buffers.Text.Base64Url.EncodeToString([
            0,
            .. System.Buffers.Text.Base64Url.DecodeFromChars(parts[2]),
        ]);
        var forged = builder.Seal(content, $"{parts[0]}.{parts[1]}.{padded}");

        var exception = Assert.Throws<TwineFormatException>(() =>
            TwinePulse.Verify(Document(forged.Json), chain)
        );

        Assert.Contains("257 bytes, not 256", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_WithNegativeIndex_Throws()
    {
        using var builder = new TwineBuilder();
        var chainDocument = Document(builder.ChainJson);
        using var chain = TwineChain.Verify(chainDocument, builder.ChainCid, RngFamily);
        var content = builder.PulseContent(-1, null, new byte[64], new byte[64], Start);

        Assert.Throws<TwineFormatException>(() =>
            TwinePulse.Verify(Document(builder.Seal(content).Json), chain)
        );
    }

    #endregion
}
