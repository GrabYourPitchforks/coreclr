// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace GenUnicodeProp
{
    internal class UnicodeDataRepository
    {
        private static readonly Dictionary<string, UnicodeCategory> UnicodeCategoryMap = new Dictionary<string, UnicodeCategory>
        {
            ["Lu"] = UnicodeCategory.UppercaseLetter,
            ["Ll"] = UnicodeCategory.LowercaseLetter,
            ["Lt"] = UnicodeCategory.TitlecaseLetter,
            ["Lm"] = UnicodeCategory.ModifierLetter,
            ["Lo"] = UnicodeCategory.OtherLetter,
            ["Mn"] = UnicodeCategory.NonSpacingMark,
            ["Mc"] = UnicodeCategory.SpacingCombiningMark,
            ["Me"] = UnicodeCategory.EnclosingMark,
            ["Nd"] = UnicodeCategory.DecimalDigitNumber,
            ["Nl"] = UnicodeCategory.LetterNumber,
            ["No"] = UnicodeCategory.OtherNumber,
            ["Zs"] = UnicodeCategory.SpaceSeparator,
            ["Zl"] = UnicodeCategory.LineSeparator,
            ["Zp"] = UnicodeCategory.ParagraphSeparator,
            ["Cc"] = UnicodeCategory.Control,
            ["Cf"] = UnicodeCategory.Format,
            ["Cs"] = UnicodeCategory.Surrogate,
            ["Co"] = UnicodeCategory.PrivateUse,
            ["Pc"] = UnicodeCategory.ConnectorPunctuation,
            ["Pd"] = UnicodeCategory.DashPunctuation,
            ["Ps"] = UnicodeCategory.OpenPunctuation,
            ["Pe"] = UnicodeCategory.ClosePunctuation,
            ["Pi"] = UnicodeCategory.InitialQuotePunctuation,
            ["Pf"] = UnicodeCategory.FinalQuotePunctuation,
            ["Po"] = UnicodeCategory.OtherPunctuation,
            ["Sm"] = UnicodeCategory.MathSymbol,
            ["Sc"] = UnicodeCategory.CurrencySymbol,
            ["Sk"] = UnicodeCategory.ModifierSymbol,
            ["So"] = UnicodeCategory.OtherSymbol,
            ["Cn"] = UnicodeCategory.OtherNotAssigned,
        };

        private static readonly Dictionary<string, RestrictedBidiClass> BidiClassMap = new Dictionary<string, RestrictedBidiClass>
        {
            ["L"] = RestrictedBidiClass.StrongLeftToRight,
            ["R"] = RestrictedBidiClass.StrongRightToLeft,
            ["AL"] = RestrictedBidiClass.StrongRightToLeft,
        };

        private readonly List<Data1> _data = new List<Data1>();

        private UnicodeDataRepository()
        {
            // First, read all of the ancillary data files into their own distinct data
            // structures. We'll use these as helper structures once we parse the
            // main data file.

            HashSet<uint> whitespaceCodePoints = GetWhiteSpaceCodePoints();
            Dictionary<uint, int> caseFoldMap = GetSimpleCaseFoldOffsets();
            Dictionary<uint, GraphemeBoundaryCategory> graphemeBreakMap = GetGraphemeBreakPropertyMap();

            // Next, process the main data file, folding in the ancillary data.

            foreach (UnicodeDataEntry entry in ReadUnicodeDataFile())
            {
                Data1 newData = new Data1()
                {
                    CodePoint = entry.codePoint,
                    UnicodeCategory = UnicodeCategoryMap[entry.generalCategory]
                };

                BidiClassMap.TryGetValue(entry.bidiClass, out newData.RestrictedBidiClass);

                if (entry.simpleUppercaseMapping.HasValue)
                {
                    newData.OffsetToSimpleUppercase = (int)(entry.simpleUppercaseMapping.Value - entry.codePoint);
                }

                if (entry.simpleLowercaseMapping.HasValue)
                {
                    newData.OffsetToSimpleLowercase = (int)(entry.simpleLowercaseMapping - entry.codePoint);
                }

                if (entry.simpleTitlecaseMapping.HasValue)
                {
                    newData.OffsetToSimpleTitlecase = (int)(entry.simpleTitlecaseMapping - entry.codePoint);
                }

                caseFoldMap.TryGetValue(entry.codePoint, out newData.OffsetToSimpleCaseFold);
                newData.IsWhitespace = whitespaceCodePoints.Contains(entry.codePoint);

                if (!string.IsNullOrEmpty(entry.decimalDigitValue))
                {
                    newData.DecimalDigitValue = sbyte.Parse(entry.decimalDigitValue, NumberStyles.Integer, CultureInfo.InvariantCulture);
                }

                if (!string.IsNullOrEmpty(entry.digitValue))
                {
                    newData.DigitValue = sbyte.Parse(entry.digitValue, NumberStyles.Integer, CultureInfo.InvariantCulture);
                }

                if (!string.IsNullOrEmpty(entry.numericValue))
                {
                    string[] split = entry.numericValue.Split('/');
                    if (split.Length > 2)
                    {
                        throw new Exception($"Unexpected numeric value: {entry.numericValue}");
                    }

                    double numerator = double.Parse(split[0], CultureInfo.InvariantCulture);

                    if (split.Length == 1)
                    {
                        newData.NumericValue = numerator;
                    }
                    else
                    {
                        double denominator = double.Parse(split[1], CultureInfo.InvariantCulture);
                        newData.NumericValue = numerator / denominator;
                    }
                }

                graphemeBreakMap.TryGetValue(entry.codePoint, out newData.GraphemeBoundaryCategory);

                _data.Add(newData);
            }
        }

        public IReadOnlyList<Data1> Data => _data;

        /// <summary>
        /// Reads GraphemeBreakProperty.txt and emoji-data.txt and returns the map of code points to grapheme break properties.
        /// </summary>
        private static Dictionary<uint, GraphemeBoundaryCategory> GetGraphemeBreakPropertyMap()
        {
            Dictionary<uint, GraphemeBoundaryCategory> map = new Dictionary<uint, GraphemeBoundaryCategory>();

            foreach (string line in File.ReadAllLines("GraphemeBreakProperty.txt"))
            {
                if (PropsFileEntry.TryParseLine(line, out PropsFileEntry parsedEntry))
                {
                    for (uint i = parsedEntry.FirstCodePoint; i <= parsedEntry.LastCodePoint; i++)
                    {
                        map[i] = Enum.Parse<GraphemeBoundaryCategory>(parsedEntry.PropName);
                    }
                }
            }

            foreach (string line in File.ReadAllLines("emoji-data.txt"))
            {
                if (PropsFileEntry.TryParseLine(line, out PropsFileEntry parsedEntry)
                    && parsedEntry.PropName == "Extended_Pictographic")
                {
                    for (uint i = parsedEntry.FirstCodePoint; i <= parsedEntry.LastCodePoint; i++)
                    {
                        map[i] = GraphemeBoundaryCategory.Extended_Pictographic;
                    }
                }
            }

            return map;
        }

        /// <summary>
        /// Reads CaseFolding.txt and returns a map of code points to their "offset to case-fold" values.
        /// </summary>
        private static Dictionary<uint, int> GetSimpleCaseFoldOffsets()
        {
            Dictionary<uint, int> map = new Dictionary<uint, int>();

            // The format we expect is "<code>; <status>; <mapping>; # <name>"

            foreach (string line in File.ReadLines("CaseFolding.txt"))
            {
                string[] split = line.Split('#', 2);
                if (split.Length < 2) { continue; } // not in correct format

                split = split[0].Split(';');
                if (split.Length != 4) { continue; } // not in correct format

                string status = split[1].Trim();
                if (status != "C" && status != "S") { continue; } // not a simple mapping

                uint thisCodePoint = uint.Parse(split[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                uint mappedCodePoint = uint.Parse(split[2], NumberStyles.HexNumber, CultureInfo.InvariantCulture);

                map[thisCodePoint] = (int)(mappedCodePoint - thisCodePoint);
            }

            return map;
        }

        /// <summary>
        /// Reads PropList.txt and returns the set of code points with the "White_Space" property.
        /// </summary>
        private static HashSet<uint> GetWhiteSpaceCodePoints()
        {
            HashSet<uint> set = new HashSet<uint>();

            foreach (string line in File.ReadAllLines("PropList.txt"))
            {
                if (PropsFileEntry.TryParseLine(line, out PropsFileEntry parsedEntry)
                    && parsedEntry.PropName == "White_Space")
                {
                    for (uint i = parsedEntry.FirstCodePoint; i <= parsedEntry.LastCodePoint; i++)
                    {
                        set.Add(i);
                    }
                }
            }

            return set;
        }

        public static UnicodeDataRepository ParseFiles() => new UnicodeDataRepository();

        /// <summary>
        /// Reads UnicodeData.txt and returns a list of all entries in the file.
        /// </summary>
        /// <returns></returns>
        private static IEnumerable<UnicodeDataEntry> ReadUnicodeDataFile()
        {
            // Format is described in UAX #44: https://www.unicode.org/reports/tr44/

            UnicodeDataEntry thisEntry = new UnicodeDataEntry();

            foreach (string line in File.ReadAllLines("UnicodeData.txt"))
            {
                if (string.IsNullOrWhiteSpace(line)) { continue; }

                string[] split = line.Split(';');
                if (split.Length != 15)
                {
                    throw new Exception($"Unexpected line: {line}");
                }

                uint thisCodePoint = uint.Parse(split[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                string name = split[1];

                // Is this the end of the range which began in the previous line?
                // If so, return a duplicate value for each code point in the range.

                if (name.EndsWith(", Last>", StringComparison.Ordinal))
                {
                    // 'thisEntry' actually points to the previous entry

                    if (thisEntry.name != name[..^(", Last>".Length)] + ", First>")
                    {
                        throw new Exception($"Unexpected line: {line}");
                    }

                    for (uint i = thisEntry.codePoint; i <= thisCodePoint; i++)
                    {
                        thisEntry.codePoint = i;
                        yield return thisEntry;
                    }

                    continue;
                }

                static uint? TryParseUInt32AsHex(string value)
                {
                    return uint.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint parsed) ? (uint?)parsed : null;
                }

                thisEntry = new UnicodeDataEntry()
                {
                    codePoint = thisCodePoint,
                    name = name,
                    generalCategory = split[2],
                    bidiClass = split[4],
                    decimalDigitValue = split[6],
                    digitValue = split[7],
                    numericValue = split[8],
                    simpleUppercaseMapping = TryParseUInt32AsHex(split[12]),
                    simpleLowercaseMapping = TryParseUInt32AsHex(split[13]),
                    simpleTitlecaseMapping = TryParseUInt32AsHex(split[14]),
                };

                yield return thisEntry;
            }
        }

        // struct gives us automatic hash code calculation & equality operators
        private struct UnicodeDataEntry
        {
            public uint codePoint;
            public string name;
            public string generalCategory;
            public string bidiClass;
            public string decimalDigitValue;
            public string digitValue;
            public string numericValue;
            public uint? simpleUppercaseMapping;
            public uint? simpleLowercaseMapping;
            public uint? simpleTitlecaseMapping;
        }
    }

    internal class Data1
    {
        public uint CodePoint = 0;
        public UnicodeCategory UnicodeCategory = UnicodeCategory.OtherNotAssigned;
        public RestrictedBidiClass RestrictedBidiClass = RestrictedBidiClass.None;
        public int OffsetToSimpleUppercase = 0;
        public int OffsetToSimpleLowercase = 0;
        public int OffsetToSimpleTitlecase = 0;
        public int OffsetToSimpleCaseFold = 0;
        public bool IsWhitespace = false;
        public sbyte DecimalDigitValue = -1;
        public sbyte DigitValue = -1;
        public double NumericValue = -1;
        public GraphemeBoundaryCategory GraphemeBoundaryCategory = GraphemeBoundaryCategory.Unassigned;
    }

    internal enum GraphemeBoundaryCategory
    {
        Unassigned,
        CR,
        LF,
        Control,
        Extend,
        ZWJ,
        Regional_Indicator,
        Prepend,
        SpacingMark,
        L,
        V,
        T,
        LV,
        LVT,
        Extended_Pictographic,
    }
}
