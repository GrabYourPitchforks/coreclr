// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Text.Unicode
{
    /// <summary>
    /// Contains useful methods for dealing with byte sequences that represent ASCII text.
    /// </summary>
    public static class Ascii
    {
        /// <summary>
        /// Returns a value stating whether <paramref name="value"/> appears anywhere in <paramref name="span"/>,
        /// ignoring case differences in ['a' - 'z'] and ['A' - 'Z']. All other bytes, including
        /// non-ASCII bytes, are checked for ordinal equality.
        /// </summary>
        /// <param name="span">The buffer in which to search for <paramref name="value"/>.</param>
        /// <param name="value">The buffer to seek.</param>
        /// <returns><see langword="true"/> if <paramref name="value"/> appears somewhere in <paramref name="span"/>;
        /// otherwise, <see langword="false"/>.</returns>
        public static bool ContainsIgnoreCase(ReadOnlySpan<byte> span, ReadOnlySpan<byte> value) => IndexOfIgnoreCase(span, value) >= 0;

        /// <summary>
        /// Returns a value stating whether <paramref name="value"/> appears at the end of <paramref name="span"/>,
        /// ignoring case differences in ['a' - 'z'] and ['A' - 'Z']. All other bytes, including
        /// non-ASCII bytes, are checked for ordinal equality.
        /// </summary>
        /// <param name="span">The buffer in which to search for <paramref name="value"/>.</param>
        /// <param name="value">The buffer to seek.</param>
        /// <returns><see langword="true"/> if <paramref name="value"/> appears at the end of <paramref name="span"/>;
        /// otherwise, <see langword="false"/>.</returns>
        public static bool EndsWithIgnoreCase(ReadOnlySpan<byte> span, ReadOnlySpan<byte> value)
        {
            return value.Length <= span.Length
                && EqualsIgnoreCase(span[^value.Length..], value);
        }

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
        public static bool EqualsIgnoreCase(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
        {
            // Quick short-circuiting checks: are the buffers different lengths, or do
            // they perhaps point to the same memory address?

            if (left.Length != right.Length)
            {
                return false;
            }

            if (left == right) // referential equality
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
        /// Returns the index of the first non-ASCII byte in the buffer.
        /// </summary>
        /// <param name="span">The buffer to check.</param>
        /// <returns>The index in <paramref name="span"/> where the first non-ASCII byte appears,
        /// or -1 if <paramref name="span"/> contains only ASCII bytes.</returns>
        /// <remarks>
        /// Empty buffers are considered all-ASCII.
        /// </remarks>
        public static int GetIndexOfFirstNonAsciiByte(ReadOnlySpan<byte> span)
        {
            for (int i = 0; i < span.Length; i++)
            {
                // TODO_UTF8STRING: Use the AsciiUtility intrinsics for the below check.

                if ((sbyte)span[i] < 0)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Returns the index in <paramref name="span"/> where <paramref name="value"/> first appears,
        /// ignoring case differences in ['a' - 'z'] and ['A' - 'Z']. All other bytes, including
        /// non-ASCII bytes, are checked for ordinal equality.
        /// </summary>
        /// <param name="span">The buffer in which to search for <paramref name="value"/>.</param>
        /// <param name="value">The buffer to seek.</param>
        /// <returns>
        /// The smallest non-negative integer 'i' for which the expression "EqualsIgnoreCase(span.Slice(i, value.Length), value)"
        /// evaluates to <see langword="true"/>; or -1 if that expression will never evaluate to <see langword="true"/>.
        /// </returns>
        public static int IndexOfIgnoreCase(ReadOnlySpan<byte> span, ReadOnlySpan<byte> value)
        {
            // Short-circuiting checks.

            if (value.IsEmpty)
            {
                return 0;
            }

            // TODO_UTF8STRING: Optimize below code path.
            // This algorithm is O(m * n), where 'm' and 'n' are the lengths of the input buffers.

            for (int i = 0; i < span.Length - value.Length; i++)
            {
                if (EqualsIgnoreCase(span.Slice(i, value.Length), value))
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Returns the index in <paramref name="span"/> where <paramref name="value"/> last appears,
        /// ignoring case differences in ['a' - 'z'] and ['A' - 'Z']. All other bytes, including
        /// non-ASCII bytes, are checked for ordinal equality.
        /// </summary>
        /// <param name="span">The buffer in which to search for <paramref name="value"/>.</param>
        /// <param name="value">The buffer to seek.</param>
        /// <returns>
        /// The largest non-negative integer 'i' for which the expression "EqualsIgnoreCase(span.Slice(i, value.Length), value)"
        /// evaluates to <see langword="true"/>; or -1 if that expression will never evaluate to <see langword="true"/>.
        /// </returns>
        public static int IndexOfLastIgnoreCase(ReadOnlySpan<byte> span, ReadOnlySpan<byte> value)
        {
            // Short-circuiting checks.

            if (value.IsEmpty)
            {
                return span.Length;
            }

            // TODO_UTF8STRING: Optimize below code path.
            // This algorithm is O(m * n), where 'm' and 'n' are the lengths of the input buffers.

            for (int i = span.Length - value.Length; i >= 0; i--)
            {
                if (EqualsIgnoreCase(span.Slice(i, value.Length), value))
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Returns a value stating whether the buffer contains only ASCII bytes (00..7F, inclusive).
        /// </summary>
        /// <param name="span">The buffer to check.</param>
        /// <returns><see langword="true"/> if <paramref name="span"/> contains only ASCII bytes;
        /// <see langword="false"/> if <paramref name="span"/> contains any non-ASCII bytes.</returns>
        /// <remarks>
        /// Empty buffers are considered all-ASCII.
        /// </remarks>
        public static bool IsAllAscii(ReadOnlySpan<byte> span)
        {
            // TODO_UTF8STRING: Use the AsciiUtility intrinsics for the below check.

            return GetIndexOfFirstNonAsciiByte(span) < 0;
        }

        /// <summary>
        /// Returns a value stating whether <paramref name="value"/> appears at the beginning of <paramref name="span"/>,
        /// ignoring case differences in ['a' - 'z'] and ['A' - 'Z']. All other bytes, including
        /// non-ASCII bytes, are checked for ordinal equality.
        /// </summary>
        /// <param name="span">The buffer in which to search for <paramref name="value"/>.</param>
        /// <param name="value">The buffer to seek.</param>
        /// <returns><see langword="true"/> if <paramref name="value"/> appears at the beginning of <paramref name="span"/>;
        /// otherwise, <see langword="false"/>.</returns>
        public static bool StartsWithIgnoreCase(ReadOnlySpan<byte> span, ReadOnlySpan<byte> value)
        {
            return value.Length <= span.Length
                && EqualsIgnoreCase(span[..value.Length], value);
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

        /// <summary>
        /// Trims ASCII whitespace (U+0009..U+000D, U+0020) from the beginning and the end
        /// of the buffer, returning the slice of the buffer which remains.
        /// </summary>
        /// <param name="span">The buffer to trim.</param>
        /// <returns>A slice of <paramref name="span"/> where whitespace has been trimmed from
        /// the beginning and the end.</returns>
        public static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> span)
        {
            return TrimEnd(TrimStart(span));
        }

        /// <summary>
        /// Trims ASCII whitespace (U+0009..U+000D, U+0020) from the beginning and the end
        /// of the buffer, returning the slice of the buffer which remains.
        /// </summary>
        /// <param name="span">The buffer to trim.</param>
        /// <returns>A slice of <paramref name="span"/> where whitespace has been trimmed from
        /// the beginning and the end.</returns>
        /// <remarks>
        /// This method does not modify the contents of <paramref name="span"/>.
        /// </remarks>
        public static Span<byte> Trim(Span<byte> span)
        {
            return TrimEnd(TrimStart(span));
        }

        /// <summary>
        /// Trims ASCII whitespace (U+0009..U+000D, U+0020) from the end
        /// of the buffer, returning the slice of the buffer which remains.
        /// </summary>
        /// <param name="span">The buffer to trim.</param>
        /// <returns>A slice of <paramref name="span"/> where whitespace has been trimmed from
        /// the end.</returns>
        public static ReadOnlySpan<byte> TrimEnd(ReadOnlySpan<byte> span)
        {
            int i = span.Length - 1;
            for (; (uint)i < (uint)span.Length; i--)
            {
                uint thisByte = span[i];
                if (thisByte > 0x7F || !char.IsWhiteSpace((char)thisByte))
                {
                    break;
                }
            }

            return span.Slice(0, i + 1);
        }

        /// <summary>
        /// Trims ASCII whitespace (U+0009..U+000D, U+0020) from the end
        /// of the buffer, returning the slice of the buffer which remains.
        /// </summary>
        /// <param name="span">The buffer to trim.</param>
        /// <returns>A slice of <paramref name="span"/> where whitespace has been trimmed from
        /// the end.</returns>
        /// <remarks>
        /// This method does not modify the contents of <paramref name="span"/>.
        /// </remarks>
        public static Span<byte> TrimEnd(Span<byte> span)
        {
            int i = span.Length - 1;
            for (; (uint)i < (uint)span.Length; i--)
            {
                uint thisByte = span[i];
                if (thisByte > 0x7F || !char.IsWhiteSpace((char)thisByte))
                {
                    break;
                }
            }

            return span.Slice(0, i + 1);
        }

        /// <summary>
        /// Trims ASCII whitespace (U+0009..U+000D, U+0020) from the beginning
        /// of the buffer, returning the slice of the buffer which remains.
        /// </summary>
        /// <param name="span">The buffer to trim.</param>
        /// <returns>A slice of <paramref name="span"/> where whitespace has been trimmed from
        /// the beginning.</returns>
        public static ReadOnlySpan<byte> TrimStart(ReadOnlySpan<byte> span)
        {
            int i = 0;
            for (; i < span.Length; i++)
            {
                uint thisByte = span[i];
                if (thisByte > 0x7F || !char.IsWhiteSpace((char)thisByte))
                {
                    break;
                }
            }

            return span.Slice(i);
        }

        /// <summary>
        /// Trims ASCII whitespace (U+0009..U+000D, U+0020) from the beginning
        /// of the buffer, returning the slice of the buffer which remains.
        /// </summary>
        /// <param name="span">The buffer to trim.</param>
        /// <returns>A slice of <paramref name="span"/> where whitespace has been trimmed from
        /// the beginning.</returns>
        /// <remarks>
        /// This method does not modify the contents of <paramref name="span"/>.
        /// </remarks>
        public static Span<byte> TrimStart(Span<byte> span)
        {
            int i = 0;
            for (; i < span.Length; i++)
            {
                uint thisByte = span[i];
                if (thisByte > 0x7F || !char.IsWhiteSpace((char)thisByte))
                {
                    break;
                }
            }

            return span.Slice(i);
        }
    }
}
