using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Utilities
{
    internal static class EncodingUtil
    {
        /// <summary>
        /// Gets the next code unit (character) from the byte buffer
        /// </summary>
        public static int ReadCode(Encoding encoding, ReadOnlySpan<byte> buffer, int start, out int length)
        {
            if (encoding == Encoding.UTF8)
            {
                return EncodingUtil.ReadUTF8Code(buffer, start, out length);
            }
            else if (encoding == Encoding.Unicode)
            {
                return EncodingUtil.ReadUTF16LECode(buffer, start, out length);
            }
            else if (encoding == Encoding.BigEndianUnicode)
            {
                return EncodingUtil.ReadUTF16BECode(buffer, start, out length);
            }
            else
            {
                throw new NotSupportedException();
            }
        }

        /// <summary>
        /// Gets the  UTF16 code unit (character) from the UTF16 byte buffer (little endian order)
        /// </summary>
        public static int ReadUTF16LECode(ReadOnlySpan<byte> buffer, int index, out int length)
        {
            length = 2;
            return (char)(buffer[index] + (buffer[index + 1] << 8));
        }

        /// <summary>
        /// Gets the UTF16 code unit (character) from the byte buffer (big endian order)
        /// </summary>
        public static int ReadUTF16BECode(ReadOnlySpan<byte> buffer, int index, out int length)
        {
            length = 2;
            return (char)((buffer[index] << 8) + buffer[index + 1]);
        }

        /// <summary>
        /// Gets the starting offset of the next character in the UTF16 (Little Endian) encoded buffer, at or after the specified offset.
        /// </summary>
        public static int GetNextUTF16LECodeStart(ReadOnlySpan<byte> buffer, int offset)
        {
            // assume start of buffer is aligned; so align any partial offset
            offset = (offset & 1) == 0 ? offset : offset + 1;

            // skip low surrogate (as we are in second half of pair)
            if (Char.IsLowSurrogate((char)ReadUTF16LECode(buffer, offset, out var length)))
            {
                offset += length;
            }

            return offset;
        }

        /// <summary>
        /// Gets the starting offset of the next character in the UTF16 (Big Endian) encoded buffer, at or after the specified offset.
        /// </summary>
        public static int GetNextUTF16BECodeStart(ReadOnlySpan<byte> buffer, int offset)
        {
            // assume start of buffer is aligned; so align any partial offset
            offset = (offset & 1) == 0 ? offset : offset + 1;

            // skip low surrogate (as we are in second half of pair)
            var c = (char)ReadUTF16BECode(buffer, offset, out var length);
            if (char.IsLowSurrogate(c))
            {
                offset += length;
            }

            return offset;
        }

        /// <summary>
        /// Returns true if the byte is a valid start byte of an encoded UTF8 code.
        /// </summary>
        public static bool IsUTF8StartByte(byte b)
        {
            // any byte with two highest bits as "10" is an extension byte to multi-byte encoding
            // characters cannot start on extension bytes
            return (b & 0b1100_0000) != 0b1000_0000;
        }

        /// <summary>
        /// Reads the UTF8 code unit from the byte buffer.
        /// </summary>
        public static int ReadUTF8Code(ReadOnlySpan<byte> buffer, int index, out int length)
        {
            var b = buffer[index];
            if ((b & 0b1000_0000) == 0b0000_0000)
            {
                length = 1;
                return (ushort)(b & 0b0111_1111);
            }
            else if ((b & 0b1110_0000) == 0b1100_0000)
            {
                length = 2;
                return (ushort)((b & 0b0001_1111)
                    + (buffer[index + 1] & 0b0011_1111) << 5);
            }
            else if ((b & 0b1111_0000) == 0b1110_0000)
            {
                length = 3;
                return (ushort)((b & 0b0000_1111)
                    + (buffer[index + 1] & 0b0011_1111) << 4
                    + (buffer[index + 2] & 0b0011_1111) << 10);
            }
            else if ((b & 0b1111_1000) == 0b1111_0000)
            {
                length = 4;
                return (ushort)((b & 0b0000_0111)
                    + (buffer[index + 1] & 0b0011_1111) << 3
                    + (buffer[index + 2] & 0b0011_1111) << 9
                    + (buffer[index + 3] & 0b0011_1111) << 15);
            }
            else
            {
                // this is not a valid UTF8 encoding byte
                throw new InvalidOperationException();
            }
        }

        /// <summary>
        /// Gets the starting offset of the next character in the UTF8 encoded buffer, at or after the specified offset.
        /// </summary>
        public static int GetNextUTF8CodeStart(ReadOnlySpan<byte> buffer, int offset)
        {
            // skip forward until we find the start of the next UTF8 character
            while (!IsUTF8StartByte(buffer[offset]))
            {
                offset++;
            }

            return offset;
        }

        /// <summary>
        /// Determines the starting offset of the next code unit (character) in the encoded buffer at or after the specified offset.
        /// </summary>
        public static int GetNextCodeStart(Encoding encoding, ReadOnlySpan<byte> buffer, int offset)
        {
            if (encoding == Encoding.UTF8)
            {
                return EncodingUtil.GetNextUTF8CodeStart(buffer, offset);
            }
            else if (encoding == Encoding.Unicode)
            {
                return EncodingUtil.GetNextUTF16LECodeStart(buffer, offset);
            }
            else if (encoding == Encoding.BigEndianUnicode)
            {
                return EncodingUtil.GetNextUTF16BECodeStart(buffer, offset);
            }
            else
            {
                throw new NotSupportedException();
            }
        }

        /// <summary>
        /// Returns the number of bytes to add to the position to align it on the 
        /// encoding specific boundary.
        /// </summary
        public static int GetAlignment(Encoding encoding, long position)
        {
            if (encoding == Encoding.Unicode ||
                encoding == Encoding.BigEndianUnicode)
            {
                // need to be on two-byte boundary
                return (position & 1) == 0 ? 0 : 1;
            }
            else if (encoding == Encoding.UTF8)
            {
                return 0;
            }
            else
            {
                throw new NotSupportedException();
            }
        }
    }
}