// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Unicode;

namespace System
{
    public sealed partial class Utf8String
    {
        /// <summary>
        /// Substrings this <see cref="Utf8String"/> without bounds checking.
        /// </summary>
        private Utf8String InternalSubstring(int startIndex, int length)
        {
            Debug.Assert(startIndex >= 0, "StartIndex cannot be negative.");
            Debug.Assert(startIndex <= this.Length, "StartIndex cannot point beyond the end of the string (except to the null terminator).");
            Debug.Assert(length >= 0, "Length cannot be negative.");
            Debug.Assert(startIndex + length <= this.Length, "StartIndex and Length cannot point beyond the end of the string.");

            Debug.Assert(length != 0 && length != this.Length, "Caller should handle Length boundary conditions.");

            // Since Utf8String instances must contain well-formed UTF-8 data, we cannot allow a substring such that
            // either boundary of the new substring splits a multi-byte UTF-8 subsequence. Fortunately this is a very
            // easy check: since we assume the original buffer consisted entirely of well-formed UTF-8 data, all we
            // need to do is check that neither the substring we're about to create nor the substring that would
            // follow immediately thereafter begins with a UTF-8 continuation byte. Should this occur, it means that
            // the UTF-8 lead byte is in a prior substring, which would indicate a multi-byte sequence has been split.
            // It's ok for us to dereference the element immediately after the end of the Utf8String instance since
            // we know it's a null terminator.
            //
            // TODO_UTF8STRING: Can skip the second check if only the start index (no length) is provided. Would
            // need to duplicate this method and have those callers invoke the duplicate method instead of this one.

            if (Utf8Utility.IsUtf8ContinuationByte(DangerousGetMutableReference(startIndex))
                || Utf8Utility.IsUtf8ContinuationByte(DangerousGetMutableReference(startIndex + length)))
            {
                ThrowImproperStringSplit();
            }

            Utf8String newString = FastAllocateSkipZeroInit(length);
            Buffer.Memmove(ref newString.DangerousGetMutableReference(), ref this.DangerousGetMutableReference(startIndex), (uint)length);
            return newString;
        }

        [StackTraceHidden]
        public void ThrowImproperStringSplit()
        {
            // TODO_UTF8STRING: Make this an actual resource string.

            throw new InvalidOperationException("Cannot create the desired substring because it would split a multi-byte UTF-8 subsequence.");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Utf8String Substring(Index startIndex)
        {
            int actualIndex = startIndex.GetOffset(Length);
            return Substring(actualIndex);
        }

        public Utf8String Substring(int startIndex)
        {
            if ((uint)startIndex > (uint)this.Length)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.startIndex);
            }

            // Optimizations: since instances are immutable, we can return 'this' or the known
            // Empty instance if the caller passed us a startIndex at the string boundary.

            if (startIndex == 0)
            {
                return this;
            }

            if (startIndex == Length)
            {
                return Empty;
            }

            return InternalSubstring(startIndex, Length - startIndex);
        }

        public Utf8String Substring(int startIndex, int length)
        {
            ValidateStartIndexAndLength(startIndex, length);

            // Optimizations: since instances are immutable, we can return 'this' or the known
            // Empty instance if the caller passed us a startIndex at the string boundary.

            if (length == 0)
            {
                return Empty;
            }

            if (length == this.Length)
            {
                return this;
            }

            return InternalSubstring(startIndex, length);
        }

        // Slice intended to be used by the compiler only to provide indexer with range parameter functionality.
        // Developers should be using Substring method instead.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [ComponentModel.EditorBrowsable(ComponentModel.EditorBrowsableState.Never)]
        public Utf8String Slice(int startIndex, int length) => Substring(startIndex, length);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ValidateStartIndexAndLength(int startIndex, int length)
        {
#if BIT64
            // See comment in Span<T>.Slice for how this works.
            if ((ulong)(uint)startIndex + (ulong)(uint)length > (ulong)(uint)this.Length)
                ValidateStartIndexAndLength_Throw(startIndex, length);
#else
            if ((uint)startIndex > (uint)this.Length || (uint)length > (uint)(this.Length - startIndex))
                ValidateStartIndexAndLength_Throw(startIndex, length);
#endif
        }

        [StackTraceHidden]
        private void ValidateStartIndexAndLength_Throw(int startIndex, int length)
        {
            throw new ArgumentOutOfRangeException(paramName: ((uint)startIndex > (uint)this.Length) ? nameof(startIndex) : nameof(length));
        }
    }
}
