// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Internal.Runtime.CompilerServices;

#if BIT64
using nuint = System.UInt64;
#else
using nuint = System.UInt32;
#endif

namespace System
{
    internal static partial class Marvin
    {
        /// <summary>
        /// Compute a Marvin hash and collapse it into a 32-bit hash.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ComputeHash32(ReadOnlySpan<byte> data, ulong seed) => ComputeHash32(ref MemoryMarshal.GetReference(data), data.Length, (uint)seed, (uint)(seed >> 32));

        /// <summary>
        /// Compute a Marvin hash and collapse it into a 32-bit hash.
        /// </summary>
        public static int ComputeHash32(ref byte data, int count, uint p0, uint p1)
        {
            nuint ucount = (nuint)count;
            nuint byteOffset = 0;

            while (ucount >= 8)
            {
                p0 += Unsafe.ReadUnaligned<uint>(ref Unsafe.AddByteOffset(ref data, byteOffset));
                Block(ref p0, ref p1);

                p0 += Unsafe.ReadUnaligned<uint>(ref Unsafe.AddByteOffset(ref data, byteOffset + 4));
                Block(ref p0, ref p1);

                byteOffset += 8;
                ucount -= 8;
            }

            switch (ucount)
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
                    p0 += 0x8000u | Unsafe.AddByteOffset(ref data, byteOffset);
                    break;

                case 6:
                    p0 += Unsafe.ReadUnaligned<uint>(ref Unsafe.AddByteOffset(ref data, byteOffset));
                    byteOffset += 4;
                    Block(ref p0, ref p1);
                    goto case 2;

                case 2:
                    p0 += 0x800000u | Unsafe.ReadUnaligned<ushort>(ref Unsafe.AddByteOffset(ref data, byteOffset));
                    break;

                case 7:
                    p0 += Unsafe.ReadUnaligned<uint>(ref Unsafe.AddByteOffset(ref data, byteOffset));
                    byteOffset += 4;
                    Block(ref p0, ref p1);
                    goto case 3;

                case 3:
                    p0 += 0x80000000u | (((uint)(Unsafe.AddByteOffset(ref data, byteOffset + 2))) << 16) | (uint)(Unsafe.ReadUnaligned<ushort>(ref Unsafe.AddByteOffset(ref data, byteOffset)));
                    break;

                default:
                    Debug.Fail("Should not get here.");
                    break;
            }

            Block(ref p0, ref p1);
            Block(ref p0, ref p1);

            return (int)(p1 ^ p0);
        }

        /// <summary>
        /// Compute a Marvin hash and collapse it into a 32-bit hash.
        /// </summary>
        public static int ComputeHash32_Test(ref byte data, int count, ulong seed0, ulong seed1)
        {
            uint s0p0 = (uint)seed0;
            uint s0p1 = (uint)(seed0 >> 32);
            nuint byteOffset = 0;

            if ((uint)count >= 8)
            {
                uint s1p0 = (uint)seed1;
                uint s1p1 = (uint)(seed1 >> 32);

                do
                {
                    s0p0 += Unsafe.ReadUnaligned<uint>(ref Unsafe.AddByteOffset(ref data, byteOffset));
                    s1p0 += Unsafe.ReadUnaligned<uint>(ref Unsafe.AddByteOffset(ref Unsafe.AddByteOffset(ref data, byteOffset), 4));

                    Block_Test(ref s0p0, ref s0p1, ref s1p0, ref s1p1);

                    byteOffset += 8;
                    count -= 8;
                } while ((uint)count >= 8);

                s0p0 ^= s1p0;
                s0p1 ^= s1p1;
            }

            switch ((uint)count)
            {
                case 4:
                    s0p0 += Unsafe.ReadUnaligned<uint>(ref Unsafe.AddByteOffset(ref data, byteOffset));
                    Block(ref s0p0, ref s0p1);
                    goto case 0;

                case 0:
                    s0p0 += 0x80u;
                    break;

                case 5:
                    s0p0 += Unsafe.ReadUnaligned<uint>(ref Unsafe.AddByteOffset(ref data, byteOffset));
                    byteOffset += 4;
                    Block(ref s0p0, ref s0p1);
                    goto case 1;

                case 1:
                    s0p0 += 0x8000u | Unsafe.AddByteOffset(ref data, byteOffset);
                    break;

                case 6:
                    s0p0 += Unsafe.ReadUnaligned<uint>(ref Unsafe.AddByteOffset(ref data, byteOffset));
                    byteOffset += 4;
                    Block(ref s0p0, ref s0p1);
                    goto case 2;

                case 2:
                    s0p0 += 0x800000u | Unsafe.ReadUnaligned<ushort>(ref Unsafe.AddByteOffset(ref data, byteOffset));
                    break;

                default:
                    Debug.Assert(count == 7);
                    s0p0 += Unsafe.ReadUnaligned<uint>(ref Unsafe.AddByteOffset(ref data, byteOffset));
                    byteOffset += 4;
                    Block(ref s0p0, ref s0p1);
                    goto case 3;

                case 3:
                    s0p0 += 0x80000000u | (((uint)(Unsafe.AddByteOffset(ref Unsafe.AddByteOffset(ref data, byteOffset), 2))) << 16) | (uint)(Unsafe.ReadUnaligned<ushort>(ref Unsafe.AddByteOffset(ref data, byteOffset)));
                    break;
            }

            Block(ref s0p0, ref s0p1);
            Block(ref s0p0, ref s0p1);

            return (int)(s0p1 ^ s0p0);
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
        private static void Block_Test(ref uint rs0p0, ref uint rs0p1, ref uint rs1p0, ref uint rs1p1)
        {
            uint s0p0 = rs0p0;
            uint s0p1 = rs0p1;
            uint s1p0 = rs1p0;
            uint s1p1 = rs1p1;

            s0p1 ^= s0p0;
            s1p1 ^= s1p0;
            s0p0 = _rotl(s0p0, 20);
            s1p0 = _rotl(s1p0, 20);

            s0p0 += s0p1;
            s1p0 += s1p1;
            s0p1 = _rotl(s0p1, 9);
            s1p1 = _rotl(s1p1, 9);

            s0p1 ^= s0p0;
            s1p1 ^= s1p0;
            s0p0 = _rotl(s0p0, 27);
            s1p0 = _rotl(s1p0, 27);

            s0p0 += s0p1;
            s1p0 += s1p1;
            s0p1 = _rotl(s0p1, 19);
            s1p1 = _rotl(s1p1, 19);

            rs0p0 = s0p0;
            rs0p1 = s0p1;
            rs1p0 = s1p0;
            rs1p1 = s1p1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint _rotl(uint value, int shift)
        {
            // This is expected to be optimized into a single rol (or ror with negated shift value) instruction
            return (value << shift) | (value >> (32 - shift));
        }

        public static ulong DefaultSeed { get; } = GenerateSeed();

        public static ulong DefaultSeed0_Test { get; } = GenerateSeed();
        public static ulong DefaultSeed1_Test { get; } = GenerateSeed();

        private static unsafe ulong GenerateSeed()
        {
            ulong seed;
            Interop.GetRandomBytes((byte*)&seed, sizeof(ulong));
            return seed;
        }
    }
}
