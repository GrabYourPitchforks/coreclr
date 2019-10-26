// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System.Text
{
    /// <summary>
    /// Contains useful methods for dealing with byte sequences that represent ASCII text.
    /// </summary>
    public static class Ascii
    {
        /// <summary>
        /// Compares two buffers for equality, ignoring case differences in ['a' - 'z'] and ['A' - 'Z'].
        /// All other bytes, including non-ASCII bytes, are checked for ordinal equality.
        /// </summary>
        /// <param name="left">The first buffer to compare.</param>
        /// <param name="right">The second buffer to compare.</param>
        /// <returns>
        /// <see langword="true"/> if the buffers are equal; <see langword="false"/> otherwise.
        /// </returns>
        /// <remarks>
        /// This method contains short-circuiting logic and isn't appropriate for security-related
        /// routines which require constant-time comparisons.
        /// </remarks>
        public static bool EqualsOrdinalIgnoreCase(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
        {
            // Quick short-circuiting checks: are the buffers different lengths, or do
            // they perhaps point to the same memory address?

            if (left.Length != right.Length)
            {
                return false;
            }

            if (Unsafe.AreSame(ref MemoryMarshal.GetReference(left), ref MemoryMarshal.GetReference(right)))
            {
                return true;
            }

            // Check byte-by-byte.

            for (int i = 0; i < left.Length; i++)
            {
                uint a = left[i];
                if (UnicodeUtility.IsInRangeInclusive(a, 'a', 'z'))
                {
                    a ^= 0x20; // normalize to uppercase
                }

                uint b = right[i];
                if (UnicodeUtility.IsInRangeInclusive(b, 'a', 'z'))
                {
                    b ^= 0x20; // normalize to uppercase
                }

                if (a != b)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Copies bytes from <paramref name="source"/> to <paramref name="destination"/>,
        /// converting the ASCII bytes ['A' - 'Z'] (U+0041..U+005A) to lowercase
        /// ['a' - 'z'] (U+0061..U+007A) along the way, leaving all other bytes unchanged.
        /// </summary>
        /// <param name="source">The buffer from which to read source data.</param>
        /// <param name="destination">The buffer to which to write destination data.</param>
        /// <returns>
        /// The number of bytes written to <paramref name="destination"/>,
        /// or -1 if <paramref name="destination"/> is too small to hold the resulting data.
        /// </returns>
        /// <remarks>
        /// The behavior of this method is undefined if <paramref name="source"/>
        /// and <paramref name="destination"/> overlap.
        /// </remarks>
        public static int ToLowerInvariant(ReadOnlySpan<byte> source, Span<byte> destination)
        {
            if (destination.Length < source.Length)
            {
                return -1;
            }

            for (int i = 0; i < source.Length; i++)
            {
                uint b = source[i];
                if (UnicodeUtility.IsInRangeInclusive(b, 'A', 'Z'))
                {
                    b ^= 0x20;
                }
                destination[i] = (byte)b;
            }

            return source.Length;
        }

        /// <summary>
        /// Converts the ASCII bytes ['A' - 'Z'] (U+0041..U+005A) to lowercase
        /// ['a' - 'z'] (U+0061..U+007A), leaving all other bytes unchanged.
        /// The buffer is modified in-place.
        /// </summary>
        /// <param name="span">The buffer to convert to lowercase.</param>
        public static void ToLowerInvariantInPlace(Span<byte> span)
        {
            for (int i = 0; i < span.Length; i++)
            {
                uint b = span[i];
                if (UnicodeUtility.IsInRangeInclusive(b, 'A', 'Z'))
                {
                    span[i] = (byte)(b ^ 0x20);
                }
            }
        }

        /// <summary>
        /// Copies bytes from <paramref name="source"/> to <paramref name="destination"/>,
        /// converting the ASCII bytes ['a' - 'z'] (U+0061..U+007A) to uppercase
        /// ['A' - 'Z'] (U+0041..U+005A) along the way, leaving all other bytes unchanged.
        /// </summary>
        /// <param name="source">The buffer from which to read source data.</param>
        /// <param name="destination">The buffer to which to write destination data.</param>
        /// <returns>
        /// The number of bytes written to <paramref name="destination"/>,
        /// or -1 if <paramref name="destination"/> is too small to hold the resulting data.
        /// </returns>
        /// <remarks>
        /// The behavior of this method is undefined if <paramref name="source"/>
        /// and <paramref name="destination"/> overlap.
        /// </remarks>
        public static int ToUpperInvariant(ReadOnlySpan<byte> source, Span<byte> destination)
        {
            if (destination.Length < source.Length)
            {
                return -1;
            }

            for (int i = 0; i < source.Length; i++)
            {
                uint b = source[i];
                if (UnicodeUtility.IsInRangeInclusive(b, 'a', 'z'))
                {
                    b ^= 0x20;
                }
                destination[i] = (byte)b;
            }

            return source.Length;
        }

        /// <summary>
        /// Converts the ASCII bytes ['a' - 'z'] (U+0061..U+007A) to uppercase
        /// ['A' - 'Z'] (U+0041..U+005A), leaving all other bytes unchanged.
        /// The buffer is modified in-place.
        /// </summary>
        /// <param name="span">The buffer to convert to uppercase.</param>
        public static void ToUpperInvariantInPlace(Span<byte> span)
        {
            for (int i = 0; i < span.Length; i++)
            {
                uint b = span[i];
                if (UnicodeUtility.IsInRangeInclusive(b, 'a', 'z'))
                {
                    span[i] = (byte)(b ^ 0x20);
                }
            }
        }
    }
}
