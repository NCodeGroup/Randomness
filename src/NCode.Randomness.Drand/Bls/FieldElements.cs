// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Numerics;
using Neo.Cryptography.BLS12_381;

namespace NCode.Randomness.Drand.Bls;

/// <summary>
/// Helpers for elements of the BLS12-381 base field, the arithmetic RFC 9380 section 4 builds on.
/// </summary>
internal static class FieldElements
{
    private const int ElementBytes = 48;

    /// <summary>
    /// Gets the prime modulus <c>p</c> of the base field.
    /// </summary>
    public static BigInteger Modulus { get; } =
        BigInteger.Parse(
            "01a0111ea397fe69a4b1ba7b6434bacd764774b84f38512bf6730d2a0f6b0f6241eabfffeb153ffffb9feffffffffaaab",
            NumberStyles.AllowHexSpecifier,
            CultureInfo.InvariantCulture
        );

    /// <summary>
    /// Gets the exponent <c>(p - 1) / 2</c> of Euler's criterion.
    /// </summary>
    private static BigInteger SquareExponent { get; } = (Modulus - 1) / 2;

    /// <summary>
    /// Creates a field element from big-endian hexadecimal digits.
    /// </summary>
    /// <param name="hex">The hexadecimal digits, at most 96 of them, of a value below the modulus.</param>
    /// <returns>The field element.</returns>
    public static Fp FromHex(string hex)
    {
        var padded = hex.PadLeft(ElementBytes * 2, '0');
        var bytes = Convert.FromHexString(padded);
        return Fp.FromBytes(bytes);
    }

    /// <summary>
    /// Creates a field element from an integer.
    /// </summary>
    /// <param name="value">The value, from zero up to but excluding the modulus.</param>
    /// <returns>The field element.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is negative or not below the modulus.</exception>
    public static Fp FromInteger(BigInteger value)
    {
        if (value.Sign < 0 || value >= Modulus)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "The value must be from zero up to but excluding the field's modulus."
            );
        }

        var bytes = new byte[ElementBytes];
        var byteCount = value.GetByteCount(isUnsigned: true);
        var destination = bytes.AsSpan(ElementBytes - byteCount);
        // Cannot fail: a value below the modulus fits in 48 bytes.
        value.TryWriteBytes(destination, out _, isUnsigned: true, isBigEndian: true);
        return Fp.FromBytes(bytes);
    }

    /// <summary>
    /// Converts a field element to an integer.
    /// </summary>
    /// <param name="value">The field element.</param>
    /// <returns>The integer, from zero up to but excluding the modulus.</returns>
    public static BigInteger ToInteger(Fp value)
    {
        var bytes = value.ToArray();
        return new BigInteger(bytes, isUnsigned: true, isBigEndian: true);
    }

    /// <summary>
    /// Reduces big-endian bytes modulo the field's prime.
    /// </summary>
    /// <param name="bytes">The big-endian bytes of an unsigned integer.</param>
    /// <returns>The field element.</returns>
    public static Fp Reduce(ReadOnlySpan<byte> bytes)
    {
        var integer = new BigInteger(bytes, isUnsigned: true, isBigEndian: true);
        var reduced = integer % Modulus;
        return FromInteger(reduced);
    }

    /// <summary>
    /// Determines whether a field element is a square, by Euler's criterion.
    /// </summary>
    /// <param name="value">The field element.</param>
    /// <returns><see langword="true"/> for zero or a quadratic residue; otherwise <see langword="false"/>.</returns>
    public static bool IsSquare(Fp value)
    {
        var integer = ToInteger(value);
        return integer.IsZero || BigInteger.ModPow(integer, SquareExponent, Modulus).IsOne;
    }

    /// <summary>
    /// Gets the sign of a field element, <c>sgn0</c> of RFC 9380 section 4.1.
    /// </summary>
    /// <param name="value">The field element.</param>
    /// <returns><see langword="true"/> when the element is odd; otherwise <see langword="false"/>.</returns>
    public static bool Sign(Fp value)
    {
        return (value.ToArray()[ElementBytes - 1] & 1) == 1;
    }
}
