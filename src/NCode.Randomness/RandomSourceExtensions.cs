// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Numerics;
using NCode.Randomness.Sampling;

namespace NCode.Randomness;

/// <summary>
/// Typed values, ranges, shuffles and selections over any <see cref="IRandomSource"/>.
/// </summary>
/// <remarks>
/// Every member is unbiased: integer ranges use rejection sampling, never a modulo, and floating-point values take
/// their mantissa bits straight from the source. The members are deterministic given the source's bytes, which is
/// what makes a <see cref="VerifiableRandom"/> draw reproducible.
/// </remarks>
public static class RandomSourceExtensions
{
    extension(IRandomSource source)
    {
        /// <summary>
        /// Returns a new array of <paramref name="count"/> random bytes.
        /// </summary>
        /// <param name="count">The number of bytes.</param>
        /// <returns>The bytes.</returns>
        /// <remarks>
        /// The array is neither pinned nor cleared, so the garbage collector can leave copies of it in memory. For a
        /// key or other secret, call <see cref="IRandomSource.Fill(Span{byte})"/> with a <see langword="stackalloc"/>
        /// or pinned span, and clear it with
        /// <see cref="System.Security.Cryptography.CryptographicOperations.ZeroMemory(Span{byte})"/> after use.
        /// </remarks>
        public byte[] GetBytes(int count)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentOutOfRangeException.ThrowIfNegative(count);
            var bytes = GC.AllocateUninitializedArray<byte>(count);
            source.Fill(bytes);
            return bytes;
        }

        /// <summary>
        /// Returns an integer drawn uniformly from every value of <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">A fixed-size integer type, from <see cref="byte"/> to <see cref="UInt128"/>.</typeparam>
        /// <returns>The integer.</returns>
        public T NextInteger<T>()
            where T : IBinaryInteger<T>, IMinMaxValue<T>
        {
            ArgumentNullException.ThrowIfNull(source);
            return UniformSampler.NextInteger<T>(source);
        }

        /// <summary>
        /// Returns an integer drawn uniformly from zero up to, but excluding, <paramref name="maxExclusive"/>.
        /// </summary>
        /// <typeparam name="T">A fixed-size integer type, from <see cref="byte"/> to <see cref="UInt128"/>.</typeparam>
        /// <param name="maxExclusive">The exclusive upper bound; it must be greater than zero.</param>
        /// <returns>The integer.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxExclusive"/> is not greater than zero.</exception>
        public T NextInteger<T>(T maxExclusive)
            where T : IBinaryInteger<T>, IMinMaxValue<T>
        {
            ArgumentNullException.ThrowIfNull(source);
            return UniformSampler.NextInteger(source, T.Zero, maxExclusive);
        }

        /// <summary>
        /// Returns an integer drawn uniformly from <paramref name="minInclusive"/> up to, but excluding,
        /// <paramref name="maxExclusive"/>. A die roll is <c>NextInteger(1, 7)</c>.
        /// </summary>
        /// <typeparam name="T">A fixed-size integer type, from <see cref="byte"/> to <see cref="UInt128"/>.</typeparam>
        /// <param name="minInclusive">The inclusive lower bound.</param>
        /// <param name="maxExclusive">The exclusive upper bound; it must be greater than <paramref name="minInclusive"/>.</param>
        /// <returns>The integer.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The range is empty.</exception>
        public T NextInteger<T>(T minInclusive, T maxExclusive)
            where T : IBinaryInteger<T>, IMinMaxValue<T>
        {
            ArgumentNullException.ThrowIfNull(source);
            return UniformSampler.NextInteger(source, minInclusive, maxExclusive);
        }

        /// <summary>
        /// Returns <see langword="true"/> or <see langword="false"/> with equal probability.
        /// </summary>
        /// <returns>The value.</returns>
        public bool NextBoolean()
        {
            ArgumentNullException.ThrowIfNull(source);
            return UniformSampler.NextBoolean(source);
        }

        /// <summary>
        /// Returns a <see cref="double"/> drawn uniformly from [0, 1), with all 53 bits of its mantissa random.
        /// </summary>
        /// <returns>The value.</returns>
        public double NextDouble()
        {
            ArgumentNullException.ThrowIfNull(source);
            return UniformSampler.NextDouble(source);
        }

        /// <summary>
        /// Returns a <see cref="float"/> drawn uniformly from [0, 1), with all 24 bits of its mantissa random.
        /// </summary>
        /// <returns>The value.</returns>
        public float NextSingle()
        {
            ArgumentNullException.ThrowIfNull(source);
            return UniformSampler.NextSingle(source);
        }

        /// <summary>
        /// Returns a random RFC 9562 version 4 <see cref="Guid"/>, with 122 random bits.
        /// </summary>
        /// <returns>The identifier.</returns>
        public Guid NextGuid()
        {
            ArgumentNullException.ThrowIfNull(source);
            return UniformSampler.NextGuid(source);
        }

        /// <summary>
        /// Shuffles <paramref name="values"/> in place with the Fisher–Yates algorithm, so every order is equally likely.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="values">The values to shuffle.</param>
        public void Shuffle<T>(Span<T> values)
        {
            ArgumentNullException.ThrowIfNull(source);
            UniformSampler.Shuffle(source, values);
        }

        /// <summary>
        /// Returns one of <paramref name="choices"/>, each equally likely.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="choices">The values to choose from; there must be at least one.</param>
        /// <returns>The chosen value.</returns>
        /// <exception cref="ArgumentException"><paramref name="choices"/> is empty.</exception>
        public T GetItem<T>(ReadOnlySpan<T> choices)
        {
            ArgumentNullException.ThrowIfNull(source);
            return UniformSampler.GetItem(source, choices);
        }

        /// <summary>
        /// Fills <paramref name="destination"/> with values chosen independently from <paramref name="choices"/>, each
        /// equally likely, with repetition.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="choices">The values to choose from; there must be at least one.</param>
        /// <param name="destination">The buffer to fill.</param>
        /// <exception cref="ArgumentException"><paramref name="choices"/> is empty.</exception>
        public void GetItems<T>(ReadOnlySpan<T> choices, Span<T> destination)
        {
            ArgumentNullException.ThrowIfNull(source);
            UniformSampler.GetItems(source, choices, destination);
        }

        /// <summary>
        /// Returns a string of <paramref name="length"/> characters chosen independently from
        /// <paramref name="alphabet"/>, such as a password or a recovery code.
        /// </summary>
        /// <param name="alphabet">The characters to choose from: at least one, each distinct, since a repeated
        /// character would be chosen more often.</param>
        /// <param name="length">The number of characters.</param>
        /// <returns>The string.</returns>
        /// <exception cref="ArgumentException"><paramref name="alphabet"/> is empty or repeats a character.</exception>
        public string GetString(string alphabet, int length)
        {
            ArgumentNullException.ThrowIfNull(source);
            return UniformSampler.GetString(source, alphabet, length);
        }

        /// <summary>
        /// Returns a <see cref="Random"/> that draws every value from this source, for an API that takes one, such as
        /// <see cref="Random.Shuffle{T}(T[])"/>. It is as secret, and as thread-safe, as the source.
        /// </summary>
        /// <returns>The adapter.</returns>
        public Random AsRandom()
        {
            ArgumentNullException.ThrowIfNull(source);
            return new RandomAdapter(source);
        }
    }
}
