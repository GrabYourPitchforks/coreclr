// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Text
{
    // Represents the fundamental elemental type of UTF-8 textual data and is distinct
    // from System.Byte, similar to how System.Char is the fundamental elemental type
    // of UTF-16 textual data and is distinct from System.UInt16.
    //
    // Ideally the compiler would support various syntaxes for this, like:
    // Utf8Char theChar = 63; // Implicit assignment of const to local of type Utf8Char
    public readonly struct Utf8Char : IComparable<Utf8Char>, IEquatable<Utf8Char>, IFormattable, ISpanFormattable
    {
        private readonly byte _value;

        private Utf8Char(byte value)
        {
            _value = value;
        }

        // Construction is performed via a cast. All casts are checked for overflow
        // but not for correctness. For example, casting -1 to Utf8Char will fail
        // with an OverflowException, but casting 0xFF to Utf8Char will succeed even
        // though 0xFF is never a valid UTF-8 code unit. Additionally, even though
        // the cast from Byte to Utf8Char can never overflow, it's still an explicit
        // cast because we don't want devs to fall into the habit of treating arbitrary
        // integral types as equivalent to textual data types. As an existing example of
        // this in the current compiler, there's no implicit cast from Byte to Char even
        // though it's a widening operation, but there is an explicit cast.

        public static explicit operator Utf8Char(byte value) => new Utf8Char(value);
        [CLSCompliant(false)]
        public static explicit operator Utf8Char(sbyte value) => new Utf8Char(checked((byte)value));
        public static explicit operator Utf8Char(char value) => new Utf8Char(checked((byte)value));
        public static explicit operator Utf8Char(short value) => new Utf8Char(checked((byte)value));
        [CLSCompliant(false)]
        public static explicit operator Utf8Char(ushort value) => new Utf8Char(checked((byte)value));
        public static explicit operator Utf8Char(int value) => new Utf8Char(checked((byte)value));
        [CLSCompliant(false)]
        public static explicit operator Utf8Char(uint value) => new Utf8Char(checked((byte)value));
        public static explicit operator Utf8Char(long value) => new Utf8Char(checked((byte)value));
        [CLSCompliant(false)]
        public static explicit operator Utf8Char(ulong value) => new Utf8Char(checked((byte)value));

        // Casts to the various primitive integral types. All casts are implicit
        // with two exceptions, which are explicit:
        // - Cast to SByte, because it could result in an OverflowException.
        // - Cast to Char, for the same reason as the Byte-to-Utf8Char cast.

        public static implicit operator byte(Utf8Char value) => value._value;
        [CLSCompliant(false)]
        public static explicit operator sbyte(Utf8Char value) => checked((sbyte)value._value);
        public static explicit operator char(Utf8Char value) => (char)value._value;
        public static implicit operator short(Utf8Char value) => value._value;
        [CLSCompliant(false)]
        public static implicit operator ushort(Utf8Char value) => value._value;
        public static implicit operator int(Utf8Char value) => value._value;
        [CLSCompliant(false)]
        public static implicit operator uint(Utf8Char value) => value._value;
        public static implicit operator long(Utf8Char value) => value._value;
        [CLSCompliant(false)]
        public static implicit operator ulong(Utf8Char value) => value._value;

        public static bool operator ==(Utf8Char a, Utf8Char b) => a._value == b._value;
        public static bool operator !=(Utf8Char a, Utf8Char b) => a._value != b._value;
        public static bool operator <(Utf8Char a, Utf8Char b) => a._value < b._value;
        public static bool operator <=(Utf8Char a, Utf8Char b) => a._value <= b._value;
        public static bool operator >(Utf8Char a, Utf8Char b) => a._value > b._value;
        public static bool operator >=(Utf8Char a, Utf8Char b) => a._value >= b._value;
        public int CompareTo(Utf8Char other) => this._value.CompareTo(other._value);
        public override bool Equals(object obj) => (obj is Utf8Char ch) && this.Equals(ch);
        public bool Equals(Utf8Char other) => this._value == other._value;
        public override int GetHashCode() => _value.GetHashCode();
        public override string ToString() => _value.ToString();
        public string ToString(string format, IFormatProvider formatProvider) => _value.ToString(format, formatProvider);
        public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider provider) => _value.TryFormat(destination, out charsWritten, format, provider);
    }
}
