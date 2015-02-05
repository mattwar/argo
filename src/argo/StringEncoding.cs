using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Argo
{
    /// <summary>
    /// Encodes types as value.ToString()  
    /// Decodes types using Type.Parse(string)
    /// </summary>
    public class StringEncoding : ObjectEncoding
    {
        public static readonly StringEncoding Instance = new StringEncoding();

        private StringEncoding()
        {
        }

        public override string Encode(object value, Type type)
        {
            return value.ToString();
        }

        public override void Encode(TextWriter writer, object value, Type type)
        {
            writer.Write(value);
        }

        public bool CanDecode(Type type)
        {
            return type.IsAssignableFrom(typeof(string)) || GetParser(type) != null;
        }

        public override object Decode(string value, Type type)
        {
            if (type.IsAssignableFrom(typeof(string)))
            {
                return value;
            }

            var parser = GetParser(type);
            if (parser != null)
            {
                return parser(value);
            }
            else
            {
                throw new InvalidOperationException(string.Format("The type '{0}' does not have a Parse method.", type));
            }
        }

        private static readonly ConcurrentDictionary<Type, Func<string, object>> typeParsers
            = new ConcurrentDictionary<Type, Func<string, object>>();

        private static Func<string, object> GetParser(Type type)
        {
            type = GetNonNullableType(type);

            Func<string, object> parser;
            if (!typeParsers.TryGetValue(type, out parser))
            {
                var parseMethod = type.GetMethods(BindingFlags.Static | BindingFlags.Public)
                    .FirstOrDefault(m => m.Name == "Parse" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(string));

                if (parseMethod != null)
                {
                    var dType = typeof(Func<,>).MakeGenericType(typeof(string), type);
                    var tmpParser = Delegate.CreateDelegate(dType, parseMethod, throwOnBindFailure: true);
                    var helper = (IParserHelper)Activator.CreateInstance(typeof(ParserHelper<>).MakeGenericType(type), new object[] { tmpParser });
                    parser = helper.Parse;
                }

                if (parser != null)
                {
                    parser = typeParsers.GetOrAdd(type, parser);
                }
            }

            return parser;
        }

        private interface IParserHelper
        {
            object Parse(string text);
        }

        private class ParserHelper<T> : IParserHelper
        {
            private readonly Func<string, T> parser;

            public ParserHelper(Func<string, T> parser)
            {
                this.parser = parser;
            }

            public object Parse(string text)
            {
                return this.parser(text);
            }
        }
    }
}
