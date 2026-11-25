// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Randomness.Nist.Pulses;

namespace NCode.Randomness.Nist.Tests.Pulses;

public sealed class PooledBufferWriterTests
{
    #region GetSpan Tests

    [Fact]
    public void GetSpan_PastTheInitialCapacity_KeepsTheWrittenBytes()
    {
        using var writer = new PooledBufferWriter(16);
        var expected = new byte[100];
        for (var i = 0; i < expected.Length; i++)
        {
            expected[i] = (byte)i;
        }

        foreach (var value in expected)
        {
            var span = writer.GetSpan(1);
            span[0] = value;
            writer.Advance(1);
        }

        var actual = writer.WrittenSpan.ToArray();
        Assert.Equal(expected, actual);
    }

    #endregion

    #region GetMemory Tests

    [Fact]
    public void GetMemory_WithSizeHint_ReturnsAtLeastThatMany()
    {
        using var writer = new PooledBufferWriter(16);

        var memory = writer.GetMemory(100);

        Assert.True(memory.Length >= 100);
    }

    [Fact]
    public void GetMemory_WithNegativeSizeHint_Throws()
    {
        using var writer = new PooledBufferWriter(16);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => writer.GetMemory(-1));

        Assert.Equal("sizeHint", exception.ParamName);
    }

    #endregion

    #region Advance Tests

    [Fact]
    public void Advance_PastTheBuffer_Throws()
    {
        using var writer = new PooledBufferWriter(16);
        var span = writer.GetSpan();
        var past = span.Length + 1;

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => writer.Advance(past));

        Assert.Equal("count", exception.ParamName);
    }

    [Fact]
    public void Advance_WithNegativeCount_Throws()
    {
        using var writer = new PooledBufferWriter(16);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => writer.Advance(-1));

        Assert.Equal("count", exception.ParamName);
    }

    [Fact]
    public void Advance_AfterDispose_Throws()
    {
        var writer = new PooledBufferWriter(16);
        writer.Dispose();
        writer.Dispose();

        Assert.Throws<ObjectDisposedException>(() => writer.Advance(0));
    }

    #endregion
}
