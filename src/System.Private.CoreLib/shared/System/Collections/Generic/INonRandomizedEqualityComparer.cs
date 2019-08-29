// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Collections.Generic
{
    /// <summary>
    /// Represents a comparer that does not offer DoS protection against hash
    /// code collisions. Only to be used when the container has other protections
    /// against DoS attacks.
    /// </summary>
    internal interface INonRandomizedEqualityComparer<T>
    {
        /// <summary>
        /// Gets the normal DoS-resistant <see cref="IEqualityComparer{T}"/>
        /// that the container should switch to if it detects hash code collision DoS.
        /// </summary>
        /// <remarks>
        /// Could return <see langword="null"/>, which means <see cref="object.GetHashCode"/>
        /// should be used instead.
        /// </remarks>
        IEqualityComparer<T>? GetNormalComparer();
    }
}
