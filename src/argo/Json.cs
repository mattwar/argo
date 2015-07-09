using System;
using System.Collections.Generic;
using System.IO;

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
            return JsonDecoder.Decode<T>(value);
        }

        public static object Decode(string value, Type type)
        {
            return JsonDecoder.Decode(value, type);
        }

        public static Dictionary<string, object> Decode(string value, IEnumerable<KeyValuePair<string, Type>> valueTypes)
        {
            return JsonDecoder.Decode(value, valueTypes);
        }
    }
}