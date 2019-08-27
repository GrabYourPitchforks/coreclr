// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Unicode;

namespace System
{
    public sealed partial class Utf8String
    {
        /*
         * CONSTRUCTORS
         *
         * Defining a new constructor for string-like types (like Utf8String) requires changes both
         * to the managed code below and to the native VM code. See the comment at the top of
         * src/vm/ecall.cpp for instructions on how to add new overloads.
         *
         * The default behavior of each ctor is to validate the input, replacing invalid sequences with the
         * Unicode replacement character U+FFFD. The resulting Utf8String instance will be well-formed but
         * might not have full fidelity with the input data. This behavior can be controlled by calling
         * any of the Create instances and specifying a different action.
         */

        /// <summary>
        /// Creates a <see cref="Utf8String"/> instance from existing UTF-8 data.
        /// </summary>
        /// <remarks>
        /// The UTF-8 data in <paramref name="value"/> is validated for well-formedness upon construction.
        /// Invalid code unit sequences are replaced with U+FFFD in the resulting <see cref="Utf8String"/>.
        /// </remarks>
        [MethodImpl(MethodImplOptions.InternalCall)]
        public extern Utf8String(ReadOnlySpan<byte> value);

#if !CORECLR
        static
#endif
        private Utf8String Ctor(ReadOnlySpan<byte> value)
        {
            if (value.IsEmpty)
            {
                return Empty;
            }

            Utf8String newString = FastAllocate(value.Length);
            Buffer.Memmove(ref newString.DangerousGetMutableReference(), ref MemoryMarshal.GetReference(value), (uint)value.Length);
            return Utf8Utility.ValidateAndFixupUtf8String(newString);
        }

        /// <summary>
        /// Creates a <see cref="Utf8String"/> instance from existing UTF-8 data.
        /// </summary>
        /// <remarks>
        /// The UTF-8 data in <paramref name="value"/> is validated for well-formedness upon construction.
        /// Invalid code unit sequences are replaced with U+FFFD in the resulting <see cref="Utf8String"/>.
        /// </remarks>
        [MethodImpl(MethodImplOptions.InternalCall)]
        public extern Utf8String(byte[]? value, int startIndex, int length);

#if !CORECLR
        static
#endif
        private Utf8String Ctor(byte[]? value, int startIndex, int length) => Ctor(new ReadOnlySpan<byte>(value, startIndex, length));

        /// <summary>
        /// Creates a <see cref="Utf8String"/> instance from existing null-terminated UTF-8 data.
        /// </summary>
        /// <remarks>
        /// The UTF-8 data in <paramref name="value"/> is validated for well-formedness upon construction.
        /// Invalid code unit sequences are replaced with U+FFFD in the resulting <see cref="Utf8String"/>.
        /// </remarks>
        [MethodImpl(MethodImplOptions.InternalCall)]
        [CLSCompliant(false)]
        public extern unsafe Utf8String(byte* value);

#if !CORECLR
        static
#endif
        private unsafe Utf8String Ctor(byte* value)
        {
            if (value == null)
            {
                return Empty;
            }

            return Ctor(new ReadOnlySpan<byte>(value, string.strlen(value)));
        }

        /// <summary>
        /// Creates a <see cref="Utf8String"/> instance from existing UTF-16 data.
        /// </summary>
        /// <remarks>
        /// The UTF-16 data in <paramref name="value"/> is validated for well-formedness upon construction.
        /// Invalid code unit sequences are replaced with U+FFFD in the resulting <see cref="Utf8String"/>.
        /// </remarks>
        [MethodImpl(MethodImplOptions.InternalCall)]
        public extern Utf8String(ReadOnlySpan<char> value);

#if !CORECLR
        static
#endif
        private Utf8String Ctor(ReadOnlySpan<char> value)
        {
            if (value.IsEmpty)
            {
                return Empty;
            }

            Utf8String newString = FastAllocate(Encoding.UTF8.GetByteCount(value));
            OperationStatus status = Utf8.FromUtf16(value, newString.DangerousGetMutableSpan(), out int _, out int bytesWritten);

            if (status != OperationStatus.Done || bytesWritten != newString.Length)
            {
                // TODO_UTF8STRING: Better exception message here.
                throw new InvalidOperationException("Transcoding error - was the input buffer unexpectedly mutated?");
            }

            return newString;
        }

        /// <summary>
        /// Creates a <see cref="Utf8String"/> instance from existing UTF-16 data.
        /// </summary>
        /// <remarks>
        /// The UTF-16 data in <paramref name="value"/> is validated for well-formedness upon construction.
        /// Invalid code unit sequences are replaced with U+FFFD in the resulting <see cref="Utf8String"/>.
        /// </remarks>
        [MethodImpl(MethodImplOptions.InternalCall)]
        public extern Utf8String(char[]? value, int startIndex, int length);

#if !CORECLR
        static
#endif
        private Utf8String Ctor(char[]? value, int startIndex, int length) => Ctor(new ReadOnlySpan<char>(value, startIndex, length));

        /// <summary>
        /// Creates a <see cref="Utf8String"/> instance from existing null-terminated UTF-16 data.
        /// </summary>
        /// <remarks>
        /// The UTF-16 data in <paramref name="value"/> is validated for well-formedness upon construction.
        /// Invalid code unit sequences are replaced with U+FFFD in the resulting <see cref="Utf8String"/>.
        /// </remarks>
        [MethodImpl(MethodImplOptions.InternalCall)]
        [CLSCompliant(false)]
        public extern unsafe Utf8String(char* value);

#if !CORECLR
        static
#endif
        private unsafe Utf8String Ctor(char* value)
        {
            if (value == null)
            {
                return Empty;
            }

            return Ctor(new ReadOnlySpan<char>(value, string.wcslen(value)));
        }

        /// <summary>
        /// Creates a <see cref="Utf8String"/> instance from existing UTF-16 data.
        /// </summary>
        /// <remarks>
        /// The UTF-16 data in <paramref name="value"/> is validated for well-formedness upon construction.
        /// Invalid code unit sequences are replaced with U+FFFD in the resulting <see cref="Utf8String"/>.
        /// </remarks>
        [MethodImpl(MethodImplOptions.InternalCall)]
        public extern Utf8String(string? value);

#if !CORECLR
        static
#endif
        private Utf8String Ctor(string? value) => Ctor(value.AsSpan());

        /*
         * STATIC FACTORIES
         */

        /// <summary>
        /// Creates a new <see cref="Utf8String"/> instance, allowing the provided delegate to populate the
        /// instance data of the returned object.
        /// </summary>
        /// <typeparam name="TState">Type of the state object provided to <paramref name="action"/>.</typeparam>
        /// <param name="length">The length, in bytes, of the <see cref="Utf8String"/> instance to create.</param>
        /// <param name="state">The state object to provide to <paramref name="action"/>.</param>
        /// <param name="action">The callback which will be invoked to populate the returned <see cref="Utf8String"/>.</param>
        /// <param name="throwOnInvalidData">
        /// <see langword="true"/> if this method should throw an exception if <paramref name="action"/> provides ill-formed
        /// UTF-8 subsequences; <see langword="false"/> if ill-formed UTF-8 subsequences should be substituted with <see cref="Rune.ReplacementChar"/>
        /// in the returned <see cref="Utf8String"/>.
        /// </param>
        /// <remarks>
        /// The runtime will perform UTF-8 validation over the contents provided by the <paramref name="action"/> delegate.
        /// If an invalid UTF-8 subsequence is detected, the <paramref name="throwOnInvalidData"/> parameter dictates the
        /// corrective action that will be taken.
        /// </remarks>
        public static Utf8String Create<TState>(int length, TState state, SpanAction<byte, TState> action, bool throwOnInvalidData = false)
        {
            if (length < 0)
            {
                ThrowHelper.ThrowLengthArgumentOutOfRange_ArgumentOutOfRange_NeedNonNegNum();
            }

            if (action is null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.action);
            }

            // Create and populate the Utf8String instance.

            Utf8String newString = FastAllocate(length);
            action(newString.DangerousGetMutableSpan(), state);

            // Now perform validation.
            // TODO_UTF8STRING: Consider calling a different overload of the validation routine
            // if we want to skip fixup and just go straight to the throwing step.

            Utf8String objectToReturn = Utf8Utility.ValidateAndFixupUtf8String(newString);
            if (throwOnInvalidData && !ReferenceEquals(newString, objectToReturn))
            {
                // TODO_UTF8STRING: Localize this exception.
                throw new InvalidOperationException("Utf8String.Create delegate provided invalid UTF-8 data.");
            }

            return objectToReturn;
        }

        /// <summary>
        /// Creates a new <see cref="Utf8String"/> instance populated with a copy of the provided contents.
        /// Please see remarks for important safety information about this method.
        /// </summary>
        /// <param name="utf8Contents">The contents to copy to the new <see cref="Utf8String"/>.</param>
        /// <remarks>
        /// This factory method can be used as an optimization to skip the validation step that the
        /// <see cref="Utf8String"/> constructors normally perform. The contract of this method requires that
        /// <paramref name="utf8Contents"/> contain only well-formed UTF-8 data, as <see cref="Utf8String"/>
        /// contractually guarantees that it contains only well-formed UTF-8 data, and runtime instability
        /// could occur if a caller violates this guarantee.
        /// </remarks>
        public static Utf8String UnsafeCreateWithoutValidation(ReadOnlySpan<byte> utf8Contents)
        {
            // Create and populate the Utf8String instance.

            Utf8String newString = FastAllocate(utf8Contents.Length);
            utf8Contents.CopyTo(newString.DangerousGetMutableSpan());

            // The line below is removed entirely in release builds.

            Debug.Assert(Utf8Utility.GetIndexOfFirstInvalidUtf8Sequence(newString.AsBytes(), out bool _) < 0, "Buffer contained ill-formed UTF-8 data.");

            return newString;
        }

        /// <summary>
        /// Creates a new <see cref="Utf8String"/> instance, allowing the provided delegate to populate the
        /// instance data of the returned object. Please see remarks for important safety information about
        /// this method.
        /// </summary>
        /// <typeparam name="TState">Type of the state object provided to <paramref name="action"/>.</typeparam>
        /// <param name="length">The length, in bytes, of the <see cref="Utf8String"/> instance to create.</param>
        /// <param name="state">The state object to provide to <paramref name="action"/>.</param>
        /// <param name="action">The callback which will be invoked to populate the returned <see cref="Utf8String"/>.</param>
        /// <remarks>
        /// This factory method can be used as an optimization to skip the validation step that
        /// <see cref="Create{TState}(int, TState, SpanAction{byte, TState}, bool)"/> normally performs. The contract
        /// of this method requires that <paramref name="action"/> populate the buffer with well-formed UTF-8
        /// data, as <see cref="Utf8String"/> contractually guarantees that it contains only well-formed UTF-8 data,
        /// and runtime instability could occur if a caller violates this guarantee.
        /// </remarks>
        public static Utf8String UnsafeCreateWithoutValidation<TState>(int length, TState state, SpanAction<byte, TState> action)
        {
            if (length < 0)
            {
                ThrowHelper.ThrowLengthArgumentOutOfRange_ArgumentOutOfRange_NeedNonNegNum();
            }

            if (action is null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.action);
            }

            // Create and populate the Utf8String instance.

            Utf8String newString = FastAllocate(length);
            action(newString.DangerousGetMutableSpan(), state);

            // The line below is removed entirely in release builds.

            Debug.Assert(Utf8Utility.GetIndexOfFirstInvalidUtf8Sequence(newString.AsBytes(), out bool _) < 0, "Callback populated the buffer with ill-formed UTF-8 data.");

            return newString;
        }

        /*
         * HELPER METHODS
         */

        /// <summary>
        /// Creates a <see cref="Utf8String"/> instance from existing data, bypassing validation.
        /// Also allows the caller to set flags dictating various attributes of the data.
        /// </summary>
        internal static Utf8String DangerousCreateWithoutValidation(ReadOnlySpan<byte> utf8Data, bool assumeWellFormed = false, bool assumeAscii = false)
        {
            if (utf8Data.IsEmpty)
            {
                return Empty;
            }

            Utf8String newString = FastAllocate(utf8Data.Length);
            utf8Data.CopyTo(new Span<byte>(ref newString.DangerousGetMutableReference(), newString.Length));
            return newString;
        }

        /// <summary>
        /// Creates a new zero-initialized instance of the specified length. Actual storage allocated is "length + 1" bytes
        /// because instances are null-terminated.
        /// </summary>
        /// <remarks>
        /// The implementation of this method checks its input argument for overflow.
        /// </remarks>
        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern Utf8String FastAllocate(int length);
    }
}
