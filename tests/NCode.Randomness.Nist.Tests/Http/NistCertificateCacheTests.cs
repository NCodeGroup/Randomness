// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NCode.Randomness.Nist.Http;
using NCode.Randomness.Nist.Pulses;
using NCode.Randomness.Nist.Tests.Fixtures;
using NCode.Randomness.Nist.Tests.TestNist;

namespace NCode.Randomness.Nist.Tests.Http;

public sealed class NistCertificateCacheTests : IDisposable
{
    private static string CurrentRoute { get; } =
        "/beacon/2.0/certificate/" + NistFixtures.CurrentCertificateId;

    private static byte[] Id(string hex) => Convert.FromHexString(hex);

    private StubNistServer Server { get; } = new();

    private FakeTimeProvider Time { get; } =
        new(new DateTimeOffset(2026, 11, 25, 12, 0, 0, TimeSpan.Zero));

    private List<IDisposable> Disposables { get; } = [];

    public void Dispose()
    {
        foreach (var disposable in Disposables)
        {
            disposable.Dispose();
        }

        Server.Dispose();
    }

    private void ServeCurrent()
    {
        var certificateBytes = NistFixtures.Certificate("5b6386e6");
        var certificate = Encoding.UTF8.GetString(certificateBytes);
        Server.Serve(CurrentRoute, certificate, "text/plain");
    }

    private NistCertificateCache Create(NistBeaconOptions? beaconOptions = null)
    {
        var options = Options.Create(beaconOptions ?? new NistBeaconOptions());
        var api = new NistApi(Server, options);
        var cache = new NistCertificateCache(api, options, Time);
        Disposables.Add(cache);
        return cache;
    }

    #region GetAsync Tests

    [Fact]
    public async Task GetAsync_TwiceForOneCertificate_FetchesItOnce()
    {
        ServeCurrent();
        var cache = Create();
        var id = Id(NistFixtures.CurrentCertificateId);

        var first = await cache.GetAsync(id, CancellationToken.None);
        var second = await cache.GetAsync(id, CancellationToken.None);

        Assert.Same(first, second);
        Assert.Equal([CurrentRoute], Server.Requests);
    }

    [Fact]
    public async Task GetAsync_WithAnUnpinnedId_ThrowsWithoutARequest()
    {
        using var signer = new NistTestSigner();
        var cache = Create();

        var exception = await Assert.ThrowsAsync<NistFormatException>(async () =>
            await cache.GetAsync(signer.Id, CancellationToken.None)
        );

        Assert.Contains("is not pinned", exception.Message, StringComparison.Ordinal);
        Assert.Contains(
            "RandomnessBeacons:Nist:CertificateIds",
            exception.Message,
            StringComparison.Ordinal
        );
        Assert.Empty(Server.Requests);
    }

    [Fact]
    public async Task GetAsync_WithDefaultOptions_AcceptsTheCurrentCertificate()
    {
        ServeCurrent();
        var cache = Create();
        var id = Id(NistFixtures.CurrentCertificateId);

        var certificate = await cache.GetAsync(id, CancellationToken.None);

        Assert.Equal(id, certificate.Id);
    }

    [Fact]
    public async Task GetAsync_WithConfiguredIds_ReplacesTheDefaults()
    {
        using var signer = new NistTestSigner();
        ServeCurrent();
        var pem = Encoding.ASCII.GetString(signer.Pem);
        Server.Serve("/beacon/2.0/certificate/" + signer.IdHex, pem, "text/plain");
        var cache = Create(new NistBeaconOptions { CertificateIds = [signer.IdHex] });
        var currentId = Id(NistFixtures.CurrentCertificateId);

        var exception = await Assert.ThrowsAsync<NistFormatException>(async () =>
            await cache.GetAsync(currentId, CancellationToken.None)
        );
        var certificate = await cache.GetAsync(signer.Id, CancellationToken.None);

        Assert.Contains("is not pinned", exception.Message, StringComparison.Ordinal);
        Assert.Equal(signer.Id, certificate.Id);
        Assert.Equal(["/beacon/2.0/certificate/" + signer.IdHex], Server.Requests);
    }

    [Fact]
    public async Task GetAsync_WithAnUppercaseConfiguredId_MatchesIt()
    {
        using var signer = new NistTestSigner();
        var pem = Encoding.ASCII.GetString(signer.Pem);
        Server.Serve("/beacon/2.0/certificate/" + signer.IdHex, pem, "text/plain");
        var upperId = signer.IdHex.ToUpperInvariant();
        var cache = Create(new NistBeaconOptions { CertificateIds = [upperId] });

        var certificate = await cache.GetAsync(signer.Id, CancellationToken.None);

        Assert.Equal(signer.Id, certificate.Id);
    }

    [Fact]
    public async Task GetAsync_WhenPinnedButNotServed_Throws()
    {
        var cache = Create();
        var id = Id(NistFixtures.CurrentCertificateId);

        var exception = await Assert.ThrowsAsync<NistFormatException>(async () =>
            await cache.GetAsync(id, CancellationToken.None)
        );

        Assert.Contains("does not serve", exception.Message, StringComparison.Ordinal);
        Assert.Equal([CurrentRoute], Server.Requests);
    }

    [Fact]
    public async Task GetAsync_WhenTwoCallersAskAtOnce_FetchesOnce()
    {
        var cache = Create();
        var id = Id(NistFixtures.CurrentCertificateId);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        ServeCurrent();
        Server.Hold(CurrentRoute, release.Task);

        var first = cache.GetAsync(id, CancellationToken.None).AsTask();
        var second = cache.GetAsync(id, CancellationToken.None).AsTask();
        var waiting = !first.IsCompleted && !second.IsCompleted;
        release.SetResult();
        var firstCertificate = await first;
        var secondCertificate = await second;

        Assert.True(waiting);
        Assert.Same(firstCertificate, secondCertificate);
        Assert.Equal([CurrentRoute], Server.Requests);
    }

    [Fact]
    public async Task GetAsync_WhenTheFirstCallerCancels_StillServesTheSecond()
    {
        var cache = Create();
        var id = Id(NistFixtures.CurrentCertificateId);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        ServeCurrent();
        Server.Hold(CurrentRoute, release.Task);
        using var cancellation = new CancellationTokenSource();

        var first = cache.GetAsync(id, cancellation.Token).AsTask();
        var second = cache.GetAsync(id, CancellationToken.None).AsTask();
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        release.SetResult();
        var certificate = await second;

        Assert.Equal(id, certificate.Id);
        Assert.Equal([CurrentRoute], Server.Requests);
    }

    [Fact]
    public async Task GetAsync_WithinTheRetryDelayAfterAFailure_FailsWithoutARequest()
    {
        var cache = Create();
        var id = Id(NistFixtures.CurrentCertificateId);
        await Assert.ThrowsAsync<NistFormatException>(async () =>
            await cache.GetAsync(id, CancellationToken.None)
        );
        ServeCurrent();
        Time.Advance(TimeSpan.FromSeconds(59));

        var exception = await Assert.ThrowsAsync<NistFormatException>(async () =>
            await cache.GetAsync(id, CancellationToken.None)
        );

        Assert.Contains("does not serve", exception.Message, StringComparison.Ordinal);
        Assert.Equal([CurrentRoute], Server.Requests);
    }

    [Fact]
    public async Task GetAsync_AfterTheRetryDelay_FetchesAgain()
    {
        var cache = Create();
        var id = Id(NistFixtures.CurrentCertificateId);
        await Assert.ThrowsAsync<NistFormatException>(async () =>
            await cache.GetAsync(id, CancellationToken.None)
        );
        ServeCurrent();
        Time.Advance(TimeSpan.FromMinutes(1));

        var certificate = await cache.GetAsync(id, CancellationToken.None);

        Assert.Equal(id, certificate.Id);
        Assert.Equal([CurrentRoute, CurrentRoute], Server.Requests);
    }

    [Fact]
    public async Task GetAsync_WhenTheFetchOutlastsItsTimeout_IsCancelled()
    {
        var cache = Create();
        var id = Id(NistFixtures.CurrentCertificateId);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        ServeCurrent();
        Server.Hold(CurrentRoute, release.Task);

        var pending = cache.GetAsync(id, CancellationToken.None).AsTask();
        Time.Advance(NistCertificateCache.FetchTimeout);
        // The real 10-second bound only stops a broken fetch from hanging the run; it ends in a TimeoutException,
        // which fails the assertion.
        var bounded = pending.WaitAsync(TimeSpan.FromSeconds(10));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => bounded);
    }

    #endregion
}
