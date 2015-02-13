using System;
using System.Reflection;

namespace UnitTests
{
    using Test;

    class Program
    {
        static void Main(string[] args)
        {
            new TestRunner().RunTests(Assembly.GetEntryAssembly(), args);
        }
    }
}
