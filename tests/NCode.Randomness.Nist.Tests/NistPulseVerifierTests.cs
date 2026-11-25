// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using NCode.Randomness.Nist.Tests.Fixtures;
using NCode.Randomness.Nist.Tests.TestNist;

namespace NCode.Randomness.Nist.Tests;

public class NistPulseVerifierTests
{
    private static byte[] Utf8(string json) => Encoding.UTF8.GetBytes(json);

    private static void AssertInvalid(BeaconException exception, string fragment)
    {
        Assert.Equal(BeaconError.InvalidPulse, exception.Error);
        Assert.Equal(NistDefaults.BeaconName, exception.Beacon);
        Assert.Contains(fragment, exception.Message, StringComparison.Ordinal);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithChainIndexBelowOne_ThrowsArgumentOutOfRangeException()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new NistPulseVerifier(0, [NistFixtures.CurrentCertificateId])
        );

        Assert.Equal("chainIndex", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNoCertificateIds_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new NistPulseVerifier(NistDefaults.ChainIndex, [])
        );

        Assert.Equal("certificateIds", exception.ParamName);
        Assert.Contains(
            "At least one certificate identifier",
            exception.Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Constructor_WithInvalidCertificateId_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new NistPulseVerifier(NistDefaults.ChainIndex, ["not-a-certificate-id"])
        );

        Assert.Equal("certificateIds", exception.ParamName);
        Assert.Contains(
            "is not a certificate identifier",
            exception.Message,
            StringComparison.Ordinal
        );
    }

    #endregion

    #region Verify Tests

    [Fact]
    public void Verify_WithNistDocuments_ReturnsTheVerifiedPulse()
    {
        var pulseJson = NistFixtures.Pulse(NistFixtures.LatestIndex);
        var pulseDocument = Utf8(pulseJson);
        var predecessorJson = NistFixtures.Pulse(NistFixtures.LatestIndex - 1);
        var predecessor = Utf8(predecessorJson);
        var certificate = NistFixtures.Certificate("5b6386e6");

        var pulse = new NistPulseVerifier().Verify(pulseDocument, predecessor, certificate);

        var randomness = Convert.ToHexString(pulse.Randomness);
        Assert.Equal(NistDefaults.BeaconName, pulse.Beacon);
        Assert.Equal("2", pulse.Chain);
        Assert.Equal(NistFixtures.LatestIndex, pulse.Round);
        Assert.Equal(NistFixtures.LatestOutput, pulse.Id);
        Assert.Equal(NistFixtures.LatestTimestamp, pulse.Timestamp);
        Assert.Equal(NistFixtures.LatestOutput, randomness);
    }

    [Fact]
    public void Verify_AcrossACertificateRotation_ReturnsThePulse()
    {
        var pulseJson = NistFixtures.Pulse(1484769);
        var pulseDocument = Utf8(pulseJson);
        var predecessorJson = NistFixtures.Pulse(1484768);
        var predecessor = Utf8(predecessorJson);
        var certificate = NistFixtures.Certificate("528943a5");
        var predecessorCertificate = NistFixtures.Certificate("e692b0f6");
        var output = NistPulseBuilder.OutputOf(pulseJson);
        var expectedId = Convert.ToHexString(output);

        var pulse = new NistPulseVerifier().Verify(
            pulseDocument,
            predecessor,
            certificate,
            predecessorCertificate
        );

        Assert.Equal(1484769, pulse.Round);
        Assert.Equal(expectedId, pulse.Id);
        Assert.Equal(64, pulse.Randomness.Length);
    }

    [Fact]
    public void Verify_AcrossACertificateRotationWithOneCertificate_IsInvalid()
    {
        var pulseJson = NistFixtures.Pulse(1484769);
        var pulseDocument = Utf8(pulseJson);
        var predecessorJson = NistFixtures.Pulse(1484768);
        var predecessor = Utf8(predecessorJson);
        var certificate = NistFixtures.Certificate("528943a5");
        var verifier = new NistPulseVerifier();

        var exception = Assert.Throws<BeaconException>(() =>
            verifier.Verify(pulseDocument, predecessor, certificate)
        );

        AssertInvalid(exception, "does not hash to its identifier");
    }

    [Fact]
    public void Verify_WhenPredecessorIsAnotherPulse_IsInvalid()
    {
        var pulseJson = NistFixtures.Pulse(NistFixtures.LatestIndex);
        var pulseDocument = Utf8(pulseJson);
        var predecessorJson = NistFixtures.Pulse(1484768);
        var predecessor = Utf8(predecessorJson);
        var certificate = NistFixtures.Certificate("5b6386e6");
        var predecessorCertificate = NistFixtures.Certificate("e692b0f6");
        var verifier = new NistPulseVerifier();

        var exception = Assert.Throws<BeaconException>(() =>
            verifier.Verify(pulseDocument, predecessor, certificate, predecessorCertificate)
        );

        AssertInvalid(exception, "does not precede");
    }

    [Fact]
    public void Verify_WhenTheCertificateIsNotPinned_IsInvalid()
    {
        using var signer = new NistTestSigner();
        var first = NistPulseBuilder.For(100).Build(signer);
        var firstOutput = NistPulseBuilder.OutputOf(first);
        var second = (NistPulseBuilder.For(101) with { Previous = firstOutput }).Build(signer);
        var pulseDocument = Utf8(second);
        var predecessor = Utf8(first);
        var verifier = new NistPulseVerifier();

        var exception = Assert.Throws<BeaconException>(() =>
            verifier.Verify(pulseDocument, predecessor, signer.Pem)
        );

        AssertInvalid(exception, "not pinned");
    }

    [Fact]
    public void Verify_WhenThePulseIsOnAnotherChain_IsInvalid()
    {
        using var signer = new NistTestSigner();
        var first = (NistPulseBuilder.For(100) with { ChainIndex = 3 }).Build(signer);
        var firstOutput = NistPulseBuilder.OutputOf(first);
        var second = (
            NistPulseBuilder.For(101) with
            {
                ChainIndex = 3,
                Previous = firstOutput,
            }
        ).Build(signer);
        var pulseDocument = Utf8(second);
        var predecessor = Utf8(first);
        var verifier = new NistPulseVerifier(NistDefaults.ChainIndex, [signer.IdHex]);

        var exception = Assert.Throws<BeaconException>(() =>
            verifier.Verify(pulseDocument, predecessor, signer.Pem)
        );

        AssertInvalid(exception, "chain 3");
    }

    [Fact]
    public void Verify_WithTamperedPulse_IsInvalid()
    {
        var tampered = NistFixtures
            .Pulse(NistFixtures.LatestIndex)
            .Replace("35D0537D3F6495AF", "35D0537D3F6495AE", StringComparison.Ordinal);
        var pulseDocument = Utf8(tampered);
        var predecessorJson = NistFixtures.Pulse(NistFixtures.LatestIndex - 1);
        var predecessor = Utf8(predecessorJson);
        var certificate = NistFixtures.Certificate("5b6386e6");
        var verifier = new NistPulseVerifier();

        var exception = Assert.Throws<BeaconException>(() =>
            verifier.Verify(pulseDocument, predecessor, certificate)
        );

        AssertInvalid(exception, "does not verify");
    }

    [Fact]
    public void Verify_WithMalformedJson_IsInvalid()
    {
        var pulseDocument = "{"u8.ToArray();
        var predecessorJson = NistFixtures.Pulse(NistFixtures.LatestIndex - 1);
        var predecessor = Utf8(predecessorJson);
        var certificate = NistFixtures.Certificate("5b6386e6");
        var verifier = new NistPulseVerifier();

        var exception = Assert.Throws<BeaconException>(() =>
            verifier.Verify(pulseDocument, predecessor, certificate)
        );

        AssertInvalid(exception, "not valid JSON");
    }

    [Fact]
    public void Verify_WithMalformedCertificate_IsInvalid()
    {
        var pulseJson = NistFixtures.Pulse(NistFixtures.LatestIndex);
        var pulseDocument = Utf8(pulseJson);
        var predecessorJson = NistFixtures.Pulse(NistFixtures.LatestIndex - 1);
        var predecessor = Utf8(predecessorJson);
        var certificate = "not a certificate"u8.ToArray();
        var verifier = new NistPulseVerifier();

        var exception = Assert.Throws<BeaconException>(() =>
            verifier.Verify(pulseDocument, predecessor, certificate)
        );

        AssertInvalid(exception, "not PEM");
    }

    [Fact]
    public void Verify_WithAnotherPinnedCertificate_AcceptsItsPulses()
    {
        using var signer = new NistTestSigner();
        var first = NistPulseBuilder.For(100).Build(signer);
        var firstOutput = NistPulseBuilder.OutputOf(first);
        var second = (NistPulseBuilder.For(101) with { Previous = firstOutput }).Build(signer);
        var output = NistPulseBuilder.OutputOf(second);
        var expectedId = Convert.ToHexString(output);
        var pulseDocument = Utf8(second);
        var predecessor = Utf8(first);
        var verifier = new NistPulseVerifier(NistDefaults.ChainIndex, [signer.IdHex]);

        var pulse = verifier.Verify(pulseDocument, predecessor, signer.Pem);

        Assert.Equal(101, pulse.Round);
        Assert.Equal(expectedId, pulse.Id);
    }

    #endregion
}
