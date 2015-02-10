using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Test
{
    public class Assert
    {
        /// <summary>
        /// Returns true if the two values are considered equal (via the Equals method).
        /// </summary>
        public static void Equal<T>(T expected, T actual)
        {
            if (!AreEqual(expected, actual))
            {
                throw new InvalidOperationException(string.Format("Assertion Failed:\r\n    Expected: {0}\r\n    Actual  : {1}", expected, actual));
            }
        }

        private static bool AreEqual<T>(T expected, T actual)
        {
            var comparer = EqualityComparer<T>.Default;
            return comparer.Equals(expected, actual);
        }

        /// <summary>
        /// Returns true if the two values are structurally similar.
        /// </summary>
        public static void Similar<T>(T expected, T actual)
        {
            var comparer = EqualityComparer<T>.Default;
            if (!AreEqual(expected, actual) && !AreSimilar(expected, actual))
            {
                throw new InvalidOperationException(string.Format("Assertion Failed:\r\n    Expected: {0}\r\n    Actual  : {1}", expected, actual));
            }
        }

        private static bool AreSimilar(object expected, object actual)
        {
            if (expected == actual)
            {
                return true;
            }

            if (expected == null || actual == null)
            {
                return false;
            }

            if (expected.Equals(actual))
            {
                return true;
            }

            var type = expected.GetType();
            if (type != actual.GetType())
            {
                return false;
            }

            // compare sequences
            if (typeof(IEnumerable).IsAssignableFrom(type))
            {
                var expEnum = ((IEnumerable)expected).GetEnumerator();
                var actEnum = ((IEnumerable)actual).GetEnumerator();

                while (expEnum.MoveNext())
                {
                    // actual has less elements
                    if (!actEnum.MoveNext())
                    {
                        return false;
                    }

                    if (!AreSimilar(expEnum.Current, actEnum.Current))
                    {
                        return false;
                    }
                }

                // actual has more elements
                if (actEnum.MoveNext())
                {
                    return false;
                }

                return true;
            }

            // compare members
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (fields.Length > 0)
            {
                for (int i = 0; i < fields.Length; i++)
                {
                    var f = fields[i];
                    if (!AreSimilar(f.GetValue(expected), f.GetValue(actual)))
                    {
                        return false;
                    }
                }

                return true;
            }

            return false;
        }

        public static void Throws<TException>(Action action)
            where TException : Exception
        {
            bool threw = false;

            try
            {
                action();
            }
            catch (TException)
            {
                threw = true;
            }

            if (!threw)
            {
                throw new InvalidOperationException("Expected exception not thrown");
            }
        }
    }
}
