// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Randomness.Generation;

namespace NCode.Randomness.Tests.Generation;

public class HmacDrbgTests
{
    public static TheoryData<HmacDrbgVector> Vectors { get; } = [.. HmacDrbgVectors.All];

    #region Generate Tests

    [Theory]
    [MemberData(nameof(Vectors))]
    public void Generate_WithCavpVector_ReturnsExpectedBits(HmacDrbgVector vector)
    {
        using var drbg = HmacDrbg.Instantiate(
            Convert.FromHexString(vector.Entropy),
            Convert.FromHexString(vector.Nonce),
            Convert.FromHexString(vector.Personalization)
        );
        if (vector.ReseedEntropy.Length > 0)
        {
            drbg.Reseed(
                Convert.FromHexString(vector.ReseedEntropy),
                Convert.FromHexString(vector.ReseedAdditional)
            );
        }

        var output = new byte[256];
        drbg.Generate(output, Convert.FromHexString(vector.Additional1));
        drbg.Generate(output, Convert.FromHexString(vector.Additional2));

        Assert.Equal(vector.Returned, Convert.ToHexStringLower(output));
    }

    [Fact]
    public void Generate_WhenRequestIsTooLarge_Throws()
    {
        using var drbg = CreateDrbg();
        var output = new byte[HmacDrbg.MaxBytesPerRequest + 1];

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => drbg.Generate(output, []));

        Assert.Equal("output", exception.ParamName);
    }

    [Fact]
    public void Generate_WhenRequestIsLargest_Succeeds()
    {
        using var drbg = CreateDrbg();
        var output = new byte[HmacDrbg.MaxBytesPerRequest];

        drbg.Generate(output, []);

        Assert.Contains(output, value => value != 0);
    }

    [Fact]
    public void Generate_AfterEachCall_CountsTheRequest()
    {
        using var drbg = CreateDrbg();

        drbg.Generate(new byte[8], []);
        drbg.Generate(new byte[8], []);

        Assert.Equal(3, drbg.ReseedCounter);
    }

    [Fact]
    public void Generate_WhenDisposed_Throws()
    {
        var drbg = CreateDrbg();
        drbg.Dispose();

        Assert.Throws<ObjectDisposedException>(() => drbg.Generate(new byte[8], []));
    }

    #endregion

    #region Instantiate Tests

    [Fact]
    public void Instantiate_WithTooLittleEntropy_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            HmacDrbg.Instantiate(new byte[31], new byte[16], [])
        );

        Assert.Equal("entropy", exception.ParamName);
    }

    [Fact]
    public void Instantiate_WithLongPersonalization_UsesItWhole()
    {
        var personalization = new byte[2048];
        using var first = HmacDrbg.Instantiate(new byte[32], new byte[16], personalization);
        personalization[^1] = 1;
        using var second = HmacDrbg.Instantiate(new byte[32], new byte[16], personalization);
        var a = new byte[32];
        var b = new byte[32];

        first.Generate(a, []);
        second.Generate(b, []);

        Assert.NotEqual(a, b);
    }

    #endregion

    #region Reseed Tests

    [Fact]
    public void Reseed_WithTooLittleEntropy_Throws()
    {
        using var drbg = CreateDrbg();

        var exception = Assert.Throws<ArgumentException>(() => drbg.Reseed(new byte[31], []));

        Assert.Equal("entropy", exception.ParamName);
    }

    [Fact]
    public void Reseed_Always_ResetsTheCounter()
    {
        using var drbg = CreateDrbg();
        drbg.Generate(new byte[8], []);

        drbg.Reseed(new byte[32], []);

        Assert.Equal(1, drbg.ReseedCounter);
    }

    [Fact]
    public void Reseed_WhenDisposed_Throws()
    {
        var drbg = CreateDrbg();
        drbg.Dispose();

        Assert.Throws<ObjectDisposedException>(() => drbg.Reseed(new byte[32], []));
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_WhenCalledTwice_DoesNotThrow()
    {
        var drbg = CreateDrbg();

        drbg.Dispose();
        drbg.Dispose();

        Assert.Throws<ObjectDisposedException>(() => drbg.Generate(new byte[1], []));
    }

    #endregion

    private static HmacDrbg CreateDrbg() => HmacDrbg.Instantiate(new byte[32], new byte[16], []);
}
