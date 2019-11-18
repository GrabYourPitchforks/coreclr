// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;

namespace GenUnicodeProp
{
    // Corresponds to https://www.unicode.org/reports/tr29/#Grapheme_Cluster_Break_Property_Values
    internal enum GraphemeBoundaryCategory
    {
        Unassigned,
        CR,
        LF,
        Control,
        Extend,
        ZWJ,
        Regional_Indicator,
        Prepend,
        SpacingMark,
        L,
        V,
        T,
        LV,
        LVT,
        Extended_Pictographic,
    }
}
