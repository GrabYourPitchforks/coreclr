// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;

namespace System.Text
{
    /// <summary>
    /// Represents the position of a <see cref="Rune"/> within a larger sequence.
    /// </summary>
    public readonly struct RunePosition : IEquatable<RunePosition>
    {
        internal RunePosition(Rune rune, SequenceValidity sequenceValidity, int startIndex, int sequenceLength)
        {
            Rune = rune;
            SequenceValidity = sequenceValidity;
            StartIndex = startIndex;
            SequenceLength = sequenceLength;
        }

        public Rune Rune { get; }
        public int SequenceLength { get; }
        public SequenceValidity SequenceValidity { get; }
        public int StartIndex { get; }

        [EditorBrowsable(EditorBrowsableState.Never)] // for compiler use
        public void Deconstruct(out Rune rune, out int startIndex)
        {
            rune = Rune;
            startIndex = StartIndex;
        }

        [EditorBrowsable(EditorBrowsableState.Never)] // for compiler use
        public void Deconstruct(out Rune rune, out int startIndex, out int sequenceLength)
        {
            rune = Rune;
            startIndex = StartIndex;
            sequenceLength = SequenceLength;
        }

        public override bool Equals(object obj)
        {
            return (obj is RunePosition) && Equals((RunePosition)obj);
        }

        public bool Equals(RunePosition other)
        {
            return this.Rune == other.Rune
                && this.SequenceLength == other.SequenceLength
                && this.SequenceValidity == other.SequenceValidity
                && this.StartIndex == other.StartIndex;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Rune, SequenceLength, SequenceValidity, StartIndex);
        }

        public struct FromStringEnumerator : IEnumerable<RunePosition>, IEnumerator<RunePosition>
        {
            private readonly string _data;
            private int _nextOffset;

            internal FromStringEnumerator(string data)
            {
                _data = data;
                _nextOffset = 0;
                Current = default;
            }

            public RunePosition Current { get; private set; }

            public IEnumerator<RunePosition> GetEnumerator() => this;

            public bool MoveNext()
            {
                // If we've reached the end of the buffer, bail.
                // This check is written in such a way to elide error checks in AsSpan.

                if ((uint)_nextOffset >= (uint)_data.Length)
                {
                    return false; // end of buffer
                }

                SequenceValidity validity = SequenceValidity.Valid;
                int scalarValue = Utf16Utility.ReadFirstScalarOrErrorCodeFromBuffer(_data.AsSpan(_nextOffset));

                if (scalarValue < 0)
                {
                    // error handling - fix up return values
                    scalarValue = Rune.ReplacementChar.Value;
                    validity = (SequenceValidity)(-scalarValue); // validity is encoded in error code
                }

                Rune rune = Rune.UnsafeCreate((uint)scalarValue);
                int sequenceLength = rune.Utf16SequenceLength;

                Current = new RunePosition(
                    rune: rune,
                    sequenceValidity: validity,
                    startIndex: _nextOffset,
                    sequenceLength: sequenceLength);

                _nextOffset += sequenceLength;
                return true;
            }

            public void Dispose() { /* no-op */ }

            public void Reset() => throw NotImplemented.ByDesign;

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            object IEnumerator.Current => Current;
        }
    }
}
