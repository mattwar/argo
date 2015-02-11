using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test
{
    public class TestClass<Tx, Ty>
    {
        public Tx X { get; set; }
        public Ty Y;
    }

    public struct TestStruct<Tx, Ty>
    {
        public Tx X { get; set; }
        public Ty Y;
    }
}
