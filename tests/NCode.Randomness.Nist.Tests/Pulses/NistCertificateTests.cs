// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using NCode.Randomness.Nist.Pulses;
using NCode.Randomness.Nist.Tests.Fixtures;
using NCode.Randomness.Nist.Tests.TestNist;

namespace NCode.Randomness.Nist.Tests.Pulses;

public class NistCertificateTests
{
    #region Parse Tests

    [Fact]
    public void Parse_WhenCertificateIsNists_ReadsKeyAndValidity()
    {
        var id = Convert.FromHexString(NistFixtures.CurrentCertificateId);
        var pem = NistFixtures.Certificate("5b6386e6");

        using var certificate = NistCertificate.Parse(pem, id);

        Assert.Equal<byte>(id, certificate.Id);
        Assert.Equal(4096, certificate.Key.KeySize);
        Assert.Equal(
            new DateTimeOffset(2026, 10, 21, 0, 0, 0, TimeSpan.Zero),
            certificate.NotBefore
        );
        Assert.Equal(
            new DateTimeOffset(2027, 3, 20, 23, 59, 59, TimeSpan.Zero),
            certificate.NotAfter
        );
    }

    [Fact]
    public void Parse_WhenCertificateIsGenerated_ReadsItsValidityInUtc()
    {
        var notBefore = new DateTimeOffset(2026, 2, 3, 4, 5, 6, TimeSpan.Zero);
        var notAfter = new DateTimeOffset(2027, 8, 9, 10, 11, 12, TimeSpan.Zero);
        using var signer = new NistTestSigner(notBefore: notBefore, notAfter: notAfter);

        using var certificate = signer.Certificate();

        Assert.Equal<byte>(signer.Id, certificate.Id);
        Assert.Equal(2048, certificate.Key.KeySize);
        Assert.Equal(notBefore, certificate.NotBefore);
        Assert.Equal(notAfter, certificate.NotAfter);
    }

    [Fact]
    public void Parse_WhenIdentifierIsAnotherCertificates_Throws()
    {
        var other = Convert.FromHexString(NistFixtures.PreviousCertificateId);
        var pem = NistFixtures.Certificate("5b6386e6");

        var exception = Assert.Throws<NistFormatException>(() => NistCertificate.Parse(pem, other));

        Assert.Contains("does not hash", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_WhenBodyIsNotPem_Throws()
    {
        var exception = Assert.Throws<NistFormatException>(() =>
            NistCertificate.Parse("not a certificate"u8, new byte[64])
        );

        Assert.Contains("PEM", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_WhenLabelIsNotCertificate_Throws()
    {
        using var signer = new NistTestSigner();
        var text = PemEncoding.WriteString("PUBLIC KEY", signer.Der);
        var pem = Encoding.ASCII.GetBytes(text);

        var exception = Assert.Throws<NistFormatException>(() =>
            NistCertificate.Parse(pem, signer.Id)
        );

        Assert.Contains("CERTIFICATE", exception.Message, StringComparison.Ordinal);
        Assert.Contains("PUBLIC KEY", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_WhenBodyIsNotUtf8_Throws()
    {
        var exception = Assert.Throws<NistFormatException>(() =>
            NistCertificate.Parse([0xFF, 0xFE], new byte[64])
        );

        Assert.Contains("UTF-8", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_WhenKeyIsTooShort_Throws()
    {
        using var signer = new NistTestSigner(keySize: 1024);

        var exception = Assert.Throws<NistFormatException>(() => signer.Certificate());

        Assert.Contains("1024 bits", exception.Message, StringComparison.Ordinal);
        Assert.Contains("2048", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_WhenKeyIsNotRsa_Throws()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=ec", key, HashAlgorithmName.SHA256);
        using var certificate = request.CreateSelfSigned(
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero)
        );
        var der = certificate.RawData;
        var text = PemEncoding.WriteString("CERTIFICATE", der);
        var pem = Encoding.ASCII.GetBytes(text);
        var id = SHA512.HashData(der);

        var exception = Assert.Throws<NistFormatException>(() => NistCertificate.Parse(pem, id));

        Assert.Contains("RSA key", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_WhenDerIsNotX509_Throws()
    {
        var der = RandomNumberGenerator.GetBytes(64);
        var text = PemEncoding.WriteString("CERTIFICATE", der);
        var pem = Encoding.ASCII.GetBytes(text);
        var id = SHA512.HashData(der);

        var exception = Assert.Throws<NistFormatException>(() => NistCertificate.Parse(pem, id));

        Assert.Contains("X.509", exception.Message, StringComparison.Ordinal);
    }

    #endregion
}
