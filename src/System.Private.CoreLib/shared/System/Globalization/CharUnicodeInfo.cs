// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Internal.Runtime.CompilerServices;

#pragma warning disable SA1121 // explicitly using type aliases instead of built-in types
#if BIT64
using nuint = System.UInt64;
#else
using nuint = System.UInt32;
#endif
#if !CORECLR
#if BIT64
using nint = System.Int64;
#else
using nint = System.Int32;
#endif
#endif

namespace System.Globalization
{
    /// <summary>
    /// This class implements a set of methods for retrieving character type
    /// information. Character type information is independent of culture
    /// and region.
    /// </summary>
    public static partial class CharUnicodeInfo
    {
        internal const char HIGH_SURROGATE_START = '\ud800';
        internal const char HIGH_SURROGATE_END = '\udbff';
        internal const char LOW_SURROGATE_START = '\udc00';
        internal const char LOW_SURROGATE_END = '\udfff';
        internal const int  HIGH_SURROGATE_RANGE = 0x3FF;

        internal const int UNICODE_CATEGORY_OFFSET = 0;
        internal const int BIDI_CATEGORY_OFFSET = 1;

        // The starting codepoint for Unicode plane 1.  Plane 1 contains 0x010000 ~ 0x01ffff.
        internal const int UNICODE_PLANE01_START = 0x10000;

        /// <summary>
        /// Returns the code point pointed to by index, decoding any surrogate sequence if possible.
        /// This is similar to char.ConvertToUTF32, but the difference is that
        /// it does not throw exceptions when invalid surrogate characters are passed in.
        ///
        /// WARNING: since it doesn't throw an exception it CAN return a value
        /// in the surrogate range D800-DFFF, which is not a legal scalar value.
        /// </summary>
        private static int InternalGetCodePointFromString(string s, int index)
        {
            Debug.Assert(s != null, "s != null");
            Debug.Assert((uint)index < (uint)s.Length, "index < s.Length");

            int codePoint = 0;

            // We know the 'if' block below will always succeed, but it allows the
            // JIT to optimize the codegen of this method.

            if ((uint)index < (uint)s.Length)
            {
                codePoint = s[index];
                int temp1 = codePoint - HIGH_SURROGATE_START;
                if ((uint)temp1 <= HIGH_SURROGATE_RANGE)
                {
                    index++;
                    if ((uint)index < (uint)s.Length)
                    {
                        int temp2 = s[index] - LOW_SURROGATE_START;
                        if (temp2 <= HIGH_SURROGATE_RANGE)
                        {
                            // Combine these surrogate code points into a supplementary code point
                            codePoint = (temp1 << 18) + temp2 + UNICODE_PLANE01_START;
                        }
                    }
                }
            }

            return codePoint;
        }

        /// <summary>
        /// Convert a character or a surrogate pair starting at index of string s
        /// to UTF32 value.
        /// WARNING: since it doesn't throw an exception it CAN return a value
        /// in the surrogate range D800-DFFF, which are not legal unicode values.
        /// </summary>
        internal static int InternalConvertToUtf32(string s, int index, out int charLength)
        {
            Debug.Assert(s != null, "s != null");
            Debug.Assert(s.Length > 0, "s.Length > 0");
            Debug.Assert(index >= 0 && index < s.Length, "index >= 0 && index < s.Length");
            charLength = 1;
            if (index < s.Length - 1)
            {
                int temp1 = (int)s[index] - HIGH_SURROGATE_START;
                if ((uint)temp1 <= HIGH_SURROGATE_RANGE)
                {
                    int temp2 = (int)s[index + 1] - LOW_SURROGATE_START;
                    if ((uint)temp2 <= HIGH_SURROGATE_RANGE)
                    {
                        // Convert the surrogate to UTF32 and get the result.
                        charLength++;
                        return (temp1 * 0x400) + temp2 + UNICODE_PLANE01_START;
                    }
                }
            }
            return (int)s[index];
        }

        /// <summary>
        /// This is called by the public char and string, index versions
        /// Note that for ch in the range D800-DFFF we just treat it as any
        /// other non-numeric character
        /// </summary>
        internal static double InternalGetNumericValue(int ch)
        {
            Debug.Assert(ch >= 0 && ch <= 0x10ffff, "ch is not in valid Unicode range.");
            // Get the level 2 item from the highest 12 bit (8 - 19) of ch.
            int index = ch >> 8;
            if ((uint)index < (uint)NumericLevel1Index.Length)
            {
                index = NumericLevel1Index[index];
                // Get the level 2 offset from the 4 - 7 bit of ch.  This provides the base offset of the level 3 table.
                // Note that & has the lower precedence than addition, so don't forget the parathesis.
                index = NumericLevel2Index[(index << 4) + ((ch >> 4) & 0x000f)];
                index = NumericLevel3Index[(index << 4) + (ch & 0x000f)];
                ref byte value = ref Unsafe.AsRef(in NumericValues[index * 8]);

                if (BitConverter.IsLittleEndian)
                {
                    return Unsafe.ReadUnaligned<double>(ref value);
                }

                return BitConverter.Int64BitsToDouble(BinaryPrimitives.ReverseEndianness(Unsafe.ReadUnaligned<long>(ref value)));
            }
            return -1;
        }

        internal static byte InternalGetDigitValues(int ch, int offset)
        {
            Debug.Assert(ch >= 0 && ch <= 0x10ffff, "ch is not in valid Unicode range.");
            // Get the level 2 item from the highest 12 bit (8 - 19) of ch.
            int index = ch >> 8;
            if ((uint)index < (uint)NumericLevel1Index.Length)
            {
                index = NumericLevel1Index[index];
                // Get the level 2 offset from the 4 - 7 bit of ch.  This provides the base offset of the level 3 table.
                // Note that & has the lower precedence than addition, so don't forget the parathesis.
                index = NumericLevel2Index[(index << 4) + ((ch >> 4) & 0x000f)];
                index = NumericLevel3Index[(index << 4) + (ch & 0x000f)];
                return DigitValues[index * 2 + offset];
            }
            return 0xff;
        }

        /// <summary>
        /// Returns the numeric value associated with the character c.
        /// If the character is a fraction,  the return value will not be an
        /// integer. If the character does not have a numeric value, the return
        /// value is -1.
        /// </summary>
        public static double GetNumericValue(char ch)
        {
            return InternalGetNumericValue(ch);
        }

        public static double GetNumericValue(string s, int index)
        {
            if (s == null)
            {
                throw new ArgumentNullException(nameof(s));
            }
            if (index < 0 || index >= s.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index), SR.ArgumentOutOfRange_Index);
            }

            return InternalGetNumericValue(InternalConvertToUtf32(s, index));
        }

        public static int GetDecimalDigitValue(char ch)
        {
            return (sbyte)InternalGetDigitValues(ch, 0);
        }

        public static int GetDecimalDigitValue(string s, int index)
        {
            if (s == null)
            {
                throw new ArgumentNullException(nameof(s));
            }
            if (index < 0 || index >= s.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index), SR.ArgumentOutOfRange_Index);
            }

            return (sbyte)InternalGetDigitValues(InternalConvertToUtf32(s, index), 0);
        }

        public static int GetDigitValue(char ch)
        {
            return (sbyte)InternalGetDigitValues(ch, 1);
        }

        public static int GetDigitValue(string s, int index)
        {
            if (s == null)
            {
                throw new ArgumentNullException(nameof(s));
            }
            if (index < 0 || index >= s.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index), SR.ArgumentOutOfRange_Index);
            }

            return (sbyte)InternalGetDigitValues(InternalConvertToUtf32(s, index), 1);
        }

        /// <summary>
        /// Given a <see cref="char"/>, returns its <see cref="UnicodeCategory"/>.
        /// </summary>
        public static UnicodeCategory GetUnicodeCategory(char ch)
        {
            return GetUnicodeCategoryNoBoundsChecks(ch);
        }

        /// <summary>
        /// Given an input <see cref="string"/> and an index into that <see cref="string"/>,
        /// returns the <see cref="UnicodeCategory"/> of that code point.
        /// </summary>
        /// <remarks>
        /// This method will decode surrogate sequences if possible.
        /// </remarks>
        public static UnicodeCategory GetUnicodeCategory(string s, int index)
        {
            if (s is null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.s);
            }
            if ((uint)index >= (uint)s.Length)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.index);
            }

            return GetUnicodeCategoryNoBoundsChecks((uint)InternalGetCodePointFromString(s, index));
        }

        /// <summary>
        /// Given a Unicode code point, returns its <see cref="UnicodeCategory"/>.
        /// </summary>
        public static UnicodeCategory GetUnicodeCategory(int codePoint)
        {
            if (!UnicodeUtility.IsValidCodePoint((uint)codePoint))
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.codePoint);
            }

            return GetUnicodeCategoryNoBoundsChecks((uint)codePoint);
        }

        /// <summary>
        /// Retrieves the offset into the "CategoryCasing" arrays where this code point's
        /// information is stored. Used for getting the Unicode category, bidi information,
        /// and whitespace information.
        /// </summary>
        private static nuint GetCategoryCasingTableOffsetNoBoundsChecks(uint codePoint)
        {
            UnicodeDebug.AssertIsValidCodePoint(codePoint);

            // The code below is written with the assumption that the backing store is 11:5:4.
            AssertCategoryCasingTableLevels(11, 5, 4);

            // Get the level index item from the high 11 bits of the code point.

            uint index = Unsafe.AddByteOffset(ref MemoryMarshal.GetReference(CategoryCasingLevel1Index), codePoint >> 9);

            // Get the level 2 WORD offset from the next 5 bits of the code point.
            // This provides the base offset of the level 3 table.
            // Note that & has lower precedence than +, so remember the parens.

            ref byte level2Ref = ref Unsafe.AddByteOffset(ref MemoryMarshal.GetReference(CategoryCasingLevel2Index), (index << 6) + ((codePoint >> 3) & 0b00111110));

            if (BitConverter.IsLittleEndian)
            {
                index = Unsafe.ReadUnaligned<ushort>(ref level2Ref);
            }
            else
            {
                index = BinaryPrimitives.ReverseEndianness(Unsafe.ReadUnaligned<ushort>(ref level2Ref));
            }

            // Get the result from the low 4 bits of the code point.
            // This is the offset into the values table where the data is stored.

            return Unsafe.AddByteOffset(ref MemoryMarshal.GetReference(CategoryCasingLevel3Index), (index << 4) + (codePoint & 0x0F));
        }

        /// <summary>
        /// Retrieves the offset into the "NumericGrapheme" arrays where this code point's
        /// information is stored. Used for getting numeric information and grapheme boundary
        /// information.
        /// </summary>
        private static nuint GetNumericGraphemeTableOffsetNoBoundsChecks(uint codePoint)
        {
            UnicodeDebug.AssertIsValidCodePoint(codePoint);

            // The code below is written with the assumption that the backing store is 11:5:4.
            AssertNumericGraphemeTableLevels(11, 5, 4);

            // Get the level index item from the high 11 bits of the code point.

            uint index = Unsafe.AddByteOffset(ref MemoryMarshal.GetReference(NumericGraphemeLevel1Index), codePoint >> 9);

            // Get the level 2 WORD offset from the next 5 bits of the code point.
            // This provides the base offset of the level 3 table.
            // Note that & has lower precedence than +, so remember the parens.

            ref byte level2Ref = ref Unsafe.AddByteOffset(ref MemoryMarshal.GetReference(NumericGraphemeLevel2Index), (index << 6) + ((codePoint >> 3) & 0b00111110));

            if (BitConverter.IsLittleEndian)
            {
                index = Unsafe.ReadUnaligned<ushort>(ref level2Ref);
            }
            else
            {
                index = BinaryPrimitives.ReverseEndianness(Unsafe.ReadUnaligned<ushort>(ref level2Ref));
            }

            // Get the result from the low 4 bits of the code point.
            // This is the offset into the values table where the data is stored.

            return Unsafe.AddByteOffset(ref MemoryMarshal.GetReference(NumericGraphemeLevel3Index), (index << 4) + (codePoint & 0x0F));
        }

        private static StrongBidiCategory GetStrongBidiCategoryNoBoundsChecks(uint codePoint)
        {
            nuint offset = GetCategoryCasingTableOffsetNoBoundsChecks(codePoint);

            // Each entry of the 'CategoryValues' table uses bits 5 - 6 to store the strong bidi information.

            StrongBidiCategory bidiCategory = (StrongBidiCategory)(Unsafe.AddByteOffset(ref MemoryMarshal.GetReference(CategoriesValues), offset) & 0b_0110_0000);
            Debug.Assert(bidiCategory == StrongBidiCategory.Other || bidiCategory == StrongBidiCategory.StrongLeftToRight || bidiCategory == StrongBidiCategory.StrongRightToLeft, "Unknown StrongBidiCategory value.");

            return bidiCategory;
        }

        private static UnicodeCategory GetUnicodeCategoryNoBoundsChecks(uint codePoint)
        {
            nuint offset = GetCategoryCasingTableOffsetNoBoundsChecks(codePoint);

            // Each entry of the 'CategoriesValues' table uses the low 5 bits to store the UnicodeCategory information.

            return (UnicodeCategory)(Unsafe.AddByteOffset(ref MemoryMarshal.GetReference(CategoriesValues), offset) & 0b_0001_1111);
        }

        /// <summary>
        /// Returns the Unicode Category property for the character c.
        /// </summary>
        internal static UnicodeCategory InternalGetUnicodeCategory(string value, int index)
        {
            Debug.Assert(value != null, "value can not be null");
            Debug.Assert(index < value.Length, "index < value.Length");

            return GetUnicodeCategory(InternalConvertToUtf32(value, index));
        }

        internal static StrongBidiCategory GetBidiCategory(string s, int index)
        {
            if (s is null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.s);
            }
            if ((uint)index >= (uint)s.Length)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.index);
            }

            return GetStrongBidiCategoryNoBoundsChecks((uint)InternalGetCodePointFromString(s, index));
        }

        internal static StrongBidiCategory GetBidiCategory(StringBuilder s, int index)
        {
            Debug.Assert(s != null, "s != null");
            Debug.Assert(index >= 0 && index < s.Length, "index < s.Length");

            int c = (int)s[index];
            if (index < s.Length - 1)
            {
                int temp1 = c - HIGH_SURROGATE_START;
                if ((uint)temp1 <= HIGH_SURROGATE_RANGE)
                {
                    int temp2 = (int)s[index + 1] - LOW_SURROGATE_START;
                    if ((uint)temp2 <= HIGH_SURROGATE_RANGE)
                    {
                        // Convert the surrogate to UTF32 and get the result.
                        c = (temp1 << 18) + temp2 + UNICODE_PLANE01_START;
                    }
                }
            }

            return GetStrongBidiCategoryNoBoundsChecks((uint)c);
        }

        /// <summary>
        /// Get the Unicode category of the character starting at index.  If the character is in BMP, charLength will return 1.
        /// If the character is a valid surrogate pair, charLength will return 2.
        /// </summary>
        internal static UnicodeCategory InternalGetUnicodeCategory(string str, int index, out int charLength)
        {
            Debug.Assert(str != null, "str can not be null");
            Debug.Assert(str.Length > 0, "str.Length > 0");
            Debug.Assert(index >= 0 && index < str.Length, "index >= 0 && index < str.Length");

            return GetUnicodeCategory(InternalConvertToUtf32(str, index, out charLength));
        }

        internal static bool IsCombiningCategory(UnicodeCategory uc)
        {
            Debug.Assert(uc >= 0, "uc >= 0");
            return
                uc == UnicodeCategory.NonSpacingMark ||
                uc == UnicodeCategory.SpacingCombiningMark ||
                uc == UnicodeCategory.EnclosingMark
            ;
        }
    }
}
