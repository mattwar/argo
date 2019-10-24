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
    using System.IO;
    using Utilities;

    public static partial class Json
    {
        private ref struct JsonDecoder
        {
            private readonly Encoding encoding;
            private ReadOnlySpan<byte> encodedText;
            private int offset;
            private StringTable strings;
            private EncodedStringTable encodedStrings;

            private JsonDecoder(ReadOnlySpan<byte> encodedText, Encoding encoding)
            {
                this.encodedText = encodedText;
                this.encoding = encoding;
                this.offset = 0;
                this.strings = null;
                this.encodedStrings = null;
            }

            private string InternString(StringBuilder builder)
            {
                if (this.strings == null)
                {
                    this.strings = new StringTable();
                }

                return this.strings.GetOrAdd(builder);
            }

            private string InternString(int offset, int length)
            {
                if (this.encodedStrings == null)
                {
                    this.encodedStrings = new EncodedStringTable(Encoding.UTF8);
                }

                return this.encodedStrings.GetOrAdd(encodedText, offset, length);
            }

            public static object Decode(ReadOnlySpan<byte> encodedText, Encoding encoding, Type type)
            {
                var decoder = new JsonDecoder(encodedText, encoding); 
                var valueDecoder = GetValueDecoder(type);
                return valueDecoder.Decode(ref decoder);
            }

            public static T Decode<T>(ReadOnlySpan<byte> encodedText, Encoding encoding)
            {
                var decoder = new JsonDecoder(encodedText, encoding);
                var valueDecoder = GetValueDecoder<T>();
                return valueDecoder.DecodeTyped(ref decoder);
            }

            public static Dictionary<string, object> Decode(ReadOnlySpan<byte> encodedText, Encoding encoding, IEnumerable<KeyValuePair<string, Type>> valueTypes)
            {
                var decoder = new JsonDecoder(encodedText, encoding);
                var valueDecoder = new DictionaryLookupDecoder(valueTypes);
                return valueDecoder.DecodeTyped(ref decoder);
            }

            private object DecodeObject()
            {
                this.SkipWhitespace();

                if (this.TryConsumeToken("null"))
                {
                    return null;
                }
                else if (this.TryConsumeToken("true"))
                {
                    return true;
                }
                else if (this.TryConsumeToken("false"))
                {
                    return false;
                }
                else if (this.IsToken('"'))
                {
                    return GetValueDecoder<string>().DecodeTyped(ref this);
                }
                else if (this.IsToken('['))
                {
                    return GetValueDecoder<List<object>>().DecodeTyped(ref this);
                }
                else if (this.IsToken('{'))
                {
                    return GetValueDecoder<Dictionary<string, object>>().DecodeTyped(ref this);
                }
                else
                {
                    double dbl;
                    decimal dec;
                    bool hasFraction;
                    var kind = this.DecodeNumber(typeof(object), out dbl, out dec, out hasFraction);
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

            private NumberKind DecodeNumber(Type type, out double dbl, out decimal dec, out bool hasFraction)
            {
                dbl = 0;
                dec = 0;

                SkipWhitespace();

                var start = offset;

                bool negate = false;
                if (ReadChar('-'))
                {
                    negate = true;
                    start = this.offset;
                }

                while (ReadChar(c => char.IsNumber(c)))
                {
                }

                hasFraction = false;
                if (ReadChar('.'))
                {
                    hasFraction = true;

                    while (ReadChar(c => char.IsNumber(c)))
                    {
                    }
                }

                bool hasExponent = false;
                if (ReadChar(c => c == 'e' || c == 'E'))
                {
                    hasExponent = true;

                    if (ReadChar(c => c == '+' || c == '-'))
                    {
                    }

                    while (ReadChar(c => char.IsNumber(c)))
                    {
                    }
                }

                if (!hasFraction && !hasExponent)
                {
                    var finalOffset = this.offset;

                    this.offset = start;
                    while (this.offset < finalOffset && this.ReadChar(out var ch))
                    {
                        dec = dec * 10 + (ch - '0');
                    }

                    if (negate)
                    {
                        dec = -dec;
                    }

                    this.offset = finalOffset;

                    return NumberKind.Decimal;
                }

                var number = InternString(start, offset - start);

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

            private void SkipObject()
            {
                SkipWhitespace();

                if (TryConsumeToken("null")
                    || TryConsumeToken("true")
                    || TryConsumeToken("false"))
                {
                    return;
                }
                else if (IsToken('"'))
                {
                    SkipString();
                }
                else if (IsToken('['))
                {
                    SkipList();
                }
                else if (IsToken('{'))
                {
                    SkipDictionary();
                }
                else
                {
                    SkipNumber();
                }
            }

            private void SkipString()
            {
                ConsumeToken('"');

                while (IsCodeUnit(c => c != '"', out var cu))
                {
                    if (cu == '\\')
                    {
                        SkipEscapedChar();
                    }
                    else
                    {
                        Read();
                    }
                }

                ConsumeToken('"');
            }

            private void SkipEscapedChar()
            {
                this.Read(); // for first \

                if (this.Read(out var cu))
                {
                    switch (cu)
                    {
                        case '"':
                        case '\\':
                        case '/':
                        case 'b':
                        case 'f':
                        case 'r':
                        case 'n':
                        case 't':
                        default:
                            break;
                        case 'u':
                            SkipHexNumber(4);
                            break;
                    }
                }
            }

            private void SkipHexNumber(int maxLength)
            {
                var start = offset;
                while (offset - start < maxLength && ReadChar(c => IsHexDigit(c)))
                {
                }
            }

            private static bool IsHexDigit(char ch)
            {
                if (ch >= '0' && ch <= '9')
                {
                    return true;
                }
                else if (ch >= 'A' && ch <= 'F')
                {
                    return true;
                }
                else if (ch >= 'a' && ch <= 'f')
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }

            private void SkipNumber()
            {
                SkipWhitespace();

                var start = this.offset;

                if (ReadChar('-'))
                {
                    start = this.offset;
                }

                while (ReadChar(c => char.IsNumber(c)))
                {
                }

                if (ReadChar('.'))
                {
                    while (ReadChar(c => char.IsNumber(c)))
                    {
                    }
                }

                if (Read(c => c == 'e' || c == 'E'))
                {
                    if (Read(c => c == '+' || c == '-'))
                    {
                    }

                    while (ReadChar(c => char.IsNumber(c)))
                    {
                    }
                }
            }

            private void SkipWhitespace()
            {
                while (ReadChar(c => char.IsWhiteSpace(c)))
                {
                }
            }

            private void SkipList()
            {
                ConsumeToken('[');

                int cu;
                while ((cu = Peek()) != '\0' && cu != '}')
                {
                    SkipObject();
                }

                ConsumeToken(']');
            }

            private void SkipDictionary()
            {
                ConsumeToken('{');

                int cu;
                while ((cu = Peek()) != '\0' && cu != '}')
                {
                    SkipString(); //key
                    ConsumeToken(':');
                    SkipObject(); // value

                    if (!TryConsumeToken(','))
                    {
                        break;
                    }
                }

                ConsumeToken('}');
            }

            /// <summary>
            /// Returns the current code unit.
            /// </summary>
            private int Peek()
            {
                if (offset < encodedText.Length)
                {
                    return (char)EncodingUtil.ReadCode(encoding, encodedText, this.offset, out _);
                }

                return 0;
            }

            /// <summary>
            /// Returns true if the current code unit is the specific char.
            /// </summary>
            private bool IsChar(char ch)
            {
                if (this.offset >= this.encodedText.Length)
                    return false;

                var code = EncodingUtil.ReadCode(encoding, encodedText, this.offset, out var length);
                return length <= sizeof(char) && ch == (char)code;
            }

            /// <summary>
            /// Returns true if the current code unit is a char and matches the specified condition.
            /// Returns the matching char as an out-parameter.
            /// </summary>
            private bool IsChar(Func<char, bool> fnTest, out char ch)
            {
                ch = '\0';

                if (this.offset >= this.encodedText.Length)
                    return false;

                var code = EncodingUtil.ReadCode(encoding, encodedText, this.offset, out var length);
                ch = unchecked((char)code);

                return length < sizeof(char) && fnTest(ch);
            }

            /// <summary>
            /// Returns true if the current code unit matches the specified condition.
            /// Returns the matching code unit as an out-parameter.
            /// </summary>
            private bool IsCodeUnit(Func<int, bool> fnTest, out int code)
            {
                code = 0;

                if (this.offset >= this.encodedText.Length)
                    return false;

                code = EncodingUtil.ReadCode(encoding, encodedText, this.offset, out var length);
                return fnTest(code);
            }

            /// <summary>
            /// Returns true if the current code unit matches the specified condition.
            /// </summary>
            private bool IsCodeUnit(Func<int, bool> fnTest)
            {
                return IsCodeUnit(fnTest, out _);
            }

            /// <summary>
            /// Advances past the current code unit.
            /// Returns true if there was a code unit to advanced past.
            /// </summary>
            private bool Read()
            {
                return Read(out _);
            }

            /// <summary>
            /// Advances past the current code unit.
            /// Returns true if there was a code unit to advance past.
            /// Returns the code unit as an out-parameter.
            /// </summary>
            private bool Read(out int code)
            {
                if (this.offset >= this.encodedText.Length)
                {
                    code = 0;
                    return false;
                }

                code = EncodingUtil.ReadCode(encoding, encodedText, this.offset, out var length);
                this.offset += length;

                return true;
            }

            /// <summary>
            /// Returns true if the current code unit is a character that passes the specified condition and advances to the next code unit if so.
            /// </summary>
            private bool Read(Func<int, bool> fnTest)
            {
                return Read(fnTest, out _);
            }

            /// <summary>
            /// Reads the next code unit from the encoded text that matches the condition
            /// </summary>
            private bool Read(Func<int, bool> fnTest, out int code)
            {
                code = 0;

                if (this.offset >= this.encodedText.Length)
                    return false;

                code = EncodingUtil.ReadCode(encoding, encodedText, this.offset, out var length);

                if (fnTest(code))
                {
                    this.offset += length;
                    return true;
                }

                return false;
            }

            /// <summary>
            /// Advances past the next code unit if it that matches the specified char.
            /// Returns false if the code unit does not match or there are no more code units.
            /// </summary>
            private bool ReadChar(char ch)
            {
                if (this.offset >= this.encodedText.Length)
                    return false;

                var code = EncodingUtil.ReadCode(encoding, encodedText, this.offset, out var length);
                
                if (length <= sizeof(char) && ch == (char)code)
                {
                    this.offset += length;
                    return true;
                }

                return false;
            }

            /// <summary>
            /// Advances past the next code unit if it is a char.
            /// Returns false if the code unit does is not a char or there are no more code units.
            /// Returns the char as an out-parameter.
            /// </summary>
            private bool ReadChar(out char ch)
            {
                if (this.offset >= this.encodedText.Length)
                {
                    ch = '\0';
                    return false;
                }

                var code = EncodingUtil.ReadCode(encoding, encodedText, this.offset, out var length);

                if (length <= sizeof(char))
                {
                    ch = (char)code;
                    this.offset += length;
                    return true;
                }
                else
                {
                    ch = '\0';
                    return false;
                }
            }

            /// <summary>
            /// Advances past the next code unit if it is a char that matches the condition.
            /// Returns false if the next code unit is not a char, does not match the condition or there are no more code units.
            /// </summary>
            private bool ReadChar(Func<char, bool> fnTest)
            {
                return ReadChar(fnTest, out _);
            }

            /// <summary>
            /// Advances past the next code unit if it is a char that matches the condition.
            /// Returns false if the code unit is not a char, does not match the condition or there are no more code units.
            /// </summary>
            private bool ReadChar(Func<char, bool> fnTest, out char ch)
            {

                if (this.offset >= this.encodedText.Length)
                {
                    ch = '\0';
                    return false;
                }

                var code = EncodingUtil.ReadCode(encoding, encodedText, this.offset, out var length);
                ch = unchecked((char)code);

                if (length < sizeof(char) && fnTest(ch))
                {
                    this.offset += length;
                    return true;
                }

                return false;
            }

            /// <summary>
            /// Returns true if the next non whitespace character is the specified character.
            /// </summary>
            private bool IsToken(char token)
            {
                SkipWhitespace();

                if (offset < encodedText.Length)
                {
                    return Peek() == token;
                }

                return false;
            }

            /// <summary>
            /// Returns true if the next non-whitespace sequence of characters matches the specified string.
            /// </summary>
            private bool IsToken(string token)
            {
                SkipWhitespace();

                var originalOffset = this.offset;

                if (offset + token.Length <= encodedText.Length)
                {
                    for (int i = 0; i < token.Length; i++)
                    {
                        if (!this.ReadChar(c => c == token[i]))
                        {
                            this.offset = originalOffset;
                            return false;
                        }
                    }

                    this.offset = originalOffset;
                    return true;
                }

                return false;
            }

            /// <summary>
            /// Asserts that the next non-whitespace character is the specified character and advances beyond it.
            /// </summary>
            private void ConsumeToken(char token)
            {
                if (!TryConsumeToken(token))
                {
                    throw new InvalidOperationException(string.Format("Expected token '{0}' not found at offset {1}", token, offset));
                }
            }

            /// <summary>
            /// Returns true if the next non-whitespace character is the specified character.
            /// </summary>
            private bool TryConsumeToken(char token)
            {
                SkipWhitespace();

                var start = this.offset;
                if (ReadChar(token))
                {
                    return true;
                }
                else
                {
                    this.offset = start;
                    return false;
                }
            }

            /// <summary>
            /// Returns true if the next non-whitespace sequence of characters matches the specified string.
            /// </summary>
            private bool TryConsumeToken(string token)
            {
                SkipWhitespace();

                var originalOffset = this.offset;

                if (offset + token.Length <= encodedText.Length)
                {
                    for (int i = 0; i < token.Length; i++)
                    {
                        if (!this.ReadChar(out var ch) || ch != token[i])
                        {
                            this.offset = originalOffset;
                            return false;
                        }
                    }

                    return true;
                }
                else
                {
                    this.offset = originalOffset;
                    return false;
                }
            }

            private abstract class ValueDecoder
            {
                public abstract Type Type { get; }
                public abstract object Decode(ref JsonDecoder decoder);
                public virtual int GetMatchingMemberCount(List<string> names) { return 0; }
            }

            private abstract class ValueDecoder<T> : ValueDecoder
            {
                public abstract T DecodeTyped(ref JsonDecoder decoder);

                public override Type Type
                {
                    get { return typeof(T); }
                }

                public override object Decode(ref JsonDecoder decoder)
                {
                    return this.DecodeTyped(ref decoder);
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

                public override S DecodeTyped(ref JsonDecoder decoder)
                {
                    return this.valueDecoder.DecodeTyped(ref decoder);
                }
            }

            private class ArrayDecoder<TElement> : ValueDecoder<TElement[]>
            {
                private readonly ValueDecoder<TElement> elementDecoder;

                internal static readonly ObjectPool<List<TElement>> listPool = new ObjectPool<List<TElement>>(() => new List<TElement>(10), list => list.Clear());

                public ArrayDecoder()
                {
                    this.elementDecoder = GetValueDecoder<TElement>();
                }

                public override TElement[] DecodeTyped(ref JsonDecoder decoder)
                {
                    var list = listPool.AllocateFromPool();
                    try
                    {
                        decoder.ConsumeToken('[');

                        int cu;
                        while ((cu = decoder.Peek()) != '\0' && cu != '}')
                        {
                            list.Add(this.elementDecoder.DecodeTyped(ref decoder));

                            if (!decoder.TryConsumeToken(','))
                            {
                                break;
                            }
                        }

                        decoder.ConsumeToken(']');

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

                public override T DecodeTyped(ref JsonDecoder decoder)
                {
                    var list = new T();

                    decoder.ConsumeToken('[');

                    int cu;
                    while ((cu = decoder.Peek()) != '\0' && cu != ']')
                    {
                        this.elementAdder(list, this.elementDecoder.DecodeTyped(ref decoder));

                        if (!decoder.TryConsumeToken(','))
                        {
                            break;
                        }
                    }

                    decoder.ConsumeToken(']');

                    return list;
                }
            }

            private class ListConstructorDecoder<T, TElement> : ValueDecoder<T>
                where T : class
            {
                private readonly ValueDecoder<TElement> elementDecoder;
                private readonly Func<List<TElement>, T> listConstructor;

                internal static readonly ObjectPool<List<TElement>> listPool = new ObjectPool<List<TElement>>(() => new List<TElement>(10), list => list.Clear());

                public ListConstructorDecoder(Func<List<TElement>, T> listConstructor)
                {
                    this.elementDecoder = GetValueDecoder<TElement>();
                    this.listConstructor = listConstructor;
                }

                public override T DecodeTyped(ref JsonDecoder decoder)
                {
                    if (decoder.TryConsumeToken("null"))
                    {
                        return null;
                    }

                    var list = listPool.AllocateFromPool();
                    try
                    {

                        decoder.ConsumeToken('[');

                        int cu;
                        while ((cu = decoder.Peek()) != '\0' && cu != ']')
                        {
                            list.Add(this.elementDecoder.DecodeTyped(ref decoder));

                            if (!decoder.TryConsumeToken(','))
                            {
                                break;
                            }
                        }

                        decoder.ConsumeToken(']');

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
                    = new ObjectPool<Dictionary<K, V>>(() => new Dictionary<K, V>(10), d => d.Clear());
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
                    this.keyDecoder = GetKeyDecoder<K>();
                    this.valueDecoder = GetValueDecoder<V>();
                }

                public override T DecodeTyped(ref JsonDecoder decoder)
                {
                    var instance = new T();

                    decoder.ConsumeToken('{');

                    int cu;
                    while ((cu = decoder.Peek()) != '\0' && cu != '}')
                    {
                        var key = keyDecoder.DecodeTyped(ref decoder);
                        decoder.ConsumeToken(':');
                        var value = valueDecoder.DecodeTyped(ref decoder);

                        this.keyValueAdder(instance, key, value);

                        if (!decoder.TryConsumeToken(','))
                        {
                            break;
                        }
                    }

                    decoder.ConsumeToken('}');

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
                    this.keyDecoder = GetKeyDecoder<K>();
                    this.valueDecoder = GetValueDecoder<V>();
                }

                public override T DecodeTyped(ref JsonDecoder decoder)
                {
                    var d = dictionaryPool.AllocateFromPool();
                    try
                    {
                        decoder.ConsumeToken('{');

                        int cu;
                        while ((cu = decoder.Peek()) != '\0' && cu != '}')
                        {
                            var key = keyDecoder.DecodeTyped(ref decoder);
                            decoder.ConsumeToken(':');
                            var value = valueDecoder.DecodeTyped(ref decoder);

                            d.Add(key, value);

                            if (!decoder.TryConsumeToken(','))
                            {
                                break;
                            }
                        }

                        decoder.ConsumeToken('}');

                        return this.instanceConstructor(d);
                    }
                    finally
                    {
                        dictionaryPool.ReturnToPool(d);
                    }
                }
            }

            private class DictionaryLookupDecoder : ValueDecoder<Dictionary<string, object>>
            {
                private readonly ValueDecoder<string> keyDecoder;
                private readonly Dictionary<string, ValueDecoder> valueDecoders;
                private readonly ValueDecoder<object> objectDecoder;

                public DictionaryLookupDecoder(IEnumerable<KeyValuePair<string, Type>> valueTypes)
                {
                    this.keyDecoder = GetValueDecoder<string>();
                    this.valueDecoders = valueTypes.ToDictionary(kvp => kvp.Key, kvp => GetValueDecoder(kvp.Value));
                    this.objectDecoder = GetValueDecoder<object>();
                }

                public override Dictionary<string, object> DecodeTyped(ref JsonDecoder decoder)
                {
                    if (decoder.TryConsumeToken("null"))
                    {
                        return null;
                    }

                    var instance = new Dictionary<string, object>();

                    decoder.ConsumeToken('{');

                    int cu;
                    while ((cu = decoder.Peek()) != '\0' && cu != '}')
                    {
                        var key = this.keyDecoder.DecodeTyped(ref decoder);
                        decoder.ConsumeToken(':');

                        ValueDecoder valueDecoder;
                        if (this.valueDecoders.TryGetValue(key, out valueDecoder))
                        {
                            instance.Add(key, valueDecoder.Decode(ref decoder));
                        }
                        else
                        {
                            instance.Add(key, objectDecoder.Decode(ref decoder));
                        }

                        if (!decoder.TryConsumeToken(','))
                        {
                            break;
                        }
                    }

                    decoder.ConsumeToken('}');

                    return instance;
                }
            }

            private class ObjectMemberDecoder<T> : ValueDecoder<T>
                where T : new()
            {
                private readonly Dictionary<string, MemberDecoder<T>> memberDecoders;
                private readonly ValueDecoder<string> keyDecoder;

                public ObjectMemberDecoder(IEnumerable<EncodingMember> members)
                {
                    this.keyDecoder = GetKeyDecoder<string>();
                    this.memberDecoders = members.ToDictionary(m => m.Name, m => CreateMemberDecoder(m));
                }

                public override int GetMatchingMemberCount(List<string> memberNames)
                {
                    int count = 0;

                    foreach (var name in memberNames)
                    {
                        MemberDecoder<T> tmp;
                        if (this.memberDecoders.TryGetValue(name, out tmp))
                        {
                            count++;
                        }
                    }

                    return count;
                }

                private static MemberDecoder<T> CreateMemberDecoder(EncodingMember member)
                {
                    return (MemberDecoder<T>)Activator.CreateInstance(typeof(MemberDecoder<,>).MakeGenericType(typeof(T), member.Type), new object[] { member });
                }

                public override T DecodeTyped(ref JsonDecoder decoder)
                {
                    var instance = new T();

                    decoder.ConsumeToken('{');

                    int cu;
                    while ((cu = decoder.Peek()) != '\0' && cu != '}')
                    {
                        var key = this.keyDecoder.DecodeTyped(ref decoder);
                        decoder.ConsumeToken(':');

                        MemberDecoder<T> memberDecoder;
                        if (this.memberDecoders.TryGetValue(key, out memberDecoder))
                        {
                            memberDecoder.DecodeInto(ref decoder, ref instance);
                        }
                        else
                        {
                            throw new InvalidOperationException(string.Format("The type '{0}' does not have a member named '{1}'.", typeof(T), key));
                        }

                        if (!decoder.TryConsumeToken(','))
                        {
                            break;
                        }
                    }

                    decoder.ConsumeToken('}');

                    return instance;
                }
            }

            private class MultiObjectMemberDecoder<T> : ValueDecoder<T>
                where T : class
            {
                private readonly List<ValueDecoder> decoders;
                private readonly ValueDecoder<string> keyDecoder;

                public MultiObjectMemberDecoder(IEnumerable<Type> subTypes)
                {
                    this.keyDecoder = GetKeyDecoder<string>();
                    this.decoders = subTypes.Select(t => GetValueDecoder(t)).ToList();
                }

                internal static readonly ObjectPool<List<string>> listPool = new ObjectPool<List<string>>(() => new List<string>(10), list => list.Clear());

                public override T DecodeTyped(ref JsonDecoder decoder)
                {
                    var memberNames = listPool.AllocateFromPool();
                    try
                    {
                        var start = decoder.offset;
                        decoder.ConsumeToken('{');

                        int cu;
                        while ((cu = decoder.Peek()) != '\0' && cu != '}')
                        {
                            var key = this.keyDecoder.DecodeTyped(ref decoder);
                            memberNames.Add(key);
                            decoder.ConsumeToken(':');
                            decoder.SkipObject();
                            if (!decoder.TryConsumeToken(','))
                            {
                                break;
                            }
                        }

                        decoder.offset = start;

                        var valueDecoder = GetBestMatchingDecoder(memberNames);
                        return (T)valueDecoder.Decode(ref decoder);
                    }
                    finally 
                    {
                        listPool.ReturnToPool(memberNames);
                    }
                }

                private ValueDecoder GetBestMatchingDecoder(List<string> memberNames)
                {
                    int bestCount = this.decoders[0].GetMatchingMemberCount(memberNames);
                    var bestDecoder = this.decoders[0];

                    for (int i = 1; i < this.decoders.Count; i++)
                    {
                        var decoder = this.decoders[i];
                        var count = decoder.GetMatchingMemberCount(memberNames);
                        if (count > bestCount)
                        {
                            bestCount = count;
                            bestDecoder = decoder;
                        }
                    }

                    return bestDecoder;
                }
            }

            private abstract class MemberDecoder<TInstance>
            {
                public abstract void DecodeInto(ref JsonDecoder decoder, ref TInstance instance);
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

                public override void DecodeInto(ref JsonDecoder decoder, ref TInstance instance)
                {
                    var value = this.valueDecoder.DecodeTyped(ref decoder);
                    this.member.SetTypedValue(ref instance, value);
                }
            }

            private delegate T DecoderFunc<T>(ref JsonDecoder decoder);

            private class FuncDecoder<T> : ValueDecoder<T>
            {
                private readonly DecoderFunc<T> func;

                public FuncDecoder(DecoderFunc<T> func)
                {
                    this.func = func;
                }

                public override T DecodeTyped(ref JsonDecoder decoder)
                {
                    return this.func(ref decoder);
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

                public override T DecodeTyped(ref JsonDecoder decoder)
                {
                    double dbl;
                    decimal dec;
                    bool hasFraction;
                    var kind = decoder.DecodeNumber(typeof(T), out dbl, out dec, out hasFraction);
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
                private readonly bool intern;

                private static readonly ObjectPool<StringBuilder> builderPool = new ObjectPool<StringBuilder>(() => new StringBuilder(), b => b.Clear());

                public StringDecoder(Func<string, T> parser, bool intern)
                {
                    this.parser = parser;
                    this.intern = intern;
                }

                public override T DecodeTyped(ref JsonDecoder decoder)
                {
                    StringBuilder builder = builderPool.AllocateFromPool();


                    try
                    {
                        decoder.ConsumeToken('"');

                        var start = decoder.offset;

                        int cu;
                        while ((cu = decoder.Peek()) != '\0' && cu != '"')
                        {
                            if (cu == '\\')
                            {
                                builder.Append(DecodeEscapedChar(ref decoder));
                            }
                            else if (builder != null)
                            {
                                if (cu <= char.MaxValue)
                                {
                                    builder.Append((char)cu);
                                }
                                else
                                {
                                    // encode as multi-byte....
                                    throw new NotImplementedException();
                                }

                                decoder.Read();
                            }
                            else
                            {
                                decoder.Read();
                            }
                        }

                        var end = decoder.offset;

                        decoder.ConsumeToken('"');

                        string value = intern
                                ? decoder.InternString(builder)
                                : builder.ToString();

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

                private static char DecodeEscapedChar(ref JsonDecoder decoder)
                {
                    decoder.Read(); // for first \

                    var cu = decoder.Peek();
                    switch (cu)
                    {
                        case '"':
                            decoder.Read();
                            return '"';
                        case '\\':
                            decoder.Read();
                            return '\\';
                        case '/':
                            decoder.Read();
                            return '/';
                        case 'b':
                            decoder.Read();
                            return '\b';
                        case 'f':
                            decoder.Read();
                            return '\f';
                        case 'r':
                            decoder.Read();
                            return '\r';
                        case 'n':
                            decoder.Read();
                            return '\n';
                        case 't':
                            decoder.Read();
                            return '\t';
                        case 'u':
                            decoder.Read();
                            return (char)ParseHexNumber(ref decoder, 4);
                        default:
                            // bad escaped: throw exception here?
                            decoder.Read();
                            return (char)cu;
                    }
                }

                private static uint ParseHexNumber(ref JsonDecoder decoder, int maxLength)
                {
                    var start = decoder.offset;
                    uint number = 0;

                    int cu;
                    while ((cu = decoder.Peek()) != '\0' && decoder.offset - start < maxLength)
                    {
                        uint digit;
                        if (TryGetHexDigitValue(cu, out digit))
                        {
                            decoder.Read();
                            number = (number << 4) + digit;
                        }
                        else
                        {
                            break;
                        }
                    }

                    return number;
                }

                private static bool TryGetHexDigitValue(int ch, out uint value)
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

                public override T? DecodeTyped(ref JsonDecoder decoder)
                {
                    if (decoder.TryConsumeToken("null"))
                    {
                        return null;
                    }
                    else
                    {
                        return this.valueDecoder.DecodeTyped(ref decoder);
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

                public override T DecodeTyped(ref JsonDecoder decoder)
                {
                    if (decoder.TryConsumeToken("null"))
                    {
                        return null;
                    }
                    else
                    {
                        return this.valueDecoder.DecodeTyped(ref decoder);
                    }
                }

                public override int GetMatchingMemberCount(List<string> names)
                {
                    return valueDecoder.GetMatchingMemberCount(names);
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

                if (type.IsAbstract)
                {
                    // if one or more subtypes are also declared then use them as possible subtypes.
                    // TODO: add way to specify subtypes or set of types (do not get them from base type's assembly)
                    var concreteTypes = type.Assembly.GetTypes().Where(t => t.IsClass && !t.IsAbstract && type.IsAssignableFrom(t)).ToList();
                    if (concreteTypes.Count > 0)
                    {
                        return (ValueDecoder)Activator.CreateInstance(typeof(MultiObjectMemberDecoder<>).MakeGenericType(type), new object[] { concreteTypes });
                    }
                }
                else
                {
                    // types with assignable members
                    var members = EncodingMember.GetEncodingMembers(type).Where(m => m.CanWrite).ToList().ToList();
                    if (members.Count > 0 && hasPublicDefaultConstructor)
                    {
                        return (ValueDecoder)Activator.CreateInstance(typeof(ObjectMemberDecoder<>).MakeGenericType(type), new object[] { members });
                    }

                    // can be parsed from string?
                    var stringDecoder = CreateTypedKeyDecoder(type);
                    if (stringDecoder != null)
                    {
                        return stringDecoder;
                    }
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

            private static readonly ConcurrentDictionary<Type, ValueDecoder> keyDecoders
                = new ConcurrentDictionary<Type, ValueDecoder>();

            private static ValueDecoder<T> GetKeyDecoder<T>()
            {
                return (ValueDecoder<T>)GetKeyDecoder(typeof(T));
            }

            private static ValueDecoder GetKeyDecoder(Type type)
            {
                ValueDecoder stringDecoder;

                if (!keyDecoders.TryGetValue(type, out stringDecoder))
                {
                    stringDecoder = keyDecoders.GetOrAdd(type, CreateKeyDecoder(type));
                }

                return stringDecoder;
            }

            private static ValueDecoder CreateKeyDecoder(Type type)
            {
                var nnType = TypeHelper.GetNonNullableType(type);
                ValueDecoder decoder = CreateTypedKeyDecoder(nnType);

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

            private static ValueDecoder CreateTypedKeyDecoder(Type type)
            {
                if (type == typeof(string))
                {
                    return new StringDecoder<string>(s => s, true);
                }

                // can be parsed from string?
                var parseMethod = type.GetMethods(BindingFlags.Static | BindingFlags.Public)
                    .FirstOrDefault(m => m.Name == "Parse" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(string));

                if (parseMethod != null)
                {
                    var dType = typeof(Func<,>).MakeGenericType(typeof(string), type);
                    var parser = Delegate.CreateDelegate(dType, parseMethod, throwOnBindFailure: true);
                    return (ValueDecoder)Activator.CreateInstance(typeof(StringDecoder<>).MakeGenericType(type), new object[] { parser, true });
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

                InitClassDecoder(new StringDecoder<string>(s => s, false));

                InitClassDecoder(new FuncDecoder<object>((ref JsonDecoder d) => d.DecodeObject()));

                InitStructDecoder(new FuncDecoder<bool>((ref JsonDecoder d) =>
                {
                    if (d.TryConsumeToken("true"))
                    {
                        return true;
                    }
                    else if (d.TryConsumeToken("false"))
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