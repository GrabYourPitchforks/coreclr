// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Text
{
    public readonly struct Utf8Char : IComparable<Utf8Char>, IEquatable<Utf8Char>
    {
        private readonly byte Value;

        public Utf8Char(byte value)
        {
            Value = value;
        }

        public static bool operator ==(Utf8Char a, Utf8Char b) => a.Value == b.Value;
        public static bool operator !=(Utf8Char a, Utf8Char b) => a.Value != b.Value;
        public static bool operator <(Utf8Char a, Utf8Char b) => a.Value < b.Value;
        public static bool operator <=(Utf8Char a, Utf8Char b) => a.Value <= b.Value;
        public static bool operator >(Utf8Char a, Utf8Char b) => a.Value > b.Value;
        public static bool operator >=(Utf8Char a, Utf8Char b) => a.Value >= b.Value;

        // Operators from Utf8Char to <other primitives>
        public static implicit operator byte(Utf8Char value) => value.Value;
        [CLSCompliant(false)]
        public static explicit operator sbyte(Utf8Char value) => checked((sbyte)value.Value); // explicit because checked
        public static explicit operator char(Utf8Char value) => (char)value.Value; // explicit because don't want to encourage char conversion
        public static implicit operator short(Utf8Char value) => value.Value;
        [CLSCompliant(false)]
        public static implicit operator ushort(Utf8Char value) => value.Value;
        public static implicit operator int(Utf8Char value) => value.Value;
        [CLSCompliant(false)]
        public static implicit operator uint(Utf8Char value) => value.Value;
        public static implicit operator long(Utf8Char value) => value.Value;
        [CLSCompliant(false)]
        public static implicit operator ulong(Utf8Char value) => value.Value;

        // Operators from <other primitives> to Utf8Char; most are explicit because checked
        public static implicit operator Utf8Char(byte value) => new Utf8Char(value);
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

        public int CompareTo(Utf8Char other) => this.Value.CompareTo(other.Value);

        public override bool Equals(object obj) => (obj is Utf8Char other) && (this == other);
        public bool Equals(Utf8Char other) => this == other;

        public override int GetHashCode() => Value;

        public override string ToString() => Value.ToString("X2");
    }
}
