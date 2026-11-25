// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text.Json;
using NCode.Randomness.Nist.Http;
using NCode.Randomness.Nist.Pulses;
using NCode.Randomness.Nist.Tests.Fixtures;
using NCode.Randomness.Nist.Tests.TestNist;

namespace NCode.Randomness.Nist.Tests.Http;

public sealed class NistApiTests : IDisposable
{
    private const string PulsePath = "chain/2/pulse/last";
    private const string PulseRoute = "/beacon/2.0/chain/2/pulse/last";
    private static string CertificatePath { get; } =
        "certificate/" + NistFixtures.CurrentCertificateId;
    private static string CertificateRoute { get; } = "/beacon/2.0/" + CertificatePath;

    private StubNistServer Server { get; } = new();

    private NistBeaconOptions Options { get; } = new();

    public void Dispose() => Server.Dispose();

    #region GetPulseAsync Tests

    [Fact]
    public async Task GetPulseAsync_WithJson_ReturnsTheDocument()
    {
        var json = NistFixtures.Pulse(NistFixtures.LatestIndex);
        Server.Serve(PulseRoute, json);
        var api = Create();

        var document = await api.GetPulseAsync(PulsePath, CancellationToken.None);

        Assert.NotNull(document);
        var index = document.Value.GetProperty("pulse").GetProperty("pulseIndex").GetInt64();

        Assert.Equal(NistFixtures.LatestIndex, index);
    }

    [Fact]
    public async Task GetPulseAsync_WithDefaultOptions_RequestsTheDefaultPath()
    {
        var json = NistFixtures.Pulse(NistFixtures.LatestIndex);
        Server.Serve(PulseRoute, json);
        var api = Create();

        await api.GetPulseAsync(PulsePath, CancellationToken.None);

        Assert.Equal([PulseRoute], Server.Requests);
    }

    [Fact]
    public async Task GetPulseAsync_WhenNotFound_ReturnsNull()
    {
        var api = Create();

        var document = await api.GetPulseAsync(PulsePath, CancellationToken.None);

        Assert.Null(document);
        Assert.Equal([PulseRoute], Server.Requests);
    }

    [Fact]
    public async Task GetPulseAsync_WithOtherMediaType_Throws()
    {
        Server.Serve(PulseRoute, "<html></html>", "text/html");
        var api = Create();

        var exception = await Assert.ThrowsAsync<NistFormatException>(async () =>
            await api.GetPulseAsync(PulsePath, CancellationToken.None)
        );

        Assert.Contains("text/html", exception.Message, StringComparison.Ordinal);
        Assert.Contains("application/json", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetPulseAsync_OverTheLimit_Throws()
    {
        Options.MaxResponseBytes = 1024;
        var json = "\"" + new string('a', 2000) + "\"";
        Server.Serve(PulseRoute, json);
        var api = Create();

        var exception = await Assert.ThrowsAsync<NistFormatException>(async () =>
            await api.GetPulseAsync(PulsePath, CancellationToken.None)
        );

        Assert.Contains("1024 bytes", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetPulseAsync_AtTheLimit_ReturnsTheDocument()
    {
        Options.MaxResponseBytes = 1024;
        var json = "\"" + new string('a', 1022) + "\"";
        Server.Serve(PulseRoute, json);
        var api = Create();

        var document = await api.GetPulseAsync(PulsePath, CancellationToken.None);

        Assert.NotNull(document);
        var text = document.Value.GetString();
        Assert.Equal(1022, text?.Length);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("{\"a\":1,\"a\":2}")]
    public async Task GetPulseAsync_WithMalformedJson_Throws(string json)
    {
        Server.Serve(PulseRoute, json);
        var api = Create();

        var exception = await Assert.ThrowsAsync<NistFormatException>(async () =>
            await api.GetPulseAsync(PulsePath, CancellationToken.None)
        );

        Assert.Contains("malformed JSON", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetPulseAsync_WithTruncatedJson_KeepsTheParserException()
    {
        Server.Serve(PulseRoute, "{");
        var api = Create();

        var exception = await Assert.ThrowsAsync<NistFormatException>(async () =>
            await api.GetPulseAsync(PulsePath, CancellationToken.None)
        );

        Assert.IsAssignableFrom<JsonException>(exception.InnerException);
    }

    [Fact]
    public async Task GetPulseAsync_WithServiceUnavailable_Throws()
    {
        Server.Answer(PulseRoute, HttpStatusCode.ServiceUnavailable);
        var api = Create();

        var exception = await Assert.ThrowsAsync<HttpRequestException>(async () =>
            await api.GetPulseAsync(PulsePath, CancellationToken.None)
        );

        Assert.Contains("503", exception.Message, StringComparison.Ordinal);
    }

    #endregion

    #region GetCertificateAsync Tests

    [Fact]
    public async Task GetCertificateAsync_WithPlainText_ReturnsTheBody()
    {
        Server.Serve(CertificateRoute, "pem text", "text/plain");
        var api = Create();

        var body = await api.GetCertificateAsync(
            NistFixtures.CurrentCertificateId,
            CancellationToken.None
        );

        var expected = "pem text"u8.ToArray();

        Assert.Equal(expected, body);
        Assert.Equal([CertificateRoute], Server.Requests);
    }

    [Fact]
    public async Task GetCertificateAsync_WhenNotFound_ReturnsNull()
    {
        var api = Create();

        var body = await api.GetCertificateAsync(
            NistFixtures.CurrentCertificateId,
            CancellationToken.None
        );

        Assert.Null(body);
    }

    [Fact]
    public async Task GetCertificateAsync_WithJson_Throws()
    {
        Server.Serve(CertificateRoute, "{}", "application/json");
        var api = Create();

        var exception = await Assert.ThrowsAsync<NistFormatException>(async () =>
            await api.GetCertificateAsync(NistFixtures.CurrentCertificateId, CancellationToken.None)
        );

        Assert.Contains("application/json", exception.Message, StringComparison.Ordinal);
        Assert.Contains("text/plain", exception.Message, StringComparison.Ordinal);
    }

    #endregion

    private NistApi Create()
    {
        var options = Microsoft.Extensions.Options.Options.Create(Options);
        return new NistApi(Server, options);
    }
}
