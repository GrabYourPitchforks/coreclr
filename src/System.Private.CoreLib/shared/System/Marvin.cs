// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Internal.Runtime.CompilerServices;

#if BIT64
using nint = System.Int64;
using nuint = System.UInt64;
#else
using nint = System.Int32;
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

        /// <summary>
        /// Compute a Marvin hash and collapse it into a 32-bit hash.
        /// </summary>
        public unsafe static int ComputeHash32(ref byte data, int count, ulong seed)
        {
            // n.b. count is treated as an unsigned integer, so it could be negative.
            // We really should just change the signature to make this unsigned.

            uint p0 = (uint)seed;
            uint p1 = (uint)(seed >> 32);

            if ((uint)count >= 8)
            {
                // the number of whole 8-byte blocks we can read from the buffer
                // (also, the iteration count of the loop below)
                nuint wholeBlockCount = (uint)count / 8;
                Debug.Assert(wholeBlockCount > 0);

                // Point data *just past* the end of where we can stop reading whole blocks.
                // It's possible that data now points just past the end of the buffer, which is
                // valid for any GC-tracked object. We'll use this new reference as the base,
                // then read forward by incrementing a *negative* index counter until it
                // reaches zero.

                // The indexes into data below are written in such a way as to generate a
                // scaling factor which allows a highly efficient mod/rm encoding, i.e.,
                // mov tempValue, qword ptr [data + index * 8].

                data = ref Unsafe.As<ulong, byte>(ref Unsafe.Add(ref Unsafe.As<byte, ulong>(ref data), (IntPtr)(void*)wholeBlockCount));
                nint index = -(nint)wholeBlockCount;

                do
                {
                    // !! WARNING !!
                    // Below logic only works on x64 little-endian platforms

                    ulong tempValue = Unsafe.ReadUnaligned<ulong>(ref Unsafe.As<ulong, byte>(ref Unsafe.Add(ref Unsafe.As<byte, ulong>(ref data), (IntPtr)(void*)index)));
                    p0 += (uint)tempValue;
                    Block(ref p0, ref p1);

                    p0 += (uint)(tempValue >> 32);
                    Block(ref p0, ref p1);

                    // Incrementing the index by 1 and comparing it against zero is optimized by the
                    // CPU and is more efficient than incrementing or decrementing by any other
                    // constant factor. It's because of this we need to use the scaling factor technique
                    // mentioned at the beginning of the loop.

                    index++;
                } while ((uint)index != 0);

                // At the end of the loop, the data ref already points to the remaining data
                // that we haven't yet consumed. All we need to do is fix up the remaining
                // byte count.

                count &= 7;
            }

            nuint byteOffset = 0;

            switch ((uint)count)
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

                default:
                    // This is actually case 7, but we're leveraging the fact that the JIT always performs
                    // a bounds check on the switch statement, and we're directing the "greater than 6" case
                    // here.
                    Debug.Assert(count == 7);
                    p0 += Unsafe.ReadUnaligned<uint>(ref Unsafe.AddByteOffset(ref data, byteOffset));
                    byteOffset += 4;
                    Block(ref p0, ref p1);
                    goto case 3;

                case 3:
                    p0 += (((uint)(Unsafe.AddByteOffset(ref Unsafe.AddByteOffset(ref data, byteOffset), 2))) << 16) + (uint)(Unsafe.ReadUnaligned<ushort>(ref Unsafe.AddByteOffset(ref data, byteOffset))) + 0x80000000u;
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
