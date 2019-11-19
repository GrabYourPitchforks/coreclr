// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers.Binary;
using System.Globalization;

namespace GenUnicodeProp
{
    /// <summary>
    /// Contains information about a code point's Unicode category,
    /// bidi class, and simple case mapping / folding.
    /// </summary>
    internal sealed class CategoryCasingInfo : IEquatable<CategoryCasingInfo>
    {
        public readonly UnicodeCategory unicodeCategory;
        public readonly StrongBidiCategory strongBidiCategory;
        public readonly int offsetToSimpleUppercase;
        public readonly int offsetToSimpleLowercase;
        public readonly int offsetToSimpleTitlecase;
        public readonly int offsetToSimpleCaseFold;
        public readonly bool isWhitespace;

        public CategoryCasingInfo(CodePointInfo codePointInfo)
        {
            unicodeCategory = codePointInfo.UnicodeCategory;
            strongBidiCategory = codePointInfo.StrongBidiCategory;
            isWhitespace = codePointInfo.IsWhitespace;

            if (Program.IncludeCasingData)
            {
                // Only persist the casing data if we have been asked to do so.

                offsetToSimpleUppercase = codePointInfo.OffsetToSimpleUppercase;
                offsetToSimpleLowercase = codePointInfo.OffsetToSimpleLowercase;
                offsetToSimpleTitlecase = codePointInfo.OffsetToSimpleTitlecase;
                offsetToSimpleCaseFold = codePointInfo.OffsetToSimpleCaseFold;
            }
        }

        public override bool Equals(object obj)
        {
            return (obj is CategoryCasingInfo other) && this.Equals(other);
        }

        public bool Equals(CategoryCasingInfo other)
        {
            return !(other is null)
                && this.unicodeCategory == other.unicodeCategory
                && this.strongBidiCategory == other.strongBidiCategory
                && this.offsetToSimpleUppercase == other.offsetToSimpleUppercase
                && this.offsetToSimpleLowercase == other.offsetToSimpleLowercase
                && this.offsetToSimpleTitlecase == other.offsetToSimpleTitlecase
                && this.offsetToSimpleCaseFold == other.offsetToSimpleCaseFold
                && this.isWhitespace == other.isWhitespace;
        }

        public override int GetHashCode()
        {
            return (unicodeCategory,
                strongBidiCategory,
                offsetToSimpleUppercase,
                offsetToSimpleLowercase,
                offsetToSimpleTitlecase,
                offsetToSimpleCaseFold,
                isWhitespace).GetHashCode();
        }

        public static byte[] ToCategoryBytes(CategoryCasingInfo input)
        {
            // We're storing 3 pieces of information in 8 bits:
            // bit 7 (high bit) = isWhitespace?
            // bits 6..5 = restricted bidi class
            // bits 4..0 = Unicode category

            int combinedValue = Convert.ToInt32(input.isWhitespace) << 7;
            combinedValue += (int)input.strongBidiCategory << 5;
            combinedValue += (int)input.unicodeCategory;

            return new byte[] { checked((byte)combinedValue) };
        }

        public static byte[] ToUpperBytes(CategoryCasingInfo input)
        {
            byte[] bytes = new byte[sizeof(int)];
            BinaryPrimitives.WriteInt32LittleEndian(bytes, input.offsetToSimpleUppercase);
            return bytes;
        }

        public static byte[] ToLowerBytes(CategoryCasingInfo input)
        {
            byte[] bytes = new byte[sizeof(int)];
            BinaryPrimitives.WriteInt32LittleEndian(bytes, input.offsetToSimpleLowercase);
            return bytes;
        }

        public static byte[] ToTitleBytes(CategoryCasingInfo input)
        {
            byte[] bytes = new byte[sizeof(int)];
            BinaryPrimitives.WriteInt32LittleEndian(bytes, input.offsetToSimpleTitlecase);
            return bytes;
        }

        public static byte[] ToCaseFoldBytes(CategoryCasingInfo input)
        {
            byte[] bytes = new byte[sizeof(int)];
            BinaryPrimitives.WriteInt32LittleEndian(bytes, input.offsetToSimpleCaseFold);
            return bytes;
        }
    }
}
