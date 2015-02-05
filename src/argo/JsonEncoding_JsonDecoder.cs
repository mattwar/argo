using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Serialization;

namespace Argo
{
    using Utilities;

    public partial class JsonEncoding
    {
        private class JsonDecoder
        {
            private readonly StringTable strings = new StringTable();

            public JsonDecoder()
            {
            }

            public object Decode(string text, ref int offset, Type type)
            {
                var decoder = GetDecoder(type);
                return decoder.Decode(this, text, ref offset);
            }

            public T Decode<T>(string text, ref int offset)
            {
                var decoder = GetDecoder<T>();
                return decoder.DecodeTyped(this, text, ref offset);
            }

            private object DecodeObject(string text, ref int offset)
            {
                if (TryConsumeToken(text, ref offset, "null"))
                {
                    return null;
                }
                else if (TryConsumeToken(text, ref offset, "true"))
                {
                    return true;
                }
                else if (TryConsumeToken(text, ref offset, "false"))
                {
                    return false;
                }
                else if (IsToken(text, offset, '"'))
                {
                    return DecodeString(text, ref offset, typeof(string));
                }
                else if (IsToken(text, offset, '['))
                {
                    return GetDecoder<List<object>>().DecodeTyped(this, text, ref offset);
                }
                else if (IsToken(text, offset, '{'))
                {
                    return GetDecoder<Dictionary<string, object>>().DecodeTyped(this, text, ref offset);
                }
                else
                {
                    double dbl;
                    decimal dec;
                    var kind = DecodeNumber(text, ref offset, typeof(object), out dbl, out dec);
                    if (kind == NumberKind.Double)
                    {
                        return dbl;
                    }
                    else
                    {
                        return dec;
                    }
                }
            }

            private object DecodeString(string text, ref int offset, Type type)
            {
                ConsumeToken(text, ref offset, '"');

                var start = offset;

                while (offset < text.Length && PeekChar(text, offset) != '"')
                {
                    offset++;
                }

                var end = offset;

                ConsumeToken(text, ref offset, '"');

                var value = this.strings.GetOrAdd(text, start, end - start);

                if (type == typeof(string) || type == typeof(object))
                {
                    return value;
                }
                else
                {
                    return StringEncoding.Instance.Decode(value, type);
                }
            }

            private enum NumberKind
            {
                Double,
                Decimal
            }

            private NumberKind DecodeNumber(string text, ref int offset, Type type, out double dbl, out decimal dec)
            {
                dbl = 0;
                dec = 0;

                SkipWhitespace(text, ref offset);

                var start = offset;

                if (PeekChar(text, offset) == '-')
                {
                    offset++;
                }

                while (Char.IsNumber(PeekChar(text, offset)))
                {
                    offset++;
                }

                bool hasFraction = false;
                if (PeekChar(text, offset) == '.')
                {
                    offset++;
                    hasFraction = true;

                    while (Char.IsNumber(PeekChar(text, offset)))
                    {
                        offset++;
                    }
                }

                if (PeekChar(text, offset) == 'e' || PeekChar(text, offset) == 'E')
                {
                    offset++;

                    if (PeekChar(text, offset) == '+' || PeekChar(text, offset) == '-')
                    {
                        offset++;
                    }

                    while (Char.IsNumber(PeekChar(text, offset)))
                    {
                        offset++;
                    }
                }

                var number = this.strings.GetOrAdd(text, start, offset - start);

                if (type == typeof(float) || type == typeof(double) || (type == typeof(object) && hasFraction))
                {
                    if (double.TryParse(number, NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out dbl))
                    {
                        return NumberKind.Double;
                    }
                }

                if (decimal.TryParse(number, NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out dec))
                {
                    return NumberKind.Decimal;
                }

                throw new InvalidOperationException("The value is not a legal number");
            }

            private static void SkipWhitespace(string text, ref int offset)
            {
                while (offset < text.Length && Char.IsWhiteSpace(text[offset]))
                {
                    offset++;
                }
            }

            private static char PeekChar(string text, int offset)
            {
                if (offset < text.Length)
                {
                    return text[offset];
                }

                return '\0';
            }

            private static bool IsToken(string text, int offset, char token)
            {
                SkipWhitespace(text, ref offset);

                if (offset < text.Length)
                {
                    return text[offset] == token;
                }

                return false;
            }

            private static bool IsToken(string text, int offset, string token)
            {
                SkipWhitespace(text, ref offset);

                if (offset + token.Length < text.Length)
                {
                    return string.CompareOrdinal(text, offset, token, 0, token.Length) == 0;
                }

                return false;
            }

            private static void ConsumeToken(string text, ref int offset, char token)
            {
                if (!TryConsumeToken(text, ref offset, token))
                {
                    throw new InvalidOperationException(string.Format("Expected token '{0}' not found at offset {1}", token, offset));
                }
            }

            private static void ConsumeToken(string text, ref int offset, string token)
            {
                if (!TryConsumeToken(text, ref offset, token))
                {
                    throw new InvalidOperationException(string.Format("Expected token '{0}' not found at offset {1}", token, offset));
                }
            }

            private static bool TryConsumeToken(string text, ref int offset, char token)
            {
                SkipWhitespace(text, ref offset);

                if (IsToken(text, offset, token))
                {
                    offset += 1;
                    return true;
                }

                return false;
            }

            private static bool TryConsumeToken(string text, ref int offset, string token)
            {
                SkipWhitespace(text, ref offset);

                if (IsToken(text, offset, token))
                {
                    offset += token.Length;
                    return true;
                }

                return false;
            }

            private abstract class ValueDecoder
            {
                public abstract object Decode(JsonDecoder decoder, string text, ref int offset);
            }

            private abstract class ValueDecoder<T> : ValueDecoder
            {
                public abstract T DecodeTyped(JsonDecoder decoder, string text, ref int offset);

                public override object Decode(JsonDecoder decoder, string text, ref int offset)
                {
                    return this.DecodeTyped(decoder, text, ref offset);
                }
            }

            private class ArrayDecoder<TElement> : ValueDecoder<TElement[]>
            {
                private readonly ValueDecoder<TElement> elementDecoder;

                internal static readonly ObjectPool<List<TElement>> listPool = new ObjectPool<List<TElement>>(() => new List<TElement>(), list => list.Clear());

                public ArrayDecoder()
                {
                    this.elementDecoder = GetDecoder<TElement>();
                }

                public override TElement[] DecodeTyped(JsonDecoder decoder, string text, ref int offset)
                {
                    if (TryConsumeToken(text, ref offset, "null"))
                    {
                        return null;
                    }

                    var list = listPool.AllocateFromPool();
                    try
                    {
                        ConsumeToken(text, ref offset, '[');

                        while (PeekChar(text, offset) != '\0')
                        {
                            list.Add(this.elementDecoder.DecodeTyped(decoder, text, ref offset));

                            if (!TryConsumeToken(text, ref offset, ','))
                            {
                                break;
                            }
                        }

                        ConsumeToken(text, ref offset, ']');

                        var array = new TElement[list.Count];
                        list.CopyTo(array);
                        return array;
                    }
                    finally
                    {
                        listPool.ReturnToPool(list);
                    }
                }
            }

            private class ListAddDecoder<T, TElement> : ValueDecoder<T>
                where T : class, new()
            {
                private readonly ValueDecoder<TElement> elementDecoder;
                private readonly Action<T, TElement> elementAdder;

                public ListAddDecoder(Action<T, TElement> elementAdder)
                {
                    this.elementDecoder = GetDecoder<TElement>();
                    this.elementAdder = elementAdder;
                }

                public override T DecodeTyped(JsonDecoder decoder, string text, ref int offset)
                {
                    if (TryConsumeToken(text, ref offset, "null"))
                    {
                        return null;
                    }

                    var list = new T();

                    ConsumeToken(text, ref offset, '[');

                    while (PeekChar(text, offset) != '\0')
                    {
                        this.elementAdder(list, this.elementDecoder.DecodeTyped(decoder, text, ref offset));

                        if (!TryConsumeToken(text, ref offset, ','))
                        {
                            break;
                        }
                    }

                    ConsumeToken(text, ref offset, ']');

                    return list;
                }
            }

            private class ListConstructorDecoder<T, TElement> : ValueDecoder<T>
                where T : class
            {
                private readonly ValueDecoder<TElement> elementDecoder;
                private readonly Func<List<TElement>, T> listConstructor;

                internal static readonly ObjectPool<List<TElement>> listPool = new ObjectPool<List<TElement>>(() => new List<TElement>(), list => list.Clear());

                public ListConstructorDecoder(Func<List<TElement>, T> listConstructor)
                {
                    this.elementDecoder = GetDecoder<TElement>();
                    this.listConstructor = listConstructor;
                }

                public override T DecodeTyped(JsonDecoder decoder, string text, ref int offset)
                {
                    if (TryConsumeToken(text, ref offset, "null"))
                    {
                        return null;
                    }

                    var list = listPool.AllocateFromPool();
                    try
                    {

                        ConsumeToken(text, ref offset, '[');

                        while (PeekChar(text, offset) != '\0')
                        {
                            list.Add(this.elementDecoder.DecodeTyped(decoder, text, ref offset));

                            if (!TryConsumeToken(text, ref offset, ','))
                            {
                                break;
                            }
                        }

                        ConsumeToken(text, ref offset, ']');

                        return this.listConstructor(list);
                    }
                    finally
                    {
                        listPool.ReturnToPool(list);
                    }
                }
            }

            private class DictionaryAddDecoder<T, K, V> : ValueDecoder<T>
                where T : class, new()
            {
                private readonly Action<T, K, V> keyValueAdder;
                private readonly ValueDecoder<K> keyDecoder;
                private readonly ValueDecoder<V> valueDecoder;

                public DictionaryAddDecoder(Action<T, K, V> keyValueAdder)
                {
                    this.keyValueAdder = keyValueAdder;
                    this.keyDecoder = GetDecoder<K>();
                    this.valueDecoder = GetDecoder<V>();
                }

                public override T DecodeTyped(JsonDecoder decoder, string text, ref int offset)
                {
                    if (TryConsumeToken(text, ref offset, "null"))
                    {
                        return null;
                    }

                    var instance = new T();

                    ConsumeToken(text, ref offset, '{');

                    while (PeekChar(text, offset) != '\0')
                    {
                        var key = keyDecoder.DecodeTyped(decoder, text, ref offset);
                        ConsumeToken(text, ref offset, ':');
                        var value = valueDecoder.DecodeTyped(decoder, text, ref offset);

                        this.keyValueAdder(instance, key, value);

                        if (!TryConsumeToken(text, ref offset, ','))
                        {
                            break;
                        }
                    }

                    ConsumeToken(text, ref offset, '}');

                    return instance;
                }
            }

            private class DictionaryConstructorDecoder<T, K, V> : ValueDecoder<T>
                where T : class
            {
                private readonly Func<Dictionary<K, V>, T> instanceConstructor;
                private readonly ValueDecoder<K> keyDecoder;
                private readonly ValueDecoder<V> valueDecoder;

                private static readonly ObjectPool<Dictionary<K, V>> dictionaryPool
                    = new ObjectPool<Dictionary<K, V>>(() => new Dictionary<K, V>(), d => d.Clear());

                public DictionaryConstructorDecoder(Func<Dictionary<K, V>, T> instanceConstructor)
                {
                    this.instanceConstructor = instanceConstructor;
                    this.keyDecoder = GetDecoder<K>();
                    this.valueDecoder = GetDecoder<V>();
                }

                public override T DecodeTyped(JsonDecoder decoder, string text, ref int offset)
                {
                    if (TryConsumeToken(text, ref offset, "null"))
                    {
                        return null;
                    }

                    var d = dictionaryPool.AllocateFromPool();
                    try
                    {
                        ConsumeToken(text, ref offset, '{');

                        while (PeekChar(text, offset) != '\0')
                        {
                            var key = keyDecoder.DecodeTyped(decoder, text, ref offset);
                            ConsumeToken(text, ref offset, ':');
                            var value = valueDecoder.DecodeTyped(decoder, text, ref offset);

                            d.Add(key, value);

                            if (!TryConsumeToken(text, ref offset, ','))
                            {
                                break;
                            }
                        }

                        ConsumeToken(text, ref offset, '}');

                        return this.instanceConstructor(d);
                    }
                    finally
                    {
                        dictionaryPool.ReturnToPool(d);
                    }
                }
            }

            private class ObjectMemberDecoder<T> : ValueDecoder<T>
                where T : new()
            {
                private readonly Dictionary<string, MemberDecoder<T>> memberDecoders;
                private readonly ValueDecoder<string> keyDecoder;

                public ObjectMemberDecoder(IEnumerable<EncodingMember> members)
                {
                    this.keyDecoder = GetDecoder<string>();
                    this.memberDecoders = members.ToDictionary(m => m.Name, m => CreateMemberDecocer(m));
                }

                private static MemberDecoder<T> CreateMemberDecocer(EncodingMember member)
                {
                    return (MemberDecoder<T>)Activator.CreateInstance(typeof(MemberDecoder<,>).MakeGenericType(typeof(T), member.Type), new object[] { member });
                }

                public override T DecodeTyped(JsonDecoder decoder, string text, ref int offset)
                {
                    if (TryConsumeToken(text, ref offset, "null"))
                    {
                        return default(T);
                    }

                    var instance = new T();

                    ConsumeToken(text, ref offset, '{');

                    while (PeekChar(text, offset) != '\0')
                    {
                        var key = this.keyDecoder.DecodeTyped(decoder, text, ref offset);
                        ConsumeToken(text, ref offset, ':');

                        MemberDecoder<T> memberDecoder;
                        if (this.memberDecoders.TryGetValue(key, out memberDecoder))
                        {
                            memberDecoder.DecodeInto(decoder, text, ref offset, ref instance);
                        }
                        else
                        {
                            throw new InvalidOperationException(string.Format("The type '{0}' does not have a member named '{1}'.", typeof(T), key));
                        }

                        if (!TryConsumeToken(text, ref offset, ','))
                        {
                            break;
                        }
                    }

                    ConsumeToken(text, ref offset, '}');

                    return instance;
                }
            }

            private abstract class MemberDecoder<TInstance>
            {
                public abstract void DecodeInto(JsonDecoder decoder, string text, ref int offset, ref TInstance instance);
            }

            private class MemberDecoder<TInstance, TMember> : MemberDecoder<TInstance>
            {
                private readonly EncodingMember<TInstance, TMember> member;
                private readonly ValueDecoder<TMember> valueDecoder;

                public MemberDecoder(EncodingMember<TInstance, TMember> member)
                {
                    this.member = member;
                    this.valueDecoder = GetDecoder<TMember>();
                }

                public override void DecodeInto(JsonDecoder decoder, string text, ref int offset, ref TInstance instance)
                {
                    var value = this.valueDecoder.DecodeTyped(decoder, text, ref offset);
                    this.member.SetTypedValue(instance, value);
                }
            }

            private delegate T DecoderFunc<T>(JsonDecoder decoder, string text, ref int offset);

            private class FuncDecoder<T> : ValueDecoder<T>
            {
                private readonly DecoderFunc<T> func;

                public FuncDecoder(DecoderFunc<T> func)
                {
                    this.func = func;
                }

                public override T DecodeTyped(JsonDecoder decoder, string text, ref int offset)
                {
                    return this.func(decoder, text, ref offset);
                }
            }

            private class NumberDecoder<T> : ValueDecoder<T>
            {
                private readonly Func<double, T> doubleConverter;
                private readonly Func<decimal, T> decimalConverter;

                public NumberDecoder(Func<double, T> doubleConverter, Func<decimal, T> decimalConverter)
                {
                    this.doubleConverter = doubleConverter;
                    this.decimalConverter = decimalConverter;
                }

                public override T DecodeTyped(JsonDecoder decoder, string text, ref int offset)
                {
                    double dbl;
                    decimal dec;
                    var kind = decoder.DecodeNumber(text, ref offset, typeof(T), out dbl, out dec);
                    if (kind == NumberKind.Double)
                    {
                        return this.doubleConverter(dbl);
                    }
                    else
                    {
                        return this.decimalConverter(dec);
                    }
                }
            }

            private class StringDecoder<T> : ValueDecoder<T>
            {
                public override T DecodeTyped(JsonDecoder decoder, string text, ref int offset)
                {
                    return (T)decoder.DecodeString(text, ref offset, typeof(T));
                }
            }

            private class NullableDecoder<T> : ValueDecoder<T?>
                where T : struct
            {
                private readonly ValueDecoder<T> valueDecoder;

                public NullableDecoder(ValueDecoder<T> valueDecoder)
                {
                    this.valueDecoder = valueDecoder;
                }

                public override T? DecodeTyped(JsonDecoder decoder, string text, ref int offset)
                {
                    if (TryConsumeToken(text, ref offset, "null"))
                    {
                        return null;
                    }
                    else
                    {
                        return this.valueDecoder.DecodeTyped(decoder, text, ref offset);
                    }
                }
            }

            private static readonly ConcurrentDictionary<Type, ValueDecoder> valueDecoders
                = new ConcurrentDictionary<Type, ValueDecoder>();

            private static ValueDecoder GetDecoder(Type type)
            {
                ValueDecoder valueDecoder;

                if (!valueDecoders.TryGetValue(type, out valueDecoder))
                {
                    valueDecoder = valueDecoders.GetOrAdd(type, CreateDecoder(type));
                }

                return valueDecoder;
            }

            private static ValueDecoder CreateDecoder(Type type)
            {
                var nnType = GetNonNullableType(type);
                ValueDecoder decoder = CreateNonNullDecoder(nnType);

                if (nnType != type)
                {
                    decoder = (ValueDecoder)Activator.CreateInstance(typeof(NullableDecoder<>).MakeGenericType(nnType), new object[] { decoder });
                }

                return decoder;
            }

            private static readonly object[] NoArgs = new object[0];

            private static ValueDecoder CreateNonNullDecoder(Type type)
            {
                // arrays
                if (type.IsArray)
                {
                    return (ValueDecoder)Activator.CreateInstance(typeof(ArrayDecoder<>).MakeGenericType(type.GetElementType()), NoArgs);
                }

                var defaultConstructor = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public).FirstOrDefault(c => c.GetParameters().Length == 0);

                // compatible with dictionary patterns?
                Type keyType;
                Type valueType;
                if (TryGetDictionaryTypes(type, out keyType, out valueType))
                {
                    // type has default constructor and compatible Add method.
                    var addMethodArgTypes = new Type[] { keyType, valueType };
                    var addMethod = type.GetMethods(BindingFlags.Instance | BindingFlags.Public).FirstOrDefault(m => IsMatchingMethod(m, "Add", addMethodArgTypes));
                    if (defaultConstructor != null && addMethod != null)
                    {
                        var actionType = typeof(Action<,,>).MakeGenericType(type, keyType, valueType);
                        var keyValueAdder = Delegate.CreateDelegate(actionType, addMethod);
                        return (ValueDecoder)Activator.CreateInstance(
                            typeof(DictionaryAddDecoder<,,>).MakeGenericType(type, keyType, valueType), 
                            new object[] { keyValueAdder });
                    }

                    // type has constructor that has argument compatible with dictionary
                    var constructorArgTypes = new Type[] { typeof(Dictionary<,>).MakeGenericType(keyType, valueType) };
                    var constructor = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public).FirstOrDefault(c => IsMatchingConstructor(c, constructorArgTypes));
                    if (constructor != null)
                    {
                        var constructorFunc = CreateMatchingConstructorDelegate(constructor, constructorArgTypes);
                        return (ValueDecoder)Activator.CreateInstance(
                            typeof(DictionaryConstructorDecoder<,,>).MakeGenericType(type, keyType, valueType),
                            new object[] { constructorFunc });
                    }
                }

                // compatible with list patterns?
                var elementType = GetElementType(type);
                if (elementType != null)
                {
                    // has default constructor and compatible Add method.
                    var addMethodArgTypes = new Type[] { elementType };
                    var addMethod = type.GetMethods(BindingFlags.Instance | BindingFlags.Public).FirstOrDefault(m => IsMatchingMethod(m, "Add", addMethodArgTypes));
                    if (defaultConstructor != null && addMethod != null)
                    {
                        var actionType = typeof(Action<,>).MakeGenericType(type, elementType);
                        var valueAdder = Delegate.CreateDelegate(actionType, addMethod);
                        return (ValueDecoder)Activator.CreateInstance(
                            typeof(ListAddDecoder<,>).MakeGenericType(type, elementType),
                            new object[] { valueAdder });
                    }

                    // type has constructor that has argument compatible with list
                    var constructorArgTypes = new Type[] { typeof(List<>).MakeGenericType(elementType) };
                    var constructor = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public).FirstOrDefault(c => IsMatchingConstructor(c, constructorArgTypes));
                    if (constructor != null)
                    {
                        var constructorFunc = CreateMatchingConstructorDelegate(constructor, constructorArgTypes);
                        return (ValueDecoder)Activator.CreateInstance(
                            typeof(ListConstructorDecoder<,>).MakeGenericType(type, elementType),
                            new object[] { constructorFunc });
                    }
                }

                // types with assignable members
                var members = GetEncodingMembers(type);
                if (members.Count > 0 && members.All(m => m.CanWrite) && defaultConstructor != null)
                {
                    return (ValueDecoder)Activator.CreateInstance(typeof(ObjectMemberDecoder<>).MakeGenericType(type), new object[] { members });
                }

                // can be parsed from string?
                if (StringEncoding.Instance.CanDecode(type))
                {
                    return (ValueDecoder)Activator.CreateInstance(typeof(StringDecoder<>).MakeGenericType(type), NoArgs);
                }

                throw new InvalidOperationException(string.Format("The type '{0}' cannot be decoded from JSON.", type));
            }

            private static bool IsMatchingConstructor(ConstructorInfo constructor, Type[] argTypes)
            {
                var parameters = constructor.GetParameters();

                if (parameters.Length != argTypes.Length)
                {
                    return false;
                }

                for (int i = 0; i < argTypes.Length; i++)
                {
                    if (!parameters[i].ParameterType.IsAssignableFrom(argTypes[i]))
                    {
                        return false;
                    }
                }

                return true;
            }

            private static Delegate CreateMatchingConstructorDelegate(ConstructorInfo constructor, Type[] argTypes)
            {
                var constructorParameters = constructor.GetParameters();
                var lambdaParameters = constructorParameters.Select((p, i) => Expression.Parameter(argTypes[i], p.Name)).ToArray();
                var args = lambdaParameters.Select((p, i) => Expression.Convert(p, constructorParameters[i].ParameterType)).ToArray();
                var lambda = Expression.Lambda(Expression.New(constructor, args), lambdaParameters);
                return lambda.Compile();
            }

            private static bool IsMatchingMethod(MethodInfo method, string name, Type[] argTypes)
            {
                if (method.Name != name)
                {
                    return false;
                }

                var parameters = method.GetParameters();
                if (parameters.Length != argTypes.Length)
                {
                    return false;
                }

                for (int i = 0; i < argTypes.Length; i++)
                {
                    if (!parameters[i].ParameterType.IsAssignableFrom(argTypes[i]))
                    {
                        return false;
                    }
                }

                return true;
            }

            static JsonDecoder()
            {
                InitStructDecoder(new NumberDecoder<byte>(d => (byte)d, d => (byte)d));
                InitStructDecoder(new NumberDecoder<sbyte>(d => (sbyte)d, d => (sbyte)d));
                InitStructDecoder(new NumberDecoder<short>(d => (short)d, d => (short)d));
                InitStructDecoder(new NumberDecoder<ushort>(d => (ushort)d, d => (ushort)d));
                InitStructDecoder(new NumberDecoder<int>(d => (int)d, d => (int)d));
                InitStructDecoder(new NumberDecoder<uint>(d => (uint)d, d => (uint)d));
                InitStructDecoder(new NumberDecoder<long>(d => (long)d, d => (long)d));
                InitStructDecoder(new NumberDecoder<ulong>(d => (ulong)d, d => (ulong)d));
                InitStructDecoder(new NumberDecoder<float>(d => (float)d, d => (float)d));
                InitStructDecoder(new NumberDecoder<double>(d => d, d => (double)d));
                InitStructDecoder(new NumberDecoder<decimal>(d => (decimal)d, d => d));

                InitClassDecoder(new FuncDecoder<object>((JsonDecoder d, string text, ref int offset) =>
                    d.DecodeObject(text, ref offset)));

                InitClassDecoder(new FuncDecoder<string>((JsonDecoder d, string text, ref int offset) =>
                {
                    if (TryConsumeToken(text, ref offset, "null"))
                    {
                        return null;
                    }

                    return (string)d.DecodeString(text, ref offset, typeof(string));
                }));

                InitStructDecoder(new FuncDecoder<bool>((JsonDecoder d, string text, ref int offset) =>
                {
                    if (TryConsumeToken(text, ref offset, "true"))
                    {
                        return true;
                    }
                    else if (TryConsumeToken(text, ref offset, "false"))
                    {
                        return false;
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                }));
            }

            private static void InitClassDecoder<T>(ValueDecoder<T> decoder)
                where T : class
            {
                valueDecoders.TryAdd(typeof(T), decoder);
            }

            private static void InitStructDecoder<T>(ValueDecoder<T> decoder)
                where T : struct
            {
                valueDecoders.TryAdd(typeof(T), decoder);
                valueDecoders.TryAdd(typeof(T?), new NullableDecoder<T>(decoder));
            }

            private static ValueDecoder<T> GetDecoder<T>()
            {
                return (ValueDecoder<T>)GetDecoder(typeof(T));
            }
        }
    }
}