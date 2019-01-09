// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using Internal.Runtime.CompilerServices;
using System.Numerics;
using System.Diagnostics;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Runtime.InteropServices;

#if BIT64
using nint = System.Int64;
using nuint = System.UInt64;
#else // BIT64
using nint = System.Int32;
using nuint = System.UInt32;
#endif // BIT64

namespace System.Text
{
    internal static partial class Utf8Utility
    {
        private static unsafe ref byte GetRefToFirstInvalidUtf8Sequence(ref byte inputBuffer, int inputLength, out int utf16CodeUnitCountAdjustment, out int scalarCountAdjustment)
        {
            // The fields below control where we read from the buffer.

            int tempUtf16CodeUnitCountAdjustment = 0;
            int tempScalarCountAdjustment = 0;

            if (inputLength < sizeof(uint))
            {
                goto ProcessInputOfLessThanDWordSize;
            }

            // The subtraction below is "safe" from the GC's perspective because if 'inputBuffer' references a managed object, it will always be
            // an interior pointer, which means that we still have some wiggle room to back up a bit and still be within the range of the
            // original object reference.

            ref byte finalPosWhereCanReadDWordFromInputBuffer = ref Unsafe.Add(ref Unsafe.Add(ref inputBuffer, inputLength), -sizeof(uint));

            // If the sequence is long enough, try running vectorized "is this sequence ASCII?"
            // logic. We perform a small test of the first few bytes to make sure they're all
            // ASCII before we incur the cost of invoking the vectorized code path.

            if (Vector.IsHardwareAccelerated)
            {
                if (IntPtr.Size >= 8)
                {
                    // Test first 16 bytes and check for all-ASCII.
                    if ((inputLength >= 2 * sizeof(ulong) + 3 * Vector<byte>.Count) && QWordAllBytesAreAscii(ReadAndFoldTwoQWordsUnaligned(ref inputBuffer)))
                    {
                        inputBuffer = ref Unsafe.AddByteOffset(ref inputBuffer, ConsumeAsciiBytesVectorized(ref Unsafe.Add(ref inputBuffer, 2 * sizeof(ulong)), inputLength - 2 * sizeof(ulong)) + 2 * sizeof(ulong));
                    }
                }
                else
                {
                    // Test first 8 bytes and check for all-ASCII.
                    if ((inputLength >= 2 * sizeof(uint) + 3 * Vector<byte>.Count) && DWordAllBytesAreAscii(ReadAndFoldTwoDWordsUnaligned(ref inputBuffer)))
                    {
                        inputBuffer = ref Unsafe.AddByteOffset(ref inputBuffer, ConsumeAsciiBytesVectorized(ref Unsafe.Add(ref inputBuffer, 2 * sizeof(uint)), inputLength - 2 * sizeof(uint)) + 2 * sizeof(uint));
                    }
                }
            }

            // Begin the main loop.

#if DEBUG
            ref byte lastBufferPosProcessed = ref Unsafe.AsRef<byte>(null); // used for invariant checking in debug builds
#endif

            while (!Unsafe.IsAddressLessThan(ref finalPosWhereCanReadDWordFromInputBuffer, ref inputBuffer))
            {
                // Read 32 bits at a time. This is enough to hold any possible UTF8-encoded scalar.

                uint thisDWord = Unsafe.ReadUnaligned<uint>(ref inputBuffer);

            AfterReadDWord:

#if DEBUG
                Debug.Assert(Unsafe.IsAddressLessThan(ref lastBufferPosProcessed, ref inputBuffer), "Algorithm should've made forward progress since last read.");
                lastBufferPosProcessed = ref inputBuffer;
#endif

                // First, check for the common case of all-ASCII bytes.

                if (DWordAllBytesAreAscii(thisDWord))
                {
                    // We read an all-ASCII sequence.

                    inputBuffer = ref Unsafe.Add(ref inputBuffer, sizeof(uint));

                    // If we saw a sequence of all ASCII, there's a good chance a significant amount of following data is also ASCII.
                    // Below is basically unrolled loops with poor man's vectorization.

                    // Below check is "can I read at least five DWORDs from the input stream?"
                    if ((int)(void*)Unsafe.ByteOffset(ref inputBuffer, ref finalPosWhereCanReadDWordFromInputBuffer) >= 4 * sizeof(uint))
                    {
                        // The JIT produces better codegen for aligned reads than it does for
                        // unaligned reads, and we want the processor to operate at maximum
                        // efficiency in the loop that follows, so we'll align the references
                        // now. It's OK to do this without pinning because the GC will never
                        // move a heap-allocated object in a manner that messes with its
                        // alignment.

                        {
                            thisDWord = Unsafe.ReadUnaligned<uint>(ref inputBuffer);
                            if (!DWordAllBytesAreAscii(thisDWord))
                            {
                                goto AfterReadDWordSkipAllBytesAsciiCheck;
                            }
                            inputBuffer = ref Unsafe.Add(ref inputBuffer, (IntPtr)GetNumberOfBytesToNextDWordAlignment(ref inputBuffer));
                        }

                        // At this point, the input buffer offset points to an aligned DWORD.
                        // We also know that there's enough room to read at least four DWORDs from the stream.

                        ref byte inputBufferFinalRefAtWhichCanSafelyLoop = ref Unsafe.Add(ref finalPosWhereCanReadDWordFromInputBuffer, -3 * sizeof(uint));
                        do
                        {
                            ref uint currentReadPosition = ref Unsafe.As<byte, uint>(ref inputBuffer);

                            if (!DWordAllBytesAreAscii(currentReadPosition | Unsafe.Add(ref currentReadPosition, 1)))
                            {
                                goto LoopTerminatedEarlyDueToNonAsciiData;
                            }

                            if (!DWordAllBytesAreAscii(Unsafe.Add(ref currentReadPosition, 2) | Unsafe.Add(ref currentReadPosition, 3)))
                            {
                                inputBuffer = ref Unsafe.Add(ref inputBuffer, 2 * sizeof(uint));
                                goto LoopTerminatedEarlyDueToNonAsciiData;
                            }

                            inputBuffer = ref Unsafe.Add(ref inputBuffer, 4 * sizeof(uint));
                        } while (!Unsafe.IsAddressLessThan(ref inputBufferFinalRefAtWhichCanSafelyLoop, ref inputBuffer));

                        continue; // need to perform a bounds check because we might be running out of data

                    LoopTerminatedEarlyDueToNonAsciiData:

                        // We know that there's *at least* two DWORDs of data remaining in the buffer.
                        // We also know that one of them (or both of them) contains non-ASCII data somewhere.
                        // Let's perform a quick check here to bypass the logic at the beginning of the main loop.

                        thisDWord = Unsafe.As<byte, uint>(ref inputBuffer);
                        if (DWordAllBytesAreAscii(thisDWord))
                        {
                            inputBuffer = ref Unsafe.Add(ref inputBuffer, sizeof(uint));
                            thisDWord = Unsafe.As<byte, uint>(ref inputBuffer);
                        }

                        goto AfterReadDWordSkipAllBytesAsciiCheck;
                    }

                    continue;
                }

            AfterReadDWordSkipAllBytesAsciiCheck:

                Debug.Assert(!DWordAllBytesAreAscii(thisDWord)); // this should have been handled earlier

                // Next, try stripping off ASCII bytes one at a time.
                // We only handle up to three ASCII bytes here since we handled the four ASCII byte case above.

                {
                    uint numLeadingAsciiBytes = CountNumberOfLeadingAsciiBytesFrom24BitInteger(thisDWord);
                    inputBuffer = ref Unsafe.Add(ref inputBuffer, (int)numLeadingAsciiBytes);

                    if (Unsafe.IsAddressLessThan(ref finalPosWhereCanReadDWordFromInputBuffer, ref inputBuffer))
                    {
                        goto ProcessRemainingBytesSlow; // Input buffer doesn't contain enough data to read a DWORD
                    }
                    else
                    {
                        // The input buffer at the current offset contains a non-ASCII byte.
                        // Read an entire DWORD and fall through to multi-byte consumption logic.
                        thisDWord = Unsafe.ReadUnaligned<uint>(ref inputBuffer);
                    }
                }

            // At this point, we know we're working with a multi-byte code unit,
            // but we haven't yet validated it.

            // The masks and comparands are derived from the Unicode Standard, Table 3-6.
            // Additionally, we need to check for valid byte sequences per Table 3-7.

            // Check the 2-byte case.

            BeforeProcessTwoByteSequence:

                if (DWordBeginsWithUtf8TwoByteMask(thisDWord))
                {
                    // Per Table 3-7, valid sequences are:
                    // [ C2..DF ] [ 80..BF ]

                    if (DWordBeginsWithOverlongUtf8TwoByteSequence(thisDWord)) { goto Error; }

                ProcessTwoByteSequenceSkipOverlongFormCheck:

                    // Optimization: If this is a two-byte-per-character language like Cyrillic or Hebrew,
                    // there's a good chance that if we see one two-byte run then there's another two-byte
                    // run immediately after. Let's check that now.

                    // On little-endian platforms, we can check for the two-byte UTF8 mask *and* validate that
                    // the value isn't overlong using a single comparison. On big-endian platforms, we'll need
                    // to validate the mask and validate that the sequence isn't overlong as two separate comparisons.

                    if ((BitConverter.IsLittleEndian && DWordEndsWithValidUtf8TwoByteSequenceLittleEndian(thisDWord))
                        || (!BitConverter.IsLittleEndian && (DWordEndsWithUtf8TwoByteMask(thisDWord) && !DWordEndsWithOverlongUtf8TwoByteSequence(thisDWord))))
                    {
                    ConsumeTwoAdjacentKnownGoodTwoByteSequences:

                        // We have two runs of two bytes each.
                        inputBuffer = ref Unsafe.Add(ref inputBuffer, 4);
                        tempUtf16CodeUnitCountAdjustment -= 2; // 4 UTF-8 code units -> 2 UTF-16 code units (and 2 scalars)

                        if (!Unsafe.IsAddressLessThan(ref finalPosWhereCanReadDWordFromInputBuffer, ref inputBuffer))
                        {
                            // Optimization: If we read a long run of two-byte sequences, the next sequence is probably
                            // also two bytes. Check for that first before going back to the beginning of the loop.

                            thisDWord = Unsafe.ReadUnaligned<uint>(ref inputBuffer);

                            if (BitConverter.IsLittleEndian)
                            {
                                if (DWordBeginsWithValidUtf8TwoByteSequenceLittleEndian(thisDWord))
                                {
                                    // The next sequence is a valid two-byte sequence.
                                    goto ProcessTwoByteSequenceSkipOverlongFormCheck;
                                }
                            }
                            else
                            {
                                if (DWordBeginsAndEndsWithUtf8TwoByteMask(thisDWord))
                                {
                                    if (DWordBeginsWithOverlongUtf8TwoByteSequence(thisDWord) || DWordEndsWithOverlongUtf8TwoByteSequence(thisDWord))
                                    {
                                        // Mask said it was 2x 2-byte sequences but validation failed, go to beginning of loop for error handling
                                        goto AfterReadDWord;
                                    }
                                    else
                                    {
                                        // Validated next bytes are 2x 2-byte sequences
                                        goto ConsumeTwoAdjacentKnownGoodTwoByteSequences;
                                    }
                                }
                                else if (DWordBeginsWithUtf8TwoByteMask(thisDWord))
                                {
                                    if (DWordBeginsWithOverlongUtf8TwoByteSequence(thisDWord))
                                    {
                                        // Mask said it was a 2-byte sequence but validation failed
                                        goto Error;
                                    }
                                    else
                                    {
                                        // Validated next bytes are a single 2-byte sequence with no valid 2-byte sequence following
                                        goto ConsumeSingleKnownGoodTwoByteSequence;
                                    }
                                }
                            }

                            // If we reached this point, the next sequence is something other than a valid
                            // two-byte sequence, so go back to the beginning of the loop.
                            goto AfterReadDWord;
                        }
                        else
                        {
                            goto ProcessRemainingBytesSlow; // Running out of data - go down slow path
                        }
                    }

                ConsumeSingleKnownGoodTwoByteSequence:

                    // The buffer contains a 2-byte sequence followed by 2 bytes that aren't a 2-byte sequence.
                    // Unlikely that a 3-byte sequence would follow a 2-byte sequence, so perhaps remaining
                    // bytes are ASCII?

                    tempUtf16CodeUnitCountAdjustment--; // 2-byte sequence + (some number of ASCII bytes) -> 1 UTF-16 code units (and 1 scalar) [+ trailing]

                    if (DWordThirdByteIsAscii(thisDWord))
                    {
                        if (DWordFourthByteIsAscii(thisDWord))
                        {
                            inputBuffer = ref Unsafe.Add(ref inputBuffer, 4);
                        }
                        else
                        {
                            inputBuffer = ref Unsafe.Add(ref inputBuffer, 3);

                            // A two-byte sequence followed by an ASCII byte followed by a non-ASCII byte.
                            // Read in the next DWORD and jump directly to the start of the multi-byte processing block.

                            if (!Unsafe.IsAddressLessThan(ref finalPosWhereCanReadDWordFromInputBuffer, ref inputBuffer))
                            {
                                thisDWord = Unsafe.ReadUnaligned<uint>(ref inputBuffer);
                                goto BeforeProcessTwoByteSequence;
                            }
                        }
                    }
                    else
                    {
                        inputBuffer = ref Unsafe.Add(ref inputBuffer, 2);
                    }

                    continue;
                }

                // Check the 3-byte case.

                if (DWordBeginsWithUtf8ThreeByteMask(thisDWord))
                {
                ProcessThreeByteSequenceWithCheck:

                    // We need to check for overlong or surrogate three-byte sequences.
                    //
                    // Per Table 3-7, valid sequences are:
                    // [   E0   ] [ A0..BF ] [ 80..BF ]
                    // [ E1..EC ] [ 80..BF ] [ 80..BF ]
                    // [   ED   ] [ 80..9F ] [ 80..BF ]
                    // [ EE..EF ] [ 80..BF ] [ 80..BF ]
                    //
                    // Big-endian examples of using the above validation table:
                    // E0A0 = 1110 0000 1010 0000 => invalid (overlong ) patterns are 1110 0000 100# ####
                    // ED9F = 1110 1101 1001 1111 => invalid (surrogate) patterns are 1110 1101 101# ####
                    // If using the bitmask ......................................... 0000 1111 0010 0000 (=0F20),
                    // Then invalid (overlong) patterns match the comparand ......... 0000 0000 0000 0000 (=0000),
                    // And invalid (surrogate) patterns match the comparand ......... 0000 1101 0010 0000 (=0D20).

                    if (BitConverter.IsLittleEndian)
                    {
                        // The "overlong or surrogate" check can be implemented using a single jump, but there's
                        // some overhead to moving the bits into the correct locations in order to perform the
                        // correct comparison, and in practice the processor's branch prediction capability is
                        // good enough that we shouldn't bother. So we'll use two jumps instead.

                        // Can't extract this check into its own helper method because JITter produces suboptimal
                        // assembly, even with aggressive inlining.

                        // Code below becomes 5 instructions: test, jz, lea, test, jz

                        if ((thisDWord & 0x0000200Fu) == 0) { goto Error; } // overlong
                        if (((thisDWord - 0x0000200Du) & 0x0000200Fu) == 0) { goto Error; } // surrogate
                    }
                    else
                    {
                        if ((thisDWord & 0x0F200000u) == 0) { goto Error; } // overlong
                        if (((thisDWord - 0x0D200000u) & 0x0F200000u) == 0) { goto Error; } // surrogate
                    }

                ProcessSingleThreeByteSequenceSkipOverlongAndSurrogateChecks:

                    // Occasionally one-off ASCII characters like spaces, periods, or newlines will make their way
                    // in to the text. If this happens strip it off now before seeing if the next character
                    // consists of three code units.

                    // Branchless: consume a 3-byte UTF-8 sequence and optionally an extra ASCII byte hanging off the end

                    nint asciiAdjustment;
                    if (BitConverter.IsLittleEndian)
                    {
                        asciiAdjustment = (int)thisDWord >> 31; // smear most significant bit across entire value
                    }
                    else
                    {
                        asciiAdjustment = (nint)(sbyte)thisDWord >> 7; // smear most significant bit of least significant byte across entire value
                    }

                    // asciiAdjustment = -1 if fourth byte is ASCII; 0 otherwise

                    // The two lines below *MUST NOT* be reordered. It's valid to add 4 before backing up since we
                    // already checked previously that the input buffer contains at least a DWORD's worth of data,
                    // so we're not going to run past the end of the buffer where the GC can no longer track the
                    // reference. However, we can't back up before adding 4, since we might back up to before the
                    // start of the buffer, and the GC isn't guaranteed to be able to track this.

                    inputBuffer = ref Unsafe.Add(ref inputBuffer, 4); // optimisitcally, consumed a 3-byte UTF-8 sequence plus an extra ASCII byte
                    inputBuffer = ref Unsafe.Add(ref inputBuffer, (IntPtr)asciiAdjustment); // back up if we didn't actually consume an ASCII byte

                    tempUtf16CodeUnitCountAdjustment -= 2; // 3 (or 4) UTF-8 bytes -> 1 (or 2) UTF-16 code unit (and 1 [or 2] scalar)

                SuccessfullyProcessedThreeByteSequence:

                    // Optimization: A three-byte character could indicate CJK text, which makes it likely
                    // that the character following this one is also CJK. We'll try to process several
                    // three-byte sequences at a time.

                    // The check below is really "can we read 9 bytes from the input buffer?" since 'finalPos...' is already offset
                    if (IntPtr.Size >= 8 && BitConverter.IsLittleEndian && (nuint)(void*)Unsafe.ByteOffset(ref inputBuffer, ref finalPosWhereCanReadDWordFromInputBuffer) >= 5)
                    {
                        ulong thisQWord = Unsafe.ReadUnaligned<ulong>(ref inputBuffer);

                        // Is this three 3-byte sequences in a row?
                        // thisQWord = [ 10yyyyyy 1110zzzz | 10xxxxxx 10yyyyyy 1110zzzz | 10xxxxxx 10yyyyyy 1110zzzz ] [ 10xxxxxx ]
                        //               ---- CHAR 3  ----   --------- CHAR 2 ---------   --------- CHAR 1 ---------     -CHAR 3-
                        if ((thisQWord & 0xC0F0C0C0F0C0C0F0UL) == 0x80E08080E08080E0UL && UnicodeHelpers.IsUtf8ContinuationByte(in Unsafe.Add(ref inputBuffer, 8)))
                        {
                            // Saw a proper bitmask for three incoming 3-byte sequences, perform the
                            // overlong and surrogate sequence checking now.

                            // Check the first character.
                            // If the first character is overlong or a surrogate, fail immediately.

                            if (((uint)thisQWord & 0x200FU) == 0) { goto Error; }
                            if ((((uint)thisQWord - 0x200DU) & 0x200FU) == 0) { goto Error; }

                            // Check the second character.
                            // If this character is overlong or a surrogate, process the first character (which we
                            // know to be good because the first check passed) before reporting an error.

                            thisDWord = (uint)thisQWord;

                            thisQWord >>= 24;
                            if (((uint)thisQWord & 0x200FU) == 0) { goto ProcessSingleThreeByteSequenceSkipOverlongAndSurrogateChecks; }
                            if ((((uint)thisQWord - 0x200DU) & 0x200FU) == 0) { goto ProcessSingleThreeByteSequenceSkipOverlongAndSurrogateChecks; }

                            // Check the third character (we already checked that it's followed by a continuation byte).
                            // If this character is overlong or a surrogate, process the first character (which we
                            // know to be good because the first check passed) before reporting an error.

                            thisQWord >>= 24;
                            if (((uint)thisQWord & 0x200FU) == 0) { goto ProcessSingleThreeByteSequenceSkipOverlongAndSurrogateChecks; }
                            if ((((uint)thisQWord - 0x200DU) & 0x200FU) == 0) { goto ProcessSingleThreeByteSequenceSkipOverlongAndSurrogateChecks; }

                            inputBuffer = ref Unsafe.Add(ref inputBuffer, 9);
                            tempUtf16CodeUnitCountAdjustment -= 6; // 9 UTF-8 bytes -> 3 UTF-16 code units (and 3 scalars)

                            goto SuccessfullyProcessedThreeByteSequence;
                        }

                        // Is this two 3-byte sequences in a row?
                        // thisQWord = [ ######## ######## | 10xxxxxx 10yyyyyy 1110zzzz | 10xxxxxx 10yyyyyy 1110zzzz ]
                        //                                   --------- CHAR 2 ---------   --------- CHAR 1 ---------
                        if ((thisQWord & 0xC0C0F0C0C0F0UL) == 0x8080E08080E0UL)
                        {
                            // Saw a proper bitmask for two incoming 3-byte sequences, perform the
                            // overlong and surrogate sequence checking now.

                            // Check the first character.
                            // If the first character is overlong or a surrogate, fail immediately.

                            if (((uint)thisQWord & 0x200FU) == 0) { goto Error; }
                            if ((((uint)thisQWord - 0x200DU) & 0x200FU) == 0) { goto Error; }

                            // Check the second character.
                            // If this character is overlong or a surrogate, process the first character (which we
                            // know to be good because the first check passed) before reporting an error.

                            thisDWord = (uint)thisQWord;

                            thisQWord >>= 24;
                            if (((uint)thisQWord & 0x200FU) == 0) { goto ProcessSingleThreeByteSequenceSkipOverlongAndSurrogateChecks; }
                            if ((((uint)thisQWord - 0x200DU) & 0x200FU) == 0) { goto ProcessSingleThreeByteSequenceSkipOverlongAndSurrogateChecks; }

                            inputBuffer = ref Unsafe.Add(ref inputBuffer, 6);
                            tempUtf16CodeUnitCountAdjustment -= 4; // 6 UTF-8 bytes -> 2 UTF-16 code units (and 2 scalars)

                            // The next char in the sequence didn't have a 3-byte marker, so it's probably
                            // an ASCII character. Jump back to the beginning of loop processing.
                            continue;
                        }

                        thisDWord = (uint)thisQWord;
                        if (DWordBeginsWithUtf8ThreeByteMask(thisDWord))
                        {
                            // A single three-byte sequence.
                            goto ProcessThreeByteSequenceWithCheck;
                        }
                        else
                        {
                            // Not a three-byte sequence; perhaps ASCII?
                            goto AfterReadDWord;
                        }
                    }

                    if (!Unsafe.IsAddressLessThan(ref finalPosWhereCanReadDWordFromInputBuffer, ref inputBuffer))
                    {
                        thisDWord = Unsafe.ReadUnaligned<uint>(ref inputBuffer);

                        // Optimization: A three-byte character could indicate CJK text, which makes it likely
                        // that the character following this one is also CJK. We'll check for a three-byte sequence
                        // marker now and jump directly to three-byte sequence processing if we see one, skipping
                        // all of the logic at the beginning of the loop.

                        if (DWordBeginsWithUtf8ThreeByteMask(thisDWord))
                        {
                            goto ProcessThreeByteSequenceWithCheck; // Found another [not yet validated] three-byte sequence; process
                        }
                        else
                        {
                            goto AfterReadDWord; // Probably ASCII punctuation or whitespace; go back to start of loop
                        }
                    }
                    else
                    {
                        goto ProcessRemainingBytesSlow; // Running out of data
                    }
                }

                // Assume the 4-byte case, but we need to validate.

                {
                    // We need to check for overlong or invalid (over U+10FFFF) four-byte sequences.
                    //
                    // Per Table 3-7, valid sequences are:
                    // [   F0   ] [ 90..BF ] [ 80..BF ] [ 80..BF ]
                    // [ F1..F3 ] [ 80..BF ] [ 80..BF ] [ 80..BF ]
                    // [   F4   ] [ 80..8F ] [ 80..BF ] [ 80..BF ]

                    if (!DWordBeginsWithUtf8FourByteMask(thisDWord)) { goto Error; }

                    // Now check for overlong / out-of-range sequences.

                    if (BitConverter.IsLittleEndian)
                    {
                        // The DWORD we read is [ 10xxxxxx 10yyyyyy 10zzzzzz 11110www ].
                        // We want to get the 'w' byte in front of the 'z' byte so that we can perform
                        // a single range comparison. We'll take advantage of the fact that the JITter
                        // can detect a ROR / ROL operation, then we'll just zero out the bytes that
                        // aren't involved in the range check.

                        uint toCheck = thisDWord & 0x0000FFFFu;

                        // At this point, toCheck = [ 00000000 00000000 10zzzzzz 11110www ].

                        toCheck = (toCheck << 24) | (toCheck >> 8); // ROR 8 / ROL 24

                        // At this point, toCheck = [ 11110www 00000000 00000000 10zzzzzz ].

                        if (!UnicodeHelpers.IsInRangeInclusive(toCheck, 0xF0000090U, 0xF400008FU)) { goto Error; }
                    }
                    else
                    {
                        if (!UnicodeHelpers.IsInRangeInclusive(thisDWord, 0xF0900000U, 0xF48FFFFFU)) { goto Error; }
                    }

                    // Validation complete.

                    inputBuffer = ref Unsafe.Add(ref inputBuffer, 4);
                    tempUtf16CodeUnitCountAdjustment -= 2; // 4 UTF-8 bytes -> 2 UTF-16 code units
                    tempScalarCountAdjustment--; // 2 UTF-16 code units -> 1 scalar

                    continue; // go back to beginning of loop for processing
                }
            }

            goto ProcessRemainingBytesSlow;

#pragma warning disable 0162 // compiler complains about unreachable code, but we need it for the local declaration
            nuint inputBufferRemainingBytes;
#pragma warning restore 0162

        ProcessInputOfLessThanDWordSize:

            Debug.Assert(inputLength < 4);
            inputBufferRemainingBytes = (nuint)inputLength;
            goto ProcessSmallBufferCommon;

        ProcessRemainingBytesSlow:

            inputBufferRemainingBytes = (nuint)Unsafe.ByteOffset(ref inputBuffer, ref finalPosWhereCanReadDWordFromInputBuffer) + 4;

        ProcessSmallBufferCommon:

            Debug.Assert(inputBufferRemainingBytes < 4);
            while (inputBufferRemainingBytes > 0)
            {
                uint firstByte = inputBuffer;

                if (firstByte < 0x80U)
                {
                    // 1-byte (ASCII) case
                    inputBuffer = ref Unsafe.Add(ref inputBuffer, 1);
                    inputBufferRemainingBytes--;
                    continue;
                }
                else if (inputBufferRemainingBytes >= 2)
                {
                    uint secondByte = Unsafe.Add(ref inputBuffer, 1);
                    if (firstByte < 0xE0U)
                    {
                        // 2-byte case
                        if (firstByte >= 0xC2U && UnicodeHelpers.IsUtf8ContinuationByte(secondByte))
                        {
                            inputBuffer = ref Unsafe.Add(ref inputBuffer, 2);
                            tempUtf16CodeUnitCountAdjustment--; // 2 UTF-8 bytes -> 1 UTF-16 code unit (and 1 scalar)
                            inputBufferRemainingBytes -= 2;
                            continue;
                        }
                    }
                    else if (inputBufferRemainingBytes >= 3)
                    {
                        uint thirdByte = Unsafe.Add(ref inputBuffer, 2);
                        if (firstByte <= 0xF0U)
                        {
                            if (firstByte == 0xE0U)
                            {
                                if (!UnicodeHelpers.IsInRangeInclusive(secondByte, 0xA0U, 0xBFU)) { goto Error; }
                            }
                            else if (firstByte == 0xEDU)
                            {
                                if (!UnicodeHelpers.IsInRangeInclusive(secondByte, 0x80U, 0x9FU)) { goto Error; }
                            }
                            else
                            {
                                if (!UnicodeHelpers.IsUtf8ContinuationByte(secondByte)) { goto Error; }
                            }

                            if (UnicodeHelpers.IsUtf8ContinuationByte(thirdByte))
                            {
                                inputBuffer = ref Unsafe.Add(ref inputBuffer, 3);
                                tempUtf16CodeUnitCountAdjustment -= 2; // 3 UTF-8 bytes -> 2 UTF-16 code units (and 2 scalars)
                                inputBufferRemainingBytes -= 3;
                                continue;
                            }
                        }
                    }
                }

                // Error - no match.

                goto Error;
            }

        // If we reached this point, we're out of data, and we saw no bad UTF8 sequence.

        Error:

            // Error handling logic.
            // (Also used for normal termination.)

            utf16CodeUnitCountAdjustment = tempUtf16CodeUnitCountAdjustment;
            scalarCountAdjustment = tempScalarCountAdjustment;
            return ref inputBuffer;
        }
    }
}
