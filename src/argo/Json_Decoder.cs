using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace Argo
{
    using Utilities;

    public static partial class Json
    {
        private class JsonDecoder
        {
            private StringTable strings;

            private JsonDecoder()
            {
            }

            public static JsonDecoder Create()
            {
                return new JsonDecoder();
            }

            private string InternString(StringBuilder builder)
            {
                if (this.strings == null)
                {
                    this.strings = new StringTable();
                }

                return this.strings.GetOrAdd(builder);
            }

            private string InternString(string text, int offset, int length)
            {
                if (this.strings == null)
                {
                    this.strings = new StringTable();
                }

                return this.strings.GetOrAdd(text, offset, length);
            }

            public object Decode(string text, ref int offset, Type type)
            {
                var decoder = GetValueDecoder(type);
                return decoder.Decode(this, text, ref offset);
            }

            public T Decode<T>(string text, ref int offset)
            {
                var decoder = GetValueDecoder<T>();
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
                    return GetValueDecoder<string>().DecodeTyped(this, text, ref offset);
                }
                else if (IsToken(text, offset, '['))
                {
                    return GetValueDecoder<List<object>>().DecodeTyped(this, text, ref offset);
                }
                else if (IsToken(text, offset, '{'))
                {
                    return GetValueDecoder<Dictionary<string, object>>().DecodeTyped(this, text, ref offset);
                }
                else
                {
                    double dbl;
                    decimal dec;
                    bool hasFraction;
                    var kind = DecodeNumber(text, ref offset, typeof(object), out dbl, out dec, out hasFraction);
                    if (kind == NumberKind.Double)
                    {
                        return dbl;
                    }
                    else
                    {
                        if (!hasFraction)
                        {
                            if (dec >= Int32.MinValue && dec <= Int32.MaxValue)
                            {
                                return (int)dec;
                            }
                            else if (dec >= Int64.MinValue && dec <= Int64.MaxValue)
                            {
                                return (long)dec;
                            }
                        }

                        return dec;
                    }
                }
            }

            private enum NumberKind
            {
                Double,
                Decimal
            }

            private NumberKind DecodeNumber(string text, ref int offset, Type type, out double dbl, out decimal dec, out bool hasFraction)
            {
                dbl = 0;
                dec = 0;

                SkipWhitespace(text, ref offset);

                var start = offset;

                bool negate = false;
                if (PeekChar(text, offset) == '-')
                {
                    negate = true;
                    offset++;
                    start++;
                }

                while (Char.IsNumber(PeekChar(text, offset)))
                {
                    offset++;
                }

                hasFraction = false;
                if (PeekChar(text, offset) == '.')
                {
                    offset++;
                    hasFraction = true;

                    while (Char.IsNumber(PeekChar(text, offset)))
                    {
                        offset++;
                    }
                }

                bool hasExponent = false;
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

                if (!hasFraction && !hasExponent)
                {
                    for (int x = start; x < offset; x++)
                    {
                        dec = dec * 10 + (text[x] - '0');
                    }

                    if (negate)
                    {
                        dec = -dec;
                    }

                    return NumberKind.Decimal;
                }

                var number = this.InternString(text, start, offset - start);

                if (type == typeof(float) || type == typeof(double) || (type == typeof(object) && hasFraction))
                {
                    if (double.TryParse(number, NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out dbl))
                    {
                        if (negate)
                        {
                            dbl = -dbl;
                        }

                        return NumberKind.Double;
                    }
                }

                if (decimal.TryParse(number, NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out dec))
                {
                    if (negate)
                    {
                        dec = -dec;
                    }

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

                if (offset + token.Length <= text.Length)
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
                public abstract Type Type { get; }
                public abstract object Decode(JsonDecoder decoder, string text, ref int offset);
            }

            private abstract class ValueDecoder<T> : ValueDecoder
            {
                public abstract T DecodeTyped(JsonDecoder decoder, string text, ref int offset);

                public override Type Type
                {
                    get { return typeof(T); }
                }

                public override object Decode(JsonDecoder decoder, string text, ref int offset)
                {
                    return this.DecodeTyped(decoder, text, ref offset);
                }
            }

            private class ConvertDecoder<T, S> : ValueDecoder<S>
                where T : S
            {
                private readonly ValueDecoder<T> valueDecoder;

                public ConvertDecoder(ValueDecoder<T> valueDecoder)
                {
                    this.valueDecoder = valueDecoder;
                }

                public override S DecodeTyped(JsonDecoder decoder, string text, ref int offset)
                {
                    return this.valueDecoder.DecodeTyped(decoder, text, ref offset);
                }
            }

            private class ArrayDecoder<TElement> : ValueDecoder<TElement[]>
            {
                private readonly ValueDecoder<TElement> elementDecoder;

                internal static readonly ObjectPool<List<TElement>> listPool = new ObjectPool<List<TElement>>(() => new List<TElement>(), list => list.Clear());

                public ArrayDecoder()
                {
                    this.elementDecoder = GetValueDecoder<TElement>();
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
                    this.elementDecoder = GetValueDecoder<TElement>();
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
                    this.elementDecoder = GetValueDecoder<TElement>();
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

            private abstract class DictionaryDecoder<T, K, V> : ValueDecoder<T>
            {
                protected static readonly ObjectPool<Dictionary<K, V>> dictionaryPool
                    = new ObjectPool<Dictionary<K, V>>(() => new Dictionary<K, V>(), d => d.Clear());
            }

            private class DictionaryAddDecoder<T, K, V> : DictionaryDecoder<T, K, V>
                where T : class, new()
            {
                private readonly Action<T, K, V> keyValueAdder;
                private readonly ValueDecoder<K> keyDecoder;
                private readonly ValueDecoder<V> valueDecoder;

                public DictionaryAddDecoder(Action<T, K, V> keyValueAdder)
                {
                    this.keyValueAdder = keyValueAdder;
                    this.keyDecoder = GetStringDecoder<K>();
                    this.valueDecoder = GetValueDecoder<V>();
                }

                public override T DecodeTyped(JsonDecoder decoder, string text, ref int offset)
                {
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

            private class DictionaryConstructorDecoder<T, K, V> : DictionaryDecoder<T, K, V>
                where T : class
            {
                private readonly Func<IEnumerable<KeyValuePair<K, V>>, T> instanceConstructor;
                private readonly ValueDecoder<K> keyDecoder;
                private readonly ValueDecoder<V> valueDecoder;

                public DictionaryConstructorDecoder(Func<IEnumerable<KeyValuePair<K, V>>, T> instanceConstructor)
                {
                    this.instanceConstructor = instanceConstructor;
                    this.keyDecoder = GetStringDecoder<K>();
                    this.valueDecoder = GetValueDecoder<V>();
                }

                public override T DecodeTyped(JsonDecoder decoder, string text, ref int offset)
                {
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
                    this.keyDecoder = GetValueDecoder<string>();
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
                    this.valueDecoder = GetValueDecoder<TMember>();
                }

                public override void DecodeInto(JsonDecoder decoder, string text, ref int offset, ref TInstance instance)
                {
                    var value = this.valueDecoder.DecodeTyped(decoder, text, ref offset);
                    this.member.SetTypedValue(ref instance, value);
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
                    bool hasFraction;
                    var kind = decoder.DecodeNumber(text, ref offset, typeof(T), out dbl, out dec, out hasFraction);
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
                private readonly Func<string, T> parser;

                private static readonly ObjectPool<StringBuilder> builderPool = new ObjectPool<StringBuilder>(() => new StringBuilder(), b => b.Clear());

                public StringDecoder(Func<string, T> parser)
                {
                    this.parser = parser;
                }

                public override T DecodeTyped(JsonDecoder decoder, string text, ref int offset)
                {
                    StringBuilder builder = null;

                    try
                    {
                        ConsumeToken(text, ref offset, '"');

                        var start = offset;

                        char ch;
                        while (offset < text.Length && (ch = PeekChar(text, offset)) != '"')
                        {
                            if (ch == '\\')
                            {
                                if (builder == null)
                                {
                                    builder = builderPool.AllocateFromPool();
                                    builder.Append(text, start, offset - start);
                                }

                                builder.Append(DecodeEscapedChar(text, ref offset));
                            }
                            else if (builder != null)
                            {
                                builder.Append(ch);
                                offset++;
                            }
                            else
                            {
                                offset++;
                            }
                        }

                        var end = offset;

                        ConsumeToken(text, ref offset, '"');

                        string value = builder != null
                            ? decoder.InternString(builder)
                            : decoder.InternString(text, start, end - start);

                        return this.parser(value);
                    }
                    finally
                    {
                        if (builder != null)
                        {
                            builderPool.ReturnToPool(builder);
                        }
                    }
                }

                private static char DecodeEscapedChar(string text, ref int offset)
                {
                    offset++; // for first \

                    if (offset >= text.Length)
                    {
                        return '\0';
                    }

                    var ch = PeekChar(text, offset);
                    switch (ch)
                    {
                        case '"':
                            offset++;
                            return '"';
                        case '\\':
                            offset++;
                            return '\\';
                        case '/':
                            offset++;
                            return '/';
                        case 'b':
                            offset++;
                            return '\b';
                        case 'f':
                            offset++;
                            return '\f';
                        case 'r':
                            offset++;
                            return '\r';
                        case 'n':
                            offset++;
                            return '\n';
                        case 't':
                            offset++;
                            return '\t';
                        case 'u':
                            offset++;
                            return (char)ParseHexNumber(text, ref offset, 4);
                        default:
                            // bad escaped: throw exception here?
                            offset++;
                            return ch;
                    }
                }

                private static uint ParseHexNumber(string text, ref int offset, int maxLength)
                {
                    var start = offset;
                    uint number = 0;

                    while (offset < text.Length && offset - start < maxLength)
                    {
                        char ch = text[offset];
                        uint digit;
                        if (TryGetHexDigitValue(ch, out digit))
                        {
                            offset++;
                            number = (number << 4) + digit;
                        }
                        else
                        {
                            break;
                        }
                    }

                    return number;
                }

                private static bool TryGetHexDigitValue(char ch, out uint value)
                {
                    if (ch >= '0' && ch <= '9')
                    {
                        value = (uint)ch - (uint)'0';
                        return true;
                    }
                    else if (ch >= 'A' && ch <= 'F')
                    {
                        value = ((uint)ch - (uint)'A') + 10;
                        return true;
                    }
                    else if (ch >= 'a' && ch <= 'f')
                    {
                        value = ((uint)ch - (uint)'a') + 10;
                        return true;
                    }
                    else
                    {
                        value = 0;
                        return false;
                    }
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

            private class NullDecoder<T> : ValueDecoder<T>
                where T : class
            {
                private readonly ValueDecoder<T> valueDecoder;

                public NullDecoder(ValueDecoder<T> valueDecoder)
                {
                    this.valueDecoder = valueDecoder;
                }

                public override T DecodeTyped(JsonDecoder decoder, string text, ref int offset)
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

            private static ValueDecoder<T> GetValueDecoder<T>()
            {
                return (ValueDecoder<T>)GetValueDecoder(typeof(T));
            }

            private static ValueDecoder GetValueDecoder(Type type)
            {
                ValueDecoder valueDecoder;

                if (!valueDecoders.TryGetValue(type, out valueDecoder))
                {
                    valueDecoder = valueDecoders.GetOrAdd(type, CreateValueDecoder(type));
                }

                return valueDecoder;
            }

            private static ValueDecoder CreateValueDecoder(Type type)
            {
                var nnType = TypeHelper.GetNonNullableType(type);
                ValueDecoder decoder = CreateTypedValueDecoder(nnType);

                if (decoder.Type != nnType)
                {
                    decoder = (ValueDecoder)Activator.CreateInstance(typeof(ConvertDecoder<,>).MakeGenericType(decoder.Type, nnType), new object[] { decoder });
                }

                if (nnType != type)
                {
                    decoder = (ValueDecoder)Activator.CreateInstance(typeof(NullableDecoder<>).MakeGenericType(nnType), new object[] { decoder });
                }
                else if (type.IsClass || type.IsInterface)
                {
                    decoder = (ValueDecoder)Activator.CreateInstance(typeof(NullDecoder<>).MakeGenericType(type), new object[] { decoder });
                }

                return decoder;
            }

            private static readonly object[] NoArgs = new object[0];

            private static ValueDecoder CreateTypedValueDecoder(Type type)
            {
                // arrays
                if (type.IsArray)
                {
                    return (ValueDecoder)Activator.CreateInstance(typeof(ArrayDecoder<>).MakeGenericType(type.GetElementType()), NoArgs);
                }

                var constructors = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public);
                var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public);

                var hasPublicDefaultConstructor = 
                    type.IsValueType || 
                    constructors.FirstOrDefault(c => c.GetParameters().Length == 0) != null;

                // compatible with dictionary patterns?
                Type keyType;
                Type valueType;
                if (TypeHelper.TryGetDictionaryTypes(type, out keyType, out valueType))
                {
                    var dictionaryType = typeof(Dictionary<,>).MakeGenericType(keyType, valueType);
                    if (type != dictionaryType && type.IsAssignableFrom(dictionaryType))
                    {
                        type = dictionaryType;
                    }

                    // type has default constructor and compatible Add method.
                    var addMethodArgTypes = new Type[] { keyType, valueType };
                    var addMethod = methods.FirstOrDefault(m => IsMatchingMethod(m, "Add", addMethodArgTypes));
                    if (hasPublicDefaultConstructor && addMethod != null)
                    {
                        var actionType = typeof(Action<,,>).MakeGenericType(type, keyType, valueType);
                        var keyValueAdder = Delegate.CreateDelegate(actionType, addMethod);
                        return (ValueDecoder)Activator.CreateInstance(
                            typeof(DictionaryAddDecoder<,,>).MakeGenericType(type, keyType, valueType), 
                            new object[] { keyValueAdder });
                    }

                    // type has constructor with argument that is compatible with dictionary
                    var constructorArgTypes = new Type[] { typeof(IEnumerable<>).MakeGenericType(typeof(KeyValuePair<,>).MakeGenericType(keyType, valueType)) };
                    var constructor = constructors.FirstOrDefault(c => IsMatchingConstructor(c, constructorArgTypes));
                    if (constructor != null)
                    {
                        var constructorFunc = CreateMatchingConstructorDelegate(constructor, constructorArgTypes);
                        return (ValueDecoder)Activator.CreateInstance(
                            typeof(DictionaryConstructorDecoder<,,>).MakeGenericType(type, keyType, valueType),
                            new object[] { constructorFunc });
                    }

                    // immutable pattern
                    var addRangeMethodArgTypes = new Type[] { typeof(IEnumerable<>).MakeGenericType(typeof(KeyValuePair<,>).MakeGenericType(keyType, valueType)) };
                    var addRangeMethod = methods.FirstOrDefault(m => m.ReturnType == type && IsMatchingMethod(m, "AddRange", addRangeMethodArgTypes));
                    var emptyField = type.GetField("Empty", BindingFlags.Static | BindingFlags.Public);
                    if (addRangeMethod != null && emptyField != null)
                    {
                        var instance = emptyField.GetValue(null);
                        var addRangeFunc = Delegate.CreateDelegate(typeof(Func<,>).MakeGenericType(addRangeMethodArgTypes[0], type), instance, addRangeMethod);
                        return (ValueDecoder)Activator.CreateInstance(
                            typeof(DictionaryConstructorDecoder<,,>).MakeGenericType(type, keyType, valueType),
                            new object[] { addRangeFunc });
                    }
                }

                // compatible with list patterns?
                var elementType = TypeHelper.GetElementType(type);
                if (elementType != null)
                {
                    var listType = typeof(List<>).MakeGenericType(elementType);
                    if (type != listType && type.IsAssignableFrom(listType))
                    {
                        type = listType;
                    }

                    // has default constructor and compatible Add method.
                    var addMethodArgTypes = new Type[] { elementType };
                    var addMethod = type.GetMethods(BindingFlags.Instance | BindingFlags.Public).FirstOrDefault(m => IsMatchingMethod(m, "Add", addMethodArgTypes));
                    if (hasPublicDefaultConstructor && addMethod != null)
                    {
                        var actionType = typeof(Action<,>).MakeGenericType(type, elementType);
                        var valueAdder = Delegate.CreateDelegate(actionType, addMethod);
                        return (ValueDecoder)Activator.CreateInstance(
                            typeof(ListAddDecoder<,>).MakeGenericType(type, elementType),
                            new object[] { valueAdder });
                    }

                    // type has constructor with argument that is compatible with list
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
                var members = EncodingMember.GetEncodingMembers(type).Where(m => m.CanWrite).ToList().ToList();
                if (members.Count > 0 && hasPublicDefaultConstructor)
                {
                    return (ValueDecoder)Activator.CreateInstance(typeof(ObjectMemberDecoder<>).MakeGenericType(type), new object[] { members });
                }

                // can be parsed from string?
                var stringDecoder = CreateTypedStringDecoder(type);
                if (stringDecoder != null)
                {
                    return stringDecoder;
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

            private static readonly ConcurrentDictionary<Type, ValueDecoder> stringDecoders
                = new ConcurrentDictionary<Type, ValueDecoder>();

            private static ValueDecoder<T> GetStringDecoder<T>()
            {
                return (ValueDecoder<T>)GetStringDecoder(typeof(T));
            }

            private static ValueDecoder GetStringDecoder(Type type)
            {
                ValueDecoder stringDecoder;

                if (!stringDecoders.TryGetValue(type, out stringDecoder))
                {
                    stringDecoder = stringDecoders.GetOrAdd(type, CreateStringDecoder(type));
                }

                return stringDecoder;
            }

            private static ValueDecoder CreateStringDecoder(Type type)
            {
                var nnType = TypeHelper.GetNonNullableType(type);
                ValueDecoder decoder = CreateTypedStringDecoder(nnType);

                if (decoder.Type != nnType)
                {
                    decoder = (ValueDecoder)Activator.CreateInstance(typeof(ConvertDecoder<,>).MakeGenericType(decoder.Type, nnType), new object[] { decoder });
                }

                if (nnType != type)
                {
                    decoder = (ValueDecoder)Activator.CreateInstance(typeof(NullableDecoder<>).MakeGenericType(nnType), new object[] { decoder });
                }
                else if (type.IsClass || type.IsInterface)
                {
                    decoder = (ValueDecoder)Activator.CreateInstance(typeof(NullDecoder<>).MakeGenericType(type), new object[] { decoder });
                }

                return decoder;
            }

            private static ValueDecoder CreateTypedStringDecoder(Type type)
            {
                if (type == typeof(string))
                {
                    return GetValueDecoder<string>();
                }

                // can be parsed from string?
                var parseMethod = type.GetMethods(BindingFlags.Static | BindingFlags.Public)
                    .FirstOrDefault(m => m.Name == "Parse" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(string));

                if (parseMethod != null)
                {
                    var dType = typeof(Func<,>).MakeGenericType(typeof(string), type);
                    var parser = Delegate.CreateDelegate(dType, parseMethod, throwOnBindFailure: true);
                    return (ValueDecoder)Activator.CreateInstance(typeof(StringDecoder<>).MakeGenericType(type), new object[] { parser });
                }

                return null;
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

                InitClassDecoder(new StringDecoder<string>(s => s));

                InitClassDecoder(new FuncDecoder<object>((JsonDecoder d, string text, ref int offset) =>
                    d.DecodeObject(text, ref offset)));

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
                valueDecoders.TryAdd(typeof(T), new NullDecoder<T>(decoder));
            }

            private static void InitStructDecoder<T>(ValueDecoder<T> decoder)
                where T : struct
            {
                valueDecoders.TryAdd(typeof(T), decoder);
                valueDecoders.TryAdd(typeof(T?), new NullableDecoder<T>(decoder));
            }
        }
    }
}