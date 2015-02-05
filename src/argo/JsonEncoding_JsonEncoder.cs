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
    using Utilities;

    public partial class JsonEncoding
    {
        private class JsonEncoder
        {
            static JsonEncoder()
            {
                InitEncoders();
            }

            public void Encode(TextWriter writer, object value, Type type)
            {
                if (value == null)
                {
                    writer.Write("null");
                }
                else
                {
                    var encoder = GetEncoder(type);
                    encoder.Encode(this, writer, value);
                }
            }

            private void EncodeString<T>(TextWriter writer, T value)
            {
                var text = StringEncoding.Instance.Encode(value, typeof(T));
                writer.Write('"');
                writer.Write(text); // TODO: escapes for Json string.
                writer.Write('"');
            }

            private abstract class ValueEncoder
            {
                public abstract void Encode(JsonEncoder encoder, TextWriter writer, object value);
            }

            private abstract class ValueEncoder<T> : ValueEncoder
            {
                public abstract void EncodeTyped(JsonEncoder encoder, TextWriter writer, T value);

                public override void Encode(JsonEncoder encoder, TextWriter writer, object value)
                {
                    this.EncodeTyped(encoder, writer, (T)value);
                }
            }

            private class DictionaryEncoder<T, K, V> : ValueEncoder<T>
                where T : IEnumerable<KeyValuePair<K, V>>
            {
                private readonly ValueEncoder<V> valueEncoder;

                public DictionaryEncoder()
                {
                    this.valueEncoder = (ValueEncoder<V>)GetEncoder(typeof(V));
                }

                public override void EncodeTyped(JsonEncoder encoder, TextWriter writer, T pairs)
                {
                    if (pairs == null)
                    {
                        writer.Write("null");
                        return;
                    }

                    writer.Write("{");

                    bool first = true;
                    foreach (var kvp in pairs)
                    {
                        if (!first)
                        {
                            writer.Write(", ");
                        }
                        else
                        {
                            first = false;
                        }

                        encoder.EncodeString(writer, kvp.Key);
                        writer.Write(": ");
                        this.valueEncoder.EncodeTyped(encoder, writer, kvp.Value);
                    }

                    writer.Write("}");
                    return;
                }
            }

            private class EnumerableEncoder<T, TElem> : ValueEncoder<T>
                where T : IEnumerable<TElem>
            {
                private readonly ValueEncoder<TElem> elementEncoder;

                public EnumerableEncoder()
                {
                    this.elementEncoder = (ValueEncoder<TElem>)GetEncoder(typeof(TElem));
                }

                public override void EncodeTyped(JsonEncoder encoder, TextWriter writer, T sequence)
                {
                    if (sequence == null)
                    {
                        writer.Write("null");
                        return;
                    }

                    writer.Write("[");
                    bool first = true;

                    foreach (var item in sequence)
                    {
                        if (!first)
                        {
                            writer.Write(", ");
                        }
                        else
                        {
                            first = false;
                        }

                        this.elementEncoder.EncodeTyped(encoder, writer, item);
                    }

                    writer.Write("]");
                }
            }

            private class StringEncoder<T> : ValueEncoder<T>
            {
                public override void EncodeTyped(JsonEncoder encoder, TextWriter writer, T value)
                {
                    if (value == null)
                    {
                        writer.Write("null");
                    }
                    else
                    {
                        encoder.EncodeString(writer, value);
                    }
                }
            }

            private class ObjectEncoder<T> : ValueEncoder<T>
            {
                private readonly ValueEncoder<T>[] memberEncoders;

                public ObjectEncoder(IReadOnlyList<EncodingMember> members)
                {
                    this.memberEncoders = members.Select(m =>
                        (ValueEncoder<T>)Activator.CreateInstance(typeof(MemberEncoder<,>).MakeGenericType(typeof(T), m.Type), new object[] { m }))
                        .ToArray();
                }

                public override void EncodeTyped(JsonEncoder encoder, TextWriter writer, T value)
                {
                    if (value == null)
                    {
                        writer.Write("null");
                        return;
                    }

                    writer.Write("{");

                    bool first = true;

                    foreach (var memberEncoder in this.memberEncoders)
                    {
                        if (!first)
                        {
                            writer.Write(", ");
                        }
                        else
                        {
                            first = false;
                        }

                        memberEncoder.EncodeTyped(encoder, writer, value);
                    }

                    writer.Write("}");
                }
            }

            private class MemberEncoder<TInstance, TMember> : ValueEncoder<TInstance>
            {
                private readonly EncodingMember<TInstance, TMember> member;
                private readonly ValueEncoder<TMember> valueEncoder;

                public MemberEncoder(EncodingMember<TInstance, TMember> member)
                {
                    this.member = member;
                    this.valueEncoder = (ValueEncoder<TMember>)GetEncoder(typeof(TMember));
                }

                public override void EncodeTyped(JsonEncoder encoder, TextWriter writer, TInstance instance)
                {
                    encoder.EncodeString<string>(writer, member.Name);
                    writer.Write(": ");
                    this.valueEncoder.EncodeTyped(encoder, writer, this.member.GetTypedValue(instance));
                }
            }

            private class ActionEncoder<T> : ValueEncoder<T>
            {
                private readonly Action<TextWriter, T> action;

                public ActionEncoder(Action<TextWriter, T> action)
                {
                    this.action = action;
                }

                public override void EncodeTyped(JsonEncoder encoder, TextWriter writer, T value)
                {
                    this.action(writer, value);
                }
            }

            private static readonly ConcurrentDictionary<Type, ValueEncoder> valueEncoders =
                new ConcurrentDictionary<Type, ValueEncoder>();

            private static void InitEncoders()
            {
                valueEncoders.TryAdd(typeof(byte), new ActionEncoder<byte>((writer, value) => writer.Write(value)));
                valueEncoders.TryAdd(typeof(sbyte), new ActionEncoder<sbyte>((writer, value) => writer.Write(value)));
                valueEncoders.TryAdd(typeof(short), new ActionEncoder<short>((writer, value) => writer.Write(value)));
                valueEncoders.TryAdd(typeof(ushort), new ActionEncoder<ushort>((writer, value) => writer.Write(value)));
                valueEncoders.TryAdd(typeof(int), new ActionEncoder<int>((writer, value) => writer.Write(value)));
                valueEncoders.TryAdd(typeof(uint), new ActionEncoder<uint>((writer, value) => writer.Write(value)));
                valueEncoders.TryAdd(typeof(long), new ActionEncoder<long>((writer, value) => writer.Write(value)));
                valueEncoders.TryAdd(typeof(ulong), new ActionEncoder<ulong>((writer, value) => writer.Write(value)));
                valueEncoders.TryAdd(typeof(decimal), new ActionEncoder<decimal>((writer, value) => writer.Write(value)));
                valueEncoders.TryAdd(typeof(float), new ActionEncoder<float>((writer, value) => writer.Write(value)));
                valueEncoders.TryAdd(typeof(double), new ActionEncoder<double>((writer, value) => writer.Write(value)));
                valueEncoders.TryAdd(typeof(bool), new ActionEncoder<bool>((writer, value) => writer.Write(value ? "true" : "false")));
                valueEncoders.TryAdd(typeof(string), new ActionEncoder<string>((writer, value) => writer.Write(value)));
            }

            private static ValueEncoder GetEncoder(Type type)
            {
                ValueEncoder encoder;
                if (!valueEncoders.TryGetValue(type, out encoder))
                {
                    var tmp = CreateEncoder(type);

                    if (tmp != null)
                    {
                        encoder = valueEncoders.GetOrAdd(type, tmp);
                    }
                    else
                    {
                        throw new InvalidOperationException(string.Format("The type '{0}' cannot be encoded into JSON.", type));
                    }
                }

                return encoder;
            }

            private static object[] NoArgs = new object[0];

            private static ValueEncoder CreateEncoder(Type type)
            {
                if (StringEncoding.Instance.CanDecode(type))
                {
                    return (ValueEncoder)Activator.CreateInstance(typeof(StringEncoder<>).MakeGenericType(type), NoArgs);
                }

                Type keyType;
                Type valueType;
                if (TryGetDictionaryTypes(type, out keyType, out valueType))
                {
                    return (ValueEncoder)Activator.CreateInstance(typeof(DictionaryEncoder<,,>).MakeGenericType(type, keyType, valueType));
                }

                if (typeof(IEnumerable).IsAssignableFrom(type))
                {
                    var elementType = GetElementType(type);
                    return (ValueEncoder)Activator.CreateInstance(typeof(EnumerableEncoder<,>).MakeGenericType(type, elementType));
                }

                var members = GetEncodingMembers(type);
                if (members.Count > 0)
                {
                    return (ValueEncoder)Activator.CreateInstance(typeof(ObjectEncoder<>).MakeGenericType(type), new object[] { members });
                }

                return null;
            }
        }
    }
}