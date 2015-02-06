using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test
{
    using Argo;

    public class JsonEncodingTests : TestBase
    {
        private static void TestEncode<T>(T value, string expected)
        {
            var actual = Json.Encode(value);
            AssertEqual(expected, actual);
        }

        public void TestEncodePrimitives()
        {
            TestEncode<byte>(0, "0");
            TestEncode<byte>(1, "1");
            TestEncode(byte.MaxValue, byte.MaxValue.ToString());
            TestEncode(byte.MinValue, byte.MinValue.ToString());
            TestEncode<byte?>(null, "null");

            TestEncode<sbyte>(0, "0");
            TestEncode<sbyte>(1, "1");
            TestEncode<sbyte>(-1, "-1");
            TestEncode(sbyte.MaxValue, sbyte.MaxValue.ToString());
            TestEncode(sbyte.MinValue, sbyte.MinValue.ToString());
            TestEncode<sbyte?>(null, "null");

            TestEncode<ushort>(0, "0");
            TestEncode<ushort>(1, "1");
            TestEncode(ushort.MaxValue, ushort.MaxValue.ToString());
            TestEncode(ushort.MinValue, ushort.MinValue.ToString());
            TestEncode<ushort?>(null, "null");

            TestEncode<short>(0, "0");
            TestEncode<short>(1, "1");
            TestEncode<short>(-1, "-1");
            TestEncode(short.MaxValue, short.MaxValue.ToString());
            TestEncode(short.MinValue, short.MinValue.ToString());
            TestEncode<short?>(null, "null");

            TestEncode<uint>(0, "0");
            TestEncode<uint>(1, "1");
            TestEncode(uint.MaxValue, uint.MaxValue.ToString());
            TestEncode(uint.MinValue, uint.MinValue.ToString());
            TestEncode<int?>(null, "null");

            TestEncode(0, "0");
            TestEncode(1, "1");
            TestEncode(-1, "-1");
            TestEncode(int.MaxValue, int.MaxValue.ToString());
            TestEncode(int.MinValue, int.MinValue.ToString());
            TestEncode<int?>(null, "null");

            TestEncode<ulong>(0, "0");
            TestEncode<ulong>(1, "1");
            TestEncode(ulong.MaxValue, ulong.MaxValue.ToString());
            TestEncode(ulong.MinValue, ulong.MinValue.ToString());
            TestEncode<ulong?>(null, "null");

            TestEncode<long>(0, "0");
            TestEncode<long>(1, "1");
            TestEncode<long>(-1, "-1");
            TestEncode(long.MaxValue, long.MaxValue.ToString());
            TestEncode(long.MinValue, long.MinValue.ToString());
            TestEncode<long?>(null, "null");

            TestEncode<float>(0, "0");
            TestEncode<float>(1, "1");
            TestEncode<float>(-1, "-1");
            TestEncode<float>(12345.67f, "12345.67");
            TestEncode<float>(float.Epsilon, float.Epsilon.ToString());
            TestEncode(float.MaxValue, float.MaxValue.ToString());
            TestEncode(float.MinValue, float.MinValue.ToString());
            TestEncode<float?>(null, "null");

            TestEncode<double>(0, "0");
            TestEncode<double>(1, "1");
            TestEncode<double>(-1, "-1");
            TestEncode<double>(12345.6789, "12345.6789");
            TestEncode<double>(double.Epsilon, double.Epsilon.ToString());
            TestEncode(double.MaxValue, double.MaxValue.ToString());
            TestEncode(double.MinValue, double.MinValue.ToString());
            TestEncode<double?>(null, "null");

            TestEncode<decimal>(0, "0");
            TestEncode<decimal>(0.00m, "0.00");
            TestEncode<decimal>(1, "1");
            TestEncode<decimal>(1.00m, "1.00");
            TestEncode<decimal>(-1, "-1");
            TestEncode<decimal>(-1.00m, "-1.00");
            TestEncode<decimal>(12345.6789m, "12345.6789");
            TestEncode(decimal.MaxValue, decimal.MaxValue.ToString());
            TestEncode(decimal.MinValue, decimal.MinValue.ToString());
            TestEncode<decimal?>(null, "null");
        }

        public void TestStrings()
        {
            TestEncode("Hello JSON", @"""Hello JSON""");
            TestEncode<string>(null, "null");
        }

        public void TestOther()
        {
            var guid = Guid.NewGuid();
            TestEncode(guid, $@"""{guid.ToString()}""");
        }

        public void TestClassWithFields()
        {
            TestEncode<ClassWithFields<int, int>>(null, "null");

            TestEncode(
                NewClassWithFields(100, 200),
                @"{""X"": 100, ""Y"": 200}");

            TestEncode(
                NewClassWithFields("abc", (string)null),
                @"{""X"": ""abc"", ""Y"": null}");

            TestEncode(
                NewClassWithFields((object)"abc", (object)null),
                @"{""X"": ""abc"", ""Y"": null}");

            TestEncode(
                NewClassWithFields((object)123, (object)null),
                @"{""X"": 123, ""Y"": null}");

            TestEncode(
                NewClassWithFields(123, NewClassWithFields(456, 789)),
                @"{""X"": 123, ""Y"": {""X"": 456, ""Y"": 789}}");
        }

        private static ClassWithFields<Tx, Ty> NewClassWithFields<Tx, Ty>(Tx x, Ty y)
        {
            return new ClassWithFields<Tx, Ty> { X = x, Y = y };
        }

        private class ClassWithFields<Tx, Ty>
        {
            public Tx X;
            public Ty Y;
        }

        private static ClassWithProperties<Tx, Ty> NewClassWithProperties<Tx, Ty>(Tx x, Ty y)
        {
            return new ClassWithProperties<Tx, Ty> { X = x, Y = y };
        }

        private class ClassWithProperties<Tx, Ty>
        {
            public Tx X { get; set; }
            public Ty Y { get; set; }
        }
    }
}
