using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Argo
{
    public abstract class ObjectEncoding
    {
        public abstract string Encode(object value, Type type = null);
        public abstract object Decode(string value, Type type);

        public virtual void Encode(TextWriter writer, object value, Type type = null)
        {
            writer.Write(this.Encode(value, type));
        }

        public virtual object Decode(TextReader reader, Type type)
        {
            return this.Decode(reader.ReadToEnd(), type);
        }

        public virtual void Encode(Stream stream, object value, Type type = null, System.Text.Encoding textEncoding = null)
        {
            var writer = new StreamWriter(stream, textEncoding ?? System.Text.Encoding.UTF8);
            this.Encode(writer, value, type);
            writer.Flush();
        }

        public virtual object Decode(Stream stream, Type type, System.Text.Encoding textEncoding = null)
        {
            var reader = new StreamReader(stream, textEncoding ?? System.Text.Encoding.UTF8);
            return this.Decode(reader, type);
        }

        protected static Type GetNonNullableType(Type type)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                return type.GetGenericArguments()[0];
            }

            return type;
        }

        private static readonly ConcurrentDictionary<Type, IReadOnlyList<EncodingMember>> serializableMembers
            = new ConcurrentDictionary<Type, IReadOnlyList<EncodingMember>>();

        protected static IReadOnlyList<EncodingMember> GetEncodingMembers(Type type)
        {
            IReadOnlyList<EncodingMember> members;
            if (!serializableMembers.TryGetValue(type, out members))
            {
                IReadOnlyList<EncodingMember> tmp = type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
                                                    .Where(m => m.MemberType == MemberTypes.Field || m.MemberType == MemberTypes.Property)
                                                    .Select(m => EncodingMember.Create(m)).ToList().AsReadOnly();

                members = serializableMembers.GetOrAdd(type, tmp);
            }

            return members;
        }

        protected static Type GetElementType(Type type)
        {
            if (type.IsArray)
            {
                return type.GetElementType();
            }
            else if (typeof(IEnumerable).IsAssignableFrom(type))
            {
                return type.GetInterfaces()
                    .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                    .Select(i => i.GetGenericArguments()[0]).FirstOrDefault()
                    ?? typeof(object);
            }
            else
            {
                return null;
            }
        }

        protected static bool TryGetDictionaryTypes(Type type, out Type keyType, out Type valueType)
        {
            foreach (var i in type.GetInterfaces())
            {
                if (i.IsGenericType)
                {
                    var gtd = i.GetGenericTypeDefinition();
                    if (gtd == typeof(IReadOnlyDictionary<,>) || gtd == typeof(IDictionary<,>))
                    {
                        var args = i.GetGenericArguments();
                        keyType = args[0];
                        valueType = args[1];
                        return true;
                    }
                }
            }

            if (typeof(IDictionary).IsAssignableFrom(type))
            {
                keyType = typeof(object);
                valueType = typeof(object);
                return true;
            }

            keyType = null;
            valueType = null;
            return false;
        }
    }
}
