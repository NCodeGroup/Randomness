// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using System.Text.Json;
using NCode.Randomness.Curby.Twine;

namespace NCode.Randomness.Curby.Tests.TestTwine;

/// <summary>
/// Writes the IPLD data model as DAG-JSON, for building test documents.
/// </summary>
internal static class DagJsonWriter
{
    public static string Write(IpldNode node)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            Write(writer, node);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void Write(Utf8JsonWriter writer, IpldNode node)
    {
        switch (node)
        {
            case IpldMap map:
                writer.WriteStartObject();
                foreach (var (key, value) in map.Entries)
                {
                    writer.WritePropertyName(key);
                    Write(writer, value);
                }

                writer.WriteEndObject();
                break;
            case IpldList list:
                writer.WriteStartArray();
                foreach (var item in list.Items)
                {
                    Write(writer, item);
                }

                writer.WriteEndArray();
                break;
            case IpldString text:
                writer.WriteStringValue(text.Value);
                break;
            case IpldInteger integer:
                writer.WriteNumberValue(integer.Value);
                break;
            case IpldFloat number:
                writer.WriteNumberValue(number.Value);
                break;
            case IpldBytes bytes:
                writer.WriteStartObject();
                writer.WriteStartObject("/");
                writer.WriteString("bytes", Convert.ToBase64String(bytes.Value).TrimEnd('='));
                writer.WriteEndObject();
                writer.WriteEndObject();
                break;
            case IpldLink link:
                writer.WriteStartObject();
                writer.WriteString("/", link.Cid.ToString());
                writer.WriteEndObject();
                break;
            case IpldBoolean boolean:
                writer.WriteBooleanValue(boolean.Value);
                break;
            default:
                writer.WriteNullValue();
                break;
        }
    }
}
