// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if AMD64 || ARM64 || (BIT32 && !ARM)
#define HAS_CUSTOM_BLOCKS
#endif

namespace System
{
    //Only contains static methods.  Does not require serialization

    using System;
    using System.Numerics;
    using System.Runtime.CompilerServices;
    using System.Runtime.ConstrainedExecution;
    using System.Runtime.InteropServices;
    using System.Runtime.Versioning;
    using System.Diagnostics;
    using System.Security;
    using System.Runtime;
    using Internal.Runtime.CompilerServices;

#if BIT64
    using nint = System.Int64;
    using nuint = System.UInt64;
#else // BIT64
    using nint = System.Int32;
    using nuint = System.UInt32;
#endif // BIT64

    public static class Buffer
    {
        // Copies from one primitive array to another primitive array without
        // respecting types.  This calls memmove internally.  The count and 
        // offset parameters here are in bytes.  If you want to use traditional
        // array element indices and counts, use Array.Copy.
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        public static extern void BlockCopy(Array src, int srcOffset,
            Array dst, int dstOffset, int count);

        // This is ported from the optimized CRT assembly in memchr.asm. The JIT generates 
        // pretty good code here and this ends up being within a couple % of the CRT asm.
        // It is however cross platform as the CRT hasn't ported their fast version to 64-bit
        // platforms.
        //
        internal unsafe static int IndexOfByte(byte* src, byte value, int index, int count)
        {
            Debug.Assert(src != null, "src should not be null");

            byte* pByte = src + index;

            // Align up the pointer to sizeof(int).
            while (((int)pByte & 3) != 0)
            {
                if (count == 0)
                    return -1;
                else if (*pByte == value)
                    return (int)(pByte - src);

                count--;
                pByte++;
            }

            // Fill comparer with value byte for comparisons
            //
            // comparer = 0/0/value/value
            uint comparer = (((uint)value << 8) + (uint)value);
            // comparer = value/value/value/value
            comparer = (comparer << 16) + comparer;

            // Run through buffer until we hit a 4-byte section which contains
            // the byte we're looking for or until we exhaust the buffer.
            while (count > 3)
            {
                // Test the buffer for presence of value. comparer contains the byte
                // replicated 4 times.
                uint t1 = *(uint*)pByte;
                t1 = t1 ^ comparer;
                uint t2 = 0x7efefeff + t1;
                t1 = t1 ^ 0xffffffff;
                t1 = t1 ^ t2;
                t1 = t1 & 0x81010100;

                // if t1 is zero then these 4-bytes don't contain a match
                if (t1 != 0)
                {
                    // We've found a match for value, figure out which position it's in.
                    int foundIndex = (int)(pByte - src);
                    if (pByte[0] == value)
                        return foundIndex;
                    else if (pByte[1] == value)
                        return foundIndex + 1;
                    else if (pByte[2] == value)
                        return foundIndex + 2;
                    else if (pByte[3] == value)
                        return foundIndex + 3;
                }

                count -= 4;
                pByte += 4;
            }

            // Catch any bytes that might be left at the tail of the buffer
            while (count > 0)
            {
                if (*pByte == value)
                    return (int)(pByte - src);

                count--;
                pByte++;
            }

            // If we don't have a match return -1;
            return -1;
        }

        // Returns a bool to indicate if the array is of primitive data types
        // or not.
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        private static extern bool IsPrimitiveTypeArray(Array array);

        // Gets a particular byte out of the array.  The array must be an
        // array of primitives.  
        //
        // This essentially does the following: 
        // return ((byte*)array) + index.
        //
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        private static extern byte _GetByte(Array array, int index);

        public static byte GetByte(Array array, int index)
        {
            // Is the array present?
            if (array == null)
                throw new ArgumentNullException(nameof(array));

            // Is it of primitive types?
            if (!IsPrimitiveTypeArray(array))
                throw new ArgumentException(SR.Arg_MustBePrimArray, nameof(array));

            // Is the index in valid range of the array?
            if (index < 0 || index >= _ByteLength(array))
                throw new ArgumentOutOfRangeException(nameof(index));

            return _GetByte(array, index);
        }

        // Sets a particular byte in an the array.  The array must be an
        // array of primitives.  
        //
        // This essentially does the following: 
        // *(((byte*)array) + index) = value.
        //
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        private static extern void _SetByte(Array array, int index, byte value);

        public static void SetByte(Array array, int index, byte value)
        {
            // Is the array present?
            if (array == null)
                throw new ArgumentNullException(nameof(array));

            // Is it of primitive types?
            if (!IsPrimitiveTypeArray(array))
                throw new ArgumentException(SR.Arg_MustBePrimArray, nameof(array));

            // Is the index in valid range of the array?
            if (index < 0 || index >= _ByteLength(array))
                throw new ArgumentOutOfRangeException(nameof(index));

            // Make the FCall to do the work
            _SetByte(array, index, value);
        }


        // Gets a particular byte out of the array.  The array must be an
        // array of primitives.  
        //
        // This essentially does the following: 
        // return array.length * sizeof(array.UnderlyingElementType).
        //
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        private static extern int _ByteLength(Array array);

        public static int ByteLength(Array array)
        {
            // Is the array present?
            if (array == null)
                throw new ArgumentNullException(nameof(array));

            // Is it of primitive types?
            if (!IsPrimitiveTypeArray(array))
                throw new ArgumentException(SR.Arg_MustBePrimArray, nameof(array));

            return _ByteLength(array);
        }

        internal unsafe static void ZeroMemory(byte* src, long len)
        {
            while (len-- > 0)
                *(src + len) = 0;
        }

        internal unsafe static void Memcpy(byte[] dest, int destIndex, byte* src, int srcIndex, int len)
        {
            Debug.Assert((srcIndex >= 0) && (destIndex >= 0) && (len >= 0), "Index and length must be non-negative!");
            Debug.Assert(dest.Length - destIndex >= len, "not enough bytes in dest");
            // If dest has 0 elements, the fixed statement will throw an 
            // IndexOutOfRangeException.  Special-case 0-byte copies.
            if (len == 0)
                return;
            fixed (byte* pDest = dest)
            {
                Memcpy(pDest + destIndex, src + srcIndex, len);
            }
        }

        internal unsafe static void Memcpy(byte* pDest, int destIndex, byte[] src, int srcIndex, int len)
        {
            Debug.Assert((srcIndex >= 0) && (destIndex >= 0) && (len >= 0), "Index and length must be non-negative!");
            Debug.Assert(src.Length - srcIndex >= len, "not enough bytes in src");
            // If dest has 0 elements, the fixed statement will throw an 
            // IndexOutOfRangeException.  Special-case 0-byte copies.
            if (len == 0)
                return;
            fixed (byte* pSrc = src)
            {
                Memcpy(pDest + destIndex, pSrc + srcIndex, len);
            }
        }

        // This is tricky to get right AND fast, so lets make it useful for the whole Fx.
        // E.g. System.Runtime.WindowsRuntime!WindowsRuntimeBufferExtensions.MemCopy uses it.

        // This method has a slightly different behavior on arm and other platforms.
        // On arm this method behaves like memcpy and does not handle overlapping buffers.
        // While on other platforms it behaves like memmove and handles overlapping buffers.
        // This behavioral difference is unfortunate but intentional because
        // 1. This method is given access to other internal dlls and this close to release we do not want to change it.
        // 2. It is difficult to get this right for arm and again due to release dates we would like to visit it later.
#if ARM
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        internal unsafe static extern void Memcpy(byte* dest, byte* src, int len);
#else // ARM
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        internal unsafe static void Memcpy(byte* dest, byte* src, int len)
        {
            Debug.Assert(len >= 0, "Negative length in memcopy!");
            Memmove(dest, src, (uint)len);
        }
#endif // ARM

        // This method has different signature for x64 and other platforms and is done for performance reasons.
        internal unsafe static void Memmove(byte* dest, byte* src, nuint len)
        {
#if AMD64 || (BIT32 && !ARM)
            const nuint CopyThreshold = 2048;
#elif ARM64
#if PLATFORM_WINDOWS
            // Determined optimal value for Windows.
            // https://github.com/dotnet/coreclr/issues/13843
            const nuint CopyThreshold = UInt64.MaxValue;
#else // PLATFORM_WINDOWS
            // Managed code is currently faster than glibc unoptimized memmove
            // TODO-ARM64-UNIX-OPT revisit when glibc optimized memmove is in Linux distros
            // https://github.com/dotnet/coreclr/issues/13844
            const nuint CopyThreshold = UInt64.MaxValue;
#endif // PLATFORM_WINDOWS
#else
            const nuint CopyThreshold = 512;
#endif // AMD64 || (BIT32 && !ARM)

            // P/Invoke into the native version when the buffers are overlapping.

            if (((nuint)dest - (nuint)src < len) || ((nuint)src - (nuint)dest < len)) goto PInvoke;

            byte* srcEnd = src + len;
            byte* destEnd = dest + len;

            if (len <= 16) goto MCPY02;
            if (len > 64) goto MCPY05;

            MCPY00:
            // Copy bytes which are multiples of 16 and leave the remainder for MCPY01 to handle.
            Debug.Assert(len > 16 && len <= 64);
#if HAS_CUSTOM_BLOCKS
            *(Block16*)dest = *(Block16*)src;                   // [0,16]
#elif BIT64
            *(long*)dest = *(long*)src;
            *(long*)(dest + 8) = *(long*)(src + 8);             // [0,16]
#else
            *(int*)dest = *(int*)src;
            *(int*)(dest + 4) = *(int*)(src + 4);
            *(int*)(dest + 8) = *(int*)(src + 8);
            *(int*)(dest + 12) = *(int*)(src + 12);             // [0,16]
#endif
            if (len <= 32) goto MCPY01;
#if HAS_CUSTOM_BLOCKS
            *(Block16*)(dest + 16) = *(Block16*)(src + 16);     // [0,32]
#elif BIT64
            *(long*)(dest + 16) = *(long*)(src + 16);
            *(long*)(dest + 24) = *(long*)(src + 24);           // [0,32]
#else
            *(int*)(dest + 16) = *(int*)(src + 16);
            *(int*)(dest + 20) = *(int*)(src + 20);
            *(int*)(dest + 24) = *(int*)(src + 24);
            *(int*)(dest + 28) = *(int*)(src + 28);             // [0,32]
#endif
            if (len <= 48) goto MCPY01;
#if HAS_CUSTOM_BLOCKS
            *(Block16*)(dest + 32) = *(Block16*)(src + 32);     // [0,48]
#elif BIT64
            *(long*)(dest + 32) = *(long*)(src + 32);
            *(long*)(dest + 40) = *(long*)(src + 40);           // [0,48]
#else
            *(int*)(dest + 32) = *(int*)(src + 32);
            *(int*)(dest + 36) = *(int*)(src + 36);
            *(int*)(dest + 40) = *(int*)(src + 40);
            *(int*)(dest + 44) = *(int*)(src + 44);             // [0,48]
#endif

            MCPY01:
            // Unconditionally copy the last 16 bytes using destEnd and srcEnd and return.
            Debug.Assert(len > 16 && len <= 64);
#if HAS_CUSTOM_BLOCKS
            *(Block16*)(destEnd - 16) = *(Block16*)(srcEnd - 16);
#elif BIT64
            *(long*)(destEnd - 16) = *(long*)(srcEnd - 16);
            *(long*)(destEnd - 8) = *(long*)(srcEnd - 8);
#else
            *(int*)(destEnd - 16) = *(int*)(srcEnd - 16);
            *(int*)(destEnd - 12) = *(int*)(srcEnd - 12);
            *(int*)(destEnd - 8) = *(int*)(srcEnd - 8);
            *(int*)(destEnd - 4) = *(int*)(srcEnd - 4);
#endif
            return;

            MCPY02:
            // Copy the first 8 bytes and then unconditionally copy the last 8 bytes and return.
            if ((len & 24) == 0) goto MCPY03;
            Debug.Assert(len >= 8 && len <= 16);
#if BIT64
            *(long*)dest = *(long*)src;
            *(long*)(destEnd - 8) = *(long*)(srcEnd - 8);
#else
            *(int*)dest = *(int*)src;
            *(int*)(dest + 4) = *(int*)(src + 4);
            *(int*)(destEnd - 8) = *(int*)(srcEnd - 8);
            *(int*)(destEnd - 4) = *(int*)(srcEnd - 4);
#endif
            return;

            MCPY03:
            // Copy the first 4 bytes and then unconditionally copy the last 4 bytes and return.
            if ((len & 4) == 0) goto MCPY04;
            Debug.Assert(len >= 4 && len < 8);
            *(int*)dest = *(int*)src;
            *(int*)(destEnd - 4) = *(int*)(srcEnd - 4);
            return;

            MCPY04:
            // Copy the first byte. For pending bytes, do an unconditionally copy of the last 2 bytes and return.
            Debug.Assert(len < 4);
            if (len == 0) return;
            *dest = *src;
            if ((len & 2) == 0) return;
            *(short*)(destEnd - 2) = *(short*)(srcEnd - 2);
            return;

            MCPY05:
            // PInvoke to the native version when the copy length exceeds the threshold.
            if (len > CopyThreshold)
            {
                goto PInvoke;
            }
            // Copy 64-bytes at a time until the remainder is less than 64.
            // If remainder is greater than 16 bytes, then jump to MCPY00. Otherwise, unconditionally copy the last 16 bytes and return.
            Debug.Assert(len > 64 && len <= CopyThreshold);
            nuint n = len >> 6;

            MCPY06:
#if HAS_CUSTOM_BLOCKS
            *(Block64*)dest = *(Block64*)src;
#elif BIT64
            *(long*)dest = *(long*)src;
            *(long*)(dest + 8) = *(long*)(src + 8);
            *(long*)(dest + 16) = *(long*)(src + 16);
            *(long*)(dest + 24) = *(long*)(src + 24);
            *(long*)(dest + 32) = *(long*)(src + 32);
            *(long*)(dest + 40) = *(long*)(src + 40);
            *(long*)(dest + 48) = *(long*)(src + 48);
            *(long*)(dest + 56) = *(long*)(src + 56);
#else
            *(int*)dest = *(int*)src;
            *(int*)(dest + 4) = *(int*)(src + 4);
            *(int*)(dest + 8) = *(int*)(src + 8);
            *(int*)(dest + 12) = *(int*)(src + 12);
            *(int*)(dest + 16) = *(int*)(src + 16);
            *(int*)(dest + 20) = *(int*)(src + 20);
            *(int*)(dest + 24) = *(int*)(src + 24);
            *(int*)(dest + 28) = *(int*)(src + 28);
            *(int*)(dest + 32) = *(int*)(src + 32);
            *(int*)(dest + 36) = *(int*)(src + 36);
            *(int*)(dest + 40) = *(int*)(src + 40);
            *(int*)(dest + 44) = *(int*)(src + 44);
            *(int*)(dest + 48) = *(int*)(src + 48);
            *(int*)(dest + 52) = *(int*)(src + 52);
            *(int*)(dest + 56) = *(int*)(src + 56);
            *(int*)(dest + 60) = *(int*)(src + 60);
#endif
            dest += 64;
            src += 64;
            n--;
            if (n != 0) goto MCPY06;

            len %= 64;
            if (len > 16) goto MCPY00;
#if HAS_CUSTOM_BLOCKS
            *(Block16*)(destEnd - 16) = *(Block16*)(srcEnd - 16);
#elif BIT64
            *(long*)(destEnd - 16) = *(long*)(srcEnd - 16);
            *(long*)(destEnd - 8) = *(long*)(srcEnd - 8);
#else
            *(int*)(destEnd - 16) = *(int*)(srcEnd - 16);
            *(int*)(destEnd - 12) = *(int*)(srcEnd - 12);
            *(int*)(destEnd - 8) = *(int*)(srcEnd - 8);
            *(int*)(destEnd - 4) = *(int*)(srcEnd - 4);
#endif
            return;

            PInvoke:
            _Memmove(dest, src, len);
        }
        
        // This method has different signature for x64 and other platforms and is done for performance reasons.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Memmove<T>(ref T destination, ref T source, nuint elementCount)
        {
            if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                // Blittable memmove

                Memmove(
                    new ByReference<byte>(ref Unsafe.As<T, byte>(ref destination)),
                    new ByReference<byte>(ref Unsafe.As<T, byte>(ref source)),
                    elementCount * (nuint)Unsafe.SizeOf<T>());
            }
            else
            {
                // Non-blittable memmove

                // Try to avoid calling RhBulkMoveWithWriteBarrier if we can get away
                // with a no-op.
                if (!Unsafe.AreSame(ref destination, ref source) && elementCount != 0)
                {
                    RuntimeImports.RhBulkMoveWithWriteBarrier(
                        ref Unsafe.As<T, byte>(ref destination),
                        ref Unsafe.As<T, byte>(ref source),
                        elementCount * (nuint)Unsafe.SizeOf<T>());
                }
            }
        }

        // This method has different signature for x64 and other platforms and is done for performance reasons.
        private static void Memmove(ByReference<byte> dest, ByReference<byte> src, nuint len)
        {
#if AMD64 || (BIT32 && !ARM)
            const nuint CopyThreshold = 2048;
#elif ARM64
#if PLATFORM_WINDOWS
            // Determined optimal value for Windows.
            // https://github.com/dotnet/coreclr/issues/13843
            const nuint CopyThreshold = UInt64.MaxValue;
#else // PLATFORM_WINDOWS
            // Managed code is currently faster than glibc unoptimized memmove
            // TODO-ARM64-UNIX-OPT revisit when glibc optimized memmove is in Linux distros
            // https://github.com/dotnet/coreclr/issues/13844
            const nuint CopyThreshold = UInt64.MaxValue;
#endif // PLATFORM_WINDOWS
#else
            const nuint CopyThreshold = 512;
#endif // AMD64 || (BIT32 && !ARM)

            // P/Invoke into the native version when the buffers are overlapping.            

            if (((nuint)Unsafe.ByteOffset(ref src.Value, ref dest.Value) < len) || ((nuint)Unsafe.ByteOffset(ref dest.Value, ref src.Value) < len))
            {
                goto BuffersOverlap;
            }

            // Use "(IntPtr)(nint)len" to avoid overflow checking on the explicit cast to IntPtr

            ref byte srcEnd = ref Unsafe.Add(ref src.Value, (IntPtr)(nint)len);
            ref byte destEnd = ref Unsafe.Add(ref dest.Value, (IntPtr)(nint)len);

            if (len <= 16)
                goto MCPY02;
            if (len > 64)
                goto MCPY05;

MCPY00:
// Copy bytes which are multiples of 16 and leave the remainder for MCPY01 to handle.
            Debug.Assert(len > 16 && len <= 64);
#if HAS_CUSTOM_BLOCKS
            Unsafe.As<byte, Block16>(ref dest.Value) = Unsafe.As<byte, Block16>(ref src.Value); // [0,16]
#elif BIT64
            Unsafe.As<byte, long>(ref dest.Value) = Unsafe.As<byte, long>(ref src.Value);
            Unsafe.As<byte, long>(ref Unsafe.Add(ref dest.Value, 8)) = Unsafe.As<byte, long>(ref Unsafe.Add(ref src.Value, 8)); // [0,16]
#else
            Unsafe.As<byte, int>(ref dest.Value) = Unsafe.As<byte, int>(ref src.Value);
            Unsafe.As<byte, int>(ref Unsafe.Add(ref dest.Value, 4)) = Unsafe.As<byte, int>(ref Unsafe.Add(ref src.Value, 4));
            Unsafe.As<byte, int>(ref Unsafe.Add(ref dest.Value, 8)) = Unsafe.As<byte, int>(ref Unsafe.Add(ref src.Value, 8));
            Unsafe.As<byte, int>(ref Unsafe.Add(ref dest.Value, 12)) = Unsafe.As<byte, int>(ref Unsafe.Add(ref src.Value, 12)); // [0,16]
#endif
            if (len <= 32)
                goto MCPY01;
#if HAS_CUSTOM_BLOCKS
            Unsafe.As<byte, Block16>(ref Unsafe.Add(ref dest.Value, 16)) = Unsafe.As<byte, Block16>(ref Unsafe.Add(ref src.Value, 16)); // [0,32]
#elif BIT64
            Unsafe.As<byte, long>(ref Unsafe.Add(ref dest.Value, 16)) = Unsafe.As<byte, long>(ref Unsafe.Add(ref src.Value, 16));
            Unsafe.As<byte, long>(ref Unsafe.Add(ref dest.Value, 24)) = Unsafe.As<byte, long>(ref Unsafe.Add(ref src.Value, 24)); // [0,32]
#else
            Unsafe.As<byte, int>(ref Unsafe.Add(ref dest.Value, 16)) = Unsafe.As<byte, int>(ref Unsafe.Add(ref src.Value, 16));
            Unsafe.As<byte, int>(ref Unsafe.Add(ref dest.Value, 20)) = Unsafe.As<byte, int>(ref Unsafe.Add(ref src.Value, 20));
            Unsafe.As<byte, int>(ref Unsafe.Add(ref dest.Value, 24)) = Unsafe.As<byte, int>(ref Unsafe.Add(ref src.Value, 24));
            Unsafe.As<byte, int>(ref Unsafe.Add(ref dest.Value, 28)) = Unsafe.As<byte, int>(ref Unsafe.Add(ref src.Value, 28)); // [0,32]
#endif
            if (len <= 48)
                goto MCPY01;
#if HAS_CUSTOM_BLOCKS
            Unsafe.As<byte, Block16>(ref Unsafe.Add(ref dest.Value, 32)) = Unsafe.As<byte, Block16>(ref Unsafe.Add(ref src.Value, 32)); // [0,48]
#elif BIT64
            Unsafe.As<byte, long>(ref Unsafe.Add(ref dest.Value, 32)) = Unsafe.As<byte, long>(ref Unsafe.Add(ref src.Value, 32));
            Unsafe.As<byte, long>(ref Unsafe.Add(ref dest.Value, 40)) = Unsafe.As<byte, long>(ref Unsafe.Add(ref src.Value, 40)); // [0,48]
#else
            Unsafe.As<byte, int>(ref Unsafe.Add(ref dest.Value, 32)) = Unsafe.As<byte, int>(ref Unsafe.Add(ref src.Value, 32));
            Unsafe.As<byte, int>(ref Unsafe.Add(ref dest.Value, 36)) = Unsafe.As<byte, int>(ref Unsafe.Add(ref src.Value, 36));
            Unsafe.As<byte, int>(ref Unsafe.Add(ref dest.Value, 40)) = Unsafe.As<byte, int>(ref Unsafe.Add(ref src.Value, 40));
            Unsafe.As<byte, int>(ref Unsafe.Add(ref dest.Value, 44)) = Unsafe.As<byte, int>(ref Unsafe.Add(ref src.Value, 44)); // [0,48]
#endif

MCPY01:
// Unconditionally copy the last 16 bytes using destEnd and srcEnd and return.
            Debug.Assert(len > 16 && len <= 64);
#if HAS_CUSTOM_BLOCKS
            Unsafe.As<byte, Block16>(ref Unsafe.Add(ref destEnd, -16)) = Unsafe.As<byte, Block16>(ref Unsafe.Add(ref srcEnd, -16));
#elif BIT64
            Unsafe.As<byte, long>(ref Unsafe.Add(ref destEnd, -16)) = Unsafe.As<byte, long>(ref Unsafe.Add(ref srcEnd, -16));
            Unsafe.As<byte, long>(ref Unsafe.Add(ref destEnd, -8)) = Unsafe.As<byte, long>(ref Unsafe.Add(ref srcEnd, -8));
#else
            Unsafe.As<byte, int>(ref Unsafe.Add(ref destEnd, -16)) = Unsafe.As<byte, int>(ref Unsafe.Add(ref srcEnd, -16));
            Unsafe.As<byte, int>(ref Unsafe.Add(ref destEnd, -12)) = Unsafe.As<byte, int>(ref Unsafe.Add(ref srcEnd, -12));
            Unsafe.As<byte, int>(ref Unsafe.Add(ref destEnd, -8)) = Unsafe.As<byte, int>(ref Unsafe.Add(ref srcEnd, -8));
            Unsafe.As<byte, int>(ref Unsafe.Add(ref destEnd, -4)) = Unsafe.As<byte, int>(ref Unsafe.Add(ref srcEnd, -4));
#endif
            return;

MCPY02:
// Copy the first 8 bytes and then unconditionally copy the last 8 bytes and return.
            if ((len & 24) == 0)
                goto MCPY03;
            Debug.Assert(len >= 8 && len <= 16);
#if BIT64
            Unsafe.As<byte, long>(ref dest.Value) = Unsafe.As<byte, long>(ref src.Value);
            Unsafe.As<byte, long>(ref Unsafe.Add(ref destEnd, -8)) = Unsafe.As<byte, long>(ref Unsafe.Add(ref srcEnd, -8));
#else
            Unsafe.As<byte, int>(ref dest.Value) = Unsafe.As<byte, int>(ref src.Value);
            Unsafe.As<byte, int>(ref Unsafe.Add(ref dest.Value, 4)) = Unsafe.As<byte, int>(ref Unsafe.Add(ref src.Value, 4));
            Unsafe.As<byte, int>(ref Unsafe.Add(ref destEnd, -8)) = Unsafe.As<byte, int>(ref Unsafe.Add(ref srcEnd, -8));
            Unsafe.As<byte, int>(ref Unsafe.Add(ref destEnd, -4)) = Unsafe.As<byte, int>(ref Unsafe.Add(ref srcEnd, -4));
#endif
            return;

MCPY03:
// Copy the first 4 bytes and then unconditionally copy the last 4 bytes and return.
            if ((len & 4) == 0)
                goto MCPY04;
            Debug.Assert(len >= 4 && len < 8);
            Unsafe.As<byte, int>(ref dest.Value) = Unsafe.As<byte, int>(ref src.Value);
            Unsafe.As<byte, int>(ref Unsafe.Add(ref destEnd, -4)) = Unsafe.As<byte, int>(ref Unsafe.Add(ref srcEnd, -4));
            return;

MCPY04:
// Copy the first byte. For pending bytes, do an unconditionally copy of the last 2 bytes and return.
            Debug.Assert(len < 4);
            if (len == 0)
                return;
            dest.Value = src.Value;
            if ((len & 2) == 0)
                return;
            Unsafe.As<byte, short>(ref Unsafe.Add(ref destEnd, -2)) = Unsafe.As<byte, short>(ref Unsafe.Add(ref srcEnd, -2));
            return;

MCPY05:
// PInvoke to the native version when the copy length exceeds the threshold.
            if (len > CopyThreshold)
            {
                goto PInvoke;
            }
            // Copy 64-bytes at a time until the remainder is less than 64.
            // If remainder is greater than 16 bytes, then jump to MCPY00. Otherwise, unconditionally copy the last 16 bytes and return.
            Debug.Assert(len > 64 && len <= CopyThreshold);
            nuint n = len >> 6;

MCPY06:
#if HAS_CUSTOM_BLOCKS
            Unsafe.As<byte, Block64>(ref dest.Value) = Unsafe.As<byte, Block64>(ref src.Value);
#elif BIT64
            Unsafe.As<byte, long>(ref dest.Value) = Unsafe.As<byte, long>(ref src.Value);
            Unsafe.As<byte, long>(ref Unsafe.Add(ref dest.Value, 8)) = Unsafe.As<byte, long>(ref Unsafe.Add(ref src.Value, 8));
            Unsafe.As<byte, long>(ref Unsafe.Add(ref dest.Value, 16)) = Unsafe.As<byte, long>(ref Unsafe.Add(ref src.Value, 16));
            Unsafe.As<byte, long>(ref Unsafe.Add(ref dest.Value, 24)) = Unsafe.As<byte, long>(ref Unsafe.Add(ref src.Value, 24));
            Unsafe.As<byte, long>(ref Unsafe.Add(ref dest.Value, 32)) = Unsafe.As<byte, long>(ref Unsafe.Add(ref src.Value, 32));
            Unsafe.As<byte, long>(ref Unsafe.Add(ref dest.Value, 40)) = Unsafe.As<byte, long>(ref Unsafe.Add(ref src.Value, 40));
            Unsafe.As<byte, long>(ref Unsafe.Add(ref dest.Value, 48)) = Unsafe.As<byte, long>(ref Unsafe.Add(ref src.Value, 48));
            Unsafe.As<byte, long>(ref Unsafe.Add(ref dest.Value, 56)) = Unsafe.As<byte, long>(ref Unsafe.Add(ref src.Value, 56));
#else
            Unsafe.As<byte, int>(ref dest.Value) = Unsafe.As<byte, int>(ref src.Value);
            Unsafe.As<byte, int>(ref Unsafe.Add(ref dest.Value, 4)) = Unsafe.As<byte, int>(ref Unsafe.Add(ref src.Value, 4));
            Unsafe.As<byte, int>(ref Unsafe.Add(ref dest.Value, 8)) = Unsafe.As<byte, int>(ref Unsafe.Add(ref src.Value, 8));
            Unsafe.As<byte, int>(ref Unsafe.Add(ref dest.Value, 12)) = Unsafe.As<byte, int>(ref Unsafe.Add(ref src.Value, 12));
            Unsafe.As<byte, int>(ref Unsafe.Add(ref dest.Value, 16)) = Unsafe.As<byte, int>(ref Unsafe.Add(ref src.Value, 16));
            Unsafe.As<byte, int>(ref Unsafe.Add(ref dest.Value, 20)) = Unsafe.As<byte, int>(ref Unsafe.Add(ref src.Value, 20));
            Unsafe.As<byte, int>(ref Unsafe.Add(ref dest.Value, 24)) = Unsafe.As<byte, int>(ref Unsafe.Add(ref src.Value, 24));
            Unsafe.As<byte, int>(ref Unsafe.Add(ref dest.Value, 28)) = Unsafe.As<byte, int>(ref Unsafe.Add(ref src.Value, 28));
            Unsafe.As<byte, int>(ref Unsafe.Add(ref dest.Value, 32)) = Unsafe.As<byte, int>(ref Unsafe.Add(ref src.Value, 32));
            Unsafe.As<byte, int>(ref Unsafe.Add(ref dest.Value, 36)) = Unsafe.As<byte, int>(ref Unsafe.Add(ref src.Value, 36));
            Unsafe.As<byte, int>(ref Unsafe.Add(ref dest.Value, 40)) = Unsafe.As<byte, int>(ref Unsafe.Add(ref src.Value, 40));
            Unsafe.As<byte, int>(ref Unsafe.Add(ref dest.Value, 44)) = Unsafe.As<byte, int>(ref Unsafe.Add(ref src.Value, 44));
            Unsafe.As<byte, int>(ref Unsafe.Add(ref dest.Value, 48)) = Unsafe.As<byte, int>(ref Unsafe.Add(ref src.Value, 48));
            Unsafe.As<byte, int>(ref Unsafe.Add(ref dest.Value, 52)) = Unsafe.As<byte, int>(ref Unsafe.Add(ref src.Value, 52));
            Unsafe.As<byte, int>(ref Unsafe.Add(ref dest.Value, 56)) = Unsafe.As<byte, int>(ref Unsafe.Add(ref src.Value, 56));
            Unsafe.As<byte, int>(ref Unsafe.Add(ref dest.Value, 60)) = Unsafe.As<byte, int>(ref Unsafe.Add(ref src.Value, 60));
#endif
            dest = new ByReference<byte>(ref Unsafe.Add(ref dest.Value, 64));
            src = new ByReference<byte>(ref Unsafe.Add(ref src.Value, 64));
            n--;
            if (n != 0)
                goto MCPY06;

            len %= 64;
            if (len > 16)
                goto MCPY00;
#if HAS_CUSTOM_BLOCKS
            Unsafe.As<byte, Block16>(ref Unsafe.Add(ref destEnd, -16)) = Unsafe.As<byte, Block16>(ref Unsafe.Add(ref srcEnd, -16));
#elif BIT64
            Unsafe.As<byte, long>(ref Unsafe.Add(ref destEnd, -16)) = Unsafe.As<byte, long>(ref Unsafe.Add(ref srcEnd, -16));
            Unsafe.As<byte, long>(ref Unsafe.Add(ref destEnd, -8)) = Unsafe.As<byte, long>(ref Unsafe.Add(ref srcEnd, -8));
#else
            Unsafe.As<byte, int>(ref Unsafe.Add(ref destEnd, -16)) = Unsafe.As<byte, int>(ref Unsafe.Add(ref srcEnd, -16));
            Unsafe.As<byte, int>(ref Unsafe.Add(ref destEnd, -12)) = Unsafe.As<byte, int>(ref Unsafe.Add(ref srcEnd, -12));
            Unsafe.As<byte, int>(ref Unsafe.Add(ref destEnd, -8)) = Unsafe.As<byte, int>(ref Unsafe.Add(ref srcEnd, -8));
            Unsafe.As<byte, int>(ref Unsafe.Add(ref destEnd, -4)) = Unsafe.As<byte, int>(ref Unsafe.Add(ref srcEnd, -4));
#endif
            return;

BuffersOverlap:
            // If the buffers overlap perfectly, there's no point to copying the data.
            if (Unsafe.AreSame(ref dest.Value, ref src.Value))
            {
                return;
            }

PInvoke:
            _Memmove(ref dest.Value, ref src.Value, len);
        }

        // Non-inlinable wrapper around the QCall that avoids polluting the fast path
        // with P/Invoke prolog/epilog.
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        private unsafe static void _Memmove(byte* dest, byte* src, nuint len)
        {
            __Memmove(dest, src, len);
        }

        // Non-inlinable wrapper around the QCall that avoids polluting the fast path
        // with P/Invoke prolog/epilog.
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        private unsafe static void _Memmove(ref byte dest, ref byte src, nuint len)
        {
            fixed (byte* pDest = &dest)
            fixed (byte* pSrc = &src)
                __Memmove(pDest, pSrc, len);
        }

        [DllImport(JitHelpers.QCall, CharSet = CharSet.Unicode)]
        extern private unsafe static void __Memmove(byte* dest, byte* src, nuint len);

        private unsafe static void WriteValueVectorized<T>(T value, ref Vector<T> start, nuint vectorElemCount) where T : struct
        {
            var iter = new ByReference<Vector<T>>(ref start);
            var vector = new Vector<T>(value);

            Debug.Assert(vectorElemCount >= 4); // condition should've been checked by caller

            do
            {
                Unsafe.WriteUnaligned(ref Unsafe.As<Vector<T>, byte>(ref iter.Value), vector);
                Unsafe.WriteUnaligned(ref Unsafe.As<Vector<T>, byte>(ref Unsafe.Add(ref iter.Value, 1)), vector);
                Unsafe.WriteUnaligned(ref Unsafe.As<Vector<T>, byte>(ref Unsafe.Add(ref iter.Value, 2)), vector);
                Unsafe.WriteUnaligned(ref Unsafe.As<Vector<T>, byte>(ref Unsafe.Add(ref iter.Value, 3)), vector);
                iter = new ByReference<Vector<T>>(ref Unsafe.Add(ref iter.Value, 4));
            } while ((vectorElemCount -= 4) > 0);

            if (vectorElemCount >= 2)
            {
                Unsafe.WriteUnaligned(ref Unsafe.As<Vector<T>, byte>(ref iter.Value), vector);
                Unsafe.WriteUnaligned(ref Unsafe.As<Vector<T>, byte>(ref Unsafe.Add(ref iter.Value, 1)), vector);
                iter = new ByReference<Vector<T>>(ref Unsafe.Add(ref iter.Value, 2));
                vectorElemCount -= 2;
            }

            if (vectorElemCount > 0)
            {
                Unsafe.WriteUnaligned(ref Unsafe.As<Vector<T>, byte>(ref iter.Value), vector);
            }
        }

        internal unsafe static void FillPrimitiveUInt16(uint extendedValue, ByReference<ushort> start, nuint elemCount)
        {
            // Special-case small buffers

            if (elemCount < 16)
            {
                goto FillSmall;
            }

            // Try vectorizing if the hardware allows it and we have enough data to vectorize.

            if (Vector.IsHardwareAccelerated && elemCount >= (uint)(2 * Vector<ushort>.Count))
            {
                nuint vectorElementCount = elemCount / (uint)Vector<ushort>.Count;
                ref var vectorRef = ref Unsafe.As<ushort, Vector<uint>>(ref start.Value);
                WriteValueVectorized(extendedValue, ref vectorRef, vectorElementCount);
                start = new ByReference<ushort>(ref Unsafe.As<Vector<uint>, ushort>(ref Unsafe.Add(ref vectorRef, (IntPtr)(nint)vectorElementCount)));
                elemCount &= (nuint)(Vector<ushort>.Count - 1);

                // JIT turns this into an unconditional jump if we can never hit the main loop after vectorization
                if (Vector<ushort>.Count <= 16 || elemCount < 16)
                {
                    goto FillSmall;
                }
            }

            // The main loop below writes *backward*

            nuint extendedValueNative = extendedValue;
#if BIT64
            extendedValueNative += (extendedValueNative << 32);
#endif

            do
            {
                Unsafe.WriteUnaligned(ref Unsafe.As<ushort, byte>(ref Unsafe.AddByteOffset(ref Unsafe.Add(ref start.Value, (IntPtr)(nint)elemCount), unchecked((nuint)(-1 * sizeof(nuint))))), extendedValueNative);
                Unsafe.WriteUnaligned(ref Unsafe.As<ushort, byte>(ref Unsafe.AddByteOffset(ref Unsafe.Add(ref start.Value, (IntPtr)(nint)elemCount), unchecked((nuint)(-2 * sizeof(nuint))))), extendedValueNative);
                Unsafe.WriteUnaligned(ref Unsafe.As<ushort, byte>(ref Unsafe.AddByteOffset(ref Unsafe.Add(ref start.Value, (IntPtr)(nint)elemCount), unchecked((nuint)(-3 * sizeof(nuint))))), extendedValueNative);
                Unsafe.WriteUnaligned(ref Unsafe.As<ushort, byte>(ref Unsafe.AddByteOffset(ref Unsafe.Add(ref start.Value, (IntPtr)(nint)elemCount), unchecked((nuint)(-4 * sizeof(nuint))))), extendedValueNative);
#if !BIT64
                Unsafe.WriteUnaligned(ref Unsafe.As<ushort, byte>(ref Unsafe.AddByteOffset(ref Unsafe.Add(ref start.Value, (IntPtr)(nint)elemCount), unchecked((nuint)(-5 * sizeof(nuint))))), extendedValueNative);
                Unsafe.WriteUnaligned(ref Unsafe.As<ushort, byte>(ref Unsafe.AddByteOffset(ref Unsafe.Add(ref start.Value, (IntPtr)(nint)elemCount), unchecked((nuint)(-6 * sizeof(nuint))))), extendedValueNative);
                Unsafe.WriteUnaligned(ref Unsafe.As<ushort, byte>(ref Unsafe.AddByteOffset(ref Unsafe.Add(ref start.Value, (IntPtr)(nint)elemCount), unchecked((nuint)(-7 * sizeof(nuint))))), extendedValueNative);
                Unsafe.WriteUnaligned(ref Unsafe.As<ushort, byte>(ref Unsafe.AddByteOffset(ref Unsafe.Add(ref start.Value, (IntPtr)(nint)elemCount), unchecked((nuint)(-8 * sizeof(nuint))))), extendedValueNative);
#endif

                // The reason for the strangely written check below is that this loop should execute multiple times
                // *only if* there were at least 32 elements to fill. But if vectorization is enabled, a 32+ element
                // buffer should have been caught by the vectorization logic at the top of this method (subject to
                // the vector length). If this is the case then the first part of the condition below evaluates to
                // 'false' at compile time, causing the JIT to skip the check entirely, automatically falling through
                // to the "handle remaining data" logic immediately below.
            } while ((elemCount -= 16) > 16 && (!Vector.IsHardwareAccelerated || Vector<ushort>.Count > 16));

            // The logic below writes *forward* (and may overlap with the backward-written buffer above, which is ok)

            if (elemCount > 0)
            {
                // Handle 1 - 8 elements
                Unsafe.WriteUnaligned(ref Unsafe.As<nuint, byte>(ref Unsafe.Add(ref Unsafe.As<ushort, nuint>(ref start.Value), 0)), extendedValueNative);
                Unsafe.WriteUnaligned(ref Unsafe.As<nuint, byte>(ref Unsafe.Add(ref Unsafe.As<ushort, nuint>(ref start.Value), 1)), extendedValueNative);
#if !BIT64
                Unsafe.WriteUnaligned(ref Unsafe.As<nuint, byte>(ref Unsafe.Add(ref Unsafe.As<ushort, nuint>(ref start.Value), 2)), extendedValueNative);
                Unsafe.WriteUnaligned(ref Unsafe.As<nuint, byte>(ref Unsafe.Add(ref Unsafe.As<ushort, nuint>(ref start.Value), 3)), extendedValueNative);
#endif
                if (elemCount > 8)
                {
                    // Handle 9 - 16 elements
#if BIT64
                    const int InnerOffset = 2;
#else
                    const int InnerOffset = 4;
#endif
                    Unsafe.WriteUnaligned(ref Unsafe.As<nuint, byte>(ref Unsafe.Add(ref Unsafe.As<ushort, nuint>(ref start.Value), InnerOffset + 0)), extendedValueNative);
                    Unsafe.WriteUnaligned(ref Unsafe.As<nuint, byte>(ref Unsafe.Add(ref Unsafe.As<ushort, nuint>(ref start.Value), InnerOffset + 1)), extendedValueNative);
#if !BIT64
                    Unsafe.WriteUnaligned(ref Unsafe.As<nuint, byte>(ref Unsafe.Add(ref Unsafe.As<ushort, nuint>(ref start.Value), InnerOffset + 2)), extendedValueNative);
                    Unsafe.WriteUnaligned(ref Unsafe.As<nuint, byte>(ref Unsafe.Add(ref Unsafe.As<ushort, nuint>(ref start.Value), InnerOffset + 3)), extendedValueNative);
#endif
                }
            }

            goto Return;

FillSmall:

            // The logic below writes both forward and backward (and may overlap, which is ok)

            Debug.Assert(elemCount < 16);
            if (elemCount > 4)
            {
                // Handle lengths of 5 - 8 elements
                Unsafe.WriteUnaligned(ref Unsafe.As<ushort, byte>(ref start.Value), extendedValue);
                Unsafe.WriteUnaligned(ref Unsafe.As<ushort, byte>(ref Unsafe.AddByteOffset(ref start.Value, sizeof(uint))), extendedValue);
                Unsafe.WriteUnaligned(ref Unsafe.As<ushort, byte>(ref Unsafe.AddByteOffset(ref Unsafe.Add(ref start.Value, (IntPtr)(nint)elemCount), unchecked((nuint)(-1 * sizeof(uint))))), extendedValue);
                Unsafe.WriteUnaligned(ref Unsafe.As<ushort, byte>(ref Unsafe.AddByteOffset(ref Unsafe.Add(ref start.Value, (IntPtr)(nint)elemCount), unchecked((nuint)(-2 * sizeof(uint))))), extendedValue);

                if (elemCount > 8)
                {
                    // Handle lengths of 9 - 15 elements
                    Unsafe.WriteUnaligned(ref Unsafe.As<ushort, byte>(ref Unsafe.AddByteOffset(ref start.Value, 2 * sizeof(uint))), extendedValue);
                    Unsafe.WriteUnaligned(ref Unsafe.As<ushort, byte>(ref Unsafe.AddByteOffset(ref start.Value, 3 * sizeof(uint))), extendedValue);
                    Unsafe.WriteUnaligned(ref Unsafe.As<ushort, byte>(ref Unsafe.AddByteOffset(ref Unsafe.Add(ref start.Value, (IntPtr)(nint)elemCount), unchecked((nuint)(-3 * sizeof(uint))))), extendedValue);
                    Unsafe.WriteUnaligned(ref Unsafe.As<ushort, byte>(ref Unsafe.AddByteOffset(ref Unsafe.Add(ref start.Value, (IntPtr)(nint)elemCount), unchecked((nuint)(-4 * sizeof(uint))))), extendedValue);
                }
            }
            else if (elemCount >= 2)
            {
                // Handle lengths of 2 - 4 elements
                Unsafe.WriteUnaligned(ref Unsafe.As<ushort, byte>(ref start.Value), extendedValue);
                Unsafe.WriteUnaligned(ref Unsafe.As<ushort, byte>(ref Unsafe.AddByteOffset(ref Unsafe.Add(ref start.Value, (IntPtr)(nint)elemCount), unchecked((nuint)(-sizeof(uint))))), extendedValue);
            }
            else if (elemCount > 0)
            {
                // Handle single element
                start.Value = (ushort)extendedValue;
            }

Return:

            return;
        }

        internal unsafe static void FillPrimitiveUInt16_a(uint extendedValue, ref ushort start, nuint elemCount)
        {
            // The main fill method writes in blocks of 16 (or more) elements each.
            // This pre-method is responsible for writing anything that doesn't
            // nicely fit in to a multiple of 16.

            // This method writes to the buffer both forward and backward
            // We're ok with taking overlapping writes to avoid 'if' checks

            if ((elemCount & (8 | 4)) != 0)
            {
                // elemCount MOD 16 is in range (4 .. 15)
                var remainderElemCount = elemCount & 15;
                Unsafe.WriteUnaligned(ref Unsafe.As<ushort, byte>(ref Unsafe.AddByteOffset(ref start, 0 * sizeof(uint))), extendedValue);
                Unsafe.WriteUnaligned(ref Unsafe.As<ushort, byte>(ref Unsafe.AddByteOffset(ref start, 1 * sizeof(uint))), extendedValue);
                Unsafe.WriteUnaligned(ref Unsafe.As<ushort, byte>(ref Unsafe.AddByteOffset(ref Unsafe.Add(ref start, (IntPtr)(nint)remainderElemCount), unchecked((nuint)(-1 * sizeof(uint))))), extendedValue);
                Unsafe.WriteUnaligned(ref Unsafe.As<ushort, byte>(ref Unsafe.AddByteOffset(ref Unsafe.Add(ref start, (IntPtr)(nint)remainderElemCount), unchecked((nuint)(-2 * sizeof(uint))))), extendedValue);
                if (remainderElemCount > 8)
                {
                    // elemCount MOD 16 is in range (9 .. 15)
                    Unsafe.WriteUnaligned(ref Unsafe.As<ushort, byte>(ref Unsafe.AddByteOffset(ref start, 2 * sizeof(uint))), extendedValue);
                    Unsafe.WriteUnaligned(ref Unsafe.As<ushort, byte>(ref Unsafe.AddByteOffset(ref start, 3 * sizeof(uint))), extendedValue);
                    Unsafe.WriteUnaligned(ref Unsafe.As<ushort, byte>(ref Unsafe.AddByteOffset(ref Unsafe.Add(ref start, (IntPtr)(nint)remainderElemCount), unchecked((nuint)(-3 * sizeof(uint))))), extendedValue);
                    Unsafe.WriteUnaligned(ref Unsafe.As<ushort, byte>(ref Unsafe.AddByteOffset(ref Unsafe.Add(ref start, (IntPtr)(nint)remainderElemCount), unchecked((nuint)(-4 * sizeof(uint))))), extendedValue);
                }
            }
            else if ((elemCount & 2) != 0)
            {
                // elemCount MOD 16 is in range (2 .. 3)
                var remainderElemCount = elemCount & 15;
                Unsafe.WriteUnaligned(ref Unsafe.As<ushort, byte>(ref Unsafe.AddByteOffset(ref start, 0 * sizeof(uint))), extendedValue);
                Unsafe.WriteUnaligned(ref Unsafe.As<ushort, byte>(ref Unsafe.AddByteOffset(ref Unsafe.Add(ref start, (IntPtr)(nint)remainderElemCount), unchecked((nuint)(-1 * sizeof(uint))))), extendedValue);
            }
            else if ((elemCount & 1) != 0)
            {
                // elemCount MOD 16 is 1
                start = (ushort)extendedValue;
            }
            else if (elemCount >= 16)
            {
                // elemCount MOD 16 is 0 AND there's enough data for bulk fill,
                // no fixup needed before calling bulk fill method
                FillPrimitiveUInt16_b(extendedValue, ref start, elemCount);
                return;
            }


            // Now that the remaining data is a perfect multiple of 16 elements, process it in bulk.

            if (elemCount >= 16)
            {
                Debug.Assert((elemCount % 16) != 0); // should've gone down "non-fixup" path if no remainder

                FillPrimitiveUInt16_b(
                    extendedValue,
                    ref Unsafe.Add(ref start, (IntPtr)(nint)(elemCount & (nuint)15)),
                    elemCount & unchecked((nuint)~15));
            }
        }

        internal unsafe static void FillPrimitiveUInt16_b(uint extendedValue, ref ushort startA, nuint elemCount)
        {
            Debug.Assert(elemCount % 16 == 0);

            var start = new ByReference<ushort>(ref startA);

            // Try vectorizing if the hardware allows it and we have enough data to vectorize.
            // Preliminary testing shows that 4x vector width is the inflection point for perf.

            if (Vector.IsHardwareAccelerated && elemCount >= (uint)(4 * Vector<ushort>.Count))
            {
                // It's ok to use Vector<ushort> and Vector<uint> interchangeably here since they have
                // the same width. We use Vector<uint> under the covers since the incoming value
                // is already extended out to 32 bits.

                Debug.Assert(Unsafe.SizeOf<Vector<ushort>>() == Unsafe.SizeOf<Vector<uint>>());

                nuint vectorCount = elemCount / (nuint)Vector<ushort>.Count;
                ref var vectorRef = ref Unsafe.As<ushort, Vector<uint>>(ref start.Value);
                WriteValueVectorized(extendedValue, ref vectorRef, vectorCount);

                // Our non-vectorized loop works on 16-element blocks. If a 16-element block
                // is a whole multiple of the vector size, then there should be no elements
                // left over after vectorization, so we don't need to run the loop. The JIT
                // can check this at compile time.

                if ((16 % Vector<ushort>.Count) == 0)
                {
                    return;
                }

                start = new ByReference<ushort>(ref Unsafe.As<Vector<uint>, ushort>(ref Unsafe.Add(ref vectorRef, (IntPtr)(nint)vectorCount)));
                elemCount %= (nuint)Vector<ushort>.Count;
            }

            // The main loop below writes *backward*.
            // We write blocks out 16 elements at a time.

            nuint extendedValueNative = extendedValue;
#if BIT64
            extendedValueNative += (extendedValueNative << 32);
#endif

            do
            {
                Unsafe.WriteUnaligned(ref Unsafe.As<ushort, byte>(ref Unsafe.AddByteOffset(ref Unsafe.Add(ref start.Value, (IntPtr)(nint)elemCount), unchecked((nuint)(-1 * sizeof(nuint))))), extendedValueNative);
                Unsafe.WriteUnaligned(ref Unsafe.As<ushort, byte>(ref Unsafe.AddByteOffset(ref Unsafe.Add(ref start.Value, (IntPtr)(nint)elemCount), unchecked((nuint)(-2 * sizeof(nuint))))), extendedValueNative);
                Unsafe.WriteUnaligned(ref Unsafe.As<ushort, byte>(ref Unsafe.AddByteOffset(ref Unsafe.Add(ref start.Value, (IntPtr)(nint)elemCount), unchecked((nuint)(-3 * sizeof(nuint))))), extendedValueNative);
                Unsafe.WriteUnaligned(ref Unsafe.As<ushort, byte>(ref Unsafe.AddByteOffset(ref Unsafe.Add(ref start.Value, (IntPtr)(nint)elemCount), unchecked((nuint)(-4 * sizeof(nuint))))), extendedValueNative);
#if !BIT64
                Unsafe.WriteUnaligned(ref Unsafe.As<ushort, byte>(ref Unsafe.AddByteOffset(ref Unsafe.Add(ref start.Value, (IntPtr)(nint)elemCount), unchecked((nuint)(-5 * sizeof(nuint))))), extendedValueNative);
                Unsafe.WriteUnaligned(ref Unsafe.As<ushort, byte>(ref Unsafe.AddByteOffset(ref Unsafe.Add(ref start.Value, (IntPtr)(nint)elemCount), unchecked((nuint)(-6 * sizeof(nuint))))), extendedValueNative);
                Unsafe.WriteUnaligned(ref Unsafe.As<ushort, byte>(ref Unsafe.AddByteOffset(ref Unsafe.Add(ref start.Value, (IntPtr)(nint)elemCount), unchecked((nuint)(-7 * sizeof(nuint))))), extendedValueNative);
                Unsafe.WriteUnaligned(ref Unsafe.As<ushort, byte>(ref Unsafe.AddByteOffset(ref Unsafe.Add(ref start.Value, (IntPtr)(nint)elemCount), unchecked((nuint)(-8 * sizeof(nuint))))), extendedValueNative);
#endif
            } while ((elemCount -= 16) > 0);
        }

        internal unsafe static void FillPrimitive<TPrimitive, TStack>(TStack value, ByReference<TPrimitive> start, nuint elemCount)
            where TPrimitive : struct
            where TStack : struct
        {
            Debug.Assert((typeof(TPrimitive) == typeof(ushort) && typeof(TStack) == typeof(uint))
                || (typeof(TPrimitive) == typeof(uint) && typeof(TStack) == typeof(uint))
                || (typeof(TPrimitive) == typeof(ulong) && typeof(TStack) == typeof(ulong)));

            // If the full byte count of the data to be written is at least two vectors, go down the
            // vectorized code path. We perform the length check because otherwise the overhead of
            // spinning up the vectorized code path is too high.

            if (Vector.IsHardwareAccelerated && elemCount >= (nuint)2 * (nuint)Vector<TPrimitive>.Count)
            {
                ref var newStartingPoint = ref FillPrimitiveVectorized(
                    value,
                    new ByReference<Vector<TPrimitive>>(ref Unsafe.As<TPrimitive, Vector<TPrimitive>>(ref start.Value)),
                    new ByReference<Vector<TPrimitive>>(ref Unsafe.As<TPrimitive, Vector<TPrimitive>>(ref Unsafe.Add(ref start.Value, (IntPtr)(nint)elemCount))));

                elemCount -= (nuint)Unsafe.ByteOffset(ref start.Value, ref newStartingPoint) / (nuint)Unsafe.SizeOf<TPrimitive>();
                start = new ByReference<TPrimitive>(ref newStartingPoint);
            }

            // Special-case T being the natural word length
            // This is uint on 32-bit, ulong on 64-bit

            if (Unsafe.SizeOf<TPrimitive>() == sizeof(IntPtr))
            {
                FillNaturalWord(
                    JitHelpers.ChangeType<TStack, IntPtr>(value),
                    ref Unsafe.As<TPrimitive, IntPtr>(ref start.Value),
                    elemCount);
                return;
            }

            if (elemCount < (nuint)(2 * (Unsafe.SizeOf<IntPtr>() / Unsafe.SizeOf<TPrimitive>() - 1)))
            {
                goto SmallFill;
            }

            nuint fillPattern;

            // Special-case T being half the natural word length
            // This is ushort on 32-bit, uint on 32-bit

            if (Unsafe.SizeOf<TPrimitive>() == sizeof(IntPtr) / 2)
            {
                // Write out the first and last values

                start.Value = UnwrapInt<TPrimitive, TStack>(value);
                Unsafe.Add(ref start.Value, (IntPtr)(nint)(elemCount - 1)) = UnwrapInt<TPrimitive, TStack>(value);

                // Set the fill pattern and call into the natural word fill routine

#if BIT64
                fillPattern = UnwrapInt<uint, TStack>(value);
                fillPattern += (fillPattern << 32);
#elif BIT32
                fillPattern = UnwrapInt<ushort, TStack>(value);
                fillPattern += (fillPattern << 16);
#else
                throw new PlatformNotSupportedException();
#endif

                goto FillNaturalWord;
            }

            // At this point, the only thing left is ushort for 64-bit platforms.

#if BIT64
            if (typeof(TPrimitive) == typeof(ushort))
            {
                // Write out the first 3 and last 3 values

                start.Value = UnwrapInt<TPrimitive, TStack>(value);
                Unsafe.Add(ref start.Value, 1) = UnwrapInt<TPrimitive, TStack>(value);
                Unsafe.Add(ref start.Value, 2) = UnwrapInt<TPrimitive, TStack>(value);
                Unsafe.Add(ref start.Value, (IntPtr)(nint)(elemCount - 1)) = UnwrapInt<TPrimitive, TStack>(value);
                Unsafe.Add(ref start.Value, (IntPtr)(nint)(elemCount - 2)) = UnwrapInt<TPrimitive, TStack>(value);
                Unsafe.Add(ref start.Value, (IntPtr)(nint)(elemCount - 3)) = UnwrapInt<TPrimitive, TStack>(value);

                // Set the fill pattern and call into the natural word fill routine

                fillPattern = JitHelpers.ChangeType<TStack, uint>(value) * 0x0001000100010001UL;

                goto FillNaturalWord;
            }
#endif

            // If we reached this point, we saw an unexpected T.

            throw new PlatformNotSupportedException();

SmallFill:
            while (elemCount > 0)
            {
                Unsafe.Add(ref start.Value, (IntPtr)(nint)(--elemCount)) = UnwrapInt<TPrimitive, TStack>(value);
            }
            return;

FillNaturalWord:
            nuint shift = (nuint)Unsafe.AsPointer(ref start.Value) % sizeof(nuint);
            FillNaturalWord(
                (IntPtr)(nint)fillPattern,
                ref Unsafe.AddByteOffset(ref Unsafe.As<TPrimitive, IntPtr>(ref start.Value), (IntPtr)(-(nint)shift)),
                elemCount / (nuint)(Unsafe.SizeOf<IntPtr>() / Unsafe.SizeOf<TPrimitive>()));
        }

        private static void FillNaturalWord(IntPtr value, ref IntPtr r, nuint length)
        {
            nuint i = 0;
            for (; i < (length & ~(nuint)7); i += 8)
            {
                Unsafe.Add(ref r, (IntPtr)(nint)(i + 0)) = value;
                Unsafe.Add(ref r, (IntPtr)(nint)(i + 1)) = value;
                Unsafe.Add(ref r, (IntPtr)(nint)(i + 2)) = value;
                Unsafe.Add(ref r, (IntPtr)(nint)(i + 3)) = value;
                Unsafe.Add(ref r, (IntPtr)(nint)(i + 4)) = value;
                Unsafe.Add(ref r, (IntPtr)(nint)(i + 5)) = value;
                Unsafe.Add(ref r, (IntPtr)(nint)(i + 6)) = value;
                Unsafe.Add(ref r, (IntPtr)(nint)(i + 7)) = value;
            }
            if (i < (length & ~(nuint)3))
            {
                Unsafe.Add(ref r, (IntPtr)(nint)(i + 0)) = value;
                Unsafe.Add(ref r, (IntPtr)(nint)(i + 1)) = value;
                Unsafe.Add(ref r, (IntPtr)(nint)(i + 2)) = value;
                Unsafe.Add(ref r, (IntPtr)(nint)(i + 3)) = value;
                i += 4;
            }
            for (; i < length; i++)
            {
                Unsafe.Add(ref r, (IntPtr)(nint)i) = value;
            }
        }

        private static ref TPrimitive FillPrimitiveVectorized<TStack, TPrimitive>(TStack value, ByReference<Vector<TPrimitive>> start, ByReference<Vector<TPrimitive>> end)
            where TPrimitive : struct
            where TStack : struct
        {
            // n.b. We're relying on the infrastructure to handle unaligned writes correctly.

            // Precondition checking

            Debug.Assert(Vector.IsHardwareAccelerated);
            Debug.Assert((nuint)Unsafe.ByteOffset(ref start.Value, ref end.Value) >= (nuint)(2 * Unsafe.SizeOf<Vector<TPrimitive>>()));
            Debug.Assert(!RuntimeHelpers.IsReferenceOrContainsReferences<TPrimitive>());

            // First, write two vectors

            var vectorizedValue = new Vector<TPrimitive>(UnwrapInt<TPrimitive, TStack>(value));

            start.Value = vectorizedValue;
            Unsafe.Add(ref start.Value, 1) = vectorizedValue;

            // Calculate new bounds

            start = new ByReference<Vector<TPrimitive>>(ref Unsafe.Add(ref start.Value, 2));
            nint bytesRemaining = (nint)Unsafe.ByteOffset(ref start.Value, ref end.Value);

            // Handle 4 vectors at a time

            while ((nuint)bytesRemaining >= 4 * (nuint)Unsafe.SizeOf<Vector<TPrimitive>>())
            {
                start.Value = vectorizedValue;
                Unsafe.Add(ref start.Value, 1) = vectorizedValue;
                Unsafe.Add(ref start.Value, 2) = vectorizedValue;
                Unsafe.Add(ref start.Value, 3) = vectorizedValue;
                start = new ByReference<Vector<TPrimitive>>(ref Unsafe.Add(ref start.Value, 4));
                bytesRemaining = (nint)Unsafe.ByteOffset(ref start.Value, ref end.Value);
            }

            // Handle 2 vectors at a time if possible

            Debug.Assert((nuint)bytesRemaining < 4 * (nuint)Unsafe.SizeOf<Vector<TPrimitive>>());

            if ((nuint)bytesRemaining >= 2 * (nuint)Unsafe.SizeOf<Vector<TPrimitive>>())
            {
                start.Value = vectorizedValue;
                Unsafe.Add(ref start.Value, 1) = vectorizedValue;

                if ((nuint)bytesRemaining >= 3 * (nuint)Unsafe.SizeOf<Vector<TPrimitive>>())
                {
                    Unsafe.Add(ref start.Value, 2) = vectorizedValue;
                    return ref Unsafe.As<Vector<TPrimitive>, TPrimitive>(ref Unsafe.Add(ref start.Value, 3));
                }
                return ref Unsafe.As<Vector<TPrimitive>, TPrimitive>(ref Unsafe.Add(ref start.Value, 2));
            }
            else if ((nuint)bytesRemaining >= (nuint)Unsafe.SizeOf<Vector<TPrimitive>>())
            {
                start.Value = vectorizedValue;
                return ref Unsafe.As<Vector<TPrimitive>, TPrimitive>(ref Unsafe.Add(ref start.Value, 1));
            }
            else
            {
                return ref Unsafe.As<Vector<TPrimitive>, TPrimitive>(ref start.Value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static TPrimitive UnwrapInt<TPrimitive, TStack>(TStack value)
            where TPrimitive : struct
            where TStack : struct
        {
            if (typeof(TPrimitive) == typeof(ushort))
            {
                Debug.Assert(typeof(TStack) == typeof(uint));
                return JitHelpers.ChangeType<ushort, TPrimitive>((ushort)JitHelpers.ChangeType<TStack, uint>(value));
            }
            else
            {
                Debug.Assert(Unsafe.SizeOf<TPrimitive>() == Unsafe.SizeOf<TStack>());
                return JitHelpers.ChangeType<TStack, TPrimitive>(value);
            }
        }

        // The attributes on this method are chosen for best JIT performance. 
        // Please do not edit unless intentional.
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public static unsafe void MemoryCopy(void* source, void* destination, long destinationSizeInBytes, long sourceBytesToCopy)
        {
            if (sourceBytesToCopy > destinationSizeInBytes)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.sourceBytesToCopy);
            }
            Memmove((byte*)destination, (byte*)source, checked((nuint)sourceBytesToCopy));
        }


        // The attributes on this method are chosen for best JIT performance. 
        // Please do not edit unless intentional.
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public static unsafe void MemoryCopy(void* source, void* destination, ulong destinationSizeInBytes, ulong sourceBytesToCopy)
        {
            if (sourceBytesToCopy > destinationSizeInBytes)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.sourceBytesToCopy);
            }
#if BIT64
            Memmove((byte*)destination, (byte*)source, sourceBytesToCopy);
#else // BIT64
            Memmove((byte*)destination, (byte*)source, checked((uint)sourceBytesToCopy));
#endif // BIT64
        }
        
#if HAS_CUSTOM_BLOCKS
        [StructLayout(LayoutKind.Sequential, Size = 16)]
        private struct Block16 { }

        [StructLayout(LayoutKind.Sequential, Size = 64)]
        private struct Block64 { } 
#endif // HAS_CUSTOM_BLOCKS         
    }
}
