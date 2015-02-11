using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PerfTests
{
    using Test;

    class Program
    {
        static void Main(string[] args)
        {
#if !DEBUG
            new TestRunner().RunTests(args);
#else
            Console.WriteLine("Do not run performance tests as DEBUG.");
#endif
        }
    }
}
