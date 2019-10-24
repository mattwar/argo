using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Argo
{
    public static partial class Json
    {
        public static string Encode<T>(T value)
        {
            return JsonEncoder.Encode(value);
        }

        public static string Encode(object value, Type type)
        {
            return JsonEncoder.Encode(value, type);
        }

        public static void Encode<T>(TextWriter writer, T value)
        {
            JsonEncoder.Encode(writer, value);
        }

        public static void Encode(TextWriter writer, object value, Type type)
        {
            JsonEncoder.Encode(writer, value, type);
        }

        public static T Decode<T>(string value)
        {
            var encoding = Encoding.UTF8;
            var encoder = encoding.GetEncoder();
            var len = encoder.GetByteCount(value.AsSpan(), flush: true);
            Span<byte> bytes = stackalloc byte[len];
            encoder.Convert(value.AsSpan(), bytes, flush: true, out var charsUsed, out var bytesUsed, out var completed);

            return Decode<T>(bytes, encoding);
        }

        public static T Decode<T>(ReadOnlySpan<byte> encodedText, Encoding encoding)
        {
            return JsonDecoder.Decode<T>(encodedText, encoding);
        }

        public static object Decode(string value, Type type)
        {
            var encoding = Encoding.UTF8;
            var encoder = encoding.GetEncoder();
            var len = encoder.GetByteCount(value.AsSpan(), flush: true);
            Span<byte> bytes = stackalloc byte[len];
            encoder.Convert(value.AsSpan(), bytes, flush: true, out var charsUsed, out var bytesUsed, out var completed);

            return Decode(bytes, encoding, type);
        }

        public static object Decode(ReadOnlySpan<byte> encodedText, Encoding encoding, Type type)
        {
            return JsonDecoder.Decode(encodedText, encoding, type);
        }

        public static Dictionary<string, object> Decode(string value, IEnumerable<KeyValuePair<string, Type>> valueTypes)
        {
            var encoding = Encoding.UTF8;
            var encoder = encoding.GetEncoder();
            var len = encoder.GetByteCount(value.AsSpan(), flush: true);
            Span<byte> bytes = stackalloc byte[len];
            encoder.Convert(value.AsSpan(), bytes, flush: true, out var charsUsed, out var bytesUsed, out var completed);

            return Decode(bytes, encoding, valueTypes);
        }

        public static Dictionary<string, object> Decode(ReadOnlySpan<byte> encodedText, Encoding encoding, IEnumerable<KeyValuePair<string, Type>> valueTypes)
        {
            return JsonDecoder.Decode(encodedText, encoding, valueTypes);
        }
    }
}