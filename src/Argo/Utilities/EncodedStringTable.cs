using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Utilities
{
    /// <summary>
    /// A table for interning encoded strings.
    /// </summary>
    internal class EncodedStringTable : IEnumerable<string>
    {
        private readonly Encoding encoding;
        private Bucket[] buckets;
        private int count;

        public EncodedStringTable(Encoding encoding, int size)
        {
            this.encoding = encoding;
            this.buckets = new Bucket[size];
            this.count = 0;
        }

        public EncodedStringTable(Encoding encoding)
            : this(encoding, 17)
        {
        }

        public int Count
        {
            get { return this.count; }
        }

        public string GetOrAdd(ReadOnlySpan<byte> text)
        {
            return this.GetOrAdd(text, 0, text.Length);
        }

        public string GetOrAdd(ReadOnlySpan<byte> text, int start, int length)
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
                text = text.Slice(start, length);
            }

            byte[] bytes = new byte[text.Length];
            text.CopyTo(bytes);
            var memoryText = new Memory<byte>(bytes);
            
            var newBucket = new Bucket { Hash = hash, Text = memoryText, Next = bucket };
            this.buckets[ibucket] = newBucket;
            this.count++;

            return newBucket.GetString(this.encoding);
        }

        private string FindInBucketList(Bucket firstBucket, int hash, ReadOnlySpan<byte> text, int start, int length)
        {
            for (var bucket = firstBucket; bucket != null; bucket = bucket.Next)
            {
                if (this.Equals(bucket, hash, text, start, length))
                {
                    return bucket.GetString(this.encoding);
                }
            }

            return null;
        }

        /// <summary>
        /// Does exact (ordinal) comparison.
        /// </summary>
        private bool Equals(Bucket bucket, int hash, ReadOnlySpan<byte> text, int start, int length)
        {
            if (bucket.Hash != hash || bucket.Text.Length != length)
            {
                return false;
            }

            var bucketSpan = bucket.Text.Span;

            for (int i = 0, t = start; i < length; i++, t++)
            {
                if (bucketSpan[i] != text[t])
                {
                    return false;
                }
            }

            return true;
        }

        private int Hash(ReadOnlySpan<byte> text, int start, int length)
        {
            int hash = 0;
            for (int i = 0; i < length; i++)
            {
                hash = hash + i + text[start + i];
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
            internal Memory<byte> Text;
            internal Bucket Next;

            private string _string = null;
            public string GetString(Encoding encoding)
            {
                if (_string == null)
                {
                    var decoder = encoding.GetDecoder();
                    var len = decoder.GetCharCount(this.Text.Span, flush: true);
                    Span<char> chars = stackalloc char[len];
                    decoder.GetChars(this.Text.Span, chars, flush: true);
                    _string = new string(chars);
                }

                return _string;
            }
        }

        public struct Enumerator : IEnumerator<string>, IEnumerator
        {
            private readonly Encoding encoding;
            private readonly Bucket[] buckets;
            private Bucket currentBucket;
            private int currentIndex;

            internal Enumerator(EncodedStringTable table)
            {
                this.encoding = table.encoding;
                this.buckets = table.buckets;
                this.currentBucket = null;
                this.currentIndex = 0;
            }

            public string Current
            {
                get { return this.currentBucket != null ? this.currentBucket.GetString(this.encoding) : null; }
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