// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Neo.Cryptography.BLS12_381;

namespace NCode.Randomness.Drand.Bls;

/// <summary>
/// Hashes to the BLS12-381 G1 group as RFC 9380 section 8.8.1 specifies.
/// </summary>
internal static class G1HashToCurve
{
    /// <summary>
    /// Gets the coefficient <c>A</c> of the isogenous curve <c>E'</c>.
    /// </summary>
    private static Fp A { get; } =
        FieldElements.FromHex(
            "144698a3b8e9433d693a02c96d4982b0ea985383ee66a8d8e8981aefd881ac98936f8da0e0f97f5cf428082d584c1d"
        );

    /// <summary>
    /// Gets the coefficient <c>B</c> of the isogenous curve <c>E'</c>.
    /// </summary>
    private static Fp B { get; } =
        FieldElements.FromHex(
            "12e2908d11688030018b12e8753eee3b2016c1f0f24f4070a0b9c14fcef35ef55a23215a316ceaa5d1cc48e98e172be0"
        );

    /// <summary>
    /// Gets the non-square <c>Z</c> of the simplified SWU map.
    /// </summary>
    private static Fp Z { get; } = FieldElements.FromInteger(11);

    /// <summary>
    /// Hashes a message to two field elements, RFC 9380 section 5.2 with <c>count</c> 2 and <c>L</c> 64.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="domain">The domain separation tag.</param>
    /// <returns>The two field elements.</returns>
    public static Fp[] HashToField(ReadOnlySpan<byte> message, ReadOnlySpan<byte> domain)
    {
        Span<byte> bytes = stackalloc byte[128];
        ExpandMessageXmd.Expand(message, domain, bytes);
        var first = bytes[..64];
        var second = bytes[64..];
        var firstElement = FieldElements.Reduce(first);
        var secondElement = FieldElements.Reduce(second);
        return [firstElement, secondElement];
    }

    /// <summary>
    /// Hashes a message to a point of the BLS12-381 G1 group, RFC 9380 section 3.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="domain">The domain separation tag.</param>
    /// <returns>The point in the G1 subgroup.</returns>
    public static G1Affine Hash(ReadOnlySpan<byte> message, ReadOnlySpan<byte> domain)
    {
        var u = HashToField(message, domain);
        var q0 = MapToCurve(u[0]);
        var q1 = MapToCurve(u[1]);
        var sum = new G1Projective(q0) + q1;
        var cleared = sum.ClearCofactor();
        return new G1Affine(cleared);
    }

    /// <summary>
    /// Maps a field element to a point of the BLS12-381 curve, by the simplified SWU map on <c>E'</c> and the
    /// 11-isogeny.
    /// </summary>
    /// <param name="u">The field element.</param>
    /// <returns>The point on the curve, not yet in the G1 subgroup.</returns>
    public static G1Affine MapToCurve(Fp u)
    {
        var zu2 = Z * u.Square();
        var tv1 = zu2.Square() + zu2;
        var x1 = tv1.IsZero ? B * (Z * A).Invert() : -B * A.Invert() * (Fp.One + tv1.Invert());

        var gx1 = Curve(x1);
        Fp x;
        Fp y;
        if (FieldElements.IsSquare(gx1))
        {
            x = x1;
            y = gx1.Sqrt();
        }
        else
        {
            x = zu2 * x1;
            y = Curve(x).Sqrt();
        }

        if (FieldElements.Sign(u) != FieldElements.Sign(y))
        {
            y = -y;
        }

        return G1Isogeny.Map(x, y);
    }

    private static Fp Curve(Fp x)
    {
        return x.Square() * x + A * x + B;
    }
}
