// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NCode.Randomness.Nist.Pulses;

/// <summary>
/// A parsed, not yet verified, pulse of a NIST Randomness Beacon 2.0 chain, with the exact bytes its certificate's
/// key signed. Members the beacon does not sign, such as the address of each list value, are ignored.
/// </summary>
internal sealed class NistPulse
{
    /// <summary>
    /// The number of bytes, 64, in a SHA-512 hash and in each hash a pulse carries.
    /// </summary>
    private const int HashLength = 64;

    /// <summary>
    /// The most bytes a pulse's signature may have.
    /// </summary>
    private const int MaxSignatureLength = 1024;

    /// <summary>
    /// The format of a pulse's timestamp: UTC, to the millisecond.
    /// </summary>
    private const string TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

    /// <summary>
    /// The type of the first member of a pulse's list of values: the output of the previous pulse.
    /// </summary>
    private const string PreviousType = "previous";

    /// <summary>
    /// The only pulse version this beacon accepts.
    /// </summary>
    private const string SupportedVersion = "2.0";

    /// <summary>
    /// The only cipher suite this beacon accepts: 0, RSA PKCS #1 v1.5 with SHA-512.
    /// </summary>
    private const uint SupportedCipherSuite = 0;

    /// <summary>
    /// The bytes rented at first for a pulse's signed bytes, enough for a pulse without renting again.
    /// </summary>
    private const int SignedBytesCapacity = 1024;

    /// <summary>
    /// The name reported for the value of a list item.
    /// </summary>
    private const string ListValuePath = "listValues[].value";

    /// <summary>
    /// Gets a strict UTF-8 encoding, without a byte order mark, that throws on invalid bytes or characters instead of
    /// replacing them.
    /// </summary>
    private static UTF8Encoding Utf8 { get; } =
        new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// Gets the options that parse a document: nesting up to 16 levels deep and no duplicate property names.
    /// </summary>
    internal static JsonDocumentOptions JsonOptions { get; } =
        new() { MaxDepth = 16, AllowDuplicateProperties = false };

    /// <summary>
    /// Parses a pulse document from its UTF-8 JSON, as the HTTP API serves it, and rebuilds the bytes its signature
    /// covers.
    /// </summary>
    /// <param name="utf8Json">The UTF-8 JSON document the beacon served for one pulse.</param>
    /// <returns>The parsed pulse.</returns>
    /// <exception cref="NistFormatException">The document is not valid JSON, or not a well-formed pulse.</exception>
    public static NistPulse Parse(ReadOnlySpan<byte> utf8Json)
    {
        if (!Utf8JsonText.IsText(utf8Json))
        {
            throw new NistFormatException(
                "The NIST pulse is not valid JSON: its text is not well-formed Unicode."
            );
        }

        JsonElement json;
        try
        {
            json = JsonElement.Parse(utf8Json, JsonOptions);
        }
        catch (JsonException exception)
        {
            throw new NistFormatException(
                $"The NIST pulse is not valid JSON: {exception.Message}",
                exception
            );
        }

        return Parse(json);
    }

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
        using var signed = new PooledBufferWriter(SignedBytesCapacity);

        var uri = ReadString(pulse, "uri");
        WriteString(signed, uri);
        var version = ReadString(pulse, "version");
        WriteString(signed, version);
        var cipherSuite = ReadUInt32(pulse, "cipherSuite");
        WriteUInt32(signed, cipherSuite);
        var period = ReadUInt32(pulse, "period");
        WriteUInt32(signed, period);
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
        var listValues = ReadListValues(pulse);
        var previousText = listValues[0];
        var previous = new byte[HashLength];
        DecodeHash(previousText, ListValuePath, previous);
        WriteBytes(signed, previous);
        for (var i = 1; i < listValues.Length; ++i)
        {
            WriteHash(signed, listValues[i], ListValuePath);
        }

        var precommitment = ReadHash(pulse, "precommitmentValue");
        WriteBytes(signed, precommitment);
        var statusCode = ReadUInt32(pulse, "statusCode");
        WriteUInt32(signed, statusCode);

        var signature = ReadSignature(pulse);
        var output = ReadHash(pulse, "outputValue");
        var signedBytes = signed.WrittenSpan.ToArray();
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
            signedBytes
        );
    }

    /// <summary>
    /// Appends the <c>external</c> member to the signed bytes when the pulse has one, in the layout the beacon signs.
    /// </summary>
    /// <param name="signed">The signed bytes to append to.</param>
    /// <param name="pulse">The <c>pulse</c> object.</param>
    /// <exception cref="NistFormatException">The member is not an object or one of its members is malformed.
    /// </exception>
    private static void WriteExternal(PooledBufferWriter signed, JsonElement pulse)
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

        var sourceId = ReadString(external, "sourceId", "external.sourceId");
        WriteHash(signed, sourceId, "external.sourceId");
        var statusCode = ReadUInt32(external, "statusCode", "external.statusCode");
        WriteUInt32(signed, statusCode);
        var value = ReadString(external, "value", "external.value");
        WriteHash(signed, value, "external.value");
    }

    /// <summary>
    /// Reads the hexadecimal text of each value of the <c>listValues</c> member, in order; the first must be of type
    /// <c>previous</c>, the previous pulse's output.
    /// </summary>
    /// <param name="pulse">The <c>pulse</c> object.</param>
    /// <returns>The hexadecimal texts, the first of type <c>previous</c>.</returns>
    /// <exception cref="NistFormatException">
    /// The member is not a non-empty array of values, or its first value is not of type <c>previous</c>.
    /// </exception>
    private static ImmutableArray<string> ReadListValues(JsonElement pulse)
    {
        var list = ReadMember(pulse, "listValues");
        if (list.ValueKind != JsonValueKind.Array || list.GetArrayLength() == 0)
        {
            throw new NistFormatException(
                "The NIST pulse member 'listValues' is not a non-empty array."
            );
        }

        var count = list.GetArrayLength();
        var values = ImmutableArray.CreateBuilder<string>(count);
        foreach (var item in list.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                throw new NistFormatException(
                    "The NIST pulse member 'listValues' holds an item that is not an object."
                );
            }

            var type = ReadString(item, "type", "listValues[].type");
            var value = ReadString(item, "value", ListValuePath);
            if (values.Count == 0 && !string.Equals(type, PreviousType, StringComparison.Ordinal))
            {
                throw new NistFormatException(
                    $"The first NIST pulse member in 'listValues' has type '{type}', not '{PreviousType}'."
                );
            }

            values.Add(value);
        }

        return values.MoveToImmutable();
    }

    /// <summary>
    /// Parses the <c>timeStamp</c> text, a UTC time with milliseconds.
    /// </summary>
    /// <param name="text">The raw text of the member.</param>
    /// <returns>The time, in UTC.</returns>
    /// <exception cref="NistFormatException">The text is not a UTC time with milliseconds.</exception>
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

    /// <summary>
    /// Reads a member that must be a JSON object.
    /// </summary>
    /// <param name="parent">The object that holds the member.</param>
    /// <param name="name">The name of the member.</param>
    /// <returns>The member.</returns>
    /// <exception cref="NistFormatException">The member is missing or not an object.</exception>
    private static JsonElement ReadObject(JsonElement parent, string name)
    {
        var member = ReadMember(parent, name);
        if (member.ValueKind != JsonValueKind.Object)
        {
            throw new NistFormatException($"The NIST member '{name}' is not an object.");
        }

        return member;
    }

    /// <summary>
    /// Reads a member of any type.
    /// </summary>
    /// <param name="parent">The object that holds the member.</param>
    /// <param name="name">The name of the member.</param>
    /// <param name="path">The name to report for a nested member, or <see langword="null"/> to report
    /// <paramref name="name"/>.</param>
    /// <returns>The member.</returns>
    /// <exception cref="NistFormatException">The member is missing.</exception>
    private static JsonElement ReadMember(JsonElement parent, string name, string? path = null)
    {
        if (!parent.TryGetProperty(name, out var member))
        {
            throw new NistFormatException($"The NIST member '{path ?? name}' is missing.");
        }

        return member;
    }

    /// <summary>
    /// Reads a member that must be a JSON string.
    /// </summary>
    /// <param name="parent">The object that holds the member.</param>
    /// <param name="name">The name of the member.</param>
    /// <param name="path">The name to report for a nested member, or <see langword="null"/> to report
    /// <paramref name="name"/>.</param>
    /// <returns>The raw text of the member.</returns>
    /// <exception cref="NistFormatException">The member is missing or not a string.</exception>
    private static string ReadString(JsonElement parent, string name, string? path = null)
    {
        var member = ReadMember(parent, name, path);
        if (member.ValueKind != JsonValueKind.String)
        {
            throw new NistFormatException($"The NIST member '{path ?? name}' is not a string.");
        }

        return member.GetString() ?? string.Empty;
    }

    /// <summary>
    /// Reads a member that must be an unsigned 32-bit integer.
    /// </summary>
    /// <param name="parent">The object that holds the member.</param>
    /// <param name="name">The name of the member.</param>
    /// <param name="path">The name to report for a nested member, or <see langword="null"/> to report
    /// <paramref name="name"/>.</param>
    /// <returns>The value of the member.</returns>
    /// <exception cref="NistFormatException">The member is missing or not an unsigned 32-bit integer.</exception>
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

    /// <summary>
    /// Reads a chain or pulse index, an integer of at least 1.
    /// </summary>
    /// <param name="parent">The object that holds the member.</param>
    /// <param name="name">The name of the member.</param>
    /// <returns>The value of the member.</returns>
    /// <exception cref="NistFormatException">The member is missing or not an integer of at least 1.</exception>
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

    /// <summary>
    /// Reads a member that must be 64 bytes of hexadecimal.
    /// </summary>
    /// <param name="parent">The object that holds the member.</param>
    /// <param name="name">The name of the member.</param>
    /// <param name="path">The name to report for a nested member, or <see langword="null"/> to report
    /// <paramref name="name"/>.</param>
    /// <returns>The decoded 64 bytes.</returns>
    /// <exception cref="NistFormatException">The member is missing, not hexadecimal or not 64 bytes.</exception>
    private static byte[] ReadHash(JsonElement parent, string name, string? path = null)
    {
        var text = ReadString(parent, name, path);
        var bytes = new byte[HashLength];
        DecodeHash(text, path ?? name, bytes);
        return bytes;
    }

    /// <summary>
    /// Decodes a 64-byte hash from its hexadecimal text into <paramref name="destination"/>.
    /// </summary>
    /// <param name="text">The hexadecimal text of the member.</param>
    /// <param name="path">The name of the member to report.</param>
    /// <param name="destination">The 64 bytes to decode into.</param>
    /// <exception cref="NistFormatException">The text is not hexadecimal or not 64 bytes.</exception>
    private static void DecodeHash(string text, string path, Span<byte> destination)
    {
        var status = Convert.FromHexString(text, destination, out _, out var written);
        if (status is OperationStatus.InvalidData or OperationStatus.NeedMoreData)
        {
            throw new NistFormatException($"The NIST member '{path}' is not hexadecimal.");
        }

        if (status != OperationStatus.Done || written != HashLength)
        {
            throw new NistFormatException($"The NIST member '{path}' is not {HashLength} bytes.");
        }
    }

    /// <summary>
    /// Reads the <c>signatureValue</c> member, hexadecimal of 1 to the maximum signature length in bytes.
    /// </summary>
    /// <param name="pulse">The <c>pulse</c> object.</param>
    /// <returns>The decoded signature.</returns>
    /// <exception cref="NistFormatException">
    /// The member is missing, not hexadecimal or of a length outside 1 to the maximum.
    /// </exception>
    private static byte[] ReadSignature(JsonElement pulse)
    {
        var bytes = ReadHexBytes(pulse, "signatureValue", null);
        if (bytes.Length < 1 || bytes.Length > MaxSignatureLength)
        {
            throw new NistFormatException(
                $"The NIST member 'signatureValue' has {bytes.Length} bytes, not 1 to {MaxSignatureLength} bytes."
            );
        }

        return bytes;
    }

    /// <summary>
    /// Reads a member that must be a string of hexadecimal and decodes it.
    /// </summary>
    /// <param name="parent">The object that holds the member.</param>
    /// <param name="name">The name of the member.</param>
    /// <param name="path">The name to report for a nested member, or <see langword="null"/> to report
    /// <paramref name="name"/>.</param>
    /// <returns>The decoded bytes, of any length.</returns>
    /// <exception cref="NistFormatException">The member is missing, not a string or not hexadecimal.</exception>
    private static byte[] ReadHexBytes(JsonElement parent, string name, string? path)
    {
        var text = ReadString(parent, name, path);
        try
        {
            return Convert.FromHexString(text);
        }
        catch (FormatException exception)
        {
            throw new NistFormatException(
                $"The NIST member '{path ?? name}' is not hexadecimal.",
                exception
            );
        }
    }

    /// <summary>
    /// Appends a 4-byte big-endian unsigned integer to the signed bytes.
    /// </summary>
    /// <param name="signed">The signed bytes to append to.</param>
    /// <param name="value">The value to append.</param>
    private static void WriteUInt32(PooledBufferWriter signed, uint value)
    {
        var span = signed.GetSpan(sizeof(uint));
        BinaryPrimitives.WriteUInt32BigEndian(span, value);
        signed.Advance(sizeof(uint));
    }

    /// <summary>
    /// Appends an 8-byte big-endian integer to the signed bytes.
    /// </summary>
    /// <param name="signed">The signed bytes to append to.</param>
    /// <param name="value">The value to append.</param>
    private static void WriteInt64(PooledBufferWriter signed, long value)
    {
        var span = signed.GetSpan(sizeof(long));
        BinaryPrimitives.WriteInt64BigEndian(span, value);
        signed.Advance(sizeof(long));
    }

    /// <summary>
    /// Appends bytes to the signed bytes, prefixed by their length as a 4-byte big-endian unsigned integer.
    /// </summary>
    /// <param name="signed">The signed bytes to append to.</param>
    /// <param name="value">The bytes to append.</param>
    private static void WriteBytes(PooledBufferWriter signed, ReadOnlySpan<byte> value)
    {
        WriteUInt32(signed, (uint)value.Length);
        signed.Write(value);
    }

    /// <summary>
    /// Appends a 64-byte hash to the signed bytes, prefixed by its length as a 4-byte big-endian unsigned integer,
    /// decoding its hexadecimal text straight into them.
    /// </summary>
    /// <param name="signed">The signed bytes to append to.</param>
    /// <param name="text">The hexadecimal text of the member.</param>
    /// <param name="path">The name of the member to report.</param>
    /// <exception cref="NistFormatException">The text is not hexadecimal or not 64 bytes.</exception>
    private static void WriteHash(PooledBufferWriter signed, string text, string path)
    {
        WriteUInt32(signed, HashLength);
        var span = signed.GetSpan(HashLength);
        var destination = span[..HashLength];
        DecodeHash(text, path, destination);
        signed.Advance(HashLength);
    }

    /// <summary>
    /// Appends text to the signed bytes as UTF-8, prefixed by its byte length as a 4-byte big-endian unsigned integer.
    /// </summary>
    /// <param name="signed">The signed bytes to append to.</param>
    /// <param name="value">The text to append.</param>
    /// <exception cref="NistFormatException">The text is not valid UTF-8.</exception>
    private static void WriteString(PooledBufferWriter signed, string value)
    {
        int length;
        try
        {
            length = Utf8.GetByteCount(value);
        }
        catch (EncoderFallbackException exception)
        {
            throw new NistFormatException(
                "A NIST pulse text member is not valid UTF-8.",
                exception
            );
        }

        WriteUInt32(signed, (uint)length);
        var span = signed.GetSpan(length);
        var written = Utf8.GetBytes(value, span);
        signed.Advance(written);
    }

    /// <summary>
    /// Initializes a pulse from parsed parts; only <see cref="Parse(JsonElement)"/> builds a pulse.
    /// </summary>
    /// <param name="version">The pulse format version.</param>
    /// <param name="cipherSuite">The cipher suite.</param>
    /// <param name="certificateId">The identifier of the signing certificate.</param>
    /// <param name="chainIndex">The index of the chain.</param>
    /// <param name="pulseIndex">The index of the pulse within its chain.</param>
    /// <param name="timestamp">The time the pulse was issued, in UTC.</param>
    /// <param name="localRandomValue">The 64-byte local random value.</param>
    /// <param name="previous">The previous pulse's output value, as this pulse lists it.</param>
    /// <param name="precommitment">The precommitment of the next pulse's local random value.</param>
    /// <param name="signature">The signature.</param>
    /// <param name="output">The claimed 64-byte output value.</param>
    /// <param name="signedBytes">The bytes the signature covers.</param>
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

        using var outputHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA512);
        outputHash.AppendData(SignedBytes);
        outputHash.AppendData(Signature);
        Span<byte> hash = stackalloc byte[HashLength];
        outputHash.GetHashAndReset(hash);
        if (!CryptographicOperations.FixedTimeEquals(hash, Output))
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

        Span<byte> localRandomHash = stackalloc byte[HashLength];
        SHA512.HashData(LocalRandomValue, localRandomHash);
        if (!CryptographicOperations.FixedTimeEquals(localRandomHash, predecessor.Precommitment))
        {
            throw new NistFormatException(
                $"The local random value of pulse {PulseIndex} does not match the precommitment of pulse {predecessor.PulseIndex}."
            );
        }
    }
}
