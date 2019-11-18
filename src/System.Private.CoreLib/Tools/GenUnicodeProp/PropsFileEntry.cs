// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text.RegularExpressions;

namespace GenUnicodeProp
{
    // Represents an entry in a Unicode props file.
    // The expected format is "XXXX[..YYYY] ; <propName> [# <comment>]".
    internal sealed class PropsFileEntry
    {
        private static readonly Regex _regex = new Regex(@"^\s*(?<firstCodePoint>[0-9a-f]{4,})(\.\.(?<lastCodePoint>[0-9a-f]{4,}))?\s*;\s*(?<propName>\w+)", RegexOptions.IgnoreCase);

        public readonly uint FirstCodePoint;
        public readonly uint LastCodePoint;
        public readonly string PropName;

        private PropsFileEntry(uint firstCodePoint, uint lastCodePoint, string propName)
        {
            FirstCodePoint = firstCodePoint;
            LastCodePoint = lastCodePoint;
            PropName = propName;
        }

        public static bool TryParseLine(string line, out PropsFileEntry value)
        {
            Match match = _regex.Match(line);

            if (!match.Success)
            {
                value = default; // no match
                return false;
            }

            uint firstCodePoint = uint.Parse(match.Groups["firstCodePoint"].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            uint lastCodePoint = firstCodePoint; // assume no "..YYYY" segment for now

            if (match.Groups["lastCodePoint"].Success)
            {
                lastCodePoint = uint.Parse(match.Groups["lastCodePoint"].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }

            value = new PropsFileEntry(firstCodePoint, lastCodePoint, match.Groups["propName"].Value);
            return true;
        }
    }
}
