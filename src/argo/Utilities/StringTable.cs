using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Utilities
{
    /// <summary>
    /// A table for interning strings.
    /// </summary>
    internal class StringTable : IEnumerable<string>
    {
        private Bucket[] buckets;
        private int count;

        public StringTable(int size)
        {
            this.buckets = new Bucket[size];
            this.count = 0;
        }

        public StringTable()
            : this(17)
        {
        }

        public int Count
        {
            get { return this.count; }
        }

        public string GetOrAdd(string text)
        {
            return this.GetOrAdd(text, 0, text.Length);
        }

        public string GetOrAdd(string text, int start, int length)
        {
            var hash = this.Hash(text, start, length);
            var ibucket = hash % this.buckets.Length;

            var bucket = this.buckets[ibucket];
            if (bucket != null)
            {
                var result = this.FindInBucketList(bucket, hash, text, start, length);
                if (result != null)
                {
                    return result;
                }
            }

            if (start != 0 || length != text.Length)
            {
                text = text.Substring(start, length);
            }

            var newBucket = new Bucket { Hash = hash, String = text, Next = bucket };
            this.buckets[ibucket] = newBucket;
            this.count++;

            return text;
        }

        public string GetOrAdd(StringBuilder text)
        {
            var hash = this.Hash(text);
            var ibucket = hash % this.buckets.Length;

            var bucket = this.buckets[ibucket];
            if (bucket != null)
            {
                var result = this.FindInBucketList(bucket, hash, text);
                if (result != null)
                {
                    return result;
                }
            }

            var str = text.ToString();
            var newBucket = new Bucket { Hash = hash, String = str, Next = bucket };
            this.buckets[ibucket] = newBucket;
            this.count++;

            return str;
        }

        private string FindInBucketList(Bucket firstBucket, int hash, string text, int start, int length)
        {
            for (var bucket = firstBucket; bucket != null; bucket = bucket.Next)
            {
                if (this.Equals(bucket, hash, text, start, length))
                {
                    return bucket.String;
                }
            }

            return null;
        }

        private string FindInBucketList(Bucket firstBucket, int hash, StringBuilder text)
        {
            for (var bucket = firstBucket; bucket != null; bucket = bucket.Next)
            {
                if (this.Equals(bucket, hash, text))
                {
                    return bucket.String;
                }
            }

            return null;
        }

        /// <summary>
        /// Does exact (ordinal) comparison.
        /// </summary>
        private bool Equals(Bucket bucket, int hash, string text, int start, int length)
        {
            if (bucket.Hash != hash || bucket.String.Length != length)
            {
                return false;
            }

            for (int i = 0, t = start; i < length; i++, t++)
            {
                if (bucket.String[i] != text[t])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Does exact (ordinal) comparison.
        /// </summary>
        private bool Equals(Bucket bucket, int hash, StringBuilder text)
        {
            if (bucket.Hash != hash || bucket.String.Length != text.Length)
            {
                return false;
            }

            for (int i = 0; i < text.Length; i++)
            {
                if (bucket.String[i] != text[i])
                {
                    return false;
                }
            }

            return true;
        }

        private int Hash(string text)
        {
            return Hash(text, 0, text.Length);
        }

        private int Hash(string text, int start, int length)
        {
            int hash = 0;
            for (int i = 0; i < length; i++)
            {
                hash = hash + i + text[start + i];
            }

            return hash;
        }

        private int Hash(StringBuilder text)
        {
            int hash = 0;
            for (int i = 0; i < text.Length; i++)
            {
                hash = hash + i + text[i];
            }

            return hash;
        }

        public Enumerator GetEnumerator()
        {
            return new Enumerator(this);
        }

        IEnumerator<string> IEnumerable<string>.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

		// this is visible in the debugger
        private string[] DebugList
        {
            get { return this.ToArray(); }
        }

        private class Bucket
        {
            internal int Hash;
            internal string String;
            internal Bucket Next;
        }

        public struct Enumerator : IEnumerator<string>, IEnumerator
        {
            private readonly Bucket[] buckets;
            private Bucket currentBucket;
            private int currentIndex;

            internal Enumerator(StringTable table)
            {
                this.buckets = table.buckets;
                this.currentBucket = null;
                this.currentIndex = 0;
            }

            public string Current
            {
                get { return this.currentBucket != null ? this.currentBucket.String : null; }
            }

            object IEnumerator.Current
            {
                get { return this.Current; }
            }

            public void Dispose()
            {
            }

            public bool MoveNext()
            {
                if (this.currentBucket != null)
                {
                    this.currentBucket = this.currentBucket.Next;
                    if (this.currentBucket != null)
                    {
                        return true;
                    }

                    this.currentIndex++;
                    this.currentBucket = null;
                }

                for (; this.currentIndex < this.buckets.Length; this.currentIndex++)
                {
                    this.currentBucket = this.buckets[this.currentIndex];
                    if (this.currentBucket != null)
                    {
                        return true;
                    }
                }

                return false;
            }

            public void Reset()
            {
                this.currentBucket = null;
                this.currentIndex = 0;
            }
        }
    }
}