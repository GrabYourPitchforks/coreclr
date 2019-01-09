// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Buffers;

namespace System.Text
{
    /// <summary>
    /// Provides facilities for transcoding between different Unicode representations.
    /// </summary>
    public static class Transcode
    {
        /*
         * OperationStatus-based APIs for transcoding of chunked data.
         * This method is similar to Encoding.UTF8.GetBytes / GetChars but has a
         * different calling convention, different error handling mechanisms, and
         * different performance characteristics.
         *
         * If 'replaceInvalidSequences' is true, the method will replace any ill-formed
         * subsequence in the source with U+FFFD when transcoding to the destination,
         * then it will continue processing the remainder of the buffers. Otherwise
         * the method will return OperationStatus.InvalidData.
         *
         * If the method does return an error code, the out parameters will represent
         * how much of the data was successfully transcoded, and the location of the
         * ill-formed subsequence can be deduced from these values.
         *
         * If 'replaceInvalidSequences' is true, the method is guaranteed never to return
         * OperationStatus.InvalidData. If 'isFinalChunk' is true, the method is
         * guaranteed never to return OperationStatus.NeedMoreData.
         */

        public static OperationStatus FromUtf8Bytes(ReadOnlySpan<byte> source, Span<char> destination, bool replaceInvalidSequences, bool isFinalChunk, out int numCharsRead, out int numBytesWritten)
        {

        }

        public static OperationStatus ToUtf8Bytes(ReadOnlySpan<char> source, Span<byte> destination, bool replaceInvalidSequences, bool isFinalChunk, out int numCharsRead, out int numBytesWritten)
        {

        }
    }
}
