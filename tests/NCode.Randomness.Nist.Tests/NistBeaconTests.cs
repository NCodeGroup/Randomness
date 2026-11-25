// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text;
using NCode.Randomness.Nist.Http;
using NCode.Randomness.Nist.Tests.Fixtures;
using NCode.Randomness.Nist.Tests.TestNist;

namespace NCode.Randomness.Nist.Tests;

public sealed class NistBeaconTests : IDisposable
{
    private const string PulsePath = "/beacon/2.0/chain/2/pulse/";
    private const string CertificatePath = "/beacon/2.0/certificate/";

    private StubNistServer Server { get; } = new();

    private NistBeaconOptions Options { get; } = new();

    private List<IDisposable> Disposables { get; } = [];

    public void Dispose()
    {
        foreach (var disposable in Disposables)
        {
            disposable.Dispose();
        }

        Server.Dispose();
    }

    #region GetLatestPulseAsync Tests

    [Fact]
    public async Task GetLatestPulseAsync_WithNistData_ReturnsTheVerifiedPulse()
    {
        ServeNist();
        var beacon = Create();

        var pulse = await beacon.GetLatestPulseAsync(CancellationToken.None);

        Assert.Equal(NistDefaults.BeaconName, pulse.Beacon);
        Assert.Equal("2", pulse.Chain);
        Assert.Equal(NistFixtures.LatestIndex, pulse.Round);
        Assert.Equal(NistFixtures.LatestOutput, pulse.Id);
        Assert.Equal(NistFixtures.LatestTimestamp, pulse.Timestamp);
        Assert.Equal(NistFixtures.LatestOutput, Convert.ToHexString(pulse.Randomness));
        Assert.Equal(
            [
                PulsePath + "last",
                CertificatePath + NistFixtures.CurrentCertificateId,
                PulsePath + (NistFixtures.LatestIndex - 1),
            ],
            Server.Requests
        );
    }

    [Fact]
    public async Task GetLatestPulseAsync_ForTheNextIndex_ReusesTheLastPulse()
    {
        var signer = Pin(new NistTestSigner());
        var first = NistPulseBuilder.For(100).Build(signer);
        var second = (
            NistPulseBuilder.For(101) with
            {
                Previous = NistPulseBuilder.OutputOf(first),
            }
        ).Build(signer);
        var third = (
            NistPulseBuilder.For(102) with
            {
                Previous = NistPulseBuilder.OutputOf(second),
            }
        ).Build(signer);
        ServeSigner(signer).Serve(PulsePath + "last", second).Serve(PulsePath + "100", first);
        var beacon = Create();
        await beacon.GetLatestPulseAsync(CancellationToken.None);
        Server.Requests.Clear();
        Server.Serve(PulsePath + "last", third);

        var pulse = await beacon.GetLatestPulseAsync(CancellationToken.None);

        Assert.Equal(102, pulse.Round);
        Assert.Equal([PulsePath + "last"], Server.Requests);
    }

    [Fact]
    public async Task GetLatestPulseAsync_WhenPredecessorIsMissing_IsInvalid()
    {
        var signer = Pin(new NistTestSigner());
        ServeSigner(signer).Serve(PulsePath + "last", NistPulseBuilder.For(101).Build(signer));
        var beacon = Create();

        var exception = await BeaconAssert.AssertBeaconErrorAsync(
            beacon.GetLatestPulseAsync,
            NistDefaults.BeaconName,
            BeaconError.InvalidPulse,
            CancellationToken.None
        );

        Assert.Contains("does not serve pulse", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetLatestPulseAsync_WhenPredecessorIsAnotherPulse_IsInvalid()
    {
        ServeNist();
        Server
            .Serve(PulsePath + (NistFixtures.LatestIndex - 1), NistFixtures.Pulse(1484768))
            .Serve(
                CertificatePath + NistFixtures.PreviousCertificateId,
                Encoding.UTF8.GetString(NistFixtures.Certificate("e692b0f6")),
                "text/plain"
            );
        var beacon = Create();

        var exception = await BeaconAssert.AssertBeaconErrorAsync(
            beacon.GetLatestPulseAsync,
            NistDefaults.BeaconName,
            BeaconError.InvalidPulse,
            CancellationToken.None
        );

        Assert.Contains("does not precede", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetLatestPulseAsync_WhenTheCertificateIsNotPinned_IsInvalid()
    {
        using var signer = new NistTestSigner();
        Server.Serve(PulsePath + "last", NistPulseBuilder.For(101).Build(signer));
        var beacon = Create();

        var exception = await BeaconAssert.AssertBeaconErrorAsync(
            beacon.GetLatestPulseAsync,
            NistDefaults.BeaconName,
            BeaconError.InvalidPulse,
            CancellationToken.None
        );

        Assert.Contains("not pinned", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetLatestPulseAsync_WhenThereIsNoLatestPulse_IsUnavailable()
    {
        var beacon = Create();

        await BeaconAssert.AssertBeaconErrorAsync(
            beacon.GetLatestPulseAsync,
            NistDefaults.BeaconName,
            BeaconError.Unavailable,
            CancellationToken.None
        );
    }

    [Fact]
    public async Task GetLatestPulseAsync_WhenTheServerFails_IsUnavailable()
    {
        Server.Answer(PulsePath + "last", HttpStatusCode.ServiceUnavailable);
        var beacon = Create();

        var exception = await BeaconAssert.AssertBeaconErrorAsync(
            beacon.GetLatestPulseAsync,
            NistDefaults.BeaconName,
            BeaconError.Unavailable,
            CancellationToken.None
        );

        Assert.IsType<HttpRequestException>(exception.InnerException);
    }

    [Fact]
    public async Task GetLatestPulseAsync_WhenTheConnectionFails_IsUnavailable()
    {
        Server.Fail(PulsePath + "last", new HttpRequestException("connection refused"));
        var beacon = Create();

        await BeaconAssert.AssertBeaconErrorAsync(
            beacon.GetLatestPulseAsync,
            NistDefaults.BeaconName,
            BeaconError.Unavailable,
            CancellationToken.None
        );
    }

    [Fact]
    public async Task GetLatestPulseAsync_WhenTheRequestTimesOut_IsUnavailable()
    {
        Server.Fail(PulsePath + "last", new TaskCanceledException("timeout"));
        var beacon = Create();

        await BeaconAssert.AssertBeaconErrorAsync(
            beacon.GetLatestPulseAsync,
            NistDefaults.BeaconName,
            BeaconError.Unavailable,
            CancellationToken.None
        );
    }

    [Fact]
    public async Task GetLatestPulseAsync_WhenTheCircuitIsOpen_IsUnavailable()
    {
        Server.Fail(PulsePath + "last", new Polly.CircuitBreaker.BrokenCircuitException("open"));
        var beacon = Create();

        await BeaconAssert.AssertBeaconErrorAsync(
            beacon.GetLatestPulseAsync,
            NistDefaults.BeaconName,
            BeaconError.Unavailable,
            CancellationToken.None
        );
    }

    [Fact]
    public async Task GetLatestPulseAsync_WhenTheCallerCancels_PropagatesTheCancellation()
    {
        ServeNist();
        var beacon = Create();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await beacon.GetLatestPulseAsync(cancellation.Token)
        );
    }

    [Fact]
    public async Task GetLatestPulseAsync_WhenTheAnswerIsNotJson_IsInvalid()
    {
        Server.Serve(PulsePath + "last", NistFixtures.Pulse(NistFixtures.LatestIndex), "text/html");
        var beacon = Create();

        var exception = await BeaconAssert.AssertBeaconErrorAsync(
            beacon.GetLatestPulseAsync,
            NistDefaults.BeaconName,
            BeaconError.InvalidPulse,
            CancellationToken.None
        );

        Assert.Contains("text/html", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetLatestPulseAsync_WhenTheAnswerIsTooLarge_IsInvalid()
    {
        Options.MaxResponseBytes = 1024;
        ServeNist();
        var beacon = Create();

        var exception = await BeaconAssert.AssertBeaconErrorAsync(
            beacon.GetLatestPulseAsync,
            NistDefaults.BeaconName,
            BeaconError.InvalidPulse,
            CancellationToken.None
        );

        Assert.Contains("1024 bytes", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetLatestPulseAsync_WhenThePulseIsOnAnotherChain_IsInvalid()
    {
        var signer = Pin(new NistTestSigner());
        ServeSigner(signer)
            .Serve(
                PulsePath + "last",
                (NistPulseBuilder.For(101) with { ChainIndex = 3 }).Build(signer)
            );
        var beacon = Create();

        var exception = await BeaconAssert.AssertBeaconErrorAsync(
            beacon.GetLatestPulseAsync,
            NistDefaults.BeaconName,
            BeaconError.InvalidPulse,
            CancellationToken.None
        );

        Assert.Contains("chain 3", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetLatestPulseAsync_WhenThePulseStartsTheChain_IsInvalid()
    {
        var signer = Pin(new NistTestSigner());
        ServeSigner(signer).Serve(PulsePath + "last", NistPulseBuilder.For(1).Build(signer));
        var beacon = Create();

        var exception = await BeaconAssert.AssertBeaconErrorAsync(
            beacon.GetLatestPulseAsync,
            NistDefaults.BeaconName,
            BeaconError.InvalidPulse,
            CancellationToken.None
        );

        Assert.Contains("no predecessor", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetLatestPulseAsync_WithAnotherChainIndex_UsesItsPaths()
    {
        Options.ChainIndex = 3;
        var signer = Pin(new NistTestSigner());
        var first = (NistPulseBuilder.For(100) with { ChainIndex = 3 }).Build(signer);
        var second = (
            NistPulseBuilder.For(101) with
            {
                ChainIndex = 3,
                Previous = NistPulseBuilder.OutputOf(first),
            }
        ).Build(signer);
        ServeSigner(signer)
            .Serve("/beacon/2.0/chain/3/pulse/last", second)
            .Serve("/beacon/2.0/chain/3/pulse/100", first);
        var beacon = Create();

        var pulse = await beacon.GetLatestPulseAsync(CancellationToken.None);

        Assert.Equal("3", pulse.Chain);
        Assert.Equal(101, pulse.Round);
        Assert.Equal(
            [
                "/beacon/2.0/chain/3/pulse/last",
                CertificatePath + signer.IdHex,
                "/beacon/2.0/chain/3/pulse/100",
            ],
            Server.Requests
        );
    }

    #endregion

    #region GetPulseAsync Tests

    [Fact]
    public async Task GetPulseAsync_WithPublishedIndex_ReturnsIt()
    {
        ServeNist();
        Server.Serve(
            PulsePath + NistFixtures.LatestIndex,
            NistFixtures.Pulse(NistFixtures.LatestIndex)
        );
        var beacon = Create();

        var pulse = await beacon.GetPulseAsync(NistFixtures.LatestIndex, CancellationToken.None);

        Assert.Equal(NistFixtures.LatestIndex, pulse.Round);
        Assert.Equal(NistFixtures.LatestOutput, pulse.Id);
    }

    [Fact]
    public async Task GetPulseAsync_AcrossACertificateRotation_ReturnsIt()
    {
        Server
            .Serve(PulsePath + "1484769", NistFixtures.Pulse(1484769))
            .Serve(PulsePath + "1484768", NistFixtures.Pulse(1484768))
            .Serve(
                CertificatePath + NistFixtures.RotatedCertificateId,
                Encoding.UTF8.GetString(NistFixtures.Certificate("528943a5")),
                "text/plain"
            )
            .Serve(
                CertificatePath + NistFixtures.PreviousCertificateId,
                Encoding.UTF8.GetString(NistFixtures.Certificate("e692b0f6")),
                "text/plain"
            );
        var beacon = Create();

        var pulse = await beacon.GetPulseAsync(1484769, CancellationToken.None);

        Assert.Equal(1484769, pulse.Round);
        Assert.Equal(64, pulse.Randomness.Length);
    }

    [Fact]
    public async Task GetPulseAsync_WhenServerAnswersAnotherPulse_IsInvalid()
    {
        ServeNist();
        Server.Serve(PulsePath + "2043000", NistFixtures.Pulse(NistFixtures.LatestIndex));
        var beacon = Create();

        var exception = await BeaconAssert.AssertBeaconErrorAsync(
            token => beacon.GetPulseAsync(2043000, token),
            NistDefaults.BeaconName,
            BeaconError.InvalidPulse,
            CancellationToken.None
        );

        Assert.Contains("pulse 2043331", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetPulseAsync_WithUnpublishedIndex_IsNotFound()
    {
        var beacon = Create();

        await BeaconAssert.AssertBeaconErrorAsync(
            token => beacon.GetPulseAsync(99_999_999, token),
            NistDefaults.BeaconName,
            BeaconError.PulseNotFound,
            CancellationToken.None
        );
    }

    [Theory]
    [InlineData(1L)]
    [InlineData(0L)]
    [InlineData(-1L)]
    public async Task GetPulseAsync_WithIndexWithoutRandomness_IsNotFound(long round)
    {
        var beacon = Create();

        var exception = await BeaconAssert.AssertBeaconErrorAsync(
            token => beacon.GetPulseAsync(round, token),
            NistDefaults.BeaconName,
            BeaconError.PulseNotFound,
            CancellationToken.None
        );

        Assert.Contains("no predecessor", exception.Message, StringComparison.Ordinal);
        Assert.Empty(Server.Requests);
    }

    #endregion

    private NistTestSigner Pin(NistTestSigner signer)
    {
        Disposables.Add(signer);
        Options.CertificateIds = [signer.IdHex];
        return signer;
    }

    private StubNistServer ServeSigner(NistTestSigner signer) =>
        Server.Serve(
            CertificatePath + signer.IdHex,
            Encoding.ASCII.GetString(signer.Pem),
            "text/plain"
        );

    private void ServeNist() =>
        Server
            .Serve(PulsePath + "last", NistFixtures.Pulse(NistFixtures.LatestIndex))
            .Serve(
                PulsePath + (NistFixtures.LatestIndex - 1),
                NistFixtures.Pulse(NistFixtures.LatestIndex - 1)
            )
            .Serve(
                CertificatePath + NistFixtures.CurrentCertificateId,
                Encoding.UTF8.GetString(NistFixtures.Certificate("5b6386e6")),
                "text/plain"
            );

    private NistBeacon Create()
    {
        var options = Microsoft.Extensions.Options.Options.Create(Options);
        var api = new NistApi(Server, options);
        var certificates = new NistCertificateCache(api, options);
        Disposables.Add(certificates);
        return new NistBeacon(api, certificates, options);
    }
}
