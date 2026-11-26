// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NCode.Randomness.Curby.Twine;

/// <summary>
/// Verifies a JWS in compact form signed with RS256 (RSASSA-PKCS1-v1_5 with SHA-256), the deterministic algorithm
/// CURBy requires so a beacon cannot grind its own randomness. The protected header may hold only <c>alg</c>, which
/// must be <c>RS256</c>, and <c>typ</c> or <c>kid</c>: any header that would bring its own key, a critical extension
/// or an unencoded payload is refused.
/// </summary>
internal static class JwsRs256
{
    private const string Algorithm = "RS256";

    public static void Verify(string compact, RSA key, ReadOnlySpan<byte> expectedPayload)
    {
        ArgumentNullException.ThrowIfNull(compact);
        ArgumentNullException.ThrowIfNull(key);
        Span<Range> parts = stackalloc Range[4];
        if (compact.AsSpan().Split(parts, '.') != 3)
        {
            throw new TwineFormatException("A signature is a JWS in compact form: three parts.");
        }

        var header = compact.AsSpan(parts[0]);
        var payload = DecodePart(compact.AsSpan(parts[1]), "payload");
        var signature = DecodePart(compact.AsSpan(parts[2]), "signature");
        // PKCS #1 v1.5 signatures are deterministic, and with canonical encoding and the exact modulus length, each
        // signature has one spelling, so the signature string, which the pulse's CID hashes, is fixed by the content.
        if (signature.Length != key.KeySize / 8)
        {
            throw new TwineFormatException(
                $"The signature has {signature.Length} bytes, not {key.KeySize / 8}."
            );
        }

        ValidateHeader(DecodePart(header, "header"));
        if (!CryptographicOperations.FixedTimeEquals(payload, expectedPayload))
        {
            throw new TwineFormatException("The signature signs other content.");
        }

        var signingInput = Encoding.ASCII.GetBytes(
            compact[..(parts[1].End.GetOffset(compact.Length))]
        );
        if (
            !key.VerifyData(
                signingInput,
                signature,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1
            )
        )
        {
            throw new TwineFormatException("The RS256 signature does not verify.");
        }
    }

    /// <summary>
    /// Imports an RSA public key from a JWK of at least 2048 bits whose <c>alg</c>, when present, is RS256.
    /// </summary>
    public static RSA ImportKey(IpldMap jwk)
    {
        ArgumentNullException.ThrowIfNull(jwk);
        if (!string.Equals(jwk.Get<IpldString>("kty").Value, "RSA", StringComparison.Ordinal))
        {
            throw new TwineFormatException("The chain key is not an RSA key.");
        }

        if (
            jwk.Entries.Any(entry => entry.Key == "alg")
            && !string.Equals(jwk.Get<IpldString>("alg").Value, Algorithm, StringComparison.Ordinal)
        )
        {
            throw new TwineFormatException("The chain key is not an RS256 key.");
        }

        var parameters = new RSAParameters
        {
            Modulus = DecodePart(jwk.Get<IpldString>("n").Value, "n"),
            Exponent = DecodePart(jwk.Get<IpldString>("e").Value, "e"),
        };
        var key = RSA.Create();
        try
        {
            key.ImportParameters(parameters);
        }
        catch (CryptographicException exception)
        {
            key.Dispose();
            throw new TwineFormatException(
                $"The chain key is not a valid RSA key: {exception.Message}",
                exception
            );
        }

        if (key.KeySize < 2048)
        {
            key.Dispose();
            throw new TwineFormatException(
                $"The chain key has {key.KeySize} bits; at least 2048 are required."
            );
        }

        return key;
    }

    /// <summary>
    /// Checks that the JWS header is a JSON object of well-formed Unicode text that names the RS256 algorithm once,
    /// and refuses any member other than a string <c>typ</c> or <c>kid</c>.
    /// </summary>
    /// <param name="header">The decoded JWS header.</param>
    /// <exception cref="TwineFormatException">
    /// The header is not JSON, its text is not well-formed Unicode, it is not a JSON object, it holds a member that
    /// is not allowed, or it does not name the RS256 algorithm.
    /// </exception>
    private static void ValidateHeader(byte[] header)
    {
        if (!Utf8JsonText.IsText(header))
        {
            throw new TwineFormatException(
                "The JWS header is not JSON: its text is not well-formed Unicode."
            );
        }

        JsonElement json;
        try
        {
            json = JsonElement.Parse(header);
        }
        catch (JsonException exception)
        {
            throw new TwineFormatException(
                $"The JWS header is not JSON: {exception.Message}",
                exception
            );
        }

        if (json.ValueKind != JsonValueKind.Object)
        {
            throw new TwineFormatException("The JWS header is not a JSON object.");
        }

        var algorithm = false;
        foreach (var member in json.EnumerateObject())
        {
            switch (member.Name)
            {
                case "alg"
                    when !algorithm
                        && member.Value.ValueKind == JsonValueKind.String
                        && member.Value.ValueEquals(Algorithm):
                    algorithm = true;
                    break;
                case "typ" or "kid" when member.Value.ValueKind == JsonValueKind.String:
                    break;
                default:
                    throw new TwineFormatException(
                        $"The JWS header member '{member.Name}' is not allowed."
                    );
            }
        }

        if (!algorithm)
        {
            throw new TwineFormatException("The JWS header must name the RS256 algorithm.");
        }
    }

    private static byte[] DecodePart(ReadOnlySpan<char> text, string part)
    {
        if (text.IsEmpty || text.Contains('='))
        {
            throw new TwineFormatException($"The JWS {part} is not unpadded base64url.");
        }

        byte[] bytes;
        try
        {
            bytes = Base64Url.DecodeFromChars(text);
        }
        catch (FormatException exception)
        {
            throw new TwineFormatException($"The JWS {part} is not base64url.", exception);
        }

        // The decoder skips white space and ignores nonzero unused bits; re-encoding refuses both.
        if (!text.SequenceEqual(Base64Url.EncodeToString(bytes)))
        {
            throw new TwineFormatException($"The JWS {part} is not canonical base64url.");
        }

        return bytes;
    }
}
