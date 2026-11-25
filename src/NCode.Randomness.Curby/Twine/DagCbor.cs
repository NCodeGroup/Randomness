// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Buffers.Binary;
using System.Text;

namespace NCode.Randomness.Curby.Twine;

/// <summary>
/// Writes the IPLD data model as DAG-CBOR, the canonical encoding a CID hashes: definite lengths, the shortest
/// integer heads, floats as 64-bit IEEE 754 (0xfb), map keys sorted by encoded length and then bytewise, and links as
/// tag 42 over a byte string of 0x00 followed by the CID.
/// </summary>
internal static class DagCbor
{
    private const byte UnsignedMajor = 0;
    private const byte NegativeMajor = 1;
    private const byte BytesMajor = 2;
    private const byte TextMajor = 3;
    private const byte ArrayMajor = 4;
    private const byte MapMajor = 5;
    private const byte TagMajor = 6;
    private const ulong LinkTag = 42;

    private static UTF8Encoding Utf8 { get; } =
        new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public static byte[] Encode(IpldNode node)
    {
        var writer = new ArrayBufferWriter<byte>();
        Write(writer, node);
        return writer.WrittenSpan.ToArray();
    }

    private static void Write(ArrayBufferWriter<byte> writer, IpldNode node)
    {
        switch (node)
        {
            case IpldMap map:
                WriteMap(writer, map);
                break;
            case IpldList list:
                WriteHead(writer, ArrayMajor, (ulong)list.Items.Length);
                foreach (var item in list.Items)
                {
                    Write(writer, item);
                }

                break;
            case IpldString text:
                WriteText(writer, text.Value);
                break;
            case IpldInteger integer when integer.Value >= 0:
                WriteHead(writer, UnsignedMajor, (ulong)integer.Value);
                break;
            case IpldInteger integer:
                WriteHead(writer, NegativeMajor, (ulong)(-1 - integer.Value));
                break;
            case IpldBytes bytes:
                WriteBytes(writer, bytes.Value);
                break;
            case IpldLink link:
                WriteHead(writer, TagMajor, LinkTag);
                WriteHead(writer, BytesMajor, (ulong)link.Cid.Bytes.Length + 1);
                writer.Write<byte>([0x00]);
                writer.Write(link.Cid.Bytes);
                break;
            case IpldBoolean boolean:
                writer.Write<byte>([boolean.Value ? (byte)0xF5 : (byte)0xF4]);
                break;
            case IpldNull:
                writer.Write<byte>([0xF6]);
                break;
            case IpldFloat number:
                if (!double.IsFinite(number.Value))
                {
                    throw new TwineFormatException(
                        $"DAG-CBOR has no encoding for the float {number.Value}."
                    );
                }

                var span = writer.GetSpan(9);
                span[0] = 0xFB;
                BinaryPrimitives.WriteDoubleBigEndian(span[1..], number.Value);
                writer.Advance(9);
                break;
            default:
                throw new TwineFormatException($"{node.GetType().Name} has no DAG-CBOR encoding.");
        }
    }

    private static void WriteMap(ArrayBufferWriter<byte> writer, IpldMap map)
    {
        var keys = new (byte[] Key, IpldNode Value)[map.Entries.Length];
        for (var i = 0; i < keys.Length; i++)
        {
            keys[i] = (Utf8.GetBytes(map.Entries[i].Key), map.Entries[i].Value);
        }

        Array.Sort(keys, static (a, b) =>
            a.Key.Length != b.Key.Length
                ? a.Key.Length.CompareTo(b.Key.Length)
                : a.Key.AsSpan().SequenceCompareTo(b.Key)
        );
        WriteHead(writer, MapMajor, (ulong)keys.Length);
        foreach (var (key, value) in keys)
        {
            WriteHead(writer, TextMajor, (ulong)key.Length);
            writer.Write(key);
            Write(writer, value);
        }
    }

    private static void WriteText(ArrayBufferWriter<byte> writer, string value)
    {
        var bytes = Utf8.GetBytes(value);
        WriteHead(writer, TextMajor, (ulong)bytes.Length);
        writer.Write(bytes);
    }

    private static void WriteBytes(ArrayBufferWriter<byte> writer, ReadOnlySpan<byte> value)
    {
        WriteHead(writer, BytesMajor, (ulong)value.Length);
        writer.Write(value);
    }

    private static void WriteHead(ArrayBufferWriter<byte> writer, byte major, ulong value)
    {
        var initial = (byte)(major << 5);
        var head = writer.GetSpan(9);
        int length;
        if (value < 24)
        {
            head[0] = (byte)(initial | value);
            length = 1;
        }
        else if (value <= byte.MaxValue)
        {
            head[0] = (byte)(initial | 24);
            head[1] = (byte)value;
            length = 2;
        }
        else if (value <= ushort.MaxValue)
        {
            head[0] = (byte)(initial | 25);
            BinaryPrimitives.WriteUInt16BigEndian(head[1..], (ushort)value);
            length = 3;
        }
        else if (value <= uint.MaxValue)
        {
            head[0] = (byte)(initial | 26);
            BinaryPrimitives.WriteUInt32BigEndian(head[1..], (uint)value);
            length = 5;
        }
        else
        {
            head[0] = (byte)(initial | 27);
            BinaryPrimitives.WriteUInt64BigEndian(head[1..], value);
            length = 9;
        }

        writer.Advance(length);
    }
}
