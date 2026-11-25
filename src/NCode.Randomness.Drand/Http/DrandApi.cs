// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using NCode.Randomness.Drand.Rounds;

namespace NCode.Randomness.Drand.Http;

/// <summary>
/// Reads rounds from the drand HTTP API: one expected media type per request, no redirects, and the body read under a
/// byte limit while it streams, since <c>Content-Length</c> is only a hint.
/// </summary>
// Copied on purpose from TwineApi in NCode.Randomness.Curby, as NistApi in NCode.Randomness.Nist is: independently
// versioned packages never share a type, so a fix to one copy belongs in the others.
internal sealed class DrandApi(IHttpClientFactory clients, IOptions<DrandBeaconOptions> options)
{
    /// <summary>
    /// The media type that a round response must have.
    /// </summary>
    private const string JsonMediaType = "application/json";

    /// <summary>
    /// The number of bytes, 16 KiB, read from a response body at a time.
    /// </summary>
    private const int ChunkBytes = 16 * 1024;

    /// <summary>
    /// The status drand answers for a round that is not yet due: 425 Too Early, which <see cref="HttpStatusCode"/> has
    /// no member for.
    /// </summary>
    private const HttpStatusCode TooEarly = (HttpStatusCode)425;

    /// <summary>
    /// Gets the options that parse a response body: nesting up to 16 levels deep and no duplicate property names.
    /// </summary>
    private static JsonDocumentOptions ParseOptions { get; } =
        new() { MaxDepth = 16, AllowDuplicateProperties = false };

    /// <summary>
    /// Parses <paramref name="body"/> as JSON, accepting nesting up to 16 levels deep and no duplicate property names.
    /// </summary>
    /// <param name="body">The response body, as UTF-8 JSON.</param>
    /// <returns>The root element of the parsed document.</returns>
    /// <exception cref="DrandFormatException"><paramref name="body"/> is not valid JSON.</exception>
    private static JsonElement ParseJson(byte[] body)
    {
        try
        {
            return JsonElement.Parse(body, ParseOptions);
        }
        catch (JsonException exception)
        {
            throw new DrandFormatException(
                $"drand answered with malformed JSON: {exception.Message}",
                exception
            );
        }
    }

    /// <summary>
    /// Gets the factory that creates the HTTP client for each request.
    /// </summary>
    private IHttpClientFactory Clients { get; } = clients;

    /// <summary>
    /// Gets the options that name the base address and limit the size of a response.
    /// </summary>
    private DrandBeaconOptions Options { get; } = options.Value;

    /// <summary>
    /// Fetches the JSON document at <paramref name="path"/>, relative to the base address, or returns
    /// <see langword="null"/> when the API answers 404 or 425.
    /// </summary>
    /// <param name="path">The path to fetch, relative to the base address.</param>
    /// <param name="cancellationToken">The token that cancels the request and the read.</param>
    /// <returns>
    /// The root element of the document, or <see langword="null"/> when the API answers 404 or 425.
    /// </returns>
    /// <exception cref="HttpRequestException">The API answered a status other than success, 404 or 425.</exception>
    /// <exception cref="DrandFormatException">
    /// The response is not <c>application/json</c>, its body is larger than the limit, or it is not valid JSON.
    /// </exception>
    public async ValueTask<JsonElement?> GetAsync(string path, CancellationToken cancellationToken)
    {
        var client = Clients.CreateClient(DrandDefaults.HttpClientName);
        var address = new Uri(Options.BaseAddress, path);
        using var request = new HttpRequestMessage(HttpMethod.Get, address);
        request.Headers.Accept.ParseAdd(JsonMediaType);
        using var response = await client
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        if (response.StatusCode is HttpStatusCode.NotFound or TooEarly)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"drand answered {(int)response.StatusCode}.",
                null,
                response.StatusCode
            );
        }

        var actual = response.Content.Headers.ContentType?.MediaType;
        if (!string.Equals(actual, JsonMediaType, StringComparison.OrdinalIgnoreCase))
        {
            throw new DrandFormatException(
                $"drand answered with '{actual}', not '{JsonMediaType}'."
            );
        }

        var body = await ReadLimitedAsync(response.Content, cancellationToken)
            .ConfigureAwait(false);
        return ParseJson(body);
    }

    /// <summary>
    /// Reads a response body while it streams, refusing one larger than the configured limit.
    /// </summary>
    /// <param name="content">The response content.</param>
    /// <param name="cancellationToken">The token that cancels the read.</param>
    /// <returns>The whole body.</returns>
    /// <exception cref="DrandFormatException">The body is larger than <c>MaxResponseBytes</c>.</exception>
    private async ValueTask<byte[]> ReadLimitedAsync(
        HttpContent content,
        CancellationToken cancellationToken
    )
    {
        var limit = Options.MaxResponseBytes;
        if (content.Headers.ContentLength > limit)
        {
            throw new DrandFormatException($"drand answered with more than {limit} bytes.");
        }

        var stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            using var body = new MemoryStream();
            var buffer = ArrayPool<byte>.Shared.Rent(ChunkBytes);
            try
            {
                var chunk = buffer.AsMemory(0, ChunkBytes);
                int read;
                while (
                    (read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false))
                    > 0
                )
                {
                    if (body.Length + read > limit)
                    {
                        throw new DrandFormatException(
                            $"drand answered with more than {limit} bytes."
                        );
                    }

                    body.Write(buffer, 0, read);
                }

                return body.ToArray();
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }
}
