// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Internal.Runtime.CompilerServices;

#if BIT64
using nuint = System.UInt64;
#else
using nuint = System.UInt32;
#endif

namespace System
{
    internal static class Marvin
    {
        /// <summary>
        /// Compute a Marvin hash and collapse it into a 32-bit hash.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ComputeHash32(ReadOnlySpan<byte> data, ulong seed) => ComputeHash32(ref MemoryMarshal.GetReference(data), data.Length, seed);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ComputeHash32(ref byte data, int count, ulong seed) => ComputeHash32(ref data, (uint)count, (uint)seed, (uint)(seed >> 32));

        // Computes an Ordinal (case-sensitive, culture-agnostic) hash over a UTF-16 text buffer.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ComputeHash32Ordinal(ref char data, int length, ulong seed)
        {
            Debug.Assert(length >= 0);

            // The multiplication below won't overflow since the type of length is (signed) Int32 and its
            // value is non-negative, so we can extend to (unsigned) UInt32 and multiply by 2 without
            // risking any loss of data.

            return ComputeHash32(ref Unsafe.As<char, byte>(ref data), (uint)length * 2, (uint)seed, (uint)(seed >> 32));
        }

        // Computes an OrdinalIgnoreCase (case-insensitive, culture-agnostic) hash over a UTF-16 text buffer.
        public static int ComputeHash32OrdinalIgnoreCase(ref char data, int length, ulong seed)
        {
            Debug.Assert(length >= 0);

            // The multiplication below won't overflow since the type of length is (signed) Int32 and its
            // value is non-negative, so we can extend to (unsigned) UInt32 and multiply by 2 without
            // risking any loss of data.

            return ComputeHash32OrdinalIgnoreCase(ref Unsafe.As<char, byte>(ref data), (uint)length * 2, (uint)seed, (uint)(seed >> 32));
        }

        private static int ComputeHash32OrdinalIgnoreCase(ref byte data, nuint count, uint p0, uint p1)
        {
            // At the start of the method, 'count' is the number of bytes of the input data (so should be a multiple of 2, since
            // the data should be a UTF-16 string).

            Debug.Assert(count % 2 == 0);

            nuint byteOffset = 0;
            uint tempValue;

            if (count >= 8)
            {
                // See comments in TextInfo.ChangeCaseCommon for how this logic works.
                // Keep the logic in ChangeCaseCommon and Marvin.ComputeHash32OrdinalIgnoreCase in sync.

                nuint lastOffsetWhereCanRead64Bits = count - 8;
                do
                {
                    tempValue = Unsafe.ReadUnaligned<uint>(ref Unsafe.AddByteOffset(ref data, byteOffset));
                    if (!Utf16Utility.DWordAllCharsAreAscii(tempValue))
                    {
                        goto NonAscii;
                    }
                    p0 += Utf16Utility.ToUpperInvariantAsciiDWord(tempValue);
                    Block(ref p0, ref p1);

                    tempValue = Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref Unsafe.AddByteOffset(ref data, byteOffset), 4));
                    if (!Utf16Utility.DWordAllCharsAreAscii(tempValue))
                    {
                        goto NonAsciiSkip4Bytes;
                    }
                    p0 += Utf16Utility.ToUpperInvariantAsciiDWord(tempValue);
                    Block(ref p0, ref p1);

                    byteOffset += 8;
                } while (byteOffset <= lastOffsetWhereCanRead64Bits);
            }

            switch ((uint)count & 7)
            {
                case 4:
                    tempValue = Unsafe.ReadUnaligned<uint>(ref Unsafe.AddByteOffset(ref data, byteOffset));
                    if (!Utf16Utility.DWordAllCharsAreAscii(tempValue))
                    {
                        goto NonAscii;
                    }
                    p0 += Utf16Utility.ToUpperInvariantAsciiDWord(tempValue);
                    Block(ref p0, ref p1);
                    goto case 0;

                case 0:
                    p0 += 0x80u;
                    break;

                case 5:
                case 1:
                    goto default; // can't have odd number byte count

                case 6:
                    tempValue = Unsafe.ReadUnaligned<uint>(ref Unsafe.AddByteOffset(ref data, byteOffset));
                    if (!Utf16Utility.DWordAllCharsAreAscii(tempValue))
                    {
                        goto NonAscii;
                    }
                    p0 += Utf16Utility.ToUpperInvariantAsciiDWord(tempValue);
                    byteOffset += 4;
                    Block(ref p0, ref p1);
                    goto case 2;

                case 2:
                    tempValue = Unsafe.ReadUnaligned<uint>(ref Unsafe.AddByteOffset(ref data, byteOffset));
                    if (tempValue > 0x7Fu)
                    {
                        goto NonAscii;
                    }
                    p0 += Utf16Utility.ToUpperInvariantAsciiDWord(tempValue) + 0x800000u;
                    break;

                case 7:
                case 3:
                default:
                    Debug.Fail("Should not get here."); // can't have an odd number byte count
                    break;
            }

            Block(ref p0, ref p1);
            Block(ref p0, ref p1);

            return (int)(p1 ^ p0);

        NonAsciiSkip4Bytes:
            byteOffset += 4;

        NonAscii:
            // We'll pass in the existing p0 and p1 state so that we don't have to throw away the hash code
            // we've computed thus far.
            return ComputeUtf16Hash32OrdinalIgnoreCaseSlow(ref Unsafe.AddByteOffset(ref data, byteOffset), count - byteOffset, p0, p1);
        }

        private static unsafe int ComputeUtf16Hash32OrdinalIgnoreCaseSlow(ref byte data, nuint count, uint p0, uint p1)
        {
            const int MaxStackAllocLimit = 32; // * 2 bytes per char = 64 bytes total

            Debug.Assert(count % 2 == 0, "Unexpected number of bytes.");
            Debug.Assert((uint)Unsafe.AsPointer(ref data) % 2 == 0, "Unexpected data offset.");

            // It's safe to go byte -> char below because we know the input data originally came from a ROS<char>.

            ReadOnlySpan<char> source = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<byte, char>(ref data), (int)((uint)count / 2));

            // Computing the hash of a string using OrdinalIgnoreCase involves converting it to uppercase using the
            // invariant culture, then calculating the ordinal hash code over that data. The invariant culture
            // uses simple case folding, which does not change the number of UTF-16 code units in the string.

            char[] borrowedArr = null;
            Span<char> buffer = (source.Length <= MaxStackAllocLimit) ? stackalloc char[MaxStackAllocLimit] : (borrowedArr = ArrayPool<char>.Shared.Rent(source.Length));

            int utf16CodeUnitCount = source.ToUpperInvariant(buffer);
            Debug.Assert(source.Length == utf16CodeUnitCount, "Unexpected UTF-16 code unit count after case conversion.");

            int hashCode = ComputeHash32(ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(buffer)), (uint)source.Length * 2, p0, p1);

            if (borrowedArr != null)
            {
                ArrayPool<char>.Shared.Return(borrowedArr);
            }

            return hashCode;
        }

        private static int ComputeHash32(ref byte data, nuint count, uint p0, uint p1)
        {
            nuint byteOffset = 0;

            if (count >= 8)
            {
                // From this point forward, 'count' is actually the last offset at which we can read
                // a QWORD without overrunning the original buffer. It's ok for us to reuse the count
                // local in this manner since we're not affecting the final three bits which will be
                // used in the switch statement at the end of this method.

                count -= 8;
                do
                {
                    p0 += Unsafe.ReadUnaligned<uint>(ref Unsafe.AddByteOffset(ref data, byteOffset));
                    Block(ref p0, ref p1);

                    p0 += Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref Unsafe.AddByteOffset(ref data, byteOffset), 4));
                    Block(ref p0, ref p1);

                    byteOffset += 8;
                } while (byteOffset <= count);
            }

            switch ((uint)count & 7)
            {
                case 4:
                    p0 += Unsafe.ReadUnaligned<uint>(ref Unsafe.AddByteOffset(ref data, byteOffset));
                    Block(ref p0, ref p1);
                    goto case 0;

                case 0:
                    p0 += 0x80u;
                    break;

                case 5:
                    p0 += Unsafe.ReadUnaligned<uint>(ref Unsafe.AddByteOffset(ref data, byteOffset));
                    byteOffset += 4;
                    Block(ref p0, ref p1);
                    goto case 1;

                case 1:
                    p0 += Unsafe.AddByteOffset(ref data, byteOffset) + 0x8000u;
                    break;

                case 6:
                    p0 += Unsafe.ReadUnaligned<uint>(ref Unsafe.AddByteOffset(ref data, byteOffset));
                    byteOffset += 4;
                    Block(ref p0, ref p1);
                    goto case 2;

                case 2:
                    p0 += Unsafe.ReadUnaligned<ushort>(ref Unsafe.AddByteOffset(ref data, byteOffset)) + 0x800000u;
                    break;

                case 7:
                    p0 += Unsafe.ReadUnaligned<uint>(ref Unsafe.AddByteOffset(ref data, byteOffset));
                    byteOffset += 4;
                    Block(ref p0, ref p1);
                    goto case 3;

                case 3:
                    p0 += (((uint)(Unsafe.Add(ref Unsafe.AddByteOffset(ref data, byteOffset), 2))) << 16) + (uint)(Unsafe.ReadUnaligned<ushort>(ref Unsafe.AddByteOffset(ref data, byteOffset))) + 0x80000000u;
                    break;

                default:
                    Debug.Fail("Should not get here.");
                    break;
            }

            Block(ref p0, ref p1);
            Block(ref p0, ref p1);

            return (int)(p1 ^ p0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Block(ref uint rp0, ref uint rp1)
        {
            uint p0 = rp0;
            uint p1 = rp1;

            p1 ^= p0;
            p0 = _rotl(p0, 20);

            p0 += p1;
            p1 = _rotl(p1, 9);

            p1 ^= p0;
            p0 = _rotl(p0, 27);

            p0 += p1;
            p1 = _rotl(p1, 19);

            rp0 = p0;
            rp1 = p1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint _rotl(uint value, int shift)
        {
            // This is expected to be optimized into a single rol (or ror with negated shift value) instruction
            return (value << shift) | (value >> (32 - shift));
        }

        public static ulong DefaultSeed { get; } = GenerateSeed();

        private static unsafe ulong GenerateSeed()
        {
            ulong seed;
            Interop.GetRandomBytes((byte*)&seed, sizeof(ulong));
            return seed;
        }
    }
}
