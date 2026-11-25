// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NCode.Randomness.Nist.Pulses;

/// <summary>
/// A parsed, not yet verified, pulse of a NIST Randomness Beacon 2.0 chain, with the exact bytes its certificate's
/// key signed. Members the beacon does not sign, such as the pulse's own address, are ignored.
/// </summary>
internal sealed class NistPulse
{
    private const int HashLength = 64;
    private const int MaxSignatureLength = 1024;
    private const string TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";
    private const string PreviousType = "previous";
    private const string SupportedVersion = "2.0";
    private const uint SupportedCipherSuite = 0;

    private static UTF8Encoding Utf8 { get; } =
        new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private NistPulse(
        string version,
        uint cipherSuite,
        byte[] certificateId,
        long chainIndex,
        long pulseIndex,
        DateTimeOffset timestamp,
        byte[] localRandomValue,
        byte[] previous,
        byte[] precommitment,
        byte[] signature,
        byte[] output,
        byte[] signedBytes
    )
    {
        Version = version;
        CipherSuite = cipherSuite;
        CertificateId = certificateId;
        ChainIndex = chainIndex;
        PulseIndex = pulseIndex;
        Timestamp = timestamp;
        LocalRandomValue = localRandomValue;
        Previous = previous;
        Precommitment = precommitment;
        Signature = signature;
        Output = output;
        SignedBytes = signedBytes;
    }

    /// <summary>
    /// Gets the pulse format version, such as <c>2.0</c>.
    /// </summary>
    public string Version { get; }

    /// <summary>
    /// Gets the cipher suite that signed the pulse and hashed its output.
    /// </summary>
    public uint CipherSuite { get; }

    /// <summary>
    /// Gets the identifier of the certificate that signed the pulse: the SHA-512 of its DER encoding.
    /// </summary>
    public byte[] CertificateId { get; }

    /// <summary>
    /// Gets the index of the chain the pulse belongs to.
    /// </summary>
    public long ChainIndex { get; }

    /// <summary>
    /// Gets the index of the pulse within its chain.
    /// </summary>
    public long PulseIndex { get; }

    /// <summary>
    /// Gets the time the pulse was issued, in UTC.
    /// </summary>
    public DateTimeOffset Timestamp { get; }

    /// <summary>
    /// Gets the 64-byte random value the beacon committed to in the previous pulse.
    /// </summary>
    public byte[] LocalRandomValue { get; }

    /// <summary>
    /// Gets the output value of the previous pulse, as this pulse lists it.
    /// </summary>
    public byte[] Previous { get; }

    /// <summary>
    /// Gets the hash the beacon commits the next pulse's local random value to.
    /// </summary>
    public byte[] Precommitment { get; }

    /// <summary>
    /// Gets the signature over <see cref="SignedBytes"/>.
    /// </summary>
    public byte[] Signature { get; }

    /// <summary>
    /// Gets the 64-byte output value the pulse claims: the SHA-512 of the signed bytes followed by the signature.
    /// </summary>
    public byte[] Output { get; }

    /// <summary>
    /// Gets the bytes the signature covers, rebuilt from the signed members in the order the beacon signs them.
    /// </summary>
    public byte[] SignedBytes { get; }

    /// <summary>
    /// Parses a pulse document and rebuilds the bytes its signature covers.
    /// </summary>
    /// <param name="document">The JSON document the beacon served for one pulse.</param>
    /// <returns>The parsed pulse.</returns>
    /// <exception cref="NistFormatException">The document is not a well-formed pulse.</exception>
    public static NistPulse Parse(JsonElement document)
    {
        if (document.ValueKind != JsonValueKind.Object)
        {
            throw new NistFormatException("The NIST document is not a JSON object.");
        }

        var pulse = ReadObject(document, "pulse");
        var signed = new ArrayBufferWriter<byte>(1024);

        WriteString(signed, ReadString(pulse, "uri"));
        var version = ReadString(pulse, "version");
        WriteString(signed, version);
        var cipherSuite = ReadUInt32(pulse, "cipherSuite");
        WriteUInt32(signed, cipherSuite);
        WriteUInt32(signed, ReadUInt32(pulse, "period"));
        var certificateId = ReadHash(pulse, "certificateId");
        WriteBytes(signed, certificateId);
        var chainIndex = ReadIndex(pulse, "chainIndex");
        WriteInt64(signed, chainIndex);
        var pulseIndex = ReadIndex(pulse, "pulseIndex");
        WriteInt64(signed, pulseIndex);
        var timestampText = ReadString(pulse, "timeStamp");
        var timestamp = ParseTimestamp(timestampText);
        WriteString(signed, timestampText);
        var localRandomValue = ReadHash(pulse, "localRandomValue");
        WriteBytes(signed, localRandomValue);
        WriteExternal(signed, pulse);
        var previous = WriteListValues(signed, pulse);
        var precommitment = ReadHash(pulse, "precommitmentValue");
        WriteBytes(signed, precommitment);
        WriteUInt32(signed, ReadUInt32(pulse, "statusCode"));

        var signature = ReadHex(pulse, "signatureValue", minimumLength: 1, MaxSignatureLength);
        var output = ReadHash(pulse, "outputValue");
        return new NistPulse(
            version,
            cipherSuite,
            certificateId,
            chainIndex,
            pulseIndex,
            timestamp,
            localRandomValue,
            previous,
            precommitment,
            signature,
            output,
            signed.WrittenSpan.ToArray()
        );
    }

    /// <summary>
    /// Checks that the pulse is a version 2.0, cipher suite 0 pulse that <paramref name="certificate"/> signed while
    /// it was valid: the signature verifies over <see cref="SignedBytes"/> and the output is the SHA-512 of the signed
    /// bytes followed by the signature.
    /// </summary>
    /// <param name="certificate">The pinned certificate the pulse names.</param>
    /// <exception cref="NistFormatException">The pulse does not verify.</exception>
    public void Verify(NistCertificate certificate)
    {
        ArgumentNullException.ThrowIfNull(certificate);
        if (!string.Equals(Version, SupportedVersion, StringComparison.Ordinal))
        {
            throw new NistFormatException(
                $"The NIST pulse version '{Version}' is not supported; only '{SupportedVersion}' is."
            );
        }

        if (CipherSuite != SupportedCipherSuite)
        {
            throw new NistFormatException(
                $"Cipher suite {CipherSuite} is not supported; only {SupportedCipherSuite} (RSA PKCS #1 v1.5 with SHA-512) is."
            );
        }

        if (!CryptographicOperations.FixedTimeEquals(CertificateId, certificate.Id))
        {
            throw new NistFormatException(
                $"Pulse {PulseIndex} names another certificate than the one pinned for it."
            );
        }

        if (Timestamp < certificate.NotBefore || Timestamp > certificate.NotAfter)
        {
            throw new NistFormatException(
                $"Pulse {PulseIndex} is dated outside the validity of its certificate."
            );
        }

        var expectedLength = certificate.Key.KeySize / 8;
        if (Signature.Length != expectedLength)
        {
            throw new NistFormatException(
                $"The signature has {Signature.Length} bytes, not {expectedLength}."
            );
        }

        bool verified;
        try
        {
            verified = certificate.Key.VerifyData(
                SignedBytes,
                Signature,
                HashAlgorithmName.SHA512,
                RSASignaturePadding.Pkcs1
            );
        }
        catch (CryptographicException)
        {
            verified = false;
        }

        if (!verified)
        {
            throw new NistFormatException($"The signature of pulse {PulseIndex} does not verify.");
        }

        var hashed = new byte[SignedBytes.Length + Signature.Length];
        SignedBytes.CopyTo(hashed, 0);
        Signature.CopyTo(hashed, SignedBytes.Length);
        if (!CryptographicOperations.FixedTimeEquals(SHA512.HashData(hashed), Output))
        {
            throw new NistFormatException(
                $"The output value of pulse {PulseIndex} is not the hash of its signed bytes and signature."
            );
        }
    }

    /// <summary>
    /// Checks that this pulse directly follows <paramref name="predecessor"/> on the same chain, lists its output
    /// as the previous value, and that its local random value is the one <paramref name="predecessor"/> committed
    /// to: SHA-512(local random value) equals the predecessor's precommitment. The beacon therefore fixed this
    /// pulse's random value a pulse before it published the pulse.
    /// </summary>
    /// <param name="predecessor">The verified pulse before this one.</param>
    /// <exception cref="NistFormatException">This pulse does not follow <paramref name="predecessor"/>.</exception>
    public void VerifyFollows(NistPulse predecessor)
    {
        ArgumentNullException.ThrowIfNull(predecessor);
        if (predecessor.ChainIndex != ChainIndex || predecessor.PulseIndex != PulseIndex - 1)
        {
            throw new NistFormatException(
                $"Pulse {predecessor.PulseIndex} of chain {predecessor.ChainIndex} does not precede pulse {PulseIndex} of chain {ChainIndex}."
            );
        }

        if (predecessor.Timestamp >= Timestamp)
        {
            throw new NistFormatException(
                $"Pulse {predecessor.PulseIndex} is not earlier than pulse {PulseIndex}."
            );
        }

        if (!CryptographicOperations.FixedTimeEquals(Previous, predecessor.Output))
        {
            throw new NistFormatException(
                $"Pulse {PulseIndex} does not chain to the output value of pulse {predecessor.PulseIndex}."
            );
        }

        if (
            !CryptographicOperations.FixedTimeEquals(
                SHA512.HashData(LocalRandomValue),
                predecessor.Precommitment
            )
        )
        {
            throw new NistFormatException(
                $"The local random value of pulse {PulseIndex} does not match the precommitment of pulse {predecessor.PulseIndex}."
            );
        }
    }

    private static void WriteExternal(ArrayBufferWriter<byte> signed, JsonElement pulse)
    {
        if (
            !pulse.TryGetProperty("external", out var external)
            || external.ValueKind == JsonValueKind.Null
        )
        {
            return;
        }

        if (external.ValueKind != JsonValueKind.Object)
        {
            throw new NistFormatException("The NIST pulse member 'external' is not an object.");
        }

        WriteBytes(signed, ReadHash(external, "sourceId", "external.sourceId"));
        WriteUInt32(signed, ReadUInt32(external, "statusCode", "external.statusCode"));
        WriteBytes(signed, ReadHash(external, "value", "external.value"));
    }

    private static byte[] WriteListValues(ArrayBufferWriter<byte> signed, JsonElement pulse)
    {
        var list = ReadMember(pulse, "listValues");
        if (list.ValueKind != JsonValueKind.Array || list.GetArrayLength() == 0)
        {
            throw new NistFormatException(
                "The NIST pulse member 'listValues' is not a non-empty array."
            );
        }

        byte[]? previous = null;
        foreach (var item in list.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                throw new NistFormatException(
                    "The NIST pulse member 'listValues' holds an item that is not an object."
                );
            }

            var type = ReadString(item, "type", "listValues[].type");
            var value = ReadHash(item, "value", "listValues[].value");
            if (previous is null)
            {
                if (!string.Equals(type, PreviousType, StringComparison.Ordinal))
                {
                    throw new NistFormatException(
                        $"The first NIST pulse member in 'listValues' has type '{type}', not '{PreviousType}'."
                    );
                }

                previous = value;
            }

            WriteBytes(signed, value);
        }

        return previous ?? throw new NistFormatException("The NIST pulse has no previous value.");
    }

    private static DateTimeOffset ParseTimestamp(string text)
    {
        if (
            !DateTimeOffset.TryParseExact(
                text,
                TimestampFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var timestamp
            )
        )
        {
            throw new NistFormatException(
                $"The NIST pulse member 'timeStamp' ('{text}') is not a UTC time with milliseconds."
            );
        }

        return timestamp;
    }

    private static JsonElement ReadObject(JsonElement parent, string name)
    {
        var member = ReadMember(parent, name);
        if (member.ValueKind != JsonValueKind.Object)
        {
            throw new NistFormatException($"The NIST member '{name}' is not an object.");
        }

        return member;
    }

    private static JsonElement ReadMember(JsonElement parent, string name, string? path = null)
    {
        if (!parent.TryGetProperty(name, out var member))
        {
            throw new NistFormatException($"The NIST member '{path ?? name}' is missing.");
        }

        return member;
    }

    private static string ReadString(JsonElement parent, string name, string? path = null)
    {
        var member = ReadMember(parent, name, path);
        if (member.ValueKind != JsonValueKind.String)
        {
            throw new NistFormatException($"The NIST member '{path ?? name}' is not a string.");
        }

        return member.GetString() ?? string.Empty;
    }

    private static uint ReadUInt32(JsonElement parent, string name, string? path = null)
    {
        var member = ReadMember(parent, name, path);
        if (member.ValueKind != JsonValueKind.Number || !member.TryGetUInt32(out var value))
        {
            throw new NistFormatException(
                $"The NIST member '{path ?? name}' is not an unsigned 32-bit integer."
            );
        }

        return value;
    }

    private static long ReadIndex(JsonElement parent, string name)
    {
        var member = ReadMember(parent, name);
        if (
            member.ValueKind != JsonValueKind.Number
            || !member.TryGetInt64(out var value)
            || value < 1
        )
        {
            throw new NistFormatException(
                $"The NIST member '{name}' is not an integer of at least 1."
            );
        }

        return value;
    }

    private static byte[] ReadHash(JsonElement parent, string name, string? path = null) =>
        ReadHex(parent, name, HashLength, HashLength, path);

    private static byte[] ReadHex(
        JsonElement parent,
        string name,
        int minimumLength,
        int maximumLength,
        string? path = null
    )
    {
        var text = ReadString(parent, name, path);
        byte[] bytes;
        try
        {
            bytes = Convert.FromHexString(text);
        }
        catch (FormatException)
        {
            throw new NistFormatException($"The NIST member '{path ?? name}' is not hexadecimal.");
        }

        if (bytes.Length < minimumLength || bytes.Length > maximumLength)
        {
            var expected =
                minimumLength == maximumLength
                    ? $"{minimumLength} bytes"
                    : $"{minimumLength} to {maximumLength} bytes";
            throw new NistFormatException(
                $"The NIST member '{path ?? name}' has {bytes.Length} bytes, not {expected}."
            );
        }

        return bytes;
    }

    private static void WriteUInt32(ArrayBufferWriter<byte> signed, uint value)
    {
        BinaryPrimitives.WriteUInt32BigEndian(signed.GetSpan(sizeof(uint)), value);
        signed.Advance(sizeof(uint));
    }

    private static void WriteInt64(ArrayBufferWriter<byte> signed, long value)
    {
        BinaryPrimitives.WriteInt64BigEndian(signed.GetSpan(sizeof(long)), value);
        signed.Advance(sizeof(long));
    }

    private static void WriteBytes(ArrayBufferWriter<byte> signed, ReadOnlySpan<byte> value)
    {
        WriteUInt32(signed, (uint)value.Length);
        signed.Write(value);
    }

    private static void WriteString(ArrayBufferWriter<byte> signed, string value)
    {
        byte[] bytes;
        try
        {
            bytes = Utf8.GetBytes(value);
        }
        catch (EncoderFallbackException)
        {
            throw new NistFormatException("A NIST pulse text member is not valid UTF-8.");
        }

        WriteBytes(signed, bytes);
    }
}
