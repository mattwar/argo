using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Test
{
    public class TestBase
    {
        public void RunTests(params string[] testNames)
        {
            if (testNames.Length > 0)
            {
                foreach (var testName in testNames)
                {
                    RunTest(testName);
                }
            }
            else
            {
                var testType = this.GetType();
                var testMethods = testType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
                                          .Where(m => m.Name.StartsWith("Test") && m.ReturnType == typeof(void) && m.GetParameters().Length == 0)
                                          .ToList();

                foreach (var testMethod in testMethods)
                {
                    RunTest(testMethod);
                }
            }
        }

        private void RunTest(string name)
        {
            var testMethod = this.GetType().GetMethod(name, BindingFlags.Public | BindingFlags.Instance);
            if (testMethod != null && testMethod.ReturnType == typeof(void) && testMethod.GetParameters().Length == 0)
            {
                RunTest(testMethod);
            }
        }

        private void RunTest(MethodInfo testMethod)
        {
            var testAction = (Action)Delegate.CreateDelegate(typeof(Action), this, testMethod);

            var color = Console.ForegroundColor;

            try
            {
                Console.Write("Test {0} - ", testMethod.Name);
                testAction();

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Succeeded");
                Console.ForegroundColor = color;
            }
            catch (Exception e)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Failed");

                Console.ForegroundColor = color;
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }
        }

        protected static void AssertEqual<T>(T expected, T actual)
        {
            var comparer = EqualityComparer<T>.Default;
            if (!comparer.Equals(expected, actual))
            {
                throw new InvalidOperationException(string.Format("Assertion Failed:\r\n    Expected: {0}\r\n    Actual  : {1}", expected, actual));
            }
        }
    }
}
