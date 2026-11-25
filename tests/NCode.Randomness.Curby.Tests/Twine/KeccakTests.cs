// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using NCode.Randomness.Curby.Twine;

namespace NCode.Randomness.Curby.Tests.Twine;

public class KeccakTests
{
    // Each input is bytes (7i + 1) mod 256; each digest comes from Python's hashlib.sha3_512, an independent
    // implementation:
    // python3 -c "import hashlib; print(hashlib.sha3_512(bytes((i*7+1)%256 for i in range(n))).hexdigest())"
    public static TheoryData<int, string> Vectors { get; } =
        new()
        {
            {
                0,
                "a69f73cca23a9ac5c8b567dc185a756e97c982164fe25859e0d1dcc1475c80a615b2123af1f5f94c11e3e9402c3ac558f500199d95b6d3e301758586281dcd26"
            },
            {
                3,
                "533604b5c4c88ede41184e036389d2cb322d4165bc75adc0f9abd83610fbb39054326bbb506c50b74f5f814fca55bbd3d61eef6943ef52de9fc182b93bebf1d2"
            },
            {
                71,
                "9e4dee6367de5a2369552f7bc5984aa66c4ddec980d907592834fc194af9cc9965bcb79292faeedcc93471ecc5b3d33983beb5549e29126074ffccc59af809b8"
            },
            {
                72,
                "574f819c848e9d6f492f03847055180317740ce2f8473374e9c59255d9e2f5fc456eef18ed169f36b74e82a03e4164694c102389c0781483838c8840890c429e"
            },
            {
                73,
                "96836c50a2d824ff0eb228e5060e0b88efd30e10b366347e95ace6909bda65b23fa8d268ecf88ad5e53cc3492280389200015556d8c2ee3e3fbaa5bce3db678b"
            },
            {
                143,
                "0ed3f446670b5dcd674fcfa83450e70a8362bbf1cf16ec25e770a99c206df607d35f537b05d84fa677ab07a9b8f2d50c94d4f00afdc0ac7737383f0794f7473a"
            },
            {
                144,
                "ef8c5c8315608c36b8bea9ba9b62aa83066fa9643a8a15764cbb4c4056416f5f3bacfc7280c9ca8af12288bbe5988317e4ce707e9861c316036731630c58ca8a"
            },
            {
                200,
                "45418dbae0f40fc96a00351bc4feaa22764a6c20c90034f1bef2164b36d8f2e3d5ae8a1bad8ed82c090c88f5609d4deb27420b574d7ea5e75f62ab887699e0b0"
            },
            {
                1000,
                "5a2579e04d14f237802922f8cbe47bcbd98bf2124dcd79b83f9250d8b5f082da995f3d72f3288873628cef5df802b32505ff4c6fd06bd5ae3f29abdcf3c8f7b1"
            },
        };

    #region Sha3_512 Tests

    [Theory]
    [MemberData(nameof(Vectors))]
    public void Sha3_512_WithInput_MatchesTheReferenceDigest(int length, string expected)
    {
        var input = Input(length);
        var digest = new byte[64];

        Keccak.Sha3_512(input, digest);

        Assert.Equal(expected, Convert.ToHexStringLower(digest));
    }

    [Theory]
    [MemberData(nameof(Vectors))]
    public void HashData512_WithInput_MatchesTheReferenceDigest(int length, string expected)
    {
        var digest = Sha3.HashData512(Input(length));

        Assert.Equal(expected, Convert.ToHexStringLower(digest));
    }

    [Sha3PlatformFact]
    public void Sha3_512_WithRandomInputs_MatchesThePlatform()
    {
        var digest = new byte[64];
        for (var length = 0; length < 300; length += 13)
        {
            var input = RandomNumberGenerator.GetBytes(length);

            Keccak.Sha3_512(input, digest);

            Assert.Equal(SHA3_512.HashData(input), digest);
        }
    }

    [Fact]
    public void Sha3_512_WithSmallDestination_Throws()
    {
        Assert.Throws<ArgumentException>(() => Keccak.Sha3_512([], new byte[63]));
    }

    #endregion

    private static byte[] Input(int length) =>
        [.. Enumerable.Range(0, length).Select(i => (byte)(((i * 7) + 1) % 256))];
}
