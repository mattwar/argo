using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;

namespace Argo
{
    public partial class JsonEncoding : ObjectEncoding
    {
        public static readonly JsonEncoding Instance = new JsonEncoding();

        private JsonEncoding()
        {
        }

        public override string Encode(object value, Type type = null)
        {
            var writer = new StringWriter();
            this.Encode(writer, value, type);
            return writer.ToString();
        }

        public override void Encode(TextWriter writer, object value, Type type = null)
        {
            type = type ?? value?.GetType() ?? typeof(object);
            new JsonEncoder().Encode(writer, value, type);
        }

        public override object Decode(string value, Type type)
        {
            int offset = 0;
            return new JsonDecoder().Decode(value, ref offset, type);
        }

        public T Decode<T>(string value)
        {
            int offset = 0;
            return new JsonDecoder().Decode<T>(value, ref offset);
        }

        public override object Decode(TextReader reader, Type type)
        {
            var text = reader.ReadToEnd();
            return this.Decode(text, type);
        }
    }
}