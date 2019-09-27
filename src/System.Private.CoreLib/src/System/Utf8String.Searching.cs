// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using System.Text;
using System.Text.Unicode;

namespace System
{
    public sealed partial class Utf8String
    {
        /// <summary>
        /// Returns a value stating whether the current <see cref="Utf8String"/> instance contains
        /// <paramref name="value"/>. An ordinal comparison is used.
        /// </summary>
        public bool Contains(char value)
        {
            return Rune.TryCreate(value, out Rune rune) && Contains(rune);
        }

        /// <summary>
        /// Returns a value stating whether the current <see cref="Utf8String"/> instance contains
        /// <paramref name="value"/>. The specified comparison is used.
        /// </summary>
        public bool Contains(char value, StringComparison comparison)
        {
            return Rune.TryCreate(value, out Rune rune) && Contains(rune, comparison);
        }

        /// <summary>
        /// Returns a value stating whether the current <see cref="Utf8String"/> instance contains
        /// the specified <see cref="Rune"/>. An ordinal comparison is used.
        /// </summary>
        public bool Contains(Rune value)
        {
            // TODO_UTF8STRING: This should be split into two methods:
            // One which operates on a single-byte (ASCII) search value,
            // the other which operates on a multi-byte (non-ASCII) search value.

            Span<byte> runeBytes = stackalloc byte[Utf8Utility.MaxBytesPerScalar];
            int runeBytesWritten = value.EncodeToUtf8(runeBytes);

            return SpanHelpers.IndexOf(
                ref DangerousGetMutableReference(), Length,
                ref MemoryMarshal.GetReference(runeBytes), runeBytesWritten) >= 0;
        }

        /// <summary>
        /// Returns a value stating whether the current <see cref="Utf8String"/> instance contains
        /// the specified <see cref="Rune"/>. The specified comparison is used.
        /// </summary>
        public bool Contains(Rune value, StringComparison comparison)
        {
            // TODO_UTF8STRING: Optimize me to avoid allocations.

            return this.ToString().Contains(value.ToString(), comparison);
        }

        /// <summary>
        /// Returns a value stating whether the current <see cref="Utf8String"/> instance contains <paramref name="value"/>.
        /// An ordinal comparison is used.
        /// </summary>
        public bool Contains(Utf8String value)
        {
            if (value is null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.value);
            }

            return this.AsBytes().IndexOf(value.AsBytes()) >= 0;
        }

        /// <summary>
        /// Returns a value stating whether the current <see cref="Utf8String"/> instance contains <paramref name="value"/>.
        /// The specified comparison is used.
        /// </summary>
        public bool Contains(Utf8String value, StringComparison comparison)
        {
            if (value is null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.value);
            }

            // TODO_UTF8STRING: Optimize me to avoid allocations.

            return this.ToString().Contains(value.ToString(), comparison);
        }

        /// <summary>
        /// Returns a value stating whether the current <see cref="Utf8String"/> instance ends with
        /// <paramref name="value"/>. An ordinal comparison is used.
        /// </summary>
        public bool EndsWith(char value)
        {
            return Rune.TryCreate(value, out Rune rune) && EndsWith(rune);
        }

        /// <summary>
        /// Returns a value stating whether the current <see cref="Utf8String"/> instance ends with
        /// <paramref name="value"/>. The specified comparison is used.
        /// </summary>
        public bool EndsWith(char value, StringComparison comparison)
        {
            return Rune.TryCreate(value, out Rune rune) && EndsWith(rune, comparison);
        }

        /// <summary>
        /// Returns a value stating whether the current <see cref="Utf8String"/> instance ends with
        /// the specified <see cref="Rune"/>. An ordinal comparison is used.
        /// </summary>
        public bool EndsWith(Rune value)
        {
            // TODO_UTF8STRING: This should be split into two methods:
            // One which operates on a single-byte (ASCII) search value,
            // the other which operates on a multi-byte (non-ASCII) search value.

            Span<byte> runeBytes = stackalloc byte[Utf8Utility.MaxBytesPerScalar];
            int runeBytesWritten = value.EncodeToUtf8(runeBytes);

            return this.AsBytes().EndsWith(runeBytes.Slice(0, runeBytesWritten));
        }

        /// <summary>
        /// Returns a value stating whether the current <see cref="Utf8String"/> instance ends with
        /// the specified <see cref="Rune"/>. The specified comparison is used.
        /// </summary>
        public bool EndsWith(Rune value, StringComparison comparison)
        {
            // TODO_UTF8STRING: Optimize me to avoid allocations.

            return this.ToString().EndsWith(value.ToString(), comparison);
        }

        /// <summary>
        /// Returns a value stating whether the current <see cref="Utf8String"/> instance ends with <paramref name="value"/>.
        /// An ordinal comparison is used.
        /// </summary>
        public bool EndsWith(Utf8String value)
        {
            if (value is null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.value);
            }

            return this.AsBytes().EndsWith(value.AsBytes());
        }

        /// <summary>
        /// Returns a value stating whether the current <see cref="Utf8String"/> instance ends with <paramref name="value"/>.
        /// The specified comparison is used.
        /// </summary>
        public bool EndsWith(Utf8String value, StringComparison comparison)
        {
            if (value is null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.value);
            }

            // TODO_UTF8STRING: Optimize me to avoid allocations.

            return this.ToString().EndsWith(value.ToString(), comparison);
        }

        /// <summary>
        /// Returns a value stating whether the current <see cref="Utf8String"/> instance begins with
        /// <paramref name="value"/>. An ordinal comparison is used.
        /// </summary>
        public bool StartsWith(char value)
        {
            return Rune.TryCreate(value, out Rune rune) && StartsWith(rune);
        }

        /// <summary>
        /// Returns a value stating whether the current <see cref="Utf8String"/> instance begins with
        /// <paramref name="value"/>. The specified comparison is used.
        /// </summary>
        public bool StartsWith(char value, StringComparison comparison)
        {
            return Rune.TryCreate(value, out Rune rune) && StartsWith(rune, comparison);
        }

        /// <summary>
        /// Returns a value stating whether the current <see cref="Utf8String"/> instance begins with
        /// the specified <see cref="Rune"/>. An ordinal comparison is used.
        /// </summary>
        public bool StartsWith(Rune value)
        {
            // TODO_UTF8STRING: This should be split into two methods:
            // One which operates on a single-byte (ASCII) search value,
            // the other which operates on a multi-byte (non-ASCII) search value.

            Span<byte> runeBytes = stackalloc byte[Utf8Utility.MaxBytesPerScalar];
            int runeBytesWritten = value.EncodeToUtf8(runeBytes);

            return this.AsBytes().StartsWith(runeBytes.Slice(0, runeBytesWritten));
        }

        /// <summary>
        /// Returns a value stating whether the current <see cref="Utf8String"/> instance begins with
        /// the specified <see cref="Rune"/>. The specified comparison is used.
        /// </summary>
        public bool StartsWith(Rune value, StringComparison comparison)
        {
            // TODO_UTF8STRING: Optimize me to avoid allocations.

            return this.ToString().StartsWith(value.ToString(), comparison);
        }

        /// <summary>
        /// Returns a value stating whether the current <see cref="Utf8String"/> instance begins with <paramref name="value"/>.
        /// An ordinal comparison is used.
        /// </summary>
        public bool StartsWith(Utf8String value)
        {
            if (value is null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.value);
            }

            return this.AsBytes().StartsWith(value.AsBytes());
        }

        /// <summary>
        /// Returns a value stating whether the current <see cref="Utf8String"/> instance begins with <paramref name="value"/>.
        /// The specified comparison is used.
        /// </summary>
        public bool StartsWith(Utf8String value, StringComparison comparison)
        {
            if (value is null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.value);
            }

            // TODO_UTF8STRING: Optimize me to avoid allocations.

            return this.ToString().StartsWith(value.ToString(), comparison);
        }

        /// <summary>
        /// Attempts to locate the target <paramref name="value"/> within this <see cref="Utf8String"/> instance.
        /// If <paramref name="value"/> is found, returns <see langword="true"/> and sets <paramref name="range"/> to
        /// the location where <paramref name="value"/> occurs within this <see cref="Utf8String"/> instance.
        /// If <paramref name="value"/> is not found, returns <see langword="false"/> and sets <paramref name="range"/>
        /// to <see langword="default"/>.
        /// </summary>
        /// <remarks>
        /// An ordinal search is performed.
        /// </remarks>
        public bool TryFind(char value, out Range range)
        {
            if (Rune.TryCreate(value, out Rune rune))
            {
                return TryFind(rune, out range);
            }
            else
            {
                // Surrogate chars can't exist in well-formed UTF-8 data - bail immediately.

                range = default;
                return false;
            }
        }

        /// <summary>
        /// Attempts to locate the target <paramref name="value"/> within this <see cref="Utf8String"/> instance.
        /// If <paramref name="value"/> is found, returns <see langword="true"/> and sets <paramref name="range"/> to
        /// the location where <paramref name="value"/> occurs within this <see cref="Utf8String"/> instance.
        /// If <paramref name="value"/> is not found, returns <see langword="false"/> and sets <paramref name="range"/>
        /// to <see langword="default"/>.
        /// </summary>
        /// <remarks>
        /// The search is performed using the specified <paramref name="comparisonType"/>.
        /// </remarks>
        public bool TryFind(char value, StringComparison comparisonType, out Range range)
        {
            if (Rune.TryCreate(value, out Rune rune))
            {
                return TryFind(rune, comparisonType, out range);
            }
            else
            {
                string.CheckStringComparison(comparisonType);

                // Surrogate chars can't exist in well-formed UTF-8 data - bail immediately.

                range = default;
                return false;
            }
        }

        /// <summary>
        /// Attempts to locate the target <paramref name="value"/> within this <see cref="Utf8String"/> instance.
        /// If <paramref name="value"/> is found, returns <see langword="true"/> and sets <paramref name="range"/> to
        /// the location where <paramref name="value"/> occurs within this <see cref="Utf8String"/> instance.
        /// If <paramref name="value"/> is not found, returns <see langword="false"/> and sets <paramref name="range"/>
        /// to <see langword="default"/>.
        /// </summary>
        /// <remarks>
        /// An ordinal search is performed.
        /// </remarks>
        public bool TryFind(Rune value, out Range range)
        {
            if (value.IsAscii)
            {
                // Special-case ASCII since it's a simple single byte search.

                int idx = this.AsBytes().IndexOf((byte)value.Value);
                if (idx < 0)
                {
                    range = default;
                    return false;
                }
                else
                {
                    range = idx..(idx + 1);
                    return true;
                }
            }
            else
            {
                // Slower path: need to search a multi-byte sequence.
                // TODO_UTF8STRING: As an optimization, we could use unsafe APIs below since we
                // know Rune instances are well-formed and slicing is safe.

                Span<byte> bytes = stackalloc byte[Utf8Utility.MaxBytesPerScalar];
                int utf8ByteLengthOfRune = value.EncodeToUtf8(bytes);

                return this.AsSpan().TryFind(Utf8Span.UnsafeCreateWithoutValidation(bytes.Slice(0, utf8ByteLengthOfRune)), out range);
            }
        }

        /// <summary>
        /// Attempts to locate the target <paramref name="value"/> within this <see cref="Utf8String"/> instance.
        /// If <paramref name="value"/> is found, returns <see langword="true"/> and sets <paramref name="range"/> to
        /// the location where <paramref name="value"/> occurs within this <see cref="Utf8String"/> instance.
        /// If <paramref name="value"/> is not found, returns <see langword="false"/> and sets <paramref name="range"/>
        /// to <see langword="default"/>.
        /// </summary>
        /// <remarks>
        /// The search is performed using the specified <paramref name="comparisonType"/>.
        /// </remarks>
        public bool TryFind(Rune value, StringComparison comparisonType, out Range range)
        {
            if (comparisonType == StringComparison.Ordinal)
            {
                return TryFind(value, out range);
            }
            else
            {
                // Slower path: not an ordinal comparison.
                // TODO_UTF8STRING: As an optimization, we could use unsafe APIs below since we
                // know Rune instances are well-formed and slicing is safe.

                Span<byte> bytes = stackalloc byte[Utf8Utility.MaxBytesPerScalar];
                int utf8ByteLengthOfRune = value.EncodeToUtf8(bytes);

                return this.AsSpan().TryFind(Utf8Span.UnsafeCreateWithoutValidation(bytes.Slice(0, utf8ByteLengthOfRune)), comparisonType, out range);
            }
        }

        /// <summary>
        /// Attempts to locate the target <paramref name="value"/> within this <see cref="Utf8String"/> instance.
        /// If <paramref name="value"/> is found, returns <see langword="true"/> and sets <paramref name="range"/> to
        /// the location where <paramref name="value"/> occurs within this <see cref="Utf8String"/> instance.
        /// If <paramref name="value"/> is not found, returns <see langword="false"/> and sets <paramref name="range"/>
        /// to <see langword="default"/>.
        /// </summary>
        /// <remarks>
        /// An ordinal search is performed.
        /// </remarks>
        public bool TryFind(Utf8String value, out Range range)
        {
            if (value is null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.value);
            }

            return ((Utf8Span)this).TryFind(value, out range);
        }

        /// <summary>
        /// Attempts to locate the target <paramref name="value"/> within this <see cref="Utf8String"/> instance.
        /// If <paramref name="value"/> is found, returns <see langword="true"/> and sets <paramref name="range"/> to
        /// the location where <paramref name="value"/> occurs within this <see cref="Utf8String"/> instance.
        /// If <paramref name="value"/> is not found, returns <see langword="false"/> and sets <paramref name="range"/>
        /// to <see langword="default"/>.
        /// </summary>
        /// <remarks>
        /// The search is performed using the specified <paramref name="comparisonType"/>.
        /// </remarks>
        public bool TryFind(Utf8String value, StringComparison comparisonType, out Range range)
        {
            if (value is null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.value);
            }

            return ((Utf8Span)this).TryFind(value, comparisonType, out range);
        }
    }
}
