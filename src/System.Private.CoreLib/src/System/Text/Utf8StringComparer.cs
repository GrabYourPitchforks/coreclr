// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;

namespace System.Text
{
    public abstract class Utf8StringComparer : IComparer<Utf8Segment>, IComparer<Utf8String>, IEqualityComparer<Utf8Segment>, IEqualityComparer<Utf8String>
    {
        // Nobody except for nested classes can create instances of this type.
        private Utf8StringComparer() { }

        public static Utf8StringComparer CurrentCulture => throw new NotImplementedException();
        public static Utf8StringComparer CurrentCultureIgnoreCase => throw new NotImplementedException();
        public static Utf8StringComparer InvariantCulture => throw new NotImplementedException();
        public static Utf8StringComparer InvariantCultureIgnoreCase => throw new NotImplementedException();
        public static Utf8StringComparer Ordinal => OrdinalComparer.Instance;
        public static Utf8StringComparer OrdinalIgnoreCase => OrdinalIgnoreCaseComparer.Instance;

        public static Utf8StringComparer Create(CultureInfo culture, bool ignoreCase) => Create(culture, ignoreCase ? CompareOptions.IgnoreCase : CompareOptions.None);

        public static Utf8StringComparer Create(CultureInfo culture, CompareOptions options)
        {
            if (culture is null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.culture);
            }

            return new CultureAwareComparer(culture, options);
        }

        public static Utf8StringComparer FromComparison(StringComparison comparisonType)
        {
            return comparisonType switch
            {
                StringComparison.CurrentCulture => CurrentCulture,
                StringComparison.CurrentCultureIgnoreCase => CurrentCultureIgnoreCase,
                StringComparison.InvariantCulture => InvariantCulture,
                StringComparison.InvariantCultureIgnoreCase => InvariantCultureIgnoreCase,
                StringComparison.Ordinal => Ordinal,
                StringComparison.OrdinalIgnoreCase => OrdinalIgnoreCase,
                _ => throw new ArgumentException(SR.NotSupported_StringComparison, nameof(comparisonType)),
            };
        }

        public abstract int Compare(Utf8Segment x, Utf8Segment y);
        public abstract int Compare(Utf8String? x, Utf8String? y);
        public abstract int Compare(Utf8Span x, Utf8Span y);
        public abstract bool Equals(Utf8Segment x, Utf8Segment y);
        public abstract bool Equals(Utf8String? x, Utf8String? y);
        public abstract bool Equals(Utf8Span x, Utf8Span y);
        public abstract int GetHashCode(Utf8Segment obj);
        public abstract int GetHashCode(Utf8String? obj);
        public abstract int GetHashCode(Utf8Span obj);

        private sealed class CultureAwareComparer : Utf8StringComparer
        {
            private readonly CultureInfo _culture;
            private readonly CompareOptions _options;

            internal CultureAwareComparer(CultureInfo culture, CompareOptions options)
            {
                Debug.Assert(culture != null);

                _culture = culture;
                _options = options;
            }
        }

        private sealed class OrdinalComparer : Utf8StringComparer
        {
            public static readonly OrdinalComparer Instance = new OrdinalComparer();

            // All accesses must be through the static factory.
            private OrdinalComparer() { }
        }

        private sealed class OrdinalIgnoreCaseComparer : Utf8StringComparer
        {
            public static readonly OrdinalIgnoreCaseComparer Instance = new OrdinalIgnoreCaseComparer();

            // All accesses must be through the static factory.
            private OrdinalIgnoreCaseComparer() { }
        }
    }
}
