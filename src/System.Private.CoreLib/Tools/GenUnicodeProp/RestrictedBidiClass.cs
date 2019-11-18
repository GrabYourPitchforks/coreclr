// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace GenUnicodeProp
{
    internal enum RestrictedBidiClass
    {
        StrongLeftToRight = 0, // Strong left-to-right character (class "L")
        StrongRightToLeft = 1, // Strong right-to-left character (classes "R", "AL")
        Other = 2, // Not a bidi class which requires special handling by us
    }
}
