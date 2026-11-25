// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace NCode.Randomness.Nist.Pulses;

/// <summary>
/// A certificate NIST signs pulses with, pinned by its identifier: the SHA-512 of its DER encoding. The certificate
/// must hold an RSA key of at least 2048 bits. Its chain to a certificate authority is not validated; the pinned
/// identifier is the trust anchor.
/// </summary>
internal sealed class NistCertificate : IDisposable
{
    private const string PemLabel = "CERTIFICATE";
    private const int MinimumKeyBits = 2048;

    private static UTF8Encoding Utf8 { get; } =
        new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// Loads an X.509 certificate from its DER encoding.
    /// </summary>
    /// <param name="der">The DER encoding of the certificate.</param>
    /// <returns>The certificate, which the caller disposes.</returns>
    /// <exception cref="NistFormatException">The bytes are not a valid X.509 certificate.</exception>
    private static X509Certificate2 Load(byte[] der)
    {
        try
        {
            return X509CertificateLoader.LoadCertificate(der);
        }
        catch (CryptographicException exception)
        {
            throw new NistFormatException(
                $"The certificate is not valid X.509: {exception.Message}"
            );
        }
    }

    /// <summary>
    /// Parses a PEM certificate and checks that it hashes to <paramref name="expectedId"/>.
    /// </summary>
    /// <param name="body">The PEM document, as UTF-8 text.</param>
    /// <param name="expectedId">The pinned SHA-512 identifier of the certificate's DER encoding.</param>
    /// <returns>The certificate, which the caller disposes.</returns>
    /// <exception cref="NistFormatException">
    /// The document is not a PEM certificate, does not hash to the identifier, or holds no RSA key of at least 2048
    /// bits.
    /// </exception>
    public static NistCertificate Parse(ReadOnlySpan<byte> body, ReadOnlySpan<byte> expectedId)
    {
        string text;
        try
        {
            text = Utf8.GetString(body);
        }
        catch (DecoderFallbackException)
        {
            throw new NistFormatException("The certificate is not UTF-8 text.");
        }

        if (!PemEncoding.TryFind(text, out var fields))
        {
            throw new NistFormatException("The certificate is not PEM.");
        }

        var label = text[fields.Label];
        if (!string.Equals(label, PemLabel, StringComparison.Ordinal))
        {
            throw new NistFormatException($"The PEM label is '{label}', not '{PemLabel}'.");
        }

        var der = new byte[fields.DecodedDataLength];
        var base64Data = text.AsSpan(fields.Base64Data);
        if (!Convert.TryFromBase64Chars(base64Data, der, out var written) || written != der.Length)
        {
            throw new NistFormatException("The certificate is not PEM.");
        }

        var id = SHA512.HashData(der);
        if (!CryptographicOperations.FixedTimeEquals(id, expectedId))
        {
            throw new NistFormatException(
                $"Certificate {Convert.ToHexStringLower(id)} does not hash to its identifier."
            );
        }

        using var certificate = Load(der);
        var key = certificate.GetRSAPublicKey();
        if (key is null)
        {
            throw new NistFormatException("The certificate holds no RSA key.");
        }

        if (key.KeySize < MinimumKeyBits)
        {
            var bits = key.KeySize;
            key.Dispose();
            throw new NistFormatException(
                $"The certificate's RSA key has {bits} bits; at least {MinimumKeyBits} are required."
            );
        }

        var notBeforeUtc = certificate.NotBefore.ToUniversalTime();
        var notBefore = new DateTimeOffset(notBeforeUtc);
        var notAfterUtc = certificate.NotAfter.ToUniversalTime();
        var notAfter = new DateTimeOffset(notAfterUtc);
        return new NistCertificate(id, key, notBefore, notAfter);
    }

    /// <summary>
    /// Initializes a certificate from parsed parts; only <see cref="Parse"/> builds a certificate.
    /// </summary>
    /// <param name="id">The SHA-512 identifier of the certificate's DER encoding.</param>
    /// <param name="key">The RSA public key, which the certificate owns from now on.</param>
    /// <param name="notBefore">The start of the certificate's validity, in UTC.</param>
    /// <param name="notAfter">The end of the certificate's validity, in UTC.</param>
    private NistCertificate(byte[] id, RSA key, DateTimeOffset notBefore, DateTimeOffset notAfter)
    {
        Id = id;
        Key = key;
        NotBefore = notBefore;
        NotAfter = notAfter;
    }

    /// <summary>
    /// Gets the certificate's identifier: the SHA-512 of its DER encoding.
    /// </summary>
    public byte[] Id { get; }

    /// <summary>
    /// Gets the certificate's RSA public key, which this instance owns.
    /// </summary>
    public RSA Key { get; }

    /// <summary>
    /// Gets the start of the certificate's validity, in UTC.
    /// </summary>
    public DateTimeOffset NotBefore { get; }

    /// <summary>
    /// Gets the end of the certificate's validity, in UTC.
    /// </summary>
    public DateTimeOffset NotAfter { get; }

    /// <summary>
    /// Disposes the certificate's key.
    /// </summary>
    public void Dispose() => Key.Dispose();
}
