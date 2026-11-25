// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using NCode.Randomness.Nist.Pulses;

namespace NCode.Randomness.Nist.Tests.TestNist;

/// <summary>
/// A test beacon's RSA key and its self-signed certificate, which signs pulses the way NIST does: RSASSA-PKCS1-v1_5
/// with SHA-512.
/// </summary>
internal sealed class NistTestSigner : IDisposable
{
    public NistTestSigner(
        int keySize = 2048,
        DateTimeOffset? notBefore = null,
        DateTimeOffset? notAfter = null
    )
    {
        Key = RSA.Create(keySize);
        var request = new CertificateRequest(
            "CN=NCode.Randomness test beacon",
            Key,
            HashAlgorithmName.SHA512,
            RSASignaturePadding.Pkcs1
        );
        using var certificate = request.CreateSelfSigned(
            notBefore ?? new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            notAfter ?? new DateTimeOffset(2027, 12, 31, 0, 0, 0, TimeSpan.Zero)
        );
        Der = certificate.RawData;
        var pem = PemEncoding.WriteString("CERTIFICATE", Der);
        Pem = Encoding.ASCII.GetBytes(pem);
        Id = SHA512.HashData(Der);
    }

    public RSA Key { get; }

    public byte[] Der { get; }

    public byte[] Pem { get; }

    public byte[] Id { get; }

    public string IdHex => Convert.ToHexStringLower(Id);

    public byte[] Sign(byte[] data) =>
        Key.SignData(data, HashAlgorithmName.SHA512, RSASignaturePadding.Pkcs1);

    public NistCertificate Certificate() => NistCertificate.Parse(Pem, Id);

    public void Dispose() => Key.Dispose();
}
