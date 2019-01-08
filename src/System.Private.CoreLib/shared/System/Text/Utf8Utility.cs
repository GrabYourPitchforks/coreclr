// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.CompilerServices;
using System.Diagnostics;
using Internal.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System.Text
{
    internal static partial class Utf8Utility
    {
        private const int SCALAR_INVALID = -1;
        private const int SCALAR_INCOMPLETE = -2;

        internal static int ReadFirstScalarFromBuffer(ReadOnlySpan<byte> utf8Input, out int bytesConsumed)
        {
            if (utf8Input.IsEmpty)
            {
                goto Error;
            }

            // First, check for ASCII.

            int firstByte = utf8Input[0];
            if ((byte)firstByte <= 0x7Fu)
            {
                bytesConsumed = 1;
                return firstByte;
            }

            // Then, check for the 2-byte header.

            firstByte -= 0xC2; // sub this now since we're going to use this in many future comparisons

            if ((byte)firstByte <= 0xDFu - 0xC2u)
            {
                // Found a 2-byte marker, but need to confirm it's valid.
                // [ 110yyyyy 10xxxxxx ]

                if (utf8Input.Length < 2)
                {
                    goto Error;
                }

                int secondByte = (sbyte)utf8Input[1]; // signed so that we can perform a simple check for a continuation byte
                if (secondByte > unchecked((sbyte)0xBF))
                {
                    goto Error;
                }

                // Valid!

                bytesConsumed = 2;

                // Move the header bits we mutated earlier back into position, then reverse the 0x80 sign extension on the 2nd byte
                return (firstByte << 6) + secondByte - ((0xC0 - 0xC2) << 6) - unchecked((sbyte)0x80);
            }

            // Then, check for the 3-byte header.

            if ((byte)firstByte <= 0xEFu - 0xC2u)
            {
                // Found a 3-byte marker, but need to confirm it's valid.
                // [ 1110zzzz 10yyyyyy 10xxxxxx ]

                if (utf8Input.Length < 3)
                {
                    goto Error;
                }

                int secondByte = (sbyte)utf8Input[1]; // signed so that we can perform a simple check for a continuation byte
                if (secondByte > unchecked((sbyte)0xBF))
                {
                    goto Error;
                }

                // perform overlong check now

                int firstAndSecondBytes = (firstByte << 6) + secondByte;
                if ((uint)firstAndSecondBytes < unchecked((uint)((0xE0 - 0xC2) << 6 + (sbyte)0xA0)))
                {
                    goto Error;
                }

                // perform surrogate check now

                if (UnicodeUtility.IsInRangeInclusive((uint)firstAndSecondBytes, unchecked((uint)((0xED - 0xC2) << 6 + (sbyte)0x9F)), unchecked((uint)((0xED - 0xC2) << 6 + (sbyte)0xBF))))
                {
                    goto Error;
                }

                int thirdByte = (sbyte)utf8Input[2]; // signed so that we can perform a simple check for a continuation byte
                if (thirdByte > unchecked((sbyte)0xBF))
                {
                    goto Error;
                }

                // Valid!

                bytesConsumed = 3;

                // Move the header bits we mutated earlier back into position, then reverse the 0x80 sign extension on the trailing bytes
                return (firstByte << 6) + secondByte - ((0xE0 - 0xC2) << 12) - (unchecked((sbyte)0x80) << 6) - unchecked((sbyte)0x80);
            }

            // Then, check for the 4-byte header.
            // If this doesn't work, fall through to error handling code path.

            if ((byte)firstByte <= 0xF4u - 0xC2u)
            {
                // Found a 4-byte marker, but need to confirm it's valid.
                // [ 11110uuu 10uuzzzz 10yyyyyy 10xxxxxx ]

                if (utf8Input.Length < 4)
                {
                    goto Error;
                }

                int secondByte = (sbyte)utf8Input[1]; // signed so that we can perform a simple check for a continuation byte
                if (secondByte > unchecked((sbyte)0xBF))
                {
                    goto Error;
                }

                // perform overlong and out-of-range check now

                int firstAndSecondBytes = (firstByte << 6) + secondByte;
                if (!UnicodeUtility.IsInRangeInclusive((uint)firstAndSecondBytes, unchecked((uint)((0xF0 - 0xC2) << 6 + (sbyte)0x90)), unchecked((uint)((0xF4 - 0xC2) << 6 + (sbyte)0x8F))))
                {
                    goto Error;
                }

                int thirdByte = (sbyte)utf8Input[2]; // signed so that we can perform a simple check for a continuation byte
                if (thirdByte > unchecked((sbyte)0xBF))
                {
                    goto Error;
                }

                int fourthByte = (sbyte)utf8Input[3]; // signed so that we can perform a simple check for a continuation byte
                if (fourthByte > unchecked((sbyte)0xBF))
                {
                    goto Error;
                }

                // Valid!

                bytesConsumed = 4;

                // Move the header bits we mutated earlier back into position, then reverse the 0x80 sign extension on the trailing bytes
                int thirdAndFourthBytes = thirdByte << 6 + fourthByte;
                return (firstAndSecondBytes << 12) + thirdAndFourthBytes - ((0xF0 - 0xC2) << 18) - (unchecked((sbyte)0x80) << 12) - (unchecked((sbyte)0x80) << 6) - unchecked((sbyte)0x80);
            }

        Error:
            return ReadFirstScalarFromBuffer_ErrorHandler(utf8Input, out bytesConsumed);
        }

        private static int ReadFirstScalarFromBuffer_ErrorHandler(ReadOnlySpan<byte> utf8Input, out int bytesConsumed)
        {
            // This is the error handling logic for when we can't read a scalar value from the input because the
            // input does not represent valid UTF-8. It's ok for this logic to be slow since this is an error handling
            // code path; the caller should be optimized for well-formed input.
            //
            // There's a race condition here in that it's possible that the UTF-8 was ill-formed when the fast-path method
            // inspected the data, but between that method running and this error handling method running some other thread
            // may have updated the buffer to contain well-formed data. We're not too worried about detecting this situation
            // since our API contracts require the buffers to be stable, so we'll just return any arbitrary error.
            //
            // See the following references for how we compute the number of bytes consumed given ill-formed input data.
            // - The Unicode Specification, Chapter 3, Clause C10 and Sec. 3.9
            //   (and specifically the heading "U+FFFD Substitution of Maximal Subparts")
            // - The Unicode Specification, Chapter 5, Sec. 5.22
            // - Unicode Technical Report #36 - Unicode Security Considerations, Sec. 3.1

            if (utf8Input.IsEmpty)
            {
                bytesConsumed = 0;
                return SCALAR_INCOMPLETE;
            }

            uint firstByte = utf8Input[0];

            if (firstByte <= 0xC1u)
            {
                // 80..C1 will never begin a valid UTF-8 sequence.

                bytesConsumed = 1;
                return SCALAR_INVALID;
            }

            if (firstByte <= 0xDFu)
            {
                // 2-byte sequence marker

                if (utf8Input.Length < 2)
                {
                    bytesConsumed = 1;
                    return SCALAR_INCOMPLETE;
                }

                // only alternative was that the second byte wasn't a valid continuation byte
                // that's a maximally invalid subsequence of length 1

                bytesConsumed = 1;
                return SCALAR_INVALID;
            }

            if (firstByte <= 0xEFu)
            {
                // 3-byte sequence marker

                if (utf8Input.Length < 2)
                {
                    bytesConsumed = 1;
                    return SCALAR_INCOMPLETE;
                }

                if (firstByte == 0xE0u)
                {
                    if (!UnicodeUtility.IsInRangeInclusive(utf8Input[1], 0xA0u, 0xBFu))
                    {
                        // 3-byte sequence marker not followed by a valid continuation byte (or overlong)
                        // maximally invalid subsequence of length 1
                        bytesConsumed = 1;
                        return SCALAR_INVALID;
                    }
                }
                else if (UnicodeUtility.IsInRangeInclusive(firstByte, 0xE1u, 0xECu))
                {
                    if (!UnicodeUtility.IsInRangeInclusive(utf8Input[1], 0x80u, 0xBFu))
                    {
                        // 3-byte sequence marker not followed by a valid continuation byte
                        // maximally invalid subsequence of length 1
                        bytesConsumed = 1;
                        return SCALAR_INVALID;
                    }
                }
                else if (firstByte == 0xEDu)
                {
                    if (!UnicodeUtility.IsInRangeInclusive(utf8Input[1], 0x80u, 0x9Fu))
                    {
                        // 3-byte sequence marker not followed by a valid continuation byte (or surrogate)
                        // maximally invalid subsequence of length 1
                        bytesConsumed = 1;
                        return SCALAR_INVALID;
                    }
                }
                else
                {
                    if (!UnicodeUtility.IsInRangeInclusive(utf8Input[1], 0x80u, 0xBFu))
                    {
                        // 3-byte sequence marker not followed by a valid continuation byte
                        // maximally invalid subsequence of length 1
                        bytesConsumed = 1;
                        return SCALAR_INVALID;
                    }
                }

                // If we reached this point, the first two bytes are a valid subsequence.
                // Only need to check now that the third (final) byte exists and is a continuation byte.

                if (utf8Input.Length < 3)
                {
                    bytesConsumed = 2;
                    return SCALAR_INCOMPLETE;
                }

                // only alternative was that the third byte wasn't a valid continuation byte
                // that's a maximally invalid subsequence of length 2

                bytesConsumed = 2;
                return SCALAR_INVALID;
            }

            if (firstByte <= 0xF4u)
            {
                // 3-byte sequence marker

                if (utf8Input.Length < 2)
                {
                    bytesConsumed = 1;
                    return SCALAR_INCOMPLETE;
                }

                if (firstByte == 0xF0u)
                {
                    if (!UnicodeUtility.IsInRangeInclusive(utf8Input[1], 0x90u, 0xBFu))
                    {
                        // 4-byte sequence marker not followed by a valid continuation byte (or overlong)
                        // maximally invalid subsequence of length 1
                        bytesConsumed = 1;
                        return SCALAR_INVALID;
                    }
                }
                else if (UnicodeUtility.IsInRangeInclusive(firstByte, 0xF1u, 0xF3u))
                {
                    if (!UnicodeUtility.IsInRangeInclusive(utf8Input[1], 0x80u, 0xBFu))
                    {
                        // 4-byte sequence marker not followed by a valid continuation byte
                        // maximally invalid subsequence of length 1
                        bytesConsumed = 1;
                        return SCALAR_INVALID;
                    }
                }
                else
                {
                    if (!UnicodeUtility.IsInRangeInclusive(utf8Input[1], 0x80u, 0x8Fu))
                    {
                        // 4-byte sequence marker not followed by a valid continuation byte (or out-of-range)
                        // maximally invalid subsequence of length 1
                        bytesConsumed = 1;
                        return SCALAR_INVALID;
                    }
                }

                // If we reached this point, the first two bytes are a valid subsequence.
                // Only need to check now that the third and fourth bytes exists and are continuation bytes.

                if (utf8Input.Length < 3)
                {
                    bytesConsumed = 2;
                    return SCALAR_INCOMPLETE;
                }

                if (!UnicodeUtility.IsInRangeInclusive(utf8Input[2], 0x80u, 0xBFu))
                {
                    // 4-byte sequence marker with two valid starting bytes and a non-continuation third byte
                    // maximally invalid subsequence of length 2
                    bytesConsumed = 2;
                    return SCALAR_INVALID;
                }

                // only alternative was that the fourth byte wasn't a valid continuation byte
                // that's a maximally invalid subsequence of length 3

                bytesConsumed = 3;
                return SCALAR_INVALID;
            }

            // If we got to this point, the first byte of the sequence was F5..FF, which is never valid UTF-8.

            bytesConsumed = 1;
            return SCALAR_INVALID;
        }
    }
}
