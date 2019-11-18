// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;

namespace GenUnicodeProp
{
    /// <summary>
    /// Contains data about a Unicode code point.
    /// </summary>
    internal class CodePointInfo
    {
        public uint CodePoint = 0;
        public UnicodeCategory UnicodeCategory = UnicodeCategory.OtherNotAssigned;
        public RestrictedBidiClass RestrictedBidiClass = RestrictedBidiClass.Other;
        public int OffsetToSimpleUppercase = 0;
        public int OffsetToSimpleLowercase = 0;
        public int OffsetToSimpleTitlecase = 0;
        public int OffsetToSimpleCaseFold = 0;
        public bool IsWhitespace = false;
        public sbyte DecimalDigitValue = -1;
        public sbyte DigitValue = -1;
        public double NumericValue = -1;
        public GraphemeBoundaryCategory GraphemeBoundaryCategory = GraphemeBoundaryCategory.Unassigned;
    }
}
