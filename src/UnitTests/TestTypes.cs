using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnitTests
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

    public abstract class BaseType
    {
        public int X;
    }

    public class DerivedType1 : BaseType
    {
        public string Y;
    }

    public class DerivedType2 : BaseType
    {
        public float Z;
    }
}
