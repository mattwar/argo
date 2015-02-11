using System;
using System.IO;

namespace Argo
{
    public static partial class Json
    {
        public static string Encode<T>(T value)
        {
            var writer = new StringWriter();
            Encode(writer, value);
            return writer.ToString();
        }

        public static string Encode(object value, Type type)
        {
            var writer = new StringWriter();
            Encode(writer, value, type);
            return writer.ToString();
        }

        public static void Encode<T>(TextWriter writer, T value)
        {
            JsonEncoder.Instance.Encode(writer, value, typeof(T));
        }

        public static void Encode(TextWriter writer, object value, Type type)
        {
            type = type ?? value?.GetType() ?? typeof(object);
            JsonEncoder.Instance.Encode(writer, value, type);
        }

        public static T Decode<T>(string value)
        {
            int offset = 0;
            return JsonDecoder.Create().Decode<T>(value, ref offset);
        }

        public static object Decode(string value, Type type)
        {
            int offset = 0;
            return JsonDecoder.Create().Decode(value, ref offset, type);
        }
    }
}