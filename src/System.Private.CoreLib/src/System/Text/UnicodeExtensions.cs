// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;

namespace System.Text
{
    public static class UnicodeExtensions
    {
        public static Utf8CharSpanScalarEnumerator GetScalars(ReadOnlySpan<Utf8Char> value)
        {
            return new Utf8CharSpanScalarEnumerator(value);
        }
        
        public static Utf8StringScalarEnumerator GetScalars(Utf8String value)
        {
            if (value is null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            return new Utf8StringScalarEnumerator(value);
        }
        
        public struct Utf8StringScalarEnumerator
            : IEnumerable<(Rune? ScalarValue, int StartIndex, int Length)>
            , IEnumerator<(Rune? ScalarValue, int StartIndex, int Length)>
        {
            private readonly Utf8String _value;
            private int _startIndex;
            private int _length;
            private Rune? _scalarValue;

            internal Utf8StringScalarEnumerator(Utf8String value)
            {
                _value = value;
                _startIndex = 0;
                _length = 0;
                _scalarValue = null;
            }

            public (Rune? ScalarValue, int StartIndex, int Length) Current
            {
                get => (_scalarValue, _startIndex, _length);
            }

            [EditorBrowsable(EditorBrowsableState.Never)] // should be compiler-called
            public Utf8StringScalarEnumerator GetEnumerator() => this;

            public bool MoveNext()
            {
                int newStartIndex = _startIndex + _length;
                if ((uint)newStartIndex >= _value.Length)
                {
                    return false; // EOF
                }

                var result = UnicodeReader.PeekFirstScalarUtf8(_value.AsSpan(newStartIndex).AsBytes());
                _scalarValue = (result.status == SequenceValidity.Valid) ? result.scalar : default(Rune?);
                _startIndex = newStartIndex;
                _length = result.charsConsumed;
                return true;
            }

            void IDisposable.Dispose() { }
            IEnumerator IEnumerable.GetEnumerator() => this;
            IEnumerator<(Rune? ScalarValue, int StartIndex, int Length)> IEnumerable<(Rune? ScalarValue, int StartIndex, int Length)>.GetEnumerator() => this;
            object IEnumerator.Current => Current;
            void IEnumerator.Reset() { }
        }

        public ref struct Utf8CharSpanScalarEnumerator
        {
            private readonly ReadOnlySpan<byte> _value;
            private int _startIndex;
            private int _length;
            private Rune? _scalarValue;

            internal Utf8CharSpanScalarEnumerator(ReadOnlySpan<Utf8Char> value)
            {
                _value = value.AsBytes();
                _startIndex = 0;
                _length = 0;
                _scalarValue = null;
            }

            public (Rune? ScalarValue, int StartIndex, int Length) Current
            {
                get => (_scalarValue, _startIndex, _length);
            }

            [EditorBrowsable(EditorBrowsableState.Never)] // should be compiler-called
            public Utf8CharSpanScalarEnumerator GetEnumerator() => this;

            public bool MoveNext()
            {
                int newStartIndex = _startIndex + _length;
                if ((uint)newStartIndex >= _value.Length)
                {
                    return false; // EOF
                }

                var result = UnicodeReader.PeekFirstScalarUtf8(_value.Slice(newStartIndex));
                _scalarValue = (result.status == SequenceValidity.Valid) ? result.scalar : default(Rune?);
                _startIndex = newStartIndex;
                _length = result.charsConsumed;
                return true;
            }
        }
    }
}
