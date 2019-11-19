// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace GenUnicodeProp
{
    /// <summary>
    /// Contains information about a code point's numeric representation
    /// and the manner in which it's treated for grapheme cluster segmentation
    /// purposes.
    /// </summary>
    internal sealed class NumericGraphemeInfo : IEquatable<NumericGraphemeInfo>
    {
        public readonly sbyte decimalDigitValue;
        public readonly sbyte digitValue;
        public readonly double numericValue;
        public readonly GraphemeBoundaryCategory graphemeCategory;

        public NumericGraphemeInfo(CodePointInfo codePointInfo)
        {
            decimalDigitValue = codePointInfo.DecimalDigitValue;
            digitValue = codePointInfo.DigitValue;
            numericValue = codePointInfo.NumericValue;
            graphemeCategory = codePointInfo.GraphemeBoundaryCategory;
        }

        public override bool Equals(object obj)
        {
            return (obj is NumericGraphemeInfo other) && this.Equals(other);
        }

        public bool Equals(NumericGraphemeInfo other)
        {
            return !(other is null)
                && this.decimalDigitValue == other.decimalDigitValue
                && this.digitValue == other.digitValue
                && this.numericValue == other.numericValue
                && this.graphemeCategory == other.graphemeCategory;
        }

        public override int GetHashCode()
        {
            return (decimalDigitValue,
                digitValue,
                numericValue,
                graphemeCategory).GetHashCode();
        }

        public static byte[] ToDigitBytes(NumericGraphemeInfo input)
        {
            // Bits 4 .. 7 contain (decimalDigitValue + 1).
            // Bits 0 .. 3 contain (digitValue + 1).
            // This means that each nibble will have a value 0x0 .. 0xa, inclusive.

            int adjustedDecimalDigitValue = input.decimalDigitValue + 1;
            int adjustedDigitValue = input.digitValue + 1;

            return new byte[] { (byte)((adjustedDecimalDigitValue << 4) | adjustedDigitValue) };
        }

        public static byte[] ToNumericBytes(NumericGraphemeInfo input)
        {
            byte[] bytes = new byte[sizeof(double)];
            double value = input.numericValue;
            BinaryPrimitives.WriteUInt64LittleEndian(bytes, Unsafe.As<double, ulong>(ref value));
            return bytes;
        }

        public static byte[] ToGraphemeBytes(NumericGraphemeInfo input)
        {
            return new byte[] { checked((byte)input.graphemeCategory) };
        }
    }
}
