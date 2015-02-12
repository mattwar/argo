using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PerfTests
{
    using Argo;
    using Test;

    public class ComparisonTests
    {
        public void TestDecoding()
        {
            var iterations = 10000;

            TestDecoding<int>("Int", iterations, @"123");
            TestDecoding<string>("String", iterations, @"""abcefg""");
            TestDecoding<string>("Escaped String", iterations, @"""abc\r\n\u1234efg""");
            TestDecoding<int[]>("Int Array", iterations, @"[1, 2, 3, 4, 5, 6, 7, 8, 9, 10]");
            TestDecoding<List<int>>("Int List", iterations, @"[1, 2, 3, 4, 5, 6, 7, 8, 9, 10]");
            TestDecoding<string[]>("String Array", iterations, @"[""A"", ""B"", ""C"", ""D""]");
            TestDecoding<Dictionary<string, int>>("Dictionary", iterations, @"{""A"": 1, ""B"": 2, ""C"": 3}");
            TestDecoding<TestClass<string, int>>("Class", iterations, @"{""X"": ""A"", ""Y"": 1}");
            TestDecoding<TestStruct<string, int>>("Struct", iterations, @"{""X"": ""A"", ""Y"": 1}");
        }

        private void TestDecoding<T>(string title, int iterations, string json)
        {
            var argoTime = RunTimedTest(iterations, n =>
            {
                var decoded = Json.Decode<T>(json);
            });

            var aspTime = RunTimedTest(iterations, n =>
            {
                var serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
                var decoded = serializer.Deserialize<T>(json);
            });

            var newtonTime = RunTimedTest(iterations, n =>
            {
                var decoded = Newtonsoft.Json.JsonConvert.DeserializeObject<T>(json);
            });

            Console.WriteLine();
            Console.WriteLine(title);
            Console.WriteLine("Argo:   {0}", argoTime * 100000);
            Console.WriteLine("ASP:    {0}", aspTime * 100000);
            Console.WriteLine("Newton: {0}", newtonTime * 100000);
        }

        public void TestEncoding()
        {
            var iterations = 10000;

            TestEncoding("Int", iterations, 123);
            TestEncoding("String", iterations, "abcefg");
            TestEncoding("Escaped String", iterations, @"abc\r\n\u1234efg");
            TestEncoding("Int Array", iterations, new int[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
            TestEncoding("Int List", iterations, new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
            TestEncoding("String Array", iterations, new string[] { "A", "B", "C", "D" });
            TestEncoding("Dictionary", iterations, new Dictionary<string, int> { { "A", 1 }, { "B", 2 }, { "C", 3 } });
            TestEncoding("Class", iterations, new TestClass<string, int> { X = "A", Y = 1 });
            TestEncoding("Struct", iterations, new TestStruct<string, int> { X = "A", Y = 1 });
        }

        private void TestEncoding<T>(string title, int iterations, T value)
        {
            var argoTime = RunTimedTest(iterations, n =>
            {
                var json = Json.Encode(value);
            });

            var aspTime = RunTimedTest(iterations, n =>
            {
                var serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
                var json = serializer.Serialize(value);
            });

            var newtonTime = RunTimedTest(iterations, n =>
            {
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(value);
            });

            Console.WriteLine();
            Console.WriteLine(title);
            Console.WriteLine("Argo:   {0}", argoTime * 100000);
            Console.WriteLine("ASP:    {0}", aspTime * 100000);
            Console.WriteLine("Newton: {0}", newtonTime * 100000);
        }

        private double RunTimedTest(int iterations, Action<int> action)
        {
            action(0); // throw out the first one  (makes sure code is loaded)

            var timer = new System.Diagnostics.Stopwatch();
            timer.Start();

            for (int i = 1; i <= iterations; i++)
            {
                action(i);
            }

            timer.Stop();
            return timer.Elapsed.TotalSeconds / iterations;
        }
    }
}
