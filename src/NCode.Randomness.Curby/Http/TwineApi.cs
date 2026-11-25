// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using NCode.Randomness.Curby.Twine;

namespace NCode.Randomness.Curby.Http;

/// <summary>
/// Reads Twine documents from the HTTP API: JSON only, no redirects, and the body read under a byte limit while it
/// streams, since <c>Content-Length</c> is only a hint.
/// </summary>
internal sealed class TwineApi(IHttpClientFactory clients, IOptions<CurbyBeaconOptions> options)
{
    private const string JsonMediaType = "application/json";

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
        var client = Clients.CreateClient(CurbyDefaults.HttpClientName);
        var address = new Uri(Options.BaseAddress, path);
        using var request = new HttpRequestMessage(HttpMethod.Get, address);
        request.Headers.Accept.ParseAdd(JsonMediaType);
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

        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (!string.Equals(mediaType, JsonMediaType, StringComparison.OrdinalIgnoreCase))
        {
            throw new TwineFormatException($"The Twine API answered with '{mediaType}', not JSON.");
        }

        var body = await ReadLimitedAsync(response.Content, cancellationToken)
            .ConfigureAwait(false);
        JsonElement json;
        try
        {
            json = JsonElement.Parse(body, ParseOptions);
        }
        catch (JsonException exception)
        {
            throw new TwineFormatException(
                $"The Twine API answered with malformed JSON: {exception.Message}"
            );
        }

        return TwineDocument.Parse(json);
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
