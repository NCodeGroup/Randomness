// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Randomness.Curby.Tests.Fixtures;

/// <summary>
/// The saved CURBy-Q documents described in Fixtures/README.md.
/// </summary>
internal static class CurbyQuantumFixtures
{
    public const string ChainId =
        "bafyriqhijw5soalbtppuwwjbskfiriqy6swa5qd5trgbzx4nypnu7mp7iyhx7m2iin2nxnglmngvjgirzb7bvlisxq3uygoik7ozbybxnftiw";
    public const string BellChainId =
        "bafyriqe3zxf5g4ifgqhea5zozxpdcfi5qcpkfpogtxzbizmmuxdjuzuq44a2cifbr7xplo4kcfsdz2c5pxfxektavrqxxb3nvbmclxz7qiz6e";
    public const string SeedChainId =
        "bafyriqa5k2d3t3r774geicueaed2wc2fosjwqeexfhwbptfgq7rcn5mwucnhfeuxu2nxbrch3rl6yqjlozhuswo5ln3xwjm35iftt3tpqlcgs";
    public const long Round = 28297;
    public const long FirstSeedPulseIndex = 1914066;
    public const long SecondSeedPulseIndex = 1914067;
    public const long ThirdSeedPulseIndex = 1914068;
    public const string RequestId =
        "bafyriqdxut7znfobli3gwij6iu2yod5ye2bgmgfzvjzyue7sojfc2oquut67i3mazb7abhocg3kdbgm2sxxgnx4gt7wqqtduwy2vch25bsxbw";
    public const string PrecommitId =
        "bafyriqb6ms4rpm7s4xaahc5xpx5qa2mcrtvdiwzbaffojtm2iwe4t5oxr4y4mgqj5xoig5gw7z3547cna5tano3zz3yq4wlxqidptr347x35e";
    public const string ResultId =
        "bafyriqgv3jrmyy5tezzsyh5r5kpswm274ofbz655xwpu4u4szkwnxk3mgtlq7l7zaupcbgknnyhanjsk42smv7tmnrrbdxmqqygscbyc7jwno";
    public const string BellResponseId =
        "bafyriqabhxmvznu4klz3bugl5hjqjq535f75fbum7ktsqphshkt3kkxhy4xyczhkaecesckmasz7zknriruyjsuv4mmux3mfmzyznr6ofozv6";
    public const string SeedPulseId =
        "bafyriqbyxrealvythz7ardbm5h76at63xsomydbc57ibaueqy3aqh7lsrnylenejk2twmv2eb3tsageclwb5wrlwqynhbvtuqob2r2bqpyvtm";
    public const string Randomness =
        "61c307b2dcedea8f5ce9a3c68e387f78df126d4a6a45924aa86457c49885966891ba507e0c9b4f9069ecbb4a9e85f925c3c1dbb84b7478909e916c3911de42a9";

    public static DateTimeOffset Timestamp { get; } =
        new(2025, 8, 22, 19, 42, 38, 156, TimeSpan.Zero);

    public static string Chain => Read("chain.json");

    public static string BellChain => Read("bell-chain.json");

    public static string SeedChain => Read("seed-chain.json");

    public static string RoundJson => Read($"round-{Round}.json");

    public static string Parameters => Read($"round-{Round}-params.json");

    public static string BellPulse(long index) => Read($"bell-pulse-{index}.json");

    public static string SeedPulse(long index) => Read($"seed-pulse-{index}.json");

    private static string Read(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "CurbyQ", name);
        return File.ReadAllText(path);
    }
}
