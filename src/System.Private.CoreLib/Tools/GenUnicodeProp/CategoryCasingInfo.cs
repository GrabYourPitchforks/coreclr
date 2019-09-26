// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;

namespace GenUnicodeProp
{
    // struct gives us automatic hash code calculation & equality operators
    internal struct CategoryCasingInfo
    {
        public UnicodeCategory? unicodeCategory;
        public RestrictedBidiClass restrictedBidiClass;
        public int offsetToSimpleUppercase;
        public int offsetToSimpleLowercase;
        public int offsetToSimpleTitlecase;
        public int offsetToSimpleCaseFold;
        public bool isWhitespace;
    }
}
