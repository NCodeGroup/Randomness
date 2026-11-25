// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Collections.Immutable;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using NCode.Randomness.Curby.Twine;

namespace NCode.Randomness.Curby.Http;

/// <summary>
/// Reads Twine documents and raw bytes from the HTTP API: one expected media type per request, no redirects, and the
/// body read under a byte limit while it streams, since <c>Content-Length</c> is only a hint.
/// </summary>
// Copied on purpose into NistApi in NCode.Randomness.Nist and DrandApi in NCode.Randomness.Drand: independently
// versioned packages never share a type, so a fix to one copy belongs in the others.
internal sealed class TwineApi(IHttpClientFactory clients, IOptions<CurbyBeaconOptions> options)
{
    private const string JsonMediaType = "application/json";
    private const int ChunkBytes = 16 * 1024;
    private const string OctetStreamMediaType = "application/octet-stream";

    private static JsonDocumentOptions ParseOptions { get; } = new() { MaxDepth = 32 };

    /// <summary>
    /// Parses <paramref name="body"/> as JSON, accepting nesting up to 32 levels deep.
    /// </summary>
    /// <param name="body">The response body, as UTF-8 JSON.</param>
    /// <returns>The root element of the parsed document.</returns>
    /// <exception cref="TwineFormatException"><paramref name="body"/> is not valid JSON.</exception>
    private static JsonElement ParseJson(byte[] body)
    {
        try
        {
            return JsonElement.Parse(body, ParseOptions);
        }
        catch (JsonException exception)
        {
            throw new TwineFormatException(
                $"The Twine API answered with malformed JSON: {exception.Message}",
                exception
            );
        }
    }

    private IHttpClientFactory Clients { get; } = clients;

    private CurbyBeaconOptions Options { get; } = options.Value;

    /// <summary>
    /// Fetches the document at <paramref name="path"/>, relative to the base address, or returns <see langword="null"/>
    /// when the API answers 404.
    /// </summary>
    public async ValueTask<TwineDocument?> GetAsync(
        string path,
        CancellationToken cancellationToken
    )
    {
        var body = await GetBodyAsync(path, JsonMediaType, cancellationToken).ConfigureAwait(false);
        if (body is null)
        {
            return null;
        }

        var json = ParseJson(body);
        return TwineDocument.Parse(json);
    }

    /// <summary>
    /// Fetches the JSON array of documents at <paramref name="path"/>, relative to the base address, or returns an
    /// empty array when the API answers 404.
    /// </summary>
    /// <param name="path">The path to fetch, relative to the base address.</param>
    /// <param name="cancellationToken">The token that cancels the request and the read.</param>
    /// <returns>The documents of the array, or an empty array when the API answers 404.</returns>
    public async ValueTask<ImmutableArray<TwineDocument>> GetDocumentsAsync(
        string path,
        CancellationToken cancellationToken
    )
    {
        var body = await GetBodyAsync(path, JsonMediaType, cancellationToken).ConfigureAwait(false);
        if (body is null)
        {
            return [];
        }

        var json = ParseJson(body);
        if (json.ValueKind != JsonValueKind.Array)
        {
            throw new TwineFormatException(
                $"The Twine API answered with a JSON {json.ValueKind}, not an array."
            );
        }

        var count = json.GetArrayLength();
        var documents = ImmutableArray.CreateBuilder<TwineDocument>(count);
        foreach (var element in json.EnumerateArray())
        {
            var document = TwineDocument.Parse(element);
            documents.Add(document);
        }

        return documents.MoveToImmutable();
    }

    /// <summary>
    /// Fetches the raw bytes at <paramref name="path"/>, relative to the base address, or returns
    /// <see langword="null"/> when the API answers 404.
    /// </summary>
    /// <param name="path">The path to fetch, relative to the base address.</param>
    /// <param name="cancellationToken">The token that cancels the request and the read.</param>
    /// <returns>The response body, or <see langword="null"/> when the API answers 404.</returns>
    public ValueTask<byte[]?> GetBytesAsync(string path, CancellationToken cancellationToken) =>
        GetBodyAsync(path, OctetStreamMediaType, cancellationToken);

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
    /// <exception cref="TwineFormatException">
    /// The response has another media type than <paramref name="mediaType"/>, or its body is larger than the limit.
    /// </exception>
    private async ValueTask<byte[]?> GetBodyAsync(
        string path,
        string mediaType,
        CancellationToken cancellationToken
    )
    {
        var client = Clients.CreateClient(CurbyDefaults.HttpClientName);
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
                $"The Twine API answered {(int)response.StatusCode}.",
                null,
                response.StatusCode
            );
        }

        var actual = response.Content.Headers.ContentType?.MediaType;
        if (!string.Equals(actual, mediaType, StringComparison.OrdinalIgnoreCase))
        {
            throw new TwineFormatException(
                $"The Twine API answered with '{actual}', not '{mediaType}'."
            );
        }

        return await ReadLimitedAsync(response.Content, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<byte[]> ReadLimitedAsync(
        HttpContent content,
        CancellationToken cancellationToken
    )
    {
        var limit = Options.MaxResponseBytes;
        if (content.Headers.ContentLength > limit)
        {
            throw new TwineFormatException($"The Twine API answered with more than {limit} bytes.");
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
                        throw new TwineFormatException(
                            $"The Twine API answered with more than {limit} bytes."
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
