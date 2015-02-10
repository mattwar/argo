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

    public static partial class Json
    {
        private class JsonEncoder
        {
            public static readonly JsonEncoder Instance = new JsonEncoder();

            private JsonEncoder()
            {
            }

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

            public void Encode<T>(TextWriter writer, T value)
            {
                var encoder = GetEncoder<T>();
                encoder.EncodeTyped(this, writer, value);
            }

            private readonly static char[] escapedChars =
                new char[] { '"', '/', '\\', '\b', '\f', '\n', '\r', '\t' };

            private void EncodeString<T>(TextWriter writer, T value)
            {
                var text = value.ToString();

                writer.Write('"');

                if (NeedsEscaping(text))
                {
                    EncodeEscapes(writer, text);
                }
                else
                {
                    writer.Write(text);
                }

                writer.Write('"');
            }

            private static bool NeedsEscaping(string text)
            {
                foreach (var ch in text)
                {
                    switch (ch)
                    {
                        case '"':
                        case '\\':
                            return true;
                        default:
                            return char.IsControl(ch);
                    }
                }

                return false;
            }

            private static void EncodeEscapes(TextWriter writer, string text)
            {
                foreach (var ch in text)
                {
                    switch (ch)
                    {
                        case '"':
                            writer.Write(@"\""");
                            break;
                        case '\\':
                            writer.Write(@"\\");
                            break;
                        case '\b':
                            writer.Write(@"\b");
                            break;
                        case '\f':
                            writer.Write(@"\f");
                            break;
                        case '\r':
                            writer.Write(@"\r");
                            break;
                        case '\n':
                            writer.Write(@"\n");
                            break;
                        case '\t':
                            writer.Write(@"\t");
                            break;
                        default:
                            if (char.IsControl(ch))
                            {
                                writer.Write(@"\u{0:x4}", (int)ch);
                            }
                            else
                            {
                                writer.Write(ch);
                            }
                            break;
                    }
                }
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
                    this.elementEncoder = GetEncoder<TElem>();
                }

                public override void EncodeTyped(JsonEncoder encoder, TextWriter writer, T sequence)
                {
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
                    encoder.EncodeString(writer, value);
                }
            }

            private class ObjectEncoder : ValueEncoder<object>
            {
                public override void EncodeTyped(JsonEncoder encoder, TextWriter writer, object value)
                {
                    var type = value.GetType();
                    if (type == typeof(object))
                    {
                        writer.Write("{}");
                    }
                    else
                    {
                        var typeEncoder = GetEncoder(type);
                        typeEncoder.Encode(encoder, writer, value);
                    }
                }
            }

            private class ObjectMemberEncoder<T> : ValueEncoder<T>
            {
                private readonly ValueEncoder<T>[] memberEncoders;

                public ObjectMemberEncoder(IReadOnlyList<EncodingMember> members)
                {
                    this.memberEncoders = members.Select(m =>
                        (ValueEncoder<T>)Activator.CreateInstance(typeof(MemberEncoder<,>).MakeGenericType(typeof(T), m.Type), new object[] { m }))
                        .ToArray();
                }

                public override void EncodeTyped(JsonEncoder encoder, TextWriter writer, T value)
                {
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
                    this.valueEncoder = GetEncoder<TMember>();
                }

                public override void EncodeTyped(JsonEncoder encoder, TextWriter writer, TInstance instance)
                {
                    encoder.EncodeString<string>(writer, member.Name);
                    writer.Write(": ");
                    this.valueEncoder.EncodeTyped(encoder, writer, this.member.GetTypedValue(ref instance));
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

            private class NullableEncoder<T> : ValueEncoder<T?> 
                where T : struct
            {
                private readonly ValueEncoder<T> valueEncoder;

                public NullableEncoder(ValueEncoder<T> valueEncoder)
                {
                    this.valueEncoder = valueEncoder;
                }

                public override void EncodeTyped(JsonEncoder encoder, TextWriter writer, T? value)
                {
                    if (value == null)
                    {
                        writer.Write("null");
                    }
                    else
                    {
                        this.valueEncoder.EncodeTyped(encoder, writer, value.GetValueOrDefault());
                    }
                }
            }

            private class NullEncoder<T> : ValueEncoder<T>
                where T : class
            {
                private readonly ValueEncoder<T> valueEncoder;

                public NullEncoder(ValueEncoder<T> valueEncoder)
                {
                    this.valueEncoder = valueEncoder;
                }

                public override void EncodeTyped(JsonEncoder encoder, TextWriter writer, T value)
                {
                    if (value == null)
                    {
                        writer.Write("null");
                    }
                    else
                    {
                        this.valueEncoder.EncodeTyped(encoder, writer, value);
                    }
                }
            }

            private static readonly ConcurrentDictionary<Type, ValueEncoder> valueEncoders =
                new ConcurrentDictionary<Type, ValueEncoder>();

            private static ValueEncoder<T> GetEncoder<T>()
            {
                return (ValueEncoder<T>)GetEncoder(typeof(T));
            }

            private static ValueEncoder GetEncoder(Type type)
            {
                ValueEncoder encoder;
                if (!valueEncoders.TryGetValue(type, out encoder))
                {
                    var tmp = CreateEncoder(type);
                    encoder = valueEncoders.GetOrAdd(type, tmp);
                }

                return encoder;
            }

            private static object[] NoArgs = new object[0];

            private static ValueEncoder CreateEncoder(Type type)
            {
                var nnType = TypeHelper.GetNonNullableType(type);
                var encoder = CreateTypeEncoder(nnType);

                if (nnType != type)
                {
                    encoder = (ValueEncoder)Activator.CreateInstance(typeof(NullableEncoder<>).MakeGenericType(nnType), new object[] { encoder });
                }
                else if (type.IsClass || type.IsInterface)
                {
                    encoder = (ValueEncoder)Activator.CreateInstance(typeof(NullEncoder<>).MakeGenericType(type), new object[] { encoder });
                }

                return encoder;
            }

            private static ValueEncoder CreateTypeEncoder(Type type)
            {
                // can be parsed from string?
                var parseMethod = type.GetMethods(BindingFlags.Static | BindingFlags.Public)
                    .FirstOrDefault(m => m.Name == "Parse" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(string));

                if (parseMethod != null)
                {
                    return (ValueEncoder)Activator.CreateInstance(typeof(StringEncoder<>).MakeGenericType(type), NoArgs);
                }

                Type keyType;
                Type valueType;
                if (TypeHelper.TryGetDictionaryTypes(type, out keyType, out valueType))
                {
                    return (ValueEncoder)Activator.CreateInstance(typeof(DictionaryEncoder<,,>).MakeGenericType(type, keyType, valueType));
                }

                if (typeof(IEnumerable).IsAssignableFrom(type))
                {
                    var elementType = TypeHelper.GetElementType(type);
                    return (ValueEncoder)Activator.CreateInstance(typeof(EnumerableEncoder<,>).MakeGenericType(type, elementType));
                }

                var members = EncodingMember.GetEncodingMembers(type);
                if (members.Count > 0)
                {
                    return (ValueEncoder)Activator.CreateInstance(typeof(ObjectMemberEncoder<>).MakeGenericType(type), new object[] { members });
                }

                throw new InvalidOperationException(string.Format("The type '{0}' cannot be encoded into JSON.", type));
            }

            private static void InitEncoders()
            {
                InitStructEncoder(new ActionEncoder<byte>((writer, value) => writer.Write(value)));
                InitStructEncoder(new ActionEncoder<sbyte>((writer, value) => writer.Write(value)));
                InitStructEncoder(new ActionEncoder<short>((writer, value) => writer.Write(value)));
                InitStructEncoder(new ActionEncoder<ushort>((writer, value) => writer.Write(value)));
                InitStructEncoder(new ActionEncoder<int>((writer, value) => writer.Write(value)));
                InitStructEncoder(new ActionEncoder<uint>((writer, value) => writer.Write(value)));
                InitStructEncoder(new ActionEncoder<long>((writer, value) => writer.Write(value)));
                InitStructEncoder(new ActionEncoder<ulong>((writer, value) => writer.Write(value)));
                InitStructEncoder(new ActionEncoder<decimal>((writer, value) => writer.Write(value.ToString(CultureInfo.InvariantCulture))));
                InitStructEncoder(new ActionEncoder<float>((writer, value) => writer.Write(value.ToString("R", CultureInfo.InvariantCulture))));
                InitStructEncoder(new ActionEncoder<double>((writer, value) => writer.Write(value.ToString("R", CultureInfo.InvariantCulture))));
                InitStructEncoder(new ActionEncoder<bool>((writer, value) => writer.Write(value ? "true" : "false")));

                InitClassEncoder(new StringEncoder<string>());
                InitClassEncoder(new ObjectEncoder());
            }

            private static void InitStructEncoder<T>(ValueEncoder<T> encoder) where T : struct
            {
                valueEncoders.TryAdd(typeof(T), encoder);
                valueEncoders.TryAdd(typeof(T?), new NullableEncoder<T>(encoder));
            }

            private static void InitClassEncoder<T>(ValueEncoder<T> encoder) where T : class
            {
                valueEncoders.TryAdd(typeof(T), new NullEncoder<T>(encoder));
            }
        }
    }
}