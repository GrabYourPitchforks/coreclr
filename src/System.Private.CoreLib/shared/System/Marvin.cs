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

        // We expect caller to reinterpret_cast the input as a byte*, but the count parameter is specified in chars
        public unsafe static int ComputeHashStringOrdinal32(ref byte bytes, uint charCount, uint p0, uint p1)
        {
            nuint byteOffset = 0;

            while (charCount >= 4)
            {
                p0 += Unsafe.ReadUnaligned<uint>(ref Unsafe.AddByteOffset(ref bytes, byteOffset));
                Block(ref p0, ref p1);

                p0 += Unsafe.ReadUnaligned<uint>(ref Unsafe.AddByteOffset(ref Unsafe.AddByteOffset(ref bytes, byteOffset), 4));
                Block(ref p0, ref p1);

                byteOffset += 8;
                charCount -= 4;
            }

            if (charCount >= 2)
            {
                p0 += Unsafe.ReadUnaligned<uint>(ref Unsafe.AddByteOffset(ref bytes, byteOffset));
                Block(ref p0, ref p1);
            }

            if ((charCount & 1) != 0)
            {
                p0 += Unsafe.Add(ref Unsafe.Add(ref Unsafe.As<byte, char>(ref bytes), (IntPtr)(void*)charCount), -1);
                p0 += 0x800000u - 0x80u;
            }

            p0 += 0x80u;

            Block(ref p0, ref p1);
            Block(ref p0, ref p1);

            return (int)(p1 ^ p0);
        }

        public unsafe static int ComputeHashStringOrdinal32(ref byte bytes, uint charCount, ulong seedA, ulong seedB)
        {
            // Sequential Marvin code path

            uint p0 = (uint)seedA;
            uint p1 = (uint)(seedA >> 32);

            nuint byteOffset = 0;

            if (charCount >= 4)
            {
                if (charCount < 8)
                {
                    // Sequential Marvin code path

                    p0 += Unsafe.ReadUnaligned<uint>(ref bytes);
                    Block(ref p0, ref p1);

                    p0 += Unsafe.ReadUnaligned<uint>(ref Unsafe.AddByteOffset(ref bytes, 4));
                    Block(ref p0, ref p1);

                    byteOffset = 8;
                    charCount -= 4;
                }
                else
                {
                    // Parallel Marvin code path

                    uint p0_b = (uint)seedB;
                    uint p1_b = (uint)(seedB >> 32);

                    p0 += Unsafe.ReadUnaligned<uint>(ref bytes);
                    p0_b += Unsafe.ReadUnaligned<uint>(ref Unsafe.AddByteOffset(ref bytes, 4));

                    byteOffset += 8;
                    charCount -= 4;
                    Block_Parallel(ref p0, ref p1, ref p0_b, ref p1_b);

                    do
                    {
                        p0 += Unsafe.ReadUnaligned<uint>(ref Unsafe.AddByteOffset(ref bytes, byteOffset));
                        p0_b += Unsafe.ReadUnaligned<uint>(ref Unsafe.AddByteOffset(ref Unsafe.AddByteOffset(ref bytes, byteOffset), 4));

                        byteOffset += 8;
                        charCount -= 4;
                        Block_Parallel(ref p0, ref p1, ref p0_b, ref p1_b);
                    } while (charCount >= 4);

                    if (charCount >= 2)
                    {
                        p0 += Unsafe.ReadUnaligned<uint>(ref Unsafe.AddByteOffset(ref bytes, byteOffset));
                        Block(ref p0, ref p1);
                    }

                    if ((charCount & 1) != 0)
                    {
                        p0 += Unsafe.Add(ref Unsafe.Add(ref Unsafe.As<byte, char>(ref bytes), (IntPtr)(void*)charCount), -1);
                        p0 += 0x800000u - 0x80u;
                    }

                    // Mix seedB state back in to seedA

                    p0 += 0x80u;
                    p0_b += 0x80u;

                    Block_Parallel(ref p0, ref p1, ref p0_b, ref p1_b);
                    Block_Parallel(ref p0, ref p1, ref p0_b, ref p1_b);
                    return (int)((p1 + p1_b) ^ (p0 + p0_b));
                }
            }

            if (charCount >= 2)
            {
                p0 += Unsafe.ReadUnaligned<uint>(ref Unsafe.AddByteOffset(ref bytes, byteOffset));
                Block(ref p0, ref p1);
            }

            if ((charCount & 1) != 0)
            {
                p0 += Unsafe.Add(ref Unsafe.Add(ref Unsafe.As<byte, char>(ref bytes), (IntPtr)(void*)charCount), -1);
                p0 += 0x800000u - 0x80u;
            }

            p0 += 0x80u;

            Block(ref p0, ref p1);
            Block(ref p0, ref p1);

            return (int)(p1 ^ p0);
        }

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
        private static void Block_Parallel(ref uint rp0_a, ref uint rp1_a, ref uint rp0_b, ref uint rp1_b)
        {
            uint p0_a = rp0_a;
            uint p0_b = rp0_b;
            uint p1_a = rp1_a;
            uint p1_b = rp1_b;

            p1_a ^= p0_a;
            p1_b ^= p0_b;
            p0_a = _rotl(p0_a, 20);
            p0_b = _rotl(p0_b, 20);

            p0_a += p1_a;
            p0_b += p1_b;
            p1_a = _rotl(p1_a, 9);
            p1_b = _rotl(p1_b, 9);

            p1_a ^= p0_a;
            p1_b ^= p0_b;
            p0_a = _rotl(p0_a, 27);
            p0_b = _rotl(p0_b, 27);

            p0_a += p1_a;
            p0_b += p1_b;
            p1_a = _rotl(p1_a, 19);
            p1_b = _rotl(p1_b, 19);

            rp0_a = p0_a;
            rp0_b = p0_b;
            rp1_a = p1_a;
            rp1_b = p1_b;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint _rotl(uint value, int shift)
        {
            // This is expected to be optimized into a single rol (or ror with negated shift value) instruction
            return (value << shift) | (value >> (32 - shift));
        }

        public static ulong DefaultSeed { get; } = GenerateSeed();

        public static ulong DefaultSeed_ParallelA { get; } = GenerateSeed();
        public static ulong DefaultSeed_ParallelB { get; } = GenerateSeed();

        private static unsafe ulong GenerateSeed()
        {
            ulong seed;
            Interop.GetRandomBytes((byte*)&seed, sizeof(ulong));
            return seed;
        }
    }
}
