// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Unicode;

#pragma warning disable 0809  //warning CS0809: Obsolete member 'Utf8Span.Equals(object)' overrides non-obsolete member 'object.Equals(object)'

namespace System.Text
{
    public readonly ref partial struct Utf8Span
    {
        /// <summary>
        /// Creates a <see cref="Utf8Span"/> from an existing <see cref="Utf8String"/> instance.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Utf8Span(Utf8String? value)
        {
            if (!(value is null))
            {
                Bytes = new ReadOnlySpan<byte>(ref value.DangerousGetMutableReference(), value.Length);
            }
            else
            {
                Bytes = default;
            }
        }

        /// <summary>
        /// Ctor for internal use only. Caller _must_ validate both invariants hold:
        /// (a) the buffer represents well-formed UTF-8 data, and
        /// (b) the buffer is immutable.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Utf8Span(ReadOnlySpan<byte> rawData)
        {
            // In debug builds, we want to ensure that the callers really did validate
            // the buffer for well-formedness. The entire line below is removed when
            // compiling release builds.

            Debug.Assert(Utf8Utility.GetIndexOfFirstInvalidUtf8Sequence(rawData, out _) == -1);

            Bytes = rawData;
        }

        public ReadOnlySpan<byte> Bytes { get; }

        public bool IsEmpty => Bytes.IsEmpty;

        public bool IsEmptyOrWhiteSpace()
        {
            // TODO_UTF8STRING: Use a non-allocating implementation.

            return string.IsNullOrWhiteSpace(ToString());
        }

        /// <summary>
        /// This method is not supported as spans cannot be boxed. To compare two spans, use operator==.
        /// <exception cref="System.NotSupportedException">
        /// Always thrown by this method.
        /// </exception>
        /// </summary>
        [Obsolete("Equals(object) on Utf8Span will always throw an exception. Use Equals(Utf8Span) or == instead.")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override bool Equals(object? obj)
        {
            // TODO_UTF8STRING: Improve error message below?

            throw new NotSupportedException(SR.NotSupported_CannotCallEqualsOnSpan);
        }

        public bool Equals(Utf8Span other) => Equals(this, other);

        public bool Equals(Utf8Span other, StringComparison comparison) => Equals(this, other, comparison);

        public static bool Equals(Utf8Span left, Utf8Span right) => left.Bytes.SequenceEqual(right.Bytes);

        public static bool Equals(Utf8Span left, Utf8Span right, StringComparison comparison)
        {
            // TODO_UTF8STRING: This perf can be improved, including removing
            // the virtual dispatch by putting the switch directly in this method.

            return Utf8StringComparer.FromComparison(comparison).Equals(left, right);
        }

        public override int GetHashCode()
        {
            // TODO_UTF8STRING: Consider whether this should use a different seed than String.GetHashCode.
            // This method should only be called to calculate the hash code over spans that represent
            // UTF-8 textual data, not over arbitrary binary sequences.

            ulong seed = Marvin.DefaultSeed;
            return Marvin.ComputeHash32(ref MemoryMarshal.GetReference(Bytes), (uint)Bytes.Length /* in bytes */, (uint)seed, (uint)(seed >> 32));
        }

        public int GetHashCode(StringComparison comparison)
        {
            // TODO_UTF8STRING: This perf can be improved, including removing
            // the virtual dispatch by putting the switch directly in this method.

            return Utf8StringComparer.FromComparison(comparison).GetHashCode(this);
        }

        internal int GetNonRandomizedHashCode()
        {
            // TODO_UTF8STRING: Avoid allocation in this code path.

            return ToUtf8String().GetNonRandomizedHashCode();
        }

        /// <summary>
        /// Returns <see langword="true"/> if this UTF-8 text consists of all-ASCII data,
        /// <see langword="false"/> if there is any non-ASCII data within this UTF-8 text.
        /// </summary>
        /// <remarks>
        /// ASCII text is defined as text consisting only of scalar values in the range [ U+0000..U+007F ].
        /// The runtime of this method is O(n).
        /// </remarks>
        public bool IsAscii()
        {
            // TODO_UTF8STRING: Use an API that takes 'ref byte' instead of a 'byte*' as a parameter.

            unsafe
            {
                fixed (byte* pData = &MemoryMarshal.GetReference(Bytes))
                {
                    return (ASCIIUtility.GetIndexOfFirstNonAsciiByte(pData, (uint)Bytes.Length) >= 0);
                }
            }
        }

        public bool IsNormalized(NormalizationForm normalizationForm = NormalizationForm.FormC)
        {
            // TODO_UTF8STRING: Avoid allocations in this code path.

            return ToString().IsNormalized(normalizationForm);
        }

        /// <summary>
        /// Gets an immutable reference that can be used in a <see langword="fixed"/> statement. Unlike
        /// <see cref="Utf8String"/>, the resulting reference is not guaranteed to be null-terminated.
        /// </summary>
        /// <remarks>
        /// If this <see cref="Utf8Span"/> instance is empty, returns <see langword="null"/>. Dereferencing
        /// such a reference will result in a <see cref="NullReferenceException"/> being generated.
        /// </remarks>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public ref readonly byte GetPinnableReference()
        {
            // This returns null if the underlying span is empty. The reason for this is that unlike
            // Utf8String, these buffers are not guaranteed to be null-terminated, so it's not always
            // safe or meaningful to dereference the element just past the end of the buffer.

            return ref Bytes.GetPinnableReference();
        }

        public override string ToString()
        {
            // TODO_UTF8STRING: Since we know the underlying data is immutable, well-formed UTF-8,
            // we can perform transcoding using an optimized code path that skips all safety checks.

            return Encoding.UTF8.GetString(Bytes);
        }

        public Utf8String ToUtf8String()
        {
            // TODO_UTF8STRING: Since we know the underlying data is immutable, well-formed UTF-8,
            // we can perform transcoding using an optimized code path that skips all safety checks.

            return new Utf8String(Bytes);
        }

        /// <summary>
        /// Wraps a <see cref="Utf8Span"/> instance around the provided <paramref name="buffer"/>,
        /// skipping validation of the input data.
        /// </summary>
        /// <remarks>
        /// Callers must uphold the following two invariants:
        ///
        /// (a) <paramref name="buffer"/> consists only of well-formed UTF-8 data and does
        ///     not contain invalid or incomplete UTF-8 subsequences; and
        /// (b) the contents of <paramref name="buffer"/> will not change for the duration
        ///     of the returned <see cref="Utf8Span"/>'s existence.
        ///
        /// If these invariants are not maintained, the runtime may exhibit undefined behavior.
        /// </remarks>
        [UnsafeMember]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Utf8Span UnsafeCreateWithoutValidation(ReadOnlySpan<byte> buffer)
        {
            return new Utf8Span(buffer);
        }
    }
}
