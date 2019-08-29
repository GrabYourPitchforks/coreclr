// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections;
using System.Collections.Generic;
using System.Text;

#pragma warning disable SA1121 // explicitly using type aliases instead of built-in types
#if BIT64
using nint = System.Int64;
using nuint = System.UInt64;
#else
using nint = System.Int32;
using nuint = System.UInt32;
#endif

namespace System
{
    public sealed partial class Utf8String
    {
        /*
         * ENUMERABLE PROPERTY ACCESSORS
         */

        /// <summary>
        /// Allows enumeration over the individual UTF-8 bytes of this <see cref="Utf8String"/> instance.
        /// </summary>
        public ByteEnumerable Bytes => new ByteEnumerable(this);

        /// <summary>
        /// Allows enumeration over the UTF-16 code units (see <see cref="char"/>) which would result
        /// from transcoding this <see cref="Utf8String"/> instance to a UTF-16 <see cref="string"/>.
        /// </summary>
        public CharEnumerable Chars => new CharEnumerable(this);

        /// <summary>
        /// Allows enumeration over the Unicode scalar values (see <see cref="Rune"/>) which are
        /// encoded by this <see cref="Utf8String"/> instance.
        /// </summary>
        public RuneEnumerable Runes => new RuneEnumerable(this);

        /*
         * ENUMERATORS
         */

        public readonly struct ByteEnumerable : IEnumerable<byte>
        {
            private readonly Utf8String _obj;

            internal ByteEnumerable(Utf8String obj)
            {
                _obj = obj;
            }

            public Enumerator GetEnumerator() => new Enumerator(_obj);

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            IEnumerator<byte> IEnumerable<byte>.GetEnumerator() => GetEnumerator();

            public struct Enumerator : IEnumerator<byte>
            {
                private readonly Utf8String _obj;
                private int _curByteIdx;

                internal Enumerator(Utf8String obj)
                {
                    _obj = obj;
                    _curByteIdx = -1;
                }

                public byte Current
                {
                    get
                    {
                        // Make copies of fields to avoid tearing issues since we're
                        // about to perform unsafe accesses.

                        Utf8String obj = _obj;
                        uint curByteIdx = (uint)_curByteIdx;

                        // If we'd go past the end of the Utf8String instance, then
                        // just dereference the null terminator and move on.

                        if (curByteIdx > (uint)obj.Length)
                        {
                            curByteIdx = (uint)obj.Length;
                        }

                        return obj.DangerousGetMutableReference(curByteIdx);
                    }
                }

                public bool MoveNext()
                {
                    int curByteIdx = _curByteIdx;

                    if (curByteIdx > _obj.Length)
                    {
                        return false; // no more data
                    }

                    _curByteIdx = curByteIdx + 1;
                    return true;
                }

                void IDisposable.Dispose()
                {
                    // intentionally no-op
                }

                object IEnumerator.Current => Current;

                void IEnumerator.Reset()
                {
                    _curByteIdx = -1;
                }
            }
        }

        public readonly struct CharEnumerable : IEnumerable<char>
        {
            private readonly Utf8String _obj;

            internal CharEnumerable(Utf8String obj)
            {
                _obj = obj;
            }

            public Enumerator GetEnumerator() => new Enumerator(_obj);

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            IEnumerator<char> IEnumerable<char>.GetEnumerator() => GetEnumerator();

            public struct Enumerator : IEnumerator<char>
            {
                private readonly Utf8String _obj;
                private uint _currentCharPair;
                private uint _nextByteIdx;

                internal Enumerator(Utf8String obj)
                {
                    _obj = obj;
                    _currentCharPair = default;
                    _nextByteIdx = 0;
                }

                public char Current => (char)_currentCharPair;

                public bool MoveNext()
                {
                    // Make copies of fields to avoid tearing issues since we're
                    // about to perform unsafe accesses.

                    uint currentCharPair = _currentCharPair;
                    if (currentCharPair > char.MaxValue)
                    {
                        // There was a surrogate pair smuggled in here from a previous operation.
                        // Shift out the high surrogate value and return immediately.

                        _currentCharPair = currentCharPair >> 16;
                        return true;
                    }

                    Utf8String obj = _obj;
                    nuint nextByteIdx = _nextByteIdx;

                    if ((uint)nextByteIdx >= (uint)obj.Length)
                    {
                        return false; // no more data
                    }

                    // TODO_UTF8STRING: Since we assume Utf8String instances are well-formed, we should instead
                    // call an optimized version of the "decode" routine below which skips well-formedness checks.

                    Rune.DecodeFromUtf8(new ReadOnlySpan<byte>(ref obj.DangerousGetMutableReference(nextByteIdx), obj.Length - (int)nextByteIdx), out Rune currentRune, out int bytesConsumedJustNow);
                    _nextByteIdx = (uint)nextByteIdx + (uint)bytesConsumedJustNow;

                    if (currentRune.IsBmp)
                    {
                        // Common case - BMP scalar value.

                        _currentCharPair = (uint)currentRune.Value;
                    }
                    else
                    {
                        // Uncommon case - supplementary plane (astral) scalar value.
                        // We'll smuggle the two UTF-16 code units into a single 32-bit value,
                        // with the leading surrogate packed into the low 16 bits of the value,
                        // and the trailing surrogate packed into the high 16 bits of the value.

                        UnicodeUtility.GetUtf16SurrogatesFromSupplementaryPlaneScalar((uint)currentRune.Value, out char leadingCodeUnit, out char trailingCodeUnit);
                        _currentCharPair = (uint)leadingCodeUnit + ((uint)trailingCodeUnit << 16);
                    }

                    return true;
                }

                void IDisposable.Dispose()
                {
                    // intentionally no-op
                }

                object IEnumerator.Current => Current;

                void IEnumerator.Reset()
                {
                    _currentCharPair = default;
                    _nextByteIdx = 0;
                }
            }
        }

        public readonly struct RuneEnumerable : IEnumerable<Rune>
        {
            private readonly Utf8String _obj;

            internal RuneEnumerable(Utf8String obj)
            {
                _obj = obj;
            }

            public Enumerator GetEnumerator() => new Enumerator(_obj);

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            IEnumerator<Rune> IEnumerable<Rune>.GetEnumerator() => GetEnumerator();

            public struct Enumerator : IEnumerator<Rune>
            {
                private readonly Utf8String _obj;
                private Rune _currentRune;
                private uint _nextByteIdx;

                internal Enumerator(Utf8String obj)
                {
                    _obj = obj;
                    _currentRune = default;
                    _nextByteIdx = 0;
                }

                public Rune Current => _currentRune;

                public bool MoveNext()
                {
                    // Make copies of fields to avoid tearing issues since we're
                    // about to perform unsafe accesses.

                    Utf8String obj = _obj;
                    nuint nextByteIdx = _nextByteIdx;

                    if ((uint)nextByteIdx >= (uint)obj.Length)
                    {
                        return false; // no more data
                    }

                    // TODO_UTF8STRING: Since we assume Utf8String instances are well-formed, we should instead
                    // call an optimized version of the "decode" routine below which skips well-formedness checks.

                    Rune.DecodeFromUtf8(new ReadOnlySpan<byte>(ref obj.DangerousGetMutableReference(nextByteIdx), obj.Length - (int)nextByteIdx), out _currentRune, out int bytesConsumedJustNow);
                    _nextByteIdx = (uint)nextByteIdx + (uint)bytesConsumedJustNow;
                    return true;
                }

                void IDisposable.Dispose()
                {
                    // intentionally no-op
                }

                object IEnumerator.Current => Current;

                void IEnumerator.Reset()
                {
                    _currentRune = default;
                    _nextByteIdx = 0;
                }
            }
        }
    }
}
