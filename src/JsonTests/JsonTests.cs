using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnitTests
{
    using Argo;
    using Test;

    public class UnitTests
    {
        private static void TestEncode<T>(T value, string expected)
        {
            var actual = Json.Encode(value);
            Assert.Equal(expected, actual);
        }

        private static void TestDecode<T>(string text, T expected)
        {
            var actual = Json.Decode<T>(text);
            Assert.Similar(expected, actual);
        }

        private static void TestEncodeDecode<T>(T value, string expected)
        {
            TestEncode<T>(value, expected);
            TestDecode<T>(expected, value);
        }

        public void TestNumbers()
        {
            TestEncodeDecode<byte>(0, "0");
            TestEncodeDecode<byte>(1, "1");
            TestEncodeDecode(byte.MaxValue, byte.MaxValue.ToString());
            TestEncodeDecode(byte.MinValue, byte.MinValue.ToString());
            TestEncodeDecode<byte?>(null, "null");

            TestEncodeDecode<sbyte>(0, "0");
            TestEncodeDecode<sbyte>(1, "1");
            TestEncodeDecode<sbyte>(-1, "-1");
            TestEncodeDecode(sbyte.MaxValue, sbyte.MaxValue.ToString());
            TestEncodeDecode(sbyte.MinValue, sbyte.MinValue.ToString());
            TestEncodeDecode<sbyte?>(null, "null");

            TestEncodeDecode<ushort>(0, "0");
            TestEncodeDecode<ushort>(1, "1");
            TestEncodeDecode(ushort.MaxValue, ushort.MaxValue.ToString());
            TestEncodeDecode(ushort.MinValue, ushort.MinValue.ToString());
            TestEncodeDecode<ushort?>(null, "null");

            TestEncodeDecode<short>(0, "0");
            TestEncodeDecode<short>(1, "1");
            TestEncodeDecode<short>(-1, "-1");
            TestEncodeDecode(short.MaxValue, short.MaxValue.ToString());
            TestEncodeDecode(short.MinValue, short.MinValue.ToString());
            TestEncodeDecode<short?>(null, "null");

            TestEncodeDecode<uint>(0, "0");
            TestEncodeDecode<uint>(1, "1");
            TestEncodeDecode(uint.MaxValue, uint.MaxValue.ToString());
            TestEncodeDecode(uint.MinValue, uint.MinValue.ToString());
            TestEncodeDecode<int?>(null, "null");

            TestEncodeDecode(0, "0");
            TestEncodeDecode(1, "1");
            TestEncodeDecode(-1, "-1");
            TestEncodeDecode(int.MaxValue, int.MaxValue.ToString());
            TestEncodeDecode(int.MinValue, int.MinValue.ToString());
            TestEncodeDecode<int?>(null, "null");

            TestEncodeDecode<ulong>(0, "0");
            TestEncodeDecode<ulong>(1, "1");
            TestEncodeDecode(ulong.MaxValue, ulong.MaxValue.ToString());
            TestEncodeDecode(ulong.MinValue, ulong.MinValue.ToString());
            TestEncodeDecode<ulong?>(null, "null");

            TestEncodeDecode<long>(0, "0");
            TestEncodeDecode<long>(1, "1");
            TestEncodeDecode<long>(-1, "-1");
            TestEncodeDecode(long.MaxValue, long.MaxValue.ToString());
            TestEncodeDecode(long.MinValue, long.MinValue.ToString());
            TestEncodeDecode<long?>(null, "null");

            TestEncodeDecode<float>(0, "0");
            TestEncodeDecode<float>(1, "1");
            TestEncodeDecode<float>(-1, "-1");
            TestEncodeDecode<float>(12345.67f, "12345.67");
            TestEncodeDecode<float>(float.Epsilon, float.Epsilon.ToString("R", CultureInfo.InvariantCulture));
            TestEncodeDecode(float.MaxValue, float.MaxValue.ToString("R", CultureInfo.InvariantCulture));
            TestEncodeDecode(float.MinValue, float.MinValue.ToString("R", CultureInfo.InvariantCulture));
            TestEncodeDecode<float?>(null, "null");

            TestEncodeDecode<double>(0, "0");
            TestEncodeDecode<double>(1, "1");
            TestEncodeDecode<double>(-1, "-1");
            TestEncodeDecode<double>(12345.6789, "12345.6789");
            TestEncodeDecode<double>(double.Epsilon, double.Epsilon.ToString("R", CultureInfo.InvariantCulture));
            TestEncodeDecode(double.MaxValue, double.MaxValue.ToString("R", CultureInfo.InvariantCulture));
            TestEncodeDecode(double.MinValue, double.MinValue.ToString("R", CultureInfo.InvariantCulture));
            TestEncodeDecode<double?>(null, "null");

            TestEncodeDecode<decimal>(0, "0");
            TestEncodeDecode<decimal>(0.00m, "0.00");
            TestEncodeDecode<decimal>(1, "1");
            TestEncodeDecode<decimal>(1.00m, "1.00");
            TestEncodeDecode<decimal>(-1, "-1");
            TestEncodeDecode<decimal>(-1.00m, "-1.00");
            TestEncodeDecode<decimal>(12345.6789m, "12345.6789");
            TestEncodeDecode(decimal.MaxValue, decimal.MaxValue.ToString(CultureInfo.InvariantCulture));
            TestEncodeDecode(decimal.MinValue, decimal.MinValue.ToString(CultureInfo.InvariantCulture));
            TestEncodeDecode<decimal?>(null, "null");
        }

        public void TestStrings()
        {
            TestEncodeDecode("Hello JSON", @"""Hello JSON""");
            TestEncodeDecode<string>(null, "null");

            // encoded characters
            TestEncodeDecode("\"", @"""\""""");
            TestEncodeDecode("\\", @"""\\""");
            TestEncodeDecode("\b", @"""\b""");
            TestEncodeDecode("\f", @"""\f""");
            TestEncodeDecode("\r", @"""\r""");
            TestEncodeDecode("\n", @"""\n""");
            TestEncodeDecode("\t", @"""\t""");
            TestEncodeDecode('\0', @"""\u0000""");
            TestEncodeDecode((char)1, @"""\u0001""");

            TestEncode("/", @"""/"""); // doesn't need to be encoded.
            TestDecode(@"""\/""", "/"); // but can be decoded.

            // other unicode escapes that can be decoded
            TestDecode(@"""\u0024""", (char)0x24);
            TestDecode(@"""\uffFF""", (char)0xffff);
        }

        public void TestParsables()
        {
            var guid = Guid.Parse("24b56d9f-2597-447a-92d0-4f5315e5b2be");               
            TestEncodeDecode(guid, $@"""{guid.ToString()}""");
        }

        public void TestLists()
        {
            var ints = new int[] { 1, 2, 3 };
            TestEncodeDecode(ints, @"[1, 2, 3]");

            // note: objects may not round-trip back to the same type
            var objects = new object[] { 1, 2.3, "4" };
            TestEncodeDecode(objects, @"[1, 2.3, ""4""]");

            var strings = new string[] { "A", "B", "C" };
            TestEncodeDecode(strings, @"[""A"", ""B"", ""C""]");

            TestEncodeDecode(ints.ToList(), @"[1, 2, 3]");

            var enumerableInts = (IEnumerable<int>)ints;
            TestEncode(enumerableInts, "[1, 2, 3]");
            var decodedEnumerableInts = Json.Decode<IEnumerable<int>>("[1, 2, 3]");
            Assert.Equal(3, decodedEnumerableInts.Count());
            Assert.Equal(1, decodedEnumerableInts.ElementAt(0));
            Assert.Equal(2, decodedEnumerableInts.ElementAt(1));
            Assert.Equal(3, decodedEnumerableInts.ElementAt(2));
        }

        public void TestDictionaries()
        {
            var d = new Dictionary<string, int> { { "A", 1 }, { "B", 2 }, { "C", 3 } };
            TestEncodeDecode(d, @"{""A"": 1, ""B"": 2, ""C"": 3}");

            var dints = new Dictionary<int, string> { { 1, "A" }, { 2, "B" }, { 3, "C" } };
            TestEncodeDecode(dints, @"{""1"": ""A"", ""2"": ""B"", ""3"": ""C""}");

            var imd = d.ToImmutableDictionary();
            var encoded = Json.Encode(imd);
            var decoded = Json.Decode<ImmutableDictionary<string, int>>(encoded);
            Assert.Equal(imd.Count, decoded.Count);
            Assert.Equal(imd["A"], decoded["A"]);
            Assert.Equal(imd["B"], decoded["B"]);
            Assert.Equal(imd["C"], decoded["C"]);
        }

        public void TestClassWithFields()
        {
            TestEncode<TestClass<int, int>>(null, "null");

            TestEncode(
                NewTestClass(100, 200),
                @"{""X"": 100, ""Y"": 200}");

            TestEncode(
                NewTestClass("abc", (string)null),
                @"{""X"": ""abc"", ""Y"": null}");

            TestEncode(
                NewTestClass((object)"abc", (object)null),
                @"{""X"": ""abc"", ""Y"": null}");

            TestEncode(
                NewTestClass((object)123, (object)null),
                @"{""X"": 123, ""Y"": null}");

            TestEncode(
                NewTestClass(123, NewTestClass(456, 789)),
                @"{""X"": 123, ""Y"": {""X"": 456, ""Y"": 789}}");
        }

        private static TestClass<Tx, Ty> NewTestClass<Tx, Ty>(Tx x, Ty y)
        {
            return new TestClass<Tx, Ty> { X = x, Y = y };
        }
    }
}
