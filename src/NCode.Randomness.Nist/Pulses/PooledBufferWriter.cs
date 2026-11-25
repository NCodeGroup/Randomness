// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;

namespace NCode.Randomness.Nist.Pulses;

/// <summary>
/// Collects bytes in an array rented from <see cref="ArrayPool{T}.Shared"/>, renting a larger one as it fills, and
/// returns its array when disposed.
/// </summary>
/// <param name="initialCapacity">The number of bytes to rent at first.</param>
internal sealed class PooledBufferWriter(int initialCapacity) : IBufferWriter<byte>, IDisposable
{
    /// <summary>
    /// Gets or sets the rented array the bytes are written to.
    /// </summary>
    private byte[] Buffer { get; set; } = ArrayPool<byte>.Shared.Rent(initialCapacity);

    /// <summary>
    /// Gets or sets the number of bytes written.
    /// </summary>
    private int Written { get; set; }

    /// <summary>
    /// Gets or sets whether the array has been returned to the pool.
    /// </summary>
    private bool Disposed { get; set; }

    /// <summary>
    /// Gets the bytes written so far.
    /// </summary>
    public ReadOnlySpan<byte> WrittenSpan
    {
        get
        {
            ObjectDisposedException.ThrowIf(Disposed, this);
            return Buffer.AsSpan(0, Written);
        }
    }

    /// <inheritdoc/>
    public void Advance(int count)
    {
        ObjectDisposedException.ThrowIf(Disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, Buffer.Length - Written);
        Written += count;
    }

    /// <inheritdoc/>
    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        Reserve(sizeHint);
        return Buffer.AsMemory(Written);
    }

    /// <inheritdoc/>
    public Span<byte> GetSpan(int sizeHint = 0)
    {
        Reserve(sizeHint);
        return Buffer.AsSpan(Written);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Disposed)
        {
            return;
        }

        Disposed = true;
        ArrayPool<byte>.Shared.Return(Buffer);
        Buffer = [];
        Written = 0;
    }

    /// <summary>
    /// Makes room for at least <paramref name="sizeHint"/> more bytes, or one when it is zero, copying the written
    /// bytes into a larger rented array when the current one is too small.
    /// </summary>
    /// <param name="sizeHint">The number of bytes the caller is about to write, or zero when it does not know.</param>
    private void Reserve(int sizeHint)
    {
        ObjectDisposedException.ThrowIf(Disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(sizeHint);

        var needed = Math.Max(sizeHint, 1);
        if (Buffer.Length - Written >= needed)
        {
            return;
        }

        var size = Math.Max(Buffer.Length * 2, Written + needed);
        var larger = ArrayPool<byte>.Shared.Rent(size);
        Buffer.AsSpan(0, Written).CopyTo(larger);
        ArrayPool<byte>.Shared.Return(Buffer);
        Buffer = larger;
    }
}
