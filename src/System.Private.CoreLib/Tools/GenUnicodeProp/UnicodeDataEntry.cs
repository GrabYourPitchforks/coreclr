// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;

namespace GenUnicodeProp
{
    // struct gives us automatic hash code calculation & equality operators
    internal struct UnicodeDataEntry
    {
        public uint codePoint;
        public string name;
        public string generalCategory;
        public string bidiClass;
        public string decimalDigitValue;
        public string digitValue;
        public string numericValue;
        public uint? simpleUppercaseMapping;
        public uint? simpleLowercaseMapping;
        public uint? simpleTitlecaseMapping;
    }
}
