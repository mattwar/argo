using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Test
{
    public class TestRunner
    {
        public bool versbose;

        public TestRunner()
        {
        }

        /// <summary>
        /// Runs test methods on test classes found in the test assembly.
        /// Test classes have names ending in "Tests", are public with a public no-arg constructor.
        /// Test methods have names starting with "Test", are public, void returning and no parameters.
        /// </summary>
        /// <param name="testAssembly">The assembly containing the test classes.</param>
        /// <param name="commandLineArgs">One or more test type or test method names.</param>
        public void RunTests(Assembly testAssembly, string[] commandLineArgs)
        {
            var testTypes = GetTestTypes(testAssembly);

            var testNames = new List<string>();
            this.ParseCommandLineArgs(commandLineArgs, out testNames, out this.versbose);

            if (testNames.Count == 0)
            {
                RunAllTests(testTypes);
            }
            else
            {
                var testTypeMap = testTypes.ToDictionary(t => t.Name, t => t);
                var testMethodLookup = testTypes.SelectMany(t => GetTestMethods(t)).ToLookup(m => m.Name);

                // name can be a test type name or a test method name
                foreach (var name in testNames)
                {
                    var testName = name.EndsWith("Tests") ? name : name + "Tests";
                    Type testType;
                    if (testTypeMap.TryGetValue(testName, out testType))
                    {
                        RunAllTests(testType);
                        continue;
                    }

                    var methodName = name.StartsWith("Test") ? name : "Test" + name;
                    var methodsByTestType = testMethodLookup[methodName].GroupBy(m => m.DeclaringType);
                    foreach (var methods in methodsByTestType)
                    {
                        RunAllTests(methods.Key, methods);
                    }
                }
            }
        }

        private void ParseCommandLineArgs(
            string[] args, 
            out List<string> testNames,
            out bool verbose)
        {
            testNames = new List<string>();
            verbose = true;

            foreach (var arg in args)
            {
                if (arg.StartsWith("/") || arg.StartsWith("-"))
                {
                    var parts = arg.Substring(1, arg.Length - 1).Split(':');
                    if (parts.Length == 0)
                    {
                        continue;
                    }

                    switch (parts[0].ToLower())
                    {
                        case "v":
                        case "verbose":
                            if (parts.Length > 1)
                            {
                                if (parts[1] == "-" || parts[1] == "false")
                                {
                                    verbose = false;
                                }
                                else if (parts[1] == "+" || parts[1] == "true")
                                {
                                    verbose = true;
                                }
                            }
                            else
                            {
                                verbose = true;
                            }
                            break;
                    }
                }
                else
                {
                    testNames.Add(arg);
                }
            }
        }

        private static Type[] GetTestTypes(Assembly testAssembly)
        {
            return testAssembly.GetTypes()
                   .Where(t => t.Name.EndsWith("Tests") && t.IsPublic && t.IsClass && !t.IsAbstract)
                   .ToArray();
        }

        private MethodInfo[] GetTestMethods(Type testType)
        {
            return testType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
                           .Where(m => m.Name.StartsWith("Test") && m.ReturnType == typeof(void) && m.GetParameters().Length == 0)
                           .ToArray();
        }

        private void RunAllTests(params Type[] testTypes)
        {
            foreach (var testType in testTypes)
            {
                RunAllTests(testType, GetTestMethods(testType));
            }
        }

        private void RunAllTests(Type testType, IEnumerable<MethodInfo> testMethods)
        {
            var instance = Activator.CreateInstance(testType, new object[] { });
            this.SetupTests(instance);

            foreach (var testMethod in testMethods)
            {
                RunTest(instance, testMethod);
            }

            this.TeardownTests(instance);
        }

        private Func<MethodInfo, bool> canRunTest;
        private int passed;
        private int skipped;
        private int failed;

        private void SetupTests(object testInstance)
        {
            var testType = testInstance.GetType();

            var canRunMethod = testType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy)
                                       .FirstOrDefault(m => m.Name == "CanRunTest" && m.ReturnType == typeof(bool) && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(MethodInfo));

            this.canRunTest = canRunMethod != null ? (Func<MethodInfo, bool>)Delegate.CreateDelegate(typeof(Func<MethodInfo, bool>), testInstance, canRunMethod) : null;

            var color = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(testType.Name);
            Console.ForegroundColor = color;
        }

        private void TeardownTests(object testInstance)
        {
            var color = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("  {0} passed", this.passed);
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("  {0} skipped", this.skipped);
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write("  {0} failed", this.failed);
            Console.ForegroundColor = color;
            Console.WriteLine();
        }

        private bool CanRunTest(object testInstance, MethodInfo testMethod)
        {
            return this.canRunTest != null ? this.canRunTest(testMethod) : true;
        }

        private void RunTest(object testInstance, MethodInfo testMethod)
        {
            var testAction = (Action)Delegate.CreateDelegate(typeof(Action), testInstance, testMethod);

            var color = Console.ForegroundColor;

            try
            {
                if (this.versbose)
                {
                    Console.Write("  {0} - ", testMethod.Name);
                }

                if (CanRunTest(testInstance, testMethod))
                {
                    testAction();

                    this.passed++;

                    if (this.versbose)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("Succeeded");
                    }
                }
                else
                {
                    this.skipped++;

                    if (this.versbose)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("Skipped");
                    }
                }
            }
            catch (Exception e)
            {
                this.failed++;

                if (this.versbose)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Failed");
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.WriteLine(e.Message);
                    Console.ForegroundColor = color;
                    Console.WriteLine(e.StackTrace);
                }
                else
                {
                    Console.Write("  {0} - ", testMethod.Name);
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Failed", testMethod.Name);
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.WriteLine(e.Message);
                    Console.ForegroundColor = color;
                    Console.WriteLine(e.StackTrace);
                }
            }
            finally
            {
                Console.ForegroundColor = color;
            }
        }
    }
}
