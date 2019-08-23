// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;

namespace System.Text
{
    public readonly ref struct Utf8Span
    {
        private ReadOnlySpan<byte> _data;

        /// <summary>
        /// This method is not supported as spans cannot be boxed. To compare two spans, use operator==.
        /// <exception cref="System.NotSupportedException">
        /// Always thrown by this method.
        /// </exception>
        /// </summary>
        [Obsolete("Equals() on ReadOnlySpan will always throw an exception. Use == instead.")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override bool Equals(object? obj)
        {
            throw new NotSupportedException(SR.NotSupported_CannotCallEqualsOnSpan);
        }

        public bool Equals(Utf8Span other)
        {
#error Not Implemented
        }

        public bool Equals(Utf8Span other, StringComparison comparison)
        {
#error Not Implemented
        }
       
        public override int GetHashCode()
        {
#error Not Implemented
        }

        public override int GetHashCode(StringComparison comparison)
        {
#error Not Implemented
        }

        public override string ToString()
        {
#error Not Implemented
        }
    }
}
