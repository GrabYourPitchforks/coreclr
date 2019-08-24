// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Marks an API as dangerous and noting that it should only be used within
    /// <see langword="unsafe"/> blocks.
    /// </summary>
    /// <remarks>
    /// This is used to denote APIs as potentially violating type safety or as bypassing
    /// standard invariant checks that would otherwise take place. Consumers of such APIs
    /// should take extra care not to violate the API's documented contracts.
    /// </remarks>
    [AttributeUsage(AttributeTargets.All)]
    public sealed class UnsafeMemberAttribute : Attribute
    {
    }
}
