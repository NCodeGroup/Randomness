// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NCode.Randomness;
using NCode.Randomness.Curby;
using NCode.Randomness.Drand;
using NCode.Randomness.Nist;
using Recorded;

namespace ReadmeSamples;

// `dotnet mdsnippets` copies each sample between begin-snippet and end-snippet into the READMEs (ADR-1014).
public sealed class ReadmeSamplesTests
{
    [Fact]
    public void QuickStartSample_Runs()
    {
        string[] args = [];

        // begin-snippet: QuickStart
        var builder = Host.CreateApplicationBuilder(args);
        builder
            .Services.AddRandomness(
                builder.Configuration.GetSection(RandomnessOptions.SectionName)
            )
            .AddCurbyBeacon(
                builder.Configuration.GetSection(CurbyBeaconOptions.SectionName)
            )
            .AddNistBeacon(
                builder.Configuration.GetSection(NistBeaconOptions.SectionName)
            )
            .AddDrandBeacon(
                builder.Configuration.GetSection(DrandBeaconOptions.SectionName)
            );
        builder.Services.AddHealthChecks().AddRandomnessBeacons("ready");
        // end-snippet

        using var host = builder.Build();
        var names = host
            .Services.GetServices<IRandomnessBeacon>()
            .Select(beacon => beacon.Name);
        Assert.Equal(["curby", "nist", "drand"], names);
    }

    [Fact]
    public void TokenServiceSample_Runs()
    {
        var service = new TokenService(SystemRandomSource.Instance);

        var code = service.NewRecoveryCode();
        var die = service.RollDie();

        Assert.Equal(16, code.Length);
        Assert.All(code, c => Assert.Contains(c, "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"));
        Assert.InRange(die, 1, 6);
    }

    [Fact]
    public void FillKeySample_Runs()
    {
        var random = SystemRandomSource.Instance;
        byte[] message = [1, 2, 3, 4];
        var mac = new byte[32];

        // begin-snippet: FillKey
        Span<byte> key = stackalloc byte[32];
        try
        {
            random.Fill(key);
            HMACSHA256.HashData(key, message, mac);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
        // The key is all zeros again, and mac holds the code.
        // end-snippet

        Assert.Equal(-1, key.IndexOfAnyExcept((byte)0));
        Assert.NotEqual(-1, mac.AsSpan().IndexOfAnyExcept((byte)0));
    }

    [Fact]
    public void TypedValuesSample_Runs()
    {
        var random = SystemRandomSource.Instance;
        var deck = Enumerable.Range(1, 52).ToArray();

        // begin-snippet: TypedValues
        var roll = random.NextInteger(1, 7); // int in [1, 7)
        long offset = random.NextInteger(-1_000L, 1_000L); // long in [-1000, 1000)
        UInt128 id = random.NextInteger<UInt128>(); // all 128 bits
        double ratio = random.NextDouble(); // [0, 1), 53 random bits
        Guid token = random.NextGuid(); // RFC 9562 version 4
        random.Shuffle<int>(deck); // Fisher–Yates
        var color = random.GetItem<string>(["red", "green", "blue"]);
        Random legacy = random.AsRandom(); // for APIs that take a System.Random
        // end-snippet

        Assert.InRange(roll, 1, 6);
        Assert.InRange(offset, -1_000L, 999L);
        Assert.InRange(ratio, 0d, 1d);
        Assert.NotEqual(1d, ratio);
        Assert.Equal(4, token.Version);
        Assert.Equal(Enumerable.Range(1, 52), deck.Order());
        Assert.Contains(color, new[] { "red", "green", "blue" });
        Assert.InRange(legacy.Next(10), 0, 9);
        Assert.IsType<UInt128>(id);
    }

    [Fact]
    public async Task WithoutDependencyInjectionSample_Runs()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?> { ["Randomness:Mode"] = "Offline" }
            )
            .Build();
        var cancellationToken = CancellationToken.None;

        // begin-snippet: WithoutDependencyInjection
        var builder = RandomnessHost.CreateBuilder(
            configuration.GetSection(RandomnessOptions.SectionName)
        );
        builder.AddCurbyBeacon().AddNistBeacon().AddDrandBeacon();
        await using var randomness = await builder.BuildAsync(cancellationToken);

        Guid id = randomness.SecureRandom.NextGuid();
        // end-snippet

        Assert.Equal(4, id.Version);
        Assert.Equal(BeaconMode.Offline, randomness.BeaconStatus.Mode);
    }

    [Fact]
    public async Task PublicDrawSample_Runs()
    {
        var services = new ServiceCollection();
        services.AddRandomness().AddBeacon<RecordedCurbyBeacon>();
        await using var provider = services.BuildServiceProvider();
        var draws = provider.GetRequiredService<IVerifiableRandomFactory>();
        string[] entrants = ["Ada", "Grace", "Linus", "Margaret", "Tim"];
        var cancellationToken = CancellationToken.None;

        // begin-snippet: PublicDraw
        using var draw = await draws.CreateAsync(
            "curby",
            round: 1620423,
            context: "example-raffle-2026-11-25",
            cancellationToken
        );
        var winner = entrants[draw.NextInteger(0, entrants.Length)];
        // Publish draw.Pulse.Id with the result, so anyone can recompute it.
        // end-snippet

        Assert.Equal(
            "bafyriqbtxzd22eybdovubearwfjyzlyowyhxqdkbzyboftamh6rhob2jafopfz4h2nf3eec4nawu2gil2vpgn57qilrlh23piwyozlugpmcvc",
            draw.Pulse.Id
        );
        Assert.Equal("example-raffle-2026-11-25", draw.Context);
        using var recomputed = VerifiableRandom.Create(
            RecordedDocuments.CurbyPulse(1620423),
            "example-raffle-2026-11-25"
        );
        Assert.Equal(entrants[recomputed.NextInteger(0, entrants.Length)], winner);
    }
}
