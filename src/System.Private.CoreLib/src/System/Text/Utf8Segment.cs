// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Unicode;
using Internal.Runtime.CompilerServices;

#pragma warning disable SA1121 // explicitly using type aliases instead of built-in types
#if BIT64
using nint = System.Int64;
using nuint = System.UInt64;
#else
using nint = System.Int32;
using nuint = System.UInt32;
#endif

namespace System.Text
{
    public readonly struct Utf8Segment : IComparable<Utf8Segment>, IEquatable<Utf8Segment>
    {
        // Data may be torn - must be checked on each access
        private readonly ReadOnlyMemory<byte> _rawData;

        /// <summary>
        /// Creates a <see cref="Utf8Segment"/> from an existing <see cref="Utf8String"/> instance.
        /// </summary>
        public Utf8Segment(Utf8String? value)
        {
            if (!(value is null))
            {
                _rawData = value.AsMemoryBytes();
            }
            else
            {
                _rawData = default;
            }
        }

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
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (_rawData.GetObjectStartLength(out int start, out int length) is Utf8String utf8String)
                {
                    // Special-casing Utf8String as out backing store allows us to perform an
                    // optimization: we can simply check for continuation bytes at the beginning
                    // of the segment or just after the end of the segment. This is the same
                    // check that Utf8String.Substring would have performed. The rest of the bounds
                    // checking logic is from ReadOnlyMemory<T>.get_Span.

                    nuint desiredLength = (uint)length;
                    nuint desiredStartIndex = (uint)start & (uint)ReadOnlyMemory<byte>.RemoveFlagsBitMask;

#if BIT64
                    nuint desiredEndIndex = desiredStartIndex + desiredLength;
                    if (desiredEndIndex > (uint)utf8String.Length
                        || Utf8Utility.IsUtf8ContinuationByte(utf8String.DangerousGetMutableReference(desiredStartIndex))
                        || Utf8Utility.IsUtf8ContinuationByte(utf8String.DangerousGetMutableReference(desiredEndIndex)))
                    {
                        ThrowHelper.ThrowArgumentOutOfRangeException(); // same exception as ROM<T>.Span getter for torn structs
                    }
#else
                    if (desiredStartIndex > (uint)utf8String.Length
                        || desiredLength > (uint)utf8String.Length - desiredStartIndex
                        || Utf8Utility.IsUtf8ContinuationByte(utf8String.DangerousGetMutableReference(desiredStartIndex))
                        || Utf8Utility.IsUtf8ContinuationByte(utf8String.DangerousGetMutableReference(desiredStartIndex + desiredLength)))
                    {
                        ThrowHelper.ThrowArgumentOutOfRangeException(); // same exception as ROM<T>.Span getter for torn structs
                    }
#endif

                    return new ReadOnlyMemory<byte>(utf8String, (int)desiredStartIndex, (int)desiredLength);
                }

                // If the backing store wasn't Utf8String, perform the more complex check.

                return GetMemorySlow();
            }
        }

        public Utf8Span Span
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                // See comments in the Bytes property getter for how this works.

                if (_rawData.GetObjectStartLength(out int start, out int length) is Utf8String utf8String)
                {
                    nuint desiredLength = (uint)length;
                    nuint desiredStartIndex = (uint)start & (uint)ReadOnlyMemory<byte>.RemoveFlagsBitMask;

#if BIT64
                    nuint desiredEndIndex = desiredStartIndex + desiredLength;
                    if (desiredEndIndex > (uint)utf8String.Length
                        || Utf8Utility.IsUtf8ContinuationByte(utf8String.DangerousGetMutableReference(desiredStartIndex))
                        || Utf8Utility.IsUtf8ContinuationByte(utf8String.DangerousGetMutableReference(desiredEndIndex)))
                    {
                        ThrowHelper.ThrowArgumentOutOfRangeException(); // same exception as ROM<T>.Span getter for torn structs
                    }
#else
                    if (desiredStartIndex > (uint)utf8String.Length
                        || desiredLength > (uint)utf8String.Length - desiredStartIndex
                        || Utf8Utility.IsUtf8ContinuationByte(utf8String.DangerousGetMutableReference(desiredStartIndex))
                        || Utf8Utility.IsUtf8ContinuationByte(utf8String.DangerousGetMutableReference(desiredStartIndex + desiredLength)))
                    {
                        ThrowHelper.ThrowArgumentOutOfRangeException(); // same exception as ROM<T>.Span getter for torn structs
                    }
#endif

                    return Utf8Span.UnsafeCreateWithoutValidation(new ReadOnlySpan<byte>(ref utf8String.DangerousGetMutableReference(desiredStartIndex), (int)desiredLength));
                }

                // If the backing store wasn't Utf8String, go down the slow path.

                return GetSpanSlow();
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

        public static bool Equals(Utf8Segment left, Utf8Segment right) => left.Span.Equals(right.Span);

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

        [MethodImpl(MethodImplOptions.NoInlining)]
        private ReadOnlyMemory<byte> GetMemorySlow()
        {
            // Used when the backing store isn't a Utf8String instance and we can't optimize
            // the tear checking logic. We make a copy of the underlying struct to avoid
            // race conditions where we end up performing tear-checking on an instance other
            // than the one we return to our caller.

            ReadOnlyMemory<byte> structCopy = _rawData;
            ThrowIfTorn(structCopy.Span);
            return structCopy;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private ReadOnlySpan<byte> GetSpanSlow()
        {
            // Used when the backing store isn't a Utf8String instance and we can't optimize
            // the tear checking logic.

            ReadOnlySpan<byte> byteSpan = _rawData.Span;
            ThrowIfTorn(byteSpan);
            return byteSpan;
        }

        public override string ToString()
        {
            return Span.ToString();
        }

        /// <summary>
        /// Creates a new <see cref="Utf8String"/> instance whose contents are equivalent
        /// to the contents of this <see cref="Utf8Segment"/>.
        /// </summary>
        public Utf8String ToUtf8String()
        {
            // If we're backed by a Utf8String instance, its Substring method is optimized for
            // returning values without new allocations if at all possible. Otherwise, rely on
            // the Utf8Span methods to perform allocations in the most optimal fashion.

            Utf8String? backingUtf8String = _rawData.GetObjectStartLength(out int start, out int length) as Utf8String;
            return (!(backingUtf8String is null)) ? backingUtf8String.Substring(start, length) : Span.ToUtf8String();
        }

        private static void ThrowIfTorn(ReadOnlySpan<byte> utf8CandidateData)
        {
            ref byte startOfData = ref MemoryMarshal.GetReference(utf8CandidateData);
            nuint length = (uint)utf8CandidateData.Length;

            // Empty spans are by definition well-formed UTF-8.
            // Let them go through.

            if ((uint)length == 0)
            {
                return;
            }

            // The ROM<byte> that backs the Utf8Segment instance is backed by one of three objects:
            // a) a Utf8String instance, or
            // b) a byte[], or
            // c) a MemoryManager<byte>.
            //
            // In the normal "safe" course of operation, the Utf8Segment is backed by a Utf8String,
            // where the entirety of the contents is guaranteed to be immutable, well-formed UTF-8.
            // There's still the possibility that the Utf8Segment could be "torn" within a multi-
            // threaded application, but since the backing fields are Utf8String instances we know
            // that the entirety of the backing buffer is still well-formed UTF-8. Then all we need
            // to check is that the tearing process didn't create a ROM<byte> whose boundary splits
            // a multibyte UTF-8 subsequence. That's a fairly straightforward O(1) check.
            //
            // There's also "unsafe" usage, where the caller creates a Utf8Segment around an arbitrary
            // ROM<byte> instance. The caller must guarantee immutability and well-formedness.
            // However, when tearing the underlying ROM<byte> instance, it's possible that the segment
            // now points to a valid sub-array within the underlying byte[], but that sub-array never
            // contained well-formed UTF-8 data because it wasn't part of the original ROM<byte> segment
            // passed to the unsafe creation API. In theory this means that it's possible our checks
            // below will succeed (since they're only checking the boundaries) even though the segment
            // does not actually contain well-formed UTF-8 data. This could lead to undefined behavior
            // since we assume Utf8Span instances always contain well-formed UTF-8 data.
            //
            // Even so, I think we should march forward with a design which allows the runtime and our
            // consumers to assume that Utf8Span data is well-formed. The aforementioned problem can
            // only ever occur if somebody wrote the word "unsafe" in their code, and there's a general
            // belief that Mem<T> and ROM<T> are "unsafe" types anyway. The typical way we expect folks
            // to use these APIs ends up creating Utf8Segment instances from Utf8String instances, which
            // as mentioned earlier is safe, even with simple tearing detection.

            if (Utf8Utility.IsUtf8ContinuationByte(startOfData))
            {
                goto Torn;
            }

            // TODO_UTF8STRING: We avoid bounds checks below because we assume the underlying buffer
            // contains well-formed UTF-8 data (though we may only be seeing a slice of it). That means
            // that as we walk backward from the end of the buffer we should terminate on or before
            // the point where we hit the beginning of the sequence. Is this a valid assumption?

            // The common case is that the final byte of the buffer is an ASCII byte.
            // If this is true, we know the end of the slice was not torn, so we
            // can return immediately without any further checks.

            if ((sbyte)Unsafe.AddByteOffset(ref startOfData, length - 1) >= 0)
            {
                return;
            }

            // If the last byte is a UTF-8 lead byte [ C0 .. FF ] (we don't care about
            // invalid bytes), then the end of the slice was torn since we expect a
            // continuation byte to follow it.

            if (Unsafe.AddByteOffset(ref startOfData, length - 1) >= 0xC0)
            {
                goto Torn;
            }

            // If the penultimate byte is a UTF-8 3-byte or 4-byte lead byte [ E0 .. FF ]
            // (we don't care about invalid bytes), then the end of the slice was torn
            // since we expect multiple continuation bytes to follow it.

            if (Unsafe.AddByteOffset(ref startOfData, length - 2) >= 0xE0)
            {
                goto Torn;
            }

            // If the 3rd-to-final byte is a UTF-8 4-byte lead byte [ F0 .. FF ]
            // (we don't care about invalid bytes), then the end of the slide was torn
            // since we expect multiple continuation bytes to follow it.

            if (Unsafe.AddByteOffset(ref startOfData, length - 3) >= 0xF0)
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
        public static Utf8Segment UnsafeCreateWithoutValidation(ReadOnlyMemory<byte> buffer)
        {
            return new Utf8Segment(buffer);
        }
    }
}
