// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
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

        private Utf8String InternalSubstringWithoutCorrectnessChecks(int startIndex, int length)
        {
            Debug.Assert(startIndex >= 0, "StartIndex cannot be negative.");
            Debug.Assert(startIndex <= this.Length, "StartIndex cannot point beyond the end of the string (except to the null terminator).");
            Debug.Assert(length >= 0, "Length cannot be negative.");
            Debug.Assert(startIndex + length <= this.Length, "StartIndex and Length cannot point beyond the end of the string.");

            // In debug mode, perform the checks anyway. It's ok if we read just past the end of the
            // Utf8String instance, since we'll just be reading the null terminator (which is safe).

            Debug.Assert(!Utf8Utility.IsUtf8ContinuationByte(DangerousGetMutableReference(startIndex)), "Somebody is trying to split this Utf8String improperly.");
            Debug.Assert(!Utf8Utility.IsUtf8ContinuationByte(DangerousGetMutableReference(startIndex + length)), "Somebody is trying to split this Utf8String improperly.");

            if (length == 0)
            {
                return Empty;
            }
            else if (length == this.Length)
            {
                return this;
            }
            else
            {
                Utf8String newString = FastAllocateSkipZeroInit(length);
                Buffer.Memmove(ref newString.DangerousGetMutableReference(), ref this.DangerousGetMutableReference(startIndex), (uint)length);
                return newString;
            }
        }

        [StackTraceHidden]
        internal static void ThrowImproperStringSplit()
        {
            throw new InvalidOperationException(
                message: SR.Utf8String_CannotSplitMultibyteSubsequence);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Utf8String Substring(Index startIndex)
        {
            int actualIndex = startIndex.GetOffset(Length);
            return Substring(actualIndex);
        }

        internal Utf8String Substring(int startIndex)
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

        internal Utf8String Substring(int startIndex, int length)
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

        /// <summary>
        /// Locates <paramref name="separator"/> within this <see cref="Utf8String"/> instance, creating <see cref="Utf8String"/>
        /// instances which represent the data on either side of the separator. If <paramref name="separator"/> is not found
        /// within this <see cref="Utf8String"/> instance, returns the tuple "(this, null)".
        /// </summary>
        /// <remarks>
        /// An ordinal search is performed.
        /// </remarks>
        public (Utf8String Before, Utf8String? After) SplitOn(char separator)
        {
            if (!TryFind(separator, out Range range))
            {
                return (this, null); // not found
            }

            (int startIndex, int length) = range.GetOffsetAndLength(this.Length);
            return (InternalSubstringWithoutCorrectnessChecks(0, startIndex), InternalSubstringWithoutCorrectnessChecks(startIndex + length, this.Length - (startIndex + length)));
        }

        /// <summary>
        /// Locates <paramref name="separator"/> within this <see cref="Utf8String"/> instance, creating <see cref="Utf8String"/>
        /// instances which represent the data on either side of the separator. If <paramref name="separator"/> is not found
        /// within this <see cref="Utf8String"/> instance, returns the tuple "(this, null)".
        /// </summary>
        /// <remarks>
        /// The search is performed using the specified <paramref name="comparisonType"/>.
        /// </remarks>
        public (Utf8String Before, Utf8String? After) SplitOn(char separator, StringComparison comparisonType)
        {
            if (!TryFind(separator, comparisonType, out Range range))
            {
                return (this, null); // not found
            }

            (int startIndex, int length) = range.GetOffsetAndLength(this.Length);
            return (InternalSubstringWithoutCorrectnessChecks(0, startIndex), InternalSubstringWithoutCorrectnessChecks(startIndex + length, this.Length - (startIndex + length)));
        }

        /// <summary>
        /// Locates <paramref name="separator"/> within this <see cref="Utf8String"/> instance, creating <see cref="Utf8String"/>
        /// instances which represent the data on either side of the separator. If <paramref name="separator"/> is not found
        /// within this <see cref="Utf8String"/> instance, returns the tuple "(this, null)".
        /// </summary>
        /// <remarks>
        /// An ordinal search is performed.
        /// </remarks>
        public (Utf8String Before, Utf8String? After) SplitOn(Rune separator)
        {
            if (!TryFind(separator, out Range range))
            {
                return (this, null); // not found
            }

            (int startIndex, int length) = range.GetOffsetAndLength(this.Length);
            return (InternalSubstringWithoutCorrectnessChecks(0, startIndex), InternalSubstringWithoutCorrectnessChecks(startIndex + length, this.Length - (startIndex + length)));
        }

        /// <summary>
        /// Locates <paramref name="separator"/> within this <see cref="Utf8String"/> instance, creating <see cref="Utf8String"/>
        /// instances which represent the data on either side of the separator. If <paramref name="separator"/> is not found
        /// within this <see cref="Utf8String"/> instance, returns the tuple "(this, null)".
        /// </summary>
        /// <remarks>
        /// The search is performed using the specified <paramref name="comparisonType"/>.
        /// </remarks>
        public (Utf8String Before, Utf8String? After) SplitOn(Rune separator, StringComparison comparisonType)
        {
            if (!TryFind(separator, comparisonType, out Range range))
            {
                return (this, null); // not found
            }

            (int startIndex, int length) = range.GetOffsetAndLength(this.Length);
            return (InternalSubstringWithoutCorrectnessChecks(0, startIndex), InternalSubstringWithoutCorrectnessChecks(startIndex + length, this.Length - (startIndex + length)));
        }

        /// <summary>
        /// Locates <paramref name="separator"/> within this <see cref="Utf8String"/> instance, creating <see cref="Utf8String"/>
        /// instances which represent the data on either side of the separator. If <paramref name="separator"/> is not found
        /// within this <see cref="Utf8String"/> instance, returns the tuple "(this, null)".
        /// </summary>
        /// <remarks>
        /// An ordinal search is performed.
        /// </remarks>
        public (Utf8String Before, Utf8String? After) SplitOn(Utf8String separator)
        {
            if (!TryFind(separator, out Range range))
            {
                return (this, null); // not found
            }

            (int startIndex, int length) = range.GetOffsetAndLength(this.Length);
            return (InternalSubstringWithoutCorrectnessChecks(0, startIndex), InternalSubstringWithoutCorrectnessChecks(startIndex + length, this.Length - (startIndex + length)));
        }

        /// <summary>
        /// Locates <paramref name="separator"/> within this <see cref="Utf8String"/> instance, creating <see cref="Utf8String"/>
        /// instances which represent the data on either side of the separator. If <paramref name="separator"/> is not found
        /// within this <see cref="Utf8String"/> instance, returns the tuple "(this, null)".
        /// </summary>
        /// <remarks>
        /// The search is performed using the specified <paramref name="comparisonType"/>.
        /// </remarks>
        public (Utf8String Before, Utf8String? After) SplitOn(Utf8String separator, StringComparison comparisonType)
        {
            if (!TryFind(separator, comparisonType, out Range range))
            {
                return (this, null); // not found
            }

            (int startIndex, int length) = range.GetOffsetAndLength(this.Length);
            return (InternalSubstringWithoutCorrectnessChecks(0, startIndex), InternalSubstringWithoutCorrectnessChecks(startIndex + length, this.Length - (startIndex + length)));
        }

        /// <summary>
        /// Trims whitespace from the beginning and the end of this <see cref="Utf8String"/>,
        /// returning a new <see cref="Utf8String"/> containing the resulting slice.
        /// </summary>
        public Utf8String Trim() => TrimHelper(TrimType.Both);

        /// <summary>
        /// Trims whitespace from only the end of this <see cref="Utf8String"/>,
        /// returning a new <see cref="Utf8String"/> containing the resulting slice.
        /// </summary>
        public Utf8String TrimEnd() => TrimHelper(TrimType.Tail);

        private Utf8String TrimHelper(TrimType trimType)
        {
            Utf8Span trimmedSpan = this.AsSpan().TrimHelper(trimType);

            // Try to avoid allocating a new Utf8String instance if possible.
            // Otherwise, allocate a new substring wrapped around the resulting slice.

            return (trimmedSpan.Length == this.Length) ? this : trimmedSpan.ToUtf8String();
        }

        /// <summary>
        /// Trims whitespace from only the beginning of this <see cref="Utf8String"/>,
        /// returning a new <see cref="Utf8String"/> containing the resulting slice.
        /// </summary>
        public Utf8String TrimStart() => TrimHelper(TrimType.Head);

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
