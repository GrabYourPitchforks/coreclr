// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace System
{
    [StackTraceHidden]
    internal static class ArgValidationHelper
    {
        /// <summary>
        /// Validates that <paramref name="desiredStartIndex"/> and <paramref name="desiredLength"/> represent a
        /// valid slice within a collection of length <paramref name="actualLength"/>.
        /// Throws <see cref="ArgumentOutOfRangeException"/> with no parameter name if the check fails.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ValidateStartAndLengthNotOutOfRange(int desiredStartIndex, int desiredLength, int actualLength)
        {
#if BIT64
            // Since start and length are both 32-bit, their sum can be computed across a 64-bit domain
            // without loss of fidelity. The cast to uint before the cast to ulong ensures that the
            // extension from 32- to 64-bit is zero-extending rather than sign-extending. The end result
            // of this is that if either input is negative or if the input sum overflows past Int32.MaxValue,
            // that information is captured correctly in the comparison against the backing _length field.
            // We don't use this same mechanism in a 32-bit process due to the overhead of 64-bit arithmetic.
            if ((ulong)(uint)desiredStartIndex + (ulong)(uint)desiredLength > (ulong)(uint)actualLength)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException();
            }
#else
            if ((uint)desiredStartIndex > (uint)actualLength || (uint)desiredLength > (uint)(actualLength - desiredStartIndex))
            {
                ThrowHelper.ThrowArgumentOutOfRangeException();
            }
#endif
        }

        /// <summary>
        /// Validates that <paramref name="desiredStartIndex"/> and <paramref name="desiredLength"/> represent a
        /// valid slice within a collection of length <paramref name="actualLength"/>.
        /// Throws <see cref="ArgumentOutOfRangeException"/> with a parameter name of <paramref name="argument"/>
        /// if the check fails.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ValidateStartAndLengthNotOutOfRange(int desiredStartIndex, int desiredLength, int actualLength, ExceptionArgument argument)
        {
#if BIT64
            if ((ulong)(uint)desiredStartIndex + (ulong)(uint)desiredLength > (ulong)(uint)actualLength)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(argument);
            }
#else
            if ((uint)desiredStartIndex > (uint)actualLength || (uint)desiredLength > (uint)(actualLength - desiredStartIndex))
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(argument);
            }
#endif
        }
    }
}
