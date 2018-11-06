// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace System
{
    [StackTraceHidden]
    internal static class ArgValidation
    {
        /// <summary>
        /// Calls <see cref="ThrowHelper.ThrowArgumentOutOfRangeException"/> if the input arguments
        /// <paramref name="desiredStart"/> and <paramref name="desiredLength"/> aren't within the range
        /// of <paramref name="actualLength"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void ThrowIfArgsOutOfRangeForSlice(int desiredStart, int desiredLength, int actualLength)
        {
            Debug.Assert(actualLength >= 0);

#if BIT64
            // See comment on over overload of ThrowIfArgsOutOfRangeForSlice.
            if ((ulong)(uint)desiredStart + (ulong)(uint)desiredLength > (ulong)(uint)actualLength)
                ThrowHelper.ThrowArgumentOutOfRangeException();
#else
            if ((uint)desiredStart > (uint)actualLength || (uint)desiredLength > (uint)(actualLength - desiredStart))
                ThrowHelper.ThrowArgumentOutOfRangeException();
#endif
        }

        /// <summary>
        /// Calls <see cref="ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument)"/> if the input arguments
        /// <paramref name="desiredStart"/> and <paramref name="desiredLength"/> aren't within the range
        /// of <paramref name="actualLength"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void ThrowIfArgsOutOfRangeForSlice(int desiredStart, int desiredLength, int actualLength, ExceptionArgument argument)
        {
            Debug.Assert(actualLength >= 0);

#if BIT64
            // Since start and length are both 32-bit, their sum can be computed across a 64-bit domain
            // without loss of fidelity. The cast to uint before the cast to ulong ensures that the
            // extension from 32- to 64-bit is zero-extending rather than sign-extending. The end result
            // of this is that if either input is negative or if the input sum overflows past Int32.MaxValue,
            // that information is captured correctly in the comparison against the backing _length field.
            // We don't use this same mechanism in a 32-bit process due to the overhead of 64-bit arithmetic.
            if ((ulong)(uint)desiredStart + (ulong)(uint)desiredLength > (ulong)(uint)actualLength)
                ThrowHelper.ThrowArgumentOutOfRangeException(argument);
#else
            if ((uint)desiredStart > (uint)actualLength || (uint)desiredLength > (uint)(actualLength - desiredStart))
                ThrowHelper.ThrowArgumentOutOfRangeException(argument);
#endif
        }
    }
}
