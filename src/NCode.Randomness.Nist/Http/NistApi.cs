// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using NCode.Randomness.Nist.Pulses;

namespace NCode.Randomness.Nist.Http;

/// <summary>
/// Reads pulses and certificates from the NIST HTTP API: one expected media type per request, no redirects, and the
/// body read under a byte limit while it streams, since <c>Content-Length</c> is only a hint.
/// </summary>
internal sealed class NistApi(IHttpClientFactory clients, IOptions<NistBeaconOptions> options)
{
    private const string JsonMediaType = "application/json";
    private const string TextMediaType = "text/plain";

    private static JsonDocumentOptions ParseOptions { get; } =
        new() { MaxDepth = 16, AllowDuplicateProperties = false };

    /// <summary>
    /// Parses <paramref name="body"/> as JSON, accepting nesting up to 16 levels deep and no duplicate property names.
    /// </summary>
    /// <param name="body">The response body, as UTF-8 JSON.</param>
    /// <returns>The root element of the parsed document.</returns>
    /// <exception cref="NistFormatException"><paramref name="body"/> is not valid JSON.</exception>
    private static JsonElement ParseJson(byte[] body)
    {
        try
        {
            return JsonElement.Parse(body, ParseOptions);
        }
        catch (JsonException exception)
        {
            throw new NistFormatException(
                $"NIST answered with malformed JSON: {exception.Message}"
            );
        }
    }

    private IHttpClientFactory Clients { get; } = clients;

    private NistBeaconOptions Options { get; } = options.Value;

    /// <summary>
    /// Fetches the JSON document at <paramref name="path"/>, relative to the base address, or returns
    /// <see langword="null"/> when the API answers 404.
    /// </summary>
    /// <param name="path">The path to fetch, relative to the base address.</param>
    /// <param name="cancellationToken">The token that cancels the request and the read.</param>
    /// <returns>The root element of the document, or <see langword="null"/> when the API answers 404.</returns>
    public async ValueTask<JsonElement?> GetPulseAsync(
        string path,
        CancellationToken cancellationToken
    )
    {
        var body = await GetBodyAsync(path, JsonMediaType, cancellationToken).ConfigureAwait(false);
        if (body is null)
        {
            return null;
        }

        return ParseJson(body);
    }

    /// <summary>
    /// Fetches the PEM certificate with the identifier <paramref name="id"/>, or returns <see langword="null"/> when
    /// the API answers 404.
    /// </summary>
    /// <param name="id">The certificate identifier: the SHA-512 hash of its DER encoding, in hex.</param>
    /// <param name="cancellationToken">The token that cancels the request and the read.</param>
    /// <returns>The response body, or <see langword="null"/> when the API answers 404.</returns>
    public ValueTask<byte[]?> GetCertificateAsync(string id, CancellationToken cancellationToken) =>
        GetBodyAsync($"certificate/{id}", TextMediaType, cancellationToken);

    /// <summary>
    /// Sends a GET request for <paramref name="path"/>, relative to the base address, that accepts only
    /// <paramref name="mediaType"/>, and returns the body read under the byte limit, or <see langword="null"/> when the
    /// API answers 404.
    /// </summary>
    /// <param name="path">The path to fetch, relative to the base address.</param>
    /// <param name="mediaType">The one media type the request accepts and the response must have.</param>
    /// <param name="cancellationToken">The token that cancels the request and the read.</param>
    /// <returns>The response body, or <see langword="null"/> when the API answers 404.</returns>
    /// <exception cref="HttpRequestException">The API answered a status other than success or 404.</exception>
    /// <exception cref="NistFormatException">
    /// The response has another media type than <paramref name="mediaType"/>, or its body is larger than the limit.
    /// </exception>
    private async ValueTask<byte[]?> GetBodyAsync(
        string path,
        string mediaType,
        CancellationToken cancellationToken
    )
    {
        var client = Clients.CreateClient(NistDefaults.HttpClientName);
        var address = new Uri(Options.BaseAddress, path);
        using var request = new HttpRequestMessage(HttpMethod.Get, address);
        request.Headers.Accept.ParseAdd(mediaType);
        using var response = await client
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"NIST answered {(int)response.StatusCode}.",
                null,
                response.StatusCode
            );
        }

        var actual = response.Content.Headers.ContentType?.MediaType;
        if (!string.Equals(actual, mediaType, StringComparison.OrdinalIgnoreCase))
        {
            throw new NistFormatException($"NIST answered with '{actual}', not '{mediaType}'.");
        }

        return await ReadLimitedAsync(response.Content, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads a response body while it streams, refusing one larger than the configured limit.
    /// </summary>
    /// <param name="content">The response content.</param>
    /// <param name="cancellationToken">The token that cancels the read.</param>
    /// <returns>The whole body.</returns>
    /// <exception cref="NistFormatException">The body is larger than <c>MaxResponseBytes</c>.</exception>
    private async ValueTask<byte[]> ReadLimitedAsync(
        HttpContent content,
        CancellationToken cancellationToken
    )
    {
        var limit = Options.MaxResponseBytes;
        if (content.Headers.ContentLength > limit)
        {
            throw new NistFormatException($"NIST answered with more than {limit} bytes.");
        }

        var stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            using var body = new MemoryStream();
            var buffer = new byte[16 * 1024];
            int read;
            while (
                (read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0
            )
            {
                if (body.Length + read > limit)
                {
                    throw new NistFormatException($"NIST answered with more than {limit} bytes.");
                }

                body.Write(buffer, 0, read);
            }

            return body.ToArray();
        }
    }
}
