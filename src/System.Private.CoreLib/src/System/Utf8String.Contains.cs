// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Buffers;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Unicode;
using System.Threading;
using Internal.Runtime.CompilerServices;

#if BIT64
using nint = System.Int64;
using nuint = System.UInt64;
#else // BIT64
using nint = System.Int32;
using nuint = System.UInt32;
#endif // BIT64

namespace System
{
    public sealed partial class Utf8String
    {
        public bool Contains(char value)
        {
            return Contains_Char_NoBoundsChecks(value, 0, Length);
        }

        public bool Contains(char value, StringComparison comparisonType)
        {
            throw new NotImplementedException();
        }

        public bool Contains(Utf8String value)
        {
            if (value is null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            // TODO: Replace IndexOf with Contains when extension method is made available.
            return this.AsSpanFast().IndexOf(value.AsSpanFast()) >= 0;
        }

        public bool Contains(Utf8String value, StringComparison comparisonType)
        {
            throw new NotImplementedException();
        }

        public bool Contains(UnicodeScalar value)
        {
            // TODO: Replace IndexOf with Contains when extension method is made available.
            return IndexOf_Scalar_NoBoundsChecks(value, 0, Length) >= 0;
        }

        public bool Contains(UnicodeScalar value, StringComparison comparisonType)
        {
            throw new NotImplementedException();
        }

        private bool Contains_Ascii_NoBoundsChecks(byte value, int startIndex, int count)
        {
            return SpanHelpers.Contains(ref Unsafe.Add(ref GetRawStringData(), startIndex), value, count);
        }
        private bool Contains_Char_NoBoundsChecks(char value, int startIndex, int count)
        {
            if (value <= 0x7Fu)
            {
                // ASCII
                return Contains_Ascii_NoBoundsChecks((byte)value, startIndex, count);
            }
            else
            {
                // Bail out early if the string contains only ASCII data (since that should've
                // been handled earlier), or if the char we're being asked to search for isn't
                // even representable in UTF-16. Only if both of those checks succeed do we perform
                // the actual search.

                // TODO: Replace IndexOf with Contains when the appropriate overload is available.
                return !IsKnownAscii()
                    && UnicodeScalar.TryCreate(value, out var scalar)
                    && IndexOf_Scalar_NoBoundsChecks(scalar, startIndex, count) >= 0;
            }
        }
    }
}
