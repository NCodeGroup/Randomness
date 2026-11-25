// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
internal sealed class TwineApi(IHttpClientFactory clients, IOptions<CurbyBeaconOptions> options)
{
    private const string JsonMediaType = "application/json";
    private const string OctetStreamMediaType = "application/octet-stream";

    private static JsonDocumentOptions ParseOptions { get; } = new() { MaxDepth = 32 };

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
        return body is null ? null : TwineDocument.Parse(ParseJson(body));
    }

    /// <summary>
    /// Fetches the JSON array of documents at <paramref name="path"/>, relative to the base address, or returns an
    /// empty array when the API answers 404.
    /// </summary>
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

        var documents = ImmutableArray.CreateBuilder<TwineDocument>(json.GetArrayLength());
        foreach (var element in json.EnumerateArray())
        {
            documents.Add(TwineDocument.Parse(element));
        }

        return documents.MoveToImmutable();
    }

    /// <summary>
    /// Fetches the raw bytes at <paramref name="path"/>, relative to the base address, or returns
    /// <see langword="null"/> when the API answers 404.
    /// </summary>
    public ValueTask<byte[]?> GetBytesAsync(string path, CancellationToken cancellationToken) =>
        GetBodyAsync(path, OctetStreamMediaType, cancellationToken);

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

    private static JsonElement ParseJson(byte[] body)
    {
        try
        {
            return JsonElement.Parse(body, ParseOptions);
        }
        catch (JsonException exception)
        {
            throw new TwineFormatException(
                $"The Twine API answered with malformed JSON: {exception.Message}"
            );
        }
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
            var buffer = new byte[16 * 1024];
            int read;
            while (
                (read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0
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
    }
}
