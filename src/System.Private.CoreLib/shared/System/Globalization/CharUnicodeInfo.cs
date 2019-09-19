// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Internal.Runtime.CompilerServices;

#pragma warning disable SA1121 // explicitly using type aliases instead of built-in types
#if BIT64
using nint = System.Int64;
using nuint = System.UInt64;
#else
using nint = System.Int32;
using nuint = System.UInt32;
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
        /// Convert the BMP character or surrogate pointed by index to a UTF32 value.
        /// This is similar to char.ConvertToUTF32, but the difference is that
        /// it does not throw exceptions when invalid surrogate characters are passed in.
        ///
        /// WARNING: since it doesn't throw an exception it CAN return a value
        /// in the surrogate range D800-DFFF, which are not legal unicode values.
        /// </summary>
        private static unsafe int ConvertToUtf32(string s, int index)
        {
            Debug.Assert(s != null, "s != null");
            Debug.Assert(index >= 0 && index < s.Length, "index < s.Length");

            nuint nIndex = (uint)index;
            uint codePoint = Unsafe.Add(ref s.GetRawStringData(), (IntPtr)(void*)nIndex);

            uint tempFirstChar = codePoint - HIGH_SURROGATE_START;
            if (tempFirstChar <= HIGH_SURROGATE_RANGE)
            {
                // Read next char; it's ok if we read off the end of the string since it's just the null terminator.
                uint tempSecondChar = Unsafe.Add(ref Unsafe.Add(ref s.GetRawStringData(), (IntPtr)(void*)nIndex), 1);
                tempSecondChar -= LOW_SURROGATE_START;

                if (tempSecondChar <= HIGH_SURROGATE_RANGE)
                {
                    codePoint = (tempFirstChar * 0x400) + tempSecondChar + UNICODE_PLANE01_START;
                }
            }

            UnicodeDebug.AssertIsValidCodePoint(codePoint); // might not be a valid scalar, but should always be a valid code point
            return (int)codePoint;
        }

        /// <summary>
        /// Convert the BMP character or surrogate pointed by index to a UTF32 value.
        /// This is similar to char.ConvertToUTF32, but the difference is that
        /// it does not throw exceptions when invalid surrogate characters are passed in.
        ///
        /// WARNING: since it doesn't throw an exception it CAN return a value
        /// in the surrogate range D800-DFFF, which are not legal unicode values.
        /// </summary>
        private static int ConvertToUtf32(StringBuilder s, int index)
        {
            Debug.Assert(s != null, "s != null");
            Debug.Assert(index >= 0 && index < s.Length, "index < s.Length");

            uint codePoint = s[index];

            uint tempFirstChar = codePoint - HIGH_SURROGATE_START;
            if (tempFirstChar <= HIGH_SURROGATE_RANGE && ++index < s.Length)
            {
                uint tempSecondChar = (uint)s[index] - LOW_SURROGATE_START;
                if (tempSecondChar <= HIGH_SURROGATE_RANGE)
                {
                    codePoint = (tempFirstChar * 0x400) + tempSecondChar + UNICODE_PLANE01_START;
                }
            }

            UnicodeDebug.AssertIsValidCodePoint(codePoint); // might not be a valid scalar, but should always be a valid code point
            return (int)codePoint;
        }

        /// <summary>
        /// Convert a character or a surrogate pair starting at index of string s
        /// to UTF32 value.
        /// WARNING: since it doesn't throw an exception it CAN return a value
        /// in the surrogate range D800-DFFF, which are not legal unicode values.
        /// </summary>
        private static unsafe int ConvertToUtf32(string s, int index, out int charLength)
        {
            Debug.Assert(s != null, "s != null");
            Debug.Assert(index >= 0 && index < s.Length, "index < s.Length");

            nuint nIndex = (uint)index;
            charLength = 1;
            uint codePoint = Unsafe.Add(ref s.GetRawStringData(), (IntPtr)(void*)nIndex);

            uint tempFirstChar = codePoint - HIGH_SURROGATE_START;
            if (tempFirstChar <= HIGH_SURROGATE_RANGE)
            {
                // Read next char; it's ok if we read off the end of the string since it's just the null terminator.
                uint tempSecondChar = Unsafe.Add(ref Unsafe.Add(ref s.GetRawStringData(), (IntPtr)(void*)nIndex), 1);
                tempSecondChar -= LOW_SURROGATE_START;

                if (tempSecondChar <= HIGH_SURROGATE_RANGE)
                {
                    codePoint = (tempFirstChar * 0x400) + tempSecondChar + UNICODE_PLANE01_START;
                    charLength = 2;
                }
            }

            UnicodeDebug.AssertIsValidCodePoint(codePoint); // might not be a valid scalar, but should always be a valid code point
            return (int)codePoint;
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

            return InternalGetNumericValue(ConvertToUtf32(s, index));
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

            return (sbyte)InternalGetDigitValues(ConvertToUtf32(s, index), 0);
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

            return (sbyte)InternalGetDigitValues(ConvertToUtf32(s, index), 1);
        }

        public static UnicodeCategory GetUnicodeCategory(char ch)
        {
            nuint index = GetIndexForCodePointPropertiesTable(ch);

            // Deref the CategoriesValue table, then look at the lower 5 bits.
            return (UnicodeCategory)(Unsafe.AddByteOffset(ref MemoryMarshal.GetReference(CategoriesValue), index) & 0x1f);
        }

        public static UnicodeCategory GetUnicodeCategory(string s, int index)
        {
            if (s == null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.s);
            }
            if ((uint)index >= (uint)s.Length)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.index);
            }

            return InternalGetUnicodeCategory(s, index);
        }

        public static UnicodeCategory GetUnicodeCategory(int codePoint)
        {
            if (!UnicodeUtility.IsValidCodePoint((uint)codePoint))
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.codePoint);
            }

            nuint index = GetIndexForCodePointPropertiesTable((uint)codePoint);

            // Deref the CategoriesValue table, then look at the lower 5 bits.
            return (UnicodeCategory)(Unsafe.AddByteOffset(ref MemoryMarshal.GetReference(CategoriesValue), index) & 0x1f);
        }

        private static nuint GetIndexForCodePointPropertiesTable(uint codePoint)
        {
            UnicodeDebug.AssertIsValidCodePoint(codePoint);

            // Get the level 2 item from the highest 11 bits of the code point.

            uint index = Unsafe.AddByteOffset(ref MemoryMarshal.GetReference(CodePointPropertiesLevel1Index), codePoint >> 9);

            // Get the level 2 WORD offset from the next 5 bits of the code point.
            // This provides the base offset of the level 3 table.
            // Note that & has the lower precedence than addition, so don't forget the parens.

            ref byte level2Ref = ref Unsafe.AddByteOffset(ref MemoryMarshal.GetReference(CodePointPropertiesLevel2Index), (index << 6) + ((codePoint >> 3) & 0b0111110));

            if (BitConverter.IsLittleEndian)
            {
                index = Unsafe.ReadUnaligned<ushort>(ref level2Ref);
            }
            else
            {
                index = BinaryPrimitives.ReverseEndianness(Unsafe.ReadUnaligned<ushort>(ref level2Ref));
            }

            // Get the result from the low nibble of the code point.

            return Unsafe.AddByteOffset(ref MemoryMarshal.GetReference(CodePointPropertiesLevel3Index), (index << 4) + (codePoint & 0xf));
        }

        /// <summary>
        /// Returns the Unicode Category property for the character c.
        /// </summary>
        internal static UnicodeCategory InternalGetUnicodeCategory(string value, int index)
        {
            Debug.Assert(value != null, "value can not be null");
            Debug.Assert(index < value.Length, "index < value.Length");

            return (GetUnicodeCategory(ConvertToUtf32(value, index)));
        }

        internal static RestrictedBidiClass GetBidiClass(string s, int index)
        {
            if (s is null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.s);
            }
            if ((uint)index >= (uint)s.Length)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.index);
            }

            nuint offset = GetIndexForCodePointPropertiesTable((uint)ConvertToUtf32(s, index));

            // Deref the CategoriesValue table, then return the byte as-is (which contains the bidi information)
            return (RestrictedBidiClass)Unsafe.AddByteOffset(ref MemoryMarshal.GetReference(CategoriesValue), offset);
        }

        internal static RestrictedBidiClass GetBidiClass(StringBuilder s, int index)
        {
            if (s is null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.s);
            }
            if ((uint)index >= (uint)s.Length)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.index);
            }

            nuint offset = GetIndexForCodePointPropertiesTable((uint)ConvertToUtf32(s, index));

            // Deref the CategoriesValue table, then return the byte as-is (which contains the bidi information)
            return (RestrictedBidiClass)Unsafe.AddByteOffset(ref MemoryMarshal.GetReference(CategoriesValue), offset);
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

            return GetUnicodeCategory(ConvertToUtf32(str, index, out charLength));
        }

        internal static bool IsCombiningCategory(UnicodeCategory uc)
        {
            Debug.Assert(uc >= 0, "uc >= 0");
            return (
                uc == UnicodeCategory.NonSpacingMark ||
                uc == UnicodeCategory.SpacingCombiningMark ||
                uc == UnicodeCategory.EnclosingMark
            );
        }

        /// <summary>
        /// Returns <see langword="true"/> iff this code point is marked as <em>White_Space</em> in PropList.txt.
        /// </summary>
        internal static bool IsWhiteSpace(char ch)
        {
            nuint index = GetIndexForCodePointPropertiesTable(ch);

            // Deref the CategoriesValue table, then check the high bit.
            return (sbyte)Unsafe.AddByteOffset(ref MemoryMarshal.GetReference(CategoriesValue), index) < 0;
        }

        /// <summary>
        /// Returns <see langword="true"/> iff this code point is marked as <em>White_Space</em> in PropList.txt.
        /// </summary>
        internal static bool IsWhiteSpace(int codePoint)
        {
            ThrowIfInvalidCodePoint(codePoint);

            nuint index = GetIndexForCodePointPropertiesTable((uint)codePoint);

            // Deref the CategoriesValue table, then check the high bit.
            return (sbyte)Unsafe.AddByteOffset(ref MemoryMarshal.GetReference(CategoriesValue), index) < 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [StackTraceHidden]
        private static void ThrowIfInvalidCodePoint(int codePoint)
        {
            if (!UnicodeUtility.IsValidCodePoint((uint)codePoint))
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.codePoint);
            }
        }

        /// <summary>
        /// Returns the simple case fold mapping of this code point per CaseFolding.txt.
        /// </summary>
        internal static unsafe int ToCaseFoldSimple(int codePoint) => SimpleCaseMap(codePoint, ref MemoryMarshal.GetReference(SimpleCaseFoldValue));

        /// <summary>
        /// Returns the <em>Simple_Lowrcase_Mapping</em> of this code point per UnicodeData.txt.
        /// </summary>
        internal static unsafe int ToLowerSimple(int codePoint) => SimpleCaseMap(codePoint, ref MemoryMarshal.GetReference(SimpleLowerValue));

        /// <summary>
        /// Returns the <em>Simple_Titlecase_Mapping</em> of this code point per UnicodeData.txt.
        /// </summary>
        internal static unsafe int ToTitleSimple(int codePoint) => SimpleCaseMap(codePoint, ref MemoryMarshal.GetReference(SimpleTitleValue));

        /// <summary>
        /// Returns the <em>Simple_Uppercase_Mapping</em> of this code point per UnicodeData.txt.
        /// </summary>
        internal static unsafe int ToUpperSimple(int codePoint) => SimpleCaseMap(codePoint, ref MemoryMarshal.GetReference(SimpleUpperValue));

        private static unsafe int SimpleCaseMap(int codePoint, ref byte baseMappingTable)
        {
            ThrowIfInvalidCodePoint(codePoint);

            nuint index = GetIndexForCodePointPropertiesTable((uint)codePoint);

            // Deref the mapping table table, then add the offset to the code point.

            int offset = Unsafe.Add(ref Unsafe.As<byte, int>(ref baseMappingTable), (IntPtr)(void*)index);
            int mapped = codePoint + offset;

            Debug.Assert(UnicodeUtility.GetPlane((uint)codePoint) == UnicodeUtility.GetPlane((uint)mapped), "The Unicode plane shouldn't change after case mapping.");

            return mapped;
        }
    }
}
