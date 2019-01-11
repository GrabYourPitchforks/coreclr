// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Buffers;

namespace System.Text.Unicode
{
    /// <summary>
    /// Provides facilities for inspecting, transcoding, and manipulating UTF-8 data.
    /// </summary>
    public static class Utf8
    {
        /// <summary>
        /// Determines whether <paramref name="source"/> represents a well-formed UTF-8 sequence.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> if the sequence is well-formed UTF-8; <see langword="false"/> otherwise.
        /// </returns>
        /// <remarks>
        /// Returns <see langword="true"/> if given an empty input.
        /// </remarks>
        public static bool IsWellFormed(ReadOnlySpan<byte> source)
        {
            return Utf8Utility.IsWellFormedSequence(source);
        }

        /// <summary>
        /// Returns the index of the first byte in <paramref name="source"/> that represents the start of an
        /// invalid UTF-8 subsequence, along with the UTF-16 code unit count and <see cref="Rune"/> count of
        /// the sequence.
        /// </summary>
        /// <returns>
        /// A non-negative integer representing the index of the first byte in <paramref name="source"/> that
        /// begins an invalid UTF-8 subsequence, or -1 if <paramref name="source"/> is well-formed.
        /// </returns>
        /// <remarks>
        /// <paramref name="utf16CharCount"/> and <paramref name="runeCount"/> represent the UTF-16 code unit count
        /// and the <see cref="Rune"/> count from the beginning of the <paramref name="source"/> buffer up until
        /// the reported first invalid subsequence. If <paramref name="source"/> is well-formed, <paramref name="utf16CharCount"/>
        /// and <paramref name="runeCount"/> represent the respective counts for the entire buffer.
        /// </remarks>
        public static int GetIndexOfFirstInvalidByte(ReadOnlySpan<byte> source, out int utf16CharCount, out int runeCount)
        {
            return Utf8Utility.GetIndexOfFirstInvalidSubsequence(source, out utf16CharCount, out runeCount);
        }
    }
}
