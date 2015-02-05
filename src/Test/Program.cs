using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Argo;

namespace Test
{
    class Program
    {
        static void Main(string[] args)
        {
            var encoding = JsonEncoding.Instance;
            //var json = encoding.Encode(new MyClass { X = 100, Y = 250 });

            var value = JsonEncoding.Instance.Decode<MyList>("[1, 2, 3]");
        }
    }

    public class MyClass
    {
        public int X;
        public int Y;
    }

    public class MyList : IEnumerable<int>
    {
        private readonly int[] items;

        public MyList(IEnumerable<int> items)
        {
            this.items = items.ToArray();
        }

        public IEnumerator<int> GetEnumerator()
        {
            return ((IEnumerable<int>)this.items).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
}
