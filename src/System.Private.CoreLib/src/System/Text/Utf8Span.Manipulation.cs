// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;

namespace System.Text
{
    public readonly ref partial struct Utf8Span
    {
        /// <summary>
        /// Locates <paramref name="separator"/> within this <see cref="Utf8Span"/> instance, creating <see cref="Utf8Span"/>
        /// instances which represent the data on either side of the separator. If <paramref name="separator"/> is not found
        /// within this <see cref="Utf8Span"/> instance, returns the tuple "(this, Empty)".
        /// </summary>
        /// <remarks>
        /// An ordinal search is performed.
        /// </remarks>
        public SplitOnResult SplitOn(char separator)
        {
            return TryFind(separator, out Range range) ? new SplitOnResult(this, range) : new SplitOnResult(this);
        }

        /// <summary>
        /// Locates <paramref name="separator"/> within this <see cref="Utf8String"/> instance, creating <see cref="Utf8Span"/>
        /// instances which represent the data on either side of the separator. If <paramref name="separator"/> is not found
        /// within this <see cref="Utf8String"/> instance, returns the tuple "(this, Empty)".
        /// </summary>
        /// <remarks>
        /// The search is performed using the specified <paramref name="comparisonType"/>.
        /// </remarks>
        public SplitOnResult SplitOn(char separator, StringComparison comparisonType)
        {
            return TryFind(separator, comparisonType, out Range range) ? new SplitOnResult(this, range) : new SplitOnResult(this);
        }

        /// <summary>
        /// Locates <paramref name="separator"/> within this <see cref="Utf8String"/> instance, creating <see cref="Utf8Span"/>
        /// instances which represent the data on either side of the separator. If <paramref name="separator"/> is not found
        /// within this <see cref="Utf8String"/> instance, returns the tuple "(this, Empty)".
        /// </summary>
        /// <remarks>
        /// An ordinal search is performed.
        /// </remarks>
        public SplitOnResult SplitOn(Rune separator)
        {
            return TryFind(separator, out Range range) ? new SplitOnResult(this, range) : new SplitOnResult(this);
        }

        /// <summary>
        /// Locates <paramref name="separator"/> within this <see cref="Utf8String"/> instance, creating <see cref="Utf8Span"/>
        /// instances which represent the data on either side of the separator. If <paramref name="separator"/> is not found
        /// within this <see cref="Utf8String"/> instance, returns the tuple "(this, Empty)".
        /// </summary>
        /// <remarks>
        /// The search is performed using the specified <paramref name="comparisonType"/>.
        /// </remarks>
        public SplitOnResult SplitOn(Rune separator, StringComparison comparisonType)
        {
            return TryFind(separator, comparisonType, out Range range) ? new SplitOnResult(this, range) : new SplitOnResult(this);
        }

        /// <summary>
        /// Locates <paramref name="separator"/> within this <see cref="Utf8String"/> instance, creating <see cref="Utf8Span"/>
        /// instances which represent the data on either side of the separator. If <paramref name="separator"/> is not found
        /// within this <see cref="Utf8String"/> instance, returns the tuple "(this, Empty)".
        /// </summary>
        /// <remarks>
        /// An ordinal search is performed.
        /// </remarks>
        public SplitOnResult SplitOn(Utf8String separator)
        {
            return TryFind(separator, out Range range) ? new SplitOnResult(this, range) : new SplitOnResult(this);
        }

        /// <summary>
        /// Locates the last occurrence of <paramref name="separator"/> within this <see cref="Utf8String"/> instance, creating <see cref="Utf8Span"/>
        /// instances which represent the data on either side of the separator. If <paramref name="separator"/> is not found
        /// within this <see cref="Utf8String"/> instance, returns the tuple "(this, Utf8Span)".
        /// </summary>
        /// <remarks>
        /// The search is performed using the specified <paramref name="comparisonType"/>.
        /// </remarks>
        public SplitOnResult SplitOn(Utf8String separator, StringComparison comparisonType)
        {
            return TryFind(separator, comparisonType, out Range range) ? new SplitOnResult(this, range) : new SplitOnResult(this);
        }

        /// <summary>
        /// Locates the last occurrence of <paramref name="separator"/> within this <see cref="Utf8Span"/> instance, creating <see cref="Utf8Span"/>
        /// instances which represent the data on either side of the separator. If <paramref name="separator"/> is not found
        /// within this <see cref="Utf8Span"/> instance, returns the tuple "(this, Empty)".
        /// </summary>
        /// <remarks>
        /// An ordinal search is performed.
        /// </remarks>
        public SplitOnResult SplitOnLast(char separator)
        {
            return TryFindLast(separator, out Range range) ? new SplitOnResult(this, range) : new SplitOnResult(this);
        }

        /// <summary>
        /// Locates the last occurrence of <paramref name="separator"/> within this <see cref="Utf8String"/> instance, creating <see cref="Utf8Span"/>
        /// instances which represent the data on either side of the separator. If <paramref name="separator"/> is not found
        /// within this <see cref="Utf8String"/> instance, returns the tuple "(this, Empty)".
        /// </summary>
        /// <remarks>
        /// The search is performed using the specified <paramref name="comparisonType"/>.
        /// </remarks>
        public SplitOnResult SplitOnLast(char separator, StringComparison comparisonType)
        {
            return TryFindLast(separator, comparisonType, out Range range) ? new SplitOnResult(this, range) : new SplitOnResult(this);
        }

        /// <summary>
        /// Locates the last occurrence of <paramref name="separator"/> within this <see cref="Utf8String"/> instance, creating <see cref="Utf8Span"/>
        /// instances which represent the data on either side of the separator. If <paramref name="separator"/> is not found
        /// within this <see cref="Utf8String"/> instance, returns the tuple "(this, Empty)".
        /// </summary>
        /// <remarks>
        /// An ordinal search is performed.
        /// </remarks>
        public SplitOnResult SplitOnLast(Rune separator)
        {
            return TryFindLast(separator, out Range range) ? new SplitOnResult(this, range) : new SplitOnResult(this);
        }

        /// <summary>
        /// Locates the last occurrence of <paramref name="separator"/> within this <see cref="Utf8String"/> instance, creating <see cref="Utf8Span"/>
        /// instances which represent the data on either side of the separator. If <paramref name="separator"/> is not found
        /// within this <see cref="Utf8String"/> instance, returns the tuple "(this, Empty)".
        /// </summary>
        /// <remarks>
        /// The search is performed using the specified <paramref name="comparisonType"/>.
        /// </remarks>
        public SplitOnResult SplitOnLast(Rune separator, StringComparison comparisonType)
        {
            return TryFindLast(separator, comparisonType, out Range range) ? new SplitOnResult(this, range) : new SplitOnResult(this);
        }

        /// <summary>
        /// Locates the last occurrence of <paramref name="separator"/> within this <see cref="Utf8String"/> instance, creating <see cref="Utf8Span"/>
        /// instances which represent the data on either side of the separator. If <paramref name="separator"/> is not found
        /// within this <see cref="Utf8String"/> instance, returns the tuple "(this, Empty)".
        /// </summary>
        /// <remarks>
        /// An ordinal search is performed.
        /// </remarks>
        public SplitOnResult SplitOnLast(Utf8String separator)
        {
            return TryFindLast(separator, out Range range) ? new SplitOnResult(this, range) : new SplitOnResult(this);
        }

        /// <summary>
        /// Locates the last occurrence of <paramref name="separator"/> within this <see cref="Utf8String"/> instance, creating <see cref="Utf8Span"/>
        /// instances which represent the data on either side of the separator. If <paramref name="separator"/> is not found
        /// within this <see cref="Utf8String"/> instance, returns the tuple "(this, Empty)".
        /// </summary>
        /// <remarks>
        /// The search is performed using the specified <paramref name="comparisonType"/>.
        /// </remarks>
        public SplitOnResult SplitOnLast(Utf8String separator, StringComparison comparisonType)
        {
            return TryFindLast(separator, comparisonType, out Range range) ? new SplitOnResult(this, range) : new SplitOnResult(this);
        }

        public readonly ref struct SplitOnResult
        {
            // Used when there is no match.
            internal SplitOnResult(Utf8Span originalSearchSpace)
            {
                Before = originalSearchSpace;
                After = Empty;
            }

            // Used when a match is found.
            internal SplitOnResult(Utf8Span originalSearchSpace, Range searchTermMatchRange)
            {
                (int startIndex, int length) = searchTermMatchRange.GetOffsetAndLength(originalSearchSpace.Length);

                // TODO_UTF8STRING: The below indexer performs correctness checks. We can skip these checks (and even the
                // bounds checks more generally) since we know the inputs are all valid and the containing struct is not
                // subject to tearing.

                Before = originalSearchSpace[..startIndex];
                After = originalSearchSpace[(startIndex + length)..];
            }

            public Utf8Span After { get; }
            public Utf8Span Before { get; }

            [EditorBrowsable(EditorBrowsableState.Never)]
            public void Deconstruct(out Utf8Span before, out Utf8Span after)
            {
                before = Before;
                after = After;
            }
        }
    }
}
