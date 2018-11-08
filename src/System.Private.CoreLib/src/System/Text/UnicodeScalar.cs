// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace System.Text
{
    // Represents a Unicode scalar value ([ U+0000..U+D7FF ], inclusive; or [ U+E000..U+10FFFF ], inclusive).
    // This type's ctors are guaranteed to validate the input, and consumers can call the APIs assuming
    // that the input is well-formed.
    //
    // This type's ctors validate, but that shouldn't be a terrible imposition because very few components
    // are going to need to create instances of this type. UnicodeScalar instances will almost always be
    // created as a result of enumeration over a UTF-8 or UTF-16 sequence, or instances will be created
    // by the compiler from known good constants in source. In both cases validation can be elided, which
    // means that there's *no runtime check at all* - not in the ctors nor in the instance methods hanging
    // off this type. This gives improved performance over APIs which require the consumer to call an
    // IsValid method before operating on instances of this type, and it means that we can get away without
    // potentially expensive branching logic in many of our property getters.
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public readonly struct UnicodeScalar : IComparable<UnicodeScalar>, IEquatable<UnicodeScalar>
    {
        private const byte IS_WHITESPACE_FLAG = 0x80;
        private const byte IS_LETTER_OR_DIGIT_FLAG = 0x40;
        private const byte UNICODECATEGORY_MASK = 0x1F;

        // Contains information about the ASCII character range [ U+0000..U+007F ], with:
        // - 0x80 bit if set means 'is whitespace'
        // - 0x40 bit if set means 'is letter or digit'
        // - 0x20 bit is reserved for future use
        // - bottom 5 bits are the UnicodeCategory of the character
        private static ReadOnlySpan<byte> AsciiCharInfo => new byte[]
        {
            0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x8E, 0x8E, 0x8E, 0x8E, 0x8E, 0x0E, 0x0E,
            0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E,
            0x8B, 0x18, 0x18, 0x18, 0x1A, 0x18, 0x18, 0x18, 0x14, 0x15, 0x18, 0x19, 0x18, 0x13, 0x18, 0x18,
            0x48, 0x48, 0x48, 0x48, 0x48, 0x48, 0x48, 0x48, 0x48, 0x48, 0x18, 0x18, 0x19, 0x19, 0x19, 0x18,
            0x18, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40,
            0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x14, 0x18, 0x15, 0x1B, 0x12,
            0x1B, 0x41, 0x41, 0x41, 0x41, 0x41, 0x41, 0x41, 0x41, 0x41, 0x41, 0x41, 0x41, 0x41, 0x41, 0x41,
            0x41, 0x41, 0x41, 0x41, 0x41, 0x41, 0x41, 0x41, 0x41, 0x41, 0x41, 0x14, 0x19, 0x15, 0x19, 0x0E
        };

        private readonly uint _value;

        /// <summary>
        /// Creates a <see cref="UnicodeScalar"/> from the provided UTF-16 code unit.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// If <paramref name="ch"/> represents a UTF-16 surrogate code point
        /// U+D800..U+DFFF, inclusive.
        /// </exception>
        public UnicodeScalar(char ch)
            : this(ch, false)
        {
            if (UnicodeHelpers.IsSurrogateCodePoint(_value))
            {
                // TODO: resource-based error message
                throw new ArgumentOutOfRangeException(
                    message: "Not a valid scalar value.",
                    paramName: nameof(ch));
            }
        }

        /// <summary>
        /// Creates a <see cref="UnicodeScalar"/> from the provided Unicode scalar value.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// If <paramref name="scalarValue"/> does not represent a value Unicode scalar value.
        /// </exception>
        public UnicodeScalar(int scalarValue)
            : this((uint)scalarValue)
        {
        }

        /// <summary>
        /// Creates a <see cref="UnicodeScalar"/> from the provided Unicode scalar value.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// If <paramref name="scalarValue"/> does not represent a value Unicode scalar value.
        /// </exception>
        [CLSCompliant(false)]
        public UnicodeScalar(uint scalarValue)
            : this(scalarValue, false)
        {
            if (!UnicodeHelpers.IsValidUnicodeScalar(_value))
            {
                // TODO: resource-based error message
                throw new ArgumentOutOfRangeException(
                    message: "Not a valid scalar value.",
                    paramName: nameof(scalarValue));
            }
        }

        // non-validating ctor
        private UnicodeScalar(uint scalarValue, bool unused)
        {
            _value = scalarValue;
        }

        public static bool operator ==(UnicodeScalar a, UnicodeScalar b) => (a.Value == b.Value);

        public static bool operator !=(UnicodeScalar a, UnicodeScalar b) => (a.Value != b.Value);

        public static bool operator <(UnicodeScalar a, UnicodeScalar b) => (a.Value < b.Value);

        public static bool operator <=(UnicodeScalar a, UnicodeScalar b) => (a.Value <= b.Value);

        public static bool operator >(UnicodeScalar a, UnicodeScalar b) => (a.Value > b.Value);

        public static bool operator >=(UnicodeScalar a, UnicodeScalar b) => (a.Value >= b.Value);

        // throws if UTF-16 code unit is not a valid scalar
        public static explicit operator UnicodeScalar(char value) => new UnicodeScalar(value);

        // throws if UTF-32 code unit is not a valid scalar
        [CLSCompliant(false)]
        public static explicit operator UnicodeScalar(uint value) => new UnicodeScalar(value);

        // throws if UTF-32 code unit is not a valid scalar
        public static explicit operator UnicodeScalar(int value) => new UnicodeScalar(value);

        private string DebuggerDisplay => FormattableString.Invariant($"'{(IsValid((int)Value) ? ToString() : "\uFFFD")}' (U+{Value:X4})");

        /// <summary>
        /// Returns true iff this scalar value is ASCII ([ U+0000..U+007F ])
        /// and therefore representable by a single UTF-8 code unit.
        /// </summary>
        public bool IsAscii => UnicodeHelpers.IsAsciiCodePoint(_value);

        /// <summary>
        /// Returns true iff this scalar value is within the BMP ([ U+0000..U+FFFF ])
        /// and therefore representable by a single UTF-16 code unit.
        /// </summary>
        public bool IsBmp => UnicodeHelpers.IsBmpCodePoint(_value);

        /// <summary>
        /// Returns the Unicode plane (0 to 16, inclusive) which contains this scalar.
        /// </summary>
        public int Plane => UnicodeHelpers.GetPlane(_value);

        /// <summary>
        /// A <see cref="UnicodeScalar"/> instance that represents the Unicode replacement character U+FFFD.
        /// </summary>
        public static UnicodeScalar ReplacementChar => DangerousCreateWithoutValidation(UnicodeHelpers.ReplacementChar);

        /// <summary>
        /// Returns the length in code units (<see cref="Char"/>) of the
        /// UTF-16 sequence required to represent this scalar value.
        /// </summary>
        /// <remarks>
        /// The return value will be 1 or 2.
        /// </remarks>
        public int Utf16SequenceLength => UnicodeHelpers.GetUtf16SequenceLength(_value);

        /// <summary>
        /// Returns the length in code units (<see cref="Utf8Char"/>) of the
        /// UTF-8 sequence required to represent this scalar value.
        /// </summary>
        /// <remarks>
        /// The return value will be 1 through 4, inclusive.
        /// </remarks>
        public int Utf8SequenceLength => UnicodeHelpers.GetUtf8SequenceLength(_value);

        /// <summary>
        /// Returns the Unicode scalar value as an unsigned integer.
        /// </summary>
        [CLSCompliant(false)]
        public uint Value => _value;

        private static UnicodeScalar ChangeCase(UnicodeScalar s, CultureInfo culture, bool toUpper)
        {
            if (culture == null)
            {
                throw new ArgumentNullException(nameof(culture));
            }

            Span<char> original = stackalloc char[2]; // worst case scenario = 2 code units (for a surrogate pair)
            Span<char> modified = stackalloc char[2]; // case change should preserve UTF-16 code unit count

            int utf16CharCount = s.ToUtf16(original);
            var slice = original.Slice(0, utf16CharCount);
            var textInfo = culture.TextInfo;
            if (toUpper)
            {
                textInfo.ChangeCaseToUpper(slice, modified);
            }
            else
            {
                textInfo.ChangeCaseToLower(slice, modified);
            }

            var result = UnicodeReader.PeekFirstScalarUtf16(modified);
            Debug.Assert(result.status == SequenceValidity.Valid, "Expected case change operation to result in valid scalar value.");
            Debug.Assert(result.charsConsumed == utf16CharCount, "Expected case change operation not to change UTF-16 code unit count.");

            return result.scalar;
        }

        public int CompareTo(UnicodeScalar other) => this.Value.CompareTo(other.Value);

        // Allows constructing a Unicode scalar value from an arbitrary 32-bit integer without
        // validation. It is the caller's responsibility to have performed manual validation
        // before calling this method. If a UnicodeScalar instance is forcibly constructed
        // from invalid input, the APIs on this type have undefined behavior, potentially including
        // introducing a security hole in the consuming application.
        //
        // An example of a security hole resulting from an invalid UnicodeScalar value:
        //
        // public int GetUtf8Marvin32HashCode(UnicodeScalar s) {
        //   Span<Char8> buffer = stackalloc Char8[s.Utf8SequenceLength];
        //   s.ToUtf8(buffer);
        //   return Marvin32.ComputeHash(buffer.AsBytes());
        // }

        [EditorBrowsable(EditorBrowsableState.Never)]
        [CLSCompliant(false)]
        public static UnicodeScalar DangerousCreateWithoutValidation(uint scalarValue) => new UnicodeScalar(scalarValue, false);

        public override bool Equals(object obj) => (obj is UnicodeScalar otherScalar) && this.Equals(otherScalar);

        public bool Equals(UnicodeScalar other) => (this == other);

        public override int GetHashCode() => (int)Value;

        /// <summary>
        /// Returns <see langword="true"/> iff <paramref name="value"/> is a valid Unicode scalar
        /// value, i.e., is in [ U+0000..U+D7FF ], inclusive; or [ U+E000..U+10FFFF ], inclusive.
        /// </summary>
        public static bool IsValid(int value) => IsValid((uint)value);

        /// <summary>
        /// Returns <see langword="true"/> iff <paramref name="value"/> is a valid Unicode scalar
        /// value, i.e., is in [ U+0000..U+D7FF ], inclusive; or [ U+E000..U+10FFFF ], inclusive.
        /// </summary>
        [CLSCompliant(false)]
        public static bool IsValid(uint value) => UnicodeHelpers.IsValidUnicodeScalar(value);

        /// <summary>
        /// Returns a <see cref="string"/> representation of this <see cref="UnicodeScalar"/> instance.
        /// </summary>
        public override string ToString()
        {
            Span<char> chars = stackalloc char[2]; // worst case
            return new string(chars.Slice(0, ToUtf16(chars)));
        }

        /// <summary>
        /// Writes this scalar value as a UTF-16 sequence to the output buffer, returning
        /// the number of code units (<see cref="Char"/>) written.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="output"/> is too short to contain the output.
        /// The required length can be queried ahead of time via the <see cref="Utf16SequenceLength"/> property.
        /// </exception>
        public int ToUtf16(Span<char> output) => ToUtf16(output, _value);

        private static int ToUtf16(Span<char> output, uint value)
        {
            if (UnicodeHelpers.IsBmpCodePoint(value) && output.Length > 0)
            {
                output[0] = (char)value;
                return 1;
            }
            else if (output.Length > 1)
            {
                // TODO: This logic can be optimized into a single unaligned write, endianness-dependent.

                output[0] = (char)((value + ((0xD800U - 0x40U) << 10)) >> 10); // high surrogate
                output[1] = (char)((value & 0x3FFU) + 0xDC00U); // low surrogate
                return 2;
            }
            else
            {
                // TODO: resource-based exception message
                throw new ArgumentException(
                    message: "Output buffer too small.",
                    paramName: nameof(output));
            }
        }

        /// <summary>
        /// Writes this scalar value as a UTF-8 sequence to the output buffer, returning
        /// the number of code units (<see cref="byte"/>) written.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="output"/> is too short to contain the output.
        /// The required length can be queried ahead of time via the <see cref="Utf8SequenceLength"/> property.
        /// </exception>
        public int ToUtf8(Span<byte> output) => ToUtf8(output, _value);

        public int ToUtf8(Span<Utf8Char> output) => ToUtf8(MemoryMarshal.Cast<Utf8Char, byte>(output));

        private static int ToUtf8(Span<byte> output, uint value)
        {
            // TODO: This logic can be optimized into fewer unaligned writes, endianness-dependent.
            // TODO: Consider using BMI2 (pext, pdep) when it comes online.
            // TODO: Consider using hardware-accelerated byte swapping (bswap, movbe) if available.

            if (UnicodeHelpers.IsAsciiCodePoint(value) && output.Length > 0)
            {
                output[0] = (byte)value;
                return 1;
            }
            else if (value < 0x800U && output.Length > 1)
            {
                output[0] = (byte)((value + (0xC0U << 6)) >> 6);
                output[1] = (byte)((value & 0x3FU) + 0x80U);
                return 2;
            }
            else if (value < 0x10000U && output.Length > 2)
            {
                output[0] = (byte)((value + (0xE0U << 12)) >> 12);
                output[1] = (byte)(((value >> 6) & 0x3FU) + 0x80U);
                output[2] = (byte)((value & 0x3FU) + 0x80U);
                return 3;
            }
            else if (output.Length > 3)
            {
                output[0] = (byte)((value + (0xF0U << 18)) >> 18);
                output[1] = (byte)(((value >> 12) & 0x3FU) + 0x80U);
                output[2] = (byte)(((value >> 6) & 0x3FU) + 0x80U);
                output[3] = (byte)((value & 0x3FU) + 0x80U);
                return 4;
            }
            else
            {
                // TODO: resource-based exception message
                throw new ArgumentException(
                    message: "Output buffer too small.",
                    paramName: nameof(output));
            }
        }

        /// <summary>
        /// Returns a <see cref="Utf8String"/> representation of this <see cref="UnicodeScalar"/> instance.
        /// </summary>
        public Utf8String ToUtf8String()
        {
            Span<byte> utf8Chars = stackalloc byte[4]; // worst case

            // TODO: Call non-validating Utf8String ctor (and pass flags)
            return new Utf8String(utf8Chars.Slice(0, ToUtf8(utf8Chars)));
        }

        /// <summary>
        /// Attempts to create a <see cref="UnicodeScalar"/> from the provided input value.
        /// </summary>
        public static bool TryCreate(char value, out UnicodeScalar result)
        {
            if (!UnicodeHelpers.IsSurrogateCodePoint(value))
            {
                result = DangerousCreateWithoutValidation(value);
                return true;
            }
            else
            {
                result = default;
                return false;
            }
        }

        /// <summary>
        /// Attempts to create a <see cref="UnicodeScalar"/> from the provided input value.
        /// </summary>
        public static bool TryCreate(int value, out UnicodeScalar result) => TryCreate((uint)value, out result);

        /// <summary>
        /// Attempts to create a <see cref="UnicodeScalar"/> from the provided input value.
        /// </summary>
        [CLSCompliant(false)]
        public static bool TryCreate(uint value, out UnicodeScalar result)
        {
            if (UnicodeHelpers.IsValidUnicodeScalar(value))
            {
                result = DangerousCreateWithoutValidation(value);
                return true;
            }
            else
            {
                result = default;
                return false;
            }
        }

        // These are analogs of APIs on System.Char

        public static double GetNumericValue(UnicodeScalar s)
        {
            if (s.IsAscii)
            {
                uint baseNum = s.Value - '0';
                return (baseNum <= (uint)9) ? (double)baseNum : -1;
            }
            else
            {
                // not an ASCII char; fall back to globalization table
                return CharUnicodeInfo.InternalGetNumericValue((int)s.Value);
            }
        }

        public static UnicodeCategory GetUnicodeCategory(UnicodeScalar s)
        {
            return (s.Value < (uint)AsciiCharInfo.Length)
                ? (UnicodeCategory)(AsciiCharInfo[(int)s.Value] & UNICODECATEGORY_MASK)
                : GetUnicodeCategoryNonAscii(s);
        }

        private static UnicodeCategory GetUnicodeCategoryNonAscii(UnicodeScalar s)
        {
            Debug.Assert(s.Value >= (uint)AsciiCharInfo.Length, "Shouldn't use this non-optimized code path for ASCII characters.");
            return CharUnicodeInfo.GetUnicodeCategory(codePoint: (int)s.Value);
        }

        // Returns true iff this Unicode category represents a letter
        private static bool IsCategoryLetter(UnicodeCategory category)
        {
            return UnicodeHelpers.IsInRangeInclusive((uint)category, (uint)UnicodeCategory.UppercaseLetter, (uint)UnicodeCategory.OtherLetter);
        }

        // Returns true iff this Unicode category represents a letter or a decimal digit
        private static bool IsCategoryLetterOrDecimalDigit(UnicodeCategory category)
        {
            return UnicodeHelpers.IsInRangeInclusive((uint)category, (uint)UnicodeCategory.UppercaseLetter, (uint)UnicodeCategory.OtherLetter)
                || (category == UnicodeCategory.DecimalDigitNumber);
        }

        // Returns true iff this Unicode category represents a number
        private static bool IsCategoryNumber(UnicodeCategory category)
        {
            return UnicodeHelpers.IsInRangeInclusive((uint)category, (uint)UnicodeCategory.DecimalDigitNumber, (uint)UnicodeCategory.OtherNumber);
        }

        // Returns true iff this Unicode category represents a punctuation mark
        private static bool IsCategoryPunctuation(UnicodeCategory category)
        {
            return UnicodeHelpers.IsInRangeInclusive((uint)category, (uint)UnicodeCategory.ConnectorPunctuation, (uint)UnicodeCategory.OtherPunctuation);
        }

        // Returns true iff this Unicode category represents a separator
        private static bool IsCategorySeparator(UnicodeCategory category)
        {
            return UnicodeHelpers.IsInRangeInclusive((uint)category, (uint)UnicodeCategory.SpaceSeparator, (uint)UnicodeCategory.ParagraphSeparator);
        }

        // Returns true iff this Unicode category represents a symbol
        private static bool IsCategorySymbol(UnicodeCategory category)
        {
            return UnicodeHelpers.IsInRangeInclusive((uint)category, (uint)UnicodeCategory.MathSymbol, (uint)UnicodeCategory.OtherSymbol);
        }

        public static bool IsControl(UnicodeScalar s)
        {
            // Per the Unicode stability policy, the set of control characters
            // is forever fixed at [ U+0000..U+001F ], [ U+007F..U+009F ]. No
            // characters will ever be added to the "control characters" group.
            // See http://www.unicode.org/policies/stability_policy.html.

            // Logic below depends on s.Value never being -1 (since UnicodeScalar is a validating type)
            // 00..1F (+1) => 01..20 (&~80) => 01..20
            // 7F..9F (+1) => 80..A0 (&~80) => 00..20

            return (((s.Value + 1) & ~0x80U) <= 0x20U);
        }

        public static bool IsDigit(UnicodeScalar s)
        {
            return (s.Value < (uint)AsciiCharInfo.Length)
                ? UnicodeHelpers.IsInRangeInclusive(s.Value, '0', '9')
                : (GetUnicodeCategoryNonAscii(s) == UnicodeCategory.DecimalDigitNumber);
        }

        public static bool IsLetter(UnicodeScalar s)
        {
            return (s.Value < (uint)AsciiCharInfo.Length)
                ? (((s.Value - 'A') & ~0x20U) <= (uint)('Z' - 'A')) // [A-Za-z]
                : (IsCategoryLetter(GetUnicodeCategoryNonAscii(s)));
        }

        public static bool IsLetterOrDigit(UnicodeScalar s)
        {
            return (s.Value < (uint)AsciiCharInfo.Length)
                ? ((AsciiCharInfo[(int)s.Value] & IS_LETTER_OR_DIGIT_FLAG) != 0)
                : (IsCategoryLetterOrDecimalDigit(GetUnicodeCategoryNonAscii(s)));
        }

        public static bool IsLower(UnicodeScalar s)
        {
            return (s.IsAscii)
                ? UnicodeHelpers.IsInRangeInclusive(s.Value, 'a', 'z')
                : (GetUnicodeCategoryNonAscii(s) == UnicodeCategory.LowercaseLetter);
        }

        public static bool IsNumber(UnicodeScalar s)
        {
            return (s.IsAscii)
                ? UnicodeHelpers.IsInRangeInclusive(s.Value, '0', '9')
                : IsCategoryNumber(GetUnicodeCategoryNonAscii(s));
        }

        public static bool IsPunctuation(UnicodeScalar s)
        {
            return IsCategoryPunctuation(GetUnicodeCategory(s));
        }

        public static bool IsSeparator(UnicodeScalar s)
        {
            return IsCategorySeparator(GetUnicodeCategory(s));
        }

        public static bool IsSymbol(UnicodeScalar s)
        {
            return IsCategorySymbol(GetUnicodeCategory(s));
        }

        public static bool IsUpper(UnicodeScalar s)
        {
            return (s.IsAscii)
                ? UnicodeHelpers.IsInRangeInclusive(s.Value, 'A', 'Z')
                : (GetUnicodeCategoryNonAscii(s) == UnicodeCategory.UppercaseLetter);
        }

        public static bool IsWhiteSpace(UnicodeScalar s)
        {
            return (s.IsAscii)
                ? ((AsciiCharInfo[(int)s.Value] & IS_WHITESPACE_FLAG) != 0)
                : (s.IsBmp)
                    ? char.IsWhiteSpace((char)s.Value)
                    : IsCategorySeparator(GetUnicodeCategoryNonAscii(s));
        }

        public static UnicodeScalar ToLower(UnicodeScalar s, CultureInfo culture) => ChangeCase(s, culture, toUpper: false);

        public static UnicodeScalar ToLowerInvariant(UnicodeScalar s)
        {
            // Handle the most common case (ASCII data) first. Within the common case, we expect
            // that there'll be a mix of lowercase & uppercase chars, so make the conversion branchless.

            if (s.IsAscii || GlobalizationMode.Invariant)
            {
                bool isUpperAlpha = UnicodeHelpers.IsInRangeInclusive(s.Value, 'A', 'Z');
                return DangerousCreateWithoutValidation(s.Value + ((isUpperAlpha) ? 0x20U : 0));
            }

            // Non-ASCII data requires going through the case folding tables.

            return ToLower(s, CultureInfo.InvariantCulture);
        }

        public static UnicodeScalar ToUpper(UnicodeScalar s, CultureInfo culture) => ChangeCase(s, culture, toUpper: true);

        public static UnicodeScalar ToUpperInvariant(UnicodeScalar s)
        {
            // Handle the most common case (ASCII data) first. Within the common case, we expect
            // that there'll be a mix of lowercase & uppercase chars, so make the conversion branchless.

            if (s.IsAscii || GlobalizationMode.Invariant)
            {
                bool isLowerAlpha = UnicodeHelpers.IsInRangeInclusive(s.Value, 'a', 'z');
                return DangerousCreateWithoutValidation(s.Value ^ ((isLowerAlpha) ? 0x20U : 0));
            }

            // Non-ASCII data requires going through the case folding tables.

            return ToUpper(s, CultureInfo.InvariantCulture);
        }
    }
}
