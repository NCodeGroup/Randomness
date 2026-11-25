// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using NCode.Randomness.Nist.Pulses;

namespace NCode.Randomness.Nist.Tests.TestNist;

/// <summary>
/// Builds the JSON document of a test pulse, signed by a <see cref="NistTestSigner"/> the way NIST signs its pulses.
/// </summary>
internal sealed record NistPulseBuilder
{
    private const int HashBytes = 64;

    private static DateTimeOffset Epoch { get; } = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);

    public static NistPulseBuilder For(long index)
    {
        var next = LocalRandom(index + 1);
        var precommitment = SHA512.HashData(next);
        return new NistPulseBuilder
        {
            PulseIndex = index,
            LocalRandomValue = LocalRandom(index),
            Precommitment = precommitment,
            Timestamp = Epoch.AddMinutes(index),
        };
    }

    public static byte[] LocalRandom(long index)
    {
        var bytes = new byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(bytes, index);
        return SHA512.HashData(bytes);
    }

    public static byte[] OutputOf(string json)
    {
        var node = JsonNode.Parse(json);
        return Convert.FromHexString((string?)node?["pulse"]?["outputValue"] ?? string.Empty);
    }

    private static JsonElement Parse(JsonNode node)
    {
        var json = node.ToJsonString();
        return JsonElement.Parse(
            json,
            new JsonDocumentOptions { MaxDepth = 16, AllowDuplicateProperties = false }
        );
    }

    public long ChainIndex { get; init; } = 2;

    public long PulseIndex { get; init; } = 1;

    public string Version { get; init; } = "2.0";

    public uint CipherSuite { get; init; }

    public DateTimeOffset Timestamp { get; init; } = Epoch;

    public byte[] LocalRandomValue { get; init; } = new byte[HashBytes];

    public byte[] Previous { get; init; } = new byte[HashBytes];

    public byte[] Precommitment { get; init; } = new byte[HashBytes];

    public bool IncludeExternal { get; init; } = true;

    public Action<JsonObject>? AfterSigning { get; init; }

    public string Build(NistTestSigner signer)
    {
        ArgumentNullException.ThrowIfNull(signer);
        var zeros = Convert.ToHexString(new byte[HashBytes]);
        var pulse = new JsonObject
        {
            ["uri"] = $"https://beacon.example/beacon/2.0/chain/{ChainIndex}/pulse/{PulseIndex}",
            ["version"] = Version,
            ["cipherSuite"] = CipherSuite,
            ["period"] = 60000,
            ["certificateId"] = Convert.ToHexString(signer.Id),
            ["chainIndex"] = ChainIndex,
            ["pulseIndex"] = PulseIndex,
            ["timeStamp"] = Timestamp.UtcDateTime.ToString(
                "yyyy-MM-dd'T'HH:mm:ss.fff'Z'",
                CultureInfo.InvariantCulture
            ),
            ["localRandomValue"] = Convert.ToHexString(LocalRandomValue),
        };

        if (IncludeExternal)
        {
            pulse["external"] = new JsonObject
            {
                ["sourceId"] = zeros,
                ["statusCode"] = 0,
                ["value"] = zeros,
            };
        }

        pulse["listValues"] = new JsonArray(
            new JsonObject { ["type"] = "previous", ["value"] = Convert.ToHexString(Previous) },
            new JsonObject { ["type"] = "hour", ["value"] = zeros }
        );
        pulse["precommitmentValue"] = Convert.ToHexString(Precommitment);
        pulse["statusCode"] = 0;
        pulse["signatureValue"] = "00";
        pulse["outputValue"] = zeros;

        var clone = pulse.DeepClone();
        var unsignedElement = Parse(new JsonObject { ["pulse"] = clone });
        var unsigned = NistPulse.Parse(unsignedElement);
        var signature = signer.Sign(unsigned.SignedBytes);
        var hashed = new byte[unsigned.SignedBytes.Length + signature.Length];
        unsigned.SignedBytes.CopyTo(hashed, 0);
        signature.CopyTo(hashed, unsigned.SignedBytes.Length);
        pulse["signatureValue"] = Convert.ToHexString(signature);
        var output = SHA512.HashData(hashed);
        pulse["outputValue"] = Convert.ToHexString(output);

        AfterSigning?.Invoke(pulse);
        return new JsonObject { ["pulse"] = pulse }.ToJsonString();
    }
}
