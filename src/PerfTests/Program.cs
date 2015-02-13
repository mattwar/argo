using System;
using System.Reflection;

namespace PerfTests
{
    using Test;

    class Program
    {
        static void Main(string[] args)
        {
#if DEBUG
            Console.WriteLine("Do not run performance tests as DEBUG.");
#else
            new TestRunner().RunTests(Assembly.GetEntryAssembly(), args);
#endif
        }
    }
}
