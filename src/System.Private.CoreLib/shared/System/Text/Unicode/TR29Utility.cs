// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Buffers;
using System.Runtime.InteropServices;

namespace System.Text.Unicode
{
    /// <summary>
    /// Provides helper utilities for computing text segmentation boundaries
    /// as specified in http://www.unicode.org/reports/tr29/.
    /// </summary>
    /// <remarks>
    /// The current implementation is compliant per Rev. 35, https://www.unicode.org/reports/tr29/tr29-35.html.
    /// </remarks>
    internal static partial class TR29Utility
    {
        private delegate OperationStatus DecodeFirstRune<T>(ReadOnlySpan<T> input, out Rune rune, out int elementsConsumed);

        private static readonly DecodeFirstRune<char> _utf16Decoder = Rune.DecodeFromUtf16;
        private static readonly DecodeFirstRune<byte> _utf8Decoder = Rune.DecodeFromUtf8;

        private static int GetLengthOfFirstExtendedGraphemeCluster<T>(ReadOnlySpan<T> input, DecodeFirstRune<T> decoder)
        {
            // Algorithm given at http://www.unicode.org/reports/tr29/#Grapheme_Cluster_Boundary_Rules.

            Processor<T> processor = new Processor<T>(input, decoder);

            // First, consume as many Prepend scalars as we can (rule GB9b).

            while (processor.NextCategory == GraphemeClusterCategory.Prepend)
            {
                processor.Consume();
            }

            // Next, make sure we're not about to violate control character restrictions.

            if (processor.TotalElementsConsumed > 0)
            {
                // If we saw Prepend data, we can't have Control | CR | LF data afterward (rule GB5).
                if (processor.NextCategory == GraphemeClusterCategory.Control
                    || processor.NextCategory == GraphemeClusterCategory.CR
                    || processor.NextCategory == GraphemeClusterCategory.LF)
                {
                    return processor.TotalElementsConsumed;
                }
            }

            // Now begin the main state machine.

            GraphemeClusterCategory thisCategory = processor.NextCategory;
            processor.Consume();

            switch (thisCategory)
            {
                case GraphemeClusterCategory.CR:
                    if (processor.NextCategory == GraphemeClusterCategory.LF)
                    {
                        processor.Consume(); // rule GB3 (<CR><LF>) followed by rule GB4 (no data after LF)
                        return processor.TotalElementsConsumed;
                    }
                    else
                    {
                        return processor.TotalElementsConsumed; // rule GB4 (no data after CR)
                    }

                case GraphemeClusterCategory.Control:
                case GraphemeClusterCategory.LF:
                    return processor.TotalElementsConsumed; // rule GB4 (no data after Control | LF)

                case GraphemeClusterCategory.HangulL:
                    if (processor.NextCategory == GraphemeClusterCategory.HangulL)
                    {
                        processor.Consume(); // rule GB6 (L x L)
                        goto case GraphemeClusterCategory.HangulL;
                    }
                    else if (processor.NextCategory == GraphemeClusterCategory.HangulV)
                    {
                        processor.Consume(); // rule GB6 (L x V)
                        goto case GraphemeClusterCategory.HangulV;
                    }
                    else if (processor.NextCategory == GraphemeClusterCategory.HangulLV)
                    {
                        processor.Consume(); // rule GB6 (L x LV)
                        goto case GraphemeClusterCategory.HangulLV;
                    }
                    else if (processor.NextCategory == GraphemeClusterCategory.HangulLVT)
                    {
                        processor.Consume(); // rule GB6 (L x LVT)
                        goto case GraphemeClusterCategory.HangulLVT;
                    }
                    else
                    {
                        goto DrainTrailers;
                    }

                case GraphemeClusterCategory.HangulLV:
                case GraphemeClusterCategory.HangulV:
                    if (processor.NextCategory == GraphemeClusterCategory.HangulV)
                    {
                        processor.Consume(); // rule GB7 (LV | V x V)
                        goto case GraphemeClusterCategory.HangulV;
                    }
                    else if (processor.NextCategory == GraphemeClusterCategory.HangulT)
                    {
                        processor.Consume(); // rule GB7 (LV | V x T)
                        goto case GraphemeClusterCategory.HangulT;
                    }
                    else
                    {
                        goto DrainTrailers;
                    }

                case GraphemeClusterCategory.HangulLVT:
                case GraphemeClusterCategory.HangulT:
                    if (processor.NextCategory == GraphemeClusterCategory.HangulT)
                    {
                        processor.Consume(); // rule GB8 (LVT | T x T)
                        goto case GraphemeClusterCategory.HangulT;
                    }
                    else
                    {
                        goto DrainTrailers;
                    }

                case GraphemeClusterCategory.ExtendedPictograph:
                    // Attempt processing extended pictographic (rules GB11, GB9).
                    // First, drain any Extend scalars that might exist

                    while (processor.NextCategory == GraphemeClusterCategory.Extend)
                    {
                        processor.Consume();
                    }

                    // Now see if there's a ZWJ + extended pictograph again.

                    if (processor.NextCategory != GraphemeClusterCategory.Zwj)
                    {
                        goto DrainTrailers;
                    }

                    processor.Consume();
                    if (processor.NextCategory != GraphemeClusterCategory.ExtendedPictograph)
                    {
                        goto DrainTrailers;
                    }

                    processor.Consume();
                    goto case GraphemeClusterCategory.ExtendedPictograph;

                case GraphemeClusterCategory.RegionalIndicator:
                    // We've consumed a single RI scalar. Try to consume another (to make it a pair).

                    if (processor.NextCategory == GraphemeClusterCategory.RegionalIndicator)
                    {
                        processor.Consume();
                    }

                    // We've consumed a pair of RI scalars. If there's another pair, consume that pair
                    // in a loop as part of this same cluster.

                    do
                    {
                        if (processor.NextCategory == GraphemeClusterCategory.RegionalIndicator)
                        {
                            Processor<T> tempProcessor = processor; // so we can back up on failure
                            tempProcessor.Consume();
                            if (tempProcessor.NextCategory == GraphemeClusterCategory.RegionalIndicator)
                            {
                                tempProcessor.Consume(); // rules GB12, GB13 ([^RI] (RI RI)* RI x RI)
                                processor = tempProcessor;
                                continue;
                            }
                        }
                    } while (false);

                    goto DrainTrailers; // nothing but trailers after the final RI

                default:
                    break;
            }

        DrainTrailers:

            // rules GB9, GB9a
            while (processor.NextCategory == GraphemeClusterCategory.Extend
                || processor.NextCategory == GraphemeClusterCategory.Zwj
                || processor.NextCategory == GraphemeClusterCategory.SpacingMark)
            {
                processor.Consume();
            }

            return processor.TotalElementsConsumed; // rules GB2, GB999
        }

        /// <summary>
        /// Given UTF-16 input text, returns the length (in chars) of the first extended grapheme cluster.
        /// The slice [0..length] represents the first standalone extended grapheme cluster in the text.
        /// If the input is empty, returns 0.
        /// </summary>
        public static int GetLengthOfFirstUtf16ExtendedGraphemeCluster(ReadOnlySpan<char> input)
        {
            return GetLengthOfFirstExtendedGraphemeCluster(input, _utf16Decoder);
        }

        /// <summary>
        /// Given UTF-8 input text, returns the length (in bytes) of the first extended grapheme cluster.
        /// The slice [0..length] represents the first standalone extended grapheme cluster in the text.
        /// If the input is empty, returns 0.
        /// </summary>
        public static int GetLengthOfFirstUtf8ExtendedGraphemeCluster(ReadOnlySpan<byte> input)
        {
            return GetLengthOfFirstExtendedGraphemeCluster(input, _utf8Decoder);
        }

        private static GraphemeClusterCategory GetGraphemeClusterCategoryForScalar(Rune value)
        {
            // TODO_UTF8STRING: Bring in the 12:4:4 / 8:4:4 mapping.

            return (GraphemeClusterCategory)GraphemeBoundaryMap[value.Value];
        }

        private enum GraphemeClusterCategory
        {
            None,
            Control,
            CR,
            LF,
            HangulL,
            HangulV,
            HangulLV,
            HangulLVT,
            HangulT,
            Extend,
            Zwj,
            SpacingMark,
            Prepend,
            ExtendedPictograph,
            RegionalIndicator
        }

        [StructLayout(LayoutKind.Auto)]
        private ref struct Processor<T>
        {
            private ReadOnlySpan<T> _buffer;
            private readonly DecodeFirstRune<T> _decoder;
            private int _elementsInNextScalar;

            internal Processor(ReadOnlySpan<T> buffer, DecodeFirstRune<T> decoder)
            {
                _buffer = buffer;
                _decoder = decoder;
                NextCategory = default;
                _elementsInNextScalar = 0;
                TotalElementsConsumed = default;
                Consume();
            }

            public GraphemeClusterCategory NextCategory { get; private set; }
            public bool IsFinished => _buffer.IsEmpty;
            public int TotalElementsConsumed { get; private set; }

            public void Consume()
            {
                TotalElementsConsumed += _elementsInNextScalar;
                _buffer = _buffer.Slice(_elementsInNextScalar);

                _decoder(_buffer, out Rune nextScalar, out _elementsInNextScalar);
                NextCategory = GetGraphemeClusterCategoryForScalar(nextScalar);
            }
        }
    }
}
