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

    // Each input is bytes (7i + 1) mod 256 of the first length; each output comes from Python's hashlib.shake_256, an
    // independent implementation:
    // python3 -c "import hashlib; print(hashlib.shake_256(bytes((i*7+1)%256 for i in range(n))).hexdigest(m))"
    public static TheoryData<int, int, string> ShakeVectors { get; } =
        new()
        {
            { 0, 32, "46b9dd2b0ba88d13233b3feb743eeb243fcd52ea62b81b82b50c27646ed5762f" },
            {
                3,
                64,
                "3409c8a9824031dd65dc94d5dd752018aab6accd4294e49b29515f005a66a380d18d04b012c826627b8b3a6967c9eb5e8e869b67f03adf44e25ebc99fbc67ae0"
            },
            {
                135,
                64,
                "d2fbe0a6ded494501cc37fd4f5da330b23e16601b12e4c37f332fdeb44311e6323db9fafa88327234ab271a50e5e9f55570595e8cec78296d0f3f4e2388d6391"
            },
            {
                136,
                64,
                "982c21d1d328ea0c182357958a9f776ca6a1811bf0f2c64b14262edef5d201c681a1883aa04988575f429adbb00859983e8f07e2047d6dd591730e57e1529c00"
            },
            {
                137,
                64,
                "aca65e9d4a27361044d570d20712ab5d6f8a43bfc0f9d4ef0b6f52ff7e6075271197657d0b9253be1731921fda2c480ea1b03b264fc9cf894be1da65890adc98"
            },
            {
                200,
                300,
                "2b693042fe67a83d2e039504933abd2044f4c863397be89bd1d6a60185b021947bdfa165aabbe10ec9aa174dc8f4f19237103652fdfade36214c4cdaa5ae76079edf94aa2f5767d81f5e47906eeadbf0b2942ea02841be4c059e1dbc35b4d8824ad8eeb70716bd313d258fad7b7a60a61fc4a030340b5235d00318dd4628d9dc5aa4126ae5d7058ca2ed29ca55452b7b59b87bd35493ebc6aefffa51d14befda5b0b81ceccda12ae0970486ea2d35f670897e00df8eefc1f7e7d72e4b6f2f2445ab93593471b283e6230430792132afcd6f4ace62b0ec5082f0ce3ae4848cafbdff071f5a36dd7561a57e5b81e5b9efb9937a560e47660187774847e72c8dc13cb35ee19cb4c33f19796aedd7a391b7a1d7fd6f0b365d14dc51e841225abd2fe963a28b23196ffb168552c6f"
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

    #region Shake256 Tests

    [Theory]
    [MemberData(nameof(ShakeVectors))]
    public void Shake256_WithInput_MatchesTheReferenceOutput(
        int length,
        int outputLength,
        string expected
    )
    {
        var output = new byte[outputLength];

        Keccak.Shake256(Input(length), output);

        Assert.Equal(expected, Convert.ToHexStringLower(output));
    }

    [Theory]
    [MemberData(nameof(ShakeVectors))]
    public void Sha3Shake256_WithInput_MatchesTheReferenceOutput(
        int length,
        int outputLength,
        string expected
    )
    {
        var output = new byte[outputLength];

        Sha3.Shake256(Input(length), output);

        Assert.Equal(expected, Convert.ToHexStringLower(output));
    }

    [Shake256PlatformFact]
    public void Shake256_WithRandomInputs_MatchesThePlatform()
    {
        for (var length = 0; length < 300; length += 13)
        {
            var input = RandomNumberGenerator.GetBytes(length);
            var output = new byte[(3 * length) + 1];

            Keccak.Shake256(input, output);

            Assert.Equal(Shake256.HashData(input, output.Length), output);
        }
    }

    #endregion

    private static byte[] Input(int length) =>
        [.. Enumerable.Range(0, length).Select(i => (byte)(((i * 7) + 1) % 256))];
}
