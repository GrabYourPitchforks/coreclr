// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Unicode;

namespace System.Text
{
    public readonly struct Utf8Segment : IComparable<Utf8Segment>, IEquatable<Utf8Segment>
    {
        // Data may be torn - must be checked on each access
        private readonly ReadOnlyMemory<byte> _rawData;

        /// <summary>
        /// Ctor for internal use only. Caller _must_ validate both invariants hold:
        /// (a) the buffer represents well-formed UTF-8 data, and
        /// (b) the buffer is immutable.
        /// </summary>
        private Utf8Segment(ReadOnlyMemory<byte> rawData)
        {
            // In debug builds, we want to ensure that the callers really did validate
            // the buffer for well-formedness. The entire line below is removed when
            // compiling release builds.

            Debug.Assert(Utf8Utility.GetIndexOfFirstInvalidUtf8Sequence(rawData.Span, out _) == -1);

            _rawData = rawData;
        }

        public ReadOnlyMemory<byte> Bytes
        {
            get
            {
                // Make a copy of the underlying struct to avoid race conditions
                // where we end up performing tear-checking on an instance other
                // than the one we return to our caller.

                ReadOnlyMemory<byte> structCopy = _rawData;
                ThrowIfTorn(structCopy.Span);
                return structCopy;
            }
        }

        public Utf8Span Span
        {
            get
            {
                ReadOnlySpan<byte> byteSpan = _rawData.Span;
                ThrowIfTorn(byteSpan);
                return Utf8Span.UnsafeCreateWithoutValidation(byteSpan);
            }
        }

        public static bool operator ==(Utf8Segment left, Utf8Segment right) => Equals(left, right);
        public static bool operator !=(Utf8Segment left, Utf8Segment right) => !Equals(left, right);

        public int CompareTo(Utf8Segment other)
        {
            // TODO_UTF8STRING: This is ordinal, but String.CompareTo uses CurrentCulture.
            // Is this acceptable? Should we perhaps just remove the interface?

            return Utf8StringComparer.Ordinal.Compare(this, other);
        }

        public override bool Equals(object? obj)
        {
            return (obj is Utf8Segment other) && Equals(other);
        }

        public bool Equals(Utf8Segment other) => Equals(this, other);

        public bool Equals(Utf8Segment other, StringComparison comparison) => Equals(this, other, comparison);

        public static bool Equals(Utf8Segment left, Utf8Segment right) => Utf8StringComparer.Ordinal.Equals(left, right);

        public static bool Equals(Utf8Segment left, Utf8Segment right, StringComparison comparison)
        {
            // TODO_UTF8STRING: This perf can be improved, including removing
            // the virtual dispatch by putting the switch directly in this method.

            return Utf8StringComparer.FromComparison(comparison).Equals(left, right);
        }

        public override int GetHashCode()
        {
            return Span.GetHashCode();
        }

        public int GetHashCode(StringComparison comparison)
        {
            // TODO_UTF8STRING: This perf can be improved, including removing
            // the virtual dispatch by putting the switch directly in this method.

            return Utf8StringComparer.FromComparison(comparison).GetHashCode(this);
        }

        public override string ToString()
        {
            return Span.ToString();
        }

        private static void ThrowIfTorn(ReadOnlySpan<byte> utf8CandidateData)
        {
            // Empty spans are by definition well-formed UTF-8.
            // Let them go through.

            if (utf8CandidateData.IsEmpty)
            {
                return;
            }

            // We assume the incoming slice is a part of (or the entirety of)
            // a larger buffer which contains only well-formed UTF-8 data. All
            // we want to do is check the boundaries of the incoming slice to
            // ensure that it didn't split a multi-byte UTF-8 subsequence within
            // the larger buffer.
            //
            // First, see if the first byte of the slice is a UTF-8 continuation
            // byte. If so, then the lead UTF-8 byte was somewhere before the
            // beginning of the buffer, and this indicates a torn buffer.

            if (Utf8Utility.IsUtf8ContinuationByte(utf8CandidateData[0]))
            {
                goto Torn;
            }

            // The common case is that the final byte of the buffer is an ASCII byte.
            // If this is true, we know the end of the slice was not torn, so we
            // can return immediately without any further checks.

            // TODO_UTF8STRING: Consider using unsafe APIs to elide the bounds check below?
            if ((sbyte)utf8CandidateData[utf8CandidateData.Length - 1] >= 0)
            {
                return;
            }

            // If the last byte is a UTF-8 lead byte [ C0 .. FF ] (we don't care about
            // invalid bytes), then the end of the slice was torn since we expect a
            // continuation byte to follow it.

            if (utf8CandidateData[utf8CandidateData.Length - 1] >= 0xC0)
            {
                goto Torn;
            }

            // If the penultimate byte is a UTF-8 3-byte or 4-byte lead byte [ E0 .. FF ]
            // (we don't care about invalid bytes), then the end of the slice was torn
            // since we expect multiple continuation bytes to follow it.

            if (utf8CandidateData[utf8CandidateData.Length - 2] >= 0xE0)
            {
                goto Torn;
            }

            // If the 3rd-to-final byte is a UTF-8 4-byte lead byte [ F0 .. FF ]
            // (we don't care about invalid bytes), then the end of the slide was torn
            // since we expect multiple continuation bytes to follow it.

            if (utf8CandidateData[utf8CandidateData.Length - 3] >= 0xF0)
            {
                goto Torn;
            }

            // Otherwise, we're good!

            return;

        Torn:
            // TODO_UTF8STRING: Use a better error message below.
            throw new InvalidOperationException("Struct torn.");
        }

        /// <summary>
        /// Wraps a <see cref="Utf8Segment"/> instance around the provided <paramref name="buffer"/>,
        /// skipping validation of the input data.
        /// </summary>
        /// <remarks>
        /// Callers must uphold the following two invariants:
        ///
        /// (a) <paramref name="buffer"/> consists only of well-formed UTF-8 data and does
        ///     not contain invalid or incomplete UTF-8 subsequences; and
        /// (b) the contents of <paramref name="buffer"/> will not change for the duration
        ///     of the returned <see cref="Utf8Segment"/>'s existence.
        ///
        /// If these invariants are not maintained, the runtime may exhibit undefined behavior.
        /// </remarks>
        [UnsafeMember]
        internal static Utf8Segment UnsafeCreateWithoutValidation(ReadOnlyMemory<byte> buffer)
        {
            return new Utf8Segment(buffer);
        }
    }
}
