// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.CompilerServices;

namespace System.Globalization
{
    // We don't preserve the entire bidi class information for the code points in our data
    // tables because our internal consumers don't require information with that fine a
    // granularity. Instead, our consumers only care about elements with a strong LTR
    // or RTL ordering, so that's all we expose. n.b. this type is [Flags] and should be
    // compared using a bitmask rather than using explicit equality.

    [Flags]
    internal enum RestrictedBidiClass
    {
        /// <summary>
        /// This code point has a bidi class other than "L", "AL", or "R".
        /// </summary>
        Other = 0,

        /// <summary>
        /// This code point has strong left-to-right ordering (bidi class "L").
        /// </summary>
        LeftToRight = 1 << 5,

        /// <summary>
        /// This code point has strong right-to-left ordering (bidi class "AL" or "R").
        /// </summary>
        RightToLeft = 2 << 5
    }

    internal static class BidiClassHelpers
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsLeftToRight(this RestrictedBidiClass value) => (value & RestrictedBidiClass.LeftToRight) != 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsRightToLeft(this RestrictedBidiClass value) => (value & RestrictedBidiClass.RightToLeft) != 0;
    }
}
